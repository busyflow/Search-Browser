using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;

namespace SearchBrowser
{
    public enum AppearanceMode { System, Light, Dark }
    public enum TabBarLayout { Left, Top }

    [DataContract]
    public class VeilItem
    {
        [DataMember] public string Selector { get; set; }
        [DataMember] public string Label { get; set; }
        [DataMember] public string Note { get; set; }
        [DataMember] public string DateIso { get; set; }
    }

    [DataContract]
    public class BookmarkItem
    {
        [DataMember] public string Id { get; set; }
        [DataMember] public string Title { get; set; }
        [DataMember] public string Url { get; set; }
        [DataMember] public string DateIso { get; set; }
    }

    [DataContract]
    public class HistoryItem
    {
        [DataMember] public string Title { get; set; }
        [DataMember] public string Url { get; set; }
        [DataMember] public string DateIso { get; set; }
        [DataMember] public int Visits { get; set; }
    }

    [DataContract]
    public class TabGroupItem
    {
        [DataMember] public string Id { get; set; }
        [DataMember] public string Name { get; set; }
        [DataMember] public string SpaceId { get; set; }
        [DataMember] public bool Collapsed { get; set; }
    }

    [DataContract]
    public class SavedTabState
    {
        [DataMember] public string Id { get; set; }
        [DataMember] public string Title { get; set; }
        [DataMember] public string Url { get; set; }
        [DataMember] public string FaviconUrl { get; set; }
        [DataMember] public bool Pinned { get; set; }
        [DataMember] public bool KeepAwake { get; set; }
        [DataMember] public string SpaceId { get; set; }
        [DataMember] public string GroupId { get; set; }
    }

    [DataContract]
    public class SpaceItem
    {
        [DataMember] public string Id { get; set; }
        [DataMember] public string Name { get; set; }
        [DataMember] public string AccentHex { get; set; }
    }

    [DataContract]
    public class VaultLogin
    {
        [DataMember] public string Host { get; set; }
        [DataMember] public string User { get; set; }
        [DataMember] public string EncryptedPasswordBase64 { get; set; }
        [DataMember] public string LastUsedIso { get; set; }
    }

    [DataContract]
    public class ExtensionInfo
    {
        [DataMember] public string Id { get; set; }
        [DataMember] public string Name { get; set; }
        [DataMember] public string Version { get; set; }
        [DataMember] public string FolderPath { get; set; }
        [DataMember] public string PopupRelativePath { get; set; }
        [DataMember] public string RuntimeExtensionId { get; set; }
        [DataMember] public bool Enabled { get; set; }
    }

    [DataContract]
    public class AppPreferences
    {
        [DataMember] public string Appearance { get; set; }
        [DataMember] public string TabLayout { get; set; }
        [DataMember] public bool SidebarCollapsed { get; set; }
        [DataMember] public double SidebarWidth { get; set; }
        [DataMember] public bool ShieldEnabled { get; set; }
        [DataMember] public List<string> ShieldPausedHosts { get; set; }
        [DataMember] public bool DuskEnabled { get; set; }
        [DataMember] public List<string> DuskDisabledHosts { get; set; }
        [DataMember] public List<string> DuskForcedHosts { get; set; }
        [DataMember] public bool SiteStylesEnabled { get; set; }
        [DataMember] public bool PeekEnabled { get; set; }
        [DataMember] public bool PinsOpenExternalInPeek { get; set; }
        [DataMember] public bool BenchServerEnabled { get; set; }
        [DataMember] public string SearchEngineUrl { get; set; }
        [DataMember] public string SearchEngineName { get; set; }
        [DataMember] public int AutoSleepMinutes { get; set; }
        [DataMember] public string ActiveSpaceId { get; set; }
        [DataMember] public string ActiveTabId { get; set; }
        [DataMember] public string SplitLeftTabId { get; set; }
        [DataMember] public string SplitRightTabId { get; set; }
        [DataMember] public string BlankWallTheme { get; set; }
        [DataMember] public string TranslateTargetLang { get; set; }
        [DataMember] public string SyncFolderPath { get; set; }
        [DataMember] public bool WindowsHelloForVault { get; set; }

