using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SafeTravel.Core.Entities;

namespace SafeTravel.Core.Data;

/// <summary>Creates roles, the first staff accounts, the standard categories and (optionally) sample data.</summary>
public class Seeder(AppDbContext db, UserManager<AppUser> users, RoleManager<IdentityRole<int>> roles)
{
    public async Task RunAsync(string? staffPassword, bool sampleData)
    {
        foreach (var r in Roles.All)
            if (!await roles.RoleExistsAsync(r)) await roles.CreateAsync(new IdentityRole<int>(r));

        if (!await db.Categories.AnyAsync())
        {
            var i = 0;
            db.Categories.AddRange(Cats.Select(c => new Category { Slug = c.Slug, Name = c.Name, Icon = c.Icon, Kind = c.Kind, SortOrder = i++ }));
            await db.SaveChangesAsync();
        }

        if (!await db.SiteSettings.AnyAsync())
        {
            db.SiteSettings.AddRange(
                new SiteSetting { Key = SettingKeys.HeroTitle, Value = "Explore Sri Lanka. We've got your back." },
                new SiteSetting { Key = SettingKeys.HeroText, Value = "Live flood, landslide and storm alerts, a safety map for the whole island, first aid and one-tap SOS. Free for every traveller." },
                new SiteSetting { Key = SettingKeys.AnnualFee, Value = "1000" });
            db.SiteSettings.AddRange(SettingKeys.Sections.Select(s => new SiteSetting { Key = SettingKeys.SectionPrefix + s, Value = "true" }));
            await db.SaveChangesAsync();
        }

        if (!await db.EmergencyNumbers.AnyAsync())
        {
            // Sample numbers. The Red Cross confirms the final list before launch.
            string[][] nums = [["Police", "119"], ["Ambulance", "1990"], ["Tourist hotline", "1912"], ["Disaster", "117"], ["Fire & rescue", "110"]];
            db.EmergencyNumbers.AddRange(nums.Select((n, i) => new EmergencyNumber { Label = n[0], Number = n[1], SortOrder = i }));
            await db.SaveChangesAsync();
        }

        if (!string.IsNullOrWhiteSpace(staffPassword))
        {
            await Staff("admin@safetravel.local", "Super admin", Roles.SuperAdmin, staffPassword);
            if (sampleData)
            {
                await Staff("content@safetravel.local", "Content reviewer", Roles.ContentReviewer, staffPassword);
                await Staff("alerts@safetravel.local", "Alerts officer", Roles.AlertsOfficer, staffPassword);
                await Staff("suppliers@safetravel.local", "Supplier manager", Roles.SupplierManager, staffPassword);
                await Staff("finance@safetravel.local", "Finance officer", Roles.FinanceOfficer, staffPassword);
                await Staff("agent1@safetravel.local", "Call centre agent 1", Roles.CallCentreAgent, staffPassword);
                await Staff("emma@traveller.example", "Emma Wilson", Roles.Traveller, staffPassword);
            }
        }

        if (sampleData && !await db.Listings.AnyAsync()) await SampleAsync(staffPassword);
        await TopBannersAsync();
    }

    /// <summary>The two banners at the top of the home page (Uber left, Dialog right). Runs on every start, also on the live site,
    /// but only fills a place that has never been saved, so changes made under Admin Portal, Sponsors are kept.</summary>
    private async Task TopBannersAsync()
    {
        if (await db.Advertisements.AnyAsync(a => a.Slot == Advertisement.TopLeft || a.Slot == Advertisement.TopRight)) return;
        async Task<Sponsor> Find(string name, string role, int order) =>
            await db.Sponsors.FirstOrDefaultAsync(s => s.Name == name) ?? db.Sponsors.Add(new Sponsor { Name = name, Role = role, SortOrder = order }).Entity;
        var uber = await Find("Uber", "Safe Travel partner", 0);
        var dialog = await Find("Dialog", "Marketing partner", 1);
        db.Advertisements.AddRange(
            new Advertisement { Slot = Advertisement.TopLeft, Sponsor = uber, Headline = "Safe rides, day or night", Text = "", LinkLabel = "Book a ride", LinkUrl = "https://www.uber.com/lk/en/" },
            new Advertisement { Slot = Advertisement.TopRight, Sponsor = dialog, Headline = "Tourist SIM with island-wide coverage", Text = "", LinkLabel = "Get a tourist SIM", LinkUrl = "https://www.dialog.lk/" });
        await db.SaveChangesAsync();
    }

