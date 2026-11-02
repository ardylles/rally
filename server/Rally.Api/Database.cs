using Npgsql;
using System.Text.Json;
namespace Rally;
public sealed class Database(NpgsqlDataSource source) {
 public async Task<NpgsqlConnection> Open()=>await source.OpenConnectionAsync();
 public static NpgsqlCommand Command(NpgsqlConnection connection,string sql,params (string,object?)[] values) {
  var cmd=new NpgsqlCommand(sql,connection);foreach(var (name,value) in values)cmd.Parameters.AddWithValue(name,value??DBNull.Value);return cmd;
 }
 public async Task<List<Dictionary<string,object?>>> Query(string sql,params (string,object?)[] values) {
  await using var c=await Open();await using var cmd=Command(c,sql,values);await using var reader=await cmd.ExecuteReaderAsync();var rows=new List<Dictionary<string,object?>>();
  while(await reader.ReadAsync()){var row=new Dictionary<string,object?>();for(int i=0;i<reader.FieldCount;i++)row[reader.GetName(i)]=reader.IsDBNull(i)?null:reader.GetValue(i);rows.Add(row);}return rows;
 }
 public async Task<int> Execute(string sql,params (string,object?)[] values) {await using var c=await Open();await using var cmd=Command(c,sql,values);return await cmd.ExecuteNonQueryAsync();}
 public async Task Initialize() {
  await using var c=await Open();await using var tx=await c.BeginTransactionAsync();
  await using(var migrationLock=Command(c,"SELECT pg_advisory_xact_lock(784243993)"))await migrationLock.ExecuteNonQueryAsync();
  await using var cmd=Command(c,Schema);await cmd.ExecuteNonQueryAsync();
  await using var migration=Command(c,Migration2);await migration.ExecuteNonQueryAsync();await tx.CommitAsync();
 }
 public async Task<Meet> GetMeet(string id) {
  var rows=await Query("SELECT document::text AS document,revision FROM meets WHERE id=@id",("id",id));
  if(rows.Count==0)throw new RuleException("Meet not found.",404);
  var m=JsonSerializer.Deserialize<Meet>((string)rows[0]["document"]!,Json.Options)!;m.Revision=(int)rows[0]["revision"]!;return m;
 }
 public async Task Save(Meet m,int expected,string actor,string action,string? publicToken=null) {
  await using var c=await Open();await using var tx=await c.BeginTransactionAsync();
  // Lock the current row before snapshotting; stale clients never overwrite newer edits.
  await using var read=Command(c,"SELECT document::text,revision FROM meets WHERE id=@id FOR UPDATE",("id",m.Id));
  string before;int version;
  await using(var reader=await read.ExecuteReaderAsync()){if(!await reader.ReadAsync())throw new RuleException("Meet not found.",404);before=reader.GetString(0);version=reader.GetInt32(1);}
  if(version!=expected)throw new RuleException("This meet changed. Refresh it and retry your change.",409);
  m.Revision=expected+1;
  await using var update=Command(c,"UPDATE meets SET document=@doc::jsonb,revision=@revision,updated_at=now() WHERE id=@id",("doc",JsonSerializer.Serialize(m,Json.Options)),("revision",m.Revision),("id",m.Id));await update.ExecuteNonQueryAsync();
  if(publicToken!=null){await using var share=Command(c,"UPDATE meets SET public_token=@token WHERE id=@id",("token",publicToken),("id",m.Id));await share.ExecuteNonQueryAsync();}
  await using var audit=Command(c,"INSERT INTO audit_events(meet_id,actor_id,actor_name,action,previous_document,revision) VALUES(@id,@actor,(SELECT name FROM users WHERE id=@actor),@action,@before::jsonb,@version)",("id",m.Id),("actor",actor),("action",action),("before",before),("version",version));await audit.ExecuteNonQueryAsync();await tx.CommitAsync();
 }
 public const string Migration2="""
 DO $migration$ BEGIN
 IF NOT EXISTS(SELECT 1 FROM schema_versions WHERE version=2) THEN
 ALTER TABLE users ADD COLUMN is_admin boolean NOT NULL DEFAULT false, ADD COLUMN disabled boolean NOT NULL DEFAULT false, ADD COLUMN session_version integer NOT NULL DEFAULT 0;
 ALTER TABLE meets ADD COLUMN public_token text UNIQUE;
 ALTER TABLE clubs ALTER COLUMN created_by DROP NOT NULL;
 ALTER TABLE clubs DROP CONSTRAINT clubs_created_by_fkey, ADD FOREIGN KEY(created_by) REFERENCES users(id) ON DELETE SET NULL;
 ALTER TABLE meets ALTER COLUMN created_by DROP NOT NULL;
 ALTER TABLE meets DROP CONSTRAINT meets_created_by_fkey, ADD FOREIGN KEY(created_by) REFERENCES users(id) ON DELETE SET NULL;
 ALTER TABLE audit_events ALTER COLUMN actor_id DROP NOT NULL;
 ALTER TABLE audit_events ADD COLUMN actor_name text;
 UPDATE audit_events a SET actor_name=u.name FROM users u WHERE u.id=a.actor_id;
 ALTER TABLE audit_events DROP CONSTRAINT audit_events_actor_id_fkey, ADD FOREIGN KEY(actor_id) REFERENCES users(id) ON DELETE SET NULL;
 ALTER TABLE audit_events DROP CONSTRAINT audit_events_meet_id_fkey, ADD FOREIGN KEY(meet_id) REFERENCES meets(id) ON DELETE SET NULL;
 CREATE TABLE admin_audit(id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,actor_id text REFERENCES users(id) ON DELETE SET NULL,actor_name text NOT NULL,action text NOT NULL,target_id text NOT NULL,target_name text NOT NULL,created_at timestamptz NOT NULL DEFAULT now());
 INSERT INTO schema_versions(version) VALUES(2);
 END IF;
 END $migration$;
 """;
 public const string Schema="""
 CREATE TABLE IF NOT EXISTS schema_versions(version integer PRIMARY KEY, applied_at timestamptz NOT NULL DEFAULT now());
 CREATE TABLE IF NOT EXISTS users(id text PRIMARY KEY,name text NOT NULL,email text NOT NULL UNIQUE,password_hash text NOT NULL,created_at timestamptz NOT NULL DEFAULT now());
 CREATE TABLE IF NOT EXISTS clubs(id text PRIMARY KEY,name text NOT NULL,created_by text NOT NULL REFERENCES users(id));
 CREATE TABLE IF NOT EXISTS club_members(club_id text NOT NULL REFERENCES clubs(id),user_id text NOT NULL REFERENCES users(id),role text NOT NULL CHECK(role IN ('organizer','member')),PRIMARY KEY(club_id,user_id));
 CREATE TABLE IF NOT EXISTS meets(id text PRIMARY KEY,club_id text NOT NULL REFERENCES clubs(id),created_by text NOT NULL REFERENCES users(id),revision integer NOT NULL DEFAULT 1,document jsonb NOT NULL,updated_at timestamptz NOT NULL DEFAULT now());
 CREATE TABLE IF NOT EXISTS meet_organizers(meet_id text NOT NULL REFERENCES meets(id),user_id text NOT NULL REFERENCES users(id),PRIMARY KEY(meet_id,user_id));
 CREATE TABLE IF NOT EXISTS audit_events(id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,meet_id text REFERENCES meets(id),actor_id text NOT NULL REFERENCES users(id),action text NOT NULL,previous_document jsonb,revision integer,created_at timestamptz NOT NULL DEFAULT now());
 CREATE INDEX IF NOT EXISTS meets_club_idx ON meets(club_id);
 CREATE INDEX IF NOT EXISTS audit_meet_idx ON audit_events(meet_id,created_at);
 INSERT INTO schema_versions(version) VALUES(1) ON CONFLICT DO NOTHING;
 """;
}
