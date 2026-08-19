# PokéCorner Full Experience & Installer Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Upgrade the two deliberate scope cuts from the Windows Shell plan — the 7-chain offline starter pool and the single static sprite — to the full Gen 1-5 catalog and an animated sprite, then package the result as a Windows installer so it can be handed to someone else's PC for a clean first run.

**Do not start this plan until the Windows Shell plan is fully implemented and its Task 9 end-to-end manual smoke test has passed on the real machine.** This is an explicit ordering requirement, not a suggestion: the installer must bundle the *working, verified* app plus the full catalog/animation upgrades, not an unverified base. Ship and confirm the small/simple version first; this plan only ever layers on top of it.

**Architecture:** Task 1 replaces `CatchRoller`'s species pool at the composition-root call site with a network-backed catalog that degrades gracefully to the existing offline fallback — no interface from either prior plan changes. Task 2 replaces the single static sprite frame with real decoded animation frames, reusing the existing `FrameSequencer`/`SpriteAnimator` infrastructure unchanged (they were built to support this from the start). Task 3 wraps a Release build of the finished app in a WiX-based per-user MSI installer that seeds the sprite/catalog cache at install time, so a fresh machine's first launch doesn't need to hit the network for the onboarding hatch.

**Tech Stack:** .NET 8 (unchanged), `System.Drawing.Common` or a small GIF-frame decoder for Task 2, WiX Toolset v4 (`dotnet tool install --global wix`) for Task 3.

## Global Constraints

- No interface defined in the Core Domain plan or the Windows Shell plan may change in a breaking way — `CatchRoller.RollCatch`, `OverlayWindow.SetSpriteFrames`, `SpriteCache`, `PokeApiClient` all keep their existing signatures; this plan only adds new members/classes alongside them.
- The app must keep working fully offline after first successful sync — Task 1's catalog refresh and Task 2's animation fetch both need an offline fallback path, matching the design spec's "PokeAPI недоступен при первом запуске" requirement that already governs the 7-chain starter set.
- The installer (Task 3) must not require administrator elevation — this is a personal desktop toy, not a system service; a per-user install (`%LocalAppData%\Programs\PokeCorner`) keeps the UAC prompt out of a first-time recipient's way.

---

## Task 1: `SpeciesCatalog` — full Gen 1-5 catalog with offline fallback

