@echo off
chcp 65001 >nul
setlocal

rem ============================================
rem  grand 构建工具 一键编译脚本
rem  需要：.NET Framework 4.x（Windows 自带）
rem ============================================

set "FW=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319"
set "VBC=%FW%\vbc.exe"
if not exist "%VBC%" (
    set "FW=%WINDIR%\Microsoft.NET\Framework\v4.0.30319"
    set "VBC=%FW%\vbc.exe"
)
if not exist "%VBC%" (
    echo [错误] 未找到 vbc.exe，请先安装 .NET Framework 4.x
    pause
    exit /b 1
)

echo 正在编译 grand...
"%VBC%" /nologo /target:exe /optionstrict- /optioninfer+ /optionexplicit+ /codepage:65001 ^
    /r:"%FW%\System.dll" ^
    /r:"%FW%\System.Core.dll" ^
    /r:"%FW%\System.IO.Compression.dll" ^
    /out:grand.exe ^
    Program.vb GrandProject.vb Template.vb

if errorlevel 1 (
    echo.
    echo [失败] 编译未通过，请查看上方错误信息。
    pause
    exit /b 1
)

echo.
echo [成功] 已生成 grand.exe
echo        运行 "grand.exe help" 查看用法。
pause
