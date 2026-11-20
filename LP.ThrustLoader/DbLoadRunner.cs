using System.Diagnostics;
using LP.Entity;
using Microsoft.EntityFrameworkCore;

namespace LP.ThrustLoader;

public sealed class DbLoadConfig
{
    public string ConnectionString { get; init; } = string.Empty;
    public int IntervalSeconds { get; init; } = 5;
    public int Top { get; init; } = 100;
    public bool QueryUsers { get; init; } = true;
    public bool QueryProfiles { get; init; } = true;
}

public sealed class DbLoadRunner
{
    private readonly DbLoadConfig _config;
    private readonly Action<string> _log;
    private readonly Action<string> _status;
    private long _iteration;

    public DbLoadRunner(DbLoadConfig config, Action<string> log, Action<string> status)
    {
        _config = config;
        _log = log;
        _status = status;
    }

    public async Task RunAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var iter = Interlocked.Increment(ref _iteration);
            var started = DateTime.Now;
            _status($"Статус: выполняется цикл #{iter}...");

            try
            {
                var options = new DbContextOptionsBuilder<LP.Entity.ApplicationContext>()
                    .UseSqlServer(_config.ConnectionString)
                    .Options;

                await using var db = new LP.Entity.ApplicationContext(options);

                if (_config.QueryUsers)
                    await QueryUsersAsync(db, _config.Top, ct);

                if (_config.QueryProfiles)
                    await QueryProfilesAsync(db, _config.Top, ct);

                _log($"[{started:HH:mm:ss}] Цикл #{iter} завершён.");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _log($"[{started:HH:mm:ss}] Цикл #{iter} ОШИБКА: {ex.GetType().Name}: {ex.Message}");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(_config.IntervalSeconds), ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task QueryUsersAsync(LP.Entity.ApplicationContext db, int top, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var count = await db.Users.AsNoTracking().CountAsync(ct);
        var msCount = sw.ElapsedMilliseconds;

        sw.Restart();
        var sample = await db.Users
            .AsNoTracking()
            .OrderBy(u => u.Id)
            .Take(top)
            .Select(u => new { u.Id, u.Caption, u.Sex, u.IsPaused })
            .ToListAsync(ct);
        sw.Stop();

        _log($"  Users: count={count} ({msCount} ms), select top {top} => {sample.Count} rows ({sw.ElapsedMilliseconds} ms)");
    }

    private async Task QueryProfilesAsync(LP.Entity.ApplicationContext db, int top, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var count = await db.Profiles.AsNoTracking().CountAsync(ct);
        var msCount = sw.ElapsedMilliseconds;

        sw.Restart();
        var sample = await db.Profiles
            .AsNoTracking()
            .OrderBy(p => p.Id)
            .Take(top)
            .Select(p => new { p.Id, p.UserId, p.CityId, p.Height, p.Weight })
            .ToListAsync(ct);
        sw.Stop();

        _log($"  Profiles: count={count} ({msCount} ms), select top {top} => {sample.Count} rows ({sw.ElapsedMilliseconds} ms)");
    }
}