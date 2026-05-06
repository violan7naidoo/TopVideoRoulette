namespace TestHarnessV2.Monitoring
{
    internal interface IRefreshCoordinator
    {
        bool TryCreateRequest(string reason, out RefreshRequest? request);
    }
}
