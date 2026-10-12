# Приёмка POKE-55 — 12 октября 2026

Main code `c0645d8` + `c6520a8`; fix1 `a853005`; guard fix2 `dbf3470`. Все три исходных находки и новая граница незавершённой миграции закрыты. Independent scoped rereview2 Approved; controller сверил фактический diff `a853005..dbf3470` с выводами reviewer. Последний полный прогон 596/596 относится к fix1, 99/99 профильных — к финальному guard. Аппаратный gamepad test исключён. Реальные сохранения, физический сбой и MSI не заявлены.



## save-protection-report.md

# POKE-55 save protection report

Status: DONE_WITH_CONCERNS. Base `cfaa524`; head `c6520a8604761e55431cae45cb79cb8f56353fe6`. Main implementation `c0645d8`; failed-write memory capture `c6520a8`. Diff `cfaa524..c6520a8`: 22 source/test files, 1,027 insertions, 136 deletions. `rtk git diff --check cfaa524..HEAD` passed.

## Implemented contract

- `App.OnStartup` acquires an OS-held exclusive `SaveProfileLease` before load. `AppPaths` rejects an unusable application-data path and `WindowsProfile.EnsurePersistent` rejects a temporary Windows profile. Load failures or a missing active save with recovery evidence enter recovery, never automatic onboarding. A valid empty collection stays valid. A committed v2 never silently falls back to stale v1.
- `JsonSaveStore` tracks the loaded byte baseline; it reads and validates current primary bytes before every replacement and refuses disappearance, replacement, corruption or an interrupted temp. It validates the candidate, preserves valid original and candidate checkpoints, flushes a temp file, then atomically moves it. `BackgroundSaveWriter` latches write failure and blocks stale retries. `AppState.CommitAsync` publishes only after durable commit.
- `SaveHistory` holds 20 recent checkpoints, 30 daily anchors, the initial checkpoint and immutable legacy migration evidence. Checkpoints have SHA256 and schema validation. History lists timestamp, collection count, schema and coins. `SaveMigration.RestoreHistory` validates selection, migrates legacy data and preserves rejected primary bytes. A native chooser is available at startup recovery and in Settings, with explicit confirmation. Explicit reset commits a valid starter-pending v2 and keeps history.
- On a latched primary-write failure, normal shutdown, exit/logoff, crash handler and Settings recovery capture validated live `AppState` to history only. The entry is visibly labeled as an unsaved memory snapshot, distinct from a confirmed primary checkpoint. This never retries the primary, clears the failure latch, or publishes an uncommitted candidate. The primary remains unchanged even when missing or externally corrupt.

## API integration

`App.PersistState` serves autosave. `App.CommitStateAsync` calls `AppState.CommitAsync`. `App.ShutdownApp`, `App.SaveState`, `App.OnExit` and `DispatcherUnhandledException` handle exit/crash. `App.RecoverSaveDataAndRestart` and `App.ResetSaveDataAndRestart` handle Settings actions. `JsonSaveStore`, `BackgroundSaveWriter`, `SaveHistory`, `SaveMigration` and `SaveProfileLease` implement the core contract. Exit disposes the lease before the single-instance mutex; a restart waits for the old mutex to release.

## Verification

Paths below are under `.superpowers/sdd/2026-10-11-global-update-transition/save-protection-logs/`. TRX counters establish results. Exact command text for earlier runs was not retained with their TRX files; the post-commit command below is exact.

