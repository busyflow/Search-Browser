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
        private readonly List<BrowserTab> _mruTabs = new List<BrowserTab>();
        private readonly Stack<SavedTabState> _closedTabs = new Stack<SavedTabState>();

        private BrowserTab _activeTab;
        private BrowserTab _splitLeftTab;
        private BrowserTab _splitRightTab;
        private bool _isSplitView;
        private BrowserTab _draggedTab;
        private bool _isFullScreen;
        private WindowState _preFullScreenState = WindowState.Normal;
        private DateTime _lastFullScreenEscUtc = DateTime.MinValue;
        private bool _isCtrlHeld;

        // Root layout elements
        private Grid _rootGrid;
        private Border _sidebarBorder;
        private Border _sidebarResizeGrip;
        private bool _isResizingSidebar;
        private Point _resizeStartScreen;
        private double _resizeStartWidth;
        private Border _sidebarHoverHotzone;
        private Border _floatingSidebarBorder;
        private Border _topTabBarBorder;
        private StackPanel _topRightBtns;
        private Border _pageCardBorder;
        private Border _loadingLine;
        private Grid _stageGrid;
        private Border _leftPaneHost;
        private Border _rightPaneHost;
        private GridSplitter _splitDivider;
        private System.Windows.Controls.Primitives.Popup _splitClosePopup;

        // Sidebar / Tab bar elements
        private WrapPanel _pinsPanel;
        private DockPanel _pinsDivider;
        private BrowserTab _carryTab;
        private UIElement _carrySource;
        private Point _carryStartScreen;
        private bool _isCarrying;
        private bool _carryOverPins;
        private bool _carryOverTabs;
        private bool _carryOverStage;
        private int _carryDropIndex = -1;
        private System.Windows.Controls.Primitives.Popup _carryPopup;
        private Border _carryVisual;
        private Border _pinGhost;
        private Border _rowGhost;
        private string _justPinnedTabId;
        private Grid _tabsContainerGrid;
        private Border _glidingTabPill;
        private TranslateTransform _glidingPillTransform;
        private StackPanel _tabsStack;
        private StackPanel _topTabsStack;
        private Border _sidebarFoldBtn;
        private Border _backBtn;
        private Border _forwardBtn;
        private Border _reloadBtn;
        private Border _topExtDoor;
        private Border _topDlDoor;
        private Border _bmDoor;
        private Border _minBtn;
        private Border _maxBtn;
        private Border _closeBtn;
        private Border _bookmarksBar;
        private StackPanel _bookmarksBarStack;
        private Border _alwaysAddressBar;
        private TextBox _alwaysAddressInput;
        private TextBlock _spaceTitleText;
        private StackPanel _spaceDotsPanel;
        private Border _spaceFooterBtn;
        private Border _bmFooterBtn;
        private Border _extFooterBtn;
        private Border _dlFooterBtn;
        private Border _mediaPlayerCard;
        private Border _spaceToast;
        private TextBlock _spaceToastText;
        private DispatcherTimer _spaceToastTimer;
        private Border _veilBanner;
        private TextBlock _veilBannerText;

        // Link Peek modal card (Peek.swift — Shift+Click on any link or external click from a pinned tab)
        private System.Windows.Controls.Primitives.Popup _peekPopup;
        private Grid _peekOverlay;
        private Border _peekCard;
        private TextBlock _peekTitleText;
        private Border _peekWebHost;
        private BrowserTab _peekTab;

        // Extension Popup / Page Style floating card
        private System.Windows.Controls.Primitives.Popup _extPopup;
        private Grid _extPopupOverlay;
        private TextBlock _extPopupTitle;
        private Border _extPopupHost;
        private WebView2 _extPopupWebView;

        // Floating Omnibox / Tab Switcher (Ctrl+L / Ctrl+K / Ctrl+T)
        private System.Windows.Controls.Primitives.Popup _omniboxPopup;
        private Grid _omniboxOverlay;
        private StackPanel _omniboxContainer;
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

        // Ctrl+Tab MRU Visual Switcher
        private System.Windows.Controls.Primitives.Popup _switcherPopup;
        private Border _switcherCard;
        private StackPanel _switcherStack;
        private List<BrowserTab> _switcherList = new List<BrowserTab>();
        private int _switcherIndex = 0;

        // Modal drawer for Settings / History / Bookmarks / Vault / Hidden / Extensions / Downloads / Site Styles / AI
        private System.Windows.Controls.Primitives.Popup _drawerPopup;
        private Grid _drawerOverlay;
        private Border _drawerCard;
        private TextBlock _drawerTitle;
        private StackPanel _drawerContent;

        // Floating Find bar (Ctrl+F) — layered Popup so WebView2 never clips it
        private System.Windows.Controls.Primitives.Popup _findPopup;
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
            BrowserTab.CleanupStalePrivateSessions();
            Palette.ResolveTheme();

            Title = "Search";
            Width = 1280;
            Height = 820;
            MinWidth = 680;
            MinHeight = 460;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            FontFamily = new FontFamily("Plus Jakarta Sans, Segoe UI Variable Text, Segoe UI");
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
            StateChanged += OnWindowStateChanged;
            PreviewKeyDown += OnWindowPreviewKeyDown;
            PreviewKeyUp += OnWindowPreviewKeyUp;
            System.Windows.Interop.ComponentDispatcher.ThreadPreprocessMessage += OnThreadPreprocessMessage;
            LocationChanged += (s, e) => { RepositionFindPopup(); RepositionSplitClosePopup(); };
            SizeChanged += (s, e) => { RepositionFindPopup(); RepositionSplitClosePopup(); };
            DownloadsUpdated += () => Dispatcher.InvokeAsync(() => UpdateFooterButtons());

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
            _rootGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

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

            // Top-left helm: [Sidebar] <  >  ↻  (+ optional top doors when ButtonsAtTop is on)
            var helmStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };
            _sidebarFoldBtn = CreateVectorIconButton(VectorIcons.SidebarPath, "Hide Sidebar (Ctrl+S)", () => ToggleSidebarFold());
            _backBtn = CreateVectorIconButton(VectorIcons.BackPath, "Back (Alt+Left / Ctrl+[)", () =>
            {
                if (_activeTab != null && _activeTab.WebView != null && _activeTab.WebView.CanGoBack) _activeTab.WebView.GoBack();
            });
            _forwardBtn = CreateVectorIconButton(VectorIcons.ForwardPath, "Forward (Alt+Right / Ctrl+])", () =>
            {
                if (_activeTab != null && _activeTab.WebView != null && _activeTab.WebView.CanGoForward) _activeTab.WebView.GoForward();
            });
            _reloadBtn = CreateVectorIconButton(VectorIcons.ReloadPath, "Reload (Ctrl+R)", () =>
            {
                if (_activeTab != null && _activeTab.IsWebViewInitialized && _activeTab.WebView != null)
                {
                    if (_activeTab.IsLoading) _activeTab.WebView.Stop();
                    else if (_activeTab.Url == "about:blank") _activeTab.RenderBlankWall();
                    else if (_activeTab.Url != null && _activeTab.Url.StartsWith("search://settings")) _activeTab.RenderSettingsTab();
                    else if (_activeTab.Url != null && _activeTab.Url.StartsWith("search://history")) _activeTab.RenderHistoryTab();
                    else _activeTab.WebView.CoreWebView2.Reload();
                }
            });
            _topExtDoor = CreateVectorIconButton(VectorIcons.ExtensionPath, "Extensions", () => ShowExtensionsDrawer());
            _topDlDoor = CreateVectorIconButton(VectorIcons.DownloadsPath, "Downloads (Ctrl+J)", () => ShowDownloadsDrawer());
            helmStack.Children.Add(_sidebarFoldBtn);
            helmStack.Children.Add(_backBtn);
            helmStack.Children.Add(_forwardBtn);
            helmStack.Children.Add(_reloadBtn);
            helmStack.Children.Add(_topExtDoor);
            helmStack.Children.Add(_topDlDoor);
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

            // Top-right: Bookmark ribbon door (12px gap before window buttons) + Window Caption Buttons (— □ ✕)
            _topRightBtns = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };
            _bmDoor = CreateVectorIconButton(VectorIcons.BookmarkPath, "Bookmarks (Ctrl+Shift+B to bookmark)", () => ShowBookmarksPopover(_bmDoor));
            _bmDoor.Margin = new Thickness(0, 0, 12, 0);
            _topRightBtns.Children.Add(_bmDoor);

            _minBtn = CreateCaptionButton(VectorIcons.WinMinimizePath, "Minimize", false, () => WindowState = WindowState.Minimized);
            _maxBtn = CreateCaptionButton(VectorIcons.WinMaximizePath, "Maximize / Restore", false, () =>
                WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized);
            _closeBtn = CreateCaptionButton(VectorIcons.WinClosePath, "Close", true, () => Close());
            _topRightBtns.Children.Add(_minBtn);
            _topRightBtns.Children.Add(_maxBtn);
            _topRightBtns.Children.Add(_closeBtn);
            Grid.SetColumn(_topRightBtns, 2);
            topBarGrid.Children.Add(_topRightBtns);

            _topTabBarBorder.Child = topBarGrid;
            _topTabBarBorder.MouseLeftButtonDown += (s, e) =>
            {
                if (e.ClickCount == 2)
                    WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
                else
                    try { DragMove(); } catch { }
            };
            Grid.SetRow(_topTabBarBorder, 0);
            Grid.SetColumnSpan(_topTabBarBorder, 3);
            _rootGrid.Children.Add(_topTabBarBorder);

            // 2. ULTRA-CLEAN SIDEBAR (Oriel's 232px column, symmetric 10px padding, resizable 176..440px)
            _sidebarBorder = new Border
            {
                Width = Store.Prefs.SidebarWidth > 100 ? Store.Prefs.SidebarWidth : 232,
                Background = Palette.Frame,
                Padding = new Thickness(0)
            };

            var sidebarOuterGrid = new Grid();
            var sidebarContentBorder = new Border
            {
                Padding = new Thickness(10, 0, 10, 10),
                Background = Brushes.Transparent
            };
            var sidebarDock = new DockPanel { LastChildFill = true };

            // Hidden legacy references so existing helpers never null-ref
            _spaceTitleText = new TextBlock();
            _spaceDotsPanel = new StackPanel();

            // Pinned tabs grid (dynamic 3/4-column squares at top of column)
            _pinsPanel = new WrapPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 0, 6),
                MinHeight = 6,
                Background = Brushes.Transparent,
                AllowDrop = true
            };
            _pinsPanel.DragOver += (s, e) =>
            {
                if (_draggedTab != null && !_draggedTab.IsPrivate) { e.Effects = DragDropEffects.Move; e.Handled = true; }
            };
            _pinsPanel.Drop += (s, e) =>
            {
                if (_draggedTab != null && !_draggedTab.IsPrivate)
                {
                    _draggedTab.Pinned = true;
                    _draggedTab.HomeUrl = _draggedTab.Url;
                    _draggedTab = null;
                    RefreshTabRows();
                    SaveCurrentSession();
                    e.Handled = true;
                }
            };
            DockPanel.SetDock(_pinsPanel, Dock.Top);
            sidebarDock.Children.Add(_pinsPanel);

            // KeepLine divider (Oriel Lines.tsx — only shown when PinnedRows is ON, with quiet Clear button on hover)
            _pinsDivider = new DockPanel
            {
                Margin = new Thickness(4, 4, 4, 6),
                Visibility = Visibility.Collapsed
            };
            var clearUnpinnedBtn = new TextBlock
            {
                Text = "Clear",
                FontSize = 11,
                Foreground = Palette.FrameMuted,
                Cursor = Cursors.Hand,
                Margin = new Thickness(8, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            clearUnpinnedBtn.MouseEnter += (s, e) => clearUnpinnedBtn.Foreground = Palette.Ink;
            clearUnpinnedBtn.MouseLeave += (s, e) => clearUnpinnedBtn.Foreground = Palette.FrameMuted;
            clearUnpinnedBtn.MouseLeftButtonUp += (s, e) =>
            {
                e.Handled = true;
                var toClose = GetTabsForActiveSpace().Where(t => !t.Pinned && string.IsNullOrEmpty(t.GroupId) && t != _activeTab).ToList();
                foreach (var tc in toClose) CloseTab(tc);
            };
            DockPanel.SetDock(clearUnpinnedBtn, Dock.Right);
            _pinsDivider.Children.Add(clearUnpinnedBtn);
            _pinsDivider.Children.Add(new Border
            {
                Height = 1,
                Background = Palette.Hairline,
                VerticalAlignment = VerticalAlignment.Center
            });
            DockPanel.SetDock(_pinsDivider, Dock.Top);
            sidebarDock.Children.Add(_pinsDivider);

            // Bottom footer (Oriel Foot.tsx: 26x26 r=8 SpaceDot + Bookmarks + Extensions + Downloads + Media Player + Space toast)
            var footerStack = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(0, 4, 0, 0) };

            _mediaPlayerCard = new Border
            {
                CornerRadius = new CornerRadius(9),
                Background = Palette.FrameTile,
                Padding = new Thickness(8, 6, 8, 6),
                Margin = new Thickness(0, 0, 0, 6),
                Visibility = Visibility.Collapsed
            };
            footerStack.Children.Add(_mediaPlayerCard);

            _spaceToast = new Border
            {
                CornerRadius = new CornerRadius(7),
                Background = Palette.FrameTile,
                Padding = new Thickness(8, 4, 8, 4),
                Margin = new Thickness(0, 0, 0, 4),
                HorizontalAlignment = HorizontalAlignment.Left,
                Visibility = Visibility.Collapsed
            };
            _spaceToastText = new TextBlock { FontSize = 11.5, FontWeight = FontWeights.Medium, Foreground = Palette.Ink };
            _spaceToast.Child = _spaceToastText;
            footerStack.Children.Add(_spaceToast);

            var footerRow = new DockPanel { Margin = new Thickness(0, 2, 0, 2) };
            _spaceFooterBtn = new Border
            {
                Width = 26,
                Height = 26,
                CornerRadius = new CornerRadius(8),
                Background = Brushes.Transparent,
                Cursor = Cursors.Hand,
                ToolTip = "Spaces (Alt+1..9)",
                Child = VectorIcons.Draw(VectorIcons.HomePath, Palette.FrameMuted, 13, 1.5)
            };
            _spaceFooterBtn.MouseEnter += (s, e) => _spaceFooterBtn.Background = Palette.FrameHover;
            _spaceFooterBtn.MouseLeave += (s, e) => _spaceFooterBtn.Background = Brushes.Transparent;
            _spaceFooterBtn.MouseLeftButtonUp += (s, e) => ShowSpacesMenu(_spaceFooterBtn);
            DockPanel.SetDock(_spaceFooterBtn, Dock.Left);
            footerRow.Children.Add(_spaceFooterBtn);

            var rightFooterDoors = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            _bmFooterBtn = CreateVectorIconButton(VectorIcons.BookmarkPath, "Bookmarks (Ctrl+Shift+B)", () => ShowBookmarksPopover(_bmFooterBtn));
            _extFooterBtn = CreateVectorIconButton(VectorIcons.ExtensionPath, "Extensions", () => ShowExtensionsDrawer());
            _dlFooterBtn = CreateVectorIconButton(VectorIcons.DownloadsPath, "Downloads (Ctrl+J)", () => ShowDownloadsDrawer());
            rightFooterDoors.Children.Add(_bmFooterBtn);
            rightFooterDoors.Children.Add(_extFooterBtn);
            rightFooterDoors.Children.Add(_dlFooterBtn);
            DockPanel.SetDock(rightFooterDoors, Dock.Right);
            footerRow.Children.Add(rightFooterDoors);
            footerRow.Children.Add(new Grid());
            footerStack.Children.Add(footerRow);

            DockPanel.SetDock(footerStack, Dock.Bottom);
            sidebarDock.Children.Add(footerStack);

            // Scrollable loose & grouped tabs list with Oriel's smooth gliding selection pill
            var tabsScroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Background = Brushes.Transparent
            };
            tabsScroll.Focusable = false;
            tabsScroll.FocusVisualStyle = null;
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
                Background = Palette.FrameWash,
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

            sidebarContentBorder.Child = sidebarDock;
            sidebarOuterGrid.Children.Add(sidebarContentBorder);

            // Sidebar edge resize grip (Oriel: 176..440px, double-click resets to 232px, <200px NarrowToIcons)
            _sidebarResizeGrip = new Border
            {
                Width = 5,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Stretch,
                Background = Brushes.Transparent,
                Cursor = Cursors.SizeWE
            };
            _sidebarResizeGrip.MouseLeftButtonDown += OnSidebarResizeDown;
            _sidebarResizeGrip.MouseMove += OnSidebarResizeMove;
            _sidebarResizeGrip.MouseLeftButtonUp += OnSidebarResizeUp;
            sidebarOuterGrid.Children.Add(_sidebarResizeGrip);

            _sidebarBorder.Child = sidebarOuterGrid;
            Grid.SetRow(_sidebarBorder, 1);
            Grid.SetColumn(_sidebarBorder, 0);
            _rootGrid.Children.Add(_sidebarBorder);

            // 3. MAIN PAGE STAGE (CardEdge: 10px rounded corners, 0.08 alpha edge, subtle shadow)
            var stageOuterGrid = new Grid();

            _pageCardBorder = new Border
            {
                Background = Palette.Ground,
                BorderBrush = Palette.CardEdge,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Margin = new Thickness(0, 0, 8, 8),
                ClipToBounds = true,
                AllowDrop = true,
                Effect = new DropShadowEffect { BlurRadius = 10, ShadowDepth = 1, Opacity = 0.08, Color = Colors.Black }
            };
            _pageCardBorder.DragOver += OnStageDragOver;
            _pageCardBorder.Drop += OnStageDrop;

            var pageInnerDock = new DockPanel { LastChildFill = true };

            // Thin 2px loading line along page top when sidebar is hidden (LoadingLineWhenHidden)
            _loadingLine = new Border
            {
                Height = 2,
                Background = Palette.Ink,
                Opacity = 0.55,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Visibility = Visibility.Collapsed
            };
            DockPanel.SetDock(_loadingLine, Dock.Top);
            pageInnerDock.Children.Add(_loadingLine);

            // Optional Always-Show Address Bar (Settings -> Appearance -> Always show the address bar)
            _alwaysAddressBar = new Border
            {
                Background = Palette.Ground,
                BorderBrush = Palette.Hairline,
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(10, 5, 10, 5),
                Visibility = Visibility.Collapsed
            };
            _alwaysAddressInput = new TextBox
            {
                FontSize = 12.5,
                Background = Palette.Wash,
                Foreground = Palette.Ink,
                CaretBrush = Palette.Ink,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(10, 4, 10, 4),
                VerticalContentAlignment = VerticalAlignment.Center
            };
            _alwaysAddressInput.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter && _activeTab != null && !string.IsNullOrWhiteSpace(_alwaysAddressInput.Text))
                {
                    _activeTab.Navigate(_alwaysAddressInput.Text.Trim());
                    e.Handled = true;
                }
            };
            _alwaysAddressBar.Child = _alwaysAddressInput;
            DockPanel.SetDock(_alwaysAddressBar, Dock.Top);
            pageInnerDock.Children.Add(_alwaysAddressBar);

            // Optional Bookmarks Bar (Settings -> Appearance -> Show the bookmarks bar)
            _bookmarksBar = new Border
            {
                Height = 30,
                Background = Palette.Ground,
                BorderBrush = Palette.Hairline,
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(8, 0, 8, 0),
                Visibility = Visibility.Collapsed
            };
            _bookmarksBarStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            _bookmarksBar.Child = _bookmarksBarStack;
            DockPanel.SetDock(_bookmarksBar, Dock.Top);
            pageInnerDock.Children.Add(_bookmarksBar);

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
            _stageGrid = new Grid { Background = Brushes.Transparent };
            _stageGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            _stageGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            _stageGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0, GridUnitType.Pixel) });

            _leftPaneHost = new Border { Background = Brushes.Transparent };
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

            _rightPaneHost = new Border { Background = Brushes.Transparent, Visibility = Visibility.Collapsed };
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

            var splitCloseBtn = new Border
            {
                Width = 26,
                Height = 26,
                CornerRadius = new CornerRadius(13),
                Background = Palette.CardSurface,
                BorderBrush = Palette.CardEdge,
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand,
                ToolTip = "Close Side Panel",
                Child = VectorIcons.Draw(VectorIcons.CloseSmallPath, Palette.Ink, 10, 1.5),
                Effect = new DropShadowEffect { BlurRadius = 10, ShadowDepth = 2, Opacity = 0.25 }
            };
            splitCloseBtn.MouseEnter += (s, e) => splitCloseBtn.Background = Palette.Wash;
            splitCloseBtn.MouseLeave += (s, e) => splitCloseBtn.Background = Palette.CardSurface;
            splitCloseBtn.MouseLeftButtonDown += (s, e) => { e.Handled = true; };
            splitCloseBtn.MouseLeftButtonUp += (s, e) => { e.Handled = true; ExitSplitView(_splitLeftTab); };
            _splitClosePopup = new System.Windows.Controls.Primitives.Popup
            {
                Child = new Border { Padding = new Thickness(4), Child = splitCloseBtn },
                PlacementTarget = _rightPaneHost,
                Placement = System.Windows.Controls.Primitives.PlacementMode.Relative,
                AllowsTransparency = true,
                StaysOpen = true
            };
            _rightPaneHost.SizeChanged += (s, e) => RepositionSplitClosePopup();

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

            // 10. CTRL+TAB MRU VISUAL SWITCHER POPUP
            BuildSwitcherPopup();

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
                Background = Brushes.Transparent,
                Visibility = Visibility.Visible
            };

            var outerStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            _peekCard = new Border
            {
                Width = 860,
                Height = 620,
                Background = Palette.CardSurface,
                BorderBrush = Palette.CardEdge,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                ClipToBounds = true,
                Effect = new DropShadowEffect { BlurRadius = 38, ShadowDepth = 12, Opacity = 0.28 }
            };

            _peekTitleText = new TextBlock { Visibility = Visibility.Collapsed };
            _peekWebHost = new Border { Background = Palette.Ground };
            _peekCard.Child = _peekWebHost;
            outerStack.Children.Add(_peekCard);

            // Oriel Peek floating right-side rail of 28x28 circular knobs (Close, Expand to Tab, Open Beside in Split)
            var knobRail = new StackPanel
            {
                Orientation = Orientation.Vertical,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(10, 6, 0, 0)
            };
            knobRail.Children.Add(CreatePeekKnob(VectorIcons.CloseSmallPath, "Close Peek (Esc)", () => ClosePeekCard()));
            knobRail.Children.Add(CreatePeekKnob(VectorIcons.WinMaximizePath, "Expand to Full Tab (Ctrl+O)", async () =>
            {
                if (_peekTab != null)
                {
                    var keep = _peekTab;
                    _peekTab = null;
                    _peekWebHost.Child = null;
                    if (_peekPopup != null) _peekPopup.IsOpen = false;
                    AttachTabEvents(keep);
                    InsertNewTab(keep, _activeTab);
                    await SelectTabAsync(keep);
                    RefreshTabRows();
                }
            }));
            knobRail.Children.Add(CreatePeekKnob(VectorIcons.SidebarPath, "Keep Beside in Split View", async () =>
            {
                if (_peekTab != null && _activeTab != null)
                {
                    var keep = _peekTab;
                    _peekTab = null;
                    _peekWebHost.Child = null;
                    if (_peekPopup != null) _peekPopup.IsOpen = false;
                    AttachTabEvents(keep);
                    InsertNewTab(keep, _activeTab);
                    await OpenTabInSplitViewAsync(keep, false);
                }
            }));
            outerStack.Children.Add(knobRail);

            _peekOverlay.Children.Add(outerStack);
            _peekPopup = new System.Windows.Controls.Primitives.Popup
            {
                Child = _peekOverlay,
                PlacementTarget = _pageCardBorder,
                Placement = System.Windows.Controls.Primitives.PlacementMode.Center,
                AllowsTransparency = true,
                StaysOpen = false,
                PopupAnimation = System.Windows.Controls.Primitives.PopupAnimation.Fade
            };
            _peekPopup.Closed += (s, e) => ClosePeekCard();
        }

        private Border CreatePeekKnob(string pathData, string tooltip, Action onClick)
        {
            var b = new Border
            {
                Width = 28,
                Height = 28,
                CornerRadius = new CornerRadius(14),
                Background = Palette.CardSurface,
                BorderBrush = Palette.CardEdge,
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 0, 8),
                Cursor = Cursors.Hand,
                ToolTip = tooltip,
                Child = VectorIcons.Draw(pathData, Palette.Ink, 11, 1.5),
                Effect = new DropShadowEffect { BlurRadius = 12, ShadowDepth = 2, Opacity = 0.22 }
            };
            b.MouseEnter += (s, e) => b.Background = Palette.Wash;
            b.MouseLeave += (s, e) => b.Background = Palette.CardSurface;
            b.MouseLeftButtonUp += (s, e) =>
            {
                e.Handled = true;
                onClick();
            };
            return b;
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
            _peekPopup.IsOpen = true;
            await _peekTab.EnsureAwakeAsync(_sharedEnv, _peekWebHost);
            _peekWebHost.Child = _peekTab.WebView;
        }

        private void ClosePeekCard()
        {
            if (_peekPopup != null && _peekPopup.IsOpen) _peekPopup.IsOpen = false;
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
            _extPopupOverlay.Background = Brushes.Transparent;
            _extPopupOverlay.Visibility = Visibility.Visible;
            _extPopup = new System.Windows.Controls.Primitives.Popup
            {
                Child = _extPopupOverlay,
                PlacementTarget = _pageCardBorder,
                Placement = System.Windows.Controls.Primitives.PlacementMode.Center,
                AllowsTransparency = true,
                StaysOpen = false,
                PopupAnimation = System.Windows.Controls.Primitives.PopupAnimation.Fade
            };
            _extPopup.Closed += (s, e) => CloseExtensionPopup();
        }

        private async void OpenExtensionPopup(ExtensionInfo ext)
        {
            CloseExtensionPopup();
            SetDrawerVisible(false);
            _extPopupTitle.Text = "🧩 " + ext.Name;
            _extPopup.IsOpen = true;

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
            if (_extPopup != null && _extPopup.IsOpen) _extPopup.IsOpen = false;
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
                Background = Palette.CardSurface,
                BorderBrush = Palette.CardEdge,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(999),
                Padding = new Thickness(14, 5, 8, 5),
                Effect = new DropShadowEffect { BlurRadius = 20, ShadowDepth = 4, Opacity = 0.2 }
            };
            var stack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            _findInput = new TextBox
            {
                Width = 190,
                Background = Brushes.Transparent,
                Foreground = Palette.Ink,
                CaretBrush = Palette.Ink,
                BorderThickness = new Thickness(0),
                FontSize = 13,
                VerticalContentAlignment = VerticalAlignment.Center
            };
            _findInput.TextChanged += async (s, e) =>
            {
                if (!string.IsNullOrEmpty(_findInput.Text))
                    await ExecuteFindOnPageAsync(_findInput.Text, true);
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
                    HideFindBar();
                    e.Handled = true;
                }
            };
            stack.Children.Add(_findInput);
            stack.Children.Add(CreateVectorIconButton(VectorIcons.ChevronUpPath, "Previous match (Shift+Enter / Ctrl+Shift+G)", async () => await ExecuteFindOnPageAsync(_findInput.Text, false)));
            stack.Children.Add(CreateVectorIconButton(VectorIcons.ChevronDownPath, "Next match (Enter / Ctrl+G)", async () => await ExecuteFindOnPageAsync(_findInput.Text, true)));
            stack.Children.Add(CreateVectorIconButton(VectorIcons.CloseSmallPath, "Close Find (Esc)", () => HideFindBar()));
            _findBar.Child = stack;

            _findPopup = new System.Windows.Controls.Primitives.Popup
            {
                Child = _findBar,
                PlacementTarget = _pageCardBorder,
                Placement = System.Windows.Controls.Primitives.PlacementMode.Relative,
                AllowsTransparency = true,
                StaysOpen = true,
                PopupAnimation = System.Windows.Controls.Primitives.PopupAnimation.Fade
            };
        }

        private void ShowFindBar()
        {
            if (_findPopup == null) return;
            _findBar.Background = Palette.CardSurface;
            _findBar.BorderBrush = Palette.CardEdge;
            _findInput.Foreground = Palette.Ink;
            _findInput.CaretBrush = Palette.Ink;
            RepositionFindPopup();
            _findPopup.IsOpen = true;
            FocusPopupControl(_findInput, true);
        }

        private void HideFindBar()
        {
            if (_findPopup != null && _findPopup.IsOpen) _findPopup.IsOpen = false;
            if (_activeTab != null && _activeTab.WebView != null) _activeTab.WebView.Focus();
        }

        private void RepositionFindPopup()
        {
            if (_findPopup == null || _pageCardBorder == null) return;
            double w = _pageCardBorder.ActualWidth > 0 ? _pageCardBorder.ActualWidth : 800;
            _findPopup.HorizontalOffset = Math.Max(14, w - 320);
            _findPopup.VerticalOffset = 12;
        }

        private async Task ExecuteFindOnPageAsync(string query, bool forward)
        {
            if (_activeTab == null || !_activeTab.IsWebViewInitialized || _activeTab.WebView == null) return;
            if (string.IsNullOrEmpty(query)) return;
            string escaped = query.Replace("\\", "\\\\").Replace("'", "\\'").Replace("\r", "\\r").Replace("\n", "\\n").Replace("\u2028", "\\u2028").Replace("\u2029", "\\u2029");
            string back = forward ? "false" : "true";
            await _activeTab.WebView.CoreWebView2.ExecuteScriptAsync("window.find('" + escaped + "', false, " + back + ", true, false, true, false);");
        }

        private void BuildOmniboxOverlay(Grid hostGrid)
        {
            _omniboxOverlay = new Grid
            {
                Background = Brushes.Transparent,
                Visibility = Visibility.Visible,
                Width = 600,
                Height = 460
            };
            _omniboxOverlay.MouseLeftButtonDown += (s, e) =>
            {
                if (e.OriginalSource == _omniboxOverlay)
                    HideOmnibox();
            };

            // Top-anchored inside fixed-height popup container so offers hang BELOW without jumping the input field upward
            _omniboxContainer = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Top,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 28, 0, 0),
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
            _omniboxContainer.Children.Add(_omniboxCard);

            _omniboxOffersBorder = new Border
            {
                Background = Palette.CardSurface,
                BorderBrush = Palette.CardEdge,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Margin = new Thickness(0, 8, 0, 0),
                Padding = new Thickness(6),
                Visibility = Visibility.Collapsed,
                Effect = new DropShadowEffect { BlurRadius = 24, ShadowDepth = 6, Opacity = 0.18 }
            };
            _omniboxOffersStack = new StackPanel { Orientation = Orientation.Vertical };
            _omniboxOffersBorder.Child = _omniboxOffersStack;
            _omniboxContainer.Children.Add(_omniboxOffersBorder);

            _omniboxOverlay.Children.Add(_omniboxContainer);
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

        private void BuildSwitcherPopup()
        {
            _switcherStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            _switcherCard = new Border
            {
                Background = Palette.MenuBg,
                BorderBrush = Palette.MenuBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(12, 10, 12, 10),
                Child = _switcherStack,
                Effect = new DropShadowEffect { BlurRadius = 28, ShadowDepth = 8, Opacity = 0.28 }
            };
            _switcherPopup = new System.Windows.Controls.Primitives.Popup
            {
                Child = _switcherCard,
                PlacementTarget = _pageCardBorder,
                Placement = System.Windows.Controls.Primitives.PlacementMode.Center,
                AllowsTransparency = true,
                StaysOpen = true,
                PopupAnimation = System.Windows.Controls.Primitives.PopupAnimation.Fade
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
                    SetDrawerVisible(false);
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
            var closeBtn = CreateQuietButton("Close (Esc)", "Close panel", () => SetDrawerVisible(false));
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
            _drawerOverlay.Background = Brushes.Transparent;
            _drawerOverlay.Visibility = Visibility.Visible;
            _drawerPopup = new System.Windows.Controls.Primitives.Popup
            {
                Child = _drawerOverlay,
                PlacementTarget = _pageCardBorder,
                Placement = System.Windows.Controls.Primitives.PlacementMode.Center,
                AllowsTransparency = true,
                StaysOpen = false,
                PopupAnimation = System.Windows.Controls.Primitives.PopupAnimation.Fade
            };
        }

        private void SetDrawerVisible(bool open)
        {
            if (_drawerPopup == null) return;
            if (open)
            {
                _drawerCard.Background = Palette.CardSurface;
                _drawerCard.BorderBrush = Palette.Hairline;
                _drawerTitle.Foreground = Palette.Ink;
            }
            _drawerPopup.IsOpen = open;
        }

        [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        private static extern IntPtr CreateRoundRectRgn(int nLeftRect, int nTopRect, int nRightRect, int nBottomRect, int nWidthEllipse, int nHeightEllipse);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern int SetWindowRgn(IntPtr hWnd, IntPtr hRgn, bool bRedraw);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr SetFocus(IntPtr hWnd);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);
        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);
        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto, SetLastError = true)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);
        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern short GetKeyState(int nVirtKey);
        [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        private IntPtr _mainHwnd = IntPtr.Zero;
        private IntPtr _kbHookId = IntPtr.Zero;
        private LowLevelKeyboardProc _kbHookProc;

        private void InstallGlobalShortcutHook()
        {
            try
            {
                _mainHwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                if (_kbHookId != IntPtr.Zero) return;
                _kbHookProc = LowLevelKeyboardCallback;
                using (var curProc = Process.GetCurrentProcess())
                using (var curMod = curProc.MainModule)
                {
                    _kbHookId = SetWindowsHookEx(13, _kbHookProc, GetModuleHandle(curMod.ModuleName), 0);
                }
            }
            catch { }
        }

        private void UninstallGlobalShortcutHook()
        {
            if (_kbHookId != IntPtr.Zero)
            {
                try { UnhookWindowsHookEx(_kbHookId); } catch { }
                _kbHookId = IntPtr.Zero;
            }
        }

        private IntPtr LowLevelKeyboardCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && _mainHwnd != IntPtr.Zero)
            {
                int msg = wParam.ToInt32();
                if ((msg == 0x0101 || msg == 0x0105) && _switcherPopup != null && _switcherPopup.IsOpen)
                {
                    int vkUp = System.Runtime.InteropServices.Marshal.ReadInt32(lParam);
                    if (vkUp == 0x11 || vkUp == 0xA2 || vkUp == 0xA3) // Ctrl released
                    {
                        Dispatcher.BeginInvoke(DispatcherPriority.Send, new Action(() => { var _ = CommitMruSwitcherAsync(); }));
                    }
                }
                if ((msg == 0x0100 || msg == 0x0104) && GetForegroundWindow() == _mainHwnd)
                {
                    int vk = System.Runtime.InteropServices.Marshal.ReadInt32(lParam);
                    bool ctrl = (GetKeyState(0x11) & 0x8000) != 0;
                    bool shift = (GetKeyState(0x10) & 0x8000) != 0;
                    bool alt = (GetKeyState(0x12) & 0x8000) != 0 || msg == 0x0104;

                    // 1. Alt+1..9 (Space switch) / Alt+Left/Right / Alt+D / Alt+Shift+N/L/D
                    if (alt && !ctrl)
                    {
                        if (!shift)
                        {
                            int spIdx = (vk >= 0x31 && vk <= 0x39) ? (vk - 0x31) : ((vk >= 0x61 && vk <= 0x69) ? (vk - 0x61) : -1);
                            if (spIdx >= 0)
                            {
                                int capIdx = spIdx;
                                Dispatcher.BeginInvoke(DispatcherPriority.Send, new Action(() =>
                                {
                                    if (!ShouldRunShortcutOnce("A:" + (capIdx + 1))) return;
                                    if (Store.Prefs.CtrlNumbersSwitchSpaces)
                                    {
                                        var st = GetTabsForActiveSpace();
                                        int ti = (capIdx == 8 && st.Count > 0) ? st.Count - 1 : capIdx;
                                        if (ti < st.Count) { var _ = SelectTabAsync(st[ti]); }
                                    }
                                    else if (capIdx < Store.Spaces.Count)
                                    {
                                        CycleSpace(capIdx);
                                    }
                                }));
                                return (IntPtr)1;
                            }
                            if (vk == 0x25 || vk == 0x27) // Alt+Left / Alt+Right
                            {
                                bool isLeft = vk == 0x25;
                                Dispatcher.BeginInvoke(DispatcherPriority.Send, new Action(() =>
                                {
                                    if (_activeTab != null && _activeTab.WebView != null)
                                    {
                                        if (isLeft && _activeTab.WebView.CanGoBack) _activeTab.WebView.GoBack();
                                        else if (!isLeft && _activeTab.WebView.CanGoForward) _activeTab.WebView.GoForward();
                                    }
                                }));
                                return (IntPtr)1;
                            }
                            if (vk == 0x44) // Alt+D
                            {
                                Dispatcher.BeginInvoke(DispatcherPriority.Send, new Action(() => ShowOmnibox(false, false)));
                                return (IntPtr)1;
                            }
                        }
                        else
                        {
                            if (vk == 0x4E) { Dispatcher.BeginInvoke(DispatcherPriority.Send, new Action(() => ToggleSplitView())); return (IntPtr)1; }
                            if (vk == 0x4C) { Dispatcher.BeginInvoke(DispatcherPriority.Send, new Action(() => OpenOrSelectSettingsTab("passwords"))); return (IntPtr)1; }
                        }
                    }

                    // 2. Function keys: F5 (Reload), F6 (Address), F11 (FullScreen), F12 (DevTools)
                    if (!alt && !ctrl && !shift)
                    {
                        if (vk == 0x74) { Dispatcher.BeginInvoke(DispatcherPriority.Send, new Action(() => { if (_activeTab != null && _activeTab.IsWebViewInitialized && _activeTab.WebView != null) _activeTab.WebView.CoreWebView2.Reload(); })); return (IntPtr)1; }
                        if (vk == 0x75) { Dispatcher.BeginInvoke(DispatcherPriority.Send, new Action(() => ShowOmnibox(false, false))); return (IntPtr)1; }
                        if (vk == 0x7A) { Dispatcher.BeginInvoke(DispatcherPriority.Send, new Action(() => ToggleFullScreen())); return (IntPtr)1; }
                        if (vk == 0x7B) { Dispatcher.BeginInvoke(DispatcherPriority.Send, new Action(() => { if (_activeTab != null && _activeTab.IsWebViewInitialized && _activeTab.WebView != null) _activeTab.WebView.CoreWebView2.OpenDevToolsWindow(); })); return (IntPtr)1; }
                    }

                    // 3. Ctrl+<key> and Ctrl+Shift+<key> browser shortcuts (never swallows C/V/X/A/Z text editing)
                    if (ctrl && !alt)
                    {
                        if (vk == 0x09) // Ctrl+Tab / Ctrl+Shift+Tab
                        {
                            bool capSh = shift;
                            Dispatcher.BeginInvoke(DispatcherPriority.Send, new Action(() => StepMruSwitcher(capSh)));
                            return (IntPtr)1;
                        }
                        if (vk == 0x21 || vk == 0x22) // Ctrl+PageUp / PageDown
                        {
                            bool up = vk == 0x21;
                            Dispatcher.BeginInvoke(DispatcherPriority.Send, new Action(() => { var _ = StepAdjacentTabAsync(up); }));
                            return (IntPtr)1;
                        }
                        if (vk == 0x73) // Ctrl+F4
                        {
                            Dispatcher.BeginInvoke(DispatcherPriority.Send, new Action(() => { if (_activeTab != null) CloseTab(_activeTab); }));
                            return (IntPtr)1;
                        }
                        if (!shift && vk >= 0x31 && vk <= 0x39)
                        {
                            int capIdx = vk - 0x31;
                            Dispatcher.BeginInvoke(DispatcherPriority.Send, new Action(() =>
                            {
                                if (!ShouldRunShortcutOnce("C:" + (capIdx + 1))) return;
                                if (Store.Prefs.CtrlNumbersSwitchSpaces && capIdx < Store.Spaces.Count) { CycleSpace(capIdx); return; }
                                var st = GetTabsForActiveSpace();
                                int ti = (capIdx == 8 && st.Count > 0) ? st.Count - 1 : capIdx;
                                if (ti < st.Count) { var _ = SelectTabAsync(st[ti]); }
                            }));
                            return (IntPtr)1;
                        }

                        string keyStr = null;
                        if (!shift)
                        {
                            if (vk == 0x48) keyStr = "h";
                            else if (vk == 0x59) keyStr = "y";
                            else if (vk == 0xBC) keyStr = ",";
                            else if (vk == 0x54) keyStr = "t";
                            else if (vk == 0x57) keyStr = "w";
                            else if (vk == 0x4C) keyStr = "l";
                            else if (vk == 0x4B) keyStr = "k";
                            else if (vk == 0x53) keyStr = "s";
                            else if (vk == 0x46) keyStr = "f";
                            else if (vk == 0x4A) keyStr = "j";
                            else if (vk == 0x52) keyStr = "r";
                            else if (vk == 0x44) keyStr = "d";
                            else if (vk == 0xDB) keyStr = "[";
                            else if (vk == 0xDD) keyStr = "]";
                        }
                        else
                        {
                            if (vk == 0x54) keyStr = "t";
                            else if (vk == 0x4E) keyStr = "n";
                            else if (vk == 0x53) keyStr = "s";
                            else if (vk == 0x42) keyStr = "b";
                            else if (vk == 0x52) keyStr = "r";
                            else if (vk == 0x48) keyStr = "h";
                            else if (vk == 0x55) keyStr = "u";
                            else if (vk == 0x50) keyStr = "p";
                            else if (vk == 0x4A) keyStr = "j";
                            else if (vk == 0x49)
                            {
                                Dispatcher.BeginInvoke(DispatcherPriority.Send, new Action(() => { if (_activeTab != null && _activeTab.IsWebViewInitialized && _activeTab.WebView != null) _activeTab.WebView.CoreWebView2.OpenDevToolsWindow(); }));
                                return (IntPtr)1;
                            }
                        }

                        if (keyStr != null)
                        {
                            string capKey = keyStr;
                            bool capSh = shift;
                            Dispatcher.BeginInvoke(DispatcherPriority.Send, new Action(() =>
                            {
                                string sig = "C" + (capSh ? "S" : "") + ":" + capKey;
                                if (!ShouldRunShortcutOnce(sig)) return;
                                var _ = HandleShortcutKeyAsync(capKey, capSh);
                            }));
                            return (IntPtr)1;
                        }
                    }
                }
            }
            return CallNextHookEx(_kbHookId, nCode, wParam, lParam);
        }

        private sealed class Win32OwnerWindow : System.Windows.Forms.IWin32Window
        {
            private readonly IntPtr _hwnd;
            public Win32OwnerWindow(IntPtr hwnd) { _hwnd = hwnd; }
            public IntPtr Handle { get { return _hwnd; } }
        }

        private Win32OwnerWindow GetWin32Owner()
        {
            return new Win32OwnerWindow(new System.Windows.Interop.WindowInteropHelper(this).Handle);
        }

        // Transfers Win32 HWND focus OUT of WebView2's HwndHost into a WPF Popup control (fixes Ctrl+T / Ctrl+L / Ctrl+F / Bookmarks typing!)
        private void FocusPopupControl(UIElement el, bool selectAll = false)
        {
            if (el == null) return;
            Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
            {
                try
                {
                    var src = PresentationSource.FromVisual(el) as System.Windows.Interop.HwndSource;
                    if (src != null && src.Handle != IntPtr.Zero)
                    {
                        SetForegroundWindow(src.Handle);
                        SetFocus(src.Handle);
                    }
                    el.Focus();
                    Keyboard.Focus(el);
                    var tb = el as TextBox;
                    if (tb != null && selectAll) tb.SelectAll();
                }
                catch { }
            }));
        }

        private void ApplyRoundedCornersToHost(Border host)
        {
            if (host == null) return;
            var wv = host.Child as Microsoft.Web.WebView2.Wpf.WebView2;
            if (wv == null || wv.Handle == IntPtr.Zero) return;
            bool isTop = string.Equals(Store.Prefs.TabLayout, "Top", StringComparison.OrdinalIgnoreCase);
            bool showCard = Store.Prefs.PageAsCard && !isTop && (!_isFullScreen || Store.Prefs.KeepCardInFullScreen);
            if (showCard)
            {
                var source = PresentationSource.FromVisual(wv);
                double dpiX = source != null && source.CompositionTarget != null ? source.CompositionTarget.TransformToDevice.M11 : 1.0;
                double dpiY = source != null && source.CompositionTarget != null ? source.CompositionTarget.TransformToDevice.M22 : 1.0;
                int w = (int)(wv.ActualWidth * dpiX);
                int h = (int)(wv.ActualHeight * dpiY);
                int r = (int)(18 * dpiX);
                if (w > 0 && h > 0)
                {
                    IntPtr rgn = CreateRoundRectRgn(0, 0, w + 1, h + 1, r, r);
                    SetWindowRgn(wv.Handle, rgn, true);
                }
            }
            else
            {
                SetWindowRgn(wv.Handle, IntPtr.Zero, true);
            }
        }

        private void OnWindowStateChanged(object sender, EventArgs e)
        {
            if (_maxBtn != null)
            {
                string path = WindowState == WindowState.Maximized ? VectorIcons.WinRestorePath : VectorIcons.WinMaximizePath;
                _maxBtn.Child = VectorIcons.Draw(path, Palette.Muted, 12, 1.45);
            }
            ApplyRoundedCornersToHost(_leftPaneHost);
            ApplyRoundedCornersToHost(_rightPaneHost);
        }

        private void ToggleFullScreen()
        {
            SetFullScreen(!_isFullScreen, false);
        }

        private Rect _preFullScreenBounds = Rect.Empty;
        private void SetFullScreen(bool enter, bool fromWebElement)
        {
            if (enter == _isFullScreen) return;
            if (enter)
            {
                _preFullScreenState = WindowState;
                _preFullScreenBounds = new Rect(Left, Top, Width, Height);
                _isFullScreen = true;
                _topTabBarBorder.Visibility = Visibility.Collapsed;
                _rootGrid.RowDefinitions[0].Height = new GridLength(0);

                // Cover the entire physical monitor (including wacOS top bar & dock), just like Brave/Chrome F11
                WindowStyle = WindowStyle.None;
                WindowState = WindowState.Normal;
                try
                {
                    var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                    var scr = System.Windows.Forms.Screen.FromHandle(hwnd);
                    var src = PresentationSource.FromVisual(this);
                    double dx = (src != null && src.CompositionTarget != null) ? src.CompositionTarget.TransformToDevice.M11 : 1.0;
                    double dy = (src != null && src.CompositionTarget != null) ? src.CompositionTarget.TransformToDevice.M22 : 1.0;
                    Left = scr.Bounds.Left / dx;
                    Top = scr.Bounds.Top / dy;
                    Width = scr.Bounds.Width / dx;
                    Height = scr.Bounds.Height / dy;
                }
                catch { WindowState = WindowState.Maximized; }

                ApplyLayoutPreferences();
                if (fromWebElement)
                {
                    if (_sidebarBorder != null) _sidebarBorder.Visibility = Visibility.Collapsed;
                    if (_sidebarHoverHotzone != null) _sidebarHoverHotzone.Visibility = Visibility.Collapsed;
                    if (_pageCardBorder != null)
                    {
                        _pageCardBorder.CornerRadius = new CornerRadius(0);
                        _pageCardBorder.Margin = new Thickness(0);
                        _pageCardBorder.BorderThickness = new Thickness(0);
                    }
                    ApplyRoundedCornersToHost(_leftPaneHost);
                    ApplyRoundedCornersToHost(_rightPaneHost);
                }
            }
            else
            {
                _isFullScreen = false;
                WindowStyle = WindowStyle.SingleBorderWindow;
                _topTabBarBorder.Visibility = Visibility.Visible;
                _rootGrid.RowDefinitions[0].Height = new GridLength(52);
                if (!_preFullScreenBounds.IsEmpty)
                {
                    Left = _preFullScreenBounds.Left;
                    Top = _preFullScreenBounds.Top;
                    Width = _preFullScreenBounds.Width;
                    Height = _preFullScreenBounds.Height;
                }
                WindowState = _preFullScreenState;
                ApplyLayoutPreferences();
            }
        }

        private void ToggleSidebarFold()
        {
            if (Store.Prefs.CtrlSMakesIconsOnly && !string.Equals(Store.Prefs.TabLayout, "Top", StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(Store.Prefs.TabLayout, "IconsOnly", StringComparison.OrdinalIgnoreCase))
                    Store.Prefs.TabLayout = "Left";
                else
                    Store.Prefs.TabLayout = "IconsOnly";
                Store.Prefs.SidebarCollapsed = false;
                Store.SavePrefs();
                ApplyLayoutPreferences();
                return;
            }

            bool nextCollapsed = !Store.Prefs.SidebarCollapsed;
            Store.Prefs.SidebarCollapsed = nextCollapsed;
            Store.SavePrefs();

            bool isTop = string.Equals(Store.Prefs.TabLayout, "Top", StringComparison.OrdinalIgnoreCase);
            if (!isTop && Store.Prefs.SlideSidebar && !Store.Prefs.ReduceAnimations && _sidebarBorder != null)
            {
                bool isIconsOnly = string.Equals(Store.Prefs.TabLayout, "IconsOnly", StringComparison.OrdinalIgnoreCase);
                double targetW = isIconsOnly ? 52 : (Store.Prefs.SidebarWidth >= 176 && Store.Prefs.SidebarWidth <= 440 ? Store.Prefs.SidebarWidth : 232);

                _sidebarBorder.BeginAnimation(FrameworkElement.WidthProperty, null);
                if (nextCollapsed)
                {
                    double startW = _sidebarBorder.ActualWidth > 0 ? _sidebarBorder.ActualWidth : targetW;
                    var anim = new DoubleAnimation(startW, 0, TimeSpan.FromMilliseconds(200))
                    {
                        EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                    };
                    anim.Completed += (s, e) =>
                    {
                        if (Store.Prefs.SidebarCollapsed)
                        {
                            _sidebarBorder.Visibility = Visibility.Collapsed;
                            _sidebarBorder.BeginAnimation(FrameworkElement.WidthProperty, null);
                            _sidebarBorder.Width = targetW;
                            _sidebarHoverHotzone.Visibility = Visibility.Visible;
                            if (Store.Prefs.PageAsCard) _pageCardBorder.Margin = new Thickness(8, 0, 8, 8);
                        }
                    };
                    _sidebarBorder.BeginAnimation(FrameworkElement.WidthProperty, anim);
                }
                else
                {
                    bool isRight = string.Equals(Store.Prefs.TabLayout, "Right", StringComparison.OrdinalIgnoreCase);
                    if (Store.Prefs.PageAsCard) _pageCardBorder.Margin = isRight ? new Thickness(8, 0, 0, 8) : new Thickness(0, 0, 8, 8);
                    _sidebarBorder.Width = 0;
                    _sidebarBorder.Visibility = Visibility.Visible;
                    _sidebarHoverHotzone.Visibility = Visibility.Collapsed;
                    var anim = new DoubleAnimation(0, targetW, TimeSpan.FromMilliseconds(200))
                    {
                        EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                    };
                    anim.Completed += (s, e) =>
                    {
                        if (!Store.Prefs.SidebarCollapsed)
                        {
                            _sidebarBorder.BeginAnimation(FrameworkElement.WidthProperty, null);
                            _sidebarBorder.Width = targetW;
                        }
                    };
                    _sidebarBorder.BeginAnimation(FrameworkElement.WidthProperty, anim);
                }
                return;
            }

            ApplyLayoutPreferences();
        }

        private void OnSidebarResizeDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                Store.Prefs.SidebarWidth = 232;
                if (string.Equals(Store.Prefs.TabLayout, "IconsOnly", StringComparison.OrdinalIgnoreCase))
                    Store.Prefs.TabLayout = "Left";
                Store.SavePrefs();
                ApplyLayoutPreferences();
                e.Handled = true;
                return;
            }
            _isResizingSidebar = true;
            _resizeStartScreen = PointToScreen(e.GetPosition(this));
            _resizeStartWidth = _sidebarBorder.ActualWidth > 0 ? _sidebarBorder.ActualWidth : Store.Prefs.SidebarWidth;
            _sidebarResizeGrip.CaptureMouse();
            e.Handled = true;
        }

        private void OnSidebarResizeMove(object sender, MouseEventArgs e)
        {
            if (!_isResizingSidebar) return;
            Point cur = PointToScreen(e.GetPosition(this));
            var src = PresentationSource.FromVisual(this);
            double dpiX = (src != null && src.CompositionTarget != null) ? src.CompositionTarget.TransformToDevice.M11 : 1.0;
            bool isRight = string.Equals(Store.Prefs.TabLayout, "Right", StringComparison.OrdinalIgnoreCase);
            double delta = (isRight ? (_resizeStartScreen.X - cur.X) : (cur.X - _resizeStartScreen.X)) / dpiX;
            double next = _resizeStartWidth + delta;
            if (Store.Prefs.NarrowToIcons && next < 200)
            {
                if (!string.Equals(Store.Prefs.TabLayout, "IconsOnly", StringComparison.OrdinalIgnoreCase))
                {
                    Store.Prefs.TabLayout = "IconsOnly";
                    ApplyLayoutPreferences();
                }
                return;
            }
            if (string.Equals(Store.Prefs.TabLayout, "IconsOnly", StringComparison.OrdinalIgnoreCase) && next >= 200)
            {
                Store.Prefs.TabLayout = "Left";
                ApplyLayoutPreferences();
            }
            double clamped = Math.Round(Math.Max(176, Math.Min(440, next)));
            if (Math.Abs(_sidebarBorder.Width - clamped) < 0.5) return;
            Store.Prefs.SidebarWidth = clamped;
            _sidebarBorder.Width = clamped;
            // Smoothly resize existing pin squares in-place without rebuilding the entire sidebar visual tree!
            if (_pinsPanel != null && _pinsPanel.Children.Count > 0)
            {
                double cellW = GetDynamicPinCellWidth(_pinsPanel.Children.Count);
                foreach (var child in _pinsPanel.Children)
                {
                    var sq = child as Border;
                    if (sq != null) sq.Width = cellW;
                }
            }
        }

        private void OnSidebarResizeUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isResizingSidebar) return;
            _isResizingSidebar = false;
            _sidebarResizeGrip.ReleaseMouseCapture();
            Store.SavePrefs();
            ApplyLayoutPreferences();
            e.Handled = true;
        }

        private void SetWindowBorderColor()
        {
            try
            {
                var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                if (hwnd == IntPtr.Zero) return;
                // DWMWA_BORDER_COLOR (34), COLORREF = 0x00BBGGRR
                int colorRef = Palette.IsDark ? 0x00262626 : 0x00E0E0E0;
                DwmSetWindowAttribute(hwnd, 34, ref colorRef, sizeof(int));
            }
            catch { }
            ApplyChromeThemeBrush();
        }

        private static Color ParseHexColor(string hex, Color fallback)
        {
            if (string.IsNullOrWhiteSpace(hex)) return fallback;
            string s = hex.Trim().TrimStart('#');
            if (s.Length != 6) return fallback;
            try
            {
                byte r = Convert.ToByte(s.Substring(0, 2), 16);
                byte g = Convert.ToByte(s.Substring(2, 2), 16);
                byte b = Convert.ToByte(s.Substring(4, 2), 16);
                return Color.FromRgb(r, g, b);
            }
            catch { return fallback; }
        }

        private static Color ToneThemeColor(Color c, bool dark, bool night, double intensity)
        {
            double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
            if (night && !dark) { r = r * 0.9 + 0.08; g = g * 0.9 + 0.08; b = b * 0.9 + 0.08; }
            double k = dark ? (night ? 0.8 : 0.42) : 1.0;
            r *= k; g *= k; b *= k;
            double inten = Math.Max(0.0, Math.Min(1.0, intensity));
            if (inten < 0.999)
            {
                double br = dark ? (28.0 / 255.0) : 1.0;
                double bg = dark ? (28.0 / 255.0) : 1.0;
                double bb = dark ? (28.0 / 255.0) : 1.0;
                r = r * inten + br * (1.0 - inten);
                g = g * inten + bg * (1.0 - inten);
                b = b * inten + bb * (1.0 - inten);
            }
            return Color.FromRgb(
                (byte)Math.Round(Math.Max(0, Math.Min(1, r)) * 255),
                (byte)Math.Round(Math.Max(0, Math.Min(1, g)) * 255),
                (byte)Math.Round(Math.Max(0, Math.Min(1, b)) * 255));
        }

        private static double ExtractJsonNumber(string json, string key, double fallback)
        {
            if (string.IsNullOrEmpty(json)) return fallback;
            var m = Regex.Match(json, "\"" + Regex.Escape(key) + "\"\\s*:\\s*(-?[0-9]+(?:\\.[0-9]+)?)");
            double v;
            if (m.Success && double.TryParse(m.Groups[1].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v))
                return v;
            return fallback;
        }

        private static Color BlendColor(Color baseC, Color tintC, double amount)
        {
            double t = Math.Max(0.0, Math.Min(1.0, amount));
            return Color.FromRgb(
                (byte)Math.Round(baseC.R * (1.0 - t) + tintC.R * t),
                (byte)Math.Round(baseC.G * (1.0 - t) + tintC.G * t),
                (byte)Math.Round(baseC.B * (1.0 - t) + tintC.B * t));
        }

        private static bool TryGetSpaceColorSpec(string key, out Color c1, out Color c2, out bool isGradient)
        {
            isGradient = false;
            c1 = Colors.Transparent;
            c2 = Colors.Transparent;
            switch ((key ?? "").ToLowerInvariant())
            {
                case "blue": c1 = Color.FromRgb(0x3B, 0x82, 0xF6); return true;
                case "green": c1 = Color.FromRgb(0x2E, 0xAD, 0x6B); return true;
                case "orange": c1 = Color.FromRgb(0xF5, 0x9E, 0x2D); return true;
                case "red": c1 = Color.FromRgb(0xE6, 0x53, 0x63); return true;
                case "purple": c1 = Color.FromRgb(0x9B, 0x6B, 0xF2); return true;
                case "sunset": c1 = Color.FromRgb(0xE8, 0x9B, 0x46); c2 = Color.FromRgb(0xE4, 0x6B, 0x84); isGradient = true; return true;
                case "ocean": c1 = Color.FromRgb(0x1D, 0xB9, 0xA6); c2 = Color.FromRgb(0x3B, 0x9E, 0xE8); isGradient = true; return true;
                case "mint": c1 = Color.FromRgb(0x7E, 0xC8, 0x5A); c2 = Color.FromRgb(0x22, 0xB8, 0x88); isGradient = true; return true;
                case "berry": c1 = Color.FromRgb(0xA8, 0x75, 0xF2); c2 = Color.FromRgb(0xE5, 0x73, 0x99); isGradient = true; return true;
                case "sky": c1 = Color.FromRgb(0x25, 0xB6, 0xD9); c2 = Color.FromRgb(0x5E, 0x8C, 0xF4); isGradient = true; return true;
                case "amber": c1 = Color.FromRgb(0xE5, 0x7E, 0x48); c2 = Color.FromRgb(0xE4, 0xAE, 0x3C); isGradient = true; return true;
                default: return false;
            }
        }

        private void ApplyChromeThemeBrush()
        {
            if (_topTabBarBorder == null || _sidebarBorder == null) return;
            var sp = (Store.Spaces != null && Store.Prefs != null)
                ? Store.Spaces.FirstOrDefault(s => string.Equals(s.Id, Store.Prefs.ActiveSpaceId, StringComparison.OrdinalIgnoreCase))
                : null;
            Color c1, c2;
            bool isGrad;
            if (sp != null && TryGetSpaceColorSpec(sp.ColorKey, out c1, out c2, out isGrad))
            {
                string st = (sp.ColorStrength ?? "Subtle").ToLowerInvariant();
                double amt = st == "vivid" ? 0.62 : (st == "rich" ? 0.38 : 0.20);
                Color frameBase = Palette.Frame.Color;
                if (isGrad)
                    Background = new LinearGradientBrush(BlendColor(frameBase, c1, amt), BlendColor(frameBase, c2, amt), 135.0);
                else
                    Background = new SolidColorBrush(BlendColor(frameBase, c1, amt));
                _topTabBarBorder.Background = Brushes.Transparent;
                _sidebarBorder.Background = Brushes.Transparent;
                return;
            }
            Background = Palette.Frame;
            _topTabBarBorder.Background = Palette.Frame;
            _sidebarBorder.Background = Palette.Frame;
        }

        private async void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                int pref = 2; // DWMWCP_ROUND
                DwmSetWindowAttribute(hwnd, 33, ref pref, sizeof(int));
                SetWindowBorderColor();
                InstallGlobalShortcutHook();
            }
            catch { }
            _leftPaneHost.SizeChanged += (s2, e2) => ApplyRoundedCornersToHost(_leftPaneHost);
            _rightPaneHost.SizeChanged += (s2, e2) => ApplyRoundedCornersToHost(_rightPaneHost);
            try
            {
                string browserArgs = "--enable-smooth-scrolling --disable-background-networking --disable-features=InterestFeedContentSuggestions,OptimizationHints,MediaRouter,PrivacySandboxAdsAPIsOverride,BrowsingTopics,AttributionReportingCrossAppWeb";
                if (Store.Prefs != null && Store.Prefs.BlockWebRtcLeaks)
                    browserArgs += " --force-webrtc-ip-handling-policy=disable_non_proxied_udp";
                var options = new CoreWebView2EnvironmentOptions
                {
                    AdditionalBrowserArguments = browserArgs
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
                    // StartWithFreshWindow: keep pinned & grouped tabs; discard loose unpinned tabs
                    if (Store.Prefs.StartWithFreshWindow && !st.Pinned && string.IsNullOrEmpty(st.GroupId))
                        continue;

                    var tab = new BrowserTab(st.Url, st.Title, st.Pinned, false, string.IsNullOrEmpty(st.SpaceId) ? "default" : st.SpaceId, st.GroupId)
                    {
                        Id = string.IsNullOrEmpty(st.Id) ? Guid.NewGuid().ToString("N") : st.Id,
                        KeepAwake = st.KeepAwake,
                        FaviconUrl = st.FaviconUrl,
                        HomeUrl = st.Url,
                        CustomLetter = st.CustomLetter,
                        CustomTitle = st.CustomTitle,
                        KeepInEverySpace = st.KeepInEverySpace
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

        private static void SafeSetClipboardText(string text)
        {
            if (text == null) text = "";
            for (int i = 0; i < 3; i++)
            {
                try
                {
                    // copy: false avoids the ~5s OLE clipboard flush stall on Windows 11
                    Clipboard.SetDataObject(text, false);
                    return;
                }
                catch
                {
                    if (i < 2) System.Threading.Thread.Sleep(10);
                }
            }
        }

        private void OnWindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (Store.Prefs.ConfirmBeforeQuitting && _allTabs.Count > 0)
            {
                var res = MessageBox.Show("Close Search and all open tabs?", "Quit Search", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (res != MessageBoxResult.Yes)
                {
                    e.Cancel = true;
                    return;
                }
            }
            foreach (var t in _allTabs)
            {
                if (t.IsPrivate) t.CleanupPrivateStorage();
            }
            BrowserTab.CleanupSharedPrivateSession();
            UninstallGlobalShortcutHook();
            System.Windows.Interop.ComponentDispatcher.ThreadPreprocessMessage -= OnThreadPreprocessMessage;
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
                GroupId = t.GroupId,
                CustomLetter = t.CustomLetter,
                CustomTitle = t.CustomTitle,
                KeepInEverySpace = t.KeepInEverySpace
            }).ToList();
            Store.SaveSession(list);
            if (Store.Prefs.SyncEnabled) FolderSync.PushToSyncFolder();
            if (_activeTab != null && !_activeTab.IsPrivate)
            {
                Store.Prefs.ActiveTabId = _activeTab.Id;
                Store.SavePrefs();
            }
        }

        private List<BrowserTab> GetTabsForActiveSpace()
        {
            string sp = Store.Prefs.ActiveSpaceId ?? "default";
            return _allTabs.Where(t => t.KeepInEverySpace || string.Equals(t.SpaceId, sp, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        private void AttachTabEvents(BrowserTab tab)
        {
            tab.StateChanged += t =>
            {
                Dispatcher.InvokeAsync(() =>
                {
                    RefreshTabRows();
                    UpdateMediaPlayerCard();
                    if (t == _activeTab)
                    {
                        Title = t.DisplayTitle + " — Search";
                        UpdateHelmButtons();
                        UpdateBookmarkDoorIcon();
                        if (_alwaysAddressInput != null && !_alwaysAddressInput.IsKeyboardFocused)
                            _alwaysAddressInput.Text = Address.ShownAddress(t.Url);
                    }
                    SaveCurrentSession();
                });
            };

            tab.NewTabRequested += (opener, url) =>
            {
                Dispatcher.InvokeAsync(() => CreateAndSelectTab(url, false, opener.IsPrivate, opener.GroupId, opener, false));
            };

            tab.PeekRequested += (opener, url) =>
            {
                Dispatcher.InvokeAsync(() => OpenPeekCard(url, opener.IsPrivate));
            };

            tab.WebMessageReceived += (t, json) =>
            {
                Dispatcher.InvokeAsync(async () => await HandleTabWebMessageAsync(t, json));
            };
            tab.FullScreenRequested += (t, isFs) =>
            {
                Dispatcher.InvokeAsync(() => SetFullScreen(isFs, true));
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
                string trigger = ExtractJsonValue(json, "trigger");
                string prefTrig = (Store.Prefs.PeekTrigger ?? "Shift-click").ToLowerInvariant();
                if (prefTrig == "off" || !Store.Prefs.PeekEnabled) return;
                bool ok = (prefTrig == "shift-click" && trigger == "shift")
                    || (prefTrig == "alt-shift-click" && trigger == "alt-shift")
                    || (prefTrig == "ctrl-alt-click" && trigger == "ctrl-alt")
                    || string.IsNullOrEmpty(trigger);
                if (!ok) return;
                string url = ExtractJsonValue(json, "url");
                if (!string.IsNullOrEmpty(url))
                {
                    OpenPeekCard(url, tab.IsPrivate);
                }
            }
            else if (type == "altLink")
            {
                if (!Store.Prefs.AltClickOpensSplitView) return;
                string url = ExtractJsonValue(json, "url");
                if (!string.IsNullOrEmpty(url))
                {
                    var splitTab = new BrowserTab(url, Address.DisplayHost(url), false, tab.IsPrivate, Store.Prefs.ActiveSpaceId, tab.GroupId) { OpenerTab = tab };
                    AttachTabEvents(splitTab);
                    InsertNewTab(splitTab, tab);
                    await OpenTabInSplitViewAsync(splitTab, false);
                }
            }
            else if (type == "readProgress")
            {
                var m = Regex.Match(json, "\"value\"\\s*:\\s*([0-9.]+)");
                double val;
                if (m.Success && double.TryParse(m.Groups[1].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out val))
                {
                    tab.ReadProgress = Math.Max(0, Math.Min(1, val));
                    if (tab == _activeTab && Store.Prefs.ShowReadingProgress)
                        RefreshTabRows();
                }
            }
            else if (type == "historyOpen")
            {
                if (tab.Url == null || !tab.Url.StartsWith("search://history", StringComparison.OrdinalIgnoreCase)) return;
                string url = ExtractJsonValue(json, "url");
                bool newTab = json.Contains("\"newTab\":true");
                if (!string.IsNullOrEmpty(url))
                {
                    if (newTab) CreateAndSelectTab(url, false);
                    else tab.Navigate(url);
                }
            }
            else if (type == "historyRemove")
            {
                if (tab.Url == null || !tab.Url.StartsWith("search://history", StringComparison.OrdinalIgnoreCase)) return;
                string url = ExtractJsonValue(json, "url");
                if (!string.IsNullOrEmpty(url))
                {
                    Store.History.RemoveAll(h => string.Equals(h.Url, url, StringComparison.OrdinalIgnoreCase));
                    Store.SaveHistory();
                }
            }
            else if (type == "historyClear")
            {
                if (tab.Url == null || !tab.Url.StartsWith("search://history", StringComparison.OrdinalIgnoreCase)) return;
                Store.History.Clear();
                Store.SaveHistory();
                tab.RenderHistoryTab();
            }
            else if (type == "cwsInstall")
            {
                string pageHost = Address.DisplayHost(tab.Url);
                if (!string.Equals(pageHost, "chromewebstore.google.com", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(pageHost, "chrome.google.com", StringComparison.OrdinalIgnoreCase))
                    return;
                string url = ExtractJsonValue(json, "url");
                string extId = CrxInstaller.ExtractExtensionId(url);
                if (!string.IsNullOrEmpty(url) && !string.IsNullOrEmpty(extId))
                {
                    if (MessageBox.Show("Install Chrome extension \"" + extId + "\" from the Chrome Web Store?", "Search Extensions", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                        return;
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
                if (!Store.Prefs.FillInSignIns) return;
                if (tab.Url == null || (!tab.Url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) &&
                    !tab.Url.StartsWith("http://localhost", StringComparison.OrdinalIgnoreCase) &&
                    !tab.Url.StartsWith("http://127.0.0.1", StringComparison.OrdinalIgnoreCase)))
                    return;
                string user = ExtractJsonValue(json, "user");
                string host = Address.DisplayHost(tab.Url);
                var login = Store.GetLoginsForHost(host).FirstOrDefault(l => string.Equals(l.User, user, StringComparison.OrdinalIgnoreCase));
                if (login != null)
                {
                    if (Store.Prefs.WindowsHelloForVault && !WindowsHelloVerifier.VerifyUser(this)) return;
                    await FillSavedLoginAsync(login);
                }
            }
            else if (type == "loginSubmitted")
            {
                if (tab.IsPrivate || !Store.Prefs.OfferToSavePasswords) return;
                if (tab.Url == null || (!tab.Url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) &&
                    !tab.Url.StartsWith("http://localhost", StringComparison.OrdinalIgnoreCase) &&
                    !tab.Url.StartsWith("http://127.0.0.1", StringComparison.OrdinalIgnoreCase)))
                    return;
                string actualHost = Address.DisplayHost(tab.Url);
                string user = ExtractJsonValue(json, "user");
                string pass = ExtractJsonValue(json, "pass");
                if (!string.IsNullOrEmpty(actualHost) && !string.IsNullOrEmpty(user) && !string.IsNullOrEmpty(pass))
                {
                    _pendingSaveHost = Store.NormalizeHost(actualHost);
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
                string sig = (ctrl ? "C" : "") + (alt ? "A" : "") + (shift ? "S" : "") + ":" + key;
                if (!ShouldRunShortcutOnce(sig)) return;
                if (key == "escape")
                {
                    HandleEscapeKey();
                    return;
                }
                if (key == "f11")
                {
                    ToggleFullScreen();
                    return;
                }
                if (key == "f12" || (ctrl && shift && key == "i"))
                {
                    if (_activeTab != null && _activeTab.IsWebViewInitialized && _activeTab.WebView != null)
                        _activeTab.WebView.CoreWebView2.OpenDevToolsWindow();
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
                    if (_findPopup != null && _findPopup.IsOpen && !string.IsNullOrEmpty(_findInput.Text))
                        await ExecuteFindOnPageAsync(_findInput.Text, !shift);
                    else
                        ShowFindBar();
                    return;
                }
                if (ctrl && key == "f4")
                {
                    if (_activeTab != null) CloseTab(_activeTab);
                    return;
                }
                if (ctrl && (key == "tab" || key == "pagedown" || key == "pageup"))
                {
                    if (key == "tab") StepMruSwitcher(shift);
                    else await StepAdjacentTabAsync(key == "pageup");
                    return;
                }
                if (alt && !ctrl && !shift && key == "home")
                {
                    if (_activeTab != null && _activeTab.Pinned && !string.IsNullOrEmpty(_activeTab.HomeUrl))
                        _activeTab.Navigate(_activeTab.HomeUrl);
                    return;
                }
                if (alt && shift && !ctrl && (key == "arrowleft" || key == "arrowright"))
                {
                    FocusSplitPane(key == "arrowleft");
                    return;
                }
                if (alt && !ctrl && !shift && key.Length == 1 && key[0] >= '1' && key[0] <= '9')
                {
                    int spIdx = key[0] - '1';
                    if (Store.Prefs.CtrlNumbersSwitchSpaces)
                    {
                        var spTabs = GetTabsForActiveSpace();
                        if (spIdx == 8 && spTabs.Count > 0) spIdx = spTabs.Count - 1;
                        if (spIdx < spTabs.Count) await SelectTabAsync(spTabs[spIdx]);
                    }
                    else if (spIdx < Store.Spaces.Count)
                    {
                        CycleSpace(spIdx);
                    }
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
                if (alt && shift && key == "n")
                {
                    ToggleSplitView();
                    return;
                }
                if (alt && shift && key == "l")
                {
                    OpenOrSelectSettingsTab("passwords");
                    return;
                }
                if (alt && shift && key == "c")
                {
                    if (_activeTab != null && !string.IsNullOrEmpty(_activeTab.Url))
                        SafeSetClipboardText(CleanLink.Clean(_activeTab.Url, true));
                    return;
                }
                if (alt && shift && key == "d")
                {
                    if (_activeTab != null)
                    {
                        PageFeatures.ToggleDuskForSite(Address.DisplayHost(_activeTab.Url));
                        await _activeTab.ApplySiteStyleSheetsAsync();
                    }
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
                if (!string.Equals(tab.Url, "about:blank", StringComparison.OrdinalIgnoreCase)) return;
                string text = ExtractJsonValue(json, "text");
                if (!string.IsNullOrWhiteSpace(text))
                    tab.Navigate(text);
            }
            else if (type == "openSettings")
            {
                if (!string.Equals(tab.Url, "about:blank", StringComparison.OrdinalIgnoreCase)) return;
                OpenOrSelectSettingsTab("general");
            }
            else if (type == "settingsAction")
            {
                if (tab.Url == null || !tab.Url.StartsWith("search://settings", StringComparison.OrdinalIgnoreCase)) return;
                string action = ExtractJsonValue(json, "action");
                string arg = ExtractJsonValue(json, "arg");
                string sec = ExtractJsonValue(json, "sec");
                int scrollY = 0;
                var sm = Regex.Match(json, "\"scrollY\"\\s*:\\s*(\\d+)");
                if (sm.Success) int.TryParse(sm.Groups[1].Value, out scrollY);
                await HandleSettingsTabActionAsync(tab, action, arg, string.IsNullOrEmpty(sec) ? "general" : sec, scrollY);
            }
        }

        private async Task HandleSettingsTabActionAsync(BrowserTab settingsTab, string action, string arg, string sec = "general", int scrollY = 0)
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
                            ApplyLayoutPreferences(false);
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
                                    Store.Prefs.SearchEngineUrl = Address.GetSearchEngineUrl(pVal);
                                }
                                else if (pName == "SleepLevel")
                                {
                                    Store.Prefs.AutoSleepMinutes = pVal == "Never" ? 99999 : pVal == "Later" ? 120 : pVal == "Sooner" ? 10 : 30;
                                }
                                else if (pName == "PeekTrigger")
                                {
                                    Store.Prefs.PeekEnabled = !string.Equals(pVal, "Off", StringComparison.OrdinalIgnoreCase);
                                }
                                Store.SavePrefs();
                                ApplyLayoutPreferences(false);
                                settingsTab.RenderSettingsTab(sec, scrollY);
                            }
                        }
                    }
                    break;
                case "saveHomeTheme":
                case "saveHomeThemeReload":
                    {
                        string themeId = ExtractJsonValue(arg ?? "", "theme");
                        bool shuf = (arg ?? "").Contains("\"shuffle\":true");
                        var tm = Regex.Match(arg ?? "", "\"tuning\"\\s*:\\s*(\\{[^}]*\\})");
                        if (!string.IsNullOrEmpty(themeId)) Store.Prefs.HomeThemeId = themeId;
                        if (tm.Success) Store.Prefs.HomeTuningJson = tm.Groups[1].Value;
                        Store.Prefs.HomeShuffle = shuf;
                        Store.SavePrefs();
                        ApplyChromeThemeBrush();
                        foreach (var bt in _allTabs)
                        {
                            if (bt.Url == "about:blank" && bt.IsWebViewInitialized) bt.RenderBlankWall();
                        }
                        if (action == "saveHomeThemeReload") settingsTab.RenderSettingsTab("themes", scrollY);
                    }
                    break;
                case "removeBgImage":
                    HomeThemesJs.RemoveCustomImageCopy();
                    Store.Prefs.HomeImagePath = "";
                    Store.Prefs.BlankWallCustomValue = "";
                    if (Store.Prefs.HomeThemeId == "custom") Store.Prefs.HomeThemeId = "off";
                    Store.SavePrefs();
                    ApplyChromeThemeBrush();
                    foreach (var bt in _allTabs) { if (bt.Url == "about:blank" && bt.IsWebViewInitialized) bt.RenderBlankWall(); }
                    settingsTab.RenderSettingsTab("themes", scrollY);
                    break;
                case "chooseDownloadsFolder":
                    Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
                    {
                        using (var fbd = new System.Windows.Forms.FolderBrowserDialog())
                        {
                            fbd.Description = "Choose default Downloads folder:";
                            string curDl = Store.Prefs.DownloadsFolder;
                            if (!string.IsNullOrWhiteSpace(curDl) && !curDl.StartsWith("~") && Directory.Exists(curDl))
                                fbd.SelectedPath = curDl;
                            if (fbd.ShowDialog(GetWin32Owner()) == System.Windows.Forms.DialogResult.OK && !string.IsNullOrWhiteSpace(fbd.SelectedPath))
                            {
                                Store.Prefs.DownloadsFolder = fbd.SelectedPath;
                                Store.SavePrefs();
                                foreach (var t in _allTabs)
                                {
                                    if (t.WebView != null && t.IsWebViewInitialized && t.WebView.CoreWebView2 != null)
                                    {
                                        try { t.WebView.CoreWebView2.Profile.DefaultDownloadFolderPath = fbd.SelectedPath; } catch { }
                                    }
                                }
                                settingsTab.RenderSettingsTab("general", scrollY);
                            }
                        }
                    }));
                    break;
                case "chooseBgImage":
                    Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
                    {
                        using (var ofd = new System.Windows.Forms.OpenFileDialog())
                        {
                            ofd.Title = "Choose Home Background Image";
                            ofd.Filter = "Images|*.png;*.jpg;*.jpeg;*.webp;*.bmp;*.gif";
                            if (ofd.ShowDialog(GetWin32Owner()) == System.Windows.Forms.DialogResult.OK && !string.IsNullOrWhiteSpace(ofd.FileName))
                            {
                                string copied = HomeThemesJs.SaveCustomImageCopy(ofd.FileName);
                                Store.Prefs.HomeImagePath = copied;
                                Store.Prefs.HomeThemeId = "custom";
                                Store.Prefs.BlankWallMode = "Image";
                                Store.Prefs.BlankWallCustomValue = copied;
                                Store.SavePrefs();
                                ApplyChromeThemeBrush();
                                foreach (var bt in _allTabs) { if (bt.Url == "about:blank" && bt.IsWebViewInitialized) bt.RenderBlankWall(); }
                                settingsTab.RenderSettingsTab("themes", scrollY);
                            }
                        }
                    }));
                    break;
                case "loadUnpackedExt":
                    Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(async () =>
                    {
                        using (var fbd = new System.Windows.Forms.FolderBrowserDialog())
                        {
                            fbd.Description = "Select unpacked extension folder containing manifest.json:";
                            if (fbd.ShowDialog(GetWin32Owner()) == System.Windows.Forms.DialogResult.OK && !string.IsNullOrWhiteSpace(fbd.SelectedPath))
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
                                    settingsTab.RenderSettingsTab("extensions", scrollY);
                                }
                                catch (Exception ex)
                                {
                                    MessageBox.Show("Could not load unpacked extension: " + ex.Message, "Search Extensions", MessageBoxButton.OK, MessageBoxImage.Warning);
                                }
                            }
                        }
                    }));
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
                    if (arg == "reset")
                    {
                        Store.Prefs.DefaultZoomPercent = 100;
                    }
                    else
                    {
                        int delta = 0;
                        if (!int.TryParse(arg, out delta)) delta = arg == "+10" ? 10 : -10;
                        Store.Prefs.DefaultZoomPercent = Math.Max(50, Math.Min(300, Store.Prefs.DefaultZoomPercent + delta));
                    }
                    Store.SavePrefs();
                    foreach (var zt in _allTabs)
                    {
                        if (zt.WebView != null && zt.IsWebViewInitialized && (zt.Url == null || !zt.Url.StartsWith("search://")))
                            zt.WebView.ZoomFactor = Store.Prefs.DefaultZoomPercent / 100.0;
                    }
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
                    Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
                    {
                        using (var fbd = new System.Windows.Forms.FolderBrowserDialog())
                        {
                            fbd.Description = "Choose a shared folder (OneDrive, Dropbox, or iCloud) for Search Sync:";
                            if (fbd.ShowDialog(GetWin32Owner()) == System.Windows.Forms.DialogResult.OK && !string.IsNullOrWhiteSpace(fbd.SelectedPath))
                            {
                                Store.Prefs.SyncFolderPath = fbd.SelectedPath;
                                Store.SavePrefs();
                                FolderSync.InitializeWatcher(() => Dispatcher.InvokeAsync(() => RefreshTabRows()));
                                settingsTab.RenderSettingsTab("general", scrollY);
                            }
                        }
                    }));
                    break;
                case "openDownloadsFolder":
                    try
                    {
                        string dl = Store.Prefs.DownloadsFolder;
                        if (string.IsNullOrWhiteSpace(dl) || dl.StartsWith("~"))
                        {
                            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                            dl = string.IsNullOrWhiteSpace(dl)
                                ? Path.Combine(home, "Downloads")
                                : Path.Combine(home, dl.Substring(1).TrimStart('\\', '/'));
                        }
                        if (!Directory.Exists(dl)) dl = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
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
                    ApplyLayoutPreferences(false);
                    foreach (var t in _allTabs)
                    {
                        if (t.IsWebViewInitialized && t.WebView != null && t.WebView.CoreWebView2 != null &&
                            t.Url != null && t.Url.StartsWith("search://settings", StringComparison.OrdinalIgnoreCase))
                        {
                            try
                            {
                                await t.WebView.CoreWebView2.ExecuteScriptAsync(
                                    "if(window.__applyLookVars)window.__applyLookVars(" + (Palette.IsDark ? "true" : "false") + ",'" + (Store.Prefs.Appearance ?? "System") + "');");
                            }
                            catch { }
                        }
                    }
                    break;
                case "toggleDusk":
                    Store.Prefs.DuskEnabled = !Store.Prefs.DuskEnabled;
                    Store.SavePrefs();
                    ApplyLayoutPreferences(false);
                    settingsTab.RenderSettingsTab("appearance", scrollY);
                    break;
                case "setTabLayout":
                    Store.Prefs.TabLayout = string.IsNullOrEmpty(arg) ? "Left" : arg;
                    Store.SavePrefs();
                    ApplyLayoutPreferences(false);
                    settingsTab.RenderSettingsTab("appearance", scrollY);
                    break;
                case "toggleFold":
                    Store.Prefs.SidebarCollapsed = !Store.Prefs.SidebarCollapsed;
                    Store.SavePrefs();
                    ApplyLayoutPreferences(false);
                    settingsTab.RenderSettingsTab("appearance", scrollY);
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
                                if (!string.IsNullOrEmpty(dec)) SafeSetClipboardText(dec);
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
                            await UninstallExtensionAsync(ext, settingsTab);
                            settingsTab.RenderSettingsTab("extensions");
                        }
                    }
                    break;
            }
        }

        private async Task UninstallExtensionAsync(ExtensionInfo ext, BrowserTab hostTab)
        {
            try
            {
                var tab = hostTab ?? _activeTab;
                if (tab != null && tab.WebView != null && tab.IsWebViewInitialized && tab.WebView.CoreWebView2 != null)
                {
                    var getMethod = tab.WebView.CoreWebView2.Profile.GetType().GetMethod("GetBrowserExtensionsAsync");
                    if (getMethod != null)
                    {
                        dynamic listTask = getMethod.Invoke(tab.WebView.CoreWebView2.Profile, null);
                        if (listTask != null)
                        {
                            var exts = await listTask;
                            foreach (var rtExt in exts)
                            {
                                string rtId = "";
                                string rtName = "";
                                try { rtId = rtExt.Id; } catch { }
                                try { rtName = rtExt.Name; } catch { }
                                if ((!string.IsNullOrEmpty(ext.RuntimeExtensionId) && rtId == ext.RuntimeExtensionId) ||
                                    (!string.IsNullOrEmpty(rtName) && string.Equals(rtName, ext.Name, StringComparison.OrdinalIgnoreCase)))
                                {
                                    try { await rtExt.RemoveAsync(); } catch { }
                                }
                            }
                        }
                    }
                }
            }
            catch { }

            try
            {
                if (!string.IsNullOrEmpty(ext.FolderPath) &&
                    ext.FolderPath.StartsWith(Store.ExtensionsDir, StringComparison.OrdinalIgnoreCase) &&
                    Directory.Exists(ext.FolderPath))
                {
                    Directory.Delete(ext.FolderPath, true);
                }
            }
            catch { }

            Store.Extensions.Remove(ext);
            Store.SaveExtensions();
        }

        private async Task CheckForUpdatesAsync()
        {
            const string repo = "busyflow/Search-Browser";
            const string currentVersion = "0.0013";
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
                string safeTag = Regex.Replace(tag ?? "", "[^0-9A-Za-z._-]", "");
                if (string.IsNullOrEmpty(safeTag) || safeTag == currentVersion)
                {
                    MessageBox.Show("You are on the latest version (v" + currentVersion + ").", "Search Updates", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                var m = Regex.Match(json, "\"browser_download_url\"\\s*:\\s*\"([^\"]*Search-Setup\\.exe)\"");
                if (!m.Success)
                {
                    MessageBox.Show("New version v" + safeTag + " is available on GitHub, but no Search-Setup.exe asset was found.", "Search Updates", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                string dlUrl = m.Groups[1].Value;
                if (!dlUrl.StartsWith("https://github.com/" + repo + "/", StringComparison.OrdinalIgnoreCase) &&
                    !dlUrl.StartsWith("https://objects.githubusercontent.com/", StringComparison.OrdinalIgnoreCase))
                {
                    MessageBox.Show("Blocked untrusted update download URL.", "Search Updates", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                if (MessageBox.Show("Update available: v" + safeTag + " (current: v" + currentVersion + ").\n\nDownload and install now?", "Search Updates", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    string setupPath = Path.Combine(Path.GetTempPath(), "Search-Setup-" + safeTag + ".exe");
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

        private void InsertNewTab(BrowserTab tab, BrowserTab opener)
        {
            if (opener != null && Store.Prefs.LinksInFrontBesidePage)
            {
                int oIdx = _allTabs.IndexOf(opener);
                if (oIdx >= 0) { _allTabs.Insert(oIdx + 1, tab); return; }
            }
            string pos = Store.Prefs.NewTabPosition ?? "After current";
            if (string.Equals(pos, "Top", StringComparison.OrdinalIgnoreCase))
            {
                _allTabs.Insert(0, tab);
            }
            else if (string.Equals(pos, "After current", StringComparison.OrdinalIgnoreCase) && _activeTab != null)
            {
                int aIdx = _allTabs.IndexOf(_activeTab);
                if (aIdx >= 0) _allTabs.Insert(aIdx + 1, tab);
                else _allTabs.Add(tab);
            }
            else
            {
                _allTabs.Add(tab);
            }
        }

        public async void CreateAndSelectTab(string url, bool openOmniboxIfBlank, bool isPrivate = false, string groupId = null, BrowserTab opener = null, bool background = false)
        {
            string resolved = (string.IsNullOrWhiteSpace(url) || url == "about:blank" || url.StartsWith("search://", StringComparison.OrdinalIgnoreCase))
                ? (string.IsNullOrWhiteSpace(url) ? "about:blank" : url)
                : (Address.TryParseUrl(url) ?? Address.SearchUrl(url));
            var tab = new BrowserTab(resolved, resolved == "about:blank" ? "New Tab" : Address.DisplayHost(resolved), false, isPrivate, Store.Prefs.ActiveSpaceId, groupId)
            {
                OpenerTab = opener,
                HomeUrl = resolved
            };
            AttachTabEvents(tab);
            InsertNewTab(tab, opener);
            if (!background)
            {
                await SelectTabAsync(tab);
            }
            else
            {
                RefreshTabRows();
                SaveCurrentSession();
            }
        }

        private int _lastHistoryRenderedCount = -1;
        private string _lastShortcutSig = "";
        private int _lastShortcutTick = 0;

        private bool ShouldRunShortcutOnce(string sig)
        {
            int now = Environment.TickCount;
            if (sig == _lastShortcutSig && Math.Abs(now - _lastShortcutTick) < 120)
                return false;
            _lastShortcutSig = sig;
            _lastShortcutTick = now;
            return true;
        }

        public async void OpenOrSelectHistoryTab()
        {
            var existing = GetTabsForActiveSpace().FirstOrDefault(t => t.Url != null && t.Url.StartsWith("search://history", StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                await SelectTabAsync(existing);
                if (_lastHistoryRenderedCount != Store.History.Count)
                {
                    _lastHistoryRenderedCount = Store.History.Count;
                    existing.RenderHistoryTab();
                }
                return;
            }
            _lastHistoryRenderedCount = Store.History.Count;
            var tab = new BrowserTab("search://history", "History", false, false, Store.Prefs.ActiveSpaceId);
            AttachTabEvents(tab);
            InsertNewTab(tab, _activeTab);
            await SelectTabAsync(tab);
            RefreshTabRows();
        }

        public async void OpenOrSelectSettingsTab(string section = "general")
        {
            var existing = GetTabsForActiveSpace().FirstOrDefault(t => t.Url != null && t.Url.StartsWith("search://settings", StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                await SelectTabAsync(existing);
                if (!string.IsNullOrEmpty(section) && section != "general" && existing.IsWebViewInitialized && existing.WebView != null && existing.WebView.CoreWebView2 != null)
                {
                    try
                    {
                        string safeSec = Regex.Replace(section, "[^a-zA-Z0-9_-]", "");
                        await existing.WebView.CoreWebView2.ExecuteScriptAsync("if(typeof showSec==='function')showSec('" + safeSec + "');");
                    }
                    catch { }
                }
                return;
            }
            var tab = new BrowserTab("search://settings/" + section, "Settings", false, false, Store.Prefs.ActiveSpaceId);
            AttachTabEvents(tab);
            InsertNewTab(tab, _activeTab);
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
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
            {
                ApplyRoundedCornersToHost(_leftPaneHost);
                ApplyRoundedCornersToHost(_rightPaneHost);
            }));

            _mruTabs.Remove(tab);
            _mruTabs.Insert(0, tab);
            _veilBanner.Visibility = tab.IsVeilPicking ? Visibility.Visible : Visibility.Collapsed;
            Title = tab.DisplayTitle + " — Search";
            UpdateHelmButtons();
            UpdateBookmarkDoorIcon();
            UpdateMediaPlayerCard();
            if (_alwaysAddressInput != null && !_alwaysAddressInput.IsKeyboardFocused)
                _alwaysAddressInput.Text = Address.ShownAddress(tab.Url);
            RefreshTabRows();
            SaveCurrentSession();
        }

        private async void CloseTab(BrowserTab tab, bool forceClosePin = false, bool deletePin = false)
        {
            if (tab == null) return;

            // Oriel PinsKeepSite: Ctrl+W on a wandered pinned tab returns it home instead of closing
            if (!forceClosePin && !deletePin && tab.Pinned && Store.Prefs.PinsKeepSite && !string.IsNullOrEmpty(tab.HomeUrl) &&
                !string.Equals(tab.Url, tab.HomeUrl, StringComparison.OrdinalIgnoreCase))
            {
                tab.Navigate(tab.HomeUrl);
                return;
            }

            // Closing a pinned tab (without Delete) closes its WebView and darkens the pin, keeping it in the pin bar!
            if (tab.Pinned && !deletePin)
            {
                if (_isSplitView && (tab == _splitLeftTab || tab == _splitRightTab))
                    ExitSplitView(tab == _splitLeftTab ? _splitRightTab : _splitLeftTab);
                tab.KeepAwake = false;
                tab.PutToSleep();
                if (_activeTab == tab)
                {
                    var spaceTabsRem = GetTabsForActiveSpace();
                    var next = spaceTabsRem.FirstOrDefault(t => t != tab && !t.IsSleeping)
                        ?? spaceTabsRem.FirstOrDefault(t => t != tab && !t.Pinned);
                    if (next != null) await SelectTabAsync(next);
                    else CreateAndSelectTab("about:blank", false);
                }
                RefreshTabRows();
                SaveCurrentSession();
                return;
            }

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

            // Compute index within current space (fixes cross-space index bug)
            var prevSpaceTabs = GetTabsForActiveSpace();
            int spaceIdx = prevSpaceTabs.IndexOf(tab);
            bool wasUnpinned = !tab.Pinned;

            _allTabs.Remove(tab);
            _mruTabs.Remove(tab);
            if (tab.IsPrivate && !_allTabs.Any(t => t.IsPrivate))
                BrowserTab.CleanupSharedPrivateSession();
            try { if (tab.WebView != null) tab.WebView.Dispose(); } catch { }

            var spaceTabs = GetTabsForActiveSpace();
            if (spaceTabs.Count == 0)
            {
                CreateAndSelectTab("about:blank", true);
                return;
            }

            // LeaveNewTabOnLastClose: if the last unpinned tab closed and only pinned tabs remain
            if (wasUnpinned && Store.Prefs.LeaveNewTabOnLastClose && !spaceTabs.Any(t => !t.Pinned))
            {
                CreateAndSelectTab("about:blank", false);
                return;
            }

            if (_activeTab == tab)
            {
                BrowserTab next = null;
                if (Store.Prefs.CloseTabGoesToPrevious)
                {
                    next = _mruTabs.FirstOrDefault(t => spaceTabs.Contains(t));
                }
                if (next == null)
                {
                    int targetIdx = spaceIdx >= 0 ? Math.Max(0, Math.Min(spaceTabs.Count - 1, spaceIdx - 1)) : 0;
                    next = spaceTabs[targetIdx];
                }
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
            if (_splitClosePopup != null) _splitClosePopup.IsOpen = false;
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

        private void RepositionSplitClosePopup()
        {
            if (_splitClosePopup == null) return;
            if (!_isSplitView || _rightPaneHost == null || _rightPaneHost.Visibility != Visibility.Visible)
            {
                _splitClosePopup.IsOpen = false;
                return;
            }
            double w = _rightPaneHost.ActualWidth;
            if (w > 40)
            {
                _splitClosePopup.HorizontalOffset = w - 42;
                _splitClosePopup.VerticalOffset = 10;
                _splitClosePopup.IsOpen = true;
            }
        }

        private void UpdateSplitFocusBorders()
        {
            _leftPaneHost.BorderThickness = new Thickness(0);
            _rightPaneHost.BorderThickness = new Thickness(0);
            RepositionSplitClosePopup();
        }

        private async Task OpenTabInSplitViewAsync(BrowserTab other, bool putOnLeft)
        {
            if (other == null || other == _activeTab) return;
            await other.EnsureAwakeAsync(_sharedEnv, putOnLeft ? _leftPaneHost : _rightPaneHost);
            _isSplitView = true;
            if (putOnLeft)
            {
                _splitLeftTab = other;
                _splitRightTab = _activeTab;
            }
            else
            {
                _splitLeftTab = _activeTab;
                _splitRightTab = other;
            }
            _stageGrid.ColumnDefinitions[0].Width = new GridLength(1, GridUnitType.Star);
            _stageGrid.ColumnDefinitions[2].Width = new GridLength(1, GridUnitType.Star);
            _splitDivider.Visibility = Visibility.Visible;
            _rightPaneHost.Visibility = Visibility.Visible;
            _leftPaneHost.Child = _splitLeftTab != null ? _splitLeftTab.WebView : null;
            _rightPaneHost.Child = _splitRightTab != null ? _splitRightTab.WebView : null;
            _activeTab = other;
            UpdateSplitFocusBorders();
            RefreshTabRows();
        }

        private void FocusSplitPane(bool left)
        {
            if (!_isSplitView) return;
            var target = left ? _splitLeftTab : _splitRightTab;
            if (target != null && _activeTab != target)
            {
                _activeTab = target;
                UpdateSplitFocusBorders();
                RefreshTabRows();
                if (target.WebView != null) target.WebView.Focus();
            }
        }

        private void CheckTabAutoSleep()
        {
            int mins = Store.Prefs.AutoSleepMinutes;
            if (Store.Prefs.EnergySaver && mins > 10) mins = 10;
            if (mins > 0 && mins < 90000)
            {
                DateTime cutoff = DateTime.UtcNow.AddMinutes(-mins);
                foreach (var tab in _allTabs)
                {
                    if (tab != _activeTab && tab != _splitLeftTab && tab != _splitRightTab &&
                        !tab.KeepAwake && !tab.IsSleeping && !tab.IsAudible && tab.LastTouchedUtc < cutoff)
                    {
                        tab.PutToSleep();
                    }
                }
            }

            // CloseIdleTabs (Oriel auto-archive: Never / After 12 hours / After 1 day / After 7 days / After a month)
            string idleSetting = Store.Prefs.CloseIdleTabs ?? "Never";
            TimeSpan idleSpan = TimeSpan.Zero;
            if (idleSetting == "After 12 hours") idleSpan = TimeSpan.FromHours(12);
            else if (idleSetting == "After 1 day") idleSpan = TimeSpan.FromDays(1);
            else if (idleSetting == "After 7 days") idleSpan = TimeSpan.FromDays(7);
            else if (idleSetting == "After a month") idleSpan = TimeSpan.FromDays(30);

            if (idleSpan > TimeSpan.Zero)
            {
                DateTime idleCutoff = DateTime.UtcNow - idleSpan;
                var toClose = _allTabs.Where(t =>
                    t != _activeTab && t != _splitLeftTab && t != _splitRightTab &&
                    !t.Pinned && string.IsNullOrEmpty(t.GroupId) && !t.KeepAwake && !t.IsAudible &&
                    t.LastTouchedUtc < idleCutoff).ToList();
                foreach (var t in toClose) CloseTab(t);
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
            ShowSpaceToast(Store.Spaces[nextIdx].Name);

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

        private void ShowSpaceToast(string spaceName)
        {
            if (_spaceToast == null || _spaceToastText == null) return;
            _spaceToastText.Text = spaceName;
            _spaceToastText.Foreground = Palette.Ink;
            _spaceToast.Background = Palette.FrameTile;
            _spaceToast.Visibility = Visibility.Visible;
            if (_spaceToastTimer == null)
            {
                _spaceToastTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1400) };
                _spaceToastTimer.Tick += (s, e) =>
                {
                    _spaceToastTimer.Stop();
                    _spaceToast.Visibility = Visibility.Collapsed;
                };
            }
            _spaceToastTimer.Stop();
            _spaceToastTimer.Start();
        }

        private void RefreshSpaceHeader()
        {
            var sp = Store.Spaces.FirstOrDefault(s => s.Id == Store.Prefs.ActiveSpaceId) ?? Store.Spaces[0];
            _spaceTitleText.Text = "●  " + sp.Name;
            if (_spaceFooterBtn != null)
            {
                _spaceFooterBtn.Child = VectorIcons.Draw(VectorIcons.GetSpaceIconPath(sp.IconName), Palette.FrameMuted, 13, 1.5);
                _spaceFooterBtn.ToolTip = sp.Name + " (Alt+1..9)";
            }
            ApplyChromeThemeBrush();

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

        private void UpdateHelmButtons()
        {
            bool canBack = _activeTab != null && _activeTab.WebView != null && _activeTab.WebView.CanGoBack;
            bool canFwd = _activeTab != null && _activeTab.WebView != null && _activeTab.WebView.CanGoForward;
            bool loading = _activeTab != null && _activeTab.IsLoading;
            if (_backBtn != null)
            {
                _backBtn.Opacity = canBack ? 1.0 : 0.3;
                _backBtn.IsHitTestVisible = canBack;
            }
            if (_forwardBtn != null)
            {
                _forwardBtn.Opacity = canFwd ? 1.0 : 0.3;
                _forwardBtn.IsHitTestVisible = canFwd;
            }
            if (_reloadBtn != null)
            {
                _reloadBtn.Child = VectorIcons.Draw(loading ? VectorIcons.StopPath : VectorIcons.ReloadPath, Palette.FrameMuted, 11, 1.45);
                _reloadBtn.ToolTip = loading ? "Stop loading (Esc)" : "Reload (Ctrl+R)";
            }
            if (_loadingLine != null)
            {
                _loadingLine.Background = Palette.Ink;
                _loadingLine.Visibility = (Store.Prefs.SidebarCollapsed && Store.Prefs.LoadingLineWhenHidden && loading)
                    ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void UpdateBookmarkDoorIcon()
        {
            if (_bmDoor == null) return;
            bool bookmarked = _activeTab != null && !string.IsNullOrEmpty(_activeTab.Url) &&
                Store.Bookmarks.Any(b => string.Equals(b.Url, _activeTab.Url, StringComparison.OrdinalIgnoreCase));
            _bmDoor.Child = bookmarked
                ? VectorIcons.DrawFilled(VectorIcons.BookmarkPath, Palette.Ink, 11)
                : VectorIcons.Draw(VectorIcons.BookmarkPath, Palette.FrameMuted, 11, 1.45);
        }

        private void UpdateFooterButtons()
        {
            bool buttonsTop = Store.Prefs.ButtonsAtTop;
            bool isTop = string.Equals(Store.Prefs.TabLayout, "Top", StringComparison.OrdinalIgnoreCase);
            bool showAtTop = buttonsTop || isTop;
            bool hasActiveDl = Downloads.Any(d => d.State == "Downloading...");
            bool showDl = Store.Prefs.AlwaysShowDownloadsButton || hasActiveDl;
            if (_topExtDoor != null) _topExtDoor.Visibility = (showAtTop && Store.Prefs.ShowExtBtn) ? Visibility.Visible : Visibility.Collapsed;
            if (_topDlDoor != null) _topDlDoor.Visibility = (showAtTop && showDl) ? Visibility.Visible : Visibility.Collapsed;
            if (_bmDoor != null) _bmDoor.Visibility = (showAtTop && Store.Prefs.ShowBookmarksBtn) ? Visibility.Visible : Visibility.Collapsed;

            if (_spaceFooterBtn != null) _spaceFooterBtn.Visibility = (Store.Prefs.SpacesEnabled || Store.Prefs.ShowProfileBtn) ? Visibility.Visible : Visibility.Collapsed;
            if (_bmFooterBtn != null) _bmFooterBtn.Visibility = (!showAtTop && Store.Prefs.ShowBookmarksBtn) ? Visibility.Visible : Visibility.Collapsed;
            if (_extFooterBtn != null) _extFooterBtn.Visibility = (!showAtTop && Store.Prefs.ShowExtBtn) ? Visibility.Visible : Visibility.Collapsed;
            if (_dlFooterBtn != null) _dlFooterBtn.Visibility = (!showAtTop && showDl) ? Visibility.Visible : Visibility.Collapsed;
        }

        private void UpdateMediaPlayerCard()
        {
            if (_mediaPlayerCard == null) return;
            if (!Store.Prefs.ShowPlayingInOtherTabs || string.Equals(Store.Prefs.TabLayout, "IconsOnly", StringComparison.OrdinalIgnoreCase))
            {
                _mediaPlayerCard.Visibility = Visibility.Collapsed;
                return;
            }
            var playing = _allTabs.FirstOrDefault(t => t != _activeTab && t.IsAudible);
            if (playing == null)
            {
                _mediaPlayerCard.Visibility = Visibility.Collapsed;
                return;
            }
            _mediaPlayerCard.Background = Palette.FrameTile;
            var dock = new DockPanel();
            var muteBtn = new Border
            {
                Width = 22, Height = 22, CornerRadius = new CornerRadius(6),
                Background = Brushes.Transparent, Cursor = Cursors.Hand,
                ToolTip = playing.IsMuted ? "Unmute" : "Mute",
                Child = new TextBlock { Text = playing.IsMuted ? "🔇" : "🔊", FontSize = 11, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
            };
            var capTab = playing;
            muteBtn.MouseLeftButtonUp += (s, e) =>
            {
                e.Handled = true;
                capTab.IsMuted = !capTab.IsMuted;
                if (capTab.WebView != null && capTab.IsWebViewInitialized && capTab.WebView.CoreWebView2 != null)
                    capTab.WebView.CoreWebView2.IsMuted = capTab.IsMuted;
                UpdateMediaPlayerCard();
            };
            DockPanel.SetDock(muteBtn, Dock.Right);
            dock.Children.Add(muteBtn);

            var txt = new TextBlock
            {
                Text = playing.DisplayTitle,
                FontSize = 11.5,
                Foreground = Palette.Ink,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Cursor = Cursors.Hand
            };
            txt.MouseLeftButtonUp += async (s, e) => { e.Handled = true; await SelectTabAsync(capTab); };
            dock.Children.Add(txt);
            _mediaPlayerCard.Child = dock;
            _mediaPlayerCard.Visibility = Visibility.Visible;
        }

        private void ApplyCaptionButtonStyle()
        {
            bool classic = string.Equals(Store.Prefs.WindowButtonsStyle, "Classic", StringComparison.OrdinalIgnoreCase);
            Border[] btns = new[] { _minBtn, _maxBtn, _closeBtn };
            foreach (var b in btns)
            {
                if (b == null) continue;
                b.Width = classic ? 46 : 28;
                b.Height = classic ? 40 : 28;
                b.CornerRadius = new CornerRadius(classic ? 0 : 8);
                b.Margin = classic ? new Thickness(0) : new Thickness(2, 0, 0, 0);
            }
            if (_topTabBarBorder != null)
                _topTabBarBorder.Padding = classic ? new Thickness(10, 0, 0, 0) : new Thickness(10, 0, 10, 0);
        }

        private void ApplyLayoutPreferences(bool reloadSettingsTabs = true)
        {
            Palette.ResolveTheme();
            SetWindowBorderColor();
            Background = Palette.Frame;
            _sidebarBorder.Background = Palette.Frame;
            if (_glidingTabPill != null)
            {
                _glidingTabPill.Background = Palette.FrameWash;
                _glidingTabPill.Height = Store.Prefs.RoomierTabs ? 36 : 28;
            }
            _topTabBarBorder.Background = Palette.Frame;
            ApplyChromeThemeBrush();
            _pageCardBorder.Background = Brushes.Transparent;
            _pageCardBorder.BorderBrush = Palette.CardEdge;
            if (_stageGrid != null) _stageGrid.Background = Brushes.Transparent;
            if (_leftPaneHost != null) _leftPaneHost.Background = Brushes.Transparent;
            if (_rightPaneHost != null) _rightPaneHost.Background = Brushes.Transparent;
            _omniboxCard.Background = Palette.CardSurface;
            _omniboxCard.BorderBrush = Palette.Hairline;
            _omniboxInput.Foreground = Palette.Ink;
            _omniboxInput.CaretBrush = Palette.Ink;
            _omniboxOffersBorder.Background = Palette.CardSurface;
            _omniboxOffersBorder.BorderBrush = Palette.CardEdge;
            _drawerCard.Background = Palette.CardSurface;
            _drawerCard.BorderBrush = Palette.Hairline;
            _drawerTitle.Foreground = Palette.Ink;
            _spaceTitleText.Foreground = Palette.Ink;

            bool isTop = string.Equals(Store.Prefs.TabLayout, "Top", StringComparison.OrdinalIgnoreCase);
            bool isRight = string.Equals(Store.Prefs.TabLayout, "Right", StringComparison.OrdinalIgnoreCase);
            bool isIconsOnly = string.Equals(Store.Prefs.TabLayout, "IconsOnly", StringComparison.OrdinalIgnoreCase);

            _topTabBarBorder.Visibility = _isFullScreen ? Visibility.Collapsed : Visibility.Visible;
            if (_sidebarFoldBtn != null) _sidebarFoldBtn.Visibility = Store.Prefs.ShowSidebarBtn ? Visibility.Visible : Visibility.Collapsed;
            if (_backBtn != null) _backBtn.Visibility = Store.Prefs.ShowBackBtn ? Visibility.Visible : Visibility.Collapsed;
            if (_forwardBtn != null) _forwardBtn.Visibility = Store.Prefs.ShowForwardBtn ? Visibility.Visible : Visibility.Collapsed;
            if (_reloadBtn != null) _reloadBtn.Visibility = Store.Prefs.ShowReloadBtn ? Visibility.Visible : Visibility.Collapsed;
            ApplyCaptionButtonStyle();
            UpdateFooterButtons();
            UpdateHelmButtons();
            UpdateBookmarkDoorIcon();

            if (_alwaysAddressBar != null)
            {
                _alwaysAddressBar.Visibility = Store.Prefs.AlwaysShowAddressBar ? Visibility.Visible : Visibility.Collapsed;
                _alwaysAddressBar.Background = Palette.Ground;
                _alwaysAddressBar.BorderBrush = Palette.Hairline;
                _alwaysAddressInput.Background = Palette.Wash;
                _alwaysAddressInput.Foreground = Palette.Ink;
                _alwaysAddressInput.CaretBrush = Palette.Ink;
            }
            if (_bookmarksBar != null)
            {
                _bookmarksBar.Visibility = Store.Prefs.ShowBookmarksBar ? Visibility.Visible : Visibility.Collapsed;
                _bookmarksBar.Background = Palette.Ground;
                _bookmarksBar.BorderBrush = Palette.Hairline;
                RefreshBookmarksBar();
            }
            if (isTop)
            {
                _sidebarBorder.Visibility = Visibility.Collapsed;
                _sidebarHoverHotzone.Visibility = Visibility.Collapsed;
                _topTabsStack.Visibility = Visibility.Visible;
            }
            else
            {
                _topTabsStack.Visibility = Visibility.Collapsed;
                Grid.SetColumn(_sidebarBorder, isRight ? 2 : 0);
                if (_sidebarResizeGrip != null)
                    _sidebarResizeGrip.HorizontalAlignment = isRight ? HorizontalAlignment.Left : HorizontalAlignment.Right;
                _sidebarHoverHotzone.HorizontalAlignment = isRight ? HorizontalAlignment.Right : HorizontalAlignment.Left;
                double w = Store.Prefs.SidebarWidth >= 176 && Store.Prefs.SidebarWidth <= 440 ? Store.Prefs.SidebarWidth : 232;
                _sidebarBorder.Width = isIconsOnly ? 52 : w;
                _sidebarBorder.Visibility = Store.Prefs.SidebarCollapsed ? Visibility.Collapsed : Visibility.Visible;
                _sidebarHoverHotzone.Visibility = Store.Prefs.SidebarCollapsed ? Visibility.Visible : Visibility.Collapsed;
            }

            bool showCard = Store.Prefs.PageAsCard && !isTop && (!_isFullScreen || Store.Prefs.KeepCardInFullScreen);
            if (showCard)
            {
                _pageCardBorder.CornerRadius = new CornerRadius(10);
                if (Store.Prefs.SidebarCollapsed)
                    _pageCardBorder.Margin = new Thickness(8, 0, 8, 8);
                else if (isRight)
                    _pageCardBorder.Margin = new Thickness(8, 0, 0, 8);
                else
                    _pageCardBorder.Margin = new Thickness(0, 0, 8, 8);
                _pageCardBorder.BorderThickness = new Thickness(1);
            }
            else
            {
                _pageCardBorder.CornerRadius = new CornerRadius(0);
                _pageCardBorder.Margin = new Thickness(0);
                _pageCardBorder.BorderThickness = new Thickness(0);
            }
            ApplyRoundedCornersToHost(_leftPaneHost);
            ApplyRoundedCornersToHost(_rightPaneHost);

            foreach (var t in _allTabs)
            {
                if (t.IsWebViewInitialized && t.WebView != null && t.WebView.CoreWebView2 != null)
                {
                    t.WebView.DefaultBackgroundColor = Palette.IsDark
                        ? System.Drawing.Color.FromArgb(255, 28, 28, 28)
                        : System.Drawing.Color.FromArgb(255, 255, 255, 255);
                    t.WebView.CoreWebView2.Profile.PreferredColorScheme = Palette.IsDark
                        ? CoreWebView2PreferredColorScheme.Dark
                        : CoreWebView2PreferredColorScheme.Light;
                    if (t.Url == "about:blank") t.RenderBlankWall();
                    else if (reloadSettingsTabs && t.Url != null && t.Url.StartsWith("search://settings", StringComparison.OrdinalIgnoreCase)) t.RenderSettingsTab();
                    else if (reloadSettingsTabs && t.Url != null && t.Url.StartsWith("search://history", StringComparison.OrdinalIgnoreCase)) t.RenderHistoryTab();
                    else if (t.Url != null && !t.Url.StartsWith("search://", StringComparison.OrdinalIgnoreCase)) { var _ = t.ApplySiteStyleSheetsAsync(); }
                }
            }

            UpdateMediaPlayerCard();
            RefreshTabRows();
        }

        private UIElement CreateFaviconOrGlyph(BrowserTab tab, double size, bool boxedLetter = false, Brush letterBrush = null)
        {
            if (tab.IsPrivate)
            {
                return VectorIcons.Draw(VectorIcons.EyeOffPath, Palette.FrameMuted, size * 0.86, 1.45);
            }
            bool preferLetters = string.Equals(Store.Prefs.TabsShowMode, "Letters", StringComparison.OrdinalIgnoreCase);
            if (!preferLetters && !string.IsNullOrEmpty(tab.FaviconUrl) && tab.FaviconUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
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
            if (boxedLetter)
            {
                return new Border
                {
                    Width = size,
                    Height = size,
                    CornerRadius = new CornerRadius(3.3),
                    Background = Palette.InkMarkBg,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Child = new TextBlock
                    {
                        Text = tab.PinGlyph,
                        FontSize = size * 0.56,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = Palette.FrameMuted,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    }
                };
            }
            return new TextBlock
            {
                Text = tab.PinGlyph,
                FontSize = size * 0.82,
                FontWeight = FontWeights.SemiBold,
                Foreground = letterBrush ?? Palette.FrameMuted,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
        }

        private static string ExtractUnreadBadge(string title)
        {
            if (string.IsNullOrEmpty(title)) return null;
            var m = Regex.Match(title, "^\\(([1-9][0-9]*)\\)\\s");
            if (!m.Success) return null;
            int n;
            if (int.TryParse(m.Groups[1].Value, out n) && n > 0)
                return n > 99 ? "99+" : n.ToString();
            return null;
        }

        private double GetDynamicPinCellWidth(int pinCount)
        {
            if (string.Equals(Store.Prefs.TabLayout, "IconsOnly", StringComparison.OrdinalIgnoreCase))
                return 32;
            double sbW = _sidebarBorder != null && _sidebarBorder.Width > 100 ? _sidebarBorder.Width : 232;
            double inner = Math.Max(140, sbW - 20); // 10px left + 10px right padding
            int cols = pinCount >= 4 ? 4 : 3;
            double gap = 4;
            // Every pin square has Margin(0,0,4,4), so all `cols` items contribute `gap * cols` horizontal margin
            return Math.Max(32, Math.Floor((inner - gap * cols - 2) / cols));
        }

        private void RefreshTabRows()
        {
            _pinsPanel.Children.Clear();
            _tabsStack.Children.Clear();
            _topTabsStack.Children.Clear();

            var spaceTabs = GetTabsForActiveSpace();
            var pinned = spaceTabs.Where(t => t.Pinned).ToList();
            var unpinned = spaceTabs.Where(t => !t.Pinned).ToList();
            bool isIconsOnly = string.Equals(Store.Prefs.TabLayout, "IconsOnly", StringComparison.OrdinalIgnoreCase);

            // 1. Pinned squares in sidebar (ALWAYS visible at top so pins & drop-ghost are never hidden!)
            if (_pinsDivider != null)
            {
                _pinsDivider.Visibility = (pinned.Count > 0 && Store.Prefs.PinnedRows && !isIconsOnly) ? Visibility.Visible : Visibility.Collapsed;
            }

            _pinsPanel.Visibility = Visibility.Visible;
            double cellW = GetDynamicPinCellWidth(pinned.Count);
            foreach (var pin in pinned)
            {
                bool isLive = pin == _activeTab || (_isSplitView && (pin == _splitLeftTab || pin == _splitRightTab));
                bool isClosedPin = pin.IsSleeping && !isLive;
                Brush pinBg = isLive ? Palette.FramePinLive : (isClosedPin ? Palette.FrameTile : Palette.FrameHover);
                Brush pinFg = isLive ? Palette.Ink : (isClosedPin ? Palette.FrameFaint : Palette.FrameMuted);
                var sq = new Border
                {
                    Tag = pin,
                    Width = cellW,
                    Height = isIconsOnly ? 32 : 34,
                    Margin = new Thickness(0, 0, 4, 4),
                    CornerRadius = new CornerRadius(9),
                    Background = pinBg,
                    Cursor = Cursors.Hand,
                    ToolTip = pin.DisplayTitle + "\n" + pin.Url
                };
                var pinGrid = new Grid();
                pinGrid.Children.Add(CreateFaviconOrGlyph(pin, 16, false, pinFg));
                if (Store.Prefs.ShowCountsOnPins)
                {
                    string badge = ExtractUnreadBadge(pin.Title);
                    if (!string.IsNullOrEmpty(badge))
                    {
                        pinGrid.Children.Add(new Border
                        {
                            Background = Palette.UnreadRed,
                            CornerRadius = new CornerRadius(7),
                            Padding = new Thickness(4, 1, 4, 1),
                            HorizontalAlignment = HorizontalAlignment.Right,
                            VerticalAlignment = VerticalAlignment.Top,
                            Margin = new Thickness(0, 2, 2, 0),
                            Child = new TextBlock { Text = badge, FontSize = 9, FontWeight = FontWeights.Bold, Foreground = Brushes.White }
                        });
                    }
                }
                sq.Child = pinGrid;

                var captured = pin;
                sq.MouseEnter += (s, e) => { if (captured != _activeTab) sq.Background = Palette.FrameHover; };
                sq.MouseLeave += (s, e) => { if (captured != _activeTab) sq.Background = pinBg; };
                sq.MouseLeftButtonDown += (s, e) =>
                {
                    if (e.ClickCount == 2 && !string.IsNullOrEmpty(captured.HomeUrl))
                    {
                        captured.Navigate(captured.HomeUrl);
                        e.Handled = true;
                        return;
                    }
                    if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && Store.Prefs.CtrlClickPinOpensNewTab)
                    {
                        CreateAndSelectTab(string.IsNullOrEmpty(captured.HomeUrl) ? captured.Url : captured.HomeUrl, false);
                        e.Handled = true;
                        return;
                    }
                    BeginPotentialCarry(captured, sq, e);
                };
                sq.MouseDown += (s, e) =>
                {
                    if (e.ChangedButton == MouseButton.Middle)
                    {
                        e.Handled = true;
                        CloseTab(captured, true);
                    }
                };
                sq.MouseRightButtonUp += (s, e) => { e.Handled = true; ShowTabContextMenu(captured); };
                if (_justPinnedTabId == pin.Id)
                {
                    _justPinnedTabId = null;
                    if (!Store.Prefs.ReduceAnimations)
                    {
                        var st = new ScaleTransform(0.8, 0.8);
                        sq.RenderTransformOrigin = new Point(0.5, 0.5);
                        sq.RenderTransform = st;
                        var anim = new DoubleAnimation(0.8, 1.0, TimeSpan.FromMilliseconds(180)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
                        st.BeginAnimation(ScaleTransform.ScaleXProperty, anim);
                        st.BeginAnimation(ScaleTransform.ScaleYProperty, anim);
                    }
                }
                _pinsPanel.Children.Add(sq);
            }

            int tabNumCounter = 1;

            // 2. Collapsible Tab Groups / Folders in current Space
            var spaceGroups = Store.TabGroups.Where(g => string.Equals(g.SpaceId, Store.Prefs.ActiveSpaceId, StringComparison.OrdinalIgnoreCase)).ToList();
            foreach (var grp in spaceGroups)
            {
                var grpTabs = unpinned.Where(t => t.GroupId == grp.Id).ToList();
                if (!isIconsOnly)
                {
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
                        Foreground = Palette.FrameMuted,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    DockPanel.SetDock(countLbl, Dock.Right);
                    hdrDock.Children.Add(countLbl);

                    var nameLbl = new TextBlock
                    {
                        Text = (grp.Collapsed ? "▸ " : "▾ ") + grp.Name,
                        FontSize = 11.5,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = Palette.FrameMuted,
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
                        e.Handled = true;
                        StackPanel menuStack;
                        var m = CreateThemedPopupMenu(null, System.Windows.Controls.Primitives.PlacementMode.MousePoint, out menuStack);
                        AddPopupMenuItem(m, menuStack, "New Tab in " + capGrp.Name, "", () => CreateAndSelectTab("about:blank", true, false, capGrp.Id));
                        AddPopupMenuItem(m, menuStack, "Rename Group…", "", () =>
                        {
                            string n = PromptForText("Rename Group", "Group name:", capGrp.Name);
                            if (!string.IsNullOrWhiteSpace(n)) { capGrp.Name = n.Trim(); Store.SaveGroups(); RefreshTabRows(); }
                        });
                        AddPopupSeparator(menuStack);
                        AddPopupMenuItem(m, menuStack, "Ungroup & Remove Folder", "", () =>
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
                }

                if (!grp.Collapsed)
                {
                    foreach (var gt in grpTabs)
                    {
                        _tabsStack.Children.Add(CreateVerticalTabRow(gt, !isIconsOnly, tabNumCounter++));
                    }
                }
            }

            // 3. Ungrouped vertical rows in sidebar
            foreach (var tab in unpinned.Where(t => string.IsNullOrEmpty(t.GroupId) || !spaceGroups.Any(g => g.Id == t.GroupId)))
            {
                _tabsStack.Children.Add(CreateVerticalTabRow(tab, false, tabNumCounter++));
            }

            // 3b. "+ New tab" quiet row directly underneath the last tab in the column
            _tabsStack.Children.Add(CreateInlineNewTabRow(isIconsOnly));

            // 4. Horizontal pills in top bar (when in Top mode)
            foreach (var tab in spaceTabs)
            {
                _topTabsStack.Children.Add(CreateHorizontalTabPill(tab));
            }

            RefreshSpaceHeader();
            AnimateGlidingTabPill();
        }

        private UIElement CreateInlineNewTabRow(bool isIconsOnly = false)
        {
            var row = new Border
            {
                Height = Store.Prefs.RoomierTabs ? 36 : 28,
                CornerRadius = new CornerRadius(9),
                Margin = new Thickness(0, 1, 0, 1),
                Padding = isIconsOnly ? new Thickness(0) : new Thickness(10, 0, 10, 0),
                Background = Brushes.Transparent,
                Cursor = Cursors.Hand,
                ToolTip = "New Tab   Ctrl+T"
            };
            if (isIconsOnly)
            {
                row.Child = VectorIcons.Draw(VectorIcons.PlusPath, Palette.FrameMuted, 12, 1.5);
            }
            else
            {
                var stack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                var plusBox = new Border
                {
                    Width = 15,
                    Height = 15,
                    Margin = new Thickness(0, 0, 8, 0),
                    Child = VectorIcons.Draw(VectorIcons.PlusPath, Palette.FrameMuted, 11, 1.5)
                };
                var lbl = new TextBlock
                {
                    Text = "New tab",
                    FontSize = Store.Prefs.RoomierTabs ? 13.5 : 12.5,
                    Foreground = Palette.FrameMuted,
                    Opacity = 0.72,
                    VerticalAlignment = VerticalAlignment.Center
                };
                stack.Children.Add(plusBox);
                stack.Children.Add(lbl);
                row.Child = stack;

                row.MouseEnter += (s, e) =>
                {
                    row.Background = Palette.FrameHover;
                    lbl.Foreground = Palette.InkHoverText;
                    lbl.Opacity = 0.9;
                };
                row.MouseLeave += (s, e) =>
                {
                    row.Background = Brushes.Transparent;
                    lbl.Foreground = Palette.FrameMuted;
                    lbl.Opacity = 0.72;
                };
            }
            row.MouseLeftButtonUp += (s, e) =>
            {
                e.Handled = true;
                if (Store.Prefs.NewTabInFloatingField) ShowOmnibox(false, true);
                else CreateAndSelectTab("about:blank", false);
            };
            return row;
        }

        private void AddIconPopupMenuItem(System.Windows.Controls.Primitives.Popup popup, StackPanel stack, bool isChecked, string iconPath, string title, string shortcut, Action action, bool enabled = true)
        {
            var row = new Border
            {
                Height = 26,
                CornerRadius = new CornerRadius(7),
                Padding = new Thickness(7, 0, 9, 0),
                Margin = new Thickness(0, 1, 0, 1),
                Background = Brushes.Transparent,
                Cursor = enabled ? Cursors.Hand : Cursors.Arrow,
                Opacity = enabled ? 1.0 : 0.42
            };
            var dock = new DockPanel();
            if (!string.IsNullOrEmpty(shortcut))
            {
                var sc = new TextBlock
                {
                    Text = shortcut,
                    FontSize = 11.5,
                    Foreground = Palette.MenuSecondary,
                    Margin = new Thickness(16, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                DockPanel.SetDock(sc, Dock.Right);
                dock.Children.Add(sc);
            }
            var left = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var chkBox = new Grid { Width = 14, Height = 14, Margin = new Thickness(0, 0, 4, 0) };
            if (isChecked) chkBox.Children.Add(VectorIcons.Draw(VectorIcons.CheckPath, Palette.Ink, 11, 1.6));
            left.Children.Add(chkBox);
            if (!string.IsNullOrEmpty(iconPath))
            {
                var icoBox = new Grid { Width = 15, Height = 15, Margin = new Thickness(0, 0, 7, 0) };
                icoBox.Children.Add(VectorIcons.Draw(iconPath, Palette.FrameMuted, 13, 1.45));
                left.Children.Add(icoBox);
            }
            left.Children.Add(new TextBlock { Text = title, FontSize = 13, Foreground = Palette.Ink, VerticalAlignment = VerticalAlignment.Center });
            dock.Children.Add(left);
            row.Child = dock;
            if (enabled)
            {
                row.MouseEnter += (s, e) => row.Background = Palette.MenuHot;
                row.MouseLeave += (s, e) => row.Background = Brushes.Transparent;
                row.MouseLeftButtonUp += (s, e) =>
                {
                    e.Handled = true;
                    popup.IsOpen = false;
                    action();
                };
            }
            stack.Children.Add(row);
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
                AddIconPopupMenuItem(popup, stack, isCur, VectorIcons.GetSpaceIconPath(sp.IconName), sp.Name, i < 9 ? "Alt+" + (i + 1) : "", () => CycleSpace(idx));
            }
            AddPopupSeparator(stack);
            AddIconPopupMenuItem(popup, stack, false, null, "New Space…", "", () =>
            {
                string name = PromptForText("New Space", "Enter a name for the new Space:", "Space " + (Store.Spaces.Count + 1));
                if (!string.IsNullOrWhiteSpace(name))
                {
                    var ns = new SpaceItem { Id = Guid.NewGuid().ToString("N").Substring(0, 8), Name = name.Trim(), AccentHex = "", IconName = "Home", ColorKey = "", ColorStrength = "Subtle", DownloadsFolder = "" };
                    Store.Spaces.Add(ns);
                    Store.SaveSpaces();
                    CycleSpace(Store.Spaces.Count - 1);
                }
            });

            int curIdx = Store.Spaces.FindIndex(s => string.Equals(s.Id, Store.Prefs.ActiveSpaceId, StringComparison.OrdinalIgnoreCase));
            var curSp = curIdx >= 0 ? Store.Spaces[curIdx] : (Store.Spaces.Count > 0 ? Store.Spaces[0] : null);
            if (curSp != null)
            {
                AddPopupSeparator(stack);
                AddIconPopupMenuItem(popup, stack, false, null, "Rename “" + curSp.Name + "”…", "", () =>
                {
                    string name = PromptForText("Rename Space", "Space name:", curSp.Name);
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        curSp.Name = name.Trim();
                        Store.SaveSpaces();
                        RefreshSpaceHeader();
                    }
                });
                AddIconPopupMenuItem(popup, stack, false, null, "Icon", "›", () => ShowSpaceIconPicker(anchor, curSp));
                AddIconPopupMenuItem(popup, stack, false, null, "Colour…", "", () => ShowSpaceColourPicker(anchor, curSp));
                if (curIdx > 0)
                {
                    AddIconPopupMenuItem(popup, stack, false, null, "Move Left", "", () =>
                    {
                        Store.Spaces.RemoveAt(curIdx);
                        Store.Spaces.Insert(curIdx - 1, curSp);
                        Store.SaveSpaces();
                        RefreshSpaceHeader();
                    });
                }
                if (curIdx >= 0 && curIdx < Store.Spaces.Count - 1)
                {
                    AddIconPopupMenuItem(popup, stack, false, null, "Move Right", "", () =>
                    {
                        Store.Spaces.RemoveAt(curIdx);
                        Store.Spaces.Insert(curIdx + 1, curSp);
                        Store.SaveSpaces();
                        RefreshSpaceHeader();
                    });
                }
                AddIconPopupMenuItem(popup, stack, false, null, "Downloads", "›", () => ShowSpaceDownloadsMenu(anchor, curSp));
                AddPopupSeparator(stack);
                AddIconPopupMenuItem(popup, stack, false, null, "Delete “" + curSp.Name + "”…", "", () =>
                {
                    if (Store.Spaces.Count <= 1) return;
                    if (MessageBox.Show("Delete space “" + curSp.Name + "” and close its tabs?", "Delete Space", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
                    var toClose = _allTabs.Where(t => string.Equals(t.SpaceId, curSp.Id, StringComparison.OrdinalIgnoreCase) && !t.KeepInEverySpace).ToList();
                    foreach (var t in toClose) CloseTab(t, true, true);
                    Store.Spaces.Remove(curSp);
                    Store.SaveSpaces();
                    CycleSpace(Math.Max(0, Math.Min(curIdx, Store.Spaces.Count - 1)));
                }, Store.Spaces.Count > 1);
            }
            popup.IsOpen = true;
        }

        private void ShowSpaceIconPicker(FrameworkElement anchor, SpaceItem sp)
        {
            StackPanel stack;
            var popup = CreateThemedPopupMenu(anchor, System.Windows.Controls.Primitives.PlacementMode.Top, out stack);
            string curIcon = string.IsNullOrEmpty(sp.IconName) ? "Home" : sp.IconName;
            foreach (var iconName in VectorIcons.SpaceIconNames)
            {
                string capName = iconName;
                bool isSel = string.Equals(curIcon, capName, StringComparison.OrdinalIgnoreCase);
                AddIconPopupMenuItem(popup, stack, isSel, VectorIcons.GetSpaceIconPath(capName), capName, "", () =>
                {
                    sp.IconName = capName;
                    Store.SaveSpaces();
                    RefreshSpaceHeader();
                });
            }
            popup.IsOpen = true;
        }

        private void ShowSpaceColourPicker(FrameworkElement anchor, SpaceItem sp)
        {
            StackPanel stack;
            var popup = CreateThemedPopupMenu(anchor, System.Windows.Controls.Primitives.PlacementMode.Top, out stack);
            stack.MinWidth = 220;
            stack.Margin = new Thickness(6, 4, 6, 4);

            string[] row1 = new[] { "", "blue", "green", "orange", "red", "purple" };
            string[] row2 = new[] { "sunset", "ocean", "mint", "berry", "sky", "amber" };
            string curKey = sp.ColorKey ?? "";
            string curStrength = string.IsNullOrEmpty(sp.ColorStrength) ? "Subtle" : sp.ColorStrength;

            Func<string, Border> makeSwatch = (key) =>
            {
                bool isSel = string.Equals(curKey, key, StringComparison.OrdinalIgnoreCase);
                Brush fill = Brushes.Transparent;
                Color c1, c2;
                bool isGrad;
                if (TryGetSpaceColorSpec(key, out c1, out c2, out isGrad))
                {
                    fill = isGrad ? (Brush)new LinearGradientBrush(c1, c2, 135.0) : new SolidColorBrush(c1);
                }
                var circle = new Border
                {
                    Width = 26,
                    Height = 26,
                    CornerRadius = new CornerRadius(13),
                    Margin = new Thickness(4),
                    Background = fill,
                    BorderBrush = isSel ? Palette.Ink : (string.IsNullOrEmpty(key) ? Palette.FrameFaint : Brushes.Transparent),
                    BorderThickness = new Thickness(isSel ? 2 : (string.IsNullOrEmpty(key) ? 1.5 : 0)),
                    Cursor = Cursors.Hand,
                    ToolTip = string.IsNullOrEmpty(key) ? "None" : (char.ToUpper(key[0]) + key.Substring(1))
                };
                if (string.IsNullOrEmpty(key))
                {
                    circle.Child = VectorIcons.Draw("M 3.5,12.5 L 12.5,3.5", Palette.FrameMuted, 13, 1.3);
                }
                circle.MouseLeftButtonUp += (s, e) =>
                {
                    e.Handled = true;
                    sp.ColorKey = key;
                    Store.SaveSpaces();
                    ApplyChromeThemeBrush();
                    popup.IsOpen = false;
                };
                return circle;
            };

            var r1Panel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
            foreach (var k in row1) r1Panel.Children.Add(makeSwatch(k));
            stack.Children.Add(r1Panel);

            var r2Panel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 2, 0, 8) };
            foreach (var k in row2) r2Panel.Children.Add(makeSwatch(k));
            stack.Children.Add(r2Panel);

            var strengthRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
            foreach (var stName in new[] { "Subtle", "Rich", "Vivid" })
            {
                string capSt = stName;
                bool isSel = string.Equals(curStrength, capSt, StringComparison.OrdinalIgnoreCase);
                var pill = new Border
                {
                    CornerRadius = new CornerRadius(13),
                    Padding = new Thickness(12, 5, 12, 5),
                    Margin = new Thickness(3, 0, 3, 2),
                    Background = isSel ? Palette.Ink : Palette.FrameWash,
                    Cursor = Cursors.Hand,
                    Child = new TextBlock
                    {
                        Text = capSt,
                        FontSize = 12,
                        FontWeight = isSel ? FontWeights.SemiBold : FontWeights.Normal,
                        Foreground = isSel ? Palette.Ground : Palette.Ink
                    }
                };
                pill.MouseLeftButtonUp += (s, e) =>
                {
                    e.Handled = true;
                    sp.ColorStrength = capSt;
                    Store.SaveSpaces();
                    ApplyChromeThemeBrush();
                    popup.IsOpen = false;
                };
                strengthRow.Children.Add(pill);
            }
            stack.Children.Add(strengthRow);
            popup.IsOpen = true;
        }

        private void ShowSpaceDownloadsMenu(FrameworkElement anchor, SpaceItem sp)
        {
            StackPanel stack;
            var popup = CreateThemedPopupMenu(anchor, System.Windows.Controls.Primitives.PlacementMode.Top, out stack);
            bool isDefault = string.IsNullOrWhiteSpace(sp.DownloadsFolder);
            AddIconPopupMenuItem(popup, stack, isDefault, null, "Same as Settings", "", () =>
            {
                sp.DownloadsFolder = "";
                Store.SaveSpaces();
            });
            if (!isDefault)
            {
                AddIconPopupMenuItem(popup, stack, true, null, Path.GetFileName(sp.DownloadsFolder), "", () => { }, false);
            }
            AddPopupSeparator(stack);
            AddIconPopupMenuItem(popup, stack, false, null, "Choose Folder…", "", () =>
            {
                Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
                {
                    using (var fbd = new System.Windows.Forms.FolderBrowserDialog())
                    {
                        fbd.Description = "Choose Downloads folder for " + sp.Name + ":";
                        if (!string.IsNullOrWhiteSpace(sp.DownloadsFolder) && Directory.Exists(sp.DownloadsFolder))
                            fbd.SelectedPath = sp.DownloadsFolder;
                        if (fbd.ShowDialog(GetWin32Owner()) == System.Windows.Forms.DialogResult.OK && !string.IsNullOrWhiteSpace(fbd.SelectedPath))
                        {
                            sp.DownloadsFolder = fbd.SelectedPath;
                            Store.SaveSpaces();
                        }
                    }
                }));
            });
            popup.IsOpen = true;
        }

        private System.Windows.Controls.Primitives.Popup CreateThemedPopupMenu(FrameworkElement anchor, System.Windows.Controls.Primitives.PlacementMode placement, out StackPanel stack)
        {
            stack = new StackPanel { Orientation = Orientation.Vertical, MinWidth = 210 };
            var card = new Border
            {
                Background = Palette.MenuBg,
                BorderBrush = Palette.MenuBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(5),
                Child = stack,
                Effect = new DropShadowEffect { BlurRadius = 22, ShadowDepth = 6, Opacity = 0.3 }
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
                Background = Palette.MenuSep,
                Margin = new Thickness(8, 5, 8, 5)
            });
        }

        private void AddPopupMenuItem(System.Windows.Controls.Primitives.Popup popup, StackPanel stack, string title, string shortcut, Action action, bool enabled = true)
        {
            var row = new Border
            {
                Height = 24,
                CornerRadius = new CornerRadius(7),
                Padding = new Thickness(9, 0, 9, 0),
                Margin = new Thickness(0, 1, 0, 1),
                Background = Brushes.Transparent,
                Cursor = enabled ? Cursors.Hand : Cursors.Arrow,
                Opacity = enabled ? 1.0 : 0.42
            };
            var dock = new DockPanel();
            if (!string.IsNullOrEmpty(shortcut))
            {
                var sc = new TextBlock
                {
                    Text = shortcut,
                    FontSize = 11.5,
                    Foreground = Palette.MenuSecondary,
                    Margin = new Thickness(16, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                DockPanel.SetDock(sc, Dock.Right);
                dock.Children.Add(sc);
            }
            var lbl = new TextBlock
            {
                Text = title,
                FontSize = 13,
                Foreground = Palette.Ink,
                VerticalAlignment = VerticalAlignment.Center
            };
            dock.Children.Add(lbl);
            row.Child = dock;
            if (enabled)
            {
                row.MouseEnter += (s, e) => row.Background = Palette.MenuHot;
                row.MouseLeave += (s, e) => row.Background = Brushes.Transparent;
                row.MouseLeftButtonUp += (s, e) =>
                {
                    e.Handled = true;
                    popup.IsOpen = false;
                    action();
                };
            }
            stack.Children.Add(row);
        }

        private static string PromptForText(string title, string label, string defaultVal)
        {
            var win = new Window
            {
                Title = title,
                Width = 356,
                SizeToContent = SizeToContent.Height,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                ResizeMode = ResizeMode.NoResize,
                Background = Brushes.Transparent,
                ShowInTaskbar = false,
                Topmost = true
            };

            var card = new Border
            {
                Background = Palette.MenuBg,
                BorderBrush = Palette.MenuBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(18, 16, 18, 16),
                Margin = new Thickness(10),
                Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 22, ShadowDepth = 6, Opacity = 0.38, Color = Colors.Black }
            };

            var sp = new StackPanel();
            sp.Children.Add(new TextBlock { Text = title, FontSize = 14, FontWeight = FontWeights.SemiBold, Foreground = Palette.Ink, Margin = new Thickness(0, 0, 0, 4) });
            sp.Children.Add(new TextBlock { Text = label, FontSize = 12, Foreground = Palette.Muted, Margin = new Thickness(0, 0, 0, 12) });

            var tbBorder = new Border
            {
                Background = Palette.Ground,
                BorderBrush = Palette.Hairline,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(8, 5, 8, 5),
                Margin = new Thickness(0, 0, 0, 14)
            };
            var tb = new TextBox
            {
                Text = defaultVal ?? "",
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = Palette.Ink,
                CaretBrush = Palette.Ink,
                FontSize = 13,
                VerticalContentAlignment = VerticalAlignment.Center
            };
            tbBorder.Child = tb;
            sp.Children.Add(tbBorder);

            var btnRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var cancelBtn = new Border
            {
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(14, 6, 14, 6),
                Margin = new Thickness(0, 0, 8, 0),
                Background = Palette.FrameWash,
                Cursor = Cursors.Hand,
                Child = new TextBlock { Text = "Cancel", FontSize = 12.5, Foreground = Palette.Ink }
            };
            cancelBtn.MouseEnter += (s, e) => cancelBtn.Background = Palette.MenuHot;
            cancelBtn.MouseLeave += (s, e) => cancelBtn.Background = Palette.FrameWash;
            cancelBtn.MouseLeftButtonUp += (s, e) => { win.DialogResult = false; win.Close(); };

            var okBtn = new Border
            {
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(16, 6, 16, 6),
                Background = Palette.Ink,
                Cursor = Cursors.Hand,
                Child = new TextBlock { Text = "OK", FontSize = 12.5, FontWeight = FontWeights.SemiBold, Foreground = Palette.Ground }
            };
            okBtn.MouseLeftButtonUp += (s, e) => { win.DialogResult = true; win.Close(); };

            btnRow.Children.Add(cancelBtn);
            btnRow.Children.Add(okBtn);
            sp.Children.Add(btnRow);

            card.Child = sp;
            win.Content = card;
            win.PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter) { e.Handled = true; win.DialogResult = true; win.Close(); }
                else if (e.Key == Key.Escape) { e.Handled = true; win.DialogResult = false; win.Close(); }
            };
            win.Loaded += (s, e) => { tb.Focus(); tb.SelectAll(); };
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
                    else if (Store.Prefs.ReduceAnimations)
                    {
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

        private UIElement CreateVerticalTabRow(BrowserTab tab, bool indented, int tabNumber = -1)
        {
            bool isLive = tab == _activeTab;
            bool isSecondarySplit = !isLive && _isSplitView && (tab == _splitLeftTab || tab == _splitRightTab);
            bool isBlank = tab.Url == "about:blank";
            bool isIconsOnly = string.Equals(Store.Prefs.TabLayout, "IconsOnly", StringComparison.OrdinalIgnoreCase);
            double rowHeight = Store.Prefs.RoomierTabs ? 36 : 28;
            double titleFontSize = Store.Prefs.RoomierTabs ? 14 : 12.5;

            var row = new Border
            {
                Tag = tab,
                Height = rowHeight,
                CornerRadius = new CornerRadius(9),
                Margin = new Thickness(indented ? 10 : 0, 1, 0, 1),
                Padding = isIconsOnly ? new Thickness(0) : new Thickness(10, 0, 8, 0),
                Background = isSecondarySplit ? Palette.FrameHover : Brushes.Transparent,
                Cursor = Cursors.Hand,
                AllowDrop = true,
                ClipToBounds = true,
                ToolTip = tab.DisplayTitle + "\n" + tab.Url + (tab.IsSleeping ? "\n(Sleeping — zero RAM used)" : "")
            };

            var rowScale = new ScaleTransform(1.0, 1.0);
            row.RenderTransformOrigin = new Point(0.5, 0.5);
            row.RenderTransform = rowScale;

            var outerLayer = new Grid();

            // Reading progress bar across active tab (Oriel ShowReadingProgress)
            if (isLive && Store.Prefs.ShowReadingProgress && tab.ReadProgress > 0.02)
            {
                var progGrid = new Grid { IsHitTestVisible = false };
                progGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(tab.ReadProgress, GridUnitType.Star) });
                progGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Max(0.001, 1.0 - tab.ReadProgress), GridUnitType.Star) });
                var progFill = new Border { Background = Palette.FrameTile, CornerRadius = new CornerRadius(9), Opacity = 0.75 };
                Grid.SetColumn(progFill, 0);
                progGrid.Children.Add(progFill);
                outerLayer.Children.Add(progGrid);
            }

            if (isIconsOnly)
            {
                var iconWrap = new Grid { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                iconWrap.Children.Add(CreateFaviconOrGlyph(tab, 16, true));
                outerLayer.Children.Add(iconWrap);
            }
            else
            {
                bool hideIcon = isBlank && !tab.IsPrivate;
                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = hideIcon ? new GridLength(0) : new GridLength(23) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                if (!hideIcon)
                {
                    UIElement iconEl;
                    if (tab.IsLoading)
                        iconEl = new TextBlock { Text = "◌", FontSize = 11, Foreground = Palette.FrameMuted, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
                    else if (tab.IsPrivate)
                        iconEl = VectorIcons.Draw(VectorIcons.EyeOffPath, Palette.FrameMuted, 12, 1.45);
                    else if (tab.Url != null && (tab.Url.StartsWith("search://settings", StringComparison.OrdinalIgnoreCase) || tab.Url.StartsWith("search://history", StringComparison.OrdinalIgnoreCase)))
                        iconEl = CreateFaviconOrGlyph(tab, 15, true);
                    else
                        iconEl = CreateFaviconOrGlyph(tab, 15, true);

                    var iconAlign = new Border { Width = 15, Height = 15, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center, Child = iconEl };
                    grid.Children.Add(iconAlign);
                }

                string sleepPrefix = (tab.IsSleeping && !isLive) ? "☾ " : "";
                string normalLabel = sleepPrefix + (tab.KeepAwake ? "⚡ " : "") + (tab.IsAudible ? (tab.IsMuted ? "🔇 " : "🔊 ") : "") + tab.DisplayTitle;
                string activeLabel = (isLive && Store.Prefs.ShowAddressOnActiveTab && !isBlank && (tab.Url == null || !tab.Url.StartsWith("search://")))
                    ? Address.DisplayHost(tab.Url)
                    : normalLabel;
                if (string.IsNullOrEmpty(activeLabel)) activeLabel = normalLabel;

                Brush restFg = isLive ? Palette.Ink : (Store.Prefs.HighContrastTabText ? Palette.Ink : Palette.FrameMuted);
                var titleBlock = new TextBlock
                {
                    Text = activeLabel,
                    FontSize = titleFontSize,
                    FontWeight = FontWeights.Normal,
                    Foreground = restFg,
                    Opacity = (tab.IsSleeping && !isLive && Store.Prefs.DimSleepingTabs) ? 0.45 : 1.0,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis
                };
                Grid.SetColumn(titleBlock, 1);
                grid.Children.Add(titleBlock);
                outerLayer.Children.Add(grid);

                // Floating circular 15x15 close disc overlaid on right on hover (Oriel Row.tsx InkCloseDisc)
                var closeBtn = new Border
                {
                    Width = 15,
                    Height = 15,
                    CornerRadius = new CornerRadius(7.5),
                    Background = Palette.InkCloseDisc,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Center,
                    Visibility = Visibility.Hidden,
                    Child = VectorIcons.Draw(VectorIcons.CloseSmallPath, Palette.Ink, 7.5, 1.6)
                };
                var captured = tab;
                closeBtn.MouseLeftButtonDown += (s, e) =>
                {
                    e.Handled = true;
                    CloseTab(captured, true);
                };
                outerLayer.Children.Add(closeBtn);

                // Tab number badge while Ctrl is held (ShowTabNumbersOnCtrl)
                if (_isCtrlHeld && Store.Prefs.ShowTabNumbersOnCtrl && tabNumber >= 1 && tabNumber <= 9)
                {
                    outerLayer.Children.Add(new Border
                    {
                        Width = 16, Height = 16, CornerRadius = new CornerRadius(4),
                        Background = Palette.Ink,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        VerticalAlignment = VerticalAlignment.Center,
                        Child = new TextBlock { Text = tabNumber.ToString(), FontSize = 10, FontWeight = FontWeights.SemiBold, Foreground = Palette.Ground, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
                    });
                }

                row.MouseEnter += (s, e) =>
                {
                    closeBtn.Visibility = Visibility.Visible;
                    titleBlock.Text = normalLabel;
                    if (!isLive)
                    {
                        row.Background = Palette.FrameHover;
                        titleBlock.Foreground = Palette.InkHoverText;
                    }
                };
                row.MouseLeave += (s, e) =>
                {
                    closeBtn.Visibility = Visibility.Hidden;
                    titleBlock.Text = activeLabel;
                    if (!isLive)
                    {
                        row.Background = isSecondarySplit ? Palette.FrameHover : Brushes.Transparent;
                        titleBlock.Foreground = restFg;
                    }
                    rowScale.ScaleX = 1.0;
                    rowScale.ScaleY = 1.0;
                };
            }

            row.Child = outerLayer;
            var capTab = tab;
            row.MouseLeftButtonDown += (s, e) =>
            {
                if (Store.Prefs.TabsGiveWhenPressed && !Store.Prefs.ReduceAnimations)
                {
                    rowScale.ScaleX = 0.97;
                    rowScale.ScaleY = 0.97;
                }
                BeginPotentialCarry(capTab, row, e);
            };
            row.MouseLeftButtonUp += (s, e) =>
            {
                rowScale.ScaleX = 1.0;
                rowScale.ScaleY = 1.0;
            };
            row.MouseDown += (s, e) =>
            {
                if (e.ChangedButton == MouseButton.Middle)
                {
                    e.Handled = true;
                    CloseTab(capTab, true);
                }
            };
            row.MouseRightButtonUp += (s, e) => { e.Handled = true; ShowTabContextMenu(capTab); };

            return row;
        }

        private void BeginPotentialCarry(BrowserTab tab, UIElement source, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left) return;
            _carryTab = tab;
            _carrySource = source;
            _carryStartScreen = PointToScreen(e.GetPosition(this));
            _isCarrying = false;
            _carryOverPins = false;
            _carryOverTabs = false;
            _carryOverStage = false;
            _carryDropIndex = -1;
            source.CaptureMouse();
            source.MouseMove += OnCarryMouseMove;
            source.MouseLeftButtonUp += OnCarryMouseUp;
            e.Handled = true;
        }

        private void CancelCarry()
        {
            if (_carrySource != null)
            {
                _carrySource.MouseMove -= OnCarryMouseMove;
                _carrySource.MouseLeftButtonUp -= OnCarryMouseUp;
                _carrySource.ReleaseMouseCapture();
            }
            if (_carryPopup != null) { _carryPopup.IsOpen = false; _carryPopup = null; }
            if (_pinGhost != null) { _pinsPanel.Children.Remove(_pinGhost); _pinGhost = null; }
            if (_rowGhost != null) { _tabsStack.Children.Remove(_rowGhost); _rowGhost = null; }
            _carryTab = null;
            _carrySource = null;
            _isCarrying = false;
            _carryOverPins = false;
            _carryOverTabs = false;
            _carryOverStage = false;
            _carryDropIndex = -1;
        }

        private void OnCarryMouseMove(object sender, MouseEventArgs e)
        {
            if (_carryTab == null) return;
            Point curWin = e.GetPosition(this);
            Point curScreen = PointToScreen(curWin);
            double dx = curScreen.X - _carryStartScreen.X;
            double dy = curScreen.Y - _carryStartScreen.Y;

            if (!_isCarrying)
            {
                if (Math.Sqrt(dx * dx + dy * dy) < 5) return;
                _isCarrying = true;
                _carryVisual = _carryTab.Pinned
                    ? new Border
                    {
                        Width = 56, Height = 34, CornerRadius = new CornerRadius(9),
                        Background = Palette.FramePinLive, BorderBrush = Palette.InkPickedOutline, BorderThickness = new Thickness(1),
                        Child = CreateFaviconOrGlyph(_carryTab, 16, false),
                        Effect = new DropShadowEffect { BlurRadius = 14, ShadowDepth = 4, Opacity = 0.28 }
                    }
                    : new Border
                    {
                        Width = 208, Height = 28, CornerRadius = new CornerRadius(9), Padding = new Thickness(10, 0, 10, 0),
                        Background = Palette.FramePinLive, BorderBrush = Palette.InkPickedOutline, BorderThickness = new Thickness(1),
                        Child = new TextBlock { Text = _carryTab.DisplayTitle, FontSize = 12.5, Foreground = Palette.Ink, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis },
                        Effect = new DropShadowEffect { BlurRadius = 14, ShadowDepth = 4, Opacity = 0.28 }
                    };
                _carryPopup = new System.Windows.Controls.Primitives.Popup
                {
                    Child = _carryVisual,
                    AllowsTransparency = true,
                    IsHitTestVisible = false,
                    Placement = System.Windows.Controls.Primitives.PlacementMode.Absolute,
                    IsOpen = true
                };
            }

            if (_carryPopup != null)
            {
                var psrc = PresentationSource.FromVisual(this);
                double dpiX = psrc != null && psrc.CompositionTarget != null ? psrc.CompositionTarget.TransformToDevice.M11 : 1.0;
                double dpiY = psrc != null && psrc.CompositionTarget != null ? psrc.CompositionTarget.TransformToDevice.M22 : 1.0;
                _carryPopup.HorizontalOffset = (curScreen.X / dpiX) - (_carryTab.Pinned ? 28 : 80);
                _carryPopup.VerticalOffset = (curScreen.Y / dpiY) - 16;
            }

            Point inSidebar = e.GetPosition(_sidebarBorder);
            Point inStage = e.GetPosition(_pageCardBorder);
            double pinZoneBottom = Math.Max(52, _pinsPanel.ActualHeight + 22);
            bool inSidebarX = inSidebar.X >= -24 && inSidebar.X <= _sidebarBorder.ActualWidth + 24;
            bool overPins = !_carryTab.IsPrivate && inSidebarX && inSidebar.Y >= -16 && inSidebar.Y < pinZoneBottom;
            bool overTabs = inSidebarX && inSidebar.Y >= pinZoneBottom;
            _carryOverStage = !inSidebarX && inStage.X >= 0 && inStage.X <= _pageCardBorder.ActualWidth && inStage.Y >= 0 && inStage.Y <= _pageCardBorder.ActualHeight;

            if (overPins)
            {
                _carryOverPins = true;
                _carryOverTabs = false;
                if (_rowGhost != null) { _tabsStack.Children.Remove(_rowGhost); _rowGhost = null; }
                if (_carryVisual != null) _carryVisual.Opacity = 0.55;
                _pinsPanel.Visibility = Visibility.Visible;
                if (_pinGhost == null)
                {
                    double cw = GetDynamicPinCellWidth(_pinsPanel.Children.Count + 1);
                    _pinGhost = new Border
                    {
                        Width = cw, Height = 34, Margin = new Thickness(0, 0, 4, 4), CornerRadius = new CornerRadius(9),
                        Background = Palette.FrameWash, BorderBrush = Palette.InkGhostEdge, BorderThickness = new Thickness(1.5),
                        Opacity = 0.85, Child = CreateFaviconOrGlyph(_carryTab, 16, false)
                    };
                }
                int targetPinIdx = Math.Max(0, Math.Min(_pinsPanel.Children.Count, (int)(Math.Max(0, inSidebar.X - 10) / Math.Max(48, GetDynamicPinCellWidth(_pinsPanel.Children.Count) + 4))));
                _carryDropIndex = targetPinIdx;
                if (_pinsPanel.Children.Contains(_pinGhost)) _pinsPanel.Children.Remove(_pinGhost);
                _pinsPanel.Children.Insert(Math.Min(targetPinIdx, _pinsPanel.Children.Count), _pinGhost);
            }
            else if (overTabs)
            {
                _carryOverPins = false;
                _carryOverTabs = true;
                if (_pinGhost != null) { _pinsPanel.Children.Remove(_pinGhost); _pinGhost = null; }
                if (_carryVisual != null) _carryVisual.Opacity = 0.55;

                Point inTabs = e.GetPosition(_tabsStack);
                double rowH = (Store.Prefs.RoomierTabs ? 36 : 28) + 2;
                int maxTabRows = Math.Max(0, _tabsStack.Children.Count - 1); // keep "+ New tab" last
                int targetRowIdx = Math.Max(0, Math.Min(maxTabRows, (int)(Math.Max(0, inTabs.Y) / rowH)));
                _carryDropIndex = targetRowIdx;

                if (_rowGhost == null)
                {
                    var sp = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                    sp.Children.Add(new Border { Width = 18, Margin = new Thickness(0, 0, 6, 0), Child = CreateFaviconOrGlyph(_carryTab, 15, true) });
                    sp.Children.Add(new TextBlock { Text = _carryTab.DisplayTitle, FontSize = 12.5, Foreground = Palette.Ink, Opacity = 0.75, VerticalAlignment = VerticalAlignment.Center });
                    _rowGhost = new Border
                    {
                        Height = Store.Prefs.RoomierTabs ? 36 : 28,
                        CornerRadius = new CornerRadius(9),
                        Margin = new Thickness(0, 1, 0, 1),
                        Padding = new Thickness(10, 0, 10, 0),
                        Background = Palette.FrameWash,
                        BorderBrush = Palette.InkGhostEdge,
                        BorderThickness = new Thickness(1.5),
                        Opacity = 0.85,
                        Child = sp
                    };
                }
                if (_tabsStack.Children.Contains(_rowGhost)) _tabsStack.Children.Remove(_rowGhost);
                _tabsStack.Children.Insert(Math.Min(targetRowIdx, Math.Max(0, _tabsStack.Children.Count - 1)), _rowGhost);
            }
            else
            {
                _carryOverPins = false;
                _carryOverTabs = false;
                if (_carryVisual != null) _carryVisual.Opacity = 0.92;
                if (_pinGhost != null) { _pinsPanel.Children.Remove(_pinGhost); _pinGhost = null; }
                if (_rowGhost != null) { _tabsStack.Children.Remove(_rowGhost); _rowGhost = null; }
            }
        }

        private async void OnCarryMouseUp(object sender, MouseButtonEventArgs e)
        {
            var tab = _carryTab;
            bool wasCarrying = _isCarrying;
            bool dropPin = _carryOverPins;
            bool dropTabs = _carryOverTabs;
            bool dropStage = _carryOverStage;
            int dropIdx = _carryDropIndex;
            Point stagePt = e.GetPosition(_pageCardBorder);
            CancelCarry();

            if (tab == null) return;
            if (!wasCarrying)
            {
                await SelectTabAsync(tab);
                return;
            }
            if (dropStage && tab != _activeTab)
            {
                bool dropLeft = stagePt.X < (_pageCardBorder.ActualWidth / 2.0);
                await OpenTabInSplitViewAsync(tab, dropLeft);
                return;
            }
            if (dropPin && !tab.IsPrivate)
            {
                bool newlyPinned = !tab.Pinned;
                tab.Pinned = true;
                if (newlyPinned) tab.HomeUrl = tab.Url;
                _justPinnedTabId = tab.Id;

                var pinnedInSpace = GetTabsForActiveSpace().Where(t => t.Pinned && t != tab).ToList();
                _allTabs.Remove(tab);
                if (dropIdx >= 0 && dropIdx < pinnedInSpace.Count)
                {
                    int globalIdx = _allTabs.IndexOf(pinnedInSpace[dropIdx]);
                    _allTabs.Insert(Math.Max(0, globalIdx), tab);
                }
                else
                {
                    _allTabs.Add(tab);
                }
                RefreshTabRows();
                SaveCurrentSession();
            }
            else if (dropTabs)
            {
                tab.Pinned = false;
                var unpinnedInSpace = GetTabsForActiveSpace().Where(t => !t.Pinned && t != tab).ToList();
                _allTabs.Remove(tab);
                if (dropIdx >= 0 && dropIdx < unpinnedInSpace.Count)
                {
                    int globalIdx = _allTabs.IndexOf(unpinnedInSpace[dropIdx]);
                    _allTabs.Insert(Math.Max(0, globalIdx), tab);
                }
                else
                {
                    _allTabs.Add(tab);
                }
                RefreshTabRows();
                SaveCurrentSession();
            }
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
                Background = isLive ? Palette.FrameWash : Brushes.Transparent,
                Cursor = Cursors.Hand
            };

            var dock = new DockPanel();
            if (!tab.Pinned)
            {
                var closeBtn = new Border
                {
                    Width = 15, Height = 15, CornerRadius = new CornerRadius(7.5),
                    Background = Brushes.Transparent,
                    Margin = new Thickness(6, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                    Child = VectorIcons.Draw(VectorIcons.CloseSmallPath, Palette.FrameMuted, 7.5, 1.6)
                };
                var capturedClose = tab;
                closeBtn.MouseEnter += (s, e) => closeBtn.Background = Palette.InkCloseDisc;
                closeBtn.MouseLeave += (s, e) => closeBtn.Background = Brushes.Transparent;
                closeBtn.MouseLeftButtonDown += (s, e) =>
                {
                    e.Handled = true;
                    CloseTab(capturedClose, true);
                };
                DockPanel.SetDock(closeBtn, Dock.Right);
                dock.Children.Add(closeBtn);
            }

            if (tab.IsPrivate)
            {
                var privIcon = new Border
                {
                    Width = 14, Height = 14, Margin = new Thickness(0, 0, 6, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                    Child = VectorIcons.Draw(VectorIcons.EyeOffPath, Palette.FrameMuted, 12, 1.45)
                };
                DockPanel.SetDock(privIcon, Dock.Left);
                dock.Children.Add(privIcon);
            }

            var label = new TextBlock
            {
                Text = tab.Pinned ? tab.PinGlyph : tab.DisplayTitle,
                FontSize = 12.5,
                FontWeight = isLive ? FontWeights.Medium : FontWeights.Normal,
                Foreground = isLive ? Palette.Ink : (Store.Prefs.HighContrastTabText ? Palette.Ink : Palette.FrameMuted),
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
            pill.MouseDown += (s, e) =>
            {
                if (e.ChangedButton == MouseButton.Middle) { e.Handled = true; CloseTab(captured, true); }
            };
            pill.MouseRightButtonUp += (s, e) => { e.Handled = true; ShowTabContextMenu(captured); };
            return pill;
        }

        private void ShowTabContextMenu(BrowserTab tab)
        {
            StackPanel stack;
            var popup = CreateThemedPopupMenu(null, System.Windows.Controls.Primitives.PlacementMode.MousePoint, out stack);
            string host = Address.DisplayHost(tab.Url);
            bool isWebUrl = !string.IsNullOrEmpty(tab.Url) && tab.Url.StartsWith("http", StringComparison.OrdinalIgnoreCase);

            if (!tab.IsPrivate)
            {
                if (tab.Pinned)
                {
                    AddPopupMenuItem(popup, stack, Store.Prefs.PinnedRows ? "Show as Square" : "Show as Row", "", () =>
                    {
                        Store.Prefs.PinnedRows = !Store.Prefs.PinnedRows;
                        Store.SavePrefs();
                        RefreshTabRows();
                    });
                    AddPopupMenuItem(popup, stack, "Change Letter", "", () =>
                    {
                        string letter = PromptForText("Change Pin Letter", "Enter a single letter (leave empty to reset):", tab.CustomLetter ?? tab.PinGlyph);
                        if (letter != null)
                        {
                            tab.CustomLetter = string.IsNullOrWhiteSpace(letter) ? "" : letter.Trim().Substring(0, 1).ToUpperInvariant();
                            RefreshTabRows();
                            SaveCurrentSession();
                        }
                    });
                    bool canReplace = !string.IsNullOrEmpty(tab.Url) && !string.Equals(tab.Url, tab.HomeUrl, StringComparison.OrdinalIgnoreCase);
                    AddPopupMenuItem(popup, stack, "Replace Pinned Page with Current URL", "", () =>
                    {
                        tab.HomeUrl = tab.Url;
                        SaveCurrentSession();
                    }, canReplace);
                    AddPopupMenuItem(popup, stack, tab.KeepInEverySpace ? "Keep in This Space Only" : "Keep in Every Space", "", () =>
                    {
                        tab.KeepInEverySpace = !tab.KeepInEverySpace;
                        RefreshTabRows();
                        SaveCurrentSession();
                    });
                    AddPopupMenuItem(popup, stack, "Unpin", "", () =>
                    {
                        tab.Pinned = false;
                        RefreshTabRows();
                        SaveCurrentSession();
                    });
                    AddPopupMenuItem(popup, stack, "Delete", "", () => CloseTab(tab, false, true));
                }
                else
                {
                    AddPopupMenuItem(popup, stack, "Pin", "", () =>
                    {
                        tab.Pinned = true;
                        tab.HomeUrl = tab.Url;
                        RefreshTabRows();
                        SaveCurrentSession();
                    });
                }

                if (Store.Spaces.Count > 1)
                {
                    AddPopupMenuItem(popup, stack, "Move to Space", "›", () =>
                    {
                        StackPanel spStack;
                        var spMenu = CreateThemedPopupMenu(null, System.Windows.Controls.Primitives.PlacementMode.MousePoint, out spStack);
                        foreach (var sp in Store.Spaces)
                        {
                            var capSp = sp;
                            bool isHere = string.Equals(tab.SpaceId, capSp.Id, StringComparison.OrdinalIgnoreCase);
                            AddPopupMenuItem(spMenu, spStack, (isHere ? "✓  " : "     ") + capSp.Name, "", () =>
                            {
                                tab.SpaceId = capSp.Id;
                                tab.KeepInEverySpace = false;
                                RefreshTabRows();
                                SaveCurrentSession();
                            }, !isHere);
                        }
                        spMenu.IsOpen = true;
                    });
                }
            }

            AddPopupMenuItem(popup, stack, "Open in Split View", "", async () =>
            {
                await OpenTabInSplitViewAsync(tab, false);
            }, tab != _activeTab);

            AddPopupSeparator(stack);

            AddPopupMenuItem(popup, stack, "Rename", "", () =>
            {
                string name = PromptForText("Rename Tab", "Tab title (leave empty to reset):", tab.CustomTitle ?? tab.DisplayTitle);
                if (name != null)
                {
                    tab.CustomTitle = name.Trim();
                    RefreshTabRows();
                    SaveCurrentSession();
                }
            });
            AddPopupMenuItem(popup, stack, "Duplicate", "", () =>
            {
                CreateAndSelectTab(tab.Url, false, tab.IsPrivate, tab.GroupId, tab);
            });
            AddPopupMenuItem(popup, stack, "Site Information…", "", () =>
            {
                bool isHttps = tab.Url != null && tab.Url.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
                bool paused = Shield.IsPaused(host);
                MessageBox.Show(
                    "Site: " + (string.IsNullOrEmpty(host) ? tab.Url : host) +
                    "\nConnection: " + (isHttps ? "Secure (HTTPS)" : "Standard") +
                    "\nAd & Tracker Shield: " + (paused ? "Paused on this site" : (Store.Prefs.ShieldEnabled ? "Active" : "Off")) +
                    "\nDark Mode (Dusk): " + (PageFeatures.IsDuskDisabledForSite(host) ? "Disabled on this site" : "Automatic"),
                    "Site Information", MessageBoxButton.OK, MessageBoxImage.Information);
            }, isWebUrl && !tab.IsSleeping);
            AddPopupMenuItem(popup, stack, "Copy Address", "", () =>
            {
                SafeSetClipboardText(tab.Url ?? "");
            });
            AddPopupMenuItem(popup, stack, "Copy as Markdown Link", "", () =>
            {
                SafeSetClipboardText("[" + tab.DisplayTitle + "](" + (tab.Url ?? "") + ")");
            });
            string cleanUrl = CleanLink.Clean(tab.Url, true);
            AddPopupMenuItem(popup, stack, "Copy Clean Link", "", () =>
            {
                SafeSetClipboardText(cleanUrl);
            }, isWebUrl && !string.Equals(cleanUrl, tab.Url, StringComparison.Ordinal));
            AddPopupMenuItem(popup, stack, "Open in Side Panel", "", async () =>
            {
                if (tab != _activeTab) await OpenTabInSplitViewAsync(tab, false);
                else ToggleSplitView();
            });
            AddPopupMenuItem(popup, stack, tab.IsMuted ? "Unmute Tab" : "Mute Tab", "", () =>
            {
                tab.IsMuted = !tab.IsMuted;
                if (tab.WebView != null && tab.IsWebViewInitialized && tab.WebView.CoreWebView2 != null)
                    tab.WebView.CoreWebView2.IsMuted = tab.IsMuted;
                RefreshTabRows();
                UpdateMediaPlayerCard();
            });
            if (!string.IsNullOrEmpty(host))
            {
                bool shieldPaused = Shield.IsPaused(host);
                AddPopupMenuItem(popup, stack, shieldPaused ? "Block Ads on This Site" : "Stop Blocking Ads on This Site", "", () =>
                {
                    Shield.TogglePause(host);
                    if (tab.WebView != null && tab.IsWebViewInitialized) tab.WebView.CoreWebView2.Reload();
                });
            }
            AddPopupMenuItem(popup, stack, "Allow Copying on This Site", "", async () =>
            {
                if (tab.WebView != null && tab.IsWebViewInitialized)
                {
                    await tab.WebView.CoreWebView2.ExecuteScriptAsync(
                        "(function(){var s=document.createElement('style');s.textContent='*{user-select:text!important;-webkit-user-select:text!important;}';(document.head||document.documentElement).appendChild(s);['copy','cut','selectstart','contextmenu'].forEach(function(ev){window.addEventListener(ev,function(e){e.stopPropagation();},true);});})();");
                }
            }, isWebUrl && !tab.IsSleeping);
            AddPopupMenuItem(popup, stack, tab.KeepAwake ? "Allow Tab to Sleep" : "Keep This Site Awake", "", () =>
            {
                tab.KeepAwake = !tab.KeepAwake;
                RefreshTabRows();
                SaveCurrentSession();
            });
            AddPopupMenuItem(popup, stack, tab.IsSleeping ? "Wake Tab" : "Put to Sleep", "", async () =>
            {
                if (tab.IsSleeping)
                {
                    await SelectTabAsync(tab);
                    return;
                }
                tab.KeepAwake = false;
                tab.PutToSleep();
                RefreshTabRows();
                SaveCurrentSession();
            }, tab != _activeTab);

            AddPopupSeparator(stack);

            AddPopupMenuItem(popup, stack, "Close Tab", "", () => CloseTab(tab, true));
            var spaceUnpinned = GetTabsForActiveSpace().Where(t => !t.Pinned).ToList();
            AddPopupMenuItem(popup, stack, "Close Other Tabs", "", () =>
            {
                foreach (var ot in spaceUnpinned.Where(t => t != tab).ToList()) CloseTab(ot, true);
            }, spaceUnpinned.Count > 1);
            int curUnpinnedIdx = spaceUnpinned.IndexOf(tab);
            AddPopupMenuItem(popup, stack, "Close Tabs Below", "", () =>
            {
                if (curUnpinnedIdx >= 0)
                {
                    foreach (var bt in spaceUnpinned.Skip(curUnpinnedIdx + 1).ToList()) CloseTab(bt, true);
                }
            }, curUnpinnedIdx >= 0 && curUnpinnedIdx < spaceUnpinned.Count - 1);
            AddPopupMenuItem(popup, stack, "Reopen Closed Tab", "", () => ReopenClosedTab(), _closedTabs.Count > 0);

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
            // Ctrl+L toggles closed if already open in the same mode
            if (_omniboxPopup != null && _omniboxPopup.IsOpen &&
                _omniboxIsTabSwitcherMode == tabSwitcherMode && _omniboxIsNewTabMode == (newTabMode && !tabSwitcherMode))
            {
                HideOmnibox();
                return;
            }
            _omniboxIsTabSwitcherMode = tabSwitcherMode;
            _omniboxIsNewTabMode = newTabMode && !tabSwitcherMode;
            SetDrawerVisible(false);
            _omniboxCard.Background = Palette.Ground;
            _omniboxCard.BorderBrush = Palette.Hairline;
            _omniboxInput.Foreground = Palette.Ink;
            _omniboxInput.CaretBrush = Palette.Ink;
            _omniboxOffersBorder.Background = Palette.CardSurface;
            _omniboxOffersBorder.BorderBrush = Palette.CardEdge;
            if (_omniboxContainer != null)
            {
                _omniboxContainer.Margin = Store.Prefs.FieldInMiddleOfWindow
                    ? new Thickness(0, 12, 0, 0)
                    : new Thickness(0, 48, 0, 0);
            }
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
                _omniboxInput.Text = (currentUrl == "about:blank" || (currentUrl != null && currentUrl.StartsWith("search://"))) ? "" : Address.ShownAddress(currentUrl);
            }

            RefreshOmniboxOffers();
            FocusPopupControl(_omniboxInput, true);
        }

        private void HideOmnibox()
        {
            if (_omniboxPopup != null) _omniboxPopup.IsOpen = false;
            if (_activeTab != null && _activeTab.WebView != null)
                _activeTab.WebView.Focus();
        }

        private bool TryRunAddressBarCommand(string q)
        {
            if (!Store.Prefs.AddressBarCommands || string.IsNullOrWhiteSpace(q)) return false;
            string cmd = q.Trim().ToLowerInvariant();
            if (cmd == "settings" || cmd == "preferences") { HideOmnibox(); OpenOrSelectSettingsTab("general"); return true; }
            if (cmd == "new tab") { HideOmnibox(); CreateAndSelectTab("about:blank", false); return true; }
            if (cmd == "history") { HideOmnibox(); OpenOrSelectHistoryTab(); return true; }
            if (cmd == "downloads") { HideOmnibox(); ShowDownloadsDrawer(); return true; }
            if (cmd == "bookmarks") { HideOmnibox(); ShowBookmarksDrawer(); return true; }
            if (cmd == "passwords") { HideOmnibox(); OpenOrSelectSettingsTab("passwords"); return true; }
            if (cmd == "extensions") { HideOmnibox(); OpenOrSelectSettingsTab("extensions"); return true; }
            return false;
        }

        private void RefreshOmniboxOffers()
        {
            _omniboxOffersStack.Children.Clear();
            _currentOfferActions.Clear();
            _selectedOfferIndex = -1;

            string q = (_omniboxInput.Text ?? "").Trim();
            bool openInNew = _omniboxIsNewTabMode;

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
                        AddOmniboxOfferRow("→  Go to " + Address.Pretty(parsedUrl), "Open Address", () =>
                        {
                            HideOmnibox();
                            if (openInNew) CreateAndSelectTab(parsedUrl, false);
                            else if (_activeTab != null) _activeTab.Navigate(parsedUrl);
                        });
                    }
                    else
                    {
                        AddOmniboxOfferRow("⌕  Search " + Store.Prefs.SearchEngineName + " for \"" + q + "\"", "Web Search", () =>
                        {
                            HideOmnibox();
                            if (openInNew) CreateAndSelectTab(q, false);
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
                        AddOmniboxOfferRow("★  " + bm.Title, Address.DisplayHost(u), () =>
                        {
                            HideOmnibox();
                            if (openInNew) CreateAndSelectTab(u, false);
                            else if (_activeTab != null) _activeTab.Navigate(u);
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
                            if (openInNew) CreateAndSelectTab(u, false);
                            else if (_activeTab != null) _activeTab.Navigate(u);
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
                            if (Store.Prefs.WindowsHelloForVault && !WindowsHelloVerifier.VerifyUser(this)) return;
                            await FillSavedLoginAsync(capLogin);
                        });
                    }

                    foreach (var bm in Store.Bookmarks.Take(5))
                    {
                        string u = bm.Url;
                        AddOmniboxOfferRow("★  " + bm.Title, Address.DisplayHost(u), () =>
                        {
                            HideOmnibox();
                            if (openInNew) CreateAndSelectTab(u, false);
                            else if (_activeTab != null) _activeTab.Navigate(u);
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
                string txt = (_omniboxInput.Text ?? "").Trim();
                bool shift = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
                bool alt = Keyboard.Modifiers.HasFlag(ModifierKeys.Alt);
                bool isNew = _omniboxIsNewTabMode;

                if (!_omniboxIsTabSwitcherMode && TryRunAddressBarCommand(txt))
                {
                    e.Handled = true;
                    return;
                }

                // Shift+Enter searches with secondary ShiftReturnEngine if configured
                if (!_omniboxIsTabSwitcherMode && shift && !string.IsNullOrEmpty(txt) &&
                    !string.Equals(Store.Prefs.ShiftReturnEngine, "Off", StringComparison.OrdinalIgnoreCase))
                {
                    string altUrl = Address.SearchUrl(txt, Store.Prefs.ShiftReturnEngine);
                    HideOmnibox();
                    if (isNew || alt) CreateAndSelectTab(altUrl, false, false, null, _activeTab, alt);
                    else if (_activeTab != null) _activeTab.Navigate(altUrl);
                    e.Handled = true;
                    return;
                }

                // Alt+Enter opens in a background tab
                if (!_omniboxIsTabSwitcherMode && alt && !string.IsNullOrEmpty(txt))
                {
                    HideOmnibox();
                    CreateAndSelectTab(txt, false, false, null, _activeTab, true);
                    e.Handled = true;
                    return;
                }

                if (_selectedOfferIndex >= 0 && _selectedOfferIndex < _currentOfferActions.Count)
                {
                    _currentOfferActions[_selectedOfferIndex]();
                }
                else if (!_omniboxIsTabSwitcherMode)
                {
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
            string uEsc = login.User.Replace("\\", "\\\\").Replace("'", "\\'").Replace("\r", "\\r").Replace("\n", "\\n").Replace("\u2028", "\\u2028").Replace("\u2029", "\\u2029");
            string pEsc = plain.Replace("\\", "\\\\").Replace("'", "\\'").Replace("\r", "\\r").Replace("\n", "\\n").Replace("\u2028", "\\u2028").Replace("\u2029", "\\u2029");
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

        private bool HandleEscapeKey()
        {
            if (_isCarrying)
            {
                CancelCarry();
                return true;
            }
            if (_switcherPopup != null && _switcherPopup.IsOpen)
            {
                _switcherPopup.IsOpen = false;
                return true;
            }
            if (_omniboxPopup != null && _omniboxPopup.IsOpen)
            {
                HideOmnibox();
                return true;
            }
            if (_peekPopup != null && _peekPopup.IsOpen)
            {
                ClosePeekCard();
                return true;
            }
            if (_extPopup != null && _extPopup.IsOpen)
            {
                CloseExtensionPopup();
                return true;
            }
            if (_drawerPopup != null && _drawerPopup.IsOpen)
            {
                SetDrawerVisible(false);
                return true;
            }
            if (_findPopup != null && _findPopup.IsOpen)
            {
                HideFindBar();
                return true;
            }
            if (_activeTab != null && _activeTab.IsVeilPicking)
            {
                StopVeilPicking();
                return true;
            }
            if (_isFullScreen)
            {
                if (Store.Prefs.EscTwiceLeavesFullScreen)
                {
                    if ((DateTime.UtcNow - _lastFullScreenEscUtc).TotalMilliseconds < 1500)
                    {
                        _lastFullScreenEscUtc = DateTime.MinValue;
                        ToggleFullScreen();
                    }
                    else
                    {
                        _lastFullScreenEscUtc = DateTime.UtcNow;
                        ShowSpaceToast("Press Esc again to leave full screen");
                    }
                }
                else
                {
                    ToggleFullScreen();
                }
                return true;
            }
            return false;
        }

        private void StepMruSwitcher(bool backwards)
        {
            var spaceTabs = GetTabsForActiveSpace();
            var candidates = _mruTabs.Where(t => spaceTabs.Contains(t)).ToList();
            foreach (var st in spaceTabs)
            {
                if (!candidates.Contains(st)) candidates.Add(st);
            }
            if (Store.Prefs.CtrlTabSkipsPinned)
                candidates = candidates.Where(t => !t.Pinned).ToList();
            if (candidates.Count < 2) return;

            if (_switcherPopup == null || !_switcherPopup.IsOpen)
            {
                _switcherList = candidates.Take(8).ToList();
                _switcherIndex = backwards ? (_switcherList.Count - 1) : 1;
            }
            else
            {
                _switcherIndex = backwards
                    ? (_switcherIndex - 1 + _switcherList.Count) % _switcherList.Count
                    : (_switcherIndex + 1) % _switcherList.Count;
            }

            _switcherStack.Children.Clear();
            _switcherCard.Background = Palette.MenuBg;
            _switcherCard.BorderBrush = Palette.MenuBorder;
            for (int i = 0; i < _switcherList.Count; i++)
            {
                var t = _switcherList[i];
                bool sel = i == _switcherIndex;
                var tile = new Border
                {
                    Width = 112,
                    Height = 76,
                    Margin = new Thickness(4, 0, 4, 0),
                    Padding = new Thickness(8),
                    CornerRadius = new CornerRadius(10),
                    Background = sel ? Palette.MenuHot : Brushes.Transparent,
                    BorderBrush = sel ? Palette.InkPickedOutline : Brushes.Transparent,
                    BorderThickness = new Thickness(1)
                };
                var sp = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
                sp.Children.Add(new Border { Width = 24, Height = 24, Margin = new Thickness(0, 0, 0, 6), Child = CreateFaviconOrGlyph(t, 20, true) });
                sp.Children.Add(new TextBlock
                {
                    Text = t.DisplayTitle,
                    FontSize = 11.5,
                    Foreground = sel ? Palette.Ink : Palette.MenuSecondary,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    MaxWidth = 96
                });
                tile.Child = sp;
                _switcherStack.Children.Add(tile);
            }
            if (_switcherPopup != null) _switcherPopup.IsOpen = true;
        }

        private async Task CommitMruSwitcherAsync()
        {
            if (_switcherPopup == null || !_switcherPopup.IsOpen) return;
            _switcherPopup.IsOpen = false;
            if (_switcherIndex >= 0 && _switcherIndex < _switcherList.Count)
            {
                await SelectTabAsync(_switcherList[_switcherIndex]);
            }
        }

        private async Task StepAdjacentTabAsync(bool prev)
        {
            var spaceTabs = GetTabsForActiveSpace();
            if (spaceTabs.Count > 1 && _activeTab != null)
            {
                int cur = spaceTabs.IndexOf(_activeTab);
                int next = prev
                    ? (cur - 1 + spaceTabs.Count) % spaceTabs.Count
                    : (cur + 1) % spaceTabs.Count;
                await SelectTabAsync(spaceTabs[next]);
            }
        }

        private async void OnWindowPreviewKeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl)
            {
                if (_isCtrlHeld)
                {
                    _isCtrlHeld = false;
                    if (Store.Prefs.ShowTabNumbersOnCtrl) RefreshTabRows();
                }
                if (_switcherPopup != null && _switcherPopup.IsOpen)
                {
                    await CommitMruSwitcherAsync();
                }
            }
        }

        // KEYBOARD SHORTCUTS
        private void OnThreadPreprocessMessage(ref System.Windows.Interop.MSG msg, ref bool handled)
        {
            if (handled || !IsActive) return;
            if (msg.message != 0x0104 && msg.message != 0x0100) return; // WM_SYSKEYDOWN or WM_KEYDOWN

            int vk = msg.wParam.ToInt32();
            var mods = Keyboard.Modifiers;
            bool ctrl = mods.HasFlag(ModifierKeys.Control);
            bool shift = mods.HasFlag(ModifierKeys.Shift);
            bool alt = mods.HasFlag(ModifierKeys.Alt) || msg.message == 0x0104;

            // 1. Alt+1..9 (and NumPad 1..9) — instant Space switching with zero Windows beep
            if (alt && !ctrl && !shift)
            {
                int spIdx = -1;
                if (vk >= 0x31 && vk <= 0x39) spIdx = vk - 0x31;
                else if (vk >= 0x61 && vk <= 0x69) spIdx = vk - 0x61;
                if (spIdx >= 0)
                {
                    handled = true;
                    if (!ShouldRunShortcutOnce("A:" + (spIdx + 1))) return;
                    if (Store.Prefs.CtrlNumbersSwitchSpaces)
                    {
                        var spaceTabs = GetTabsForActiveSpace();
                        if (spIdx == 8 && spaceTabs.Count > 0) spIdx = spaceTabs.Count - 1;
                        if (spIdx < spaceTabs.Count) { var _ = SelectTabAsync(spaceTabs[spIdx]); }
                    }
                    else if (spIdx < Store.Spaces.Count)
                    {
                        CycleSpace(spIdx);
                    }
                    return;
                }
            }

            // 2. High-frequency browser Ctrl shortcuts intercepted at Win32 loop (0ms latency, works even mid-load)
            if (ctrl && !alt)
            {
                string keyStr = null;
                if (!shift)
                {
                    if (vk == 0x48) keyStr = "h";        // Ctrl+H (History)
                    else if (vk == 0x59) keyStr = "y";   // Ctrl+Y (History)
                    else if (vk == 0xBC) keyStr = ",";   // Ctrl+, (Settings)
                    else if (vk == 0x54) keyStr = "t";   // Ctrl+T (New Tab)
                    else if (vk == 0x57) keyStr = "w";   // Ctrl+W (Close Tab)
                    else if (vk == 0x4C) keyStr = "l";   // Ctrl+L (Address)
                    else if (vk == 0x4B) keyStr = "k";   // Ctrl+K (Switcher)
                    else if (vk == 0x53) keyStr = "s";   // Ctrl+S (Fold Sidebar)
                    else if (vk == 0x46) keyStr = "f";   // Ctrl+F (Find)
                    else if (vk == 0x4A) keyStr = "j";   // Ctrl+J (Downloads)
                    else if (vk == 0x52) keyStr = "r";   // Ctrl+R (Reload)
                    else if (vk == 0xDB) keyStr = "[";   // Ctrl+[ (Back)
                    else if (vk == 0xDD) keyStr = "]";   // Ctrl+] (Forward)
                    else if (vk >= 0x31 && vk <= 0x39)
                    {
                        int idx = vk - 0x31;
                        handled = true;
                        if (!ShouldRunShortcutOnce("C:" + (idx + 1))) return;
                        if (Store.Prefs.CtrlNumbersSwitchSpaces && idx < Store.Spaces.Count) { CycleSpace(idx); return; }
                        var spaceTabs = GetTabsForActiveSpace();
                        if (idx == 8 && spaceTabs.Count > 0) idx = spaceTabs.Count - 1;
                        if (idx < spaceTabs.Count) { var _ = SelectTabAsync(spaceTabs[idx]); }
                        return;
                    }
                }
                else
                {
                    if (vk == 0x54) keyStr = "t";        // Ctrl+Shift+T (Reopen Closed)
                    else if (vk == 0x4E) keyStr = "n";   // Ctrl+Shift+N (Private Tab)
                    else if (vk == 0x53) keyStr = "s";   // Ctrl+Shift+S (Top/Left Bar)
                    else if (vk == 0x42) keyStr = "b";   // Ctrl+Shift+B (Bookmark)
                    else if (vk == 0x52) keyStr = "r";   // Ctrl+Shift+R (Reader)
                    else if (vk == 0x48) keyStr = "h";   // Ctrl+Shift+H (Curtain)
                    else if (vk == 0x50) keyStr = "p";   // Ctrl+Shift+P (PiP)
                    else if (vk == 0x4A) keyStr = "j";   // Ctrl+Shift+J (Downloads)
                }

                if (keyStr != null)
                {
                    handled = true;
                    string sig = "C" + (shift ? "S" : "") + ":" + keyStr;
                    if (!ShouldRunShortcutOnce(sig)) return;
                    var _ = HandleShortcutKeyAsync(keyStr, shift);
                    return;
                }
            }
        }

        private async void OnWindowPreviewKeyDown(object sender, KeyEventArgs e)
        {
            var mods = Keyboard.Modifiers;
            bool ctrl = mods.HasFlag(ModifierKeys.Control);
            bool shift = mods.HasFlag(ModifierKeys.Shift);
            bool alt = mods.HasFlag(ModifierKeys.Alt);
            // CRITICAL on Windows WPF: when Alt is held, e.Key is Key.System and the real key is e.SystemKey!
            Key k = (e.Key == Key.System) ? e.SystemKey : e.Key;

            if ((k == Key.LeftCtrl || k == Key.RightCtrl) && !_isCtrlHeld)
            {
                _isCtrlHeld = true;
                if (Store.Prefs.ShowTabNumbersOnCtrl) RefreshTabRows();
            }

            if (k == Key.Escape)
            {
                if (HandleEscapeKey())
                {
                    e.Handled = true;
                    return;
                }
            }

            if (k == Key.F11)
            {
                ToggleFullScreen();
                e.Handled = true;
                return;
            }
            if (k == Key.F12 || (ctrl && shift && !alt && k == Key.I))
            {
                if (_activeTab != null && _activeTab.IsWebViewInitialized && _activeTab.WebView != null)
                    _activeTab.WebView.CoreWebView2.OpenDevToolsWindow();
                e.Handled = true;
                return;
            }
            if (k == Key.F5)
            {
                if (_activeTab != null && _activeTab.IsWebViewInitialized && _activeTab.WebView != null)
                {
                    if (ctrl || shift) await _activeTab.WebView.CoreWebView2.ExecuteScriptAsync("location.reload(true)");
                    else _activeTab.WebView.CoreWebView2.Reload();
                }
                e.Handled = true;
                return;
            }
            if (k == Key.F6 || (alt && !ctrl && !shift && k == Key.D))
            {
                ShowOmnibox(false, false);
                e.Handled = true;
                return;
            }
            if (k == Key.F3)
            {
                if (_findPopup != null && _findPopup.IsOpen && !string.IsNullOrEmpty(_findInput.Text))
                    await ExecuteFindOnPageAsync(_findInput.Text, !shift);
                else
                    ShowFindBar();
                e.Handled = true;
                return;
            }
            if (ctrl && !alt && k == Key.F4)
            {
                if (_activeTab != null) CloseTab(_activeTab);
                e.Handled = true;
                return;
            }
            if (alt && !ctrl && !shift && k == Key.Home)
            {
                if (_activeTab != null && _activeTab.Pinned && !string.IsNullOrEmpty(_activeTab.HomeUrl))
                    _activeTab.Navigate(_activeTab.HomeUrl);
                e.Handled = true;
                return;
            }
            if (alt && shift && !ctrl && (k == Key.Left || k == Key.Right))
            {
                FocusSplitPane(k == Key.Left);
                e.Handled = true;
                return;
            }
            if (alt && !ctrl && !shift && (k == Key.Left || k == Key.Right))
            {
                if (_activeTab != null && _activeTab.WebView != null)
                {
                    if (k == Key.Left && _activeTab.WebView.CanGoBack) _activeTab.WebView.GoBack();
                    else if (k == Key.Right && _activeTab.WebView.CanGoForward) _activeTab.WebView.GoForward();
                }
                e.Handled = true;
                return;
            }
            if (alt && shift && !ctrl && k == Key.N)
            {
                ToggleSplitView();
                e.Handled = true;
                return;
            }
            if (alt && shift && !ctrl && k == Key.L)
            {
                OpenOrSelectSettingsTab("passwords");
                e.Handled = true;
                return;
            }
            if (alt && shift && !ctrl && k == Key.C)
            {
                if (_activeTab != null && !string.IsNullOrEmpty(_activeTab.Url))
                    SafeSetClipboardText(CleanLink.Clean(_activeTab.Url, true));
                e.Handled = true;
                return;
            }
            if (alt && shift && !ctrl && k == Key.D)
            {
                if (_activeTab != null)
                {
                    PageFeatures.ToggleDuskForSite(Address.DisplayHost(_activeTab.Url));
                    await _activeTab.ApplySiteStyleSheetsAsync();
                }
                e.Handled = true;
                return;
            }
            if (alt && !ctrl && !shift && ((k >= Key.D1 && k <= Key.D9) || (k >= Key.NumPad1 && k <= Key.NumPad9)))
            {
                int spIdx = (k >= Key.NumPad1 && k <= Key.NumPad9) ? (int)(k - Key.NumPad1) : (int)(k - Key.D1);
                if (Store.Prefs.CtrlNumbersSwitchSpaces)
                {
                    var spaceTabs = GetTabsForActiveSpace();
                    if (spIdx == 8 && spaceTabs.Count > 0) spIdx = spaceTabs.Count - 1;
                    if (spIdx < spaceTabs.Count) { await SelectTabAsync(spaceTabs[spIdx]); e.Handled = true; return; }
                }
                else if (spIdx < Store.Spaces.Count)
                {
                    CycleSpace(spIdx);
                    e.Handled = true;
                    return;
                }
            }

            if (ctrl && !alt)
            {
                if (!shift && (k >= Key.D1 && k <= Key.D9))
                {
                    int idx = (int)(k - Key.D1);
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
                if (k == Key.Tab)
                {
                    StepMruSwitcher(shift);
                    e.Handled = true;
                    return;
                }
                if (k == Key.PageDown || k == Key.PageUp)
                {
                    await StepAdjacentTabAsync(k == Key.PageUp);
                    e.Handled = true;
                    return;
                }

                string keyStr = KeyToShortcutString(k);
                if (!string.IsNullOrEmpty(keyStr))
                {
                    string sig = "C" + (shift ? "S" : "") + ":" + keyStr;
                    if (!ShouldRunShortcutOnce(sig)) { e.Handled = true; return; }
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
                case Key.O: return "o";
                case Key.M: return "m";
                case Key.Q: return "q";
                case Key.Delete: return "delete";
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
                        if (Store.Prefs.NewTabInFloatingField) ShowOmnibox(false, true);
                        else CreateAndSelectTab("about:blank", false);
                        return true;
                    case "w":
                        if (_activeTab != null) CloseTab(_activeTab);
                        return true;
                    case "n":
                        try { Process.Start(System.Reflection.Assembly.GetExecutingAssembly().Location); } catch { }
                        return true;
                    case "o":
                        if (_peekPopup != null && _peekPopup.IsOpen && _peekTab != null)
                        {
                            var keep = _peekTab;
                            _peekTab = null;
                            _peekWebHost.Child = null;
                            _peekPopup.IsOpen = false;
                            AttachTabEvents(keep);
                            InsertNewTab(keep, _activeTab);
                            await SelectTabAsync(keep);
                            RefreshTabRows();
                            return true;
                        }
                        using (var ofd = new System.Windows.Forms.OpenFileDialog())
                        {
                            ofd.Filter = "Web files|*.html;*.htm;*.pdf;*.svg;*.png;*.jpg;*.txt|All files|*.*";
                            if (ofd.ShowDialog() == System.Windows.Forms.DialogResult.OK && !string.IsNullOrWhiteSpace(ofd.FileName))
                                CreateAndSelectTab(new Uri(ofd.FileName).AbsoluteUri, false);
                        }
                        return true;
                    case "p":
                        if (_activeTab != null && _activeTab.IsWebViewInitialized && _activeTab.WebView != null)
                            await _activeTab.WebView.CoreWebView2.ExecuteScriptAsync("window.print()");
                        return true;
                    case "r":
                        if (_activeTab != null && _activeTab.IsWebViewInitialized && _activeTab.WebView != null)
                            _activeTab.WebView.CoreWebView2.Reload();
                        return true;
                    case "s":
                        ToggleSidebarFold();
                        return true;
                    case "h":
                    case "y":
                        OpenOrSelectHistoryTab();
                        return true;
                    case "j":
                        ShowDownloadsDrawer();
                        return true;
                    case "d":
                        if (_activeTab != null) CreateAndSelectTab(_activeTab.Url, false, _activeTab.IsPrivate, _activeTab.GroupId, _activeTab);
                        return true;
                    case "f":
                        ShowFindBar();
                        return true;
                    case "g":
                        if (_findPopup != null && _findPopup.IsOpen && !string.IsNullOrEmpty(_findInput.Text))
                            await ExecuteFindOnPageAsync(_findInput.Text, true);
                        else
                            ShowFindBar();
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
                            _activeTab.WebView.ZoomFactor = Store.Prefs.DefaultZoomPercent / 100.0;
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
                    case "w":
                        Close();
                        return true;
                    case "q":
                        Application.Current.Shutdown();
                        return true;
                    case "m":
                        if (_activeTab != null)
                        {
                            _activeTab.IsMuted = !_activeTab.IsMuted;
                            if (_activeTab.WebView != null && _activeTab.IsWebViewInitialized && _activeTab.WebView.CoreWebView2 != null)
                                _activeTab.WebView.CoreWebView2.IsMuted = _activeTab.IsMuted;
                            RefreshTabRows();
                            UpdateMediaPlayerCard();
                        }
                        return true;
                    case "o":
                        ShowBookmarksDrawer();
                        return true;
                    case "delete":
                        OpenOrSelectSettingsTab("privacy");
                        return true;
                    case "[":
                        await StepAdjacentTabAsync(true);
                        return true;
                    case "]":
                        await StepAdjacentTabAsync(false);
                        return true;
                    case "g":
                        if (_findPopup != null && _findPopup.IsOpen && !string.IsNullOrEmpty(_findInput.Text))
                            await ExecuteFindOnPageAsync(_findInput.Text, false);
                        else
                            ShowFindBar();
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
                        BookmarkCurrentPageAndShowCard();
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
                            SafeSetClipboardText(CleanLink.Clean(_activeTab.Url, true));
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

        private void BookmarkCurrentPageAndShowCard()
        {
            if (_activeTab == null || string.IsNullOrWhiteSpace(_activeTab.Url) || _activeTab.Url == "about:blank" || _activeTab.Url.StartsWith("search://"))
            {
                ShowBookmarksPopover(_bmDoor, false);
                return;
            }
            var existing = Store.Bookmarks.FirstOrDefault(b => string.Equals(b.Url, _activeTab.Url, StringComparison.OrdinalIgnoreCase));
            if (existing == null)
            {
                existing = new BookmarkItem
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Title = _activeTab.DisplayTitle,
                    Url = _activeTab.Url,
                    DateIso = DateTime.UtcNow.ToString("o")
                };
                Store.Bookmarks.Insert(0, existing);
                Store.SaveBookmarks();
                if (Store.Prefs.SyncEnabled) FolderSync.PushToSyncFolder();
                RefreshBookmarksBar();
                UpdateBookmarkDoorIcon();
            }
            ShowBookmarksPopover(_bmDoor, true);
        }

        // Exact 1:1 port of oriel-ref/src/ui/data/BookmarkDoor.tsx + data.css:
        // 280px rounded 12px Ground card + rotated diamond arrow, 27px rows with 15x15 r=3.3 monogram marks,
        // middle-click background open, right-click Open/Rename/Remove, hairline rule, Add/Edit foot row, and inline Bookmarked Card.
        private void ShowBookmarksPopover(FrameworkElement anchor, bool openCardDirectly = false)
        {
            bool canBookmark = _activeTab != null && !string.IsNullOrWhiteSpace(_activeTab.Url) && _activeTab.Url != "about:blank" && !_activeTab.Url.StartsWith("search://");
            var existing = canBookmark ? Store.Bookmarks.FirstOrDefault(b => string.Equals(b.Url, _activeTab.Url, StringComparison.OrdinalIgnoreCase)) : null;

            var cardHost = new Border
            {
                Width = 280,
                Background = Palette.Ground,
                BorderBrush = Palette.CardEdge,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Effect = new DropShadowEffect { BlurRadius = 20, ShadowDepth = 6, Opacity = 0.24 }
            };

            // Diamond pointer arrow at top right pointing directly at the bookmark door
            var wrapper = new Grid { Margin = new Thickness(8, 6, 8, 12) };
            var arrow = new Border
            {
                Width = 12,
                Height = 12,
                Background = Palette.Ground,
                BorderBrush = Palette.CardEdge,
                BorderThickness = new Thickness(1, 1, 0, 0),
                CornerRadius = new CornerRadius(2),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, -5, 16, 0),
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = new RotateTransform(45)
            };
            wrapper.Children.Add(cardHost);
            wrapper.Children.Add(arrow);

            var popup = new System.Windows.Controls.Primitives.Popup
            {
                Child = wrapper,
                PlacementTarget = anchor ?? _bmDoor,
                Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom,
                HorizontalOffset = -236,
                VerticalOffset = 4,
                AllowsTransparency = true,
                StaysOpen = false,
                PopupAnimation = System.Windows.Controls.Primitives.PopupAnimation.Fade
            };

            Action renderCard = null;
            Action renderList = null;

            renderCard = () =>
            {
                if (!canBookmark) { renderList(); return; }
                var curBm = Store.Bookmarks.FirstOrDefault(b => string.Equals(b.Url, _activeTab.Url, StringComparison.OrdinalIgnoreCase));
                if (curBm == null)
                {
                    curBm = new BookmarkItem
                    {
                        Id = Guid.NewGuid().ToString("N"),
                        Title = _activeTab.DisplayTitle,
                        Url = _activeTab.Url,
                        DateIso = DateTime.UtcNow.ToString("o")
                    };
                    Store.Bookmarks.Insert(0, curBm);
                    Store.SaveBookmarks();
                    if (Store.Prefs.SyncEnabled) FolderSync.PushToSyncFolder();
                    RefreshBookmarksBar();
                    UpdateBookmarkDoorIcon();
                }

                var panel = new StackPanel { Margin = new Thickness(14) };
                panel.Children.Add(new TextBlock
                {
                    Text = "Bookmarked",
                    FontSize = 13,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = Palette.Ink,
                    Margin = new Thickness(0, 0, 0, 12)
                });

                var nameRow = new DockPanel { Margin = new Thickness(0, 0, 0, 14) };
                var lbl = new TextBlock
                {
                    Text = "Name",
                    Width = 44,
                    FontSize = 11.5,
                    Foreground = Palette.Muted,
                    VerticalAlignment = VerticalAlignment.Center
                };
                DockPanel.SetDock(lbl, Dock.Left);
                nameRow.Children.Add(lbl);

                var nameBox = new TextBox
                {
                    Text = curBm.Title ?? "",
                    Height = 26,
                    FontSize = 12.5,
                    Background = Palette.Wash,
                    Foreground = Palette.Ink,
                    CaretBrush = Palette.Ink,
                    BorderThickness = new Thickness(0),
                    Padding = new Thickness(8, 4, 8, 4),
                    VerticalContentAlignment = VerticalAlignment.Center
                };
                var nameWrap = new Border { CornerRadius = new CornerRadius(8), Background = Palette.Wash, ClipToBounds = true, Child = nameBox };
                var capBm = curBm;
                nameBox.TextChanged += (s, e) =>
                {
                    capBm.Title = nameBox.Text;
                    Store.SaveBookmarks();
                    RefreshBookmarksBar();
                };
                nameBox.KeyDown += (s, e) =>
                {
                    if (e.Key == Key.Enter || e.Key == Key.Escape) { popup.IsOpen = false; e.Handled = true; }
                };
                nameRow.Children.Add(nameWrap);
                panel.Children.Add(nameRow);

                var btnsDock = new DockPanel();
                var rmBtn = new Border
                {
                    Padding = new Thickness(6, 4, 6, 4),
                    CornerRadius = new CornerRadius(6),
                    Background = Brushes.Transparent,
                    Cursor = Cursors.Hand,
                    Child = new TextBlock { Text = "Remove", FontSize = 12, Foreground = new SolidColorBrush(Color.FromRgb(229, 72, 77)) }
                };
                rmBtn.MouseEnter += (s, e) => rmBtn.Background = Palette.Hover;
                rmBtn.MouseLeave += (s, e) => rmBtn.Background = Brushes.Transparent;
                rmBtn.MouseLeftButtonUp += (s, e) =>
                {
                    e.Handled = true;
                    Store.Bookmarks.Remove(capBm);
                    Store.SaveBookmarks();
                    RefreshBookmarksBar();
                    UpdateBookmarkDoorIcon();
                    popup.IsOpen = false;
                };
                DockPanel.SetDock(rmBtn, Dock.Left);
                btnsDock.Children.Add(rmBtn);

                var doneBtn = new Border
                {
                    Padding = new Thickness(14, 5, 14, 5),
                    CornerRadius = new CornerRadius(999),
                    Background = Palette.Ink,
                    Cursor = Cursors.Hand,
                    Child = new TextBlock { Text = "Done", FontSize = 12, FontWeight = FontWeights.Medium, Foreground = Palette.Ground }
                };
                doneBtn.MouseLeftButtonUp += (s, e) => { e.Handled = true; popup.IsOpen = false; };
                DockPanel.SetDock(doneBtn, Dock.Right);
                btnsDock.Children.Add(doneBtn);
                btnsDock.Children.Add(new Grid());
                panel.Children.Add(btnsDock);

                cardHost.Child = panel;
                FocusPopupControl(nameBox, true);
            };

            renderList = () =>
            {
                var outer = new StackPanel();
                if (Store.Bookmarks.Count == 0)
                {
                    outer.Children.Add(new TextBlock
                    {
                        Text = "No bookmarks yet",
                        FontSize = 12.5,
                        Foreground = Palette.Muted,
                        Margin = new Thickness(14, 14, 14, 14)
                    });
                }
                else
                {
                    var scroll = new ScrollViewer
                    {
                        MaxHeight = 360,
                        VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
                        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
                    };
                    var rowsStack = new StackPanel { Margin = new Thickness(6) };
                    foreach (var bm in Store.Bookmarks.ToList())
                    {
                        var cap = bm;
                        var row = new Border
                        {
                            Height = 27,
                            CornerRadius = new CornerRadius(8),
                            Padding = new Thickness(10, 0, 10, 0),
                            Margin = new Thickness(0, 0.5, 0, 0.5),
                            Background = Brushes.Transparent,
                            Cursor = Cursors.Hand,
                            ToolTip = cap.Url
                        };
                        var rowDock = new DockPanel();
                        string letter = string.IsNullOrEmpty(cap.Title) ? "•" : cap.Title.Substring(0, 1).ToUpperInvariant();
                        var mark = new Border
                        {
                            Width = 15, Height = 15, CornerRadius = new CornerRadius(3.3),
                            Background = Palette.InkMarkBg, Margin = new Thickness(0, 0, 8, 0),
                            VerticalAlignment = VerticalAlignment.Center,
                            Child = new TextBlock { Text = letter, FontSize = 8.4, FontWeight = FontWeights.Medium, Foreground = Palette.Muted, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
                        };
                        DockPanel.SetDock(mark, Dock.Left);
                        rowDock.Children.Add(mark);
                        rowDock.Children.Add(new TextBlock
                        {
                            Text = string.IsNullOrWhiteSpace(cap.Title) ? Address.DisplayHost(cap.Url) : cap.Title,
                            FontSize = 12.5,
                            Foreground = Palette.Ink,
                            VerticalAlignment = VerticalAlignment.Center,
                            TextTrimming = TextTrimming.CharacterEllipsis
                        });
                        row.Child = rowDock;
                        row.MouseEnter += (s, e) => row.Background = Palette.Wash;
                        row.MouseLeave += (s, e) => row.Background = Brushes.Transparent;
                        row.MouseDown += (s, e) =>
                        {
                            if (e.ChangedButton == MouseButton.Middle)
                            {
                                e.Handled = true;
                                popup.IsOpen = false;
                                CreateAndSelectTab(cap.Url, false, false, null, _activeTab, true);
                            }
                        };
                        row.MouseLeftButtonUp += (s, e) =>
                        {
                            e.Handled = true;
                            popup.IsOpen = false;
                            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) CreateAndSelectTab(cap.Url, false);
                            else if (_activeTab != null) _activeTab.Navigate(cap.Url);
                        };
                        row.MouseRightButtonUp += (s, e) =>
                        {
                            e.Handled = true;
                            StackPanel ms;
                            var cm = CreateThemedPopupMenu(null, System.Windows.Controls.Primitives.PlacementMode.MousePoint, out ms);
                            AddPopupMenuItem(cm, ms, "Open", "", () => { popup.IsOpen = false; if (_activeTab != null) _activeTab.Navigate(cap.Url); });
                            AddPopupMenuItem(cm, ms, "Open in New Tab", "", () => { popup.IsOpen = false; CreateAndSelectTab(cap.Url, false); });
                            AddPopupSeparator(ms);
                            AddPopupMenuItem(cm, ms, "Rename…", "", () =>
                            {
                                string n = PromptForText("Rename Bookmark", "Bookmark name:", cap.Title);
                                if (n != null) { cap.Title = n.Trim(); Store.SaveBookmarks(); RefreshBookmarksBar(); renderList(); }
                            });
                            AddPopupMenuItem(cm, ms, "Remove", "", () =>
                            {
                                Store.Bookmarks.Remove(cap); Store.SaveBookmarks(); RefreshBookmarksBar(); UpdateBookmarkDoorIcon(); renderList();
                            });
                            cm.IsOpen = true;
                        };
                        rowsStack.Children.Add(row);
                    }
                    scroll.Content = rowsStack;
                    outer.Children.Add(scroll);
                }

                outer.Children.Add(new Border { Height = 1, Background = Palette.Hairline });

                var foot = new StackPanel { Margin = new Thickness(6) };
                var addRow = new Border
                {
                    Height = 27,
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(10, 0, 10, 0),
                    Background = Brushes.Transparent,
                    Opacity = canBookmark ? 1.0 : 0.4,
                    Cursor = canBookmark ? Cursors.Hand : Cursors.Arrow
                };
                var addStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                addStack.Children.Add(new Border
                {
                    Width = 14, Height = 14, Margin = new Thickness(0, 0, 8, 0),
                    Child = existing != null
                        ? VectorIcons.DrawFilled(VectorIcons.BookmarkPath, Palette.Ink, 12)
                        : VectorIcons.Draw(VectorIcons.BookmarkPath, Palette.Muted, 12, 1.45)
                });
                addStack.Children.Add(new TextBlock
                {
                    Text = existing != null ? "Edit This Bookmark…" : "Add This Page",
                    FontSize = 12.5,
                    Foreground = Palette.Ink,
                    VerticalAlignment = VerticalAlignment.Center
                });
                addRow.Child = addStack;
                if (canBookmark)
                {
                    addRow.MouseEnter += (s, e) => addRow.Background = Palette.Wash;
                    addRow.MouseLeave += (s, e) => addRow.Background = Brushes.Transparent;
                    addRow.MouseLeftButtonUp += (s, e) => { e.Handled = true; renderCard(); };
                }
                foot.Children.Add(addRow);

                var importRow = new Border
                {
                    Height = 27,
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(10, 0, 10, 0),
                    Background = Brushes.Transparent,
                    Cursor = Cursors.Hand
                };
                importRow.Child = new TextBlock { Text = "Import Bookmarks…", FontSize = 12, Foreground = Palette.Muted, VerticalAlignment = VerticalAlignment.Center };
                importRow.MouseEnter += (s, e) => importRow.Background = Palette.Wash;
                importRow.MouseLeave += (s, e) => importRow.Background = Brushes.Transparent;
                importRow.MouseLeftButtonUp += (s, e) => { e.Handled = true; popup.IsOpen = false; ShowImportDrawer(); };
                foot.Children.Add(importRow);

                outer.Children.Add(foot);
                cardHost.Child = outer;
            };

            if (openCardDirectly && canBookmark) renderCard();
            else renderList();
            popup.IsOpen = true;
        }

        private void RefreshBookmarksBar()
        {
            if (_bookmarksBarStack == null) return;
            _bookmarksBarStack.Children.Clear();
            if (Store.Bookmarks.Count == 0)
            {
                _bookmarksBarStack.Children.Add(new TextBlock
                {
                    Text = "No bookmarks yet: Ctrl+Shift+B keeps the page you're on",
                    FontSize = 11.5,
                    Foreground = Palette.Muted,
                    VerticalAlignment = VerticalAlignment.Center
                });
                return;
            }
            foreach (var bm in Store.Bookmarks.Take(18).ToList())
            {
                var cap = bm;
                string letter = string.IsNullOrEmpty(cap.Title) ? "•" : cap.Title.Substring(0, 1).ToUpperInvariant();
                var rowStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                rowStack.Children.Add(new Border
                {
                    Width = 13, Height = 13, CornerRadius = new CornerRadius(3),
                    Background = Palette.InkMarkBg, Margin = new Thickness(0, 0, 6, 0),
                    Child = new TextBlock { Text = letter, FontSize = 8, FontWeight = FontWeights.SemiBold, Foreground = Palette.Muted, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
                });
                rowStack.Children.Add(new TextBlock
                {
                    Text = cap.Title,
                    MaxWidth = 150,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    FontSize = 12,
                    Foreground = Palette.Ink,
                    VerticalAlignment = VerticalAlignment.Center
                });
                var btn = new Border
                {
                    Height = 22,
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(7, 0, 7, 0),
                    Margin = new Thickness(0, 0, 4, 0),
                    Background = Brushes.Transparent,
                    Cursor = Cursors.Hand,
                    ToolTip = cap.Title + "\n" + cap.Url,
                    Child = rowStack
                };
                btn.MouseEnter += (s, e) => btn.Background = Palette.Hover;
                btn.MouseLeave += (s, e) => btn.Background = Brushes.Transparent;
                btn.MouseDown += (s, e) =>
                {
                    if (e.ChangedButton == MouseButton.Middle)
                    {
                        e.Handled = true;
                        CreateAndSelectTab(cap.Url, false, false, null, _activeTab, true);
                    }
                };
                btn.MouseLeftButtonUp += (s, e) =>
                {
                    if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) CreateAndSelectTab(cap.Url, false);
                    else if (_activeTab != null) _activeTab.Navigate(cap.Url);
                };
                _bookmarksBarStack.Children.Add(btn);
            }
        }

        private void ToggleBookmarkCurrentPage(bool openDrawer = true)
        {
            if (_activeTab == null || string.IsNullOrWhiteSpace(_activeTab.Url) || _activeTab.Url == "about:blank" || _activeTab.Url.StartsWith("search://")) return;
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
            if (Store.Prefs.SyncEnabled) FolderSync.PushToSyncFolder();
            RefreshBookmarksBar();
            UpdateBookmarkDoorIcon();
            if (openDrawer) ShowBookmarksDrawer();
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
                    else if (u.StartsWith("search://history", StringComparison.OrdinalIgnoreCase))
                    {
                        OpenOrSelectHistoryTab();
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
            SetDrawerVisible(true);

            // Quick Actions row
            var quickPanel = new WrapPanel { Margin = new Thickness(0, 0, 0, 14) };
            quickPanel.Children.Add(CreatePillActionButton("📖 Reading Mode (Ctrl+Shift+R)", async () => { SetDrawerVisible(false); await ToggleReadingModeAsync(); }));
            quickPanel.Children.Add(CreatePillActionButton("🪄 Hide Element (Ctrl+Shift+H)", async () => { SetDrawerVisible(false); await ToggleVeilPickingAsync(); }));
            quickPanel.Children.Add(CreatePillActionButton("🌙 Dusk Site Dark Mode (Ctrl+Shift+D)", async () =>
            {
                if (_activeTab != null)
                {
                    PageFeatures.ToggleDuskForSite(Address.DisplayHost(_activeTab.Url));
                    await _activeTab.ApplySiteStyleSheetsAsync();
                }
                SetDrawerVisible(false);
            }));
            quickPanel.Children.Add(CreatePillActionButton("🌐 Translate Page", async () => { SetDrawerVisible(false); await ToggleTranslatePageAsync(); }));
            quickPanel.Children.Add(CreatePillActionButton("🎨 Custom Site CSS", () => ShowSiteStylesDrawer()));
            quickPanel.Children.Add(CreatePillActionButton("📺 Float Video PiP (Ctrl+Shift+P)", async () => { SetDrawerVisible(false); await TogglePictureInPictureAsync(); }));
            quickPanel.Children.Add(CreatePillActionButton("🔗 Copy Clean Link (Ctrl+Shift+C)", () =>
            {
                if (_activeTab != null) SafeSetClipboardText(CleanLink.Clean(_activeTab.Url));
                SetDrawerVisible(false);
            }));
            quickPanel.Children.Add(CreatePillActionButton("◫ Split View (Alt+Ctrl+N)", () => { SetDrawerVisible(false); ToggleSplitView(); }));
            quickPanel.Children.Add(CreatePillActionButton("🕶 Private Tab (Ctrl+Shift+N)", () => { SetDrawerVisible(false); CreateAndSelectTab("about:blank", true, true); }));
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
            SetDrawerVisible(true);

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
            SetDrawerVisible(true);

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
            SetDrawerVisible(true);

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
                    SetDrawerVisible(false);
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
            SetDrawerVisible(true);

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
                    SetDrawerVisible(false);
                    if (_activeTab != null) _activeTab.Navigate(captured.Url);
                });
            }
        }

        private void ShowHiddenElementsDrawer()
        {
            string host = _activeTab != null ? Address.DisplayHost(_activeTab.Url) : "";
            _drawerTitle.Text = "Curtain — Hidden Clutter" + (string.IsNullOrEmpty(host) ? "" : " on " + host);
            _drawerContent.Children.Clear();
            SetDrawerVisible(true);

            var topBar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
            topBar.Children.Add(CreatePillActionButton("🪄 Point & Hide Element on Page (Ctrl+Shift+H)", async () =>
            {
                SetDrawerVisible(false);
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
            SetDrawerVisible(true);

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
                        SafeSetClipboardText(dec);
                        await FillSavedLoginAsync(cap);
                        SetDrawerVisible(false);
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
            SetDrawerVisible(true);

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
                SetDrawerVisible(false);
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
                    "Remove", async () =>
                    {
                        await UninstallExtensionAsync(cap, _activeTab);
                        ShowExtensionsDrawer();
                    });
            }
        }

        private void ShowDownloadsDrawer()
        {
            _drawerTitle.Text = "Downloads (Ctrl+Shift+J)";
            _drawerContent.Children.Clear();
            SetDrawerVisible(true);

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
            b.MouseLeftButtonUp += (s, e) =>
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
            b.MouseLeftButtonUp += (s, e) =>
            {
                e.Handled = true;
                onClick();
            };
            return b;
        }

        private Border CreateCaptionButton(string pathData, string tooltip, bool isClose, Action onClick)
        {
            var b = new Border
            {
                Width = 28,
                Height = 28,
                CornerRadius = new CornerRadius(8),
                Background = Brushes.Transparent,
                Margin = new Thickness(2, 0, 0, 0),
                Cursor = Cursors.Hand,
                ToolTip = tooltip,
                Child = VectorIcons.Draw(pathData, Palette.FrameMuted, 12, 1.45)
            };
            WindowChrome.SetIsHitTestVisibleInChrome(b, true);
            b.MouseEnter += (s, e) =>
            {
                b.Background = isClose ? Palette.CloseHoverBg : Palette.FrameHover;
                string curPath = pathData;
                if (b == _maxBtn && WindowState == WindowState.Maximized) curPath = VectorIcons.WinRestorePath;
                b.Child = VectorIcons.Draw(curPath, isClose ? Palette.CloseHoverFg : Palette.InkHoverText, 12, 1.45);
            };
            b.MouseLeave += (s, e) =>
            {
                b.Background = Brushes.Transparent;
                string curPath = pathData;
                if (b == _maxBtn && WindowState == WindowState.Maximized) curPath = VectorIcons.WinRestorePath;
                b.Child = VectorIcons.Draw(curPath, Palette.FrameMuted, 12, 1.45);
            };
            b.MouseLeftButtonDown += (s, e) => { e.Handled = true; };
            b.MouseLeftButtonUp += (s, e) =>
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
                Margin = new Thickness(0, 0, 4, 0),
                Cursor = Cursors.Hand,
                ToolTip = tooltip,
                Child = VectorIcons.Draw(pathData, Palette.FrameMuted, 11, 1.45)
            };
            WindowChrome.SetIsHitTestVisibleInChrome(b, true);
            b.MouseEnter += (s, e) => b.Background = Palette.FrameHover;
            b.MouseLeave += (s, e) => b.Background = Brushes.Transparent;
            b.MouseLeftButtonDown += (s, e) => { e.Handled = true; };
            b.MouseLeftButtonUp += (s, e) =>
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
            b.MouseLeftButtonUp += (s, e) =>
            {
                e.Handled = true;
                onClick();
            };
            return b;
        }
    }
}
