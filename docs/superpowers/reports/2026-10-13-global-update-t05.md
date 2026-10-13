# Приёмка T05 / POKE-48 — 2026-10-13

Независимое spec/quality review принято для code `401a5187e13c2d8c2458c19ee3251b8227536d32`. Ниже исходный отчёт реализации и доработок; старые промежуточные counts сохранены как история.

# T05 / POKE-48 — общий инвентарь и магазин

Base: `b19ad9d0a07da77732595256dca45c89fcb0a95b`. Head: `510fc4778d29b8398468869bcb255e5d1154dafd`.

## Поставка

- `ShopService.Goods` читает все 12 ID, цены и размеры пачек из установленного `final/balance.json`; шесть ягод доступны в одной прокрутке. `ShopView` использует 12 штатных PNG, показывает эффект, цену, запас, количество, стоимость и нехватку монет. Из UI удалены продажа яиц и старые непосредственные списания; существующие сохранённые яйца продолжают вылупляться.
- `ShopService.TryPurchase(SaveData next, string itemId, int quantity, Guid operationId)` проверяет v2, ID, количество, переполнение запаса и цены, монеты и журнал `CompletedOperations`. Сумма и товар добавляются в один снимок. Повтор того же ID сообщает сохранённый успех без второго списания. `AppState.CommitAsync` записывает снимок до публикации. UI удерживает один незавершённый клик и показывает ошибки записи.
- `ShopInventory.Berries/Items` отражают v2 снимок в live state. `Feeding.Treats` остаётся источником для лакомств; `Items["treats"]` формируется как сериализуемый alias. Ягоды и остальные предметы общие для будущих механик.
- `TryUseXpPotion(next, individualId, operationId, levels)` и `PotionPreview` дают 300 XP по общей кривой, показывают полезные/излишние XP и не тратят зелье на уровне 40. `TryUseCareBooster` применяет 7500 CP с защитой переполнения. Оба применения проходят тот же commit и журнал.
- `TrySpendChoiceItem(next, itemId, availableChoices, operationId)` поддерживает `evolution-stone` и `offspring-charm`, расходует предмет лишь при более чем одном допустимом исходе. Вызывающий код обязан вычислить допустимые варианты из каталога и сохранить выбранный исход **в том же снимке**. Это не самостоятельное игровое действие.
- `ShopView.SelectBerryShortage(berryId, required)` подставляет `max(0, required - owned)` без списания или покупки; T10 может открыть магазин с этим предзаполнением.

## Проверки

- `rtk proxy dotnet test tests/PokeCorner.App.Tests/PokeCorner.App.Tests.csproj --no-restore --filter FullyQualifiedName~ShopTests --verbosity minimal`: 10/10, exit 0.
- `rtk proxy dotnet test PokeCorner.sln --no-restore --verbosity minimal --filter "FullyQualifiedName!~FinishReviewEvidenceRendersPopulatedPokedexAndResetArea&FullyQualifiedName!~StartStop_WithNoControllerConnected_RecordsNothingAndDisposesCleanly"`: Core 228/228, App 372/372, exit 0. Исключены только тест захвата экрана, которому нужен `POKE_SHELL_CAPTURE_DIR`, и тест, предполагающий отсутствие ввода с физического геймпада. Остальные ShellTests и GamepadInputHookTests выполнялись.
- `rtk git diff --check`: exit 0.
- Синтетические тесты покрывают нехватку, нулевое/огромное количество, переполнение запаса, повтор ID, отказ записи без live-публикации, двойной клик при незавершённом commit, выбор одного исхода, максимальный и частично полезный XP, максимальный CP, предзаполнение рецепта и отсутствие траты до нажатия. Реальные сохранения не открывались.

## Границы следующей поставки

- T07 берёт `Items["lucky-charm"]` при следующем допустимом shiny-исходе в своём атомарном снимке; текущий `ShopCatchLuckyCharmActive` сохраняется для совместимого чтения старых данных. Покупка нового талисмана пока только кладёт его в инвентарь.
- T13 вызывает `TrySpendChoiceItem` только после проверки нескольких допустимых ветвей. Питомник аналогично использует его для талисмана потомства и цены 14 ягод. Оба сохраняют выбранный исход и расход предмета одним commit.
- Полная визуальная матрица размеров/DPI и состояний остаётся T20. Проверены создание и раскладка WPF по текущим ShellTests; отдельная ручная аппаратная приёмка не выполнялась.
- Тесты этой задачи написаны после первой реализации; отдельный исходный RED-прогон не зафиксирован. Последующая попытка проверить отказ теста временным отключением проверки нехватки монет отклонена automatic approval review как риск записи отрицательного баланса. Отказ не обходился; GREEN-проверки указаны выше.

