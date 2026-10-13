using System.Security.Cryptography;
using System.Text.Json;

namespace PokeCorner.Core.PokeApi;

public sealed class CatalogPackage
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private readonly string _root;
    private readonly Dictionary<int, CatalogSpecies> _byId;

    public string DataRoot => _root;
    public string CatalogRevision { get; }

    public IReadOnlyList<CatalogSpecies> Species { get; }

    private CatalogPackage(string root, List<CatalogSpecies> species, string revision)
    {
        _root = root;
        CatalogRevision = revision;
        Species = species;
        _byId = species.ToDictionary(s => s.Id);
    }

    public static CatalogPackage Load(string root)
    {
        var manifest = Read<PackageManifest>(Path.Combine(root, "final", "runtime-package.json"));
        var catalog = Read<RuntimeCatalog>(Path.Combine(root, "final", "runtime-catalog.json"));
        if (manifest.SchemaVersion != 1 || catalog.SchemaVersion != 1 ||
            manifest.CatalogRevision != catalog.DataRevision || catalog.Species.Count == 0)
            throw new InvalidDataException("Catalog package version or revision mismatch.");

        var targets = new Dictionary<string, PackageFile>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in manifest.Files)
        {
            ValidateRelativePath(file.Target);
            ValidateRelativePath(file.Path);
            if (!targets.TryAdd(file.Target, file) || file.Bytes <= 0 || !ValidHash(file.Sha256))
                throw new InvalidDataException($"Invalid package file: {file.Target}");
            var path = FullPath(root, file.Target);
            if (!File.Exists(path) || new FileInfo(path).Length != file.Bytes)
                throw new InvalidDataException($"Missing or wrong-sized package file: {file.Target}");
            using var stream = File.OpenRead(path);
            if (!Convert.ToHexString(SHA256.HashData(stream)).Equals(file.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"Package SHA mismatch: {file.Target}");
        }

        var ids = new HashSet<int>();
        foreach (var species in catalog.Species)
        {
            if (species.Id <= 0 || !ids.Add(species.Id) || string.IsNullOrWhiteSpace(species.DisplayName) ||
                string.IsNullOrWhiteSpace(species.DescriptionRu) || species.Types.Count == 0)
                throw new InvalidDataException($"Invalid species record: {species.Id}");
            ValidateAsset(species.NormalAsset, species.NormalAvailable, ".gif", targets, root);
            ValidateAsset(species.ShinyAsset, species.ShinyAvailable, ".gif", targets, root);
            if (species.Voice is not null) ValidateAsset(species.Voice, true, ".wav", targets, root);
        }

        return new CatalogPackage(root, catalog.Species, catalog.DataRevision);
    }

    public bool CanObtain(int speciesId, bool isShiny) =>
        _byId.TryGetValue(speciesId, out var species) &&
        (isShiny ? species.ShinyAvailable : species.NormalAvailable);

    public bool CanIssueDirectly(int speciesId, bool isShiny) =>
        _byId.TryGetValue(speciesId, out var species) && species.Id == species.FamilyRootId && CanObtain(speciesId, isShiny);

    public string? GetAssetPath(int speciesId, bool isShiny)
    {
        if (!CanObtain(speciesId, isShiny)) return null;
        var species = _byId[speciesId];
        return FullPath(_root, (isShiny ? species.ShinyAsset : species.NormalAsset)!.Path);
    }

    public string? GetVoicePath(int speciesId) =>
        _byId.TryGetValue(speciesId, out var species) && species.Voice is not null
            ? FullPath(_root, species.Voice.Path) : null;

    public IReadOnlyDictionary<int, string> GetRouteHints()
    {
        using var document = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(_root, "location-draft.json")));
        var root = document.RootElement;
        var names = root.GetProperty("routes").EnumerateArray().ToDictionary(
            route => route.GetProperty("id").GetString()!, route => route.GetProperty("name").GetString()!);
        return root.GetProperty("species").EnumerateArray()
            .Where(species => species.GetProperty("locationIds").GetArrayLength() > 0)
            .ToDictionary(species => species.GetProperty("id").GetInt32(),
                species => names[species.GetProperty("locationIds")[0].GetString()!]);
    }

    private static T Read<T>(string path)
    {
        try { return JsonSerializer.Deserialize<T>(File.ReadAllText(path), JsonOptions) ?? throw new InvalidDataException($"Empty catalog file: {path}"); }
        catch (JsonException ex) { throw new InvalidDataException($"Invalid catalog JSON: {path}", ex); }
    }

    private static void ValidateAsset(CatalogAsset? asset, bool available, string extension,
        Dictionary<string, PackageFile> targets, string root)
    {
        if (!available)
        {
            if (asset is not null) throw new InvalidDataException("Unavailable variant has an asset.");
            return;
        }
        if (asset is null) throw new InvalidDataException("Available variant has no asset.");
        ValidateRelativePath(asset.Path);
        if (!asset.Path.EndsWith(extension, StringComparison.OrdinalIgnoreCase) ||
            !ValidHash(asset.Sha256) || !targets.TryGetValue(asset.Path, out var file) ||
            !string.Equals(asset.Sha256, file.Sha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Asset is outside the package allowlist: {asset.Path}");

        var path = FullPath(root, asset.Path);
        using var stream = File.OpenRead(path);
        Span<byte> header = stackalloc byte[12];
        if (stream.Read(header) < (extension == ".gif" ? 6 : 12) ||
            (extension == ".gif" && !(header[..6].SequenceEqual("GIF87a"u8) || header[..6].SequenceEqual("GIF89a"u8))) ||
            (extension == ".wav" && !(header[..4].SequenceEqual("RIFF"u8) && header[8..12].SequenceEqual("WAVE"u8))))
            throw new InvalidDataException($"Invalid asset format: {asset.Path}");
    }

    private static bool ValidHash(string? hash) => hash is { Length: 64 } && hash.All(Uri.IsHexDigit);

    private static void ValidateRelativePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Contains('\\') || path.StartsWith('/') ||
            path.Split('/').Any(part => part is "" or "." or ".." || part.Contains(':')))
            throw new InvalidDataException($"Unsafe package path: {path}");
    }

    private static string FullPath(string root, string relative) =>
        Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));

    private sealed class PackageManifest
    {
        public int SchemaVersion { get; set; }
        public string CatalogRevision { get; set; } = "";
        public List<PackageFile> Files { get; set; } = [];
    }
    private sealed class PackageFile
    {
        public string Path { get; set; } = "";
        public string Target { get; set; } = "";
        public long Bytes { get; set; }
        public string Sha256 { get; set; } = "";
    }
    private sealed class RuntimeCatalog
    {
        public int SchemaVersion { get; set; }
        public string DataRevision { get; set; } = "";
        public List<CatalogSpecies> Species { get; set; } = [];
    }
}

public sealed class CatalogSpecies
{
    public int Id { get; set; }
    public string DisplayName { get; set; } = "";
    public string DescriptionRu { get; set; } = "";
    public List<string> Types { get; set; } = [];
    public int FamilyRootId { get; set; }
    public string Rarity { get; set; } = "";
    public int EvolutionChainId { get; set; }
    public bool NormalAvailable { get; set; }
    public bool ShinyAvailable { get; set; }
    public CatalogAsset? NormalAsset { get; set; }
    public CatalogAsset? ShinyAsset { get; set; }
    public CatalogAsset? Voice { get; set; }
}

public sealed class CatalogAsset
{
    public string Path { get; set; } = "";
    public string Sha256 { get; set; } = "";
}
