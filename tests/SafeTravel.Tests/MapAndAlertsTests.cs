using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace SafeTravel.Tests;

/// <summary>The safety map with its risk zones, and the alerts page with advice and map pins.</summary>
public class MapAndAlertsTests(TestApp app) : IClassFixture<TestApp>
{
    private HttpClient Browser() => app.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });

    [Theory, InlineData("/map"), InlineData("/alerts")]
    public async Task Map_and_alerts_pages_open(string url)
    {
        var res = await Browser().GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.DoesNotContain("<script>", await res.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Alerts_page_shows_the_advice_and_a_map_link()
    {
        var html = await Browser().GetStringAsync("/alerts");
        Assert.Contains("Heavy rain in the hills until Thursday evening", html);
        Assert.Contains("Avoid trails, waterfalls and road cuttings.", html);
        Assert.Contains("/map?lat=6.8667", html);
        Assert.DoesNotContain("A16 closed between Haputale and Beragala", html); // not active
    }

    [Fact]
    public async Task Sample_risk_zones_are_on_the_map()
    {
        var zones = await app.CreateClient().GetAsync("/api/v1/risk-zones").ReadAsync();
        Assert.True(zones.GetArrayLength() >= 4);
        Assert.Contains("Mirissa beach", zones.EnumerateArray().Select(z => z.GetProperty("name").GetString()));
        Assert.Contains("flood", zones.EnumerateArray().Select(z => z.GetProperty("type").GetString()));
        Assert.Contains("Flood zones", await Browser().GetStringAsync("/map"));
    }

    [Fact]
    public async Task Alerts_staff_manage_zones_and_pin_alerts()
    {
        var staff = await app.SignedInAsync("alerts@safetravel.local");
        var anon = app.CreateClient();

        var zone = await staff.PostAsJsonAsync("/api/v1/admin/risk-zones", new { type = "roadclosure", name = "A4 at Haputale", details = "Closed after a rockfall.", source = "Police", latitude = 6.7667, longitude = 80.9667, radiusMeters = 0 }).ReadAsync();
        var id = zone.GetProperty("id").GetInt32();
        Assert.Equal("roadclosure", zone.GetProperty("type").GetString());
        var shown = await anon.GetAsync("/api/v1/risk-zones").ReadAsync();
        Assert.Contains("A4 at Haputale", shown.EnumerateArray().Select(z => z.GetProperty("name").GetString()));

        var edited = await staff.PutAsJsonAsync($"/api/v1/admin/risk-zones/{id}", new { type = "roadclosure", name = "A4 at Haputale", details = "One lane open.", latitude = 6.7667, longitude = 80.9667, radiusMeters = 200 }).ReadAsync();
        Assert.Equal(200, edited.GetProperty("radiusMeters").GetInt32());
        Assert.Equal("One lane open.", edited.GetProperty("details").GetString());

        // A zone must be inside Sri Lanka and of a known type.
        Assert.Equal(HttpStatusCode.BadRequest, (await staff.PostAsJsonAsync("/api/v1/admin/risk-zones", new { type = "flood", name = "Thames flood", latitude = 51.5, longitude = -0.12, radiusMeters = 100 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await staff.PostAsJsonAsync("/api/v1/admin/risk-zones", new { type = "volcano", name = "No such type", latitude = 7.0, longitude = 80.0, radiusMeters = 0 })).StatusCode);

        (await staff.PutAsJsonAsync($"/api/v1/admin/risk-zones/{id}/active", new { on = false })).EnsureSuccessStatusCode();
        var afterHide = await anon.GetAsync("/api/v1/risk-zones").ReadAsync();
        Assert.DoesNotContain("A4 at Haputale", afterHide.EnumerateArray().Select(z => z.GetProperty("name").GetString()));
        Assert.Equal(HttpStatusCode.NoContent, (await staff.DeleteAsync($"/api/v1/admin/risk-zones/{id}")).StatusCode);

        var alert = await staff.PostAsJsonAsync("/api/v1/admin/alerts", new { level = "red", type = "Storm warning", area = "Galle", title = "Strong winds along the coast tonight", details = "Stay away from the sea wall.", latitude = 6.0535, longitude = 80.221 }).ReadAsync();
        Assert.Equal("Stay away from the sea wall.", alert.GetProperty("details").GetString());
        var live = await anon.GetAsync("/api/v1/alerts").ReadAsync();
        var mine = live.EnumerateArray().Single(a => a.GetProperty("title").GetString() == "Strong winds along the coast tonight");
        Assert.Equal(6.0535, mine.GetProperty("latitude").GetDouble(), 4);
        Assert.Contains("Stay away from the sea wall.", await Browser().GetStringAsync("/alerts"));

        // A pin needs both coordinates.
        Assert.Equal(HttpStatusCode.BadRequest, (await staff.PostAsJsonAsync("/api/v1/admin/alerts", new { level = "blue", type = "Health notice", area = "Kandy", title = "Half a pin is refused", latitude = 7.29 })).StatusCode);
    }

    [Fact]
    public async Task Only_alerts_staff_can_change_the_map()
    {
        var finance = await app.SignedInAsync("finance@safetravel.local");
        Assert.Equal(HttpStatusCode.Forbidden, (await finance.PostAsJsonAsync("/api/v1/admin/risk-zones", new { type = "flood", name = "Not allowed", latitude = 7.0, longitude = 80.0, radiusMeters = 0 })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await app.CreateClient().GetAsync("/api/v1/admin/risk-zones")).StatusCode);
    }
}
