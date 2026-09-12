'use strict';
const viewer = new URL('web/gallery.html', location.href);
viewer.search = location.search;
viewer.hash = location.hash;
location.replace(viewer.href);
