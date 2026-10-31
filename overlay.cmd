@echo off
rem Radar natif francais uniquement. Aucun serveur web ni journal de session.
rem Builds first. A radar that is still closing gets up to 5 s, so the build is not blocked by its files.
setlocal
set "EXE=%~dp0src\CubeRadar.Cli\bin\Release\net10.0-windows\cuberadar.exe"
set tries=0
:wait
tasklist /FI "IMAGENAME eq cuberadar.exe" 2>nul | find /I "cuberadar.exe" >nul || goto build
set /a tries+=1
if %tries% geq 5 (
  echo Le radar fonctionne deja. Quittez-le depuis son icone pres de l'horloge Windows.
  exit /b 1
)
timeout /t 1 /nobreak >nul
goto wait
:build
dotnet build "%~dp0src\CubeRadar.Cli\CubeRadar.Cli.csproj" -c Release -p:CubeRadarWindowsApp=true -v q -nologo || (
  echo Echec de la compilation. Fermez le radar puis consultez les erreurs ci-dessus.
  exit /b 1
)
start "Radar de cubes FR" "%EXE%"
