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
function paintTop(){var c=kc();if(!c)return;var y=$('.k-top .k-pill.y');if(y)y.innerHTML='<i class="ic ic-clock"></i>Còn '+S.remaining(c)+' phút';
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
  row.innerHTML=cs.map(function(c){return '<a class="k-card" href="/kids-home" data-c="'+c.id+'" style="width:230px;text-align:center;display:flex;flex-direction:column;align-items:center;gap:14px"><span class="av" style="width:112px;height:112px;font-size:44px;background:linear-gradient(135deg,'+c.c[0]+','+c.c[1]+')">'+esc(c.init)+'</span><div class="kf" style="font-size:28px">'+esc(c.name)+'</div><span class="k-note">Lớp '+c.cls+' · còn '+S.remaining(c)+' phút</span></a>';}).join('');
  row.addEventListener('click',function(e){var a=e.target.closest('[data-c]');if(!a)return;S.set(function(s){s.kidsChild=a.dataset.c;});});
  var par=$('main > a.sl-btn');par.href='/pin?exit';par.innerHTML='<i class="ic ic-lock"></i>Dành cho ba mẹ (cần PIN)';
  S.set(function(s){s.kidsChild=null;});
}

/* ---------- home ---------- */
if(P==='kids-home'){
  var c=kc(),asg=(S.get().assigned[c.id]||[]).map(S.lesson).filter(Boolean);
  var next=asg.filter(function(l){return !S.prog(c.id,l.id).done;})[0]||asg[0];
  var hero=$('.k-card.grow,.k-card[style*="var(--grad)"]');
  if(hero){hero.querySelector('.kf').innerHTML='Chào '+esc(c.name)+'!<br>'+(next?'Học tiếp nhé':'Hôm nay con nghỉ nhé');
    hero.querySelector('.chip').innerHTML='<i class="ic ic-flame"></i>'+(S.get().streak[c.id]||0)+' ngày liên tiếp';
    var info=hero.querySelector('div[style*="opacity:.92"]');if(info)info.textContent=next?next.title+' · '+(S.prog(c.id,next.id).pct?'đã học '+S.prog(c.id,next.id).pct+'%':S.SUBJECTS[next.subj].name):'Ba mẹ chưa giao bài mới';
    var b=hero.querySelector('.k-btn');if(next)b.href=href(next);else b.classList.add('hidden');}
  var dn=$('.donut'),pct=Math.min(100,Math.round(c.used*100/(c.limit+c.extra)));if(dn){dn.style.background='conic-gradient(#FFB020 0 '+pct+'%,var(--surface-2) '+pct+'% 100%)';dn.querySelector('b').innerHTML=c.used+'<small>phút</small>';var ss=dn.parentNode.querySelector('.k-note');ss.textContent='Còn '+S.remaining(c)+' phút';}
  var grid=$$('main > div')[1].querySelector('div[style*="gap:18px"]');
  grid.innerHTML=c.subjects.map(function(k){var sj=S.SUBJECTS[k],st=subjStat(c,k);return '<a class="k-card" href="/kids-subject?s='+k+'" style="flex:1 1 200px;display:flex;flex-direction:column;gap:12px;background:linear-gradient(135deg,'+sj.c[0]+','+sj.c[1]+');color:#fff;border-color:transparent"><i class="ic ic-'+sj.icon+' ic-xl"></i><div class="kf" style="font-size:26px">'+sj.name+'</div><div style="font-weight:700;font-size:14px;opacity:.95">'+st.d+'/'+st.t+' bài</div><div style="height:8px;border-radius:4px;background:rgba(255,255,255,.3)"><i style="display:block;height:8px;border-radius:4px;background:#fff;width:'+Math.round(st.d*100/st.t)+'%"></i></div></a>';}).join('');
  var lst=$$('main > div')[2].querySelector('div[style*="gap:18px"]');
  lst.innerHTML=asg.length?asg.map(function(l){var sj=S.SUBJECTS[l.subj],d=S.prog(c.id,l.id).done;return '<a class="k-card" href="'+href(l)+'" style="flex:1 1 260px;display:flex;gap:14px;align-items:center">'+thumb(l.c,kind(l.kind),88,70)+'<div><div class="b" style="font-size:16px">'+esc(l.title)+'</div><span class="k-note">'+sj.name+' · '+l.mins+' phút'+(d?' · Đã xong':'')+'</span></div></a>';}).join(''):'<div class="empty" style="flex:1">Ba mẹ chưa giao bài nào. Nhờ ba mẹ chọn bài nhé!</div>';
}

