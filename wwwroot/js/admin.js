/* SafeLearn Kids · admin area (Areas/Admin) */
(function(){
'use strict';
var S=KL,UI=window.UI,$=UI.$,$$=UI.$$,esc=S.esc;
function body(html){var m=$('.main'),h=$('.head',m);Array.prototype.slice.call(m.children).forEach(function(x){if(x!==h&&!x.hasAttribute('data-keep')&&h.compareDocumentPosition(x)&4)x.remove();});var d=document.createElement('div');d.innerHTML=html;m.appendChild(d);return d;}
function hashScore(n){var h=0;for(var i=0;i<n.length;i++)h=(h*31+n.charCodeAt(i))%1000;return 42+h%52;}
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

if(P==='admin-curate'){
  var sel=null,tb=$('.tbl tbody'),panel=$$('.sl-card')[1];
  var STc={ok:['Đã duyệt','g'],review:['Chờ duyệt','a'],no:['Từ chối','r']};
  function draw(){var ss=S.get().sources;if(!sel||!ss.some(function(x){return x.id===sel;}))sel=(ss.filter(function(x){return x.st==='review';})[0]||ss[0]||{}).id;
    tb.innerHTML=ss.map(function(x){var sc=hashScore(x.name),s=STc[x.st];return '<tr data-id="'+x.id+'" '+(x.id===sel?'class="sel"':'')+' style="cursor:pointer"><td><div class="b">'+esc(x.name)+'</div><div class="muted" style="font-size:12.5px">'+x.n+' video · '+esc(x.sub)+'</div></td><td><div class="sl-row" style="gap:10px"><div class="bar" style="width:90px"><i style="width:'+sc+'%;background:var(--'+(sc>70?'green':sc>55?'sun':'rose')+')"></i></div><span class="b">'+sc+'</span></div></td><td><span class="chip t-'+s[1]+'">'+s[0]+'</span></td><td style="text-align:right"><button type="button" class="sl-btn sl-btn-sm" data-v="'+x.id+'">Xem</button></td></tr>';}).join('');
    var x=S.get().sources.filter(function(z){return z.id===sel;})[0];if(!x){panel.innerHTML='<div class="empty">Không có kênh</div>';return;}var sc=hashScore(x.name);
    panel.innerHTML='<div class="sl-row" style="gap:8px;margin-bottom:10px"><span class="chip t-v"><i class="ic ic-sparkles"></i>Nhận xét AI</span></div><div class="b">'+esc(x.name)+' · '+sc+'/100</div><ul style="margin:10px 0 0;padding-left:18px;color:var(--text-2);line-height:1.8;font-size:14px"><li>Ngôn ngữ '+(sc>60?'phù hợp lớp 2–4':'cần xem lại độ tuổi')+'</li><li>'+(sc>70?'Không có quảng cáo trong video mẫu':'Phát hiện quảng cáo trong video mẫu')+'</li><li>'+(sc>80?'Không có video cần xem lại':'2 video cần xem lại')+'</li></ul><div class="banner t-a" style="font-size:13px;font-weight:600;margin-top:14px"><i class="ic ic-alert"></i>AI chỉ gợi ý. Quyết định cuối cùng thuộc về quản trị viên.</div>'+(x.st==='review'?'<div class="sl-row" style="gap:8px;margin-top:14px"><button class="sl-btn sl-btn-d grow" type="button" id="no">Từ chối</button><button class="sl-btn sl-btn-p grow" type="button" id="yes">Duyệt kênh</button></div>':'<div class="chip t-'+STc[x.st][1]+'" style="margin-top:14px">'+STc[x.st][0]+'</div>');
    var y=$('#yes'),n=$('#no');
    if(y)y.addEventListener('click',function(){S.set(function(s){s.sources.filter(function(z){return z.id===x.id;})[0].st='ok';});S.audit('quantri · Kênh '+x.sub,'Duyệt kênh '+x.name);S.notify({ic:'checkc',tone:'b',t:'Nguồn mới được duyệt',s:x.name});UI.toast('Đã duyệt kênh');draw();});
    if(n)n.addEventListener('click',function(){S.set(function(s){s.sources.filter(function(z){return z.id===x.id;})[0].st='no';});S.audit('quantri · Kênh '+x.sub,'Từ chối kênh '+x.name);UI.toast('Đã từ chối kênh');draw();});}
  tb.addEventListener('click',function(e){var r=e.target.closest('tr[data-id]');if(r){sel=r.dataset.id;draw();}});
  UI.on(UI.byText('.head button','Chấm lại'),function(){UI.toast('AI đã chấm lại '+S.get().sources.length+' kênh');draw();});
  draw();
}

if(P==='admin-reports'){
  var sel=null,tb=$('.tbl tbody'),panel=$$('.sl-card')[1],seg=$('.head .seg'),mode=0;
  function draw(){var rs=S.get().reports.filter(function(r){return mode===0?r.status==='open':r.status!=='open';});
    seg.children[0].textContent='Chờ xử lý ('+S.get().reports.filter(function(r){return r.status==='open';}).length+')';
    if(!sel||!rs.some(function(r){return r.id===sel;}))sel=rs[0]&&rs[0].id;
    tb.innerHTML=rs.map(function(r){var l=S.lesson(r.lesson);return '<tr data-id="'+r.id+'" '+(r.id===sel?'class="sel"':'')+' style="cursor:pointer"><td><div class="b">'+esc(l.title)+'</div><div class="muted" style="font-size:12.5px">'+esc(r.reason)+'</div></td><td class="muted">'+esc(r.by)+'</td><td><span class="chip t-'+(r.sev==='Khẩn'?'r':'a')+'">'+(r.status==='open'?r.sev:({removed:'Đã gỡ',kept:'Đã giữ',dismissed:'Bỏ qua'})[r.status]||r.status)+'</span></td><td style="text-align:right"><button type="button" class="sl-btn sl-btn-sm" data-v="'+r.id+'">Mở</button></td></tr>';}).join('')||'<tr><td colspan="4"><div class="empty">Không có báo cáo</div></td></tr>';
    var r=S.get().reports.filter(function(z){return z.id===sel;})[0];
    if(!r){panel.innerHTML='<div class="empty"><div class="b" style="color:var(--text-2)">Đã xử lý hết báo cáo</div></div>';return;}var l=S.lesson(r.lesson);
    panel.innerHTML='<div class="sl-row" style="gap:12px"><div style="width:84px;height:56px;border-radius:14px;background:linear-gradient(135deg,'+l.c[0]+','+l.c[1]+');display:flex;align-items:center;justify-content:center;color:#fff"><i class="ic ic-play"></i></div><div><div class="b">'+esc(l.title)+'</div><span class="chip t-'+(r.sev==='Khẩn'?'r':'a')+'">'+r.sev+'</span></div></div><p class="sub" style="margin-top:12px;line-height:1.6">"'+esc(r.reason)+'" · '+esc(r.by)+'</p>'+(r.status==='open'?'<div class="field" style="margin-top:10px"><label class="lab" for="ar1">Ghi chú xử lý</label><textarea id="ar1" class="inp ta">Đã kiểm tra nội dung.</textarea></div><div class="sl-row" style="gap:8px;margin-top:14px"><button class="sl-btn grow" type="button" id="keep">Giữ nội dung</button><button class="sl-btn sl-btn-d grow" type="button" id="rm">Gỡ nội dung</button></div>':'<div class="chip t-n" style="margin-top:10px">Đã xử lý</div>');
    var k=$('#keep'),m=$('#rm');
    if(k)k.addEventListener('click',function(){S.set(function(s){s.reports.filter(function(z){return z.id===r.id;})[0].status='kept';});S.audit('quantri · Báo cáo '+r.id,'Giữ nội dung '+l.title);UI.toast('Đã giữ nội dung');draw();});
    if(m)m.addEventListener('click',function(){UI.confirm({title:'Gỡ "'+l.title+'"?',text:'Bài sẽ biến mất khỏi lộ trình của mọi bé.',ok:'Gỡ nội dung',danger:true,icon:'trash'}).then(function(ok){if(!ok)return;S.set(function(s){s.reports.filter(function(z){return z.id===r.id;})[0].status='removed';Object.keys(s.assigned).forEach(function(c){s.assigned[c]=s.assigned[c].filter(function(x){return x!==l.id;});});});S.audit('quantri · Báo cáo '+r.id,'Gỡ nội dung '+l.title);S.notify({ic:'checkc',tone:'g',t:'Báo cáo đã được xử lý',s:l.title+' đã được gỡ'});UI.toast('Đã gỡ nội dung');draw();});});}
  tb.addEventListener('click',function(e){var r=e.target.closest('tr[data-id]');if(r){sel=r.dataset.id;draw();}});
  seg.addEventListener('kl:select',function(e){mode=Array.prototype.indexOf.call(seg.children,e.detail);sel=null;draw();});
  draw();document.addEventListener('kl:change',function(){var a=document.activeElement;if(!a||a.tagName!=='TEXTAREA')draw();});
}

// Any admin form with data-confirm asks first: users, subjects and lessons all rely on it.
document.addEventListener('submit',function(e){var f=e.target;if(!f.dataset.confirm||f.dataset.confirmed)return;e.preventDefault();
  UI.confirm({title:f.dataset.confirm,text:f.dataset.text,ok:f.dataset.okLabel,danger:f.dataset.danger==='true',icon:f.dataset.icon||'lock'}).then(function(ok){if(!ok)return;f.dataset.confirmed='1';f.submit();});});

if(P==='admin-audit'){
  var mode=0,seg=$('.seg');
  function draw(){var card=$('.sl-card');Array.prototype.slice.call(card.children).slice(1).forEach(function(x){x.remove();});
    card.insertAdjacentHTML('beforeend',S.get().audit.slice(0,mode?50:20).map(function(a){return '<div class="sl-row" style="gap:14px;padding:14px 0;border-top:1px solid var(--line)"><span class="ib t-b"><i class="ic ic-history"></i></span><div class="grow"><div class="b">'+esc(a.a)+'</div><div class="muted" style="font-size:12.5px">'+esc(a.t)+'</div></div><span class="muted" style="font-size:12.5px">'+esc(a.w)+'</span></div>';}).join(''));}
  draw();seg.addEventListener('kl:select',function(e){mode=Array.prototype.indexOf.call(seg.children,e.detail);draw();});
  UI.on(UI.byText('.head button','Lọc'),function(){UI.toast('Bộ lọc nâng cao sẽ có ở sprint 9');});
}
});
})();
