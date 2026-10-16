# Приёмка T19 / POKE-35 — 2026-10-16

Независимое spec/quality review принято для code `c70fae5`. Ниже исходный отчёт реализации и доработок; старые промежуточные counts сохранены как история.

# T19 — global update onboarding and gift: implementer report

Date: 2026-10-16. Branch: `codex/global-update`. Starting commit: `05b5890`. This report and the `task-19-*.log` files live in the ignored SDD directory. Source/test commit: see handoff at the end.

## Delivered behavior

- New global-update tutorial has the six approved steps. The revision is `global-update-v1`, independent of the gift's existing `global-update-compatible-pair-v1` grant ID. On first entry the tour starts automatically, commits its current step before display, and persists next/back/skip/complete before changing the window. Close/Esc leaves the last saved step for the next start. Settings opens a read-only replay at step 1, with no gift, resource, or tutorial write.
- Contextual first-view hints use the existing `Tutorial.SeenHintKeys` store for Pokédex, expeditions, nursery, shop, and achievements. Their key is committed before notification. Catch is explained in tour step 2 instead of a first-open hint: a first-open write at Catch could hold the T18 click/reveal path behind a menu commit. The final narrow suite covers both this mapping and T18 deferred catch/reopen.
- A genuinely new profile receives a direct-issued starter, 20 base coins and 5 treats, and the two compatible closed gift balls in one candidate commit before any live publish. A canceled selection does not commit: `_hasLoadedState` stays false, and the OnExit/SaveState guards cannot persist an empty live snapshot. On relaunch, absence of both save files/traces remains a new profile. A persisted reset's pending marker also retains the pack across close/restart before selection.
- An existing empty v1 collection may select a starter while retaining its resources. An old v1 `StarterSelectionPending` flag is normalized away on migration, so it cannot independently claim the new/reset pack. A nonempty collection with no active individual selects an existing available individual; it does not mint a starter. If all are unavailable, they remain intact and the app reports that honestly. No old runaway Chill is resurrected; Chill decode compatibility remains in existing migration code.
- Gift issuance reuses `SaveMigration.ReserveGift` through one small `EnsureGift` adapter. Existing two-gift records short circuit, partial records fail closed, canonical recipe/root metadata stays with the T13 engine, and no parallel grant store exists. Existing/evolved/shiny individuals and resources survive; repeat, skip, minor updates, and replay do not repeat the gift or spend resources. Tutorial demos do not execute game operations.
- Commit failures leave the live starter/active/tour view unchanged. Existing T02 save candidate, foreign baseline, backup, restore, and read-failure barriers were not relaxed. This is synthetic coverage only; no real user profile, MSI, OS install, network, or visual DPI acceptance was touched. The reported FriendChill loss remains cause-unproven, and no absolute disk guarantee is claimed.

## Source ownership / scope

Changed `src/PokeCorner.App/Onboarding/FirstRunFlow.cs`, `OnboardingWindow.xaml`, `OnboardingWindow.xaml.cs`, and direct startup/tour adapters in `src/PokeCorner.App/App.xaml.cs`. Small necessary extensions: `src/PokeCorner.Core/Persistence/SaveMigration.cs` exposes the existing gift engine and clears legacy pending semantics; `src/PokeCorner.App/Popup/AppMenuWindow.xaml.cs` emits the viewed section for existing hint metadata. Changed `tests/PokeCorner.App.Tests/Onboarding/FirstRunFlowTests.cs`; added `tests/PokeCorner.App.Tests/GlobalUpdate/onboardingTests.cs`. No controller-owned docs, ledger, board, or visual design assets changed. The dispatch shorthand `SDDimplementer-prompt.md` was initially unresolved; the template was then read at `C:/Users/Georgy/.codex/plugins/cache/claude-plugins-official/superpowers/6.4.1/skills/subagent-driven-development/implementer-prompt.md`, alongside the full task-19 brief and canonical documents.

## RED evidence before production changes

All commands below ran from this managed worktree through RTK proxy. The first attempt itself failed to compile because my fixture omitted required `Nature`, `IsShiny`, and `CaughtAtUtc` members; that setup mistake is kept separately and is not claimed as behavioral RED.

