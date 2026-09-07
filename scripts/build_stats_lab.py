"""Pack exact 500-square cell masks and descriptive survey ranges for the visual stats lab."""
import base64
import gzip
import hashlib
import json
from pathlib import Path
import statistics
from collections import defaultdict
ROOT = Path(__file__).resolve().parents[1]

def traits(s):
    return {'occupancy': 100*s['openFraction'], 'density': s['tileDensity']['variance'],
            'runs': (s['horizontalRuns']['mean']+s['verticalRuns']['mean'])/2,
            'pillars': 100*s['isolatedWallFraction'], 'degree': 100*s['degreeCounts'][2]/s['openCells'],
            'squares': s['largestOpenSquare'][2], 'walls': s['largestWallSquare'][2],
            'interface': s['interfacePerOpen'], 'axis': s['axisBias'],
            'symmetry': s['symmetry']['mirrorX'], 'edge': 100*(s['edgeLayers'][0]['openFraction']-s['openFraction'])}

def main():
    catalog=json.loads((ROOT/'gallery/catalog.json').read_text())
    boards=[]
    for b in catalog['boards']:
        if b['phase']!='boards' or b['side']!=500:continue
        path=ROOT/b['path'][3:]/'level.board.gz'
        text=gzip.decompress(path.read_bytes()).decode().strip()
        assert hashlib.sha256(text.encode()).hexdigest()==b['boardSha256']
        saved=json.loads(path.with_name('stats.json').read_text())
        assert saved['stats']==b['stats'] and saved['boardSha256']==b['boardSha256'], b['id']
        cells=text.split('&board=',1)[1]
        assert len(cells)==250000
        mask=bytearray((len(cells)+7)//8)
        for i,c in enumerate(cells):
            if c=='X':mask[i//8]|=1<<(i%8)
        boards.append({'id':b['id'],'recipe':b['recipe']['id'],'seed':b['seed'],'path':b['path'],
                       'side':500,'boardSha256':b['boardSha256'],'wallsBase64':base64.b64encode(mask).decode(),
                       'metadata':{k:v for k,v in saved.items() if k!='stats'},'stats':saved['stats']})
    ranges={}
    for scope in ('all','tweaks'):
        groups=defaultdict(list)
        for b in catalog['boards']:
            if b['phase']=='survey' and (scope=='all' or b['recipe']['family']=='tweak survey'):
                groups[b['recipe']['id']].append(traits(b['stats']))
        ranges[scope]={}
        for metric in traits(boards[0]['stats']):
            rows=[{'recipe':name,'min':min(v[metric] for v in vs),'max':max(v[metric] for v in vs),
                   'mean':statistics.mean(v[metric] for v in vs)} for name,vs in groups.items()]
            ranges[scope][metric]={'configurations':len(groups),'minMean':min(r['mean'] for r in rows),
                'maxMean':max(r['mean'] for r in rows),'medianSeedRange':statistics.median(r['max']-r['min'] for r in rows),
                'widest':max(rows,key=lambda r:r['max']-r['min'])}
    result={'boards':boards,'ranges':ranges,'scope':'Exact 500-square boards; descriptive ranges from the separate 300-square three-seed survey.'}
    (ROOT/'gallery/stats-lab-data.js').write_text('window.COIL_STATS_LAB = '+json.dumps(result,separators=(',',':'))+';\n')
    print(f'Packed {len(boards)} exact cell masks; two survey comparison scopes')
if __name__=='__main__':main()
