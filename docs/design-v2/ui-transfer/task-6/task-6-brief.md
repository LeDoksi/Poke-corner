# Approved UI System → WPF Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development to implement this plan task-by-task. The user approved continuous execution and the complete visual kit on 17 October 2026: «да вроде все заебись как будто. можно наверное делать уже. фиксируем короче». Do not ask again to start or between tasks.

**Goal:** Replace the rejected half-old UI with the approved 37-state kit while preserving the accepted gameplay and save protections.

**Architecture:** Keep the existing WPF views and production service delegates; replace their compositions and shared presentation components. Ship approved PNGs as WPF resources, reuse runtime catalog/sprite/room services, and use an asynchronous in-window confirmation surface. Preserve existing public navigation/delegate contracts unless this plan explicitly extends them.

**Tech Stack:** .NET8 WPF, existing Nunito, native XAML templates/DrawingImage, packaged PNG/GIF and xUnit STA host. No WebView rewrite or new UI library.

**Spec:** docs/design-v2/UI-SYSTEM.md + ui-system/LATEST-REVIEW.md + ui-system/review.html (approved 37-state reference); GLOBAL-UPDATE.md and canonical final JSON remain gameplay authority.

## Global Constraints

- Existing isolated checkout: C:\Users\Georgy\.codex\worktrees\global-update\PokeTokenBar-Windows, branch codex/global-update. Retain all pre-existing WIP; never reset/revert/stage another task's paths. Prefix shell commands with rtk. Request sandbox escalation for writes/builds/git in this checkout.
- Пользователь принял комплект37состояний как эталон и разрешил перенос17октября2026. New art still requires an individual prompt and explicit approval before inclusion.
- Кремовая бумага #F8F4E9; лесной #31694D; текст #26352D/#50614F; границы #C8C2B3; опасное действие #9B3833; Nunito with Cyrillic. Base1000×760 DIP, compact840×650; reduce to actual available work area rather than clipping system controls.
- Five primary sections in order Уголок / Покедекс / Экспедиции / Питомник / Магазин. Settings, encounters, achievements, detail and confirmation remain reachable inside the shell. No second empty window for ordinary confirmation.
- No gameplay percentages/probabilities/formulas in text/tooltips/onboarding; short truthful cause→effect only. Exact prices, counts, level and time remain visible.
- Preserve Chill-only, root-only acquisition including standalone species, unsupported-asset guards, busy/active exclusions, persistent idempotency/frozen outcomes and commit-before-publish semantics.
- Never use real profiles or install MSI to exercise a UI change. No reset, installer launch, autostart changes, release or push without the applicable concrete final boundary. Synthetic temporary saves only.
- One implementer at a time; task-scoped functional/code review per whole feature batch. One coordinated visual comparison/fix/confirmation after all screens. No per-font agent or repeated whole-UI capture loops.
- Do not claim full Russian facts from the11curated examples: POKE-58 must supply the complete offline runtime coverage before POKE-34 final acceptance.

## Review Focus

1. Closing/navigating while confirmation or async mutation is pending must not grant resources or confirm accidentally (Task1 focus/close test; scenario tasks retry tests).
2. Live wallet and encounter/ready markers must come from current state across all pages, not hardcoded demo amounts (Task1 snapshot refresh test, Task3 ready-trip test).
3. Long Cyrillic names and800individuals must retain reachable actions at both window sizes (Tasks1–4 layout/selector tests).
4. Unknown/shiny forms and forked families must not leak names or present evolved species as obtainable directly (Task2 shape tests; Task4 hidden outcome tests).
5. Timers/GIF/animation callbacks must detach on unload and never mutate a hidden/new scenario (Tasks1/4/5 lifecycle tests).

## Task 6: Shop, settings, save history and onboarding

