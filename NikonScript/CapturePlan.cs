﻿using NikonScript.Plans;
using System.Reflection.Metadata.Ecma335;
using System.Text.RegularExpressions;

namespace NikonScript
{
    public class CapturePlan
    {
        public CapturePlan(string[] planContent) { 
            Parse(planContent);
        }

        protected Func<string, string>? _dispatcher = null;
        protected Statement? _planStart = null;
        protected Statement? _planCurrent = null;
        protected bool _cancelPlan = false;
        protected object _internalLock = new ();
        protected ManualResetEvent _signalExit = new(false);

        public void Parse(string[] planContent)
        {
            Statement? list = null;
            Statement? tail = null;

            Action<Statement> appendStatement = (s) =>
            {
                if (null == list)
                {
                    list = s;
                    tail = s;
                    return;
                }

                if (null == tail)
                {
                    throw new InvalidOperationException("missing item at the end of capture plan while parsing statements");
                }

                if (s is LoopSegment)
                {
                    tail.next = s;
                    tail = s;
                    return;
                }

                if (tail is LoopSegment)
                {
                    var loop = (LoopSegment)tail;
                    if (null == loop.children)
                    {
                        loop.children = s;
                        loop.tail = s;
                    }
                    else
                    {
                        if (loop.tail == null)
                        {
                            throw new InvalidOperationException("missing item at the end of capture plan while parsing loop items");
                        }

                        loop.tail.next = s;
                        loop.tail = s;
                    }
                    return;
                }

                tail.next = s;
                tail = s;
            };

            foreach (var rawline in planContent)
            {
                if (string.IsNullOrWhiteSpace(rawline)) { continue; }

                var line = rawline.Trim().ToLower();
                var parts = line.Split(' ');

                if (parts[0].EndsWith(':'))
                {
                    if (parts[0].Equals("loop:"))
                    {
                        LoopSegment loopItem = new LoopSegment();
                        switch (parts.Length)
                        {
                            case 2:
                                long l;
                                if (long.TryParse(parts[1], out l))
                                {
                                    loopItem.max = l;
                                }
                                else
                                {
                                    throw new ArgumentOutOfRangeException($"unable to parse term \"{parts[1]}\" to determine loop count");
                                }
                                break;
                            case 1:
                                break;
                            default:
                                throw new ArgumentOutOfRangeException($"encountered unsupported syntax for loop for statement \"{line}\"");
                        }
                        appendStatement(loopItem);
                    }
                    else
                    {
                        throw new ArgumentException($"encountered unknown directive \"{parts[0]}\"");
                    }
                }
                else
                {
                    switch (parts[0])
                    {
                        case "rem":
                        case "#":
                        case "//":
                            // These are comments and don't get handled by the script host.
                            break;
                        case "connect":
                            if (parts.Length != 2)
                            {
                                throw new ArgumentException($"encountered unsupported syntax for statement \"{line}\" to connect to a camera");
                            }
                            switch (parts[1])
                            {
                                case "d500":
                                case "z7":
                                case "z7ii":
                                    break;
                                default:
                                    throw new ArgumentException($"encountered unknown camera \"{parts[1]}\"");
                            }
                            Statement statementConnect = new Statement();
                            statementConnect.cmd = "connect";
                            statementConnect.invocation = $"connect {parts[1]}";
                            appendStatement(statementConnect);
                            break;
                        case "set_aperture":
                            if (parts.Length != 2)
                            {
                                throw new ArgumentException($"encountered unsupported syntax for statement \"{line}\" to set aperture");
                            }
                            Regex reAperture = new Regex("^((f/?)?(\\d+\\.?\\d*))", RegexOptions.IgnoreCase);
                            if (!reAperture.IsMatch(parts[1]))
                            {
                                throw new ArgumentException($"encountered unsupported syntax for statement \"{line}\" to set aperture");
                            }
                            var matchAperture = reAperture.Match(parts[1]);
                            decimal dAperture = decimal.Parse(matchAperture.Groups[3].Value);
                            Statement statementSetAperture = new Statement();
                            statementSetAperture.cmd = "set_aperture";
                            statementSetAperture.invocation = $"set_aperture {dAperture}";
                            appendStatement(statementSetAperture);
                            break;
                        case "set_iso":
                            if (parts.Length != 2)
                            {
                                throw new ArgumentException($"encountered unsupported syntax for statement \"{line}\" to set iso");
                            }
                            Regex reIso = new Regex("^((hi-\\d(\\.\\d)?)|(lo-\\d(\\.\\d)?)|(\\d+))", RegexOptions.IgnoreCase);
                            if (!reIso.IsMatch(parts[1]))
                            {
                                throw new ArgumentException($"encountered unsupported syntax for statement \"{line}\" to set iso");
                            }
                            var matchIso = reIso.Match(parts[1]);
                            Statement statementSetIso = new Statement();
                            statementSetIso.cmd = "set_iso";
                            statementSetIso.invocation = $"set_iso {matchIso.Groups[0].Value}";
                            appendStatement(statementSetIso);
                            break;
                        case "set_shutter":
                            if (parts.Length != 2)
                            {
                                throw new ArgumentException($"encountered unsupported syntax for statement \"{line}\" to set shutter speed");
                            }
                            Regex reShutter = new Regex("^((1\\/\\d+\\.?\\d?)|(\\d(\\.\\d)?))[s]?", RegexOptions.IgnoreCase);
                            if (!reShutter.IsMatch(parts[1]))
                            {
                                throw new ArgumentException($"encountered unsupported syntax for statement \"{line}\" to set shutter speed");
                            }
                            var matchShutter = reShutter.Match(parts[1]);
                            Statement statementSetShutter = new Statement();
                            statementSetShutter.cmd = "set_shutter";
                            statementSetShutter.invocation = $"set_Shutter {matchShutter.Groups[1].Value}";
                            appendStatement(statementSetShutter);
                            break;
                        case "capture":
                            if (parts.Length != 1)
                            {
                                throw new ArgumentException($"encountered unsupported syntax for statement \"{line}\" to trigger capture/shutter");
                            }
                            Statement statementCapture = new Statement();
                            statementCapture.cmd = "capture";
                            statementCapture.invocation = $"capture";
                            appendStatement(statementCapture);
                            break;
                        case "wait":
                            if (parts.Length != 2)
                            {
                                throw new ArgumentException($"encountered unsupported syntax for statement \"{line}\" for wait statement");
                            }
                            int checkDuration;
                            if (!int.TryParse(parts[1], out checkDuration) || (checkDuration < 0))
                            {
                                throw new ArgumentException($"dureation \"{parts[1]}\" specified for wait statement is disallowed or not recognized");
                            }
                            Statement statementWait = new Statement();
                            statementWait.cmd = "wait";
                            statementWait.intParam = checkDuration;
                            statementWait.invocation = $"wait {checkDuration}";
                            appendStatement(statementWait);
                            break;
                        case "echo":
                        case "print":
                            Statement statementEcho = new Statement();
                            statementEcho.cmd = "print";
                            statementEcho.stringParam = line.Substring(parts[0].Length + 1);
                            statementEcho.invocation = $"print {statementEcho.stringParam}";
                            appendStatement(statementEcho);
                            break;
                        default:
                            throw new ArgumentException($"encountered unsupported statement \"{line}\"");
                    }
                }
            }

            _planStart = list;
            _planCurrent = list;
        }

