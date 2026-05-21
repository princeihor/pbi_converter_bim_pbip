#Requires -Version 5.1
<#
.SYNOPSIS
    Converts a Tabular model.bim into a Power BI Desktop project (PBIP).

.DESCRIPTION
    A no-install, no-admin, no-internet converter. Windows PowerShell ships with
    every supported Windows version, so this script "just runs":

      * Double-click BimToPbip.cmd  -> opens a small GUI window.
      * Or run it from a console with -BimPath ... for scripting / CI.

    It produces the "PBIP with TMSL" layout exactly as Power BI Desktop itself
    saves it (a .bim file IS TMSL JSON, so it is stored directly as model.bim):

      <root>/
        <name>.pbip
        <name>.SemanticModel/
          definition.pbism
          model.bim                 <- the input .bim, copied verbatim
        <name>.Report/
          definition.pbir
          report.json               <- a blank, openable report bound to the model

    The PBIP file structure is NOT defined in this script. It is loaded from the
    'pbip-templates' folder (the single source of truth, taken from a real Power
    BI Desktop export). See pbip-templates/REFERENCE.md for the field-by-field
    rationale. No pbi-tools, no .NET install, no network access are required.

.PARAMETER BimPath
    Path to the input model.bim file. When supplied, the script runs headless.

.PARAMETER OutputRoot
    PBIP project root folder. Defaults to a sub-folder named after the dataset,
    next to the .bim file.

.PARAMETER DatasetName
    Dataset / project name. Defaults to the .bim file name without extension.

.PARAMETER NoGui
    Never show the GUI. Without -BimPath this just reports an error.

.EXAMPLE
    .\bim-to-pbip.ps1
    Opens the GUI window.

.EXAMPLE
    .\bim-to-pbip.ps1 -BimPath "C:\Models\MyModel.bim"

.EXAMPLE
    .\bim-to-pbip.ps1 -BimPath "C:\Models\MyModel.bim" -OutputRoot "C:\PBIP\MyModel" -DatasetName "MyModel"
#>
[CmdletBinding()]
param(
    [string]$BimPath,
    [string]$OutputRoot,
    [string]$DatasetName,
    [switch]$NoGui
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$script:AppName = 'BimToPbip'

# ===========================================================================
#  Logging
# ===========================================================================

# Console logger: a script block invoked as  & $log <level> <message>
$script:ConsoleLog = {
    param([string]$Level, [string]$Message)
    switch ($Level) {
        'error'   { Write-Host "[ ERROR ] $Message" -ForegroundColor Red }
        'warn'    { Write-Host "[ WARN ] $Message"  -ForegroundColor Yellow }
        'step'    { Write-Host "[ STEP ] $Message"  -ForegroundColor Cyan }
        'success' { Write-Host "[  OK  ] $Message"  -ForegroundColor Green }
        'detail'  { Write-Host $Message }
        default   { Write-Host "[ INFO ] $Message" }
    }
}

# ===========================================================================
#  UTF-8 (no BOM) file helpers
#
#  Power BI Desktop rejects PBIP files that start with a UTF-8 byte-order mark
#  ("Only text with UTF8 encoding without BOM ... is supported"). Windows
#  PowerShell 5.1's `Set-Content -Encoding UTF8` writes UTF-8 *with* a BOM, and
#  `-Encoding UTF8NoBOM` only exists in PowerShell 7+. So every file this tool
#  writes goes through Write-Utf8NoBom, and the finished project is swept once
#  more with Remove-Utf8Bom to strip a BOM from anything else (e.g. a model.bim
#  that already contained a BOM).
# ===========================================================================

function Write-Utf8NoBom {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][AllowEmptyString()][string]$Content
    )
    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($Path, $Content, $utf8NoBom)
}

function Remove-Utf8Bom {
    # Strips a leading UTF-8 BOM (EF BB BF) from a file in place.
    # Returns $true if a BOM was found and removed, otherwise $false.
    param([Parameter(Mandatory)][string]$Path)

    $bytes = [System.IO.File]::ReadAllBytes($Path)
    if ($bytes.Length -ge 3 -and
        $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF) {
        $rest = New-Object 'byte[]' ($bytes.Length - 3)
        [Array]::Copy($bytes, 3, $rest, 0, $rest.Length)
        [System.IO.File]::WriteAllBytes($Path, $rest)
        return $true
    }
    return $false
}

