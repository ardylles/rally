using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.RateLimiting;
using Npgsql;
using Rally;
using System.Threading.RateLimiting;
var builder=WebApplication.CreateBuilder(args);
var connections=DatabaseConfiguration.Resolve(builder.Configuration);
builder.WebHost.ConfigureKestrel(o=>o.Limits.MaxRequestBodySize=1024*1024);
builder.Services.AddSingleton(NpgsqlDataSource.Create(connections.Application));builder.Services.AddSingleton<Database>();
var keysPath=builder.Configuration["DataProtection:KeysPath"]??Path.Combine(builder.Environment.ContentRootPath,"../../.local/keys");
builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(keysPath));
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(o=>{
 o.Cookie.Name="rally.session";o.Cookie.HttpOnly=true;o.Cookie.SameSite=SameSiteMode.Lax;o.Cookie.SecurePolicy=CookieSecurePolicy.SameAsRequest;o.ExpireTimeSpan=TimeSpan.FromDays(7);o.SlidingExpiration=true;
 o.Events.OnRedirectToLogin=c=>{c.Response.StatusCode=401;return Task.CompletedTask;};o.Events.OnRedirectToAccessDenied=c=>{c.Response.StatusCode=403;return Task.CompletedTask;};
});
builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(o=>{o.RejectionStatusCode=429;o.AddPolicy("auth",c=>RateLimitPartition.GetFixedWindowLimiter(c.Connection.RemoteIpAddress?.ToString()??"unknown",_=>new(){PermitLimit=20,Window=TimeSpan.FromMinutes(1),QueueLimit=0}));});
builder.Services.ConfigureHttpJsonOptions(o=>{o.SerializerOptions.PropertyNamingPolicy=JsonNamingPolicy.CamelCase;});
var app=builder.Build();
app.Use(async(ctx,next)=>{
 try{
  if(ctx.Request.Path.StartsWithSegments("/api")&&ctx.Request.Method is not ("GET" or "HEAD" or "OPTIONS")&&ctx.Request.Headers["X-Rally-Request"]!="1")throw new RuleException("Invalid request origin.",403);
  await next();
 }catch(RuleException e){ctx.Response.StatusCode=e.Status;await ctx.Response.WriteAsJsonAsync(new{message=e.Message});}
 catch(PostgresException e) when(e.SqlState=="23505"){ctx.Response.StatusCode=409;await ctx.Response.WriteAsJsonAsync(new{message="That record already exists."});}
});
app.UseRateLimiter();app.UseAuthentication();app.UseAuthorization();
app.UseDefaultFiles();app.UseStaticFiles();
// Schema setup uses a direct connection, independently of the pooled request data source.
await using(var schemaSource=NpgsqlDataSource.Create(connections.Schema))
 await new Database(schemaSource).Initialize();
