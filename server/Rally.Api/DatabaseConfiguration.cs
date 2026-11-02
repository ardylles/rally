using Microsoft.Extensions.Configuration;
using Npgsql;

namespace Rally;

public sealed record DatabaseConnections(string Application, string Schema);

public static class DatabaseConfiguration
{
    public static DatabaseConnections Resolve(IConfiguration config)
    {
        // Explicit Npgsql configuration keeps the existing local/Compose workflow working.
        if (config.GetConnectionString("Rally") is { Length: > 0 } legacy)
        {
            var options = new NpgsqlConnectionStringBuilder(legacy);
            if (config["Database:Password"] is { Length: > 0 } password)
                options.Password = password;
            return new(options.ConnectionString, options.ConnectionString);
        }

        var url = config["DATABASE_URL"];
        if (string.IsNullOrWhiteSpace(url))
            throw new InvalidOperationException("Set DATABASE_URL for Neon, or ConnectionStrings__Rally for local PostgreSQL.");
        var application = FromUrl(url);
        var direct = config["DATABASE_URL_UNPOOLED"];
        if (string.IsNullOrWhiteSpace(direct) && new NpgsqlConnectionStringBuilder(application).Host!.Contains("-pooler"))
            throw new InvalidOperationException("Set DATABASE_URL_UNPOOLED for schema initialization with a pooled DATABASE_URL.");
        return new(application, string.IsNullOrWhiteSpace(direct) ? application : FromUrl(direct));
    }

    public static string FromUrl(string value)
    {
        // Do not include input URLs or parser exceptions in errors: they contain passwords.
        try
        {
            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
                uri.Scheme is not ("postgres" or "postgresql") ||
                string.IsNullOrWhiteSpace(uri.Host) || uri.Fragment.Length > 0)
                throw new FormatException();
            var credentials = uri.UserInfo.Split(':', 2);
            var database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/'));
            if (credentials.Length != 2 || credentials[0].Length == 0 || database.Length == 0)
                throw new FormatException();

            var options = new NpgsqlConnectionStringBuilder
            {
                Host = uri.Host,
                Port = uri.Port < 0 ? 5432 : uri.Port,
                Database = database,
                Username = Uri.UnescapeDataString(credentials[0]),
                Password = Uri.UnescapeDataString(credentials[1]),
                SslMode = SslMode.VerifyFull,
                Pooling = true,
                MaxPoolSize = 20,
                Timeout = 30,
                ApplicationName = "Rally"
            };
            foreach (var part in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var pair = part.Split('=', 2);
                if (pair.Length != 2) throw new FormatException();
                var key = Uri.UnescapeDataString(pair[0]);
                var setting = Uri.UnescapeDataString(pair[1]);
                switch (key)
                {
                    case "sslmode":
                        options.SslMode = setting switch
                        {
                            "require" or "verify-full" => SslMode.VerifyFull,
                            "verify-ca" => SslMode.VerifyCA,
                            "disable" => SslMode.Disable,
                            "prefer" => SslMode.Prefer,
                            "allow" => SslMode.Allow,
                            _ => throw new FormatException()
                        };
                        break;
                    case "channel_binding":
                        options.ChannelBinding = setting switch
                        {
                            "require" => ChannelBinding.Require,
                            "prefer" => ChannelBinding.Prefer,
                            "disable" => ChannelBinding.Disable,
                            _ => throw new FormatException()
                        };
                        break;
                    case "connect_timeout": options.Timeout = int.Parse(setting); break;
                    case "application_name": options.ApplicationName = setting; break;
                    default: throw new FormatException();
                }
            }
            if (uri.Host.EndsWith(".neon.tech", StringComparison.OrdinalIgnoreCase))
                options.SslMode = SslMode.VerifyFull;
            return options.ConnectionString;
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException or OverflowException)
        {
            throw new InvalidOperationException("Invalid PostgreSQL URL or unsupported connection option. Check DATABASE_URL and DATABASE_URL_UNPOOLED.");
        }
    }
}
