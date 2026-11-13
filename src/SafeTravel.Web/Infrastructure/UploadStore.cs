namespace SafeTravel.Web.Infrastructure;

/// <summary>Files that staff upload (advertisement pictures). Kept outside the web root and checked by content, not only by name.</summary>
public class UploadStore(IConfiguration config, IWebHostEnvironment env)
{
    private static readonly Dictionary<string, byte[]> Signatures = new()
    {
        [".pdf"] = "%PDF"u8.ToArray(), [".png"] = [0x89, 0x50, 0x4E, 0x47], [".jpg"] = [0xFF, 0xD8, 0xFF], [".jpeg"] = [0xFF, 0xD8, 0xFF]
    };

    public string Root => Path.GetFullPath(Path.Combine(env.ContentRootPath, config["Uploads:Path"] ?? "App_Data/uploads"));

    /// <summary>Saves the file under the folder and returns its stored path, or null when the type is not allowed or the content does not match.</summary>
    public async Task<string?> SaveAsync(IFormFile file, string folder, string[] allowed, CancellationToken ct)
    {
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!allowed.Contains(ext) || !Signatures.TryGetValue(ext, out var magic)) return null;
        await using var input = file.OpenReadStream();
        var head = new byte[8];
        var read = await input.ReadAsync(head, ct);
        if (read < magic.Length || !head.AsSpan(0, magic.Length).SequenceEqual(magic)) return null;
        input.Position = 0;

        Directory.CreateDirectory(Path.Combine(Root, folder));
        var stored = Path.Combine(folder, Guid.NewGuid().ToString("N") + ext);
        await using var output = File.Create(Path.Combine(Root, stored));
        await input.CopyToAsync(output, ct);
        return stored;
    }

    /// <summary>The full path of a stored file, or null when it is missing or points outside the uploads folder.</summary>
    public string? Resolve(string? storedPath)
    {
        if (string.IsNullOrEmpty(storedPath)) return null;
        var path = Path.GetFullPath(Path.Combine(Root, storedPath));
        return path.StartsWith(Root, StringComparison.Ordinal) && File.Exists(path) ? path : null;
    }

    public void Delete(string? storedPath)
    {
        if (Resolve(storedPath) is { } path) File.Delete(path);
    }
}
