import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';

const root=new URL('./',import.meta.url);
const catalog=JSON.parse(await readFile(new URL('catalog-package/catalog.json',root),'utf8'));
const graph=JSON.parse(await readFile(new URL('nursery-recipes-draft.json',root),'utf8'));
const berryIds=new Set(JSON.parse(await readFile(new URL('berry-profiles.json',root),'utf8')).berries.map(b=>b.id));
const rarity=JSON.parse(await readFile(new URL('rarity-draft-v2.json',root),'utf8')).species;
const byId=new Map(catalog.species.map(s=>[s.id,s]));
function familyRoot(id) {
  const visited=new Set();
  while (byId.get(id).species.evolves_from_species_id) {
    assert(!visited.has(id));visited.add(id);
    id=Number(byId.get(id).species.evolves_from_species_id);
  }
  return id;
}
assert.equal(familyRoot(26),172); // Raichu -> Pichu, not Pikachu.
assert.equal(familyRoot(134),133); // Vaporeon -> Eevee.
const evolvedOutcomes=graph.recipes.flatMap(r=>r.outcomes.filter(c=>familyRoot(c.speciesId)!==c.speciesId)
  .map(c=>({recipe:r.id,child:c.speciesId,base:familyRoot(c.speciesId)})));
assert.equal(evolvedOutcomes.length,0,`Evolved hatch results: ${JSON.stringify(evolvedOutcomes.slice(0,8))}`);
assert.equal(graph.sameSpeciesRule.outcome,'family-root');
assert.equal(graph.sameSpeciesRecipes.length,1025);
assert.equal(new Set(graph.sameSpeciesRecipes.map(r=>r.speciesId)).size,1025);
for (const row of graph.sameSpeciesRecipes) {
  assert.equal(row.outcomeSpeciesId,familyRoot(row.speciesId));
  assert.equal(Object.keys(row.baseBerries).length,1);
  assert.equal(Object.values(row.baseBerries)[0],10);
  assert(Object.keys(row.baseBerries).every(id=>berryIds.has(id)&&id===id.toLowerCase()));
  assert.equal(Object.keys(row.selectedSurcharge).length,0);
  assert.equal(row.selectionUseful,false);
  assert.equal(row.offspringCharmRequired,false);
}
for (const [parent,child,berry,hours] of [[26,172,'oran',2],[134,133,'oran',4],[150,150,'chesto',8]]) {
  const row=graph.sameSpeciesRecipes.find(r=>r.speciesId===parent);
  assert.equal(row.outcomeSpeciesId,child);
  assert.deepEqual(row.baseBerries,{[berry]:10});
  assert.equal(row.hours,hours);
}
for (const recipe of graph.recipes) {
  assert([...Object.keys(recipe.baseBerries),...Object.keys(recipe.selectedSurcharge)].every(id=>berryIds.has(id)));
  for (const outcome of recipe.outcomes) {
    const assets=byId.get(outcome.speciesId).assets;
    const usable=Boolean(assets.shiny.animated&&assets.shiny.sha256&&assets.shiny.sha256!==assets.normal.sha256);
    assert.equal(outcome.normalAvailable,outcome.active);
    assert.equal(outcome.shinyAvailable,outcome.normalAvailable&&usable);
  }
}
assert(graph.sameSpeciesRecipes.filter(r=>rarity.find(s=>s.id===r.speciesId).rarity==='Legendary')
  .every(r=>r.plannedHours===8));
console.log('Nursery base-stage regression checks OK');
