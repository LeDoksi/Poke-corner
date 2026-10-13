# Приёмка T11 / POKE-42 — 2026-10-13

Независимое spec/quality review принято для code `35d9706bda2e840711fe8d06fb4da420f19e4a70`. Ниже исходный отчёт реализации и доработок; старые промежуточные counts сохранены как история.

# T11 — питомник: гнездо, наследование, однократное вылупление

Реализован core и команды App на ветке `codex/global-update` поверх `e0a8861287c83f7d5b140307178b198560faddfc`. Полный экран принадлежит T12, общая визуальная матрица — T20. Реальные пользовательские сохранения, MSI и системный UI не затрагивались.

## Контракт и доступные API

- `new NurseryService(CatalogPackage, proposed SaveData)` даёт `Preview(parentIds, selectedSpeciesId?, now?)`, `Start(NurseryRequest)`, `Hatch(nestId, operationId, now?)`, `RefreshReady(now)`. `NurseryRequest`: два GUID родителей, необязательный выбранный species ID, стабильный GUID запуска и UTC-время. Все изменения выполняются только в предложенном снимке. `AppState.CommitAsync` записывает его и публикует успех после подтверждённого commit.
- `App.PreviewNursery`, `StartNurseryAsync`, `HatchNurseryAsync`, `RefreshNurseryReadyAsync` — команды T12. Изменяющие команды используют `CommitStateAsync` и ожидают экспедиционный progress task. Отказ возвращает игроку причину через `InvalidOperationException`, опубликованное состояние остаётся прежним.
- `NurseryPreview` даёт доступные исходы, исходные нормализованные веса как `OutcomeChances`, точную цену, время/UTC готовности, списки допустимых особенностей по каждому виду и shiny-шанс по каждому исходу. Выбор вида разрешён только при нескольких доступных исходах.
- `SaveData.Nest` — `NurseryState` с сохранённым ребёнком, parent IDs, стоимостью, временем, пригодностью shiny и броском. Состояния `running → ready → hatched`; ready не откатывается при переводе часов назад. Ребёнок скрыт из `NurseryAction` запуска и показывается через результат `Hatch`. T12 не должен раскрывать `Nest.Child` в случайном режиме до вылупления.
- Родители берутся по текущему `CurrentSpeciesId`, включая развитые сохранённые экземпляры; ребёнок — полный family root, включая baby и одиночные виды. T10 `NurseryCatalog` определяет допустимую пару, доступные исходы, веса, стоимость и длительность. `ExpeditionService.BusyIndividualIds` исключает родителей из running/ready экспедиции или гнезда. Наследование берёт уникальные разрешённые родительские особенности; при пустом пуле — разрешённый общий пул. `fast-grower` исключён для ребёнка без дальнейшей эволюции. `caring-parent` и общий предел времени берутся из баланса.
- Выбор вида использует T05 `ShopService.TrySpendChoiceItem` внутри того же кандидата, после проверки полного species ID и рецепта. Shiny-решение, черты, характер и время фиксируются при запуске. Фактический normal-вид выбирается по исходным весам до проверки его shiny-ресурса. Активный lucky charm расходуется при пригодном запуске; удача питомника меняется только при подтверждённом пригодном вылуплении. Непригодный исход сохраняет charm и накопленный бонус.
- Идентичный GUID запуска повторяет исход; другой payload с тем же GUID отвергается. Hatch по тому же operation ID повторяет сохранённого ребёнка без новой выдачи, а другой ID отвергается. `JsonSaveStore` проверяет payload гнезда, ID, фазы, связь родителей и child, shiny draw/eligibility и claim marker; `SaveMigration.ValidateCatalog` проверяет также сохранённого ребёнка.

## Проверка

