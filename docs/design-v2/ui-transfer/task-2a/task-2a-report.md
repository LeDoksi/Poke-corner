# Task 2A report — Corner and achievement album

Status: DONE_WITH_CONCERNS. Base a00b391 on codex/global-update, accepted Task1 source 38da5d5. Source HEAD recorded after commit below.

## Result and scope

- Corner now composes header, nickname/species/number/types/rarity/level, real room and earned shelf layers, care-for-evolution, real XP/next-ball/treat context, Pet/Feed/exact-personal-card/album actions, and empty-Pokedex/missing-image fallbacks.
- Room sprite scale is fixed at 5.07 in the 1448x582 scene, with visible foot at (800,524), matching final revision3 CSS. The Eevee arranged-width test checks 145–160 DIP at base composition. Desktop PetSizePixels is never changed or used for room sizing. Frame-source cache and load/unload animator lifetime are retained.
- Album has 26 cards and 3 visual shelf places. Earned art is current tier; locked art uses only the first-tier silhouette. Current values and next conditions use the pure AchievementService.Preview API and canonical JSON/goal selector. Unknown historical chain metrics and explicit null special metrics stay unknown. Preview never calls Evaluate or writes state.
- Shared all-normal goal selector now counts discovered reachable species so partial progress is meaningful; its existing >= reachable-count award comparison is unchanged. Rewards, grants, schema, persistence producers and economy are unchanged.
- Existing ApplyShelfSelection and confirmed commit are reused. Save disables shelf/card actions immediately; a failed write retains live shelf and selected award for retry. Placement, replacement, removal and de-duplication are free.
- Production supplies existing PetActiveIndividual/FeedActiveIndividual callbacks, catalog and exact collectionView.OpenIndividual(id) navigation. Existing async notification and commit-before-publish behavior remain authoritative.
- Changed only: App.xaml.cs constructor wiring; Popup/RoomView.xaml/.cs; Popup/AchievementsView.xaml/.cs; Core/Domain/AchievementService.cs; App.Tests/GlobalUpdate/room_albumTests.cs and approvedRoomAlbumTests.cs; Core.Tests/GlobalUpdate/achievementPreviewTests.cs. No theme, Collection/evolution, docs/Kanban source, installer or OS changes.

## Commands and evidence

All commands ran in C:/Users/Georgy/.codex/worktrees/global-update/PokeTokenBar-Windows through rtk, using scoped escalation for writes/builds/git. Logs below are retained beside this report. Exact dotnet invocations were run through rtk proxy powershell -NoProfile -Command, with *> log and exit $LASTEXITCODE.

1. RED: dotnet test tests/PokeCorner.App.Tests/PokeCorner.App.Tests.csproj --filter FullyQualifiedName~ApprovedRoomAlbumTests --logger "console;verbosity=normal"
   task-2a-red.log — exit1, 0/3 passed. Genuine missing behavior: CareText absent; room image138→552 after desktop-size change; no12/25 next-goal progress. Tests compiled and executed before corresponding source edits.
2. Initial GREEN attempt, same command: task-2a-first-green.log — exit1, compile error CS0104 ambiguous ProgressBar. Fixed with explicit WPF type.
3. Owned batch: dotnet test tests/PokeCorner.App.Tests/PokeCorner.App.Tests.csproj --filter "FullyQualifiedName~ApprovedRoomAlbumTests|FullyQualifiedName~RoomAlbumTests" --logger "console;verbosity=normal"
   task-2a-focused.log — exit1,22/23 passed. Remaining test expected the wrong existing type label (Обычный instead of TypeDisplay Нормальный); corrected fixture assertion.
4. Focused affected batch: dotnet test tests/PokeCorner.App.Tests/PokeCorner.App.Tests.csproj --filter "FullyQualifiedName~RoomAlbumTests|FullyQualifiedName~ShellTests|FullyQualifiedName~ThemeRenderingTests" --logger "console;verbosity=normal"
   task-2a-focused-final.log — exit1,135/137 passed. Two fixture issues: IsVisible is false for an unattached WPF control (replaced with Visibility.Visible plus actual nonzero arranged bounds and within-viewport actions); existing shell capture fixture required POKE_SHELL_CAPTURE_DIR.
5. Same focused command with process-only POKE_SHELL_CAPTURE_DIR=$env:TEMP/pokecorner-task2a-captures:
   task-2a-focused-green.log — exit0,137/137 passed.
6. Self-review regression RED: dotnet test tests/PokeCorner.Core.Tests/PokeCorner.Core.Tests.csproj --filter FullyQualifiedName~AchievementPreviewTests --logger "console;verbosity=normal"
   task-2a-preview-red.log — exit1,0/1 passed: explicit null historical special metric incorrectly previewed0. Fixed in pure preview without changing grant eligibility.
7. One Core gate: dotnet test tests/PokeCorner.Core.Tests/PokeCorner.Core.Tests.csproj --logger "console;verbosity=normal"
   task-2a-core-gate.log — exit0,257/257 passed, including preview immutability/JSON target/unknown-metric regression.
