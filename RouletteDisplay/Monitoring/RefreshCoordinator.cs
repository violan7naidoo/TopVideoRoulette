using System;

namespace RouletteDisplay.Monitoring
{
    internal sealed class RefreshCoordinator : IRefreshCoordinator
    {
        private readonly TimeSpan _cooldown;
        private readonly object _lock = new object();
        private DateTime _lastRefreshUtc = DateTime.MinValue;

        public RefreshCoordinator(TimeSpan cooldown)
        {
            _cooldown = cooldown <= TimeSpan.Zero ? TimeSpan.FromMinutes(5) : cooldown;
        }

        public bool TryCreateRequest(string reason, out RefreshRequest? request)
        {
            lock (_lock)
            {
                DateTime nowUtc = DateTime.UtcNow;
                TimeSpan elapsed = nowUtc - _lastRefreshUtc;
                if (elapsed < _cooldown)
                {
                    request = null;
                    return false;
                }

                _lastRefreshUtc = nowUtc;
                request = new RefreshRequest(reason, nowUtc);
                return true;
            }
        }
    }
}