        public void Start(Func<string, string> dispatcher)
        {
            if(null != _dispatcher) { return; }

            _dispatcher = dispatcher;

            if ((null == _planStart) || (null == _planCurrent)) { return; }

            do
            {
                Func<string, string>? remote = null;

                Func<bool> fnCheckForClose = () =>
                {
                    bool shouldCancel = false;
                    lock (_internalLock)
                    {
                        shouldCancel = _cancelPlan;
                    }
                    if(shouldCancel)
                    {
                        if(null != remote)
                        {
                            remote("disconnect");
                        }
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                };

                Func<Statement, bool> fnSpecialCommandHandled = (cmd) =>
                {
                    switch(cmd.cmd)
                    {
                        case "wait":
                            if(cmd.intParam.HasValue)
                            {
                                Console.ForegroundColor = ConsoleColor.Gray;
                                Console.WriteLine($"waiting {cmd.intParam.Value} seconds");
                                Console.ForegroundColor = ConsoleColor.White;
                                if(_signalExit.WaitOne(cmd.intParam.Value * 1000))
                                {
                                    if (null != remote)
                                    {
                                        remote("disconnect");
                                    }
                                    Stop();
                                }
                                Console.ForegroundColor = ConsoleColor.Gray;
                                Console.WriteLine($"wait completed");
                                Console.ForegroundColor = ConsoleColor.White;
                            }
                            return true;
                        case "print":
                            if(!string.IsNullOrWhiteSpace(cmd.stringParam))
                            {
                                // TODO: maybe add real console color support?
                                Console.ForegroundColor = ConsoleColor.Magenta;
                                Console.WriteLine($"{cmd.stringParam}");
                                Console.ForegroundColor = ConsoleColor.White;
                            }
                            return true;
                            break;
                        default:
                            return false;
                    }
                };

                var maybeLoop = _planCurrent as LoopSegment;
                if (maybeLoop != null)
                {
                    Func<bool> fnExecLoop = () =>
                    {
                        Statement? _planInner = maybeLoop.children;
                        while (_planInner != null)
                        {
                            if (_planInner is LoopSegment)
                            {
                                throw new InvalidOperationException("nested loops not supported yet for capture plans");
                            }

                            if (!fnSpecialCommandHandled(_planInner))
                            {
                                lock (_internalLock)
                                {
                                    remote = _dispatcher;
                                    if (null == remote)
                                    {
                                        return false;
                                    }
                                }

                                var response = remote(_planInner.invocation);
                                if (!string.Equals((response ?? string.Empty).ToLower(), "ready"))
                                {
                                    Stop();
                                    return false;
                                }

                                bool shouldCancel = false;
                                lock (_internalLock)
                                {
                                    shouldCancel = _cancelPlan;
                                }
                            }

                            _planInner = _planInner.next;
                        }

                        return true;
                    };

                    long lmax = maybeLoop.max ?? long.MaxValue;
                    for (long l = 0; l < lmax; l++)
                    {
                        if (!fnExecLoop())
                        {
                            return;
                        }
                    }
                }
                else
                {
                    if (!fnSpecialCommandHandled(_planCurrent))
                    {
                        lock (_internalLock)
                        {
                            remote = _dispatcher;
                            if (null == remote)
                            {
                                return;
                            }
                        }

                        var response = remote(_planCurrent.invocation);
                        if (!string.Equals((response ?? string.Empty).ToLower(), "ready"))
                        {
                            Stop();
                            return;
                        }
                    }
                }

                _planCurrent = _planCurrent.next;
            }
            while (_planCurrent != null);
        }

        public void Stop()
        {
            _signalExit.Set();
            var finalDispatch = _dispatcher;
            lock (_internalLock)
            {
                _cancelPlan = true;
                _dispatcher = null;
            }

            if (null != finalDispatch)
            {
                finalDispatch("disconnect");
            }
        }

        public void Clear()
        {
            lock (_internalLock)
            {
                if (null != _dispatcher)
                {
                    throw new InvalidOperationException("cannot clear script plan while dispatcher is live");
                }
            }

            _planStart = null;
            _planCurrent = null;
        }
    }
}
