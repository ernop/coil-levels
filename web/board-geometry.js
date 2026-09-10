'use strict';
window.CoilBoardGeometry = (() => {
  function inspectLarge(board) {
    const width=board.width||board.side, height=board.height||board.side, tileSide=board.stats.tileDensity.side, columns=Math.ceil(width/tileSide);
    const bytes=Uint8Array.from(atob(board.wallsBase64),c=>c.charCodeAt(0));
    if(bytes.length!==Math.ceil(width*height/8))throw new Error('Incorrect board data length');
    const wallAt=i=>(bytes[i>>3]>>(i&7))&1;
    const horizontal=new Map(), vertical=new Map(), tiles=new Map();
    function runAt(x,y,isVertical) {
      const cache=isVertical?vertical:horizontal, key=isVertical?x:y, length=isVertical?height:width;
      if(!cache.has(key)) {
        if(cache.size>=768)cache.delete(cache.keys().next().value);
        const row=new Uint16Array(length),start=isVertical?x:y*width,step=isVertical?width:1;
        let p=0;
        while(p<length){if(wallAt(start+p*step)){p++;continue;}let end=p+1;while(end<length&&!wallAt(start+end*step))end++;row.fill(end-p,p,end);p=end;}
        cache.set(key,row);
      }
      return cache.get(key)[isVertical?y:x];
    }
    function tileAt(x,y) {
      const tx=Math.floor(x/tileSide),ty=Math.floor(y/tileSide),key=ty*columns+tx;
      if(!tiles.has(key)) {
        const right=Math.min(width,(tx+1)*tileSide),bottom=Math.min(height,(ty+1)*tileSide);
        let open=0;
        for(let yy=ty*tileSide;yy<bottom;yy++)for(let xx=tx*tileSide;xx<right;xx++)open+=!wallAt(yy*width+xx);
        tiles.set(key,open/((right-tx*tileSide)*(bottom-ty*tileSide)));
      }
      return tiles.get(key);
    }
    const degreeAt=(x,y)=>(x>0&&!wallAt(y*width+x-1))+(x<width-1&&!wallAt(y*width+x+1))+(y>0&&!wallAt((y-1)*width+x))+(y<height-1&&!wallAt((y+1)*width+x));
    let finish=null;
    function getFinish() {
      if(!board.solution)return null;
      if(finish)return finish;
      finish=replay(board,width,height,wallAt).finish;return finish;
    }
    return {wallAt,degreeAt,runAt,tileAt,tileSide,columns,width,height,start:board.solution?{x:Number(board.solution.x),y:Number(board.solution.y)}:null,finish,getFinish};
  }
  function replay(board,width,height,wallAt,visit=null,sample=null) {
    const solution=board.solution;
    if(!solution)return {start:null,finish:null};
    const seen=new Uint8Array(Math.ceil(width*height/8));
    const has=i=>(seen[i>>3]>>(i&7))&1;
    let x=Number(solution.x),y=Number(solution.y),count=1;
    if(!Number.isInteger(x)||!Number.isInteger(y)||x<0||x>=width||y<0||y>=height||wallAt(y*width+x))throw new Error('Invalid solution start');
    const start={x,y};let at=y*width+x;seen[at>>3]|=1<<(at&7);if(visit)visit[at]=count;
    if(sample)sample.points.push({x,y,visit:count});
    for(const direction of solution.path) {
      const delta={U:[0,-1],R:[1,0],D:[0,1],L:[-1,0]}[direction];if(!delta)throw new Error('Invalid solution direction');
      const [dx,dy]=delta;let moved=false;
      while(x+dx>=0&&x+dx<width&&y+dy>=0&&y+dy<height) {
        at=(y+dy)*width+x+dx;if(wallAt(at)||has(at))break;
        x+=dx;y+=dy;seen[at>>3]|=1<<(at&7);count++;if(visit)visit[at]=count;moved=true;
        if(sample&&(count-1)%sample.stride===0)sample.points.push({x,y,visit:count});
      }
      if(!moved)throw new Error('Saved solution contains an illegal zero-length slide');
    }
    if(count!==board.stats.openCells)throw new Error('Saved solution does not cover every open cell');
    if(sample&&sample.points.at(-1).visit!==count)sample.points.push({x,y,visit:count});
    return {start,finish:{x,y}};
  }
  function decode(board) {
    const width=board.width||board.side,height=board.height||board.side,area=width*height,tileSide=board.stats.tileDensity.side;
    const columns=Math.ceil(width/tileSide),tileCount=columns*Math.ceil(height/tileSide);
    const bytes=Uint8Array.from(atob(board.wallsBase64),c=>c.charCodeAt(0));
    if(bytes.length!==Math.ceil(area/8))throw new Error('Incorrect board data length');
    const wall=new Uint8Array(area),degree=new Uint8Array(area),horizontal=new Uint16Array(area),vertical=new Uint16Array(area);
    const tiles=new Float64Array(tileCount),totals=new Uint32Array(tileCount),visit=board.solution?new Uint32Array(area):null;
    let open=0;
    for(let i=0;i<area;i++){wall[i]=(bytes[i>>3]>>(i&7))&1;open+=!wall[i];}
    if(open!==board.stats.openCells)throw new Error('Cell data does not match measured board');
    for(let y=0;y<height;y++)for(let x=0;x<width;x++) {
      const i=y*width+x,tile=Math.floor(y/tileSide)*columns+Math.floor(x/tileSide);
      tiles[tile]+=!wall[i];totals[tile]++;
      degree[i]=(x>0&&!wall[i-1])+(x<width-1&&!wall[i+1])+(y>0&&!wall[i-width])+(y<height-1&&!wall[i+width]);
    }
    for(const [lines,length,step,out] of [[height,width,1,horizontal],[width,height,width,vertical]])for(let a=0;a<lines;a++) {
      const start=step===1?a*width:a;let r=0;
      while(r<length){if(wall[start+r*step]){r++;continue;}let end=r+1;while(end<length&&!wall[start+end*step])end++;for(let k=r;k<end;k++)out[start+k*step]=end-r;r=end;}
    }
    for(let t=0;t<tileCount;t++)tiles[t]/=totals[t];
    return {wall,degree,horizontal,vertical,tiles,columns,tileSide,visit,width,height,...replay(board,width,height,i=>wall[i],visit)};
  }
  function sampleSolution(board,stride,geometry) {
    if(!Number.isInteger(stride)||stride<1)throw new Error('Sample interval must be a positive integer');
    if(!board.solution)return [];
    const g=geometry||(Math.max(board.width,board.height)>1000?inspectLarge(board):decode(board));
    const points=[],count=board.stats.openCells;
    if(g.visit) {
      for(let i=0;i<g.visit.length;i++) {
        const visit=g.visit[i];
        if(visit&&((visit-1)%stride===0||visit===count))points[Math.ceil((visit-1)/stride)]={x:i%g.width,y:Math.floor(i/g.width),visit};
      }
    } else replay(board,g.width,g.height,g.wallAt,null,{stride,points});
    return points;
  }
  return {inspectLarge,decode,sampleSolution};
})();
