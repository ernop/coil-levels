'use strict';
const fs=require('node:fs'),vm=require('node:vm'),assert=require('node:assert/strict'),path=require('node:path');
const root=path.resolve(__dirname,'..'),read=name=>fs.readFileSync(path.join(root,name),'utf8');
const context=vm.createContext({window:{}});
vm.runInContext(read('gallery/stats-lab-data.js'),context);
vm.runInContext(read('web/gallery-browser.js'),context);
vm.runInContext(read('web/stats-ledger.js'),context);
assert.equal(context.window.CoilStatsLedger.number(56.555555),'56.56');
assert.equal(context.window.CoilStatsLedger.number(123456789),'123,456,789');
assert.equal(context.window.CoilStatsLedger.number(.00014),'1.4e-4');
assert.equal(context.window.CoilStatsLedger.number(null),'—');
const boards=context.window.COIL_STATS_LAB.boards,match=context.window.CoilGalleryBrowser.matches;
const compare=context.window.CoilGalleryBrowser.compareBoards;
const traitValue=context.window.CoilGalleryBrowser.traitValue;
const squareFixture=[
  {id:'large',title:'Large',width:100,height:50,traits:{squares:10,walls:5}},
  {id:'small',title:'Small',width:4,height:3,traits:{squares:2,walls:1}}
];
assert.equal(traitValue(squareFixture[0],'squares'),20);
assert.equal(traitValue(squareFixture[0],'walls'),10);
assert.equal(traitValue(squareFixture[1],'squares'),200/3);
assert.deepEqual([...squareFixture].sort((a,b)=>compare(a,b,'squares','desc')).map(b=>b.id),['small','large'],'Square sorting compares relative side lengths across rectangular boards');
const orderFixture=[
  {id:'level-10',title:'Level 10',width:4,height:3,traits:{runs:2}},
  {id:'level-2',title:'Level 2',width:2,height:2,traits:{runs:null}},
  {id:'level-1',title:'Level 1',width:3,height:2,traits:{runs:5}}
];
for(const [key,order,expected] of [
  ['id','asc',['level-2','level-1','level-10']],
  ['id','desc',['level-10','level-1','level-2']],
  ['name','asc',['level-1','level-2','level-10']],
  ['runs','asc',['level-10','level-1','level-2']],
  ['runs','desc',['level-1','level-10','level-2']]
])assert.deepEqual([...orderFixture].sort((a,b)=>compare(a,b,key,order)).map(b=>b.id),expected);
const all={source:'all',method:'all',config:'all',collection:'all',size:'all',search:''};
assert.equal(boards.filter(b=>match(b,all)).length,4864);
const imported=JSON.parse(read('gallery/imported-boards.json'));
assert.equal(imported.legacyFiles,3306);assert.equal(imported.legacyOccurrences,5292);
assert.equal(imported.originalGameBoards,1208);
assert.deepEqual(imported.boards.filter(b=>b.id.startsWith('original-game/')).map(b=>Number(b.id.split('-').at(-1))),Array.from({length:1208},(_,i)=>i+1));
const configurations=new Map();
for(const board of boards) {
  const c=board.configuration;
  if(configurations.has(c.id))assert.equal(JSON.stringify(configurations.get(c.id).parameters),JSON.stringify(c.parameters),'A configuration must have identical recorded parameters across seeds');
  configurations.set(c.id,c);
  assert.ok(match(board,{...all,method:c.method,config:c.id,size:context.window.CoilGalleryBrowser.sizeKey(board),collection:board.collection,search:board.id}));
}
for(const c of configurations.values()) {
  const matches=boards.filter(b=>match(b,{...all,method:c.method,config:c.id}));
  assert.ok(matches.length>0);
  assert.ok(matches.every(b=>b.configuration.id===c.id));
}
assert.equal(boards.filter(b=>match(b,{...all,collection:'sampling'})).length,296);
assert.equal(boards.filter(b=>match(b,{...all,source:'original-game'})).length,1208);
assert.equal(boards.filter(b=>b.solutionAvailable).length,732);
assert.equal(boards.filter(b=>match(b,{...all,hasSolution:true})).length,732);
assert.equal(boards.filter(b=>match(b,{...all,hasSolution:true,source:'original-game'})).length,0);
assert.ok(boards.filter(b=>match(b,{...all,hasSolution:true,size:'1000'})).every(b=>b.solutionAvailable&&b.width===1000&&b.height===1000));
assert.equal(boards.filter(b=>b.width!==b.height).length,2803);
assert.equal(boards.filter(b=>match(b,{...all,collection:'legacy-generator'})).length,2924);
assert.equal(boards.filter(b=>match(b,{...all,collection:'saved-hard'})).length,12);
assert.equal(boards.filter(b=>match(b,{...all,size:'10000'})).length,4);
assert.equal(boards.filter(b=>match(b,{...all,search:'no-such-board'})).length,0);
for(const [url,expect] of [
  ['http://example/web/stats-lab.html?board=deep-v1%2Findependent-controls%2F32-legacy-horizontal-s2&zoom=12&x=16&y=12#viewer',{board:'deep-v1/independent-controls/32-legacy-horizontal-s2',zoom:'12',x:'16',y:'12'}],
  ['http://example/web/stats-lab.html?left=500-random-s101&metric=degree#all-stats',{left:'500-random-s101',metric:'degree',panel:'all-stats'}],
  ['http://example/web/board-wall.html',{size:'500',collection:'boards'}],
  ['http://example/web/board-wall.html?collection=sampling&size=all',{size:'all',collection:'sampling'}],
  ['http://example/web/gallery-guide.html?phase=survey&size=300#variation',{size:'300',collection:'survey',panel:'compare'}],
  ['http://example/web/gallery-guide.html#methods',{panel:'research'}]
]) {
  let result;const source=new URL(url);
  const scope=vm.createContext({URL,location:{href:url,pathname:source.pathname,search:source.search,hash:source.hash,replace:v=>result=v},document:{getElementById:()=>({href:''})}});
  vm.runInContext(read('web/gallery-redirect.js'),scope);
  const target=new URL(result);assert.equal(target.pathname,'/web/gallery.html');
  for(const [key,value] of Object.entries(expect))assert.equal(target.searchParams.get(key),value,`Preserve bookmarked ${key}`);
}
for(const name of ['stats-lab.html','board-wall.html','gallery-guide.html'])assert.ok(read('web/'+name).includes('gallery-redirect.js'),'Old entry points must converge on the same page');
const html=read('web/gallery.html'),ids=[...html.matchAll(/\bid="([^"]+)"/g)].map(m=>m[1]);
assert.equal(ids.length,new Set(ids).size,'Unified page must have unique control IDs');
for(const m of html.matchAll(/<(?:script|link)\b[^>]*(?:src|href)="([^"?]+)/g))assert.ok(fs.existsSync(path.resolve(root,'web',m[1])),`Missing dependency ${m[1]}`);
for(const retired of ['lab-board','lab-metric','lab-display','next-seed'])assert.ok(!ids.includes(retired),'Remove duplicate control '+retired);
assert.ok(html.includes('id="more-filters"')&&!html.includes('id="more-filters" open'),'Detailed filters start collapsed');
console.log(`Verified ${boards.length} selectable boards, ${configurations.size} parameter configurations, compound filters, six bookmark redirects, and unified-page dependencies.`);
