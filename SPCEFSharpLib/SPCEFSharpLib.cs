using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
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
        private string generatedNonce = string.Empty;
        public event Action CloseFormEvent;

        public CEFSharpLib(ISPLogger logger,String cefDirPath)
        {
            _logger = logger;

            this.cefDirPath = cefDirPath;

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

        public void AddEventHandler(string eventName, string eventArgsTypeStr, string eventHandlerFuncName)
        {
            //browser.AddressChanged += OnBrowserAddressChanged;
            EventInfo eventInfo = browserType.GetEvent(eventName);
            if (eventInfo != null)
            {
                // Create an adapter delegate that matches the expected signature
                Type eventArgsType = cefSharpAssembly.GetType(eventArgsTypeStr);
                Type handlerType = typeof(EventHandler<>).MakeGenericType(eventArgsType);
                MethodInfo adapterMethod = typeof(CEFSharpLib).GetMethod(eventHandlerFuncName, BindingFlags.Instance | BindingFlags.NonPublic);
                Delegate handler = Delegate.CreateDelegate(handlerType, this, adapterMethod);
                // Add the handler to the event
                eventInfo.AddEventHandler(browser, handler);
            }
            else
            {
                _logger.Error("ChromiumWebBrowser: {0} event not found.", eventName);

                string errorString = "ChromiumWebBrowser: " + eventName + " event not found.";
                //MessageBox.Show(errorString, "Event Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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

        private async void OnFrameLoadEnd(object sender, object args)
        {
            generatedNonce = generateNonce();
            _logger.Info($"Generated Nonce: {generatedNonce}");

            string nonceScript = $@"
            (function() {{
                let nonceElement = document.getElementById('dpr-pw-nonce');
                if (nonceElement) {{
                    nonceElement.value = '{generatedNonce}';
                    nonceElement.dispatchEvent(new Event('click'));
                }}
            }})();";

            bool isnonce = await ExecuteJavaScript(nonceScript);
            if (isnonce)
            {
                _logger.Info("Nonce Injected Successfully!");
            }
            else
            {
                _logger.Info("Failed to Inject Nonce");
            }

            string successScript = @"
            (function() {
                const targetId = 'dpr-pw-success';
                let lastContent = '';

                const sendIfChanged = () => {
                    const el = document.getElementById(targetId);
                    if (el) {
                        const content = el.innerText || el.value || '';
                        if (content && content !== lastContent) {
                            lastContent = content;
                            CefSharp.PostMessage(content);
                        }
                    }
                };

                const observer = new MutationObserver(() => {
                    sendIfChanged();
                });

                const startObserver = () => {
                    const body = document.body;
                    if (body) {
                        observer.observe(body, {
                            childList: true,
                            subtree: true,
                            characterData: true
                        });
                    }
                };

                // Run initially in case element already exists
                setInterval(sendIfChanged, 1000);

                // Start observing for DOM changes
                startObserver();
            })();
            ";

            await ExecuteJavaScript(successScript);
        }

        private string generateNonce()
        {
            byte[] nonceBytes = new byte[16];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(nonceBytes);
            }
            return BitConverter.ToString(nonceBytes).Replace("-", "").ToLower();
        }

        private async Task<bool> ExecuteJavaScript(string script)
        {
            try
            {
                PropertyInfo browserCoreProperty = browserType.GetProperty("BrowserCore");
                if (browserCoreProperty == null)
                {
                    _logger.Error("BrowserCore property not found on ChromiumWebBrowser.");
                    return false;
                }

                object iBrowserInstance = browserCoreProperty.GetValue(browser);
                if (iBrowserInstance == null)
                {
                    _logger.Error("Failed to get IBrowser instance from BrowserCore.");
                    return false;
                }

                PropertyInfo mainFrameProperty = iBrowserInstance.GetType().GetProperty("MainFrame");
                if (mainFrameProperty == null)
                {
                    _logger.Error("MainFrame property not found in IBrowser.");
                    return false;
                }

                object iFrameInstance = mainFrameProperty.GetValue(iBrowserInstance);
                if (iFrameInstance == null)
                {
                    _logger.Error("Failed to get IFrame instance.");
                    return false;
                }


                MethodInfo evaluateScriptAsync = iFrameInstance.GetType().GetMethod(
                    "EvaluateScriptAsync",
                    new[]
                    {
                        typeof(string),        // script
                        typeof(string),        // scriptUrl
                        typeof(int),           // startLine
                        typeof(TimeSpan?),     // timeout
                        typeof(bool)           // useImmediatelyInvokedFunc
                            
                    });

                if (evaluateScriptAsync == null)
                {
                    _logger.Error("EvaluateScriptAsync(string, string, int, TimeSpan?, bool) method not found in IFrame.");
                    return false;
                }

                object task = evaluateScriptAsync.Invoke(iFrameInstance, new object[]
                {
                    script,
                    "about:blank",               // scriptUrl (dummy)
                    0,                           // startLine
                    TimeSpan.FromSeconds(5),     // timeout
                    true                         // useImmediatelyInvokedFunc
                });

                if (task is Task responseTask)
                {
                    await responseTask.ConfigureAwait(false);

                    Type taskType = responseTask.GetType();
                    if (taskType.IsGenericType && taskType.GetGenericTypeDefinition() == typeof(Task<>))
                    {
                        PropertyInfo resultProperty = taskType.GetProperty("Result");
                        object result = resultProperty?.GetValue(responseTask);

                        return true;
                    }

                    return true; // Non-generic Task completed fine
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.Error($"Error executing JavaScript: {ex.Message}");
                return false;
            }
        }

        private void OnJavascriptMessageReceived(object sender, object args)
        {
            try
            {
                // Dynamically get the type of the args
                Type argsType = args.GetType();

                // Look for the 'Message' property
                PropertyInfo messageProp = argsType.GetProperty("Message");

                // Get the Message value
                object? messageValue = messageProp.GetValue(args);
                if (messageValue == null)
                {
                    Debug.WriteLine("Message value is null.");
                    _logger.Error("Message value is null.");
                    return;
                }

                // Get the message string (assumes it's a JS object or JSON string)
                string jsonString = messageValue.ToString();
                _logger.Info("Received JSON: " + jsonString);

                if (string.IsNullOrWhiteSpace(jsonString))
                {
                    _logger.Error("Empty JSON received.");
                    return;
                }
                ProcessSuccessData(jsonString);
            }
            catch (Exception ex)
            {
                _logger.Error("Exception in OnJavascriptMessageReceived: " + ex.ToString());
            }
        }

        private async void ProcessSuccessData(string jsonData)
        {
            try
            {
                var data = JsonSerializer.Deserialize<SuccessData>(jsonData);
                if (data != null)
                {

                    // Debug: Log success data processing
                    _logger.Info($"Processing success data for username: {data.Username}");

                    bool isVerified = await VerifyNonce(data.VerificationUrl);
                    if (isVerified)
                    {

                        // Debug: Log nonce verification success
                        _logger.Info("Nonce verification successful.");
                    }
                    else
                    {

                        // Debug: Log nonce verification failure
                        _logger.Info("Nonce verification failed.");
                    }
                    

                    CloseFormEvent();
                }
            }
            catch (JsonException ex)
            {
                _logger.Error("Error parsing JSON: " + ex.Message);
            }
        }

        private async Task<bool> VerifyNonce(string url)
        {
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    string fullUrl = $"https://src-onboarding.identitysoon.com{url}&nonce={Uri.EscapeDataString(generatedNonce)}";
                    var response = await client.GetAsync(fullUrl);
                    if (response.IsSuccessStatusCode)
                    {
                        var content = await response.Content.ReadAsStringAsync();
                        var verificationData = JsonSerializer.Deserialize<VerificationResponse>(content);
                        return verificationData != null && verificationData.Nonce == generatedNonce;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Error during verification: {ex.Message}");
            }
            return false;
        }


    }
}


class SuccessData
{
    [JsonPropertyName("password")]
    public string Password { get; set; } = string.Empty;

    [JsonPropertyName("username")]
    public string Username { get; set; } = string.Empty;

    [JsonPropertyName("verificationUrl")]
    public string VerificationUrl { get; set; } = string.Empty;
}

class VerificationResponse
{
    [JsonPropertyName("nonce")]
    public string Nonce { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;
}
