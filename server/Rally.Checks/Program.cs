using Rally;
using Microsoft.Extensions.Configuration;
using System.Text.Json;
int checks=0;
void Check(bool condition,string name){if(!condition)throw new Exception("FAIL: "+name);checks++;Console.WriteLine("PASS: "+name);}
void Reject(Action action,string name){try{action();throw new Exception("Not rejected: "+name);}catch(RuleException){checks++;Console.WriteLine("PASS: "+name);}}
Meet Example(int n,int courts,int rounds,string mode,int pairs=1){var m=new Meet{Name="Test",Venue="Test venue",Date="2026-11-01",Mode=mode,Courts=courts,Rounds=rounds,Players=Enumerable.Range(0,n).Select(i=>new Player{Id="p"+i,Name="Player "+i,Skill=2.5+(i%4)*.5}).ToList()};if(mode is "fixed" or "mixed")m.FixedPairs=Enumerable.Range(0,mode=="fixed"?n/2:pairs).Select(i=>new[]{"p"+(i*2),"p"+(i*2+1)}).ToList();return m;}
foreach(var mode in new[]{"rotating","balanced","fixed","mixed"}) {
 var m=Example(10,2,5,mode);Scheduler.Generate(m);
 Check(m.Matches.GroupBy(x=>x.Round).All(g=>g.SelectMany(x=>x.Teams.SelectMany(t=>t)).Distinct().Count()==g.Count()*4),mode+" no double bookings");
 Check(m.Matches.All(x=>x.Teams.Length==2&&x.Teams.All(t=>t.Length==2)),mode+" four players per match");
 if(mode is "fixed" or "mixed")Check(m.Matches.All(x=>x.Teams.All(t=>m.FixedPairs.All(p=>!t.Any(p.Contains)||p.All(t.Contains)))),mode+" fixed pair integrity");
 var count=m.Players.Select(p=>m.Matches.Count(x=>x.Teams.SelectMany(t=>t).Contains(p.Id))).ToList();
 Check(count.Max()-count.Min()<=1,mode+" fair participation");
 var first=m.Matches[0];var other=m.Matches[1];MeetRules.Start(m,other);
 var incoming=other.Teams[0][0];var outId=first.Teams[0][0];var originalOther=JsonSerializer.Serialize(other);
 MeetRules.Substitute(m,first,new(0,outId,incoming));
 Check(first.Teams[0][0]==incoming&&JsonSerializer.Serialize(other)==originalOther,mode+" busy player can substitute without changing other match");
 Reject(()=>MeetRules.Start(m,first),mode+" prevents simultaneous live play");
 MeetRules.Substitute(m,first,new(0,incoming,first.Teams[1][0]));Check(first.Teams.SelectMany(t=>t).Distinct().Count()==4,mode+" same-match selection swaps without duplicates");
 Reject(()=>Scheduler.Generate(m),mode+" preserves started/substituted schedule");
 MeetRules.Result(m,other,new(0,[4,2],new(){[incoming]=new(){Winners=2,UnforcedErrors=5}}));
 Check(other.Stats[incoming].PadelRatio==-3,mode+" negative padel ratio");
 Reject(()=>MeetRules.Result(m,other,new(0,[4,4],null)),mode+" rejects tied completed score");
}
var mixed=Example(12,2,6,"mixed",2);Scheduler.Generate(mixed);
Check(mixed.Players.Select(p=>mixed.Matches.Count(x=>x.Teams.SelectMany(t=>t).Contains(p.Id))).Distinct().Count()==1,"mixed equal match counts");
var ids=mixed.Matches.Select(x=>string.Join(",",x.Teams.SelectMany(t=>t))).ToList();Scheduler.Generate(mixed);
Check(ids.SequenceEqual(mixed.Matches.Select(x=>string.Join(",",x.Teams.SelectMany(t=>t)))),"scheduler deterministic");
var odd=Example(9,2,9,"mixed");Scheduler.Generate(odd);var counts=odd.Players.Select(p=>odd.Matches.Count(x=>x.Teams.SelectMany(t=>t).Contains(p.Id))).ToList();
Check(counts.Max()-counts.Min()<=1,"odd mixed roster fairness");
var replacement=Example(8,2,3,"rotating");Scheduler.Generate(replacement);var outgoing=replacement.Matches[0].Teams[0][0];
MeetRules.Replacement(replacement,new(0,outgoing,"New person",null,"one"));
var newId=replacement.Players.Last().Id;
Check(replacement.Matches.Count(x=>x.Teams.SelectMany(t=>t).Contains(newId))==1,"new replacement affects next match only");
Check(new PlayerStats().PadelRatio==null,"unknown statistics stay unknown");
var bestOf=Example(4,1,1,"rotating");bestOf.Format="5";Scheduler.Generate(bestOf);MeetRules.Result(bestOf,bestOf.Matches[0],new(0,[3,2],null));
Check(bestOf.Matches[0].Status=="done","best of five uses three games");
foreach(var mode in new[]{"rotating","balanced","fixed","mixed"}) {
 var windows=Example(12,3,9,mode,2);windows.Time="18:00";windows.DurationMinutes=180;windows.SlotMinutes=20;
 windows.CourtAvailability=[new(){Name="Court A",Start="18:00",End="21:00"},new(){Name="Court B",Start="18:00",End="21:00"},new(){Name="Court C",Start="18:00",End="20:00"}];
 Scheduler.Generate(windows);
 Check(windows.Matches.All(x=>x.Court!=3||x.Round<6),mode+" respects early court closing");
 Check(windows.Matches.All(x=>x.PlannedStart==CourtPlanning.Clock(1080+x.Round*20)&&x.PlannedEnd==CourtPlanning.Clock(1100+x.Round*20)),mode+" planned match times");
 Check(windows.Matches.GroupBy(x=>x.Round).All(g=>g.SelectMany(x=>x.Teams.SelectMany(t=>t)).Distinct().Count()==g.Count()*4),mode+" variable capacity no double bookings");
 var appearances=windows.Players.Select(p=>windows.Matches.Count(x=>x.Teams.SelectMany(t=>t).Contains(p.Id))).ToArray();
 Check(appearances.Max()-appearances.Min()<=1,mode+" variable capacity fairness");
}
var late=Example(8,2,6,"rotating");late.Time="18:00";late.SlotMinutes=20;
late.CourtAvailability=[new(){Name="A",Start="18:00",End="20:00"},new(){Name="B",Start="18:30",End="19:50"}];Scheduler.Generate(late);
Check(late.Matches.Where(x=>x.Court==2).All(x=>x.Round>=2&&x.Round<=4),"full slot must fit after opening and before closing");
late.CourtAvailability[0].End="19:00";Scheduler.Generate(late);Check(late.SchedulingNotes.Any(n=>n.Contains("guidance")),"uncovered booking times generate an advisory warning");
late.CourtAvailability[0].End="20:00";late.SlotMinutes=30;Scheduler.Generate(late);Check(late.Matches.Any(x=>x.Round==5),"round estimates can exceed session time");
Reject(()=>ReclubScraper.Code("https://evil.test/m/PATO94"),"reject other scraper hosts");
Reject(()=>ReclubScraper.Code("https://reclub.co:444/m/PATO94"),"reject nonstandard scraper ports");
Reject(()=>ReclubScraper.Parse("<html>Changed markup</html>","PATO94"),"changed Reclub markup fails explicitly");
// A small synthetic Nuxt payload, without real participant data.
var fixture="""<script id="__NUXT_DATA__">[["ShallowReactive",1],{"data":2},["ShallowReactive",3],{"meet-DEMO12":4},{"meet":5,"usersMap":6},["Reactive",7],{"123":13},{"referenceCode":8,"name":9,"participants":10},"DEMO12","Sample",["Reactive",11],[12],{"referenceId":14,"status":15},{"name":16},123,1,"Example Player"]</script>""";
var preview=ReclubScraper.Parse(fixture,"DEMO12");
Check(preview.Players.Count==1&&preview.Players[0].Name=="Example Player"&&preview.Players[0].Status==1,"extract participant via referenced public profile");
Reject(()=>ReclubScraper.Parse(fixture,"OTHER1"),"reject mismatched Reclub meet");
if(args.Length>0) {
 var sample=ReclubScraper.Parse(File.ReadAllText(args[0]),"PATO94");
 Check(sample.Players.Count(p=>p.Status==1)==22,"sample page matches 22 visibly confirmed players");
 Console.WriteLine($"Sample preview: {sample.Players.Count} roster entries, {sample.Players.Count(p=>p.Status==1)} confirmed.");
}

