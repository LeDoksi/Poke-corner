const teamData=reviewData.team;
const teamTraits=Object.fromEntries(teamData.traits.map(t=>[t.id,t]));
let expeditionTeam=[],draftTeam=[],teamPage=0,teamRoute='grove',teamLevel=10;
const teamPageSize=12;
const teamByKey=key=>teamData.members.find(p=>p.key===key);
const teamFree=p=>!p.busy&&!p.desktop;
const teamMatches=p=>teamData.locations.find(l=>l.id===teamRoute).bonusTypes.some(t=>round3.species[p.sid].types.includes(t));
const teamName=value=>String(value).toLocaleLowerCase('ru').replaceAll('ё','е');
function teamPicture(p){return '<img loading="lazy" src="../catalog-package/assets/'+(p.shiny?'shiny':'normal')+'/'+p.sid+'.gif" alt="'+safe(p.name)+'">';}
function teamSlot(p,index,editable){return '<div class="team-slot">'+(p?teamPicture(p)+'<div><strong>'+safe(p.name)+'</strong><small>Ур. '+p.level+(p.shiny?' · Shiny':'')+'</small></div>'+(editable?'<button type="button" class="pc-icon-button" data-team-remove="'+p.key+'" aria-label="Убрать '+safe(p.name)+' из команды">×</button>':''):'<span class="team-slot-number">'+(index+1)+'</span><span>Место для питомца</span>')+'</div>';}
function readTeamRoute(page){
 teamRoute=page.querySelector('[data-route][aria-pressed="true"]')?.dataset.route||teamRoute;
 const difficulty=page.querySelector('.pc-panel .pc-segments [aria-pressed="true"]')?.textContent.trim();
 teamLevel=teamData.recommendedLevels[{'Лёгкая':0,'Обычная':1,'Сложная':2}[difficulty]??1];
}
function renderExpeditionPreparation(page){
 const selected=expeditionTeam.map(teamByKey);const count=selected.length;
 page.querySelector('[data-current-team]').innerHTML='<div class="team-prep-slots">'+[0,1,2].map(i=>teamSlot(selected[i],i,false)).join('')+'</div>';
 page.querySelector('[data-team-count]').textContent=count+' / 3';
 page.querySelector('[data-select-team]').textContent=count?'Изменить команду':'Выбрать команду';
 page.querySelector('[data-expedition-start]').disabled=count===0;
 page.querySelector('[data-team-note]').textContent=count===3?'Полная команда — можно встретить покемона.':count?'Небольшая команда привезёт меньше находок. Для встречи нужны трое.':'Выбери от одного до трёх питомцев.';
 page.querySelector('[data-team-estimate]').hidden=count!==3;
 const placeholder=page.querySelector('[data-team-estimate-empty]');placeholder.hidden=count===3;placeholder.textContent=count?'Ягоды, монеты и опыт. Полная команда привезёт больше находок.':'Выбери питомцев — здесь появятся находки и опыт.';
}
function filteredTeamMembers(){
 const page=document.getElementById('expedition-team');const query=teamName(page.querySelector('[data-team-search]').value.trim());
 const type=page.querySelector('[data-team-type]').value,trait=page.querySelector('[data-team-trait]').value,available=page.querySelector('[data-team-available]').checked,matching=page.querySelector('[data-team-matching]').checked;
 const sort=page.querySelector('[data-team-sort]').value;
 return teamData.members.filter(p=>(!available||teamFree(p))&&(!matching||teamMatches(p))&&(!type||round3.species[p.sid].types.includes(type))&&(!trait||p.traits.includes(trait))&&(!query||teamName(p.name+' '+round3.species[p.sid].name+' #'+String(p.sid).padStart(4,'0')+' '+p.sid).includes(query))).sort((a,b)=>sort==='number'?a.sid-b.sid||Number(a.key)-Number(b.key):sort==='level'?b.level-a.level||a.sid-b.sid:Number(teamFree(b))-Number(teamFree(a))||Number(teamMatches(b))-Number(teamMatches(a))||Number(b.level>=teamLevel)-Number(a.level>=teamLevel)||b.level-a.level||a.sid-b.sid);
}
function renderTeamSelection(){
 const page=document.getElementById('expedition-team'),members=filteredTeamMembers();teamPage=Math.max(0,Math.min(teamPage,Math.ceil(members.length/teamPageSize)-1));
 page.querySelector('[data-team-route]').textContent='Маршрут: '+(round3.locations[teamData.locations.findIndex(l=>l.id===teamRoute)].name)+' · рекомендуемый уровень '+teamLevel;
 page.querySelector('[data-team-slots]').innerHTML=[0,1,2].map(i=>teamSlot(teamByKey(draftTeam[i]),i,true)).join('');
 page.querySelector('[data-team-search-status]').textContent='Найдено питомцев: '+members.length;
 page.querySelector('[data-team-grid]').innerHTML=members.slice(teamPage*teamPageSize,(teamPage+1)*teamPageSize).map(p=>{
  const selected=draftTeam.includes(p.key),blocked=!teamFree(p),full=draftTeam.length===3&&!selected;
  return '<button type="button" class="team-member" data-team-pick="'+p.key+'" aria-pressed="'+selected+'" '+(blocked||full?'disabled':'')+'>'+teamPicture(p)+'<div class="team-member-copy"><div class="pc-row between"><strong>'+safe(p.name)+'</strong><span>Ур. '+p.level+'</span></div><small>#'+String(p.sid).padStart(4,'0')+(p.shiny?' · Shiny':'')+'</small><div class="team-trait-effects">'+p.traits.map(id=>'<small>'+safe(teamTraits[id].name)+' · '+safe(teamTraits[id].effect)+'</small>').join('')+'</div><span class="team-fit '+(teamMatches(p)?'matches':'')+'">'+(blocked?(p.desktop?'На рабочем столе':p.busyReason):selected?'В команде':teamMatches(p)?'Подходит маршруту':p.level<teamLevel?'Рекомендуется уровень '+teamLevel:'Можно отправить')+'</span></div></button>';
 }).join('')||'<p class="pc-muted">Никого не нашли. Попробуй другой поиск или фильтры.</p>';
 page.querySelector('[data-team-page-label]').textContent=(teamPage+1)+' / '+Math.max(1,Math.ceil(members.length/teamPageSize));
 page.querySelector('[data-team-page="-1"]').disabled=teamPage===0;page.querySelector('[data-team-page="1"]').disabled=(teamPage+1)*teamPageSize>=members.length;
 page.querySelector('[data-team-selection-status]').textContent='В команде '+draftTeam.length+' из 3';page.querySelector('[data-team-save]').disabled=draftTeam.length===0;
}
const beforeTeamScreen=showScreen;
showScreen=function(id){
 if(id==='expedition-selected')expeditionTeam=teamData.demoKeys.slice();
 beforeTeamScreen(id);
 if(id==='expedition'||id==='expedition-selected'){const page=document.getElementById(id);readTeamRoute(page);renderExpeditionPreparation(page);}
 if(id==='expedition-team')renderTeamSelection();
 if(['expedition-running','expedition-ready','expedition-result'].includes(id)&&expeditionTeam.length){
  const grid=document.querySelector('#'+id+' .pc-three');if(grid)grid.innerHTML=expeditionTeam.map(key=>{const p=teamByKey(key);return '<div class="pc-slot-pet">'+teamPicture(p)+'<div><strong>'+safe(p.name)+'</strong><small>Ур. '+p.level+'</small><small>'+safe(teamTraits[p.trait].name)+' · '+safe(teamTraits[p.trait].effect)+'</small></div></div>';}).join('');
 }
};
function resetTeamFilters(){const page=document.getElementById('expedition-team');for(const input of page.querySelectorAll('.team-filters input,.team-filters select,.team-filter-row input,.team-filter-row select')){if(input.type==='checkbox')input.checked=input.hasAttribute('data-team-available');else input.value=input.hasAttribute('data-team-sort')?'recommended':'';}teamPage=0;}
document.querySelector('[data-team-type]').innerHTML=document.querySelector('#collection [data-filter="type"]').innerHTML;
document.addEventListener('click',event=>{
 const target=event.target;
 if(target.closest('[data-select-team]')){readTeamRoute(target.closest('.pc-page'));draftTeam=expeditionTeam.slice();resetTeamFilters();showScreen('expedition-team');}
 const pick=target.closest('[data-team-pick]');if(pick){const key=pick.dataset.teamPick;if(draftTeam.includes(key))draftTeam=draftTeam.filter(k=>k!==key);else if(draftTeam.length<3&&teamFree(teamByKey(key)))draftTeam.push(key);renderTeamSelection();}
 const remove=target.closest('[data-team-remove]');if(remove){draftTeam=draftTeam.filter(k=>k!==remove.dataset.teamRemove);renderTeamSelection();}
 const paging=target.closest('[data-team-page]');if(paging){teamPage+=Number(paging.dataset.teamPage);renderTeamSelection();}
 if(target.closest('[data-team-reset]')){resetTeamFilters();renderTeamSelection();}
 if(target.closest('[data-team-cancel]'))showScreen('expedition');
 if(target.closest('[data-team-save]')&&draftTeam.length){expeditionTeam=draftTeam.slice();showScreen('expedition');}
 if(target.closest('[data-route]')||target.closest('#expedition .pc-panel .pc-segments button,#expedition-selected .pc-panel .pc-segments button')){const page=target.closest('.pc-page');const route=target.closest('[data-route]');if(route)for(const b of document.querySelectorAll('#expedition [data-route],#expedition-selected [data-route]'))b.setAttribute('aria-pressed',String(b.dataset.route===route.dataset.route));if(page?.querySelector('[data-current-team]')){readTeamRoute(page);renderExpeditionPreparation(page);}}
});
for(const input of document.querySelectorAll('.team-filters input,.team-filters select,.team-filter-row input,.team-filter-row select'))input.addEventListener('input',()=>{teamPage=0;renderTeamSelection();});
const initialTeamPage=document.querySelector('.pc-page:not([hidden])');if(initialTeamPage&&['expedition','expedition-selected','expedition-team'].includes(initialTeamPage.id))showScreen(initialTeamPage.id);
