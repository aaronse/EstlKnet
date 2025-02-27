using System.Diagnostics;
using System.Text;

namespace EstlKnet
{
    class Log
    {
        private static DateTime _startTime = DateTime.Now;
        private static int _warnCount;
        private static int _errorCount;
        private static StringBuilder _sbErrors = new StringBuilder();

        public static bool Verbose { get; set; } = true;
        public static bool InfoEnabled { get; set; } = true;
        public static bool LogTime { get; set; } = true;
        public static bool LogMessageType { get; set; } = true;
        public static bool LogRelativeTime { get; set; } = false;
        public static bool ConsoleOutput { get; set; } = true;
        public static bool HtmlOutput { get; set; } = false;
        public static int WarnCount => _warnCount;
        public static int ErrorCount => _errorCount;

        public static void DumpErrors(bool resetErrorCount)
        {
            if (ConsoleOutput) Console.ForegroundColor = ConsoleColor.Red;

            Console.WriteLine(_sbErrors.ToString());

            if (HtmlOutput)
            {
                Trace.WriteLine("<br/>" + _sbErrors.ToString().Replace("\n", "\n<br/>", StringComparison.Ordinal));
            }
            else
            {
                Trace.WriteLine(_sbErrors.ToString());
            }

            Log.Error($"{Log.ErrorCount} Error(s)");

            if (ConsoleOutput) Console.ResetColor();

            if (resetErrorCount)
            {
                _sbErrors.Clear();
                _errorCount = 0;
            }
        }

        public static void Error(string msg)
        {
            _errorCount++;
            LogMessage("[E]", msg);
        }

        public static void Error(string msg, params object[] args)
        {
            _errorCount++;
            LogMessage("[E]", string.Format(msg, args));
        }

        public static void Warn(string msg)
        {
            _warnCount++;
            LogMessage("[W]", msg);
        }

        public static void Warn(string msg, params object[] args)
        {
            _warnCount++;
            LogMessage("[W]", string.Format(msg, args));
        }

        public static void Info(string msg)
        {
            if (!InfoEnabled) return;
            LogMessage("[I]", msg);
        }

        public static void Info(string msg, params object[] args)
        {
            if (!InfoEnabled) return;
            LogMessage("[I]", string.Format(msg, args));
        }

        public static void Debug(string msg)
        {
            if (!Verbose) return;
            LogMessage("[D]", msg);
        }

        public static void Debug(string msg, params object[] args)
        {
            if (!Verbose) return;
            LogMessage("[D]", string.Format(msg, args));
        }

        private static void LogMessage(string msgType, string msg)
        {
            ConsoleColor color = msgType switch
            {
                "[E]" => ConsoleColor.Red,
                "[W]" => ConsoleColor.Yellow,
                "[I]" => ConsoleColor.White,
                "[D]" => ConsoleColor.DarkGray,
                _ => ConsoleColor.Gray
            };

            if (ConsoleOutput) Console.ForegroundColor = color;
            Write(_sbErrors, msgType, msg);
            if (ConsoleOutput) Console.ResetColor();
        }

        private static void Write(StringBuilder? sbByLogType, string msgType, string msg)
        {
            StringBuilder sb = new StringBuilder();
            if (LogMessageType) sb.Append(msgType + " ");
            if (LogTime) sb.Append(LogRelativeTime
                ? DateTime.Now.Subtract(_startTime).ToString(@"hh\:mm\:ss\.fff")
                : DateTime.Now.ToString("HH:mm:ss.ff"));

            sb.Append(" ").Append(msg);

            sbByLogType?.AppendLine(sb.ToString());

            if (ConsoleOutput) Console.WriteLine(HtmlOutput ? "<br/>" + sb : sb.ToString());
            Trace.WriteLine(HtmlOutput ? "<br/>" + sb : sb.ToString());
        }

        public static void SetLogFile(string logFile)
        {
            Trace.Listeners.Clear();
            if (!string.IsNullOrEmpty(logFile))
            {
                Trace.Listeners.Add(new TextWriterTraceListener(logFile));
            }
        }

        public static void Close()
        {
            foreach (TraceListener tl in Trace.Listeners)
            {
                tl.Flush();
            }
        }
    }
}
