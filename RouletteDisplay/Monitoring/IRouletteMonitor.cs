using System;
using System.Threading.Tasks;

namespace RouletteDisplay.Monitoring
{
    internal interface IRouletteMonitor : IAsyncDisposable
    {
        event EventHandler<RefreshRequest>? RefreshRequested;

        void Start();
        Task StopAsync();
    }
}
