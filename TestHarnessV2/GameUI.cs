using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
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
            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string logsFolder = Path.Combine(desktopPath, "Game-logs");
            try
            {
                if (!Directory.Exists(logsFolder))
                    Directory.CreateDirectory(logsFolder);
            }
            catch { logsFolder = desktopPath; }

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
                    logPath = Path.Combine(desktopPath, fileName);
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
        private readonly Video _video;
        private bool _pageLoadDone;

        public GameUI2()
        {
            InitializeComponent();
            _video = new Video(webView_Main);

            try { this.Icon = new Icon(Path.Combine(Application.StartupPath, "images", "gameonstudios.ico")); } catch { }
            this.ShowIcon = true;
            this.ShowInTaskbar = true;

            var screen = Screen.PrimaryScreen;
            this.FormBorderStyle = FormBorderStyle.None;
            this.WindowState = FormWindowState.Normal;
            this.StartPosition = FormStartPosition.Manual;
            this.TopMost = false;
            this.Padding = new Padding(0);
            this.Margin = new Padding(0);
            this.Bounds = screen.Bounds;
            this.Location = new Point(0, 0);
            this.Size = screen.Bounds.Size;

            this.Load += GameUI2_Load;
            this.Resize += GameUI2_Resize;
            this.Shown += GameUI2_Shown;

            Logger.Log("=== Video fullscreen app started ===");
        }

        private void GameUI2_Load(object sender, EventArgs e)
        {
            var screen = Screen.PrimaryScreen;
            IntPtr handle = this.Handle;
            SetWindowLong(handle, GWL_STYLE, WS_POPUP | WS_VISIBLE);
            SetWindowPos(handle, HWND_TOP, 0, 0, screen.Bounds.Width, screen.Bounds.Height, SWP_SHOWWINDOW);
            this.Bounds = screen.Bounds;
        }

        private void GameUI2_Shown(object sender, EventArgs e)
        {
            var screen = Screen.PrimaryScreen;
            this.Bounds = screen.Bounds;
            this.Location = new Point(0, 0);
            if (webView_Main != null)
            {
                webView_Main.Size = new Size(screen.Bounds.Width, screen.Bounds.Height);
                webView_Main.Location = new Point(0, 0);
            }
        }

        private void GameUI2_Resize(object sender, EventArgs e)
        {
            if (webView_Main == null) return;
            var screen = Screen.PrimaryScreen;
            var newSize = new Size(screen.Bounds.Width, screen.Bounds.Height);
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
                settings.IsZoomControlEnabled = false;
                settings.AreDefaultContextMenusEnabled = false;
                coreWebView2.ContextMenuRequested += (s, e) => e.Handled = true;

                var screen = Screen.PrimaryScreen;
                webView_Main.Size = new Size(screen.Bounds.Width, screen.Bounds.Height);
                webView_Main.Location = new Point(0, 0);
                webView_Main.ZoomFactor = 1.0;

                coreWebView2.NavigationCompleted += OnNavigationCompleted;

                Logger.Log("[INIT] Loading game URL (fullscreen video): " + GameUrl);
                webView_Main.Source = new Uri(GameUrl);
            }
            catch (Exception ex)
            {
                Logger.LogError("Error initializing WebView2: " + ex.Message, ex);
            }
        }

        private async void OnNavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            if (!e.IsSuccess || webView_Main?.CoreWebView2 == null) return;
            if (_pageLoadDone)
            {
                Logger.Log("[NAV] Subsequent navigation, skipping re-init");
                return;
            }
            _pageLoadDone = true;

            var coreWebView2 = webView_Main.CoreWebView2;
            try
            {
                _video.AttachRequestInterception(coreWebView2);
                Logger.Log("[INIT] Video request interception attached (same as reference project)");

                int w = Screen.PrimaryScreen.Bounds.Width;
                int h = Screen.PrimaryScreen.Bounds.Height;
                string fullscreenScript = @"
(function() {
    function makeFullscreen(v) {
        if (!v || v.dataset.fullscreenDone) return;
        v.style.setProperty('position', 'fixed', 'important');
        v.style.setProperty('top', '0', 'important');
        v.style.setProperty('left', '0', 'important');
        v.style.setProperty('width', '100%', 'important');
        v.style.setProperty('height', '100%', 'important');
        v.style.setProperty('object-fit', 'contain', 'important');
        v.style.setProperty('z-index', '99999', 'important');
        v.dataset.fullscreenDone = '1';
    }
    var style = document.createElement('style');
    style.id = 'fullscreen-video-override';
    style.textContent = 'video{position:fixed!important;top:0!important;left:0!important;width:100vw!important;height:100vh!important;object-fit:contain!important;z-index:99999!important;background:#000!important}html,body{background:#000!important;overflow:hidden!important}';
    if (!document.getElementById('fullscreen-video-override')) document.head.appendChild(style);
    document.querySelectorAll('video').forEach(makeFullscreen);
    var obs = new MutationObserver(function(mutations) {
        document.querySelectorAll('video').forEach(makeFullscreen);
    });
    obs.observe(document.body, { childList: true, subtree: true });
})();
";
                await coreWebView2.ExecuteScriptAsync(fullscreenScript);
                Logger.Log("[INIT] Fullscreen video CSS injected");
            }
            catch (Exception ex)
            {
                Logger.LogError("[INIT] OnNavigationCompleted: " + ex.Message, ex);
            }
        }
    }
}