    private async Task Staff(string email, string name, string role, string password)
    {
        if (await users.FindByEmailAsync(email) is not null) return;
        var u = new AppUser { UserName = email, Email = email, FullName = name, EmailConfirmed = true };
        var result = await users.CreateAsync(u, password);
        if (!result.Succeeded) throw new InvalidOperationException("Could not create " + email + ": " + string.Join(" ", result.Errors.Select(e => e.Description)));
        await users.AddToRoleAsync(u, role);
    }

    private record Cat(string Slug, string Name, string Icon, CategoryKind Kind);

    private static readonly Cat[] Cats =
    [
        new("bank", "Banks & ATMs", "🏦", CategoryKind.Help), new("sos", "SOS points", "🆘", CategoryKind.Help),
        new("police", "Police stations", "👮", CategoryKind.Help), new("hospital", "Hospitals", "🏥", CategoryKind.Help),
        new("pharmacy", "Pharmacies", "💊", CategoryKind.Help), new("embassy", "Embassies", "🏛️", CategoryKind.Help),
        new("exchange", "Money exchange", "💱", CategoryKind.Help), new("fuel", "Fuel stations", "⛽", CategoryKind.Help),
        new("transport", "Stations & airports", "🚉", CategoryKind.Help), new("shelter", "Shelters", "🏠", CategoryKind.Help),
        new("stay", "Hotels & stays", "🛏️", CategoryKind.Business), new("food", "Restaurants", "🍛", CategoryKind.Business),
        new("tour", "Tours & guides", "🧭", CategoryKind.Business), new("ride", "Drivers & transport", "🚕", CategoryKind.Business),
        new("rental", "Vehicle rental", "🛵", CategoryKind.Business), new("activity", "Activities", "🏄", CategoryKind.Business),
        new("wellness", "Wellness & Ayurveda", "🌿", CategoryKind.Business)
    ];

    private async Task SampleAsync(string? password)
    {
        var cat = await db.Categories.ToDictionaryAsync(c => c.Slug);
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddMinutes(330));

        Listing Help(string c, string name, string town, string district, string phone, string hours, double lat, double lng, string desc = "") =>
            new() { Category = cat[c], Name = name, Town = town, District = district, Phone = phone, Hours = hours, Latitude = lat, Longitude = lng, Description = desc, Status = ListingStatus.Published, IsVerified = true };

        db.Listings.AddRange(
            Help("bank", "Lakeside Bank, Fort branch", "Colombo", "Colombo", "011 555 0101", "Mon to Fri 9:00 to 15:00, ATM 24 hours", 6.934, 79.845, "Foreign card ATM and currency counter."),
            Help("bank", "Hill Country Bank ATM", "Ella", "Badulla", "057 555 0102", "ATM 24 hours", 6.867, 81.046, "Accepts Visa and Mastercard."),
            Help("bank", "Southern Bank, Galle Fort", "Galle", "Galle", "091 555 0103", "Mon to Fri 9:00 to 15:00", 6.027, 80.217, "Branch with two ATMs inside the fort."),
            Help("sos", "Ella town SOS point", "Ella", "Badulla", "119", "24 hours", 6.875, 81.049, "Staffed help point beside the railway station."),
            Help("sos", "Mirissa beach lifeguard post", "Mirissa", "Matara", "041 555 0105", "Daily 7:00 to 18:00", 5.946, 80.458, "Lifeguards and beach flag updates."),
            Help("police", "Tourist Police, Kandy", "Kandy", "Kandy", "081 555 0106", "24 hours", 7.293, 80.635, "English-speaking officers."),
            Help("police", "Ella Police Station", "Ella", "Badulla", "057 555 0107", "24 hours", 6.871, 81.047),
            Help("police", "Galle Fort Police Post", "Galle", "Galle", "091 555 0108", "24 hours", 6.030, 80.215),
            Help("hospital", "Badulla General Hospital", "Badulla", "Badulla", "055 555 0109", "Emergency unit 24 hours", 6.990, 81.055, "Nearest emergency unit to Ella."),
            Help("hospital", "Kandy National Hospital", "Kandy", "Kandy", "081 555 0110", "Emergency unit 24 hours", 7.286, 80.631),
            Help("pharmacy", "Fort Pharmacy", "Galle", "Galle", "091 555 0111", "Daily 8:00 to 21:00", 6.028, 80.218),
            Help("exchange", "Arrivals currency counter", "Katunayake", "Gampaha", "011 555 0112", "24 hours", 7.180, 79.884, "Inside the airport arrivals hall."),
            Help("transport", "Ella railway station", "Ella", "Badulla", "057 555 0113", "Ticket office 6:00 to 18:00", 6.876, 81.047),
            Help("shelter", "Ella Central College shelter", "Ella", "Badulla", "057 555 0114", "Opens when a warning is issued", 6.869, 81.052, "Capacity 250 people."),
            Help("fuel", "Wellawaya Road fuel station", "Ella", "Badulla", "057 555 0116", "Daily 6:00 to 22:00", 6.861, 81.044));

