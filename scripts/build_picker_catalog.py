"""Keep the offline picker demo synchronized with the selectable C# names."""
import argparse
import json
from pathlib import Path
import subprocess
ROOT=Path(__file__).resolve().parents[1]
p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');args=p.parse_args()
lines=subprocess.check_output(['dotnet',str(ROOT/'bin/Release/net10.0/coil-levels-csharp.dll'),'pickers'],text=True).splitlines()
data={'tweaks':sorted(lines[0].split(':',1)[1].split()),'segments':sorted(lines[1].split(':',1)[1].split())}
text='window.COIL_PICKERS = '+json.dumps(data)+';\n';path=ROOT/'web/pickers.js'
if args.check:
    if path.read_text()!=text:raise RuntimeError('Picker catalog is stale; run scripts/build_picker_catalog.py')
else:path.write_text(text)
print(len(data['tweaks']),'tweak names and',len(data['segments']),'segment names')
