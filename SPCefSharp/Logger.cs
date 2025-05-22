using SPLoggerLib;
using System;
using System.IO;

namespace SPCefSharp.WinForms
{
    public static class Logger
    {

        private static readonly ISPLogger logger;
        private static readonly ISPLogger.LogLevel minLogLevel = ISPLogger.LogLevel.Debug;
        private static readonly long logFileSize = 3 * 1024 * 1024; // 3MB

        public static ISPLogger GetLoggerObject()
        {
            return logger;
        }

        static Logger()
        {
            logger = new SPLogger(Globals.LogFilePath, minLogLevel, logFileSize);

            // Clear the log file on startup and write initial message
            logger.Info($"SPCefSharp started at {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n");
        }

        public static void Log(ISPLogger.LogLevel severity, string format, params object[] args)
        {
            // Write to file
            try
            {
                logger.Log(severity, format, args);
            }
            catch (Exception ex)
            {
                string message = args.Length > 0 ? string.Format(format, args) : format;
                string logEntry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{severity}] - {message}";
                // Fallback to console if file write fails
                Console.WriteLine($"Failed to write to log file: {ex.Message}");
                Console.WriteLine(logEntry);
            }
        }

        public static void Debug(string format, params object[] args) => logger.Debug(format, args);
        public static void Info(string format, params object[] args) => logger.Info(format, args);
        public static void Warning(string format, params object[] args) => logger.Warn(format, args);
        public static void Error(string format, params object[] args) => logger.Error(format, args);

    }
}
