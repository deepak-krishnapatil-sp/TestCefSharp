using System.Reflection;
using SPLoggerLib;



namespace SPCEFSharpLib
{
    public class CEFSharpLib
    {
        private readonly ISPLogger _logger;

        private string cefDirPath;
        private Assembly cefSharpAssembly;
        private Assembly cefSharpWinFormsAssembly;
        private Assembly cefSharpCoreAssembly;
        private object browser;
        private Type browserType = null;

        public CEFSharpLib(ISPLogger logger, string CefDirPath)
        {
            _logger = logger;

            this.cefDirPath = CefDirPath;

            cefSharpWinFormsAssembly = Assembly.LoadFrom(Path.Combine(cefDirPath, "CefSharp.WinForms.dll"));
            cefSharpCoreAssembly = Assembly.LoadFrom(Path.Combine(cefDirPath, "CefSharp.Core.dll"));
            cefSharpAssembly = Assembly.LoadFrom(Path.Combine(cefDirPath, "CefSharp.dll"));
        }

        public void LogInfoMessage()
        {
            _logger.Info("hello from {0}", "class lib");
        }


        public string GetCefVersions()
        {
            Type cefType = cefSharpCoreAssembly.GetType("CefSharp.Cef");

            PropertyInfo chromiumVersionProperty = cefType.GetProperty("ChromiumVersion", BindingFlags.Public | BindingFlags.Static);
            PropertyInfo cefVersionProperty = cefType.GetProperty("CefVersion", BindingFlags.Public | BindingFlags.Static);
            PropertyInfo cefSharpVersionProperty = cefType.GetProperty("CefSharpVersion", BindingFlags.Public | BindingFlags.Static);

            if (chromiumVersionProperty == null || cefVersionProperty == null || cefSharpVersionProperty == null)
            {
                return "Version information unavailable.";
            }

            string chromiumVersion = (string)chromiumVersionProperty.GetValue(null);
            string cefVersionTmp = (string)cefVersionProperty.GetValue(null);
            string cefVersion = cefVersionTmp.Substring(0, cefVersionTmp.IndexOf("+"));
            string cefSharpVersion = (string)cefSharpVersionProperty.GetValue(null);

            return string.Format("Chromium: {0}, CEF: {1}, CefSharp: {2}",
                chromiumVersion, cefVersion, cefSharpVersion);
        }

        public object CreateCefSettings(string cachePath)
        {
            Type cefSettingsType = cefSharpWinFormsAssembly.GetType("CefSharp.WinForms.CefSettings");

            if (cefSettingsType == null)
            {
                throw new Exception("CefSettings type not found in CefSharp.WinForms.dll.");
            }

            object cefSettings = Activator.CreateInstance(cefSettingsType);

            PropertyInfo cachePathProp = cefSettingsType.GetProperty("CachePath");

            if (cachePathProp != null)
            {
                cachePathProp.SetValue(cefSettings, cachePath);
            }

            return cefSettings;
        }

        public void AddCommandLineArgs(object cefSettings, string command, string value = null)
        {
            Type cefSettingsType = cefSharpWinFormsAssembly.GetType("CefSharp.WinForms.CefSettings");

            PropertyInfo commandLineArgsProperty = cefSettingsType.GetProperty("CefCommandLineArgs");
            if (commandLineArgsProperty == null)
            {
                throw new Exception("CefCommandLineArgs property not found.");
            }

            object commandLineArgs = commandLineArgsProperty.GetValue(cefSettings);
            if (commandLineArgs == null)
            {
                throw new Exception("CefCommandLineArgs is null.");
            }

            Type commandLineArgsType = commandLineArgs.GetType();
            MethodInfo addMethod = commandLineArgsType.GetMethod("Add", new[] { typeof(string), typeof(string) });

            if (addMethod == null)
            {
                throw new Exception("Add method not found on CefCommandLineArgs.");
            }




            addMethod.Invoke(commandLineArgs, new object[] { command, value });

        }

