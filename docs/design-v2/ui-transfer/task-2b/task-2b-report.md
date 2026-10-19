# Task 2B report — approved Pokédex, exact individual and evolution

Status: DONE_WITH_CONCERNS.
Worktree: C:/Users/Georgy/.codex/worktrees/global-update/PokeTokenBar-Windows
Branch: codex/global-update
BASE: 554f2b68e9d8cc37db6a92e54f0bf47f5f41133f
HEAD: 1dbc8589460884f61fd1b01516a78cb2f952ac88
Commits: 049d8d8b4ef8419cf519f9af68f53d09faf42699 — feat(ui): transfer approved pokedex and individual growth cards; ac6316159497127fd2230a968c1804a951709e28 — fix(ui): release unreadable collection artwork streams

## Implemented whole batch

- Pokédex opens on the owned collection; the old hero remains an internal compatibility tab without a second user-facing navigation row. Bounded owned pages contain 16 individuals, species pages12. Existing static/GIF caches, content signatures, card/hero/detail timer teardown remain. Unchanged refresh retains page and ItemsSource; no800-image decode or per-card network path was added.
- Owned search includes species, nickname and padded/unpadded number. Type, rarity, growth, CURRENT-species duplicates, favorite and shiny AND together. Number/level/catch-date/care sorting is functional. Duplicates ignore nickname/shiny/cosmetic display. No universal free label or ready-for-evolution filter.
- Species mode has ordinary/shiny switches, independent discovery masking and only selected-form individual choices. Unknown name/type/rarity never enters its searchable presentation record. Species tiles omit owned/busy/free status and individual levels. Native second-line Where-find actions and an in-context dialog use canonical routes/root rules plus prepared NurseryCatalog recipes. Unknown parents/predecessors are numbers; evolved forms give predecessor/evolution route, never direct encounter claims. Existing unsupported forms remain represented with missing-art fallback and no ordinary-art substitution.
- The full in-window individual card contains a large real pet, stable exact GUID behind identity/#/types/rarity/level/date/nickname/favorite, desktop action near the name, explicit care and XP, traits with short effects, canonical evolution graph, prepared fact when available, per-pet item actions and rehome. Back/outside/Esc preserve prior grid/filter/page/form. Desktop selection rechecks live busy exclusions inside the candidate commit.
- Canonical graph reads final/catalog-rules.json and renders branches at equal depth. Eevee has one root and8 parallel children. Reached-stage cosmetic display actions retain their existing API. Closed target names/images stay masked. Evolution uses the canonical Preview for care/berries/stocks/shortages/availability and a whole Time-to-grow composition with current/target images. Optional large keyboard-accessible stone card appears only with multiple eligible choices; checked stone selects an actual target, unchecked stone ignores stale forced selection and passes null for canonical random.
- Evolution, retraining, rehome and items use async shared-shell confirmation in production. No synchronous dispatcher bridge or new empty confirmation Window. Cancel is the shared host's first focus; local fixture fallback uses the same ConfirmationOverlay control. Pending item/evolution/rehome clicks are guarded before confirmation; close/navigation/unload generation checks prevent confirmation applying to another/hidden detail. Failed write errors stay on their own card. Stable operation IDs survive retry for items/evolution/rehome; existing retraining retry IDs remain.
- App appends real per-pet ShopService.TryUseCareBooster/TryUseXpPotion adapters inside AppState.CommitAsync. It never publishes before confirmed write. Stock/effect/use/XPoverflow are explicit; care shows canonical item units without a CP formula. Busy/unsupported guards re-run inside proposed snapshots. No new inventory mutator, economy or acquisition-rule rewrite.
- Runtime facts are src/PokeCorner.App/Assets/UiSystem/curated-russian-facts.json, embedded in the App. Exactly11 curated Russian rows copied from the approved sourced input, with source CSV SHA256, per-row original language/version/source URL and runtime curation language/version/input SHA256. CollectionPresentation.Fact returns null for uncovered species; the fact section is omitted. Normal/shiny share species facts. No reviewData dependency or per-card network. POKE-58 full1025 coverage remains mandatory before release.

## Metadata compatibility and handoff interface

