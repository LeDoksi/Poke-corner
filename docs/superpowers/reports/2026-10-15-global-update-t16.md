# Приёмка T16 / POKE-40 — 2026-10-15

Независимое spec/quality review принято для code `786c589`. Ниже исходный отчёт реализации и доработок; старые промежуточные counts сохранены как история.

# T16 — achievement metrics, retro credit, one-time rewards

Source/test commit: `138eb53` (`feat: add durable achievement metrics and retro rewards`).

## Implemented

- Added `AchievementService.Evaluate(SaveData)` as the single evaluator for the 40 chain goals and 16 special awards in the packaged `achievement-rewards.json`. It uses stable AwardId keys, keeps old ledger entries, grants 865 coins across 56 awards only once, and records the catalog revision and eligible species list with each final goal.
- Derived the normal-species goal from directly issuable full family roots plus currently allowed, asset-backed normal evolution edges. Current package yields 856 reachable species and 81 reachable legends; the flat 868 normal asset count is not used as the completion target. Synthetic blocked-root test covers the distinction.
- `AchievementMetrics.Refresh` uses confirmed opened encounters, frozen hatch results, persistent discovery/shiny/evolution histories, saved counters, and one-time event markers. A resolved expedition encounter does not count as a catch before Open. Unknown migrated catch/feed/expedition actions remain unknown.
- Migration now calls the same evaluator; the former private award table and payout code were removed. `AppState.CommitAsync` evaluates the proposed v2 snapshot before validation and durable publish. Award, coin delta, and final-goal revision therefore commit together. On failure live state and disk stay unchanged; after recovery retry grants once.
- The existing expedition, evolution, nursery, rehome journal, potion journal, and current pet/feed mutation paths supply confirmed special/care evidence. Pet and feed now mutate a proposed save and show success only after confirmed commit. Failed writes surface a notification.

## Evidence

- Genuine pre-production RED: `task-16-red.log` contains the exact command, raw compiler output (`CS0246 AchievementService`) and ExitCode 1. It was saved before source edits.
- Focused Core: `task-16-focused.log`, 6/6, ExitCode 0. Covers closed/opened catch, blocked root closure, unknown retro history, 56 awards/865 coins, frozen hatch/care archive, failed care-award commit and one-time retry.
- Focused App migration: `task-16-migration-focused.log`, 77/77, ExitCode 0.
- During migration verification, `RetroCoinOverflowFailsWithoutClamping` exposed a moved error-handling regression: `OverflowException` replaced the existing `InvalidDataException` contract, 76/77, ExitCode 1 (`task-16-migration-regression.log`). The evaluator now wraps overflow as `InvalidDataException`, then 77/77 passed.
- Direct final gates, once after self-review: `task-16-core-final.log` Core 254/254 ExitCode 0; `task-16-app-final.log` App 445/445 ExitCode 0. App excludes only the existing screenshot-capture environment test `FinishReviewEvidenceRendersPopulatedPokedexAndResetArea`. `rtk git diff --check` passed.

## Self-review and handoff

All changed call sites and persisted metrics were checked. Source/test ownership is nine files: `AchievementService.cs`, `AchievementMetrics.cs`, `AppState.cs`, `SaveMigration.cs`, `ExpeditionService.cs`, `EvolutionService.cs`, `NurseryService.cs`, `App.xaml.cs`, and `achievementsTests.cs`. No new store or dependency was added. Existing earned trophies, historical discoveries, and old AwardLedger values are never cleared by reevaluation.

For T17, read `SaveData.AwardLedger` and `Metrics` for the album; `AchievementService(catalog).ReachableSpeciesIds` / `ReachableLegendaryIds` expose current goal closure for display. Do not invoke `Evaluate` from rendering. It mutates only a proposed save inside `AppState.CommitAsync`; `SaveMigration.Migrate` calls it while building the initial v2 snapshot. The 26 cards are ten chains plus sixteen specials; tier art comes from the ledger keys.

The retrain action has no current runtime producer. When that action is implemented, its successful candidate must call `AchievementMetrics.Record(next, "special:trophy-special-retrain")` within the shared commit. Real user saves, MSI/OS state, and live WPF/DPI appearance were not touched; visual acceptance remains T20.