- Core RED `save-protection-core-red.trx`: 1 passed, 5 failed. GREEN `save-protection-core-green.trx`: 6 passed.
- Other RED: `save-protection-red.trx` 0/1; `selection-red/` two 0/1 runs; `onboarding-red/` 0/1. Before the exit fix, `focused-final/` shows 74/74 Core + 87/87 App = 161/161. `final-lifecycle/` and `final-owned/` each show 74/74 Core + 103/103 App = 177/177.
- Full suite on main implementation `c0645d8`: `full/` shows 255/255 Core + 355/355 App = 610/610. The known real-controller-sensitive gamepad test was excluded. This full run predates `c6520a8`.
- Failed-write exit RED `exit-red/Georgy_MEPHPC_2026-10-12_15_24_42.trx`: 0/1. After fix, `exit-green/` shows 19/19 Core + 5/5 App = 24/24. The App TRX names `ExitAfterPrimaryWriteFailureKeepsMemoryInHistoryWithoutReplacingForeignPrimary` as passed.
- Exact command run at committed head `c6520a8`, exit 0: `rtk dotnet test PokeCorner.sln --no-restore --filter "FullyQualifiedName~SaveProtectionTests|FullyQualifiedName~ExitAfterPrimaryWriteFailureKeepsMemoryInHistoryWithoutReplacingForeignPrimary|FullyQualifiedName~PersistenceStartupTests" --logger "trx;LogFilePrefix=head-focused" --results-directory .superpowers/sdd/2026-10-11-global-update-transition/save-protection-logs/head-focused`. Output: 23 passed, 0 warnings, 2 projects. `head-focused/` TRX: 19/19 Core + 4/4 App.

## Self-review and limits

The synthetic regression commits valid v2, replaces the primary with corrupt bytes, latches an autosave failure, changes live coins, calls production `SaveState`, and confirms the foreign primary is unchanged, a visibly labeled memory checkpoint exists, and failure remains latched. Crash/exit capture checks `IsCommitting` so no failed tentative commit candidate becomes a recovery entry. Normal shutdown and Settings recovery use the same capture API.

A forced process kill, power loss before history write completes, or storage failure affecting primary and history can still lose recent memory changes. The reported installer/Chill incident cause remains unproven. No real saves, installed MSI, system setting, physical logoff or crash were tested. Chill-only POKE-56 is separate; legacy mode fields remain compatible. Four controller-owned docs remain unstaged and untouched.


## POKE-55 fix round 1 after POKE-56

Fix base `8e42790`, head `a853005`. Three review findings were fixed: same-day peak retention, retry of manifest-absent legacy staging without touching partial bytes, and catalog validation/quarantine of the displaced primary during recovery. RED 0/3, focused 97/97, full gate 596/596 (one hardware gamepad assertion excluded). Exact commands, TRX paths, self-review and remaining limits are in `save-protection-fix1-report.md`. The full gate applies to this fix tree after POKE-56, whereas the earlier 610/610 gate above applied to `c0645d8`.


## POKE-55 fix round 2: missing active v1 with partial migration

Fix base `a853005`, head `dbf3470`. Scoped rereview 1 identified a new no-manifest path: a partial `legacy-v1` directory plus missing active v1 and missing v2 returned `null`, allowing onboarding. `SaveMigration.LoadAsync` now checks for the staging directory after the v1 load and throws `SaveLoadException` before observing a fresh legacy baseline. An empty staging directory and one containing partial state bytes both fail closed; the latter bytes remain exact. The existing no-files/no-evidence case still returns `null`, intact active v1 still retries into a new backup, and completed/ambiguous manifest evidence still blocks stale automatic remigration. No POKE-56 or controller file was changed by this commit.

Exact RED command (exit 1): `rtk dotnet test PokeCorner.sln --no-restore --filter "FullyQualifiedName~MissingActiveV1WithIncompleteStagingNeverStartsNewGame" --logger "trx;LogFilePrefix=fix2-red" --results-directory .superpowers/sdd/2026-10-11-global-update-transition/save-protection-logs/fix2-red`. TRX: App 0/2 (both variants failed as expected), Core 0 discovered.

Exact targeted GREEN command (exit 0): `rtk dotnet test PokeCorner.sln --no-restore --filter "FullyQualifiedName~MissingActiveV1WithIncompleteStagingNeverStartsNewGame|FullyQualifiedName~NoFilesIsNewGameButExistingEmptySaveIsNot|FullyQualifiedName~InterruptedLegacyStagingBeforeManifestResumesFromIntactV1|FullyQualifiedName~MissingV2WithOnlyImmutableMigrationEvidenceRefusesRemigration|FullyQualifiedName~CorruptManifestNeverAllowsAutomaticStaleV1Remigration" --logger "trx;LogFilePrefix=fix2-green" --results-directory .superpowers/sdd/2026-10-11-global-update-transition/save-protection-logs/fix2-green`. TRX: App 6/6, Core 0 discovered, zero warnings.

