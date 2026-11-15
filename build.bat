@echo off
setlocal
echo ========================================================
echo   Building Aeris Browser (Lightweight ^& Ad-Free)
echo ========================================================

set CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe

if not exist "%CSC%" (
    echo Error: C# compiler not found at %CSC%
    exit /b 1
)

echo [1/3] Copying runtime DLLs to output directory...
copy /Y "lib\*.dll" "." >nul

echo [2/3] Compiling source files with optimizations ^& app icon...
"%CSC%" /nologo /optimize+ /target:winexe /win32icon:app.ico /out:AerisBrowser.exe ^
    /r:System.dll ^
    /r:System.Core.dll ^
    /r:System.Drawing.dll ^
    /r:System.Windows.Forms.dll ^
    /r:System.Web.Extensions.dll ^
    /r:lib\Microsoft.Web.WebView2.Core.dll ^
    /recurse:src\*.cs

if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] Compilation failed!
    exit /b 1
)

echo [3/3] Build succeeded! Created AerisBrowser.exe with custom icon.
echo ========================================================
echo   Ready to launch: run.bat or .\AerisBrowser.exe
echo ========================================================
