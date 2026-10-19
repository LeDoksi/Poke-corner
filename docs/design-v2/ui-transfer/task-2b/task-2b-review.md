### Spec Compliance

- ❌ Issues found: four Important defects prevent Task 2B acceptance: successful rehome refreshes a removed individual, async completion can restart animation after unload, the same selected species cannot reopen its dialog, and v1 migration discards history-only discoveries.
- ✅ The inspected implementation otherwise covers the approved owned/species selectors, exact GUID routing, independent forms, selected-form specimen lists, canonical branch graph, optional stone choice, per-pet items, asynchronous confirmation, offline curated facts and missing-art fallback.
- ⚠️ Cannot verify from this task diff: final comparison against the approved 37-state visual kit; actual small work-area resizing owned by the shell; physical input/focus behavior of the shared confirmation host; complete Russian fact coverage. These remain coordinated Task 8 / Task 1 / POKE-58 boundaries, not evidence that this batch supplies them.

### Strengths

- Exact ordinary history is additive and new-instance-empty; missing OLD JSON falls back once, while an explicitly present empty set stays authoritative (`JsonSaveStore.cs:199-202`, `SaveData.cs:13-14`). New tests exercise actual load/roundtrip, v2 fallback, clone isolation, refused write, and canonical capture/evolution/hatch/rehome producers (`formDiscoveryTests.cs:10-110`). The v1 migration exception is finding I4.
- Producer changes only add discovery metadata alongside existing publication and journal logic (`EncounterService.cs:132`, `EvolutionService.cs:109`, `NurseryService.cs:163`, `RehomingService.cs:78`). Production item adapters reuse ShopService inside CommitAsync and repeat busy/unsupported guards in the candidate (`App.xaml.cs:974-991`).
- Individual filters AND together; duplicate grouping uses current species independently of cosmetic identity, nickname and shiny; level/care sorting and a 16-card page are exercised against 800 actual synthetic individuals (`CollectionViewModel.cs:380-409`, `approvedCollectionTests.cs:79-98`).
- Canonical edges supply real parallel branches with identity stripped from unreached graph nodes. The stone toggle controls whether a selected target is actually passed; turning it off passes null even with a stale combo selection (`CollectionViewModel.cs:248-269`, `CollectionView.xaml.cs:1431-1442`, `approvedCollectionTests.cs:146-165`).
- Facts are a clearly scoped embedded 11-row Russian resource with source/version/language/SHA provenance; uncovered exact cards omit the section, with no filler or per-card network (`curated-russian-facts.json:1-123`, `CollectionViewModel.cs:232`, `CollectionView.xaml.cs:854-856`). Full 1025-species coverage is explicitly deferred.
- Missing/corrupt artwork uses an OnLoad bitmap backed by a scoped stream. The new corrupt-file fixture also verifies that cleanup can delete the file, catching the original leaked handle (`CollectionView.xaml.cs:336-354`, `approvedCollectionTests.cs:168-187`).
- The authorized Theme assertion changes fit the approved composition: four-digit number, each action measured at its reachable scroll position, visible exact-card type labels, and explicit selection of the retained compatibility hero (`ThemeRenderingTests.cs:185,486-489,1000,1384-1388`). No GIF duration/cycling assertion changed.

### Issues

#### Critical (Must Fix)

- None.

#### Important (Should Fix)

**I1 — [P1] Successful rehome refreshes a removed pet and can escape the async handler.**

- Evidence: `src/PokeCorner.App/Popup/CollectionView.xaml.cs:1316-1320`; `src/PokeCorner.App/Popup/CollectionView.xaml.cs:1444-1447`; `src/PokeCorner.App/App.xaml.cs:995`.
- A successful transfer removes the individual (`RehomingService.cs:79`), but the handler calls Refresh while its exact card is still visible, and only then intends to HideDetail. Refresh now calls RefreshDetailItems; the production XP provider uses Collection.Individuals.Single for that removed GUID. This throws before the hide. The catch leaves the card open; the unconditional Refresh in finally repeats the same lookup outside the catch, allowing an unhandled exception from async void.
- Hide/invalidate the successfully removed detail before any refresh, and make detail refresh tolerate an individual that disappeared during an async action. Add a success-path test using a rehome delegate that actually publishes removal plus the production-equivalent XP provider. Existing cancellation/refusal/delayed-error tests do not exercise that composition.

**I2 — [P2] Async cancellation/completion can restart stopped animators after unload.**

- Evidence: `src/PokeCorner.App/Popup/CollectionView.xaml.cs:1477` (also `:983`, `:1320`), `:194-215`, `:232-248`, `:446-474`.
- StopLiveRefresh stops the animators and clears the hero signature. An item/evolution/rehome continuation still calls Refresh unconditionally in finally. Repro by leaving the section while confirmation or the commit is pending, then resolving it; local confirmation cancellation on unload also takes this path. Refresh has no loaded/lifetime guard, and RefreshHeroSprite sees the cleared signature and starts a new DispatcherTimer for an animated active pet. Generation checks protect the action target but do not protect this timer restart.
- Gate presentation refresh/animation restart on the current loaded lifetime, while retaining necessary global state publication. Add a focused unload-then-complete/cancel test that asserts animators remain stopped until Loaded.