| Log | Command / selection | Actual exit | Result |
| --- | --- | ---: | --- |
| `task-19-red-attempt1.log` | `rtk proxy dotnet test tests/PokeCorner.App.Tests/PokeCorner.App.Tests.csproj --no-restore --filter FullyQualifiedName~GlobalUpdate.onboardingTests --verbosity minimal` | 1 | Fixture compilation error; not behavioral proof. |
| `task-19-red-attempt2.log` | Same command after fixture completion | 1 | 0 passed / 3 failed: old tour had seven steps; nonempty old pending was treated as starter; old tutorial completion suppressed the new revision. |
| `task-19-red-legacy-pending.log` | `rtk proxy dotnet test tests/PokeCorner.App.Tests/PokeCorner.App.Tests.csproj --no-restore --filter FullyQualifiedName~LegacyPendingFlagDoesNotConvertExistingEmptyProfileIntoNewGame --verbosity minimal` | 1 | 0 passed / 1 failed: v1 migration retained a stale pending flag. |

Each command redirected all output to its listed unique SDD log (`*> .superpowers/sdd/2026-10-11-global-update-transition/<log>`). The legacy-pending regression was added after the initial three-case RED capture, before its production fix.

Relevant raw failure lines: `task-19-red-attempt2.log` recorded `Expected: 6 / Actual: 7` for the tour and `Expected: False / Actual: True` for existing collection starter selection; its summary was `не пройдено 3, пройдено 0`. `task-19-red-legacy-pending.log` recorded `Expected: False / Actual: True`. Each failure directly described the behavior later changed.

## Verification chronology and actual exits

The focused command, with its changing test count as cases were added, was `rtk proxy dotnet test tests/PokeCorner.App.Tests/PokeCorner.App.Tests.csproj --no-restore --filter FullyQualifiedName~GlobalUpdate.onboardingTests --verbosity minimal` (redirected to each unique log below). `attempt1` and `attempt2` exited 1 on my test compile ambiguities (`KeyEventArgs`, `MessageBox`); both were corrected without production changes. `attempt3` exited 0 at 8/8; `attempt4` exited 0 at 12/12; `focused-keyboard` exited 0 at 13/13; `focused-final` exited 0 at 14/14. After the close-during-pending-commit guard and regression were added, `focused-postclose` exited 0 at 15/15.

| Log | Exact command (append `*> .superpowers/sdd/2026-10-11-global-update-transition/<log>` to capture raw output) | Actual exit / proof |
| --- | --- | --- |
| `task-19-core-final.log` | `rtk proxy dotnet test tests/PokeCorner.Core.Tests/PokeCorner.Core.Tests.csproj --no-restore --verbosity minimal` | 0, 256/256. No Core production changes after this. |
| `task-19-app-final.log` | `rtk proxy dotnet test tests/PokeCorner.App.Tests/PokeCorner.App.Tests.csproj --no-restore --filter "FullyQualifiedName!~FinishReviewEvidenceRendersPopulatedPokedexAndResetArea" --verbosity minimal` | 1, 521/522. Sole failure: retained `CollectionView_HeroSprite_MultiFrameSpecies_ActuallyCyclesFrames` observed one frame in ~700 ms. T12/T15/T18 saw this intermittent failure; cause remains unproven. No threshold, assertion, filter, or animation production change was made. |
| `task-19-hero-isolation.log` | `rtk proxy dotnet test tests/PokeCorner.App.Tests/PokeCorner.App.Tests.csproj --no-restore --filter FullyQualifiedName~CollectionView_HeroSprite_MultiFrameSpecies_ActuallyCyclesFrames --verbosity minimal` | 0, 1/1. |
| `task-19-app-final-retry.log` | Same App command as `app-final` | 0, 523/523. Between these whole-suite runs production gained the close-during-pending-commit guard and tests gained that regression. This is not a code-unchanged retry. No more whole-suite repetitions were run. |
| `task-19-postgate-focused.log` | A broad post-gate focused OR selection for T19 and T18, accidentally without the known capture-test exclusion. The exact OR string was not retained in the tool record; the raw output log is retained, and no rerun was done merely to reconstruct the command. | 1, 45/46; the only failure was the preexisting `FinishReviewEvidenceRendersPopulatedPokedexAndResetArea` environment-sensitive capture test. This was a filter mistake, not a new product failure. Raw log retained. |
| `task-19-postgate-focused-filtered.log` | `rtk proxy dotnet test tests/PokeCorner.App.Tests/PokeCorner.App.Tests.csproj --no-restore --filter "(FullyQualifiedName~GlobalUpdate.onboardingTests|FullyQualifiedName~Onboarding.FirstRunFlowTests|FullyQualifiedName~DeferredReopenDuringSavedCatchReceivesConfirmedResultBeforeCommitReturns|FullyQualifiedName~ConfirmedCatchRemainsVisibleWhenPlaybackIsInterrupted|FullyQualifiedName~FiveSectionsAndSettingsKeepCatchAndSoundControlsReachable)&FullyQualifiedName!~FinishReviewEvidenceRendersPopulatedPokedexAndResetArea" --verbosity minimal` | 0, 29/29. |

