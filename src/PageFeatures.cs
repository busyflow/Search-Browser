using System;
using System.Collections.Generic;
using System.Linq;

namespace SearchBrowser
{
    // Ported directly from driceroland/Search Sources/Search/Curtain.swift, Reader.swift, NativePiP.swift, Dusk.swift, SiteStyles.swift, Peek.swift, Translate.swift
    public static class PageFeatures
    {
        public static List<VeilItem> GetVeils(string host)
        {
            string h = Store.NormalizeHost(host);
            if (string.IsNullOrEmpty(h)) return new List<VeilItem>();
            List<VeilItem> list;
            if (Store.HiddenElements.TryGetValue(h, out list))
                return list;
            return new List<VeilItem>();
        }

        public static void AddVeil(string host, string selector, string label, string note)
        {
            string h = Store.NormalizeHost(host);
            if (string.IsNullOrEmpty(h) || string.IsNullOrWhiteSpace(selector)) return;
            List<VeilItem> list;
            if (!Store.HiddenElements.TryGetValue(h, out list))
            {
                list = new List<VeilItem>();
                Store.HiddenElements[h] = list;
            }
            if (list.Any(v => v.Selector == selector)) return;
            list.Add(new VeilItem
            {
                Selector = selector,
                Label = string.IsNullOrWhiteSpace(label) ? selector : label,
                Note = note ?? "",
                DateIso = DateTime.UtcNow.ToString("o")
            });
            Store.SaveHidden();
        }

        public static void RemoveVeil(string host, string selector)
        {
            string h = Store.NormalizeHost(host);
            List<VeilItem> list;
            if (Store.HiddenElements.TryGetValue(h, out list))
            {
                list.RemoveAll(v => v.Selector == selector);
                if (list.Count == 0) Store.HiddenElements.Remove(h);
                Store.SaveHidden();
            }
        }

        public static void RestoreAllVeils(string host)
        {
            string h = Store.NormalizeHost(host);
            if (Store.HiddenElements.Remove(h))
                Store.SaveHidden();
        }

        public static string GetCurtainCss(string host)
        {
            var list = GetVeils(host);
            if (list.Count == 0) return "";
            return string.Join("\n", list.Select(v =>
                ":is(" + v.Selector + "):not(#office-veiled):not(#office-veiled):not(#office-veiled) { display: none !important; }"));
        }

        // SiteStyles.swift: Custom per-site user CSS (like Arc Boosts)
        public static string GetSiteStyleCss(string host)
        {
            if (Store.Prefs == null || !Store.Prefs.SiteStylesEnabled) return "";
            string h = Store.NormalizeHost(host);
            if (string.IsNullOrEmpty(h)) return "";
            string css;
            if (Store.SiteStyles.TryGetValue(h, out css))
                return css ?? "";
            return "";
        }

        public static void SetSiteStyleCss(string host, string css)
        {
            string h = Store.NormalizeHost(host);
            if (string.IsNullOrEmpty(h)) return;
            if (string.IsNullOrWhiteSpace(css))
                Store.SiteStyles.Remove(h);
            else
                Store.SiteStyles[h] = css;
            Store.SaveSiteStyles();
        }

        // Dusk.swift: Automatic dark mode for light websites when frame is dark (Ctrl+Shift+D toggles per site)
        public static bool IsDuskDisabledForSite(string host)
        {
            string h = Store.NormalizeHost(host);
            return Store.Prefs.DuskDisabledHosts.Contains(h, StringComparer.OrdinalIgnoreCase);
        }

        public static bool IsDuskForcedForSite(string host)
        {
            string h = Store.NormalizeHost(host);
            return Store.Prefs.DuskForcedHosts.Contains(h, StringComparer.OrdinalIgnoreCase);
        }

        public static void ToggleDuskForSite(string host)
        {
            string h = Store.NormalizeHost(host);
            if (string.IsNullOrEmpty(h)) return;
            if (IsDuskDisabledForSite(h))
            {
                Store.Prefs.DuskDisabledHosts.RemoveAll(x => string.Equals(x, h, StringComparison.OrdinalIgnoreCase));
                Store.Prefs.DuskForcedHosts.Add(h);
            }
            else
            {
                Store.Prefs.DuskForcedHosts.RemoveAll(x => string.Equals(x, h, StringComparison.OrdinalIgnoreCase));
                Store.Prefs.DuskDisabledHosts.Add(h);
            }
            Store.SavePrefs();
        }

