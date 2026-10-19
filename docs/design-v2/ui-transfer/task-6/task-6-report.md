# Task 6 — Shop, Settings, save history and onboarding

Status: DONE_WITH_CONCERNS.
Worktree: C:/Users/Georgy/.codex/worktrees/global-update/PokeTokenBar-Windows
Branch: codex/global-update
BASE: 58c48cc08a47e62ec7c660bf6e80c6db558037fc
HEAD: edc909b11c9fcd8c8547cda65b92d6b29a97b77d
Source/test commit: edc909b11c9fcd8c8547cda65b92d6b29a97b77d

## Implemented behavior

- Shop uses approved paper/forest components and packaged real item art, with all 12 canonical goods, six berries, live wallet/stock, pack/item prices, positive quantities, total cost and shortages. The two-column composition retains reachable actions at 1000x760 and 840x650. Purchase confirmation uses shared async ConfirmAsync over current content. Double clicks are guarded before awaiting; cancel/unload/generation changes do not purchase. Failed writes retain the same operation ID for the same item/quantity; committed purchases clear it. A repaint failure after a successful commit explicitly says the purchase was saved, without inviting a duplicate purchase.
- Care and XP shop actions now lead to choosing an individual; no active-pet item application engine remains in ShopView. The existing Task2B exact card owns previews, busy/unsupported checks and confirmed item usage. Root explicitly authorized the narrow CollectionView.OpenItemSelection extension: existing detail/species closure helpers, existing owned catalog selection, Refresh; ordinary filters/query are retained. App navigates to Pokédex before calling it, so Loaded resumes presentation before refresh. A meaningful old-card -> shop Apply -> owned-catalog test first failed, then passed.
- Lucky-charm production action navigates to Task5's real source-specific Catch control. Catch still reserves/consumes through its established adapter. Existing optional standalone shop activation compatibility remains; no new item/economy mutator was introduced. Shop presentation replaces the inherited chance multiplier text with short cause/effect copy; Core balance and service semantics are unchanged.
- Settings retain Chill only, live size/voice/effect controls, fullscreen/progress preferences and privacy statement. Reset requires two explicit async confirmations in the shared overlay. First/second cancellation, pending repeated action and unload cannot reset. An appended getAutostart/setAutostart seam makes tests independent of actual registry writes; failed changes restore displayed status and show feedback.
- History shows actual local date, collection count, coins and memory-copy labels without exposing private checkpoint paths. Technical diagnostics are collapsed. Restore selection is frozen to the exact selected entry and requires two deliberate confirmations. Cancellation/close/unload returns no selection. Standalone startup recovery remains a real SaveHistoryWindow with the same in-content confirmation overlay, including Escape cancellation.
- Minimal AppMenuWindow hosted-content hook supports history and replayed onboarding without a new navigation framework. It returns to the prior Settings/content section, cancels on navigation/Escape/close, and keeps async confirmations usable. Completion is bound to the exact hosted element so delayed closure of an older wrapper cannot dismiss a later screen. Public constructor/navigation APIs remain compatible.
- In-menu recovery selects/confirms before freezing the menu/overlay. Only the actual replacement boundary stops growth, waits for commits, preserves failed-writer memory history and buffered expedition activity, flushes, and calls the EXISTING SaveMigration.RestoreHistory/RestartAfterReplacement path. Cancellation leaves growth/menu usable; finally releases freeze on exceptions. Failed-writer memory history is captured before listing and again at replacement for current activity. Startup no-menu invalid-save route remains separate and never silently starts a new profile.
- Six concise illustrated onboarding steps use existing packaged room, ball, grove, nursery and Pokémon assets. Step/back/skip commits precede displayed navigation; pending actions are guarded. Failed progress writes remain visible on the current step and allow a later attempt. Hosted unload/close suppresses late UI changes. Last step reads the committed gift queue in production; replay never grants gifts. Existing FirstRunFlow gift idempotency/real synthetic commit tests pass.
- StarterSelectionWindow now asks canonical frames FIRST. A single canonical frame is sufficient; legacy/static fetch happens only for missing/empty frames or absent optional provider. Real fallback null/error behavior is preserved. Closing during fetch prevents fallback/animator creation. The inherited throwing-network OfflineStarterUsesCanonicalFramesBeforeAnyLegacyPngFetch assertion is unchanged and passes.

## Real save-failure route

