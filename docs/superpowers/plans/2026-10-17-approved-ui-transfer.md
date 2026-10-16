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

## Task 1: Shared shell, resources and confirmation host

**Files:** modify src/PokeCorner.App/Popup/AppMenuWindow.xaml(.cs), Theme/PokedexTheme.xaml, App.xaml, PokeCorner.App.csproj; extend App.xaml.cs only at menu construction/status integration; create Theme/UiSystem.xaml and Popup/ConfirmationOverlay.xaml(.cs); tests in tests/PokeCorner.App.Tests/GlobalUpdate/approvedShellTests.cs and affected shellTests.cs/ThemeRenderingTests.cs. Existing App/test WIP is outside this task except named integration lines.

**Interfaces:** retain MenuSection enum values, AppMenuWindow.Navigate(MenuSection), SelectTab(int), ShowCatchReveal(CatchReveal), SetCatchReady(int), SectionViewed and existing constructor arguments. Add a final optional `Func<PokeCorner.Core.Persistence.SaveData>? getSnapshot = null` constructor argument and `Task<bool> ConfirmAsync(string title, string body, string confirmCaption)`. App supplies `() => _appState.ToSaveData(_catchReadyCount)`. Existing callers compile without changes. Overlay owns presentation/focus only; no persistence.

- [ ] Read the approved shell HTML/CSS and current callers/tests before editing. Record exact pre-existing dirty paths; never stage them incidentally.
- [ ] Write meaningful RED tests using existing WpfTestHost. Assert default1000×760/min840×650, five labelled sections in approved order, wallet reflecting supplied ShopCoins, encounter button count/navigation, hidden legacy subtab headers with catch/album still navigable. Test real confirmation Cancel/Esc/confirm results, original screen retained, underlying input blocked, focus restored, window close resolving false. Use actual controls/event routes; no implementation-text snapshots.
- [ ] Run `rtk dotnet test tests/PokeCorner.App.Tests/PokeCorner.App.Tests.csproj --filter FullyQualifiedName~ApprovedShellTests --no-restore` and retain actual failing output before production changes.
- [ ] Implement approved two-row shell (brand/wallet/encounters/settings/window chrome, then five equal tabs with existing vector icon vocabulary). Keep native drag/minimize/maximize/close, keyboard section navigation and existing compatibility methods. Hide old nested Home/Catch/Album tab strip while preserving content/navigation. Wallet/encounter count must be real state, not fixture constants; refresh on selection/load and while open using an existing update signal or one lifetime-bound timer. Reuse SetCatchReady and reduced-motion behavior.
- [ ] Add semantic role keys in UiSystem.xaml and reuse existing templates where they already match. Keep legacy resource keys resolving for views awaiting transfer. Normal button min40, primary48; explicit focus and disabled states. Include only6approved PNGs as Resource links under Assets/UiSystem: coin, nursery, nursery-waiting, nursery-ready, ball-closed, ball-open (byte-exact from docs/design-v2/ui-system/assets). Expose shared BitmapImage resources, avoid another parallel runtime asset registry. Use current Nunito.
- [ ] Implement ConfirmationOverlay as an asynchronous in-window control hosted above the retained content. Cancel gets initial focus, Tab stays inside, Esc cancels, closing window cancels, no duplicate active prompt. Restore focus and background input when complete. Ordinary existing Ask callers are migrated with their scenario in Tasks2–6; do not force a synchronous nested dispatcher bridge.
- [ ] Wire optional snapshot callback at production AppMenuWindow construction only. No service/save schema/economy edits. Fit initial window/minimum to available Windows work area; both accepted sizes remain renderable. No actual OS setting changes.
- [ ] Run focused ApprovedShellTests + affected ShellTests + ThemeRenderingTests, then one full App test run on final task tree. Retain failures and diagnose; never weaken unrelated GIF assertions or hide tests. Core gate not repeated for a presentation-only task unless Core changes.
- [ ] Self-review, commit only this task's owned changes, report exact commands/counts/exits, base/head, paths and any limitation. No full visual acceptance claim yet.

## Task 2: Corner, collection, personal card, evolution and shelf

