# Task 8 — scoped daily-use UI re-review

20 October 2026. Independent continuation of `task-8-ui-review.md`, limited to findings 1–8 and material regressions caused by their consolidated fix. Requirements read first from `task-8-ui-rereview-brief.md`; authority remains UI-SYSTEM/LATEST-REVIEW, accepted review.html and latest binding human instructions. No repeated 47-frame whole-UI pass.

Pinned range: `0a5349db880da6dda04b3bc90048cc41334a9c48..2228fda590f8f7379719b013cd88243e7123c514`. Implementation: `5d3e7866d70777bcd0a2a2c2e1a92d0944af7243`; final report append: `2228fda`. Inspected the pinned `task-8-daily-use-rereview-package.md`, latest report/index, named source and relevant regression fixtures.

## Verdict

**Spec compliance: PASS within this fix scope.** All eight original findings are closed. Direct name editing, independent retraining, aligned header controls, canonical nursery decision terms, coherent species browsing, bounded shop quantity, distinguishable duplicate selection, destructive confirmation roles, visible expedition choices/rewards and source-specific catch copy meet the reviewed requirements.

**UI/code quality: PASS within this fix scope.** Existing cream/Nunito/forest/pixel-art direction is retained. The changed paths are substantially clearer and more consistent. No caused material regression was found in the reviewed source and representative current frames. No further cosmetic batch is requested.

**Closed: 8 / Open: 0 / New material regressions: 0** (Critical 0, Important 0, Minor 0).

This is acceptance of the eight-item UI finish contract, not whole Task 8 completion, installed-save safety, recovery/MSI acceptance or release readiness.

## Closure evidence

Paths below are relative to this worktree. **M** = `docs/design-v2/ui-transfer/task-8/daily-use-confirmation/daily-use/`; **D** = `docs/design-v2/ui-transfer/task-8/daily-use-detail-confirmation/daily-use/`. D editor/retraining/linear frames supersede their older M variants; no superseded frame was used to reopen a defect.

| ID | Status | Independent closure assessment |
|---|---|---|
| 1 Personal actions | CLOSED | M `detail-supported-eevee-compact-200.png` shows name/pencil as direct edit target, coordinated desktop/star/Back and visible separate retraining. D `detail-name-editor-compact-200.png` retains Eevee identity and explicit Save/Cancel; D long-name base/compact fit; D `detail-retraining-compact-200-action.png` exposes readable exact berry cost and reachable action. `CollectionView.xaml.cs:1246–1323` uses explicit draft/commit, blocks pending repeats, retains draft/focus on refusal and checks identity/generation before repaint. Escape cancels editor at `:816`; no typing-time mutation. Model Nickname setter still normalizes empty/whitespace (`PokemonIndividual.cs:83–94`). Focus/refusal/cancel and pending-other-card fixtures were inspected, not rerun. |
| 2 Nursery terms | CLOSED | M selected preparation base/compact shows actual berry icons/counts and time beside pinned Start; M selected-charm base displays changed berry spend and charm ×1. M unselected compact explicitly says terms appear after parent selection and keeps both parent actions visible. The same ResourcePricesPanel/CostText/ShortageText were moved into the footer, with no second price calculation. Offspring details may intentionally scroll while parents/terms/action remain available. |
| 3 All species | CLOSED | M `species-compact-200.png` has house selected/secondary modes, transparent list, fitting filter captions and regular bounded cards; full acquisition prose is removed from grid. M `species-where-find-base-150.png` retains actual evolution guidance in disclosure. Source applies explicit styles in both mode directions and preserves ordinary/shiny state, ListBox semantics, masking, existing owned navigation and pagination. The route/evolution disclosure is meaningful also on evolved cards. |
| 4 Shop | CLOSED | M shop-stepper base/compact uses minus/editable count/plus, local exact total, primary purchase and secondary navigation. `ShopView.xaml.cs:104–122` provides native editing/accessible names; `:156–189` derives inventory/pack/cost bounds, uses long arithmetic, clamps adjustment and disables invalid/over-capacity/unaffordable purchase; `:210–229` retains operation IDs across retry and removes the key after a confirmed commit so later legitimate same-quantity purchases remain separate. Existing pending double-click and newly added quantity/edit/overflow/capacity fixtures were inspected. No new dependency. |
| 5 Duplicate identity | CLOSED | M collection compact shows visible distinct ordinals; full clipped date is available through metadata tooltip/accessibility. M team-duplicates compact and selected expedition base/compact use matching ordinal/date for otherwise identical Bulbasaur. `CollectionCardModels.cs` IndividualDisplay derives ordering from the full current-species collection by caught date then ID, not filtered/sorted views; collection, team and nursery call that helper. The cross-view/filter/sort fixture checks stable labels and selected-slot accessible names. Ordinals can change after ownership changes; they are deliberately current-collection labels, not permanent IDs. |
| 6 Danger role | CLOSED | M `confirm-rehome-actual-caller-compact-200.png` shows actual name/price/irreversible consequence and red final action. Inspected actual App factories, AppMenuWindow safe/destructive adapters, CollectionView and expedition routing, settings/history local paths: destructive role is forwarded through the real caller chain, safe purchase/evolution/retraining remain safe. Overlay resets its style each presentation, retains Cancel-first focus and existing trap/Esc/background guards. UiDangerButtonStyle contains hover/pressed/focus/disabled treatments. Inspected actual expedition-cancel/decline and danger-then-safe reset fixtures; generic screenshots alone were not treated as caller proof. |
| 7 Expedition decisions | CLOSED | M unselected base and selected base/compact show finite difficulty/duration segments, clear route selection, team/edit, illustrated berry/coin/XP and concise return time beside launch. Empty launch is disabled with a reason. Icons/counts and return time come from the same canonical preview; detailed contributions intentionally scroll. Compact scene may be partially visible above the fixed team/decision region; it does not hide current controls or required launch terms. Accepted route art remains. |
| 8 Catch source copy | CLOSED | M `encounter-source-explanation-base-150.png` contains one expedition reason despite gifts elsewhere. `CatchView.xaml.cs:117–129` now selects gift/expedition/activity copy from current source; original active activity reservation/unsupported-shiny promise is retained. No queue, spend, timing or save mutation was introduced by this copy change. |

