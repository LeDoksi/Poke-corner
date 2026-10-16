"""User-reviewed design changes; this module only generates the review prototype."""
import json
from html import escape

def apply_revision(c):
    base,out,pages=c['BASE'],c['OUT'],c['pages']
    title,button,sprite,badge,panel,note,scene,meter,item,coins,icon=[c[k] for k in ('title','button','sprite','badge','panel','note','scene','meter','item','coins','icon')]
    catalog={s['id']:s for s in json.loads((base/'final/runtime-catalog.json').read_text())['species']}
    types={'normal':'Нормальный','grass':'Трава','poison':'Яд','fire':'Огонь','water':'Вода','electric':'Электро','flying':'Летающий','bug':'Насекомое','ground':'Земля','rock':'Камень','ice':'Лёд','psychic':'Психический','dark':'Тёмный','ghost':'Призрак','dragon':'Дракон','fairy':'Фея','steel':'Сталь','fighting':'Боевой'}
    rarities={'Common':'Обычный','Uncommon':'Необычный','Rare':'Редкий','Legendary':'Легендарный'}
    def setpage(key,html,label=None,tab=None):
        old=pages.get(key,(key,'',html));pages[key]=(label or old[0],old[1] if tab is None else tab,html)
    def action(label,attrs='',kind=''):
        return '<button type="button" class="pc-button '+kind+'" '+attrs+'>'+label+'</button>'
    def pills(sid):
        s=catalog[sid]
        return ''.join(badge(types[t],t) for t in s['types'])+badge(rarities[s['rarity']],'rarity-'+s['rarity'].lower(),'star')
    def about(sid):
        # Keep prepared factual content; do not reveal names of unopened evolutions.
        return '. '.join(p for p in catalog[sid]['descriptionRu'].split('. ') if not p.startswith(('Следующая эволюция:','Развивается из '))).rstrip('.')+'.'
    def card(sid,name,level=12,status='',shiny=False,can_grow=True,favorite=False,species=False):
        s=catalog[sid]
        attrs='data-sid="'+str(sid)+'" data-name="'+escape(name,quote=True)+'" data-types="'+' '.join(s['types'])+'" data-rarity="'+s['rarity']+'" data-shiny="'+str(shiny).lower()+'" data-grow="'+str(can_grow).lower()+'" data-favorite="'+str(favorite).lower()+'" data-level="'+str(level)+'"'
        if species:
            tags=''
            caption=s['displayName'];attrs+=' data-species-card="true"'
        else:
            tags=badge(status,'busy') if status else ''
            tags+=badge('Shiny','shiny','spark') if shiny else ''
            caption=name;attrs+=' data-personal-card="true"'
        attrs+=' data-care="'+str(level*1000)+'"'
        return '<button type="button" class="pc-pet-card" '+attrs+'><span class="pc-card-top"><span>#'+str(sid).zfill(4)+'</span>'+(icon('heart') if favorite else '')+'</span>'+sprite(sid,name,shiny=shiny)+'<div class="pc-row between"><strong class="pc-name">'+escape(caption)+'</strong>'+('' if species else '<span class="pc-muted">Ур. '+str(level)+'</span>')+'</div><div class="pc-badges">'+pills(sid)+tags+'</div></button>'
    def filters(species=False):
        return '<div class="pc-filters v2-filters"><input type="search" placeholder="'+('Номер или открытый вид' if species else 'Имя, кличка или номер')+'" aria-label="'+('Поиск вида' if species else 'Поиск питомца')+'" data-filter="search"><select aria-label="Сортировка" data-filter="sort"><option value="number">По номеру</option><option value="level">По уровню</option><option value="date">По дате поимки</option></select><select aria-label="Тип" data-filter="type"><option value="">Все типы</option>'+''.join('<option value="'+k+'">'+v+'</option>' for k,v in types.items())+'</select><select aria-label="Редкость" data-filter="rarity"><option value="">Любая редкость</option>'+''.join('<option value="'+k+'">'+v+'</option>' for k,v in rarities.items())+'</select></div><div class="pc-filter-checks">'+('' if species else '<label><input type="checkbox" data-filter="grow">Есть куда расти</label><label><input type="checkbox" data-filter="favorite">Избранные</label>')+'<label><input type="checkbox" data-filter="shiny">Shiny</label>'+action('Сбросить','data-reset-filters', 'small')+'</div>'
    own=[(1,'Росток',12,'',False,True,False),(4,'Огонёк',8,'',False,True,True),(7,'Squirtle',9,'В питомнике',False,True,False),(133,'Eevee',10,'На столе',False,True,False),(43,'Oddish',12,'',False,True,False),(152,'Chikorita',14,'',False,True,False),(25,'Pikachu',6,'В походе',True,True,False),(1,'Клевер',5,'',False,True,False),(3,'Venusaur',24,'',False,False,False)]
    own[-1]=(131,'Lapras',24,'',False,False,False)
    switch=c['switch']
    setpage('collection',title('Покедекс','Твоя маленькая команда.')+switch+filters()+'<div class="pc-pet-grid" data-filter-grid>'+''.join(card(*p) for p in own)+'</div><p class="pc-empty-filter" hidden>Никого не нашли. Попробуй другие фильтры.</p>')
    setpage('collection',pages['collection'][2].replace('<option value="date">По дате поимки</option>','<option value="date">По дате поимки</option><option value="care">По уходу для эволюции</option>').replace('Есть куда расти</label>','Есть куда расти</label><label><input type="checkbox" data-filter="duplicate">Дубли</label>'))
    def closed(sid,hint):
        return '<div class="pc-pet-card" data-closed="true" data-number="'+str(sid)+'"><span class="pc-card-top">#'+str(sid).zfill(4)+'</span>'+sprite(sid,hidden=True)+'<strong>Пока не встречали</strong><p class="pc-muted">'+hint+'</p></div>'
    species_cards=card(1,'Bulbasaur',species=True)+closed(2,'Развивается из #0001')+closed(3,'Развивается из #0002')+card(4,'Charmander',species=True)+closed(5,'Развивается из #0004')+closed(6,'Развивается из #0005')+card(7,'Squirtle',species=True)+card(133,'Eevee',species=True)
    species_cards+=card(25,'Pikachu',shiny=True,species=True)
    setpage('species',title('Все виды','Впереди ещё много открытий.')+switch+filters(True)+'<div class="pc-pet-grid" data-filter-grid>'+species_cards+'</div><p class="pc-empty-filter" hidden>Таких открытых видов пока нет.</p>')
    active_switch=switch.replace('aria-pressed="true"','aria-pressed="false"').replace('data-open="species"','aria-pressed="true" data-open="species"')
    setpage('species',pages['species'][2].replace(switch,active_switch))
    # The room remains unchanged; enlarge the independent pet around its foot anchor.
    old=pages['corner'][2]
    old=old.replace('Eevee рядом. Можно немного отдохнуть.','Eevee · #0133 · Нормальный · Необычный · Ур. 10')
    old=old.replace(meter('Уход','Всё хорошо',72),meter('Уход для эволюции','Подготовка к росту',72))
    old=old.replace('Подготовка к росту','')
    setpage('corner',old)
    evo=c['evo']
    inventory=panel('<h2>Помочь питомцу</h2><div class="v2-use-item"><img src="../final/assets/shop/care-booster.png" alt="Бустер ухода"><div><strong>Бустер ухода</strong><small>Приближает эволюцию · в запасе 2</small></div>'+action('Применить','data-item="care"','small')+'</div><div class="v2-use-item"><img src="../final/assets/shop/xp-potion.png" alt="Зелье опыта"><div><strong>Зелье опыта</strong><small>Добавляет 300 опыта · в запасе 1</small></div>'+action('Применить','data-item="xp"','small')+'</div>')
    detail=title('<span data-detail-name>Росток</span>','<span data-detail-species>Bulbasaur · #0001</span>',button(icon('monitor')+'На рабочий стол',kind='primary small'))+'<div class="pc-split v2-detail"><div><div data-detail-sprite>'+sprite(1,'Росток','large')+'</div><div class="pc-badges" data-detail-types>'+pills(1)+'</div><div class="pc-row between pc-section-title"><span>Эволюция</span>'+button('Условия','evolution',small=True)+'</div><div data-detail-evolution>'+evo+'</div>'+button('Ветви Eevee','evolution-branch',small=True)+'<div class="v2-fact"><h3>О виде</h3><p data-detail-fact>'+about(1)+'</p></div></div><div>'+meter('Уровень 12','До следующего — 94 опыта',56)+meter('Уход для эволюции','Подготовка к следующей форме',76)+c['traits']+inventory+'<div class="pc-divider pc-muted">Характер: спокойный · Пойман: 23 сентября 2026</div></div></div><div class="pc-footer between">'+button(icon('back')+'Мои покемоны','collection')+action('Найти новый дом · '+coins(13),'data-open="confirm-rehome"','danger small')+'</div>'
    setpage('detail',detail)
    setpage('detail',pages['detail'][2].replace('Подготовка к следующей форме',''))
    setpage('detail',pages['detail'][2].replace(button('Ветви Eevee','evolution-branch',small=True),''))
    eevee_branches=[134,135,136,196,197,470,471,700]
    branches='<div class="v2-branches">'+''.join('<button type="button" class="pc-pet-card" data-branch="'+str(sid)+'">'+sprite(sid,hidden=True)+'<span>#'+str(sid).zfill(4)+'</span></button>' for sid in eevee_branches)+'</div>'
    branch_page=title('Свой путь для Eevee','Можно довериться случаю или выбрать ветвь.',button('К питомцу','detail',small=True))+'<div class="pc-split"><div><div class="pc-segments"><button type="button" aria-pressed="true" data-evo-mode="random">Случайная ветвь</button><button type="button" aria-pressed="false" data-evo-mode="stone">Выбрать с камнем</button></div>'+branches+'</div><div>'+panel('<h2>Для эволюции</h2>'+meter('Уход для эволюции','Достаточно',100)+'<div class="pc-rewards">'+item('oran','× 6')+'<span data-stone-price hidden>'+item('evolution-stone','× 1')+'</span></div><p class="pc-muted" data-branch-copy>Форма станет сюрпризом.</p>'+action('Эволюционировать','data-open="confirm-evolution"','primary wide'))+'</div></div>'
    setpage('evolution-branch',branch_page,'Эволюция · выбор камнем','pokedex')
    # Every expedition member carries independent route affinity and trait information.
    for key,(label,tab,html) in list(pages.items()):
        if tab!='expeditions':continue
        replacements={'<small>Больше ягод</small>':'<small>Собиратель · больше ягод</small><span class="pc-badge grass" data-affinity>'+icon('leaf')+'Подходит для рощи</span>','<small>Подходит для рощи</small>':'<small>Ботаник · больше находок</small><span class="pc-badge grass" data-affinity>'+icon('leaf')+'Подходит для рощи</span>','<small>Больше находок</small>':'<small>Следопыт · больше встреч</small><span class="pc-badge grass" data-affinity>'+icon('leaf')+'Подходит для рощи</span>'}
        for old,new in replacements.items():html=html.replace(old,new)
        html=html.replace('Ботаник · больше находок','Кладоискатель · больше монет')
        setpage(key,html)
    def parent(sid,name,slot,editable):
        return '<div class="pc-slot-pet pc-parent-'+slot+'">'+sprite(sid,name)+'<div><strong data-parent-name="'+slot+'">'+name+'</strong><small>Ур. 12</small>'+ (action('Сменить','data-parent="'+slot+'"','small') if editable else '<small>В питомнике</small>')+'</div></div>'
    parents_edit='<div class="pc-two">'+parent(1,'Росток','first',True)+parent(152,'Chikorita','second',True)+'</div>'
    parents_busy='<div class="pc-two">'+parent(1,'Росток','first',False)+parent(152,'Chikorita','second',False)+'</div>'
    setpage('nursery',pages['nursery'][2].replace(c['parentcards'],parents_edit))
    setpage('nursery-running',title('В гнезде кто-то растёт','Можно закрыть приложение — время продолжит идти.')+'<div class="pc-split"><div>'+scene('assets/nursery-waiting.png')+'<div class="pc-section-title">Родители</div>'+parents_busy+'</div><div>'+panel('<h2>Осталось 24 минуты</h2>'+meter('До вылупления','Совсем скоро',70)+'<div class="pc-divider"><h2>Удача питомника</h2>'+c['luck']()+'<p class="pc-muted">Сохраняется между разными парами.</p></div>'+button('Вернуться в уголок','corner','primary wide'))+'</div></div>')
    setpage('nursery-ready',title('Пора встретить малыша!')+scene('assets/nursery-ready.png',cls='v2-ready-scene')+'<div class="v2-hatch-action">'+button(icon('egg')+'Вылупить','nursery-result','primary')+'</div>')
    setpage('tour-4',pages['tour-4'][2].replace(c['nest_scene'],scene('assets/nursery-waiting.png')))
    # Complete achievement progress, next tier and three real shelf selection controls.
    trophies=c['trophies'];goals=json.loads((base/'final/achievement-rewards.json').read_text())['chainGoals']
    byid={t['id']:t for t in c['manifest']['trophies']};goalmap={g['awardId']:g for g in goals}
    initial=['trophy-population-1','trophy-expeditions-1','trophy-care-1']
    shelf='<div class="pc-shelf">'+''.join('<div><img data-shelf-image="'+str(i)+'" src="'+c['assetroot']+byid[tid]['file']+'" alt="Выбранная награда"><div>Место '+str(i+1)+'</div>'+action('Заменить','data-shelf-slot="'+str(i)+'"','small')+'</div>' for i,tid in enumerate(initial))+'</div>'
    owned_ids=set(initial+['trophy-species-1','trophy-catches-1'])
    cards=[]
    for i,t in enumerate(trophies):
        earned=t['id'] in owned_ids
        current=t
        if t['category']=='chain':
            nextid=t['id'].rsplit('-',1)[0]+'-'+('2' if earned else '1')
            target=goalmap[nextid]['count'];value=min(target-1,max(0,target//2))
            condition=goalmap[nextid]['condition']
            next_text={'discovered-species':f'Открыть {target} видов','owned-individuals':f'Собрать {target} питомцев','successful-catches':f'Поймать {target} покемонов','claimed-expeditions':f'Получить награды за {target} походов','visited-regions':f'Побывать в {target} местностях','individuals-at-required-level':f'Вырастить {target} питомцев до уровня 10','first-care-evolution-ready':'Подготовить питомца к эволюции','distinct-hatched-root-species':f'Вылупить {target} разных корневых видов','distinct-species-transitions':f'Открыть {target} переходов эволюции','distinct-shiny-species':f'Открыть {target} shiny-видов'}.get(condition,c['conditions'][i])
            if t['chainId']=='population' and earned:value=9
            progress='<div class="pc-metric-label"><span>'+('Следующая ступень' if earned else 'До первой награды')+'</span><span>'+str(value)+' / '+str(target)+'</span></div><div class="pc-meter"><i style="width:'+str(value/target*100)+'%"></i></div><p>'+c['conditions'][i]+'</p>'
            progress=progress.replace('<p>'+c['conditions'][i]+'</p>','<p>'+next_text+'</p>')
        else:progress='<p>'+c['conditions'][i]+'</p>'
        pick=action('На полку','data-pick-award="'+t['id']+'"','small') if earned else ''
        cards.append('<article class="pc-trophy"><img src="'+c['assetroot']+current['file' if earned else 'maskFile']+'" '+('style="filter:brightness(0);opacity:.7"' if not earned else '')+' alt="'+('Заработанная награда' if earned else 'Закрытая награда')+'"><h3>'+c['titles'][i]+'</h3>'+progress+pick+'</article>')
    setpage('achievements',title('Наши маленькие победы','Три любимые награды будут рядом со спутником.',button('В уголок','corner',small=True))+shelf+'<div class="pc-trophy-grid">'+''.join(cards)+'</div>')
    # Catch UI exposes explicit activation and an actual throw/shake/open sequence.
    lucky='<div class="v2-lucky-control"><img src="../final/assets/shop/lucky-charm.png" alt="Талисман удачи"><div><strong>Талисман удачи</strong><p class="pc-muted" data-lucky-status>В запасе 2 · действует на следующую поимку</p>'+action('Активировать','data-activate-charm','small')+'</div></div>'
    visual='<div class="pc-result-visual v2-catch-stage"><img class="pc-ball" src="assets/ball-closed.png" alt="Закрытый покебол" data-animated-ball><span class="v2-catch-caption" data-catch-stage aria-live="polite">Кто же внутри?</span></div>'
    catch=title('Кто же внутри?','Два покебола ждут знакомства.',button('В уголок','corner',small=True))+'<div class="pc-result-layout">'+visual+'<div><h2>Ещё один новый друг</h2><p class="pc-muted">Открой покебол, когда будешь готов.</p>'+lucky+'<div style="margin-top:22px">'+action('Бросить покебол','data-throw','primary wide')+'</div></div></div>'
    setpage('catch',catch)
    setpage('catch-animation',catch.replace('Кто же внутри?</h1>','Маленькая встреча</h1>'),'Поимка · бросок и покачивания','corner')
    encounter=title('Кто-то рядом','Этого Pidgey команда встретила в Лесной роще.')+'<div class="pc-split"><div>'+scene('../final/assets/expeditions/grove.png','<img class="v2-wild-pet" src="../catalog-package/assets/normal/16.gif" alt="Встреченный Pidgey">')+'<div class="pc-center"><h2>Pidgey · #0016</h2><div class="pc-row" style="justify-content:center">'+pills(16)+'</div></div></div><div>'+panel('<h2>Кажется, он хочет познакомиться</h2>'+lucky+action('Бросить покебол','data-throw','primary wide')+'<div style="margin-top:12px">'+button('Вернуться позже','expedition','wide')+'</div>')+'</div></div>'
    setpage('encounter',encounter,'Встреча · знакомый вид из похода','expeditions')
    # Keep confirmation scenarios in the picker, but render them over their real context.
    contexts={'confirm-cancel':'expedition-running','confirm-restore':'save-history','confirm-rehome':'detail'}
    for key,context in contexts.items():setpage(key,pages[context][2],tab=pages[context][1])
    modal='<div class="v2-modal-layer" id="review-modal" hidden><div class="v2-modal" role="dialog" aria-modal="true" aria-labelledby="modal-title"><h2 id="modal-title"></h2><div id="modal-content"></div><div class="pc-footer" id="modal-actions"></div></div></div>'
    c['modal_markup']=modal
    facts={str(i):{'name':catalog[i]['displayName'],'types':pills(i),'fact':about(i),'canGrow':any(s.get('evolvesFromSpeciesId')==i for s in catalog.values()),'evolution':[s['id'] for s in catalog.values() if s.get('evolvesFromSpeciesId')==i]} for i in {p[0] for p in own}|{16}}
    awards=[{'id':byid[tid]['id'],'file':c['assetroot']+byid[tid]['file'],'name':c['titles'][next(i for i,t in enumerate(trophies) if t['id']==tid)],'size':byid[tid]['logicalSize'],'foot':byid[tid]['footAnchor']} for tid in initial+['trophy-species-1','trophy-catches-1']]
    c['revision_data']={'facts':facts,'awards':awards,'anchors':c['manifest']['shelf']['anchors']}
    # Buttons within the unmodified result screens stay as accepted by the user.
    return c
