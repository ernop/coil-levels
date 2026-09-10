"""Pack every saved board for the unified gallery; exact cell records load on demand."""
import base64
import gzip
import hashlib
import json
from pathlib import Path
import statistics
from urllib.parse import parse_qs
from collections import defaultdict
from gallery_configurations import configuration
from PIL import Image
ROOT = Path(__file__).resolve().parents[1]

def traits(s):
    return {'occupancy': 100*s['openFraction'], 'density': s['tileDensity']['variance'],
            'runs': (s['horizontalRuns']['mean']+s['verticalRuns']['mean'])/2,
            'pillars': None if s['isolatedWallFraction'] is None else 100*s['isolatedWallFraction'], 'degree': 100*s['degreeCounts'][2]/s['openCells'],
            'squares': s['largestOpenSquare'][2], 'walls': s['largestWallSquare'][2],
            'interface': s['interfacePerOpen'], 'axis': s['axisBias'],
            'symmetry': s['symmetry']['mirrorX'], 'edge': 100*(s['edgeLayers'][0]['openFraction']-s['openFraction'])}

def main():
    catalog=json.loads((ROOT/'gallery/catalog.json').read_text())
    boards=[]
    output = ROOT / 'gallery/viewer-data'
    output.mkdir(exist_ok=True)
    for b in catalog['boards']:
        path = ROOT / b['assets']['board'][3:]
        text = (gzip.decompress(path.read_bytes()).decode() if path.suffix == '.gz' else path.read_text()).strip()
        assert hashlib.sha256(text.encode()).hexdigest() == b['boardSha256'], b['id']
        saved = json.loads((ROOT / b['assets']['stats'][3:]).read_text())
        assert saved['stats'] == b['stats'] and saved['boardSha256'] == b['boardSha256'], b['id']
        cells = parse_qs(text)['board'][0]
        assert len(cells) == b['width']*b['height']
        with Image.frombytes('L', (len(cells), 1), cells.encode('ascii')) as pixels:
            mask = pixels.point([255 if n == ord('X') else 0 for n in range(256)], mode='1').tobytes()
        mask = mask.translate(bytes(int(f'{n:08b}'[::-1], 2) for n in range(256)))
        solution = None
        if b['solutionAvailable']:
            solution_path = ROOT / b['assets']['solution'][3:]
            solution_text = (gzip.decompress(solution_path.read_bytes()).decode() if solution_path.suffix == '.gz' else solution_path.read_text()).strip()
            solution = {k: v[0] for k, v in parse_qs(solution_text, keep_blank_values=True).items()}
        filename = b['id'].replace('/', '--') + '.js'
        entry = {k: b[k] for k in ('id', 'side', 'width', 'height', 'source', 'solutionAvailable', 'seed', 'collection', 'path', 'assets', 'boardSha256')}
        entry.update(title=b.get('title',b['id'].split('/')[-1]),recipe=b['recipe']['id'], recordScript='../gallery/viewer-data/' + filename,
                     openCells=b['stats']['openCells'], openFraction=b['stats']['openFraction'],
                     configuration=configuration(saved), traits=traits(saved['stats']))
        packed = dict(entry, wallsBase64=base64.b64encode(mask).decode(), solution=solution,
                      metadata={k: v for k, v in saved.items() if k != 'stats'}, stats=saved['stats'])
        (output / filename).write_text('window.COIL_STATS_RECORDS[' + json.dumps(b['id']) + '] = ' + json.dumps(packed, separators=(',', ':')) + ';\n')
        boards.append(entry)
    ranges={}
    for scope in ('all','tweaks'):
        groups=defaultdict(list)
        for b in catalog['boards']:
            if b['phase']=='survey' and (scope=='all' or b['recipe']['family']=='tweak survey'):
                groups[b['recipe']['id']].append(traits(b['stats']))
        ranges[scope]={}
        for metric in traits(catalog['boards'][0]['stats']):
            rows=[{'recipe':name,'min':min(v[metric] for v in vs),'max':max(v[metric] for v in vs),
                   'mean':statistics.mean(v[metric] for v in vs)} for name,vs in groups.items()]
            ranges[scope][metric]={'configurations':len(groups),'minMean':min(r['mean'] for r in rows),
                'maxMean':max(r['mean'] for r in rows),'medianSeedRange':statistics.median(r['max']-r['min'] for r in rows),
                'widest':max(rows,key=lambda r:r['max']-r['min'])}
    result={'boards':boards,'ranges':ranges,'collections':catalog['collections'],'diagnostics':json.loads((ROOT/'gallery/deep-v1/diagnostics.json').read_text()),'scope':'Every saved board; descriptive survey ranges belong only to the original 300-square survey.'}
    (ROOT/'gallery/stats-lab-data.js').write_text('window.COIL_STATS_LAB = '+json.dumps(result,separators=(',',':'))+';\n')
    print(f'Packed {len(boards)} individually loaded exact boards; two survey comparison scopes')
if __name__=='__main__':main()