The one-line Catch hint mapping removal and tour step-2 copy update happened after `app-final-retry`. Thus 523/523 is the whole-App proof before those final focused edits; 29/29 is the final-source relevant proof, including first-view mapping/tour and T18 confirmed catch/deferred reopen. The capture exclusion is the same preexisting whole-App exclusion; it was not broadened to hide a T19 or T18 failure.

The final raw focused summary is `Пройден! : не пройдено 0, пройдено 29, пропущено 0, всего 29`. Initial fixture and test-namespace compile errors were corrected; no warning or failure appears in the passing final logs.

## Self-review / handoff

Checked `rtk git diff --check` (exit 0), own source/test scope, and no other changes in this worktree. The existing save validator still requires tutorial revision/status/step; T13 root metadata and T02 barriers remain in the shared save pipeline. Restart-before-first-selection, cancel/Alt+F4, close during an in-flight tour write, nonempty/no-active, unavailable companions, and no duplicate gift have synthetic regressions. The final whole visual/type/color/layout/DPI batch belongs to T20. Known intermittent hero GIF one-frame test and environment-sensitive capture test remain visible in their raw failure logs; causes not asserted. Controller should independently review and update board/ledger after accepting this source commit.

Source/test commit SHA: `5ba72031adca91bb004c4375ec8a4911e46147d3` (`feat(onboarding): persist global update tour and gift`). `rtk git status --short --branch` after commit returned only `## codex/global-update` (exit 0): clean tracked/untracked worktree; the SDD report/logs are intentionally ignored.

## Review fix round 1 — closed gift delivery (2026-10-16)

Review source: `task-19-review.md`, scoped to `5ba7203`. The reviewer confirmed that T19 saved two reserved gift individuals but the product catch path only selected activity encounters and the Catch view only enabled Open for activity count. Controller ruled that making the gifts usable is T19 delivery. This round also strengthens the close-during-commit WPF regression's post-completion assertion.

### Behavioral RED before production patch

All commands in this section used the same managed worktree, `rtk proxy`, `--no-restore`, and raw output redirection to the uniquely named SDD log. Their tool-returned exit codes are given explicitly:

| Log | Exact command before redirection | Exit / raw evidence |
| --- | --- | --- |
| `task-19-fix1-red-gift.log` | `rtk proxy dotnet test tests/PokeCorner.App.Tests/PokeCorner.App.Tests.csproj --no-restore --filter FullyQualifiedName~StarterGiftAppearsAsTwoClosedReadyBallsAndOpensDurablyWithoutActivityCatch --verbosity minimal` | 1; `Expected: 2 / Actual: 0` sealed gift encounters after starter commit; 0/1. Expected because the gift record had no catch queue adapter. |
| `task-19-fix1-red-catchview.log` | `rtk proxy dotnet test tests/PokeCorner.App.Tests/PokeCorner.App.Tests.csproj --no-restore --filter FullyQualifiedName~CatchViewOffersAlreadyReservedGiftWhenNoActivityBallIsReady --verbosity minimal` | 1; `Expected: True / Actual: False` for the WPF Open button with a reserved gift and zero activity balls; 0/1. Expected because CatchView used only activity count. |

Each exact invocation appended `*> .superpowers/sdd/2026-10-11-global-update-transition/<log>`; failed logs were retained and no test assertion was weakened after observing RED.

### Fix and GREEN

`FirstRunFlow` now links each still-reserved gift individual to a sealed `EncounterService.ReserveGift` encounter in the same startup candidate commit that grants or restores the gift. The stable operation ID is the gift individual's ID; an existing encounter for that gift is reused even if its older operation ID differs. Startup also detects already-granted, unqueued gifts when an existing pet is active. `CatchBallQueue` is the small App adapter shared by the actual `App.TryPerformCatchAsync` selector, the tray/menu ready count, the WPF CatchView ready count, and the integration test. It offers gift balls first and then sealed activity balls. CatchView shows only counts and explains that a shiny charm does not alter a frozen gift; before Open the encounter stores no species, individual, or shiny result in the visible queue. `EncounterService.Open` remains the only opener and runs inside `AppState.CommitAsync`; T18's confirmed DTO still publishes in the `afterPublish` callback before deferred menu work. Gift opening does not decrement `PendingCatchCount`, consume an activity encounter, alter `ActivityNewGuaranteed`, spend resources, or switch the active pet. Restart/replay retains opened status and does not duplicate the gift.

