# Приёмка T15 / POKE-44 — 2026-10-14

Независимое spec/quality review принято для code `faabf74`. Ниже исходный отчёт реализации и доработок; старые промежуточные counts сохранены как история.

# T15 — передача одного экземпляра в новый дом

## Поставка

- `RehomingService.Quote(Guid, Guid? replacementId = null)` читает `rehome` из загруженного канонического пакета `balance.json` и уровень из `LevelProgression`. Выплата 6/12/24/40 + floor(level/10) + 5 shiny, максимум 49. Quote не меняет сохранение.
- `Transfer(Guid, Guid operationId, Guid? replacementId = null)` отказывает для последнего, избранного, занятого running/ready походом или гнездом, а также для активного без явно указанной доступной замены. Удаляет только указанный GUID, начисляет монеты и сохраняет входо-зависимый результат в предложенном снимке. Повтор того же действия возвращает прежнюю выплату без второго начисления; повтор ID с другими входами отказывается.
- В карточке конкретного экземпляра старое «Отпустить» заменено на «Найти новый дом» с суммой/причиной защиты и подтверждением имени и необратимости. Активному сначала нужно выбрать другого спутника существующей кнопкой «Выбрать» на его карточке. После ожидания проверяются GUID и поколение открытой карточки. Production-путь вызывает `AppState.CommitAsync` и публикует успех только после записи.
- Открытия, shiny-история, достижения, полученные подарки и архивы встреч/вылупления не удаляются. Для ранее открытой встречи строгая проверка сохранения разрешает отсутствие бывшего питомца только при валидном журнале подтверждённой передачи с совпадающим fingerprint; отсутствие записи или поддельный fingerprint отвергается.
- Удалены обходящий commit `CollectionViewModel.Release` и два устаревших теста его прямого вызова. Внутренний `PokemonCollection.Release` сохранён для `AppState.Hydrate` и старых низкоуровневых тестов.

## Проверки

- RED до первого production-изменения: `task-15-red.log`, точная команда/сырой stdout/stderr/`ExitCode: 1`, отсутствующий `RehomingService` (`CS0246`).
- Найденная позднее граница валидатора: первоначальное разрешение исторической opened-встречи было добавлено до отдельного behavioral RED; этого доказательства задним числом нет. После обнаружения недостаточно строгой связи журнала с fingerprint сохранён `task-15-journal-red.log`: `Assert.Throws` не получил `JsonException` для поддельной записи, `ExitCode: 1`. Затем fingerprint и replacement были связаны с durable результатом.
- Итоговые T15: `task-15-focused-direct.log`, **9/9**, `ExitCode: 0`. Сценарии включают active replacement, favorite/last/busy, максимум 49, один GUID/одну выплату, отмену подтверждения, отказ записи/повтор, opened-встречу после commit/load/replay, отрицательную валидацию и задержанную ошибку при переключении карточки.
- Core: `task-15-core-final.log`, **248/248**, `ExitCode: 0`. App: `task-15-app-final.log`, **444/444**, `ExitCode: 0`. В App исключён только прежний environment-specific `FinishReviewEvidenceRendersPopulatedPokedexAndResetArea`; прямые выводы `exec_command` и exit-коды сохранены в логах без перефразирования.
- Объединённый `.sln` и вложенный PowerShell-запуск App зависали без итогового кода; `task-15-final-gate.log` и `task-15-app-gate.log` являются прерванными попытками и не выдаются за успешные. Диагностический bounded прогон указал на мой тест подтверждения, выбирающий первое `ConfirmDialog` без проверки видимости/заголовка. Он исправлен; после исправления отдельные полные Core/App наборы прошли. В том же диагностическом прогоне был один сбой существующего timing-теста hero sprite; в завершившемся полном App наборе он прошёл. Конкретная внутренняя причина зависаний объединённого запуска не доказана.

## Самопроверка и передача

- Проверены целевой GUID и состояние на момент commit, блокировки на обеих фазах, отсутствие старого v2 release UI, сериализация/повтор журнала, сохранение исторической opened-встречи и отказ при подделке, предупреждение/клавиатурная доступность кнопки, сохранение T14 generation guard, T13 evolution path и выходной guard App.
- Полная матрица реального окна/150–200% DPI остаётся T20. Настоящие сохранения, MSI и состояние Windows не затрагивались.
- API следующей задачи: `new RehomingService(catalog, saveSnapshot).Quote(individualId, replacementId?)` для чтения; `Transfer(individualId, operationId, replacementId?)` только внутри предложенного снимка `AppState.CommitAsync`. `RehomeAction.Coins` — сохранённая выплата, включая повтор. `SaveData.RehomingResults` хранит подтверждённый GUID/замену/выплату для истории и replay.

## Fix round 1 — обратная проверка журнала

