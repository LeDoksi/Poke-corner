"""Static final design sheets and handoff metadata; no game logic or app implementation."""
import html
import json
from html.parser import HTMLParser
from pathlib import Path
from preparation import CSS as PREP_CSS, sheets as preparation_sheets

ROOT=Path(__file__).resolve().parent
PACK=ROOT.parent/'art-production/last-attempt/final-assets'
manifest=json.loads((PACK/'manifest.json').read_text(encoding='utf-8'))
content=json.loads((ROOT.parent/'catalog-package/runtime-content.json').read_text('utf-8'))
audio=json.loads((ROOT.parent/'voice-preview/delivery.json').read_text('utf-8'))
assert len(content['species'])==1025 and len({row['id'] for row in content['species']})==1025
assert len(audio['voices'])==868 and all(row['status']=='ready' for row in audio['voices'])
assert all((ROOT.parent/'voice-preview'/row['path']).exists() for row in audio['voices']+audio['effects'])
rows=manifest['trophies'];assert len(rows)==56
registry={row['id']:row for row in rows}
assert len(registry)==56 and len(manifest['albumCards'])==26
titles={'species':'Полевой альбом','population':'Большая компания','catches':'Рады знакомству',
        'expeditions':'В путь','regions':'Карта воспоминаний','team':'Растём вместе','care':'Рядом',
        'nursery':'Новая жизнь','evolution':'Время расти','shiny':'Особенная встреча',
        'nick':'Теперь ты Росток','favorite':'Любимчик','type-team':'На своём месте','bonus-find':'Слаженная работа',
        'nursery-expedition':'Из гнезда — в путь','family-expedition':'Семейное дело',
        'evolved-expedition':'Новая глава','all-berries':'Полная корзинка','berry-evolution':'По рецепту',
        'branch-choice':'Осознанный выбор','offspring-choice':'Ждали именно тебя','all-legends':'Легенды рядом',
        'second-trait':'Второе призвание','retrain':'Новые привычки','rehome':'В добрые руки','xp-potion':'Сила в капле'}
requirements={'species':'Откройте25 видов','population':'Соберите10 питомцев','catches':'Поймайте25 покемонов',
              'expeditions':'Завершите10 походов','regions':'Посетите4 местности','team':'Поднимите троих до уровня10',
              'care':'Подготовьте питомца к эволюции','nursery':'Вылупите5 разных начальных видов',
              'evolution':'Выполните5 разных переходов','shiny':'Получите3 разных shiny-вида',
              'nick':'Дайте питомцу кличку','favorite':'Отметьте питомца избранным',
              'type-team':'Завершите поход тройкой с бонусом каждого участника',
              'bonus-find':'Получите дополнительную находку в экспедиции',
              'nursery-expedition':'Завершите поход вылупившимся питомцем',
              'family-expedition':'Завершите поход родителем и его потомком вместе',
              'evolved-expedition':'Завершите поход питомцем после эволюции',
              'all-berries':'Получите все шесть типов ягод','berry-evolution':'Выполните ягодный рецепт эволюции',
              'branch-choice':'Выберите ветвь эволюции камнем','offspring-choice':'Выберите потомство талисманом',
              'all-legends':'Получите каждого доступного легендарного покемона',
              'second-trait':'Откройте вторую особенность на уровне10','retrain':'Переобучите особенность',
              'rehome':'Передайте питомца в добрые руки','xp-potion':'Примените зелье с полезной прибавкой опыта'}
initial={'species':'Откройте первый вид','population':'Соберите3 питомцев','catches':'Поймайте первого покемона',
         'expeditions':'Завершите первую экспедицию','regions':'Посетите первую местность',
         'team':'Получите первый опыт экспедиции','care':'Впервые покормите питомца',
         'nursery':'Вылупите первый начальный вид потомства','evolution':'Выполните первый переход эволюции',
         'shiny':'Получите первого shiny-покемона'}
progress={'species':'8 / 25 видов','population':'8 / 10 питомцев','catches':'5 / 25 поимок',
          'expeditions':'3 / 10 походов','regions':'3 / 4 местностей','team':'0 / 3 питомцев уровня10',
          'care':'58% до готовности к эволюции','nursery':'0 / 1 начальных видов','evolution':'0 / 1 переходов',
          'shiny':'0 / 1 shiny-видов'}


