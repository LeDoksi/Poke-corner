from pathlib import Path
from html import escape
from html.parser import HTMLParser
import hashlib
import json
import shutil
from PIL import Image

ROOT = Path(__file__).resolve().parents[3]
BASE = ROOT / 'docs/design-v2'
OUT = BASE / 'ui-system'
SOURCE = Path(__file__).parent
OUT.mkdir(exist_ok=True)
(OUT / 'assets').mkdir(exist_ok=True)
generated = Path(r'C:\Users\Georgy\.codex\generated_images\01a12cd0-80d3-74f2-bfa3-1a085d54d6b1')
art = {'nursery':'exec-8d36320b-4748-40cf-bda2-b9b5916a4a58.png', 'egg':'exec-97e4645c-8a36-40ea-b610-cf76bcdd0962.png', 'ball-closed':'exec-f65453a7-ea9e-453f-ab39-1d1c82e75d60.png', 'ball-open':'exec-2cd5e9da-e586-4c2f-82cd-a937412fc96e.png'}
proof = []
for key, name in art.items():
    raw = (OUT / 'assets' / (key + '.png')).read_bytes()
    target = OUT / 'assets' / (key + '.png')
    if target.exists():
        assert target.read_bytes() == raw, 'Refusing an unrequested overwrite'
    else:
        shutil.copyfile(generated / name, target)
    with Image.open(target) as im:
        width, height = im.size
        if key != 'nursery':
            assert im.mode == 'RGBA' and im.getchannel('A').getextrema() == (0, 255), key
    proof.append({'id':key,'file':'assets/'+key+'.png','sourceImage':name,'bytes':len(raw),'sha256':hashlib.sha256(raw).hexdigest(),'width':width,'height':height,'approval':'approved-by-user-2026-10-16','runtimeRegistered':False})