Added SaveData.NormalSpeciesIds (HashSet<int>, new-instance default empty), cloned/serialized normally. AppState exposes read-only NormalSpeciesIds and ShinySpeciesIds alongside unchanged global DiscoveredSpeciesIds. Existing producer metadata now records normal history in EncounterService.Open, EvolutionService.Evolve, NurseryService.Hatch, RehomingService.Transfer, AchievementMetrics refresh, and AppState snapshots. Existing global/shiny fields and reward/acquisition semantics are unchanged.

Exact root ruling implemented: an OLD JSON file missing NormalSpeciesIds copies its legacy global DiscoveredSpeciesIds once at deserialization; v1 migration retains the equivalent ordinary-known reached state, including historical shiny-only records. This preserves prior catalog opening, and is explicitly legacy DISPLAY INFERENCE, not proof of exact ordinary history. If the exact field exists, including an explicitly empty array, global discovery is NEVER used to infer ordinary again. New profiles serialize an empty exact set; a newly opened shiny-only encounter records shiny/global and leaves normal empty. No collection/ID/stage/shiny/currency reset or unsupported-pet deletion. ValidateData rejects null/nonpositive exact IDs; ValidateCatalog checks catalog membership. Refused candidate write does not publish the new history.

CollectionView.OpenIndividual(Guid)->bool, public constructor/service navigation APIs are retained; optional dependencies were appended only. New optional CollectionView dependencies: confirmAsync, previewItem, useItem, xpProgress, whereFind. New optional VM dependencies: normalSpeciesIds, shinySpeciesIds, CollectionPresentation. GetSpeciesEntries(bool isShiny=false) preserves ordinary default calls. Production supplies real metadata providers and shared shell ConfirmAsync. Room/Album2A is unchanged and still opens exact GUID through its accepted callback.

## RED/GREEN commands and raw evidence

All shell invocations used rtk (initial bootstrap read loaded RTK.md), with scoped escalation for this worktree's source/build/git. dotnet commands below ran under `rtk proxy powershell -NoProfile -Command`, redirecting complete raw output with `*>` to the named ledger files and recording native exit codes. Log encoding from Windows PowerShell is UTF-16. No failed log was replaced.

1. RED App: `dotnet test tests/PokeCorner.App.Tests/PokeCorner.App.Tests.csproj --filter FullyQualifiedName~ApprovedCollectionTests --logger 'console;verbosity=normal'`
   task-2b-app-red.log: exit1,0/3. Behavior executed before production changes: shiny-only falsely opened ordinary; approved form/filter/item actions absent at1000x760 and840x650.
2. RED Core: `dotnet test tests/PokeCorner.Core.Tests/PokeCorner.Core.Tests.csproj --filter FullyQualifiedName~FormDiscoveryTests --logger 'console;verbosity=normal'`
   task-2b-core-red.log: exit1,0/1. New profile did not serialize exact ordinary history.
3. First App GREEN (same App RED command): task-2b-first-green.log: exit0,3/3.
4. Focused metadata: `dotnet test tests/PokeCorner.Core.Tests/PokeCorner.Core.Tests.csproj --filter 'FullyQualifiedName~FormDiscoveryTests|FullyQualifiedName~SaveProtectionTests|FullyQualifiedName~Migration' --logger 'console;verbosity=normal'`
   task-2b-metadata-green.log: exit1, fixture compile errors from using internal Deserialize; corrected to actual public temporary-file Load.
   task-2b-metadata-green2.log: exit1,24/26. Invalid ordinary IDs were not checked for schema1; moved validation to the common boundary, keeping assertions.
   task-2b-metadata-green3.log: exit0,26/26, including legacy/missing-field/explicit-empty/clone/refusal/save protection. Later final Core gate includes added actual-v2 and canonical-producer coverage.
5. Owned feature batch: `dotnet test tests/PokeCorner.App.Tests/PokeCorner.App.Tests.csproj --filter 'FullyQualifiedName~ApprovedCollectionTests|FullyQualifiedName~PokedexTests|FullyQualifiedName~EvolutionTests|FullyQualifiedName~RehomeTests' --logger 'console;verbosity=normal'`
   task-2b-focused.log: exit1, fixture compile error assigning init-only EvolutionChainId after construction; corrected fixture.
   task-2b-focused2.log: exit1,46/49. One missing synthetic xp stock key and two obsolete expectations (equal-probability copy and stale forced target requiring a stone while stone off). Fixed fixture/approved-composition assertions; canonical branch/stock guards retained.
   task-2b-focused3.log: exit0,49/49.
