'use strict';
const fs=require('node:fs'),vm=require('node:vm'),assert=require('node:assert/strict'),path=require('node:path');
const context=vm.createContext({window:{}});
vm.runInContext(fs.readFileSync(path.join(__dirname,'../web/stats-ledger.js'),'utf8'),context);
// Rectangular fixture: ..#. / ...# / ####. The two run populations differ.
const stats={width:4,height:3,openCells:6,openFraction:.5,degreeCounts:[1,1,3,1,0],
  isolatedWallFraction:1/6,interfacePerOpen:7/3,axisBias:.2,
  horizontalRuns:{count:3,mean:2,max:3,histogram:{1:1,2:1,3:1}},
  verticalRuns:{count:4,mean:1.5,max:2,histogram:{1:2,2:2}},
  openSquareCounts:{1:6,2:1},wallSquareCounts:{1:6},largestOpenSquare:[0,0,2],largestWallSquare:[2,0,1],
  tileDensity:{side:32,columns:1,rows:1,min:.5,max:.5,variance:0},
  symmetry:{mirrorX:0,mirrorY:0,rotate180:0,rawMirrorX:.5,rawMirrorY:.5,rawRotate180:.5},
  edgeLayers:[{distance:0,openFraction:.4},{distance:1,openFraction:1}],scope:'whole-board'};
const render=stats=>context.window.CoilStatsLedger.render({stats,metadata:{generator:'test'}});
const html=render(stats);
const decode=s=>s.replace(/&quot;/g,'"').replace(/&#39;/g,"'").replace(/&lt;/g,'<').replace(/&gt;/g,'>').replace(/&amp;/g,'&');
function cell(html,path) {
  const match=[...html.matchAll(/<td class="value" data-stat-path="([^"]+)" data-value="([^"]+)" title="([^"]+)">([^<]*)<\/td>/g)].find(m=>m[1]===path);
  assert.ok(match,path);
  return {raw:JSON.parse(decode(match[2])),title:decode(match[3]),text:decode(match[4])};
}
for(const [field,raw,text] of [
  ['openCells',6,'6'],['degreeCounts[0]',1,'16.67%'],['degreeCounts[2]',3,'50%'],['degreeCounts[4]',0,'0%'],
  ['horizontalRuns.count',3,'42.86%'],['verticalRuns.count',4,'57.14%'],
  ['horizontalRuns.histogram.1',1,'33.33%'],['verticalRuns.histogram.1',2,'50%'],
  ['openSquareCounts.1',6,'50%'],['openSquareCounts.2',1,'16.67%'],['wallSquareCounts.1',6,'50%'],
  ['largestOpenSquare',[0,0,2],'66.67% · 2 cells'],['horizontalRuns.mean',2,'2']
]) {
  const actual=cell(html,'stats.'+field);
  assert.deepEqual(actual.raw,raw,field+' retains the saved count');
  assert.equal(actual.text,text,field+' uses the correct denominator');
  assert.equal(actual.title,'Saved value: '+JSON.stringify(raw));
}
assert.ok(html.includes('33.33% (1 / 3 horizontal runs)'));
assert.ok(html.includes('16.67% (1 / 6 possible placements)'));
assert.equal((html.match(/fixed zero to one hundred percent scale/g)||[]).length,2);
const empty=render({...stats,openCells:0,openFraction:0,degreeCounts:[0,0,0,0,0],horizontalRuns:{count:0,mean:0,max:0,histogram:{}},verticalRuns:{count:0,mean:0,max:0,histogram:{}}});
assert.equal(cell(empty,'stats.degreeCounts[0]').text,'—');
assert.equal(cell(empty,'stats.horizontalRuns.count').text,'—');
assert.ok(!empty.includes('NaN')&&!empty.includes('Infinity'));
console.log('Verified neighbor, orientation, run-frequency, and square-placement denominators, rectangular square proportions, exact saved values, fixed chart scales, and empty populations.');
