namespace RentLog.Services;

public sealed class SupabaseClientProvider
{
    private readonly string _url;
    private readonly string _anonKey;
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private Supabase.Client? _client;

    public SupabaseClientProvider(string url, string anonKey)
    {
        _url = url;
        _anonKey = anonKey;
    }

    public async Task<Supabase.Client> GetClientAsync()
    {
        if (_client is not null)
        {
            return _client;
        }

        await _initializationLock.WaitAsync();
        try
        {
            if (_client is null)
            {
                var client = new Supabase.Client(_url, _anonKey, new Supabase.SupabaseOptions
                {
                    AutoRefreshToken = true,
                    AutoConnectRealtime = false
                });
                await client.InitializeAsync();
                _client = client;
            }
        }
        finally
        {
            _initializationLock.Release();
        }

        return _client;
    }
}
