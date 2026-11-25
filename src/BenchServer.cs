using System;
using System.IO;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SearchBrowser
{
    // Ported from driceroland/Search Sources/Search/Bench.swift & AgentControl.swift
    // Provides a local Named Pipe ("search-bench") so CLI scripts & AI coding agents can drive Search without closing it.
    public static class BenchServer
    {
        private static CancellationTokenSource _cts;

        public static void Start(MainWindow window)
        {
            if (_cts != null) return;
            _cts = new CancellationTokenSource();
            Task.Run(() => ListenLoopAsync(window, _cts.Token));
        }

        public static void Stop()
        {
            if (_cts != null)
            {
                _cts.Cancel();
                _cts = null;
            }
        }

        private static async Task ListenLoopAsync(MainWindow window, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                bool errored = false;
                try
                {
                    var pipeSec = new PipeSecurity();
                    var currentUser = WindowsIdentity.GetCurrent().User;
                    if (currentUser != null)
                        pipeSec.AddAccessRule(new PipeAccessRule(currentUser, PipeAccessRights.FullControl, AccessControlType.Allow));
                    using (var pipe = new NamedPipeServerStream("search-bench", PipeDirection.InOut, 4, PipeTransmissionMode.Byte, PipeOptions.Asynchronous, 4096, 4096, pipeSec))
                    {
                        await Task.Factory.FromAsync(pipe.BeginWaitForConnection, pipe.EndWaitForConnection, null);
                        if (token.IsCancellationRequested) break;

                        using (var reader = new StreamReader(pipe, Encoding.UTF8, false, 4096, true))
                        using (var writer = new StreamWriter(pipe, new UTF8Encoding(false), 4096, true) { AutoFlush = true })
                        {
                            string cmdLine = await reader.ReadLineAsync();
                            if (!string.IsNullOrWhiteSpace(cmdLine))
                            {
                                string reply = await window.ExecuteBenchCommandAsync(cmdLine.Trim());
                                await writer.WriteLineAsync(reply ?? "OK");
                            }
                        }
                    }
                }
                catch
                {
                    errored = true;
                }
                if (errored)
                {
                    await Task.Delay(500);
                }
            }
        }
    }
}