function Test-FileHasBom {
    param([Parameter(Mandatory)][string]$Path)
    $bytes = [System.IO.File]::ReadAllBytes($Path)
    return ($bytes.Length -ge 3 -and
            $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF)
}

# ===========================================================================
#  PBIP templates  (single source of truth = the 'pbip-templates' folder)
# ===========================================================================

function Resolve-TemplatesDir {
    <#
        Locates the 'pbip-templates' folder. The PBIP structure lives there and
        nowhere else, so it must be shipped alongside this script. Looked up
        next to the script and one level up (repository layout).
    #>
    $candidates = @(
        (Join-Path $PSScriptRoot 'pbip-templates'),
        (Join-Path (Split-Path -Parent $PSScriptRoot) 'pbip-templates')
    )
    foreach ($c in $candidates) {
        if (Test-Path -LiteralPath (Join-Path $c 'project.pbip') -PathType Leaf) {
            return (Resolve-Path -LiteralPath $c).Path
        }
    }
    throw ("PBIP templates folder not found. Keep the 'pbip-templates' folder " +
           "next to this script (or in its parent folder). Looked in: " +
           ($candidates -join '; '))
}

function Get-Template {
    # Reads a template file from the templates folder, verbatim.
    param(
        [Parameter(Mandatory)][string]$TemplatesDir,
        [Parameter(Mandatory)][string]$RelativePath
    )
    $full = Join-Path $TemplatesDir $RelativePath
    if (-not (Test-Path -LiteralPath $full -PathType Leaf)) {
        throw "PBIP template missing: $full"
    }
    return [System.IO.File]::ReadAllText($full)
}

function New-PageName {
    # A fresh 20-hex-character report page id, matching how Power BI Desktop
    # names report pages.
    return [Guid]::NewGuid().ToString('N').Substring(0, 20)
}

function Get-SafeName {
    # Removes characters that are illegal in Windows file/folder names so the
    # dataset name can be used as a folder name.
    param([Parameter(Mandatory)][string]$Name)
    $invalid = [System.IO.Path]::GetInvalidFileNameChars()
    $clean = ($Name.ToCharArray() | Where-Object { $invalid -notcontains $_ }) -join ''
    $clean = $clean.Trim()
    if (-not $clean) { throw "Dataset name '$Name' contains no valid file-name characters." }
    return $clean
}

# ===========================================================================
#  Validation
# ===========================================================================

function Test-BimFile {
    <#
        Validates the input .bim before conversion. A .bim is a TMSL database
        definition (JSON). We require it to parse as a JSON object; a missing
        top-level 'model' member is only a warning (some exports differ) so an
        unusual but complex model is never blocked.
    #>
    param(
        [Parameter(Mandatory)][string]$Path,
        [scriptblock]$Log = $script:ConsoleLog
    )
    $text = [System.IO.File]::ReadAllText($Path)
    if (-not $text.Trim()) { throw "Input .bim file is empty: $Path" }
    try {
        $obj = $text | ConvertFrom-Json
    }
    catch {
        throw "Input .bim is not valid JSON ($Path): $($_.Exception.Message)"
    }
    if ($obj -isnot [System.Management.Automation.PSCustomObject]) {
        throw "Input .bim must be a JSON object (TMSL model). File: $Path"
    }
    if ($obj.PSObject.Properties.Name -notcontains 'model') {
        & $Log warn "Input .bim has no top-level 'model' member - copying it anyway."
    }
}

