/* SafeLearn Kids · UI layer: theme, toast, modal, guards, shared widgets */
(function(){
'use strict';
var S=window.KL,B=document.body,APP=document.getElementById('app'),AREA=B.dataset.area||'public',PAGE=B.dataset.page;
var $=function(s,r){return (r||document).querySelector(s);},$$=function(s,r){return Array.prototype.slice.call((r||document).querySelectorAll(s));};
var UI={$:$,$$:$$,PAGE:PAGE,AREA:AREA};
window.UI=UI;

/* ---------- guards ---------- */
UI.guard=function(){
  if(window.SL_USER){ /* server mode: [Authorize] already enforced */
    if(AREA==='kids'&&PAGE!=='kids-picker'&&!S.get().kidsChild){location.replace('/kids-picker');return false;}
    if(AREA==='parent'&&PAGE!=='onboarding'&&!S.get().children.length){location.replace('/onboarding');return false;}
    return true;}
  var s=S.get().session;
  var need=AREA==='parent'||AREA==='kids'?'parent':AREA==='admin'?'admin':null;
  if(need&&(!s||s.role!==need)){location.replace('/login?next='+encodeURIComponent(location.pathname.split('/').pop()+location.search));return false;}
  if(AREA==='parent'&&PAGE!=='onboarding'&&!S.get().children.length){location.replace('/onboarding');return false;}
  if(AREA==='kids'&&PAGE!=='kids-picker'&&!S.get().kidsChild){location.replace('/kids-picker');return false;}
  return true;
};
if(!UI.guard())return;

/* ---------- theme ---------- */
var ORDER=['light','dark','auto'],LABEL={light:'Sáng',dark:'Tối',auto:'Theo máy'};
UI.theme=function(t){
  if(t){S.set(function(s){s.theme=t;});}
  t=S.get().theme;APP.setAttribute('data-theme',t);
  var dark=t==='dark'||(t==='auto'&&matchMedia('(prefers-color-scheme:dark)').matches);
  document.documentElement.setAttribute('data-bs-theme',dark?'dark':'light');
  $$('[data-theme-toggle] .ic').forEach(function(i){i.className='ic '+(dark?'ic-sun':'ic-moon');});
  $$('[data-theme-toggle]').forEach(function(b){b.setAttribute('aria-label','Giao diện: '+LABEL[t]+'. Bấm để đổi');b.title='Giao diện: '+LABEL[t];});
  $$('[data-theme-opt]').forEach(function(o){var on=o.dataset.themeOpt===t;o.style.borderColor=on?'var(--primary)':'var(--line)';o.setAttribute('aria-pressed',on);});
};
function cycleTheme(){var i=ORDER.indexOf(S.get().theme);UI.theme(ORDER[(i+1)%3]);UI.toast('Giao diện: '+LABEL[S.get().theme]);}

/* ---------- toast / modal ---------- */
var tbox=document.createElement('div');tbox.className='toasts';tbox.setAttribute('aria-live','polite');tbox.setAttribute('role','status');APP.appendChild(tbox);
UI.toast=function(msg,type){
  var t=document.createElement('div');t.className='sl-toast';
  t.innerHTML='<i class="ic ic-'+(type==='err'?'alert':'checkc')+'"></i><span>'+S.esc(msg)+'</span>';
  tbox.appendChild(t);setTimeout(function(){t.style.opacity=0;t.style.transition='opacity .3s';setTimeout(function(){t.remove();},300);},2800);
};
UI.modal=function(o){
  return new Promise(function(res){
    var ov=document.createElement('div');ov.className='overlay';ov.setAttribute('role','dialog');ov.setAttribute('aria-modal','true');ov.setAttribute('aria-label',o.title);
    ov.innerHTML='<div class="sl-modal"><div class="sl-row" style="gap:12px;margin-bottom:6px"><span class="ib t-'+(o.tone||'r')+'"><i class="ic ic-'+(o.icon||'alert')+'"></i></span><div class="card-t">'+S.esc(o.title)+'</div></div>'+
      (o.text?'<p class="sub" style="margin:10px 0 0;line-height:1.6">'+S.esc(o.text)+'</p>':'')+(o.html?'<div style="margin-top:14px">'+o.html+'</div>':'')+
      '<div class="sl-row" style="gap:10px;justify-content:flex-end;margin-top:20px"><button type="button" class="sl-btn" data-x="0">'+(o.cancel||'Hủy')+'</button>'+(o.noOk?'':'<button type="button" class="sl-btn '+(o.danger?'sl-btn-d':'sl-btn-p')+'" data-x="1">'+(o.ok||'Đồng ý')+'</button>')+'</div></div>';
    var prev=document.activeElement;
    function close(v){ov.remove();document.removeEventListener('keydown',key);if(prev&&prev.focus)prev.focus();res(v);}
    function key(e){if(e.key==='Escape')close(null);if(e.key==='Tab'){var f=$$('button,input,select,textarea,[tabindex="0"]',ov);if(!f.length)return;var a=f[0],z=f[f.length-1];if(e.shiftKey&&document.activeElement===a){e.preventDefault();z.focus();}else if(!e.shiftKey&&document.activeElement===z){e.preventDefault();a.focus();}}}
    ov.addEventListener('click',function(e){if(e.target===ov)close(null);var b=e.target.closest('[data-x]');if(b)close(b.dataset.x==='1'?(o.value?o.value(ov):true):false);});
    document.addEventListener('keydown',key);APP.appendChild(ov);var f=$('[data-x="1"],[data-x="0"],input,textarea',ov);f&&f.focus();
  });
};
UI.confirm=function(o){return UI.modal(o).then(function(v){return v===true;});};
UI.download=function(name,text,mime){var a=document.createElement('a');a.href=URL.createObjectURL(new Blob([text],{type:mime||'text/plain;charset=utf-8'}));a.download=name;document.body.appendChild(a);a.click();setTimeout(function(){URL.revokeObjectURL(a.href);a.remove();},500);};
UI.csv=function(rows){return '﻿'+rows.map(function(r){return r.map(function(c){c=String(c==null?'':c);return /[",\n]/.test(c)?'"'+c.replace(/"/g,'""')+'"':c;}).join(',');}).join('\n');};
UI.go=function(u){location.href=u;};
UI.byText=function(sel,text,root){return $$(sel,root).filter(function(e){return e.textContent.replace(/\s+/g,' ').trim().indexOf(text)>-1;})[0]||null;};
UI.on=function(el,fn){if(!el)return;el.addEventListener('click',function(e){e.__h=true;fn(e,el);});};
UI.field=function(inp,msg){ /* inline validation state */
  inp.classList.toggle('bad',!!msg);inp.setAttribute('aria-invalid',!!msg);
  var h=inp.closest('.field');var e=h&&$('.err',h);
  if(msg){if(!e){e=document.createElement('span');e.className='err';e.setAttribute('role','alert');h.appendChild(e);}e.textContent=msg;e.classList.remove('hidden');}
  else if(e){e.classList.add('hidden');}
  return !msg;
};
UI.emailOk=function(v){return /^[^\s@]+@[^\s@]+\.[^\s@]{2,}$/.test(v);};
UI.vn=function(s){return String(s).toLowerCase().normalize('NFD').replace(/[̀-ͯ]/g,'').replace(/đ/g,'d');};
UI.logout=function(){S.set(function(s){s.session=null;s.kidsChild=null;});location.href='/login';};

/* ---------- shared enhancements ---------- */
function enhance(){
  document.documentElement.lang='vi';
  var main=$('.main')||$('main');if(main&&!main.id)main.id='main';
  var skip=document.createElement('a');skip.className='skip';skip.href='#main';skip.textContent='Bỏ qua tới nội dung';APP.insertBefore(skip,APP.firstChild);
  // theme toggles
  $$('.icbtn[aria-label^="Đổi giao diện"],.icbtn[aria-label^="Giao diện sáng"]').forEach(function(b){b.setAttribute('data-theme-toggle','');b.setAttribute('role','button');b.tabIndex=0;});
  if(AREA==='kids'){var r=$('.k-top .sl-row:last-child');if(r){var b=document.createElement('button');b.type='button';b.className='k-pill';b.setAttribute('data-theme-toggle','');b.innerHTML='<i class="ic ic-moon"></i>';r.appendChild(b);}}
  document.addEventListener('click',function(e){if(e.target.closest('[data-theme-toggle]'))cycleTheme();});
  document.addEventListener('keydown',function(e){var t=e.target;if((e.key==='Enter'||e.key===' ')&&t.matches&&t.matches('.icbtn[role=button],.switch,.seg span,.tabs span,[data-theme-opt]')){e.preventDefault();t.click();}});
  // switches
  $$('.switch').forEach(function(s){s.setAttribute('role','switch');s.tabIndex=0;s.setAttribute('aria-checked',s.classList.contains('on'));
    var lab=s.closest('.sl-row,.card-h');if(lab&&!s.getAttribute('aria-label')){var t=lab.querySelector('.b,.card-t');if(t)s.setAttribute('aria-label',t.textContent.trim());}});
  document.addEventListener('click',function(e){var s=e.target.closest('.switch');if(!s)return;s.classList.toggle('on');s.setAttribute('aria-checked',s.classList.contains('on'));s.dispatchEvent(new CustomEvent('kl:switch',{bubbles:true}));});
  // seg / tabs
  document.addEventListener('click',function(e){var t=e.target.closest('.seg span,.tabs span');if(!t||t.hasAttribute('data-nosel'))return;var p=t.parentNode;$$('span',p).forEach(function(x){x.classList.remove('on');x.removeAttribute('aria-pressed');});t.classList.add('on');t.setAttribute('aria-pressed','true');p.dispatchEvent(new CustomEvent('kl:select',{bubbles:true,detail:t}));});
  $$('.seg span,.tabs span').forEach(function(s){s.setAttribute('role','button');s.tabIndex=0;});
  // nav
  $$('.sl-nav.on').forEach(function(n){n.setAttribute('aria-current','page');});
  $$('.side').forEach(function(s){s.setAttribute('aria-label','Điều hướng chính');});
  $$('a[href="/login"].btn-g').forEach(function(a){if(/Đăng xuất/.test(a.textContent))a.addEventListener('click',function(e){e.preventDefault();UI.logout();});});
  $$('.icbtn[aria-label="Thông báo"]').forEach(function(b){var a=document.createElement('a');a.className=b.className;a.href='/notifications';a.setAttribute('aria-label','Thông báo');a.innerHTML=b.innerHTML;b.replaceWith(a);});
  // mobile drawer
  var side=$('.side'),top=$('.top');
  if(side&&top){var h=document.createElement('button');h.type='button';h.className='icbtn hamb';h.setAttribute('aria-label','Mở menu');h.innerHTML='<i class="ic ic-menu"></i>';top.insertBefore(h,top.firstChild);
    var sc=document.createElement('div');sc.className='scrim';APP.appendChild(sc);
    var tog=function(on){side.classList.toggle('open',on);sc.classList.toggle('on',on);h.setAttribute('aria-expanded',on);};
    h.addEventListener('click',function(){tog(!side.classList.contains('open'));});sc.addEventListener('click',function(){tog(false);});
    document.addEventListener('keydown',function(e){if(e.key==='Escape')tog(false);});}
  // search box -> input
  var sb=$('.top .search');
  if(sb){var ic=sb.querySelector('.ic').outerHTML,ph=sb.textContent.trim();sb.innerHTML=ic+'<input type="search" aria-label="Tìm kiếm" placeholder="'+S.esc(ph)+'">';
    var inp=sb.querySelector('input');var q=new URLSearchParams(location.search).get('q');if(q&&PAGE==='library')inp.value=q;
    inp.addEventListener('keydown',function(e){if(e.key==='Enter'){var v=inp.value.trim();if(AREA==='parent'){if(PAGE==='library'){document.dispatchEvent(new CustomEvent('kl:q',{detail:v}));}else location.href='/library?q='+encodeURIComponent(v);}else UI.toast('Tìm "'+v+'" trong quản trị');}});
    inp.addEventListener('input',function(){if(PAGE==='library')document.dispatchEvent(new CustomEvent('kl:q',{detail:inp.value.trim()}));});}
  // child switcher
  var cs=$('.child-sw');
  if(cs){cs.tabIndex=0;cs.setAttribute('role','button');cs.setAttribute('aria-haspopup','menu');
    var paint=function(){var c=S.child(S.get().parentChild);cs.querySelector('.av').textContent=c.init;cs.querySelector('.av').style.background='linear-gradient(135deg,'+c.c[0]+','+c.c[1]+')';cs.querySelector('.b').textContent=c.name;cs.querySelector('.muted').textContent='Lớp '+c.cls+' · '+S.remaining(c)+' phút còn lại';};paint();document.addEventListener('kl:change',paint);
    var pop=null;var open=function(){if(pop){pop.remove();pop=null;return;}pop=document.createElement('div');pop.className='pop';pop.setAttribute('role','menu');pop.style.cssText='left:14px;top:'+(cs.offsetTop+cs.offsetHeight+4)+'px';
      pop.innerHTML=S.get().children.map(function(c){return '<button type="button" role="menuitem" data-c="'+c.id+'"><span class="av av-s" style="background:linear-gradient(135deg,'+c.c[0]+','+c.c[1]+')">'+c.init+'</span>'+S.esc(c.name)+' · Lớp '+c.cls+'</button>';}).join('');
      cs.parentNode.style.position='relative';cs.parentNode.appendChild(pop);pop.addEventListener('click',function(e){var b=e.target.closest('[data-c]');if(!b)return;S.set(function(s){s.parentChild=b.dataset.c;});pop.remove();pop=null;UI.toast('Đang xem '+S.child(b.dataset.c).name);document.dispatchEvent(new CustomEvent('kl:child'));});};
    cs.addEventListener('click',open);cs.addEventListener('keydown',function(e){if(e.key==='Enter'||e.key===' '){e.preventDefault();open();}});}
  // bell dot + nav badge from store
  var unread=S.get().notifs.filter(function(n){return n.unread;}).length;
  $$('.sl-nav').forEach(function(n){if(/Thông báo/.test(n.textContent)){var b=n.querySelector('.sl-badge');if(b){b.textContent=unread;b.classList.toggle('hidden',!unread);}}});
  $$('.icbtn .dot').forEach(function(d){d.classList.toggle('hidden',!unread);});
  var pr=S.pendingReqs().length;
  $$('.sl-nav').forEach(function(n){if(/Báo cáo nội dung/.test(n.textContent)){var b=n.querySelector('.sl-badge');if(b){var c=S.get().reports.filter(function(r){return r.status==='open';}).length;b.textContent=c;b.classList.toggle('hidden',!c);}}});
  // images/icons decorative
  $$('i.ic').forEach(function(i){i.setAttribute('aria-hidden','true');});
}
/* generic fallback for demo-only buttons */
document.addEventListener('click',function(e){
  if(e.__h||e.defaultPrevented)return;
  var b=e.target.closest('button[type=button],a.sl-btn[href="#"],button.sl-btn:not([type])');if(!b||b.closest('.sl-modal')||b.closest('.pop'))return;
  var t=b.textContent.replace(/\s+/g,' ').trim();if(!t)return;
  if(/^(Tải|Xuất)/.test(t)){UI.download('safelearn-'+PAGE+'.csv',UI.csv([['Trang',PAGE],['Xuất lúc',new Date().toLocaleString('vi-VN')],['Nội dung','Dữ liệu mẫu của bản demo']]),'text/csv;charset=utf-8');UI.toast('Đã tạo tệp '+t.toLowerCase());}
  else if(/^Lưu|Cập nhật/.test(t))UI.toast('Đã lưu thay đổi');
  else if(/^Gửi/.test(t))UI.toast('Đã gửi');
  else if(/^Thêm|mới$/.test(t))UI.toast('Tính năng sẽ có trong sprint tiếp theo','err');
  else if(/^Hủy|^Quay/.test(t))history.back();
  else UI.toast(t);
});
document.addEventListener('DOMContentLoaded',function(){});
UI.ready=function(fn){if(document.readyState!=='loading')fn();else document.addEventListener('DOMContentLoaded',fn);};
UI.ready(function(){enhance();UI.theme();document.dispatchEvent(new CustomEvent('kl:ui'));});
matchMedia('(prefers-color-scheme:dark)').addEventListener&&matchMedia('(prefers-color-scheme:dark)').addEventListener('change',function(){UI.theme();});
document.addEventListener('kl:change',function(){});
})();
