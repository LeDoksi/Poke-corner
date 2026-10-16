const scenario = document.getElementById('review-scenario');
const size = document.getElementById('review-size');
const screens = [...document.querySelectorAll('.pc-page')];
const defaults = {corner:'corner',pokedex:'collection',expeditions:'expedition',nursery:'nursery',shop:'shop'};
function showScreen(id) {
  const active = document.getElementById(id);
  if (!active || !active.classList.contains('pc-page')) return;
  for (const screen of screens) screen.hidden = screen !== active;
  active.scrollTop = 0;
  scenario.value = id;
  for (const tab of document.querySelectorAll('[data-tab]')) {
    if (tab.dataset.tab === active.dataset.section) tab.setAttribute('aria-current','page');
    else tab.removeAttribute('aria-current');
  }
  history.replaceState(null,'','#'+id);
}
scenario.addEventListener('change',()=>showScreen(scenario.value));
size.addEventListener('change',()=>document.body.classList.toggle('compact',size.value==='compact'));
document.addEventListener('click',event=>{
  const target=event.target.closest('[data-open]');
  if(target && !target.disabled) showScreen(target.dataset.open);
  const route=event.target.closest('[data-route]');
  if(route){
    for(const b of route.parentElement.querySelectorAll('button')) b.setAttribute('aria-pressed',String(b===route));
    const names={grove:'Лесная роща',lake:'Озёрный берег',coast:'Морское побережье',mountains:'Горные тропы',desert:'Пустынные дюны',volcano:'Вулканическая долина',snow:'Снежный перевал',ruins:'Древние руины'};
    for(const scene of document.querySelectorAll('.pc-exp-scene')){
      scene.querySelector('img').src='../final/assets/expeditions/'+route.dataset.route+'.png';
      scene.querySelector('[data-route-title]').textContent=names[route.dataset.route];
    }
    for(const screen of screens.filter(s=>s.dataset.section==='expeditions')){
      const subtitle=screen.querySelector('.pc-title p');
      if(subtitle && screen.id!=='expedition' && screen.id!=='expedition-result') subtitle.textContent=names[route.dataset.route]+' · обычная сложность';
      const label=screen.querySelector('.pc-metric-label span');
      if(label) label.textContent=names[route.dataset.route];
      const effects=[...screen.querySelectorAll('.pc-slot-pet small')];
      for(const effect of effects) if(effect.textContent==='Подходит для рощи'||effect.dataset.routeEffect){effect.dataset.routeEffect='true';effect.textContent=route.dataset.route==='grove'?'Подходит для рощи':'В команде';}
    }
  }
  const segment=event.target.closest('.pc-segments button:not([data-open])');
  if(segment) for(const b of segment.parentElement.querySelectorAll('button')) b.setAttribute('aria-pressed',String(b===segment));
  const outcome=event.target.closest('[data-outcome]');
  if(outcome && document.getElementById('offspring-choice')?.checked){
    for(const b of outcome.parentElement.querySelectorAll('button')) b.classList.toggle('selected',b===outcome);
  }
  const step=event.target.closest('[data-quantity]');
  if(step){
    const row=step.closest('.pc-shop-item'),output=row.querySelector('output');
    output.value=String(Math.max(1,Math.min(99,Number(output.value)+Number(step.dataset.quantity))));
    row.querySelector('[data-total]').textContent=String(Number(output.value)*Number(row.dataset.unitPrice));
  }
});
showScreen(location.hash.slice(1)||'corner');