Exact focused command (exit 0): `rtk dotnet test PokeCorner.sln --no-restore --filter "FullyQualifiedName~SaveProtectionTests|FullyQualifiedName~MigrationTests|FullyQualifiedName~PersistenceStartupTests" --logger "trx;LogFilePrefix=fix2-focused" --results-directory .superpowers/sdd/2026-10-11-global-update-transition/save-protection-logs/fix2-focused`. TRX: Core 20/20 + App 79/79 = 99/99, zero warnings. The prior full gate (596/596) was on `a853005`; this round changed only a two-line guard and the two-variant test, so the focused gate covers the changed decision boundary. `rtk git diff --check` passed before commit.

Self-review: the guard executes only when v2 load yielded no save and no completed manifest was found. It does not change valid v2 loading, intact v1 migration, explicit recovery from completed evidence, or a truly fresh profile. Existing malformed final-manifest evidence remains fail-closed per controller ruling. No real save, MSI, system setting, or physical failure was tested.


## save-protection-fix1-report.md

# POKE-55 fix round 1 report

Fix base: `8e42790` (POKE-56 Chill normalization). Fix head: `a853005` (`fix(persistence): keep daily peaks and recover incomplete migration`). Reviewed base for these findings: `cfaa524..c6520a8`. Owned commit changes three Core persistence files and two test files. POKE-56 normalization and controller-owned documents were not staged.

## Findings addressed

1. Critical same-day retention: `SaveHistory.Preserve` compares valid daily checkpoint collection counts and atomically replaces the anchor when a candidate has more individuals, or on an equal count with a later timestamp. Initial checkpoint, recent-20 pruning, daily-30 pruning, checksum validation, and memory-snapshot labeling remain. The synthetic sequence 1 -> 100 -> later 100 (more coins) -> 1 -> 40 distinct autosaves on the SAME date retains the later 100 checkpoint as the daily anchor, verifies timestamp/hash metadata, lists it, and restores it through `ReadValidated`/`RestoreBytes`.
2. Important interrupted legacy staging: `SaveMigration.LoadAsync` treats a manifest path as completed or ambiguous migration evidence and still forbids automatic stale v1 import. With no manifest, an incomplete `legacy-v1` directory cannot block an intact active v1. The retry creates a new backup subdirectory and leaves partial files untouched; its state, lookup and manifest writes now use durable temp-then-move. The before-manifest test retains exact active v1 and partial backup bytes, writes a complete new backup and v2, then verifies that deleting v2 does NOT remigrate stale v1. A malformed existing manifest fails closed because it could also be corrupted evidence of an already committed v2.
3. Important displaced-primary classification: production `SaveMigration.RestoreHistory` passes the catalog to `JsonSaveStore.RestoreBytes`. Restore validates the displaced primary with `SaveMigration.ValidateCatalog` before adding it to ordinary recoverable history; structurally valid but unknown-species or mismatched-family bytes go to exact-byte rejected quarantine. A valid selected checkpoint restores normally. Direct schema-only `RestoreBytes` calls remain available to existing Core tests; the application recovery route always supplies its catalog.

## Verification

All TRX paths below are relative to `.superpowers/sdd/2026-10-11-global-update-transition/save-protection-logs/`. Commands ran in the fix worktree with `rtk`; exit codes and counts were read from command output and TRX counters.

