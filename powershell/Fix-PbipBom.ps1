#Requires -Version 5.1
<#
.SYNOPSIS
    Strips a UTF-8 BOM from every file in a PBIP project folder.

.DESCRIPTION
    Power BI Desktop refuses to open a PBIP project if any of its files starts
    with a UTF-8 byte-order mark (BOM): "Only text with UTF8 encoding without
    BOM ... is supported."

    Run this on a project folder that was produced by an older tool / an editor
    that added a BOM. It rewrites every affected file as UTF-8 without a BOM.
    Safe to run repeatedly: files that have no BOM are left untouched.

.PARAMETER Path
    The PBIP project folder to fix. If omitted, a folder picker is shown.

.EXAMPLE
    .\Fix-PbipBom.ps1 -Path "C:\Users\me\Downloads\new t1"

.EXAMPLE
    Double-click Fix-PbipBom.cmd and pick the folder.
#>
[CmdletBinding()]
param([string]$Path)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not $Path) {
    Add-Type -AssemblyName System.Windows.Forms
    $dialog = New-Object System.Windows.Forms.FolderBrowserDialog
    $dialog.Description = 'Select the PBIP project folder to fix'
    if ($dialog.ShowDialog() -ne [System.Windows.Forms.DialogResult]::OK) {
        Write-Host 'Cancelled.'
        return
    }
    $Path = $dialog.SelectedPath
}

if (-not (Test-Path -LiteralPath $Path -PathType Container)) {
    Write-Error "Folder not found: $Path"
    exit 1
}

Write-Host "Scanning: $Path"
$script:fixed = 0

Get-ChildItem -LiteralPath $Path -Recurse -File | ForEach-Object {
    $bytes = [System.IO.File]::ReadAllBytes($_.FullName)
    if ($bytes.Length -ge 3 -and
        $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF) {
        $rest = New-Object 'byte[]' ($bytes.Length - 3)
        [Array]::Copy($bytes, 3, $rest, 0, $rest.Length)
        [System.IO.File]::WriteAllBytes($_.FullName, $rest)
        Write-Host "  Stripped BOM: $($_.FullName)"
        $script:fixed++
    }
}

Write-Host ''
if ($script:fixed -eq 0) {
    Write-Host "No UTF-8 BOM found. Nothing to change."
}
else {
    Write-Host "Done. Removed a UTF-8 BOM from $script:fixed file(s)."
    Write-Host "The PBIP project should now open in Power BI Desktop."
}
