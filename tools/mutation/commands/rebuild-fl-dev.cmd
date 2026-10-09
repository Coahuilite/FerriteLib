@echo off
rem M5: after a mutation is restored, rebuild the configuration it mutated.
dotnet build Source/FerriteLib.UiKit/FerriteLib.UiKit.csproj -c Dev --no-restore --nologo -v quiet
exit /b %ERRORLEVEL%
