# Приёмка T17 / POKE-38 — 2026-10-16

Независимое spec/quality review принято для code `de9f0bd`. Ниже исходный отчёт реализации и доработок; старые промежуточные counts сохранены как история.

# Task 17 — fixed room, album, and shelf

Status: DONE

Implemented:
- Replaced the corner placeholder with the packaged approved 1448×582 scene. RoomArt reads Data/art-production/last-attempt/final-assets/manifest.json and shipped PNGs; RoomView places 56 possible award layers by manifest FootAnchor at three manifest anchors, overlays the manifest frontlip, then the active pet under one Uniform Viewbox. The pet uses the existing decoded frame list, SpriteAnimator, and saved size.
- Added the 26-card album. A chain displays its latest earned tier; a locked card uses its first-tier exact-alpha mask with the UI dark brush. The only entry is the room's Достижения button. Manual placement, replacement, and removal are free; a selected card can be deselected.
- Added SaveData.TrophyShelfIds with v2 validation and v1 migration reset. AchievementService fills only the first free slot when it issues a genuinely new award and upgrades a displayed chain in place. A full shelf stays unchanged; manually removed old awards stay removed. The app uses CommitStateAsync and publishes no shelf change on failed write.
- The shared GIF frame cache now keys normal and shiny variants separately; the active shiny pet uses the same decoder and animator.

Tests and evidence:
- RED before production edits: `rtk proxy dotnet test tests/PokeCorner.App.Tests/PokeCorner.App.Tests.csproj --no-restore --verbosity minimal --filter FullyQualifiedName~RoomAlbumTests`, ExitCode 1, CS1061 missing SaveData.TrophyShelfIds. Raw output: task-17-red.log.
- Focused final: same command, ExitCode 0, 9/9. Covers all 168 placements, exact alpha for 56 trophy/mask pairs, packaged anchors/frontlip, 26 cards and future-tier hiding, selection cancellation, free shelf edits, chain upgrade, full shelf, removed slot, failed write/retry, compact WPF layout at 150/200% render DPI, and distinct normal/shiny paths for small/wide species.
- Final direct Core/App: `rtk proxy dotnet test PokeCorner.sln --no-restore --verbosity minimal --filter 'FullyQualifiedName!~FinishReviewEvidenceRendersPopulatedPokedexAndResetArea'`, ExitCode 0; Core 256/256, App 482/482. Raw output: task-17-final.log. Only the known capture-directory-dependent test was excluded.
- `rtk proxy git diff --check`: ExitCode 0.

Self-review: confirmed room art comes from packaged Data, not a source-checkout fallback; no separate GIF decoder or store; AwardLedger IDs, coins, timestamps, and revisions remain in the confirmed snapshot. Changes are confined to RoomView, AchievementsView, room_albumTests, menu/app adapters, and minimal shelf persistence/issuance/validation/migration.

Handoff: T20 should run the complete assembled UI visual and DPI matrix against real authenticated/profile data and tune final spacing if needed. No real profile, MSI, or OS state was touched.

Commit: 8045eee1526b8da249fa721672c7979031e3026a — feat: add fixed room, achievement album and trophy shelf.
Final staged `rtk proxy git diff --cached --check`: ExitCode 0. Post-commit `rtk proxy git status --short --branch`: clean `## codex/global-update`.

Observed `exec_command` return metadata (from the actual tool responses, not inferred from test text):
- RED focused command above: `exit_code: 1` (retained tool response; matching `task-17-red.log` header).
- Final focused command above (9/9): `exit_code: 0` (retained tool response).
- Final Core/App command above (256/256 + 482/482): `exit_code: 0` (retained tool response; matching `task-17-final.log` header).

