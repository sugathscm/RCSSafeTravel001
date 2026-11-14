# Safe Travel Sri Lanka

One ASP.NET Core 10 solution with four faces, all on one REST API and one SQL Server 2019 database.

| Application | Address | Who uses it |
|---|---|---|
| Web Portal | `/` | Travellers and the public |
| Admin Portal (Internal) | `/admin` | Staff |
| Admin Portal (Supplier) | `/supplier` | Registered businesses |
| REST API | `/api/v1/...` | The portals and the Android and iOS apps |

The Web Portal has no fixed content. The headline, sections, emergency numbers, topics, sponsors, categories, listings and alerts all come from the database and change the moment staff or suppliers change them.

## Run it on your PC

You need the .NET 10 SDK and SQL Server 2019 (any edition, including Express or LocalDB).

1. Open `SafeTravel.slnx` in Visual Studio 2026, or a terminal in this folder.
2. The connection string in `src/SafeTravel.Web/appsettings.json` points at the hosted database `db_a666d3_safetravel` on `sql5080.site4now.net`. Replace `YOUR_DB_PASSWORD` with the real password. Better still, keep the password out of the file:

   ```
   cd src/SafeTravel.Web
   dotnet user-secrets init
   dotnet user-secrets set "ConnectionStrings:Default" "Data Source=sql5080.site4now.net;Initial Catalog=db_a666d3_safetravel;User Id=db_a666d3_safetravel_admin;Password=THE-REAL-PASSWORD;Encrypt=True;TrustServerCertificate=True;MultipleActiveResultSets=True;"
   ```

   For a SQL Server on your own PC use `Server=localhost;Database=SafeTravel;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True`.

   Note: in Development the first start also loads the sample data (sample suppliers, alerts, advertisements) into that database. For a clean database set `Seed:SampleData` to `false` in `appsettings.Development.json` before the first run.
3. Run:

   ```
   dotnet run --project src/SafeTravel.Web
   ```

   On first start the application creates the database, the tables and the sample data. Open the address it prints, for example `http://localhost:5198`.

To create the tables yourself instead, run `database/SafeTravel-MSSQL2019.sql` in SQL Server Management Studio and set `Database:MigrateOnStart` to `false`.

No SQL Server at hand? This runs the same application on a local SQLite file, for a quick look only:

```
set Database__Provider=Sqlite
dotnet run --project src/SafeTravel.Web
```

### Sample sign-ins (Development only)

The password for every sample account is the value of `Seed:StaffPassword` in `appsettings.Development.json` (`ChangeMe!2026`).

| Sign in as | Email |
|---|---|
| Super admin | admin@safetravel.local |
| Content reviewer | content@safetravel.local |
| Alerts officer | alerts@safetravel.local |
| Supplier manager | suppliers@safetravel.local |
| Finance officer | finance@safetravel.local |
| Call centre agent | agent1@safetravel.local |
| Supplier, live listing | owner@ellaridge.example |
| Supplier, approved and waiting to pay | bookings@saferides.example |
| Supplier, fee lapsed | ruwan@coastscooters.example |
| Supplier, waiting for review | dilan@weligamasurf.example |

### Try the full flow

1. Web Portal, **List your business**: register a new business.
2. Sign in as that business, upload a document (PDF, JPG or PNG).
3. Sign in as the super admin, open **Suppliers**, approve it.
4. Sign in as the business again, **Payment and renewal**, pay the annual fee.
5. The listing is now on the Web Portal under **Find help & places** and in `GET /api/v1/listings`.

## Supplier listings and promotions

A supplier can have several listings, for example a hotel adds its restaurant and its rooms (Supplier Portal, **My listings**). Each one is its own tile on the Web Portal and in the app. New listings follow the supplier: they wait while the supplier is in review and go live at once when the supplier is approved and the annual fee is paid. One annual fee covers all listings of a supplier.

Any listing can carry a promotion: a short offer, optional details and an optional last day. Travellers see a red **Promotion** badge on the tile, the offer under the name, and a **Promotions** filter on the directory (`GET /api/v1/listings?promo=true`). A promotion disappears by itself after its last day. Staff can remove one under **Listings**.

## Supplier products

Under each listing a supplier can add products: rooms, dishes, tours, rentals (Supplier Portal, **My listings**, **Products**). A product has a name, description, price in LKR with a note such as "per night", an optional picture (JPG or PNG up to 2 MB) and a switch to show or hide it. Travellers see the products and prices when they open the listing tile. The mobile apps read them from `GET /api/v1/listings/{id}/products`.

## Listing pages

Every live listing has its own page at `/listing/{id}`: details, map, promotion, all products with prices and pictures, and the supplier's other listings. Tiles on the home page and the directory open this page. The directory itself (`/find`) loads 24 tiles at a time with a **Show more** button.

## Safety map and alerts

- **Safety map** (`/map`): one map with layers the traveller switches on and off. Risk layers (flood zones, landslide zones, road closures, beach flags, wildlife, other risks), pinned alerts, and every help point category. Tapping a pin shows the advice, directions and, for help points, a call button.
- **Map zones**: staff mark risk zones in the Admin Portal under **Map zones**: type, name, advice, source, position and a radius in metres (0 shows a pin only). Zones must be inside Sri Lanka.
- **Alerts** (`/alerts`): every active alert with its level, advice, area and source. An alert can carry optional advice and an optional map position; a pinned alert shows on the map and gets a "Show on map" link.

Zones and alerts are entered by staff. Feeds from the Disaster Management Centre, NBRO or the Department of Meteorology are not connected.

## Safety content pages

