"""Choose coverage representatives in measured descriptor space, never a difficulty score."""
import itertools
import json
import math
from pathlib import Path
import statistics
ROOT=Path(__file__).resolve().parents[1]
FEATURES=['openFraction','isolatedWallFraction','interfacePerOpen','axisBias','runMean','log1pRunMax','densityVariance','wallSquare','openSquare','edgeContrast']
def vector(s):
    return [s['openFraction'],s['isolatedWallFraction'] or 0,s['interfacePerOpen'] or 0,s['axisBias'] or 0,
        (s['horizontalRuns']['mean']+s['verticalRuns']['mean'])/2,
        math.log1p(max(s['horizontalRuns']['max'],s['verticalRuns']['max'])),
        s['tileDensity']['variance'],s['largestWallSquare'][2],s['largestOpenSquare'][2],
        s['edgeLayers'][0]['openFraction']-s['openFraction']]
def main():
    configs=json.loads((ROOT/'gallery/survey-configs.json').read_text())
    results=json.loads((ROOT/'gallery/survey-results.json').read_text())
    if len(results)!=len(configs)*3:raise RuntimeError('Survey not complete')
    complete={r['id'] for r in results if r['status']=='complete'}
    groups={}
    for c in configs:
        paths=[ROOT/f'gallery/survey/300-{c["id"]}-s{seed}/stats.json' for seed in (101,202,303)]
        if not all(p.parent.name in complete for p in paths):continue
        rows=[json.loads(p.read_text()) for p in paths]
        groups[c['id']]={'config':c,'rows':rows,'mean':[statistics.mean(v) for v in zip(*(vector(r['stats']) for r in rows))]}
    lo=[min(g['mean'][i] for g in groups.values()) for i in range(len(FEATURES))]
    hi=[max(g['mean'][i] for g in groups.values()) for i in range(len(FEATURES))]
    def norm(v):return [(x-a)/(b-a) if b!=a else 0 for x,a,b in zip(v,lo,hi)]
    def dist(a,b):return sum((x-y)**2 for x,y in zip(a,b))
    for g in groups.values():g['normalized']=norm(g['mean'])
    chosen=['tweak-rnd99','control-rnd99-wanderfull']
    while len(chosen)<8:
        candidate=max((k for k in groups if k not in chosen),key=lambda k:min(dist(groups[k]['normalized'],groups[c]['normalized']) for c in chosen))
        chosen.append(candidate)
    selected=[]
    for k in chosen:
        g=groups[k]
        pair=max(itertools.combinations(g['rows'],2),key=lambda pair:dist(norm(vector(pair[0]['stats'])),norm(vector(pair[1]['stats']))))
        selected.append(dict(config=g['config'],seeds=[r['seed'] for r in pair],meanFeatures=dict(zip(FEATURES,g['mean']))))
    # The completed 500-square runs exposed a scaling cost absent from geometry alone.
    scale_selected = list(selected)
    costly = next(i for i,g in enumerate(scale_selected) if g['config']['id']=='control-last-unlimited')
    retained = [g['config']['id'] for i,g in enumerate(scale_selected) if i!=costly]
    alternative = max((k for k in groups if k not in chosen and groups[k]['config']['lim']!='none'),
        key=lambda k:min(dist(groups[k]['normalized'],groups[c]['normalized']) for c in retained))
    g=groups[alternative]
    pair=max(itertools.combinations(g['rows'],2),key=lambda pair:dist(norm(vector(pair[0]['stats'])),norm(vector(pair[1]['stats']))))
    scale_selected[costly]=dict(config=g['config'],seeds=[r['seed'] for r in pair],meanFeatures=dict(zip(FEATURES,g['mean'])))
    largest_selected = list(scale_selected)
    short_index = next(i for i,g in enumerate(largest_selected) if g['config']['id']=='seg-equal23short-First')
    g=groups['seg-equal23short-Longest']
    pair=max(itertools.combinations(g['rows'],2),key=lambda pair:dist(norm(vector(pair[0]['stats'])),norm(vector(pair[1]['stats']))))
    largest_selected[short_index]=dict(config=g['config'],seeds=[r['seed'] for r in pair],meanFeatures=dict(zip(FEATURES,g['mean'])))
    dense = dict(config=groups['tweak-len23']['config'], seeds=[101], meanFeatures=dict(zip(FEATURES,groups['tweak-len23']['mean'])))
    ten_thousand = [dict(scale_selected[0],seeds=[101]),dict(scale_selected[1],seeds=[101,303]),dense]
    attempts=[json.loads(p.read_text()) for p in sorted((ROOT/'gallery/attempts').glob('*.json'))]
    excluded=[a['id'] for a in attempts if a['status']=='generation_budget_exceeded']
    anchors=['tweak-rnd99','control-rnd99-wanderfull','tweak-sz1-23','seg-first-Longest','tweak-len23']
    extra=[]
    for _ in range(3):
        k=max((k for k,g in groups.items() if k not in anchors and g['config']['family']=='tweak survey' and g['config']['picker'] not in ('equal23short','sz23-opt')),
              key=lambda k:min(dist(groups[k]['normalized'],groups[c]['normalized']) for c in anchors))
        anchors.append(k);g=groups[k]
        extra.append(dict(config=g['config'],seeds=[101],meanFeatures=dict(zip(FEATURES,g['mean']))))
    record=dict(extraCoverageDecision="After ordering-cost limits, add three further Weighted4/lim20 survey representatives by farthest-point mean geometry coverage from the five practical anchors. Avoid the two Len1-capped pickers in this additional scale probe. Retain their small-board survey results.", excludedAtScale=excluded, scalingAttempts=attempts, tenThousandSelected=ten_thousand, additionalFiveThousand=[dense]+extra, tenThousandDecision="The ordering-heavy 5000-square probes each exceeded eight minutes. len23/Weighted4/lim20 produced 71.464% open area at 5000 in 104.4 seconds total (51.6 seconds generation), so use it as the dense 10000 representative. Include both full-walk seeds 101 and 303 to preserve the within-method contrast: four 10000 boards across three configurations.", largestSelected=largest_selected, largestScalingDecision="equal23short/First took 241.7 seconds to export 2000-square seed101. Retain the 2000 specimens; at 5000 use the same tweak picker with Longest segment ordering, a separately measured survey configuration. The 10000 series has its own cost-informed selection.", scaleSelected=scale_selected, scalingDecision="Unlimited last picker retained at 300/500. The 500-square exports took 51.6 and 122.2 seconds; the 1000-square seed101 attempt was stopped before export after several minutes. For 1000 and above, substitute the farthest measured finite-candidate configuration from the seven retained representatives. No board claimed for the stopped attempt.", method='Two anchors (rnd99 baseline and full initial slides), then greedy farthest-point coverage of configuration mean vectors. Maximum run length uses log1p; each feature is then scaled by its observed configuration-mean range. Eight configurations; most separated pair of observed seeds per configuration. This is a descriptive coverage heuristic, not a hardness score or proof of independent controls.',features=FEATURES,minimum=lo,maximum=hi,selected=selected)
    (ROOT/'gallery/selection.json').write_text(json.dumps(record,indent=2)+'\n')
    print(json.dumps(selected,indent=2))
if __name__=='__main__':main()
