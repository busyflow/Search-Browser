using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;

[assembly: AssemblyTitle("Search")]
[assembly: AssemblyDescription("A small, fast, quiet web browser for Windows (WebView2 native port of driceroland/Search)")]
[assembly: AssemblyCompany("Office Commun / Search Windows Port")]
[assembly: AssemblyProduct("Search")]
[assembly: AssemblyVersion("1.0.0.0")]

namespace SearchBrowser
{
    public static class Program
    {
        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        [STAThread]
        public static void Main(string[] args)
        {
            try { SetProcessDPIAware(); } catch { }

            if (args != null && args.Any(a => string.Equals(a, "--register", StringComparison.OrdinalIgnoreCase)))
            {
                DefaultBrowserRegistrar.RegisterInWindowsRegistry();
                return;
            }

            List<string> urlsToOpen = new List<string>();
            if (args != null)
            {
                foreach (var arg in args)
                {
                    if (string.IsNullOrWhiteSpace(arg) || arg == "--" || arg.StartsWith("--")) continue;
                    string trimmed = arg.Trim('"');
                    if (trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                        trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                        trimmed.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
                    {
                        urlsToOpen.Add(trimmed);
                    }
                    else if (trimmed.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
                    {
                        urlsToOpen.Add("https://mail.google.com/mail/?extsrc=mailto&url=" + Uri.EscapeDataString(trimmed));
                    }
                    else if (File.Exists(trimmed))
                    {
                        try { urlsToOpen.Add(new Uri(Path.GetFullPath(trimmed)).AbsoluteUri); } catch { }
                    }
                }
            }

            // If another instance of Search is already running and Windows passed us a link, forward it over the named pipe!
            if (urlsToOpen.Count > 0)
            {
                try
                {
                    using (var pipe = new System.IO.Pipes.NamedPipeClientStream(".", "search-bench", System.IO.Pipes.PipeDirection.InOut))
                    {
                        pipe.Connect(250);
                        using (var writer = new StreamWriter(pipe) { AutoFlush = true })
                        using (var reader = new StreamReader(pipe))
                        {
                            foreach (var u in urlsToOpen)
                            {
                                writer.WriteLine("open " + u);
                                reader.ReadLine();
                            }
                        }
                        return;
                    }
                }
                catch { }
            }

            var app = new Application();
            var win = new MainWindow();

            if (urlsToOpen.Count > 0)
            {
                win.Loaded += (s, e) =>
                {
                    foreach (var u in urlsToOpen)
                        win.CreateAndSelectTab(u, false);
                };
            }

            app.Run(win);
        }
    }
}
