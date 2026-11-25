using Microsoft.Win32;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;

namespace CannizzoModPatcher;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}

internal sealed class MainForm : Form
{
    private const string MarkerName = "CannizzoModPatcher.install.txt";
    private readonly TextBox pathBox = new() { ReadOnly = true, Dock = DockStyle.Fill };
    private readonly TextBox logBox = new()
    {
        ReadOnly = true,
        Multiline = true,
        ScrollBars = ScrollBars.Vertical,
        Dock = DockStyle.Fill,
        BackColor = Color.FromArgb(30, 27, 38),
        ForeColor = Color.White,
        BorderStyle = BorderStyle.FixedSingle
    };
    private readonly Button installButton = new() { Text = "Install both mods", AutoSize = true };
    private readonly Button removeButton = new() { Text = "Remove local copies", AutoSize = true };
    private string? modsPath;

    private static readonly Payload[] Payloads =
    {
        new("Hot Pot House Enhanced", "HotPotHouseEnhanced", "HotPotHouseEnhanced.dll",
            "CannizzoModPatcher.Payload.HotPotHouseEnhanced.dll", "2.4.5"),
        new("Hot Pot House Colorblind+ Compatibility", "HotPotHouseColorblindPlus", "HotPotHouseColorblindPlus.dll",
            "CannizzoModPatcher.Payload.HotPotHouseColorblindPlus.dll", "1.0.1")
    };

    public MainForm()
    {
        Text = "Cannizzo ModPatcher";
        ClientSize = new Size(700, 420);
        MinimumSize = new Size(620, 360);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(45, 39, 57);
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 10F);