6. ONE final Core gate: `dotnet test tests/PokeCorner.Core.Tests/PokeCorner.Core.Tests.csproj --logger 'console;verbosity=normal'`
   task-2b-core-gate.log: exit0,266/266. Includes actual v2 missing-field load, exact-empty authoritative roundtrip, refused commit, and real canonical capture/evolution/hatch/rehome history surviving serialization; existing migration/commit-protection/acquisition safeguards pass.
7. ONE final App gate: `dotnet test tests/PokeCorner.App.Tests/PokeCorner.App.Tests.csproj --logger 'console;verbosity=normal'`
   Process-only POKE_SHELL_CAPTURE_DIR=$env:TEMP/pokecorner-task2b-captures for existing shell fixtures.
   task-2b-app-gate.log: exit1,552/559. Seven failures retained before corrections: known starter legacy-first defect; #004 vs approved #0004; old pinned-bottom detail action geometry; old hidden-hero default type/icon observers; two backdrop-close tests exposed a lost event hookup. Restored the actual backdrop event safeguard. No sprite cycling/timing assertion changed; hero GIF cycling PASSED this full gate.
8. First covering focused batch AFTER fixes (no broad rerun): `dotnet test tests/PokeCorner.App.Tests/PokeCorner.App.Tests.csproj --filter 'FullyQualifiedName~ApprovedCollectionTests|FullyQualifiedName~PokedexTests|FullyQualifiedName~EvolutionTests|FullyQualifiedName~RehomeTests|FullyQualifiedName~CollectionViewModelTests|FullyQualifiedName~ThemeRenderingTests' --logger 'console;verbosity=normal'`
   Same process-only temporary capture directory. task-2b-final-focused.log: exit0,205/205. Covers exact stone target vs random, hidden target masking, actual scroll reachability for selection/items/rehome with long Cyrillic name at both accepted sizes,800-individual filters/level/care/duplicates/page identity, prepared facts/routes/fork graph, missing/unreadable artwork handling and busy state, item refusal/retry IDs and pending confirmation isolation. All six corrected full-gate collection/Theme failures pass here, including backdrop input safeguards. Full App result remains the honest552/559 checkpoint; no claim of a later all-green full gate.
8b. Explicit artwork evidence added after checking the report against the brief:
   `dotnet test tests/PokeCorner.App.Tests/PokeCorner.App.Tests.csproj --filter FullyQualifiedName~MissingOrUnreadableArtworkKeepsExactIndividualReachable --logger 'console;verbosity=normal'`
   task-2b-artwork-focused.log: exit1,1/2. Missing file passed. Corrupt image left the exact card reachable, but failed WPF UriSource decoding retained a file handle and prevented fixture cleanup. This was a real lifecycle issue, not an assertion to suppress. Changed only package image loading to scoped File.OpenRead + BitmapImage.StreamSource/OnLoad so both success and failure dispose the stream.
   Repeated the covering App filter from8 solely because of this new source fix: task-2b-artwork-covering-green.log, exit0,207/207, including both explicit missing/corrupt fixtures and every prior collection/Theme case. No repeated full App/Core gate. Follow-up commit ac6316159497127fd2230a968c1804a951709e28 changes only CollectionView.xaml.cs and approvedCollectionTests.cs; post-commit status is empty.
9. `rtk git diff --check` and scoped staged diff check: exit0. Self-review inspected all producers, deserialization/migration/validation, production delegates, form/target masking, canonical direct-root/standalone hints, exact GUID routing, no-stone stale selection, page/cache/timer lifetime and cancellation/refusal semantics. Route/nursery hint data is indexed once (2071 prepared recipes), avoiding a per-species scan on every refresh. Commit contains23 owned files. Post-commit status: empty.

## Authorized Theme assertion changes

