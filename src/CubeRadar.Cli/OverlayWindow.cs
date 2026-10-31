using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using CubeRadar.Core.Radar;
using static CubeRadar.Core.Localization.Text;

namespace CubeRadar.Cli;

/// <summary>
/// Borderless, non-activating native window above AION 2. Cursor visibility and Alt never hide it.
/// Foreground filtering, manual hiding and capture pause control its normal visibility; placement and settings
/// can reveal it temporarily. No drawing into the game or exclusive-fullscreen support.
/// </summary>
internal sealed class OverlayWindow : Form
{
    private const int WsExLayered = 0x80000, WsExToolWindow = 0x80, WsExNoActivate = 0x08000000, WsExTopmost = 0x8;
    private const int WmNcHitTest = 0x84, WmMouseActivate = 0x21, WmEnterSizeMove = 0x231, WmExitSizeMove = 0x232, HtCaption = 2, MaNoActivate = 3;
    private const byte AcSrcAlpha = 0x1;
    private const int UlwAlpha = 0x2;
    private static readonly IntPtr HwndTopmost = new(-1);
    public static readonly int[] Ranges = [60, 120, 250];

    private readonly Func<RadarSnapshot> _snapshot;
    private readonly RadarSettings _settings;
    private readonly OverlayActions _actions;
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 250 };
    private SettingsForm? _settingsForm;
    private RadarSnapshot? _last;
    private int _ticks;
    private bool _moving;
    private bool _placementMode;
    private int[] _gamePids = [];
    private long _gamePidsAt = -10_000;
    private NotifyIcon? _tray;
    private ToolStripMenuItem? _pauseItem;
    private ToolStripMenuItem? _moveItem;
    private bool Paused => _actions.GetPaused?.Invoke() == true;

    public OverlayWindow(Func<RadarSnapshot> snapshot, RadarSettings settings, OverlayActions actions)
    {
        _snapshot = snapshot;
        _settings = settings;
        _actions = actions;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        BackColor = Palette.Background;
        DoubleBuffered = true;
        Text = T("app");
        ApplySettings();
        var p = new Point(settings.OverlayX ?? 0, settings.OverlayY ?? 0);
        Location = settings.OverlayX is not null && OnSomeScreen(p) ? p : DefaultLocation();
        ClampToScreen();
        _timer.Tick += (_, _) => Tick();
        _timer.Start();
    }

    /// <summary>Runs the overlay on its own UI thread; returns once the window exists.</summary>
    public static OverlayWindow Start(Func<RadarSnapshot> snapshot, RadarSettings settings, OverlayActions actions)
    {
        OverlayWindow? window = null;
        using var ready = new ManualResetEventSlim();
        var thread = new Thread(() =>
        {
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            window = new OverlayWindow(snapshot, settings, actions);
            _ = window.Handle; // the handle must exist for calls from other threads, also while hidden
            ready.Set();
            Application.Run(new ApplicationContext());
        }) { IsBackground = true, Name = "overlay" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        ready.Wait();
        return window!;
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= WsExLayered | WsExToolWindow | WsExNoActivate | WsExTopmost;
            return cp;
        }
    }

    protected override void WndProc(ref Message m)
    {
        switch (m.Msg)
        {
            case WmNcHitTest:
                var screen = new Point(unchecked((short)(long)m.LParam), unchecked((short)((long)m.LParam >> 16)));
                var point = PointToClient(screen);
                if (OverlayRenderer.GearRect(RenderScale).Contains(point) || OverlayRenderer.PauseRect(RenderScale).Contains(point) || OverlayRenderer.MoveRect(RenderScale).Contains(point) || OverlayRenderer.FindRect(RenderScale).Contains(point))
                {
                    m.Result = 1; // HTCLIENT: the gear button gets the click
                    return;
                }
                m.Result = HtCaption; // everything else is a drag handle; Windows moves it
                _moving = (MouseButtons & MouseButtons.Left) != 0;
                return;
            case WmMouseActivate:
                m.Result = MaNoActivate; // clicking it never takes the focus from the game
                return;
            case WmEnterSizeMove:
                _moving = true;
                return;
            case WmExitSizeMove:
                _moving = false;
                ClampToScreen();
                _settings.OverlayX = Left;
                _settings.OverlayY = Top;
                _settings.Save();
                break;
        }
        base.WndProc(ref m);
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left && OverlayRenderer.GearRect(RenderScale).Contains(e.Location)) OpenSettings();
        else if (e.Button == MouseButtons.Left && OverlayRenderer.PauseRect(RenderScale).Contains(e.Location)) _actions.SetPaused?.Invoke(!Paused);
        else if (e.Button == MouseButtons.Left && OverlayRenderer.MoveRect(RenderScale).Contains(e.Location)) TogglePlacement();
        else if (e.Button == MouseButtons.Left && OverlayRenderer.FindRect(RenderScale).Contains(e.Location)) _actions.FindPosition?.Invoke();
        base.OnMouseClick(e);
    }

    /// <summary>Overlay scale: screen DPI times the user's size setting.</summary>
    private float RenderScale => DeviceDpi / 96f * Math.Clamp(_settings.OverlayScalePct, 50, 200) / 100f;

    /// <summary>Re-reads size and opacity (called after every change in the settings window).</summary>
    public void ApplySettings()
    {
        Size = OverlayRenderer.Measure(RenderScale);
        if (IsHandleCreated) ClampToScreen();
        Redraw();
    }

    /// <summary>
    /// Paints the overlay into a bitmap and hands it to Windows with per-pixel alpha (UpdateLayeredWindow), so the radar
    /// and the enemy list below it each get their own opacity. A window updated this way does not use WM_PAINT.
    /// </summary>
    private void Redraw()
    {
        var size = ClientSize;
        if (!IsHandleCreated || size.Width <= 0 || size.Height <= 0) return;
        using var frame = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(frame))
            OverlayRenderer.Render(g, size, _last, RenderScale, _settings.OverlayRangeM, Paused, _placementMode);
        OverlayRenderer.ApplyOpacity(frame, Math.Clamp(_settings.OverlayOpacityPct, 40, 100));
        var bitmap = frame.GetHbitmap(Color.FromArgb(0)); // premultiplied alpha, as UpdateLayeredWindow needs it
        var dc = CreateCompatibleDC(IntPtr.Zero);
        var old = SelectObject(dc, bitmap);
        try
        {
            var source = Point.Empty;
            var blend = new BlendFunction { SourceConstantAlpha = 255, AlphaFormat = AcSrcAlpha };
            UpdateLayeredWindow(Handle, IntPtr.Zero, IntPtr.Zero, ref size, dc, ref source, 0, ref blend, UlwAlpha);
        }
        finally
        {
            SelectObject(dc, old);
            DeleteDC(dc);
            DeleteObject(bitmap);
        }
    }

    public void OpenSettings() => Ui(() =>
    {
        if (_settingsForm is { IsDisposed: false })
        {
            _settingsForm.Activate();
            return;
        }
        _settingsForm = new SettingsForm(_settings, _actions, ApplySettings, ResetPosition, () =>
        {
            if (!_placementMode) TogglePlacement();
        });
        _settingsForm.Show();
    });

    /// <summary>Ctrl+Shift+F11: settings open (overlay visible to move and zoom it) or closed again.</summary>
    public void ToggleSettings() => Ui(() =>
    {
        if (SettingsOpen) _settingsForm!.Close();
        else OpenSettings();
    });

    private bool SettingsOpen => _settingsForm is { IsDisposed: false, Visible: true };

    private void ResetPosition()
    {
        Location = DefaultLocation();
        _settings.OverlayX = _settings.OverlayY = null;
        _settings.Save();
    }

    private Point DefaultLocation()
    {
        var screen = Screen.FromHandle(GetForegroundWindow()).WorkingArea;
        return new Point(Math.Max(screen.Left, screen.Right - Width - (int)(24 * RenderScale)), screen.Top + (int)(24 * RenderScale));
    }

    private void ClampToScreen()
    {
        var screen = Screen.FromRectangle(Bounds).WorkingArea;
        Location = new Point(Math.Clamp(Left, screen.Left, Math.Max(screen.Left, screen.Right - Width)),
            Math.Clamp(Top, screen.Top, Math.Max(screen.Top, screen.Bottom - Height)));
    }

    /// <summary>Placement reveals a hidden or paused radar without resuming its capture.</summary>
    public void TogglePlacement() => Ui(() =>
    {
        _placementMode = !_placementMode;
        if (_placementMode) ClampToScreen();
        UpdateVisibility();
        Redraw();
        if (_placementMode) Notify(T("moveHelp"));
    });

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        var i = Math.Max(0, Array.IndexOf(Ranges, _settings.OverlayRangeM));
        i = Math.Clamp(i + (e.Delta > 0 ? -1 : 1), 0, Ranges.Length - 1); // wheel up = zoom in
        _settings.OverlayRangeM = Ranges[i];
        _settings.Save();
        Redraw();
    }

    /// <summary>Ctrl+Shift+F10: the user's on/off switch (auto-hiding works on top of it).</summary>
    public void ToggleVisible() => Ui(() =>
    {
        _placementMode = false;
        _settings.OverlayVisible = !_settings.OverlayVisible;
        _settings.Save();
        UpdateVisibility();
    });

    public void CycleRange() => Ui(() =>
    {
        var i = Array.IndexOf(Ranges, _settings.OverlayRangeM);
        _settings.OverlayRangeM = Ranges[(i + 1) % Ranges.Length];
        _settings.Save();
        Redraw();
    });

    public void Shutdown() => Ui(() =>
    {
        if (_tray is not null)
        {
            _tray.Visible = false; // otherwise a dead icon stays in the tray until the mouse passes over it
            _tray.Dispose();
        }
        Close();
        Application.ExitThread();
    });

    /// <summary>Tray icon with a menu: overlay on/off, range, web view, quit; and an update entry once one is found.</summary>
    public void ShowTray() => Ui(() =>
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add(T("traySettings"), null, (_, _) => OpenSettings());
        _pauseItem = new ToolStripMenuItem(T(Paused ? "resume" : "pause"), null, (_, _) => _actions.SetPaused?.Invoke(!Paused));
        _moveItem = new ToolStripMenuItem(T("move"), null, (_, _) => TogglePlacement());
        menu.Items.Add(_pauseItem);
        menu.Items.Add(_moveItem);
        menu.Items.Add(T("findPosition"), null, (_, _) => _actions.FindPosition?.Invoke());
        menu.Items.Add(T("toggle"), null, (_, _) => ToggleVisible());
        menu.Items.Add(T("zoom"), null, (_, _) => CycleRange());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(T("quit"), null, (_, _) => _actions.Quit());
        _tray = new NotifyIcon
        {
            Icon = AppIcon(),
            Text = T("app"),
            ContextMenuStrip = menu,
            Visible = true,
        };
        _tray.DoubleClick += (_, _) => OpenSettings();
    });

    public void Notify(string message) => Ui(() => _tray?.ShowBalloonTip(8000, T("alertTitle"), message, ToolTipIcon.Info));

    /// <summary>The program icon (assets/cuberadar.ico, compiled into the exe); a drawn glyph if that fails.</summary>
    public static Icon AppIcon()
    {
        try
        {
            if (Environment.ProcessPath is { } exe && Icon.ExtractAssociatedIcon(exe) is { } icon) return icon;
        }
        catch (Exception ex) when (ex is ArgumentException or IOException)
        {
        }
        return TrayGlyph();
    }

    /// <summary>A small radar glyph: rings and a green cube.</summary>
    private static Icon TrayGlyph()
    {
        using var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var bg = new SolidBrush(Palette.Background);
            g.FillEllipse(bg, 0, 0, 31, 31);
            using var ring = new Pen(Palette.Me, 2);
            g.DrawEllipse(ring, 2, 2, 27, 27);
            g.DrawEllipse(ring, 10, 10, 11, 11);
            using var cube = new SolidBrush(Palette.Visible);
            g.FillRectangle(cube, 19, 6, 8, 8);
        }
        return Icon.FromHandle(bmp.GetHicon());
    }

    private void Ui(Action action)
    {
        if (IsDisposed) return;
        if (InvokeRequired) BeginInvoke(action);
        else action();
    }

    private void Tick()
    {
        UpdateVisibility();
        if (_pauseItem is not null) _pauseItem.Text = T(Paused ? "resume" : "pause");
        if (_moveItem is not null) _moveItem.Text = T(_placementMode ? "endMove" : "move");
        _timer.Interval = Visible && !Paused ? 250 : 1000;
        if (!Visible) return;
        _last = _snapshot();
        // Some games push themselves to the top when they regain focus: re-assert every 2 s.
        if (++_ticks % 8 == 0) SetWindowPos(Handle, HwndTopmost, 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010);
        Redraw();
    }

    private void UpdateVisibility()
    {
        var show = _moving || _placementMode || SettingsOpen || (!Paused && _settings.OverlayVisible && WantedOverGame());
        if (show == Visible) return;
        if (show)
        {
            _last = _snapshot();
            Redraw(); // the content must be there before the window shows
            Show(); // without activation (ShowWithoutActivation)
        }
        else Hide();
    }

    /// <summary>Only while the game is in front when foreground filtering is enabled.</summary>
    private bool WantedOverGame()
    {
        if (SettingsOpen) return true; // the user is adjusting it: keep it visible to drag and zoom
        var now = Environment.TickCount64;
        if (now - _gamePidsAt > 5_000)
        {
            _gamePids = CubeRadar.Core.Capture.GameProcessLocator.FindGameProcessIds();
            _gamePidsAt = now;
        }
        if (_gamePids.Length == 0) return !_settings.OverlayOnlyWhenGameFocused;

        GetWindowThreadProcessId(GetForegroundWindow(), out var pid);
        return WantedForForeground(pid);
    }

    private bool WantedForForeground(uint pid)
    {
        // Our own window is in front (tray menu, a dialog): leave the overlay as it is. Showing it now would close the
        // tray menu that is just opening (right-click on the tray icon brings our process to the front).
        if (pid == Environment.ProcessId) return Visible;
        if (!_gamePids.Contains((int)pid)) return !_settings.OverlayOnlyWhenGameFocused;

        // Never infer a menu from a visible cursor: Alt uses that cursor to reach our buttons,
        // and Windows may hide the keyboard state of an elevated game from this normal process.
        return true;
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _timer.Dispose();
        base.OnFormClosed(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _timer.Dispose();
        base.Dispose(disposing);
    }

    /// <summary>Native Windows smoke checks on the CI runner, with no game, capture, hotkeys or sound.</summary>
    internal static void VerifyControls()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var paused = true;
                var searches = 0;
                var settings = new RadarSettings { OverlayVisible = false, OverlayOnlyWhenGameFocused = false };
                var engine = new RadarEngine(new RadarConfig(), trackPlayers: false);
                var actions = new OverlayActions(() => { }, () => { }, () => "-y", _ => { }, () => { },
                    GetPaused: () => paused, SetPaused: value => paused = value, FindPosition: () => searches++);
                using var window = new OverlayWindow(engine.Snapshot, settings, actions);
                _ = window.Handle;
                void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
                var legacyPath = Path.GetTempFileName();
                try
                {
                    File.WriteAllText(legacyPath, "{\"OverlayHideInMenus\":true,\"CapturePaused\":true}");
                    var imported = RadarSettings.Load(legacyPath);
                    using var migrated = new OverlayWindow(engine.Snapshot, imported, actions);
                    migrated._gamePids = [42];
                    Require(migrated.WantedForForeground(42), "Legacy cursor/menu hiding must not hide a foreground game radar");
                    Require(imported.CapturePaused, "Legacy settings migration must preserve explicit capture pause");
                }
                finally { File.Delete(legacyPath); }
                window.UpdateVisibility();
                Require(!window.Visible, "Paused capture must hide the normal overlay");
                window.TogglePlacement();
                Require(window.Visible, "Placement must reveal even a paused or manually hidden overlay");
                var enter = Message.Create(window.Handle, WmEnterSizeMove, IntPtr.Zero, IntPtr.Zero);
                window.WndProc(ref enter);
                window.Location = new Point(-30000, -30000);
                var exit = Message.Create(window.Handle, WmExitSizeMove, IntPtr.Zero, IntPtr.Zero);
                window.WndProc(ref exit);
                Require(!window._moving && settings.OverlayX == window.Left && settings.OverlayY == window.Top,
                    "Dragging must save the clamped position");
                Require(Screen.AllScreens.Any(s => s.WorkingArea.Contains(window.Location)), "Radar must remain reachable");
                window.TogglePlacement();
                Require(!window.Visible, "Ending placement must return to pause");
                paused = false;
                settings.OverlayVisible = true;
                window.UpdateVisibility();
                Require(window.Visible, "Active borderless overlay must be visible when focus filtering is disabled");
                var searchRect = OverlayRenderer.FindRect(window.RenderScale);
                window.OnMouseClick(new MouseEventArgs(MouseButtons.Left, 1, searchRect.Left + 2, searchRect.Top + 2, 0));
                Require(searches == 1 && window.Visible, "Position search button must call its action without hiding the radar");
                var rect = OverlayRenderer.PauseRect(window.RenderScale);
                window.OnMouseClick(new MouseEventArgs(MouseButtons.Left, 1, rect.Left + 2, rect.Top + 2, 0));
                window.Tick();
                Require(paused && !window.Visible, "Pause button must request pause and hide the radar");
                paused = false;
                window.UpdateVisibility();
                window.ToggleVisible();
                Require(!window.Visible && !paused, "Hiding must not request a capture pause");
                window.Close();
            }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
        if (failure is not null) throw new InvalidOperationException("Windows overlay interaction checks failed", failure);
        Console.WriteLine("Windows overlay checks passed: placement, saved bounds, pause button, visibility.");
    }

    private static bool OnSomeScreen(Point p) => Screen.AllScreens.Any(s => s.WorkingArea.Contains(p));

    [StructLayout(LayoutKind.Sequential)]
    private struct BlendFunction
    {
        public byte BlendOp;
        public byte BlendFlags;
        public byte SourceConstantAlpha;
        public byte AlphaFormat;
    }

    [DllImport("user32.dll")]
    private static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, IntPtr pptDst, ref Size psize, IntPtr hdcSrc,
        ref Point pptSrc, int crKey, ref BlendFunction pblend, int flags);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr hdc, IntPtr obj);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr obj);



    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr insertAfter, int x, int y, int cx, int cy, uint flags);
}

