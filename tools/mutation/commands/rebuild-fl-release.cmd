@echo off
rem M5: rebuild the configuration a library-source mutation changed.
dotnet build Source/FerriteLib.UiKit/FerriteLib.UiKit.csproj -c Release --no-restore --nologo -v quiet
exit /b %ERRORLEVEL%
