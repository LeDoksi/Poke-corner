# Заказ Астре 29.10 — поимка, поля дневника, магазин, рейка

Все пути — внутри `docs\design-v3\night-diary\`. Рядом с каждой картинкой положить `.prompt.txt` с точным запросом и путём к исходнику, как у catch-ball.
Решения: D-24 (поимка) и D-25 (поля, магазин, рейка) в docs/project/DECISION-LOG.md.

**Порядок:** 1) ball-open и camp; 2) поля M02 и M07 — пробные, ждать приёмки; 3) остальные 12 полей с M02/M07 как референсом; 4) магазин партиями по 4; 5) рейка после приёмки M01–M05.

Всего картинок: 33.

## 1. Поимка · ball-open.png

- **Сохранить:** `astra-inbox\icons\ball-open.png`
- **Приложить:** astra-inbox\icons\catch-ball.png (это тот же шар, закрытый)
- **Где в приложении:** сцена поимки (60 px), обучение (130 px)

```
Night Diary style for a cozy desktop Pokémon companion app. Edit-match the attached catch-ball.png: this is the SAME ball, now OPEN. Keep its exact pixel technique, sculpted volume from discrete stepped color clusters, thick deep-plum outline (#2A1F3D), the same 10-degree tilt, the same palette (warm coral-red #E8636F upper shell with brick and plum shadows, cream-lavender #E9E1F7 lower half with lavender #C4ABF0 shadows) and exactly one compact apricot highlight (#FFB574) on the red shell.

The lower half stays exactly where it is in catch-ball.png: same pixels, same size, same position on the canvas, so the two images can be swapped in place without the ball jumping. The upper red shell is hinged at the back and lifted open about 60 degrees, its inside visible as a darker cream-lavender cavity with a plum rim. The round cream button stays on the front of the lower half. Nothing comes out of the ball: no light beam, no glow, no sparkles, no smoke, no Pokémon. No hands, no text, no shadow under it. Fits inside the canvas with at least 6% transparent margin on every side. Exactly 512 x 512 pixels, PNG, genuinely transparent background.
```

Приёмка: наложение на catch-ball с прозрачностью 50% — нижняя половина совпадает с точностью до 2–3 px; на 60 px читается открытым шаром; никакого свечения.

## 2. Поимка · camp.png

- **Сохранить:** `astra-inbox\scenes\camp.png`
- **Приложить:** pilot\grove-environment-v2.png (только техника и палитра)
- **Где в приложении:** поимка покебола за активность и подарок: шар лежит, открывается, результат

```
Night Diary style for a cozy desktop Pokémon companion app. High-quality hand-placed pixel painting with deliberate clusters, no blurry upscaling, no dithering noise, crisp 1px pixel edges. Night lighting with depth in three planes (close foreground, middle, far). Palette anchored on deep ink-blue and plum darks (#101426, #16192B, #23253C), soft lavender light (#C4ABF0) and warm apricot accents (#FFB574) where the scene has a light source. No text, no letters, no UI, no buttons, no frames, no Pokémon, no people, no trophies. Style reference: the attached grove-environment.png.

Pixel-art view from a crouching trainer's eye height at the edge of the trainer's own small night camp in an open forest clearing. Close foreground: short soft grass and a few small wildflowers along the bottom edge. The middle of the frame is a wide, open, flat patch of trampled grass and packed earth, softly lit: a clear landing spot kept free of objects and strong texture, wide and deep enough for a very large creature to stand on it as naturally as a small one. Left third: a small campfire in a ring of stones casting a warm apricot pool of light across the ground, a rolled sleeping mat and a backpack leaning on a low stump. Right third: a small canvas tent partly cropped by the frame, a lantern hanging from its pole. Background: a loose ring of dark trees receding into indigo, open night sky with stars and a moon above the clearing. Upper third calm and dark for a title. Key elements in the central horizontal band; the outer sixths may be cropped on narrow windows. It must not look like the forest grove route: no giant ancient trunks, no diagonal fallen log, no mushrooms, no swarm of fireflies. No Poké Ball, no Pokémon, no people, no glowing effects.

Output: one opaque PNG, exactly 2400 x 800 pixels. Attached grove-environment.png is a style reference only: use its pixel painting technique and palette, completely replace its forest composition with this camp clearing. No collage.
```

Приёмка: в центре стоят и маленький покемон, и крупный дракон — оба на земле, не наклейкой; не путается с рощей на общем листе маршрутов; кроп проверен при 1200×660 и FullHD.

## 3. Поля · M02 rail-pokedex.png

- **Сохранить:** `astra-inbox\margins\rail-pokedex.png`
- **Приложить:** ничего
- **Где в приложении:** рейка «Покедекс», знакомство шаг 2 (110 px)

```
Night Diary style margin sketch for a cozy desktop Pokémon companion app. A quick but confident hand-drawn pencil line sketch from a Pokémon trainer's travel diary. One single line colour, muted lavender-grey #9A8FBE, on a genuinely transparent background. Soft graphite feel: varied line weight, slightly imperfect hand-drawn strokes, a few short hatching strokes for shadow only. No fill, no second colour, no gradients, no glow, no paper texture, no frame, no border, no text, no letters, no numbers, no logo. Lines about 6 to 8 px thick at full size so the sketch stays visible when shown at 110 to 230 px on a dark #101426 background. The subject is centered, fills about 80% of the canvas, at least 8% transparent margin on every side, uncropped. No real Pokémon species, no Poké Ball, no people, no hands.

Subject: An open field notebook. The left page shows a filled-in rough silhouette of a small round creature with two pointed ears and a big question mark beside it; the right page has a few wavy unreadable lines and a tiny sketched leaf. The creature must not resemble any specific real Pokémon.

Output: exactly 512 x 512 pixels, PNG with alpha.
```

Приёмка: на 110 px на #101426 предмет узнаётся сразу; та же рука, что у остальных; ни букв, ни заливки, ни реальных покемонов; не похоже на кнопку.

## 4. Поля · M07 exp-tracks.png

- **Сохранить:** `astra-inbox\margins\exp-tracks.png`
- **Приложить:** ничего
- **Где в приложении:** экспедиции, левое поле (высота 230 px)

```
Night Diary style margin sketch for a cozy desktop Pokémon companion app. A quick but confident hand-drawn pencil line sketch from a Pokémon trainer's travel diary. One single line colour, muted lavender-grey #9A8FBE, on a genuinely transparent background. Soft graphite feel: varied line weight, slightly imperfect hand-drawn strokes, a few short hatching strokes for shadow only. No fill, no second colour, no gradients, no glow, no paper texture, no frame, no border, no text, no letters, no numbers, no logo. Lines about 6 to 8 px thick at full size so the sketch stays visible when shown at 110 to 230 px on a dark #101426 background. The subject is centered, fills about 80% of the canvas, at least 8% transparent margin on every side, uncropped. No real Pokémon species, no Poké Ball, no people, no hands.

Subject: Three parallel winding trails of footprints going from the bottom to the top: one of a hiking boot, one of a round four-toed paw, one of a three-toed bird-like foot. The trails cross once.

Output: exactly 512 x 768 pixels, PNG with alpha.
```

Приёмка: на 110 px на #101426 предмет узнаётся сразу; та же рука, что у остальных; ни букв, ни заливки, ни реальных покемонов; не похоже на кнопку.

## 5. Поля · M01 rail-corner.png

- **Сохранить:** `astra-inbox\margins\rail-corner.png`
- **Приложить:** принятые astra-inbox\margins\rail-pokedex.png и exp-tracks.png (референс штриха)
- **Где в приложении:** рейка «Уголок», знакомство шаги 1 и 5 (110 px)

```
Night Diary style margin sketch for a cozy desktop Pokémon companion app. A quick but confident hand-drawn pencil line sketch from a Pokémon trainer's travel diary. One single line colour, muted lavender-grey #9A8FBE, on a genuinely transparent background. Soft graphite feel: varied line weight, slightly imperfect hand-drawn strokes, a few short hatching strokes for shadow only. No fill, no second colour, no gradients, no glow, no paper texture, no frame, no border, no text, no letters, no numbers, no logo. Lines about 6 to 8 px thick at full size so the sketch stays visible when shown at 110 to 230 px on a dark #101426 background. The subject is centered, fills about 80% of the canvas, at least 8% transparent margin on every side, uncropped. No real Pokémon species, no Poké Ball, no people, no hands.

Subject: A round woven pet basket with a soft cushion, seen at three-quarter view, one small paw print on the cushion.

Output: exactly 512 x 512 pixels, PNG with alpha.
```

Приёмка: на 110 px на #101426 предмет узнаётся сразу; та же рука, что у остальных; ни букв, ни заливки, ни реальных покемонов; не похоже на кнопку.

## 6. Поля · M03 rail-expeditions.png

- **Сохранить:** `astra-inbox\margins\rail-expeditions.png`
- **Приложить:** принятые astra-inbox\margins\rail-pokedex.png и exp-tracks.png (референс штриха)
- **Где в приложении:** рейка «Экспедиции», знакомство шаг 3 (110 px)

```
Night Diary style margin sketch for a cozy desktop Pokémon companion app. A quick but confident hand-drawn pencil line sketch from a Pokémon trainer's travel diary. One single line colour, muted lavender-grey #9A8FBE, on a genuinely transparent background. Soft graphite feel: varied line weight, slightly imperfect hand-drawn strokes, a few short hatching strokes for shadow only. No fill, no second colour, no gradients, no glow, no paper texture, no frame, no border, no text, no letters, no numbers, no logo. Lines about 6 to 8 px thick at full size so the sketch stays visible when shown at 110 to 230 px on a dark #101426 background. The subject is centered, fills about 80% of the canvas, at least 8% transparent margin on every side, uncropped. No real Pokémon species, no Poké Ball, no people, no hands.

Subject: An old pocket compass with its hinged lid open, a scratched rim, the needle pointing up-right, a short loop of chain.

Output: exactly 512 x 512 pixels, PNG with alpha.
```

Приёмка: на 110 px на #101426 предмет узнаётся сразу; та же рука, что у остальных; ни букв, ни заливки, ни реальных покемонов; не похоже на кнопку.

## 7. Поля · M04 rail-nursery.png

- **Сохранить:** `astra-inbox\margins\rail-nursery.png`
- **Приложить:** принятые astra-inbox\margins\rail-pokedex.png и exp-tracks.png (референс штриха)
- **Где в приложении:** рейка «Питомник», знакомство шаг 4 (110 px)

```
Night Diary style margin sketch for a cozy desktop Pokémon companion app. A quick but confident hand-drawn pencil line sketch from a Pokémon trainer's travel diary. One single line colour, muted lavender-grey #9A8FBE, on a genuinely transparent background. Soft graphite feel: varied line weight, slightly imperfect hand-drawn strokes, a few short hatching strokes for shadow only. No fill, no second colour, no gradients, no glow, no paper texture, no frame, no border, no text, no letters, no numbers, no logo. Lines about 6 to 8 px thick at full size so the sketch stays visible when shown at 110 to 230 px on a dark #101426 background. The subject is centered, fills about 80% of the canvas, at least 8% transparent margin on every side, uncropped. No real Pokémon species, no Poké Ball, no people, no hands.

Subject: One large spotted egg standing upright, wrapped loosely in a knitted scarf with a visible knit pattern and a fringe end.

Output: exactly 512 x 512 pixels, PNG with alpha.
```

Приёмка: на 110 px на #101426 предмет узнаётся сразу; та же рука, что у остальных; ни букв, ни заливки, ни реальных покемонов; не похоже на кнопку.

## 8. Поля · M05 rail-shop.png

- **Сохранить:** `astra-inbox\margins\rail-shop.png`
- **Приложить:** принятые astra-inbox\margins\rail-pokedex.png и exp-tracks.png (референс штриха)
- **Где в приложении:** рейка «Магазин» (110 px)

```
Night Diary style margin sketch for a cozy desktop Pokémon companion app. A quick but confident hand-drawn pencil line sketch from a Pokémon trainer's travel diary. One single line colour, muted lavender-grey #9A8FBE, on a genuinely transparent background. Soft graphite feel: varied line weight, slightly imperfect hand-drawn strokes, a few short hatching strokes for shadow only. No fill, no second colour, no gradients, no glow, no paper texture, no frame, no border, no text, no letters, no numbers, no logo. Lines about 6 to 8 px thick at full size so the sketch stays visible when shown at 110 to 230 px on a dark #101426 background. The subject is centered, fills about 80% of the canvas, at least 8% transparent margin on every side, uncropped. No real Pokémon species, no Poké Ball, no people, no hands.

Subject: A small cloth drawstring pouch tied with twine, a blank paper tag on the string, two round berries peeking out of the top.

Output: exactly 512 x 512 pixels, PNG with alpha.
```

Приёмка: на 110 px на #101426 предмет узнаётся сразу; та же рука, что у остальных; ни букв, ни заливки, ни реальных покемонов; не похоже на кнопку.

## 9. Поля · M06 rail-album.png

- **Сохранить:** `astra-inbox\margins\rail-album.png`
- **Приложить:** принятые astra-inbox\margins\rail-pokedex.png и exp-tracks.png (референс штриха)
- **Где в приложении:** рейка в «Достижениях», знакомство шаг 6 (110 px)

```
Night Diary style margin sketch for a cozy desktop Pokémon companion app. A quick but confident hand-drawn pencil line sketch from a Pokémon trainer's travel diary. One single line colour, muted lavender-grey #9A8FBE, on a genuinely transparent background. Soft graphite feel: varied line weight, slightly imperfect hand-drawn strokes, a few short hatching strokes for shadow only. No fill, no second colour, no gradients, no glow, no paper texture, no frame, no border, no text, no letters, no numbers, no logo. Lines about 6 to 8 px thick at full size so the sketch stays visible when shown at 110 to 230 px on a dark #101426 background. The subject is centered, fills about 80% of the canvas, at least 8% transparent margin on every side, uncropped. No real Pokémon species, no Poké Ball, no people, no hands.

Subject: An open hinged badge case with eight small round slots in two rows; three slots hold simple geometric badges (a leaf, a drop, a flame), five are empty. The badges must not copy any real gym badge.

Output: exactly 512 x 512 pixels, PNG with alpha.
```

Приёмка: на 110 px на #101426 предмет узнаётся сразу; та же рука, что у остальных; ни букв, ни заливки, ни реальных покемонов; не похоже на кнопку.

## 10. Поля · M08 exp-lantern.png

- **Сохранить:** `astra-inbox\margins\exp-lantern.png`
- **Приложить:** принятые astra-inbox\margins\rail-pokedex.png и exp-tracks.png (референс штриха)
- **Где в приложении:** экспедиции, правое поле (высота 230 px)

```
Night Diary style margin sketch for a cozy desktop Pokémon companion app. A quick but confident hand-drawn pencil line sketch from a Pokémon trainer's travel diary. One single line colour, muted lavender-grey #9A8FBE, on a genuinely transparent background. Soft graphite feel: varied line weight, slightly imperfect hand-drawn strokes, a few short hatching strokes for shadow only. No fill, no second colour, no gradients, no glow, no paper texture, no frame, no border, no text, no letters, no numbers, no logo. Lines about 6 to 8 px thick at full size so the sketch stays visible when shown at 110 to 230 px on a dark #101426 background. The subject is centered, fills about 80% of the canvas, at least 8% transparent margin on every side, uncropped. No real Pokémon species, no Poké Ball, no people, no hands.

Subject: An old camping lantern with a wire handle, glass panes and a small flame drawn with a few strokes, hanging from a short strap.

Output: exactly 512 x 768 pixels, PNG with alpha.
```

Приёмка: на 110 px на #101426 предмет узнаётся сразу; та же рука, что у остальных; ни букв, ни заливки, ни реальных покемонов; не похоже на кнопку.

## 11. Поля · M09 exp-map.png

- **Сохранить:** `astra-inbox\margins\exp-map.png`
- **Приложить:** принятые astra-inbox\margins\rail-pokedex.png и exp-tracks.png (референс штриха)
- **Где в приложении:** шапка экспедиций (90 px)

```
Night Diary style margin sketch for a cozy desktop Pokémon companion app. A quick but confident hand-drawn pencil line sketch from a Pokémon trainer's travel diary. One single line colour, muted lavender-grey #9A8FBE, on a genuinely transparent background. Soft graphite feel: varied line weight, slightly imperfect hand-drawn strokes, a few short hatching strokes for shadow only. No fill, no second colour, no gradients, no glow, no paper texture, no frame, no border, no text, no letters, no numbers, no logo. Lines about 6 to 8 px thick at full size so the sketch stays visible when shown at 110 to 230 px on a dark #101426 background. The subject is centered, fills about 80% of the canvas, at least 8% transparent margin on every side, uncropped. No real Pokémon species, no Poké Ball, no people, no hands.

Subject: A folded paper map, slightly open, with a dotted route line, a small cross at the end and a tiny compass rose in one corner.

Output: exactly 512 x 512 pixels, PNG with alpha.
```

Приёмка: на 110 px на #101426 предмет узнаётся сразу; та же рука, что у остальных; ни букв, ни заливки, ни реальных покемонов; не похоже на кнопку.

## 12. Поля · M10 nursery-scheme.png

- **Сохранить:** `astra-inbox\margins\nursery-scheme.png`
- **Приложить:** принятые astra-inbox\margins\rail-pokedex.png и exp-tracks.png (референс штриха)
- **Где в приложении:** питомник, левое поле FullHD (180 px)

```
Night Diary style margin sketch for a cozy desktop Pokémon companion app. A quick but confident hand-drawn pencil line sketch from a Pokémon trainer's travel diary. One single line colour, muted lavender-grey #9A8FBE, on a genuinely transparent background. Soft graphite feel: varied line weight, slightly imperfect hand-drawn strokes, a few short hatching strokes for shadow only. No fill, no second colour, no gradients, no glow, no paper texture, no frame, no border, no text, no letters, no numbers, no logo. Lines about 6 to 8 px thick at full size so the sketch stays visible when shown at 110 to 230 px on a dark #101426 background. The subject is centered, fills about 80% of the canvas, at least 8% transparent margin on every side, uncropped. No real Pokémon species, no Poké Ball, no people, no hands.

Subject: A top-down diagram of a twig nest drawn as concentric rings, with four short arrows pointing at different layers, like a hand-drawn care diagram. Arrows only, no labels.

Output: exactly 512 x 512 pixels, PNG with alpha.
```

Приёмка: на 110 px на #101426 предмет узнаётся сразу; та же рука, что у остальных; ни букв, ни заливки, ни реальных покемонов; не похоже на кнопку.

## 13. Поля · M11 nursery-paws.png

- **Сохранить:** `astra-inbox\margins\nursery-paws.png`
- **Приложить:** принятые astra-inbox\margins\rail-pokedex.png и exp-tracks.png (референс штриха)
- **Где в приложении:** питомник, правое поле FullHD (180 px)

```
Night Diary style margin sketch for a cozy desktop Pokémon companion app. A quick but confident hand-drawn pencil line sketch from a Pokémon trainer's travel diary. One single line colour, muted lavender-grey #9A8FBE, on a genuinely transparent background. Soft graphite feel: varied line weight, slightly imperfect hand-drawn strokes, a few short hatching strokes for shadow only. No fill, no second colour, no gradients, no glow, no paper texture, no frame, no border, no text, no letters, no numbers, no logo. Lines about 6 to 8 px thick at full size so the sketch stays visible when shown at 110 to 230 px on a dark #101426 background. The subject is centered, fills about 80% of the canvas, at least 8% transparent margin on every side, uncropped. No real Pokémon species, no Poké Ball, no people, no hands.

Subject: Two different adult paw prints side by side (one round, one with long toes) and one tiny paw print below between them, with a small heart drawn above.

Output: exactly 512 x 512 pixels, PNG with alpha.
```

Приёмка: на 110 px на #101426 предмет узнаётся сразу; та же рука, что у остальных; ни букв, ни заливки, ни реальных покемонов; не похоже на кнопку.

## 14. Поля · M12 shop-list.png

- **Сохранить:** `astra-inbox\margins\shop-list.png`
- **Приложить:** принятые astra-inbox\margins\rail-pokedex.png и exp-tracks.png (референс штриха)
- **Где в приложении:** магазин, левое поле FullHD (высота 300 px)

```
Night Diary style margin sketch for a cozy desktop Pokémon companion app. A quick but confident hand-drawn pencil line sketch from a Pokémon trainer's travel diary. One single line colour, muted lavender-grey #9A8FBE, on a genuinely transparent background. Soft graphite feel: varied line weight, slightly imperfect hand-drawn strokes, a few short hatching strokes for shadow only. No fill, no second colour, no gradients, no glow, no paper texture, no frame, no border, no text, no letters, no numbers, no logo. Lines about 6 to 8 px thick at full size so the sketch stays visible when shown at 110 to 230 px on a dark #101426 background. The subject is centered, fills about 80% of the canvas, at least 8% transparent margin on every side, uncropped. No real Pokémon species, no Poké Ball, no people, no hands.

Subject: A torn-off note pinned at the top, with five wavy unreadable lines and a check mark before three of them, a short pencil lying across the bottom corner.

Output: exactly 512 x 768 pixels, PNG with alpha.
```

Приёмка: на 110 px на #101426 предмет узнаётся сразу; та же рука, что у остальных; ни букв, ни заливки, ни реальных покемонов; не похоже на кнопку.

## 15. Поля · M13 shop-berries.png

- **Сохранить:** `astra-inbox\margins\shop-berries.png`
- **Приложить:** принятые astra-inbox\margins\rail-pokedex.png и exp-tracks.png (референс штриха)
- **Где в приложении:** магазин, правое поле FullHD (высота 300 px)

```
Night Diary style margin sketch for a cozy desktop Pokémon companion app. A quick but confident hand-drawn pencil line sketch from a Pokémon trainer's travel diary. One single line colour, muted lavender-grey #9A8FBE, on a genuinely transparent background. Soft graphite feel: varied line weight, slightly imperfect hand-drawn strokes, a few short hatching strokes for shadow only. No fill, no second colour, no gradients, no glow, no paper texture, no frame, no border, no text, no letters, no numbers, no logo. Lines about 6 to 8 px thick at full size so the sketch stays visible when shown at 110 to 230 px on a dark #101426 background. The subject is centered, fills about 80% of the canvas, at least 8% transparent margin on every side, uncropped. No real Pokémon species, no Poké Ball, no people, no hands.

Subject: A small sprig with two round berries of different shapes, one with a pointed tip, and three leaves; a single fallen berry below.

Output: exactly 512 x 768 pixels, PNG with alpha.
```

Приёмка: на 110 px на #101426 предмет узнаётся сразу; та же рука, что у остальных; ни букв, ни заливки, ни реальных покемонов; не похоже на кнопку.

## 16. Поля · M14 hatch-shell.png

- **Сохранить:** `astra-inbox\margins\hatch-shell.png`
- **Приложить:** принятые astra-inbox\margins\rail-pokedex.png и exp-tracks.png (референс штриха)
- **Где в приложении:** питомник: «малыш готов» и результат (160 px)

```
Night Diary style margin sketch for a cozy desktop Pokémon companion app. A quick but confident hand-drawn pencil line sketch from a Pokémon trainer's travel diary. One single line colour, muted lavender-grey #9A8FBE, on a genuinely transparent background. Soft graphite feel: varied line weight, slightly imperfect hand-drawn strokes, a few short hatching strokes for shadow only. No fill, no second colour, no gradients, no glow, no paper texture, no frame, no border, no text, no letters, no numbers, no logo. Lines about 6 to 8 px thick at full size so the sketch stays visible when shown at 110 to 230 px on a dark #101426 background. The subject is centered, fills about 80% of the canvas, at least 8% transparent margin on every side, uncropped. No real Pokémon species, no Poké Ball, no people, no hands.

Subject: Two halves of a cracked spotted eggshell lying on their sides, a few small shell pieces around them.

Output: exactly 512 x 512 pixels, PNG with alpha.
```

Приёмка: на 110 px на #101426 предмет узнаётся сразу; та же рука, что у остальных; ни букв, ни заливки, ни реальных покемонов; не похоже на кнопку.

## 17. Магазин · treats.png

- **Сохранить:** `astra-inbox\shop\treats.png`
- **Приложить:** 1) astra-inbox\icons\care-v3.png  2) docs\design-v2\final\assets\shop\treats.png
- **Где в приложении:** магазин, «Помочь питомцу», питомник, награды экспедиций (40–48 px)

```
Night Diary style for a cozy desktop Pokémon companion app. Hand-placed pixel art item icon with deliberate clusters, crisp 1px pixel edges, no blurry upscaling, no dithering noise. Match the first attached image (care-v3.png) in pixel technique: sculpted volume built from discrete stepped color clusters, thick deep-plum outline (#2A1F3D), exactly one compact warm apricot highlight (#FFB574). The second attached image is the old version of the same item: keep its recognisable shape and main colours, drop its glossy white highlights, soft gradients and missing outline.

Subject: A small paper treat bag, top folded over, with the same bone-shaped biscuit printed on its front label as the treat icon, two biscuits peeking out.

Single item, centered, fills about 80% of the canvas with at least 8% transparent margin. Reads instantly at 40 px on dark #101426 and next to the care-v3 heart icon. No sparkles, no stars, no glow, no shadow under it, no text, no labels, no Pokémon. Exactly 512 x 512 pixels, PNG, genuinely transparent background.
```

Приёмка: рядом с care-v3, catch-ball, treat и монетой — один набор; ягоды узнаются по форме; пачка связана с косточкой.

## 18. Магазин · care-booster.png

- **Сохранить:** `astra-inbox\shop\care-booster.png`
- **Приложить:** 1) astra-inbox\icons\care-v3.png  2) docs\design-v2\final\assets\shop\care-booster.png
- **Где в приложении:** магазин, «Помочь питомцу», питомник, награды экспедиций (40–48 px)

```
Night Diary style for a cozy desktop Pokémon companion app. Hand-placed pixel art item icon with deliberate clusters, crisp 1px pixel edges, no blurry upscaling, no dithering noise. Match the first attached image (care-v3.png) in pixel technique: sculpted volume built from discrete stepped color clusters, thick deep-plum outline (#2A1F3D), exactly one compact warm apricot highlight (#FFB574). The second attached image is the old version of the same item: keep its recognisable shape and main colours, drop its glossy white highlights, soft gradients and missing outline.

Subject: A small corked glass bottle with a warm orange tonic and a rosy heart on its label.

Single item, centered, fills about 80% of the canvas with at least 8% transparent margin. Reads instantly at 40 px on dark #101426 and next to the care-v3 heart icon. No sparkles, no stars, no glow, no shadow under it, no text, no labels, no Pokémon. Exactly 512 x 512 pixels, PNG, genuinely transparent background.
```

Приёмка: рядом с care-v3, catch-ball, treat и монетой — один набор; ягоды узнаются по форме; пачка связана с косточкой.

## 19. Магазин · xp-potion.png

- **Сохранить:** `astra-inbox\shop\xp-potion.png`
- **Приложить:** 1) astra-inbox\icons\care-v3.png  2) docs\design-v2\final\assets\shop\xp-potion.png
- **Где в приложении:** магазин, «Помочь питомцу», питомник, награды экспедиций (40–48 px)

```
Night Diary style for a cozy desktop Pokémon companion app. Hand-placed pixel art item icon with deliberate clusters, crisp 1px pixel edges, no blurry upscaling, no dithering noise. Match the first attached image (care-v3.png) in pixel technique: sculpted volume built from discrete stepped color clusters, thick deep-plum outline (#2A1F3D), exactly one compact warm apricot highlight (#FFB574). The second attached image is the old version of the same item: keep its recognisable shape and main colours, drop its glossy white highlights, soft gradients and missing outline.

Subject: A round glass flask with a violet potion and two stacked upward chevrons on its label.

Single item, centered, fills about 80% of the canvas with at least 8% transparent margin. Reads instantly at 40 px on dark #101426 and next to the care-v3 heart icon. No sparkles, no stars, no glow, no shadow under it, no text, no labels, no Pokémon. Exactly 512 x 512 pixels, PNG, genuinely transparent background.
```

Приёмка: рядом с care-v3, catch-ball, treat и монетой — один набор; ягоды узнаются по форме; пачка связана с косточкой.

## 20. Магазин · lucky-charm.png

- **Сохранить:** `astra-inbox\shop\lucky-charm.png`
- **Приложить:** 1) astra-inbox\icons\care-v3.png  2) docs\design-v2\final\assets\shop\lucky-charm.png
- **Где в приложении:** магазин, «Помочь питомцу», питомник, награды экспедиций (40–48 px)

```
Night Diary style for a cozy desktop Pokémon companion app. Hand-placed pixel art item icon with deliberate clusters, crisp 1px pixel edges, no blurry upscaling, no dithering noise. Match the first attached image (care-v3.png) in pixel technique: sculpted volume built from discrete stepped color clusters, thick deep-plum outline (#2A1F3D), exactly one compact warm apricot highlight (#FFB574). The second attached image is the old version of the same item: keep its recognisable shape and main colours, drop its glossy white highlights, soft gradients and missing outline.

Subject: A golden pendant on a short cord holding a four-pointed star made of two colours, coral and lavender, to suggest an unusual colour form.

Single item, centered, fills about 80% of the canvas with at least 8% transparent margin. Reads instantly at 40 px on dark #101426 and next to the care-v3 heart icon. No sparkles, no stars, no glow, no shadow under it, no text, no labels, no Pokémon. Exactly 512 x 512 pixels, PNG, genuinely transparent background.
```

Приёмка: рядом с care-v3, catch-ball, treat и монетой — один набор; ягоды узнаются по форме; пачка связана с косточкой.

## 21. Магазин · evolution-stone.png

- **Сохранить:** `astra-inbox\shop\evolution-stone.png`
- **Приложить:** 1) astra-inbox\icons\care-v3.png  2) docs\design-v2\final\assets\shop\evolution-stone.png
- **Где в приложении:** магазин, «Помочь питомцу», питомник, награды экспедиций (40–48 px)

```
Night Diary style for a cozy desktop Pokémon companion app. Hand-placed pixel art item icon with deliberate clusters, crisp 1px pixel edges, no blurry upscaling, no dithering noise. Match the first attached image (care-v3.png) in pixel technique: sculpted volume built from discrete stepped color clusters, thick deep-plum outline (#2A1F3D), exactly one compact warm apricot highlight (#FFB574). The second attached image is the old version of the same item: keep its recognisable shape and main colours, drop its glossy white highlights, soft gradients and missing outline.

Subject: A faceted blue crystal stone with a lighter inner core.

Single item, centered, fills about 80% of the canvas with at least 8% transparent margin. Reads instantly at 40 px on dark #101426 and next to the care-v3 heart icon. No sparkles, no stars, no glow, no shadow under it, no text, no labels, no Pokémon. Exactly 512 x 512 pixels, PNG, genuinely transparent background.
```

Приёмка: рядом с care-v3, catch-ball, treat и монетой — один набор; ягоды узнаются по форме; пачка связана с косточкой.

## 22. Магазин · offspring-charm.png

- **Сохранить:** `astra-inbox\shop\offspring-charm.png`
- **Приложить:** 1) astra-inbox\icons\care-v3.png  2) docs\design-v2\final\assets\shop\offspring-charm.png
- **Где в приложении:** магазин, «Помочь питомцу», питомник, награды экспедиций (40–48 px)

```
Night Diary style for a cozy desktop Pokémon companion app. Hand-placed pixel art item icon with deliberate clusters, crisp 1px pixel edges, no blurry upscaling, no dithering noise. Match the first attached image (care-v3.png) in pixel technique: sculpted volume built from discrete stepped color clusters, thick deep-plum outline (#2A1F3D), exactly one compact warm apricot highlight (#FFB574). The second attached image is the old version of the same item: keep its recognisable shape and main colours, drop its glossy white highlights, soft gradients and missing outline.

Subject: A golden pendant framing a small spotted egg between two leaves.

Single item, centered, fills about 80% of the canvas with at least 8% transparent margin. Reads instantly at 40 px on dark #101426 and next to the care-v3 heart icon. No sparkles, no stars, no glow, no shadow under it, no text, no labels, no Pokémon. Exactly 512 x 512 pixels, PNG, genuinely transparent background.
```

Приёмка: рядом с care-v3, catch-ball, treat и монетой — один набор; ягоды узнаются по форме; пачка связана с косточкой.

## 23. Магазин · cheri.png

- **Сохранить:** `astra-inbox\shop\cheri.png`
- **Приложить:** 1) astra-inbox\icons\care-v3.png  2) docs\design-v2\final\assets\shop\cheri.png
- **Где в приложении:** магазин, «Помочь питомцу», питомник, награды экспедиций (40–48 px)

```
Night Diary style for a cozy desktop Pokémon companion app. Hand-placed pixel art item icon with deliberate clusters, crisp 1px pixel edges, no blurry upscaling, no dithering noise. Match the first attached image (care-v3.png) in pixel technique: sculpted volume built from discrete stepped color clusters, thick deep-plum outline (#2A1F3D), exactly one compact warm apricot highlight (#FFB574). The second attached image is the old version of the same item: keep its recognisable shape and main colours, drop its glossy white highlights, soft gradients and missing outline.

Subject: Cheri Berry: twin round red berries on a joined stem with one leaf.

Single item, centered, fills about 80% of the canvas with at least 8% transparent margin. Reads instantly at 40 px on dark #101426 and next to the care-v3 heart icon. No sparkles, no stars, no glow, no shadow under it, no text, no labels, no Pokémon. Exactly 512 x 512 pixels, PNG, genuinely transparent background.
```

Приёмка: рядом с care-v3, catch-ball, treat и монетой — один набор; ягоды узнаются по форме; пачка связана с косточкой.

## 24. Магазин · oran.png

- **Сохранить:** `astra-inbox\shop\oran.png`
- **Приложить:** 1) astra-inbox\icons\care-v3.png  2) docs\design-v2\final\assets\shop\oran.png
- **Где в приложении:** магазин, «Помочь питомцу», питомник, награды экспедиций (40–48 px)

```
Night Diary style for a cozy desktop Pokémon companion app. Hand-placed pixel art item icon with deliberate clusters, crisp 1px pixel edges, no blurry upscaling, no dithering noise. Match the first attached image (care-v3.png) in pixel technique: sculpted volume built from discrete stepped color clusters, thick deep-plum outline (#2A1F3D), exactly one compact warm apricot highlight (#FFB574). The second attached image is the old version of the same item: keep its recognisable shape and main colours, drop its glossy white highlights, soft gradients and missing outline.

Subject: Oran Berry: a round blue berry with a slightly bumpy skin and a green leafy crown.

Single item, centered, fills about 80% of the canvas with at least 8% transparent margin. Reads instantly at 40 px on dark #101426 and next to the care-v3 heart icon. No sparkles, no stars, no glow, no shadow under it, no text, no labels, no Pokémon. Exactly 512 x 512 pixels, PNG, genuinely transparent background.
```

Приёмка: рядом с care-v3, catch-ball, treat и монетой — один набор; ягоды узнаются по форме; пачка связана с косточкой.

## 25. Магазин · aspear.png

- **Сохранить:** `astra-inbox\shop\aspear.png`
- **Приложить:** 1) astra-inbox\icons\care-v3.png  2) docs\design-v2\final\assets\shop\aspear.png
- **Где в приложении:** магазин, «Помочь питомцу», питомник, награды экспедиций (40–48 px)

```
Night Diary style for a cozy desktop Pokémon companion app. Hand-placed pixel art item icon with deliberate clusters, crisp 1px pixel edges, no blurry upscaling, no dithering noise. Match the first attached image (care-v3.png) in pixel technique: sculpted volume built from discrete stepped color clusters, thick deep-plum outline (#2A1F3D), exactly one compact warm apricot highlight (#FFB574). The second attached image is the old version of the same item: keep its recognisable shape and main colours, drop its glossy white highlights, soft gradients and missing outline.

Subject: Aspear Berry: a plump yellow berry with a pitted skin and a short green crown.

Single item, centered, fills about 80% of the canvas with at least 8% transparent margin. Reads instantly at 40 px on dark #101426 and next to the care-v3 heart icon. No sparkles, no stars, no glow, no shadow under it, no text, no labels, no Pokémon. Exactly 512 x 512 pixels, PNG, genuinely transparent background.
```

Приёмка: рядом с care-v3, catch-ball, treat и монетой — один набор; ягоды узнаются по форме; пачка связана с косточкой.

## 26. Магазин · pecha.png

- **Сохранить:** `astra-inbox\shop\pecha.png`
- **Приложить:** 1) astra-inbox\icons\care-v3.png  2) docs\design-v2\final\assets\shop\pecha.png
- **Где в приложении:** магазин, «Помочь питомцу», питомник, награды экспедиций (40–48 px)

```
Night Diary style for a cozy desktop Pokémon companion app. Hand-placed pixel art item icon with deliberate clusters, crisp 1px pixel edges, no blurry upscaling, no dithering noise. Match the first attached image (care-v3.png) in pixel technique: sculpted volume built from discrete stepped color clusters, thick deep-plum outline (#2A1F3D), exactly one compact warm apricot highlight (#FFB574). The second attached image is the old version of the same item: keep its recognisable shape and main colours, drop its glossy white highlights, soft gradients and missing outline.

Subject: Pecha Berry: a soft pink rounded berry with a pointed tip and one green leaf.

Single item, centered, fills about 80% of the canvas with at least 8% transparent margin. Reads instantly at 40 px on dark #101426 and next to the care-v3 heart icon. No sparkles, no stars, no glow, no shadow under it, no text, no labels, no Pokémon. Exactly 512 x 512 pixels, PNG, genuinely transparent background.
```

Приёмка: рядом с care-v3, catch-ball, treat и монетой — один набор; ягоды узнаются по форме; пачка связана с косточкой.

## 27. Магазин · chesto.png

- **Сохранить:** `astra-inbox\shop\chesto.png`
- **Приложить:** 1) astra-inbox\icons\care-v3.png  2) docs\design-v2\final\assets\shop\chesto.png
- **Где в приложении:** магазин, «Помочь питомцу», питомник, награды экспедиций (40–48 px)

```
Night Diary style for a cozy desktop Pokémon companion app. Hand-placed pixel art item icon with deliberate clusters, crisp 1px pixel edges, no blurry upscaling, no dithering noise. Match the first attached image (care-v3.png) in pixel technique: sculpted volume built from discrete stepped color clusters, thick deep-plum outline (#2A1F3D), exactly one compact warm apricot highlight (#FFB574). The second attached image is the old version of the same item: keep its recognisable shape and main colours, drop its glossy white highlights, soft gradients and missing outline.

Subject: Chesto Berry: a hard purple berry with a pointed top sitting in a pale cream husk.

Single item, centered, fills about 80% of the canvas with at least 8% transparent margin. Reads instantly at 40 px on dark #101426 and next to the care-v3 heart icon. No sparkles, no stars, no glow, no shadow under it, no text, no labels, no Pokémon. Exactly 512 x 512 pixels, PNG, genuinely transparent background.
```

Приёмка: рядом с care-v3, catch-ball, treat и монетой — один набор; ягоды узнаются по форме; пачка связана с косточкой.

## 28. Магазин · rawst.png

- **Сохранить:** `astra-inbox\shop\rawst.png`
- **Приложить:** 1) astra-inbox\icons\care-v3.png  2) docs\design-v2\final\assets\shop\rawst.png
- **Где в приложении:** магазин, «Помочь питомцу», питомник, награды экспедиций (40–48 px)

```
Night Diary style for a cozy desktop Pokémon companion app. Hand-placed pixel art item icon with deliberate clusters, crisp 1px pixel edges, no blurry upscaling, no dithering noise. Match the first attached image (care-v3.png) in pixel technique: sculpted volume built from discrete stepped color clusters, thick deep-plum outline (#2A1F3D), exactly one compact warm apricot highlight (#FFB574). The second attached image is the old version of the same item: keep its recognisable shape and main colours, drop its glossy white highlights, soft gradients and missing outline.

Subject: Rawst Berry: an elongated green leafy berry with frilled edges.

Single item, centered, fills about 80% of the canvas with at least 8% transparent margin. Reads instantly at 40 px on dark #101426 and next to the care-v3 heart icon. No sparkles, no stars, no glow, no shadow under it, no text, no labels, no Pokémon. Exactly 512 x 512 pixels, PNG, genuinely transparent background.
```

Приёмка: рядом с care-v3, catch-ball, treat и монетой — один набор; ягоды узнаются по форме; пачка связана с косточкой.

## 29. Рейка · corner.png

- **Сохранить:** `astra-inbox\rail\corner.png`
- **Приложить:** принятые astra-inbox\margins\rail-*.png (M01–M05)
- **Где в приложении:** значок раздела на рейке (24 px)

```
Night Diary style navigation icon for a cozy desktop Pokémon companion app, drawn like a pencil sketch from a Pokémon trainer's travel diary. Simple icon-like sketch: one continuous confident outline, no hatching, slightly imperfect hand-drawn line, lines about 18 px thick at full size so it stays clear at 24 px. One single line colour, muted lavender-grey #9A8FBE, on a genuinely transparent background. No fill, no second colour, no gradients, no glow, no frame, no text, no letters, no numbers. Centered, fills about 80% of the canvas with at least 8% transparent margin. Match the line character of the attached accepted margin sketches.

Subject: a small house outline with a round window and a paw print on the door.

Output: exactly 256 x 256 pixels, PNG with alpha.
```

Приёмка: на 24 px пять значков различаются без подписей; яйцо не похоже на каплю.

## 30. Рейка · pokedex.png

- **Сохранить:** `astra-inbox\rail\pokedex.png`
- **Приложить:** принятые astra-inbox\margins\rail-*.png (M01–M05)
- **Где в приложении:** значок раздела на рейке (24 px)

```
Night Diary style navigation icon for a cozy desktop Pokémon companion app, drawn like a pencil sketch from a Pokémon trainer's travel diary. Simple icon-like sketch: one continuous confident outline, no hatching, slightly imperfect hand-drawn line, lines about 18 px thick at full size so it stays clear at 24 px. One single line colour, muted lavender-grey #9A8FBE, on a genuinely transparent background. No fill, no second colour, no gradients, no glow, no frame, no text, no letters, no numbers. Centered, fills about 80% of the canvas with at least 8% transparent margin. Match the line character of the attached accepted margin sketches.

Subject: a closed field notebook with a strap and a small circle on the cover.

Output: exactly 256 x 256 pixels, PNG with alpha.
```

Приёмка: на 24 px пять значков различаются без подписей; яйцо не похоже на каплю.

## 31. Рейка · expeditions.png

- **Сохранить:** `astra-inbox\rail\expeditions.png`
- **Приложить:** принятые astra-inbox\margins\rail-*.png (M01–M05)
- **Где в приложении:** значок раздела на рейке (24 px)

```
Night Diary style navigation icon for a cozy desktop Pokémon companion app, drawn like a pencil sketch from a Pokémon trainer's travel diary. Simple icon-like sketch: one continuous confident outline, no hatching, slightly imperfect hand-drawn line, lines about 18 px thick at full size so it stays clear at 24 px. One single line colour, muted lavender-grey #9A8FBE, on a genuinely transparent background. No fill, no second colour, no gradients, no glow, no frame, no text, no letters, no numbers. Centered, fills about 80% of the canvas with at least 8% transparent margin. Match the line character of the attached accepted margin sketches.

Subject: a folded map with a dotted path.

Output: exactly 256 x 256 pixels, PNG with alpha.
```

Приёмка: на 24 px пять значков различаются без подписей; яйцо не похоже на каплю.

## 32. Рейка · nursery.png

- **Сохранить:** `astra-inbox\rail\nursery.png`
- **Приложить:** принятые astra-inbox\margins\rail-*.png (M01–M05)
- **Где в приложении:** значок раздела на рейке (24 px)

```
Night Diary style navigation icon for a cozy desktop Pokémon companion app, drawn like a pencil sketch from a Pokémon trainer's travel diary. Simple icon-like sketch: one continuous confident outline, no hatching, slightly imperfect hand-drawn line, lines about 18 px thick at full size so it stays clear at 24 px. One single line colour, muted lavender-grey #9A8FBE, on a genuinely transparent background. No fill, no second colour, no gradients, no glow, no frame, no text, no letters, no numbers. Centered, fills about 80% of the canvas with at least 8% transparent margin. Match the line character of the attached accepted margin sketches.

Subject: an upright egg with three spots.

Output: exactly 256 x 256 pixels, PNG with alpha.
```

Приёмка: на 24 px пять значков различаются без подписей; яйцо не похоже на каплю.

## 33. Рейка · shop.png

- **Сохранить:** `astra-inbox\rail\shop.png`
- **Приложить:** принятые astra-inbox\margins\rail-*.png (M01–M05)
- **Где в приложении:** значок раздела на рейке (24 px)

```
Night Diary style navigation icon for a cozy desktop Pokémon companion app, drawn like a pencil sketch from a Pokémon trainer's travel diary. Simple icon-like sketch: one continuous confident outline, no hatching, slightly imperfect hand-drawn line, lines about 18 px thick at full size so it stays clear at 24 px. One single line colour, muted lavender-grey #9A8FBE, on a genuinely transparent background. No fill, no second colour, no gradients, no glow, no frame, no text, no letters, no numbers. Centered, fills about 80% of the canvas with at least 8% transparent margin. Match the line character of the attached accepted margin sketches.

Subject: a drawstring pouch with a tag.

Output: exactly 256 x 256 pixels, PNG with alpha.
```

Приёмка: на 24 px пять значков различаются без подписей; яйцо не похоже на каплю.
