#!/usr/bin/env python3
"""Family-level nursery recipes (Georgy 12.11): every family has the same number of partner families,
every pair has the same number of possible babies, every baby hatches from the same number of pairs.

Rules
- a parent is an evolution family, a baby is a family root (D-32);
- two different families never hatch a baby of either family; one family hatches its own baby;
- partners share a type and stand at most one rarity step apart;
- a baby shares a type with its parents (the common one when it can), stands at most one rarity step
  from the rarer parent, and a legendary baby needs both parents rare or legendary;
- families whose root has no art yet get the same structure among themselves, so nothing a player can
  own today ever asks for a Pokemon that cannot be obtained.

Deterministic: the same catalog gives the same file. Usage: build-nursery-families.py [partners] [babies]
"""
import json, random, sys, collections, hashlib
from pathlib import Path

BASE = Path(__file__).resolve().parent
K = int(sys.argv[1]) if len(sys.argv) > 1 else 4
B = int(sys.argv[2]) if len(sys.argv) > 2 else 2
catalog = json.loads((BASE / 'final/runtime-catalog.json').read_text(encoding='utf-8'))
balance = json.loads((BASE / 'final/balance.json').read_text(encoding='utf-8'))
old = json.loads((BASE / 'nursery-recipes-draft.json').read_text(encoding='utf-8'))
ORDER = balance['rarityOrder']
species = {s['id']: s for s in catalog['species']}
members = collections.defaultdict(list)
for s in species.values(): members[s['familyRootId']].append(s)
roots = sorted(members)
types = {r: sorted({t for s in members[r] for t in s['types']}) for r in roots}
rarity = {r: ORDER.index(species[r]['rarity']) for r in roots}
BERRY = {}
for item in old['sameSpeciesRecipes']:
    BERRY.setdefault(species[item['outcomeSpeciesId']]['types'][0], next(iter(item['baseBerries'])))

def common(a, b): return [t for t in types[a] if t in types[b]]

def partner_ok(a, b, loose): return a != b and (loose or common(a, b)) and abs(rarity[a] - rarity[b]) <= (2 if loose else 1)

def baby_score(pair, baby, loose):
    a, b = pair
    if baby in pair: return 0
    top = max(rarity[a], rarity[b])
    if rarity[baby] == 3 and min(rarity[a], rarity[b]) < 2 and not loose: return 0
    if abs(rarity[baby] - top) > (2 if loose else 1): return 0
    shared = set(types[baby]) & set(common(a, b))
    either = set(types[baby]) & (set(types[a]) | set(types[b]))
    return 3 if shared else 2 if either else 1 if loose else 0

def partner_graph(nodes, rng, loose):
    need = {v: K for v in nodes}; edges = set()
    def cands(v): return [u for u in nodes if need[u] > 0 and partner_ok(v, u, loose) and (min(u, v), max(u, v)) not in edges]
    while True:
        open_nodes = [v for v in nodes if need[v] > 0]
        if not open_nodes: break
        options = {v: cands(v) for v in open_nodes}
        v = min(open_nodes, key=lambda x: (len(options[x]) - need[x], rng.random()))
        if not options[v]: break
        u = max(options[v], key=lambda x: (need[x], rarity[x] == rarity[v], len(common(v, x)), rng.random()))
        edges.add((min(u, v), max(u, v))); need[u] -= 1; need[v] -= 1
    # Repair: a node still short takes one end of an existing edge (x,y) -> (v,x) and (w,y) for another short node w.
    for _ in range(4000):
        short = [v for v in nodes if need[v] > 0]
        if not short: break
        v = short[0]; w = next((x for x in short[1:] if x != v), v if need[v] > 1 else None)
        if w is None: break
        pool = sorted(edges); rng.shuffle(pool)
        for x, y in pool:
            for p, q in ((x, y), (y, x)):
                e1 = (min(v, p), max(v, p)); e2 = (min(w, q), max(w, q))
                if len({v, w, p, q}) >= (3 if v == w else 4) and e1 != e2 and e1 not in edges and e2 not in edges \
                        and partner_ok(v, p, loose) and partner_ok(w, q, loose):
                    edges.remove((x, y)); edges.add(e1); edges.add(e2); need[v] -= 1; need[w] -= 1; break
            else: continue
            break
        else: break
    return edges, sum(need.values())

