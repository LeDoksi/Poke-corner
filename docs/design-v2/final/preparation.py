"""Static companion settings, onboarding and boundary states; no save access."""
from html import escape
import json
from pathlib import Path

CSS = '''.prep-grid{display:grid;grid-template-columns:1fr 1fr;gap:24px}.setting-group,.tour-step,.state{padding:20px 0;border-top:1px solid #c8c2b3}.setting-row{display:flex;justify-content:space-between;gap:24px;align-items:center;padding:12px 0}.setting-row>div:first-child{max-width:65ch}.setting-row small{display:block}.setting-value{flex:none;max-width:45%;text-align:right;font-weight:800}.setting-row input[type=range]{width:160px;max-width:100%;accent-color:#31694d}.setting-row input[type=checkbox]{width:20px;height:20px;accent-color:#31694d}.mode{display:flex;gap:8px;flex-wrap:wrap}.mode .active{background:#31694d;color:white}.warning{background:#f4dfd0;border-left:4px solid #b76650;padding:14px;border-radius:0 9px 9px 0;color:#873b2c}.muted-control{color:#687569;background:#e6e5db}.tour-step{display:grid;grid-template-columns:50px 1fr;gap:16px}.step-number{width:42px;height:42px;border-radius:50%;background:#2f654d;color:white;display:grid;place-items:center;font-weight:800}.step-example{margin:12px 0;padding:14px;background:#eee7d7;border-radius:9px}.state-actions{display:flex;gap:12px;flex-wrap:wrap;margin-top:12px}.state a{padding:8px 0}.danger{border-color:#b76650;color:#873b2c}.tour-footer{display:flex;justify-content:space-between;gap:16px;flex-wrap:wrap;margin-top:24px}.settings-note{font-size:15px;max-width:85ch}@media(max-width:850px){.prep-grid{grid-template-columns:1fr}.setting-row{align-items:flex-start;gap:12px}.setting-value{max-width:42%}.setting-row input[type=range]{width:120px}}'''

