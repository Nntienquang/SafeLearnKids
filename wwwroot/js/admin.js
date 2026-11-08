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

if(P==='admin-questions'){
  var Q=[{t:'5 × 7 = ?',s:'Trắc nghiệm · 4 đáp án',src:'Tay',ok:1},{t:'Kết quả 5 × 6 tận cùng bằng chữ số nào?',s:'Trắc nghiệm',src:'AI',ok:0,a:['0','5','6','3'],c:0},{t:'5 × 9 = ?',s:'Trắc nghiệm',src:'Tay',ok:1},{t:'Số nào chia hết cho 5?',s:'Trắc nghiệm',src:'AI',ok:0,a:['12','25','33','48'],c:1}],sel=1,mode=0;
  var tabs=$('.tabs'),left=$$('.sl-card')[0],right=$$('.sl-card')[1];
  function draw(){var list=Q.map(function(x,i){return {x:x,i:i};}).filter(function(o){return mode===0?o.x.src==='Tay'||o.x.ok:mode===2?!o.x.ok:true;});
    tabs.children[2].textContent='Chờ duyệt AI ('+Q.filter(function(x){return !x.ok;}).length+')';
    $$('div[style*="margin-top:10px"]',left).forEach(function(x){x.remove();});
    left.insertAdjacentHTML('beforeend',list.map(function(o){var x=o.x;return '<button type="button" data-i="'+o.i+'" class="sl-row" style="all:unset;box-sizing:border-box;width:100%;display:flex;gap:14px;align-items:center;padding:14px;border-radius:16px;border:1px solid '+(o.i===sel?'var(--primary)':'var(--line)')+';margin-top:10px;cursor:pointer"><div class="grow"><div class="b">'+esc(x.t)+'</div><div class="muted" style="font-size:12.5px">'+x.s+'</div></div><span class="chip t-'+(x.src==='AI'?'v':'n')+'">'+x.src+'</span><span class="chip t-'+(x.ok?'g':'a')+'">'+(x.ok?'Đã duyệt':'Chờ duyệt')+'</span></button>';}).join('')||'<div class="empty" style="margin-top:10px">Không có câu hỏi</div>');
    var x=Q[sel];right.innerHTML=x&&x.a?'<div class="sl-row" style="gap:8px;margin-bottom:12px"><span class="chip t-v"><i class="ic ic-sparkles"></i>AI gợi ý</span><span class="muted" style="font-size:12.5px">Cần người duyệt trước khi dùng</span></div><div class="b" style="margin-bottom:10px">'+esc(x.t)+'</div>'+x.a.map(function(a,i){var c=i===x.c;return '<div class="sl-row" style="gap:10px;padding:10px 12px;border-radius:12px;border:1px solid '+(c?'var(--green)':'var(--line)')+';margin-top:8px;'+(c?'background:rgba(34,197,94,.1)':'')+'"><i class="ic ic-'+(c?'checkc':'chevr')+'" style="color:var(--'+(c?'green':'text-3')+')"></i><span class="b">'+esc(a)+'</span></div>';}).join('')+(x.ok?'<div class="banner t-g" style="margin-top:14px;font-weight:600"><i class="ic ic-checkc"></i>Đã duyệt</div>':'<div class="sl-row" style="gap:8px;margin-top:14px"><button class="sl-btn grow" type="button" id="rj">Từ chối</button><button class="sl-btn sl-btn-p grow" type="button" id="ap">Duyệt câu hỏi</button></div>'):'<div class="empty">Chọn một câu hỏi để xem chi tiết</div>';
    var ap=$('#ap'),rj=$('#rj');if(ap)ap.addEventListener('click',function(){x.ok=1;S.audit('quantri · Câu hỏi','Duyệt: '+x.t);UI.toast('Đã duyệt câu hỏi');draw();});if(rj)rj.addEventListener('click',function(){Q.splice(sel,1);sel=0;S.audit('quantri · Câu hỏi','Từ chối câu hỏi AI');UI.toast('Đã từ chối');draw();});}
  draw();tabs.addEventListener('kl:select',function(e){mode=Array.prototype.indexOf.call(tabs.children,e.detail);draw();});
  left.addEventListener('click',function(e){var b=e.target.closest('[data-i]');if(b){sel=+b.dataset.i;draw();}});
  UI.on(UI.byText('.head button','AI gợi ý câu hỏi'),function(){var b=this;b.disabled=true;b.innerHTML='<i class="ic ic-sparkles"></i>Đang tạo...';setTimeout(function(){Q.push({t:'5 × 4 = ?',s:'Trắc nghiệm',src:'AI',ok:0,a:['15','20','25','10'],c:1});b.disabled=false;b.innerHTML='<i class="ic ic-sparkles"></i>AI gợi ý câu hỏi';sel=Q.length-1;mode=2;Array.prototype.forEach.call(tabs.children,function(t,i){t.classList.toggle('on',i===2);});draw();UI.toast('AI đã gợi ý 1 câu hỏi, chờ bạn duyệt');},900);});
}

/* Forms marked data-confirm ask before posting. */
if(P==='admin-users'||P==='admin-catalog-sql'||P==='admin-lesson-sql'||P==='admin-curate-sql'||P==='admin-reports-sql'){
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
