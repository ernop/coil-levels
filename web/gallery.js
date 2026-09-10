'use strict';
const data = window.COIL_STATS_LAB;
const boards = data.boards.slice().sort((a,b)=>a.side-b.side||a.id.localeCompare(b.id));
window.COIL_STATS_RECORDS = {};
let activeBoard = null, loadVersion = 0, visibleBoards = boards;
const currentBoard = () => activeBoard;
const width = () => currentBoard().width;
const height = () => currentBoard().height;
const side = () => Math.max(width(),height());
const dimensions = b => `${b.width.toLocaleString()} × ${b.height.toLocaleString()}`;
let selectedId = '';
const collectionById = new Map(data.collections.map(c=>[c.id,c]));
const byId = new Map(boards.map(board=>[board.id,board]));
const $ = id => document.getElementById(id);
const escapeText = value => String(value).replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
const metrics={
 occupancy:{name:'Open area (%)',value:s=>100*s.openFraction,definition:'Count open cells and divide by all cells in this board. Green marks every cell included in the numerator.',limits:'It discards arrangement. Equal occupancy can describe fine corridors, ordered bands, scattered pillars, or a large empty region.',legend:'Green: open cell. Black: wall.'},
 density:{name:'32-cell tile density variance',value:s=>s.tileDensity.variance,definition:'Partition the board into nonoverlapping 32×32 tiles. Compute each tile’s open fraction, then the mean squared deviation from the tile mean. Partial edge tiles have the same weight as full tiles.',limits:'It measures variation in local occupancy, not fine pattern or organization within tiles. The fixed tile alignment and 32-cell scale matter. Both very dense and very sparse uniform patterns can have low variance.',legend:'Shared color scale: dark blue = 0% open; yellow = 100% open. Lines show the 32-cell tiles.'},
 runs:{name:'Mean open run (cells)',value:s=>(s.horizontalRuns.mean+s.verticalRuns.mean)/2,definition:'Find every maximal horizontal and vertical run of open cells. Average each orientation’s run lengths, then average the two means. The slider highlights the long-run tail; it does not change the reported mean.',limits:'These are geometric runs, not legal solution segments. A mean can hide a small number of extremely long runs and does not show how runs join.',legend:'Orange: horizontal runs above the chosen length. Blue: vertical runs. Purple: both. Unhighlighted open cells are gray.'},
 pillars:{name:'Isolated wall cells (%)',value:s=>s.isolatedWallFraction===null?null:100*s.isolatedWallFraction,definition:'Count wall cells without any orthogonally adjacent wall; divide by all wall cells. The overlay highlights each counted wall cell.',limits:'Diagonal contact is ignored. It does not distinguish arrangement, spacing, repeated motifs, or the shape of larger wall clusters.',legend:'Red: isolated wall cell. Other walls are black; open cells are gray.'},
 degree:{name:'Degree-two open cells (%)',value:s=>100*s.degreeCounts[2]/s.openCells,definition:'Count open cells with exactly two orthogonal open neighbors; divide by all open cells. Every highlighted cell contributes once.',limits:'Degree two includes both bends and straight corridors. It does not by itself force two solution edges because the cell may be an endpoint.',legend:'Green: open cell with exactly two open neighbors. Other open cells are gray.'},
 squares:{name:'Largest open square (side)',value:s=>s.largestOpenSquare[2],definition:'Find the largest completely open, axis-aligned square. The highlighted square is the exact coordinate witness saved in the stats.',limits:'One maximum can be an outlier. It ignores long thin open areas and the organization of the rest of the board. Square side is not area or a room decomposition.',legend:'Amber frame and tint: exact largest all-open square. Click the map to inspect cells in a labeled detail.'},
 walls:{name:'Largest wall square (side)',value:s=>s.largestWallSquare[2],definition:'Find the largest completely blocked, axis-aligned square. The red outline identifies the exact witness.',limits:'A large thin wall or diagonal barrier can still have a maximum square of side one. This is not wall component area.',legend:'Red frame and tint: exact largest all-wall square. Small squares are easiest to inspect in the labeled cell detail.'},
 interface:{name:'Interface sides per open cell',value:s=>s.interfacePerOpen,definition:'Count every open-cell side facing a wall or the board exterior and divide by the number of open cells. Equivalently (4N − 2E) / N, with E the open-to-open adjacency count.',limits:'It captures boundary density, not where boundaries occur. Different wall shapes and arrangements can produce the same value.',legend:'Open cells: yellow to red as their number of wall-facing sides rises from 0 to 4. Exterior boundaries count.'},
 axis:{name:'Horizontal axis bias',value:s=>s.axisBias,definition:'Count horizontal and vertical open adjacencies. Report (horizontal − vertical) / (horizontal + vertical). The overlay shows each cell’s local difference; global bias counts each adjacency once.',limits:'Opposing local orientations can cancel. A value near zero does not mean no directional structure, and orientation is not a measure of complexity.',legend:'Orange: more horizontal open neighbors. Blue: more vertical. Gray: balanced local counts. The color scale is the same for every board.'},
 symmetry:{name:'Adjusted left/right symmetry',value:s=>s.symmetry.mirrorX,definition:'Compare each cell to its reflected partner across the vertical midline. Adjust observed agreement for p² + (1−p)², the independent baseline at this occupancy.',limits:'A high raw agreement can come from an almost uniform board. This adjusted value tests one global reflection, not local repeated motifs, translation symmetry, or rotational structure.',legend:'Magenta: mismatch with the horizontally reflected cell. Matching cells retain their wall/open shade.'},
 edge:{name:'Outer-edge occupancy contrast (pp)',value:s=>100*(s.edgeLayers[0].openFraction-s.openFraction),definition:'Subtract overall open fraction from the occupancy of the outermost layer. The slider highlights other exact-distance layers for context; the headline statistic always refers to distance zero.',limits:'It only summarizes the outer border. An unusual region just inside the border can be missed, which is why the full edge profile is also saved.',legend:'Green: open cells in the chosen edge-distance layer. Red: wall cells in that layer. Slider distance is measured in cells.'}
};
function cells(board) { return board.geometry ||= (board.side>1000?window.CoilBoardGeometry.inspectLarge(board):window.CoilBoardGeometry.decode(board)); }
const format = window.CoilStatsLedger.number;
const metricLabels={occupancy:'Open %',density:'Density variance',runs:'Mean run',pillars:'Pillars %',degree:'Degree 2 %',squares:'Open square',walls:'Wall square',interface:'Interface',axis:'Axis bias',symmetry:'Symmetry',edge:'Edge Δ pp'};
const query = new URLSearchParams(location.search);
const clamp = (n,low,high) => Math.max(low,Math.min(high,n));
function queryInteger(key,fallback,low,high) {
  const raw = query.get(key);
  return raw !== null && /^-?\d+$/.test(raw) ? clamp(Number(raw),low,high) : fallback;
}
const requested = query.get('board') || query.get('left');
const initialId = byId.has(requested) ? requested : 'original-game/level-0001';
function filterBoards(rows,preferred) {
  visibleBoards=rows;

  for(const id of ['previous-board','next-board'])$(id).disabled=!rows.length;
  if(!rows.length) {
    loadVersion++;if(activeBoard)delete activeBoard.geometry;activeBoard=null;$('inspection').hidden=true;
    $('loading-status').textContent='No boards match. Change a filter or choose Clear filters.';
    $('sampling-context').textContent='Select a saved board to see its creation method.';$('creation-parameters').innerHTML='';$('sampling-trace').innerHTML='';$('stats-ledger').innerHTML='';$('raw-stats').textContent='';$('stats-board-name').textContent='No board selected';$('stats-scope').textContent='';syncURL();return;
  }
  const id=rows.some(b=>b.id===preferred)?preferred:rows[0].id;
  if(activeBoard?.id===id){loadVersion++;selectedId=id;$('inspection').setAttribute('aria-busy','false');$('loading-status').textContent='';$('board-position').textContent=`${rows.findIndex(b=>b.id===id)+1} / ${rows.length}`;browserUI.select(id);syncURL();return;}
  chooseBoard(id);
}
let selectedMetric = Object.hasOwn(metrics,query.get('metric')) ? query.get('metric') : 'runs';
let selectedDisplay = ['raw','overlay','solution'].includes(query.get('display')) ? query.get('display') : 'raw';
$('lab-view').innerHTML='<option value="raw">Board</option><optgroup label="Measurements">'+Object.entries(metrics).map(([key,m])=>`<option value="${key}">${m.name}</option>`).join('')+'</optgroup>';
let solutionDetail=query.get('solutionDetail')==='sampled'?'sampled':'cells';
let preferredSample=query.has('sample')?queryInteger('sample',100,1,100000000):null;
let displayBeforeSolution='raw';
let preferredZoom = queryInteger('zoom',48,1,10000);
$('run-min').value = queryInteger('run',10,2,100);
$('edge-distance').value = queryInteger('edge',0,0,4999);
let focus = {x:queryInteger('x',500,0,9999),y:queryInteger('y',500,0,9999)};
const surface = document.createElement('canvas'); surface.width = 1; surface.height = 1;
let surfaceKey = '', drag = null, framePending = false;
function cropBounds() {
  const size = Number($('zoom-size').value);
  return {size,x:clamp(Math.floor(focus.x-size/2),0,width()-size),y:clamp(Math.floor(focus.y-size/2),0,height()-size)};
}
function syncURL() {
  const url = new URL(location.href); url.searchParams.delete('left');url.hash=activePanel==='creation'?'':activePanel;
  for (const [key,value] of Object.entries({...browserUI.state(),panel:activePanel,board:selectedId,metric:selectedMetric,display:selectedDisplay,zoom:$('zoom-size').value,x:focus.x,y:focus.y,run:$('run-min').value,edge:$('edge-distance').value,solutionDetail})) url.searchParams.set(key,String(value));
  if(preferredSample!==null)url.searchParams.set('sample',String(preferredSample));else url.searchParams.delete('sample');
  history.replaceState(null,'',url);
}
function renderSurface() {
  const board = currentBoard(), key = selectedMetric, overlay = selectedDisplay === 'overlay';
  const threshold = Number($('run-min').value), layer = Number($('edge-distance').value);
  const signature = [board.id,key,selectedDisplay,solutionDetail,threshold,layer].join(':');
  if (signature === surfaceKey) return;
  surfaceKey = signature;
  const w=width(),h=height(); surface.width=w; surface.height=h;
  const g = cells(board), ctx = surface.getContext('2d'), img = ctx.createImageData(w,h);
  for (let y=0;y<h;y++) for (let x=0;x<w;x++) {
    const i = y*w+x, isWall = g.wall[i]; let rgb = isWall ? [0,0,0] : [255,255,255];
    if (overlay) {
      rgb = isWall ? [12,20,15] : [204,211,206];
      if (key==='occupancy'&&!isWall) rgb=[128,239,169];
      if (key==='density') {const p=g.tiles[Math.floor(y/g.tileSide)*g.columns+Math.floor(x/g.tileSide)];rgb=[Math.round(20+235*p),Math.round(45+175*p),Math.round(105-55*p)];}
      if (key==='runs'&&!isWall) {const h=g.horizontal[i]>=threshold,v=g.vertical[i]>=threshold;rgb=h&&v?[194,120,231]:h?[248,157,66]:v?[72,160,244]:rgb;}
      if (key==='pillars'&&isWall&&g.degree[i]===(x>0)+(x<w-1)+(y>0)+(y<h-1)) rgb=[255,65,81];
      if (key==='degree'&&!isWall&&g.degree[i]===2) rgb=[86,233,154];
      if (key==='interface'&&!isWall) {const n=4-g.degree[i];rgb=[255,240-n*45,140-n*30];}
      if (key==='axis'&&!isWall) {const hd=(x>0&&!g.wall[i-1])+(x<w-1&&!g.wall[i+1]),vd=(y>0&&!g.wall[i-w])+(y<h-1&&!g.wall[i+w]);rgb=hd>vd?[246,158,67]:vd>hd?[66,161,245]:[204,211,206];}
      if (key==='symmetry'&&isWall!==g.wall[y*w+w-1-x]) rgb=[242,75,183];
      if (key==='edge'&&Math.min(x,y,w-1-x,h-1-y)===layer) rgb=isWall?[255,70,90]:[81,240,145];
    }
    if (selectedDisplay==='solution' && solutionDetail==='sampled') rgb=isWall?[13,22,18]:[48,65,55];
    else if (selectedDisplay==='solution' && !isWall) {
      const t=(g.visit[i]-1)/Math.max(1,board.stats.openCells-1);
      rgb=[Math.round(55+200*t),Math.round(180-70*t),Math.round(240-160*t)];
    }
    const k=i*4; img.data[k]=rgb[0]; img.data[k+1]=rgb[1]; img.data[k+2]=rgb[2]; img.data[k+3]=255;
  }
  ctx.putImageData(img,0,0);
  if (overlay&&key==='density') {
    ctx.strokeStyle='#15251c88';ctx.lineWidth=.5;
    for(let x=0;x<=w;x+=g.tileSide){ctx.beginPath();ctx.moveTo(x,0);ctx.lineTo(x,h);ctx.stroke();}for(let y=0;y<=h;y+=g.tileSide){ctx.beginPath();ctx.moveTo(0,y);ctx.lineTo(w,y);ctx.stroke();}
  }
  if (overlay&&(key==='squares'||key==='walls')) {
    const [x,y,n]=board.stats[key==='squares'?'largestOpenSquare':'largestWallSquare'];
    ctx.fillStyle=key==='squares'?'#ffa92a88':'#ff234c88';ctx.fillRect(x,y,n,n);
    ctx.strokeStyle=key==='squares'?'#bd6600':'#ff234c';ctx.lineWidth=1;ctx.strokeRect(x,y,n,n);
  }
}
function overviewBounds() {
  const scale=640/side();return {x:(640-width()*scale)/2,y:(640-height()*scale)/2,w:width()*scale,h:height()*scale,scale};
}
function sampledPoints() {
  const board=currentBoard(),g=cells(board),stride=Number($('solution-step').value);
  if(g.sampledSolution?.stride!==stride)g.sampledSolution={stride,points:window.CoilBoardGeometry.sampleSolution(board,stride,g)};
  return g.sampledSolution.points;
}
function drawSampledPath(ctx,bounds,target) {
  const points=sampledPoints(),total=Math.max(1,currentBoard().stats.openCells-1);
  const x=p=>target.x+(p.x+.5-bounds.x)*target.w/bounds.w,y=p=>target.y+(p.y+.5-bounds.y)*target.h/bounds.h;
  ctx.save();ctx.beginPath();ctx.rect(target.x,target.y,target.w,target.h);ctx.clip();
  ctx.lineWidth=1.6;ctx.lineJoin='round';ctx.lineCap='round';
  const arrowEvery=Math.max(1,Math.ceil(points.length/30));
  for(let i=1;i<points.length;i++) {
    const a=points[i-1],b=points[i],ax=x(a),ay=y(a),bx=x(b),by=y(b),t=(b.visit-1)/total;
    ctx.strokeStyle=`rgb(${Math.round(55+200*t)},${Math.round(180-70*t)},${Math.round(240-160*t)})`;
    ctx.beginPath();ctx.moveTo(ax,ay);ctx.lineTo(bx,by);ctx.stroke();
    const length=Math.hypot(bx-ax,by-ay);
    if(i%arrowEvery===0&&length>12) {
      const dx=(bx-ax)/length,dy=(by-ay)/length,mx=(ax+bx)/2,my=(ay+by)/2;
      ctx.beginPath();ctx.moveTo(mx-dx*5-dy*3,my-dy*5+dx*3);ctx.lineTo(mx,my);ctx.lineTo(mx-dx*5+dy*3,my-dy*5-dx*3);ctx.stroke();
    }
  }
  for(const [point,color,label] of [[points[0],'#37b4f0','S'],[points.at(-1),'#ff6e50','F']]) {
    if(!point)continue;
    ctx.fillStyle=color;ctx.beginPath();ctx.arc(x(point),y(point),4,0,Math.PI*2);ctx.fill();
    ctx.font='bold 13px system-ui';ctx.fillText(label,x(point)+7,y(point)-5);
  }
  ctx.restore();
}
function renderViews() {
  if (!activeBoard) return;
  if(side()<=1000)renderSurface();else renderLargeSurface();
  const bounds=cropBounds(),ctx=$('board-map').getContext('2d'),map=overviewBounds();
  ctx.fillStyle='#101914';ctx.fillRect(0,0,640,640);ctx.imageSmoothingEnabled=false;
  ctx.drawImage(surface,0,0,surface.width,surface.height,map.x,map.y,map.w,map.h);
  if(selectedDisplay==='solution'&&solutionDetail==='sampled')drawSampledPath(ctx,{x:0,y:0,w:width(),h:height()},map);
  ctx.strokeStyle='#00e9ff';ctx.lineWidth=2;ctx.strokeRect(map.x+bounds.x*map.scale,map.y+bounds.y*map.scale,Math.max(1,bounds.size*map.scale),Math.max(1,bounds.size*map.scale));
  if(bounds.size*map.scale<8){const cx=map.x+(bounds.x+bounds.size/2)*map.scale,cy=map.y+(bounds.y+bounds.size/2)*map.scale;ctx.beginPath();ctx.moveTo(cx-10,cy);ctx.lineTo(cx-4,cy);ctx.moveTo(cx+4,cy);ctx.lineTo(cx+10,cy);ctx.moveTo(cx,cy-10);ctx.lineTo(cx,cy-4);ctx.moveTo(cx,cy+4);ctx.lineTo(cx,cy+10);ctx.stroke();}
  const zoom=$('board-zoom'),z=zoom.getContext('2d'),{x,y,size}=bounds;
  z.fillStyle='#183527';z.fillRect(0,0,640,640);z.imageSmoothingEnabled=false;
  if(side()>1000){const crop=renderLargeRegion({x,y,width:size,height:size},Math.min(576,size));z.drawImage(crop,0,0,crop.width,crop.height,32,40,576,576);}else z.drawImage(surface,x,y,size,size,32,40,576,576);
  if($('zoom-grid').checked&&size<=144){z.strokeStyle='#64706777';z.lineWidth=.65;for(let i=0;i<=size;i++){const at=i*576/size;z.beginPath();z.moveTo(32+at,40);z.lineTo(32+at,616);z.moveTo(32,40+at);z.lineTo(608,40+at);z.stroke();}}
  if(selectedDisplay==='solution'&&solutionDetail==='sampled')drawSampledPath(z,{x,y,w:size,h:size},{x:32,y:40,w:576,h:576});
  z.strokeStyle='#00e9ff';z.lineWidth=2;z.strokeRect(31,39,578,578);
  z.fillStyle='#a5eac6';z.font='bold 16px system-ui';z.fillText(`CROP · ${size}×${size} · x=${x}–${x+size-1}, y=${y}–${y+size-1}`,20,25);
  z.font='13px system-ui';z.fillText(`Detail of the ${width()}×${height()} board · zero-based coordinates`,20,636);
  $('zoom-coordinates').textContent=`x ${x}–${x+size-1} · y ${y}–${y+size-1} · drag to pan`;
  zoom.setAttribute('aria-label',`Crop of ${currentBoard().title}: x ${x} through ${x+size-1}, y ${y} through ${y+size-1}. Drag to pan.`);
  $('focus-x').value=focus.x;$('focus-y').value=focus.y;syncURL();
}
function queueViews() {
  if(framePending)return;framePending=true;
  requestAnimationFrame(()=>{framePending=false;renderViews();});
}
function renderSurvey() {
  const r=data.ranges[$('survey-scope').value][selectedMetric];
  $('survey-readout').innerHTML=`<article><h3>${r.configurations} recipe means</h3><div class="big">${format(r.minMean)} → ${format(r.maxMean)}</div><p>Range of three-seed means</p></article><article><h3>Typical seed variation</h3><div class="big">${format(r.medianSeedRange)}</div><p>Median within-recipe max − min</p></article><article><h3>Largest seed range</h3><div class="big">${format(r.widest.max-r.widest.min)}</div><p>${escapeText(r.widest.recipe)}</p></article>`;
}
function renderMetric() {
  if (!activeBoard) return;
  const key=selectedMetric,m=metrics[key],board=currentBoard(),raw=selectedDisplay==='raw';
  $('run-control').hidden=key!=='runs'||selectedDisplay!=='overlay';$('layer-control').hidden=key!=='edge'||selectedDisplay!=='overlay';
  $('run-value').textContent=$('run-min').value+' cells';$('layer-value').textContent=$('edge-distance').value+' cells';
  $('lab-view').value=selectedDisplay==='overlay'?selectedMetric:'raw';
  $('show-solution').checked=selectedDisplay==='solution';
  $('solution-controls').hidden=selectedDisplay!=='solution';
  $('solution-detail').value=solutionDetail;
  $('sample-control').hidden=solutionDetail!=='sampled';
  $('lab-legend').hidden=selectedDisplay==='raw';
  const solutionLegend=solutionDetail==='sampled'?`Sampled solution · every ${Number($('solution-step').value).toLocaleString()} cells, plus both endpoints. Blue S → orange F. Lines skip intermediate moves and may cross walls.`:'Legal solution · every cell in visit order. Blue start → orange finish.';
  $('lab-legend').textContent=(selectedDisplay==='solution'?solutionLegend:raw?'Original board: white open, black wall.':m.legend)+' Cyan frame: close-up area.';
  $('metric-definition').textContent=m.definition;$('metric-limits').textContent=m.limits;
  $('metric-readout').textContent='The reported value always describes the complete board. Overlay controls change the highlighted cells; the zoom changes only the view.';
  if(selectedDisplay==='overlay')$('lab-legend').textContent=`${m.name}: ${format(m.value(board.stats))}. `+$('lab-legend').textContent;
  let detail=board.id;
  if(key==='squares'||key==='walls') {const [x,y,n]=board.stats[key==='squares'?'largestOpenSquare':'largestWallSquare'];detail+=` · Witness x=${x}, y=${y}, side=${n}.`;}
  if(key==='edge') detail+=` · Highlighted layer ${$('edge-distance').value}: ${format(board.stats.edgeLayers[Number($('edge-distance').value)].openFraction*100)}% open. Headline contrast always uses layer 0.`;

  document.querySelectorAll('[data-headline]').forEach(button=>button.setAttribute('aria-pressed',String(selectedDisplay==='overlay'&&button.dataset.headline===key)));
  renderSurvey();renderViews();
}
function renderParameters(board) {
  const names={archiveSettings:'Recorded recipe headings',generationSeconds:'Generation time (seconds)',solutionSlides:'Legal solution slides',targetOpenCells:'Target open-cell count',stopReason:'Reason the run stopped',choiceCount:'Random choices used',constructionHexDigits:'Construction code length (hex digits)',validation:'Validation',generator:'Creation method identifier',sampler:'Sampling rule',generationPolicy:'Output policy',picker:'Tweak picker',segpicker:'Segment picker',lim:'Candidate limit',steps:'Steps',activity:'Activity',initialization:'Initialization',spacing:'Stripe spacing',kernel:'Edit kernel',blockProbability:'Region replacement probability',blockSides:'Region side lengths',lazyProbability:'No-change probability',endpointMax:'Maximum endpoint edit',rectangleMax:'Maximum detour length',heightMax:'Maximum detour height',operationOrder:'Edit choices',burnIn:'Burn-in steps',stride:'Steps between draws',catalogueSize:'Solvable-board catalog size',randomAlgorithm:'Random-choice algorithm'};
  function rows(value,path='') {
    return Object.entries(value).map(([key,v])=>{
      const at=path?path+'.'+key:key;
      if(v!==null&&typeof v==='object'&&!Array.isArray(v))return rows(v,at);
      return `<tr><th>${escapeText(names[key]||key)}<code>${escapeText(at)}</code></th><td data-parameter-path="${escapeText(at)}">${escapeText(Array.isArray(v)?v.join(', '):v===null?'Not recorded':typeof v==='number'?format(v):v)}</td></tr>`;
    }).join('');
  }
  const m=board.metadata, batch=m.samplingBatch||{};
  if(m.generator==='original-game'){$('creation-parameters').innerHTML=`<h3>Archive record</h3><table><tbody><tr><th>Game level</th><td>${m.levelNumber}</td></tr><tr><th>Dimensions</th><td>${dimensions(board)}</td></tr><tr><th>Published collection</th><td><a href="${escapeText(m.archiveUrl)}">coilbench · data-v1.0</a></td></tr></tbody></table>`;return;}
  const seed=m.seedHex??m.seed??batch.SeedHex??batch.seedHex;
  const outcomes={};for(const key of ['generationSeconds','solutionSlides','targetOpenCells','stopReason','choiceCount','constructionHexDigits','validation'])if(m[key]!==undefined)outcomes[key]=m[key];
  $('creation-parameters').innerHTML=`<h3>${escapeText(board.configuration.label)}</h3><div class="parameter-grid"><article><h3>Configuration parameters</h3><table><tbody>${rows(board.configuration.parameters)}</tbody></table></article><article><h3>This saved run</h3><table><tbody><tr><th>Board dimensions</th><td>${dimensions(board)}</td></tr><tr><th>${m.samplingBatch?'Batch seed':'Seed'}</th><td class="seed-value">${seed===undefined?'Not recorded':String(seed).length>40?`<details><summary>${escapeText(String(seed).slice(0,12))}…${escapeText(String(seed).slice(-8))}</summary><code>${escapeText(seed)}</code></details>`:escapeText(seed)}</td></tr>${m.sampleRecord?`<tr><th>Saved draw</th><td>${m.sampleRecord.index}</td></tr>`:''}${rows(outcomes)}</tbody></table></article></div>`;
}
function renderLargeRegion(bounds,pixels) {
  const board=currentBoard(),g=cells(board),w=width(),h=height(),key=selectedMetric,overlay=selectedDisplay==='overlay';
  const canvas=document.createElement('canvas');canvas.width=Math.max(1,Math.round(pixels*bounds.width/Math.max(bounds.width,bounds.height)));canvas.height=Math.max(1,Math.round(pixels*bounds.height/Math.max(bounds.width,bounds.height)));
  const context=canvas.getContext('2d'),image=context.createImageData(canvas.width,canvas.height),threshold=Number($('run-min').value),layer=Number($('edge-distance').value);
  for(let py=0;py<canvas.height;py++)for(let px=0;px<canvas.width;px++) {
    const x=bounds.x+Math.floor((px+.5)*bounds.width/canvas.width),y=bounds.y+Math.floor((py+.5)*bounds.height/canvas.height),i=y*w+x,isWall=g.wallAt(i);
    let rgb=selectedDisplay==='solution'?(isWall?[13,22,18]:[48,65,55]):(isWall?[0,0,0]:[255,255,255]);
    if(overlay) {
      rgb=isWall?[12,20,15]:[204,211,206];
      if(key==='occupancy'&&!isWall)rgb=[128,239,169];
      if(key==='density'){const p=g.tileAt(x,y);rgb=[Math.round(20+235*p),Math.round(45+175*p),Math.round(105-55*p)];}
      if(key==='runs'&&!isWall){const h=g.runAt(x,y,false)>=threshold,v=g.runAt(x,y,true)>=threshold;rgb=h&&v?[194,120,231]:h?[248,157,66]:v?[72,160,244]:rgb;}
      if(key==='pillars'&&isWall&&g.degreeAt(x,y)===(x>0)+(x<w-1)+(y>0)+(y<h-1))rgb=[255,65,81];
      if(key==='degree'&&!isWall&&g.degreeAt(x,y)===2)rgb=[86,233,154];
      if(key==='interface'&&!isWall){const d=4-g.degreeAt(x,y);rgb=[255,240-d*45,140-d*30];}
      if(key==='axis'&&!isWall){const hd=(x>0&&!g.wallAt(i-1))+(x<w-1&&!g.wallAt(i+1)),vd=(y>0&&!g.wallAt(i-w))+(y<h-1&&!g.wallAt(i+w));rgb=hd>vd?[246,158,67]:vd>hd?[66,161,245]:rgb;}
      if(key==='symmetry'&&isWall!==g.wallAt(y*w+w-1-x))rgb=[242,75,183];
      if(key==='edge'&&Math.min(x,y,w-1-x,h-1-y)===layer)rgb=isWall?[255,70,90]:[81,240,145];
      if(key==='squares'||key==='walls'){const [sx,sy,length]=board.stats[key==='squares'?'largestOpenSquare':'largestWallSquare'];if(x>=sx&&x<sx+length&&y>=sy&&y<sy+length)rgb=key==='squares'?[255,169,42]:[255,35,76];}
    }
    const k=(py*canvas.width+px)*4;image.data[k]=rgb[0];image.data[k+1]=rgb[1];image.data[k+2]=rgb[2];image.data[k+3]=255;
  }
  context.putImageData(image,0,0);return canvas;
}
function renderLargeSurface() {
  const signature=[currentBoard().id,selectedMetric,selectedDisplay,$('run-min').value,$('edge-distance').value].join(':');
  if(surfaceKey===signature)return;
  const overview=renderLargeRegion({x:0,y:0,width:width(),height:height()},640);surface.width=overview.width;surface.height=overview.height;surface.getContext('2d').drawImage(overview,0,0);surfaceKey=signature;
}