def sheets(frame):
    def row(label, detail, value):
        return '<div class="setting-row"><div><strong>'+label+'</strong><small>'+detail+'</small></div><div class="setting-value">'+value+'</div></div>'
    def toggle(label, detail, checked):
        return row(label,detail,'<input type="checkbox" disabled '+('checked ' if checked else '')+'aria-label="'+escape(label)+'">')
    settings_body='<p class="intro">Настройте спутника под свой день. Настройки сохраняются сразу.</p>'
    settings_body+='<div class="setting-group"><h2>Режим заботы</h2><div class="mode"><button class="active" disabled>Чилл</button><button disabled>Хардкор</button></div><p>В чилле клубок напоминает о внимании, но питомец не уходит. В хардкоре после 6 часов невнимания он уходит на час.</p>'
    settings_body+=toggle('Суперхардкор','Доступен в хардкоре: питомец может уйти навсегда. При переходе в чилл выключается.',False)+'</div>'
    settings_body+='<div class="setting-group"><h2>На рабочем столе</h2>'
    settings_body+=toggle('Скрывать в полноэкранных приложениях','Игры и фильмы — без спутника поверх изображения.',True)
    settings_body+=toggle('Показывать кольца прогресса','Уход, поимка и лакомства рядом с питомцем. По умолчанию выключены.',False)
    settings_body+=row('Размер питомца','От 48 до 384 пикселей; размер меняется сразу.','<label>96 px<br><input type="range" min="48" max="384" value="96" disabled aria-label="Размер питомца"></label>')
    settings_body+='<p class="settings-note">Перетащите питомца в удобное место. Сохранённые положение, монитор и угол экрана переносятся при обновлении.</p></div>'
    settings_body+='<div class="setting-group"><h2>Звук и движение</h2>'+row('Громкость голоса','0% полностью отключает голос. Изменение слышно при следующем поглаживании.','<label>70%<br><input type="range" min="0" max="100" value="70" disabled aria-label="Громкость голоса"></label>')
    settings_body+=row('Громкость эффектов','Отдельно от голоса: короткий crunch при кормлении. 0% отключает эффекты.','<label>30%<br><input type="range" min="0" max="100" value="30" disabled aria-label="Громкость эффектов"></label>')
    settings_body+='<p class="settings-note">Движение эффектов следует настройке анимаций Windows. Когда они отключены, недовольство остаётся неподвижным значком до внимания.</p></div>'
    settings_body+='<div class="setting-group"><h2>Приложение</h2>'+toggle('Запускать вместе с Windows','Переключатель показывает фактическое состояние автозапуска Windows.',False)
    settings_body+=row('Знакомство с PokéCorner','Повторите короткое обучение в любое время.','<button disabled>Показать обучение</button>')
    settings_body+='<p>Активность учитывает мышь, клавиатуру и геймпад. Содержимое нажатий не записывается.</p></div>'
    settings_body+='<div class="setting-group"><h2>Сохранённые данные</h2><p>Удаление сбросит коллекцию, предметы, достижения и настройки.</p><button class="danger" disabled>Очистить все данные…</button></div>'

    steps=[
        ('Спутник всегда рядом','Один клик — погладить. Двойной — открыть меню. Кольца прогресса можно включить в настройках.','Клик → сердечки и голос · двойной клик → меню'),
        ('Ваш уютный уголок','Уход помогает расти, активность приносит покеболы и лакомства. Заслуженные трофеи занимают три места на полке.','Уход · поимка · лакомства — три отдельные шкалы'),
        ('Каждый питомец — особенный','Ищите и фильтруйте коллекцию по типу, редкости и доступной эволюции. В карточке есть характер, дата поимки, кличка и ветка роста.','☆ Избранное · дать кличку · покормить · эволюция'),
        ('Соберите команду в путь','Одновременно идёт один поход. Выберите местность, сложность и длительность. Трое получают полную добычу и шанс находок и встречи.','30 мин · 1,5 часа · 3 часа · 6 часов; один −20% базы, двое полная база'),
        ('Новая жизнь в питомнике','Посмотрите совместимость и 2–3 варианта потомства. Запуск стоит ягоды; талисман позволяет выбрать доступный вид. Малыш всегда начинает семью с первого звена.','Одна пара → одно гнездо · яйца не продаются'),
        ('Ягоды, предметы и открытия','Шесть видов ягод находят в походах или покупают. Лакомства отдельно. Камень выбирает ветку эволюции, а достижения становятся трофеями интерьера.','Совместимая пара в двух закрытых покеболах — подарок к крупному обновлению'),
    ]
    tour='<p class="intro">Добро пожаловать в большой уютный апдейт. Ваша коллекция на месте. Шесть коротких шагов покажут новые возможности.</p><p><small>Макет последовательности: приложение показывает по одному шагу. Здесь все шаги доступны прокруткой.</small></p><div class="prep-grid">'
    for i,(title,text,example) in enumerate(steps,1):
        tour+='<article class="tour-step"><div class="step-number">'+str(i)+'</div><div><h2>'+title+'</h2><p>'+text+'</p><div class="step-example">'+example+'</div><small>Шаг '+str(i)+' из 6</small></div></article>'
    tour+='</div><div class="tour-footer"><button disabled>Пропустить обучение</button><div class="state-actions"><button disabled>Назад</button><button disabled>Дальше / Всё понятно</button></div></div><p><small>Обучение ничего не тратит. Можно повторить его из настроек.</small></p>'

    content=json.loads((Path(__file__).resolve().parent.parent/'catalog-package/runtime-content.json').read_text('utf-8'))
    example=next(r for r in content['species'] if r['id']==133)
    specimens=''.join('<div class="setting-row"><div><strong>'+escape(nickname)+'</strong><small>'+escape(example['displayName'])+' · уровень '+str(level)+' · '+escape(status)+'</small></div><button disabled>Открыть '+escape(nickname)+'</button></div>' for nickname,level,status in [('Росток',12,'спутник'),('Искра',7,'свободен · shiny'),('Туман',18,'в экспедиции')])
    states=[
        ('Закрытый вид · #0133','Подсказка: ищите в лесной местности. Найти можно по номеру или этой подсказке. Название, типы и другие свойства откроются после получения.','<button disabled>Показать местность</button>'),
        ('Ваши экземпляры · '+example['displayName'],escape(example['descriptionRu'])+'<p>У вас 3 питомца этого вида. Выберите, чью карточку открыть.</p>'+specimens,'<button disabled>Назад к виду</button>'),
        ('Нужны ещё 4 ягоды Оран','Для эволюции требуется 6. У вас 2.','<a href="#shop">Докупить 4 за 20 монет</a><button disabled>Найти в экспедиции</button>'),
        ('Не хватает 10 монет','Покупка стоит 30, у вас 20. Количество можно уменьшить; предметы и монеты не списаны.','<button disabled>Изменить количество</button>'),
        ('Команда ниже рекомендации','Средний уровень 6, рекомендуется 10: −10% к добыче и шансам бонусов. Итоговая основная добыча видна до отправки.','<button disabled>Изменить команду</button><button disabled>Отправить с этим составом</button>'),
        ('В пути двое','Полная основная добыча. Дополнительные находки и встреча доступны команде из трёх.','<button disabled>Добавить третьего</button>'),
        ('Этот вид ещё никто не встречал','Анимация пока не готова: вид нельзя получить; в рецепте он отмечен «Пока недоступно», без силуэта. Описание доступно только для уже открытого вида.','<button disabled>Вернуться к доступным вариантам</button>'),
        ('Питомец занят в походе','Для питомника выберите другого участника или дождитесь возвращения. Активный поход не отменяется автоматически.','<button disabled>Выбрать другого</button>'),
        ('Выбор здесь не нужен','У пары доступен только один начальный вид. Талисман потомства не применяется; его стоимость не добавляется.','<button disabled>Оставить случайный исход</button>'),
        ('Поход завершён','Награды ждут вас и не сгорают. Получение повторно не выдаёт добычу и не меняет результат.','<button disabled>Забрать награды</button>'),
        ('Зелье опыта: +80 из 300','До уровня 40 осталось 80 опыта. Будет использовано одно зелье; 220 опыта сверх максимума не начислятся.','<button disabled>Применить одно зелье</button><button disabled>Отмена</button>'),
        ('Большой апдейт — подарок вам','Два закрытых покебола содержат совместимую пару. Узнаете виды после броска. Обучение и повторный запуск не повторяют подарок.','<button disabled>Открыть первый покебол</button>'),
        ('Прервать поход?','Участники вернутся сразу. За незавершённый поход не будет добычи и опыта.','<button disabled>Оставить в пути</button><button class="danger" disabled>Прервать без наград</button>'),
        ('Команда уже вернулась','Пока было открыто подтверждение, поход завершился. Отмена больше недоступна; награды сохранены.','<button disabled>Забрать награды</button>'),
        ('Найти новый дом для Ростка?','Передаётся именно этот экземпляр. Вы получите 13 монет. Вернуть питомца нельзя; открытый вид и достижения остаются.','<button disabled>Оставить Ростка</button><button class="danger" disabled>Передать за 13 монет</button>'),
        ('Питомец под защитой','Избранного сначала нужно снять с избранного в карточке. Занятого походом или гнездом передавать нельзя. Активному спутнику сначала выберите замену.','<button disabled>Вернуться в карточку</button>'),
        ('Очистить сохранённые данные?','Первое подтверждение. Коллекция, предметы, достижения и настройки будут удалены.','<button disabled>Отмена</button><button class="danger" disabled>Продолжить…</button>'),
        ('Удалить всё окончательно?','Второе подтверждение. Закройте окно, чтобы сохранить данные. После удаления приложение начнёт работу заново.','<button disabled>Сохранить данные</button><button class="danger" disabled>Удалить и перезапустить</button>'),
    ]
    state_html='<p class="intro">Состояния действий: всегда понятно, что требуется, что будет получено и что останется после отмены.</p><div class="prep-grid">'
    for title,text,actions in states:
        state_html+='<article class="state"><h2>'+title+'</h2><div class="state-description">'+text+'</div><div class="state-actions">'+actions+'</div></article>'
    state_html+='</div>'
    return {
        'settings':frame('Настройки','',settings_body,'<button disabled>Закрыть</button>'),
        'onboarding':frame('Знакомство с обновлением','',tour),
        'states':frame('Состояния и подтверждения','',state_html),
    }
