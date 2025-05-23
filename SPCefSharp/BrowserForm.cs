// Copyright © 2010-2015 The CefSharp Authors. All rights reserved.
//
// Use of this source code is governed by a BSD-style license that can be found in the LICENSE file.

using SPCefSharp.WinForms.Controls;
using SPCEFSharpLib;
using SPLoggerLib;
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows.Forms;


namespace SPCefSharp.WinForms
{
    public partial class BrowserForm : Form
    {
        private string title = "CEF DynamicLoading : ";
        private string urlToLoad = "https://src-onboarding.identitysoon.com/passwordreset";
        // private string urlToLoad = "https://src-onboarding.identitysoon.com/r/default/flow-selection";
        private string generatedNonce = string.Empty;

        Type? browserType = null;       //ChromiumWebBrowser
        private object? browser;        //ChromiumWebBrowser Object

        private Assembly? cefSharpAssembly;
        private Assembly? cefSharpWinFormsAssembly;
        private Assembly? cefSharpCoreAssembly;

        SPCEFSharpLib.CEFSharpLib CefSharpLib;


        private BrowserForm()
        {
            InitializeComponent();

            CefSharpLib = new CEFSharpLib(Logger.GetLoggerObject(),Globals.CefDirPath);
            CefSharpLib.CloseFormEvent += CloseForm;

            title += Program.GetCefVersion();
            this.Text = title;
            WindowState = FormWindowState.Normal;

        }

        public static BrowserForm GetBrowserFormObject()
        {
            var browserObject = new BrowserForm();

            browserObject.InitChromiumWebBrowserObject();
            browserObject.SetBrowserAddressProperty();
            browserObject.SetBrowserEventHandlers();

            return browserObject;
        }

        private void InitChromiumWebBrowserObject()
        {
            // Load CefSharp.WinForms assembly
            cefSharpWinFormsAssembly = Assembly.LoadFrom(Path.Combine(Globals.CefDirPath, "CefSharp.WinForms.dll"));
            cefSharpCoreAssembly = Assembly.LoadFrom(Path.Combine(Globals.CefDirPath, "CefSharp.Core.dll"));
            cefSharpAssembly = Assembly.LoadFrom(Path.Combine(Globals.CefDirPath, "CefSharp.dll"));

            // Create browser
            browser = CefSharpLib.InitializeBrowser();
            browserType = browser.GetType();
            Control browserControl = (Control)browser;
            browserControl.Dock = DockStyle.Fill;
            toolStripContainer.ContentPanel.Controls.Add(browserControl);

            

            Logger.Info("Created ChromiumWebBrowser Instance successfully");
        }

        private void SetBrowserAddressProperty()
        {
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
        }

        private void SetBrowserEventHandlers()
        {
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

            AddEventHandler("LoadError",
                            "CefSharp.LoadErrorEventArgs",
                            nameof(OnBrowserLoadError));

            AddEventHandler("AddressChanged",
                            "CefSharp.AddressChangedEventArgs",
                            nameof(OnBrowserAddressChanged));

            AddEventHandler("FrameLoadStart",
                            "CefSharp.FrameLoadStartEventArgs",
                            nameof(OnFrameLoadStart));


            CefSharpLib.AddEventHandler("FrameLoadEnd",
                            "CefSharp.FrameLoadEndEventArgs",
                            "OnFrameLoadEnd");


            CefSharpLib.AddEventHandler("JavascriptMessageReceived",
                            "CefSharp.JavascriptMessageReceivedEventArgs",
                            "OnJavascriptMessageReceived");
        }

        private void CloseForm()
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => this.Close()));
            }
            else
            {
                this.Close();
            }
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

        private void OnFrameLoadStart(object sender, object args)
        {
            return;
        }

        private void AddEventHandler(string eventName, string eventArgsTypeStr, string eventHandlerFuncName)
        {
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

        private void toolStripContainer_ContentPanel_Load(object sender, EventArgs e)
        {

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
}