internal static class Palette
{
    public static readonly Color Background = Color.FromArgb(12, 23, 32);
    public static readonly Color Text = Color.FromArgb(230, 235, 240);
    public static readonly Color Muted = Color.FromArgb(139, 150, 163);
    public static readonly Color Line = Color.FromArgb(42, 66, 80);
    public static readonly Color Visible = Color.FromArgb(85, 230, 175);
    public static readonly Color OutOfView = Color.FromArgb(242, 179, 61);
    public static readonly Color Opening = Color.FromArgb(255, 225, 77);
    public static readonly Color Me = Color.FromArgb(109, 189, 255);
    public static readonly Color Warn = Color.FromArgb(255, 107, 91);
}

/// <summary>Draws the mini radar; also used to render a PNG for checking the layout.</summary>
internal static class OverlayRenderer
{
    private const float Side = 260, Header = 30, Margin = 21, Footer = 65;
    private const double LevelMetres = 3, SendRadiusM = 100;

    public static Size Measure(float scale) =>
        new((int)(Side * scale), (int)((Side + Header + Footer) * scale));

    /// <summary>The settings button in the header, in client pixels.</summary>
    public static Rectangle GearRect(float scale) =>
        Rectangle.Round(new RectangleF((Side - 24) * scale, 1 * scale, 22 * scale, Header * scale - 2 * scale));

