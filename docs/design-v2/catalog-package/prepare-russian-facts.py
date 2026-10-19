"""Prepare/check editorial Russian flavor facts from the supplied offline PokeAPI CSV.
No automatic translation or fallback text. --show emits at most 50 selected source rows.
"""
import argparse
import csv
import hashlib
import json
import re
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parent
REPO = ROOT.parents[2]
SOURCE = ROOT / "source/pokemon_species_flavor_text.csv"
APPROVED = REPO / "src/PokeCorner.App/Assets/UiSystem/curated-russian-facts.json"
SELECTIONS = ROOT / "russian-facts-source-selections.json"
BATCHES = ROOT / "facts-ru"
FACTS = ROOT / "russian-facts.json"
PROVENANCE = ROOT / "russian-facts-provenance.json"
CHECKS = ROOT / "russian-facts-checks.json"
APPROVED_SHA = "c00ea0289dd4f411faca017463d64931b3121867dcfc4f85a0d72cf2dd3ef3f8"
SOURCE_VERSION_OVERRIDES = {35: 33}  # More distinctive full-moon observation than v34 popularity.
EXPECTED_SHA = "059b24f07ccce256df1295b5d2fa19b82129cb6d5cfdf6a83455b6dffbcc5e9b"

def sha(data):
    return hashlib.sha256(data).hexdigest()

def read(path):
    return json.loads(path.read_text(encoding="utf-8"))

def write(path, value):
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")

def normalized(text):
    return " ".join(text.replace("\u00ad", "").split())

def source_rows():
    assert sha(APPROVED.read_bytes()) == APPROVED_SHA, "Approved 11 artifact changed"
    assert sha(SOURCE.read_bytes()) == EXPECTED_SHA, "Source CSV changed"
    with SOURCE.open(encoding="utf-8", newline="") as stream:
        return [r for r in csv.DictReader(stream) if r["language_id"] == "9"]

def select():
    rows = source_rows()
    accepted = {r["speciesId"]: r for r in read(APPROVED)["facts"]}
    selected = {}
    for row in rows:
        sid, version = int(row["species_id"]), int(row["version_id"])
        if sid in SOURCE_VERSION_OVERRIDES and version != SOURCE_VERSION_OVERRIDES[sid]:
            continue
        if version == 39 and sid < 899 and sid not in accepted:
            continue
        if sid in accepted:
            old = accepted[sid]
            if version != old["sourceVersionId"] or normalized(row["flavor_text"]) != old["sourceText"]:
                continue
        if sid not in selected or version > selected[sid]["sourceVersionId"]:
            raw = row["flavor_text"]
            selected[sid] = dict(speciesId=sid, sourceLanguage="en", sourceLanguageId=9,
                sourceVersionId=version, sourceVersionUrl=f"https://pokeapi.co/api/v2/version/{version}/",
                sourceUrl=f"https://pokeapi.co/api/v2/pokemon-species/{sid}/",
                sourceRawText=raw, sourceRawTextSha256=sha(raw.encode("utf-8")))
    assert set(selected) == set(range(1, 1026)), "Incomplete genuine source coverage"
    write(SELECTIONS, dict(schemaVersion=1, sourceFileSha256=EXPECTED_SHA,
        selectionPolicy="Approved 11 exact versions retained; remaining species use latest supplied English version, excluding Legends: Arceus v39 for pre-Hisui species to avoid regional-form identity drift. Explicit version overrides in script retain genuine CSV identity.",
        facts=[selected[i] for i in sorted(selected)]))
    print("Selected 1025 genuine English rows; Russian coverage is separate.")

def ranges(ids):
    result = []
    for sid in sorted(ids):
        if result and sid == result[-1][1] + 1:
            result[-1][1] = sid
        else:
            result.append([sid, sid])
    return result

