using System.Diagnostics;
using System.Text;

namespace LP.ThrustLoader;

public partial class Form1 : Form
{
    private CancellationTokenSource? _httpCts;
    private Task? _httpWorker;
    private HttpLoadRunner? _httpRunner;
    private DateTime _httpStartedAt;

    private CancellationTokenSource? _dbCts;
    private Task? _dbWorker;

    public Form1()
    {
        InitializeComponent();
        txtBaseUrl.Text = "https://localhost:7010";
        txtDbConnection.Text = "Server=localhost;Database=LP;Trusted_Connection=True;TrustServerCertificate=True;";
    }

    // ---------------- HTTP ----------------

    private async void btnHttpStart_Click(object? sender, EventArgs e)
    {
        var baseUrl = txtBaseUrl.Text.Trim();
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            MessageBox.Show(this, "Укажите Base URL backend.", "LP.ThrustLoader", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var scenarios = BuildHttpScenarios();
        if (scenarios.Count == 0)
        {
            MessageBox.Show(this, "Выберите хотя бы один HTTP-сценарий.", "LP.ThrustLoader", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var config = new HttpLoadConfig
        {
            BaseUrl = baseUrl,
            Concurrency = (int)numConcurrency.Value,
            DurationSeconds = (int)numDuration.Value,
            IgnoreSslErrors = chkIgnoreSsl.Checked,
            Username = string.IsNullOrWhiteSpace(txtUser.Text) ? null : txtUser.Text.Trim(),
            Password = txtPass.Text,
            Scenarios = scenarios
        };

        _httpCts = new CancellationTokenSource();
        _httpRunner = new HttpLoadRunner(config, HttpLog, SetHttpStatus);
        _httpStartedAt = DateTime.Now;

        SetHttpRunningState(true);
        var durText = config.DurationSeconds <= 0 ? "∞ (до Стоп)" : $"{config.DurationSeconds}s";
        HttpLog($"Старт HTTP-нагрузки: {config.BaseUrl}, concurrency={config.Concurrency}, duration={durText}");
        HttpLog("Сценарии: " + string.Join(", ", scenarios.Select(s => s.Name)));
        lblHttpLive.Text = "Запросов: 0 | OK: 0 | Ошибок: 0 | RPS: 0";
        httpStatsTimer.Start();

        _httpWorker = Task.Run(async () =>
        {
            try
            {
                await _httpRunner.RunAsync(_httpCts.Token);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                HttpLog($"Фатальная ошибка: {ex.GetType().Name}: {ex.Message}");
            }
        });

        try
        {
            await _httpWorker;
        }
        finally
        {
            httpStatsTimer.Stop();
            RefreshHttpLive();
            AppendHttpSummary();

            _httpWorker = null;
            _httpRunner = null;
            _httpCts?.Dispose();
            _httpCts = null;
            SetHttpRunningState(false);
            SetHttpStatus("Статус: остановлен");
        }
    }

    private List<HttpScenarioConfig> BuildHttpScenarios()
    {
        var list = new List<HttpScenarioConfig>();
        if (chkVotes.Checked)
            list.Add(new HttpScenarioConfig { Kind = HttpScenarioKind.VotesList, Enabled = true, Name = "GET /Votes/list", VotesCount = (int)numVotesCount.Value });
        if (chkAuthStatus.Checked)
            list.Add(new HttpScenarioConfig { Kind = HttpScenarioKind.AuthStatus, Enabled = true, Name = "GET /auth/status" });
        if (chkUsersInfo.Checked)
            list.Add(new HttpScenarioConfig { Kind = HttpScenarioKind.UsersInfo, Enabled = true, Name = "GET /Users/info" });
        if (chkUsersView.Checked)
            list.Add(new HttpScenarioConfig { Kind = HttpScenarioKind.UsersView, Enabled = true, Name = "GET /Users/view" });
        return list;
    }

    private void btnHttpStop_Click(object? sender, EventArgs e)
    {
        if (_httpCts == null) return;
        SetHttpStatus("Статус: остановка...");
        btnHttpStop.Enabled = false;
        _httpCts.Cancel();
    }

    private void btnHttpClear_Click(object? sender, EventArgs e)
    {
        txtHttpLog.Clear();
        lblHttpLive.Text = "Запросов: 0 | OK: 0 | Ошибок: 0 | RPS: 0";
    }

    private void httpStatsTimer_Tick(object? sender, EventArgs e)
    {
        RefreshHttpLive();
    }

    private void RefreshHttpLive()
    {
        var runner = _httpRunner;
        if (runner == null) return;
        if (!runner.Stats.TryGetValue("OVERALL", out var overall)) return;
        var snap = overall.Snapshot();
        var elapsed = Math.Max(0.001, (DateTime.Now - _httpStartedAt).TotalSeconds);
        var rps = snap.Count / elapsed;
        lblHttpLive.Text = $"Запросов: {snap.Count} | OK: {snap.Success} | Ошибок: {snap.Fail} | RPS: {rps:F1} | avg: {snap.AvgMs:F1} ms | p95: {snap.P95Ms:F1} ms";
    }

    private void AppendHttpSummary()
    {
        var runner = _httpRunner;
        if (runner == null) return;

        var elapsed = Math.Max(0.001, (DateTime.Now - _httpStartedAt).TotalSeconds);
        var sb = new StringBuilder();
        sb.AppendLine();
        sb.AppendLine("========== ИТОГИ ==========");
        sb.AppendLine($"Длительность: {elapsed:F1} c");
        sb.AppendLine($"{"Сценарий",-22} {"Count",8} {"OK",8} {"Fail",8} {"RPS",8} {"Avg",10} {"Min",8} {"P50",8} {"P95",8} {"P99",8} {"Max",8}");

        foreach (var kv in runner.Stats.OrderBy(k => k.Key == "OVERALL" ? 1 : 0).ThenBy(k => k.Key))
        {
            var s = kv.Value.Snapshot();
            var rps = s.Count / elapsed;
            sb.AppendLine($"{s.Name,-22} {s.Count,8} {s.Success,8} {s.Fail,8} {rps,8:F1} {s.AvgMs,10:F1} {s.MinMs,8} {s.P50Ms,8:F1} {s.P95Ms,8:F1} {s.P99Ms,8:F1} {s.MaxMs,8}");
        }

        if (!runner.RecentErrors.IsEmpty)
        {
            sb.AppendLine();
            sb.AppendLine("Последние ошибки:");
            foreach (var err in runner.RecentErrors.ToArray().TakeLast(30))
                sb.AppendLine("  " + err);
        }
        sb.AppendLine("===========================");
        HttpLog(sb.ToString().TrimEnd());
    }

    private void SetHttpRunningState(bool running)
    {
        txtBaseUrl.Enabled = !running;
        numConcurrency.Enabled = !running;
        numDuration.Enabled = !running;
        chkIgnoreSsl.Enabled = !running;
        txtUser.Enabled = !running;
        txtPass.Enabled = !running;
        chkVotes.Enabled = !running;
        numVotesCount.Enabled = !running;
        chkAuthStatus.Enabled = !running;
        chkUsersInfo.Enabled = !running;
        chkUsersView.Enabled = !running;
        btnHttpStart.Enabled = !running;
        btnHttpStop.Enabled = running;
    }

    private void SetHttpStatus(string text)
    {
        if (IsDisposed) return;
        if (InvokeRequired) BeginInvoke(() => lblHttpStatus.Text = text); else lblHttpStatus.Text = text;
    }

    private void HttpLog(string message)
    {
        if (IsDisposed) return;
        var line = message.Contains('\n') ? message + Environment.NewLine : $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}";
        if (InvokeRequired) BeginInvoke(() => txtHttpLog.AppendText(line)); else txtHttpLog.AppendText(line);
    }

    // ---------------- DB ----------------

    private async void btnDbStart_Click(object? sender, EventArgs e)
    {
        var connectionString = txtDbConnection.Text.Trim();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            MessageBox.Show(this, "Укажите строку подключения.", "LP.ThrustLoader", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (!chkDbUsers.Checked && !chkDbProfiles.Checked)
        {
            MessageBox.Show(this, "Выберите хотя бы одну таблицу (Users/Profiles).", "LP.ThrustLoader", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var config = new DbLoadConfig
        {
            ConnectionString = connectionString,
            IntervalSeconds = (int)numDbInterval.Value,
            Top = (int)numDbTop.Value,
            QueryUsers = chkDbUsers.Checked,
            QueryProfiles = chkDbProfiles.Checked
        };

        _dbCts = new CancellationTokenSource();
        var runner = new DbLoadRunner(config, DbLog, SetDbStatus);
        SetDbRunningState(true);
        DbLog("Старт DB-нагрузочного цикла.");

        _dbWorker = Task.Run(async () =>
        {
            try { await runner.RunAsync(_dbCts.Token); }
            catch (OperationCanceledException) { }
        });

        try { await _dbWorker; }
        finally
        {
            _dbWorker = null;
            _dbCts?.Dispose();
            _dbCts = null;
            SetDbRunningState(false);
            SetDbStatus("Статус: остановлен");
            DbLog("Цикл остановлен.");
        }
    }

    private void btnDbStop_Click(object? sender, EventArgs e)
    {
        if (_dbCts == null) return;
        SetDbStatus("Статус: остановка...");
        btnDbStop.Enabled = false;
        _dbCts.Cancel();
    }

    private void btnDbClear_Click(object? sender, EventArgs e)
    {
        txtDbLog.Clear();
    }

    private void SetDbRunningState(bool running)
    {
        txtDbConnection.Enabled = !running;
        numDbInterval.Enabled = !running;
        numDbTop.Enabled = !running;
        chkDbUsers.Enabled = !running;
        chkDbProfiles.Enabled = !running;
        btnDbStart.Enabled = !running;
        btnDbStop.Enabled = running;
    }

    private void SetDbStatus(string text)
    {
        if (IsDisposed) return;
        if (InvokeRequired) BeginInvoke(() => lblDbStatus.Text = text); else lblDbStatus.Text = text;
    }

    private void DbLog(string message)
    {
        if (IsDisposed) return;
        var line = message.Contains('\n') ? message + Environment.NewLine : $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}";
        if (InvokeRequired) BeginInvoke(() => txtDbLog.AppendText(line)); else txtDbLog.AppendText(line);
    }

    // ---------------- Common ----------------

    private void Form1_FormClosing(object? sender, FormClosingEventArgs e)
    {
        _httpCts?.Cancel();
        _dbCts?.Cancel();
    }
}
