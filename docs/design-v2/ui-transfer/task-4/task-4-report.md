# Task 4 — approved nursery, exact parent draft, waiting and committed baby

Status: DONE_WITH_CONCERNS. Date: 2026-10-19.
Worktree: C:/Users/Georgy/.codex/worktrees/global-update/PokeTokenBar-Windows
Branch: codex/global-update
BASE: aa7e332d2e7f57d77cc86b82732dfa87a4e64b7f
HEAD/commit: 04246b40bc5e7be04cc0131c3753fb68324666ab - feat(ui): transfer approved nursery and exact parent selection

## Whole feature batch

- First visit has two genuinely empty slots and real Choose/Change buttons. Full native parent selection is a draft: first/second editing controls, search by species/nickname/exact padded or unpadded number, rarity and shiny filters, species groups then exact individuals, Save pair/Cancel/Escape. Cancel preserves the applied pair. Species pages contain at most24 groups, individual pages12; groups decode no sprites. The per-view static sprite cache is bounded128 keys; berry art has a separate six-item cache.800 individuals including200 duplicate species remain searchable/paged, and page/filter/group state survives returning where useful. All-zero numeric queries remain zero results rather than becoming an empty substring.
- Exact individual GUIDs remain tags/producer identifiers only. Visible name/number/nickname, form, level, date and a stable local specimen ordinal distinguish actual specimens, including same-species normal/shiny parents. First-parent unavailable cards explain desktop/busy/unsupported/no-recipe restrictions. Second-parent rows contain only distinct, available compatible individuals using the actual NurseryCatalog recipe index. Save rechecks current membership/availability and canonical Preview. Runtime Preview/Start/Hatch still own recipes, traits, resources, timings, optional offspring-charm selection and frozen child.
- Accepted packaged UiNurseryImage / UiNurseryWaitingImage / UiNurseryReadyImage form the whole greenhouse scene. Preparation and waiting use the approved split composition with parent cards; ready expands the cracked-egg scene and has one local «Вылупить» action. Egg/crack stay integrated in the original scene; the old separate ellipse/egg shake/sticker is removed. No new art or resource-byte changes. Actual existing berry images, Russian berry labels/quantities, canonical waiting time, inherited trait effects and optional talisman price/shortage are visible. Possible children are actual canonical roots, including pure evolved-parent families and standalone families, without an evolved direct-acquisition claim.
- Possible normal-form outcome names/images use exact NormalSpeciesIds from accepted Task2B. Global/shiny discovery never opens the ordinary form. Undiscovered names remain numbered/unknown; frozen true child is never read for preview/wait/ready presentation. Luck uses the live saved bonus normalized to canonical ShinyRules cap in a friendly bar and words. No gameplay percentages/probability/coefficient text or tooltips remains; exact quantities/time/level remain.
- Hatch retains stable nest/operation IDs across refusal/retry, verifies successful publication, saved hatch receipt and matching archived child, then displays that real committed baby in the SAME greenhouse. A short450ms opacity reveal follows Windows ClientAreaAnimation; Continue exposes the archived result/card/explicit desktop/new-nest actions. Reloaded already-hatched saves show their real archived result directly. Missing archives preserve the existing unavailable warning and disabled result actions. Failed proposed writes publish no baby, stock spend or discovery metadata; retries preserve the frozen child and avoid duplicated charm spend/child.
- Timer callbacks, async start/hatch/ready/activation results and messages use suspension/lifetime generation guards. Unload suspends before further presentation, stops the timer, abandons draft selection, detaches the Windows motion-policy event and removes the baby animation clock. External Refresh while suspended is inert; late successful global persistence remains valid without reviving hidden UI. Loaded resumes current persisted state. The reveal has no delayed continuation/animation-completion callback that could resurrect the page. No ordinary confirmation is needed for the explicit start/hatch actions; no MessageBox, new confirmation Window, nested dispatcher bridge or async confirmation was added.

## Root-authorized narrow extensions and exact contracts

1. App.xaml.cs: root explicitly rejected UI-only availability checking for an operation queued behind progress persistence. StartNurseryAsync now passes ValidateNurseryStartCandidate into the EXISTING CommitNurseryAsync/CommitStateAsync candidate callback, immediately before canonical Start. It checks current candidate active companion and actual current normal/shiny asset eligibility. No writer/commit-publication path, Core/economy/schema or RNG implementation was added or changed. Existing completed-operation keys bypass ONLY these new-acquisition preconditions; canonical Start remains responsible for fingerprint/replay validation. A queued active change now refuses without stock/child publication; the same request succeeds after the condition clears. A committed replay leaves price/frozen child intact even if parents later become active; a changed fingerprint still refuses.
2. reactionsTests.InterruptedHatchCanContinueFromItsSavedChild ONLY: old test programmatically clicked Continue AFTER Unloaded and required the hidden view to mutate. Root ruled that accepted lifetime semantics forbid that hidden update. The test now asserts unloaded Continue/external Refresh leave the hidden result unchanged and no animation clock survives; Loaded restores the saved archived baby/result, and a fresh view also resumes it. Original child identity, single owned child, one hatch receipt and confirmed hatched state assertions remain. No case was deleted/excluded, no producer was changed, and no other reactions test was edited.

