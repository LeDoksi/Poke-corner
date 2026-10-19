# Task 5 — catch and encounter transfer

Status: DONE_WITH_CONCERNS. Source committed; the one final App gate is deliberately retained as Exit 1.

## Source identity and ownership

- Worktree: C:\Users\Georgy\.codex\worktrees\global-update\PokeTokenBar-Windows
- Branch: codex/global-update
- Exact BASE: e4de4d72244af18e3088905f22a4d3cb94329e02
- Exact HEAD / source commit: 6ca78beb51a9779c176ef08411f1990ed4711db0
- Commit: feat(ui): transfer catch choreography and pinned encounters
- Only seven owned files staged/committed:
  - src/PokeCorner.App/App.xaml.cs
  - src/PokeCorner.App/Popup/CatchBallQueue.cs
  - src/PokeCorner.App/Popup/CatchReveal.cs
  - src/PokeCorner.App/Popup/CatchView.xaml
  - src/PokeCorner.App/Popup/CatchView.xaml.cs
  - src/PokeCorner.App/Popup/ExpeditionView.xaml.cs
  - tests/PokeCorner.App.Tests/GlobalUpdate/catchTests.cs
- Final source/test diff: 845 insertions, 250 deletions, seven files. Core production unchanged. No other person's paths staged or reverted. This ignored report/evidence stays outside the source commit for root documentation handling.

## Implemented behavior

Closed activity/gift shows the approved closed ball PNG and unknown caption. Neither a species name nor result-art fetch is accessed before the committed result is ready for display. Gift-specific wording states the result is frozen. Known resolved expedition entry shows its public species and exact normal/shiny image, but SelectEncounter and the optional ExpeditionView entry callback perform navigation only.

SelectEncounter accepts only the exact resolved expedition with known species and saved individual; it pins its ID. Throw supplies that exact ID to the App adapter. The candidate also checks exact ID/source/readiness before the established EncounterService.Open producer runs. The generic queue still prioritizes gift then activity, with resolved unopened expeditions included afterward; explicit expedition selection cannot accidentally open the oldest gift/activity. Refused writes retain the pinned selection. The existing public ExpeditionView open delegate remains compatible as fallback for existing callers.

The App candidate-save callback publishes CatchReveal only after the candidate is confirmed. CatchReveal now has an optional EncounterId; matching pending receipt requests reuse that result, while a different operation cannot overwrite an unacknowledged catch. Same-operation save/service receipts stay authoritative. ReadyBallCount includes unopened resolved expeditions; after confirmed expedition actions the existing tray/shell SetCatchReady APIs receive the current count.

The choreography uses Task1 UiBallClosedImage/UiBallOpenImage and packaged route/Pokémon bytes, with no redraw or CodePen import:
flight 1100 ms → capture 650 ms → landing 800 ms → three wobbles of 1000 ms → success 700 ms = 6250 ms.
Each wobble settles by 55% and holds still through the remainder. Unknown identity remains inaccessible throughout these phases. Known title is retained even after the producer hydrates the encounter to opened. Result/Continue resets scene, captions and art before the next item. Skip displays the SAME committed reveal and never calls the producer.

The Windows ClientAreaAnimation preference is read without modifying OS state. Reduced motion uses one calm 250 ms delay. Unload cancels phase delays, stops/removes transform/opacity clocks, detaches the refresh Tick and Windows preference event, and invalidates image callbacks. Repeated Loaded subscribes once. Pending commit completion after unload/reload restores its confirmed result without starting hidden/new choreography. Slow old art cannot overwrite a later closed scene.

The explicit charm control shows current stock and the reserved active unit. Its callback uses existing ShopService.TryActivateLuckyCharm inside CommitStateAsync; the UI never mutates inventory. The confirmed activation reserves stock; final eligible consumption remains the canonical producer's responsibility. Frozen gift/expedition text does not promise a reroll; unsupported shiny preservation stays in the existing service. No probabilities/formulas appear in catch text/tooltips.

Catch-specific art first reads CatalogPackage.GetAssetPath(id, shiny), then permits normal-only legacy/network fallback if normal canonical art is absent. Unavailable shiny returns no image and never substitutes normal. This avoids changing the shared overlay/provider outside ownership. The existing lucky-charm PNG and route PNGs are reused.

## Test strategy and cases

Criticality HIGH (durable grant/inventory); Gate0 OFF, unit ON, integration ON, component ON, independently deployed contract OFF, deployment smoke OFF, property OFF (bounded phase/source/state partitions). Small xUnit unit/STA tests plus medium local temporary-save integration tests were selected. No actual profile/application/installer, network-dependent new integration, snapshot comparison or OS-setting mutation was added. The existing final App suite is the regression gate; a global visual/manual pass remains Task8.

RED tests exercise source partitions (gift/activity unknown vs known expedition), readiness transitions, exact phase durations/order, one confirmed result under rapid click/skip/reopen, pinned refusal retry, commit/navigation lifetime, explicit charm stock reservation and calm reduced motion.

21 final Catch cases also cover exact canonical normal/shiny bytes, unavailable shiny without normal fallback, delayed sprite after reset, repeated-load detachment, navigation-only expedition entry, known caption after receipt publication, and real temporary-file refusal/retry/commit-before-reopen with no duplicate grant. Existing encounter, onboarding, reactions, shop and expedition tests retain frozen results, unsupported-art guards, standalone/root acquisition and service receipts.