        public static string BuildApplySheetsScript(string host)
        {
            string h = Store.NormalizeHost(host);
            string shieldCss = EscapeJsTemplate(Shield.GetCosmeticCssForHost(h));
            string curtainCss = EscapeJsTemplate(GetCurtainCss(h));
            string customCss = EscapeJsTemplate(GetSiteStyleCss(h));

            bool duskActive = Store.Prefs.DuskEnabled && Palette.IsDark && !IsDuskDisabledForSite(h);
            bool duskForced = IsDuskForcedForSite(h);

            // Also pass matching Vault usernames (never passwords!) so clicking a login box shows the inline Safari/Search offer chip
            var logins = Store.GetLoginsForHost(h);
            string usersArrayJs = "[" + string.Join(",", logins.Select(l => "\"" + EscapeJsString(l.User) + "\"")) + "]";

            return "(function(){" +
                   "if(window.__officeSheet){" +
                   "window.__officeSheet('office-shield', `" + shieldCss + "`);" +
                   "window.__officeSheet('office-veil', `" + curtainCss + "`);" +
                   "window.__officeSheet('office-style', `" + customCss + "`);" +
                   "}" +
                   "if(window.__officeApplyDusk){ window.__officeApplyDusk(" + (duskActive ? "true" : "false") + ", " + (duskForced ? "true" : "false") + "); }" +
                   "window.__officeVaultUsers = " + usersArrayJs + ";" +
                   "})();";
        }

        private static string EscapeJsTemplate(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\", "\\\\").Replace("`", "\\`").Replace("$", "\\$");
        }

        private static string EscapeJsString(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "").Replace("\n", "");
        }

