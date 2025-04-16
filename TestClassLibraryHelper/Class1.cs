using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using System.IO;
using System.Windows.Forms;
using System.Security.AccessControl;

namespace CefSharpIntegration
{
    public class CefSharpHelper
    {
        private string cefDirPath;
        private Assembly cefSharpAssembly;
        private Assembly cefSharpWinFormsAssembly;
        private Assembly cefSharpCoreAssembly;
        private object browser;
        private Type browserType;
        public event EventHandler BrowserInitialized;
        public event Action<string> BrowserAddressChanged;
        public event Action<string> TitleChanged;
        public event Action<string> OnConsoleMessageReceived;



        public CefSharpHelper(string cefDirPath)
        {
            this.cefDirPath = cefDirPath;

            MessageBox.Show(cefDirPath);


            cefSharpWinFormsAssembly = Assembly.LoadFrom(Path.Combine(cefDirPath, "CefSharp.WinForms.dll"));
            cefSharpCoreAssembly = Assembly.LoadFrom(Path.Combine(cefDirPath, "CefSharp.Core.dll"));
            cefSharpAssembly = Assembly.LoadFrom(Path.Combine(cefDirPath, "CefSharp.dll"));


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

        public void AddCommandLineArgs(object cefSettings, string command)
        {
            // Get CefCommandLineArgs property
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

            addMethod.Invoke(commandLineArgs, new object[] { command, null });

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

            // It's typically a Dictionary<string, string>
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
                MessageBox.Show($"[{key}] = {value}");
            }
        }

