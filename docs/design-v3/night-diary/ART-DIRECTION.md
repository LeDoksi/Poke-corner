# Арт-дирекция — ночной дневник тренера

Решение пользователя от 21.10.2026. Обязательно для всего редизайна: макетов, компонентов, иллюстраций, производства ассетов и приёмки WPF. Уточняет принятый concept.png; при противоречии старой картинки этим правилам действуют эти правила.

## Дневник о жизни тренера

Заметки на полях связаны с конкретным экраном и текущим занятием. Это зарисовки предметов, следов, наблюдений и небольшие осмысленные записи. Растительный орнамент не является универсальным оформлением. Растения допустимы там, где они действительно относятся к содержанию.

| Экран / ситуация | Подходящие мотивы |
| --- | --- |
| Подготовка экспедиции | Сложенная карта, пунктир маршрута, следы ботинок, дорожные пометки |
| Подбор команды | Рюкзак и свёрнутая карта; небольшие заметки о составе похода |
| Поход | Пунктир пройденного пути, следы; без выдуманной геолокации и новых показателей |
| Возвращение и результат | Распакованный рюкзак, зарисовка найденного предмета; реальные суммы остаются обычным интерфейсом |
| Покедекс и карточка | Набросок знакомого покемона, характерной детали или наблюдения; никаких спойлеров неизвестного вида |
| Питомник | Скорлупа, схема заботы, мягкие бытовые заметки; без раскрытия ещё неизвестного результата |
| Магазин | Предметы из ассортимента, эскиз упаковки; цены и доступность только функциональным текстом |
| Достижения | Значок, лента, набросок трофея, запись о завершённом событии |
| Уголок | Следы жизни текущего спутника, игрушка, бытовая зарисовка |
| Настройки, восстановление, ошибки, подтверждения | Минимум украшений или их отсутствие; ясность действия важнее рисунка |

Это словарь возможностей. Поля богатые (решение D-06 от 22.10.2026): на основном экране несколько зарисовок и рукописных заметок тренера, все по смыслу этого экрана. Богатство не означает одинаковый орнамент: каждый экран собирает свой набор мотивов, без повторяющейся ботаники и одного и того же штампа карты.

Рисунок не перекрывает текст, не выглядит кнопкой и не сообщает выдуманную механику, награду, таймер или характеристику. Подписи с продуктовым смыслом — нативный читаемый текст, доступный локализации; обязательная информация не запекается в растр. Рукописная интонация допустима в коротких второстепенных заметках, если они правдивы для состояния.

## Восемь разных мест

Камера находится непосредственно внутри локации. Передний план близко к зрителю, объекты обрезаны рамкой, поверхность под ногами ощущается материально. Обзорная открытка с далёкой достопримечательностью не подходит.

Ночь и уют определяют оболочку дневника. Локации сохраняют собственный характер: вулкан может быть беспощадным, руины — тревожными, перевал — холодным. Не делать все места уютными лесными полянками.

Каждое место имеет собственный промпт: позиция камеры, главный силуэт, материалы, композиция, свет, палитра и исключения. Общим остаётся качество пиксельной живописи и глубина. Вариация одного фона или смена цвета не считается новой локацией.

### Лесная роща · grove

**Образ:** мы стоим внутри старой густой чащи. Крупный обрезанный ствол слева, переплетённые корни, сухая листва под ногами, мох и грибы. Справа диагональный упавший ствол; за ним тесные вертикали деревьев уходят в индиго. Светлячки у корней, редкие лучи сквозь скрывающую луну крону.

**Промпт:** Wide intimate pixel-art view from walking height INSIDE an ancient dense forest hollow. Close cropped ridged trunk left, interwoven roots and dry leaf litter underfoot, moss and scattered mushrooms, diagonal fallen log right, closely spaced vertical trunks receding into deep indigo. Sparse amber fireflies and narrow moonbeams, moon hidden by canopy. Asymmetric layered composition, tangible solid forest floor, detailed authored pixel clusters. No water, lake, pond, river, stream, reflective blue ground, horizon panorama or distant mountain.

### Озёрный берег · lake

**Образ:** стоим у самой кромки спокойной замкнутой воды, между близкими камышами и округлыми мокрыми камнями. Поверхность занимает середину, противоположный берег читается поперёк кадра. Серебристый свет, глубокий холодный синий, тонкие круги на воде.

**Промпт:** Pixel-art view from the waterline INSIDE a sheltered freshwater lakeshore. Close reeds and rounded wet pebbles frame the lower corners, calm water fills the middle, a continuous opposite shore visibly closes the basin. Narrow silver moon reflection, small gentle ripples, still cool blue air. Low intimate framing with foreground bank underfoot. No open ocean horizon, breaking surf, sea foam, towering cliffs or forest clearing composition.

### Морское побережье · coast

**Образ:** открытый ветру берег; мы рядом с разбивающейся волной, мокрой солёной плитой и полосой пены. Горизонт открыт, вода движется широкими массами. Бирюзовая пена и холодный стальной свет, никакого замкнутого пруда.

