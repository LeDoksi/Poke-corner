import assert from 'node:assert/strict';
import { readFile, writeFile } from 'node:fs/promises';

const root = new URL('./', import.meta.url);
const catalog = JSON.parse(await readFile(new URL('catalog-package/catalog.json', root), 'utf8'));
const species = new Map(catalog.species.map(s => [s.id, s]));
const voices = new Map(JSON.parse(await readFile(new URL('voice-preview/selected-sources.json', root), 'utf8')).map(s => [s.id, s]));
const families = new Map();
for (const s of species.values()) {
  let ancestor = s;
  const seen = new Set();
  while (ancestor.species.evolves_from_species_id) {
    assert(!seen.has(ancestor.id), `Evolution cycle: ${s.id}`);
    seen.add(ancestor.id);
    ancestor = species.get(Number(ancestor.species.evolves_from_species_id));
    assert(ancestor, `Missing parent: ${s.id}`);
  }
  if (!families.has(ancestor.id)) families.set(ancestor.id, []);
  families.get(ancestor.id).push(s);
}
const uncommonFamilies = new Set([
  1,4,7,152,155,158,252,255,258,387,390,393,495,498,501,650,653,656,
  722,725,728,810,813,816,906,909,912,133,
  138,140,142,345,347,408,410,564,566,696,698,880,881,882,883,
]);
const ordinaryRarity = rate => rate >= 150 ? 'Common' : rate >= 60 ? 'Uncommon' : 'Rare';
assert.equal(ordinaryRarity(150), 'Common');
assert.equal(ordinaryRarity(149), 'Uncommon');
assert.equal(ordinaryRarity(60), 'Uncommon');
assert.equal(ordinaryRarity(59), 'Rare');
assert.equal(ordinaryRarity(3), 'Rare');
const rows = [];
const familyRows = [];
assert([...uncommonFamilies].every(id => families.has(id)), 'Override must name a family root');
for (const [ancestorId, members] of families) {
  const ancestor = species.get(ancestorId);
  const flags = members.map(s => s.species.is_legendary === '1' || s.species.is_mythical === '1');
  assert(flags.every(Boolean) || flags.every(f => !f), `Mixed legendary flags: ${ancestorId}`);
  const captureRate = Number(ancestor.species.capture_rate);
  assert(Number.isInteger(captureRate) && captureRate >= 0 && captureRate <= 255);
  const override = !flags[0] && uncommonFamilies.has(ancestorId);
  const rarity = flags[0] ? 'Legendary' : override ? 'Uncommon' : ordinaryRarity(captureRate);
  const basis = flags[0] ? 'legendary-or-mythical-flag' : override ? 'explicit-uncommon-family' : 'family-root-capture-rate';
  familyRows.push({rootId: ancestorId, rootName: ancestor.species.identifier, rarity, basis, captureRate, memberIds: members.map(s => s.id)});
  for (const s of members) {
    rows.push({id: s.id, name: s.species.identifier, familyRootId: ancestorId, rarity,
      basis,
      normalAnimated: s.assets.normal.animated, shinyAnimated: s.assets.shiny.animated,
      voiceSource: voices.get(s.id)?.source ?? null, types: s.types});
  }
}
assert.equal(rows.length, 1025);
assert.equal(new Set(rows.map(s => s.id)).size, rows.length);
const byId = new Map(rows.map(s => [s.id, s]));
for (const s of species.values()) {
  if (s.species.evolves_from_species_id) assert.equal(byId.get(s.id).rarity, byId.get(Number(s.species.evolves_from_species_id)).rarity);
  assert.equal(byId.get(s.id).rarity === 'Legendary', s.species.is_legendary === '1' || s.species.is_mythical === '1');
}
assert.equal(byId.get(1).rarity, byId.get(3).rarity);
assert.equal(byId.get(133).rarity, byId.get(470).rarity);
assert.equal(byId.get(151).rarity, 'Legendary');
assert.equal(byId.get(1).rarity, 'Uncommon');
assert.equal(byId.get(133).rarity, 'Uncommon');
assert.equal(byId.get(138).rarity, 'Uncommon');
assert.equal(byId.get(374).rarity, 'Rare');
const summary = Object.fromEntries(['Common', 'Uncommon', 'Rare', 'Legendary'].map(rarity => [rarity, {
  species: rows.filter(s => s.rarity === rarity).length,
  families: familyRows.filter(f => f.rarity === rarity).length,
  bwAvailable: rows.filter(s => s.rarity === rarity && s.normalAnimated).length,
  withoutBw: rows.filter(s => s.rarity === rarity && !s.normalAnimated).length,
}]));
const draft = {status: 'draft-for-balance', dataRevision: catalog.dataRevision,
  rules: 'One rarity per family; flags select Legendary including Mythical. Starter/Eevee/fossil families explicitly Uncommon. Other root capture_rate >=150 Common, >=60 Uncommon, otherwise Rare.',
  summary, families: familyRows.sort((a,b) => a.rootId-b.rootId), species: rows.sort((a,b) => a.id-b.id)};
await writeFile(new URL('rarity-draft-v2.json', root), JSON.stringify(draft, null, 2) + '\n');
console.log(JSON.stringify({families: familyRows.length, summary, examples: [1,3,25,26,133,470,150,151,374,376,789,792,1000].map(id => byId.get(id))}));
