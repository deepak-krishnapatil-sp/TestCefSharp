
using System;
using System.IO;
using System.Text;


namespace SPLoggerLib
{

    public interface ISPLogger
    {
        public enum LogLevel
        {
            Fatal,
            Error,
            Warn,
            Info,
            Debug,
            Trace
        }

        void Log(LogLevel level, string message, params object[] args);
        void Fatal(string message, params object[] args);
        void Error(string message, params object[] args);
        void Warn(string message, params object[] args);
        void Info(string message, params object[] args);
        void Debug(string message, params object[] args);
        void Trace(string message, params object[] args);
    }

    public class SPLogger : ISPLogger
    {
        private const string defaultFilePath = "debug.log";
        private const ISPLogger.LogLevel defaultLogLevel = ISPLogger.LogLevel.Debug;
        private const long defaultMaxFileSize = 1024 * 1024; // 1MB

        private readonly string _logFilePath;
        private readonly ISPLogger.LogLevel _minLogLevel;
        private readonly long _maxFileSize;

        // private readonly bool LogToConsole = false; // Toggle for console output

        // Constructor with optional parameters, including max file size for log rotation
        public SPLogger(string logFilePath = defaultFilePath, ISPLogger.LogLevel minLogLevel = defaultLogLevel, long maxFileSize = defaultMaxFileSize)
        {
            _logFilePath = logFilePath;
            _minLogLevel = minLogLevel;
            _maxFileSize = maxFileSize;
        }

        // Method to rotate log file if it exceeds the max file size
        private void RotateLogFile()
        {
            FileInfo logFileInfo = new FileInfo(_logFilePath);
            if (logFileInfo.Exists && logFileInfo.Length >= _maxFileSize)
            {
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string backupFilePath = $"{_logFilePath}_{timestamp}";

                try
                {
                    // Rename the current log file to include a timestamp
                    File.Move(_logFilePath, backupFilePath);
                    Console.WriteLine($"Log file rotated. Old log file moved to: {backupFilePath}");
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Log rotation failed: {ex.Message}");
                }
            }
        }

        public void Log(ISPLogger.LogLevel level, string message, params object[] args)
        {
            if (level > _minLogLevel)
                return;

            string formattedMessage = args?.Length > 0 ? string.Format(message, args) : message;
            string logEntry = $"{DateTime.Now:u} [{level}] {formattedMessage}{Environment.NewLine}";

            try
            {
                // Check if log rotation is needed before writing
                RotateLogFile();

                // Write the log entry to the log file
                File.AppendAllText(_logFilePath, logEntry, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Logging failed: {ex.Message}");
            }
        }

        public void Fatal(string message, params object[] args) => Log(ISPLogger.LogLevel.Fatal, message, args);
        public void Error(string message, params object[] args) => Log(ISPLogger.LogLevel.Error, message, args);
        public void Warn(string message, params object[] args) => Log(ISPLogger.LogLevel.Warn, message, args);
        public void Info(string message, params object[] args) => Log(ISPLogger.LogLevel.Info, message, args);
        public void Debug(string message, params object[] args) => Log(ISPLogger.LogLevel.Debug, message, args);
        public void Trace(string message, params object[] args) => Log(ISPLogger.LogLevel.Trace, message, args);
    }
}