        public void PrintCommandLineArgs(object cefSettings)
        {
            Type cefSettingsType = cefSharpWinFormsAssembly.GetType("CefSharp.WinForms.CefSettings");

            PropertyInfo commandLineArgsProperty = cefSettingsType?.GetProperty("CefCommandLineArgs");
            if (commandLineArgsProperty == null)
            {
                Console.WriteLine("CefCommandLineArgs property not found.");
                return;
            }

            object commandLineArgs = commandLineArgsProperty.GetValue(cefSettings);
            if (commandLineArgs == null)
            {
                Console.WriteLine("CefCommandLineArgs is null.");
                return;
            }

            Type commandLineArgsType = commandLineArgs.GetType();
            PropertyInfo countProperty = commandLineArgsType.GetProperty("Count");
            Console.WriteLine($"Command-line args count: {countProperty?.GetValue(commandLineArgs)}");

            MethodInfo getEnumeratorMethod = commandLineArgsType.GetMethod("GetEnumerator");
            var enumerator = getEnumeratorMethod.Invoke(commandLineArgs, null);

            Type enumeratorType = enumerator.GetType();
            MethodInfo moveNextMethod = enumeratorType.GetMethod("MoveNext");
            PropertyInfo currentProperty = enumeratorType.GetProperty("Current");

            while ((bool)moveNextMethod.Invoke(enumerator, null))
            {
                var current = currentProperty.GetValue(enumerator);
                var keyProperty = current.GetType().GetProperty("Key");
                var valueProperty = current.GetType().GetProperty("Value");

                string key = keyProperty?.GetValue(current)?.ToString();
                string value = valueProperty?.GetValue(current)?.ToString();

                //Console.WriteLine($"[{key}] = {value}");
                //MessageBox.Show($"[{key}] = {value}");
            }
        }

        public Object InitializeBrowser(string initialUrl = "https://src-onboarding.identitysoon.com/passwordreset")
        {
            try
            {

                browserType = cefSharpWinFormsAssembly.GetType("CefSharp.WinForms.ChromiumWebBrowser");
                browser = Activator.CreateInstance(browserType);

                if (browser == null)
                {
                    //MessageBox.Show("Failed to create ChromiumWebBrowser instance.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return null;
                }


                return browser;
            }
            catch (Exception ex)
            {
                //MessageBox.Show($"Error Initializing Browser: {ex.Message}", "Browser Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return null;
            }
        }

        public void SetBrowserAddress(string urlToLoad)
        {
            Type browserType = cefSharpWinFormsAssembly.GetType("CefSharp.WinForms.ChromiumWebBrowser");
            PropertyInfo addressProperty = browserType.GetProperty("Address");

            if (addressProperty != null)
            {
                addressProperty.SetValue(browser, urlToLoad);
            }
            else
            {
                // Try Setting initial URL using Load method
                MethodInfo loadMethod = browserType.GetMethod("Load", new[] { typeof(string) });
                loadMethod?.Invoke(browser, new object[] { urlToLoad });
            }
        }

        public void Dispose()
        {
            try
            {
                if (browser != null)
                {
                    MethodInfo disposeMethod = browserType.GetMethod("Dispose");
                    disposeMethod?.Invoke(browser, null);
                    browser = null;

                }

                if (cefSharpCoreAssembly != null)
                {
                    Type cefType = cefSharpCoreAssembly.GetType("CefSharp.Cef");
                    MethodInfo shutdownMethod = cefType?.GetMethod("Shutdown");

                    shutdownMethod?.Invoke(null, null);

                }
            }
            catch (Exception ex)
            {
                //MessageBox.Show($"Error disposing ChromiumWebBrowser: {ex.Message}");
            }
        }

