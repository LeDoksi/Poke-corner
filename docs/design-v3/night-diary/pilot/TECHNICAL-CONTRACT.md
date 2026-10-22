# R2 expedition technical contract

Read-only source audit, 21 October 2026. Repository root for all relative source/test paths below: `C:/Users/Georgy/.codex/worktrees/global-update/PokeTokenBar-Windows`, checkpoint `f958a145`. No app or tests executed; no real saves read. This document records existing behavior, not new gameplay requirements.

## Routes

Exact UI names from `src/PokeCorner.App/Popup/ExpeditionView.xaml.cs:26–31`; berries/types from `docs/design-v2/final/catalog-rules.json` routes.

| ID | UI name | Primary berry | Matching types |
|---|---|---|---|
| grove | Лесная роща | Равст | Травяной, Насекомый, Ядовитый |
| lake | Озёрный берег | Оран | Водный, Обычный |
| coast | Морское побережье | Печа | Водный, Летающий, Волшебный |
| mountains | Горные тропы | Често | Каменный, Боевой, Драконий |
| desert | Пустынные дюны | Аспир | Земляной, Тёмный |
| volcano | Вулканическая долина | Чери | Огненный, Стальной |
| snow | Снежный перевал | Оран | Ледяной, Обычный |
| ruins | Древние руины | Често | Призрачный, Психический, Электрический |

## Chosen pilot fixture: computed from source, not executed

`grove / normal / 90 minutes`; three different GUIDs: normal Bulbasaur #0001, normal Chikorita #0152, normal Pikachu #0025. Each has XP1200 (=level10), first trait `caring-parent`, no second trait; all resource/XP remainders zero. Active desktop Eevee is a separate individual. No participant is in another running/ready expedition or nursery. These normal variants must use their packaged catalog assets.

`ExpeditionService.CalculateRates` (`src/PokeCorner.Core/Domain/ExpeditionService.cs:305–325`) and `docs/design-v2/final/balance.json` give:

- Average level10 meets normal recommendation10: no underlevel penalty.
- Bulbasaur and Chikorita match grove; Pikachu does not. A member counts once even when two types match.
- Common bonus = `(10−1)×0.0025 + 2×0.03 = 0.0825`.
- Three-member composition multiplier = 1. Caring-parent has no expedition resource, XP or time effect.
- Primary berries = `floor(2×1.0825×90/60) = floor(3.2475) = 3 Равст`.
- Coins = `floor(8×1.0825×90/60) = floor(12.99) = 12`.
- XP = `100×90/60 = 150 per participant`, `450 team`.
- Initial return is start +90 minutes; subsequent activity may accelerate it.

Guaranteed preview for this fixture: **Равст ×3 / 12 монет / 150 XP каждому**. Do not show 13 coins for this exact trio/level/trait/remainder fixture. Species/type matching affects resource rates and encounter/extra-find probabilities; it does not affect base XP or initial duration here. Extra finds/encounter are possible, not guaranteed, and excluded from these totals.

Existing asserted reference fixture, not the chosen grove composition: `tests/PokeCorner.App.Tests/GlobalUpdate/expedition_coreTests.cs:149`, `SixHourLevelTenExampleAndCancelBoundary`, uses three individuals of a dynamically selected first directly issuable species, XP1200/caring-parent, lake/normal/360: 12 Oran, 49 coins, 600 XP each. The chosen fixture above is a source-arithmetic adaptation, not an assertion already executed by that test.

## Configuration and eligibility

Difficulty labels: Лёгкая/easy, Обычная/normal, Сложная/hard; recommended levels 1/10/25. Duration labels: 30 минут, 1,5 часа, 3 часа, 6 часов; values 30/90/180/360. Recommendation is not a minimum-level gate. Source: `src/PokeCorner.App/Popup/ExpeditionView.xaml:18–24`, balance.json.

One running/ready expedition maximum; 1–3 distinct existing participant GUIDs, duplicate species allowed. One/two participants receive main resources and XP; only a full trio qualifies for extra finds and at most one encounter. Full trio does not guarantee either.

Presentation selector excludes active desktop individual, running/ready expedition/nursery participants, and unavailable normal/shiny variant art. Exact reasons: «На рабочем столе», «В походе», «В питомнике», «Изображение этой формы пока недоступно». Active/art restrictions belong to the UI selector; Core validates distinct/existing/not-busy participants. Sources: `src/PokeCorner.App/Popup/ExpeditionTeamSelector.cs`, `src/PokeCorner.Core/Domain/ExpeditionService.cs:281–325`.

## Preparation and picker

Empty preview: «Выберите хотя бы одного участника.» Helper: «Выберите команду из 1–3 питомцев. Полная тройка может принести дополнительные находки и встречу.» Selected preparation shows guaranteed berry/coins/XP, return time, per-participant level/XP, route match and trait effects; no numeric probability display required by existing UI.

