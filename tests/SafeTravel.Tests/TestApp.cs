using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;

namespace SafeTravel.Tests;

/// <summary>Runs the real application against a private in-memory database with the sample data.</summary>
public class TestApp : WebApplicationFactory<Program>
{
    public const string Password = "Test-Password-2026";
    private readonly string _cs = $"Data Source=file:st{Guid.NewGuid():N}?mode=memory&cache=shared";
    private SqliteConnection? _keepAlive;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        _keepAlive = new SqliteConnection(_cs);
        _keepAlive.Open();
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Database:Provider"] = "Sqlite", ["ConnectionStrings:Default"] = _cs,
            ["Seed:StaffPassword"] = Password, ["Seed:SampleData"] = "true", ["Renewals:Enabled"] = "false",
            ["RateLimits:ApiPerMinute"] = "100000", ["RateLimits:AuthPerMinute"] = "100000", ["RateLimits:PublicWritesPerMinute"] = "100000",
            ["Uploads:Path"] = Path.Combine(Path.GetTempPath(), "st-uploads-" + Guid.NewGuid().ToString("N"))
        }));
    }

    protected override void Dispose(bool disposing) { base.Dispose(disposing); _keepAlive?.Dispose(); }

    /// <summary>A client signed in the way the mobile apps sign in: with a bearer token.</summary>
    public async Task<HttpClient> SignedInAsync(string email, string password = Password)
    {
        var client = CreateClient();
        var res = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
        res.EnsureSuccessStatusCode();
        var token = (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    public Task<HttpClient> AdminAsync() => SignedInAsync("admin@safetravel.local");
}

public static class Json
{
    public static async Task<JsonElement> ReadAsync(this Task<HttpResponseMessage> call)
    {
        var res = await call;
        var body = await res.Content.ReadAsStringAsync();
        Assert.True(res.IsSuccessStatusCode, $"{(int)res.StatusCode}: {body}");
        return JsonSerializer.Deserialize<JsonElement>(body);
    }

    public static IEnumerable<string> Names(this JsonElement page) => page.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("name").GetString()!);
}
