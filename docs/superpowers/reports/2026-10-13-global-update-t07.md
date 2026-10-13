# Приёмка T07 / POKE-45 — 2026-10-13

Независимое spec/quality review принято для code `425ba33fab938adbb086e562e80103144de8515b`. Ниже исходный отчёт реализации и доработок; старые промежуточные counts сохранены как история.

# T07 — common shiny and lucky charm

Status: source implementation and focused/full functional gates complete. `task-7-focused.log` and `task-7-gate.log` retain raw stdout, test counts and exit codes.

## Delivered

- `ShinyRules.Chance(ShinySource, nurseryLuck, charm, variantSupported)` applies 1/512 base, nursery-only accumulated bonus capped at 10% before charm, x4 active charm and 100% final cap. Unsupported variant returns zero.
- `ShinyRules.Resolve(ShinySource, ShinyDraw)` resolves a supplied draw; callers persist the selected result in their existing operation snapshot. `NextNurseryLuck` handles eligible normal +0.25 percentage points, eligible shiny reset, and unsupported no change.
- `SaveData.NurseryLuckBonus` persists the nursery bonus in the existing save. V2 validation bounds it to `0..(10%-1/512)`.
- `ShopService.TryActivateLuckyCharm(SaveData)` moves one `Items["lucky-charm"]` unit into the existing active flag. Already active cannot stack. `TryConsumeLuckyCharm(SaveData, variantSupported)` clears that flag only for an eligible selected variant. The shop button commits activation through the shared snapshot writer.
- Activity and known encounter rolls use common chance/resolve. The selected variant is checked before charm consumption. Gift properties are used as reserved; no new roll. The saved `opened` result is returned on replay. The old hidden duplicate x4 was removed, including the legacy catch roller.
- Activity preview gives the eligible chance with/without the active charm and says unsupported variants have 0% and preserve the charm.

## Review fix 1 — legacy shop eggs

The approved `final/MIGRATION.md` explicitly removes purchased `PendingEggs` during migration while retaining already hatched individuals. The earlier T05 handoff statement that legacy eggs would continue to hatch was inaccurate. The old live `HatchEgg` path, its collection button/panel/card, its tier roller and its separate live charm consumer are retired. This removes the only runtime issuance path that mutated charm, collection and egg before a durable commit. Migration still clears only pending eggs; it preserves existing individual GUIDs and shiny flags. No live collection, save or backup cleanup was added. PendingEggs serialization remains for reading legacy data and migration verification; the dormant view-model egg projection is not an issuance path.

Fix commit: `425ba33` (base T07 commit `c54d7ef`). Fix verification: RED 1 failed/1 passed (stale hatch panel visible), then focused App 2/2 passed; full solution excluding the environment-dependent screenshot capture: Core 229/229 and App 391/391 passed. The exact focused and solution commands, raw stdout, counts, and exit codes are in `task-7-fix1-*.log`. `git diff --cached --check` exited 0 before commit.

## Handoff

- T08 expedition: at explicit claim, select actual normal root first; call `ShinyRules.Chance(ShinySource.Expedition, 0, next.ShopCatchLuckyCharmActive, catalog.CanIssueDirectly(id, true))`; resolve the one draw, call `TryConsumeLuckyCharm(next, supported)` only when there is an actual eligible encounter, and commit the outcome with claim marker. Background ready must not touch active charm. Existing `EncounterService.Open` handles the reserved encounter path, but do not independently roll the same encounter twice.
- T11 nursery: select actual normal child with recipe weights before inspecting `CanIssueDirectly(id, true)`; freeze child, variant eligibility, chance/draw/outcome at nest launch. Call `NextNurseryLuck` only at committed eligible hatch; unsupported outcome preserves both bonus and charm. The `Nest` engine is outside T07.
- The chance preview for mixed nursery recipes and expedition claim belongs to their corresponding screens. T07 shows only the activity preview and shop activation control.

## Verification and limits

- Focused shiny/roller: 15 passed, 0 failed, exit 0.
- Full solution gate, excluding `FinishReviewEvidenceRendersPopulatedPokedexAndResetArea`: Core 233 and App 391 passed, 0 failed, exit 0. The unfiltered command fails only that capture test because `POKE_SHELL_CAPTURE_DIR` is absent; it is an environment-dependent screenshot test unrelated to T07.
- Functional WPF preview assertion passed in the App suite. Full visual size/DPI matrix remains T20.


## Independent scoped re-review

# T07 fix 1 scoped re-review — c54d7efe67889b1fd255d8b0b2f686ca38ca0655..425ba33fab938adbb086e562e80103144de8515b

## Spec Compliance

