# Промпты для Астры — пакеты генерации

Статус: 23.10.2026. Астра только генерирует картинки (D-01). Claude проверяет результат по критериям ниже и сам встраивает в приложение.

## Что мы делаем (прочитай перед работой)

PokéCorner — уютное приложение-компаньон для Windows. Маленький покемон живёт в углу рабочего стола поверх окон, а в окне приложения тренер ухаживает за ним, собирает коллекцию (до сотен питомцев), отправляет команду в экспедиции, выводит потомство в питомнике и покупает мелочи в магазине. Спрайты покемонов берутся из готового каталога и в картинки не рисуются.

Сейчас приложение переодевается в стиль «Ночной дневник тренера»: тёмный чернильно-сливовый интерфейс, мягкий лавандовый текст, тёплые абрикосовые акценты, ночные сцены с глубиной, на полях — рукописные заметки и зарисовки тренера. Ощущение — вечер, уют, тихое приключение. Ночь и уют относятся к оболочке; отдельные места могут быть суровыми или тревожными (вулкан, руины).

Твоя роль — только картинки: сцены, фоны, иконки. Интерфейс, текст и кнопки рисует приложение поверх, поэтому в картинках не должно быть ни текста, ни элементов интерфейса, ни покемонов. Каждая картинка будет обрезаться под разные размеры окна (от 1200×660 до развёрнутого FullHD), поэтому важное держи там, где указано в задании. Claude проверит каждую картинку по критериям и, если что-то не так, пришлёт исправленный промпт.

## Куда сохранять

Все результаты кладём в одну папку-приёмник, по подпапке на пакет:

```
C:\Users\Georgy\.codex\worktrees\night-diary-pilot\PokeTokenBar-Windows\docs\design-v3\night-diary\astra-inbox\
```

Рядом с каждой картинкой сохранить текстовый файл с тем же именем и расширением `.prompt.txt`: точный итоговый промпт и дата. Варианты одной картинки называть `-v1`, `-v2` и т.д. Ничего не класть в `src/`: в приложение встраивает Claude после приёмки.

## Общий стиль (вставлять в начало каждого промпта)

```
Night Diary style for a cozy desktop Pokémon companion app. High-quality hand-placed pixel painting with deliberate clusters, no blurry upscaling, no dithering noise, crisp 1px pixel edges. Night lighting with depth in three planes (close foreground, middle, far). Palette anchored on deep ink-blue and plum darks (#101426, #16192B, #23253C), soft lavender light (#C4ABF0) and warm apricot accents (#FFB574) where the scene has a light source. No text, no letters, no UI, no buttons, no frames, no Pokémon, no people, no trophies. Style reference: the attached grove-environment.png.
```

Прикладывать как референс стиля: `src\PokeCorner.App\Assets\NightDiary\grove-environment.png` (принятая сцена рощи).

---

## Пакет 1 — семь сцен экспедиций (обязательно, первым)

Где используются: шапка экрана экспедиций, сцена тянется на всю ширину окна. На минимальном окне 1200×660 видна полоса высотой 150–320 px по центру, на развёрнутом FullHD — до 1784×560. Значит всё важное — в центральной горизонтальной полосе, края и верх/низ могут обрезаться. Левая треть кадра сверху — место под заголовок места (засечками, светлым), там не должно быть ярких деталей.

