using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Win32;

public class InstallRecord {
    public string BackupDir;
    public string Status;
    public List<FileRecord> Files = new List<FileRecord>();
}
public class FileRecord { public string Path; public bool Existed; }

public static class Installer {
    public const string Marker = ".repo-easy-mods/install.json";
    public const string VietnameseFile = "BepInEx/plugins/REPOEasyMods/Teck-VietnameseUI-1.0.0/Teck.Vietnamese.dll";
    public const string GameToolsFile = "BepInEx/plugins/REPOEasyMods/Zichen-GameTools-1.0.9/Zichen-GameTools-1.0.9.dll";
    public const string CosmeticsToolsFile = "BepInEx/plugins/REPOEasyMods/Zichen-CosmeticsTools-1.0.1/Zichen-CosmeticsTools-1.0.1.dll";
    static JavaScriptSerializer json = new JavaScriptSerializer();
    public static string SafePath(string root, string relative) {
        if (String.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative) || relative.Contains(":")) throw new Exception("Đường dẫn không hợp lệ.");
        string fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string path = Path.GetFullPath(Path.Combine(fullRoot, relative.Replace('/', Path.DirectorySeparatorChar)));
        if (!path.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase)) throw new Exception("Đường dẫn vượt ngoài thư mục game.");
        string cursor = path;
        while (cursor != null && cursor.Length >= fullRoot.TrimEnd(Path.DirectorySeparatorChar).Length) {
            if ((Directory.Exists(cursor) || File.Exists(cursor)) && (File.GetAttributes(cursor) & FileAttributes.ReparsePoint) != 0)
                throw new Exception("Không hỗ trợ thư mục liên kết: " + cursor);
            cursor = Path.GetDirectoryName(cursor);
        }
        return path;
    }
    public static bool HasInstall(string root) { return File.Exists(SafePath(root, Marker)); }
    static InstallRecord Read(string root) {
        var r = json.Deserialize<InstallRecord>(File.ReadAllText(SafePath(root, Marker)));
        if (r == null || r.Files == null || String.IsNullOrEmpty(r.BackupDir) || !Regex.IsMatch(r.BackupDir, @"^\.repo-easy-mods/backups/[0-9]{14}-[a-f0-9]{32}$")) throw new Exception("Thông tin sao lưu không hợp lệ.");
        SafePath(root, r.BackupDir);
        var allowed = new HashSet<string>(Payload().Keys, StringComparer.OrdinalIgnoreCase);
        allowed.UnionWith(Configs(10, true, 100, false, false).Keys);
        if (r.Files.Count > allowed.Count || r.Files.Count < allowed.Count - 3) throw new Exception("Danh sách khôi phục không hợp lệ.");
        foreach (var f in r.Files) { SafePath(root, f.Path); if (!allowed.Remove(f.Path)) throw new Exception("File khôi phục không hợp lệ."); }
        if (allowed.Any(path => path != GameToolsFile && path != CosmeticsToolsFile && path != VietnameseFile)) throw new Exception("Danh sách khôi phục không hợp lệ.");
        return r;
    }
    static void Save(string root, InstallRecord r) {
        string path = SafePath(root, Marker);
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        string temp = path + ".tmp";
        File.WriteAllText(temp, json.Serialize(r), new UTF8Encoding(false));
        if (File.Exists(path)) File.Replace(temp, path, null); else File.Move(temp, path);
    }
    static Dictionary<string, byte[]> Payload() {
        var files = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        foreach (string resource in Assembly.GetExecutingAssembly().GetManifestResourceNames().Where(n => n.StartsWith("pkg."))) {
            string package = resource.Substring(4, resource.Length - 8);
            bool loader = package.StartsWith("BepInEx-BepInExPack-");
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource))
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Read)) {
                foreach (var entry in zip.Entries) {
                    if (String.IsNullOrEmpty(entry.Name)) continue;
                    string path;
                    if (loader) {
                        if (!entry.FullName.StartsWith("BepInExPack/", StringComparison.Ordinal)) continue;
                        path = entry.FullName.Substring("BepInExPack/".Length);
                    } else {
                        if (!entry.FullName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) continue;
                        path = "BepInEx/plugins/REPOEasyMods/" + package + "/" + entry.Name;
                    }
                    using (var data = new MemoryStream()) { using (var input = entry.Open()) input.CopyTo(data); files.Add(path, data.ToArray()); }
                }
            }
        }
        if (files.Count < 10 || !files.ContainsKey("winhttp.dll")) throw new Exception("Gói cài đặt bị thiếu dữ liệu.");
        if (!files.ContainsKey(GameToolsFile)) throw new Exception("Thiếu gói GameTools.");
        if (!files.ContainsKey(VietnameseFile)) throw new Exception("Thiếu gói Việt hóa.");
        if (!files.ContainsKey(CosmeticsToolsFile)) throw new Exception("Thiếu gói CosmeticsTools.");
        string loaderConfig = "BepInEx/config/BepInEx.cfg";
        files[loaderConfig] = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(files[loaderConfig]).Replace("HideManagerGameObject = false", "HideManagerGameObject = true"));
        return files;
    }
    static Dictionary<string, byte[]> Configs(int players, bool sharing, int chance, bool heal, bool scale) {
        if (players < 2 || players > 20 || chance < 0 || chance > 100) throw new Exception("Cấu hình ngoài giới hạn.");
        var result = new Dictionary<string, byte[]>();
        result["BepInEx/config/ozoka.moreplayers.cfg"] = Encoding.UTF8.GetBytes("[General]\r\nMax Players = " + players + "\r\nPublic Lobby Support = true\r\n");
        result["BepInEx/config/Vippy.SharedUpgradesPlus.cfg"] = Encoding.UTF8.GetBytes(
            "[General]\r\nEnableSharedUpgrades = " + sharing.ToString().ToLowerInvariant() +
            "\r\nSharedUpgradesChance = " + chance + "\r\nSharedUpgradesChanceMode = Whole team\r\nLateJoinSync = true\r\nEnableModdedUpgrades = true\r\nAvoidMoreUpgradesDoubleShare = true\r\nScalePriceWithSharing = " + scale.ToString().ToLowerInvariant() +
            "\r\nPricePerExtraCopy = 100\r\nLogLevel = Off\r\n\r\n[Effects]\r\nEnableSharedUpgradeHeal = " + heal.ToString().ToLowerInvariant() + "\r\nEnableShareNotification = true\r\n");
        return result;
    }
    public static void ValidateGame(string root) {
        if (!File.Exists(Path.Combine(root, "REPO.exe")) || !Directory.Exists(Path.Combine(root, "REPO_Data")))
            throw new Exception("Hãy chọn thư mục có REPO.exe và thư mục REPO_Data.");
        SafePath(root, Marker);
        if (Process.GetProcessesByName("REPO").Length > 0) throw new Exception("Hãy thoát R.E.P.O. trước khi cài, lưu cấu hình hoặc gỡ mod.");
    }
    public static void Install(string root, int players, bool sharing, int chance, bool heal, bool scale, Action<string> log) {
        ValidateGame(root);
        var configs = Configs(players, sharing, chance, heal, scale);
        if (HasInstall(root)) {
            var previous = Read(root);
            if (previous.Status != "installed") throw new Exception("Lần cài trước chưa hoàn tất. Nhấn Gỡ / khôi phục trước khi cài lại.");
            foreach (var f in previous.Files) if (!File.Exists(SafePath(root, f.Path))) throw new Exception("Thiếu file mod. Hãy Gỡ / khôi phục rồi cài lại.");
            var update = new Dictionary<string, byte[]>(configs);
            foreach (var pair in Payload()) if (!previous.Files.Any(f => String.Equals(f.Path, pair.Key, StringComparison.OrdinalIgnoreCase)) || (pair.Key.StartsWith("BepInEx/plugins/REPOEasyMods/",StringComparison.OrdinalIgnoreCase) && pair.Key.EndsWith(".dll",StringComparison.OrdinalIgnoreCase))) update[pair.Key] = pair.Value;
            string loaderPath = "BepInEx/config/BepInEx.cfg";
            string loaderText = File.ReadAllText(SafePath(root, loaderPath));
            loaderText = Regex.Replace(loaderText, @"(?m)^HideManagerGameObject\s*=\s*[^\r\n]+", "HideManagerGameObject = true");
            update[loaderPath] = Encoding.UTF8.GetBytes(loaderText);
            string originalRecord = json.Serialize(previous);
            var before = new Dictionary<string, byte[]>();
            foreach (var pair in update) {
                string target = SafePath(root, pair.Key);
                before[pair.Key] = File.Exists(target) ? File.ReadAllBytes(target) : null;
                if (!previous.Files.Any(f => String.Equals(f.Path, pair.Key, StringComparison.OrdinalIgnoreCase))) {
                    var added = new FileRecord { Path = pair.Key, Existed = File.Exists(target) };
                    if (added.Existed) { string backup = SafePath(root, previous.BackupDir + "/" + added.Path); Directory.CreateDirectory(Path.GetDirectoryName(backup)); File.Copy(target, backup, true); }
                    previous.Files.Add(added);
                }
            }
            previous.Status = "pending"; Save(root, previous);
            try {
                foreach (var pair in update) { string target = SafePath(root, pair.Key); Directory.CreateDirectory(Path.GetDirectoryName(target)); File.WriteAllBytes(target, pair.Value); if(pair.Key.EndsWith(".dll",StringComparison.OrdinalIgnoreCase) && !File.ReadAllBytes(target).SequenceEqual(pair.Value))throw new Exception("Xác minh DLL thất bại: "+pair.Key); }
                previous.Status = "installed"; Save(root, previous);
            } catch {
                foreach (var pair in before) { string target = SafePath(root, pair.Key); if (pair.Value == null) { if (File.Exists(target)) File.Delete(target); } else File.WriteAllBytes(target, pair.Value); }
                Save(root, json.Deserialize<InstallRecord>(originalRecord)); throw;
            }
            log("Đã thay và xác minh các DLL Việt hóa v2. Đóng hẳn game rồi mở lại để áp dụng. F4: GameTools; F5: xu / đồ trang trí."); return;
        }
        string bepin = Path.Combine(root, "BepInEx");
        bool otherDll = Directory.Exists(bepin) && Directory.EnumerateFiles(bepin, "*.dll", SearchOption.AllDirectories).Any();
        if (otherDll || File.Exists(Path.Combine(root, "winhttp.dll")) || File.Exists(Path.Combine(root, "doorstop_config.ini")))
            throw new Exception("Thư mục này đã có bộ nạp mod khác. Trình cài giữ nguyên các file đó. Hãy dùng profile Thunderstore hiện có hoặc khôi phục bản game chưa mod trước khi dùng trình cài này.");
        var files = Payload();
        foreach (var pair in configs) files[pair.Key] = pair.Value;
        var record = new InstallRecord { BackupDir = ".repo-easy-mods/backups/" + DateTime.UtcNow.ToString("yyyyMMddHHmmss") + "-" + Guid.NewGuid().ToString("N"), Status = "pending" };
        foreach (var pair in files) {
            string target = SafePath(root, pair.Key);
            var f = new FileRecord { Path = pair.Key, Existed = File.Exists(target) };
            if (f.Existed) { string backup = SafePath(root, record.BackupDir + "/" + f.Path); Directory.CreateDirectory(Path.GetDirectoryName(backup)); File.Copy(target, backup); }
            record.Files.Add(f);
        }
        Save(root, record);
        try {
            foreach (var pair in files) {
                string target = SafePath(root, pair.Key);
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                File.WriteAllBytes(target, pair.Value);
                log("Đã cài: " + pair.Key);
            }
            record.Status = "installed"; Save(root, record);
        } catch (Exception original) {
            try { Uninstall(root, log); } catch (Exception rollback) { throw new Exception(original.Message + "\r\nKhôi phục chưa hoàn tất: " + rollback.Message); }
            throw;
        }
        log("Hoàn tất! " + players + " người; chia sẻ nâng cấp " + (sharing ? chance + "%" : "tắt") + ". F4: GameTools. F5: cộng xu / đồ trang trí.");
    }
    public static void Uninstall(string root, Action<string> log) {
        ValidateGame(root);
        if (!HasInstall(root)) throw new Exception("Không tìm thấy bản cài do công cụ này tạo.");
        var record = Read(root);
        // Check all backup files before changing anything; a partial restore is retryable.
        foreach (var f in record.Files) if (f.Existed && !File.Exists(SafePath(root, record.BackupDir + "/" + f.Path))) throw new Exception("Thiếu file sao lưu: " + f.Path);
        foreach (var f in record.Files.AsEnumerable().Reverse()) {
            string target = SafePath(root, f.Path);
            if (f.Existed) File.Copy(SafePath(root, record.BackupDir + "/" + f.Path), target, true);
            else if (File.Exists(target)) File.Delete(target);
        }
        // Remove only empty directories created for the tracked files. Other mods/logs stay intact.
        var dirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var f in record.Files) {
            string d = Path.GetDirectoryName(SafePath(root, f.Path));
            while (!String.Equals(d, Path.GetFullPath(root).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase) && d != null) { dirs.Add(d); d = Path.GetDirectoryName(d); }
        }
        foreach (string d in dirs.OrderByDescending(s => s.Length)) if (Directory.Exists(d) && !Directory.EnumerateFileSystemEntries(d).Any()) Directory.Delete(d);
        File.Delete(SafePath(root, Marker));
        log("Đã gỡ các file đã cài và khôi phục bản sao lưu. Các file khác được giữ nguyên.");
    }
    public static string FindGame() {
        var libraries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in new [] { @"HKEY_CURRENT_USER\Software\Valve\Steam", @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam" }) {
            try { var path = Registry.GetValue(key, key.Contains("CURRENT_USER") ? "SteamPath" : "InstallPath", null) as string; if (!String.IsNullOrEmpty(path)) libraries.Add(path.Replace('/', '\\')); } catch { }
        }
        libraries.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam"));
        foreach (string library in libraries.ToArray()) {
            string vdf = Path.Combine(library, @"steamapps\libraryfolders.vdf");
            try { if (File.Exists(vdf)) foreach (Match m in Regex.Matches(File.ReadAllText(vdf), "\"path\"\\s*\"([^\"]+)\"")) libraries.Add(m.Groups[1].Value.Replace("\\\\", "\\")); } catch { }
        }
        foreach (string library in libraries) {
            foreach (string folder in new [] { "REPO", "R.E.P.O." }) {
                string path = Path.Combine(library, "steamapps", "common", folder);
                if (File.Exists(Path.Combine(path, "REPO.exe"))) return path;
            }
            try {
                string common = Path.Combine(library, "steamapps", "common");
                if (Directory.Exists(common)) foreach (string path in Directory.GetDirectories(common)) if (File.Exists(Path.Combine(path, "REPO.exe"))) return path;
            } catch { }
        }
        return "";
    }
    public static void SelfTest(string root) {
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "REPO.exe"), "fake game - never execute"); Directory.CreateDirectory(Path.Combine(root, "REPO_Data"));
        File.WriteAllText(Path.Combine(root, ".doorstop_version"), "original-sentinel");
        Install(root, 10, true, 100, false, false, s => {});
        if (!File.Exists(Path.Combine(root, "winhttp.dll")) || !File.ReadAllText(Path.Combine(root, "BepInEx/config/ozoka.moreplayers.cfg")).Contains("Max Players = 10")) throw new Exception("Install test failed");
        if (!File.Exists(SafePath(root, GameToolsFile))) throw new Exception("GameTools payload test failed");
        if (!File.Exists(SafePath(root, CosmeticsToolsFile))) throw new Exception("CosmeticsTools payload test failed");
        // Recreate the precise v1 file manifest, then exercise the upgrade path.
        var legacy = Read(root); legacy.Files.RemoveAll(f => f.Path == GameToolsFile || f.Path == CosmeticsToolsFile || f.Path == VietnameseFile); Save(root, legacy);
        File.Delete(SafePath(root, GameToolsFile)); File.Delete(SafePath(root, VietnameseFile));
        File.Delete(SafePath(root, CosmeticsToolsFile));
        string loaderPath = SafePath(root, "BepInEx/config/BepInEx.cfg");
        File.WriteAllText(loaderPath, File.ReadAllText(loaderPath).Replace("HideManagerGameObject = true", "HideManagerGameObject = false"));
        Install(root, 16, true, 80, true, false, s => {});
        if (!File.Exists(SafePath(root, GameToolsFile)) || !File.ReadAllText(loaderPath).Contains("HideManagerGameObject = true")) throw new Exception("Legacy upgrade test failed");
        if (!File.ReadAllText(Path.Combine(root, "BepInEx/config/ozoka.moreplayers.cfg")).Contains("Max Players = 16")) throw new Exception("Config test failed");
        if (!File.Exists(SafePath(root, CosmeticsToolsFile))) throw new Exception("V1 CosmeticsTools upgrade test failed");
        var f4Only = Read(root); f4Only.Files.RemoveAll(f => f.Path == CosmeticsToolsFile); Save(root, f4Only);
        File.Delete(SafePath(root, CosmeticsToolsFile));
        string cosmeticConfig = SafePath(root, "BepInEx/config/zichen.cosmeticstools.cfg");
        File.WriteAllText(cosmeticConfig, "user settings sentinel");
        Install(root, 16, true, 80, true, false, s => {});
        if (!File.Exists(SafePath(root, CosmeticsToolsFile)) || File.ReadAllText(cosmeticConfig) != "user settings sentinel") throw new Exception("F4-only upgrade / settings preservation test failed");
        var untranslated=Read(root);untranslated.Files.RemoveAll(f=>f.Path==VietnameseFile);Save(root,untranslated);File.Delete(SafePath(root,VietnameseFile));
        Install(root,16,true,80,true,false,s=>{});
        if(!File.Exists(SafePath(root,VietnameseFile)))throw new Exception("Vietnamese upgrade test failed");
        File.WriteAllText(SafePath(root,GameToolsFile),"old untranslated DLL sentinel");
        File.WriteAllText(SafePath(root,VietnameseFile),"old translator sentinel");
        Install(root,16,true,80,true,false,s=>{});
        if(!File.ReadAllBytes(SafePath(root,GameToolsFile)).SequenceEqual(Payload()[GameToolsFile]) || !File.ReadAllBytes(SafePath(root,VietnameseFile)).SequenceEqual(Payload()[VietnameseFile]))throw new Exception("Existing DLL replacement regression failed");
        File.WriteAllText(Path.Combine(root, "BepInEx/plugins/unrelated.txt"), "keep");
        Uninstall(root, s => {});
        if (File.Exists(Path.Combine(root, "winhttp.dll")) || HasInstall(root) || File.ReadAllText(Path.Combine(root, ".doorstop_version")) != "original-sentinel" || !File.Exists(Path.Combine(root, "BepInEx/plugins/unrelated.txt"))) throw new Exception("Restore test failed");
        bool blocked = false; try { SafePath(root, "../escape"); } catch { blocked = true; } if (!blocked) throw new Exception("Path test failed");
        Install(root, 12, true, 100, false, false, s => {}); Uninstall(root, s => {});
        File.WriteAllText(Path.Combine(root, "winhttp.dll"), "other-loader");
        blocked = false; try { Install(root, 10, true, 100, false, false, s => {}); } catch { blocked = true; } if (!blocked || File.ReadAllText(Path.Combine(root, "winhttp.dll")) != "other-loader") throw new Exception("Existing mod test failed");
        File.WriteAllText(Path.Combine(root, "SELFTEST-PASSED.txt"), "PASS: existing DLL replacement regression, direct localized payload, upgrades, install/uninstall, backup restore, preserve configs/files and path checks.");
    }
}

