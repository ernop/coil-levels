'use strict';
const fs=require('node:fs'),path=require('node:path'),vm=require('node:vm'),assert=require('node:assert/strict');
const root=path.resolve(__dirname,'..');
const context=vm.createContext({window:{},atob:value=>Buffer.from(value,'base64').toString('binary')});
vm.runInContext(fs.readFileSync(path.join(root,'web/board-geometry.js'),'utf8'),context);
const geometry=context.window.CoilBoardGeometry;
// The legal slide order fills a 3×2 rectangle clockwise, ending at the lower left.
const board={width:3,height:2,side:3,wallsBase64:'AA==',solution:{x:0,y:0,path:'RDL'},stats:{openCells:6,tileDensity:{side:32}}};
const expected=[{x:0,y:0,visit:1},{x:1,y:0,visit:2},{x:2,y:0,visit:3},{x:2,y:1,visit:4},{x:1,y:1,visit:5},{x:0,y:1,visit:6}];
const full=geometry.decode(board),compact=geometry.inspectLarge(board);
for(const stride of [1,2,3,5,6,100])for(const g of [full,compact]) {
  const actual=JSON.parse(JSON.stringify(geometry.sampleSolution(board,stride,g)));
  assert.deepEqual(actual,expected.filter((point,i)=>i%stride===0||i===expected.length-1));
}
assert.deepEqual(JSON.parse(JSON.stringify(geometry.sampleSolution({...board,solution:null},2))),[]);
for(const stride of [0,-1,1.5,NaN])assert.throws(()=>geometry.sampleSolution(board,stride,full),/positive integer/);
assert.throws(()=>geometry.sampleSolution({...board,solution:{x:0,y:0,path:'R'}},2,compact),/does not cover/);
assert.throws(()=>geometry.sampleSolution({...board,solution:{x:0,y:0,path:'RRDL'}},2,compact),/zero-length/);
const single={width:1,height:1,side:1,wallsBase64:'AA==',solution:{x:0,y:0,path:''},stats:{openCells:1,tileDensity:{side:32}}};
assert.deepEqual(JSON.parse(JSON.stringify(geometry.sampleSolution(single,5))),[{x:0,y:0,visit:1}]);
console.log('Verified solution sample order, both endpoints, compact replay, one-cell boards, and rejection of invalid paths.');
