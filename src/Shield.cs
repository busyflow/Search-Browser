using System;
using System.Collections.Generic;
using System.Linq;

namespace SearchBrowser
{
    // Ported directly from driceroland/Search Sources/Search/Shield.swift & YouTubeAds.swift
    public static class Shield
    {
        public static int BlockedCountTotal = 0;

        public static readonly HashSet<string> UnwantedDomains = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "doubleclick.net", "googlesyndication.com", "googleadservices.com",
            "googletagservices.com", "google-analytics.com", "googletagmanager.com",
            "adservice.google.com", "amazon-adsystem.com", "adnxs.com", "adsrvr.org",
            "criteo.com", "criteo.net", "taboola.com", "outbrain.com",
            "rubiconproject.com", "pubmatic.com", "openx.net", "casalemedia.com",
            "smartadserver.com", "sharethrough.com", "indexww.com", "bidswitch.net",
            "33across.com", "teads.tv", "moatads.com", "adroll.com",
            "scorecardresearch.com", "quantserve.com", "chartbeat.com",
            "hotjar.com", "mouseflow.com", "fullstory.com", "clarity.ms",
            "mixpanel.com", "amplitude.com", "segment.com", "segment.io",
            "branch.io", "appsflyer.com", "adjust.com", "analytics.tiktok.com",
            "connect.facebook.net", "ads-twitter.com", "analytics.twitter.com",
            "intergient.com", "btloader.com", "ad-delivery.net", "mentorsticks.com",
            "dns-finder.com", "confiant-integrations.net", "cloudflareinsights.com"
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
    }
}
