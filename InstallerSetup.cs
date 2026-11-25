using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Windows;

[assembly: AssemblyTitle("Search Browser Setup")]
[assembly: AssemblyDescription("One-click installer for Search Browser for Windows")]
[assembly: AssemblyProduct("Search Browser")]
[assembly: AssemblyVersion("1.0.0.0")]

namespace SearchSetup
{
    public static class SetupProgram
    {
        [STAThread]
        public static void Main(string[] args)
        {
            bool silent = args != null && Array.Exists(args, a => a.Equals("/S", StringComparison.OrdinalIgnoreCase) || a.Equals("--silent", StringComparison.OrdinalIgnoreCase));
            if (silent)
            {
                RunInstall(true, true, true, true);
                return;
            }

            var app = new Application();
            var win = new Window
            {
                Title = "Search Browser Setup",
                Width = 440,
                Height = 330,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                ResizeMode = ResizeMode.NoResize,
                Background = System.Windows.Media.Brushes.White
            };

            var root = new System.Windows.Controls.StackPanel { Margin = new Thickness(24) };
            root.Children.Add(new System.Windows.Controls.TextBlock
            {
                Text = "Install Search Browser",
                FontSize = 20,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 6)
            });
            root.Children.Add(new System.Windows.Controls.TextBlock
            {
                Text = "Native Windows x64 + WebView2 browser (v0.0018)\nDestination: %LOCALAPPDATA%\\Programs\\Search",
                FontSize = 12.5,
                Foreground = System.Windows.Media.Brushes.DimGray,
                Margin = new Thickness(0, 0, 0, 16)
            });

            var chkDesktop = new System.Windows.Controls.CheckBox { Content = "Create Desktop shortcut", IsChecked = true, Margin = new Thickness(0, 0, 0, 8) };
            var chkStart = new System.Windows.Controls.CheckBox { Content = "Create Start Menu shortcut", IsChecked = true, Margin = new Thickness(0, 0, 0, 8) };
            var chkDefault = new System.Windows.Controls.CheckBox { Content = "Open Windows Default Apps after install", IsChecked = true, Margin = new Thickness(0, 0, 0, 8) };
            var chkLaunch = new System.Windows.Controls.CheckBox { Content = "Launch Search when setup finishes", IsChecked = true, Margin = new Thickness(0, 0, 0, 16) };
            root.Children.Add(chkDesktop);
            root.Children.Add(chkStart);
            root.Children.Add(chkDefault);
            root.Children.Add(chkLaunch);

            var statusText = new System.Windows.Controls.TextBlock { Text = "", FontSize = 12, Foreground = System.Windows.Media.Brushes.DimGray, Margin = new Thickness(0, 0, 0, 10) };
            root.Children.Add(statusText);

            var btnRow = new System.Windows.Controls.StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var cancelBtn = new System.Windows.Controls.Button { Content = "Cancel", Width = 84, Height = 30, Margin = new Thickness(0, 0, 8, 0) };
            cancelBtn.Click += (s, e) => win.Close();
            var installBtn = new System.Windows.Controls.Button { Content = "Install", Width = 96, Height = 30, IsDefault = true };
            installBtn.Click += (s, e) =>
            {
                installBtn.IsEnabled = false;
                cancelBtn.IsEnabled = false;
                statusText.Text = "Installing Search Browser...";
                try
                {
                    RunInstall(chkDesktop.IsChecked == true, chkStart.IsChecked == true, chkLaunch.IsChecked == true, chkDefault.IsChecked == true);
                    statusText.Text = "Installation complete!";
                    win.Close();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Search installation failed: " + ex.Message, "Search Setup", MessageBoxButton.OK, MessageBoxImage.Error);
                    installBtn.IsEnabled = true;
                    cancelBtn.IsEnabled = true;
                    statusText.Text = "";
                }
            };
            btnRow.Children.Add(cancelBtn);
            btnRow.Children.Add(installBtn);
            root.Children.Add(btnRow);

            win.Content = root;
            app.Run(win);
        }

        private static void RunInstall(bool desktopShortcut, bool startShortcut, bool launchAfter, bool openDefaultApps)
        {
            string installDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                @"Programs\Search");

            foreach (var proc in Process.GetProcessesByName("Search"))
            {
                try { proc.Kill(); proc.WaitForExit(2000); } catch { }
            }

            Directory.CreateDirectory(installDir);

            string tempZip = Path.Combine(Path.GetTempPath(), "search-payload-" + Guid.NewGuid().ToString("N") + ".zip");
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("payload.zip"))
            {
                if (stream == null)
                    throw new Exception("Embedded payload.zip not found in installer.");
                using (var fs = File.Create(tempZip))
                {
                    stream.CopyTo(fs);
                }
            }

            string fullInstallRoot = Path.GetFullPath(installDir);
            if (!fullInstallRoot.EndsWith(Path.DirectorySeparatorChar.ToString()))
                fullInstallRoot += Path.DirectorySeparatorChar;
            using (var archive = ZipFile.OpenRead(tempZip))
            {
                foreach (var entry in archive.Entries)
                {
                    if (string.IsNullOrEmpty(entry.Name)) continue;
                    string destPath = Path.GetFullPath(Path.Combine(installDir, entry.FullName));
                    if (!destPath.StartsWith(fullInstallRoot, StringComparison.OrdinalIgnoreCase))
                        throw new Exception("Blocked unsafe installer archive entry: " + entry.FullName);
                    Directory.CreateDirectory(Path.GetDirectoryName(destPath));
                    entry.ExtractToFile(destPath, true);
                }
            }
            try { File.Delete(tempZip); } catch { }

            string installedExe = Path.Combine(installDir, "Search.exe");
            string installedIco = Path.Combine(installDir, "search.ico");

            var regProc = Process.Start(new ProcessStartInfo
            {
                FileName = installedExe,
                Arguments = "--register",
                UseShellExecute = false,
                CreateNoWindow = true
            });
            if (regProc != null) regProc.WaitForExit(4000);

            if (desktopShortcut)
                CreateShortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Search.lnk"), installedExe, installDir, installedIco);
            if (startShortcut)
                CreateShortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Search.lnk"), installedExe, installDir, installedIco);

            if (launchAfter)
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = installedExe,
                    WorkingDirectory = installDir,
                    UseShellExecute = true
                });
            }

            if (openDefaultApps)
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "ms-settings:defaultapps?registeredAppUser=Search",
                    UseShellExecute = true
                });
            }
        }

        private static void CreateShortcut(string shortcutPath, string targetExe, string workingDir, string iconPath)
        {
            try
            {
                Type t = Type.GetTypeFromProgID("WScript.Shell");
                dynamic shell = Activator.CreateInstance(t);
                var lnk = shell.CreateShortcut(shortcutPath);
                lnk.TargetPath = targetExe;
                lnk.WorkingDirectory = workingDir;
                lnk.IconLocation = File.Exists(iconPath) ? iconPath : (targetExe + ",0");
                lnk.Description = "Search — A small, fast, quiet web browser for Windows";
                lnk.Save();
            }
            catch { }
        }
    }
}
