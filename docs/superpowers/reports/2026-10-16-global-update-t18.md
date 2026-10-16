# Приёмка T18 / POKE-39 — 2026-10-16

Независимое spec/quality review принято для code `b649712`. Ниже исходный отчёт реализации и доработок; старые промежуточные counts сохранены как история.

# T18 implementation handoff — reactions and confirmed reveal playback

Status: implemented and locally verified on `codex/global-update` from `473f558`; source/test commit `330fa64acf94a8ef6165c63a387aacf4850288c1`. This report is ignored SDD evidence, not an acceptance mark. No live profile, MSI, OS setting, network, or Kanban operation was performed.

## Source scope

- `src/PokeCorner.App/Overlay/OverlayWindow.xaml` and `.xaml.cs`: bounded, click-through heart and feeding-crumb elements; replacement animation instead of accumulating visual children; permanent yarn thought stages after 3, 4.5 and 5.75 idle hours; system `ClientAreaAnimation` changes take effect while running; reaction bounds stay in the selected monitor work area.
- `src/PokeCorner.App/App.xaml.cs`: updates thought from saved active-care interaction time; keeps cry and crunch on separate players with separate saved volumes, stops a prior cry, plays crunch after confirmed feed, and retains an in-session display-only `CatchReveal` across a real `AppMenuWindow` close/reopen until Continue. The existing encounter `CommitAsync` completes before this display DTO is set. No new reward, RNG or persistence operation occurs in playback.
- `src/PokeCorner.App/Popup/CatchView.xaml` and `.xaml.cs`: throw, shake, open and result/Continue playback; cancellation or motion-off exposes the already confirmed result; a fresh view accepts that same DTO via `RestoreReveal`, with Continue acknowledging it. Sprite fetch cannot block Continue and art failure does not hide a saved result.
- `src/PokeCorner.App/Popup/NurseryView.xaml` and `.xaml.cs`: necessary Hatch supporting adapter, with egg shake/open/result/Continue only after the existing saved hatch action confirms the frozen child. Closing/reopening shows the saved result; no extra hatch grant/commit/RNG. Existing T12 archive, traits, luck and closed child state remain in their original service/save flow.
- `tests/PokeCorner.App.Tests/GlobalUpdate/reactionsTests.cs`: synthetic WPF host tests for playback interruption/recreated menu, Continue acknowledgement, slow art, bounded/click-through effects, yarn thresholds, hatch result and packaged crunch asset/window geometry.
- `tests/PokeCorner.App.Tests/GlobalUpdate/evolutionTests.cs`: bounded fixture correction after the final App gate exposed an observer selecting the first `ConfirmDialog` regardless of visibility/title. It now selects the visible retraining confirmation by exact title and stops its timer before assertions. A separate negative test confirms a hidden unrelated dialog is present in `Application.Current.Windows` but rejected by that selector. Production retraining and assertions were not weakened. This selector defect is demonstrated; its causal connection to the particular full-suite scheduling failure is consistent with evidence but not proven.

## Test evidence

All commands below ran in `C:\Users\Georgy\.codex\worktrees\global-update\PokeTokenBar-Windows` with per-command `require_escalated`. Logs are literal raw output under this directory. Each pipeline ended in `exit $LASTEXITCODE`, and the exits below are the returned process exits, not inferred from the summary text.