        async Task Biz(string c, string name, string contact, string email, string phone, string town, string district, string hours, double lat, double lng,
            string desc, SupplierStatus status, DateOnly? paidUntil, double rating, int reviews, bool featured, int views, int calls, int dirs, params string[] docs)
        {
            var s = new Supplier { BusinessName = name, ContactName = contact, Email = email, Phone = phone, Status = status, PaidUntil = paidUntil, Views = views, Calls = calls, Directions = dirs, ApprovedUtc = status == SupplierStatus.Approved ? DateTime.UtcNow.AddDays(-40) : null, CreatedUtc = DateTime.UtcNow.AddDays(-(name.Length % 50)) };
            s.Documents.AddRange(docs.Select(d => new SupplierDocument { Name = d, FileName = "sample.pdf", Status = status == SupplierStatus.Approved ? DocumentStatus.Verified : DocumentStatus.Uploaded }));
            var st = status != SupplierStatus.Approved ? ListingStatus.Pending : paidUntil is null ? ListingStatus.Unpaid : paidUntil < today ? ListingStatus.Lapsed : ListingStatus.Published;
            s.Listings.Add(new Listing { Category = cat[c], Name = name, Town = town, District = district, Phone = phone, Hours = hours, Latitude = lat, Longitude = lng, Description = desc, Status = st, IsVerified = status == SupplierStatus.Approved, IsFeatured = featured, RatingAverage = rating, RatingCount = reviews });
            db.Suppliers.Add(s);
            await db.SaveChangesAsync();
            if (password is null) return;
            var u = new AppUser { UserName = email, Email = email, FullName = contact, EmailConfirmed = true, SupplierId = s.Id };
            if ((await users.CreateAsync(u, password)).Succeeded) await users.AddToRoleAsync(u, Roles.Supplier);
        }

        await Biz("stay", "Ella Ridge Homestay", "Nimal Perera", "owner@ellaridge.example", "077 555 0201", "Ella", "Badulla", "Check-in from 14:00", 6.872, 81.051, "Family-run homestay with a mountain view, five minutes from the station. Staff trained in first aid.", SupplierStatus.Approved, today.AddDays(67), 4.7, 86, true, 1240, 96, 310, "Business registration", "Tourist board licence");
        await Biz("tour", "Nine Arch Walks", "Kavindi Silva", "hello@ninearch.example", "077 555 0202", "Ella", "Badulla", "Daily 6:00 to 18:00", 6.878, 81.060, "Licensed guides for Nine Arch Bridge, Little Adam's Peak and Ella Rock. Routes change when a landslide watch is active.", SupplierStatus.Approved, today.AddDays(202), 4.9, 132, true, 2210, 174, 420, "Business registration", "Guide licence");
        await Biz("ride", "Lanka Safe Rides", "Mohamed Rizwan", "bookings@saferides.example", "077 555 0203", "Kandy", "Kandy", "24 hours", 7.291, 80.640, "Registered drivers for airport and hill-country transfers.", SupplierStatus.Approved, today.AddDays(14), 4.5, 54, false, 860, 141, 12, "Business registration", "Vehicle insurance");
        await Biz("food", "Mirissa Catch Kitchen", "Tharushi Fernando", "team@mirissacatch.example", "077 555 0204", "Mirissa", "Matara", "Daily 11:00 to 22:00", 5.948, 80.455, "Seafood on the beach road. Food safety certificate on file.", SupplierStatus.Approved, today.AddDays(138), 4.6, 211, true, 3105, 88, 640, "Business registration", "Food safety certificate");
        await Biz("rental", "Coast Scooters", "Ruwan Jayasuriya", "ruwan@coastscooters.example", "077 555 0205", "Galle", "Galle", "Daily 8:00 to 19:00", 6.035, 80.220, "Scooter and helmet hire with insurance.", SupplierStatus.Approved, today.AddDays(-12), 4.1, 18, false, 402, 31, 77, "Business registration");
        await Biz("activity", "Weligama Surf School", "Dilan Wickramasinghe", "dilan@weligamasurf.example", "077 555 0206", "Weligama", "Matara", "Daily 6:30 to 17:30", 5.972, 80.426, "Beginner lessons with qualified lifeguard instructors.", SupplierStatus.Pending, null, 0, 0, false, 0, 0, 0, "Business registration", "Lifeguard certificates");
        await Biz("wellness", "Sigiri Herbal Spa", "Anusha Kumari", "anusha@sigirispa.example", "077 555 0207", "Sigiriya", "Matale", "Daily 9:00 to 20:00", 7.955, 80.757, "Traditional Ayurveda treatments.", SupplierStatus.Pending, null, 0, 0, false, 0, 0, 0, "Business registration");

