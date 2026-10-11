import assert from 'node:assert/strict';
import { readFile, writeFile } from 'node:fs/promises';
import { species, pairs, eligible } from './audit-nursery-feasibility.mjs';

const root = new URL('./', import.meta.url);
const catalog = JSON.parse(await readFile(new URL('catalog-package/catalog.json', root), 'utf8'));
const berryCatalog = JSON.parse(await readFile(new URL('berry-profiles.json', root), 'utf8'));
const berryIds = new Set(berryCatalog.berries.map(b=>b.id));
const assetById = new Map(catalog.species.map(s=>[s.id,s.assets]));
const shinyUsable = id => {
  const {normal,shiny}=assetById.get(id);
  return Boolean(shiny.animated && shiny.sha256 && shiny.sha256!==normal.sha256);
};
const byId = new Map(species.map(s => [s.id,s]));
const baseSpecies=species.filter(s=>s.isBase);
assert(baseSpecies.every(s=>s.id===s.familyRootId));
const evolutionEdges=catalog.species.filter(s=>s.species.evolves_from_species_id)
  .map(s=>[Number(s.species.evolves_from_species_id),s.id]);
const recipeByPair = new Map();
const exposure = new Map(species.map(s => [s.id,0]));
const parentUse = new Map(species.map(s => [s.id,0]));
// Draft density only: species-level counts may exceed these targets when filling a recipe.
const targets = [6,4,2,1];
const weights = [8,4,2,.5];
const hours = [2,4,6,8];
const berries = { grass:'rawst', bug:'rawst', poison:'pecha', water:'oran', normal:'oran',
  flying:'pecha', fairy:'pecha', rock:'chesto', fighting:'chesto', dragon:'chesto',
  ground:'aspear', dark:'aspear', fire:'cheri', steel:'cheri', ice:'aspear',
  ghost:'chesto', psychic:'chesto', electric:'oran' };
assert([...berryIds].every(id=>id===id.toLowerCase()));
assert(Object.values(berries).every(id=>berryIds.has(id)));

function score(pair, child) {
  let result = 0;
  if (pair.a.types.includes(child.types[0]) && pair.b.types.includes(child.types[0])) result += 30;
  if (pair.a.familyRootId !== pair.b.familyRootId) result += 12;
  if (pair.a.familyRootId === child.familyRootId || pair.b.familyRootId === child.familyRootId) result += 4;
  for (const parent of [pair.a,pair.b]) {
    result += parent.types.filter(t=>child.types.includes(t)).length * 4;
    if (parent.habitat && parent.habitat === child.habitat) result += 4;
    if (parent.shape === child.shape) result += 2;
    result += Math.max(0, 2-Math.abs(parent.generation-child.generation));
  }
  return result;
}

function add(pair, child) {
  assert(eligible(pair,child));
  let recipe = recipeByPair.get(pair.id);
  if (!recipe) {
    recipe = { pair, children:[] };
    recipeByPair.set(pair.id,recipe);
    for (const parent of [pair.a,pair.b]) parentUse.set(parent.id,parentUse.get(parent.id)+1);
  }
  assert(recipe.children.length < 3);
  assert(!recipe.children.some(s=>s.id===child.id));
  assert(child.rank !== 3 || !recipe.children.some(s=>s.rank===3));
  recipe.children.push(child);
  exposure.set(child.id,exposure.get(child.id)+1);
}

const candidateByChild = new Map();
const missing = [];
for (const child of baseSpecies) {
  const parentRank = Math.max(0,child.rank-1);
  const candidates = pairs.filter(p=>p.a.rank===parentRank && p.b.rank===parentRank &&
    eligible(p,child) && (!child.normalAnimated || p.active));
  candidates.sort((a,b)=>Number(b.active)-Number(a.active) || score(b,child)-score(a,child) || a.a.id-b.a.id || a.b.id-b.b.id);
  candidateByChild.set(child.id,candidates);
  if (!candidates.length) missing.push({id:child.id,name:child.name,rarity:child.rarity});
}
assert.equal(missing.length,0,`No strict progression candidate: ${JSON.stringify(missing)}`);

