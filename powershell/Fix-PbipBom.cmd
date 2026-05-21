@echo off
rem ==========================================================================
rem  Fix-PbipBom launcher
rem
rem  Double-click to repair a PBIP project that Power BI Desktop refuses to
rem  open with "Only text with UTF8 encoding without BOM is supported".
rem  A folder picker appears; choose the PBIP project folder.
rem ==========================================================================
powershell.exe -NoProfile -ExecutionPolicy Bypass -STA -File "%~dp0Fix-PbipBom.ps1" %*
echo.
pause
