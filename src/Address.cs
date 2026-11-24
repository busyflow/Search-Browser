using System;
using System.Collections.Generic;
using System.Linq;

namespace SearchBrowser
{
    // Ported directly from driceroland/Search Sources/Search/Address.swift
    public static class Address
    {
        private static readonly HashSet<string> AllowedSchemes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "http", "https", "file", "about", "data", "search"
        };

        public static string ResolveInput(string typed, string searchEngineTemplate)
        {
            if (string.IsNullOrWhiteSpace(typed)) return "about:blank";
            string text = typed.Trim();

            string directUrl = TryParseUrl(text);
            if (directUrl != null) return directUrl;

            string engine = string.IsNullOrWhiteSpace(searchEngineTemplate)
                ? "https://duckduckgo.com/?q="
                : searchEngineTemplate;
            return engine + Uri.EscapeDataString(text);
        }

        public static string TryParseUrl(string typed)
        {
            if (string.IsNullOrWhiteSpace(typed)) return null;
            string text = typed.Trim();
            if (text.Contains(" ")) return null;

            // Local Windows path e.g. C:\...
            if (text.Length >= 3 && char.IsLetter(text[0]) && text[1] == ':' && (text[2] == '\\' || text[2] == '/'))
            {
                try { return new Uri(text).AbsoluteUri; } catch { }
            }

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

            if (text.StartsWith("about:", StringComparison.OrdinalIgnoreCase) ||
                text.StartsWith("data:", StringComparison.OrdinalIgnoreCase) ||
                text.StartsWith("search:", StringComparison.OrdinalIgnoreCase))
            {
                return text;
            }

            // Extract host before /, ?, #
            int slashIdx = text.IndexOfAny(new[] { '/', '?', '#' });
            string head = slashIdx >= 0 ? text.Substring(0, slashIdx) : text;
            if (head.Contains("@")) return null; // email address

            if (head.StartsWith("[::1]"))
                return "http://" + text;

            string host = head.Split(':')[0];
            if (!LooksLikeHost(host)) return null;

            bool isLocal = string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
                || host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase)
                || host.EndsWith(".local", StringComparison.OrdinalIgnoreCase)
                || host == "127.0.0.1"
                || host == "0.0.0.0"
                || IsPrivateRange(host);

            if (host == "0.0.0.0")
                text = "localhost" + text.Substring("0.0.0.0".Length);

            return (isLocal ? "http://" : "https://") + text;
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
            return string.IsNullOrEmpty(host) ? url : host;
        }
    }
}
