# Приёмка T09 / POKE-36 — 2026-10-15

Независимое spec/quality review принято для code `b170b23`. Ниже исходный отчёт реализации и доработок; старые промежуточные counts сохранены как история.

# T09 expedition WPF screen — implementer report

Status: DONE_WITH_CONCERNS. Source/test commit: `1664328 feat: add expedition preparation and result screen`. Worktree: `codex/global-update` from `a6ff7b0`. No real save, installer, OS mutation, or game account was used.

## Delivered

- Replaced the expedition placeholder with a scrollable WPF screen using existing Nunito/theme resources, eight route text buttons, one selected-route landscape slot, three difficulties, four durations, GUID-based team selection, and canonical `ExpeditionService.Preview` amounts and chances.
- Busy members from an expedition or nest are disabled. The active home companion is eligible if otherwise free. Current team choice never changes the active companion. Preview shows each selected participant's XP and traits; one/two/three-member restrictions are explicit.
- Running and ready views use the persisted trip route, duration, participants, UTC deadline, frozen reward fractions, and current saved remainders. They never recalculate a started trip from live levels or show the hidden encounter/extra outcome before claim. The cancel confirmation checks the saved deadline again and presents claim when completion won the race.
- App's existing start/cancel/claim/open commands remain the only mutation path. A narrow `closeMenu: false` option keeps the expedition screen visible after its confirmed commit; other `CommitStateAsync` callers retain their prior close behavior. Failed commands show the exception and keep selection/ready state. Pending start request and claim operation ID are reused on retry.
- Claim shows the returned `EncounterResolvedResult` as a closed result. The result panel locates the exact persisted encounter by the trip ID; Open invokes `App.OpenExpeditionEncounterAsync` with that ID. The UI never directly grants an individual.

## TDD and verification

- RED before production edits: `task-9-red.log`, focused App test exit 1 with CS0246 because `ExpeditionView` did not exist.
- Focused final: `task-9-focused.log`, 7/7 passed, exit 0. Synthetic WpfTestHost checks cover route count/compact logical width, Preview number agreement, nest-busy GUID, frozen trip reward text after XP change, cancel boundary recheck, failed claim and stable claim retry ID, failed start and stable request, and exact closed encounter ID forwarding/opening.
- Core gate: `task-9-core-gate.log`, 256/256 passed, exit 0.
- App gate: `task-9-app-gate.log`, 455/455 passed, exit 0 on the committed tree; only `FinishReviewEvidenceRendersPopulatedPokedexAndResetArea` excluded for the established screenshot capture environment condition. No other tests were excluded.
- `git diff --check` exit 0. The Core gate ran before the final added failed-start test; production/Core code was unchanged afterward. The final App gate covers all seven T09 UI cases.

## Self-review and handoff

Changed source: `App.xaml.cs`, `AppMenuWindow.xaml/.cs`, `ExpeditionView.xaml/.cs`; test: `expedition_uiTests.cs`. Existing menu constructor calls remain valid through an optional view parameter. Existing nursery and other generic commit callers still close the menu.

T18 can use `App.OpenExpeditionEncounterAsync(Guid)` for the saved closed encounter; its ID is the claimed expedition ID. The UI owns no separate reward calculator or encounter store.

Concern for T20: there are no packaged per-route landscape illustrations. The screen has the approved single selected landscape slot and route-specific provisional colors/shapes. This is not complete art or full 150/200% DPI visual acceptance. Result amounts from the just-confirmed claim appear in-session from saved pre-claim terms; on a fresh launch the claimed trip retains its reward fractions and extra item counts but not an exact integer receipt after remainders settle, so the result panel avoids claiming an invented amount. T20 should assess art, typography, and all visual states together. The built-in keyboard controls were used; hardware keyboard and whole DPI matrix remain unverified.

## Review fix 1 (2026-10-15)

Review found three material gaps. A failed `OpenExpeditionEncounterAsync` left the same-window button disabled; route/duration controls could change during pending start and discard its retry ID; exact claimed berries/coins/XP were only held in view memory. All three are fixed in source commit `b170b23 fix: persist expedition claim receipt and restore UI retries`.

`ExpeditionState.Receipt` is an optional persisted `ExpeditionReceipt`. Claim fills it with the exact primary berry settlement, total coin grant including extra coins, and actual XP applied to each participant after the level cap. It is set on the same proposed save snapshot as the resource grant, claim marker, closed resolved encounter, metrics, and lock release. Failed write leaves live `Receipt` null and state ready. Retry with the same operation ID produces the same receipt; replay does not grant again. `JsonSaveStore` validates nonnegative fields and exact participant GUID keys. Older claimed v2 saves with no receipt still load; the UI explicitly says those historical amounts were not saved. The result view reads saved amounts after reload rather than settling updated remainders.

The view now disables route/difficulty/duration/team controls during pending start, ignores reentrant route/team clicks, and restores a programmatic combo change to the pending request. A failed start retains the exact request for retry. The encounter Open button derives both visibility and enabled state from the saved `resolved` phase and `_busy`, so a failed write can be retried in the same window.

