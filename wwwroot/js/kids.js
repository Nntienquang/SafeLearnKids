/* SafeLearn Kids · kids area (Areas/Kids) */
(function(){
'use strict';
var S=KL,UI=window.UI,$=UI.$,$$=UI.$$,esc=S.esc;
var TICK=+(new URLSearchParams(location.search).get('tick'))||2000; // demo: 1 simulated minute per TICK ms
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
/* usage timer: server-side in production (30s heartbeat); simulated here */
var timer=null;
function startTimer(onTick){
  stopTimer();timer=setInterval(function(){
    if(document.hidden)return;var c=kc();
    S.set(function(s){var x=s.children.filter(function(z){return z.id===c.id;})[0];x.used++;});
    paintTop();var r=S.remaining(kc());
    if(r===5)UI.toast('Còn 5 phút nữa là hết giờ học nhé!');
    if(onTick)onTick();
    if(r<=0){stopTimer();location.href='/kids-time-up';}
  },TICK);
}
function stopTimer(){if(timer){clearInterval(timer);timer=null;}}
window.addEventListener('beforeunload',stopTimer);
function giveStars(n){S.set(function(s){var id=s.kidsChild;s.stars[id]=(s.stars[id]||0)+n;});}
function markDone(id,score){var c=kc(),first=!S.prog(c.id,id).done;S.set(function(s){var p=s.progress[c.id]=s.progress[c.id]||{};var q=p[id]=p[id]||{};q.done=1;q.pct=100;if(score!=null)q.score=Math.max(q.score||0,score);});
  if(first){giveStars(10);var n=S.get().progress[c.id],cnt=Object.keys(n).filter(function(k){return n[k].done;}).length;
    var toan=S.LESSONS.filter(function(l){return l.subj==='toan'&&n[l.id]&&n[l.id].done;}).length;if(toan>=5&&S.addBadge(c.id,'toan'))UI.toast('Huy hiệu mới: Nhà toán học!');}
  return first;}
function reportModal(id){
  UI.modal({title:'Video này có gì lạ?',icon:'flag',tone:'a',ok:'Gửi cho ba mẹ',html:['Có quảng cáo lạ','Âm thanh quá lớn','Hình ảnh làm con sợ','Con không hiểu bài'].map(function(t,i){return '<label class="sl-row" style="gap:10px;padding:12px 14px;border-radius:14px;border:1px solid var(--line);margin-top:8px;font-weight:600"><input type="radio" name="rs" class="chk" value="'+t+'" '+(i===0?'checked':'')+'>'+t+'</label>';}).join(''),value:function(o){return o.querySelector('input:checked').value;}}).then(function(v){
    if(!v)return;var c=kc();S.set(function(s){s.reports.unshift({id:'r'+Date.now(),lesson:id,reason:v,by:'Bé · '+c.name,sev:'Vừa',status:'open',at:new Date().toISOString()});});
    S.notify({ic:'flag',tone:'r',t:c.name+' báo cáo một bài học',s:S.lesson(id).title+' · '+v});UI.toast('Đã báo cho ba mẹ. Cảm ơn con!');});
}

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

/* ---------- quiz ---------- */
if(P==='kids-quiz'){
  var c=kc(),id=q.get('id')||'nhan5',l=S.lesson(id)||S.lesson('nhan5'),Q=S.QUIZ[id]||S.QUIZ.nhan5,i=0,score=0,answered=false;
  function draw(){var it=Q[i];answered=false;
    main('<div class="sl-row" style="gap:14px;flex-wrap:wrap"><a class="k-pill" href="'+href(l)+'"><i class="ic ic-x"></i>Thoát</a><div class="grow k-card" style="padding:12px 18px"><div class="bar bar-l" style="height:14px"><i style="width:'+Math.round(i*100/Q.length)+'%;background:#22C55E"></i></div></div><span class="k-pill p">Câu '+(i+1)+' / '+Q.length+'</span></div>'+
    '<div class="k-card" style="padding:34px;display:flex;flex-direction:column;gap:26px;max-width:960px;align-self:center;width:100%"><div class="kf" id="qq" style="font-size:38px;text-align:center" tabindex="-1">'+esc(it.q)+'</div><div role="radiogroup" aria-labelledby="qq" style="display:grid;grid-template-columns:repeat(auto-fit,minmax(200px,1fr));gap:16px">'+it.a.map(function(a,k){return '<button type="button" role="radio" aria-checked="false" class="opt" data-k="'+k+'" style="padding:20px;border-radius:22px;border:3px solid var(--line);background:var(--surface);text-align:center;font-weight:800;font-size:28px;color:var(--text);font-family:inherit">'+esc(a)+'</button>';}).join('')+'</div><div id="fb" aria-live="polite"></div><div class="sl-row" style="justify-content:space-between"><button class="sl-btn sl-btn-lg" type="button" id="hint"><i class="ic ic-info"></i>Gợi ý</button><button class="k-btn" type="button" id="nx" disabled style="opacity:.5">'+(i===Q.length-1?'Xem kết quả':'Câu tiếp theo')+'<i class="ic ic-arrowr"></i></button></div></div>');
    paintTop();$('#qq').focus();
    $$('.opt').forEach(function(o){o.addEventListener('click',function(){if(answered)return;answered=true;var k=+o.dataset.k,ok=k===it.c;if(ok)score++;
      $$('.opt').forEach(function(x){x.setAttribute('aria-checked',x===o);x.disabled=true;if(+x.dataset.k===it.c)x.classList.add('ok');});if(!ok)o.classList.add('no');
      $('#fb').innerHTML='<div class="banner t-'+(ok?'g':'r')+'" style="font-weight:700"><i class="ic ic-'+(ok?'checkc':'x')+'"></i>'+(ok?'Đúng rồi! ':'Chưa đúng. ')+esc(it.e)+'</div>';
      var nx=$('#nx');nx.disabled=false;nx.style.opacity=1;nx.focus();});});
    $('#hint').addEventListener('click',function(){var wrong=$$('.opt').filter(function(x){return +x.dataset.k!==it.c&&!x.disabled;})[0];if(wrong){wrong.disabled=true;wrong.style.opacity=.3;UI.toast('Đáp án này không đúng nhé');}});
    $('#nx').addEventListener('click',function(){if(i<Q.length-1){i++;draw();}else finish();});
  }
  function finish(){stopTimer();var pct=score/Q.length,stars=score*8+(pct===1?20:0);
    var firstDone=markDone(l.id,Math.round(pct*10));giveStars(stars);var nb=null;if(pct===1&&S.addBadge(c.id,'diem10'))nb='diem10';
    sessionStorage.setItem('kl.last',JSON.stringify({id:l.id,score:score,total:Q.length,stars:stars,badge:nb}));location.href='/kids-result';}
  draw();startTimer();
}

/* ---------- result ---------- */
if(P==='kids-result'){
  var c=kc(),r=null;try{r=JSON.parse(sessionStorage.getItem('kl.last'));}catch(e){}
  if(!r){location.replace('/kids-home');return;}
  $('.kf').textContent=r.score===r.total?'Tuyệt vời, '+c.name+'!':r.score/r.total>=.6?'Giỏi quá, '+c.name+'!':'Cố lên, '+c.name+'!';
  var nums=$$('.kf');nums[1].textContent=r.score+'/'+r.total;nums[2].textContent='+'+r.stars;
  var full=Math.round(r.score/r.total*5);$$('.sl-row .ic-star').forEach(function(s,i){s.style.color=i<full?'':'var(--line)';});
  var bn=$('.banner.t-p');if(r.badge){var b=S.BADGES.filter(function(x){return x.id===r.badge;})[0];bn.innerHTML='<i class="ic ic-sparkles"></i>Huy hiệu mới: '+b.name+'!';}else bn.classList.add('hidden');
  var again=$('a.sl-btn-lg');again.href='/kids-quiz?id='+r.id;
  var hint=document.createElement('p');hint.className='k-note';hint.textContent=r.score/r.total>=.6?'Bài này đã hoàn thành.':'Con làm lại để nhớ bài hơn nhé.';again.parentNode.parentNode.appendChild(hint);
}

/* ---------- badges ---------- */
if(P==='kids-badges'){
  var c=kc(),own=S.get().badges[c.id]||[],stars=S.get().stars[c.id]||0;
  var pills=$$('main .k-pill');pills[1].innerHTML='<i class="ic ic-star"></i>'+stars+' sao';pills[2].textContent=own.length+' / '+S.BADGES.length;
  $('main > div:last-child').innerHTML=S.BADGES.map(function(b){var on=own.indexOf(b.id)>-1;return '<div class="k-card" style="flex:1 1 200px;text-align:center;display:flex;flex-direction:column;align-items:center;gap:10px;'+(on?'':'opacity:.55')+'"><span style="width:84px;height:84px;border-radius:42px;background:'+(on?'linear-gradient(135deg,'+b.g[0]+','+b.g[1]+')':'var(--surface-2)')+';color:'+(on?'#fff':'var(--text-3)')+';display:flex;align-items:center;justify-content:center"><i class="ic ic-'+(on?b.icon:'lock')+' ic-xl"></i></span><div class="b" style="font-size:16px">'+b.name+'</div><span class="k-note">'+(on?'Đã đạt · ':'Cách đạt: ')+b.d+'</span></div>';}).join('');
}
});
})();
