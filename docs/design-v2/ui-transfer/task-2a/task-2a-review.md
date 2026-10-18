### Spec Compliance

**Needs fixes.** Task 2A implements the requested whole Corner and album scope, but two truthful-state requirements remain unmet: absent historical special metrics become zero (AchievementService.cs:60), and care completion is presented as full evolution readiness (RoomView.xaml.cs:191).

Reviewed base a00b391 through head feaff52866a741f21db1ad1ba2b6a7791add4b57 using task-2a-brief.md, task-2a-report.md and task-2a-review-package.md. Accepted reference checked: docs/design-v2/ui-system/review.html Corner and achievements markup plus revision3.css:6 pet placement. No unrelated migration or general cosmetic/font assessment.

### Strengths

- src/PokeCorner.Core/Domain/AchievementService.cs:51 introduces a read-only preview using the existing canonical goal selector and JSON thresholds; it does not invoke Evaluate or award resources. tests/PokeCorner.Core.Tests/GlobalUpdate/achievementPreviewTests.cs:12 checks thresholds and serialized state preservation.
- src/PokeCorner.App/Popup/AchievementsView.xaml.cs:72 guards shelf art with the earned ledger; :103 keeps locked cards on first-tier silhouettes and :157 disables interaction during persistence while retaining selection on failure. tests/PokeCorner.App.Tests/GlobalUpdate/approvedRoomAlbumTests.cs:118 exercises an actual pending/failing commit and retry.
- src/PokeCorner.App/Popup/RoomView.xaml.cs:245 fixes room pet scale independently of desktop preference while retaining frame signatures and animator teardown at :137. The synthetic real-control tests cover sprite independence, primary button routing and unloaded polling.
- src/PokeCorner.App/App.xaml.cs:989 wires existing Pet/Feed producers and exact individual navigation through compatible final optional constructor parameters. The existing production callbacks retain commit-before-publish and error notification (App.xaml.cs:685, :792).

### Issues

#### Critical

None found.

#### Important

1. **Preserve unknown history when a special metric is absent.** src/PokeCorner.Core/Domain/AchievementService.cs:60 checks only an explicitly present null metric. For an unearned metric-backed special such as trophy-special-type-team, a missing key falls through to SpecialEarned=false and returns 0; AchievementsView.xaml.cs:115 consequently renders 0 / 1. Real migrated saves have exactly this shape: SaveMigration.cs:112 initializes unknown chain metrics but does not initialize special keys; SpecialEarned at AchievementService.cs:123 treats absent special event history as false. This invents a historical zero contrary to the brief. Preserve null for missing/unavailable event-based history, while allowing reconstructible present-state specials to use their real state. Add a regression using the actual migrated shape (absent special key), alongside the current explicit-null test.

2. **Do not announce full evolution readiness from care alone.** src/PokeCorner.App/Popup/RoomView.xaml.cs:190-191 displays "Готов к эволюции" as soon as StageForPoints exceeds CurrentStage. A pet with enough care but no recipe berries, or with unavailable next-form assets, cannot evolve: canonical EvolutionService.cs:57-62 returns a blocking reason for those states. The room therefore offers a false readiness statement when the personal card correctly blocks it. Keep the meter about care and say that care is accumulated, or obtain actual readiness from the existing canonical preview without adding another eligibility switch. Cover enough-care/zero-berries and unavailable-branch examples.

#### Minor

None raised; assembled visual/DPI comparison and broader polish remain Task 8.

### Cannot verify / retained evidence

- Exact assembled visual correspondence at base/compact sizes and real DPI remains deferred to Task 8. The diff establishes composition and synthetic layout assertions, not visual acceptance of a running shell.
- Real-session sound/reaction timing and physical interaction remain unverified; callbacks were checked statically, no real profile was opened.
- Reported focused 137/137 and Core 257/257 passes were not rerun. Full App evidence remains 546/548, with OfflineStarterUsesCanonicalFramesBeforeAnyLegacyPngFetch assigned to Task 6 and CollectionView_HeroSprite_MultiFrameSpecies_ActuallyCyclesFrames unresolved for final acceptance. Neither failure is claimed fixed or suppressed here.
- The initial combined tool output truncated middle diff hunks. Only that missing package range was reread; no changed source file was reread wholesale. Focused outside-diff checks addressed named risks: AchievementMetrics.Get and SaveMigration metric initialization for unknown history; SpecialEarned for preview semantics; EvolutionCalculator/enum and EvolutionService.Preview for care versus readiness; App Pet/Feed callbacks for persistence/delegate behavior. No tests, git commands, source edits or index mutations were performed.

### Assessment

**Task quality: Needs fixes.** The implementation preserves the principal persistence and sprite-lifetime boundaries and adds meaningful control/state tests. The two user-visible state claims above need correction before Task 2A can pass its task-scoped gate.
