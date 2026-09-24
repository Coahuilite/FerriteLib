@echo off
rem M5 for the consumer half: rebuild the mutated configuration so the artifact is not left poisoned.
dotnet build Source/UniversalSqueaker/UniversalSqueaker.csproj -c Dev -p:FerriteLibArtifactPath=..\ferritelib\dist\build\Dev\FerriteLib.UiKit.dll --no-restore --nologo -v quiet
exit /b %ERRORLEVEL%
