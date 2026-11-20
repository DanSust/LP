namespace LP.ThrustLoader;

partial class Form1
{
    private System.ComponentModel.IContainer components = null;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            components.Dispose();
        }
        base.Dispose(disposing);
    }

    #region Windows Form Designer generated code

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        tabControl = new TabControl();
        tabHttp = new TabPage();
        splitHttp = new SplitContainer();
        grpHttpConfig = new GroupBox();
        lblBaseUrl = new Label();
        txtBaseUrl = new TextBox();
        lblConcurrency = new Label();
        numConcurrency = new NumericUpDown();
        lblDuration = new Label();
        numDuration = new NumericUpDown();
        lblSec2 = new Label();
        chkIgnoreSsl = new CheckBox();
        lblUser = new Label();
        txtUser = new TextBox();
        lblPass = new Label();
        txtPass = new TextBox();
        grpScenarios = new GroupBox();
        chkVotes = new CheckBox();
        lblVotesCount = new Label();
        numVotesCount = new NumericUpDown();
        chkAuthStatus = new CheckBox();
        chkUsersInfo = new CheckBox();
        chkUsersView = new CheckBox();
        btnHttpStart = new Button();
        btnHttpStop = new Button();
        btnHttpClear = new Button();
        lblHttpStatus = new Label();
        lblHttpLive = new Label();
        txtHttpLog = new TextBox();
        tabDb = new TabPage();
        grpDb = new GroupBox();
        lblDbConnection = new Label();
        txtDbConnection = new TextBox();
        lblDbInterval = new Label();
        numDbInterval = new NumericUpDown();
        lblDbSeconds = new Label();
        chkDbUsers = new CheckBox();
        chkDbProfiles = new CheckBox();
        lblDbTop = new Label();
        numDbTop = new NumericUpDown();
        btnDbStart = new Button();
        btnDbStop = new Button();
        btnDbClear = new Button();
        lblDbStatus = new Label();
        txtDbLog = new TextBox();
        httpStatsTimer = new System.Windows.Forms.Timer(components);
        tabControl.SuspendLayout();
        tabHttp.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)splitHttp).BeginInit();
        splitHttp.Panel1.SuspendLayout();
        splitHttp.Panel2.SuspendLayout();
        splitHttp.SuspendLayout();
        grpHttpConfig.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)numConcurrency).BeginInit();
        ((System.ComponentModel.ISupportInitialize)numDuration).BeginInit();
        grpScenarios.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)numVotesCount).BeginInit();
        tabDb.SuspendLayout();
        grpDb.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)numDbInterval).BeginInit();
        ((System.ComponentModel.ISupportInitialize)numDbTop).BeginInit();
        SuspendLayout();
        // 
        // tabControl
        // 
        tabControl.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        tabControl.Controls.Add(tabHttp);
        tabControl.Controls.Add(tabDb);
        tabControl.Location = new Point(12, 12);
        tabControl.Name = "tabControl";
        tabControl.SelectedIndex = 0;
        tabControl.Size = new Size(1060, 637);
        tabControl.TabIndex = 0;
        // 
        // tabHttp
        // 
        tabHttp.Controls.Add(splitHttp);
        tabHttp.Location = new Point(4, 24);
        tabHttp.Name = "tabHttp";
        tabHttp.Padding = new Padding(3);
        tabHttp.Size = new Size(1052, 609);
        tabHttp.TabIndex = 0;
        tabHttp.Text = "HTTP нагрузка (backend)";
        tabHttp.UseVisualStyleBackColor = true;
        // 
        // splitHttp
        // 
        splitHttp.Dock = DockStyle.Fill;
        splitHttp.FixedPanel = FixedPanel.Panel1;
        splitHttp.Location = new Point(3, 3);
        splitHttp.Name = "splitHttp";
        splitHttp.Orientation = Orientation.Horizontal;
        // 
        // splitHttp.Panel1
        // 
        splitHttp.Panel1.Controls.Add(grpHttpConfig);
        splitHttp.Panel1.Controls.Add(grpScenarios);
        splitHttp.Panel1.Controls.Add(btnHttpStart);
        splitHttp.Panel1.Controls.Add(btnHttpStop);
        splitHttp.Panel1.Controls.Add(btnHttpClear);
        splitHttp.Panel1.Controls.Add(lblHttpStatus);
        splitHttp.Panel1.Controls.Add(lblHttpLive);
        // 
        // splitHttp.Panel2
        // 
        splitHttp.Panel2.Controls.Add(txtHttpLog);
        splitHttp.Size = new Size(1046, 603);
        splitHttp.SplitterDistance = 170;
        splitHttp.TabIndex = 0;
        // 
        // grpHttpConfig
        // 
        grpHttpConfig.Controls.Add(lblBaseUrl);
        grpHttpConfig.Controls.Add(txtBaseUrl);
        grpHttpConfig.Controls.Add(lblConcurrency);
        grpHttpConfig.Controls.Add(numConcurrency);
        grpHttpConfig.Controls.Add(lblDuration);
        grpHttpConfig.Controls.Add(numDuration);
        grpHttpConfig.Controls.Add(lblSec2);
        grpHttpConfig.Controls.Add(chkIgnoreSsl);
        grpHttpConfig.Controls.Add(lblUser);
        grpHttpConfig.Controls.Add(txtUser);
        grpHttpConfig.Controls.Add(lblPass);
        grpHttpConfig.Controls.Add(txtPass);
        grpHttpConfig.Location = new Point(3, 3);
        grpHttpConfig.Name = "grpHttpConfig";
        grpHttpConfig.Size = new Size(1038, 78);
        grpHttpConfig.TabIndex = 0;
        grpHttpConfig.TabStop = false;
        grpHttpConfig.Text = "Подключение и параметры";
        // 
        // lblBaseUrl
        // 
        lblBaseUrl.AutoSize = true;
        lblBaseUrl.Location = new Point(10, 22);
        lblBaseUrl.Name = "lblBaseUrl";
        lblBaseUrl.Size = new Size(58, 15);
        lblBaseUrl.TabIndex = 0;
        lblBaseUrl.Text = "Base URL:";
        // 
        // txtBaseUrl
        // 
        txtBaseUrl.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        txtBaseUrl.Location = new Point(71, 19);
        txtBaseUrl.Name = "txtBaseUrl";
        txtBaseUrl.Size = new Size(380, 23);
        txtBaseUrl.TabIndex = 0;
        // 
        // lblConcurrency
        // 
        lblConcurrency.AutoSize = true;
        lblConcurrency.Location = new Point(460, 22);
        lblConcurrency.Name = "lblConcurrency";
        lblConcurrency.Size = new Size(102, 15);
        lblConcurrency.TabIndex = 1;
        lblConcurrency.Text = "Параллельность:";
        // 
        // numConcurrency
        // 
        numConcurrency.Location = new Point(562, 20);
        numConcurrency.Maximum = new decimal(new int[] { 500, 0, 0, 0 });
        numConcurrency.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
        numConcurrency.Name = "numConcurrency";
        numConcurrency.Size = new Size(52, 23);
        numConcurrency.TabIndex = 2;
        numConcurrency.Value = new decimal(new int[] { 10, 0, 0, 0 });
        // 
        // lblDuration
        // 
        lblDuration.AutoSize = true;
        lblDuration.Location = new Point(620, 22);
        lblDuration.Name = "lblDuration";
        lblDuration.Size = new Size(87, 15);
        lblDuration.TabIndex = 3;
        lblDuration.Text = "Длительность:";
        // 
        // numDuration
        // 
        numDuration.Location = new Point(708, 18);
        numDuration.Maximum = new decimal(new int[] { 36000, 0, 0, 0 });
        numDuration.Minimum = new decimal(new int[] { 1, 0, 0, int.MinValue });
        numDuration.Name = "numDuration";
        numDuration.Size = new Size(60, 23);
        numDuration.TabIndex = 4;
        numDuration.Value = new decimal(new int[] { 30, 0, 0, 0 });
        // 
        // lblSec2
        // 
        lblSec2.AutoSize = true;
        lblSec2.Location = new Point(774, 22);
        lblSec2.Name = "lblSec2";
        lblSec2.Size = new Size(100, 15);
        lblSec2.TabIndex = 5;
        lblSec2.Text = "сек (0/-1 = беск.)";
        // 
        // chkIgnoreSsl
        // 
        chkIgnoreSsl.AutoSize = true;
        chkIgnoreSsl.Checked = true;
        chkIgnoreSsl.CheckState = CheckState.Checked;
        chkIgnoreSsl.Location = new Point(810, 20);
        chkIgnoreSsl.Name = "chkIgnoreSsl";
        chkIgnoreSsl.Size = new Size(115, 19);
        chkIgnoreSsl.TabIndex = 6;
        chkIgnoreSsl.Text = "Игнор. SSL (dev)";
        // 
        // lblUser
        // 
        lblUser.AutoSize = true;
        lblUser.Location = new Point(10, 50);
        lblUser.Name = "lblUser";
        lblUser.Size = new Size(84, 15);
        lblUser.TabIndex = 7;
        lblUser.Text = "Логин (email):";
        // 
        // txtUser
        // 
        txtUser.Location = new Point(107, 47);
        txtUser.Name = "txtUser";
        txtUser.Size = new Size(200, 23);
        txtUser.TabIndex = 8;
        // 
        // lblPass
        // 
        lblPass.AutoSize = true;
        lblPass.Location = new Point(320, 50);
        lblPass.Name = "lblPass";
        lblPass.Size = new Size(52, 15);
        lblPass.TabIndex = 9;
        lblPass.Text = "Пароль:";
        // 
        // txtPass
        // 
        txtPass.Location = new Point(378, 47);
        txtPass.Name = "txtPass";
        txtPass.PasswordChar = '*';
        txtPass.Size = new Size(180, 23);
        txtPass.TabIndex = 10;
        // 
        // grpScenarios
        // 
        grpScenarios.Controls.Add(chkVotes);
        grpScenarios.Controls.Add(lblVotesCount);
        grpScenarios.Controls.Add(numVotesCount);
        grpScenarios.Controls.Add(chkAuthStatus);
        grpScenarios.Controls.Add(chkUsersInfo);
        grpScenarios.Controls.Add(chkUsersView);
        grpScenarios.Location = new Point(3, 84);
        grpScenarios.Name = "grpScenarios";
        grpScenarios.Size = new Size(1038, 50);
        grpScenarios.TabIndex = 1;
        grpScenarios.TabStop = false;
        grpScenarios.Text = "Сценарии (эндпоинты backend)";
        // 
        // chkVotes
        // 
        chkVotes.AutoSize = true;
        chkVotes.Checked = true;
        chkVotes.CheckState = CheckState.Checked;
        chkVotes.Location = new Point(10, 22);
        chkVotes.Name = "chkVotes";
        chkVotes.Size = new Size(141, 19);
        chkVotes.TabIndex = 0;
        chkVotes.Text = "GET /Votes/list (anon)";
        // 
        // lblVotesCount
        // 
        lblVotesCount.AutoSize = true;
        lblVotesCount.Location = new Point(178, 23);
        lblVotesCount.Name = "lblVotesCount";
        lblVotesCount.Size = new Size(41, 15);
        lblVotesCount.TabIndex = 1;
        lblVotesCount.Text = "count:";
        // 
        // numVotesCount
        // 
        numVotesCount.Location = new Point(226, 19);
        numVotesCount.Maximum = new decimal(new int[] { 20, 0, 0, 0 });
        numVotesCount.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
        numVotesCount.Name = "numVotesCount";
        numVotesCount.Size = new Size(50, 23);
        numVotesCount.TabIndex = 2;
        numVotesCount.Value = new decimal(new int[] { 5, 0, 0, 0 });
        // 
        // chkAuthStatus
        // 
        chkAuthStatus.AutoSize = true;
        chkAuthStatus.Location = new Point(300, 22);
        chkAuthStatus.Name = "chkAuthStatus";
        chkAuthStatus.Size = new Size(115, 19);
        chkAuthStatus.TabIndex = 3;
        chkAuthStatus.Text = "GET /auth/status";
        // 
        // chkUsersInfo
        // 
        chkUsersInfo.AutoSize = true;
        chkUsersInfo.Location = new Point(440, 22);
        chkUsersInfo.Name = "chkUsersInfo";
        chkUsersInfo.Size = new Size(144, 19);
        chkUsersInfo.TabIndex = 4;
        chkUsersInfo.Text = "GET /Users/info (auth)";
        // 
        // chkUsersView
        // 
        chkUsersView.AutoSize = true;
        chkUsersView.Location = new Point(610, 22);
        chkUsersView.Name = "chkUsersView";
        chkUsersView.Size = new Size(247, 19);
        chkUsersView.TabIndex = 5;
        chkUsersView.Text = "GET /Users/view?id=... (auth, id из ленты)";
        // 
        // btnHttpStart
        // 
        btnHttpStart.Location = new Point(10, 140);
        btnHttpStart.Name = "btnHttpStart";
        btnHttpStart.Size = new Size(90, 25);
        btnHttpStart.TabIndex = 2;
        btnHttpStart.Text = "Старт";
        btnHttpStart.Click += btnHttpStart_Click;
        // 
        // btnHttpStop
        // 
        btnHttpStop.Enabled = false;
        btnHttpStop.Location = new Point(106, 140);
        btnHttpStop.Name = "btnHttpStop";
        btnHttpStop.Size = new Size(90, 25);
        btnHttpStop.TabIndex = 3;
        btnHttpStop.Text = "Стоп";
        btnHttpStop.Click += btnHttpStop_Click;
        // 
        // btnHttpClear
        // 
        btnHttpClear.Location = new Point(202, 140);
        btnHttpClear.Name = "btnHttpClear";
        btnHttpClear.Size = new Size(90, 25);
        btnHttpClear.TabIndex = 4;
        btnHttpClear.Text = "Очистить";
        btnHttpClear.Click += btnHttpClear_Click;
        // 
        // lblHttpStatus
        // 
        lblHttpStatus.AutoSize = true;
        lblHttpStatus.Location = new Point(310, 145);
        lblHttpStatus.Name = "lblHttpStatus";
        lblHttpStatus.Size = new Size(113, 15);
        lblHttpStatus.TabIndex = 5;
        lblHttpStatus.Text = "Статус: остановлен";
        // 
        // lblHttpLive
        // 
        lblHttpLive.AutoSize = true;
        lblHttpLive.Location = new Point(520, 145);
        lblHttpLive.Name = "lblHttpLive";
        lblHttpLive.Size = new Size(218, 15);
        lblHttpLive.TabIndex = 6;
        lblHttpLive.Text = "Запросов: 0 | OK: 0 | Ошибок: 0 | RPS: 0";
        // 
        // txtHttpLog
        // 
        txtHttpLog.Dock = DockStyle.Fill;
        txtHttpLog.Font = new Font("Consolas", 9F);
        txtHttpLog.Location = new Point(0, 0);
        txtHttpLog.Multiline = true;
        txtHttpLog.Name = "txtHttpLog";
        txtHttpLog.ReadOnly = true;
        txtHttpLog.ScrollBars = ScrollBars.Both;
        txtHttpLog.Size = new Size(1046, 429);
        txtHttpLog.TabIndex = 0;
        txtHttpLog.WordWrap = false;
        // 
        // tabDb
        // 
        tabDb.Controls.Add(grpDb);
        tabDb.Controls.Add(lblDbStatus);
        tabDb.Controls.Add(txtDbLog);
        tabDb.Location = new Point(4, 24);
        tabDb.Name = "tabDb";
        tabDb.Padding = new Padding(3);
        tabDb.Size = new Size(1052, 609);
        tabDb.TabIndex = 1;
        tabDb.Text = "DB (Users/Profiles)";
        tabDb.UseVisualStyleBackColor = true;
        // 
        // grpDb
        // 
        grpDb.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        grpDb.Controls.Add(lblDbConnection);
        grpDb.Controls.Add(txtDbConnection);
        grpDb.Controls.Add(lblDbInterval);
        grpDb.Controls.Add(numDbInterval);
        grpDb.Controls.Add(lblDbSeconds);
        grpDb.Controls.Add(chkDbUsers);
        grpDb.Controls.Add(chkDbProfiles);
        grpDb.Controls.Add(lblDbTop);
        grpDb.Controls.Add(numDbTop);
        grpDb.Controls.Add(btnDbStart);
        grpDb.Controls.Add(btnDbStop);
        grpDb.Controls.Add(btnDbClear);
        grpDb.Location = new Point(6, 6);
        grpDb.Name = "grpDb";
        grpDb.Size = new Size(1040, 80);
        grpDb.TabIndex = 0;
        grpDb.TabStop = false;
        grpDb.Text = "Прямые запросы к БД (EF, периодически)";
        // 
        // lblDbConnection
        // 
        lblDbConnection.AutoSize = true;
        lblDbConnection.Location = new Point(10, 22);
        lblDbConnection.Name = "lblDbConnection";
        lblDbConnection.Size = new Size(105, 15);
        lblDbConnection.TabIndex = 0;
        lblDbConnection.Text = "Connection string:";
        // 
        // txtDbConnection
        // 
        txtDbConnection.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        txtDbConnection.Location = new Point(126, 19);
        txtDbConnection.Name = "txtDbConnection";
        txtDbConnection.Size = new Size(900, 23);
        txtDbConnection.TabIndex = 1;
        // 
        // lblDbInterval
        // 
        lblDbInterval.AutoSize = true;
        lblDbInterval.Location = new Point(10, 50);
        lblDbInterval.Name = "lblDbInterval";
        lblDbInterval.Size = new Size(63, 15);
        lblDbInterval.TabIndex = 2;
        lblDbInterval.Text = "Интервал:";
        // 
        // numDbInterval
        // 
        numDbInterval.Location = new Point(126, 46);
        numDbInterval.Maximum = new decimal(new int[] { 3600, 0, 0, 0 });
        numDbInterval.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
        numDbInterval.Name = "numDbInterval";
        numDbInterval.Size = new Size(70, 23);
        numDbInterval.TabIndex = 3;
        numDbInterval.Value = new decimal(new int[] { 5, 0, 0, 0 });
        // 
        // lblDbSeconds
        // 
        lblDbSeconds.AutoSize = true;
        lblDbSeconds.Location = new Point(202, 50);
        lblDbSeconds.Name = "lblDbSeconds";
        lblDbSeconds.Size = new Size(25, 15);
        lblDbSeconds.TabIndex = 4;
        lblDbSeconds.Text = "сек";
        // 
        // chkDbUsers
        // 
        chkDbUsers.AutoSize = true;
        chkDbUsers.Checked = true;
        chkDbUsers.CheckState = CheckState.Checked;
        chkDbUsers.Location = new Point(250, 48);
        chkDbUsers.Name = "chkDbUsers";
        chkDbUsers.Size = new Size(54, 19);
        chkDbUsers.TabIndex = 5;
        chkDbUsers.Text = "Users";
        // 
        // chkDbProfiles
        // 
        chkDbProfiles.AutoSize = true;
        chkDbProfiles.Checked = true;
        chkDbProfiles.CheckState = CheckState.Checked;
        chkDbProfiles.Location = new Point(320, 48);
        chkDbProfiles.Name = "chkDbProfiles";
        chkDbProfiles.Size = new Size(65, 19);
        chkDbProfiles.TabIndex = 6;
        chkDbProfiles.Text = "Profiles";
        // 
        // lblDbTop
        // 
        lblDbTop.AutoSize = true;
        lblDbTop.Location = new Point(410, 50);
        lblDbTop.Name = "lblDbTop";
        lblDbTop.Size = new Size(32, 15);
        lblDbTop.TabIndex = 7;
        lblDbTop.Text = "TOP:";
        // 
        // numDbTop
        // 
        numDbTop.Location = new Point(450, 46);
        numDbTop.Maximum = new decimal(new int[] { 10000, 0, 0, 0 });
        numDbTop.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
        numDbTop.Name = "numDbTop";
        numDbTop.Size = new Size(70, 23);
        numDbTop.TabIndex = 8;
        numDbTop.Value = new decimal(new int[] { 100, 0, 0, 0 });
        // 
        // btnDbStart
        // 
        btnDbStart.Location = new Point(540, 45);
        btnDbStart.Name = "btnDbStart";
        btnDbStart.Size = new Size(80, 25);
        btnDbStart.TabIndex = 9;
        btnDbStart.Text = "Старт";
        btnDbStart.Click += btnDbStart_Click;
        // 
        // btnDbStop
        // 
        btnDbStop.Enabled = false;
        btnDbStop.Location = new Point(626, 45);
        btnDbStop.Name = "btnDbStop";
        btnDbStop.Size = new Size(80, 25);
        btnDbStop.TabIndex = 10;
        btnDbStop.Text = "Стоп";
        btnDbStop.Click += btnDbStop_Click;
        // 
        // btnDbClear
        // 
        btnDbClear.Location = new Point(712, 45);
        btnDbClear.Name = "btnDbClear";
        btnDbClear.Size = new Size(80, 25);
        btnDbClear.TabIndex = 11;
        btnDbClear.Text = "Очистить";
        btnDbClear.Click += btnDbClear_Click;
        // 
        // lblDbStatus
        // 
        lblDbStatus.AutoSize = true;
        lblDbStatus.Location = new Point(10, 92);
        lblDbStatus.Name = "lblDbStatus";
        lblDbStatus.Size = new Size(113, 15);
        lblDbStatus.TabIndex = 1;
        lblDbStatus.Text = "Статус: остановлен";
        // 
        // txtDbLog
        // 
        txtDbLog.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        txtDbLog.Font = new Font("Consolas", 9F);
        txtDbLog.Location = new Point(6, 112);
        txtDbLog.Multiline = true;
        txtDbLog.Name = "txtDbLog";
        txtDbLog.ReadOnly = true;
        txtDbLog.ScrollBars = ScrollBars.Both;
        txtDbLog.Size = new Size(1040, 491);
        txtDbLog.TabIndex = 2;
        txtDbLog.WordWrap = false;
        // 
        // httpStatsTimer
        // 
        httpStatsTimer.Interval = 500;
        httpStatsTimer.Tick += httpStatsTimer_Tick;
        // 
        // Form1
        // 
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(1084, 661);
        Controls.Add(tabControl);
        MinimumSize = new Size(1000, 600);
        Name = "Form1";
        StartPosition = FormStartPosition.CenterScreen;
        Text = "LP.ThrustLoader - нагрузочное тестирование";
        FormClosing += Form1_FormClosing;
        tabControl.ResumeLayout(false);
        tabHttp.ResumeLayout(false);
        splitHttp.Panel1.ResumeLayout(false);
        splitHttp.Panel1.PerformLayout();
        splitHttp.Panel2.ResumeLayout(false);
        splitHttp.Panel2.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)splitHttp).EndInit();
        splitHttp.ResumeLayout(false);
        grpHttpConfig.ResumeLayout(false);
        grpHttpConfig.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)numConcurrency).EndInit();
        ((System.ComponentModel.ISupportInitialize)numDuration).EndInit();
        grpScenarios.ResumeLayout(false);
        grpScenarios.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)numVotesCount).EndInit();
        tabDb.ResumeLayout(false);
        tabDb.PerformLayout();
        grpDb.ResumeLayout(false);
        grpDb.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)numDbInterval).EndInit();
        ((System.ComponentModel.ISupportInitialize)numDbTop).EndInit();
        ResumeLayout(false);
    }

    #endregion

    private TabControl tabControl;
    private TabPage tabHttp;
    private TabPage tabDb;
    private SplitContainer splitHttp;
    private GroupBox grpHttpConfig;
    private Label lblBaseUrl;
    private TextBox txtBaseUrl;
    private Label lblConcurrency;
    private NumericUpDown numConcurrency;
    private Label lblDuration;
    private NumericUpDown numDuration;
    private Label lblSec2;
    private CheckBox chkIgnoreSsl;
    private Label lblUser;
    private TextBox txtUser;
    private Label lblPass;
    private TextBox txtPass;
    private GroupBox grpScenarios;
    private CheckBox chkVotes;
    private Label lblVotesCount;
    private NumericUpDown numVotesCount;
    private CheckBox chkAuthStatus;
    private CheckBox chkUsersInfo;
    private CheckBox chkUsersView;
    private Button btnHttpStart;
    private Button btnHttpStop;
    private Button btnHttpClear;
    private Label lblHttpStatus;
    private Label lblHttpLive;
    private TextBox txtHttpLog;
    private GroupBox grpDb;
    private Label lblDbConnection;
    private TextBox txtDbConnection;
    private Label lblDbInterval;
    private NumericUpDown numDbInterval;
    private Label lblDbSeconds;
    private CheckBox chkDbUsers;
    private CheckBox chkDbProfiles;
    private Label lblDbTop;
    private NumericUpDown numDbTop;
    private Button btnDbStart;
    private Button btnDbStop;
    private Button btnDbClear;
    private Label lblDbStatus;
    private TextBox txtDbLog;
    private System.Windows.Forms.Timer httpStatsTimer;
}
