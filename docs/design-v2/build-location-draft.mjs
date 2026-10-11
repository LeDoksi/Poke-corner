import assert from 'node:assert/strict';
import { readFile, writeFile } from 'node:fs/promises';

const root = new URL('./', import.meta.url);
const catalog = JSON.parse(await readFile(new URL('catalog-package/catalog.json', root), 'utf8'));
const rarity = new Map(JSON.parse(await readFile(new URL('rarity-draft-v2.json', root), 'utf8')).species.map(s => [s.id, s.rarity]));
const routes = [
  {id:'grove', name:'Лесная роща', types:{grass:5,bug:5,poison:3,fairy:2,flying:1}, habitats:{2:4,3:2}},
  {id:'lake', name:'Озёрный берег', types:{water:4,normal:2,grass:1,ground:1}, habitats:{9:4,3:1}},
  {id:'coast', name:'Морское побережье', types:{water:4,flying:2,rock:1}, habitats:{7:4,9:1}},
  {id:'mountains', name:'Горные тропы', types:{rock:4,fighting:3,dragon:3,steel:2,ground:2,flying:1}, habitats:{1:3,4:4}},
  {id:'desert', name:'Пустынные дюны', types:{ground:4,rock:3,dark:2,fire:1}, habitats:{6:3}},
  {id:'volcano', name:'Вулканическая долина', types:{fire:5,steel:3,ground:2,rock:2,dragon:1}, habitats:{4:1}},
  {id:'snow', name:'Снежный перевал', types:{ice:6,water:1,normal:1,flying:1}, habitats:{}},
  {id:'ruins', name:'Древние руины', types:{ghost:5,psychic:4,dark:3,electric:3,steel:2,fairy:2}, habitats:{1:2,8:2}},
];
// Draft exceptions distinguish marine families and shoreline spirits from inland water.
const marineRoots = new Set([72,86,90,98,116,120,170,211,222,458,318,320,366,369,456,688,728,769]);
const byId = new Map(catalog.species.map(s => [s.id,s]));
const descriptionHints = {
  grove:/\b(forest|woodland|trees|branches|jungle)\b/i,
  lake:/\b(lakes?|rivers?|ponds?|freshwater|wetlands?)\b/i,
  coast:/\b(ocean|sea|seas|seabed|beaches?|seashore|coral)\b/i,
  mountains:/\b(mountains?|cliffs?|caves?|rocky)\b/i,
  desert:/\b(desert|dunes?|arid)\b/i,
  volcano:/\b(volcano|volcanic|lava|magma)\b/i,
  snow:/\b(snow|snowy|blizzards?|glaciers?|freezing)\b/i,
  ruins:/\b(ruins|graveyards?|cemeteries|power plants?|abandoned)\b/i,
};
function familyRoot(species) {
  const visited = new Set();
  while (species.species.evolves_from_species_id) {
    assert(!visited.has(species.id));
    visited.add(species.id);
    species = byId.get(Number(species.species.evolves_from_species_id));
    assert(species);
  }
  return species.id;
}
const coveredTypes = new Set(routes.flatMap(r => Object.keys(r.types)));
assert(catalog.species.every(s => s.types.some(t => coveredTypes.has(t))));
const species = catalog.species.map(s => {
  const ancestor = familyRoot(s);
  const text = [...new Set(s.descriptions.filter(d=>d.language_id==='9').map(d=>d.flavor_text))].join(' ');
  const hints = Object.entries(descriptionHints).filter(([,pattern])=>pattern.test(text)).map(([id])=>id);
  const ranked = routes.map((r, order) => {
    let score = s.types.reduce((sum,t) => sum + (r.types[t] ?? 0),0) + (r.habitats[s.species.habitat_id] ?? 0);
    if (hints.includes(r.id)) score += 2;
    if (r.id === 'coast' && marineRoots.has(ancestor)) score += 5;
    if (r.id === 'lake' && (marineRoots.has(ancestor) || s.species.habitat_id === '7')) score -= 3;
    return {id:r.id, score, order};
  }).sort((a,b) => b.score-a.score || a.order-b.order);
  assert(ranked[0].score > 0);
  const secondary = ranked[1].score >= 2 && ranked[1].score >= ranked[0].score * .6 ? ranked[1].id : null;
  return {id:s.id,name:s.species.identifier,rarity:rarity.get(s.id),normalAnimated:s.assets.normal.animated,
    types:s.types,primary:ranked[0].id,secondary,locationIds:[ranked[0].id,...(secondary?[secondary]:[])],
    basis:marineRoots.has(ancestor)?'draft-marine-family-override':s.species.habitat_id?'types-habitat-description':'types-description',
    descriptionHints:hints,
    needsReview:true};
});
assert.equal(species.length,1025);
assert.equal(new Set(species.map(s => s.id)).size,1025);
assert(species.every(s => s.rarity && s.locationIds.length >= 1 && s.locationIds.length <= 2));
assert.equal(species.find(s=>s.id===4).primary,'volcano');
assert.equal(species.find(s=>s.id===129).primary,'lake');
assert.equal(species.find(s=>s.id===728).primary,'coast');
assert.equal(species.find(s=>s.id===144).primary,'snow');
const summary = routes.map(r => {
  const all = species.filter(s=>s.locationIds.includes(r.id));
  const active = all.filter(s=>s.normalAnimated);
  assert(active.length);
  return {id:r.id,name:r.name,fullPool:all.length,bwPool:active.length,
    bwPrimary:active.filter(s=>s.primary===r.id).length,
    bwRarities:Object.fromEntries(['Common','Uncommon','Rare','Legendary'].map(t=>[t,active.filter(s=>s.rarity===t).length]))};
});
assert.equal(summary.reduce((sum,r)=>sum+r.bwPrimary,0),868);
const report = {status:'automatic-draft-needs-review',dataRevision:catalog.dataRevision,
  notes:'Game-specific routes, not canonical habitats. Scores choose locations, not encounter odds or team bonuses.',routes,summary,species};
await writeFile(new URL('location-draft.json',root),JSON.stringify(report,null,2)+'\n');
console.log(JSON.stringify({summary,oneLocation:species.filter(s=>!s.secondary).length,twoLocations:species.filter(s=>s.secondary).length}));
