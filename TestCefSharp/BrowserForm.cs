// Copyright © 2010-2015 The CefSharp Authors. All rights reserved.
//
// Use of this source code is governed by a BSD-style license that can be found in the LICENSE file.

using TestCefSharp.WinForms.Controls;
using System;
using System.Windows.Forms;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Security.Policy;
using System.Text.Json;
using System.Net.Http;
using System.Text.Json.Serialization;
using System.Linq;

namespace TestCefSharp.WinForms
{
    public partial class BrowserForm : Form
    {
        private string title = "CEF DynamicLoading : ";
        private string urlToLoad = "https://src-onboarding.identitysoon.com/r/default/flow-selection";

        Type browserType;       //ChromiumWebBrowser
        private object browser; //ChromiumWebBrowser Object
        private string generatedNonce;

        private Assembly cefSharpAssembly;
        private Assembly cefSharpWinFormsAssembly;
        private Assembly cefSharpCoreAssembly;

        public BrowserForm()
        {
            InitializeComponent();

            title += Program.GetCefVersion();
            this.Text = title;
            WindowState = FormWindowState.Normal;


            // Load CefSharp.WinForms assembly
            cefSharpWinFormsAssembly = Assembly.LoadFrom(Path.Combine(Globals.CefDirPath, "CefSharp.WinForms.dll"));
            cefSharpCoreAssembly = Assembly.LoadFrom(Path.Combine(Globals.CefDirPath, "CefSharp.Core.dll"));
            cefSharpAssembly = Assembly.LoadFrom(Path.Combine(Globals.CefDirPath, "CefSharp.dll"));

            // Create browser
            browserType = cefSharpWinFormsAssembly.GetType("CefSharp.WinForms.ChromiumWebBrowser");
            browser = Activator.CreateInstance(browserType);
            Control browserControl = (Control)browser;
            browserControl.Dock = DockStyle.Fill;
            toolStripContainer.ContentPanel.Controls.Add(browserControl);

            Logger.Info("Created ChromiumWebBrowser Instance successfully");

            // Try setting Address property
            PropertyInfo addressProperty = browserType.GetProperty("Address");

            if (addressProperty != null)
            {
                addressProperty.SetValue(browser, urlToLoad);
            }
            else
            {
                // Try Setting initial URL using Load method
                MethodInfo loadMethod = browserType.GetMethod("Load", new[] { typeof(string) });
                if (loadMethod != null)
                {
                    loadMethod.Invoke(browser, new object[] { urlToLoad });
                }
                else
                {
                    Logger.Error("ChromiumWebBrowser: Neither Load method nor Address property found.");
                    MessageBox.Show("Neither Load method nor Address property found.", "Initialization Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }

            EventInfo isBrowserInitializedChangedEvent = browserType.GetEvent("IsBrowserInitializedChanged");
            if (isBrowserInitializedChangedEvent != null)
            {
                // Create delegate for the event handler
                Delegate handler = Delegate.CreateDelegate(
                    isBrowserInitializedChangedEvent.EventHandlerType,
                    this,
                    nameof(OnIsBrowserInitializedChanged));

                // Add the handler to the event
                isBrowserInitializedChangedEvent.AddEventHandler(browser, handler);
            }
            else
            {
                Logger.Error("ChromiumWebBrowser: IsBrowserInitializedChanged event not found.");
                MessageBox.Show("IsBrowserInitializedChanged event not found.", "Event Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            EventInfo jsMessageEvent = browserType.GetEvent("JavascriptMessageReceived");

            if (jsMessageEvent == null)
            {
                Debug.WriteLine("Error: JavascriptMessageReceived event not found!");
                //Logger.Error("JavascriptMessageReceived event not found!");
                return;
            }
            AddEventHandler("LoadError", "CefSharp.LoadErrorEventArgs", nameof(OnBrowserLoadError));
            AddEventHandler("TitleChanged", "CefSharp.TitleChangedEventArgs", nameof(OnBrowserTitleChanged));
            AddEventHandler("StatusMessage", "CefSharp.StatusMessageEventArgs", nameof(OnBrowserStatusMessage));
            AddEventHandler("ConsoleMessage", "CefSharp.ConsoleMessageEventArgs", nameof(OnBrowserConsoleMessage));
            AddEventHandler("AddressChanged", "CefSharp.AddressChangedEventArgs", nameof(OnBrowserAddressChanged));
            AddEventHandler("LoadingStateChanged", "CefSharp.LoadingStateChangedEventArgs", nameof(OnLoadingStateChanged));
            AddEventHandler("FrameLoadEnd", "CefSharp.FrameLoadEndEventArgs", nameof(OnFrameLoadEnd));
            AddEventHandler("JavascriptMessageReceived", "CefSharp.JavascriptMessageReceivedEventArgs", nameof(OnWebMessageReceived));

        }
        private void ExitMenuItemClick(object sender, EventArgs e)
        {
            MethodInfo disposeMethod = browserType.GetMethod("Dispose");
            disposeMethod.Invoke(browser, null);
            // Shutdown CEF
            Type cefType = cefSharpCoreAssembly.GetType("CefSharp.Cef");
            MethodInfo shutdownMethod = cefType.GetMethod("Shutdown");
            shutdownMethod.Invoke(null, null);
            Close();
        }
        private void ShowDevToolsMenuItemClick(object sender, EventArgs e)
        {
            try
            {
                MethodInfo showDevToolsMethod = browserType.GetMethod("ShowDevTools", new[] { typeof(string) });
                showDevToolsMethod.Invoke(browser, null);
            }
            catch (Exception ex)
            {
                Logger.Error($"ChromiumWebBrowser:Error handling ShowDevTools event: {ex.Message}");
                MessageBox.Show($"Error handling ShowDevTools: {ex.Message}", "Event Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        private void OnIsBrowserInitializedChanged(object sender, EventArgs e)
        {
            MethodInfo focusMethod = browserType.GetMethod("Focus");
            this.InvokeOnUiThreadIfRequired(() => focusMethod.Invoke(browser, null));
            PropertyInfo isBrowserInitializedProperty = browserType.GetProperty("IsBrowserInitialized");

        }
        private void OnBrowserLoadError(object sender, object args)
        {
            try
            {
                Type loadErrorEventArgsType = cefSharpAssembly.GetType("CefSharp.LoadErrorEventArgs");
                PropertyInfo errorCodeProperty = loadErrorEventArgsType.GetProperty("ErrorCode");
                PropertyInfo errorTextProperty = loadErrorEventArgsType.GetProperty("ErrorText");
                PropertyInfo failedUrlProperty = loadErrorEventArgsType.GetProperty("FailedUrl");

                object errorCode = errorCodeProperty.GetValue(args); // Enum CefErrorCode
                string errorText = (string)errorTextProperty.GetValue(args);
                string failedUrl = (string)failedUrlProperty.GetValue(args);

                // Get CefErrorCode enum type and Aborted value
                Type cefErrorCodeType = cefSharpWinFormsAssembly.GetType("CefSharp.CefErrorCode");
                object abortedValue = Enum.Parse(cefErrorCodeType, "Aborted");
                int abortedCode = (int)abortedValue;
                //Actions that trigger a download will raise an aborted error.
                //Aborted is generally safe to ignore
                if ((int)errorCode == abortedCode)
                {
                    return;
                }

                // PropertyInfo BrowserProperty = loadErrorEventArgsType.GetProperty("Browser");

                var errorHtml = string.Format("<html><body><h2>Failed to load URL {0} with error {1} ({2}).</h2></body></html>",
                                  failedUrl, errorText, errorCode);

                // Get ChromiumWebBrowser type and SetMainFrameDocumentContentAsync method
                Type browserType = cefSharpWinFormsAssembly.GetType("CefSharp.WinForms.ChromiumWebBrowser");
                MethodInfo setContentMethod = browserType.GetMethod("SetMainFrameDocumentContentAsync", new[] { typeof(string) });

                if (setContentMethod != null)
                {
                    // Invoke the method (returns a Task)
                    object task = setContentMethod.Invoke(browser, new object[] { errorHtml });

                    // Since this is async, wait for it to complete (blocking call for simplicity)
                    Type taskType = task.GetType();
                    MethodInfo getAwaiterMethod = taskType.GetMethod("GetAwaiter");
                    object awaiter = getAwaiterMethod.Invoke(task, null);
                    MethodInfo getResultMethod = awaiter.GetType().GetMethod("GetResult");
                    getResultMethod.Invoke(awaiter, null); // Blocks until the task completes
                    Logger.Info("ChromiumWebBrowser: Custom error page set successfully.");
                }
                else
                {
                    Logger.Error("ChromiumWebBrowser:SetMainFrameDocumentContentAsync method not found.");
                    MessageBox.Show("SetMainFrameDocumentContentAsync method not found.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }

                //AddressChanged isn't called for failed Urls so we need to manually update the Url TextBox
                this.InvokeOnUiThreadIfRequired(() => urlTextBox.Text = failedUrl);
            }
            catch (Exception ex)
            {
                Logger.Error($"ChromiumWebBrowser:Error handling LoadError: {ex.Message}");
                MessageBox.Show($"Error handling LoadError: {ex.Message}", "Event Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        private void OnBrowserTitleChanged(object sender, object args)
        {
            try
            {
                // Get the Address property from AddressChangedEventArgs
                Type titleChangedEventArgsType = cefSharpAssembly.GetType("CefSharp.TitleChangedEventArgs");
                PropertyInfo addressProperty = titleChangedEventArgsType.GetProperty("Title");
                string newTitle = (string)addressProperty.GetValue(args);

                this.InvokeOnUiThreadIfRequired(() => Text = title + " - " + newTitle);
            }
            catch (Exception ex)
            {
                Logger.Error($"ChromiumWebBrowser:Error handling OnBrowserTitleChanged: {ex.Message}");
                MessageBox.Show($"Error handling OnBrowserTitleChanged: {ex.Message}", "Event Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        private void OnBrowserStatusMessage(object sender, object args)
        {
            try
            {
                Type statusMessageEventArgsType = cefSharpAssembly.GetType("CefSharp.StatusMessageEventArgs");
                PropertyInfo valueProperty = statusMessageEventArgsType.GetProperty("Value");

                string statusText = (string)valueProperty.GetValue(args);
                return;
            }
            catch (Exception ex)
            {
                Logger.Error($"ChromiumWebBrowser:Error handling StatusMessage: {ex.Message}");
                MessageBox.Show($"Error handling StatusMessage: {ex.Message}", "Event Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        private void OnBrowserConsoleMessage(object sender, object args)
        {
            try
            {
                Type consoleMessageEventArgsType = cefSharpAssembly.GetType("CefSharp.ConsoleMessageEventArgs");
                PropertyInfo messageProperty = consoleMessageEventArgsType.GetProperty("Message");
                PropertyInfo sourceProperty = consoleMessageEventArgsType.GetProperty("Source");
                PropertyInfo lineProperty = consoleMessageEventArgsType.GetProperty("Line");

                string message = (string)messageProperty.GetValue(args);
                string source = (string)sourceProperty.GetValue(args);
                int line = (int)lineProperty.GetValue(args);

                //DisplayOutput(string.Format("Line: {0}, Source: {1}, Message: {2}", line, source, message));
            }
            catch (Exception ex)
            {
                Logger.Error($"ChromiumWebBrowser:Error handling ConsoleMessage: {ex.Message}");
                MessageBox.Show($"Error handling ConsoleMessage: {ex.Message}", "Event Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        private void OnBrowserAddressChanged(object sender, object args)
        {
            try
            {
                // Get the Address property from AddressChangedEventArgs
                Type addressChangedEventArgsType = cefSharpAssembly.GetType("CefSharp.AddressChangedEventArgs");
                PropertyInfo addressProperty = addressChangedEventArgsType.GetProperty("Address");
                string newAddress = (string)addressProperty.GetValue(args);

                this.InvokeOnUiThreadIfRequired(() => urlTextBox.Text = newAddress);

            }
            catch (Exception ex)
            {
                Logger.Error($"ChromiumWebBrowser:Error handling AddressChanged: {ex.Message}");
                MessageBox.Show($"Error handling AddressChanged: {ex.Message}", "Event Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        private void OnLoadingStateChanged(object sender, object args)
        {
            try
            {
                // Get the LoadingStateChangedEventArgs properties
                Type loadingStateChangedEventArgsType = cefSharpAssembly.GetType("CefSharp.LoadingStateChangedEventArgs");
                PropertyInfo canReloadProperty = loadingStateChangedEventArgsType.GetProperty("CanReload");
                //PropertyInfo canGoBackProperty = loadingStateChangedEventArgsType.GetProperty("CanGoBack");
                //PropertyInfo canGoForwardProperty = loadingStateChangedEventArgsType.GetProperty("CanGoForward");

                bool canReload = (bool)canReloadProperty.GetValue(args);
                //bool canGoBack = (bool)canGoBackProperty.GetValue(args);
                //bool canGoForward = (bool)canGoForwardProperty.GetValue(args);

                //this.InvokeOnUiThreadIfRequired(() => SetIsLoading(!canReload));
            }
            catch (Exception ex)
            {
                Logger.Error($"ChromiumWebBrowser:Error handling LoadingStateChanged: {ex.Message}");
                MessageBox.Show($"Error handling LoadingStateChanged: {ex.Message}", "Event Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void AddEventHandler(string eventName, string eventArgsTypeStr, string eventHandlerFuncName)
        {
            //Logger.Info("AddEventHandler method");
            //browser.AddressChanged += OnBrowserAddressChanged;
            EventInfo eventInfo = browserType.GetEvent(eventName);
            if (eventInfo != null)
            {
                // Create an adapter delegate that matches the expected signature
                Type eventArgsType = cefSharpAssembly.GetType(eventArgsTypeStr);
                Type handlerType = typeof(EventHandler<>).MakeGenericType(eventArgsType);
                MethodInfo adapterMethod = typeof(BrowserForm).GetMethod(eventHandlerFuncName, BindingFlags.Instance | BindingFlags.NonPublic);
                Delegate handler = Delegate.CreateDelegate(handlerType, this, adapterMethod);
                // Add the handler to the event
                eventInfo.AddEventHandler(browser, handler);
            }
            else
            {
                Logger.Error("ChromiumWebBrowser: {0} event not found.", eventName);

                string errorString = "ChromiumWebBrowser: " + eventName + " event not found.";
                MessageBox.Show(errorString, "Event Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

        }
        private void OnWebMessageReceived(object sender, object args)
        {
            try
            {
                // Dynamically get the type of the args
                Type argsType = args.GetType();

                // Look for the 'Message' property
                PropertyInfo messageProp = argsType.GetProperty("Message");

                // Get the Message value
                object messageValue = messageProp.GetValue(args);
                if (messageValue == null)
                {
                    Debug.WriteLine("Message value is null.");
                    Logger.Error("Message value is null.");
                    return;
                }

                // Get the message string (assumes it's a JS object or JSON string)
                string jsonString = messageValue.ToString();
                Logger.Info("Received JSON: " + jsonString);

                if (string.IsNullOrWhiteSpace(jsonString))
                {
                    Logger.Error("Empty JSON received.");
                    return;
                }
                ProcessSuccessData(jsonString);
            }
            catch (Exception ex)
            {
                Logger.Error("Exception in OnWebMessageReceived: " + ex.ToString());
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
                    Logger.Info($"Processing success data for username: {data.Username}");

                    bool isVerified = await VerifyNonce(data.VerificationUrl);
                    if (isVerified)
                    {

                        // Debug: Log nonce verification success
                        Logger.Info("Nonce verification successful.");
                    }
                    else
                    {

                        // Debug: Log nonce verification failure
                        Logger.Info("Nonce verification failed.");
                    }
                    if (this.InvokeRequired)
                    {
                        this.Invoke(new Action(() => this.Close()));
                    }
                    else
                    {
                        this.Close();
                    }
                }
            }
            catch (JsonException ex)
            {
                Logger.Error("Error parsing JSON: " + ex.Message);
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
                Logger.Error($"Error during verification: {ex.Message}");
            }
            return false;
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
        private async void OnFrameLoadEnd(object sender, object args)
        {
            generatedNonce = generateNonce();
            Logger.Info($"Generated Nonce: {generatedNonce}");

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
                Logger.Info("Nonce Injected Successfully!");
            }
            else
            {
                Logger.Info("Failed to Inject Nonce");
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


        private async Task<bool> ExecuteJavaScript(string script)
        {
            try
            {
                PropertyInfo browserCoreProperty = browserType.GetProperty("BrowserCore");
                if (browserCoreProperty == null)
                {
                    Logger.Error("BrowserCore property not found on ChromiumWebBrowser.");
                    return false;
                }

                object iBrowserInstance = browserCoreProperty.GetValue(browser);
                if (iBrowserInstance == null)
                {
                    Logger.Error("Failed to get IBrowser instance from BrowserCore.");
                    return false;
                }

                PropertyInfo mainFrameProperty = iBrowserInstance.GetType().GetProperty("MainFrame");
                if (mainFrameProperty == null)
                {
                    Logger.Error("MainFrame property not found in IBrowser.");
                    return false;
                }

                object iFrameInstance = mainFrameProperty.GetValue(iBrowserInstance);
                if (iFrameInstance == null)
                {
                    Logger.Error("Failed to get IFrame instance.");
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
                    Logger.Error("EvaluateScriptAsync(string, string, int, TimeSpan?, bool) method not found in IFrame.");
                    return false;
                }

                object task = evaluateScriptAsync.Invoke(iFrameInstance, new object[]
                {
                    script,
                    "about:blank",               // scriptUrl (dummy)
                    0,                           // startLine
                    TimeSpan.FromSeconds(5),    // timeout
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
                Logger.Error($"Error executing JavaScript: {ex.Message}");
                return false;
            }
        }
        private void CefSharpWebMessageReceivedHandler(object sender, EventArgs args)
        {
            try
            {
                // Use reflection to get the 'WebMessageAsJson' property from the args
                PropertyInfo jsonProp = args.GetType().GetProperty("WebMessageAsJson");
                if (jsonProp == null)
                {
                    Logger.Error("WebMessageAsJson property not found on event args.");
                    return;
                }

                string jsonData = jsonProp.GetValue(args)?.ToString();

                if (string.IsNullOrEmpty(jsonData))
                {
                    Logger.Error("Received empty WebMessageAsJson.");
                    return;
                }

                // Debug: Log when success data is received
                Debug.WriteLine($"Received success data: {jsonData}");
                Logger.Info($"Received success data: {jsonData}");

                ProcessSuccessData(jsonData); // This can be the same method you already use
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error handling CefSharp WebMessage: " + ex.Message);
                Logger.Error("Error handling CefSharp WebMessage: " + ex.Message);
            }
        }


    }
    class SuccessData
    {
        [JsonPropertyName("password")]
        public string Password { get; set; }

        [JsonPropertyName("username")]
        public string Username { get; set; }

        [JsonPropertyName("verificationUrl")]
        public string VerificationUrl { get; set; }
    }
    class VerificationResponse
    {
        [JsonPropertyName("nonce")]
        public string Nonce { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; }
    }
}