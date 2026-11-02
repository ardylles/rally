import assert from 'node:assert/strict';
import {writeFile} from 'node:fs/promises';
const base=process.env.RALLY_URL||'http://127.0.0.1:5080';let passed=0;
function check(value,label){assert.ok(value,label);passed++;console.log('PASS:',label)}
class Client{
 cookie='';
 async request(path,method='GET',body,status=200){
  const r=await fetch(base+'/api'+path,{method,headers:{'Content-Type':'application/json','X-Rally-Request':'1',...(this.cookie?{Cookie:this.cookie}:{})},body:body===undefined?undefined:JSON.stringify(body)});
  const set=r.headers.getSetCookie();if(set.length)this.cookie=set.map(x=>x.split(';')[0]).join('; ');
  const text=await r.text();let data;try{data=JSON.parse(text)}catch{data=text}
  assert.equal(r.status,status,method+' '+path+': '+JSON.stringify(data).slice(0,400));return data;
 }
}
const owner=new Client(),member=new Client(),stranger=new Client(),anonymous=new Client();
await anonymous.request('/clubs','GET',undefined,401);check(true,'unauthenticated access denied');
const suffix=Date.now(),password='Rally-test-'+suffix+'-strong';
const u=await owner.request('/auth/register','POST',{name:'Integration organizer',email:'owner-'+suffix+'@example.test',password});
const v=await member.request('/auth/register','POST',{name:'Integration member',email:'member-'+suffix+'@example.test',password});
await stranger.request('/auth/register','POST',{name:'Other club',email:'other-'+suffix+'@example.test',password});
const club=await owner.request('/clubs','POST',{name:'Verification '+suffix});
await owner.request('/clubs/'+club.id+'/members','POST',{email:v.email,role:'member'},204);
await stranger.request('/clubs/'+club.id+'/meets','GET',undefined,403);check(true,'club isolation');
function input(mode='mixed'){const players=Array.from({length:10},(_,i)=>({id:'p'+i,name:'Player '+i,skill:3+(i%3)*.5}));return {name:'Verification '+mode,venue:'Test court',date:'2026-11-01',time:'16:00',timezone:'Asia/Jakarta',durationMinutes:120,courts:2,rounds:5,mode,format:'4',timing:true,players,fixedPairs:mode==='mixed'?[['p0','p1']]:mode==='fixed'?Array.from({length:5},(_,i)=>['p'+i*2,'p'+(i*2+1)]):[]}}
await member.request('/clubs/'+club.id+'/reclub/preview','POST',{url:'https://reclub.co/m/PATO94'},403);check(true,'Reclub preview requires club organizer');
await owner.request('/clubs/'+club.id+'/reclub/preview','POST',{url:'https://example.com/m/PATO94'},400);check(true,'Reclub preview rejects other hosts');
const booking={...input(),time:'18:00',durationMinutes:180,courts:3,rounds:9,slotMinutes:20,courtAvailability:[{name:'Court A',start:'18:00',end:'21:00'},{name:'Court B',start:'18:00',end:'21:00'},{name:'Court C',start:'18:00',end:'20:00'}]};
const booked=await owner.request('/clubs/'+club.id+'/meets','POST',booking);
const restored=await owner.request('/meets/'+booked.id);
check(restored.courtAvailability.length===3&&restored.slotMinutes===20&&restored.matches.every(x=>x.court!==3||x.plannedEnd<='20:00'),'court bookings and planned times persist');
await owner.request('/clubs/'+club.id+'/meets','POST',{...booking,slotMinutes:30},400);check(true,'API rejects rounds beyond booking session');
if(process.env.RALLY_CHECK_RECLUB==='1'){
 const preview=await owner.request('/clubs/'+club.id+'/reclub/preview','POST',{url:'https://reclub.co/m/PATO94'});
 check(preview.players.length>0&&preview.players.every(p=>p.name&&p.id),'live Reclub page preview');
 console.log('Reclub preview:',preview.players.length,'entries');
}
let m=await owner.request('/clubs/'+club.id+'/meets','POST',input());
check(m.matches.length>0&&m.revision===1,'meet and schedule persisted');
await member.request('/meets/'+m.id+'/generate','POST',{revision:m.revision},403);check(true,'member cannot mutate unassigned meet');
await owner.request('/meets/'+m.id+'/organizers','PUT',{userIds:[v.id]},204);
m=await member.request('/meets/'+m.id+'/generate','POST',{revision:m.revision});check(m.revision===2,'assigned meet organizer can manage');
let second=await owner.request('/clubs/'+club.id+'/meets','POST',input('rotating'));
await member.request('/meets/'+second.id+'/generate','POST',{revision:second.revision},403);check(true,'meet role scoped to assigned meet');
await owner.request('/clubs/'+club.id+'/roles','PUT',{userId:u.id,role:'member'},400);check(true,'last club organizer protected');
await owner.request('/clubs/'+club.id+'/roles','PUT',{userId:v.id,role:'organizer'},204);
await member.request('/clubs/'+club.id+'/meets','POST',input('fixed'));check(true,'multiple club organizers supported');
await owner.request('/clubs/'+club.id+'/roles','PUT',{userId:v.id,role:'member'},204);
const old=m.revision;m=await owner.request('/meets/'+m.id+'/generate','POST',{revision:old});
await owner.request('/meets/'+m.id+'/generate','POST',{revision:old},409);check(true,'stale revision rejected');
const [a,b]=m.matches.filter(x=>x.round===0);const incoming=b.teams[0][0],outgoing=a.teams[0][0];
m=await owner.request('/meets/'+m.id+'/matches/'+b.id+'/start','POST',{revision:m.revision});
m=await owner.request('/meets/'+m.id+'/matches/'+a.id+'/substitute','POST',{revision:m.revision,outgoingId:outgoing,incomingId:incoming});
check(m.matches.find(x=>x.id===a.id).teams.flat().includes(incoming),'busy registered player may substitute');
await owner.request('/meets/'+m.id+'/matches/'+a.id+'/start','POST',{revision:m.revision},409);check(true,'simultaneous live play blocked');
await owner.request('/meets/'+m.id+'/matches/'+a.id+'/substitute','POST',{revision:m.revision,outgoingId:incoming,incomingId:'external'},400);check(true,'unregistered match substitute rejected');
m=await owner.request('/meets/'+m.id+'/matches/'+b.id+'/result','PUT',{revision:m.revision,score:[4,2],stats:{[incoming]:{winners:2,unforcedErrors:5}}});
check(m.matches.find(x=>x.id===b.id).stats[incoming].padelRatio===-3,'negative ratio persisted');
const saved=await owner.request('/meets/'+m.id);
check(saved.matches.find(x=>x.id===b.id).status==='done','fresh read restores saved result');
const replacement=await owner.request('/meets/'+second.id+'/replacements','POST',{revision:second.revision,outgoingId:second.matches[0].teams[0][0],name:'New replacement',scope:'one',skill:null});
check(replacement.players.length===11&&replacement.matches.filter(x=>x.teams.flat().includes(replacement.players.at(-1).id)).length===1,'new roster replacement for one match');
let balanced=await owner.request('/clubs/'+club.id+'/meets','POST',input('balanced'));
balanced=await owner.request('/meets/'+balanced.id+'/skills','PUT',{revision:balanced.revision,playerId:'p0',skill:4.5});
check(balanced.players[0].skill===4.5,'editable balanced skill persisted');
await owner.request('/meets/'+m.id+'/skills','PUT',{revision:m.revision,playerId:'p0',skill:4},400);check(true,'skill edit rejected outside balance mode');
const snapshot=await owner.request('/meets/'+balanced.id);const race=await Promise.allSettled([owner.request('/meets/'+balanced.id+'/skills','PUT',{revision:snapshot.revision,playerId:'p1',skill:4}),owner.request('/meets/'+balanced.id+'/skills','PUT',{revision:snapshot.revision,playerId:'p2',skill:4})]);assert.equal(race.filter(r=>r.status==='fulfilled').length,1);assert.ok(race.some(r=>r.status==='rejected'&&r.reason.message.includes('409')));
// Separate stale-edit assertion above is deterministic; concurrency may commit either writer.
const afterRace=await owner.request('/meets/'+balanced.id);check(afterRace.revision===snapshot.revision+1,'concurrent edits commit only once');
await owner.request('/meets/'+m.id+'/organizers','PUT',{userIds:[]},204);
await member.request('/meets/'+m.id+'/generate','POST',{revision:m.revision},403);check(true,'revoked meet role no longer authorizes');
let depart=await owner.request('/clubs/'+club.id+'/meets','POST',input('rotating'));
await member.request('/meets/'+depart.id+'/departures','POST',{revision:depart.revision,playerId:'p0',fromRound:0},403);
await member.request('/meets/'+depart.id+'/regenerate-remaining','POST',{revision:depart.revision},403);
check(true,'departure and remaining regeneration require meet authorization');
const [historyMatch,liveMatch]=depart.matches.filter(x=>x.round===0);
depart=await owner.request('/meets/'+depart.id+'/matches/'+historyMatch.id+'/result','PUT',{revision:depart.revision,score:[4,2],stats:{[historyMatch.teams[0][0]]:{winners:3,unforcedErrors:1}}});
depart=await owner.request('/meets/'+depart.id+'/matches/'+liveMatch.id+'/start','POST',{revision:depart.revision});
const preserved=depart.matches.filter(x=>x.status!=='ready');
const leaving=liveMatch.teams[0][0],beforeDeparture=depart.revision;
await owner.request('/meets/'+depart.id+'/organizers','PUT',{userIds:[v.id]},204);
depart=await member.request('/meets/'+depart.id+'/departures','POST',{revision:depart.revision,playerId:leaving,fromRound:0});
check(depart.players.find(p=>p.id===leaving).unavailableFromRound===0&&depart.matches.filter(x=>x.status==='ready').every(x=>!x.teams.flat().includes(leaving)),'assigned organizer can save departure and rebuild remaining schedule');
check(preserved.every(old=>JSON.stringify(depart.matches.find(x=>x.id===old.id))===JSON.stringify(old)),'departure preserves results statistics and live timer exactly');
await owner.request('/meets/'+depart.id+'/regenerate-remaining','POST',{revision:beforeDeparture},409);check(true,'remaining regeneration rejects stale revisions');
depart=await owner.request('/meets/'+depart.id+'/regenerate-remaining','POST',{revision:depart.revision});
check(depart.matches.filter(x=>x.status==='ready').every(x=>!x.teams.flat().includes(leaving)),'repeat regeneration keeps departed player excluded');
const restoredDeparture=await owner.request('/meets/'+depart.id);
check(restoredDeparture.players.find(p=>p.id===leaving).unavailableFromRound===0&&restoredDeparture.matches.length===depart.matches.length,'departure and regenerated schedule persist on reload');
const departureAudit=await owner.request('/meets/'+depart.id+'/audit');
check(departureAudit.some(x=>x.action==='player-departure')&&departureAudit.some(x=>x.action==='regenerate-remaining'),'departure and regeneration are audited');
const audit=await owner.request('/meets/'+m.id+'/audit');check(audit.some(x=>x.action==='match-result')&&audit.some(x=>x.action==='substitution'),'audit trail persisted');
await owner.request('/auth/logout','POST',undefined,204);
await owner.request('/clubs','GET',undefined,401);
await owner.request('/auth/login','POST',{name:'',email:u.email,password});
check((await owner.request('/meets/'+m.id)).matches.some(x=>x.status==='done'),'sign out/in retains history');