public static class TeckTheme {
    public static readonly Color Ink = Color.FromArgb(9, 15, 21), Card = Color.FromArgb(17, 26, 34), Edge = Color.FromArgb(40, 54, 64), Muted = Color.FromArgb(153, 172, 184), White = Color.FromArgb(237, 242, 242), Gold = Color.FromArgb(244, 209, 77), Teal = Color.FromArgb(101, 221, 201);
    public static System.Drawing.Text.PrivateFontCollection Fonts = new System.Drawing.Text.PrivateFontCollection();
    static IntPtr fontMemory; [System.Runtime.InteropServices.DllImport("gdi32.dll")] static extern IntPtr AddFontMemResourceEx(IntPtr data, uint size, IntPtr reserved, ref uint count);
    public static void Init() {
        using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("asset.titlefont")) {
            byte[] bytes = new byte[s.Length]; s.Read(bytes, 0, bytes.Length);
            fontMemory = System.Runtime.InteropServices.Marshal.AllocCoTaskMem(bytes.Length);
            System.Runtime.InteropServices.Marshal.Copy(bytes, 0, fontMemory, bytes.Length); Fonts.AddMemoryFont(fontMemory, bytes.Length); uint count=0; AddFontMemResourceEx(fontMemory,(uint)bytes.Length,IntPtr.Zero,ref count);
        }
    }
    public static Font Title(float size) { return new Font(Fonts.Families[0], size, FontStyle.Regular, GraphicsUnit.Pixel); }
    public static Font Body(float size, bool bold) { return new Font(Fonts.Families[0], size * 1.35f, FontStyle.Regular, GraphicsUnit.Pixel); }
}