// Maximum bipartite matching preserves one distinct rare-parent pair for every legend.
const assignedPair = new Map();
const anchoredLegends = new Set();
const pairById = new Map(pairs.map(p=>[p.id,p]));
// Hand-picked game recipes, not claims about official Pokemon breeding.
const legendaryAnchors = new Map([[144,[131,615]],[145,[466,479]],[146,[467,637]],
  [150,[124,376]],[151,[375,876]],[251,[465,781]],[385,[303,375]],[493,[132,474]]]);
for (const [childId,parentIds] of legendaryAnchors) {
  const key=[...parentIds].sort((a,b)=>a-b).join('-');
  assert(candidateByChild.get(childId).some(p=>p.id===key),`Invalid legendary anchor ${childId}: ${key}`);
  assert(!assignedPair.has(key));
  assignedPair.set(key,byId.get(childId));
  anchoredLegends.add(childId);
}
function assignLegend(child, visited) {
  for (const pair of candidateByChild.get(child.id)) {
    if (visited.has(pair.id)) continue;
    visited.add(pair.id);
    const previous = assignedPair.get(pair.id);
    if (previous && anchoredLegends.has(previous.id)) continue;
    if (!previous || assignLegend(previous,visited)) {
      assignedPair.set(pair.id,child);
      return true;
    }
  }
  return false;
}
const legends = baseSpecies.filter(s=>s.rank===3).sort((a,b)=>candidateByChild.get(a.id).length-candidateByChild.get(b.id).length || a.id-b.id);
for (const child of legends) if (!anchoredLegends.has(child.id)) assert(assignLegend(child,new Set()),`Legend pairing failed: ${child.name}`);
for (const [pairId,child] of assignedPair) add(pairById.get(pairId),child);

function fillSecond(recipe) {
  const required=recipe.pair.active&&recipe.children.some(c=>!c.normalAnimated)?3:2;
  if (recipe.children.length >= required) return;
  const candidates = baseSpecies.filter(s=>s.rank<3 && eligible(recipe.pair,s) &&
    !recipe.children.some(c=>c.id===s.id) && (!recipe.pair.active || s.normalAnimated));
  candidates.sort((a,b)=>exposure.get(a.id)/targets[a.rank]-exposure.get(b.id)/targets[b.rank] ||
    score(recipe.pair,b)-score(recipe.pair,a) || a.id-b.id);
  assert(candidates.length,`No second result for ${recipe.pair.id}`);
  add(recipe.pair,candidates[0]);
  fillSecond(recipe);
}
for (const recipe of recipeByPair.values()) fillSecond(recipe);

for (const child of baseSpecies.filter(s=>s.rank<3).sort((a,b)=>b.rank-a.rank || a.id-b.id)) {
  while (exposure.get(child.id) < targets[child.rank]) {
    let best = null, bestScore = -Infinity;
    for (const pair of candidateByChild.get(child.id)) {
      const existing = recipeByPair.get(pair.id);
      if (existing && (existing.children.length>=3 || existing.children.some(c=>c.id===child.id))) continue;
      if (existing && !child.normalAnimated && existing.children.some(c=>!c.normalAnimated)) continue;
      const value = score(pair,child) + (pair.active?100:0) + (existing?30:0) -
        2*(parentUse.get(pair.a.id)+parentUse.get(pair.b.id));
      if (value > bestScore) { best=pair; bestScore=value; }
    }
    assert(best,`Not enough pairs for ${child.name}`);
    add(best,child);
    fillSecond(recipeByPair.get(best.id));
  }
}

