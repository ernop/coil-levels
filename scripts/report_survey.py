"""Summarize observed matched-seed effects; no inferential significance claims."""
import csv
import json
from pathlib import Path
import statistics
from collections import defaultdict
ROOT=Path(__file__).resolve().parents[1]
def main():
    configs=json.loads((ROOT/'gallery/survey-configs.json').read_text())
    data={}
    hashes=defaultdict(list)
    for p in (ROOT/'gallery/survey').glob('*/stats.json'):
        d=json.loads(p.read_text());cid=p.parent.name[4:].rsplit('-s',1)[0]
        data[cid,d['seed']]=d['stats'];hashes[d['boardSha256']].append(p.parent.name)
    def traits(s):return {'open_pp':s['openFraction']*100,'run_mean':(s['horizontalRuns']['mean']+s['verticalRuns']['mean'])/2,
        'density_variance':s['tileDensity']['variance'],'axis_bias':s['axisBias'],'largest_open_square':s['largestOpenSquare'][2]}
    rows=[]
    for c in configs:
        if c['family']=='tweak survey':continue
        for seed in (101,202,303):
            a=traits(data[c['id'],seed]);b=traits(data['tweak-'+c['picker'],seed])
            rows.append(dict(configuration=c['id'],baseline='tweak-'+c['picker'],seed=seed,**{k:a[k]-b[k] for k in a}))
    with (ROOT/'gallery/paired-effects.csv').open('w') as f:
        writer=csv.DictWriter(f,fieldnames=list(rows[0]));writer.writeheader();writer.writerows(rows)
    means={c['id']:{k:statistics.mean(traits(data[c['id'],s])[k] for s in (101,202,303)) for k in traits(next(iter(data.values())))} for c in configs}
    lines=['# Measured style survey — 2026-09-06','','All 330 generated 300-square boards passed the internal validator and independent slide-rule replay, including replay of the persisted gzip pairs. 110 configurations × seeds 101, 202, 303. No path search or DFS difficulty ranking was used for selection.','','## Observations','']
    for cid in ('tweak-rnd99','tweak-first','tweak-last','seg-2lim10-Last','control-rnd99-wanderfull','control-last-unlimited'):
        vals=[traits(data[cid,s]) for s in (101,202,303)]
        lines.append(f'- `{cid}`: open area {min(v["open_pp"] for v in vals):.2f}–{max(v["open_pp"] for v in vals):.2f}%; mean open run {min(v["run_mean"] for v in vals):.2f}–{max(v["run_mean"] for v in vals):.2f} cells. These ranges describe only the three observed seeds.')
    lines+=['','## Paired changes','','Changes below hold the seed and tweak picker fixed and compare to Weighted4 / lim20 / default initial walk and trimming. Values are the mean of three seed-wise differences, not confidence intervals. The shared seed does not guarantee the same random-call sequence after a method changes.','','| Variant | Open area change (percentage points) | Mean run change (cells) |','|---|---:|---:|']
    for cid in ('seg-rnd99-First','seg-rnd99-Last','seg-last-First','seg-2lim10-Last','control-rnd99-wanderfull','control-rnd99-onepass','control-rnd99-keepends','control-last-unlimited'):
        rs=[r for r in rows if r['configuration']==cid]
        lines.append(f'| `{cid}` | {statistics.mean(r["open_pp"] for r in rs):+.3f} | {statistics.mean(r["run_mean"] for r in rs):+.3f} |')
    lines+=['','Full per-seed changes: [paired-effects.csv](paired-effects.csv).','','## Identical outcomes','','There are '+str(len(hashes))+' distinct board hashes across 330 runs. The following recipe/seed sets coincide exactly; parameter names are not evidence of distinct output:','']
    for names in hashes.values():
        if len(names)>1:lines.append('- '+', '.join('`'+n+'`' for n in sorted(names)))
    lines+=['','## Coverage and limitations','','The first survey holds the candidate limit at 20 for all 53 selectable tweak names. Six tweak families are crossed with the other six segment pickers. Three tweak families receive seven extra control settings. This is not all 53 × 7 × all limits × all seed values.','','The strongest visual distinction in this sample is the full-length initial walk: very broad open regions can remain alongside fine texture. Ordinary recipes mostly vary over a narrower occupancy range, but run distributions, pillars, axis bias, and spatial density still distinguish them. Central crops alone can miss this large-scale arrangement.','','The 500-square unlimited-last exports took 51.6 and 122.2 seconds. The 1000-square attempt was stopped before export after several minutes; no board is claimed for it. Larger selected boards replace that recipe with `seg-2lim10-Last` (lim20), chosen by the same geometric coverage rule among finite-candidate alternatives.','','Next investigations: more seeds for high-spread configurations; more initial-walk lengths and starting positions; controlled spatial maps and motif spectra; bridge/room/proof analysis on selected manageable boards. These remain distinct from a ranking by search effort.','']
    lines += ['## Recorded scale limits', '', '| Attempt | Outcome | Budget (seconds) |', '|---|---|---:|']
    for p in sorted((ROOT/'gallery/attempts').glob('*.json')):
        a=json.loads(p.read_text());lines.append(f'| `{a["id"]}` | {a["status"]} | {a["budgetSeconds"]} |')
    lines += ['', 'Budget-exceeded attempts have no claimed specimen. The retained larger collection uses additional Weighted4/lim20 representatives selected for measured geometry coverage. The 10000-square series uses rnd99, full initial slides at two seeds, and len23. Generation budgets measure practicality in these runs, not mathematical impossibility or solver difficulty.', '']
    lines += ['## Saved large-board measurements', '', 'Sizes below are side lengths. Generation time includes generator validation but excludes artifact export. These are individual observations on this machine, not complexity estimates.', '', '| Specimen | Open area | Mean open run (cells) | Largest open square (side) | Generation seconds |', '|---|---:|---:|---:|---:|']
    for p in sorted((ROOT/'gallery/boards').glob('*/stats.json')):
        d=json.loads(p.read_text())
        if d['side']<5000:continue
        t=traits(d['stats'])
        lines.append(f'| `{p.parent.name}` | {t["open_pp"]:.3f}% | {t["run_mean"]:.3f} | {t["largest_open_square"]:,} | {d["generationSeconds"]:.1f} |')
    lines += ['', 'Compare full initial slides at seeds 101 and 303 to inspect variation within one method at 10000 square. Whole-board overviews matter here: a central crop can lie entirely inside a broad open region.', '']
    (ROOT/'gallery/FINDINGS.md').write_text('\n'.join(lines))
if __name__=='__main__':main()