| Phase | Actual command before `2>&1 \| Tee-Object -FilePath '<log>'; exit $LASTEXITCODE` | Raw log | Result |
| --- | --- | --- | --- |
| Initial catch RED | `rtk proxy dotnet test tests/PokeCorner.App.Tests/PokeCorner.App.Tests.csproj --no-restore --filter FullyQualifiedName~ReactionsTests --verbosity normal` | `task-18-red.log` | exit 1; 1 passed, 1 failed (interrupted confirmed catch showed Open instead of Continue) |
| Hatch behavioral RED | `rtk proxy dotnet test tests/PokeCorner.App.Tests/PokeCorner.App.Tests.csproj --no-restore --filter FullyQualifiedName~InterruptedHatchCanContinueFromItsSavedChild --verbosity minimal` | `task-18-red-hatch.log` | exit 1; 0 passed, 1 failed (missing Hatch Continue control) |
| First focused GREEN | `rtk proxy dotnet test tests/PokeCorner.App.Tests/PokeCorner.App.Tests.csproj --no-restore --filter FullyQualifiedName~ReactionsTests --verbosity minimal` | `task-18-focused.log` | exit 0; 9/9 |
| Expanded focused GREEN | same focused command | `task-18-focused-final.log` | exit 0; 11/11 |
| Pre-reopen postreview focused GREEN | same focused command | `task-18-focused-postreview.log` | exit 0; 12/12 |
| Core final, unchanged since | `rtk proxy dotnet test tests/PokeCorner.Core.Tests/PokeCorner.Core.Tests.csproj --no-restore --verbosity minimal` | `task-18-core-final.log` | exit 0; 256/256 |
| Pre-reopen App gate | `rtk proxy dotnet test tests/PokeCorner.App.Tests/PokeCorner.App.Tests.csproj --no-restore --filter 'FullyQualifiedName!~FinishReviewEvidenceRendersPopulatedPokedexAndResetArea' --verbosity minimal` | `task-18-app-final.log` | exit 0; 501/501 |
| Pre-reopen App gate after image-failure correction | same App command | `task-18-app-postreview.log` | exit 0; 502/502 |
| Recreated-menu behavioral RED | `rtk proxy dotnet test tests/PokeCorner.App.Tests/PokeCorner.App.Tests.csproj --no-restore --filter FullyQualifiedName~RecreatedCatchViewCanShowTheSameConfirmedResultWithoutAnotherOpen --verbosity minimal` | `task-18-red-reopen.log` | exit 1; 0 passed, 1 failed (fresh CatchView had no RestoreReveal) |
| Post-reopen focused GREEN | focused command above | `task-18-focused-postreopen.log` | exit 0; 13/13; includes Continue acknowledgement and no repeat open |
| Post-reopen App failed gate | App command above | `task-18-app-postreopen.log` | exit 1; 481 passed, 1 failed, 482 run; retraining observer assertion then unhandled timer exception aborted host |
| Retraining isolation | `rtk proxy dotnet test tests/PokeCorner.App.Tests/PokeCorner.App.Tests.csproj --no-restore --filter FullyQualifiedName~ConfirmedPersonalCardRetrainingCallsRealProducer --verbosity minimal` | `task-18-retraining-isolation.log` | exit 0; 1/1 |
| Observer correction plus negative case | `rtk proxy dotnet test tests/PokeCorner.App.Tests/PokeCorner.App.Tests.csproj --no-restore --filter 'FullyQualifiedName~RetrainingDialogObserverIgnoresHiddenUnrelatedDialog\|FullyQualifiedName~ConfirmedPersonalCardRetrainingCallsRealProducer' --verbosity minimal` | `task-18-retraining-observer.log` | exit 0; 2/2 |
| Final App gate | App command above | `task-18-app-final-postreopen.log` | exit 0; 504/504 |

The first attempted hatch RED had a fixture compile error (missing required `IsShiny` initialization); the corrected behavioral RED reused `task-18-red-hatch.log` and overwrote that first output. The earlier compile-error raw output is **not retained** and is not represented as behavioral RED evidence. `task-18-app-postreopen.log` remains a failed raw gate and is not renamed or counted as passing. No extra App exclusions were used beyond the one known capture-dependent `FinishReviewEvidenceRendersPopulatedPokedexAndResetArea` case.

## Self-review and handoff

The playback only reads confirmed outcomes; the existing encounter and nursery save paths issue and persist them first. Catch `RestoreReveal` is display-only. New egg/result UI is a minimal adapter for required Hatch behavior; visual/DPI whole-screen polish and real interactive review remain with T20. The WPF synthetic tests do not prove multi-monitor live DPI, animation aesthetics, or speaker output. A process restart preserves the existing granted catch in saved state but does not resume its in-session animation DTO; a normal menu close/reopen in the same process does restore it.

