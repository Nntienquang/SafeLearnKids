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
function paintTop(){if($('.k-top[data-server]')){/* name is server-rendered; keep the live minutes from the heartbeat */var t=$('.k-top .k-pill.y');if(t&&window.SL_KID_LEFT!=null)t.innerHTML='<i class="ic ic-clock"></i>Còn '+window.SL_KID_LEFT+' phút';return;}var c=kc();if(!c)return;var y=$('.k-top .k-pill.y');if(y)y.innerHTML='<i class="ic ic-clock"></i>Còn '+left()+' phút';
  var p=$('.k-top .k-pill.p');if(p)p.innerHTML='<span class="av av-s" style="background:linear-gradient(135deg,'+c.c[0]+','+c.c[1]+')">'+esc(c.init)+'</span>'+esc(c.name);}
document.addEventListener('kl:ui',function(){
var P=UI.PAGE,q=new URLSearchParams(location.search);
if(P!=='kids-picker')paintTop();
document.addEventListener('kl:change',function(){if(P!=='kids-picker')paintTop();});
var exitLink=$$('a[href="/kids-picker"]');

/* ---------- read aloud: the device's own Vietnamese voice (Web Speech API); nothing is sent to a server ----------
   Buttons stay hidden unless a Vietnamese voice exists, so an English voice never mangles the text. A second press stops. */
var ttsButtons=$$('[data-tts]');
if(ttsButtons.length&&'speechSynthesis' in window&&'SpeechSynthesisUtterance' in window){
  var synth=window.speechSynthesis,viVoice=null,speaking=null;
  var pickVoice=function(){var vs=synth.getVoices();viVoice=vs.filter(function(v){return /^vi(-|_|$)/i.test(v.lang);})[0]||null;
    ttsButtons.forEach(function(b){b.hidden=!viVoice;});};
  var reset=function(){if(speaking){speaking.setAttribute('aria-pressed','false');speaking=null;}};
  pickVoice();
  if(synth.addEventListener)synth.addEventListener('voiceschanged',pickVoice);else synth.onvoiceschanged=pickVoice;
  ttsButtons.forEach(function(b){b.addEventListener('click',function(){
    var again=speaking===b;synth.cancel();reset();if(again||!viVoice)return;
    var text=b.dataset.tts.split(',').map(function(sel){var el=$(sel.trim());return el?el.innerText.replace(/\s+/g,' ').trim():'';}).filter(Boolean).join('. ');
    if(!text)return;
    var u=new SpeechSynthesisUtterance(text);u.lang=viVoice.lang;u.voice=viVoice;u.rate=0.9;
    u.onend=u.onerror=function(){if(speaking===b)reset();};
    speaking=b;b.setAttribute('aria-pressed','true');synth.speak(u);});});
  window.addEventListener('pagehide',function(){synth.cancel();});
}

/* ---------- picker ---------- */
if(P==='kids-picker'){
  var cs=S.get().children;
  var row=$('main > div[style*="gap:24px"]');
  row.addEventListener('click',function(e){var a=e.target.closest('[data-c]');if(!a)return;S.set(function(s){s.kidsChild=a.dataset.c;});});
  S.set(function(s){s.kidsChild=null;});
}

/* ---------- exit: the server checks the PIN; the keypad only fills the form ---------- */
if(P==='kids-exit'){
  var pf=$('#pin-form');
  if(pf){
    var input=$('#pin-input'),dots=$$('#pin-dots span'),sent=false;
    input.style.position='absolute';input.style.opacity='0';input.style.pointerEvents='none';input.tabIndex=-1;
    $('#pin-dots').hidden=false;$('#pin-pad').hidden=false;$('#pin-submit').hidden=true;
    var paint=function(){dots.forEach(function(d,i){d.style.background=i<input.value.length?'var(--primary)':'transparent';});};
    var type=function(k){if(sent)return;if(k==='back')input.value=input.value.slice(0,-1);else if(input.value.length<4)input.value+=k;paint();
      if(input.value.length===4){sent=true;setTimeout(function(){pf.submit();},120);}};
    $$('[data-k]').forEach(function(b){b.addEventListener('click',function(e){e.__h=true;type(b.dataset.k);});});
    document.addEventListener('keydown',function(e){if(e.target.closest&&e.target.closest('#exit-password'))return;
      if(/^\d$/.test(e.key))type(e.key);else if(e.key==='Backspace')type('back');});
    paint();
  }
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
        if(s.badges&&s.badges.length)UI.toast('Huy hiệu mới: '+s.badges.join(', ')+'!');
        if(s.left===5)UI.toast('Còn 5 phút nữa là hết giờ học nhé!');
        if(!s.allowed)location.href='/kids-time-up';}).catch(function(){});},30000);
}

/* ---------- quiz (SQL): one question at a time, countdown, study-time heartbeat; the server scores ---------- */
if(P==='kids-quiz-sql'){
  var m=$('main'),form=$('#quiz-form'),qs=$$('[data-q]',form),cur=0,sent=false,attempt=m.dataset.attempt;
  var prev=$('[data-prev]'),next=$('[data-next]'),send=$('[data-submit]'),step=$('[data-step]'),bar=$('[data-progress]'),note=$('[data-unanswered]');
  var dots=$$('[data-goto]');
  function answered(f){return !!f.querySelector('input:checked');}
  /* No automatic jump after an answer: a child who also pressed "next" used to skip a question. The dots show which
     questions are answered, and "next" lights up once this one is. */
  function paintState(){var done=qs.filter(answered).length;
    bar.querySelector('i').style.width=Math.round(done*100/qs.length)+'%';
    dots.forEach(function(d,k){var a=answered(qs[k]);d.classList.toggle('done',a);d.classList.toggle('on',k===cur);
      d.setAttribute('aria-label','Câu '+(k+1)+(a?', đã trả lời':', chưa trả lời'));if(k===cur)d.setAttribute('aria-current','step');else d.removeAttribute('aria-current');});
    next.classList.toggle('ready',answered(qs[cur]));}
  function show(i){cur=i;qs.forEach(function(f,k){f.hidden=k!==i;});
    prev.hidden=i===0;next.hidden=i>=qs.length-1;send.hidden=i<qs.length-1;
    if(step)step.textContent='Câu '+(i+1)+' / '+qs.length;
    paintState();
    var first=qs[i].querySelector('input:checked')||qs[i].querySelector('input');if(first&&document.activeElement!==document.body)first.focus();}
  function submitNow(){if(sent)return;sent=true;form.submit();}
  if(qs.length>1){bar.hidden=false;show(0);
    prev.addEventListener('click',function(){show(Math.max(0,cur-1));});
    next.addEventListener('click',function(){show(Math.min(qs.length-1,cur+1));});
    dots.forEach(function(d){d.addEventListener('click',function(){show(+d.dataset.goto);});});
    form.addEventListener('change',paintState);}
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
        if(s.badges&&s.badges.length)UI.toast('Huy hiệu mới: '+s.badges.join(', ')+'!');
        if(s.left===5)UI.toast('Còn 5 phút nữa là hết giờ học nhé!');
        if(!s.allowed){UI.toast('Hết giờ học hôm nay, bài làm được nộp luôn.');submitNow();}}).catch(function(){});},30000);
}

});
})();