Fix round 1 (review of commit 8045eee, 2026-10-16):
- `AchievementsView` locked art now uses the same Uniform scale and actual top-left alignment as earned art. A 87x94 card is rendered and its opaque pixel bounds are compared in both states.
- `RoomView` uses the already decoded PNG frame dimensions and union of nontransparent pixels over all animation frames. At the saved default 96 px, the approved Eevee 46x47 sprite renders at 3x and top-left (731,383); all variants keep visible foot (800,524). Saved desktop size is used only as a relative scene scale and is never rewritten. The same decoded images feed `SpriteAnimator`.
- Added actual packaged normal/shiny 25/133/143 frame tests, a transparent two-frame animation test, and a UI rendered-alpha test. Existing empty-sprite layout test now asserts the empty source instead of the obsolete 384px square.

Exact fix1 commands and observed `exec_command` return metadata; each named log contains the raw output:
- RED before production edit: `rtk proxy dotnet test tests/PokeCorner.App.Tests/PokeCorner.App.Tests.csproj --no-restore --verbosity minimal --filter FullyQualifiedName~RoomAlbumTests` -> `exit_code: 1`, 7 failed/9 passed (`task-17-fix1-red.log`). Six size failures used real normal/shiny decoded frames; one showed mask Fill versus Uniform.
- Final focused: same command -> `exit_code: 0`, 17/17 (`task-17-fix1-focused.log`). An intermediate focused run failed the added pixel-render assertion and exposed ImageBrush centering versus earned Image top-left placement; the brush alignment was corrected before this final run.
- Initial full gate: `rtk proxy dotnet test PokeCorner.sln --no-restore --verbosity minimal --filter 'FullyQualifiedName!~FinishReviewEvidenceRendersPopulatedPokedexAndResetArea'` -> `exit_code: 1`, Core 256/256, App run aborted after a `ConfirmedPersonalCardRetrainingCallsRealProducer` dialog observer failure (`task-17-fix1-final.log`). Its cause is UNPROVEN; the partial 472 App successes are not a completed gate.
- Bounded diagnostic: `rtk proxy dotnet test tests/PokeCorner.App.Tests/PokeCorner.App.Tests.csproj --no-restore --verbosity minimal --filter FullyQualifiedName~ConfirmedPersonalCardRetrainingCallsRealProducer` -> `exit_code: 0`, 1/1 (`task-17-fix1-diagnosis.log`). No test source was changed or excluded for this failure.
- Same full gate retried with unchanged source -> `exit_code: 0`, Core 256/256 and App 490/490 (`task-17-fix1-final-retry.log`). The sole exclusion remains the previously known capture-directory-dependent test.

Fix1 self-review: compared the staged diff with both review findings; the rendered album alpha bounds match, the approved Eevee registration and normal/shiny small/wide sprite dimensions match, transparent animation keeps one foot, and saved size is unchanged. No source or test edits outside the three owned T17 files. `rtk proxy git diff --check` and `rtk proxy git diff --cached --check` both returned `exit_code: 0`; staged stat was 3 files, 224 insertions, 16 deletions. Commit `de9f0bdf048b04ed5a5225772c75016d85e64e67` (`fix: align album masks and register room sprites`). Post-commit `rtk proxy git status --short --branch` returned `exit_code: 0`, clean `## codex/global-update`.


## Independent review evidence: task-17-review.md

﻿### Spec Compliance

- ❌ Issues found: the room and album are wired to packaged art, but the locked silhouette rendering and pet registration do not match the approved geometry. Details below.
- ✅ The three shelf slots use stable award IDs; new awards fill the first free slot, a displayed chain upgrades in place, and a full shelf is not displaced (AchievementService.cs:117-125, AchievementsView.xaml.cs:46-58).
- ✅ The album is reached through the room button, and its nested tab is hidden from ordinary navigation (RoomView.xaml:11-13, AppMenuWindow.xaml:75, AppMenuWindow.xaml.cs:108-113).
- ✅ Shelf edits use the existing confirmed commit path; failed writes leave the live snapshot unchanged (App.xaml.cs:875-876, AppState.cs:68-82). The shelf validator enforces at most three distinct ledger IDs (JsonSaveStore.cs:327-328).
- ✅ RoomArt loads the packaged final-assets manifest and computes each award position from its foot anchor (RoomView.xaml.cs:20, 39-76, 88-89). RuntimePackage.props:118 packages the manifest; the same file lists its art assets.
- ⚠️ Cannot verify from this task diff: final assembled room appearance across compact window and 150/200% DPI, real active normal/shiny pets, and physical interaction. T20 owns that visual matrix per the T17 brief and GLOBAL-WORKFLOW.md.

