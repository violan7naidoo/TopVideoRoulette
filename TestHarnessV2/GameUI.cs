using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;

namespace TestHarnessV2
{
    public static class Logger
    {
        private static string LogFilePath;
        private static readonly object _lock = new object();

        static Logger()
        {
            string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string logsFolder = Path.Combine(documentsPath, "Game-logs");
            try
            {
                if (!Directory.Exists(logsFolder))
                    Directory.CreateDirectory(logsFolder);
            }
            catch { logsFolder = documentsPath; }

            string fileName = $"GameUI_Log_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
            string logPath = Path.Combine(logsFolder, fileName);
            try
            {
                System.IO.File.WriteAllText(logPath, $"=== GameUI Log Started at {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===\r\n\r\n", Encoding.UTF8);
                LogFilePath = logPath;
            }
            catch
            {
                try
                {
                    logPath = Path.Combine(documentsPath, fileName);
                    System.IO.File.WriteAllText(logPath, $"=== GameUI Log Started at {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===\r\n\r\n", Encoding.UTF8);
                    LogFilePath = logPath;
                }
                catch { LogFilePath = Path.Combine(Application.StartupPath, fileName); }
            }
        }

        public static void Log(string message)
        {
            lock (_lock)
            {
                try
                {
                    var logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}";
                    Console.WriteLine(logEntry);
                    System.IO.File.AppendAllText(LogFilePath, logEntry + "\r\n", Encoding.UTF8);
                }
                catch (Exception ex) { try { Console.WriteLine($"Logging error: {ex.Message}"); } catch { } }
            }
        }

        public static void LogError(string message, Exception ex = null)
        {
            var errorMessage = $"[ERROR] {message}";
            if (ex != null) errorMessage += $": {ex.Message}\r\nStack Trace: {ex.StackTrace}";
            Log(errorMessage);
        }