**Files:** Popup/RoomView.*, CollectionView.*, CollectionViewModel.cs, CollectionCardModels.cs, AchievementsView.*, existing Theme resources; add a narrowly scoped fact reader under Catalog only if no existing API; GlobalUpdate/pokedexTests.cs, roomAchievementsTests.cs, evolution_uiTests.cs and ThemeRenderingTests.cs.
**Interfaces:** consumes Task1 resource keys/ConfirmAsync and existing production mutation delegates; preserves individual GUID identity and existing read/commit services. Fact source is prepared POKE-58 offline data, not descriptionRu. No rewritten evolution/achievement rules.

- [ ] Pin failing behavior tests for independent Normal/Shiny discovery and individual selection, species-based duplicates, closed-root acquisition hint, forked Eevee tree/stone selection, item confirmations and unearned trophy restrictions. Retain previously passing save-failure/retry tests.
- [ ] Replace room with accepted enlarged pet, identity/types/#/rarity, explicit care for evolution, XP and care actions; keep room/trophy art composition. Replace collection/detail/evolution as whole screens using approved controls/filter set; no Ready-to-evolve or Owned filter; Where-find separate button. Large stone card only when applicable, parallel branches, real prices/shortages, rehome and desktop actions. Use interesting sourced facts; missing full data tracked byPOKE-58 until Task7.
- [ ] Replace shelf/album with26trophies, progress/next tier and3slot assignment, preserving existing availability and confirmed commits. Migrate these scenario confirmations to `await shell.ConfirmAsync(...)` through existing delegates or a final optional dialog delegate; no synchronous dispatcher bridge.
- [ ] Run the affected tests at1000×760 and840×650 with long names/missing assets, then one final App gate. Commit owned paths/report. Visual refinement remains Task8.

## Task 3: Complete expedition flow and team selection

**Files:** Popup/ExpeditionView.*, existing catalog display helpers and App menu status/notification integration only; GlobalUpdate/expedition_uiTests.cs and new selector tests if needed.
**Interfaces:** existing preview/start/cancel/claim/open delegates, immutable trip/result receipt and Task1 confirmation host. Use actual CatalogPackage route/type/trait/level data.

- [ ] Write RED tests for initial empty team/disabled start, draft Save/Cancel, max3,800individual search/filter/sort, active/busy exclusions, current route/difficulty recommendations, normal/shiny duplicates and ready badge retained until claim.
- [ ] Build accepted route scene +3slots +settings/reward icons, full picker and running/ready/result screens. Filters search/type/either trait/available/route; sorting recommendations/level/#, real quantities from Preview, not browser estimates. Show both trait effect and route affinity. Retain frozen trip rewards, stable operation IDs and failures/retries. Migrate cancel confirmation. Emit ready notification once through existing notification infrastructure and persistent tab marker.
- [ ] Run focused expedition UI/service-connection tests and one App gate; commit/report. Do not retest whole game mathematically unless production service changes.

## Task 4: Complete nursery and parent selection

**Files:** Popup/NurseryView.*, existing catalog UI helpers, GlobalUpdate/nursery_uiTests.cs and relevant Theme tests.
**Interfaces:** existing nursery preview/start/hatch/ready/shortage/result delegates; Task1 assets/confirmation. Recipe engine and frozen Nest/outcomes stay authoritative.

- [ ] Write RED tests for empty initial parents,800individuals/200duplicates, only compatible distinct second parent, busy exclusion, draft selection, unavailable form and hidden frozen child until durable hatch.
- [ ] Transfer whole scene: approved empty greenhouse, integrated waiting egg and ready cracked egg; never overlay separate egg PNG. Search/group by species then exact individual. Show actual possible root children, trait effects, price/time/luck wording and optional talisman. Ready scene has single Hatch action; reveal chosen committed baby in same context, then result/card route. Detach animation/timer on unload.
- [ ] Run focused nursery UI and relevant layout/failure-retry coverage, final App gate; commit/report.

## Task 5: Catch and encounter choreography

**Files:** Popup/CatchView.*, CatchReveal.cs/CatchBallQueue.cs only if presentation binding needs it; App.xaml.cs wiring; GlobalUpdate/catchTests.cs and queue/onboarding integration tests.
**Interfaces:** existing TryPerformCatchAsync and committed CatchReveal, known expedition encounter identity, Task1 ball resources and encounter entry. Never spend charm/grant before confirmed durable result.