## RED/GREEN commands and retained raw evidence

All shell commands were prefixed rtk. Worktree writes/build/git used scoped escalation. Exact runner after initial creation:

`rtk proxy powershell -NoProfile -File .superpowers/sdd/2026-10-17-approved-ui-transfer/task-4-test.ps1 -Filter '<filter>' -Log '<raw log path>'`

The ignored runner calls `dotnet test tests/PokeCorner.App.Tests/PokeCorner.App.Tests.csproj --logger 'console;verbosity=normal'` plus optional --filter, saves COMPLETE output, records NATIVE_EXIT, tails14 lines and returns the native code. Omit -Filter for the ONE full App gate. Raw logs are beside this report and retain failures unchanged. Windows PowerShell redirection is UTF-16. Early focused runs inherited process-only POKE_SHELL_CAPTURE_DIR=TEMP/pokecorner-task3-captures from the Task3 runner template; the final App gate uses process-only TEMP/pokecorner-task4-captures. These are existing synthetic shell fixture outputs, not a new global visual acceptance loop.

1. RED before production edits: filter `FullyQualifiedName~ApprovedNursery`, task-4-red.log, native/wrapper exit1,0/4. Existing eager composition lacked packaged scene/empty Choose buttons/full draft picker/grouped bounded800 selection.
2. task-4-first-green.log, same filter, exit1, compile stopped on a Cyrillic character literal corrupted by Windows PowerShell reading a UTF8-no-BOM helper as ANSI. Helper source was made UTF8-BOM and rerun; final production/test files were normalized to repository UTF8 without BOM. This is wrapper/encoding evidence, not a behavior pass.
3. task-4-second-green.log, same filter, exit1, compile stopped on missing SpriteImages namespace and WPF/Forms HorizontalAlignment ambiguity. Corrected the real namespace and alias.
4. task-4-third-green.log, same filter, exit0,4/4.
5. task-4-focused-initial.log, filter `FullyQualifiedName~NurseryUiTests`, exit0,18/18. Old helper tests now exercise the actual species→individual draft controls; their retry/shortage/keyboard/hidden-outcome guards remain. Rejected zero-percent UI assertion now checks truthful unsupported shiny wording and absence of percentage text.
6. Candidate-guard RED after root authorized the narrow App extension: filter `FullyQualifiedName~ApprovedNurseryQueued`.
   - task-4-candidate-red.log, exit1, fixture compile error from nonexistent AppState.FromSaveData. Fixed fixture to actual Hydrate/Collection APIs.
   - task-4-candidate-behavior-red.log, exit1,1/2. The active-parent queued candidate actually executed canonical Start and unexpectedly succeeded (`Assert.Throws() Failure: No exception was thrown`). Unsupported current species was already refused canonically. This meaningful behavior RED preceded the production guard.
7. task-4-focused-expanded.log, filter `FullyQualifiedName~NurseryUiTests|FullyQualifiedName~InterruptedHatch`, exit0,31/31. Includes success/refusal completion after Unloaded for start/hatch/ready, proposed-clone refusal/retry with charm/metadata/frozen child, exact same-species normal/shiny duplicates, incompatible/same/active/busy/unsupported exclusions, exact discovery masking,800/200 paging, queued real AppState/BackgroundSaveWriter candidate guard and canonical committed replay/fingerprint behavior.
8. Covering filter B: `FullyQualifiedName~NurseryUiTests|FullyQualifiedName~InterruptedHatch|FullyQualifiedName~ThemeRenderingTests|FullyQualifiedName~ApprovedShellTests|FullyQualifiedName~ShellTests`.
   - task-4-covering.log, exit0,145/145, before applying the final approved split composition. A local XML restructuring helper initially stopped before writing because it searched a detached element through the old root; fixed helper and applied it. No failed native test log was overwritten.
   - task-4-covering-composition.log, exit0,145/145, approved split preparation/waiting plus whole ready scene and actual berry price controls.
   - task-4-final-covering.log, exit0,152/152, AFTER compact outcome-card/trait presentation, actual canonical pure evolved/standalone root assertions, long Cyrillic applied-pair draft Cancel and real button bounds/scroll reachability at1000×650 and840×540 content areas (reserving shell chrome for1000×760 /840×650 windows), one ready action bounds and active baby clock teardown/reload coverage. This is37 nursery tests, one interrupted-hatch case,114 shared shell/Theme cases. No per-font/capture loop.
