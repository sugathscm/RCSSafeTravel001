using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SafeTravel.Core.Data;
using SafeTravel.Core.Entities;
using SafeTravel.Core.Services;

namespace SafeTravel.Tests;

public class PublicApiTests(TestApp app) : IClassFixture<TestApp>
{
    private readonly HttpClient _http = app.CreateClient();

    [Fact]
    public async Task Home_content_has_headline_numbers_topics_and_sponsors()
    {
        var home = await _http.GetAsync("/api/v1/content/home").ReadAsync();
        Assert.Contains("Sri Lanka", home.GetProperty("heroTitle").GetString());
        Assert.Equal(5, home.GetProperty("emergencyNumbers").GetArrayLength());
        Assert.Equal(6, home.GetProperty("topics").GetArrayLength());
        Assert.Equal(1000m, home.GetProperty("annualFee").GetDecimal());
        Assert.True(home.GetProperty("sections").GetProperty("map").GetBoolean());
    }

    [Fact]
    public async Task Listings_show_only_live_places()
    {
        var names = (await _http.GetAsync("/api/v1/listings?pageSize=200").ReadAsync()).Names().ToList();
        Assert.Contains("Ella Ridge Homestay", names);
        Assert.Contains("Tourist Police, Kandy", names);
        Assert.DoesNotContain("Coast Scooters", names);        // annual fee lapsed
        Assert.DoesNotContain("Weligama Surf School", names);  // not approved yet
    }

    [Theory]
    [InlineData("category=bank", "Lakeside Bank, Fort branch", "Ella Police Station")]
    [InlineData("kind=business", "Nine Arch Walks", "Fort Pharmacy")]
    [InlineData("q=police", "Galle Fort Police Post", "Fort Pharmacy")]
    [InlineData("district=Galle", "Fort Pharmacy", "Ella railway station")]
    public async Task Listings_can_be_filtered(string query, string expected, string notExpected)
    {
        var names = (await _http.GetAsync("/api/v1/listings?" + query).ReadAsync()).Names().ToList();
        Assert.Contains(expected, names);
        Assert.DoesNotContain(notExpected, names);
    }

    [Fact]
    public async Task Listings_near_a_point_come_back_nearest_first()
    {
        var page = await _http.GetAsync("/api/v1/listings?lat=6.875&lng=81.049&radiusKm=5").ReadAsync();
        var items = page.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal("Ella town SOS point", items[0].GetProperty("name").GetString());
        Assert.All(items, i => Assert.True(i.GetProperty("distanceKm").GetDouble() <= 5));
        Assert.DoesNotContain("Tourist Police, Kandy", page.Names());
    }

    [Fact]
    public async Task Bad_paging_is_refused()
    {
        Assert.Equal(HttpStatusCode.BadRequest, (await _http.GetAsync("/api/v1/listings?pageSize=5000")).StatusCode);
    }

    [Fact]
    public async Task Responses_carry_security_headers()
    {
        var res = await _http.GetAsync("/api/v1/alerts");
        Assert.Equal("nosniff", res.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Contains("frame-ancestors 'none'", res.Headers.GetValues("Content-Security-Policy").Single());
    }
}

public class AuthTests(TestApp app) : IClassFixture<TestApp>
{
    [Fact]
    public async Task Wrong_password_is_refused()
    {
        var res = await app.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new { email = "admin@safetravel.local", password = "wrong-password" });
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Account_locks_after_five_wrong_attempts()
    {
        var http = app.CreateClient();
        for (var i = 0; i < 5; i++) await http.PostAsJsonAsync("/api/v1/auth/login", new { email = "agent1@safetravel.local", password = "wrong-password" });
        var res = await http.PostAsJsonAsync("/api/v1/auth/login", new { email = "agent1@safetravel.local", password = TestApp.Password });
        Assert.Equal((HttpStatusCode)423, res.StatusCode);
    }