        // Bootstrap script injected before document creation:
        // 1. Curtain element picker (Ctrl+Shift+H)
        // 2. Dusk smart luminance sampler & darkener (Ctrl+Shift+D)
        // 3. Link Peek on Shift+Click (Peek.swift)
        // 4. Inline Vault Autofill dropdown directly under <input type="password"> & username inputs (Passwords.swift)
        // 5. Chrome Web Store direct "Add to Chrome" / "Add to Search" 1-click installer hook
        public static readonly string CurtainPickerBootstrapScript = @"
(function () {
  if (window.__officeVeil) return;
  var frame = null, tag = null, target = null, live = false;
  var under = null, trail = [];

  function sendToHost(obj) {
    try {
      if (window.chrome && window.chrome.webview) {
        window.chrome.webview.postMessage(JSON.stringify(obj));
      }
    } catch (e) {}
  }

  // 1. DUSK SMART DARK MODE (Ported from Dusk.swift)
  window.__officeApplyDusk = function(active, forced) {
    if (!active) {
      if (window.__officeSheet) window.__officeSheet('office-dusk', '');
      return;
    }
    var duskCss = 'html { filter: invert(0.92) hue-rotate(180deg) !important; background: #171717 !important; } ' +
      'img, video, picture, canvas, iframe, svg, [style*=""background-image""] { filter: invert(1) hue-rotate(180deg) !important; }';

    if (forced) {
      if (window.__officeSheet) window.__officeSheet('office-dusk', duskCss);
      return;
    }
    // Measure whether the site is already dark on its own by sampling computed background luminance
    function checkAndDarken() {
      if (!document.body) return;
      if (window.__officeSheet) window.__officeSheet('office-dusk', '');
      var bg = getComputedStyle(document.body).backgroundColor || '';
      var htmlBg = getComputedStyle(document.documentElement).backgroundColor || '';
      function isDarkRgb(str) {
        var m = str.match(/rgba?\((\d+),\s*(\d+),\s*(\d+)(?:,\s*([\d.]+))?\)/);
        if (!m) return false;
        if (m[4] !== undefined && parseFloat(m[4]) < 0.15) return false;
        var r = parseInt(m[1], 10), g = parseInt(m[2], 10), b = parseInt(m[3], 10);
        var lum = (0.2126 * r + 0.7152 * g + 0.0722 * b) / 255;
        return lum < 0.45;
      }
      var alreadyDark = isDarkRgb(bg) || isDarkRgb(htmlBg);
      if (!alreadyDark && window.__officeSheet) {
        window.__officeSheet('office-dusk', duskCss);
      }
    }
    if (document.body) checkAndDarken();
    else document.addEventListener('DOMContentLoaded', checkAndDarken, { once: true });
  };

  // 2. CURTAIN ELEMENT PICKER (Ported from Curtain.swift)
  function chrome() {
    if (frame) return frame;
    frame = document.createElement('div');
    frame.style.cssText = 'position:fixed;z-index:2147483646;pointer-events:none;' +
      'border:2px solid rgba(23,23,23,.9);background:rgba(23,23,23,.07);' +
      'border-radius:4px;transition:all .07s ease-out;display:none';
    tag = document.createElement('div');
    tag.style.cssText = 'position:absolute;font:500 11px ""Segoe UI"",-apple-system,' +
      'BlinkMacSystemFont,sans-serif;color:#fff;background:#171717;padding:2px 7px;' +
      'border-radius:5px;white-space:nowrap;overflow:hidden;text-overflow:ellipsis';
    frame.appendChild(tag);
    document.documentElement.appendChild(frame);
    return frame;
  }

  function place(el) {
    var box = chrome(), r = el.getBoundingClientRect();
    box.style.display = 'block';
    box.style.left = r.left + 'px';
    box.style.top = r.top + 'px';
    box.style.width = r.width + 'px';
    box.style.height = r.height + 'px';
    tag.textContent = name(el) + ' (Click to hide · ↑/↓ parent/child · Esc cancel)';
    tag.style.top = r.top >= 26 ? '-23px' : '3px';
    tag.style.left = Math.max(2, -r.left + 4) + 'px';
    tag.style.maxWidth = Math.max(120, window.innerWidth - Math.max(0, r.left) - 16) + 'px';
  }

  var known = {
    nav: 'Navigation', header: 'Header', footer: 'Footer', aside: 'Sidebar',
    form: 'Form', dialog: 'Dialog', video: 'Video', img: 'Image',
    button: 'Button', iframe: 'Embed', figure: 'Figure', table: 'Table'
  };

  function name(el) {
    var said = el.getAttribute && (el.getAttribute('aria-label') || el.getAttribute('title'));
    if (said && said.trim()) return clip(said.trim(), 40);
    var tagName = el.tagName.toLowerCase();
    if (known[tagName]) return known[tagName];
    var role = el.getAttribute && el.getAttribute('role');
    if (role) return role.charAt(0).toUpperCase() + role.slice(1);
    var text = (el.innerText || '').trim().replace(/\s+/g, ' ');
    return text ? clip(text, 40) : tagName;
  }

  function shape(el) {
    var r = el.getBoundingClientRect();
    var cx = r.left + r.width / 2, cy = r.top + r.height / 2;
    var side = cx < window.innerWidth / 3 ? 'left'
             : (cx > window.innerWidth * 2 / 3 ? 'right' : 'centre');
    var band = cy < window.innerHeight / 3 ? 'top'
             : (cy > window.innerHeight * 2 / 3 ? 'bottom' : 'middle');
    return Math.round(r.width) + '×' + Math.round(r.height) + ' · ' + band + ' ' + side;
  }

  function clip(text, n) { return text.length > n ? text.slice(0, n) + '…' : text; }

  function steady(c) {
    return /^[a-zA-Z][\w-]{2,29}$/.test(c) && !/\d{3,}/.test(c) &&
           !/^(css|sc|jsx|emotion|svelte|styles?)-/.test(c);
  }

  function unique(sel) {
    try { return document.querySelectorAll(sel).length === 1; } catch (e) { return false; }
  }

  function selectorFor(el) {
    if (el.id && unique('#' + CSS.escape(el.id))) return '#' + CSS.escape(el.id);

    var hooks = ['data-testid', 'data-test', 'data-qa', 'data-cy', 'aria-label', 'name', 'role'];
    for (var i = 0; i < hooks.length; i++) {
      var v = el.getAttribute && el.getAttribute(hooks[i]);
      if (v) {
        var s = el.tagName.toLowerCase() + '[' + hooks[i] + '=""' + CSS.escape(v) + '""]';
        if (unique(s)) return s;
      }
    }

    var classes = (el.className && typeof el.className === 'string')
      ? el.className.trim().split(/\s+/).filter(steady) : [];
    if (classes.length) {
      var byClass = el.tagName.toLowerCase() + '.' + classes.map(CSS.escape).join('.');
      if (unique(byClass)) return byClass;
    }

    var parts = [], node = el;
    while (node && node.nodeType === 1 && node !== document.documentElement) {
      if (node.id && unique('#' + CSS.escape(node.id))) {
        parts.unshift('#' + CSS.escape(node.id));
        break;
      }
      var tagName = node.tagName.toLowerCase();
      var parent = node.parentElement;
      if (!parent) { parts.unshift(tagName); break; }
      var kin = Array.prototype.filter.call(parent.children, function (c) {
        return c.tagName === node.tagName;
      });
      parts.unshift(kin.length > 1
        ? tagName + ':nth-of-type(' + (kin.indexOf(node) + 1) + ')'
        : tagName);
      node = parent;
    }
    return parts.join(' > ');
  }

  function onMove(e) {
    if (!live) return;
    var el = document.elementFromPoint(e.clientX, e.clientY);
    if (!el || el === frame || el === document.documentElement || el === document.body) return;
    if (el === under && target) return place(target);
    under = el;
    trail = [];
    target = el;
    place(el);
  }

  function onKey(e) {
    if (!live) return;
    if (e.key === 'Escape') {
      swallow(e);
      off();
      return;
    }
    if (!target || e.metaKey || e.ctrlKey || e.altKey) return;
    if (e.key === 'ArrowUp') {
      var up = target.parentElement;
      if (up && up !== document.body && up !== document.documentElement) {
        trail.push(target);
        target = up;
        place(target);
      }
    } else if (e.key === 'ArrowDown') {
      if (trail.length) {
        target = trail.pop();
        place(target);
      }
    } else {
      return;
    }
    swallow(e);
  }

  function swallow(e) {
    if (!live) return;
    e.preventDefault();
    e.stopPropagation();
    e.stopImmediatePropagation();
  }

  function onPress(e) {
    if (!live) return;
    swallow(e);
    var el = target || document.elementFromPoint(e.clientX, e.clientY);
    if (!el || el === frame || el === document.documentElement || el === document.body) return;
    sendToHost({
      type: 'veilPicked',
      selector: selectorFor(el),
      label: name(el),
      note: shape(el)
    });
    target = null;
    under = null;
    trail = [];
    if (frame) frame.style.display = 'none';
  }

  var presses = ['pointerdown', 'mousedown', 'pointerup', 'mouseup', 'click',
                 'dblclick', 'contextmenu', 'touchstart'];

  function off() {
    if (!live) return;
    live = false;
    target = null;
    under = null;
    trail = [];
    if (frame) frame.style.display = 'none';
    document.documentElement.style.cursor = '';
    document.removeEventListener('mousemove', onMove, true);
    document.removeEventListener('pointermove', onMove, true);
    document.removeEventListener('keydown', onKey, true);
    window.removeEventListener('pagehide', off);
    presses.forEach(function (kind) {
      document.removeEventListener(kind, kind === 'pointerdown' ? onPress : swallow, true);
    });
    sendToHost({ type: 'veilOff' });
  }

  window.__officeVeil = {
    on: function () {
      if (live) { off(); return; }
      live = true;
      chrome();
      document.documentElement.style.cursor = 'crosshair';
      document.addEventListener('mousemove', onMove, true);
      document.addEventListener('pointermove', onMove, true);
      document.addEventListener('keydown', onKey, true);
      window.addEventListener('pagehide', off);
      presses.forEach(function (kind) {
        document.addEventListener(kind, kind === 'pointerdown' ? onPress : swallow, true);
      });
    },
    off: off
  };

  // 3. LINK PEEK (Shift+Click on any link opens floating Peek preview card) + CHROME WEB STORE 1-CLICK INSTALL
  document.addEventListener('click', function(e) {
    if (live) return;

    // Chrome Web Store direct install detection
    if (location.hostname === 'chromewebstore.google.com' || location.hostname === 'chrome.google.com') {
      var btn = e.target && e.target.closest ? e.target.closest('button, [role=""button""]') : null;
      if (btn) {
        var txt = (btn.innerText || '').trim().toLowerCase();
        if (txt.indexOf('add to chrome') >= 0 || txt.indexOf('add to search') >= 0 || txt.indexOf('install') >= 0) {
          e.preventDefault();
          e.stopPropagation();
          sendToHost({ type: 'cwsInstall', url: location.href });
          return;
        }
      }
    }

    // Link Peek & Alt-Click Split View (host decides based on PeekTrigger / AltClickOpensSplitView prefs)
    var a = e.target && e.target.closest ? e.target.closest('a[href]') : null;
    if (a && a.href && /^https?:\/\//i.test(a.href)) {
      if (e.shiftKey && !e.ctrlKey && !e.altKey && !e.metaKey) {
        e.preventDefault(); e.stopPropagation();
        sendToHost({ type: 'peekLink', url: a.href, mod: 'shift' });
      } else if (e.altKey && !e.ctrlKey && !e.shiftKey && !e.metaKey) {
        e.preventDefault(); e.stopPropagation();
        sendToHost({ type: 'altLink', url: a.href });
      } else if (e.altKey && e.shiftKey && !e.ctrlKey && !e.metaKey) {
        e.preventDefault(); e.stopPropagation();
        sendToHost({ type: 'peekLink', url: a.href, mod: 'alt-shift' });
      } else if (e.ctrlKey && e.altKey && !e.shiftKey && !e.metaKey) {
        e.preventDefault(); e.stopPropagation();
        sendToHost({ type: 'peekLink', url: a.href, mod: 'ctrl-alt' });
      }
    }
  }, true);

  // 4. INLINE PASSWORD AUTOFILL DROPDOWN UNDER SIGN-IN FIELDS (Ported from Passwords.swift)
  var vaultPopup = null;
  function hideVaultPopup() {
    if (vaultPopup) { vaultPopup.style.display = 'none'; }
  }
  document.addEventListener('focusin', function(e) {
    var el = e.target;
    if (!el || el.tagName !== 'INPUT') return;
    var t = (el.type || 'text').toLowerCase();
    var isLoginField = (t === 'password') ||
      ((t === 'text' || t === 'email') && el.form && el.form.querySelector('input[type=""password""]'));
    if (!isLoginField) { hideVaultPopup(); return; }

    var users = window.__officeVaultUsers || [];
    if (!users.length) return;

    if (!vaultPopup) {
      vaultPopup = document.createElement('div');
      vaultPopup.style.cssText = 'position:fixed;z-index:2147483645;background:#1c1c1c;color:#ededed;' +
        'border:1px solid #333;border-radius:8px;padding:4px;box-shadow:0 8px 24px rgba(0,0,0,0.28);' +
        'font:500 12.5px ""Segoe UI"",-apple-system,sans-serif;min-width:210px;display:none';
      document.documentElement.appendChild(vaultPopup);
    }
    vaultPopup.innerHTML = '';
    var hdr = document.createElement('div');
    hdr.textContent = '🔑 Search Vault — Saved Account';
    hdr.style.cssText = 'font-size:10.5px;color:#8c8c8c;padding:4px 8px 2px';
    vaultPopup.appendChild(hdr);

    users.forEach(function(u) {
      var row = document.createElement('div');
      row.textContent = u + '  ·  Click to Fill';
      row.style.cssText = 'padding:6px 8px;border-radius:5px;cursor:pointer;';
      row.onmouseenter = function() { row.style.background = '#2d2d2d'; };
      row.onmouseleave = function() { row.style.background = 'transparent'; };
      row.onmousedown = function(ev) {
        ev.preventDefault();
        ev.stopPropagation();
        hideVaultPopup();
        sendToHost({ type: 'vaultFillUser', user: u });
      };
      vaultPopup.appendChild(row);
    });

    var r = el.getBoundingClientRect();
    vaultPopup.style.left = Math.max(8, r.left) + 'px';
    vaultPopup.style.top = Math.min(window.innerHeight - 90, r.bottom + 4) + 'px';
    vaultPopup.style.display = 'block';
  }, true);

  document.addEventListener('mousedown', function(e) {
    if (vaultPopup && vaultPopup.style.display === 'block' && !vaultPopup.contains(e.target)) {
      hideVaultPopup();
    }
  }, true);

  // Password form submission observer
  document.addEventListener('submit', function(e) {
    try {
      var form = e.target;
      if (!form || !form.querySelectorAll) return;
      var pw = form.querySelector('input[type=""password""]');
      if (!pw || !pw.value) return;
      var inputs = form.querySelectorAll('input[type=""text""], input[type=""email""], input:not([type])');
      var user = '';
      for (var i = 0; i < inputs.length; i++) {
        if (inputs[i].value && inputs[i].value.trim()) { user = inputs[i].value.trim(); break; }
      }
      if (user && pw.value) {
        sendToHost({ type: 'loginSubmitted', host: location.hostname, user: user, pass: pw.value });
      }
    } catch (err) {}
  }, true);

  // Keyboard shortcuts inside WebView2 forwarded to host (respects editable fields for page-first keys)
  document.addEventListener('keydown', function(e) {
    var k = (e.key || '').toLowerCase();
    var ctrl = e.ctrlKey;
    var alt = e.altKey;
    var sh = e.shiftKey;
    var el = document.activeElement;
    var inEdit = el && (el.tagName === 'INPUT' || el.tagName === 'TEXTAREA' || el.isContentEditable);
    if (k === 'escape' || k === 'f2' || k === 'f3' || k === 'f5' || k === 'f6' || k === 'f11' || k === 'f12') {
      if (k === 'f2' && inEdit) return;
      if (k === 'f3' || k === 'f5' || k === 'f6' || k === 'f11') e.preventDefault();
      sendToHost({ type: 'shortcut', key: k, shift: sh, ctrl: ctrl, alt: alt });
      return;
    }
    if (alt && !ctrl && !sh && (k >= '1' && k <= '9')) {
      e.preventDefault();
      sendToHost({ type: 'shortcut', key: k, shift: false, ctrl: false, alt: true });
      return;
    }
    if (alt && !ctrl && !sh && (k === 'arrowleft' || k === 'arrowright' || k === 'd' || k === 'home')) {
      e.preventDefault();
      sendToHost({ type: 'shortcut', key: k, shift: false, ctrl: false, alt: true });
      return;
    }
    if (alt && sh && !ctrl && (k === 'n' || k === 'l' || k === 'c' || k === 'd' || k === 'arrowleft' || k === 'arrowright')) {
      e.preventDefault();
      sendToHost({ type: 'shortcut', key: k, shift: true, ctrl: false, alt: true });
      return;
    }
    if (ctrl && !alt) {
      if (k === 'tab' || k === 'pageup' || k === 'pagedown' || k === 'f4') {
        e.preventDefault();
        sendToHost({ type: 'shortcut', key: k, shift: sh, ctrl: true, alt: false });
        return;
      }
      if (!sh && k === 'z' && !inEdit) {
        sendToHost({ type: 'shortcut', key: 'z', shift: false, ctrl: true, alt: false });
        return;
      }
      if ((!sh && (k === 'l' || k === 'k' || k === 't' || k === 'w' || k === 'r' || k === 'h' || k === 'j' || k === 's' || k === 'd' || k === 'n' || k === 'o' || k === 'p' || k === ',' || k === 'f' || k === 'g' || k === '=' || k === '+' || k === '-' || k === '0' || k === '[' || k === ']' || (k >= '1' && k <= '9'))) ||
          (sh && (k === 'r' || k === 'h' || k === 'p' || k === 's' || k === 'b' || k === 'n' || k === 't' || k === 'u' || k === 'd' || k === 'c' || k === 'g' || k === 'w' || k === 'i' || k === 'o' || k === 'm' || k === 'q' || k === 'v' || k === 'delete' || k === '[' || k === ']'))) {
        e.preventDefault();
        sendToHost({ type: 'shortcut', key: k, shift: sh, ctrl: true, alt: false });
      }
    }
  }, true);
})();";

