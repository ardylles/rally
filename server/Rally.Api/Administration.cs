using System.Security.Claims;
using System.Text.Json;
namespace Rally;
public static class Administration {
 public static async Task RequireAdmin(Database db,string id) {
  var rows=await db.Query("SELECT 1 FROM users WHERE id=@id AND is_admin AND NOT disabled",("id",id));
  MeetRules.Require(rows.Count==1,"Application administrator access required.",403);
 }
 public static async Task ChangeUser(Database db,string actor,string target,bool? disabled) {
  await using var c=await db.Open();await using var tx=await c.BeginTransactionAsync();
  // Serialize administrator changes so concurrent requests cannot remove the last admin.
  await using(var guard=Database.Command(c,"LOCK TABLE users IN SHARE ROW EXCLUSIVE MODE"))await guard.ExecuteNonQueryAsync();
  string name;bool admin;bool active;
  await using(var cmd=Database.Command(c,"SELECT name,is_admin,NOT disabled FROM users WHERE id=@id",("id",target)))
  await using(var reader=await cmd.ExecuteReaderAsync()){
   MeetRules.Require(await reader.ReadAsync(),"User not found.",404);
   name=reader.GetString(0);admin=reader.GetBoolean(1);active=reader.GetBoolean(2);
  }
  if(admin&&active&&disabled!=false){
   await using var count=Database.Command(c,"SELECT count(*) FROM users WHERE is_admin AND NOT disabled AND id<>@id",("id",target));
   MeetRules.Require((long)(await count.ExecuteScalarAsync())!>0,"Keep at least one active application administrator.");
  }
  // Recheck inside the serialized transaction.
  await using(var check=Database.Command(c,"SELECT count(*) FROM users WHERE id=@id AND is_admin AND NOT disabled",("id",actor)))
   MeetRules.Require((long)(await check.ExecuteScalarAsync())!>0,"Application administrator access required.",403);
  var action=disabled==null?"delete-user":disabled.Value?"disable-user":"enable-user";
  await using(var audit=Database.Command(c,"INSERT INTO admin_audit(actor_id,actor_name,action,target_id,target_name) SELECT id,name,@action,@target,@name FROM users WHERE id=@actor",("actor",actor),("action",action),("target",target),("name",name)))await audit.ExecuteNonQueryAsync();
  if(disabled==null){
   // Only detach account links. Participant identity, results and statistics are retained verbatim.
   await using(var unlink=Database.Command(c,"""
    UPDATE meets SET document=jsonb_set(document,'{players}',(
     SELECT jsonb_agg(CASE WHEN p->>'userId'=@id THEN jsonb_set(p,'{userId}','null'::jsonb) ELSE p END ORDER BY ord)
     FROM jsonb_array_elements(document->'players') WITH ORDINALITY AS players(p,ord))),
     revision=revision+1,updated_at=now()
    WHERE EXISTS(SELECT 1 FROM jsonb_array_elements(document->'players') p WHERE p->>'userId'=@id);
    DELETE FROM meet_organizers WHERE user_id=@id;
    DELETE FROM club_members WHERE user_id=@id;
    DELETE FROM users WHERE id=@id;
    """,("id",target)))await unlink.ExecuteNonQueryAsync();
  }else{
   await using var update=Database.Command(c,"UPDATE users SET disabled=@disabled,session_version=session_version+1 WHERE id=@id",("id",target),("disabled",disabled.Value));await update.ExecuteNonQueryAsync();
  }
  await tx.CommitAsync();
 }
 public static async Task DeleteMeet(Database db,string actor,string id) {
  await using var c=await db.Open();await using var tx=await c.BeginTransactionAsync();
  await using var read=Database.Command(c,"SELECT document->>'name' FROM meets WHERE id=@id FOR UPDATE",("id",id));
  var name=await read.ExecuteScalarAsync();MeetRules.Require(name is string,"Meet not found.",404);
  await using var cmd=Database.Command(c,"""
   INSERT INTO admin_audit(actor_id,actor_name,action,target_id,target_name) SELECT id,name,'delete-meet',@target,@name FROM users WHERE id=@actor;
   DELETE FROM meet_organizers WHERE meet_id=@target;
   DELETE FROM audit_events WHERE meet_id=@target;
   DELETE FROM meets WHERE id=@target;
   """,("actor",actor),("target",id),("name",name));
  await cmd.ExecuteNonQueryAsync();await tx.CommitAsync();
 }
}