var neonUrl="postgresql://sample:p%40ss%3B%3Aword@ep-example-pooler.test/neondb?sslmode=require&channel_binding=require";
var parsed=new Npgsql.NpgsqlConnectionStringBuilder(DatabaseConfiguration.FromUrl(neonUrl));
Check(parsed.Password=="p@ss;:word"&&parsed.Username=="sample","Postgres URL credentials are decoded safely");
Check(parsed.SslMode==Npgsql.SslMode.VerifyFull&&parsed.ChannelBinding==Npgsql.ChannelBinding.Require,"Neon URL enforces TLS verification and channel binding");
Check(parsed.Port==5432&&parsed.Database=="neondb"&&parsed.MaxPoolSize==20,"Neon connection defaults and pool limit");
var config=new Microsoft.Extensions.Configuration.ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{
 ["DATABASE_URL"]=neonUrl,["DATABASE_URL_UNPOOLED"]=neonUrl.Replace("-pooler","")
}).Build();
var connections=DatabaseConfiguration.Resolve(config);
Check(new Npgsql.NpgsqlConnectionStringBuilder(connections.Application).Host!.Contains("-pooler")&&!new Npgsql.NpgsqlConnectionStringBuilder(connections.Schema).Host!.Contains("-pooler"),"pooled app and direct schema connections");
config["ConnectionStrings:Rally"]="Host=localhost;Database=local;Username=rally";
config["Database:Password"]="semi;colon";
var local=DatabaseConfiguration.Resolve(config);
Check(local.Application==local.Schema&&new Npgsql.NpgsqlConnectionStringBuilder(local.Application).Password=="semi;colon","explicit local configuration takes precedence and quotes password");
try { DatabaseConfiguration.FromUrl("postgresql://secret:secret@host/db?unsupported=secret");throw new Exception("Expected URL rejection"); }
catch(InvalidOperationException e){Check(!e.ToString().Contains("secret"),"invalid connection errors do not expose secrets");}

