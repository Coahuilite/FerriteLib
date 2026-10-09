@echo off
rem Cross-repo half: run the consumer's harness against the carrier this repository just built in Dev.
dotnet run --no-restore --project tools/UniversalSqueakerKernelHostTests -c Dev -p:FerriteLibArtifactPath=..\ferritelib\dist\build\Dev\FerriteLib.UiKit.dll
exit /b %ERRORLEVEL%