    [Fact]
    public async Task Admin_pages_need_a_staff_sign_in()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await app.CreateClient().GetAsync("/api/v1/admin/dashboard")).StatusCode);
        var supplier = await app.SignedInAsync("owner@ellaridge.example");
        Assert.Equal(HttpStatusCode.Forbidden, (await supplier.GetAsync("/api/v1/admin/dashboard")).StatusCode);
        var admin = await app.AdminAsync();
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/v1/admin/dashboard")).StatusCode);
    }

    [Fact]
    public async Task Each_staff_role_reaches_only_its_own_work()
    {
        var alerts = await app.SignedInAsync("alerts@safetravel.local");
        Assert.Equal(HttpStatusCode.Forbidden, (await alerts.PostAsync("/api/v1/admin/suppliers/6/approve", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await alerts.GetAsync("/api/v1/admin/payments")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await alerts.GetAsync("/api/v1/admin/audit")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await alerts.GetAsync("/api/v1/admin/alerts")).StatusCode);
    }

    [Fact]
    public async Task Refresh_token_works_once()
    {
        var http = app.CreateClient();
        var login = await http.PostAsJsonAsync("/api/v1/auth/login", new { email = "finance@safetravel.local", password = TestApp.Password }).ReadAsync();
        var refresh = login.GetProperty("refreshToken").GetString();
        var next = await http.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = refresh }).ReadAsync();
        Assert.False(string.IsNullOrEmpty(next.GetProperty("accessToken").GetString()));
        // The old token was used up. Presenting it again is treated as theft and ends every session.
        Assert.Equal(HttpStatusCode.Unauthorized, (await http.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = refresh })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await http.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = next.GetProperty("refreshToken").GetString() })).StatusCode);
    }

    [Fact]
    public async Task Traveller_can_register_and_read_their_account()
    {
        var http = app.CreateClient();
        var reg = await http.PostAsJsonAsync("/api/v1/auth/register", new { email = "traveller@example.com", password = "Sunny-Beach-2026", fullName = "Test Traveller" }).ReadAsync();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", reg.GetProperty("accessToken").GetString());
        var me = await http.GetAsync("/api/v1/auth/me").ReadAsync();
        Assert.Equal("Traveller", me.GetProperty("roles")[0].GetString());
        Assert.Equal(HttpStatusCode.Forbidden, (await http.GetAsync("/api/v1/supplier/me")).StatusCode);
    }
}

public class SupplierFlowTests(TestApp app) : IClassFixture<TestApp>
{
    private static object Registration(string email, int categoryId) => new
    {
        businessName = "Kandy Lake Cafe", categoryId, town = "Kandy", district = "Kandy", contactName = "Test Owner",
        phone = "0771234567", email, password = "Lake-View-2026", hours = "Daily 8:00 to 20:00", description = "Cafe by the lake."
    };

    private async Task<int> BusinessCategoryAsync() =>
        (await app.CreateClient().GetAsync("/api/v1/categories").ReadAsync()).EnumerateArray().First(c => c.GetProperty("slug").GetString() == "food").GetProperty("id").GetInt32();