    public static Rectangle PauseRect(float scale) => Rectangle.Round(new RectangleF((Side - 63) * scale, (Header + Side) * scale, 59 * scale, 19 * scale));
    public static Rectangle MoveRect(float scale) => Rectangle.Round(new RectangleF((Side - 72) * scale, (Header + Side + 20) * scale, 68 * scale, 19 * scale));
    public static Rectangle FindRect(float scale) => Rectangle.Round(new RectangleF(6 * scale, (Header + Side + 43) * scale, (Side - 12) * scale, 19 * scale));

    public static void Render(Graphics g, Size size, RadarSnapshot? s, float scale, int rangeM, bool paused = false, bool placement = false)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        g.Clear(Palette.Background);
        g.ScaleTransform(scale, scale);

        using var small = new Font("Segoe UI", 8f, FontStyle.Bold, GraphicsUnit.Point);
        using var label = new Font("Segoe UI", 8.5f, FontStyle.Bold, GraphicsUnit.Point);
        using var tiny = new Font("Segoe UI", 7f, FontStyle.Regular, GraphicsUnit.Point);

        using (var border = new Pen(Palette.Line, 1))
            g.DrawRectangle(border, 0.5f, 0.5f, Side - 1, Side + Header + Footer - 1);

        // header: name, own position age, range
        Text(g, T(placement ? "placementTitle" : "radarTitle"), small, Palette.Visible, 10, 8);
        Text(g, $"{rangeM} m", small, Palette.Muted, Side - 32, 8, right: true);
        using (var gear = new Font("Segoe UI Symbol", 10.5f, FontStyle.Regular, GraphicsUnit.Point))
            Text(g, "⚙", gear, Palette.Text, Side - 14, 6, center: true);
        if (paused) s = null;
        var active = s?.Cubes.Where(c => c.Status is nameof(CubeStatus.Visible) or nameof(CubeStatus.Opening)).ToList() ?? [];
        var nearest = active.Where(c => c.DistanceM is not null).OrderBy(c => c.DistanceM).FirstOrDefault();
        Text(g, paused ? T("pausedTitle") : nearest is null ? T("noCubes") : T("nearest", nearest.DistanceM), small, Palette.Text, 10, Header + Side + 2);
        Text(g, paused ? T("captureOff") : T("cubeCount", active.Count), tiny, Palette.Muted, 10, Header + Side + 21);
        using (var buttonBorder = new Pen(Palette.Line))
        {
            g.DrawRectangle(buttonBorder, Side - 63, Header + Side, 59, 19);
            g.DrawRectangle(buttonBorder, Side - 72, Header + Side + 20, 68, 19);
            g.DrawRectangle(buttonBorder, 6, Header + Side + 43, Side - 12, 19);
        }
        Text(g, T(paused ? "resumeShort" : "pauseShort"), tiny, Palette.Visible, Side - 34, Header + Side + 3, center: true);
        Text(g, T(placement ? "endMoveShort" : "moveShort"), tiny, Palette.Me, Side - 38, Header + Side + 23, center: true);
        Text(g, T("findPosition"), small, paused ? Palette.Muted : Palette.Me, Side / 2, Header + Side + 44, center: true);
        var me = Me(s);
        if (s is not null)
        {
            var (posText, posColor) = me switch
            {
                { Ring: > 0 } m => ($"± {m.Ring / 100:0} m", Palette.OutOfView),
                { } m => (T("positionAge", m.AgeMs / 1000), m.AgeMs <= s.FixFreshSeconds * 1000 ? Palette.Visible : m.AgeMs <= 30_000 ? Palette.OutOfView : Palette.Warn),
                null => ("—", Palette.Warn),
            };
            var tw = g.MeasureString(posText, small).Width;
            var px = Side / 2 - tw / 2 + 5;
            using var dot = new SolidBrush(posColor);
            g.FillEllipse(dot, px - 10, 13, 6, 6);
            Text(g, posText, small, Palette.Muted, px, 8);
        }

