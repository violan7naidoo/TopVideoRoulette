using System;

namespace TestHarnessV2.Monitoring
{
    internal sealed class RefreshRequest
    {
        public RefreshRequest(string reason, DateTime requestedAtUtc)
        {
            Reason = reason;
            RequestedAtUtc = requestedAtUtc;
        }

        public string Reason { get; }
        public DateTime RequestedAtUtc { get; }
    }
}
