# R2 revision2 — проверка полноты проекта

**2026-10-21: R2 revision2 визуально принят пользователем.** [Запись принятия](ACCEPTANCE.md). Следующий R3/POKE-68 ещё не начат; runtime-ограничения сохранены.


21 октября2026. Область: предложенные компоновки/переходы экспедиций, документы и растры. Подробный пилот пользователь принял 2026-10-21; см. ACCEPTANCE.md. Это не WPF/runtime PASS. Основной агент визуально просмотрел изображения; технический проход сопоставляет IDs с конкретными регионами и переходами STATE-COVERAGE/SPEC.

## Полная матрица IDs

Листы01/02/03/04/05/06/07/08 означают одноимённые файлы `*-v2.png`. Детальные листы не являются новыми полными окнами: регионы base/compact и управление точно заданы в STATE-COVERAGE.md. Все21 строка исходной матрицы SPEC сопоставлены ниже; combined cancel-pending/failure раскрыт двумя деталями.

| ID SPEC | Лист/регион | Переход/документальная опора |
|---|---|---|
| preparation-empty | 05 empty, base01/compact02 | Выбрать → picker; старт disabled |
| preparation-partial | 05 partial, base01/02 | Отправить1–2 → start-pending; без обещания extras |
| preparation-selected | 01/02 team/footer | Изменить → draft; отправить → pending |
| picker-draft | 03, полное compact06 slots/list/footer | Готово1–3 → preparation;0 disabled; отмена discard |
| picker-empty-search | 07 empty list | Сбросить в list/изменить фильтры → список, slots сохраняются; empty pagination hidden |
| picker-unavailable | 07 четыре карточки причин | Disabled exact GUID; фильтр доступности не меняет busy |
| picker-invalidated | 07 inline + slots | All-invalidated пример; partial сохраняет валидных; повторная валидация при apply |
| start-pending | 05 pending footer | Await existing commit, без нового request ID |
| start-failed | 05 error/footer | Retry по актуальному snapshot/request; детали ошибки доступны |
| running | 04 running content/action | Динамические ETA/progress, cancel-confirm |
| cancel-confirm | 04 confirmation | Safe/Escape → running; danger → clock/ID recheck |
| cancel-raced-ready | 08 ready/message | Закрыть confirm → ready, cancel hidden |
| cancel-pending/failure | 08 confirmation action-row/error | Pending safe/danger disabled; failure safe/explicit red Retry с повторной проверкой |
| ready | 04 ready | Claim → pending; busy до commit |
| claim-pending | 08 ready footer | CTA disabled; snapshot claimed → result |
| claim-failed | 08 ready error/footer | Повторить получение; stable operation; reconciliation |
| result | 04 result receipt/footer | Продолжить → preparation |
| result-with-encounter | 08 optional result block | Resolved normal Bulbasaur как known example → Catch с тем же ID, без reroll/выдачи до Catch commit |
| result-without-encounter | 04/08 result optional block hidden | Продолжить, без пустой ошибки |
| legacy-result | 08 receipt fallback region | Нет придуманных сумм; optional encounter независим |
| return-after-close | STATE-COVERAGE полная snapshot table; базы01/04/08 | In-flight/failed/committed start/cancel/claim, clock ready, receipt/no receipt, resolved/opened/no encounter, Catch return, restart и unavailable snapshot |

В SPEC фактически21 ID-строка (включая combined cancel-pending/failure);22 получается после разделения pending/failure. Эта формулировка фиксирует точный способ подсчёта, а не добавляет экран.

## Что покрыто

Исходные expedition / expedition-selected / expedition-team / expedition-running / expedition-ready / expedition-result / confirm-cancel покрыты указанными листами и вариантами. Encounter как отдельный Catch экран остаётся R9; R2 покрывает известный optional block и точный переход. Восемь route names/types,1–3 участника,3 сложности/4 длительности и гарантированные3Равст/12coins/150XP у выбранной тройки сверены TECHNICAL-CONTRACT по исходникам, не новым прогоном.

Новые копии деталей empty/retry/legacy — проектные тексты, не заявление, что старый WPF уже показывает эти labels. Блокировки, доступность, commit/retry/reconciliation остаются прежними контрактами. Недоступная форма не обозначает неизвестный вид; анонимные глаза арта не раскрывают вид. Partial invalidation не очищает валидные GUID. Неопределённый исход записи не объявляется ни успехом, ни отказом без reconciliation.

## Проверяемые артефакты

- JSON manifest/revision2/coverage/inventory разбираются; manifest current9 assets, history revision1 original6 с точными prompts сохранена.
-15 PNG имеют корректную PNG signature и CRC chunk; source/file bytes совпадают, исходники не удалялись.
- README/SPEC/STATE-COVERAGE/gallery relative refs указывают на существующие файлы; все9 текущих изображений присутствуют в HTML и README.
- UTF-8 документы декодируются, русские строки сохранены; S→R синхронизация без удаления с byte comparison.

## Ограничения

Browser rendering **not verified**: HTTP preview основного агента завершился timeout. HTML проверен как автономный статический артефакт по ссылкам/структуре, не по браузерному снимку. Приложение/WPF и тесты не запускались; DPI/контраст/клавиатура/звук/плавность/реальная прокрутка остаются R3. Пользовательское визуальное принятие ревизии2 зафиксировано 2026-10-21 в ACCEPTANCE.md; R3 ещё не начат.

Лист04 содержит обобщённый optional encounter; лист08 с известным Bulbasaur имеет приоритет для этого result-варианта. R2 принят пользователем 2026-10-21; закрытие Kanban выполняет основной агент. R3 ещё не начат.