// Attendance, strict sides and public viewing work through the same revision-protected API.
let arrival=await owner.request('/clubs/'+club.id+'/meets','POST',input('rotating'));
await member.request('/meets/'+arrival.id+'/arrivals','POST',{revision:arrival.revision,playerId:'p0',fromRound:5},403);
arrival=await owner.request('/meets/'+arrival.id+'/arrivals','POST',{revision:arrival.revision,playerId:'p0',fromRound:5});
check(arrival.matches.every(x=>!x.teams.flat().includes('p0')),'late player waiting is excluded');
arrival=await owner.request('/meets/'+arrival.id+'/arrivals','POST',{revision:arrival.revision,playerId:'p0',fromRound:2});
check(arrival.matches.filter(x=>x.round<2).every(x=>!x.teams.flat().includes('p0'))&&arrival.matches.some(x=>x.round>=2&&x.teams.flat().includes('p0')),'arrival rebuilds future games only');
arrival=await owner.request('/meets/'+arrival.id+'/sides','PUT',{revision:arrival.revision,playerId:'p0',side:'left'});
check((await owner.request('/meets/'+arrival.id)).players[0].side==='left','playing side persists');
await owner.request('/meets/'+arrival.id+'/sides','PUT',{revision:arrival.revision,playerId:'p0',side:'preference'},400);
check(true,'invalid playing side rejected');
await stranger.request('/meets/'+arrival.id+'/sharing','GET',undefined,403);
await member.request('/meets/'+arrival.id+'/sharing','PUT',{revision:arrival.revision,enabled:true},403);
check(true,'public sharing requires organizer authorization');
check(!(await owner.request('/meets/'+arrival.id+'/sharing')).path,'new meet private by default');
arrival=await owner.request('/meets/'+arrival.id+'/sharing','PUT',{revision:arrival.revision,enabled:true});
let share=await owner.request('/meets/'+arrival.id+'/sharing');
const publicPath=share.path.replace('/public/','/public/');
const exposed=await anonymous.request(publicPath);
check(exposed.matches.length===arrival.matches.length&&exposed.players.every(p=>Object.keys(p).sort().join(',')==='id,name'),'anonymous public view returns only player display identity');
check(!JSON.stringify(exposed).match(/userId|email|changes|skill|stats|clubId|revision|public_token/),'public response excludes account and management data');
await anonymous.request(publicPath,'POST',{},404);check(true,'public endpoint is read only');
arrival=await owner.request('/meets/'+arrival.id+'/sharing','PUT',{revision:arrival.revision,enabled:false});
await anonymous.request(publicPath,'GET',undefined,404);check(true,'disabling public view revokes access');
arrival=await owner.request('/meets/'+arrival.id+'/sharing','PUT',{revision:arrival.revision,enabled:true,reset:true});
share=await owner.request('/meets/'+arrival.id+'/sharing');
await anonymous.request(publicPath,'GET',undefined,404);
await anonymous.request(share.path);
check(share.path!==publicPath,'reset revokes old link and creates usable new link');
await owner.request('/admin/users','GET',undefined,403);await owner.request('/admin/meets/'+arrival.id,'DELETE',undefined,403);
check(true,'club organizer has no application administrator privilege');

