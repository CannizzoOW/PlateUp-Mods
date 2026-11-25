using Microsoft.Win32;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
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
    private const string ManifestUrl = "https://raw.githubusercontent.com/CannizzoOW/PlateUp-Mods/main/mods.json";
    private const string MarkerName = "CannizzoModPatcher.install.json";
    private const string LegacyMarkerName = "CannizzoModPatcher.install.txt";
    private static readonly HttpClient Http = CreateHttpClient();
    private readonly TextBox pathBox = new() { ReadOnly = true, Dock = DockStyle.Fill };
    private readonly CheckedListBox modList = new() { Dock = DockStyle.Fill, CheckOnClick = true };
    private readonly TextBox logBox = new()
    {
        ReadOnly = true, Multiline = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill,
        BackColor = Color.FromArgb(30, 27, 38), ForeColor = Color.White
    };
    private readonly Button installButton = new() { Text = "Install selected mods", AutoSize = true, Enabled = false };
    private readonly Button removeButton = new() { Text = "Remove selected mods", AutoSize = true, Enabled = false };
    private string? modsPath;
    private ModManifest? manifest;

    public MainForm()
    {
        Text = "Cannizzo ModPatcher";
        ClientSize = new Size(760, 570);
        MinimumSize = new Size(650, 480);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(45, 39, 57);
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 10F);

        var title = new Label { Text = "Cannizzo ModPatcher", Font = new Font("Segoe UI Semibold", 20F), AutoSize = true };
        var subtitle = new Label { Text = "Downloads verified PlateUp mods from GitHub", AutoSize = true, ForeColor = Color.Gainsboro };
        var findButton = new Button { Text = "Find PlateUp", AutoSize = true };
        var browseButton = new Button { Text = "Choose game folder...", AutoSize = true };
        var refreshButton = new Button { Text = "Check GitHub", AutoSize = true };
        StyleButton(installButton, Color.FromArgb(77, 155, 92));
        StyleButton(removeButton, Color.FromArgb(174, 77, 77));
        StyleButton(findButton, Color.FromArgb(88, 103, 166));
        StyleButton(browseButton, Color.FromArgb(88, 103, 166));
        StyleButton(refreshButton, Color.FromArgb(88, 103, 166));

        var pathRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, AutoSize = true };
        pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        pathRow.Controls.Add(pathBox, 0, 0);
        pathRow.Controls.Add(findButton, 1, 0);
        pathRow.Controls.Add(browseButton, 2, 0);

        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
        actions.Controls.Add(installButton);
        actions.Controls.Add(removeButton);
        actions.Controls.Add(refreshButton);

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(20), RowCount = 8, ColumnCount = 1 };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 45));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 55));
        layout.Controls.Add(title, 0, 0);
        layout.Controls.Add(subtitle, 0, 1);
        layout.Controls.Add(new Label { Text = "PlateUp Mods folder", AutoSize = true, Margin = new Padding(0, 16, 0, 4) }, 0, 2);
        layout.Controls.Add(pathRow, 0, 3);
        layout.Controls.Add(new Label { Text = "Available GitHub mods", AutoSize = true, Margin = new Padding(0, 14, 0, 4) }, 0, 4);
        layout.Controls.Add(modList, 0, 5);
        layout.Controls.Add(actions, 0, 6);
        layout.Controls.Add(logBox, 0, 7);
        Controls.Add(layout);

        findButton.Click += (_, _) => DetectPlateUp();
        browseButton.Click += (_, _) => BrowseForGame();
        refreshButton.Click += async (_, _) => await LoadManifestAsync();
        installButton.Click += async (_, _) => await InstallSelectedAsync();
        removeButton.Click += (_, _) => RemoveSelected();
        modList.ItemCheck += (_, _) => BeginInvoke(UpdateButtons);
        Shown += async (_, _) => { DetectPlateUp(); await LoadManifestAsync(); };
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Cannizzo-ModPatcher/1.1.0");
        return client;
    }

    private static void StyleButton(Button button, Color color)
    {
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;
        button.BackColor = color;
        button.ForeColor = Color.White;
        button.Padding = new Padding(10, 5, 10, 5);
        button.Margin = new Padding(0, 10, 10, 10);
    }

    private void DetectPlateUp()
    {
        modsPath = PlateUpLocator.FindModsPath();
        pathBox.Text = modsPath ?? "PlateUp was not found automatically";
        Log(modsPath == null ? "PlateUp was not found through Steam." : $"PlateUp path: {modsPath}");
        UpdateButtons();
    }

    private void BrowseForGame()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Select the folder containing PlateUp.exe (or its Mods folder)",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        string selected = Path.GetFullPath(dialog.SelectedPath);
        if (string.Equals(Path.GetFileName(selected), "Mods", StringComparison.OrdinalIgnoreCase))
            modsPath = selected;
        else if (File.Exists(Path.Combine(selected, "PlateUp.exe")))
            modsPath = Path.Combine(selected, "Mods");
        else if (File.Exists(Path.Combine(selected, "PlateUp", "PlateUp.exe")))
            modsPath = Path.Combine(selected, "PlateUp", "Mods");
        else
        {
            MessageBox.Show(this, "Select the folder containing PlateUp.exe, or the Mods folder.", Text,
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        pathBox.Text = modsPath;
        Log($"Using PlateUp Mods path: {modsPath}");
        UpdateButtons();
    }

    private async Task LoadManifestAsync()
    {
        SetBusy(true);
        try
        {
            Log("Checking GitHub for available mods...");
            string json = await Http.GetStringAsync(ManifestUrl + "?t=" + DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            ModManifest? loaded = JsonSerializer.Deserialize<ModManifest>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (loaded?.SchemaVersion != 1 || loaded.Mods == null)
                throw new InvalidDataException("GitHub returned an unsupported mod manifest.");
            foreach (ModDefinition mod in loaded.Mods) ValidateModDefinition(mod);
            manifest = loaded;
            modList.Items.Clear();
            foreach (ModDefinition mod in loaded.Mods) modList.Items.Add(mod, true);
            Log($"Found {loaded.Mods.Count} mod(s) on GitHub.");
        }
        catch (Exception ex)
        {
            manifest = null;
            modList.Items.Clear();
            Log("ERROR checking GitHub: " + ex.Message);
            MessageBox.Show(this, "Could not download the mod list from GitHub.\n\n" + ex.Message,
                Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally { SetBusy(false); UpdateButtons(); }
    }

    private async Task InstallSelectedAsync()
    {
        if (!CanModify() || manifest == null) return;
        var selected = modList.CheckedItems.Cast<ModDefinition>().ToList();
        if (selected.Count == 0) return;
        SetBusy(true);
        try
        {
            Directory.CreateDirectory(modsPath!);
            foreach (ModDefinition mod in selected)
            {
                Log($"Downloading {mod.Name} {mod.Version}...");
                byte[] bytes = await Http.GetByteArrayAsync(mod.DownloadUrl + "?sha=" + mod.Sha256);
                if (bytes.Length == 0 || bytes.Length > 50 * 1024 * 1024)
                    throw new InvalidDataException($"Unexpected file size for {mod.Name}.");
                string hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
                if (!string.Equals(hash, mod.Sha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException($"SHA-256 verification failed for {mod.Name}.");
                string folder = SafeChild(modsPath!, mod.Folder);
                Directory.CreateDirectory(folder);
                string destination = SafeChild(folder, mod.FileName);
                string temporary = destination + ".download";
                await File.WriteAllBytesAsync(temporary, bytes);
                File.Move(temporary, destination, true);
                File.WriteAllText(SafeChild(folder, MarkerName), JsonSerializer.Serialize(new
                {
                    installedBy = "Cannizzo ModPatcher", mod.Id, mod.Name, mod.Version, mod.Sha256
                }, new JsonSerializerOptions { WriteIndented = true }));
                Log($"Installed {mod.Name} {mod.Version} (SHA-256 verified).");
            }
            MessageBox.Show(this, "Selected mods were downloaded, verified, and installed.", Text,
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (UnauthorizedAccessException) { ShowError("Windows denied access. Close PlateUp and run as administrator."); }
        catch (Exception ex) { ShowError(ex.Message); }
        finally { SetBusy(false); UpdateButtons(); }
    }

    private void RemoveSelected()
    {
        if (!CanModify()) return;
        try
        {
            foreach (ModDefinition mod in modList.CheckedItems.Cast<ModDefinition>())
            {
                string folder = SafeChild(modsPath!, mod.Folder);
                string marker = SafeChild(folder, MarkerName);
                string legacyMarker = SafeChild(folder, LegacyMarkerName);
                if (!File.Exists(marker) && !File.Exists(legacyMarker)) { Log($"Skipped {mod.Name}: no ModPatcher marker found."); continue; }
                string dll = SafeChild(folder, mod.FileName);
                if (File.Exists(dll)) File.Delete(dll);
                if (File.Exists(marker)) File.Delete(marker);
                if (File.Exists(legacyMarker)) File.Delete(legacyMarker);
                if (Directory.Exists(folder) && !Directory.EnumerateFileSystemEntries(folder).Any()) Directory.Delete(folder);
                Log($"Removed local copy of {mod.Name}.");
            }
            MessageBox.Show(this, "Selected ModPatcher-managed copies were removed.", Text,
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex) { ShowError(ex.Message); }
    }

    private static void ValidateModDefinition(ModDefinition mod)
    {
        if (string.IsNullOrWhiteSpace(mod.Id) || string.IsNullOrWhiteSpace(mod.Name) ||
            string.IsNullOrWhiteSpace(mod.Version) || string.IsNullOrWhiteSpace(mod.Folder) ||
            string.IsNullOrWhiteSpace(mod.FileName) || !Regex.IsMatch(mod.Sha256 ?? "", "^[0-9a-fA-F]{64}$"))
            throw new InvalidDataException("The GitHub manifest contains an invalid mod entry.");
        if (Path.GetFileName(mod.Folder) != mod.Folder || Path.GetFileName(mod.FileName) != mod.FileName)
            throw new InvalidDataException($"Unsafe path in manifest entry {mod.Name}.");
        var uri = new Uri(mod.DownloadUrl, UriKind.Absolute);
        if (uri.Scheme != Uri.UriSchemeHttps || !string.Equals(uri.Host, "raw.githubusercontent.com", StringComparison.OrdinalIgnoreCase) ||
            !uri.AbsolutePath.StartsWith("/CannizzoOW/PlateUp-Mods/", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Untrusted download URL for {mod.Name}.");
    }

    private bool CanModify()
    {
        if (modsPath == null) { ShowError("Select a valid PlateUp installation first."); return false; }
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
        if (!result.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Unsafe destination path.");
        return result;
    }

    private void SetBusy(bool busy) { UseWaitCursor = busy; modList.Enabled = !busy; installButton.Enabled = removeButton.Enabled = false; }
    private void UpdateButtons()
    {
        bool ready = modsPath != null && manifest != null && modList.CheckedItems.Count > 0 && !UseWaitCursor;
        installButton.Enabled = removeButton.Enabled = ready;
    }
    private void Log(string message) => logBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
    private void ShowError(string message)
    {
        Log("ERROR: " + message);
        MessageBox.Show(this, message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
}

internal static class PlateUpLocator
{
    public static string? FindModsPath()
    {
        foreach (string steamRoot in SteamRoots())
        foreach (string library in SteamLibraries(steamRoot))
        {
            string manifest = Path.Combine(library, "steamapps", "appmanifest_1599600.acf");
            if (!File.Exists(manifest)) continue;
            string text = File.ReadAllText(manifest);
            Match match = Regex.Match(text, "\\\"installdir\\\"\\s+\\\"(?<dir>[^\\\"]+)\\\"", RegexOptions.IgnoreCase);
            string installName = match.Success ? match.Groups["dir"].Value : "PlateUp";
            string installRoot = Path.Combine(library, "steamapps", "common", installName);
            foreach (string gameFolder in new[] { Path.Combine(installRoot, "PlateUp"), installRoot })
                if (File.Exists(Path.Combine(gameFolder, "PlateUp.exe"))) return Path.Combine(gameFolder, "Mods");
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
            foreach (Match match in Regex.Matches(File.ReadAllText(vdf), "\\\"path\\\"\\s+\\\"(?<path>[^\\\"]+)\\\"", RegexOptions.IgnoreCase))
            {
                string path = match.Groups["path"].Value.Replace("\\\\", "\\");
                if (Directory.Exists(path)) libraries.Add(Path.GetFullPath(path));
            }
        }
        return libraries;
    }
}

internal sealed class ModManifest
{
    public int SchemaVersion { get; set; }
    public List<ModDefinition> Mods { get; set; } = new();
}

internal sealed class ModDefinition
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Version { get; set; } = "";
    public string Folder { get; set; } = "";
    public string FileName { get; set; } = "";
    public string DownloadUrl { get; set; } = "";
    public string Sha256 { get; set; } = "";
    public List<string> WorkshopDependencies { get; set; } = new();
    public override string ToString() => $"{Name}  v{Version}";
}
