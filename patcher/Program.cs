using Microsoft.Win32;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CannizzoModPatcher;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Length == 3 && args[0] == "--apply-update")
        {
            try
            {
                int pid = int.Parse(args[2]);
                try { Process.GetProcessById(pid).WaitForExit(15000); } catch { }
                File.Copy(Application.ExecutablePath, args[1], true);
                Process.Start(new ProcessStartInfo(args[1]) { UseShellExecute = true });
            }
            catch (Exception ex) { MessageBox.Show("Could not apply the launcher update.\n\n" + ex.Message, "Cannizzo ModPatcher"); }
            return;
        }
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}

internal sealed class MainForm : Form
{
    private const string AppVersion = "2.0.0";
    private const string ManifestUrl = "https://raw.githubusercontent.com/CannizzoOW/PlateUp-Mods/main/mods.json";
    private const string MarkerName = "CannizzoModPatcher.install.json";
    private const string LegacyMarkerName = "CannizzoModPatcher.install.txt";
    private static readonly HttpClient Http = CreateHttpClient();
    private static readonly Color WindowColor = Color.FromArgb(13, 16, 22);
    private static readonly Color CardColor = Color.FromArgb(24, 29, 39);
    private static readonly Color InputColor = Color.FromArgb(33, 40, 52);
    private static readonly Color AccentColor = Color.FromArgb(112, 92, 255);
    private static readonly Color MutedColor = Color.FromArgb(164, 173, 190);
    private readonly TextBox pathBox = new() { ReadOnly = true, Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, Margin = new Padding(11, 10, 11, 7) };
    private readonly CheckedListBox modList = new() { Dock = DockStyle.Fill, CheckOnClick = true, BorderStyle = BorderStyle.None, IntegralHeight = false };
    private readonly TextBox logBox = new()
    {
        ReadOnly = true, Multiline = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill,
        BackColor = InputColor, ForeColor = Color.FromArgb(205, 213, 225), BorderStyle = BorderStyle.None, Padding = new Padding(8)
    };
    private readonly Button installButton = new RoundedButton { Text = "Install selected", AutoSize = true, Enabled = false };
    private readonly Button removeButton = new RoundedButton { Text = "Remove selected", AutoSize = true, Enabled = false };
    private readonly Label notificationTitle = new() { AutoSize = true, Font = new Font("Segoe UI Semibold", 11F) };
    private readonly Label notificationText = new() { AutoSize = true, ForeColor = MutedColor };
    private readonly Button launcherButton = new RoundedButton { Text = "Download new launcher", AutoSize = true, Visible = false };
    private readonly Panel notificationPanel = new RoundedPanel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(27, 34, 47), Padding = new Padding(14, 10, 14, 10), Radius = 10 };
    private readonly System.Windows.Forms.Timer updateTimer = new() { Interval = 5 * 60 * 1000 };
    private readonly System.Windows.Forms.Timer gameTimer = new() { Interval = 2000 };
    private readonly WebView2 webView = new() { Dock = DockStyle.Fill, DefaultBackgroundColor = WindowColor };
    private string? modsPath;
    private ModManifest? manifest;
    private bool loadingManifest;
    private bool gameRunning;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int valueSize);

    public MainForm()
    {
        Text = $"Cannizzo ModPatcher {AppVersion}";
        ClientSize = new Size(880, 900);
        MinimumSize = new Size(720, 560);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = true;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = WindowColor;
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 10F);
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);

        var title = new Label { Text = "Cannizzo ModPatcher", Font = new Font("Segoe UI Semibold", 23F), AutoSize = true };
        var version = new Label { Text = $"v{AppVersion}", AutoSize = true, ForeColor = Color.FromArgb(143, 125, 255), Margin = new Padding(12, 15, 0, 0) };
        var titleRow = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = false };
        titleRow.Controls.Add(title); titleRow.Controls.Add(version);
        var subtitle = new Label { Text = "Manage local PlateUp mod fixes, compatibility patches, and tools", AutoSize = true, ForeColor = MutedColor };
        var findButton = new RoundedButton { Text = "Find PlateUp", AutoSize = true };
        var browseButton = new RoundedButton { Text = "Choose folder", AutoSize = true };
        var refreshButton = new RoundedButton { Text = "Check now", AutoSize = true };
        StyleButton(installButton, AccentColor);
        StyleButton(removeButton, Color.FromArgb(105, 55, 67));
        StyleButton(findButton, Color.FromArgb(48, 58, 75));
        StyleButton(browseButton, Color.FromArgb(48, 58, 75));
        StyleButton(refreshButton, Color.FromArgb(48, 58, 75));
        StyleButton(launcherButton, AccentColor);
        ApplyDarkInput(pathBox); ApplyDarkInput(modList);

        var pathRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, AutoSize = true, BackColor = CardColor, Margin = Padding.Empty };
        pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        pathRow.Controls.Add(pathBox, 0, 0);
        pathRow.Controls.Add(findButton, 1, 0);
        pathRow.Controls.Add(browseButton, 2, 0);
        var pathInput = new RoundedPanel { Dock = DockStyle.Fill, BackColor = InputColor, Radius = 8, Margin = new Padding(0, 0, 7, 0) };
        pathRow.Controls.Remove(pathBox);
        pathInput.Controls.Add(pathBox);
        pathRow.Controls.Add(pathInput, 0, 0);
        var pathCard = new RoundedPanel { Dock = DockStyle.Fill, BackColor = CardColor, BorderColor = Color.FromArgb(48, 56, 73), BorderWidth = 1, Radius = 11, Padding = new Padding(7), AutoSize = true };
        pathCard.Controls.Add(pathRow);

        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, BackColor = CardColor };
        actions.Controls.Add(installButton);
        actions.Controls.Add(removeButton);
        actions.Controls.Add(refreshButton);

        var noticeText = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = notificationPanel.BackColor };
        noticeText.Controls.Add(notificationTitle); noticeText.Controls.Add(notificationText);
        var noticeLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, AutoSize = true, BackColor = notificationPanel.BackColor };
        noticeLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); noticeLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        noticeLayout.Controls.Add(noticeText, 0, 0); noticeLayout.Controls.Add(launcherButton, 1, 0);
        notificationPanel.Controls.Add(noticeLayout);

        var modsCard = new Panel { Dock = DockStyle.Fill, BackColor = CardColor, Padding = new Padding(14) };
        modsCard.Controls.Add(modList);
        var logCard = new Panel { Dock = DockStyle.Fill, BackColor = CardColor, Padding = new Padding(12) };
        logCard.Controls.Add(logBox);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(24), RowCount = 10, ColumnCount = 1 };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 55));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 45));
        layout.Controls.Add(titleRow, 0, 0);
        layout.Controls.Add(subtitle, 0, 1);
        layout.Controls.Add(notificationPanel, 0, 2);
        layout.Controls.Add(new Label { Text = "PLATEUP INSTALLATION", AutoSize = true, ForeColor = MutedColor, Margin = new Padding(0, 16, 0, 5) }, 0, 3);
        layout.Controls.Add(pathCard, 0, 4);
        layout.Controls.Add(new Label { Text = "MODS", AutoSize = true, ForeColor = MutedColor, Margin = new Padding(0, 16, 0, 5) }, 0, 5);
        layout.Controls.Add(modsCard, 0, 6);
        layout.Controls.Add(actions, 0, 7);
        layout.Controls.Add(new Label { Text = "ACTIVITY", AutoSize = true, ForeColor = MutedColor, Margin = new Padding(0, 5, 0, 5) }, 0, 8);
        layout.Controls.Add(logCard, 0, 9);
        Controls.Add(layout);
        Controls.Add(webView);
        webView.BringToFront();

        findButton.Click += (_, _) => DetectPlateUp();
        browseButton.Click += (_, _) => BrowseForGame();
        refreshButton.Click += async (_, _) => await LoadManifestAsync();
        launcherButton.Click += (_, _) => OpenLauncherDownload();
        installButton.Click += async (_, _) => await InstallSelectedAsync();
        removeButton.Click += (_, _) => RemoveSelected();
        modList.ItemCheck += (_, _) => BeginInvoke(UpdateButtons);
        updateTimer.Tick += async (_, _) => await LoadManifestAsync(true);
        gameTimer.Tick += (_, _) => RefreshGameStatus();
        Shown += async (_, _) =>
        {
            await InitializeWebUiAsync();
            DetectPlateUp();
            await LoadManifestAsync();
            RenderWebUi();
            updateTimer.Start();
            gameTimer.Start();
        };
        SetNotification("Connecting to GitHub…", "Checking the launcher and available mods.", false, false);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        ApplyDarkWindowChrome();
    }

    private void ApplyDarkWindowChrome()
    {
        if (!OperatingSystem.IsWindows()) return;
        int enabled = 1;
        // Attribute 20 is DWMWA_USE_IMMERSIVE_DARK_MODE on supported Windows 10/11 builds.
        if (DwmSetWindowAttribute(Handle, 20, ref enabled, sizeof(int)) != 0)
            DwmSetWindowAttribute(Handle, 19, ref enabled, sizeof(int));
        // Windows 11 caption and text colors. Older Windows versions safely ignore these.
        int captionColor = 0x0016100D;
        int textColor = 0x00F8F7F5;
        DwmSetWindowAttribute(Handle, 35, ref captionColor, sizeof(int));
        DwmSetWindowAttribute(Handle, 36, ref textColor, sizeof(int));
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd($"Cannizzo-ModPatcher/{AppVersion}");
        client.DefaultRequestHeaders.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue
        {
            NoCache = true,
            NoStore = true
        };
        client.DefaultRequestHeaders.Pragma.ParseAdd("no-cache");
        return client;
    }

    private async Task InitializeWebUiAsync()
    {
        await webView.EnsureCoreWebView2Async();
        webView.CoreWebView2.Settings.AreDevToolsEnabled = false;
        webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
        webView.CoreWebView2.WebMessageReceived += WebMessageReceived;
        webView.CoreWebView2.NavigationStarting += (_, e) =>
        {
            if (e.Uri.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                e.Cancel = true;
                Process.Start(new ProcessStartInfo(e.Uri) { UseShellExecute = true });
            }
        };
        RenderWebUi();
    }

    private async void WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            WebCommand? command = JsonSerializer.Deserialize<WebCommand>(e.WebMessageAsJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (command == null) return;
            SetCheckedMods(command.Ids);
            switch (command.Action)
            {
                case "find": DetectPlateUp(); break;
                case "browse": BrowseForGame(); break;
                case "refresh": await LoadManifestAsync(); break;
                case "install": await InstallSelectedAsync(); break;
                case "remove": RemoveSelected(); break;
                case "launcher": OpenLauncherDownload(); break;
                case "launch-game": LaunchPlateUp(); break;
                case "update-all": await UpdateAllAsync(); break;
                case "repair": await RepairSelectedAsync(); break;
                case "rollback": RollBackSelected(); break;
                case "open-folder": OpenModsFolder(); break;
                case "self-update": await SelfUpdateAsync(); break;
            }
            RenderWebUi();
        }
        catch (Exception ex) { ShowError(ex.Message); RenderWebUi(); }
    }

    private void SetCheckedMods(IEnumerable<string>? ids)
    {
        var selected = new HashSet<string>(ids ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < modList.Items.Count; i++)
            if (modList.Items[i] is ModDefinition mod) modList.SetItemChecked(i, selected.Contains(mod.Id));
    }

    private void RenderWebUi()
    {
        if (webView.CoreWebView2 == null) return;
        string template;
        using (Stream? stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("CannizzoModPatcher.launcher-ui.html"))
        using (var reader = new StreamReader(stream ?? throw new InvalidOperationException("Launcher UI resource is missing.")))
            template = reader.ReadToEnd();
        string mods = manifest == null ? "<div class=\"mod\"><div></div><div><div class=\"name\">Checking GitHub…</div><div class=\"meta\">Loading the mod catalog</div></div></div>" :
            string.Join("", manifest.Mods.Select(BuildModHtml));
        string launcherButtonHtml = launcherButton.Visible ? "<button class=\"btn primary\" onclick=\"send('self-update')\">Update launcher</button>" : "";
        string gameLabel = gameRunning ? "PlateUp is running" : "Launch PlateUp";
        string gameStyle = gameRunning ? "background:#4b5260" : "background:#287a5b";
        string gameDisabled = gameRunning ? "disabled" : "";
        string html = template
            .Replace("html{height:100%;overflow-y:auto;", "html{height:100%;overflow:hidden;")
            .Replace("Install and update verified PlateUp mods from GitHub", "Manage local PlateUp mod fixes, compatibility patches, and tools")
            .Replace("<button class=\"btn\" onclick=\"send('refresh')\">Check now</button>",
                $"<button class=\"btn\" onclick=\"send('refresh')\">Check now</button><button class=\"btn\" onclick=\"send('update-all')\">Update all</button><button class=\"btn\" onclick=\"send('repair')\">Repair selected</button><button class=\"btn\" onclick=\"send('rollback')\">Roll back selected</button><button class=\"btn\" onclick=\"send('open-folder')\">Open Mods folder</button><button class=\"btn\" style=\"{gameStyle}\" {gameDisabled} onclick=\"send('launch-game')\">{gameLabel}</button>")
            .Replace("{{VERSION}}", H(AppVersion)).Replace("{{PATH}}", H(pathBox.Text))
            .Replace("{{NOTICE_TITLE}}", H(notificationTitle.Text)).Replace("{{NOTICE_TEXT}}", H(notificationText.Text))
            .Replace("{{LAUNCHER_BUTTON}}", launcherButtonHtml).Replace("{{MODS}}", mods)
            .Replace("{{LOG}}", H(logBox.Text));
        if (manifest?.Mods.Count <= 4)
            html = html.Replace("<div class=\"mods\">", "<div class=\"mods\" style=\"max-height:none;overflow-y:hidden\">");
        webView.NavigateToString(html);
    }

    private string BuildModHtml(ModDefinition mod)
    {
        string dependencies = mod.WorkshopDependencies.Count == 0 ? "No Workshop dependencies." :
            string.Join("", mod.WorkshopDependencies.Select(id =>
            {
                bool installed = PlateUpLocator.IsWorkshopItemInstalled(id);
                string color = installed ? "#63d7a1" : "#ff8ea0";
                string state = installed ? "installed" : "missing";
                return $"<a href=\"https://steamcommunity.com/sharedfiles/filedetails/?id={H(id)}\" style=\"color:{color};margin-right:12px\">{H(id)} ({state})</a>";
            }));
        string description = string.IsNullOrWhiteSpace(mod.Description) ? "Locally managed PlateUp mod." : H(mod.Description);
        string changelog = string.IsNullOrWhiteSpace(mod.Changelog) ? "No release notes supplied." : H(mod.Changelog);
        string dialogId = "details-" + Regex.Replace(mod.Id, "[^a-zA-Z0-9_-]", "");
        return $"<div class=\"mod\"><input type=\"checkbox\" value=\"{H(mod.Id)}\" checked><div><div class=\"name\">{H(mod.Name)}</div><div class=\"meta\">Version {H(mod.Version)} · <button type=\"button\" style=\"background:none;border:0;color:#a99cff;padding:0;cursor:pointer\" onclick=\"document.getElementById('{dialogId}').showModal()\">Details</button></div></div><div class=\"state {(mod.InstallState == ModInstallState.NotInstalled ? "new" : "")}\">{H(InstallStateText(mod.InstallState))}</div><dialog id=\"{dialogId}\" style=\"max-width:620px;background:#181d27;color:#f7f8fc;border:1px solid #465169;border-radius:12px;padding:22px;box-shadow:0 24px 80px #000b\"><h2 style=\"margin-top:0\">{H(mod.Name)}</h2><p style=\"color:#a4adbe\">{description}</p><h3>Workshop dependencies</h3><div>{dependencies}</div><h3>Latest changes</h3><p style=\"color:#a4adbe\">{changelog}</p><button class=\"btn primary\" onclick=\"document.getElementById('{dialogId}').close()\">Close</button></dialog></div>";
    }

    private static string H(string? text) => WebUtility.HtmlEncode(text ?? "");
    private static string InstallStateText(ModInstallState state) => state switch
    {
        ModInstallState.Current => "INSTALLED • UP TO DATE",
        ModInstallState.UpdateAvailable => "UPDATE AVAILABLE",
        ModInstallState.NotInstalled => "NOT INSTALLED",
        _ => "STATUS UNKNOWN"
    };

    private static void StyleButton(Button button, Color color)
    {
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;
        button.BackColor = color;
        button.ForeColor = Color.White;
        button.Padding = new Padding(10, 5, 10, 5);
        button.Margin = new Padding(0, 0, 7, 0);
        button.Cursor = Cursors.Hand;
    }

    private static void ApplyDarkInput(Control control)
    {
        control.BackColor = InputColor;
        control.ForeColor = Color.White;
        control.Font = new Font("Segoe UI", 10F);
    }

    private void DetectPlateUp()
    {
        modsPath = PlateUpLocator.NormalizePathCasing(PlateUpLocator.FindModsPath());
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
        modsPath = PlateUpLocator.NormalizePathCasing(modsPath);
        pathBox.Text = modsPath;
        Log($"Using PlateUp Mods path: {modsPath}");
        UpdateButtons();
    }

    private async Task LoadManifestAsync(bool silent = false)
    {
        if (loadingManifest) return;
        loadingManifest = true;
        SetBusy(true);
        try
        {
            if (!silent) Log("Checking GitHub for launcher and mod updates…");
            string requestUrl = ManifestUrl + "?request=" + Guid.NewGuid().ToString("N");
            using HttpResponseMessage response = await Http.GetAsync(requestUrl, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            string json = await response.Content.ReadAsStringAsync();
            ModManifest? loaded = JsonSerializer.Deserialize<ModManifest>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (loaded?.SchemaVersion != 1 || loaded.Mods == null)
                throw new InvalidDataException("GitHub returned an unsupported mod manifest.");
            foreach (ModDefinition mod in loaded.Mods) ValidateModDefinition(mod);
            manifest = loaded;
            modList.Items.Clear();
            foreach (ModDefinition mod in loaded.Mods)
            {
                mod.InstallState = GetInstallState(mod);
                modList.Items.Add(mod, mod.InstallState != ModInstallState.Current);
            }
            UpdateNotification();
            if (!silent) Log($"GitHub check complete: {loaded.Mods.Count} mods in the catalog.");
        }
        catch (Exception ex)
        {
            manifest = null;
            modList.Items.Clear();
            Log("ERROR checking GitHub: " + ex.Message);
            SetNotification("Could not reach GitHub", "The launcher will automatically try again in five minutes.", false, true);
            if (!silent) MessageBox.Show(this, "Could not download the mod catalog from GitHub.\n\n" + ex.Message,
                Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally { loadingManifest = false; SetBusy(false); UpdateButtons(); RenderWebUi(); }
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
                string backup = destination + ".backup";
                if (File.Exists(destination)) File.Copy(destination, backup, true);
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
            RefreshInstallStates();
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
            RefreshInstallStates();
        }
        catch (Exception ex) { ShowError(ex.Message); }
    }

    private async Task UpdateAllAsync()
    {
        if (manifest == null) return;
        SetCheckedMods(manifest.Mods.Where(m => m.InstallState == ModInstallState.UpdateAvailable).Select(m => m.Id));
        if (modList.CheckedItems.Count == 0) { Log("All installed mods are already up to date."); return; }
        await InstallSelectedAsync();
    }

    private async Task RepairSelectedAsync()
    {
        if (manifest == null) return;
        var ids = modList.CheckedItems.Cast<ModDefinition>()
            .Where(m => m.InstallState != ModInstallState.Current).Select(m => m.Id).ToList();
        SetCheckedMods(ids);
        if (ids.Count == 0) { Log("Selected mod files passed SHA-256 verification."); return; }
        Log($"Repairing {ids.Count} selected mod file(s)…");
        await InstallSelectedAsync();
    }

    private void RollBackSelected()
    {
        if (!CanModify()) return;
        int restored = 0;
        foreach (ModDefinition mod in modList.CheckedItems.Cast<ModDefinition>())
        {
            string destination = SafeChild(SafeChild(modsPath!, mod.Folder), mod.FileName);
            string backup = destination + ".backup";
            if (!File.Exists(backup)) { Log($"No backup exists for {mod.Name}."); continue; }
            File.Copy(backup, destination, true);
            restored++;
            Log($"Rolled back {mod.Name} to its previous local file.");
        }
        RefreshInstallStates();
        if (restored > 0) MessageBox.Show(this, $"Restored {restored} mod backup(s).", Text,
            MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void OpenModsFolder()
    {
        if (modsPath == null) { ShowError("PlateUp has not been located yet."); return; }
        Directory.CreateDirectory(modsPath);
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{modsPath}\"") { UseShellExecute = true });
        Log("Opened the PlateUp Mods folder.");
    }

    private void RefreshGameStatus()
    {
        bool running = Process.GetProcessesByName("PlateUp").Length > 0;
        if (running == gameRunning) return;
        gameRunning = running;
        Log(running ? "PlateUp is running. Mod changes are locked." : "PlateUp has closed. Mod changes are available.");
        RenderWebUi();
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

    private ModInstallState GetInstallState(ModDefinition mod)
    {
        if (modsPath == null) return ModInstallState.Unknown;
        string folder = SafeChild(modsPath, mod.Folder);
        string dll = SafeChild(folder, mod.FileName);
        if (!File.Exists(dll)) return ModInstallState.NotInstalled;
        try
        {
            string actualHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(dll))).ToLowerInvariant();
            return string.Equals(actualHash, mod.Sha256, StringComparison.OrdinalIgnoreCase)
                ? ModInstallState.Current : ModInstallState.UpdateAvailable;
        }
        catch { return ModInstallState.Unknown; }
    }

    private void RefreshInstallStates()
    {
        if (manifest == null) return;
        for (int i = 0; i < modList.Items.Count; i++)
            if (modList.Items[i] is ModDefinition mod) mod.InstallState = GetInstallState(mod);
        modList.Refresh();
        UpdateNotification();
    }

    private void UpdateNotification()
    {
        if (manifest == null) return;
        bool launcherUpdate = manifest.Launcher != null && IsNewer(manifest.Launcher.Version, AppVersion);
        int updates = manifest.Mods.Count(m => m.InstallState == ModInstallState.UpdateAvailable);
        int available = manifest.Mods.Count(m => m.InstallState == ModInstallState.NotInstalled);
        if (launcherUpdate)
            SetNotification($"Launcher v{manifest.Launcher!.Version} is available",
                string.IsNullOrWhiteSpace(manifest.Launcher.ReleaseNotes) ? BuildModSummary(updates, available) : manifest.Launcher.ReleaseNotes,
                true, false);
        else if (updates > 0)
            SetNotification($"{updates} mod update{(updates == 1 ? " is" : "s are")} available", BuildModSummary(0, available), false, false);
        else if (available > 0)
            SetNotification("All installed mods are up to date", $"{available} more mod{(available == 1 ? " is" : "s are")} available in the catalog.", false, false);
        else
            SetNotification("Everything is up to date", "The launcher and installed mods are on the latest versions.", false, false);
    }

    private static string BuildModSummary(int updates, int available)
    {
        var parts = new List<string>();
        if (updates > 0) parts.Add($"{updates} mod update{(updates == 1 ? "" : "s")}");
        if (available > 0) parts.Add($"{available} new mod{(available == 1 ? "" : "s")}");
        return parts.Count == 0 ? "No mod updates are currently available." : string.Join(" • ", parts) + " available.";
    }

    private void SetNotification(string title, string detail, bool showLauncherButton, bool error)
    {
        notificationTitle.Text = title;
        notificationTitle.ForeColor = error ? Color.FromArgb(255, 126, 144) : Color.White;
        notificationText.Text = detail;
        launcherButton.Visible = showLauncherButton;
    }

    private void OpenLauncherDownload()
    {
        string url = manifest?.Launcher?.ReleaseUrl ?? "https://github.com/CannizzoOW/PlateUp-Mods/releases/latest";
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (Exception ex) { ShowError("Could not open the download page: " + ex.Message); }
    }

    private void LaunchPlateUp()
    {
        try
        {
            Process.Start(new ProcessStartInfo("steam://rungameid/1599600") { UseShellExecute = true });
            Log("Launching PlateUp through Steam…");
        }
        catch (Exception ex) { ShowError("Could not launch PlateUp through Steam: " + ex.Message); }
    }

    private async Task SelfUpdateAsync()
    {
        LauncherDefinition? update = manifest?.Launcher;
        if (update == null || !IsNewer(update.Version, AppVersion)) { Log("The launcher is already up to date."); return; }
        if (string.IsNullOrWhiteSpace(update.DownloadUrl) || !Regex.IsMatch(update.Sha256 ?? "", "^[0-9a-fA-F]{64}$"))
        {
            OpenLauncherDownload();
            return;
        }
        try
        {
            var uri = new Uri(update.DownloadUrl);
            if (uri.Scheme != Uri.UriSchemeHttps || !uri.Host.EndsWith("github.com", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("The launcher update URL is not trusted.");
            Log($"Downloading launcher v{update.Version}…");
            byte[] bytes = await Http.GetByteArrayAsync(uri);
            string hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            if (!string.Equals(hash, update.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Launcher SHA-256 verification failed.");
            string temporary = Path.Combine(Path.GetTempPath(), $"Cannizzo-ModPatcher-{update.Version}.exe");
            await File.WriteAllBytesAsync(temporary, bytes);
            Log("Launcher update verified. Restarting to apply it…");
            Process.Start(new ProcessStartInfo(temporary,
                $"--apply-update \"{Application.ExecutablePath}\" {Environment.ProcessId}") { UseShellExecute = true });
            Close();
        }
        catch (Exception ex) { ShowError("Could not update the launcher: " + ex.Message); }
    }

    private static bool IsNewer(string candidate, string current)
    {
        return Version.TryParse(candidate.TrimStart('v', 'V'), out Version? next) &&
               Version.TryParse(current.TrimStart('v', 'V'), out Version? installed) && next > installed;
    }
}

internal static class RoundedGeometry
{
    public static GraphicsPath Create(Rectangle bounds, int radius)
    {
        int diameter = Math.Max(2, radius * 2);
        var path = new GraphicsPath();
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}

internal sealed class RoundedPanel : Panel
{
    public int Radius { get; set; } = 10;
    public Color BorderColor { get; set; } = Color.Transparent;
    public int BorderWidth { get; set; }

    public RoundedPanel()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }

    protected override void OnResize(EventArgs eventargs)
    {
        base.OnResize(eventargs);
        if (Width <= 1 || Height <= 1) return;
        using GraphicsPath path = RoundedGeometry.Create(new Rectangle(0, 0, Width, Height), Radius);
        Region = new Region(path);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.Clear(Parent?.BackColor ?? BackColor);
        Rectangle bounds = new(0, 0, Width - 1, Height - 1);
        using GraphicsPath path = RoundedGeometry.Create(bounds, Radius);
        using var fill = new SolidBrush(BackColor);
        e.Graphics.FillPath(fill, path);
        if (BorderWidth > 0)
        {
            using var pen = new Pen(BorderColor, BorderWidth);
            e.Graphics.DrawPath(pen, path);
        }
    }
}

internal sealed class RoundedButton : Button
{
    public int Radius { get; set; } = 8;

    public RoundedButton()
    {
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        Color fillColor = Enabled ? BackColor : Color.FromArgb(45, 51, 63);
        if (ClientRectangle.Contains(PointToClient(Cursor.Position)) && Enabled)
            fillColor = ControlPaint.Light(BackColor, .08f);
        Rectangle bounds = new(0, 0, Width - 1, Height - 1);
        using GraphicsPath path = RoundedGeometry.Create(bounds, Radius);
        using var fill = new SolidBrush(fillColor);
        e.Graphics.FillPath(fill, path);
        TextRenderer.DrawText(e.Graphics, Text, Font, bounds, Enabled ? ForeColor : Color.FromArgb(115, 123, 138),
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
    }

    protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); Invalidate(); }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); Invalidate(); }
}

internal static class PlateUpLocator
{
    public static bool IsWorkshopItemInstalled(string workshopId)
    {
        foreach (string steamRoot in SteamRoots())
        foreach (string library in SteamLibraries(steamRoot))
            if (Directory.Exists(Path.Combine(library, "steamapps", "workshop", "content", "1599600", workshopId)))
                return true;
        return false;
    }

    public static string? NormalizePathCasing(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return path;
        string fullPath = Path.GetFullPath(path);
        string root = Path.GetPathRoot(fullPath) ?? "";
        if (root.Length >= 2 && root[1] == ':')
            root = char.ToUpperInvariant(root[0]) + root[1..];
        string current = root;
        string remainder = fullPath[root.Length..];
        foreach (string part in remainder.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            string actualPart = part;
            try
            {
                if (Directory.Exists(current))
                {
                    string? match = Directory.EnumerateFileSystemEntries(current)
                        .Select(Path.GetFileName)
                        .FirstOrDefault(name => string.Equals(name, part, StringComparison.OrdinalIgnoreCase));
                    if (!string.IsNullOrEmpty(match)) actualPart = match;
                }
            }
            catch { /* Keep the supplied segment when a parent cannot be enumerated. */ }
            current = Path.Combine(current, actualPart);
        }
        return current;
    }

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
    public LauncherDefinition? Launcher { get; set; }
    public List<ModDefinition> Mods { get; set; } = new();
}

internal sealed class LauncherDefinition
{
    public string Version { get; set; } = "";
    public string ReleaseUrl { get; set; } = "";
    public string DownloadUrl { get; set; } = "";
    public string Sha256 { get; set; } = "";
    public string ReleaseNotes { get; set; } = "";
    public string MinimumSupportedVersion { get; set; } = "";
}

internal sealed class WebCommand
{
    public string Action { get; set; } = "";
    public List<string> Ids { get; set; } = new();
}

internal enum ModInstallState { Unknown, NotInstalled, Current, UpdateAvailable }

internal sealed class ModDefinition
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Version { get; set; } = "";
    public string Folder { get; set; } = "";
    public string FileName { get; set; } = "";
    public string DownloadUrl { get; set; } = "";
    public string Sha256 { get; set; } = "";
    public string Description { get; set; } = "";
    public string Changelog { get; set; } = "";
    public List<string> WorkshopDependencies { get; set; } = new();
    public ModInstallState InstallState { get; set; }
    public override string ToString()
    {
        string state = InstallState switch
        {
            ModInstallState.Current => "INSTALLED • UP TO DATE",
            ModInstallState.UpdateAvailable => "UPDATE AVAILABLE",
            ModInstallState.NotInstalled => "NOT INSTALLED",
            _ => "STATUS UNKNOWN"
        };
        return $"  {Name}   v{Version}     {state}";
    }
}
