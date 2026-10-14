# Приёмка T13 / POKE-43 — 2026-10-14

Независимое spec/quality review принято для code `cef4767ccfbf9977fff726242af014b14ab8fc47`. Ниже исходный отчёт реализации и доработок; старые промежуточные counts сохранены как история.

# T13 — canonical evolution implementation

## Delivered

- `EvolutionService.Preview(Guid)` reads immediate canonical edges and their berry recipe from `catalog-rules.json`, the quantity from `balance.json`, and actual normal/shiny availability from the loaded `CatalogPackage`. It reports available and blocked branches, care, berry and stone stock, busy state, and a refusal reason. A missing immediate edge or unavailable variant cannot be skipped or downgraded.
- `EvolutionService.Evolve(Guid, int?, Guid)` advances exactly one edge after the 70,000/196,000 care threshold and six matching berries. A null branch draws deterministically among available next branches; an explicit branch spends a stone only when there are multiple available choices. It retains the individual GUID, shiny, family root, nature, traits, nickname, XP, caught date, and accumulated care. Discovery, shiny discovery, reached path and transition journal advance with the evolution.
- A completed operation stores an input fingerprint and chosen species in the v2 save. Same-input replay returns that result without a second charge or evolution; an ID reused for another individual or choice is rejected. Old v2 saves deserialize with an empty result journal. The old `AppState.TryEvolve` mutation is disabled for v2.
- Production evolution from the T14 personal card shows the actual recipe, shortage, available/blocked choices and refusal. It offers the existing shop with missing berry quantity prefilled and the expedition tab, asks for confirmation, then uses `AppState.CommitAsync` before publishing success. It preserves the card's GUID/generation across `await`, keeps each pending individual's action disabled during live refresh, and rechecks exit state after waiting for expedition progress. Closing confirmation or a failed write leaves the published individual and stock unchanged.

## Verification and evidence

- **RED before any production edit:** `task-13-red.log` and `task-13-red-scenarios.log` contain exact `COMMAND`, compiler `CS0246` for missing `EvolutionService`, and `ExitCode: 1`. The expanded scenario log existed and was inspected before implementation.
- Focused T13 scenarios: 10/10 passed in `task-13-focused-final.log`; real WPF confirmation cancellation: 1/1 passed in `task-13-cancel-confirmed.log`. These cover Eevee branches and one berry, blocked shiny, no stage skip, both thresholds, meaningful stone use, branch/individual-bound replay, running/ready busy refusal, failed commit/retry, legacy bypass prevention, compact 320-pixel card and shop route, and actual dialog cancellation.
- Final shared gate: `task-13-final-gate.log` records `rtk proxy dotnet test PokeCorner.sln --no-restore --verbosity minimal --filter 'FullyQualifiedName!~FinishReviewEvidenceRendersPopulatedPokedexAndResetArea'`, Core **248/248**, App **434/434**, total **682/682**, `ExitCode: 0`. The only exclusion is the previously agreed environment-specific screenshot capture test.
- Self-review checked immediate edge selection, shiny asset checks, preserved individual fields, replay after serialization and failed write, old v2 loading defaults, live busy checks, card lifetime after an async action, and compact control bounds. `git diff --cached --check` is performed before the source commit.

## Interface and remaining boundaries

- Downstream tasks can use `new EvolutionService(catalog, saveSnapshot).Preview(individualId)` for read-only state and `Evolve(individualId, selectedBranchOrNull, operationId)` only inside a proposed `AppState.CommitAsync` snapshot. `EvolutionAction.SpeciesId` is the durable selected result, including replay. `CollectionView.OpenIndividual(Guid)` and `SelectedIndividualId` remain the T14 personal-card contract.
- The card's expedition button opens the existing placeholder tab; T09/POKE-36 owns the working expedition screen, so the route is not yet a usable expedition flow. T20 owns the full visual/DPI matrix. No live save, installer, Windows registration, or physical DPI run was touched. The user's actual deployed state has not been migrated by this task.

## Fix round 1 — opened migration gift chain metadata

