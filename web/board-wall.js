'use strict';
const boards = window.COIL_CATALOG.boards.filter(board => board.phase === 'boards');
const control = id => document.getElementById(id);
const escapeText = value => String(value).replace(/[&<>"']/g, c => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
const query = new URLSearchParams(location.search);
if ([...control('wall-size').options].some(option => option.value === query.get('size'))) control('wall-size').value = query.get('size');
function renderWall() {
  const size = control('wall-size').value;
  const large = board => board.stats.largestOpenSquare[2] / board.side >= .25;
  const detail = control('wall-view').value === 'detail';
  const search = control('wall-search').value.trim().toLowerCase();
  const rows = boards.filter(board => (size === 'all' || board.side === Number(size)) && `${board.recipe.id} seed ${board.seed}`.toLowerCase().includes(search))
    .sort((a,b) => a.side - b.side || a.recipe.id.localeCompare(b.recipe.id) || a.seed - b.seed);
  control('wall-count').textContent = `${rows.length} boards shown · ${size === 'all' ? 'all sizes' : `${Number(size).toLocaleString()} × ${Number(size).toLocaleString()}`} · ${boards.length} saved boards in this folder`;
  control('wall-cards').classList.toggle('detail', detail);
  control('wall-cards').innerHTML = rows.map(board => {
    const label = `${board.side}×${board.side} · ${board.recipe.id} · seed ${board.seed}`;
    const source = board.recipe.family === 'initial sample' ? 'Initial sample' : 'Measured scale selection';
    return `<article><a href="${board.path}/map.png?v=${board.boardSha256}" target="_blank" rel="noopener" aria-label="Open full map: ${escapeText(label)}"><img src="${board.path}/${detail ? 'detail' : 'preview'}.png?v=${board.boardSha256}-crop2" width="480" height="480" alt="${escapeText(label)} — ${detail ? 'central detail' : 'whole board'}"></a><h2>${escapeText(board.recipe.id)}</h2><p>Seed ${board.seed} · ${board.side.toLocaleString()} × ${board.side.toLocaleString()}</p><p>Open ${(board.stats.openFraction*100).toFixed(2)}% · mean run ${((board.stats.horizontalRuns.mean+board.stats.verticalRuns.mean)/2).toFixed(2)}</p><p class="source">${source}</p>${large(board) ? `<p class="open-region">Large open square: ${board.stats.largestOpenSquare[2]} × ${board.stats.largestOpenSquare[2]}</p>` : ''}<div class="links"><a href="${board.path}/map.png?v=${board.boardSha256}" target="_blank" rel="noopener">Full map</a><a href="stats-lab.html?left=${board.id}">Explain stats visually</a><a href="${board.path}/stats.json?v=${board.boardSha256}">JSON</a><a href="${board.path}/level.board.gz?v=${board.boardSha256}" download>Board</a><a href="${board.path}/level.solution.gz?v=${board.boardSha256}" download>Solution</a></div></article>`;
  }).join('');
}
for (const id of ['wall-size', 'wall-view', 'wall-search']) control(id).addEventListener(id === 'wall-search' ? 'input' : 'change', renderWall);
renderWall();