- Review `task-15-review.md` нашёл одностороннюю проверку: `CompletedOperations["rehome:<guid>"]` без `RehomingResults[guid]` проходил загрузку, но replay отказывал. Теперь валидатор проверяет и обратное направление. Ключи пространства `rehome` обязаны иметь канонический формат `rehome:<GUID-D>` и соответствующий результат; неверный GUID, пустой GUID, лишний суффикс или регистр префикса отвергаются. Ключи других операций не затронуты.
- Реальный RED до изменения production: `task-15-fix1-red.log`, `RehomeCompletionRequiresMatchingResultAndCanonicalKey` упал с `Assert.Throws() Failure: No exception was thrown`, `ExitCode: 1`. Тест также проверяет принятие стороннего `shop:` ключа. Лог содержит точную команду и фактический вывод.
- GREEN: `task-15-fix1-focused.log` — T15 **10/10**, `ExitCode: 0`; `task-15-fix1-app.log` — App **445/445**, `ExitCode: 0`; `task-15-fix1-core.log` — Core **248/248**, `ExitCode: 0`. App исключает только прежний environment-specific screenshot capture test. Все три лога записаны из фактически возвращённого `exec_command` stdout и exit-кода после прямого `rtk proxy dotnet test`.
- Самопроверка: новый предикат читает только ключи rehome, не переписывает `CompletedOperations` и не меняет правила evolution, nursery, expedition или shop. Прежняя проверка fingerprint и отсутствия переданного GUID среди владельцев остаётся на месте. Отложены только косметический DPI-прогон T20 и timing-оптимизация синхронного чтения правил без доказанной задержки.


## Independent review evidence: task-15-review.md

### Spec Compliance

- ❌ Issues found: `JsonSaveStore.cs:292-298` checks each rehome result against a completion fingerprint but accepts a `rehome:<operationId>` completion with no result. This violates the required inconsistent-journal refusal.
- ✅ One-GUID transfer, canonical payout, protected cases, explicit active replacement, input-bound replay, commit-before-publish, and opened encounter history are implemented (`RehomingService.cs:35-90`, `App.xaml.cs:731-747`, `JsonSaveStore.cs:330-338`).
- ⚠️ Cannot verify: real compact-window/150–200% DPI acceptance belongs to T20; real save, MSI, and OS state were not exercised.

### Strengths

- `RehomingService.cs:62-90` applies removal, coins, and replay result to one proposed snapshot; `App.xaml.cs:731-747` uses the existing commit barrier.
- `CollectionView.xaml.cs:1136-1213` confirms identity and payout, rechecks the quote, and gates post-await UI changes by GUID and card generation.
- `rehomeTests.cs:35-279` covers the key transaction, refusal, cancellation, history, and delayed UI cases.

### Issues

#### Important (Should Fix)

- `JsonSaveStore.cs:292-298`: Journal validation is one-way. A completion key `rehome:<valid-guid>` without `RehomingResults[guid]` passes load validation, while replay refuses it. Check both directions, including malformed rehome keys, and add a focused negative test. Preserve validation of other operation types.

#### Minor (Nice to Have)

- `CollectionView.xaml.cs:1136-1150` / `RehomingService.cs:20-32`: The once-per-second card refresh synchronously reloads balance and progression files. Cache existing immutable rules if a timing check shows a visible delay.
- `rehomeTests.cs:164-192`: The opened-encounter allowance lacked its own behavior-specific RED run. The implementer reports this honestly; the forged-fingerprint case has a captured failing RED.

### Assessment

**Task quality:** Needs fixes

**Reasoning:** The main transaction and UI path are sound, and direct logs confirm focused 9/9, Core 248/248, and App 444/444 with exit code 0. The missing reverse journal invariant blocks approval of the strict load boundary.

**Checks:** Read the provided diff and report. Focused unchanged-code checks covered level cap 40, running/ready expedition and nest busy IDs, AppState commit/publish, gift/hatch history validation, remaining low-level Release callers, and exact final logs. The combined solution run was interrupted and was not counted as passing. No tests were rerun.


## Independent review evidence: task-15-rereview1.md

### Finding Verdicts

- **Journal validation is one-way. A completion key rehome:<valid-guid> without RehomingResults[guid] passes load validation, while replay refuses it. Check both directions, including malformed rehome keys, and add a focused negative test. Preserve validation of other operation types.** — ADDRESSED. `JsonSaveStore.cs:300-304` now requires every case-insensitive `rehome`-prefixed completion key to be canonical `rehome:<GUID-D>` and have a matching result; the existing result-to-completion fingerprint check at `JsonSaveStore.cs:292-298` remains. `rehomeTests.cs:195-210` covers orphan, malformed, alternate-format, and uppercase keys, plus acceptance of an unrelated `shop:` key.

### New Breakage in the Fix Diff

None. The new predicate reads only completion keys in the rehome prefix, and existing production operation writers use distinct `evolution:`, `expedition:`, `nursery:`, and `shop:` namespaces.

### Out-of-Scope Observations

None. Previously noted synchronous rules-file reading and the opened-history RED gap are unchanged by this fix; T20 owns visual/DPI calibration.

### Verdict

**Fix round:** All findings addressed, no new Critical/Important breakage.

**Checks:** Reviewed provided fix diff once, appended fix report, and exact logs: RED `ExitCode: 1`; focused 10/10, Core 248/248, App 445/445 each `ExitCode: 0`. No tests rerun.