        // Extended Oriel 1:1 Preferences
        [DataMember] public string MailtoHandler { get; set; }
        [DataMember] public bool SyncEnabled { get; set; }
        [DataMember] public bool ConfirmBeforeQuitting { get; set; }
        [DataMember] public string DownloadsFolder { get; set; }
        [DataMember] public bool AskWhereToSave { get; set; }
        [DataMember] public bool AlwaysShowDownloadsButton { get; set; }
        [DataMember] public string OpenDownloadsIn { get; set; }

        [DataMember] public bool KeepDevToolsDark { get; set; }
        [DataMember] public bool SlideSidebar { get; set; }
        [DataMember] public bool CtrlSMakesIconsOnly { get; set; }
        [DataMember] public bool LoadingLineWhenHidden { get; set; }
        [DataMember] public bool PageAsCard { get; set; }
        [DataMember] public bool KeepCardInFullScreen { get; set; }
        [DataMember] public bool AlwaysShowAddressBar { get; set; }
        [DataMember] public bool RoomierTabs { get; set; }
        [DataMember] public bool ButtonsAtTop { get; set; }
        [DataMember] public bool ShowBackBtn { get; set; }
        [DataMember] public bool ShowForwardBtn { get; set; }
        [DataMember] public bool ShowReloadBtn { get; set; }
        [DataMember] public bool ShowExtBtn { get; set; }
        [DataMember] public bool ShowBookmarksBtn { get; set; }
        [DataMember] public bool ShowProfileBtn { get; set; }
        [DataMember] public bool ShowSidebarBtn { get; set; }
        [DataMember] public bool NarrowToIcons { get; set; }
        [DataMember] public bool ShowPlayingInOtherTabs { get; set; }
        [DataMember] public string TabsShowMode { get; set; }
        [DataMember] public bool HighContrastTabText { get; set; }
        [DataMember] public bool ShowReadingProgress { get; set; }
        [DataMember] public bool ShowCountsOnPins { get; set; }
        [DataMember] public bool TabsGiveWhenPressed { get; set; }
        [DataMember] public bool ShowBookmarksBar { get; set; }
        [DataMember] public bool EnergySaver { get; set; }
        [DataMember] public bool ReduceAnimations { get; set; }

        [DataMember] public string BlankWallMode { get; set; }
        [DataMember] public string BlankWallCustomValue { get; set; }
        [DataMember] public string HomeThemeId { get; set; }
        [DataMember] public string HomeTuningJson { get; set; }
        [DataMember] public bool HomeShuffle { get; set; }
        [DataMember] public string HomeImagePath { get; set; }
        [DataMember] public string WindowButtonsStyle { get; set; }
        [DataMember] public int PrefsMigrationVersion { get; set; }

        [DataMember] public string NewTabPosition { get; set; }
        [DataMember] public bool LinksInFrontBesidePage { get; set; }
        [DataMember] public bool LeaveNewTabOnLastClose { get; set; }
        [DataMember] public bool CloseTabGoesToPrevious { get; set; }
        [DataMember] public bool SwipeBackClosesOpenedTab { get; set; }
        [DataMember] public bool ShowHomeBookmarksOnNewTab { get; set; }
        [DataMember] public bool CtrlZReopensClosedTab { get; set; }
        [DataMember] public bool CtrlTabGoesToBackgroundLink { get; set; }
        [DataMember] public bool CtrlTabSkipsPinned { get; set; }
        [DataMember] public bool LoadBackgroundTabsOnSelect { get; set; }
        [DataMember] public bool OpenSizedPopupsInSmallWindows { get; set; }
        [DataMember] public string SleepLevel { get; set; }
        [DataMember] public bool DimSleepingTabs { get; set; }
        [DataMember] public string CloseIdleTabs { get; set; }
        [DataMember] public bool ShowTabNumbersOnCtrl { get; set; }
        [DataMember] public bool ShowAddressOnActiveTab { get; set; }
        [DataMember] public bool ClickActiveTabEditsAddress { get; set; }
        [DataMember] public bool SameTabsInEveryWindow { get; set; }

