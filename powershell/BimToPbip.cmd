@echo off
rem ==========================================================================
rem  BimToPbip launcher
rem
rem  Double-click this file to start the converter. It runs the PowerShell
rem  script next to it. No installation and no administrator rights required:
rem  PowerShell ships with every supported version of Windows.
rem
rem  -ExecutionPolicy Bypass  -> runs the script without changing machine policy
rem  -STA                     -> required for the file/folder picker dialogs
rem  %*                       -> forwards any arguments for command-line use
rem ==========================================================================
powershell.exe -NoProfile -ExecutionPolicy Bypass -STA -File "%~dp0bim-to-pbip.ps1" %*

if errorlevel 1 (
    echo.
    echo The converter exited with an error. See the messages above.
    pause
)
