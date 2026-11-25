using System;
using System.Collections.Generic;
using System.Linq;

namespace SearchBrowser
{
    // Ported directly from driceroland/Search Sources/Search/Shield.swift & YouTubeAds.swift
    public static class Shield
    {
        public static int BlockedCountTotal = 0;
        public static readonly int SessionFingerprintSeed = new Random().Next(1000, 999999);

        public static readonly HashSet<string> UnwantedDomains = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // Ad networks & exchanges
            "doubleclick.net", "googlesyndication.com", "googleadservices.com",
            "googletagservices.com", "google-analytics.com", "googletagmanager.com",
            "adservice.google.com", "amazon-adsystem.com", "adnxs.com", "adsrvr.org",
            "criteo.com", "criteo.net", "taboola.com", "outbrain.com", "outbrainimg.com",
            "rubiconproject.com", "pubmatic.com", "openx.net", "casalemedia.com",
            "smartadserver.com", "sharethrough.com", "indexww.com", "bidswitch.net",
            "33across.com", "teads.tv", "moatads.com", "adroll.com", "revcontent.com",
            "mgid.com", "zergnet.com", "zemanta.com", "mediavine.com", "adthrive.com",
            "ezoic.net", "nitropay.com", "playwire.com", "freestar.com", "snigelweb.com",
            "primis.tech", "connatix.com", "jwpltx.com", "yieldmo.com", "triplelift.com",
            "gumgum.com", "kargo.com", "lijit.com", "sovrn.com", "spotxchange.com",
            "springserve.com", "stickyadstv.com", "tidaltv.com", "tremorhub.com",
            "undertone.com", "vungle.com", "unityads.unity3d.com", "applovin.com",
            "ironsrc.com", "chartboost.com", "inmobi.com", "mopub.com", "smaato.net",
            "adcolony.com", "adform.net", "adition.com", "adkernel.com", "admanmedia.com",
            "admixer.net", "adocean.pl", "adrecover.com", "adsafeprotected.com",
            "doubleverify.com", "integralads.com", "iasds01.com", "flashtalking.com",
            "serving-sys.com", "sizmek.com", "innovid.com", "celtra.com", "media.net",
            "contextweb.com", "pulsepoint.com", "sonobi.com", "rhythmone.com",
            "gumgum.com", "cpx.to", "exoclick.com", "popads.net", "popcash.net",
            "propellerads.com", "trafficjunky.net", "juicyads.com", "adsterra.com",
            "intergient.com", "btloader.com", "ad-delivery.net", "mentorsticks.com",
            "dns-finder.com", "confiant-integrations.net", "fundingchoicesmessages.google.com",

            // Analytics, telemetry & session replay
            "scorecardresearch.com", "quantserve.com", "chartbeat.com", "chartbeat.net",
            "hotjar.com", "hotjar.io", "mouseflow.com", "fullstory.com", "clarity.ms",
            "mixpanel.com", "amplitude.com", "segment.com", "segment.io", "logrocket.com",
            "logrocket.io", "crazyegg.com", "luckyorange.com", "inspectlet.com",
            "smartlook.com", "contentsquare.net", "quantummetric.com", "glassboxdigital.io",
            "heap.io", "heapanalytics.com", "kissmetrics.com", "pendo.io", "walkme.com",
            "optimizely.com", "vwo.com", "abtasty.com", "kameleoon.eu", "launchdarkly.com",
            "omtrdc.net", "demdex.net", "everesttech.net", "2o7.net", "adobedtm.com",
            "newrelic.com", "nr-data.net", "datadoghq.com", "browser-intake-datadoghq.com",
            "sentry.io", "bugsnag.com", "rollbar.com", "trackjs.com", "raygun.io",
            "cloudflareinsights.com", "app-measurement.com", "crashlyticsreports-pa.googleapis.com",
            "firebase-settings.crashlytics.com", "mc.yandex.ru", "top-fwz1.mail.ru",
            "counter.yadro.ru", "statcounter.com", "histats.com", "parsely.com",
            "pingdom.net", "rum-static.pingdom.net", "go-mpulse.net", "mpstat.us",

            // Fingerprinting & identity / data brokers
            "fingerprintjs.com", "fpjs.io", "fpnpmcdn.net", "iovation.com", "threatmetrix.com",
            "bluekai.com", "krxd.net", "exelator.com", "rlcdn.com", "liveramp.com",
            "tapad.com", "agkn.com", "crwdcntrl.net", "lotame.com", "eyeota.net",
            "id5-sync.com", "uidapi.com", "sharedid.org", "intentiq.com", "zeotap.com",
            "permutive.com", "permutive.app", "narrativ.com", "skimresources.com",
            "viglink.com", "bounceexchange.com", "bouncex.net", "wunderkind.co",