**I3 — [P2] Closing a species dialog leaves the same tile unable to reopen it.**

- Evidence: `src/PokeCorner.App/Popup/CollectionView.xaml.cs:357-363`, `:791`, `:1412`; `src/PokeCorner.App/Popup/CollectionView.xaml:237`.
- Dialog opening is attached only to ListBox.SelectionChanged. Back and Escape merely collapse SpeciesDialog, leaving SpeciesGrid.SelectedItem unchanged. An unchanged live refresh deliberately preserves ItemsSource, so clicking the same discovered species again raises no selection change and cannot reopen its fact/specimen picker. The same issue occurs after opening one individual from that picker and returning to the grid; discovered tiles have no Where-find button to reopen it.
- Clear selection when closing/consuming the species dialog, or open it through a repeatable tile activation command. Cover open → Back/Escape → same tile, plus picker → exact card → Back → same tile; keep page/filter/form state.

**I4 — [P2] v1 migration discards valid historical discoveries outside the remaining collection.**

- Evidence: `src/PokeCorner.Core/Persistence/SaveMigration.cs:81-83,108-110`; `JsonSaveStore.cs:200`; tests `formDiscoveryTests.cs:18-34,57-65`.
- Deserialization correctly infers ordinary-known IDs from a missing old field, but migration immediately clears NormalSpeciesIds along with global and shiny history, rebuilding only from surviving individuals. A valid v1 file whose global/shiny history includes a previously removed species therefore loses that discovery on migration. The new tests load a schema1 file without actually migrating it, or exercise v2; neither catches this loss.
- The global/shiny resets predate this diff, but preserving all legacy global discovery is an explicit Task 2B requirement, and the new exact-form inference is lost at the newly added reset. Preserve valid old global/shiny histories, seed ordinary history according to the root ruling, then union the migrated reached stages. Do not infer ordinary again when exact metadata was explicitly present. Add an actual v1 migration test containing a history-only species, retained shiny history, and an explicitly empty exact field.

#### Minor (Nice to Have)

- None.

### Verification and scope

- Reviewed both commits, `049d8d8b4ef8419cf519f9af68f53d09faf42699` and `ac6316159497127fd2230a968c1804a951709e28`, against base `554f2b68e9d8cc37db6a92e54f0bf47f5f41133f`, using the supplied 234273-byte package in bounded passes. The first package output was truncated by the tool; subsequent bounded reads recovered the omitted section. No git rederivation or repository crawl.
- Diff context cut needed lifecycle methods and the migration function. Focused source checks completed StopLiveRefresh/Refresh/RefreshHeroSprite/ShowDetailSprite and the end of SaveMigration; the partly omitted card presentation helper/template was checked for inherited percentage output. No percentage-tooltip finding: the shared helper's tooltip is qualitative and the new visible detail text overrides the in-progress percentage label.
- Named outside-diff API checks: EvolutionProgressDisplay in `Popup/ProgressCalculator.cs:34-66` for the inherited tooltip risk; TraitRules for the new short effect-label IDs. TraitRules defines allowed IDs/values rather than the complete effect application, so cross-feature effect semantics are not claimed verified from that check.
- Read existing raw gate evidence, without executing tests: Core `task-2b-core-gate.log:281-285` = 266/266; pre-fix full App `task-2b-app-gate.log:781-785` = 552/559; post-artwork covering App `task-2b-artwork-covering-green.log:224-227` = 207/207. No warning lines found in those three logs.
- Full-App failed cases match the report: starter offline failure remains Task 6; four Theme observers encoded old number/geometry/default hero assumptions; two outside-click failures were real and the final XAML restores the event hookups. Their cases pass in the covering evidence. The full-App checkpoint remains failed; no later full-green claim is made.
- Existing covering tests verify form masking, real graph shape, filters/page identity, item refusal/stable operation retry and confirmation isolation across cards. They do not cover the successful-removal + XP-provider path, reactivation of the same species tile, post-unload timer resurrection, or history-only v1 migration.
- No new tests, suites, capture loops, source/index/HEAD changes, profile access, app/MSI launches or OS changes were performed. Only this requested review report is written.

### Assessment

**Task quality: Needs fixes**

**Reasoning:** The batch substantially implements the approved functional composition and contains useful behavior evidence, but successful rehome has an unsafe refresh order, asynchronous cleanup does not preserve unload lifetime, species activation is not repeatable, and v1 migration misses the explicit discovery-preservation rule. Resolve the four targeted defects in one coordinated fix round, then validate only their focused behavior and affected gates.