JsonSaveStore and BackgroundSaveWriter deliberately latch a real failed write. Ordinary Shop retry cannot repair that latch. IO/SaveLoadException/permission failures now direct the player to Settings -> history recovery, preserving the live wallet/stock and main file. The synthetic repaired-writer test explicitly proves writer.HasFailed and byte-for-byte main/live preservation BEFORE cleanup, then deliberately reloads the valid store baseline and reconstructs only its fixture writer. It does not change or bypass production guards. Stable operation identity is independently verified across a refused UI commit and that controlled repaired-baseline retry. Actual recovery/restart itself was NOT executed against a real profile.

## Ownership and source self-review

15 source/test files, all explicitly staged:
- src/PokeCorner.App/App.xaml.cs
- src/PokeCorner.App/Onboarding/OnboardingWindow.xaml
- src/PokeCorner.App/Onboarding/OnboardingWindow.xaml.cs
- src/PokeCorner.App/Onboarding/StarterSelectionWindow.xaml.cs
- src/PokeCorner.App/Popup/AppMenuWindow.xaml
- src/PokeCorner.App/Popup/AppMenuWindow.xaml.cs
- src/PokeCorner.App/Popup/CollectionView.xaml.cs (root-authorized narrow recipient adapter)
- src/PokeCorner.App/Popup/SaveHistoryWindow.xaml
- src/PokeCorner.App/Popup/SaveHistoryWindow.xaml.cs
- src/PokeCorner.App/Popup/SettingsView.xaml
- src/PokeCorner.App/Popup/SettingsView.xaml.cs
- src/PokeCorner.App/Popup/ShopView.xaml
- src/PokeCorner.App/Popup/ShopView.xaml.cs
- tests/PokeCorner.App.Tests/GlobalUpdate/shopTests.cs
- tests/PokeCorner.App.Tests/GlobalUpdate/onboardingTests.cs

Reviewed complete production diff, callers, cancellation/generation paths, commit-before-publish boundaries, hosted identity lifetime, quantity overflow/shortage delegation, selection of exact checkpoint/pet, privacy-safe history fields and preserved startup/backup engines. No Core production file changed. No Core gate was run. Existing assertions were not weakened: one old double-click purchase fixture appends an explicit accepting confirmation delegate so it still tests the same pending-commit guard.

rtk git diff --check exited 0 before and after final focused coverage. Index was empty before scoped staging. No other task edits were staged or reverted. Root owns docs/Kanban and the coordinated visual pass.

An automatic review initially mistook a Python SOURCE-TEXT edit of App.xaml.cs for actually invoking restore/reset/restart. It rejected the command before mutation. Read-only evidence from task-6-brief lines7/64 established explicitly authorized App adapter source scope, and the clarified source-only retry was approved. No app/profile operation was attempted.

## RED/GREEN commands and raw evidence

All test commands ran in the stated isolated worktree through rtk powershell -NoProfile -Command with scoped escalation. Each native dotnet command below used --logger "console;verbosity=normal", redirected COMPLETE output with *> to the named log under .superpowers/sdd/2026-10-17-approved-ui-transfer, captured $LASTEXITCODE in $task6Exit and returned that native exit. PowerShell raw logs are UTF-16. All failed logs remain untouched.

Project prefix for EACH filtered command:
dotnet test tests/PokeCorner.App.Tests/PokeCorner.App.Tests.csproj --filter "<exact filter below>" --logger "console;verbosity=normal"

1. task-6-red.log — exit1,0/3,1.7379s.
Filter: FullyQualifiedName~RefusedPurchaseRetryRetainsOperationIdentity|FullyQualifiedName~RepeatedTourActionWhileCommitPendingOnlySavesOnce|FullyQualifiedName~OfflineStarterUsesCanonicalFramesBeforeAnyLegacyPngFetch
Behavioral failures before source changes: different shop operation IDs on retry; pending tour calls2 instead of1; throwing "Legacy PNG/network fetch should not run." at StarterSelectionWindow line88.

2. task-6-first-focused.log — exit1, compile failure (no test count).
Filter: FullyQualifiedName~ShopTests|FullyQualifiedName~onboardingTests|FullyQualifiedName~OfflineStarterUsesCanonicalFramesBeforeAnyLegacyPngFetch|FullyQualifiedName~SaveHistoryWindowRenders
CS0176 at onboarding illustration HorizontalAlignment.Center; fixed by qualifying the WPF enum, no assertions changed.

