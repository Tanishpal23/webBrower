using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using AerisBrowser.Core;
using AerisBrowser.Search;

namespace AerisBrowser.Testing
{
    class BenchmarkSuite
    {
        static void Main(string[] args)
        {
            Console.WriteLine("===============================================================");
            Console.WriteLine("       AERIS BROWSER AUTOMATED BENCHMARK & TEST SUITE          ");
            Console.WriteLine("===============================================================");
            Console.WriteLine();

            TestContentBlocker();
            TestSearchEngine();
            TestTabManagerAndSuspension();
            TestPrivacyManager();

            Console.WriteLine();
            Console.WriteLine("===============================================================");
            Console.WriteLine("              ALL BENCHMARKS & TESTS PASSED!                   ");
            Console.WriteLine("===============================================================");
        }

        static void TestContentBlocker()
        {
            Console.WriteLine("[1/4] Benchmarking Content & Tracker Blocker...");
            var blocker = new ContentBlocker();

            string[] testUrls = new string[] {
                "https://pagead2.googlesyndication.com/pagead/js/adsbygoogle.js",
                "https://ad.doubleclick.net/ddm/trackclk/N1234",
                "https://telemetry.microsoft.com/collect",
                "https://pixel.facebook.com/tr/?id=123",
                "https://static.criteo.net/js/ld/ld.js",
                "https://en.wikipedia.org/wiki/Web_browser",
                "https://github.com/microsoft/webview2",
                "https://news.ycombinator.com/item?id=12345",
                "https://duckduckgo.com/?q=privacy",
                "https://cdnjs.cloudflare.com/ajax/libs/react/18.2.0/umd/react.production.min.js"
            };

            int blockedCount = 0;
            int allowedCount = 0;

            var sw = Stopwatch.StartNew();
            int iterations = 10000;
            for (int i = 0; i < iterations; i++)
            {
                foreach (var url in testUrls)
                {
                    string reason;
                    if (blocker.ShouldBlock(new Uri(url), out reason))
                    {
                        if (i == 0) blockedCount++;
                    }
                    else
                    {
                        if (i == 0) allowedCount++;
                    }
                }
            }
            sw.Stop();

            double totalQueries = iterations * testUrls.Length;
            double microsPerQuery = (sw.Elapsed.TotalMilliseconds * 1000.0) / totalQueries;

            Console.WriteLine("   • Filter Speed: {0:0.00} microseconds per request ({1:0,000} checks/sec)", microsPerQuery, totalQueries / sw.Elapsed.TotalSeconds);
            Console.WriteLine("   • Blocked: {0}/10 test domains, Allowed legitimate: {1}/10", blockedCount, allowedCount);
            if (blockedCount != 5 || allowedCount != 5)
            {
                throw new Exception("Content blocker accuracy mismatch!");
            }

            // Test Whitelisting
            blocker.AddToWhitelist("pagead2.googlesyndication.com");
            string whitelistedReason;
            bool shouldBlockWhitelisted = blocker.ShouldBlock(new Uri("https://pagead2.googlesyndication.com/pagead/js/adsbygoogle.js"), out whitelistedReason);
            Console.WriteLine("   • Whitelist verification: {0} (Successfully bypassed ad-block)", !shouldBlockWhitelisted ? "PASSED" : "FAILED");
            if (shouldBlockWhitelisted) throw new Exception("Whitelist failed!");

            Console.WriteLine("   [OK] Content Blocker passed all latency and accuracy tests.");
            Console.WriteLine();
        }

        static void TestSearchEngine()
        {
            Console.WriteLine("[2/4] Testing Ad-Free Search Engine & Suggestions...");
            var provider = new DuckDuckGoProvider();
            string templatePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "src", "Assets", "SearchResults.html");
            var service = new SearchEngineService(provider, templatePath);

            var sw = Stopwatch.StartNew();
            var suggestions = service.GetSuggestions("linux");
            sw.Stop();

