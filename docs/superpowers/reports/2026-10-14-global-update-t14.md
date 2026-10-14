# Приёмка T14 / POKE-47 — 2026-10-14

Независимое spec/quality review принято для code `bb91d25a6c3ec419f0434e92229f1605bf2b034c`. Ниже исходный отчёт реализации и доработок; старые промежуточные counts сохранены как история.

# T14 implementation report — new Pokédex

## Delivered

- Replaced the collection tab's Pokédex area with distinct **All species** and **My Pokémon** modes, species cards, filters, pagination, inline species details, and explicit GUID-based selection of each owned individual. The collection's existing care/active hero remains available.
- Read names, Russian factual descriptions, types, rarity, and availability from `CatalogPackage.Species`; read public route hints from the packaged `location-draft.json`. No parallel content store was introduced. Closed species expose their number and public hint only. Search, type/rarity filters, type sort, detail text, and automation names do not reveal closed metadata. Silhouettes require both an available canonical asset and a successful image decode; an unavailable asset is never treated as discovery.
- Kept discovered species visible after their owned instances are gone. Species membership uses each individual's actual species ID even when its cosmetic displayed stage is overridden. Owned details show nickname, level, live nest/expedition busy state, traits, Shiny/favorite/active status, and caught date. Individual changes in the production caller pass through `AppState.CommitAsync` before the UI reports success; errors remain visible in the open detail. Existing save and Chill paths remain in place.
- Added card keyboard activation and Escape dismissal, a bounded detail overlay, and a compact 320-pixel layout assertion. The full multi-DPI visual matrix is assigned to T20.

## Verification

- RED was observed before production edits using `rtk proxy dotnet test tests/PokeCorner.App.Tests/PokeCorner.App.Tests.csproj --no-restore --verbosity minimal --filter 'FullyQualifiedName~PokedexTests'`: exit code **1**, with missing new API compiler diagnostics `CS1061`, `CS0117`, and `CS1739`. That early command was visible in tool stdout but was not redirected to a file; complete raw RED stdout is unavailable. This record states only the observed command, exit, and diagnostic IDs.
- Focused `PokedexTests`: **10 passed, 0 failed, 0 skipped** after the final functional edits. This includes synthetic closed/open boundaries, unsupported assets, explicit multi-instance choice, cosmetic override grouping, live busy provider, compact WPF rendering, failed commit behavior, packaged route hints, and packaged silhouette decode.
- `ThemeRenderingTests`: **101 passed, 0 failed** when run alone during integration. The final solution gate includes those tests.
- Final Core/App solution gate: **Core 248 passed; App 419 passed; total 667 passed, 0 failed**, exit code **0**. Exact command and stdout/stderr are saved in [task-14-final-gate.log](2026-10-14-global-update-t14/task-14-final-gate.log). The filter excludes only the known environment-specific `FinishReviewEvidenceRendersPopulatedPokedexAndResetArea` capture test, as agreed in the controller bridge.
- Final source self-review checked species secrecy, stable individual IDs, missing asset behavior, commit-before-success and failure retention, busy-state guards, and compact detail bounds. `git diff --cached --check` is performed before the source commit.

## Legacy test expectations revised

- `CollectionView_FavoriteIndividual_SortsAboveThePlainOnesAndRendersAGoldStar` became `CollectionView_FavoriteIndividual_KeepsNumberOrderAndRendersAGoldStar`: the new owned-mode default follows Pokédex number order, while the favorite badge remains checked on the same individual.
- The active type-badge assertion now expects the Russian label `Огонь` for the existing Fire type. It still verifies that all rendered badges have text.

## Next-task interface and limits

- T13/T15 can call `CollectionView.OpenIndividual(Guid)` and inspect `SelectedIndividualId`; species mode exposes each owned instance by stable `IndividualId`. `CollectionViewModel.GetSpeciesEntries()`, `FilterSpecies()`, and `GetEntry(Guid, now)` provide the read-side contract. Evolution/transfer engines are outside this task.
- T20 still owns visual calibration across window sizes and 150/200% DPI. This task verified focused WPF compact bounds and behavior, but did not claim full visual acceptance or capture a new screenshot. No live save, MSI, or Windows registration was changed.

## Independent review fix round 1

- The owned-card rebuild signature now tracks its displayed live busy and level state, as well as trait and description fields. A WPF regression changes both providers without changing the individual identity and checks the refreshed card.
- Detail actions capture the opened card's GUID and generation. Post-`await` control changes, error text, and dismissal occur only while that same detail lifetime remains open; reopening the same GUID starts a new generation. Favorite completion uses a safe lookup if another queued action removed the individual. `ShowDetail` restores the new card's favorite action state. The committed operation still targets its originally captured GUID.
- Added delayed favorite, delayed failure/select target, and released-individual regressions, plus Enter/Space activation and Escape dismissal assertions. Full DPI visual acceptance remains T20.
- Real behavioral RED before these production edits: [task-14-fix1-red.log](2026-10-14-global-update-t14/task-14-fix1-red.log), exact focused command, **2 failed / 10 passed / exit 1** on stale level and the wrong detail star. Focused GREEN: **14/14**. Final shared gate: [task-14-fix1-final.log](2026-10-14-global-update-t14/task-14-fix1-final.log), Core **248/248**, App **423/423**, **671/671**, exit **0**, with only the previously agreed capture-environment exclusion.
- Self-review checked old and new detail lifetimes, safe post-publication lookup, button state after a switch, and the live card signature. T08/T11 and real-save evidence remain in their accepted reports; this round did not redo those suites or the T20 visual matrix.


## Independent review evidence: task-14-review.md

# T14 independent review (8f8e0c5..31b1123)

## Spec Compliance

