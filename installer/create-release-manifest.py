"""Verify the complete offline payload and write its release hashes. Run from the repo root."""
import hashlib
import json
import xml.etree.ElementTree as ET
from pathlib import Path

repo = Path(__file__).resolve().parents[1]
publish = repo / "publish"
msi = repo / "installer/bin/Release/PokeCorner.msi"
inventory_path = repo / "docs/design-v2/final/runtime-package.json"
inventory = json.loads(inventory_path.read_text(encoding="utf-8-sig"))
version = ET.parse(repo / "installer/Product.wxs").getroot().find("{http://wixtoolset.org/schemas/v4/wxs}Package").attrib["Version"]

def digest(path):
    with path.open("rb") as source:
        return hashlib.file_digest(source, "sha256").hexdigest()

expected = {row["target"] for row in inventory["files"]} | {"catalog.json", "final/runtime-package.json"}
actual = {path.relative_to(publish / "Data").as_posix() for path in (publish / "Data").rglob("*") if path.is_file()}
assert actual == expected, f"Published Data mismatch: missing={expected - actual}; extra={actual - expected}"
for row in inventory["files"]:
    path = publish / "Data" / row["target"]
    assert path.resolve().is_relative_to((publish / "Data").resolve()), row["target"]
    assert path.stat().st_size == row["bytes"] and digest(path) == row["sha256"].lower(), row["target"]
assert digest(publish / "Data/final/runtime-package.json") == digest(inventory_path), "Published runtime inventory is stale"
assert digest(publish / "Data/catalog.json") == digest(repo / "src/PokeCorner.Core/PokeApi/OfflineStarterFallback.json"), "Frozen v1 lookup is stale"
assert {path.name for path in publish.iterdir()} == {"PokeCorner.exe", "D3DCompiler_47_cor3.dll", "PenImc_cor3.dll", "PresentationNative_cor3.dll", "vcruntime140_cor3.dll", "wpfgfx_cor3.dll", "Data"}, "Unexpected/missing offline runtime files"
assert msi.is_file(), msi
files = [path for path in publish.rglob("*") if path.is_file()] + [msi]
result = {
    "version": version, "runtime": "win-x64", "selfContained": True,
    "upgradeCode": "ABEC4F80-F721-4D76-85F5-BCFF1CCBD9B4",
    "runtimeInventorySha256": digest(inventory_path),
    "dataFiles": len(actual),
    "verificationLimit": "Hashes and compiled tables do not prove an actual Windows Installer upgrade or rollback.",
    "files": [{"path": path.relative_to(repo).as_posix(), "bytes": path.stat().st_size, "sha256": digest(path)} for path in sorted(files)],
}
output = repo / "release" / f"PokeCorner-{version}-release-manifest.json"
output.parent.mkdir(exist_ok=True)
output.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
print(f"PASS: {len(actual)} Data files; {len(files)} total release files")
print(output)
print(f"SHA256 {digest(output)}")

