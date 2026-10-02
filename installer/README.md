# PokeCorner installer

Builds `PokeCorner.msi` — a per-machine (elevated) MSI that installs to
`C:\Program Files\PokeCorner`, with a folder-selection page, an optional desktop-shortcut
checkbox, a Start Menu shortcut, and an Add/Remove Programs entry.

## Prerequisites (one time)

```
dotnet tool install --global wix --version 5.0.2
wix extension add -g WixToolset.UI.wixext/5.0.2
wix extension add -g WixToolset.Util.wixext/5.0.2
```

Pin to **WiX 5**. WiX 7 refuses to run until you accept the paid Open Source
Maintenance Fee EULA; v5 has no such requirement.

## Building a release

Run both steps from the repo root, in order. The MSI harvests whatever is sitting in
`publish/`, so a stale or missing publish silently ships the wrong payload.

```
dotnet publish src/PokeCorner.App/PokeCorner.App.csproj -c Release -r win-x64 ^
    --self-contained true -p:PublishSingleFile=true -p:DebugType=none -o publish/

dotnet build installer/PokeCorner.Installer.wixproj -c Release
```

Output: `installer/bin/Release/PokeCorner.msi` (~110 MB).

`--self-contained` is deliberate: the .NET 8 runtime is bundled, so the MSI works on a
machine that has never had .NET installed. `-p:DebugType=none` keeps `.pdb` files out of
the payload.

The publish layout is pinned — `PokeCorner.exe`, five WPF native DLLs, and `Data\`.
`Product.wxs` harvests the root with a non-recursive glob and `Data\**` separately, so a
**new subfolder** appearing in the publish output would not be picked up without editing
`Product.wxs`.

## Where the app keeps its data

Shared assets — identical for every account on the machine — ship with the install in
`<InstallDir>\Data\`. Personal save progress does not; it stays per-user in `%APPDATA%`:

| File | Origin |
|---|---|
| `Data\sprites\{id}.png` | shipped pre-seeded (643 files) |
| `Data\animated\{id}.gif` | shipped pre-seeded (638 files) |
| `Data\cries\{id}.wav` | shipped pre-seeded (643 files, ~41 MB) |
| `Data\catalog.json` | shipped pre-seeded, linked from `OfflineStarterFallback.json` |
| `%APPDATA%\PokeCorner\state.json` | created by the app at runtime; **not** an MSI component |

Because the seeded sprites and catalog are already on disk, a fresh install makes **zero
network calls** on first launch.

`state.json` lives outside the package entirely, and an MSI never manages a user profile,
so it survives upgrades *and* uninstalls while `<InstallDir>` is removed completely. Each
Windows account keeps its own save rather than sharing one.

### Write permissions

`Product.wxs` grants the `Users` group full control on `Data\` via `util:PermissionEx`,
because the app runs unelevated but must cache newly fetched sprites and refresh the
catalog there. The grant is scoped to
`Data\` only — `PokeCorner.exe` and the runtime DLLs stay read-only for standard users, so
this is not a "swap the binary" escalation path. The ACEs are inheritable, so `sprites\`
and `animated\` are writable too (needed: a species missing from the seed is still fetched
and cached at runtime).

## Every release: bump the Version

In `installer/Product.wxs`, on the `<Package>` element:

```xml
Version="1.0.0.0"
```

Increase it for **every** build you hand out. `MajorUpgrade` compares versions — an
installer whose version is not higher than the installed one will refuse to install
(and a lower one reports the downgrade message).

## Never change the UpgradeCode

```
UpgradeCode="ABEC4F80-F721-4D76-85F5-BCFF1CCBD9B4"
```

This GUID is the only thing that makes a new MSI count as an *upgrade* of the installed
PokeCorner. Change it and the next installer installs side by side with the old one: two
Add/Remove Programs entries, two copies in Program Files, no automatic cleanup. It is
fixed for the life of the product.

## Regenerating the seed cache

`src/PokeCorner.App/Data/{sprites,animated,cries}` is committed (~69 MB). Regenerate it only
if the bundled species catalog changes; it was produced by crawling PokeAPI for every species
id reachable from `OfflineStarterFallback.json`.

`cries\` is by far the largest slice (~41 MB of the 69 MB) and it is the one that does **not**
shrink in the cab — 16-bit PCM compresses only about 4%, unlike the PNGs and GIFs. It is
uncompressed on purpose: PCM WAV is the one sound format that plays with no codec installed on
the target machine, and the upstream cries are Ogg Vorbis, which needs one — so they are decoded
once by the seed crawl rather than on the player's PC. If the payload ever has to shrink, the
levers in order of least audible damage are: drop to a 16 kHz sample rate (halves it), then to
8-bit samples (halves it again, audibly gritty). Both are changes to the seed crawl only —
nothing in the app parses these files, it hands the path straight to the player. (That player is
WPF's `System.Windows.Media.MediaPlayer` since the cry-volume setting; it was
`System.Media.SoundPlayer` before, which had no volume control. Both take a local PCM WAV with
no codec dependency, so the format decision here is unaffected.)

## Signing

The MSI and the exe are unsigned, so SmartScreen shows "Windows protected your PC" on a
machine that has not seen the binary before (More info -> Run anyway). Fixing that needs a
paid code-signing certificate.

## License.rtf

Shown on the wizard's license page. It is a short personal-use / no-warranty notice plus
the Nintendo and PokeAPI attributions. Review it before distributing publicly.
