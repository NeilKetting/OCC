namespace OCC.WpfClient.Services.Results
{
    public sealed record DatabaseStatusResult(
        bool IsConnected,
        string StatusText,
        string DatabaseName);
}
