
using System;
using System.IO;
using System.Text;


namespace SPLoggerLib
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
    public interface ISPLogger
    {
        void Log(string message, LogLevel level, params object[] args);
        void LogFatal(string message, params object[] args);
        void LogError(string message, params object[] args);
        void LogWarn(string message, params object[] args);
        void LogInfo(string message, params object[] args);
        void LogDebug(string message, params object[] args);
        void LogTrace(string message, params object[] args);
    }

    public class SPLogger : ISPLogger
    {
        private readonly string _logFilePath;
        private readonly LogLevel _minLogLevel;
        private readonly long _maxFileSize;

        // Constructor with optional parameters, including max file size for log rotation
        public SPLogger(string logFilePath = "debuglog.log", LogLevel minLogLevel = LogLevel.Trace, long maxFileSize = 3 * 1024 * 1024) // 3 MB
        {
            _logFilePath = logFilePath;
            _minLogLevel = minLogLevel;
            _maxFileSize = maxFileSize;
        }

        // Method to rotate log file if it exceeds the max file size
        private void RotateLogFile()
        {
            if (File.Exists(_logFilePath) && new FileInfo(_logFilePath).Length >= _maxFileSize)
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

        public void Log(string message, LogLevel level, params object[] args)
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

        public void LogFatal(string message, params object[] args) => Log(message, LogLevel.Fatal, args);
        public void LogError(string message, params object[] args) => Log(message, LogLevel.Error, args);
        public void LogWarn(string message, params object[] args) => Log(message, LogLevel.Warn, args);
        public void LogInfo(string message, params object[] args) => Log(message, LogLevel.Info, args);
        public void LogDebug(string message, params object[] args) => Log(message, LogLevel.Debug, args);
        public void LogTrace(string message, params object[] args) => Log(message, LogLevel.Trace, args);
    }
}