## Fix round 1 — retraining producer (supersedes the retrain handoff above)

Reviewer finding: the service consumed `special:trophy-special-retrain`, but no runtime action produced it. The exact pre-production RED command and compiler errors for missing `RetrainingService`, `RetrainingAction`, and `RetrainingResults` are in `task-16-fix1-red.log` (ExitCode 1). No production file was changed before this RED.

`RetrainingService.Preview(Guid individualId, int slot)` reads the current species recipe from packaged `catalog-rules.json`, lists three berry costs (20 each) and equal-chance outcomes from `TraitRules.Allowed` for the individual's actual normal/shiny variant. It excludes both the replaced and other current traits and rejects locked slots, busy individuals, and shortages. `Retrain(Guid individualId, int slot, Guid operationId)` draws uniformly with a stable operation-bound hash, debits resources, replaces only the selected trait, writes `special:trophy-special-retrain`, and stores an input-bound journal/result in the same proposed save. Replay returns the frozen result without spending or paying again. The save validator rejects mismatched or orphaned retraining journal entries. App routes this through the existing exit-guarded `AppState.CommitAsync` and personal card. The card shows slot, exact recipe, outcomes, confirmation, and T14 shop-shortage navigation; async results use its GUID/generation guard. A failed UI attempt retains the operation ID for deterministic retry.

The canonical 56-award/865-coin test now invokes real retraining instead of injecting that special marker. Core tests also prove failed durable write leaves the trait, berries, and award unpublished; retry with the same operation selects the same trait; replay preserves coins and berries; busy/shortage checks, slot input, and other-trait exclusion. App tests cover the displayed recipe/shortage route and a confirmed card click invoking the real producer. The formerly misleading `FrozenHatchAndDistinctCareMarkersSurviveRehome` test is renamed to `...SurviveReevaluation`, matching its actual behavior.

Current packaged-data check is captured in UTF-8 `task-16-fix1-package-utf8.log`: 1025 packaged species, 856 reachable normal species, 81 reachable legendary species, one test passed, ExitCode 0. The initial `task-16-fix1-package.log` wrapper produced mixed-encoding body text; it is retained as an evidence attempt, and only the direct-output UTF-8 log is authoritative. Focused Core achievements 8/8 and App card checks passed. After self-review, direct final gates ran once: `task-16-fix1-core-final.log` Core 256/256, ExitCode 0; `task-16-fix1-app-final.log` App 448/448, ExitCode 0. The App filter excludes only `FinishReviewEvidenceRendersPopulatedPokedexAndResetArea`, as in the original T16 gate. `rtk git diff --check` passed. No real save, MSI/OS state, or live visual acceptance was touched.

T17 can read the award from `SaveData.AwardLedger` after commit; the new action's public contracts are `RetrainingPreview(Costs, Outcomes, CurrentTraitId, Reason)` and `RetrainingAction(Success, Message, TraitId, Repeated)`. App card wiring is `CollectionView`'s optional `previewRetraining` / `retrainIndividual` delegates. Rendering must not call `AchievementService.Evaluate`.

Fix1 source/test commit: `786c589` (`fix: make retraining award reachable through confirmed action`).


## Independent review evidence: task-16-review.md

### Spec Compliance

- ❌ Issues found: First-retrain award is unreachable. docs/design-v2/ACHIEVEMENTS.md:37 and docs/design-v2/final/achievement-rewards.json:22 require it. src/PokeCorner.Core/Domain/AchievementService.cs:101-102 consumes only special:trophy-special-retrain, but no runtime retrain action or producer exists. tests/PokeCorner.Core.Tests/GlobalUpdate/achievementsTests.cs:120-123 manually injects the marker for the 56-award/865-coin assertion. Implement a successful retrain under the shared candidate commit, record the marker after rules/cost pass, and test success and replay. Alternatively assign a concrete owner/dependency before claiming all 56 are available.
- ⚠️ Cannot verify from this diff: actual packaged closure counts 856 normal/81 legendary are reported but not asserted in new tests. AchievementService.cs:25-39 computes them. Focused unchanged-code check CatalogPackage.cs:62-67 confirms direct issue means an available family root.
- ⚠️ WPF visual/DPI acceptance belongs to T20 per docs/superpowers/GLOBAL-WORKFLOW.md:11-14.

