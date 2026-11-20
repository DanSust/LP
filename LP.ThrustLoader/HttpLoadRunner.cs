using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace LP.ThrustLoader;

public sealed class HttpLoadRunner
{
    private readonly HttpLoadConfig _config;
    private readonly Action<string> _log;
    private readonly Action<string> _status;

    public ConcurrentDictionary<string, ScenarioStats> Stats { get; } = new(StringComparer.OrdinalIgnoreCase);
    public ConcurrentQueue<string> RecentErrors { get; } = new();

    public HttpLoadRunner(HttpLoadConfig config, Action<string> log, Action<string> status)
    {
        _config = config;
        _log = log;
        _status = status;
    }

    public async Task RunAsync(CancellationToken ct)
    {
        var scenarios = _config.Scenarios.Where(s => s.Enabled).ToList();
        if (scenarios.Count == 0)
            throw new InvalidOperationException("Не выбрано ни одного сценария.");

        foreach (var s in scenarios)
            Stats.TryAdd(s.Name, new ScenarioStats(s.Name));
        Stats.TryAdd("OVERALL", new ScenarioStats("OVERALL"));

        var baseUri = NormalizeBaseUri(_config.BaseUrl);
        using var handler = CreateHandler(_config.IgnoreSslErrors);
        using var client = new HttpClient(handler) { BaseAddress = baseUri, Timeout = TimeSpan.FromSeconds(30) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("LP.ThrustLoader/1.0");

        // Login if credentials provided (for auth scenarios)
        var needAuth = scenarios.Any(s => s.Kind is HttpScenarioKind.UsersInfo or HttpScenarioKind.UsersView);
        if (needAuth && !string.IsNullOrWhiteSpace(_config.Username))
        {
            _status("Статус: логин...");
            var ok = await TryLoginAsync(client, _config.Username!, _config.Password ?? string.Empty, ct);
            _log(ok ? "Логин: успешно (cookie установлена)." : "Логин: не удалось (продолжаю; auth-сценарии могут возвращать 401).");
        }

        // Warmup: discover user ids for UsersView if needed
        var discoveredUserIds = new ConcurrentQueue<Guid>();
        if (scenarios.Any(s => s.Kind == HttpScenarioKind.UsersView))
        {
            await DiscoverUserIdsAsync(client, discoveredUserIds, scenarios.First(s => s.Kind == HttpScenarioKind.VotesList && s.Enabled)?.VotesCount ?? 5, ct);
            _log($"Обнаружено id пользователей для /Users/view: {discoveredUserIds.Count}.");
        }

        var infinite = _config.DurationSeconds <= 0;
        var durationLabel = infinite ? "∞, до Стоп" : $"{_config.DurationSeconds} c";
        _status($"Статус: нагрузка ({_config.Concurrency} потоков, {durationLabel})...");
        var stopAt = infinite ? DateTime.MaxValue : DateTime.UtcNow + TimeSpan.FromSeconds(_config.DurationSeconds);
        var scenarioArr = scenarios.ToArray();

        var workers = Enumerable.Range(0, Math.Max(1, _config.Concurrency))
            .Select(i => Task.Run(() => WorkerLoopAsync(client, scenarioArr, discoveredUserIds, stopAt, ct), ct))
            .ToArray();

        try
        {
            await Task.WhenAll(workers);
        }
        catch (OperationCanceledException)
        {
            // stop requested
        }

        _status("Статус: завершено");
    }

    private async Task WorkerLoopAsync(HttpClient client, HttpScenarioConfig[] scenarios, ConcurrentQueue<Guid> userIds, DateTime stopAt, CancellationToken ct)
    {
        var rnd = Random.Shared;
        while (DateTime.UtcNow < stopAt && !ct.IsCancellationRequested)
        {
            var scenario = scenarios[rnd.Next(scenarios.Length)];
            var sample = await ExecuteScenarioAsync(client, scenario, userIds, ct);
            Record(sample);
        }
    }

    private async Task<RequestSample> ExecuteScenarioAsync(HttpClient client, HttpScenarioConfig scenario, ConcurrentQueue<Guid> userIds, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            HttpResponseMessage resp;
            switch (scenario.Kind)
            {
                case HttpScenarioKind.VotesList:
                    resp = await client.GetAsync($"Votes/list?count={scenario.VotesCount}", ct);
                    // try to harvest ids for view scenario
                    if (resp.IsSuccessStatusCode)
                        await TryHarvestIdsAsync(resp, userIds, ct);
                    else
                        await DrainAsync(resp, ct);
                    break;
                case HttpScenarioKind.AuthStatus:
                    resp = await client.GetAsync("auth/status", ct);
                    await DrainAsync(resp, ct);
                    break;
                case HttpScenarioKind.UsersInfo:
                    resp = await client.GetAsync("Users/info", ct);
                    await DrainAsync(resp, ct);
                    break;
                case HttpScenarioKind.UsersView:
                    if (!TryPick(userIds, out var id))
                    {
                        // fallback: try to get some ids quickly
                        var r = await client.GetAsync($"Votes/list?count=5", ct);
                        if (r.IsSuccessStatusCode) await TryHarvestIdsAsync(r, userIds, ct); else await DrainAsync(r, ct);
                        TryPick(userIds, out id);
                    }
                    if (id == Guid.Empty)
                    {
                        sw.Stop();
                        return new RequestSample { Scenario = scenario.Name, Success = false, StatusCode = 0, LatencyMs = sw.ElapsedMilliseconds, Error = "No user id available" };
                    }
                    resp = await client.GetAsync($"Users/view?id={id}", ct);
                    await DrainAsync(resp, ct);
                    break;
                default:
                    throw new NotSupportedException(scenario.Kind.ToString());
            }

            sw.Stop();
            var ok = (int)resp.StatusCode >= 200 && (int)resp.StatusCode < 400;
            return new RequestSample { Scenario = scenario.Name, Success = ok, StatusCode = (int)resp.StatusCode, LatencyMs = sw.ElapsedMilliseconds, Error = ok ? null : resp.ReasonPhrase };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new RequestSample { Scenario = scenario.Name, Success = false, StatusCode = 0, LatencyMs = sw.ElapsedMilliseconds, Error = ex.GetType().Name + ": " + ex.Message };
        }
    }