var requested=Example(12,3,7,"rotating");requested.Time="18:00";requested.DurationMinutes=180;requested.SlotMinutes=25;
requested.CourtAvailability=[new(){Name="Court A",Start="18:00",End="21:00"},new(){Name="Court B",Start="18:00",End="21:00"},new(){Name="Court C",Start="18:00",End="20:00"}];
Scheduler.Generate(requested);
Check(requested.Matches.Where(x=>x.Court==3).Any()&&requested.Matches.Where(x=>x.Court==3).All(x=>x.Round<4),"requested seven 25-minute rounds respect Court C closing");
Check(requested.Matches.Where(x=>x.Round==6).All(x=>x.PlannedEnd=="20:55"),"requested 180-minute meet ends play at 20:55");
requested.Time="16:00";
CourtPlanning.Validate(requested);Check(true,"bookings outside session are advisory");
requested.Time="18:00";requested.CourtAvailability[0].End="17:00";
try { CourtPlanning.Validate(requested);throw new Exception("Expected reversed times"); }
catch(RuleException e){Check(e.Message.Contains("Court A")&&e.Message.Contains("later than start"),"reversed court times have a separate clear error");}

foreach(var mode in new[]{"rotating","balanced","fixed","mixed"}) {
 var departure=Example(12,3,7,mode,2);Scheduler.Generate(departure);
 foreach(var match in departure.Matches.Where(x=>x.Round==0))MeetRules.Result(departure,match,new(0,[4,2],null));
 var live=departure.Matches.First(x=>x.Round==1);MeetRules.Start(departure,live);
 var leaving=live.Teams[0][0];
 var history=departure.Matches.Where(x=>x.Status!="ready").ToDictionary(x=>x.Id,x=>JsonSerializer.Serialize(x));
 MeetRules.Depart(departure,new(0,leaving,1));
 Check(departure.Matches.Where(x=>x.Status=="ready"&&x.Round>=1).All(x=>!x.Teams.SelectMany(t=>t).Contains(leaving)),mode+" departure excludes future play");
 Check(history.All(entry=>departure.Matches.Any(x=>x.Id==entry.Key&&JsonSerializer.Serialize(x)==entry.Value)),mode+" departure preserves results and live timer");
 Check(departure.Matches.GroupBy(x=>x.Round).All(g=>g.SelectMany(x=>x.Teams.SelectMany(t=>t)).Distinct().Count()==g.Count()*4),mode+" remaining schedule no player duplicates");
 Check(departure.Matches.GroupBy(x=>new{x.Round,x.Court}).All(g=>g.Count()==1),mode+" remaining schedule no court duplicates");
 var ready=departure.Matches.First(x=>x.Status=="ready"&&x.Round>=1);
 MeetRules.Substitute(departure,ready,new(0,ready.Teams[0][0],leaving));
 Check(ready.Teams.SelectMany(t=>t).Contains(leaving),mode+" departed player can be explicitly substituted");
 Reject(()=>MeetRules.Depart(departure,new(0,leaving,0)),mode+" departure cannot rewrite past round");
 if(mode=="fixed"){
  var partner=departure.FixedPairs.Single(t=>t.Contains(leaving)).Single(id=>id!=leaving);
  Check(departure.Matches.Where(x=>x.Status=="ready").All(x=>!x.Teams.SelectMany(t=>t).Contains(partner)),"fixed orphan sits out");
 }
}
var scheduled=Example(12,3,7,"rotating");scheduled.Time="18:00";scheduled.DurationMinutes=180;scheduled.SlotMinutes=25;
scheduled.CourtAvailability=[new(){Name="A",Start="18:00",End="21:00"},new(){Name="B",Start="18:00",End="21:00"},new(){Name="C",Start="18:00",End="20:00"}];
Scheduler.Generate(scheduled);MeetRules.Depart(scheduled,new(0,"p0",4));
Check(scheduled.Matches.Any(x=>x.Round<4&&x.Teams.SelectMany(t=>t).Contains("p0"))&&scheduled.Matches.Where(x=>x.Round>=4).All(x=>!x.Teams.SelectMany(t=>t).Contains("p0")),"planned departure takes effect at selected round");
Check(scheduled.Matches.Where(x=>x.Court==3).All(x=>x.Round<4),"remaining generation respects court windows");
var fixedExit=Example(4,1,4,"fixed");Scheduler.Generate(fixedExit);
MeetRules.Result(fixedExit,fixedExit.Matches[0],new(0,[4,1],null));
MeetRules.Depart(fixedExit,new(0,"p0",1));
Check(fixedExit.Matches.Count==1&&fixedExit.SchedulingNotes.Count>0,"too few eligible players preserves history and explains empty rounds");
MeetRules.Replacement(fixedExit,new(0,"p0","New partner",null,"meet"));
Check(fixedExit.Matches.Count==4&&fixedExit.Matches.Where(x=>x.Status=="ready").All(x=>x.Teams.SelectMany(t=>t).Contains(fixedExit.Players.Last().Id)),"replacement restores departed fixed pair");
var oneSub=Example(8,2,4,"rotating");Scheduler.Generate(oneSub);
MeetRules.Replacement(oneSub,new(0,oneSub.Matches[0].Teams[0][0],"One match guest",null,"one"));
var pinned=oneSub.Matches.Single(x=>x.Changes.Count>0);var pinJson=JsonSerializer.Serialize(pinned);
Scheduler.RegenerateRemaining(oneSub);
Check(oneSub.Matches.Any(x=>x.Id==pinned.Id&&JsonSerializer.Serialize(x)==pinJson),"regeneration preserves eligible manual substitution");
Check(oneSub.Matches.Count(x=>x.Teams.SelectMany(t=>t).Contains(oneSub.Players.Last().Id))==1,"one-match replacement does not enter remaining rotation");
var fairness=Example(8,1,5,"rotating");Scheduler.Generate(fairness);
fairness.Matches[0].Teams=[["p0","p1"],["p2","p3"]];MeetRules.Result(fairness,fairness.Matches[0],new(0,[4,2],null));
Scheduler.RegenerateRemaining(fairness);
var played=fairness.Players.Select(p=>fairness.Matches.Count(x=>x.Teams.SelectMany(t=>t).Contains(p.Id))).ToArray();
Check(played.Max()-played.Min()<=1,"remaining fairness includes completed games");
var finished=Example(4,1,1,"rotating");Scheduler.Generate(finished);MeetRules.Result(finished,finished.Matches[0],new(0,[4,2],null));
Reject(()=>Scheduler.RegenerateRemaining(finished),"completed session cannot regenerate");
var corrected=Example(4,1,4,"rotating");Scheduler.Generate(corrected);MeetRules.Depart(corrected,new(0,"p0",0));MeetRules.Depart(corrected,new(0,"p0",2));
Check(corrected.Matches.Count==2&&corrected.Matches.All(x=>x.Round<2),"departure can be corrected after all remaining matches were removed");

