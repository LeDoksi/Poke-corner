# Приёмка T08 / POKE-41 — 2026-10-13

Независимое spec/quality review принято для code `60c1cf151ae1f39bfa1a3564155c6be289ca801a`. Ниже исходный отчёт реализации и доработок; старые промежуточные counts сохранены как история.

# T08 — expedition core handoff

Status: core and App command wiring implemented on `codex/global-update`. T09 owns the full expedition screen; T20 owns the complete compact-window/DPI visual matrix. No live save, MSI, or system UI was used.

## Contract and scope

- `new ExpeditionService(CatalogPackage, proposed SaveData)` exposes `Preview(ExpeditionRequest)`, `Start(ExpeditionRequest)`, `Cancel(Guid id, DateTimeOffset now)`, `Claim(Guid id, Guid operationId, DateTimeOffset? now = null)`, `RefreshReady(now)`, `ApplyActivityActions(actions, now)` and `BusyIndividualIds(SaveData)`. `ExpeditionRequest` contains location, difficulty, duration, participant IDs, stable start ID and UTC clock. A service mutates **only the proposed snapshot**; `AppState.CommitAsync` publishes it after a confirmed write.
- `App.PreviewExpedition`, `StartExpeditionAsync`, `CancelExpeditionAsync`, and `ClaimExpeditionAsync` are the T09 entry points. Rejected commands throw `InvalidOperationException` with a player-facing reason and keep the prior snapshot. App timer accumulates activity units and commits acceleration/ready transitions through the same writer. `AppState.BusyIndividualIds` includes both running/ready expedition and nest participants for T11/T13/T15.
- One active expedition; eight routes from `catalog-rules.json`; four durations and three difficulties from `balance.json`; 1–3 distinct owned, nonbusy members. Running and ready keep the slot and member locks. Cancel rechecks the deadline and grants nothing. Claim adds resources, individual XP and rational remainders to current balances, reserves/opens at most one encounter, consumes an eligible active charm through `EncounterService.Open`, records metrics/operation ID, marks claimed and releases locks in one candidate commit. Ready alone does none of these.
- Start fixes resource rates, trait/rarity/collector effects, event chance and extra-loot draws. Claim picks the actual normal full-family root from the saved route/rarity pool, then the existing EncounterService resolves the single shiny roll; direct issuance and variant availability are enforced there. Persisted start IDs seed draws, including deterministic second-trait selection at the XP threshold. Replay markers cover start/cancel/claim; a failed commit leaves the published snapshot unchanged.

## Numerical authority

`docs/design-v2/final/balance.json` supplies 30/90/180/360 minutes, recommended levels 1/10/25, berry hourly 2/2/3 with post-penalty minimum 2/hour, coins 6/8/12, XP 100/hour, composition 0.8/1/1, level/fit/preparation coefficients and caps, all 11 trait tiers, encounter/extra/relic chances and rarity/loot weights, 500 actions per 1% acceleration to 10% within the total 25% time cap. `docs/design-v2/final/catalog-rules.json` supplies the eight actual route pools, berry profiles and bonus types. The hourly fixed scale is 10^9; `RationalRemainder.Settle` carries exact scaled fractions across claims.

## Verification

- RED before implementation: focused App project exited 1 with CS0234 for missing `ExpeditionService`.
- Final focused: `task-8-focused.log`, 7/7 passed, exit 0. Synthetic cases cover UTC/offline/acceleration and monotonic ready, 12×30 versus 6 hours with remainder, 1/2-member exclusion, the level-10 six-hour 12 berries/49 coins/600 XP example, cancellation boundary/replay, and failed claim write followed by same result and one grant.
- First unfiltered solution run: `task-8-gate.log`, Core 229/229; App 397/399, exit 1. `ShellTests.FinishReviewEvidenceRendersPopulatedPokedexAndResetArea` failed at `shellTests.cs:211` in the known capture-environment condition. `MigrationTests.ConsecutiveCommitsUsePublishedSnapshotAndExitWaitsForBoth` also failed once at `migrationTests.cs:500`: `Assert.Equal() Failure: Expected: 22 Actual: 19`. The isolated run of that migration test then passed 1/1; no cause was established, so this is recorded as an intermittent observation rather than a proven pre-existing failure.
- Final full gate excluding **only** the capture-environment test: `task-8-gate-filtered.log`, Core 229/229 and App 398/398, exit 0. Raw stdout and exit codes remain in those logs. `git diff --check` passed before staging.

## Review fix 1 (2026-10-13)

The fixed `10^9` hourly scale described above has been replaced. `ExpeditionService` now reads the canonical JSON decimal coefficients as exact fractions, uses integer level sums and team size for average level, reduces each product before multiplying by duration, and stores separate reduced numerator/denominator pairs for berries, coins, and XP. Preview and persisted rewards use the same rate calculation. With levels 1, 1, 2, no matching type and a 30-minute normal trip, the canonical underlevel penalty produces a coin remainder of **2447/18000**; easy produces **1/400**. The review example's 1/300 omitted the normal difficulty's level-10 recommendation and underlevel penalty. The focused fractional test initially failed with `Expected: 2447 Actual: 271888889` (old scaled numerator), then passed 2/2 after the correction. No numerical coefficients were guessed.

