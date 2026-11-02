namespace Rally;
public static class Scheduler {
 public static void Generate(Meet m,int seed=91) {
  MeetRules.CanGenerate(m);MeetRules.Validate(m);
  Build(m,0,[],seed);
 }
 public static void RegenerateRemaining(Meet m,int seed=91) {
  int from=MeetRules.RemainingRound(m);
  MeetRules.Require(m.Matches.Any(x=>x.Status=="ready"&&x.Round>=from)||m.Matches.Count==0||from<m.Rounds-1,"No unstarted matches remain to regenerate.");
  CourtPlanning.Validate(m);
  // History and explicit substitutions are pinned; a departure invalidates only unstarted assignments.
  var pinned=m.Matches.Where(x=>x.Status!="ready"||(x.Round<from&&x.Teams.SelectMany(t=>t).All(id=>MeetRules.AvailableForMatch(m.Players.Single(p=>p.Id==id),x)))||
   ((x.Changes.Count>0||x.ManualAssignment)&&x.Teams.All(t=>MeetRules.CompatibleTeam(m,t))&&x.Teams.SelectMany(t=>t).All(id=>MeetRules.AvailableForMatch(m.Players.Single(p=>p.Id==id),x)))).ToList();
  Build(m,from,pinned,seed);
 }
 public static Meet AddRounds(Meet original,ExtraRoundsInput input,int seed=91) {
  MeetRules.Require(input.Count>=1&&input.Count<=20-original.Rounds,"Add at least one round, up to 20 rounds in total.");
  // Work on a copy: rejected extensions leave the current meet and its history untouched.
  var m=System.Text.Json.JsonSerializer.Deserialize<Meet>(System.Text.Json.JsonSerializer.Serialize(original,Json.Options),Json.Options)!;
  int from=m.Rounds;int slot=CourtPlanning.Slot(m);
  m.SlotMinutes=slot;m.Rounds+=input.Count;
  m.DurationMinutes=input.DurationMinutes??m.DurationMinutes;
  MeetRules.Require(m.DurationMinutes>=original.DurationMinutes&&m.DurationMinutes<=480,"Keep or extend the session duration, up to 480 minutes.");
  if(input.CourtAvailability!=null) {
   MeetRules.Require(input.CourtAvailability.Count==original.CourtAvailability.Count,"Keep the existing courts when adding rounds.");
   m.CourtAvailability=input.CourtAvailability.Select(c=>new CourtWindow{Name=c.Name,Start=c.Start,End=c.End}).ToList();
  }
  CourtPlanning.Validate(m);
  for(int i=0;i<original.CourtAvailability.Count;i++) {
   var old=original.CourtAvailability[i];var updated=m.CourtAvailability[i];
   MeetRules.Require(updated.Name==old.Name&&updated.Start==old.Start&&CourtPlanning.Minute(updated.End)>=CourtPlanning.Minute(old.End),"Only extend court closing times when adding rounds; existing bookings must stay intact.");
  }
  // The old rounds value means waiting, not an automatic arrival at the new first round.
  foreach(var p in m.Players.Where(p=>p.AvailableFromRound>=from))p.AvailableFromRound=m.Rounds;
  var previousNotes=m.SchedulingNotes.ToList();
  Build(m,from,m.Matches.ToList(),seed);
  MeetRules.Require(m.Matches.Any(x=>x.Round>=from),"No extra matches can be arranged with the available players and playing sides.");
  m.SchedulingNotes=previousNotes.Concat(m.SchedulingNotes).Distinct().ToList();
  return m;
 }
 public static Meet RegenerateAll(Meet original,bool confirmed,int seed=91) {
  MeetRules.Require(confirmed,"Confirm full regeneration: all matches, results, statistics and live timers will be replaced.");
  var m=Copy(original);CourtPlanning.Validate(m);
  m.Matches=[];Build(m,0,[],seed);
  MeetRules.Require(m.Matches.Count>0,"No matches can be arranged with the current attendance, pairs and playing sides.");
  return m;
 }
 private static Meet Copy(Meet original)=>System.Text.Json.JsonSerializer.Deserialize<Meet>(System.Text.Json.JsonSerializer.Serialize(original,Json.Options),Json.Options)!;
 public static Meet AddMatch(Meet original,int court,int seed=91) {
  MeetRules.Require(court>=1&&court<=original.Courts,"Choose a registered court.");
  int first=original.Matches.Select(x=>x.Round).DefaultIfEmpty(0).Max();
  for(int round=first;round<20;round++) {
   if(original.Matches.Any(x=>x.Round==round&&x.Court==court))continue;
   var m=Copy(original);m.SlotMinutes=CourtPlanning.Slot(original);m.Rounds=Math.Max(original.Rounds,round+1);
   foreach(var p in m.Players.Where(p=>p.AvailableFromRound>=original.Rounds))p.AvailableFromRound=m.Rounds;
   Build(m,round,m.Matches.ToList(),seed,court,round);
   var added=m.Matches.SingleOrDefault(x=>!original.Matches.Any(old=>old.Id==x.Id));
   if(added==null)continue;
   added.ManualAssignment=true;added.PlannedStart=null;added.PlannedEnd=null;
   m.SchedulingNotes=original.SchedulingNotes.Concat(m.SchedulingNotes).Append("Added match has no fixed start time. Start it when its players and selected court are free.").Distinct().ToList();
   return m;
  }
  throw new RuleException("No new match can be arranged with the available players, fixed pairs and sides within 20 rounds.");
 }
 private static void Build(Meet m,int from,List<Match> pinned,int seed,int? onlyCourt=null,int? onlyRound=null) {
  var rng=new Random(seed);var n=m.Players.Count;
  var fixedPairs=m.Mode is "fixed" or "mixed"?m.FixedPairs.Select(t=>t.Select(id=>m.Players.FindIndex(p=>p.Id==id)).ToArray()).ToList():[];
  // New players entered for just one match must not become general rotation members.
  var changes=m.Matches.SelectMany(x=>x.Changes.Select(c=>(Match:x,Change:c))).ToList();
  var oneOnly=changes.Where(c=>c.Change.Source=="players"&&c.Change.Scope=="one").Select(c=>c.Change.IncomingId).ToHashSet();
  var replaced=changes.Where(c=>c.Change.Source=="players"&&c.Change.Scope=="meet").GroupBy(c=>c.Change.OutgoingId).ToDictionary(g=>g.Key,g=>g.Min(c=>c.Match.Round));
  var joined=changes.Where(c=>c.Change.Source=="players"&&c.Change.Scope=="meet").GroupBy(c=>c.Change.IncomingId).ToDictionary(g=>g.Key,g=>g.Min(c=>c.Match.Round));
  bool Eligible(int i,int round)=>MeetRules.Available(m.Players[i],round)&&!m.Players[i].MatchOnlyReplacement&&!oneOnly.Contains(m.Players[i].Id)&&
   (!replaced.TryGetValue(m.Players[i].Id,out var leave)||round<leave)&&(!joined.TryGetValue(m.Players[i].Id,out var join)||round>=join);
  int[] EligiblePlayers(int round) {
   var eligible=Enumerable.Range(0,n).Where(i=>Eligible(i,round)).ToHashSet();
   return m.Mode=="fixed"?fixedPairs.Where(t=>t.All(eligible.Contains)).SelectMany(t=>t).ToArray():eligible.ToArray();
  }
  List<Match>? best=null;double bestCost=double.MaxValue;
  string Key(int a,int b)=>a<b?$"{a}:{b}":$"{b}:{a}";
  List<T> Shuffle<T>(IEnumerable<T> values) {var a=values.ToList();for(int i=a.Count-1;i>0;i--){int j=rng.Next(i+1);(a[i],a[j])=(a[j],a[i]);}return a;}
  for(int attempt=0;attempt<24;attempt++) {
   var counts=new double[n];var rests=new int[n];var partners=new Dictionary<string,int>();var opponents=new Dictionary<string,int>();
   var matches=pinned.ToList();double variety=0,balance=0;
   void Record(int[][] teams) {
    foreach(var team in teams){foreach(var i in team)counts[i]++;var k=Key(team[0],team[1]);partners[k]=partners.GetValueOrDefault(k)+1;}
    foreach(var a in teams[0])foreach(var b in teams[1]){var k=Key(a,b);opponents[k]=opponents.GetValueOrDefault(k)+1;}
   }
   for(int round=0;round<m.Rounds;round++) {
    // Give newcomers the current virtual baseline: missed rounds create no catch-up debt.
    if(round>0) {
     var established=Enumerable.Range(0,n).Where(i=>m.Players[i].AvailableFromRound<round&&Eligible(i,round)).ToArray();
     var baseline=established.Length>0?established.Average(i=>counts[i]):0;
     foreach(var i in Enumerable.Range(0,n).Where(i=>m.Players[i].AvailableFromRound==round))counts[i]=baseline;
    }
    var reserved=pinned.Where(x=>x.Round==round).ToList();
    foreach(var match in reserved)Record(match.Teams.Select(t=>t.Select(id=>m.Players.FindIndex(p=>p.Id==id)).ToArray()).ToArray());
    if(round<from||(onlyRound.HasValue&&round!=onlyRound))continue;
    var occupied=reserved.SelectMany(x=>x.Teams.SelectMany(t=>t)).ToHashSet();
    var available=(onlyCourt.HasValue?new List<int>{onlyCourt.Value}:CourtPlanning.Available(m,round)).Where(c=>reserved.All(x=>x.Court!=c)).ToList();
    var eligible=EligiblePlayers(round);
    var free=eligible.Where(i=>!occupied.Contains(m.Players[i].Id)).ToHashSet();
    var pairs=fixedPairs.Where(t=>t.All(free.Contains)&&MeetRules.Compatible(m.Players[t[0]],m.Players[t[1]])).ToList();
    var locked=fixedPairs.Where(t=>t.All(i=>eligible.Contains(i))).SelectMany(t=>t).ToHashSet();
    var pool=m.Mode=="fixed"?[]:free.Where(i=>!locked.Contains(i)).ToArray();
    List<int[]>? choice=null;double roundBest=double.MaxValue;
    for(int trial=0;trial<70;trial++) {
     var singles=Shuffle(pool).OrderBy(i=>counts[i]).ToList();
     var teams=pairs.Select(t=>t.ToArray()).ToList();
     while(singles.Count>1) {
      // Preserve maximum compatible capacity, then favor players with fewer games.
      int Possible(IEnumerable<int> ids) { var list=ids.ToList();return Math.Min(list.Count/2,list.Count-Math.Max(list.Count(i=>m.Players[i].Side=="left"),list.Count(i=>m.Players[i].Side=="right"))); }
      int target=Possible(singles);
      int a=singles[0];singles.RemoveAt(0);
      int j=singles.FindIndex(b=>MeetRules.Compatible(m.Players[a],m.Players[b])&&Possible(singles.Where(i=>i!=b))>=target-1);
      if(j<0)continue;
      teams.Add([a,singles[j]]);singles.RemoveAt(j);
     }
     int capacity=Math.Min(available.Count,teams.Count/2);
     int used=attempt>=12&&trial%4==0&&capacity>1?capacity-1:capacity;
     var selected=Shuffle(Shuffle(teams).OrderBy(t=>counts[t[0]]+counts[t[1]]+rng.NextDouble()*1.6).Take(used*2));
     var ids=selected.SelectMany(t=>t).ToHashSet();
     var next=eligible.Select(i=>counts[i]+(ids.Contains(i)?1:0)).ToArray();
     double mean=next.Length>0?next.Average():0,repeat=0,gap=0;
     for(int i=0;i<selected.Count;i+=2) {
      foreach(var team in new[]{selected[i],selected[i+1]})if(!locked.Contains(team[0]))repeat+=partners.GetValueOrDefault(Key(team[0],team[1]))*5;
      foreach(var a in selected[i])foreach(var b in selected[i+1])repeat+=opponents.GetValueOrDefault(Key(a,b));
      if(m.Mode=="balanced")gap+=Math.Abs(selected[i].Sum(p=>m.Players[p].Skill!.Value)-selected[i+1].Sum(p=>m.Players[p].Skill!.Value));
     }
     double rest=eligible.Where(i=>!ids.Contains(i)&&!occupied.Contains(m.Players[i].Id)).Sum(i=>Math.Pow(rests[i]+1,2));
     double cost=(next.Length>0?next.Max()-next.Min():0)*10000+next.Sum(c=>Math.Pow(c-mean,2))*100+rest*10-selected.Count*20+repeat+gap*40;
     if(cost<roundBest){roundBest=cost;choice=selected;}
    }
    choice ??= [];
    var active=choice.SelectMany(t=>t).ToHashSet();
    for(int i=0;i<n;i++)rests[i]=active.Contains(i)||occupied.Contains(m.Players[i].Id)?0:rests[i]+1;
    for(int c=0;c<choice.Count;c+=2) {
     var teams=new[]{choice[c],choice[c+1]};
     foreach(var team in teams)if(!locked.Contains(team[0]))variety+=partners.GetValueOrDefault(Key(team[0],team[1]));
     if(m.Mode=="balanced")balance+=Math.Abs(teams[0].Sum(i=>m.Players[i].Skill!.Value)-teams[1].Sum(i=>m.Players[i].Skill!.Value));
     Record(teams);
     matches.Add(new(){Round=round,Court=available[c/2],PlannedStart=CourtPlanning.Clock(CourtPlanning.Start(m,round)),PlannedEnd=CourtPlanning.Clock(CourtPlanning.Start(m,round)+CourtPlanning.Slot(m)),Teams=teams.Select(t=>(m.Players[t[0]].Side=="right"||m.Players[t[1]].Side=="left"?t.Reverse():t).Select(i=>m.Players[i].Id).ToArray()).ToArray(),Format=m.Format});
    }
   }
   var finalCounts=EligiblePlayers(m.Rounds-1).Select(i=>counts[i]).ToArray();
   double finalCost=(finalCounts.Length>0?finalCounts.Max()-finalCounts.Min():0)*100000-matches.Count*100+variety+balance*40;
   if(finalCost<bestCost){bestCost=finalCost;best=matches;}
  }
  m.Matches=best!.OrderBy(x=>x.Round).ThenBy(x=>x.Court).ToList();
  m.SchedulingNotes=[];
  if(m.Rounds*CourtPlanning.Slot(m)>m.DurationMinutes||m.Matches.Any(x=>m.CourtAvailability.Count>0&&(CourtPlanning.Start(m,x.Round)<CourtPlanning.Minute(m.CourtAvailability[x.Court-1].Start)||CourtPlanning.Start(m,x.Round)+CourtPlanning.Slot(m)>CourtPlanning.Minute(m.CourtAvailability[x.Court-1].End))))m.SchedulingNotes.Add("Some estimated match times fall outside the session or court bookings. Times are guidance; confirm the court is available before starting.");
  if(m.Players.Any(p=>p.AvailableFromRound>0))m.SchedulingNotes.Add("Late players receive a fair share from arrival onward; missed rounds do not create catch-up games.");
  if(m.Players.Any(p=>p.Side!="both")&&Enumerable.Range(from,m.Rounds-from).Any(r=>m.Matches.Count(x=>x.Round==r)<CourtPlanning.Available(m,r).Count))m.SchedulingNotes.Add("Some courts are unused to respect strict playing sides, attendance, fixed partners and fair rotation. Both allows either side.");
  if(m.Mode=="fixed")foreach(var pair in fixedPairs) {
   var remaining=pair.Where(i=>Eligible(i,m.Rounds-1)).ToArray();
   if(remaining.Length==1)m.SchedulingNotes.Add($"{m.Players[remaining[0]].Name} sits out after their fixed partner leaves. Add a remaining-meet replacement from Players to restore the pair.");
  }
  if(m.Mode=="mixed"&&fixedPairs.Any(t=>t.Count(i=>Eligible(i,m.Rounds-1))==1))
   m.SchedulingNotes.Add("Players whose fixed partner has left join the rotating pool for later matches.");
  foreach(var round in Enumerable.Range(from,m.Rounds-from).Where(r=>!m.Matches.Any(x=>x.Round==r)))
   m.SchedulingNotes.Add($"Round {round+1}: no match can be arranged with the eligible players and fixed pairs.");
 }
}