The WPF close-during-pending-commit test now observes the window **after** releasing the pending task on the UI dispatcher and asserts it remains closed with the same displayed step. The new gift test runs a starter candidate commit, inspects two sealed balls and hidden result fields, checks the real WPF CatchView button/status, forces an open-write failure and verifies live/disk remain sealed, adds a separate activity ball, opens each gift through the App-shared selection adapter and candidate commit, reloads from disk, retries open idempotently, and verifies the activity ball/counter/novelty guarantee, resources, and active pet survive. This is synthetic App/UI/adapter coverage; it does not claim a real installed profile or a physical click.

| Log | Exact command before redirection | Actual exit / output |
| --- | --- | --- |
| `task-19-fix1-focused1.log` | `rtk proxy dotnet test tests/PokeCorner.App.Tests/PokeCorner.App.Tests.csproj --no-restore --filter "FullyQualifiedName~GlobalUpdate.onboardingTests" --verbosity minimal` | 0, 17/17 (first implementation). |
| `task-19-fix1-focused2.log` | Same focused command after shared queue adapter and failure/restart assertions | 0, 17/17. |
| `task-19-fix1-relevant.log` | `rtk proxy dotnet test tests/PokeCorner.App.Tests/PokeCorner.App.Tests.csproj --no-restore --filter "(FullyQualifiedName~GlobalUpdate.onboardingTests|FullyQualifiedName~GlobalUpdate.encountersTests|FullyQualifiedName~GlobalUpdate.reactionsTests|FullyQualifiedName~Onboarding.FirstRunFlowTests|FullyQualifiedName~FiveSectionsAndSettingsKeepCatchAndSoundControlsReachable)&FullyQualifiedName!~FinishReviewEvidenceRendersPopulatedPokedexAndResetArea" --verbosity minimal` | 0, 58/58; T19 tour/gift, existing encounter contract, T18 confirmed reveal/deferred reopen, shell catch access, and post-completion close assertion. |
| `task-19-fix1-catchcopy.log` | `rtk proxy dotnet test tests/PokeCorner.App.Tests/PokeCorner.App.Tests.csproj --no-restore --filter FullyQualifiedName~CatchViewOffersAlreadyReservedGiftWhenNoActivityBallIsReady --verbosity minimal` | 0, 1/1 after final gift/charm explanation. |
| `task-19-fix1-app-final.log` | `rtk proxy dotnet test tests/PokeCorner.App.Tests/PokeCorner.App.Tests.csproj --no-restore --filter "FullyQualifiedName!~FinishReviewEvidenceRendersPopulatedPokedexAndResetArea" --verbosity minimal` | 1, 524/525. Sole failure was the known hero GIF one-frame observation (`saw 1 distinct frames`, 981 ms), cause unproven. No collection animation source/test/threshold/filter edits. |
| `task-19-fix1-hero-isolation.log` | `rtk proxy dotnet test tests/PokeCorner.App.Tests/PokeCorner.App.Tests.csproj --no-restore --filter FullyQualifiedName~CollectionView_HeroSprite_MultiFrameSpecies_ActuallyCyclesFrames --verbosity minimal` | 0, 1/1. |
| `task-19-fix1-app-final-retry.log` | Same unchanged full-App command as `fix1-app-final` | 0, 525/525. This was the one controller-authorized unchanged retry; no more whole-suite repeats. |

Raw failure and pass logs are retained with their exact names. Core production code did not change in this fix round; the prior direct Core 256/256 remains applicable. The only full-App exclusion is the preexisting environment-sensitive `FinishReviewEvidenceRendersPopulatedPokedexAndResetArea`. GIF scheduling cause is still unproven, and T20 owns its recurrent triage plus the single full visual/type/color/layout/DPI pass.

