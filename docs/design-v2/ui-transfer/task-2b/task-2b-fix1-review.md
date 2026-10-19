### Spec Compliance

- ✅ **I1 ADDRESSED; I2 ADDRESSED; I3 ADDRESSED; I4 ADDRESSED; I5 ADDRESSED.**
- ✅ Scoped fix-round review: **Approved**. No new Critical or Important defect identified in `ac6316159497127fd2230a968c1804a951709e28..1dbc8589460884f61fd1b01516a78cb2f952ac88`.
- ⚠️ This verdict covers the five named corrections and new breakage in the supplied fix diff. It does not replace coordinated visual/manual acceptance, the final whole-branch review, or the outstanding Task 6 / Task 8 / POKE-58 gates.

### Finding Dispositions

**I1 — ADDRESSED: successful rehome invalidates the removed exact card before refresh.**

- `src/PokeCorner.App/Popup/CollectionView.xaml.cs:1325-1333` now clears the successful operation and hides that GUID before the callback or Refresh. Refresh also invalidates externally removed detail identity, and RefreshDetailItems checks existence before consulting the XP delegate (`:249`, `:1466-1469`).
- `tests/PokeCorner.App.Tests/GlobalUpdate/approvedCollectionTests.cs:190-214` actually removes the live individual, supplies the original Single-based XP lookup, observes dispatcher exceptions and asserts zero missing-ID lookups, no escaped exception and a closed card. Its pass appears at `task-2b-fix1-app-covering.log:265`.

**I2 — ADDRESSED: unloaded presentation stays suspended across async continuations.**

- `CollectionView.xaml.cs:190-202,240,748,1116` resumes presentation only at Loaded, suspends before confirmation cancellation/teardown, and guards Refresh plus card/detail animation entry points. Existing completion callbacks may still publish global state, while presentation Refresh returns during suspension.
- `approvedCollectionTests.cs:221-260` covers the seven supplied item/evolution/rehome pending-confirmation/commit paths, including local-overlay unload cancellation. It proves a hero animator exists before unload, verifies no hero/detail/card/refresh animator remains after continuation or external Refresh, and verifies Loaded resumes the hero. The existing GIF duration/cycling assertions are not modified by this diff.

**I3 — ADDRESSED: the same species tile can reopen its picker.**

- `CollectionView.xaml.cs:398,800,1427-1433` routes consumption, Escape and Back through CloseSpeciesDialog, clearing selection and the selected presentation ID without rebuilding the grid or resetting its form/filter/page.
- `approvedCollectionTests.cs:267-298` repeats the SAME tile twice for ordinary/shiny × Back/Escape/picker-to-card paths. Assertions retain the exact selected-form GUID, original ItemsSource, page, query and form. All six named cases appear in the covering log (`:73,81,87,102,112,119`).

**I4 — ADDRESSED: actual v1 migration retains history-only global/shiny discovery and exact-empty authority.**

- `src/PokeCorner.Core/Persistence/SaveMigration.cs:81-82,107-110` retains existing discovery sets and unions reached stages. Ordinary inference for a surviving shiny individual is conditional on actual missing-field provenance.
- `JsonSaveStore.cs:30-35,204-209` and `SaveData.cs:18-19` carry this provenance through Clone with one internal JsonIgnored annotation; an explicitly present exact empty set does not gain the legacy flag.
- `tests/PokeCorner.Core.Tests/GlobalUpdate/formDiscoveryTests.cs:95-116` performs old-v1 load → actual Migrate → roundtrip for both missing and explicit-empty exact fields. It checks history-only global25/shiny133, reached shiny1→2, identity/stage/nickname/shiny/care/treats and initial coins plus the canonical awarded sum. Both cases pass in focused Core (`task-2b-fix1-core-focused-green.log:31-32`) and the final Core gate (`task-2b-fix1-core-gate.log:259-260`).

**I5 — ADDRESSED: fast-grower reduces the canonical cumulative care requirement and both approved care surfaces consume it.**

