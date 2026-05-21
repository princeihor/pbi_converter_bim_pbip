#Requires -Version 5.1
<#
.SYNOPSIS
    Converts a Tabular model.bim into a PBIP-compatible project folder.

.DESCRIPTION
    A no-install, no-admin alternative to the compiled .exe. PowerShell ships
    with every supported Windows version, so this script "just runs":

      * Double-click BimToPbip.cmd  -> opens a small GUI window.
      * Or run it from a console with -BimPath ... for scripting.

    It drives the pbi-tools CLI to convert model.bim to a TMDL model folder,
    then assembles a PBIP-style project around it. If pbi-tools is not found
    it can download the latest release automatically into your user profile
    (%LOCALAPPDATA%\BimToPbip\pbi-tools) -- no administrator rights needed.

.PARAMETER BimPath
    Path to the input model.bim file. When supplied, the script runs headless
    (no GUI) and is suitable for scripting / CI.

.PARAMETER OutputRoot
    PBIP project root folder. Defaults to a sub-folder named after the dataset,
    next to the .bim file.

.PARAMETER DatasetName
    Dataset name. Defaults to the .bim file name without extension.

.PARAMETER PbiToolsPath
    Path to pbi-tools(.exe) or its folder. If omitted the script uses the
    PBI_TOOLS_PATH environment variable, then PATH, then offers to download it.

.PARAMETER KeepTemp
    Keep the temporary working directory instead of deleting it.

.PARAMETER NoGui
    Never show the GUI. Without -BimPath this just reports an error.

.EXAMPLE
    .\bim-to-pbip.ps1
    Opens the GUI window.

.EXAMPLE
    .\bim-to-pbip.ps1 -BimPath "C:\Models\MyModel.bim"

.EXAMPLE
    .\bim-to-pbip.ps1 -BimPath "C:\Models\MyModel.bim" -OutputRoot "C:\PBIP\MyModel" -DatasetName "MyModelDataset"

.NOTES
    The PBIP project format is owned by Microsoft and still evolving. The JSON
    templates emitted here live in the Get-*Json functions below -- update them
    if Microsoft changes the schema. Authoritative spec:
    https://learn.microsoft.com/power-bi/developer/projects/