### Strengths

- src/PokeCorner.Core/AppState.cs:76-79 evaluates on a proposed save before publish; achievementsTests.cs:157-192 checks failed-write rollback and one-time retry.
- src/PokeCorner.Core/Domain/AchievementMetrics.cs:12-39 derives confirmed opened catches and retains persistent discovery, shiny, hatch and care evidence. AchievementService.cs:55-69 uses stable AwardId keys and freezes final-goal revision/species.
- src/PokeCorner.Core/Persistence/SaveMigration.cs:107-110 reuses the evaluator without inventing unknown legacy actions. App.xaml.cs:638-663,712-747 commits pet/feed before success feedback.
- Recorded focused Core 6/6, migration 77/77, final Core 254/254 and App 445/445 logs all have ExitCode 0. The App gate excludes the known screenshot-capture environment test. No suites were rerun.

### Issues

#### Critical (Must Fix)

- None.

#### Important (Should Fix)

- src/PokeCorner.Core/Domain/AchievementService.cs:101-102; achievementsTests.cs:120-123: Retrain's 10-coin award has no production event source. Synthetic marker injection proves evaluation, not that a player can earn it. Add the retrain action and event marker, then test real success and one-time replay.

#### Minor (Nice to Have)

- tests/PokeCorner.Core.Tests/GlobalUpdate/achievementsTests.cs:133-154: FrozenHatchAndDistinctCareMarkersSurviveRehome never executes rehome. Rename it to match what it checks, or perform a successful rehome and verify the histories persist.

### Assessment

**Task quality:** Needs fixes

**Reasoning:** Evaluator, migration and durable commit integration are coherent; the recorded tests pass. Full 56-award delivery requires a real retrain producer.


## Independent review evidence: task-16-rereview1.md

### Finding Verdicts

- **First-retrain trophy had no production event source** — ADDRESSED. src/PokeCorner.Core/Domain/RetrainingService.cs:58-77 validates the action, spends the canonical recipe, changes the selected trait, records special:trophy-special-retrain, and journals an input-bound result. src/PokeCorner.App/App.xaml.cs:762-780 routes it through AppState.CommitAsync; src/PokeCorner.App/Popup/CollectionView.xaml.cs:989-1037 exposes the confirmed card action and shortage route. tests/PokeCorner.Core.Tests/GlobalUpdate/achievementsTests.cs:161-220 covers failed durable write, retry, one-time award and replay. The 56/865 test now calls RetrainingService instead of injecting the marker (achievementsTests.cs:118-133).
- **Rehome-named test did not rehome** — ADDRESSED. tests/PokeCorner.Core.Tests/GlobalUpdate/achievementsTests.cs:137 renames it to SurviveReevaluation, matching its assertions.
- **Packaged 856/81 closure was not directly evidenced** — ADDRESSED. tests/PokeCorner.App.Tests/GlobalUpdate/achievementPackageTests.cs:10-19 reads the packaged catalog; task-16-fix1-package-utf8.log:17-28 records 1025 total, 856 reachable normal, 81 reachable legendary, one pass, ExitCode 0.
- **WPF visual/DPI acceptance** — remains assigned to T20 by docs/superpowers/GLOBAL-WORKFLOW.md:11-14; it is not a T16 fix finding.

### New Breakage in the Fix Diff

- None found. The new service checks busy state, slot unlock, actual-variant allowed traits, three 20-berry costs and shortages before mutation (src/PokeCorner.Core/Domain/RetrainingService.cs:36-77). Save validation ties every retrain journal key to its input-bound result (src/PokeCorner.Core/Persistence/JsonSaveStore.cs:288-302).

### Out-of-Scope Observations

- None.

### Verdict

**Fix round:** All findings addressed, no new Critical/Important breakage.

Recorded direct final gates: Core 256/256 and App 448/448, both ExitCode 0 (task-16-fix1-core-final.log:11-13; task-16-fix1-app-final.log:12-14). The App filter retains the known screenshot-capture exclusion. No passing suite was rerun.
