// Copyright © 2010-2015 The CefSharp Authors. All rights reserved.
//
// Use of this source code is governed by a BSD-style license that can be found in the LICENSE file.

using TestCefSharp.WinForms.Controls;
using System;
using System.Net;
using System.Windows.Forms;
using System.IO;
using System.Reflection;
using System.Collections.Generic;

namespace TestCefSharp.WinForms
{
    public partial class BrowserForm : Form
    {
        private string title = "CEF DynamicLoading : ";
        private string urlToLoad = "https://www.google.com";

        Type browserType;       //ChromiumWebBrowser
        private object browser; //ChromiumWebBrowser Object

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

            AddEventHandler("LoadError",
                            "CefSharp.LoadErrorEventArgs",
                            nameof(OnBrowserLoadError));

            AddEventHandler("AddressChanged",
                            "CefSharp.AddressChangedEventArgs",
                            nameof(OnBrowserAddressChanged));

            AddEventHandler("FrameLoadStart",
                            "CefSharp.FrameLoadStartEventArgs",
                            nameof(OnFrameLoadStart));

            AddEventHandler("FrameLoadEnd",
                            "CefSharp.FrameLoadEndEventArgs",
                            nameof(OnFrameLoadEnd));

            AddEventHandler("JavascriptMessageReceived",
                            "CefSharp.JavascriptMessageReceivedEventArgs",
                            nameof(OnJavascriptMessageReceived));
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

        private void OnFrameLoadEnd(object sender, object args)
        {
            return;
        }

        private void OnJavascriptMessageReceived(object sender, object args)
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
    }
}
