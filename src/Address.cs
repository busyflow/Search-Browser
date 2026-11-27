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

        public enum AtRestKind { Secure, NotSecure, File, Other }

        public class AtRestInfo
        {
            public AtRestKind Kind { get; set; }
            public string Lead { get; set; }
            public string Name { get; set; }
            public string Tail { get; set; }
            public bool IsWarn { get { return Kind == AtRestKind.NotSecure; } }
            public string FullText { get { return (Kind == AtRestKind.NotSecure ? "Not secure — " : "") + (Lead ?? "") + (Name ?? "") + (Tail ?? ""); } }
        }

        private static readonly HashSet<string> TwoPartSuffixes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "co.uk", "org.uk", "ac.uk", "gov.uk", "me.uk", "net.uk",
            "com.au", "net.au", "org.au", "edu.au", "gov.au",
            "co.jp", "ne.jp", "or.jp", "ac.jp", "go.jp",
            "co.nz", "net.nz", "org.nz", "govt.nz",
            "co.za", "org.za", "gov.za", "ac.za",
            "com.br", "net.br", "org.br", "gov.br",
            "com.mx", "com.cn", "com.hk", "com.sg", "com.tw", "com.tr", "com.ar", "com.co",
            "co.in", "co.kr", "co.il", "co.id", "co.th", "com.ua", "com.pl", "com.my", "com.ph"
        };

        // Exact 1:1 port of driceroland/Search AtRest.swift: splits subdomain (dim) + registrable domain (ink) + non-standard port (dim)
        public static AtRestInfo GetAtRest(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;
            Uri u;
            if (!Uri.TryCreate(url, UriKind.Absolute, out u)) return null;
            string scheme = (u.Scheme ?? "").ToLowerInvariant();
            if (scheme == "about" || scheme == "search" || string.IsNullOrEmpty(scheme)) return null;
            if (scheme == "http" || scheme == "https")
            {
                string raw = (u.Host ?? "").Trim().TrimEnd('.').ToLowerInvariant();
                if (string.IsNullOrEmpty(raw)) return null;
                bool isIp = raw.Contains(":") || IsIpv4(raw);
                string bare = (!isIp && raw.StartsWith("www.", StringComparison.OrdinalIgnoreCase) && raw.Split('.').Length > 2)
                    ? raw.Substring(4) : raw;
                string lead = "";
                string name = bare;
                if (isIp)
                {
                    name = raw.Contains(":") && !raw.StartsWith("[") ? "[" + raw + "]" : raw;
                }
                else
                {
                    var labels = bare.Split('.');
                    if (labels.Length >= 3)
                    {
                        string lastTwo = labels[labels.Length - 2] + "." + labels[labels.Length - 1];
                        int regCount = TwoPartSuffixes.Contains(lastTwo) ? 3 : 2;
                        if (labels.Length > regCount)
                        {
                            name = string.Join(".", labels.Skip(labels.Length - regCount));
                            lead = string.Join(".", labels.Take(labels.Length - regCount)) + ".";
                        }
                    }
                }
                string tail = "";
                if (!u.IsDefaultPort && u.Port > 0) tail = ":" + u.Port;
                bool local = IsLocalHost(raw);
                return new AtRestInfo
                {
                    Kind = (scheme == "https" || local) ? AtRestKind.Secure : AtRestKind.NotSecure,
                    Lead = lead,
                    Name = name,
                    Tail = tail
                };
            }
            if (scheme == "file")
            {
                string fname = "";
                try { fname = Path.GetFileName(u.LocalPath); } catch { }
                return new AtRestInfo { Kind = AtRestKind.File, Lead = "", Name = string.IsNullOrEmpty(fname) ? "File" : fname, Tail = "" };
            }
            if (scheme == "chrome-extension")
            {
                return new AtRestInfo { Kind = AtRestKind.Other, Lead = "", Name = "Extension page", Tail = "" };
            }
            return new AtRestInfo { Kind = AtRestKind.Other, Lead = "", Name = "Not a website", Tail = "" };
        }

        private static bool IsIpv4(string host)
        {
            var parts = host.Split('.');
            if (parts.Length != 4) return false;
            for (int i = 0; i < 4; i++) { byte b; if (!byte.TryParse(parts[i], out b)) return false; }
            return true;
        }

        // Exact 1:1 port of driceroland/Search Address.editable: omits https:// and bare trailing / for clean inline editing
        public static string EditableAddress(string url)
        {
            if (string.IsNullOrWhiteSpace(url) || url == "about:blank") return "";
            Uri u;
            if (!Uri.TryCreate(url, UriKind.Absolute, out u)) return url;
            string full = u.AbsoluteUri;
            if (string.Equals(u.Scheme, "https", StringComparison.OrdinalIgnoreCase))
            {
                string shortUrl = full.Substring("https://".Length);
                if (u.AbsolutePath == "/" && string.IsNullOrEmpty(u.Query) && string.IsNullOrEmpty(u.Fragment) && shortUrl.EndsWith("/"))
                    shortUrl = shortUrl.Substring(0, shortUrl.Length - 1);
                return shortUrl;
            }
            if (string.Equals(u.Scheme, "http", StringComparison.OrdinalIgnoreCase) && IsLocalHost(u.Host))
            {
                string shortUrl = full.Substring("http://".Length);
                if (u.AbsolutePath == "/" && string.IsNullOrEmpty(u.Query) && string.IsNullOrEmpty(u.Fragment) && shortUrl.EndsWith("/"))
                    shortUrl = shortUrl.Substring(0, shortUrl.Length - 1);
                return shortUrl;
            }
            return full;
        }
    }
}