**Files:** Popup/ShopView.*, SettingsView.*, SaveHistoryWindow.*, ConfirmDialog.* callers as needed, Onboarding/*, App.xaml.cs remaining prompts; GlobalUpdate/shopTests.cs,onboardingTests.cs and existing save recovery tests.
**Interfaces:** real shop/commit/recovery/onboarding delegates and Task1 overlay. SaveHistory can become hosted content while retaining standalone startup recovery fallback; no schema/reset/migration changes from presentation work.

- [ ] RED: all12shop items/6berries/quantity/shortage confirmation, care booster/XP entry routes, cancel-safe two-step reset/recovery, onboarding six steps and gift queue without duplicate grant.
- [ ] Transfer accepted illustrated shop, calm Chill-only settings, readable history and contextual confirmation, six illustrated onboarding steps. Ordinary menu confirmations stay over current content. Startup recovery without a menu retains safe existing standalone route. Technical details collapsed, save failure visible/actionable, no game formulas.
- [ ] Run relevant shop/settings/recovery/onboarding tests; final App gate, commit/report. No real data reset/restore.


## Binding remaining flows and inherited startup failure

Transfer final accepted shop/settings/history/confirmation/onboarding states. Shop uses real catalog item art, all12items/6berries, quantity/price/shortage/stock and explicit item-to-pet routing through Task2B card, no new inventory mutator or demo stock. Chill remains only mode; technical recovery diagnostics may collapse, but failed save/recovery error remains visible/actionable. No game chance/percentage/formula even tooltips/help/onboarding. Preserve privacy-safe/history paths and safe startup recovery route when menu cannot yet exist.

Migrate ordinary menu confirmations to shared async ConfirmAsync over current screen with Cancel-first/Esc/focus-return; no synchronous DispatcherFrame bridge. Save-history may be hosted while preserving standalone startup fallback. Reset/recovery retain guarded two-step consequences and commit-before-publish; cancel/close must not restore/reset. No schema migration, backup retention or save-engine rewrite from presentation task; ask root for broader architecture. Synthetic temporary files only for tests.

Fix concrete inherited acceptance failure: StarterSelectionWindow.xaml.cs calls _fetchSprite before _fetchAnimatedFrames even when canonical offline frames already exist. acceptanceTests.OfflineStarterUsesCanonicalFramesBeforeAnyLegacyPngFetch uses throwing legacy/network delegate; make packaged canonical frames first, falling back only when genuinely unavailable. Preserve starter eligibility, offline safety and cancellation. Test actual fallback branches and retain error/failure semantics; don't weaken old assertion. No per-card/known-offline asset network requests added.

Six illustrated onboarding steps must be useful and concise, with exact committed gift queue, no duplicate gift after repeated clicks/reopen/restart or failed save. Existing save/recovery guard tests must stay meaningful. Treat actual startup no-menu/invalid-save branch separately from in-menu overlays; do not force unsafe new profile when old save is unreadable. Preserve current backup/restore and single-instance defenses.

Cover shop purchase quantity/stock/shortage/failed-write stable-operation retry, per-pet boosters route, cancel-safe reset/recovery, six-step navigation/gift idempotence and offline starter fallback. One final App gate; no unnecessary full Core unless production touched after root ruling. Write task-6-report.md. Full historical save-loss/upgrade/MSI acceptance remainsTask8/POKE-34, not certified by UI tests.

## Execution boundaries

Use the final accepted states from docs/design-v2/ui-system/review.html and LATEST-REVIEW.md, not obsolete build.py alone or reviewData fixtures in production. Read the previous accepted task reports only for their actual interface changes. Root owns docs/Kanban. One writer; you are not alone, never revert or stage other work. No subagents (including reviewers), imagegen, actual-profile app launch, real restore/reset, installer/OS mutation, push or release. Source/test changes only in named ownership; append optional service/callback dependencies to preserve public callers.

Prefix all shell commands rtk, use scoped escalation for worktree mutation/build/git. Behavioral RED→GREEN, focused iterations; one final App gate after source complete. Core gate only if Core production necessarily changed with root ruling. Do not rerun suites to erase a failure. Preserve raw logs/commands/counts/exits, source base/head/paths, self-review and concerns in the report. Initial known deterministic starter legacy-first defect belongsTask6; unresolved shared hero timing belongsTask8, never weaken/exclude assertions. A full actual-app visual comparison happens once after all scenarios, no per-font review/capture loop.


## Root forward preflight of existing menu recovery/shop boundary

Actual existing App.RestoreFromHistory is synchronous Window.ShowDialog with SaveMigration.RestoreHistory; RecoverSaveDataAndRestart disables the entire menu before calling it. A hosted history/ConfirmAsync path cannot operate beneath an IsEnabled=false host: preserve the existing startup standalone fallback, but make the in-menu history/selection/confirmation asynchronous and usable, and hold the recovery freeze only at the correct mutation boundary. Retain buffered expedition activity capture/flush, save-writer-failure history capture, no restore until deliberate confirmed selection, and cancellation/exception finally re-enables/resumes. Read actual current code after Task5; do not duplicate commit/recovery engines. Root authorizes the smallest optional AppMenuWindow hosted-content hook needed for the accepted in-shell history or onboarding route, preserving constructor/public navigation contracts. No general new navigation framework. Wrong grouping costs bounded local shell/App adapter work.

ShopView currently applies care/XP immediately to the active pet using its own handlers. Accepted Task2B card has canonical preview/use-item routes: the shop's Apply action should navigate/select the intended pet rather than silently apply to whichever active pet happens to exist; append an optional routing callback and use established card/selector, no second item-application engine. Talisman reservation integrates with Task5's accepted source-specific flow; inspect its final report before wiring shop activation. Old purchase handler allocates a new Guid for every retry; cover a stable-operation identity for a refused-write retry without changing canonical ShopService semantics. No UI-only grant/spend.

SettingsView construction reads autostart status, and the changed handler writes the registry. Tests must not toggle actual autostart or change OS settings; use a narrow injected existing-status/action seam if needed. Genuine sound-volume percentage describes the user's audio setting, unlike forbidden gameplay probabilities; do not invent a gameplay mechanics exception. UI control reachability at both sizes and truthful action feedback still apply.
Root authorized narrow CollectionView.xaml.cs ownership addition: public OpenItemSelection (or existing-style name) closes stale detail/species with existing helpers, switches existing owned-individual catalog, Refresh. App shop Apply route must use it rather than merely Navigate(Pokedex), which can retain an old detail. One meaningful old-detail -> ShopApply -> owned-catalog regression. Preserve filters unless genuinely inaccessible; no new selector or item application engine, no visual redesign. Record in report.
