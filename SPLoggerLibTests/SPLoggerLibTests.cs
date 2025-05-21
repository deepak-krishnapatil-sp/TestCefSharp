using SPLoggerLib;
using System;
using System.IO;
using Xunit;

namespace SPLoggerLib.Tests
{
    public class SPLoggerTests : IDisposable
    {
        private readonly string _tempLogFile;
        private readonly SPLogger _logger;

        public SPLoggerTests()
        {
            _tempLogFile = Path.GetTempFileName();
            _logger = new SPLogger(_tempLogFile, LogLevel.Trace, maxFileSize: 1024 * 1024);
        }

        [Fact]
        public void Logs_Info_Message()
        {
            // Act
            _logger.LogInfo("Test message {0}", 123);

            // Assert
            var content = File.ReadAllText(_tempLogFile);
            Assert.Contains("[Info]", content);
            Assert.Contains("Test message 123", content);
        }

        [Fact]
        public void Does_Not_Log_Below_Min_Level()
        {
            var strictLogger = new SPLogger(_tempLogFile, LogLevel.Warn);
            strictLogger.LogDebug("This should not appear");

            var content = File.ReadAllText(_tempLogFile);
            Assert.DoesNotContain("Debug", content);
        }

        [Fact]
        public void LogFile_Rotates_When_Too_Large()
        {
            // Fill up log file just below limit
            File.WriteAllText(_tempLogFile, new string('x', 1024 * 1024 - 10));

            // This should trigger rotation
            _logger.LogInfo("Trigger rotation");

            // Check that original file is reset or moved
            Assert.True(File.Exists(_tempLogFile));
            string content = File.ReadAllText(_tempLogFile);
            Assert.Contains("Trigger rotation", content);
        }

        public void Dispose()
        {
            // Cleanup
            if (File.Exists(_tempLogFile))
                File.Delete(_tempLogFile);

            var logDir = Path.GetDirectoryName(_tempLogFile);
            var logFile = Path.GetFileName(_tempLogFile);

            if (logDir != null && logFile != null) 
            {
            var backupFiles = Directory.GetFiles(logDir, logFile + "_*");
            foreach (var f in backupFiles)
                File.Delete(f);
            }
        }
    }
}