Final source checks from the same workdir, all actual exit 0: `rtk git diff --check`, `rtk git diff --cached --check`, explicit `rtk git add --` of the nine source/test paths listed above, and `rtk git commit -m "feat: add shared reactions and confirmed reveal playback"` (commit `330fa64acf94a8ef6165c63a387aacf4850288c1`). Post-commit `rtk git rev-parse HEAD` returned that SHA with exit 0, and `rtk git status --short` returned no paths with exit 0. The ignored report/raw logs remain outside the source commit for controller review.

## Fix round 1 — independent review findings

Base commit `330fa64acf94a8ef6165c63a387aacf4850288c1`. Both Important findings in `task-18-review.md` were addressed together in `App.xaml.cs` with synthetic regressions in `reactionsTests.cs`.

- An untouched active individual now uses its saved `CaughtAtUtc` as the idle baseline when no `CareState` exists. Existing care records still supply `LastInteractionUtc`; no per-tick invented timestamp resets the stage.
- The catch display DTO is now published by the `AppState.CommitAsync` post-persist callback before deferred `RunWhenReady` menu creation. It is not published before the writer succeeds, and the saved grant/encounter operation is unchanged. The real controlled-writer test queues a close/reopen during the commit and checks that the first new CatchView shows Continue, with one saved individual and no second open.

All fix-round commands below used the same managed workdir and `require_escalated`; each had `2>&1 | Tee-Object -FilePath '.superpowers/sdd/2026-10-11-global-update-transition/<raw-log>'; exit $LASTEXITCODE`. Every filename names a distinct attempt. The numbers below are returned actual exits and raw test counts.

| Phase | Exact test command before the common Tee/exit suffix | Raw log | Result |
| --- | --- | --- | --- |
| Initial idle fixture attempt | `rtk proxy dotnet test tests/PokeCorner.App.Tests/PokeCorner.App.Tests.csproj --no-restore --filter FullyQualifiedName~UntouchedPetUsesSavedObtainedTimeAcrossRefreshTicks --verbosity minimal` | `task-18-fix1-red-idle-attempt1.log` | exit 1; fixture compile CS9035, missing required `SaveData.Individuals`; not behavioral RED |
| Corrected idle behavioral RED, before production edit | same idle command | `task-18-fix1-red-idle.log` | exit 1; 0/3, expected yarn 1/2/3, actual 0 on all three |
| Deferred menu behavioral RED, before production edit | `rtk proxy dotnet test tests/PokeCorner.App.Tests/PokeCorner.App.Tests.csproj --no-restore --filter FullyQualifiedName~DeferredReopenDuringSavedCatchReceivesConfirmedResultBeforeCommitReturns --verbosity minimal` | `task-18-fix1-red-deferred.log` | exit 1; 0/1, first reopened view showed `Открыть покебол` instead of `Продолжить` |
| Focused GREEN | `rtk proxy dotnet test tests/PokeCorner.App.Tests/PokeCorner.App.Tests.csproj --no-restore --filter FullyQualifiedName~ReactionsTests --verbosity minimal` | `task-18-fix1-focused.log` | exit 0; 17/17 |
| First App gate | `rtk proxy dotnet test tests/PokeCorner.App.Tests/PokeCorner.App.Tests.csproj --no-restore --filter 'FullyQualifiedName!~FinishReviewEvidenceRendersPopulatedPokedexAndResetArea' --verbosity minimal` | `task-18-fix1-app-final.log` | exit 1; 507 passed, 1 failed / 508 total: existing 700-ms hero-frame observation saw one frame |
| Hero-frame isolation | `rtk proxy dotnet test tests/PokeCorner.App.Tests/PokeCorner.App.Tests.csproj --no-restore --filter FullyQualifiedName~CollectionView_HeroSprite_MultiFrameSpecies_ActuallyCyclesFrames --verbosity minimal` | `task-18-fix1-hero-isolation.log` | exit 0; 1/1 without changes |
| One unchanged-code App retry | same App command | `task-18-fix1-app-unchanged-retry.log` | exit 0; 508/508, no further exclusions |