- RED, exit 1: `rtk dotnet test PokeCorner.sln --no-restore --filter "FullyQualifiedName~SameDayPeakSurvivesPruningAndLatestPeakWinsTie|FullyQualifiedName~InterruptedLegacyStagingBeforeManifestResumesFromIntactV1|FullyQualifiedName~RestoreQuarantinesCatalogInvalidDisplacedPrimary" --logger "trx;LogFilePrefix=fix1-red" --results-directory .superpowers/sdd/2026-10-11-global-update-transition/save-protection-logs/fix1-red`. `fix1-red/`: 0/1 Core, 0/2 App; all three expected failures.
- Initial GREEN, exit 0: same filter with `LogFilePrefix=fix1-green-final` and `.../fix1-green-final`, 3/3 passed. A later fourth boundary test covers a malformed manifest; the final focused/full runs include it.
- Focused, exit 0: `rtk dotnet test PokeCorner.sln --no-restore --filter "FullyQualifiedName~SaveProtectionTests|FullyQualifiedName~MigrationTests|FullyQualifiedName~PersistenceStartupTests" --logger "trx;LogFilePrefix=fix1-focused" --results-directory .superpowers/sdd/2026-10-11-global-update-transition/save-protection-logs/fix1-focused`. `fix1-focused/`: 20/20 Core + 77/77 App = 97/97, zero warnings.
- Full gate, exit 0: `rtk dotnet test PokeCorner.sln --no-restore --filter "FullyQualifiedName!~GamepadInputHookTests.StartStop_WithNoControllerConnected_RecordsNothingAndDisposesCleanly" --logger "trx;LogFilePrefix=fix1-full" --results-directory .superpowers/sdd/2026-10-11-global-update-transition/save-protection-logs/fix1-full`. `fix1-full/`: 243/243 Core + 353/353 App = 596/596, zero warnings. The one excluded gamepad assertion expects no input while a connected real controller can emit input; it is unrelated to save persistence. `rtk git diff --cached --check` passed before commit.

## Self-review and remaining limits

The migration decision relies on the write order: manifest is durably moved before v2 commit, and v2 history is created before primary replacement. A missing manifest proves the staging did not reach the commit step; an existing manifest blocks silent remigration even when malformed. A pre-existing malformed manifest with missing v2 has no automatic in-app recovery entry from the active v1; it remains fail-closed to avoid treating possibly stale v1 as a new game. Future manifest writes are atomic, so process interruption leaves the final manifest either absent or complete. The exact installer/Chill loss cause remains unproven; no real profile, MSI, Windows settings, power loss or physical logoff was tested.


## POKE-55 fix round 2: missing active v1 with partial migration

Fix base `a853005`, head `dbf3470`. Scoped rereview 1 identified a new no-manifest path: a partial `legacy-v1` directory plus missing active v1 and missing v2 returned `null`, allowing onboarding. `SaveMigration.LoadAsync` now checks for the staging directory after the v1 load and throws `SaveLoadException` before observing a fresh legacy baseline. An empty staging directory and one containing partial state bytes both fail closed; the latter bytes remain exact. The existing no-files/no-evidence case still returns `null`, intact active v1 still retries into a new backup, and completed/ambiguous manifest evidence still blocks stale automatic remigration. No POKE-56 or controller file was changed by this commit.

Exact RED command (exit 1): `rtk dotnet test PokeCorner.sln --no-restore --filter "FullyQualifiedName~MissingActiveV1WithIncompleteStagingNeverStartsNewGame" --logger "trx;LogFilePrefix=fix2-red" --results-directory .superpowers/sdd/2026-10-11-global-update-transition/save-protection-logs/fix2-red`. TRX: App 0/2 (both variants failed as expected), Core 0 discovered.

Exact targeted GREEN command (exit 0): `rtk dotnet test PokeCorner.sln --no-restore --filter "FullyQualifiedName~MissingActiveV1WithIncompleteStagingNeverStartsNewGame|FullyQualifiedName~NoFilesIsNewGameButExistingEmptySaveIsNot|FullyQualifiedName~InterruptedLegacyStagingBeforeManifestResumesFromIntactV1|FullyQualifiedName~MissingV2WithOnlyImmutableMigrationEvidenceRefusesRemigration|FullyQualifiedName~CorruptManifestNeverAllowsAutomaticStaleV1Remigration" --logger "trx;LogFilePrefix=fix2-green" --results-directory .superpowers/sdd/2026-10-11-global-update-transition/save-protection-logs/fix2-green`. TRX: App 6/6, Core 0 discovered, zero warnings.