function Test-PbipStructure {
    <#
        Validates the finished PBIP project against the rules Power BI Desktop
        enforces, so a broken project is never reported as success. Throws on
        the first problem found. This is the guard that turns the silent
        "Cannot read .pbip" failures into a clear, local error.
    #>
    param(
        [Parameter(Mandatory)][string]$Root,
        [Parameter(Mandatory)][string]$Name,
        [scriptblock]$Log = $script:ConsoleLog
    )

    $reportFolder = "$Name.Report"
    $smFolder     = "$Name.SemanticModel"

    $pbipPath  = Join-Path $Root "$Name.pbip"
    $reportDir = Join-Path $Root $reportFolder
    $smDir     = Join-Path $Root $smFolder
    $pbirPath  = Join-Path $reportDir 'definition.pbir'
    $reportJsn = Join-Path $reportDir 'report.json'
    $pbismPath = Join-Path $smDir 'definition.pbism'
    $modelPath = Join-Path $smDir 'model.bim'

    $required = @($pbipPath, $reportDir, $smDir, $pbirPath, $reportJsn, $pbismPath, $modelPath)
    foreach ($p in $required) {
        if (-not (Test-Path -LiteralPath $p)) {
            throw "PBIP validation failed: missing '$p'."
        }
    }

    function ConvertFrom-JsonFile([string]$p) {
        try { return ([System.IO.File]::ReadAllText($p) | ConvertFrom-Json) }
        catch { throw "PBIP validation failed: '$p' is not valid JSON: $($_.Exception.Message)" }
    }

    # .pbip : must declare exactly a 'report' artifact pointing at the report
    # folder, and must NOT declare a 'dataset' artifact (Power BI rejects that).
    $pbip = ConvertFrom-JsonFile $pbipPath
    if (-not $pbip.version) { throw "PBIP validation failed: '$pbipPath' has no 'version'." }
    if (-not $pbip.artifacts -or @($pbip.artifacts).Count -lt 1) {
        throw "PBIP validation failed: '$pbipPath' has no artifacts."
    }
    $artifact = @($pbip.artifacts)[0]
    if ($artifact.PSObject.Properties.Name -contains 'dataset') {
        throw "PBIP validation failed: '$pbipPath' declares a 'dataset' artifact (Power BI rejects this)."
    }
    if ($artifact.PSObject.Properties.Name -notcontains 'report') {
        throw "PBIP validation failed: '$pbipPath' artifact has no 'report' entry."
    }
    if ($artifact.report.path -ne $reportFolder) {
        throw "PBIP validation failed: '$pbipPath' report.path is '$($artifact.report.path)', expected '$reportFolder'."
    }

    # definition.pbir : must reference the semantic model by relative path.
    $pbir = ConvertFrom-JsonFile $pbirPath
    $expectedRef = "../$smFolder"
    if ($pbir.datasetReference.byPath.path -ne $expectedRef) {
        throw "PBIP validation failed: '$pbirPath' byPath is '$($pbir.datasetReference.byPath.path)', expected '$expectedRef'."
    }

    # report.json : must parse and contain at least one page.
    $report = ConvertFrom-JsonFile $reportJsn
    if (-not $report.sections -or @($report.sections).Count -lt 1) {
        throw "PBIP validation failed: '$reportJsn' has no report pages."
    }

    # definition.pbism and model.bim : must parse as JSON.
    [void](ConvertFrom-JsonFile $pbismPath)
    [void](ConvertFrom-JsonFile $modelPath)

    # No file in the project may carry a UTF-8 BOM.
    Get-ChildItem -LiteralPath $Root -Recurse -File | ForEach-Object {
        if (Test-FileHasBom -Path $_.FullName) {
            throw "PBIP validation failed: '$($_.FullName)' starts with a UTF-8 BOM."
        }
    }

    & $Log success 'PBIP structure validated (all checks passed).'
}

# ===========================================================================
#  Conversion pipeline
# ===========================================================================

