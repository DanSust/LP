using System.Collections.Concurrent;
using System.Diagnostics;

namespace LP.ThrustLoader;

public enum HttpScenarioKind
{
    VotesList,
    AuthStatus,
    UsersInfo,
    UsersView
}

public sealed class HttpScenarioConfig
{
    public HttpScenarioKind Kind { get; init; }
    public bool Enabled { get; init; }
    public string Name { get; init; } = string.Empty;
    public int VotesCount { get; init; } = 5;
}

public sealed class HttpLoadConfig
{
    public string BaseUrl { get; init; } = string.Empty;
    public int Concurrency { get; init; } = 10;
    public int DurationSeconds { get; init; } = 30;
    public bool IgnoreSslErrors { get; init; }
    public string? Username { get; init; }
    public string? Password { get; init; }
    public IReadOnlyList<HttpScenarioConfig> Scenarios { get; init; } = Array.Empty<HttpScenarioConfig>();
}

public sealed class RequestSample
{
    public required string Scenario { get; init; }
    public bool Success { get; init; }
    public int StatusCode { get; init; }
    public long LatencyMs { get; init; }
    public string? Error { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.Now;
}

public sealed class ScenarioStats
{
    public string Name { get; }
    public long Count;
    public long Success;
    public long Fail;
    public long TotalMs;
    public long MinMs = long.MaxValue;
    public long MaxMs;
    public ConcurrentBag<long> Latencies { get; } = new();

    public ScenarioStats(string name) => Name = name;

    public void Add(long latencyMs, bool success)
    {
        Interlocked.Increment(ref Count);
        if (success) Interlocked.Increment(ref Success); else Interlocked.Increment(ref Fail);
        Interlocked.Add(ref TotalMs, latencyMs);

        long initial, computed;
        do
        {
            initial = MinMs;
            if (latencyMs >= initial) break;
            computed = latencyMs;
        } while (Interlocked.CompareExchange(ref MinMs, computed, initial) != initial);

        do
        {
            initial = MaxMs;
            if (latencyMs <= initial) break;
            computed = latencyMs;
        } while (Interlocked.CompareExchange(ref MaxMs, computed, initial) != initial);

        Latencies.Add(latencyMs);
    }

    public StatsSnapshot Snapshot()
    {
        var count = Interlocked.Read(ref Count);
        var success = Interlocked.Read(ref Success);
        var fail = Interlocked.Read(ref Fail);
        var total = Interlocked.Read(ref TotalMs);
        var min = Interlocked.Read(ref MinMs);
        var max = Interlocked.Read(ref MaxMs);
        if (count == 0) min = 0;
        var arr = Latencies.ToArray();
        Array.Sort(arr);
        return new StatsSnapshot(Name, count, success, fail, count == 0 ? 0 : (double)total / count, min, max, Percentile(arr, 50), Percentile(arr, 95), Percentile(arr, 99));
    }

    private static double Percentile(long[] sorted, int p)
    {
        if (sorted.Length == 0) return 0;
        if (sorted.Length == 1) return sorted[0];
        var rank = (p / 100.0) * (sorted.Length - 1);
        var lo = (int)Math.Floor(rank);
        var hi = (int)Math.Ceiling(rank);
        if (lo == hi) return sorted[lo];
        var w = rank - lo;
        return sorted[lo] * (1 - w) + sorted[hi] * w;
    }
}

public sealed record StatsSnapshot(
    string Name,
    long Count,
    long Success,
    long Fail,
    double AvgMs,
    long MinMs,
    long MaxMs,
    double P50Ms,
    double P95Ms,
    double P99Ms);
