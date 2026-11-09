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
  var n=$('#ob-n'),y=$('#ob-y'),cls=3,PAL=S.PALETTE,busy=false;
  $$('.seg span').forEach(function(s){if(s.classList.contains('on'))cls=+s.textContent;});
  $('.seg').addEventListener('kl:select',function(e){cls=+e.detail.textContent;});
  var nx=$('#ob-next');
  nx.addEventListener('click',function(e){e.preventDefault();
    if(!UI.field(n,n.value.trim().length<1?'Nhập tên hoặc biệt danh của bé.':n.value.trim().length>30?'Tên tối đa 30 ký tự.':''))return n.focus();
    if(y.value&&!UI.field(y,!/^(19|20)\d\d$/.test(y.value)||+y.value>2026||+y.value<2005?'Năm sinh không hợp lệ.':''))return y.focus();
    var ok=$('#ob-consent'),okErr=$('#ob-consent-err');
    if(ok&&!ok.checked){okErr.textContent='Ba mẹ cần đồng ý chính sách dữ liệu trẻ em để tạo hồ sơ.';okErr.classList.remove('hidden');return ok.focus();}
    if(okErr)okErr.classList.add('hidden');
    if(st.children.length>=4){UI.toast('Tối đa 4 hồ sơ','err');return;}
    if(busy)return;
    var nm=n.value.trim(),subjects=Object.keys(S.SUBJECTS).filter(function(k){return S.SUBJECTS[k].active!==false;});
    var add=function(id,init){
      S.set(function(s){s.children.push({id:id,name:nm,cls:cls,birthYear:y.value?+y.value:null,init:init,c:PAL[s.children.length%4],limit:60,used:0,extra:0,subjects:subjects,search:true});s.assigned[id]=[];s.progress[id]={};s.stars[id]=0;s.streak[id]=0;s.badges[id]=[];s.parentChild=id;});
      UI.toast('Đã tạo hồ sơ cho '+nm);setTimeout(function(){location.href='/parent-home';},400);};
    if(!S.synced)return add('c'+Date.now(),nm.charAt(0).toUpperCase());
    busy=true;
    S.api('/children',{name:nm,cls:cls,birthYear:y.value?+y.value:null,subjects:subjects,consent:!!(ok&&ok.checked)}).then(function(r){add(r.id,r.init);}).catch(function(err){busy=false;UI.toast(err.message,'err');});});
  if(!st.children.length){$$('a[href="/register"]').forEach(function(a){a.classList.add('hidden');});}
}

/* home: rendered on the server (Areas/Parent/Views/Home/Index.cshtml) */

