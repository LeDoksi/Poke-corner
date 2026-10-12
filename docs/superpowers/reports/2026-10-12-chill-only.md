# Приёмка POKE-56 — 12 октября 2026

Code `8e42790`, base `c6520a8`; independent review Approved, 0 Critical/Important.

# POKE-56 Chill-only report

Base `c6520a8`; implementation commit `8e42790`.

## Result

Chill is the only selectable and executable care mode. The Settings selector and SuperHardcore control, neglect warning/release/bench code, mood cloud, run-away animation, and collection availability badge/restriction were removed. Care mood still decays over 12 hours; petting, feeding, manual selection and explicit release remain. `CollectionViewModel` no longer accepts a mode argument. The only retained `CareMode.Hardcore`, `SuperHardcoreEnabled`, `RanAwayUntilUtc`, and `LastNeglectCheckUtc` source fields are legacy serialized values for valid older saves and history.

`JsonSaveStore.Deserialize` validates the raw v1/v2 data first, then normalizes the in-memory result to `CareMode.Chill`, `SuperHardcoreEnabled=false`, and null `RanAwayUntilUtc`. `SaveMigration.Migrate` normalizes its copied v1 data; `AppState.Hydrate` also enforces Chill for direct callers without changing their care-state objects. Original v1 files and immutable history bytes are unchanged. A restored v2 old-mode checkpoint remains byte-identical on disk and loads as Chill. Released individuals are not recreated. Unsupported schema and invalid enum/data continue through existing validation and fail closed.

## Verification log

- RED: `rtk dotnet test tests/PokeCorner.Core.Tests/PokeCorner.Core.Tests.csproj --filter FullyQualifiedName~Load_LegacyPunishment_NormalizesWithoutChangingOriginalBytesOrCollection --no-restore` — 0 passed, 1 failed; expected `Chill`, actual `Hardcore` at `JsonSaveStoreTests.cs:83`. TRX: `C:\Users\Georgy\AppData\Local\Temp\rtk_dotnet_testresults_1a1301f93d93fb01\Georgy_MEPHPC_2026-10-12_15_38_59.trx`.
- GREEN Core: `rtk dotnet test tests/PokeCorner.Core.Tests/PokeCorner.Core.Tests.csproj --no-restore` — 244 passed, 0 warnings (before the final deletion of two obsolete cooldown tests).
- GREEN focused App: `rtk dotnet test tests/PokeCorner.App.Tests/PokeCorner.App.Tests.csproj --no-restore --filter 'FullyQualifiedName~MigrationTests|FullyQualifiedName~CollectionViewModelTests|FullyQualifiedName~SettingsView'` — 134 passed, 0 warnings. Covers v1 migration, v1/v2 history restore, 100-individual preservation, availability with an old cooldown, and Settings opening without mode controls or writes to legacy mode values.
- Final full gate: `rtk dotnet test PokeCorner.sln --no-restore --filter 'FullyQualifiedName!~GamepadInputHookTests.StartStop_WithNoControllerConnected_RecordsNothingAndDisposesCleanly'` — 592 passed, 0 warnings across 2 projects. The excluded gamepad test depends on controller hardware and was not edited.
- `rtk git diff --check` — clean. Source scan found no executable Hardcore branch or punishment copy; old enum/DTO names occur only in compatibility data fields and normalization.

## Self-review and limits

The validation-before-normalization order preserves fail-closed save checks, while byte comparisons cover both a legacy file and a restored v2 checkpoint. Tests also retain Chill's 12-hour mood and explicit collection mechanics. This removes mode-based loss; it does not identify or prove the cause of the reported Chill save-loss incident. No real user profile, app installation, registry setting, or history-retention code was changed. Canonical docs and board updates are owned by the controller; their uncommitted edits were not staged. The known gamepad test remains unverified here.


## Независимое review

### Spec Compliance

- Spec compliant for pinned c6520a8..8e42790. Hardcore controls, warnings, punishment paths and cooldown UI removed; explicit release remains (src/PokeCorner.App/App.xaml.cs:315,338; src/PokeCorner.App/Popup/SettingsView.xaml:7; src/PokeCorner.App/Popup/CollectionViewModel.cs:170-184).
- Raw v1/v2 validation precedes in-memory Chill/false/null normalization, so invalid enum values and schemas still fail closed (src/PokeCorner.Core/Persistence/JsonSaveStore.cs:166,190-200,235-243). Migration validates and clones before normalizing; originals and immutable backups preserve raw bytes, and old v2 restored bytes load as Chill (src/PokeCorner.Core/Persistence/SaveMigration.cs:65-71,258-275,295-309).
- The 12-hour mood and pet/feed/select behavior remain (src/PokeCorner.Core/Domain/CareModeService.cs:5-15; src/PokeCorner.Core/Domain/FeedingService.cs:17-37; src/PokeCorner.App/Popup/CollectionViewModel.cs:170-183). Removed mood cloud was Hardcore-only (src/PokeCorner.App/App.xaml.cs:338); current Chill visuals do not regress.
- Existing 100-individual migration/recovery tests retain IDs, shiny state and currency; new tests cover old cooldowns, bytes, history, and Settings opening without mode writes (tests/PokeCorner.App.Tests/GlobalUpdate/migrationTests.cs:111-119,184-199,205-228,417-445; tests/PokeCorner.App.Tests/Popup/CollectionViewModelTests.cs:70-88; tests/PokeCorner.App.Tests/Theme/ThemeRenderingTests.cs:1015-1028).
- Cannot verify live real-profile behavior under task instruction. Future common soft attention timing is outside this task.

### Strengths

- Validation precedes normalization (src/PokeCorner.Core/Persistence/JsonSaveStore.cs:190-200).
- Hydrate keeps care timestamps without mutating input (src/PokeCorner.Core/AppState.cs:165-195; tests/PokeCorner.Core.Tests/AppStateTests.cs:162-173).
- Collection selection ignores old cooldowns; Settings construction leaves legacy values untouched (src/PokeCorner.App/Popup/CollectionViewModel.cs:170-183; tests/PokeCorner.App.Tests/Theme/ThemeRenderingTests.cs:1015-1028).

### Issues

#### Critical (Must Fix)
- None.

#### Important (Should Fix)
- None.

#### Minor (Nice to Have)
- tests/PokeCorner.App.Tests/Theme/ThemeRenderingTests.cs:1033 uses Hardcore as generic ComboBox test text. It is not shipped copy; a neutral label would simplify source scans.

### Assessment

**Task quality:** Approved.

**Reasoning:** The pinned diff removes executable mode consequences while preserving compatible decoding and raw recovery evidence. Implementer evidence: RED 0/1, focused App 134/134, final 592/592, zero warnings. Hardware-dependent gamepad test excluded; no suite rerun for review.