Root explicitly extended ownership after the final-gate obsolete expectations were found. Only four composition changes in ThemeRenderingTests: approved #0004 padding; type-name visible-text assertion now examines the actual exact-individual card; retained compatibility hero icon test explicitly selects that internal hero; selection/rehome bounds are still measured against the real viewport after scrolling to each approved action position. No bounds, semantic identity, glyph-style identity, cancellation, timing, or GIF-cycling assertion was weakened. Two outside-click regressions were fixed in production event wiring, without editing their safeguards.

## Files (23)

App: App.xaml.cs; PokeCorner.App.csproj; Assets/UiSystem/curated-russian-facts.json; Popup/CollectionView.xaml/.cs; CollectionViewModel.cs; CollectionCardModels.cs; CollectionPresentation.cs.
Core: AppState.cs; Domain/AchievementMetrics.cs, EncounterService.cs, EvolutionService.cs, NurseryService.cs, RehomingService.cs; Persistence/SaveData.cs, JsonSaveStore.cs, SaveMigration.cs.
Tests: App GlobalUpdate/approvedCollectionTests.cs, pokedexTests.cs, evolutionTests.cs, rehomeTests.cs; Theme/ThemeRenderingTests.cs; Core GlobalUpdate/formDiscoveryTests.cs.

## Concerns and deferred acceptance

- Known starter gate failure remains Task6: acceptanceTests.OfflineStarterUsesCanonicalFramesBeforeAnyLegacyPngFetch, StarterSelectionWindow.xaml.cs:88, "Legacy PNG/network fetch should not run." Not changed or excluded.
- Earlier suite-level CollectionView hero timing failure remains historical Task8 evidence. This batch's full and both covering focused runs passed it, with no assertion/timing change. Passing here does not establish that earlier intermittent issue is resolved.
- Full Russian facts remain11/1025, intentionally no fake filler. POKE-58/Task7 must provide release coverage.
- Whole-app visual comparison against the37-state kit and real manual interaction remain Task8. Test bounds/cache/input checks and temporary fixture captures are not that global visual acceptance.
- No real profile, app launch against real state, new art/imagegen, subagent/reviewer, installer/MSI, OS/autostart changes, release, network-per-card addition or push. No Room/Album source or root docs/Kanban edits.


## Review correction round1/5 — 19 October2026

Status: DONE_WITH_CONCERNS, all four Important review findings and root-authorized I5 corrected; independent scoped re-review remains root-owned.
FIX_BASE: ac6316159497127fd2230a968c1804a951709e28
FIX_HEAD: 1dbc8589460884f61fd1b01516a78cb2f952ac88
Commit: fix(ui): preserve collection lifetime history and canonical care

### Findings and correction

- I1: A successfully published rehome now invalidates/hides that exact removed card BEFORE any presentation refresh or on-state-changed callback. General detail refresh and item/XP refresh also detect a missing individual before consulting the production-equivalent Single-based XP provider. The regression actually removes the published individual, uses that provider, records dispatcher-unhandled exceptions, and verifies no missing-XP lookup, no exception, retained survivor, and closed card. Refusal and delayed-error behavior remains covered.
- I2: Unload suspends presentation before canceling any local confirmation and stopping timers. Refresh returns while suspended; card/detail animation entry points also honor suspension. Only Loaded resumes refresh and detail animation, leaving global committed publication independent of the page lifetime. Seven pending paths cover item/evolution/rehome confirmation cancellation and commit completion, plus the local overlay's unload-cancel path. They verify no hero/detail/card/refresh animator survives or restarts, even on an external Refresh, and a subsequent Loaded resumes presentation. No GIF duration/cycling assertion changed.
- I3: One CloseSpeciesDialog path clears tile selection and the selected-species presentation ID when Back, Escape or consuming the picker opens an exact individual. Six ordinary/shiny cases repeat the SAME tile twice after Back, Esc or picker→exact card→Back. The exact selected-form GUID, same ItemsSource, page2, query and form remain intact. No grid/filter/page reset or refresh rebuild is used to mask the defect.
- I4: The global/shiny history resets were inherited from the ancestor migration, but preserving those histories was an explicit Task2B requirement, so they are owned and corrected here. Migration retains global, shiny and once-inferred normal sets, then unions migrated reached stages. A minimal internal JsonIgnored UsesLegacyNormalDiscoveryInference annotation distinguishes an OLD missing field from an explicitly present exact empty field through Clone and actual v1→v2 migration. Only missing-field provenance permits ordinary display inference for surviving shiny reached stages; exact records never infer ordinary from global/shiny history. Actual old-v1 load→Migrate→roundtrip tests include history-only global25, shiny133, a surviving shiny1→2, unsupported asset histories, exact-empty vs missing ordinary metadata, preserved ID/stage/nickname/shiny/care/treats and initial coins plus the unchanged canonical award ledger. No profile collection/resources are reset by the new correction.
- I5 (root-authorized narrow extension): the card's fast-grower effect previously described an accepted but unapplied canonical rule. EvolutionCalculator now has the shared personal cumulative requirement overload reading existing TraitRules/LevelProgression/catalog eligibility. Either unique trait slot uses the existing I/II/III values5/10/15 percent from final/balance.json and existing tier/stack policy. The accepted integer thresholds give exact integer results (first stage66500/63000/59500; second-stage III166600); no new balance or rounding policy was invented. EvolutionService.Preview uses that helper, so actual eligibility and exact-card threshold/bar share one authority. Care and XP remain distinct; cumulative/stored care, level, six-berry price, branch/shiny/terminal/busy/idempotency/confirmed-save guards remain. Root separately authorized RoomView.xaml.cs CARE ONLY: cached TraitRules/LevelProgression use the same helper for current and preceding cumulative thresholds. Its text remains care-specific and never claims complete berry/branch readiness. No sprite, geometry or album redesign. No other trait audit or engine rewrite.

