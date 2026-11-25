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

/* Forms marked data-confirm: handled for every page in ui.js. */

/* A background sync is running: reload until it is done (the server clears the mark). Stops after ~3 minutes. */
var refresh=$('[data-auto-refresh]');
if(refresh){var tries=+(sessionStorage.getItem('sl-refresh')||0);
  try{if(tries<45){setTimeout(function(){try{sessionStorage.setItem('sl-refresh',String(tries+1));}catch(e){}location.reload();},+refresh.dataset.autoRefresh||5000);}}catch(e){}}
else{try{sessionStorage.removeItem('sl-refresh');}catch(e){}}

/* Question form: ask AI for a child-friendly draft of "explain it another way". It only fills the box; the admin saves. */
var draftBtn=$('[data-kid-draft]');
if(draftBtn){
  var box=$('#KidExplanation'),by=$('#KidExplanationBy'),note=$('[data-kid-draft-note]'),form=draftBtn.closest('form');
  draftBtn.addEventListener('click',function(){
    if(box.value.trim()&&!confirm('Thay nội dung đang có trong ô bằng bản AI soạn?'))return;
    var data=new FormData();data.append('__RequestVerificationToken',$('input[name=__RequestVerificationToken]',form).value);
    draftBtn.disabled=true;note.textContent='Đang soạn…';
    fetch(draftBtn.dataset.kidDraft,{method:'POST',body:data,credentials:'same-origin'}).then(function(r){return r.json();}).then(function(d){
      if(d.error||!d.text){note.textContent=d.error||'Chưa soạn được, hãy thử lại hoặc tự viết.';return;}
      box.value=d.text;by.value=d.model;note.textContent='Bản nháp của '+d.model+'. Hãy đọc, sửa nếu cần rồi bấm Lưu câu hỏi.';box.focus();
    }).catch(function(){note.textContent='Chưa soạn được, hãy thử lại hoặc tự viết.';}).then(function(){draftBtn.disabled=false;});
  });
  box.addEventListener('input',function(){if(!box.value.trim())by.value='';});
}

/* Video queue: tick several videos, approve or block them at once. The server checks the reason for blocking. */
var bulk=$('[data-bulk]');
if(bulk){
  var picks=$$('[data-bulk-pick]'),all=$('[data-bulk-all]',bulk),count=$('[data-bulk-count]',bulk);
  var paint=function(){var n=picks.filter(function(p){return p.checked;}).length;count.textContent=n+' video đã chọn';all.checked=n>0&&n===picks.length;all.indeterminate=n>0&&n<picks.length;};
  all.addEventListener('change',function(){picks.forEach(function(p){p.checked=all.checked;});paint();});
  picks.forEach(function(p){p.addEventListener('change',paint);});
  bulk.addEventListener('submit',function(e){
    if(!picks.some(function(p){return p.checked;})){e.preventDefault();UI.toast('Hãy tick chọn ít nhất một video.','err');return;}
    var by=e.submitter,note=$('[name=note]',bulk);
    if(by&&by.value==='Blocked'&&!note.value.trim()){e.preventDefault();note.focus();UI.toast('Hãy ghi lý do chặn.','err');}
  });
  paint();
}

/* Lesson editor: show only the fields of the chosen content kind. */
if(P==='admin-lesson-sql'){
  var kind=$('[data-content-kind]');
  function showKind(){$$('[data-for]').forEach(function(f){var on=f.dataset.for.split(' ').indexOf(kind.value)>-1;f.hidden=!on;$$('input,select,textarea',f).forEach(function(i){i.disabled=!on;});});}
  if(kind){kind.addEventListener('change',showKind);showKind();}
}

});
})();