function Invoke-BimToPbip {
    <#
        Runs the full pipeline. Returns:
          [pscustomobject]@{ Success=<bool>; ProjectPath=<string>; Message=<string> }
        Failures never crash silently -- they are caught and returned.
    #>
    param(
        [Parameter(Mandatory)][string]$BimPath,
        [string]$OutputRoot,
        [string]$DatasetName,
        [scriptblock]$Log = $script:ConsoleLog
    )

    try {
        # --- Step 1: validate input -------------------------------------
        $bimFull = [IO.Path]::GetFullPath($BimPath)
        if (-not (Test-Path -LiteralPath $bimFull -PathType Leaf)) {
            throw "Input model.bim not found: $bimFull"
        }
        & $Log step 'Validating input model...'
        Test-BimFile -Path $bimFull -Log $Log
        & $Log info "Input model:  $bimFull"

        if ($DatasetName) { $dsName = Get-SafeName $DatasetName }
        else { $dsName = Get-SafeName ([IO.Path]::GetFileNameWithoutExtension($bimFull)) }

        if ($OutputRoot) { $root = [IO.Path]::GetFullPath($OutputRoot) }
        else { $root = Join-Path (Split-Path -Parent $bimFull) $dsName }

        & $Log info "Dataset name: $dsName"
        & $Log info "Output root:  $root"

        $templatesDir = Resolve-TemplatesDir
        & $Log info "Templates:    $templatesDir"

        # --- Step 2: assemble the PBIP project --------------------------
        & $Log step 'Assembling PBIP project (TMSL layout)...'

        $reportFolder = "$dsName.Report"
        $smFolder     = "$dsName.SemanticModel"
        $reportDir    = Join-Path $root $reportFolder
        $smDir        = Join-Path $root $smFolder

        New-Item -ItemType Directory -Path $root      -Force | Out-Null
        New-Item -ItemType Directory -Path $reportDir -Force | Out-Null
        New-Item -ItemType Directory -Path $smDir     -Force | Out-Null

        # The .bim IS the TMSL model -> store it verbatim as model.bim.
        # Re-encoded as UTF-8 without BOM (Power BI rejects a BOM).
        $bimText = [System.IO.File]::ReadAllText($bimFull)
        Write-Utf8NoBom -Path (Join-Path $smDir 'model.bim') -Content $bimText

        # The four metadata files, from the templates, with tokens substituted.
        $pbip = (Get-Template $templatesDir 'project.pbip').Replace('{{REPORT_FOLDER}}', $reportFolder)
        Write-Utf8NoBom -Path (Join-Path $root "$dsName.pbip") -Content $pbip

        $pbism = Get-Template $templatesDir (Join-Path 'SemanticModel' 'definition.pbism')
        Write-Utf8NoBom -Path (Join-Path $smDir 'definition.pbism') -Content $pbism

        $pbir = (Get-Template $templatesDir (Join-Path 'Report' 'definition.pbir')).Replace('{{SEMANTIC_MODEL_FOLDER}}', $smFolder)
        Write-Utf8NoBom -Path (Join-Path $reportDir 'definition.pbir') -Content $pbir

        $report = (Get-Template $templatesDir (Join-Path 'Report' 'report.json')).Replace('{{PAGE_NAME}}', (New-PageName))
        Write-Utf8NoBom -Path (Join-Path $reportDir 'report.json') -Content $report

        # Safety sweep: strip a UTF-8 BOM from every file (belt and braces).
        $strippedCount = (
            Get-ChildItem -LiteralPath $root -Recurse -File -ErrorAction SilentlyContinue |
                Where-Object { Remove-Utf8Bom -Path $_.FullName } |
                Measure-Object
        ).Count
        if ($strippedCount -gt 0) {
            & $Log info "Stripped a UTF-8 BOM from $strippedCount file(s)."
        }
        & $Log success 'PBIP structure assembled.'

        # --- Step 3: validate the result --------------------------------
        & $Log step 'Validating PBIP project...'
        Test-PbipStructure -Root $root -Name $dsName -Log $Log

        # --- Step 4: summary --------------------------------------------
        & $Log detail ''
        & $Log success 'Conversion complete.'
        & $Log detail "  PBIP project root : $root"
        & $Log detail "  Open in Power BI  : $(Join-Path $root "$dsName.pbip")"
        & $Log detail "  Semantic model    : $smDir"
        & $Log detail "  Report            : $reportDir"
        & $Log detail ''
        & $Log detail '  Double-click the .pbip file to open it in Power BI Desktop.'

        return [pscustomobject]@{
            Success     = $true
            ProjectPath = $root
            Message     = "PBIP project created at: $root"
        }
    }
    catch {
        $message = $_.Exception.Message
        & $Log error $message
        return [pscustomobject]@{
            Success     = $false
            ProjectPath = $null
            Message     = $message
        }
    }
}