(OUT/'art-provenance.json').write_text(json.dumps({'tool':'built-in image_gen','assets':proof,'prompts':json.loads((SOURCE/'poke57-art-prompts.json').read_text(encoding='utf-8'))},ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
assert (OUT/'review.css').is_file()

ICONS = {
'ball':'<circle cx="12" cy="12" r="9"/><path d="M3 12h6m6 0h6"/><circle cx="12" cy="12" r="3"/>',
'home':'<path d="m3 10 9-7 9 7M5 9v12h14V9M9 21v-8h6v8"/>',
'book':'<path d="M12 5v16M3 4c4-1 6 0 9 2 3-2 5-3 9-2v15c-4-1-6 0-9 2-3-2-5-3-9-2Z"/>',
'map':'<path d="m3 6 6-3 6 3 6-3v15l-6 3-6-3-6 3ZM9 3v15m6-12v15"/>',
'egg':'<path d="M19 15c0 5-3 7-7 7s-7-2-7-7S9 2 12 2s7 8 7 13Z"/><path d="m9 11 2 2m4 3 1-2"/>',
'shop':'<path d="M4 10v11h16V10M3 4h18l1 6H2ZM9 21v-7h6v7M7 4l-1 6m6-6v6m5-6 1 6"/>',
'gear':'<circle cx="12" cy="12" r="3"/><path d="m9 3 1-1h4l1 3 3 1 3 1v4l-2 1-1 3 1 3-3 3-3-1-3 1-2 1-3-3 1-3-1-3-2-2 1-4 3-1Z"/>',
'coin':'<circle cx="12" cy="12" r="9"/><circle cx="12" cy="12" r="6"/><path d="m9 15 6-6"/>',
'star':'<path d="m12 2 3 6 7 1-5 5 1 7-6-3-6 3 1-7-5-5 7-1Z"/>',
'heart':'<path d="M12 21 3 12C-2 6 7-1 12 6c5-7 14 0 9 6Z"/>',
'clock':'<circle cx="12" cy="12" r="9"/><path d="M12 6v6l4 2"/>',
'check':'<path d="m4 12 5 5L20 6"/>',
'plus':'<path d="M12 4v16M4 12h16"/>',
'arrow':'<path d="M4 12h16m-6-6 6 6-6 6"/>',
'back':'<path d="M20 12H4m6-6-6 6 6 6"/>',
'leaf':'<path d="M21 3C9 2 1 9 5 16c7 7 16-2 16-13ZM4 21 17 7"/>',
'lock':'<rect x="5" y="10" width="14" height="12" rx="2"/><path d="M8 10V6a4 4 0 0 1 8 0v4m-4 5v3"/>',
'spark':'<path d="m12 2 3 7 7 3-7 3-3 7-3-7-7-3 7-3Zm8 0v4m-2-2h4"/>',
'volume':'<path d="m3 9 4 0 5-5v16l-5-5H3Zm13-3c4 3 4 9 0 12m-1-9c2 1 2 5 0 6"/>',
'monitor':'<rect x="2" y="3" width="20" height="14" rx="2"/><path d="M12 17v4m-5 0h10"/>',
'close':'<path d="m5 5 14 14M19 5 5 19"/>',
'gift':'<rect x="3" y="8" width="18" height="5" rx="1"/><path d="M5 13v9h14v-9m-7-5v14M12 8C1 8 6-3 12 8Zm0 0c11 0 6-11 0 0Z"/>',
'search':'<circle cx="10" cy="10" r="7"/><path d="m15 15 6 6"/>',
'minus':'<path d="M4 12h16"/>',
}
def icon(key):
    if key=='coin':return '<img class="pc-coin" src="assets/coin.png" alt="Монеты">'
    assert key in ICONS, key
    return '<svg class="i" aria-hidden="true" viewBox="0 0 24 24">'+ICONS[key]+'</svg>'
def button(text, page=None, kind='', small=False, disabled=False):
    return '<button type="button" class="pc-button '+kind+(' small' if small else '')+'"'+(' data-open="'+page+'"' if page else '')+(' disabled' if disabled else '')+'>'+text+'</button>'
def sprite(sid, name='', kind='', shiny=False, hidden=False):
    return '<img class="pc-sprite '+kind+(' silhouette' if hidden else '')+'" src="../catalog-package/assets/'+('shiny' if shiny else 'normal')+'/'+str(sid)+'.gif" alt="'+('Неизвестный вид' if hidden else escape(name))+'">'
def badge(text, kind='', glyph=None):
    return '<span class="pc-badge '+kind+'">'+(icon(glyph) if glyph else '')+text+'</span>'
def title(text, sub='', action=''):
    return '<div class="pc-title"><div><h1>'+text+'</h1>'+('<p>'+sub+'</p>' if sub else '')+'</div>'+action+'</div>'
def item(item_id, count):
    label={'treats':'Лакомства','rawst':'Ягода Rawst','pecha':'Ягода Pecha','oran':'Ягода Oran','offspring-charm':'Талисман гнезда'}.get(item_id,item_id)
    return '<span class="pc-item"><img src="../final/assets/shop/'+item_id+'.png" alt="'+label+'">'+str(count)+'</span>'
def coins(count):
    return '<span class="pc-item">'+icon('coin')+str(count)+'</span>'
def meter(label,value,width=55):
    return '<div class="pc-meter-block"><div class="pc-metric-label"><span>'+label+'</span><span>'+value+'</span></div><div class="pc-meter"><i style="width:'+str(width)+'%"></i></div></div>'
def pet(sid,name,level=12,kind='grass',status='Свободен',selected=False,shiny=False):
    return '<button type="button" class="pc-pet-card '+('selected' if selected else '')+'" data-open="detail"><span class="pc-card-top"><span>#'+str(sid).zfill(4)+'</span>'+icon('spark' if shiny else 'heart')+'</span>'+sprite(sid,name,shiny=shiny)+'<div class="pc-row between"><span class="pc-name">'+name+'</span><span class="pc-muted">Ур. '+str(level)+'</span></div><div class="pc-badges">'+badge({'grass':'Трава','fire':'Огонь','water':'Вода','electric':'Электро','':'Нормальный'}.get(kind,'Нормальный'),kind)+badge(status,'busy' if status!='Свободен' else '')+'</div></button>'
def member(sid,name,level,effect):
    return '<div class="pc-slot-pet">'+sprite(sid,name)+'<div><strong>'+name+'</strong><small>Ур. '+str(level)+'</small><small>'+effect+'</small></div></div>'
def note(text,kind='',glyph='leaf'):
    return '<div class="pc-note '+kind+'">'+icon(glyph)+'<span>'+text+'</span></div>'
def scene(asset,over='',cls=''):
    return '<div class="pc-scene '+cls+'"><img src="'+asset+'" alt="'+('Питомник' if 'nursery' in asset else 'Игровая сцена')+'">'+over+'</div>'
def panel(text):
    return '<div class="pc-panel">'+text+'</div>'
def luck():
    return '<div class="pc-luck" role="img" aria-label="Удача питомника растёт">'+''.join('<span class="'+('filled' if i<3 else '')+'"></span>' for i in range(6))+'</div>'
pages={}
groups={}
def add(key,label,tab,html):
    assert key not in pages
    pages[key]=(label,tab,html)

manifest=json.loads((BASE/'art-production/last-attempt/final-assets/manifest.json').read_text())
assetroot='../art-production/last-attempt/final-assets/'
room=assetroot+manifest['room']['file']
roomlayers=''
for tid,(x,y) in zip(['trophy-population-1','trophy-expeditions-1','trophy-care-1'],manifest['shelf']['anchors']):
    t=next(t for t in manifest['trophies'] if t['id']==tid)
    w,h=t['logicalSize'];ax,ay=t['footAnchor']
    roomlayers+='<img src="'+assetroot+t['file']+'" alt="Заработанная награда" style="position:absolute;left:'+str((x-w*ax)/1448*100)+'%;top:'+str((y-h*ay)/582*100)+'%;width:'+str(w/1448*100)+'%;height:'+str(h/582*100)+'%">'
lip=manifest['frontlip'];lx,ly,rx,by=lip['rect']
roomlayers+='<img src="'+assetroot+lip['file']+'" alt="" style="position:absolute;left:'+str(lx/1448*100)+'%;top:'+str(ly/582*100)+'%;width:'+str((rx-lx)/1448*100)+'%;height:'+str((by-ly)/582*100)+'%"><img class="pc-room-pet" src="../catalog-package/assets/normal/133.gif" alt="Eevee">'
add('corner','Уголок','corner',title('Мой уголок','Eevee рядом. Можно немного отдохнуть.',button(icon('star')+'Достижения','achievements'))+scene(room,roomlayers,'pc-room')+'<div class="pc-three" style="margin-top:18px">'+meter('Уход','Всё хорошо',72)+meter('Следующий покебол','Уже скоро',63)+meter('Лакомства','12 в запасе',80)+'</div><div class="pc-footer">'+button(icon('heart')+'Погладить')+button(item('treats','')+'Покормить',kind='primary')+'</div>')
collection=[(1,'Росток',12,'grass','Свободен'),(4,'Огонёк',8,'fire','Свободен'),(7,'Squirtle',9,'water','В питомнике'),(133,'Eevee',10,'','На столе'),(43,'Oddish',12,'grass','Свободен'),(152,'Chikorita',14,'grass','Свободен'),(25,'Pikachu',6,'electric','В походе'),(1,'Клевер',5,'grass','Свободен')]
filters='<div class="pc-filters"><input type="search" placeholder="Имя, кличка или номер" aria-label="Поиск питомца"><select aria-label="Сортировка"><option>По номеру</option><option>По уровню</option><option>По дате поимки</option></select><select aria-label="Тип"><option>Все типы</option><option>Трава</option><option>Вода</option></select>'+button(icon('heart')+'Избранные',small=True)+'</div>'
switch='<div class="pc-segments"><button type="button" aria-pressed="true" data-open="collection">Мои покемоны</button><button type="button" data-open="species">Все виды</button></div>'
add('collection','Покедекс · мои','pokedex',title('Покедекс','Твоя маленькая команда.')+switch+filters+'<div class="pc-pet-grid">'+''.join(pet(*x,selected=i==0) for i,x in enumerate(collection))+'</div>')
unknown='<div class="pc-pet-card"><span class="pc-card-top">#0002</span>'+sprite(2,hidden=True)+'<strong>Пока не встречали</strong><p class="pc-muted">Лесная роща</p></div>'
add('species','Покедекс · все виды','pokedex',title('Все виды','Впереди ещё много открытий.')+switch+filters+'<div class="pc-pet-grid">'+pet(1,'Bulbasaur')+unknown+unknown.replace('#0002','#0003').replace('/2.gif','/3.gif')+pet(4,'Charmander',8,'fire')+unknown.replace('#0002','#0005').replace('/2.gif','/5.gif')+unknown.replace('#0002','#0006').replace('/2.gif','/6.gif')+pet(7,'Squirtle',9,'water')+pet(133,'Eevee',10,'')+'</div>')
traits='<div class="pc-detail-traits"><div class="pc-trait"><img src="../final/assets/shop/rawst.png" alt=""><div><h3>Собиратель</h3><p>Приносит больше ягод из экспедиций.</p></div></div><div class="pc-trait">'+icon('leaf')+'<div><h3>Подходит для Лесной рощи</h3><p>Помогает команде собрать больше добычи.</p></div></div></div>'
evo='<div class="pc-evolution"><div>'+sprite(1,'Bulbasaur')+'<small>Bulbasaur</small></div>'+icon('arrow')+'<div>'+sprite(2,hidden=True)+'<small>#0002</small></div>'+icon('arrow')+'<div>'+sprite(3,hidden=True)+'<small>#0003</small></div></div>'
add('detail','Личная карточка','pokedex',title('Росток','Bulbasaur · #0001',button(icon('back')+'Мои покемоны','collection',small=True))+'<div class="pc-split"><div>'+sprite(1,'Росток','large')+'<div class="pc-row" style="justify-content:center">'+badge('Трава','grass','leaf')+badge('Яд','poison')+badge('Необычный','','star')+'</div><div class="pc-section-title">Эволюция</div>'+evo+'</div><div>'+note('Свободен',glyph='check')+meter('Уровень 12','До следующего — 94 опыта',56)+meter('Уход','Готов к новым открытиям',76)+traits+'<div class="pc-divider pc-muted">Характер: спокойный<br>Пойман: 23 сентября 2026</div></div></div><div class="pc-footer">'+button('Условия эволюции','evolution')+button(icon('monitor')+'На рабочий стол',kind='primary')+'</div>')
add('evolution','Эволюция','pokedex',title('Время расти','Росток готов к новой форме.',button(icon('back')+'К питомцу','detail',small=True))+evo+'<div class="pc-two">'+panel('<h2>Забота помогла ему вырасти</h2>'+meter('Уход','Достаточно',100)+'<p class="pc-muted">Уровень, кличка и особенности сохранятся.</p>')+panel('<h2>Понадобится</h2><div class="pc-rewards">'+item('rawst','× 6')+'</div>'+note('Всё готово к эволюции',glyph='check'))+'</div><div class="pc-footer">'+button('Позже','detail')+button('Эволюционировать',kind='primary')+'</div>')
route_ids=['grove','lake','coast','mountains','desert','volcano','snow','ruins']
route_names=['Роща','Озеро','Море','Горы','Пустыня','Вулкан','Снег','Руины']
routes='<div class="pc-routes">'+''.join('<button type="button" data-route="'+i+'" aria-pressed="'+str(i=='grove').lower()+'">'+n+'</button>' for i,n in zip(route_ids,route_names))+'</div>'
team='<div class="pc-three">'+member(1,'Росток',12,'Больше ягод')+member(152,'Chikorita',14,'Подходит для рощи')+member(43,'Oddish',12,'Больше находок')+'</div>'
exp_scene=scene('../final/assets/expeditions/grove.png','<span class="pc-scene-label" data-route-title>Лесная роща</span>','pc-exp-scene')
terms=panel('<h2>Сложность</h2><div class="pc-segments"><button type="button">Лёгкая</button><button type="button" aria-pressed="true">Обычная</button><button type="button">Сложная</button></div><h2 style="margin-top:20px">Длительность</h2><div class="pc-segments"><button type="button">30 мин</button><button type="button" aria-pressed="true">1,5 ч</button><button type="button">3 ч</button><button type="button">6 ч</button></div>'+note('Полная команда — могут быть редкие находки.')+'<div class="pc-divider"><h2>Привезут с собой</h2><div class="pc-rewards">'+item('rawst','× 3')+coins(13)+'</div><p class="pc-muted" style="margin-top:10px">Каждому — 150 опыта</p></div><div style="margin-top:18px">'+button(icon('map')+'Отправить на 1,5 часа','expedition-running','primary wide')+'</div>')
add('expedition','Экспедиция · подготовка','expeditions',title('Куда отправимся?','Выбери маршрут и собери команду.')+'<div class="pc-split"><div>'+routes+exp_scene+'<div class="pc-section-title">Команда</div>'+team+'<div class="pc-row between" style="margin-top:12px"><span class="pc-muted">Спутник останется на рабочем столе.</span>'+button('Изменить команду',small=True)+'</div></div><div>'+terms+'</div></div>')
for ready in [False,True]:
    key='expedition-ready' if ready else 'expedition-running'
    right=panel('<h2>'+('Команда вернулась!' if ready else 'В пути')+'</h2>'+('<p>Находки уже ждут тебя.</p>' if ready else '<h1 style="font-size:32px;margin:8px 0">Осталось 48 минут</h1>'+meter('Лесная роща','1,5 часа',47))+'<p class="pc-muted">'+('Награды не сгорят.' if ready else 'Можно закрыть приложение — поход продолжится.')+'</p><div class="pc-divider"><h2>Ожидаемая добыча</h2><div class="pc-rewards">'+item('rawst','× 3')+coins(13)+'</div></div><div style="margin-top:22px">'+button('Забрать находки' if ready else 'Вернуться в уголок','expedition-result' if ready else 'corner','primary wide')+'</div>'+('' if ready else '<div style="margin-top:14px">'+button('Прервать поход','confirm-cancel','danger wide')+'</div>'))
    add(key,'Экспедиция · '+('готова' if ready else 'в пути'),'expeditions',title('С возвращением!' if ready else 'Маленькое приключение','Лесная роща · обычная сложность')+'<div class="pc-split"><div>'+exp_scene+'<div class="pc-section-title">Твоя команда</div>'+team+'</div><div>'+right+'</div></div>')
add('expedition-result','Экспедиция · результат','expeditions',title('Хороший поход!','Находки уже у тебя.')+'<div class="pc-split"><div>'+exp_scene+'<div class="pc-section-title">Каждому участнику — 150 опыта</div>'+team+'</div><div>'+panel('<h2>Привезли с собой</h2><div class="pc-rewards">'+item('rawst','× 3')+coins(13)+'</div><div class="pc-divider"><h2>Ещё кое-что нашли</h2><div class="pc-rewards">'+item('oran','× 1')+'</div></div>'+note('В этот раз без новой встречи.')+button('Продолжить','expedition','primary wide'))+'</div></div>')
nursery_data=json.loads((BASE/'nursery-recipes-draft.json').read_text())
recipe=next((r for r in nursery_data['recipes'] if set(r['parentIds'])=={1,43}),nursery_data['recipes'][0])
assert len(recipe['outcomes']) in (2,3)
parents=recipe['parentIds']
names={1:'Росток',43:'Oddish',152:'Chikorita',114:'Tangela',187:'Hoppip',798:'Kartana'}
parentcards='<div class="pc-two">'+''.join(member(p,names.get(p,'Питомец'),12,'Выбрать другого') for p in parents)+'</div>'
outcome_cards='<div class="pc-three pc-outcomes">'+''.join('<button type="button" class="pc-pet-card" data-outcome="'+str(i)+'">'+sprite(r['speciesId'],hidden=i>0)+'<span class="pc-muted">'+(names.get(r['speciesId'],'Открытый вид') if i==0 else '#'+str(r['speciesId']).zfill(4))+'</span></button>' for i,r in enumerate(recipe['outcomes']))+'</div>'
nursery_scene=scene('assets/nursery.png')
nest_scene=scene('assets/nursery.png','<img class="pc-egg" src="assets/egg.png" alt="Яйцо в гнезде">')
nest_cost='<div class="pc-rewards">'+''.join(item(k,'× '+str(v)) for k,v in recipe['baseBerries'].items())+'</div>'
nest_options=panel('<h2>Кто может появиться</h2>'+outcome_cards+'<p class="pc-muted" style="margin-top:8px">Один из малышей — сюрприз.</p><label class="pc-charm-choice"><img src="../final/assets/shop/offspring-charm.png" alt=""><span><strong>Выбрать малыша</strong><small>С талисманом гнезда</small></span><input type="checkbox" id="offspring-choice"></label><h2 style="margin-top:14px">Удача питомника</h2>'+luck()+'<p class="pc-muted">Обычные вылупления приближают shiny.</p><div class="pc-divider" data-nest-price>'+nest_cost+'</div><p class="pc-muted" data-nest-time>Ожидание — '+str(recipe['hours'])+' часов</p>'+button(icon('egg')+'Подготовить гнездо','nursery-running','primary wide'))
add('nursery','Питомник · подготовка','nursery',title('Маленькие открытия','У каждой пары — своя история.')+'<div class="pc-split"><div>'+nursery_scene+'<div class="pc-section-title">Родители</div>'+parentcards+note('Эта пара совместима',glyph='heart')+'<div class="pc-row between"><span class="pc-muted">Особенности малышу достанутся от родителей.</span>'+badge('Собиратель','','leaf')+'</div></div><div>'+nest_options+'</div></div>')
for ready in [False,True]:
    add('nursery-ready' if ready else 'nursery-running','Питомник · '+('готово' if ready else 'ожидание'),'nursery',title('Пора познакомиться!' if ready else 'В гнезде кто-то растёт','Родители рядом, малыш в безопасности.')+'<div class="pc-split"><div>'+nest_scene+'<div class="pc-section-title">Родители</div>'+parentcards+'</div><div>'+panel('<h2>'+('Яйцо покачивается' if ready else 'Осталось 24 минуты')+'</h2>'+meter('До встречи','Совсем скоро',95 if ready else 70)+'<p class="pc-muted">Можно закрыть приложение — время продолжит идти.</p><div class="pc-divider"><h2>Удача питомника</h2>'+luck()+'<p class="pc-muted">Сохраняется между разными парами.</p></div><div style="margin-top:24px">'+button('Встретить малыша' if ready else 'Вернуться в уголок','nursery-result' if ready else 'corner','primary wide')+'</div>')+'</div></div>')
child=recipe['outcomes'][0]['speciesId']
add('nursery-result','Питомник · малыш','nursery',title('Знакомься с малышом!','Новый друг уже в твоей коллекции.')+'<div class="pc-split"><div>'+scene('assets/nursery.png','<img class="pc-baby" src="../catalog-package/assets/normal/'+str(child)+'.gif" alt="Новый малыш">')+'<div class="pc-center"><h2>'+names.get(child,'Новый малыш')+'</h2>'+badge('Уровень 1')+'</div></div><div>'+panel('<h2>Маленький собиратель</h2><div class="pc-trait"><img src="../final/assets/shop/rawst.png" alt=""><div><h3>Собиратель</h3><p>Будет приносить больше ягод.</p></div></div><p class="pc-muted">Характер: любопытный</p><div class="pc-divider"><h2>Удача выросла</h2>'+luck()+'</div><label>Кличка — по желанию<input type="text" placeholder="Имя малыша" style="width:100%;margin:10px 0 16px"></label>'+button('Продолжить','nursery','primary wide')+'<div style="margin-top:12px">'+button(icon('monitor')+'На рабочий стол',kind='wide')+'</div>')+'</div></div>')
shop=[('treats','Лакомства','Пять маленьких радостей для питомца.',20),('care-booster','Бустер ухода','Помогает подготовиться к эволюции.',20),('lucky-charm','Талисман удачи','Поможет встретить shiny-покемона.',30),('evolution-stone','Камень эволюции','Позволяет выбрать ветвь эволюции.',20),('offspring-charm','Талисман гнезда','Позволяет выбрать малыша из вариантов пары.',50),('xp-potion','Зелье опыта','Даёт питомцу 300 опыта.',40)]+[(k,'Ягода '+n,'Для питомника и эволюции.',5) for k,n in [('cheri','Cheri'),('chesto','Chesto'),('pecha','Pecha'),('rawst','Rawst'),('aspear','Aspear'),('oran','Oran')]]
def shop_row(k,n,d,price):
    berry=k in ('cheri','chesto','pecha','rawst','aspear','oran')
    quantity='<div class="pc-stepper" aria-label="Количество '+n+'"><button type="button" data-quantity="-1" aria-label="Уменьшить количество">'+icon('minus')+'</button><output>1</output><button type="button" data-quantity="1" aria-label="Увеличить количество">'+icon('plus')+'</button></div>' if berry else ''
    return '<article class="pc-shop-item" data-unit-price="'+str(price)+'"><img src="../final/assets/shop/'+k+'.png" alt="'+n+'"><div><h3>'+n+'</h3><p>'+d+'</p><span class="pc-muted pc-stock">В запасе: 12</span><div class="pc-shop-actions"><span class="pc-wallet">'+icon('coin')+'<span data-total>'+str(price)+'</span></span>'+quantity+button('Купить',small=True)+'</div></div></article>'
add('shop','Магазин','shop',title('Маленькие радости','Всё нужное для новых историй.')+'<div class="pc-shop-grid">'+''.join(shop_row(*x) for x in shop)+'</div>')
trophies=[next(t for t in manifest['trophies'] if t['id']==tid) for tid in manifest['albumCards']]
titles=['Маленькие открытия','Дружная компания','Новые друзья','Путешественники','Все края','Вместе лучше','Заботливые руки','Новая жизнь','Время расти','Редкая встреча','Знакомое имя','Любимый друг','Общий язык','Приятная находка','Жизнь кипит','Семейный поход','Большая команда','Все вкусы','Ягодная забота','Свой путь','Желанный малыш','Легендарная коллекция','Новый талант','Другое призвание','Новый дом','Глоток опыта']
conditions=['Открыть первый вид','Собрать трёх питомцев','Поймать первого друга','Завершить первый поход','Исследовать новые маршруты','Отправить полную команду','Заботиться о питомцах','Встретить первого малыша','Провести эволюцию','Получить shiny-покемона','Дать питомцу кличку','Добавить в избранное','Подобрать команду одного типа','Получить дополнительную находку','Запустить гнездо и поход','Отправить семью в поход','Отправить развитую команду','Собрать все виды ягод','Использовать ягоды для эволюции','Выбрать ветвь камнем','Выбрать малыша талисманом','Открыть легендарные виды','Открыть вторую особенность','Переобучить особенность','Передать питомца в новый дом','Использовать зелье опыта']
assert len(titles)==len(conditions)==len(trophies)==26
add('achievements','Достижения','corner',title('Наши маленькие победы','Выбери три любимые награды для полки.',button('В уголок','corner',small=True))+'<div class="pc-shelf">'+''.join('<div><img src="'+assetroot+t['file']+'" alt="Награда"><div>Место '+str(i+1)+'</div></div>' for i,t in enumerate(trophies[:3]))+'</div><div class="pc-trophy-grid">'+''.join('<article class="pc-trophy"><img src="'+assetroot+t['file' if i<4 else 'maskFile']+'" '+('style="filter:brightness(0);opacity:.7" ' if i>=4 else '')+'alt="'+('Закрытая награда' if i>=4 else 'Заработанная награда')+'"><h3>'+titles[i]+'</h3><p>'+('Получено' if i<4 else conditions[i])+'</p></article>' for i,t in enumerate(trophies))+'</div>')
add('catch','Поимка · закрытый покебол','corner',title('Кто же внутри?','Два покебола ждут знакомства.',button('В уголок','corner',small=True))+'<div class="pc-result-layout"><div class="pc-result-visual"><img class="pc-ball" src="assets/ball-closed.png" alt="Закрытый покебол"></div><div><h2>Ещё один новый друг</h2><p class="pc-muted">Открой покебол, когда будешь готов.</p><div class="pc-trait"><img src="../final/assets/shop/lucky-charm.png" alt="Талисман удачи"><div><h3>Талисман удачи</h3><p>Можно использовать перед поимкой.</p></div></div><div style="margin-top:24px">'+button('Бросить покебол','catch-result','primary wide')+'</div></div></div>')
add('catch-result','Поимка · результат','corner',title('Теперь вы знакомы!','Pidgey уже добавлен в коллекцию.')+'<div class="pc-result-layout"><div class="pc-result-visual">'+sprite(16,'Pidgey','large')+'<img class="pc-ball" src="assets/ball-open.png" alt="Открытый покебол"></div><div><h1 style="font-size:32px">Pidgey</h1><div class="pc-row">'+badge('Нормальный')+badge('Летающий')+'</div><div class="pc-trait">'+icon('map')+'<div><h3>Следопыт</h3><p>Помогает находить новых друзей в походах.</p></div></div><label>Кличка — по желанию<input type="text" placeholder="Pidgey" style="width:100%;margin:12px 0 18px"></label>'+button('Продолжить','catch','primary wide')+'</div></div>')
settings=[('Прятаться в полноэкранных приложениях','Чтобы спутник не мешал игре или фильму.','<input type="checkbox" checked aria-label="Прятаться в полноэкранных приложениях">'),('Панель прогресса','Показывать прогресс рядом со спутником.','<input type="checkbox" checked aria-label="Панель прогресса">'),('Размер питомца','Подбери удобный размер.','<input type="range" min="48" max="384" value="96" aria-label="Размер питомца">'),('Голос питомца','Громкость криков Pokémon.','<input type="range" value="30" aria-label="Голос питомца">'),('Звуковые эффекты','Кормление и другие маленькие звуки.','<input type="range" value="30" aria-label="Звуковые эффекты">'),('Запускать вместе с Windows','Спутник будет встречать тебя сам.','<input type="checkbox" checked aria-label="Запускать вместе с Windows">')]
add('settings','Настройки','',title('Настроим под тебя','Только то, что нужно каждый день.')+''.join('<label class="pc-settings-row"><span><strong>'+n+'</strong><p>'+d+'</p></span>'+c+'</label>' for n,d,c in settings)+'<div class="pc-footer">'+button('Посмотреть знакомство','tour-1')+button('История сохранений','save-history')+button('Готово','corner','primary')+'</div>')
add('save-history','История сохранений','',title('Твоя коллекция под защитой','Выбери копию, если нужно вернуться назад.',button('Настройки','settings',small=True))+''.join('<div class="pc-settings-row"><div><strong>'+d+'</strong><p>'+str(n)+' питомцев · автоматическая копия</p></div>'+button('Восстановить','confirm-restore',small=True)+'</div>' for d,n in [('Сегодня, 18:40',108),('Сегодня, 09:10',106),('Вчера, 22:35',104),('14 октября, 20:20',99)])+note('Перед восстановлением сохраним текущую коллекцию.',glyph='lock'))
for key,label,question,body,action in [('confirm-cancel','Подтверждение · прервать','Прервать поход?','Команда вернётся без находок и опыта.','Прервать поход'),('confirm-restore','Подтверждение · восстановить','Вернуться к этой копии?','Вернём коллекцию на выбранный момент. Текущую версию сохраним отдельно.','Восстановить'),('confirm-rehome','Подтверждение · передать','Новый дом для Ростка?','Росток покинет коллекцию. Вернуть его этой кнопкой не получится.','Передать')]:
    add(key,label,'',title('Подтверждение')+'<div class="pc-confirm"><h2>'+question+'</h2><p>'+body+'</p><div class="pc-footer">'+button('Отмена','corner')+button(action,'corner','danger')+'</div></div>')
tour=[('Добро пожаловать в уголок','Спутник рядом, пока ты занимаешься своими делами.',scene(room,roomlayers,'pc-room')),('Новые друзья ждут знакомства','За обычную активность появляются покеболы. Открывай их, когда удобно.','<div class="pc-center"><img class="pc-ball" src="assets/ball-closed.png" alt="Покебол"></div>'),('Маленькие приключения','Собери команду. Она привезёт ягоды и другие находки.',exp_scene),('В гнезде — новое открытие','Выбери совместимую пару и познакомься с малышом.',nest_scene),('Забота помогает расти','Гладь и корми питомца. Со временем он будет готов к эволюции.',evo),('Подарок для первых открытий','Два подарочных покебола ждут тебя в уголке.','<div class="pc-center"><img class="pc-ball" src="assets/ball-closed.png" alt="Первый подарочный покебол"><img class="pc-ball" src="assets/ball-closed.png" alt="Второй подарочный покебол"></div>')]
for i,(heading,copy,visual) in enumerate(tour,1):
    add('tour-'+str(i),'Знакомство · шаг '+str(i),'', '<div class="pc-training">'+title(heading,copy)+visual+'<div class="pc-dots">'+''.join('<span class="'+('current' if j==i else '')+'"></span>' for j in range(1,7))+'</div><div class="pc-footer">'+button('Позже','corner')+(button('Назад','tour-'+str(i-1)) if i>1 else '')+button('В мой уголок' if i==6 else 'Дальше','corner' if i==6 else 'tour-'+str(i+1),'primary')+'</div></div>')
components=title('Одна система, разные состояния','Библиотека компонентов для всех экранов.')+'<div class="pc-catalog"><section><h2>Действия</h2><div class="pc-state-row">'+button('Основное действие',kind='primary')+button('Вторичное')+button('Нельзя выбрать',disabled=True)+button('Сохраняем…',disabled=True)+button('Необратимое',kind='danger')+'</div></section><section><h2>Питомцы и выбор</h2><div class="pc-pet-grid">'+pet(1,'Росток',selected=True)+pet(7,'Squirtle',9,'water','В питомнике')+pet(25,'Pikachu',6,'','Свободен',shiny=True)+unknown+'</div></section><section><h2>Обратная связь</h2>'+note('Сохранено',glyph='check')+note('Не удалось сохранить. Попробуй ещё раз.','warning','lock')+panel('<h3>Пока нет команды</h3><p class="pc-muted">Выбери питомцев для первого маленького похода.</p>'+button('Собрать команду','expedition','primary'))+'<div class="pc-loading" style="margin-top:12px" role="status" aria-label="Загружаем карточки"></div></section><section><h2>Цена и нехватка</h2><div class="pc-rewards">'+item('rawst','× 6')+item('pecha','× 4')+coins(20)+'</div>'+note('Не хватает 2 ягод Rawst.','warning')+button('Докупить ягоды','shop')+'</section><section><h2>Фокус, наведение и нажатие</h2><p class="pc-muted">Наведи мышь или используй Tab: состояния работают на общих кнопках, полях и карточках.</p><input type="text" placeholder="Очень длинная кличка питомца" aria-label="Кличка для проверки"><div class="pc-row" style="margin-top:12px">'+badge('Готов к вылуплению','','egg')+badge('В походе','busy','map')+badge('Shiny','shiny','spark')+'</div></section></div>'
add('components','Компоненты и состояния','',components)

from revision2 import apply_revision
apply_revision(globals())
from revision3 import apply_revision as apply_round3
apply_round3(globals())
from expedition_team import apply_revision as apply_team
apply_team(globals())

tabs=[('corner','Уголок','home'),('pokedex','Покедекс','book'),('expeditions','Экспедиции','map'),('nursery','Питомник','egg'),('shop','Магазин','shop')]
header='<header class="pc-header"><div class="pc-brand">'+icon('ball')+'PokéCorner</div><div class="pc-header-right"><span class="pc-wallet">'+icon('coin')+'1 240</span><button type="button" class="pc-link" data-open="catch">'+icon('ball')+'Встречи · 2</button><button type="button" class="pc-icon-button" data-open="settings" aria-label="Настройки">'+icon('gear')+'</button></div></header><nav class="pc-tabs" aria-label="Разделы">'+''.join('<button type="button" data-tab="'+key+'" data-open="'+{'pokedex':'collection','expeditions':'expedition'}.get(key,key)+'">'+icon(glyph)+name+'</button>' for key,name,glyph in tabs)+'</nav>'
tools='<div class="review-tools"><label>Сценарий <select id="review-scenario">'+''.join('<option value="'+k+'">'+escape(label)+'</option>' for k,(label,_,_) in pages.items())+'</select></label><label>Размер <select id="review-size"><option value="base">Базовый · 1000 × 760</option><option value="compact">Компактный · 840 × 650</option></select></label><a href="../UI-SYSTEM.md">Спецификация</a><span class="pc-state-indicator">Макеты · данные для примера</span></div>'
pagehtml=''.join('<main class="pc-page" id="'+k+'" data-section="'+tab+'"'+(' hidden' if k!='corner' else '')+'>'+html+'</main>' for k,(_,tab,html) in pages.items())
doc='<!doctype html><html lang="ru"><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1"><title>PokéCorner — целые экраны и компоненты</title><link rel="stylesheet" href="review.css"><link rel="stylesheet" href="revision2.css"><link rel="stylesheet" href="revision3.css"><link rel="stylesheet" href="capture.css"><link rel="stylesheet" href="expedition-team.css">'+tools+'<div class="pc-window">'+header+pagehtml+modal_markup+'</div><p class="review-note">Дизайн для просмотра: переходы показывают сценарии, покупки и сохранения не выполняются. Предлагаемый базовый размер проверяем на целых экранах; окончательные ограничения окна сверим с рабочей областью Windows при переносе. Визуальный комплект принят 17 октября 2026; идёт перенос в приложение.</p><script src="review.js"></script><script type="application/json" id="revision-data">'+json.dumps(revision_data,ensure_ascii=False).replace('</','<\\/')+'</script><script src="revision2.js"></script><script src="revision3.js"></script><script src="capture.js"></script><script src="expedition-team.js"></script></html>'
# Fingerprint prototype scripts/styles so a review never mixes revisions.
for filename in ('review.css','revision2.css','revision3.css','capture.css','expedition-team.css','review.js','revision2.js','revision3.js','capture.js','expedition-team.js'):
    version=hashlib.sha256((OUT/filename).read_bytes()).hexdigest()[:12]
    doc=doc.replace('"'+filename+'"','"'+filename+'?v='+version+'"')
(OUT/'review.html').write_text(doc,encoding='utf-8',newline='\n')
assert (OUT/'review.js').is_file()
(OUT/'screens.json').write_text(json.dumps({'baseWindowDip':[1000,760],'compactReviewDip':[840,650],'status':'proposed-design-not-runtime','screens':[{'id':k,'name':label,'tab':tab} for k,(label,tab,_) in pages.items()],'nurseryExample':{'recipeId':recipe['id'],'parentIds':parents,'outcomeSpeciesIds':[r['speciesId'] for r in recipe['outcomes']],'berries':recipe['baseBerries'],'hours':recipe['hours']}},ensure_ascii=False,indent=2)+'\n',encoding='utf-8')

class Links(HTMLParser):
    def handle_starttag(self, tag, attrs):
        for key,val in attrs:
            if key in ('src','href') and val and not val.startswith('#'):
                path=(OUT/val.split('#')[0].split('?')[0]).resolve()
                assert path.is_relative_to(ROOT) and path.is_file(),val
Links().feed(doc)
assert len(pages)>=25
assert all('вероятность' not in html.lower() and 'коэффициент' not in html.lower() for _,_,html in pages.values())
assert '../catalog-package/assets/normal/1.gif' in doc
print('Built',len(pages),'whole-screen/state examples; all local references verified. New art approval recorded; no runtime registrations or app changes.')
print(OUT/'review.html')
