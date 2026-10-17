# Task 1 — DONE_WITH_CONCERNS
Date: 2026-10-17. Checkout: C:UsersGeorgy.codexworktreesglobal-updatePokeTokenBar-Windows
Branch: codex/global-update. Source review base: 99ae3e3 (approved art checkpoint after 296e009).
No pre-existing dirty paths at first elevated inspection; resume had only this task's untracked approvedShellTests.cs.
No service, save schema, Core, artwork, real profile, installer, autostart, push or release changes.

## Implementation
- AppMenuWindow: approved 1000x760 default / 840x650 minimum, initial work-area clamping, 52-DIP brand/wallet/encounters/settings/chrome row, 54-DIP equal five-tab row in approved order with reference vector icons.
- MenuSection enum numeric values and Navigate, SelectTab, ShowCatchReveal, SetCatchReady, SectionViewed contracts retained. Corner/Catch/Achievements content retained with nested headers removed via selected-content template.
- Optional final Func<SaveData> getSnapshot constructor argument; production App construction supplies current state. Wallet refreshes on construction/navigation/selection/load and one-second lifetime-bound timer. No callback displays a dash, not demo money.
- Existing catch-ready update drives encounter count/button route/badge. Badge animation stops on unload and respects reduced motion.
- ConfirmationOverlay: in-window asynchronous Task<bool> host, retained/disabled background, cancel initial focus, cycling Tab focus, Escape cancellation, close/unload cancellation, duplicate prompt rejection, focus restoration. No persistence.
- UiSystem.xaml: semantic paper/forest/ink/border/danger roles, shell/header/button templates, focus/disabled states, six BitmapImage resources. PokedexTheme merges it so both production App.xaml and existing STA host resolve identical resources and all legacy keys remain valid. App.xaml therefore needed no duplicate merge.
- Six exact approved PNGs are linked Resource items directly from docs/design-v2/ui-system/assets (no copying or registry). Shared normal secondary min40, primary min48.
- Existing shell index/render fixtures updated for approved visible order/sizes; synthetic factory shared internally for direct-API tests.

## Owned changed paths (10)
src/PokeCorner.App/App.xaml.cs
src/PokeCorner.App/PokeCorner.App.csproj
src/PokeCorner.App/Popup/AppMenuWindow.xaml
src/PokeCorner.App/Popup/AppMenuWindow.xaml.cs
src/PokeCorner.App/Popup/ConfirmationOverlay.xaml
src/PokeCorner.App/Popup/ConfirmationOverlay.xaml.cs
src/PokeCorner.App/Theme/PokedexTheme.xaml
src/PokeCorner.App/Theme/UiSystem.xaml
tests/PokeCorner.App.Tests/GlobalUpdate/approvedShellTests.cs
tests/PokeCorner.App.Tests/GlobalUpdate/shellTests.cs

## TDD and verification
All commands prefixed rtk; writes/builds/git in assigned isolated checkout used scoped escalation.
1. Initial rtk dotnet test tests/PokeCorner.App.Tests/PokeCorner.App.Tests.csproj --filter FullyQualifiedName~ApprovedShellTests --no-restore: exit1, 0 passed/2 failed; expected missing ConfirmAsync and old820px width. task-1-red.log retained.
2. Raw rtk proxy dotnet test same filter: exit1, 0/2; first wallet insertion missed newline match; corrected. task-1-red-complete.log later confirmed0/3 but human interrupted tool so its process exit unknown. Human STOP preserved tests/no production edits; checkpoint report was written.
3. Explicit resume at99ae3e3: rtk proxy dotnet test same filter: exit1, 0 passed/3 failed, task-1-red-resumed.log.
4. Added real shown-window focus/Tab/Escape test before production: same command exit1, 0 passed/4 failed, task-1-red-focus.log. Expected feature absence.
5. First GREEN compile: exit1 (duplicate ShopTab after Unicode shell encoding mismatch; ambiguous WPF/Forms UserControl). task-1-green-first.log retained. Corrected encoding and fully qualified UserControl.
6. GREEN same filter: exit0, 4 passed, 0 failed; task-1-green-second.log. Four xUnit1031 warnings on synchronous task-result assertions; removed by asserting task completion before reading result in shared helper.
7. rtk proxy dotnet test tests/PokeCorner.App.Tests/PokeCorner.App.Tests.csproj --filter 'FullyQualifiedName~ApprovedShellTests|FullyQualifiedName~ShellTests|FullyQualifiedName~ThemeRenderingTests' --no-restore: exit0, 113 passed, 0 failed/0 skipped; task-1-focused.log.
8. Additional lifecycle coverage verifies live wallet updates and no snapshot reads after close; direct API replaced RED reflection bridges. Intermediate compile exit1 because Assert.Throws overload inferred Func<Task>; fixed with Action body. Exact error CS0619/xUnit2014: You must call Assert.ThrowsAsync<T> (and await the result) when testing async code. The intermediate task-1-focused-final.log was accidentally overwritten by the successful rerun; its tool output remains in session history, not a separate raw file. No test was disabled.
9. Final focused same command: exit0, 114 passed/0 failed/0 skipped, no warnings; task-1-focused-final.log.
10. Single full gate: rtk proxy dotnet test tests/PokeCorner.App.Tests/PokeCorner.App.Tests.csproj --no-restore: exit1, 540 passed/2 failed/0 skipped, total542, 28seconds. Full raw output retained task-1-full-app.log.
11. rtk git diff --check: exit0, no whitespace errors. Final owned-path staging/commit verification recorded below after commit.