    [Fact]
    public async Task Register_approve_pay_then_live()
    {
        var anon = app.CreateClient();
        var created = await anon.PostAsJsonAsync("/api/v1/suppliers/register", Registration("cafe@example.com", await BusinessCategoryAsync())).ReadAsync();
        var id = created.GetProperty("id").GetInt32();
        Assert.Equal("pending", created.GetProperty("status").GetString());
        Assert.DoesNotContain("Kandy Lake Cafe", (await anon.GetAsync("/api/v1/listings?q=Kandy+Lake").ReadAsync()).Names());

        var supplier = await app.SignedInAsync("cafe@example.com", "Lake-View-2026");
        Assert.Equal(HttpStatusCode.BadRequest, (await supplier.PostAsync("/api/v1/supplier/me/pay", null)).StatusCode); // not approved yet

        var admin = await app.AdminAsync();
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsync($"/api/v1/admin/suppliers/{id}/approve", null)).StatusCode);
        var me = await supplier.GetAsync("/api/v1/supplier/me").ReadAsync();
        Assert.Equal("unpaid", me.GetProperty("supplier").GetProperty("listing").GetProperty("status").GetString());
        Assert.DoesNotContain("Kandy Lake Cafe", (await anon.GetAsync("/api/v1/listings?q=Kandy+Lake").ReadAsync()).Names());

        var payment = await supplier.PostAsync("/api/v1/supplier/me/pay", null).ReadAsync();
        Assert.Equal("Paid", payment.GetProperty("status").GetString());
        Assert.Equal(1000m, payment.GetProperty("amount").GetDecimal());

        me = await supplier.GetAsync("/api/v1/supplier/me").ReadAsync();
        var clock = app.Services.GetRequiredService<IClock>();
        Assert.Equal(clock.Today.AddYears(1).ToString("yyyy-MM-dd"), me.GetProperty("supplier").GetProperty("paidUntil").GetString());
        Assert.Contains("Kandy Lake Cafe", (await anon.GetAsync("/api/v1/listings?q=Kandy+Lake").ReadAsync()).Names());
    }

    [Fact]
    public async Task Same_email_cannot_register_twice()
    {
        var anon = app.CreateClient();
        var cat = await BusinessCategoryAsync();
        (await anon.PostAsJsonAsync("/api/v1/suppliers/register", Registration("twice@example.com", cat))).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await anon.PostAsJsonAsync("/api/v1/suppliers/register", Registration("twice@example.com", cat))).StatusCode);
    }

    [Fact]
    public async Task Supplier_edits_own_listing_and_renewing_early_adds_a_year()
    {
        var supplier = await app.SignedInAsync("hello@ninearch.example");
        var before = await supplier.GetAsync("/api/v1/supplier/me").ReadAsync();
        var listing = before.GetProperty("supplier").GetProperty("listing");
        var until = DateOnly.Parse(before.GetProperty("supplier").GetProperty("paidUntil").GetString()!);

        var updated = await supplier.PutAsJsonAsync("/api/v1/supplier/me/listing", new
        {
            name = "Nine Arch Walks", categoryId = listing.GetProperty("categoryId").GetInt32(), town = "Ella", district = "Badulla",
            phone = "077 555 0999", hours = "Daily 5:30 to 18:00", description = "Sunrise walks added."
        }).ReadAsync();
        Assert.Equal("077 555 0999", updated.GetProperty("listing").GetProperty("phone").GetString());
        var seen = await app.CreateClient().GetAsync("/api/v1/listings?q=Nine+Arch").ReadAsync();
        Assert.Equal("077 555 0999", seen.GetProperty("items")[0].GetProperty("phone").GetString());

        await supplier.PostAsync("/api/v1/supplier/me/pay", null).ReadAsync();
        var after = await supplier.GetAsync("/api/v1/supplier/me").ReadAsync();
        Assert.Equal(until.AddYears(1).ToString("yyyy-MM-dd"), after.GetProperty("supplier").GetProperty("paidUntil").GetString());
        Assert.Equal(1, (await supplier.GetAsync("/api/v1/supplier/me/payments").ReadAsync()).GetArrayLength());
    }

    [Fact]
    public async Task Document_upload_checks_the_real_file_type()
    {
        var supplier = await app.SignedInAsync("team@mirissacatch.example");
        MultipartFormDataContent Form(byte[] bytes, string fileName)
        {
            var file = new ByteArrayContent(bytes);
            file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
            return new MultipartFormDataContent { { new StringContent("Insurance certificate"), "name" }, { file, "file", fileName } };
        }

        Assert.Equal(HttpStatusCode.BadRequest, (await supplier.PostAsync("/api/v1/supplier/me/documents", Form("MZ this is a program"u8.ToArray(), "cert.pdf"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await supplier.PostAsync("/api/v1/supplier/me/documents", Form("%PDF-1.7"u8.ToArray(), "cert.exe"))).StatusCode);

        var ok = await supplier.PostAsync("/api/v1/supplier/me/documents", Form("%PDF-1.7 sample"u8.ToArray(), "cert.pdf")).ReadAsync();
        var docId = ok.GetProperty("id").GetInt32();
        Assert.Equal(HttpStatusCode.OK, (await supplier.GetAsync($"/api/v1/documents/{docId}")).StatusCode);

        var other = await app.SignedInAsync("owner@ellaridge.example");
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/v1/documents/{docId}")).StatusCode);
    }

    [Fact]
    public async Task Renewal_job_hides_lapsed_listings_and_reminds_once()
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var suppliers = scope.ServiceProvider.GetRequiredService<SupplierService>();
        var today = scope.ServiceProvider.GetRequiredService<IClock>().Today;

        var rides = await db.Suppliers.Include(s => s.Listings).FirstAsync(s => s.BusinessName == "Lanka Safe Rides");
        var homestay = await db.Suppliers.FirstAsync(s => s.BusinessName == "Ella Ridge Homestay");
        rides.PaidUntil = today.AddDays(-1);
        homestay.PaidUntil = today.AddDays(30);
        await db.SaveChangesAsync();

        var (lapsed, reminders) = await suppliers.RunRenewalsAsync();
        Assert.Equal(1, lapsed);
        Assert.Equal(1, reminders);
        Assert.Equal(ListingStatus.Lapsed, rides.Listings[0].Status);

        (lapsed, reminders) = await suppliers.RunRenewalsAsync();
        Assert.Equal((0, 0), (lapsed, reminders));
    }
}

