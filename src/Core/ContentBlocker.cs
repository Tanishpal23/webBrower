using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace AerisBrowser.Core
{
    public enum ContentBlockerMode
    {
        Disabled = 0,
        Standard = 1,
        Aggressive = 2
    }

    public class ContentBlocker
    {
        private static readonly HashSet<string> BlockedDomains = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> BlockedKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _whitelist = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<string>> _tabBlockedDomains = new Dictionary<string, List<string>>();

        private long _totalBlockedCount = 0;
        public ContentBlockerMode Mode { get; set; }
        public bool CosmeticFilteringEnabled { get; set; }

        public const string CosmeticFilterScript = @"
(function() {
    const css = `
.adsbygoogle, .ad-container, .ad-wrapper, .ad-banner, .advertisement,
.sponsored-post, .sponsor-box, [id*=""google_ads""], [class*=""google_ads""],
[id*=""ad-slot""], [class*=""ad-slot""], [id*=""dfp-ad""], [data-ad-slot],
.taboola-container, .outbrain-container, [data-ad-unit], .trc_related_container,
.ad-unit, .ads-banner, #ad_banner, #leaderboard-ad {
    display: none !important;
    visibility: hidden !important;
    height: 0 !important;
    width: 0 !important;
    opacity: 0 !important;
    pointer-events: none !important;
}`;
    function injectStyle() {
        if (!document.getElementById('aeris-cosmetic-filter')) {
            const style = document.createElement('style');
            style.id = 'aeris-cosmetic-filter';
            style.textContent = css;
            if (document.head) document.head.appendChild(style);
            else if (document.documentElement) document.documentElement.appendChild(style);
        }
    }
    injectStyle();
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', injectStyle);
    }
})();
";

        public long TotalBlockedCount { get { return Interlocked.Read(ref _totalBlockedCount); } }

        public ContentBlocker()
        {
            Mode = ContentBlockerMode.Standard;
            CosmeticFilteringEnabled = true;
            InitializeBlocklists();
        }

        private void InitializeBlocklists()
        {
            // Core advertising & tracker domains (Peter Lowe AdServers + EasyList core)
            string[] domains = new string[] {
                // Major Ad Networks
                "doubleclick.net", "googleadservices.com", "googlesyndication.com", "adservice.google.com",
                "adnxs.com", "adsrvr.org", "criteo.com", "criteo.net", "outbrain.com", "taboola.com",
                "rubiconproject.com", "openx.net", "pubmatic.com", "casalemedia.com", "advertising.com",
                "scorecardresearch.com", "quantserve.com", "zedo.com", "adroll.com", "exponential.com",
                "popads.net", "popcash.net", "propellerads.com", "exoclick.com", "revcontent.com",
                "mgid.com", "adblade.com", "adtechus.com", "tribalfusion.com", "yieldmanager.com",
                "bidswitch.net", "smartadserver.com", "indexexchange.com", "spotxchange.com",
                "admob.com", "flurry.com", "chartbeat.com", "hotjar.com", "crazyegg.com",
                "mouseflow.com", "fullstory.com", "mixpanel.com", "segment.io", "branch.io",
                "appsflyer.com", "adjust.com", "newrelic.com", "nr-data.net", "sentry.io",
                "bugsnag.com", "datadoghq.com", "optimizely.com", "vwo.com",

                // Tracking & Telemetry
                "telemetry.microsoft.com", "vortex.data.microsoft.com", "browser.events.data.msn.com",
                "events.reddit.com", "pixel.facebook.com", "an.facebook.com", "analytics.twitter.com",
                "ads-twitter.com", "t.co/ads", "tr.snapchat.com", "analytics.tiktok.com",
                "ads.tiktok.com", "ads-api.tiktok.com", "yandex.ru/metrika", "mc.yandex.ru"
            };

            foreach (var d in domains)
            {
                BlockedDomains.Add(d);
            }

            // Keyword heuristics for Aggressive mode
            string[] keywords = new string[] {
                "/ads.js", "/ad.js", "/advertisement", "/sponsor", "/affiliate", "/telemetry", "/tracker"
            };
            foreach (var k in keywords)
            {
                BlockedKeywords.Add(k);
            }
        }

        public bool IsWhitelisted(string host)
        {
            if (string.IsNullOrEmpty(host)) return false;
            lock (_whitelist)
            {
                return _whitelist.Contains(host);
            }
        }

        public void AddToWhitelist(string host)
        {
            if (string.IsNullOrEmpty(host)) return;
            lock (_whitelist)
            {
                _whitelist.Add(host.ToLowerInvariant());
            }
        }

        public void RemoveFromWhitelist(string host)
        {
            if (string.IsNullOrEmpty(host)) return;
            lock (_whitelist)
            {
                _whitelist.Remove(host.ToLowerInvariant());
            }
        }

        public List<string> GetWhitelist()
        {
            lock (_whitelist)
            {
                return new List<string>(_whitelist);
            }
        }

        public bool ShouldBlock(Uri uri, out string reason)
        {
            reason = null;
            if (Mode == ContentBlockerMode.Disabled || uri == null)
                return false;

            string host = uri.Host.ToLowerInvariant();

            // Check whitelist
            if (IsWhitelisted(host))
                return false;

            // Direct match or parent domain match (e.g. ad.doubleclick.net -> doubleclick.net)
            if (IsDomainBlocked(host))
            {
                Interlocked.Increment(ref _totalBlockedCount);
                reason = "Blocked ad/tracker domain: " + host;
                return true;
            }

            // Aggressive mode: check path patterns
            if (Mode == ContentBlockerMode.Aggressive)
            {
                string pathAndQuery = uri.PathAndQuery.ToLowerInvariant();
                foreach (var kw in BlockedKeywords)
                {
                    if (pathAndQuery.Contains(kw))
                    {
                        Interlocked.Increment(ref _totalBlockedCount);
                        reason = "Blocked tracking path pattern: " + kw;
                        return true;
                    }
                }
            }

            return false;
        }

        private bool IsDomainBlocked(string host)
        {
            if (BlockedDomains.Contains(host))
                return true;

            // Check subdomains
            int dotIdx = host.IndexOf('.');
            while (dotIdx > 0 && dotIdx < host.Length - 1)
            {
                string parent = host.Substring(dotIdx + 1);
                if (BlockedDomains.Contains(parent))
                    return true;
                dotIdx = host.IndexOf('.', dotIdx + 1);
            }

            return false;
        }

        public void ResetStats()
        {
            Interlocked.Exchange(ref _totalBlockedCount, 0);
        }
    }
}