Exact focused command (exit 0): `rtk dotnet test PokeCorner.sln --no-restore --filter "FullyQualifiedName~SaveProtectionTests|FullyQualifiedName~MigrationTests|FullyQualifiedName~PersistenceStartupTests" --logger "trx;LogFilePrefix=fix2-focused" --results-directory .superpowers/sdd/2026-10-11-global-update-transition/save-protection-logs/fix2-focused`. TRX: Core 20/20 + App 79/79 = 99/99, zero warnings. The prior full gate (596/596) was on `a853005`; this round changed only a two-line guard and the two-variant test, so the focused gate covers the changed decision boundary. `rtk git diff --check` passed before commit.

Self-review: the guard executes only when v2 load yielded no save and no completed manifest was found. It does not change valid v2 loading, intact v1 migration, explicit recovery from completed evidence, or a truly fresh profile. Existing malformed final-manifest evidence remains fail-closed per controller ruling. No real save, MSI, system setting, or physical failure was tested.


## save-protection-review.md

# POKE-55 task review — cfaa524..c6520a8

## Spec Compliance

- ❌ Issues found: same-day progress can age out of every retained checkpoint (SaveHistory.cs:38,139); an interrupted migration can leave intact v1 data with no selectable recovery (SaveMigration.cs:243,254-259); catalog-invalid primary bytes are promoted to normal history on restore (JsonSaveStore.cs:108-112).
- ⚠️ Cannot verify from this diff: real forced-kill, power-loss, MSI/startup, logoff and physical profile behavior. The reported installer/Chill incident cause remains unknown.

## Strengths

- The profile lease spans startup to OnExit, with fail-closed temporary-profile and APPDATA checks (App.xaml.cs:105-125,1064-1075; SaveProfileLease.cs:8-14; WindowsProfile.cs:10-25; AppPaths.cs:32-42).
- Baseline bytes are reread and validated before replacement, and latched writer failure prevents a stale queued candidate from retrying (JsonSaveStore.cs:43-54,68-85; BackgroundSaveWriter.cs:119-161). Startup requires explicit recovery when v2 history exists, and committed v2 does not silently fall back to v1 (JsonSaveStore.cs:137-146; SaveMigration.cs:235-245).
- The recovery UI confirms selection, and restore preserves unreadable primary bytes; failed-write exit capture labels live memory separately (SaveHistoryWindow.xaml.cs:16-23; JsonSaveStore.cs:103-127; App.xaml.cs:957-964,1085-1090). Head-focused TRX files show 19/19 Core and 4/4 App passing, with zero warnings.

## Issues

### Critical (Must Fix)

- SaveHistory.cs:38,51-55,139-140 — The day's first checkpoint is immutable. Example: initial/day checkpoint has 1 individual, the player reaches 100 later that day, then an accidental save has 1, followed by 21+ distinct autosaves. The 100-individual state is pruned from recent while initial and daily still hold 1. This violates the requested 100-to-1 recovery guarantee. Keep a daily anchor for the highest collection count (latest on ties), replace it atomically, and add this exact same-day test. SaveProtectionTests.cs:114-121 currently puts 100 on a new day, so it does not cover the failure.

### Important (Should Fix)

- SaveMigration.cs:243,254-259 and SaveHistory.cs:93-102,123-132 — Creating legacy-v1 before the state, lookup and manifest are complete makes an interrupted migration look like a previous completed migration. On restart v2 is absent, auto migration is refused, and the history picker cannot list the partial backup because ReadLegacy requires manifest and lookup. The intact active v1 is stranded without an in-app recovery choice. Distinguish incomplete migration staging from completed migration evidence, or expose a validated explicit recovery path for active v1 without allowing silent stale fallback after committed v2. Add an interruption test before manifest creation.
- JsonSaveStore.cs:108-112 and SaveHistory.cs:115-119 — RestoreBytes treats any structurally valid v2 JSON as a valid checkpoint. A primary with invalid catalog species fails startup in SaveMigration.ValidateCatalog, yet restore archives it as an ordinary history entry and List displays it as recoverable; selecting it fails again. Validate the displaced primary against the same catalog before adding it to normal history; quarantine rejected bytes instead.

### Minor (Nice to Have)

- None.

## Assessment

**Task quality:** Needs fixes.