def asset(path):return '../art-production/last-attempt/final-assets/'+path


def top(active):
    nav=''.join('<span class="nav '+('selected' if name==active else '')+'">'+name+'</span>' for name in ['Уголок','Покедекс','Экспедиции','Питомник','Магазин'])
    return '<header><div class="brand"><svg viewBox="0 0 40 40" aria-hidden="true"><circle cx="20" cy="20" r="17" fill="#fff" stroke="currentColor" stroke-width="3"/><path d="M3 20a17 17 0 0 1 34 0" fill="#cf4f40"/><path d="M3 20h34" stroke="currentColor" stroke-width="3"/><circle cx="20" cy="20" r="6" fill="#fff" stroke="currentColor" stroke-width="3"/></svg>PokéCorner</div><div class="wallet"><svg width="24" height="24" viewBox="0 0 24 24" aria-hidden="true"><circle cx="12" cy="12" r="10" fill="#e8b244" stroke="#946627" stroke-width="2"/><circle cx="12" cy="12" r="7" fill="none" stroke="#ffe19a" stroke-width="2"/></svg>1240 <span>Встречи · 2</span><button disabled aria-label="Настройки">Настройки</button></div></header><nav>'+nav+'</nav>'


def frame(title,active,body,actions=''):
    return '<article class="window">'+top(active)+'<div class="title"><h1>'+title+'</h1>'+actions+'</div>'+body+'</article>'


layers=[]
for item_id,point in zip(['trophy-population-1','trophy-expeditions-1','trophy-care-1'],manifest['shelf']['anchors']):
    row=registry[item_id];w,h=row['logicalSize'];foot=row['footAnchor']
    x=round(point[0]-w*foot[0]);y=round(point[1]-h*foot[1])
    layers.append('<img class="award-layer" src="'+asset(row['file'])+'" alt="Заработанная награда" style="left:'+str(x/1448*100)+'%;top:'+str(y/582*100)+'%;width:'+str(w/1448*100)+'%;height:'+str(h/582*100)+'%">')
lip=manifest['frontlip'];rect=lip['rect']
layers.append('<img class="award-layer" src="'+asset(lip['file'])+'" alt="" style="left:'+str(rect[0]/1448*100)+'%;top:'+str(rect[1]/582*100)+'%;width:'+str((rect[2]-rect[0])/1448*100)+'%;height:'+str((rect[3]-rect[1])/582*100)+'%">')
home_body='<div class="room-stage"><img class="room" src="'+asset(manifest['room']['file'])+'" alt="Фиксированная детальная комната">'+''.join(layers)+'<svg class="pet-shadow" viewBox="0 0 46 10" aria-hidden="true"><ellipse cx="23" cy="5" rx="23" ry="5" fill="#2f1e0f" fill-opacity=".26"/></svg><img class="pet-sprite" src="../catalog-package/assets/normal/133.gif" alt="Исходный анимированный Иви"></div><div class="care"><strong>Eevee · #0133</strong>'+''.join('<div><span>'+name+'</span><div class="track"><i style="width:'+width+'%;background:'+color+'"></i></div></div>' for name,width,color in [('Уход','58','#39835b'),('Поимка','36','#397fac'),('Лакомства','73','#d99936')])+'</div><div class="pet-actions"><button disabled>Погладить</button><button disabled>Покормить</button></div>'
home=frame('Мой уголок','Уголок',home_body,'<button disabled>Достижения</button>')

products=[('Пачка лакомств','5 штук для питомца','20','У вас:8'),('Бустер ухода','+7500 CP','20','У вас:1'),
          ('Талисман удачи','×4 к шансу shiny следующего исхода','30','Активен:0'),('Камень эволюции','Выберите допустимую ветвь','20','У вас:1'),
          ('Талисман потомства','Выберите доступный вид из рецепта пары','50','У вас:0'),('Зелье опыта','+300 опыта; полезная прибавка видна до применения','40','У вас:2')]
