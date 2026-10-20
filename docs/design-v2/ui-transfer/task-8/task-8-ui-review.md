# Task 8 — holistic native UI review, 20 October 2026

Range: `66544f7586491908b94f3ddcb74e0b3c2531041d..2fc48377c12e1ebbdde61a60ff179ce46fcd41c4`. Independent review of existing captures and bounded source inspection. No implementation, builds, tests, new captures, scripts, art or child agents.

## Independent verdicts

**Spec compliance: NEEDS_CHANGES.** The accepted world is substantially transferred, but visible choices, cost/action proximity, coherent species browsing and destructive-action semantics still diverge. Latest binding requirements for inline name editing, independent retraining, coordinated neighboring actions and pleasant quantity adjustment remain unimplemented.

**UI quality: NEEDS_CHANGES.** Cream/Nunito/forest, real sprites and approved scenes are convincing. Remaining friction lies in control and decision composition: hidden ordinary actions, inconsistent neighboring controls, ambiguous duplicate selection and unnecessarily dense information. Apply the single batch below before scoped visual acceptance.

**0 Critical / 7 Important / 1 Minor.** No demonstrated blocked core task or immediate loss hazard was established. This is a UI range gate, not full Task 8 recovery/save/MSI acceptance or the final whole-branch review.

Authority read: `docs/design-v2/UI-SYSTEM.md`; `docs/design-v2/ui-system/LATEST-REVIEW.md`; actual `review.html` main-screen content; latest continuation/override index in `task-8-report.md`; pinned package summary and relevant source; updated human addendum in `task-8-ui-review-brief.md`. Quality guidance: Impeccable polish/Operate; UI UX Pro Max skill, quick-reference §§1–9, pro-rules and `task-8-uimax-guidance.md`. Generic mobile/architecture/motion advice does not override this WPF product, accepted art or 6250 ms catch.

Paths below are worktree-relative. **A** = `docs/design-v2/ui-transfer/task-8/confirmation/assembled/`; **E** = `docs/design-v2/ui-transfer/task-8/targeted-continuation/`; **R** = `docs/design-v2/ui-system/`.

## One ordered material-fix batch

### 1. Important — direct personal-card actions and coherent header

**Consequence:** rename is disconnected from the name, and retraining hides inside the same initially collapsed disclosure below the compact viewport. A tall desktop action, a 30-DIP framed star and differently aligned Back look like separate control systems.

**Evidence:** E `human-confirmation/hierarchy/detail-supported-eevee-base-150.png` and `detail-supported-eevee-compact-200.png`. `src/PokeCorner.App/Popup/CollectionView.xaml:437` groups desktop/star; `:439` renders the name as a noninteractive TextBlock; `:488–495` hides nickname/retraining in one Expander. `src/PokeCorner.App/Theme/PokedexTheme.xaml:727–750` defines the star's independent 30×30 template and local hover treatment.

**Counterpart:** R `review.html` personal-card composition and `snapshots/revision3/eevee-detail.jpg`; latest human steering explicitly requires clicking the name to rename, a separate retraining action and unified desktop/star/Back. These are binding additions, not optional taste.

**Smallest correction:** make the name a discoverable edit target with visible edit affordance and keyboard activation; reuse validation/persistence, explicit commit/cancel and focus return. Remove the combined rename expander. Expose retraining beside traits, disclosing only its slot/cost editor. Align header actions to shared height/baseline/spacing and clear primary/secondary roles; keep recognizable favorite state and keyboard focus. Optional technical diagnostics elsewhere may remain disclosed but should use the theme rather than alien stock chrome.

### 2. Important — nursery price/time/shortage beside pinned Start

**Consequence:** compact preparation offers enabled “Начать ожидание” while exact berry spend, time, selected-charm spend and shortage context are below the baby options. Terms and spend action cannot be read together; a later confirmation does not replace preparation clarity.

**Evidence:** E `human-confirmation/hierarchy/nursery-selected-pinned-footer-compact-200.png`. `src/PokeCorner.App/Popup/NurseryView.xaml:39–43` keeps resource prices/time/shortage in scrolling PreviewPanel, whereas `:90` pins Start separately. `NurseryView.xaml.cs:467` supplies canonical time/charm terms.

**Counterpart:** R `review.html` nursery preparation; `UI-SYSTEM.md` “Стоимость и действие” and nursery composition require exact cost next to action. Tour step 4 promises visible price before starting.

