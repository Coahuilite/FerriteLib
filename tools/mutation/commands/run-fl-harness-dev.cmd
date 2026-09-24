@echo off
rem One-token command for the battery: a [string[]] cannot cross a `pwsh -File` boundary as an array.
dotnet run --no-restore --project tools/FerriteLib.UiKit.Tests -c Dev
exit /b %ERRORLEVEL%