function renderRecord() {
  const board=currentBoard(), record={...board.metadata,stats:board.stats};
  $('stats-board-name').textContent=board.id;
  $('stats-ledger').innerHTML=window.CoilStatsLedger.render(board);
  $('raw-stats').textContent=JSON.stringify(record,null,2);
  $('headline-stats').innerHTML=Object.entries(metrics).map(([key,m])=>`<button class="stat-button" data-headline="${key}" aria-pressed="false" title="${escapeText(m.name)}: ${m.value(board.stats)??'undefined'} · click to show overlay"><span>${metricLabels[key]}</span><strong>${format(m.value(board.stats))}</strong></button>`).join('')+`<div class="compact-stat" title="Open cells in the whole board"><span>Open cells</span><strong>${format(board.stats.openCells)}</strong></div>`;
  window.CoilSamplingResearch.renderBoard(board);
  renderParameters(board);
  const names={map:'Full-resolution PNG',stats:'Exact stats JSON',board:'Board',solution:'Legal solution',constructionCode:'Construction code',recipe:'Replay recipe',solutionMap:'Solution colors PNG',samplingBatch:'Sampling batch'};
  $('board-links').innerHTML='<a href="#all-stats">Full stats</a>'+Object.entries(board.assets).filter(([key])=>names[key]).map(([key,url])=>`<a href="${escapeText(url)}" target="_blank" rel="noopener">${names[key]}</a>`).join('');
}
async function chooseBoard(id) {
  if(id===activeBoard?.id&&selectedId===id)return;
  const version=++loadVersion, entry=byId.get(id);
  selectedId=id; $('inspection').setAttribute('aria-busy','true'); $('loading-status').textContent=`Loading ${entry.title}…`;
  $('navigation-note').textContent='';
  try {
    if (!window.COIL_STATS_RECORDS[id]) await new Promise((resolve,reject)=>{
      const script=document.createElement('script'); script.src=entry.recordScript+'?v='+entry.boardSha256+'&schema=sources-v1';
      script.onload=()=>{script.remove();resolve();};
      script.onerror=()=>{script.remove();reject(new Error('Could not load '+entry.recordScript));};
      document.head.append(script);
    });
    if (version!==loadVersion) return;
    const board=window.COIL_STATS_RECORDS[id];
    if (!board) throw new Error('Board record was not registered');
    cells(board);
    if (activeBoard) delete activeBoard.geometry;
    activeBoard=board;
    browserUI.select(id);
    // Only the current record needs to stay resident; script loading also works from file://.
    for (const key of Object.keys(window.COIL_STATS_RECORDS)) if (key!==id) delete window.COIL_STATS_RECORDS[key];
    const n=side();
    $('show-solution').disabled=!board.solutionAvailable;
    $('solution-detail').querySelector('option[value=cells]').disabled=n>1000;
    if(n>1000)solutionDetail='sampled';
    const minSample=Math.max(1,Math.ceil(board.stats.openCells/50000));
    $('solution-step').min=minSample;$('solution-step').max=board.stats.openCells;
    $('solution-step').value=clamp(preferredSample??Math.max(1,Math.ceil(board.stats.openCells/600)),minSample,board.stats.openCells);
    $('solution-status').textContent=board.solutionAvailable?'':'No saved solution';
    for(const id of ['focus-start','focus-finish'])$(id).disabled=!board.solutionAvailable;
    if(!board.solutionAvailable&&selectedDisplay==='solution')selectedDisplay='raw';
    $('zoom-size').max=Math.min(width(),height());
    syncZoomControls(clamp(preferredZoom,1,Number($('zoom-size').max)));
    $('focus-x').max=width()-1;$('focus-y').max=height()-1;
    $('edge-distance').max=Math.floor((Math.min(width(),height())-1)/2);
    $('edge-distance').value=clamp(Number($('edge-distance').value),0,Math.floor((Math.min(width(),height())-1)/2));
    $('board-dimensions').textContent=n>640?'Reduced overview':'';
    $('selected-source').textContent=board.source.label;$('selected-source').dataset.source=board.source.id;
    $('selected-title').textContent=board.title;
    $('selected-title').title=board.title;
    $('board-position').textContent=`${visibleBoards.findIndex(b=>b.id===id)+1} / ${visibleBoards.length}`;
    $('selected-subtitle').textContent=dimensions(board);
    $('selected-subtitle').title=board.configuration.methodLabel;
    $('stats-scope').textContent=`Every saved geometry measurement appears below. Values describe all ${format(width()*height())} cells, regardless of the zoom. Select a headline value to see its overlay; expand a distribution table for every exact count.`;
    if (version===1 && query.has('x') && query.has('y')) focus={x:clamp(focus.x,0,width()-1),y:clamp(focus.y,0,height()-1)};
    else if (board.collection==='backward-v1') focus={...board.geometry.start};
    else focus={x:Math.floor(width()/2),y:Math.floor(height()/2)};
    surfaceKey='';$('loading-status').textContent='';$('inspection').hidden=false;
    renderRecord();renderMetric();
  } catch(error) {
    if (version!==loadVersion) return;
    selectedId=activeBoard?.id||'';browserUI.select(selectedId);
    $('loading-status').textContent='Unable to display board: '+error.message;
    syncURL();
    console.error(error);
  } finally {
    if(version===loadVersion)$('inspection').setAttribute('aria-busy','false');
  }
}
function chooseMetric(key,scroll=false) {
  selectedMetric=key;selectedDisplay='overlay';renderMetric();
  if(scroll)$('viewer').scrollIntoView({block:'start',behavior:'instant'});
}
for(const [id,delta] of [['previous-board',-1],['next-board',1]]) $(id).addEventListener('click',()=>chooseBoard(visibleBoards[(visibleBoards.findIndex(b=>b.id===selectedId)+delta+visibleBoards.length)%visibleBoards.length].id));
$('lab-view').addEventListener('change',()=>{const value=$('lab-view').value;selectedDisplay=value==='raw'?'raw':'overlay';if(selectedDisplay==='overlay')selectedMetric=value;renderMetric();});
function toggleSolution() {
  if(!activeBoard||$('show-solution').disabled)return;
  if(selectedDisplay==='solution')selectedDisplay=displayBeforeSolution;
  else {displayBeforeSolution=selectedDisplay;selectedDisplay='solution';}
  renderMetric();
}
$('show-solution').addEventListener('change',toggleSolution);
$('solution-detail').addEventListener('change',()=>{solutionDetail=$('solution-detail').value;renderMetric();});
$('solution-step').addEventListener('change',()=>{
  const value=Number($('solution-step').value);
  preferredSample=clamp(Number.isFinite(value)?Math.round(value):1,Number($('solution-step').min),Number($('solution-step').max));
  $('solution-step').value=preferredSample;renderMetric();
});
document.addEventListener('keydown',event=>{
  if(event.key!=='s'||event.repeat||event.altKey||event.ctrlKey||event.metaKey||event.target.closest('input,select,textarea,[contenteditable]:not([contenteditable="false"])'))return;
  event.preventDefault();toggleSolution();
});
for(const id of ['run-min','edge-distance']) $(id).addEventListener('input',renderMetric);
$('survey-scope').addEventListener('change',renderSurvey);
function syncZoomControls(size,updateRange=true) {
  $('zoom-size').value=size;
  const maximum=Number($('zoom-size').max);
  if(updateRange)$('zoom-range').value=maximum<=1?0:Math.round(Math.log(size)/Math.log(maximum)*1000);
  $('zoom-range').disabled=maximum<=1;
  $('zoom-range').setAttribute('aria-valuetext',`${size} × ${size} cells`);
  $('zoom-less').disabled=size<=1;$('zoom-more').disabled=size>=maximum;
}
function setZoom(size,updateRange=true) {
  if(!activeBoard)return;
  preferredZoom=clamp(Math.round(size),1,Number($('zoom-size').max));
  syncZoomControls(preferredZoom,updateRange);queueViews();
}
$('zoom-size').addEventListener('input',()=>{if($('zoom-size').value!==''&&Number.isFinite($('zoom-size').valueAsNumber))setZoom($('zoom-size').valueAsNumber);});
$('zoom-size').addEventListener('change',()=>setZoom(Number($('zoom-size').value)||preferredZoom));
$('zoom-range').addEventListener('input',()=>setZoom(Math.exp(Number($('zoom-range').value)/1000*Math.log(Number($('zoom-size').max))),false));
for(const [id,delta] of [['zoom-less',-1],['zoom-more',1]])$(id).addEventListener('click',()=>setZoom(Number($('zoom-size').value)+delta));
$('zoom-grid').addEventListener('change',renderViews);
$('focus-start').addEventListener('click',()=>{focus={...cells(currentBoard()).start};renderViews();});
$('focus-finish').addEventListener('click',()=>{const g=cells(currentBoard());focus={...(g.finish||g.getFinish())};renderViews();});
for(const axis of ['x','y']) $('focus-'+axis).addEventListener('input',()=>{
  if($('focus-'+axis).value==='')return;
  const value=Number($('focus-'+axis).value);focus[axis]=Number.isFinite(value)?clamp(Math.round(value),0,(axis==='x'?width():height())-1):focus[axis];renderViews();
});
$('headline-stats').addEventListener('click',event=>{const button=event.target.closest('[data-headline]');if(button)chooseMetric(button.dataset.headline,true);});
$('stats-ledger').addEventListener('click',event=>{
  const button=event.target.closest('button');if(!button)return;
  if(button.dataset.overlay)chooseMetric(button.dataset.overlay,true);
  if(button.dataset.focusSquare) {
    const key=button.dataset.focusSquare,[x,y,n]=currentBoard().stats[key==='squares'?'largestOpenSquare':'largestWallSquare'];
    focus={x:clamp(Math.floor(x+n/2),0,width()-1),y:clamp(Math.floor(y+n/2),0,height()-1)};
    $('zoom-size').value=String([...$('zoom-size').options].map(o=>Number(o.value)).find(size=>size>=n+6)||Math.min(width(),height()));chooseMetric(key,true);
  }
  if(button.dataset.edgeLayer!==undefined) {
    const layer=Number(button.dataset.edgeLayer);$('edge-distance').value=layer;focus={x:Math.floor(width()/2),y:layer};chooseMetric('edge',true);
  }
});
function pointOnBoard(event) {
  const rect=$('board-map').getBoundingClientRect(),map=overviewBounds();
  focus={x:clamp(Math.floor(((event.clientX-rect.left)/rect.width*640-map.x)/map.scale),0,width()-1),y:clamp(Math.floor(((event.clientY-rect.top)/rect.height*640-map.y)/map.scale),0,height()-1)};
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
      const scale=cropBounds().size/(canvas.getBoundingClientRect().width*576/640);
      focus={x:clamp(Math.round(drag.focus.x-(event.clientX-drag.x)*scale),0,width()-1),y:clamp(Math.round(drag.focus.y-(event.clientY-drag.y)*scale),0,height()-1)};
    }
    queueViews();
  });
  const endDrag=()=>{drag=null;};
  canvas.addEventListener('pointerup',endDrag);canvas.addEventListener('pointercancel',endDrag);canvas.addEventListener('lostpointercapture',endDrag);
  canvas.addEventListener('keydown',event=>{
    const moves={ArrowLeft:[-1,0],ArrowRight:[1,0],ArrowUp:[0,-1],ArrowDown:[0,1]};if(!moves[event.key])return;
    event.preventDefault();const [x,y]=moves[event.key],step=event.shiftKey?10:1;
    focus={x:clamp(focus.x+x*step,0,width()-1),y:clamp(focus.y+y*step,0,height()-1)};queueViews();
  });
}
let activePanel='creation';
const browserUI=window.CoilGalleryBrowser.init(data,metrics,filterBoards,chooseBoard);
function showPanel(id,scroll=false) {
  activePanel=id;
  for(const panel of document.querySelectorAll('.detail-panel')) panel.hidden=panel.id!==id;
  for(const button of document.querySelectorAll('[data-panel]')){const selected=button.dataset.panel===id;button.setAttribute('aria-selected',String(selected));button.tabIndex=selected?0:-1;}
  if(scroll)$(id).scrollIntoView({block:'start',behavior:'instant'});
  syncURL();
}
for(const button of document.querySelectorAll('[data-panel]')) {
  button.addEventListener('click',()=>showPanel(button.dataset.panel));
  button.addEventListener('keydown',event=>{if(!['ArrowLeft','ArrowRight'].includes(event.key))return;event.preventDefault();const buttons=[...document.querySelectorAll('[data-panel]')],next=buttons[(buttons.indexOf(button)+(event.key==='ArrowRight'?1:buttons.length-1))%buttons.length];showPanel(next.dataset.panel);next.focus();});
}
function panelForAnchor(hash) {
  if(['#all-stats','#guide-stats'].includes(hash))return 'all-stats';
  if(['#compare','#variation','#explore','#survey'].includes(hash))return 'compare';
  if(['#sampling-research','#research','#guide','#methods','#development'].includes(hash))return 'research';
  return null;
}
document.addEventListener('click',event=>{
  for(const popup of document.querySelectorAll('.viewer-caveats[open],.zoom-options[open]'))if(!popup.contains(event.target))popup.open=false;
  const anchor=event.target.closest('a');if(!anchor||event.ctrlKey||event.metaKey||event.shiftKey||event.button>0)return;
  const url=new URL(anchor.href,location.href);if(url.origin!==location.origin||!['gallery.html','stats-lab.html','board-wall.html','gallery-guide.html'].includes(url.pathname.split('/').at(-1)))return;
  if(!url.hash&&!url.search)return;
  event.preventDefault();
  const fragmentOnly=anchor.getAttribute('href').startsWith('#');
  const id=fragmentOnly?null:url.searchParams.get('board')||url.searchParams.get('left');
  if(id&&byId.has(id)) {browserUI.reveal(id);showPanel('creation');$('workspace-top').scrollIntoView({block:'start',behavior:'instant'});}
  else if(!fragmentOnly&&url.searchParams.has('collection')) {$('lab-method').value='all';$('lab-method').dispatchEvent(new Event('change'));$('lab-collection').value=url.searchParams.get('collection');browserUI.apply();$('board-thumbnails').scrollIntoView({block:'nearest'});}
  const panel=panelForAnchor(url.hash);if(panel){showPanel(panel);($(url.hash.slice(1))||$(panel)).scrollIntoView({block:'start',behavior:'instant'});}
  if(url.hash==='#viewer')$('workspace-top').scrollIntoView({block:'start',behavior:'instant'});
});
window.CoilSamplingResearch.renderOverview(data);
for(const [id,open] of [['toy-block',(x,y)=>x<4],['toy-stripes',(x,y)=>x%2===0]]){const ctx=$(id).getContext('2d');for(let y=0;y<8;y++)for(let x=0;x<8;x++){ctx.fillStyle=open(x,y)?'#fff':'#000';ctx.fillRect(x*20,y*20,20,20);}}
browserUI.apply(initialId);
showPanel(panelForAnchor(location.hash)||(['creation','all-stats','compare','research'].includes(query.get('panel'))?query.get('panel'):'creation'));
