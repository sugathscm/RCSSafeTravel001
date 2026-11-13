using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace SafeTravel.Tests;

/// <summary>The browser portals: pages, cookie sign-in and the anti-forgery rule.</summary>
public class PageTests(TestApp app) : IClassFixture<TestApp>
{
    private HttpClient Browser(bool follow = true) =>
        app.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = follow, HandleCookies = true });

    private static string Token(string html, string pattern) => WebUtility.HtmlDecode(Regex.Match(html, pattern).Groups[1].Value);

    private static async Task<string> SignInAsync(HttpClient c, string email)
    {
        var page = await c.GetStringAsync("/account/login");
        var res = await c.PostAsync("/account/login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = email, ["Password"] = TestApp.Password,
            ["__RequestVerificationToken"] = Token(page, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"")
        }));
        return res.RequestMessage!.RequestUri!.AbsolutePath;
    }

    [Theory, InlineData("/"), InlineData("/find"), InlineData("/donate"), InlineData("/register"), InlineData("/account/login")]
    public async Task Public_pages_open_with_security_headers(string url)
    {
        var res = await Browser().GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Contains("script-src 'self'", res.Headers.GetValues("Content-Security-Policy").Single());
        Assert.DoesNotContain("<script>", await res.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Home_page_shows_what_the_admin_set()
    {
        var html = await Browser().GetStringAsync("/");
        Assert.Contains("Emergency in Sri Lanka", html);
        Assert.Contains("1990", html);
        Assert.Contains("Uber", html);
    }

    [Theory, InlineData("/admin"), InlineData("/supplier")]
    public async Task Portals_send_visitors_to_sign_in(string url)
    {
        var res = await Browser(follow: false).GetAsync(url);
        Assert.Equal(HttpStatusCode.Redirect, res.StatusCode);
        Assert.StartsWith("/account/login", res.Headers.Location!.PathAndQuery);
    }

    [Fact]
    public async Task Staff_land_in_the_admin_portal_and_suppliers_in_theirs()
    {
        Assert.Equal("/admin", await SignInAsync(Browser(), "admin@safetravel.local"));
        var supplier = Browser();
        Assert.Equal("/supplier", await SignInAsync(supplier, "owner@ellaridge.example"));
        Assert.Equal("/account/denied", (await supplier.GetAsync("/admin")).RequestMessage!.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task Wrong_password_stays_on_the_sign_in_page()
    {
        var c = Browser();
        var page = await c.GetStringAsync("/account/login");
        var res = await c.PostAsync("/account/login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = "admin@safetravel.local", ["Password"] = "not-the-password",
            ["__RequestVerificationToken"] = Token(page, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"")
        }));
        Assert.Contains("The email or password is not right.", await res.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Browser_changes_need_the_anti_forgery_token()
    {
        var c = Browser();
        await SignInAsync(c, "admin@safetravel.local");
        var body = new { level = "amber", type = "Storm warning", area = "Kandy", title = "Strong winds expected tonight" };

        var without = await c.PostAsJsonAsync("/api/v1/admin/alerts", body);
        Assert.Equal(HttpStatusCode.BadRequest, without.StatusCode);

        var token = Token(await c.GetStringAsync("/admin"), "name=\"csrf-token\" content=\"([^\"]+)\"");
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/admin/alerts") { Content = JsonContent.Create(body) };
        req.Headers.Add("X-CSRF-TOKEN", token);
        Assert.True((await c.SendAsync(req)).IsSuccessStatusCode);
    }
}

public class AdvertisementTests(TestApp app) : IClassFixture<TestApp>
{
    [Fact]
    public async Task Portal_and_apps_get_the_sponsor_advertisements()
    {
        var ads = await app.CreateClient().GetFromJsonAsync<System.Text.Json.JsonElement>("/api/v1/ads");
        Assert.Equal(2, ads.GetArrayLength());
        Assert.Equal("Uber", ads[0].GetProperty("sponsor").GetString());
        Assert.Contains("Sponsored", await app.CreateClient().GetStringAsync("/"));
    }

    [Fact]
    public async Task Staff_fill_the_third_place_and_can_switch_it_off()
    {
        var admin = await app.AdminAsync();
        var sponsor = await admin.PostAsJsonAsync("/api/v1/admin/sponsors", new { name = "Third Sponsor", role = "Insurance partner" }).ReadAsync();
        var body = new { sponsorId = sponsor.GetProperty("id").GetInt32(), headline = "Travel cover in minutes", text = "Sample", linkLabel = "Get cover", linkUrl = "https://example.com/cover", isActive = true };

        Assert.Equal(HttpStatusCode.NotFound, (await admin.PutAsJsonAsync("/api/v1/admin/ads/4", body)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync("/api/v1/admin/ads/3", body with { linkUrl = "javascript:alert(1)" })).StatusCode);

        var ad = await admin.PutAsJsonAsync("/api/v1/admin/ads/3", body).ReadAsync();
        var open = app.CreateClient();
        Assert.Equal(3, (await open.GetFromJsonAsync<System.Text.Json.JsonElement>("/api/v1/ads")).GetArrayLength());

        (await open.PostAsync($"/api/v1/ads/{ad.GetProperty("id").GetInt32()}/click", null)).EnsureSuccessStatusCode();
        var all = await admin.GetFromJsonAsync<System.Text.Json.JsonElement>("/api/v1/admin/ads");
        Assert.Equal(1, all.EnumerateArray().Single(a => a.GetProperty("slot").GetInt32() == 3).GetProperty("clicks").GetInt32());

        (await admin.PutAsJsonAsync("/api/v1/admin/ads/3", body with { isActive = false })).EnsureSuccessStatusCode();
        Assert.Equal(2, (await open.GetFromJsonAsync<System.Text.Json.JsonElement>("/api/v1/ads")).GetArrayLength());
    }

    [Fact]
    public async Task Advertisement_picture_must_be_a_real_image_and_only_content_staff_may_change_it()
    {
        var admin = await app.AdminAsync();
        MultipartFormDataContent File(byte[] bytes, string name) => new() { { new ByteArrayContent(bytes), "file", name } };

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsync("/api/v1/admin/ads/1/image", File("not a picture"u8.ToArray(), "ad.png"))).StatusCode);
        var ad = await admin.PostAsync("/api/v1/admin/ads/1/image", File([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0], "ad.png")).ReadAsync();
        var image = await app.CreateClient().GetAsync(ad.GetProperty("imageUrl").GetString());
        Assert.Equal("image/png", image.Content.Headers.ContentType!.MediaType);

        var finance = await app.SignedInAsync("finance@safetravel.local");
        Assert.Equal(HttpStatusCode.Forbidden, (await finance.DeleteAsync("/api/v1/admin/ads/1/image")).StatusCode);
        (await admin.DeleteAsync("/api/v1/admin/ads/1/image")).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NotFound, (await app.CreateClient().GetAsync(ad.GetProperty("imageUrl").GetString())).StatusCode);
    }
}