### RED/GREEN evidence, exact commands and exits

All commands ran with rtk/scoped worktree escalation. Full raw logs remain in this ignored ledger beside this report; Windows PowerShell redirection is UTF-16. Nothing was overwritten or excluded. Completed GREEN suites were not replayed after the usage-limit checkpoint.

1. RED original review UI regressions, before production correction:
   `dotnet test tests/PokeCorner.App.Tests/PokeCorner.App.Tests.csproj --filter FullyQualifiedName~Fix1 --logger 'console;verbosity=normal'`
   task-2b-fix1-app-red.log — exit1,10/23 passed. The13 new I1/I2/I3 cases failed as intended: removed-XP refresh/async escape, all six external pending unload paths restarted hero animation, and all six repeated normal/shiny picker paths failed reopening. The broad name substring also matched10 existing MigrationTests.Fix1 cases, which passed; no unrelated source was touched.
2. First Core RED fixture attempt:
   `dotnet test tests/PokeCorner.Core.Tests/PokeCorner.Core.Tests.csproj --filter FullyQualifiedName~Fix1ActualOldV1Migration --logger 'console;verbosity=normal'`
   task-2b-fix1-core-red.log — exit1,0/2, stopped on the synthetic catalog missing achievement-rewards.json. This was NOT counted as migration-behavior evidence; the fixture was corrected by copying the existing canonical resource, then meaningful RED reran before source fixes.
3. Actual migration-history and fast-grower RED:
   `dotnet test tests/PokeCorner.Core.Tests/PokeCorner.Core.Tests.csproj --filter 'FullyQualifiedName~FormDiscoveryTests.Fix1' --logger 'console;verbosity=normal'`
   task-2b-fix1-core-behavior-red.log — exit1,0/9. Both actual old-v1 migration cases lost history-only discovery; six slot/tier cases used70000 instead of the canonical reduced threshold; cumulative second-stage case used196000 instead of166600.
4. Exact-card/Corner RED, after root explicitly extended care-only ownership:
   `dotnet test tests/PokeCorner.App.Tests/PokeCorner.App.Tests.csproj --filter 'FullyQualifiedName~Fix1CardCare|FullyQualifiedName~Fix1CornerFastGrower' --logger 'console;verbosity=normal'`
   task-2b-fix1-care-app-red.log — exit1,0/2. The card displayed66500/70000 and Corner bar remained incomplete at the canonical66500 boundary.