- ❌ Issues found. The two modes, catalog-backed content, closed-species redaction, public route hints, stable individual GUID selection, discovery retention on release, and commit-before-publish path are implemented (`CollectionViewModel.cs:112-174`, `CollectionView.xaml.cs:187-347`, `App.xaml.cs:752-759`). The owned card's live busy state does not update when only busy status changes (`CollectionView.xaml.cs:584-589`), and asynchronous action completion can update or close a different detail card (`CollectionView.xaml.cs:872-891`, `974-993`, `996-1034`).
- ⚠️ Cannot verify from this task diff: T08 prepared encounter isolation, T11 hatch archive/variant guards, complete real-save migration behavior, and the full 150/200% DPI and state matrix assigned to T20. The current focused test renders at 320 pixels (`pokedexTests.cs:150`) but has no keyboard event assertion or DPI change; the handlers exist (`CollectionView.xaml.cs:710-729`). The implementer reports the initial RED as observed exit 1 with CS1061/CS0117/CS1739, but raw RED output was not captured; this is explicitly documented in `task-14-report.md`.

## Strengths

- Closed entries omit name, description, types, and rarity before filtering or binding; search uses only number and public hint for them (`CollectionViewModel.cs:122-174`).
- The production callback uses `AppState.CommitAsync`, which builds and validates a snapshot, awaits `CommitAndPublishAsync`, then hydrates published state (`App.xaml.cs:759`, `AppState.cs:65-85`). Failed commits leave the original individual and open detail in the focused test (`pokedexTests.cs:167-189`).
- The final gate log records the exact filter, Core 248 and App 419 passing, and exit 0. The only excluded test is the stated capture-environment case (`task-14-final-gate.log`).

## Issues

### Important (Should Fix)

- `CollectionView.xaml.cs:584-589`: `RefreshPokedex` skips rebuilding the owned cards when its signature is unchanged, but `IsBusy` and `Level` newly appear on those cards (`CollectionView.xaml.cs:595-596`, `CollectionCardModels.cs:28`). Starting or completing a nest/expedition can leave the visible availability stale indefinitely; level can likewise lag when progress changes without the signature fields changing. Include every newly displayed live field in the signature, or update existing card bindings in place. Add one WPF assertion that changes the busy provider and refreshes the view.
- `CollectionView.xaml.cs:872-891`, `974-993`, `996-1034`, `1037-1049`: the handlers capture a GUID for the commit but update shared detail controls and call `HideDetail()` after `await` without checking that the same card remains open. Escape/backdrop can close it and another card can open while the save runs. A prior action can then repaint or close that new card, put its error on the wrong card, and the favorite handler's `First` can throw if that GUID was released by a queued commit. Bind post-await UI work to the captured GUID and current detail state; use a safe lookup after publication. Cover a delayed commit followed by a detail switch.

### Minor (Nice to Have)

- `pokedexTests.cs:127-163`: the compact WPF case checks width, but no test fires Enter/Space/Escape or changes DPI even though the brief requests those checks. Add a focused keyboard assertion now; keep the full visual DPI matrix in T20.

## Assessment

**Task quality:** Needs fixes.

**Reasoning:** The read-side secrecy and save integration are strong, but stale live data and post-await detail races block the task's functional gate. The existing 10 focused tests and 667-test solution gate do not exercise those transitions.

**Focused checks outside the prepared diff:** Named risk, commit publication order: inspected `AppState.cs:65-85`; named risk, current snapshot after hydration: inspected `AppState.cs:185-210,254-282`; named risk, detail lifetime/call sites: inspected `CollectionView.xaml.cs:810-825,872-1049`. No suite was rerun.


## Independent review evidence: task-14-rereview1.md

# T14 fix round 1 re-review (31b1123..bb91d25)

## Finding Verdicts

- **Owned cards retain stale busy/level state** — ADDRESSED. `CollectionView.xaml.cs:584-589` now includes `IsBusy` and `Level` (also the newly displayed trait/description fields) in the card rebuild signature. `pokedexTests.cs:128-163` changes both providers without changing the GUID and asserts the rebuilt card.
- **Post-await action can repaint/close another detail or throw after release** — ADDRESSED. `CollectionView.xaml.cs:738-743,751-756,815-819` tracks a detail lifetime. Favorite, evolution, nickname, select, and release completions check captured GUID/generation before changing detail controls; favorite uses `FirstOrDefault` after publication (`CollectionView.xaml.cs:880-1068`). Delayed switch, error, and release regressions are in `pokedexTests.cs:165-269`.
- **Keyboard activation/dismissal lacked a focused assertion** — ADDRESSED. `pokedexTests.cs:289-300` fires Enter, Escape, and Space and checks selected GUID changes. The full visual DPI/state matrix remains assigned to T20.

## New Breakage in the Fix Diff

None found. The fix is limited to the owned-card signature, detail lifetime guards, and focused tests.

## Out-of-Scope Observations

None. T08/T11/save boundaries were accepted by the controller from unchanged code; T20 retains full visual DPI and live-environment acceptance.

## Verdict

**Spec compliance:** Approved for the scoped T14 functional gate. **Task quality:** Approved. **Fix round:** All findings addressed, no new Critical/Important breakage.

**Evidence checked:** The implementer report's fix appendix matches the diff. `task-14-fix1-red.log` records two behavioral failures before production edits, 10 passed and exit 1; it also records an xUnit1031 warning from that temporary RED state. `task-14-fix1-final.log` records Core 248 passed, App 423 passed, total 671, exit 0, with only the agreed capture-environment exclusion. The reported focused GREEN is 14/14; no separate focused GREEN raw log was supplied. No tests or Git commands were rerun in this re-review.
