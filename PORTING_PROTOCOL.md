# Search for Windows — 1:1 Official `driceroland/Search` Porting Protocol

This document is our living engineering protocol for bringing **Search for Windows** (`C:\Users\jacqu\SearchBrowser`) to the closest possible 1:1 visual and functional parity with official macOS **Search** (`https://github.com/driceroland/Search`), **without ever breaking features that already work**.

---

## 1. Golden Working Baseline (`v0.0018` — NEVER REGRESS)

Every release MUST keep 100% of these working:

1. **Closing the last tab** never crashes (detaches pane host, clears `_activeTab`, calls `PutToSleep()`, opens replacement blank tab).
2. **Typing `1`–`9` in web forms / inputs** works cleanly (`GetAsyncKeyState`, never `GetKeyState`, in `LowLevelKeyboardCallback`).
3. **Instant keyboard shortcuts** even when WebView2 has focus (`Ctrl+L`, `Ctrl+T`, `Ctrl+W`, `Ctrl+Shift+T`, `Ctrl+H`, `Ctrl+,`, `Ctrl+S`, `Ctrl+Tab` MRU switcher with `Ctrl`-up commit, `Ctrl+1`–`9`, `Alt+1`–`9`).
4. **Google Search, OAuth "Sign in with Google" popups, and X/Twitter login** work cleanly (OAuth/sized popups preserve `window.opener`; non-destructive bot-safe anti-fingerprinting; Balanced Tracking Prevention).
5. **Shared isolated Private Session** across all Private Tabs (wiped on last private tab close + app close + stale startup cleanup).
6. **Instant 0ms Dark/Light switching** via `:root` CSS vars without half-inverted flashes.
7. **Lean RAM & CPU profile** (`Search.exe` ~12–25 MB, capped WebView2 renderers, instant background `MemoryUsageTargetLevel.Low`, 30s `TrySuspendAsync()`, 32px cached favicons, coalesced sidebar repaints).
8. **Built-in vector brand icons** (YouTube, YT Music, Spotify, Google, Gmail, Calendar, Docs, GitHub) + 64px PNG fallback for all other sites.
9. **True monitor-wide fullscreen** for YouTube/HTML5 video covering wacOS top bar & dock.
10. **Smooth zero-wrap sidebar resizing** & **instant non-blocking clipboard copy** (`SafeSetClipboardText(..., false)`).
11. **Windhawk Dynamic Island clearance**: top frame strip stays quiet & centered-clear (default 36px `"Notch"` mode) so the floating Dynamic Island never blocks website headers/search bars.

---

## 2. macOS (`driceroland/Search`) → Windows (WPF + WebView2) Translation Rules

| Official macOS (`SwiftUI / AppKit / WebKit`) | Windows Equivalent (`WPF / Win32 / WebView2`) |
|---|---|
| `Palette` + `Greys` (`Design.swift`) | `Palette.cs` frozen `SolidColorBrush`es (exact RGB + translucent alpha overlays so Space tints blend naturally) |
| `Lights.swift` (traffic lights in top-left of sidebar) | Custom 12×12px WPF vector traffic lights in top-left of quiet 36px top frame strip (with focused/unfocused resting colors + hover `× − +` glyphs); toggleable back to Windows `— □ ✕` |
| `CardEdge` (`8px` gap, `10px` radius, `0.08` hairline) | `_pageCardBorder` + `ApplyRoundedCornersToHost` (`HRGN` clip on WebView2 `HwndHost`) |
| `EdgeGrip` (4×28→40px capsule on resize/split edge) | WPF `Border` capsule inside `_sidebarResizeGrip` / split divider, animating opacity/height on hover & drag |
| `WKWebView` + `WKContentRuleList` | `WebView2` (`1.0.4370.26`) + `WebResourceRequested` `Shield.cs` + `AddScriptToExecuteOnDocumentCreatedAsync` |
| `NSPanel` floating over WebView2 (Site Card, Peek, Popovers) | WPF `Popup` (`AllowsTransparency = true`) so it floats cleanly ABOVE WebView2's `HwndHost` (solves WPF airspace!) |
| `Keychain` / Touch ID | Windows DPAPI (`ProtectedData`) + Windows Hello (`UserConsentVerifier`, fail-closed) |
| `DevToolsFull` (Swift custom inspector) | Chromium built-in F12 DevTools (kept — faster & full-featured) |
| `ICloudSync` / `Handoff` / `ApplePasswords` | Skipped (Apple-only); local folder sync + local DPAPI vault used instead |

