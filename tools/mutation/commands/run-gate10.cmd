@echo off
rem One-token command: gate 10, which runs the Dev harness itself and reads its output.
pwsh -NoProfile -File scripts/verify-dev-instrument.ps1 -ProjectRoot .
exit /b %ERRORLEVEL%