// Coverage alone leaves some owned species without a partner. Add reviewable bridge recipes.
const bridgePairs=[];
for (const parent of [...species].sort((a,b)=>Number(b.normalAnimated)-Number(a.normalAnimated) || b.rank-a.rank || a.id-b.id)) {
  if (parentUse.get(parent.id)) continue;
  const candidates=pairs.filter(p=>(p.a.id===parent.id||p.b.id===parent.id)&&!recipeByPair.has(p.id)&&
    (!parent.normalAnimated||p.active));
  candidates.sort((a,b)=>{
    const partner=p=>p.a.id===parent.id?p.b:p.a;
    const x=partner(a),y=partner(b),desired=Math.min(parent.rank,2);
    return Math.abs(x.rank-desired)-Math.abs(y.rank-desired) || Number(b.active)-Number(a.active) ||
      parentUse.get(x.id)-parentUse.get(y.id) || a.a.id-b.a.id || a.b.id-b.b.id;
  });
  let chosen=null,children=null;
  for (const pair of candidates) {
    const choices=baseSpecies.filter(c=>c.rank<3&&eligible(pair,c)&&(!pair.active||c.normalAnimated));
    choices.sort((a,b)=>{
      const preference=c=>c.rank===2&&exposure.get(c.id)<3?3:c.rank===1&&exposure.get(c.id)<5?2:c.rank===0?1:0;
      return preference(b)-preference(a) || exposure.get(a.id)/targets[a.rank]-exposure.get(b.id)/targets[b.rank] ||
        score(pair,b)-score(pair,a) || a.id-b.id;
    });
    if (choices.length>=2) {chosen=pair;children=choices.slice(0,2);break;}
  }
  assert(chosen,`No partner bridge for ${parent.name}`);
  for (const child of children) add(chosen,child);
  bridgePairs.push(chosen.id);
}
assert(species.every(s=>parentUse.get(s.id)>0));

const recipes = [...recipeByPair.values()].sort((a,b)=>a.pair.a.id-b.pair.a.id || a.pair.b.id-b.pair.b.id).map(({pair,children})=>{
  children.sort((a,b)=>a.id-b.id);
  const commonTypes = pair.a.types.filter(t=>pair.b.types.includes(t));
  const primary = berries[commonTypes[0]];
  const secondary = [...pair.a.types,...pair.b.types].map(t=>berries[t]).find(b=>b!==primary) ?? primary;
  const totalWeight = children.reduce((sum,c)=>sum+weights[c.rank],0);
  const activeChildren = pair.active?children.filter(c=>c.normalAnimated):[];
  const activeWeight = activeChildren.reduce((sum,c)=>sum+weights[c.rank],0);
  return { id:pair.id, parentIds:[pair.a.id,pair.b.id], commonTypes,
    baseBerries:primary===secondary?{[primary]:10}:{[primary]:6,[secondary]:4},
    selectedSurcharge:{[primary]:4},
    hours:activeChildren.length?hours[Math.max(...activeChildren.map(c=>c.rank))]:null,
    plannedHours:hours[Math.max(...children.map(c=>c.rank))],
    active:activeChildren.length>0, selectionUseful:activeChildren.length>1,
    outcomes:children.map(c=>({speciesId:c.id,rarity:c.rarity,weight:weights[c.rank],
      probability:weights[c.rank]/totalWeight, active:pair.active&&c.normalAnimated,
      normalAvailable:pair.active&&c.normalAnimated, shinyAvailable:pair.active&&c.normalAnimated&&shinyUsable(c.id),
      activeProbability:pair.active&&c.normalAnimated?weights[c.rank]/activeWeight:0})),
    needsThemeReview:true };
});