RED evidence before the corresponding fix: `task-9-fix1-receipt-red.log` exited 1 with CS1061 missing `ExpeditionState.Receipt`. After implementing the real receipt contract, with UI handlers still unchanged, `task-9-fix1-ui-red.log` exited 1 for both behaviors: expected grove but route changed to ruins; expected enabled retry button but it remained disabled. A temporary attempt to conditionalize the new receipt tests was rejected by automatic approval review with the stated reason: “The patch adds conditional guards that hide the newly added receipt tests, contrary to the explicit requirement not to introduce rollback guards after RED capture. Do not bypass this rejection through a workaround or indirect execution.” No conditionalization or rollback was applied; the real contract made the UI behavioral RED runnable.

Final evidence: `task-9-fix1-focused.log` 27/27, `task-9-fix1-core-gate.log` 256/256, `task-9-fix1-app-gate.log` 459/459; every exit code 0. The App gate excludes only the established `FinishReviewEvidenceRendersPopulatedPokedexAndResetArea` screenshot-capture environment case. Synthetic tests cover failed Open/retry and one grant, in-flight start selection/request identity, settled receipt candidate versus failed publication, confirmed commit/reload/replay, corrupt receipt rejection, old claimed save without receipt, and claimed view after reload. `git diff --check` exited 0 with only Git line-ending notices.

Only the review's minor landscape/ready-copy polish and full visual/DPI matrix remain for T20. No real saves, installer, or OS state were used. No action remains blocked by the automatic review rejection.


## Independent review evidence: task-9-review.md

### Spec Compliance

- ❌ Issues found. Eight route buttons, one selected landscape, difficulty/duration, GUID team selection, canonical Preview numbers, persisted running/ready terms, and App commit wiring are present (ExpeditionView.xaml.cs:20-30,67-83,136-183,195-225; App.xaml.cs:843-846,950-971). But after restart the claimed result omits the exact integer primary berries, coins, and participant XP (ExpeditionView.xaml.cs:228-238), conflicting with docs/design-v2/EXPEDITIONS.md:111,121-123 and docs/design-v2/final/SCREENS.md:9,34. The T08 ExpeditionState/ExpeditionRewards contract lacks a settled receipt (ExpeditionState.cs:5-16; ExpeditionService.cs:204-229); coordinate a persisted claim receipt with its owner.
- ⚠️ Cannot verify from this task diff: route art remains provisional (ExpeditionView.xaml:14-31). Full visual, keyboard, compact, and 150%/200% DPI acceptance belongs to T20. WpfTestHost only laid out 600×420 (expedition_uiTests.cs:45-58).

### Strengths

- The UI delegates Preview/start/cancel/claim/open to the existing service and App commit barrier (App.xaml.cs:843-846,950-971; ExpeditionView.xaml.cs:50-64,169-183,247-311). Expedition commits keep the menu open while other CommitStateAsync callers retain the default close behavior (App.xaml.cs:891-904).
- Busy nest/expedition participants are excluded by GUID (ExpeditionView.xaml.cs:136-159). Running rewards settle copies of saved remainders against frozen fractions and conceal extras/encounter until claim (ExpeditionView.xaml.cs:205-218). Cancel checks saved status and deadline after confirmation (ExpeditionView.xaml.cs:263-274).
- Supplied logs record focused 7/7, Core 256/256, and final App 455/455, exit code 0; App excludes only the established capture-environment test (task-9-focused.log, task-9-core-gate.log, task-9-app-gate.log).

### Issues

#### Critical (Must Fix)

- None found.

#### Important (Should Fix)

- `src/PokeCorner.App/Popup/ExpeditionView.xaml.cs:300-311,228-245` — Failed Open disables OpenEncounterButton, and RefreshResult never re-enables it. A write failure leaves the closed encounter without an in-window retry. Restore IsEnabled according to !_busy and persisted phase; test a failed-open retry.
- `src/PokeCorner.App/Popup/ExpeditionView.xaml.cs:67-79,96-102,247-260` — Route and difficulty/duration controls stay interactive while start awaits commit. Their handlers clear _pendingStart during _busy, losing the stable operation ID on retry and letting visible choices diverge from the in-flight request. Lock those controls or ignore changes while busy; test an attempted change during a pending failing start.
- `src/PokeCorner.App/Popup/ExpeditionView.xaml.cs:228-238,284-295` — Exact claim amounts exist only in _lastClaimRewardText, captured from pre-claim UI and lost with the view. After restart only berry ID and extras remain. Persist the confirmed claim receipt in the shared result contract and render it; do not settle already-updated remainders. This crosses T09 UI ownership into T08 service/schema.

#### Minor (Nice to Have)

