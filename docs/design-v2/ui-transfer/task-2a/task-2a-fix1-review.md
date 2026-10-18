### Spec Compliance

**Approved for Task 2A correction round 1.** Both prior Important findings are ADDRESSED. No new breakage found in the fix diff.

Range: feaff52866a741f21db1ad1ba2b6a7791add4b57..02f9beb76d97676e47309b945abae6e9c8d26d0d. Reviewed task-2a-brief.md, task-2a-report.md including the correction appendix, prior task-2a-review.md and task-2a-fix1-review-package.md. Review stayed within the two findings and four changed correction files.

### Findings disposition

1. **ADDRESSED — absent historical special metric.** src/PokeCorner.Core/Domain/AchievementService.cs:60 reads nullable truth from the shared SpecialEarned selector; :127 now preserves both missing and explicitly null event history through AchievementMetrics.Get. Preview therefore returns unknown rather than invented zero. The Evaluate guard at :93 accepts only true: previously false missing-history cases still cannot grant an award. Reconstructible present-state branches retain their existing booleans. tests/PokeCorner.Core.Tests/GlobalUpdate/achievementPreviewTests.cs:13 exercises actual SaveMigration.Migrate, asserts that the key is absent, covers absence and explicit null, checks the independent nickname case, verifies serialized snapshot immutability, and records the event to establish known progress.

2. **ADDRESSED — care completion claimed full evolution readiness.** src/PokeCorner.App/Popup/RoomView.xaml.cs:191 now states that care is accumulated and directs the player to the personal card for evolution conditions. This is truthful without duplicating canonical branch/berry eligibility in the room. tests/PokeCorner.App.Tests/GlobalUpdate/approvedRoomAlbumTests.cs:81 establishes two actual canonical blocks using EvolutionService.Preview: zero berries and unavailable branches. It checks both the care-specific text and the still-complete care meter.

### Strengths

- The correction reuses the canonical selector rather than adding a UI eligibility switch or independent achievement rules.
- Regression cases reproduce the reported state shapes using real migration, canonical evolution preview and WPF controls.
- The change is limited to the two fixes and focused tests; no reward, save schema, persistence producer, art or other-screen change appears in the package.

### Issues

Critical: none. Important: none. Minor: none raised. No open issues in this correction range.

### Verification and limitations

- Read the retained GREEN log tails: task-2a-fix1-core-green.log confirms 11/11, task-2a-fix1-app-green.log confirms 25/25. The report retains the preceding RED cases and exit codes. No tests or suites rerun by this reviewer.
- Existing full App evidence remains 546/548 with the same deferred starter and hero-animation failures; this review does not claim a full green gate.
- Whole-app visual/DPI acceptance remains Task 8; real-session interaction remains unverified as before. These are retained limitations, not new correction findings.
- No source or index edits, git commands, real-profile launch, outside-diff source inspection or subagents. Only this review report was written.

### Assessment

**Task quality: Approved.** Both requested corrections are narrowly implemented, preserve prior grant eligibility, and have meaningful regressions. The prior Task 2A blockers are closed.
