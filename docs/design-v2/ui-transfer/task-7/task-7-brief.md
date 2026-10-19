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

## Task 7: POKE-58 complete offline interesting facts

**Files:** existing catalog-package source CSV, new offline fact data/provenance and bounded preparation/check script; runtime package/catalog loading and CollectionView fact binding; catalog tests.
**Interfaces:** one interesting Russian text per1025catalog species, same fornormal/shiny, keyed by real species ID; retain source English/version/language/SHA. Bind only discovered species, no network percard. No synthesized type/family/rarity filler.

- [ ] Preserve11approved curated facts. Select real PokéAPI flavor rows for remaining species, prepare Russian editorial text with source tracking, validate1025unique IDs/nonempty facts/source row equality and absence of service boilerplate. Read in bounded batches; do not mistake English coverage for Russian coverage.
- [ ] Write failing coverage/offline/hidden species tests, package dataset through existing runtime pipeline and connect reader. Check real complete output before markingPOKE-58done. Run catalog/fact UI tests and final App/Core gates only for touched projects; commit/report.


## Coverage, language and provenance requirements

POKE-58 is a release prerequisite, not optional follow-up. Saved source: docs/design-v2/catalog-package/source/pokemon_species_flavor_text.csv; accepted11rows: docs/design-v2/ui-system/flavor-facts.json. Consume actual final Task2B runtime subset/reader/resource interfaces from task-2b-report.md; extend production source, not UI demo data. Do not redesign any screen or change save/discovery/acquisition metadata. Root owns docs/Kanban.

Keep accepted11Russian facts and source selections byte-for-byte unless a demonstrated source defect is reported to root. For remaining species select an actual coherent English flavor row from CSV, preserve original source text (including raw whitespace where provenance requires), language/version/species ID and source URL. Source-file SHA must match actual CSV. Store editorial Russian explicitly not official translation. Text should give an interesting species-specific observation/behavior, no invented fact, type/family/game-rarity summary, numeric evolution/economy rule or English fallback pretending coverage. Russian names if used must correspond to catalog species, no accidental text borrowed from a different version/species. Normal/shiny same fact.

Use bounded source/translation batches with checkpointed artifacts in this ledger; never dump the entire CSV or all1025facts into tool output. One writer and no worker subagents per global plan. If all1025 cannot fit one context, checkpoint completed ID ranges/source choices and ask root for a controlled continuation, never claim partial as DONE. No network is necessary per-card; supplied source is local. If source truly lacks a species, report concrete ID/evidence, do not synthesize coverage or silently skip it.

Coverage checks:1025unique valid catalog IDs, nonempty Russian interesting text, genuine exact source-row identity/language/version, source SHA, no duplicate ID/accidental unchanged generic template, accepted11preserved. Offline package includes dataset and provenance through existing RuntimePackage.props/manifest rather than review-only path. Bind discovered individual only; undiscovered form search/detail must not reveal fact. Missing/malformed data fails safely with no fabricated summary, without touching save. Tests RED before completing data, then catalog reader/coverage/offline/masked-species/normal+shiny equality. Tests can't alone certify translation quality: self-review all prepared rows in bounded batches, report edited issues and source mapping evidence.

After all data prepared, run relevant Core/App gates once for touched projects. Retain known failures pending Task8, no assertion exclusion. Review raw coverage output and actual packaged source before marking complete. No profile/app/MSI/OS mutation, push, imagegen or subagents. Write task-7-report.md with completed ID ranges, row counts, source/provenance SHA, RED/GREEN commands/counts/exits/rawlogs, paths/base/head/self-review/concerns. Root updates POKE-58 after independent review.

Root bounded source-coverage preflight19Oct is in facts-source-preflight.json: source has68220rows and genuine English rows for all1025species, no missing IDs. CSV SHA059b24f07ccce256df1295b5d2fa19b82129cb6d5cfdf6a83455b6dffbcc5e9b. This proves source availability ONLY, not Russian translation/quality/packaging coverage. Worker still verifies source mappings and final data; no per-card network required.
Root source-version/provenance clarification: new rows must describe canonical baseline form, not regional alternate inadvertently described by newest English version. Inspect actual selected row (PLA39 and SunMoon/SwordShield legacy-regional traps); version exclusion alone not complete semantic proof. Accepted11artifact/text/sourceversions unchanged. Approved43v4/133v4/152v5 sourceText is already normalized by removing U+00AD/collapsing whitespace; sidecar stores exact sourceRawText from CSV and its UTF8 SHA separately, validates raw row equality, preserves normalized approvedsourceText independently. No false raw equality claim.