public class TeckButton : Control {
    public bool Primary, Selected;
    bool hover;
    public TeckButton() { SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Selectable, true); Cursor = Cursors.Hand; Font = TeckTheme.Body(14, true); TabStop = true; }
    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnKeyDown(KeyEventArgs e) { if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space) { OnClick(EventArgs.Empty); e.Handled = true; } base.OnKeyDown(e); }
    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
    protected override void OnPaint(PaintEventArgs e) {
        Color bg = Primary ? (hover ? Color.FromArgb(255, 225, 107) : TeckTheme.Gold) : Selected ? Color.FromArgb(35, 47, 53) : hover ? Color.FromArgb(38, 50, 60) : Color.FromArgb(24, 34, 43);
        if (!Enabled) bg = Color.FromArgb(43, 48, 50);
        e.Graphics.Clear(bg);
        using (var pen = new Pen(Selected ? TeckTheme.Gold : Primary ? bg : TeckTheme.Edge)) e.Graphics.DrawRectangle(pen, 0, 0, Width-1, Height-1);
        TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, Primary ? TeckTheme.Ink : Enabled ? TeckTheme.White : TeckTheme.Muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        if (Focused) ControlPaint.DrawFocusRectangle(e.Graphics, new Rectangle(4,4,Width-8,Height-8));
    }
}

