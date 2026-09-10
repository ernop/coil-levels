'use strict';
window.CoilGalleryBrowser = (() => {
  const esc=v=>String(v).replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
  const $=id=>document.getElementById(id);
  const sizeKey=b=>b.width===b.height?String(b.width):`${b.width}x${b.height}`;
  function compareBoards(a,b,sort='id',order='asc') {
    const value=board=>sort==='id'?board.width*board.height:sort==='name'?board.title:board.traits[sort];
    const av=value(a),bv=value(b);
    if(av==null&&bv!=null)return 1;
    if(bv==null&&av!=null)return -1;
    const difference=av==null?0:sort==='name'?av.localeCompare(bv,undefined,{numeric:true}):av-bv;
    return difference*(order==='desc'?-1:1)||a.width*a.height-b.width*b.height||a.id.localeCompare(b.id);
  }
  const matches=(b,f)=>(!f.hasSolution||b.solutionAvailable)&&(!f.source||f.source==='all'||b.source.id===f.source)&&(f.method==='all'||b.configuration.method===f.method)&&(f.config==='all'||b.configuration.id===f.config)&&(f.collection==='all'||b.collection===f.collection||(f.collection==='sampling'&&!['boards','survey','original-game','legacy-generator','saved-hard'].includes(b.collection)))&&(f.size==='all'||sizeKey(b)===f.size)&&`${b.id} ${b.title} ${b.recipe} ${b.source.label} ${b.configuration.label}`.toLowerCase().includes(f.search.toLowerCase());
  function init(data,metrics,onFilter,onSelect) {
    const configurations=new Map(data.boards.map(b=>[b.configuration.id,b.configuration]));
    const query=new URLSearchParams(location.search);
    let rows=[],selectedId='',shown=48;
    const observer=new IntersectionObserver(entries=>{if(entries.some(entry=>entry.isIntersecting))loadMore();},{root:$('board-scroll'),rootMargin:'240px 0px'});
    observer.observe($('more-boards'));
    let order=query.get('order')||(['id','name',null].includes(query.get('sort'))?'asc':'desc');
    if(!['asc','desc'].includes(order))order='asc';
    $('lab-sort').insertAdjacentHTML('beforeend',Object.entries(metrics).map(([key,m])=>`<option value="${key}">${esc(m.name)}</option>`).join(''));
    const sources=[...new Map(data.boards.map(b=>[b.source.id,b.source])).values()].sort((a,b)=>a.id==='original-game'?-1:b.id==='original-game'?1:0);
    $('lab-source').innerHTML='<option value="all">All sources</option>'+sources.map(s=>`<option value="${s.id}">${esc(s.label)} (${data.boards.filter(b=>b.source.id===s.id).length.toLocaleString()})</option>`).join('');
    $('lab-collection').innerHTML='<option value="all">All collections</option><option value="sampling">All sampling experiments</option>'+data.collections.map(c=>`<option value="${c.id}">${esc(c.label)}</option>`).join('');
    $('lab-size').innerHTML='<option value="all">All dimensions</option>'+[...new Map(data.boards.map(b=>[sizeKey(b),b])).values()].sort((a,b)=>a.width*a.height-b.width*b.height||a.width-b.width).map(b=>`<option value="${sizeKey(b)}">${b.width.toLocaleString()} × ${b.height.toLocaleString()}</option>`).join('');
    function methodOptions(preferred='all') {
      const available=data.boards.filter(b=>$('lab-source').value==='all'||b.source.id===$('lab-source').value);
      $('lab-method').innerHTML='<option value="all">All methods</option>'+[...new Map(available.map(b=>[b.configuration.method,b.configuration.methodLabel]))].map(([key,label])=>`<option value="${key}">${esc(label)}</option>`).join('');
      $('lab-method').value=[...$('lab-method').options].some(o=>o.value===preferred)?preferred:'all';
    }
    function configurationOptions(preferred='all') {
      const ids=new Set(data.boards.filter(b=>($('lab-source').value==='all'||b.source.id===$('lab-source').value)&&($('lab-method').value==='all'||b.configuration.method===$('lab-method').value)).map(b=>b.configuration.id));
      $('lab-config').innerHTML='<option value="all">All configurations</option>'+[...configurations.values()].filter(c=>ids.has(c.id)).sort((a,b)=>a.label.localeCompare(b.label)).map(c=>`<option value="${c.id}">${esc(c.label)}</option>`).join('');
      $('lab-config').value=ids.has(preferred)?preferred:'all';
    }
    for(const [key,id] of [['source','lab-source'],['collection','lab-collection'],['size','lab-size'],['sort','lab-sort']])if([...$(id).options].some(o=>o.value===query.get(key)))$(id).value=query.get(key);
    methodOptions(query.get('method')||'all');configurationOptions(query.get('config')||'all');$('lab-search').value=query.get('search')||'';
    $('lab-has-solution').checked=query.get('hasSolution')==='true';
    const state=()=>({source:$('lab-source').value,method:$('lab-method').value,config:$('lab-config').value,collection:$('lab-collection').value,size:$('lab-size').value,search:$('lab-search').value,hasSolution:$('lab-has-solution').checked,sort:$('lab-sort').value,order});
    function loadMore(){if(shown>=rows.length)return;shown+=48;renderCards(false);}
    function renderCards(reset=true) {
      const displayed=rows.slice(0,shown);
      const tabStop=displayed.some(b=>b.id===selectedId)?selectedId:displayed[0]?.id;
      const container=$('board-thumbnails');
      if(reset)container.replaceChildren();
      const existing=new Set([...container.querySelectorAll('[data-board]')].map(card=>card.dataset.board));
      container.insertAdjacentHTML('beforeend',displayed.filter(b=>!existing.has(b.id)).map(b=>`<button class="board-thumb" data-board="${esc(b.id)}" tabindex="${b.id===tabStop?0:-1}" aria-pressed="${b.id===selectedId}" title="${esc(b.title)}"><img src="${b.assets.preview}" loading="lazy" width="120" height="120" alt="${esc(b.title)}"><strong>${esc(b.title)}</strong><span>${b.width} × ${b.height}</span><small class="thumb-source ${b.source.id}">${esc(b.source.label)}</small></button>`).join(''));
      for(const card of container.querySelectorAll('[data-board]'))card.tabIndex=card.dataset.board===tabStop?0:-1;
      $('more-boards').hidden=rows.length<=shown;
      observer.unobserve($('more-boards'));observer.observe($('more-boards'));
    }
    function apply(preferred=selectedId) {
      const f=state();shown=48;$('board-scroll').scrollTop=0;
      rows=data.boards.filter(b=>matches(b,f)).sort((a,b)=>compareBoards(a,b,f.sort,order));
      const active=['method','config','collection','size'].filter(k=>f[k]!=='all').length;
      const orderNames=f.sort==='name'?['A → Z','Z → A']:f.sort==='id'?['Small first','Large first']:['Low first','High first'];
      $('sort-direction').textContent=(order==='asc'?'↑ ':'↓ ')+orderNames[order==='asc'?0:1];
      $('sort-direction').title=order==='asc'?'Ascending · click for descending':'Descending · click for ascending';
      $('sort-direction').setAttribute('aria-label',$('sort-direction').title);
      $('filter-summary').textContent=active?`${active} active`:'';
      $('clear-filters').hidden=!(active||f.search||f.hasSolution||f.source!=='all'||f.sort!=='id'||order!=='asc');
      $('board-count').textContent=`${rows.length.toLocaleString()} of ${data.boards.length.toLocaleString()} boards`;
      selectedId=rows.some(b=>b.id===preferred)?preferred:rows[0]?.id||'';
      renderCards();renderComparison();onFilter(rows,preferred);
    }
    function select(id) {
      const previous=selectedId;selectedId=id;
      // Keep the existing cards, order, keyboard focus, and chooser scroll position.
      const cards=[...$('board-thumbnails').querySelectorAll('[data-board]')];
      const tabStop=cards.some(card=>card.dataset.board===id)?id:cards[0]?.dataset.board;
      for(const card of cards) {
        card.tabIndex=card.dataset.board===tabStop?0:-1;
        if(card.dataset.board===previous||card.dataset.board===id)card.setAttribute('aria-pressed',String(card.dataset.board===id));
      }
      for(const point of $('compare-scatter').querySelectorAll('[data-board]')) {
        if(point.dataset.board!==previous&&point.dataset.board!==id)continue;
        const selected=point.dataset.board===id;
        point.setAttribute('r',selected?'7':'4');
        point.setAttribute('fill',selected?'#00e9ff':point.dataset.color);
      }
    }
    function reveal(id){const b=data.boards.find(b=>b.id===id);if(!b)throw new Error('Unknown board '+id);$('lab-source').value='all';methodOptions();configurationOptions();$('lab-size').value='all';$('lab-collection').value='all';$('lab-search').value='';$('lab-has-solution').checked=false;apply(id);}
    function renderComparison() {
      const xk=$('compare-x').value,yk=$('compare-y').value,svg=$('compare-scatter'),points=rows.filter(b=>b.traits[xk]!==null&&b.traits[yk]!==null);
      if(!points.length){svg.innerHTML='<text x="40" y="60" fill="white">No matching measurements.</text>';$('configuration-ranges').innerHTML='';$('compare-caption').textContent='';return;}
      let xmin=Math.min(...points.map(b=>b.traits[xk])),xmax=Math.max(...points.map(b=>b.traits[xk])),ymin=Math.min(...points.map(b=>b.traits[yk])),ymax=Math.max(...points.map(b=>b.traits[yk]));
      if(xmin===xmax){xmin-=.5;xmax+=.5;}if(ymin===ymax){ymin-=.5;ymax+=.5;}
      const x=v=>85+(v-xmin)/(xmax-xmin)*870,y=v=>290-(v-ymin)/(ymax-ymin)*255,n=window.CoilStatsLedger.number;
      let html='<title>Each point is one saved board. Select it to inspect that board.</title>';
      for(let i=0;i<=4;i++){const xv=xmin+(xmax-xmin)*i/4,yv=ymin+(ymax-ymin)*i/4;html+=`<path d="M85 ${y(yv)}H955" stroke="#35493e"/><text x="75" y="${y(yv)+4}" fill="#aec0b5" text-anchor="end" font-size="12">${n(yv)}</text><text x="${x(xv)}" y="313" fill="#aec0b5" text-anchor="middle" font-size="12">${n(xv)}</text>`;}
      html+=`<text x="500" y="345" text-anchor="middle" fill="white" font-size="14">${metrics[xk].name}</text><text x="85" y="18" fill="white" font-size="14">${metrics[yk].name}</text>`;
      for(const b of points)html+=`<circle tabindex="0" role="button" data-board="${esc(b.id)}" data-color="${b.source.id==='original-game'?'#edbc78':'#a5eac6'}" aria-label="Inspect ${esc(b.id)}" cx="${x(b.traits[xk])}" cy="${y(b.traits[yk])}" r="${b.id===selectedId?7:4}" fill="${b.id===selectedId?'#00e9ff':b.source.id==='original-game'?'#edbc78':'#a5eac6'}" opacity=".8"><title>${esc(b.source.label)} · ${esc(b.title)}: ${n(b.traits[xk])}, ${n(b.traits[yk])}</title></circle>`;
      svg.innerHTML=html;$('compare-caption').textContent=`${points.length.toLocaleString()} boards · gold: original game · green: coil-levels generator · cyan: selected. Coincident points can overlap.`;
      const groups=new Map();for(const b of points){if(!groups.has(b.configuration.id))groups.set(b.configuration.id,[]);groups.get(b.configuration.id).push(b);}
      $('configuration-ranges').innerHTML=`<h3>${metrics[yk].name} by configuration</h3><p>Observed ranges summarize the saved boards at the selected sizes; they are not confidence intervals.</p><table><thead><tr><th>Configuration</th><th>Boards</th><th>Minimum</th><th>Mean</th><th>Maximum</th></tr></thead><tbody>${[...groups].map(([id,bs])=>{const vs=bs.map(b=>b.traits[yk]);return `<tr><th><button data-config="${id}">${esc(configurations.get(id).label)}</button></th><td>${bs.length}</td><td>${n(Math.min(...vs))}</td><td>${n(vs.reduce((a,b)=>a+b,0)/vs.length)}</td><td>${n(Math.max(...vs))}</td></tr>`;}).join('')}</tbody></table>`;
    }
    for(const id of ['compare-x','compare-y']){$(id).innerHTML=Object.entries(metrics).map(([k,m])=>`<option value="${k}">${m.name}</option>`).join('');$(id).addEventListener('change',renderComparison);}
    $('compare-x').value='occupancy';$('compare-y').value='runs';
    $('lab-source').addEventListener('change',()=>{methodOptions();configurationOptions();$('lab-size').value='all';$('lab-collection').value='all';apply();});
    $('lab-method').addEventListener('change',()=>{configurationOptions();$('lab-collection').value='all';apply();});
    for(const id of ['lab-config','lab-collection','lab-size','lab-sort','lab-search','lab-has-solution'])$(id).addEventListener(id==='lab-search'?'input':'change',()=>apply());
    $('clear-filters').addEventListener('click',()=>{for(const id of ['lab-source','lab-size','lab-collection'])$(id).value='all';methodOptions();configurationOptions();$('lab-search').value='';$('lab-sort').value='id';$('lab-has-solution').checked=false;order='asc';apply();});
    $('sort-direction').addEventListener('click',()=>{order=order==='asc'?'desc':'asc';apply();});
    $('board-thumbnails').addEventListener('keydown',event=>{
      if(!['ArrowLeft','ArrowRight','ArrowUp','ArrowDown'].includes(event.key)||event.altKey||event.ctrlKey||event.metaKey)return;
      const target=event.target.closest('[data-board]');if(!target)return;
      event.preventDefault();
      const container=$('board-thumbnails'),style=getComputedStyle(container);
      const columns=style.display==='grid'?style.gridTemplateColumns.split(' ').length:1;
      let cards=[...container.querySelectorAll('[data-board]')];
      const delta={ArrowLeft:-1,ArrowRight:1,ArrowUp:-columns,ArrowDown:columns}[event.key];
      const next=cards.indexOf(target)+delta;if(next<0)return;
      if(next>=cards.length&&shown<rows.length){shown+=48;renderCards(false);cards=[...container.querySelectorAll('[data-board]')];}
      if(next>=cards.length)return;
      const card=cards[next];
      card.focus({preventScroll:true});card.scrollIntoView({block:'nearest',inline:'nearest'});
      onSelect(card.dataset.board);
    });
    for(const id of ['board-thumbnails','compare-scatter'])for(const eventName of ['click','keydown'])$(id).addEventListener(eventName,event=>{const target=event.target.closest('[data-board]');if(!target)return;if(eventName==='keydown'&&!['Enter',' '].includes(event.key))return;event.preventDefault();onSelect(target.dataset.board);});
    $('configuration-ranges').addEventListener('click',event=>{const button=event.target.closest('[data-config]');if(!button)return;methodOptions(configurations.get(button.dataset.config).method);configurationOptions(button.dataset.config);apply();});
    return {apply,select,reveal,state};
  }
  return {init,matches,sizeKey,compareBoards};
})();
