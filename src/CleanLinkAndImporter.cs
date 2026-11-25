using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace SearchBrowser
{
    // Ported from driceroland/Search Sources/Search/CleanLink.swift & Import.swift
    public static class CleanLink
    {
        private static readonly HashSet<string> GlobalTrackingParams = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "fbclid", "gclid", "dclid", "gbraid", "wbraid", "msclkid", "yclid",
            "twclid", "ttclid", "li_fat_id", "mc_eid", "mc_cid", "igshid", "igsh",
            "_hsenc", "_hsmi", "mkt_tok", "vero_id", "oly_enc_id", "oly_anon_id",
            "s_cid", "wickedid", "epik", "ref_src", "ref_url", "spm", "scm",
            "pd_rd_r", "pd_rd_w", "pd_rd_wg", "pf_rd_i", "pf_rd_m", "pf_rd_p",
            "pf_rd_r", "pf_rd_s", "pf_rd_t", "dib", "dib_tag", "_openstat",
            "rb_clickid", "sclid", "icid", "ncid", "ocid", "cvid", "sr_share",
            "ga_source", "ga_medium", "ga_term", "ga_content", "ga_campaign",
            "at_medium", "at_campaign", "at_custom1", "at_custom2"
        };

        private static readonly HashSet<string> SignatureParams = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "x-amz-signature", "x-goog-signature", "signature", "sig", "x-amz-credential", "token", "code", "state"
        };

        public static string Clean(string rawUrl, bool stripUtm = true)
        {
            if (string.IsNullOrWhiteSpace(rawUrl)) return rawUrl;
            Uri uri;
            if (!Uri.TryCreate(rawUrl.Trim(), UriKind.Absolute, out uri)) return rawUrl;
            if (!string.Equals(uri.Scheme, "http", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(uri.Scheme, "https", StringComparison.OrdinalIgnoreCase))
                return rawUrl;

            string query = uri.Query;
            if (string.IsNullOrEmpty(query) || query.Length <= 1) return rawUrl;

            string host = uri.Host.ToLowerInvariant();
            string[] parts = query.Substring(1).Split('&');
            List<string> kept = new List<string>();

            foreach (var part in parts)
            {
                string key = part.Split('=')[0];
                string decodedKey = Uri.UnescapeDataString(key.Replace("+", " ")).ToLowerInvariant();
                if (SignatureParams.Contains(decodedKey))
                    return rawUrl; // Never touch signed URLs
            }

            foreach (var part in parts)
            {
                string key = part.Split('=')[0];
                string decodedKey = Uri.UnescapeDataString(key.Replace("+", " ")).ToLowerInvariant();
                if (!IsTrackingParam(decodedKey, host, stripUtm))
                    kept.Add(part);
            }

            if (kept.Count == parts.Length) return rawUrl;

            var builder = new UriBuilder(uri)
            {
                Query = kept.Count == 0 ? "" : string.Join("&", kept)
            };
            return builder.Uri.AbsoluteUri;
        }

        private static bool IsTrackingParam(string name, string host, bool stripUtm)
        {
            if ((stripUtm && name.StartsWith("utm_", StringComparison.OrdinalIgnoreCase)) ||
                name.StartsWith("hsa_", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("mtm_", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("pk_", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("matomo_", StringComparison.OrdinalIgnoreCase) ||
                GlobalTrackingParams.Contains(name))
                return true;
            if (name == "si" && (host == "youtube.com" || host.EndsWith(".youtube.com") || host == "youtu.be" || host == "spotify.com" || host.EndsWith(".spotify.com")))
                return true;
            return false;
        }
    }

    public static class BrowserImporter
    {
        public class ImportSummary
        {
            public string BrowserName { get; set; }
            public int BookmarksImported { get; set; }
            public int PasswordsImported { get; set; }
        }

        public static List<ImportSummary> ImportFromInstalledBrowsers()
        {
            var results = new List<ImportSummary>();
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

            var candidates = new[]
            {
                new { Name = "Google Chrome", Path = Path.Combine(localAppData, @"Google\Chrome\User Data\Default\Bookmarks") },
                new { Name = "Microsoft Edge", Path = Path.Combine(localAppData, @"Microsoft\Edge\User Data\Default\Bookmarks") },
                new { Name = "Brave", Path = Path.Combine(localAppData, @"BraveSoftware\Brave-Browser\User Data\Default\Bookmarks") },
                new { Name = "Arc", Path = Path.Combine(localAppData, @"Packages\TheBrowserCompany.Arc_ttt1ap7aakyb4\LocalCache\Local\Arc\User Data\Default\Bookmarks") },
                new { Name = "Opera", Path = Path.Combine(appData, @"Opera Software\Opera Stable\Bookmarks") }
            };

            foreach (var c in candidates)
            {
                if (File.Exists(c.Path))
                {
                    int count = ImportChromiumBookmarksFile(c.Path);
                    if (count > 0)
                    {
                        results.Add(new ImportSummary
                        {
                            BrowserName = c.Name,
                            BookmarksImported = count
                        });
                    }
                }
            }

            Store.SaveBookmarks();
            return results;
        }

        public static int ImportChromiumBookmarksFile(string filePath)
        {
            int added = 0;
            try
            {
                string json = File.ReadAllText(filePath, Encoding.UTF8);
                // Match Chromium bookmark entries: "name": "...", ... "type": "url", "url": "http..."
                var matches = Regex.Matches(json, "\"name\"\\s*:\\s*\"((?:\\\\.|[^\"])*)\"[^{}]*?\"type\"\\s*:\\s*\"url\"[^{}]*?\"url\"\\s*:\\s*\"(https?://(?:\\\\.|[^\"])*)\"", RegexOptions.Singleline);
                foreach (Match m in matches)
                {
                    string title = Regex.Unescape(m.Groups[1].Value).Trim();
                    string url = Regex.Unescape(m.Groups[2].Value).Trim();
                    if (string.IsNullOrEmpty(url)) continue;
                    if (!Store.Bookmarks.Any(b => string.Equals(b.Url, url, StringComparison.OrdinalIgnoreCase)))
                    {
                        Store.Bookmarks.Add(new BookmarkItem
                        {
                            Id = Guid.NewGuid().ToString("N"),
                            Title = string.IsNullOrEmpty(title) ? url : title,
                            Url = url,
                            DateIso = DateTime.UtcNow.ToString("o")
                        });
                        added++;
                    }
                }
            }
            catch { }
            return added;
        }

        public static int ImportPasswordsFromCsv(string csvFilePath)
        {
            if (!File.Exists(csvFilePath)) return 0;
            int added = 0;
            try
            {
                var lines = File.ReadAllLines(csvFilePath, Encoding.UTF8);
                // Chrome/Edge/Brave CSV format: name,url,username,password
                for (int i = 1; i < lines.Length; i++)
                {
                    string line = lines[i].Trim();
                    if (string.IsNullOrEmpty(line)) continue;
                    var cols = ParseCsvRow(line);
                    if (cols.Count >= 4)
                    {
                        string urlOrHost = cols[1];
                        string user = cols[2];
                        string pass = cols[3];
                        if (!string.IsNullOrEmpty(urlOrHost) && !string.IsNullOrEmpty(user) && !string.IsNullOrEmpty(pass))
                        {
                            Store.SavePassword(urlOrHost, user, pass);
                            added++;
                        }
                    }
                }
            }
            catch { }
            return added;
        }

        private static List<string> ParseCsvRow(string row)
        {
            var list = new List<string>();
            bool inQuotes = false;
            var cur = new StringBuilder();
            for (int i = 0; i < row.Length; i++)
            {
                char ch = row[i];
                if (ch == '"')
                {
                    if (inQuotes && i + 1 < row.Length && row[i + 1] == '"')
                    {
                        cur.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if (ch == ',' && !inQuotes)
                {
                    list.Add(cur.ToString());
                    cur.Clear();
                }
                else
                {
                    cur.Append(ch);
                }
            }
            list.Add(cur.ToString());
            return list;
        }
    }
}
