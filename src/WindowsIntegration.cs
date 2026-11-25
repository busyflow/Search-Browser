using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using Microsoft.Win32;

namespace SearchBrowser
{
    // 1. DEFAULT BROWSER REGISTRATION & WINDOWS 11 SETTINGS DEEP LINK
    // Ported from Oriel's build/installer.nsh & src/main/settings/browser.ts
    public static class DefaultBrowserRegistrar
    {
        public const string AppRegisteredName = "Search";
        public const string ProgIdUrl = "SearchURL";
        public const string ProgIdHtml = "SearchHTML";
        public const string ClientKeyPath = @"Software\Clients\StartMenuInternet\Search";

        public static readonly string[] FileExtensions = new[]
        {
            ".htm", ".html", ".shtml", ".xht", ".xhtml", ".svg", ".webp"
        };

        [DllImport("shell32.dll")]
        private static extern void SHChangeNotify(int wEventId, int uFlags, IntPtr dwItem1, IntPtr dwItem2);

        public static string GetInstalledExePath()
        {
            string localInstalled = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                @"Programs\Search\Search.exe");
            if (File.Exists(localInstalled)) return localInstalled;
            return Process.GetCurrentProcess().MainModule.FileName;
        }

        public static void RegisterInWindowsRegistry(string exePath = null)
        {
            try
            {
                if (string.IsNullOrEmpty(exePath))
                    exePath = GetInstalledExePath();

                RegisterProgId(ProgIdUrl, "Search URL", exePath);
                RegisterProgId(ProgIdHtml, "Search HTML Document", exePath);

                using (var clientKey = Registry.CurrentUser.CreateSubKey(ClientKeyPath))
                {
                    if (clientKey != null)
                    {
                        clientKey.SetValue("", AppRegisteredName);
                        using (var iconKey = clientKey.CreateSubKey("DefaultIcon"))
                            if (iconKey != null) iconKey.SetValue("", exePath + ",0");
                        using (var cmdKey = clientKey.CreateSubKey(@"shell\open\command"))
                            if (cmdKey != null) cmdKey.SetValue("", "\"" + exePath + "\"");

                        using (var capKey = clientKey.CreateSubKey("Capabilities"))
                        {
                            if (capKey != null)
                            {
                                capKey.SetValue("ApplicationName", AppRegisteredName);
                                capKey.SetValue("ApplicationDescription", "Search — A small, fast, quiet web browser");
                                capKey.SetValue("ApplicationIcon", exePath + ",0");

                                using (var smKey = capKey.CreateSubKey("StartMenu"))
                                    if (smKey != null) smKey.SetValue("StartMenuInternet", AppRegisteredName);

                                using (var urlKey = capKey.CreateSubKey("URLAssociations"))
                                {
                                    if (urlKey != null)
                                    {
                                        urlKey.SetValue("http", ProgIdUrl);
                                        urlKey.SetValue("https", ProgIdUrl);
                                        urlKey.SetValue("mailto", ProgIdUrl);
                                    }
                                }

                                using (var faKey = capKey.CreateSubKey("FileAssociations"))
                                {
                                    if (faKey != null)
                                    {
                                        foreach (var ext in FileExtensions)
                                            faKey.SetValue(ext, ProgIdHtml);
                                    }
                                }
                            }
                        }
                    }
                }

                foreach (var ext in FileExtensions)
                {
                    using (var owKey = Registry.CurrentUser.CreateSubKey(@"Software\Classes\" + ext + @"\OpenWithProgids"))
                        if (owKey != null) owKey.SetValue(ProgIdHtml, "");
                }

                using (var regApps = Registry.CurrentUser.CreateSubKey(@"Software\RegisteredApplications"))
                {
                    if (regApps != null)
                        regApps.SetValue(AppRegisteredName, ClientKeyPath + @"\Capabilities");
                }

                // Write Uninstall entry so Search appears cleanly in Windows Settings > Installed Apps
                using (var uninst = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\SearchBrowser"))
                {
                    if (uninst != null)
                    {
                        uninst.SetValue("DisplayName", "Search");
                        uninst.SetValue("DisplayVersion", "1.0.0");
                        uninst.SetValue("Publisher", "Office Commun / Search for Windows");
                        uninst.SetValue("DisplayIcon", exePath + ",0");
                        uninst.SetValue("InstallLocation", Path.GetDirectoryName(exePath));
                        uninst.SetValue("UninstallString", "\"" + exePath + "\" --uninstall");
                        uninst.SetValue("NoModify", 1, RegistryValueKind.DWord);
                        uninst.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                    }
                }

                // Notify Windows Shell & Settings immediately (SHCNE_ASSOCCHANGED = 0x08000000)
                SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero);
            }
            catch { }
        }

        private static void RegisterProgId(string progId, string friendlyName, string exePath)
        {
            using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Classes\" + progId))
            {
                if (key == null) return;
                key.SetValue("", friendlyName);
                key.SetValue("FriendlyTypeName", friendlyName);
                key.SetValue("AppUserModelID", "app.search.browser");

                using (var appKey = key.CreateSubKey("Application"))
                {
                    if (appKey != null)
                    {
                        appKey.SetValue("ApplicationName", AppRegisteredName);
                        appKey.SetValue("ApplicationDescription", "Search — A small, fast, quiet web browser");
                        appKey.SetValue("ApplicationIcon", exePath + ",0");
                        appKey.SetValue("ApplicationCompany", "Search Browser");
                        appKey.SetValue("AppUserModelID", "app.search.browser");
                    }
                }
                using (var iconKey = key.CreateSubKey("DefaultIcon"))
                    if (iconKey != null) iconKey.SetValue("", exePath + ",0");
                using (var cmdKey = key.CreateSubKey(@"shell\open\command"))
                    if (cmdKey != null) cmdKey.SetValue("", "\"" + exePath + "\" -- \"%1\"");
            }
        }

        public static bool IsDefaultBrowser()
        {
            try
            {
                // Check UserChoiceLatest (Windows 11 2025+) then UserChoice
                string[] paths = new[]
                {
                    @"Software\Microsoft\Windows\Shell\Associations\UrlAssociations\https\UserChoiceLatest\ProgId",
                    @"Software\Microsoft\Windows\Shell\Associations\UrlAssociations\https\UserChoiceLatest",
                    @"Software\Microsoft\Windows\Shell\Associations\UrlAssociations\https\UserChoice"
                };
                foreach (var p in paths)
                {
                    using (var k = Registry.CurrentUser.OpenSubKey(p))
                    {
                        if (k != null)
                        {
                            string progId = k.GetValue("ProgId") as string;
                            if (!string.IsNullOrEmpty(progId))
                                return string.Equals(progId, ProgIdUrl, StringComparison.OrdinalIgnoreCase);
                        }
                    }
                }
            }
            catch { }
            return false;
        }

        public static void OpenWindowsDefaultAppsSettings()
        {
            RegisterInWindowsRegistry();
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "ms-settings:defaultapps?registeredAppUser=" + Uri.EscapeDataString(AppRegisteredName),
                    UseShellExecute = true
                });
            }
            catch
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "ms-settings:defaultapps",
                        UseShellExecute = true
                    });
                }
                catch { }
            }
        }
    }

    // 2. WINDOWS HELLO / WINDOWS CREDENTIAL VERIFIER
    // Ported from Oriel's src/main/data/hello.ts (CredUIPromptForWindowsCredentialsW + LogonUserW)
    public static class WindowsHelloVerifier
    {
        private static DateTime _lastVerifiedUtc = DateTime.MinValue;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct CREDUI_INFO
        {
            public int cbSize;
            public IntPtr hwndParent;
            public string pszMessageText;
            public string pszCaptionText;
            public IntPtr hbmBanner;
        }

        [DllImport("credui.dll", CharSet = CharSet.Unicode)]
        private static extern uint CredUIPromptForWindowsCredentialsW(
            ref CREDUI_INFO pUiInfo,
            uint dwAuthError,
            ref uint pulAuthPackage,
            IntPtr pvInAuthBuffer,
            uint ulInAuthBufferSize,
            out IntPtr ppvOutAuthBuffer,
            out uint pulOutAuthBufferSize,
            ref bool pfSave,
            uint dwFlags);

        [DllImport("ole32.dll")]
        private static extern void CoTaskMemFree(IntPtr ptr);

        public static bool VerifyUser(Window ownerWindow, string reasonMessage = "Verify your identity to view or copy saved passwords in Search Vault.")
        {
            // Cache verification for 5 minutes so the user isn't prompted on every single row click
            if ((DateTime.UtcNow - _lastVerifiedUtc).TotalMinutes < 5)
                return true;

            try
            {
                IntPtr hwnd = ownerWindow != null ? new WindowInteropHelper(ownerWindow).Handle : IntPtr.Zero;
                var info = new CREDUI_INFO
                {
                    cbSize = Marshal.SizeOf(typeof(CREDUI_INFO)),
                    hwndParent = hwnd,
                    pszCaptionText = "Search — Windows Security",
                    pszMessageText = reasonMessage,
                    hbmBanner = IntPtr.Zero
                };

                uint authPkg = 0;
                IntPtr outBuf = IntPtr.Zero;
                uint outSize = 0;
                bool save = false;
                // CREDUIWIN_GENERIC (0x1) | CREDUIWIN_ENUMERATE_CURRENT_USER (0x200)
                uint res = CredUIPromptForWindowsCredentialsW(ref info, 0, ref authPkg, IntPtr.Zero, 0, out outBuf, out outSize, ref save, 0x00000201);
                if (outBuf != IntPtr.Zero)
                    CoTaskMemFree(outBuf);

                if (res == 0)
                {
                    _lastVerifiedUtc = DateTime.UtcNow;
                    return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }
    }

    // 3. MAC & PC SHARED FOLDER SYNC ("Search Sync")
    // Ported from Oriel's src/main/tabs/sync.ts & Search's SyncMerge.swift
    [DataContract]
    public class SyncEnvelope
    {
        [DataMember] public int format { get; set; }
        [DataMember] public string device { get; set; }
        [DataMember] public string updatedIso { get; set; }
        [DataMember] public List<SpaceItem> spaces { get; set; }
        [DataMember] public List<SavedTabState> pins { get; set; }
        [DataMember] public List<BookmarkItem> bookmarks { get; set; }
    }

    public static class FolderSync
    {
        private static FileSystemWatcher _watcher;
        public static readonly string DeviceId = Environment.MachineName + "-win";

        public static void InitializeWatcher(Action onExternalSyncArrived)
        {
            StopWatcher();
            string root = Store.Prefs != null ? Store.Prefs.SyncFolderPath : "";
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root)) return;

            try
            {
                string syncDir = Path.Combine(root, "Search Sync");
                Directory.CreateDirectory(syncDir);
                PullFromSyncFolder();
                PushToSyncFolder();

                _watcher = new FileSystemWatcher(syncDir, "*.json")
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName
                };
                _watcher.Changed += (s, e) =>
                {
                    if (!e.Name.StartsWith(DeviceId, StringComparison.OrdinalIgnoreCase))
                    {
                        if (PullFromSyncFolder() && onExternalSyncArrived != null)
                            onExternalSyncArrived();
                    }
                };
                _watcher.EnableRaisingEvents = true;
            }
            catch { }
        }

        public static void StopWatcher()
        {
            if (_watcher != null)
            {
                try { _watcher.Dispose(); } catch { }
                _watcher = null;
            }
        }

        public static void PushToSyncFolder()
        {
            string root = Store.Prefs != null ? Store.Prefs.SyncFolderPath : "";
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root)) return;

            try
            {
                string syncDir = Path.Combine(root, "Search Sync");
                Directory.CreateDirectory(syncDir);

                var env = new SyncEnvelope
                {
                    format = 1,
                    device = DeviceId,
                    updatedIso = DateTime.UtcNow.ToString("o"),
                    spaces = Store.Spaces,
                    pins = (Store.SessionTabs ?? new List<SavedTabState>()).Where(t => t.Pinned).ToList(),
                    bookmarks = Store.Bookmarks
                };

                string file = Path.Combine(syncDir, DeviceId + ".json");
                string tmp = file + ".tmp";
                using (var fs = File.Create(tmp))
                {
                    var ser = new DataContractJsonSerializer(typeof(SyncEnvelope));
                    ser.WriteObject(fs, env);
                }
                if (File.Exists(file)) File.Delete(file);
                File.Move(tmp, file);
            }
            catch { }
        }

        public static bool PullFromSyncFolder()
        {
            string root = Store.Prefs != null ? Store.Prefs.SyncFolderPath : "";
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root)) return false;

            bool changed = false;
            try
            {
                string syncDir = Path.Combine(root, "Search Sync");
                if (!Directory.Exists(syncDir)) return false;

                foreach (var file in Directory.GetFiles(syncDir, "*.json"))
                {
                    if (Path.GetFileNameWithoutExtension(file).Equals(DeviceId, StringComparison.OrdinalIgnoreCase))
                        continue;

                    using (var fs = File.OpenRead(file))
                    {
                        var ser = new DataContractJsonSerializer(typeof(SyncEnvelope));
                        var env = ser.ReadObject(fs) as SyncEnvelope;
                        if (env == null) continue;

                        if (env.bookmarks != null)
                        {
                            foreach (var bm in env.bookmarks)
                            {
                                if (!Store.Bookmarks.Any(b => string.Equals(b.Url, bm.Url, StringComparison.OrdinalIgnoreCase)))
                                {
                                    Store.Bookmarks.Add(bm);
                                    changed = true;
                                }
                            }
                        }

                        if (env.spaces != null)
                        {
                            foreach (var sp in env.spaces)
                            {
                                if (!Store.Spaces.Any(s => string.Equals(s.Id, sp.Id, StringComparison.OrdinalIgnoreCase)))
                                {
                                    Store.Spaces.Add(sp);
                                    changed = true;
                                }
                            }
                        }
                    }
                }

                if (changed)
                {
                    Store.SaveBookmarks();
                    Store.SaveSpaces();
                }
            }
            catch { }
            return changed;
        }
    }
}
