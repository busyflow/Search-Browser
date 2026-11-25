using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SearchBrowser
{
    // Exact 1:1 port of oriel-ref/src/main/address.ts & engines.ts
    public static class Address
    {
        private static readonly HashSet<string> AllowedSchemes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "http", "https", "file", "about", "data", "search", "chrome-extension"
        };

        private static readonly Dictionary<string, string> BuiltInKeywords = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "yt", "https://www.youtube.com/results?search_query=%s" },
            { "g", "https://www.google.com/search?q=%s" },
            { "ddg", "https://duckduckgo.com/?q=%s" },
            { "w", "https://en.wikipedia.org/w/index.php?search=%s" },
            { "gh", "https://github.com/search?q=%s" },
            { "r", "https://www.reddit.com/search/?q=%s" },
            { "a", "https://www.amazon.com/s?k=%s" }
        };

        public static string ResolveInput(string typed, string searchEngineTemplate)
        {
            if (string.IsNullOrWhiteSpace(typed)) return "about:blank";
            string text = typed.Trim();

            string directUrl = TryParseUrl(text);
            if (directUrl != null) return directUrl;

            // Site shortcuts ("yt cats", "gh repo", etc.)
            int sp = text.IndexOf(' ');
            if (sp > 0 && sp < text.Length - 1)
            {
                string kw = text.Substring(0, sp).Trim();
                string rest = text.Substring(sp + 1).Trim();
                string tpl;
                if (!string.IsNullOrEmpty(rest) && BuiltInKeywords.TryGetValue(kw, out tpl))
                    return FormatSearch(tpl, rest);
            }

            string engine = string.IsNullOrWhiteSpace(searchEngineTemplate)
                ? "https://www.google.com/search?q=%s"
                : searchEngineTemplate;
            return FormatSearch(engine, text);
        }

        public static string GetSearchEngineUrl(string name)
        {
            if (string.Equals(name, "Google", StringComparison.OrdinalIgnoreCase)) return "https://www.google.com/search?q=%s";
            if (string.Equals(name, "Brave Search", StringComparison.OrdinalIgnoreCase) || string.Equals(name, "Brave", StringComparison.OrdinalIgnoreCase)) return "https://search.brave.com/search?q=%s";
            if (string.Equals(name, "Bing", StringComparison.OrdinalIgnoreCase)) return "https://www.bing.com/search?q=%s";
            if (string.Equals(name, "Ecosia", StringComparison.OrdinalIgnoreCase)) return "https://www.ecosia.org/search?q=%s";
            if (string.Equals(name, "Startpage", StringComparison.OrdinalIgnoreCase)) return "https://www.startpage.com/do/search?q=%s";
            if (string.Equals(name, "Kagi", StringComparison.OrdinalIgnoreCase)) return "https://kagi.com/search?q=%s";
            if (string.Equals(name, "Qwant", StringComparison.OrdinalIgnoreCase)) return "https://www.qwant.com/?q=%s";
            return "https://duckduckgo.com/?q=%s";
        }

        public static string SearchUrl(string query, string engineName = null)
        {
            if (!string.IsNullOrEmpty(engineName))
                return FormatSearch(GetSearchEngineUrl(engineName), query);
            string tpl = Store.Prefs != null && !string.IsNullOrWhiteSpace(Store.Prefs.SearchEngineUrl)
                ? Store.Prefs.SearchEngineUrl
                : GetSearchEngineUrl(Store.Prefs != null ? Store.Prefs.SearchEngineName : "DuckDuckGo");
            return ResolveInput(query, tpl);
        }

        public static string FormatSearch(string template, string query)
        {
            string enc = Uri.EscapeDataString(query ?? "");
            if (template.Contains("%s"))
                return template.Replace("%s", enc);
            return template + enc;
        }

        public static string TryParseUrl(string typed)
        {
            if (string.IsNullOrWhiteSpace(typed)) return null;
            string text = typed.Trim();

            // Parse about:, data:, search: BEFORE whitespace rejection (address.ts:58-60)
            if (text.StartsWith("about:", StringComparison.OrdinalIgnoreCase) ||
                text.StartsWith("data:", StringComparison.OrdinalIgnoreCase) ||
                text.StartsWith("search:", StringComparison.OrdinalIgnoreCase))
            {
                return text;
            }

            // Local file paths (C:\..., \\server\..., ~\...)
            string localFile = TryFileUrl(text);
            if (localFile != null) return localFile;

            if (text.Any(char.IsWhiteSpace)) return null;

            int schemeIdx = text.IndexOf("://", StringComparison.Ordinal);
            if (schemeIdx > 0)
            {
                string scheme = text.Substring(0, schemeIdx).ToLowerInvariant();
                if (!AllowedSchemes.Contains(scheme)) return null;
                Uri u;
                if (Uri.TryCreate(text, UriKind.Absolute, out u))
                {
                    if (u.Host == "0.0.0.0")
                    {
                        var ub = new UriBuilder(u) { Host = "localhost" };
                        return ub.Uri.AbsoluteUri;
                    }
                    return u.AbsoluteUri;
                }
                return null;
            }

            // Extract host before /, ?, #
            int slashIdx = text.IndexOfAny(new[] { '/', '?', '#' });
            string head = slashIdx >= 0 ? text.Substring(0, slashIdx) : text;
            if (head.Contains("@")) return null; // email address

            if (head.StartsWith("[::1]"))
                return "http://" + text;

            string host = head.Split(':')[0];
            if (!LooksLikeHost(host)) return null;

            bool isLocal = IsLocalHost(host);
            if (host == "0.0.0.0")
                text = "localhost" + text.Substring("0.0.0.0".Length);

            return (isLocal ? "http://" : "https://") + text;
        }

        private static string TryFileUrl(string text)
        {
            try
            {
                string candidate = text;
                if (candidate.StartsWith("~\\") || candidate.StartsWith("~/"))
                {
                    string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                    candidate = Path.Combine(home, candidate.Substring(2));
                }
                bool looksPath = (candidate.Length >= 3 && char.IsLetter(candidate[0]) && candidate[1] == ':' && (candidate[2] == '\\' || candidate[2] == '/'))
                    || candidate.StartsWith("\\\\");
                if (looksPath && File.Exists(candidate))
                    return new Uri(candidate).AbsoluteUri;
            }
            catch { }
            return null;
        }

        public static bool IsLocalHost(string host)
        {
            if (string.IsNullOrWhiteSpace(host)) return false;
            return string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
                || host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase)
                || host.EndsWith(".local", StringComparison.OrdinalIgnoreCase)
                || host.EndsWith(".test", StringComparison.OrdinalIgnoreCase)
                || host == "127.0.0.1"
                || host == "0.0.0.0"
                || host == "::1"
                || host == "[::1]"
                || IsPrivateRange(host);
        }

        private static bool IsPrivateRange(string host)
        {
            var parts = host.Split('.');
            if (parts.Length != 4) return false;
            byte[] octets = new byte[4];
            for (int i = 0; i < 4; i++)
            {
                if (!byte.TryParse(parts[i], out octets[i])) return false;
            }
            return octets[0] == 10
                || (octets[0] == 192 && octets[1] == 168)
                || (octets[0] == 172 && octets[1] >= 16 && octets[1] <= 31);
        }

        private static bool LooksLikeHost(string host)
        {
            if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)) return true;

            var parts = host.Split('.');
            if (parts.Length == 4 && parts.All(p => { byte b; return byte.TryParse(p, out b); }))
                return true;

            if (parts.Length < 2) return false;
            foreach (var label in parts)
            {
                if (string.IsNullOrEmpty(label) || label.StartsWith("-") || label.EndsWith("-"))
                    return false;
                if (!label.All(c => char.IsLetterOrDigit(c) || c == '-'))
                    return false;
            }

            string tld = parts[parts.Length - 1];
            return tld.Length >= 2 && tld.All(char.IsLetter);
        }

        public static string DisplayHost(string url)
        {
            if (string.IsNullOrWhiteSpace(url) || url == "about:blank") return "";
            Uri u;
            if (!Uri.TryCreate(url, UriKind.Absolute, out u)) return url;
            string host = u.Host;
            if (host.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
                host = host.Substring(4);
            return host;
        }

        public static string ShownAddress(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return "";
            Uri u;
            if (!Uri.TryCreate(url, UriKind.Absolute, out u)) return url;
            if (!string.IsNullOrEmpty(u.UserInfo))
            {
                var ub = new UriBuilder(u) { UserName = "", Password = "" };
                return ub.Uri.AbsoluteUri;
            }
            return u.AbsoluteUri;
        }

        public static string Pretty(string url)
        {
            if (string.IsNullOrWhiteSpace(url) || url == "about:blank") return "";
            Uri u;
            if (!Uri.TryCreate(url, UriKind.Absolute, out u)) return url;
            string host = DisplayHost(url);
            string path = Uri.UnescapeDataString(u.AbsolutePath ?? "");
            if (path == "/") path = "";
            return host + path;
        }
    }
}