class Flow:
    def __init__(self, n): self.g = [[] for _ in range(n)]
    def add(self, a, b, cap): self.g[a].append([b, cap, len(self.g[b])]); self.g[b].append([a, 0, len(self.g[a]) - 1])
    def run(self, s, t):
        total = 0
        while True:
            level = [-1] * len(self.g); level[s] = 0; queue = [s]
            for a in queue:
                for b, cap, _ in self.g[a]:
                    if cap > 0 and level[b] < 0: level[b] = level[a] + 1; queue.append(b)
            if level[t] < 0: return total
            it = [0] * len(self.g)
            def push(a, f):
                if a == t: return f
                while it[a] < len(self.g[a]):
                    e = self.g[a][it[a]]
                    if e[1] > 0 and level[e[0]] == level[a] + 1:
                        got = push(e[0], min(f, e[1]))
                        if got: e[1] -= got; self.g[e[0]][e[2]][1] += got; return got
                    it[a] += 1
                return 0
            while True:
                got = push(s, 10 ** 9)
                if not got: break
                total += got

def assign(nodes, edges, loose):
    """Pairs take B babies, babies take K pairs. Best-scored links are offered first, weaker ones only if needed."""
    pairs = sorted(edges); index = {v: i for i, v in enumerate(nodes)}
    quota = {v: sum(1 for e in pairs if v in e) for v in nodes}  # a baby hatches from as many pairs as its family has
    for floor in (3, 2, 1):
        flow = Flow(2 + len(pairs) + len(nodes)); s, t = 0, 1
        for i, pair in enumerate(pairs):
            flow.add(s, 2 + i, B)
            for baby in nodes:
                if baby_score(pair, baby, loose) >= floor: flow.add(2 + i, 2 + len(pairs) + index[baby], 1)
        for v in nodes: flow.add(2 + len(pairs) + index[v], t, quota[v])
        sys.setrecursionlimit(10000)
        got = flow.run(s, t)
        if got == B * len(pairs) or floor == 1:
            out = {}
            for i, pair in enumerate(pairs):
                out[pair] = sorted(nodes[e[0] - 2 - len(pairs)] for e in flow.g[2 + i] if e[0] >= 2 + len(pairs) and e[1] == 0)
            return out, B * len(pairs) - got, floor
    raise AssertionError

def build(nodes, label):
    best = None
    for loose in (False, True):
        for seed in range(40):
            rng = random.Random(seed * 7919 + len(nodes))
            edges, short = partner_graph(nodes, rng, loose)
            babies, missing, floor = assign(nodes, edges, loose)
            score = (short + missing, -floor, loose, seed)
            if best is None or score < best[0]: best = (score, edges, babies)
            if short + missing == 0 and floor >= 2: break
        if best[0][0] == 0: break
    print(label, 'families', len(nodes), 'pairs', len(best[1]), 'unfilled', best[0][0], 'score floor', -best[0][1], 'loose', best[0][2], file=sys.stderr)
    return spread(best[2], best[0][2], 1 if best[0][2] else 2, random.Random(len(nodes)))

def spread(babies, loose, floor, rng):
    """A family should meet as many different babies as it can: swap babies between pairs while that grows."""
    seen = collections.defaultdict(collections.Counter)
    for pair, kids in babies.items():
        for parent in pair:
            for kid in kids: seen[parent][kid] += 1
    def gain(pair, old_kid, new_kid):
        return sum((seen[x][new_kid] == 0) - (seen[x][old_kid] == 1) for x in pair)
    pairs = sorted(babies)
    for _ in range(60):
        moved = 0
        rng.shuffle(pairs)
        for p in pairs:
            for x in list(babies[p]):
                if all(seen[parent][x] == 1 for parent in p): continue
                for q in pairs:
                    if q == p: continue
                    done = False
                    for y in list(babies[q]):
                        if y == x or y in babies[p] or x in babies[q]: continue
                        if baby_score(p, y, loose) < floor or baby_score(q, x, loose) < floor: continue
                        if gain(p, x, y) + gain(q, y, x) <= 0: continue
                        for parent in p: seen[parent][x] -= 1; seen[parent][y] += 1
                        for parent in q: seen[parent][y] -= 1; seen[parent][x] += 1
                        babies[p] = sorted(set(babies[p]) - {x} | {y}); babies[q] = sorted(set(babies[q]) - {y} | {x})
                        moved += 1; done = True; break
                    if done: break
        if not moved: break
    return babies

