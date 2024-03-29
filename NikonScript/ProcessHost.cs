using System.Diagnostics;
using System.Text;

namespace NikonScript
{
    public class ProcessHost : IDisposable
    {
        public ProcessHost() { }

        private Process? _remote = null;
        private CapturePlan? _plan = null;
        private Thread? _worker = null;

        public void Dispose()
        {
            Stop();
            GC.SuppressFinalize(this);
        }

        public void Start()
        {
            if (_remote != null) { return; }

            _remote = new Process();
            _remote.StartInfo.FileName = @"..\..\..\..\NikonConsoleDriver\bin\Debug\net8.0\NikonConsoleDriver.exe";
            _remote.StartInfo.Arguments = "";
            _remote.StartInfo.UseShellExecute = false;
            _remote.StartInfo.RedirectStandardOutput = true;
            _remote.StartInfo.CreateNoWindow = false;
            _remote.StartInfo.RedirectStandardInput = true;
            _remote.Start();
        }

        protected string RelayCmd(string cmd)
        {
            if (_remote == null) { return string.Empty; }

            try
            {
                _remote.StandardInput.WriteLine(cmd);
            }
            catch (IOException ioex)
            {
                return ioex.ToString();
            }

            // super special case
            if ("disconnect".Equals(cmd))
            {
                Thread.Yield();
                return "(disconnecting)";
            }

            bool ready = false;
            StringBuilder sb = new StringBuilder();
            do
            {
                try
                {
                    var line = _remote.StandardOutput.ReadLine() ?? string.Empty;
                    var scan = line.ToLower();
                    if (!scan.Equals("ready"))
                    {
                        sb.AppendLine(line);
                        if (scan.Contains("exception"))
                        {
                            break;
                        }
                    }
                    else
                    {
                        ready = true;
                    }
                }
                catch(IOException ioex)
                {
                    return ioex.ToString();
                }
            }
            while (!ready);

            try
            {
                if (ready)
                {
                    if (sb.Length > 0)
                    {
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.Write(sb.ToString());
                    }

                    return "Ready";
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.Write(sb.ToString());
                    return sb.ToString();
                }
            }
            finally
            {
                Console.ForegroundColor = ConsoleColor.White;
            }
        }

        public void RunPlan(string planFile)
        {
            if (_remote == null) { return; }
            if (_worker != null)
            {
                throw new InvalidOperationException($"plan launched twice, second invocation is \"{planFile}\"");
            }

            if (!File.Exists(planFile))
            {
                return;
            }

            _worker = new Thread(() =>
            {
                var planCommands = File.ReadAllLines(planFile);

                _plan = new CapturePlan(planCommands);

                _plan.Start((cmd) =>
                {
                    return RelayCmd(cmd);
                });
            });

            _worker.Start();
        }

        public void StopPlan()
        {
            if (null == _plan) { return; }

            _plan.Stop();
            _worker?.Join();

            _plan = null;
            _worker = null;
        }

        public void Stop()
        {
            if (_remote != null)
            {
                _remote.WaitForExit(4000);
                _remote.Close();
                _remote.Dispose();
                _remote = null;
            }
        }
    }
}
