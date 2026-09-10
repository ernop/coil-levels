'use strict';
// Keep bookmarked boards, filters and crop positions when retiring the three old entry points.
const destination=new URL('gallery.html',location.href);
destination.search=location.search;
const oldPage=location.pathname.split('/').at(-1);
if(oldPage==='board-wall.html'&&!destination.searchParams.has('collection'))destination.searchParams.set('collection','boards');
if(oldPage==='board-wall.html'&&!destination.searchParams.has('size'))destination.searchParams.set('size','500');
if(destination.searchParams.has('phase')&&!destination.searchParams.has('collection'))destination.searchParams.set('collection',destination.searchParams.get('phase'));
const sections={'#variation':'compare','#explore':'compare','#guide':'research','#methods':'research','#development':'research','#sampling-research':'research','#all-stats':'all-stats'};
if(sections[location.hash])destination.searchParams.set('panel',sections[location.hash]);
destination.hash=location.hash||'#viewer';
document.getElementById('new-gallery-link').href=destination.href;
location.replace(destination.href);
