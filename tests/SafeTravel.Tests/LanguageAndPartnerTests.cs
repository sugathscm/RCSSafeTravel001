using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace SafeTravel.Tests;

/// <summary>The language selector (English, Sinhala, Tamil) and the enquiry form for hotels and tour operators.</summary>
public class LanguageAndPartnerTests(TestApp app) : IClassFixture<TestApp>
{
    private HttpClient Browser() =>
        app.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = true, HandleCookies = true });

    private static async Task<string> Text(HttpClient c, string url) => WebUtility.HtmlDecode(await c.GetStringAsync(url));

    [Fact]
    public async Task Pages_are_in_English_until_a_language_is_chosen()
    {
        var html = await Text(Browser(), "/");
        Assert.Contains("lang=\"en\"", html);
        Assert.Contains("Find help near you", html);
        Assert.Contains("සිංහල", html); // the selector offers Sinhala
        Assert.Contains("தமிழ்", html);
    }

    [Fact]
    public async Task Chosen_language_is_remembered_and_shown_on_every_public_page()
    {
        var c = Browser();
        var res = await c.GetAsync("/language/si?returnUrl=%2Ftopics");
        Assert.Equal("/topics", res.RequestMessage!.RequestUri!.AbsolutePath);
        var topics = WebUtility.HtmlDecode(await res.Content.ReadAsStringAsync());
        Assert.Contains("lang=\"si\"", topics);
        Assert.Contains("ආරක්ෂක මාතෘකා", topics); // Safety topics

        var home = await Text(c, "/");
        Assert.Contains("මුල් පිටුව", home); // Home
        Assert.Contains("ඔබ අසල උදව් සොයන්න", home); // Find help near you
        Assert.DoesNotContain("Find help near you", home);
        Assert.Contains("1990", home); // numbers and staff-entered content are unchanged

        await c.GetAsync("/language/ta");
        var tamil = await Text(c, "/emergency");
        Assert.Contains("lang=\"ta\"", tamil);
        Assert.Contains("அவசர தொலைபேசி எண்கள்", tamil); // Emergency numbers

        // An unknown language is ignored, and the return address must be on this site.
        await c.GetAsync("/language/xx");
        Assert.Contains("lang=\"ta\"", await Text(c, "/"));
        var away = await c.GetAsync("/language/en?returnUrl=https%3A%2F%2Fexample.com%2F");
        Assert.Equal("localhost", away.RequestMessage!.RequestUri!.Host);
        Assert.Contains("Find help near you", await Text(c, "/"));
    }

    [Fact]
    public async Task Hotel_sends_an_enquiry_and_staff_handle_it()
    {
        Assert.Equal(HttpStatusCode.OK, (await Browser().GetAsync("/partners")).StatusCode);
        var anon = app.CreateClient();
        var sent = await anon.PostAsJsonAsync("/api/v1/partner-enquiries", new { businessName = "Hill View Hotel", businessType = "hotel", contactName = "Dinesh Silva", email = "dinesh@hillview.example", guestsPerMonth = 400, message = "We have 40 rooms in Ella." });
        Assert.Equal(HttpStatusCode.NoContent, sent.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await anon.PostAsJsonAsync("/api/v1/partner-enquiries", new { businessName = "No email", businessType = "hotel", contactName = "Nobody", email = "not-an-email" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await anon.PostAsJsonAsync("/api/v1/partner-enquiries", new { businessName = "Wrong type", businessType = "airline", contactName = "Somebody", email = "a@b.example" })).StatusCode);

        var manager = await app.SignedInAsync("suppliers@safetravel.local");
        var list = await manager.GetAsync("/api/v1/admin/partner-enquiries").ReadAsync();
        var mine = list.EnumerateArray().Single(p => p.GetProperty("businessName").GetString() == "Hill View Hotel");
        Assert.False(mine.GetProperty("isHandled").GetBoolean());
        Assert.Equal(400, mine.GetProperty("guestsPerMonth").GetInt32());

        var id = mine.GetProperty("id").GetInt32();
        Assert.Equal(HttpStatusCode.NoContent, (await manager.PutAsJsonAsync($"/api/v1/admin/partner-enquiries/{id}/handled", new { on = true })).StatusCode);
        var after = await manager.GetAsync("/api/v1/admin/partner-enquiries").ReadAsync();
        Assert.True(after.EnumerateArray().Single(p => p.GetProperty("id").GetInt32() == id).GetProperty("isHandled").GetBoolean());

        // Finance staff may read enquiries but not change them; content staff and the public may not read them.
        var finance = await app.SignedInAsync("finance@safetravel.local");
        Assert.Equal(HttpStatusCode.OK, (await finance.GetAsync("/api/v1/admin/partner-enquiries")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await finance.PutAsJsonAsync($"/api/v1/admin/partner-enquiries/{id}/handled", new { on = false })).StatusCode);
        var content = await app.SignedInAsync("content@safetravel.local");
        Assert.Equal(HttpStatusCode.Forbidden, (await content.GetAsync("/api/v1/admin/partner-enquiries")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/v1/admin/partner-enquiries")).StatusCode);
    }
}
