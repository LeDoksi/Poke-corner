import assert from 'node:assert/strict';
import { readFile, writeFile } from 'node:fs/promises';

const root=new URL('./',import.meta.url);
const graph=JSON.parse(await readFile(new URL('nursery-recipes-draft.json',root),'utf8'));
const species=JSON.parse(await readFile(new URL('rarity-draft-v2.json',root),'utf8')).species;
const tiers=['Common','Uncommon','Rare','Legendary'];
const active=species.filter(s=>s.normalAnimated);
const pools=tiers.map(r=>active.filter(s=>s.rarity===r));
const recipes=graph.recipes.filter(r=>r.active);
let state=20261010;
const random=()=>{state=(Math.imul(state,1664525)+1013904223)>>>0;return state/4294967296;};
const samples=3000;
const smallCollection=[];
for (const size of [3,10,25,50]) {
  let withRecipe=0,totalRecipes=0;
  for (let trial=0;trial<samples;trial++) {
    const owned=new Set();
    // Distinct-species snapshot only, not a simulation of pity, duplicates or elapsed time.
    const weights=size===3?[2/3,1/3,0,0]:[.6,.3,.09,.01];
    while (owned.size<size) {
      const roll=random(); let cumulative=0,tier=0;
      for (;tier<weights.length-1;tier++) {cumulative+=weights[tier];if(roll<cumulative)break;}
      const pool=pools[tier];
      owned.add(pool[Math.floor(random()*pool.length)].id);
    }
    assert.equal(owned.size,size);
    let matches=0;
    for (const recipe of recipes) if (recipe.parentIds.every(id=>owned.has(id))) matches++;
    withRecipe+=Number(matches>0);totalRecipes+=matches;
  }
  smallCollection.push({distinctSpecies:size,samples,probabilityAtLeastOneDifferentParentRecipe:withRecipe/samples,
    meanCompatiblePairs:totalRecipes/samples});
}
const parentCounts=new Map(species.map(s=>[s.id,0]));
for (const r of recipes) for (const id of r.parentIds) parentCounts.set(id,parentCounts.get(id)+1);
const report={status:'synthetic-access-check-not-full-game-simulation',seed:20261010,samples,
  assumptions:'Three distinct Common/Uncommon species for initial snapshot; larger collections drawn with 60/30/9/1 rarity weights and duplicate rejection. No species affinities, starter choice, item stock, care or time modeled. Same-species breeding needs two individuals and is excluded.',
  smallCollection,
  activeSpeciesWithoutDifferentParentRecipeAsParent:active.filter(s=>!parentCounts.get(s.id)).map(s=>({id:s.id,name:s.name,rarity:s.rarity})),
  unavailableSpeciesUsedAsParents:[...parentCounts].filter(([id,n])=>n&&!active.some(s=>s.id===id)).map(([id])=>id),
  existingExamplePairs:[{parents:[19,52],meaning:'Rattata + Meowth'},
    {parents:[43,69],meaning:'Oddish + Bellsprout'},
    {parents:[10,13],meaning:'Caterpie + Weedle'}].map(pair=>({...pair,
      recipe:recipes.find(r=>r.parentIds.join('-')===pair.parents.join('-'))??null})) };
assert.equal(report.unavailableSpeciesUsedAsParents.length,0);
await writeFile(new URL('nursery-access-audit.json',root),JSON.stringify(report,null,2)+'\n');
console.log(JSON.stringify({smallCollection,activeUnusedParents:report.activeSpeciesWithoutDifferentParentRecipeAsParent.length,
  unusedParentRarities:Object.fromEntries(tiers.map(r=>[r,report.activeSpeciesWithoutDifferentParentRecipeAsParent.filter(s=>s.rarity===r).length])),
  existingExamplePairs:report.existingExamplePairs}));
