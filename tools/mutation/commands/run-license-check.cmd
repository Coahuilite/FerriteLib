@echo off
rem One-token command: the licence checker (read-only; this mutation changes no build output).
pwsh -NoProfile -File scripts/verify-license.ps1 -ProjectRoot .
exit /b %ERRORLEVEL%