def build(allow_partial, check):
    rows = source_rows()
    exact = {(int(r["species_id"]), int(r["version_id"]), r["flavor_text"]) for r in rows}
    selections = read(SELECTIONS)
    assert selections["sourceFileSha256"] == EXPECTED_SHA
    sources = {r["speciesId"]: r for r in selections["facts"]}
    assert len(sources) == len(selections["facts"]) == 1025
    assert set(sources) == set(range(1, 1026))
    for sid, row in sources.items():
        assert row["sourceLanguage"] == "en" and row["sourceLanguageId"] == 9
        assert row["sourceUrl"] == f"https://pokeapi.co/api/v2/pokemon-species/{sid}/"
        assert row["sourceVersionUrl"] == f"https://pokeapi.co/api/v2/version/{row['sourceVersionId']}/"
        assert (sid, row["sourceVersionId"], row["sourceRawText"]) in exact, f"Not a source row: {sid}"
        assert row["sourceRawTextSha256"] == sha(row["sourceRawText"].encode("utf-8")), f"Raw SHA: {sid}"
    approved = read(APPROVED)
    facts = {r["speciesId"]: r.copy() for r in approved["facts"]}
    original = {sid: r.copy() for sid, r in facts.items()}
    for sid, row in original.items():
        source = sources[sid]
        assert row["sourceVersionId"] == source["sourceVersionId"], f"Approved version changed: {sid}"
        assert row["sourceText"] == normalized(source["sourceRawText"]), f"Approved normalized source mismatch: {sid}"
        assert row["sourceLanguage"] == source["sourceLanguage"] and row["sourceUrl"] == source["sourceUrl"]
    batch_paths = sorted(BATCHES.glob("*.json"))
    for path in batch_paths:
        batch = read(path)
        assert batch["sourceFileSha256"] == EXPECTED_SHA
        for row in batch["facts"]:
            sid = row["speciesId"]
            assert batch["idRange"][0] <= sid <= batch["idRange"][1], f"Batch range mismatch: {sid}"
            assert sid in sources and sid not in facts, f"Invalid/duplicate/approved ID: {sid}"
            text = row["textRu"]
            assert isinstance(text, str) and len(text.strip()) >= 20 and re.search("[А-Яа-яЁё]", text), f"Not Russian: {sid}"
            assert not re.search("[A-Za-z]|Тип:|редкость|эволюционн|перевод отсутствует|TODO", text, re.I), f"Boilerplate/English: {sid}"
            source = sources[sid]
            facts[sid] = dict(speciesId=sid, textRu=text, sourceText=normalized(source["sourceRawText"]),
                sourceLanguage="en", sourceVersionId=source["sourceVersionId"], sourceVersionUrl=source["sourceVersionUrl"],
                sourceUrl=source["sourceUrl"], officialRussianTranslation=False)
    assert all(facts[sid] == row for sid, row in original.items()), "Approved facts changed"
    assert len({r["textRu"] for r in facts.values()}) == len(facts), "Duplicate editorial text"
    assert allow_partial or len(facts) == 1025, f"Russian coverage {len(facts)}/1025; checkpoint only"
    payload = dict(schemaVersion=1, curationLanguage="ru", officialRussianTranslation=False,
        runtimeDatasetVersion="editorial-ru-2026-10-19-v1", sourceFileSha256=EXPECTED_SHA,
        approvedSubsetSha256=sha(APPROVED.read_bytes()), russianSpecies=len(facts),
        scope="Complete offline species facts" if len(facts) == 1025 else "INCOMPLETE checkpoint; POKE-58 remains open",
        facts=[facts[i] for i in sorted(facts)])
    provenance = dict(schemaVersion=1, sourceFileSha256=EXPECTED_SHA, officialRussianTranslation=False,
        facts=[sources[i] for i in sorted(facts)])
    if not check:
        write(FACTS, payload)
        write(PROVENANCE, provenance)
    else:
        assert read(FACTS) == payload, "Fact output stale"
        assert read(PROVENANCE) == provenance, "Provenance output stale"
    manifest_path = REPO / "docs/design-v2/final/runtime-package.json"
    manifest = read(manifest_path)
    for path in (FACTS, PROVENANCE):
        entry = dict(path=path.relative_to(REPO).as_posix(), target="catalog-package/" + path.name,
            bytes=path.stat().st_size, sha256=sha(path.read_bytes()))
        existing = [r for r in manifest["files"] if r["target"] == entry["target"]]
        if check:
            assert existing == [entry], f"Manifest stale: {path.name}"
        elif existing:
            existing[0].update(entry)
        else:
            manifest["files"].append(entry)
    if not check:
        write(manifest_path, manifest)
        subprocess.run(["python", str(REPO / "src/PokeCorner.App/generate-runtime-props.py")], check=True)
    summary = dict(status="COMPLETE" if len(facts) == 1025 else "INCOMPLETE", russianSpecies=len(facts),
        missingSpecies=1025-len(facts), completedIdRanges=ranges(facts),
        selectedEnglishSources=1025, approvedPreserved=11, sourceFileSha256=EXPECTED_SHA,
        datasetSha256=sha(FACTS.read_bytes()), provenanceSha256=sha(PROVENANCE.read_bytes()),
        batches=[dict(path=p.relative_to(REPO).as_posix(), sha256=sha(p.read_bytes())) for p in batch_paths],
        selfReview="See batch selfReview fields; structural validation is not editorial certification.")
    if not check:
        write(CHECKS, summary)
    print(json.dumps(summary, ensure_ascii=False))

if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--select", action="store_true")
    parser.add_argument("--show", nargs=2, type=int, metavar=("START", "END"))
    parser.add_argument("--review", nargs=2, type=int, metavar=("START", "END"))
    parser.add_argument("--check", action="store_true")
    parser.add_argument("--allow-partial", action="store_true")
    args = parser.parse_args()
    if args.select:
        select()
    elif args.show or args.review:
        start, end = args.show or args.review
        assert 1 <= start <= end <= 1025 and end-start < 50, "Read at most 50 sources per batch"
        prepared = {r["speciesId"]: r["textRu"] for r in read(FACTS)["facts"]} if args.review else {}
        for row in read(SELECTIONS)["facts"]:
            if start <= row["speciesId"] <= end:
                print(f'{row["speciesId"]}|v{row["sourceVersionId"]}|{normalized(row["sourceRawText"])}')
                if args.review:
                    print("RU: " + prepared.get(row["speciesId"], "[NOT PREPARED]"))
    else:
        build(args.allow_partial, args.check)