### Strengths

- The room uses one Uniform Viewbox for its background, awards, frontlip, and pet (RoomView.xaml:17-25).
- The active overlay''s decoded frames are reused for the room, with separate normal/shiny cache keys and the existing SpriteAnimator (App.xaml.cs:80, 459, 536-575; RoomView.xaml.cs:173-178).
- Tests exercise 168 award placements, 56 matching image/mask alpha pairs, shelf history and commit failure/retry, and nested navigation (room_albumTests.cs:26-59, 114-180, 211-238). The recorded direct Core/App run exited 0 with 256/482 passing; the focused run is reported as 9/9.

### Issues

#### Important (Should Fix)

1. AchievementsView.xaml.cs:98-106: a locked card stretches its exact-alpha mask to a 132x94 Rectangle with Stretch.Fill, while the earned image uses Stretch.Uniform. The approved trophy-species-1 image and mask are 87x94 in the manifest, so the locked silhouette becomes wider than the same earned art. Render the mask with the same aspect-preserving size and alignment as the earned image; add a UI geometry assertion for a non-132x94 card.
2. RoomView.xaml.cs:159-161: the pet is forced into a square of saved desktop size and placed at a fixed scene point. The default saved size is 96 (AppSettings.cs:12), which puts a 46x47 Eevee frame near x752/y428, while IMPLEMENTATION.md:15 specifies the registered reference at 3x and x731/y383. The same square and point are applied to every species despite the stated need to account for their different transparent bounds. Derive the scene render dimensions from the decoded frame and approved registration, while keeping the saved size unchanged. The current room test has no active pet and null frames (room_albumTests.cs:183-200), so it cannot catch this.

#### Minor (Nice to Have)

- No additional minor finding.

### Assessment

**Task quality:** Needs fixes

**Reasoning:** Save, shelf, packaging, navigation, and cache integration are coherent and supported by focused tests and the recorded direct suite. Two geometry errors remain visible in the code and conflict with the approved room and album art contract; the full assembled visual acceptance remains scheduled for T20.



## Independent review evidence: task-17-rereview1.md

﻿### Finding Verdicts

- **Locked album mask distorted and misaligned versus earned art** — ADDRESSED. The mask now uses Uniform scaling and explicit left/top alignment (AchievementsView.xaml.cs:98-107). A rendered 87x94 non-square card has matching opaque bounds in locked and earned states (room_albumTests.cs:90-113).
- **Room pet forced into a square at a species-independent fixed point** — ADDRESSED. The image now uses decoded frame width and height, computes the union of visible alpha across animation frames, and positions that visible region at foot (800,524) with the saved size as relative scale (RoomView.xaml.cs:164-229). Packaged normal/shiny tests for species 25/133/143 assert the dimensions and foot, including Eevee at (731,383); a transparent two-frame test checks a stable union and saved-size change without rewriting it (room_albumTests.cs:122-177).

### New Breakage in the Fix Diff

- None identified. The fix changes only the two rendering paths and their tests. Empty sprite state clears the source and bounds before positioning (RoomView.xaml.cs:169-175).

### Out-of-Scope Observations

- None.

### Checks

- Read fix package review-8045eee..de9f0bd.diff and the appended fix1 report. Recorded focused WPF run: 17/17 passed (task-17-fix1-focused.log).
- The first full gate was interrupted after one App test failure; its cause is unproven and it is not a passing gate (task-17-fix1-final.log). A bounded run of that test passed 1/1; the unchanged source then passed the full filtered gate, Core 256/256 and App 490/490 (task-17-fix1-final-retry.log). I did not rerun tests.
- Final assembled visual and DPI acceptance remains T20.

### Verdict

**Fix round:** All findings addressed, no new Critical/Important breakage.