available = [r for r in roots if species[r]['normalAvailable']]
hidden = [r for r in roots if not species[r]['normalAvailable']]
result = {}
result.update(build(available, 'available'))
if len(hidden) > K: result.update(build(hidden, 'no art yet'))

weights = [int(w * 2) for w in balance['nurseryWeights']]
recipes = []
for (a, b), kids in sorted(result.items()):
    if not kids: continue
    shared = common(a, b)
    first = BERRY[species[a]['types'][0]]; second = BERRY[species[b]['types'][0]]
    berries = {first: 15} if first == second else {first: 9, second: 6}
    total = sum(weights[rarity[k]] for k in kids)
    recipes.append({
        'id': f'{a}-{b}', 'parentIds': [a, b], 'commonTypes': shared, 'baseBerries': berries,
        'selectedSurcharge': {first: balance['choiceExtraBerries']},
        'hours': balance['nurseryHours'][max(rarity[k] for k in kids)], 'plannedHours': balance['nurseryHours'][max(rarity[k] for k in kids)],
        'active': all(species[x]['normalAvailable'] for x in (a, b)), 'selectionUseful': len(kids) > 1,
        'outcomes': [{'speciesId': k, 'rarity': ORDER[rarity[k]], 'weight': weights[rarity[k]], 'probability': weights[rarity[k]] / total,
                      'active': species[k]['normalAvailable'], 'normalAvailable': species[k]['normalAvailable'],
                      'shinyAvailable': species[k]['shinyAvailable'], 'activeProbability': weights[rarity[k]] / total} for k in kids],
        'needsThemeReview': False})

out = dict(old)
out['status'] = 'family-level-regular-v3'
out['offspringRule'] = 'Only evolution roots. Parents are families: every recipe lists two family roots and stands for any stages of those families.'
out['draftChoices'] = {'partnersPerFamily': K, 'babiesPerPair': B, 'pairsPerBaby': K, 'partnersShareType': True,
                       'partnerRarityStep': 1, 'babyRarityStepFromRarerParent': 1, 'legendaryBabyNeedsRareParents': True,
                       'babyNeverFromParentFamilies': True, 'genderAndOfficialEggGroupsIgnored': True}
out['recipes'] = recipes
ids = {r['id'] for r in recipes}
out['acceptedStarterGifts'] = dict(old['acceptedStarterGifts'], candidateRecipeIds=sorted(
    r['id'] for r in recipes if r['active'] and all(rarity[p] <= 1 for p in r['parentIds'])))
(BASE / 'nursery-recipes-draft.json').write_text(json.dumps(out, ensure_ascii=False, indent=2) + '\n', encoding='utf-8', newline='\n')

# Report
deg = collections.Counter(); kids_of = collections.defaultdict(set); src = collections.Counter()
for r in recipes:
    for p in r['parentIds']: deg[p] += 1; kids_of[p] |= {o['speciesId'] for o in r['outcomes']}
    for o in r['outcomes']: src[o['speciesId']] += 1
def hist(values): return dict(sorted(collections.Counter(values).items()))
print('pairs', len(recipes), file=sys.stderr)
print('partners per family', hist(deg.get(r, 0) for r in roots), file=sys.stderr)
print('babies per family', hist(len(kids_of.get(r, ())) for r in roots), file=sys.stderr)
print('pairs per baby', hist(src.get(r, 0) for r in roots), file=sys.stderr)
print('babies per pair', hist(len(r['outcomes']) for r in recipes), file=sys.stderr)
easy = sum(1 for r in available if rarity[r] == 0 and any(x['active'] and max(rarity[p] for p in x['parentIds']) <= 1 and any(o['speciesId'] == r for o in x['outcomes']) for x in recipes))
print('common babies reachable from common/uncommon parents', easy, 'of', sum(1 for r in available if rarity[r] == 0), file=sys.stderr)
