using System.Net.Http.Headers;
using System.Text.Json;

public class SupabaseService
{
    private readonly HttpClient _http;
    private readonly string _url;
    private readonly string _key;
    public SupabaseService(IConfiguration cfg, IHttpClientFactory f)
    {
        _url = cfg["Supabase:Url"]!;
        _key = cfg["Supabase:AnonKey"]!;
        _http = f.CreateClient();
        _http.BaseAddress = new Uri(_url + "/rest/v1/");
        _http.DefaultRequestHeaders.Add("apikey", _key);
        _http.DefaultRequestHeaders.Add("Authorization", "Bearer " + _key);
    }
    public async Task<JsonElement> GetTable(string table) =>
        await _http.GetFromJsonAsync<JsonElement>(table + "?select=*");
}
