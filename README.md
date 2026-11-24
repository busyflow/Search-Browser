# Search for Windows

A native, ultra-lightweight Windows port of [driceroland/Search](https://github.com/driceroland/Search) — built around the Windows OS native **WebView2** engine (the same approach Zephium uses on Windows, without bundling a 300 MB second copy of Chromium).

- **Tiny Footprint:** `Search.exe` is **~174 KB** (~1.1 MB total including the WebView2 interop wrappers).
- **Zero-Cost Restored Tabs:** Restored tabs from your last session do not spin up a browser process until you click them, and inactive background tabs automatically go to sleep to free RAM.

## Keyboard Shortcuts (Windows Parity with macOS Search)

| Action | Windows Shortcut | macOS Original |
|---|---|---|
| Open floating Address / Search field | `Ctrl + L` | `⌘L` |
| Quick Tab Switcher by name/URL | `Ctrl + K` | `⌘K` |
| New Tab | `Ctrl + T` | `⌘T` |
| New Private Tab (isolated session) | `Ctrl + Shift + N` | `⇧⌘N` |
| Close Tab | `Ctrl + W` | `⌘W` |
| Reopen Closed Tab | `Ctrl + Shift + T` | `⇧⌘T` |
| Jump to Tab 1–9 | `Ctrl + 1` .. `Ctrl + 9` | `⌘1`–`⌘9` |
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
| Settings & Panels Menu | `Ctrl + ,` | `⌘,` |

## Building from Source

Run `./build.ps1` in PowerShell. No external SDK download is required — it compiles directly with the native Windows .NET x64 compiler (`csc.exe`).