public class TeckForm : Form {
    TextBox folder = new TextBox(), log = new TextBox();
    NumericUpDown players = new NumericUpDown(), chance = new NumericUpDown();
    CheckBox sharing = new CheckBox(), heal = new CheckBox(), scale = new CheckBox();
    TeckButton install, launch, remove, browse, overview, guide, journal;
    List<Control> overviewControls = new List<Control>();
    Image backdrop;
    string status = "Sẵn sàng cài đặt", page = "overview";
    bool busy;
    Point dragPoint;
    public TeckForm() {
        Text = "Teck — R.E.P.O. Launcher"; ClientSize = new Size(1180, 800); FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterScreen; AutoScaleMode = AutoScaleMode.None; Font = TeckTheme.Body(14, false); BackColor = TeckTheme.Ink; ForeColor = TeckTheme.White;
        DoubleBuffered = true;
        using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("asset.background")) backdrop = new Bitmap(s);
        using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("asset.icon")) Icon = new Icon(s);
        overview = Button("Tổng quan", 20, 174, 150, 42, () => Navigate("overview"), false);
        guide = Button("Hướng dẫn", 20, 226, 150, 42, () => Navigate("guide"), false);
        journal = Button("Nhật ký", 20, 278, 150, 42, () => Navigate("log"), false);
        overview.Selected = true;
        Button("—", 1088, 12, 30, 28, () => WindowState = FormWindowState.Minimized, false);
        Button("×", 1132, 12, 30, 28, () => { if (!busy) Close(); }, false);
        browse = Button("Chọn game", 658, 394, 112, 34, Browse, false); overviewControls.Add(browse);
        folder.SetBounds(232, 398, 410, 24); folder.BorderStyle = BorderStyle.None; folder.BackColor = Color.FromArgb(25, 38, 48); folder.ForeColor = TeckTheme.White; folder.Font = TeckTheme.Body(13,false); Controls.Add(folder); overviewControls.Add(folder);
        SetupNumber(players, 232, 466, 10, 2, 20); SetupNumber(chance, 512, 466, 100, 0, 100);
        SetupCheck(sharing, "Chia sẻ nâng cấp cho cả đội", 232, 510, true);
        SetupCheck(heal, "Hồi đầy máu khi nhận nâng cấp máu", 232, 542, false);
        SetupCheck(scale, "Tăng giá nâng cấp theo số đồng đội", 232, 574, false);
        install = Button("CÀI MOD / LƯU", 232, 616, 250, 40, Install, true); overviewControls.Add(install);
        launch = Button("CHƠI QUA STEAM  →", 496, 616, 274, 40, Launch, false); overviewControls.Add(launch);
        remove = Button("Gỡ mod / Khôi phục", 836, 613, 292, 40, Remove, false); overviewControls.Add(remove);
        log.SetBounds(228, 712, 900, 48); log.Multiline = true; log.ReadOnly = true; log.BorderStyle = BorderStyle.None; log.ScrollBars = ScrollBars.Vertical; log.BackColor = TeckTheme.Card; log.ForeColor = TeckTheme.Muted; log.Font = TeckTheme.Body(12,false); Controls.Add(log);
        folder.TextChanged += delegate { LoadSettings(); UpdateStatus(); };
        folder.Text = Installer.FindGame();
        Append("Chọn game → Cài mod → Chơi qua Steam. F4: GameTools • F5: Xu & đồ trang trí.");
        Append("Bản mod cố định • Cài offline • Dùng Teck để gỡ sau khi nâng cấp.");
        LoadSettings(); UpdateStatus();
    }
    protected override bool ShowWithoutActivation { get { return true; } }
    void SetupNumber(NumericUpDown n, int x, int y, int value, int min, int max) {
        n.SetBounds(x,y,258,30); n.Minimum=min; n.Maximum=max; n.Value=value; n.BackColor=Color.FromArgb(25,38,48); n.ForeColor=TeckTheme.White; n.BorderStyle=BorderStyle.FixedSingle; n.Font=TeckTheme.Body(16,true); Controls.Add(n); overviewControls.Add(n);
    }
    void SetupCheck(CheckBox c, string text, int x, int y, bool value) {
        c.Text=text; c.Checked=value; c.SetBounds(x,y,538,26); c.BackColor=TeckTheme.Card; c.ForeColor=TeckTheme.White; c.Font=TeckTheme.Body(13,false); c.Cursor=Cursors.Hand; Controls.Add(c); overviewControls.Add(c);
    }
    TeckButton Button(string text,int x,int y,int w,int h,Action action,bool primary) {
        var b=new TeckButton { Text=text, Primary=primary }; b.SetBounds(x,y,w,h); b.Click += delegate { action(); }; Controls.Add(b); return b;
    }
    void Navigate(string target) {
        page=target; foreach (Control c in overviewControls) c.Visible=target=="overview";
        overview.Selected=target=="overview"; guide.Selected=target=="guide"; journal.Selected=target=="log"; overview.Invalidate(); guide.Invalidate(); journal.Invalidate();
        log.Visible=target!="guide"; log.SetBounds(228,target=="log"?390:712,900,target=="log"?354:48); Invalidate();
    }
    void Browse() { using (var d=new OpenFileDialog { Filter="R.E.P.O.|REPO.exe", Title="Teck — Chọn REPO.exe" }) if(d.ShowDialog(this)==DialogResult.OK) folder.Text=Path.GetDirectoryName(d.FileName); }
    void Install() { RunAction(() => Installer.Install(folder.Text.Trim(),(int)players.Value,sharing.Checked,(int)chance.Value,heal.Checked,scale.Checked,Append), "Đã cài mod • F4 / F5 sẵn sàng"); }
    void Remove() { RunAction(() => Installer.Uninstall(folder.Text.Trim(),Append), "Đã gỡ mod và khôi phục file"); }
    void Launch() { RunAction(() => { Installer.ValidateGame(folder.Text.Trim()); if(!Installer.HasInstall(folder.Text.Trim())) throw new Exception("Hãy cài mod trước khi mở game."); Process.Start(new ProcessStartInfo("steam://rungameid/3241660") { UseShellExecute=true }); Append("Đã gửi lệnh mở game tới Steam. Thư mục được chọn cần là bản game Steam đang sử dụng."); }, "Đã gửi lệnh mở game tới Steam"); }
    void RunAction(Action action,string success) {
        busy=true; foreach(var c in overviewControls) c.Enabled=false; status="Đang xử lý…"; Invalidate(); Update();
        try { action(); status=success; }
        catch(Exception e) { status="Chưa hoàn tất • xem nhật ký"; Append("LỖI: "+e.Message); MessageBox.Show(this,e.Message,"Teck — Chưa hoàn tất",MessageBoxButtons.OK,MessageBoxIcon.Information); }
        finally { busy=false; foreach(var c in overviewControls) c.Enabled=true; Invalidate(); }
    }
    void UpdateStatus() { try { status=Installer.HasInstall(folder.Text.Trim())?"Đã có bộ mod • có thể cập nhật":"Sẵn sàng cài đặt"; } catch { status="Chọn thư mục game để bắt đầu"; } Invalidate(); }
    void Append(string text) { log.AppendText(text+Environment.NewLine); log.SelectionStart=log.TextLength; log.ScrollToCaret(); }
    void LoadSettings() {
        try {
            string root=folder.Text.Trim(), path=Path.Combine(root,"BepInEx/config/ozoka.moreplayers.cfg");
            if(File.Exists(path)) { Match m=Regex.Match(File.ReadAllText(path),@"(?m)^Max Players\s*=\s*(\d+)"); if(m.Success) players.Value=Math.Max(2,Math.Min(20,Int32.Parse(m.Groups[1].Value))); }
            path=Path.Combine(root,"BepInEx/config/Vippy.SharedUpgradesPlus.cfg");
            if(File.Exists(path)) {
                string text=File.ReadAllText(path);
                foreach(var item in new[]{new{Key="EnableSharedUpgrades",Box=sharing},new{Key="EnableSharedUpgradeHeal",Box=heal},new{Key="ScalePriceWithSharing",Box=scale}}) { Match m=Regex.Match(text,@"(?m)^"+item.Key+@"\s*=\s*(true|false)",RegexOptions.IgnoreCase); if(m.Success)item.Box.Checked=Boolean.Parse(m.Groups[1].Value); }
                Match m2=Regex.Match(text,@"(?m)^SharedUpgradesChance\s*=\s*(\d+)"); if(m2.Success)chance.Value=Math.Max(0,Math.Min(100,Int32.Parse(m2.Groups[1].Value)));
            }
        } catch { }
    }
    void TextAt(Graphics g,string text,float size,Color color,int x,int y,int width,int height,bool bold) {
        using(var f=TeckTheme.Body(size,bold)) using(var b=new SolidBrush(color)) using(var fmt=new StringFormat { Trimming=StringTrimming.EllipsisCharacter }) g.DrawString(text,f,b,new RectangleF(x,y,width,height),fmt);
    }
    void LabelAt(Graphics g,string text,int x,int y,int width) { TextAt(g,text,11,TeckTheme.Muted,x,y,width,24,true); }
    void PanelAt(Graphics g,int x,int y,int w,int h) { using(var b=new SolidBrush(TeckTheme.Card))g.FillRectangle(b,x,y,w,h); using(var p=new Pen(TeckTheme.Edge))g.DrawRectangle(p,x,y,w,h); }
    void KeyCard(Graphics g,string key,string heading,string body,int y) {
        using(var b=new SolidBrush(Color.FromArgb(35,47,51)))g.FillRectangle(b,836,y,42,36);
        using(var f=TeckTheme.Title(22))using(var b=new SolidBrush(TeckTheme.Gold))g.DrawString(key,f,b,842,y+1);
        TextAt(g,heading,14,TeckTheme.White,891,y-1,230,24,true); TextAt(g,body,12,TeckTheme.Muted,891,y+25,230,42,false);
    }
    protected override void OnPaint(PaintEventArgs e) {
        base.OnPaint(e); Graphics g=e.Graphics; g.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias; g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        using(var b=new SolidBrush(Color.FromArgb(12,20,27)))g.FillRectangle(b,0,0,190,800);
        var save=g.Save(); g.SetClip(new Rectangle(190,48,990,260)); g.DrawImage(backdrop,new Rectangle(190,-30,990,557));
        using(var shade=new System.Drawing.Drawing2D.LinearGradientBrush(new Rectangle(190,48,990,260),Color.FromArgb(100,9,15,21),Color.FromArgb(0,9,15,21),0f))g.FillRectangle(shade,190,48,990,260); g.Restore(save);
        using(var p=new Pen(TeckTheme.Edge)) { g.DrawLine(p,190,0,190,800); g.DrawLine(p,190,48,1180,48); }
        using(var f=TeckTheme.Title(38))using(var b=new SolidBrush(TeckTheme.Gold))g.DrawString("Teck",f,b,24,26);
        LabelAt(g,"R.E.P.O. LAUNCHER",24,82,160); LabelAt(g,"ĐIỀU KHIỂN",24,140,150);
        LabelAt(g,"TECK / MOD LAUNCHER",212,17,460); LabelAt(g,"OFFLINE EDITION  /  03",842,17,230);
        LabelAt(g,"THU GOM ĐỒ. CHƠI THEO CÁCH CỦA BẠN.",228,78,620);
        using(var f=TeckTheme.Title(90))using(var b=new SolidBrush(TeckTheme.White))g.DrawString("Teck",f,b,224,99);
        // Fine breaks give the heavy title an industrial stencil character.
        using(var b=new SolidBrush(Color.FromArgb(8,17,24)))g.FillRectangle(b,229,155,147,2);
        TextAt(g,"Đội đông hơn. Nhiều cách chơi hơn.",20,TeckTheme.White,230,222,620,32,true);
        TextAt(g,"GameTools F4   /   Xu & trang trí F5   /   Tối đa 20 người",13,TeckTheme.Muted,230,262,650,30,false);
        using(var b=new SolidBrush(TeckTheme.Gold))g.FillRectangle(b,24,694,24,3);
        TextAt(g,"BỘ MOD CỦA BẠN",11,TeckTheme.White,24,712,150,22,true);
        TextAt(g,"7 gói nhúng sẵn\nKhông cần tải riêng",12,TeckTheme.Muted,24,739,150,40,false);
        if(page=="overview") {
            PanelAt(g,208,326,586,348); PanelAt(g,812,326,340,348); PanelAt(g,208,690,944,88);
            TextAt(g,"THIẾT LẬP PHIÊN CHƠI",17,TeckTheme.White,232,344,520,30,true);
            LabelAt(g,"THƯ MỤC GAME • REPO.exe",232,376,410);
            using(var b=new SolidBrush(Color.FromArgb(25,38,48)))g.FillRectangle(b,228,394,420,34);
            LabelAt(g,"SỐ NGƯỜI  /  2–20",232,440,258); LabelAt(g,"TỈ LỆ CHIA SẺ  /  %",512,440,258);
            TextAt(g,"BẮT ĐẦU TRONG 3 BƯỚC",16,TeckTheme.White,836,344,294,30,true);
            KeyCard(g,"01","Chọn game & cài mod","Chọn REPO.exe, rồi nhấn Cài mod / Lưu.",390);
            KeyCard(g,"F4","GameTools","Bất tử, bay, dịch chuyển, hồi sinh…",462);
            KeyCard(g,"F5","Xu & đồ trang trí","Mở Tokens → chọn loại xu → +100.",534);
            TextAt(g,status,12,TeckTheme.Teal,228,692,900,22,true);
        } else if(page=="guide") {
            PanelAt(g,208,326,944,452); TextAt(g,"HƯỚNG DẪN SỬ DỤNG",22,TeckTheme.White,236,348,880,36,true);
            string[] headings={"01  Cài hoặc nâng cấp bộ mod","02  Mở game và dùng menu F4","03  Cộng xu quay đồ bằng F5","04  Gỡ mod khi cần"};
            string[] bodies={"Thoát game → chọn REPO.exe → Cài mod / Lưu. Teck nhận bộ cài cũ trong chat và thêm mod còn thiếu.","Chơi qua Steam → vào game → F4. Các chức năng điều khiển cả đội có thể yêu cầu bạn làm chủ phòng.","F5 → đổi sang English ở góc phải → Tokens → chọn độ hiếm → +100 hoặc nhập số lượng. Xu chỉ cộng cho bạn.","Thoát game → Gỡ mod / Khôi phục. Gỡ mod không hoàn tác xu hoặc đồ bạn đã chỉnh trong save."};
            for(int i=0;i<4;i++){TextAt(g,headings[i],16,TeckTheme.Gold,236,404+i*78,880,26,true);TextAt(g,bodies[i],14,TeckTheme.Muted,236,434+i*78,880,44,false);}
            TextAt(g,"GameTools & CosmeticsTools: tác giả ghi hỗ trợ game v0.4. Bộ cài đã kiểm thử file, chưa kiểm thử trong game.",12,TeckTheme.Muted,236,734,880,34,false);
        } else {
            PanelAt(g,208,326,944,452);TextAt(g,"NHẬT KÝ HOẠT ĐỘNG",22,TeckTheme.White,228,348,900,36,true);
        }
        using(var p=new Pen(Color.FromArgb(55,69,77)))g.DrawRectangle(p,0,0,1179,799);
    }
    protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); if(e.Button==MouseButtons.Left && e.Y<100){dragPoint=e.Location;} }
    protected override void OnMouseMove(MouseEventArgs e) { base.OnMouseMove(e); if(e.Button==MouseButtons.Left && dragPoint!=Point.Empty){Location=new Point(Location.X+e.X-dragPoint.X,Location.Y+e.Y-dragPoint.Y);} }
    protected override void OnMouseUp(MouseEventArgs e) { dragPoint=Point.Empty; base.OnMouseUp(e); }
    protected override void Dispose(bool disposing) { if(disposing && backdrop!=null)backdrop.Dispose();base.Dispose(disposing); }
    [STAThread] public static int Main(string[] args) {
        if(args.Length==2 && args[0]=="--self-test") {try{Installer.SelfTest(Path.GetFullPath(args[1]));return 0;}catch(Exception e){Directory.CreateDirectory(args[1]);File.WriteAllText(Path.Combine(args[1],"SELFTEST-FAILED.txt"),e.ToString());return 1;}}
        Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);TeckTheme.Init();
        if(args.Length==2 && args[0]=="--preview") {
            using(var form=new TeckForm()){form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-4000,-4000);form.Show();Application.DoEvents();using(var bitmap=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bitmap,form.ClientRectangle);bitmap.Save(Path.GetFullPath(args[1]),System.Drawing.Imaging.ImageFormat.Png);}form.Navigate("guide");Application.DoEvents();using(var bitmap=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bitmap,form.ClientRectangle);bitmap.Save(Path.GetFullPath(args[1])+".guide.png",System.Drawing.Imaging.ImageFormat.Png);}form.Close();}return 0;
        }
        Application.Run(new TeckForm());return 0;
    }
}
public class TeckDVDState {
    public string Folder = Installer.FindGame();
    public int Players = 10, Chance = 100;
    public bool Sharing = true, Heal, Scale;
    public void Load() {
        try {
            string path=Path.Combine(Folder,"BepInEx/config/ozoka.moreplayers.cfg");
            if(File.Exists(path)) {var m=Regex.Match(File.ReadAllText(path),@"(?m)^Max Players\s*=\s*(\d+)");if(m.Success)Players=Math.Max(2,Math.Min(20,Int32.Parse(m.Groups[1].Value)));}
            path=Path.Combine(Folder,"BepInEx/config/Vippy.SharedUpgradesPlus.cfg");
            if(File.Exists(path)) {string t=File.ReadAllText(path);Sharing=GetBool(t,"EnableSharedUpgrades",true);Heal=GetBool(t,"EnableSharedUpgradeHeal",false);Scale=GetBool(t,"ScalePriceWithSharing",false);var m=Regex.Match(t,@"(?m)^SharedUpgradesChance\s*=\s*(\d+)");if(m.Success)Chance=Math.Max(0,Math.Min(100,Int32.Parse(m.Groups[1].Value)));}
        }catch{}
    }
    static bool GetBool(string t,string key,bool fallback){var m=Regex.Match(t,@"(?m)^"+key+@"\s*=\s*(true|false)",RegexOptions.IgnoreCase);return m.Success?Boolean.Parse(m.Groups[1].Value):fallback;}
}