product_ids=['treats','care-booster','lucky-charm','evolution-stone','offspring-charm','xp-potion']
berry_ids=['cheri','oran','aspear','pecha','chesto','rawst']
def item_icon(item_id):
    return '<img class="item-icon" src="assets/shop/'+item_id+'.png" alt="" width="80" height="80">'
shop_items=''.join('<div class="product">'+item_icon(item_id)+'<div class="product-copy"><h3>'+name+'</h3><p>'+effect+'</p><small>'+stock+'</small></div><div class="buy"><strong>'+price+' монет</strong><button disabled>Купить</button></div></div>' for item_id,(name,effect,price,stock) in zip(product_ids,products))
berries=[('Чери','Красная'),('Оран','Синяя'),('Аспир','Жёлтая'),('Печа','Розовая'),('Често','Фиолетовая'),('Роуст','Зелёная')]
berry_items=''.join('<div class="product">'+item_icon(item_id)+'<div class="product-copy"><h3>'+name+'</h3><p>'+color+' ягода · для рецептов и эволюций</p><small>У вас:6</small></div><div class="buy"><span class="quantity">− &nbsp; 6 &nbsp; +</span><strong>30 монет</strong><button disabled>Купить 6</button></div></div>' for item_id,(name,color) in zip(berry_ids,berries))
shop=frame('Магазин','Магазин','<p class="intro">Расходники и все шесть ягод в одном списке.</p><h2>Для питомцев</h2><div class="products">'+shop_items+'</div><h2>Ягоды</h2><div class="products">'+berry_items+'</div>')

cards=[]
for index,item_id in enumerate(manifest['albumCards']):
    row=registry[item_id];chain=row.get('chainId');special=item_id.removeprefix('trophy-special-')
    key=chain or special;opened=item_id in {'trophy-species-1','trophy-population-1','trophy-catches-1',
        'trophy-expeditions-1','trophy-regions-1','trophy-team-1','trophy-care-1'}
    picture=asset(row['file'] if opened else row['maskFile'])
    text=initial[key] if chain and not opened else requirements[key]
    progress_text='<p class="progress-line">'+html.escape(progress[key])+'</p>' if chain else ''
    cards.append('<div data-achievement-id="'+item_id+'" class="achievement '+('opened' if opened else 'locked')+'"><img src="'+html.escape(picture)+'" alt="'+('Открытая награда' if opened else 'Силуэт закрытой награды')+'"><div><h3>'+html.escape(titles[key])+'</h3><p>'+html.escape(text)+'</p>'+progress_text+'<small>'+('Ступень I · следующая цель впереди' if opened else 'Ещё не открыто')+'</small></div></div>')
album=frame('Достижения','Уголок','<p class="intro">10 цепочек и16 самостоятельных целей. В каждой цепочке одна карточка; будущий вид награды скрыт.</p><h2>Моя полка</h2><div class="shelf-selection">'+''.join('<div><img src="'+asset(registry[item_id]['file'])+'" alt="Награда на выбранном месте"><span>Место '+str(i+1)+'</span><button disabled>Заменить</button></div>' for i,item_id in enumerate(['trophy-population-1','trophy-expeditions-1','trophy-care-1']))+'</div><h2>Альбом</h2><div class="achievements">'+''.join(cards)+'</div>','<button disabled>Назад в уголок</button>')

