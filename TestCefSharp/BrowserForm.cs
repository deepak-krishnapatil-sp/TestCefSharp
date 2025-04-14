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
        
        private CefSharpHelper cefSharpManager;

        public BrowserForm()
        {

            InitializeComponent();
            
            title += Program.GetCefVersion();
            this.Text = title;
            WindowState = FormWindowState.Normal;

            cefSharpManager = new CefSharpHelper(Globals.CefDirPath);

            Control browserControl = cefSharpManager.InitializeBrowser(urlToLoad);
        

            if (browserControl != null)
            { 
                toolStripContainer.ContentPanel.Controls.Add(browserControl);
                cefSharpManager.SetBrowserAddress(urlToLoad);
            }

            cefSharpManager.BrowserAddressChanged += UpdateUrlTextBox;
            cefSharpManager.TitleChanged += ChangeTitle;


            Logger.Info("Created ChromiumWebBrowser Instance successfully");

            
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
