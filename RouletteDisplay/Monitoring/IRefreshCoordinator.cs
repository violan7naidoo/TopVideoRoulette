namespace RouletteDisplay.Monitoring
{
    internal interface IRefreshCoordinator
    {
        bool TryCreateRequest(string reason, out RefreshRequest? request);
    }
}
