using SPLoggerLib;


namespace SPCEFSharpLib
{
    public class CEFSharpLib
    {
        private readonly ISPLogger _logger;

        public CEFSharpLib(ISPLogger logger)
        {
            _logger = logger;
        }

        public void LogInfoMessage()
        {
            _logger.LogInfo("hello from {0}", "class lib");
        }

    }
}