- [ ] RED: unknown gift/activity cannot disclose species before result; known expedition species remains visible; one grant/charm use under rapid click/skip/reopen/navigation; reduced-motion and unload callbacks.
- [ ] Build exact approved flow using approved PNGs: flight1100ms, capture650ms, landing800ms,3wobbles×1000ms, success700ms (6250total). Unknown activity/gift remains closed until safe reveal; known encounter shows sprite. Skip returns same committed result, interruption retains recoverable result, next catch resets captions. No CodePen asset/code import.
- [ ] Run focused catch/queue/service-boundary tests and final App gate; commit/report. Judge actual timing with the assembled app in Task8.

## Task 6: Shop, settings, save history and onboarding

**Files:** Popup/ShopView.*, SettingsView.*, SaveHistoryWindow.*, ConfirmDialog.* callers as needed, Onboarding/*, App.xaml.cs remaining prompts; GlobalUpdate/shopTests.cs,onboardingTests.cs and existing save recovery tests.
**Interfaces:** real shop/commit/recovery/onboarding delegates and Task1 overlay. SaveHistory can become hosted content while retaining standalone startup recovery fallback; no schema/reset/migration changes from presentation work.

- [ ] RED: all12shop items/6berries/quantity/shortage confirmation, care booster/XP entry routes, cancel-safe two-step reset/recovery, onboarding six steps and gift queue without duplicate grant.
- [ ] Transfer accepted illustrated shop, calm Chill-only settings, readable history and contextual confirmation, six illustrated onboarding steps. Ordinary menu confirmations stay over current content. Startup recovery without a menu retains safe existing standalone route. Technical details collapsed, save failure visible/actionable, no game formulas.
- [ ] Run relevant shop/settings/recovery/onboarding tests; final App gate, commit/report. No real data reset/restore.

## Task 7: POKE-58 complete offline interesting facts

**Files:** existing catalog-package source CSV, new offline fact data/provenance and bounded preparation/check script; runtime package/catalog loading and CollectionView fact binding; catalog tests.
**Interfaces:** one interesting Russian text per1025catalog species, same fornormal/shiny, keyed by real species ID; retain source English/version/language/SHA. Bind only discovered species, no network percard. No synthesized type/family/rarity filler.

- [ ] Preserve11approved curated facts. Select real PokéAPI flavor rows for remaining species, prepare Russian editorial text with source tracking, validate1025unique IDs/nonempty facts/source row equality and absence of service boilerplate. Read in bounded batches; do not mistake English coverage for Russian coverage.
- [ ] Write failing coverage/offline/hidden species tests, package dataset through existing runtime pipeline and connect reader. Check real complete output before markingPOKE-58done. Run catalog/fact UI tests and final App/Core gates only for touched projects; commit/report.

## Task 8: One assembled visual pass, then final acceptance

**Files:** tests/PokeCorner.App.Tests/GlobalUpdate/acceptanceVisualTests.cs, existing acceptance harness, docs/design-v2 final/REMAINING.md/runtime-package.json, release manifest/installer files only when actual acceptance stage starts.
**Interfaces:** all accepted Tasks1–7 and original T01–T19 services/save guards. POKE-34 stays backlog until these dependencies complete.

- [ ] Run one assembled synthetic-profile capture atbase/compact and representative work area/DPI; compare actual app with37approved states. Check hierarchy/illustrations/caption/action/focus/scrolling, complete known/unknown catch timing and sound; produce actual-app comparison foruser. User explicitly wants to see final app, not another static-comp-only review.
- [ ] Apply one coordinated material visual fix batch and one confirmation pass. Do not repeatedly dispatch tiny cosmetic reviews. New art if truly missing uses individual prompts and user approval before registration.
- [ ] Resume original T20 acceptance: synthetic old/new/malformed/recovery saves, no overwrite on ambiguous load, offline package/MSI static tables, complete covering gates and one whole-branch review pinned from6441a874e8a1562d50d4c6a2139db2b504fc822a. Preserve pre-existing failed logs/deferred findings for reviewer.
- [ ] Build concrete reviewable installer and release report before requesting any applicable real-profile/MSI/system action. Never claim upgrade safety from static UI proof alone. Update Kanban and final limits honestly.