// Check independently from serialized output, including the unavailable catalog.
const fullExposure = new Map(species.map(s=>[s.id,[]]));
for (const recipe of recipes) {
  const [a,b] = recipe.parentIds.map(id=>byId.get(id));
  assert(a.id<b.id && (a.mask&b.mask));
  assert(recipe.outcomes.length>=2 && recipe.outcomes.length<=3);
  assert.equal(new Set(recipe.outcomes.map(c=>c.speciesId)).size,recipe.outcomes.length);
  assert(recipe.outcomes.filter(c=>c.rarity==='Legendary').length<=1);
  assert(Math.abs(recipe.outcomes.reduce((sum,c)=>sum+c.probability,0)-1)<1e-12);
  assert.equal(Object.values(recipe.baseBerries).reduce((sum,n)=>sum+n,0),10);
  assert.equal(Object.values(recipe.selectedSurcharge).reduce((sum,n)=>sum+n,0),4);
  assert([...Object.keys(recipe.baseBerries),...Object.keys(recipe.selectedSurcharge)].every(id=>berryIds.has(id)));
  const active = recipe.outcomes.filter(c=>c.active);
  assert.equal(recipe.hours,active.length?hours[Math.max(...active.map(c=>byId.get(c.speciesId).rank))]:null);
  assert(!recipe.active || active.length>=2);
  assert(!active.length || Math.abs(active.reduce((sum,c)=>sum+c.activeProbability,0)-1)<1e-12);
  for (const outcome of recipe.outcomes) {
    const c=byId.get(outcome.speciesId);
    assert(c.isBase,`Evolved hatch result ${c.name}`);
    assert(!recipe.parentIds.includes(c.id));
    assert(c.rank<=Math.min(a.rank,b.rank)+1);
    assert((a.mask&b.mask&c.mask)!==0);
    assert(!outcome.active || (a.normalAnimated&&b.normalAnimated&&c.normalAnimated));
    fullExposure.get(c.id).push(recipe.id);
  }
}
// Same-species rows reuse the family root and type map, without another recipe network.
const sameSpeciesRecipes=species.map(parent=>{
  const child=byId.get(parent.familyRootId);
  const active=parent.normalAnimated&&child.normalAnimated;
  return {speciesId:parent.id,outcomeSpeciesId:child.id,baseBerries:{[berries[child.types[0]]]:10},
    hours:active?hours[child.rank]:null,plannedHours:hours[child.rank],
    normalAvailable:active,shinyAvailable:active&&shinyUsable(child.id),
    selectedSurcharge:{},selectionUseful:false,offspringCharmRequired:false};
});
assert.equal(sameSpeciesRecipes.length,1025);
assert(sameSpeciesRecipes.every(r=>r.outcomeSpeciesId===byId.get(r.speciesId).familyRootId&&
  Object.keys(r.baseBerries).length===1&&Object.keys(r.baseBerries).every(id=>berryIds.has(id))&&
  Object.values(r.baseBerries)[0]===10));
assert.equal(sameSpeciesRecipes.find(r=>r.speciesId===26).outcomeSpeciesId,172);
assert.equal(sameSpeciesRecipes.find(r=>r.speciesId===134).outcomeSpeciesId,133);
assert(baseSpecies.every(s=>fullExposure.get(s.id).length>=1));
assert(legends.every(s=>fullExposure.get(s.id).length===1));
assert(baseSpecies.filter(s=>s.normalAnimated).every(s=>recipes.some(r=>r.outcomes.some(c=>c.speciesId===s.id&&c.active))));