The failed App raw log remains failed evidence; the exact scheduling cause of the first hero-frame observation is unproven. The test pumps WPF for a fixed 700 ms and passed in isolation and the one unchanged-code full retry. A bounded source check found the new still-effect timer stops on `OverlayWindow.Closed`, Catch and Nursery refresh timers stop on `Unloaded`, and their playback cancellation runs on `Unloaded`; yarn opacity intentionally repeats while an overlay exists. No CollectionView/animator code or test threshold was changed. Live motion, fullscreen, multi-monitor DPI and speaker checks remain T20 work. Core source did not change; the earlier 256/256 Core gate remains valid.

Fix1 finalization: `rtk git diff --check` returned exit 0 before staging; `rtk git add -- src/PokeCorner.App/App.xaml.cs tests/PokeCorner.App.Tests/GlobalUpdate/reactionsTests.cs` returned exit 0 and staged exactly those two owned paths. After resumption, `rtk git diff --cached --check` returned exit 0. `rtk git commit -m "fix: preserve idle thought and catch reveal across deferred menu"` returned exit 0 and created `b649712ae4c0acabce3a62abf03c8da5ea13497d`. Post-commit `rtk git rev-parse HEAD` returned that SHA with exit 0; `rtk git status --short` returned no paths with exit 0. The intervening first commit attempt was not executed because automatic approval review hit a usage limit; no alternate path or bypass was used. Scoped independent rereview should compare `330fa64acf94a8ef6165c63a387aacf4850288c1..b649712ae4c0acabce3a62abf03c8da5ea13497d`.


## Independent review evidence: task-18-review.md

### Spec Compliance

- ❌ Issues found: saved idle time does not advance for an active pet without a CareState (`src/PokeCorner.App/App.xaml.cs:751-753`); a confirmed catch can be absent from the newly reopened menu when reopening is queued during its commit (`src/PokeCorner.App/App.xaml.cs:642-644,890-891`). Both violate T18's durable outcome / permanent yarn behavior.
- ⚠️ Cannot verify from this task diff: actual fullscreen hiding, cross-monitor DPI placement, speaker output, and saved EffectsSoundVolume migration belong to unchanged T04/T20 paths. The source-level WPF checks here do not establish live acceptance.

### Strengths

- `src/PokeCorner.App/Popup/CatchView.xaml.cs:139-235`: reveal playback uses an already confirmed `CatchReveal`, cancellation exposes its result, and late sprite loading cannot block Continue.
- `src/PokeCorner.App/Popup/NurseryView.xaml.cs:382-461`: hatch animation starts only after the saved hatch and child are checked; interruption preserves a visible result.
- `src/PokeCorner.App/Overlay/OverlayWindow.xaml:42-56` and `OverlayWindow.xaml.cs:400-493`: reusable three-element effects are bounded and hit-test transparent; the thought stage persists between animation cycles. Focused check of the existing `ThoughtBubbleStyle` in `OverlayWindow.xaml:12-19` confirms the bubble is also hit-test transparent.
- `src/PokeCorner.App/App.xaml.cs:696-744`: cry and crunch use separate players and their saved volume settings, including zero. The raw final logs show 13/13 focused, 256/256 Core, and 504/504 filtered App tests passing, without warning lines. The earlier 481/1 App failure remains documented, and the observer fixture correction at `tests/PokeCorner.App.Tests/GlobalUpdate/evolutionTests.cs:17-35,130-142` selects a visible exact-title dialog and stops its timer before asserting.

### Issues

#### Critical (Must Fix)

- None.

#### Important (Should Fix)