**Промпт:** Pixel-art view from INSIDE an exposed ocean shoreline, standing on a wet low rock shelf beside a breaking wave. Close foamy wash runs across dark stone and scattered shells, large rolling swells beyond, open uninterrupted sea horizon and salt spray in wind. Broad horizontal motion, cold steel-blue light with pale turquoise foam. No reeds, still pond, enclosing opposite bank, freshwater lilies or sheltered forest lake.

### Горные тропы · mountains

**Образ:** узкий каменный траверс. Почти вплотную видна ломаная слоистая стена, под ногами щебень, сбоку резкий провал. Главные линии — диагональные пласты скалы; поверхность голая, сухая, без снега.

**Промпт:** Pixel-art scene from INSIDE a rugged mountain traverse at boot height. A close angular stratified rock wall dominates one side, rough scree and fractured stone ledge underfoot, steep exposed drop on the other side. Strong diagonal geology, wind-blown dust, dry slate and muted violet moonlight. Spatial depth through the nearby ledge and chasm, not a centered distant peak. No snow, ice, lava, trees framing a vista or postcard mountain panorama.

### Пустынные дюны · desert

**Образ:** стоим в глубоком изгибе дюны; передний план — тонкая рябь песка, рядом высокий серповидный гребень. Композиция из крупных мягких кривых, пустота, ветер. Ночной песок медно-лиловый, холодные тени.

**Промпт:** Pixel-art view from INSIDE a wind-carved desert dune hollow. Close fine ripple patterns underfoot, a high sweeping crescent sand ridge rises beside the viewer and curves across the frame, wind lifts a thin veil from its crest. Large uncluttered curved masses, copper-lilac night sand and deep cool shadows. No oasis, water, ruins, forest, mountain wall or cactus border. Not an aerial dune panorama.

### Вулканическая долина · volcano

**Образ:** мы у подножья, непосредственно в поле расколотого базальта. Перед нами реки лавы, прожилки раскалённого камня, пепел и жар; жизнь отступила. Вулканический конус вдали не заменяет эту сцену.

**Промпт:** Pixel-art environment from INSIDE the barren volcanic foothills, standing on close fractured black basalt. Branching molten lava rivers pass directly before us between jagged crust plates, glowing fissures illuminate the underside of nearby rocks, ash hangs in scorching air with restrained heat shimmer. Harsh orange-red light against soot-black and deep plum, asymmetric low viewpoint, tangible hostile ground. No distant volcano postcard, centered cone, vegetation, flowers, lake, blue water, snow or cozy campsite.

### Снежный перевал · snow

**Образ:** мы в тесном проходе между высокими сугробами и обледеневшими выступами. Снег частично скрывает поверхность и даль; крупные светлые массы и нависающие карнизы, совсем другой силуэт, чем сухая горная тропа.

**Промпт:** Pixel-art scene from INSIDE a deep snowbound mountain pass, close shoulder-high wind-packed snowbanks and overhanging white cornices, partly buried rock and translucent blue ice at arm's reach. Powder drifts across the near path, blowing snow obscures the far opening. Large pale snow masses, compressed enclosed perspective, blue-white diffuse moonlight. No dry scree composition recolored white, distant scenic peaks, trees, lava or lake surface.

### Древние руины · ruins

**Образ:** внутри разрушенного каменного зала. Близкие обломанные колонны, треснувшие плиты, тёмные боковые проходы; в одном из них едва видны анонимные глаза. Холодный луч падает через пролом; остаётся много настоящей темноты.

**Промпт:** Pixel-art environment from INSIDE a dark ruined stone hall. Close cropped broken column bases, cracked flagstones underfoot, collapsed lintels overhead and deep black side passages. One thin cold shaft of moonlight enters through a broken ceiling; a very subtle anonymous pair of eyes glints far inside a shadowed doorway, no identifiable creature or species. Mysterious oppressive enclosed masonry, uneven architectural rhythm, charcoal and desaturated violet. No lush garden, dominant vegetation, water, distant temple facade, tourist panorama or bright welcoming courtyard.

## Проверка различимости

- Роща / озеро: у рощи сухая земля и вертикали стволов, у озера вода и замыкающий её дальний берег.
- Озеро / море: спокойный замкнутый водоём против открытого горизонта, прибоя, пены и солёных скал.
- Горы / снег: диагональные голые пласты и щебень против крупных светлых сугробов, льда и скрытой снегом дали. Не один и тот же кадр с новым материалом.
- Горы / вулкан: сухой каменный траверс против близких рек лавы, чёрной корки, жара и пепла.
- Руины / роща: каменная архитектура, замкнутость и тёмные проходы против живых вертикалей леса. Руины не превращаются в заросший сад.
- Дюны имеют собственный силуэт мягких кривых; не заменять их каменной пустошью.

Перед приёмкой R6 показать общий лист всех восьми сцен без названий и отдельно проверить уменьшенный размер и фактический кроп в окне. Названия должны угадываться по изображению. Совпавшие по композиции места перерабатываются. Ни один общий шаблон «рамка по краям + дорожка к свету по центру» не размножается на весь набор.

## Состояние производства

Промпты выше — отдельные брифы, составленные по новым требованиям для R6, а не заявление о готовности восьми иллюстраций. Сейчас перерабатывается пилот рощи R2. Его старая версия с водой и универсальными веточками сохраняется как история и не используется как действующий арт-эталон. История точных запросов генерации и актуальные варианты — в pilot/manifest.json.