**Smallest correction:** move the existing canonical resource row plus concise time/shortage into the fixed preparation decision area beside Start. Include charm only when selected. Keep detailed offspring/traits scrolling and selected parents visible. Reuse the same preview, not a second price calculation.

### 3. Important — finish “Все виды” as the other half of one Pokédex

**Consequence:** switching from My Pokémon produces gray stock selected chrome, a white ListBox well, extra filter rows, clipped rarity text and irregular tall cards dominated by acquisition prose. The sprite grid becomes a text list; even its first row and “Где найти?” require excessive scrolling.

**Evidence:** A `species-compact-200.png`, `species-unknown-acquisition-compact-200.png`, versus latest E `confirmation-complete/targeted/collection-compact-200.png`. Current `CollectionView.xaml.cs:271` sets `SpeciesModeButton.Style = null`; `CollectionView.xaml:200–220` separates/fixes species filter widths; `:222–250` uses unthemed ListBox and unbounded SubText; `CollectionView.xaml.cs:419–422` embeds full RouteHint inside unknown cards alongside the dedicated action.

**Counterpart:** actual R `review.html` species and `snapshots/revision2/species-final.jpg`: shared selected segment, compact filters, regular sprite cards, short unknown/evolution captions and acquisition disclosure. Exact-form ordinary/shiny masking remains authoritative.

**Smallest correction:** use existing selected/secondary mode styles both ways; share My Pokémon filter rhythm and themed list/container. Bound name wrapping and card metadata; show a brief origin label and place full canonical route/pair lists in existing “Где найти?”. Make rarity value fit. Preserve ListBox keyboard/selection semantics, filtering, pagination, ownership navigation and unknown-name masking.

### 4. Important — bounded shop quantity and one primary card action

**Consequence:** buying multiple berries requires selecting/replacing a small “1” manually. Large green “Выбрать питомца”/“К поимке” navigation competes with the smaller green purchase button inside the same card.

**Evidence:** E `human-confirmation/hierarchy/shop-native-quantity-base-150.png`. `src/PokeCorner.App/Popup/ShopView.xaml.cs:100–111` creates only a 52-DIP TextBox, total and Buy; `:112–130` inserts full-width default-primary navigation. Existing validation and long total arithmetic are at `:140–144`, `:162–167`.

**Counterpart:** R `review.html` shop and `snapshots/shop.jpg` use minus/count/plus for berries with clear price/purchase. Latest human instruction explicitly requires pleasant bounded quantity interaction with house primitives and useful direct keyboard editing retained.

**Smallest correction:** house minus/plus around count, bounds from real purchase contract, clear pack/item units, keyboard operation and local invalid-input feedback. Preserve parsing, overflow-safe total, shortage, pending and idempotent handling. Purchase stays primary; use/navigation secondary. Keep all twelve real illustrations. No new dependency or numeric-widget framework.

### 5. Important — consistently identify duplicate individuals

**Consequence:** adjacent expedition candidates can show identical species, level, type, route effect and trait, with no stable visible distinction. Users cannot reliably choose the intended duplicate or distinguish selected slots. Rendering 800 buttons is insufficient for explicit individual choice.

**Evidence:** E `human-confirmation/hierarchy/expedition-team-search-role-compact-200.png` shows identical adjacent Bulbasaur cards. `src/PokeCorner.App/Popup/ExpeditionView.xaml.cs:227` defines display name without discriminator; `:303–316` builds candidate/automation labels without caught date or stable ordinal. Latest My Pokémon frame also has identical first two cards. Existing A `nursery-select-800-individuals-compact-200.png` already uses “Экземпляр 277/278” plus date; its old search chrome is historical.

**Counterpart:** `UI-SYSTEM.md` requires duplicates distinguishable by nickname/level/shiny/date; `LATEST-REVIEW.md` requires explicit individual selection in the 800-entry picker.

**Smallest correction:** reuse nursery's human-readable discriminator on genuinely duplicate collection/team candidates and selected slots, with concise caught date where useful; carry it into accessible names. Avoid GUIDs or a new permanent identity system. Preserve sort, nickname, shiny, traits and availability.

### 6. Important — preserve danger semantics in shared confirmations

**Consequence:** irreversible transfer and abandonment of expedition progress use the same green emphasis as safe purchase/continue. Warning prose exists, but the final control loses the accepted destructive role.