        db.Alerts.AddRange(
            new Alert { Level = AlertLevel.Red, Type = "Landslide watch", Area = "Ella, Badulla", Title = "Heavy rain in the hills until Thursday evening", Source = "NBRO", Details = "Avoid trails, waterfalls and road cuttings.", Latitude = 6.8667, Longitude = 81.0466, PublishedUtc = DateTime.UtcNow.AddMinutes(-12) },
            new Alert { Level = AlertLevel.Blue, Type = "Flood warning", Area = "Ratnapura", Title = "Kalu Ganga rising after heavy rain", Source = "Irrigation Department", Details = "Low-lying roads around Ratnapura may flood overnight.", Latitude = 6.6828, Longitude = 80.3992, PublishedUtc = DateTime.UtcNow.AddHours(-2) },
            new Alert { Level = AlertLevel.Red, Type = "Red flag", Area = "Mirissa, Unawatuna", Title = "No swimming in Mirissa because of rip currents", Source = "Lifeguards", Details = "Rough seas along the south coast. Swim only where a lifeguard is on duty.", Latitude = 5.9483, Longitude = 80.4589, PublishedUtc = DateTime.UtcNow.AddHours(-3) },
            new Alert { Level = AlertLevel.Amber, Type = "Road closure", Area = "Haputale", Title = "A16 closed between Haputale and Beragala", Source = "Road Development Authority", PublishedUtc = DateTime.UtcNow.AddDays(-1), IsActive = false });

        string[][] topics = [["Floods, landslides & storms", "Monsoon floods, hill-country landslides, cyclones, lightning and heat.", "🌧️", "blue"], ["Beach & water safety", "Rip currents, monsoon seas, rivers and waterfalls.", "🌊", "teal"],
            ["Health", "Dengue, food and water, heat illness.", "🦟", "green"], ["First aid", "What to do until help arrives.", "🩹", "rose"],
            ["Roads & transport", "Tuk-tuks, buses, trains, scooters and night driving.", "🛺", "sand"], ["Wildlife", "Elephants, snakes, monkeys, leeches and safaris.", "🐘", "olive"]];
        // Sample risk zones for the safety map. Staff replace them with live information.
        db.RiskZones.AddRange(
            new RiskZone { Type = RiskZoneType.Landslide, Name = "Ella hill slopes", Details = "Avoid trails, waterfalls and road cuttings in heavy rain.", Source = "NBRO", Latitude = 6.8667, Longitude = 81.0466, RadiusMeters = 3000 },
            new RiskZone { Type = RiskZoneType.RoadClosure, Name = "Ella to Wellawaya road (A23) at Ravana Falls", Details = "Closed after a rockfall. Use the Badulla route.", Source = "Police", Latitude = 6.8406, Longitude = 81.0548 },
            new RiskZone { Type = RiskZoneType.Flood, Name = "Kalu Ganga, Ratnapura", Details = "Low-lying roads around Ratnapura may flood overnight.", Source = "Irrigation Department", Latitude = 6.6828, Longitude = 80.3992, RadiusMeters = 4000 },
            new RiskZone { Type = RiskZoneType.BeachFlag, Name = "Mirissa beach", Details = "Red flag. No swimming because of rip currents.", Source = "Lifeguards", Latitude = 5.9483, Longitude = 80.4589, RadiusMeters = 600 });