        public Control InitializeBrowser(string initialUrl = "https://www.google.com")
        {
            try
            {

                browserType = cefSharpWinFormsAssembly.GetType("CefSharp.WinForms.ChromiumWebBrowser");
                browser = Activator.CreateInstance(browserType);

                if (browser == null)
                {
                    MessageBox.Show("Failed to create ChromiumWebBrowser instance.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    //return 1;
                }


                Control browserControl = (Control)browser;
                browserControl.Dock = DockStyle.Fill;


                EventInfo isBrowserInitializedChangedEvent = browserType.GetEvent("IsBrowserInitializedChanged");

                if (isBrowserInitializedChangedEvent != null)
                {
                    MethodInfo eventHandlerMethod = typeof(CefSharpHelper).GetMethod(nameof(OnIsBrowserInitializedChanged), BindingFlags.NonPublic | BindingFlags.Instance);
                    Delegate handler = Delegate.CreateDelegate(isBrowserInitializedChangedEvent.EventHandlerType, this, eventHandlerMethod);
                    isBrowserInitializedChangedEvent.AddEventHandler(browser, handler);
                }



                EventInfo addressChangedEvent = browserType.GetEvent("AddressChanged");
                if (addressChangedEvent != null)
                {
                    MethodInfo eventHandlerMethod = typeof(CefSharpHelper).GetMethod("OnBrowserAddressChanged", BindingFlags.NonPublic | BindingFlags.Instance);

                    if (eventHandlerMethod != null)
                    {
                        Delegate handler = Delegate.CreateDelegate(addressChangedEvent.EventHandlerType, this, eventHandlerMethod);
                        addressChangedEvent.AddEventHandler(browser, handler);
                    }
                    else
                    {
                        MessageBox.Show("OnBrowserAddressChanged method not found.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                else
                {
                    MessageBox.Show("AddressChanged event not found in ChromiumWebBrowser.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }


                // Add handler for TitleChanged event
                EventInfo titleChangedEvent = browserType.GetEvent("TitleChanged");
                if (titleChangedEvent != null)
                {
                    MethodInfo titleChangedHandlerMethod = typeof(CefSharpHelper).GetMethod("OnBrowserTitleChanged", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (titleChangedHandlerMethod != null)
                    {
                        Delegate handler = Delegate.CreateDelegate(titleChangedEvent.EventHandlerType, this, titleChangedHandlerMethod);
                        titleChangedEvent.AddEventHandler(browser, handler);
                    }
                    else
                    {
                        MessageBox.Show("OnBrowserTitleChanged method not found.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                else
                {
                    MessageBox.Show("TitleChanged event not found in ChromiumWebBrowser.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }


                EventInfo loadingStateChangedEvent = browserType.GetEvent("LoadingStateChanged");
                if (loadingStateChangedEvent != null)
                {
                    MethodInfo loadingHandlerMethod = typeof(CefSharpHelper).GetMethod("OnLoadingStateChanged", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (loadingHandlerMethod != null)
                    {
                        Delegate handler = Delegate.CreateDelegate(loadingStateChangedEvent.EventHandlerType, this, loadingHandlerMethod);
                        loadingStateChangedEvent.AddEventHandler(browser, handler);
                    }
                    else
                    {
                        MessageBox.Show("OnLoadingStateChanged method not found.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                else
                {
                    MessageBox.Show("LoadingStateChanged event not found in ChromiumWebBrowser.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }



                EventInfo statusMessageEvent = browserType.GetEvent("StatusMessage");
                if (statusMessageEvent != null)
                {
                    MethodInfo statusMessageHandlerMethod = typeof(CefSharpHelper).GetMethod("OnBrowserStatusMessage", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (statusMessageHandlerMethod != null)
                    {
                        Delegate handler = Delegate.CreateDelegate(statusMessageEvent.EventHandlerType, this, statusMessageHandlerMethod);
                        statusMessageEvent.AddEventHandler(browser, handler);
                    }
                    else
                    {
                        MessageBox.Show("OnBrowserStatusMessage method not found.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                else
                {
                    MessageBox.Show("StatusMessage event not found in ChromiumWebBrowser.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }


                EventInfo consoleMessageEvent = browserType.GetEvent("ConsoleMessage");
                if (consoleMessageEvent != null)
                {
                    MethodInfo handlerMethod = typeof(CefSharpHelper).GetMethod(nameof(OnBrowserConsoleMessage), BindingFlags.NonPublic | BindingFlags.Instance);
                    if (handlerMethod != null)
                    {
                        Delegate handler = Delegate.CreateDelegate(consoleMessageEvent.EventHandlerType, this, handlerMethod);
                        consoleMessageEvent.AddEventHandler(browser, handler);
                    }
                    else
                    {
                        throw new MissingMethodException("OnBrowserConsoleMessage method not found.");
                    }
                }
                else
                {
                    throw new MissingMemberException("ConsoleMessage event not found in ChromiumWebBrowser.");
                }




                return browserControl;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error Initializing Browser: {ex.Message}", "Browser Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return null;
            }
        }

        private void OnIsBrowserInitializedChanged(object sender, EventArgs e)
        {
            MessageBox.Show("calling OnIsBrowserInitializedChanged");
            BrowserInitialized?.Invoke(this, EventArgs.Empty);
        }

        private void OnBrowserAddressChanged(object sender, object args)
        {
            try
            {

                MessageBox.Show("calling OnBrowserAddressChanged ");
                // Get AddressChangedEventArgs type
                Type addressChangedEventArgsType = cefSharpAssembly.GetType("CefSharp.AddressChangedEventArgs");

                // Get the Address property
                PropertyInfo addressProperty = addressChangedEventArgsType.GetProperty("Address");
                string newAddress = (string)addressProperty.GetValue(args);

                BrowserAddressChanged?.Invoke(newAddress);


            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error handling AddressChanged: {ex.Message}", "Event Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
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

        private void OnBrowserTitleChanged(object sender, object args)
        {
            try
            {
                // Get the Title property from TitleChangedEventArgs
                Type titleChangedEventArgsType = cefSharpAssembly.GetType("CefSharp.TitleChangedEventArgs");
                PropertyInfo titleProperty = titleChangedEventArgsType.GetProperty("Title");
                string newTitle = (string)titleProperty.GetValue(args);

                MessageBox.Show($"Title Changed: {newTitle}");

                TitleChanged?.Invoke(newTitle);

                // Optionally update form title if running in a form
                //this.InvokeOnUiThreadIfRequired(() => Text = "Browser - " + newTitle);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error handling OnBrowserTitleChanged: {ex.Message}", "Event Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
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
            MessageBox.Show("OnBrowserConsoleMsg");
            try
            {
                Type eventArgsType = cefSharpAssembly.GetType("CefSharp.ConsoleMessageEventArgs");
                PropertyInfo messageProp = eventArgsType.GetProperty("Message");
                PropertyInfo sourceProp = eventArgsType.GetProperty("Source");
                PropertyInfo lineProp = eventArgsType.GetProperty("Line");

                string message = (string)messageProp.GetValue(args);
                string source = (string)sourceProp.GetValue(args);
                int line = (int)lineProp.GetValue(args);

                string output = $"Console Message - Line: {line}, Source: {source}, Message: {message}";
                OnConsoleMessageReceived?.Invoke(output);
            }
            catch (Exception ex)
            {
                OnConsoleMessageReceived?.Invoke($"Error in ConsoleMessage: {ex.Message}");
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
                MessageBox.Show($"Error disposing ChromiumWebBrowser: {ex.Message}");
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
                MessageBox.Show($"Error handling ShowDevTools: {ex.Message}", "Event Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                MessageBox.Show("Cef.Initialize failed and GetExitCode method not found.", "Initialization Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 0;
            }

            int exitCode = (int)getExitCodeMethod.Invoke(null, null);
            return exitCode;
        }


    }
}
