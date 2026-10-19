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

## Task 5: Catch and encounter choreography

**Files:** Popup/CatchView.*, CatchReveal.cs/CatchBallQueue.cs only if presentation binding needs it; App.xaml.cs wiring; GlobalUpdate/catchTests.cs and queue/onboarding integration tests.
**Interfaces:** existing TryPerformCatchAsync and committed CatchReveal, known expedition encounter identity, Task1 ball resources and encounter entry. Never spend charm/grant before confirmed durable result.

- [ ] RED: unknown gift/activity cannot disclose species before result; known expedition species remains visible; one grant/charm use under rapid click/skip/reopen/navigation; reduced-motion and unload callbacks.
- [ ] Build exact approved flow using approved PNGs: flight1100ms, capture650ms, landing800ms,3wobbles×1000ms, success700ms (6250total). Unknown activity/gift remains closed until safe reveal; known encounter shows sprite. Skip returns same committed result, interruption retains recoverable result, next catch resets captions. No CodePen asset/code import.
- [ ] Run focused catch/queue/service-boundary tests and final App gate; commit/report. Judge actual timing with the assembled app in Task8.


## Binding choreography and transaction boundary

Use Task1 approved closed/open ball PNGs and existing packaged scene/Pokémon frames; no redraw or external CodePen import.6250ms total: flight1100, capture650, landing800,3wobbles×1000, success700. Brief pauses inside wobbles, not frantic continuous shaking. Known expedition Pokémon is visible before throw; generic activity/gift begins unknown/closed, and no premature species/result access in labels/tooltips/image-source. Within ONE throw, «Кто внутри?» must not reappear as a second unknown introduction, and it must never appear for a known expedition encounter. A new independent sealed unknown ball may show its initial question once before Throw; no persisted first-ever-catch flag is requested. Correct reset of captions/charm/scene for next item.

Existing committed CatchReveal/receipt and TryPerformCatchAsync stay authoritative. Talisman activation is explicit and shows available stock; selection changes presentation/request only. Spend/grant are confirmed durably exactly once by producer, not at animation phases. Save refusal keeps live pet/inventory/queue and enables same-operation retry. Rapid click, skip, unload/reload and navigation must never create a second grant or spend. Skip goes to the SAME committed result; interruptions retain recoverable result, delayed callbacks can't switch a new/hidden scenario. Failure state reachable and truthful, no swallowed receipt/save error.

Implement reduced-motion policy by Windows preference (reference uses250ms calm reveal), without changing OS. All callbacks/timers/animators detach on unload; reattach once when needed. Encounter entry remains clearly marked via accepted shell APIs from Task3. Ordinary confirmations if any use shared ConfirmAsync, no empty Window/MessageBox/nested dispatcher bridge. Preserve source-specific known/unknown handling, root-only acquisition including standalone families and unsupported-art guard.

Cover RED/GREEN exact known/unknown captions, phase ordering/timing contracts, same receipt after skip/reopen, charm selection/stock, refused write, cancellation during animation and reduced-motion. Actual animation rhythm/sound judgedTask8. Write task-5-report.md.

## Execution boundaries

Use the final accepted states from docs/design-v2/ui-system/review.html and LATEST-REVIEW.md, not obsolete build.py alone or reviewData fixtures in production. Read the previous accepted task reports only for their actual interface changes. Root owns docs/Kanban. One writer; you are not alone, never revert or stage other work. No subagents (including reviewers), imagegen, actual-profile app launch, real restore/reset, installer/OS mutation, push or release. Source/test changes only in named ownership; append optional service/callback dependencies to preserve public callers.

Prefix all shell commands rtk, use scoped escalation for worktree mutation/build/git. Behavioral RED→GREEN, focused iterations; one final App gate after source complete. Core gate only if Core production necessarily changed with root ruling. Do not rerun suites to erase a failure. Preserve raw logs/commands/counts/exits, source base/head/paths, self-review and concerns in the report. Initial known deterministic starter legacy-first defect belongsTask6; unresolved shared hero timing belongsTask8, never weaken/exclude assertions. A full actual-app visual comparison happens once after all scenarios, no per-font review/capture loop.
Root preflight actual source: CatchBallQueue currently counts only activity/gift and NextReady excludes expedition. ExpeditionView.OpenEncounterButton_Click currently calls _open(id), which commits adding the pet immediately. App.OpenExpeditionEncounterAsync directly opens; CatchView current fetch callback is species-only legacy PNG. Accepted known encounter flow must enter a visible pre-throw scene without invoking the Open/grant producer merely for navigation, pin the chosen encounter ID, then use the existing confirmed producer on Throw (not accidentally oldest gift/activity). Root authorizes minimal ExpeditionView optional known-scene entry callback + App wiring and CatchBallQueue/CatchReveal presentation extensions while retaining existing public delegates/constructor compatibility. Include unopened resolved expedition encounters in meaningful readiness/entry as appropriate; no Core RNG/economy/issuance rewrite/new save engine. Canonical normal/shiny packaged asset provider before legacy/network fallback; no normal substitute for unavailable shiny. Inspect exact source and decide minimal interface; report architecture ambiguity before broad change. Lucky charm uses existing durable ShopService activation reservation, not UI inventory mutation: distinguish reserved active unit from final consumption, preserve unsupported/frozen gift/resolved outcome behavior and failed write. Activation itself only after its confirmed candidate save, catch result/grant/consumption only through confirmed canonical producer; no false promise that a frozen gift/expedition result can reroll.