        // Sample guidance, one tip per line. The Red Cross reviews the wording before launch.
        var guidance = new Dictionary<string, (string Dos, string Donts)>
        {
            ["Floods, landslides & storms"] = (
                "Check alerts before hill-country trips and long drives\nIf water starts rising, move to higher ground. Don't wait to be told\nWatch for landslide signs: new cracks, tilting trees, muddy streams\nIf you feel a strong quake on the coast or the sea pulls back, head inland right away",
                "Walking, swimming or driving through floodwater\nSheltering under trees during lightning\nStaying below steep slopes in heavy rain"),
            ["Beach & water safety"] = (
                "Swim at beaches with lifeguards or flags\nCheck the flag first. Red means no swimming\nCaught in a rip? Float, raise an arm and swim parallel to the shore\nThe south and west coasts are rough from May to September, the east coast from November to February",
                "Swimming alone, at dusk or after drinking\nClimbing wet rocks near waterfalls\nBathing in rivers after rain upstream"),
            ["Health"] = (
                "Wear repellent in the daytime too. Dengue mosquitoes bite during the day\nDrink bottled or boiled water, and carry rehydration salts\nSee a doctor for any fever that lasts 2 days or more\nStay in the shade from 11 am to 3 pm and drink often",
                "Taking ibuprofen or aspirin for a fever until dengue is ruled out\nLeaving any dog or monkey bite untreated. Get rabies care the same day"),
            ["Roads & transport"] = (
                "Use metered tuk-tuks or ride-hailing apps, or agree the fare first\nWear a helmet. The law requires it for riders and passengers\nAvoid driving after dark: poor lighting, animals and fast buses\nStay inside the train. Hanging out of the doors is dangerous",
                "Riding a scooter without a Sri Lankan driving permit. Your insurance may not cover you\nStopping under cliffs on hill roads when it rains"),
            ["Wildlife"] = (
                "Stay at least 50 m from wild elephants and stay in your vehicle\nUse licensed safari jeeps and park guides\nWear closed shoes and carry a torch at night because of snakes\nWear leech socks on rainforest walks. Salt helps remove leeches",
                "Feeding monkeys or carrying food where they can see it\nHonking at, flashing or chasing elephants for photos")
        };
        db.Topics.AddRange(topics.Select((t, i) => new Topic
        {
            Name = t[0], Summary = t[1], Icon = t[2], Tone = t[3], SortOrder = i,
            Dos = guidance.TryGetValue(t[0], out var tips) ? tips.Dos : "", Donts = guidance.TryGetValue(t[0], out var avoid) ? avoid.Donts : ""
        }));

        // Sample first aid guides, one step per line. The Red Cross reviews the wording before launch.
        string[][] guides = [
            ["Not breathing (CPR)", "❤️", "Check for danger. Then tap their shoulders and shout.\nCall 1990, or get someone else to call. Put the phone on speaker.\nPush hard and fast in the centre of the chest, 100 to 120 times a minute.\nIf you are trained, give 2 rescue breaths after every 30 pushes.\nKeep going until help arrives or they start breathing. If there's an AED (defibrillator), use it."],
            ["Severe bleeding", "🩸", "Press firmly on the wound with a clean cloth or your hand.\nKeep pressing. Don't lift it to check.\nIf blood soaks through, put more cloth on top and keep pressing.\nCall 1990. Keep them lying down and warm."],
            ["Burns", "🔥", "Cool the burn under cool running water for at least 20 minutes.\nGently remove rings or tight items nearby, but not clothing stuck to the burn.\nCover it loosely with cling film or a clean plastic bag.\nGet medical help for big or deep burns, or burns on the face or hands. Never use ice, butter or toothpaste."],
            ["Snake bite", "🐍", "Move away from the snake. Help the person stay calm and still.\nTake off rings, watches and tight clothing near the bite.\nKeep the bitten limb still, like a splint, at heart level or below.\nGet to hospital fast. Call 1990. Don't cut or suck the bite, and don't use a tourniquet."],
            ["Heat stroke", "🥵", "Move them into shade or a cool room.\nCool them fast with wet cloths, a fan, or ice packs at the neck and armpits.\nIf they are awake, give small sips of water.\nCall 1990 if they are confused, collapse or stop sweating."],
            ["Drowning", "🏊", "Don't go into the water unless you are trained. Throw them something that floats.\nOnce they are out, check if they are breathing.\nIf not, start CPR. If you are trained, give 5 rescue breaths first.\nCall 1990 and keep them warm. They need a check-up even if they seem fine."],
            ["Choking", "😮", "Ask \"Are you choking?\" Encourage them to cough.\nGive up to 5 firm blows between the shoulder blades.\nThen give up to 5 abdominal thrusts.\nKeep switching between the two. Call 1990 if it doesn't clear."]];
        db.FirstAidGuides.AddRange(guides.Select((g, i) => new FirstAidGuide { Title = g[0], Icon = g[1], Steps = g[2], SortOrder = i }));