#>
[CmdletBinding()]
param(
    [string]$BimPath,
    [string]$OutputRoot,
    [string]$DatasetName,
    [string]$PbiToolsPath,
    [switch]$KeepTemp,
    [switch]$NoGui
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$script:AppName              = 'BimToPbip'
$script:DatasetFolderName    = 'dataset'
$script:DefinitionFolderName = 'definition'

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
#  pbi-tools resolution
# ===========================================================================

function Resolve-PbiTools {
    <#
        Resolution order: explicit hint -> PBI_TOOLS_PATH env var -> PATH.
        Returns the full path to pbi-tools(.exe), or $null if not found.
        The path is never hard-coded.
    #>
    param([string]$Hint)

    $names = @('pbi-tools.exe', 'pbi-tools.core.exe', 'pbi-tools')

    if ($Hint) {
        if (Test-Path -LiteralPath $Hint -PathType Leaf) {
            return (Resolve-Path -LiteralPath $Hint).Path
        }
        if (Test-Path -LiteralPath $Hint -PathType Container) {
            foreach ($n in $names) {
                $candidate = Join-Path $Hint $n
                if (Test-Path -LiteralPath $candidate -PathType Leaf) {
                    return (Resolve-Path -LiteralPath $candidate).Path
                }
            }
        }
        return $null
    }

    $envValue = [Environment]::GetEnvironmentVariable('PBI_TOOLS_PATH')
    if ($envValue) {
        $resolved = Resolve-PbiTools -Hint $envValue
        if ($resolved) { return $resolved }
    }

    $cmd = Get-Command -Name $names -CommandType Application -ErrorAction SilentlyContinue |
        Select-Object -First 1
    if ($cmd) { return $cmd.Source }

    return $null
}

function Install-PbiToolsAuto {
    <#
        Downloads the latest pbi-tools release from GitHub into
        %LOCALAPPDATA%\BimToPbip\pbi-tools (per-user, no admin rights needed)
        and returns the path to pbi-tools.exe.
    #>
    param([scriptblock]$Log = $script:ConsoleLog)

    [Net.ServicePointManager]::SecurityProtocol =
        [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12

    $installRoot = Join-Path $env:LOCALAPPDATA "$script:AppName\pbi-tools"

    $existing = Get-ChildItem -Path $installRoot -Recurse -Filter 'pbi-tools.exe' -ErrorAction SilentlyContinue |
        Select-Object -First 1
    if ($existing) {
        & $Log info "Using previously downloaded pbi-tools: $($existing.FullName)"
        return $existing.FullName
    }

    & $Log step 'Downloading pbi-tools (latest release from GitHub)...'
    $headers = @{ 'User-Agent' = $script:AppName; 'Accept' = 'application/vnd.github+json' }

    $release = Invoke-RestMethod -Uri 'https://api.github.com/repos/pbi-tools/pbi-tools/releases/latest' -Headers $headers

    # Prefer the Windows .NET Framework build (no runtime install needed).
    $asset = $release.assets |
        Where-Object { $_.name -like 'pbi-tools.*.zip' -and $_.name -notlike '*core*' } |
        Select-Object -First 1
    if (-not $asset) {
        $asset = $release.assets | Where-Object { $_.name -like '*.zip' } | Select-Object -First 1
    }
    if (-not $asset) {
        throw 'Could not find a downloadable pbi-tools .zip asset in the latest release.'
    }

    $zipPath = Join-Path $env:TEMP $asset.name
    & $Log info "Downloading $($asset.name)..."
    Invoke-WebRequest -Uri $asset.browser_download_url -Headers $headers -OutFile $zipPath

    Unblock-File -LiteralPath $zipPath
    New-Item -ItemType Directory -Path $installRoot -Force | Out-Null
    Expand-Archive -LiteralPath $zipPath -DestinationPath $installRoot -Force
    Remove-Item -LiteralPath $zipPath -Force -ErrorAction SilentlyContinue

    # Clear the "downloaded from the internet" mark so the files run cleanly.
    Get-ChildItem -Path $installRoot -Recurse -File -ErrorAction SilentlyContinue |
        ForEach-Object { Unblock-File -LiteralPath $_.FullName -ErrorAction SilentlyContinue }

    $exe = Get-ChildItem -Path $installRoot -Recurse -Filter 'pbi-tools.exe' -ErrorAction SilentlyContinue |
        Select-Object -First 1
    if (-not $exe) {
        throw "pbi-tools.exe was not found after extracting the release into '$installRoot'."
    }

    & $Log success "pbi-tools ready: $($exe.FullName)"
    return $exe.FullName
}

# ===========================================================================
#  PBIP metadata templates
#
#  Single source of truth for the JSON files emitted by this tool. If Microsoft
#  changes the PBIP schema, update only these three functions.
# ===========================================================================

function Get-PbipProjectJson {
    @{
        version   = '1.0'
        artifacts = @( @{ dataset = @{ path = $script:DatasetFolderName } } )
        settings  = @{ enableAutoRecovery = $true }
    } | ConvertTo-Json -Depth 8
}

function Get-DatasetPlatformJson {
    param([string]$Name, [Guid]$LogicalId)
    @{
        '$schema' = 'https://developer.microsoft.com/json-schemas/fabric/gitIntegration/platformProperties/2.0.0/schema.json'
        metadata  = @{ type = 'SemanticModel'; displayName = $Name }
        config    = @{ version = '2.0'; logicalId = $LogicalId.ToString() }
    } | ConvertTo-Json -Depth 8
}

function Get-DatasetDefinitionPropsJson {
    @{
        version  = '4.0'
        settings = @{}
    } | ConvertTo-Json -Depth 8
}

# ===========================================================================
#  Conversion pipeline
# ===========================================================================

function Invoke-BimToPbip {
    <#
        Runs the full pipeline. Returns a result object:
          [pscustomobject]@{ Success = <bool>; ProjectPath = <string>; Message = <string> }
        All progress is reported through the $Log script block. Failures never
        crash silently -- they are caught and returned with Success = $false.
    #>
    param(
        [Parameter(Mandatory)][string]$BimPath,
        [string]$OutputRoot,
        [string]$DatasetName,
        [Parameter(Mandatory)][string]$PbiToolsExe,
        [switch]$KeepTemp,
        [scriptblock]$Log = $script:ConsoleLog
    )

    $tempDir = $null
    try {
        # --- Step 1: validate input --------------------------------------
        $bimFull = [IO.Path]::GetFullPath($BimPath)
        if (-not (Test-Path -LiteralPath $bimFull -PathType Leaf)) {
            throw "Input model.bim not found: $bimFull"
        }
        & $Log info "Input model:  $bimFull"

        if ($DatasetName) {
            $dsName = $DatasetName.Trim()
        }
        else {
            $dsName = [IO.Path]::GetFileNameWithoutExtension($bimFull)
        }

        if ($OutputRoot) {
            $root = [IO.Path]::GetFullPath($OutputRoot)
        }
        else {
            $root = Join-Path (Split-Path -Parent $bimFull) $dsName
        }
        & $Log info "Dataset name: $dsName"
        & $Log info "Output root:  $root"
        & $Log info "pbi-tools:    $PbiToolsExe"

        # --- Step 1b: temporary workspace --------------------------------
        $tempDir = Join-Path ([IO.Path]::GetTempPath()) "$script:AppName\$([Guid]::NewGuid().ToString('N'))"
        New-Item -ItemType Directory -Path $tempDir -Force | Out-Null
        $modelFolder = Join-Path $tempDir 'model'

        # --- Step 2: pbi-tools convert -----------------------------------
        & $Log step 'Running pbi-tools convert (Tmdl)...'
        $output = & $PbiToolsExe convert $bimFull $modelFolder 'Tmdl' 2>&1
        $exitCode = $LASTEXITCODE
        foreach ($line in $output) {
            if ($null -ne $line -and "$line".Trim().Length -gt 0) {
                & $Log info "  pbi-tools> $line"
            }
        }
        if ($exitCode -ne 0) {
            throw "pbi-tools convert failed with exit code $exitCode."
        }
        if (-not (Test-Path -LiteralPath $modelFolder) -or
            -not (Get-ChildItem -LiteralPath $modelFolder -ErrorAction SilentlyContinue)) {
            throw "pbi-tools convert reported success but produced no output in '$modelFolder'."
        }
        & $Log success 'Model converted to TMDL.'

        # --- Step 3: assemble the PBIP project ---------------------------
        & $Log step 'Assembling PBIP project structure...'
        $datasetDir    = Join-Path $root        $script:DatasetFolderName
        $definitionDir = Join-Path $datasetDir  $script:DefinitionFolderName

        New-Item -ItemType Directory -Path $datasetDir -Force | Out-Null
        if (Test-Path -LiteralPath $definitionDir) {
            Remove-Item -LiteralPath $definitionDir -Recurse -Force
        }
        Copy-Item -LiteralPath $modelFolder -Destination $definitionDir -Recurse

        $logicalId = [Guid]::NewGuid()
        Set-Content -LiteralPath (Join-Path $root "$dsName.pbip") `
            -Value (Get-PbipProjectJson) -Encoding UTF8
        Set-Content -LiteralPath (Join-Path $datasetDir '.platform') `
            -Value (Get-DatasetPlatformJson -Name $dsName -LogicalId $logicalId) -Encoding UTF8
        Set-Content -LiteralPath (Join-Path $datasetDir 'definition.pbism') `
            -Value (Get-DatasetDefinitionPropsJson) -Encoding UTF8
        & $Log info "Dataset GUID: $logicalId"
        & $Log success 'PBIP structure assembled.'

        # --- Step 4: cleanup ---------------------------------------------
        if ($KeepTemp) {
            & $Log info "--KeepTemp set; temporary workspace kept at: $tempDir"
        }
        else {
            & $Log step 'Cleaning up temporary workspace...'
            Remove-Item -LiteralPath $tempDir -Recurse -Force -ErrorAction SilentlyContinue
        }
        $tempDir = $null

        # --- Step 5: summary ---------------------------------------------
        & $Log detail ''
        & $Log success 'Conversion complete.'
        & $Log detail "  PBIP project root : $root"
        & $Log detail "  Project file      : $(Join-Path $root "$dsName.pbip")"
        & $Log detail "  Dataset folder    : $datasetDir"
        & $Log detail "  Model definition  : $definitionDir (TMDL)"
        & $Log detail ''
        & $Log detail '  Created a PBIP dataset (semantic model) project. No report part was'
        & $Log detail '  generated -- open the .pbip in Power BI Desktop to add a report.'

        return [pscustomobject]@{
            Success     = $true
            ProjectPath = $root
            Message     = "PBIP project (dataset only) created at: $root"
        }
    }
    catch {
        $message = $_.Exception.Message
        & $Log error $message
        if ($tempDir -and -not $KeepTemp -and (Test-Path -LiteralPath $tempDir)) {
            Remove-Item -LiteralPath $tempDir -Recurse -Force -ErrorAction SilentlyContinue
        }
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
    $form.Size          = New-Object System.Drawing.Size(640, 660)
    $form.StartPosition = 'CenterScreen'
    $form.MinimumSize   = New-Object System.Drawing.Size(560, 560)
    $form.Font          = New-Object System.Drawing.Font('Segoe UI', 9)

    # --- helper: a label at (x,y) -------------------------------------------
    function New-Label([string]$Text, [int]$X, [int]$Y) {
        $l = New-Object System.Windows.Forms.Label
        $l.Text     = $Text
        $l.Location = New-Object System.Drawing.Point($X, $Y)
        $l.AutoSize = $true
        return $l
    }
    function New-TextBox([int]$X, [int]$Y, [int]$Width) {
        $t = New-Object System.Windows.Forms.TextBox
        $t.Location = New-Object System.Drawing.Point($X, $Y)
        $t.Size     = New-Object System.Drawing.Size($Width, 24)
        $t.Anchor   = 'Top,Left,Right'
        return $t
    }
    function New-Button([string]$Text, [int]$X, [int]$Y, [int]$Width) {
        $b = New-Object System.Windows.Forms.Button
        $b.Text     = $Text
        $b.Location = New-Object System.Drawing.Point($X, $Y)
        $b.Size     = New-Object System.Drawing.Size($Width, 26)
        $b.Anchor   = 'Top,Right'
        return $b
    }

    $y = 14

    $form.Controls.Add((New-Label 'Model .bim file *' 14 $y))
    $y += 20
    $bimBox = New-TextBox 14 $y 480
    $bimBtn = New-Button 'Browse...' 502 ($y - 1) 100
    $form.Controls.AddRange(@($bimBox, $bimBtn))
    $y += 38

    $form.Controls.Add((New-Label 'Output project folder (optional)' 14 $y))
    $y += 20
    $outBox = New-TextBox 14 $y 480
    $outBtn = New-Button 'Browse...' 502 ($y - 1) 100
    $form.Controls.AddRange(@($outBox, $outBtn))
    $y += 38

    $form.Controls.Add((New-Label 'Dataset name (optional)' 14 $y))
    $y += 20
    $dsBox = New-TextBox 14 $y 588
    $form.Controls.Add($dsBox)
    $y += 38

    $form.Controls.Add((New-Label 'pbi-tools path (optional - leave blank to auto-detect/download)' 14 $y))
    $y += 20
    $ptBox = New-TextBox 14 $y 372
    $ptBtn = New-Button 'Browse...' 394 ($y - 1) 100
    $ptAuto = New-Button 'Download' 502 ($y - 1) 100
    $form.Controls.AddRange(@($ptBox, $ptBtn, $ptAuto))
    $y += 38

    $keepChk = New-Object System.Windows.Forms.CheckBox
    $keepChk.Text     = 'Keep temporary working files'
    $keepChk.Location = New-Object System.Drawing.Point(14, $y)
    $keepChk.AutoSize = $true
    $form.Controls.Add($keepChk)
    $y += 34

    $convertBtn = New-Object System.Windows.Forms.Button
    $convertBtn.Text      = 'Convert'
    $convertBtn.Location  = New-Object System.Drawing.Point(14, $y)
    $convertBtn.Size      = New-Object System.Drawing.Size(588, 34)
    $convertBtn.Anchor    = 'Top,Left,Right'
    $convertBtn.BackColor = [System.Drawing.Color]::FromArgb(79, 70, 229)
    $convertBtn.ForeColor = [System.Drawing.Color]::White
    $convertBtn.FlatStyle = 'Flat'
    $form.Controls.Add($convertBtn)
    $y += 44

    $statusLbl = New-Object System.Windows.Forms.Label
    $statusLbl.Location  = New-Object System.Drawing.Point(14, $y)
    $statusLbl.AutoSize  = $true
    $statusLbl.Font      = New-Object System.Drawing.Font('Segoe UI', 9, [System.Drawing.FontStyle]::Bold)
    $form.Controls.Add($statusLbl)
    $y += 24

    $logBox = New-Object System.Windows.Forms.TextBox
    $logBox.Location   = New-Object System.Drawing.Point(14, $y)
    $logBox.Size       = New-Object System.Drawing.Size(588, 300)
    $logBox.Multiline  = $true
    $logBox.ReadOnly   = $true
    $logBox.ScrollBars = 'Vertical'
    $logBox.Anchor     = 'Top,Bottom,Left,Right'
    $logBox.BackColor  = [System.Drawing.Color]::FromArgb(15, 23, 42)
    $logBox.ForeColor  = [System.Drawing.Color]::FromArgb(226, 232, 240)
    $logBox.Font       = New-Object System.Drawing.Font('Consolas', 9)
    $form.Controls.Add($logBox)

    # --- GUI logger ---------------------------------------------------------
    $guiLog = {
        param([string]$Level, [string]$Message)
        if ($Level -eq 'detail') {
            $line = $Message
        }
        else {
            $line = '[' + $Level.ToUpper().PadRight(7) + '] ' + $Message
        }
        $logBox.AppendText($line + "`r`n")
        [System.Windows.Forms.Application]::DoEvents()
    }

    # --- event handlers -----------------------------------------------------
    $bimBtn.Add_Click({
        $dlg = New-Object System.Windows.Forms.OpenFileDialog
        $dlg.Title  = 'Select the model.bim file'
        $dlg.Filter = 'BIM model (*.bim)|*.bim|All files (*.*)|*.*'
        if ($dlg.ShowDialog() -eq [System.Windows.Forms.DialogResult]::OK) {
            $bimBox.Text = $dlg.FileName
        }
    })

    $outBtn.Add_Click({
        $dlg = New-Object System.Windows.Forms.FolderBrowserDialog
        $dlg.Description = 'Select the PBIP project output folder'
        if ($dlg.ShowDialog() -eq [System.Windows.Forms.DialogResult]::OK) {
            $outBox.Text = $dlg.SelectedPath
        }
    })

    $ptBtn.Add_Click({
        $dlg = New-Object System.Windows.Forms.OpenFileDialog
        $dlg.Title  = 'Select pbi-tools executable'
        $dlg.Filter = 'pbi-tools (pbi-tools*.exe)|pbi-tools*.exe|All files (*.*)|*.*'
        if ($dlg.ShowDialog() -eq [System.Windows.Forms.DialogResult]::OK) {
            $ptBox.Text = $dlg.FileName
        }
    })

    $ptAuto.Add_Click({
        $ptAuto.Enabled = $false
        $statusLbl.Text = 'Downloading pbi-tools...'
        $statusLbl.ForeColor = [System.Drawing.Color]::FromArgb(180, 83, 9)
        try {
            $exe = Install-PbiToolsAuto -Log $guiLog
            $ptBox.Text = $exe
            $statusLbl.Text = 'pbi-tools downloaded.'
            $statusLbl.ForeColor = [System.Drawing.Color]::FromArgb(21, 128, 61)
        }
        catch {
            & $guiLog error $_.Exception.Message
            $statusLbl.Text = 'pbi-tools download failed.'
            $statusLbl.ForeColor = [System.Drawing.Color]::FromArgb(220, 38, 38)
        }
        finally {
            $ptAuto.Enabled = $true
        }
    })

    $convertBtn.Add_Click({
        if (-not $bimBox.Text.Trim()) {
            [System.Windows.Forms.MessageBox]::Show(
                'Please choose the model.bim file first.', $script:AppName,
                'OK', 'Warning') | Out-Null
            return
        }

        $convertBtn.Enabled = $false
        $logBox.Clear()
        $statusLbl.Text = 'Converting...'
        $statusLbl.ForeColor = [System.Drawing.Color]::FromArgb(180, 83, 9)
        [System.Windows.Forms.Application]::DoEvents()

        try {
            $exe = Resolve-PbiTools -Hint $ptBox.Text.Trim()
            if (-not $exe) {
                & $guiLog warn 'pbi-tools not found locally.'
                $answer = [System.Windows.Forms.MessageBox]::Show(
                    "pbi-tools was not found.`r`n`r`nDownload the latest release now? " +
                    "It will be saved under your user profile (no admin rights needed).",
                    $script:AppName, 'YesNo', 'Question')
                if ($answer -eq [System.Windows.Forms.DialogResult]::Yes) {
                    $exe = Install-PbiToolsAuto -Log $guiLog
                    $ptBox.Text = $exe
                }
                else {
                    throw 'pbi-tools is required. Provide its path or allow the download.'
                }
            }

            $result = Invoke-BimToPbip `
                -BimPath     $bimBox.Text.Trim() `
                -OutputRoot  $outBox.Text.Trim() `
                -DatasetName $dsBox.Text.Trim() `
                -PbiToolsExe $exe `
                -KeepTemp:$keepChk.Checked `
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
    # Headless / command-line mode.
    & $script:ConsoleLog info "$script:AppName -- BIM -> PBIP project converter"
    $exe = Resolve-PbiTools -Hint $PbiToolsPath
    if (-not $exe) {
        & $script:ConsoleLog warn 'pbi-tools not found locally -- downloading the latest release...'
        $exe = Install-PbiToolsAuto -Log $script:ConsoleLog
    }
    $result = Invoke-BimToPbip `
        -BimPath     $BimPath `
        -OutputRoot  $OutputRoot `
        -DatasetName $DatasetName `
        -PbiToolsExe $exe `
        -KeepTemp:$KeepTemp `
        -Log $script:ConsoleLog
    if ($result.Success) { exit 0 } else { exit 1 }
}
elseif ($NoGui) {
    & $script:ConsoleLog error 'No -BimPath was provided and -NoGui is set. Nothing to do.'
    exit 1
}
else {
    # Default: show the GUI window (this is what double-clicking does).
    Show-Gui
    exit 0
}
