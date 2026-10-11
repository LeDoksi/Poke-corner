"""Reviewable balance tables and deterministic checks. Never reads application saves."""
import csv
import json
import math
from collections import Counter
from fractions import Fraction as F
from itertools import product
from pathlib import Path

ROOT=Path(__file__).resolve().parent
DOCS=ROOT.parent
def read(path):
    return json.loads((DOCS/path).read_text(encoding='utf-8'))
def write(name,value):
    (ROOT/name).write_text(json.dumps(value,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')

rarity=read('rarity-draft-v2.json')['species']
catalog=read('catalog-package/catalog.json')['species']
nursery=read('nursery-recipes-draft.json')
recipes=nursery['recipes']
berries=read('berry-profiles.json')
by_id={r['id']:r for r in rarity}
data={r['id']:r for r in catalog}
ranks={'Common':0,'Uncommon':1,'Rare':2,'Legendary':3}
berry_ids={b['id'] for b in berries['berries']}
assert len(berry_ids)==6 and all(b==b.lower() for b in berry_ids)
type_berry={'grass':'rawst','bug':'rawst','poison':'pecha','water':'oran','normal':'oran',
    'flying':'pecha','fairy':'pecha','rock':'chesto','fighting':'chesto','dragon':'chesto',
    'ground':'aspear','dark':'aspear','fire':'cheri','steel':'cheri','ice':'aspear',
    'ghost':'chesto','psychic':'chesto','electric':'oran'}
assert set(type_berry.values())<=berry_ids
assert all({l['primary'],*l['additional']}<=berry_ids for l in berries['locations'])
def shiny_usable(species_id):
    assets=data[species_id]['assets']
    return bool(assets['shiny']['animated'] and assets['shiny'].get('sha256') and
                assets['shiny']['sha256']!=assets['normal'].get('sha256'))
assert sum(r['normalAnimated'] and shiny_usable(r['id']) for r in rarity)==859
location_types={'grove':['grass','bug','poison'],'lake':['water','normal'],
    'coast':['water','flying','fairy'],'mountains':['rock','fighting','dragon'],
    'desert':['ground','dark'],'volcano':['fire','steel'],'snow':['ice','normal'],
    'ruins':['ghost','psychic','electric']}
xp=[]
for level in range(1,41):
    if level<=10: threshold=math.ceil(F((level-1)*1200,9))
    elif level<=20: threshold=1200+(level-10)*160
    elif level<=30: threshold=2800+(level-20)*200
    else: threshold=4800+(level-30)*160
    xp.append({'level':level,'totalXp':threshold,'baseHours':threshold/100})
assert [xp[i-1]['totalXp'] for i in [1,10,20,30,40]]==[0,1200,2800,4800,6400]
assert all(a['totalXp']<b['totalXp'] for a,b in zip(xp,xp[1:]))
traits={
    'gatherer':[.05,.10,.15],'treasure-hunter':[.05,.10,.15],'tracker':[.10,.15,.20],
    'finder':[.10,.15,.20],'mentor':[.05,.10,.15],'collector':[2,3,4],
    'relic-finder':[.01,.02,.03],'terrain-expert':[.05,.08,.12],
    'fast-grower':[.05,.10,.15],'rarity-expert':[1.1,1.2,1.3],'caring-parent':[.05,.08,.12]}
config={'schemaVersion':1,'status':'implementation-baseline-not-runtime',
    'acceptedSource':'../BALANCE-REVIEW.md','completionDefaultsSource':'BALANCE.md',
    'levels':xp,'traitTiersAt':[1,20,30],'secondTraitLevel':10,'traits':traits,
    'maxLevel':40,'maxXp':xp[-1]['totalXp'],'newIndividualLevel':1,'xpPerHour':100,
    'rarityOrder':list(ranks),'difficultyOrder':['easy','normal','hard'],
    'durationMinutes':[30,90,180,360],'recommendedLevels':[1,10,25],
    'resourceCompositionByMembers':[.8,1,1],'eventTeamSize':3,
    'maxActiveExpeditions':1,'maxActiveNests':1,'maxEncountersPerExpedition':1,
    'extraCheckMinutes':30,
    'coinsPerHour':[6,8,12],'primaryBerriesPerHour':[2,2,3],
    'minimumPrimaryBerriesPerHour':2,'primaryBerryFloorOrder':'after capped bonus, underlevel and composition; before duration',
    'completionDecisionAuthority':'user-delegated-audit-finalization-2026-10-11',
    'hourlyEncounterChance':[.03,.05,.07],'extraChancePerHalfHour':[.1,.2,.3],
    'expeditionRarityWeights':[[75,22,2.8,.2],[60,30,9,1],[45,35,17,3]],
    'trackerAppliesTo':'relative multiplier of final encounter probability','encounterChanceCap':.95,
    'extraLoot':{'berry':{'probability':.6,'quantity':1,'source':'uniform-route-additional'},
                 'coins':{'probability':.4,'quantityByDifficulty':[3,4,6]}},
    'relicLootWeights':{'evolutionStone':60,'offspringCharm':30,'luckyCharm':10},
    'eventChanceCap':.95,'underlevelPenaltyPerLevel':.025,
    'eventUnderlevelPenaltyPerLevel':.025,'maxUnderlevelPenalty':.5,
    'levelBonusPerLevel':.0025,'matchingTypeBonusPerMember':.03,'resourceBonusCap':.4,
    'stackWeights':[1,.5,.25],'maxExpeditionTimeReduction':.25,'maxNurseryTimeReduction':.2,
    'activityAcceleration':{'actionsPerStep':500,'reductionPerStep':.01,'maxReduction':.1},
    'rounding':'duration-times-minimum-hourly-rate-then-floor-with-persisted-rational-remainder',
    'expeditionStates':['running','ready','claimed'],'expeditionRelease':'on-confirmed-claim',
    'readyIsMonotonic':True,
    'activityBallActions':22500,'activityCoinActions':900,'activityTreatActions':3750,
    'activityNewChance':.8,'newAfterDuplicate':True,'activityRarityWeights':[60,30,9,1],
    'nurseryHours':[2,4,6,8],'nurseryWeights':[8,4,2,.5],
    'nurseryBerries':10,'choiceExtraBerries':4,'evolutionBerries':6,'retrainBerries':60,
    'shinyBase':1/512,'nurseryShinyIncrement':.0025,'nurseryShinyCap':.1,
    'shinyCharmMultiplier':4,'shinyChanceCap':1,
    'unusableShinyPolicy':{'chance':0,'consumeLuckyCharm':False,'increaseNurseryLuck':False,
                         'mixedOutcomes':'preserve selected actual species; apply that species availability'},
    'shopPrices':{'treats5':20,'careBooster7500':20,'luckyCharm':30,'evolutionStone':20,'offspringCharm':50,'xpPotion300':40,'berry1':5},
    'treatPackQuantity':5,'careBoosterPoints':7500,'xpPotionXp':300,
    'startingResources':{'coins':20,'treats':5,'compatibleGiftPokeballs':2},
    'rehome':{'coinsByRarity':[6,12,24,40],'levelBonusEvery':10,'coinsPerLevelStep':1,'shinyBonus':5},
    'care':{'evolutionCarePoints':[70000,196000],'feedingPoints':2250,'pettingPoints':110,
            'pettingCooldownSeconds':3,'hardcoreNeglectMinutes':360,'hardcoreReturnMinutes':60,
            'tangleStagesMinutes':[180,270,345],'reminderBeforeNeglectMinutes':5,'idleGraceMinutes':60},
    'trophyShelfSlots':3,
    'achievementCoins':{'chainTiers':[5,10,20,40],'specialDefault':5,'hardSpecial':10},
    'locations':[{**l,'bonusTypes':location_types[l['id']]} for l in berries['locations']],
    'typeBerries':type_berry}
assert config['eventUnderlevelPenaltyPerLevel']==config['underlevelPenaltyPerLevel']
assert config['rarityOrder']==['Common','Uncommon','Rare','Legendary']
assert all(len(row)==len(ranks) and all(w>0 for w in row) and sum(F(str(w)) for w in row)==100
           for row in config['expeditionRarityWeights'])
assert config['expeditionRarityWeights'][1]==config['activityRarityWeights']
assert F(str(config['expeditionRarityWeights'][0][-1]))/100==F(1,500)
assert sum(config['relicLootWeights'].values())==100
assert set(config['relicLootWeights'])<=set(config['shopPrices'])
assert sum(F(str(loot['probability'])) for loot in config['extraLoot'].values())==1
assert config['resourceCompositionByMembers']==[.8,1,1] and config['eventTeamSize']==3
assert config['shinyChanceCap']==1 and config['nurseryShinyCap']*config['shinyCharmMultiplier']==.4
acceleration=config['activityAcceleration']
assert F(str(acceleration['maxReduction']))/F(str(acceleration['reductionPerStep']))*acceleration['actionsPerStep']==5000
assert acceleration['maxReduction']<=config['maxExpeditionTimeReduction']
assert config['maxXp']==6400 and config['xpPotionXp']==300
assert config['care']['tangleStagesMinutes'][-1]<config['care']['hardcoreNeglectMinutes']
assert config['startingResources']['compatibleGiftPokeballs']==2 and config['maxActiveNests']==config['maxActiveExpeditions']==1
write('balance.json',config)

def stack(values):
    return sum((F(str(v))*F(str(weight)) for v,weight in zip(sorted(values,reverse=True),config['stackWeights'])),F(0))
def settle(value,remainder=F(0)):
    total=value+remainder
    return total.numerator//total.denominator,total%1
def encounter_chance(hours,difficulty,common,penalty,tracker_effects=()):
    hourly=min(config['encounterChanceCap'],config['hourlyEncounterChance'][difficulty]*(1+float(common))*float(penalty))
    final=1-(1-hourly)**float(hours)
    return min(config['encounterChanceCap'],final*(1+float(stack(tracker_effects))))
def result(minutes,count,level,difficulty,matches=0,berry_effects=(),coin_effects=()):
    hours=F(minutes,60)
    recommended=config['recommendedLevels'][difficulty]
    penalty=1-min(F(str(config['maxUnderlevelPenalty'])),max(0,recommended-level)*F(str(config['underlevelPenaltyPerLevel'])))
    composition=F(str(config['resourceCompositionByMembers'][count-1]))
    common=(level-1)*F(str(config['levelBonusPerLevel']))+matches*F(str(config['matchingTypeBonusPerMember']))
    berry_bonus=min(F(str(config['resourceBonusCap'])),common+stack(berry_effects))
    coin_bonus=min(F(str(config['resourceBonusCap'])),common+stack(coin_effects))
    b=hours*max(F(config['minimumPrimaryBerriesPerHour']),config['primaryBerriesPerHour'][difficulty]*(1+berry_bonus)*penalty*composition)
    c=hours*config['coinsPerHour'][difficulty]*(1+coin_bonus)*penalty*composition
    b_int,b_rem=settle(b);c_int,c_rem=settle(c)
    encounter=encounter_chance(hours,difficulty,common,penalty) if count==config['eventTeamSize'] else 0
    return {'minutes':minutes,'members':count,'level':level,'difficulty':config['difficultyOrder'][difficulty],
        'matchingMembers':matches,'guaranteedBerries':b_int,'guaranteedCoins':c_int,
        'berryRemainder':str(b_rem),'coinRemainder':str(c_rem),'xpEach':minutes*config['xpPerHour']//60,
        'encounterChance':round(encounter,6),'extraChecks':minutes//config['extraCheckMinutes'] if count==config['eventTeamSize'] else 0,
        'underlevelFactor':float(penalty)}

scenarios=[result(m,n,l,d,k) for m,n,l,d in product([30,90,180,360],[1,2,3],[1,10,25,40],[0,1,2]) for k in [0,n]]
assert len(scenarios)==288
assert all(r['guaranteedBerries']>=1 and r['guaranteedCoins']>=0 for r in scenarios)
assert all(r['encounterChance']==0 and r['extraChecks']==0 for r in scenarios if r['members']<3)
assert all(r['guaranteedBerries']>=6 for r in scenarios if r['minutes']==360 and r['difficulty']=='normal')
weak_hard=result(360,3,1,2);weak_normal=result(360,3,1,1)
assert weak_hard['guaranteedBerries']<=weak_normal['guaranteedBerries'] and weak_hard['guaranteedCoins']<weak_normal['guaranteedCoins']
assert stack([.15,.15,.15])==F(21,80)
# Fractional bonuses survive short expeditions; rewards are committed only once.
rem=F(0);paid=0
for _ in range(20):
    amount,rem=settle(F(21,20),rem);paid+=amount
assert paid==21 and rem==0
assert result(30,1,1,2)['guaranteedBerries']==1
assert result(360,3,40,2,3,[.15,.15,.15])['guaranteedBerries']<=25
# Compare totals AND carry at fixed starting conditions, rather than independent snapshots.
sequence_checks=0
for count,level,difficulty,matches,effects in product([1,2,3],[1,10,25,40],[0,1,2],[0,1],
        [(),(.05,),(.15,.15,.15)]):
    matches*=count
    long=result(360,count,level,difficulty,matches,effects,effects)
    for duration in [30,90,180]:
        small=result(duration,count,level,difficulty,matches,effects,effects)
        for reward,carry in [('guaranteedBerries','berryRemainder'),('guaranteedCoins','coinRemainder')]:
            paid=0;rem=F(2,7)
            exact=F(small[reward])+F(small[carry])
            for _ in range(360//duration):
                amount,rem=settle(exact,rem);paid+=amount
            assert (paid,rem)==settle(F(long[reward])+F(long[carry]),F(2,7))
        assert small['xpEach']*(360//duration)==long['xpEach']
        sequence_checks+=1
tracker_checks=0
for minutes,tier,difficulty,level,matches in product([30,90,180,360],[.10,.15,.20],[0,1,2],[1,10,25,40],[0,3]):
    common=F(level-1,400)+F(matches*3,100)
    penalty=max(F(1,2),1-F(max(0,[1,10,25][difficulty]-level),40))
    base=encounter_chance(F(minutes,60),difficulty,common,penalty)
    actual=encounter_chance(F(minutes,60),difficulty,common,penalty,[tier])
    assert math.isclose(actual,min(.95,base*(1+tier)),rel_tol=1e-12)
    tracker_checks+=1
assert encounter_chance(360,2,F(100),F(1),[100])==.95
assert math.isclose(encounter_chance(6,0,F(29,400),F(1),[.20]),.213806208,abs_tol=1e-8)
with (ROOT/'expedition-scenarios.csv').open('w',encoding='utf-8',newline='') as f:
    writer=csv.DictWriter(f,fieldnames=scenarios[0].keys());writer.writeheader();writer.writerows(scenarios)

edges=[]
for item in catalog:
    parent=int(item['species']['evolves_from_species_id'] or 0)
    if not parent: continue
    source=by_id[parent];target=by_id[item['id']]
    assert source['familyRootId']==target['familyRootId'] and source['rarity']==target['rarity']
    edges.append({'from':parent,'to':item['id'],'berry':type_berry[source['types'][0]],'quantity':config['evolutionBerries'],
        'normalAvailable':source['normalAnimated'] and target['normalAnimated'],
        'shinyAvailable':source['normalAnimated'] and target['normalAnimated'] and shiny_usable(parent) and shiny_usable(item['id'])})
assert len(edges)==484
assert all(e['berry'] in berry_ids for e in edges)
assert all(e['normalAvailable'] and not e['shinyAvailable'] for e in edges if (e['from'],e['to']) in [(651,652),(714,715)])
for parent in {e['from'] for e in edges}:
    assert len({e['berry'] for e in edges if e['from']==parent})==1
berry_order=[b['id'] for b in berries['berries']]
retrain=[]
for row in rarity:
    preferred=list(dict.fromkeys(type_berry[t] for t in row['types']))
    start=berry_order.index(preferred[0])
    for offset in range(1,7):
        berry=berry_order[(start+offset)%6]
        if berry not in preferred: preferred.append(berry)
    retrain.append({'speciesId':row['id'],'berries':{b:config['retrainBerries']//3 for b in preferred[:3]}})
assert all(len(r['berries'])==3 and sum(r['berries'].values())==60 for r in retrain)
assert all(set(r['berries'])<=berry_ids for r in retrain)
same_species=nursery['sameSpeciesRecipes']
assert len(same_species)==1025 and {r['speciesId'] for r in same_species}==set(by_id)
for row in same_species:
    parent=by_id[row['speciesId']];child=by_id[parent['familyRootId']]
    assert row['outcomeSpeciesId']==child['id']
    assert row['baseBerries']=={type_berry[child['types'][0]]:10}
    assert not row['selectedSurcharge'] and not row['selectionUseful'] and not row['offspringCharmRequired']
    assert row['normalAvailable']==(parent['normalAnimated'] and child['normalAnimated'])
    assert row['shinyAvailable']==(row['normalAvailable'] and shiny_usable(child['id']))
    assert row['hours']==([2,4,6,8][ranks[child['rarity']]] if row['normalAvailable'] else None)
legend_exposure=Counter();offspring=set();active_parents=set();same_family=[]
for recipe in recipes:
    a,b=(by_id[i] for i in recipe['parentIds'])
    shared=set(a['types'])&set(b['types'])
    assert shared and 2<=len(recipe['outcomes'])<=3
    assert sum(recipe['baseBerries'].values())==10 and sum(recipe['selectedSurcharge'].values())==4
    assert set(recipe['baseBerries'])|set(recipe['selectedSurcharge'])<=berry_ids
    assert sum(o['rarity']=='Legendary' for o in recipe['outcomes'])<=1
    active=[o for o in recipe['outcomes'] if o['active']]
    if active: assert math.isclose(sum(o['activeProbability'] for o in active),1)
    assert recipe['hours']==([2,4,6,8][max(ranks[o['rarity']] for o in active)] if active else None)
    if a['familyRootId']==b['familyRootId']: same_family.append(recipe['id'])
    for o in recipe['outcomes']:
        child=by_id[o['speciesId']]
        assert o['normalAvailable']==o['active']
        assert o['shinyAvailable']==(o['normalAvailable'] and shiny_usable(child['id']))
        assert child['id']==child['familyRootId'] and not data[child['id']]['species']['evolves_from_species_id']
        assert child['id'] not in recipe['parentIds'] and ranks[child['rarity']]<=min(ranks[a['rarity']],ranks[b['rarity']])+1
        assert set(child['types'])&shared
        offspring.add(child['id'])
        if child['rarity']=='Legendary': legend_exposure[child['id']]+=1
    if recipe['active']: active_parents.update(recipe['parentIds'])
assert len(offspring)==541 and all(v==1 for v in legend_exposure.values())
assert len(legend_exposure)==88 and len(active_parents)==868
routes=[]
location_draft=read('location-draft.json')
assert len(location_draft['species'])==1025
for location in config['locations']:
    pool=[r['id'] for r in location_draft['species'] if by_id[r['id']]['normalAnimated'] and location['id'] in r['locationIds']]
    assert pool
    routes.append({**location,'speciesIds':pool,'rarityCounts':dict(Counter(by_id[i]['rarity'] for i in pool))})
assert set().union(*(set(r['speciesIds']) for r in routes))=={r['id'] for r in rarity if r['normalAnimated']}
gift_candidates=[r for r in recipes if r['active'] and all(by_id[i]['rarity'] in ['Common','Uncommon'] for i in r['parentIds'])]
gift_candidates.sort(key=lambda r:(r['hours'],r['id']))
assert all(any(starter not in r['parentIds'] for r in gift_candidates) for starter in by_id)
write('catalog-rules.json',{'schemaVersion':1,'status':'deterministic-tables-for-implementation',
    'evolutionBerryRule':'source-primary-type; all branches share cost so berries cannot choose a random branch',
    'evolutionEdges':edges,'retraining':retrain,'routes':routes,
    'variantAvailability':'use field for current individual variant; shiny requires usable distinct asset; never downgrade or skip blocked stage',
    'nurseryRecipeSource':'../nursery-recipes-draft.json','routeAssignmentSource':'../location-draft.json',
    'giftRecipeCandidates':[r['id'] for r in gift_candidates],
    'giftSelection':'shortest active recipe; exclude selected starter/current existing species when possible; persist once before opening'})

# Actual chosen species governs luck/charm, including mixed supported/unsupported pools.
def nursery_shiny_attempt(species_id,luck=0,charm=False):
    eligible=shiny_usable(species_id)
    return {'speciesId':species_id,'shinyEligible':eligible,
        'chance':min(config['shinyChanceCap'],min(config['nurseryShinyCap'],config['shinyBase']+luck)*
                     (config['shinyCharmMultiplier'] if charm else 1)) if eligible else 0,
        'consumeLuckyCharm':eligible and charm,
        'normalHatchLuckIncrement':config['nurseryShinyIncrement'] if eligible else 0}
unsupported=nursery_shiny_attempt(870,.02,True)
assert unsupported['chance']==0 and not unsupported['consumeLuckyCharm'] and unsupported['normalHatchLuckIncrement']==0
mixed_checks=0
for recipe in recipes:
    active=[o for o in recipe['outcomes'] if o['active']]
    if {o['shinyAvailable'] for o in active}!={True,False}: continue
    for outcome in active:
        attempt=nursery_shiny_attempt(outcome['speciesId'],.02,True)
        assert attempt['speciesId']==outcome['speciesId']
        assert attempt['consumeLuckyCharm']==outcome['shinyAvailable']
        assert (attempt['normalHatchLuckIncrement']>0)==outcome['shinyAvailable']
    mixed_checks+=1
assert mixed_checks>0
# Exact distribution counts only attempts with a usable shiny resource.
survival=1.;expected=0.;median=None
for attempts in range(1,1501):
    expected+=survival
    chance=min(config['nurseryShinyCap'],config['shinyBase']+(attempts-1)*config['nurseryShinyIncrement'])
    survival*=1-chance
    if median is None and survival<=.5: median=attempts
assert survival<1e-60 and 15<expected<40 and 10<median<35
rehome=config['rehome']
max_rehome=max(rehome['coinsByRarity'])+config['maxLevel']//rehome['levelBonusEvery']*rehome['coinsPerLevelStep']+rehome['shinyBonus']
assert max_rehome==49<10*5
assert config['shopPrices']['xpPotion300']>math.ceil(config['xpPotionXp']/160)  # Potion cost exceeds even two level-bracket coin bonuses.
trophies=read('art-production/last-attempt/final-assets/manifest.json')['trophies']
specials=[r['id'] for r in trophies if r['category']!='chain']
hard_specials={'trophy-special-all-legends','trophy-special-family-expedition','trophy-special-evolved-expedition','trophy-special-all-berries','trophy-special-branch-choice','trophy-special-offspring-choice','trophy-special-retrain'}
assert len(specials)==16 and hard_specials<=set(specials)
coins={r:config['achievementCoins']['hardSpecial'] if r in hard_specials else config['achievementCoins']['specialDefault'] for r in specials}
normal_species_count=sum(r['normalAnimated'] for r in rarity)
normal_legend_count=sum(r['normalAnimated'] and r['rarity']=='Legendary' for r in rarity)
chain_goals={
    'species':[('discovered-species',n) for n in [1,25,100]]+[('all-normal-species-in-award-revision',normal_species_count)],
    'population':[('owned-individuals',n) for n in [3,10,30,100]],
    'catches':[('successful-catches',n) for n in [1,25,100,500]],
    'expeditions':[('claimed-expeditions',n) for n in [1,10,50,200]],
    'regions':[('visited-regions',n) for n in [1,4,8]]+[('hard-expedition-in-each-region',len(config['locations']))],
    'team':[('first-expedition-xp',1)]+[('individuals-at-required-level',3)]*3,
    'care':[('first-feeding',1),('first-care-evolution-ready',1)]+[('distinct-cared-individuals',n) for n in [10,50]],
    'nursery':[('distinct-hatched-root-species',n) for n in [1,5,15,30]],
    'evolution':[('distinct-species-transitions',n) for n in [1,5,20,50]],
    'shiny':[('distinct-shiny-species',n) for n in [1,3,10,30]]}
goal_rows=[{'awardId':f'trophy-{chain}-{tier}','condition':condition,'count':count,
            **({'minimumLevel':[10,20,40][tier-2]} if chain=='team' and tier>1 else {})}
           for chain,goals in chain_goals.items() for tier,(condition,count) in enumerate(goals,1)]
assert len(goal_rows)==40 and {r['awardId'] for r in goal_rows}=={r['id'] for r in trophies if r['category']=='chain'}
assert all(isinstance(r['count'],int) and r['count']>0 for r in goal_rows)
assert normal_species_count==868 and normal_legend_count==81
special_thresholds={
    'trophy-special-type-team':{'members':config['eventTeamSize'],'matchingMembers':config['eventTeamSize']},
    'trophy-special-all-berries':{'distinctBerryIds':len(berry_ids)},
    'trophy-special-second-trait':{'level':config['secondTraitLevel']},
    'trophy-special-all-legends':{'speciesCount':normal_legend_count,'countSource':'available-normal-legendary-species-at-award-revision'}}
assert set(special_thresholds)<=set(specials)
write('achievement-rewards.json',{'chainTiers':config['achievementCoins']['chainTiers'],'specials':coins,
    'chainGoals':goal_rows,'specialMinimumOccurrences':1,'specialThresholds':special_thresholds,
    'catalogCountsAreAwardRevisionSnapshots':True,'oneTimePerAwardId':True})
report={'status':'passed-mathematical-preparation-not-live-game',
    'scenarios':len(scenarios),'xpLevels':40,'evolutionEdges':len(edges),'retrainRecipes':len(retrain),
    'nurseryPairs':len(recipes),'rootOffspring':len(offspring),'legendaryRootsWithExactlyOneRecipe':len(legend_exposure),
    'activeParentsWithPartner':len(active_parents),'routesCoverAll868':True,
    'sameFamilyDifferentSpeciesPairs':len(same_family),
    'themeMethod':'all recipes share parent/child type; same-family pairs allowed by different-species rule; individual visual-lore approval not inferred',
    'giftCandidatePairs':len(gift_candidates),'giftShortestHours':gift_candidates[0]['hours'],
    'normalSixHoursMinimumBerries':min(r['guaranteedBerries'] for r in scenarios if r['minutes']==360 and r['difficulty']=='normal'),
    'canonicalBerryIdsVerified':True,'sameSpeciesRecipes':len(same_species),
    'fixedConditionsDurationSequenceChecks':sequence_checks,'trackerFinalProbabilityChecks':tracker_checks,
    'variantEvolutionAvailabilityChecks':True,'unsupportedShinyDoesNotAdvanceLuck':True,
    'mixedShinyEligibilityRecipesChecked':mixed_checks,'shinyMeanPopulation':'usable-shiny attempts only',
    'validatedShinySpecies':859,'normalSpeciesWithUnavailableShiny':9,
    'acceptedNumericConstantsMaterialized':True,'achievementGoalRows':len(goal_rows),
    'maxRehomeCoins':max_rehome,'minimumBoughtNestCoins':50,'roundingCarrySelfCheck':True,
    'activityMeanNewFractionWhileBothPoolsExist':1/(2-config['activityNewChance']),
    'activityMeanEncountersPerNew':2-config['activityNewChance'],
    'firstNurseryShinyMeanAttempts':round(expected,3),'firstNurseryShinyMedianAttempts':median,
    'nurseryThirtyDistinctBestCaseHours':chain_goals['nursery'][-1][1]*min(config['nurseryHours'])*(1-config['maxNurseryTimeReduction']),
    'nurseryThirtyDistinctBerryCostAtLeast':chain_goals['nursery'][-1][1]*config['nurseryBerries'],
    'fixedRoutePrimaryBerryHoursFor300':chain_goals['nursery'][-1][1]*config['nurseryBerries']//config['primaryBerriesPerHour'][1],
    'awardsTotalCoins':len(chain_goals)*sum(config['achievementCoins']['chainTiers'])+sum(coins.values()),
    'finiteRewardIssuance':True,'noDecayOrDailyCap':True,
    'limitations':['No assumed real-world actions/hour','No manual lore approval of every pair',
      'No runtime/save integration or real player-session simulation','Coin accumulation has no endless mandatory sink; acceptable in a cozy collection game']}
write('balance-checks.json',report)
print(json.dumps(report,ensure_ascii=False))
