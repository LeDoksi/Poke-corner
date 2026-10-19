# Task 3 — approved expedition flow and full team selection

Status: DONE_WITH_CONCERNS. Date: 2026-10-19.
Worktree: C:/Users/Georgy/.codex/worktrees/global-update/PokeTokenBar-Windows
Branch: codex/global-update
BASE: d95f4c1038b15505d4e8901dc2ebb21523a35a11
HEAD/commit: 7e0ab398a40571be76d4f3c38e4b96834040b378 — feat(ui): transfer expedition team and committed ready flow

## Whole feature batch

- First preparation has three empty slots and disabled Start. Choose/Change team opens the same full native picker; draft Cancel preserves the previous applied exact GUIDs; Done applies1–3 valid individuals. Fourth selection is disabled, chosen slots can remove individuals, and resetting filters retains draft choices. Normal/shiny and duplicate individuals retain their own identities; GUID fragments never enter labels/tooltips.
- Picker has fixed selected slots/header and fixed count/Done/Cancel/page controls with internal catalog scrolling.12 individuals per page, search by species/nickname/number, type, either unlocked trait, Available default, route affinity, recommendations/level/number sorting. Base uses three columns and compact two. Long Cyrillic nickname/action bounds are measured at1000x650 and840x540 content areas, reserving the shell chrome of the accepted1000x760 and840x650 windows.800-individual fixture is paged/searchable, including normal/shiny duplicates. No800-image decode: only visible page/slots load through the existing SpriteImages decoder into a bounded128-key per-view sprite cache; reward art is cached. No sprite callback/animator was added.
- Presentation helper indexes actual CatalogPackage species/types, final/catalog-rules.json route bonus types, and final/balance.json recommended levels and secondTraitLevel. Recommendations sort availability, matching type, recommended-level readiness, high level. Route affinity and trait name/effect are separately visible; second trait is shown/filtered only at the real unlock level. Active desktop companion, active trip/nursery participants, and unsupported forms are unavailable with short reasons. Done and preparation refresh recheck current availability; canonical producers still recheck their existing exclusion rules.
- Uses the eight existing accepted runtime Data/expeditions/*.png files linked from final/assets/expeditions through RuntimePackage.props. No image generation/redraw/new resource copying. Full preparation, running, ready, claimed result and Continue-to-new-preparation states; route/three-slot/settings/reward compositions use the existing palette/components. Real canonical Preview quantities, cached accepted berry/coin art and XP cards replace estimates/chance copy. No gameplay percentages/probabilities/formulas in view text or tooltips. Exact berries/coins/XP/time remain.
- Existing frozen reward payload, receipt/result/open encounter behavior, stable start/claim IDs and failed-write retry tests remain. Start/claim/open async result messages are generation guarded; suspended views do not Refresh. Unload stops the view timer, abandons the picker and cancels local confirmation; Loaded resumes current-state presentation. No global committed mutation depends on page lifetime.
- Public constructor/delegate APIs retained; optional final confirmAsync appended. Production passes the Task1 shared AppMenuWindow.ConfirmAsync. Legacy optional synchronous confirmCancel remains supported for callers/tests. Standalone fallback uses the same ConfirmationOverlay; Escape/unload cancel it. Cancel is busy guarded before awaiting; it rechecks current trip ID/status/clock and view generation before the producer. No DispatcherFrame bridge or ordinary confirmation Window.

## Ready marker/notification integration and lifetime

AppMenuWindow reads wallet and expedition readiness from ONE current snapshot per existing snapshot refresh. The expedition tab marker derives ready status OR running ReadyAtUtc/current clock and stays through navigation, reload and refused claim until a successful claim publishes claimed. Encounter readiness is unchanged.

ExpeditionReadyNotifications belongs to the application instance, with an in-memory announced trip-ID set, not the view. ObserveCommitted accepts only persisted/published status ready, never merely a passed running deadline. Production observation occurs after tray construction at startup (already-ready saved trip reminder) and AFTER awaiting successful ExpeditionActivityBuffer progress CommitAndPublish. A refused candidate never reaches the observation call. This adds no work to the globally constrained cheap tick beyond its existing progress scheduling. Notifications use existing TrayIconService.ShowNotification. Navigation/menu open/close/reload and repeated observations do not reannounce; each new committed-ready trip can announce once. A new application session intentionally reminds once again. No save-schema notification flag, rewards mutation or new persistence path.

Synthetic notification regression covers menu absent and present, view reload/navigation, repeated observations, refused real BackgroundSaveWriter progress commit, successful AppState publication, saved-ready new-session reminder, claim and a second trip. OS balloon delivery itself was not exercised.

## RED/GREEN and raw evidence

All shell commands prefixed rtk, with scoped escalation for this checkout writes/build/git. Raw logs remain beside this report in the ignored ledger; Windows PowerShell redirection uses UTF-16. No failure log/assertion was deleted or overwritten.

Base command: dotnet test tests/PokeCorner.App.Tests/PokeCorner.App.Tests.csproj --logger 'console;verbosity=normal'

Focused filter A: FullyQualifiedName~ApprovedExpeditionTests|FullyQualifiedName~ExpeditionUiTests
Covering filter B: FullyQualifiedName~ApprovedExpeditionTests|FullyQualifiedName~ExpeditionUiTests|FullyQualifiedName~ExpeditionCoreTests|FullyQualifiedName~ApprovedShellTests|FullyQualifiedName~ShellTests

After the first wrapper issue, exact invocation form was:
rtk proxy powershell -NoProfile -File .superpowers/sdd/2026-10-17-approved-ui-transfer/task-3-test.ps1 -Filter '<filter>' -Log '<raw log path>'
The saved helper runs the base dotnet command plus the supplied --filter, records NATIVE_EXIT, tails14 lines and returns that native code. It sets process-only POKE_SHELL_CAPTURE_DIR to TEMP/pokecorner-task3-captures for existing shell evidence fixtures. No full visual-capture loop was performed.

1. RED BEFORE production edits, --filter FullyQualifiedName~ApprovedExpeditionTests: task-3-red.log,4 executed/4 failed (0/4), missing three slots/full draft picker/ready marker. The first nested -Command wrapper accidentally expanded LASTEXITCODE before the child ran, so its WRAPPER exit was0; native dotnet output explicitly says failed4/4, but native exit was not captured and is NOT claimed as an observed exit1. Root informed/accepted honest checkpoint; no cosmetic RED rerun.
2. task-3-first-green.log, native/wrapper exit1, compile failed: List/string[] mismatch, ordered enumerable fallback and WPF/Forms Image/Orientation/HorizontalAlignment ambiguity. Corrected source types/aliases.
3. task-3-focused-first.log, filter A, native/wrapper exit1, compile failed: test attempted internal BerryName without friend access. Changed incidental label assertion to canonical quantity prefix, preserving actual Preview equality.
4. task-3-focused-second.log, filter A, native/wrapper exit1,15/16. Existing frozen-reward fixture assigned XP100000 outside canonical progression range; changed fixture to actual LevelProgression.XpForLevel(20), preserving byte-for-byte reward-text freeze and cancel-boundary assertions.
5. task-3-focused-third.log, filter A, native/wrapper exit1,18/19. New refused-write fixture lacked ReachedSpeciesIds so validation correctly stopped before the writer. Supplied valid v2 reached history; retained exact IOException/refusal assertion.
6. task-3-focused-covering.log, filter B, native/wrapper exit0,49/49.
7. task-3-focused-final.log, filter B, native/wrapper exit0,49/49, after compact two-column sizing, action bounds/long Cyrillic assertions, generation guards, Escape handling and second-trip notification coverage.
8. task-3-focused-complete.log, filter B, native/wrapper exit0,50/50, after completing separate result/Continue, actual reward cards/cache and route-type ranking/type-filter regression.
9. task-3-final-covering.log, filter B, native/wrapper exit0,50/50, after self-review moved notification observation from the cheap global tick to the explicit startup/successful-progress publication hooks. This is the final focused state.
10. ONE final App gate (no -Filter, same saved helper), task-3-app-gate.log, native/wrapper exit1,583 passed/2 failed/0 skipped, total585,44.0123seconds. All Task3 focused behaviors and remaining App tests pass except the two explicit concerns below. No repeated full App gate and no broad Core gate: Core source was unchanged.
11. rtk git diff --check and rtk git diff --cached --check: exit0. Staged9 explicit owned paths only; scoped commit exit0; rev-parse confirms HEAD above; post-commit git status --short empty.

## App gate concerns / root routing

- Task6 known starter failure: acceptanceTests.OfflineStarterUsesCanonicalFramesBeforeAnyLegacyPngFetch; StarterSelectionWindow.xaml.cs:88; 'Legacy PNG/network fetch should not run.' No starter source/assertion changed or excluded.
- Task8 old interrupted capture harness: AcceptanceVisualTests.CaptureAssembledApplicationMatrix, acceptanceVisualTests.cs:131, NullReferenceException. The unchanged harness casts expedition.FindName('TeamPanel') then accesses Children, after the rejected old eager picker has intentionally been replaced. Root was informed of this concrete failure before any scope extension. Per brief, no dummy TeamPanel/control, rejected composition or harness rewrite was added. Task8 wholesale replacement remains required.
- Historical competing hero GIF timing failure did NOT fail this App gate; no animation assertion/timing/source changed, and this pass does not establish that historical issue resolved.
- Global37-state visual comparison/manual acceptance, actual OS balloon behavior, real-profile/runtime migration and MSI/offline release acceptance remain later tasks. Functional STA layout, temporary existing shell captures and canonical producer tests are not a claim of full visual/OS acceptance.

## Owned paths (9)

src/PokeCorner.App/App.xaml.cs
src/PokeCorner.App/ExpeditionReadyNotifications.cs
src/PokeCorner.App/Popup/AppMenuWindow.xaml
src/PokeCorner.App/Popup/AppMenuWindow.xaml.cs
src/PokeCorner.App/Popup/ExpeditionView.xaml
src/PokeCorner.App/Popup/ExpeditionView.xaml.cs
src/PokeCorner.App/Popup/ExpeditionTeamSelector.cs
 tests/PokeCorner.App.Tests/GlobalUpdate/approvedExpeditionTests.cs
 tests/PokeCorner.App.Tests/GlobalUpdate/expedition_uiTests.cs

No Core/economy/save-schema/collection/Room/nursery source redesign, real profile or app launch, installer/MSI, OS/autostart change, new art, push/release, subagent/reviewer, or root docs/Kanban edit. Root performs independent feature review.