---

## 3. Strict Change & Verification Gate (Every Release)

1. **One focused vertical slice per release** (`v0.0019`, `v0.0020`, ...). Never mix unrelated big refactors in one commit.
2. **C# 5 syntax ONLY** (`csc.exe` .NET Framework 4.x — no `?.`, `$"..."`, `out var`, `is Type var`, `when`).
3. **Unique `edit_file` targets** — always verify context around every edit (never accidentally duplicate calls or create recursion).
4. **Always bump version** in all 3 places: `src/MainWindow.cs` (`currentVersion`), `src/InternalPagesHtml.cs` (About string), `InstallerSetup.cs` (subtitle).
5. **Run `smoke_test.ps1`** before committing/deploying:
   - Compiles `Search.exe` + `Search-Setup.exe` with 0 errors.
   - Verifies version strings match across all 3 files.
   - Launches `Search.exe`, confirms it stays running with 0 crash/exception for 4s, verifies lean memory footprint, then cleanly terminates test instance.
6. **Deploy + Tag + GitHub Release**: copy `Search.exe` to `%LOCALAPPDATA%\Programs\Search\Search.exe`, copy `Search-Setup.exe` to Desktop, commit & push `main`, tag `vX.XXXX`, publish GitHub Release with `Search-Setup.exe`.

---

## 4. Phased 1:1 Parity Master Plan

- **Phase 1 (`v0.0019`) — Official Visual Shell + Dynamic Island Notch Strip**:
  - 36px quiet notch-friendly top strip (toggleable: Notch 36px / Compact 28px / Standard 48px)
  - macOS traffic lights on top-left next to `< > ↻` (with unfocused resting grey + hover glyphs; toggleable back to Windows Inline/Classic)
  - Exact official `SideFoot` layout (Space, Extensions, Bookmarks, Downloads grouped left; quiet Settings icon right)
  - Official `EdgeGrip` pill on sidebar resize border
  - Pure official Blank New Tab page (remove Oriel floating bottom-right gear box)
- **Phase 2 (`v0.0020`) — Active Tab Inline Edit + Full Official Site Card**:
  - Clean "at rest" site domain display on active tab (`AtRest.swift` style: dim subdomain, ink registrable domain, amber HTTP warning)
  - Clicking active tab edits address inline + hangs official **Site Card** (`Popup`) directly underneath: Secure/Not Secure badge, Copy URL, Print, Zoom `− 100% +`, Ad Blocker toggle, Keep Site Awake, Mute/Sound, Translate, Hidden Elements, Edit Site Style, Clear Site Data
  - Optional official `AddressLine` right at top of sidebar column (above pins/tabs) with the `slider.horizontal.3` Site Card button
- **Phase 3 (`v0.0021`) — Command Bar, Site Search & Floating `Ctrl+T`**:
  - Official `AddressCommands` (`settings`, `history`, `downloads`, `reading mode`, `hide elements`, `new private tab`, `split view`, etc.)
  - Site Search (`SiteSearch.swift`: type site name + `Tab` chip) + custom Keyword shortcuts (`yt`, `gh`, `w`) + `Shift+Enter` secondary search engine
  - Optional Arc-style floating `Ctrl+T` popup with open tab switching & recent history
- **Phase 4 (`v0.0022`) — Sidebar Depth: 3-Tier Pins, Pin Folders, Previews, Multi-Select & Media Player**:
  - Official multi-row Media Player cards at foot of sidebar (hairline card, play/pause, dismiss `×`, click to jump across Spaces)
  - Pin Hover Previews + Unread Count badges (`(3)`) + Pin Folders (3-tier Arc sidebar) + Multi-tab selection (`Ctrl`/`Shift`-click) + Auto-Close old tabs + Tab Give press feel
- **Phase 5 (`v0.0023`) — Settings 1:1 Polish + Live "Find a Setting" (`Ctrl+F`) + "Set Up Like Arc"**:
  - Live `Ctrl+F` search bar in Settings rail with per-section match counts, row jump highlight, and 1-click "Set Up Like Arc" with Undo
- **Phase 6 (`v0.0024`) — Power Browsing Tools**:
  - Resizable Website Side Panel (`SidePanel.swift`), Page Screenshot Capture (`Snap.swift`), Floating Downloads card (`DownloadsFloat.swift`), Split View floating controls + edge drag-to-split, Dusk & Edit Site Style polish, DNS Auto-Retry & Little Window polish.
