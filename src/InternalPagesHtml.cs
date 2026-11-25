using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SearchBrowser
{
    // Builds the 1:1 internal pages for Search (New Tab empty page with centered field & bottom-right gear,
    // and the full-page Settings tab `search://settings` matching Oriel/Search's Settings.tsx & settings.css to the pixel!)
    public static class InternalPagesHtml
    {
        public static string BuildNewTabHtml()
        {
            bool dark = Palette.IsDark;
            string ground = dark ? "#1C1C1C" : "#FFFFFF";
            string ink = dark ? "#EDEDED" : "#171717";
            string inkRgb = dark ? "237,237,237" : "23,23,23";
            string hairline = dark ? "#333333" : "#E8E8E8";
            string muted = dark ? "#949494" : "#8C8C8C";
            string hover = dark ? "rgba(255,255,255,0.045)" : "rgba(0,0,0,0.035)";
            string wash = dark ? "#2D2D2D" : "#EFEFEF";

            string theme = Store.Prefs != null ? Store.Prefs.BlankWallTheme : "Clean";
            string wallMode = Store.Prefs != null ? Store.Prefs.BlankWallMode : "Off";
            string customVal = Store.Prefs != null ? Store.Prefs.BlankWallCustomValue : "";
            string wallStyle = "background:" + ground + ";";
            if (wallMode == "Wall painting")
            {
                if (theme == "Dusk Gradient")
                    wallStyle = dark ? "background:radial-gradient(circle at 50% 35%, #252836 0%, #15161e 100%);" : "background:radial-gradient(circle at 50% 35%, #f3f5ff 0%, #ffffff 100%);";
                else if (theme == "Warm Paper")
                    wallStyle = dark ? "background:radial-gradient(circle at 50% 40%, #26221e 0%, #181614 100%);" : "background:radial-gradient(circle at 50% 40%, #faf6f0 0%, #f5efe6 100%);";
                else if (theme == "Forest Mist")
                    wallStyle = dark ? "background:radial-gradient(circle at 50% 40%, #1a2822 0%, #121815 100%);" : "background:radial-gradient(circle at 50% 40%, #f0f9f4 0%, #ffffff 100%);";
                else
                    wallStyle = dark ? "background:radial-gradient(circle at 50% 35%, #252836 0%, #15161e 100%);" : "background:radial-gradient(circle at 50% 35%, #f3f5ff 0%, #ffffff 100%);";
            }
            else if (wallMode == "Solid colour" && !string.IsNullOrEmpty(customVal))
            {
                wallStyle = "background:" + EscHtml(customVal) + ";";
            }
            else if (wallMode == "Gradient")
            {
                string g = !string.IsNullOrEmpty(customVal) ? customVal : (dark ? "linear-gradient(135deg, #1a1b26 0%, #24283b 50%, #1f2335 100%)" : "linear-gradient(135deg, #eef2ff 0%, #f8fafc 50%, #f0fdf4 100%)");
                wallStyle = "background:" + g + ";";
            }
            else if (wallMode == "Stickers")
            {
                wallStyle = dark
                    ? "background:radial-gradient(rgba(255,255,255,0.06) 1px, transparent 1px) 0 0 / 24px 24px, " + ground + ";"
                    : "background:radial-gradient(rgba(0,0,0,0.06) 1px, transparent 1px) 0 0 / 24px 24px, " + ground + ";";
            }
            else if (wallMode == "Image" && !string.IsNullOrEmpty(customVal) && System.IO.File.Exists(customVal))
            {
                string fileUri = new Uri(customVal).AbsoluteUri;
                wallStyle = "background:url('" + EscHtml(fileUri) + "') center/cover no-repeat;";
            }

            // Build history/bookmarks JSON for instant inline suggestions right under the centered New Tab field
            var suggestions = new List<string>();
            foreach (var b in Store.Bookmarks.Take(6))
            {
                suggestions.Add("{\"kind\":\"bookmark\",\"title\":\"" + Esc(b.Title) + "\",\"url\":\"" + Esc(b.Url) + "\",\"host\":\"" + Esc(Address.DisplayHost(b.Url)) + "\"}");
            }
            foreach (var h in Store.History.Take(12))
            {
                suggestions.Add("{\"kind\":\"history\",\"title\":\"" + Esc(h.Title) + "\",\"url\":\"" + Esc(h.Url) + "\",\"host\":\"" + Esc(Address.DisplayHost(h.Url)) + "\"}");
            }
            string itemsJson = "[" + string.Join(",", suggestions) + "]";

            return @"<!DOCTYPE html>
<html>
<head>
<meta charset='utf-8'>
<title>New Tab</title>
<style>
  :root {
    --ground: " + ground + @";
    --ink: " + ink + @";
    --ink-rgb: " + inkRgb + @";
    --hairline: " + hairline + @";
    --muted: " + muted + @";
    --hover: " + hover + @";
    --wash: " + wash + @";
  }
  * { box-sizing: border-box; margin: 0; padding: 0; }
  html, body {
    height: 100%;
    overflow: hidden;
    " + wallStyle + @"
    color: var(--ink);
    font-family: 'Segoe UI Variable Text', 'Plus Jakarta Sans', 'Segoe UI', system-ui, sans-serif;
    -webkit-font-smoothing: antialiased;
    user-select: none;
  }
  .empty-page {
    position: absolute;
    inset: 0;
    display: grid;
    place-items: center;
    padding-bottom: 60px;
  }
  .field-wrap {
    position: relative;
    width: min(560px, calc(100% - 28px));
  }
  .field {
    position: relative;
    height: 50px;
    display: flex;
    align-items: center;
    gap: 8px;
    padding: 0 22px;
    border-radius: 14px;
    background: var(--ground);
    box-shadow: inset 0 0 0 1px var(--hairline), 0 8px 48px rgba(0,0,0,0.06);
    transition: box-shadow 140ms cubic-bezier(0,0,0.58,1);
  }
  .field-breath {
    position: absolute;
    inset: 0;
    border-radius: 26px;
    z-index: -1;
    pointer-events: none;
    box-shadow: 0 0 52px rgba(var(--ink-rgb), 0.055);
    animation: breath 2.6s ease-in-out infinite alternate;
  }
  @keyframes breath {
    from { transform: scale(0.97); opacity: 0.65; }
    to { transform: scale(1.03); opacity: 1; }
  }
  .field input {
    flex: 1;
    min-width: 0;
    height: 22px;
    border: none;
    outline: none;
    background: transparent;
    font-family: inherit;
    font-size: 15.5px;
    line-height: 22px;
    color: var(--ink);
    caret-color: var(--ink);
    user-select: text;
  }
  .field input::placeholder {
    color: rgba(var(--ink-rgb), 0.30);
    opacity: 1;
  }
  .field-list {
    position: absolute;
    left: 0;
    right: 0;
    top: 58px;
    padding: 6px;
    border-radius: 14px;
    background: var(--ground);
    box-shadow: inset 0 0 0 1px var(--hairline), 0 6px 40px rgba(0,0,0,0.12);
    display: none;
    z-index: 10;
  }
  .field-row {
    display: flex;
    align-items: center;
    gap: 10px;
    padding: 9px 12px;
    border-radius: 9px;
    cursor: pointer;
  }
  .field-row:hover, .field-row.picked {
    background: var(--wash);
  }
  .field-key {
    font-size: 13px;
    color: var(--ink);
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
  }
  .field-title {
    font-size: 12px;
    color: var(--muted);
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
    margin-left: auto;
  }
  /* Bottom-right Settings gear door (Blank.tsx SettingsDoor: 26x26, radius 8, 14px from corner) */
  .blank-door {
    position: fixed;
    right: 14px;
    bottom: 14px;
    width: 26px;
    height: 26px;
    border-radius: 8px;
    border: none;
    background: transparent;
    color: var(--muted);
    display: grid;
    place-items: center;
    cursor: pointer;
    transition: background-color 140ms cubic-bezier(0,0,0.58,1), color 140ms cubic-bezier(0,0,0.58,1);
  }
  .blank-door:hover {
    background: var(--hover);
    color: rgba(var(--ink-rgb), 0.75);
  }
</style>
</head>
<body>
  <div class='empty-page'>
    <div class='field-wrap'>
      <div class='field-breath'></div>
      <div class='field'>
        <input id='addr' type='text' spellcheck='false' autocomplete='off' placeholder='Enter a web address' autofocus />
      </div>
      <div id='list' class='field-list'></div>
    </div>
  </div>

  <button class='blank-door' title='Settings (Ctrl+,)' onclick=""send({type:'openSettings'})"">
    <svg width='12.1' height='12.1' viewBox='0 0 16 16' fill='none' stroke='currentColor' stroke-width='1.5' stroke-linecap='round' stroke-linejoin='round'>
      <path d='M6.81 3.56L7.02 1.56L8.98 1.56L9.19 3.56A4.6 4.6 0 0 1 11.4 4.84L13.24 4.02L14.22 5.72L12.59 6.91A4.6 4.6 0 0 1 12.59 9.09L14.22 10.28L13.24 11.98L11.4 11.16A4.6 4.6 0 0 1 9.19 12.44L8.98 14.44L7.02 14.44L6.81 12.44A4.6 4.6 0 0 1 4.6 11.16L2.76 11.98L1.78 10.28L3.41 9.09A4.6 4.6 0 0 1 3.41 6.91L1.78 5.72L2.76 4.02L4.6 4.84A4.6 4.6 0 0 1 6.81 3.56Z'/>
      <circle cx='8' cy='8' r='1.9'/>
    </svg>
  </button>

  <script>
    var items = " + itemsJson + @";
    var input = document.getElementById('addr');
    var list = document.getElementById('list');
    var pickedIdx = 0;
    var currentOffers = [];

    function send(msg) {
      if (window.chrome && window.chrome.webview) {
        window.chrome.webview.postMessage(JSON.stringify(msg));
      }
    }

    window.addEventListener('load', function() {
      input.focus();
    });
    document.body.addEventListener('click', function(e) {
      if (e.target === document.body || e.target.classList.contains('empty-page')) {
        input.focus();
      }
    });

    function renderOffers() {
      var q = (input.value || '').trim();
      if (!q) {
        list.style.display = 'none';
        currentOffers = [];
        return;
      }
      currentOffers = [];
      currentOffers.push({ label: q, sub: q.indexOf('.') > 0 && q.indexOf(' ') < 0 ? 'Open Address' : 'Search Web', value: q });
      for (var i = 0; i < items.length; i++) {
        var it = items[i];
        if (it.title.toLowerCase().indexOf(q.toLowerCase()) >= 0 || it.url.toLowerCase().indexOf(q.toLowerCase()) >= 0) {
          currentOffers.push({ label: it.host || it.url, sub: it.title, value: it.url });
          if (currentOffers.length >= 7) break;
        }
      }
      pickedIdx = 0;
      list.innerHTML = '';
      currentOffers.forEach(function(off, idx) {
        var row = document.createElement('div');
        row.className = 'field-row' + (idx === pickedIdx ? ' picked' : '');
        var k = document.createElement('span');
        k.className = 'field-key';
        k.textContent = off.label;
        var t = document.createElement('span');
        t.className = 'field-title';
        t.textContent = off.sub;
        row.appendChild(k);
        row.appendChild(t);
        row.onmouseenter = function() {
          pickedIdx = idx;
          updateHighlight();
        };
        row.onmousedown = function(ev) {
          ev.preventDefault();
          send({ type: 'navigate', text: off.value });
        };
        list.appendChild(row);
      });
      list.style.display = currentOffers.length > 0 ? 'block' : 'none';
    }

    function updateHighlight() {
      var rows = list.querySelectorAll('.field-row');
      for (var i = 0; i < rows.length; i++) {
        rows[i].className = 'field-row' + (i === pickedIdx ? ' picked' : '');
      }
    }

    input.addEventListener('input', renderOffers);
    input.addEventListener('keydown', function(e) {
      if (e.key === 'ArrowDown' && currentOffers.length > 0) {
        e.preventDefault();
        pickedIdx = (pickedIdx + 1) % currentOffers.length;
        updateHighlight();
      } else if (e.key === 'ArrowUp' && currentOffers.length > 0) {
        e.preventDefault();
        pickedIdx = (pickedIdx - 1 + currentOffers.length) % currentOffers.length;
        updateHighlight();
      } else if (e.key === 'Enter') {
        e.preventDefault();
        var chosen = (currentOffers.length > 0 && pickedIdx >= 0) ? currentOffers[pickedIdx].value : input.value;
        if (chosen && chosen.trim()) {
          send({ type: 'navigate', text: chosen.trim() });
        }
      }
    });
  </script>
</body>
</html>";
        }

        // Builds the 1:1 full-page Settings tab matching Oriel's screenshots 2 & 3 (Settings.tsx & settings.css)
        private static string Sw(string prop, bool on)
        {
            return "<button class='st-sw " + (on ? "on" : "") + "' onclick=\"togglePref('" + Esc(prop) + "')\"><span class='st-sw-knob'></span></button>";
        }

        private static string Seg(string prop, string current, params string[] opts)
        {
            var sb = new StringBuilder("<div class='st-seg'>");
            foreach (var o in opts)
            {
                bool active = string.Equals(o, current, StringComparison.OrdinalIgnoreCase);
                sb.Append("<button class='st-seg-btn " + (active ? "on" : "") + "' onclick=\"setPref('" + Esc(prop) + "','" + Esc(o) + "')\">" + EscHtml(o) + "</button>");
            }
            sb.Append("</div>");
            return sb.ToString();
        }

        private static string Sel(string prop, string current, params string[] opts)
        {
            var sb = new StringBuilder("<select class='st-sel' onchange=\"setPref('" + Esc(prop) + "', this.value)\">");
            foreach (var o in opts)
            {
                bool active = string.Equals(o, current, StringComparison.OrdinalIgnoreCase);
                sb.Append("<option value='" + EscHtml(o) + "'" + (active ? " selected" : "") + ">" + EscHtml(o) + "</option>");
            }
            sb.Append("</select>");
            return sb.ToString();
        }

        private static string Chk(string prop, string label, bool on)
        {
            return "<label class='st-chk' onclick=\"togglePref('" + Esc(prop) + "')\"><span class='st-chk-box " + (on ? "on" : "") + "'>" + (on ? "✓" : "") + "</span><span>" + EscHtml(label) + "</span></label>";
        }

        private static string Info(string tip)
        {
            return "<span class='st-info' title='" + EscHtml(tip) + "'>i</span>";
        }

        public static string BuildSettingsTabHtml(string activeSection = "general")
        {
            var p = Store.Prefs;
            bool dark = Palette.IsDark;
            string ground = dark ? "#1C1C1C" : "#FFFFFF";
            string ink = dark ? "#EDEDED" : "#171717";
            string inkRgb = dark ? "237 237 237" : "23 23 23";
            string muted = dark ? "#949494" : "#8C8C8C";
            string faint = dark ? "#525252" : "#D4D4D4";
            string hairline = dark ? "#333333" : "#E8E8E8";
            string wash = dark ? "#2D2D2D" : "#EFEFEF";
            string hover = dark ? "#262626" : "#F6F6F6";
            string railBg = dark ? "#222222" : "#F7F7F7";

            bool isDefault = DefaultBrowserRegistrar.IsDefaultBrowser();
            string lookPref = p.Appearance ?? "System";
            string tabLayout = p.TabLayout ?? "Left";
            string syncFolder = string.IsNullOrEmpty(p.SyncFolderPath) ? "Off" : p.SyncFolderPath;

            var loginsHtml = new StringBuilder();
            if (Store.Logins.Count == 0)
            {
                loginsHtml.Append("<div class='st-nothing'>No saved passwords yet. Search offers to save sign-ins once they work, encrypted in your Windows DPAPI keychain.</div>");
            }
            else
            {
                for (int i = 0; i < Store.Logins.Count; i++)
                {
                    var l = Store.Logins[i];
                    if (i > 0) loginsHtml.Append("<div class='st-rule'></div>");
                    loginsHtml.Append("<div class='st-line'><div class='st-line-words'><div class='st-line-title'>" + EscHtml(l.Host) + "</div><div class='st-line-detail'>" + EscHtml(l.User) + "</div></div>" +
                                      "<div class='st-line-control'>" +
                                      "<button class='pill-st' onclick=\"act('copyPassword', '" + Esc(l.Host) + "|" + Esc(l.User) + "')\">Copy password</button>" +
                                      "<button class='pill-st red' onclick=\"act('deletePassword', '" + Esc(l.Host) + "|" + Esc(l.User) + "')\">Remove</button>" +
                                      "</div></div>");
                }
            }

            var extHtml = new StringBuilder();
            if (Store.Extensions.Count == 0)
            {
                extHtml.Append("<div class='st-nothing'>No extensions yet.</div>");
            }
            else
            {
                for (int i = 0; i < Store.Extensions.Count; i++)
                {
                    var ext = Store.Extensions[i];
                    if (i > 0) extHtml.Append("<div class='st-rule'></div>");
                    extHtml.Append("<div class='st-line'><div class='st-line-words'><div class='st-line-title'>" + EscHtml(ext.Name) + " <span style='color:var(--muted);font-size:11.5px'>v" + EscHtml(ext.Version) + "</span></div><div class='st-line-detail'>" + EscHtml(ext.Id) + "</div></div>" +
                                   "<div class='st-line-control'>" +
                                   "<button class='pill-st' onclick=\"act('openExtPopup', '" + Esc(ext.Id) + "')\">Open Popup</button>" +
                                   "<button class='pill-st red' onclick=\"act('removeExt', '" + Esc(ext.Id) + "')\">Remove</button>" +
                                   "</div></div>");
                }
            }

            string sleepDetail = p.SleepLevel == "Never" ? "Background tabs stay awake." :
                                 p.SleepLevel == "Later" ? "After two hours away, a tab rests and wakes when you go back to it." :
                                 p.SleepLevel == "Sooner" ? "After ten minutes away, a tab rests and wakes when you go back to it." :
                                 "After half an hour away, a tab rests and wakes when you go back to it.";

            string wallMode = p.BlankWallMode ?? "Off";
            string wallDesc = wallMode == "Off" ? "The blank tab stays plain." :
                              wallMode == "Wall painting" ? "Soft ambient gradient behind the blank tab (" + EscHtml(p.BlankWallTheme) + ")." :
                              wallMode == "Stickers" ? "Subtle dot-grid pattern behind the blank tab." :
                              wallMode == "Solid colour" ? "Solid tint behind the blank tab." :
                              wallMode == "Gradient" ? "Custom gradient behind the blank tab." :
                              "Your own picture behind the blank tab.";

            string wallSubControls = "";
            if (wallMode == "Wall painting")
            {
                wallSubControls = "<div style='display:flex;gap:6px;margin-top:8px;flex-wrap:wrap'>" +
                    "<button class='pill-st " + (p.BlankWallTheme == "Dusk Gradient" ? "filled" : "") + "' onclick=\"act('setWall','Dusk Gradient')\">Dusk Gradient</button>" +
                    "<button class='pill-st " + (p.BlankWallTheme == "Warm Paper" ? "filled" : "") + "' onclick=\"act('setWall','Warm Paper')\">Warm Paper</button>" +
                    "<button class='pill-st " + (p.BlankWallTheme == "Forest Mist" ? "filled" : "") + "' onclick=\"act('setWall','Forest Mist')\">Forest Mist</button>" +
                    "</div>";
            }
            else if (wallMode == "Solid colour")
            {
                wallSubControls = "<div style='display:flex;gap:6px;margin-top:8px'>" +
                    "<button class='pill-st' onclick=\"setPref('BlankWallCustomValue','#1e293b')\">Slate</button>" +
                    "<button class='pill-st' onclick=\"setPref('BlankWallCustomValue','#1c1917')\">Stone</button>" +
                    "<button class='pill-st' onclick=\"setPref('BlankWallCustomValue','#172554')\">Midnight</button>" +
                    "<button class='pill-st' onclick=\"setPref('BlankWallCustomValue','#14532d')\">Pine</button>" +
                    "</div>";
            }
            else if (wallMode == "Image")
            {
                wallSubControls = "<div style='margin-top:8px'><button class='pill-st' onclick=\"act('chooseBgImage')\">Choose Image…</button></div>";
            }

            return @"<!DOCTYPE html>
<html data-root='page' data-look='" + (dark ? "dark" : "light") + @"'>
<head>
<meta charset='utf-8'>
<title>Settings</title>
<style>
  :root {
    --ground: " + ground + @";
    --ink: " + ink + @";
    --ink-rgb: " + inkRgb + @";
    --muted: " + muted + @";
    --faint: " + faint + @";
    --hairline: " + hairline + @";
    --wash: " + wash + @";
    --hover: " + hover + @";
    --settings-rail: 196px;
    --settings-column: 600px;
    --card-group-radius: 11px;
  }
  * { box-sizing: border-box; margin: 0; padding: 0; }
  html, body {
    height: 100%;
    background: var(--ground);
    color: var(--ink);
    font-family: 'Segoe UI Variable Text', 'Plus Jakarta Sans', 'Segoe UI', system-ui, sans-serif;
    font-size: 13px;
    line-height: 15.5px;
    -webkit-font-smoothing: antialiased;
    user-select: none;
    overflow: hidden;
  }
  button, input, select { font-family: inherit; border: none; outline: none; background: none; color: inherit; }
  button { cursor: pointer; }

  .st { display: flex; height: 100%; }
  .rail {
    width: var(--settings-rail);
    flex: none;
    display: flex;
    flex-direction: column;
    gap: 2px;
    padding: 8px;
    background: " + railBg + @";
    overflow-y: auto;
  }
  .rail-title { padding: 14px 10px 10px; font-weight: 600; font-size: 13px; }
  .st-find {
    display: flex;
    align-items: center;
    gap: 7px;
    height: 28px;
    padding: 0 9px;
    margin-bottom: 10px;
    border-radius: 8px;
    background: var(--ground);
    box-shadow: inset 0 0 0 1px var(--hairline);
    color: var(--muted);
    flex: none;
  }
  .st-find input { flex: 1; min-width: 0; font-size: 12.5px; color: var(--ink); user-select: text; }
  .st-find-key { font-size: 10.5px; color: var(--muted); opacity: 0.8; }
  .rail-rule { height: 1px; background: var(--hairline); margin: 7px 10px; flex: none; }
  .rail-row {
    display: flex;
    align-items: center;
    gap: 9px;
    height: 30px;
    padding: 0 10px;
    border-radius: 8px;
    color: var(--muted);
    text-align: left;
    transition: background 140ms, color 140ms;
  }
  .rail-row svg { width: 15px; height: 15px; flex: none; stroke: currentColor; fill: none; stroke-width: 1.5; stroke-linecap: round; stroke-linejoin: round; }
  .rail-row:hover:not(.on) { background: var(--hover); color: var(--ink); }
  .rail-row.on { background: var(--ground); color: var(--ink); font-weight: 500; box-shadow: 0 1px 6px rgba(0,0,0,0.08); }
  .rail-name { flex: 1; font-size: 13px; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }

  .st-hairline { width: 1px; flex: none; background: var(--hairline); }

  .st-content { flex: 1; min-width: 0; display: flex; flex-direction: column; padding-top: 38px; overflow-y: auto; }
  .st-page { display: none; width: 100%; max-width: var(--settings-column); margin: 0 auto; padding: 0 24px 64px; flex-direction: column; gap: 22px; }
  .st-page.on { display: flex; }
  .st-head { display: flex; align-items: center; gap: 10px; padding-bottom: 4px; }
  .st-head svg { width: 18px; height: 18px; stroke: currentColor; fill: none; stroke-width: 1.6; stroke-linecap: round; stroke-linejoin: round; }
  .st-head h1 { font-size: 17px; font-weight: 600; }

  .st-group { display: flex; flex-direction: column; gap: 8px; }
  .st-group-head {
    display: flex;
    align-items: center;
    gap: 6px;
    padding-left: 2px;
    font-size: 10.5px;
    font-weight: 600;
    letter-spacing: 0.5px;
    text-transform: uppercase;
    color: var(--muted);
  }
  .card-st {
    background: var(--ground);
    border-radius: var(--card-group-radius);
    box-shadow: inset 0 0 0 1px var(--hairline);
    overflow: hidden;
  }
  .st-rule { height: 1px; background: var(--hairline); }

  .sr { display: flex; flex-direction: column; gap: 10px; padding: 11px 14px; }
  .sr-main { display: flex; align-items: center; gap: 12px; }
  .sr-tile {
    width: 26px;
    height: 26px;
    flex: none;
    display: grid;
    place-items: center;
    border-radius: 7px;
    background: var(--wash);
    color: var(--ink);
  }
  .sr-tile svg { width: 14px; height: 14px; stroke: currentColor; fill: none; stroke-width: 1.5; stroke-linecap: round; stroke-linejoin: round; }
  .sr-words { flex: 1; min-width: 0; display: flex; flex-direction: column; gap: 2px; }
  .sr-title { font-size: 13px; color: var(--ink); display: flex; align-items: center; gap: 5px; }
  .sr-detail { font-size: 11.5px; line-height: 13.7px; color: var(--muted); }
  .sr-control { flex: none; margin-left: 8px; display: flex; align-items: center; gap: 6px; }
  .sr-wide { padding-left: 38px; padding-bottom: 4px; }

  .st-info {
    display: inline-grid;
    place-items: center;
    width: 13px;
    height: 13px;
    border-radius: 50%;
    border: 1px solid var(--faint);
    color: var(--muted);
    font-size: 9px;
    font-weight: 600;
    cursor: help;
  }

  .st-sw {
    position: relative;
    width: 30px;
    height: 18px;
    border-radius: 9px;
    background: var(--faint);
    flex: none;
    transition: background 180ms;
  }
  .st-sw.on { background: var(--ink); }
  .st-sw-knob {
    position: absolute;
    top: 2px;
    left: 2px;
    width: 14px;
    height: 14px;
    border-radius: 50%;
    background: var(--ground);
    box-shadow: 0 1px 3px rgba(0,0,0,0.22);
    transition: transform 180ms;
  }
  .st-sw.on .st-sw-knob { transform: translateX(12px); }

  .pill-st {
    font-size: 11.5px;
    line-height: 13.7px;
    padding: 5px 11px;
    border-radius: 999px;
    background: var(--ground);
    color: var(--ink);
    box-shadow: inset 0 0 0 1px var(--hairline);
    white-space: nowrap;
    transition: background 140ms;
  }
  .pill-st:hover { background: var(--hover); }
  .pill-st.filled { background: var(--ink); color: var(--ground); box-shadow: none; font-weight: 500; }
  .pill-st.red { color: #E5484D; }

  .st-seg {
    display: inline-flex;
    padding: 2px;
    border-radius: 8px;
    background: var(--wash);
    box-shadow: inset 0 0 0 1px var(--hairline);
    gap: 2px;
  }
  .st-seg-btn {
    padding: 4px 10px;
    border-radius: 6px;
    font-size: 11.5px;
    color: var(--muted);
    white-space: nowrap;
    transition: background 120ms, color 120ms;
  }
  .st-seg-btn.on {
    background: var(--ground);
    color: var(--ink);
    font-weight: 500;
    box-shadow: 0 1px 3px rgba(0,0,0,0.14);
  }

  .st-sel {
    height: 26px;
    padding: 0 10px;
    border-radius: 8px;
    background: var(--wash);
    color: var(--ink);
    font-size: 11.5px;
    box-shadow: inset 0 0 0 1px var(--hairline);
    cursor: pointer;
  }
  .st-sel option { background: var(--ground); color: var(--ink); }

  .st-chks { display: flex; flex-wrap: wrap; gap: 14px; }
  .st-chk { display: inline-flex; align-items: center; gap: 7px; font-size: 12px; color: var(--ink); cursor: pointer; }
  .st-chk-box {
    width: 14px;
    height: 14px;
    border-radius: 4px;
    border: 1px solid var(--faint);
    display: grid;
    place-items: center;
    font-size: 10px;
    font-weight: 700;
    color: #fff;
  }
  .st-chk-box.on { background: #3B82F6; border-color: #3B82F6; }

  .st-pics { display: flex; align-items: flex-start; gap: 14px; flex-wrap: wrap; }
  .st-pic { display: flex; flex-direction: column; align-items: center; gap: 6px; cursor: pointer; }
  .st-pic-ring { padding: 3px; border-radius: 9.5px; box-shadow: inset 0 0 0 2px transparent; transition: box-shadow 160ms; }
  .st-pic.on .st-pic-ring { box-shadow: inset 0 0 0 2px var(--ink); }
  .st-pic-frame {
    position: relative;
    display: block;
    width: 76px;
    height: 50px;
    border-radius: 7px;
    overflow: hidden;
    background: var(--wash);
    box-shadow: inset 0 0 0 1px var(--hairline);
  }
  .st-pic-label { font-size: 11.5px; color: var(--muted); }
  .st-pic.on .st-pic-label { color: var(--ink); font-weight: 500; }

  .home-preview {
    position: relative;
    height: 140px;
    border-radius: 10px;
    background: var(--ground);
    box-shadow: inset 0 0 0 1px var(--hairline);
    display: grid;
    place-items: center;
    overflow: hidden;
  }
  .home-preview-tabs { position: absolute; left: 10px; top: 10px; display: flex; flex-direction: column; gap: 4px; }
  .home-preview-tab { width: 42px; height: 6px; border-radius: 3px; background: var(--wash); }
  .home-preview-pill {
    padding: 6px 22px;
    border-radius: 999px;
    border: 1px solid var(--hairline);
    background: var(--ground);
    font-size: 11px;
    color: var(--muted);
  }

  .st-nothing { font-size: 12.5px; color: var(--muted); padding: 16px 14px; }
  .st-line { display: flex; align-items: center; gap: 16px; padding: 10px 14px; }
  .st-line-words { flex: 1; min-width: 0; display: flex; flex-direction: column; gap: 2px; }
  .st-line-title { font-size: 13px; color: var(--ink); }
  .st-line-detail { font-size: 11.5px; color: var(--muted); overflow: hidden; text-overflow: ellipsis; }
  .st-line-control { flex: none; display: flex; gap: 6px; align-items: center; }
  .st-input {
    height: 30px;
    padding: 0 10px;
    border-radius: 8px;
    background: var(--wash);
    color: var(--ink);
    font-size: 12.5px;
    box-shadow: inset 0 0 0 1px var(--hairline);
    flex: 1;
    user-select: text;
  }
  .key-badge {
    font-size: 11px;
    padding: 4px 8px;
    border-radius: 6px;
    background: var(--wash);
    color: var(--ink);
    box-shadow: inset 0 0 0 1px var(--hairline);
    white-space: nowrap;
  }
  .key-badge.none { color: var(--muted); }
</style>
</head>
<body>
<div class='st'>
  <div class='rail'>
    <div class='rail-title'>Settings</div>
    <div class='st-find'>
      <svg width='12' height='12' viewBox='0 0 16 16' fill='none' stroke='currentColor' stroke-width='1.6'><circle cx='7' cy='7' r='4.3'/><path d='m10.3 10.3 3.2 3.2'/></svg>
      <input id='findBox' type='text' placeholder='Find a setting' oninput='filterSettings(this.value)' />
      <span class='st-find-key'>Ctrl+F</span>
    </div>

    <button class='rail-row' data-sec='general' onclick=""showSec('general')"">
      <svg viewBox='0 0 16 16'><circle cx='8' cy='8' r='2.2'/><path d='M8 2v1.6M8 12.4V14M2 8h1.6M12.4 8H14M3.8 3.8l1.1 1.1M11.1 11.1l1.1 1.1M12.2 3.8l-1.1 1.1M4.9 11.1l-1.1 1.1'/></svg>
      <span class='rail-name'>General</span>
    </button>
    <button class='rail-row' data-sec='appearance' onclick=""showSec('appearance')"">
      <svg viewBox='0 0 16 16'><path d='M11.5 2.5a2.1 2.1 0 0 1 2 2L6 12l-3.5 1 1-3.5z'/></svg>
      <span class='rail-name'>Appearance</span>
    </button>
    <button class='rail-row' data-sec='themes' onclick=""showSec('themes')"">
      <svg viewBox='0 0 16 16'><rect x='2.2' y='3' width='11.6' height='10' rx='2'/><circle cx='5.8' cy='6.2' r='1.1'/><path d='m3 11.5 3.5-3.2 2.5 2.2 2-1.8 2.5 2.5'/></svg>
      <span class='rail-name'>Themes</span>
    </button>
    <button class='rail-row' data-sec='tabs' onclick=""showSec('tabs')"">
      <svg viewBox='0 0 16 16'><rect x='2.2' y='3' width='11.6' height='10' rx='2'/><path d='M6 3v10'/></svg>
      <span class='rail-name'>Tabs</span>
    </button>
    <button class='rail-row' data-sec='browsing' onclick=""showSec('browsing')"">
      <svg viewBox='0 0 16 16'><circle cx='8' cy='8' r='5.6'/><path d='M2.5 8h11M8 2.4c1.6 1.6 2.4 3.5 2.4 5.6S9.6 12 8 13.6C6.4 12 5.6 10.1 5.6 8S6.4 4 8 2.4'/></svg>
      <span class='rail-name'>Browsing</span>
    </button>
    <button class='rail-row' data-sec='spaces' onclick=""showSec('spaces')"">
      <svg viewBox='0 0 16 16'><rect x='2.5' y='2.5' width='4.5' height='4.5' rx='1.2'/><rect x='9' y='2.5' width='4.5' height='4.5' rx='1.2'/><rect x='2.5' y='9' width='4.5' height='4.5' rx='1.2'/><rect x='9' y='9' width='4.5' height='4.5' rx='1.2'/></svg>
      <span class='rail-name'>Spaces &amp; Pins</span>
    </button>

    <div class='rail-rule'></div>

    <button class='rail-row' data-sec='passwords' onclick=""showSec('passwords')"">
      <svg viewBox='0 0 16 16'><circle cx='5.5' cy='8' r='2.6'/><path d='M8.1 8h5.4M11.5 8v2.2M13.5 8v2.2'/></svg>
      <span class='rail-name'>Passwords</span>
    </button>
    <button class='rail-row' data-sec='privacy' onclick=""showSec('privacy')"">
      <svg viewBox='0 0 16 16'><path d='M8 2.2 3.2 4.2v3.6c0 3.2 2 5.4 4.8 6.2 2.8-.8 4.8-3 4.8-6.2V4.2z'/></svg>
      <span class='rail-name'>Privacy</span>
    </button>
    <button class='rail-row' data-sec='extensions' onclick=""showSec('extensions')"">
      <svg viewBox='0 0 16 16'><path d='M6 2.8a1.4 1.4 0 0 1 2.8 0v1h2.6v2.6h1a1.4 1.4 0 0 1 0 2.8h-1v3.2H8.6v-1a1.4 1.4 0 0 0-2.8 0v1H2.6V9.2h1a1.4 1.4 0 0 0 0-2.8h-1V3.8H6z'/></svg>
      <span class='rail-name'>Extensions</span>
    </button>
    <button class='rail-row' data-sec='shortcuts' onclick=""showSec('shortcuts')"">
      <svg viewBox='0 0 16 16'><rect x='2' y='4' width='12' height='8' rx='1.8'/><path d='M4.5 6.8h1M7.5 6.8h1M10.5 6.8h1M5 9.5h6'/></svg>
      <span class='rail-name'>Keyboard &amp; Mouse</span>
    </button>

    <div class='rail-rule'></div>

    <button class='rail-row' data-sec='about' onclick=""showSec('about')"">
      <svg viewBox='0 0 16 16'><circle cx='8' cy='8' r='5.6'/><path d='M8 7.2v3.6M8 5.1h.01'/></svg>
      <span class='rail-name'>About</span>
    </button>
  </div>

  <div class='st-hairline'></div>

  <div class='st-content'>

    <!-- 1. GENERAL -->
    <div class='st-page' id='sec-general'>
      <div class='st-head'>
        <svg viewBox='0 0 16 16'><circle cx='8' cy='8' r='2.2'/><path d='M8 2v1.6M8 12.4V14M2 8h1.6M12.4 8H14M3.8 3.8l1.1 1.1M11.1 11.1l1.1 1.1M12.2 3.8l-1.1 1.1M4.9 11.1l-1.1 1.1'/></svg>
        <h1>General</h1>
      </div>

      <div class='st-group'>
        <div class='st-group-head'>YOUR BROWSER</div>
        <div class='card-st'>
          <div class='sr'><div class='sr-main'>
            <div class='sr-tile'><svg viewBox='0 0 16 16'><path d='M6.5 9.5 9.5 6.5M5.5 7.5l-1.3 1.3a2.1 2.1 0 0 0 3 3l1.3-1.3M10.5 8.5l1.3-1.3a2.1 2.1 0 0 0-3-3L7.5 5.5'/></svg></div>
            <div class='sr-words'><div class='sr-title'>Open links from other apps</div><div class='sr-detail'>Make Search the browser Outlook, Slack, Discord and the rest send links to</div></div>
            <div class='sr-control'><button class='pill-st " + (isDefault ? "" : "filled") + @"' onclick=""act('makeDefault')"">" + (isDefault ? "Default ✓" : "Make default") + @"</button></div>
          </div></div>
          <div class='st-rule'></div>
          <div class='sr'><div class='sr-main'>
            <div class='sr-tile'><svg viewBox='0 0 16 16'><rect x='2' y='3.5' width='12' height='9' rx='1.5'/><path d='m2.5 4 5.5 4.2L13.5 4'/></svg></div>
            <div class='sr-words'><div class='sr-title'>Email links open in " + Info("Choose what opens mailto: links") + @"</div><div class='sr-detail'>Write in Gmail or another webmail, in a tab, instead of your mail app</div></div>
            <div class='sr-control'>" + Sel("MailtoHandler", p.MailtoHandler, "Your mail app", "Gmail", "Outlook", "Fastmail") + @"</div>
          </div></div>
          <div class='st-rule'></div>
          <div class='sr'><div class='sr-main'>
            <div class='sr-tile'><svg viewBox='0 0 16 16'><path d='M8 3v7M5.5 7.5 8 10l2.5-2.5M3 12.5h10'/></svg></div>
            <div class='sr-words'><div class='sr-title'>Bring things over " + Info("Import from another browser") + @"</div><div class='sr-detail'>Bookmarks, history, passwords and extensions from another browser</div></div>
            <div class='sr-control'><button class='pill-st' onclick=""act('importBrowsers')"">Bring Things Over…</button></div>
          </div></div>
          <div class='st-rule'></div>
          <div class='sr'><div class='sr-main'>
            <div class='sr-tile'><svg viewBox='0 0 16 16'><path d='M3.5 6.5A5 5 0 0 1 12.5 5M12.5 9.5A5 5 0 0 1 3.5 11'/><path d='M12.5 2.5V5h-2.5M3.5 13.5V11H6'/></svg></div>
            <div class='sr-words'><div class='sr-title'>Sync with your other computers " + Info("Folder-based sync via OneDrive, Dropbox, or iCloud") + @"</div><div class='sr-detail'>Spaces, pins, bookmarks and settings, through a folder you choose (" + EscHtml(syncFolder) + @")</div></div>
            <div class='sr-control'><button class='pill-st' onclick=""act('chooseSyncFolder')"">Choose Folder…</button>" + Sw("SyncEnabled", p.SyncEnabled) + @"</div>
          </div></div>
          <div class='st-rule'></div>
          <div class='sr'><div class='sr-main'>
            <div class='sr-tile'><svg viewBox='0 0 16 16'><path d='M8 3v5M5 4.8a4.5 4.5 0 1 0 6 0'/></svg></div>
            <div class='sr-words'><div class='sr-title'>Confirm before quitting</div><div class='sr-detail'>Ask before Alt+F4 or Quit Search closes the browser</div></div>
            <div class='sr-control'>" + Sw("ConfirmBeforeQuitting", p.ConfirmBeforeQuitting) + @"</div>
          </div></div>
        </div>
      </div>

      <div class='st-group'>
        <div class='st-group-head'>DOWNLOADS</div>
        <div class='card-st'>
          <div class='sr'><div class='sr-main'>
            <div class='sr-tile'><svg viewBox='0 0 16 16'><path d='M2.5 5.2C2.5 4.3 3.2 3.6 4.1 3.6h2.7l1.4 1.5h3.7c.9 0 1.6.7 1.6 1.6v4.5c0 .9-.7 1.6-1.6 1.6H4.1c-.9 0-1.6-.7-1.6-1.6z'/></svg></div>
            <div class='sr-words'><div class='sr-title'>Save to</div><div class='sr-detail'>" + EscHtml(p.DownloadsFolder) + @"</div></div>
            <div class='sr-control'><button class='pill-st' onclick=""act('chooseDownloadsFolder')"">Change…</button><button class='pill-st' onclick=""act('openDownloadsFolder')"">Show</button></div>
          </div></div>
          <div class='st-rule'></div>
          <div class='sr'><div class='sr-main'>
            <div class='sr-tile'><svg viewBox='0 0 16 16'><path d='M2.5 5.2C2.5 4.3 3.2 3.6 4.1 3.6h2.7l1.4 1.5h3.7c.9 0 1.6.7 1.6 1.6v4.5c0 .9-.7 1.6-1.6 1.6H4.1c-.9 0-1.6-.7-1.6-1.6z'/></svg></div>
            <div class='sr-words'><div class='sr-title'>Ask where to save each file</div><div class='sr-detail'>A save sheet for every download</div></div>
            <div class='sr-control'>" + Sw("AskWhereToSave", p.AskWhereToSave) + @"</div>
          </div></div>
          <div class='st-rule'></div>
          <div class='sr'><div class='sr-main'>
            <div class='sr-tile'><svg viewBox='0 0 16 16'><path d='M8 3v7M5.5 7.5 8 10l2.5-2.5M3 12.5h10'/></svg></div>
            <div class='sr-words'><div class='sr-title'>Always show the downloads button</div><div class='sr-detail'>Off, it shows only while a file comes in</div></div>
            <div class='sr-control'>" + Sw("AlwaysShowDownloadsButton", p.AlwaysShowDownloadsButton) + @"</div>
          </div></div>
          <div class='st-rule'></div>
          <div class='sr'><div class='sr-main'>
            <div class='sr-tile'><svg viewBox='0 0 16 16'><path d='M8 3v7M5.5 7.5 8 10l2.5-2.5M3 12.5h10'/></svg></div>
            <div class='sr-words'><div class='sr-title'>Open Downloads " + Info("Where Ctrl+J opens") + @"</div><div class='sr-detail'>From its button, Ctrl+J and History &gt; Downloads</div></div>
            <div class='sr-control'>" + Sel("OpenDownloadsIn", p.OpenDownloadsIn, "In a tab", "In a panel") + @"</div>
          </div></div>
        </div>
      </div>

      <div class='st-group'>
        <div class='st-group-head'>AUTOMATION</div>
        <div class='card-st'>
          <div class='sr'><div class='sr-main'>
            <div class='sr-tile'><svg viewBox='0 0 16 16'><path d='M4 5l3 3-3 3M9 11h3'/></svg></div>
            <div class='sr-words'><div class='sr-title'>Let a script drive Search</div><div class='sr-detail'>Listens on \\.\pipe\search-bench for ./bench.ps1 and coding agents</div></div>
            <div class='sr-control'>" + Sw("BenchServerEnabled", p.BenchServerEnabled) + @"</div>
          </div></div>
        </div>
      </div>
    </div>

    <!-- 2. APPEARANCE -->
    <div class='st-page' id='sec-appearance'>
      <div class='st-head'>
        <svg viewBox='0 0 16 16'><path d='M11.5 2.5a2.1 2.1 0 0 1 2 2L6 12l-3.5 1 1-3.5z'/></svg>
        <h1>Appearance</h1>
      </div>

      <div class='st-group'>
        <div class='st-group-head'>LIGHT AND DARK</div>
        <div class='card-st'>
          <div class='sr'>
            <div class='sr-main'>
              <div class='sr-tile'><svg viewBox='0 0 16 16'><circle cx='8' cy='8' r='4.5'/></svg></div>
              <div class='sr-words'><div class='sr-title'>Appearance</div><div class='sr-detail'>Light, dark, or whatever Windows is doing. Pages follow it too</div></div>
            </div>
            <div class='sr-wide'>
              <div class='st-pics'>
                <div class='st-pic " + (lookPref == "Light" ? "on" : "") + @"' onclick=""act('setLook','Light')"">
                  <div class='st-pic-ring'><span class='st-pic-frame' style='background:#f4f4f4'><span style='position:absolute;left:18px;top:6px;right:4px;bottom:4px;background:#fff;border-radius:4px;border:1px solid #e0e0e0'></span></span></div>
                  <span class='st-pic-label'>Light</span>
                </div>
                <div class='st-pic " + (lookPref == "Dark" ? "on" : "") + @"' onclick=""act('setLook','Dark')"">
                  <div class='st-pic-ring'><span class='st-pic-frame' style='background:#131313'><span style='position:absolute;left:18px;top:6px;right:4px;bottom:4px;background:#1c1c1c;border-radius:4px;border:1px solid #333'></span></span></div>
                  <span class='st-pic-label'>Dark</span>
                </div>
                <div class='st-pic " + (lookPref == "System" ? "on" : "") + @"' onclick=""act('setLook','System')"">
                  <div class='st-pic-ring'><span class='st-pic-frame' style='background:linear-gradient(90deg, #f4f4f4 50%, #131313 50%)'><span style='position:absolute;left:18px;top:6px;right:4px;bottom:4px;background:linear-gradient(90deg, #fff 50%, #1c1c1c 50%);border-radius:4px;border:1px solid #555'></span></span></div>
                  <span class='st-pic-label'>Auto</span>
                </div>
              </div>
            </div>
          </div>
          <div class='st-rule'></div>
          <div class='sr'><div class='sr-main'>
            <div class='sr-tile'><svg viewBox='0 0 16 16'><path d='M10.5 3.2a5 5 0 1 0 2.3 9.3 4.5 4.5 0 0 1-2.3-9.3z'/></svg></div>
            <div class='sr-words'><div class='sr-title'>Darken pages that stay light " + Info("Dusk mode: Ctrl+Shift+D toggles per site") + @"</div><div class='sr-detail'>Pages with no dark look of their own are darkened too</div></div>
            <div class='sr-control'>" + Sw("DuskEnabled", p.DuskEnabled) + @"</div>
          </div></div>
          <div class='st-rule'></div>
          <div class='sr'><div class='sr-main'>
            <div class='sr-tile'><svg viewBox='0 0 16 16'><path d='M10.5 3.2a5 5 0 1 0 2.3 9.3 4.5 4.5 0 0 1-2.3-9.3z'/></svg></div>
            <div class='sr-words'><div class='sr-title'>Keep the Web Inspector dark " + Info("DevTools stays dark in light mode") + @"</div><div class='sr-detail'>The tools stay dark while Search is light</div></div>
            <div class='sr-control'>" + Sw("KeepDevToolsDark", p.KeepDevToolsDark) + @"</div>
          </div></div>
        </div>
      </div>

      <div class='st-group'>
        <div class='st-group-head'>WINDOW</div>
        <div class='card-st'>
          <div class='sr'>
            <div class='sr-main'>
              <div class='sr-tile'><svg viewBox='0 0 16 16'><rect x='2.2' y='3' width='11.6' height='10' rx='2'/><path d='M6 3v10'/></svg></div>
              <div class='sr-words'><div class='sr-title'>Tabs " + Info("Ctrl+Shift+S cycles layout") + @"</div><div class='sr-detail'>Across the top of the window, down a side as a column, or as icons only</div></div>
            </div>
            <div class='sr-wide'>
              <div class='st-pics'>
                <div class='st-pic " + (tabLayout == "Top" ? "on" : "") + @"' onclick=""act('setTabLayout','Top')"">
                  <div class='st-pic-ring'><span class='st-pic-frame'><span style='position:absolute;left:4px;top:12px;right:4px;bottom:4px;background:var(--ground);border-radius:4px;border:1px solid var(--hairline)'></span></span></div>
                  <span class='st-pic-label'>Top</span>
                </div>
                <div class='st-pic " + (tabLayout == "Left" ? "on" : "") + @"' onclick=""act('setTabLayout','Left')"">
                  <div class='st-pic-ring'><span class='st-pic-frame'><span style='position:absolute;left:22px;top:6px;right:4px;bottom:4px;background:var(--ground);border-radius:4px;border:1px solid var(--hairline)'></span></span></div>
                  <span class='st-pic-label'>Left</span>
                </div>
                <div class='st-pic " + (tabLayout == "Right" ? "on" : "") + @"' onclick=""act('setTabLayout','Right')"">
                  <div class='st-pic-ring'><span class='st-pic-frame'><span style='position:absolute;left:4px;top:6px;right:22px;bottom:4px;background:var(--ground);border-radius:4px;border:1px solid var(--hairline)'></span></span></div>
                  <span class='st-pic-label'>Right</span>
                </div>
                <div class='st-pic " + (tabLayout == "IconsOnly" ? "on" : "") + @"' onclick=""act('setTabLayout','IconsOnly')"">
                  <div class='st-pic-ring'><span class='st-pic-frame'><span style='position:absolute;left:13px;top:6px;right:4px;bottom:4px;background:var(--ground);border-radius:4px;border:1px solid var(--hairline)'></span></span></div>
                  <span class='st-pic-label'>Icons only</span>
                </div>
              </div>
            </div>
          </div>
          <div class='st-rule'></div>
          <div class='sr'><div class='sr-main'>
            <div class='sr-tile'><svg viewBox='0 0 16 16'><rect x='2.2' y='3' width='11.6' height='10' rx='2'/></svg></div>
            <div class='sr-words'><div class='sr-title'>Hide the column until the pointer reaches the edge</div><div class='sr-detail'>The page takes the whole window; Ctrl+S keeps the tabs out</div></div>
            <div class='sr-control'>" + Sw("SidebarCollapsed", p.SidebarCollapsed) + @"</div>
          </div></div>
          <div class='st-rule'></div>
          <div class='sr'><div class='sr-main'>
            <div class='sr-tile'><svg viewBox='0 0 16 16'><rect x='2.2' y='3' width='11.6' height='10' rx='2'/><path d='M6 3v10'/></svg></div>
            <div class='sr-words'><div class='sr-title'>Slide the column in and out " + Info("Animated sidebar reveal") + @"</div><div class='sr-detail'>A gentler slide when Ctrl+S or the window's edge brings the column out or puts it away</div></div>
            <div class='sr-control'>" + Sw("SlideSidebar", p.SlideSidebar) + @"</div>
          </div></div>
          <div class='st-rule'></div>
          <div class='sr'><div class='sr-main'>
            <div class='sr-tile'><svg viewBox='0 0 16 16'><path d='M10 4L6 8l4 4'/></svg></div>
            <div class='sr-words'><div class='sr-title'>Ctrl+S makes the column icons only " + Info("Collapse to narrow icon strip") + @"</div><div class='sr-detail'>The tabs stay in sight as a narrow column of icons; Ctrl+S again brings the titles back</div></div>
            <div class='sr-control'>" + Sw("CtrlSMakesIconsOnly", p.CtrlSMakesIconsOnly) + @"</div>
          </div></div>
          <div class='st-rule'></div>
          <div class='sr'><div class='sr-main'>
            <div class='sr-tile'><svg viewBox='0 0 16 16'><rect x='2.2' y='3' width='11.6' height='10' rx='2'/><path d='M3.5 5h9'/></svg></div>
            <div class='sr-words'><div class='sr-title'>A loading line with the tabs hidden " + Info("Thin top progress bar") + @"</div><div class='sr-detail'>Folded away with Ctrl+S, a thin line along the page's top while it loads</div></div>
            <div class='sr-control'>" + Sw("LoadingLineWhenHidden", p.LoadingLineWhenHidden) + @"</div>
          </div></div>
          <div class='st-rule'></div>
          <div class='sr'>
            <div class='sr-main'>
              <div class='sr-tile'><svg viewBox='0 0 16 16'><rect x='2.2' y='3' width='11.6' height='10' rx='2'/></svg></div>
              <div class='sr-words'><div class='sr-title'>The page as a card " + Info("Inset rounded card around web content") + @"</div><div class='sr-detail'>A little room around the page, with rounded corners</div></div>
            </div>
            <div class='sr-wide'>
              <div class='st-pics'>
                <div class='st-pic " + (p.PageAsCard ? "on" : "") + @"' onclick=""setPref('PageAsCard','true')"">
                  <div class='st-pic-ring'><span class='st-pic-frame'><span style='position:absolute;left:22px;top:6px;right:4px;bottom:4px;background:var(--ground);border-radius:4px;border:1px solid var(--hairline)'></span></span></div>
                  <span class='st-pic-label'>As a card</span>
                </div>
                <div class='st-pic " + (!p.PageAsCard ? "on" : "") + @"' onclick=""setPref('PageAsCard','false')"">
                  <div class='st-pic-ring'><span class='st-pic-frame'><span style='position:absolute;left:20px;top:0;right:0;bottom:0;background:var(--ground);border-left:1px solid var(--hairline)'></span></span></div>
                  <span class='st-pic-label'>Edge to edge</span>
                </div>
              </div>
            </div>
          </div>
          <div class='st-rule'></div>
          <div class='sr'><div class='sr-main'>
            <div class='sr-tile'><svg viewBox='0 0 16 16'><path d='M3 6V3h3M10 3h3v3M13 10v3h-3M6 13H3v-3'/></svg></div>
            <div class='sr-words'><div class='sr-title'>Keep the card in full screen</div><div class='sr-detail'>Off, the page goes edge to edge when the window fills the screen</div></div>
            <div class='sr-control'>" + Sw("KeepCardInFullScreen", p.KeepCardInFullScreen) + @"</div>
          </div></div>
          <div class='st-rule'></div>
          <div class='sr'><div class='sr-main'>
            <div class='sr-tile'><svg viewBox='0 0 16 16'><path d='M6.5 9.5 9.5 6.5M5.5 7.5l-1.3 1.3a2.1 2.1 0 0 0 3 3l1.3-1.3M10.5 8.5l1.3-1.3a2.1 2.1 0 0 0-3-3L7.5 5.5'/></svg></div>
            <div class='sr-words'><div class='sr-title'>Always show the address bar</div><div class='sr-detail'>The page's full address, to type in</div></div>
            <div class='sr-control'>" + Sw("AlwaysShowAddressBar", p.AlwaysShowAddressBar) + @"</div>
          </div></div>
        </div>
      </div>

      <div class='st-group'>
        <div class='st-group-head'>TABS</div>
        <div class='card-st'>
          <div class='sr'><div class='sr-main'>
            <div class='sr-tile'><svg viewBox='0 0 16 16'><path d='M4 5h8M4 8h8M4 11h5'/></svg></div>
            <div class='sr-words'><div class='sr-title'>Roomier tabs</div><div class='sr-detail'>Taller rows and larger titles in the column</div></div>
            <div class='sr-control'>" + Sw("RoomierTabs", p.RoomierTabs) + @"</div>
          </div></div>
          <div class='st-rule'></div>
          <div class='sr'><div class='sr-main'>
            <div class='sr-tile'><svg viewBox='0 0 16 16'><rect x='2.2' y='3' width='11.6' height='10' rx='2'/><path d='M3 6.5h10'/></svg></div>
            <div class='sr-words'><div class='sr-title'>Buttons at the top</div><div class='sr-detail'>Bookmarks, extensions and downloads beside back, forward and reload, when they fit</div></div>
            <div class='sr-control'>" + Sw("ButtonsAtTop", p.ButtonsAtTop) + @"</div>
          </div></div>
          <div class='st-rule'></div>
          <div class='sr'>
            <div class='sr-main'>
              <div class='sr-tile'><svg viewBox='0 0 16 16'><path d='M3 8h10M3 8l3-3M3 8l3 3'/></svg></div>
              <div class='sr-words'><div class='sr-title'>Back, forward and reload " + Info("Choose visible navigation buttons") + @"</div><div class='sr-detail'>Untick any you reach with the keys: the tabs get the room</div></div>
            </div>
            <div class='sr-wide'><div class='st-chks'>" + Chk("ShowBackBtn", "Back", p.ShowBackBtn) + Chk("ShowForwardBtn", "Forward", p.ShowForwardBtn) + Chk("ShowReloadBtn", "Reload", p.ShowReloadBtn) + @"</div></div>
          </div>
          <div class='st-rule'></div>
          <div class='sr'>
            <div class='sr-main'>
              <div class='sr-tile'><svg viewBox='0 0 16 16'><rect x='2.5' y='2.5' width='4.5' height='4.5' rx='1'/><rect x='9' y='2.5' width='4.5' height='4.5' rx='1'/><rect x='2.5' y='9' width='4.5' height='4.5' rx='1'/><rect x='9' y='9' width='4.5' height='4.5' rx='1'/></svg></div>
              <div class='sr-words'><div class='sr-title'>Buttons under the column " + Info("Footer buttons in tab column") + @"</div><div class='sr-detail'>Untick extensions, bookmarks or your profile to hide its button</div></div>
            </div>
            <div class='sr-wide'><div class='st-chks'>" + Chk("ShowExtBtn", "Extensions", p.ShowExtBtn) + Chk("ShowBookmarksBtn", "Bookmarks", p.ShowBookmarksBtn) + Chk("ShowProfileBtn", "Profile", p.ShowProfileBtn) + @"</div></div>
          </div>
          <div class='st-rule'></div>
          <div class='sr'><div class='sr-main'>
            <div class='sr-tile'><svg viewBox='0 0 16 16'><rect x='2.2' y='3' width='11.6' height='10' rx='2'/><path d='M6 3v10'/></svg></div>
            <div class='sr-words'><div class='sr-title'>Sidebar button " + Info("Toggle sidebar button in title bar") + @"</div><div class='sr-detail'>Beside the window's buttons, to hide the sidebar as Ctrl+S does</div></div>
            <div class='sr-control'>" + Sw("ShowSidebarBtn", p.ShowSidebarBtn) + @"</div>
          </div></div>
          <div class='st-rule'></div>
          <div class='sr'><div class='sr-main'>
            <div class='sr-tile'><svg viewBox='0 0 16 16'><rect x='2.2' y='3' width='11.6' height='10' rx='2'/><path d='M5 3v10'/></svg></div>
            <div class='sr-words'><div class='sr-title'>Narrow to icons " + Info("Auto-collapse when resized below 200px") + @"</div><div class='sr-detail'>Pulled narrower than 200 pixels, the sidebar becomes a strip of site icons and its buttons</div></div>
            <div class='sr-control'>" + Sw("NarrowToIcons", p.NarrowToIcons) + @"</div>
          </div></div>
          <div class='st-rule'></div>
          <div class='sr'><div class='sr-main'>
            <div class='sr-tile'><svg viewBox='0 0 16 16'><circle cx='8' cy='8' r='5.5'/><path d='M6.5 5.5l4 2.5-4 2.5z'/></svg></div>
            <div class='sr-words'><div class='sr-title'>What's playing in other tabs " + Info("Media mini-player at bottom of sidebar") + @"</div><div class='sr-detail'>At the foot of the sidebar, to pause it, play it again or go to it</div></div>
            <div class='sr-control'>" + Sw("ShowPlayingInOtherTabs", p.ShowPlayingInOtherTabs) + @"</div>
          </div></div>
          <div class='st-rule'></div>
          <div class='sr'><div class='sr-main'>
            <div class='sr-tile'><svg viewBox='0 0 16 16'><rect x='2.5' y='3.5' width='11' height='9' rx='1.5'/></svg></div>
            <div class='sr-words'><div class='sr-title'>Tabs show " + Info("Monogram letters or site favicons") + @"</div><div class='sr-detail'>A letter or the site's icon</div></div>
            <div class='sr-control'>" + Seg("TabsShowMode", p.TabsShowMode, "Letters", "Site icons") + @"</div>
          </div></div>
          <div class='st-rule'></div>
          <div class='sr'><div class='sr-main'>
            <div class='sr-tile'><svg viewBox='0 0 16 16'><path d='M5 12V4h3.5a2 2 0 0 1 0 4H5m0 0h4a2 2 0 0 1 0 4H5'/></svg></div>
            <div class='sr-words'><div class='sr-title'>High contrast text</div><div class='sr-detail'>Tab titles in black or white, instead of soft grey</div></div>
            <div class='sr-control'>" + Sw("HighContrastTabText", p.HighContrastTabText) + @"</div>
          </div></div>
          <div class='st-rule'></div>
          <div class='sr'><div class='sr-main'>
            <div class='sr-tile'><svg viewBox='0 0 16 16'><path d='M3 5h10M3 8h7M3 11h4'/></svg></div>
            <div class='sr-words'><div class='sr-title'>Show how far you've read</div><div class='sr-detail'>The tab you're on fills with grey as you scroll down</div></div>
            <div class='sr-control'>" + Sw("ShowReadingProgress", p.ShowReadingProgress) + @"</div>
          </div></div>
          <div class='st-rule'></div>
          <div class='sr'><div class='sr-main'>
            <div class='sr-tile'><svg viewBox='0 0 16 16'><rect x='3' y='3' width='10' height='10' rx='2'/><circle cx='12' cy='4' r='2'/></svg></div>
            <div class='sr-words'><div class='sr-title'>Show counts on pins</div><div class='sr-detail'>A pinned page's unread count, from its title, as a badge on its icon</div></div>
            <div class='sr-control'>" + Sw("ShowCountsOnPins", p.ShowCountsOnPins) + @"</div>
          </div></div>
          <div class='st-rule'></div>
          <div class='sr'><div class='sr-main'>
            <div class='sr-tile'><svg viewBox='0 0 16 16'><path d='M6 3v7l2-1.5L10 13l1.5-.7-2-4.5L12 7z'/></svg></div>
            <div class='sr-words'><div class='sr-title'>Tabs give when pressed</div><div class='sr-detail'>A tab sinks a little under the pointer, as in Arc</div></div>
            <div class='sr-control'>" + Sw("TabsGiveWhenPressed", p.TabsGiveWhenPressed) + @"</div>
          </div></div>
          <div class='st-rule'></div>
          <div class='sr'><div class='sr-main'>
            <div class='sr-tile'><svg viewBox='0 0 16 16'><path d='M4 3h8v10l-4-2.5L4 13z'/></svg></div>
            <div class='sr-words'><div class='sr-title'>Show the bookmarks bar</div><div class='sr-detail'>Your bookmarks in a row above the page</div></div>
            <div class='sr-control'>" + Sw("ShowBookmarksBar", p.ShowBookmarksBar) + @"</div>
          </div></div>
        </div>
      </div>

      <div class='st-group'>
        <div class='st-group-head'>MOTION</div>
        <div class='card-st'>
          <div class='sr'><div class='sr-main'>
            <div class='sr-tile'><svg viewBox='0 0 16 16'><path d='M9 2L4.5 9H8l-1 5L11.5 7H8z'/></svg></div>
            <div class='sr-words'><div class='sr-title'>Save power with Energy Saver " + Info("Reduces background activity on battery") + @"</div><div class='sr-detail'>Simpler motion on battery; idle tabs sleep sooner with Energy Saver on</div></div>
            <div class='sr-control'>" + Sw("EnergySaver", p.EnergySaver) + @"</div>
          </div></div>
          <div class='st-rule'></div>
          <div class='sr'><div class='sr-main'>
            <div class='sr-tile'><svg viewBox='0 0 16 16'><circle cx='8' cy='8' r='5'/></svg></div>
            <div class='sr-words'><div class='sr-title'>Reduce animations</div><div class='sr-detail'>Switching tabs, spaces and panels happens at once, without sliding or fading</div></div>
            <div class='sr-control'>" + Sw("ReduceAnimations", p.ReduceAnimations) + @"</div>
          </div></div>
        </div>
      </div>
    </div>

    <!-- 3. THEMES -->
    <div class='st-page' id='sec-themes'>
      <div class='st-head'>
        <svg viewBox='0 0 16 16'><rect x='2.2' y='3' width='11.6' height='10' rx='2'/><circle cx='5.8' cy='6.2' r='1.1'/><path d='m3 11.5 3.5-3.2 2.5 2.2 2-1.8 2.5 2.5'/></svg>
        <h1>Themes</h1>
      </div>

      <div class='st-group'>
        <div class='st-group-head'>HOME BACKGROUND</div>
        <div class='card-st'>
          <div class='sr'>
            <div class='sr-main'>
              <div class='sr-tile'><svg viewBox='0 0 16 16'><rect x='2.2' y='3' width='11.6' height='10' rx='2'/><circle cx='5.8' cy='6.2' r='1.1'/><path d='m3 11.5 3.5-3.2 2.5 2.2 2-1.8 2.5 2.5'/></svg></div>
              <div class='sr-words'><div class='sr-title'>Home background " + Info("Customize the New Tab canvas") + @"</div><div class='sr-detail'>Paint the blank tab: a wall, stickers, a colour, a gradient or your own picture</div></div>
            </div>
            <div class='sr-wide'>
              <div class='home-preview'>
                <div class='home-preview-tabs'><div class='home-preview-tab'></div><div class='home-preview-tab'></div><div class='home-preview-tab'></div><div class='home-preview-tab'></div></div>
                <div class='home-preview-pill'>Search or enter address</div>
              </div>
              <div style='margin-top:10px'>" + Seg("BlankWallMode", wallMode, "Off", "Wall painting", "Stickers", "Solid colour", "Gradient", "Image") + @"</div>
              " + wallSubControls + @"
              <div class='sr-detail' style='margin-top:8px'>" + wallDesc + @"</div>
            </div>
          </div>
          <div class='st-rule'></div>
          <div class='sr'>
            <div class='sr-main'>
              <div class='sr-tile'><svg viewBox='0 0 16 16'><rect x='2.2' y='3' width='11.6' height='10' rx='2'/></svg></div>
              <div class='sr-words'><div class='sr-title'>Window buttons</div><div class='sr-detail'>Inline, as doors beside the tabs, or Classic, in the corner as in Windows</div></div>
            </div>
            <div class='sr-wide'>
              <div class='st-pics'>
                <div class='st-pic " + (p.WindowButtonsStyle == "Inline" ? "on" : "") + @"' onclick=""setPref('WindowButtonsStyle','Inline')"">
                  <div class='st-pic-ring'><span class='st-pic-frame'><span style='position:absolute;left:6px;top:6px;width:4px;height:4px;border-radius:50%;background:#ef4444'></span><span style='position:absolute;left:12px;top:6px;width:4px;height:4px;border-radius:50%;background:#eab308'></span><span style='position:absolute;left:18px;top:6px;width:4px;height:4px;border-radius:50%;background:#22c55e'></span></span></div>
                  <span class='st-pic-label'>Inline</span>
                </div>
                <div class='st-pic " + (p.WindowButtonsStyle == "Classic" ? "on" : "") + @"' onclick=""setPref('WindowButtonsStyle','Classic')"">
                  <div class='st-pic-ring'><span class='st-pic-frame'><span style='position:absolute;right:5px;top:5px;width:16px;height:5px;border-radius:2px;background:var(--faint)'></span></span></div>
                  <span class='st-pic-label'>Classic</span>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- 4. TABS -->
    <div class='st-page' id='sec-tabs'>
      <div class='st-head'>
        <svg viewBox='0 0 16 16'><rect x='2.2' y='3' width='11.6' height='10' rx='2'/><path d='M6 3v10'/></svg>
        <h1>Tabs</h1>
      </div>

      <div class='st-group'>
        <div class='st-group-head'>NEW TABS</div>
        <div class='card-st'>
          <div class='sr'><div class='sr-main'>
            <div class='sr-tile'><svg viewBox='0 0 16 16'><path d='M3 8h9M9 5l3 3-3 3'/></svg></div>
            <div class='sr-words'><div class='sr-title'>New tab position " + Info("Where Ctrl+T opens") + @"</div><div class='sr-detail'>Where a link or Ctrl+T's new tab goes in the row</div></div>
            <div class='sr-control'>" + Sel("NewTabPosition", p.NewTabPosition, "After current", "At end") + @"</div>
          </div></div>
          <div class='st-rule'></div>
          <div class='sr'><div class='sr-main'>
            <div class='sr-tile'><svg viewBox='0 0 16 16'><path d='M9 4l3 3-3 3M4 11V7h8'/></svg></div>
            <div class='sr-words'><div class='sr-title'>Links opened in front go beside their page " + Info("Child tabs open adjacent") + @"</div><div class='sr-detail'>A link that opens a new tab lands right after the tab it came from</div></div>
            <div class='sr-control'>" + Sw("LinksInFrontBesidePage", p.LinksInFrontBesidePage) + @"</div>
          </div></div>
          <div class='st-rule'></div>
          <div class='sr'><div class='sr-main'>
            <div class='sr-tile'><svg viewBox='0 0 16 16'><rect x='2.5' y='3.5' width='11' height='9' rx='1.5'/></svg></div>
            <div class='sr-words'><div class='sr-title'>Leave a new tab when the last tab closes</div><div class='sr-detail'>Otherwise you land on a pinned tab</div></div>
            <div class='sr-control'>" + Sw("LeaveNewTabOnLastClose", p.LeaveNewTabOnLastClose) + @"</div>
          </div></div>
          <div class='st-rule'></div>
          <div class='sr'><div class='sr-main'>
            <div class='sr-tile'><svg viewBox='0 0 16 16'><path d='M10 4L6 8l4 4'/></svg></div>
            <div class='sr-words'><div class='sr-title'>Closing a tab goes back to the one before " + Info("MRU tab focus on close") + @"</div><div class='sr-detail'>Otherwise you land on the tab beside it</div></div>
            <div class='sr-control'>" + Sw("CloseTabGoesToPrevious", p.CloseTabGoesToPrevious) + @"</div>
          </div></div>
          <div class='st-rule'></div>
          <div class='sr'><div class='sr-main'>
            <div class='sr-tile'><svg viewBox='0 0 16 16'><path d='M10 4L6 8l4 4'/></svg></div>
            <div class='sr-words'><div class='sr-title'>Swipe back closes a tab a link opened " + Info("Back at history start closes child tab") + @"</div><div class='sr-detail'>Back to the tab you were on, as Safari and Arc do</div></div>
            <div class='sr-control'>" + Sw("SwipeBackClosesOpenedTab", p.SwipeBackClosesOpenedTab) + @"</div>
          </div></div>
          <div class='st-rule'></div>
          <div class='sr'><div class='sr-main'>
            <div class='sr-tile'><svg viewBox='0 0 16 16'><rect x='2.5' y='2.5' width='4.5' height='4.5' rx='1'/><rect x='9' y='2.5' width='4.5' height='4.5' rx='1'/><rect x='2.5' y='9' width='4.5' height='4.5' rx='1'/><rect x='9' y='9' width='4.5' height='4.5' rx='1'/></svg></div>
            <div class='sr-words'><div class='sr-title'>Show the Home bookmarks on a new tab " + Info("Display quick links under the address pill") + @"</div><div class='sr-detail'>The sites of your bookmark folder called Home, under the address field</div></div>
            <div class='sr-control'>" + Sw("ShowHomeBookmarksOnNewTab", p.ShowHomeBookmarksOnNewTab) + @"</div>
          </div></div>
          <div class='st-rule'></div>
          <div class='sr'><div class='sr-main'>
            <div class='sr-tile'><svg viewBox='0 0 16 16'><path d='M4 7a4.5 4.5 0 1 1 1 4M4 4v3h3'/></svg></div>
            <div class='sr-words'><div class='sr-title'>Ctrl+Z reopens a closed tab " + Info("Also works alongside Ctrl+Shift+T") + @"</div><div class='sr-detail'>Right after closing it, as Ctrl+Shift+T would bring it back</div></div>
            <div class='sr-control'>" + Sw("CtrlZReopensClosedTab", p.CtrlZReopensClosedTab) + @"</div>
          </div></div>
          <div class='st-rule'></div>
          <div class='sr'><div class='sr-main'>
            <div class='sr-tile'><svg viewBox='0 0 16 16'><path d='M3 8h9M9 5l3 3-3 3'/></svg></div>
            <div class='sr-words'><div class='sr-title'>Ctrl+Tab goes to a link opened behind " + Info("Focus background-opened child tab") + @"</div><div class='sr-detail'>Otherwise a quick Ctrl+Tab goes back to the tab you were on before</div></div>
            <div class='sr-control'>" + Sw("CtrlTabGoesToBackgroundLink", p.CtrlTabGoesToBackgroundLink) + @"</div>
          </div></div>
          <div class='st-rule'></div>
          <div class='sr'><div class='sr-main'>
            <div class='sr-tile'><svg viewBox='0 0 16 16'><rect x='2.5' y='3.5' width='11' height='9' rx='1.5'/></svg></div>
            <div class='sr-words'><div class='sr-title'>The Ctrl+Tab switcher leaves out pinned tabs " + Info("Exclude pins from Ctrl+Tab") + @"</div><div class='sr-detail'>Otherwise pinned tabs appear alongside regular tabs</div></div>
            <div class='sr-control'>" + Sw("CtrlTabSkipsPinned", p.CtrlTabSkipsPinned) + @"</div>
          </div></div>
          <div class='st-rule'></div>
          <div class='sr'><div class='sr-main'>
            <div class='sr-tile'><svg viewBox='0 0 16 16'><path d='M5 3h6M5 13h6M6 3c0 3 4 3 4 5s-4 2-4 5M10 3c0 3-4 3-4 5s4 2 4 5'/></svg></div>
            <div class='sr-words'><div class='sr-title'>Load background tabs when you go to them " + Info("Lazy-load middle-clicked links") + @"</div><div class='sr-detail'>A link opened behind the page waits until you go to its tab</div></div>
            <div class='sr-control'>" + Sw("LoadBackgroundTabsOnSelect", p.LoadBackgroundTabsOnSelect) + @"</div>
          </div></div>
          <div class='st-rule'></div>
          <div class='sr'><div class='sr-main'>
            <div class='sr-tile'><svg viewBox='0 0 16 16'><rect x='2.5' y='3.5' width='11' height='9' rx='1.5'/></svg></div>
            <div class='sr-words'><div class='sr-title'>Open sized pop-ups in small windows " + Info("OAuth and sized popups") + @"</div><div class='sr-detail'>A site asking for a window at its own size gets one, not a tab</div></div>
            <div class='sr-control'>" + Sw("OpenSizedPopupsInSmallWindows", p.OpenSizedPopupsInSmallWindows) + @"</div>
          </div></div>
        </div>
      </div>

      <div class='st-group'>
        <div class='st-group-head'>MEMORY</div>
        <div class='card-st'>
          <div class='sr'>
            <div class='sr-main'>
              <div class='sr-tile'><svg viewBox='0 0 16 16'><path d='M10.5 3.2a5 5 0 1 0 2.3 9.3 4.5 4.5 0 0 1-2.3-9.3z'/></svg></div>
              <div class='sr-words'><div class='sr-title'>Sleep tabs you aren't using " + Info("Frees RAM by suspending inactive WebViews") + @"</div><div class='sr-detail'>A resting tab lets its page go and comes back where you left it</div></div>
              <div class='sr-control'><button class='pill-st' onclick=""act('sleepAllNow')"">Sleep now</button></div>
            </div>
            <div class='sr-wide'>
              " + Seg("SleepLevel", p.SleepLevel, "Never", "Later", "Normal", "Sooner") + @"
              <div class='sr-detail' style='margin-top:6px'>" + sleepDetail + @"</div>
            </div>
          </div>
          <div class='st-rule'></div>
          <div class='sr'><div class='sr-main'>
            <div class='sr-tile'><svg viewBox='0 0 16 16'><path d='M10.5 3.2a5 5 0 1 0 2.3 9.3 4.5 4.5 0 0 1-2.3-9.3z'/></svg></div>
            <div class='sr-words'><div class='sr-title'>Dim sleeping tabs</div><div class='sr-detail'>A resting tab's title and icon fade until you go to it</div></div>
            <div class='sr-control'>" + Sw("DimSleepingTabs", p.DimSleepingTabs) + @"</div>
          </div></div>
          <div class='st-rule'></div>
          <div class='sr'><div class='sr-main'>
            <div class='sr-tile'><svg viewBox='0 0 16 16'><circle cx='8' cy='8' r='5.5'/><path d='M8 4.5v3.8l2.5 1.5'/></svg></div>
            <div class='sr-words'><div class='sr-title'>Close tabs you leave alone " + Info("Auto-archive old unpinned tabs") + @"</div><div class='sr-detail'>Pinned, named and grouped tabs stay. Ctrl+Shift+T brings one back</div></div>
            <div class='sr-control'>" + Sel("CloseIdleTabs", p.CloseIdleTabs, "Never", "After 12 hours", "After 1 day", "After 7 days") + @"</div>
          </div></div>
        </div>
      </div>

      <div class='st-group'>
        <div class='st-group-head'>IN THE TAB BAR</div>
        <div class='card-st'>
          <div class='sr'><div class='sr-main'>
            <div class='sr-tile'><svg viewBox='0 0 16 16'><path d='M5 3v10M11 3v10M3 6h10M3 10h10'/></svg></div>
            <div class='sr-words'><div class='sr-title'>Show tab numbers while Ctrl is held</div><div class='sr-detail'>Which tab each Ctrl+1 to 9 selects</div></div>
            <div class='sr-control'>" + Sw("ShowTabNumbersOnCtrl", p.ShowTabNumbersOnCtrl) + @"</div>
          </div></div>
          <div class='st-rule'></div>
          <div class='sr'><div class='sr-main'>
            <div class='sr-tile'><svg viewBox='0 0 16 16'><rect x='3.5' y='7' width='9' height='6' rx='1.5'/><path d='M5.5 7V5a2.5 2.5 0 0 1 5 0v2'/></svg></div>
            <div class='sr-words'><div class='sr-title'>Show the address on the tab you're on " + Info("Displays domain/URL on active tab") + @"</div><div class='sr-detail'>Its site in place of its title; the title comes back under the pointer</div></div>
            <div class='sr-control'>" + Sw("ShowAddressOnActiveTab", p.ShowAddressOnActiveTab) + @"</div>
          </div></div>
          <div class='st-rule'></div>
          <div class='sr'><div class='sr-main'>
            <div class='sr-tile'><svg viewBox='0 0 16 16'><path d='M3 12l2.5-7 2.5 7M4 10h3M11 5v7'/></svg></div>
            <div class='sr-words'><div class='sr-title'>Clicking the tab you're on edits its address only " + Info("Inline URL editing on active tab") + @"</div><div class='sr-detail'>Without the site card under it</div></div>
            <div class='sr-control'>" + Sw("ClickActiveTabEditsAddress", p.ClickActiveTabEditsAddress) + @"</div>
          </div></div>
        </div>
      </div>

      <div class='st-group'>
        <div class='st-group-head'>WINDOWS</div>
        <div class='card-st'>
          <div class='sr'><div class='sr-main'>
            <div class='sr-tile'><svg viewBox='0 0 16 16'><rect x='2.5' y='3.5' width='11' height='9' rx='1.5'/></svg></div>
            <div class='sr-words'><div class='sr-title'>Same tabs in every window " + Info("Sync open tabs across windows") + @"</div><div class='sr-detail'>Each window lists the same open tabs in a space, as it lists the same pins</div></div>
            <div class='sr-control'>" + Sw("SameTabsInEveryWindow", p.SameTabsInEveryWindow) + @"</div>
          </div></div>
        </div>
      </div>
    </div>

    <!-- 5. BROWSING -->
    <div class='st-page' id='sec-browsing'>
      <div class='st-head'>
        <svg viewBox='0 0 16 16'><circle cx='8' cy='8' r='5.6'/><path d='M2.5 8h11M8 2.4c1.6 1.6 2.4 3.5 2.4 5.6S9.6 12 8 13.6C6.4 12 5.6 10.1 5.6 8S6.4 4 8 2.4'/></svg>
        <h1>Browsing</h1>
      </div>

      <div class='st-group'>
        <div class='st-group-head'>SEARCHING</div>
        <div class='card-st'>
          <div class='sr'><div class='sr-main'>
            <div class='sr-tile'><svg viewBox='0 0 16 16'><circle cx='7' cy='7' r='4.2'/><path d='m10.2 10.2 3.2 3.2'/></svg></div>
            <div class='sr-words'><div class='sr-title'>Search with</div><div class='sr-detail'>Where words that aren't an address go</div></div>
            <div class='sr-control'>" + Sel("SearchEngineName", p.SearchEngineName, "DuckDuckGo", "Google", "Brave Search") + @"</div>
          </div></div>
          <div class='st-rule'></div>
          <div class='sr'><div class='sr-main'>
            <div class='sr-tile'><svg viewBox='0 0 16 16'><path d='M8 12V4M5 7l3-3 3 3'/></svg></div>
            <div class='sr-words'><div class='sr-title'>Shift-Return searches with " + Info("Secondary search engine on Shift+Enter") + @"</div><div class='sr-detail'>Off: Shift-Return does what Return does</div></div>
            <div class='sr-control'>" + Sel("ShiftReturnEngine", p.ShiftReturnEngine, "Off", "Google", "DuckDuckGo", "Brave Search") + @"</div>
          </div></div>
          <div class='st-rule'></div>
          <div class='sr'><div class='sr-main'>
            <div class='sr-tile'><svg viewBox='0 0 16 16'><path d='M3 5h10M3 8h10M3 11h6'/></svg></div>
            <div class='sr-words'><div class='sr-title'>Suggestions from your search engine " + Info("Live autocomplete suggestions") + @"</div><div class='sr-detail'>What you type is sent to your search engine as you type</div></div>
            <div class='sr-control'>" + Sw("SearchSuggestions", p.SearchSuggestions) + @"</div>
          </div></div>
          <div class='st-rule'></div>
          <div class='sr'><div class='sr-main'>
            <div class='sr-tile'><svg viewBox='0 0 16 16'><path d='M3 12l2.5-7 2.5 7M4 10h3M11 5v7'/></svg></div>
            <div class='sr-words'><div class='sr-title'>Site shortcuts " + Info("Bang-style keyword shortcuts") + @"</div><div class='sr-detail'>A word before your search sends it to that site: &quot;yt cats&quot;</div></div>
            <div class='sr-control'><button class='pill-st' onclick=""act('manageSiteShortcuts')"">Add</button></div>
          </div></div>
        </div>
      </div>

      <div class='st-group'>
        <div class='st-group-head'>LINKS</div>
        <div class='card-st'>
          <div class='sr'><div class='sr-main'>
            <div class='sr-tile'><svg viewBox='0 0 16 16'><rect x='2.5' y='3.5' width='11' height='9' rx='1.5'/></svg></div>
            <div class='sr-words'><div class='sr-title'>Open links from other apps in a small window " + Info("Little Arc style quick preview") + @"</div><div class='sr-detail'>To read and close, or keep with Open in Search (Ctrl+O); a tab's menu opens one too</div></div>
            <div class='sr-control'>" + Sw("OpenExternalInSmallWindow", p.OpenExternalInSmallWindow) + @"</div>
          </div></div>
          <div class='st-rule'></div>
          <div class='sr'><div class='sr-main'>
            <div class='sr-tile'><svg viewBox='0 0 16 16'><path d='M3 8h9M9 5l3 3-3 3'/></svg></div>
            <div class='sr-words'><div class='sr-title'>Go to a tab that has the page</div><div class='sr-detail'>A link to a page already open in a tab goes to that tab, not a small window</div></div>
            <div class='sr-control'>" + Sw("GoToExistingTab", p.GoToExistingTab) + @"</div>
          </div></div>
        </div>
      </div>

      <div class='st-group'>
        <div class='st-group-head'>READING MODE</div>
        <div class='card-st'>
          <div class='sr'><div class='sr-main'>
            <div class='sr-tile'><svg viewBox='0 0 16 16'><path d='M3 3.5h10v9H3zM5 6.5h6M5 9h4'/></svg></div>
            <div class='sr-words'><div class='sr-title'>Read the site's own Markdown " + Info("Prefer .md if published") + @"</div><div class='sr-detail'>When a page has a Markdown copy, reading mode shows that</div></div>
            <div class='sr-control'>" + Sw("ReadSiteMarkdown", p.ReadSiteMarkdown) + @"</div>
          </div></div>
          <div class='st-rule'></div>
          <div class='sr'><div class='sr-main'>
            <div class='sr-tile'><svg viewBox='0 0 16 16'><circle cx='8' cy='8' r='4.5'/></svg></div>
            <div class='sr-words'><div class='sr-title'>Reading mode follows light and dark " + Info("Match browser theme in Reader View") + @"</div><div class='sr-detail'>In a dark window the article is light on dark</div></div>
            <div class='sr-control'>" + Sw("ReadingModeFollowsTheme", p.ReadingModeFollowsTheme) + @"</div>
          </div></div>
        </div>
      </div>

      <div class='st-group'>
        <div class='st-group-head'>VIDEO</div>
        <div class='card-st'>
          <div class='sr'><div class='sr-main'>
            <div class='sr-tile'><svg viewBox='0 0 16 16'><rect x='2.5' y='3.5' width='11' height='9' rx='1.5'/><rect x='8' y='7.5' width='4.5' height='3.5' rx='0.8'/></svg></div>
            <div class='sr-words'><div class='sr-title'>The floating video " + Info("Picture-in-Picture style") + @"</div><div class='sr-detail'>Search's window, or Chromium's picture in picture</div></div>
            <div class='sr-control'>" + Seg("FloatingVideoHost", p.FloatingVideoHost, "Search's window", "Picture in picture") + @"</div>
          </div></div>
          <div class='st-rule'></div>
          <div class='sr'>
            <div class='sr-main'>
              <div class='sr-tile'><svg viewBox='0 0 16 16'><rect x='2.5' y='3.5' width='11' height='9' rx='1.5'/></svg></div>
              <div class='sr-words'><div class='sr-title'>Float the video on its own " + Info("Auto-PiP when switching tabs") + @"</div><div class='sr-detail'>A playing video comes out when you leave its tab, or Search</div></div>
            </div>
            <div class='sr-wide'>" + Seg("AutoFloatVideo", p.AutoFloatVideo, "Never", "Switching tabs", "Tabs or apps") + @"</div>
          </div>
          <div class='st-rule'></div>
          <div class='sr'><div class='sr-main'>
            <div class='sr-tile'><svg viewBox='0 0 16 16'><rect x='2.5' y='3.5' width='11' height='9' rx='1.5'/></svg></div>
            <div class='sr-words'><div class='sr-title'>Minimize the floating video</div><div class='sr-detail'>A button tucks the floating video small. The same button brings it back</div></div>
            <div class='sr-control'>" + Sw("MinimizeFloatingVideo", p.MinimizeFloatingVideo) + @"</div>
          </div></div>
          <div class='st-rule'></div>
          <div class='sr'><div class='sr-main'>
            <div class='sr-tile'><svg viewBox='0 0 16 16'><path d='M3 5l8 3-8 3z'/></svg></div>
            <div class='sr-words'><div class='sr-title'>Videos wait for a click</div><div class='sr-detail'>Videos don't start by themselves, even without sound</div></div>
            <div class='sr-control'>" + Sw("VideosWaitForClick", p.VideosWaitForClick) + @"</div>
          </div></div>
          <div class='st-rule'></div>
          <div class='sr'><div class='sr-main'>
            <div class='sr-tile'><svg viewBox='0 0 16 16'><path d='M3 6V3h3M10 3h3v3M13 10v3h-3M6 13H3v-3'/></svg></div>
            <div class='sr-words'><div class='sr-title'>Press Esc twice to leave full screen</div><div class='sr-detail'>The first press shows a reminder, as in Arc</div></div>
            <div class='sr-control'>" + Sw("EscTwiceLeavesFullScreen", p.EscTwiceLeavesFullScreen) + @"</div>
          </div></div>
        </div>
      </div>

      <div class='st-group'>
        <div class='st-group-head'>PAGES</div>
        <div class='card-st'>
          <div class='sr'><div class='sr-main'>
            <div class='sr-tile'><svg viewBox='0 0 16 16'><path d='M3 4h6M6 3v1m-2.5 0c.5 2.5 2 4 4 5M7.5 4c-.5 2-1.5 3.5-3.5 5M9.5 13l2-5 2 5M10.2 11.2h2.6'/></svg></div>
            <div class='sr-words'><div class='sr-title'>Translation languages " + Info("Local page translation target") + @"</div><div class='sr-detail'>Pages are translated on this PC. Right-click a page to translate it into one of these (" + EscHtml(p.TranslateTargetLang) + @")</div></div>
            <div class='sr-control'>" + Sel("TranslateTargetLang", p.TranslateTargetLang, "en", "es", "fr", "de", "ja", "zh") + @"</div>
          </div></div>
          <div class='st-rule'></div>
          <div class='sr'><div class='sr-main'>
            <div class='sr-tile'><svg viewBox='0 0 16 16'><circle cx='7' cy='7' r='4.2'/><path d='m10.2 10.2 3.2 3.2M5.5 7h3M7 5.5v3'/></svg></div>
            <div class='sr-words'><div class='sr-title'>Page zoom</div><div class='sr-detail'>Where every site starts. Ctrl+Plus and Ctrl+Minus are kept for each site</div></div>
            <div class='sr-control'>
              <button class='pill-st' onclick=""act('zoomChange','-10')"">−</button>
              <span style='font-size:12px;min-width:38px;text-align:center'>" + p.DefaultZoomPercent + @"%</span>
              <button class='pill-st' onclick=""act('zoomChange','+10')"">+</button>
            </div>
          </div></div>
          <div class='st-rule'></div>
          <div class='sr'><div class='sr-main'>
            <div class='sr-tile'><svg viewBox='0 0 16 16'><rect x='2.5' y='3.5' width='11' height='9' rx='1.5'/><circle cx='8' cy='8' r='2'/></svg></div>
            <div class='sr-words'><div class='sr-title'>Capture pages " + Info("Screenshot area, element, or full page") + @"</div><div class='sr-detail'>Capture… in the View menu copies an area, an element or the whole page</div></div>
            <div class='sr-control'>" + Sw("CapturePagesEnabled", p.CapturePagesEnabled) + @"</div>
          </div></div>
          <div class='st-rule'></div>
          <div class='sr'><div class='sr-main'>
            <div class='sr-tile'><svg viewBox='0 0 16 16'><path d='M11.5 2.5a2.1 2.1 0 0 1 2 2L6 12l-3.5 1 1-3.5z'/></svg></div>
            <div class='sr-words'><div class='sr-title'>Your own style for a site " + Info("Custom CSS per domain") + @"</div><div class='sr-detail'>Edit Site Style… in the View menu puts CSS of yours on the site you're on</div></div>
            <div class='sr-control'>" + Sw("SiteStylesEnabled", p.SiteStylesEnabled) + @"</div>
          </div></div>
        </div>
      </div>
    </div>

    <!-- 6. SPACES & PINS -->
    <div class='st-page' id='sec-spaces'>
      <div class='st-head'>
        <svg viewBox='0 0 16 16'><rect x='2.5' y='2.5' width='4.5' height='4.5' rx='1.2'/><rect x='9' y='2.5' width='4.5' height='4.5' rx='1.2'/><rect x='2.5' y='9' width='4.5' height='4.5' rx='1.2'/><rect x='9' y='9' width='4.5' height='4.5' rx='1.2'/></svg>
        <h1>Spaces &amp; Pins</h1>
      </div>

      <div class='st-group'>
        <div class='st-group-head'>COMING FROM ARC?</div>
        <div class='card-st'>
          <div class='sr'><div class='sr-main'>
            <div class='sr-tile'><svg viewBox='0 0 16 16'><path d='M3 13L13 3M9 3h4v4'/></svg></div>
            <div class='sr-words'><div class='sr-title'>Set Up Like Arc " + Info("Apply Arc-style defaults") + @"</div><div class='sr-detail'>Tabs on the left, spaces, pinned rows, and Arc's habits, like closing tabs after 12 hours</div></div>
            <div class='sr-control'><button class='pill-st filled' onclick=""act('setupLikeArc')"">Set Up</button></div>
          </div></div>
          <div class='st-rule'></div>
          <div class='sr'><div class='sr-main'>
            <div class='sr-tile'><svg viewBox='0 0 16 16'><path d='M8 3v7M5.5 7.5 8 10l2.5-2.5M3 12.5h10'/></svg></div>
            <div class='sr-words'><div class='sr-title'>Bring Arc's spaces and pins over</div><div class='sr-detail'>Import spaces, pinned tabs, and bookmarks from Arc</div></div>
            <div class='sr-control'><button class='pill-st' onclick=""act('importBrowsers')"">Bring Over…</button></div>
          </div></div>
        </div>
      </div>

      <div class='st-group'>
        <div class='st-group-head'>SPACES AND PROFILES</div>
        <div class='card-st'>
          <div class='sr'><div class='sr-main'>
            <div class='sr-tile'><svg viewBox='0 0 16 16'><rect x='2.5' y='2.5' width='4.5' height='4.5' rx='1'/><rect x='9' y='2.5' width='4.5' height='4.5' rx='1'/><rect x='2.5' y='9' width='4.5' height='4.5' rx='1'/><rect x='9' y='9' width='4.5' height='4.5' rx='1'/></svg></div>
            <div class='sr-words'><div class='sr-title'>Spaces " + Info("Separate tab sets with Alt+1..3") + @"</div><div class='sr-detail'>Separate sets of tabs, switched with Alt+1 to Alt+9 or a swipe</div></div>
            <div class='sr-control'>
              <button class='pill-st' onclick=""act('switchSpace','0')"">Personal</button>
              <button class='pill-st' onclick=""act('switchSpace','1')"">Work</button>
              <button class='pill-st' onclick=""act('switchSpace','2')"">Research</button>
              " + Sw("SpacesEnabled", p.SpacesEnabled) + @"
            </div>
          </div></div>
          <div class='st-rule'></div>
          <div class='sr'><div class='sr-main'>
            <div class='sr-tile'><svg viewBox='0 0 16 16'><path d='M5 3v10M11 3v10M3 6h10M3 10h10'/></svg></div>
            <div class='sr-words'><div class='sr-title'>Ctrl+1 to Ctrl+9 switch spaces</div><div class='sr-detail'>Spaces take Ctrl+1 to Ctrl+9; tabs move to Alt+1 to Alt+9, as in Arc</div></div>
            <div class='sr-control'>" + Sw("CtrlNumbersSwitchSpaces", p.CtrlNumbersSwitchSpaces) + @"</div>
          </div></div>
          <div class='st-rule'></div>
          <div class='sr'><div class='sr-main'>
            <div class='sr-tile'><svg viewBox='0 0 16 16'><circle cx='8' cy='5.5' r='2.5'/><path d='M3.5 13.5c0-2.5 2-4 4.5-4s4.5 1.5 4.5 4'/></svg></div>
            <div class='sr-words'><div class='sr-title'>Profiles " + Info("Isolated cookie & session containers") + @"</div><div class='sr-detail'>Spaces with their own sign-ins, history and bookmarks</div></div>
            <div class='sr-control'><button class='pill-st' onclick=""act('newProfile')"">New Profile…</button></div>
          </div></div>
        </div>
      </div>

      <div class='st-group'>
        <div class='st-group-head'>PINS</div>
        <div class='card-st'>
          <div class='sr'><div class='sr-main'>
            <div class='sr-tile'><svg viewBox='0 0 16 16'><path d='M6 3h4l1 4 2 1.5V10H9v3.5l-1 1-1-1V10H3V8.5L5 7z'/></svg></div>
            <div class='sr-words'><div class='sr-title'>Pins keep their site</div><div class='sr-detail'>Links elsewhere open over the pin, and Ctrl+W takes it back home</div></div>
            <div class='sr-control'>" + Sw("PinsKeepSite", p.PinsKeepSite) + @"</div>
          </div></div>
          <div class='st-rule'></div>
          <div class='sr'><div class='sr-main'>
            <div class='sr-tile'><svg viewBox='0 0 16 16'><path d='M3 5h10M3 8h10M3 11h10'/></svg></div>
            <div class='sr-words'><div class='sr-title'>Pinned rows</div><div class='sr-detail'>Pins as rows under the squares, and Clear over the rest</div></div>
            <div class='sr-control'>" + Sw("PinnedRows", p.PinnedRows) + @"</div>
          </div></div>
          <div class='st-rule'></div>
          <div class='sr'><div class='sr-main'>
            <div class='sr-tile'><svg viewBox='0 0 16 16'><path d='M6 3v7l2-1.5L10 13l1.5-.7-2-4.5L12 7z'/></svg></div>
            <div class='sr-words'><div class='sr-title'>Ctrl-click on a pin opens a new tab</div><div class='sr-detail'>Opens a tab at the pin's site, or moves a wandered page to loose tabs</div></div>
            <div class='sr-control'>" + Sw("CtrlClickPinOpensNewTab", p.CtrlClickPinOpensNewTab) + @"</div>
          </div></div>
        </div>
      </div>

      <div class='st-group'>
        <div class='st-group-head'>OPENING AND STARTING</div>
        <div class='card-st'>
          <div class='sr'><div class='sr-main'>
            <div class='sr-tile'><svg viewBox='0 0 16 16'><rect x='2.5' y='4' width='11' height='8' rx='2'/></svg></div>
            <div class='sr-words'><div class='sr-title'>New tab in a floating field</div><div class='sr-detail'>Ctrl+T raises the address field over the page, as in Arc</div></div>
            <div class='sr-control'>" + Sw("NewTabInFloatingField", p.NewTabInFloatingField) + @"</div>
          </div></div>
          <div class='st-rule'></div>
          <div class='sr'><div class='sr-main'>
            <div class='sr-tile'><svg viewBox='0 0 16 16'><rect x='2.5' y='3.5' width='11' height='9' rx='1.5'/><path d='M5 7h6'/></svg></div>
            <div class='sr-words'><div class='sr-title'>The field in the middle of the window</div><div class='sr-detail'>Ctrl+T's and Ctrl+L's field over a page, centred on the window and higher up</div></div>
            <div class='sr-control'>" + Sw("FieldInMiddleOfWindow", p.FieldInMiddleOfWindow) + @"</div>
          </div></div>
          <div class='st-rule'></div>
          <div class='sr'><div class='sr-main'>
            <div class='sr-tile'><svg viewBox='0 0 16 16'><path d='M4 5l3 3-3 3M9 11h3'/></svg></div>
            <div class='sr-words'><div class='sr-title'>Address bar commands</div><div class='sr-detail'>&quot;settings&quot; or &quot;new tab&quot; alone in the address field goes there</div></div>
            <div class='sr-control'>" + Sw("AddressBarCommands", p.AddressBarCommands) + @"</div>
          </div></div>
          <div class='st-rule'></div>
          <div class='sr'><div class='sr-main'>
            <div class='sr-tile'><svg viewBox='0 0 16 16'><path d='M4 7a4.5 4.5 0 1 1 1 4M4 4v3h3'/></svg></div>
            <div class='sr-words'><div class='sr-title'>Start with a fresh window</div><div class='sr-detail'>Pins, groups and named tabs come back; last time's others don't</div></div>
            <div class='sr-control'>" + Sw("StartWithFreshWindow", p.StartWithFreshWindow) + @"</div>
          </div></div>
        </div>
      </div>
    </div>

    <!-- 7. PASSWORDS -->
    <div class='st-page' id='sec-passwords'>
      <div class='st-head'>
        <svg viewBox='0 0 16 16'><circle cx='5.5' cy='8' r='2.6'/><path d='M8.1 8h5.4M11.5 8v2.2M13.5 8v2.2'/></svg>
        <h1>Passwords</h1>
      </div>

      <div class='st-group'>
        <div class='st-group-head'>YOUR PASSWORDS</div>
        <div class='card-st'>
          <div class='sr'><div class='sr-main'>
            <div class='sr-tile'><svg viewBox='0 0 16 16'><circle cx='5.5' cy='8' r='2.6'/><path d='M8.1 8h5.4M11.5 8v2.2'/></svg></div>
            <div class='sr-words'><div class='sr-title'>Your passwords</div><div class='sr-detail'>Kept by Windows DPAPI, shown with Windows Hello</div></div>
            <div class='sr-control'><button class='pill-st' onclick=""act('openVaultDrawer')"">Open…</button></div>
          </div></div>
          <div class='st-rule'></div>
          <div class='sr'><div class='sr-main'>
            <div class='sr-tile'><svg viewBox='0 0 16 16'><circle cx='5.5' cy='8' r='2.6'/><path d='M8.1 8h5.4'/></svg></div>
            <div class='sr-words'><div class='sr-title'>Offer to save passwords</div><div class='sr-detail'>Asked once per site, never again for a site you refuse</div></div>
            <div class='sr-control'>" + Sw("OfferToSavePasswords", p.OfferToSavePasswords) + @"</div>
          </div></div>
          <div class='st-rule'></div>
          <div class='sr'><div class='sr-main'>
            <div class='sr-tile'><svg viewBox='0 0 16 16'><path d='M3 10h10M5 7h6'/></svg></div>
            <div class='sr-words'><div class='sr-title'>Fill in sign-ins</div><div class='sr-detail'>Click a sign-in box and your accounts for the site hang from it</div></div>
            <div class='sr-control'>" + Sw("FillInSignIns", p.FillInSignIns) + @"</div>
          </div></div>
          <div class='st-rule'></div>
          <div class='sr'><div class='sr-main'>
            <div class='sr-tile'><svg viewBox='0 0 16 16'><rect x='3.5' y='3.5' width='9' height='9' rx='1.5'/></svg></div>
            <div class='sr-words'><div class='sr-title'>Ask for Windows Hello before filling " + Info("Biometric/PIN gate before autofill") + @"</div><div class='sr-detail'>Or your PIN, before a saved password goes into a page</div></div>
            <div class='sr-control'>" + Sw("WindowsHelloForVault", p.WindowsHelloForVault) + @"</div>
          </div></div>
          <div class='st-rule'></div>
          <div class='sr'><div class='sr-main'>
            <div class='sr-tile'><svg viewBox='0 0 16 16'><circle cx='8' cy='5.5' r='2.5'/><path d='M3.5 13.5c0-2.5 2-4 4.5-4s4.5 1.5 4.5 4'/></svg></div>
            <div class='sr-words'><div class='sr-title'>Offer passkeys</div><div class='sr-detail'>Windows Hello or a passkey on your phone, on sites that offer one</div></div>
            <div class='sr-control'>" + Sw("OfferPasskeys", p.OfferPasskeys) + @"</div>
          </div></div>
        </div>
      </div>

      <div class='st-group'>
        <div class='st-group-head'>SAVED PASSWORDS (" + Store.Logins.Count + @")</div>
        <div class='card-st'>" + loginsHtml.ToString() + @"</div>
      </div>
    </div>

    <!-- 8. PRIVACY -->
    <div class='st-page' id='sec-privacy'>
      <div class='st-head'>
        <svg viewBox='0 0 16 16'><path d='M8 2.2 3.2 4.2v3.6c0 3.2 2 5.4 4.8 6.2 2.8-.8 4.8-3 4.8-6.2V4.2z'/></svg>
        <h1>Privacy</h1>
      </div>

      <div class='st-group'>
        <div class='st-group-head'>BLOCKING</div>
        <div class='card-st'>
          <div class='sr'><div class='sr-main'>
            <div class='sr-tile'><svg viewBox='0 0 16 16'><path d='M8 2.2 3.2 4.2v3.6c0 3.2 2 5.4 4.8 6.2 2.8-.8 4.8-3 4.8-6.2V4.2z'/></svg></div>
            <div class='sr-words'><div class='sr-title'>Block ads and trackers " + Info("Network-level blocker + YouTube ad skipper") + @"</div><div class='sr-detail'>Ads, trackers, pop-ups and harmful sites (" + Shield.BlockedCountTotal + @" blocked this session)</div></div>
            <div class='sr-control'>" + Sw("ShieldEnabled", p.ShieldEnabled) + @"</div>
          </div></div>
          <div class='st-rule'></div>
          <div class='sr'><div class='sr-main'>
            <div class='sr-tile'><svg viewBox='0 0 16 16'><path d='M8 2.2 3.2 4.2v3.6c0 3.2 2 5.4 4.8 6.2 2.8-.8 4.8-3 4.8-6.2V4.2z'/></svg></div>
            <div class='sr-words'><div class='sr-title'>Prevent cross-site tracking " + Info("Isolate third-party tracking") + @"</div><div class='sr-detail'>Sites inside others get no cookies. Private tabs keep it on</div></div>
            <div class='sr-control'>" + Sw("PreventCrossSiteTracking", p.PreventCrossSiteTracking) + @"</div>
          </div></div>
          <div class='st-rule'></div>
          <div class='sr'><div class='sr-main'>
            <div class='sr-tile'><svg viewBox='0 0 16 16'><path d='M6.5 9.5 9.5 6.5M5.5 7.5l-1.3 1.3a2.1 2.1 0 0 0 3 3l1.3-1.3'/></svg></div>
            <div class='sr-words'><div class='sr-title'>Remove click trackers from links " + Info("Strip fbclid, gclid, utm_*") + @"</div><div class='sr-detail'>gclid, fbclid and the like go before the page loads. Private tabs always</div></div>
            <div class='sr-control'>" + Sw("RemoveClickTrackers", p.RemoveClickTrackers) + @"</div>
          </div></div>
          <div class='st-rule'></div>
          <div class='sr'><div class='sr-main'>
            <div class='sr-tile'><svg viewBox='0 0 16 16'><path d='M4 8v4M7 5v7M10 6v6M13 8v4'/></svg></div>
            <div class='sr-words'><div class='sr-title'>Tell sites not to sell or share your data</div><div class='sr-detail'>Global Privacy Control, with every request. Private tabs always</div></div>
            <div class='sr-control'>" + Sw("SendGpc", p.SendGpc) + @"</div>
          </div></div>
          <div class='st-rule'></div>
          <div class='sr'><div class='sr-main'>
            <div class='sr-tile'><svg viewBox='0 0 16 16'><rect x='3.5' y='7' width='9' height='6' rx='1.5'/><path d='M5.5 7V5a2.5 2.5 0 0 1 5 0v2'/></svg></div>
            <div class='sr-words'><div class='sr-title'>Try HTTPS first " + Info("Upgrade http:// to https://") + @"</div><div class='sr-detail'>An http:// page loads over HTTPS where the site has it</div></div>
            <div class='sr-control'>" + Sw("TryHttpsFirst", p.TryHttpsFirst) + @"</div>
          </div></div>
        </div>
      </div>

      <div class='st-group'>
        <div class='st-group-head'>SITE PERMISSIONS</div>
        <div class='card-st'>
          <div class='sr'><div class='sr-main'>
            <div class='sr-tile'><svg viewBox='0 0 16 16'><path d='M8 2.5a3.5 3.5 0 0 1 3.5 3.5v3l1.5 2H3l1.5-2V6A3.5 3.5 0 0 1 8 2.5z'/></svg></div>
            <div class='sr-words'><div class='sr-title'>Let sites ask to send notifications</div><div class='sr-detail'>Only a site you allow reaches Windows' notifications</div></div>
            <div class='sr-control'>" + Sw("AllowNotifications", p.AllowNotifications) + @"</div>
          </div></div>
          <div class='st-rule'></div>
          <div class='sr'><div class='sr-main'>
            <div class='sr-tile'><svg viewBox='0 0 16 16'><circle cx='8' cy='8' r='4.5'/></svg></div>
            <div class='sr-words'><div class='sr-title'>Camera, microphone, location and notifications</div><div class='sr-detail'>What each site was allowed or refused</div></div>
            <div class='sr-control'><button class='pill-st' onclick=""act('forgetPermissions')"">Forget choices</button></div>
          </div></div>
        </div>
      </div>

      <div class='st-group'>
        <div class='st-group-head'>CLEAR</div>
        <div class='card-st'>
          <div class='sr'><div class='sr-main'>
            <div class='sr-tile'><svg viewBox='0 0 16 16'><circle cx='8' cy='8' r='5.5'/><path d='M8 4.5v3.8l2.5 1.5'/></svg></div>
            <div class='sr-words'><div class='sr-title'>History (" + Store.History.Count + @")</div><div class='sr-detail'>Every address you have been to</div></div>
            <div class='sr-control'><button class='pill-st' onclick=""act('clearHistory')"">Clear</button></div>
          </div></div>
          <div class='st-rule'></div>
          <div class='sr'><div class='sr-main'>
            <div class='sr-tile'><svg viewBox='0 0 16 16'><circle cx='8' cy='8' r='5.5'/><circle cx='6.5' cy='6.5' r='0.8'/><circle cx='9.5' cy='9' r='0.8'/></svg></div>
            <div class='sr-words'><div class='sr-title'>Cookies and sign-ins</div><div class='sr-detail'>Signs you out of every site</div></div>
            <div class='sr-control'><button class='pill-st' onclick=""act('clearCookies')"">Sign out of everything</button></div>
          </div></div>
          <div class='st-rule'></div>
          <div class='sr'><div class='sr-main'>
            <div class='sr-tile'><svg viewBox='0 0 16 16'><path d='M3 5h10v7H3zM5 3h6'/></svg></div>
            <div class='sr-words'><div class='sr-title'>Cache</div><div class='sr-detail'>Only what was fetched to draw pages</div></div>
            <div class='sr-control'><button class='pill-st' onclick=""act('clearCache')"">Clear</button></div>
          </div></div>
        </div>
      </div>

      <div class='st-group'>
        <div class='st-group-head'>PROXY</div>
        <div class='card-st'>
          <div class='sr'><div class='sr-main'>
            <div class='sr-tile'><svg viewBox='0 0 16 16'><circle cx='8' cy='8' r='5.5'/><path d='M3 8h10'/></svg></div>
            <div class='sr-words'><div class='sr-title'>Send everything through a proxy</div><div class='sr-detail'>Off: Search follows Windows' own network settings</div></div>
            <div class='sr-control'>" + Sel("ProxyMode", p.ProxyMode, "Off, or Windows' own", "Direct (No proxy)") + @"</div>
          </div></div>
        </div>
      </div>
    </div>

    <!-- 9. EXTENSIONS -->
    <div class='st-page' id='sec-extensions'>
      <div class='st-head'>
        <svg viewBox='0 0 16 16'><path d='M6 2.8a1.4 1.4 0 0 1 2.8 0v1h2.6v2.6h1a1.4 1.4 0 0 1 0 2.8h-1v3.2H8.6v-1a1.4 1.4 0 0 0-2.8 0v1H2.6V9.2h1a1.4 1.4 0 0 0 0-2.8h-1V3.8H6z'/></svg>
        <h1>Extensions</h1>
      </div>

      <div class='st-group'>
        <div class='card-st'>
          <div class='sr'>
            <div class='sr-main'>
              <div class='sr-tile'><svg viewBox='0 0 16 16'><path d='M6 2.8a1.4 1.4 0 0 1 2.8 0v1h2.6v2.6h1a1.4 1.4 0 0 1 0 2.8h-1v3.2H8.6v-1a1.4 1.4 0 0 0-2.8 0v1H2.6V9.2h1a1.4 1.4 0 0 0 0-2.8h-1V3.8H6z'/></svg></div>
              <div class='sr-words'><div class='sr-title'>Add from the Chrome Web Store</div></div>
              <div class='sr-control'><button class='pill-st' onclick=""act('openWebStore')"">Open the Store</button></div>
            </div>
            <div class='sr-wide'>
              <div style='display:flex;gap:8px;align-items:center'>
                <input id='extInput' class='st-input' type='text' placeholder='Paste a link to an extension, or its id' />
                <button class='pill-st filled' onclick=""act('installExt', document.getElementById('extInput').value)"">Add</button>
              </div>
              <div class='sr-detail' style='margin-top:6px'>Or find it in the store and press Add to Search on its page.</div>
            </div>
          </div>
          <div class='st-rule'></div>
          <div class='sr'><div class='sr-main'>
            <div class='sr-tile'><svg viewBox='0 0 16 16'><path d='M8 3v7M5.5 7.5 8 10l2.5-2.5M3 12.5h10'/></svg></div>
            <div class='sr-words'><div class='sr-title'>From another browser</div><div class='sr-detail'>What Chrome, Arc, Brave and the others added from the Chrome Web Store</div></div>
            <div class='sr-control'><button class='pill-st' onclick=""act('importBrowsers')"">Bring them over…</button></div>
          </div></div>
        </div>
      </div>

      <div class='st-group'>
        <div class='card-st'>" + extHtml.ToString() + @"</div>
      </div>

      <div class='st-group'>
        <div class='card-st'>
          <div class='sr'><div class='sr-main'>
            <div class='sr-tile'><svg viewBox='0 0 16 16'><path d='M2.5 5.2C2.5 4.3 3.2 3.6 4.1 3.6h2.7l1.4 1.5h3.7c.9 0 1.6.7 1.6 1.6v4.5c0 .9-.7 1.6-1.6 1.6H4.1c-.9 0-1.6-.7-1.6-1.6z'/></svg></div>
            <div class='sr-words'><div class='sr-title'>Load an unpacked extension</div><div class='sr-detail'>A folder with a manifest.json: your own, or one exported from another browser</div></div>
            <div class='sr-control'><button class='pill-st' onclick=""act('loadUnpackedExt')"">Choose…</button></div>
          </div></div>
        </div>
      </div>
    </div>

    <!-- 10. KEYBOARD & MOUSE -->
    <div class='st-page' id='sec-shortcuts'>
      <div class='st-head'>
        <svg viewBox='0 0 16 16'><rect x='2' y='4' width='12' height='8' rx='1.8'/><path d='M4.5 6.8h1M7.5 6.8h1M10.5 6.8h1M5 9.5h6'/></svg>
        <h1>Keyboard &amp; Mouse</h1>
      </div>

      <div class='st-group'>
        <div class='st-group-head'>MOUSE AND TRACKPAD</div>
        <div class='card-st'>
          <div class='sr'><div class='sr-main'>
            <div class='sr-tile'><svg viewBox='0 0 16 16'><circle cx='8' cy='8' r='4.5'/><circle cx='8' cy='8' r='1.5'/></svg></div>
            <div class='sr-words'><div class='sr-title'>Peek at a link " + Info("Floating card preview without leaving tab") + @"</div><div class='sr-detail'>Its page opens in a panel over the one you're reading</div></div>
            <div class='sr-control'>" + Sel("PeekTrigger", p.PeekTrigger, "Shift-click", "Alt-click", "Off") + @"</div>
          </div></div>
          <div class='st-rule'></div>
          <div class='sr'><div class='sr-main'>
            <div class='sr-tile'><svg viewBox='0 0 16 16'><rect x='2.5' y='3.5' width='11' height='9' rx='1.5'/></svg></div>
            <div class='sr-words'><div class='sr-title'>Peek stays with its tab</div><div class='sr-detail'>Another tab hides it, with its video and sound paused, and coming back shows it</div></div>
            <div class='sr-control'>" + Sw("PeekStaysWithTab", p.PeekStaysWithTab) + @"</div>
          </div></div>
          <div class='st-rule'></div>
          <div class='sr'><div class='sr-main'>
            <div class='sr-tile'><svg viewBox='0 0 16 16'><rect x='2.2' y='3' width='11.6' height='10' rx='2'/><path d='M8 3v10'/></svg></div>
            <div class='sr-words'><div class='sr-title'>Alt-click a link opens it in Split View " + Info("Side-by-side tab view") + @"</div><div class='sr-detail'>Its page beside the one you're reading, as Open Link in Split View</div></div>
            <div class='sr-control'>" + Sw("AltClickOpensSplitView", p.AltClickOpensSplitView) + @"</div>
          </div></div>
          <div class='st-rule'></div>
          <div class='sr'><div class='sr-main'>
            <div class='sr-tile'><svg viewBox='0 0 16 16'><path d='M8 3v10M5 6l3-3 3 3M5 10l3 3 3-3'/></svg></div>
            <div class='sr-words'><div class='sr-title'>Scroll with the middle button</div><div class='sr-detail'>Click the wheel, then move the mouse to scroll, as in Chrome</div></div>
            <div class='sr-control'>" + Sw("MiddleClickScroll", p.MiddleClickScroll) + @"</div>
          </div></div>
          <div class='st-rule'></div>
          <div class='sr'><div class='sr-main'>
            <div class='sr-tile'><svg viewBox='0 0 16 16'><path d='M8 3v10M5 6l3-3 3 3'/></svg></div>
            <div class='sr-words'><div class='sr-title'>Smooth scrolling with a mouse wheel " + Info("Interpolated wheel scrolling") + @"</div><div class='sr-detail'>Each notch of the wheel glides over a few frames, as in Chrome, instead of jumping</div></div>
            <div class='sr-control'>" + Sw("SmoothScrolling", p.SmoothScrolling) + @"</div>
          </div></div>
          <div class='st-rule'></div>
          <div class='sr'><div class='sr-main'>
            <div class='sr-tile'><svg viewBox='0 0 16 16'><path d='M4 11c2-5 6-5 8 0'/></svg></div>
            <div class='sr-words'><div class='sr-title'>Bounce at the top and bottom</div><div class='sr-detail'>A page stretches a little past its ends and springs back, as in Safari</div></div>
            <div class='sr-control'>" + Sw("BounceScroll", p.BounceScroll) + @"</div>
          </div></div>
        </div>
      </div>

      <div class='st-group'>
        <div class='st-group-head'>FROM ANY APP</div>
        <div class='card-st'>
          <div class='sr'><div class='sr-main'>
            <div class='sr-tile'><svg viewBox='0 0 16 16'><rect x='2.5' y='4' width='11' height='8' rx='2'/></svg></div>
            <div class='sr-words'><div class='sr-title'>Open the address field from any app " + Info("Global launcher shortcut") + @"</div><div class='sr-detail'>A key of your own raises it over the app you're in, as the Start menu's search does</div></div>
            <div class='sr-control'>" + Sw("GlobalAddressField", p.GlobalAddressField) + @"</div>
          </div></div>
          <div class='st-rule'></div>
          <div class='sr'><div class='sr-main'>
            <div class='sr-tile'><svg viewBox='0 0 16 16'><rect x='2.5' y='3.5' width='11' height='9' rx='1.5'/></svg></div>
            <div class='sr-words'><div class='sr-title'>Open in a small window " + Info("Quick preview window from global launcher") + @"</div><div class='sr-detail'>Return opens the page in a small window over the app you're in, not in Search's window</div></div>
            <div class='sr-control'>" + Sw("GlobalOpenInSmallWindow", p.GlobalOpenInSmallWindow) + @"</div>
          </div></div>
        </div>
      </div>

      <div class='st-group'>
        <div class='st-group-head'>SHORTCUTS</div>
        <div class='st-find' style='margin-bottom:4px;height:32px'>
          <svg width='12' height='12' viewBox='0 0 16 16' fill='none' stroke='currentColor' stroke-width='1.6'><circle cx='7' cy='7' r='4.3'/><path d='m10.3 10.3 3.2 3.2'/></svg>
          <input id='scFind' type='text' placeholder='Search commands or keys' oninput='filterShortcuts(this.value)' />
        </div>
        <div class='st-group-head' style='margin-top:6px'>Search</div>
        <div class='card-st sc-list'>
          <div class='st-line'><div class='st-line-words'><div class='st-line-title'>Settings…</div></div><div class='st-line-control'><span class='key-badge'>Ctrl+,</span></div></div>
          <div class='st-rule'></div>
          <div class='st-line'><div class='st-line-words'><div class='st-line-title'>Passwords…</div></div><div class='st-line-control'><span class='key-badge'>Alt+Shift+L</span></div></div>
        </div>
        <div class='st-group-head' style='margin-top:6px'>File</div>
        <div class='card-st sc-list'>
          <div class='st-line'><div class='st-line-words'><div class='st-line-title'>New Window</div></div><div class='st-line-control'><span class='key-badge'>Ctrl+N</span></div></div>
          <div class='st-rule'></div>
          <div class='st-line'><div class='st-line-words'><div class='st-line-title'>New Tab</div></div><div class='st-line-control'><span class='key-badge'>Ctrl+T</span></div></div>
          <div class='st-rule'></div>
          <div class='st-line'><div class='st-line-words'><div class='st-line-title'>New Private Tab</div></div><div class='st-line-control'><span class='key-badge'>Ctrl+Shift+N</span></div></div>
          <div class='st-rule'></div>
          <div class='st-line'><div class='st-line-words'><div class='st-line-title'>Reopen Closed Tab</div></div><div class='st-line-control'><span class='key-badge'>Ctrl+Shift+T</span></div></div>
          <div class='st-rule'></div>
          <div class='st-line'><div class='st-line-words'><div class='st-line-title'>Open Address…</div><div class='st-line-detail'>Also Alt+D, F6</div></div><div class='st-line-control'><span class='key-badge'>Ctrl+L</span></div></div>
          <div class='st-rule'></div>
          <div class='st-line'><div class='st-line-words'><div class='st-line-title'>Close Tab</div><div class='st-line-detail'>Also Ctrl+F4</div></div><div class='st-line-control'><span class='key-badge'>Ctrl+W</span></div></div>
          <div class='st-rule'></div>
          <div class='st-line'><div class='st-line-words'><div class='st-line-title'>Close Window</div></div><div class='st-line-control'><span class='key-badge'>Ctrl+Shift+W</span></div></div>
        </div>
        <div class='st-group-head' style='margin-top:6px'>Edit</div>
        <div class='card-st sc-list'>
          <div class='st-line'><div class='st-line-words'><div class='st-line-title'>Find on Page…</div></div><div class='st-line-control'><span class='key-badge'>Ctrl+F</span></div></div>
          <div class='st-rule'></div>
          <div class='st-line'><div class='st-line-words'><div class='st-line-title'>Find Next</div><div class='st-line-detail'>Also F3</div></div><div class='st-line-control'><span class='key-badge'>Ctrl+G</span></div></div>
          <div class='st-rule'></div>
          <div class='st-line'><div class='st-line-words'><div class='st-line-title'>Find Previous</div><div class='st-line-detail'>Also Shift+F3</div></div><div class='st-line-control'><span class='key-badge'>Ctrl+Shift+G</span></div></div>
        </div>
        <div class='st-group-head' style='margin-top:6px'>View</div>
        <div class='card-st sc-list'>
          <div class='st-line'><div class='st-line-words'><div class='st-line-title'>Hide Sidebar or Tab Bar</div></div><div class='st-line-control'><span class='key-badge'>Ctrl+S</span></div></div>
          <div class='st-rule'></div>
          <div class='st-line'><div class='st-line-words'><div class='st-line-title'>Reload Page</div><div class='st-line-detail'>Also F5</div></div><div class='st-line-control'><span class='key-badge'>Ctrl+R</span></div></div>
          <div class='st-rule'></div>
          <div class='st-line'><div class='st-line-words'><div class='st-line-title'>Reload Page From Origin</div><div class='st-line-detail'>Also Shift+F5</div></div><div class='st-line-control'><span class='key-badge'>Ctrl+F5</span></div></div>
          <div class='st-rule'></div>
          <div class='st-line'><div class='st-line-words'><div class='st-line-title'>Reading Mode</div></div><div class='st-line-control'><span class='key-badge'>Ctrl+Shift+R</span></div></div>
          <div class='st-rule'></div>
          <div class='st-line'><div class='st-line-words'><div class='st-line-title'>Float Video</div></div><div class='st-line-control'><span class='key-badge'>Ctrl+Shift+P</span></div></div>
          <div class='st-rule'></div>
          <div class='st-line'><div class='st-line-words'><div class='st-line-title'>Hide Elements… (Curtain)</div></div><div class='st-line-control'><span class='key-badge'>Ctrl+Shift+H</span></div></div>
          <div class='st-rule'></div>
          <div class='st-line'><div class='st-line-words'><div class='st-line-title'>Zoom In / Out / Actual Size</div></div><div class='st-line-control'><span class='key-badge'>Ctrl+= / Ctrl+- / Ctrl+0</span></div></div>
        </div>
        <div class='st-group-head' style='margin-top:6px'>Tabs</div>
        <div class='card-st sc-list'>
          <div class='st-line'><div class='st-line-words'><div class='st-line-title'>Back / Forward</div></div><div class='st-line-control'><span class='key-badge'>Alt+Left / Alt+Right</span></div></div>
          <div class='st-rule'></div>
          <div class='st-line'><div class='st-line-words'><div class='st-line-title'>Next Tab / Previous Tab</div></div><div class='st-line-control'><span class='key-badge'>Ctrl+PageDown / Ctrl+PageUp</span></div></div>
          <div class='st-rule'></div>
          <div class='st-line'><div class='st-line-words'><div class='st-line-title'>Switch to Recent Tab</div></div><div class='st-line-control'><span class='key-badge'>Ctrl+Tab</span></div></div>
          <div class='st-rule'></div>
          <div class='st-line'><div class='st-line-words'><div class='st-line-title'>Search Tabs…</div></div><div class='st-line-control'><span class='key-badge'>Ctrl+K</span></div></div>
          <div class='st-rule'></div>
          <div class='st-line'><div class='st-line-words'><div class='st-line-title'>Split Current Page</div></div><div class='st-line-control'><span class='key-badge'>Alt+Shift+N</span></div></div>
          <div class='st-rule'></div>
          <div class='st-line'><div class='st-line-words'><div class='st-line-title'>Focus Left / Right Page</div></div><div class='st-line-control'><span class='key-badge'>Alt+Shift+Left / Right</span></div></div>
          <div class='st-rule'></div>
          <div class='st-line'><div class='st-line-words'><div class='st-line-title'>Duplicate Tab</div></div><div class='st-line-control'><span class='key-badge'>Ctrl+D</span></div></div>
          <div class='st-rule'></div>
          <div class='st-line'><div class='st-line-words'><div class='st-line-title'>Tab 1 to 9</div><div class='st-line-detail'>The ninth is the last. With spaces on, they can switch spaces instead</div></div><div class='st-line-control'><span class='key-badge'>Ctrl+1 to Ctrl+9</span></div></div>
        </div>
        <div class='st-group-head' style='margin-top:6px'>Bookmarks &amp; History</div>
        <div class='card-st sc-list'>
          <div class='st-line'><div class='st-line-words'><div class='st-line-title'>Add This Page</div></div><div class='st-line-control'><span class='key-badge'>Ctrl+Shift+B</span></div></div>
          <div class='st-rule'></div>
          <div class='st-line'><div class='st-line-words'><div class='st-line-title'>Show History…</div></div><div class='st-line-control'><span class='key-badge'>Ctrl+H</span></div></div>
          <div class='st-rule'></div>
          <div class='st-line'><div class='st-line-words'><div class='st-line-title'>Downloads…</div></div><div class='st-line-control'><span class='key-badge'>Ctrl+J</span></div></div>
          <div class='st-rule'></div>
          <div class='st-line'><div class='st-line-words'><div class='st-line-title'>Copy Clean Link</div></div><div class='st-line-control'><span class='key-badge'>Ctrl+Shift+C</span></div></div>
        </div>
      </div>
    </div>

    <!-- 11. ABOUT -->
    <div class='st-page' id='sec-about'>
      <div class='st-head'>
        <svg viewBox='0 0 16 16'><circle cx='8' cy='8' r='5.6'/><path d='M8 7.2v3.6M8 5.1h.01'/></svg>
        <h1>About</h1>
      </div>
      <div class='st-group'>
        <div class='card-st'>
          <div class='sr'>
            <div class='sr-main'>
              <div class='sr-words'>
                <div class='sr-title'>Search for Windows — v0.0002</div>
                <div class='sr-detail'>Native Windows x64 + WebView2 port of driceroland/Search with Oriel UI design tokens. Lightweight ~290 KB executable, zero bundled Chromium or Electron overhead.</div>
              </div>
              <div class='sr-control'>
                <button class='pill-st filled' onclick=""act('checkForUpdates')"">Check for updates</button>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>

  </div>
</div>

<script>
  var curSec = '" + Esc(activeSection) + @"';
  function showSec(id) {
    curSec = id;
    document.querySelectorAll('.st-page').forEach(function(p) { p.classList.remove('on'); });
    document.querySelectorAll('.rail-row').forEach(function(r) {
      r.classList.toggle('on', r.getAttribute('data-sec') === id);
    });
    var target = document.getElementById('sec-' + id);
    if (target) target.classList.add('on');
  }

  function filterSettings(q) {
    q = (q || '').trim().toLowerCase();
    if (!q) {
      showSec(curSec || 'general');
      return;
    }
    document.querySelectorAll('.st-page').forEach(function(p) {
      var match = p.innerText.toLowerCase().indexOf(q) >= 0;
      p.classList.toggle('on', match);
    });
  }

  function filterShortcuts(q) {
    q = (q || '').trim().toLowerCase();
    document.querySelectorAll('.sc-list .st-line').forEach(function(line) {
      var match = !q || line.innerText.toLowerCase().indexOf(q) >= 0;
      line.style.display = match ? 'flex' : 'none';
    });
  }

  function act(action, arg) {
    if (window.chrome && window.chrome.webview) {
      window.chrome.webview.postMessage(JSON.stringify({ type: 'settingsAction', action: action, arg: arg || '', sec: curSec }));
    }
  }

  function togglePref(prop) {
    act('togglePref', prop);
  }

  function setPref(prop, val) {
    act('setPref', prop + '=' + val);
  }

  showSec(curSec);
</script>
</body>
</html>";
        }

        private static string Esc(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("'", "\\'").Replace("\r", "").Replace("\n", "");
        }

        private static string EscHtml(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
        }
    }
}