function closure(activeOnly) {
  const allowed=species.filter(s=>!activeOnly||s.normalAnimated);
  const reached=new Set(allowed.filter(s=>s.isBase&&s.rank===0).map(s=>s.id));
  const rounds=[{round:0,added:reached.size,total:reached.size}];
  for (let round=1;round<=species.length;round++) {
    const added=new Set();
    for (const recipe of recipes) {
      if (!recipe.parentIds.every(id=>reached.has(id))) continue;
      for (const c of recipe.outcomes) if ((!activeOnly||c.active)&&!reached.has(c.speciesId)) added.add(c.speciesId);
    }
    for (const [from,to] of evolutionEdges) {
      if (reached.has(from)&&!reached.has(to)&&(!activeOnly||byId.get(to).normalAnimated)) added.add(to);
    }
    if (!added.size) break;
    for (const id of added) reached.add(id);
    rounds.push({round,added:added.size,total:reached.size});
  }
  return { seed:'All Common evolution roots; nursery plus evolution edges, not three starting individuals',rounds,
    unreachable:allowed.filter(s=>!reached.has(s.id)).map(s=>s.id) };
}
const fullClosure=closure(false), activeClosure=closure(true);
assert.equal(fullClosure.unreachable.length,0);
// Missing root/intermediate assets can block a nursery path to a currently animated later species.
const activeBlockedByAssets=activeClosure.unreachable.map(id=>({id,name:byId.get(id).name,
  familyRootId:byId.get(id).familyRootId}));
const giftPairs=recipes.filter(r=>r.active && r.parentIds.every(id=>byId.get(id).rank<=1) &&
  r.outcomes.filter(c=>c.active).length>=2);
assert(giftPairs.length>0);
assert(species.every(starter=>giftPairs.some(r=>!r.parentIds.includes(starter.id))));
const exposureSummary=Object.fromEntries(['Common','Uncommon','Rare','Legendary'].map(r=>{
  const list=baseSpecies.filter(s=>s.rarity===r).map(s=>fullExposure.get(s.id).length);
  return [r,{species:list.length,min:Math.min(...list),max:Math.max(...list),mean:list.reduce((a,b)=>a+b,0)/list.length}];
}));
const graph={ schemaVersion:1,status:'generated-proposal-not-approved-or-integrated',dataRevision:catalog.dataRevision,
  raritySource:'rarity-draft-v2.json',
  sameSpeciesRule:{twoDistinctIndividuals:true,outcome:'family-root',excludedFromCrossSpeciesCoverage:true,
    berries:'10 of family-root primary-type berry',selectionSurcharge:0,offspringCharmRequired:false},
  sameSpeciesRecipes,
  offspringRule:'Only evolution roots, including baby species; later species are reached through evolution.',
  draftChoices:{childSharesCommonParentType:true,coverageParentsFromPreviousRarityTier:true,
    densityTargets:targets,legendaryAnchors:Object.fromEntries(legendaryAnchors),
    bridgePairs,genderAndOfficialEggGroupsIgnored:true},
  acceptedStarterGifts:{twoClosedPokeballs:true,distinctFromStarter:true,
    compatibleCommonOrUncommonParentPair:true,candidateRecipeIds:giftPairs.map(r=>r.id)},
  recipes };
const report={status:'structural-checks-passed-theme-and-economy-review-pending',
  canonicalBerryIdsVerified:true,sameSpeciesRecipes:sameSpeciesRecipes.length,
  recipes:recipes.length,activeRecipes:recipes.filter(r=>r.active).length,
  twoOutcomes:recipes.filter(r=>r.outcomes.length===2).length,threeOutcomes:recipes.filter(r=>r.outcomes.length===3).length,
  fullSpeciesCovered:1025,baseSpeciesCovered:baseSpecies.length,
  activeBaseSpeciesCovered:baseSpecies.filter(s=>s.normalAnimated).length,
  activeSpeciesCovered:species.filter(s=>s.normalAnimated).length-activeBlockedByAssets.length,
  activeBlockedByAssets,
  legendaryUniqueRecipes:legends.length,exposureSummary,fullClosure,activeClosure,
  giftPairCandidates:giftPairs.length,
  activeSingleOutcomeRecipes:recipes.filter(r=>r.outcomes.filter(c=>c.active).length===1).map(r=>r.id),
  highParentUse:[...parentUse].sort((a,b)=>b[1]-a[1]).slice(0,15).map(([id,count])=>({id,name:byId.get(id).name,count})),
  examples:[1,25,133,144,145,146,150,151,493,1000,1025].map(id=>({id,name:byId.get(id).name,
    offspringId:byId.get(id).familyRootId,recipeIds:fullExposure.get(byId.get(id).familyRootId),
    firstRecipe:recipes.find(r=>r.id===fullExposure.get(byId.get(id).familyRootId)[0])})) };
