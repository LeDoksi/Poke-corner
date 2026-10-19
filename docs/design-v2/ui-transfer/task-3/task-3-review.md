# Task 3 review

## Spec Compliance

- ✅ Task-scoped expedition/team/ready flow is compliant, with the minor numeric-search edge case below. Package base d95f4c1038b15505d4e8901dc2ebb21523a35a11 → head 7e0ab398a40571be76d4f3c38e4b96834040b378.
- Draft selection/application, three-member cap and exact individual identities: src/PokeCorner.App/Popup/ExpeditionView.xaml.cs:258,273,288. Actual catalog/balance recommendations and unlocked-trait filtering: src/PokeCorner.App/Popup/ExpeditionTeamSelector.cs:28,44,53.
- Current-snapshot/clock marker: src/PokeCorner.App/Popup/AppMenuWindow.xaml.cs:110. Committed-ready session notification boundary: src/PokeCorner.App/ExpeditionReadyNotifications.cs:8; startup and awaited progress-publication hooks: src/PokeCorner.App/App.xaml.cs:307,1196.
- ⚠️ Cannot verify from diff: coordinated 37-state visual acceptance, actual OS balloons, shell work-area/system-control behavior, MSI/offline acceptance and real-profile migration. These remain later controller gates.
- ⚠️ Core gameplay authority is unchanged. Chill/root-only acquisition including standalone species and broader persistence invariants are not re-audited here; task-facing code continues to use canonical service delegates and frozen receipt quantities (src/PokeCorner.App/Popup/ExpeditionView.xaml.cs:401).

## Strengths

- Draft and applied teams are separate; Done rechecks live availability and the picker materializes only twelve candidates per page (src/PokeCorner.App/Popup/ExpeditionView.xaml.cs:258,273,288).
- Async cancel rechecks lifetime generation, trip identity and clock before the producer; unload stops the timer and cancels local presentation (src/PokeCorner.App/Popup/ExpeditionView.xaml.cs:110,450,453).
- Notifications have application-session lifetime and observe committed status without adding save-schema flags (src/PokeCorner.App/ExpeditionReadyNotifications.cs:8; src/PokeCorner.App/App.xaml.cs:1196).
- Behavioral tests use canonical services and refused writes for drafts, exclusions, 800 individuals, unlocked traits, route ranking, confirmation lifetime and notification deduplication (tests/PokeCorner.App.Tests/GlobalUpdate/approvedExpeditionTests.cs:34,115,141).

## Issues

### Critical (Must Fix)

None.

### Important (Should Fix)

None.

### Minor (Nice to Have)

1. src/PokeCorner.App/Popup/ExpeditionTeamSelector.cs:58 — All-zero numeric search becomes an empty substring after TrimStart. Searching "0" or "0000" therefore matches every individual, including species numbers containing no zero. Preserve a nonempty normalized query or explicitly handle all-zero input. Normal padded valid numbers such as "#0001" work.
2. tests/PokeCorner.App.Tests/GlobalUpdate/approvedCollectionTests.cs:283 and :255 — Final focused output contains CS8602 (possible null dereference) and xUnit2013 (collection-size assertion). These are outside the Task 3 diff, but validation output is not warning-free; route cleanup to the collection-test owner.

## Evidence and scope

- Read the report and supplied full 103473-byte review package in bounded passes. No git rederivation, source/index/HEAD mutation, subagents or test reruns.
- Named outside-diff risk: wall-clock readiness must reach the new post-commit observation without activity. One focused App integration scan confirmed StartExpeditionProgressCommit schedules a commit when ReadyAtUtc elapses even below the action threshold (src/PokeCorner.App/App.xaml.cs:1172). The scan also exposed existing mutation wait/call sites. A guessed App-side ExpeditionActivityBuffer.cs path was absent; no broader search followed.
- Named outside-diff risk: delegated trait copy might reintroduce forbidden percentages. One focused CollectionView.TraitEffect/TraitLabel check confirmed short qualitative strings without percentages (src/PokeCorner.App/Popup/CollectionView.xaml.cs:1535).
- Raw task-3-red.log confirms 4 executed / 4 failed; the report correctly distinguishes the first faulty wrapper exit from test failure evidence.
- Raw task-3-final-covering.log confirms 50/50 passing and the two collection warnings above.
- Raw task-3-app-gate.log confirms 583 passed / 2 failed / 585 total, 44.0123 seconds: OfflineStarterUsesCanonicalFramesBeforeAnyLegacyPngFetch ("Legacy PNG/network fetch should not run") and CaptureAssembledApplicationMatrix (NullReferenceException from obsolete TeamPanel capture). These remain routed to Task 6 and Task 8; the full gate is not green.
- The saved helper captures LASTEXITCODE after dotnet and returns it. Reported native/wrapper exit values and git checks are implementer evidence; raw log endings independently confirm counts, not persisted native-exit records.

## Assessment

**Task quality: Approved**

No blocking Task 3 functional/component defect found. Track the two minor items; branch acceptance still requires the explicitly red App gate and later coordinated validation.
