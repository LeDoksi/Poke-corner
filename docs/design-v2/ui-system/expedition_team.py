"""Expedition team selection storyboard; individual identities and existing route contracts."""
import json

def apply_revision(c):
    pages=c['pages'];title,button,panel=c['title'],c['button'],c['panel']
    def setpage(key,html,label=None,tab='expeditions'):
        old=pages.get(key,(key,tab,''));pages[key]=(label or old[0],tab,html)
    def action(label,attrs='',kind=''):
        return '<button type="button" class="pc-button '+kind+'" '+attrs+'>'+label+'</button>'
    balance=json.loads((c['BASE']/'final/balance.json').read_text())
    traits=[
      ('gatherer','Собиратель','Больше ягод'),('treasure-hunter','Кладоискатель','Больше монет'),
      ('tracker','Следопыт','Больше встреч'),('finder','Искатель','Больше находок'),
      ('mentor','Наставник','Больше опыта команде'),('collector','Коллекционер','Больше новых видов'),
      ('relic-finder','Искатель реликвий','Больше особых предметов'),('terrain-expert','Знаток местности','Быстрее проходит маршрут'),
      ('fast-grower','Быстрорастущий','Быстрее готовится к эволюции'),('rarity-expert','Знаток редкостей','Помогает встретить редкий вид'),
      ('caring-parent','Заботливый родитель','Ускоряет ожидание в питомнике')]
    assert {t[0] for t in traits}==set(balance['traits'])
    members=[]
    for i,p in enumerate(c['revision_data']['round3']['parents']):
        member=dict(p,trait=traits[i%len(traits)][0],desktop=p['sid']==133 and i<600)
        if i<600 and p['sid'] in (1,152,43):
            member['level']={1:12,152:14,43:12}[p['sid']]
            member['trait']={1:'gatherer',152:'treasure-hunter',43:'tracker'}[p['sid']]
        if i<600 and p['sid'] in (4,7,25,133,131):
            member['level']={4:8,7:9,25:6,133:10,131:24}[p['sid']]
            if p['sid']==4:member['name']='Огонёк'
            if p['sid'] in (7,25):member['busy']=True
            if p['sid']==25:member['shiny']=True
        if i==600:member.update(busy=False,name='Клевер',shiny=True)
        member['busyReason']='В питомнике' if p['sid']==7 and i<600 else 'В походе'
        member['traits']=[member['trait']]
        if member['level']>=balance['secondTraitLevel']:
            member['traits'].append(next(t[0] for t in traits if t[0]!=member['trait']))
        members.append(member)
    c['revision_data']['team']={'members':members,'traits':[{'id':k,'name':n,'effect':e} for k,n,e in traits],
      'locations':balance['locations'],'recommendedLevels':balance['recommendedLevels'],
      'demoKeys':[next(p['key'] for p in members if p['sid']==sid and int(p['key'])<600) for sid in (1,152,43)]}
    terms=c['terms'].replace('data-open="expedition-running"','data-expedition-start data-open="expedition-running"')
    # Hide example amounts until there is a selected team. The game service owns live estimates.
    terms=terms.replace('Полная команда — могут быть редкие находки.','<span data-team-note>Собери команду для первого похода.</span>')
    terms=terms.replace('<h2>Привезут с собой</h2>','<h2>Привезут с собой</h2><p class="pc-muted" data-team-estimate-empty>Выбери питомцев — здесь появятся находки и опыт.</p><div data-team-estimate>')
    terms=terms.replace('<div style="margin-top:18px">','</div><div style="margin-top:18px">')
    prep=title('Куда отправимся?','Выбери маршрут и собери команду.')+'<div class="pc-split"><div>'+c['routes']+c['exp_scene']+'<div class="pc-row between pc-section-title"><span>Команда</span><span class="pc-muted" data-team-count></span></div><div data-current-team></div><div class="pc-row between" style="margin-top:12px"><span class="pc-muted">Спутник останется на рабочем столе.</span>'+action('Выбрать команду','data-select-team','small')+'</div></div><div>'+terms+'</div></div>'
    setpage('expedition',prep,'Экспедиция · первая команда')
    setpage('expedition-selected',prep,'Экспедиция · команда выбрана')
    filters='<div class="team-filters"><input type="search" data-team-search placeholder="Имя, кличка или номер" aria-label="Поиск участника"><select data-team-type aria-label="Тип участника"></select><select data-team-trait aria-label="Особенность участника"><option value="">Любая особенность</option>'+''.join('<option value="'+k+'">'+n+'</option>' for k,n,_ in traits)+'</select></div><div class="team-filter-row"><label><input type="checkbox" data-team-available checked>Доступные</label><label><input type="checkbox" data-team-matching>Подходят маршруту</label><select data-team-sort aria-label="Сортировка команды"><option value="recommended">Рекомендованные</option><option value="level">Сначала высокий уровень</option><option value="number">По номеру</option></select>'+action('Сбросить','data-team-reset','small')+'</div>'
    selection=title('Соберём команду','',action('Отмена','data-team-cancel','small'))+'<p class="pc-muted team-route" data-team-route></p><div class="team-selected" data-team-slots></div>'+filters+'<p class="pc-muted team-search-status" data-team-search-status></p><div class="team-grid-wrap"><div class="team-grid" data-team-grid></div></div><div class="team-bottom"><div class="pc-row">'+action('Назад','data-team-page="-1"','small')+'<span data-team-page-label></span>'+action('Дальше','data-team-page="1"','small')+'</div><span role="status" data-team-selection-status></span>'+action('Готово','data-team-save','primary')+'</div>'
    setpage('expedition-team',selection,'Экспедиция · подбор команды')
    return c