        var cx = Side / 2;
        var cy = Header + Side / 2;
        var r = Side / 2 - Margin;
        var px2world = r / (rangeM * 100f); // pixels per game unit

        // rings, cross, north
        using (var ring = new Pen(Palette.Line, 1))
        {
            g.DrawEllipse(ring, cx - r, cy - r, 2 * r, 2 * r);
            g.DrawEllipse(ring, cx - r / 2, cy - r / 2, r, r);
            g.DrawLine(ring, cx - r, cy, cx + r, cy);
            g.DrawLine(ring, cx, cy - r, cx, cy + r);
        }
        Text(g, $"{rangeM / 2} m", tiny, Palette.Muted, cx + 3, cy - r / 2 - 12);
        Text(g, "N", small, Palette.Text, cx - 4, cy - r - 13);
        if (SendRadiusM < rangeM)
        {
            using var send = new Pen(Color.FromArgb(110, Palette.Me), 1) { DashStyle = DashStyle.Dash };
            var sr = (float)(SendRadiusM * 100 * px2world);
            g.DrawEllipse(send, cx - sr, cy - sr, 2 * sr, 2 * sr);
        }

        if (s is null || me is null)
        {
            Text(g, paused ? T("pausedTitle") : s is null || s.Frames == 0 ? T("waiting") : T("positionUnknown"), label, Palette.Muted, cx, cy + r / 2, center: true);
            if (paused) Text(g, T("resumeHint"), tiny, Palette.Muted, cx, cy + r / 2 + 16, center: true);
            else if (s is not null) Text(g, T("positionHint"), tiny, Palette.Muted, cx, cy + r / 2 + 16, center: true);
            return;
        }