await writeFile(new URL('nursery-recipes-draft.json',root),JSON.stringify(graph,null,2)+'\n');
await writeFile(new URL('nursery-recipe-audit.json',root),JSON.stringify(report,null,2)+'\n');
const fmt=c=>`#${String(c.id).padStart(4,'0')} ${c.name}`;
const recipeLine=r=>`${r.parentIds.map(id=>fmt(byId.get(id))).join(' + ')} → ${r.outcomes.map(c=>
  `${fmt(byId.get(c.speciesId))} (${(c.probability*100).toFixed(1)}%${c.active?'':', пока недоступен'})`).join('; ')}`;
const review=[
  '# Рецепты питомника — первый полный черновик',
  '',
  'Дата: 2026-10-11. Численные правила механики приняты; уточнения аудита выбраны сейчас в рамках делегированного пользователем решения. Конкретные пары сохраняются для тематической проверки. Это подготовка, не реализация игры.',
  '',
  '## Что построено и проверено',
  '',
  `- ${recipes.length} пар разных родителей; ${report.activeRecipes} доступны по текущим нормальным BW-анимациям, остальные ожидают ассетов родителей.`,
  `- Все ${baseSpecies.length} начальных видов имеют рецепт разных родителей; потомство всегда первое звено цепочки, включая baby-виды. Все 1025 видов достижимы через вылупление и дальнейшие эволюции в полном каталоге.`,
  `- У всех ${legends.length} легендарных/мифических начальных видов ровно один рецепт разных родителей; развитые легенды получаются эволюцией, не дополнительными яйцами.`,
  '- Все виды используются как родители хотя бы одной пары; потомство только начальные виды. Для всех 868 доступных видов есть партнёр с готовой анимацией.',
  '- У разных родителей 2–3 разных начальных вида, ни один не совпадает с видом родителя. У каждого доступного рецепта минимум два доступных результата. Одинаковые родители дают начальный вид своей семьи: Райчу → Пичу, Вапореон → Иви.',
  '- Редкость потомка не выше одной ступени над более обычным родителем. Общий тип родителей проверен; в этом черновике потомок также разделяет этот общий тип.',
  '- Стоимость 10 ягод; выбор вида 14 + талисман. Время 2/4/6/8 часов по самой высокой редкости ДОСТУПНЫХ результатов, фиксируется при запуске. Если доступных результатов нет, запуск запрещён. plannedHours хранит будущую длительность полной таблицы только для проектирования. Разные проценты легендарных исходов пользователь подтвердил; веса 8/4/2/0,5 сохраняются.',
  '- Отдельные 1025 строк sameSpeciesRecipes: два разных экземпляра одного вида дают корень семьи; ровно 10 ягод по ОСНОВНОМУ типу корня. Райчу → Пичу: 10 oran; Вапореон → Иви: 10 oran. У одинаковых легендарных то же правило и время 8 часов при доступном корне. Один результат: без доплаты, режима выбора и талисмана потомства.',
  '- Все ID ягод строчные и проверены по berry-profiles.json. normalAvailable и shinyAvailable проверяются отдельно; отсутствующие и identical shiny не заменяют обычного результата. Для смеси исходов сохраняются конкретный вид и его shiny-доступность: нельзя исключать нормальный вид ради shiny или перебрасывать результат.',
  '',
  '## Распределение рецептов по начальным видам',
  '',
  ...Object.entries(exposureSummary).map(([r,s])=>`- ${r}: ${s.min}–${s.max} рецептов на вид, в среднем ${s.mean.toFixed(2)}.`),
  '',
  'Эти частоты — результат первого подбора, а не новые жёсткие лимиты механики. Целевые плотности 6/4/2/1 использованы только как эвристика; дополнительные пары обеспечивают возможность использовать всех родителей.',
  '',
  '## Достижимость',
  '',
  `Проверено постепенное открытие из обычных начальных видов через питомник И эволюции. Полный каталог достижим. В текущем BW-каталоге по этому пути достижимы ${report.activeSpeciesCovered} из 868 анимированных видов; ${activeBlockedByAssets.length} ждут ассетов начального/промежуточного звена. Это не запрещает поимку готового развитого вида через прежние источники.`,
  'Это не утверждение, что любая стартовая тройка даст весь каталог без встреч. Проверка предполагает получение исходных обычных видов через активность/экспедиции и не учитывает время, ягоды или число экземпляров.',
  '',
  '## Принятое уточнение старта',
  '',
  `Два подарочных покебола выбираются совместимой парой обычных/необычных видов, оба отличаются от стартового питомца. Для выбора подготовлено ${giftPairs.length} пар; для каждого стартового вида существует подходящий вариант. Содержимое фиксируется совместно и остаётся скрытым до открытия покеболов. Питомник можно попробовать после добычи ингредиентов. Обучение не заставляет тратить ресурсы.`,
  '',
  'Доступность случайных небольших коллекций пересчитывается отдельно после изменения рецептов: nursery-access-audit.json. Стартовая совместимая подарочная пара остаётся принятой независимо от пересчёта.',
  '',
  '## Несколько показательных рецептов',
  '',
  ...[144,145,146,150,151,493].map(id=>'- '+recipeLine(recipes.find(r=>r.id===fullExposure.get(id)[0]))),
  '',
  'Рецепты авторские для нашей игры, не правила официального разведения. Подбор по типам и среде не гарантирует хорошую визуальную или сюжетную связь: все пары помечены needsThemeReview.',
  '',
  '## Что осталось решить и проверить',
  '',
  '- Вручную просмотреть легендарные пары и спорные обычные связи; генератор служит заготовкой для фиксированного каталога, не случайным подбором во время игры.',
  '- Устранить блокирующие ассеты начальных/промежуточных видов. Запрещено обходить нехватку анимации выдачей готовой эволюции из яйца.',
  '- При будущих обновлениях пересчитывать доступные результаты и время только до запуска; уже запущенные гнёзда сохраняют время, цену и зафиксированный исход.',
  '- Проверить реальные частоты получения редких видов и легенд с выбранными парами, талисманами и повторными яйцами; только структурная проверка не доказывает темп экономики.',
  '- Частоты рецептов считаются по начальным видам/семьям. Прошлый черновик с прямым вылуплением всех видов недействителен.',
  '',
  '## Все легендарные рецепты для тематической проверки',
  '',
  ...legends.slice().sort((a,b)=>a.id-b.id).map(c=>'- '+recipeLine(recipes.find(r=>r.id===fullExposure.get(c.id)[0]))),
  '',
  'Данные: nursery-recipes-draft.json. Проверки: nursery-feasibility.json, nursery-recipe-audit.json, nursery-access-audit.json. Пересборка: rtk proxy node docs/design-v2/build-nursery-draft.mjs; затем rtk proxy node docs/design-v2/check-nursery-access.mjs.',
  ''
].join('\n');
await writeFile(new URL('NURSERY-RECIPES.md',root),review);
console.log(JSON.stringify({recipes:report.recipes,activeRecipes:report.activeRecipes,exposureSummary,
  fullClosure,activeClosure,activeSingleOutcomeRecipes:report.activeSingleOutcomeRecipes.length,
  examples:report.examples.map(e=>({child:e.name,parents:e.firstRecipe.parentIds.map(id=>byId.get(id).name),
    outcomes:e.firstRecipe.outcomes.map(c=>byId.get(c.speciesId).name)}))}));