        [DataMember] public string ShiftReturnEngine { get; set; }
        [DataMember] public bool SearchSuggestions { get; set; }
        [DataMember] public bool OpenExternalInSmallWindow { get; set; }
        [DataMember] public bool GoToExistingTab { get; set; }
        [DataMember] public bool ReadSiteMarkdown { get; set; }
        [DataMember] public bool ReadingModeFollowsTheme { get; set; }
        [DataMember] public string FloatingVideoHost { get; set; }
        [DataMember] public string AutoFloatVideo { get; set; }
        [DataMember] public bool MinimizeFloatingVideo { get; set; }
        [DataMember] public bool VideosWaitForClick { get; set; }
        [DataMember] public bool EscTwiceLeavesFullScreen { get; set; }
        [DataMember] public int DefaultZoomPercent { get; set; }
        [DataMember] public bool CapturePagesEnabled { get; set; }

        [DataMember] public bool SpacesEnabled { get; set; }
        [DataMember] public bool CtrlNumbersSwitchSpaces { get; set; }
        [DataMember] public bool PinsKeepSite { get; set; }
        [DataMember] public bool PinnedRows { get; set; }
        [DataMember] public bool CtrlClickPinOpensNewTab { get; set; }
        [DataMember] public bool NewTabInFloatingField { get; set; }
        [DataMember] public bool FieldInMiddleOfWindow { get; set; }
        [DataMember] public bool AddressBarCommands { get; set; }
        [DataMember] public bool StartWithFreshWindow { get; set; }

        [DataMember] public bool OfferToSavePasswords { get; set; }
        [DataMember] public bool FillInSignIns { get; set; }
        [DataMember] public bool OfferPasskeys { get; set; }

        [DataMember] public bool PreventCrossSiteTracking { get; set; }
        [DataMember] public bool RemoveClickTrackers { get; set; }
        [DataMember] public bool SendGpc { get; set; }
        [DataMember] public bool TryHttpsFirst { get; set; }
        [DataMember] public bool AllowNotifications { get; set; }
        [DataMember] public string ProxyMode { get; set; }

        [DataMember] public string PeekTrigger { get; set; }
        [DataMember] public bool PeekStaysWithTab { get; set; }
        [DataMember] public bool AltClickOpensSplitView { get; set; }
        [DataMember] public bool MiddleClickScroll { get; set; }
        [DataMember] public bool SmoothScrolling { get; set; }
        [DataMember] public bool BounceScroll { get; set; }
        [DataMember] public bool GlobalAddressField { get; set; }
        [DataMember] public bool GlobalOpenInSmallWindow { get; set; }

        public AppPreferences()
        {
            ShieldEnabled = true;
            DuskEnabled = true;
            SiteStylesEnabled = true;
            PeekEnabled = true;
            PinsOpenExternalInPeek = true;
            BenchServerEnabled = true;
            WindowsHelloForVault = false;
            PageAsCard = true;
            ShowBackBtn = true;
            ShowForwardBtn = true;
            ShowReloadBtn = true;
            ShowExtBtn = true;
            ShowBookmarksBtn = true;
            ShowProfileBtn = true;
            DimSleepingTabs = true;
            ShowPlayingInOtherTabs = true;
            ShowCountsOnPins = true;
            PrefsMigrationVersion = 2;
            OpenExternalInSmallWindow = true;
            ShowAddressOnActiveTab = true;
            PinsKeepSite = true;
            OfferToSavePasswords = true;
            FillInSignIns = true;
            OfferPasskeys = true;
            PreventCrossSiteTracking = true;
            RemoveClickTrackers = true;
            SendGpc = true;
            SmoothScrolling = true;
            EnsureDefaults();
        }

        [OnDeserialized]
        private void OnDeserialized(StreamingContext ctx)
        {
            EnsureDefaults();
        }

