using System.Text.RegularExpressions;
namespace Rally;
public sealed record ClubPlayerInput(int Revision,string Name,string? ReclubUrl,string? ReclubName,string Side,double? Skill,string? PhotoData=null);
public sealed record LinkPlayerInput(int Revision,string? Email);
public sealed class ClubPlayer {
 public string Id {get;set;}=""; public string Name {get;set;}=""; public int Revision {get;set;}
 public string? ReclubUrl {get;set;} public string ReclubName {get;set;}="";
 public string Side {get;set;}="both";public double? Skill {get;set;} public string? UserId {get;set;}
 public string? Email {get;set;} public string? PhotoUrl {get;set;}
}
public static class ClubPlayers {
 public static string? ProfileUrl(string? value) {
  if(string.IsNullOrWhiteSpace(value))return null;
  MeetRules.Require(Uri.TryCreate(value.Trim(),UriKind.Absolute,out var uri)&&uri.Scheme=="https"&&uri.Host=="reclub.co"&&uri.IsDefaultPort&&uri.UserInfo.Length==0,"Use a public Reclub player profile URL.");
  var match=Regex.Match(uri!.AbsolutePath,@"^/(?:[a-z]{2}(?:-[A-Za-z]{2,4})?/)?players/(@?[A-Za-z0-9._-]{1,100})/?$");
  MeetRules.Require(match.Success,"Use a Reclub player link, such as https://reclub.co/players/@username.");
  return "https://reclub.co/players/"+match.Groups[1].Value.ToLowerInvariant();
 }
 public static (byte[] Bytes,string Mime) Photo(string data) {
  var comma=data.IndexOf(',');MeetRules.Require(comma>0&&data.Length<=280000,"Choose a photo smaller than 200 KB after resizing.");
  var mime=data[..comma] switch {"data:image/jpeg;base64"=>"image/jpeg","data:image/png;base64"=>"image/png","data:image/webp;base64"=>"image/webp",_=>throw new RuleException("Use a JPEG, PNG or WebP photo.")};
  byte[] bytes;try{bytes=Convert.FromBase64String(data[(comma+1)..]);}catch(FormatException){throw new RuleException("Invalid photo.");}
  MeetRules.Require(bytes.Length is >12 and <=204800,"Choose a photo smaller than 200 KB.");
  bool valid=mime=="image/jpeg"?bytes[0]==255&&bytes[1]==216&&bytes[2]==255:
   mime=="image/png"?bytes.Take(8).SequenceEqual(new byte[]{137,80,78,71,13,10,26,10}):
   System.Text.Encoding.ASCII.GetString(bytes,0,4)=="RIFF"&&System.Text.Encoding.ASCII.GetString(bytes,8,4)=="WEBP";
  MeetRules.Require(valid,"The photo contents do not match the selected image type.");return(bytes,mime);
 }
 public static async Task<List<ClubPlayer>> List(Database db,string club,bool organizer) {
  var rows=await db.Query("""SELECT p.id,p.club_id,p.user_id,p.revision,p.name,p.reclub_url,p.reclub_name,p.side,p.skill,(p.photo IS NOT NULL) AS has_photo,u.email FROM club_players p LEFT JOIN users u ON u.id=p.user_id WHERE p.club_id=@club ORDER BY lower(p.name),p.id""",("club",club));
  return rows.Select(r=>new ClubPlayer{Id=(string)r["id"]!,Name=(string)r["name"]!,Revision=(int)r["revision"]!,ReclubUrl=r["reclub_url"] as string,ReclubName=(string)r["reclub_name"]!,Side=(string)r["side"]!,Skill=r["skill"] is double skill?skill:null,UserId=r["user_id"] as string,Email=organizer?r["email"] as string:null,PhotoUrl=(bool)r["has_photo"]!?"/api/clubs/"+club+"/players/"+r["id"]+"/photo?v="+r["revision"]:null}).ToList();
 }
 public static async Task<string> Save(Database db,string club,string actor,string? id,ClubPlayerInput input) {
  var name=(input.Name??"").Trim();MeetRules.Require(name.Length is >=1 and <=80,"Enter a club name for the player, up to 80 characters.");
  var reclubName=(input.ReclubName??"").Trim();MeetRules.Require(reclubName.Length<=80,"Reclub name must be at most 80 characters.");
  var url=ProfileUrl(input.ReclubUrl);MeetRules.ValidateSide(input.Side);if(input.Skill.HasValue)MeetRules.ValidateSkill(input.Skill.Value);
  if(!string.IsNullOrEmpty(input.PhotoData))Photo(input.PhotoData);
  bool creating=id==null;id??=Guid.NewGuid().ToString("N");
  await using var c=await db.Open();await using var tx=await c.BeginTransactionAsync();
  var sql=creating?"""
   INSERT INTO club_players(id,club_id,name,reclub_url,reclub_name,side,skill,photo)
   VALUES(@id,@club,@name,@url,@reclubName,@side,@skill,NULLIF(@photo,''))
   """:"""
   UPDATE club_players SET name=@name,reclub_url=@url,reclub_name=@reclubName,side=@side,skill=@skill,
   photo=CASE WHEN @photo::text IS NULL THEN photo ELSE NULLIF(@photo,'') END,revision=revision+1
   WHERE id=@id AND club_id=@club AND revision=@revision
   """;
  await using(var cmd=Database.Command(c,sql,("id",id),("club",club),("name",name),("url",url),("reclubName",reclubName),("side",input.Side),("skill",input.Skill),("photo",input.PhotoData),("revision",input.Revision)))
   MeetRules.Require(await cmd.ExecuteNonQueryAsync()==1,"This profile changed or is unavailable. Refresh and retry.",409);
  await using(var audit=Database.Command(c,"INSERT INTO club_player_audit(club_id,player_id,actor_id,action) VALUES(@club,@id,@actor,@action)",("club",club),("id",id),("actor",actor),("action",creating?"create":"edit")))await audit.ExecuteNonQueryAsync();
  await tx.CommitAsync();return id;
 }
 public static async Task Link(Database db,string club,string id,string actor,LinkPlayerInput input) {
  await using var c=await db.Open();await using var tx=await c.BeginTransactionAsync();
  string? user=null;
  if(!string.IsNullOrWhiteSpace(input.Email)){
   await using var find=Database.Command(c,"SELECT id FROM users WHERE email=@email AND NOT disabled FOR KEY SHARE",("email",input.Email.Trim().ToLowerInvariant()));
   user=await find.ExecuteScalarAsync() as string;MeetRules.Require(user!=null,"This person must register an active account with that email first.");
  }
  await using(var update=Database.Command(c,"UPDATE club_players SET user_id=@user,revision=revision+1 WHERE id=@id AND club_id=@club AND revision=@revision",("user",user),("id",id),("club",club),("revision",input.Revision)))
   MeetRules.Require(await update.ExecuteNonQueryAsync()==1,"This profile changed or is unavailable. Refresh and retry.",409);
  if(user!=null){await using var member=Database.Command(c,"INSERT INTO club_members(club_id,user_id,role) VALUES(@club,@user,'member') ON CONFLICT DO NOTHING",("club",club),("user",user));await member.ExecuteNonQueryAsync();}
  await using(var audit=Database.Command(c,"INSERT INTO club_player_audit(club_id,player_id,actor_id,action) VALUES(@club,@id,@actor,@action)",("club",club),("id",id),("actor",actor),("action",user==null?"unlink-account":"link-account")))await audit.ExecuteNonQueryAsync();
  await tx.CommitAsync();
 }
 public static async Task Resolve(Database db,Meet meet) {
  var profiles=await List(db,meet.ClubId,false);
  foreach(var p in meet.Players){
   p.UserId=null;
   var url=ProfileUrl(p.ReclubProfileUrl);
   var profile=p.ClubPlayerId!=null?profiles.SingleOrDefault(x=>x.Id==p.ClubPlayerId):url==null?null:profiles.SingleOrDefault(x=>x.ReclubUrl==url);
   MeetRules.Require(p.ClubPlayerId==null||profile!=null,"Choose a player profile from this club.");
   p.ReclubProfileUrl=url;
   if(profile==null)continue;
   p.ClubPlayerId=profile.Id;p.UserId=profile.UserId;p.Name=profile.Name;p.Side=profile.Side;p.Skill=profile.Skill;p.ReclubProfileUrl=profile.ReclubUrl;
  }
  MeetRules.Require(meet.Players.Where(p=>p.ClubPlayerId!=null).Select(p=>p.ClubPlayerId).Distinct().Count()==meet.Players.Count(p=>p.ClubPlayerId!=null),"A club player can only appear once in a meet.");
 }
}
