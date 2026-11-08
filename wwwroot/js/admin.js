/* SafeLearn Kids · admin area (Areas/Admin) */
(function(){
'use strict';
var S=KL,UI=window.UI,$=UI.$,$$=UI.$$,esc=S.esc;
function body(html){var m=$('.main'),h=$('.head',m);Array.prototype.slice.call(m.children).forEach(function(x){if(x!==h&&!x.hasAttribute('data-keep')&&h.compareDocumentPosition(x)&4)x.remove();});var d=document.createElement('div');d.innerHTML=html;m.appendChild(d);return d;}
function hashScore(n){var h=0;for(var i=0;i<n.length;i++)h=(h*31+n.charCodeAt(i))%1000;return 42+h%52;}
var DRAFT={hinhhoc:1,tadovat:1};
function kind(k){return k==='Video'?'playc':k==='Quiz'?'target':'book';}
document.addEventListener('kl:ui',function(){
var P=UI.PAGE,st=S.get();


/* Question form: true/false has two fixed answers, other types up to four. */
if(P==='admin-questions-sql'){
  var ty=$('#q-type');
  if(ty&&!ty.disabled)ty.addEventListener('change',function(){var tf=ty.value==='TrueFalse',on=tf?'truefalse':'choice';
    [['choice',!tf],['truefalse',tf]].forEach(function(p){var box=$('[data-options="'+p[0]+'"]');box.hidden=!p[1];$$('input',box).forEach(function(i){i.disabled=!p[1];});});
    if(!$('[data-options="'+on+'"] input[type=radio]:checked')){var first=$('[data-options="'+on+'"] input[type=radio]');if(first)first.checked=true;}});
}

/* Forms marked data-confirm ask before posting. */
if(P==='admin-users'||P==='admin-catalog-sql'||P==='admin-lesson-sql'||P==='admin-curate-sql'||P==='admin-reports-sql'||P==='admin-questions-sql'||P==='admin-paths-sql'||P==='admin-data-sql'){
  document.addEventListener('submit',function(e){var f=e.target,sub=e.submitter;if(!f.dataset.confirm||f.dataset.confirmed)return;e.preventDefault();
    UI.confirm({title:f.dataset.confirm,text:f.dataset.text,ok:f.dataset.okLabel,danger:f.dataset.danger==='true',icon:'lock'}).then(function(ok){if(!ok)return;f.dataset.confirmed='1';/* requestSubmit keeps the clicked button's name and value (for example decision=approve). */if(sub&&f.requestSubmit)f.requestSubmit(sub);else f.submit();});});
}

/* Lesson editor: show only the fields of the chosen content kind. */
if(P==='admin-lesson-sql'){
  var kind=$('[data-content-kind]');
  function showKind(){$$('[data-for]').forEach(function(f){var on=f.dataset.for.split(' ').indexOf(kind.value)>-1;f.hidden=!on;$$('input,select,textarea',f).forEach(function(i){i.disabled=!on;});});}
  if(kind){kind.addEventListener('change',showKind);showKind();}
}

});
})();
