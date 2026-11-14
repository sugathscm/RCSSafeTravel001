using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace SafeTravel.Tests;

/// <summary>Traveller accounts: sign-up, emergency contacts, trip plan, and reviews that staff check before they are shown.</summary>
public class TravellerTests(TestApp app) : IClassFixture<TestApp>
{
    private HttpClient Browser(bool follow = true) =>
        app.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = follow, HandleCookies = true });

    /// <summary>A new traveller, signed in the way the mobile apps sign in.</summary>
    private async Task<HttpClient> NewTravellerAsync(string email, string fullName)
    {
        var http = app.CreateClient();
        var reg = await http.PostAsJsonAsync("/api/v1/auth/register", new { email, password = "Sunny-Beach-2026", fullName }).ReadAsync();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", reg.GetProperty("accessToken").GetString());
        return http;
    }

    [Fact]
    public async Task Join_page_opens_and_the_account_page_needs_sign_in()
    {
        Assert.Equal(HttpStatusCode.OK, (await Browser().GetAsync("/join")).StatusCode);
        var res = await Browser(follow: false).GetAsync("/account");
        Assert.Equal(HttpStatusCode.Redirect, res.StatusCode);
        Assert.StartsWith("/account/login", res.Headers.Location!.PathAndQuery);
    }

    [Fact]
    public async Task Sample_traveller_signs_in_on_the_web_and_sees_the_account_page()
    {
        var c = Browser();
        var page = await c.GetStringAsync("/account/login");
        var token = WebUtility.HtmlDecode(Regex.Match(page, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
        var res = await c.PostAsync("/account/login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = "emma@traveller.example", ["Password"] = TestApp.Password, ["__RequestVerificationToken"] = token
        }));
        Assert.Equal("/account", res.RequestMessage!.RequestUri!.AbsolutePath);
        var html = await res.Content.ReadAsStringAsync();
        Assert.Contains("Emma Wilson", html);
        Assert.Contains("Sarah Wilson", html);
        Assert.Contains("Trip plan", html);
        Assert.DoesNotContain("<script>", html);
    }

    [Fact]
    public async Task Traveller_keeps_contacts_and_a_trip_plan_that_nobody_else_can_touch()
    {
        var me = await NewTravellerAsync("asha@example.com", "Asha Perera");
        var contact = await me.PostAsJsonAsync("/api/v1/me/contacts", new { name = "Ravi Perera", phone = "+94 77 123 4567", relationship = "Brother" }).ReadAsync();
        var contactId = contact.GetProperty("id").GetInt32();
        Assert.Equal(HttpStatusCode.BadRequest, (await me.PostAsJsonAsync("/api/v1/me/contacts", new { name = "Bad phone", phone = "call me" })).StatusCode);

        (await me.PostAsJsonAsync("/api/v1/me/trip", new { place = "Ella", date = "2027-03-20", note = "Hiking" })).EnsureSuccessStatusCode();
        (await me.PostAsJsonAsync("/api/v1/me/trip", new { place = "Kandy", date = "2027-03-18" })).EnsureSuccessStatusCode();

        var account = await me.GetAsync("/api/v1/me").ReadAsync();
        Assert.Equal("Asha Perera", account.GetProperty("fullName").GetString());
        Assert.Equal(1, account.GetProperty("contacts").GetArrayLength());
        Assert.Equal("Kandy", account.GetProperty("trip")[0].GetProperty("place").GetString()); // date order
        Assert.False(account.GetProperty("checkInEnabled").GetBoolean());

        var other = await NewTravellerAsync("someone.else@example.com", "Someone Else");
        Assert.Equal(0, (await other.GetAsync("/api/v1/me").ReadAsync()).GetProperty("contacts").GetArrayLength());
        Assert.Equal(HttpStatusCode.NotFound, (await other.DeleteAsync($"/api/v1/me/contacts/{contactId}")).StatusCode);

        // "I'm safe" messages stay off until an SMS service is connected.
        Assert.Equal(HttpStatusCode.BadRequest, (await me.PostAsync("/api/v1/me/check-in", null)).StatusCode);

        // No more than five contacts.
        for (var i = 2; i <= 5; i++)
            (await me.PostAsJsonAsync("/api/v1/me/contacts", new { name = $"Contact {i}", phone = $"+94 77 000 000{i}" })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.BadRequest, (await me.PostAsJsonAsync("/api/v1/me/contacts", new { name = "One too many", phone = "+94 77 000 0006" })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await me.DeleteAsync($"/api/v1/me/contacts/{contactId}")).StatusCode);
    }

    [Fact]
    public async Task Review_is_shown_and_counted_only_after_staff_approve_it()
    {
        var me = await NewTravellerAsync("nimali@example.com", "Nimali Fernando");
        var anon = app.CreateClient();
        var found = await anon.GetAsync("/api/v1/listings?q=Mirissa Catch Kitchen").ReadAsync();
        var listing = found.GetProperty("items")[0];
        var id = listing.GetProperty("id").GetInt32();
        var before = listing.GetProperty("reviews").GetInt32();

        var review = await me.PostAsJsonAsync("/api/v1/me/reviews", new { listingId = id, stars = 5, text = "Fresh fish and friendly staff." }).ReadAsync();
        var reviewId = review.GetProperty("id").GetInt32();
        Assert.Equal("pending", review.GetProperty("status").GetString());
        Assert.Equal("Nimali F.", review.GetProperty("authorName").GetString());
        Assert.Equal(0, (await anon.GetAsync($"/api/v1/listings/{id}/reviews").ReadAsync()).GetArrayLength());
        Assert.DoesNotContain("Fresh fish and friendly staff.", await Browser().GetStringAsync($"/listing/{id}"));

        var content = await app.SignedInAsync("content@safetravel.local");
        var waiting = await content.GetAsync("/api/v1/admin/reviews?status=pending").ReadAsync();
        Assert.Contains(reviewId, waiting.EnumerateArray().Select(r => r.GetProperty("id").GetInt32()));
        var approved = await content.PostAsync($"/api/v1/admin/reviews/{reviewId}/approve", null).ReadAsync();
        Assert.Equal("approved", approved.GetProperty("status").GetString());

        var shown = await anon.GetAsync($"/api/v1/listings/{id}/reviews").ReadAsync();
        Assert.Equal("Fresh fish and friendly staff.", shown.EnumerateArray().Single().GetProperty("text").GetString());
        Assert.Equal(before + 1, (await anon.GetAsync($"/api/v1/listings/{id}").ReadAsync()).GetProperty("reviews").GetInt32());
        Assert.Contains("Fresh fish and friendly staff.", await Browser().GetStringAsync($"/listing/{id}"));

        // One review per traveller per business, and a review is decided once.
        Assert.Equal(HttpStatusCode.BadRequest, (await me.PostAsJsonAsync("/api/v1/me/reviews", new { listingId = id, stars = 1, text = "Second try" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await content.PostAsync($"/api/v1/admin/reviews/{reviewId}/reject", null)).StatusCode);

        // Help points are not reviewed, and stars are 1 to 5.
        var helpId = (await anon.GetAsync("/api/v1/listings?kind=help").ReadAsync()).GetProperty("items")[0].GetProperty("id").GetInt32();
        Assert.Equal(HttpStatusCode.BadRequest, (await me.PostAsJsonAsync("/api/v1/me/reviews", new { listingId = helpId, stars = 5 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await me.PostAsJsonAsync("/api/v1/me/reviews", new { listingId = id, stars = 9 })).StatusCode);

        // Deleting an approved review takes it back out of the rating.
        Assert.Equal(HttpStatusCode.NoContent, (await content.DeleteAsync($"/api/v1/admin/reviews/{reviewId}")).StatusCode);
        Assert.Equal(before, (await anon.GetAsync($"/api/v1/listings/{id}").ReadAsync()).GetProperty("reviews").GetInt32());
    }

    [Fact]
    public async Task Only_travellers_use_the_account_api_and_only_content_staff_decide_reviews()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await app.CreateClient().GetAsync("/api/v1/me")).StatusCode);
        var supplier = await app.SignedInAsync("owner@ellaridge.example");
        Assert.Equal(HttpStatusCode.Forbidden, (await supplier.GetAsync("/api/v1/me")).StatusCode);

        var emma = await app.SignedInAsync("emma@traveller.example");
        var account = await emma.GetAsync("/api/v1/me").ReadAsync();
        Assert.Equal(2, account.GetProperty("contacts").GetArrayLength());
        Assert.Equal(3, account.GetProperty("trip").GetArrayLength());
        Assert.Equal(HttpStatusCode.Forbidden, (await emma.GetAsync("/api/v1/admin/reviews")).StatusCode);

        var finance = await app.SignedInAsync("finance@safetravel.local");
        Assert.Equal(HttpStatusCode.Forbidden, (await finance.PostAsync("/api/v1/admin/reviews/1/approve", null)).StatusCode);
    }
}
