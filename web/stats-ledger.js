'use strict';
// The ledger reads the complete saved record; it never measures the selected crop.
window.CoilStatsLedger = (() => {
  const esc = value => String(value).replace(/[&<>"']/g, c => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
  const number = value => value == null ? '—' : typeof value==='number'&&value!==0&&Math.abs(value)<.01 ? value.toExponential(1) : value.toLocaleString(undefined, {maximumFractionDigits:2});
  const percent = value => value === null ? 'undefined' : number(value * 100) + '%';
  const valueCell = (path, value, text = number(value)) => `<td class="value" data-stat-path="${esc(path)}" data-value="${esc(JSON.stringify(value))}" title="Saved value: ${esc(JSON.stringify(value))}">${esc(text)}</td>`;
  const row = (path, label, value, explanation, text) => `<tr><th scope="row" title="${esc(path)}${explanation?' · '+esc(explanation):''}">${esc(label)}</th>${valueCell(path,value,text)}</tr>`;
  const table = rows => `<table><tbody>${rows}</tbody></table>`;
  const group = (title, description, content, wide = false) => `<article class="stat-group${wide ? ' wide-group' : ''}"><h3>${title}</h3><details class="stat-notes"><summary>Notes</summary><p>${description}</p></details>${content}</article>`;

  function distributionChart(series, xLabel) {
    const allKeys = [...new Set(series.flatMap(s => Object.keys(s.values)))].map(Number).sort((a,b)=>a-b);
    const maximum = Math.max(1,...allKeys), peak = Math.max(1,...series.flatMap(s=>Object.values(s.values)));
    const x = n => 48 + (n - .5) / maximum * 490;
    const y = n => 166 - Math.log10(1+n) / Math.log10(1+peak) * 125;
    let svg = '<svg class="distribution-chart" viewBox="0 0 560 210" role="img" aria-label="Exact distribution; counts use a logarithmic scale"><text x="48" y="15" fill="#aec0b5" font-size="11">Count · log10(1 + count) scale</text>';
    let lastTickY = Infinity;
    for (let tick of [0,1,10,100,1000,10000,100000,1000000].filter(t=>t<=peak)) {
      if (lastTickY-y(tick)<14) continue;
      lastTickY=y(tick);
      svg += `<path d="M48 ${y(tick)}H542" stroke="#35493e"/><text x="42" y="${y(tick)+4}" text-anchor="end" fill="#aec0b5" font-size="10">${number(tick)}</text>`;
    }
    const width = 490 / maximum / series.length;
    for (const [index,seriesItem] of series.entries()) for (const [length,count] of Object.entries(seriesItem.values)) {
      svg += `<rect x="${x(Number(length)) - 245/maximum + index*width}" y="${y(count)}" width="${Math.max(.35,width*.88)}" height="${166-y(count)}" fill="${seriesItem.color}"><title>${esc(seriesItem.name)} · ${length} cells: ${number(count)}</title></rect>`;
    }
    svg += `<text x="48" y="185" fill="#aec0b5" font-size="11">1</text><text x="542" y="185" text-anchor="end" fill="#aec0b5" font-size="11">${maximum}</text><text x="295" y="204" text-anchor="middle" fill="#aec0b5" font-size="11">${xLabel}</text></svg>`;
    return svg;
  }

  function edgeChart(s) {
    const x = d => 48 + d / Math.max(1,s.edgeLayers.length-1) * 490;
    const points = s.edgeLayers.map(l=>`${x(l.distance)},${164-l.openFraction*120}`).join(' ');
    return `<svg class="distribution-chart" viewBox="0 0 560 205" role="img" aria-label="Open fraction at every distance from the border; zero to one hundred percent"><text x="48" y="15" fill="#aec0b5" font-size="11">Open area by exact distance from the border</text>${[0,.5,1].map(p=>`<path d="M48 ${164-p*120}H542" stroke="#35493e"/><text x="42" y="${168-p*120}" text-anchor="end" fill="#aec0b5" font-size="10">${p*100}%</text>`).join('')}<path d="M48 ${164-s.openFraction*120}H542" stroke="#edbc78" stroke-dasharray="4 4"/><polyline points="${points}" fill="none" stroke="#a5eac6" stroke-width="1.3"/><text x="48" y="186" fill="#aec0b5" font-size="11">Border (0)</text><text x="542" y="186" text-anchor="end" fill="#aec0b5" font-size="11">Center (${s.edgeLayers.length-1})</text></svg>`;
  }

  function render(board) {
    const s = board.stats, area = s.width*s.height;
    let html = group('Population and local connections', 'Orthogonal neighbors share an edge. Counts and fractions below use the whole board.', table(
      row('stats.width','Width',s.width,'Cells across') + row('stats.height','Height',s.height,'Cells down') +
      row('stats.openCells','Open cells',s.openCells,`${number(area-s.openCells)} wall cells; ${number(area)} cells in total`) +
      row('stats.openFraction','Open fraction',s.openFraction,'Open cells ÷ all cells',percent(s.openFraction)) +
      s.degreeCounts.map((n,d)=>row(`stats.degreeCounts[${d}]`,`Open cells with ${d} open neighbors`,n,`${percent(n/s.openCells)} of open cells; degree ${d}`)).join('') +
      row('stats.isolatedWallFraction','Isolated wall fraction',s.isolatedWallFraction,'Wall cells with no orthogonal wall neighbor ÷ wall cells',percent(s.isolatedWallFraction)) +
      row('stats.interfacePerOpen','Interface sides per open cell',s.interfacePerOpen,'Wall-facing and exterior-facing sides ÷ open cells') +
      row('stats.axisBias','Horizontal axis bias',s.axisBias,'(Horizontal open adjacencies − vertical) ÷ their sum; opposing local orientations can cancel')
    ));

    const lengths = [...new Set([...Object.keys(s.horizontalRuns.histogram),...Object.keys(s.verticalRuns.histogram)])].map(Number).sort((a,b)=>a-b);
    const histogramCell = (key,n) => Object.hasOwn(s[key].histogram,n) ? valueCell(`stats.${key}.histogram.${n}`,s[key].histogram[n]) : '<td class="value">0</td>';
    html += group('Open runs', 'A run is a maximal uninterrupted row or column of open cells. These are geometric runs, not solution moves. The two means can hide very different tails.',
      table(['horizontalRuns','verticalRuns'].map((key,i)=>{
        const label = i ? 'Vertical' : 'Horizontal';
        return row(`stats.${key}.count`,`${label} run count`,s[key].count,'Each maximal run counts once') + row(`stats.${key}.mean`,`${label} mean length`,s[key].mean,'Cells per run') + row(`stats.${key}.max`,`${label} longest run`,s[key].max,'Cells');
      }).join('')) + '<div class="chart-key"><span class="horizontal">Horizontal</span><span class="vertical">Vertical</span></div>' +
      distributionChart([{name:'Horizontal',values:s.horizontalRuns.histogram,color:'#f89d42'},{name:'Vertical',values:s.verticalRuns.histogram,color:'#48a0f4'}],'Run length (cells)') +
      `<details><summary>Every run length and exact count · ${lengths.length} observed lengths</summary><p>Absent lengths have zero runs. Counts partition the open cells separately in each orientation.</p><div class="distribution-table"><table><thead><tr><th>Length</th><th>Horizontal</th><th>Vertical</th></tr></thead><tbody>${lengths.map(n=>`<tr><th scope="row">${n}</th>${histogramCell('horizontalRuns',n)}${histogramCell('verticalRuns',n)}</tr>`).join('')}</tbody></table></div></details>`);

    const sizes = [...new Set([...Object.keys(s.openSquareCounts),...Object.keys(s.wallSquareCounts)])].map(Number).sort((a,b)=>a-b);
    html += group('Open and wall squares', 'Counts include every overlapping axis-aligned placement of each exact size. They are not disjoint rooms. A witness records one largest square as [x, y, side], with zero-based coordinates.',
      table(row('stats.largestOpenSquare','Largest open square',s.largestOpenSquare,'Top-left x, top-left y, side',`[${s.largestOpenSquare.join(', ')}]`) + row('stats.largestWallSquare','Largest wall square',s.largestWallSquare,'Top-left x, top-left y, side',`[${s.largestWallSquare.join(', ')}]`)) +
      '<div class="controls"><button data-focus-square="squares">Inspect largest open square</button><button data-focus-square="walls">Inspect largest wall square</button></div>' +
      '<div class="chart-key"><span class="horizontal">Open squares</span><span class="vertical">Wall squares</span></div>' + distributionChart([{name:'Open squares',values:s.openSquareCounts,color:'#f89d42'},{name:'Wall squares',values:s.wallSquareCounts,color:'#48a0f4'}],'Square side (cells)') +
      `<details><summary>Every square size and placement count · ${sizes.length} sizes</summary><div class="distribution-table"><table><thead><tr><th>Side</th><th>All open</th><th>All wall</th></tr></thead><tbody>${sizes.map(n=>`<tr><th scope="row">${n} × ${n}</th>${['openSquareCounts','wallSquareCounts'].map(key=>Object.hasOwn(s[key],n)?valueCell(`stats.${key}.${n}`,s[key][n]):'<td class="value">0</td>').join('')}</tr>`).join('')}</tbody></table></div></details>`);

    const t = s.tileDensity;
    html += group('Tile density', 'Nonoverlapping tiles summarize local open fractions. Partial edge tiles have the same weight as full tiles. Low variance can still hide very different patterns within each tile.', table(
      row('stats.tileDensity.side','Tile side',t.side,'Cells; partial edge tiles are smaller') +
      row('stats.tileDensity.columns','Tile columns',t.columns,'Tiles across') + row('stats.tileDensity.rows','Tile rows',t.rows,'Tiles down') +
      row('stats.tileDensity.min','Lowest tile occupancy',t.min,'Minimum tile open fraction',percent(t.min)) +
      row('stats.tileDensity.max','Highest tile occupancy',t.max,'Maximum tile open fraction',percent(t.max)) +
      row('stats.tileDensity.variance','Tile occupancy variance',t.variance,'Mean squared deviation of tile fractions from their unweighted mean; fractions use 0–1 units')
    ) + '<button data-overlay="density">Show tile density overlay</button>');

    const transforms = [['mirrorX','rawMirrorX','Left/right reflection'],['mirrorY','rawMirrorY','Top/bottom reflection'],['rotate180','rawRotate180','Half-turn']];
    html += group('Symmetry', 'Raw agreement is the fraction of matching cells. Adjusted agreement = (raw − chance) ÷ (1 − chance), with chance = p² + (1−p)². Zero means agreement at that density baseline; it does not rule out local structure.',
      `<p>Density baseline for this board: ${percent(s.openFraction**2+(1-s.openFraction)**2)}.</p><table><thead><tr><th>Transform</th><th>Adjusted</th><th>Raw</th></tr></thead><tbody>${transforms.map(([adjusted,raw,label])=>`<tr><th scope="row">${label}<code>${adjusted} / ${raw}</code></th>${valueCell('stats.symmetry.'+adjusted,s.symmetry[adjusted])}${valueCell('stats.symmetry.'+raw,s.symmetry[raw],percent(s.symmetry[raw]))}</tr>`).join('')}</tbody></table>` + '<button data-overlay="symmetry">Show left/right mismatches</button>');

    html += group('Every edge-distance layer', 'Each layer includes all cells at an exact distance from the nearest board border. Green is layer occupancy; the dashed amber line is whole-board occupancy. Small center layers contain fewer cells.',
      edgeChart(s) + `<details><summary>All ${s.edgeLayers.length} layers · exact values and inspect controls</summary><div class="distribution-table"><table><thead><tr><th>Distance</th><th>Open fraction</th><th>Overlay</th></tr></thead><tbody>${s.edgeLayers.map((layer,index)=>`<tr><th scope="row" data-stat-path="stats.edgeLayers[${index}].distance" data-value="${layer.distance}">${layer.distance}</th>${valueCell(`stats.edgeLayers[${index}].openFraction`,layer.openFraction,percent(layer.openFraction))}<td><button data-edge-layer="${layer.distance}">Inspect layer ${layer.distance}</button></td></tr>`).join('')}</tbody></table></div></details>`);

    const metadataRows = Object.entries(board.metadata).map(([key,value])=>{
      const formatted = typeof value === 'object' && value !== null ? `<details><summary>${esc(key)} · complete saved values</summary><pre>${esc(JSON.stringify(value,null,2))}</pre></details>` : esc(String(value));
      return `<tr><th scope="row">${esc(key)}</th><td data-stat-path="${esc(key)}" data-value="${esc(JSON.stringify(value))}">${formatted}</td></tr>`;
    }).join('');
    html += group('Scope, generation settings, and provenance', 'These fields identify the saved specimen. Generation time depends on the machine and load; it is not a board-geometry or difficulty measurement.',
      `<p data-stat-path="stats.scope" data-value="${esc(JSON.stringify(s.scope))}">${esc(s.scope)}</p><table class="identity-table"><tbody>${metadataRows}</tbody></table>`, true);
    return html;
  }
  return {render,number};
})();
