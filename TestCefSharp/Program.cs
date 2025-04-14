// Copyright © 2010-2015 The CefSharp Authors. All rights reserved.
//
// Use of this source code is governed by a BSD-style license that can be found in the LICENSE file.

using System;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using CefSharpIntegration;

namespace TestCefSharp.WinForms
{


    public static class Globals
    {
        public const int CmdArgsMaxCount = 1;
        public const string AppName = "TestCefSharp";

        public static string CefDirName = "cef";
        public static string CefDirPath = "";

        public static string LogFileDir = "";
        public static string LogFilePath = "";
        public static string LogFileName = "";

        public static string CefCacheFolder = "CefSharp\\Cache";
        public static string CefCacheFolderPath = "";

        public static void InitializeGlobals(string[] args) {

            if (CmdArgsMaxCount < args.Length)
            {
                LogFileDir = AppDomain.CurrentDomain.BaseDirectory;
                LogFileName = AppName + ".log";
                Logger.Error("Invalid no. of command line args {0}.", args.Length.ToString());
                Environment.Exit(0); //0 is exit status
            }

            if (args.Length >= 1)
            {
                CefDirName = args[0];
            }

            CefDirPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, CefDirName);
            LogFileDir = AppDomain.CurrentDomain.BaseDirectory;
            LogFileName = AppName + "_" + CefDirName + ".log";
            LogFilePath = Path.Combine(LogFileDir, LogFileName);

            Random random = new Random();
            int randomNumber = random.Next();
            CefCacheFolder += randomNumber.ToString();
            CefCacheFolderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), CefCacheFolder);
            Logger.Info("Loading CEF from {0}", Globals.CefDirPath);
        }
    }

    public static class CefSharpResultCode
    {
        public const int NormalExitProcessNotified = 266;
    }

    public static class Program
    {
        
        private static CefSharpHelper cefSharpManager;


        public static string GetCefVersion()
        {
            try
            {
                return cefSharpManager.GetCefVersions();
            }
            catch (Exception ex)
            {
                return $"Error retrieving CEF version: {ex.Message}";
            }
        }

        [STAThread]
        public static int Main(string[] args)
        {
            try
            {
                Globals.InitializeGlobals(args);

                cefSharpManager = new CefSharpHelper(Globals.CefDirPath);

                Logger.Info("Loaded assembly {0}", Globals.CefDirPath + "CefSharp.Core.dll");
                Logger.Info("Loaded assembly {0}", Globals.CefDirPath + "CefSharp.WinForms.dll");

#if ANYCPU
            Assembly cefSharpAssembly = Assembly.LoadFrom(Path.Combine(Globals.CefDir, "CefSharp.dll"));
            Logger.Info("Loaded assembly {0}", Globals.CefDir + "CefSharp.dll");
            Type cefRuntimeType = cefSharpAssembly.GetType("CefSharp.CefRuntime");
            MethodInfo subscribeMethod = cefRuntimeType.GetMethod("SubscribeAnyCpuAssemblyResolver", BindingFlags.Public | BindingFlags.Static);
            subscribeMethod.Invoke(null, new object[] {""});
#endif


                var cefSettings = cefSharpManager.CreateCefSettings(Globals.CefCacheFolderPath);


                //Example of setting a command line argument
                //Enables WebRTC
                // - CEF Doesn't currently support permissions on a per browser basis see https://bitbucket.org/chromiumembedded/cef/issues/2582/allow-run-time-handling-of-media-access
                // - CEF Doesn't currently support displaying a UI for media access permissions
                //
                //NOTE: WebRTC Device Id's aren't persisted as they are in Chrome see https://bitbucket.org/chromiumembedded/cef/issues/2064/persist-webrtc-deviceids-across-restart
                // Access CefCommandLineArgs property

                cefSharpManager.AddCommandLineArgs(cefSettings, "enable-media-stream");
                cefSharpManager.AddCommandLineArgs(cefSettings, "use-fake-ui-for-media-stream");
                cefSharpManager.AddCommandLineArgs(cefSettings, "enable-usermedia-screen-capturing");
       

                bool success = cefSharpManager.initializeCef(cefSettings);

                if (!success)
                {

                    int exitCode=cefSharpManager.getExitCode();


                    if (exitCode == CefSharpResultCode.NormalExitProcessNotified)
                    {
                        Logger.Error("Cef.Initialize failed with exit code {0}, another process is already using cache path {1}.", exitCode, Globals.CefCacheFolderPath);
                        MessageBox.Show($"Cef.Initialize failed with exit code {exitCode}, another process is already using cache path {Globals.CefCacheFolderPath}",
                            "Initialization Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    else
                    {
                        Logger.Error("Cef.Initialize failed with exit code {0}.", exitCode);
                        MessageBox.Show($"Cef.Initialize failed with exit code {exitCode}, check the log file for more details.",
                            "Initialization Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }

                    return 0;
                }
                else
                {
                    Logger.Info("Cef Initialized successfully.");
                }

                Application.EnableVisualStyles();
                Application.Run(new BrowserForm());                
            }
            catch (FileNotFoundException ex)
            {
                Logger.Error("Exception:{0}.", ex.Message);
            }
            catch (Exception ex)
            {
                Logger.Error("Unknown exception occurred, message:{0}.", ex.Message);
            }
            return 0;
        }
}
}
