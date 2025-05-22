// Copyright © 2010-2015 The CefSharp Authors. All rights reserved.
//
// Use of this source code is governed by a BSD-style license that can be found in the LICENSE file.

using System;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

using SPLoggerLib;
using SPCEFSharpLib;

namespace SPCefSharp.WinForms
{

    public static class Globals
    {
        public const int CmdArgsMaxCount = 1;
        public const string AppName = "SPCefSharp";

        public static string CefDirName = "cef";
        public static string CefDirPath = "";

        public static string LogFileDir = "";
        public static string LogFilePath = "";
        public static string LogFileName = "";

        public static string CefCacheFolder = "CefSharp\\Cache";
        public static string CefCacheFolderPath = "";

        public static void InitLogger()
        {
            LogFileDir = AppDomain.CurrentDomain.BaseDirectory;
            LogFileName = AppName + ".log";
            LogFilePath = Path.Combine(LogFileDir, LogFileName);
            // Log from main application
            Logger.Debug("hello from application");
        }

        public static void InitCefSDK()
        {
            CefDirPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, CefDirName);

            Random random = new Random();
            int randomNumber = random.Next();
            CefCacheFolder += randomNumber.ToString();
            CefCacheFolderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), CefCacheFolder);
            Logger.Info("Loading CEF from {0}", Globals.CefDirPath);


            // Log from class library
            var consumer = new CEFSharpLib(Logger.GetLoggerObject());
            consumer.LogInfoMessage();
        }

        public static void InitializeGlobals(string[] args) {

            InitLogger();

            if (CmdArgsMaxCount < args.Length)
            {
                Logger.Error("Invalid no. of command line args {0}.", args.Length.ToString());
                Environment.Exit(0); //0 is exit status
            }

            if (args.Length >= 1)
            {
                CefDirName = args[0];
            }

            InitCefSDK();

        }
    }

    public static class CefSharpResultCode
    {
        // Hardcoded values from CefSharp.Enums.ResultCode
        public const int NormalExitProcessNotified = 266;
    }

    public static class Program
    {
        private static Assembly? cefSharpCoreAssembly;
        private static Assembly? cefSharpWinFormsAssembly;

        public static string GetCefVersion()
        {
            try
            {
                if (cefSharpCoreAssembly == null)
                {
                    string cefDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cef");
                    cefSharpCoreAssembly = Assembly.LoadFrom(Path.Combine(cefDir, "CefSharp.Core.dll"));
                    throw new InvalidOperationException("CEF assembly not loaded. Call InitializeCef first.");
                }

                Type cefType = cefSharpCoreAssembly.GetType("CefSharp.Cef");

                // Get version properties
                PropertyInfo chromiumVersionProperty = cefType.GetProperty("ChromiumVersion", BindingFlags.Public | BindingFlags.Static);
                PropertyInfo cefVersionProperty = cefType.GetProperty("CefVersion", BindingFlags.Public | BindingFlags.Static);
                PropertyInfo cefSharpVersionProperty = cefType.GetProperty("CefSharpVersion", BindingFlags.Public | BindingFlags.Static);

                if (chromiumVersionProperty == null || cefVersionProperty == null || cefSharpVersionProperty == null)
                {
                    return "Version information unavailable.";
                }

                string chromiumVersion = (string)chromiumVersionProperty.GetValue(null);
                string cefVersionTmp = (string)cefVersionProperty.GetValue(null);
                string cefVersion = cefVersionTmp.Substring(0, cefVersionTmp.IndexOf("+"));  // Get the substring from the start to the position of '+'
                string cefSharpVersion = (string)cefSharpVersionProperty.GetValue(null);

                // Format the version string
                return string.Format("Chromium: {0}, CEF: {1}, CefSharp: {2}",
                    chromiumVersion, cefVersion, cefSharpVersion);
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
                //Init Globals
                Globals.InitializeGlobals(args);

                // Load assemblies

                cefSharpCoreAssembly = Assembly.LoadFrom(Path.Combine(Globals.CefDirPath, "CefSharp.Core.dll"));
                cefSharpWinFormsAssembly = Assembly.LoadFrom(Path.Combine(Globals.CefDirPath, "CefSharp.WinForms.dll"));

                Logger.Info("Loaded assembly {0}", Globals.CefDirPath + "CefSharp.Core.dll");
                Logger.Info("Loaded assembly {0}", Globals.CefDirPath + "CefSharp.WinForms.dll");

#if ANYCPU
            Assembly cefSharpAssembly = Assembly.LoadFrom(Path.Combine(Globals.CefDir, "CefSharp.dll"));
            Logger.Info("Loaded assembly {0}", Globals.CefDir + "CefSharp.dll");
            Type cefRuntimeType = cefSharpAssembly.GetType("CefSharp.CefRuntime");
            MethodInfo subscribeMethod = cefRuntimeType.GetMethod("SubscribeAnyCpuAssemblyResolver", BindingFlags.Public | BindingFlags.Static);
            subscribeMethod.Invoke(null, new object[] {""});
#endif

                Type cefSettingsType = cefSharpWinFormsAssembly.GetType("CefSharp.WinForms.CefSettings");
                if (cefSettingsType == null)
                {
                    Logger.Error("CefSettings type not found in CefSharp.WinForms.dll.");
                    throw new Exception("CefSettings type not found in CefSharp.WinForms.dll.");
                }

                object cefSettings = Activator.CreateInstance(cefSettingsType);
                cefSettingsType.GetProperty("CachePath")?.SetValue(cefSettings, Globals.CefCacheFolderPath);
                Logger.Info("Cef Cache Path {0}", Globals.CefCacheFolderPath);


                //Example of setting a command line argument
                //Enables WebRTC
                // - CEF Doesn't currently support permissions on a per browser basis see https://bitbucket.org/chromiumembedded/cef/issues/2582/allow-run-time-handling-of-media-access
                // - CEF Doesn't currently support displaying a UI for media access permissions
                //
                //NOTE: WebRTC Device Id's aren't persisted as they are in Chrome see https://bitbucket.org/chromiumembedded/cef/issues/2064/persist-webrtc-deviceids-across-restart
                // Access CefCommandLineArgs property
                PropertyInfo commandLineArgsProperty = cefSettingsType.GetProperty("CefCommandLineArgs");
                object commandLineArgs = commandLineArgsProperty.GetValue(cefSettings);

                // Get the type of CefCommandLineArgs (it's an IDictionary<string, string>)
                Type commandLineArgsType = commandLineArgs.GetType();
                MethodInfo addMethod = commandLineArgsType.GetMethod("Add", new[] { typeof(string), typeof(string) });

                // Add "enable-media-stream" (no value needed, just the switch)
                addMethod.Invoke(commandLineArgs, new object[] { "enable-media-stream", null });

                //https://peter.sh/experiments/chromium-command-line-switches/#use-fake-ui-for-media-stream
                addMethod.Invoke(commandLineArgs, new object[] { "use-fake-ui-for-media-stream", null });

                //For screen sharing add (see https://bitbucket.org/chromiumembedded/cef/issues/2582/allow-run-time-handling-of-media-access#comment-58677180)
                addMethod.Invoke(commandLineArgs, new object[] { "enable-usermedia-screen-capturing", null });


                // Proceed with CEF initialization
                Type cefType = cefSharpCoreAssembly.GetType("CefSharp.Cef");

                MethodInfo initializeMethod = cefType.GetMethod("Initialize", new[] { cefSettingsType });

                //Perform dependency check to make sure all relevant resources are in our output directory.
                bool success = (bool)initializeMethod.Invoke(null, new object[] { cefSettings });

                if (!success)
                {
                    // Get the exit code after initialization failure
                    MethodInfo getExitCodeMethod = cefType.GetMethod("GetExitCode", BindingFlags.Public | BindingFlags.Static);
                    if (getExitCodeMethod == null)
                    {
                        Logger.Error("Cef.Initialize failed and GetExitCode method not found.");
                        MessageBox.Show("Cef.Initialize failed and GetExitCode method not found.", "Initialization Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return 0;
                    }

                    int exitCode = (int)getExitCodeMethod.Invoke(null, null);


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
                var brwoserObject = BrowserForm.GetBrowserFormObject();
                Application.Run(brwoserObject);                
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
