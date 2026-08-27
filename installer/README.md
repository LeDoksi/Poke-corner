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

Output: `installer/bin/Release/PokeCorner.msi` (~66 MB).

`--self-contained` is deliberate: the .NET 8 runtime is bundled, so the MSI works on a
machine that has never had .NET installed. `-p:DebugType=none` keeps `.pdb` files out of
the payload.

The publish layout is pinned — `PokeCorner.exe`, five WPF native DLLs, and `Data\`.
`Product.wxs` harvests the root with a non-recursive glob and `Data\**` separately, so a
**new subfolder** appearing in the publish output would not be picked up without editing
`Product.wxs`.

## Where the app keeps its data

Everything except the executable lives in `<InstallDir>\Data\`:

| File | Origin |
|---|---|
| `Data\sprites\{id}.png` | shipped pre-seeded (643 files) |
| `Data\animated\{id}.gif` | shipped pre-seeded (638 files) |
| `Data\catalog.json` | shipped pre-seeded, linked from `OfflineStarterFallback.json` |
| `Data\state.json` | created by the app at runtime; **not** an MSI component |

Nothing is stored in `%APPDATA%`. Because the seeded sprites and catalog are already on
disk, a fresh install makes **zero network calls** on first launch.

`state.json` is written by the app, not installed by MSI, so MSI never deletes it: it
survives upgrades *and* uninstalls. After an uninstall, `<InstallDir>\Data\state.json` is
deliberately left behind — that is the save file being protected, not a bug. Delete the
folder by hand if you want it gone.

### Write permissions

`Product.wxs` grants the `Users` group full control on `Data\` via `util:PermissionEx`,
because the app runs unelevated but must write `state.json` there. The grant is scoped to
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

`src/PokeCorner.App/Data/{sprites,animated}` is committed (~28 MB). Regenerate it only if
the bundled species catalog changes; it was produced by crawling PokeAPI for every species
id reachable from `OfflineStarterFallback.json`.

## Signing

The MSI and the exe are unsigned, so SmartScreen shows "Windows protected your PC" on a
machine that has not seen the binary before (More info -> Run anyway). Fixing that needs a
paid code-signing certificate.

## License.rtf

Shown on the wizard's license page. It is a short personal-use / no-warranty notice plus
the Nintendo and PokeAPI attributions. Review it before distributing publicly.