public class SupplierListingTests(TestApp app) : IClassFixture<TestApp>
{
    private static object Listing(string name, int categoryId) => new { name, categoryId, town = "Ella", district = "Badulla", phone = "077 555 0999", hours = "Daily", description = "Sample" };

    private async Task<int> CategoryAsync(string slug) =>
        (await app.CreateClient().GetFromJsonAsync<System.Text.Json.JsonElement>("/api/v1/categories")).EnumerateArray().Single(c => c.GetProperty("slug").GetString() == slug).GetProperty("id").GetInt32();

    [Fact]
    public async Task Paid_supplier_adds_a_restaurant_and_it_is_on_the_portal_at_once()
    {
        var owner = await app.SignedInAsync("owner@ellaridge.example");
        var me = await owner.PostAsJsonAsync("/api/v1/supplier/me/listings", Listing("Ella Ridge Restaurant", await CategoryAsync("food"))).ReadAsync();
        var added = me.GetProperty("listings").EnumerateArray().Single(l => l.GetProperty("name").GetString() == "Ella Ridge Restaurant");
        Assert.Equal("published", added.GetProperty("status").GetString());
        Assert.True(added.GetProperty("isVerified").GetBoolean());
        Assert.Equal("Ella Ridge Homestay", me.GetProperty("businessName").GetString());

        var found = await app.CreateClient().GetAsync("/api/v1/listings?q=Ella Ridge Restaurant").ReadAsync();
        Assert.Contains("Ella Ridge Restaurant", found.Names());

        var id = added.GetProperty("id").GetInt32();
        (await owner.DeleteAsync($"/api/v1/supplier/me/listings/{id}")).EnsureSuccessStatusCode();
        var main = me.GetProperty("listing").GetProperty("id").GetInt32();
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.DeleteAsync($"/api/v1/supplier/me/listings/{main}")).StatusCode);
    }

    [Fact]
    public async Task Listings_of_a_supplier_in_review_wait_and_others_cannot_touch_them()
    {
        var pending = await app.SignedInAsync("dilan@weligamasurf.example");
        var me = await pending.PostAsJsonAsync("/api/v1/supplier/me/listings", Listing("Weligama Surf Rooms", await CategoryAsync("stay"))).ReadAsync();
        var added = me.GetProperty("listings").EnumerateArray().Last();
        Assert.Equal("pending", added.GetProperty("status").GetString());
        Assert.DoesNotContain("Weligama Surf Rooms", (await app.CreateClient().GetAsync("/api/v1/listings?q=Weligama Surf Rooms").ReadAsync()).Names());

        var other = await app.SignedInAsync("owner@ellaridge.example");
        var id = added.GetProperty("id").GetInt32();
        Assert.Equal(HttpStatusCode.NotFound, (await other.PutAsJsonAsync($"/api/v1/supplier/me/listings/{id}/promotion", new { title = "Not mine" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.DeleteAsync($"/api/v1/supplier/me/listings/{id}")).StatusCode);
    }

    [Fact]
    public async Task Promotion_shows_on_the_tile_until_it_ends_and_staff_can_remove_it()
    {
        var owner = await app.SignedInAsync("hello@ninearch.example");
        var id = (await owner.GetAsync("/api/v1/supplier/me").ReadAsync()).GetProperty("supplier").GetProperty("listing").GetProperty("id").GetInt32();
        var open = app.CreateClient();

        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PutAsJsonAsync($"/api/v1/supplier/me/listings/{id}/promotion", new { title = "Old offer", until = "2020-01-01" })).StatusCode);
        (await owner.PutAsJsonAsync($"/api/v1/supplier/me/listings/{id}/promotion", new { title = "2 for 1 sunrise walk", text = "Weekdays only", until = "2099-12-31" })).EnsureSuccessStatusCode();

        var listing = await open.GetAsync($"/api/v1/listings/{id}").ReadAsync();
        Assert.Equal("2 for 1 sunrise walk", listing.GetProperty("promotion").GetProperty("title").GetString());
        Assert.Equal(["Nine Arch Walks"], (await open.GetAsync("/api/v1/listings?promo=true").ReadAsync()).Names());
        Assert.Contains("promo-badge", await open.GetStringAsync("/"));

        var admin = await app.AdminAsync();
        (await admin.DeleteAsync($"/api/v1/admin/listings/{id}/promotion")).EnsureSuccessStatusCode();
        Assert.Equal(System.Text.Json.JsonValueKind.Null, (await open.GetAsync($"/api/v1/listings/{id}").ReadAsync()).GetProperty("promotion").ValueKind);
        Assert.Empty((await open.GetAsync("/api/v1/listings?promo=true").ReadAsync()).Names());
    }
}

