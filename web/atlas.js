'use strict';
const catalog = window.COIL_CATALOG;
if (!catalog || !catalog.boards.length) throw new Error('Missing measured catalog: gallery/catalog.js');
const el = id => document.getElementById(id);
const esc = value => String(value).replace(/[&<>"']/g, c => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
const metrics = {
 open:{label:'Open area (%)',get:s=>100*s.openFraction,digits:2},
 pillars:{label:'Isolated walls (%)',get:s=>s.isolatedWallFraction===null?null:100*s.isolatedWallFraction,digits:2},
 degree2:{label:'Degree-two open cells (%)',get:s=>100*s.degreeCounts[2]/s.openCells,digits:2},
 runs:{label:'Mean open run (cells)',get:s=>(s.horizontalRuns.mean+s.verticalRuns.mean)/2,digits:2},
 maxrun:{label:'Longest open run (cells)',get:s=>Math.max(s.horizontalRuns.max,s.verticalRuns.max),digits:0},
 axis:{label:'Horizontal axis bias',get:s=>s.axisBias,digits:4},
 variance:{label:'32-cell tile density variance',get:s=>s.tileDensity.variance,digits:5},
 wall:{label:'Largest wall square (side)',get:s=>s.largestWallSquare[2],digits:0},
 square:{label:'Largest open square (side)',get:s=>s.largestOpenSquare[2],digits:0},
 wallnorm:{label:'Largest wall square (% of side)',get:s=>100*s.largestWallSquare[2]/Math.min(s.width,s.height),digits:2},
 squarenorm:{label:'Largest open square (% of side)',get:s=>100*s.largestOpenSquare[2]/Math.min(s.width,s.height),digits:2},
 interface:{label:'Interface sides per open cell',get:s=>s.interfacePerOpen,digits:3},
 edge:{label:'Outer-edge occupancy contrast (pp)',get:s=>100*(s.edgeLayers[0].openFraction-s.openFraction),digits:2},
 mirror:{label:'Adjusted left/right symmetry',get:s=>s.symmetry.mirrorX,digits:4}
};
let selectedId = catalog.boards[0].id;
let shown = 24;
const options=Object.entries(metrics).map(([k,m])=>`<option value="${k}">${m.label}</option>`).join('');
for(const id of ['sort','xmetric','ymetric','range-metric'])el(id).innerHTML=options;
el('xmetric').value='open';el('ymetric').value='runs';el('sort').value='open';el('range-metric').value='open';
const groups=new Map();
for(const b of catalog.boards.filter(b=>b.phase==='survey')){
 if(!groups.has(b.recipe.id))groups.set(b.recipe.id,[]);
 groups.get(b.recipe.id).push(b);
}
el('recipe').innerHTML=[...groups.keys()].sort().map(k=>`<option>${esc(k)}</option>`).join('');
el('recipe').value='control-rnd99-wanderfull';
function value(b,k){return metrics[k].get(b.stats);}
function fmt(v,k){return v===null?'undefined':v.toLocaleString(undefined,{maximumFractionDigits:metrics[k].digits,minimumFractionDigits:metrics[k].digits});}
function short(b){return `${b.side.toLocaleString()}² · ${b.recipe.id} · seed ${b.seed}`;}
function filtered(){
 const search=el('search').value.toLowerCase();
 return catalog.boards.filter(b=>(el('size').value==='all'||b.side===Number(el('size').value))&&(el('phase').value==='all'||b.phase===el('phase').value)&&`${b.recipe.id} ${JSON.stringify(b.options)}`.toLowerCase().includes(search))
 .sort((a,b)=>(value(b,el('sort').value)??-Infinity)-(value(a,el('sort').value)??-Infinity)||a.id.localeCompare(b.id));
}
function links(b){return `<div class="links"><a href="${b.path}/map.png" target="_blank" rel="noopener">Full map</a><a href="${b.path}/level.board.gz" download>Board .gz</a><a href="${b.path}/level.solution.gz" download>Solution .gz</a><a href="${b.path}/stats.json">All stats</a></div>`;}
function card(b){return `<article class="card ${b.id===selectedId?'selected-card':''}"><img src="${b.path}/${el('view').value}.png" loading="lazy" alt="${esc(short(b))}; ${el('view').value==='detail'?'central 128-cell detail':'full board overview'}"><h3>${b.side.toLocaleString()} × ${b.side.toLocaleString()}</h3><p>${esc(b.recipe.id)} · seed ${b.seed}</p><p>Open ${fmt(value(b,'open'),'open')}% · mean run ${fmt(value(b,'runs'),'runs')} cells<br>Density variance ${fmt(value(b,'variance'),'variance')}</p>${links(b)}<button data-board="${esc(b.id)}">Inspect distributions</button></article>`;}
function bindCards(root){root.querySelectorAll('[data-board]').forEach(button=>button.addEventListener('click',()=>select(button.dataset.board,true)));}
function renderCards(){
 const rows=filtered();
 if(rows.length&&!rows.some(b=>b.id===selectedId)){selectedId=rows[0].id;renderSelected();}
 el('selected').hidden=rows.length===0;
 el('count').textContent=`${rows.length} matching specimens · showing ${Math.min(shown,rows.length)}`;
 el('cards').innerHTML=rows.slice(0,shown).map(card).join('');bindCards(el('cards'));el('more').hidden=rows.length<=shown;
 renderScatter(rows);
}
function renderScatter(rows){
 const xk=el('xmetric').value,yk=el('ymetric').value,xm=metrics[xk],ym=metrics[yk];
 const points=rows.filter(b=>value(b,xk)!==null&&value(b,yk)!==null);
 const svg=el('scatter');
 if(!points.length){svg.innerHTML='<text x="30" y="50" fill="white">No matching measurements.</text>';return;}
 let xs=points.map(b=>value(b,xk)),ys=points.map(b=>value(b,yk));
 let xmin=Math.min(...xs),xmax=Math.max(...xs),ymin=Math.min(...ys),ymax=Math.max(...ys);
 if(xmin===xmax){xmin-=.5;xmax+=.5;}if(ymin===ymax){ymin-=.5;ymax+=.5;}
 const x=v=>85+(v-xmin)/(xmax-xmin)*870,y=v=>290-(v-ymin)/(ymax-ymin)*255;
 let markup='<title>Each dot is one saved validated board. Click to inspect.</title>';
 for(let i=0;i<=4;i++){
  let xv=xmin+(xmax-xmin)*i/4,yv=ymin+(ymax-ymin)*i/4;
  markup+=`<path d="M85 ${y(yv)}H955" stroke="#35493e"/><text x="75" y="${y(yv)+4}" fill="#aec0b5" text-anchor="end" font-size="12">${yv.toLocaleString(undefined,{maximumSignificantDigits:5})}</text><text x="${x(xv)}" y="313" fill="#aec0b5" text-anchor="middle" font-size="12">${xv.toLocaleString(undefined,{maximumSignificantDigits:5})}</text>`;
 }
 markup+=`<text x="500" y="348" text-anchor="middle" fill="white" font-size="14">${xm.label}</text><text x="85" y="18" fill="white" font-size="14">${ym.label}</text>`;
 for(const b of points)markup+=`<circle class="plot-dot" tabindex="0" role="button" aria-label="Inspect ${esc(short(b))}" data-point="${esc(b.id)}" cx="${x(value(b,xk))}" cy="${y(value(b,yk))}" r="${b.id===selectedId?7:4}" fill="${b.phase==='survey'?'#9ce3ba':'#edbc78'}" opacity=".8"><title>${esc(short(b))}\n${xm.label}: ${fmt(value(b,xk),xk)}\n${ym.label}: ${fmt(value(b,yk),yk)}</title></circle>`;
 svg.innerHTML=markup;
 svg.querySelectorAll('[data-point]').forEach(dot=>{dot.addEventListener('click',()=>select(dot.dataset.point,false));dot.addEventListener('keydown',e=>{if(e.key==='Enter'||e.key===' '){e.preventDefault();select(dot.dataset.point,false);}});});
 el('plot-caption').textContent='Green: 300-square controlled survey. Amber: scale series and initial samples. Coincident dots can overlap; the cards and seed comparison retain every specimen.';
}
function distributionChart(b){
 const h=b.stats.horizontalRuns.histogram,v=b.stats.verticalRuns.histogram;
 const lengths=[...new Set([...Object.keys(h),...Object.keys(v)])].map(Number).sort((a,b)=>a-b);
 // Twenty bins retain all runs, including long tails, without thousands of marks.
 const max=Math.max(...lengths),bins=Math.min(20,max),bh=Array(bins).fill(0),bv=Array(bins).fill(0);
 for(const len of lengths){let i=Math.min(bins-1,Math.floor((len-1)*bins/max));bh[i]+=h[len]||0;bv[i]+=v[len]||0;}
 const peak=Math.max(...bh,...bv,1);
 let s='<svg class="mini-chart" viewBox="0 0 600 170" role="img" aria-label="All horizontal and vertical open run counts, in up to twenty equal-width length bins"><title>Green horizontal, amber vertical. Bin counts include all runs.</title>';
 for(let i=0;i<bins;i++){let x=35+i*530/bins; s+=`<rect x="${x}" y="${135-bh[i]/peak*100}" width="${240/bins}" height="${bh[i]/peak*100}" fill="#9ce3ba"><title>Horizontal, bin ${i+1}: ${bh[i]} runs</title></rect><rect x="${x+250/bins}" y="${135-bv[i]/peak*100}" width="${240/bins}" height="${bv[i]/peak*100}" fill="#edbc78"><title>Vertical, bin ${i+1}: ${bv[i]} runs</title></rect>`;}
 return s+`<text x="35" y="18" fill="white" font-size="13">Run counts · green horizontal / amber vertical · peak ${peak}</text><text x="35" y="158" fill="white" font-size="12">1 cell</text><text x="565" y="158" text-anchor="end" fill="white" font-size="12">${max} cells</text></svg>`;
}
function edgeChart(b){
 const ls=b.stats.edgeLayers,max=Math.max(1,ls.length-1);let pts=ls.map(l=>`${35+l.distance/max*530},${135-100*l.openFraction}`).join(' ');
 return `<svg class="mini-chart" viewBox="0 0 600 165" role="img" aria-label="Exact occupancy at every distance from board edge"><text x="35" y="18" fill="white" font-size="13">Edge-layer occupancy · 0–100% vertical range</text><path d="M35 35V135H565" stroke="#779282" fill="none"/><polyline points="${pts}" stroke="#9ce3ba" stroke-width="1.4" fill="none"/><text x="35" y="156" fill="white" font-size="12">Border (0)</text><text x="565" y="156" text-anchor="end" fill="white" font-size="12">Center (${ls.length-1} cells from edge)</text></svg>`;
}
function select(id,scroll){selectedId=id;renderSelected();renderScatter(filtered());el('cards').querySelectorAll('.card').forEach(c=>c.classList.toggle('selected-card',c.querySelector('[data-board]').dataset.board===id));if(scroll)el('selected').scrollIntoView({behavior:'instant',block:'start'});}
function renderSelected(){
 const b=catalog.boards.find(b=>b.id===selectedId);if(!b)throw new Error('Selected board not found');
 el('selected').innerHTML=`<h3>${esc(short(b))}</h3><div class="selected-layout"><div><img src="${b.path}/detail.png" alt="Central 128 by 128 crop"><p class="muted">Central 128×128 crop · white open / black wall</p>${links(b)}<p><code>${esc(JSON.stringify(b.options))}</code></p><p class="muted">Generation ${b.generationSeconds.toFixed(2)} s (machine/load dependent). Validated before export and replayed after gzip persistence.</p></div><div><div class="stats">${Object.keys(metrics).map(k=>`<div><span>${metrics[k].label}</span><strong>${fmt(value(b,k),k)}</strong></div>`).join('')}</div>${distributionChart(b)}${edgeChart(b)}</div></div>`;
}
function renderRanges(){
 const k=el('range-metric').value;
 const rows=[...groups].map(([id,bs])=>{const vs=bs.map(b=>value(b,k)).filter(v=>v!==null);return{id,bs,min:Math.min(...vs),max:Math.max(...vs),mean:vs.reduce((a,b)=>a+b,0)/vs.length};}).sort((a,b)=>(b.max-b.min)-(a.max-a.min));
 const lo=Math.min(...rows.map(r=>r.min)),hi=Math.max(...rows.map(r=>r.max)),span=hi-lo||1;
 el('ranges').innerHTML=rows.map(r=>`<button class="range-row" data-recipe="${esc(r.id)}"><span>${esc(r.id)}</span><span class="range-track"><span class="range-line" style="left:${100*(r.min-lo)/span}%;width:${100*(r.max-r.min)/span}%"></span><span class="range-mean" style="left:${100*(r.mean-lo)/span}%"></span></span><span class="range-value">${fmt(r.min,k)} → ${fmt(r.max,k)}</span></button>`).join('');
 el('ranges').querySelectorAll('[data-recipe]').forEach(button=>button.addEventListener('click',()=>{el('recipe').value=button.dataset.recipe;renderSeeds();el('seed-comparison').scrollIntoView({behavior:'instant',block:'center'});}));renderSeeds();
}
function renderSeeds(){
 const k=el('range-metric').value,bs=groups.get(el('recipe').value);el('seed-comparison').innerHTML=bs.map(b=>`<article><img src="${b.path}/preview.png" width="100%" alt="Whole-board preview ${esc(short(b))}"><h3>Seed ${b.seed}</h3><p>${metrics[k].label}: <strong>${fmt(value(b,k),k)}</strong></p>${links(b)}</article>`).join('');
}
const surveyBoards=catalog.boards.filter(b=>b.phase==='survey');
const openValues=surveyBoards.map(b=>value(b,'open'));
const maxSpan=Math.max(...[...groups.values()].map(bs=>Math.max(...bs.map(b=>value(b,'open')))-Math.min(...bs.map(b=>value(b,'open')))));
const unique=new Set(surveyBoards.map(b=>b.boardSha256)).size;
el('summary').textContent=`${catalog.boards.length} saved specimens${catalog.complete?"":" (generation snapshot; more planned)"} · ${surveyBoards.length} controlled survey boards · ${catalog.configs.length} configurations · ${catalog.survey.filter(r=>r.status==='complete').length}/${catalog.survey.length} survey runs validated`;
el('findings-cards').innerHTML=`<article><div class="big">${Math.min(...openValues).toFixed(1)}–${Math.max(...openValues).toFixed(1)}%</div><h3>Open area across the survey</h3><p>Recipes differ in both overall occupancy and arrangement.</p></article><article><div class="big">${maxSpan.toFixed(1)} pp</div><h3>Largest within-recipe spread</h3><p>Observed open-area range across the three shared seeds.</p></article><article><div class="big">${unique} / ${surveyBoards.length}</div><h3>Distinct board hashes</h3><p>Different recipes can produce identical boards at a given seed. Duplicate outcomes remain visible.</p></article>`;
for(const id of ['size','phase','search','view','sort'])el(id).addEventListener(id==='search'?'input':'change',()=>{shown=24;renderCards();});
for(const id of ['xmetric','ymetric'])el(id).addEventListener('change',()=>renderScatter(filtered()));
el('more').addEventListener('click',()=>{shown+=24;renderCards();});
el('range-metric').addEventListener('change',renderRanges);el('recipe').addEventListener('change',renderSeeds);
for(const [id,open] of [['toy-block',(x,y)=>x<4],['toy-stripes',(x,y)=>x%2===0]]){
 const ctx=el(id).getContext('2d');for(let y=0;y<8;y++)for(let x=0;x<8;x++){ctx.fillStyle=open(x,y)?'#ffffff':'#000000';ctx.fillRect(x*20,y*20,20,20);}
}
renderSelected();renderCards();renderRanges();

const contrasts=['seg-rnd99-First','seg-rnd99-Last','seg-last-First','seg-2lim10-Last','control-rnd99-wanderfull','control-rnd99-onepass','control-last-unlimited'];
const meanChange=(id,k)=>{const bs=groups.get(id);return bs.reduce((sum,b)=>sum+value(b,k)-value(groups.get('tweak-'+b.options.picker).find(base=>base.seed===b.seed),k),0)/bs.length;};
el('paired-effects').innerHTML='<table><thead><tr><th>Changed configuration</th><th>Open area Δ (pp)</th><th>Mean run Δ (cells)</th></tr></thead><tbody>'+contrasts.map(id=>`<tr><td>${esc(id)}</td><td>${meanChange(id,'open').toFixed(3)}</td><td>${meanChange(id,'runs').toFixed(3)}</td></tr>`).join('')+'</tbody></table>';

el('large-pair').addEventListener('click',()=>{el('size').value='10000';el('phase').value='boards';el('search').value='control-rnd99-wanderfull';el('view').value='preview';shown=24;renderCards();el('cards').scrollIntoView({behavior:'instant',block:'start'});});