        // Exact Reading Mode script from Reader.swift (with Markdown twin detection + Prose scoring)
        public static string GetReaderToggleScript(bool isDarkWindow)
        {
            string look = isDarkWindow ? "dark" : "light";
            return @"
(function() {
  if (document.getElementById('office-reader')) {
    location.reload();
    return 'reloading';
  }
  var readerLook = '" + look + @"';
  var dark = 'color-scheme:dark;--r-ground:#1c1c1c;--r-ink:#e0e0e0;--r-quiet:#8a8a8a;--r-aside:#a8a8a8;--r-rule:#333;--r-code:#2a2a2a;--r-picture:brightness(.92)';
  var sheet = ':root{color-scheme:light;--r-ground:#fff;--r-ink:#171717;--r-quiet:#a3a3a3;--r-aside:#555;--r-rule:#e8e8e8;--r-code:#f5f5f5;--r-picture:none}' +
    ':root[data-office-look=dark]{' + dark + '}' +
    'html,body{background:var(--r-ground) !important;margin:0 !important;padding:0 !important;filter:none !important}' +
    '#office-reader{max-width:38em;margin:0 auto;padding:72px 24px 160px;font:400 18px/1.72 Georgia,""Times New Roman"",serif;color:var(--r-ink)}' +
    '#office-reader h1{font:600 30px/1.24 ""Segoe UI"",-apple-system,sans-serif;margin:0 0 8px;letter-spacing:-0.01em}' +
    '#office-reader .office-from{font:400 12px/1 ""Segoe UI"",-apple-system,sans-serif;color:var(--r-quiet);margin:0 0 40px;text-transform:uppercase;letter-spacing:.06em}' +
    '#office-reader p{margin:0 0 1.35em}' +
    '#office-reader img,#office-reader video,#office-reader iframe{max-width:100%;height:auto;border-radius:6px;margin:1.6em 0;display:block}' +
    '#office-reader img{filter:var(--r-picture)}' +
    '#office-reader figure{margin:1.8em 0}' +
    '#office-reader figcaption{font:400 13px/1.5 ""Segoe UI"",sans-serif;color:var(--r-quiet);margin-top:.6em}' +
    '#office-reader a{color:var(--r-ink);text-underline-offset:3px}' +
    '#office-reader h2,#office-reader h3{font:600 20px/1.3 ""Segoe UI"",sans-serif;margin:2em 0 .6em}' +
    '#office-reader h4,#office-reader h5,#office-reader h6{font:600 16px/1.4 ""Segoe UI"",sans-serif;margin:1.8em 0 .5em}' +
    '#office-reader pre,#office-reader code{font-family:Consolas,ui-monospace,monospace;font-size:14px}' +
    '#office-reader pre{background:var(--r-code);padding:14px;border-radius:8px;overflow:auto}' +
    '#office-reader blockquote{margin:1.6em 0;padding-left:1.2em;border-left:2px solid var(--r-rule);color:var(--r-aside)}' +
    '#office-reader ul,#office-reader ol{margin:0 0 1.35em;padding-left:1.4em}' +
    '#office-reader li{margin:.3em 0}';

  function prose(el) {
    var paragraphs = el.querySelectorAll('p');
    if (paragraphs.length < 2) return 0;
    var letters = 0;
    for (var i = 0; i < paragraphs.length; i++) letters += (paragraphs[i].innerText || '').length;
    if (letters < 300) return 0;
    var whole = (el.textContent || '').length;
    if (!whole) return 0;
    var inside = 0, links = el.querySelectorAll('a');
    for (var j = 0; j < links.length; j++) inside += (links[j].textContent || '').length;
    return letters * (1 - Math.min(inside / whole, 1));
  }

  function best() {
    var candidates = document.querySelectorAll('article, main, [role=""main""], .post, .entry, .article, .content, #content, div, section');
    var top = null, mark = 0;
    for (var i = 0; i < candidates.length; i++) {
      var el = candidates[i], score = prose(el);
      if (!score) continue;
      if (!top) { top = el; mark = score; continue; }
      if (top.contains(el)) {
        if (score * 1.25 > mark) { top = el; mark = score; }
      } else if (el.contains(top)) {
        if (score > mark * 1.25) { top = el; mark = score; }
      } else if (score > mark) {
        top = el; mark = score;
      }
    }
    return top;
  }

  var article = best();
  if (!article) return 'none';

  var heading = document.querySelector('h1');
  var title = (heading && heading.innerText.trim()) || document.title;
  var wrap = document.createElement('div');
  wrap.innerHTML = article.innerHTML;

  var clutter = wrap.querySelectorAll('script,style,noscript,form,nav,aside,footer,button,input,select,textarea,[role=""complementary""],[role=""navigation""],[role=""banner""],[aria-hidden=""true""]');
  for (var c = 0; c < clutter.length; c++) clutter[c].remove();
  var styled = wrap.querySelectorAll('[style]');
  for (var y = 0; y < styled.length; y++) styled[y].removeAttribute('style');

  var own = wrap.querySelector('h1');
  if (own && own.textContent.replace(/\s+/g, ' ').trim().toLowerCase() === title.replace(/\s+/g, ' ').trim().toLowerCase()) own.remove();

  document.documentElement.setAttribute('data-office-look', readerLook);
  var theirs = document.querySelectorAll('style, link[rel~=""stylesheet"" i]');
  for (var i = 0; i < theirs.length; i++) {
    if ((theirs[i].id || '').indexOf('office-') !== 0) theirs[i].remove();
  }
  document.documentElement.removeAttribute('style');
  document.body.removeAttribute('style');
  var style = document.createElement('style');
  style.id = 'office-reader-sheet';
  style.textContent = sheet;
  var top = document.createElement('h1');
  top.textContent = title;
  var line = document.createElement('p');
  line.className = 'office-from';
  line.textContent = location.host.replace(/^www\./, '') + ' · Reading Mode (Ctrl+Shift+R to exit)';
  wrap.id = 'office-reader';
  document.body.innerHTML = '';
  (document.head || document.documentElement).appendChild(style);
  wrap.insertBefore(line, wrap.firstChild);
  wrap.insertBefore(top, wrap.firstChild);
  document.body.appendChild(wrap);
  window.scrollTo(0, 0);
  return 'read';
})();";
        }