8. One final App gate, same temporary capture env: dotnet test tests/PokeCorner.App.Tests/PokeCorner.App.Tests.csproj --logger "console;verbosity=normal"
   task-2a-app-gate.log — exit1,546/548 passed. Both failures are detailed below. No exclusions or weakening were used.
9. rtk git diff --check — exit0. Scoped diff/self-review covered production callers, data preservation, navigation, unknown/future artwork, actual metrics, resize bounds and lifecycle.

The auto-review service became unavailable once due usage limit between steps3/4. That command was not executed or bypassed. User resumed after reset; normal scoped escalation then succeeded.

## Concerns / deferred verification

- Full App remains red: PokeCorner.App.Tests.GlobalUpdate.acceptanceTests.OfflineStarterUsesCanonicalFramesBeforeAnyLegacyPngFetch — Legacy PNG/network fetch should not run. Existing StarterSelectionWindow.xaml.cs:88 path; owned by Task6.
- Full App also failed PokeCorner.App.Tests.Theme.ThemeRenderingTests.CollectionView_HeroSprite_MultiFrameSpecies_ActuallyCyclesFrames — the hero sprite never changed frame (saw 1 distinct frames). Same known suite-level GIF acceptance gap; focused theme batch passed. Root cause remains unestablished; final Task8 must resolve it. No repeated whole-suite diagnostic was run.
- No real profile, manual app acceptance, whole-app visual comparison, installer, autostart/OS change, new art, release or push. Temporary fixture captures were generated solely because existing tests require an output path; no cosmetic capture review was performed. Final assembled visual acceptance belongs to Task8.
- Synthetic callback tests exercise actual buttons/state/adapters; real user-session sounds/reaction timing and physical interaction remain unverified.

## Commit

feaff52866a741f21db1ad1ba2b6a7791add4b57 — feat(ui): transfer approved corner and trophy album.

Post-commit rtk git status --short: exit0, empty (clean tracked/untracked source). Report and raw logs remain in the intentionally ignored .superpowers task directory.

## Review correction round1 — base feaff52866a741f21db1ad1ba2b6a7791add4b57

Independent task-2a-review.md found two Important issues, no Critical. Both corrected in the same owned batch:

1. The shared canonical SpecialEarned selector now returns nullable truth for event-backed special:* metrics: missing and explicit-null history remain unknown; reconstructible present-state branches retain their actual booleans. Evaluate still grants only when the selector returns true, preserving previous eligibility; Preview reads the nullable value directly without duplicating eligibility. Regression invokes actual SaveMigration.Migrate on a synthetic v1 save, verifies its special key is absent, covers both absence and explicit null, checks reconstructible nickname state remains0, records the event and verifies1, and proves preview does not mutate the save.
2. Corner says "Уход накоплен · условия эволюции в карточке" when its care meter is complete. It no longer equates care alone with full evolution readiness. Both regression cases use canonical EvolutionService.Preview to establish a block: enough care with zero berries, and enough care/berries with unavailable branches. The care meter remains full and room text stays care-specific. No eligibility switch was added to the UI.

Commands ran through the same rtk/scoped-escalation wrappers, exact filters below; complete raw outputs remain beside this report.

- RED Core: dotnet test tests/PokeCorner.Core.Tests/PokeCorner.Core.Tests.csproj --filter FullyQualifiedName~AchievementPreviewTests --logger "console;verbosity=normal"
  task-2a-fix1-core-red.log — exit1,2/3 passed; actual migrated absent-key case failed Expected:null Actual:0. Explicit-null case already passed.
- RED App: dotnet test tests/PokeCorner.App.Tests/PokeCorner.App.Tests.csproj --filter FullyQualifiedName~CompletedCareDoesNotClaimEvolutionReadyWhenCanonicalRecipeIsBlocked --logger "console;verbosity=normal"
  task-2a-fix1-app-red.log — exit1,0/2 passed; both blocked states displayed "Готов к эволюции · откройте карточку".
- GREEN Core: dotnet test tests/PokeCorner.Core.Tests/PokeCorner.Core.Tests.csproj --filter "FullyQualifiedName~AchievementPreviewTests|FullyQualifiedName~AchievementsTests" --logger "console;verbosity=normal"
  task-2a-fix1-core-green.log — exit0,11/11 passed, including canonical achievement producer/idempotency/failed-write cases.
- GREEN App: dotnet test tests/PokeCorner.App.Tests/PokeCorner.App.Tests.csproj --filter FullyQualifiedName~RoomAlbumTests --logger "console;verbosity=normal"
  task-2a-fix1-app-green.log — exit0,25/25 passed, covering approvedRoomAlbumTests plus existing room_albumTests.
- rtk git diff --check — exit0. Self-review confirmed only4 correction files changed: RoomView.xaml.cs, AchievementService.cs, approvedRoomAlbumTests.cs, achievementPreviewTests.cs.

No full-suite rerun, GIF/starter test edits, real profile, OS/installer change, new art, extra view or Task2B work. Earlier full App546/548 remains the recorded full-suite result with the same two deferred failures; whole-app visual acceptance remains Task8.

Correction commit: 02f9beb76d97676e47309b945abae6e9c8d26d0d — fix(ui): keep achievement history and care captions truthful. Post-commit rtk git status --short: exit0, empty.
