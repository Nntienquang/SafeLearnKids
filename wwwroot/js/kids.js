/* SafeLearn Kids · kids area (Areas/Kids) */
(function(){
'use strict';
var S=KL,UI=window.UI,$=UI.$,$$=UI.$$,esc=S.esc;
function kc(){return S.child(S.get().kidsChild);}
function main(html){var m=$('main');m.innerHTML=html;return m;}
function kind(k){return k==='Video'?'play':k==='Quiz'?'pen':'book';}
function href(l){return '/'+(l.kind==='Quiz'?'kids-quiz':l.kind==='Bài đọc'?'kids-reading':'kids-lesson')+'?id='+l.id;}
function thumb(c,ic,w,h){return '<div style="width:'+w+'px;height:'+h+'px;flex:0 0 auto;border-radius:18px;background:linear-gradient(135deg,'+c[0]+','+c[1]+');display:flex;align-items:center;justify-content:center;color:#fff"><i class="ic ic-'+ic+'"></i></div>';}
function subjStat(c,sj){var ls=S.LESSONS.filter(function(l){return l.subj===sj;});return {d:ls.filter(function(l){return S.prog(c.id,l.id).done;}).length,t:ls.length};}
/* Minutes left come from the server; the local store is only a fallback for the demo pages. */
function left(){var c=kc();return window.SL_KID_LEFT!=null?window.SL_KID_LEFT:c?S.remaining(c):0;}
function paintTop(){var c=kc();if(!c)return;var y=$('.k-top .k-pill.y');if(y)y.innerHTML='<i class="ic ic-clock"></i>Còn '+left()+' phút';
  var p=$('.k-top .k-pill.p');if(p)p.innerHTML='<span class="av av-s" style="background:linear-gradient(135deg,'+c.c[0]+','+c.c[1]+')">'+esc(c.init)+'</span>'+esc(c.name);}
document.addEventListener('kl:ui',function(){
var P=UI.PAGE,q=new URLSearchParams(location.search);
if(P!=='kids-picker')paintTop();
document.addEventListener('kl:change',function(){if(P!=='kids-picker')paintTop();});
var exitLink=$$('a[href="/kids-picker"]');

/* ---------- picker ---------- */
if(P==='kids-picker'){
  var cs=S.get().children;
  var row=$('main > div[style*="gap:24px"]');
  row.addEventListener('click',function(e){var a=e.target.closest('[data-c]');if(!a)return;S.set(function(s){s.kidsChild=a.dataset.c;});});
  var par=$('main > a.sl-btn');par.innerHTML='<i class="ic ic-lock"></i>Dành cho ba mẹ (cần PIN)';
  S.set(function(s){s.kidsChild=null;});
}

/* ---------- exit: the parent area stays hidden until the PIN is right ---------- */
if(P==='kids-exit'){
  var buf='',dots=$$('#pin-dots span'),err=$('#pin-err');
  $('#pin-back').href=S.get().kidsChild?'/kids-home':'/kids-picker';
  function paintDots(){dots.forEach(function(d,i){d.style.background=i<buf.length?'var(--primary)':'transparent';});}
  function say(msg){err.textContent=msg;err.classList.remove('hidden');buf='';paintDots();}
  function submit(){
    var lk=S.get().lock.pin||{fails:0,until:0};
    if(lk.until>Date.now())return say('Đã khóa. Thử lại sau '+Math.ceil((lk.until-Date.now())/60000)+' phút.');
    if(buf===S.get().pin){S.set(function(s){s.lock.pin={fails:0,until:0};s.kidsChild=null;});location.href='/parent-home';return;}
    S.set(function(s){var l=s.lock.pin=s.lock.pin||{fails:0,until:0};l.fails++;if(l.fails>=5){l.until=Date.now()+5*60000;l.fails=0;}});
    say(S.get().lock.pin.until>Date.now()?'Sai 5 lần. PIN bị khóa 5 phút.':'Sai PIN. Còn '+(5-S.get().lock.pin.fails)+' lần thử.');
  }
  function type(k){if(k==='back')buf=buf.slice(0,-1);else if(buf.length<4)buf+=k;paintDots();if(k!=='back'&&buf.length===4)setTimeout(submit,120);}
  $$('[data-k]').forEach(function(b){b.addEventListener('click',function(e){e.__h=true;type(b.dataset.k);});});
  document.addEventListener('keydown',function(e){if(/^\d$/.test(e.key))type(e.key);else if(e.key==='Backspace')type('back');});
}

/* ---------- SQL child pages: today's minutes, study-time heartbeat, the parent's search switch ---------- */
if(P==='kids-home-sql'){
  var c=kc(),dn=$('[data-today]');
  if(c&&dn){var tot=Math.max(1,c.limit+c.extra),pct=Math.min(100,Math.round(c.used*100/tot));dn.style.background='conic-gradient(#FFB020 0 '+pct+'%,var(--surface-2) '+pct+'% 100%)';dn.querySelector('b').innerHTML=c.used+'<small>phút</small>';$('[data-left]').textContent='Còn '+left()+' phút';}
}
if(P==='kids-lesson-sql'){
  var id=$('main').dataset.lesson;
  /* Report visible study time every 30 seconds; the server counts it, caps each report and says when time is up. */
  setInterval(function(){if(document.hidden)return;var f=new FormData();f.append('seconds','30');
    fetch('/kids-lesson/'+id+'/heartbeat',{method:'POST',credentials:'same-origin',headers:{'RequestVerificationToken':window.SL_CSRF||''},body:f})
      .then(function(r){return r.ok?r.json():null;}).then(function(s){if(!s)return;window.SL_KID_LEFT=s.left;paintTop();
        if(s.breakDue)UI.toast('Con nghỉ mắt 5 phút rồi học tiếp nhé!');
        if(s.left===5)UI.toast('Còn 5 phút nữa là hết giờ học nhé!');
        if(!s.allowed)location.href='/kids-time-up';}).catch(function(){});},30000);
}
if(P==='kids-search-sql'){
  var c=kc();
  if(c&&c.search===false){var m=$('main');m.innerHTML='<div class="empty"><div class="b" style="font-size:16px;color:var(--text-2)">Ba mẹ đã tắt tìm kiếm.</div><div style="font-size:14px;margin-top:4px">Con học bài ba mẹ giao ở trang học nhé.</div></div>';}
}

/* ---------- quiz (SQL): one question at a time, countdown, study-time heartbeat; the server scores ---------- */
if(P==='kids-quiz-sql'){
  var m=$('main'),form=$('#quiz-form'),qs=$$('[data-q]',form),cur=0,sent=false,attempt=m.dataset.attempt;
  var prev=$('[data-prev]'),next=$('[data-next]'),send=$('[data-submit]'),step=$('[data-step]'),bar=$('[data-progress]'),note=$('[data-unanswered]');
  function answered(f){return !!f.querySelector('input:checked');}
  function show(i){cur=i;qs.forEach(function(f,k){f.hidden=k!==i;});
    prev.hidden=i===0;next.hidden=i>=qs.length-1;send.hidden=i<qs.length-1;
    if(step)step.textContent='Câu '+(i+1)+' / '+qs.length;
    bar.querySelector('i').style.width=Math.round(qs.filter(answered).length*100/qs.length)+'%';
    var first=qs[i].querySelector('input');if(first&&document.activeElement!==document.body)first.focus();}
  function submitNow(){if(sent)return;sent=true;form.submit();}
  if(qs.length>1){bar.hidden=false;show(0);
    prev.addEventListener('click',function(){show(Math.max(0,cur-1));});
    next.addEventListener('click',function(){show(Math.min(qs.length-1,cur+1));});
    form.addEventListener('change',function(){bar.querySelector('i').style.width=Math.round(qs.filter(answered).length*100/qs.length)+'%';
      if(cur<qs.length-1)setTimeout(function(){show(cur+1);},350);});}
  form.addEventListener('submit',function(e){if(sent)return;var left=qs.filter(function(f){return !answered(f);}).length;
    if(!left){sent=true;return;}
    e.preventDefault();UI.confirm({title:'Còn '+left+' câu chưa trả lời',text:'Con có muốn nộp bài luôn không?',ok:'Nộp bài',icon:'info'}).then(function(ok){if(ok)submitNow();else{var k=qs.findIndex(function(f){return !answered(f);});if(k>-1&&qs.length>1)show(k);}});});
  /* Countdown from the server's remaining seconds; at zero the sheet is sent as it is. */
  var clock=$('[data-clock]'),secs=m.dataset.seconds===''?null:+m.dataset.seconds;
  if(clock&&secs!=null){var paint=function(){clock.innerHTML='<i class="ic ic-clock"></i>'+Math.floor(secs/60)+':'+('0'+secs%60).slice(-2);};paint();
    var tick=setInterval(function(){secs=Math.max(0,secs-1);paint();if(secs===60)UI.toast('Còn 1 phút nữa là hết giờ làm bài!');if(secs<=0){clearInterval(tick);submitNow();}},1000);}
  /* Study time counts on quiz pages too; when today's minutes run out the answers are submitted, not lost. */
  setInterval(function(){if(document.hidden||sent)return;var f=new FormData();f.append('seconds','30');
    fetch('/kids-quiz/'+attempt+'/heartbeat',{method:'POST',credentials:'same-origin',headers:{'RequestVerificationToken':window.SL_CSRF||''},body:f})
      .then(function(r){return r.ok?r.json():null;}).then(function(s){if(!s)return;window.SL_KID_LEFT=s.left;paintTop();
        if(s.left===5)UI.toast('Còn 5 phút nữa là hết giờ học nhé!');
        if(!s.allowed){UI.toast('Hết giờ học hôm nay, bài làm được nộp luôn.');submitNow();}}).catch(function(){});},30000);
}

/* ---------- badges ---------- */
if(P==='kids-badges'){
  var c=kc(),own=S.get().badges[c.id]||[],stars=S.get().stars[c.id]||0;
  var pills=$$('main .k-pill');pills[1].innerHTML='<i class="ic ic-star"></i>'+stars+' sao';pills[2].textContent=own.length+' / '+S.BADGES.length;
  $('main > div:last-child').innerHTML=S.BADGES.map(function(b){var on=own.indexOf(b.id)>-1;return '<div class="k-card" style="flex:1 1 200px;text-align:center;display:flex;flex-direction:column;align-items:center;gap:10px;'+(on?'':'opacity:.55')+'"><span style="width:84px;height:84px;border-radius:42px;background:'+(on?'linear-gradient(135deg,'+b.g[0]+','+b.g[1]+')':'var(--surface-2)')+';color:'+(on?'#fff':'var(--text-3)')+';display:flex;align-items:center;justify-content:center"><i class="ic ic-'+(on?b.icon:'lock')+' ic-xl"></i></span><div class="b" style="font-size:16px">'+b.name+'</div><span class="k-note">'+(on?'Đã đạt · ':'Cách đạt: ')+b.d+'</span></div>';}).join('');
}
});
})();
