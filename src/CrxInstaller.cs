using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SearchBrowser
{
    // Ported directly from driceroland/Search Sources/Search/Crx.swift
    public static class CrxInstaller
    {
        public const string ChromeVersion = "156.0.0.0";
        private static readonly Regex IdRegex = new Regex(@"(?<![a-z])([a-p]{32})(?![a-z])", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static string ExtractExtensionId(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return null;
            var match = IdRegex.Match(input.Trim());
            return match.Success ? match.Groups[1].Value.ToLowerInvariant() : null;
        }

        public static string BuildDownloadUrl(string extId)
        {
            string xParam = Uri.EscapeDataString("id=" + extId + "&installsource=ondemand&uc");
            return "https://clients2.google.com/service/update2/crx?response=redirect&os=win&arch=x64&os_arch=x86_64&nacl_arch=x86-64&prod=chromecrx&prodchannel=&prodversion=" +
                   ChromeVersion + "&acceptformat=crx2,crx3&x=" + xParam;
        }

        public static async Task<ExtensionInfo> InstallFromWebStoreAsync(string inputOrUrl)
        {
            string extId = ExtractExtensionId(inputOrUrl);
            if (string.IsNullOrEmpty(extId))
                throw new Exception("That isn't a valid Chrome Web Store link or 32-character extension ID.");

            string downloadUrl = BuildDownloadUrl(extId);
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            byte[] crxBytes;
            using (var wc = new WebClient())
            {
                wc.Headers[HttpRequestHeader.UserAgent] = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/" + ChromeVersion + " Safari/537.36";
                crxBytes = await wc.DownloadDataTaskAsync(downloadUrl);
            }

            if (crxBytes == null || crxBytes.Length < 16)
                throw new Exception("The Chrome Web Store returned an empty response for ID: " + extId);
            if (crxBytes.Length > 50 * 1024 * 1024)
                throw new Exception("Extension package exceeds the 50 MB safety limit.");

            byte[] zipBytes = ExtractVerifiedZipFromCrx(crxBytes, extId);
            string destFolder = Path.Combine(Store.ExtensionsDir, extId);
            if (Directory.Exists(destFolder))
                Directory.Delete(destFolder, true);
            Directory.CreateDirectory(destFolder);

            string tempZip = Path.Combine(Path.GetTempPath(), "search-crx-" + Guid.NewGuid().ToString("N") + ".zip");
            try
            {
                File.WriteAllBytes(tempZip, zipBytes);
                ExtractZipSafely(tempZip, destFolder);
            }
            finally
            {
                if (File.Exists(tempZip)) File.Delete(tempZip);
            }

            return RegisterUnpackedFolder(destFolder, extId);
        }

        private static void ExtractZipSafely(string zipPath, string destFolder)
        {
            string fullDestRoot = Path.GetFullPath(destFolder);
            if (!fullDestRoot.EndsWith(Path.DirectorySeparatorChar.ToString()))
                fullDestRoot += Path.DirectorySeparatorChar;

            using (var archive = ZipFile.OpenRead(zipPath))
            {
                foreach (var entry in archive.Entries)
                {
                    if (string.IsNullOrEmpty(entry.Name)) continue;
                    string destPath = Path.GetFullPath(Path.Combine(destFolder, entry.FullName));
                    if (!destPath.StartsWith(fullDestRoot, StringComparison.OrdinalIgnoreCase))
                        throw new Exception("Blocked unsafe archive entry (path traversal): " + entry.FullName);
                    string dir = Path.GetDirectoryName(destPath);
                    if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                    entry.ExtractToFile(destPath, true);
                }
            }
        }

        public static ExtensionInfo RegisterUnpackedFolder(string folderPath, string explicitId = null)
        {
            string manifestPath = Path.Combine(folderPath, "manifest.json");
            if (!File.Exists(manifestPath))
                throw new Exception("No manifest.json found in the extension folder.");

            string manifestText = File.ReadAllText(manifestPath, Encoding.UTF8);
            string name = ExtractManifestField(manifestText, "name") ?? Path.GetFileName(folderPath);
            string version = ExtractManifestField(manifestText, "version") ?? "1.0";
            string popupRel = ExtractManifestField(manifestText, "default_popup") ?? "";

            if (name.StartsWith("__MSG_"))
                name = ResolveLocalizedMessage(folderPath, name) ?? Path.GetFileName(folderPath);

            string id = explicitId ?? ComputeUnpackedId(folderPath);
            var existing = Store.Extensions.FirstOrDefault(e => string.Equals(e.Id, id, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
                Store.Extensions.Remove(existing);

            var info = new ExtensionInfo
            {
                Id = id,
                Name = name,
                Version = version,
                FolderPath = folderPath,
                PopupRelativePath = popupRel,
                RuntimeExtensionId = id,
                Enabled = true
            };
            Store.Extensions.Add(info);
            Store.SaveExtensions();
            return info;
        }

        private static byte[] ExtractVerifiedZipFromCrx(byte[] crx, string expectedId)
        {
            if (crx.Length <= 12 || crx[0] != 'C' || crx[1] != 'r' || crx[2] != '2' || crx[3] != '4')
                throw new Exception("Downloaded package is not a valid Chrome CRX file.");

            uint version = BitConverter.ToUInt32(crx, 4);
            int zipStartOffset;

            if (version == 3)
            {
                int headerSize = (int)BitConverter.ToUInt32(crx, 8);
                if (12 + headerSize > crx.Length)
                    throw new Exception("Corrupt CRX3 header size.");
                zipStartOffset = 12 + headerSize;
            }
            else if (version == 2)
            {
                int pubKeyLen = (int)BitConverter.ToUInt32(crx, 8);
                int sigLen = (int)BitConverter.ToUInt32(crx, 12);
                zipStartOffset = 16 + pubKeyLen + sigLen;
            }
            else
            {
                throw new Exception("Unsupported CRX version: " + version);
            }

            if (zipStartOffset <= 12 || zipStartOffset >= crx.Length)
                throw new Exception("Corrupt CRX payload offset.");
            int zipLen = crx.Length - zipStartOffset;
            byte[] zipBytes = new byte[zipLen];
            Buffer.BlockCopy(crx, zipStartOffset, zipBytes, 0, zipLen);
            return zipBytes;
        }

        public static string ComputeUnpackedId(string folderPath)
        {
            using (var sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(Path.GetFullPath(folderPath)));
                char[] chars = new char[32];
                for (int i = 0; i < 16; i++)
                {
                    chars[i * 2] = (char)('a' + (hash[i] >> 4));
                    chars[i * 2 + 1] = (char)('a' + (hash[i] & 0x0F));
                }
                return new string(chars);
            }
        }

        private static string ExtractManifestField(string json, string field)
        {
            var m = Regex.Match(json, "\"" + Regex.Escape(field) + "\"\\s*:\\s*\"([^\"]+)\"");
            return m.Success ? m.Groups[1].Value : null;
        }

        private static string ResolveLocalizedMessage(string folderPath, string msgTag)
        {
            try
            {
                string key = msgTag.Replace("__MSG_", "").TrimEnd('_');
                string localesDir = Path.Combine(folderPath, "_locales");
                if (!Directory.Exists(localesDir)) return null;
                string enPath = Path.Combine(localesDir, "en", "messages.json");
                if (!File.Exists(enPath))
                {
                    var anyLoc = Directory.GetDirectories(localesDir).FirstOrDefault();
                    if (anyLoc != null) enPath = Path.Combine(anyLoc, "messages.json");
                }
                if (!File.Exists(enPath)) return null;
                string text = File.ReadAllText(enPath, Encoding.UTF8);
                var m = Regex.Match(text, "\"" + Regex.Escape(key) + "\"\\s*:\\s*\\{[^\\}]*\"message\"\\s*:\\s*\"([^\"]+)\"", RegexOptions.IgnoreCase);
                return m.Success ? m.Groups[1].Value : null;
            }
            catch { return null; }
        }
    }
}
