# Task 7 / POKE-58 — controlled checkpoint 1–200

Status: NEEDS_CONTEXT. This is an incomplete checkpoint, NOT completion of POKE-58 or release acceptance.
Worktree: C:/Users/Georgy/.codex/worktrees/global-update/PokeTokenBar-Windows
Branch: codex/global-update
BASE: f86dde2f55edc38aeb015a12793526ab7bc14637
HEAD / checkpoint commit: fe82d6fb766e9da939d64c8c604d377bd9e061f4
Commit: feat(facts): checkpoint offline Russian facts for species 1-200

## Exact coverage and next work

- Completed, individually prepared and paired-source self-reviewed: every ID 1–200.
- Runtime Russian facts: 200 unique species; 189 new editorial Russian rows plus all 11 accepted rows.
- Missing Russian facts: 825, IDs 201–1025. Do NOT translate or redo 1–200 during continuation.
- Genuine English selections exist for all 1025 IDs. This proves source availability/identity only; selected rows 201–1025 still require actual reading, base-form checking, editorial Russian preparation and self-review.
- Complete dataset/packaging tests intentionally remain RED. No fallback, type/family/rarity template, invented filler or official-translation claim was added.
- Parent explicitly directed a controlled continuation at this natural checkpoint, with one whole-feature review only after 1025. No worker subagents or per-batch reviewers were used.

## Committed continuation artifacts / schema

All paths below are relative to the worktree above and are committed in this checkpoint:

- docs/design-v2/catalog-package/prepare-russian-facts.py — bounded source display / paired review, genuine CSV identity/raw SHA checks, Russian batch assembly, manifest update, RuntimePackage.props generation, structural checks and release coverage gate.
- docs/design-v2/catalog-package/russian-facts-source-selections.json — schemaVersion1; 1025 selected source records. Each has speciesId, sourceLanguage=en, sourceLanguageId=9, sourceVersionId, sourceVersionUrl, sourceUrl, exact sourceRawText and sourceRawTextSha256. This is preparation provenance, not translation coverage.
- docs/design-v2/catalog-package/facts-ru/0001-0050.json, 0051-0100.json, 0101-0150.json, 0151-0200.json — sourceFileSha256, idRange, selfReview notes, and new {speciesId,textRu} records. Approved IDs are omitted here and copied from the unchanged accepted artifact.
- docs/design-v2/catalog-package/russian-facts.json — schemaVersion1; curationLanguage=ru; officialRussianTranslation=false; runtimeDatasetVersion=editorial-ru-2026-10-19-v1; source/approved SHA; russianSpecies=200; INCOMPLETE scope; facts contain explicit Russian text and preserved source display text/version/language/URLs.
- docs/design-v2/catalog-package/russian-facts-provenance.json — runtime sidecar for the 200 prepared species, with exact raw CSV text and per-row UTF-8 SHA.
- docs/design-v2/catalog-package/russian-facts-checks.json — current 200/1025 counts, exact completed ranges, dataset/provenance/batch SHA and explicit INCOMPLETE status.
- docs/design-v2/final/runtime-package.json; src/PokeCorner.App/RuntimePackage.props — both dataset and provenance are real production Content items, not review-only data.
- src/PokeCorner.App/PokeCorner.App.csproj; Popup/CollectionPresentation.cs; Popup/CollectionViewModel.cs — reader uses one lazy offline package load; no per-card network. Missing/malformed dataset yields no fact. Ordinary/shiny exact cards share the species fact; hidden forms remain masked. Discovered species presentation entries now use editorial facts instead of the legacy catalog service summary. Existing curated JSON file remains on disk unchanged; its old separate embedded-resource item was removed.
- tests/PokeCorner.App.Tests/GlobalUpdate/russianFactsTests.cs — full coverage, manifest/hash/provenance, hidden selected form/search/detail, normal/shiny equality and nine missing/malformed trust-boundary cases.

## Source fidelity / approved 11 preservation

CSV: docs/design-v2/catalog-package/source/pokemon_species_flavor_text.csv
CSV SHA256: 059b24f07ccce256df1295b5d2fa19b82129cb6d5cfdf6a83455b6dffbcc5e9b
Accepted artifact: src/PokeCorner.App/Assets/UiSystem/curated-russian-facts.json
Accepted artifact SHA256: c00ea0289dd4f411faca017463d64931b3121867dcfc4f85a0d72cf2dd3ef3f8
Source selections SHA256: 3c0995ebb11ec9377398ee65110c3895ac4640e45a2f1c8c01342e06903f0316

The accepted artifact was never edited and its full bytes are pinned by the preparation script. All 11 accepted objects, Russian text, sourceText, versions/language/URLs and translation flag remain equal in the generated dataset. Raw-source equality is checked separately from accepted display normalization:

