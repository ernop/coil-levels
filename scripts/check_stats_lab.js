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
const context = vm.createContext({window:{}});
vm.runInContext(read('gallery/stats-lab-data.js'),context);
vm.runInContext(read('web/stats-ledger.js'),context);
const data = context.window.COIL_STATS_LAB;
const expected = JSON.parse(read('gallery/catalog.json')).boards.filter(b=>b.side===500&&b.phase==='boards').map(b=>b.id).sort();
assert.deepEqual(Array.from(data.boards,b=>b.id).sort(),expected,'Every saved 500-square specimen must be selectable');
const decode = s => s.replace(/&(amp|lt|gt|quot|#39);/g,(_,key)=>({amp:'&',lt:'<',gt:'>',quot:'"','#39':"'"}[key]));
function lookup(record,field) {return field.replace(/\[(\d+)\]/g,'.$1').split('.').reduce((value,key)=>value[key],record);}
function leaves(value,prefix='') {
  if(value===null||typeof value!=='object')return [prefix];
  return Object.entries(value).flatMap(([key,v])=>leaves(v,Array.isArray(value)?`${prefix}[${key}]`:prefix?`${prefix}.${key}`:key));
}
let fieldsChecked=0;
for(const board of data.boards) {
  const directory=path.join(root,'gallery/boards',board.id);
  const saved=JSON.parse(fs.readFileSync(path.join(directory,'stats.json'),'utf8'));
  assert.deepEqual(JSON.parse(JSON.stringify({...board.metadata,stats:board.stats})),saved,`${board.id}: packed record`);
  const text=zlib.gunzipSync(fs.readFileSync(path.join(directory,'level.board.gz'))).toString().trim();
  assert.equal(crypto.createHash('sha256').update(text).digest('hex'),board.boardSha256,`${board.id}: hash`);
  const cells=text.split('&board=')[1],mask=Buffer.from(board.wallsBase64,'base64');
  assert.equal(cells.length,board.side**2);
  for(let i=0;i<cells.length;i++)assert.equal((mask[i>>3]>>(i&7))&1,Number(cells[i]==='X'),`${board.id}: cell ${i}`);
  const html=context.window.CoilStatsLedger.render(board),covered=[];
  for(const match of html.matchAll(/data-stat-path="([^"]*)" data-value="([^"]*)"/g)) {
    const field=decode(match[1]),value=JSON.parse(decode(match[2]));
    assert.deepEqual(value,lookup(saved,field),`${board.id}: displayed ${field}`);covered.push(field);fieldsChecked++;
  }
  const missing=leaves(saved).filter(field=>!covered.some(parent=>field===parent||field.startsWith(parent+'.')||field.startsWith(parent+'[')));
  assert.deepEqual(missing,[],`${board.id}: saved fields omitted from the ledger`);
}
console.log(`Verified ${data.boards.length} boards: exact masks, complete saved records, and ${fieldsChecked.toLocaleString()} displayed field values; no omitted fields.`);