- RED до production-правок: `task-11-red.log`, focused Core ExitCode 1, CS0246 — отсутствующий `NurseryRequest`. Валидная синтетическая catalog/package fixture и поведенческие сценарии созданы до реализации.
- Итоговый focused Core: `task-11-focused.log`, ExitCode 0, 9/9. Проверены развитые родители → baby root, одно гнездо/ready lock/double hatch, занятые родители и неверный выбор без списаний, точная доплата и предмет, single-outcome запрет выбора, исходные веса и `caring-parent`, отказ записи при запуске/вылуплении с повтором после загрузки, fallback особенности, пригодная/непригодная shiny-попытка и удача.
- Неотфильтрованный solution gate: `task-11-gate.log`, ExitCode 1, Core 238/238, App 403/404. Единственная ошибка — известный screenshot-capture environment тест `FinishReviewEvidenceRendersPopulatedPokedexAndResetArea` в `shellTests.cs:211`.
- Итоговый solution gate после последней правки: `task-11-gate-filtered.log`, ExitCode 0, Core 238/238 и App 403/403; исключён только этот screenshot-environment тест. Во всех логах сохранены точные `COMMAND` и `ExitCode`. `git diff --check` ExitCode 0.

## Передача и границы

T12 строит экран на командах App, показывает стоимость/исходы/особенности/shiny из `NurseryPreview`, удерживает случайный исход скрытым, отображает running/ready по сохранённому `Nest` и сообщает ошибки команд. `RefreshNurseryReadyAsync` вызывается при наблюдении дедлайна; `HatchNurseryAsync` сам проверяет его на текущем снимке. Никакой второй store или legacy shop egg engine не создан.

Намеренно не добавлена отмена запущенного гнезда: канонический NURSERY-контракт определяет сохранение результата до подтверждённого вылупления, но не цену/возврат/момент отмены; T12 не должен показывать неподтверждённую кнопку отмены. Тема каждой смешанной пары, реальные игровые сессии и визуальная DPI-матрица остаются отдельной приёмкой T20. Известный screenshot-env тест остаётся непроверенным в этой среде.

## Review1 fix batch (2026-10-13)

- `TraitRules.Allowed(int speciesId, bool isShiny, CatalogPackage catalog)` проверяет доступность непосредственного следующего вида для фактического варианта. `NurseryService.Start` сначала фиксирует исход и shiny, затем формирует наследуемый пул. `NurseryPreview.TraitsBySpecies` и `ShinyTraitsBySpecies` отдельно показывают возможные normal/shiny пулы без раскрытия сохранённого случайного ребёнка. `EncounterService.Open` использует тот же variant-aware helper для новых выдач; ранее сохранённые черты не меняются. Тесты используют реальные family roots 679→680 (нет normal asset) и 714→715 (нет shiny asset), а не неканонические 654/651.
- `AppState.IsReadyToEvolve` и `TryEvolve` сверяют общий `BusyIndividualIds`, блокируя мутацию running/ready родителей питомника и экспедиции. `CollectionViewModel.Release` получает текущий busy set из `App`, реально отказывает в освобождении; кнопка detail отключается для занятого питомца, а отказ не вызывает `_onStateChanged`. Освобождение после подтверждённого hatch вновь доступно.
- `SaveData.HatchedNurseryResults` хранит отдельную неизменяемую копию вылупившегося ребёнка. `Hatch` выдаёт в коллекцию отдельную копию, повтор возвращает сохранённый исход даже после эволюции, освобождения, сериализации или нового гнезда и не добавляет второго ребёнка. Store/catalog validation включает архив; прежний уже вылупившийся `Nest.Child` остаётся fallback для сохранений до этого исправления. Для более старого сохранения, где hatch marker сохранился, но гнездо уже перезаписано до появления архива, прежний исход восстановить невозможно; повтор не выдаёт изменённого текущего ребёнка.
- RED до production-правок: `task-11-fix1-red-core.log` (4 поведенческих failures), `task-11-fix1-red-app.log` (отсутствующий busy provider), `task-11-fix1-red-replay.log` (возврат evolved ребёнка), все ExitCode 1. Итоговые логи с точными `COMMAND`/`ExitCode`: `task-11-fix1-focused-core.log` 16/16, `task-11-fix1-focused-app.log` 56/56, `task-11-fix1-gate-filtered.log` Core 245/245 и App 404/404, все ExitCode 0. Исключён только ранее известный screenshot-env тест `FinishReviewEvidenceRendersPopulatedPokedexAndResetArea`; реальный save/MSI/OS и DPI-визуальная проверка не выполнялись.

