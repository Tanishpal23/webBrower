using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using AerisBrowser.UI;
using Microsoft.Web.WebView2.Core;

namespace AerisBrowser
{
    static class Program
    {
        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        [STAThread]
        static void Main(string[] args)
        {
            try
            {
                SetProcessDPIAware();
            }
            catch {}

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            bool isIncognito = false;
            string startUrl = "about:home";

            foreach (var arg in args)
            {
                if (arg.Equals("--incognito", StringComparison.OrdinalIgnoreCase) ||
                    arg.Equals("-incognito", StringComparison.OrdinalIgnoreCase) ||
                    arg.Equals("/incognito", StringComparison.OrdinalIgnoreCase))
                {
                    isIncognito = true;
                }
                else if (!arg.StartsWith("-") && !arg.StartsWith("/"))
                {
                    startUrl = arg;
                }
            }

            // User data directory
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string dataDir = isIncognito
                ? Path.Combine(Path.GetTempPath(), "AerisIncognito_" + Guid.NewGuid().ToString("N"))
                : Path.Combine(appData, "AerisBrowser", "UserData");

            if (!Directory.Exists(dataDir))
                Directory.CreateDirectory(dataDir);

            try
            {
                // Initialize WebView2 Environment with performance switches
                // Switches: --disable-background-timer-throttling=false, --disable-features=Translate
                var envOptions = new CoreWebView2EnvironmentOptions(
                    "--disable-features=Translate,OptimizationHints,MediaRouter --enable-features=NetworkServiceInProcess --disable-breakpad --disable-component-update");

                var env = CoreWebView2Environment.CreateAsync(null, dataDir, envOptions).GetAwaiter().GetResult();

                var mainForm = new MainForm(env, dataDir, isIncognito);
                if (!string.IsNullOrEmpty(startUrl) && startUrl != "about:home")
                {
                    // If a custom start url was passed on command line
                    // The main form will navigate to it
                }

                Application.Run(mainForm);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Failed to initialize Aeris Browser:\n" + ex.Message + "\n\nPlease ensure Microsoft Edge WebView2 Evergreen Runtime is installed.",
                    "Aeris Startup Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}

