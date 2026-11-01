/* SafeLearn Kids · parent area (Areas/Parent) */
(function(){
'use strict';
var S=KL,UI=window.UI,$=UI.$,$$=UI.$$,esc=S.esc;
var DAYS=['T2','T3','T4','T5','T6','T7','CN'];
function ch(){return S.child(S.get().parentChild);}
function body(html){var m=$('.main'),h=$('.head',m);Array.prototype.slice.call(m.children).forEach(function(x){if(x!==h&&!x.hasAttribute('data-keep')&&h.compareDocumentPosition(x)&4)x.remove();});var d=document.createElement('div');d.innerHTML=html;m.appendChild(d);return d;}
function setHead(h1,sub){var a=$('.head .sl-h1');if(a&&h1!=null)a.textContent=h1;var b=$('.head .sub');if(b&&sub!=null)b.textContent=sub;}
function thumb(c,ic,w,h){return '<div style="width:'+w+'px;height:'+h+'px;flex:0 0 auto;border-radius:16px;background:linear-gradient(135deg,'+c[0]+','+c[1]+');display:flex;align-items:center;justify-content:center;color:#fff"><i class="ic ic-'+ic+'"></i></div>';}
function kind(k){return k==='Video'?'play':k==='Quiz'?'pen':'book';}
function lessonState(c,l){var p=S.prog(c.id,l.id);return p.done?['Đã học','g']:(S.get().assigned[c.id]||[]).indexOf(l.id)>-1?['Đã giao','b']:['Chưa giao','n'];}
function subjDone(c,sj){var ls=S.LESSONS.filter(function(l){return l.subj===sj;});var d=ls.filter(function(l){return S.prog(c.id,l.id).done;}).length;return {d:d,t:ls.length,p:Math.round(d*100/ls.length)};}
function doneCount(c){return S.LESSONS.filter(function(l){return S.prog(c.id,l.id).done;}).length;}
document.addEventListener('kl:child',function(){location.reload();});

document.addEventListener('kl:ui',function(){
var P=UI.PAGE,st=S.get(),c=ch();

/* ---------------- onboarding ---------------- */
if(P==='onboarding'){
  var n=$('#ob-n'),y=$('#ob-y'),cls=3,PAL=[['#6A4CFF','#FF7AB6'],['#22C55E','#38BDF8'],['#F59E0B','#FB7185'],['#14B8A6','#6A4CFF']];
  $$('.seg span').forEach(function(s){if(s.classList.contains('on'))cls=+s.textContent;});
  $('.seg').addEventListener('kl:select',function(e){cls=+e.detail.textContent;});
  var nx=$('#ob-next');
  nx.addEventListener('click',function(e){e.preventDefault();
    if(!UI.field(n,n.value.trim().length<1?'Nhập tên hoặc biệt danh của bé.':n.value.trim().length>30?'Tên tối đa 30 ký tự.':''))return n.focus();
    if(y.value&&!UI.field(y,!/^(19|20)\d\d$/.test(y.value)||+y.value>2026||+y.value<2005?'Năm sinh không hợp lệ.':''))return y.focus();
    if(st.children.length>=4){UI.toast('Tối đa 4 hồ sơ','err');return;}
    var id='c'+Date.now(),nm=n.value.trim();
    S.set(function(s){s.children.push({id:id,name:nm,cls:cls,init:nm.charAt(0).toUpperCase(),c:PAL[s.children.length%4],limit:60,used:0,extra:0,subjects:['toan','tiengviet','khoahoc','tienganh'],search:true});s.assigned[id]=[];s.progress[id]={};s.stars[id]=0;s.streak[id]=0;s.badges[id]=[];s.parentChild=id;});
    UI.toast('Đã tạo hồ sơ cho '+nm);setTimeout(function(){location.href='/parent-home';},400);});
  if(!st.children.length){$$('a[href="/register"]').forEach(function(a){a.classList.add('hidden');});}
}

/* ---------------- home ---------------- */
if(P==='parent-home'){
  var last=(st.session.name||'').split(' ').pop();
  setHead('Chào chị '+last+', hôm nay '+c.name+' học thế nào?');
  var k=$$('.kpi');
  k[0].querySelector('.kpi-v').innerHTML=c.used+'<small style="font-size:15px;font-weight:700;color:var(--text-3)"> phút</small>';
  k[0].querySelector('.muted').textContent='Hôm nay · '+c.name;k[0].querySelector('.kpi-d').textContent='Còn '+S.remaining(c)+' / '+(c.limit+c.extra)+' phút';k[0].querySelector('.bar i').style.width=Math.min(100,c.used*100/(c.limit+c.extra))+'%';
  var d=doneCount(c),asg=(st.assigned[c.id]||[]).length;k[1].querySelector('.kpi-v').textContent=d+' / '+(d+asg);k[1].querySelector('.bar i').style.width=(d+asg?d*100/(d+asg):0)+'%';
  var open=st.reports.filter(function(r){return r.status==='open';}).length;k[3].querySelector('.kpi-v').textContent=open;k[3].querySelector('.kpi-d').textContent=open?'Đang chờ quản trị xử lý':'Không có báo cáo';
  // attention list
  var card=$$('.sl-card').filter(function(x){return /Cần ba mẹ xem/.test(x.textContent);})[0];
  if(card){var items=[];S.pendingReqs().forEach(function(r){items.push(['Xin thêm '+r.mins+' phút',S.child(r.cid).name+' · '+(r.note||'không ghi chú'),'a','clock','/time','Duyệt',1]);});
    st.notifs.filter(function(x){return x.unread;}).slice(0,3-items.length).forEach(function(x){items.push([x.t,x.s,x.tone,x.ic,'/notifications','Xem',0]);});
    card.querySelectorAll('.sl-row[style*="border-top"]').forEach(function(r){r.remove();});
    card.insertAdjacentHTML('beforeend',items.length?items.map(function(i){return '<div class="sl-row" style="gap:14px;padding:14px 0;border-top:1px solid var(--line)"><span class="ib t-'+i[2]+'"><i class="ic ic-'+i[3]+'"></i></span><div class="grow"><div class="b">'+esc(i[0])+'</div><div class="muted" style="font-size:13px">'+esc(i[1])+'</div></div><a class="sl-btn sl-btn-sm '+(i[6]?'sl-btn-p':'')+'" href="'+i[4]+'">'+i[5]+'</a></div>';}).join(''):'<div class="empty" style="margin-top:10px"><div class="b">Không có gì cần xử lý</div></div>');}
  // subject progress
  var sp=$$('.sl-card').filter(function(x){return /Tiến độ theo môn/.test(x.textContent);})[0];
  if(sp){$$('.sl-row',sp).forEach(function(r,i){var sj=['toan','tiengviet','khoahoc','tienganh'][i];if(!sj)return;var v=subjDone(c,sj);r.querySelector('.bar i').style.width=v.p+'%';r.querySelector('.b:last-child').textContent=v.p+'%';});}
  var cur=$$('.sl-card').filter(function(x){return /Đang học dở/.test(x.textContent);})[0];
  if(cur){var l=S.lesson('nhan5');}
}

/* ---------------- profiles ---------------- */
if(P==='profiles'){
  var sel=c.id;
  function render(){
    var cs=S.get().children,cur=S.child(sel);
    var cards=cs.map(function(x){var pct=Math.min(100,Math.round(x.used*100/(x.limit+x.extra)));
      return '<div class="sl-card" data-id="'+x.id+'" style="flex:1 1 320px;min-width:0;'+(x.id===sel?'outline:2px solid var(--primary);':'')+'display:flex;flex-direction:column;gap:16px"><div class="sl-row" style="gap:14px"><span class="av av-l" style="background:linear-gradient(135deg,'+x.c[0]+','+x.c[1]+')">'+esc(x.init)+'</span><div class="grow"><div class="card-t" style="font-size:18px">'+esc(x.name)+'</div><div class="muted b" style="font-size:13px">Lớp '+x.cls+'</div></div></div><div><div class="sl-row" style="justify-content:space-between;font-size:13px;margin-bottom:6px"><span class="muted b">Hôm nay</span><span class="b">'+x.used+' / '+(x.limit+x.extra)+' phút</span></div><div class="bar bar-l"><i style="width:'+pct+'%"></i></div></div><div class="wrap" style="gap:8px">'+x.subjects.map(function(s){return '<span class="chip t-'+S.SUBJECTS[s].tone+'">'+S.SUBJECTS[s].name+'</span>';}).join('')+'</div><div class="sl-row" style="gap:8px"><button type="button" class="sl-btn sl-btn-sm grow" data-edit="'+x.id+'"><i class="ic ic-edit"></i>Sửa</button><a class="sl-btn sl-btn-sm grow" href="/time" data-pc="'+x.id+'"><i class="ic ic-clock"></i>Giờ học</a><a class="sl-btn sl-btn-sm sl-btn-p grow" href="/kids-picker">Vào học</a></div></div>';}).join('');
    var add=cs.length<4?'<a class="empty" href="/onboarding" style="flex:1 1 320px;display:flex;flex-direction:column;align-items:center;justify-content:center;gap:12px"><span class="ib t-n" style="width:56px;height:56px"><i class="ic ic-plus"></i></span><div class="b" style="color:var(--text-2)">Thêm bé thứ '+(cs.length+1)+'</div><div style="font-size:13px">Mỗi tài khoản có tối đa 4 hồ sơ.</div></a>':'';
    var chips=Object.keys(S.SUBJECTS).map(function(k){var on=cur.subjects.indexOf(k)>-1;return '<button type="button" class="chip '+(on?'t-'+S.SUBJECTS[k].tone:'t-n')+'" data-sj="'+k+'" aria-pressed="'+on+'" style="border:0;cursor:pointer">'+(on?'<i class="ic ic-check"></i>':'')+S.SUBJECTS[k].name+'</button>';}).join('');
    body('<div style="display:flex;flex-wrap:wrap;gap:16px">'+cards+add+'</div><form id="pf" novalidate class="sl-card" style="margin-top:16px"><div class="card-h"><div><div class="card-t">Sửa hồ sơ · '+esc(cur.name)+'</div><div class="card-s">Thay đổi có hiệu lực ngay ở lần vào học tiếp theo.</div></div></div><div style="display:grid;grid-template-columns:repeat(auto-fit,minmax(240px,1fr));gap:16px;margin-top:8px"><div class="field"><label class="lab" for="pf1">Tên hiển thị</label><input id="pf1" class="inp" value="'+esc(cur.name)+'" maxlength="30"></div><div class="field"><span class="lab">Lớp</span><div class="seg" id="pf-cls">'+[1,2,3,4,5].map(function(i){return '<span class="'+(i===cur.cls?'on':'')+'">'+i+'</span>';}).join('')+'</div></div><div class="field"><span class="lab">Môn được học</span><div class="wrap" style="gap:8px" id="pf-sj">'+chips+'</div></div><div class="field"><span class="lab">Cho phép tìm kiếm</span><div class="sl-row" style="gap:10px"><span class="switch'+(cur.search?' on':'')+'" id="pf-search"></span><span class="muted" style="font-size:13px">Chỉ tìm trong bài đã giao</span></div></div></div><div class="sl-row" style="justify-content:space-between;margin-top:20px"><button class="sl-btn sl-btn-d sl-btn-sm" type="button" id="pf-del"><i class="ic ic-trash"></i>Xóa hồ sơ và dữ liệu</button><div class="sl-row" style="gap:10px"><button class="sl-btn sl-btn-p" type="submit">Lưu thay đổi</button></div></div></form>');
    $$('.switch',$('.main')).forEach(function(s){s.setAttribute('role','switch');s.tabIndex=0;s.setAttribute('aria-checked',s.classList.contains('on'));s.setAttribute('aria-label','Cho phép tìm kiếm');});
    $$('[data-edit]').forEach(function(b){b.addEventListener('click',function(e){e.__h=true;sel=b.dataset.edit;S.set(function(s){s.parentChild=sel;});render();$('#pf').scrollIntoView({behavior:'smooth'});});});
    $$('[data-pc]').forEach(function(a){a.addEventListener('click',function(){S.set(function(s){s.parentChild=a.dataset.pc;});});});
    $$('[data-sj]').forEach(function(b){b.addEventListener('click',function(){var k=b.dataset.sj;S.set(function(s){var x=S.child(sel),i=s.children.filter(function(z){return z.id===sel;})[0];var j=i.subjects.indexOf(k);if(j>-1){if(i.subjects.length<2){UI.toast('Cần ít nhất 1 môn','err');return;}i.subjects.splice(j,1);}else i.subjects.push(k);});render();});});
    $('#pf-del').addEventListener('click',function(e){e.__h=true;if(S.get().children.length<2){UI.toast('Cần giữ ít nhất 1 hồ sơ','err');return;}
      UI.confirm({title:'Xóa hồ sơ '+cur.name+'?',text:'Toàn bộ lịch sử học và điểm quiz sẽ bị xóa vĩnh viễn.',ok:'Xóa hồ sơ',danger:true,icon:'trash'}).then(function(ok){if(!ok)return;S.set(function(s){s.children=s.children.filter(function(z){return z.id!==cur.id;});delete s.assigned[cur.id];delete s.progress[cur.id];s.parentChild=s.children[0].id;if(s.kidsChild===cur.id)s.kidsChild=null;});sel=S.get().parentChild;UI.toast('Đã xóa hồ sơ');render();});});
    $('#pf').addEventListener('submit',function(e){e.preventDefault();var n=$('#pf1');if(!UI.field(n,!n.value.trim()?'Nhập tên của bé.':''))return n.focus();
      var cl=+$('#pf-cls .on').textContent,se=$('#pf-search').classList.contains('on');
      S.set(function(s){var i=s.children.filter(function(z){return z.id===sel;})[0];i.name=n.value.trim();i.init=i.name.charAt(0).toUpperCase();i.cls=cl;i.search=se;});UI.toast('Đã lưu hồ sơ');render();});
  }
  render();
}

/* ---------------- pin ---------------- */
if(P==='pin'){
  var exit=new URLSearchParams(location.search).has('exit'),buf='';
  var dots=$$('.sl-row[style*="gap:14px"] > span[style*="width:18px"]'),err=$('.err'),keys=$$('.icbtn').filter(function(k){return /^\d$/.test(k.textContent.trim())||k.querySelector('.ic-arrowl');});
  var prev=$('.sl-card[style*="surface-2"]');
  function paint(){dots.forEach(function(d,i){d.style.background=i<buf.length?'var(--primary)':'var(--surface-2)';});}
  function fail(){var lk=S.get().lock.pin||{fails:0,until:0};return lk;}
  function submit(){
    var lk=fail();if(lk.until>Date.now()){err.textContent='Đã khóa. Thử lại sau '+Math.ceil((lk.until-Date.now())/60000)+' phút.';err.classList.remove('hidden');buf='';paint();return;}
    if(buf===S.get().pin){S.set(function(s){s.lock.pin={fails:0,until:0};});err.classList.add('hidden');UI.toast('PIN đúng');if(exit){S.set(function(s){s.kidsChild=null;});setTimeout(function(){location.href='/parent-home';},350);}buf='';paint();}
    else{S.set(function(s){var l=s.lock.pin=s.lock.pin||{fails:0,until:0};l.fails++;if(l.fails>=5){l.until=Date.now()+5*60000;l.fails=0;}});var left=5-(S.get().lock.pin.fails||0);err.textContent=S.get().lock.pin.until>Date.now()?'Sai 5 lần. PIN bị khóa 5 phút.':'Sai PIN. Còn '+left+' lần thử.';err.classList.remove('hidden');buf='';paint();}
  }
  err.classList.add('hidden');err.setAttribute('role','alert');
  keys.forEach(function(k){k.classList.add('pin-key');k.setAttribute('role','button');k.tabIndex=0;k.setAttribute('aria-label',k.querySelector('.ic-arrowl')?'Xóa':'Số '+k.textContent.trim());k.addEventListener('click',function(){if(k.querySelector('.ic-arrowl'))buf=buf.slice(0,-1);else if(buf.length<4)buf+=k.textContent.trim();paint();if(buf.length===4)setTimeout(submit,120);});});
  document.addEventListener('keydown',function(e){if(e.target.closest&&e.target.closest('input'))return;if(/^\d$/.test(e.key)&&buf.length<4){buf+=e.key;paint();if(buf.length===4)setTimeout(submit,120);}else if(e.key==='Backspace'){buf=buf.slice(0,-1);paint();}});
  paint();
  if(exit){var h=$('.head .sl-h1');h.textContent='Nhập PIN để thoát chế độ trẻ';setHead(null,'Chỉ ba mẹ biết mã PIN này.');}
  // change PIN
  var cur=$('input[aria-label="PIN hiện tại"]'),nw=$('input[aria-label="PIN mới"]');cur.value='';nw.value='';cur.classList.remove('f');nw.classList.remove('f');[cur,nw].forEach(function(i){i.maxLength=4;i.inputMode='numeric';i.placeholder='••••';i.classList.add('inp');});
  UI.byText('button','Cập nhật').addEventListener('click',function(e){e.__h=true;
    var ok=UI.field(cur,cur.value!==S.get().pin?'PIN hiện tại chưa đúng.':'');ok=UI.field(nw,!/^\d{4}$/.test(nw.value)?'PIN mới gồm đúng 4 chữ số.':'')&&ok;if(!ok)return;
    S.set(function(s){s.pin=nw.value;});cur.value=nw.value='';UI.toast('Đã đổi mã PIN');});
  var chk=$$('.chk');chk.forEach(function(x){x.addEventListener('change',function(){UI.toast('Đã lưu');});});
  var kidsSw=$('.switch');if(kidsSw)kidsSw.addEventListener('kl:switch',function(){UI.toast(kidsSw.classList.contains('on')?'Đã bật chế độ trẻ':'Đã tắt chế độ trẻ');});
}

/* ---------------- library ---------------- */
if(P==='library'){
  var f={subj:'all',cls:3,q:new URLSearchParams(location.search).get('q')||'',only:false,page:1},PER=8;
  var act=$('.head .sl-row');act.innerHTML='<button type="button" class="sl-btn" id="only"><i class="ic ic-checkc"></i>Chỉ bài đã giao</button>';
  body('<div class="sl-row" style="gap:12px;flex-wrap:wrap;margin-bottom:18px"><div class="seg" id="fs"><span class="on" data-v="all">Tất cả</span><span data-v="toan">Toán</span><span data-v="tiengviet">Tiếng Việt</span><span data-v="khoahoc">Khoa học</span><span data-v="tienganh">Tiếng Anh</span></div><div class="seg" id="fc"><span data-v="2">Lớp 2</span><span class="on" data-v="3">Lớp 3</span><span data-v="4">Lớp 4</span></div><span class="grow"></span><span class="muted b" id="cnt"></span></div><div id="grid" style="display:grid;grid-template-columns:repeat(auto-fill,minmax(250px,1fr));gap:16px"></div><div class="sl-row" id="pg" style="justify-content:center;gap:8px;margin-top:22px"></div>');
  function draw(){
    var c=ch();var ls=S.LESSONS.filter(function(l){return (f.subj==='all'||l.subj===f.subj)&&l.cls===f.cls&&(!f.q||UI.vn(l.title).indexOf(UI.vn(f.q))>-1||UI.vn(S.SUBJECTS[l.subj].name).indexOf(UI.vn(f.q))>-1)&&(!f.only||(S.get().assigned[c.id]||[]).indexOf(l.id)>-1);});
    var pages=Math.max(1,Math.ceil(ls.length/PER));f.page=Math.min(f.page,pages);
    $('#cnt').textContent=ls.length+' bài · giao cho '+c.name;
    $('#grid').innerHTML=ls.slice((f.page-1)*PER,f.page*PER).map(function(l){var sj=S.SUBJECTS[l.subj],s=lessonState(c,l),asg=(S.get().assigned[c.id]||[]).indexOf(l.id)>-1;
      return '<div class="sl-card" style="padding:0;overflow:hidden;display:flex;flex-direction:column"><a href="/lesson-detail?id='+l.id+'" aria-label="'+esc(l.title)+'" style="height:112px;background:linear-gradient(135deg,'+l.c[0]+','+l.c[1]+');display:flex;align-items:center;justify-content:center;color:#fff"><i class="ic ic-'+kind(l.kind)+' ic-xl"></i></a><div style="padding:16px;display:flex;flex-direction:column;gap:10px;flex:1"><div class="sl-row" style="gap:6px"><span class="chip t-'+sj.tone+'">'+sj.name+'</span><span class="chip t-n">'+l.kind+'</span><span class="chip t-'+s[1]+'" style="margin-left:auto">'+s[0]+'</span></div><a class="b" href="/lesson-detail?id='+l.id+'" style="font-size:16px;line-height:1.3">'+esc(l.title)+'</a><div class="muted" style="font-size:12.5px">'+l.mins+' phút · Lớp '+l.cls+'</div><button type="button" class="sl-btn sl-btn-sm '+(asg?'':'sl-btn-p')+'" style="margin-top:auto" data-a="'+l.id+'">'+(asg?'Bỏ giao':'Giao cho '+esc(c.name))+'</button></div></div>';}).join('')||'<div class="empty" style="grid-column:1/-1"><div class="b" style="color:var(--text-2)">Không tìm thấy bài phù hợp</div><div style="font-size:13px;margin-top:4px">Thử đổi môn, lớp hoặc từ khóa.</div></div>';
    $('#pg').innerHTML=pages>1?Array.apply(null,Array(pages)).map(function(_,i){return '<button type="button" class="sl-btn sl-btn-sm '+(i+1===f.page?'sl-btn-p':'')+'" data-p="'+(i+1)+'" aria-label="Trang '+(i+1)+'">'+(i+1)+'</button>';}).join(''):'';
  }
  $('#fs').addEventListener('kl:select',function(e){f.subj=e.detail.dataset.v;f.page=1;draw();});
  $('#fc').addEventListener('kl:select',function(e){f.cls=+e.detail.dataset.v;f.page=1;draw();});
  document.addEventListener('kl:q',function(e){f.q=e.detail;f.page=1;draw();});
  $('#only').addEventListener('click',function(e){e.__h=true;f.only=!f.only;this.classList.toggle('sl-btn-p',f.only);f.page=1;draw();});
  $('#grid').addEventListener('click',function(e){var b=e.target.closest('[data-a]');if(!b)return;var c=ch(),id=b.dataset.a,has=(S.get().assigned[c.id]||[]).indexOf(id)>-1;if(has){S.unassign(c.id,id);UI.toast('Đã bỏ giao');}else{S.assign(c.id,id);UI.toast('Đã giao "'+S.lesson(id).title+'" cho '+c.name);}draw();});
  $('#pg').addEventListener('click',function(e){var b=e.target.closest('[data-p]');if(b){f.page=+b.dataset.p;draw();window.scrollTo({top:0,behavior:'smooth'});}});
  draw();
}

/* ---------------- lesson detail ---------------- */
if(P==='lesson-detail'){
  var id=new URLSearchParams(location.search).get('id')||'nhan5',l=S.lesson(id)||S.lesson('nhan5'),sj=S.SUBJECTS[l.subj];
  setHead(l.title,sj.name+' · Lớp '+l.cls+' · '+l.mins+' phút');
  $('.crumb').innerHTML='<a href="/library">Thư viện bài học</a> <i class="ic ic-chevr"></i> '+sj.name+' <i class="ic ic-chevr"></i> '+esc(l.title);
  var hero=$('div[style*="height:300px"]');hero.style.background='linear-gradient(135deg,'+l.c[0]+','+l.c[1]+')';
  var give=UI.byText('.head button','Giao cho'),rm=UI.byText('.head button','Gỡ khỏi');
  var box=UI.byText('.sl-card','Giao cho');var list=box.querySelectorAll('.sl-row');
  var sel={};S.get().children.forEach(function(x){sel[x.id]=x.id===ch().id;});
  function drawKids(){var h='<div class="card-t" style="margin-bottom:12px">Giao cho</div>'+S.get().children.map(function(x){var on=sel[x.id],asg=(S.get().assigned[x.id]||[]).indexOf(l.id)>-1;return '<button type="button" data-k="'+x.id+'" class="sl-row" aria-pressed="'+on+'" style="all:unset;box-sizing:border-box;width:100%;display:flex;align-items:center;gap:12px;padding:10px;border-radius:14px;cursor:pointer;'+(on?'background:var(--primary-soft)':'')+'"><span class="av av-s" style="background:linear-gradient(135deg,'+x.c[0]+','+x.c[1]+')">'+esc(x.init)+'</span><span class="b grow">'+esc(x.name)+' · Lớp '+x.cls+'</span>'+(asg?'<span class="chip t-g">Đã giao</span>':x.cls!==l.cls?'<span class="chip t-n">Khác lớp</span>':'')+(on?'<i class="ic ic-checkc" style="color:var(--primary)"></i>':'')+'</button>';}).join('')+'<div class="field" style="margin-top:14px"><label class="lab" for="ld-d">Hạn hoàn thành</label><input id="ld-d" type="date" class="inp" value="2026-10-25"></div>';box.innerHTML=h;}
  drawKids();box.addEventListener('click',function(e){var b=e.target.closest('[data-k]');if(!b)return;sel[b.dataset.k]=!sel[b.dataset.k];drawKids();});
  UI.on(give,function(){var ids=Object.keys(sel).filter(function(k){return sel[k];});if(!ids.length)return UI.toast('Chọn ít nhất 1 bé','err');ids.forEach(function(k){S.assign(k,l.id);});UI.toast('Đã giao cho '+ids.map(function(k){return S.child(k).name;}).join(', '));drawKids();});
  UI.on(rm,function(){var ids=Object.keys(sel).filter(function(k){return sel[k];});ids.forEach(function(k){S.unassign(k,l.id);});UI.toast('Đã gỡ khỏi lộ trình');drawKids();});
  var rep=UI.byText('a.sl-btn-d','Báo cáo nội dung');if(rep)rep.addEventListener('click',function(e){e.preventDefault();e.__h=true;
    UI.modal({title:'Báo cáo nội dung',icon:'flag',ok:'Gửi báo cáo',html:'<label class="lab" for="rr">Lý do</label><textarea id="rr" class="inp ta" placeholder="Ví dụ: có quảng cáo lạ, âm thanh quá lớn...">Có quảng cáo lạ</textarea>',value:function(o){return o.querySelector('#rr').value.trim()||'Không rõ';}}).then(function(v){if(!v)return;S.set(function(s){s.reports.unshift({id:'r'+Date.now(),lesson:l.id,reason:v,by:'Phụ huynh · '+s.session.name.split(' ').slice(-2).join(' '),sev:'Vừa',status:'open',at:new Date().toISOString()});});UI.toast('Đã gửi báo cáo cho quản trị');});});
  $$('.chip.t-g').forEach(function(x){});
}

/* ---------------- sources ---------------- */
if(P==='sources'){
  var tb=$('.tbl tbody'),inpbox=$('.search.grow'),add=UI.byText('button','Kiểm tra và thêm'),sync=UI.byText('button','Đồng bộ ngay');
  inpbox.innerHTML='<i class="ic ic-link"></i><input id="src-url" type="url" aria-label="Liên kết kênh hoặc playlist" placeholder="Dán liên kết kênh hoặc danh sách phát YouTube"><span class="err hidden" style="position:absolute"></span>';inpbox.style.cssText+=';position:relative';inpbox.classList.add('field');
  var STc={ok:['Đã duyệt','g'],review:['Đang kiểm duyệt','a'],no:['Bị từ chối','r']};
  function draw(){tb.innerHTML=S.get().sources.map(function(x){var s=STc[x.st],t=x.type==='Kênh'?'p':'g';return '<tr><td><div class="sl-row" style="gap:12px"><span class="av" style="background:linear-gradient(135deg,#6A4CFF,#38BDF8);border-radius:14px">'+esc(x.name.charAt(0))+'</span><div><div class="b">'+esc(x.name)+'</div><div class="muted" style="font-size:12.5px">'+esc(x.sub)+'</div></div></div></td><td><span class="chip t-'+t+'">'+x.type+'</span></td><td class="b">'+x.n+'</td><td><span class="chip t-'+s[1]+'">'+s[0]+'</span></td><td style="text-align:right"><button type="button" class="icbtn" aria-label="Xóa nguồn '+esc(x.name)+'" data-del="'+x.id+'" style="width:36px;height:36px;display:inline-flex"><i class="ic ic-trash"></i></button></td></tr>';}).join('')||'<tr><td colspan="5"><div class="empty">Chưa có nguồn nào. Dán liên kết để thêm.</div></td></tr>';}
  draw();
  UI.on(add,function(){var i=$('#src-url'),v=i.value.trim();var m=/^https?:\/\/(www\.)?(youtube\.com\/(@[\w.-]+|channel\/[\w-]+|playlist\?list=[\w-]+|c\/[\w-]+)|youtu\.be\/[\w-]+)/i.test(v);
    if(!v)return UI.toast('Dán liên kết YouTube trước','err');if(!m){inpbox.style.outline='2px solid var(--rose)';return UI.toast('Liên kết chưa đúng: dùng kênh (@tên) hoặc danh sách phát','err');}inpbox.style.outline='';
    var pl=/playlist/.test(v),nm=(v.match(/@([\w.-]+)/)||[0,pl?'Danh sách phát mới':'Kênh mới'])[1];
    if(S.get().sources.some(function(x){return x.sub.indexOf(nm)>-1;}))return UI.toast('Nguồn này đã có trong danh sách','err');
    S.set(function(s){s.sources.unshift({id:'s'+Date.now(),name:nm.replace(/[._-]/g,' '),sub:pl?'Danh sách phát':'@'+nm,type:pl?'Playlist':'Kênh',n:Math.floor(20+Math.random()*80),st:'review'});});i.value='';draw();UI.toast('Đã gửi quản trị kiểm duyệt');});
  tb.addEventListener('click',function(e){var b=e.target.closest('[data-del]');if(!b)return;var x=S.get().sources.filter(function(z){return z.id===b.dataset.del;})[0];UI.confirm({title:'Xóa nguồn "'+x.name+'"?',text:'Các bài từ nguồn này sẽ ẩn khỏi thư viện.',ok:'Xóa nguồn',danger:true,icon:'trash'}).then(function(ok){if(ok){S.set(function(s){s.sources=s.sources.filter(function(z){return z.id!==x.id;});});draw();UI.toast('Đã xóa nguồn');}});});
  UI.on(sync,function(){var t=this;t.disabled=true;t.innerHTML='<i class="ic ic-refresh"></i>Đang đồng bộ...';setTimeout(function(){t.disabled=false;t.innerHTML='<i class="ic ic-refresh"></i>Đồng bộ ngay';var d=new Date();$('.muted[style*="margin-top:12px"]').textContent='Đồng bộ lần cuối: '+('0'+d.getHours()).slice(-2)+':'+('0'+d.getMinutes()).slice(-2)+' hôm nay · Hạn mức API còn 91%';UI.toast('Đã đồng bộ '+S.get().sources.filter(function(z){return z.st==='ok';}).length+' nguồn');},1200);});
}

/* ---------------- time ---------------- */
if(P==='time'){
  var sched=c.sched||[45,45,60,45,45,90,90],win=c.win||['17:00','20:30'];
  var root=$('.main');
  function req(){return S.pendingReqs().filter(function(r){return r.cid===c.id;});}
  function draw(){c=ch();var r=req()[0],pct=Math.min(100,Math.round(c.used*100/(c.limit+c.extra))),rem=S.remaining(c);
    body('<div style="display:flex;flex-wrap:wrap;gap:16px;align-items:flex-start"><div class="sl-card" style="flex:2 1 460px;min-width:0"><div class="card-h"><div><div class="card-t">Giới hạn theo ngày · '+esc(c.name)+'</div><div class="card-s">Máy chủ tự dừng bài học khi hết giờ.</div></div></div>'+
    sched.map(function(m,i){return '<div class="sl-row" style="gap:12px;padding:8px 12px;border-radius:14px;'+(i===6?'background:var(--primary-soft)':'')+'"><span class="b" style="width:36px">'+DAYS[i]+'</span><div class="bar grow"><i style="width:'+Math.round(m*100/120)+'%"></i></div><button type="button" class="icbtn" style="width:34px;height:34px" data-d="'+i+'" data-s="-15" aria-label="Giảm 15 phút '+DAYS[i]+'"><i class="ic ic-x" style="transform:scale(.7)"></i></button><span class="b" style="width:64px;text-align:center">'+m+' phút</span><button type="button" class="icbtn" style="width:34px;height:34px" data-d="'+i+'" data-s="15" aria-label="Tăng 15 phút '+DAYS[i]+'"><i class="ic ic-plus"></i></button></div>';}).join('')+
    '<div class="field" style="margin-top:16px"><span class="lab">Khung giờ được học</span><div class="sl-row" style="gap:10px"><input class="inp" id="w1" type="time" value="'+win[0]+'" style="width:140px"><span class="muted">đến</span><input class="inp" id="w2" type="time" value="'+win[1]+'" style="width:140px"></div></div></div>'+
    '<div class="sl-col" style="flex:1 1 320px;min-width:0;gap:16px">'+(r?'<div class="sl-card" style="border:1px solid var(--sun)"><div class="sl-row" style="gap:12px"><span class="ib t-a"><i class="ic ic-clock"></i></span><div class="grow"><div class="b">'+esc(c.name)+' xin thêm '+r.mins+' phút</div><div class="muted" style="font-size:12.5px">'+esc(r.time)+' · "'+esc(r.note||'Con muốn học thêm')+'"</div></div></div><div class="sl-row" style="gap:8px;margin-top:14px"><button class="sl-btn grow" type="button" id="no">Từ chối</button><button class="sl-btn sl-btn-p grow" type="button" id="yes">Cho thêm '+r.mins+' phút</button></div></div>':'<div class="sl-card"><div class="sl-row" style="gap:12px"><span class="ib t-g"><i class="ic ic-checkc"></i></span><div><div class="b">Không có yêu cầu thêm giờ</div><div class="muted" style="font-size:12.5px">Khi bé xin, yêu cầu sẽ hiện ở đây.</div></div></div></div>')+
    '<div class="sl-card"><div class="card-t" style="margin-bottom:12px">Hôm nay</div><div class="sl-row" style="gap:18px"><div class="donut" style="background:conic-gradient(var(--primary) 0 '+pct+'%,var(--surface-2) '+pct+'% 100%)"><b>'+c.used+'<small>phút đã học</small></b></div><div class="sl-col" style="gap:8px;font-size:13.5px"><span class="muted b">Giới hạn '+(c.limit+c.extra)+' phút</span><span class="chip t-'+(rem>10?'g':'a')+'">Còn '+rem+' phút</span></div></div></div><div class="sl-card"><div class="card-h"><div class="card-t">Nhắc nghỉ mắt</div><span class="switch on"></span></div><div class="card-s">Gợi ý bé nghỉ 5 phút sau mỗi 25 phút học.</div></div></div></div>');
    $$('.switch',root).forEach(function(s){s.setAttribute('role','switch');s.tabIndex=0;s.setAttribute('aria-checked','true');s.setAttribute('aria-label','Nhắc nghỉ mắt');});
    $$('[data-d]').forEach(function(b){b.addEventListener('click',function(){var i=+b.dataset.d;sched[i]=Math.max(15,Math.min(180,sched[i]+ +b.dataset.s));draw();});});
    var y=$('#yes'),n=$('#no');
    if(y)y.addEventListener('click',function(){S.set(function(s){var q=s.reqs.filter(function(z){return z.id===r.id;})[0];q.status='approved';s.children.filter(function(z){return z.id===c.id;})[0].extra+=r.mins;});S.notify({ic:'checkc',tone:'g',t:'Đã cho thêm '+r.mins+' phút',s:c.name});UI.toast('Đã cho '+c.name+' thêm '+r.mins+' phút');draw();});
    if(n)n.addEventListener('click',function(){S.set(function(s){s.reqs.filter(function(z){return z.id===r.id;})[0].status='rejected';});UI.toast('Đã từ chối');draw();});
  }
  UI.on(UI.byText('.head button','Lưu cài đặt'),function(){var w1=$('#w1').value,w2=$('#w2').value;if(w1>=w2)return UI.toast('Giờ bắt đầu phải trước giờ kết thúc','err');S.set(function(s){var i=s.children.filter(function(z){return z.id===c.id;})[0];i.sched=sched.slice();i.win=[w1,w2];i.limit=sched[6];});win=[w1,w2];UI.toast('Đã lưu giới hạn cho '+c.name);draw();});
  draw();document.addEventListener('kl:change',function(){var a=document.activeElement;if(!a||a.tagName!=='INPUT')draw();});
}

/* ---------------- history ---------------- */
if(P==='history'){
  var panes=$$('.main > div:last-child > div');var tabs=$('.tabs');
  var rcard=panes[1],tcard=panes[0];
  function drawRep(){var rs=S.get().reports.filter(function(r){return r.status==='open';});
    tabs.children[1].textContent='Báo cáo nội dung ('+rs.length+')';
    rcard.innerHTML=rs.length?rs.map(function(r){var l=S.lesson(r.lesson);return '<div style="margin-bottom:18px"><div class="sl-row" style="gap:12px"><span class="ib t-r"><i class="ic ic-flag"></i></span><div class="grow"><div class="card-t">'+esc(l.title)+'</div><div class="card-s">'+esc(r.by)+'</div></div><span class="chip t-a">Chờ xử lý</span></div><div class="muted" style="margin:10px 0">Lý do: "'+esc(r.reason)+'"</div><div class="sl-row" style="gap:8px"><button class="sl-btn grow" type="button" data-r="'+r.id+'" data-v="dismissed">Bỏ qua</button><button class="sl-btn sl-btn-d grow" type="button" data-r="'+r.id+'" data-v="escalated">Chặn và báo quản trị</button></div></div>';}).join(''):'<div class="empty"><div class="b" style="color:var(--text-2)">Không có báo cáo chờ xử lý</div></div>';}
  drawRep();
  rcard.addEventListener('click',function(e){var b=e.target.closest('[data-r]');if(!b)return;S.set(function(s){var r=s.reports.filter(function(z){return z.id===b.dataset.r;})[0];if(b.dataset.v==='dismissed')r.status='dismissed';else{r.sev='Khẩn';r.escalated=1;s.assigned[c.id]=(s.assigned[c.id]||[]).filter(function(x){return x!==r.lesson;});}});UI.toast(b.dataset.v==='dismissed'?'Đã bỏ qua báo cáo':'Đã chặn bài và báo quản trị');drawRep();});
  tabs.addEventListener('kl:select',function(e){var i=Array.prototype.indexOf.call(tabs.children,e.detail);tcard.classList.toggle('hidden',i===1);rcard.classList.toggle('hidden',i===0&&false);});
  UI.on(UI.byText('.head button','Xuất CSV'),function(){var rows=[['Bài học','Thời điểm','Thời lượng','Kết quả']];$$('.tbl tbody tr').forEach(function(tr){rows.push($$('td',tr).map(function(td,i){return i===0?td.querySelector('.b').textContent:td.textContent.trim();}));});UI.download('lich-su-hoc.csv',UI.csv(rows),'text/csv;charset=utf-8');UI.toast('Đã xuất lịch sử học');});
}

/* ---------------- progress ---------------- */
if(P==='progress'){
  var sg=$('.head .seg');sg.innerHTML=S.get().children.map(function(x){return '<span class="'+(x.id===c.id?'on':'')+'" data-c="'+x.id+'">'+esc(x.name)+'</span>';}).join('');
  sg.addEventListener('kl:select',function(e){S.set(function(s){s.parentChild=e.detail.dataset.c;});location.reload();});
  var k=$$('.kpi'),d=doneCount(c),tot=S.LESSONS.filter(function(l){return l.cls===c.cls;}).length;
  k[0].querySelector('.kpi-v').textContent=d+' / '+S.LESSONS.length;k[0].querySelector('.bar i').style.width=Math.round(d*100/S.LESSONS.length)+'%';k[0].querySelector('.kpi-d').textContent=Math.round(d*100/S.LESSONS.length)+'% chương trình';
  var sc=Object.keys(S.get().progress[c.id]||{}).map(function(i){return S.get().progress[c.id][i].score;}).filter(function(v){return v!=null;});k[1].querySelector('.kpi-v').textContent=sc.length?(sc.reduce(function(a,b){return a+b;},0)/sc.length).toFixed(1).replace('.',','):'–';
  k[2].querySelector('.kpi-v').textContent=(S.get().streak[c.id]||0)+' ngày';k[3].querySelector('.kpi-v').textContent=((S.get().badges[c.id]||[]).length)+' / '+S.BADGES.length;
  var path=S.PATH.toan,cc=$('.card-t');var pc=$$('.sl-card').filter(function(x){return /Lộ trình/.test(x.textContent);})[0];
  if(pc){var cur=0;path.forEach(function(id,i){if(S.prog(c.id,id).done)cur=i+1;});pc.querySelector('.chip').textContent='Bước '+Math.min(cur+1,path.length)+' / '+path.length;
    var nodes=$$('.sl-col[style*="width:120px"]',pc);nodes.forEach(function(nd,i){var done=S.prog(c.id,path[i]).done,now=i===cur,av=nd.querySelector('.av');av.style.background=done?'var(--green)':now?'var(--primary)':'var(--surface-2)';av.style.color=done||now?'#fff':'var(--text-3)';av.style.boxShadow=now?'0 0 0 6px var(--primary-soft)':'none';av.firstChild.className='ic ic-'+(done?'check':now?'play':'lock');nd.querySelector('span.b').textContent=S.lesson(path[i]).title;});}
}

/* ---------------- weekly ---------------- */
if(P==='weekly'){
  UI.on(UI.byText('.head button','Tải PDF'),function(){UI.toast('Mở hộp thoại in: chọn "Lưu dưới dạng PDF"');setTimeout(function(){window.print();},400);});
  UI.on(UI.byText('.head button','Gửi lại email'),function(){UI.toast('Đã gửi báo cáo tới '+S.get().session.email);});
  $$('.sl-card .sl-row .ic-download').forEach(function(i){var r=i.parentNode;r.style.cursor='pointer';r.setAttribute('role','button');r.tabIndex=0;r.addEventListener('click',function(){UI.download('bao-cao-tuan.txt','Báo cáo tuần '+r.textContent.trim()+'\nDữ liệu mẫu',"text/plain;charset=utf-8");UI.toast('Đã tải báo cáo');});});
}

/* ---------------- notifications ---------------- */
if(P==='notifications'){
  var tabs=$('.tabs'),mode=0;tabs.setAttribute('data-keep','');
  function draw(){var ns=S.get().notifs.filter(function(n){return mode===0||(mode===1&&(n.ic==='clock'||n.ic==='flag'))||(mode===2&&n.ic==='file');});
    tabs.children[0].textContent='Tất cả ('+S.get().notifs.filter(function(n){return n.unread;}).length+' mới)';
    $$('.main > div:not(.top):not(.head):not(.tabs)').forEach(function(x){x.remove();});
    body('<div class="sl-card" style="padding:0;overflow:hidden">'+(ns.map(function(n){return '<button type="button" data-n="'+n.id+'" class="sl-row" style="all:unset;box-sizing:border-box;width:100%;display:flex;align-items:center;gap:14px;padding:16px 18px;border-top:1px solid var(--line);cursor:pointer;'+(n.unread?'background:var(--primary-soft)':'')+'"><span class="ib t-'+n.tone+'"><i class="ic ic-'+n.ic+'"></i></span><div class="grow"><div class="b">'+esc(n.t)+'</div><div class="muted" style="font-size:13px">'+esc(n.s)+'</div></div><span class="muted" style="font-size:12.5px">'+esc(n.when)+'</span></button>';}).join('')||'<div class="empty" style="margin:16px">Không có thông báo</div>')+'</div>');
  }
  draw();
  tabs.addEventListener('kl:select',function(e){mode=Array.prototype.indexOf.call(tabs.children,e.detail);draw();});
  $('.main').addEventListener('click',function(e){var b=e.target.closest('[data-n]');if(!b)return;var n=S.get().notifs.filter(function(z){return z.id===b.dataset.n;})[0];S.set(function(s){s.notifs.forEach(function(z){if(z.id===b.dataset.n)z.unread=0;});});if(n.ic==='clock')location.href='/time';else if(n.ic==='file')location.href='/weekly';else if(n.ic==='flag')location.href='/history';else draw();});
  UI.on(UI.byText('.head button','Đánh dấu đã đọc'),function(){S.set(function(s){s.notifs.forEach(function(n){n.unread=0;});});UI.toast('Đã đánh dấu đã đọc');draw();$$('.sl-badge,.dot').forEach(function(b){b.classList.add('hidden');});});
}

/* ---------------- privacy ---------------- */
if(P==='privacy'){
  UI.on(UI.byText('button','Tải dữ liệu của bé'),function(){var x={hoSo:c,daGiao:S.get().assigned[c.id],tienDo:S.get().progress[c.id],huyHieu:S.get().badges[c.id],xuatLuc:new Date().toISOString()};UI.download('du-lieu-'+UI.vn(c.name).replace(/\s+/g,'-')+'.json',JSON.stringify(x,null,2),'application/json');UI.toast('Đã tải dữ liệu của '+c.name);});
  var del=UI.byText('button','Xóa hồ sơ');del.textContent='Xóa hồ sơ '+c.name;
  UI.on(del,function(){UI.confirm({title:'Xóa hồ sơ '+c.name+'?',text:'Dữ liệu sẽ bị xóa vĩnh viễn sau 30 ngày. Không thể hoàn tác.',ok:'Xóa hồ sơ',danger:true,icon:'trash'}).then(function(ok){if(!ok)return;S.set(function(s){s.children=s.children.filter(function(z){return z.id!==c.id;});s.parentChild=s.children[0]&&s.children[0].id;s.kidsChild=null;});UI.toast('Đã xóa hồ sơ');setTimeout(function(){location.href=S.get().children.length?'/profiles':'/onboarding';},500);});});
}

/* ---------------- settings ---------------- */
if(P==='settings'){
  var nm=$('#st1'),em=$('#st2'),sv=UI.byText('.head button','Lưu thay đổi');
  nm.value=st.session.name;em.value=st.session.email;$('.card-t[style*="18px"]').textContent=st.session.name;
  $$('[data-theme-opt]').length||$$('.sl-card').forEach(function(card){if(/Giao diện/.test(card.textContent)&&card.querySelector('.ib')){var opts=$$('.sl-col[style*="border-radius:16px"]',card);['light','dark','auto'].forEach(function(k,i){var o=opts[i];if(!o)return;o.setAttribute('data-theme-opt',k);o.setAttribute('role','button');o.tabIndex=0;o.style.cursor='pointer';o.addEventListener('click',function(){UI.theme(k);});});}});
  UI.theme();
  var sws=$$('.switch'),keys=['req','weekly','badge'];sws.forEach(function(s,i){var n=S.get().settings.notify;s.classList.toggle('on',!!n[keys[i]]);s.setAttribute('aria-checked',!!n[keys[i]]);s.addEventListener('kl:switch',function(){S.set(function(z){z.settings.notify[keys[i]]=s.classList.contains('on')?1:0;});});});
  nm.readOnly=true;em.readOnly=true;if(sv){sv.disabled=true;sv.title='Chỉnh sửa hồ sơ chưa khả dụng.';}
}
});
})();
