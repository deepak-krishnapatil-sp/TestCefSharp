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
using CefSharpIntegration;



namespace TestCefSharp.WinForms
{
    public partial class BrowserForm : Form
    {
        private string title = "CEF DynamicLoading : ";
        private string urlToLoad = "https://src-onboarding.identitysoon.com/passwordreset";
        Object browser = null;
        Type browserType = null;


        private CefSharpHelper cefSharpManager;

        public BrowserForm()
        {

            InitializeComponent();


            title += Program.GetCefVersion();
            this.Text = title;
            WindowState = FormWindowState.Normal;

            cefSharpManager = new CefSharpHelper(Globals.CefDirPath);

            
            browser = cefSharpManager.InitializeBrowser(urlToLoad);
            browserType = browser.GetType();


            AddEventHandler("IsBrowserInitializedChanged", "OnIsBrowserInitializedChanged");
            AddEventHandler("AddressChanged", "OnBrowserAddressChanged");
            AddEventHandler("TitleChanged", "OnBrowserTitleChanged");
            AddEventHandler("LoadingStateChanged", "OnLoadingStateChanged");
            AddEventHandler("StatusMessage", "OnBrowserStatusMessage");
            AddEventHandler("ConsoleMessage", "OnBrowserConsoleMessage");
            

            if (browser != null)
            {
                Control browserControl = (Control)browser;
                browserControl.Dock = DockStyle.Fill;
                toolStripContainer.ContentPanel.Controls.Add(browserControl);
                cefSharpManager.SetBrowserAddress(urlToLoad);

            }

            cefSharpManager.BrowserAddressChanged += UpdateUrlTextBox;
            cefSharpManager.TitleChanged += ChangeTitle;


            Logger.Info("Created ChromiumWebBrowser Instance successfully");

            
        }

        public void AddEventHandler(string EventName, string MethodName)
        {
            MethodInfo eventHandlerMethod = typeof(BrowserForm).GetMethod(MethodName, BindingFlags.NonPublic | BindingFlags.Instance);
            
            try { 


                EventInfo Event = browserType.GetEvent(EventName);

                if (Event != null)
                {
                    Delegate handler = Delegate.CreateDelegate(Event.EventHandlerType, this, eventHandlerMethod);
                    Event.AddEventHandler(browser, handler);
                }

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
            

        }

        private void OnIsBrowserInitializedChanged(object sender, EventArgs e)
        {
            MessageBox.Show("calling OnIsBrowserInitializedChanged -- 1");
        }

        private void OnBrowserAddressChanged(object sender, object args)
        {
            cefSharpManager.OnBrowserAddressChanged(sender, args);

        }

        private void OnBrowserTitleChanged(object sender, object args)
        {
            cefSharpManager.OnBrowserTitleChanged(sender, args);
        }

        private void OnLoadingStateChanged(object sender, object args)
        {
            try
            {
                MessageBox.Show("loading state changed");
                Type loadingArgsType = args.GetType();
                PropertyInfo isLoadingProp = loadingArgsType.GetProperty("IsLoading");
                PropertyInfo canReloadProp = loadingArgsType.GetProperty("CanReload");

                bool isLoading = (bool)isLoadingProp?.GetValue(args);
                bool canReload = (bool)canReloadProp?.GetValue(args);


                // For example, update UI or internal state
                //this.InvokeOnUiThreadIfRequired(() =>
                //{
                //    SetIsLoading(isLoading);
                //});
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "LoadingStateChanged Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OnBrowserStatusMessage(object sender, object args)
        {
            try
            {
                MessageBox.Show("calling on browser status message through handler");
                Type statusMessageEventArgsType = args.GetType();
                PropertyInfo valueProperty = statusMessageEventArgsType.GetProperty("Value");

                if (valueProperty != null)
                {
                    string statusText = (string)valueProperty.GetValue(args);

                    // Optional: update a label or notify the UI
                    //this.InvokeOnUiThreadIfRequired(() => UpdateStatusBar(statusText));
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error handling StatusMessage: {ex.Message}", "Event Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OnBrowserConsoleMessage(object sender, object args)
        {
           cefSharpManager.OnBrowserConsoleMessage(sender, args);
        }


        private void ExitMenuItemClick(object sender, EventArgs e)
        {
            cefSharpManager.Dispose(); 
            Close();
        }

        private void ShowDevToolsMenuItemClick(object sender, EventArgs e)
        {
            cefSharpManager.ShowDevTools();
        }

        private void UpdateUrlTextBox(string newAddress)
        {
            MessageBox.Show("calling UpdateUrlTextBox");
            this.InvokeOnUiThreadIfRequired( () => urlTextBox.Text = newAddress);
        }

        private void ChangeTitle(string newTitle)
        {
            MessageBox.Show("calling change title");
            this.InvokeOnUiThreadIfRequired(() => Text = title + " - " + newTitle);

        }

      
        private void toolStripContainer_ContentPanel_Load(object sender, EventArgs e)
        {

        }
    
    }
}