Fix-round self-review: read the own diff and checked gift reservation identity, old pre-granted/unqueued migration, publish-after-persist ordering, sealed UI copy, activity accounting, replay/idempotency, and WPF post-close behavior. Source scope is `App.xaml.cs`, `Onboarding/FirstRunFlow.cs`, `Popup/CatchView.xaml.cs`, new `Popup/CatchBallQueue.cs`, and `GlobalUpdate/onboardingTests.cs`; no Core source, design docs, board, or ledger edits. `rtk git diff --check` and `rtk git diff --cached --check` returned exit 0. Exact own five-file staging was checked with `rtk git status --short` and cached stat (170 insertions, 16 deletions). Fix-round commit: `c70fae5ed5485cde029b3d90f17f5c6e2bc5dbc4` (`fix(onboarding): make reserved gift balls catchable`). Post-commit `rtk git status --short --branch` returned only `## codex/global-update` (exit 0), with ignored SDD evidence intentionally outside Git.


## Independent review evidence: task-19-review.md

### Spec Compliance

- ❌ Issues found: T19 reserves two gift individuals but never makes either closed ball available to the player. `FirstRunFlow.CompleteStarter` and `RestoreCompanionAndGift` call `EnsureGift` (src/PokeCorner.App/Onboarding/FirstRunFlow.cs:35, :46), while the only product catch path selects a sealed **activity** encounter (src/PokeCorner.App/App.xaml.cs:654-661). `CatchView` enables Open only from the activity count (src/PokeCorner.App/Popup/CatchView.xaml.cs:79-95). The existing gift queue API `EncounterService.ReserveGift` (src/PokeCorner.Core/Domain/EncounterService.cs:74-92) has no production caller. Therefore the promised two closed balls cannot be opened, despite durable reservation. Connect the reserved gift IDs to the existing catch menu through a candidate commit and display the combined ready queue without revealing species or individual properties before Open.
- ✅ The six steps, independent tutorial revision and gift ID, persisted next/back/skip/complete, read-only Settings replay, fresh versus existing resources, stale v1 pending normalization, unavailable-individual preservation, and candidate commit behavior match the task's state contract (src/PokeCorner.App/Onboarding/OnboardingWindow.xaml.cs:12-20, :80-115; src/PokeCorner.App/Onboarding/FirstRunFlow.cs:10-58; src/PokeCorner.App/App.xaml.cs:143-151, :177-242; src/PokeCorner.Core/Persistence/SaveMigration.cs:69-70, :198-205).
- ⚠️ Cannot verify from this task diff: real profile/MSI/OS acceptance and the complete visual, type, color, layout and DPI matrix are scheduled for T20. The last full App pass (523/523) predates the final Catch hint mapping/copy edit; the final relevant focused run is 29/29. The earlier whole-App 521/522 hero-animation failure passed in isolation, and the accidental broad post-gate 45/46 included the known environment-sensitive capture case; neither cause is proven by this review (.superpowers/sdd/2026-10-11-global-update-transition/task-19-report.md; task-19-*.log).

### Strengths

- Startup does not persist a canceled first selection: `_hasLoadedState` remains false until after the tour and `PersistState` guards the exit/crash path (src/PokeCorner.App/App.xaml.cs:197-208, :242, :1325-1333). The existing save loader also rejects missing primaries with history/temporary traces (src/PokeCorner.Core/Persistence/JsonSaveStore.cs:142-155).
- The gift uses the existing recipe/root engine and rejects partial gift journals; previously migrated resources and owned GUIDs are not recreated (src/PokeCorner.Core/Persistence/SaveMigration.cs:167-205; src/PokeCorner.App/Onboarding/FirstRunFlow.cs:29-46). `AppState.CommitAsync` validates and writes the candidate before `Hydrate` publishes it (src/PokeCorner.Core/AppState.cs:66-96).
- The tour commits a step before changing its displayed page, retains a closed window's previous display, and reports write errors (src/PokeCorner.App/Onboarding/OnboardingWindow.xaml.cs:94-115). Catch was deliberately omitted from the first-view hint mapping, leaving its confirmed reveal path untouched (src/PokeCorner.App/App.xaml.cs:905-929; :654-670).

### Issues

#### Critical (Must Fix)

- None.

#### Important (Should Fix)

- **Gift is inaccessible** — src/PokeCorner.App/App.xaml.cs:654-661 and src/PokeCorner.App/Popup/CatchView.xaml.cs:79-95. The app queues only activity balls in the catch path and never calls `EncounterService.ReserveGift`. Add the smallest App/CatchView adapter that exposes the two reserved gift balls as closed ready catches and commits each gift encounter/open using the existing service. Test the real startup → gift queue → Open → restart flow, including no duplicate grant/open and hidden properties before throwing.

