using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.Sqlite;

/*
 * FarmReel server: license activation + metered quotas (Time Change Keys),
 * server-generated device info, mail stock storefront, and the update channel.
 *
 * Run:  dotnet run --project src/FarmReel.Server
 * Docs: README section "Server"
 *
 * All state lives in SQLite (LicenseDbPath). The client only calls these
 * endpoints when a license server URL is configured in Settings; otherwise it
 * runs fully offline.
 */

var builder = WebApplication.CreateBuilder(args);
builder.Logging.SetMinimumLevel(LogLevel.Information);
var app = builder.Build();

var dbPath = app.Configuration["LicenseDbPath"] ?? "farmreel_server.db";
var updateDir = app.Configuration["UpdateFilesDir"] ?? Path.Combine(app.Environment.ContentRootPath, "update-files");
var updateVersion = app.Configuration["UpdateVersion"] ?? "1.0.0";
var updateNotes = app.Configuration["UpdateNotes"] ?? "";
var adminKey = app.Configuration["AdminKey"] ?? "";

Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(dbPath)) ?? ".");
Directory.CreateDirectory(updateDir);

InitializeDb(dbPath);
SeedStock(dbPath, app.Configuration["StockCsvPath"]);

/* ---------- license + quota ---------- */

app.MapPost("/api/license/activate", async (HttpContext ctx) =>
{
    using var body = await JsonDocument.ParseAsync(ctx.Request.Body);
    var key = GetString(body.RootElement, "key");
    var machineId = GetString(body.RootElement, "machineId");
    if (string.IsNullOrWhiteSpace(key))
        return Results.Json(new { ok = false, error = "key required" });

    using var db = Open(dbPath);
    var row = FindLicense(db, key);
    if (row == null)
    {
        // Demo/offline activation: accept FR-xxxx keys and mint a license
        var plan = key.StartsWith("FR-", StringComparison.OrdinalIgnoreCase) ? "1m" : "trial";
        var keys = plan == "trial" ? 2 : 10;
        var expires = DateTime.UtcNow.AddMonths(plan == "trial" ? 1 : 1);
        InsertLicense(db, key, plan, expires, keys, 0, plan == "12m", plan == "12m", machineId);
        row = (key, plan, expires, keys, 0, plan == "12m", plan == "12m");
    }

    return Results.Json(new
    {
        ok = true,
        plan = row.plan,
        expiresAt = row.expires.ToString("o"),
        keys = row.keysTotal,
        used = row.keysUsed,
        regFull = row.regFull,
        novery = row.novery,
        note = $"Activated {row.plan}. Time change keys: {row.keysTotal - row.keysUsed} left."
    });
});

app.MapGet("/api/license/status", (string key) =>
{
    if (string.IsNullOrWhiteSpace(key)) return Results.Json(new { ok = false, error = "key required" });
    using var db = Open(dbPath);
    var row = FindLicense(db, key);
    if (row == null) return Results.Json(new { ok = false, error = "license not found" });
    return Results.Json(new { ok = true, plan = row.plan, keys = row.keysTotal, used = row.keysUsed,
        regFull = row.regFull, novery = row.novery, expiresAt = row.expires.ToString("o") });
});

app.MapPost("/api/license/consume", async (HttpContext ctx) =>
{
    using var body = await JsonDocument.ParseAsync(ctx.Request.Body);
    var key = GetString(body.RootElement, "key");
    var feature = GetString(body.RootElement, "feature") ?? "timechange";
    using var db = Open(dbPath);
    var row = FindLicense(db, key);
    if (row == null) return Results.Json(new { ok = false, error = "license not found", remaining = 0 });

    if (feature == "timechange")
    {
        if (row.keysUsed >= row.keysTotal)
            return Results.Json(new { ok = false, error = "no time change keys left", remaining = 0 });
        var used = row.keysUsed + 1;
        using var cmd = db.CreateCommand();
        cmd.CommandText = "UPDATE Licenses SET KeysUsed=$used WHERE [Key]=$key";
        cmd.Parameters.AddWithValue("$used", used);
        cmd.Parameters.AddWithValue("$key", key);
        cmd.ExecuteNonQuery();
        return Results.Json(new { ok = true, remaining = row.keysTotal - used, used });
    }
    return Results.Json(new { ok = true, remaining = row.keysTotal - row.keysUsed });
});

/* ---------- server-generated device info (fingerprint service) ---------- */

