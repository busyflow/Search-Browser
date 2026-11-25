# Search for Windows

A native, ultra-lightweight Windows browser inspired by [driceroland/Search](https://github.com/driceroland/Search) and [Oriel](https://github.com/carpaaaaa/oriel) — powered by the native Windows **Microsoft Edge WebView2** engine (no bundled 300 MB Chromium copy).

## Download & Install

[![Download Search-Setup.exe](https://img.shields.io/badge/Download-Search--Setup.exe_(Latest)-2ea44f?style=for-the-badge&logo=windows)](https://github.com/busyflow/Search-Browser/releases/latest/download/Search-Setup.exe)

- **Direct Installer (Recommended):** [Download `Search-Setup.exe` (Latest Release)](https://github.com/busyflow/Search-Browser/releases/latest/download/Search-Setup.exe)
- **All Versions & Changelogs:** [Browse GitHub Releases](https://github.com/busyflow/Search-Browser/releases)

### How to Install
1. Download **`Search-Setup.exe`** from the button above (or the **Releases** tab on the right).
2. Double-click `Search-Setup.exe` and click **Install**.
   > **Note on Windows SmartScreen:** Because this is a free, open-source app without a paid Authenticode code-signing certificate yet, Windows may show *"Windows protected your PC"* on first run. Click **More info → Run anyway** (or build directly from source below using `./build.ps1`).
3. **In-App Updates:** Anytime a new version is released, open **Settings → About → Check for updates** inside Search to download and install the latest version automatically.

---

## Highlights

- **Tiny Footprint:** `Search.exe` is under **~250 KB** (uses the system's WebView2 runtime).
- **Zero-Cost Restored Tabs & Tab Sleep:** Restored tabs stay lightweight until clicked, and background tabs automatically sleep to save RAM.
- **Spaces, Pinned Tabs & Split View:** Organize work into Spaces (`Alt+1`–`Alt+9`), pin favorite sites (middle-click or close a pin to darken & sleep it without removing it), and view two pages side-by-side in Split View (`Alt+Ctrl+N`).
- **Live UI Themes & Backgrounds:** Customize both the browser chrome and New Tab wall with Wall Paintings, Stickers, Solid Colors, Gradients, or Custom Images in **Settings → Themes**.
- **Built-in Shield & Reading Tools:** Ad/tracker blocking, YouTube ad skipper, Clean Link URL stripping, Reading Mode, Dusk dark mode, Curtain element hider, and Picture-in-Picture.
- **Security First:** Chromium WebView2 sandbox + SmartScreen protection, Windows DPAPI encrypted Password Vault with optional Windows Hello verification, origin-gated IPC, Zip Slip protection, and isolated Private Tabs. See [SECURITY.md](SECURITY.md) for full details.

---

## Keyboard Shortcuts

| Action | Windows Shortcut | macOS Original |
|---|---|---|
| Open floating Address / Search field | `Ctrl + L` | `⌘L` |
| Quick Tab Switcher by name/URL | `Ctrl + K` | `⌘K` |
| New Tab | `Ctrl + T` | `⌘T` |
| New Private Tab (isolated session) | `Ctrl + Shift + N` | `⇧⌘N` |
| Close Tab / Sleep Pinned Tab | `Ctrl + W` or Middle-Click | `⌘W` |
| Reopen Closed Tab | `Ctrl + Shift + T` | `⇧⌘T` |
| Jump to Tab 1–9 | `Ctrl + 1` .. `Ctrl + 9` | `⌘1`–`⌘9` |
| Switch Space 1–9 | `Alt + 1` .. `Alt + 9` | `⌥1`–`⌥9` |
| Next / Previous Tab | `Ctrl + Tab` / `Ctrl + Shift + Tab` | `⌃Tab` / `⌃⇧Tab` |
| Back / Forward | `Ctrl + [` / `Ctrl + ]` | `⌘[` / `⌘]` |
| Toggle Tabs Left Sidebar vs. Top Bar | `Ctrl + Shift + S` | `⇧⌘S` |
| Fold / Collapse Sidebar | `Ctrl + S` | `⌘S` |
| Toggle Split View (Two pages side by side) | `Alt + Ctrl + N` | `⌥⌘N` |
| Reading Mode (Distraction-free article) | `Ctrl + Shift + R` | `⇧⌘R` |
| Curtain — Point & Hide Element Forever | `Ctrl + Shift + H` | `⇧⌘H` |
| Inspect / Restore Hidden Elements | `Ctrl + Shift + U` | `⇧⌘U` |
| Float Video (Picture-in-Picture) | `Ctrl + Shift + P` | `⇧⌘P` |
| Bookmark Current Page | `Ctrl + Shift + B` | `⇧⌘B` |
| History Panel | `Ctrl + Y` | `⌘Y` |
| Downloads Panel | `Ctrl + Shift + J` | `⇧⌘J` |
| Passwords Vault (Windows DPAPI) | `Alt + Ctrl + L` | `⌥⌘L` |
| Find on Page | `Ctrl + F` | `⌘F` |
| Settings | `Ctrl + ,` | `⌘,` |

---

## Building from Source

```powershell
git clone https://github.com/busyflow/Search-Browser.git
cd Search-Browser
.\build.ps1
```

No external SDK download is required — `build.ps1` compiles directly with the native Windows .NET x64 compiler (`csc.exe`) and outputs both `Search.exe` and `Search-Setup.exe`.

## License & Attribution

Released under the [MIT License](LICENSE). Provided "as is", without warranty of any kind.
Inspired by [driceroland/Search](https://github.com/driceroland/Search) and [carpaaaaa/oriel](https://github.com/carpaaaaa/oriel).