        // Standard W3C / Chromium Picture-in-Picture toggle ported from NativePiP.swift
        public static readonly string PictureInPictureToggleScript = @"
(function () {
  if (document.pictureInPictureElement) {
    document.exitPictureInPicture().catch(function(){});
    return 'out';
  }
  var videos = document.querySelectorAll('video');
  var best = null, area = 0;
  for (var j = 0; j < videos.length; j++) {
    var w = videos[j];
    if (w.paused || w.ended || w.readyState < 2) continue;
    var box = w.getBoundingClientRect();
    if (box.width * box.height >= area) { area = box.width * box.height; best = w; }
  }
  if (!best) {
    for (var k = 0; k < videos.length; k++) {
      var u = videos[k];
      if (u.readyState < 2) continue;
      var b = u.getBoundingClientRect();
      if (b.width * b.height >= area) { area = b.width * b.height; best = u; }
    }
  }
  if (!best) return 'none';
  if (best.requestPictureInPicture) {
    best.requestPictureInPicture().catch(function(){});
    return 'in';
  }
  return 'refused';
})();";

        // In-Page DOM Text Node Translation (ported from Translate.swift — preserves React/DOM nodes & restores original)
        public static string GetInPageTranslateScript(string targetLang)
        {
            string tl = string.IsNullOrWhiteSpace(targetLang) ? "en" : targetLang.Trim();
            return @"
(async function() {
  if (window.__officeTranslated) {
    window.__officeTranslated.forEach(function(item) { item.node.nodeValue = item.orig; });
    window.__officeTranslated = null;
    return 'restored';
  }
  var walker = document.createTreeWalker(document.body, NodeFilter.SHOW_TEXT, null, false);
  var nodes = [], n;
  while ((n = walker.nextNode())) {
    var p = n.parentElement;
    if (!p) continue;
    var tag = p.tagName;
    if (tag === 'SCRIPT' || tag === 'STYLE' || tag === 'NOSCRIPT' || tag === 'CODE' || tag === 'PRE') continue;
    var t = (n.nodeValue || '').trim();
    if (t.length >= 4 && /[a-zA-Z\u00C0-\u024F\u0400-\u04FF\u4E00-\u9FFF]/.test(t)) {
      nodes.push({ node: n, orig: n.nodeValue, text: t });
    }
    if (nodes.length >= 60) break;
  }
  if (!nodes.length) return 'none';
  window.__officeTranslated = nodes;
  for (var i = 0; i < nodes.length; i += 8) {
    var batch = nodes.slice(i, i + 8);
    var joined = batch.map(function(x){ return x.text.replace(/\n/g, ' '); }).join('\n');
    try {
      var res = await fetch('https://translate.googleapis.com/translate_a/single?client=gtx&sl=auto&tl=" + tl + @"&dt=t&q=' + encodeURIComponent(joined));
      var data = await res.json();
      var full = data[0].map(function(s){ return s[0]; }).join('');
      var lines = full.split('\n');
      for (var j = 0; j < batch.length && j < lines.length; j++) {
        batch[j].node.nodeValue = ' ' + lines[j].trim() + ' ';
      }
    } catch (e) {}
  }
  return 'translated';
})();";
        }
    }
}
