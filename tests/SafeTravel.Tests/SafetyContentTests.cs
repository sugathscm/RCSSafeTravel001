using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace SafeTravel.Tests;

/// <summary>Topic pages with do and avoid lists, first aid guides with videos, and the emergency numbers page.</summary>
public class SafetyContentTests(TestApp app) : IClassFixture<TestApp>
{
    private HttpClient Browser() => app.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });

    [Theory, InlineData("/topics"), InlineData("/first-aid"), InlineData("/emergency")]
    public async Task Safety_pages_open(string url)
    {
        var res = await Browser().GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.DoesNotContain("<script>", await res.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Topic_page_shows_the_do_and_avoid_lists()
    {
        var topics = await app.CreateClient().GetAsync("/api/v1/topics").ReadAsync();
        var beach = topics.EnumerateArray().Single(t => t.GetProperty("summary").GetString()!.StartsWith("Rip currents"));
        Assert.Equal(4, beach.GetProperty("dos").GetArrayLength());
        Assert.Equal(3, beach.GetProperty("donts").GetArrayLength());

        var html = await Browser().GetStringAsync($"/topics/{beach.GetProperty("id").GetInt32()}");
        Assert.Contains("Red means no swimming", html);
        Assert.Contains("Climbing wet rocks near waterfalls", html);
        Assert.Equal(HttpStatusCode.NotFound, (await Browser().GetAsync("/topics/999999")).StatusCode);
    }

    [Fact]
    public async Task First_aid_and_emergency_pages_show_the_seeded_content()
    {
        var firstAid = await Browser().GetStringAsync("/first-aid");
        Assert.Contains("Severe bleeding", firstAid);
        Assert.Contains("Push hard and fast in the centre of the chest", firstAid);

        var emergency = await Browser().GetStringAsync("/emergency");
        Assert.Contains("1990", emergency);
        Assert.Contains("What to tell the operator", emergency);
    }

    [Fact]
    public async Task Content_staff_edit_topic_lists_and_first_aid_guides()
    {
        var content = await app.SignedInAsync("content@safetravel.local");
        var anon = app.CreateClient();

        var topics = await content.GetAsync("/api/v1/admin/content/topics").ReadAsync();
        var id = topics[0].GetProperty("id").GetInt32();
        var saved = await content.PutAsJsonAsync($"/api/v1/admin/content/topics/{id}/guidance", new { dos = "Carry water\n\n  Tell someone your route  ", donts = "Walking alone at night" }).ReadAsync();
        Assert.Equal(2, saved.GetProperty("dos").GetArrayLength());
        var shown = await anon.GetAsync($"/api/v1/topics/{id}").ReadAsync();
        Assert.Equal("Tell someone your route", shown.GetProperty("dos")[1].GetString());
        Assert.Equal("Walking alone at night", shown.GetProperty("donts")[0].GetString());

        var guide = await content.PostAsJsonAsync("/api/v1/admin/first-aid", new { title = "Sprains", steps = "Rest the limb\nCool it with a cold pack", videoUrl = "https://vimeo.com/123456789" }).ReadAsync();
        var gid = guide.GetProperty("id").GetInt32();
        Assert.Equal("https://player.vimeo.com/video/123456789", guide.GetProperty("videoEmbedUrl").GetString());
        Assert.Equal(2, guide.GetProperty("steps").GetArrayLength());
        var listed = await anon.GetAsync("/api/v1/first-aid").ReadAsync();
        Assert.Contains("Sprains", listed.EnumerateArray().Select(g => g.GetProperty("title").GetString()));
        Assert.Contains("player.vimeo.com/video/123456789", await Browser().GetStringAsync($"/first-aid/{gid}"));

        var edited = await content.PutAsJsonAsync($"/api/v1/admin/first-aid/{gid}", new { title = "Sprains and strains", steps = "Rest the limb", videoUrl = "https://streamable.com/abc123" }).ReadAsync();
        Assert.Equal("https://streamable.com/e/abc123", edited.GetProperty("videoEmbedUrl").GetString());

        // Only Vimeo and Streamable links are accepted, and a guide needs at least one step.
        Assert.Equal(HttpStatusCode.BadRequest, (await content.PostAsJsonAsync("/api/v1/admin/first-aid", new { title = "Bad video", steps = "One step", videoUrl = "https://example.com/video.mp4" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await content.PostAsJsonAsync("/api/v1/admin/first-aid", new { title = "No steps", steps = "  \n " })).StatusCode);

        (await content.PutAsJsonAsync($"/api/v1/admin/first-aid/{gid}/active", new { on = false })).EnsureSuccessStatusCode();
        var afterHide = await anon.GetAsync("/api/v1/first-aid").ReadAsync();
        Assert.DoesNotContain("Sprains and strains", afterHide.EnumerateArray().Select(g => g.GetProperty("title").GetString()));
        Assert.Equal(HttpStatusCode.NotFound, (await Browser().GetAsync($"/first-aid/{gid}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await content.DeleteAsync($"/api/v1/admin/first-aid/{gid}")).StatusCode);
    }

    [Fact]
    public async Task Only_content_staff_can_change_safety_content()
    {
        var finance = await app.SignedInAsync("finance@safetravel.local");
        Assert.Equal(HttpStatusCode.Forbidden, (await finance.PostAsJsonAsync("/api/v1/admin/first-aid", new { title = "Not allowed", steps = "One step" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await finance.PutAsJsonAsync("/api/v1/admin/content/topics/1/guidance", new { dos = "x" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await app.CreateClient().GetAsync("/api/v1/admin/first-aid")).StatusCode);
    }
}