3. task-6-focused2.log — exit1,63/64,4.1545s.
Same exact filter as2.
One real packaged illustration path failure on tour step3; changed Data/final/assets/expeditions/grove.png to actual established Data/expeditions/grove.png. Canonical offline starter and the initial two new regressions already passed.

4. task-6-focused3.log — exit0,161/161,7.2772s.
Filter: FullyQualifiedName~ShopTests|FullyQualifiedName~onboardingTests|FullyQualifiedName~OfflineStarterUsesCanonicalFramesBeforeAnyLegacyPngFetch|FullyQualifiedName~SaveHistoryWindowRenders|FullyQualifiedName~MigrationTests|FullyQualifiedName~SettingsViewModelTests|FullyQualifiedName~FirstRunFlowTests
Includes existing synthetic restore/reset/migration/save-protection and gift-queue guards, new confirmation cancellations, six illustrations, genuine null/empty/single/multi canonical fallback, explicit legacy failure and close-during-fetch semantics, registry-free injected settings, and two-size shop/settings/history reachability.

5. task-6-recipient-red.log — exit1,0/1,1.1050s.
Filter: FullyQualifiedName~ShopApplyClosesOldDetailAndReturnsToOwnedRecipientCatalog
Root-authorized recipient extension was not yet implemented. Old detail remained Visible when Collapsed was required; query/owned-catalog intent was preserved in the final regression.

6. task-6-covering-green.log — exit1, compile failure (no test count).
Filter: FullyQualifiedName~ShopTests|FullyQualifiedName~onboardingTests|FullyQualifiedName~ApprovedCollectionTests|FullyQualifiedName~OfflineStarterUsesCanonicalFramesBeforeAnyLegacyPngFetch|FullyQualifiedName~SaveHistoryWindowRenders|FullyQualifiedName~MigrationTests|FullyQualifiedName~SettingsViewModelTests|FullyQualifiedName~FirstRunFlowTests
New hook used ambiguous TabControl (WinForms/WPF); qualified WPF type. No behavior/assertion changed.

7. task-6-covering-green2.log — exit1,191/192,6.5686s.
Same exact filter as6.
Only new disk-retry fixture failed: expected95/actual100 after replacing writer but leaving JsonSaveStore's deliberate failure latch. Fixture correction explicitly reloads its valid baseline before reconstructing writer, retaining every main/live/stock/ID assertion. No engine/production guard was changed. Recipient route, identity-bound host completion, all other Task6/Task2B/save guards passed.

8. task-6-disk-retry-green.log — exit0,1/1,1.0798s.
Filter: FullyQualifiedName~UiPurchaseRefusedDiskWriteKeepsStockAndReusesIdentityAfterWriterRepair
Corrected real synthetic disk refusal/repaired-baseline fixture passes. No broad rerun.

9. ONE full App gate, task-6-app-gate.log — exit1,674/677,35.7494s.
Exact command:
rtk powershell -NoProfile -Command '$env:POKE_SHELL_CAPTURE_DIR=Join-Path $env:TEMP "pokecorner-task6-captures"; dotnet test tests/PokeCorner.App.Tests/PokeCorner.App.Tests.csproj --logger "console;verbosity=normal" *> .superpowers/sdd/2026-10-17-approved-ui-transfer/task-6-app-gate.log; $task6Exit=$LASTEXITCODE; Get-Content .superpowers/sdd/2026-10-17-approved-ui-transfer/task-6-app-gate.log -Tail 34; exit $task6Exit'
Process-only temporary capture directory for inherited fixtures; no persistent environment mutation. All Task6 tests and inherited offline starter passed.
Full gate is a preserved checkpoint, NOT a claim of all-green current full-suite acceptance.

