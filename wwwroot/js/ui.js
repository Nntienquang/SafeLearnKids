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
    if(AREA==='kids'&&PAGE!=='kids-picker'&&PAGE!=='kids-exit'&&!S.get().kidsChild){location.replace('/kids-picker');return false;}
    if(AREA==='parent'&&PAGE!=='onboarding'&&!S.get().children.length){location.replace('/onboarding');return false;}
    return true;}
  var s=S.get().session;
  var need=AREA==='parent'||AREA==='kids'?'parent':AREA==='admin'?'admin':null;
  if(need&&(!s||s.role!==need)){location.replace('/login?next='+encodeURIComponent(location.pathname.split('/').pop()+location.search));return false;}
  if(AREA==='parent'&&PAGE!=='onboarding'&&!S.get().children.length){location.replace('/onboarding');return false;}
  if(AREA==='kids'&&PAGE!=='kids-picker'&&PAGE!=='kids-exit'&&!S.get().kidsChild){location.replace('/kids-picker');return false;}
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
  document.documentElement.style.colorScheme=dark?'dark':'light';
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
    var busy=false;
    ov.addEventListener('click',function(e){if(e.target===ov&&!o.submit)close(null);var b=e.target.closest('[data-x]');if(!b)return;
      /* o.submit keeps the dialog open until it succeeds, so errors stay beside the form. */
      if(b.dataset.x==='1'&&o.submit){if(busy)return;var r=o.submit(ov);if(r===false)return;busy=b.disabled=true;Promise.resolve(r).then(function(ok){busy=b.disabled=false;if(ok)close(true);});return;}
      close(b.dataset.x==='1'?(o.value?o.value(ov):true):false);});
    document.addEventListener('keydown',key);APP.appendChild(ov);var f=$('[data-x="1"],[data-x="0"],input,textarea',ov);f&&f.focus();if(o.ready)o.ready(ov);
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
  /* The bell is a real link rendered by the server (parents: notifications, admins: content reports). */
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
    var inp=sb.querySelector('input');var q=new URLSearchParams(location.search).get('q');if(q)inp.value=q;
    inp.addEventListener('keydown',function(e){if(e.key!=='Enter')return;var v=inp.value.trim();
      /* Real searches: parents search the lesson library, admins the content catalog. */
      if(AREA==='parent')location.href='/library?q='+encodeURIComponent(v);
      else if(AREA==='admin')location.href='/admin-catalog?q='+encodeURIComponent(v);});}
  // child switcher: rendered on the server (_ParentSidebar.cshtml) as a <details> menu of ?child= links
  // bell dot + nav badge from store
  /* Counts come from the server when signed in; the demo store is only a fallback. */
  var unread=window.SL_UNREAD!=null?window.SL_UNREAD:S.get().notifs.filter(function(n){return n.unread;}).length;
  $$('.sl-nav').forEach(function(n){if(/Thông báo/.test(n.textContent)){var b=n.querySelector('.sl-badge');if(b){b.textContent=unread;b.classList.toggle('hidden',!unread);}}});
  var bell=window.SL_USER&&window.SL_USER.role==='admin'?(window.SL_OPEN_REPORTS||0):unread;
  $$('.icbtn .dot').forEach(function(d){d.classList.toggle('hidden',!bell);});
  var pr=S.pendingReqs().length;
  $$('.sl-nav').forEach(function(n){if(/Báo cáo nội dung/.test(n.textContent)){var b=n.querySelector('.sl-badge');if(b){var c=window.SL_OPEN_REPORTS!=null?window.SL_OPEN_REPORTS:S.get().reports.filter(function(r){return r.status==='open';}).length;b.textContent=c;b.classList.toggle('hidden',!c);}}});
  // images/icons decorative
  $$('i.ic').forEach(function(i){i.setAttribute('aria-hidden','true');});
}
document.addEventListener('DOMContentLoaded',function(){});
/* Forms marked data-confirm ask before posting, on every page (admin, parent and child). */
document.addEventListener('submit',function(e){var f=e.target,sub=e.submitter;if(!f.dataset||!f.dataset.confirm||f.dataset.confirmed)return;e.preventDefault();
  UI.confirm({title:f.dataset.confirm,text:f.dataset.text,ok:f.dataset.okLabel,danger:f.dataset.danger==='true',icon:'lock'}).then(function(ok){if(!ok)return;f.dataset.confirmed='1';/* requestSubmit keeps the clicked button's name and value (for example decision=approve). */if(sub&&f.requestSubmit)f.requestSubmit(sub);else f.submit();});});
UI.ready=function(fn){if(document.readyState!=='loading')fn();else document.addEventListener('DOMContentLoaded',fn);};
UI.ready(function(){enhance();UI.theme();document.dispatchEvent(new CustomEvent('kl:ui'));});
matchMedia('(prefers-color-scheme:dark)').addEventListener&&matchMedia('(prefers-color-scheme:dark)').addEventListener('change',function(){UI.theme();});
document.addEventListener('kl:change',function(){});
function avatarPick(p,state,src){var q=function(s){return p.querySelector(s);},img=q('[data-avatar-image]'),start=p.dataset.start===undefined?(p.dataset.start=img.getAttribute('src')||''):p.dataset.start,save=p.closest('form').querySelector('[data-avatar-save]'),shown=state==='new'?src:state==='clear'?'':start;
if(img.src.indexOf('blob:')===0)URL.revokeObjectURL(img.src);
if(shown)img.src=shown;else img.removeAttribute('src');img.hidden=!shown;q('[data-avatar-initial]').hidden=!!shown;
q('[data-avatar-clear]').value=state==='clear'?'true':'false';q('[data-avatar-undo]').hidden=state==='same';q('[data-avatar-remove]').hidden=state!=='same'||!start;
if(state!=='new')q('[data-avatar-file]').value='';if(save)save.hidden=state==='same';
var s=q('[data-avatar-status]');s.className='ud-hint '+(state==='same'?'muted':'t-note');s.textContent=state==='new'?'Đang xem trước, ảnh chưa được lưu.':state==='clear'?'Ảnh sẽ được gỡ khi bạn lưu.':'JPG, PNG hoặc WebP, tối đa 2 MB.';}
document.addEventListener('change',function(e){var t=e.target;if(!t.matches||!t.matches('[data-avatar-file]')||!t.files.length)return;var f=t.files[0],p=t.closest('[data-avatar-picker]'),s=p.querySelector('[data-avatar-status]');
if(['image/jpeg','image/png','image/webp'].indexOf(f.type)<0||f.size>2097152){t.value='';s.className='ud-hint err';s.textContent='Chọn ảnh JPG, PNG hoặc WebP, tối đa 2 MB.';return;}
avatarPick(p,'new',URL.createObjectURL(f));});
document.addEventListener('click',function(e){var b=e.target.closest&&e.target.closest('[data-avatar-remove],[data-avatar-undo]');if(b)avatarPick(b.closest('[data-avatar-picker]'),b.hasAttribute('data-avatar-remove')?'clear':'same');});
})();