**Evidence:** A `confirm-rehome-compact-200-action.png`, `confirm-cancel-compact-200-action.png`. `src/PokeCorner.App/Popup/ConfirmationOverlay.xaml:15` always applies `UiPrimaryButtonStyle`; `ConfirmationOverlay.xaml.cs:12–17` changes only content, not action role. Actual transfer caller `CollectionView.xaml.cs:1337` includes name/price/irreversible consequence: generic fixture screenshots do not prove real caller omissions.

**Counterpart:** R `snapshots/revision2/confirmation-overlay-final.jpg` uses danger-styled transfer and safe initial Cancel; `UI-SYSTEM.md` reserves `#9B3833` for destructive actions. UI UX Pro Max destructive-emphasis agrees.

**Smallest correction:** allow existing shared confirmation to apply established danger treatment for destructive callers; keep safe confirmations green. Audit callers together. Preserve Cancel-first focus, focus trap, Esc/close, blocked background navigation and consequence copy. Technical history disclosure is legitimate optional content, not a reason to remove it.

### 7. Important — restore visible expedition choices and illustrated launch terms

**Consequence:** finite difficulty/duration choices hide in ComboBoxes; route selection is barely distinguished by weight. Canonical pinned reward terms become a long sentence while berry/coin icons stay in the scroll body. Launch is visible and numbers exist, but comparison and the accepted image-led decision flow weaken.

**Evidence:** E `human-confirmation/hierarchy/expedition-selected-pinned-footer-compact-200.png`. `src/PokeCorner.App/Popup/ExpeditionView.xaml:34` places illustrated PreviewRewardCards in body; `:63–66` pins Start/team and text. `ExpeditionView.xaml.cs:203–205` already derives both from one canonical preview. Frame shows dropdown-only difficulty/duration and near-identical route pills.

**Counterpart:** actual R `review.html` and `snapshots/expedition-team/team-selected-base.jpg` show visible segments, clear selected route and compact berry/coin/XP near launch. That reference's older grove art is historical; preserve all eight newly approved route assets.

**Smallest correction:** existing pill/segment vocabulary for finite difficulty/duration choices with compact wrapping; clear selected route state. Put a compact existing illustrated reward row beside fixed launch with concise return time. Keep detailed contributions scrolling. No duplicate math or percentages; no new expedition visual world.

### 8. Minor — catch explanation must follow current source

**Consequence:** expedition encounter simultaneously says a gift ball is sealed and expedition result predetermined whenever gifts remain elsewhere. Looking at a known expedition Pokémon, the user receives an unrelated gift explanation and an unnecessarily long charm paragraph.

**Evidence:** A `encounter-compact-200.png`. `src/PokeCorner.App/Popup/CatchView.xaml.cs:121–124` appends gift copy if any gift remains, then independently expedition copy for selected encounter.

**Counterpart:** R `review.html` known encounter uses one contextual charm explanation; `UI-SYSTEM.md` asks for one concrete reason and next action.

**Smallest correction:** select one short reason from current encounter/source. Gifts elsewhere belong in queue status. Preserve sealed-result/charm rules, unavailable-art behavior, timing and saves.

## Improvements retained and evidence distinctions

- Latest supported Eevee frames fix labeled Russian Nature placement, rarity/type scale, item illustrations and root-left/eight-branch composition. Do not reopen superseded Nature/giant-rarity/missing-art complaints.
- Latest compact evolution shows Later/Evolve and shortage navigation initially outside body, with illustrated cost/selected stone. An optional selector partially below the body fold is not an absent primary action. Linear/evolved ordering is source-backed and has recorded assertions, but there is no fresh full-size linear frame in the override index after footer changes.
- Personal body has an obvious scrollbar. Long 689 fact is demonstrated by its explicitly retained frame; its older rarity/Nature/header treatments are historical. Exact-form art unavailability is truthful, not permission to show wrong normal/shiny art.
- Team/parent selection has pinned controls, search and pagination. Latest rounded input fixes old white TextBoxes. The nursery ordinal is useful; rarity clipping is a concrete fit case for the shared control pass, not another font ticket.
- Corner/companion/trophies, shelf/album, known/unknown catch, six-step onboarding and local hatch are materially present. Latest settings Back is visible. Technical diagnostics appropriately remain optional.
- Actual restore source includes timestamp, replacement consequence and second restore confirmation (`SaveHistoryWindow.xaml.cs:65–69`). Older synthetic confirm screenshot lacks these details and cannot establish a current copy defect. UI does not prove save safety.