## Changed-path regression checks

- **Rename lifecycle:** draft entry does not save; Enter/Save commits, Escape/Cancel restores display and focus, validation prevents control characters/overlength, pending editor is disabled, refusal keeps it editable, and identity/generation guards prevent late completion from repainting another card. The native key/focus/automation fixture and asynchronous gated-other-card fixture directly cover these cases. Native synthetic events are not physical-keyboard acceptance.
- **Purchase trust boundary:** bounds respect units per pack, inventory room and int-bounded price; display uses long multiplication. Invalid direct input cannot purchase. `_busy` blocks a pending second invocation. Stable operation identity is retained on failed commit and removed only after confirmed commit. Existing error handling distinguishes committed purchase with repaint failure from failed persistence. No new gameplay or purchase service math was added.
- **Confirmation roles:** explicit role forwarding was followed through App.xaml.cs factories to shell/local overlays. The safe API remains backward-compatible. Existing cancellation/background/focus behavior is preserved. Settings/history use destructive adapters intentionally; this does not validate their recovery engine.
- **Identity:** the helper receives the full current collection and groups by current species; filter/sort changes do not renumber. Nursery's prior explicit-instance behavior remains. No GUID is displayed or persisted as a new user-facing identity.
- **Linear evolution:** final D linear base/compact shows one real next form, canonical illustrated berry price, visible Later/Evolve, and no redundant random-branch selector. Source hides the selector only under the same multi-outcome condition as stone selection. Branched Eevee identity/personal graph remains intact in current detail frames; no source change to ownership/masking was found.
- **Themed controls:** shared favorite and choice treatments keep keyboard focus/disabled states rather than reintroducing ad-hoc chrome. Ordinary content scrolling and compact date ellipsis are narrow intentional choices, not new acceptance blockers.

## Current frames actually opened

**22 current native PNGs covering all 17 indexed compositions**, with representative base and compact checks. No new capture was generated, no old 47-frame pass repeated, and no claim all 38 effective PNGs were independently viewed.

M (15):

- `detail-supported-eevee-compact-200.png`
- `nursery-unselected-compact-200.png`
- `nursery-selected-pinned-footer-compact-200.png`
- `nursery-selected-pinned-footer-base-150.png`
- `nursery-selected-charm-base-150.png`
- `species-compact-200.png`
- `species-where-find-base-150.png`
- `collection-compact-200.png`
- `shop-stepper-compact-200.png`
- `shop-stepper-base-150.png`
- `expedition-team-duplicates-compact-200.png`
- `expedition-unselected-base-150.png`
- `expedition-selected-pinned-footer-compact-200.png`
- `expedition-selected-pinned-footer-base-150.png`
- `confirm-rehome-actual-caller-compact-200.png`

M additionally (1): `encounter-source-explanation-base-150.png`.

D (6): `detail-name-editor-compact-200.png`; `detail-long-nickname-base-150.png`; `detail-long-nickname-compact-200.png`; `detail-retraining-compact-200-action.png`; `evolution-linear-compact-200.png`; `evolution-linear-base-150.png`.

The retraining action frame is intentionally scrolled and is not proof that its complete optional editor fits initially. Initial visibility is required for the separate entry action, which is visible in the default personal frame. Footer/launch initial visibility is judged from initial frames, not action-scrolled variants.

## Verification provenance and limits

Read the actual final focused log `task-8-daily-use-final-focused.log`: 275 passed, 0 failed, 0 skipped, 15 s; the retained report records Exit 0. No build/test was rerun. Relevant test source was inspected to assess what those assertions actually prove. Test success supports behavior, while the independent current-frame assessment above supports this visual verdict.

No source edits, new scripts/art/dependencies, capture generation or child agents were used. Only this re-review report was written. Evidence does not prove physical/per-monitor DPI, real pointer feel, screen-reader output, audio, installed profile safety, private App recovery wiring, offline publication or MSI upgrade/rollback. Those existing Task 8 gates remain separate; this review requests no additional UI fix loop.
