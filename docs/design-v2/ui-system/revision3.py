"""Second user review: whole-screen prototype overrides, no game mutations."""
import json
from html import escape

def apply_revision(c):
    base, pages = c['BASE'], c['pages']
    title, button, sprite, panel, scene, meter, item, icon = [c[k] for k in ('title','button','sprite','panel','scene','meter','item','icon')]
    catalog = {s['id']: s for s in json.loads((base/'final/runtime-catalog.json').read_text())['species']}
    def setpage(key, html, label=None, tab=None):
        old = pages.get(key, (key, '', ''))
        pages[key] = (label or old[0], old[1] if tab is None else tab, html)
    def action(text, attrs='', kind=''):
        return '<button type="button" class="pc-button '+kind+'" '+attrs+'>'+text+'</button>'

    # Show the exact review example: two Bulbasaur, one ordinary and one shiny.
    own=pages['collection'][2]
    start=own.rfind('<button',0,own.index('data-name="Клевер"'))
    end=own.index('</button>',start)+len('</button>')
    card=own[start:end].replace('data-shiny="false"','data-shiny="true"').replace('/normal/1.gif','/shiny/1.gif')
    card=card.replace('</div></button>',c['badge']('Shiny','shiny','spark')+'</div></button>')
    setpage('collection',own[:start]+card+own[end:])

    # A form belongs to the species register; individual filters stay in My Pokemon.
    html = pages['species'][2]
    html = html.replace('<label><input type="checkbox" data-filter="shiny">Shiny</label>', '')
    form_switch = '<div class="pc-segments r3-form-switch" aria-label="Форма в покедексе">'+action('Обычные','data-dex-form="normal" aria-pressed="true"')+action('Shiny','data-dex-form="shiny" aria-pressed="false"')+'</div>'
    html = html.replace('<div class="pc-filters', form_switch+'<div class="pc-filters', 1)
    # First undiscovered base species in this fixture; click offers acquisition guidance.
    hint = '<div class="pc-pet-card" data-number="10" data-closed="true"><span class="pc-card-top">#0010</span>'+sprite(10,hidden=True)+'<strong class="species-unseen-label">Пока не встречали</strong><div class="species-find-action">'+action('Где найти?','data-acquire="10"','small')+'</div></div>'
    html = html.replace('</div><p class="pc-empty-filter"', hint+'</div><p class="pc-empty-filter"',1)
    setpage('species', html)

    # Keep the approved evolution composition for both linear and branching families.
    branches = [134,135,136,196,197,470,471,700]
    def crossroads(selectable=False):
        return '<div class="r3-crossroads"><div class="r3-root">'+sprite(133,'Eevee')+'<strong>Eevee</strong></div><div class="r3-forks" aria-label="Варианты эволюции">'+''.join(('<button type="button" data-branch="'+str(sid)+'" class="r3-branch">' if selectable else '<div class="r3-branch">')+sprite(sid,hidden=True)+'<small>#'+str(sid).zfill(4)+'</small>'+('</button>' if selectable else '</div>') for sid in branches)+'</div></div>'
    ordinary = pages['evolution'][2].replace('Уход</span>','Уход для эволюции</span>')
    ordinary = ordinary.replace('Эволюционировать</button>','Эволюционировать</button>')
    stone_choice=action('<img src="../final/assets/shop/evolution-stone.png" alt=""><span><strong>Камень эволюции</strong><small>Выбрать будущую форму · в запасе 1</small></span><span class="capture-choice-mark" aria-hidden="true">'+icon('check')+'</span>','data-use-stone aria-pressed="false"','capture-stone-choice')
    branch = title('Время расти','Eevee готов к новой форме.',button(icon('back')+'К питомцу','detail',small=True))+crossroads(True)+'<div class="pc-two">'+panel('<h2>Забота помогла ему вырасти</h2>'+meter('Уход для эволюции','Достаточно',100)+'<p class="pc-muted">Уровень, кличка и особенности сохранятся.</p>')+panel('<h2>Понадобится</h2><div class="pc-rewards">'+item('oran','× 6')+'<span data-stone-price hidden>'+item('evolution-stone','× 1')+'</span></div>'+stone_choice+'<button type="button" data-evo-mode="random" aria-pressed="true" hidden></button><button type="button" data-evo-mode="stone" aria-pressed="false" hidden></button><p data-branch-copy class="pc-muted">Форма станет сюрпризом.</p>')+'</div><div class="pc-footer">'+button('Позже','detail')+action('Эволюционировать','data-open="confirm-evolution"','primary')+'</div>'
    setpage('evolution',ordinary)
    setpage('evolution-branch',branch,'Эволюция · варианты и камень')

    for key in ['nursery-running']:
        setpage(key,pages[key][2].replace('<small>В питомнике</small>',''))
    hatch = title('Сейчас он появится…')+scene('assets/nursery-ready.png','<span class="r3-hatch-glow" aria-hidden="true"></span>',cls='v2-ready-scene r3-hatch-scene')+'<p class="pc-center" role="status">Скорлупа трескается…</p>'
    setpage('nursery-hatching',hatch,'Питомник · появление малыша','nursery')
    setpage('nursery-ready',pages['nursery-ready'][2].replace('data-open="nursery-result"','data-open="nursery-hatching"'))

    raw_recipes=json.loads((base/'nursery-recipes-draft.json').read_text())
    recipes=[]
    for r in raw_recipes['recipes']:
        if not r['active']:continue
        outcomes=list(dict.fromkeys(catalog[o['speciesId']]['familyRootId'] for o in r['outcomes'] if o.get('active') and catalog[o['speciesId']]['normalAvailable']))
        recipes.append({'parents':r['parentIds'],'outcomes':outcomes,'berries':r['baseBerries'],'hours':r.get('plannedHours',r['hours']),'selectionUseful':r['selectionUseful'],'surcharge':r['selectedSurcharge']})
    for r in raw_recipes['sameSpeciesRecipes']:
        if r['normalAvailable']:recipes.append({'parents':[r['speciesId'],r['speciesId']],'outcomes':[catalog[r['outcomeSpeciesId']]['familyRootId']],'berries':r['baseBerries'],'hours':r.get('plannedHours',r['hours']),'selectionUseful':False,'surcharge':{}})
    available=[s for s in catalog.values() if s['normalAvailable'] and (base/'catalog-package/assets/normal'/f"{s['id']}.gif").exists()][:600]
    assert len(available)==600
    fixture=[]
    for i,s in enumerate(available+available[:200]):
        name={1:'Росток' if i<600 else 'Клевер',152:'Chikorita' if i<600 else 'Листик'}.get(s['id'],s['displayName'])
        fixture.append({'key':str(i),'sid':s['id'],'name':name,'level':5+i%30,'shiny':i>=600 and s['shinyAvailable'],'busy':i%29==20})
    assert len(fixture)==800 and len({x['sid'] for x in fixture})==600
    data={str(s['id']):{'name':s['displayName'],'types':s['types'],'rarity':s['rarity'],'root':s['familyRootId']} for s in catalog.values()}
    c['revision_data']['round3']={'species':data,'parents':fixture,'recipes':recipes,'crossroads':crossroads(False),'luck':c['luck']()}
    chooser=title('Первый родитель','800 питомцев · одинаковые виды собраны вместе.',button('К гнезду','nursery',small=True))+'<div class="pc-filters"><input type="search" data-parent-search placeholder="Имя, кличка или номер" aria-label="Поиск родителя"><select data-parent-rarity aria-label="Редкость родителя"><option value="">Любая редкость</option><option value="Common">Обычный</option><option value="Uncommon">Необычный</option><option value="Rare">Редкий</option><option value="Legendary">Легендарный</option></select><label><input type="checkbox" data-parent-shiny>Shiny</label></div><p data-parent-summary class="pc-muted"></p><div class="pc-pet-grid r3-parent-grid" data-parent-groups></div><div class="pc-row between r3-pagination">'+action('Назад','data-parent-page="-1"')+'<span data-parent-page-label></span>'+action('Дальше','data-parent-page="1"')+'</div>'
    setpage('nursery-select',chooser,'Питомник · первый выбор из 800','nursery')
    prep=title('Маленькие открытия','Выбери родителей — мы покажем, кто может появиться.')+'<div class="pc-split"><div>'+scene('assets/nursery.png')+'<div class="pc-section-title">Родители</div><div class="pc-two" data-r3-parents></div></div><div>'+panel('<div data-r3-offspring></div>')+'</div></div>'
    setpage('nursery',prep)

    # Acquisition information uses the same prepared recipes and route type pools.
    balance=json.loads((base/'final/balance.json').read_text())
    routes={'grove':'Лесная роща','lake':'Тихое озеро','coast':'Морское побережье','mountains':'Горные тропы','desert':'Пустынные дюны','volcano':'Вулкан','snow':'Снежный перевал','ruins':'Древние руины'}
    c['revision_data']['round3']['locations']=[{'name':routes[r['id']],'types':r['bonusTypes']} for r in balance['locations']]
    # The selected cinematic takes place on one scene; no extracted third-party art.
    stars=''.join('<span class="capture-star capture-star-'+str(i)+'" aria-hidden="true">'+icon('star')+'</span>' for i in range(3))
    layers='<span class="capture-shadow" aria-hidden="true"></span><img class="capture-pet" data-capture-pet src="../catalog-package/assets/normal/16.gif" alt="Встреченный покемон"><span class="capture-beam" aria-hidden="true"></span><img class="capture-ball" data-capture-ball src="assets/ball-closed.png" alt="Покебол"><span class="capture-click" aria-hidden="true"></span>'+stars
    capture=title('Новая встреча','Немного волнения — и ещё один друг рядом.')+scene('../final/assets/expeditions/grove.png',layers,cls='capture-scene')+'<div class="capture-footer"><p data-capture-stage role="status" aria-live="polite">Покебол летит…</p>'+action('К результату','data-capture-skip','small')+'</div>'
    setpage('catch-animation',capture,'Поимка · полная сцена','corner')
    setpage('catch-reduced',capture,'Поимка · уменьшение движения','corner')
    facts=json.loads((c['OUT']/'flavor-facts.json').read_text(encoding='utf-8'))
    curated={str(f['speciesId']):f['textRu'] for f in facts['facts']}
    old_fact=c['revision_data']['facts']['1']['fact']
    for sid,value in c['revision_data']['facts'].items():
        assert sid in curated, sid
        value['fact']=curated[sid]
    setpage('detail',pages['detail'][2].replace(old_fact,escape(curated['1'])))
    return c