        var title = new Label
        {
            Text = "Cannizzo ModPatcher",
            Font = new Font("Segoe UI Semibold", 20F),
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 4)
        };
        var subtitle = new Label
        {
            Text = "Local installer for Hot Pot House Enhanced and Colorblind+ Compatibility",
            AutoSize = true,
            ForeColor = Color.Gainsboro
        };
        var rescanButton = new Button { Text = "Find PlateUp", AutoSize = true };
        var browseButton = new Button { Text = "Choose folder...", AutoSize = true };

        StyleButton(installButton, Color.FromArgb(77, 155, 92));
        StyleButton(removeButton, Color.FromArgb(174, 77, 77));
        StyleButton(rescanButton, Color.FromArgb(88, 103, 166));
        StyleButton(browseButton, Color.FromArgb(88, 103, 166));

        var pathRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, AutoSize = true };
        pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        pathRow.Controls.Add(pathBox, 0, 0);
        pathRow.Controls.Add(rescanButton, 1, 0);
        pathRow.Controls.Add(browseButton, 2, 0);

        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
        actions.Controls.Add(installButton);
        actions.Controls.Add(removeButton);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(20),
            RowCount = 6,
            ColumnCount = 1
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.Controls.Add(title, 0, 0);
        layout.Controls.Add(subtitle, 0, 1);
        layout.Controls.Add(new Label { Text = "PlateUp Mods folder", AutoSize = true, Margin = new Padding(0, 18, 0, 4) }, 0, 2);
        layout.Controls.Add(pathRow, 0, 3);
        layout.Controls.Add(actions, 0, 4);
        layout.Controls.Add(logBox, 0, 5);
        Controls.Add(layout);

        rescanButton.Click += (_, _) => DetectPlateUp();
        browseButton.Click += (_, _) => BrowseForMods();
        installButton.Click += (_, _) => InstallMods();
        removeButton.Click += (_, _) => RemoveMods();
        Shown += (_, _) => DetectPlateUp();
    }

    private static void StyleButton(Button button, Color color)
    {
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;
        button.BackColor = color;
        button.ForeColor = Color.White;
        button.Padding = new Padding(10, 5, 10, 5);
        button.Margin = new Padding(0, 12, 10, 12);
    }

    private void DetectPlateUp()
    {
        modsPath = PlateUpLocator.FindModsFolder();
        pathBox.Text = modsPath ?? "PlateUp was not found automatically";
        SetButtons();
        Log(modsPath == null
            ? "PlateUp was not found. Use 'Choose folder...' and select PlateUp\\PlateUp\\Mods."
            : $"Found PlateUp Mods folder: {modsPath}");
    }

    private void BrowseForMods()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Select the PlateUp Mods folder",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        string selected = Path.GetFullPath(dialog.SelectedPath);
        if (!string.Equals(Path.GetFileName(selected), "Mods", StringComparison.OrdinalIgnoreCase))
        {
            MessageBox.Show(this, "Select the folder named Mods inside the PlateUp installation.", Text,
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        modsPath = selected;
        pathBox.Text = selected;
        SetButtons();
        Log($"Using Mods folder: {selected}");
    }

    private void InstallMods()
    {
        if (!CanModify()) return;
        try
        {
            foreach (Payload payload in Payloads)
            {
                string folder = SafeChild(modsPath!, payload.FolderName);
                Directory.CreateDirectory(folder);
                string destination = SafeChild(folder, payload.FileName);
                using Stream input = Assembly.GetExecutingAssembly().GetManifestResourceStream(payload.ResourceName)
                    ?? throw new InvalidOperationException($"Embedded payload is missing: {payload.FileName}");
                using FileStream output = new(destination, FileMode.Create, FileAccess.Write, FileShare.None);
                input.CopyTo(output);
                File.WriteAllText(SafeChild(folder, MarkerName),
                    $"Installed by Cannizzo ModPatcher{Environment.NewLine}{payload.DisplayName} {payload.Version}{Environment.NewLine}");
                Log($"Installed {payload.DisplayName} {payload.Version}");
            }
            MessageBox.Show(this, "Both mods were installed locally. Start PlateUp to test them.", Text,
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (UnauthorizedAccessException)
        {
            ShowWriteError("Windows denied access. Close PlateUp and run Cannizzo ModPatcher as administrator.");
        }
        catch (Exception ex)
        {
            ShowWriteError(ex.Message);
        }
    }

    private void RemoveMods()
    {
        if (!CanModify()) return;
        try
        {
            foreach (Payload payload in Payloads)
            {
                string folder = SafeChild(modsPath!, payload.FolderName);
                string marker = SafeChild(folder, MarkerName);
                if (!File.Exists(marker))
                {
                    Log($"Skipped {payload.DisplayName}: no ModPatcher marker was found.");
                    continue;
                }
                string dll = SafeChild(folder, payload.FileName);
                if (File.Exists(dll)) File.Delete(dll);
                File.Delete(marker);
                if (Directory.Exists(folder) && !Directory.EnumerateFileSystemEntries(folder).Any())
                    Directory.Delete(folder, false);
                Log($"Removed local copy of {payload.DisplayName}");
            }
            MessageBox.Show(this, "ModPatcher-managed local copies were removed.", Text,
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (UnauthorizedAccessException)
        {
            ShowWriteError("Windows denied access. Close PlateUp and run Cannizzo ModPatcher as administrator.");
        }
        catch (Exception ex)
        {
            ShowWriteError(ex.Message);
        }
    }

    private bool CanModify()
    {
        if (modsPath == null || !Directory.Exists(modsPath))
        {
            MessageBox.Show(this, "Select a valid PlateUp Mods folder first.", Text,
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }
        if (Process.GetProcessesByName("PlateUp").Length > 0)
        {
            MessageBox.Show(this, "Close PlateUp before installing or removing mods.", Text,
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }
        return true;
    }

    private static string SafeChild(string parent, string child)
    {
        string root = Path.GetFullPath(parent).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string result = Path.GetFullPath(Path.Combine(root, child));
        if (!result.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Unsafe destination path.");
        return result;
    }

    private void SetButtons() => installButton.Enabled = removeButton.Enabled = modsPath != null;

    private void Log(string message) => logBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");

    private void ShowWriteError(string message)
    {
        Log("ERROR: " + message);
        MessageBox.Show(this, message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    private sealed record Payload(string DisplayName, string FolderName, string FileName, string ResourceName, string Version);
}

internal static class PlateUpLocator
{
    public static string? FindModsFolder()
    {
        foreach (string steamRoot in SteamRoots())
        {
            foreach (string library in SteamLibraries(steamRoot))
            {
                string manifest = Path.Combine(library, "steamapps", "appmanifest_1599600.acf");
                if (!File.Exists(manifest)) continue;

                string[] candidates =
                {
                    Path.Combine(library, "steamapps", "common", "PlateUp", "PlateUp", "Mods"),
                    Path.Combine(library, "steamapps", "common", "PlateUp", "Mods")
                };
                foreach (string candidate in candidates)
                    if (Directory.Exists(candidate)) return Path.GetFullPath(candidate);
            }
        }
        return null;
    }

    private static IEnumerable<string> SteamRoots()
    {
        var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string?[] values =
        {
            Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null) as string,
            Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath", null) as string,
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam")
        };
        foreach (string? value in values)
            if (!string.IsNullOrWhiteSpace(value) && Directory.Exists(value) && found.Add(Path.GetFullPath(value)))
                yield return Path.GetFullPath(value);
    }

    private static IEnumerable<string> SteamLibraries(string steamRoot)
    {
        var libraries = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { Path.GetFullPath(steamRoot) };
        string vdf = Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf");
        if (File.Exists(vdf))
        {
            string text = File.ReadAllText(vdf);
            foreach (Match match in Regex.Matches(text, "\\\"path\\\"\\s+\\\"(?<path>[^\\\"]+)\\\"", RegexOptions.IgnoreCase))
            {
                string path = match.Groups["path"].Value.Replace("\\\\", "\\");
                if (Directory.Exists(path)) libraries.Add(Path.GetFullPath(path));
            }
        }
        return libraries;
    }
}