Focused/full runs set POKE_SHELL_CAPTURE_DIR to .superpowers/sdd/2026-10-17-approved-ui-transfer/task-1-functional-captures because an existing ShellTests evidence test requires that environment variable. Existing functional fixtures generate native synthetic captures as part of their assertions; no app launched against user data. Inspected the regular and compact images from that single feature batch; no iterative whole-screen cosmetic capture loop or full visual acceptance claimed.

## Full-gate failures / diagnosis
- PokeCorner.App.Tests.GlobalUpdate.acceptanceTests.OfflineStarterUsesCanonicalFramesBeforeAnyLegacyPngFetch: Legacy PNG/network fetch should not run. Unchanged StarterSelectionWindow.LoadSpritesAsync line88 explicitly calls _fetchSprite before _fetchAnimatedFrames; belongs to later starter/onboarding scenario transfer. No actual fetch succeeds in this test: the synthetic delegate throws by design.
- PokeCorner.App.Tests.GlobalUpdate.acceptanceTests.ExistingHeroFrameAssertionSurvivesACompetingSlowUiFixture: the hero sprite never changed frame (saw 1 distinct frames). Existing standalone ThemeRenderingTests.CollectionView_HeroSprite_MultiFrameSpecies_ActuallyCyclesFrames passed in focused batch. Competing fixture wraps that same unchanged assertion; timing/root cause is not established. Shared host/animation/assertions untouched. Do not label whole App suite green.
Root notified of both failures; no repeated full-gate loop and no weakened unrelated GIF test.

## Self-review and limitations
Reviewed complete owned diff against99ae3e3 and Task1 contracts: only production App change is optional snapshot wiring; enum/service/delegate/save compatibility retained. Resource paths resolve through the same theme in synthetic STA tests. Async prompt cannot implicitly accept on navigation/close; timer and badge animation stop on unload/close. Ordinary synchronous Ask callers intentionally migrate with their own later scenario tasks.
Task1 tests cover live/navigation wallet, approved order/encounter route, hidden nested headers, cancel/confirm/duplicate/close, retained disabled background/navigation refusal, cancel focus/Tab cycle/Escape/focus restoration.
Fifth timer lifecycle test was added as verification after four-feature RED/GREEN, not claimed as a separate RED cycle.
No Core repeat (unchanged), real-save/app/install/manual OS interaction, full37-state visual acceptance, or cross-monitor hardware validation. Full suite has the two explicit failures above.
## Commit verification
Owned-path commit: 38da5d5f5dd16e5c642998c94768926b80f7c4d4; git commit exit0, 10 files changed/421 insertions/31 deletions. git rev-parse HEAD confirms commit; git status --short empty immediately afterward. Report/logs remain in ignored planning directory and were not staged. No push.
## Bounded acceptance diagnosis requested by root
Current head38da5d5. One diagnostic command only:
rtk proxy dotnet test tests/PokeCorner.App.Tests/PokeCorner.App.Tests.csproj --filter 'FullyQualifiedName~OfflineStarterUsesCanonicalFramesBeforeAnyLegacyPngFetch|FullyQualifiedName~ExistingHeroFrameAssertionSurvivesACompetingSlowUiFixture|FullyQualifiedName~CollectionView_HeroSprite_MultiFrameSpecies_ActuallyCyclesFrames' --no-restore
Actual exit1; 2 passed/1 failed/0 skipped, total3, duration1second.
Raw output retained task-1-acceptance-diagnostic.log.
Failed again: OfflineStarterUsesCanonicalFramesBeforeAnyLegacyPngFetch with Legacy PNG/network fetch should not run; StarterSelectionWindow.LoadSpritesAsync line88. Both the standalone hero frame assertion AND ExistingHeroFrameAssertionSurvivesACompetingSlowUiFixture passed in this combined diagnostic.
Scoped git diff --exit-code99ae3e3 HEAD for StarterSelectionWindow.xaml.cs, Popup/CollectionView.xaml.cs, Overlay/SpriteAnimator.cs and Theme/WpfTestHost.cs produced no diff, actual exit 0. These four source files are byte-identical in Git to the assigned task base. The full owned-path diff also lists no edits to these files or either failing acceptance test.
Evidence supports a stable starter-loading acceptance gap in code retained from baseline; the competing GIF failure was not reproduced by this focused run. Its root cause remains unestablished; source equality and this pass do not prove that the full-run failure is unrelated to Task1, or that it is flaky. No baseline test execution or full-suite rerun was performed.
No source/test changes; working tree was clean during diagnosis. Earlier report wording calling GIF unchanged describes source equality only, not a proven causal exclusion.