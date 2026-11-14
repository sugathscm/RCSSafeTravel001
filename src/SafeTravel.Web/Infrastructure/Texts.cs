using System.Text.Json;

namespace SafeTravel.Web.Infrastructure;

/// <summary>
/// The fixed text of the public pages in other languages. The views hold the English text and look it up here:
/// <c>@T["Find help near you"]</c>. A text with no translation is shown in English.
/// Translations live in Resources/texts.si.json (Sinhala) and Resources/texts.ta.json (Tamil), one English text per key.
/// </summary>
public class Texts
{
    public static readonly (string Code, string Name)[] Languages = [("en", "English"), ("si", "සිංහල"), ("ta", "தமிழ்")];
    public const string CookieName = "lang";

    private readonly IHttpContextAccessor _http;
    private readonly Dictionary<string, Dictionary<string, string>> _all = new();

    public Texts(IHttpContextAccessor http, IWebHostEnvironment env)
    {
        _http = http;
        foreach (var (code, _) in Languages.Where(l => l.Code != "en"))
        {
            var path = Path.Combine(env.ContentRootPath, "Resources", $"texts.{code}.json");
            _all[code] = File.Exists(path) ? JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path)) ?? new() : new();
        }
    }

    public static bool IsKnown(string? code) => Languages.Any(l => l.Code == code);

    /// <summary>The language the visitor chose, kept in a cookie. English when none is chosen.</summary>
    public string Language
    {
        get
        {
            var chosen = _http.HttpContext?.Request.Cookies[CookieName];
            return chosen is not null && IsKnown(chosen) ? chosen : "en";
        }
    }

    public string this[string english]
    {
        get
        {
            var lang = Language;
            return lang != "en" && _all.TryGetValue(lang, out var texts) && texts.TryGetValue(english, out var t) && t.Length > 0 ? t : english;
        }
    }
}