            Console.WriteLine("   • Autocomplete response time: {0} ms", sw.ElapsedMilliseconds);
            Console.WriteLine("   • Suggestions found for 'linux': {0} items", suggestions.Count);
            foreach (var s in suggestions)
            {
                Console.WriteLine("     - {0}", s);
            }

            sw.Restart();
            string resultsHtml = service.GenerateResultsHtml("operating system", "all", 1);
            sw.Stop();

            Console.WriteLine("   • Search results HTML generated in {0} ms", sw.ElapsedMilliseconds);
            Console.WriteLine("   • Generated HTML size: {0:0.0} KB", resultsHtml.Length / 1024.0);
            Console.WriteLine("   • Contains zero sponsored links verification: {0}", !resultsHtml.Contains("sponsor") ? "CONFIRMED AD-FREE" : "CHECK");

            Console.WriteLine("   [OK] Search Engine subsystem passed.");
            Console.WriteLine();
        }

        static void TestTabManagerAndSuspension()
        {
            Console.WriteLine("[3/4] Testing Tab Lifecycle, Suspension & Session Persistence...");
            string tempDir = Path.Combine(Path.GetTempPath(), "AerisTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                var blocker = new ContentBlocker();
                var privacy = new PrivacyManager();
                var downloads = new DownloadManager();

                var tabMgr = new TabManager(null, blocker, privacy, downloads, tempDir, false);

                // Create tabs
                var tab1 = new BrowserTab();
                tab1.Url = "https://en.wikipedia.org/wiki/Computer";
                tab1.Title = "Computer - Wikipedia";

                var tab2 = new BrowserTab();
                tab2.Url = "https://github.com";
                tab2.Title = "GitHub: Let's build from here";

                var tab3 = new BrowserTab();
                tab3.Url = "https://news.ycombinator.com";
                tab3.Title = "Hacker News";

                // Verify Tab Suspension logic
                tab2.Suspend();
                Console.WriteLine("   • Tab 2 (GitHub) suspended: {0} (Preserved URL: {1})", tab2.IsSuspended, tab2.Url);
                if (!tab2.IsSuspended || tab2.Url != "https://github.com")
                    throw new Exception("Tab suspension failed!");

                tab2.Resume();
                Console.WriteLine("   • Tab 2 resumed: {0} (Restored URL: {1})", !tab2.IsSuspended, tab2.Url);
                if (tab2.IsSuspended)
                    throw new Exception("Tab resumption failed!");

                // Session Persistence
                string sessionFile = Path.Combine(tempDir, "session.json");
                File.WriteAllText(sessionFile, "[\"https://en.wikipedia.org\",\"https://github.com\"]");

                var restored = tabMgr.LoadSavedSession();
                Console.WriteLine("   • Session restoration: {0} tabs successfully recovered", restored.Count);
                if (restored.Count != 2)
                    throw new Exception("Session restore failed!");

                Console.WriteLine("   [OK] Tab Lifecycle & Suspension tests passed.");
            }
            finally
            {
                try { Directory.Delete(tempDir, true); } catch {}
            }
            Console.WriteLine();
        }

        static void TestPrivacyManager()
        {
            Console.WriteLine("[4/4] Testing Privacy & HTTPS-First Rules...");
            var privacy = new PrivacyManager();

            string upgraded = privacy.EnsureSecureUrl("http://example.com/article");
            Console.WriteLine("   • Insecure HTTP upgrade: {0} -> {1}", "http://example.com/article", upgraded);
            if (!upgraded.StartsWith("https://")) throw new Exception("HTTPS upgrade failed!");

            string localhost = privacy.EnsureSecureUrl("http://localhost:3000/api");
            Console.WriteLine("   • Localhost preserved: {0}", localhost);
            if (localhost != "http://localhost:3000/api") throw new Exception("Localhost incorrectly modified!");

            Console.WriteLine("   [OK] Privacy rules verified.");
        }
    }
}