Picker search covers name/nickname/number; filters are type, unlocked trait, Доступные (default on), Подходят маршруту. No rarity filter. Sort labels: Рекомендованные / Высокий уровень / Номер. Recommended order: available, matching type, meets recommended level, descending level, species number. Trait filter accepts either unlocked trait; second trait shown/unlocked at level10.

Twelve candidates/page, three slots. Fourth unselected candidate disabled when three selected. Selection remains draft until «Готово»; «Отмена»/Escape discards it. Reset filters preserves draft selections. Availability rechecked before apply; invalidated choices removed. Empty search: «Никого не нашли. Измените фильтры.» Count: «Выбрано N / 3 · найдено M». Sources: `src/PokeCorner.App/Popup/ExpeditionView.xaml.cs:196–221,265–329`; `ExpeditionTeamSelector.cs`.

## Running, ready, result, encounter

Running: «Команда в походе», preserved route/difficulty/duration/team, remaining rounded-up minutes, return time, progress, «Прервать поход». Offline time uses UTC. Trait/activity acceleration can shorten return; combined cap25%, activity component cap10%. Do not imply an immutable return time.

Ready: «С возвращением!», «Поход завершён. Находки ждут получения.», «Забрать находки»; cancel hidden. Participants remain busy until confirmed claim. Rewards do not expire; ready marker survives navigation until claim.

Result: «Экспедиция завершена», «Находки уже в запасах». Receipt shows actual granted berry/coin amounts and per-participant XP; extra findings are already included in totals. Legacy receipt absence has explicit fallback: «Точные количества ягод, монет и опыта этого старого похода не сохранены.» «Продолжить» returns to preparation.

Claim may create one closed resolved encounter of a known species without adding/discovering it yet. «Открыть встречу» enters catch flow; later opening uses frozen claim result, not a new draw/charm. Unopened encounter remains available. Sources: `src/PokeCorner.App/Popup/ExpeditionView.xaml.cs:365–433`, `src/PokeCorner.Core/Domain/ExpeditionService.cs`; `tests/PokeCorner.App.Tests/GlobalUpdate/expedition_coreTests.cs:171–210`.

## Early cancellation

Exact dialog: «Прервать экспедицию?» / «Команда вернётся без добычи и опыта.» / «Прервать». Destructive confirmation uses cancel focus and safe dismissal.

Allowed only strictly before ReadyAtUtc. After confirmation recheck trip identity, running status, clock and lifecycle. If completed while confirmation was open: «Поход уже завершён. Заберите находки.» Success: «Поход отменён без наград.» No partial loot or XP. Sources: `src/PokeCorner.App/Popup/ExpeditionView.xaml.cs:454–484`, `src/PokeCorner.Core/Domain/ExpeditionService.cs:177–188`.

## Pending, errors and retry

Start/cancel/claim/open block repeated actions during await. Inline polite MessagePanel shows errors; safe retry remains available. Failed start retains request/operation identity; failed claim retains claim ID for that trip. Successful completion clears pending identity. Suspension/generation checks suppress stale updates to hidden/replaced UI; confirmation rechecks after await.

Real service messages include «Уже идёт поход.», «Нужны 1–3 разных участника.», «Участник уже занят.», «Участник не найден.», «Поход ещё идёт.», «Награды уже сохранены.». Preview failure disables start and displays its reason. Commit-before-publish remains the AppState barrier, not a visual optimistic success. Sources: `src/PokeCorner.App/Popup/ExpeditionView.xaml.cs:437–523`; `src/PokeCorner.Core/Domain/ExpeditionService.cs`.

## Existing verification pointers; not executed here

- `tests/PokeCorner.App.Tests/GlobalUpdate/approvedExpeditionTests.cs:34,60,91,115,141,215`: draft cancel/three exact members; 800 individuals/paging/busy reasons; filters/recommendations; clock/unload cancellation; once-per-trip committed ready notification; dangerous cancellation.
- `tests/PokeCorner.App.Tests/GlobalUpdate/expedition_coreTests.cs`: offline/acceleration/persistence; partial teams; numeric reference; closed encounter claim and frozen later open.
- `tests/PokeCorner.App.Tests/GlobalUpdate/acceptanceVisualTests.cs`: real assembled WPF controls with synthetic data, POKE_SHELL_CAPTURE_DIR gate, temporary cache; existing team/search/selected fixtures.
- Current scope and acceptance sizes: `docs/design-v3/night-diary/PLAN.md` R2 and `INVENTORY.md`; full windows1000×760 and840×650 DIP. Test content dimensions can differ from whole-window dimensions.

No fresh runtime/DPI/physical-input/performance verification is claimed by this source contract.
