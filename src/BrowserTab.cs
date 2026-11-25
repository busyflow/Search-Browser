using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace SearchBrowser
{
    // Represents a single browser tab with lazy WebView2 process creation & sleeping (like Search's Tab.swift)
    public class BrowserTab
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Url { get; set; }
        public string FaviconUrl { get; set; }
        public bool Pinned { get; set; }
        public bool KeepAwake { get; set; }
        public bool IsPrivate { get; set; }
        public string SpaceId { get; set; }
        public string GroupId { get; set; }
        public bool IsLoading { get; set; }
        public bool IsSleeping { get; set; }
        public bool IsVeilPicking { get; set; }
        public DateTime LastTouchedUtc { get; set; }

        // WebView2 instance is null until the tab is actually viewed (Lazy Restoration)
        public WebView2 WebView { get; private set; }
        public bool IsWebViewInitialized { get; private set; }
        private static bool _extensionsLoadedOnce;
        private string _privateDataDir;
        private string _lastHttpsUpgradedUri;
        public string HomeUrl { get; set; }
        public BrowserTab OpenerTab { get; set; }
        public bool IsMuted { get; set; }
        public bool IsAudible { get; set; }
        public double ReadProgress { get; set; }
        public string CustomLetter { get; set; }
        public string CustomTitle { get; set; }
        public bool KeepInEverySpace { get; set; }

        public event Action<BrowserTab> StateChanged;
        public event Action<BrowserTab, string> WebMessageReceived;
        public event Action<BrowserTab, string> NewTabRequested;
        public event Action<BrowserTab, string> PeekRequested;

        public BrowserTab(string url = "about:blank", string title = "New Tab", bool pinned = false, bool isPrivate = false, string spaceId = null, string groupId = null)
        {
            Id = Guid.NewGuid().ToString("N");
            Url = string.IsNullOrWhiteSpace(url) ? "about:blank" : url;
            Title = string.IsNullOrWhiteSpace(title) ? DisplayTitle : title;
            Pinned = pinned;
            IsPrivate = isPrivate;
            SpaceId = spaceId ?? (Store.Prefs != null ? Store.Prefs.ActiveSpaceId : "default");
            GroupId = groupId ?? "";
            LastTouchedUtc = DateTime.UtcNow;
            IsSleeping = true; // Starts lazy without consuming any OS process
        }

        public string DisplayTitle
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(CustomTitle))
                    return CustomTitle;
                if (Url != null && Url.StartsWith("search://settings", StringComparison.OrdinalIgnoreCase))
                    return "Settings";
                if (!string.IsNullOrWhiteSpace(Title) && Title != "about:blank")
                    return Title;
                string host = Address.DisplayHost(Url);
                return string.IsNullOrEmpty(host) ? "New Tab" : host;
            }
        }

        public string PinGlyph
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(CustomLetter))
                    return CustomLetter.Substring(0, 1).ToUpperInvariant();
                if (Url != null && Url.StartsWith("search://settings", StringComparison.OrdinalIgnoreCase))
                    return "S";
                string host = Address.DisplayHost(Url);
                if (!string.IsNullOrEmpty(host))
                    return char.ToUpperInvariant(host[0]).ToString();
                string dt = DisplayTitle;
                return !string.IsNullOrEmpty(dt) ? char.ToUpperInvariant(dt[0]).ToString() : "•";
            }
        }

        public void Touch()
        {
            LastTouchedUtc = DateTime.UtcNow;
        }

        public async Task EnsureAwakeAsync(CoreWebView2Environment sharedEnv, Border host = null)
        {
            try { await EnsureAwakeCoreAsync(sharedEnv, host); }
            catch (Exception ex)
            {
                try { File.AppendAllText(Path.Combine(Store.RootDir, "error.log"), DateTime.Now + " " + ex + "\r\n"); } catch { }
            }
        }

        private async Task EnsureAwakeCoreAsync(CoreWebView2Environment sharedEnv, Border host)
        {
            Touch();
            if (WebView != null && IsWebViewInitialized)
            {
                IsSleeping = false;
                return;
            }

            IsSleeping = false;
            WebView = new WebView2
            {
                DefaultBackgroundColor = Palette.IsDark
                    ? System.Drawing.Color.FromArgb(255, 28, 28, 28)
                    : System.Drawing.Color.FromArgb(255, 255, 255, 255)
            };
            if (host != null) host.Child = WebView; // must be in the visual tree or WebView2 never initializes

            CoreWebView2Environment envToUse = sharedEnv;
            if (IsPrivate)
            {
                _privateDataDir = Path.Combine(Path.GetTempPath(), "SearchBrowser_Priv_" + Id);
                envToUse = await CoreWebView2Environment.CreateAsync(null, _privateDataDir);
            }

            await WebView.EnsureCoreWebView2Async(envToUse);
            var core = WebView.CoreWebView2;

            // Configure clean, fast, quiet WebView2 settings
            core.Settings.AreDefaultScriptDialogsEnabled = true;
            core.Settings.AreBrowserAcceleratorKeysEnabled = false;
            core.Settings.IsStatusBarEnabled = false;
            core.Settings.IsPasswordAutosaveEnabled = false;
            core.Settings.IsGeneralAutofillEnabled = false;
            core.Settings.AreDevToolsEnabled = true;
            core.Profile.PreferredColorScheme = Palette.IsDark
                ? CoreWebView2PreferredColorScheme.Dark
                : CoreWebView2PreferredColorScheme.Light;

            if (Store.Prefs != null && Store.Prefs.DefaultZoomPercent > 0 && Store.Prefs.DefaultZoomPercent != 100)
            {
                WebView.ZoomFactor = Store.Prefs.DefaultZoomPercent / 100.0;
            }
            if (Store.Prefs != null && !string.IsNullOrWhiteSpace(Store.Prefs.DownloadsFolder) && Store.Prefs.DownloadsFolder != "~\\Downloads" && Directory.Exists(Store.Prefs.DownloadsFolder))
            {
                try { core.Profile.DefaultDownloadFolderPath = Store.Prefs.DownloadsFolder; } catch { }
            }

            // Inject Search's startup scripts before any frame renders (including GPC + Peek trigger + Video click-to-play)
            await core.AddScriptToExecuteOnDocumentCreatedAsync(Shield.GetStartupScript());
            await core.AddScriptToExecuteOnDocumentCreatedAsync(PageFeatures.CurtainPickerBootstrapScript);
            if ((Store.Prefs != null && Store.Prefs.SendGpc) || IsPrivate)
            {
                await core.AddScriptToExecuteOnDocumentCreatedAsync("try{Object.defineProperty(Navigator.prototype,'globalPrivacyControl',{get:function(){return true;},configurable:true});}catch(e){}");
            }
            if (Store.Prefs != null && Store.Prefs.VideosWaitForClick)
            {
                await core.AddScriptToExecuteOnDocumentCreatedAsync("document.addEventListener('play',function(e){var v=e.target;if(v&&v.tagName==='VIDEO'&&!v.__userClicked){v.pause();}},true);document.addEventListener('pointerdown',function(e){var v=e.target&&e.target.closest?e.target.closest('video'):null;if(v)v.__userClicked=true;},true);");
            }
            if (Store.Prefs != null && Store.Prefs.ShowReadingProgress)
            {
                await core.AddScriptToExecuteOnDocumentCreatedAsync("window.addEventListener('scroll',function(){try{var h=Math.max(1,document.documentElement.scrollHeight-window.innerHeight);var p=Math.min(1,Math.max(0,window.scrollY/h));if(window.chrome&&window.chrome.webview)window.chrome.webview.postMessage(JSON.stringify({type:'readProgress',val:p.toFixed(3)}));}catch(e){}},{passive:true});");
            }

            // Load enabled extensions once on non-private profile (never into private tabs)
            if (!IsPrivate && !_extensionsLoadedOnce)
            {
                _extensionsLoadedOnce = true;
                await TryLoadExtensionsAsync(core);
            }

            // Network-level ad & tracker blocker (Shield)
            core.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
            core.WebResourceRequested += (s, e) =>
            {
                try
                {
                    if (Store.Prefs.SendGpc || IsPrivate)
                    {
                        e.Request.Headers.SetHeader("Sec-GPC", "1");
                    }
                    if (Shield.ShouldBlockRequest(e.Request.Uri, Url))
                    {
                        e.Response = core.Environment.CreateWebResourceResponse(Stream.Null, 403, "Blocked by Search Shield", "");
                    }
                }
                catch { }
            };

            core.PermissionRequested += (s, e) =>
            {
                if (e.PermissionKind == CoreWebView2PermissionKind.Notifications && (!Store.Prefs.AllowNotifications || IsPrivate))
                {
                    e.State = CoreWebView2PermissionState.Deny;
                }
            };

            core.NavigationStarting += (s, e) =>
            {
                if (string.IsNullOrEmpty(e.Uri) || e.Uri.StartsWith("data:") || e.Uri == "about:blank")
                {
                    return;
                }

                if (Store.Prefs.TryHttpsFirst && e.Uri.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                {
                    string h = Address.DisplayHost(e.Uri);
                    string upgraded = "https://" + e.Uri.Substring(7);
                    if (!Address.IsLocalHost(h) && !string.Equals(_lastHttpsUpgradedUri, upgraded, StringComparison.OrdinalIgnoreCase))
                    {
                        _lastHttpsUpgradedUri = upgraded;
                        e.Cancel = true;
                        core.Navigate(upgraded);
                        return;
                    }
                }

                if (Store.Prefs.RemoveClickTrackers || IsPrivate)
                {
                    string cleaned = CleanLink.Clean(e.Uri, false); // keep utm_ on nav (oriel privacy.ts:41)
                    if (!string.Equals(cleaned, e.Uri, StringComparison.Ordinal))
                    {
                        e.Cancel = true;
                        core.Navigate(cleaned);
                        return;
                    }
                }

                // Pinned tabs external-domain Peek behavior (from Peek.swift peeksFromPin)
                if (Pinned && (Store.Prefs.PinsKeepSite || Store.Prefs.PinsOpenExternalInPeek) && e.IsUserInitiated &&
                    !string.IsNullOrEmpty(Url) && Url != "about:blank" && !Url.StartsWith("search://"))
                {
                    string hereDom = Store.RegistrableDomain(Address.DisplayHost(Url));
                    string thereDom = Store.RegistrableDomain(Address.DisplayHost(e.Uri));
                    if (!string.IsNullOrEmpty(hereDom) && !string.IsNullOrEmpty(thereDom) &&
                        !string.Equals(hereDom, thereDom, StringComparison.OrdinalIgnoreCase) &&
                        !thereDom.Contains("google.com") && !thereDom.Contains("microsoftonline.com") && !thereDom.Contains("auth0.com"))
                    {
                        e.Cancel = true;
                        var peekHandler = PeekRequested;
                        if (peekHandler != null) peekHandler(this, e.Uri);
                        return;
                    }
                }

                IsLoading = true;
                Url = e.Uri;
                if (IsVeilPicking)
                {
                    IsVeilPicking = false;
                }
                NotifyChanged();
            };

            core.DOMContentLoaded += async (s, e) =>
            {
                if (!Url.StartsWith("about:") && !Url.StartsWith("search://"))
                    await ApplySiteStyleSheetsAsync();
            };

            core.NavigationCompleted += async (s, e) =>
            {
                IsLoading = false;
                if (Url != null && (Url == "about:blank" || Url.StartsWith("search://", StringComparison.OrdinalIgnoreCase)))
                {
                    NotifyChanged();
                    return;
                }
                if (core.Source != null && !core.Source.StartsWith("data:") && core.Source != "about:blank")
                {
                    Url = core.Source;
                }
                if (!string.IsNullOrWhiteSpace(core.DocumentTitle) && core.DocumentTitle != "about:blank")
                {
                    Title = core.DocumentTitle;
                }
                if (!string.IsNullOrEmpty(core.FaviconUri))
                {
                    FaviconUrl = core.FaviconUri;
                }
                else
                {
                    string h = Address.DisplayHost(Url);
                    if (!string.IsNullOrEmpty(h))
                        FaviconUrl = "https://" + h + "/favicon.ico";
                }
                if (!IsPrivate && e.IsSuccess && !Url.StartsWith("about:"))
                {
                    Store.RecordHistory(Url, Title);
                }
                await ApplySiteStyleSheetsAsync();
                NotifyChanged();
            };

            core.FaviconChanged += (s, e) =>
            {
                if (!string.IsNullOrEmpty(core.FaviconUri) && !Url.StartsWith("about:") && !Url.StartsWith("search://"))
                {
                    FaviconUrl = core.FaviconUri;
                    NotifyChanged();
                }
            };

            core.DocumentTitleChanged += (s, e) =>
            {
                if (!string.IsNullOrWhiteSpace(core.DocumentTitle) && !Url.StartsWith("about:") && !Url.StartsWith("search://"))
                {
                    Title = core.DocumentTitle;
                    if (!IsPrivate)
                        Store.RecordHistory(Url, Title);
                    NotifyChanged();
                }
            };

            core.SourceChanged += (s, e) =>
            {
                if (!string.IsNullOrWhiteSpace(core.Source) && !core.Source.StartsWith("data:") && core.Source != "about:blank")
                {
                    Url = core.Source;
                    NotifyChanged();
                }
            };

            core.NewWindowRequested += (s, e) =>
            {
                e.Handled = true;
                if (!string.IsNullOrWhiteSpace(e.Uri))
                {
                    var handler = NewTabRequested;
                    if (handler != null) handler(this, e.Uri);
                }
            };

            core.WebMessageReceived += (s, e) =>
            {
                try
                {
                    string msg = e.TryGetWebMessageAsString();
                    if (!string.IsNullOrEmpty(msg))
                    {
                        var handler = WebMessageReceived;
                        if (handler != null) handler(this, msg);
                    }
                }
                catch { }
            };

            core.DownloadStarting += (s, e) =>
            {
                if (Store.Prefs != null && !Store.Prefs.AskWhereToSave && !string.IsNullOrWhiteSpace(Store.Prefs.DownloadsFolder) && Store.Prefs.DownloadsFolder != "~\\Downloads" && Directory.Exists(Store.Prefs.DownloadsFolder))
                {
                    try
                    {
                        string fname = Path.GetFileName(e.ResultFilePath);
                        if (!string.IsNullOrEmpty(fname))
                            e.ResultFilePath = Path.Combine(Store.Prefs.DownloadsFolder, fname);
                    }
                    catch { }
                }
                MainWindow.RecordDownload(e.DownloadOperation);
            };

            try
            {
                core.IsDocumentPlayingAudioChanged += (s, e) =>
                {
                    IsAudible = core.IsDocumentPlayingAudio;
                    NotifyChanged();
                };
            }
            catch { }

            IsWebViewInitialized = true;

            if (!string.IsNullOrWhiteSpace(Url) && Url.StartsWith("search://settings", StringComparison.OrdinalIgnoreCase))
            {
                RenderSettingsTab();
            }
            else if (!string.IsNullOrWhiteSpace(Url) && Url.StartsWith("search://history", StringComparison.OrdinalIgnoreCase))
            {
                RenderHistoryTab();
            }
            else if (!string.IsNullOrWhiteSpace(Url) && Url != "about:blank")
            {
                core.Navigate(Url);
            }
            else
            {
                RenderBlankWall();
            }

            NotifyChanged();
        }

        private async Task TryLoadExtensionsAsync(CoreWebView2 core)
        {
            try
            {
                var method = core.Profile.GetType().GetMethod("AddBrowserExtensionAsync");
                if (method != null)
                {
                    foreach (var ext in Store.Extensions)
                    {
                        if (ext.Enabled && Directory.Exists(ext.FolderPath))
                        {
                            try
                            {
                                dynamic task = method.Invoke(core.Profile, new object[] { ext.FolderPath });
                                if (task != null)
                                {
                                    var loadedExt = await task;
                                    if (loadedExt != null)
                                    {
                                        var idProp = loadedExt.GetType().GetProperty("Id");
                                        if (idProp != null)
                                        {
                                            string runtimeId = idProp.GetValue(loadedExt, null) as string;
                                            if (!string.IsNullOrEmpty(runtimeId))
                                            {
                                                ext.RuntimeExtensionId = runtimeId;
                                                Store.SaveExtensions();
                                            }
                                        }
                                    }
                                }
                            }
                            catch { }
                        }
                    }
                }
            }
            catch { }
        }

        public async Task ApplySiteStyleSheetsAsync()
        {
            if (WebView == null || !IsWebViewInitialized || WebView.CoreWebView2 == null) return;
            try
            {
                string host = Address.DisplayHost(Url);
                string script = PageFeatures.BuildApplySheetsScript(host);
                await WebView.CoreWebView2.ExecuteScriptAsync(script);
            }
            catch { }
        }

        public void RenderBlankWall()
        {
            if (WebView == null || !IsWebViewInitialized || WebView.CoreWebView2 == null) return;
            Url = "about:blank";
            Title = "New Tab";
            FaviconUrl = "";
            WebView.CoreWebView2.NavigateToString(InternalPagesHtml.BuildNewTabHtml());
        }

        public void RenderSettingsTab(string section = "general", int scrollY = 0)
        {
            if (WebView == null || !IsWebViewInitialized || WebView.CoreWebView2 == null) return;
            Url = "search://settings/" + section;
            Title = "Settings";
            FaviconUrl = "";
            WebView.CoreWebView2.NavigateToString(InternalPagesHtml.BuildSettingsTabHtml(section, scrollY));
        }

        public void RenderHistoryTab()
        {
            if (WebView == null || !IsWebViewInitialized || WebView.CoreWebView2 == null) return;
            Url = "search://history";
            Title = "History";
            FaviconUrl = "";
            WebView.CoreWebView2.NavigateToString(InternalPagesHtml.BuildHistoryTabHtml());
        }

        public void CleanupPrivateStorage()
        {
            if (!IsPrivate || string.IsNullOrEmpty(_privateDataDir)) return;
            string dir = _privateDataDir;
            _privateDataDir = null;
            System.Threading.Tasks.Task.Run(async () =>
            {
                await System.Threading.Tasks.Task.Delay(1200);
                try { if (Directory.Exists(dir)) Directory.Delete(dir, true); } catch { }
            });
        }

        public void Navigate(string inputUrlOrSearch)
        {
            Touch();
            if (!string.IsNullOrWhiteSpace(inputUrlOrSearch) && inputUrlOrSearch.StartsWith("search://history", StringComparison.OrdinalIgnoreCase))
            {
                Url = "search://history";
                Title = "History";
                FaviconUrl = "";
                if (IsWebViewInitialized && WebView != null && WebView.CoreWebView2 != null)
                    RenderHistoryTab();
                NotifyChanged();
                return;
            }
            if (!string.IsNullOrWhiteSpace(inputUrlOrSearch) && inputUrlOrSearch.StartsWith("search://settings", StringComparison.OrdinalIgnoreCase))
            {
                Url = inputUrlOrSearch;
                Title = "Settings";
                FaviconUrl = "";
                if (IsWebViewInitialized && WebView != null && WebView.CoreWebView2 != null)
                {
                    string sec = "general";
                    int idx = inputUrlOrSearch.LastIndexOf('/');
                    if (idx > "search://settings".Length - 1 && idx + 1 < inputUrlOrSearch.Length)
                        sec = inputUrlOrSearch.Substring(idx + 1);
                    RenderSettingsTab(sec);
                }
                NotifyChanged();
                return;
            }

            string resolved = Address.ResolveInput(inputUrlOrSearch, Store.Prefs != null ? Store.Prefs.SearchEngineUrl : null);
            Url = resolved;
            if (resolved == "about:blank")
            {
                Title = "New Tab";
                FaviconUrl = "";
                if (IsWebViewInitialized && WebView != null && WebView.CoreWebView2 != null)
                    RenderBlankWall();
            }
            else
            {
                Title = Address.DisplayHost(resolved);
                if (IsWebViewInitialized && WebView != null && WebView.CoreWebView2 != null)
                    WebView.CoreWebView2.Navigate(resolved);
            }
            NotifyChanged();
        }

        public void PutToSleep()
        {
            if (KeepAwake || IsSleeping || WebView == null) return;
            try
            {
                WebView.Dispose();
            }
            catch { }
            WebView = null;
            IsWebViewInitialized = false;
            IsSleeping = true;
            IsLoading = false;
            IsVeilPicking = false;
            NotifyChanged();
        }

        private void NotifyChanged()
        {
            var handler = StateChanged;
            if (handler != null) handler(this);
        }
    }
}