        public void EnsureDefaults()
        {
            if (string.IsNullOrEmpty(Appearance)) Appearance = "System";
            if (string.IsNullOrEmpty(TabLayout)) TabLayout = "Left";
            if (SidebarWidth <= 100) SidebarWidth = 232;
            if (ShieldPausedHosts == null) ShieldPausedHosts = new List<string>();
            if (DuskDisabledHosts == null) DuskDisabledHosts = new List<string>();
            if (DuskForcedHosts == null) DuskForcedHosts = new List<string>();
            if (string.IsNullOrEmpty(SearchEngineUrl)) SearchEngineUrl = "https://duckduckgo.com/?q=";
            if (string.IsNullOrEmpty(SearchEngineName)) SearchEngineName = "DuckDuckGo";
            if (AutoSleepMinutes <= 0) AutoSleepMinutes = 15;
            if (string.IsNullOrEmpty(ActiveSpaceId)) ActiveSpaceId = "default";
            if (ActiveTabId == null) ActiveTabId = "";
            if (SplitLeftTabId == null) SplitLeftTabId = "";
            if (SplitRightTabId == null) SplitRightTabId = "";
            if (string.IsNullOrEmpty(BlankWallTheme)) BlankWallTheme = "Clean";
            if (string.IsNullOrEmpty(TranslateTargetLang)) TranslateTargetLang = "en";
            if (SyncFolderPath == null) SyncFolderPath = "";
            if (string.IsNullOrEmpty(MailtoHandler)) MailtoHandler = "Your mail app";
            if (string.IsNullOrEmpty(DownloadsFolder)) DownloadsFolder = "~\\Downloads";
            if (string.IsNullOrEmpty(OpenDownloadsIn)) OpenDownloadsIn = "In a tab";
            if (string.IsNullOrEmpty(TabsShowMode)) TabsShowMode = "Letters";
            if (string.IsNullOrEmpty(BlankWallMode)) BlankWallMode = "Off";
            if (BlankWallCustomValue == null) BlankWallCustomValue = "";
            if (string.IsNullOrEmpty(HomeThemeId)) HomeThemeId = "off";
            if (string.IsNullOrEmpty(HomeTuningJson)) HomeTuningJson = "{\"angle\":150,\"density\":0.5,\"gradientA\":\"#E9B8A0\",\"gradientB\":\"#8E86B8\",\"intensity\":1,\"pack\":\"space\",\"scale\":0.5,\"solid\":\"#E8DFD0\"}";
            if (HomeImagePath == null) HomeImagePath = "";
            if (PrefsMigrationVersion < 2)
            {
                // Repair booleans that deserialized as false when upgrading from pre-v0.0009 prefs.json
                if (!ShowBookmarksBtn && !ShowBackBtn && !ShowForwardBtn && !ShowReloadBtn)
                {
                    ShowBookmarksBtn = true;
                    ShowBackBtn = true;
                    ShowForwardBtn = true;
                    ShowReloadBtn = true;
                    ShowExtBtn = true;
                    ShowProfileBtn = true;
                }
                DimSleepingTabs = true;
                PrefsMigrationVersion = 2;
            }
            if (string.IsNullOrEmpty(WindowButtonsStyle)) WindowButtonsStyle = "Inline";
            if (string.IsNullOrEmpty(NewTabPosition)) NewTabPosition = "After current";
            if (string.IsNullOrEmpty(SleepLevel)) SleepLevel = "Normal";
            if (string.IsNullOrEmpty(CloseIdleTabs)) CloseIdleTabs = "Never";
            if (string.IsNullOrEmpty(ShiftReturnEngine)) ShiftReturnEngine = "Off";
            if (string.IsNullOrEmpty(FloatingVideoHost)) FloatingVideoHost = "Search's window";
            if (string.IsNullOrEmpty(AutoFloatVideo)) AutoFloatVideo = "Switching tabs";
            if (DefaultZoomPercent <= 0) DefaultZoomPercent = 100;
            if (string.IsNullOrEmpty(ProxyMode)) ProxyMode = "Off, or Windows' own";
            if (string.IsNullOrEmpty(PeekTrigger)) PeekTrigger = "Shift-click";
        }
    }