        public void ShowDevTools()
        {
            try
            {
                MethodInfo showDevToolsMethod = browserType.GetType().GetMethod("ShowDevTools", new Type[0]);

                if (showDevToolsMethod != null)
                {
                    showDevToolsMethod.Invoke(browserType, null);
                }
                else
                {
                    throw new MissingMethodException("ShowDevTools method not found in ChromiumWebBrowser.");
                }
            }
            catch (Exception ex)
            {
                //MessageBox.Show($"Error handling ShowDevTools: {ex.Message}", "Event Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public bool initializeCef(Object cefSettings)
        {
            Type cefType = cefSharpCoreAssembly.GetType("CefSharp.Cef");
            Type cefSettingsType = cefSharpWinFormsAssembly.GetType("CefSharp.WinForms.CefSettings");

            MethodInfo initializeMethod = cefType.GetMethod("Initialize", new[] { cefSettingsType });

            //Perform dependency check to make sure all relevant resources are in our output directory.
            bool success = (bool)initializeMethod.Invoke(null, new object[] { cefSettings });

            return success;
        }

        public int getExitCode()
        {
            Type cefType = cefSharpCoreAssembly.GetType("CefSharp.Cef");

            MethodInfo getExitCodeMethod = cefType.GetMethod("GetExitCode", BindingFlags.Public | BindingFlags.Static);
            if (getExitCodeMethod == null)
            {
                //MessageBox.Show("Cef.Initialize failed and GetExitCode method not found.", "Initialization Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 0;
            }

            int exitCode = (int)getExitCodeMethod.Invoke(null, null);
            return exitCode;
        }

        public object getMainFrame(object browser1)
        {

            //MessageBox.Show("calling getMain frame");

            object mainFrame = null;

            try
            {


                var extensionType = cefSharpAssembly.GetType("CefSharp.WebBrowserExtensions");
               
                var getMainFrameMethod = extensionType?.GetMethod(
                    "GetMainFrame",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new Type[] { browser1.GetType().GetInterface("CefSharp.IChromiumWebBrowserBase") ?? browser1.GetType() },
                    null
                );



                mainFrame = getMainFrameMethod.Invoke(null, new object[] { browser1 });


            }
            catch (Exception ex)
            {
                //MessageBox.Show(ex.Message);
            }

            return mainFrame;


        }

        public bool isFrameValid(object frame)
        {
            var frameType = frame.GetType();

            var isValidProperty = frameType.GetProperty("IsValid", BindingFlags.Public | BindingFlags.Instance);

            bool isValid = false;

            if (isValidProperty != null)
            {
                isValid = (bool)isValidProperty.GetValue(frame);
                //MessageBox.Show("IsValid: " + isValid);
            }
            else
            {
                //MessageBox.Show("IsValid property not found");
            }
            return isValid;
        }

        public bool CanExecuteJavascriptInMainFrame(object browser)
        {
            Type browserType = browser.GetType();

            var canExecuteProp = browserType.GetProperty("CanExecuteJavascriptInMainFrame", BindingFlags.Public | BindingFlags.Instance);
           

            var canExecute = (bool)canExecuteProp.GetValue(browser); return true;

            return canExecute;
        }

        public void ExecuteJavaScriptAsyncFrame(object frame, string script)
        {

            try
            {
                Type frameType = frame.GetType();

                // Find method with 3 parameters: string, string, int
                var method = frameType.GetMethod("ExecuteJavaScriptAsync", new Type[] { typeof(string), typeof(string), typeof(int) });

                if (method == null)
                {
                    return;
                }

                object[] parameters = new object[] { script, "", 0 };

                method.Invoke(frame, parameters);

            }
            catch (Exception ex)
            {
                //MessageBox.Show("Exception: " + ex.Message);
            }
        }

        public void StopBrowser(object browser1)
        {

            try
            {

                var extensionType = cefSharpAssembly.GetType("CefSharp.WebBrowserExtensions");
                if (extensionType == null)
                {
                    //MessageBox.Show("type is null");
                }
                var StopBrowserMethod = extensionType?.GetMethod(
                    "Stop",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new Type[] { browser1.GetType().GetInterface("CefSharp.IChromiumWebBrowserBase") ?? browser1.GetType() },
                    null
                );


                StopBrowserMethod.Invoke(null, new object[] { browser1 });

            }
            catch (Exception ex)
            {
                //MessageBox.Show(ex.Message);
            }

        }

        public void LoadURL(object browser, string url)
        {

            //MessageBox.Show("calling load browser");

            MethodInfo loadMethod = browserType.GetMethod("Load", new[] { typeof(string) });
            loadMethod?.Invoke(browser, new object[] { url });

        }

        public object GetCefErrorCodeAborted()
        {
            try
            {

                Type cefErrorCodeType = cefSharpAssembly.GetType("CefSharp.CefErrorCode");

                if (cefErrorCodeType == null)
                {
                    return null;
                }


                object abortedValue = Enum.Parse(cefErrorCodeType, "Aborted");

                //MessageBox.Show("Got CefErrorCode.Aborted successfully: " + abortedValue.ToString());

                return abortedValue;
            }
            catch (Exception ex)
            {
                //MessageBox.Show("Error getting CefErrorCode.Aborted: " + ex.Message);
                return null;
            }
        }

        public void SetBrowserLanguage(object CefSettings, string language)
        {
            Type cefSettingsType = CefSettings.GetType();

            var property = cefSettingsType.GetProperty("AcceptLanguageList");

            if (property != null && property.CanWrite)
            {
                property.SetValue(CefSettings, language);
            }
            else
            {
                //MessageBox.Show("Property 'AcceptLanguageList' not found or not writable.");
            }

        }


    }
}