public class ProductTests(TestApp app) : IClassFixture<TestApp>
{
    [Fact]
    public async Task Supplier_lists_products_with_prices_and_travellers_see_the_shown_ones()
    {
        var owner = await app.SignedInAsync("owner@ellaridge.example");
        var listing = (await owner.GetAsync("/api/v1/supplier/me").ReadAsync()).GetProperty("supplier").GetProperty("listing").GetProperty("id").GetInt32();

        var room = await owner.PostAsJsonAsync($"/api/v1/supplier/me/listings/{listing}/products", new { name = "Deluxe double room", description = "Mountain view", price = 18500, unit = "per night", isActive = true }).ReadAsync();
        await owner.PostAsJsonAsync($"/api/v1/supplier/me/listings/{listing}/products", new { name = "Hidden room", isActive = false }).ReadAsync();
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsJsonAsync($"/api/v1/supplier/me/listings/{listing}/products", new { name = "X", price = -5 })).StatusCode);

        var open = app.CreateClient();
        var shown = await open.GetAsync($"/api/v1/listings/{listing}/products").ReadAsync();
        Assert.Equal(1, shown.GetArrayLength());
        Assert.Equal(18500, shown[0].GetProperty("price").GetDecimal());
        Assert.Equal(2, (await owner.GetAsync($"/api/v1/supplier/me/listings/{listing}/products").ReadAsync()).GetArrayLength());

        var id = room.GetProperty("id").GetInt32();
        var other = await app.SignedInAsync("hello@ninearch.example");
        Assert.Equal(HttpStatusCode.NotFound, (await other.PutAsJsonAsync($"/api/v1/supplier/me/products/{id}", new { name = "Taken over" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsJsonAsync($"/api/v1/supplier/me/listings/{listing}/products", new { name = "Not my listing" })).StatusCode);

        MultipartFormDataContent File(byte[] bytes) => new() { { new ByteArrayContent(bytes), "file", "room.png" } };
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsync($"/api/v1/supplier/me/products/{id}/image", File("not a picture"u8.ToArray()))).StatusCode);
        var withImage = await owner.PostAsync($"/api/v1/supplier/me/products/{id}/image", File([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0])).ReadAsync();
        Assert.Equal("image/png", (await open.GetAsync(withImage.GetProperty("imageUrl").GetString())).Content.Headers.ContentType!.MediaType);

        (await owner.DeleteAsync($"/api/v1/supplier/me/products/{id}")).EnsureSuccessStatusCode();
        Assert.Equal(0, (await open.GetAsync($"/api/v1/listings/{listing}/products").ReadAsync()).GetArrayLength());
    }

    [Fact]
    public async Task Products_of_a_listing_that_is_not_live_stay_private()
    {
        var pending = await app.SignedInAsync("anusha@sigirispa.example");
        var listing = (await pending.GetAsync("/api/v1/supplier/me").ReadAsync()).GetProperty("supplier").GetProperty("listing").GetProperty("id").GetInt32();
        await pending.PostAsJsonAsync($"/api/v1/supplier/me/listings/{listing}/products", new { name = "Herbal massage", price = 6000, unit = "60 minutes" }).ReadAsync();
        Assert.Equal(0, (await app.CreateClient().GetAsync($"/api/v1/listings/{listing}/products").ReadAsync()).GetArrayLength());
    }
}