        public static string GetLogFilePath() => LogFilePath;
    }

    public partial class GameUI2 : Form
    {
        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, uint dwNewLong);
        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        private const int GWL_STYLE = -16;
        private const uint WS_POPUP = 0x80000000;
        private const uint WS_VISIBLE = 0x10000000;
        private static readonly IntPtr HWND_TOP = new IntPtr(0);
        private const uint SWP_SHOWWINDOW = 0x0040;

        private const string GameUrl = "https://stagingretail.gameonstudios.bet/";
        private const double WindowScale = 1.0; // 100% full screen
        private const double DefaultZoomFactor = 0.6; // 60% zoom out by default (match browser zoom-out on large TV)
        private const int CacheClearIntervalMs = 2 * 60 * 60 * 1000; // 2 hours
        private System.Windows.Forms.Timer _cacheClearTimer;

        public GameUI2()
        {
            InitializeComponent();

            try { this.Icon = new Icon(Path.Combine(Application.StartupPath, "images", "gameonstudios.ico")); } catch { }
            this.ShowIcon = true;
            this.ShowInTaskbar = true;

            var screen = Screen.PrimaryScreen;
            int w = (int)(screen.Bounds.Width * WindowScale);
            int h = (int)(screen.Bounds.Height * WindowScale);
            int x = (screen.Bounds.Width - w) / 2;
            int y = (screen.Bounds.Height - h) / 2;

            this.FormBorderStyle = FormBorderStyle.None;
            this.WindowState = FormWindowState.Normal;
            this.StartPosition = FormStartPosition.Manual;
            this.TopMost = false;
            this.Padding = new Padding(0);
            this.Margin = new Padding(0);
            this.Bounds = new Rectangle(x, y, w, h);
            this.Location = new Point(x, y);
            this.Size = new Size(w, h);

            this.Load += GameUI2_Load;
            this.Resize += GameUI2_Resize;
            this.Shown += GameUI2_Shown;

            Logger.Log("Fullscreen app started – loading " + GameUrl);
        }

        private void GameUI2_Load(object sender, EventArgs e)
        {
            var screen = Screen.PrimaryScreen;
            int w = (int)(screen.Bounds.Width * WindowScale);
            int h = (int)(screen.Bounds.Height * WindowScale);
            int x = (screen.Bounds.Width - w) / 2;
            int y = (screen.Bounds.Height - h) / 2;
            IntPtr handle = this.Handle;
            SetWindowLong(handle, GWL_STYLE, WS_POPUP | WS_VISIBLE);
            SetWindowPos(handle, HWND_TOP, x, y, w, h, SWP_SHOWWINDOW);
            this.Bounds = new Rectangle(x, y, w, h);
        }

        private void GameUI2_Shown(object sender, EventArgs e)
        {
            var screen = Screen.PrimaryScreen;
            int w = (int)(screen.Bounds.Width * WindowScale);
            int h = (int)(screen.Bounds.Height * WindowScale);
            int x = (screen.Bounds.Width - w) / 2;
            int y = (screen.Bounds.Height - h) / 2;
            this.Bounds = new Rectangle(x, y, w, h);
            this.Location = new Point(x, y);
            if (webView_Main != null)
            {
                webView_Main.Size = new Size(w, h);
                webView_Main.Location = new Point(0, 0);
            }
        }

        private void GameUI2_Resize(object sender, EventArgs e)
        {
            if (webView_Main == null) return;
            var screen = Screen.PrimaryScreen;
            int w = (int)(screen.Bounds.Width * WindowScale);
            int h = (int)(screen.Bounds.Height * WindowScale);
            var newSize = new Size(w, h);
            var newLoc = new Point(0, 0);
            if (webView_Main.Size != newSize || webView_Main.Location != newLoc)
            {
                webView_Main.Size = newSize;
                webView_Main.Location = newLoc;
            }
        }

        public void Form1_Load(object sender, EventArgs e)
        {
            panel2.Dock = DockStyle.Fill;
            panel2.Visible = true;
            panel2.BringToFront();
            webView_Main.Dock = DockStyle.Fill;
            webView_Main.Visible = true;
            InitializeWebView2();
        }

        private async void InitializeWebView2()
        {
            try
            {
                if (webView_Main.CoreWebView2 == null)
                    await webView_Main.EnsureCoreWebView2Async();

                var coreWebView2 = webView_Main.CoreWebView2;
                if (coreWebView2 == null) throw new Exception("Failed to initialize CoreWebView2");

                var settings = coreWebView2.Settings;
                settings.IsScriptEnabled = true;
                settings.IsStatusBarEnabled = false;
                settings.IsZoomControlEnabled = true;
                settings.AreDefaultContextMenusEnabled = false;
                coreWebView2.ContextMenuRequested += (s, e) => e.Handled = true;

                // Apply default zoom after page loads so URL scale/sizing is applied first (like browser)
                coreWebView2.NavigationCompleted += OnNavigationCompleted;

                // Clear disk cache so it does not slow down the app
                await coreWebView2.Profile.ClearBrowsingDataAsync(
                    CoreWebView2BrowsingDataKinds.DiskCache,
                    DateTime.Now.AddYears(-10),
                    DateTime.Now);

                var screen = Screen.PrimaryScreen;
                int w = (int)(screen.Bounds.Width * WindowScale);
                int h = (int)(screen.Bounds.Height * WindowScale);
                webView_Main.Size = new Size(w, h);
                webView_Main.Location = new Point(0, 0);

                webView_Main.Source = new Uri(GameUrl);

                // Clear cache every 2 hours
                _cacheClearTimer = new System.Windows.Forms.Timer();
                _cacheClearTimer.Interval = CacheClearIntervalMs;
                _cacheClearTimer.Tick += CacheClearTimer_Tick;
                _cacheClearTimer.Start();
            }
            catch (Exception ex)
            {
                Logger.LogError("Error initializing WebView2: " + ex.Message, ex);
            }
        }

        private void OnNavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            if (!e.IsSuccess || webView_Main?.CoreWebView2 == null) return;
            try
            {
                webView_Main.ZoomFactor = DefaultZoomFactor;
            }
            catch { /* ignore */ }
        }

        private async void CacheClearTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                var coreWebView2 = webView_Main?.CoreWebView2;
                if (coreWebView2 == null) return;

                await coreWebView2.Profile.ClearBrowsingDataAsync(
                    CoreWebView2BrowsingDataKinds.DiskCache,
                    DateTime.Now.AddYears(-10),
                    DateTime.Now);
                Logger.Log("[CACHE] Cleared disk cache (scheduled 2-hour clear)");
            }
            catch (Exception ex)
            {
                Logger.LogError("[CACHE] Error clearing cache: " + ex.Message, ex);
            }
        }
    }
}
