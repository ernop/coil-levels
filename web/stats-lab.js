'use strict';
const data=window.COIL_STATS_LAB, byId=new Map(data.boards.map(b=>[b.id,b]));
const $=id=>document.getElementById(id), escapeText=s=>String(s).replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
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
const format=n=>n===null?'undefined':n.toLocaleString(undefined,{maximumSignificantDigits:6});
const options=data.boards.slice().sort((a,b)=>a.recipe.localeCompare(b.recipe)||a.seed-b.seed).map(b=>`<option value="${b.id}">${escapeText(b.recipe)} · seed ${b.seed}</option>`).join('');
$('lab-left').innerHTML=options;$('lab-right').innerHTML=options;
const requested=new URLSearchParams(location.search).get('left');
$('lab-left').value=byId.has(requested)?requested:'500-random-s101';$('lab-right').value='500-control-last-unlimited-s101';
$('lab-metric').innerHTML=Object.entries(metrics).map(([key,m])=>`<option value="${key}">${m.name}</option>`).join('');$('lab-metric').value='runs';
let focus={x:314,y:283};
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
function renderMap(board,which){
 const g=cells(board),canvas=$(which+'-map'),ctx=canvas.getContext('2d'),img=ctx.createImageData(500,500),key=$('lab-metric').value,overlay=$('lab-display').value==='overlay',threshold=Number($('run-min').value),layer=Number($('edge-distance').value);
 for(let y=0;y<500;y++)for(let x=0;x<500;x++){
  const i=y*500+x,w=g.wall[i];let rgb=w?[0,0,0]:[255,255,255];
  if(overlay){
   rgb=w?[12,20,15]:[204,211,206];
   if(key==='occupancy'&&!w)rgb=[128,239,169];
   if(key==='density'){const p=g.tiles[(y>>5)*16+(x>>5)];rgb=[Math.round(20+235*p),Math.round(45+175*p),Math.round(105-55*p)];}
   if(key==='runs'&&!w){const h=g.horizontal[i]>=threshold,v=g.vertical[i]>=threshold;rgb=h&&v?[194,120,231]:h?[248,157,66]:v?[72,160,244]:rgb;}
   if(key==='pillars'&&w&&g.degree[i]===(x>0)+(x<499)+(y>0)+(y<499))rgb=[255,65,81];
   if(key==='degree'&&!w&&g.degree[i]===2)rgb=[86,233,154];
   if(key==='interface'&&!w){const n=4-g.degree[i];rgb=[255,240-n*45,140-n*30];}
   if(key==='axis'&&!w){const h=(x>0&&!g.wall[i-1])+(x<499&&!g.wall[i+1]),v=(y>0&&!g.wall[i-500])+(y<499&&!g.wall[i+500]);rgb=h>v?[246,158,67]:v>h?[66,161,245]:[204,211,206];}
   if(key==='symmetry'&&w!==g.wall[y*500+499-x])rgb=[242,75,183];
   if(key==='edge'&&Math.min(x,y,499-x,499-y)===layer)rgb=w?[255,70,90]:[81,240,145];
  }
  const k=i*4;img.data[k]=rgb[0];img.data[k+1]=rgb[1];img.data[k+2]=rgb[2];img.data[k+3]=255;
 }
 ctx.putImageData(img,0,0);
 if(overlay&&key==='density'){ctx.strokeStyle='#15251c88';ctx.lineWidth=.5;for(let i=0;i<=500;i+=32){ctx.beginPath();ctx.moveTo(i,0);ctx.lineTo(i,500);ctx.moveTo(0,i);ctx.lineTo(500,i);ctx.stroke();}}
 if(overlay&&(key==='squares'||key==='walls')){const [x,y,n]=board.stats[key==='squares'?'largestOpenSquare':'largestWallSquare'];ctx.fillStyle=key==='squares'?'#ffa92a88':'#ff234c88';ctx.fillRect(x,y,n,n);ctx.strokeStyle=key==='squares'?'#bd6600':'#ff234c';ctx.lineWidth=1;ctx.strokeRect(x,y,n,n);}
 const sx=Math.max(0,Math.min(478,focus.x-11)),sy=Math.max(0,Math.min(478,focus.y-11));
 ctx.strokeStyle='#00e9ff';ctx.lineWidth=2;ctx.strokeRect(sx,sy,22,22);
 const m=metrics[key];$(which+'-value').textContent=format(m.value(board.stats));
 $(which+'-detail').textContent=`${m.name} · ${board.recipe} · seed ${board.seed}. Open ${format(100*board.stats.openFraction)}%.`;
 if(key==='squares'||key==='walls'){const [x,y,n]=board.stats[key==='squares'?'largestOpenSquare':'largestWallSquare'];$(which+'-detail').textContent+=` Witness: x=${x}, y=${y}, side=${n}; ${format(n*n/250000*100)}% of board area.`;}
 if(key==='edge')$(which+'-detail').textContent+=` Layer ${layer}: ${format(board.stats.edgeLayers[layer].openFraction*100)}% open.`;
 $(which+'-links').innerHTML=`<a href="${board.path}/map.png?v=${board.boardSha256}" target="_blank" rel="noopener">Full-resolution map</a><a href="${board.path}/stats.json?v=${board.boardSha256}">All measurements</a>`;
 renderZoom(board,which);
}
function renderZoom(board,which){
 const c=$(which+'-zoom'),ctx=c.getContext('2d'),g=cells(board),sx=Math.max(0,Math.min(478,focus.x-11)),sy=Math.max(0,Math.min(478,focus.y-11));
 ctx.fillStyle='#183527';ctx.fillRect(0,0,336,336);ctx.fillStyle='#a5eac6';ctx.font='bold 13px system-ui';ctx.fillText(`CROP · x=${sx}–${sx+21}, y=${sy}–${sy+21}`,16,22);
 for(let y=0;y<22;y++)for(let x=0;x<22;x++){ctx.fillStyle=g.wall[(y+sy)*500+x+sx]?'#000':'#fff';ctx.fillRect(36+x*12,40+y*12,12,12);ctx.strokeStyle='#8885';ctx.strokeRect(36+x*12,40+y*12,12,12);}
 ctx.fillStyle='#a5eac6';ctx.font='12px system-ui';ctx.fillText('Detail only · board continues outside this frame',16,326);
}
function survey(){const r=data.ranges[$('survey-scope').value][$('lab-metric').value];$('survey-readout').innerHTML=`<article><h3>${r.configurations} recipe means</h3><div class="big">${format(r.minMean)} → ${format(r.maxMean)}</div><p>Range of three-seed means</p></article><article><h3>Typical seed variation</h3><div class="big">${format(r.medianSeedRange)}</div><p>Median within-recipe max − min</p></article><article><h3>Largest seed range</h3><div class="big">${format(r.widest.max-r.widest.min)}</div><p>${escapeText(r.widest.recipe)}</p></article>`;}
function render(){const key=$('lab-metric').value,m=metrics[key];$('run-control').hidden=key!=='runs';$('layer-control').hidden=key!=='edge';$('run-value').textContent=$('run-min').value+' cells';$('layer-value').textContent=$('edge-distance').value+' cells';$('lab-legend').textContent=$('lab-display').value==='raw'?'Original board: white open, black wall. Cyan frame marks the labeled cell detail; click either full map to move it.':m.legend+' Cyan frame: the cell detail shown below.';$('metric-definition').textContent=m.definition;$('metric-limits').textContent=m.limits;$('metric-readout').textContent='Both reported values use the complete board. Overlay sliders only change what is highlighted.';for(const which of ['left','right'])renderMap(byId.get($('lab-'+which).value),which);survey();}
for(const id of ['lab-left','lab-right','lab-metric','lab-display','run-min','edge-distance','survey-scope'])$(id).addEventListener(id.includes('min')||id==='edge-distance'?'input':'change',()=>{ $('comparison-note').textContent='';render();});
for(const which of ['left','right'])$(which+'-map').addEventListener('click',event=>{const rect=event.currentTarget.getBoundingClientRect();focus={x:Math.min(499,Math.floor((event.clientX-rect.left)/rect.width*500)),y:Math.min(499,Math.floor((event.clientY-rect.top)/rect.height*500))};for(const side of ['left','right'])renderMap(byId.get($('lab-'+side).value),side);});
function compare(mode){const left=byId.get($('lab-left').value),m=metrics[$('lab-metric').value];let candidates=data.boards.filter(b=>b.id!==left.id);if(mode==='seed')candidates=candidates.filter(b=>b.recipe===left.recipe);else candidates=candidates.filter(b=>b.recipe!==left.recipe&&b.wallsBase64!==left.wallsBase64);candidates.sort((a,b)=>(Math.abs(m.value(a.stats)-m.value(left.stats))-Math.abs(m.value(b.stats)-m.value(left.stats)))*(mode==='far'?-1:1));if(!candidates.length){$('comparison-note').textContent='No other seed of this recipe is present in the 500-square set.';return;}$('lab-right').value=candidates[0].id;render();$('comparison-note').textContent=mode==='near'?'Closest numerical match from a different recipe, excluding identical board masks. Compare the original maps to see what this number misses.':mode==='far'?'Largest observed difference in this metric within the saved 500-square set.':'Same recipe, different seed; the method is held fixed.';}
$('compare-near').addEventListener('click',()=>compare('near'));$('compare-far').addEventListener('click',()=>compare('far'));$('compare-seed').addEventListener('click',()=>compare('seed'));
$('example-full').addEventListener('click',()=>{$('lab-left').value='500-random-s101';$('lab-right').value='500-control-last-unlimited-s101';$('lab-metric').value='runs';render();$('comparison-note').textContent='Compare fine random texture with the ordered long-tweak pattern. Change metrics to see which measurements expose that difference.';});
for(const which of ['left','right']){const details=document.createElement('details');details.open=true;details.innerHTML=`<summary>Cell detail — click a full map to move it</summary><canvas id="${which}-zoom" class="cell-zoom" width="336" height="336" aria-label="Labeled crop with individual cells"></canvas>`;$(which+'-links').after(details);}
render();
