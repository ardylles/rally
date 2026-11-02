namespace Rally;
public static class CourtPlanning {
 public static int Minute(string value) => TimeOnly.ParseExact(value,"HH:mm").Hour*60+TimeOnly.ParseExact(value,"HH:mm").Minute;
 public static int Slot(Meet m)=>m.SlotMinutes==0?m.DurationMinutes/m.Rounds:m.SlotMinutes;
 public static int Start(Meet m,int round)=>Minute(m.Time)+round*Slot(m);
 public static string Clock(int minute)=>$"{minute/60%24:00}:{minute%60:00}"+(minute>=1440?$" (+{minute/1440}d)":"");
 public static List<int> Available(Meet m,int round) {
  int start=Start(m,round),end=start+Slot(m);
  var preferred=Enumerable.Range(1,m.Courts).Where(c=>m.CourtAvailability.Count==0 ||
   (Minute(m.CourtAvailability[c-1].Start)<=start&&Minute(m.CourtAvailability[c-1].End)>=end)).ToList();
  return preferred.Count>0?preferred:Enumerable.Range(1,m.Courts).ToList();
 }
 public static void Validate(Meet m) {
  m.CourtAvailability ??= [];
  MeetRules.Require(m.SlotMinutes==0 || m.SlotMinutes is >=5 and <=120,"Planned round length must be 5–120 minutes.");
  MeetRules.Require(Slot(m)>0,"Enter a positive planned round length.");
  int start=Minute(m.Time),end=start+m.DurationMinutes;
  MeetRules.Require(end<1440,"Keep the session within one calendar day.");
  MeetRules.Require(m.CourtAvailability.Count==0||m.CourtAvailability.Count==m.Courts,"Enter availability for every court, or leave all courts at full session availability.");
  foreach(var court in m.CourtAvailability) {
   MeetRules.Require(court!=null&&!string.IsNullOrWhiteSpace(court.Name)&&court.Name.Trim().Length<=60,"Each court needs a name of up to 60 characters.");
   court!.Name=court.Name.Trim();
   MeetRules.Require(TimeOnly.TryParseExact(court.Start,"HH:mm",out _)&&TimeOnly.TryParseExact(court.End,"HH:mm",out _),"Enter valid court opening and closing times.");
   MeetRules.Require(Minute(court.Start)<Minute(court.End),$"{court.Name}: end time {court.End} must be later than start time {court.Start}.");

  }
  MeetRules.Require(m.CourtAvailability.Select(c=>c.Name.ToUpperInvariant()).Distinct().Count()==m.CourtAvailability.Count,"Court names must be unique.");

 }
}
