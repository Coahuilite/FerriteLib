@echo off
rem M5 for a stub-layout mutation. A rename leaves the OLD folder in bin/stubs and rebuilding does not remove
rem it, so the leftover IS the poison: drop anything that is not one of the four published folders, then
rem rebuild the stubs through the harness project's own target.
for /d %%D in ("tools\FerriteLib.UiKit.Tests\bin\stubs\*") do (
  if /i not "%%~nxD"=="unityengine" if /i not "%%~nxD"=="unityengine-imgui" if /i not "%%~nxD"=="unityengine-textrendering" if /i not "%%~nxD"=="verse" rmdir /s /q "%%D"
)
dotnet build tools\FerriteLib.UiKit.Tests\FerriteLib.UiKit.Tests.csproj -c Release --no-restore --nologo -v quiet
exit /b %ERRORLEVEL%