        var o = s.Orientation;
        PointF ToScreen(double x, double y)
        {
            double dx = x - me.X, dy = y - me.Y;
            var n = dx * o.NorthX + dy * o.NorthY;
            var e = dx * o.EastX + dy * o.EastY;
            return new PointF(cx + (float)(e * px2world), cy - (float)(n * px2world));
        }

        // own trail (last ~2 minutes): shows where we came from, i.e. roughly where we are heading
        using (var trail = new Pen(Color.FromArgb(90, Palette.Me), 1.5f))
        {
            PointF? prev = null;
            foreach (var p in s.Trail.TakeLast(40))
            {
                if (p.Length < 2) { prev = null; continue; }
                var q = ToScreen(p[0], p[1]);
                if (prev is { } a && Inside(a, cx, cy, r) && Inside(q, cx, cy, r)) g.DrawLine(trail, a, q);
                prev = q;
            }
        }

        // us
        if (me.Ring > 0)
        {
            using var unc = new Pen(Color.FromArgb(140, Palette.Me), 1) { DashStyle = DashStyle.Dot };
            var ur = Math.Max(4f, (float)(me.Ring * px2world));
            g.DrawEllipse(unc, cx - ur, cy - ur, 2 * ur, 2 * ur);
        }
        using (var meBrush = new SolidBrush(me.AgeMs <= s.FixFreshSeconds * 1000 ? Palette.Me : Color.FromArgb(130, Palette.Me)))
            g.FillEllipse(meBrush, cx - 4.5f, cy - 4.5f, 9, 9);

