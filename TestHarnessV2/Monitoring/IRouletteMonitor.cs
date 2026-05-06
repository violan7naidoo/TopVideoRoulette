using System;
using System.Threading.Tasks;

namespace TestHarnessV2.Monitoring
{
    internal interface IRouletteMonitor : IAsyncDisposable
    {
        event EventHandler<RefreshRequest>? RefreshRequested;

        void Start();
        Task StopAsync();
    }
}