## Re-review1 fix2: особенность на 10-м уровне (2026-10-13)

- `LevelProgression.AddXp(individual, xp, catalog, random?)` применяет тот же `TraitRules.Allowed(currentSpeciesId, isShiny, catalog)` при выдаче второго trait. Каталог передаётся из уже загруженного `ExpeditionService.catalog` и из `App._catalogPackage` через `ShopView`/`ShopService` для XP potion. Повторной загрузки или проверки ассетов на каждом XP tick нет; правила уровня, RNG draw, уже назначенные особенности и сохранённые результаты не переписываются.
- Реальные root 679 normal (нет ассета 680 normal) и 714 shiny (нет ассета 715 shiny) покрыты детерминированным поведенческим RED. Контрольный 714 normal с доступным 715 normal сохраняет право получить `fast-grower`. `task-11-fix2-red.log`: 2 failures / 1 pass, ExitCode 1 до production-правки. `task-11-fix2-focused-core.log`: Progression+Nursery 28/28; `task-11-fix2-focused-app.log`: Shop 16/16; `task-11-fix2-gate-filtered.log`: Core 248/248, App 404/404; все итоговые ExitCode 0 и точные `COMMAND`/`ExitCode` сохранены. В полном gate исключён только прежний screenshot-env тест.


## Independent scoped re-reviews

### Spec Compliance

- Issues remain in the scoped fix1: variant-specific fast-grower is excluded from the newborn first trait, but can still be awarded as the level-10 second trait. See Important.
- Important 1, variant-aware inheritance: PARTIALLY ADDRESSED. NurseryService.cs:59-64 previews separate normal/shiny pools and NurseryService.cs:109-115 draws the actual variant before choosing the inherited trait. TraitRules.cs:31-38 checks an obtainable immediate next variant. The new tests use valid family roots 679 and 714 (nursery_coreTests.cs:242-279). The earlier review's 654/651 examples were nonroots and are withdrawn.
- Important 2, parent lock: ADDRESSED. AppState.cs:127-136 guards readiness and actual evolution; CollectionViewModel.cs:187 checks a live busy-ID provider wired by App.xaml.cs:750. CollectionView.xaml.cs:547 disables release and :771 avoids a success callback on refusal. The busy set covers running/ready nest and expedition; tests cover both phases (nursery_coreTests.cs:309-349; CollectionViewModelTests.cs:128-144).
- Minor, frozen hatch replay: ADDRESSED. NurseryService.cs:154-172 archives separate child copies and returns the frozen result after later evolution, release, restart, and a new nest. SaveData.cs:26 and JsonSaveStore.cs:353-361 persist and validate the archive; nursery_coreTests.cs:281-306 exercises those paths. Older saves whose overwritten nest and marker predate this archive cannot reconstruct the original child; replay does not grant a new one.
- Cannot verify from this fix diff: T12 display and refusal UX, physical save/MSI/OS behavior, and T20 visual/DPI acceptance. The known screenshot-capture test remains excluded from the reported filtered solution gate.

### Strengths

- The fix uses the existing shared BusyIndividualIds at both UI and mutation boundaries, so a stale enabled control cannot release or evolve a locked parent. AppState.cs:127-136; CollectionViewModel.cs:187.
- Archive and owned child are independent copies; the replay path does not spend or grant again. NurseryService.cs:154-172.
- RED logs predate the fix; focused 16 Core and 56 App tests, and filtered 245 Core plus 404 App tests report ExitCode 0. task-11-fix1-red-core.log:48; task-11-fix1-red-app.log:5; task-11-fix1-red-replay.log:20; task-11-fix1-focused-core.log:11; task-11-fix1-focused-app.log:12; task-11-fix1-gate-filtered.log:20.