- `src/PokeCorner.App/Popup/ExpeditionView.xaml.cs:197-225` — The running/ready panel hides the selected landscape and does not repeat participant traits or that ready rewards do not expire, contrary to docs/design-v2/EXPEDITIONS.md:110,119. Include this content in T20 visual/content acceptance.

### Assessment

**Task quality:** Needs fixes.

**Reasoning:** Main service wiring and phases are sound, but a failed encounter write strands the user and changing options during start invalidates retry identity. The claimed-result screen needs a durable receipt to show exact amounts after restart.

**Checks:** Read pinned a6ff7b0..1664328 review package once; inspected changed code where tool output cut its middle, the named saved-result risk in ExpeditionState/ExpeditionService, canonical screen requirements, and supplied test logs. No suites rerun; no product code, Git state, save, installer, or OS changes.


## Independent review evidence: task-9-rereview1.md

### Spec Compliance

- ✅ Spec compliant for the three original Important findings in the scoped fix 1664328..b170b23. The failed-Open button is enabled again from persisted resolved phase once _busy clears (ExpeditionView.xaml.cs:256-259,315-326); route, duration, difficulty and team controls are locked or guarded during a pending start, retaining the exact request/operation ID (ExpeditionView.xaml.cs:73,95-109,123-125,145-165,265-278); claim now stores exact granted primary berries, total coins including extras, and actual capped XP per GUID on the same candidate as the claim marker and rewards (ExpeditionService.cs:204-235; ExpeditionState.cs:17-25), then reloads them in the result view (ExpeditionView.xaml.cs:238-250).
- ⚠️ Older already-claimed v2 saves legitimately have no Receipt. The optional field and explicit unknown-amount copy preserve readable historical claims rather than inventing a settled number (JsonSaveStore.cs:386-390; ExpeditionView.xaml.cs:248). Full route art, physical keyboard and 150%/200% DPI visual acceptance remain T20, not this scoped fix.

### Strengths

- Failed save leaves the live ready trip without a receipt; retry commits a receipt with the same operation ID, and replay returns before settlement (ExpeditionService.cs:193-203,204-235; expedition_coreTests.cs:349-426). Receipt validation rejects negative grants, wrong phase, null XP map and mismatched participant keys (JsonSaveStore.cs:386-390).
- The UI test exercises failed Open and successful same-view retry with one individual grant, and forces route/duration changes from within an in-flight start to verify request identity (expedition_uiTests.cs:213-287). Claimed view test clones a save, reads persisted quantities, then checks old null receipt copy (expedition_uiTests.cs:290-314).
- Supplied RED evidence records the missing Receipt compile error, followed by two real UI behavior failures before handler edits. Final logs record focused 27/27, Core 256/256 and App 459/459, all exit code 0; the established screenshot-capture test alone is excluded from App. No passing suites rerun for this review.

### Original finding disposition

1. **ADDRESSED — failed Open retry.** ExpeditionView.xaml.cs:256-259 makes IsEnabled depend on !_busy and saved resolved phase; expedition_uiTests.cs:213-247 verifies retry and one grant.
2. **ADDRESSED — in-flight start identity.** ExpeditionView.xaml.cs:73,95-109,123-125,145-165 guards the controls and restores attempted programmatic combo changes to the pending request; expedition_uiTests.cs:249-287 verifies same request on retry.
3. **ADDRESSED — transient claim amounts.** ExpeditionService.cs:204-235 persists actual settlement and level-capped XP in ExpeditionState.Receipt; JsonSaveStore.cs:386-390 validates it; ExpeditionView.xaml.cs:238-250 renders it after reload; expedition_coreTests.cs:349-426 and expedition_uiTests.cs:290-314 cover commit/reload/failure/replay/legacy null.

### Issues

#### Critical (Must Fix)

- None found in the scoped fix.

#### Important (Should Fix)

- None found in the scoped fix.

#### Minor (Nice to Have)

- `src/PokeCorner.App/Popup/ExpeditionView.xaml.cs:242,251-255` — Receipt.Coins is the total including ExtraCoins (ExpeditionService.cs:207-211), while the following “Дополнительные” line lists ExtraCoins again. The arithmetic and saved amount are correct, but the result can be read as two additive coin grants. Label the receipt value “монеты всего” or split base and extra presentation in a later copy pass.
- Original T20 content/visual note remains: running/ready landscape, traits and non-expiring reward reassurance; real visual/keyboard/DPI acceptance is outstanding.

### Assessment

**Task quality:** Approved for scoped fix 1.

**Reasoning:** All three blocking findings have direct code changes and focused regressions. The receipt is captured inside the proposed claim state, survives reload, tolerates old claimed saves, and is not regranted on replay. No new blocking regression found.

**Checks:** Read the pinned 1664328..b170b23 review package once; inspected the cut-off validator section and named receipt/UI paths, appended task-9-report.md, and supplied RED/final logs. No tests rerun; no product code, Git state, real save, installer, or OS changes. The report records an automatic approval rejection of temporary test conditionalization; that patch was not applied and does not block this fix.