## Commands, raw logs, results

All shell commands were prefixed rtk. Worktree writes/build/git used scoped sandbox escalation. Test commands run from the worktree above through rtk proxy python -c; subprocess stdout+stderr is written as original bytes before the native return code is returned. DOTNET_CLI_UI_LANGUAGE=en-US is process-only on all runs after the first fixture build. POKE_SHELL_CAPTURE_DIR WAS NOT SET by this runner.

Exact native argv and the executable wrapper are preserved in task-5-logs/commands.json. The display tail used on longer runs does not truncate the raw files.

Common command:
`rtk proxy python -c "import subprocess,pathlib,sys,os;os.environ['DOTNET_CLI_UI_LANGUAGE']='en-US';r=subprocess.run(['dotnet','test','tests/PokeCorner.App.Tests/PokeCorner.App.Tests.csproj','--filter',FILTER,'--logger','console;verbosity=normal'],stdout=subprocess.PIPE,stderr=subprocess.STDOUT);pathlib.Path(LOG).write_bytes(r.stdout);print(r.stdout.decode('utf-8',errors='replace'));print('EXIT',r.returncode);sys.exit(r.returncode)"`

| Raw log under task-5-logs | Result | Native exit |
|---|---|---|
| red.log | fixture compile failure, CS9035 Nature/IsShiny/CaughtAtUtc; no tests | 1 |
| red-behavior.log | actual behavioral RED: Total 11; Failed 11 | 1 |
| green-1.log | Total 11; Passed 11 | 0 |
| focused-2.log | new fixture Settings/import compile failures; no tests | 1 |
| focused-3.log | new fixture Individuals compile failure; no tests | 1 |
| focused-4.log | Total 101; Passed 101, including 21 Catch cases | 0 |
| final-app-gate.log | Total 638; Passed 635; Failed 3; all Catch21 pass | 1 |

The focused filter is exactly:
`FullyQualifiedName~CatchTests|FullyQualifiedName~EncountersTests|FullyQualifiedName~OnboardingTests|FullyQualifiedName~ReactionsTests|FullyQualifiedName~ShopTests|FullyQualifiedName~ExpeditionCoreTests`.

The ONE final App gate native argv:
`dotnet test tests/PokeCorner.App.Tests/PokeCorner.App.Tests.csproj --logger "console;verbosity=normal" --logger "trx;LogFileName=task5-app.trx" --results-directory .superpowers/sdd/2026-10-17-approved-ui-transfer/task-5-logs/results`
executed by the same rtk proxy Python raw-log wrapper with NO --filter. Raw final output states Test Run Failed / Total tests: 638 / Passed: 635 / Failed: 3 / Total time: 46.5648 Seconds. The wrapper returned EXIT 1.
TRX: task-5-logs/results/task5-app.trx.

Final three failures retained by exact name:
1. PokeCorner.App.Tests.GlobalUpdate.acceptanceTests.OfflineStarterUsesCanonicalFramesBeforeAnyLegacyPngFetch — InvalidOperationException: Legacy PNG/network fetch should not run; StarterSelectionWindow.xaml.cs:88. Existing Task6 defect, untouched.
2. PokeCorner.App.Tests.GlobalUpdate.ShellTests.FinishReviewEvidenceRendersPopulatedPokedexAndResetArea — Assert.False(string.IsNullOrWhiteSpace(output)), shellTests.cs:211. Runner did not set process-only POKE_SHELL_CAPTURE_DIR.
3. PokeCorner.App.Tests.GlobalUpdate.AcceptanceVisualTests.CaptureAssembledApplicationMatrix — same missing-env assertion, acceptanceVisualTests.cs:22.

The two capture fixtures stopped before their visual harness. This does NOT establish that the already known stale TeamPanel/FirstParentsPanel/SecondParentsPanel harness is fixed. Root explicitly ruled to retain this single full gate, perform no repeat/full visual loop, and hand both the runner setup gap and stale harness to Task8. Task8 will configure a process-only temporary synthetic capture directory when replacing that harness.

Both known prior warnings remain: approvedCollectionTests.cs:283 CS8602 and :255 xUnit2013. No assertions were weakened or excluded to obtain green.

There was also an interrupted sandbox mutation attempt before the resumed user authorization: automatic review could not run because of usage limit; no source mutation occurred in that rejected attempt. Resume corrected the same fixture checkpoint; no work was restarted.

## Self-review and remaining concerns

Read the owned production diff (including unabridged CatchView lifecycle diff), authored tests and caller/source interactions before staging. Verified compatible appended optional dependencies, exact ID/source binding, candidate-save publication before deferred reopen, a single reserved charm source of authority, hidden callback generations and same-result Skip/Restore. Final rtk proxy git diff --check exited 0. The final App gate compiled the completed source, including the small exact-source and post-claim badge publication changes after focused101; no source changed after that gate.

No subagents/reviewer were dispatched. Root owns independent functional/code review and final documentation/Kanban. No new art, actual profile, installer, OS/autostart/reset/restore mutation, push or release.

Concerns: the App gate is Exit1 as stated, with one existing starter defect and two concrete runner setup failures. Assembled rhythm/sound, global37-state visual/DPI/manual acceptance and the stale capture harness remain Task8. No claim of live visual acceptance, actual save acceptance, complete Russian flavor facts or MSI acceptance is made.
