@echo off
rem M5 for a mutation in the harness project: the artifact is the harness assembly, so that is what is rebuilt.
dotnet build tools/FerriteLib.UiKit.Tests/FerriteLib.UiKit.Tests.csproj -c Dev --no-restore --nologo -v quiet
exit /b %ERRORLEVEL%