## Review round 1 — operation identity and pack price

- The existing `Dictionary<string, Guid> CompletedOperations` retains its shape. New shop, potion, care-booster, and choice entries store a SHA-256-derived GUID fingerprint of the operation type and request inputs. A same-input replay reports `Repeated=true`; a reused ID with different inputs returns failure without mutation. Legacy entries whose value was the operation ID cannot prove their original inputs, so a replay is refused without mutation; a caller can use a fresh ID for a new action. No persistence schema or validator changed.
- `TryPurchase` binds item ID and quantity; potion/care bind individual ID; choice binds item ID and available choice count. The treats row now labels 20 coins per five-item pack, while single-unit rows say per item.
- Test-first RED: `rtk proxy dotnet test tests/PokeCorner.App.Tests/PokeCorner.App.Tests.csproj --no-restore --filter FullyQualifiedName~ShopTests --verbosity minimal` initially hit fixture compile errors (`CS9035`, exit 1); after fixture correction it produced 5 behavioral failures, 10 passing, exit 1. The focused WPF label test using `--filter FullyQualifiedName~ShopTests.TreatPriceIsLabeledPerPackAndBerryPricePerItem` failed with `"Запас: 0 · 20 монет/шт."`, 1 failing, exit 1. No production guard was altered for RED.
- GREEN: `rtk proxy dotnet test tests/PokeCorner.App.Tests/PokeCorner.App.Tests.csproj --no-restore --filter FullyQualifiedName~ShopTests --verbosity minimal`: 16 passed, 0 failed, 0 skipped, exit 0. `rtk proxy git diff --check`: exit 0; only line-ending conversion notices.
- Next task interface stays `TryPurchase(SaveData next, string itemId, int quantity, Guid operationId)`, `TryUseXpPotion(next, individualId, operationId, levels)`, `TryUseCareBooster(next, individualId, operationId)`, and `TrySpendChoiceItem(next, itemId, availableChoices, operationId)`. Supply a fresh GUID per distinct request, and reuse it only for a retry with identical inputs.


## Independent scoped re-review

﻿# T05 / POKE-48 — re-review round 1

Pinned scope: cbd7557ab4509bf3422804bab70c7340e65978f5..401a5187e13c2d8c2458c19ee3251b8227536d32 (review-cbd7557..401a518.diff). Re-review of the two findings in task-5-review.md only; concurrent T06 work is outside this verdict.

## Findings

- **Important — reused operation ID with different request inputs: ADDRESSED.** ShopService.cs:44-75,86-131 fingerprints operation type and request inputs in the existing Dictionary<string, Guid> CompletedOperations. Matching input replays return Success=true, Repeated=true before another effect; changed purchase SKU/quantity, potion or care target, and choice item/count return failure before mutation. A legacy value equal to the operation ID cannot establish original inputs and is safely refused. The journal shape stays compatible with SaveData.cs:25. New tests cover changed purchase inputs, both targets, choice inputs, and legacy refusal (shopTests.cs:73-150).
- **Minor — 20 coins per five-treat pack mislabeled per item: ADDRESSED.** ShopView.xaml.cs:207 shows монет/пачка for treats and монет/шт. for one-unit goods. The WPF assertion checks the treat and berry labels (shopTests.cs:234-247).

## Verdict

**Spec compliance: PASS for the requested fix scope. Task quality: PASS for the requested fix scope.** No new Critical or Important issue is introduced by this diff. The choice primitive still relies on its future caller to commit the selected outcome in the same snapshot, as recorded in the original review; this fix does not change that handoff.

Evidence: reviewed the pinned diff and the reported RED/GREEN logs. task-5-fix1-red.log records five behavioral failures and one focused pack-label failure before the fix; task-5-fix1-green.log records 16/16 passing, exit 0. Tests were not rerun for this read-only re-review.