9. ONE final complete App gate: no -Filter, task-4-app-gate.log, native/wrapper exit1,606 passed/2 failed/0 skipped, total608,47.8468seconds. Every nursery/new candidate/lifetime test passed. Exact two known baseline failures are below. No full App rerun and no Core gate: Core production source unchanged.
10. `rtk proxy git diff --check`: first exit1 on one extra EOF blank line in nursery_uiTests.cs; corrected only whitespace, then exit0. Final source/receipt/lifetime/cache/form/root/actual-candidate self-review completed before App gate. Staging/commit evidence below is scoped to the9 owned files.

11. Final precommit semantic check found catalog rarity strings use TitleCase while the local label switch used lowercase. Corrected case normalization and added a real canonical tag/filter/Russian-label regression. Strengthened berry-price assertions to compare each exact Russian berry identity AND canonical quantity. Same covering filter B: task-4-label-covering.log, native/wrapper exit0,153/153 (38 nursery, one interrupted-hatch,114 shared cases),11.8360seconds. This focused replay was justified by the newly found label fix; no second full App/Core gate ran. The full App checkpoint remains honestly606/608 before this small final correction and one added test.

## Files (9)

- src/PokeCorner.App/App.xaml.cs (root-authorized current-candidate nursery start guard only)
- src/PokeCorner.App/Popup/NurseryView.xaml
- src/PokeCorner.App/Popup/NurseryView.xaml.cs
- tests/PokeCorner.App.Tests/GlobalUpdate/nursery_uiTests.cs
- tests/PokeCorner.App.Tests/GlobalUpdate/approvedNurseryTests.cs
- tests/PokeCorner.App.Tests/GlobalUpdate/approvedNurseryMoreTests.cs
- tests/PokeCorner.App.Tests/GlobalUpdate/approvedNurseryCandidateTests.cs
- tests/PokeCorner.App.Tests/GlobalUpdate/approvedNurseryFinalTests.cs
- tests/PokeCorner.App.Tests/GlobalUpdate/reactionsTests.cs (one root-authorized nursery case only)

## Concerns and boundaries

- Task6 known starter: acceptanceTests.OfflineStarterUsesCanonicalFramesBeforeAnyLegacyPngFetch; StarterSelectionWindow.xaml.cs:88; `Legacy PNG/network fetch should not run.` Untouched and not excluded.
- Task8 old AcceptanceVisualTests.CaptureAssembledApplicationMatrix still stops on NullReferenceException at acceptanceVisualTests.cs:131, casting removed expedition TeamPanel. Its nursery lines148–149 also use rejected FirstParentsPanel/SecondParentsPanel eager-control internals; those were identified but not reached by this gate. No dummy controls, rejected eager UI or harness rewrite was introduced. Task8 wholesale harness replacement/global37-state visual/manual comparison remains necessary.
- Historical shared hero GIF timing did NOT fail this gate; no GIF/timing assertion/source was weakened. This pass does not establish resolution of earlier intermittent evidence.
- Functional STA bounds/temporary synthetic saves and existing capture fixtures do not establish whole-app visual, DPI/manual, actual OS animation-policy delivery or real profile/MSI acceptance. Windows motion policy is consumed read-only; OS settings were not changed.
- No real profile/application launch, Core recipe/economy/frozen-outcome mutation, new art/imagegen, release/MSI/installer, OS/autostart action, push, root docs/Kanban edit or subagent/reviewer. Root owns independent scoped review and later global visual acceptance.

## Commit evidence

Final scoped staged diff check: exit0. Commit04246b40bc5e7be04cc0131c3753fb68324666ab created successfully (exit0), exactly9 owned/explicitly extended paths,804 insertions/514 deletions. Post-commit git status --short is empty. Ignored report/raw logs/runner remain available in this ledger; no push.


## Review correction round1/5 — 19 October2026

Status: DONE_WITH_CONCERNS; Important reload-before-completion finding corrected. Root owns independent scoped re-review.
FIX_BASE: 04246b40bc5e7be04cc0131c3753fb68324666ab
FIX_HEAD: 45ca1520e75e2360798478e0e6edc3fb90df6bcc

### Finding and scoped correction

The exact missing order was pending Start -> Unloaded -> Loaded -> failed completion. Loaded rendered the selected pair while _busy remained true, then the old lifetime's finally cleared _busy but refused to Refresh. An empty-nest timer deliberately did not refresh, so the visible Choose/Start buttons stayed disabled. This was a production retry defect, not an obsolete assertion.