Технические требования для всех семи:
- PNG, 2400×800 px, без прозрачности.
- Сохранить в `astra-inbox\scenes\` под именами: `lake.png`, `coast.png`, `mountains.png`, `desert.png`, `volcano.png`, `snow.png`, `ruins.png`.

Промпты (после общего стиля):

**lake.png**
```
Pixel-art view from the waterline INSIDE a sheltered freshwater lakeshore. Close reeds and rounded wet pebbles frame the lower corners, calm water fills the middle, a continuous opposite shore visibly closes the basin. Narrow silver moon reflection, small gentle ripples, still cool blue air. Low intimate framing with foreground bank underfoot. No open ocean horizon, breaking surf, sea foam, towering cliffs or forest clearing composition. Wide 3:1 frame, key elements in the central horizontal band, upper-left third calm and dark.
```

**coast.png**
```
Pixel-art view from INSIDE an exposed ocean shoreline, standing on a wet low rock shelf beside a breaking wave. Close foamy wash runs across dark stone and scattered shells, large rolling swells beyond, open uninterrupted sea horizon and salt spray in wind. Broad horizontal motion, cold steel-blue light with pale turquoise foam. No reeds, still pond, enclosing opposite bank, freshwater lilies or sheltered forest lake. Wide 3:1 frame, key elements in the central horizontal band, upper-left third calm and dark.
```

**mountains.png**
```
Pixel-art scene from INSIDE a rugged mountain traverse at boot height. A close angular stratified rock wall dominates one side, rough scree and fractured stone ledge underfoot, steep exposed drop on the other side. Strong diagonal geology, wind-blown dust, dry slate and muted violet moonlight. Spatial depth through the nearby ledge and chasm, not a centered distant peak. No snow, ice, lava, trees framing a vista or postcard mountain panorama. Wide 3:1 frame, key elements in the central horizontal band, upper-left third calm and dark.
```

**desert.png**
```
Pixel-art view from INSIDE a wind-carved desert dune hollow. Close fine ripple patterns underfoot, a high sweeping crescent sand ridge rises beside the viewer and curves across the frame, wind lifts a thin veil from its crest. Large uncluttered curved masses, copper-lilac night sand and deep cool shadows. No oasis, water, ruins, forest, mountain wall or cactus border. Not an aerial dune panorama. Wide 3:1 frame, key elements in the central horizontal band, upper-left third calm and dark.
```

**volcano.png**
```
Pixel-art environment from INSIDE the barren volcanic foothills, standing on close fractured black basalt. Branching molten lava rivers pass directly before us between jagged crust plates, glowing fissures illuminate the underside of nearby rocks, ash hangs in scorching air with restrained heat shimmer. Harsh orange-red light against soot-black and deep plum, asymmetric low viewpoint, tangible hostile ground. No distant volcano postcard, centered cone, vegetation, flowers, lake, blue water, snow or cozy campsite. Wide 3:1 frame, key elements in the central horizontal band, upper-left third calm and dark.
```

**snow.png**
```
Pixel-art scene from INSIDE a deep snowbound mountain pass, close shoulder-high wind-packed snowbanks and overhanging white cornices, partly buried rock and translucent blue ice at arm's reach. Powder drifts across the near path, blowing snow obscures the far opening. Large pale snow masses, compressed enclosed perspective, blue-white diffuse moonlight. No dry scree composition recolored white, distant scenic peaks, trees, lava or lake surface. Wide 3:1 frame, key elements in the central horizontal band, upper-left third calm and dark.
```

**ruins.png**
```
Pixel-art environment from INSIDE a dark ruined stone hall. Close cropped broken column bases, cracked flagstones underfoot, collapsed lintels overhead and deep black side passages. One thin cold shaft of moonlight enters through a broken ceiling; a very subtle anonymous pair of eyes glints far inside a shadowed doorway, no identifiable creature or species. Mysterious oppressive enclosed masonry, uneven architectural rhythm, charcoal and desaturated violet. No lush garden, dominant vegetation, water, distant temple facade, tourist panorama or bright welcoming courtyard. Wide 3:1 frame, key elements in the central horizontal band, upper-left third calm and dark.
```

Приёмка пакета 1 (Claude): все восемь сцен (с рощей) рядом без подписей узнаются и не повторяют композицию; запреты брифа выполнены (роща без воды, вулкан вплотную, руины без сада); кроп проверен на 1200×660 и FullHD; заголовок места читается поверх левой трети.

---

## Пакет 2 — ночная комната уголка (обязательно, D-07)

Где используется: экран «Уголок», комната питомца. Спрайт питомца и трофеи накладываются приложением поверх, поэтому геометрия должна совпасть с текущей комнатой точно.

- Референс геометрии (приложить): `docs\design-v2\art-production\last-attempt\final-assets\assets\room-master.png`.
- PNG, ровно 1448×582 px, без прозрачности.
- Сохранить: `astra-inbox\room\room-night.png`.

```
Night version of the attached room, keeping EXACTLY the same geometry, camera, perspective and furniture placement pixel for pixel: window on the left, armchair, small table, wall shelf with three pictures, low cabinet, dresser and round pet bed on the right, rug in the middle. Change only time of day: deep night outside the window with moon and stars, the room lit by one warm desk lamp or wall lamp (apricot light) and cool moonlight from the window, soft long shadows, cozy and calm. Keep the three trophy spots on the shelf top (around pixels x=664, 810, 956 at y=280) empty and clearly lit, they hold trophies drawn by the app. Keep the floor area in the middle clear for the pet. No characters, no Pokémon, no text.
```

Приёмка: наложение на старую комнату совпадает по мебели и опорам трофеев; пол в центре свободен; ночь, но питомец будет читаться (не слишком темно).

---

## Пакет 3 — иконки монеты, ухода, лакомства (желательно, D-09)

Где используются: шапка (монеты), магазин, кольца питомца на рабочем столе (нейтральный фон любых обоев), кнопки. Рисуются мелко: 20–32 px, поэтому силуэт должен читаться при 24 px.

- PNG с прозрачным фоном, 512×512 px, объект занимает ~80% кадра, без тени на фон.
- Сохранить в `astra-inbox\icons\`: `coin.png`, `care.png`, `treat.png`.

**coin.png**
```
Single game coin icon, front view, pixel art with chunky readable shapes, warm apricot-gold metal (#FFB574 family) with a simple embossed star or paw mark, one small highlight. Must read clearly at 24x24 px on both dark and light backgrounds: thin dark outline. Transparent background, centered, no text.
```

**care.png**
```
Single icon for "care" of a pet: a small heart cradled by an open hand or paw, pixel art with chunky readable shapes, soft lavender and warm pink tones with a thin dark outline, one small highlight. Must read clearly at 24x24 px on dark and light backgrounds. Transparent background, centered, no text.
```

**treat.png**
```
Single icon of a pet treat: a small bone-shaped biscuit or round cookie with a paw print, pixel art with chunky readable shapes, warm biscuit brown and apricot tones with a thin dark outline, one small highlight. Must read clearly at 24x24 px on dark and light backgrounds. Transparent background, centered, no text.
```

Приёмка: читаются на 24 px на тёмном и светлом фоне; одна стилистика у всех трёх; не спорят с существующими иконками ягод и предметов магазина.

---

## Пакет 4 — ночной питомник, три состояния (ок Georgy 23.10)

Где используется: шапка экрана «Питомник». Сейчас там дневная оранжерея (`docs\design-v2\ui-system\assets\nursery.png`, `nursery-waiting.png`, `nursery-ready.png`) — на ночном экране она чужеродна. Нужна ночная версия той же композиции в трёх состояниях; приложение переключает их по ходу ожидания.

- Референсы композиции (приложить): три текущих файла по путям выше.
- PNG, 2048×768 px, без прозрачности; первые две картинки совпадают по камере и предметам, отличается только содержимое гнезда; третья — ближний план того же гнезда.
- Главное — гнездо — в центре кадра; края могут обрезаться.
- Сохранить в `astra-inbox\nursery\`: `nursery-night.png`, `nursery-night-waiting.png`, `nursery-night-ready.png`.

Общая часть для всех трёх (после общего стиля):
```
Night version of the attached greenhouse nursery, keeping the same composition and camera: wooden potting bench across the frame, woven straw nest in the center of the bench, watering can and clay pots on the left, flower pots and a brass lantern on the right, large mullioned windows behind. It is late night: outside the windows a dark indigo sky with stars and a soft moon over distant hills, no daylight, no blue sky. The room is lit by the warm lantern (apricot glow) and cool moonlight through the windows, plants in deep shadow with a few lavender highlights. Calm, cozy, quiet. Keep the nest area clearly lit and readable.
```

Различия:
- **nursery-night.png** — `The nest is empty.`
- **nursery-night-waiting.png** — `A large pale speckled egg rests in the nest, softly lit by the lantern.`
- **nursery-night-ready.png** — `Closer framing on the nest; the same speckled egg has a fresh crack with a faint warm glow leaking from it and a few tiny sparkles around. Nothing hatching yet, no creature visible.`

Приёмка: первые две картинки совпадают по камере (наложение без сдвига мебели), ночь читается, гнездо в центре светлое, никакого существа и неизвестного покемона не видно.

## Позже

Зарисовки на полях (D-15): компас, лупа, блокнот, фонарь, следы, покебол — одноцветные линейные рисунки на прозрачном фоне. Сейчас стоят векторные заглушки; заказывать после того, как экраны покедекса и магазина устоятся, чтобы не рисовать под меняющиеся места.

---

## Приёмка 23.10 и переделки

Принято и встроено: семь сцен (снег — `snow-v2`), ночной питомник (`nursery-night`, `-waiting`, `-ready`), монета. Размер сцен 2172×724 вместо 2400×800 принят: пропорция 3:1 та же, а растягивать сгенерированную картинку бессмысленно.

Не принято:
- **care.png** — на 24 px сердце почти не видно: мелкий объект, много пустого места и искры. Переделать (промпт ниже).
- **room-night.png** — по композиции хороша, но не совпадает со старой комнатой по геометрии (на неё ставятся трофеи и передний край полки). Перегенерацию не заказываем: Claude сам перенесёт опоры трофеев и вырежет передний край полки из новой комнаты.
- **treat.png** — принят, встраивается вместе с иконкой ухода.

### Переделка: care.png
Сохранить в `astra-inbox\icons\care-v2.png`, PNG 512×512 с прозрачностью.
```
Single bold icon for pet "care": one large chunky pink-red heart cradled from below by an open lavender hand, the heart fills about 60% of the frame and the whole icon fills 85% of the canvas. Pixel art with big readable shapes and a thick dark outline, one small highlight on the heart. No sparkles, no stars, no extra particles, no thin details. Must read instantly at 24x24 px on dark and light backgrounds. Transparent background, centered, no text.
```
Приёмка: на 24 px сердце занимает большую часть значка, читается на тёмном и светлом фоне.

## Приёмка 26.10

- **care-v2.png** — принят: на 24 px сердце занимает значок и читается на обоих фонах. Встроен вместе с `treat.png` (96 px в `ui-system/assets/`): кнопки «Погладить» и «Покормить», кольцо лакомства, заголовки ухода и лакомств.
- **room-night.png** — встроен масштабированием до 1448×582: опоры трофеев и передний край полки совпали со старыми координатами, трофеи стоят на полке.

Открытых заказов у Астры нет.

## Заказ 29.10 — care-v3

Georgy 27.10: ладонь под сердцем выглядит странно. Убираем руку совсем. Уход — это «побыть вместе», поэтому два сердца: большое тёплое и маленькое лавандовое, прижатое к нему. Лапку не берём: она уже есть на лакомстве, значки спутались бы.

Сохранить в `astra-inbox\icons\care-v3.png`, PNG 512×512 с прозрачностью. Приложить `care-v2.png` как референс пиксельной техники и обводки, не композиции.
```
Night Diary style for a cozy desktop Pokémon companion app. Hand-placed pixel art icon with deliberate clusters, crisp 1px pixel edges, no blurry upscaling, no dithering noise. Palette: rosy red-pink (#F27A93 family), soft lavender (#C4ABF0), deep plum outline (#2A1F3D), one warm apricot highlight (#FFB574).

Single bold icon for pet "care" meaning "time together": one large chunky rosy-pink heart, and a smaller lavender heart tucked against its lower left side, overlapping it slightly, as if leaning on it. The large heart is about 70% of canvas width, the small heart about 35%. Both hearts share one thick deep-plum outline; one small apricot highlight on the large heart only. No hands, no paws, no arms, no faces, no sparkles, no stars, no particles, no text. Simple silhouette that reads instantly at 24x24 px on dark (#101426) and light backgrounds. The pair fills about 85% of the canvas, centered, genuinely transparent background. Exactly 512 x 512 pixels, PNG.
```
Приёмка: на 24 px читаются два сердца без руки; маленькое не сливается с большим (обводка видна на тёмном и светлом фоне); не путается со значком лакомства на соседней кнопке «Покормить».

## Приёмка 29.10

- **care-v3.png** — принят: два сердца без руки читаются на 24, 20 и 18 px на тёмном и светлом фоне и не путаются с косточкой лакомства. Заменил `ui-system/assets/care.png` (96 px): кнопка «Погладить», заголовки ухода в уголке и на странице питомца.

Открытых заказов у Астры нет.