public class ListingPageTests(TestApp app) : IClassFixture<TestApp>
{
    [Fact]
    public async Task Listing_has_its_own_page_with_products_and_the_suppliers_other_listings()
    {
        var owner = await app.SignedInAsync("team@mirissacatch.example");
        var me = (await owner.GetAsync("/api/v1/supplier/me").ReadAsync()).GetProperty("supplier");
        var id = me.GetProperty("listing").GetProperty("id").GetInt32();
        var cat = me.GetProperty("listing").GetProperty("categoryId").GetInt32();
        await owner.PostAsJsonAsync($"/api/v1/supplier/me/listings/{id}/products", new { name = "Grilled seer fish", price = 3200, unit = "per plate" }).ReadAsync();
        await owner.PostAsJsonAsync("/api/v1/supplier/me/listings", new { name = "Mirissa Catch Rooftop", categoryId = cat, town = "Mirissa", district = "Matara", phone = "077 555 0204" }).ReadAsync();

        var html = await app.CreateClient().GetStringAsync($"/listing/{id}");
        Assert.Contains("Mirissa Catch Kitchen", html);
        Assert.Contains("Grilled seer fish", html);
        Assert.Contains("LKR 3,200", html);
        Assert.Contains("More from this business", html);
        Assert.Contains("Mirissa Catch Rooftop", html);
    }

    [Fact]
    public async Task Page_of_a_listing_that_is_not_live_is_not_found()
    {
        var pending = await app.SignedInAsync("anusha@sigirispa.example");
        var id = (await pending.GetAsync("/api/v1/supplier/me").ReadAsync()).GetProperty("supplier").GetProperty("listing").GetProperty("id").GetInt32();
        Assert.Equal(HttpStatusCode.NotFound, (await app.CreateClient().GetAsync($"/listing/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await app.CreateClient().GetAsync("/listing/999999")).StatusCode);
    }
}