        // cubes, nearest drawn last (on top)
        var live = s.Cubes.Where(c => c.Status is nameof(CubeStatus.Visible) or nameof(CubeStatus.Opening) or nameof(CubeStatus.OutOfView))
            .OrderByDescending(c => c.DistanceM ?? double.MaxValue).ToList();
        foreach (var c in live)
        {
            var color = c.Status switch
            {
                nameof(CubeStatus.Visible) => Palette.Visible,
                nameof(CubeStatus.Opening) => Palette.Opening,
                _ => Palette.OutOfView,
            };
            var dist = Math.Sqrt(Math.Pow(c.X - me.X, 2) + Math.Pow(c.Y - me.Y, 2)) / 100;
            (string Kind, string Text) height = c.DzUnits is { } dz
                ? Math.Abs(dz / 100) <= LevelMetres ? ("level", "≈") : dz > 0 ? ("up", $"▲{dz / 100:0}") : ("down", $"▼{-dz / 100:0}")
                : ("level", "");
            var text = $"{dist:0} m {height.Text}".Trim();
            var p = ToScreen(c.X, c.Y);
            if (Inside(p, cx, cy, r - 4))
            {
                Marker(g, p.X, p.Y, 6.5f, height.Kind, color, filled: c.Status != nameof(CubeStatus.OutOfView));
                var lx = p.X + 9;
                if (lx + g.MeasureString(text, label).Width > Side - 3) lx = p.X - 9 - g.MeasureString(text, label).Width;
                Text(g, text, label, color, lx, p.Y - 8);
            }
            else
            {
                // beyond the range: an open chevron on the rim (not a filled triangle, which would read as ▲/▼ height)
                var a = Math.Atan2(p.Y - cy, p.X - cx);
                var ax = cx + (float)Math.Cos(a) * (r - 7);
                var ay = cy + (float)Math.Sin(a) * (r - 7);
                var state = g.Save();
                g.TranslateTransform(ax, ay);
                g.RotateTransform((float)(a * 180 / Math.PI));
                using (var chevron = new Pen(color, 2.5f) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round })
                {
                    g.DrawLines(chevron, [new PointF(-4, -7), new PointF(5, 0), new PointF(-4, 7)]);
                    g.DrawLines(chevron, [new PointF(-10, -7), new PointF(-1, 0), new PointF(-10, 7)]);
                }
                g.Restore(state);
                var tx = cx + (float)Math.Cos(a) * (r - 30);
                var ty = cy + (float)Math.Sin(a) * (r - 22);
                Text(g, text, label, color, tx, ty - 7, center: true);
            }
        }
    }

    /// <summary>Scales the alpha of the radar (header and map) and of the enemy list below it to the given percentages.</summary>
    public static void ApplyOpacity(Bitmap frame, int radarPct)
    {
        var data = frame.LockBits(new Rectangle(Point.Empty, frame.Size), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
        try
        {
            var row = new byte[frame.Width * 4];
            for (var y = 0; y < frame.Height; y++)
            {
                var pct = radarPct;
                var line = data.Scan0 + y * data.Stride;
                Marshal.Copy(line, row, 0, row.Length);
                for (var i = 3; i < row.Length; i += 4) row[i] = (byte)(row[i] * pct / 100);
                Marshal.Copy(row, 0, line, row.Length);
            }
        }
        finally
        {
            frame.UnlockBits(data);
        }
    }

    private sealed record MeView(double X, double Y, long AgeMs, double Ring);

    /// <summary>Our position: the later of real fix and ring estimate (as in the web UI).</summary>
    private static MeView? Me(RadarSnapshot? s)
    {
        if (s is null) return null;
        if (s.Ring is { } r && (s.Self is null || r.TimeMs > s.Self.TimeMs)) return new MeView(r.X, r.Y, r.AgeMs, r.Uncertainty);
        return s.Self is { } f ? new MeView(f.X, f.Y, f.AgeMs, 0) : null;
    }

    private static bool Inside(PointF p, float cx, float cy, float r) => (p.X - cx) * (p.X - cx) + (p.Y - cy) * (p.Y - cy) <= r * r;

    /// <summary>Square for "same height", triangle up / down for above / below us.</summary>
    private static void Marker(Graphics g, float x, float y, float s, string kind, Color color, bool filled)
    {
        PointF[] pts = kind switch
        {
            "up" => [new(x, y - s), new(x + s, y + s * 0.8f), new(x - s, y + s * 0.8f)],
            "down" => [new(x, y + s), new(x + s, y - s * 0.8f), new(x - s, y - s * 0.8f)],
            _ => [new(x - s * 0.8f, y - s * 0.8f), new(x + s * 0.8f, y - s * 0.8f), new(x + s * 0.8f, y + s * 0.8f), new(x - s * 0.8f, y + s * 0.8f)],
        };
        if (filled)
        {
            using var brush = new SolidBrush(color);
            g.FillPolygon(brush, pts);
        }
        else
        {
            using var pen = new Pen(color, 2);
            g.DrawPolygon(pen, pts);
        }
    }

    private static void Text(Graphics g, string text, Font font, Color color, float x, float y, bool right = false, bool center = false)
    {
        using var brush = new SolidBrush(color);
        var w = g.MeasureString(text, font).Width;
        if (right) x -= w;
        else if (center) x -= w / 2;
        g.DrawString(text, font, brush, x, y);
    }

    /// <summary>One frame as PNG (for checking the layout without a game).</summary>
    public static void SavePng(string path, RadarSnapshot? s, int rangeM = 120, float scale = 1.5f, bool paused = false, bool placement = false)
    {
        var size = Measure(scale);
        using var bmp = new Bitmap(size.Width, size.Height);
        using (var g = Graphics.FromImage(bmp)) Render(g, size, s, scale, rangeM, paused, placement);
        bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
    }
}
