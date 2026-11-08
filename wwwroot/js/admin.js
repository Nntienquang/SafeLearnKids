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

if(P==='admin-home'){
  var open=st.reports.filter(function(r){return r.status==='open';}).length,rev=st.sources.filter(function(x){return x.st==='review';}).length;
  var k=$$('.kpi');k[3].querySelector('.kpi-v').textContent=open;k[2].querySelector('.kpi-v').textContent=rev;k[1].querySelector('.kpi-v').textContent=S.LESSONS.length-Object.keys(DRAFT).length;
  var card=$$('.sl-card').filter(function(x){return /Việc cần làm/.test(x.textContent);})[0];
  var rows=$$('.sl-row[style*="border-top"]',card);
  rows[0].querySelector('.b').textContent=open+' báo cáo nội dung';rows[0].querySelector('.muted').textContent=open?'Có '+st.reports.filter(function(r){return r.status==='open'&&r.sev==='Khẩn';}).length+' mức khẩn':'Không còn báo cáo';
  rows[1].querySelector('.b').textContent=rev+' kênh chờ duyệt';
}

/* Question form: true/false has two fixed answers, other types up to four. */
if(P==='admin-questions-sql'){
  var ty=$('#q-type');
  if(ty&&!ty.disabled)ty.addEventListener('change',function(){var tf=ty.value==='TrueFalse',on=tf?'truefalse':'choice';
    [['choice',!tf],['truefalse',tf]].forEach(function(p){var box=$('[data-options="'+p[0]+'"]');box.hidden=!p[1];$$('input',box).forEach(function(i){i.disabled=!p[1];});});
    if(!$('[data-options="'+on+'"] input[type=radio]:checked')){var first=$('[data-options="'+on+'"] input[type=radio]');if(first)first.checked=true;}});
}

/* Forms marked data-confirm ask before posting. */
if(P==='admin-users'||P==='admin-catalog-sql'||P==='admin-lesson-sql'||P==='admin-curate-sql'||P==='admin-reports-sql'||P==='admin-questions-sql'){
  document.addEventListener('submit',function(e){var f=e.target;if(!f.dataset.confirm||f.dataset.confirmed)return;e.preventDefault();
    UI.confirm({title:f.dataset.confirm,text:f.dataset.text,ok:f.dataset.okLabel,danger:f.dataset.danger==='true',icon:'lock'}).then(function(ok){if(!ok)return;f.dataset.confirmed='1';f.submit();});});
}

/* Lesson editor: show only the fields of the chosen content kind. */
if(P==='admin-lesson-sql'){
  var kind=$('[data-content-kind]');
  function showKind(){$$('[data-for]').forEach(function(f){var on=f.dataset.for.split(' ').indexOf(kind.value)>-1;f.hidden=!on;$$('input,select,textarea',f).forEach(function(i){i.disabled=!on;});});}
  if(kind){kind.addEventListener('change',showKind);showKind();}
}

if(P==='admin-audit'){
  var mode=0,seg=$('.seg');
  function draw(){var card=$('.sl-card');Array.prototype.slice.call(card.children).slice(1).forEach(function(x){x.remove();});
    card.insertAdjacentHTML('beforeend',S.get().audit.slice(0,mode?50:20).map(function(a){return '<div class="sl-row" style="gap:14px;padding:14px 0;border-top:1px solid var(--line)"><span class="ib t-b"><i class="ic ic-history"></i></span><div class="grow"><div class="b">'+esc(a.a)+'</div><div class="muted" style="font-size:12.5px">'+esc(a.t)+'</div></div><span class="muted" style="font-size:12.5px">'+esc(a.w)+'</span></div>';}).join(''));}
  draw();seg.addEventListener('kl:select',function(e){mode=Array.prototype.indexOf.call(seg.children,e.detail);draw();});
  UI.on(UI.byText('.head button','Lọc'),function(){UI.toast('Bộ lọc nâng cao sẽ có ở sprint 9');});
}
});
})();
