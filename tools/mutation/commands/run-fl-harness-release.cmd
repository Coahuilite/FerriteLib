@echo off
rem One-token command: the Release harness run (the boundary and stub lanes live in both configurations).
dotnet run --no-restore --project tools/FerriteLib.UiKit.Tests -c Release
exit /b %ERRORLEVEL%