## Actual frame inventory and coverage

**47 native PNG frames plus 6 approved JPG references opened through view_image**, not inferred from contact sheets. All 13 explicit latest overrides examined. Representative whole-journey review; not all 210 PNGs/state×size combinations inspected.

Latest 13 overrides:

- E `human-confirmation/hierarchy/`: `detail-supported-eevee-base-150.png`, `detail-supported-eevee-compact-200.png`, `expedition-selected-pinned-footer-compact-200.png`, `nursery-selected-pinned-footer-compact-200.png`, `expedition-team-search-role-compact-200.png`, `nursery-select-search-role-compact-200.png`, `shop-native-quantity-base-150.png`, `settings-visible-back-compact-200.png`.
- E `human-confirmation/evolution-footer/`: `evolution-with-stone-compact-200.png`, `evolution-without-stone-base-150.png`.
- E `confirmation-complete/targeted/`: `detail-longest-fact-689-visible-compact-200-action.png`, `tour-5-silhouettes-base-150.png`, `collection-compact-200.png`.

Assembled 34 frames:

- A `corner-compact-200.png`, `achievements-compact-200.png`, `species-compact-200.png`, `species-unknown-acquisition-compact-200.png`, `nursery-compact-200.png`, `nursery-select-800-individuals-compact-200.png`.
- A `expedition-compact-200.png`, `expedition-running-compact-200.png`, `expedition-ready-compact-200-action.png`, `expedition-result-compact-200.png`, `nursery-running-compact-200.png`, `nursery-ready-compact-200-action.png`, `nursery-hatching-compact-200-action.png`, `nursery-result-compact-200-action.png`.
- A `catch-unknown-compact-200.png`, `catch-animation-known-phase-0-compact-200-action.png`, `catch-animation-known-phase-2-compact-200-action.png`, `catch-animation-unknown-phase-5-compact-200-action.png`, `catch-result-compact-200.png`, `catch-reduced-reduced-motion-base-150-action.png`, `save-history-compact-200-action.png`, `confirm-restore-compact-200-action.png`.
- A `confirm-rehome-compact-200-action.png`, `confirm-cancel-compact-200-action.png`, `tour-1-compact-200.png`, `tour-2-compact-200.png`, `tour-3-compact-200.png`, `tour-4-compact-200.png`, `tour-6-compact-200.png`, `tour-6-all-opened-compact-200-action.png`.
- A `encounter-compact-200.png`, `components-audio-zero-and-controls-compact-200-action.png`, `catch-compact-200.png`, `expedition-running-compact-200-action.png`.

Approved 6: R `snapshots/revision3/eevee-detail.jpg`, `snapshots/revision3/parent-selector-800.jpg`, `snapshots/revision2/species-final.jpg`, `snapshots/expedition-team/team-selected-base.jpg`, `snapshots/shop.jpg`, `snapshots/revision2/confirmation-overlay-final.jpg`. Historical art/data/copy differences are subordinate to current review.html/LATEST-REVIEW.

Some assembled preparation/body frames predate scene-height/footer fixes. Used only for journey presence or unchanged content. Revised personal/evolution/selected nursery/selected expedition/header/input findings use latest indexed frames plus source. Action-scrolled pictures are not initial-visibility proof.

## Limits and scoped follow-up

Existing frames cannot certify pointer feel, hover/focus everywhere, screen-reader output, physical input, per-monitor DPI, animation rhythm, audio, offline/startup, private recovery wiring, installed saves or MSI upgrade/rollback. No test rerun; recorded 19/19 and 101/101 remain implementer evidence. Generic confirmation fixtures are not actual-caller proof. No claim of full all-state acceptance.

Use this one finish contract: repair eight shared causes, preserve accepted world/mechanics/ownership/masking, then inspect changed paths at base/compact with long names/duplicates. Include fresh linear evolution and altered nonselected nursery/expedition states in the same writer's scoped follow-up evidence. Verify rename keyboard/cancel/focus, separate retraining, quantity bounds/validation, species browse/acquisition, nursery terms, expedition comparison and destructive roles. No new architecture, palette, art, options or unrelated features.
