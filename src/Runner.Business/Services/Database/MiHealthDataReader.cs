using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;
using JavaRuntime = Java.Lang.Runtime;

namespace Runner.Business.Services.Database;

public class MiHealthDataReader
{
    private static readonly string[] KnownDbPaths =
    [
        "/data/data/com.xiaomi.hm.health/databases/no_account/global/fitness_data",
        "/data/data/com.xiaomi.hm.health/databases/fitness_data",
    ];

    private readonly ILogger<MiHealthDataReader> _logger;
    private string? _dbPath;

    public MiHealthDataReader(ILogger<MiHealthDataReader> logger)
    {
        _logger = logger;
    }

    public bool IsAvailable => _dbPath != null;

    public async Task<bool> InitializeAsync()
    {
        var result = ExecRoot("find /data/data/com.xiaomi.hm.health/databases -name \"fitness_data\" -type f 2>/dev/null | head -1");
        if (!string.IsNullOrWhiteSpace(result))
        {
            _dbPath = result.Trim();
            _logger.LogInformation("[MiHealthDb] Found DB at: {Path}", _dbPath);
            return true;
        }

        foreach (var path in KnownDbPaths)
        {
            var exists = ExecRoot($"test -f \"{path}\" && echo yes || echo no");
            if (exists?.Trim() == "yes")
            {
                _dbPath = path;
                _logger.LogInformation("[MiHealthDb] Found DB at known path: {Path}", _dbPath);
                return true;
            }
        }

        _logger.LogWarning("[MiHealthDb] Could not find fitness_data database");
        return false;
    }

    public async Task<List<SportReport>> GetRecentWorkoutsAsync(int limit = 20)
    {
        var result = new List<SportReport>();

        if (_dbPath == null)
        {
            _logger.LogWarning("[MiHealthDb] No DB path available");
            return result;
        }

        var sqlite = FindSqlite();
        if (sqlite == null)
        {
            _logger.LogWarning("[MiHealthDb] sqlite3 not available on device");
            return result;
        }

        var query = $"SELECT sid,key,time,category,timeIn0Tz,COALESCE(value,'') FROM sport_report WHERE isDeleted = 0 ORDER BY time DESC LIMIT {limit}";
        var output = ExecRoot($"\"{sqlite}\" -separator '|' \"{_dbPath}\" \"{query}\" 2>/dev/null");

        if (string.IsNullOrWhiteSpace(output))
        {
            _logger.LogInformation("[MiHealthDb] No sport_report data found");
            return result;
        }

        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.Split('|');
            if (parts.Length >= 5)
            {
                result.Add(new SportReport
                {
                    Sid = parts[0],
                    Key = parts[1],
                    Time = long.TryParse(parts[2], out var t) ? t : 0,
                    Category = parts.Length > 3 ? parts[3] : null,
                    TimeIn0Tz = parts.Length > 4 && long.TryParse(parts[4], out var tz) ? tz : null,
                    Value = parts.Length > 5 ? parts[5] : null
                });
            }
        }

        _logger.LogInformation("[MiHealthDb] Found {Count} sport reports", result.Count);
        return result;
    }

    private static string? FindSqlite()
    {
        var candidates = new[] { "sqlite3", "/system/bin/sqlite3", "/system/xbin/sqlite3",
            "/data/data/com.termux/files/usr/bin/sqlite3" };

        foreach (var c in candidates)
        {
            var r = ExecRoot($"command -v \"{c}\" 2>/dev/null || which \"{c}\" 2>/dev/null || echo MISSING");
            if (r?.Trim() != "MISSING" && !string.IsNullOrWhiteSpace(r))
                return c;
        }
        return null;
    }

    private static string? ExecRoot(string command)
    {
        try
        {
            var proc = JavaRuntime.GetRuntime()!.Exec(new[] { "su", "-c", command });
            using var reader = new StreamReader(proc.InputStream, Encoding.UTF8);
            var output = reader.ReadToEnd();
            proc.WaitFor();
            return output;
        }
        catch (Exception ex)
        {
            return null;
        }
    }
}