app.MapPost("/api/deviceinfo/generate", async (HttpContext ctx) =>
{
    using var body = await JsonDocument.ParseAsync(ctx.Request.Body);
    var key = GetString(body.RootElement, "key");
    if (string.IsNullOrWhiteSpace(key) || FindLicense(Open(dbPath), key) == null)
        return Results.Json(new { ok = false, error = "invalid license" });

    var rng = new Random();
    string Rand(int n) => string.Concat(Enumerable.Range(0, n).Select(_ => rng.Next(0, 10)));
    string Mac() => string.Join(":", Enumerable.Range(0, 6).Select(_ => rng.Next(0, 256).ToString("X2")));
    var models = new[] { ("samsung", "SM-G991B"), ("google", "Pixel 7"), ("Xiaomi", "Redmi Note 11"), ("OPPO", "OPPO A54"), ("vivo", "Vivo Y21") };
    var m = models[rng.Next(models.Length)];

    return Results.Json(new
    {
        ok = true,
        device = new
        {
            mac = Mac(),
            imei = Rand(15),
            imsi = Rand(15),
            simSerial = Rand(19),
            androidId = Guid.NewGuid().ToString("N")[..16],
            manufacturer = m.Item1,
            model = m.Item2
        }
    });
});

/* ---------- mail stock storefront ---------- */

app.MapGet("/api/stock", (string key) =>
{
    if (string.IsNullOrWhiteSpace(key) || FindLicense(Open(dbPath), key) == null)
        return Results.Json(new { ok = false, error = "invalid license" });
    using var db = Open(dbPath);
    var rows = Query(db, "SELECT Provider, COUNT(*) AS N, SUM(CASE WHEN Sold=0 THEN 1 ELSE 0 END) AS Avail FROM Stock GROUP BY Provider");
    var stock = rows.Select(r => new
    {
        provider = (string)r["Provider"],
        total = Convert.ToInt32(r["N"]),
        available = Convert.ToInt32(r["Avail"]),
        price = providerPrice((string)r["Provider"])
    });
    return Results.Json(new { ok = true, stock });
});

app.MapPost("/api/stock/order", async (HttpContext ctx) =>
{
    using var body = await JsonDocument.ParseAsync(ctx.Request.Body);
    var key = GetString(body.RootElement, "key");
    var count = Math.Clamp(GetInt(body.RootElement, "count", 1), 1, 100);
    var provider = GetString(body.RootElement, "provider");
    if (string.IsNullOrWhiteSpace(key) || FindLicense(Open(dbPath), key) == null)
        return Results.Json(new { ok = false, error = "invalid license" });

    using var db = Open(dbPath);
    var list = new List<object>();
    using (var cmd = db.CreateCommand())
    {
        cmd.CommandText = provider switch
        {
            null or "" => "SELECT * FROM Stock WHERE Sold=0 ORDER BY Id LIMIT $n",
            _ => "SELECT * FROM Stock WHERE Sold=0 AND Provider=$p ORDER BY Id LIMIT $n"
        };
        cmd.Parameters.AddWithValue("$n", count);
        if (!string.IsNullOrEmpty(provider)) cmd.Parameters.AddWithValue("$p", provider);
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            var id = Convert.ToInt64(r["Id"]);
            list.Add(new { id, email = (string)r["Email"], password = (string)r["Password"], provider = (string)r["Provider"] });
        }
    }
    foreach (var item in list.Cast<dynamic>())
    {
        using var up = db.CreateCommand();
        up.CommandText = "UPDATE Stock SET Sold=1 WHERE Id=$id";
        up.Parameters.AddWithValue("$id", (long)item.id);
        up.ExecuteNonQuery();
    }
    return Results.Json(new { ok = true, delivered = list.Count, accounts = list });
});

/* ---------- update channel ---------- */

app.MapGet("/api/update/manifest", () =>
{
    var exe = Path.Combine(updateDir, "FarmReel.exe");
    if (!File.Exists(exe))
        return Results.Json(new { ok = false, error = "no update file staged", version = updateVersion });

    using var sha = SHA256.Create();
    var hash = Convert.ToHexString(sha.ComputeHash(File.ReadAllBytes(exe))).ToLowerInvariant();
    var baseUrl = $"{app.Urls.FirstOrDefault()?.TrimEnd('/') ?? ""}";
    return Results.Json(new
    {
        ok = true,
        version = updateVersion,
        url = baseUrl + "/api/update/download",
        sha256 = hash,
        notes = updateNotes
    });
});

app.MapGet("/api/update/download", () =>
{
    var exe = Path.Combine(updateDir, "FarmReel.exe");
    if (!File.Exists(exe)) return Results.NotFound("no update staged");
    return Results.File(exe, "application/octet-stream", "FarmReel.exe");
});

/* ---------- helpers ---------- */

static void InitializeDb(string path)
{
    using var db = Open(path);
    Exec(db, "CREATE TABLE IF NOT EXISTS Licenses([Key] TEXT PRIMARY KEY, Plan TEXT NOT NULL, ExpiresAt TEXT NOT NULL, KeysTotal INTEGER NOT NULL DEFAULT 0, KeysUsed INTEGER NOT NULL DEFAULT 0, RegFull INTEGER NOT NULL DEFAULT 0, Novery INTEGER NOT NULL DEFAULT 0, MachineId TEXT NOT NULL DEFAULT '')");
    Exec(db, "CREATE TABLE IF NOT EXISTS Stock(Id INTEGER PRIMARY KEY AUTOINCREMENT, Email TEXT NOT NULL, Password TEXT NOT NULL, Provider TEXT NOT NULL, Sold INTEGER NOT NULL DEFAULT 0)");
}