            // Social tracking pixels & marketing automation
            "branch.io", "appsflyer.com", "adjust.com", "singular.net", "kochava.com",
            "analytics.tiktok.com", "business-api.tiktok.com", "mon.tiktokv.com",
            "connect.facebook.net", "pixel.facebook.com", "an.facebook.com",
            "ads-twitter.com", "analytics.twitter.com", "static.ads-twitter.com",
            "ads.linkedin.com", "px.ads.linkedin.com", "snap.licdn.com",
            "tr.snapchat.com", "sc-static.net", "ct.pinterest.com", "log.pinterest.com",
            "bat.bing.com", "ads.reddit.com", "alb.reddit.com", "events.redditmedia.com",
            "hs-analytics.net", "hs-scripts.com", "hsadspixel.net", "hs-banner.com",
            "marketo.net", "mktoresp.com", "pardot.com", "salesforceiq.com",
            "braze.com", "onesignal.com", "klaviyo.com", "drip.com", "intercom.io",
            "fls-na.amazon.com", "unagi.amazon.com", "fls-eu.amazon.com"
        };

        public static readonly string[] CosmeticSlots = new[]
        {
            ".adsbygoogle", "ins.adsbygoogle", "[id^=\"google_ads_\"]",
            "[id^=\"div-gpt-ad\"]", "[id^=\"taboola-\"]", "#taboola-below-article",
            "iframe[src*=\"doubleclick.net\"]", "iframe[src*=\"googlesyndication\"]",
            "iframe[src*=\"amazon-adsystem\"]",
            ".ad-slot", ".ad-slot-container", ".top-banner-ad-container",
            ".ad-leaderboard", ".ad-billboard", ".ad-giga", ".ad-mpu", ".ad-mrec",
            ".ad-unit", ".adunit", ".adslot", ".dfp-ad", ".gpt-ad", ".w_ad"
        };

        public static readonly string[] RedditSlots = new[]
        {
            "shreddit-ad-post", "shreddit-comments-page-ad", "shreddit-sidebar-ad",
            "shreddit-dynamic-ad-link", "comment-tree-ad", ".promotedlink"
        };

        public static readonly string[] YouTubeBlockedPaths = new[]
        {
            "/api/stats/ads", "/pagead/", "/ptracking", "/get_midroll_info",
            "/youtubei/v1/player/ad_break", "/pcs/activeview"
        };

        public static bool IsPaused(string pageHost)
        {
            if (string.IsNullOrEmpty(pageHost)) return false;
            string norm = Store.NormalizeHost(pageHost);
            return Store.Prefs.ShieldPausedHosts.Contains(norm, StringComparer.OrdinalIgnoreCase);
        }

