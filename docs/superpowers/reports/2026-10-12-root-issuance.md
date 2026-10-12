# Приёмка POKE-54 — 12 октября 2026

Code `f488efa`, base `7855b69`; независимое task review Approved.

# POKE-54 root issuance guard

Commit: `f488efabbea79a6734b4ee9616ec14b5c1487223` on `codex/global-update`, base `7855b69`.

`CatalogPackage.CanIssueDirectly(speciesId, isShiny)` requires `species.Id == species.FamilyRootId` and variant availability. `CanObtain` remains asset-only, so evolved forms can still display and evolve. `App.GetDirectIssueChains` filters legacy chain starters for onboarding, activity catch, shop egg, and tier availability. `ValidateDirectResult` checks the output and converts unsupported new shiny results to normal without rerolling the species. `FirstRunFlow.HatchChosen` requires the catalog and checks/normalizes its direct output. `SaveMigration.ReserveGift` filters both recipe parents before ranking; existing migrated individuals and legacy paths are untouched. The canonical gift generator now emits 166 root-root candidates instead of 789. It does not alter nursery mixed recipes (1046), same-species recipes (1025), or asset flags.

Tests cover full-family baby root 172 vs Pikachu 25/Raichu 26, standalone root 201, an unavailable root 650 whose child 651 has a normal asset, unavailable shiny, two-root gift candidate table including standalone root 142, and migrated gift parent roots. Existing evolved/shiny migration assertions pass.

Verification logs:

```text
RED: dotnet test tests/PokeCorner.App.Tests/PokeCorner.App.Tests.csproj --no-restore --filter 'FullyQualifiedName~GlobalUpdate.CatalogTests|FullyQualifiedName~GlobalUpdate.MigrationTests' --verbosity quiet
exit 1: CS1061 CatalogPackage has no CanIssueDirectly.

prepare-balance.py: exit 0; giftCandidatePairs=166; nurseryPairs=1046; sameSpeciesRecipes=1025; rootOffspring=541; validatedShinySpecies=859.
check-preparation.py: exit 0; passed=true; runtimeFiles=2739; runtimeBytes=255721533; runtimeManifestSha256=81b50353b9cc765300f73766c93b3ca3f0ba5d19437f0035a2d97b8740848cd0.
Focused GREEN: 84/84.
Full solution first run: 236 core passed, 341 app passed.
Full solution final run: 236 core passed, app 340 passed / 1 failed: Input.GamepadInputHookTests.StartStop_WithNoControllerConnected_RecordsNothingAndDisposesCleanly, Assert.Equal expected 0 actual 1 at line 124. Isolated rerun reproduced expected 0 actual 1. Test polls real controller state; not touched by this commit.
Full solution excluding that one hardware-dependent assertion: exit 0; 236 core + 340 app passed.
git diff --cached --check: clean before commit.
```

Boundary for follow-up: T07 owns the full shared shiny/charm mechanics; current narrow guard preserves the selected species, but an existing catch lucky charm can still be consumed when that root's shiny asset is unavailable. No real save, cache, MSI, or system setting was touched. For future reachability/achievement work, read-only graph audit found 868 normal-asset species, 463 supported roots, 856 reachable through supported adjacent evolution edges, and 12 supported descendants blocked behind unavailable steps: 651, 652, 658, 681, 691, 727, 748, 804, 830, 858, 861, 1019. Keep asset availability separate from reachable-new-player goal eligibility. Existing owned specimens and earned snapshots remain valid.


## Независимое review

### Spec Compliance

- ✅ The bound change issues only available full-family roots for onboarding, activity catches, and shop egg hatches through `GetDirectIssueChains` and `ValidateDirectResult` (`src/PokeCorner.App/App.xaml.cs:832-839`); `HatchChosen` also rejects an invalid starter (`src/PokeCorner.App/Onboarding/FirstRunFlow.cs:13-18`). `CanIssueDirectly` checks the canonical `FamilyRootId` and requested variant while `CanObtain` remains asset-only (`src/PokeCorner.Core/PokeApi/CatalogPackage.cs:62-67`). Gift migration filters both recipe parents before ranking (`src/PokeCorner.Core/Persistence/SaveMigration.cs:1115-1122`), and the generated candidate table is restricted to roots (`docs/design-v2/final/prepare-balance.py:258`). The shipped checks retain 1046 nursery pairs and 1025 same-species recipes while gift candidates drop to 166 (`docs/design-v2/final/balance-checks.json:8-19`). Existing migrated individuals and nursery parents are not rewritten by this diff.
- ⚠️ Future encounter and nursery hatch issuance cannot be verified from this diff: those mechanics are assigned to later tasks. The existing nursery catalog already validates every outcome as a full-family root (`src/PokeCorner.Core/Domain/NurseryCatalog.cs:55-82,104-108`); future issuance must use that validated outcome. Full lucky-charm semantics are assigned to T07; the present catch/egg path can still spend a charm before an unsupported shiny roll is normalized (`src/PokeCorner.App/App.xaml.cs:675-676,808-813`).

### Strengths

- The new shared predicate makes the root and variant rule explicit without changing asset lookup (`src/PokeCorner.Core/PokeApi/CatalogPackage.cs:62-73`). Runtime gift filtering and generated data apply the same root rule (`src/PokeCorner.Core/Persistence/SaveMigration.cs:1118-1120`; `docs/design-v2/final/prepare-balance.py:258`).
- Tests pin the baby-root/available-child edge case, standalone species, variant availability, gift candidates, and migrated gift roots (`tests/PokeCorner.App.Tests/GlobalUpdate/catalogTests.cs:33-52,55-66`; `tests/PokeCorner.App.Tests/GlobalUpdate/migrationTests.cs:114-124`; `tests/PokeCorner.App.Tests/Onboarding/FirstRunFlowTests.cs:53-66`).

### Issues

#### Critical (Must Fix)

- None.

#### Important (Should Fix)

- None.

#### Minor (Nice to Have)

- `tests/PokeCorner.App.Tests/GlobalUpdate/catalogTests.cs:33-52`: the tests exercise `CanIssueDirectly` and table contents, but do not directly exercise App's private pool filter or normalization (`src/PokeCorner.App/App.xaml.cs:832-839`). A focused synthetic app-level test would protect those call sites when the later catch/encounter work changes them; this is a coverage suggestion, not a current behavioral defect.

### Assessment

**Task quality:** Approved

**Reasoning:** The bound diff implements the direct-issuance rule at each currently active creation path and keeps existing-save and nursery-parent semantics intact. Implementer-reported focused tests passed 84/84 and the full run excluding the unrelated hardware-dependent gamepad assertion passed 576/576; I did not rerun the suites. The gamepad assertion still fails on this host (expected 0, actual 1), and live WPF/save verification remains for later integration.