/* ---------- subject ---------- */
if(P==='kids-subject'){
  var c=kc(),k=q.get('s')||'toan',sj=S.SUBJECTS[k]||S.SUBJECTS.toan,ls=S.LESSONS.filter(function(l){return l.subj===k;}),st=subjStat(c,k);
  $('.kf').textContent=sj.name+' · Lớp '+c.cls;$('.k-pill.p:not(.k-top *)')&&0;
  $$('main .k-pill.p')[0].textContent=st.d+'/'+st.t+' bài';
  $('.bar.bar-l i').style.width=Math.round(st.d*100/st.t)+'%';
  var cur=-1;ls.forEach(function(l,i){if(cur<0&&!S.prog(c.id,l.id).done&&!S.locked(c.id,l.id))cur=i;});
  $('main > div:last-child').innerHTML=ls.map(function(l,i){var lk=S.locked(c.id,l.id),d=S.prog(c.id,l.id).done,now=i===cur;
    var inner='<span class="av" style="width:56px;height:56px;background:'+(d?'var(--green)':now?'var(--primary)':lk?'var(--surface-2)':'var(--primary-soft)')+';color:'+(d||now?'#fff':'var(--text-3)')+'"><i class="ic ic-'+(d?'check':lk?'lock':kind(l.kind))+'"></i></span><div class="grow"><div class="b" style="font-size:17px">'+esc(l.title)+'</div><span class="k-note">'+l.kind+' · '+l.mins+' phút</span></div>';
    return lk?'<div class="k-card" aria-disabled="true" style="display:flex;gap:14px;align-items:center;opacity:.55">'+inner+'</div>':'<a class="k-card" href="'+href(l)+'" style="display:flex;gap:14px;align-items:center">'+inner+'</a>';}).join('');
}

/* ---------- lesson (video) ---------- */
if(P==='kids-lesson'){
  var c=kc(),l=S.lesson(q.get('id')||'nhan5')||S.lesson('nhan5'),pr=S.prog(c.id,l.id),pct=pr.done?100:(pr.pct||0),playing=false;
  $('.kf').textContent=l.title;$('.k-pill:not(.k-top *)');
  var back=$('main .k-pill');back.href='/kids-subject?s='+l.subj;back.innerHTML='<i class="ic ic-arrowl"></i>'+S.SUBJECTS[l.subj].name;
  var player=$('div[style*="aspect-ratio"]');player.style.background='linear-gradient(135deg,'+l.c[0]+','+l.c[1]+')';player.setAttribute('role','button');player.tabIndex=0;player.setAttribute('aria-label','Phát hoặc tạm dừng video');
  var playBtn=player.querySelector('span'),bar=player.querySelector('div[style*="bottom"]');
  bar.innerHTML='<i class="ic ic-play" id="pi"></i><div style="flex:1;height:8px;border-radius:4px;background:rgba(255,255,255,.3)"><i id="pb" style="display:block;height:8px;border-radius:4px;background:#FFC93C;width:'+pct+'%"></i></div><span id="pt" style="font-weight:700;font-size:13px"></span>';
  function paintP(){var tot=l.mins*60,cur=Math.round(tot*pct/100);$('#pb').style.width=pct+'%';$('#pt').textContent=Math.floor(cur/60)+':'+('0'+cur%60).slice(-2)+' / '+l.mins+':00';$('#pi').className='ic ic-'+(playing?'pause':'play');playBtn.style.display=playing?'none':'flex';}
  function save(){S.set(function(s){var p=s.progress[c.id]=s.progress[c.id]||{};var q2=p[l.id]=p[l.id]||{};if(!q2.done)q2.pct=Math.min(99,Math.round(pct));});}
  function toggle(){if(S.remaining(c)<=0){location.href='/kids-time-up';return;}playing=!playing;if(playing)startTimer(function(){pct=Math.min(100,pct+100/l.mins);save();paintP();if(pct>=100){playing=false;stopTimer();paintP();var f=markDone(l.id);UI.toast(f?'Xem xong rồi! +10 sao':'Xem xong rồi!');paintList();}});else{stopTimer();save();}paintP();}
  player.addEventListener('click',toggle);player.addEventListener('keydown',function(e){if(e.key==='Enter'||e.key===' '){e.preventDefault();toggle();}});
  var note=document.createElement('p');note.className='k-note';note.style.marginTop='8px';note.textContent='Bản demo: 1 phút học tính bằng '+(TICK/1000)+' giây thật.';player.parentNode.appendChild(note);
  var card=$$('main .k-card').filter(function(x){return /Trong bài này/.test(x.textContent);})[0];
  function paintList(){var d=S.prog(c.id,l.id).done;card.innerHTML='<div class="kf" style="font-size:22px;margin-bottom:10px">Trong bài này</div>'+[['playc','p','Xem video',null,d],['book','t','Đọc ghi nhớ','/kids-reading?id='+l.id,0],['target','a','Làm quiz','/kids-quiz?id='+l.id,(S.prog(c.id,l.id).score!=null)]].map(function(r){var inner='<span class="ib t-'+r[1]+'"><i class="ic ic-'+r[0]+'"></i></span><div class="grow b">'+r[2]+'</div><i class="ic ic-'+(r[4]?'checkc':'chevr')+'" style="color:var(--'+(r[4]?'green':'text-3')+')"></i>';return r[3]?'<a class="sl-row" href="'+r[3]+'" style="gap:12px;padding:10px 0;border-top:1px solid var(--line)">'+inner+'</a>':'<div class="sl-row" style="gap:12px;padding:10px 0;border-top:1px solid var(--line)">'+inner+'</div>';}).join('');}
  paintList();
  var qz=$('a.k-btn');qz.href='/kids-quiz?id='+l.id;
  var more=$$('main a.sl-btn').filter(function(x){return /thêm giờ/.test(x.textContent);})[0];if(more)more.href='/kids-time-up';
  UI.on(UI.byText('main button','Video này có gì lạ'),function(){reportModal(l.id);});
  paintP();
}