- `src/PokeCorner.Core/Domain/EvolutionCalculator.cs:24-33` reads the existing tier via LevelProgression, the existing coefficient via TraitRules, and the existing stack policy once for either unique slot. Unsupported/no-next-form eligibility and the terminal sentinel retain the base requirement. It changes required care rather than stored care or prices.
- `EvolutionService.cs:57` supplies this shared requirement to canonical Preview; the exact card already consumes Preview. `src/PokeCorner.App/Popup/RoomView.xaml.cs:109,129,190-201` caches its rule dependency and uses the same helper for current/previous cumulative thresholds. The changed wording remains care-specific, with no displayed gameplay percentage.
- `formDiscoveryTests.cs:121-154` executes plain-vs-trait checks for both slots at all accepted tiers, one-care-below/exact boundary Evolve, unchanged six-berry price, stored care/XP, second-stage166600, terminal and unsupported shiny guards.
- `approvedCollectionTests.cs:302-314` and `approvedRoomAlbumTests.cs:226-240` exercise actual Preview-backed exact-card care and Corner boundary/terminal/standalone display. Passes appear at `task-2b-fix1-app-covering.log:140,264`.
- The implementation stays inside the root addendum's helper/Preview/consuming-care ownership extension; Room composition, sprite behavior and album source are not redesigned.

### Strengths

- The corrections address the causal paths rather than relaxing failing expectations: successful removal happens in the rehome regression, unload tests wait for async completion, and repeated-species tests assert unchanged grid identity.
- Migration tests now cross the actual v1 boundary and keep exact-empty distinct from absence; the adjusted coin assertion accounts for real canonical awards instead of hiding a resource reset.
- Fast-grower tests execute actual eligibility and Evolve at the reduced threshold while checking prices, stored care/XP and unsupported/terminal behavior. No economic coefficient, berry expenditure or new acquisition path is introduced by the fix.
- The fix diff contains no old-test deletion, GIF timing/cycling change, shared confirmation replacement, or alternate inventory mutator.

### New Issues

#### Critical

- None.

#### Important

- None.

#### Minor

**M1 — Two new test warnings should be cleaned up.**

- `tests/PokeCorner.App.Tests/GlobalUpdate/approvedCollectionTests.cs:283`: CS8602, possible null dereference of the item returned by the nongeneric Items collection. Establish a non-null assertion/type before calling GetType, keeping the exact-GUID assertion.
- `approvedCollectionTests.cs:255`: xUnit2013, collection emptiness checked through Count/Assert.Equal. Use Assert.Empty for the animator dictionary.
- Both warnings are present in the successful covering build, `task-2b-fix1-app-covering.log:5-6`. They do not invalidate the behavior passes or block this scoped approval.

### Evidence and Review Scope

- Reviewed the supplied 59034-byte `task-2b-fix1-review-package.md` in three bounded passes, plus the appended round1 report, prior I1–I4 review and root I5 addendum. Exact reference extraction used the supplied diff metadata; no git rederivation, source crawl, source/index/HEAD edits or subagents.
- Existing raw output confirms focused Core **37/37** (`task-2b-fix1-core-focused-green.log:54-55`), covering App **248/248** (`task-2b-fix1-app-covering.log:268-269`) and final Core **275/275** (`task-2b-fix1-core-gate.log:292-293`).
- The report's focused Core filter covers FormDiscoveryTests/SaveProtectionTests/Migration. The named new Core cases match the test diff and occur in both the focused and final logs. Its covering App filter includes ApprovedCollectionTests, PokedexTests, EvolutionTests, RehomeTests, CollectionViewModelTests, ThemeRenderingTests and RoomAlbumTests; the newly added ApprovedRoomAlbumTests case is included by the RoomAlbumTests substring and appears in the raw output. No test output was regenerated.
- The two App warnings above are the only warnings found in the three checked green logs. No failures were found in those outputs.
- No suite or focused test was run by this reviewer. No additional outside-diff implementation lookup was needed to adjudicate these five corrections.
- Nonblocking final-review ledger: the retained full-App552/559 checkpoint, known starter-offline defect, historical hero timing evidence, coordinated 37-state visual/manual acceptance and 11/1025 Russian facts remain their existing Task 6 / Task 8 / POKE-58 boundaries. No new out-of-scope defect was established in this narrow pass.

### Assessment

**Task quality: Approved**

**Counts:** 5/5 prior findings addressed; new findings: **0 Critical, 0 Important, 1 Minor item containing two test warnings**.

**Reasoning:** The targeted fixes restore safe removal refresh order, unloaded lifetime, repeatable species activation, legacy history preservation and canonical fast-grower care semantics with relevant executed regression evidence. Clean up the two warnings; no second broad review or suite replay is justified by this assessment.