`ExpeditionActivityBuffer` owns the UI-thread pending-action remainder and its running trip ID. `CreditActivity` associates actions with the published trip. A successful start/cancel/claim synchronizes the buffer to the current trip instead of clearing actions indiscriminately. A progress commit acknowledges only actions from the matching trip after a successful write. Shutdown and recovery now commit any 1–499 action remainder before final flush; if the writer has failed, the history snapshot applies the uncommitted actions without changing the live state. Behavioral tests cover actions queued during start publication, forced sub-step commit/reload, and the recovery candidate.

The original isolated `ConsecutiveCommitsUsePublishedSnapshotAndExitWaitsForBoth` failure (expected 22, disk 19) remains an unproven schedule; its old test invoked the UI-only writer contract from multiple threads. A readonly diagnosis at `task-8-barrier-diagnosis.md` identified a separate supported UI gap: an exit flush could resume before an already requested queued commit and finish early. `AppState` now registers commit requests synchronously before their first await; `WhenIdle` and `FlushAsync` await the registered drain, while each queued commit waits only for the active slot. Both the WPF dispatcher test with two gated writes and a single-thread manually reordered continuation test assert exit remains pending until the earlier queued commit completes. We did not change writer sequence ordering or claim arbitrary multithreaded entry support. A temporary source reversion proposed solely to record this barrier test's RED state was rejected by automatic approval review because it would remove save protection; no workaround or reversion was used. The first failed manual-context test was its own setup error (`UI continuations did not arrive`), corrected by accounting for the fixed flush awaiting the drain rather than the per-commit idle task.

Verification (all commands use `rtk proxy`, raw stdout in the named ignored logs):

- `dotnet test tests/PokeCorner.App.Tests/PokeCorner.App.Tests.csproj --no-restore --filter "FullyQualifiedName~ExpeditionCoreTests|FullyQualifiedName~ConsecutiveCommitsUsePublishedSnapshotAndExitWaitsForBoth|FullyQualifiedName~ExitWaitsForEarlierQueuedCommitEvenWhenItsUiContinuationRunsFirst" --verbosity minimal` — `task-8-fix1-focused.log`, exit 0, 13/13.
- `dotnet test tests/PokeCorner.Core.Tests/PokeCorner.Core.Tests.csproj --no-restore --verbosity minimal` — `task-8-fix1-core-gate.log`, exit 0, 229/229.
- `dotnet test tests/PokeCorner.App.Tests/PokeCorner.App.Tests.csproj --no-restore --filter "FullyQualifiedName!~FinishReviewEvidenceRendersPopulatedPokedexAndResetArea" --verbosity minimal` — `task-8-fix1-app-gate.log`, exit 0, 403/403. Only the known screenshot-capture environment test is excluded.
- `git diff --check` — exit 0; only line-ending normalization warnings.

T09 should use `App` commands on the UI dispatcher and may read `AppState.RunningExpeditionId` for activity coordination. No full-screen UI or cosmetic legacy UI was added. The final App gate does not validate screenshot capture in this environment; the prohibited temporary RED reversion leaves a documented test-history gap for the queued-exit case, while the corrected behavior is covered by the deterministic single-thread regression.

## Handoff and limits

T09 should bind previews and command errors to its new screen and show actual claim eligibility/charm state from current `SaveData`; call App commands rather than mutating live inventory. T11/T13 should use `BusyIndividualIds` for parent/evolution/transfer guards. T15 can read `Metrics["claimed-expeditions"]`, `Metrics["visited-regions"]`, `Metrics["hard-expedition-in-each-region"]`, `Metrics["first-expedition-xp"]` and per-route `expedition:visited:<id>` / `expedition:hard:<id>` markers. The service does not render the new screen; WPF visual acceptance belongs to T09/T20. The unfiltered capture test needs its screenshot environment, and the one-off concurrent-save assertion has no established root cause.


## Independent scoped re-review

# T08 fix1 scoped re-review — 875afa25d56e3431ac1c585e572d6300610f989b..60c1cf151ae1f39bfa1a3564155c6be289ca801a

### Spec Compliance

- ✅ Spec compliant for the five Important findings from `task-8-review.md`. This fix preserves sub-step activity through exit/recovery and start publication, calculates exact canonical rational rewards, and waits for registered queued commits before the exit snapshot. It does not change the later T09 screen or T20 visual acceptance.
- ⚠️ Cannot verify from this scoped diff: the exact schedule behind the old one-off `Expected: 22 Actual: 19` test failure; live app exit under real input load; future catalog revisions or downstream T11/T13 lock consumers. The supplied diagnosis distinguishes that unproven schedule from a supported UI queued-exit gap established by code.

