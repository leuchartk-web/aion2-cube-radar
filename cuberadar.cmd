@echo off
rem Compile puis lance la version francaise. Ne lance jamais une ancienne compilation en cas d'erreur.
set "EXE=%~dp0src\CubeRadar.Cli\bin\Release\net10.0-windows\cuberadar.exe"
dotnet build "%~dp0src\CubeRadar.Cli\CubeRadar.Cli.csproj" -c Release -v q -nologo || exit /b 1
"%EXE%" %*
