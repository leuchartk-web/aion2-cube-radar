using System.Drawing;
using System.Windows.Forms;
using static CubeRadar.Core.Localization.Text;

namespace CubeRadar.Cli;

internal sealed record OverlayActions(Action Quit, Action TestSound, Func<string> GetNorth, Action<string> SetNorth, Action OpenData);

/// <summary>Bounded, scrollable native settings. All changes apply immediately.</summary>
internal sealed class SettingsForm : Form
{
    private readonly ToolTip _tips = new() { AutoPopDelay = 15000 };
    private bool _preview;
    protected override bool ShowWithoutActivation => _preview;
    private static readonly Color Field = Color.FromArgb(27, 42, 55);
    public SettingsForm(RadarSettings settings, OverlayActions actions, Action applied, Action resetPosition)
    {
        Text = T("settingsTitle");
        Icon = OverlayWindow.AppIcon();
        BackColor = Palette.Background; ForeColor = Palette.Text;
        Font = new Font("Segoe UI", 10);
        AutoScaleMode = AutoScaleMode.Dpi;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(620, 590); MinimumSize = new Size(560, 540);
        TopMost = true; MaximizeBox = false; Padding = new Padding(18);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4 };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(new Label { Text = T("tagline"), AutoSize = true, ForeColor = Palette.Visible,
            Font = new Font(Font.FontFamily, 19, FontStyle.Bold), Margin = new Padding(0, 0, 0, 8) });
        layout.Controls.Add(Note("intro"));
        var tabs = new TabControl { Dock = DockStyle.Fill, Margin = new Padding(0, 12, 0, 12) };
        layout.Controls.Add(tabs);
        TableLayoutPanel Page(string key)
        {
            var page = new TabPage(T(key)) { BackColor = Palette.Background, ForeColor = Palette.Text, AutoScroll = true, Padding = new Padding(16) };
            var grid = new TableLayoutPanel { AutoSize = true, ColumnCount = 1, Dock = DockStyle.Top };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            page.Controls.Add(grid); tabs.TabPages.Add(page); return grid;
        }
        void Save() { settings.Save(); applied(); }
        void Heading(TableLayoutPanel panel, string key) => panel.Controls.Add(new Label { Text = T(key), AutoSize = true,
            Font = new Font(Font, FontStyle.Bold), ForeColor = Palette.Me, Margin = new Padding(0, 10, 0, 5) });
        void Row(TableLayoutPanel panel, string key, Control control)
        {
            var line = new FlowLayoutPanel { AutoSize = true, WrapContents = true, Dock = DockStyle.Top, Margin = new Padding(0, 3, 0, 6) };
            line.Controls.Add(new Label { Text = T(key), Width = 185, AutoSize = false, Height = 28, TextAlign = ContentAlignment.MiddleLeft });
            control.AccessibleName = T(key); line.Controls.Add(control); panel.Controls.Add(line);
        }
        ComboBox Combo(string[] labels, int index, Action<int> change)
        {
            var c = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 205, BackColor = Field, ForeColor = Palette.Text };
            c.Items.AddRange(labels); c.SelectedIndex = Math.Max(0, index);
            c.SelectedIndexChanged += (_, _) => change(c.SelectedIndex); return c;
        }
        void Check(TableLayoutPanel panel, string key, bool value, Action<bool> change)
        {
            var c = new CheckBox { Text = T(key), AccessibleName = T(key), AutoSize = true, MaximumSize = new Size(510, 0),
                Checked = value, Margin = new Padding(0, 6, 0, 6) };
            c.CheckedChanged += (_, _) => change(c.Checked); panel.Controls.Add(c);
        }
        void Slider(TableLayoutPanel panel, string key, int min, int value, Action<int> change)
        {
            var line = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
            var bar = new TrackBar { Minimum = min, Maximum = 100, Value = value, TickStyle = TickStyle.None, Width = 155, BackColor = Palette.Background, AccessibleName = T(key) };
            var number = new Label { Text = $"{value} %", AutoSize = true, Margin = new Padding(2, 8, 0, 0) };
            bar.ValueChanged += (_, _) => { number.Text = $"{bar.Value} %"; change(bar.Value); };
            line.Controls.Add(bar); line.Controls.Add(number); Row(panel, key, line);
        }
        var radar = Page("tabRadar");
        Heading(radar, "appearance");
        Row(radar, "range", Combo(OverlayWindow.Ranges.Select(r => $"{r} m").ToArray(), Array.IndexOf(OverlayWindow.Ranges, settings.OverlayRangeM), i => { settings.OverlayRangeM = OverlayWindow.Ranges[i]; Save(); }));
        int[] sizes = [75, 100, 125, 150];
        Row(radar, "size", Combo(sizes.Select(s => $"{s} %").ToArray(), Array.IndexOf(sizes, settings.OverlayScalePct), i => { settings.OverlayScalePct = sizes[i]; Save(); }));
        Slider(radar, "opacity", 40, settings.OverlayOpacityPct, v => { settings.OverlayOpacityPct = v; Save(); });
        string[] axes = ["-y", "+y", "+x", "-x"];
        var north = Combo([T("defaultNorth"), "+Y", "+X", "−X"], Array.IndexOf(axes, actions.GetNorth()), i => actions.SetNorth(axes[i]));
        _tips.SetToolTip(north, T("northHelp")); Row(radar, "north", north);
        Heading(radar, "visibility");
        Check(radar, "focused", settings.OverlayOnlyWhenGameFocused, v => { settings.OverlayOnlyWhenGameFocused = v; Save(); });
        Check(radar, "menus", settings.OverlayHideInMenus, v => { settings.OverlayHideInMenus = v; Save(); });
        radar.Controls.Add(Note("visibilityHelp")); radar.Controls.Add(Button("resetPosition", resetPosition));
        var audio = Page("tabSound");
        Check(audio, "soundEnabled", settings.SoundEnabled, v => { settings.SoundEnabled = v; Save(); });
        Slider(audio, "soundVolume", 5, settings.SoundVolumePct, v => { settings.SoundVolumePct = v; Save(); });
        Heading(audio, "soundFile");
        var file = new TextBox { ReadOnly = true, Width = 470, BackColor = Field, ForeColor = Palette.Text, Text = settings.SoundFile ?? "", AccessibleName = T("soundFile") };
        audio.Controls.Add(file);
        var buttons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Top };
        buttons.Controls.Add(Button("browse", () =>
        {
            using var dialog = new OpenFileDialog { Filter = T("wavFilter"), Title = T("wavTitle") };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            settings.SoundFile = file.Text = dialog.FileName; Save();
        }));
        buttons.Controls.Add(Button("defaultSound", () => { settings.SoundFile = null; file.Text = ""; Save(); }));
        buttons.Controls.Add(Button("testSound", actions.TestSound)); audio.Controls.Add(buttons); audio.Controls.Add(Note("soundHelp"));
        var help = Page("tabHelp");
        Heading(help, "helpTitle");
        foreach (var key in new[] { "legend", "shortcuts", "privacy", "limit" }) help.Controls.Add(Note(key));
        help.Controls.Add(Button("dataFolder", actions.OpenData));
        var bottom = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        var close = Button("close", Close); close.DialogResult = DialogResult.Cancel; CancelButton = close;
        bottom.Controls.Add(close); bottom.Controls.Add(Button("quit", () => { Close(); actions.Quit(); }));
        layout.Controls.Add(bottom); Controls.Add(layout);
    }
    private static Label Note(string key) => new() { Text = T(key), AutoSize = true, MaximumSize = new Size(510, 0),
        ForeColor = Palette.Muted, Margin = new Padding(0, 4, 0, 12) };
    private Button Button(string key, Action action)
    {
        var b = new Button { Text = T(key), AccessibleName = T(key), AutoSize = true, FlatStyle = FlatStyle.Flat,
            BackColor = Field, ForeColor = Palette.Text, Padding = new Padding(10, 5, 10, 5), Margin = new Padding(0, 8, 8, 0) };
        b.FlatAppearance.BorderColor = Palette.Line; b.Click += (_, _) => action(); return b;
    }
    protected override void Dispose(bool disposing) { if (disposing) _tips.Dispose(); base.Dispose(disposing); }
    public static void SavePng(string directory, RadarSettings settings, OverlayActions actions)
    {
        var thread = new Thread(() =>
        {
            using var form = new SettingsForm(settings, actions, () => { }, () => { })
            { _preview = true, StartPosition = FormStartPosition.Manual, Location = new Point(-30000, -30000), ShowInTaskbar = false };
            form.Show();
            var tabs = form.Controls.OfType<TableLayoutPanel>().Single().Controls.OfType<TabControl>().Single();
            for (var i = 0; i < tabs.TabCount; i++)
            {
                tabs.SelectedIndex = i; Application.DoEvents();
                using var image = new Bitmap(form.Width, form.Height);
                form.DrawToBitmap(image, new Rectangle(0, 0, form.Width, form.Height));
                image.Save(Path.Combine(directory, $"parametres-{i + 1}.png"));
            }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
    }
}
