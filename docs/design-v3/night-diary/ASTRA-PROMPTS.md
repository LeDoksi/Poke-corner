# Промпты для Астры — пакеты генерации

Статус: 23.10.2026. Астра только генерирует картинки (D-01). Claude проверяет результат по критериям ниже и сам встраивает в приложение.

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

## Пакет 4 — ночной питомник (НЕ запускать до ок Georgy)

Найдено при сквозной проверке: картинка питомника (`nursery.png`, `nursery-waiting.png`, `nursery-ready.png`, 2048×768) — дневная оранжерея, на ночном экране выглядит так же чужеродно, как дневная комната уголка. В решениях её пока нет. Если Georgy согласится — три ночных состояния той же композиции (пустое гнездо / яйцо ждёт / яйцо готово), PNG 2048×768, в `astra-inbox\nursery\`. Промпты подготовлю после решения.

## Позже

Зарисовки на полях (D-15): компас, лупа, блокнот, фонарь, следы, покебол — одноцветные линейные рисунки на прозрачном фоне. Сейчас стоят векторные заглушки; заказывать после того, как экраны покедекса и магазина устоятся, чтобы не рисовать под меняющиеся места.