### Strengths

- `src/PokeCorner.Core/Domain/ExpeditionService.cs:26-53,84-100,286-306` converts decimal JSON coefficients to reduced `BigInteger` fractions, takes the level average from an integer sum, applies the berry floor to the hourly rate, and shares rate calculation between preview and saved rewards. `ExpeditionRewards.cs:5-12` stores separate reduced numerators/denominators. The new `expedition_coreTests.cs:184-197` checks the canonical normal `2447/18000` and easy `1/400` coin remainders for levels 1, 1, 2; the first review's `1/300` example omitted normal difficulty's underlevel penalty.
- `src/PokeCorner.Core/Domain/ExpeditionActivityBuffer.cs:9-49` associates actions with a running trip ID and acknowledges only the captured amount after a successful commit. `App.xaml.cs:836-843,879-883,898-912,956-965` preserves actions credited during start publication, commits 1–499 actions before normal shutdown/recovery flush, and applies them to a failed-writer history candidate without mutating the live state.
- `src/PokeCorner.Core/AppState.cs:23-26,64-100` registers commit requests synchronously before waiting for the active slot, lets queued commits wait on the per-commit idle task rather than their own drain, and makes external `WhenIdle`/`FlushAsync` wait for the whole registered queue. `migrationTests.cs:489-559` gates two writes on the WPF dispatcher and exercises a reordered single-thread continuation.

### Issues

#### Critical (Must Fix)

- None found in fix1.

#### Important (Should Fix)

- None found in fix1.

#### Minor (Nice to Have)

- `.superpowers/sdd/2026-10-11-global-update-transition/task-8-fix1-focused.log:10`, `task-8-fix1-core-gate.log:9`, `task-8-fix1-app-gate.log:10`: the saved stdout files show 13/13, 229/229, and 403/403 passing but do not contain an `ExitCode=` line, despite `task-8-report.md:31-35` saying raw exit codes remain in those logs. Preserve the shell exit status in each future log; this is an evidence-recording gap, not a demonstrated test failure.

### Five-finding disposition

1. **ADDRESSED — shutdown remainder loss.** `App.xaml.cs:898-912` commits pending running-trip actions before `FlushAsync`; failed-writer recovery uses `SnapshotIncludingPendingExpeditionActivity` at `App.xaml.cs:870-875,906`. `expedition_coreTests.cs:37-83` checks reload and recovery candidate behavior.
2. **ADDRESSED — start-publication loss.** `App.xaml.cs:843` synchronizes the buffer to the published trip instead of clearing all actions; `ExpeditionActivityBuffer.cs:12-24` retains actions when IDs match. The queued-publication test is `expedition_coreTests.cs:37-70`.
3. **ADDRESSED — inexact fixed-scale remainder.** `ExpeditionService.cs:26-53,286-306` removes hourly `10^9` rounding; preview, start, and claim use the same exact fraction and persisted denominator (`ExpeditionService.cs:84-91,116-126,204-215`). Canonical fractional regression is `expedition_coreTests.cs:184-197`.
4. **ADDRESSED for the supported UI queued-exit gap; old failure origin remains unproven.** `AppState.cs:64-100` makes `WhenIdle` and exit flush drain already requested commits. `migrationTests.cs:489-559` provides dispatcher and deliberately reordered single-thread gates. The previously observed multithread schedule was outside the writer's UI-thread contract; neither this review nor the fix claims arbitrary concurrent entry support.
5. **ADDRESSED — missing focused regression coverage.** `expedition_coreTests.cs:37-83,184-197` now tests trip-buffer publication, sub-step commit/reload, recovery candidate, and canonical fractional carry; `migrationTests.cs:489-559` guards queued exit. These test helper/state paths rather than launching the full App UI, which is a remaining live acceptance boundary.

### Assessment

**Task quality:** Approved for the scoped fix1 gate.

**Reasoning:** The fix closes the demonstrated activity, arithmetic, and supported UI save-order defects without widening the writer contract. The old full-run `22/19` interleaving has no retained trace and is not assigned a proven cause; the deterministic supported-path regression and final gates provide appropriate evidence for this task stage.

**Checks:** Read updated `task-8-report.md`, `task-8-barrier-diagnosis.md`, and pinned fix diff (revisited only portions truncated by tool output). Inspected focused named-risk `WpfTestHost` behavior. Supplied logs show focused 13/13, Core 229/229, and App 403/403 passing, with only the capture-environment test excluded; the implementer reports exit 0, but the log files omit explicit exit-code lines. No suites rerun; no source/index/Git mutations. The report records an automatic approval rejection of a temporary rollback solely for RED evidence; the rollback was not performed.


Evidence note: raw stdout logs preserve counts but omit explicit exit-code lines. Exit 0 is reported by the implementer in the original report; no test was rerun to reconstruct missing records. Future logs must retain command and process exit status. The pinned barrier diagnosis is preserved in the companion proof directory.