    public static class Store
    {
        public static readonly string RootDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SearchBrowser");

        public static readonly string WebViewDataDir = Path.Combine(RootDir, "WebView2Data");
        public static readonly string ExtensionsDir = Path.Combine(RootDir, "Extensions");

        public static AppPreferences Prefs { get; private set; }
        public static List<SavedTabState> SessionTabs { get; private set; }
        public static List<TabGroupItem> TabGroups { get; private set; }
        public static List<SpaceItem> Spaces { get; private set; }
        public static List<BookmarkItem> Bookmarks { get; private set; }
        public static List<HistoryItem> History { get; private set; }
        public static Dictionary<string, List<VeilItem>> HiddenElements { get; private set; }
        public static Dictionary<string, string> SiteStyles { get; private set; }
        public static List<VaultLogin> Logins { get; private set; }
        public static List<ExtensionInfo> Extensions { get; private set; }

        public static void Initialize()
        {
            Directory.CreateDirectory(RootDir);
            Directory.CreateDirectory(WebViewDataDir);
            Directory.CreateDirectory(ExtensionsDir);

            Prefs = LoadJson<AppPreferences>("prefs.json");
            if (Prefs == null)
            {
                Prefs = new AppPreferences();
            }
            else
            {
                Prefs.EnsureDefaults();
            }

            Spaces = LoadJson<List<SpaceItem>>("spaces.json") ?? new List<SpaceItem>();
            if (Spaces.Count == 0)
            {
                Spaces.Add(new SpaceItem { Id = "default", Name = "Personal", AccentHex = "" });
                Spaces.Add(new SpaceItem { Id = "work", Name = "Work", AccentHex = "#EEF2FF" });
                Spaces.Add(new SpaceItem { Id = "research", Name = "Research", AccentHex = "#F0FDF4" });
            }

            SessionTabs = LoadJson<List<SavedTabState>>("session.json") ?? new List<SavedTabState>();
            TabGroups = LoadJson<List<TabGroupItem>>("groups.json") ?? new List<TabGroupItem>();
            Bookmarks = LoadJson<List<BookmarkItem>>("bookmarks.json") ?? new List<BookmarkItem>();
            History = LoadJson<List<HistoryItem>>("history.json") ?? new List<HistoryItem>();
            HiddenElements = LoadJson<Dictionary<string, List<VeilItem>>>("hidden.json") ?? new Dictionary<string, List<VeilItem>>();
            SiteStyles = LoadJson<Dictionary<string, string>>("styles.json") ?? new Dictionary<string, string>();
            Logins = LoadJson<List<VaultLogin>>("vault.json") ?? new List<VaultLogin>();
            Extensions = LoadJson<List<ExtensionInfo>>("extensions.json") ?? new List<ExtensionInfo>();
        }

        public static void SavePrefs() { SaveJson("prefs.json", Prefs); }
        public static void SaveSpaces() { SaveJson("spaces.json", Spaces); }
        public static void SaveGroups() { SaveJson("groups.json", TabGroups); }
        public static void SaveSession(List<SavedTabState> tabs) { SessionTabs = tabs; SaveJson("session.json", SessionTabs); }
        public static void SaveBookmarks() { SaveJson("bookmarks.json", Bookmarks); }
        public static void SaveHistory() { SaveJson("history.json", History); }
        public static void SaveHidden() { SaveJson("hidden.json", HiddenElements); }
        public static void SaveSiteStyles() { SaveJson("styles.json", SiteStyles); }
        public static void SaveVault() { SaveJson("vault.json", Logins); }
        public static void SaveExtensions() { SaveJson("extensions.json", Extensions); }