ReleaseBusy now clears the shared busy flag and reconciles the CURRENT view snapshot when the view is currently resumed. Start/hatch/activation use this small release path. Their result messages, receipt processing and reveal remain guarded by the ORIGINAL captured lifetime/selection generation. The ready refresh flag has the equivalent current-resumed presentation refresh on release. No suspended view is refreshed, no timer is restarted by a completion, no stale message/exception/result payload/animation is published, and global confirmed publication remains independent. A successfully committed hatch after navigation is rendered from the current saved archive as an ordinary resumed result, without replaying the old reveal. Refused Start keeps the exact pending request, and refused Hatch keeps the same nest/operation IDs for the next explicit retry.

Only the four touched async methods and the small release method were formatted into ordinary statements. No broad selection/render refactor, bulk readability cleanup, App/Core/producer/economy/schema change, or other owner's warning fix was performed. Root deferred those Minor matters to the coordinated final source cleanup; existing approvedCollectionTests.cs CS8602/xUnit2013 warnings remain outside this correction.

### RED/GREEN evidence

All shell commands used rtk; source/test writes, test build and git used scoped worktree escalation. The SAME ignored task-4-test.ps1 runner and process-only temporary fixture capture folder were used. Exact base command remains `dotnet test tests/PokeCorner.App.Tests/PokeCorner.App.Tests.csproj --logger 'console;verbosity=normal'`, plus the filters below. Full raw output/observed native and wrapper exit evidence remains beside this report; no failure was overwritten.

1. Meaningful RED BEFORE source correction:
   `rtk proxy powershell -NoProfile -File .superpowers/sdd/2026-10-17-approved-ui-transfer/task-4-test.ps1 -Filter FullyQualifiedName~Fix1ReloadBeforePendingCompletion -Log .superpowers/sdd/2026-10-17-approved-ui-transfer/task-4-fix1-red.log`
   Native/wrapper exit1,1/8 passed and7 failed. The8 new cases use pending Start/Hatch/ready/activation success/refusal -> Unloaded -> Loaded BEFORE releasing the pending task. The failures expose disabled current Start/Choose, Hatch and activation controls and stale current successful-start/hatch/ready presentation. The ready-refusal case already passed via its explicit subsequent timer retry; it remains in the covering set. Start refusal asserts no nest/archive/baby/stale message, usable Choose/Start and successful retry with the SAME NurseryRequest; Hatch refusal asserts same operation/nest IDs and unique durable child on retry; activation checks exact child retry. Success checks use current committed snapshots without a stale reveal/clock/message.
2. ONE covering GREEN after correction, no interim replay:
   `rtk proxy powershell -NoProfile -File .superpowers/sdd/2026-10-17-approved-ui-transfer/task-4-test.ps1 -Filter 'FullyQualifiedName~NurseryUiTests|FullyQualifiedName~InterruptedHatch|FullyQualifiedName~ThemeRenderingTests|FullyQualifiedName~ApprovedShellTests|FullyQualifiedName~ShellTests' -Log .superpowers/sdd/2026-10-17-approved-ui-transfer/task-4-fix1-covering.log`
   Native/wrapper exit0,161/161,12.7498seconds. Includes all153 accepted Task4/shared cases plus the8 new exact-order regressions. Existing completion-while-STILL-unloaded cases remain green, so the current-loaded reconciliation does not revive suspended UI. Existing frozen result/metadata/refusal/idempotency/receipt/clock/bounds cases remain green.
3. Self-review and `rtk git diff --check`: exit0. Verified no new operation can interleave while _busy is held, final reconciliation reads only the current snapshot, stale messages/reveals still require Live(capturedLifetime), suspended views remain inert, and request/hatch IDs are not cleared on refusal. No full App or Core suite was rerun, per root instruction. The previous606/608 full App checkpoint remains historical and unchanged, with its Task6 starter and Task8 old visual-harness concerns.

### Fix1 owned paths (2)

- src/PokeCorner.App/Popup/NurseryView.xaml.cs
- tests/PokeCorner.App.Tests/GlobalUpdate/approvedNurseryFix1Tests.cs

Scoped commit45ca1520e75e2360798478e0e6edc3fb90df6bcc - fix(ui): restore nursery controls after pending reload. Commit and staged diff check exit0; exactly2 owned paths,286 insertions/30 deletions. Post-commit git status --short is empty.

No real profile, actual application/MSI/OS change, new art, subagent/reviewer, unrelated source edit, release or push. Global visual/manual acceptance, the two older full-gate failures and independent re-review remain root-owned.