public class AdminTests(TestApp app) : IClassFixture<TestApp>
{
    private readonly HttpClient _anon = app.CreateClient();

    [Fact]
    public async Task Help_point_goes_live_can_be_hidden_and_its_category_switched_off()
    {
        var admin = await app.AdminAsync();
        var cats = await admin.GetAsync("/api/v1/admin/categories").ReadAsync();
        var bank = cats.EnumerateArray().First(c => c.GetProperty("slug").GetString() == "bank").GetProperty("id").GetInt32();

        var added = await admin.PostAsJsonAsync("/api/v1/admin/listings", new { name = "Jaffna Town Bank", categoryId = bank, town = "Jaffna", district = "Jaffna", phone = "021 555 0100", hours = "Mon to Fri" }).ReadAsync();
        var id = added.GetProperty("id").GetInt32();
        Assert.InRange(added.GetProperty("latitude").GetDouble(), 9.5, 9.8); // placed at the district centre
        Assert.Contains("Jaffna Town Bank", (await _anon.GetAsync("/api/v1/listings?district=Jaffna").ReadAsync()).Names());

        (await admin.PutAsJsonAsync($"/api/v1/admin/listings/{id}/visible", new { on = false })).EnsureSuccessStatusCode();
        Assert.DoesNotContain("Jaffna Town Bank", (await _anon.GetAsync("/api/v1/listings?district=Jaffna").ReadAsync()).Names());
        (await admin.PutAsJsonAsync($"/api/v1/admin/listings/{id}/visible", new { on = true })).EnsureSuccessStatusCode();

        (await admin.PutAsJsonAsync($"/api/v1/admin/categories/{bank}/active", new { on = false })).EnsureSuccessStatusCode();
        Assert.Empty((await _anon.GetAsync("/api/v1/listings?category=bank").ReadAsync()).Names());
        Assert.DoesNotContain("bank", (await _anon.GetAsync("/api/v1/categories").ReadAsync()).EnumerateArray().Select(c => c.GetProperty("slug").GetString()));
        (await admin.PutAsJsonAsync($"/api/v1/admin/categories/{bank}/active", new { on = true })).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/v1/admin/listings/{id}")).StatusCode);
    }