foreach(var mode in new[]{"rotating","balanced","fixed","mixed"}) {
 var sides=Example(12,3,7,mode,2);
 for(int i=0;i<12;i++)sides.Players[i].Side=i%2==0?"left":"right";
 Scheduler.Generate(sides);
 Check(sides.Matches.Count>0&&sides.Matches.All(x=>x.Teams.All(t=>MeetRules.CompatibleTeam(sides,t))),mode+" strict side compatibility");
 Check(sides.Matches.All(x=>x.Teams.All(t=>sides.Players.Single(p=>p.Id==t[0]).Side=="left")),mode+" left player listed first");
 var arrivalMeet=Example(12,1,10,mode,2);Scheduler.Generate(arrivalMeet);
 MeetRules.Arrival(arrivalMeet,new(0,"p0",arrivalMeet.Rounds));
 Check(arrivalMeet.Matches.All(x=>!x.Teams.SelectMany(t=>t).Contains("p0")),mode+" waiting player excluded");
 foreach(var match in arrivalMeet.Matches.Where(x=>x.Round<6))MeetRules.Result(arrivalMeet,match,new(0,[4,2],null));
 var before=arrivalMeet.Matches.Where(x=>x.Status=="done").ToDictionary(x=>x.Id,x=>JsonSerializer.Serialize(x));
 MeetRules.Arrival(arrivalMeet,new(0,"p0",6));
 Check(before.All(kv=>arrivalMeet.Matches.Any(x=>x.Id==kv.Key&&JsonSerializer.Serialize(x)==kv.Value)),mode+" arrival preserves history");
 Check(arrivalMeet.Matches.Where(x=>x.Round<6).All(x=>!x.Teams.SelectMany(t=>t).Contains("p0")),mode+" arrival never rewrites missed rounds");
 int lateGames=arrivalMeet.Matches.Count(x=>x.Teams.SelectMany(t=>t).Contains("p0"));
 Check(lateGames is >=1 and <=2,mode+" arrival fair share without catch-up");
 Scheduler.RegenerateRemaining(arrivalMeet);
 Check(arrivalMeet.Matches.Count(x=>x.Teams.SelectMany(t=>t).Contains("p0")) is >=1 and <=2,mode+" repeated regeneration keeps arrival fairness");
}
var blockedSides=Example(8,2,4,"rotating");foreach(var p in blockedSides.Players)p.Side="left";Scheduler.Generate(blockedSides);
Check(blockedSides.Matches.Count==0&&blockedSides.SchedulingNotes.Count>0,"incompatible sides leave courts idle with explanation");
MeetRules.SetSide(blockedSides,new(0,"p0","right"));MeetRules.SetSide(blockedSides,new(0,"p1","right"));
Check(blockedSides.Matches.Count>0&&blockedSides.Matches.All(x=>x.Teams.All(t=>MeetRules.CompatibleTeam(blockedSides,t))),"side edits restore compatible schedule");
var badPair=Example(4,1,3,"fixed");badPair.Players[0].Side="left";badPair.Players[1].Side="left";
Reject(()=>Scheduler.Generate(badPair),"incompatible fixed team rejected");
var substituteSides=Example(8,1,4,"rotating");for(int i=0;i<8;i++)substituteSides.Players[i].Side=i%2==0?"left":"right";Scheduler.Generate(substituteSides);
var sm=substituteSides.Matches[0];var wrong=substituteSides.Players.First(p=>p.Side=="right"&&!sm.Teams.SelectMany(t=>t).Contains(p.Id));
Reject(()=>MeetRules.Substitute(substituteSides,sm,new(0,sm.Teams[0][0],wrong.Id)),"substitution cannot violate strict side");
Reject(()=>MeetRules.Replacement(substituteSides,new(0,sm.Teams[0][0],"Wrong side guest",null,"one","right")),"new replacement cannot violate strict side");
var allLate=Example(4,1,4,"rotating");Scheduler.Generate(allLate);MeetRules.Arrival(allLate,new(0,"p0",4));MeetRules.Arrival(allLate,new(0,"p0",0));
Check(allLate.Matches.Count==4,"arrival restores matches after waiting removed all assignments");

