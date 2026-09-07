'use strict';
const data = window.COIL_STATS_LAB;
const boards = data.boards.slice().sort((a,b)=>a.recipe.localeCompare(b.recipe)||a.seed-b.seed);
const byId = new Map(boards.map(board=>[board.id,board]));
const $ = id => document.getElementById(id);
const escapeText = value => String(value).replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
const metrics={
 occupancy:{name:'Open area (%)',value:s=>100*s.openFraction,definition:'Count open cells and divide by all 250,000 cells. Green marks every cell included in the numerator.',limits:'It discards arrangement. Equal occupancy can describe fine corridors, ordered bands, scattered pillars, or a large empty region.',legend:'Green: open cell. Black: wall.'},
 density:{name:'32-cell tile density variance',value:s=>s.tileDensity.variance,definition:'Partition the board into nonoverlapping 32×32 tiles. Compute each tile’s open fraction, then the mean squared deviation from the tile mean. Partial edge tiles have the same weight as full tiles.',limits:'It measures variation in local occupancy, not fine pattern or organization within tiles. The fixed tile alignment and 32-cell scale matter. Both very dense and very sparse uniform patterns can have low variance.',legend:'Shared color scale: dark blue = 0% open; yellow = 100% open. Lines show the 32-cell tiles.'},
 runs:{name:'Mean open run (cells)',value:s=>(s.horizontalRuns.mean+s.verticalRuns.mean)/2,definition:'Find every maximal horizontal and vertical run of open cells. Average each orientation’s run lengths, then average the two means. The slider highlights the long-run tail; it does not change the reported mean.',limits:'These are geometric runs, not legal solution segments. A mean can hide a small number of extremely long runs and does not show how runs join.',legend:'Orange: horizontal runs above the chosen length. Blue: vertical runs. Purple: both. Unhighlighted open cells are gray.'},
 pillars:{name:'Isolated wall cells (%)',value:s=>100*s.isolatedWallFraction,definition:'Count wall cells without any orthogonally adjacent wall; divide by all wall cells. The overlay highlights each counted wall cell.',limits:'Diagonal contact is ignored. It does not distinguish arrangement, spacing, repeated motifs, or the shape of larger wall clusters.',legend:'Red: isolated wall cell. Other walls are black; open cells are gray.'},
 degree:{name:'Degree-two open cells (%)',value:s=>100*s.degreeCounts[2]/s.openCells,definition:'Count open cells with exactly two orthogonal open neighbors; divide by all open cells. Every highlighted cell contributes once.',limits:'Degree two includes both bends and straight corridors. It does not by itself force two solution edges because the cell may be an endpoint.',legend:'Green: open cell with exactly two open neighbors. Other open cells are gray.'},
 squares:{name:'Largest open square (side)',value:s=>s.largestOpenSquare[2],definition:'Find the largest completely open, axis-aligned square. The highlighted square is the exact coordinate witness saved in the stats.',limits:'One maximum can be an outlier. It ignores long thin open areas and the organization of the rest of the board. Square side is not area or a room decomposition.',legend:'Amber frame and tint: exact largest all-open square. Click the map to inspect cells in a labeled detail.'},
 walls:{name:'Largest wall square (side)',value:s=>s.largestWallSquare[2],definition:'Find the largest completely blocked, axis-aligned square. The red outline identifies the exact witness.',limits:'A large thin wall or diagonal barrier can still have a maximum square of side one. This is not wall component area.',legend:'Red frame and tint: exact largest all-wall square. Small squares are easiest to inspect in the labeled cell detail.'},
 interface:{name:'Interface sides per open cell',value:s=>s.interfacePerOpen,definition:'Count every open-cell side facing a wall or the board exterior and divide by the number of open cells. Equivalently (4N − 2E) / N, with E the open-to-open adjacency count.',limits:'It captures boundary density, not where boundaries occur. Different wall shapes and arrangements can produce the same value.',legend:'Open cells: yellow to red as their number of wall-facing sides rises from 0 to 4. Exterior boundaries count.'},
 axis:{name:'Horizontal axis bias',value:s=>s.axisBias,definition:'Count horizontal and vertical open adjacencies. Report (horizontal − vertical) / (horizontal + vertical). The overlay shows each cell’s local difference; global bias counts each adjacency once.',limits:'Opposing local orientations can cancel. A value near zero does not mean no directional structure, and orientation is not a measure of complexity.',legend:'Orange: more horizontal open neighbors. Blue: more vertical. Gray: balanced local counts. Same scale on both boards.'},
 symmetry:{name:'Adjusted left/right symmetry',value:s=>s.symmetry.mirrorX,definition:'Compare each cell to its reflected partner across the vertical midline. Adjust observed agreement for p² + (1−p)², the independent baseline at this occupancy.',limits:'A high raw agreement can come from an almost uniform board. This adjusted value tests one global reflection, not local repeated motifs, translation symmetry, or rotational structure.',legend:'Magenta: mismatch with the horizontally reflected cell. Matching cells retain their wall/open shade.'},
 edge:{name:'Outer-edge occupancy contrast (pp)',value:s=>100*(s.edgeLayers[0].openFraction-s.openFraction),definition:'Subtract overall open fraction from the occupancy of the outermost layer. The slider highlights other exact-distance layers for context; the headline statistic always refers to distance zero.',limits:'It only summarizes the outer border. An unusual region just inside the border can be missed, which is why the full edge profile is also saved.',legend:'Green: open cells in the chosen edge-distance layer. Red: wall cells in that layer. Slider distance is measured in cells.'}
};
function cells(board){
 if(board.geometry)return board.geometry;
 const bytes=Uint8Array.from(atob(board.wallsBase64),c=>c.charCodeAt(0)),wall=new Uint8Array(250000),degree=new Uint8Array(250000),horizontal=new Uint16Array(250000),vertical=new Uint16Array(250000),tiles=new Float64Array(256),totals=new Uint16Array(256);
 let open=0;
 for(let i=0;i<250000;i++){wall[i]=(bytes[i>>3]>>(i&7))&1;open+=!wall[i];}
 if(open!==board.stats.openCells)throw new Error('Cell mask does not match measured board');
 for(let y=0;y<500;y++)for(let x=0;x<500;x++){const i=y*500+x,t=(y>>5)*16+(x>>5);tiles[t]+=!wall[i];totals[t]++;degree[i]=(x>0&&!wall[i-1])+(x<499&&!wall[i+1])+(y>0&&!wall[i-500])+(y<499&&!wall[i+500]);}
 for(let a=0;a<500;a++)for(const [step,start,out] of [[1,a*500,horizontal],[500,a,vertical]]){let r=0;while(r<500){if(wall[start+r*step]){r++;continue;}let end=r+1;while(end<500&&!wall[start+end*step])end++;for(let k=r;k<end;k++)out[start+k*step]=end-r;r=end;}}
 for(let t=0;t<256;t++)tiles[t]/=totals[t];
 return board.geometry={wall,degree,horizontal,vertical,tiles};
}
const format = n => n === null ? 'undefined' : n.toLocaleString(undefined,{maximumSignificantDigits:7});
const query = new URLSearchParams(location.search);
const clamp = (n,low,high) => Math.max(low,Math.min(high,n));
function queryInteger(key,fallback,low,high) {
  const raw = query.get(key);
  return raw !== null && /^-?\d+$/.test(raw) ? clamp(Number(raw),low,high) : fallback;
}
$('lab-board').innerHTML = boards.map(b=>`<option value="${b.id}">${escapeText(b.recipe)} · seed ${b.seed}</option>`).join('');
const requested = query.get('board') || query.get('left'); // Existing gallery links remain valid.
$('lab-board').value = byId.has(requested) ? requested : '500-random-s101';
$('lab-metric').innerHTML = Object.entries(metrics).map(([key,m])=>`<option value="${key}">${m.name}</option>`).join('');
$('lab-metric').value = Object.hasOwn(metrics,query.get('metric')) ? query.get('metric') : 'runs';
$('lab-display').value = query.get('display') === 'raw' ? 'raw' : 'overlay';
if ([12,24,48,96,144].includes(Number(query.get('zoom')))) $('zoom-size').value = query.get('zoom');
$('run-min').value = queryInteger('run',10,2,100);
$('edge-distance').value = queryInteger('edge',0,0,249);
let focus = {x:queryInteger('x',314,0,499),y:queryInteger('y',283,0,499)};
const surface = document.createElement('canvas'); surface.width = 500; surface.height = 500;
let surfaceKey = '', drag = null, framePending = false;
const currentBoard = () => byId.get($('lab-board').value);
function cropBounds() {
  const size = Number($('zoom-size').value);
  return {size,x:clamp(Math.floor(focus.x-size/2),0,500-size),y:clamp(Math.floor(focus.y-size/2),0,500-size)};
}
function syncURL() {
  const url = new URL(location.href); url.searchParams.delete('left');
  for (const [key,value] of Object.entries({board:$('lab-board').value,metric:$('lab-metric').value,display:$('lab-display').value,zoom:$('zoom-size').value,x:focus.x,y:focus.y,run:$('run-min').value,edge:$('edge-distance').value})) url.searchParams.set(key,String(value));
  history.replaceState(null,'',url);
}
function renderSurface() {
  const board = currentBoard(), key = $('lab-metric').value, overlay = $('lab-display').value === 'overlay';
  const threshold = Number($('run-min').value), layer = Number($('edge-distance').value);
  const signature = [board.id,key,overlay,threshold,layer].join(':');
  if (signature === surfaceKey) return;
  surfaceKey = signature;
  const g = cells(board), ctx = surface.getContext('2d'), img = ctx.createImageData(500,500);
  for (let y=0;y<500;y++) for (let x=0;x<500;x++) {
    const i = y*500+x, w = g.wall[i]; let rgb = w ? [0,0,0] : [255,255,255];
    if (overlay) {
      rgb = w ? [12,20,15] : [204,211,206];
      if (key==='occupancy'&&!w) rgb=[128,239,169];
      if (key==='density') {const p=g.tiles[(y>>5)*16+(x>>5)];rgb=[Math.round(20+235*p),Math.round(45+175*p),Math.round(105-55*p)];}
      if (key==='runs'&&!w) {const h=g.horizontal[i]>=threshold,v=g.vertical[i]>=threshold;rgb=h&&v?[194,120,231]:h?[248,157,66]:v?[72,160,244]:rgb;}
      if (key==='pillars'&&w&&g.degree[i]===(x>0)+(x<499)+(y>0)+(y<499)) rgb=[255,65,81];
      if (key==='degree'&&!w&&g.degree[i]===2) rgb=[86,233,154];
      if (key==='interface'&&!w) {const n=4-g.degree[i];rgb=[255,240-n*45,140-n*30];}
      if (key==='axis'&&!w) {const h=(x>0&&!g.wall[i-1])+(x<499&&!g.wall[i+1]),v=(y>0&&!g.wall[i-500])+(y<499&&!g.wall[i+500]);rgb=h>v?[246,158,67]:v>h?[66,161,245]:[204,211,206];}
      if (key==='symmetry'&&w!==g.wall[y*500+499-x]) rgb=[242,75,183];
      if (key==='edge'&&Math.min(x,y,499-x,499-y)===layer) rgb=w?[255,70,90]:[81,240,145];
    }
    const k=i*4; img.data[k]=rgb[0]; img.data[k+1]=rgb[1]; img.data[k+2]=rgb[2]; img.data[k+3]=255;
  }
  ctx.putImageData(img,0,0);
  if (overlay&&key==='density') {
    ctx.strokeStyle='#15251c88';ctx.lineWidth=.5;
    for (let i=0;i<=500;i+=32) {ctx.beginPath();ctx.moveTo(i,0);ctx.lineTo(i,500);ctx.moveTo(0,i);ctx.lineTo(500,i);ctx.stroke();}
  }
  if (overlay&&(key==='squares'||key==='walls')) {
    const [x,y,n]=board.stats[key==='squares'?'largestOpenSquare':'largestWallSquare'];
    ctx.fillStyle=key==='squares'?'#ffa92a88':'#ff234c88';ctx.fillRect(x,y,n,n);
    ctx.strokeStyle=key==='squares'?'#bd6600':'#ff234c';ctx.lineWidth=1;ctx.strokeRect(x,y,n,n);
  }
}
function renderViews() {
  renderSurface();
  const bounds=cropBounds(), canvas=$('board-map'), ctx=canvas.getContext('2d');
  ctx.drawImage(surface,0,0);ctx.strokeStyle='#00e9ff';ctx.lineWidth=1.5;ctx.strokeRect(bounds.x+.75,bounds.y+.75,bounds.size-1.5,bounds.size-1.5);
  const zoom=$('board-zoom'), z=zoom.getContext('2d'), {x,y,size}=bounds;
  z.fillStyle='#183527';z.fillRect(0,0,640,640);z.imageSmoothingEnabled=false;
  z.drawImage(surface,x,y,size,size,32,40,576,576);
  if ($('zoom-grid').checked) {
    z.strokeStyle='#64706777';z.lineWidth=.65;
    for(let i=0;i<=size;i++) {const at=i*576/size;z.beginPath();z.moveTo(32+at,40);z.lineTo(32+at,616);z.moveTo(32,40+at);z.lineTo(608,40+at);z.stroke();}
  }
  z.strokeStyle='#00e9ff';z.lineWidth=2;z.strokeRect(31,39,578,578);
  z.fillStyle='#a5eac6';z.font='bold 16px system-ui';z.fillText(`CROP · ${size}×${size} · x=${x}–${x+size-1}, y=${y}–${y+size-1}`,20,25);
  z.font='13px system-ui';z.fillText('Detail of the 500×500 board · coordinates are zero-based',20,636);
  $('zoom-summary').textContent=`${size} × ${size} cells`;
  $('zoom-coordinates').textContent=`Crop: x=${x}–${x+size-1}, y=${y}–${y+size-1} (zero-based). ${size*size} of 250,000 cells shown.`;
  zoom.setAttribute('aria-label',`Crop of ${currentBoard().recipe}, seed ${currentBoard().seed}: x ${x} through ${x+size-1}, y ${y} through ${y+size-1}. Drag to pan.`);
  $('focus-x').value=focus.x;$('focus-y').value=focus.y;syncURL();
}
function queueViews() {
  if(framePending)return;framePending=true;
  requestAnimationFrame(()=>{framePending=false;renderViews();});
}
function renderSurvey() {
  const r=data.ranges[$('survey-scope').value][$('lab-metric').value];
  $('survey-readout').innerHTML=`<article><h3>${r.configurations} recipe means</h3><div class="big">${format(r.minMean)} → ${format(r.maxMean)}</div><p>Range of three-seed means</p></article><article><h3>Typical seed variation</h3><div class="big">${format(r.medianSeedRange)}</div><p>Median within-recipe max − min</p></article><article><h3>Largest seed range</h3><div class="big">${format(r.widest.max-r.widest.min)}</div><p>${escapeText(r.widest.recipe)}</p></article>`;
}
function renderMetric() {
  const key=$('lab-metric').value,m=metrics[key],board=currentBoard(),raw=$('lab-display').value==='raw';
  $('run-control').hidden=key!=='runs';$('layer-control').hidden=key!=='edge';
  $('run-value').textContent=$('run-min').value+' cells';$('layer-value').textContent=$('edge-distance').value+' cells';
  $('lab-legend').textContent=(raw?'Original board: white open, black wall.':m.legend)+' Cyan frame: the zoomed area on the right.';
  $('metric-definition').textContent=m.definition;$('metric-limits').textContent=m.limits;
  $('metric-readout').textContent='The reported value always describes the complete board. Overlay controls change the highlighted cells; the zoom changes only the view.';
  $('board-value').textContent=format(m.value(board.stats));$('board-value-label').textContent=m.name+' · whole board';
  let detail=`${board.recipe} · seed ${board.seed}`;
  if(key==='squares'||key==='walls') {const [x,y,n]=board.stats[key==='squares'?'largestOpenSquare':'largestWallSquare'];detail+=` · Witness x=${x}, y=${y}, side=${n}.`;}
  if(key==='edge') detail+=` · Highlighted layer ${$('edge-distance').value}: ${format(board.stats.edgeLayers[Number($('edge-distance').value)].openFraction*100)}% open. Headline contrast always uses layer 0.`;
  $('board-detail').textContent=detail;
  document.querySelectorAll('[data-headline]').forEach(button=>button.setAttribute('aria-pressed',String(button.dataset.headline===key)));
  renderSurvey();renderViews();
}
function renderRecord() {
  const board=currentBoard(), record={...board.metadata,stats:board.stats};
  $('stats-board-name').textContent=`${board.id} · ${board.recipe} · seed ${board.seed}`;
  $('stats-ledger').innerHTML=window.CoilStatsLedger.render(board);
  $('raw-stats').textContent=JSON.stringify(record,null,2);
  $('headline-stats').innerHTML=Object.entries(metrics).map(([key,m])=>`<button class="stat-button" data-headline="${key}" aria-pressed="false" title="Show ${m.name} overlay"><span>${m.name}</span><strong>${format(m.value(board.stats))}</strong></button>`).join('');
  $('board-links').innerHTML=`<a href="#all-stats">Complete stats below</a><a href="${board.path}/map.png?v=${board.boardSha256}" target="_blank" rel="noopener">Full-resolution PNG</a><a href="${board.path}/stats.json?v=${board.boardSha256}" download="${board.id}-stats.json">Download stats JSON</a>`;
}
function chooseBoard(id) {
  $('lab-board').value=id;$('navigation-note').textContent='';renderRecord();renderMetric();
}
function chooseMetric(key,scroll=false) {
  $('lab-metric').value=key;$('lab-display').value='overlay';renderMetric();
  if(scroll)$('viewer').scrollIntoView({block:'start',behavior:'instant'});
}
$('lab-board').addEventListener('change',()=>chooseBoard($('lab-board').value));
for(const [id,delta] of [['previous-board',-1],['next-board',1]]) $(id).addEventListener('click',()=>chooseBoard(boards[(boards.findIndex(b=>b.id===$('lab-board').value)+delta+boards.length)%boards.length].id));
$('next-seed').addEventListener('click',()=>{
  const current=currentBoard(), matches=boards.filter(b=>b.recipe===current.recipe);
  if(matches.length<2) {$('navigation-note').textContent='No other seed of this recipe is saved at 500 square.';return;}
  chooseBoard(matches[(matches.findIndex(b=>b.id===current.id)+1)%matches.length].id);
});
for(const id of ['lab-metric','lab-display']) $(id).addEventListener('change',renderMetric);
for(const id of ['run-min','edge-distance']) $(id).addEventListener('input',renderMetric);
$('survey-scope').addEventListener('change',renderSurvey);
for(const id of ['zoom-size','zoom-grid']) $(id).addEventListener('change',renderViews);
for(const axis of ['x','y']) $('focus-'+axis).addEventListener('input',()=>{
  if($('focus-'+axis).value==='')return;
  const value=Number($('focus-'+axis).value);focus[axis]=Number.isFinite(value)?clamp(Math.round(value),0,499):focus[axis];renderViews();
});
$('headline-stats').addEventListener('click',event=>{const button=event.target.closest('[data-headline]');if(button)chooseMetric(button.dataset.headline,true);});
$('stats-ledger').addEventListener('click',event=>{
  const button=event.target.closest('button');if(!button)return;
  if(button.dataset.overlay)chooseMetric(button.dataset.overlay,true);
  if(button.dataset.focusSquare) {
    const key=button.dataset.focusSquare,[x,y,n]=currentBoard().stats[key==='squares'?'largestOpenSquare':'largestWallSquare'];
    focus={x:clamp(Math.floor(x+n/2),0,499),y:clamp(Math.floor(y+n/2),0,499)};
    $('zoom-size').value=String([12,24,48,96,144].find(size=>size>=n+6)||144);chooseMetric(key,true);
  }
  if(button.dataset.edgeLayer!==undefined) {
    const layer=Number(button.dataset.edgeLayer);$('edge-distance').value=layer;focus={x:250,y:layer};chooseMetric('edge',true);
  }
});
function pointOnBoard(event) {
  const rect=$('board-map').getBoundingClientRect();
  focus={x:clamp(Math.floor((event.clientX-rect.left)/rect.width*500),0,499),y:clamp(Math.floor((event.clientY-rect.top)/rect.height*500),0,499)};
}
for(const id of ['board-map','board-zoom']) {
  const canvas=$(id);
  canvas.addEventListener('pointerdown',event=>{
    if(event.button!==0)return;event.preventDefault();canvas.focus();canvas.setPointerCapture(event.pointerId);
    drag={id,x:event.clientX,y:event.clientY,focus:{...focus}};
    if(id==='board-map'){pointOnBoard(event);queueViews();}
  });
  canvas.addEventListener('pointermove',event=>{
    if(!drag||drag.id!==id)return;
    if(id==='board-map')pointOnBoard(event);
    else {
      const scale=Number($('zoom-size').value)/(canvas.getBoundingClientRect().width*576/640);
      focus={x:clamp(Math.round(drag.focus.x-(event.clientX-drag.x)*scale),0,499),y:clamp(Math.round(drag.focus.y-(event.clientY-drag.y)*scale),0,499)};
    }
    queueViews();
  });
  const endDrag=()=>{drag=null;};
  canvas.addEventListener('pointerup',endDrag);canvas.addEventListener('pointercancel',endDrag);canvas.addEventListener('lostpointercapture',endDrag);
  canvas.addEventListener('keydown',event=>{
    const moves={ArrowLeft:[-1,0],ArrowRight:[1,0],ArrowUp:[0,-1],ArrowDown:[0,1]};if(!moves[event.key])return;
    event.preventDefault();const [x,y]=moves[event.key],step=event.shiftKey?10:1;
    focus={x:clamp(focus.x+x*step,0,499),y:clamp(focus.y+y*step,0,499)};queueViews();
  });
}
renderRecord();renderMetric();
