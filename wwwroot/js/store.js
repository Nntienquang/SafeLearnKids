/* SafeLearn Kids · data layer (mock). In MVC this maps to controllers + EF Core; shapes mirror the entities. */
(function(){
'use strict';
var KEY='kl.v1';
var SUBJECTS={
  toan:{name:'Toán',tone:'p',icon:'calc',c:['#6A4CFF','#38BDF8']},
  tiengviet:{name:'Tiếng Việt',tone:'g',icon:'pen',c:['#14B8A6','#22C55E']},
  khoahoc:{name:'Khoa học',tone:'a',icon:'flask',c:['#F59E0B','#FB7185']},
  tienganh:{name:'Tiếng Anh',tone:'r',icon:'globe',c:['#F43F5E','#FB923C']}
};
var LESSONS=[
 {id:'sove1000',title:'Số đến 1000',subj:'toan',cls:3,kind:'Video',mins:10,c:['#6A4CFF','#38BDF8']},
 {id:'congtru',title:'Cộng trừ trong 1000',subj:'toan',cls:3,kind:'Video',mins:12,c:['#4F46E5','#22B8CF']},
 {id:'nhan2',title:'Bảng nhân 2',subj:'toan',cls:3,kind:'Video',mins:8,c:['#7C3AED','#C084FC']},
 {id:'nhan5',title:'Bảng nhân 5',subj:'toan',cls:3,kind:'Video',mins:10,c:['#6A4CFF','#38BDF8']},
 {id:'phanso',title:'Phân số cơ bản',subj:'toan',cls:3,kind:'Bài đọc',mins:8,c:['#7C3AED','#C084FC']},
 {id:'hinhhoc',title:'Hình học: chu vi',subj:'toan',cls:3,kind:'Video',mins:11,c:['#0EA5E9','#6A4CFF']},
 {id:'quiznhan',title:'Quiz: bảng nhân 2 và 5',subj:'toan',cls:3,kind:'Quiz',mins:6,c:['#4F46E5','#EC4899']},
 {id:'cauke',title:'Câu kể Ai làm gì?',subj:'tiengviet',cls:3,kind:'Video',mins:12,c:['#14B8A6','#22C55E']},
 {id:'tuhd',title:'Từ chỉ hoạt động',subj:'tiengviet',cls:3,kind:'Bài đọc',mins:7,c:['#0EA5E9','#14B8A6']},
 {id:'tadovat',title:'Tập làm văn: tả đồ vật',subj:'tiengviet',cls:3,kind:'Bài đọc',mins:9,c:['#10B981','#84CC16']},
 {id:'buom',title:'Vòng đời của bướm',subj:'khoahoc',cls:3,kind:'Video',mins:9,c:['#F59E0B','#FB7185']},
 {id:'dongvat',title:'Động vật sống ở đâu?',subj:'khoahoc',cls:3,kind:'Video',mins:11,c:['#F97316','#FACC15']},
 {id:'thucvat',title:'Cây cần gì để lớn?',subj:'khoahoc',cls:3,kind:'Bài đọc',mins:7,c:['#16A34A','#A3E635']},
 {id:'greet',title:'Greetings',subj:'tienganh',cls:3,kind:'Video',mins:8,c:['#F43F5E','#FB923C']},
 {id:'colors',title:'Colors and shapes',subj:'tienganh',cls:3,kind:'Video',mins:8,c:['#EC4899','#F59E0B']},
 {id:'family',title:'My family',subj:'tienganh',cls:3,kind:'Bài đọc',mins:6,c:['#E11D48','#FB7185']}
];
var PATH={toan:['sove1000','congtru','nhan2','nhan5','phanso','hinhhoc']};
var QUIZ={
 nhan5:[{q:'5 × 3 = ?',a:['10','15','20','25'],c:1,e:'5 × 3 = 5 + 5 + 5 = 15.'},{q:'5 × 5 = ?',a:['20','25','30','35'],c:1,e:'5 × 5 = 25.'},{q:'5 × 7 = ?',a:['30','35','40','45'],c:1,e:'5 × 7 = 5 × 5 + 5 × 2 = 35.'},{q:'Kết quả của 5 × 6 tận cùng bằng chữ số nào?',a:['0','5','6','3'],c:0,e:'Số lần chẵn thì kết quả tận cùng là 0.'},{q:'5 × 9 = ?',a:['40','45','50','55'],c:1,e:'5 × 9 = 45.'}],
 phanso:[{q:'Một nửa của cái bánh viết là?',a:['1/3','1/2','2/1','1/4'],c:1,e:'Chia 2 phần bằng nhau, lấy 1 phần: 1/2.'},{q:'Phân số nào bằng 2/4?',a:['1/2','1/3','3/4','2/3'],c:0,e:'2/4 = 1/2 (cùng chia cho 2).'},{q:'Trong 3/4, mẫu số là?',a:['3','4','7','1'],c:1,e:'Số dưới gạch ngang là mẫu số.'}]
};
QUIZ.nhan2=QUIZ.quiznhan=QUIZ.nhan5;
var BADGES=[
 {id:'chamchi',name:'Siêu chăm chỉ',d:'5 ngày liên tiếp',icon:'flame',g:['#F59E0B','#FB7185']},
 {id:'toan',name:'Nhà toán học',d:'10 bài Toán',icon:'calc',g:['#6A4CFF','#38BDF8']},
 {id:'sach',name:'Mọt sách',d:'Đọc 5 bài',icon:'book',g:['#14B8A6','#22C55E']},
 {id:'diem10',name:'Điểm 10',d:'Quiz tuyệt đối',icon:'trophy',g:['#F43F5E','#FB923C']},
 {id:'khoahoc',name:'Nhà khoa học',d:'8 bài Khoa học',icon:'flask',g:['#7C3AED','#C084FC']},
 {id:'14ngay',name:'Bạn nhỏ chăm học',d:'14 ngày liên tiếp',icon:'heart',g:['#0EA5E9','#14B8A6']}
];
function now(){return new Date().toISOString();}
function seed(){return {
 v:1,theme:'light',session:null,kidsChild:null,pin:'1234',
 accounts:[],
 lock:{},
 children:[
  {id:'c1',name:'Minh An',cls:3,init:'M',c:['#6A4CFF','#FF7AB6'],limit:60,used:32,extra:0,subjects:['toan','tiengviet','khoahoc','tienganh'],search:true},
  {id:'c2',name:'Bảo Nam',cls:1,init:'N',c:['#22C55E','#38BDF8'],limit:45,used:15,extra:0,subjects:['tiengviet','toan'],search:true}],
 assigned:{c1:['nhan5','buom','quiznhan'],c2:['cauke']},
 progress:{c1:{sove1000:{done:1,score:null},congtru:{done:1,score:null},nhan2:{done:1,score:9},nhan5:{pct:62},buom:{done:1}},c2:{}},
 stars:{c1:340,c2:60},streak:{c1:6,c2:2},badges:{c1:['chamchi','toan','sach'],c2:[]},
 reqs:[],
 reports:[{id:'r1',lesson:'buom',reason:'Có quảng cáo lạ',by:'Phụ huynh · Thu Hà',sev:'Khẩn',status:'open',at:now()},{id:'r2',lesson:'greet',reason:'Âm thanh quá lớn',by:'Phụ huynh · Lan',sev:'Vừa',status:'open',at:now()},{id:'r3',lesson:'dongvat',reason:'Hình ảnh gây sợ',by:'Bé · Minh An',sev:'Vừa',status:'open',at:now()}],
 notifs:[{id:'n1',ic:'flag',tone:'r',t:'Báo cáo nội dung mới',s:'Vòng đời của bướm · có quảng cáo lạ',when:'Hôm qua',unread:1},{id:'n2',ic:'file',tone:'v',t:'Báo cáo tuần đã sẵn sàng',s:'Tuần 12 – 18/10',when:'CN',unread:1},{id:'n3',ic:'trophy',tone:'g',t:'Minh An nhận huy hiệu',s:'Siêu chăm chỉ · 5 ngày liên tiếp',when:'T6',unread:0},{id:'n4',ic:'checkc',tone:'b',t:'Nguồn mới được duyệt',s:'Tiếng Việt lớp 3 · 24 bài',when:'T5',unread:0}],
 sources:[{id:'s1',name:'Học Toán Vui',sub:'@hoctoanvui',type:'Kênh',n:128,st:'ok'},{id:'s2',name:'Tiếng Việt lớp 3',sub:'Danh sách phát · 24 bài',type:'Playlist',n:24,st:'ok'},{id:'s3',name:'Khoa học cho bé',sub:'@khoahoccho_be',type:'Kênh',n:76,st:'review'},{id:'s4',name:'Vui học tiếng Anh',sub:'@vuihoctienganh',type:'Kênh',n:54,st:'no'}],
 audit:[{t:'quantri · Kênh @toantuduy',w:'08:40',a:'Duyệt kênh Toán tư duy Kids'},{t:'hệ thống',w:'07:00',a:'Đồng bộ 4 nguồn YouTube'}],
 settings:{notify:{req:1,weekly:1,badge:0},eye:1}
};}
var state=null;
function load(){try{var r=localStorage.getItem(KEY);state=r?JSON.parse(r):seed();}catch(e){state=seed();}if(!state||state.v!==1)state=seed();}
function save(){try{localStorage.setItem(KEY,JSON.stringify(state));}catch(e){}}
function bind(){
  var u=window.SL_USER;if(!u)return;
  var th=state.theme;
  if(u.role==='parent'&&state.owner!==u.email){ /* a different parent: start from clean data unless demo account */
    state=seed();state.theme=th;state.owner=u.email;
    if(!u.demo){state.children=[];state.assigned={};state.progress={};state.stars={};state.streak={};state.badges={};state.reqs=[];state.notifs=[];state.parentChild=null;}
  }
  state.session={email:u.email,name:u.name,role:u.role};save();
}
load();
// Remove credentials persisted by earlier mock-auth versions. Identity owns all passwords.
state.accounts=[];state.lock={};if(!window.SL_USER)state.session=null;save();
bind();
var S={
 SUBJECTS:SUBJECTS,LESSONS:LESSONS,PATH:PATH,QUIZ:QUIZ,BADGES:BADGES,
 get:function(){return state;},
 set:function(fn){fn(state);save();document.dispatchEvent(new CustomEvent('kl:change'));},
 reset:function(){state=seed();save();},
 lesson:function(id){return LESSONS.filter(function(l){return l.id===id;})[0];},
 child:function(id){var cs=state.children;if(!cs.length)return null;id=id||state.kidsChild||state.parentChild||cs[0].id;return cs.filter(function(c){return c.id===id;})[0]||cs[0];},
 remaining:function(c){return Math.max(0,c.limit+c.extra-c.used);},
 prog:function(cid,lid){return (state.progress[cid]||{})[lid]||{};},
 locked:function(cid,lid){
   var l=S.lesson(lid),p=PATH[l.subj];if(!p)return false;var i=p.indexOf(lid);if(i<=0)return false;
   return !S.prog(cid,p[i-1]).done;},
 assign:function(cid,lid){S.set(function(s){s.assigned[cid]=s.assigned[cid]||[];if(s.assigned[cid].indexOf(lid)<0)s.assigned[cid].push(lid);});},
 unassign:function(cid,lid){S.set(function(s){s.assigned[cid]=(s.assigned[cid]||[]).filter(function(x){return x!==lid;});});},
 notify:function(n){S.set(function(s){n.id='n'+Date.now()+Math.random().toString(16).slice(2,5);n.unread=1;n.when='Vừa xong';s.notifs.unshift(n);});},
 audit:function(t,a){S.set(function(s){var d=new Date();s.audit.unshift({t:t,a:a,w:('0'+d.getHours()).slice(-2)+':'+('0'+d.getMinutes()).slice(-2)});});},
 addBadge:function(cid,bid){var added=false;S.set(function(s){s.badges[cid]=s.badges[cid]||[];if(s.badges[cid].indexOf(bid)<0){s.badges[cid].push(bid);added=true;}});return added;},
 pendingReqs:function(){return state.reqs.filter(function(r){return r.status==='pending';});},
 esc:function(x){return String(x==null?'':x).replace(/[&<>"']/g,function(m){return {'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[m];});}
};
window.addEventListener('storage',function(e){if(e.key===KEY){load();document.dispatchEvent(new CustomEvent('kl:change'));}});
window.KL=S;
})();