static void SeedStock(string path, string csvPath)
{
    using var db = Open(path);
    var cnt = Convert.ToInt64(ExecScalar(db, "SELECT COUNT(*) FROM Stock"));
    if (cnt > 0) return;

    if (!string.IsNullOrEmpty(csvPath) && File.Exists(csvPath))
    {
        foreach (var line in File.ReadAllLines(csvPath))
        {
            var p = line.Split(',');
            if (p.Length >= 2)
                Exec(db, "INSERT INTO Stock(Email,Password,Provider,Sold) VALUES($e,$p,$r,0)",
                    ("$e", p[0].Trim()), ("$p", p[1].Trim()), ("$r", p.Length > 2 ? p[2].Trim() : "outlook"));
        }
        return;
    }

    // Demo stock so the storefront is testable out of the box
    var rng = new Random();
    var domains = new[] { "outlook.com", "gmail.com", "zoho.com" };
    for (int i = 0; i < 30; i++)
    {
        var d = domains[rng.Next(domains.Length)];
        var email = $"demo{i}_{Guid.NewGuid():N}"[..12] + "@" + d;
        Exec(db, "INSERT INTO Stock(Email,Password,Provider,Sold) VALUES($e,'ChangeMe123!',$r,0)",
            ("$e", email), ("$r", d));
    }
}

static string providerPrice(string provider) => provider switch
{
    "outlook" => "0.35",
    "gmail" => "0.55",
    "zoho" => "0.25",
    _ => "0.30"
};

static SqliteConnection Open(string path)
{
    var conn = new SqliteConnection($"Data Source={path}");
    conn.Open();
    using var p = conn.CreateCommand();
    p.CommandText = "PRAGMA journal_mode=WAL; PRAGMA busy_timeout=5000;";
    p.ExecuteNonQuery();
    return conn;
}

static void Exec(SqliteConnection db, string sql, params (string, object)[] parms)
{
    using var cmd = db.CreateCommand();
    cmd.CommandText = sql;
    foreach (var (n, v) in parms) cmd.Parameters.AddWithValue(n, v ?? DBNull.Value);
    cmd.ExecuteNonQuery();
}

static object ExecScalar(SqliteConnection db, string sql)
{
    using var cmd = db.CreateCommand();
    cmd.CommandText = sql;
    return cmd.ExecuteScalar();
}

static List<Dictionary<string, object>> Query(SqliteConnection db, string sql)
{
    using var cmd = db.CreateCommand();
    cmd.CommandText = sql;
    using var r = cmd.ExecuteReader();
    var rows = new List<Dictionary<string, object>>();
    while (r.Read())
    {
        var row = new Dictionary<string, object>();
        for (int i = 0; i < r.FieldCount; i++) row[r.GetName(i)] = r.GetValue(i);
        rows.Add(row);
    }
    return rows;
}

static (string key, string plan, DateTime expires, int keysTotal, int keysUsed, bool regFull, bool novery) FindLicense(SqliteConnection db, string key)
{
    using var cmd = db.CreateCommand();
    cmd.CommandText = "SELECT * FROM Licenses WHERE [Key]=$key";
    cmd.Parameters.AddWithValue("$key", key);
    using var r = cmd.ExecuteReader();
    if (!r.Read()) return (null, null, default, 0, 0, false, false);
    return (
        (string)r["Key"], (string)r["Plan"], DateTime.Parse((string)r["ExpiresAt"]),
        Convert.ToInt32(r["KeysTotal"]), Convert.ToInt32(r["KeysUsed"]),
        Convert.ToInt32(r["RegFull"]) != 0, Convert.ToInt32(r["Novery"]) != 0);
}

static void InsertLicense(SqliteConnection db, string key, string plan, DateTime expires, int keys, int used, bool regFull, bool novery, string machineId)
{
    using var cmd = db.CreateCommand();
    cmd.CommandText = "INSERT OR REPLACE INTO Licenses([Key],Plan,ExpiresAt,KeysTotal,KeysUsed,RegFull,Novery,MachineId) VALUES($k,$p,$e,$t,$u,$r,$n,$m)";
    cmd.Parameters.AddWithValue("$k", key); cmd.Parameters.AddWithValue("$p", plan);
    cmd.Parameters.AddWithValue("$e", expires.ToString("o")); cmd.Parameters.AddWithValue("$t", keys);
    cmd.Parameters.AddWithValue("$u", used); cmd.Parameters.AddWithValue("$r", regFull ? 1 : 0);
    cmd.Parameters.AddWithValue("$n", novery ? 1 : 0); cmd.Parameters.AddWithValue("$m", machineId);
    cmd.ExecuteNonQuery();
}

static string GetString(JsonElement e, string name) =>
    e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";

static int GetInt(JsonElement e, string name, int def) =>
    e.TryGetProperty(name, out var v) && v.TryGetInt32(out var i) ? i : def;

app.Run();