- Review `task-13-review.md` found that migration-issued gifts had `ChainLength = 1` even when their root had canonical outgoing edges. The same individual was opened and owned, so evolution changed its stage but `JsonSaveStore.ValidateIndividual` rejected the proposed save. The hero's legacy length gate also suppressed the ready state.
- New gifts now take their length from `EncounterService`'s canonical direct-root chain. For an already-saved v2 `update-gift` that still has the length-one placeholder, `EvolutionService.Evolve` reconciles only the owned root individual to canonical length in the proposed transaction, after checking its source, stage, reached path and root identity. The historical gift/encounter records remain intact; unrelated inconsistent metadata is not silently repaired. The strict save validator is unchanged. Hero readiness now uses the canonical preview's next edges, care and busy state, so a real single-stage root never appears ready.
- RED logs `task-13-fix1-red.log` and `task-13-fix1-red-commit.log` were saved and confirmed before production edits. The second log exercises actual `SaveMigration → ReserveGift → Open → AppState.CommitAsync` and an already-saved v2 round trip; both fail with `JsonException: неверная стадия или прогресс`, `ExitCode: 1`.
- Focused `task-13-fix1-focused-final.log`: **14/14**, `ExitCode: 0`; added coverage includes the opened gift's durable evolution and identity/traits/XP/care/history, old v2 gift round trip, failed write plus deterministic retry, canonical hero readiness, and direct issuance of a standalone root that cannot evolve.
- Final shared gate `task-13-fix1-final-gate.log`: Core **248/248**, App **437/437**, total **685/685**, `ExitCode: 0`, using the same single known environment-specific screenshot-capture exclusion. No real save, physical DPI, installer or OS state was touched.


## Independent review evidence: task-13-review.md

﻿### Spec Compliance

- Issues found (Important): opened migration gift roots cannot evolve. SaveMigration creates eligible gift parents with ChainLength = 1 (src/PokeCorner.Core/Persistence/SaveMigration.cs:188); EncounterService carries the same individual into the collection (src/PokeCorner.Core/Domain/EncounterService.cs:186-199). EvolutionService increments CurrentStage (src/PokeCorner.Core/Domain/EvolutionService.cs:95), then AppState.CommitAsync validates the proposed save (src/PokeCorner.Core/AppState.cs:75-79). JsonSaveStore rejects CurrentStage >= ChainLength (src/PokeCorner.Core/Persistence/JsonSaveStore.cs:263). A valid card action therefore fails to save. App's hero readiness is also hidden by the ChainLength gate (src/PokeCorner.App/App.xaml.cs:721; src/PokeCorner.Core/AppState.cs:128-130). Repair new and existing v2 gift metadata or reconcile it from canonical edges in the evolution transaction; add an opened-gift commit regression test.
- Verified in the diff: immediate canonical edges, normal/shiny availability, berry recipe and balance quantity (src/PokeCorner.Core/Domain/EvolutionService.cs:28-66); thresholds and one-edge mutation (src/PokeCorner.Core/Domain/EvolutionCalculator.cs:19-25; src/PokeCorner.Core/Domain/EvolutionService.cs:54-95); meaningful stone use and deterministic random branch (src/PokeCorner.Core/Domain/EvolutionService.cs:83-92); live busy protection (src/PokeCorner.Core/Domain/EvolutionService.cs:52; src/PokeCorner.Core/Domain/ExpeditionService.cs:76-79); retained identity and care, advanced discovery/history (src/PokeCorner.Core/Domain/EvolutionService.cs:93-102); input-bound replay (src/PokeCorner.Core/Domain/EvolutionService.cs:71-78,100-101); v2 legacy bypass blocked (src/PokeCorner.Core/AppState.cs:133-140); card GUID/generation guard (src/PokeCorner.App/Popup/CollectionView.xaml.cs:893-921); proposed snapshot committed before publication (src/PokeCorner.App/App.xaml.cs:727-745; src/PokeCorner.Core/AppState.cs:65-81).
- Cannot verify here: physical compact-window/DPI visual acceptance is T20. The expedition tab is currently a placeholder; T09 owns the working route. No real save, installer, or OS registration was exercised.