    private async Task<bool> TryLoginAsync(HttpClient client, string username, string password, CancellationToken ct)
    {
        try
        {
            // Try cookie auth endpoint first
            var payload = new { Username = username, Password = password };
            var resp = await client.PostAsJsonAsync("auth/login", payload, ct);
            if (resp.IsSuccessStatusCode)
            {
                await DrainAsync(resp, ct);
                return true;
            }
            await DrainAsync(resp, ct);
            // fallback: Users/login (token-based, not cookie) - not useful for cookie auth, but try anyway
            resp = await client.PostAsJsonAsync("Users/login", payload, ct);
            var ok = resp.IsSuccessStatusCode;
            await DrainAsync(resp, ct);
            return ok;
        }
        catch
        {
            return false;
        }
    }

    private async Task DiscoverUserIdsAsync(HttpClient client, ConcurrentQueue<Guid> ids, int count, CancellationToken ct)
    {
        try
        {
            var resp = await client.GetAsync($"Votes/list?count={Math.Max(count, 10)}", ct);
            if (resp.IsSuccessStatusCode)
                await TryHarvestIdsAsync(resp, ids, ct);
            else
                await DrainAsync(resp, ct);
        }
        catch { /* ignore */ }
    }

    private static async Task TryHarvestIdsAsync(HttpResponseMessage resp, ConcurrentQueue<Guid> ids, CancellationToken ct)
    {
        try
        {
            await using var stream = await resp.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var el in doc.RootElement.EnumerateArray())
                {
                    if (el.TryGetProperty("id", out var idProp) || el.TryGetProperty("Id", out idProp))
                    {
                        if (idProp.ValueKind == JsonValueKind.String && Guid.TryParse(idProp.GetString(), out var g))
                            ids.Enqueue(g);
                    }
                }
            }
        }
        catch
        {
            // ignore parse errors
        }
    }

    private static async Task DrainAsync(HttpResponseMessage resp, CancellationToken ct)
    {
        try { await resp.Content.CopyToAsync(Stream.Null, ct); } catch { }
    }

    private static bool TryPick(ConcurrentQueue<Guid> q, out Guid id)
    {
        // rotate: dequeue/enqueue to spread load
        if (q.TryDequeue(out id))
        {
            if (id != Guid.Empty) q.Enqueue(id);
            return id != Guid.Empty;
        }
        id = Guid.Empty;
        return false;
    }

    private void Record(RequestSample sample)
    {
        if (Stats.TryGetValue(sample.Scenario, out var s)) s.Add(sample.LatencyMs, sample.Success);
        if (Stats.TryGetValue("OVERALL", out var o)) o.Add(sample.LatencyMs, sample.Success);
        if (!sample.Success && sample.Error != null)
        {
            RecentErrors.Enqueue($"[{DateTime.Now:HH:mm:ss}] {sample.Scenario}: {(sample.StatusCode == 0 ? "" : sample.StatusCode + " ")}{sample.Error}");
            while (RecentErrors.Count > 200 && RecentErrors.TryDequeue(out _)) { }
        }
    }

    private static Uri NormalizeBaseUri(string baseUrl)
    {
        var u = baseUrl.Trim();
        if (!u.EndsWith('/')) u += "/";
        return new Uri(u);
    }

    private static HttpClientHandler CreateHandler(bool ignoreSsl)
    {
        var h = new HttpClientHandler
        {
            UseCookies = true,
            CookieContainer = new CookieContainer(),
            AutomaticDecompression = DecompressionMethods.All
        };
        if (ignoreSsl)
        {
            h.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
        }
        return h;
    }
}