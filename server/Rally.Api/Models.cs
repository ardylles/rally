using System.Text.Json;
namespace Rally;
public static class Json { public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web); }
public sealed class RuleException(string message, int status = 400) : Exception(message) { public int Status { get; } = status; }
public sealed class Meet {
 public string Id { get; set; } = Guid.NewGuid().ToString("N");
 public string ClubId { get; set; } = "";
 public int Revision { get; set; }
 public string Name { get; set; } = "";
 public string Venue { get; set; } = "";
 public string Date { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-dd");
 public string Time { get; set; } = "16:00";
 public string Timezone { get; set; } = "Asia/Jakarta";
 public int DurationMinutes { get; set; } = 120;
 public int Courts { get; set; } = 2;
 public List<CourtWindow> CourtAvailability { get; set; } = [];
 public int SlotMinutes { get; set; } // Zero preserves older meets: divide the session into rounds.
 public int Rounds { get; set; } = 5;
 public string Mode { get; set; } = "rotating";
 public string Format { get; set; } = "4";
 public bool Timing { get; set; } = true;
 public List<Player> Players { get; set; } = [];
 public List<string[]> FixedPairs { get; set; } = [];
 public List<Match> Matches { get; set; } = [];
 public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
public sealed class Player {
 public string Id { get; set; } = Guid.NewGuid().ToString("N");
 public string Name { get; set; } = "";
 public double? Skill { get; set; }
 public string? UserId { get; set; }
}
public sealed class Match {
 public string Id { get; set; } = Guid.NewGuid().ToString("N");
 public int Round { get; set; }
 public int Court { get; set; }
 public string? PlannedStart { get; set; }
 public string? PlannedEnd { get; set; }
 public string[][] Teams { get; set; } = [];
 public string Format { get; set; } = "4";
 public string Status { get; set; } = "ready";
 public DateTimeOffset? StartedAt { get; set; }
 public DateTimeOffset? EndedAt { get; set; }
 public int[]? Score { get; set; }
 public Dictionary<string, PlayerStats> Stats { get; set; } = [];
 public List<PlayerChange> Changes { get; set; } = [];
}
public sealed class PlayerStats { public int? Winners { get; set; } public int? UnforcedErrors { get; set; } public int? PadelRatio => Winners.HasValue && UnforcedErrors.HasValue ? Winners-UnforcedErrors : null; }
public sealed record PlayerChange(string OutgoingId, string IncomingId, string Scope, string Source, DateTimeOffset At);
public sealed record Credentials(string Name, string Email, string Password);
public sealed record ClubInput(string Name);
public sealed record MemberInput(string Email, string Role);
public sealed record RoleInput(string UserId, string Role);
public sealed record RevisionInput(int Revision);
public sealed record PairInput(int Revision, List<string[]> Pairs);
public sealed record SkillInput(int Revision, string PlayerId, double Skill);
public sealed record ResultInput(int Revision, int[] Score, Dictionary<string, PlayerStats>? Stats);
public sealed record SubstituteInput(int Revision, string OutgoingId, string IncomingId);
public sealed record ReplacementInput(int Revision, string OutgoingId, string Name, double? Skill, string Scope);
public sealed record OrganizerInput(List<string> UserIds);
public sealed record UserInfo(string Id, string Name, string Email);

public sealed class CourtWindow { public string Name { get; set; } = ""; public string Start { get; set; } = ""; public string End { get; set; } = ""; }
public sealed record ReclubInput(string Url);
