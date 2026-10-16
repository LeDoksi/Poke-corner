const reviewData=JSON.parse(document.getElementById('revision-data').textContent);
const modalLayer=document.getElementById('review-modal');
const modalTitle=document.getElementById('modal-title');
const modalContent=document.getElementById('modal-content');
const modalActions=document.getElementById('modal-actions');
const originalShow=showScreen;
let previousFocus=null,toastTimer=null,catchTimers=[],luckyActive=false,luckyCount=2,selectedSlot=0;
let subject={sid:1,name:'Росток',level:12,rarity:'Uncommon',shiny:false,busy:false,favorite:false,active:false};
let shelf=['trophy-population-1','trophy-expeditions-1','trophy-care-1'];
const safe=value=>String(value).replace(/[&<>"']/g,x=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[x]));
function toast(text){let el=document.querySelector('.v2-toast');if(!el){el=document.createElement('div');el.className='v2-toast';el.setAttribute('role','status');document.querySelector('.pc-window').append(el);}el.textContent=text;el.hidden=false;clearTimeout(toastTimer);toastTimer=setTimeout(()=>el.hidden=true,2200);}
function closeModal(){modalLayer.hidden=true;for(const el of document.querySelector('.pc-window').children)el.inert=false;if(previousFocus?.isConnected)previousFocus.focus();}
function modal(heading,content,actions=''){previousFocus=document.activeElement;modalTitle.textContent=heading;modalContent.innerHTML=content;modalActions.innerHTML='<button type="button" class="pc-button" data-close-modal>Отмена</button>'+actions;modalLayer.hidden=false;for(const el of document.querySelector('.pc-window').children)if(el!==modalLayer)el.inert=true;modalActions.querySelector('[data-close-modal]').focus();}
function confirm(kind){
 if(kind==='evolution'&&document.querySelector('[data-evo-mode="stone"]').getAttribute('aria-pressed')==='true'){
   const branch=document.querySelector('[data-branch][aria-selected="true"]');
   if(!branch){modal('Выбери ветвь','<p>Сначала отметь форму, в которую эволюционирует Eevee.</p>');return;}
   modal('Эволюция в выбранную форму','<p>Выбрана ветвь #'+String(branch.dataset.branch).padStart(4,'0')+'. Понадобятся 6 ягод Oran и один камень.</p>','<button type="button" class="pc-button primary" data-confirm-next="detail">Эволюционировать</button>');return;
 }
 const names={cancel:['Прервать поход?','Команда вернётся без находок и опыта.','Прервать поход','expedition'],restore:['Вернуться к этой копии?','Вернём коллекцию на выбранный момент. Текущую версию сохраним отдельно.','Восстановить','save-history'],rehome:['Новый дом для '+subject.name+'?','Питомец покинет коллекцию. Открытый вид и достижения сохранятся. Вернуть питомца этой кнопкой не получится.','Передать','collection'],evolution:['Довериться случаю?','Ветка будет выбрана случайно. После эволюции изменить её этим действием нельзя.','Эволюционировать','detail']};
 const [rawHeading,p,label,next]=names[kind];const h=kind==='rehome'?'Найти новый дом?':rawHeading;
 if(kind==='rehome'&&(subject.favorite||subject.busy||subject.active)){modal(h,'<p>'+(subject.favorite?'Избранный питомец защищён. Сначала убери его из избранного.':subject.busy?'Сейчас питомец занят. Дождись завершения его дела.':'Сначала выбери другого спутника на рабочем столе.')+'</p>');return;}
 const amount=({Common:6,Uncommon:12,Rare:24,Legendary:40}[subject.rarity]||12)+Math.floor(subject.level/10)+(subject.shiny?5:0);
 modal(h,'<p>'+safe(p)+'</p>'+(kind==='rehome'?'<div class="v2-item-preview"><img src="../catalog-package/assets/'+(subject.shiny?'shiny':'normal')+'/'+subject.sid+'.gif" alt="'+safe(subject.name)+'"><strong>'+safe(subject.name)+'<br><span class="pc-item"><img class="pc-coin" src="assets/coin.png" alt="Монеты">'+amount+' монет</span></strong></div>':''),'<button type="button" class="pc-button '+(kind==='evolution'?'primary':'danger')+'" data-confirm-next="'+next+'">'+label+'</button>');
}
showScreen=function(id){
 if(id.startsWith('confirm-')){const contexts={'confirm-cancel':'expedition-running','confirm-restore':'save-history','confirm-rehome':'detail'};const base=contexts[id];if(base&&document.querySelector('.pc-page:not([hidden])')?.id!==base)originalShow(base);confirm(id.slice(8));scenario.value=id;return;}
 for(const timer of catchTimers)clearTimeout(timer);catchTimers=[];
 if(!modalLayer.hidden)closeModal();originalShow(id);
 if(id==='catch-animation')startCatch(document.getElementById(id));
};
function filterPage(page){
 const controls=[...page.querySelectorAll('[data-filter]')];const values=Object.fromEntries(controls.map(el=>[el.dataset.filter,el.type==='checkbox'?el.checked:el.value]));const grid=page.querySelector('[data-filter-grid]');if(!grid)return;
 const cards=[...grid.children];let visible=0;const counts={};for(const card of cards)if(card.dataset.sid)counts[card.dataset.sid]=(counts[card.dataset.sid]||0)+1;
 for(const card of cards){const d=card.dataset;let ok=true;const q=(values.search||'').trim().toLowerCase();const number=d.sid||d.number||'';if(q&&!((card.textContent+' '+number).toLowerCase().includes(q)))ok=false;if(values.type&&!(d.types||'').split(' ').includes(values.type))ok=false;if(values.rarity&&d.rarity!==values.rarity)ok=false;for(const field of ['shiny','grow','favorite'])if(values[field]&&d[field]!=='true')ok=false;if(values.duplicate&&(counts[d.sid]||0)<2)ok=false;card.hidden=!ok;if(ok)visible++;}
 cards.sort((a,b)=>values.sort==='level'?Number(b.dataset.level||-1)-Number(a.dataset.level||-1):values.sort==='care'?Number(b.dataset.care||-1)-Number(a.dataset.care||-1):values.sort==='date'?Number(b.dataset.ordinal||0)-Number(a.dataset.ordinal||0):Number(a.dataset.sid||a.dataset.number)-Number(b.dataset.sid||b.dataset.number));for(const card of cards)grid.append(card);page.querySelector('.pc-empty-filter').hidden=visible!==0;
}
for(const page of document.querySelectorAll('.pc-page')){[...page.querySelectorAll('[data-filter-grid]>.pc-pet-card')].forEach((el,i)=>el.dataset.ordinal=String(i));page.addEventListener('input',event=>{if(event.target.matches('[data-filter]'))filterPage(page);});page.addEventListener('change',event=>{if(event.target.matches('[data-filter]'))filterPage(page);});}
function selectPet(card){
 const d=card.dataset;subject={sid:Number(d.sid),name:d.name,level:Number(d.level),rarity:d.rarity,shiny:d.shiny==='true',favorite:d.favorite==='true',busy:card.textContent.includes('В походе')||card.textContent.includes('В питомнике'),active:card.textContent.includes('На столе')};const facts=reviewData.facts[d.sid];
 document.querySelector('[data-detail-name]').textContent=subject.name;document.querySelector('[data-detail-species]').textContent=facts.name+' · #'+String(subject.sid).padStart(4,'0');document.querySelector('[data-detail-sprite] img').src='../catalog-package/assets/'+(subject.shiny?'shiny':'normal')+'/'+subject.sid+'.gif';document.querySelector('[data-detail-sprite] img').alt=subject.name;document.querySelector('[data-detail-types]').innerHTML=facts.types;document.querySelector('[data-detail-fact]').textContent=facts.fact;
 const level=document.querySelector('#detail .pc-metric-label span');level.textContent='Уровень '+subject.level;
 const payout=({Common:6,Uncommon:12,Rare:24,Legendary:40}[subject.rarity]||12)+Math.floor(subject.level/10)+(subject.shiny?5:0);document.querySelector('#detail [data-open="confirm-rehome"]').innerHTML='Найти новый дом · <span class="pc-item"><img class="pc-coin" src="assets/coin.png" alt="Монеты">'+payout+'</span>';
 const evolutionAction=document.querySelector('#detail [data-open="evolution"],#detail [data-open="evolution-branch"]');evolutionAction.dataset.open=subject.sid===133?'evolution-branch':'evolution';evolutionAction.disabled=!facts.canGrow||subject.busy;evolutionAction.title=subject.busy?'Питомец сейчас занят':!facts.canGrow?'Это финальная форма':'';
 const evo=document.querySelector('[data-detail-evolution]');evo.innerHTML='<div class="pc-evolution"><div><img src="../catalog-package/assets/normal/'+subject.sid+'.gif" alt="'+safe(facts.name)+'"><small>'+safe(facts.name)+'</small></div>'+facts.evolution.slice(0,3).map(sid=>'<span aria-hidden="true">→</span><div><img class="pc-sprite silhouette" src="../catalog-package/assets/normal/'+sid+'.gif" alt="Закрытая форма"><small>#'+String(sid).padStart(4,'0')+'</small></div>').join('')+'</div>';
 const care=document.querySelector('[data-item="care"]');care.disabled=!facts.canGrow;care.title=facts.canGrow?'':'Питомец больше не эволюционирует';const xp=document.querySelector('[data-item="xp"]');xp.disabled=subject.level>=40;xp.title=xp.disabled?'Питомец уже достиг максимального уровня':'';
 showScreen('detail');
}
function updateShelf(){
 for(const el of document.querySelectorAll('[data-shelf-image]')){const award=reviewData.awards.find(a=>a.id===shelf[Number(el.dataset.shelfImage)]);el.hidden=!award;if(award){el.src=award.file;el.alt=award.name;}}
 for(const room of document.querySelectorAll('.pc-room')){const images=[...room.querySelectorAll('img[alt="Заработанная награда"]')];images.forEach((img,i)=>{const award=reviewData.awards.find(a=>a.id===shelf[i]);img.hidden=!award;if(award){const [w,h]=award.size,[ax,ay]=award.foot,[x,y]=reviewData.anchors[i];img.src=award.file;Object.assign(img.style,{left:(x-w*ax)/1448*100+'%',top:(y-h*ay)/582*100+'%',width:w/1448*100+'%',height:h/582*100+'%'});}});}
}
function chooseAward(slot){selectedSlot=slot;modal('Награда для места '+(slot+1),'<div class="pc-trophy-grid">'+reviewData.awards.map(a=>'<article class="pc-trophy"><img src="'+a.file+'" alt="'+safe(a.name)+'"><h3>'+safe(a.name)+'</h3><button type="button" class="pc-button small" data-award-choice="'+a.id+'">Поставить</button></article>').join('')+'</div>');}
function startCatch(page){
 if(page.id!=='catch-animation'){showScreen('catch-animation');return;}
 const ball=page.querySelector('[data-animated-ball]'),caption=page.querySelector('[data-catch-stage]'),button=page.querySelector('[data-throw]');ball.src='assets/ball-closed.png';ball.className='pc-ball v2-throw';caption.textContent='Бросаем покебол…';button.disabled=true;
 const later=(fn,ms)=>catchTimers.push(setTimeout(fn,ms));
 later(()=>{ball.className='pc-ball v2-shake';caption.textContent='Покебол покачивается…';},700);
 later(()=>{ball.src='assets/ball-open.png';ball.className='pc-ball v2-opening';caption.textContent='Пойман!';},2400);
 later(()=>{button.disabled=false;if(luckyActive){luckyCount=Math.max(0,luckyCount-1);luckyActive=false;for(const row of document.querySelectorAll('.v2-lucky-control')){row.querySelector('[data-lucky-status]').textContent='Талисман использован · в запасе '+luckyCount;const b=row.querySelector('[data-activate-charm]');b.textContent=luckyCount?'Активировать':'Нет в запасе';b.disabled=!luckyCount;}}showScreen('catch-result');},3000);
}
document.addEventListener('click',event=>{
 const t=event.target;
 const reset=t.closest('[data-reset-filters]');if(reset){const page=reset.closest('.pc-page');for(const el of page.querySelectorAll('[data-filter]')){if(el.type==='checkbox')el.checked=false;else el.value=el.tagName==='SELECT'?(el.dataset.filter==='sort'?'number':''):'';}filterPage(page);}
 const pet=t.closest('[data-personal-card]');if(pet)selectPet(pet);
 const species=t.closest('[data-species-card]');if(species){const sid=species.dataset.sid,owned=[...document.querySelectorAll('#collection [data-personal-card]')].filter(x=>x.dataset.sid===sid);modal(reviewData.facts[sid].name,'<p>Мои экземпляры</p><div class="v2-owned-list">'+owned.map((el,i)=>'<button type="button" class="pc-button" data-owned-index="'+i+'" data-owned-species="'+sid+'">'+safe(el.dataset.name)+' · ур. '+el.dataset.level+'<span>'+ (el.textContent.includes('В питомнике')?'В питомнике':el.textContent.includes('На столе')?'На столе':'')+'</span></button>').join('')+'</div>');}
 const owned=t.closest('[data-owned-index]');if(owned){const list=[...document.querySelectorAll('#collection [data-personal-card]')].filter(x=>x.dataset.sid===owned.dataset.ownedSpecies);closeModal();selectPet(list[Number(owned.dataset.ownedIndex)]);}
 if(t.closest('[data-close-modal]'))closeModal();const confirmNext=t.closest('[data-confirm-next]');if(confirmNext){const next=confirmNext.dataset.confirmNext;closeModal();showScreen(next);toast('Готово · пример результата');}
 const use=t.closest('[data-item]');if(use){const care=use.dataset.item==='care';modal(care?'Применить бустер ухода?':'Использовать зелье опыта?','<div class="v2-item-preview"><img src="../final/assets/shop/'+(care?'care-booster':'xp-potion')+'.png" alt=""><p>'+ (care?'Увеличит уход для эволюции.':'Добавит 300 опыта.')+'<br>Питомец: <strong>'+safe(subject.name)+'</strong></p></div>','<button type="button" class="pc-button primary" data-apply-item="'+use.dataset.item+'">Применить</button>');}
 const apply=t.closest('[data-apply-item]');if(apply){closeModal();toast(apply.dataset.applyItem==='care'?'Бустер ухода применён · пример':'Питомец получил опыт · пример');}
 const parent=t.closest('[data-parent]');if(parent){const slot=parent.dataset.parent;const sid=slot==='first'?1:152;const names=slot==='first'?['Росток','Клевер']:['Chikorita','Листик'];modal(slot==='first'?'Выбрать родителя':'Выбрать совместимого родителя',names.map(name=>'<button type="button" class="pc-button v2-parent-option" data-parent-choice="'+slot+'" data-parent-label="'+name+'"><img src="../catalog-package/assets/normal/'+sid+'.gif" alt=""><span>'+name+'<small>Уровень 12 · совместим с парой</small></span>Выбрать</button>').join(''));}
 const parentChoice=t.closest('[data-parent-choice]');if(parentChoice){for(const el of document.querySelectorAll('[data-parent-name="'+parentChoice.dataset.parentChoice+'"]'))el.textContent=parentChoice.dataset.parentLabel;closeModal();}
 const mode=t.closest('[data-evo-mode]');if(mode){const stone=mode.dataset.evoMode==='stone';for(const b of document.querySelectorAll('[data-evo-mode]'))b.setAttribute('aria-pressed',String(b===mode));document.querySelector('[data-stone-price]').hidden=!stone;document.querySelector('[data-branch-copy]').textContent=stone?'Выбери одну из доступных ветвей.':'Форма станет сюрпризом.';for(const b of document.querySelectorAll('[data-branch]')){b.disabled=!stone;b.removeAttribute('aria-selected');}}
 const branch=t.closest('[data-branch]');if(branch&&!branch.disabled){for(const b of document.querySelectorAll('[data-branch]'))b.setAttribute('aria-selected',String(b===branch));document.querySelector('[data-branch-copy]').textContent='Выбрана ветвь #'+String(branch.dataset.branch).padStart(4,'0');}
 const slot=t.closest('[data-shelf-slot]');if(slot)chooseAward(Number(slot.dataset.shelfSlot));
 const pick=t.closest('[data-pick-award]');if(pick)modal('Куда поставить награду?','<p>Выбери одно из трёх мест полки.</p><div class="pc-row">'+[0,1,2].map(i=>'<button type="button" class="pc-button" data-place-award="'+pick.dataset.pickAward+'" data-place-slot="'+i+'">Место '+(i+1)+'</button>').join('')+'</div>');
 const place=t.closest('[data-place-award]');if(place){const award=place.dataset.placeAward,index=Number(place.dataset.placeSlot);shelf=shelf.map((a,i)=>a===award&&i!==index?null:a);shelf[index]=award;updateShelf();closeModal();toast('Награда на полке');}
 const award=t.closest('[data-award-choice]');if(award){shelf=shelf.map((a,i)=>a===award.dataset.awardChoice&&i!==selectedSlot?null:a);shelf[selectedSlot]=award.dataset.awardChoice;updateShelf();closeModal();toast('Полка обновлена');}
 const activate=t.closest('[data-activate-charm]');if(activate){luckyActive=true;for(const row of document.querySelectorAll('.v2-lucky-control')){row.querySelector('[data-lucky-status]').textContent='Активен на следующую поимку';const b=row.querySelector('[data-activate-charm]');b.textContent='Талисман активен';b.disabled=true;}}
 const throwing=t.closest('[data-throw]');if(throwing)startCatch(throwing.closest('.pc-page'));
 const route=t.closest('[data-route]');if(route)for(const label of document.querySelectorAll('[data-affinity]')){const match=route.dataset.route==='grove';label.hidden=!match;label.innerHTML='<svg class="i" aria-hidden="true" viewBox="0 0 24 24"><path d="M21 3C9 2 1 9 5 16c7 7 16-2 16-13ZM4 21 17 7"/></svg>Подходит для рощи';}
});
document.addEventListener('keydown',event=>{if(modalLayer.hidden)return;if(event.key==='Escape'){event.preventDefault();closeModal();}if(event.key==='Tab'){const controls=[...modalLayer.querySelectorAll('button:not(:disabled),input,select,a[href]')];const first=controls[0],last=controls[controls.length-1];if(event.shiftKey&&document.activeElement===first){event.preventDefault();last.focus();}else if(!event.shiftKey&&document.activeElement===last){event.preventDefault();first.focus();}}});
for(const branch of document.querySelectorAll('[data-branch]'))branch.disabled=true;
const initialScreen=scenario.value;showScreen(initialScreen);