# ===========================================================================
#  GUI
# ===========================================================================

function Show-Gui {
    Add-Type -AssemblyName System.Windows.Forms
    Add-Type -AssemblyName System.Drawing
    [System.Windows.Forms.Application]::EnableVisualStyles()

    $form = New-Object System.Windows.Forms.Form
    $form.Text          = 'BIM -> PBIP Converter'
    $form.Size          = New-Object System.Drawing.Size(640, 560)
    $form.StartPosition = 'CenterScreen'
    $form.MinimumSize   = New-Object System.Drawing.Size(560, 480)
    $form.Font          = New-Object System.Drawing.Font('Segoe UI', 9)

    function New-Label([string]$Text, [int]$X, [int]$Y) {
        $l = New-Object System.Windows.Forms.Label
        $l.Text = $Text; $l.Location = New-Object System.Drawing.Point($X, $Y); $l.AutoSize = $true
        return $l
    }
    function New-TextBox([int]$X, [int]$Y, [int]$Width) {
        $t = New-Object System.Windows.Forms.TextBox
        $t.Location = New-Object System.Drawing.Point($X, $Y)
        $t.Size = New-Object System.Drawing.Size($Width, 24); $t.Anchor = 'Top,Left,Right'
        return $t
    }
    function New-Button([string]$Text, [int]$X, [int]$Y, [int]$Width) {
        $b = New-Object System.Windows.Forms.Button
        $b.Text = $Text; $b.Location = New-Object System.Drawing.Point($X, $Y)
        $b.Size = New-Object System.Drawing.Size($Width, 26); $b.Anchor = 'Top,Right'
        return $b
    }

    $y = 14
    $form.Controls.Add((New-Label 'Model .bim file *' 14 $y)); $y += 20
    $bimBox = New-TextBox 14 $y 480
    $bimBtn = New-Button 'Browse...' 502 ($y - 1) 100
    $form.Controls.AddRange(@($bimBox, $bimBtn)); $y += 38

    $form.Controls.Add((New-Label 'Output project folder (optional)' 14 $y)); $y += 20
    $outBox = New-TextBox 14 $y 480
    $outBtn = New-Button 'Browse...' 502 ($y - 1) 100
    $form.Controls.AddRange(@($outBox, $outBtn)); $y += 38

    $form.Controls.Add((New-Label 'Dataset / project name (optional)' 14 $y)); $y += 20
    $dsBox = New-TextBox 14 $y 588
    $form.Controls.Add($dsBox); $y += 40

    $convertBtn = New-Object System.Windows.Forms.Button
    $convertBtn.Text = 'Convert'
    $convertBtn.Location = New-Object System.Drawing.Point(14, $y)
    $convertBtn.Size = New-Object System.Drawing.Size(588, 34)
    $convertBtn.Anchor = 'Top,Left,Right'
    $convertBtn.BackColor = [System.Drawing.Color]::FromArgb(79, 70, 229)
    $convertBtn.ForeColor = [System.Drawing.Color]::White
    $convertBtn.FlatStyle = 'Flat'
    $form.Controls.Add($convertBtn); $y += 44

    $statusLbl = New-Object System.Windows.Forms.Label
    $statusLbl.Location = New-Object System.Drawing.Point(14, $y)
    $statusLbl.AutoSize = $true
    $statusLbl.Font = New-Object System.Drawing.Font('Segoe UI', 9, [System.Drawing.FontStyle]::Bold)
    $form.Controls.Add($statusLbl); $y += 24

    $logBox = New-Object System.Windows.Forms.TextBox
    $logBox.Location = New-Object System.Drawing.Point(14, $y)
    $logBox.Size = New-Object System.Drawing.Size(588, 300)
    $logBox.Multiline = $true; $logBox.ReadOnly = $true; $logBox.ScrollBars = 'Vertical'
    $logBox.Anchor = 'Top,Bottom,Left,Right'
    $logBox.BackColor = [System.Drawing.Color]::FromArgb(15, 23, 42)
    $logBox.ForeColor = [System.Drawing.Color]::FromArgb(226, 232, 240)
    $logBox.Font = New-Object System.Drawing.Font('Consolas', 9)
    $form.Controls.Add($logBox)

    $guiLog = {
        param([string]$Level, [string]$Message)
        if ($Level -eq 'detail') { $line = $Message }
        else { $line = '[' + $Level.ToUpper().PadRight(7) + '] ' + $Message }
        $logBox.AppendText($line + "`r`n")
        [System.Windows.Forms.Application]::DoEvents()
    }

    $bimBtn.Add_Click({
        $dlg = New-Object System.Windows.Forms.OpenFileDialog
        $dlg.Title = 'Select the model.bim file'
        $dlg.Filter = 'BIM model (*.bim)|*.bim|All files (*.*)|*.*'
        if ($dlg.ShowDialog() -eq [System.Windows.Forms.DialogResult]::OK) { $bimBox.Text = $dlg.FileName }
    })
    $outBtn.Add_Click({
        $dlg = New-Object System.Windows.Forms.FolderBrowserDialog
        $dlg.Description = 'Select the PBIP project output folder'
        if ($dlg.ShowDialog() -eq [System.Windows.Forms.DialogResult]::OK) { $outBox.Text = $dlg.SelectedPath }
    })

    $convertBtn.Add_Click({
        if (-not $bimBox.Text.Trim()) {
            [System.Windows.Forms.MessageBox]::Show(
                'Please choose the model.bim file first.', $script:AppName, 'OK', 'Warning') | Out-Null
            return
        }
        $convertBtn.Enabled = $false
        $logBox.Clear()
        $statusLbl.Text = 'Converting...'
        $statusLbl.ForeColor = [System.Drawing.Color]::FromArgb(180, 83, 9)
        [System.Windows.Forms.Application]::DoEvents()
        try {
            $result = Invoke-BimToPbip `
                -BimPath     $bimBox.Text.Trim() `
                -OutputRoot  $outBox.Text.Trim() `
                -DatasetName $dsBox.Text.Trim() `
                -Log $guiLog
            if ($result.Success) {
                $statusLbl.Text = 'Done.'
                $statusLbl.ForeColor = [System.Drawing.Color]::FromArgb(21, 128, 61)
                $open = [System.Windows.Forms.MessageBox]::Show(
                    "PBIP project created at:`r`n$($result.ProjectPath)`r`n`r`nOpen the folder now?",
                    $script:AppName, 'YesNo', 'Information')
                if ($open -eq [System.Windows.Forms.DialogResult]::Yes) {
                    Start-Process -FilePath 'explorer.exe' -ArgumentList "`"$($result.ProjectPath)`""
                }
            }
            else {
                $statusLbl.Text = 'Failed. See the log below.'
                $statusLbl.ForeColor = [System.Drawing.Color]::FromArgb(220, 38, 38)
            }
        }
        catch {
            & $guiLog error $_.Exception.Message
            $statusLbl.Text = 'Failed. See the log below.'
            $statusLbl.ForeColor = [System.Drawing.Color]::FromArgb(220, 38, 38)
        }
        finally {
            $convertBtn.Enabled = $true
        }
    })

    [void]$form.ShowDialog()
    $form.Dispose()
}

# ===========================================================================
#  Entry point
# ===========================================================================

if ($BimPath) {
    & $script:ConsoleLog info "$script:AppName -- BIM -> PBIP project converter"
    $result = Invoke-BimToPbip -BimPath $BimPath -OutputRoot $OutputRoot -DatasetName $DatasetName -Log $script:ConsoleLog
    if ($result.Success) { exit 0 } else { exit 1 }
}
elseif ($NoGui) {
    & $script:ConsoleLog error 'No -BimPath was provided and -NoGui is set. Nothing to do.'
    exit 1
}
else {
    Show-Gui
    exit 0
}