public class OrbButton : Control {
    public string SubText="";
    public bool Primary;
    bool hover;
    public OrbButton(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.Selectable|ControlStyles.SupportsTransparentBackColor,true);BackColor=Color.Transparent;Cursor=Cursors.Hand;TabStop=true;}
    protected override void OnResize(EventArgs e){base.OnResize(e);using(var p=new System.Drawing.Drawing2D.GraphicsPath()){p.AddEllipse(1,1,Width-2,Height-2);var old=Region;Region=new Region(p);if(old!=null)old.Dispose();}}
    protected override void OnMouseEnter(EventArgs e){hover=true;Invalidate();base.OnMouseEnter(e);}
    protected override void OnMouseLeave(EventArgs e){hover=false;Invalidate();base.OnMouseLeave(e);}
    protected override void OnKeyDown(KeyEventArgs e){if(e.KeyCode==Keys.Enter||e.KeyCode==Keys.Space){OnClick(EventArgs.Empty);e.Handled=true;}base.OnKeyDown(e);}
    protected override void OnPaint(PaintEventArgs e){
        var g=e.Graphics;g.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit;
        float w=Width,h=Height;
        Color accent=Primary?Color.FromArgb(255,218,119):Color.FromArgb(157,228,237);
        // Transparent parent rendering remains visible through the tinted lens.
        for(int i=8;i>=2;i-=2)using(var p=new Pen(Color.FromArgb(hover?9:5,accent),i))g.DrawEllipse(p,5,5,w-11,h-11);
        using(var shadow=new Pen(Color.FromArgb(38,0,4,9),5))g.DrawEllipse(shadow,5,7,w-11,h-13);
        var bounds=new RectangleF(4,4,w-9,h-9);
        Color top=Primary?Color.FromArgb(hover?148:110,226,184,86):Color.FromArgb(hover?104:66,162,202,216);
        Color bottom=Primary?Color.FromArgb(hover?175:145,77,58,25):Color.FromArgb(hover?146:112,14,36,49);
        using(var lens=new System.Drawing.Drawing2D.LinearGradientBrush(bounds,top,bottom,90f))g.FillEllipse(lens,bounds);
        using(var p=new Pen(Color.FromArgb(hover?175:98,accent),Focused?2:1))g.DrawEllipse(p,bounds);
        using(var p=new Pen(Color.FromArgb(42,238,251,255),1))g.DrawEllipse(p,7,7,w-15,h-15);
        var saved=g.Save();using(var path=new System.Drawing.Drawing2D.GraphicsPath()){
            path.AddEllipse(bounds);g.SetClip(path,System.Drawing.Drawing2D.CombineMode.Intersect);
            using(var sheen=new System.Drawing.Drawing2D.LinearGradientBrush(new RectangleF(w*.14f,h*.08f,w*.72f,h*.43f),Color.FromArgb(hover?85:56,255,255,255),Color.FromArgb(0,255,255,255),90f))g.FillEllipse(sheen,w*.14f,h*.08f,w*.72f,h*.43f);
        }g.Restore(saved);
        using(var p=new Pen(Color.FromArgb(hover?196:128,255,255,255),1))g.DrawArc(p,5,5,w-11,h-11,205,95);
        using(var p=new Pen(Color.FromArgb(hover?114:59,accent),2))g.DrawArc(p,7,7,w-15,h-15,31,92);
        using(var f=Primary?TeckTheme.Title(44):TeckTheme.Body(16,true))using(var fmt=new StringFormat{Alignment=StringAlignment.Center,LineAlignment=StringAlignment.Center}){
            var r=new RectangleF(3,SubText.Length>0?-10:0,w-6,h);
            using(var shadow=new SolidBrush(Color.FromArgb(155,0,7,12)))g.DrawString(Text,f,shadow,new RectangleF(r.X+1,r.Y+2,r.Width,r.Height),fmt);
            using(var b=new SolidBrush(Primary?Color.FromArgb(255,239,190):TeckTheme.White))g.DrawString(Text,f,b,r,fmt);
        }
        if(SubText.Length>0)using(var f=TeckTheme.Body(Primary?12:11,false))using(var b=new SolidBrush(Primary?Color.FromArgb(235,221,177):Color.FromArgb(183,210,220)))using(var fmt=new StringFormat{Alignment=StringAlignment.Center})g.DrawString(SubText,f,b,new RectangleF(5,h/2+20,w-10,35),fmt);
    }

}

public class CircleSettings : Form {
    TextBox folder=new TextBox();NumericUpDown players=new NumericUpDown(),chance=new NumericUpDown();
    CheckBox sharing=new CheckBox(),heal=new CheckBox(),scale=new CheckBox();TeckDVDState state;Action<Action,string> run;Action<string> log;
    public CircleSettings(TeckDVDState s,Action<Action,string> action,Action<string> logger){
        state=s;run=action;log=logger;Text="Teck — Thiết lập";ClientSize=new Size(620,458);FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;StartPosition=FormStartPosition.CenterParent;BackColor=TeckTheme.Ink;ForeColor=TeckTheme.White;Font=TeckTheme.Body(14,false);AutoScaleMode=AutoScaleMode.None;
        LabelAt("THIẾT LẬP PHIÊN CHƠI",24,22,20,true);LabelAt("Chọn game rồi cài hoặc cập nhật bộ mod.",24,58,13,false);
        LabelAt("THƯ MỤC CÓ REPO.exe",24,100,11,true);folder.SetBounds(24,124,436,30);folder.Text=s.Folder;folder.BackColor=TeckTheme.Card;folder.ForeColor=TeckTheme.White;Controls.Add(folder);
        Button("Chọn game",474,122,122,32,()=>{using(var d=new OpenFileDialog{Filter="R.E.P.O.|REPO.exe"})if(d.ShowDialog(this)==DialogResult.OK){folder.Text=Path.GetDirectoryName(d.FileName);state.Folder=folder.Text;state.Load();ReadState();}},false);
        LabelAt("SỐ NGƯỜI / 2–20",24,174,11,true);LabelAt("CHIA SẺ / %",322,174,11,true);
        Number(players,24,202,272,2,20);Number(chance,322,202,274,0,100);
        Check(sharing,"Chia sẻ nâng cấp cho cả đội",252);Check(heal,"Hồi đầy máu khi nhận nâng cấp máu chung",286);Check(scale,"Tăng giá nâng cấp theo số đồng đội",320);ReadState();
        Button("CÀI MOD / LƯU",24,374,272,44,()=>{WriteState();run(()=>Installer.Install(state.Folder,state.Players,state.Sharing,state.Chance,state.Heal,state.Scale,log),"Đã cài mod • F4 / F5 sẵn sàng");},true);
        Button("Gỡ / Khôi phục",322,374,274,44,()=>{WriteState();run(()=>Installer.Uninstall(state.Folder,log),"Đã gỡ và khôi phục file");},false);
    }
    void LabelAt(string t,int x,int y,int size,bool bold){Controls.Add(new Label{Text=t,Location=new Point(x,y),AutoSize=true,Font=TeckTheme.Body(size,bold),ForeColor=bold?TeckTheme.White:TeckTheme.Muted});}
    void Button(string t,int x,int y,int w,int h,Action a,bool primary){var b=new TeckButton{Text=t,Primary=primary};b.SetBounds(x,y,w,h);b.Click+=delegate{a();};Controls.Add(b);}
    void Number(NumericUpDown n,int x,int y,int w,int min,int max){n.SetBounds(x,y,w,30);n.Minimum=min;n.Maximum=max;n.BackColor=TeckTheme.Card;n.ForeColor=TeckTheme.White;Controls.Add(n);}
    void Check(CheckBox c,string t,int y){c.Text=t;c.SetBounds(24,y,572,28);c.ForeColor=TeckTheme.White;Controls.Add(c);}
    void ReadState(){players.Value=state.Players;chance.Value=state.Chance;sharing.Checked=state.Sharing;heal.Checked=state.Heal;scale.Checked=state.Scale;}
    void WriteState(){state.Folder=folder.Text.Trim();state.Players=(int)players.Value;state.Chance=(int)chance.Value;state.Sharing=sharing.Checked;state.Heal=heal.Checked;state.Scale=scale.Checked;}
}

public class ConsoleKey : Control {
    public bool Round;public Color Ink=Color.FromArgb(80,99,107);bool hover,pressed;
    public ConsoleKey(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.Selectable|ControlStyles.SupportsTransparentBackColor,true);BackColor=Color.Transparent;Cursor=Cursors.Hand;TabStop=true;}
    protected override void OnMouseEnter(EventArgs e){hover=true;Invalidate();base.OnMouseEnter(e);}
    protected override void OnMouseLeave(EventArgs e){hover=false;pressed=false;Invalidate();base.OnMouseLeave(e);}
    protected override void OnMouseDown(MouseEventArgs e){pressed=true;Invalidate();base.OnMouseDown(e);}
    protected override void OnMouseUp(MouseEventArgs e){pressed=false;Invalidate();base.OnMouseUp(e);}
    protected override void OnKeyDown(KeyEventArgs e){if(e.KeyCode==Keys.Enter||e.KeyCode==Keys.Space){OnClick(EventArgs.Empty);e.Handled=true;}base.OnKeyDown(e);}
    protected override void OnPaintBackground(PaintEventArgs e){var console=Parent as TeckPS1;if(console!=null)console.PaintMachineBackground(e.Graphics,Bounds);else base.OnPaintBackground(e);}
    protected override void OnPaint(PaintEventArgs e){
        var g=e.Graphics;g.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit;
        if(hover||pressed){using(var b=new SolidBrush(Color.FromArgb(pressed?47:23,167,215,227))){if(Round)g.FillEllipse(b,3,3,Width-7,Height-7);else g.FillRectangle(b,2,2,Width-4,Height-4);}}
        // POWER / RESET / OPEN lettering and their physical button rims are in the photo layer.
        if(Text!="POWER"&&Text!="RESET"&&Text!="OPEN"){
            bool port=Text.StartsWith("1 /")||Text.StartsWith("2 /");Color color=port?Color.FromArgb(228,233,227):Color.FromArgb(52,71,83);if(hover)color=port?Color.FromArgb(150,247,221):Color.FromArgb(13,85,104);
            using(var f=TeckTheme.Body(Text.Length>10?12:14,true))using(var b=new SolidBrush(color))using(var fmt=new StringFormat{Alignment=StringAlignment.Center,LineAlignment=StringAlignment.Center}){
                if(port)using(var shadow=new SolidBrush(Color.FromArgb(160,14,22,27)))g.DrawString(Text,f,shadow,new RectangleF(1,1,Width,Height),fmt);
                g.DrawString(Text,f,b,new RectangleF(0,0,Width,Height),fmt);
            }
        }
        if(Focused)using(var p=new Pen(Color.FromArgb(95,121,143,154),1)){if(Round)g.DrawEllipse(p,4,4,Width-9,Height-9);else g.DrawRectangle(p,2,2,Width-5,Height-5);}
    }

}