- #43 v4: CSV has moon\u00ad light; accepted sourceText has moon light.
- #133 v4: CSV has com\u00ad position and envi\u00ad ronment; accepted sourceText omits U+00AD.
- #152 v5: CSV has hu\u00ad midity and tem\u00ad perature; accepted sourceText omits U+00AD.
- Display matching removes U+00AD and collapses whitespace only. It does NOT claim raw equality with the normalized accepted sourceText.
- csv.DictReader reads with newline='' to retain CR/LF/form-feed/soft-hyphen in sourceRawText. Runtime provenance and full preparation selections retain those exact decoded field values, with SHA256 of their UTF-8 bytes. Every selected species/version/rawtext tuple matches a genuine language9 CSV row.

Regional-form source trap: Legends: Arceus version39 flavor rows for older species can describe Hisuian variants (#58/#59 observed). Default selection excludes v39 for IDs below899 while keeping approved selections unchanged. This is only a guard, not proof that every other version is base-form safe. Every row 1–200 was read, including known Sun/Moon/SwordShield regional risks. #26/#27/#37/#38/#52/#77/#78/#83/#110 etc describe the base form; #110 explicitly compares it with Galarian Weezing. #124/#128 refer to a regional population of a species without a distinct regional form. For #35 the explicit script override selects genuine v33 full-moon dancing, replacing v34 generic popularity. Later rows still require the same manual source scrutiny and explicit source override if necessary. Keep overrides reproducible in SOURCE_VERSION_OVERRIDES; do not accidentally erase them with an unmodified --select.

## Packaging proof at this checkpoint

Dataset: 125203 bytes, SHA256 1537d4af5955504c8790efacd65545e0afac512c5cef848ca4bf9ac2629650a9
Runtime provenance: 103259 bytes, SHA256 752ee3dc00ab233fe5924703d940d4eab72d5a4a3a3ef85b0848ed8359dc4874

Both actual copies under src/PokeCorner.App/bin/Debug/net8.0-windows/Data/catalog-package/ and tests/PokeCorner.App.Tests/bin/Debug/net8.0-windows/Data/catalog-package/ match these source bytes exactly. Both are allowlisted in runtime-package.json and generated RuntimePackage.props; CatalogTests.ShippedPackageCounts/BuildMappingMatchesManifest pass with the additions. Raw proof file: .superpowers/sdd/2026-10-17-approved-ui-transfer/task-7-checkpoint-200-package-proof.json. This proves packaging of 200 prepared facts, not 1025 coverage.

## Commands / actual RED and checkpoint verification

Every shell command used rtk; worktree writes/build/git used scoped escalation. Full raw logs are in .superpowers/sdd/2026-10-17-approved-ui-transfer/ beside this report. No failed log was overwritten. Commands below omit only redirection to the separately named log.

1. Meaningful initial RED, before dataset/reader/manifest production changes:
   rtk proxy dotnet test tests/PokeCorner.App.Tests/PokeCorner.App.Tests.csproj --filter FullyQualifiedName~RussianFactsTests --logger 'console;verbosity=normal'
   task-7-red.log: exit1, 2/4 passed. Both hidden ordinary/shiny cases pass; Russian coverage fails with1014 missing and manifest dataset/provenance absent.

2. First implemented partial checkpoint at152 Russian rows:
   rtk proxy dotnet test tests/PokeCorner.App.Tests/PokeCorner.App.Tests.csproj --filter 'FullyQualifiedName~RussianFactsTests|FullyQualifiedName~CatalogTests|FullyQualifiedName~ApprovedCollectionTests|FullyQualifiedName~PokedexTests' --logger 'console;verbosity=normal'
   task-7-checkpoint-focused.log: exit1, 64/66 passed. Nine malformed/missing dataset cases, selected-form masking, normal/shiny facts, catalog package/hash/map and existing collection behavior pass. Only two mandatory1025 coverage assertions remain RED.

3. Additional meaningful shared-VM-binding RED BEFORE its production one-line fix:
   rtk proxy dotnet test tests/PokeCorner.App.Tests/PokeCorner.App.Tests.csproj --filter FullyQualifiedName~HiddenSelectedFormDoesNotRevealFactAndOwnedFormsShareIt --logger 'console;verbosity=normal'
   task-7-binding-red.log: exit1, 0/2. Actual discovered entry DescriptionRu was 'Служебная сводка типов' instead of the prepared fact for both owned-form cases.

4. Covering checkpoint verification AFTER shared binding correction and reviewed200-row data:
   Same covering command as2.
   task-7-checkpoint-200-focused.log: exit1, 64/66 passed. Both binding/masking cases are now GREEN; all nine malformed/missing cases and all existing Catalog/ApprovedCollection/Pokedex cases pass. The only failures are RussianFactsTests.EveryCatalogSpeciesHasRussianFactOffline (825 missing) and RussianFactsTests.DataAndExactSourceProvenanceShipThroughManifest (expects IDs1–1025, gets1–200). Those assertions were not changed, excluded or weakened.

5. Preparation / exact-source / packaging checks:
   rtk proxy python -X utf8 docs/design-v2/catalog-package/prepare-russian-facts.py --allow-partial
   task-7-checkpoint-build.log: exit0, initial152-row checkpoint,2749 runtime files.
   task-7-checkpoint-200-build.log: exit0, first200-row checkpoint.
   task-7-checkpoint-200-reviewed-build.log: exit0, final reviewed200-row checkpoint; no full-coverage claim.
   rtk proxy python -X utf8 docs/design-v2/catalog-package/prepare-russian-facts.py --check --allow-partial
   task-7-checkpoint-200-check.log: exit0; later task-7-checkpoint-200-final-check.log exit0 after strengthening approved-selection/range guards.
   rtk proxy python -X utf8 docs/design-v2/catalog-package/prepare-russian-facts.py --check
   task-7-release-coverage-red.log: exit1, correctly rejects Russian coverage200/1025. Default generation/check does not silently accept partial coverage.
   rtk git diff --check and rtk git diff --cached --check: exit0 before commit.

No final App gate yet: the task requires complete1025 data first. No Core gate: Core production is untouched. Task8's known capture harness/Catch ordering/hero timing evidence remains unchanged; no broad test rerun or assertion exclusion was used.

## Editorial self-review

All actual prepared source/Russian pairs were read in bounded ranges1–50,51–100,101–150,151–200; the output-truncated middle115–141 was reread separately. Four final corrections were reread against actual generated source/text:
#6 tightened4600ft altitude conversion to about1400m; #28 removed an implication that the receiving Pokémon were the same species; #35 selected actualv33 full-moon observation instead of generic popularity; #155 removed unsupported full extinguishing (source states irregular sputtering/incomplete combustion). Earlier regional-source changes rewrote nine first-batch rows. #53/#80 avoided unnecessary unverified species aliases; #64 retains forehead energy storage without growth/economy instructions; #75 now means rolling over obstacles.

Rumors/uncertainty remain explicitly attributed. Text is usually one or two connected sentences. Structural tests are not presented as translation certification. Longest NEW fact at this checkpoint: ID156 (Quilava),155 Unicode characters, sourcev26:
«Он держит врагов на расстоянии жарким пламенем и потоками раскалённого воздуха. Ловкость позволяет ему уклоняться от ударов, продолжая обжигать противника.»
Task8 should include the eventual longest row in its existing general visual comparison; no additional UI review was created here.

## Controlled continuation procedure

1. Resume this exact checkpoint HEAD in the same approved checkout/branch. Read this report and brief. Keep all1–200 batches, approved input and current code; no repeated implementation/review for them.
2. For each next bounded range (first201–250):
   rtk proxy python -X utf8 docs/design-v2/catalog-package/prepare-russian-facts.py --show 201 250
   Read actual source rows and verify base-form identity; use genuine overrides when necessary. Write explicit editorial Russian rows to facts-ru/0201-0250.json with the same sourceFileSha256/idRange/selfReview/facts schema, excluding any already-approved IDs (none remain after200).
3. Checkpoint only with explicit --allow-partial; run --review START END in ranges of at most50 after each build. Scripts never translate automatically. Store exact further completed ranges and checks in this report; if another context is needed, return NEEDS_CONTEXT before losing range state.
4. At1025, run default build then --check without --allow-partial; inspect actual output, complete source/provenance/approved preservation, and packaged bytes. The two mandatory full-coverage assertions must then go GREEN. Update the obsolete Task2B assertion in approvedCollectionTests.cs that currently expects Fact(1025)==null to expect prepared fact instead; retain invalid-ID null coverage.
5. Only after complete data/source/self-review run the appropriate focused facts/catalog/collection tests, then ONE final App gate with process-only temporary POKE_SHELL_CAPTURE_DIR using the Task6 runner/report. Keep concrete Task8 legacy failures visible; no weakening/exclusion or duplicate full gate. Core gate only if later production Core changes are actually justified.
6. Self-review owned changes, commit only owned paths and extend this report with exact final head/hashes/tests/concerns. Root owns docs/Kanban and independent whole-feature review / POKE-58 completion.

## Boundaries / concerns

POKE-58 remains OPEN,200/1025;825 translations/source-form reviews and full final acceptance are still required. Full-App/Core gates were deliberately not run at this partial checkpoint. No UI/economy/save/discovery/acquisition redesign, actual profile/app/MSI/OS/autostart changes, new art, network-per-card, subagents/reviewers, push or release. This report and raw logs are local ignored ledger artifacts; all15 implementation/data/test paths are committed.

## Continuation checkpoint 400 (20 October, before further editing)

Status remains NEEDS_CONTEXT / INCOMPLETE. Original Task7 BASE f86dde2f55edc38aeb015a12793526ab7bc14637; continuation started at fe82d6fb766e9da939d64c8c604d377bd9e061f4, current HEAD still that commit. New batches0201-0250/0251-0300/0301-0350/0351-0400 and generated data are currently uncommitted.

Completed each new actual English/Russian pair in bounded --show and --review ranges201-250,251-300,301-350,351-400. Coverage is exactly400/1025, missing625 IDs401-1025. Paired review corrected #263 to retain likely; #289 changed over1metre to over90cm because source says over1yard; #383 replaced Primal-focused v26 with actual baseline v9 sleep-in-magma/volcano mythology. SOURCE_VERSION_OVERRIDES now reproducibly retains {35:33,383:9}; --select was run and no accepted/checkpoint source changed. #383 corrected pair reread with --review383383. Other four source/Russian ranges read in full, no truncated output.

Commands (all rtk proxy python -X utf8 docs/design-v2/catalog-package/prepare-russian-facts.py): --select exit0; --allow-partial exit0 at250,300,350,400; --check --allow-partial exit0 at final400, raw log task-7-checkpoint-400-check.log. No per-batch whole test suites or reviewers. Existing two full-coverage RED assertions remain; no App/Core gate at partial coverage.

task-7-checkpoint-400-proof.json records exact source bytes/SHA and assertion that all original200 generated fact objects equal fe82d6f. Dataset256765bytes SHAd9e6aa10e1eef44752de07ee06e21d7a94da07f02fd42c81d8967388b0b4e8a3; provenance209731bytes SHA32afc94ad04d1353339bfe4d1d4ffcfd4c71e2f171120dc87d95b85d66358ccc. Selections SHA83ca46bc1b7c9bfb9cf015344e1af3de6cefd4619c58e52882990100f66b697a; CSV SHAunchanged059b24f07ccce256df1295b5d2fa19b82129cb6d5cfdf6a83455b6dffbcc5e9b; approved7470bytes SHAc00ea0289dd4f411faca017463d64931b3121867dcfc4f85a0d72cf2dd3ef3f8. Longest overall remains#156,155Unicodecharacters. No new binaries built/copied, so prior200 packaged binaries are stale and NOT asserted400 coverage.

Resume401 onward; no redo1-400. Source401-450 already read once but no Russian batch yet. Source #439 describes the same baseline MimeJr observing MrRime (MimeJr has no regional alternate); #413/#421 are species-wide cloak/petal descriptions.

## LATEST controlled checkpoint 800 (20 October 2026)

Status: NEEDS_CONTEXT. POKE-58 remains incomplete, NOT release acceptance.
Original Task7 BASE remains f86dde2f55edc38aeb015a12793526ab7bc14637 across ALL Task7 commits. This continuation starts at fe82d6fb766e9da939d64c8c604d377bd9e061f4 in the same worktree C:/Users/Georgy/.codex/worktrees/global-update/PokeTokenBar-Windows, branch codex/global-update.

### Exact completed work / resume point

Every ID1-800 is explicitly prepared and source-paired self-reviewed. This continuation adds600 new facts, IDs201-800, in twelve bounded50-row batches0201-0250 through0751-0800. Existing1-200 were not rewritten. Missing225 IDs801-1025 have genuine English selections but NO Russian editorial coverage. Start at --show801850; do not redo1-800 or repeat reader/manifest/UI implementation.

Each actual English row was read via --show START END and every Russian/source pair via --review START END. No truncated range was accepted. Final bounded review logs task-7-review-201-250.log,251-300,301-350,351-400,401-450,451-500,501-550,551-600,601-650,651-700,701-750,751-800 (same task-7-review- prefix/.log suffix) each exit0. Twelve files contain exact post-edit pairs; structural checks are NOT editorial certification.

Key self-review fixes: #263 restores likely; #289 avoids rounding a greater-than-one-yard lower bound UP to1m; #383 baseline v9 volcano legend; #404 voltage phrasing; #460 brings rather than foretells blizzards; #491 removed unsupported most-active comparison; #492 baseline v25 detoxification; #532 v33 construction instead of numeric evolution readiness; #663 approximation phrasing; #741 literal red-nectar observation; #748 symptoms still present3days AFTER onset; #754 pronoun; #761 under attack rather than attacker; #776 tautology. All corrected actual pairs were reread individually. Approximate Fahrenheit/feet/mph conversions preserve magnitudes/qualifiers; rumors, theories and unknowns remain attributed.

Source form review: #211/#215/#222/#263/#264/#503/#549/#554/#555/#562/#570/#571/#618/#628/#705/#706/#713/#724 baseline confirmed. Regional populations of species WITHOUT distinct regional alternate forms (#568/#747) and other-species regional references (#439/#459) do not change fact identity. #678 refers explicitly to male baseline. #741red-nectar Baile baseline, #745swift daytime baseline, #774exterior shell and #800light-seeking baseline. #718 is explicitly a reported conditional stronger-form ability, not an unconditional alternate-form description.

Reproducible SOURCE_VERSION_OVERRIDES now {35:33,383:9,492:25,532:33,720:24,795:28,797:28,798:28}. Reasons:35prior accepted checkpoint;383avoid Primal focus;492avoid Sky transition focus;532avoid numeric evolution rule;720v26 explicitly six-armed Unbound -> v24 baseline loop;795/797/798v30 identical generic UltraBeast filler -> genuine v28 no-touch hypothesis/gas forest fire/steel tower cut observations. --select preserves these and all11 approved choices; selection diff vsfe82d6f contains EXACTLY IDs383,492,532,720,795,797,798. Selected rows801+ still require actual reading and semantic/form checks.

### Structural check / manifest aggregate correction

Existing helper updated only for overrides plus a real aggregate defect found during self-review: runtime manifest totalBytes remained280352232 while sum(files.bytes) was281277646 at800. Existing source/hash row checks did not detect this. Added recomputation in normal build and one assertion in existing --check path, no wrapper/new script/dependency.

Meaningful RED before correction of the stale manifest:
rtk proxy python -X utf8 docs/design-v2/catalog-package/prepare-russian-facts.py --check --allow-partial
task-7-manifest-total-red.log exit1, AssertionError: Manifest totalBytes stale.
GREEN after existing normal generation recomputed total:
rtk proxy python -X utf8 docs/design-v2/catalog-package/prepare-russian-facts.py --allow-partial
task-7-checkpoint-800-build-final.log exit0,2749 runtime files,800 Russian,225missing,11approved.
rtk proxy python -X utf8 docs/design-v2/catalog-package/prepare-russian-facts.py --check --allow-partial
task-7-checkpoint-800-check-final.log exit0; exact rawsource/hash/dataset/provenance/manifest total checks pass. Earlier800 build/check logs are retained separately, not overwritten.
rtk proxy python -X utf8 docs/design-v2/catalog-package/prepare-russian-facts.py --check
task-7-checkpoint-800-release-red.log exit1, correctly rejects800/1025 Russian coverage; no --allow-partial by default. The mandatory1025 test assertions remain unweakened.

No .NET suites/App/Core gates were rerun at partial data, as directed. No Core production change. The obsolete Fact(1025)==null assertion is deliberately still pending fullcoverage. No assertions excluded, sleep/exclusion/capture changes, actual app/profile/MSI/OS actions, push/release/subagents/reviewers/newart.

### Byte/source proof at800

task-7-checkpoint-800-proof.json independently asserts all original200 generated fact objects equalfe82d6f, and all four original batch files SHA match their recorded checkpoint hashes (byte-level, including local newline form). A preliminary raw git-show-vs-worktree byte comparison only differed by Git LF vs Windows CRLF; that was resolved by comparing the recorded raw checkpoint SHA, without editing1-200.
Accepted11 artifact remains7470bytes SHAc00ea0289dd4f411faca017463d64931b3121867dcfc4f85a0d72cf2dd3ef3f8; build pins full artifact bytes and exact accepted11objects/versions/text, with rawsource proof separately retaining the three approved soft-hyphen display normalization cases.
CSV unchanged SHA059b24f07ccce256df1295b5d2fa19b82129cb6d5cfdf6a83455b6dffbcc5e9b.
Selections1025 SHAed23e5b672e1e46b1afb3f6887527c5f47703aa4dbe75cd94476b5f26e4ff535.
Dataset800:508143bytes SHAf2a65851b9f986134d87a361aa841239fbd68331d1316fcae9375932e8c3ca2d.
Runtime provenance800:417271bytes SHAb38cba14b7ed616d91be5c580b5373dd073c4545e4283de8f467bb0df14c5e77.
Manifest2749files total281277646bytes. Actual source dataset/provenance bytes/size/SHA match both allowlisted production manifest entries; existing RuntimePackage.props regenerated identically. No binary build/copy was done at partial data, so bin/test copies still represent prior200 checkpoint and MUST NOT be claimed800 proof. Full1025 final build must verify actual copied bytes.

Longest NEW and overall prepared fact:ID689(Barbaracle),sourcev34,157Unicodecharacters:
«Глаза на каждой ладони позволяют ему следить за всеми направлениями. В опасности конечности начинают действовать самостоятельно, добиваясь победы над врагом.»
Task8 should include eventual longest row in its existing visual comparison; no extra visual review requested.

### Remaining continuation / acceptance

Resume801-1025 in same checkout after checkpoint commit below. Read actual sources at most50 per call, write explicit Russian, check baseline/uncertainty and use genuine reproducible overrides where necessary. Preserve all1-800 accepted text unless a concrete defect is identified; preserve approved11bytes/objects/sources, raw CSV proof, source versions/overrides. At actual1025 run default helper build and --check withoutpartial, update ONLY obsolete Fact(1025)==null expectation to actual fact retaining invalid-ID null coverage; focused facts/catalog/collection tests then ONE final App gate with process-only temporary POKE_SHELL_CAPTURE_DIR using Task6runner/report. Keep known capture/Catch/hero failures visible and no broad repeats/exclusions. No Coregate unless productionCore changes. Root performs ONE whole-feature review across ALL Task7 commits from originalBASEf86dde2, then owns docs/Kanban acceptance.

Self-review: all600 source/Russian pairs, all seven changedsource records, helper diff, actual generated metadata/hashes and manifest changes inspected; git diff --check passed. Only owned data/helper/manifest paths are staged for the checkpoint. Ledger/rawlogs remain local ignored artifacts.

Checkpoint800 committed HEAD: a0572f91f3e2f58fcb5886b222c131c63269d23a
Commit: feat(facts): checkpoint Russian editorial coverage through species800
Both git diff --check and git diff --cached --check exit0; git status --short empty after commit. Exactly18owned paths committed; no root docs/Kanban or other-task edits. Latest status NEEDS_CONTEXT, resume801-1025.

## FINAL complete Task7 continuation (20 October 2026)

Status: DONE_WITH_CONCERNS for Task7 implementation/data; complete editorial coverage1-1025, with two inherited App capture-fixture failures retained for Task8. Root still owns independent whole-feature review and POKE-58/docs/Kanban acceptance.

Original Task7 BASE across ALL checkpoints remains f86dde2f55edc38aeb015a12793526ab7bc14637. This final continuation starts at verified clean a0572f91f3e2f58fcb5886b222c131c63269d23a on codex/global-update in C:/Users/Georgy/.codex/worktrees/global-update/PokeTokenBar-Windows. Final commit HEAD is appended below after the owned commit. Root review must compare originalBASE through finalHEAD, not HEAD~1.

### Coverage and owned changes

Completed remaining225IDs801-1025 in five explicit Russian editorial files: docs/design-v2/catalog-package/facts-ru/0801-0850.json,0851-0900.json,0901-0950.json,0951-1000.json,1001-1025.json. Each actual selected English source row was read through bounded --show, then every actual Russian/source pair through bounded --review; maximum50pairs per call. Inherited1-800 editorial review was not repeated or rewritten.

Final dataset docs/design-v2/catalog-package/russian-facts.json and runtime sidecar russian-facts-provenance.json contain exactly1025uniqueIDs1-1025, zero missing, genuine sourceLanguage=en/languageId9/version/URL/rawtext/UTF8SHA. russian-facts-checks.json is COMPLETE. Both default build and default --check reject partial coverage and now pass without --allow-partial. No fabricated type/family/rarity summary or English fallback was added. Both runtime files still ship through the existing RuntimePackage.props/manifest pipeline; props regenerate identically. Reader/UI/masking implementation remains inherited unchanged. Only obsolete ApprovedCollectionTests Fact(1025)==null expectation now asserts its genuine full Russian fact. Existing invalidID0/1026null tests are retained and pass.

Final reproducible overrides retain all inherited {35:33,383:9,492:25,532:33,720:24,795:28,797:28,798:28}; new {953:40,990:40,991:40,992:40,993:40,994:40,995:40,1006:40,1022:40}. Their genuine v40 alternatives were read directly from the actual CSV before selection. #953 now gives mudball construction/value rather than evolution-only text. #990-995 replace vague object references with distinctive, explicitly attributed magazine lore (alien weapon/ancient robot/athlete cyborg/robot-love offspring/UFO/future Tyranitar). #1006/#1022 retain attributed mad-scientist/modified-Terrakion lore. Actual source selection changes vscheckpoint800 are EXACTLY those9IDs; no earlier source choice changed.

Source/form self-review: #849aggressive baseline; #875iceface baseline; #888/#889unarmed legends; #892mountain baseline SingleStrike; #901personalobserverhypothesis kept; #902specieswide souls; #905legend kept; #916male baseline; #920explicit conditional Showdown ability rather than unconditional alternate; #931green baseline; #964explicit conditional hero transformation; #999chest baseline; #1017mask ability contextualized; #1024historical extinction remains hypothesis. No regional form description was silently assigned to another base species. Approximate conversions preserve inequalities/magnitude. #1005actual source is itself vague about a regional phenomenon: Russian retains its attribution and vagueness without inventing an explanation.

Final editorial corrections reread against exact assembled sources: #849preserves possible rather than certain toxins; #953selects genuine v40 mudball-value observation; #981removes unsupported former-tail wording; #1018explicitly holes in mountains. All other225new pairs were read in full, no truncated range accepted.

### Exact byte/source/package proof

Raw independent assertion artifact: .superpowers/sdd/2026-10-17-approved-ui-transfer/task-7-final-package-proof.json.
It asserts first800generated fact objects equalcheckpointa0572f9; all16prior batch files retain their recorded rawbyteSHA (thus first200rawproof retained); accepted11objects match and approved artifact stays7470bytes SHA c00ea0289dd4f411faca017463d64931b3121867dcfc4f85a0d72cf2dd3ef3f8. All1025rawCSVtuples and their UTF8SHA were independently rechecked. The three approved soft-hyphen display-normalization cases remain distinct from raw proof, unchanged.

CSV SHA059b24f07ccce256df1295b5d2fa19b82129cb6d5cfdf6a83455b6dffbcc5e9b.
Source selections1025 SHA db7e4d0670dd149f99feb267207e78319c16857e6724026251c3a772271a6726.
Dataset1025:655281bytes SHA88aaa589de061fe1943e21d36b0d2e3805f7873eb6e3801837e4978552effd21.
Provenance1025:535006bytes SHA14943b9669b1dbd877de6ded9bbac2ae4d480fc29d29281367f80dc46d07ada1.
Manifest2749files, total281542519bytes = sum(files.bytes). The checkpoint800 meaningful totalBytes RED→GREEN assertion/recomputation is retained unchanged.
Actual copies of BOTH files in src/PokeCorner.App/bin/Debug/net8.0-windows/Data/catalog-package/ AND tests/PokeCorner.App.Tests/bin/Debug/net8.0-windows/Data/catalog-package/ equal the final source bytes/SHA above and each contains1025actualrows. These are fresh fullbuild copies, not the old200checkpoint copies.

Longest overall remainsID689,157Unicodecharacters,sourcev34 (already inherited). Longest NEW ID818,152characters,sourcev34:
«Мигательные перепонки помогают ему замечать слабые места врага. Из кончиков пальцев он точно стреляет водой со скоростью в три раза выше скорости звука.»
Task8 uses eventual longest overallID689 in its existing visual comparison; no separate visual loop was created.

### Commands, raw logs, native exits

All commands ran in the approved isolated checkout with rtk; writes/build/git used scoped escalation. Raw logs retained under .superpowers/sdd/2026-10-17-approved-ui-transfer/. Python paired-review logs from Tee-Object and final native log redirection are PowerShell UTF16; independent packageproof/correctionreview are UTF8. Earlier failed/checkpoint logs remain untouched.

1. Source reading/preparation:
rtk proxy python -X utf8 docs/design-v2/catalog-package/prepare-russian-facts.py --show START END
Exact ranges801850,851900,901950,9511000,10011025; each exit0. Actual alternative CSVrows for990-995,1005/1006/1022,808/885/953 read through bounded csv.DictReader filters; English source was never treated as Russian coverage.
rtk proxy python -X utf8 docs/design-v2/catalog-package/prepare-russian-facts.py --select
Three invocations, exit0 each, all1025genuine sources and11approved choices retained.
rtk proxy python -X utf8 docs/design-v2/catalog-package/prepare-russian-facts.py --allow-partial
Only partial checkpoints850/900/950/1000; exit0. Retained build logs task-7-build-900.log,task-7-build-950.log,task-7-build-1000.log. No .NET suite perbatch.

2. All225paired reviews:
rtk proxy python -X utf8 docs/design-v2/catalog-package/prepare-russian-facts.py --review START END
task-7-review-801-850.log,task-7-review-851-900.log,task-7-review-901-950.log,task-7-review-951-1000.log,task-7-review-1001-1025.log; each exit0. Later corrected849/953/981/1018exact finalpairs and commands/exits recorded in task-7-review-corrections-final.log (four --review SID SID,each exit0). These reviewed pairs, not structural checks alone, support editorial quality.

3. Complete default generation:
rtk proxy python -X utf8 docs/design-v2/catalog-package/prepare-russian-facts.py
task-7-final-build.log exit0 at first1025; task-7-final-reviewed-build.log exit0 after final editorial corrections. Actual COMPLETE1025/1025,0missing,11approved,2749runtimefiles. No --allow-partial.
rtk proxy python -X utf8 docs/design-v2/catalog-package/prepare-russian-facts.py --check
task-7-final-check.log exit0; finalsource/provenance/hash/coverage/manifestaggregates all pass. No --allow-partial.

4. Focused fullfacts/catalog/collection/Pokedex:
rtk proxy dotnet test tests/PokeCorner.App.Tests/PokeCorner.App.Tests.csproj --filter "FullyQualifiedName~RussianFactsTests|FullyQualifiedName~CatalogTests|FullyQualifiedName~ApprovedCollectionTests|FullyQualifiedName~PokedexTests" --logger "console;verbosity=normal"
task-7-final-focused.log exit0,66/66passed,10.1847s. Both previously mandatory1025coverage RED assertions now GREEN. Nine malformed/missing trust-boundary cases, exact source/manifest, ordinary/shiny equality, discoveredbinding, maskedsearch/detail, allcatalog/collection/Pokedex tests pass. No assertion exclusion/weakening.

5. ONE final App gate, same Task6runner pattern:
rtk proxy powershell -NoProfile -Command '$env:POKE_SHELL_CAPTURE_DIR=Join-Path $env:TEMP "pokecorner-task7-captures"; rtk proxy dotnet test tests/PokeCorner.App.Tests/PokeCorner.App.Tests.csproj --logger "console;verbosity=normal" *> .superpowers/sdd/2026-10-17-approved-ui-transfer/task-7-final-app-gate.log; $task7Exit=$LASTEXITCODE; Get-Content .superpowers/sdd/2026-10-17-approved-ui-transfer/task-7-final-app-gate.log -Tail 38; exit $task7Exit'
task-7-final-app-gate.log exit1,688/690passed,2failed,40.5185s. Process-only temporary capture directory; no persistent environment mutation.
Retained failures:
- AcceptanceVisualTests.CaptureAssembledApplicationMatrix: InvalidOperationException wrapping NullReferenceException, acceptanceVisualTests.cs131 (outerline19), rawlog253-282. Existing capturefixture.
- ShellTests.FinishReviewEvidenceRendersPopulatedPokedexAndResetArea: InvalidOperationException wrapping InvalidCastException, Grid→ScrollViewer, shellTests.cs260 (outer208), rawlog461-489. Existing Settings overlay/capturefixture.
CatchTests.RealWriteRefusalPreservesCharmAndPinnedQueueForSameOperationRetry passed thisrun (rawlog712). Hero regression/animation tests passed (rawlog505,627,645,667). Those passes do not establish the cause of prior intermittent Catch/hero failures; their earlier Task6/Task8 evidence and deterministic handoff follow-up remain. No rerun, timing inflation, assertion exclusion or capturefixture modification.

No Coregate because no Coreproduction changed. No broad repeats after this oneAppgate; root explicitly directed finalization only.

### Self-review, boundaries and remaining concerns

Whole owned continuationdiff self-reviewed: five newbatch225pairedtexts, exactly9source changes, helperoverride1line, generatedcoverage/provenance/data equality, manifesttwoentries+total, and oneobsolete1025test assertion. Independent first800facts/batchbyte checks prevent accidental earlier edits. git diff --check exit0 before commit; staged check/result and finalcommit append below. Owned paths only; ledger/rawlogs ignored/local as before.

Task7 complete offline implementation/data and focused gates are done. FullAppgate remains RED on exactlytwo inherited capturefixtures for Task8. Structural checks cannot certify translation; explicit bounded source-paired editorial self-review covers all225newfacts, inherited1-800proof retained without redo. The longestfact awaits existing Task8 generalvisual comparison. Root performs one wholefeature review across originalBASEf86dde2f55edc38aeb015a12793526ab7bc14637 through finalHEAD and owns POKE-58/docs/Kanban acceptance.

No new abstraction/script/dependency, subagent/reviewer/art, reader/UI/gameplay/economy/save/discovery/acquisition/Core changes, actual profile/app/MSI/OS/autostart mutation, push/release, broadretaining/repeat suites. Only approved offline data/helper/manifest/test paths are committed.

Final owned commit HEAD:24f342a26bab65dcc83a8adb5abe58b6ef00f0e5
Commit:feat(facts): complete offline Russian facts for all 1025 species
Exactly12ownedpaths committed; git diff --check and git diff --cached --check exit0. git status --short empty after commit. Report/rawproof/rawlogs remain preserved local ignored ledger artifacts. Root whole-feature review range:f86dde2f55edc38aeb015a12793526ab7bc14637..24f342a26bab65dcc83a8adb5abe58b6ef00f0e5. Final status:DONE_WITH_CONCERNS(two inherited Appcapturefixtures,Task8visual acceptance).