5. Initial focused correction verification:
   `dotnet test tests/PokeCorner.Core.Tests/PokeCorner.Core.Tests.csproj --filter 'FullyQualifiedName~FormDiscoveryTests|FullyQualifiedName~SaveProtectionTests|FullyQualifiedName~Migration' --logger 'console;verbosity=normal'`
   task-2b-fix1-core-focused.log — exit1,35/37. All production behavior fixes passed; two migration fixtures incorrectly expected77 coins even though existing unchanged AchievementService legitimately grants70. The assertions now require INITIAL77 PLUS the actual canonical AwardLedger.CoinsGranted sum, preserving a concrete no-reset invariant without changing rewards.
   `dotnet test tests/PokeCorner.App.Tests/PokeCorner.App.Tests.csproj --filter 'FullyQualifiedName~ApprovedCollectionTests|FullyQualifiedName~ApprovedRoomAlbumTests' --logger 'console;verbosity=normal'`
   task-2b-fix1-app-focused.log — exit0,34/34.
6. Focused Core GREEN after the fixture correction:
   Same Core filter as5. task-2b-fix1-core-focused-green.log — exit0,37/37.
7. Covering App GREEN:
   `dotnet test tests/PokeCorner.App.Tests/PokeCorner.App.Tests.csproj --filter 'FullyQualifiedName~ApprovedCollectionTests|FullyQualifiedName~PokedexTests|FullyQualifiedName~EvolutionTests|FullyQualifiedName~RehomeTests|FullyQualifiedName~CollectionViewModelTests|FullyQualifiedName~ThemeRenderingTests|FullyQualifiedName~RoomAlbumTests' --logger 'console;verbosity=normal'`
   Process-only POKE_SHELL_CAPTURE_DIR=$env:TEMP/pokecorner-task2b-fix1-captures for existing shell fixture requirements.
   task-2b-fix1-app-covering.log — exit0,248/248. Includes all I1–I5 regressions and local confirmation cancellation, existing collection/evolution/rehome failed-write/idempotency/shiny/branch safeguards, Theme lifecycle/cycling/input/layout checks, and care-only Corner/album regressions. No full App suite/capture loop was run.
8. ONE root-authorized final Core gate, justified by the I5 production gameplay correction:
   `dotnet test tests/PokeCorner.Core.Tests/PokeCorner.Core.Tests.csproj --logger 'console;verbosity=normal'`
   task-2b-fix1-core-gate.log — exit0,275/275. Existing acquisition/migration/save/refusal/commit/nursery/trait eligibility safeguards pass. This gate ran only after focused GREEN and the usage-limit resume; earlier completed37/248 runs were not repeated.
9. Self-review, `rtk git diff --check` and explicit staged-path diff check — exit0. Reviewed publication-before-refresh ordering, external-removal tolerance, unload/Loaded lifetime, dialog reuse without page/filter/form reset, once-only inference vs exact-empty migration, no loss of history-only IDs, shared canonical trait requirement, no berry/care spend changes and no per-refresh Corner config load. Only the10 owned/explicitly extended paths below were staged. Post-commit `git status --short` is empty.

### Fix1 files (10)

- src/PokeCorner.App/Popup/CollectionView.xaml.cs
- src/PokeCorner.App/Popup/RoomView.xaml.cs (root-authorized care-only extension)
- src/PokeCorner.Core/Domain/EvolutionCalculator.cs
- src/PokeCorner.Core/Domain/EvolutionService.cs
- src/PokeCorner.Core/Persistence/JsonSaveStore.cs
- src/PokeCorner.Core/Persistence/SaveData.cs
- src/PokeCorner.Core/Persistence/SaveMigration.cs
- tests/PokeCorner.App.Tests/GlobalUpdate/approvedCollectionTests.cs
- tests/PokeCorner.App.Tests/GlobalUpdate/approvedRoomAlbumTests.cs (root-authorized care-only coverage)
- tests/PokeCorner.Core.Tests/GlobalUpdate/formDiscoveryTests.cs

### Remaining boundaries

All five named findings are addressed in this commit; scoped independent re-review is still required. The earlier full App552/559 checkpoint remains honestly retained; this round supplies248/248 covering App, not a new full-App claim. Known starter failure is Task6; earlier hero timing evidence and whole-app manual/visual acceptance remain Task8; Russian facts remain11/1025 pending POKE-58. No real profile, new art, subagent/reviewer, actual app/MSI/installer/OS/autostart change, root docs/Kanban edit, release or push.