#### Minor (Nice to Have)

- **Close-during-commit regression asserts only the pre-close display** — tests/PokeCorner.App.Tests/GlobalUpdate/onboardingTests.cs:300-317. After `pending.SetResult()`, it performs an empty dispatcher call without asserting that the closed window stays closed or that no late page update occurs. Assert the observable post-completion state on the WPF dispatcher; the production `_closed` guard itself is present (src/PokeCorner.App/Onboarding/OnboardingWindow.xaml.cs:99-103).

### Assessment

**Task quality:** Needs fixes.

**Reasoning:** State migration, durability and tour navigation are sound in this diff, with reported Core 256/256 and final focused App 29/29 passing. The gift remains a persisted record with no user-facing throw path, so T19's independent gift delivery is incomplete. I inspected only focused unchanged contracts for startup save guards, commit publication, gift encounter callers, menu hint/catch lifetime and the WPF test dispatcher; no tests were rerun.


## Independent review evidence: task-19-rereview1.md

### Finding Verdicts

- **Gift is inaccessible — ADDRESSED.** `FirstRunFlow.QueueReservedGifts` now maps each reserved gift to a sealed encounter using its stable individual ID and reuses an older encounter already linked to that gift (src/PokeCorner.App/Onboarding/FirstRunFlow.cs:22-33, :45, :55). Startup repairs a prior grant with no encounter (src/PokeCorner.App/App.xaml.cs:216-221); gift-first `CatchBallQueue.NextReady` drives the actual `TryPerformCatchAsync` candidate Open (src/PokeCorner.App/Popup/CatchBallQueue.cs:7-16; src/PokeCorner.App/App.xaml.cs:654-670). The tray/menu badge and CatchView button/count include sealed gift balls, with no species text before Open (src/PokeCorner.App/App.xaml.cs:305, :641-642, :667-668, :990; src/PokeCorner.App/Popup/CatchView.xaml.cs:69-104, :133). The App test covers confirmed starter grant, sealed queue, failed write, gift-first opening, restart/idempotency, and activity/resource/active preservation (tests/PokeCorner.App.Tests/GlobalUpdate/onboardingTests.cs:215-319). That test exercises the App-shared selector and WPF button state; it does not physically click the installed app.
- **Close-during-commit post-completion assertion — NOT ADDRESSED.** The new assertions inspect the closed window after `pending.SetResult()` (tests/PokeCorner.App.Tests/GlobalUpdate/onboardingTests.cs:405-425), but `WpfTestHost.Run` uses synchronous `Dispatcher.Invoke` (tests/PokeCorner.App.Tests/Theme/WpfTestHost.cs:20-35). That invocation can run before the async `MoveAsync` continuation posted by the task completion, so the test can pass without exercising the late `_closed` guard. The production guard is present (src/PokeCorner.App/Onboarding/OnboardingWindow.xaml.cs:99-103); defer the fixture repair to T20's test batch. A completion signal from the move or a lower-priority dispatcher assertion would make the check deterministic.

### New Breakage in the Fix Diff

- None found. The selector counts only unopened gift encounters plus the existing activity count and prefers gifts; `EncounterService.Open` remains inside `AppState.CommitAsync` with `PublishConfirmedCatch` in `afterPublish` (src/PokeCorner.App/Popup/CatchBallQueue.cs:7-16; src/PokeCorner.App/App.xaml.cs:654-670). Gift opening does not consume the activity counter in the existing service (src/PokeCorner.Core/Domain/EncounterService.cs:132-144).
- Reported evidence: gift and CatchView behavioral RED 0/1 each; final relevant 58/58 and final copy 1/1; App 524/525 had the same unproven hero one-frame failure, isolated 1/1, then one unchanged App retry 525/525. No suite was rerun in this review (.superpowers/sdd/2026-10-11-global-update-transition/task-19-report.md; task-19-fix1-*.log).

### Out-of-Scope Observations

- Real installed-profile and physical-click acceptance, plus the full visual/DPI matrix, remain T20 work. The recurring hero GIF test's cause is unproven; the fix touched no collection animation code (src/PokeCorner.App/Popup/CatchView.xaml.cs:69-104; .superpowers/sdd/2026-10-11-global-update-transition/task-19-fix1-app-final.log).

### Verdict

**Fix round:** All Important findings addressed, no new Critical/Important breakage. The remaining Minor fixture-ordering weakness is deferred to T20; it does not extend this T19 fix loop.
