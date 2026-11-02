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
 Reject(()=>MeetRules.Substitute(m,first,new(0,incoming,first.Teams[1][0])),mode+" rejects duplicate participant");
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
late.CourtAvailability[0].End="19:00";Reject(()=>Scheduler.Generate(late),"reject uncovered rounds");
late.CourtAvailability[0].End="20:00";late.SlotMinutes=30;Reject(()=>Scheduler.Generate(late),"reject rounds beyond session");
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

Console.WriteLine($"All {checks} checks passed.");