        Sponsor uber = new() { Name = "Uber", Role = "Safe Travel partner", SortOrder = 0 }, dialog = new() { Name = "Dialog", Role = "Marketing partner", SortOrder = 1 };
        db.Sponsors.AddRange(uber, dialog);
        // Sample advertisements for the Sponsored section. The third place stays empty until a third sponsor is added in the Admin Portal.
        // The two top banners are added by TopBannersAsync.
        db.Advertisements.AddRange(
            new Advertisement { Slot = 1, Sponsor = uber, Headline = "Rides with registered drivers", Text = "Sample advertisement. Book a ride with a verified driver, day or night.", LinkLabel = "Book a ride", LinkUrl = "https://www.uber.com/lk/en/" },
            new Advertisement { Slot = 2, Sponsor = dialog, Headline = "Stay connected while you travel", Text = "Sample advertisement. Tourist SIM and roaming packs with island-wide coverage.", LinkLabel = "See tourist plans", LinkUrl = "https://www.dialog.lk/" });

        db.Payments.AddRange(
            new Payment { Kind = PaymentKind.Donation, PayerName = "A. Tanaka", Amount = 10000, Status = PaymentStatus.Paid, CreatedUtc = DateTime.UtcNow.AddDays(-8), GatewayReference = "SBX-SAMPLE0001" },
            new Payment { Kind = PaymentKind.Donation, PayerName = "H. Schmidt", Amount = 2500, Status = PaymentStatus.Paid, CreatedUtc = DateTime.UtcNow.AddDays(-4), GatewayReference = "SBX-SAMPLE0002" },
            new Payment { Kind = PaymentKind.Donation, PayerName = "Anonymous donor", Amount = 5000, Status = PaymentStatus.Paid, CreatedUtc = DateTime.UtcNow.AddDays(-2), GatewayReference = "SBX-SAMPLE0003" });

        // A sample traveller with emergency contacts, a trip plan and reviews.
        var emma = await users.FindByEmailAsync("emma@traveller.example");
        if (emma is not null)
        {
            db.TravellerContacts.AddRange(
                new TravellerContact { UserId = emma.Id, Name = "Sarah Wilson", Phone = "+44 7700 900123", Relationship = "Sister" },
                new TravellerContact { UserId = emma.Id, Name = "James Wilson", Phone = "+44 7700 900456", Relationship = "Father" });
            db.TripStops.AddRange(
                new TripStop { UserId = emma.Id, Place = "Kandy", Date = today.AddDays(-2), Note = "Temple of the Tooth" },
                new TripStop { UserId = emma.Id, Place = "Ella", Date = today, Note = "Nine Arch Bridge and Little Adam's Peak" },
                new TripStop { UserId = emma.Id, Place = "Mirissa", Date = today.AddDays(3), Note = "Whale watching" });
            var homestay = db.Listings.Local.FirstOrDefault(l => l.Name == "Ella Ridge Homestay") ?? await db.Listings.FirstOrDefaultAsync(l => l.Name == "Ella Ridge Homestay");
            var walks = db.Listings.Local.FirstOrDefault(l => l.Name == "Nine Arch Walks") ?? await db.Listings.FirstOrDefaultAsync(l => l.Name == "Nine Arch Walks");
            if (homestay is not null) db.Reviews.Add(new Review { Listing = homestay, UserId = emma.Id, AuthorName = "Emma W.", Stars = 5, Text = "Sample review. Friendly hosts, a clear view of the valley and good advice about the weather.", Status = ReviewStatus.Approved, CreatedUtc = DateTime.UtcNow.AddDays(-6), DecidedUtc = DateTime.UtcNow.AddDays(-5) });
            if (walks is not null) db.Reviews.Add(new Review { Listing = walks, UserId = emma.Id, AuthorName = "Emma W.", Stars = 4, Text = "Sample review. The guide knew the trail well and kept the group safe in the rain.", Status = ReviewStatus.Pending, CreatedUtc = DateTime.UtcNow.AddHours(-5) });
        }
        await db.SaveChangesAsync();
    }
}