1. `src/PokeCorner.App/App.xaml.cs:751-753`: when the active individual has no CareState, `last` is set to the current tick's `now`, so idle time stays zero forever until the first pet/feed. Focused check: `EncounterService` and `NurseryService` do not create CareStates; `AppState.cs:244-251` only hydrates records that exist, and App creates a record on care actions at `App.xaml.cs:665,777`. Use the obtained pet's saved timestamp (for example `CaughtAtUtc`) as the stable baseline when care is absent. Add one synthetic case for an untouched pet crossing 3, 4.5, and 5.75 hours across refresh ticks.
2. `src/PokeCorner.App/App.xaml.cs:642-644,890-891`: `_pendingCatchReveal` is assigned after `await CommitAsync`, but `AppState.cs:84-90` drains deferred `OpenMenu` calls before that await returns. If the user closes the old menu and reopens it during the save, `OpenMenu` queues at `App.xaml.cs:870-874`; its new CatchView is built before the DTO exists and will not refresh it when the DTO arrives. The current menu can show zero ready balls and no Continue despite a confirmed catch. Publish the display DTO in the post-persist callback before deferred menu creation, or update an already recreated CatchView once the DTO exists. Test the real `AppState.RunWhenReady` / commit order with close-reopen before release of a controlled writer, and assert the first reopened menu exposes the saved result without another open.

#### Minor (Nice to Have)

- `tests/PokeCorner.App.Tests/GlobalUpdate/reactionsTests.cs:100-194`: tests establish bounded child counts, static click-through flags, and a primary work-area fit, but do not exercise live fullscreen, dynamic reduced-motion transitions, zero volume, or multi-monitor hit testing. Keep those explicit in T20's live verification; a synthetic test of the settings paths would close the T18 coverage gap.

### Assessment

**Task quality:** Needs fixes

**Reasoning:** Outcome playback and interruption handling are sound in the tested path, but the missing care baseline and deferred-menu ordering break two normal user flows. No suites were rerun; this review inspected the supplied diff once and only the named unchanged code and retained logs needed to evaluate these risks.


## Independent review evidence: task-18-rereview1.md

### Finding Verdicts

- **Untouched pet never reaches yarn thresholds without CareState** — ADDRESSED. `src/PokeCorner.App/App.xaml.cs:761-764` now uses the persisted `active.CaughtAtUtc` whenever care is absent, while existing care still supplies `LastInteractionUtc`. `tests/PokeCorner.App.Tests/GlobalUpdate/reactionsTests.cs:24-52` hydrates an active pet without care and invokes two refreshes at each 3/4.5/5.75-hour stage; retained behavioral RED was 0/3 and final focused run was 17/17.
- **Deferred menu reopening misses confirmed catch** — ADDRESSED. `src/PokeCorner.App/App.xaml.cs:635-654` creates and stores the display DTO in `CommitAsync`'s post-persist callback, before the deferred `OpenMenu` queue drains. Focused check of unchanged `BackgroundSaveWriter.cs:52-61` confirms `publish()` is called only after `await CommitAsync(data)` succeeds; a failed save therefore cannot publish this DTO. `tests/PokeCorner.App.Tests/GlobalUpdate/reactionsTests.cs:54-117` uses a controlled writer, queues `RunWhenReady` during the commit, and confirms the first reopened view shows Continue with one persisted individual and no second open. Retained behavioral RED was 0/1; final focused run was 17/17.

### New Breakage in the Fix Diff

- None. The fix changes only `App.xaml.cs` and its synthetic regressions; the save operation and playback remain separate.

### Out-of-Scope Observations

- The first filtered App gate reported 507/508: `ThemeRenderingTests.CollectionView_HeroSprite_MultiFrameSpecies_ActuallyCyclesFrames` saw one frame in a fixed 700-ms observation. Its scheduling cause is unproven. The unchanged test then passed 1/1 in isolation and the unchanged filtered App gate passed 508/508 on one retry; no CollectionView/animator code, thresholds, or additional exclusions changed. Retain this raw failure for final review.
- Live fullscreen, multi-monitor DPI, speaker output, and dynamic Windows motion verification remain with T20, as in the prior review.

### Verdict

**Fix round:** All findings addressed, no new Critical/Important breakage.