string UserId(HttpContext c)=>c.User.FindFirstValue(ClaimTypes.NameIdentifier)??throw new RuleException("Sign in to continue.",401);
async Task<string> ClubRole(Database db,string club,string user) {var rows=await db.Query("SELECT role FROM club_members WHERE club_id=@club AND user_id=@user",("club",club),("user",user));if(rows.Count==0)throw new RuleException("Club access denied.",403);return (string)rows[0]["role"]!;}
async Task RequireClub(Database db,string club,string user){MeetRules.Require(await ClubRole(db,club,user)=="organizer","Club organizer access required.",403);}
async Task<bool> CanManage(Database db,Meet m,string user){var role=await ClubRole(db,m.ClubId,user);if(role=="organizer")return true;return (await db.Query("SELECT 1 FROM meet_organizers WHERE meet_id=@meet AND user_id=@user",("meet",m.Id),("user",user))).Count>0;}
async Task<Meet> Managed(Database db,string id,string user){var m=await db.GetMeet(id);MeetRules.Require(await CanManage(db,m,user),"Meet organizer access required.",403);return m;}
async Task SignIn(HttpContext ctx,string id,string name){await ctx.SignInAsync(new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,id),new Claim(ClaimTypes.Name,name)],CookieAuthenticationDefaults.AuthenticationScheme)));}
string Hash(string password){var salt=RandomNumberGenerator.GetBytes(16);var bytes=Rfc2898DeriveBytes.Pbkdf2(password,salt,210000,HashAlgorithmName.SHA256,32);return Convert.ToBase64String(salt)+"."+Convert.ToBase64String(bytes);}
bool Verify(string password,string stored){var parts=stored.Split('.');return CryptographicOperations.FixedTimeEquals(Convert.FromBase64String(parts[1]),Rfc2898DeriveBytes.Pbkdf2(password,Convert.FromBase64String(parts[0]),210000,HashAlgorithmName.SHA256,32));}
app.MapPost("/api/clubs/{club}/reclub/preview",async(string club,ReclubInput input,Database db,HttpContext ctx)=>{await RequireClub(db,club,UserId(ctx));return await ReclubScraper.Fetch(input.Url,ctx.RequestAborted);}).RequireAuthorization().RequireRateLimiting("auth");
app.MapGet("/api/health",async(Database db)=>{await db.Query("SELECT 1");return Results.Ok(new{status="ready",database="PostgreSQL"});});
app.MapPost("/api/auth/register",async(Credentials input,Database db,HttpContext ctx)=>{
 var name=(input.Name??"").Trim();var email=(input.Email??"").Trim().ToLowerInvariant();
 MeetRules.Require(name.Length is >=1 and <=80,"Enter your name.");
 MeetRules.Require(email.Length<=254&&System.Net.Mail.MailAddress.TryCreate(email,out var address)&&address.Address==email,"Enter a valid email address.");
 MeetRules.Require((input.Password?.Length??0) is >=12 and <=128,"Use a password of 12–128 characters.");
 var id=Guid.NewGuid().ToString("N");await db.Execute("INSERT INTO users(id,name,email,password_hash) VALUES(@id,@name,@email,@hash)",("id",id),("name",name),("email",email),("hash",Hash(input.Password!)));await SignIn(ctx,id,name);return new UserInfo(id,name,email);
}).RequireRateLimiting("auth");
app.MapPost("/api/auth/login",async(Credentials input,Database db,HttpContext ctx)=>{
 MeetRules.Require(input.Password is {Length:>=1 and <=128},"Invalid email or password.",401);
 var rows=await db.Query("SELECT id,name,email,password_hash FROM users WHERE email=@email",("email",(input.Email??"").Trim().ToLowerInvariant()));
 MeetRules.Require(rows.Count==1&&Verify(input.Password!,(string)rows[0]["password_hash"]!),"Invalid email or password.",401);
 var row=rows[0];await SignIn(ctx,(string)row["id"]!,(string)row["name"]!);return new UserInfo((string)row["id"]!,(string)row["name"]!,(string)row["email"]!);
}).RequireRateLimiting("auth");
app.MapPost("/api/auth/logout",async(HttpContext ctx)=>{await ctx.SignOutAsync();return Results.NoContent();});
app.MapGet("/api/auth/me",async(Database db,HttpContext ctx)=>{var id=UserId(ctx);return (await db.Query("SELECT id,name,email FROM users WHERE id=@id",("id",id))).Single();}).RequireAuthorization();
var api=app.MapGroup("/api").RequireAuthorization();
api.MapGet("/clubs",async(Database db,HttpContext ctx)=>await db.Query("SELECT c.id,c.name,m.role FROM clubs c JOIN club_members m ON m.club_id=c.id WHERE m.user_id=@user ORDER BY c.name",("user",UserId(ctx))));
api.MapPost("/clubs",async(ClubInput input,Database db,HttpContext ctx)=>{
 var name=(input.Name??"").Trim();MeetRules.Require(name.Length is >=1 and <=100,"Enter a club name.");var user=UserId(ctx);var id=Guid.NewGuid().ToString("N");
 await using var c=await db.Open();await using var tx=await c.BeginTransactionAsync();
 await using var cmd=Database.Command(c,"INSERT INTO clubs(id,name,created_by) VALUES(@id,@name,@user);INSERT INTO club_members(club_id,user_id,role) VALUES(@id,@user,'organizer')",("id",id),("name",name),("user",user));await cmd.ExecuteNonQueryAsync();await tx.CommitAsync();return new{id,name,role="organizer"};
});
api.MapGet("/clubs/{club}/members",async(string club,Database db,HttpContext ctx)=>{await ClubRole(db,club,UserId(ctx));return await db.Query("SELECT u.id,u.name,u.email,m.role FROM club_members m JOIN users u ON u.id=m.user_id WHERE m.club_id=@club ORDER BY u.name",("club",club));});
api.MapPost("/clubs/{club}/members",async(string club,MemberInput input,Database db,HttpContext ctx)=>{
 await RequireClub(db,club,UserId(ctx));MeetRules.Require(input.Role is "member" or "organizer","Choose a role.");var rows=await db.Query("SELECT id FROM users WHERE email=@email",("email",input.Email.Trim().ToLowerInvariant()));MeetRules.Require(rows.Count==1,"This person needs to register an account first.");var user=(string)rows[0]["id"]!;
 await db.Execute("INSERT INTO club_members(club_id,user_id,role) VALUES(@club,@user,@role)",("club",club),("user",user),("role",input.Role));return Results.NoContent();
});
api.MapPut("/clubs/{club}/roles",async(string club,RoleInput input,Database db,HttpContext ctx)=>{
 await RequireClub(db,club,UserId(ctx));MeetRules.Require(input.Role is "organizer" or "member","Choose a role.");
 await using var c=await db.Open();await using var tx=await c.BeginTransactionAsync();
 await using(var guard=Database.Command(c,"SELECT id FROM clubs WHERE id=@club FOR UPDATE",("club",club)))await guard.ExecuteScalarAsync();
 if(input.Role=="member"){await using var count=Database.Command(c,"SELECT count(*) FROM club_members WHERE club_id=@club AND role='organizer' AND user_id<>@user",("club",club),("user",input.UserId));MeetRules.Require((long)(await count.ExecuteScalarAsync())!>0,"Keep at least one club organizer.");}
 await using var cmd=Database.Command(c,"UPDATE club_members SET role=@role WHERE club_id=@club AND user_id=@user",("role",input.Role),("club",club),("user",input.UserId));MeetRules.Require(await cmd.ExecuteNonQueryAsync()==1,"Member not found.",404);await tx.CommitAsync();return Results.NoContent();
});
api.MapGet("/clubs/{club}/meets",async(string club,Database db,HttpContext ctx)=>{
 var user=UserId(ctx);var role=await ClubRole(db,club,user);
 return await db.Query("""SELECT id,document->>'name' AS name,document->>'date' AS date,document->>'mode' AS mode,revision,(@organizer OR EXISTS(SELECT 1 FROM meet_organizers mo WHERE mo.meet_id=meets.id AND mo.user_id=@user)) AS "canManage" FROM meets WHERE club_id=@club ORDER BY document->>'date' DESC, updated_at DESC""",("club",club),("user",user),("organizer",role=="organizer"));
});
api.MapPost("/clubs/{club}/meets",async(string club,Meet input,Database db,HttpContext ctx)=>{
 var user=UserId(ctx);await RequireClub(db,club,user);input.Id=Guid.NewGuid().ToString("N");input.ClubId=club;input.Revision=1;input.CreatedAt=DateTimeOffset.UtcNow;input.Matches=[];
 // Client player identifiers are meet-local, not account ownership claims.
 input.Players ??= []; MeetRules.Require(input.Players.All(p=>p!=null),"Invalid player record.");
 foreach(var p in input.Players)p.UserId=null;
 Scheduler.Generate(input);
 await db.Execute("INSERT INTO meets(id,club_id,created_by,revision,document) VALUES(@id,@club,@user,1,@doc::jsonb)",("id",input.Id),("club",club),("user",user),("doc",JsonSerializer.Serialize(input,Json.Options)));return input;
});
api.MapGet("/meets/{id}",async(string id,Database db,HttpContext ctx)=>{var m=await db.GetMeet(id);await ClubRole(db,m.ClubId,UserId(ctx));return m;});
api.MapPut("/meets/{id}/settings",async(string id,Meet input,Database db,HttpContext ctx)=>{
 var user=UserId(ctx);var m=await Managed(db,id,user);MeetRules.CanGenerate(m);MeetRules.Require(input.Players.Select(p=>p.Id).SequenceEqual(m.Players.Select(p=>p.Id)),"Use the roster controls to change players.");input.Id=id;input.ClubId=m.ClubId;input.CreatedAt=m.CreatedAt;input.Matches=[];foreach(var p in input.Players)p.UserId=m.Players.Single(old=>old.Id==p.Id).UserId;Scheduler.Generate(input);await db.Save(input,input.Revision,user,"settings");return input;
});
api.MapPost("/meets/{id}/generate",async(string id,RevisionInput input,Database db,HttpContext ctx)=>{var user=UserId(ctx);var m=await Managed(db,id,user);Scheduler.Generate(m);await db.Save(m,input.Revision,user,"generate");return m;});
api.MapPut("/meets/{id}/pairs",async(string id,PairInput input,Database db,HttpContext ctx)=>{var user=UserId(ctx);var m=await Managed(db,id,user);MeetRules.Require(m.Mode is "fixed" or "mixed","This mode does not use fixed pairs.");MeetRules.CanGenerate(m);m.FixedPairs=input.Pairs;Scheduler.Generate(m);await db.Save(m,input.Revision,user,"fixed-pairs");return m;});
api.MapPut("/meets/{id}/skills",async(string id,SkillInput input,Database db,HttpContext ctx)=>{var user=UserId(ctx);var m=await Managed(db,id,user);MeetRules.Require(m.Mode=="balanced","Skills are editable only in balanced meets.");MeetRules.ValidateSkill(input.Skill);var p=m.Players.FirstOrDefault(p=>p.Id==input.PlayerId)??throw new RuleException("Player not found.",404);p.Skill=input.Skill;await db.Save(m,input.Revision,user,"skill");return m;});
api.MapPost("/meets/{id}/replacements",async(string id,ReplacementInput input,Database db,HttpContext ctx)=>{var user=UserId(ctx);var m=await Managed(db,id,user);MeetRules.Replacement(m,input);await db.Save(m,input.Revision,user,"replacement");return m;});
api.MapPost("/meets/{id}/matches/{matchId}/start",async(string id,string matchId,RevisionInput input,Database db,HttpContext ctx)=>{var user=UserId(ctx);var m=await Managed(db,id,user);MeetRules.Start(m,MeetRules.Find(m,matchId));await db.Save(m,input.Revision,user,"match-start");return m;});
api.MapPut("/meets/{id}/matches/{matchId}/result",async(string id,string matchId,ResultInput input,Database db,HttpContext ctx)=>{var user=UserId(ctx);var m=await Managed(db,id,user);MeetRules.Result(m,MeetRules.Find(m,matchId),input);await db.Save(m,input.Revision,user,"match-result");return m;});
api.MapPost("/meets/{id}/matches/{matchId}/substitute",async(string id,string matchId,SubstituteInput input,Database db,HttpContext ctx)=>{var user=UserId(ctx);var m=await Managed(db,id,user);MeetRules.Substitute(m,MeetRules.Find(m,matchId),input);await db.Save(m,input.Revision,user,"substitution");return m;});
api.MapGet("/meets/{id}/organizers",async(string id,Database db,HttpContext ctx)=>{var m=await db.GetMeet(id);await ClubRole(db,m.ClubId,UserId(ctx));return await db.Query("SELECT u.id,u.name,u.email FROM users u JOIN meet_organizers mo ON mo.user_id=u.id WHERE mo.meet_id=@id ORDER BY u.name",("id",id));});
api.MapPut("/meets/{id}/organizers",async(string id,OrganizerInput input,Database db,HttpContext ctx)=>{
 var m=await db.GetMeet(id);var user=UserId(ctx);await RequireClub(db,m.ClubId,user);var ids=(input.UserIds??[]).Distinct().ToArray();var members=await db.Query("SELECT user_id FROM club_members WHERE club_id=@club",("club",m.ClubId));MeetRules.Require(ids.All(id=>members.Any(row=>(string)row["user_id"]! == id)),"Choose registered members of this club.");
 await using var c=await db.Open();await using var tx=await c.BeginTransactionAsync();await using(var guard=Database.Command(c,"SELECT id FROM meets WHERE id=@id FOR UPDATE",("id",id)))await guard.ExecuteScalarAsync();
 await using(var clear=Database.Command(c,"DELETE FROM meet_organizers WHERE meet_id=@id",("id",id)))await clear.ExecuteNonQueryAsync();
 foreach(var organizer in ids){await using var add=Database.Command(c,"INSERT INTO meet_organizers(meet_id,user_id) VALUES(@id,@user)",("id",id),("user",organizer));await add.ExecuteNonQueryAsync();}
 await using(var audit=Database.Command(c,"INSERT INTO audit_events(meet_id,actor_id,action) VALUES(@id,@user,'organizer-assignments')",("id",id),("user",user)))await audit.ExecuteNonQueryAsync();await tx.CommitAsync();return Results.NoContent();
});
api.MapGet("/meets/{id}/audit",async(string id,Database db,HttpContext ctx)=>{await Managed(db,id,UserId(ctx));return await db.Query("SELECT a.id,a.action,a.revision,a.created_at AS at,u.name AS actor FROM audit_events a JOIN users u ON u.id=a.actor_id WHERE meet_id=@id ORDER BY a.id DESC LIMIT 100",("id",id));});
app.MapFallback(async ctx=>{if(ctx.Request.Path.StartsWithSegments("/api")){ctx.Response.StatusCode=404;return;}var file=Path.Combine(app.Environment.WebRootPath??Path.Combine(app.Environment.ContentRootPath,"wwwroot"),"index.html");if(File.Exists(file))await ctx.Response.SendFileAsync(file);else{ctx.Response.StatusCode=404;await ctx.Response.WriteAsync("Build the React frontend or run it on port 5173.");}});
app.Run();