    [Fact]
    public async Task Supplier_listing_cannot_be_deleted_only_hidden()
    {
        var admin = await app.AdminAsync();
        var page = await admin.GetAsync("/api/v1/admin/listings?kind=business").ReadAsync();
        var id = page.GetProperty("items")[0].GetProperty("id").GetInt32();
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.DeleteAsync($"/api/v1/admin/listings/{id}")).StatusCode);
    }

    [Fact]
    public async Task Portal_content_changes_show_at_once()
    {
        var content = await app.SignedInAsync("content@safetravel.local");
        await _anon.GetAsync("/api/v1/content/home"); // warm the cache
        (await content.PutAsJsonAsync("/api/v1/admin/content/hero", new { heroTitle = "Travel safe, travel far.", heroText = "New text." })).EnsureSuccessStatusCode();
        (await content.PutAsJsonAsync("/api/v1/admin/content/sections/sponsors", new { on = false })).EnsureSuccessStatusCode();
        (await content.PutAsJsonAsync("/api/v1/admin/content/emergency-numbers", new[] { new { label = "Police", number = "119" }, new { label = "Ambulance", number = "1990" } })).EnsureSuccessStatusCode();
        var topic = await content.PostAsJsonAsync("/api/v1/admin/content/topics", new { name = "Trekking", summary = "Guides, weather and leeches." }).ReadAsync();

        var home = await _anon.GetAsync("/api/v1/content/home").ReadAsync();
        Assert.Equal("Travel safe, travel far.", home.GetProperty("heroTitle").GetString());
        Assert.False(home.GetProperty("sections").GetProperty("sponsors").GetBoolean());
        Assert.Equal(2, home.GetProperty("emergencyNumbers").GetArrayLength());
        Assert.Contains("Trekking", home.GetProperty("topics").EnumerateArray().Select(t => t.GetProperty("name").GetString()));

        Assert.Equal(HttpStatusCode.NoContent, (await content.DeleteAsync($"/api/v1/admin/content/topics/{topic.GetProperty("id").GetInt32()}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await content.PutAsJsonAsync("/api/v1/admin/content/hero", new { heroTitle = "" })).StatusCode);
    }

    [Fact]
    public async Task Alert_is_published_then_expired()
    {
        var officer = await app.SignedInAsync("alerts@safetravel.local");
        await _anon.GetAsync("/api/v1/alerts");
        var a = await officer.PostAsJsonAsync("/api/v1/admin/alerts", new { level = "amber", type = "Storm warning", area = "Nuwara Eliya", title = "Strong winds expected tonight" }).ReadAsync();
        IEnumerable<string?> Titles(JsonElement e) => e.EnumerateArray().Select(x => x.GetProperty("title").GetString());
        Assert.Contains("Strong winds expected tonight", Titles(await _anon.GetAsync("/api/v1/alerts").ReadAsync()));
        (await officer.PutAsJsonAsync($"/api/v1/admin/alerts/{a.GetProperty("id").GetInt32()}/active", new { on = false })).EnsureSuccessStatusCode();
        Assert.DoesNotContain("Strong winds expected tonight", Titles(await _anon.GetAsync("/api/v1/alerts").ReadAsync()));
    }

    [Fact]
    public async Task Donation_reaches_the_finance_page_and_fee_can_change()
    {
        var before = await (await app.SignedInAsync("finance@safetravel.local")).GetAsync("/api/v1/admin/payments").ReadAsync();
        var donated = await _anon.PostAsJsonAsync("/api/v1/donations", new { amount = 2500, name = "Test Donor", email = "donor@example.com" }).ReadAsync();
        Assert.Equal("Paid", donated.GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, (await _anon.PostAsJsonAsync("/api/v1/donations", new { amount = 5 })).StatusCode);

        var finance = await app.SignedInAsync("finance@safetravel.local");
        var after = await finance.GetAsync("/api/v1/admin/payments").ReadAsync();
        Assert.Equal(before.GetProperty("summary").GetProperty("donations").GetDecimal() + 2500, after.GetProperty("summary").GetProperty("donations").GetDecimal());

        (await finance.PutAsJsonAsync("/api/v1/admin/settings/annual-fee", new { fee = 1500 })).EnsureSuccessStatusCode();
        Assert.Equal(1500m, (await _anon.GetAsync("/api/v1/content/home").ReadAsync()).GetProperty("annualFee").GetDecimal());
        (await finance.PutAsJsonAsync("/api/v1/admin/settings/annual-fee", new { fee = 1000 })).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Every_admin_change_is_written_to_the_audit_log()
    {
        var admin = await app.AdminAsync();
        (await admin.PostAsJsonAsync("/api/v1/admin/sponsors", new { name = "Test Insurer", role = "Insurance partner" })).EnsureSuccessStatusCode();
        var log = await admin.GetAsync("/api/v1/admin/audit").ReadAsync();
        var first = log.GetProperty("items")[0];
        Assert.Contains("Test Insurer", first.GetProperty("action").GetString());
        Assert.Equal("Super admin", first.GetProperty("userName").GetString());
    }

    [Fact]
    public async Task Disabled_staff_cannot_sign_in()
    {
        var admin = await app.AdminAsync();
        var added = await admin.PostAsJsonAsync("/api/v1/admin/users", new { fullName = "Temp Agent", email = "temp@safetravel.local", role = "CallCentreAgent", password = "Temp-Agent-2026" }).ReadAsync();
        (await app.SignedInAsync("temp@safetravel.local", "Temp-Agent-2026")).Dispose();
        (await admin.PutAsJsonAsync($"/api/v1/admin/users/{added.GetProperty("id").GetInt32()}/active", new { on = false })).EnsureSuccessStatusCode();
        var res = await _anon.PostAsJsonAsync("/api/v1/auth/login", new { email = "temp@safetravel.local", password = "Temp-Agent-2026" });
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }
}
