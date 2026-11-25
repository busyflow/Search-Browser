# Security Policy

## Security Architecture

Search for Windows is built on the native **Microsoft Edge WebView2 (Chromium)** runtime and applies defense-in-depth protections across the browser shell:

- **Chromium Multi-Process Sandbox & SmartScreen:** All web content runs inside Microsoft's up-to-date, sandboxed WebView2 Chromium engine with **Microsoft Defender SmartScreen** enabled for phishing and malicious download protection.
- **Brave-Style Anti-Fingerprinting ("Farbling"):** Randomizes Canvas 2D readbacks, masks WebGL GPU vendor/renderer strings, adds imperceptible deterministic jitter to AudioContext, normalizes `hardwareConcurrency`/`deviceMemory`, blocks the Battery Status API, and blocks tracker `sendBeacon` calls (per-session deterministic, randomized every restart and per Private Tab).
- **Strict Engine-Level Tracking Prevention & WebRTC Protection:** Enables Chromium's `Strict` Tracking Prevention (Disconnect.me list + third-party storage partitioning), disables Google's Privacy Sandbox Topics/Attribution APIs and background networking, blocks WebRTC non-proxied UDP IP leaks, trims cross-site `Referer` headers to origin-only, blocks `<a ping>` click trackers, sends `Sec-GPC: 1` & `DNT: 1`, upgrades HTTP → HTTPS by default, and strips 55+ URL tracking parameters via Clean Link.
- **Origin-Gated IPC Bridge:** Privileged browser actions (`settingsAction`, `history*`, `navigate`, `cwsInstall`, `vaultFillUser`, `loginSubmitted`) are strictly validated against the active tab's URL/origin so untrusted websites cannot modify browser settings, clear history, spoof credential saves, or trigger silent extension installs.
- **Encrypted Password Vault (Windows DPAPI):** Saved passwords are encrypted at rest using Windows Data Protection API (`ProtectedData` with `DataProtectionScope.CurrentUser`) and optional Windows Hello / Windows Credential verification (fail-closed on error). Password autofill and save prompts are restricted to `https://` and `localhost` origins, and the host is always derived from the actual tab URL.
- **Hardened Extension & Installer Archives:** Chrome Web Store (`.crx`) installations require explicit user confirmation, enforce a 50 MB size cap, validate CRX header bounds, and guard every archive entry against Zip Slip path traversal (`..`). The Windows installer (`Search-Setup.exe`) also validates all archive entries before extraction.
- **Locked Local Automation Pipe:** The optional local named pipe (`search-bench`) is **disabled by default** and restricted via Windows `PipeSecurity` ACL to the current Windows user's SID only.
- **Private Browsing Isolation:** Private tabs run in a dedicated, temporary WebView2 user-data directory that is deleted when the private tab closes.
- **Verified HTTPS Auto-Updater:** In-app updates fetch metadata over TLS 1.2 exclusively from `https://api.github.com/repos/busyflow/Search-Browser/releases/latest` and only download `Search-Setup.exe` from official GitHub release URLs.

## Reporting a Vulnerability

If you discover a security vulnerability in Search for Windows, please report it responsibly:

1. Open a private security advisory on GitHub (**Security → Advisories → Report a vulnerability**), or open an issue without publishing a live exploit.
2. Include steps to reproduce, the affected version (`Settings → About`), and any relevant logs.

We aim to acknowledge reports promptly and ship fixes in the next release.
