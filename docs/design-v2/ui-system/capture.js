// A rare encounter reveal: state feedback and a short celebration, not UI navigation motion.
let captureRun=null;
const beforeCaptureScreen=showScreen;
showScreen=function(id){
 if(id!=='catch-animation'&&id!=='catch-reduced')captureRun=null;
 beforeCaptureScreen(id);
 if(id==='catch-reduced')startCatch(document.getElementById(id));
};
function finishCapture(run){
 if(captureRun!==run||run.finished)return;
 run.finished=true;
 if(luckyActive){
  luckyCount=Math.max(0,luckyCount-1);luckyActive=false;
  for(const row of document.querySelectorAll('.v2-lucky-control')){
   row.querySelector('[data-lucky-status]').textContent='Талисман использован · в запасе '+luckyCount;
   const button=row.querySelector('[data-activate-charm]');button.textContent=luckyCount?'Активировать':'Нет в запасе';button.disabled=!luckyCount;
  }
 }
 showScreen('catch-result');
}
startCatch=function(page){
 if(page.id!=='catch-animation'&&page.id!=='catch-reduced'){
  captureKnown=page.id==='encounter';showScreen('catch-animation');return;
 }
 for(const timer of catchTimers)clearTimeout(timer);catchTimers=[];
 const run={page,finished:false};captureRun=run;
 const stage=page.querySelector('.capture-scene'),ball=page.querySelector('[data-capture-ball]'),pet=page.querySelector('[data-capture-pet]'),caption=page.querySelector('[data-capture-stage]');
 const reduced=page.id==='catch-reduced'||window.matchMedia('(prefers-reduced-motion: reduce)').matches;
 stage.classList.toggle('capture-reduced-motion',reduced);
 pet.classList.toggle('silhouette',!captureKnown);pet.alt=captureKnown?'Pidgey':'Незнакомый покемон';
 page.querySelector('h1').textContent=captureKnown?'Ловим Pidgey':'Новая встреча';
 page.querySelector('.pc-title p').textContent=captureKnown?'На той самой поляне в Лесной роще.':'Пора познакомиться с новым другом.';
 ball.src='assets/ball-closed.png';stage.dataset.phase='flight';
 const later=(fn,ms)=>catchTimers.push(setTimeout(()=>{if(captureRun===run&&!run.finished)fn();},ms));
 const phase=(name,text)=>{stage.dataset.phase=name;caption.textContent=text;};
 if(reduced){phase('reduced','Пойман!');later(()=>finishCapture(run),250);return;}
 phase('flight','Покебол летит…');
 later(()=>{ball.src='assets/ball-open.png';phase('capture','Почти получилось…');},1100);
 later(()=>{ball.src='assets/ball-closed.png';phase('landing','Мягкое приземление…');},1750);
 later(()=>phase('wobble','Покебол покачивается…'),2550);
 later(()=>phase('success','Пойман!'),5550);
 later(()=>finishCapture(run),6250);
};
document.addEventListener('click',event=>{if(event.target.closest('[data-capture-skip]')&&captureRun)finishCapture(captureRun);});
// A direct review link can enter before this last script has loaded.
const initialCapture=document.querySelector('.pc-page:not([hidden])');
if(initialCapture&&['catch-animation','catch-reduced'].includes(initialCapture.id))startCatch(initialCapture);