- **Safety topics** (`/topics`, `/topics/{id}`): each topic has its own page with a "Do" list and an "Avoid" list. Staff edit the lists in the Admin Portal under **Web Portal content**, **Safety topics**, **Do and avoid** (one tip per line).
- **First aid** (`/first-aid`): step-by-step guides. Staff manage them under **First aid guides**: title, icon, steps (one per line) and an optional video. Videos are hosted on Vimeo or Streamable, not uploaded here: paste the video's link (for example `https://vimeo.com/123456789`) and the page shows the player. Other links are refused.
- **Emergency numbers** (`/emergency`): the numbers set under **Web Portal content**, each with a call button, plus the hospitals listed as help points.

The sample topics and guides are sample wording. The Red Cross reviews them before launch.

## Sponsor advertisements

The Web Portal home page has three advertisement places, one per sponsor, shown under the headline. Staff manage them in the Admin Portal under **Sponsors**: choose the sponsor, write the headline, text, button and link, add a picture (JPG or PNG up to 2 MB) and switch each one on or off. Clicks are counted per advertisement. The mobile apps read the same advertisements from `GET /api/v1/ads`. The whole strip can be hidden under **Web Portal content**.

## Look and feel

The screens follow the approved Safe Travel prototype (web portal): 1280px pages, large rounded cards, Bricolage Grotesque headings, Public Sans text, one signal red. All of it lives in `src/SafeTravel.Web/wwwroot/css/site.css`. The Admin and Supplier portals use the same design language.

The Sri Lanka Red Cross Society logo is one file, `src/SafeTravel.Web/wwwroot/img/slrcs-logo.png` (square PNG). It appears in the header, hero badge, footer, sign-in page and both admin portals. To change it, replace that file. If the file is ever missing, a neutral shield mark is shown in its place.

## What is where

```
src/SafeTravel.Core     Entities, the EF Core database context, business rules (no web code)
src/SafeTravel.Web      REST API (Api/), MVC pages (Controllers/, Areas/Admin, Areas/Supplier, Views/),
                        Bootstrap 5 + jQuery screens (wwwroot/js), SQL Server migrations (Data/Migrations)
tests/SafeTravel.Tests  48 automated tests that run the real application end to end
database/               SQL script that creates the schema on SQL Server 2019
Models/                 The clickable model approved before the build
```

Run the tests with `dotnet test`.

## REST API for the mobile apps

Open `/openapi/v1.json` in Development for the full list. The main calls:

| Call | What it does |
|---|---|
| `POST /api/v1/auth/login` | Sign in. Returns a 15 minute access token and a refresh token |
| `POST /api/v1/auth/refresh` | Swap the refresh token for a new pair. Each refresh token works once |
| `POST /api/v1/auth/register` | Create a traveller account |
| `GET /api/v1/content/home` | Headline, sections, topics, emergency numbers, sponsors |
| `GET /api/v1/categories` | Categories that are switched on |
| `GET /api/v1/listings?kind=&category=&district=&q=&lat=&lng=&radiusKm=` | Help points and businesses, nearest first when a position is sent |
| `GET /api/v1/alerts` | Active alerts |
| `GET /api/v1/risk-zones` | Risk zones for the safety map: type, name, advice, position and radius |
| `GET /api/v1/topics` and `GET /api/v1/topics/{id}` | Safety topics with their do and avoid lists |
| `GET /api/v1/first-aid` | First aid guides: steps in order and the video player address |
| `GET /api/v1/ads` | The sponsor advertisements, up to three |
| `POST /api/v1/listings/{id}/track/{view,call,directions}` | Counts shown to the supplier |
| `POST /api/v1/donations` | Make a donation |

Send the access token as `Authorization: Bearer <token>`.

## Security

- Passwords hashed by ASP.NET Core Identity. Sign-in locks for 15 minutes after 5 wrong attempts.
- Two-step sign-in with an authenticator app (Admin Portal, **Security and API**).
- Roles: super admin, content reviewer, alerts officer, supplier manager, finance officer, call centre agent, supplier, traveller. Every API call checks the role on the server.
- Browsers use a secure, HTTP-only cookie and must send an anti-forgery token with every change. Mobile apps use short-lived JWT tokens with rotating refresh tokens.
- Rate limits on the whole API, tighter on sign-in and public forms.
- Strict content security policy (no inline or third-party scripts), HSTS and HTTPS redirect outside Development.
- Uploaded documents are checked by content, not only by name, and stored outside the web root.
- Every admin change is written to the audit log.

## High traffic

- Public API answers are cached in memory (15 to 60 seconds) and cleared the moment an admin changes something.
- The Web Portal pages read one cached snapshot, so alert peaks do not reach the database.
- Database connections are pooled, read queries do not track entities, lists are paged, and the search columns are indexed.
- Responses are compressed and static files are cached by the browser for a week.

To run on more than one server, move the output cache and data-protection keys to Redis.

## Before going live

| Setting | What to do |
|---|---|
| `Jwt:Key` | Set a secret of 32 characters or more (environment variable `Jwt__Key`). The application will not start in Production without it |
| `ConnectionStrings:Default` | Point at the production SQL Server with a least-privilege login |
| `Seed:StaffPassword` | Set once to create the first super admin (`admin@safetravel.local`), then remove it and change the password |
| `Seed:SampleData` | Leave `false` |
| Payment gateway | `SandboxPaymentGateway` approves every payment. Replace it with the real gateway behind `IPaymentGateway` |
| Email and SMS | `LogNotificationSender` only writes to the log. Replace it behind `INotificationSender` |
| Emergency numbers, annual fee | Sample values. Confirm them in the Admin Portal |
