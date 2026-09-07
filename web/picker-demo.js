'use strict';
for(const [id,key] of [['tweakPicker','tweaks'],['segPicker','segments']]){const select=document.getElementById(id);for(const name of window.COIL_PICKERS[key])select.add(new Option(name,name));select.addEventListener('change',render);}
document.getElementById('tweakPicker').value='rnd99';document.getElementById('segPicker').value='Weighted4';
function render(){document.getElementById('picker-command').textContent=`dotnet run -c Release -- specimen output/example 500 101 --picker ${document.getElementById('tweakPicker').value} --segpicker ${document.getElementById('segPicker').value} --lim 20`;}
render();
