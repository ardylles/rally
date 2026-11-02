using System.Text.Json;
using System.Text.RegularExpressions;
namespace Rally;
public sealed record ReclubPlayer(string Id,string Name,int Status);
public sealed record ReclubPreview(string Url,string Name,List<ReclubPlayer> Players);
public static class ReclubScraper {
 public static string Code(string url) {
  if(!Uri.TryCreate(url,UriKind.Absolute,out var uri)||uri.Scheme!="https"||uri.Host!="reclub.co"||!uri.IsDefaultPort||uri.UserInfo.Length>0)
   throw new RuleException("Use a public https://reclub.co/m/… meet URL.");
  var match=Regex.Match(uri.AbsolutePath,@"^/m/([A-Za-z0-9]{4,20})/?$");
  if(!match.Success)throw new RuleException("Use a Reclub meet URL, such as https://reclub.co/m/PATO94.");
  return match.Groups[1].Value;
 }
 public static async Task<ReclubPreview> Fetch(string url,CancellationToken cancellation) {
  var code=Code(url);
  using var handler=new HttpClientHandler{AllowAutoRedirect=false};
  using var client=new HttpClient(handler){Timeout=TimeSpan.FromSeconds(20)};
  try {
   using var response=await client.GetAsync("https://reclub.co/m/"+code,HttpCompletionOption.ResponseHeadersRead,cancellation);
   if(!response.IsSuccessStatusCode)throw new RuleException("Reclub could not provide this public meet page. Enter players manually or try again later.",502);
   await using var stream=await response.Content.ReadAsStreamAsync(cancellation);
   using var buffer=new MemoryStream();var bytes=new byte[8192];
   int count;
   while((count=await stream.ReadAsync(bytes,cancellation))>0) {
    if(buffer.Length+count>2_000_000)throw new RuleException("The Reclub page is too large to import.",502);
    buffer.Write(bytes,0,count);
   }
   return Parse(System.Text.Encoding.UTF8.GetString(buffer.ToArray()),code);
  } catch(HttpRequestException){throw new RuleException("Reclub is unreachable. Enter players manually or try again later.",502);}
    catch(TaskCanceledException){throw new RuleException("Reclub took too long to respond. Please try again.",504);}
 }
 public static ReclubPreview Parse(string html,string code) {
  try {
   var script=Regex.Match(html,"<script[^>]*id=[\\\"']__NUXT_DATA__[\\\"'][^>]*>(.*?)</script>",RegexOptions.Singleline,TimeSpan.FromSeconds(1));
   if(!script.Success)throw new FormatException();
   using var doc=JsonDocument.Parse(script.Groups[1].Value);
   var nodes=doc.RootElement;
   JsonElement Ref(JsonElement reference) {
    var value=nodes[reference.GetInt32()];
    for(int i=0;i<10&&value.ValueKind==JsonValueKind.Array&&value.GetArrayLength()==2&&value[0].ValueKind==JsonValueKind.String;i++) {
     if(value[0].GetString() is not ("Reactive" or "ShallowReactive" or "Ref"))break;
     value=nodes[value[1].GetInt32()];
    }
    return value;
   }
   JsonElement Prop(JsonElement obj,string name)=>Ref(obj.GetProperty(name));
   var root=nodes[0];var rootObject=nodes[root[1].GetInt32()];
   var payload=Prop(Prop(rootObject,"data"),"meet-"+code);
   var meet=Prop(payload,"meet");var users=Prop(payload,"usersMap");
   if(Prop(meet,"referenceCode").GetString()!=code)throw new FormatException();
   var result=new List<ReclubPlayer>();
   foreach(var participantRef in Prop(meet,"participants").EnumerateArray()) {
    var participant=Ref(participantRef);var reference=Prop(participant,"referenceId");
    string id,name;
    if(reference.ValueKind==JsonValueKind.Number&&users.TryGetProperty(reference.ToString(),out var userRef)) {
     var user=Ref(userRef);id=reference.ToString();name=Prop(user,"name").GetString()??"";
    } else {
     var external=Prop(participant,"externalReference");
     if(external.ValueKind!=JsonValueKind.Object)continue;
     name=Prop(external,"name").GetString()??"";id="guest:"+Prop(participant,"id").GetString();
    }
    if(name.Trim().Length is >0 and <=80)
     result.Add(new(id,name.Trim(),Prop(participant,"status").GetInt32()));
   }
   if(result.Count==0)throw new FormatException();
   return new("https://reclub.co/m/"+code,Prop(meet,"name").GetString()??"Reclub meet",result.DistinctBy(p=>p.Id).ToList());
  } catch(Exception e) when(e is JsonException or InvalidOperationException or KeyNotFoundException or IndexOutOfRangeException or FormatException or RegexMatchTimeoutException) {
   throw new RuleException("The player list could not be read from this Reclub page. Its format may have changed. Enter players manually.",422);
  }
 }
}