        public static void RecordHistory(string url, string title)
        {
            if (string.IsNullOrWhiteSpace(url) || url.StartsWith("about:") || url.StartsWith("data:") || url.StartsWith("search:"))
                return;

            var existing = History.FirstOrDefault(h => string.Equals(h.Url, url, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                existing.Title = string.IsNullOrWhiteSpace(title) ? existing.Title : title;
                existing.DateIso = DateTime.UtcNow.ToString("o");
                existing.Visits++;
                History.Remove(existing);
                History.Insert(0, existing);
            }
            else
            {
                History.Insert(0, new HistoryItem
                {
                    Url = url,
                    Title = string.IsNullOrWhiteSpace(title) ? url : title,
                    DateIso = DateTime.UtcNow.ToString("o"),
                    Visits = 1
                });
            }

            if (History.Count > 2500)
                History.RemoveRange(2500, History.Count - 2500);

            SaveHistory();
        }

        public static void SavePassword(string host, string user, string plainPassword)
        {
            if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(user) || string.IsNullOrEmpty(plainPassword))
                return;

            host = NormalizeHost(host);
            byte[] bytes = Encoding.UTF8.GetBytes(plainPassword);
            byte[] enc = ProtectedData.Protect(bytes, Encoding.UTF8.GetBytes("SearchBrowser.Vault:" + host), DataProtectionScope.CurrentUser);
            string b64 = Convert.ToBase64String(enc);

            var existing = Logins.FirstOrDefault(l =>
                string.Equals(l.Host, host, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(l.User, user, StringComparison.OrdinalIgnoreCase));

            if (existing != null)
            {
                existing.EncryptedPasswordBase64 = b64;
                existing.LastUsedIso = DateTime.UtcNow.ToString("o");
            }
            else
            {
                Logins.Add(new VaultLogin
                {
                    Host = host,
                    User = user,
                    EncryptedPasswordBase64 = b64,
                    LastUsedIso = DateTime.UtcNow.ToString("o")
                });
            }
            SaveVault();
        }

        public static string DecryptPassword(VaultLogin login)
        {
            try
            {
                byte[] enc = Convert.FromBase64String(login.EncryptedPasswordBase64);
                byte[] dec = ProtectedData.Unprotect(enc, Encoding.UTF8.GetBytes("SearchBrowser.Vault:" + login.Host), DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(dec);
            }
            catch
            {
                return "";
            }
        }

        public static List<VaultLogin> GetLoginsForHost(string host)
        {
            host = NormalizeHost(host);
            if (string.IsNullOrEmpty(host)) return new List<VaultLogin>();
            string reg = RegistrableDomain(host);
            return Logins.Where(l =>
                string.Equals(l.Host, host, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(RegistrableDomain(l.Host), reg, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(l => string.Equals(l.Host, host, StringComparison.OrdinalIgnoreCase))
                .ThenByDescending(l => l.LastUsedIso)
                .ToList();
        }

        public static string NormalizeHost(string hostOrUrl)
        {
            if (string.IsNullOrWhiteSpace(hostOrUrl)) return "";
            string h = hostOrUrl.Trim().ToLowerInvariant();
            if (h.Contains("://"))
            {
                Uri u;
                if (Uri.TryCreate(h, UriKind.Absolute, out u)) h = u.Host.ToLowerInvariant();
            }
            if (h.StartsWith("www.")) h = h.Substring(4);
            return h;
        }

        public static string RegistrableDomain(string host)
        {
            if (string.IsNullOrEmpty(host)) return "";
            var parts = host.Split('.');
            if (parts.Length <= 2) return host;
            return parts[parts.Length - 2] + "." + parts[parts.Length - 1];
        }

        private static T LoadJson<T>(string fileName) where T : class
        {
            string path = Path.Combine(RootDir, fileName);
            if (!File.Exists(path)) return null;
            try
            {
                using (var fs = File.OpenRead(path))
                {
                    var ser = new DataContractJsonSerializer(typeof(T));
                    return ser.ReadObject(fs) as T;
                }
            }
            catch
            {
                return null;
            }
        }

        private static void SaveJson<T>(string fileName, T data)
        {
            string path = Path.Combine(RootDir, fileName);
            string tmp = path + ".tmp";
            try
            {
                using (var fs = File.Create(tmp))
                {
                    var ser = new DataContractJsonSerializer(typeof(T));
                    ser.WriteObject(fs, data);
                }
                if (File.Exists(path)) File.Delete(path);
                File.Move(tmp, path);
            }
            catch { }
        }
    }
}