### Strengths

- Canonical catalog data and actual shiny availability drive refusal without variant downgrade or stage skipping (src/PokeCorner.Core/Domain/EvolutionService.cs:28-66).
- The existing commit publisher protects failure and replay checks both individual and choice (src/PokeCorner.App/App.xaml.cs:727-745; src/PokeCorner.Core/Domain/EvolutionService.cs:71-78).
- The card retains its chosen GUID across confirmation and await, updates stock/busy state, and routes shortage to the existing shop (src/PokeCorner.App/Popup/CollectionView.xaml.cs:836-932).

### Issues

#### Important (Should Fix)

- src/PokeCorner.Core/Domain/EvolutionService.cs:95: an opened migration gift passes Preview, but its ChainLength=1 makes the proposed evolved stage invalid at commit (SaveMigration.cs:188; JsonSaveStore.cs:263). Correct persisted gift metadata or normalize it in the transaction, including already-saved v2 gifts.

#### Minor (Nice to Have)

- None.

### Assessment

**Task quality:** Needs fixes

**Reasoning:** The main evolution path is coherent and reported tests pass, but an eligible owned gift cannot complete evolution. This is a runtime integration defect.

**Checks:** Read the prepared 475e95d..edb8c74 diff; the first tool output was truncated, so retrieved only omitted line ranges. Focused unchanged-code checks covered gift creation/encounter/validation, shared busy provider and commit path, and shop shortage route. Verified task-13-red-scenarios.log CS0246 ExitCode 1; focused-final 10/10 ExitCode 0; cancel-confirmed 1/1 ExitCode 0; final-gate Core 248/248 plus App 434/434 ExitCode 0 with stated screenshot-capture exclusion. No tests rerun and no source/index/HEAD mutation.



## Independent review evidence: task-13-rereview1.md

﻿### Finding Verdicts

- Opened migration gift root cannot evolve because ChainLength=1 - ADDRESSED. New gifts now receive the canonical direct-root length (src/PokeCorner.Core/Persistence/SaveMigration.cs:181-190; src/PokeCorner.Core/Domain/EncounterService.cs:19-20,37-44). For an already-owned v2 update-gift root with the old placeholder, Evolve checks source, root/path/stage and reconciles only the proposed individual's length before advancing (src/PokeCorner.Core/Domain/EvolutionService.cs:86-102; src/PokeCorner.Core/Domain/PokemonIndividual.cs:29-38). The strict stage validator remains intact. The active hero uses canonical branch/care/busy readiness, so old gifts become visible and solo roots do not (src/PokeCorner.App/App.xaml.cs:718-724; src/PokeCorner.Core/AppState.cs:132-135). Actual commit/round-trip tests cover new and old gift paths (tests/PokeCorner.App.Tests/GlobalUpdate/evolutionTests.cs:286-393).

### New Breakage in the Fix Diff

- None found. DirectRootChainLength uses the same CanIssueDirectly root set from which gift candidates are selected (src/PokeCorner.Core/Domain/EncounterService.cs:37-44; src/PokeCorner.Core/Persistence/SaveMigration.cs:170-190). Reconciliation is restricted to old owned update-gift roots and does not relax JsonSaveStore validation (src/PokeCorner.Core/Domain/EvolutionService.cs:86-102).

### Out-of-Scope Observations

- None.

### Verdict

**Spec compliance:** Approved for the scoped finding.
**Task quality:** Approved for the scoped fix.
**Fix round:** All findings addressed, no new Critical/Important breakage.

**Checks:** Read prepared 0acdbca..cef4767 diff once; checked the canonical root set because DirectRootChainLength uses Single. Reviewed actual fix1-red-commit.log (two JsonException failures, ExitCode 1), fix1-focused-final.log (14/14, ExitCode 0), and fix1-final-gate.log (Core 248/248 plus App 437/437, total 685/685, ExitCode 0; known screenshot-capture exclusion only). No tests rerun, no source/index/HEAD changes. Cannot verify physical DPI, deployed real saves, or the T09 expedition screen in this scoped rereview.