var flexible=Example(5,1,5,"rotating");
for(int i=0;i<4;i++)flexible.Players[i].Side=i%2==0?"left":"right";
Scheduler.Generate(flexible);
Check(flexible.Players.All(p=>flexible.Matches.Count(x=>x.Teams.SelectMany(t=>t).Contains(p.Id))==4),"Both player receives fair games alongside strict sides");
var sideCapacity=Example(8,2,4,"rotating");
for(int i=0;i<8;i++)sideCapacity.Players[i].Side=i<4?"left":i<6?"right":"both";
Scheduler.Generate(sideCapacity);
Check(sideCapacity.Matches.Count==8,"flexible players preserve maximum compatible court capacity");
var singleRoundArrival=Example(4,1,1,"rotating");Scheduler.Generate(singleRoundArrival);
MeetRules.Arrival(singleRoundArrival,new(0,"p0",1));MeetRules.Arrival(singleRoundArrival,new(0,"p0",0));
Check(singleRoundArrival.Matches.Count==1,"arrival restores an empty single-round meet");
foreach(var mode in new[]{"rotating","balanced","fixed","mixed"}) {
 var original=Example(12,2,4,mode,2);Scheduler.Generate(original);
 MeetRules.Result(original,original.Matches[0],new(0,[4,1],null));
 MeetRules.Start(original,original.Matches[1]);
 var snapshot=original.Matches.ToDictionary(x=>x.Id,x=>JsonSerializer.Serialize(x));
 var extended=Scheduler.AddRounds(original,new(0,2,180));
 Check(extended.Rounds==6&&extended.SlotMinutes==30&&extended.DurationMinutes==180,mode+" append rounds freezes existing slot length");
 Check(snapshot.All(kv=>extended.Matches.Any(x=>x.Id==kv.Key&&JsonSerializer.Serialize(x)==kv.Value)),mode+" extra rounds preserve all existing matches exactly");
 Check(extended.Matches.Any(x=>x.Round>=4)&&extended.Matches.Where(x=>x.Round>=4).All(x=>x.Status=="ready"),mode+" extra rounds create unstarted matches");
 Check(extended.Matches.GroupBy(x=>x.Round).All(g=>g.SelectMany(x=>x.Teams.SelectMany(t=>t)).Distinct().Count()==4*g.Count()),mode+" extra rounds avoid duplicate player assignments");
 Check(original.Rounds==4&&original.DurationMinutes==120,mode+" extension does not mutate original aggregate");
}
var absentSub=Example(8,1,4,"rotating");Scheduler.Generate(absentSub);
MeetRules.Arrival(absentSub,new(0,"p0",4));
var substituteMatch=absentSub.Matches[0];MeetRules.Substitute(absentSub,substituteMatch,new(0,substituteMatch.Teams[0][0],"p0"));
var manualJson=JsonSerializer.Serialize(substituteMatch);Scheduler.RegenerateRemaining(absentSub);
Check(absentSub.Matches.Any(x=>x.Id==substituteMatch.Id&&JsonSerializer.Serialize(x)==manualJson),"regeneration preserves explicit late-player match substitution");
Check(absentSub.Matches.Count(x=>x.Teams.SelectMany(t=>t).Contains("p0"))==1&&absentSub.Players[0].AvailableFromRound==4,"manual substitution never changes other attendance");
var subPersisted=absentSub.Matches.Single(x=>x.Id==substituteMatch.Id);MeetRules.Start(absentSub,subPersisted);MeetRules.Result(absentSub,subPersisted,new(0,[4,2],null));
Check(subPersisted.Status=="done","late-player substitution can start and save result");
var extendedAbsent=Scheduler.AddRounds(absentSub,new(0,1,150));
Check(extendedAbsent.Players[0].AvailableFromRound==5&&extendedAbsent.Matches.Where(x=>x.Round==4).All(x=>!x.Teams.SelectMany(t=>t).Contains("p0")),"waiting players do not automatically arrive in appended rounds");
var bookedExtra=Example(8,2,4,"rotating");bookedExtra.SlotMinutes=20;bookedExtra.CourtAvailability=[new(){Name="A",Start="16:00",End="18:00"},new(){Name="B",Start="16:00",End="17:20"}];Scheduler.Generate(bookedExtra);
var spare=Scheduler.AddRounds(bookedExtra,new(0,2));
Check(spare.Matches.Where(x=>x.Round>=4).All(x=>x.Court==1)&&spare.DurationMinutes==120,"extra rounds use spare session time and honor early court closing");
var beforeReject=JsonSerializer.Serialize(spare);
var advisoryExtra=Scheduler.AddRounds(spare,new(0,1));Check(advisoryExtra.Matches.Any(x=>x.Round==6)&&advisoryExtra.SchedulingNotes.Any(n=>n.Contains("guidance")),"extra rounds allow unextended bookings with warning");
Check(JsonSerializer.Serialize(spare)==beforeReject,"advisory extension leaves original meet unchanged");
var longer=Scheduler.AddRounds(spare,new(0,1,140,[new(){Name="A",Start="16:00",End="18:20"},new(){Name="B",Start="16:00",End="17:20"}]));
Check(longer.Matches.Any(x=>x.Round==6&&x.PlannedStart=="18:00"&&x.PlannedEnd=="18:20"),"explicit court extension enables later round");
Reject(()=>Scheduler.AddRounds(longer,new(0,0)),"reject zero extra rounds");
Reject(()=>Scheduler.AddRounds(longer,new(0,20)),"reject exceeding total round limit");
var completeExtra=Example(4,1,2,"rotating");Scheduler.Generate(completeExtra);foreach(var match in completeExtra.Matches)MeetRules.Result(completeExtra,match,new(0,[4,0],null));
Check(Scheduler.AddRounds(completeExtra,new(0,1,180)).Matches.Count==3,"completed meet can append extra rounds");
var allGone=Example(4,1,2,"rotating");Scheduler.Generate(allGone);MeetRules.Depart(allGone,new(0,"p0",0));
Reject(()=>Scheduler.AddRounds(allGone,new(0,1,180)),"empty extra schedule reports insufficient players");
foreach(var mode in new[]{"rotating","balanced","fixed","mixed"}) {
 var quickBase=Example(8,2,3,mode,1);Scheduler.Generate(quickBase);
 MeetRules.Result(quickBase,quickBase.Matches[0],new(0,[4,1],new(){[quickBase.Matches[0].Teams[0][0]]=new(){Winners=5,UnforcedErrors=1}}));
 var existing=quickBase.Matches.ToDictionary(x=>x.Id,x=>JsonSerializer.Serialize(x));
 var quick=Scheduler.AddMatch(quickBase,2);var added=quick.Matches.Single(x=>!existing.ContainsKey(x.Id));
 Check(added.Court==2&&added.ManualAssignment&&added.PlannedStart==null&&added.PlannedEnd==null,mode+" quick match uses selected court without fixed time");
 Check(quick.Matches.Count==quickBase.Matches.Count+1&&existing.All(kv=>quick.Matches.Any(x=>x.Id==kv.Key&&JsonSerializer.Serialize(x)==kv.Value)),mode+" quick match preserves every existing assignment");
 Check(added.Teams.All(t=>MeetRules.CompatibleTeam(quick,t)),mode+" quick match respects sides");
 Scheduler.RegenerateRemaining(quick);Check(quick.Matches.Any(x=>x.Id==added.Id),mode+" quick match survives remaining regeneration");
 var regenerated=Scheduler.RegenerateAll(quick,true);
 Check(regenerated.Matches.All(x=>x.Status=="ready"&&x.Score==null&&x.Stats.Count==0&&x.StartedAt==null&&!quick.Matches.Any(old=>old.Id==x.Id)),mode+" full regeneration creates fresh upcoming matches");
}
var managedMatches=Example(8,2,3,"rotating");Scheduler.Generate(managedMatches);
var restartMatch=managedMatches.Matches[0];MeetRules.Start(managedMatches,restartMatch);MeetRules.Result(managedMatches,restartMatch,new(0,[4,1],new(){[restartMatch.Teams[0][0]]=new(){Winners=9}}));
var originalTeams=JsonSerializer.Serialize(restartMatch.Teams);
Reject(()=>MeetRules.Restart(restartMatch,false),"restart requires explicit confirmation");
MeetRules.Restart(restartMatch,true);
Check(restartMatch.Status=="ready"&&restartMatch.Score==null&&restartMatch.Stats.Count==0&&restartMatch.StartedAt==null&&restartMatch.EndedAt==null&&JsonSerializer.Serialize(restartMatch.Teams)==originalTeams,"restart clears result and timer while retaining teams");
MeetRules.Start(managedMatches,restartMatch);
Check(restartMatch.Status=="live"&&restartMatch.StartedAt!=null,"restarted match can start a new timer");
var beforeDelete=managedMatches.Matches.Count;
Reject(()=>MeetRules.Delete(managedMatches,restartMatch,false),"delete requires explicit confirmation");
MeetRules.Delete(managedMatches,restartMatch,true);
Check(managedMatches.Matches.Count==beforeDelete-1&&!managedMatches.Matches.Any(x=>x.Id==restartMatch.Id),"delete removes selected live match only");
Reject(()=>Scheduler.RegenerateAll(managedMatches,false),"full regeneration requires warning acknowledgment");
Reject(()=>Scheduler.AddMatch(managedMatches,3),"quick generation rejects nonexistent court");
Check(CourtPlanning.Clock(1450)=="00:10 (+1d)","late estimated slots show following day explicitly");
var outsideBooking=Scheduler.AddMatch(spare,2);Check(outsideBooking.Matches.Any(x=>x.ManualAssignment&&x.Court==2&&x.PlannedStart==null),"quick match accepts selected court outside estimated booking");
foreach(var mode in new[]{"rotating","balanced","fixed","mixed"}) {
 var swapMeet=Example(12,3,4,mode,2);Scheduler.Generate(swapMeet);
 var source=swapMeet.Matches[0];var target=swapMeet.Matches[1];var unaffected=swapMeet.Matches.Where(x=>x.Id!=source.Id&&x.Id!=target.Id).ToDictionary(x=>x.Id,x=>JsonSerializer.Serialize(x));
 var swapOutgoing=source.Teams[0][0];var incoming=target.Teams[1][1];
 MeetRules.Substitute(swapMeet,source,new(0,swapOutgoing,incoming));
 Check(source.Teams.SelectMany(t=>t).Contains(incoming)&&!source.Teams.SelectMany(t=>t).Contains(swapOutgoing)&&target.Teams.SelectMany(t=>t).Contains(swapOutgoing)&&!target.Teams.SelectMany(t=>t).Contains(incoming),mode+" upcoming cross-court players swap reciprocally");
 Check(unaffected.All(kv=>JsonSerializer.Serialize(swapMeet.Matches.Single(x=>x.Id==kv.Key))==kv.Value),mode+" swap preserves all other matches");
 var sourceJson=JsonSerializer.Serialize(source);var targetJson=JsonSerializer.Serialize(target);Scheduler.RegenerateRemaining(swapMeet);
 Check(swapMeet.Matches.Any(x=>x.Id==source.Id&&JsonSerializer.Serialize(x)==sourceJson)&&swapMeet.Matches.Any(x=>x.Id==target.Id&&JsonSerializer.Serialize(x)==targetJson),mode+" regeneration preserves both sides of swap");
}
var sameSwap=Example(4,1,2,"rotating");Scheduler.Generate(sameSwap);var same=sameSwap.Matches[0];var sameOut=same.Teams[0][0];var sameIn=same.Teams[1][0];
MeetRules.Substitute(sameSwap,same,new(0,sameOut,sameIn));
Check(same.Teams[0][0]==sameIn&&same.Teams[1][0]==sameOut,"same-match opponent swap changes both positions");
var teamMate=same.Teams[0][1];MeetRules.Substitute(sameSwap,same,new(0,sameIn,teamMate));
Check(same.Teams[0][0]==teamMate&&same.Teams[0][1]==sameIn,"flexible teammates can swap positions");
Reject(()=>MeetRules.Substitute(sameSwap,same,new(0,teamMate,teamMate)),"cannot substitute player with themselves");
var rejectSwap=Example(8,2,2,"rotating");Scheduler.Generate(rejectSwap);var rs=rejectSwap.Matches[0];var rt=rejectSwap.Matches[1];
rejectSwap.Players.Single(p=>p.Id==rs.Teams[0][0]).Side="left";
rejectSwap.Players.Single(p=>p.Id==rt.Teams[0][0]).Side="both";
rejectSwap.Players.Single(p=>p.Id==rt.Teams[0][1]).Side="left";
var rejectBefore=JsonSerializer.Serialize(rejectSwap);
Reject(()=>MeetRules.Substitute(rejectSwap,rs,new(0,rs.Teams[0][0],rt.Teams[0][0])),"reject side conflict in reciprocal target match");
Check(JsonSerializer.Serialize(rejectSwap)==rejectBefore,"failed reciprocal swap is atomic");
foreach(var mode in new[]{"rotating","balanced","fixed","mixed"}) {
 var original=Example(8,2,6,mode);Scheduler.Generate(original);
 MeetRules.Result(original,original.Matches[0],new(0,[4,2],null));MeetRules.Start(original,original.Matches[1]);
 var history=original.Matches.Where(x=>x.Status!="ready").ToDictionary(x=>x.Id,x=>JsonSerializer.Serialize(x));
 var added=MeetRules.AddPlayer(original,new(0,"New player","both",3,true,"New partner"));
 Check(added.Players.Count==8+(mode=="fixed"?2:1),mode+" new participants registered");
 Check(history.All(kv=>JsonSerializer.Serialize(added.Matches.Single(x=>x.Id==kv.Key))==kv.Value),mode+" joining preserves results and live match");
 Check(added.Matches.Any(x=>x.Status=="ready"&&x.Teams.SelectMany(t=>t).Contains(added.Players[8].Id)),mode+" new player scheduled");
 var rosterOnly=MeetRules.AddPlayer(original,new(0,"Roster only","both",3,false,"Roster partner"));
 Check(JsonSerializer.Serialize(rosterOnly.Matches)==JsonSerializer.Serialize(original.Matches),mode+" roster-only leaves schedule unchanged");
 Reject(()=>MeetRules.AddPlayer(original,new(0,"Player 0","both",3,true,"Other")),"duplicate addition rejected");
}
var fullRoster=Example(24,2,4,"rotating");Scheduler.Generate(fullRoster);
Reject(()=>MeetRules.AddPlayer(fullRoster,new(0,"Overflow","both",null,false)),"roster limit enforced");
var needSkill=Example(8,2,4,"balanced");Scheduler.Generate(needSkill);
Reject(()=>MeetRules.AddPlayer(needSkill,new(0,"No skill","both",null,true)),"balanced addition requires skill");
var fixedAdd=Example(8,2,4,"fixed");Scheduler.Generate(fixedAdd);
Reject(()=>MeetRules.AddPlayer(fixedAdd,new(0,"Left one","left",null,true,"Left two","left")),"new fixed pair side compatibility enforced");
Check(fixedAdd.Players.Count==8,"rejected addition leaves original roster intact");
Check(ReclubScraper.Code("https://reclub.co/id/m/K3YK9M")=="K3YK9M","Indonesian meet URL accepted");
Check(ReclubScraper.Code("https://reclub.co/en/m/K3YK9M/?source=share")=="K3YK9M","localized trailing slash and query accepted");
Check(ReclubScraper.Code("https://reclub.co/zh-TW/m/K3YK9M")=="K3YK9M","regional language prefix accepted");
Reject(()=>ReclubScraper.Code("https://reclub.co/id/other/K3YK9M"),"localized non-meet path rejected");
Reject(()=>ReclubScraper.Code("https://evil.test/id/m/K3YK9M"),"localized external host rejected");
if(args.Length>1){var localized=ReclubScraper.Parse(File.ReadAllText(args[1]),"K3YK9M");Check(localized.Players.Count>0,"live Indonesian sample roster parsed");var fetched=await ReclubScraper.Fetch("https://reclub.co/id/m/K3YK9M",CancellationToken.None);Check(fetched.Players.Count>0,"localized sample URL fetched and imported end to end");Console.WriteLine($"Imported {fetched.Players.Count} participants from localized sample.");}
Check(ClubPlayers.ProfileUrl("https://reclub.co/id/players/@Example/?share=1")=="https://reclub.co/players/@example","profile links normalize locale and username");
Reject(()=>ClubPlayers.ProfileUrl("https://reclub.co.evil.test/players/@test"),"profile links reject external hosts");
Reject(()=>ClubPlayers.ProfileUrl("https://reclub.co/m/TEST12"),"meet links cannot be used as player profiles");
Reject(()=>ClubPlayers.Photo("data:image/svg+xml;base64,PHN2Zy8+"),"profile photos reject SVG");
Reject(()=>ClubPlayers.Photo("data:image/png;base64,"+Convert.ToBase64String(new byte[20])),"profile photos verify signature");
Console.WriteLine($"All {checks} checks passed.");
