import assert from 'node:assert/strict';
import { readFile, writeFile } from 'node:fs/promises';

const root = new URL('./', import.meta.url);
const catalog = JSON.parse(await readFile(new URL('catalog-package/catalog.json', root), 'utf8'));
const rarities = JSON.parse(await readFile(new URL('rarity-draft-v2.json', root), 'utf8'));
const ranks = { Common: 0, Uncommon: 1, Rare: 2, Legendary: 3 };
const byId = new Map(catalog.species.map(s => [s.id, s]));
const typeNames = [...new Set(rarities.species.flatMap(s => s.types))].sort();
const bit = new Map(typeNames.map((t, i) => [t, 1 << i]));
export const species = rarities.species.map(s => ({ ...s, rank: ranks[s.rarity], mask: s.types.reduce((m, t) => m | bit.get(t), 0),
  isBase:!byId.get(s.id).species.evolves_from_species_id,
  habitat: byId.get(s.id).species.habitat_id, shape: byId.get(s.id).species.shape_id,
  generation: Number(byId.get(s.id).species.generation_id) }));
assert.equal(species.length, 1025);
assert.equal(new Set(species.map(s => s.id)).size, 1025);
assert(species.every(s => s.mask && Number.isInteger(s.rank)));

export const pairs = [];
for (let i = 0; i < species.length; i++) {
  for (let j = i + 1; j < species.length; j++) {
    const a = species[i], b = species[j];
    const mask = a.mask & b.mask;
    if (!mask) continue;
    pairs.push({ id: `${a.id}-${b.id}`, a, b, mask, ceiling: Math.min(a.rank, b.rank) + 1,
      active: a.normalAnimated && b.normalAnimated });
  }
}
export const eligible = (pair, child) => pair.a.id !== child.id && pair.b.id !== child.id &&
  child.isBase && child.rank <= pair.ceiling && Boolean(pair.mask & child.mask);
assert(!eligible({ a:{id:1}, b:{id:2}, ceiling:1, mask:1 }, {id:1,rank:0,mask:1,isBase:true}));
assert(!eligible({ a:{id:1}, b:{id:2}, ceiling:1, mask:1 }, {id:3,rank:3,mask:1,isBase:true}));
assert(!eligible({ a:{id:1}, b:{id:2}, ceiling:3, mask:1 }, {id:3,rank:0,mask:2,isBase:true}));
assert(!eligible({ a:{id:1}, b:{id:2}, ceiling:3, mask:1 }, {id:3,rank:0,mask:1,isBase:false}));
assert(eligible({ a:{id:1}, b:{id:2}, ceiling:1, mask:1 }, {id:3,rank:1,mask:1,isBase:true}));

const candidates = species.filter(s=>s.isBase).map(child => {
  let full = 0, activeParents = 0, rareOnlyParents = 0, activeRareOnlyParents = 0;
  for (const pair of pairs) {
    if (!eligible(pair, child)) continue;
    full++;
    if (pair.active) activeParents++;
    if (pair.a.rank === 2 && pair.b.rank === 2) {
      rareOnlyParents++;
      if (pair.active) activeRareOnlyParents++;
    }
  }
  return { id:child.id, name:child.name, rarity:child.rarity, types:child.types, available:child.normalAnimated,
    full, activeParents, rareOnlyParents, activeRareOnlyParents };
});
const report = { status:'feasibility-audit-not-recipes', dataRevision:catalog.dataRevision,
  additionalDraftCriterion:'Every child shares at least one common parent type. This thematic selection criterion is stricter than the accepted parent compatibility rule, not a newly accepted rule.',
  potentialSharedTypePairs:pairs.length, activeSharedTypePairs:pairs.filter(p=>p.active).length,
  withoutCandidate:candidates.filter(s=>!s.full), activeWithoutActiveParents:candidates.filter(s=>s.available&&!s.activeParents),
  legendsWithoutActiveRarePair:candidates.filter(s=>s.rarity==='Legendary'&&s.available&&!s.activeRareOnlyParents),
  rarePairCountsByType:Object.fromEntries(typeNames.map(t=>[t,pairs.filter(p=>p.active&&p.a.rank===2&&p.b.rank===2&&(p.mask&bit.get(t))).length])),
  candidates };
await writeFile(new URL('nursery-feasibility.json',root), JSON.stringify(report,null,2)+'\n');
console.log(JSON.stringify({ potentialPairs:report.potentialSharedTypePairs, activePairs:report.activeSharedTypePairs,
  withoutCandidate:report.withoutCandidate, activeWithoutActiveParents:report.activeWithoutActiveParents,
  legendsWithoutActiveRarePair:report.legendsWithoutActiveRarePair,
  rarePairCountsByType:report.rarePairCountsByType }));
