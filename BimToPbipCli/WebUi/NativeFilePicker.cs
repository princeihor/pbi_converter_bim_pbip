namespace BimToPbipCli.WebUi;

/// <summary>
/// Shows a native Windows file/folder picker by shelling out to Windows
/// PowerShell (which always ships with System.Windows.Forms). This keeps the
/// utility itself free of a WinForms/WPF dependency, so it still builds and
/// publishes as a plain console app.
///
/// Windows-only: on other platforms the picker simply returns null and the user
/// types the path manually in the web UI.
/// </summary>
public static class NativeFilePicker
{
    /// <summary>Kind of path the user is selecting.</summary>
    public enum PickKind
    {
        BimFile,
        Folder,
        PbiToolsExecutable,
    }

    /// <summary>
    /// Opens the dialog and returns the selected path, or null if the user
    /// cancelled or no native picker is available.
    /// </summary>
    public static string? Pick(PickKind kind)
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        var script = kind switch
        {
            PickKind.BimFile => BuildFileDialogScript(
                "Select the model.bim file",
                "BIM model (*.bim)|*.bim|All files (*.*)|*.*"),
            PickKind.PbiToolsExecutable => BuildFileDialogScript(
                "Select pbi-tools executable",
                "pbi-tools (pbi-tools*.exe)|pbi-tools*.exe|All files (*.*)|*.*"),
            PickKind.Folder => BuildFolderDialogScript(),
            _ => null,
        };

        if (script is null)
        {
            return null;
        }

        try
        {
            var result = ProcessRunner.Run("powershell.exe",
                ["-NoProfile", "-STA", "-NonInteractive", "-Command", script]);

            var path = result.StandardOutput.Trim();
            return string.IsNullOrEmpty(path) ? null : path;
        }
        catch (InvalidOperationException)
        {
            // PowerShell unavailable — fall back to manual path entry.
            return null;
        }
    }

    // A hidden, top-most owner form ensures the dialog appears above the browser.
    private static string BuildFileDialogScript(string title, string filter) => $$"""
        Add-Type -AssemblyName System.Windows.Forms | Out-Null
        $owner = New-Object System.Windows.Forms.Form
        $owner.TopMost = $true
        $dialog = New-Object System.Windows.Forms.OpenFileDialog
        $dialog.Title = '{{title}}'
        $dialog.Filter = '{{filter}}'
        $dialog.CheckFileExists = $true
        $dialog.Multiselect = $false
        if ($dialog.ShowDialog($owner) -eq [System.Windows.Forms.DialogResult]::OK) {
            [Console]::Out.Write($dialog.FileName)
        }
        $owner.Dispose()
        """;

    private static string BuildFolderDialogScript() => """
        Add-Type -AssemblyName System.Windows.Forms | Out-Null
        $dialog = New-Object System.Windows.Forms.FolderBrowserDialog
        $dialog.Description = 'Select the PBIP project output folder'
        $dialog.ShowNewFolderButton = $true
        if ($dialog.ShowDialog() -eq [System.Windows.Forms.DialogResult]::OK) {
            [Console]::Out.Write($dialog.SelectedPath)
        }
        """;
}