### Issues

#### Critical (Must Fix)

- None found in fix1.

#### Important (Should Fix)

- LevelProgression.cs:64 still calls TraitRules.Allowed(individual.CurrentSpeciesId), the old edge-only overload (TraitRules.cs:24-28), when awarding the level-10 second trait. A normal child rooted at 679, or shiny child rooted at 714, can receive fast-grower then even though its immediate next asset is unavailable. This leaves the original variant-eligibility requirement incomplete beyond birth. Route this award through the new variant-aware helper using individual.IsShiny and the catalog, and test the second-trait threshold for a blocked root. The birth-only tests at nursery_coreTests.cs:242-279 cannot catch this.

#### Minor (Nice to Have)

- None newly found.

### Assessment

**Task quality:** Needs fixes.

**Reasoning:** Parent locks and frozen hatch replay are sound in the scoped paths. The fix repairs first-trait inheritance but leaves the same forbidden trait available through the existing level-10 grant path.

### Named outside-diff check

- Checked only LevelProgression.AddXp and references to TraitRules.Allowed to determine whether the new variant-aware helper reached all awards. LevelProgression.cs:64 remains on the old overload. No suites were rerun; the pinned diff was read once, and cut-off mid-function hunks were checked in the named source sections.


### Spec Compliance

- ADDRESSED: The remaining Important from rereview1 is fixed. LevelProgression.cs:53-69 now requires a loaded CatalogPackage and uses CurrentSpeciesId plus IsShiny in TraitRules.Allowed when awarding the level-10 second trait. Root 679 normal and root 714 shiny cannot receive fast-grower when their immediate next variant is unavailable; the available 714 normal control can still receive it (progressionTests.cs:37-69).
- Both production XP award paths pass their existing catalog: expedition claim at ExpeditionService.cs:216 and XP potion through ShopView.xaml.cs:142-163, ShopService.cs:87-101, wired from App.xaml.cs:756-758. Repository call-site search found no remaining old AddXp invocation.
- Cannot verify from this fix diff: T12 screen behavior, real user saves, MSI/OS behavior, and T20 visual/DPI acceptance. The known screenshot-capture test remains excluded from the reported solution gate. Previously accepted parent-lock, hatch-archive, and birth-trait fixes were not reopened in this scoped review.

### Strengths

- The rule is applied at the award boundary and uses the actual current species and stored shiny flag. LevelProgression.cs:64-66.
- The fix passes the already loaded CatalogPackage into both mutation paths; it does not reload assets on each XP award. ExpeditionService.cs:216; ShopView.xaml.cs:142-163.
- Behavioral RED recorded two failures and one allowed-normal control pass before production changes. Focused Core 28/28, Shop 16/16, and filtered full gate Core 248/248 plus App 404/404 all show ExitCode 0. task-11-fix2-red.log:30-31; task-11-fix2-focused-core.log:10-11; task-11-fix2-focused-app.log:11-12; task-11-fix2-gate-filtered.log:17-20.

### Issues

#### Critical (Must Fix)

- None found in fix2.

#### Important (Should Fix)

- None found in fix2.

#### Minor (Nice to Have)

- None material in this scoped diff.

### Assessment

**Task quality:** Approved for this scoped fix2 gate.

**Reasoning:** The last identified variant-eligibility gap is closed at the second-trait award point, and both real XP callers supply the catalog. Existing tests and the logged filtered gate support the change; tests were not rerun in this read-only review.

### Named outside-diff check

- Searched AddXp, TryUseXpPotion, and ShopView call sites to verify the signature change reached all production callers. Did not review the separate T08 encounter-phase correction.