css='''*{box-sizing:border-box}body{margin:0;background:#e9e2d3;color:#26352d;font:17px "Nunito", "Segoe UI",sans-serif}main{max-width:1460px;margin:auto;padding:24px}a{color:#24543d;text-underline-offset:3px}a:focus-visible{outline:3px solid #24543d;outline-offset:4px}.sheet-nav{display:flex;gap:28px;flex-wrap:wrap;padding:12px 0 28px}.caption{margin:0 0 18px;max-width:75ch;line-height:1.55}.window{background:#f8f4e9;border:1px solid #b9b2a1;border-radius:12px;padding:18px;margin-bottom:52px}header{display:flex;align-items:center;justify-content:space-between;gap:24px;padding-bottom:16px}.brand{display:flex;align-items:center;gap:12px;font-size:26px;font-weight:800}.brand svg{width:32px;height:32px}.wallet{display:flex;align-items:center;gap:18px;font-size:18px;font-variant-numeric:tabular-nums}.wallet span{color:#873b2c;border:1px solid #b76650;background:#f4dfd0;padding:8px 14px;border-radius:9px}nav{display:flex;border:1px solid #c6c0b1;border-radius:9px;overflow:hidden}.nav{flex:1;text-align:center;padding:14px 8px;font-weight:700}.selected{background:#2f654d;color:#fff}.title{display:flex;align-items:center;gap:12px;margin:14px 0}.title h1{margin:0 auto 0 0;font-size:38px;letter-spacing:-.025em}button{border:1px solid #929a8e;background:#f7f4e8;color:#274932;padding:10px 18px;border-radius:9px;font:700 16px "Nunito", "Segoe UI",sans-serif}button:disabled{opacity:1}.room{display:block;width:100%;height:auto;border-radius:8px}.care{display:grid;grid-template-columns:1.1fr repeat(3,1fr);gap:20px;align-items:center;padding:18px 0}.care span{display:block;margin-bottom:8px}.track{height:11px;border-radius:7px;background:#d8d7ce;overflow:hidden}.track i{display:block;height:100%;border-radius:7px}.pet-actions{display:flex;justify-content:flex-end;gap:16px}.pet-actions button{min-width:220px;background:#31694d;color:#fff;border:0;padding:14px}.intro,p{line-height:1.5}h2{font-size:24px;margin:28px 0 12px}h3{font-size:20px;margin:0 0 7px}p{margin:0 0 10px}small{color:#50614f}.products{display:grid;grid-template-columns:1fr 1fr;gap:0 24px}.product{display:flex;justify-content:space-between;gap:18px;padding:20px 4px;border-bottom:1px solid #c8c2b3}.buy{display:flex;flex-direction:column;align-items:flex-end;justify-content:center;gap:10px;white-space:nowrap}.buy button{background:#31694d;color:#fff;border:0}.quantity{background:#ebe7d9;border-radius:8px;padding:8px 14px}.shelf-selection{display:flex;gap:36px;justify-content:center;padding:18px 0;background:#eee7d7;border-radius:9px}.shelf-selection>div{display:flex;align-items:center;gap:14px}.shelf-selection img{max-width:132px;height:94px;object-fit:contain}.achievements{display:grid;grid-template-columns:1fr 1fr;gap:0 24px}.achievement{display:flex;gap:24px;align-items:center;padding:20px 0;border-bottom:1px solid #c8c2b3}.achievement img{width:132px;height:94px;object-fit:contain}.locked img{filter:brightness(0);opacity:.8}section{scroll-margin-top:18px}@media(max-width:850px){main{padding:12px}.window{padding:12px}header,.wallet,.title,.shelf-selection{flex-wrap:wrap}.nav{font-size:13px}.title h1{font-size:29px}.care{grid-template-columns:1fr 1fr}.products,.achievements{grid-template-columns:1fr}.pet-actions button{min-width:0;flex:1}.shelf-selection>div{flex-wrap:wrap;max-width:180px}.achievement{gap:16px}}'''
css='@font-face{font-family:Nunito;src:url("../../prototype-v1/assets/nunito.ttf") format("truetype");font-display:swap}'+css
css+='.product{align-items:center}.product-copy{flex:1;min-width:0}.item-icon{width:80px;height:80px;flex:none;object-fit:contain}@media(max-width:500px){.product{display:grid;grid-template-columns:64px minmax(0,1fr);gap:12px}.item-icon{width:64px;height:64px}.buy{grid-column:2;align-items:flex-start}}'
css+='.room-stage{position:relative;line-height:0;overflow:hidden;border-radius:8px}.award-layer{position:absolute;object-fit:contain}.pet-sprite{position:absolute;left:50.4834254144%;top:65.8075601375%;width:9.5303867403%;height:24.2268041237%;image-rendering:pixelated}.pet-shadow{position:absolute;left:53.729281768%;top:89.3470790378%;width:3.1767955801%;height:1.7182130584%}'
assert (ROOT/'../../prototype-v1/assets/nunito.ttf').resolve().exists()
contract='<!-- THESIS: Fixed detailed companion room with independently earned trophies. OWN-WORLD: Cream paper, honey timber illustration, Nunito, forest-green native controls. STORY: Visit companion, buy consumables, browse earned awards. FIRST VIEWPORT: Five sections above wide room, three care bars and pet actions below. FORM: Static operate-mode design sheet; seed pokecorner-fixed-room. FINISH: Art and semantic specs reviewed; no claim of live WPF or browser validation. -->'
document='<!doctype html><html lang="ru"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>PokéCorner — итоговый статичный дизайн</title><style>'+css+'</style></head><body>'+contract+'<main><p class="caption">Итоговые статичные экраны фиксированной комнаты. Значения коллекции и кошелька демонстрационные; кнопки не исполняют игровую логику.</p><div class="sheet-nav"><a href="#home">Мой уголок</a><a href="#shop">Магазин</a><a href="#album">Достижения и полка</a><a href="../art-production/last-attempt/final-assets/README.md">Пакет для внедрения</a></div><section id="home">'+home+'</section><section id="shop">'+shop+'</section><section id="album">'+album+'</section></main></body></html>'
assert '<script' not in document and 'Украсить' not in document and 'Купить и поставить' not in document
assert 'animated-room.png' not in home_body and '133.gif' in home_body and home_body.count('class="award-layer"')==4
assert 'Вылупите первый начальный вид потомства' in document and 'Получите первого shiny-покемона' in document
assert 'Вылупите5 разных начальных видов' not in document and 'Получите3 разных shiny-вида' not in document
assert document.count('class="progress-line"')==10
assert '<button disabled>Полка</button>' not in home and home.count('<button disabled>Достижения</button>')==1
assert document.count('class="item-icon"')==12


