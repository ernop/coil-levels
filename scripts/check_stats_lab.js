'use strict';
// Ensure every saved field is available in the inspector, with its original value.
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const assert = require('node:assert/strict');
const crypto = require('node:crypto');
const zlib = require('node:zlib');
const root = path.resolve(__dirname,'..');
const read = name => fs.readFileSync(path.join(root,name),'utf8');
const context = vm.createContext({window:{COIL_STATS_RECORDS:{}},atob:s=>Buffer.from(s,'base64').toString('binary')});
vm.runInContext(read('gallery/stats-lab-data.js'),context);
vm.runInContext(read('web/stats-ledger.js'),context);
vm.runInContext(read('web/board-geometry.js'),context);
const data = context.window.COIL_STATS_LAB;
const catalog = JSON.parse(read('gallery/catalog.json'));
const expected = catalog.boards.map(b=>b.id).sort();
assert.deepEqual(Array.from(data.boards,b=>b.id).sort(),expected,'Every saved specimen must be selectable');
for(const board of catalog.boards) for(const asset of Object.values(board.assets)) assert.ok(fs.existsSync(path.join(root,asset.slice(3))),`${board.id}: missing ${asset}`);
const sourceIds=[];
for(const collection of ['backward-v1','reversible-v1','deep-v1']) for(const entry of JSON.parse(read(`gallery/${collection}/manifest.json`))) sourceIds.push(collection+'/'+entry.id);
for(const source of ['sampling-v1/uniform-4x4','sampling-v1/chain-4x4','deep-v1/uniform-5x5','deep-v1/uniform-6x6','deep-v1/uniform-7x7']) {
  const batch=JSON.parse(read(`gallery/${source}/sampling.json`));
  for(const sample of batch.samples) sourceIds.push(source.split('/')[1]+'/sample-'+String(sample.index).padStart(4,'0'));
}
assert.deepEqual(catalog.boards.filter(b=>b.phase==='sampling').map(b=>b.id).sort(),sourceIds.sort(),'Every saved experimental draw must remain in the gallery');
const decode = s => s.replace(/&(amp|lt|gt|quot|#39);/g,(_,key)=>({amp:'&',lt:'<',gt:'>',quot:'"','#39':"'"}[key]));
function lookup(record,field) {return field.replace(/\[(\d+)\]/g,'.$1').split('.').reduce((value,key)=>value[key],record);}
function leaves(value,prefix='') {
  if(value===null||typeof value!=='object')return [prefix];
  return Object.entries(value).flatMap(([key,v])=>leaves(v,Array.isArray(value)?`${prefix}[${key}]`:prefix?`${prefix}.${key}`:key));
}
let fieldsChecked=0;
for(const entry of data.boards) {
  vm.runInContext(read(entry.recordScript.slice(3)),context);
  const board=context.window.COIL_STATS_RECORDS[entry.id];
  const saved=JSON.parse(read(board.assets.stats.slice(3)));
  assert.deepEqual(JSON.parse(JSON.stringify({...board.metadata,stats:board.stats})),saved,`${board.id}: packed record`);
  const boardPath=path.join(root,board.assets.board.slice(3));
  const text=(boardPath.endsWith('.gz')?zlib.gunzipSync(fs.readFileSync(boardPath)).toString():fs.readFileSync(boardPath,'utf8')).trim();
  assert.equal(crypto.createHash('sha256').update(text).digest('hex'),board.boardSha256,`${board.id}: hash`);
  const cells=text.split('&board=')[1],mask=Buffer.from(board.wallsBase64,'base64');
  assert.equal(cells.length,board.width*board.height);
  for(let i=0;i<cells.length;i++)if(((mask[i>>3]>>(i&7))&1)!==Number(cells[i]==='X'))throw new Error(`${board.id}: wrong cell ${i}`);
  if(board.side<=1000) {
  const geometry=context.window.CoilBoardGeometry.decode(board);
  const s=board.stats, degrees=[0,0,0,0,0], horizontal={}, vertical={};
  for(let y=0;y<board.height;y++) for(let x=0;x<board.width;x++) {
    const i=y*board.width+x;
    if(geometry.wall[i]) continue;
    degrees[geometry.degree[i]]++;
    if(x===0||geometry.wall[i-1]) horizontal[geometry.horizontal[i]]=(horizontal[geometry.horizontal[i]]||0)+1;
    if(y===0||geometry.wall[i-board.width]) vertical[geometry.vertical[i]]=(vertical[geometry.vertical[i]]||0)+1;
  }
  assert.deepEqual(degrees,Array.from(s.degreeCounts),`${board.id}: overlay degrees`);
  assert.deepEqual(horizontal,JSON.parse(JSON.stringify(s.horizontalRuns.histogram)),`${board.id}: horizontal runs`);
  assert.deepEqual(vertical,JSON.parse(JSON.stringify(s.verticalRuns.histogram)),`${board.id}: vertical runs`);
  if(board.solutionAvailable)assert.equal(geometry.visit.reduce((n,v)=>n+Number(v>0),0),s.openCells,`${board.id}: legal solution coverage`);else assert.equal(geometry.visit,null);
  const tileMean=geometry.tiles.reduce((a,b)=>a+b,0)/geometry.tiles.length;
  const variance=geometry.tiles.reduce((sum,p)=>sum+(p-tileMean)**2,0)/geometry.tiles.length;
  assert.ok(Math.abs(variance-s.tileDensity.variance)<1e-10,`${board.id}: overlay tile density`);
  } else {
    const geometry=context.window.CoilBoardGeometry.inspectLarge(board),w=board.width,h=board.height;
    for(let sample=0;sample<120;sample++) {
      const x=sample<4?(sample%2)*(w-1):(sample*7919)%w,y=sample<4?Math.floor(sample/2)*(h-1):(sample*104729)%h,i=y*w+x;
      const wall=j=>cells[j]==='X';
      assert.equal(geometry.wallAt(i),Number(wall(i)),`${board.id}: large crop cell`);
      const degree=(x>0&&!wall(i-1))+(x<w-1&&!wall(i+1))+(y>0&&!wall(i-w))+(y<h-1&&!wall(i+w));
      assert.equal(geometry.degreeAt(x,y),degree,`${board.id}: large degree overlay`);
      for(const v of [false,true]) {
        let count=0;
        if(!wall(i)){count=1;for(let p=(v?y:x)-1;p>=0&&!wall(i+((v?p-y:p-x)*(v?w:1)));p--)count++;for(let p=(v?y:x)+1;p<(v?h:w)&&!wall(i+((v?p-y:p-x)*(v?w:1)));p++)count++;}
        assert.equal(geometry.runAt(x,y,v),count,`${board.id}: large run crosses crop boundary`);
      }
      const sx=Math.floor(x/32)*32,sy=Math.floor(y/32)*32;let open=0,total=0;
      for(let yy=sy;yy<Math.min(sy+32,h);yy++)for(let xx=sx;xx<Math.min(sx+32,w);xx++){total++;open+=!wall(yy*w+xx);}
      assert.equal(geometry.tileAt(x,y),open/total,`${board.id}: large tile density`);
    }
    const finish=geometry.getFinish();if(board.solutionAvailable)assert.equal(cells[finish.y*w+finish.x],'.',`${board.id}: large legal solution finish`);else assert.equal(finish,null);
  }
  assert.ok(board.configuration.id&&board.configuration.parameters.generator,`${board.id}: recorded configuration`);
  const html=context.window.CoilStatsLedger.render(board),covered=[];
  for(const match of html.matchAll(/data-stat-path="([^"]*)" data-value="([^"]*)"/g)) {
    const field=decode(match[1]),value=JSON.parse(decode(match[2]));
    assert.deepEqual(value,lookup(saved,field),`${board.id}: displayed ${field}`);covered.push(field);fieldsChecked++;
  }
  const missing=leaves(saved).filter(field=>!covered.some(parent=>field===parent||field.startsWith(parent+'.')||field.startsWith(parent+'[')));
  assert.deepEqual(missing,[],`${board.id}: saved fields omitted from the ledger`);
  delete context.window.COIL_STATS_RECORDS[entry.id];
}
console.log(`Verified ${data.boards.length} boards: exact cells, replay of all supplied solutions, matching measurement overlays, complete saved records, and ${fieldsChecked.toLocaleString()} displayed field values; no omitted fields.`);
const research={innerHTML:''};
context.document={getElementById:()=>research};
vm.runInContext(read('web/sampling-research.js'),context);
context.window.CoilSamplingResearch.renderOverview(data);
let researchLinks=0;
for(const match of research.innerHTML.matchAll(/(?:href|src)="([^"]+)"/g)) {
  const url=new URL(decode(match[1]),'http://gallery.local/web/stats-lab.html');
  if(url.host!=='gallery.local')continue;
  assert.ok(fs.existsSync(path.join(root,decodeURIComponent(url.pathname))),`Missing research link ${url}`);
  if(url.pathname==='/web/gallery.html'&&url.searchParams.has('board')) assert.ok(expected.includes(url.searchParams.get('board')),`Unknown linked board ${url}`);
  researchLinks++;
}
console.log(`Verified all ${sourceIds.length} experimental draws, all catalog assets, and ${researchLinks} local research links.`);
