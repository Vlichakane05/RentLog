namespace RentLog.Services;

public sealed class BackendMode(bool usesMockData, string? setupMessage)
{
    public bool UsesMockData { get; } = usesMockData;
    public string? SetupMessage { get; } = setupMessage;
}