class Links(HTMLParser):
    def __init__(self):
        super().__init__()
        self.files=0
        self.sections=set()
        self.anchors=set()

    def handle_starttag(self,tag,attrs):
        if tag=='section':
            self.sections.add(dict(attrs)['id'])
        for key,value in attrs:
            if key in ['src','href'] and not value.startswith('#'):
                assert (ROOT/value).resolve().exists(),value
                self.files+=1
            elif key=='href' and value.startswith('#'):
                self.anchors.add(value[1:])


Links().feed(document)
extra=preparation_sheets(frame)
extra_nav='<a href="#settings">Настройки</a><a href="#onboarding">Обучение</a><a href="#states">Состояния действий</a>'
document=document.replace('</style>',PREP_CSS+'</style>').replace('<a href="../art-production/last-attempt/final-assets/README.md">',extra_nav+'<a href="../art-production/last-attempt/final-assets/README.md">')
document=document.replace('</main>',''.join('<section id="'+key+'">'+sheet+'</section>' for key,sheet in extra.items())+'</main>')
assert len(extra)==3 and document.count('class="tour-step"')==6 and document.count('class="state"')==18
assert document.count('<section id=')==6 and '<script' not in document
for label in ['Суперхардкор','Скрывать в полноэкранных приложениях','Показывать кольца прогресса','Размер питомца','Громкость голоса','Громкость эффектов','Запускать вместе с Windows','Очистить все данные']:
    assert label in extra['settings'],label
verified_links=Links()
verified_links.feed(document)
assert len(verified_links.sections)==6 and verified_links.anchors<=verified_links.sections
(ROOT/'index.html').write_text(document,encoding='utf-8')
report={'staticScreens':6,'onboardingSteps':6,'serviceStates':18,'activeAwards':56,'albumCards':26,'berriesOnSameScreen':6,'runtimeJavaScript':False,
        'localizedSpecies':1025,'portableVoices':868,'temporaryCries':24,'soundEffects':1,'effectsVolumeDefault':30,'movementPreference':'Windows ClientAreaAnimation','closedSpeciesSearch':'number-and-visible-route-hint','specimenSelectionState':True,
        'localFileLinks':verified_links.files,'leafCoverage':sorted(verified_links.sections),'internalAnchorsExist':True,
        'shopIcons':12,'homeAchievementEntries':1,'linksExist':True,'roomMode':'fixed-with-independent-awards','liveBrowserVerified':False}
(ROOT/'checks.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
print(json.dumps(report))
