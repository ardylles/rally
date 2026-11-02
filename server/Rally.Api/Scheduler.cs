namespace Rally;
public static class Scheduler {
 public static void Generate(Meet m,int seed=91) {
  MeetRules.CanGenerate(m);MeetRules.Validate(m);
  var rng=new Random(seed);var n=m.Players.Count;
  var fixedPairs=m.Mode is "fixed" or "mixed"?m.FixedPairs.Select(t=>t.Select(id=>m.Players.FindIndex(p=>p.Id==id)).ToArray()).ToList():[];
  var locked=fixedPairs.SelectMany(t=>t).ToHashSet();
  var pool=Enumerable.Range(0,n).Where(i=>!locked.Contains(i)).ToArray();
  List<Match>? best=null;double bestCost=double.MaxValue;
  string Key(int a,int b)=>a<b?$"{a}:{b}":$"{b}:{a}";
  List<T> Shuffle<T>(IEnumerable<T> values) { var a=values.ToList();for(int i=a.Count-1;i>0;i--){int j=rng.Next(i+1);(a[i],a[j])=(a[j],a[i]);}return a; }
  for(int attempt=0;attempt<24;attempt++) {
   var counts=new int[n];var rests=new int[n];var partners=new Dictionary<string,int>();var opponents=new Dictionary<string,int>();var matches=new List<Match>();double variety=0,balance=0;
   for(int round=0;round<m.Rounds;round++) {
    var available= CourtPlanning.Available(m,round);
    List<int[]>? choice=null;double roundBest=double.MaxValue;
    for(int trial=0;trial<70;trial++) {
     var singles=Shuffle(pool).OrderBy(i=>counts[i]).ToList();
     var teams=fixedPairs.Select(t=>t.ToArray()).ToList();
     for(int i=0;i+1<singles.Count;i+=2)teams.Add([singles[i],singles[i+1]]);
     int capacity=Math.Min(available.Count,teams.Count/2);
     int used=attempt>=12&&trial%4==0&&capacity>1?capacity-1:capacity;
     var selected=Shuffle(Shuffle(teams).OrderBy(t=>counts[t[0]]+counts[t[1]]+rng.NextDouble()*1.6).Take(used*2));
     var ids=selected.SelectMany(t=>t).ToHashSet();var next=counts.Select((c,i)=>c+(ids.Contains(i)?1:0)).ToArray();double mean=next.Average(),repeat=0,gap=0;
     for(int i=0;i<selected.Count;i+=2) {
      foreach(var team in new[]{selected[i],selected[i+1]})if(!locked.Contains(team[0]))repeat+=partners.GetValueOrDefault(Key(team[0],team[1]))*5;
      foreach(var a in selected[i])foreach(var b in selected[i+1])repeat+=opponents.GetValueOrDefault(Key(a,b));
      if(m.Mode=="balanced")gap+=Math.Abs(selected[i].Sum(p=>m.Players[p].Skill!.Value)-selected[i+1].Sum(p=>m.Players[p].Skill!.Value));
     }
     double rest=Enumerable.Range(0,n).Where(i=>!ids.Contains(i)).Sum(i=>Math.Pow(rests[i]+1,2));
     double cost=(next.Max()-next.Min())*10000+next.Sum(c=>Math.Pow(c-mean,2))*100+rest*10-selected.Count*20+repeat+gap*40;
     if(cost<roundBest){roundBest=cost;choice=selected;}
    }
    choice ??= [];
    var active=choice.SelectMany(t=>t).ToHashSet();
    for(int i=0;i<n;i++){if(active.Contains(i))counts[i]++;rests[i]=active.Contains(i)?0:rests[i]+1;}
    for(int c=0;c<choice.Count;c+=2) {
     var teams=new[]{choice[c],choice[c+1]};
     foreach(var team in teams){var k=Key(team[0],team[1]);if(!locked.Contains(team[0]))variety+=partners.GetValueOrDefault(k);partners[k]=partners.GetValueOrDefault(k)+1;}
     foreach(var a in teams[0])foreach(var b in teams[1]){var k=Key(a,b);opponents[k]=opponents.GetValueOrDefault(k)+1;}
     if(m.Mode=="balanced")balance+=Math.Abs(teams[0].Sum(i=>m.Players[i].Skill!.Value)-teams[1].Sum(i=>m.Players[i].Skill!.Value));
     matches.Add(new(){Round=round,Court=available[c/2],PlannedStart=CourtPlanning.Clock(CourtPlanning.Start(m,round)),PlannedEnd=CourtPlanning.Clock(CourtPlanning.Start(m,round)+CourtPlanning.Slot(m)),Teams=teams.Select(t=>t.Select(i=>m.Players[i].Id).ToArray()).ToArray(),Format=m.Format});
    }
   }
   double finalCost=(counts.Max()-counts.Min())*100000-matches.Count*100+variety+balance*40;
   if(finalCost<bestCost){bestCost=finalCost;best=matches;}
  }
  m.Matches=best!;
 }
}
