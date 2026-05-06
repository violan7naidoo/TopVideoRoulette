using TestHarnessV2.Monitoring;
using TestHarnessV2.WebSocket;

namespace TestHarnessV2
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            ApplicationConfiguration.Initialize();

            var settings = AppConfig.Current;
            var refreshCoordinator = new RefreshCoordinator(TimeSpan.FromSeconds(Math.Max(1, settings.RefreshCooldownSeconds)));
            var webSocketClient = new ClientWebSocketAdapter();
            var rouletteMonitor = new RouletteSocketMonitor(webSocketClient, refreshCoordinator, settings);

            Application.Run(new GameUI2(rouletteMonitor));
        }
    }
}