/* ---------------- profiles ---------------- */
if(P==='profiles'){
  function card(x){var pct=Math.min(100,Math.round(x.used*100/(x.limit+x.extra)));
    return '<div class="sl-card" data-id="'+x.id+'" style="flex:1 1 320px;min-width:0;display:flex;flex-direction:column;gap:16px"><div class="sl-row" style="gap:14px"><span class="av av-l" style="background:linear-gradient(135deg,'+x.c[0]+','+x.c[1]+')">'+esc(x.init)+'</span><div class="grow"><div class="card-t" style="font-size:18px">'+esc(x.name)+'</div><div class="muted b" style="font-size:13px">Lớp '+x.cls+(x.birthYear?' · Sinh năm '+x.birthYear:'')+'</div></div></div><div><div class="sl-row" style="justify-content:space-between;font-size:13px;margin-bottom:6px"><span class="muted b">Hôm nay</span><span class="b">'+x.used+' / '+(x.limit+x.extra)+' phút</span></div><div class="bar bar-l"><i style="width:'+pct+'%"></i></div></div><div class="wrap" style="gap:8px">'+x.subjects.map(function(s){return '<span class="chip t-'+S.SUBJECTS[s].tone+'">'+S.SUBJECTS[s].name+'</span>';}).join('')+'</div><div class="sl-row" style="gap:8px;margin-top:auto"><button type="button" class="sl-btn sl-btn-sm grow" data-edit="'+x.id+'"><i class="ic ic-edit"></i>Sửa</button><a class="sl-btn sl-btn-sm grow" href="/time" data-pc="'+x.id+'"><i class="ic ic-clock"></i>Giờ học</a><a class="sl-btn sl-btn-sm sl-btn-p grow" href="/kids-picker">Vào học</a></div></div>';}
  function chip(k,on){return '<button type="button" class="chip '+(on?'t-'+S.SUBJECTS[k].tone:'t-n')+'" data-sj="'+k+'" aria-pressed="'+on+'" style="border:0;cursor:pointer">'+(on?'<i class="ic ic-check"></i>':'')+S.SUBJECTS[k].name+'</button>';}
  function render(){
    var cs=S.get().children;
    var add=cs.length<4?'<a class="empty" href="/onboarding" style="flex:1 1 320px;display:flex;flex-direction:column;align-items:center;justify-content:center;gap:12px"><span class="ib t-n" style="width:56px;height:56px"><i class="ic ic-plus"></i></span><div class="b" style="color:var(--text-2)">Thêm bé thứ '+(cs.length+1)+'</div><div style="font-size:13px">Mỗi tài khoản có tối đa 4 hồ sơ.</div></a>':'';
    body('<div style="display:flex;flex-wrap:wrap;gap:16px">'+cs.map(card).join('')+add+'</div>');
    $$('[data-edit]').forEach(function(b){b.addEventListener('click',function(e){e.__h=true;edit(S.child(b.dataset.edit));});});
    $$('[data-pc]').forEach(function(a){a.addEventListener('click',function(){S.set(function(s){s.parentChild=a.dataset.pc;});});});
  }
  /* The dialog edits a copy: nothing reaches the profile until Save succeeds. */
  function edit(cur){
    var subjects=cur.subjects.slice(),remove=false,year=new Date().getFullYear();
    var fail=function(ov,msg){var e=$('#pf-err',ov);e.textContent=msg;e.classList.remove('hidden');};
    UI.modal({title:'Sửa hồ sơ · '+cur.name,tone:'p',icon:'edit',ok:'Lưu thay đổi',
      html:'<div style="display:flex;flex-direction:column;gap:16px"><div class="field"><label class="lab" for="pf1">Tên hiển thị</label><input id="pf1" class="inp" value="'+esc(cur.name)+'" maxlength="30" autocomplete="off"></div><div class="field"><label class="lab" for="pf2">Năm sinh (không bắt buộc)</label><input id="pf2" class="inp" type="number" min="2005" max="'+year+'" value="'+(cur.birthYear||'')+'"></div><div class="field"><span class="lab">Lớp</span><div class="seg" id="pf-cls">'+[1,2,3,4,5].map(function(i){return '<span role="button" tabindex="0" class="'+(i===cur.cls?'on':'')+'">'+i+'</span>';}).join('')+'</div></div><div class="field"><span class="lab">Môn được học</span><div class="wrap" style="gap:8px" id="pf-sj">'+Object.keys(S.SUBJECTS).filter(function(k){return S.SUBJECTS[k].active!==false||subjects.indexOf(k)>-1;}).map(function(k){return chip(k,subjects.indexOf(k)>-1);}).join('')+'</div></div><div class="field"><span class="lab">Cho phép tìm kiếm</span><div class="sl-row" style="gap:10px"><span class="switch'+(cur.search?' on':'')+'" id="pf-search" role="switch" tabindex="0" aria-checked="'+!!cur.search+'" aria-label="Cho phép tìm kiếm"></span><span class="muted" style="font-size:13px">Chỉ tìm trong bài đã giao</span></div></div><span class="err hidden" id="pf-err" role="alert"></span>'+(S.get().children.length>1?'<div class="sl-row" style="justify-content:space-between;gap:12px;padding-top:14px;border-top:1px solid var(--line)"><span class="muted" style="font-size:12.5px">Xóa hồ sơ sẽ xóa cả dữ liệu học tập của bé.</span><button type="button" class="sl-btn sl-btn-d sl-btn-sm" id="pf-del" data-x="0"><i class="ic ic-trash"></i>Xóa hồ sơ</button></div>':'')+'</div>',
      ready:function(ov){
        var del=$('#pf-del',ov);if(del)del.addEventListener('click',function(){remove=true;});
        $('#pf-sj',ov).addEventListener('click',function(e){var b=e.target.closest('[data-sj]');if(!b)return;var k=b.dataset.sj,i=subjects.indexOf(k);if(i>-1)subjects.splice(i,1);else subjects.push(k);b.outerHTML=chip(k,i<0);});
        $$('input',ov).forEach(function(i){i.addEventListener('keydown',function(e){if(e.key==='Enter'){e.preventDefault();$('[data-x="1"]',ov).click();}});});
      },
      submit:function(ov){
        var n=$('#pf1',ov),y=$('#pf2',ov),nm=n.value.trim(),by=y.value?+y.value:null;
        $('#pf-err',ov).classList.add('hidden');
        if(!UI.field(n,nm?'':'Nhập tên của bé.')){n.focus();return false;}
        if(!UI.field(y,by!==null&&(by<2005||by>year)?'Năm sinh không hợp lệ.':'')){y.focus();return false;}
        if(!subjects.length){fail(ov,'Chọn ít nhất 1 môn.');return false;}
        var cl=+$('#pf-cls .on',ov).textContent,se=$('#pf-search',ov).classList.contains('on');
        var keep=function(){S.set(function(s){var i=s.children.filter(function(z){return z.id===cur.id;})[0];i.name=nm;i.init=nm.charAt(0).toUpperCase();i.cls=cl;i.birthYear=by;i.subjects=subjects;i.search=se;});UI.toast('Đã lưu hồ sơ');render();return true;};
        if(!S.synced)return keep();
        return S.api('/children/'+cur.id,{name:nm,cls:cl,birthYear:by,subjects:subjects}).then(keep).catch(function(err){fail(ov,err.message);return false;});
      }}).then(function(){if(remove)drop(cur);});
  }
  function drop(cur){
    var gone=function(){S.set(function(s){s.children=s.children.filter(function(z){return z.id!==cur.id;});delete s.assigned[cur.id];delete s.progress[cur.id];if(s.parentChild===cur.id)s.parentChild=s.children[0].id;if(s.kidsChild===cur.id)s.kidsChild=null;});UI.toast('Đã xóa hồ sơ');render();return true;};
    UI.modal({title:'Xóa hồ sơ '+cur.name+'?',text:'Toàn bộ lịch sử học và điểm quiz sẽ bị xóa vĩnh viễn, không thể hoàn tác.',ok:'Xóa hồ sơ',danger:true,icon:'trash',
      html:'<div class="field"><label class="lab" for="pf-del-n">Nhập "'+esc(cur.name)+'" để xác nhận</label><input id="pf-del-n" class="inp" autocomplete="off"></div>',
      submit:function(ov){var n=$('#pf-del-n',ov);
        if(!UI.field(n,n.value.trim()===cur.name?'':'Tên chưa khớp.')){n.focus();return false;}
        if(!S.synced)return gone();
        return S.api('/children/'+cur.id+'/delete').then(gone).catch(function(err){UI.field(n,err.message);return false;});}});
  }
  render();
}





/* Weekly report: the browser's print dialog also saves as PDF. */
if(P==='weekly-sql'){
  var pr=$('[data-print]');if(pr)pr.addEventListener('click',function(e){e.__h=true;window.print();});
}

});
})();