if(process.env.RALLY_TEST_ADMIN_DLL){
 // Elevated fixtures are allowed only against the dedicated local test database.
 assert.equal(new URL(base).hostname,'127.0.0.1');
 assert.match(process.env.ConnectionStrings__Rally||'',/Database=rally_features_20261102(?:;|$)/);
 const {execFileSync}=await import('node:child_process');
 const admin=new Client(),disabledClient=new Client();
 const au=await admin.request('/auth/register','POST',{name:'Test administrator',email:'admin-'+suffix+'@example.test',password});
 execFileSync('dotnet',[process.env.RALLY_TEST_ADMIN_DLL,'--bootstrap-admin',au.email],{stdio:'pipe',env:process.env});
 check((await admin.request('/auth/me')).isAdmin===true,'explicit bootstrap assigns existing account');
 await admin.request('/admin/users/'+au.id,'PUT',{disabled:true},400);
 await admin.request('/admin/users/'+au.id,'DELETE',undefined,400);
 check(true,'last active application admin protected');
 const du=await disabledClient.request('/auth/register','POST',{name:'Retained participant',email:'disabled-'+suffix+'@example.test',password});
 const disabledCookie=disabledClient.cookie;
 await admin.request('/admin/users/'+du.id,'PUT',{disabled:true},204);
 await disabledClient.request('/auth/me','GET',undefined,401);
 await disabledClient.request('/auth/login','POST',{email:du.email,password},401);
 await admin.request('/admin/users/'+du.id,'PUT',{disabled:false},204);
 disabledClient.cookie=disabledCookie;
 await disabledClient.request('/auth/me','GET',undefined,401);
 await disabledClient.request('/auth/login','POST',{email:du.email,password});
 check(true,'disable blocks login and invalidates old sessions even after re-enable');
 const ownedClub=await disabledClient.request('/clubs','POST',{name:'Retained history'});
 let owned=await disabledClient.request('/clubs/'+ownedClub.id+'/meets','POST',input('rotating'));
 const first=owned.matches[0],participant=first.teams[0][0];
 owned=await disabledClient.request('/meets/'+owned.id+'/matches/'+first.id+'/result','PUT',{revision:owned.revision,score:[4,1],stats:{[participant]:{winners:7,unforcedErrors:2}}});
 await disabledClient.request('/clubs/'+ownedClub.id+'/members','POST',{email:au.email,role:'organizer'},204);
 // Seed an account link (account linking has no public endpoint yet).
 execFileSync('C:/Program Files/PostgreSQL/17/bin/psql.exe',['-h','127.0.0.1','-p','55432','-U','rally','-d','rally_features_20261102','-v','ON_ERROR_STOP=1','-c',"UPDATE meets SET document=jsonb_set(document,'{players,0,userId}',to_jsonb('"+du.id+"'::text)) WHERE id='"+owned.id+"'"],{env:{...process.env,PGPASSWORD:process.env.Database__Password},stdio:'pipe'});
 const deleteCookie=disabledClient.cookie;
 await admin.request('/admin/users/'+du.id,'DELETE',undefined,204);
 disabledClient.cookie=deleteCookie;await disabledClient.request('/auth/me','GET',undefined,401);
 const retained=await admin.request('/meets/'+owned.id);
 check(retained.players[0].userId===null&&retained.players.every((p,i)=>p.name===owned.players[i].name)&&JSON.stringify(retained.matches)===JSON.stringify(owned.matches),'account deletion detaches links and preserves participant names matches and statistics');
 const oldAudit=await admin.request('/meets/'+owned.id+'/audit');
 check(oldAudit.some(a=>a.actor==='Retained participant'),'deleted account audit retains actor display name');
 await admin.request('/admin/meets/'+arrival.id,'DELETE',undefined,204);
 await anonymous.request(share.path,'GET',undefined,404);
 await owner.request('/meets/'+arrival.id,'GET',undefined,404);
 check(true,'meet deletion removes meet and public access');
 const adminAudit=await admin.request('/admin/audit');
 check(['disable-user','enable-user','delete-user','delete-meet'].every(action=>adminAudit.some(a=>a.action===action)),'administrative actions recorded');
}

await writeFile(new URL('../.local/verification.json',import.meta.url),JSON.stringify({passed,meetId:m.id,clubId:club.id,at:new Date().toISOString()},null,2));
console.log('All '+passed+' API/database checks passed.');
