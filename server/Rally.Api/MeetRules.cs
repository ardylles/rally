namespace Rally;
public static class MeetRules {
 public static void Require(bool condition, string message, int status=400) { if(!condition) throw new RuleException(message,status); }
 public static int Target(string format) => format=="5"?3:int.Parse(format);
 public static void Validate(Meet m) {
  m.Name=(m.Name??"").Trim(); m.Venue=(m.Venue??"").Trim(); m.Players ??= []; m.FixedPairs ??= [];
  Require(m.Players.All(p=>p!=null),"Invalid player record.");
  Require(m.Name.Length is >=1 and <=80,"Enter a meet name of up to 80 characters.");
  Require(m.Venue.Length is >=1 and <=150,"Enter a venue.");
  Require(DateOnly.TryParseExact(m.Date,"yyyy-MM-dd",out _),"Enter a valid date.");
  Require(TimeOnly.TryParseExact(m.Time,"HH:mm",out _),"Enter a valid time.");
  Require(m.DurationMinutes is >=30 and <=480,"Session duration must be 30–480 minutes.");
  Require(m.Mode is "rotating" or "balanced" or "fixed" or "mixed","Choose a partner mode.");
  Require(m.Format is "4" or "6" or "5","Choose a match format.");
  Require(m.Players.Count is >=4 and <=24,"Add 4–24 players.");
  Require(m.Players.Select(p=>p.Id).Distinct().Count()==m.Players.Count,"Player IDs must be unique.");
  Require(m.Players.All(p=>!string.IsNullOrWhiteSpace(p.Id)&&!string.IsNullOrWhiteSpace(p.Name)&&p.Name.Trim().Length<=80),"Each player needs a name of up to 80 characters.");
  Require(m.Courts>=1&&m.Courts<=6,"Choose 1–6 courts.");
  Require(m.Rounds is >=1 and <=20,"Choose 1–20 rounds.");
  CourtPlanning.Validate(m);
  foreach(var p in m.Players) { ValidateSide(p.Side); Require(p.AvailableFromRound>=0&&p.AvailableFromRound<=m.Rounds,"Arrival round must be inside the meet, or waiting for arrival."); p.Name=p.Name.Trim(); if(p.Skill.HasValue) ValidateSkill(p.Skill.Value); if(m.Mode=="balanced")Require(p.Skill.HasValue,"Enter a skill for every player in balanced mode."); }
  if(m.Mode is "fixed" or "mixed") {
   Require(m.FixedPairs.All(t=>t!=null),"Invalid fixed pair.");
   var ids=m.FixedPairs.SelectMany(t=>t).ToList();
   Require(m.FixedPairs.All(t=>t!=null&&t.Length==2)&&ids.Distinct().Count()==ids.Count&&ids.All(id=>m.Players.Any(p=>p.Id==id)),"Fixed pairs must contain two distinct registered players, without repeats.");
   Require(m.FixedPairs.Count>0,"Select at least one fixed pair.");
   Require(m.FixedPairs.All(t=>Compatible(m.Players.Single(p=>p.Id==t[0]),m.Players.Single(p=>p.Id==t[1]))),"Fixed partners need compatible Left/Right sides. Choose Both for flexible players.");
   if(m.Mode=="fixed")Require(ids.Count==m.Players.Count,"Every player needs a fixed partner.");
   else Require(m.Players.Count-ids.Count>=2,"Leave at least two players in the rotating pool.");
  } else m.FixedPairs=[];
 }
 public static int RemainingRound(Meet m)=>m.Matches.Where(x=>x.Status!="ready").Select(x=>x.Round).DefaultIfEmpty(0).Max();
 public static void ValidateSide(string side)=>Require(side is "left" or "right" or "both","Choose Left, Right or Both.");
 public static bool Compatible(Player a,Player b)=>a.Side=="both"||b.Side=="both"||a.Side!=b.Side;
 public static bool CompatibleTeam(Meet m,string[] team)=>Compatible(m.Players.Single(p=>p.Id==team[0]),m.Players.Single(p=>p.Id==team[1]));
 public static void Arrival(Meet m,ArrivalInput input) {
  var p=m.Players.FirstOrDefault(p=>p.Id==input.PlayerId)??throw new RuleException("Player not found.",404);
  Require(input.FromRound>=RemainingRound(m)&&input.FromRound<=m.Rounds,"Choose the current or a future arrival round.");
  Require(!m.Matches.Any(x=>x.Status!="ready"&&x.Teams.SelectMany(t=>t).Contains(p.Id)),"Arrival cannot change after this player has begun playing.");
  Require(p.UnavailableFromRound==null||input.FromRound<p.UnavailableFromRound,"Arrival must be before departure.");
  p.AvailableFromRound=input.FromRound; Scheduler.RegenerateRemaining(m);
 }
 public static void SetSide(Meet m,SideInput input) {
  ValidateSide(input.Side);var p=m.Players.FirstOrDefault(p=>p.Id==input.PlayerId)??throw new RuleException("Player not found.",404);
  Require(!m.Matches.Any(x=>x.Status=="live"&&x.Teams.SelectMany(t=>t).Contains(p.Id)),"Finish this player’s live match before changing sides.");
  p.Side=input.Side;
  Require(m.FixedPairs.All(t=>CompatibleTeam(m,t)),"This side conflicts with the fixed partner. Choose Both or change the pair before play.");
  Scheduler.RegenerateRemaining(m);
 }
 public static bool Available(Player p,int round)=>round>=p.AvailableFromRound&&(p.UnavailableFromRound is not int from || round<from);
 // Explicit registered substitutions override attendance for this match only.
 public static bool AvailableForMatch(Player p,Match match)=>Available(p,match.Round)||
  match.Changes.LastOrDefault(c=>c.IncomingId==p.Id||c.OutgoingId==p.Id) is { Source:"match" } change&&change.IncomingId==p.Id;
 public static void Depart(Meet m,DepartureInput input) {
  var p=m.Players.FirstOrDefault(p=>p.Id==input.PlayerId)??throw new RuleException("Player not found.",404);
  Require(input.FromRound>=RemainingRound(m)&&input.FromRound<m.Rounds,"Choose the current or a future round within this meet.");
  Require(m.Matches.Any(x=>x.Status=="ready"&&x.Round>=input.FromRound)||(p.UnavailableFromRound!=null&&RemainingRound(m)<m.Rounds-1),"No unstarted matches remain from that round.");
  p.UnavailableFromRound=input.FromRound;
  Scheduler.RegenerateRemaining(m);
 }
 public static void ValidateSkill(double skill)=>Require(double.IsFinite(skill)&&skill>=1&&skill<=5&&skill*2==Math.Truncate(skill*2),"Skill must be 1–5 in steps of 0.5.");
 public static Match Find(Meet m,string id)=>m.Matches.FirstOrDefault(x=>x.Id==id)??throw new RuleException("Match not found.",404);
 public static void CanGenerate(Meet m)=>Require(m.Players.All(p=>p.UnavailableFromRound==null&&p.AvailableFromRound==0)&&m.Matches.All(x=>x.Status=="ready"&&x.Changes.Count==0),"Regeneration is available before play or substitutions begin.");
 public static void Start(Meet m, Match match) {
  Require(m.Timing,"Timing is disabled for this meet.");
  if(match.Status=="live")return;
  Require(match.Status=="ready","This match has already finished.");
  Require(match.Teams.All(t=>CompatibleTeam(m,t)),"A team has incompatible playing sides. Regenerate remaining matches.");
  var ids=match.Teams.SelectMany(t=>t).ToHashSet();
  Require(m.Players.Where(p=>ids.Contains(p.Id)).All(p=>AvailableForMatch(p,match)),"A player is not available in this round. Regenerate remaining matches.");
  Require(!m.Matches.Any(x=>x.Id!=match.Id&&x.Status=="live"&&(x.Court==match.Court||x.Teams.SelectMany(t=>t).Any(ids.Contains))),"A player or this court is still in a live match.",409);
  match.Status="live"; match.StartedAt=DateTimeOffset.UtcNow;
 }
 public static void Restart(Match match,bool confirmed) {
  Require(confirmed,"Confirm restarting this match.");
  Require(match.Status is "live" or "done","Only started or completed matches can restart.");
  match.Status="ready";match.StartedAt=null;match.EndedAt=null;match.Score=null;match.Stats=[];
  match.ManualAssignment=true;
 }
 public static void Delete(Meet m,Match match,bool confirmed) {
  Require(confirmed,"Confirm removing this match.");
  m.Matches.Remove(match);
 }
 public static void Result(Meet m,Match match,ResultInput input) {
  if(match.Status=="ready") { Require(match.Teams.SelectMany(t=>t).All(id=>AvailableForMatch(m.Players.Single(p=>p.Id==id),match)),"A player is not available for this round."); Require(match.Teams.All(t=>CompatibleTeam(m,t)),"A team has incompatible playing sides."); }
  Require(input.Score is {Length:2},"Enter both game totals.");
  var target=Target(match.Format);
  Require(input.Score.All(n=>n>=0&&n<=target)&&input.Score.Max()==target&&input.Score[0]!=input.Score[1],"One team must reach the target and the other must be below it.");
  var ids=match.Teams.SelectMany(t=>t).ToHashSet();
  var stats=input.Stats??[];
  Require(stats.Keys.All(ids.Contains),"Statistics must belong to the actual match players.");
  Require(stats.Values.All(s=>s!=null&&(!s.Winners.HasValue||s.Winners.Value is >=0 and <=10000)&&(!s.UnforcedErrors.HasValue||s.UnforcedErrors.Value is >=0 and <=10000)),"Statistics must be non-negative whole numbers.");
  match.Score=input.Score;match.Stats=stats;match.Status="done";match.EndedAt??=DateTimeOffset.UtcNow;
 }
 public static void Substitute(Meet m,Match match,SubstituteInput input) {
  Require(match.Status=="ready","Only unstarted matches can be substituted.");
  Require(match.Teams.SelectMany(t=>t).Contains(input.OutgoingId),"Choose an outgoing player from this match.");
  Require(m.Players.Any(p=>p.Id==input.IncomingId),"Choose a registered player from this meet.");
  Require(!match.Teams.SelectMany(t=>t).Contains(input.IncomingId),"That player is already in this match.");
  Require(match.Teams.Select(t=>t.Select(id=>id==input.OutgoingId?input.IncomingId:id).ToArray()).All(t=>CompatibleTeam(m,t)),"The substitute’s side conflicts with their partner.");
  Replace(m,match,input.OutgoingId,input.IncomingId,"one","match");
 }
 public static void Replacement(Meet m,ReplacementInput input) {
  ValidateSide(input.Side);
  Require(input.Scope is "one" or "meet","Choose next match or remaining meet.");
  var name=(input.Name??"").Trim();Require(name.Length is >=1 and <=80,"Enter a new player name.");
  Require(!m.Players.Any(p=>p.Name.Equals(name,StringComparison.OrdinalIgnoreCase)),"This player is already registered. Substitute them from a match.");
  Require(m.Players.Any(p=>p.Id==input.OutgoingId),"Choose a registered outgoing player.");
  if(input.Skill.HasValue)ValidateSkill(input.Skill.Value);
  Require(m.Mode!="balanced"||input.Skill.HasValue,"Enter the incoming player's skill.");
  var matches=m.Matches.Where(x=>x.Status=="ready"&&x.Teams.SelectMany(t=>t).Contains(input.OutgoingId)).OrderBy(x=>x.Round).ThenBy(x=>x.Court).ToList();
  var outgoingPlayer=m.Players.Single(p=>p.Id==input.OutgoingId);
  if(input.Scope=="meet"&&outgoingPlayer.UnavailableFromRound is int departure) {
   Require(m.Matches.Any(x=>x.Status=="ready")||MeetRules.RemainingRound(m)<m.Rounds-1,"No remaining rounds are available.");
   var replacement=new Player{Name=name,Skill=input.Skill,Side=input.Side,AvailableFromRound=Math.Max(departure,RemainingRound(m))};
   m.Players.Add(replacement);
   m.FixedPairs=m.FixedPairs.Select(t=>t.Select(id=>id==input.OutgoingId?replacement.Id:id).ToArray()).ToList();
   Require(m.FixedPairs.All(t=>CompatibleTeam(m,t)),"The replacement’s side conflicts with the fixed partner.");
   Scheduler.RegenerateRemaining(m);return;
  }
  Require(matches.Count>0,"No unstarted matches remain for this player.");
  if(input.Scope=="one")matches=matches.Take(1).ToList();
  var p=new Player{Name=name,Skill=input.Skill,Side=input.Side,AvailableFromRound=matches[0].Round,MatchOnlyReplacement=input.Scope=="one"};m.Players.Add(p);
  if(input.Scope=="meet")outgoingPlayer.UnavailableFromRound=matches[0].Round;
  Require(matches.All(match=>match.Teams.Select(t=>t.Select(id=>id==input.OutgoingId?p.Id:id).ToArray()).All(t=>CompatibleTeam(m,t))),"The replacement’s side conflicts with a partner.");
  foreach(var match in matches)Replace(m,match,input.OutgoingId,p.Id,input.Scope,"players");
  if(input.Scope=="meet")m.FixedPairs=m.FixedPairs.Select(t=>t.Select(id=>id==input.OutgoingId?p.Id:id).ToArray()).ToList();
 }
 private static void Replace(Meet m,Match match,string outgoing,string incoming,string scope,string source) {
  match.Teams=match.Teams.Select(t=>t.Select(p=>p==outgoing?incoming:p).ToArray()).ToArray();
  match.Teams=match.Teams.Select(t=>m.Players.Single(p=>p.Id==t[0]).Side=="right"||m.Players.Single(p=>p.Id==t[1]).Side=="left"?t.Reverse().ToArray():t).ToArray();
  match.Changes.Add(new(outgoing,incoming,scope,source,DateTimeOffset.UtcNow));
 }
}
