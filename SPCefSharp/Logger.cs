using System;
using System.IO;

namespace SPCefSharp.WinForms
{
    public enum LogSeverity
    {
        Debug,
        Info,
        Warning,
        Error
    }

    public static class Logger
    {
        private static readonly string LogFilePath = Globals.LogFilePath;
        private static readonly bool LogToConsole = false; // Toggle for console output

        static Logger()
        {
            // Ensure the log file directory exists
            string dir = Path.GetDirectoryName(LogFilePath);
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            // Clear the log file on startup and write initial message
            File.WriteAllText(LogFilePath, $"SPCefSharp started at {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n");
        }

        private static void Log(LogSeverity severity, string format, params object[] args)
        {
            string message = args.Length > 0 ? string.Format(format, args) : format;
            string logEntry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{severity}] - {message}";

            // Write to file
            try
            {
                File.AppendAllText(LogFilePath, logEntry + "\n");
            }
            catch (Exception ex)
            {
                // Fallback to console if file write fails
                Console.WriteLine($"Failed to write to log file: {ex.Message}");
                Console.WriteLine(logEntry);
            }

            // Write to console if enabled
            if (LogToConsole)
            {
                Console.WriteLine(logEntry);
            }
        }

        public static void Debug(string format, params object[] args) => Log(LogSeverity.Debug, format, args);
        public static void Info(string format, params object[] args) => Log(LogSeverity.Info, format, args);
        public static void Warning(string format, params object[] args) => Log(LogSeverity.Warning, format, args);
        public static void Error(string format, params object[] args) => Log(LogSeverity.Error, format, args);

    }
}
