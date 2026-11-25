using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shell;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace SearchBrowser
{
    public class DownloadRecord
    {
        public string FileName { get; set; }
        public string ResultPath { get; set; }
        public string State { get; set; }
        public long ReceivedBytes { get; set; }
        public long TotalBytes { get; set; }
    }

    public class MainWindow : Window
    {
        public static List<DownloadRecord> Downloads = new List<DownloadRecord>();
        public static event Action DownloadsUpdated;

        public static void RecordDownload(CoreWebView2DownloadOperation op)
        {
            var rec = new DownloadRecord
            {
                FileName = Path.GetFileName(op.ResultFilePath),
                ResultPath = op.ResultFilePath,
                State = "Downloading...",
                ReceivedBytes = 0,
                TotalBytes = (long)(op.TotalBytesToReceive ?? 0)
            };
            Downloads.Insert(0, rec);
            op.BytesReceivedChanged += (s, e) =>
            {
                rec.ReceivedBytes = op.BytesReceived;
                rec.TotalBytes = (long)(op.TotalBytesToReceive ?? 0);
                if (DownloadsUpdated != null) DownloadsUpdated();
            };
            op.StateChanged += (s, e) =>
            {
                rec.State = op.State.ToString();
                if (DownloadsUpdated != null) DownloadsUpdated();
            };
            if (DownloadsUpdated != null) DownloadsUpdated();
        }

        private CoreWebView2Environment _sharedEnv;
        private readonly List<BrowserTab> _allTabs = new List<BrowserTab>();
        private readonly Stack<SavedTabState> _closedTabs = new Stack<SavedTabState>();

        private BrowserTab _activeTab;
        private BrowserTab _splitLeftTab;
        private BrowserTab _splitRightTab;
        private bool _isSplitView;
        private BrowserTab _draggedTab;

        // Root layout elements
        private Grid _rootGrid;
        private Border _sidebarBorder;
        private Border _sidebarHoverHotzone;
        private Border _floatingSidebarBorder;
        private Border _topTabBarBorder;
        private Border _pageCardBorder;
        private Grid _stageGrid;
        private Border _leftPaneHost;
        private Border _rightPaneHost;
        private GridSplitter _splitDivider;

        // Sidebar / Tab bar elements
        private WrapPanel _pinsPanel;
        private Grid _tabsContainerGrid;
        private Border _glidingTabPill;
        private TranslateTransform _glidingPillTransform;
        private StackPanel _tabsStack;
        private StackPanel _topTabsStack;
        private TextBlock _spaceTitleText;
        private StackPanel _spaceDotsPanel;
        private Border _spaceFooterBtn;
        private Border _veilBanner;
        private TextBlock _veilBannerText;

        // Link Peek modal card (Peek.swift — Shift+Click on any link or external click from a pinned tab)
        private Grid _peekOverlay;
        private Border _peekCard;
        private TextBlock _peekTitleText;
        private Border _peekWebHost;
        private BrowserTab _peekTab;

        // Extension Popup / Page Style floating card
        private Grid _extPopupOverlay;
        private TextBlock _extPopupTitle;
        private Border _extPopupHost;
        private WebView2 _extPopupWebView;

        // Floating Omnibox / Tab Switcher (Ctrl+L / Ctrl+K / Ctrl+T)
        private System.Windows.Controls.Primitives.Popup _omniboxPopup;
        private Grid _omniboxOverlay;
        private Border _omniboxCard;
        private Border _siteChipBorder;
        private TextBlock _siteChipText;
        private TextBox _omniboxInput;
        private StackPanel _omniboxOffersStack;
        private Border _omniboxOffersBorder;
        private bool _omniboxIsTabSwitcherMode;
        private bool _omniboxIsNewTabMode;
        private int _selectedOfferIndex = -1;
        private readonly List<Action> _currentOfferActions = new List<Action>();

        // Modal drawer for Settings / History / Bookmarks / Vault / Hidden / Extensions / Downloads / Site Styles / AI
        private Grid _drawerOverlay;
        private Border _drawerCard;
        private TextBlock _drawerTitle;
        private StackPanel _drawerContent;

        // Floating Find bar (Ctrl+F)
        private Border _findBar;
        private TextBox _findInput;

        // Password save prompt banner
        private Border _passwordOfferBanner;
        private TextBlock _passwordOfferText;
        private string _pendingSaveHost;
        private string _pendingSaveUser;
        private string _pendingSavePass;

        private DispatcherTimer _sleepTimer;

        public MainWindow()
        {
            Store.Initialize();
            Palette.ResolveTheme();

            Title = "Search";
            Width = 1280;
            Height = 820;
            MinWidth = 680;
            MinHeight = 460;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            UseLayoutRounding = true;
            SnapsToDevicePixels = true;

            try
            {
                string icoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "search.ico");
                if (File.Exists(icoPath))
                    Icon = new BitmapImage(new Uri(icoPath));
            }
            catch { }

            WindowChrome.SetWindowChrome(this, new WindowChrome
            {
                CaptionHeight = 0,
                ResizeBorderThickness = new Thickness(6),
                GlassFrameThickness = new Thickness(0),
                CornerRadius = new CornerRadius(0),
                UseAeroCaptionButtons = false
            });

            BuildUserInterface();
            Loaded += OnWindowLoaded;
            Closing += OnWindowClosing;
            PreviewKeyDown += OnWindowPreviewKeyDown;

            _sleepTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(60) };
            _sleepTimer.Tick += (s, e) => CheckTabAutoSleep();
            _sleepTimer.Start();

            if (Store.Prefs.BenchServerEnabled)
            {
                BenchServer.Start(this);
            }

            DefaultBrowserRegistrar.RegisterInWindowsRegistry();
            FolderSync.InitializeWatcher(() => Dispatcher.InvokeAsync(() =>
            {
                RefreshSpaceHeader();
                RefreshTabRows();
            }));
        }

        private void BuildUserInterface()
        {
            Background = Palette.Frame;

            _rootGrid = new Grid();
            _rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(52) }); // Full-width 52px top band (matching Oriel Screenshot 1)
            _rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            _rootGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            _rootGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // 1. FULL-WIDTH TOP BAND (52px high: Back, Forward, Reload on far left; Center tabs when in Top mode; Bookmark ribbon + Minimize, Maximize, Close on far right)
            _topTabBarBorder = new Border
            {
                Background = Palette.Frame,
                Padding = new Thickness(10, 0, 10, 0)
            };
            var topBarGrid = new Grid();
            topBarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            topBarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            topBarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Top-left helm: <  >  ↻ (26x26 doors, 4px gap)
            var helmStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };
            helmStack.Children.Add(CreateVectorIconButton(VectorIcons.BackPath, "Back (Alt+Left / Ctrl+[)", () =>
            {
                if (_activeTab != null && _activeTab.WebView != null && _activeTab.WebView.CanGoBack) _activeTab.WebView.GoBack();
            }));
            helmStack.Children.Add(CreateVectorIconButton(VectorIcons.ForwardPath, "Forward (Alt+Right / Ctrl+])", () =>
            {
                if (_activeTab != null && _activeTab.WebView != null && _activeTab.WebView.CanGoForward) _activeTab.WebView.GoForward();
            }));
            helmStack.Children.Add(CreateVectorIconButton(VectorIcons.ReloadPath, "Reload (Ctrl+R)", () =>
            {
                if (_activeTab != null && _activeTab.IsWebViewInitialized && _activeTab.WebView != null)
                {
                    if (_activeTab.Url == "about:blank") _activeTab.RenderBlankWall();
                    else if (_activeTab.Url != null && _activeTab.Url.StartsWith("search://settings")) _activeTab.RenderSettingsTab();
                    else _activeTab.WebView.CoreWebView2.Reload();
                }
            }));
            topBarGrid.Children.Add(helmStack);

            // Top-center horizontal tab strip (only visible when TabLayout == "Top", otherwise empty drag region)
            var topTabsScroll = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(14, 0, 14, 0),
                Background = Brushes.Transparent
            };
            _topTabsStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            topTabsScroll.Content = _topTabsStack;
            Grid.SetColumn(topTabsScroll, 1);
            topBarGrid.Children.Add(topTabsScroll);

            // Top-right: Bookmark ribbon door (12px gap before window buttons) + Inline Window Caption Buttons (— □ ✕)
            var topRightBtns = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };
            var bmDoor = CreateVectorIconButton(VectorIcons.BookmarkPath, "Bookmark this page (Ctrl+Shift+B)", () => ToggleBookmarkCurrentPage());
            bmDoor.Margin = new Thickness(0, 0, 12, 0);
            topRightBtns.Children.Add(bmDoor);

            topRightBtns.Children.Add(CreateVectorIconButton(VectorIcons.WinMinimizePath, "Minimize", () => WindowState = WindowState.Minimized));
            topRightBtns.Children.Add(CreateVectorIconButton(VectorIcons.WinMaximizePath, "Maximize / Restore", () =>
                WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized));
            topRightBtns.Children.Add(CreateVectorIconButton(VectorIcons.WinClosePath, "Close", () => Close()));
            Grid.SetColumn(topRightBtns, 2);
            topBarGrid.Children.Add(topRightBtns);

            _topTabBarBorder.Child = topBarGrid;
            _topTabBarBorder.MouseLeftButtonDown += (s, e) =>
            {
                if (e.ClickCount == 2)
                    WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
                else
                    try { DragMove(); } catch { }
            };
            Grid.SetRow(_topTabBarBorder, 0);
            Grid.SetColumnSpan(_topTabBarBorder, 2);
            _rootGrid.Children.Add(_topTabBarBorder);

            // 2. ULTRA-CLEAN LEFT SIDEBAR (Search's 232px column: Pinned squares, Tab rows, and "+ New tab" right below the last tab — zero footer clutter!)
            _sidebarBorder = new Border
            {
                Width = 232,
                Background = Palette.Frame,
                Padding = new Thickness(10, 0, 2, 10)
            };

            var sidebarDock = new DockPanel { LastChildFill = true };

            // Hidden legacy references so existing helpers never null-ref
            _spaceTitleText = new TextBlock();
            _spaceDotsPanel = new StackPanel();

            // Pinned tabs grid (compact squares at top of column)
            _pinsPanel = new WrapPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 0, 4)
            };
            DockPanel.SetDock(_pinsPanel, Dock.Top);
            sidebarDock.Children.Add(_pinsPanel);

            // Bottom-left Space switcher button (Oriel bottom-left home/space icon)
            var footerRow = new DockPanel { Margin = new Thickness(0, 4, 0, 2) };
            _spaceFooterBtn = new Border
            {
                Width = 28,
                Height = 28,
                CornerRadius = new CornerRadius(7),
                Background = Brushes.Transparent,
                Cursor = Cursors.Hand,
                ToolTip = "Spaces (Alt+1..9)",
                Child = VectorIcons.Draw(VectorIcons.HomePath, Palette.Muted, 14, 1.5)
            };
            _spaceFooterBtn.MouseEnter += (s, e) => _spaceFooterBtn.Background = Palette.Hover;
            _spaceFooterBtn.MouseLeave += (s, e) => _spaceFooterBtn.Background = Brushes.Transparent;
            _spaceFooterBtn.MouseLeftButtonUp += (s, e) => ShowSpacesMenu(_spaceFooterBtn);
            DockPanel.SetDock(_spaceFooterBtn, Dock.Left);
            footerRow.Children.Add(_spaceFooterBtn);
            DockPanel.SetDock(footerRow, Dock.Bottom);
            sidebarDock.Children.Add(footerRow);

            // Scrollable loose & grouped tabs list with Oriel's smooth gliding selection pill
            var tabsScroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Background = Brushes.Transparent
            };
            tabsScroll.MouseLeftButtonDown += (s, e) =>
            {
                if (e.OriginalSource == tabsScroll || e.OriginalSource == _tabsStack || e.OriginalSource == _tabsContainerGrid)
                {
                    try { DragMove(); } catch { }
                }
            };
            _tabsContainerGrid = new Grid { Background = Brushes.Transparent };
            _glidingPillTransform = new TranslateTransform(0, 0);
            _glidingTabPill = new Border
            {
                Height = 28,
                CornerRadius = new CornerRadius(9),
                Background = Palette.Wash,
                VerticalAlignment = VerticalAlignment.Top,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                IsHitTestVisible = false,
                Visibility = Visibility.Collapsed,
                RenderTransform = _glidingPillTransform
            };
            _tabsStack = new StackPanel { Orientation = Orientation.Vertical, Background = Brushes.Transparent };
            _tabsContainerGrid.Children.Add(_glidingTabPill);
            _tabsContainerGrid.Children.Add(_tabsStack);
            tabsScroll.Content = _tabsContainerGrid;
            sidebarDock.Children.Add(tabsScroll);

            _sidebarBorder.Child = sidebarDock;
            Grid.SetRow(_sidebarBorder, 1);
            Grid.SetColumn(_sidebarBorder, 0);
            _rootGrid.Children.Add(_sidebarBorder);

            // 3. MAIN PAGE STAGE (CardEdge: 10px rounded corners, 8px inset from right/bottom, 0px top since 52px band is above it)
            var stageOuterGrid = new Grid();

            _pageCardBorder = new Border
            {
                Background = Palette.Ground,
                BorderBrush = Palette.Hairline,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Margin = new Thickness(6, 0, 8, 8),
                ClipToBounds = true,
                AllowDrop = true
            };
            _pageCardBorder.DragOver += OnStageDragOver;
            _pageCardBorder.Drop += OnStageDrop;

            var pageInnerDock = new DockPanel { LastChildFill = true };

            // Curtain / Veil active banner at top of page
            _veilBanner = new Border
            {
                Background = Palette.Ink,
                Padding = new Thickness(14, 7, 14, 7),
                Visibility = Visibility.Collapsed
            };
            var veilBannerDock = new DockPanel();
            var veilDoneBtn = CreateQuietButton("Done (Esc)", "Exit element hiding mode", () => StopVeilPicking());
            DockPanel.SetDock(veilDoneBtn, Dock.Right);
            veilBannerDock.Children.Add(veilDoneBtn);
            _veilBannerText = new TextBlock
            {
                Text = "Point and click any banner, overlay, or clutter to hide it permanently on this site · ↑/↓ expand/narrow · Esc to finish",
                Foreground = Palette.Ground,
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center
            };
            veilBannerDock.Children.Add(_veilBannerText);
            _veilBanner.Child = veilBannerDock;
            DockPanel.SetDock(_veilBanner, Dock.Top);
            pageInnerDock.Children.Add(_veilBanner);

            // Password save offer banner (Vault)
            _passwordOfferBanner = new Border
            {
                Background = Palette.Wash,
                BorderBrush = Palette.Hairline,
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(14, 7, 14, 7),
                Visibility = Visibility.Collapsed
            };
            var pwDock = new DockPanel();
            var pwBtns = new StackPanel { Orientation = Orientation.Horizontal };
            pwBtns.Children.Add(CreateQuietButton("Save in Vault", "Encrypt and save password locally", () =>
            {
                Store.SavePassword(_pendingSaveHost, _pendingSaveUser, _pendingSavePass);
                _passwordOfferBanner.Visibility = Visibility.Collapsed;
            }));
            pwBtns.Children.Add(CreateQuietButton("Not Now", "Dismiss", () =>
            {
                _passwordOfferBanner.Visibility = Visibility.Collapsed;
            }));
            DockPanel.SetDock(pwBtns, Dock.Right);
            pwDock.Children.Add(pwBtns);
            _passwordOfferText = new TextBlock
            {
                FontSize = 12,
                Foreground = Palette.Ink,
                VerticalAlignment = VerticalAlignment.Center
            };
            pwDock.Children.Add(_passwordOfferText);
            _passwordOfferBanner.Child = pwDock;
            DockPanel.SetDock(_passwordOfferBanner, Dock.Top);
            pageInnerDock.Children.Add(_passwordOfferBanner);

            // Stage Grid supporting single page or Split View side-by-side
            _stageGrid = new Grid { Background = Palette.Ground };
            _stageGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            _stageGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            _stageGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0, GridUnitType.Pixel) });

            _leftPaneHost = new Border { Background = Palette.Ground };
            _leftPaneHost.PreviewMouseDown += (s, e) =>
            {
                if (_isSplitView && _splitLeftTab != null && _activeTab != _splitLeftTab)
                {
                    _activeTab = _splitLeftTab;
                    UpdateSplitFocusBorders();
                    RefreshTabRows();
                }
            };
            Grid.SetColumn(_leftPaneHost, 0);
            _stageGrid.Children.Add(_leftPaneHost);

            _splitDivider = new GridSplitter
            {
                Width = 5,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Stretch,
                Background = Palette.Hairline,
                ShowsPreview = false,
                Visibility = Visibility.Collapsed
            };
            Grid.SetColumn(_splitDivider, 1);
            _stageGrid.Children.Add(_splitDivider);

            _rightPaneHost = new Border { Background = Palette.Ground, Visibility = Visibility.Collapsed };
            _rightPaneHost.PreviewMouseDown += (s, e) =>
            {
                if (_isSplitView && _splitRightTab != null && _activeTab != _splitRightTab)
                {
                    _activeTab = _splitRightTab;
                    UpdateSplitFocusBorders();
                    RefreshTabRows();
                }
            };
            Grid.SetColumn(_rightPaneHost, 2);
            _stageGrid.Children.Add(_rightPaneHost);

            pageInnerDock.Children.Add(_stageGrid);
            _pageCardBorder.Child = pageInnerDock;
            stageOuterGrid.Children.Add(_pageCardBorder);

            // 4. FOLDED SIDEBAR LEFT-EDGE HOVER PEEK (from Search's Fold.swift)
            BuildSidebarHoverPeek(stageOuterGrid);

            // 5. FLOATING FIND BAR (Ctrl+F)
            BuildFindBar(stageOuterGrid);

            // 6. LINK PEEK OVERLAY CARD (Shift+Click on any link or external click from a pinned tab — Peek.swift)
            BuildPeekOverlay(stageOuterGrid);

            // 7. EXTENSION POPUP OVERLAY CARD
            BuildExtensionPopupOverlay(stageOuterGrid);

            // 8. FLOATING OMNIBOX & TAB SWITCHER (Ctrl+L / Ctrl+K — Search's Omnibox.swift)
            BuildOmniboxOverlay(stageOuterGrid);

            // 9. MODAL DRAWER (History, Bookmarks, Vault, Curtain, SiteStyles, Extensions, Importer, Downloads, Settings)
            BuildDrawerOverlay(stageOuterGrid);

            Grid.SetRow(stageOuterGrid, 1);
            Grid.SetColumn(stageOuterGrid, 1);
            _rootGrid.Children.Add(stageOuterGrid);

            Content = _rootGrid;
            ApplyLayoutPreferences();
        }

        private void BuildSidebarHoverPeek(Grid hostGrid)
        {
            // 6px invisible trigger along the left edge when sidebar is collapsed (Ctrl+S)
            _sidebarHoverHotzone = new Border
            {
                Width = 7,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Stretch,
                Background = Brushes.Transparent,
                Visibility = Visibility.Collapsed
            };
            _sidebarHoverHotzone.MouseEnter += (s, e) =>
            {
                if (Store.Prefs.SidebarCollapsed && Store.Prefs.TabLayout != "Top")
                {
                    Store.Prefs.SidebarCollapsed = false;
                    ApplyLayoutPreferences();
                }
            };
            hostGrid.Children.Add(_sidebarHoverHotzone);
        }

        private void BuildPeekOverlay(Grid hostGrid)
        {
            _peekOverlay = new Grid
            {
                Background = Palette.OverlayScrim,
                Visibility = Visibility.Collapsed
            };
            _peekOverlay.MouseLeftButtonDown += (s, e) =>
            {
                if (e.OriginalSource == _peekOverlay)
                    ClosePeekCard();
            };

            _peekCard = new Border
            {
                Width = 860,
                Height = 620,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Background = Palette.CardSurface,
                BorderBrush = Palette.Hairline,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                ClipToBounds = true,
                Effect = new DropShadowEffect { BlurRadius = 36, ShadowDepth = 12, Opacity = 0.28 }
            };

            var dock = new DockPanel();
            var topBar = new Border
            {
                Background = Palette.Wash,
                BorderBrush = Palette.Hairline,
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(14, 8, 14, 8)
            };
            var topDock = new DockPanel();
            var actions = new StackPanel { Orientation = Orientation.Horizontal };
            actions.Children.Add(CreatePillActionButton("◰ Expand to Full Tab", async () =>
            {
                if (_peekTab != null)
                {
                    var keep = _peekTab;
                    _peekTab = null;
                    _peekWebHost.Child = null;
                    _peekOverlay.Visibility = Visibility.Collapsed;
                    _allTabs.Add(keep);
                    await SelectTabAsync(keep);
                    RefreshTabRows();
                }
            }));
            actions.Children.Add(CreateQuietButton("✕ Close Peek (Esc)", "Close preview", () => ClosePeekCard()));
            DockPanel.SetDock(actions, Dock.Right);
            topDock.Children.Add(actions);

            _peekTitleText = new TextBlock
            {
                Text = "Link Peek",
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = Palette.Ink,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            topDock.Children.Add(_peekTitleText);
            topBar.Child = topDock;
            DockPanel.SetDock(topBar, Dock.Top);
            dock.Children.Add(topBar);

            _peekWebHost = new Border { Background = Palette.Ground };
            dock.Children.Add(_peekWebHost);
            _peekCard.Child = dock;
            _peekOverlay.Children.Add(_peekCard);
            hostGrid.Children.Add(_peekOverlay);
        }

        private async void OpenPeekCard(string url, bool isPrivate)
        {
            ClosePeekCard();
            _peekTab = new BrowserTab(url, Address.DisplayHost(url), false, isPrivate, Store.Prefs.ActiveSpaceId);
            _peekTab.StateChanged += t =>
            {
                Dispatcher.InvokeAsync(() =>
                {
                    if (_peekTab == t)
                        _peekTitleText.Text = "👁 Peek: " + t.DisplayTitle + "  (" + Address.DisplayHost(t.Url) + ")";
                });
            };
            _peekOverlay.Visibility = Visibility.Visible;
            await _peekTab.EnsureAwakeAsync(_sharedEnv, _peekWebHost);
            _peekWebHost.Child = _peekTab.WebView;
        }

        private void ClosePeekCard()
        {
            _peekOverlay.Visibility = Visibility.Collapsed;
            _peekWebHost.Child = null;
            if (_peekTab != null)
            {
                try { if (_peekTab.WebView != null) _peekTab.WebView.Dispose(); } catch { }
                _peekTab = null;
            }
        }

        private void BuildExtensionPopupOverlay(Grid hostGrid)
        {
            _extPopupOverlay = new Grid
            {
                Background = Palette.OverlayScrim,
                Visibility = Visibility.Collapsed
            };
            _extPopupOverlay.MouseLeftButtonDown += (s, e) =>
            {
                if (e.OriginalSource == _extPopupOverlay)
                    CloseExtensionPopup();
            };

            var card = new Border
            {
                Width = 420,
                Height = 520,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Background = Palette.CardSurface,
                BorderBrush = Palette.Hairline,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                ClipToBounds = true,
                Effect = new DropShadowEffect { BlurRadius = 28, ShadowDepth = 8, Opacity = 0.25 }
            };

            var dock = new DockPanel();
            var hdr = new Border
            {
                Background = Palette.Wash,
                Padding = new Thickness(12, 8, 12, 8)
            };
            var hdrDock = new DockPanel();
            var closeBtn = CreateQuietButton("✕ Close", "Close popup", () => CloseExtensionPopup());
            DockPanel.SetDock(closeBtn, Dock.Right);
            hdrDock.Children.Add(closeBtn);
            _extPopupTitle = new TextBlock
            {
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = Palette.Ink,
                VerticalAlignment = VerticalAlignment.Center
            };
            hdrDock.Children.Add(_extPopupTitle);
            hdr.Child = hdrDock;
            DockPanel.SetDock(hdr, Dock.Top);
            dock.Children.Add(hdr);

            _extPopupHost = new Border { Background = Palette.Ground };
            dock.Children.Add(_extPopupHost);
            card.Child = dock;
            _extPopupOverlay.Children.Add(card);
            hostGrid.Children.Add(_extPopupOverlay);
        }

        private async void OpenExtensionPopup(ExtensionInfo ext)
        {
            CloseExtensionPopup();
            _drawerOverlay.Visibility = Visibility.Collapsed;
            _extPopupTitle.Text = "🧩 " + ext.Name;
            _extPopupOverlay.Visibility = Visibility.Visible;

            _extPopupWebView = new WebView2();
            _extPopupHost.Child = _extPopupWebView;
            await _extPopupWebView.EnsureCoreWebView2Async(_sharedEnv);

            string rel = string.IsNullOrEmpty(ext.PopupRelativePath) ? "popup.html" : ext.PopupRelativePath.TrimStart('/');
            string runId = string.IsNullOrEmpty(ext.RuntimeExtensionId) ? ext.Id : ext.RuntimeExtensionId;
            string localPopupFile = Path.Combine(ext.FolderPath, rel.Replace('/', '\\'));

            if (!string.IsNullOrEmpty(runId) && !string.IsNullOrEmpty(ext.PopupRelativePath))
            {
                _extPopupWebView.CoreWebView2.Navigate("chrome-extension://" + runId + "/" + rel);
            }
            else if (File.Exists(localPopupFile))
            {
                _extPopupWebView.CoreWebView2.Navigate(new Uri(localPopupFile).AbsoluteUri);
            }
            else
            {
                _extPopupWebView.CoreWebView2.NavigateToString(
                    "<html><body style='font:13px Segoe UI,sans-serif;padding:20px;background:#1c1c1c;color:#ededed'>" +
                    "<h3>" + ext.Name + " (v" + ext.Version + ")</h3>" +
                    "<p>This extension is active in the background and injects its content scripts / rules automatically.</p>" +
                    "<p style='color:#8c8c8c;font-size:11px'>Folder: " + ext.FolderPath + "</p></body></html>");
            }
        }

        private void CloseExtensionPopup()
        {
            _extPopupOverlay.Visibility = Visibility.Collapsed;
            _extPopupHost.Child = null;
            if (_extPopupWebView != null)
            {
                try { _extPopupWebView.Dispose(); } catch { }
                _extPopupWebView = null;
            }
        }

        // DRAG TAB ONTO PAGE LEFT/RIGHT EDGE TO SPLIT VIEW (from Search's TabDrag.swift & SplitView.swift)
        private void OnStageDragOver(object sender, DragEventArgs e)
        {
            if (_draggedTab != null && _draggedTab != _activeTab)
            {
                e.Effects = DragDropEffects.Move;
                e.Handled = true;
            }
        }

        private async void OnStageDrop(object sender, DragEventArgs e)
        {
            if (_draggedTab == null || _draggedTab == _activeTab) return;
            var dropped = _draggedTab;
            _draggedTab = null;

            Point pt = e.GetPosition(_pageCardBorder);
            bool dropOnLeft = pt.X < (_pageCardBorder.ActualWidth / 2.0);

            await dropped.EnsureAwakeAsync(_sharedEnv, dropOnLeft ? _leftPaneHost : _rightPaneHost);
            _isSplitView = true;
            if (dropOnLeft)
            {
                _splitLeftTab = dropped;
                _splitRightTab = _activeTab;
            }
            else
            {
                _splitLeftTab = _activeTab;
                _splitRightTab = dropped;
            }

            _stageGrid.ColumnDefinitions[0].Width = new GridLength(1, GridUnitType.Star);
            _stageGrid.ColumnDefinitions[2].Width = new GridLength(1, GridUnitType.Star);
            _splitDivider.Visibility = Visibility.Visible;
            _rightPaneHost.Visibility = Visibility.Visible;
            _leftPaneHost.Child = _splitLeftTab.WebView;
            _rightPaneHost.Child = _splitRightTab.WebView;
            _activeTab = dropped;
            UpdateSplitFocusBorders();
            RefreshTabRows();
        }

        private void BuildFindBar(Grid hostGrid)
        {
            _findBar = new Border
            {
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 16, 24, 0),
                Background = Palette.CardSurface,
                BorderBrush = Palette.Hairline,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(10, 6, 10, 6),
                Visibility = Visibility.Collapsed,
                Effect = new DropShadowEffect { BlurRadius = 16, ShadowDepth = 4, Opacity = 0.18 }
            };
            var stack = new StackPanel { Orientation = Orientation.Horizontal };
            _findInput = new TextBox
            {
                Width = 180,
                Background = Brushes.Transparent,
                Foreground = Palette.Ink,
                CaretBrush = Palette.Ink,
                BorderThickness = new Thickness(0),
                FontSize = 13,
                VerticalContentAlignment = VerticalAlignment.Center
            };
            _findInput.KeyDown += async (s, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    await ExecuteFindOnPageAsync(_findInput.Text, !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift));
                    e.Handled = true;
                }
                else if (e.Key == Key.Escape)
                {
                    _findBar.Visibility = Visibility.Collapsed;
                    e.Handled = true;
                }
            };
            stack.Children.Add(_findInput);
            stack.Children.Add(CreateIconButton("↑", "Previous match", async () => await ExecuteFindOnPageAsync(_findInput.Text, false)));
            stack.Children.Add(CreateIconButton("↓", "Next match", async () => await ExecuteFindOnPageAsync(_findInput.Text, true)));
            stack.Children.Add(CreateIconButton("✕", "Close Find (Esc)", () => _findBar.Visibility = Visibility.Collapsed));
            _findBar.Child = stack;
            hostGrid.Children.Add(_findBar);
        }

        private async Task ExecuteFindOnPageAsync(string query, bool forward)
        {
            if (_activeTab == null || !_activeTab.IsWebViewInitialized || _activeTab.WebView == null) return;
            if (string.IsNullOrEmpty(query)) return;
            string escaped = query.Replace("\\", "\\\\").Replace("'", "\\'");
            string back = forward ? "false" : "true";
            await _activeTab.WebView.CoreWebView2.ExecuteScriptAsync("window.find('" + escaped + "', false, " + back + ", true, false, true, false);");
        }

        private void BuildOmniboxOverlay(Grid hostGrid)
        {
            _omniboxOverlay = new Grid
            {
                Background = Brushes.Transparent,
                Visibility = Visibility.Visible
            };
            _omniboxOverlay.MouseLeftButtonDown += (s, e) =>
            {
                if (e.OriginalSource == _omniboxOverlay)
                    HideOmnibox();
            };

            var centerContainer = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 60),
                Width = 560
            };

            _omniboxCard = new Border
            {
                Height = 50,
                Background = Palette.Ground,
                BorderBrush = Palette.Hairline,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(22, 0, 22, 0),
                Effect = new DropShadowEffect { BlurRadius = 40, ShadowDepth = 8, Opacity = 0.14 }
            };

            var inputDock = new DockPanel { VerticalAlignment = VerticalAlignment.Center };

            _siteChipBorder = new Border
            {
                Background = Palette.Wash,
                CornerRadius = new CornerRadius(7),
                Padding = new Thickness(8, 3, 8, 3),
                Margin = new Thickness(0, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Visibility = Visibility.Collapsed
            };
            _siteChipText = new TextBlock
            {
                FontSize = 12,
                FontWeight = FontWeights.Medium,
                Foreground = Palette.Ink
            };
            _siteChipBorder.Child = _siteChipText;
            DockPanel.SetDock(_siteChipBorder, Dock.Left);
            inputDock.Children.Add(_siteChipBorder);

            _omniboxInput = new TextBox
            {
                FontSize = 15.5,
                Background = Brushes.Transparent,
                Foreground = Palette.Ink,
                CaretBrush = Palette.Ink,
                BorderThickness = new Thickness(0),
                VerticalContentAlignment = VerticalAlignment.Center
            };
            _omniboxInput.TextChanged += (s, e) => RefreshOmniboxOffers();
            _omniboxInput.PreviewKeyDown += OnOmniboxPreviewKeyDown;
            inputDock.Children.Add(_omniboxInput);

            _omniboxCard.Child = inputDock;
            centerContainer.Children.Add(_omniboxCard);

            _omniboxOffersBorder = new Border
            {
                Background = Palette.CardSurface,
                BorderBrush = Palette.Hairline,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Margin = new Thickness(0, 8, 0, 0),
                Padding = new Thickness(6),
                Visibility = Visibility.Collapsed,
                Effect = new DropShadowEffect { BlurRadius = 24, ShadowDepth = 6, Opacity = 0.18 }
            };
            _omniboxOffersStack = new StackPanel { Orientation = Orientation.Vertical };
            _omniboxOffersBorder.Child = _omniboxOffersStack;
            centerContainer.Children.Add(_omniboxOffersBorder);

            _omniboxOverlay.Children.Add(centerContainer);
            _omniboxPopup = new System.Windows.Controls.Primitives.Popup
            {
                Child = _omniboxOverlay,
                PlacementTarget = _pageCardBorder,
                Placement = System.Windows.Controls.Primitives.PlacementMode.Center,
                AllowsTransparency = true,
                StaysOpen = false,
                PopupAnimation = System.Windows.Controls.Primitives.PopupAnimation.Fade
            };
            _omniboxPopup.Closed += (s, e) =>
            {
                if (_activeTab != null && _activeTab.WebView != null)
                    _activeTab.WebView.Focus();
            };
        }

        private void BuildDrawerOverlay(Grid hostGrid)
        {
            _drawerOverlay = new Grid
            {
                Background = Palette.OverlayScrim,
                Visibility = Visibility.Collapsed
            };
            _drawerOverlay.MouseLeftButtonDown += (s, e) =>
            {
                if (e.OriginalSource == _drawerOverlay)
                    _drawerOverlay.Visibility = Visibility.Collapsed;
            };

            _drawerCard = new Border
            {
                Width = 640,
                MaxHeight = 650,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Background = Palette.CardSurface,
                BorderBrush = Palette.Hairline,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(20),
                Effect = new DropShadowEffect { BlurRadius = 30, ShadowDepth = 10, Opacity = 0.25 }
            };

            var dock = new DockPanel();
            var headerDock = new DockPanel { Margin = new Thickness(0, 0, 0, 14) };
            var closeBtn = CreateQuietButton("Close (Esc)", "Close panel", () => _drawerOverlay.Visibility = Visibility.Collapsed);
            DockPanel.SetDock(closeBtn, Dock.Right);
            headerDock.Children.Add(closeBtn);

            _drawerTitle = new TextBlock
            {
                FontSize = 18,
                FontWeight = FontWeights.SemiBold,
                Foreground = Palette.Ink,
                VerticalAlignment = VerticalAlignment.Center
            };
            headerDock.Children.Add(_drawerTitle);
            DockPanel.SetDock(headerDock, Dock.Top);
            dock.Children.Add(headerDock);

            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };
            _drawerContent = new StackPanel { Orientation = Orientation.Vertical };
            scroll.Content = _drawerContent;
            dock.Children.Add(scroll);

            _drawerCard.Child = dock;
            _drawerOverlay.Children.Add(_drawerCard);
            hostGrid.Children.Add(_drawerOverlay);
        }

        private async void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                var options = new CoreWebView2EnvironmentOptions
                {
                    AdditionalBrowserArguments = "--disable-features=msSmartScreenProtection --enable-smooth-scrolling"
                };
                try
                {
                    var prop = options.GetType().GetProperty("AreBrowserExtensionsEnabled");
                    if (prop != null) prop.SetValue(options, true, null);
                }
                catch { }

                _sharedEnv = await CoreWebView2Environment.CreateAsync(null, Store.WebViewDataDir, options);
            }
            catch (Exception ex)
            {
                MessageBox.Show("WebView2 failed to initialize: " + ex.Message, "Search Browser", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (Store.SessionTabs != null && Store.SessionTabs.Count > 0)
            {
                foreach (var st in Store.SessionTabs)
                {
                    var tab = new BrowserTab(st.Url, st.Title, st.Pinned, false, string.IsNullOrEmpty(st.SpaceId) ? "default" : st.SpaceId, st.GroupId)
                    {
                        Id = string.IsNullOrEmpty(st.Id) ? Guid.NewGuid().ToString("N") : st.Id,
                        KeepAwake = st.KeepAwake,
                        FaviconUrl = st.FaviconUrl
                    };
                    AttachTabEvents(tab);
                    _allTabs.Add(tab);
                }
            }

            var currentSpaceTabs = GetTabsForActiveSpace();
            if (currentSpaceTabs.Count == 0)
            {
                var welcomeTab = new BrowserTab("about:blank", "New Tab", false, false, Store.Prefs.ActiveSpaceId);
                AttachTabEvents(welcomeTab);
                _allTabs.Add(welcomeTab);
                currentSpaceTabs = GetTabsForActiveSpace();
            }

            var toSelect = currentSpaceTabs.FirstOrDefault(t => t.Id == Store.Prefs.ActiveTabId) ?? currentSpaceTabs[0];
            await SelectTabAsync(toSelect);
            RefreshSpaceHeader();
            RefreshTabRows();
        }

        private void OnWindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            BenchServer.Stop();
            SaveCurrentSession();
        }

        private void SaveCurrentSession()
        {
            var list = _allTabs.Where(t => !t.IsPrivate).Select(t => new SavedTabState
            {
                Id = t.Id,
                Title = t.Title,
                Url = t.Url,
                FaviconUrl = t.FaviconUrl,
                Pinned = t.Pinned,
                KeepAwake = t.KeepAwake,
                SpaceId = t.SpaceId,
                GroupId = t.GroupId
            }).ToList();
            Store.SaveSession(list);
            FolderSync.PushToSyncFolder();
            if (_activeTab != null && !_activeTab.IsPrivate)
            {
                Store.Prefs.ActiveTabId = _activeTab.Id;
                Store.SavePrefs();
            }
        }

        private List<BrowserTab> GetTabsForActiveSpace()
        {
            string sp = Store.Prefs.ActiveSpaceId ?? "default";
            return _allTabs.Where(t => string.Equals(t.SpaceId, sp, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        private void AttachTabEvents(BrowserTab tab)
        {
            tab.StateChanged += t =>
            {
                Dispatcher.InvokeAsync(() =>
                {
                    RefreshTabRows();
                    if (t == _activeTab)
                    {
                        Title = t.DisplayTitle + " — Search";
                    }
                    SaveCurrentSession();
                });
            };

            tab.NewTabRequested += (opener, url) =>
            {
                Dispatcher.InvokeAsync(() => CreateAndSelectTab(url, false, opener.IsPrivate));
            };

            tab.PeekRequested += (opener, url) =>
            {
                Dispatcher.InvokeAsync(() => OpenPeekCard(url, opener.IsPrivate));
            };

            tab.WebMessageReceived += (t, json) =>
            {
                Dispatcher.InvokeAsync(async () => await HandleTabWebMessageAsync(t, json));
            };
        }

        private async Task HandleTabWebMessageAsync(BrowserTab tab, string json)
        {
            string type = ExtractJsonValue(json, "type");
            if (type == "veilPicked")
            {
                string selector = ExtractJsonValue(json, "selector");
                string label = ExtractJsonValue(json, "label");
                string note = ExtractJsonValue(json, "note");
                string host = Address.DisplayHost(tab.Url);
                if (!string.IsNullOrEmpty(host) && !string.IsNullOrEmpty(selector))
                {
                    PageFeatures.AddVeil(host, selector, label, note);
                    await tab.ApplySiteStyleSheetsAsync();
                    _veilBannerText.Text = "Hid \"" + label + "\" on " + host + " · Click another element or press Esc when done (Ctrl+Z to undo)";
                }
            }
            else if (type == "veilOff")
            {
                tab.IsVeilPicking = false;
                _veilBanner.Visibility = Visibility.Collapsed;
            }
            else if (type == "peekLink")
            {
                if (!Store.Prefs.PeekEnabled) return;
                string url = ExtractJsonValue(json, "url");
                if (!string.IsNullOrEmpty(url))
                {
                    OpenPeekCard(url, tab.IsPrivate);
                }
            }
            else if (type == "cwsInstall")
            {
                string url = ExtractJsonValue(json, "url");
                if (!string.IsNullOrEmpty(url))
                {
                    try
                    {
                        var info = await CrxInstaller.InstallFromWebStoreAsync(url);
                        if (tab.IsWebViewInitialized && tab.WebView != null)
                        {
                            var method = tab.WebView.CoreWebView2.Profile.GetType().GetMethod("AddBrowserExtensionAsync");
                            if (method != null)
                            {
                                dynamic task = method.Invoke(tab.WebView.CoreWebView2.Profile, new object[] { info.FolderPath });
                                if (task != null) await task;
                            }
                        }
                        ShowExtensionsDrawer();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Could not install Chrome extension: " + ex.Message, "Search Extensions", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
            }
            else if (type == "vaultFillUser")
            {
                string user = ExtractJsonValue(json, "user");
                string host = Address.DisplayHost(tab.Url);
                var login = Store.GetLoginsForHost(host).FirstOrDefault(l => string.Equals(l.User, user, StringComparison.OrdinalIgnoreCase));
                if (login != null)
                {
                    await FillSavedLoginAsync(login);
                }
            }
            else if (type == "loginSubmitted")
            {
                if (tab.IsPrivate) return;
                string host = ExtractJsonValue(json, "host");
                string user = ExtractJsonValue(json, "user");
                string pass = ExtractJsonValue(json, "pass");
                if (!string.IsNullOrEmpty(host) && !string.IsNullOrEmpty(user) && !string.IsNullOrEmpty(pass))
                {
                    _pendingSaveHost = Store.NormalizeHost(host);
                    _pendingSaveUser = user;
                    _pendingSavePass = pass;
                    _passwordOfferText.Text = "Save password for " + user + " on " + _pendingSaveHost + " in Windows encrypted Vault?";
                    _passwordOfferBanner.Visibility = Visibility.Visible;
                }
            }
            else if (type == "shortcut")
            {
                string key = ExtractJsonValue(json, "key");
                bool shift = json.Contains("\"shift\":true");
                bool ctrl = !json.Contains("\"ctrl\":false");
                bool alt = json.Contains("\"alt\":true");
                if (key == "escape")
                {
                    if (_omniboxPopup != null && _omniboxPopup.IsOpen) HideOmnibox();
                    else if (_peekOverlay.Visibility == Visibility.Visible) ClosePeekCard();
                    else if (_extPopupOverlay.Visibility == Visibility.Visible) CloseExtensionPopup();
                    else if (_drawerOverlay.Visibility == Visibility.Visible) _drawerOverlay.Visibility = Visibility.Collapsed;
                    else if (_findBar.Visibility == Visibility.Visible) _findBar.Visibility = Visibility.Collapsed;
                    else if (_activeTab != null && _activeTab.IsVeilPicking) StopVeilPicking();
                    return;
                }
                if (key == "f5")
                {
                    if (_activeTab != null && _activeTab.IsWebViewInitialized && _activeTab.WebView != null)
                    {
                        if (ctrl || shift) await _activeTab.WebView.CoreWebView2.ExecuteScriptAsync("location.reload(true)");
                        else _activeTab.WebView.CoreWebView2.Reload();
                    }
                    return;
                }
                if (key == "f6" || (alt && key == "d"))
                {
                    ShowOmnibox(false, false);
                    return;
                }
                if (key == "f3")
                {
                    _findBar.Visibility = Visibility.Visible;
                    _findInput.Focus();
                    return;
                }
                if (alt && !ctrl && !shift && key.Length == 1 && key[0] >= '1' && key[0] <= '9')
                {
                    int spIdx = key[0] - '1';
                    if (spIdx < Store.Spaces.Count) CycleSpace(spIdx);
                    return;
                }
                if (alt && !ctrl && !shift && (key == "arrowleft" || key == "arrowright"))
                {
                    if (_activeTab != null && _activeTab.WebView != null)
                    {
                        if (key == "arrowleft" && _activeTab.WebView.CanGoBack) _activeTab.WebView.GoBack();
                        else if (key == "arrowright" && _activeTab.WebView.CanGoForward) _activeTab.WebView.GoForward();
                    }
                    return;
                }
                if ((alt && shift && key == "n") || (ctrl && alt && key == "n"))
                {
                    ToggleSplitView();
                    return;
                }
                if ((alt && shift && key == "l") || (ctrl && alt && key == "l"))
                {
                    OpenOrSelectSettingsTab("passwords");
                    return;
                }
                if (ctrl && !alt && !shift && key.Length == 1 && key[0] >= '1' && key[0] <= '9')
                {
                    int idx = key[0] - '1';
                    if (Store.Prefs.CtrlNumbersSwitchSpaces && idx < Store.Spaces.Count)
                    {
                        CycleSpace(idx);
                        return;
                    }
                    var spaceTabs = GetTabsForActiveSpace();
                    if (idx == 8 && spaceTabs.Count > 0) idx = spaceTabs.Count - 1;
                    if (idx < spaceTabs.Count) await SelectTabAsync(spaceTabs[idx]);
                    return;
                }
                await HandleShortcutKeyAsync(key, shift);
            }
            else if (type == "navigate")
            {
                string text = ExtractJsonValue(json, "text");
                if (!string.IsNullOrWhiteSpace(text))
                    tab.Navigate(text);
            }
            else if (type == "openSettings")
            {
                OpenOrSelectSettingsTab("general");
            }
            else if (type == "settingsAction")
            {
                string action = ExtractJsonValue(json, "action");
                string arg = ExtractJsonValue(json, "arg");
                string sec = ExtractJsonValue(json, "sec");
                await HandleSettingsTabActionAsync(tab, action, arg, string.IsNullOrEmpty(sec) ? "general" : sec);
            }
        }

        private async Task HandleSettingsTabActionAsync(BrowserTab settingsTab, string action, string arg, string sec = "general")
        {
            switch (action)
            {
                case "togglePref":
                    {
                        var prop = typeof(AppPreferences).GetProperty(arg ?? "");
                        if (prop != null && prop.PropertyType == typeof(bool))
                        {
                            bool cur = (bool)prop.GetValue(Store.Prefs, null);
                            prop.SetValue(Store.Prefs, !cur, null);
                            if (arg == "BenchServerEnabled")
                            {
                                if (!cur) BenchServer.Start(this); else BenchServer.Stop();
                            }
                            else if (arg == "PeekEnabled")
                            {
                                Store.Prefs.PinsOpenExternalInPeek = !cur;
                            }
                            Store.SavePrefs();
                            ApplyLayoutPreferences();
                            settingsTab.RenderSettingsTab(sec);
                        }
                    }
                    break;
                case "setPref":
                    {
                        int eq = (arg ?? "").IndexOf('=');
                        if (eq > 0)
                        {
                            string pName = arg.Substring(0, eq);
                            string pVal = arg.Substring(eq + 1);
                            var prop = typeof(AppPreferences).GetProperty(pName);
                            if (prop != null)
                            {
                                if (prop.PropertyType == typeof(bool))
                                    prop.SetValue(Store.Prefs, string.Equals(pVal, "true", StringComparison.OrdinalIgnoreCase), null);
                                else if (prop.PropertyType == typeof(string))
                                    prop.SetValue(Store.Prefs, pVal, null);

                                if (pName == "SearchEngineName")
                                {
                                    if (pVal == "Google") Store.Prefs.SearchEngineUrl = "https://www.google.com/search?q=";
                                    else if (pVal == "Brave Search") Store.Prefs.SearchEngineUrl = "https://search.brave.com/search?q=";
                                    else Store.Prefs.SearchEngineUrl = "https://duckduckgo.com/?q=";
                                }
                                else if (pName == "SleepLevel")
                                {
                                    Store.Prefs.AutoSleepMinutes = pVal == "Never" ? 99999 : pVal == "Later" ? 120 : pVal == "Sooner" ? 10 : 30;
                                }
                                else if (pName == "PeekTrigger")
                                {
                                    Store.Prefs.PeekEnabled = pVal != "Off";
                                }
                                Store.SavePrefs();
                                ApplyLayoutPreferences();
                                settingsTab.RenderSettingsTab(sec);
                            }
                        }
                    }
                    break;
                case "chooseDownloadsFolder":
                    using (var fbd = new System.Windows.Forms.FolderBrowserDialog())
                    {
                        fbd.Description = "Choose default Downloads folder:";
                        if (fbd.ShowDialog() == System.Windows.Forms.DialogResult.OK && !string.IsNullOrWhiteSpace(fbd.SelectedPath))
                        {
                            Store.Prefs.DownloadsFolder = fbd.SelectedPath;
                            Store.SavePrefs();
                            settingsTab.RenderSettingsTab("general");
                        }
                    }
                    break;
                case "chooseBgImage":
                    using (var ofd = new System.Windows.Forms.OpenFileDialog())
                    {
                        ofd.Filter = "Images|*.png;*.jpg;*.jpeg;*.webp;*.bmp";
                        if (ofd.ShowDialog() == System.Windows.Forms.DialogResult.OK && !string.IsNullOrWhiteSpace(ofd.FileName))
                        {
                            Store.Prefs.BlankWallMode = "Image";
                            Store.Prefs.BlankWallCustomValue = ofd.FileName;
                            Store.SavePrefs();
                            ApplyLayoutPreferences();
                            settingsTab.RenderSettingsTab("themes");
                        }
                    }
                    break;
                case "loadUnpackedExt":
                    using (var fbd = new System.Windows.Forms.FolderBrowserDialog())
                    {
                        fbd.Description = "Select unpacked extension folder containing manifest.json:";
                        if (fbd.ShowDialog() == System.Windows.Forms.DialogResult.OK && !string.IsNullOrWhiteSpace(fbd.SelectedPath))
                        {
                            try
                            {
                                var info = CrxInstaller.RegisterUnpackedFolder(fbd.SelectedPath);
                                if (settingsTab.WebView != null && settingsTab.IsWebViewInitialized)
                                {
                                    var method = settingsTab.WebView.CoreWebView2.Profile.GetType().GetMethod("AddBrowserExtensionAsync");
                                    if (method != null)
                                    {
                                        dynamic task = method.Invoke(settingsTab.WebView.CoreWebView2.Profile, new object[] { info.FolderPath });
                                        if (task != null) await task;
                                    }
                                }
                                settingsTab.RenderSettingsTab("extensions");
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show("Could not load unpacked extension: " + ex.Message, "Search Extensions", MessageBoxButton.OK, MessageBoxImage.Warning);
                            }
                        }
                    }
                    break;
                case "clearCookies":
                    if (settingsTab.WebView != null && settingsTab.IsWebViewInitialized)
                    {
                        await settingsTab.WebView.CoreWebView2.Profile.ClearBrowsingDataAsync(CoreWebView2BrowsingDataKinds.Cookies);
                        MessageBox.Show("Signed out of all sites (cookies cleared).", "Search Privacy", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    break;
                case "clearCache":
                    if (settingsTab.WebView != null && settingsTab.IsWebViewInitialized)
                    {
                        await settingsTab.WebView.CoreWebView2.Profile.ClearBrowsingDataAsync(CoreWebView2BrowsingDataKinds.DiskCache);
                        MessageBox.Show("Browser cache cleared.", "Search Privacy", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    break;
                case "forgetPermissions":
                    if (settingsTab.WebView != null && settingsTab.IsWebViewInitialized)
                    {
                        await settingsTab.WebView.CoreWebView2.Profile.ClearBrowsingDataAsync(CoreWebView2BrowsingDataKinds.Settings);
                        MessageBox.Show("Site permission choices reset.", "Search Privacy", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    break;
                case "zoomChange":
                    int delta = arg == "+10" ? 10 : -10;
                    Store.Prefs.DefaultZoomPercent = Math.Max(50, Math.Min(200, Store.Prefs.DefaultZoomPercent + delta));
                    Store.SavePrefs();
                    settingsTab.RenderSettingsTab("browsing");
                    break;
                case "setupLikeArc":
                    Store.Prefs.TabLayout = "Left";
                    Store.Prefs.SpacesEnabled = true;
                    Store.Prefs.PinnedRows = true;
                    Store.Prefs.NewTabInFloatingField = true;
                    Store.Prefs.PageAsCard = true;
                    Store.Prefs.CloseIdleTabs = "After 12 hours";
                    Store.SavePrefs();
                    ApplyLayoutPreferences();
                    RefreshTabRows();
                    settingsTab.RenderSettingsTab("spaces");
                    MessageBox.Show("Search is now set up like Arc:\n• Left tab column with Spaces & Pinned rows\n• Ctrl+T opens the floating address field\n• Page shown as a rounded card", "Set Up Like Arc", MessageBoxButton.OK, MessageBoxImage.Information);
                    break;
                case "openVaultDrawer":
                    ShowVaultDrawer();
                    break;
                case "newProfile":
                    CycleSpace((Store.Spaces.FindIndex(s => s.Id == Store.Prefs.ActiveSpaceId) + 1) % Math.Max(1, Store.Spaces.Count));
                    settingsTab.RenderSettingsTab("spaces");
                    break;
                case "manageSiteShortcuts":
                    MessageBox.Show("Type a site prefix in the address bar like: yt cats, g search, w wiki, gh repo.", "Site Shortcuts", MessageBoxButton.OK, MessageBoxImage.Information);
                    break;
                case "checkForUpdates":
                    await CheckForUpdatesAsync();
                    break;
                case "makeDefault":
                    DefaultBrowserRegistrar.OpenWindowsDefaultAppsSettings();
                    settingsTab.RenderSettingsTab("general");
                    break;
                case "importBrowsers":
                    ShowImportDrawer();
                    break;
                case "chooseSyncFolder":
                    using (var fbd = new System.Windows.Forms.FolderBrowserDialog())
                    {
                        fbd.Description = "Choose a shared folder (OneDrive, Dropbox, or iCloud) for Search Sync:";
                        if (fbd.ShowDialog() == System.Windows.Forms.DialogResult.OK && !string.IsNullOrWhiteSpace(fbd.SelectedPath))
                        {
                            Store.Prefs.SyncFolderPath = fbd.SelectedPath;
                            Store.SavePrefs();
                            FolderSync.InitializeWatcher(() => Dispatcher.InvokeAsync(() => RefreshTabRows()));
                            settingsTab.RenderSettingsTab("general");
                        }
                    }
                    break;
                case "openDownloadsFolder":
                    try
                    {
                        string dl = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
                        Process.Start("explorer.exe", "\"" + dl + "\"");
                    }
                    catch { }
                    break;
                case "toggleBench":
                    Store.Prefs.BenchServerEnabled = !Store.Prefs.BenchServerEnabled;
                    Store.SavePrefs();
                    if (Store.Prefs.BenchServerEnabled) BenchServer.Start(this);
                    else BenchServer.Stop();
                    settingsTab.RenderSettingsTab("general");
                    break;
                case "setLook":
                    Store.Prefs.Appearance = string.IsNullOrEmpty(arg) ? "System" : arg;
                    Store.SavePrefs();
                    ApplyLayoutPreferences();
                    settingsTab.RenderSettingsTab("appearance");
                    break;
                case "toggleDusk":
                    Store.Prefs.DuskEnabled = !Store.Prefs.DuskEnabled;
                    Store.SavePrefs();
                    ApplyLayoutPreferences();
                    settingsTab.RenderSettingsTab("appearance");
                    break;
                case "setTabLayout":
                    Store.Prefs.TabLayout = string.IsNullOrEmpty(arg) ? "Left" : arg;
                    Store.SavePrefs();
                    ApplyLayoutPreferences();
                    settingsTab.RenderSettingsTab("appearance");
                    break;
                case "toggleFold":
                    Store.Prefs.SidebarCollapsed = !Store.Prefs.SidebarCollapsed;
                    Store.SavePrefs();
                    ApplyLayoutPreferences();
                    settingsTab.RenderSettingsTab("appearance");
                    break;
                case "setWall":
                    Store.Prefs.BlankWallTheme = string.IsNullOrEmpty(arg) ? "Clean" : arg;
                    Store.SavePrefs();
                    settingsTab.RenderSettingsTab("themes");
                    break;
                case "cycleSleepMinutes":
                    int[] choices = new[] { 5, 15, 30, 60 };
                    int idx = Array.IndexOf(choices, Store.Prefs.AutoSleepMinutes);
                    Store.Prefs.AutoSleepMinutes = choices[(idx + 1) % choices.Length];
                    Store.SavePrefs();
                    settingsTab.RenderSettingsTab("tabs");
                    break;
                case "sleepAllNow":
                    foreach (var t in _allTabs)
                    {
                        if (t != _activeTab && t != _splitLeftTab && t != _splitRightTab && !t.KeepAwake)
                            t.PutToSleep();
                    }
                    RefreshTabRows();
                    settingsTab.RenderSettingsTab("tabs");
                    break;
                case "toggleSplit":
                    ToggleSplitView();
                    break;
                case "setEngine":
                    if (arg == "Google") { Store.Prefs.SearchEngineName = "Google"; Store.Prefs.SearchEngineUrl = "https://www.google.com/search?q="; }
                    else if (arg == "Brave") { Store.Prefs.SearchEngineName = "Brave Search"; Store.Prefs.SearchEngineUrl = "https://search.brave.com/search?q="; }
                    else { Store.Prefs.SearchEngineName = "DuckDuckGo"; Store.Prefs.SearchEngineUrl = "https://duckduckgo.com/?q="; }
                    Store.SavePrefs();
                    settingsTab.RenderSettingsTab("browsing");
                    break;
                case "togglePeek":
                    Store.Prefs.PeekEnabled = !Store.Prefs.PeekEnabled;
                    Store.Prefs.PinsOpenExternalInPeek = Store.Prefs.PeekEnabled;
                    Store.SavePrefs();
                    settingsTab.RenderSettingsTab("browsing");
                    break;
                case "switchSpace":
                    int spIdx;
                    if (int.TryParse(arg, out spIdx)) CycleSpace(spIdx);
                    break;
                case "toggleHello":
                    Store.Prefs.WindowsHelloForVault = !Store.Prefs.WindowsHelloForVault;
                    Store.SavePrefs();
                    settingsTab.RenderSettingsTab("passwords");
                    break;
                case "copyPassword":
                    {
                        var parts = (arg ?? "").Split('|');
                        if (parts.Length >= 2)
                        {
                            var login = Store.Logins.FirstOrDefault(l => l.Host == parts[0] && l.User == parts[1]);
                            if (login != null)
                            {
                                if (Store.Prefs.WindowsHelloForVault && !WindowsHelloVerifier.VerifyUser(this)) return;
                                string dec = Store.DecryptPassword(login);
                                if (!string.IsNullOrEmpty(dec)) Clipboard.SetText(dec);
                            }
                        }
                    }
                    break;
                case "deletePassword":
                    {
                        var parts = (arg ?? "").Split('|');
                        if (parts.Length >= 2)
                        {
                            var login = Store.Logins.FirstOrDefault(l => l.Host == parts[0] && l.User == parts[1]);
                            if (login != null)
                            {
                                if (Store.Prefs.WindowsHelloForVault && !WindowsHelloVerifier.VerifyUser(this, "Verify your identity to delete a saved password.")) return;
                                Store.Logins.Remove(login);
                                Store.SaveVault();
                                settingsTab.RenderSettingsTab("passwords");
                            }
                        }
                    }
                    break;
                case "toggleShield":
                    Store.Prefs.ShieldEnabled = !Store.Prefs.ShieldEnabled;
                    Store.SavePrefs();
                    settingsTab.RenderSettingsTab("privacy");
                    break;
                case "clearHistory":
                    Store.History.Clear();
                    Store.SaveHistory();
                    settingsTab.RenderSettingsTab("privacy");
                    break;
                case "installExt":
                    if (!string.IsNullOrWhiteSpace(arg))
                    {
                        try
                        {
                            ExtensionInfo info = Directory.Exists(arg.Trim())
                                ? CrxInstaller.RegisterUnpackedFolder(arg.Trim())
                                : await CrxInstaller.InstallFromWebStoreAsync(arg.Trim());
                            if (settingsTab.WebView != null && settingsTab.IsWebViewInitialized)
                            {
                                var method = settingsTab.WebView.CoreWebView2.Profile.GetType().GetMethod("AddBrowserExtensionAsync");
                                if (method != null)
                                {
                                    dynamic task = method.Invoke(settingsTab.WebView.CoreWebView2.Profile, new object[] { info.FolderPath });
                                    if (task != null) await task;
                                }
                            }
                            settingsTab.RenderSettingsTab("extensions");
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show("Extension install error: " + ex.Message, "Search Extensions", MessageBoxButton.OK, MessageBoxImage.Warning);
                        }
                    }
                    break;
                case "openWebStore":
                    CreateAndSelectTab("https://chromewebstore.google.com", false);
                    break;
                case "openExtPopup":
                    {
                        var ext = Store.Extensions.FirstOrDefault(x => x.Id == arg);
                        if (ext != null) OpenExtensionPopup(ext);
                    }
                    break;
                case "removeExt":
                    {
                        var ext = Store.Extensions.FirstOrDefault(x => x.Id == arg);
                        if (ext != null)
                        {
                            Store.Extensions.Remove(ext);
                            Store.SaveExtensions();
                            settingsTab.RenderSettingsTab("extensions");
                        }
                    }
                    break;
            }
        }

        private async Task CheckForUpdatesAsync()
        {
            const string repo = "busyflow/Search-Browser";
            const string currentVersion = "1.0.0";
            try
            {
                System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;
                string json = await Task.Run(() =>
                {
                    using (var wc = new System.Net.WebClient())
                    {
                        wc.Headers.Add("User-Agent", "SearchBrowser/1.0.0");
                        return wc.DownloadString("https://api.github.com/repos/" + repo + "/releases/latest");
                    }
                });
                string tag = ExtractJsonValue(json, "tag_name").TrimStart('v', 'V');
                if (string.IsNullOrEmpty(tag) || tag == currentVersion)
                {
                    MessageBox.Show("You are on the latest version (v" + currentVersion + ").", "Search Updates", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                var m = Regex.Match(json, "\"browser_download_url\"\\s*:\\s*\"([^\"]*Search-Setup\\.exe)\"");
                if (!m.Success)
                {
                    MessageBox.Show("New version v" + tag + " is available on GitHub, but no Search-Setup.exe asset was found.", "Search Updates", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                if (MessageBox.Show("Update available: v" + tag + " (current: v" + currentVersion + ").\n\nDownload and install now?", "Search Updates", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    string dlUrl = m.Groups[1].Value;
                    string setupPath = Path.Combine(Path.GetTempPath(), "Search-Setup-" + tag + ".exe");
                    await Task.Run(() =>
                    {
                        using (var wc = new System.Net.WebClient())
                        {
                            wc.Headers.Add("User-Agent", "SearchBrowser/1.0.0");
                            wc.DownloadFile(dlUrl, setupPath);
                        }
                    });
                    Process.Start(new ProcessStartInfo { FileName = setupPath, Arguments = "/S", UseShellExecute = true });
                }
            }
            catch (System.Net.WebException wex)
            {
                var resp = wex.Response as System.Net.HttpWebResponse;
                if (resp != null && resp.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    MessageBox.Show("You are on the latest version (v" + currentVersion + ").", "Search Updates", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                MessageBox.Show("Could not check for updates from github.com/" + repo + ":\n" + wex.Message, "Search Updates", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not check for updates from github.com/" + repo + ":\n" + ex.Message, "Search Updates", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private static string ExtractJsonValue(string json, string key)
        {
            var m = Regex.Match(json, "\"" + Regex.Escape(key) + "\"\\s*:\\s*\"((?:\\\\.|[^\"])*)\"");
            if (!m.Success) return "";
            return m.Groups[1].Value.Replace("\\\"", "\"").Replace("\\\\", "\\");
        }

        public async void CreateAndSelectTab(string url, bool openOmniboxIfBlank, bool isPrivate = false, string groupId = null)
        {
            var tab = new BrowserTab(url, url == "about:blank" ? "New Tab" : Address.DisplayHost(url), false, isPrivate, Store.Prefs.ActiveSpaceId, groupId);
            AttachTabEvents(tab);
            _allTabs.Add(tab);
            await SelectTabAsync(tab);
            RefreshTabRows();
        }

        public async void OpenOrSelectSettingsTab(string section = "general")
        {
            var existing = GetTabsForActiveSpace().FirstOrDefault(t => t.Url != null && t.Url.StartsWith("search://settings", StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                await SelectTabAsync(existing);
                existing.RenderSettingsTab(section);
                return;
            }
            var tab = new BrowserTab("search://settings/" + section, "Settings", false, false, Store.Prefs.ActiveSpaceId);
            AttachTabEvents(tab);
            _allTabs.Add(tab);
            await SelectTabAsync(tab);
            RefreshTabRows();
        }

        private void CreateNewTabGroup()
        {
            string name = "Group " + (Store.TabGroups.Count + 1);
            var grp = new TabGroupItem
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = name,
                SpaceId = Store.Prefs.ActiveSpaceId,
                Collapsed = false
            };
            Store.TabGroups.Add(grp);
            Store.SaveGroups();
            if (_activeTab != null && !_activeTab.Pinned)
            {
                _activeTab.GroupId = grp.Id;
                SaveCurrentSession();
            }
            RefreshTabRows();
        }

        public async Task SelectTabAsync(BrowserTab tab)
        {
            if (tab == null) return;
            Border targetHost = _leftPaneHost;
            if (_isSplitView && _activeTab == _splitRightTab && tab != _splitLeftTab) targetHost = _rightPaneHost;
            else if (_isSplitView && tab == _splitRightTab) targetHost = _rightPaneHost;
            await tab.EnsureAwakeAsync(_sharedEnv, targetHost);
            if (tab.WebView == null) { _activeTab = tab; RefreshTabRows(); return; }

            if (_isSplitView)
            {
                if (_activeTab == _splitRightTab && tab != _splitLeftTab)
                {
                    _splitRightTab = tab;
                    _rightPaneHost.Child = tab.WebView;
                }
                else if (tab != _splitRightTab)
                {
                    _splitLeftTab = tab;
                    _leftPaneHost.Child = tab.WebView;
                }
                _activeTab = tab;
                UpdateSplitFocusBorders();
            }
            else
            {
                _activeTab = tab;
                _splitLeftTab = tab;
                _leftPaneHost.Child = tab.WebView;
            }

            _veilBanner.Visibility = tab.IsVeilPicking ? Visibility.Visible : Visibility.Collapsed;
            Title = tab.DisplayTitle + " — Search";
            RefreshTabRows();
            SaveCurrentSession();
        }

        private async void CloseTab(BrowserTab tab)
        {
            if (tab == null) return;
            if (!tab.IsPrivate && tab.Url != "about:blank")
            {
                _closedTabs.Push(new SavedTabState
                {
                    Id = tab.Id,
                    Title = tab.Title,
                    Url = tab.Url,
                    FaviconUrl = tab.FaviconUrl,
                    Pinned = tab.Pinned,
                    KeepAwake = tab.KeepAwake,
                    SpaceId = tab.SpaceId,
                    GroupId = tab.GroupId
                });
            }

            if (_isSplitView && (tab == _splitLeftTab || tab == _splitRightTab))
            {
                ExitSplitView(tab == _splitLeftTab ? _splitRightTab : _splitLeftTab);
            }

            int idx = _allTabs.IndexOf(tab);
            _allTabs.Remove(tab);
            try { if (tab.WebView != null) tab.WebView.Dispose(); } catch { }

            var spaceTabs = GetTabsForActiveSpace();
            if (spaceTabs.Count == 0)
            {
                CreateAndSelectTab("about:blank", true);
                return;
            }

            if (_activeTab == tab)
            {
                var next = spaceTabs[Math.Max(0, Math.Min(spaceTabs.Count - 1, idx - 1))];
                await SelectTabAsync(next);
            }
            RefreshTabRows();
        }

        private async void ReopenClosedTab()
        {
            if (_closedTabs.Count == 0) return;
            var st = _closedTabs.Pop();
            var tab = new BrowserTab(st.Url, st.Title, st.Pinned, false, Store.Prefs.ActiveSpaceId, st.GroupId);
            AttachTabEvents(tab);
            _allTabs.Add(tab);
            await SelectTabAsync(tab);
            RefreshTabRows();
        }

        private async void ToggleSplitView()
        {
            if (_isSplitView)
            {
                ExitSplitView(_activeTab ?? _splitLeftTab);
                return;
            }

            var spaceTabs = GetTabsForActiveSpace();
            BrowserTab second = spaceTabs.FirstOrDefault(t => t != _activeTab);
            if (second == null)
            {
                second = new BrowserTab("https://en.wikipedia.org", "Wikipedia", false, false, Store.Prefs.ActiveSpaceId);
                AttachTabEvents(second);
                _allTabs.Add(second);
            }

            await second.EnsureAwakeAsync(_sharedEnv, _rightPaneHost);
            _isSplitView = true;
            _splitLeftTab = _activeTab;
            _splitRightTab = second;

            _stageGrid.ColumnDefinitions[0].Width = new GridLength(1, GridUnitType.Star);
            _stageGrid.ColumnDefinitions[2].Width = new GridLength(1, GridUnitType.Star);
            _splitDivider.Visibility = Visibility.Visible;
            _rightPaneHost.Visibility = Visibility.Visible;

            _leftPaneHost.Child = _splitLeftTab.WebView;
            _rightPaneHost.Child = _splitRightTab.WebView;
            UpdateSplitFocusBorders();
            RefreshTabRows();
        }

        private void ExitSplitView(BrowserTab keepActive)
        {
            _isSplitView = false;
            _rightPaneHost.Child = null;
            _rightPaneHost.Visibility = Visibility.Collapsed;
            _splitDivider.Visibility = Visibility.Collapsed;
            _stageGrid.ColumnDefinitions[0].Width = new GridLength(1, GridUnitType.Star);
            _stageGrid.ColumnDefinitions[2].Width = new GridLength(0, GridUnitType.Pixel);

            _leftPaneHost.BorderThickness = new Thickness(0);
            _rightPaneHost.BorderThickness = new Thickness(0);

            if (keepActive != null)
            {
                _activeTab = keepActive;
                _splitLeftTab = keepActive;
                _leftPaneHost.Child = keepActive.WebView;
            }
            RefreshTabRows();
        }

        private void UpdateSplitFocusBorders()
        {
            if (!_isSplitView)
            {
                _leftPaneHost.BorderThickness = new Thickness(0);
                _rightPaneHost.BorderThickness = new Thickness(0);
                return;
            }
            _leftPaneHost.BorderBrush = _activeTab == _splitLeftTab ? Palette.Ink : Palette.Hairline;
            _leftPaneHost.BorderThickness = new Thickness(_activeTab == _splitLeftTab ? 1.5 : 0);
            _rightPaneHost.BorderBrush = _activeTab == _splitRightTab ? Palette.Ink : Palette.Hairline;
            _rightPaneHost.BorderThickness = new Thickness(_activeTab == _splitRightTab ? 1.5 : 0);
        }

        private void CheckTabAutoSleep()
        {
            int mins = Store.Prefs.AutoSleepMinutes;
            if (mins <= 0) return;
            DateTime cutoff = DateTime.UtcNow.AddMinutes(-mins);
            foreach (var tab in _allTabs)
            {
                if (tab != _activeTab && tab != _splitLeftTab && tab != _splitRightTab &&
                    !tab.KeepAwake && !tab.IsSleeping && tab.LastTouchedUtc < cutoff)
                {
                    tab.PutToSleep();
                }
            }
            RefreshTabRows();
        }

        private async void CycleSpace(int? explicitIndex = null)
        {
            if (Store.Spaces.Count == 0) return;
            int idx = Store.Spaces.FindIndex(s => s.Id == Store.Prefs.ActiveSpaceId);
            int nextIdx = explicitIndex.HasValue
                ? Math.Max(0, Math.Min(Store.Spaces.Count - 1, explicitIndex.Value))
                : (idx + 1) % Store.Spaces.Count;

            Store.Prefs.ActiveSpaceId = Store.Spaces[nextIdx].Id;
            Store.SavePrefs();
            RefreshSpaceHeader();

            if (_isSplitView) ExitSplitView(null);

            var spaceTabs = GetTabsForActiveSpace();
            if (spaceTabs.Count == 0)
            {
                CreateAndSelectTab("about:blank", false);
            }
            else
            {
                await SelectTabAsync(spaceTabs[0]);
            }
            RefreshTabRows();
        }

        private void RefreshSpaceHeader()
        {
            var sp = Store.Spaces.FirstOrDefault(s => s.Id == Store.Prefs.ActiveSpaceId) ?? Store.Spaces[0];
            _spaceTitleText.Text = "●  " + sp.Name;

            if (_spaceDotsPanel != null)
            {
                _spaceDotsPanel.Children.Clear();
                for (int i = 0; i < Store.Spaces.Count; i++)
                {
                    var sItem = Store.Spaces[i];
                    bool isCur = sItem.Id == sp.Id;
                    var dot = new Border
                    {
                        Width = isCur ? 14 : 6,
                        Height = 6,
                        CornerRadius = new CornerRadius(3),
                        Margin = new Thickness(2.5, 0, 2.5, 0),
                        Background = isCur ? Palette.Ink : Palette.Faint,
                        Cursor = Cursors.Hand,
                        ToolTip = sItem.Name + " Space (Alt+" + (i + 1) + ")"
                    };
                    int capIdx = i;
                    dot.MouseLeftButtonDown += (sender, ev) =>
                    {
                        ev.Handled = true;
                        CycleSpace(capIdx);
                    };
                    _spaceDotsPanel.Children.Add(dot);
                }
            }
        }

        private void ApplyLayoutPreferences()
        {
            Palette.ResolveTheme();
            Background = Palette.Frame;
            _sidebarBorder.Background = Palette.Frame;
            if (_glidingTabPill != null) _glidingTabPill.Background = Palette.Wash;
            _topTabBarBorder.Background = Palette.Frame;
            _pageCardBorder.Background = Palette.Ground;
            _pageCardBorder.BorderBrush = Palette.Hairline;
            _omniboxCard.Background = Palette.CardSurface;
            _omniboxCard.BorderBrush = Palette.Hairline;
            _omniboxInput.Foreground = Palette.Ink;
            _omniboxInput.CaretBrush = Palette.Ink;
            _omniboxOffersBorder.Background = Palette.CardSurface;
            _omniboxOffersBorder.BorderBrush = Palette.Hairline;
            _drawerCard.Background = Palette.CardSurface;
            _drawerCard.BorderBrush = Palette.Hairline;
            _drawerTitle.Foreground = Palette.Ink;
            _spaceTitleText.Foreground = Palette.Ink;

            bool isTop = string.Equals(Store.Prefs.TabLayout, "Top", StringComparison.OrdinalIgnoreCase);
            bool isIconsOnly = string.Equals(Store.Prefs.TabLayout, "IconsOnly", StringComparison.OrdinalIgnoreCase);
            _topTabBarBorder.Visibility = Visibility.Visible;
            if (isTop)
            {
                _sidebarBorder.Visibility = Visibility.Collapsed;
                _sidebarHoverHotzone.Visibility = Visibility.Collapsed;
                _topTabsStack.Visibility = Visibility.Visible;
            }
            else
            {
                _topTabsStack.Visibility = Visibility.Collapsed;
                _sidebarBorder.Width = isIconsOnly ? 56 : 232;
                _sidebarBorder.Visibility = Store.Prefs.SidebarCollapsed ? Visibility.Collapsed : Visibility.Visible;
                _sidebarHoverHotzone.Visibility = Store.Prefs.SidebarCollapsed ? Visibility.Visible : Visibility.Collapsed;
            }

            if (Store.Prefs.PageAsCard)
            {
                _pageCardBorder.CornerRadius = new CornerRadius(10);
                _pageCardBorder.Margin = new Thickness(0, 0, 8, 8);
                _pageCardBorder.BorderThickness = new Thickness(1);
            }
            else
            {
                _pageCardBorder.CornerRadius = new CornerRadius(0);
                _pageCardBorder.Margin = new Thickness(0);
                _pageCardBorder.BorderThickness = new Thickness(0);
            }

            foreach (var t in _allTabs)
            {
                if (t.IsWebViewInitialized && t.WebView != null && t.WebView.CoreWebView2 != null)
                {
                    t.WebView.CoreWebView2.Profile.PreferredColorScheme = Palette.IsDark
                        ? CoreWebView2PreferredColorScheme.Dark
                        : CoreWebView2PreferredColorScheme.Light;
                    if (t.Url == "about:blank") t.RenderBlankWall();
                    else if (t.Url != null && t.Url.StartsWith("search://settings", StringComparison.OrdinalIgnoreCase)) t.RenderSettingsTab();
                    else { var _ = t.ApplySiteStyleSheetsAsync(); }
                }
            }

            RefreshTabRows();
        }

        private UIElement CreateFaviconOrGlyph(BrowserTab tab, double size)
        {
            if (!string.IsNullOrEmpty(tab.FaviconUrl) && tab.FaviconUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.UriSource = new Uri(tab.FaviconUrl);
                    bmp.DecodePixelWidth = (int)(size * 2);
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.EndInit();
                    return new Image
                    {
                        Source = bmp,
                        Width = size,
                        Height = size,
                        VerticalAlignment = VerticalAlignment.Center,
                        HorizontalAlignment = HorizontalAlignment.Center
                    };
                }
                catch { }
            }
            return new TextBlock
            {
                Text = tab.PinGlyph,
                FontSize = size * 0.82,
                FontWeight = FontWeights.SemiBold,
                Foreground = Palette.Muted,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
        }

        private void RefreshTabRows()
        {
            _pinsPanel.Children.Clear();
            _tabsStack.Children.Clear();
            _topTabsStack.Children.Clear();

            var spaceTabs = GetTabsForActiveSpace();
            var pinned = spaceTabs.Where(t => t.Pinned).ToList();
            var unpinned = spaceTabs.Where(t => !t.Pinned).ToList();

            // 1. Pinned squares in sidebar (with real Favicon or Letter monogram)
            foreach (var pin in pinned)
            {
                bool isLive = pin == _activeTab || (_isSplitView && (pin == _splitLeftTab || pin == _splitRightTab));
                var sq = new Border
                {
                    Width = 36,
                    Height = 36,
                    Margin = new Thickness(2),
                    CornerRadius = new CornerRadius(7),
                    Background = isLive ? Palette.Wash : Brushes.Transparent,
                    BorderBrush = isLive ? Palette.Hairline : Brushes.Transparent,
                    BorderThickness = new Thickness(1),
                    Cursor = Cursors.Hand,
                    ToolTip = pin.DisplayTitle + " (External links open in Peek · Right-click to unpin)"
                };
                sq.Child = CreateFaviconOrGlyph(pin, 16);
                var captured = pin;
                sq.MouseEnter += (s, e) => { if (captured != _activeTab) sq.Background = Palette.Hover; };
                sq.MouseLeave += (s, e) => { if (captured != _activeTab) sq.Background = Brushes.Transparent; };
                sq.MouseLeftButtonDown += async (s, e) => await SelectTabAsync(captured);
                sq.MouseRightButtonUp += (s, e) => { e.Handled = true; ShowTabContextMenu(captured); };
                _pinsPanel.Children.Add(sq);
            }

            // 2. Collapsible Tab Groups / Folders in current Space (from Search's TabGroups.swift)
            var spaceGroups = Store.TabGroups.Where(g => string.Equals(g.SpaceId, Store.Prefs.ActiveSpaceId, StringComparison.OrdinalIgnoreCase)).ToList();
            foreach (var grp in spaceGroups)
            {
                var grpTabs = unpinned.Where(t => t.GroupId == grp.Id).ToList();
                var hdr = new Border
                {
                    Height = 26,
                    CornerRadius = new CornerRadius(6),
                    Margin = new Thickness(0, 4, 0, 1),
                    Padding = new Thickness(8, 0, 8, 0),
                    Background = Brushes.Transparent,
                    Cursor = Cursors.Hand
                };
                var hdrDock = new DockPanel();
                var countLbl = new TextBlock
                {
                    Text = grpTabs.Count.ToString(),
                    FontSize = 11,
                    Foreground = Palette.Muted,
                    VerticalAlignment = VerticalAlignment.Center
                };
                DockPanel.SetDock(countLbl, Dock.Right);
                hdrDock.Children.Add(countLbl);

                var nameLbl = new TextBlock
                {
                    Text = (grp.Collapsed ? "▸ " : "▾ ") + grp.Name,
                    FontSize = 11.5,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = Palette.Muted,
                    VerticalAlignment = VerticalAlignment.Center
                };
                hdrDock.Children.Add(nameLbl);
                hdr.Child = hdrDock;

                var capGrp = grp;
                hdr.MouseLeftButtonDown += (s, e) =>
                {
                    capGrp.Collapsed = !capGrp.Collapsed;
                    Store.SaveGroups();
                    RefreshTabRows();
                };
                hdr.MouseRightButtonUp += (s, e) =>
                {
                    var m = new ContextMenu();
                    AddMenuItem(m, "+ New Tab in " + capGrp.Name, () => CreateAndSelectTab("about:blank", true, false, capGrp.Id));
                    AddMenuItem(m, "Ungroup & Remove Folder", () =>
                    {
                        foreach (var gt in _allTabs.Where(x => x.GroupId == capGrp.Id)) gt.GroupId = "";
                        Store.TabGroups.Remove(capGrp);
                        Store.SaveGroups();
                        SaveCurrentSession();
                        RefreshTabRows();
                    });
                    m.IsOpen = true;
                };
                _tabsStack.Children.Add(hdr);

                if (!grp.Collapsed)
                {
                    foreach (var gt in grpTabs)
                    {
                        _tabsStack.Children.Add(CreateVerticalTabRow(gt, true));
                    }
                }
            }

            // 3. Ungrouped vertical rows in sidebar
            foreach (var tab in unpinned.Where(t => string.IsNullOrEmpty(t.GroupId) || !spaceGroups.Any(g => g.Id == t.GroupId)))
            {
                _tabsStack.Children.Add(CreateVerticalTabRow(tab, false));
            }

            // 3b. "+ New tab" quiet row directly underneath the last tab in the column (1:1 with Oriel Screenshot 1 & Lines.tsx NewTabRow)
            _tabsStack.Children.Add(CreateInlineNewTabRow());

            // 4. Horizontal pills in top bar (when in Top mode)
            foreach (var tab in spaceTabs)
            {
                _topTabsStack.Children.Add(CreateHorizontalTabPill(tab));
            }

            RefreshSpaceHeader();
            AnimateGlidingTabPill();
        }

        private UIElement CreateInlineNewTabRow()
        {
            var row = new Border
            {
                Height = 28,
                CornerRadius = new CornerRadius(9),
                Margin = new Thickness(0, 1, 0, 1),
                Padding = new Thickness(10, 0, 10, 0),
                Background = Brushes.Transparent,
                Cursor = Cursors.Hand,
                ToolTip = "New Tab   Ctrl+T"
            };
            var stack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var plusBox = new Border
            {
                Width = 15,
                Height = 15,
                Margin = new Thickness(0, 0, 8, 0),
                Child = VectorIcons.Draw(VectorIcons.PlusPath, Palette.Muted, 11, 1.5)
            };
            var lbl = new TextBlock
            {
                Text = "New tab",
                FontSize = 12.5,
                Foreground = Palette.Muted,
                Opacity = 0.65,
                VerticalAlignment = VerticalAlignment.Center
            };
            stack.Children.Add(plusBox);
            stack.Children.Add(lbl);
            row.Child = stack;

            row.MouseEnter += (s, e) =>
            {
                row.Background = Palette.Hover;
                lbl.Foreground = Palette.Ink;
                lbl.Opacity = 0.85;
            };
            row.MouseLeave += (s, e) =>
            {
                row.Background = Brushes.Transparent;
                lbl.Foreground = Palette.Muted;
                lbl.Opacity = 0.65;
            };
            row.MouseLeftButtonDown += (s, e) =>
            {
                e.Handled = true;
                ShowOmnibox(false, true);
            };
            return row;
        }

        private void ShowSpacesMenu(FrameworkElement anchor)
        {
            StackPanel stack;
            var popup = CreateThemedPopupMenu(anchor, System.Windows.Controls.Primitives.PlacementMode.Top, out stack);
            for (int i = 0; i < Store.Spaces.Count; i++)
            {
                var sp = Store.Spaces[i];
                int idx = i;
                bool isCur = string.Equals(sp.Id, Store.Prefs.ActiveSpaceId, StringComparison.OrdinalIgnoreCase);
                AddPopupMenuItem(popup, stack, (isCur ? "✓  " : "     ") + sp.Name, i < 9 ? "Alt+" + (i + 1) : "", () => CycleSpace(idx));
            }
            AddPopupSeparator(stack);
            AddPopupMenuItem(popup, stack, "New Space…", "", () =>
            {
                string name = PromptForText("New Space", "Enter a name for the new Space:", "Space " + (Store.Spaces.Count + 1));
                if (!string.IsNullOrWhiteSpace(name))
                {
                    var ns = new SpaceItem { Id = Guid.NewGuid().ToString("N").Substring(0, 8), Name = name.Trim(), AccentHex = "" };
                    Store.Spaces.Add(ns);
                    Store.SaveSpaces();
                    CycleSpace(Store.Spaces.Count - 1);
                }
            });
            var curSp = Store.Spaces.FirstOrDefault(s => string.Equals(s.Id, Store.Prefs.ActiveSpaceId, StringComparison.OrdinalIgnoreCase));
            if (curSp != null)
            {
                AddPopupMenuItem(popup, stack, "Rename \"" + curSp.Name + "\"…", "", () =>
                {
                    string name = PromptForText("Rename Space", "Space name:", curSp.Name);
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        curSp.Name = name.Trim();
                        Store.SaveSpaces();
                        RefreshSpaceHeader();
                    }
                });
            }
            AddPopupSeparator(stack);
            AddPopupMenuItem(popup, stack, "Downloads", "Ctrl+J", () => ShowDownloadsDrawer());
            popup.IsOpen = true;
        }

        private System.Windows.Controls.Primitives.Popup CreateThemedPopupMenu(FrameworkElement anchor, System.Windows.Controls.Primitives.PlacementMode placement, out StackPanel stack)
        {
            stack = new StackPanel { Orientation = Orientation.Vertical, MinWidth = 210 };
            var card = new Border
            {
                Background = Palette.CardSurface,
                BorderBrush = Palette.Hairline,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(5),
                Child = stack,
                Effect = new DropShadowEffect { BlurRadius = 20, ShadowDepth = 4, Opacity = 0.28 }
            };
            var popup = new System.Windows.Controls.Primitives.Popup
            {
                Child = card,
                AllowsTransparency = true,
                StaysOpen = false,
                PopupAnimation = System.Windows.Controls.Primitives.PopupAnimation.Fade
            };
            if (anchor != null)
            {
                popup.PlacementTarget = anchor;
                popup.Placement = placement;
            }
            else
            {
                popup.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
            }
            return popup;
        }

        private void AddPopupSeparator(StackPanel stack)
        {
            stack.Children.Add(new Border
            {
                Height = 1,
                Background = Palette.Hairline,
                Margin = new Thickness(6, 4, 6, 4)
            });
        }

        private void AddPopupMenuItem(System.Windows.Controls.Primitives.Popup popup, StackPanel stack, string title, string shortcut, Action action)
        {
            var row = new Border
            {
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(10, 6, 10, 6),
                Background = Brushes.Transparent,
                Cursor = Cursors.Hand
            };
            var dock = new DockPanel();
            if (!string.IsNullOrEmpty(shortcut))
            {
                var sc = new TextBlock
                {
                    Text = shortcut,
                    FontSize = 11.5,
                    Foreground = Palette.Muted,
                    Margin = new Thickness(16, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                DockPanel.SetDock(sc, Dock.Right);
                dock.Children.Add(sc);
            }
            var lbl = new TextBlock
            {
                Text = title,
                FontSize = 12.5,
                Foreground = Palette.Ink,
                VerticalAlignment = VerticalAlignment.Center
            };
            dock.Children.Add(lbl);
            row.Child = dock;
            row.MouseEnter += (s, e) => row.Background = Palette.Hover;
            row.MouseLeave += (s, e) => row.Background = Brushes.Transparent;
            row.MouseLeftButtonUp += (s, e) =>
            {
                e.Handled = true;
                popup.IsOpen = false;
                action();
            };
            stack.Children.Add(row);
        }

        private static string PromptForText(string title, string label, string defaultVal)
        {
            var win = new Window
            {
                Title = title,
                Width = 340,
                Height = 155,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ResizeMode = ResizeMode.NoResize,
                Background = Palette.Ground,
                Foreground = Palette.Ink
            };
            var sp = new StackPanel { Margin = new Thickness(16) };
            sp.Children.Add(new TextBlock { Text = label, FontSize = 12.5, Margin = new Thickness(0, 0, 0, 8), Foreground = Palette.Ink });
            var tb = new TextBox { Text = defaultVal ?? "", Padding = new Thickness(6, 4, 6, 4), Margin = new Thickness(0, 0, 0, 12) };
            sp.Children.Add(tb);
            var ok = new Button { Content = "OK", Width = 76, Height = 26, HorizontalAlignment = HorizontalAlignment.Right, IsDefault = true };
            ok.Click += (s, e) => { win.DialogResult = true; win.Close(); };
            sp.Children.Add(ok);
            win.Content = sp;
            tb.Focus();
            tb.SelectAll();
            return win.ShowDialog() == true ? tb.Text : null;
        }

        private void AnimateGlidingTabPill()
        {
            if (_glidingTabPill == null || _glidingPillTransform == null) return;
            Dispatcher.InvokeAsync(() =>
            {
                Border activeRow = null;
                foreach (var child in _tabsStack.Children)
                {
                    var b = child as Border;
                    if (b != null && b.Tag is BrowserTab && ((BrowserTab)b.Tag) == _activeTab)
                    {
                        activeRow = b;
                        break;
                    }
                }

                if (activeRow == null || (_activeTab != null && _activeTab.Pinned))
                {
                    _glidingTabPill.Visibility = Visibility.Collapsed;
                    return;
                }

                try
                {
                    Point pt = activeRow.TransformToAncestor(_tabsContainerGrid).Transform(new Point(0, 0));
                    _glidingTabPill.Margin = new Thickness(activeRow.Margin.Left, 0, 0, 0);
                    if (_glidingTabPill.Visibility != Visibility.Visible)
                    {
                        _glidingTabPill.Visibility = Visibility.Visible;
                        _glidingPillTransform.BeginAnimation(TranslateTransform.YProperty, null);
                        _glidingPillTransform.Y = pt.Y;
                    }
                    else
                    {
                        var anim = new DoubleAnimation
                        {
                            To = pt.Y,
                            Duration = TimeSpan.FromMilliseconds(165),
                            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                        };
                        _glidingPillTransform.BeginAnimation(TranslateTransform.YProperty, anim);
                    }
                }
                catch { }
            }, DispatcherPriority.Loaded);
        }

        private UIElement CreateVerticalTabRow(BrowserTab tab, bool indented)
        {
            bool isLive = tab == _activeTab;
            bool isSecondarySplit = !isLive && _isSplitView && (tab == _splitLeftTab || tab == _splitRightTab);
            bool isBlank = tab.Url == "about:blank";
            var row = new Border
            {
                Tag = tab,
                Height = 28,
                CornerRadius = new CornerRadius(9),
                Margin = new Thickness(indented ? 10 : 0, 1, 0, 1),
                Padding = new Thickness(10, 0, 6, 0),
                Background = isSecondarySplit ? Palette.Hover : Brushes.Transparent,
                Cursor = Cursors.Hand,
                AllowDrop = true,
                ToolTip = tab.DisplayTitle + "\n" + tab.Url + (tab.IsSleeping ? "\n(Sleeping — zero RAM used)" : "")
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = isBlank ? new GridLength(0) : new GridLength(23) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            if (!isBlank)
            {
                UIElement iconEl;
                if (tab.IsLoading)
                    iconEl = new TextBlock { Text = "◌", FontSize = 11, Foreground = Palette.Muted, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
                else if (tab.IsPrivate)
                    iconEl = new TextBlock { Text = "🕶", FontSize = 11, Foreground = Palette.Muted, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
                else if (tab.Url != null && tab.Url.StartsWith("search://settings", StringComparison.OrdinalIgnoreCase))
                {
                    iconEl = new Border
                    {
                        Width = 15,
                        Height = 15,
                        CornerRadius = new CornerRadius(3.3),
                        Background = Palette.Hover,
                        HorizontalAlignment = HorizontalAlignment.Left,
                        VerticalAlignment = VerticalAlignment.Center,
                        Child = new TextBlock
                        {
                            Text = "S",
                            FontSize = 8.5,
                            FontWeight = FontWeights.SemiBold,
                            Foreground = Palette.Muted,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            VerticalAlignment = VerticalAlignment.Center
                        }
                    };
                }
                else
                    iconEl = CreateFaviconOrGlyph(tab, 15);
                grid.Children.Add(iconEl);
            }

            var titleBlock = new TextBlock
            {
                Text = (tab.KeepAwake ? "⚡ " : "") + tab.DisplayTitle,
                FontSize = 12.5,
                FontWeight = FontWeights.Normal,
                Foreground = isLive ? Palette.Ink : Palette.Muted,
                Opacity = tab.IsSleeping && !isLive ? 0.45 : 1.0,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            Grid.SetColumn(titleBlock, 1);
            grid.Children.Add(titleBlock);

            var closeBtn = new Border
            {
                Width = 20,
                Height = 20,
                CornerRadius = new CornerRadius(4),
                Background = Brushes.Transparent,
                Visibility = Visibility.Hidden,
                Child = new TextBlock
                {
                    Text = "✕",
                    FontSize = 10,
                    Foreground = Palette.Muted,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            var captured = tab;
            closeBtn.MouseLeftButtonDown += (s, e) =>
            {
                e.Handled = true;
                CloseTab(captured);
            };
            Grid.SetColumn(closeBtn, 2);
            grid.Children.Add(closeBtn);

            row.Child = grid;
            row.MouseEnter += (s, e) =>
            {
                closeBtn.Visibility = Visibility.Visible;
                if (!isLive) row.Background = Palette.Hover;
            };
            row.MouseLeave += (s, e) =>
            {
                closeBtn.Visibility = Visibility.Hidden;
                if (!isLive) row.Background = Brushes.Transparent;
            };
            row.MouseLeftButtonDown += async (s, e) =>
            {
                await SelectTabAsync(captured);
            };
            row.MouseMove += (s, e) =>
            {
                if (e.LeftButton == MouseButtonState.Pressed && _draggedTab == null)
                {
                    _draggedTab = captured;
                    DragDrop.DoDragDrop(row, captured.Id, DragDropEffects.Move);
                    _draggedTab = null;
                }
            };
            row.DragOver += (s, e) =>
            {
                if (_draggedTab != null && _draggedTab != captured)
                {
                    e.Effects = DragDropEffects.Move;
                    e.Handled = true;
                }
            };
            row.Drop += (s, e) =>
            {
                if (_draggedTab != null && _draggedTab != captured)
                {
                    int fromIdx = _allTabs.IndexOf(_draggedTab);
                    int toIdx = _allTabs.IndexOf(captured);
                    if (fromIdx >= 0 && toIdx >= 0)
                    {
                        _draggedTab.GroupId = captured.GroupId;
                        _allTabs.RemoveAt(fromIdx);
                        _allTabs.Insert(toIdx, _draggedTab);
                        SaveCurrentSession();
                        RefreshTabRows();
                    }
                    _draggedTab = null;
                    e.Handled = true;
                }
            };
            row.MouseDown += (s, e) =>
            {
                if (e.ChangedButton == MouseButton.Middle)
                {
                    e.Handled = true;
                    CloseTab(captured);
                }
            };
            row.MouseRightButtonUp += (s, e) => { e.Handled = true; ShowTabContextMenu(captured); };

            return row;
        }

        private UIElement CreateHorizontalTabPill(BrowserTab tab)
        {
            bool isLive = tab == _activeTab || (_isSplitView && (tab == _splitLeftTab || tab == _splitRightTab));
            var pill = new Border
            {
                Height = 30,
                MaxWidth = tab.Pinned ? 40 : 190,
                MinWidth = tab.Pinned ? 36 : 110,
                CornerRadius = new CornerRadius(7),
                Margin = new Thickness(2, 0, 2, 0),
                Padding = new Thickness(10, 0, 8, 0),
                Background = isLive ? Palette.Wash : Brushes.Transparent,
                Cursor = Cursors.Hand
            };

            var dock = new DockPanel();
            if (!tab.Pinned)
            {
                var closeText = new TextBlock
                {
                    Text = "✕",
                    FontSize = 10,
                    Foreground = Palette.Muted,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(6, 0, 0, 0)
                };
                var capturedClose = tab;
                closeText.MouseLeftButtonDown += (s, e) =>
                {
                    e.Handled = true;
                    CloseTab(capturedClose);
                };
                DockPanel.SetDock(closeText, Dock.Right);
                dock.Children.Add(closeText);
            }

            var label = new TextBlock
            {
                Text = tab.Pinned ? tab.PinGlyph : tab.DisplayTitle,
                FontSize = 12.5,
                FontWeight = isLive ? FontWeights.Medium : FontWeights.Normal,
                Foreground = isLive ? Palette.Ink : Palette.Muted,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            dock.Children.Add(label);
            pill.Child = dock;

            var captured = tab;
            pill.MouseLeftButtonDown += async (s, e) =>
            {
                e.Handled = true;
                await SelectTabAsync(captured);
            };
            pill.MouseRightButtonUp += (s, e) => { e.Handled = true; ShowTabContextMenu(captured); };
            return pill;
        }

        private void ShowTabContextMenu(BrowserTab tab)
        {
            StackPanel stack;
            var popup = CreateThemedPopupMenu(null, System.Windows.Controls.Primitives.PlacementMode.MousePoint, out stack);
            AddPopupMenuItem(popup, stack, tab.Pinned ? "Unpin Tab" : "Pin Tab", "", () =>
            {
                tab.Pinned = !tab.Pinned;
                RefreshTabRows();
                SaveCurrentSession();
            });
            AddPopupMenuItem(popup, stack, tab.KeepAwake ? "Allow Tab to Sleep" : "Keep This Site Awake", "", () =>
            {
                tab.KeepAwake = !tab.KeepAwake;
                RefreshTabRows();
                SaveCurrentSession();
            });
            AddPopupMenuItem(popup, stack, "Put Tab to Sleep Now", "", () =>
            {
                if (tab != _activeTab) tab.PutToSleep();
            });
            AddPopupMenuItem(popup, stack, "Duplicate Tab", "Ctrl+D", () =>
            {
                CreateAndSelectTab(tab.Url, false, tab.IsPrivate, tab.GroupId);
            });
            AddPopupMenuItem(popup, stack, "Split with Current Page", "Alt+Ctrl+N", async () =>
            {
                if (_activeTab != null && tab != _activeTab)
                {
                    if (!_isSplitView) ToggleSplitView();
                    await tab.EnsureAwakeAsync(_sharedEnv, _rightPaneHost);
                    _splitRightTab = tab;
                    _rightPaneHost.Child = tab.WebView;
                    RefreshTabRows();
                }
            });
            AddPopupMenuItem(popup, stack, "Copy Clean Link", "Ctrl+Shift+C", () =>
            {
                Clipboard.SetText(CleanLink.Clean(tab.Url));
            });

            var spaceGroups = Store.TabGroups.Where(g => g.SpaceId == Store.Prefs.ActiveSpaceId).ToList();
            if (spaceGroups.Count > 0)
            {
                AddPopupSeparator(stack);
                foreach (var g in spaceGroups)
                {
                    var capG = g;
                    AddPopupMenuItem(popup, stack, "Move to Folder: " + g.Name, "", () =>
                    {
                        tab.GroupId = capG.Id;
                        SaveCurrentSession();
                        RefreshTabRows();
                    });
                }
                if (!string.IsNullOrEmpty(tab.GroupId))
                {
                    AddPopupMenuItem(popup, stack, "Remove from Folder", "", () =>
                    {
                        tab.GroupId = "";
                        SaveCurrentSession();
                        RefreshTabRows();
                    });
                }
            }

            AddPopupSeparator(stack);
            AddPopupMenuItem(popup, stack, "Close Tab", "Ctrl+W", () => CloseTab(tab));
            popup.IsOpen = true;
        }

        private static void AddMenuItem(ContextMenu menu, string header, Action action)
        {
            var item = new MenuItem { Header = header };
            item.Click += (s, e) => action();
            menu.Items.Add(item);
        }

        // OMNIBOX ("One field." from Search README)
        public void ShowOmnibox(bool tabSwitcherMode, bool newTabMode = false)
        {
            _omniboxIsTabSwitcherMode = tabSwitcherMode;
            _omniboxIsNewTabMode = newTabMode && !tabSwitcherMode;
            _drawerOverlay.Visibility = Visibility.Collapsed;
            _omniboxCard.Background = Palette.Ground;
            _omniboxCard.BorderBrush = Palette.Hairline;
            _omniboxInput.Foreground = Palette.Ink;
            _omniboxInput.CaretBrush = Palette.Ink;
            _omniboxOffersBorder.Background = Palette.CardSurface;
            _omniboxOffersBorder.BorderBrush = Palette.Hairline;
            _omniboxPopup.IsOpen = true;

            if (tabSwitcherMode)
            {
                _siteChipText.Text = "Switch Tab";
                _siteChipBorder.Visibility = Visibility.Visible;
                _omniboxInput.Text = "";
            }
            else if (_omniboxIsNewTabMode)
            {
                _siteChipText.Text = "New tab: enter a web address";
                _siteChipBorder.Visibility = Visibility.Visible;
                _omniboxInput.Text = "";
            }
            else
            {
                string currentUrl = _activeTab != null ? _activeTab.Url : "";
                _siteChipBorder.Visibility = Visibility.Collapsed;
                _omniboxInput.Text = (currentUrl == "about:blank" || (currentUrl != null && currentUrl.StartsWith("search://"))) ? "" : currentUrl;
            }

            RefreshOmniboxOffers();
            Dispatcher.InvokeAsync(() =>
            {
                _omniboxInput.Focus();
                _omniboxInput.SelectAll();
            }, DispatcherPriority.Input);
        }

        private void HideOmnibox()
        {
            if (_omniboxPopup != null) _omniboxPopup.IsOpen = false;
            if (_activeTab != null && _activeTab.WebView != null)
                _activeTab.WebView.Focus();
        }

        private void RefreshOmniboxOffers()
        {
            _omniboxOffersStack.Children.Clear();
            _currentOfferActions.Clear();
            _selectedOfferIndex = -1;

            string q = (_omniboxInput.Text ?? "").Trim();

            if (_omniboxIsTabSwitcherMode)
            {
                var matches = _allTabs.Where(t =>
                    string.IsNullOrEmpty(q) ||
                    t.DisplayTitle.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    t.Url.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0)
                    .Take(9).ToList();

                foreach (var tab in matches)
                {
                    var captured = tab;
                    var spObj = Store.Spaces.FirstOrDefault(s => s.Id == tab.SpaceId);
                    string spaceName = (spObj != null ? spObj.Name : null) ?? "Personal";
                    AddOmniboxOfferRow("◰  " + tab.DisplayTitle, Address.DisplayHost(tab.Url) + " · " + spaceName, async () =>
                    {
                        HideOmnibox();
                        if (captured.SpaceId != Store.Prefs.ActiveSpaceId)
                        {
                            Store.Prefs.ActiveSpaceId = captured.SpaceId;
                            RefreshSpaceHeader();
                        }
                        await SelectTabAsync(captured);
                    });
                }
            }
            else
            {
                if (!string.IsNullOrEmpty(q))
                {
                    string parsedUrl = Address.TryParseUrl(q);
                    if (parsedUrl != null)
                    {
                        AddOmniboxOfferRow("→  Go to " + parsedUrl, "Open Address", () =>
                        {
                            bool isNew = _omniboxIsNewTabMode;
                            HideOmnibox();
                            if (isNew) CreateAndSelectTab(parsedUrl, false);
                            else if (_activeTab != null) _activeTab.Navigate(parsedUrl);
                        });
                    }
                    else
                    {
                        AddOmniboxOfferRow("⌕  Search " + Store.Prefs.SearchEngineName + " for \"" + q + "\"", "Web Search", () =>
                        {
                            bool isNew = _omniboxIsNewTabMode;
                            HideOmnibox();
                            if (isNew) CreateAndSelectTab(q, false);
                            else if (_activeTab != null) _activeTab.Navigate(q);
                        });
                    }

                    foreach (var tab in _allTabs.Where(t =>
                        t.DisplayTitle.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0 ||
                        t.Url.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0).Take(3))
                    {
                        var captured = tab;
                        AddOmniboxOfferRow("◰  Switch to: " + tab.DisplayTitle, Address.DisplayHost(tab.Url), async () =>
                        {
                            HideOmnibox();
                            await SelectTabAsync(captured);
                        });
                    }

                    foreach (var bm in Store.Bookmarks.Where(b =>
                        b.Title.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0 ||
                        b.Url.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0).Take(3))
                    {
                        string u = bm.Url;
                        AddOmniboxOfferRow("★  " + bm.Title, u, () =>
                        {
                            HideOmnibox();
                            if (_activeTab != null) _activeTab.Navigate(u);
                        });
                    }

                    foreach (var h in Store.History.Where(x =>
                        x.Title.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0 ||
                        x.Url.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0).Take(4))
                    {
                        string u = h.Url;
                        AddOmniboxOfferRow("🕘  " + h.Title, Address.DisplayHost(u), () =>
                        {
                            HideOmnibox();
                            if (_activeTab != null) _activeTab.Navigate(u);
                        });
                    }
                }
                else
                {
                    string currentHost = _activeTab != null ? Address.DisplayHost(_activeTab.Url) : "";
                    var logins = Store.GetLoginsForHost(currentHost);
                    foreach (var login in logins)
                    {
                        var capLogin = login;
                        AddOmniboxOfferRow("🔑  Fill saved password for " + login.User, login.Host, async () =>
                        {
                            HideOmnibox();
                            await FillSavedLoginAsync(capLogin);
                        });
                    }

                    foreach (var bm in Store.Bookmarks.Take(5))
                    {
                        string u = bm.Url;
                        AddOmniboxOfferRow("★  " + bm.Title, Address.DisplayHost(u), () =>
                        {
                            HideOmnibox();
                            if (_activeTab != null) _activeTab.Navigate(u);
                        });
                    }
                }
            }

            _omniboxOffersBorder.Visibility = _currentOfferActions.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            if (_currentOfferActions.Count > 0)
            {
                _selectedOfferIndex = 0;
                HighlightSelectedOffer();
            }
        }

        private void AddOmniboxOfferRow(string title, string subtitle, Action onPick)
        {
            var row = new Border
            {
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12, 8, 12, 8),
                Background = Brushes.Transparent,
                Cursor = Cursors.Hand
            };
            var dock = new DockPanel();
            var sub = new TextBlock
            {
                Text = subtitle,
                FontSize = 11.5,
                Foreground = Palette.Muted,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(12, 0, 0, 0)
            };
            DockPanel.SetDock(sub, Dock.Right);
            dock.Children.Add(sub);

            var main = new TextBlock
            {
                Text = title,
                FontSize = 13.5,
                Foreground = Palette.Ink,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            dock.Children.Add(main);
            row.Child = dock;

            int myIdx = _currentOfferActions.Count;
            _currentOfferActions.Add(onPick);
            row.MouseEnter += (s, e) =>
            {
                _selectedOfferIndex = myIdx;
                HighlightSelectedOffer();
            };
            row.MouseLeftButtonDown += (s, e) =>
            {
                e.Handled = true;
                onPick();
            };
            _omniboxOffersStack.Children.Add(row);
        }

        private void HighlightSelectedOffer()
        {
            for (int i = 0; i < _omniboxOffersStack.Children.Count; i++)
            {
                var b = _omniboxOffersStack.Children[i] as Border;
                if (b != null)
                {
                    b.Background = (i == _selectedOfferIndex) ? Palette.Wash : Brushes.Transparent;
                }
            }
        }

        private void OnOmniboxPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                HideOmnibox();
                e.Handled = true;
            }
            else if (e.Key == Key.Down)
            {
                if (_currentOfferActions.Count > 0)
                {
                    _selectedOfferIndex = (_selectedOfferIndex + 1) % _currentOfferActions.Count;
                    HighlightSelectedOffer();
                }
                e.Handled = true;
            }
            else if (e.Key == Key.Up)
            {
                if (_currentOfferActions.Count > 0)
                {
                    _selectedOfferIndex = (_selectedOfferIndex - 1 + _currentOfferActions.Count) % _currentOfferActions.Count;
                    HighlightSelectedOffer();
                }
                e.Handled = true;
            }
            else if (e.Key == Key.Enter)
            {
                if (_selectedOfferIndex >= 0 && _selectedOfferIndex < _currentOfferActions.Count)
                {
                    _currentOfferActions[_selectedOfferIndex]();
                }
                else if (!_omniboxIsTabSwitcherMode)
                {
                    string txt = _omniboxInput.Text;
                    bool isNew = _omniboxIsNewTabMode;
                    HideOmnibox();
                    if (isNew) CreateAndSelectTab(string.IsNullOrWhiteSpace(txt) ? "about:blank" : txt, false);
                    else if (_activeTab != null) _activeTab.Navigate(txt);
                }
                e.Handled = true;
            }
        }

        private async Task FillSavedLoginAsync(VaultLogin login)
        {
            if (_activeTab == null || !_activeTab.IsWebViewInitialized || _activeTab.WebView == null) return;
            string plain = Store.DecryptPassword(login);
            if (string.IsNullOrEmpty(plain)) return;
            string uEsc = login.User.Replace("\\", "\\\\").Replace("'", "\\'");
            string pEsc = plain.Replace("\\", "\\\\").Replace("'", "\\'");
            string script = @"
(function() {
  var pw = document.querySelector('input[type=""password""]');
  if (pw) {
    pw.focus();
    pw.value = '" + pEsc + @"';
    pw.dispatchEvent(new Event('input', {bubbles:true}));
    pw.dispatchEvent(new Event('change', {bubbles:true}));
  }
  var inputs = document.querySelectorAll('input[type=""text""], input[type=""email""], input:not([type])');
  for (var i = 0; i < inputs.length; i++) {
    if (inputs[i].offsetParent !== null) {
      inputs[i].value = '" + uEsc + @"';
      inputs[i].dispatchEvent(new Event('input', {bubbles:true}));
      inputs[i].dispatchEvent(new Event('change', {bubbles:true}));
      break;
    }
  }
})();";
            await _activeTab.WebView.CoreWebView2.ExecuteScriptAsync(script);
        }

        // KEYBOARD SHORTCUTS
        private async void OnWindowPreviewKeyDown(object sender, KeyEventArgs e)
        {
            var mods = Keyboard.Modifiers;
            bool ctrl = mods.HasFlag(ModifierKeys.Control);
            bool shift = mods.HasFlag(ModifierKeys.Shift);
            bool alt = mods.HasFlag(ModifierKeys.Alt);

            if (e.Key == Key.Escape)
            {
                if (_omniboxPopup != null && _omniboxPopup.IsOpen)
                {
                    HideOmnibox();
                    e.Handled = true;
                    return;
                }
                if (_peekOverlay.Visibility == Visibility.Visible)
                {
                    ClosePeekCard();
                    e.Handled = true;
                    return;
                }
                if (_extPopupOverlay.Visibility == Visibility.Visible)
                {
                    CloseExtensionPopup();
                    e.Handled = true;
                    return;
                }
                if (_drawerOverlay.Visibility == Visibility.Visible)
                {
                    _drawerOverlay.Visibility = Visibility.Collapsed;
                    e.Handled = true;
                    return;
                }
                if (_findBar.Visibility == Visibility.Visible)
                {
                    _findBar.Visibility = Visibility.Collapsed;
                    e.Handled = true;
                    return;
                }
                if (_activeTab != null && _activeTab.IsVeilPicking)
                {
                    StopVeilPicking();
                    e.Handled = true;
                    return;
                }
            }

            if (e.Key == Key.F5)
            {
                if (_activeTab != null && _activeTab.IsWebViewInitialized && _activeTab.WebView != null)
                {
                    if (ctrl || shift) await _activeTab.WebView.CoreWebView2.ExecuteScriptAsync("location.reload(true)");
                    else _activeTab.WebView.CoreWebView2.Reload();
                }
                e.Handled = true;
                return;
            }
            if (e.Key == Key.F6 || (alt && !ctrl && !shift && e.Key == Key.D))
            {
                ShowOmnibox(false, false);
                e.Handled = true;
                return;
            }
            if (e.Key == Key.F3)
            {
                _findBar.Visibility = Visibility.Visible;
                _findInput.Focus();
                e.Handled = true;
                return;
            }
            if (alt && !ctrl && !shift && (e.Key == Key.Left || e.Key == Key.Right))
            {
                if (_activeTab != null && _activeTab.WebView != null)
                {
                    if (e.Key == Key.Left && _activeTab.WebView.CanGoBack) _activeTab.WebView.GoBack();
                    else if (e.Key == Key.Right && _activeTab.WebView.CanGoForward) _activeTab.WebView.GoForward();
                }
                e.Handled = true;
                return;
            }
            if (alt && shift && !ctrl && e.Key == Key.N)
            {
                ToggleSplitView();
                e.Handled = true;
                return;
            }
            if (alt && shift && !ctrl && e.Key == Key.L)
            {
                OpenOrSelectSettingsTab("passwords");
                e.Handled = true;
                return;
            }
            if (alt && !ctrl && !shift && (e.Key >= Key.D1 && e.Key <= Key.D9))
            {
                int spIdx = (int)(e.Key - Key.D1);
                if (spIdx < Store.Spaces.Count)
                {
                    CycleSpace(spIdx);
                    e.Handled = true;
                    return;
                }
            }
            if (ctrl && alt && e.Key == Key.N)
            {
                ToggleSplitView();
                e.Handled = true;
                return;
            }
            if (ctrl && alt && e.Key == Key.L)
            {
                OpenOrSelectSettingsTab("passwords");
                e.Handled = true;
                return;
            }

            if (ctrl && !alt)
            {
                if (!shift && (e.Key >= Key.D1 && e.Key <= Key.D9))
                {
                    int idx = (int)(e.Key - Key.D1);
                    if (Store.Prefs.CtrlNumbersSwitchSpaces && idx < Store.Spaces.Count)
                    {
                        CycleSpace(idx);
                        e.Handled = true;
                        return;
                    }
                    var spaceTabs = GetTabsForActiveSpace();
                    if (idx == 8 && spaceTabs.Count > 0) idx = spaceTabs.Count - 1;
                    if (idx < spaceTabs.Count)
                    {
                        await SelectTabAsync(spaceTabs[idx]);
                        e.Handled = true;
                        return;
                    }
                }
                if (e.Key == Key.Tab || e.Key == Key.PageDown || e.Key == Key.PageUp)
                {
                    var spaceTabs = GetTabsForActiveSpace();
                    if (spaceTabs.Count > 1 && _activeTab != null)
                    {
                        bool prev = shift || e.Key == Key.PageUp;
                        int cur = spaceTabs.IndexOf(_activeTab);
                        int next = prev
                            ? (cur - 1 + spaceTabs.Count) % spaceTabs.Count
                            : (cur + 1) % spaceTabs.Count;
                        await SelectTabAsync(spaceTabs[next]);
                    }
                    e.Handled = true;
                    return;
                }

                string keyStr = KeyToShortcutString(e.Key);
                if (!string.IsNullOrEmpty(keyStr))
                {
                    bool handled = await HandleShortcutKeyAsync(keyStr, shift);
                    if (handled) e.Handled = true;
                }
            }
        }

        private static string KeyToShortcutString(Key k)
        {
            switch (k)
            {
                case Key.L: return "l";
                case Key.K: return "k";
                case Key.T: return "t";
                case Key.W: return "w";
                case Key.R: return "r";
                case Key.H: return "h";
                case Key.U: return "u";
                case Key.P: return "p";
                case Key.S: return "s";
                case Key.B: return "b";
                case Key.Y: return "y";
                case Key.J: return "j";
                case Key.D: return "d";
                case Key.N: return "n";
                case Key.F: return "f";
                case Key.Z: return "z";
                case Key.C: return "c";
                case Key.V: return "v";
                case Key.OemComma: return ",";
                case Key.OemOpenBrackets: return "[";
                case Key.OemCloseBrackets: return "]";
                case Key.OemPlus: return "=";
                case Key.Add: return "=";
                case Key.OemMinus: return "-";
                case Key.Subtract: return "-";
                case Key.D0: return "0";
                case Key.G: return "g";
                default: return "";
            }
        }

        private async Task<bool> HandleShortcutKeyAsync(string key, bool shift)
        {
            if (!shift)
            {
                switch (key)
                {
                    case "l":
                        ShowOmnibox(false, false);
                        return true;
                    case "k":
                        ShowOmnibox(true, false);
                        return true;
                    case "t":
                        ShowOmnibox(false, true);
                        return true;
                    case "w":
                        if (_activeTab != null) CloseTab(_activeTab);
                        return true;
                    case "n":
                        try { Process.Start(System.Reflection.Assembly.GetExecutingAssembly().Location); } catch { }
                        return true;
                    case "r":
                        if (_activeTab != null && _activeTab.IsWebViewInitialized && _activeTab.WebView != null)
                            _activeTab.WebView.CoreWebView2.Reload();
                        return true;
                    case "s":
                        Store.Prefs.SidebarCollapsed = !Store.Prefs.SidebarCollapsed;
                        Store.SavePrefs();
                        ApplyLayoutPreferences();
                        return true;
                    case "h":
                    case "y":
                        ShowHistoryDrawer();
                        return true;
                    case "j":
                        ShowDownloadsDrawer();
                        return true;
                    case "d":
                        if (_activeTab != null) CreateAndSelectTab(_activeTab.Url, false, _activeTab.IsPrivate, _activeTab.GroupId);
                        return true;
                    case "f":
                        _findBar.Visibility = Visibility.Visible;
                        _findInput.Focus();
                        _findInput.SelectAll();
                        return true;
                    case "g":
                        _findBar.Visibility = Visibility.Visible;
                        _findInput.Focus();
                        return true;
                    case "=":
                    case "+":
                        if (_activeTab != null && _activeTab.WebView != null)
                            _activeTab.WebView.ZoomFactor = Math.Min(3.0, _activeTab.WebView.ZoomFactor + 0.1);
                        return true;
                    case "-":
                        if (_activeTab != null && _activeTab.WebView != null)
                            _activeTab.WebView.ZoomFactor = Math.Max(0.25, _activeTab.WebView.ZoomFactor - 0.1);
                        return true;
                    case "0":
                        if (_activeTab != null && _activeTab.WebView != null)
                            _activeTab.WebView.ZoomFactor = 1.0;
                        return true;
                    case ",":
                        OpenOrSelectSettingsTab("general");
                        return true;
                    case "[":
                        if (_activeTab != null && _activeTab.WebView != null && _activeTab.WebView.CanGoBack)
                            _activeTab.WebView.GoBack();
                        return true;
                    case "]":
                        if (_activeTab != null && _activeTab.WebView != null && _activeTab.WebView.CanGoForward)
                            _activeTab.WebView.GoForward();
                        return true;
                    case "z":
                        if (_activeTab != null && _activeTab.IsVeilPicking)
                        {
                            string host = Address.DisplayHost(_activeTab.Url);
                            var veils = PageFeatures.GetVeils(host);
                            if (veils.Count > 0)
                            {
                                PageFeatures.RemoveVeil(host, veils[veils.Count - 1].Selector);
                                await _activeTab.ApplySiteStyleSheetsAsync();
                            }
                            return true;
                        }
                        if (Store.Prefs.CtrlZReopensClosedTab)
                        {
                            ReopenClosedTab();
                            return true;
                        }
                        break;
                }
            }
            else
            {
                switch (key)
                {
                    case "t":
                        ReopenClosedTab();
                        return true;
                    case "n":
                        CreateAndSelectTab("about:blank", true, true);
                        return true;
                    case "s":
                        Store.Prefs.TabLayout = string.Equals(Store.Prefs.TabLayout, "Top", StringComparison.OrdinalIgnoreCase) ? "Left" : "Top";
                        Store.SavePrefs();
                        ApplyLayoutPreferences();
                        return true;
                    case "r":
                        await ToggleReadingModeAsync();
                        return true;
                    case "h":
                        await ToggleVeilPickingAsync();
                        return true;
                    case "u":
                        ShowHiddenElementsDrawer();
                        return true;
                    case "p":
                        await TogglePictureInPictureAsync();
                        return true;
                    case "b":
                        ToggleBookmarkCurrentPage();
                        return true;
                    case "j":
                        ShowDownloadsDrawer();
                        return true;
                    case "d":
                        if (_activeTab != null)
                        {
                            string host = Address.DisplayHost(_activeTab.Url);
                            PageFeatures.ToggleDuskForSite(host);
                            await _activeTab.ApplySiteStyleSheetsAsync();
                        }
                        return true;
                    case "c":
                        if (_activeTab != null && !string.IsNullOrEmpty(_activeTab.Url))
                        {
                            Clipboard.SetText(CleanLink.Clean(_activeTab.Url));
                        }
                        return true;
                    case "v":
                        if (_activeTab != null && Clipboard.ContainsText())
                        {
                            string clip = Clipboard.GetText();
                            if (!string.IsNullOrWhiteSpace(clip))
                                _activeTab.Navigate(clip.Trim());
                        }
                        return true;
                }
            }
            return false;
        }

        private async Task ToggleReadingModeAsync()
        {
            if (_activeTab == null || !_activeTab.IsWebViewInitialized || _activeTab.WebView == null) return;
            await _activeTab.WebView.CoreWebView2.ExecuteScriptAsync(PageFeatures.GetReaderToggleScript(Palette.IsDark));
        }

        private async Task ToggleVeilPickingAsync()
        {
            if (_activeTab == null || !_activeTab.IsWebViewInitialized || _activeTab.WebView == null) return;
            if (_activeTab.IsVeilPicking)
            {
                StopVeilPicking();
            }
            else
            {
                _activeTab.IsVeilPicking = true;
                _veilBannerText.Text = "Point and click any banner, overlay, or clutter to hide it permanently on this site · ↑/↓ expand/narrow · Esc to finish";
                _veilBanner.Visibility = Visibility.Visible;
                await _activeTab.WebView.CoreWebView2.ExecuteScriptAsync("if(window.__officeVeil) window.__officeVeil.on();");
            }
        }

        private async void StopVeilPicking()
        {
            if (_activeTab == null) return;
            _activeTab.IsVeilPicking = false;
            _veilBanner.Visibility = Visibility.Collapsed;
            if (_activeTab.IsWebViewInitialized && _activeTab.WebView != null)
            {
                await _activeTab.WebView.CoreWebView2.ExecuteScriptAsync("if(window.__officeVeil) window.__officeVeil.off();");
            }
        }

        private async Task TogglePictureInPictureAsync()
        {
            if (_activeTab == null || !_activeTab.IsWebViewInitialized || _activeTab.WebView == null) return;
            await _activeTab.WebView.CoreWebView2.ExecuteScriptAsync(PageFeatures.PictureInPictureToggleScript);
        }

        private async Task ToggleTranslatePageAsync()
        {
            if (_activeTab == null || !_activeTab.IsWebViewInitialized || _activeTab.WebView == null) return;
            await _activeTab.WebView.CoreWebView2.ExecuteScriptAsync(PageFeatures.GetInPageTranslateScript(Store.Prefs.TranslateTargetLang));
        }

        private void ToggleBookmarkCurrentPage()
        {
            if (_activeTab == null || string.IsNullOrWhiteSpace(_activeTab.Url) || _activeTab.Url == "about:blank") return;
            var existing = Store.Bookmarks.FirstOrDefault(b => string.Equals(b.Url, _activeTab.Url, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                Store.Bookmarks.Remove(existing);
            }
            else
            {
                Store.Bookmarks.Insert(0, new BookmarkItem
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Title = _activeTab.DisplayTitle,
                    Url = _activeTab.Url,
                    DateIso = DateTime.UtcNow.ToString("o")
                });
            }
            Store.SaveBookmarks();
            FolderSync.PushToSyncFolder();
            ShowBookmarksDrawer();
        }

        // BENCH SERVER COMMAND HANDLER (Bench.swift parity for CLI & AI agents)
        public async Task<string> ExecuteBenchCommandAsync(string cmdLine)
        {
            return await await Dispatcher.InvokeAsync(async () =>
            {
                string[] parts = cmdLine.Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0) return "ERROR: empty command";
                string verb = parts[0].ToLowerInvariant();
                string arg = parts.Length > 1 ? parts[1].Trim() : "";

                if (verb == "open" || verb == "newtab")
                {
                    string u = string.IsNullOrEmpty(arg) ? "https://example.com" : arg;
                    if (u.StartsWith("search://settings", StringComparison.OrdinalIgnoreCase))
                    {
                        string sec = u.Length > "search://settings/".Length ? u.Substring("search://settings/".Length) : "general";
                        OpenOrSelectSettingsTab(sec);
                    }
                    else
                    {
                        var tab = new BrowserTab(u, Address.DisplayHost(u), false, false, Store.Prefs.ActiveSpaceId);
                        AttachTabEvents(tab);
                        _allTabs.Add(tab);
                        await SelectTabAsync(tab);
                        RefreshTabRows();
                    }
                }
                else if (verb == "go")
                {
                    if (_activeTab != null && !string.IsNullOrEmpty(arg))
                    {
                        _activeTab.Navigate(arg);
                    }
                }
                else if (verb == "eval")
                {
                    if (_activeTab != null && _activeTab.IsWebViewInitialized && _activeTab.WebView != null && !string.IsNullOrEmpty(arg))
                    {
                        return await _activeTab.WebView.CoreWebView2.ExecuteScriptAsync(arg);
                    }
                    return "ERROR: not initialized";
                }
                else if (verb == "probe")
                {
                    return "tabs=" + _allTabs.Count + " active=" + (_activeTab != null ? _activeTab.Url : "none") + " split=" + _isSplitView + " shieldBlocked=" + Shield.BlockedCountTotal;
                }
                else if (verb == "text")
                {
                    if (_activeTab != null && _activeTab.IsWebViewInitialized && _activeTab.WebView != null)
                    {
                        string res = await _activeTab.WebView.CoreWebView2.ExecuteScriptAsync("document.body ? document.body.innerText : ''");
                        return res;
                    }
                    return "";
                }
                else if (verb == "close" && arg == "all")
                {
                    while (_allTabs.Count > 1) CloseTab(_allTabs[_allTabs.Count - 1]);
                    return "OK";
                }
                return "OK: " + verb;
            });
        }

        // DRAWERS: Settings, Shield, Dusk, SiteStyles, Importer, Curtain, Bookmarks, History, Vault, Extensions, Downloads
        private void ShowMenuDrawer()
        {
            _drawerTitle.Text = "Search — Controls & Settings";
            _drawerContent.Children.Clear();
            _drawerOverlay.Visibility = Visibility.Visible;

            // Quick Actions row
            var quickPanel = new WrapPanel { Margin = new Thickness(0, 0, 0, 14) };
            quickPanel.Children.Add(CreatePillActionButton("📖 Reading Mode (Ctrl+Shift+R)", async () => { _drawerOverlay.Visibility = Visibility.Collapsed; await ToggleReadingModeAsync(); }));
            quickPanel.Children.Add(CreatePillActionButton("🪄 Hide Element (Ctrl+Shift+H)", async () => { _drawerOverlay.Visibility = Visibility.Collapsed; await ToggleVeilPickingAsync(); }));
            quickPanel.Children.Add(CreatePillActionButton("🌙 Dusk Site Dark Mode (Ctrl+Shift+D)", async () =>
            {
                if (_activeTab != null)
                {
                    PageFeatures.ToggleDuskForSite(Address.DisplayHost(_activeTab.Url));
                    await _activeTab.ApplySiteStyleSheetsAsync();
                }
                _drawerOverlay.Visibility = Visibility.Collapsed;
            }));
            quickPanel.Children.Add(CreatePillActionButton("🌐 Translate Page", async () => { _drawerOverlay.Visibility = Visibility.Collapsed; await ToggleTranslatePageAsync(); }));
            quickPanel.Children.Add(CreatePillActionButton("🎨 Custom Site CSS", () => ShowSiteStylesDrawer()));
            quickPanel.Children.Add(CreatePillActionButton("📺 Float Video PiP (Ctrl+Shift+P)", async () => { _drawerOverlay.Visibility = Visibility.Collapsed; await TogglePictureInPictureAsync(); }));
            quickPanel.Children.Add(CreatePillActionButton("🔗 Copy Clean Link (Ctrl+Shift+C)", () =>
            {
                if (_activeTab != null) Clipboard.SetText(CleanLink.Clean(_activeTab.Url));
                _drawerOverlay.Visibility = Visibility.Collapsed;
            }));
            quickPanel.Children.Add(CreatePillActionButton("◫ Split View (Alt+Ctrl+N)", () => { _drawerOverlay.Visibility = Visibility.Collapsed; ToggleSplitView(); }));
            quickPanel.Children.Add(CreatePillActionButton("🕶 Private Tab (Ctrl+Shift+N)", () => { _drawerOverlay.Visibility = Visibility.Collapsed; CreateAndSelectTab("about:blank", true, true); }));
            _drawerContent.Children.Add(quickPanel);

            // Navigation to dedicated panels
            var panelsRow = new WrapPanel { Margin = new Thickness(0, 0, 0, 16) };
            panelsRow.Children.Add(CreatePillActionButton("★ Bookmarks (" + Store.Bookmarks.Count + ")", () => ShowBookmarksDrawer()));
            panelsRow.Children.Add(CreatePillActionButton("🕘 History (" + Store.History.Count + ")", () => ShowHistoryDrawer()));
            panelsRow.Children.Add(CreatePillActionButton("🔑 Passwords Vault (" + Store.Logins.Count + ")", () => ShowVaultDrawer()));
            panelsRow.Children.Add(CreatePillActionButton("🧩 Chrome Extensions (" + Store.Extensions.Count + ")", () => ShowExtensionsDrawer()));
            panelsRow.Children.Add(CreatePillActionButton("🙈 Hidden Clutter", () => ShowHiddenElementsDrawer()));
            panelsRow.Children.Add(CreatePillActionButton("📥 1-Click Browser Import", () => ShowImportDrawer()));
            panelsRow.Children.Add(CreatePillActionButton("⬇ Downloads (" + Downloads.Count + ")", () => ShowDownloadsDrawer()));
            _drawerContent.Children.Add(panelsRow);

            // Shield Ad & Tracker Blocker section
            string currentHost = _activeTab != null ? Address.DisplayHost(_activeTab.Url) : "";
            bool pausedHere = Shield.IsPaused(currentHost);
            AddSectionHeader("Shield — Network Ad & Tracker Blocker");
            AddSettingRow(
                "Block third-party ads, trackers & YouTube ads (" + Shield.BlockedCountTotal + " blocked this session)",
                Store.Prefs.ShieldEnabled ? "Enabled" : "Disabled",
                () =>
                {
                    Store.Prefs.ShieldEnabled = !Store.Prefs.ShieldEnabled;
                    Store.SavePrefs();
                    ShowMenuDrawer();
                });

            if (!string.IsNullOrEmpty(currentHost))
            {
                AddSettingRow(
                    "Shield on " + currentHost,
                    pausedHere ? "Paused on this site" : "Active on this site",
                    () =>
                    {
                        Shield.TogglePause(currentHost);
                        if (_activeTab != null && _activeTab.WebView != null && _activeTab.IsWebViewInitialized)
                            _activeTab.WebView.CoreWebView2.Reload();
                        ShowMenuDrawer();
                    });
            }

            // Appearance, Dusk & Layout section
            AddSectionHeader("Look, Dusk & Layout");
            AddSettingRow("Appearance (Light, Dark, System)", Store.Prefs.Appearance, () =>
            {
                string cur = Store.Prefs.Appearance;
                Store.Prefs.Appearance = cur == "System" ? "Light" : (cur == "Light" ? "Dark" : "System");
                Store.SavePrefs();
                ApplyLayoutPreferences();
                ShowMenuDrawer();
            });
            AddSettingRow("Dusk — Automatic Dark Mode for light websites (Ctrl+Shift+D per site)", Store.Prefs.DuskEnabled ? "Enabled" : "Disabled", () =>
            {
                Store.Prefs.DuskEnabled = !Store.Prefs.DuskEnabled;
                Store.SavePrefs();
                ApplyLayoutPreferences();
                ShowMenuDrawer();
            });
            AddSettingRow("Link Peek on Shift+Click & External Pinned Links", Store.Prefs.PeekEnabled ? "Enabled" : "Disabled", () =>
            {
                Store.Prefs.PeekEnabled = !Store.Prefs.PeekEnabled;
                Store.Prefs.PinsOpenExternalInPeek = Store.Prefs.PeekEnabled;
                Store.SavePrefs();
                ShowMenuDrawer();
            });
            AddSettingRow("Tab Bar Position (Ctrl+Shift+S)", Store.Prefs.TabLayout == "Top" ? "Across the Top" : "Down the Left", () =>
            {
                Store.Prefs.TabLayout = Store.Prefs.TabLayout == "Top" ? "Left" : "Top";
                Store.SavePrefs();
                ApplyLayoutPreferences();
                ShowMenuDrawer();
            });
            AddSettingRow("Blank Tab Wall Theme", Store.Prefs.BlankWallTheme, () =>
            {
                string[] walls = new[] { "Clean", "Dusk Gradient", "Warm Paper", "Forest Mist" };
                int idx = Array.IndexOf(walls, Store.Prefs.BlankWallTheme);
                Store.Prefs.BlankWallTheme = walls[(idx + 1) % walls.Length];
                Store.SavePrefs();
                if (_activeTab != null && _activeTab.Url == "about:blank") _activeTab.RenderBlankWall();
                ShowMenuDrawer();
            });
            AddSettingRow("Search Engine", Store.Prefs.SearchEngineName, () =>
            {
                if (Store.Prefs.SearchEngineName == "DuckDuckGo")
                {
                    Store.Prefs.SearchEngineName = "Google";
                    Store.Prefs.SearchEngineUrl = "https://www.google.com/search?q=";
                }
                else if (Store.Prefs.SearchEngineName == "Google")
                {
                    Store.Prefs.SearchEngineName = "Brave Search";
                    Store.Prefs.SearchEngineUrl = "https://search.brave.com/search?q=";
                }
                else
                {
                    Store.Prefs.SearchEngineName = "DuckDuckGo";
                    Store.Prefs.SearchEngineUrl = "https://duckduckgo.com/?q=";
                }
                Store.SavePrefs();
                ShowMenuDrawer();
            });

            // Memory, Sync & Default Browser section
            int awakeCount = _allTabs.Count(t => !t.IsSleeping);
            long memMb = Process.GetCurrentProcess().WorkingSet64 / (1024 * 1024);
            AddSectionHeader("Default Browser, Mac/PC Folder Sync & Memory (" + awakeCount + " of " + _allTabs.Count + " tabs awake · App shell " + memMb + " MB)");
            bool isDef = DefaultBrowserRegistrar.IsDefaultBrowser();
            AddSettingRow(
                "Default Web Browser in Windows 11 (http, https, .html, .svg, .webp)",
                isDef ? "✓ Search is your Default Browser" : "Make Search Default Browser →",
                () =>
                {
                    DefaultBrowserRegistrar.OpenWindowsDefaultAppsSettings();
                });

            string syncPath = string.IsNullOrWhiteSpace(Store.Prefs.SyncFolderPath) ? "Off (Click to choose OneDrive / Dropbox / iCloud folder)" : Store.Prefs.SyncFolderPath;
            AddSettingRow(
                "Search Sync Folder (Syncs Spaces, Pins & Bookmarks with Search on Mac / Oriel)",
                syncPath,
                () =>
                {
                    using (var fbd = new System.Windows.Forms.FolderBrowserDialog())
                    {
                        fbd.Description = "Choose a shared folder (e.g. inside OneDrive, Dropbox, or iCloud Drive) to sync Spaces, Pins, and Bookmarks across your computers:";
                        if (fbd.ShowDialog() == System.Windows.Forms.DialogResult.OK && !string.IsNullOrWhiteSpace(fbd.SelectedPath))
                        {
                            Store.Prefs.SyncFolderPath = fbd.SelectedPath;
                            Store.SavePrefs();
                            FolderSync.InitializeWatcher(() => Dispatcher.InvokeAsync(() =>
                            {
                                RefreshSpaceHeader();
                                RefreshTabRows();
                            }));
                            ShowMenuDrawer();
                        }
                    }
                });

            AddSettingRow("Protect saved Passwords in Vault with Windows Security / Windows Hello", Store.Prefs.WindowsHelloForVault ? "Enabled" : "Disabled", () =>
            {
                Store.Prefs.WindowsHelloForVault = !Store.Prefs.WindowsHelloForVault;
                Store.SavePrefs();
                ShowMenuDrawer();
            });
            AddSettingRow("Auto-sleep inactive background tabs after", Store.Prefs.AutoSleepMinutes + " minutes", () =>
            {
                int[] choices = new[] { 5, 15, 30, 60 };
                int idx = Array.IndexOf(choices, Store.Prefs.AutoSleepMinutes);
                Store.Prefs.AutoSleepMinutes = choices[(idx + 1) % choices.Length];
                Store.SavePrefs();
                ShowMenuDrawer();
            });
            AddSettingRow("Sleep all background tabs right now", "Free RAM", () =>
            {
                foreach (var t in _allTabs)
                {
                    if (t != _activeTab && t != _splitLeftTab && t != _splitRightTab && !t.KeepAwake)
                        t.PutToSleep();
                }
                RefreshTabRows();
                ShowMenuDrawer();
            });
            AddSettingRow("Let scripts & coding agents drive Search (./bench pipe)", Store.Prefs.BenchServerEnabled ? "Listening on \\\\.\\pipe\\search-bench" : "Off", () =>
            {
                Store.Prefs.BenchServerEnabled = !Store.Prefs.BenchServerEnabled;
                Store.SavePrefs();
                if (Store.Prefs.BenchServerEnabled) BenchServer.Start(this);
                else BenchServer.Stop();
                ShowMenuDrawer();
            });
        }

        private void ShowSiteStylesDrawer()
        {
            string host = _activeTab != null ? Address.DisplayHost(_activeTab.Url) : "";
            _drawerTitle.Text = "Site Styles — Custom CSS Boost" + (string.IsNullOrEmpty(host) ? "" : " for " + host);
            _drawerContent.Children.Clear();
            _drawerOverlay.Visibility = Visibility.Visible;

            if (string.IsNullOrEmpty(host))
            {
                _drawerContent.Children.Add(CreateMutedNote("Open any website first to write custom CSS for it."));
                return;
            }

            _drawerContent.Children.Add(CreateMutedNote("Write custom CSS rules for " + host + ". Applied live as you type and injected before the first frame on every future visit:"));
            var cssBox = new TextBox
            {
                Text = PageFeatures.GetSiteStyleCss(host),
                AcceptsReturn = true,
                Height = 180,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12.5,
                Padding = new Thickness(10),
                Background = Palette.Ground,
                Foreground = Palette.Ink,
                CaretBrush = Palette.Ink,
                BorderBrush = Palette.Hairline,
                BorderThickness = new Thickness(1),
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };
            cssBox.TextChanged += async (s, e) =>
            {
                PageFeatures.SetSiteStyleCss(host, cssBox.Text);
                if (_activeTab != null) await _activeTab.ApplySiteStyleSheetsAsync();
            };
            _drawerContent.Children.Add(cssBox);

            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 0) };
            row.Children.Add(CreatePillActionButton("Clear Site CSS", async () =>
            {
                cssBox.Text = "";
                PageFeatures.SetSiteStyleCss(host, "");
                if (_activeTab != null) await _activeTab.ApplySiteStyleSheetsAsync();
            }));
            row.Children.Add(CreatePillActionButton("← Back to Settings", () => ShowMenuDrawer()));
            _drawerContent.Children.Add(row);
        }

        private void ShowImportDrawer()
        {
            _drawerTitle.Text = "Bring Your Bookmarks & Passwords into Search";
            _drawerContent.Children.Clear();
            _drawerOverlay.Visibility = Visibility.Visible;

            var statusNote = CreateMutedNote("Import bookmarks directly from Chrome, Edge, Brave, Arc, or Opera in one click — nothing leaves your PC.");
            _drawerContent.Children.Add(statusNote);

            var btnRow = new WrapPanel { Margin = new Thickness(0, 8, 0, 14) };
            btnRow.Children.Add(CreatePillActionButton("📥 1-Click Import Bookmarks from Installed Browsers", () =>
            {
                var results = BrowserImporter.ImportFromInstalledBrowsers();
                if (results.Count == 0)
                {
                    statusNote.Text = "No new bookmarks found (or all bookmarks were already imported).";
                }
                else
                {
                    statusNote.Text = "Imported: " + string.Join(", ", results.Select(r => r.BookmarksImported + " bookmarks from " + r.BrowserName)) + "!";
                }
            }));

            btnRow.Children.Add(CreatePillActionButton("🔑 Import Passwords from CSV File...", () =>
            {
                var dlg = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "CSV Password Export (*.csv)|*.csv|All Files (*.*)|*.*",
                    Title = "Select Exported Passwords CSV (Chrome / Edge / Brave / 1Password / Bitwarden)"
                };
                if (dlg.ShowDialog() == true)
                {
                    int count = BrowserImporter.ImportPasswordsFromCsv(dlg.FileName);
                    statusNote.Text = "Encrypted & imported " + count + " logins into Windows DPAPI Vault!";
                }
            }));
            btnRow.Children.Add(CreatePillActionButton("← Back to Settings", () => ShowMenuDrawer()));
            _drawerContent.Children.Add(btnRow);
        }

        private void ShowBookmarksDrawer()
        {
            _drawerTitle.Text = "Bookmarks (Ctrl+Shift+B to bookmark current page)";
            _drawerContent.Children.Clear();
            _drawerOverlay.Visibility = Visibility.Visible;

            var topBar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
            topBar.Children.Add(CreatePillActionButton("+ Bookmark Current Page", () => ToggleBookmarkCurrentPage()));
            topBar.Children.Add(CreatePillActionButton("📥 Import from Chrome / Edge / Brave", () => ShowImportDrawer()));
            topBar.Children.Add(CreatePillActionButton("← Back", () => ShowMenuDrawer()));
            _drawerContent.Children.Add(topBar);

            if (Store.Bookmarks.Count == 0)
            {
                _drawerContent.Children.Add(CreateMutedNote("No bookmarks yet. Press Ctrl+Shift+B on any page or click Import above."));
                return;
            }

            foreach (var bm in Store.Bookmarks.ToList())
            {
                var captured = bm;
                AddListRow(bm.Title, bm.Url, "Open", () =>
                {
                    _drawerOverlay.Visibility = Visibility.Collapsed;
                    if (_activeTab != null) _activeTab.Navigate(captured.Url);
                }, "Remove", () =>
                {
                    Store.Bookmarks.Remove(captured);
                    Store.SaveBookmarks();
                    ShowBookmarksDrawer();
                });
            }
        }

        private void ShowHistoryDrawer()
        {
            _drawerTitle.Text = "History (Local only — never leaves your PC)";
            _drawerContent.Children.Clear();
            _drawerOverlay.Visibility = Visibility.Visible;

            var topBar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
            topBar.Children.Add(CreatePillActionButton("Clear All History", () =>
            {
                Store.History.Clear();
                Store.SaveHistory();
                ShowHistoryDrawer();
            }));
            topBar.Children.Add(CreatePillActionButton("← Back to Settings", () => ShowMenuDrawer()));
            _drawerContent.Children.Add(topBar);

            if (Store.History.Count == 0)
            {
                _drawerContent.Children.Add(CreateMutedNote("Your browsing history is empty."));
                return;
            }

            foreach (var h in Store.History.Take(100))
            {
                var captured = h;
                AddListRow(h.Title, h.Url, "Open", () =>
                {
                    _drawerOverlay.Visibility = Visibility.Collapsed;
                    if (_activeTab != null) _activeTab.Navigate(captured.Url);
                });
            }
        }

        private void ShowHiddenElementsDrawer()
        {
            string host = _activeTab != null ? Address.DisplayHost(_activeTab.Url) : "";
            _drawerTitle.Text = "Curtain — Hidden Clutter" + (string.IsNullOrEmpty(host) ? "" : " on " + host);
            _drawerContent.Children.Clear();
            _drawerOverlay.Visibility = Visibility.Visible;

            var topBar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
            topBar.Children.Add(CreatePillActionButton("🪄 Point & Hide Element on Page (Ctrl+Shift+H)", async () =>
            {
                _drawerOverlay.Visibility = Visibility.Collapsed;
                await ToggleVeilPickingAsync();
            }));
            if (!string.IsNullOrEmpty(host) && PageFeatures.GetVeils(host).Count > 0)
            {
                topBar.Children.Add(CreatePillActionButton("Restore All on " + host, async () =>
                {
                    PageFeatures.RestoreAllVeils(host);
                    if (_activeTab != null) await _activeTab.ApplySiteStyleSheetsAsync();
                    ShowHiddenElementsDrawer();
                }));
            }
            topBar.Children.Add(CreatePillActionButton("← Back", () => ShowMenuDrawer()));
            _drawerContent.Children.Add(topBar);

            var veils = PageFeatures.GetVeils(host);
            if (veils.Count == 0)
            {
                _drawerContent.Children.Add(CreateMutedNote("Nothing hidden on this site yet. Press Ctrl+Shift+H and click any cookie banner, overlay, or sidebar to vanish it permanently."));
                return;
            }

            foreach (var v in veils.ToList())
            {
                var captured = v;
                AddListRow(v.Label, v.Selector + (string.IsNullOrEmpty(v.Note) ? "" : "  (" + v.Note + ")"), "Restore", async () =>
                {
                    PageFeatures.RemoveVeil(host, captured.Selector);
                    if (_activeTab != null) await _activeTab.ApplySiteStyleSheetsAsync();
                    ShowHiddenElementsDrawer();
                });
            }
        }

        private void ShowVaultDrawer()
        {
            _drawerTitle.Text = "Vault — Encrypted Local Passwords (DPAPI)";
            _drawerContent.Children.Clear();
            _drawerOverlay.Visibility = Visibility.Visible;

            var addBox = new Border
            {
                Background = Palette.Wash,
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12),
                Margin = new Thickness(0, 0, 0, 14)
            };
            var addStack = new StackPanel();
            addStack.Children.Add(new TextBlock
            {
                Text = "Save an Account to Windows Encrypted Vault (Offered automatically under password fields on the site)",
                FontSize = 12.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = Palette.Ink,
                Margin = new Thickness(0, 0, 0, 8)
            });

            var fieldsGrid = new Grid();
            fieldsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            fieldsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            fieldsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            fieldsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            string defaultHost = _activeTab != null ? Address.DisplayHost(_activeTab.Url) : "example.com";
            var hostBox = CreateDrawerTextBox(string.IsNullOrEmpty(defaultHost) ? "example.com" : defaultHost);
            var userBox = CreateDrawerTextBox("username@email.com");
            var passBox = CreateDrawerTextBox("password");
            Grid.SetColumn(hostBox, 0);
            Grid.SetColumn(userBox, 1);
            Grid.SetColumn(passBox, 2);
            fieldsGrid.Children.Add(hostBox);
            fieldsGrid.Children.Add(userBox);
            fieldsGrid.Children.Add(passBox);

            var saveBtn = CreatePillActionButton("Save", async () =>
            {
                Store.SavePassword(hostBox.Text, userBox.Text, passBox.Text);
                if (_activeTab != null) await _activeTab.ApplySiteStyleSheetsAsync();
                ShowVaultDrawer();
            });
            Grid.SetColumn(saveBtn, 3);
            fieldsGrid.Children.Add(saveBtn);
            addStack.Children.Add(fieldsGrid);
            addBox.Child = addStack;
            _drawerContent.Children.Add(addBox);

            if (Store.Logins.Count == 0)
            {
                _drawerContent.Children.Add(CreateMutedNote("No saved passwords yet. Passwords are encrypted with your Windows user account (CryptProtectData) so only you can read them."));
                return;
            }

            foreach (var login in Store.Logins.ToList())
            {
                var cap = login;
                AddListRow(login.Host + "  —  " + login.User, "Encrypted with Windows DPAPI · Protected by Windows Security", "Verify & Fill / Copy", async () =>
                {
                    if (Store.Prefs.WindowsHelloForVault && !WindowsHelloVerifier.VerifyUser(this))
                        return;
                    string dec = Store.DecryptPassword(cap);
                    if (!string.IsNullOrEmpty(dec))
                    {
                        Clipboard.SetText(dec);
                        await FillSavedLoginAsync(cap);
                        _drawerOverlay.Visibility = Visibility.Collapsed;
                    }
                }, "Delete", () =>
                {
                    if (Store.Prefs.WindowsHelloForVault && !WindowsHelloVerifier.VerifyUser(this, "Verify your identity to delete a saved password."))
                        return;
                    Store.Logins.Remove(cap);
                    Store.SaveVault();
                    ShowVaultDrawer();
                });
            }
        }

        private void ShowExtensionsDrawer()
        {
            _drawerTitle.Text = "Chrome Extensions — without Chrome";
            _drawerContent.Children.Clear();
            _drawerOverlay.Visibility = Visibility.Visible;

            var installBox = new Border
            {
                Background = Palette.Wash,
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12),
                Margin = new Thickness(0, 0, 0, 14)
            };
            var stack = new StackPanel();
            stack.Children.Add(new TextBlock
            {
                Text = "Paste a Chrome Web Store link, 32-letter extension ID, or local unpacked folder path (or browse the Chrome Web Store directly and click 'Add to Chrome'):",
                FontSize = 12.5,
                Foreground = Palette.Ink,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 8)
            });

            var inputDock = new DockPanel();
            var statusNote = CreateMutedNote("");
            var urlBox = CreateDrawerTextBox("https://chromewebstore.google.com/detail/...");

            var addBtn = CreatePillActionButton("Install Extension", async () =>
            {
                string raw = (urlBox.Text ?? "").Trim();
                if (string.IsNullOrEmpty(raw)) return;
                statusNote.Text = "Fetching & unpacking extension...";
                try
                {
                    ExtensionInfo info;
                    if (Directory.Exists(raw))
                        info = CrxInstaller.RegisterUnpackedFolder(raw);
                    else
                        info = await CrxInstaller.InstallFromWebStoreAsync(raw);

                    if (_activeTab != null && _activeTab.IsWebViewInitialized && _activeTab.WebView != null)
                    {
                        var method = _activeTab.WebView.CoreWebView2.Profile.GetType().GetMethod("AddBrowserExtensionAsync");
                        if (method != null)
                        {
                            dynamic task = method.Invoke(_activeTab.WebView.CoreWebView2.Profile, new object[] { info.FolderPath });
                            if (task != null) await task;
                        }
                    }
                    ShowExtensionsDrawer();
                }
                catch (Exception ex)
                {
                    statusNote.Text = "Error: " + ex.Message;
                }
            });
            DockPanel.SetDock(addBtn, Dock.Right);
            inputDock.Children.Add(addBtn);
            inputDock.Children.Add(urlBox);
            stack.Children.Add(inputDock);

            var browseStoreBtn = CreatePillActionButton("🌐 Open Chrome Web Store", () =>
            {
                _drawerOverlay.Visibility = Visibility.Collapsed;
                if (_activeTab != null) _activeTab.Navigate("https://chromewebstore.google.com");
            });
            browseStoreBtn.Margin = new Thickness(0, 8, 0, 0);
            stack.Children.Add(browseStoreBtn);

            stack.Children.Add(statusNote);
            installBox.Child = stack;
            _drawerContent.Children.Add(installBox);

            if (Store.Extensions.Count == 0)
            {
                _drawerContent.Children.Add(CreateMutedNote("No extensions installed yet. Remember: ad-blocking (Shield), hiding clutter (Curtain), Dark Mode (Dusk), Reading Mode, Picture-in-Picture, and Passwords are already built in natively."));
                return;
            }

            foreach (var ext in Store.Extensions.ToList())
            {
                var cap = ext;
                AddListRow(ext.Name + "  (v" + ext.Version + ")", ext.Id + " · " + ext.FolderPath,
                    "Open Popup", () => OpenExtensionPopup(cap),
                    "Remove", () =>
                    {
                        Store.Extensions.Remove(cap);
                        Store.SaveExtensions();
                        ShowExtensionsDrawer();
                    });
            }
        }

        private void ShowDownloadsDrawer()
        {
            _drawerTitle.Text = "Downloads (Ctrl+Shift+J)";
            _drawerContent.Children.Clear();
            _drawerOverlay.Visibility = Visibility.Visible;

            if (Downloads.Count == 0)
            {
                _drawerContent.Children.Add(CreateMutedNote("No downloads in this session yet."));
                return;
            }

            foreach (var d in Downloads)
            {
                var cap = d;
                string progress = d.TotalBytes > 0
                    ? (d.ReceivedBytes / 1024) + " KB / " + (d.TotalBytes / 1024) + " KB"
                    : (d.ReceivedBytes / 1024) + " KB";
                AddListRow(d.FileName, d.State + " · " + progress, "Show in Explorer", () =>
                {
                    if (File.Exists(cap.ResultPath))
                        Process.Start("explorer.exe", "/select,\"" + cap.ResultPath + "\"");
                });
            }
        }

        // UI Helpers
        private void AddSectionHeader(string title)
        {
            _drawerContent.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = Palette.Muted,
                Margin = new Thickness(2, 12, 0, 6)
            });
        }

        private void AddSettingRow(string label, string valueText, Action onToggle)
        {
            var border = new Border
            {
                Background = Palette.Wash,
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12, 9, 12, 9),
                Margin = new Thickness(0, 3, 0, 3),
                Cursor = Cursors.Hand
            };
            var dock = new DockPanel();
            var val = new TextBlock
            {
                Text = valueText,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = Palette.Ink,
                VerticalAlignment = VerticalAlignment.Center
            };
            DockPanel.SetDock(val, Dock.Right);
            dock.Children.Add(val);

            var lbl = new TextBlock
            {
                Text = label,
                FontSize = 13,
                Foreground = Palette.Ink,
                VerticalAlignment = VerticalAlignment.Center
            };
            dock.Children.Add(lbl);
            border.Child = dock;
            border.MouseLeftButtonDown += (s, e) => onToggle();
            _drawerContent.Children.Add(border);
        }

        private void AddListRow(string title, string subtitle, string btn1Text, Action btn1Action, string btn2Text = null, Action btn2Action = null)
        {
            var border = new Border
            {
                Background = Palette.Wash,
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12, 8, 12, 8),
                Margin = new Thickness(0, 3, 0, 3)
            };
            var dock = new DockPanel();
            var btns = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            btns.Children.Add(CreateQuietButton(btn1Text, btn1Text, btn1Action));
            if (!string.IsNullOrEmpty(btn2Text) && btn2Action != null)
                btns.Children.Add(CreateQuietButton(btn2Text, btn2Text, btn2Action));
            DockPanel.SetDock(btns, Dock.Right);
            dock.Children.Add(btns);

            var stack = new StackPanel();
            stack.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 13,
                FontWeight = FontWeights.Medium,
                Foreground = Palette.Ink,
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            stack.Children.Add(new TextBlock
            {
                Text = subtitle,
                FontSize = 11.5,
                Foreground = Palette.Muted,
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            dock.Children.Add(stack);
            border.Child = dock;
            _drawerContent.Children.Add(border);
        }

        private TextBox CreateDrawerTextBox(string initialText)
        {
            return new TextBox
            {
                Text = initialText,
                FontSize = 12.5,
                Padding = new Thickness(8, 5, 8, 5),
                Margin = new Thickness(0, 0, 6, 0),
                Background = Palette.Ground,
                Foreground = Palette.Ink,
                CaretBrush = Palette.Ink,
                BorderBrush = Palette.Hairline,
                BorderThickness = new Thickness(1)
            };
        }

        private TextBlock CreateMutedNote(string text)
        {
            return new TextBlock
            {
                Text = text,
                FontSize = 12.5,
                Foreground = Palette.Muted,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(2, 6, 2, 6)
            };
        }

        private Border CreatePillActionButton(string text, Action onClick)
        {
            var b = new Border
            {
                Background = Palette.Wash,
                BorderBrush = Palette.Hairline,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(7),
                Padding = new Thickness(10, 6, 10, 6),
                Margin = new Thickness(0, 0, 6, 6),
                Cursor = Cursors.Hand,
                Child = new TextBlock
                {
                    Text = text,
                    FontSize = 12.5,
                    FontWeight = FontWeights.Medium,
                    Foreground = Palette.Ink
                }
            };
            b.MouseEnter += (s, e) => b.Background = Palette.Hover;
            b.MouseLeave += (s, e) => b.Background = Palette.Wash;
            b.MouseLeftButtonDown += (s, e) =>
            {
                e.Handled = true;
                onClick();
            };
            return b;
        }

        private Border CreateQuietButton(string text, string tooltip, Action onClick)
        {
            var b = new Border
            {
                Background = Brushes.Transparent,
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(8, 5, 8, 5),
                Margin = new Thickness(2, 0, 2, 0),
                Cursor = Cursors.Hand,
                ToolTip = tooltip,
                Child = new TextBlock
                {
                    Text = text,
                    FontSize = 12,
                    Foreground = Palette.Muted,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            WindowChrome.SetIsHitTestVisibleInChrome(b, true);
            b.MouseEnter += (s, e) => b.Background = Palette.Hover;
            b.MouseLeave += (s, e) => b.Background = Brushes.Transparent;
            b.MouseLeftButtonDown += (s, e) =>
            {
                e.Handled = true;
                onClick();
            };
            return b;
        }

        private Border CreateVectorIconButton(string pathData, string tooltip, Action onClick)
        {
            var b = new Border
            {
                Width = 26,
                Height = 26,
                CornerRadius = new CornerRadius(7),
                Background = Brushes.Transparent,
                Margin = new Thickness(1, 0, 1, 0),
                Cursor = Cursors.Hand,
                ToolTip = tooltip,
                Child = VectorIcons.Draw(pathData, Palette.Muted, 13, 1.45)
            };
            WindowChrome.SetIsHitTestVisibleInChrome(b, true);
            b.MouseEnter += (s, e) => b.Background = Palette.Hover;
            b.MouseLeave += (s, e) => b.Background = Brushes.Transparent;
            b.MouseLeftButtonDown += (s, e) =>
            {
                e.Handled = true;
                onClick();
            };
            return b;
        }

        private Border CreateIconButton(string icon, string tooltip, Action onClick)
        {
            var b = new Border
            {
                Width = 26,
                Height = 26,
                CornerRadius = new CornerRadius(6),
                Background = Brushes.Transparent,
                Margin = new Thickness(1, 0, 1, 0),
                Cursor = Cursors.Hand,
                ToolTip = tooltip,
                Child = new TextBlock
                {
                    Text = icon,
                    FontSize = 12.5,
                    Foreground = Palette.Muted,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            WindowChrome.SetIsHitTestVisibleInChrome(b, true);
            b.MouseEnter += (s, e) => b.Background = Palette.Hover;
            b.MouseLeave += (s, e) => b.Background = Brushes.Transparent;
            b.MouseLeftButtonDown += (s, e) =>
            {
                e.Handled = true;
                onClick();
            };
            return b;
        }
    }
}
