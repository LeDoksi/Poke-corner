// Read-only source inventory: file presence is not proof of animation quality.
import assert from 'node:assert/strict';
import { writeFile } from 'node:fs/promises';
const get = async url => {
  const r = await fetch(url, { headers: { 'User-Agent': 'PokeCorner-catalog-audit' }, signal: AbortSignal.timeout(60000) });
  if (!r.ok) throw new Error(`${r.status} ${url}`);
  return r.json();
};
const speciesUrl = 'https://pokeapi.co/api/v2/pokemon-species?limit=2000';
const treeUrl = 'https://api.github.com/repos/PokeAPI/sprites/git/trees/master';
const [species, tree] = await Promise.all([get(speciesUrl), get(treeUrl)]);
assert.equal(species.next, null, 'Species pagination incomplete');
assert.equal(species.results.length, species.count, 'Species count mismatch');
assert.equal(tree.truncated, false, 'Repository tree truncated');
const ids = species.results.map(s => Number(s.url.match(/\/(\d+)\/$/)[1]));
assert(ids.every(Number.isInteger));
const treeCache = new Map([['', Promise.resolve(tree)]]);
function directory(path) {
  if (!treeCache.has(path)) treeCache.set(path, (async () => {
    const parts = path.split('/');
    const name = parts.pop();
    const parent = await directory(parts.join('/'));
    const entry = parent.tree.find(x => x.type === 'tree' && x.path === name);
    assert(entry, `Missing directory: ${path}`);
    const result = await get(`https://api.github.com/repos/PokeAPI/sprites/git/trees/${entry.sha}`);
    assert.equal(result.truncated, false, `Truncated directory: ${path}`);
    return result;
  })());
  return treeCache.get(path);
}
const files = new Map();
const groups = {
  bw: 'sprites/pokemon/versions/generation-v/black-white/animated/',
  bwShiny: 'sprites/pokemon/versions/generation-v/black-white/animated/shiny/',
  showdown: 'sprites/pokemon/other/showdown/',
  showdownShiny: 'sprites/pokemon/other/showdown/shiny/',
};
await Promise.all(Object.values(groups).map(async prefix => {
  const listing = await directory(prefix.slice(0, -1));
  for (const entry of listing.tree.filter(x => x.type === 'blob')) files.set(prefix + entry.path, entry);
}));
const report = { checkedAt: new Date().toISOString(), speciesUrl, treeUrl, treeSha: tree.sha, speciesCount: species.count, caveat: 'Species-ID file presence only. Default-form ID mapping, GIF frame counts, visual style, voices and reuse terms not verified.', groups: {} };
for (const [name, prefix] of Object.entries(groups)) {
  const present = ids.filter(id => files.has(`${prefix}${id}.gif`));
  report.groups[name] = { present: present.length, bytes: present.reduce((n,id) => n + files.get(`${prefix}${id}.gif`).size, 0), missing: ids.filter(id => !files.has(`${prefix}${id}.gif`)) };
}
report.bwBoth = ids.filter(id => files.has(`${groups.bw}${id}.gif`) && files.has(`${groups.bwShiny}${id}.gif`)).length;
report.showdownBoth = ids.filter(id => files.has(`${groups.showdown}${id}.gif`) && files.has(`${groups.showdownShiny}${id}.gif`)).length;
await writeFile(new URL('./catalog-coverage-audit.json', import.meta.url), JSON.stringify(report, null, 2) + '\n');
console.log(JSON.stringify(report));