10. Root-approved minimal post-gate Shop IO-error wording/assertion correction and ONE bounded diagnostic/covering run:
task-6-post-gate-focused.log — exit0,14/14,7.3821s.
Filter: FullyQualifiedName~RealWriteRefusalPreservesCharmAndPinnedQueueForSameOperationRetry|FullyQualifiedName~UiPurchaseRefusedDiskWriteKeepsStockAndReusesIdentityAfterWriterRepair|FullyQualifiedName~RefusedPurchaseRetryRetainsOperationIdentity|FullyQualifiedName~LeavingWhilePurchaseConfirmationPendingDoesNotSpend|FullyQualifiedName~ResetCannotContinueAfterSettingsUnloads|FullyQualifiedName~ClosingHistoryDuringPendingConfirmationKeepsSelectionEmpty|FullyQualifiedName~ClosingSecondHistoryPromptCannotSelectOrRestore|FullyQualifiedName~HostedHistoryKeepsMenuUsableAndCancelsPendingConfirmation|FullyQualifiedName~OldHostedCompletionCannotCloseNewContent|FullyQualifiedName~ClosingDuringPendingStepCommitKeepsDisplayedStepUntilRestart|FullyQualifiedName~RepeatedTourActionWhileCommitPendingOnlySavesOnce|FullyQualifiedName~TourSaveFailureRemainsOnCurrentStepAndAllowsRetry|FullyQualifiedName~ShopApplyClosesOldDetailAndReturnsToOwnedRecipientCatalog|FullyQualifiedName~RefreshFailureAfterConfirmedPurchaseNeverReportsRefusedWrite
This compiles/tests final source after the small error guidance change; no full App/Core repeat and no Catch source/test/timing/assertion change. Two pre-existing ApprovedCollection test warnings remain CS8602/xUnit2013.

## Preserved full-gate failures and bounded diagnosis

1. AcceptanceVisualTests.CaptureAssembledApplicationMatrix:
NullReferenceException, acceptanceVisualTests.cs:131, via WpfTestHost.cs:49. Old capture code looks up removed Expedition TeamPanel. Wholesale37-state capture replacement belongsTask8. No obsolete harness edit or new capture/cosmetic loop.

2. ShellTests.FinishReviewEvidenceRendersPopulatedPokedexAndResetArea:
InvalidCastException at shellTests.cs:260, Grid -> ScrollViewer because SettingsView now has the in-content local-overlay root. Root explicitly retained this as Task8 fixture replacement; production was not reverted to obsolete layout.

3. CatchTests.RealWriteRefusalPreservesCharmAndPinnedQueueForSameOperationRetry:
catchTests.cs:378 expected "Продолжить", actual "Бросить покебол"; full-gate raw stack retained at task-6-app-gate.log:695-726. The ONE root-requested named diagnostic passed inside the14-case covering run, with related Task6 host/lifecycle tests present.

Read superpowers:systematic-debugging before diagnosing3. No fix was guessed. The test's afterPublish callback sets confirmed before Open returns to CatchButton_Click; Until polls that value off the shared STA. CatchView assigns _pendingReveal only after await _tryPerformEncounter returns (CatchView.xaml.cs:206-215). The test then posts Skip and Unloaded to the shared WPF dispatcher; Skip returns if _pendingReveal is still null, and Unloaded can then see no pending reveal. A narrow signal/presentation handoff ordering race is plausible from source, but a latent race/root cause is NOT established by one passing diagnostic. Task6's App adapters are not used by this standalone Catch fixture; Catch source is unchanged, which by itself is not proof of irrelevance. Concrete Task8 follow-up: instrument afterPublish -> handler _pendingReveal assignment -> Skip/Unloaded and replace the unowned diagnostic's early signal observation with an explicit deterministic handoff barrier only after evidence/root ruling. Do not add sleeps, weaken captions or dismiss the full failure.

## Concerns / deferred acceptance

- Full App checkpoint remains674/677. Final-source focused14/14 does not replace that full result. No full-suite rerun erased failures.
- Whole-app37-state visual comparison, capture fixture replacement, shared scheduling/timing diagnosis and real manual interaction belongTask8. This source/test transfer does not certify pixel-perfect global acceptance or every illustration overlay.
- Historical save-loss/upgrade/MSI acceptance and real recovery/restart are NOT certified by synthetic UI tests. No real profiles, reset/restore, installer, autostart/registry writes, OS changes, release or push occurred.
- No subagents/reviewers, image generation, new art, duplicate inventory/save engine, Core production change, schema migration or backup-retention change.

## Final commit verification

rtk git diff --cached --check: exit0. Explicit15-file owned commit: edc909b11c9fcd8c8547cda65b92d6b29a97b77d, feat(ui): transfer shop settings recovery and onboarding. Post-commit branch codex/global-update and git status --short empty. Report/raw logs are ignored task evidence and remain on disk; no production/test edits after commit and no additional tests after the14-case covering run.