- ✅ Approved for this fix. The authoritative migration contract removes purchased `PendingEggs` but retains already hatched individuals (`docs/design-v2/final/MIGRATION.md:32`; `src/PokeCorner.Core/Persistence/SaveMigration.cs:80`). Removing the obsolete hatch action therefore closes the T07 durability defect without requiring a new legacy issuance engine (`src/PokeCorner.App/App.xaml.cs:729-750`; `src/PokeCorner.App/Popup/CollectionView.xaml.cs:172-181, 277-285`; `src/PokeCorner.App/Popup/CollectionView.xaml:384-405`). The migration assertion now checks that the hatched individual's GUID and shiny flag survive (`tests/PokeCorner.App.Tests/GlobalUpdate/migrationTests.cs:148-165`).
- ⚠️ Cannot verify from this fix diff: T08 claim and T11 nest integration remain later tasks. The T20 visual/DPI matrix remains later work. These are unchanged boundaries from the original T07 review, not defects introduced by fix 1.

## Prior findings

- **ADDRESSED — Important, live legacy egg hatch:** `App.HatchEgg`, its CollectionView callback/button/panel and direct charm consumer were removed (`src/PokeCorner.App/App.xaml.cs:729-750`; `src/PokeCorner.App/Popup/CollectionView.xaml.cs:13-24, 91-117, 277-285, 811-828`; `src/PokeCorner.Core/Domain/ShopService.cs:156-166`). A focused call-site check found no remaining production references to `HatchEgg`, `HatchButton_Click`, `TryRollCatchFromTier` or `ConsumeCatchLuckyCharm`. This follows the migration ruling rather than preserving the erroneous earlier T05 handoff. The WPF test verifies that even a stale in-memory egg exposes no hatch panel (`tests/PokeCorner.App.Tests/Theme/ThemeRenderingTests.cs:1511-1532`).
- **ADDRESSED — Minor, duplicate x4 constant/API:** The obsolete `CatchLuckyCharmShinyMultiplier` and live `ConsumeCatchLuckyCharm` were removed (`src/PokeCorner.Core/Domain/ShopService.cs:140-166`); the tier roller was removed with its retired caller (`src/PokeCorner.Core/Domain/CatchRoller.cs:38-80`).
- **NOT ADDRESSED — Minor, activation UI failure test:** Fix 1 changes no ShopView activation test or handler. The service activation test remains, but the specific UI failed-commit/message assertion suggested in the first review was not added (`tests/PokeCorner.Core.Tests/GlobalUpdate/shinyTests.cs:29-43`; `src/PokeCorner.App/Popup/ShopView.xaml.cs:190-207`). This remains optional coverage, not a fix 1 blocker.

## Strengths

- The retirement removes the panel and issuance callback together, including the constructor wiring and WPF fixtures (`src/PokeCorner.App/App.xaml.cs:745-750`; `src/PokeCorner.App/Popup/CollectionView.xaml.cs:91-117`; `tests/PokeCorner.App.Tests/GlobalUpdate/shellTests.cs:232-239, 278-285`). The replacement shiny test still checks a selected unsupported species on the live activity roller (`tests/PokeCorner.Core.Tests/GlobalUpdate/shinyTests.cs:46-59`).
- The migration test pairs egg removal with preservation of the existing individual's ID and shiny status (`tests/PokeCorner.App.Tests/GlobalUpdate/migrationTests.cs:148-165`).

## Issues

### Critical (Must Fix)

- None introduced by fix 1.

### Important (Should Fix)

- None introduced by fix 1.

### Minor (Nice to Have)

- The previously suggested ShopView activation failure test remains open; see prior findings above.

## Assessment

**Task quality:** Approved for fix 1.

**Reasoning:** The changed path is obsolete under the approved migration contract. Retiring its UI and APIs eliminates the pre-commit charm/result mutation while preserving already hatched pets; no new Critical or Important issue was found in the fix diff.

**Evidence checked:** Read the pinned fix diff; inspected only its reported change and focused call sites. No suites rerun. `task-7-fix1-focused.log` records `rtk proxy dotnet test tests/PokeCorner.App.Tests/PokeCorner.App.Tests.csproj --no-restore --filter "FullyQualifiedName~CollectionView_DoesNotExposeLegacyShopEggHatch|FullyQualifiedName~SettingsCareCharmEggsAndEarnedEncountersPreserved" --verbosity minimal`, exit 0, App 2/2. `task-7-fix1-gate.log` records `rtk proxy dotnet test PokeCorner.sln --no-restore --verbosity minimal --filter "FullyQualifiedName!~FinishReviewEvidenceRendersPopulatedPokedexAndResetArea"`, exit 0, Core 229/229 and App 391/391 (620 total). The earlier 624 count was for the pre-retirement code; four obsolete Core tests were removed with their APIs (`tests/PokeCorner.Core.Tests/Domain/CatchRollerTests.cs:78-98`; `tests/PokeCorner.Core.Tests/Domain/ShopServiceTests.cs:6-16`). The environment-dependent screenshot test remains excluded.