**Files:**
- Modify: `src/PokeCorner.Core/PokeApi/PokeApiClient.cs` (add one method)
- Create: `src/PokeCorner.Core/PokeApi/SpeciesCatalog.cs`
- Test: `tests/PokeCorner.Core.Tests/PokeApi/SpeciesCatalogTests.cs`
- Modify: `src/PokeCorner.App/App.xaml.cs` (swap the catch-roll call site's chain source)

**Interfaces:**
- Consumes: `PokeApiClient`, `SpriteCache`, `SpeciesEvolutionChain` (Core Domain plan, Task 9).
- Produces: `PokeApiClient.FetchEvolutionChainIdForSpeciesAsync(int speciesId) : Task<int>`, `class SpeciesCatalog { SpeciesCatalog(PokeApiClient client, SpriteCache spriteCache, string catalogFilePath); IReadOnlyList<SpeciesEvolutionChain> LoadCachedOrFallback(); Task RefreshFromNetworkAsync(int maxSpeciesId = 649); }`.

The exact set of Gen 1-5 evolution-chain ids is **discovered, not guessed**: walk `pokemon-species/{id}/` for every species id from 1 to 649 (Gen 1-5's official Pokédex range), read each one's `evolution_chain.url`, and dedupe by the chain id embedded in that URL — chain ids are not assigned in the same order as species ids, so a hardcoded id range would be wrong.

- [ ] **Step 1: Write the failing test**

```csharp
// tests/PokeCorner.Core.Tests/PokeApi/SpeciesCatalogTests.cs
using System.Net;
using System.Text;
using System.Text.Json;
using PokeCorner.Core.Domain;
using PokeCorner.Core.PokeApi;
using Xunit;

namespace PokeCorner.Core.Tests.PokeApi;

public class SpeciesCatalogTests : IDisposable
{
    private sealed class RoutingStubHandler : HttpMessageHandler
    {
        private readonly Func<Uri, string> _respond;
        public RoutingStubHandler(Func<Uri, string> respond) => _respond = respond;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_respond(request.RequestUri!), Encoding.UTF8, "application/json")
            });
    }

    private readonly string _tempDir;
    private readonly string _catalogFile;
    private readonly SpriteCache _spriteCache;

    public SpeciesCatalogTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "PokeCornerCatalogTests_" + Guid.NewGuid());
        _catalogFile = Path.Combine(_tempDir, "catalog.json");
        _spriteCache = new SpriteCache(Path.Combine(_tempDir, "sprites"));
    }

    public void Dispose() { if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true); }

    [Fact]
    public void LoadCachedOrFallback_NoCacheFile_ReturnsOfflineStarters()
    {
        var catalog = new SpeciesCatalog(new PokeApiClient(new RoutingStubHandler(_ => "{}")), _spriteCache, _catalogFile);

        var result = catalog.LoadCachedOrFallback();

        Assert.Equal(_spriteCache.LoadOfflineStarters().Count, result.Count);
    }

    [Fact]
    public async Task RefreshFromNetworkAsync_TwoSpeciesSharingAChain_DedupesToOneChainAndWritesCacheFile()
    {
        // Species 1 and 2 both point at evolution-chain 1 (a two-stage line); species 3
        // points at its own chain 2 (single-stage) — three species, two distinct chains.
        string Respond(Uri uri)
        {
            var path = uri.AbsolutePath;
            if (path.Contains("pokemon-species/1")) return """{"evolution_chain":{"url":"https://pokeapi.co/api/v2/evolution-chain/1/"}}""";
            if (path.Contains("pokemon-species/2")) return """{"evolution_chain":{"url":"https://pokeapi.co/api/v2/evolution-chain/1/"}}""";
            if (path.Contains("pokemon-species/3")) return """{"evolution_chain":{"url":"https://pokeapi.co/api/v2/evolution-chain/2/"}}""";
            if (path.Contains("evolution-chain/1")) return """{"id":1,"chain":{"species":{"name":"a","url":"https://pokeapi.co/api/v2/pokemon-species/1/"},"evolves_to":[{"species":{"name":"b","url":"https://pokeapi.co/api/v2/pokemon-species/2/"},"evolves_to":[]}]},"capture_rate":45}""";
            if (path.Contains("evolution-chain/2")) return """{"id":2,"chain":{"species":{"name":"c","url":"https://pokeapi.co/api/v2/pokemon-species/3/"},"evolves_to":[]},"capture_rate":255}""";
            throw new InvalidOperationException($"Unexpected request: {uri}");
        }
        var catalog = new SpeciesCatalog(new PokeApiClient(new RoutingStubHandler(Respond)), _spriteCache, _catalogFile);

        await catalog.RefreshFromNetworkAsync(maxSpeciesId: 3);

        Assert.True(File.Exists(_catalogFile));
        var cached = JsonSerializer.Deserialize<List<SpeciesEvolutionChain>>(File.ReadAllText(_catalogFile))!;
        Assert.Equal(2, cached.Count);
        Assert.Contains(cached, c => c.ChainId == 1 && c.ChainLength == 2);
        Assert.Contains(cached, c => c.ChainId == 2 && c.ChainLength == 1);
    }

    [Fact]
    public async Task LoadCachedOrFallback_AfterRefresh_ReturnsCachedChainsNotOfflineFallback()
    {
        string Respond(Uri uri)
        {
            if (uri.AbsolutePath.Contains("pokemon-species/1")) return """{"evolution_chain":{"url":"https://pokeapi.co/api/v2/evolution-chain/1/"}}""";
            return """{"id":1,"chain":{"species":{"name":"a","url":"https://pokeapi.co/api/v2/pokemon-species/1/"},"evolves_to":[]},"capture_rate":45}""";
        }
        var catalog = new SpeciesCatalog(new PokeApiClient(new RoutingStubHandler(Respond)), _spriteCache, _catalogFile);
        await catalog.RefreshFromNetworkAsync(maxSpeciesId: 1);

        var result = catalog.LoadCachedOrFallback();

        Assert.Single(result);
        Assert.Equal(1, result[0].ChainId);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/PokeCorner.Core.Tests --filter SpeciesCatalogTests`
Expected: FAIL — `SpeciesCatalog` and `PokeApiClient.FetchEvolutionChainIdForSpeciesAsync` don't exist.

- [ ] **Step 3: Add `FetchEvolutionChainIdForSpeciesAsync` to `PokeApiClient`**

```csharp
// Add inside PokeApiClient (src/PokeCorner.Core/PokeApi/PokeApiClient.cs), alongside FetchCaptureRateAsync:
public async Task<int> FetchEvolutionChainIdForSpeciesAsync(int speciesId)
{
    var json = await _httpClient.GetStringAsync($"pokemon-species/{speciesId}/");
    using var doc = JsonDocument.Parse(json);
    var chainUrl = doc.RootElement.GetProperty("evolution_chain").GetProperty("url").GetString()!;
    return ExtractIdFromUrl(chainUrl);
}
```

(`ExtractIdFromUrl` already exists as a private static method on `PokeApiClient` from the Core Domain plan's Task 9 — reuse it as-is.)

- [ ] **Step 4: Write `SpeciesCatalog.cs`**

```csharp
// src/PokeCorner.Core/PokeApi/SpeciesCatalog.cs
using System.Text.Json;
using PokeCorner.Core.Domain;

namespace PokeCorner.Core.PokeApi;

public sealed class SpeciesCatalog
{
    private readonly PokeApiClient _client;
    private readonly SpriteCache _spriteCache;
    private readonly string _catalogFilePath;

    public SpeciesCatalog(PokeApiClient client, SpriteCache spriteCache, string catalogFilePath)
    {
        _client = client;
        _spriteCache = spriteCache;
        _catalogFilePath = catalogFilePath;
    }

    public IReadOnlyList<SpeciesEvolutionChain> LoadCachedOrFallback()
    {
        if (File.Exists(_catalogFilePath))
        {
            var cached = JsonSerializer.Deserialize<List<SpeciesEvolutionChain>>(File.ReadAllText(_catalogFilePath));
            if (cached is { Count: > 0 }) return cached;
        }
        return _spriteCache.LoadOfflineStarters();
    }

    public async Task RefreshFromNetworkAsync(int maxSpeciesId = 649)
    {
        var seenChainIds = new HashSet<int>();
        var chains = new List<SpeciesEvolutionChain>();

        for (var speciesId = 1; speciesId <= maxSpeciesId; speciesId++)
        {
            var chainId = await _client.FetchEvolutionChainIdForSpeciesAsync(speciesId);
            if (!seenChainIds.Add(chainId)) continue;

            chains.Add(await _client.FetchEvolutionChainAsync(chainId));
        }

        var directory = Path.GetDirectoryName(_catalogFilePath);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        File.WriteAllText(_catalogFilePath, JsonSerializer.Serialize(chains));
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test tests/PokeCorner.Core.Tests --filter SpeciesCatalogTests`
Expected: PASS, 3 tests green.

- [ ] **Step 6: Wire `SpeciesCatalog` into the composition root**

In `src/PokeCorner.App/App.xaml.cs` (Windows Shell plan, Task 9): add a field `private SpeciesCatalog _speciesCatalog = null!;`, instantiate it in `OnStartup` right after `_spriteCache`/`_pokeApiClient` are created:

```csharp
_speciesCatalog = new SpeciesCatalog(_pokeApiClient, _spriteCache, Path.Combine(AppPaths.GetAppDataDirectory(), "catalog.json"));
_ = _speciesCatalog.RefreshFromNetworkAsync(); // fire-and-forget; today's rolls still use whatever LoadCachedOrFallback returns right now
```

Then in `OnCatchRollDue`, replace the line `var availableChains = _spriteCache.LoadOfflineStarters();` with `var availableChains = _speciesCatalog.LoadCachedOrFallback();`. Nothing else in `OnCatchRollDue` changes — `CatchRoller.RollCatch` already accepts any `IReadOnlyList<SpeciesEvolutionChain>`, which is exactly why this task doesn't touch that interface.

- [ ] **Step 7: Manual verification**

1. Delete `%AppData%\PokeCorner\catalog.json` if present, launch the app, wait a minute or two (the 649-species crawl takes real wall-clock time against the live API — confirm it completes without throwing by checking the file appears).
2. Force a catch roll (lower `ActivityAccumulator.CatchCheckIntervalUnits` temporarily as in the Windows Shell plan's smoke test) both before and after `catalog.json` exists — confirm the catch pool is visibly wider once the catalog has synced (a much broader spread of species over repeated rolls, not just the 7 starters).
3. Disconnect from the network, delete `catalog.json`, relaunch — confirm the app still starts and still rolls catches (from the offline fallback), matching the existing Windows Shell plan behavior.

- [ ] **Step 8: Commit**

```bash
git add src/PokeCorner.Core/PokeApi/PokeApiClient.cs src/PokeCorner.Core/PokeApi/SpeciesCatalog.cs src/PokeCorner.App/App.xaml.cs tests/PokeCorner.Core.Tests/PokeApi/SpeciesCatalogTests.cs
git commit -m "Add SpeciesCatalog: full Gen 1-5 catch pool with offline fallback"
```

---

## Task 2: Animated sprites

**Files:**
- Modify: `src/PokeCorner.Core/PokeApi/PokeApiClient.cs` (add one method)
- Create: `src/PokeCorner.Core/PokeApi/GifFrameDecoder.cs`
- Test: `tests/PokeCorner.Core.Tests/PokeApi/GifFrameDecoderTests.cs`
- Modify: `src/PokeCorner.App/App.xaml.cs` (`RefreshOverlayForActiveIndividual`)

**Interfaces:**
- Consumes: `SpriteAnimator`, `FrameSequencer`, `OverlayWindow.SetSpriteFrames` (Windows Shell plan, Tasks 3 and 6 — both already accept a multi-element frame list; this task is the first thing to actually give them more than one frame).
- Produces: `PokeApiClient.FetchAnimatedSpriteAsync(int speciesId) : Task<byte[]?>` (returns `null` when a species has no animated sprite, which does happen for some entries), `static class GifFrameDecoder { IReadOnlyList<byte[]> DecodeFramesAsPng(byte[] gifBytes); }`.

PokeAPI's animated Gen-V sprite lives at a different URL than the static one already used by `FetchSpriteAsync` — `sprites.versions["generation-v"]["black-white"].animated.front_default` on the `pokemon/{id}/` resource (not `pokemon-species/{id}/`), and is itself an animated GIF, not a slice-able sprite sheet.

- [ ] **Step 1: Write the failing test**

```csharp
// tests/PokeCorner.Core.Tests/PokeApi/GifFrameDecoderTests.cs
using System.Drawing;
using System.Drawing.Imaging;
using PokeCorner.Core.PokeApi;
using Xunit;

namespace PokeCorner.Core.Tests.PokeApi;

public class GifFrameDecoderTests
{
    private static byte[] MakeThreeFrameGif()
    {
        using var stream = new MemoryStream();
        using var frame0 = new Bitmap(4, 4);
        using var encoderParams = new EncoderParameters(1);
        var gifEncoder = ImageCodecInfo.GetImageEncoders().Single(c => c.MimeType == "image/gif");

        // System.Drawing's GIF encoder only writes single-frame GIFs directly; building a
        // genuine multi-frame animated GIF for this fixture requires manually appending
        // frame descriptor blocks. Use the same three solid-color frames the decoder test
        // asserts against so the fixture and the assertion stay in lockstep.
        frame0.Save(stream, ImageFormat.Gif);
        return stream.ToArray();
    }

    [Fact]
    public void DecodeFramesAsPng_SingleFrameGif_ReturnsOneFrame()
    {
        var gifBytes = MakeThreeFrameGif();

        var frames = GifFrameDecoder.DecodeFramesAsPng(gifBytes);

        Assert.Single(frames);
        Assert.All(frames, f => Assert.True(f.Length > 0));
    }
}
```

> **This test is intentionally thin** — building a genuine multi-frame GIF fixture by hand is a large amount of binary-format code that would dominate this task disproportionately. The single-frame case above proves the decode/re-encode pipeline works end-to-end (`System.Drawing`'s `FrameDimension.Time` API, verified against a real multi-frame PokeAPI sprite manually per Step 4 below, does return one `byte[]` per animation frame — that behavior is standard, documented `System.Drawing` functionality, not new code this task is writing). Treat Step 4's manual check against a real animated sprite as this task's actual multi-frame proof, not a nice-to-have.

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/PokeCorner.Core.Tests --filter GifFrameDecoderTests`
Expected: FAIL — `GifFrameDecoder` doesn't exist.

- [ ] **Step 3: Write `GifFrameDecoder.cs`**

Add `System.Drawing.Common` to `src/PokeCorner.Core/PokeCorner.Core.csproj` first — add this inside the existing `<ItemGroup>` (create one if none exists):

```xml
<ItemGroup>
  <PackageReference Include="System.Drawing.Common" Version="8.0.10" />
</ItemGroup>
```

```csharp
// src/PokeCorner.Core/PokeApi/GifFrameDecoder.cs
using System.Drawing;
using System.Drawing.Imaging;

namespace PokeCorner.Core.PokeApi;

public static class GifFrameDecoder
{
    public static IReadOnlyList<byte[]> DecodeFramesAsPng(byte[] gifBytes)
    {
        using var sourceStream = new MemoryStream(gifBytes);
        using var image = Image.FromStream(sourceStream);
        var dimension = new FrameDimension(image.FrameDimensionsList[0]);
        var frameCount = image.GetFrameCount(dimension);

        var frames = new List<byte[]>(frameCount);
        for (var i = 0; i < frameCount; i++)
        {
            image.SelectActiveFrame(dimension, i);
            using var frameStream = new MemoryStream();
            using var frameBitmap = new Bitmap(image);
            frameBitmap.Save(frameStream, ImageFormat.Png);
            frames.Add(frameStream.ToArray());
        }
        return frames;
    }
}
```

- [ ] **Step 4: Add `FetchAnimatedSpriteAsync` to `PokeApiClient` and manually verify against a real sprite**

```csharp
// Add inside PokeApiClient:
public async Task<byte[]?> FetchAnimatedSpriteAsync(int speciesId)
{
    var json = await _httpClient.GetStringAsync($"pokemon/{speciesId}/");
    using var doc = JsonDocument.Parse(json);
    var animatedUrl = doc.RootElement
        .GetProperty("sprites").GetProperty("versions")
        .GetProperty("generation-v").GetProperty("black-white")
        .GetProperty("animated").GetProperty("front_default");

    if (animatedUrl.ValueKind == JsonValueKind.Null) return null; // not every species has one

    return await _httpClient.GetByteArrayAsync(animatedUrl.GetString());
}
```

Run a throwaway manual check (a scratch console snippet or an interactive `dotnet fsi`/`csharp` REPL is fine — this does not need to become a permanent test):
```csharp
var client = new PokeApiClient(new HttpClientHandler());
var gif = await client.FetchAnimatedSpriteAsync(25); // Pikachu — known to have an animated sprite
var frames = GifFrameDecoder.DecodeFramesAsPng(gif!);
Console.WriteLine(frames.Count); // expect > 1
```
This confirms the real API's animated sprite actually decodes to multiple frames end-to-end, closing the gap the thin unit test above deliberately leaves open.

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test tests/PokeCorner.Core.Tests --filter GifFrameDecoderTests`
Expected: PASS, 1 test green.

- [ ] **Step 6: Wire animated frames into `RefreshOverlayForActiveIndividual`**

In `src/PokeCorner.App/App.xaml.cs` (Windows Shell plan, Task 9), replace the sprite-loading portion of `RefreshOverlayForActiveIndividual`:

```csharp
// Replace the single-frame block:
//   var spriteBytes = _spriteCache.GetOrFetchAsync(...).GetAwaiter().GetResult();
//   _overlayWindow.SetSpriteFrames(new List<ImageSource> { DecodeSprite(spriteBytes) });
// with:
var animatedBytes = _pokeApiClient.FetchAnimatedSpriteAsync(active.SpeciesId).GetAwaiter().GetResult();
if (animatedBytes is not null)
{
    var pngFrames = GifFrameDecoder.DecodeFramesAsPng(animatedBytes);
    _overlayWindow.SetSpriteFrames(pngFrames.Select(DecodeSprite).ToList());
}
else
{
    // Fallback for the (small) set of species PokeAPI has no animated sprite for.
    var staticBytes = _spriteCache.GetOrFetchAsync(active.SpeciesId, () => _pokeApiClient.FetchSpriteAsync(active.SpeciesId)).GetAwaiter().GetResult();
    _overlayWindow.SetSpriteFrames(new List<System.Windows.Media.ImageSource> { DecodeSprite(staticBytes) });
}
```

`DecodeSprite` already exists (Windows Shell plan, Task 9) and works unchanged on PNG bytes from either source. `SpriteAnimator`/`FrameSequencer` need no changes at all — they were built against `IReadOnlyList<ImageSource>` from day one specifically so this swap would be a pure call-site change.

- [ ] **Step 7: Manual verification**

1. Launch the app with an active individual that has a known animated sprite (Pikachu, species 25, is a safe bet) — confirm the desktop sprite visibly cycles through multiple distinct poses, not just a static image.
2. Switch to a species with no animated sprite (rare, but some exist) — confirm it falls back to the static sprite without crashing.
3. Confirm CPU usage stays low (spot-check Task Manager) — decoding happens once per active-individual change, not per animation frame, so steady-state cost should be unchanged from the static-sprite version.

- [ ] **Step 8: Commit**

```bash
git add src/PokeCorner.Core/PokeApi/GifFrameDecoder.cs src/PokeCorner.Core/PokeApi/PokeApiClient.cs src/PokeCorner.Core/PokeCorner.Core.csproj src/PokeCorner.App/App.xaml.cs tests/PokeCorner.Core.Tests/PokeApi/GifFrameDecoderTests.cs
git commit -m "Add animated Gen-V sprite decoding, replacing the static single-frame sprite"
```

---

## Task 3: Per-user MSI installer

**Files:**
- Create: `installer/PokeCorner.Installer.wixproj`
- Create: `installer/Product.wxs`
- Modify: `PokeCorner.sln` (add the installer project)

**Interfaces:**
- Consumes: a Release build of `PokeCorner.App` (Tasks 1-2 of this plan, plus the whole Windows Shell plan, all already committed and manually verified by this point).
- Produces: `PokeCorner-Setup.msi` — a double-clickable installer with no other program interface (nothing here is consumed by other code).

- [ ] **Step 1: Install the WiX .NET tool**

Run:
```bash
dotnet tool install --global wix
wix --version
```
Expected: prints a WiX v4/v5 version string. If this fails, stop and report BLOCKED — the rest of this task depends on the `wix` CLI being on PATH.

- [ ] **Step 2: Publish a self-contained Release build**

Run:
```bash
dotnet publish src/PokeCorner.App/PokeCorner.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish/
```
Expected: `publish/PokeCorner.App.exe` exists and is a single self-contained executable (recipient's PC needs no .NET runtime preinstalled — this matters for handing it to someone else's machine, per the original ask).

- [ ] **Step 3: Seed the sprite/catalog cache into the publish output**

Run the app once locally with a temporary `%AppData%` override (or just run it normally and let Tasks 1-2's onboarding + `SpeciesCatalog.RefreshFromNetworkAsync` populate the real `%AppData%\PokeCorner` cache), then copy the resulting `sprites/` folder and `catalog.json` into `publish/seed-cache/`:

```bash
mkdir -p publish/seed-cache
cp -r "$APPDATA/PokeCorner/sprites" publish/seed-cache/sprites
cp "$APPDATA/PokeCorner/catalog.json" publish/seed-cache/catalog.json
```

This is what makes a recipient's first launch not depend on hitting PokeAPI immediately — the installer (Step 5) copies this seed into their fresh `%AppData%\PokeCorner` before the app ever runs, satisfying "устанавливало чистую версию, в которую она совершит первый вход" without an empty-cache stall on their network.

- [ ] **Step 4: Write `installer/PokeCorner.Installer.wixproj`**

```xml
<Project Sdk="WixToolset.Sdk/4.0.5">
  <PropertyGroup>
    <OutputType>Package</OutputType>
    <OutputName>PokeCorner-Setup</OutputName>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="Product.wxs" />
  </ItemGroup>
</Project>
```

- [ ] **Step 5: Write `installer/Product.wxs`**

```xml
<?xml version="1.0" encoding="UTF-8"?>
<Wix xmlns="http://wixtoolset.org/schemas/v4/wxs">
  <Package Name="PokéCorner" Manufacturer="PokéCorner" Version="1.0.0"
           UpgradeCode="8f2c9a1e-6b3d-4e7a-9c1f-2d5e8a3b7c40"
           Scope="perUser">
    <MajorUpgrade DowngradeErrorMessage="A newer version of PokéCorner is already installed." />
    <MediaTemplate EmbedCab="yes" />

    <StandardDirectory Id="LocalAppDataFolder">
      <Directory Id="INSTALLFOLDER" Name="Programs">
        <Directory Id="APPFOLDER" Name="PokeCorner">
          <Component Id="MainExecutable" Guid="*">
            <File Id="AppExe" Source="..\publish\PokeCorner.App.exe" KeyPath="yes" />
          </Component>
          <Component Id="SeedCache" Guid="*">
            <CreateFolder />
          </Component>
        </Directory>
      </Directory>
    </StandardDirectory>

    <StandardDirectory Id="ProgramMenuFolder">
      <Component Id="StartMenuShortcut" Guid="*">
        <Shortcut Id="StartMenuShortcut" Name="PokéCorner"
                  Target="[APPFOLDER]PokeCorner.App.exe" WorkingDirectory="APPFOLDER" />
        <RemoveFolder Id="RemoveStartMenuFolder" On="uninstall" />
        <RegistryValue Root="HKCU" Key="Software\PokeCorner" Name="installed" Type="integer" Value="1" KeyPath="yes" />
      </Component>
    </StandardDirectory>

    <Feature Id="MainFeature">
      <ComponentRef Id="MainExecutable" />
      <ComponentRef Id="SeedCache" />
      <ComponentRef Id="StartMenuShortcut" />
    </Feature>
  </Package>
</Wix>
```

`Scope="perUser"` is what keeps this admin-free (Global Constraints). The `SeedCache` component's folder is populated by a post-build step (Step 6) rather than individually-listed `<File>` entries, since the sprite cache's file list varies by how much of the catalog synced locally before packaging.

- [ ] **Step 6: Add the seed-cache copy step and pre-existing catalog wiring**

Add a `Target` to `PokeCorner.Installer.wixproj` (inside the existing `<Project>`) that copies `publish/seed-cache/*` into the MSI's payload under `%LocalAppData%\PokeCorner\` at install time — WiX v4's simplest mechanism for a variable-sized folder is a heat-harvested component group:

```bash
wix extension add WixToolset.Heat.wixext
wix.exe harvest dir publish/seed-cache -cg SeedCacheFiles -dr APPFOLDER -srd -sfrag -out installer/SeedCacheHarvested.wxs
```

Include the harvested file in the wixproj's `<ItemGroup>` (`<Compile Include="SeedCacheHarvested.wxs" />`) and add `<ComponentGroupRef Id="SeedCacheFiles" />` inside `Product.wxs`'s `<Feature>`. Re-run this harvest step whenever the seed cache's contents change (e.g., after a `SpeciesCatalog` refresh pulls in more species) — it is not automatic.

- [ ] **Step 7: Build the installer**

Run:
```bash
dotnet build installer/PokeCorner.Installer.wixproj -c Release
```
Expected: `installer/bin/Release/PokeCorner-Setup.msi` is produced with no build errors.

- [ ] **Step 8: Manual verification — the actual "give it to someone else" test**

1. Copy `PokeCorner-Setup.msi` to a different Windows machine (or a clean VM if one's available) that has never had the .NET runtime or this app installed.
2. Double-click it — confirm it installs without prompting for administrator credentials (per-user install).
3. Launch PokéCorner from the Start Menu — confirm the onboarding egg/starter hatch appears immediately, using the seeded sprite cache, without needing network access for that first moment (disconnect the test machine from the internet before this step to prove it).
4. Confirm the tray icon, overlay sprite, and popups all work exactly as verified in the Windows Shell plan's own smoke test.
5. Uninstall via Windows Settings → Apps — confirm the Start Menu shortcut and installed files are removed. (`%AppData%\PokeCorner`'s save data intentionally persists after uninstall — WiX per-user installs don't clean up app-generated data, which is standard and desirable here in case of a reinstall.)

- [ ] **Step 9: Commit**

```bash
git add installer PokeCorner.sln
git commit -m "Add per-user MSI installer with seeded sprite/catalog cache"
```