/* ---------- reading ---------- */
if(P==='kids-reading'){
  var c=kc(),l=S.lesson(q.get('id')||'nhan5')||S.lesson('nhan5');
  var back=$('main .k-pill');back.href='/kids-subject?s='+l.subj;back.innerHTML='<i class="ic ic-arrowl"></i>'+S.SUBJECTS[l.subj].name;
  if(l.id!=='nhan5'){$('article h1').textContent=l.title;$$('article p').forEach(function(p,i){p.innerHTML=i===0?'Hôm nay con sẽ học bài <b>'+esc(l.title)+'</b>. Hãy đọc thật chậm và tìm ý chính nhé.':'Sau khi đọc xong, con làm quiz nhỏ để nhớ bài lâu hơn.';});var g=$('article > div');if(g)g.remove();}
  var qz=$('a.k-btn');qz.href='/kids-quiz?id='+l.id;
  var done=document.createElement('button');done.type='button';done.className='sl-btn btn-lg btn-block';done.innerHTML='<i class="ic ic-checkc"></i>Con đọc xong rồi';done.addEventListener('click',function(){var f=markDone(l.id);UI.toast(f?'Giỏi lắm! +10 sao':'Đã ghi nhận');done.disabled=true;});qz.parentNode.appendChild(done);
  startTimer();
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

/* ---------- search ---------- */
if(P==='kids-search'){
  var c=kc(),bar=$('main > div.sl-row'),box=bar.querySelector('.k-card');
  box.innerHTML='<i class="ic ic-search"></i><input id="ks" type="search" aria-label="Tìm bài học" autocomplete="off" placeholder="Con muốn học gì?" style="all:unset;flex:1;font-size:20px;font-weight:700;color:var(--text)">';
  var inp=$('#ks'),res=$$('main > *')[2],cnt=$('.k-note'),empty=$('.empty');
  var grid=$('main > div[style*="grid-template-columns"]');
  function draw(){var v=UI.vn(inp.value.trim()),ok=c.search!==false,pool=(S.get().assigned[c.id]||[]).map(S.lesson).filter(Boolean);
    var rs=ok?pool.filter(function(l){return !v||UI.vn(l.title).indexOf(v)>-1||UI.vn(S.SUBJECTS[l.subj].name).indexOf(v)>-1;}):[];
    cnt.textContent=ok?rs.length+' bài ba mẹ cho con học':'Ba mẹ đã tắt tìm kiếm';
    grid.innerHTML=rs.map(function(l){return '<a class="k-card" href="'+href(l)+'" style="display:flex;gap:14px;align-items:center">'+thumb(l.c,kind(l.kind),88,70)+'<div><div class="b" style="font-size:16px">'+esc(l.title)+'</div><span class="k-note">'+S.SUBJECTS[l.subj].name+' · '+l.mins+' phút</span></div></a>';}).join('');
    empty.classList.toggle('hidden',rs.length>0&&true);}
  inp.addEventListener('input',draw);draw();inp.focus();
}

/* ---------- time up ---------- */
if(P==='kids-time-up'){
  var c=kc(),rem=S.remaining(c),h=$('.kf'),p=$('main p'),btn=UI.byText('button','Xin ba mẹ thêm giờ'),ok=$('.banner.t-g');
  ok.classList.add('hidden');
  if(rem>0){h.textContent='Con muốn học thêm?';p.textContent='Hôm nay con còn '+rem+' phút. Nếu cần thêm, con xin ba mẹ nhé.';}
  else p.textContent='Con đã học '+(c.limit+c.extra)+' phút, giỏi lắm. Nghỉ ngơi, chơi và mai học tiếp nhé.';
  function pending(){return S.pendingReqs().filter(function(r){return r.cid===c.id;})[0];}
  function paint(){if(!btn)return;var r=pending();c=kc();
    if(r){btn.disabled=true;btn.innerHTML='<i class="ic ic-clock"></i>Đang chờ ba mẹ...';ok.classList.remove('hidden');ok.innerHTML='<i class="ic ic-checkc"></i>Đã gửi cho ba mẹ. Đợi ba mẹ trả lời nhé!';}
    else{var last=S.get().reqs.filter(function(z){return z.cid===c.id;}).slice(-1)[0];btn.disabled=false;btn.innerHTML='<i class="ic ic-send"></i>Xin ba mẹ thêm giờ';
      if(last&&last.status==='approved'&&S.remaining(c)>0){ok.classList.remove('hidden');ok.innerHTML='<i class="ic ic-checkc"></i>Ba mẹ cho con thêm '+last.mins+' phút rồi!';h.textContent='Học tiếp nào!';p.textContent='Con còn '+S.remaining(c)+' phút.';btn.outerHTML='<a class="k-btn" href="/kids-home">Về trang học</a>';btn=null;}
      else if(last&&last.status==='rejected'){ok.classList.remove('hidden');ok.className='banner t-a';ok.innerHTML='<i class="ic ic-info"></i>Ba mẹ muốn con nghỉ ngơi. Hẹn con ngày mai nhé!';}}}
  if(btn)btn.addEventListener('click',function(){if(pending())return;var d=new Date();S.set(function(s){s.reqs.push({id:'q'+Date.now(),cid:c.id,mins:15,note:'Con muốn học nốt bài ạ',time:('0'+d.getHours()).slice(-2)+':'+('0'+d.getMinutes()).slice(-2),status:'pending'});});S.notify({ic:'clock',tone:'a',t:c.name+' xin thêm 15 phút',s:'Chờ ba mẹ duyệt'});paint();});
  document.addEventListener('kl:change',paint);paint();
}

/* ---------- badges ---------- */
if(P==='kids-badges'){
  var c=kc(),own=S.get().badges[c.id]||[],stars=S.get().stars[c.id]||0;
  var pills=$$('main .k-pill');pills[1].innerHTML='<i class="ic ic-star"></i>'+stars+' sao';pills[2].textContent=own.length+' / '+S.BADGES.length;
  $('main > div:last-child').innerHTML=S.BADGES.map(function(b){var on=own.indexOf(b.id)>-1;return '<div class="k-card" style="flex:1 1 200px;text-align:center;display:flex;flex-direction:column;align-items:center;gap:10px;'+(on?'':'opacity:.55')+'"><span style="width:84px;height:84px;border-radius:42px;background:'+(on?'linear-gradient(135deg,'+b.g[0]+','+b.g[1]+')':'var(--surface-2)')+';color:'+(on?'#fff':'var(--text-3)')+';display:flex;align-items:center;justify-content:center"><i class="ic ic-'+(on?b.icon:'lock')+' ic-xl"></i></span><div class="b" style="font-size:16px">'+b.name+'</div><span class="k-note">'+(on?'Đã đạt · ':'Cách đạt: ')+b.d+'</span></div>';}).join('');
}
});
})();
