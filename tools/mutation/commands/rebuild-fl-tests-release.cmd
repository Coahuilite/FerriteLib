@echo off
dotnet build tools/FerriteLib.UiKit.Tests/FerriteLib.UiKit.Tests.csproj -c Release --no-restore --nologo -v quiet
exit /b %ERRORLEVEL%
