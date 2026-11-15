using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Core;

namespace AerisBrowser.Core
{
    public class PrivacyManager
    {
        public bool HttpsFirstEnabled { get; set; }
        public bool BlockThirdPartyCookies { get; set; }
        public bool DoNotTrackHeaderEnabled { get; set; }

        public PrivacyManager()
        {
            HttpsFirstEnabled = true;
            BlockThirdPartyCookies = true;
            DoNotTrackHeaderEnabled = true;
        }

        public string EnsureSecureUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return url;

            if (HttpsFirstEnabled && url.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            {
                // Do not upgrade localhost or 127.0.0.1
                if (!url.StartsWith("http://localhost", StringComparison.OrdinalIgnoreCase) &&
                    !url.StartsWith("http://127.0.0.1", StringComparison.OrdinalIgnoreCase))
                {
                    return "https://" + url.Substring(7);
                }
            }
            return url;
        }

        public void ConfigureSecuritySettings(CoreWebView2Settings settings)
        {
            if (settings == null) return;

            // Enhance security and reduce memory overhead
            settings.IsPasswordAutosaveEnabled = false; // Prevent plain-text local storage of credentials
            settings.IsGeneralAutofillEnabled = false;
            settings.AreDefaultScriptDialogsEnabled = true;
            settings.IsWebMessageEnabled = true;
            settings.AreDevToolsEnabled = true; // Accessible via F12 for developers
            settings.IsStatusBarEnabled = false; // Minimal UI
        }

        public void HandlePermissionRequested(object sender, CoreWebView2PermissionRequestedEventArgs e)
        {
            // Protect privacy by denying sensitive permissions unless approved
            if (e.PermissionKind == CoreWebView2PermissionKind.Camera ||
                e.PermissionKind == CoreWebView2PermissionKind.Microphone ||
                e.PermissionKind == CoreWebView2PermissionKind.Geolocation)
            {
                // By default in a privacy-first browser, prompt or deny silent sensor access
                e.State = CoreWebView2PermissionState.Default;
            }
            else if (e.PermissionKind == CoreWebView2PermissionKind.Notifications)
            {
                // Suppress annoying web notification popups
                e.State = CoreWebView2PermissionState.Deny;
            }
        }
    }
}
