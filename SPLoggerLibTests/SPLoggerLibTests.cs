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
            _logger = new SPLogger(_tempLogFile, ISPLogger.LogLevel.Trace, maxFileSize: 1024 * 1024);
        }

        [Fact]
        public void Logs_Fatal_Message()
        {
            // Act
            _logger.Fatal("Test message {0}", "FATAL");

            // Assert
            var content = File.ReadAllText(_tempLogFile);
            Assert.Contains("[Fatal]", content);
            Assert.Contains("Test message FATAL", content);
        }

        [Fact]
        public void Logs_Error_Message()
        {
            // Act
            _logger.Error("Test message {0}", "ERROR");

            // Assert
            var content = File.ReadAllText(_tempLogFile);
            Assert.Contains("[Error]", content);
            Assert.Contains("Test message ERROR", content);
        }

        [Fact]
        public void Logs_Warn_Message()
        {
            // Act
            _logger.Warn("Test message {0}", "WARNING");

            // Assert
            var content = File.ReadAllText(_tempLogFile);
            Assert.Contains("[Warn]", content);
            Assert.Contains("Test message WARNING", content);
        }

        [Fact]
        public void Logs_Info_Message()
        {
            // Act
            _logger.Info("Test message {0}", "INFO");

            // Assert
            var content = File.ReadAllText(_tempLogFile);
            Assert.Contains("[Info]", content);
            Assert.Contains("Test message INFO", content);
        }

        [Fact]
        public void Logs_Debug_Message()
        {
            // Act
            _logger.Debug("Test message {0}", "DEBUG");

            // Assert
            var content = File.ReadAllText(_tempLogFile);
            Assert.Contains("[Debug]", content);
            Assert.Contains("Test message DEBUG", content);
        }

        [Fact]
        public void Logs_Trace_Message()
        {
            // Act
            _logger.Trace("Test message {0}", "TRACE");

            // Assert
            var content = File.ReadAllText(_tempLogFile);
            Assert.Contains("[Trace]", content);
            Assert.Contains("Test message TRACE", content);
        }

        [Fact]
        public void Does_Not_Log_Below_Min_Level()
        {
            var strictLogger = new SPLogger(_tempLogFile, ISPLogger.LogLevel.Warn);
            strictLogger.Debug("This should not appear");

            var content = File.ReadAllText(_tempLogFile);
            Assert.DoesNotContain("Debug", content);
        }

        [Fact]
        public void LogFile_Rotates_When_Too_Large()
        {
            // Fill up log file just below limit
            File.WriteAllText(_tempLogFile, new string('x', 1024 * 1024 + 10));

            // This should trigger rotation
            _logger.Info("Trigger rotation");

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