        public static void TogglePause(string pageHost)
        {
            if (string.IsNullOrEmpty(pageHost)) return;
            string norm = Store.NormalizeHost(pageHost);
            var existing = Store.Prefs.ShieldPausedHosts.FirstOrDefault(h => string.Equals(h, norm, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
                Store.Prefs.ShieldPausedHosts.Remove(existing);
            else
                Store.Prefs.ShieldPausedHosts.Add(norm);
            Store.SavePrefs();
        }

        public static bool ShouldBlockRequest(string requestUrl, string pageUrl)
        {
            if (!Store.Prefs.ShieldEnabled) return false;

            Uri reqUri;
            if (!Uri.TryCreate(requestUrl, UriKind.Absolute, out reqUri)) return false;
            string reqHost = reqUri.Host.ToLowerInvariant();
            string reqPathAndQuery = reqUri.PathAndQuery;

            string pageHost = "";
            Uri pageUri;
            if (!string.IsNullOrEmpty(pageUrl) && Uri.TryCreate(pageUrl, UriKind.Absolute, out pageUri))
                pageHost = pageUri.Host.ToLowerInvariant();

            if (IsPaused(pageHost)) return false;

            // Allow YouTube's instream ad_status.js check so playback doesn't stall
            if (reqHost == "static.doubleclick.net" && reqPathAndQuery.StartsWith("/instream/ad_status.js", StringComparison.OrdinalIgnoreCase))
                return false;

            // YouTube specific ad endpoints (exact host boundary match from oriel lists.ts:86)
            if (reqHost == "youtube.com" || reqHost.EndsWith(".youtube.com", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var ytPath in YouTubeBlockedPaths)
                {
                    if (reqPathAndQuery.StartsWith(ytPath, StringComparison.OrdinalIgnoreCase))
                    {
                        BlockedCountTotal++;
                        return true;
                    }
                }
                if (reqPathAndQuery.StartsWith("/api/stats/qoe?", StringComparison.OrdinalIgnoreCase) &&
                    reqPathAndQuery.IndexOf("adformat=", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    BlockedCountTotal++;
                    return true;
                }
            }

            bool pageIsYt = pageHost == "youtube.com" || pageHost.EndsWith(".youtube.com", StringComparison.OrdinalIgnoreCase) ||
                            pageHost == "youtube-nocookie.com" || pageHost.EndsWith(".youtube-nocookie.com", StringComparison.OrdinalIgnoreCase);
            if (pageIsYt &&
                (reqHost == "imasdk.googleapis.com" || ((reqHost == "google.com" || reqHost.EndsWith(".google.com", StringComparison.OrdinalIgnoreCase)) && reqPathAndQuery.StartsWith("/pagead/"))))
            {
                BlockedCountTotal++;
                return true;
            }

            // Third-party tracker & ad network check
            string reqDomain = Store.RegistrableDomain(reqHost);
            string pageDomain = Store.RegistrableDomain(pageHost);

            // First-party requests are untouched
            if (!string.IsNullOrEmpty(pageDomain) && string.Equals(reqDomain, pageDomain, StringComparison.OrdinalIgnoreCase))
                return false;

            if (MatchesUnwantedDomain(reqHost))
            {
                BlockedCountTotal++;
                return true;
            }

            return false;
        }

        private static bool MatchesUnwantedDomain(string host)
        {
            if (UnwantedDomains.Contains(host)) return true;
            int dot = host.IndexOf('.');
            while (dot > 0 && dot < host.Length - 1)
            {
                string sub = host.Substring(dot + 1);
                if (UnwantedDomains.Contains(sub)) return true;
                dot = host.IndexOf('.', dot + 1);
            }
            return false;
        }

        public static string GetCosmeticCssForHost(string host)
        {
            if (!Store.Prefs.ShieldEnabled || IsPaused(host)) return "";
            string h = Store.NormalizeHost(host);
            List<string> selectors = new List<string>(CosmeticSlots);

            if (h.EndsWith("reddit.com"))
                selectors.AddRange(RedditSlots);
            else if (h.EndsWith("as.com") || h.EndsWith("elpais.com"))
                selectors.Add(".ad");
            else if (h.EndsWith("theguardian.com"))
                selectors.Add(".top-fronts-banner-ad-container");
            else if (h.EndsWith("independent.co.uk") || h.EndsWith("the-independent.com"))
                selectors.Add("#billboard-wrapper");
            else if (h.EndsWith("cnn.com"))
                selectors.Add(".ad-slot-header__wrapper");
            else if (h.EndsWith("albumoftheyear.org"))
                selectors.Add(".headerAd, .homePageAdSquare");

            return string.Join(", ", selectors) + " { display: none !important; }";
        }

        // Ported from YouTubeAds.swift skipper + Shield cosmetic injector
        public static string GetStartupScript()
        {
            return @"
(function() {
  if (!window.__officeSheet) {
    window.__officeSheet = function (id, css) {
      var all = window.__officeSheets || (window.__officeSheets = {});
      try {
        var made = all[id] || (all[id] = new CSSStyleSheet());
        made.replaceSync(css);
        var adopted = document.adoptedStyleSheets;
        if (adopted.indexOf(made) < 0) document.adoptedStyleSheets = adopted.concat([made]);
        return;
      } catch (e) {}
      var s = document.getElementById(id);
      if (!s) {
        s = document.createElement('style');
        s.id = id;
        (document.head || document.documentElement).appendChild(s);
      }
      s.textContent = css;
    };
  }

  // YouTube fast ad skipper from Search's YouTubeAds.swift
  if (location.hostname.indexOf('youtube.com') >= 0) {
    var held = new WeakMap();
    function look(event) {
      var video = event.target;
      if (!(video instanceof HTMLVideoElement)) return;
      var player = video.closest('.html5-video-player');
      var ad = !!player && player.classList.contains('ad-showing');
      var was = held.get(video);
      try {
        if (ad) {
          if (!was) {
            was = { muted: video.muted, rate: video.playbackRate };
            held.set(video, was);
            video.muted = true;
          }
          if (video.playbackRate !== 16) video.playbackRate = 16;
          var skipBtn = document.querySelector('.ytp-ad-skip-button, .ytp-ad-skip-button-modern, .ytp-skip-ad-button');
          if (skipBtn) skipBtn.click();
        } else if (was) {
          held.delete(video);
          video.playbackRate = was.rate;
          video.muted = was.muted;
        }
      } catch (e) {}
    }
    ['playing', 'timeupdate', 'durationchange', 'emptied'].forEach(function (type) {
      document.addEventListener(type, look, true);
    });
  }
})();";
        }

        // Brave-style per-session deterministic anti-fingerprinting ("Farbling")
        // Randomizes Canvas, WebGL GPU strings, AudioContext, Hardware/Battery hints, and blocks tracker beacons.
        public static string GetAntiFingerprintingScript(int seed)
        {
            return @"
(function() {
  try {
    if (window.__searchFarbleInstalled) return;
    if (location.protocol !== 'http:' && location.protocol !== 'https:') return;
    window.__searchFarbleInstalled = true;

    // Deterministic per-session + per-origin hash so a site sees consistent values within a session
    var baseSeed = " + seed + @";
    var host = (location.hostname || '');
    var h = baseSeed;
    for (var i = 0; i < host.length; i++) {
      h = ((h << 5) - h + host.charCodeAt(i)) | 0;
    }
    function nextNoise(idx) {
      var x = Math.sin(h + idx * 12.9898) * 43758.5453;
      return x - Math.floor(x);
    }

    // 1. Non-destructive Canvas 2D getImageData readback farbling (never mutates the live canvas)
    var origGetImageData = CanvasRenderingContext2D.prototype.getImageData;
    CanvasRenderingContext2D.prototype.getImageData = function(sx, sy, sw, sh) {
      var img = origGetImageData.apply(this, arguments);
      try {
        if (img && img.data && img.data.length >= 16 && img.data.length <= 262144) {
          var d = img.data;
          var step = Math.max(4, Math.floor(d.length / 16) * 4);
          for (var p = 0; p < d.length; p += step) {
            var delta = nextNoise(p + sx + sy) > 0.5 ? 1 : -1;
            d[p] = Math.max(0, Math.min(255, d[p] + delta));
          }
        }
      } catch (e) {}
      return img;
    };

    // 2. AudioContext fingerprinting farbling (imperceptible 1e-7 deterministic jitter)
    if (window.AudioBuffer && AudioBuffer.prototype.getChannelData) {
      var origGetChannelData = AudioBuffer.prototype.getChannelData;
      AudioBuffer.prototype.getChannelData = function() {
        var data = origGetChannelData.apply(this, arguments);
        try {
          if (data && data.length > 0 && !data.__searchFarbled) {
            Object.defineProperty(data, '__searchFarbled', { value: true, enumerable: false });
            var stride = Math.max(1, Math.floor(data.length / 32));
            for (var i = 0; i < data.length; i += stride) {
              data[i] = data[i] + (nextNoise(i) - 0.5) * 1e-7;
            }
          }
        } catch (e) {}
        return data;
      };
    }

    // 3. Normalize hardware hints & return generic Battery status (avoids anti-bot rejection tells)
    try {
      var hc = (nextNoise(1) > 0.5) ? 8 : 4;
      Object.defineProperty(Navigator.prototype, 'hardwareConcurrency', { get: function() { return hc; }, configurable: true });
    } catch (e) {}
    try {
      Object.defineProperty(Navigator.prototype, 'deviceMemory', { get: function() { return 8; }, configurable: true });
    } catch (e) {}
    try {
      if (Navigator.prototype.getBattery) {
        Navigator.prototype.getBattery = function() {
          return Promise.resolve({ charging: true, chargingTime: 0, dischargingTime: Infinity, level: 1, addEventListener: function(){}, removeEventListener: function(){} });
        };
      }
    } catch (e) {}

    // 5. Block sendBeacon calls to obvious third-party tracker paths
    if (navigator.sendBeacon) {
      var origBeacon = navigator.sendBeacon.bind(navigator);
      navigator.sendBeacon = function(url, data) {
        try {
          var u = new URL(String(url), location.href);
          if (u.hostname !== location.hostname && /(doubleclick|google-analytics|googletagmanager|clarity\.ms|hotjar|mixpanel|amplitude|segment|fullstory|mouseflow|sentry|datadoghq|newrelic|nr-data|facebook\.net|tiktok|taboola|outbrain)/i.test(u.hostname)) {
            return true;
          }
        } catch (e) {}
        return origBeacon(url, data);
      };
    }
  } catch (e) {}
})();";
        }
    }
}