**Reasoning:** The baseline, lease and failure-latch design are sound in the reviewed code, but the retention gap directly defeats the data-loss scenario POKE-55 must handle. The migration and restore classifications also need correction before this protection can be trusted.

**Checks:** Read cfaa524..c6520a8 review package and targeted callers; verified head-focused TRX counters 19 Core + 4 App passed, 0 failed/warnings. Did not rerun suites or touch real saves, Windows settings or MSI.


## save-protection-rereview1.md

# POKE-55 scoped rereview 1 — 8e42790..a853005

## Prior findings

- **ADDRESSED — same-day 100-to-1 retention.** SaveHistory.cs:32-49 now replaces a daily anchor atomically when the collection count is higher, or latest on a tie. SaveProtectionTests.cs:129-148 exercises 1 -> 100 -> 1 -> 40 distinct saves on one date, checks the surviving checkpoint and restores 100.
- **ADDRESSED — interrupted migration before final manifest with intact active v1.** SaveMigration.cs:243-260 treats a final manifest as completed/ambiguous evidence, retries from intact v1 when none exists, writes backup files through durable temp-then-move, and leaves partial files untouched. migrationTests.cs:185-205 verifies retry and subsequent refusal of stale v1. Malformed existing final manifests remain fail-closed by the controller's explicit ruling.
- **ADDRESSED — catalog-invalid displaced primary.** SaveMigration.cs:289 passes the catalog into JsonSaveStore.RestoreBytes; JsonSaveStore.cs:105-122 validates the displaced primary against it and quarantines rejected raw bytes. migrationTests.cs:276-295 checks that an unknown species is not listed as normal history. Source callers show the application restore path uses SaveMigration.RestoreHistory.

## New material breakage

### Important (Should Fix)

- SaveMigration.cs:243-247 — The relaxed no-manifest branch also applies when active v1 has disappeared. With an incomplete legacy-v1 directory (for example, a backed-up state.json but no final manifest), no v2 and no v2 history, v1.Load returns null and LoadAsync returns null. App.xaml.cs:140-149 then starts onboarding and may commit a new-game v2 despite migration evidence and preserved partial bytes. This violates the missing-save recovery guard. If any incomplete legacy staging exists and active v1 is absent, return a recovery error instead of null; add a missing-v1 variant of migrationTests.cs:185. This does not call for automatic retry of a malformed final manifest.

## Assessment

**Task quality:** Needs fixes. The three original findings are addressed, but the new no-manifest branch can misclassify a profile with migration evidence as fresh.

**Checks:** Read review-8e42790..a853005.diff and the production RestoreBytes callers. TRX counters confirm focused 20/20 Core + 77/77 App = 97/97, and full 243/243 Core + 353/353 App = 596/596, all zero warnings. No suite rerun.

**Limits:** No real saves, Windows settings, MSI, forced kill or physical logoff were tested. The reported installer/Chill incident cause remains unproven.


## save-protection-rereview2.md

# Save protection rereview 2

**Result: Approved.** The two-line guard closes the remaining onboarding path for an absent active v1 when a `legacy-v1` staging directory exists. The earlier manifest check still refuses completed migration evidence, while the new condition refuses incomplete staging before `ObserveLegacyBeforeFirstWrite` and before `LoadAsync` can return `null` for onboarding.

The boundary is appropriately narrow: an absent v1 with no `legacy-v1` directory still returns `null` for a genuinely new profile. An intact v1 with partial staging continues through the existing retry path, and a completed/stale migration manifest continues to refuse remigration. The new regression theory covers absent v1 with both an empty staging directory and a staged `state.json`; it asserts no v1/v2 is created and staged bytes remain unchanged. No source bytes are modified by the guard.

**Material regression findings: none.** The review is limited to the supplied `a853005..dbf3470` diff and the reported focused evidence (RED 0/2, GREEN 6/6, focused 99/99); the previous full run was 596 tests before this small guard. Controller tests remain a separate known exclusion/gate.

**Not reviewed:** full POKE55, catalog/UI behavior, Windows/MSI, or real-save flows. These are outside this rereview's scope.

TRX-файлы сохранены рядом в `2026-10-12-save-protection/`; исходные пути ignored scratch остаются в отчётах как история.