public class TeckPS1 : Form {
    TeckDVDState state=new TeckDVDState();Image discArt;Bitmap photoBase,photoLid,photoClosed;Bitmap baseLayer,openLayer,closedLayer,discLayer,machineFrame;bool frameDirty=true;float lidVelocity;Region bodyMask,lidMask,closedMask;
    System.Windows.Forms.Timer animation=new System.Windows.Forms.Timer();System.Diagnostics.Stopwatch clock=System.Diagnostics.Stopwatch.StartNew();double last;
    float angle,lid=1,targetLid=1;bool spinning=true,busy,dragging;Point drag;string status="Sẵn sàng",history="";
    ConsoleKey openKey;ToolTip tips=new ToolTip();
    public TeckPS1(){
        Text="Teck — PS1 Smooth Edition";ClientSize=new Size(1120,880);FormBorderStyle=FormBorderStyle.None;StartPosition=FormStartPosition.CenterScreen;AutoScaleMode=AutoScaleMode.None;BackColor=TeckTheme.Ink;DoubleBuffered=true;
        photoBase=Asset("asset.ps1base");photoLid=Asset("asset.ps1lid");photoClosed=Asset("asset.ps1closed");baseLayer=ScaledImage(photoBase,1030,512);openLayer=ScaledImage(photoLid,625,280);closedLayer=ScaledImage(photoClosed,545,340);
        bodyMask=AlphaRegion(photoBase);lidMask=AlphaRegion(photoLid);closedMask=AlphaRegion(photoClosed);UpdateSilhouette();
        using(Stream s=Assembly.GetExecutingAssembly().GetManifestResourceStream("asset.background"))discArt=new Bitmap(s);discLayer=ScaledImage(discArt,460,460);
        using(Stream s=Assembly.GetExecutingAssembly().GetManifestResourceStream("asset.icon"))Icon=new Icon(s);
        Key("POWER",127,594,140,100,true,Color.FromArgb(20,112,85),Launch,"Mở R.E.P.O. qua Steam");
        Key("RESET",148,523,66,49,true,Color.FromArgb(78,95,106),Install,"Cài / cập nhật DLL mod Việt hóa");
        openKey=Key("OPEN",855,592,142,102,true,Color.FromArgb(62,109,151),ToggleLid,"Mở / đóng nắp máy");
        Key("1 / THIẾT LẬP",354,738,150,28,false,Color.White,Settings,"Chọn game, tùy chỉnh, gỡ / khôi phục");
        Key("2 / HƯỚNG DẪN",619,738,150,28,false,Color.White,Guide,"Hướng dẫn cài và dùng mod");
        Key("F4 / MOD",810,773,80,27,false,Color.White,()=>Info("GameTools / F4","Trong game, nhấn F4 để mở GameTools.\n\nCó các chức năng bay, bất tử, hồi sinh, dịch chuyển và tạo vật phẩm.\nMột số chức năng cho cả đội yêu cầu bạn làm chủ phòng."),"Hướng dẫn menu F4");
        Key("F5 / XU",894,773,80,27,false,Color.White,()=>Info("Xu & trang trí / F5","Trong game → F5 → Xu trang trí.\nChọn độ hiếm rồi nhấn +100 hoặc nhập số lượng.\n\nXu chỉ cộng cho người cài mod. Gỡ mod không hoàn tác xu/đồ trong save."),"Hướng dẫn menu F5");
        Key("—",917,339,28,26,false,Color.White,()=>WindowState=FormWindowState.Minimized,"Thu nhỏ");
        Key("×",950,339,28,26,false,Color.White,()=>{if(!busy)Close();},"Đóng Teck");
        state.Load();RefreshStatus();animation.Interval=16;animation.Tick+=delegate{double now=clock.Elapsed.TotalSeconds;Step((float)Math.Min(.1,now-last));last=now;};animation.Start();
    }
    protected override bool ShowWithoutActivation{get{return true;}}
    static System.Drawing.Drawing2D.GraphicsPath RoundPath(RectangleF r,float radius){var p=new System.Drawing.Drawing2D.GraphicsPath();float d=radius*2;p.AddArc(r.X,r.Y,d,d,180,90);p.AddArc(r.Right-d,r.Y,d,d,270,90);p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);p.AddArc(r.X,r.Bottom-d,d,d,90,90);p.CloseFigure();return p;}
    ConsoleKey Key(string text,int x,int y,int w,int h,bool round,Color ink,Action action,string tip){var b=new ConsoleKey{Text=text,Round=round,Ink=ink};b.SetBounds(x,y,w,h);b.Click+=delegate{if(!busy)action();};Controls.Add(b);tips.SetToolTip(b,tip);return b;}
    void ToggleLid(){targetLid=targetLid>.5f?0:1;tips.SetToolTip(openKey,targetLid>.5f?"Đóng nắp máy":"Mở nắp máy");openKey.Invalidate();}
    void Append(string text){history+=text+Environment.NewLine;}
    void RefreshStatus(){try{status=Installer.HasInstall(state.Folder)?"BỘ MOD ĐÃ CÀI / SẴN SÀNG CẬP NHẬT":"CHỌN GAME TRONG THIẾT LẬP ĐỂ BẮT ĐẦU";}catch{status="CHỌN GAME TRONG THIẾT LẬP";}frameDirty=true;Invalidate();}
    void Run(Action action,string success){busy=true;status="ĐANG XỬ LÝ…";frameDirty=true;Invalidate();Update();try{action();status=success;}catch(Exception e){status="CHƯA HOÀN TẤT";Append("LỖI: "+e.Message);MessageBox.Show(e.Message,"Teck",MessageBoxButtons.OK,MessageBoxIcon.Information);}finally{busy=false;frameDirty=true;Invalidate();}}
    void Install(){if(!File.Exists(Path.Combine(state.Folder,"REPO.exe"))){Settings();return;}Run(()=>Installer.Install(state.Folder,state.Players,state.Sharing,state.Chance,state.Heal,state.Scale,Append),"ĐÃ CÀI & XÁC MINH DLL / HÃY MỞ LẠI GAME");}
    void Launch(){Run(()=>{Installer.ValidateGame(state.Folder);if(!Installer.HasInstall(state.Folder))throw new Exception("Nhấn RESET để cài mod trước, hoặc mở THIẾT LẬP để chọn game.");Process.Start(new ProcessStartInfo("steam://rungameid/3241660"){UseShellExecute=true});Append("Đã gửi lệnh mở game tới Steam.");},"ĐÃ GỬI LỆNH MỞ GAME QUA STEAM");}
    void Settings(){using(var d=new CircleSettings(state,Run,Append))d.ShowDialog(this);RefreshStatus();}
    void Guide(){Info("Bắt đầu với Teck Station","POWER: mở game qua Steam.\nRESET: cài/cập nhật mod Việt hóa.\nOPEN: mở/đóng nắp máy (hoặc bấm vào nắp).\nBấm đĩa khi nắp mở để tạm dừng/xoay tiếp.\n\nCổng 1: Thiết lập → chọn REPO.exe, số người, chia sẻ nâng cấp.\nCổng 2: hướng dẫn. △ F4 / ○ F5: hướng dẫn menu mod.\n\nKéo vùng vỏ máy trống để di chuyển.\nBản Việt hóa DLL v2 được giữ nguyên, không thay đổi save.");}
    void Info(string title,string body){using(var d=new Form{Text="Teck — "+title,ClientSize=new Size(620,420),BackColor=TeckTheme.Ink,ForeColor=TeckTheme.White,FormBorderStyle=FormBorderStyle.FixedDialog,StartPosition=FormStartPosition.CenterParent,MaximizeBox=false,MinimizeBox=false}){var heading=new Label{Text=title,Location=new Point(24,20),Size=new Size(572,40),Font=TeckTheme.Body(21,true),ForeColor=TeckTheme.Gold};d.Controls.Add(heading);var content=new TextBox{Text=body,Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical,BorderStyle=BorderStyle.None,BackColor=TeckTheme.Ink,ForeColor=TeckTheme.White,Font=TeckTheme.Body(15,false)};content.SetBounds(24,76,572,320);d.Controls.Add(content);d.ShowDialog(this);}}
    void Words(Graphics g,string text,float size,Color color,RectangleF area,bool center){using(var f=TeckTheme.Body(size,false))using(var b=new SolidBrush(color))using(var fmt=new StringFormat{Alignment=center?StringAlignment.Center:StringAlignment.Near,LineAlignment=StringAlignment.Center})g.DrawString(text,f,b,area,fmt);}
    static void Poly(Graphics g,Color color,params PointF[] points){using(var b=new SolidBrush(color))g.FillPolygon(b,points);}
    void DrawLid(Graphics g){
        float eased=lid*lid*(3-2*lid),w=510+eased*92,h=344-eased*94,y=367-eased*285,x=556-w/2;
        using(var shadow=new SolidBrush(Color.FromArgb(70,0,0,0)))g.FillEllipse(shadow,x-4,y+15,w+8,h+10);
        var r=new RectangleF(x,y,w,h);
        using(var shell=new System.Drawing.Drawing2D.LinearGradientBrush(r,Color.FromArgb(218,224,224),Color.FromArgb(105,120,128),90f))g.FillEllipse(shell,r);
        using(var p=new Pen(Color.FromArgb(235,241,241),3))g.DrawArc(p,r,185,170);
        using(var p=new Pen(Color.FromArgb(68,83,91),2))g.DrawEllipse(p,r);
        var inner=new RectangleF(x+13,y+12,w-26,h-27);
        Color light=eased>.4f?Color.FromArgb(123,137,143):Color.FromArgb(207,214,216),dark=eased>.4f?Color.FromArgb(57,70,77):Color.FromArgb(166,180,185);
        using(var b=new System.Drawing.Drawing2D.LinearGradientBrush(inner,light,dark,90f))g.FillEllipse(b,inner);
        using(var p=new Pen(Color.FromArgb(95,103,119,125),1))g.DrawEllipse(p,x+30,y+23,w-60,h-48);
        using(var p=new Pen(Color.FromArgb(63,37,51,60),2))g.DrawEllipse(p,x+w*.40f,y+h*.36f,w*.20f,h*.28f);
        var textColor=eased>.4f?Color.FromArgb(191,204,207):Color.FromArgb(73,91,104);
        using(var f=TeckTheme.Title(eased>.4f?52:68))using(var b=new SolidBrush(textColor))using(var fmt=new StringFormat{Alignment=StringAlignment.Center,LineAlignment=StringAlignment.Center})g.DrawString("Teck",f,b,new RectangleF(x,y+h*.25f,w,h*.5f),fmt);
        Words(g,eased>.4f?"CLASSIC / 01  •  DISC SYSTEM":"R.E.P.O. / CLASSIC STATION",11,textColor,new RectangleF(x,y+h*.65f,w,26),true);
        if(eased>.65f){using(var b=new SolidBrush(Color.FromArgb(100,115,123))){g.FillRectangle(b,365,320,22,61);g.FillRectangle(b,726,320,22,61);}using(var p=new Pen(Color.FromArgb(51,65,73),2)){g.DrawLine(p,375,330,365,386);g.DrawLine(p,736,330,746,386);}}
    }
    void DrawDisc(Graphics g){
        var well=new RectangleF(288,361,538,367);using(var b=new System.Drawing.Drawing2D.LinearGradientBrush(well,Color.FromArgb(85,103,113),Color.FromArgb(183,196,201),90f))g.FillEllipse(b,well);
        using(var p=new Pen(Color.FromArgb(225,234,235),2))g.DrawArc(p,well,4,170);
        using(var b=new SolidBrush(Color.FromArgb(31,43,51)))g.FillEllipse(b,310,379,494,332);
        using(var b=new SolidBrush(Color.FromArgb(15,25,31)))g.FillEllipse(b,324,390,466,311);
        var save=g.Save();using(var clip=new System.Drawing.Drawing2D.GraphicsPath()){clip.AddEllipse(351,401,412,276);g.SetClip(clip,System.Drawing.Drawing2D.CombineMode.Intersect);}
        g.TranslateTransform(557,539);g.ScaleTransform(1,.67f);g.RotateTransform(angle);
        g.DrawImage(discArt,new Rectangle(-210,-210,420,420));
        using(var tint=new SolidBrush(Color.FromArgb(58,14,31,42)))g.FillEllipse(tint,-210,-210,420,420);
        using(var f=TeckTheme.Title(32))using(var b=new SolidBrush(Color.FromArgb(235,235,224)))using(var fmt=new StringFormat{Alignment=StringAlignment.Center})g.DrawString("TECK / R.E.P.O.",f,b,new RectangleF(-170,-130,340,60),fmt);
        using(var p=new Pen(Color.FromArgb(187,199,223,230),5))g.DrawEllipse(p,-203,-203,406,406);
        Color[] color={Color.FromArgb(143,113,235,214),Color.FromArgb(140,211,133,231),Color.FromArgb(153,236,203,131)};
        for(int i=0;i<3;i++)using(var p=new Pen(color[i],2))g.DrawArc(p,-198+i*3,-198+i*3,396-i*6,396-i*6,-70+i*5,135);
        g.Restore(save);
        using(var metal=new System.Drawing.Drawing2D.LinearGradientBrush(new Rectangle(516,512,82,55),Color.FromArgb(210,222,225),Color.FromArgb(74,91,105),45f))g.FillEllipse(metal,516,512,82,55);
        using(var b=new SolidBrush(Color.FromArgb(16,24,30)))g.FillEllipse(b,537,524,40,28);
        using(var p=new Pen(Color.FromArgb(177,194,199),2))g.DrawArc(p,537,524,40,28,190,140);
    }
    static Bitmap Asset(string name){using(Stream s=Assembly.GetExecutingAssembly().GetManifestResourceStream(name))return new Bitmap(s);}
    static Region AlphaRegion(Bitmap image){
        var region=new Region();region.MakeEmpty();var rectangle=new Rectangle(0,0,image.Width,image.Height);
        using(var normalized=new Bitmap(image.Width,image.Height,System.Drawing.Imaging.PixelFormat.Format32bppArgb)){
            using(var g=Graphics.FromImage(normalized))g.DrawImageUnscaled(image,0,0);
            var data=normalized.LockBits(rectangle,System.Drawing.Imaging.ImageLockMode.ReadOnly,System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            try{byte[] pixels=new byte[Math.Abs(data.Stride)*data.Height];System.Runtime.InteropServices.Marshal.Copy(data.Scan0,pixels,0,pixels.Length);
                for(int y=0;y<image.Height;y+=2){int start=-1;for(int x=0;x<=image.Width;x++){
                    bool visible=x<image.Width&&pixels[y*data.Stride+x*4+3]>=230;
                    if(visible&&start<0)start=x;
                    if(!visible&&start>=0){if(x-start>=4)region.Union(new Rectangle(start,y,x-start,Math.Min(2,image.Height-y)));start=-1;}
                }}
            }finally{normalized.UnlockBits(data);}
        }return region;
    }
    void DrawPhotoDisc(Graphics g){
        var saved=g.Save();using(var path=new System.Drawing.Drawing2D.GraphicsPath()){path.AddEllipse(332,384,460,280);g.SetClip(path,System.Drawing.Drawing2D.CombineMode.Intersect);}
        g.TranslateTransform(562,524);g.ScaleTransform(1,280f/460f);g.RotateTransform(angle);
        g.DrawImage(discLayer,new Rectangle(-230,-230,460,460));
        using(var tint=new SolidBrush(Color.FromArgb(33,12,24,32)))g.FillEllipse(tint,-230,-230,460,460);
        using(var f=TeckTheme.Title(35))using(var b=new SolidBrush(Color.FromArgb(232,236,222)))using(var fmt=new StringFormat{Alignment=StringAlignment.Center})g.DrawString("TECK / R.E.P.O.",f,b,new RectangleF(-188,-138,376,58),fmt);
        using(var p=new Pen(Color.FromArgb(215,200,214,218),6))g.DrawEllipse(p,-224,-224,448,448);
        for(int i=0;i<4;i++)using(var p=new Pen(Color.FromArgb(115,155+i*18,217-i*16,227-i*9),1.5f))g.DrawArc(p,-218+i*3,-218+i*3,436-i*6,436-i*6,-85+i*7,153);
        g.Restore(saved);
        using(var ring=new System.Drawing.Drawing2D.LinearGradientBrush(new Rectangle(510,492,104,63),Color.FromArgb(209,223,226),Color.FromArgb(113,136,149),35f))g.FillEllipse(ring,510,492,104,63);
        var hub=g.Save();using(var shape=new System.Drawing.Drawing2D.GraphicsPath()){shape.AddEllipse(507,484,116,83);g.SetClip(shape,System.Drawing.Drawing2D.CombineMode.Intersect);}g.DrawImage(photoBase,new RectangleF(507,484,116,83),new RectangleF(793,300,200,143),GraphicsUnit.Pixel);g.Restore(hub);
    }
    float LidFaceBlend(){float t=Math.Max(0,Math.Min(1,(lid-.49f)/.14f));return t*t*(3-2*t);}
    RectangleF LidBounds(){
        // The rear hinge stays at y=350. The lid passes through a narrow edge-on
        // position, then reveals its underside instead of switching images abruptly.
        double tilt=(40+90*lid)*Math.PI/180;
        float half=(float)(170/Math.Cos(40*Math.PI/180)*Math.Cos(tilt));
        float width=545+80*lid,height=Math.Max(10,2*Math.Abs(half));
        return new RectangleF(562-width/2,350+half-height/2,width,height);
    }
    static float AdvanceLid(float current,float target,ref float velocity,float delta){
        const float smoothing=.27f;float omega=2/smoothing,x=omega*delta;
        float decay=1/(1+x+.48f*x*x+.235f*x*x*x),difference=current-target;
        float temp=(velocity+omega*difference)*delta;
        velocity=(velocity-omega*temp)*decay;float next=target+(difference+temp)*decay;
        if(next<0||next>1){next=Math.Max(0,Math.Min(1,next));velocity=0;}
        if(Math.Abs(next-target)<.0004f&&Math.Abs(velocity)<.002f){next=target;velocity=0;}
        return next;
    }
    void Step(float dt){
        if(WindowState==FormWindowState.Minimized)return;bool changed=false;
        if(lid!=targetLid||Math.Abs(lidVelocity)>.0001f){lid=AdvanceLid(lid,targetLid,ref lidVelocity,dt);UpdateSilhouette();changed=true;}
        if(spinning&&!busy){angle=(angle+dt*36)%360;changed=true;}
        if(changed){frameDirty=true;Invalidate();foreach(Control c in Controls)c.Invalidate();}
    }
    static Bitmap ScaledImage(Image original,int width,int height){
        var result=new Bitmap(width,height,System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
        using(var g=Graphics.FromImage(result)){g.CompositingQuality=System.Drawing.Drawing2D.CompositingQuality.HighQuality;g.InterpolationMode=System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;g.DrawImage(original,new Rectangle(0,0,width,height));}return result;
    }
    void EnsureFrame(){
        if(machineFrame==null)machineFrame=new Bitmap(1120,880,System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
        if(!frameDirty)return;
        using(var g=Graphics.FromImage(machineFrame))RenderMachine(g);frameDirty=false;
    }
    public void PaintMachineBackground(Graphics graphics,Rectangle source){EnsureFrame();graphics.DrawImage(machineFrame,new Rectangle(0,0,source.Width,source.Height),source,GraphicsUnit.Pixel);}
    void DrawCoverLayer(Graphics graphics,Image image,RectangleF bounds,float opacity){
        if(opacity<.001f)return;
        if(opacity>.999f){graphics.DrawImage(image,bounds);return;}
        using(var attrs=new System.Drawing.Imaging.ImageAttributes()){
            var matrix=new System.Drawing.Imaging.ColorMatrix();matrix.Matrix33=opacity;attrs.SetColorMatrix(matrix);
            graphics.DrawImage(image,new[]{new PointF(bounds.Left,bounds.Top),new PointF(bounds.Right,bounds.Top),new PointF(bounds.Left,bounds.Bottom)},new RectangleF(0,0,image.Width,image.Height),GraphicsUnit.Pixel,attrs);
        }
    }
    void UpdateSilhouette(){
        if(bodyMask==null)return;
        var next=bodyMask.Clone();using(var matrix=new System.Drawing.Drawing2D.Matrix()){matrix.Scale(1030f/photoBase.Width,512f/photoBase.Height);matrix.Translate(45,310,System.Drawing.Drawing2D.MatrixOrder.Append);next.Transform(matrix);}
        float blend=LidFaceBlend();var bounds=LidBounds();
        for(int i=0;i<2;i++){
            if((i==0&&blend<.02f)||(i==1&&blend>.98f))continue;
            var image=i==0?photoLid:photoClosed;
            using(var cover=(i==0?lidMask:closedMask).Clone())using(var transform=new System.Drawing.Drawing2D.Matrix()){
                transform.Scale(bounds.Width/image.Width,bounds.Height/image.Height);transform.Translate(bounds.X,bounds.Y,System.Drawing.Drawing2D.MatrixOrder.Append);cover.Transform(transform);next.Union(cover);
            }
        }
        var previous=Region;Region=next;if(previous!=null)previous.Dispose();
    }
    protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);EnsureFrame();e.Graphics.DrawImageUnscaled(machineFrame,0,0);}
    void RenderMachine(Graphics g){
        g.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;g.InterpolationMode=System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit;
        g.Clear(Color.FromArgb(11,13,15));g.DrawImageUnscaled(baseLayer,45,310);DrawPhotoDisc(g);
        var cover=LidBounds();float blend=LidFaceBlend();
        DrawCoverLayer(g,closedLayer,cover,1-blend);DrawCoverLayer(g,openLayer,cover,blend);
        if(cover.Height<22)using(var p=new Pen(Color.FromArgb(130,151,158,161),3))g.DrawLine(p,cover.Left+15,350,cover.Right-15,350);
        if(blend<.999f){
            var saved=g.Save();g.TranslateTransform(cover.X,cover.Y);g.ScaleTransform(cover.Width/545,cover.Height/340);
            using(var f=TeckTheme.Title(59))using(var b=new SolidBrush(Color.FromArgb((int)(255*(1-blend)),87,91,95)))using(var fmt=new StringFormat{Alignment=StringAlignment.Center,LineAlignment=StringAlignment.Center})g.DrawString("Teck",f,b,new RectangleF(0,95,545,130),fmt);
            Words(g,"CLASSIC STATION / R.E.P.O.",9,Color.FromArgb((int)(255*(1-blend)),102,108,112),new RectangleF(0,221,545,24),true);g.Restore(saved);
        }
        // Keep the console surface clear, with the brand on the right.
        using(var f=TeckTheme.Title(43))using(var b=new SolidBrush(Color.FromArgb(73,86,96)))using(var fmt=new StringFormat{Alignment=StringAlignment.Center,LineAlignment=StringAlignment.Center})g.DrawString("Teck",f,b,new RectangleF(827,365,156,47),fmt);
        Words(g,"CLASSIC STATION 01",9,Color.FromArgb(88,99,106),new RectangleF(827,409,156,21),true);
        Words(g,"CÀI MOD",10,Color.FromArgb(55,72,81),new RectangleF(126,572,116,20),true);
        Words(g,"MỞ GAME",10,Color.FromArgb(38,94,72),new RectangleF(127,698,140,20),true);
        Words(g,targetLid>.5f?"ĐÓNG NẮP":"MỞ NẮP",10,Color.FromArgb(44,80,107),new RectangleF(855,698,142,20),true);
        Words(g,busy?"ĐANG XỬ LÝ":status.Contains("CHỌN GAME")?"CHƯA CÀI MOD":status.Contains("CHƯA HOÀN TẤT")?"CÓ LỖI":"ĐÃ CÀI MOD",9,Color.FromArgb(218,227,221),new RectangleF(509,751,104,38),true);
        using(var led=new SolidBrush(busy?Color.FromArgb(229,164,57):Color.FromArgb(107,233,154)))g.FillEllipse(led,138,790,7,4);
    }
    protected override void OnMouseDown(MouseEventArgs e){
        base.OnMouseDown(e);if(e.Button!=MouseButtons.Left)return;
        if(lid>.7f&&Math.Pow((e.X-562)/230.0,2)+Math.Pow((e.Y-524)/140.0,2)<1){spinning=!spinning;frameDirty=true;Invalidate();return;}
        var r=LidBounds();if(Math.Pow((e.X-(r.X+r.Width/2))/(r.Width/2),2)+Math.Pow((e.Y-(r.Y+r.Height/2))/(r.Height/2),2)<.96){ToggleLid();return;}
        drag=e.Location;dragging=true;Capture=true;
    }
    protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);if(dragging&&e.Button==MouseButtons.Left)Location=new Point(Location.X+e.X-drag.X,Location.Y+e.Y-drag.Y);}
    protected override void OnMouseUp(MouseEventArgs e){dragging=false;Capture=false;base.OnMouseUp(e);}
    protected override void Dispose(bool disposing){if(disposing){animation.Dispose();tips.Dispose();if(discArt!=null)discArt.Dispose();if(baseLayer!=null)baseLayer.Dispose();if(openLayer!=null)openLayer.Dispose();if(closedLayer!=null)closedLayer.Dispose();if(discLayer!=null)discLayer.Dispose();if(machineFrame!=null)machineFrame.Dispose();if(photoBase!=null)photoBase.Dispose();if(photoLid!=null)photoLid.Dispose();if(photoClosed!=null)photoClosed.Dispose();if(bodyMask!=null)bodyMask.Dispose();if(lidMask!=null)lidMask.Dispose();if(closedMask!=null)closedMask.Dispose();}base.Dispose(disposing);}
    Bitmap Snapshot(float opening,float rotation){lid=targetLid=opening;angle=rotation;UpdateSilhouette();frameDirty=true;Invalidate();foreach(Control c in Controls)c.Invalidate();Update();Application.DoEvents();
        using(var raw=new Bitmap(Width,Height)){DrawToBitmap(raw,ClientRectangle);var result=new Bitmap(Width,Height,System.Drawing.Imaging.PixelFormat.Format32bppArgb);using(var g=Graphics.FromImage(result)){g.Clear(Color.Transparent);g.SetClip(Region,System.Drawing.Drawing2D.CombineMode.Replace);g.DrawImageUnscaled(raw,0,0);}return result;}
    }
    [STAThread] public static int Main(string[] args){
        if(args.Length==2&&args[0]=="--self-test"){try{Installer.SelfTest(Path.GetFullPath(args[1]));return 0;}catch(Exception e){Directory.CreateDirectory(args[1]);File.WriteAllText(Path.Combine(args[1],"SELFTEST-FAILED.txt"),e.ToString());return 1;}}
        Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);TeckTheme.Init();
        if(args.Length==2&&args[0]=="--preview"){
            using(var form=new TeckPS1()){form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-4000,-4000);form.Show();Application.DoEvents();form.animation.Stop();
                using(var open=form.Snapshot(1,0))using(var rotated=form.Snapshot(1,90))using(var closed=form.Snapshot(0,90)){
                    int discChanges=0,lidChanges=0;for(int y=405;y<670;y+=10)for(int x=360;x<760;x+=10)if(open.GetPixel(x,y)!=rotated.GetPixel(x,y))discChanges++;
                    for(int y=100;y<690;y+=20)for(int x=280;x<830;x+=20)if(open.GetPixel(x,y)!=closed.GetPixel(x,y))lidChanges++;
                    if(closed.GetPixel(560,150).A!=0 || open.GetPixel(560,150).A<220 || open.GetPixel(0,0).A!=0)throw new Exception("Transparent silhouette test failed");
                    if(discChanges<100||lidChanges<100)throw new Exception("Lid/disc render test failed");
                    open.Save(args[1],System.Drawing.Imaging.ImageFormat.Png);rotated.Save(args[1]+".spin.png",System.Drawing.Imaging.ImageFormat.Png);closed.Save(args[1]+".closed.png",System.Drawing.Imaging.ImageFormat.Png);
                }
                float position30=1,position60=1,velocity30=0,velocity60=0;
                for(int i=0;i<15;i++)position30=AdvanceLid(position30,0,ref velocity30,1f/30);
                for(int i=0;i<30;i++)position60=AdvanceLid(position60,0,ref velocity60,1f/60);
                if(Math.Abs(position30-position60)>.005f)throw new Exception("Frame rate independent motion test failed");
                float midpoint=position60;float reversed=AdvanceLid(position60,1,ref velocity60,1f/60);
                if(Math.Abs(midpoint-reversed)>.06f)throw new Exception("Mid-animation reverse continuity failed");
                using(var middle=form.Snapshot(.5556f,45))middle.Save(args[1]+".middle.png",System.Drawing.Imaging.ImageFormat.Png);
                form.lid=1;form.targetLid=0;form.Step(.25f);if(!(form.lid>0&&form.lid<1))throw new Exception("Lid animation step failed");for(int i=0;i<150;i++)form.Step(1f/60);if(form.lid!=0)throw new Exception("Lid did not close");form.ToggleLid();for(int i=0;i<150;i++)form.Step(1f/60);if(form.lid!=1)throw new Exception("Lid did not open");
                form.targetLid=0;var benchmark=System.Diagnostics.Stopwatch.StartNew();
                for(int frame=0;frame<60;frame++){form.Step(1f/60);form.EnsureFrame();}
                benchmark.Stop();File.WriteAllText(args[1]+".performance.txt","Offscreen machine rendering + animated window region: "+(benchmark.Elapsed.TotalMilliseconds/60).ToString("F2")+" ms/frame. Not an end-to-end display FPS measurement.");
                form.Close();
            }return 0;
        }
        Application.Run(new TeckPS1());return 0;
    }
}
