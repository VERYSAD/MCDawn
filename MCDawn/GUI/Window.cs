using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Media;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

namespace MCDawn.Gui
{
    public partial class Window : Form
    {
        Regex regex = new Regex(@"^([1-9]|[1-9][0-9]|1[0-9][0-9]|2[0-4][0-9]|25[0-5])(\." +
                                "([0-9]|[1-9][0-9]|1[0-9][0-9]|2[0-4][0-9]|25[0-5])){3}$");
        // for cross thread use
        delegate void StringCallback(string s);
        delegate void PlayerListCallback(List<Player> players);
        delegate void PlayerBotListCallBack(List<PlayerBot> playerbots);
        delegate void ReportCallback(Report r);
        delegate void VoidDelegate();

        public static event EventHandler Minimize;
        public NotifyIcon notifyIcon1 = new NotifyIcon();
        //  public static bool Minimized = false;
        public bool miniConsole = false;

        Player propertiesofplayer;
        Level propertiesoflevel;
        
        internal static Server s;
        
        // Console Beeps
        public SoundPlayer consoleSound = new SoundPlayer("extra/sounds/chatupdate.wav");

        bool shuttingDown = false;
        public Window() {
            InitializeComponent();
        }

        private const int CP_NOCLOSE_BUTTON = 0x200;
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams myCp = base.CreateParams;
                myCp.ClassStyle = myCp.ClassStyle | CP_NOCLOSE_BUTTON;
                return myCp;
            }
        } 

        private void Window_Minimize(object sender, EventArgs e)
        {
      /*     if (!Minimized)
            {
                Minimized = true;
                ntf.Text = "MCZall";
                ntf.Icon = this.Icon;
                ntf.Click += delegate
                {
                    try
                    {
                        Minimized = false;
                        this.ShowInTaskbar = true;
                        this.Show();
                        WindowState = FormWindowState.Normal;
                    }
                    catch (Exception ex) { MessageBox.Show(ex.Message); }
                };
                ntf.Visible = true;
                this.ShowInTaskbar = false;
            } */
        }

        public static Window thisWindow;

        public string uptime = "0 0 0 1";
        private void Window_Load(object sender, EventArgs e) {
            try
            {
                CheckForIllegalCrossThreadCalls = false;
                thisWindow = this;
                MaximizeBox = false;
                this.Text = "<server name here>";
                this.Icon = new Icon(System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("MCDawn.Lawl.ico"));

                this.Show();
                this.BringToFront();
                WindowState = FormWindowState.Normal;

                s = new Server();
                s.OnLog += WriteLine;
                s.OnCommand += newCommand;
                s.OnError += newError;
                s.OnSystem += newSystem;

                new Thread(() => {
                    Invoke(new Action(() => {
                        foreach (TabPage tP in tabControl1.TabPages)
                            tabControl1.SelectTab(tP);
                        tabControl1.SelectTab(tabControl1.TabPages[0]);
                    }));
                }).Start();

                s.HeartBeatFail += HeartBeatFail;
                s.OnURLChange += UpdateUrl;
                s.OnPlayerListChange += UpdateClientList;
                s.OnPlayerBotListChange += UpdateBotList;
                s.OnSettingsUpdate += SettingsUpdate;
                s.Start();
                notifyIcon1.Text = "MCDawn Server: " + Server.name;

                this.notifyIcon1.ContextMenuStrip = this.iconContext;
                this.notifyIcon1.Icon = this.Icon;
                this.notifyIcon1.Visible = true;
                this.notifyIcon1.MouseClick += new System.Windows.Forms.MouseEventHandler(this.notifyIcon1_MouseClick);

                System.Timers.Timer MapTimer = new System.Timers.Timer(10000);
                MapTimer.Elapsed += delegate
                {
                    UpdateMapList();
                }; MapTimer.Start();

                System.Timers.Timer uptimeTimer = new System.Timers.Timer(1000);
                uptimeTimer.Elapsed += delegate
                {
                    try
                    {
                        TimeSpan up = (DateTime.Now - Process.GetCurrentProcess().StartTime);
                        lblUptime.Text = up.Days + " days, " + up.Hours + " hours, " + up.Minutes + " minutes, " + up.Seconds + " seconds.";
                    }
                    catch { lblUptime.Text = "0 days, 0 hours, 0 minutes, 1 seconds."; }
                };

                System.Timers.Timer statsTimer = new System.Timers.Timer(10000); //10 Seconds
                statsTimer.Elapsed += delegate
                {
                    if (Server.PCCounter == null)
                    {
                        Server.PCCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                        Server.PCCounter.BeginInit();
                        Server.PCCounter.NextValue();
                    }
                    if (Server.ProcessCounter == null)
                    {
                        Server.ProcessCounter = new PerformanceCounter("Process", "% Processor Time", Process.GetCurrentProcess().ProcessName);
                        Server.ProcessCounter.BeginInit();
                        Server.ProcessCounter.NextValue();
                    }
                    SetLabel(lblCPU, Server.ProcessCounter.NextValue() + " / " + Server.PCCounter.NextValue() + " Processes");
                    SetLabel(lblMemory, Math.Round((double)Process.GetCurrentProcess().PrivateMemorySize64 / 1048576).ToString() + " Megabytes");
                    SetLabel(lblThreads, Process.GetCurrentProcess().Threads.Count + " Threads");
                }; statsTimer.Start();

                try
                {
                    using (WebClient w = new WebClient())
                        w.DownloadFile("http://updates.mcdawn.com/Changelog.txt", "Changelog.txt");
                }
                catch { }
                if (File.Exists("Changelog.txt"))
                {
                    txtChangelog.Clear();
                    new Thread(() => {
                        Invoke(new Action(() => {
                            foreach (string line in File.ReadAllLines("Changelog.txt"))
                                txtChangelog.AppendText(line + Environment.NewLine);
                        }));
                    }).Start();
                }
                Properties.Load("properties/server.properties");
                txtHost.Text = Server.ZallState;
                chkConsoleSounds.Checked = Server.consoleSound;
                // DevList
                string temp;
                foreach (string d in Server.devs)
                {
                    temp = d.Substring(0, 1);
                    temp = temp.ToUpper() + d.Remove(0, 1);
                    liDevs.Items.Add(temp);
                }
                // StaffList
                foreach (string d in Server.staff)
                {
                    temp = d.Substring(0, 1);
                    temp = temp.ToUpper() + d.Remove(0, 1);
                    liStaff.Items.Add(temp);
                }
                // Administration
                foreach (string d in Server.administration)
                {
                    temp = d.Substring(0, 1);
                    temp = temp.ToUpper() + d.Remove(0, 1);
                    liAdministration.Items.Add(temp);
                }
                lblCurVersion.Text = Server.Version;
                lblLatestVersion.Text = Server.LatestVersion();
                // Auto-Update on server start
                if (lblCurVersion.Text != lblLatestVersion.Text)
                    if (MessageBox.Show("New version found. Would you like to update?", "Update?", MessageBoxButtons.YesNo) == DialogResult.Yes)
                        new Updater().Show();
                string total = " / " + Server.players;
                lblTotalPlayers.Text = Player.number.ToString() + total;
                lblTotalGuests.Text = Player.guests.ToString() + total;
                lblTotalOps.Text = Player.ops.ToString() + total;
                if (Server.useMySQL)
                {
                    DataTable count = MySQL.fillData("SELECT COUNT(id) FROM players");
                    lblTotalPlayersVisited.Text = count.Rows[0]["COUNT(id)"] + " players have visited this server.";
                }
                else { lblTotalPlayersVisited.Text = "0"; }
                int bancount = Group.findPerm(LevelPermission.Banned).playerList.All().Count;
                lblTotalPlayersBanned.Text = bancount + " players have been banned.";
                UnloadedlistUpdate();
                lblUptime.Text = "0 days, 0 hours, 0 minutes, 1 seconds."; uptime = "0 0 0 1";
                uptimeTimer.Start();
                lblCPU.Text = "0 / 0 Processes";
                lblMemory.Text = "0 Megabytes";
                lblThreads.Text = "0 Threads";

                // Remote Tab
                try
                {
                    liRCUsers.Items.Clear();
                    RemoteServer.LoadUsers();
                    txtRCPort.Text = RemoteServer.port.ToString(); txtRCKey.Text = RemoteServer.rcpass;
                }
                catch { }
                chkUseRemote.Checked = Server.useRemote;
                // Map Viewer/Editor
                txtMapEditorLevelName.Text = Server.level;
                txtMapEditorX.Text = "0"; txtMapEditorY.Text = "0"; txtMapEditorZ.Text = "0";
                chkChatColors.Checked = Server.consoleChatColors;

                try
                {
                    UpdateMapList();
                    UnloadedlistUpdate();
                }
                catch { }

                foreach (string file in Directory.GetFiles("logs"))
                    if (!cmbLogs.Items.Contains(LogFileNameFormat(file)))
                        cmbLogs.Items.Add(LogFileNameFormat(file));
                cmbLogs.Text = LogFileNameFormat(Logger.LogPath);
                ScrollToBottom(txtLogs);

                foreach (string file in Directory.GetFiles("logs/errors"))
                    if (!cmbErrors.Items.Contains(LogFileNameFormat(file)))
                        cmbErrors.Items.Add(LogFileNameFormat(file));
                cmbErrors.Text = LogFileNameFormat(Logger.ErrorLogPath);
                ScrollToBottom(txtErrors);
            }
            catch (Exception ex) { Server.ErrorLog(ex); }
        }

        // guies dis fucking method is majic, you can liek call any action from any thred O_O_O_O_O
        public void SetLabel(Label lbl, string newText) { Invoke(new Action(() => lbl.Text = newText)); }

        public void ChangeColor(Color color, Color back)
        {
            foreach (TabPage tp in tabControl1.TabPages)
            {
                tp.BackColor = back;
                tp.ForeColor = color;
            }
            //BackColor = back;
            //ForeColor = color;
        }

        private void LoadPlayerTabDetails()
        {
            try
            {
                Player p = Player.Find(liPlayers.SelectedItem.ToString());
                if (p != null)
                {
                    txtPlayerLog.ScrollBars = ScrollBars.Vertical;
                    if (p.prefix != "")
                    {
                        if (Server.devs.Contains(p.name.ToLower())) { WritePlayerLine(p.prefix + " " + p.name + " (MCDawn Developer)"); }
                        else if (Server.staff.Contains(p.name.ToLower())) { WritePlayerLine(p.prefix + " " + p.name + " (MCDawn Staff)"); }
                        else if (Server.administration.Contains(p.name.ToLower())) { WritePlayerLine(p.prefix + " " + p.name + " (MCDawn Administrator)"); }
                        else { WritePlayerLine(p.prefix + " " + p.name); }
                    }
                    else
                    {
                        if (Server.devs.Contains(p.name.ToLower())) { WritePlayerLine(p.name + " (MCDawn Developer)"); }
                        else if (Server.staff.Contains(p.name.ToLower())) { WritePlayerLine(p.name + " (MCDawn Staff)"); }
                        else if (Server.administration.Contains(p.name.ToLower())) { WritePlayerLine(p.name + " (MCDawn Administrator)"); }
                        else { WritePlayerLine(p.name); }
                    }
                    {
                        propertiesofplayer = p;
                        txtPlayerName.Text = p.name;
                        txtLevel.Text = p.level.name;
                        txtRank.Text = p.group.name;
                        if (Server.afkset.Contains(p.name)) { txtStatus.Text = "AFK"; } else { txtStatus.Text = "Active"; }
                        txtIP.Text = p.ip;
                        txtDeaths.Text = p.deathCount.ToString();
                        txtModified.Text = p.overallBlocks.ToString();
                        txtKicks.Text = p.totalKicked.ToString();
                    }
                    {
                        if (p.joker) { btnJoker.Text = "Un-Joker"; } else { btnJoker.Text = "Joker"; }
                        if (p.frozen) { btnFreeze.Text = "Un-Freeze"; } else { btnFreeze.Text = "Freeze"; }
                        if (p.muted) { btnMute.Text = "Un-Mute"; } else { btnMute.Text = "Mute"; }
                        if (p.voice) { btnVoice.Text = "Un-Voice"; } else { btnVoice.Text = "Voice"; }
                        if (p.hidden) { btnHide.Text = "Un-Hide"; } else { btnHide.Text = "Hide"; }
                        if (p.jailed) { btnJail.Text = "Un-Jail"; } else { btnJail.Text = "Jail"; }
                    }
                }
            }
            catch { }
        }

        private void tabPage6_Click(object sender, EventArgs e)
        {
            UnloadedlistUpdate();
            UpdateMapList();
        }

        public void UpdatePlayersListBox()
        {
            liPlayers.Items.Clear();
            foreach (Player p in Player.players)
            {
                liPlayers.Items.Add(p.name);
            }
        }

        private void tabPage5_Click(object sender, EventArgs e)
        {
            UpdatePlayersListBox();
            LoadPlayerTabDetails();
        }

        void SettingsUpdate()
        {
            if (shuttingDown) return;
            // Main chat channel
            if (txtLog.InvokeRequired)
            {
                VoidDelegate d = new VoidDelegate(SettingsUpdate);
                this.Invoke(d);
            }  else {
                this.Text = Server.name + " - MCDawn Version: " + Server.Version;
            }
            // Op chat channel
            /*if (txtOpLog.InvokeRequired)
            {
                VoidDelegate d = new VoidDelegate(SettingsUpdate);
                this.Invoke(d);
            }
            else
            {
                this.Text = Server.name + " MCDawn Version: " + Server.Version;
            }*/
        }

        void HeartBeatFail() {
            WriteLine("Recent Heartbeat Failed");
        }

        string LogFileNameFormat(string input)
        {
            if (input.LastIndexOf(@"\") != -1) input = input.Substring(input.LastIndexOf(@"\") + 1);
            if (input.LastIndexOf("/") != -1) input = input.Substring(input.LastIndexOf("/") + 1);
            return input.Replace(".log", "").Replace(".txt", "");
        }

        void newError(string message)
        {
            try
            {
                if (txtErrors.InvokeRequired)
                {
                    LogDelegate d = new LogDelegate(newError);
                    this.Invoke(d, new object[] { message });
                }
                else
                {
                    if (LogFileNameFormat(cmbErrors.Text.ToLower()) == LogFileNameFormat(Logger.ErrorLogPath.ToLower()))
                        txtErrors.AppendText(message + Environment.NewLine);
                    ScrollToBottom(txtErrors);
                }
            } catch { }
        }
        void newSystem(string message)
        {
            try
            {
                if (txtSystem.InvokeRequired)
                {
                    LogDelegate d = new LogDelegate(newSystem);
                    this.Invoke(d, new object[] { message });
                }
                else
                {
                    txtSystem.AppendText(message + Environment.NewLine);
                }
            } catch { }
        }

        delegate void LogDelegate(string message);
        public void WriteLine(string s) { WriteLine(s, Server.consoleChatColors); }
        public void WriteLine(string s, bool parseColors = true)
        {
            if (shuttingDown) return;
            // Main chat channel
            if (this.InvokeRequired)
            {
                LogDelegate d = new LogDelegate(WriteLine);
                this.Invoke(d, new object[] { s });
            }
            else
            {
                if (Server.consoleSound && Window.thisWindow.WindowState == FormWindowState.Minimized) consoleSound.Play();
                if (!parseColors) { txtLog.AppendText(Player.RemoveAllColors(s) + Environment.NewLine); ScrollToBottom(txtLog); return; }
                s = Player.RemoveBadColors("&0" + s.Replace("&g", "&0").Replace("%g", "&0"));
                string nocolors = Player.RemoveAllColors(s);
                txtLog.AppendText(nocolors + Environment.NewLine);
                var sections = s.Split('&');
                string done = txtLog.Text.Remove(txtLog.Text.Length - nocolors.Length - 1);
                for (int i = 1; i < sections.Length; i++)
                {
                    string section = sections[i];
                    if (String.IsNullOrEmpty(section)) continue;
                    Color color = GetColor(section[0]);
                    section = section.Substring(1);
                    done += section;
                    ColorText(done.Length - section.Length, section.Length, color);
                }
                ScrollToBottom(txtLog);

                if (LogFileNameFormat(cmbLogs.Text.ToLower()) == LogFileNameFormat(Logger.LogPath.ToLower()))
                    txtLogs.AppendText(nocolors + Environment.NewLine);
                ScrollToBottom(txtLogs);
            }
        }

        public void ScrollToBottom(RichTextBox txt)
        {
            txt.SelectionStart = txt.Text.Length;
            txt.ScrollToCaret();
        }

        public void ScrollToBottom(TextBox txt)
        {
            txt.SelectionStart = txt.Text.Length;
            txt.ScrollToCaret();
        }

        public void ColorText(int start, int length, Color color)
        {
            txtLog.Select(start, length);
            txtLog.SelectionColor = color;
            txtLog.DeselectAll();
        }

        public Color GetColor(char ch)
        {
            switch (ch.ToString().ToLower())
            {
                case "a": return Color.LimeGreen;
                case "b": return Color.Aqua;
                case "c": return Color.Red;
                case "d": return Color.Pink;
                case "e": return Color.Yellow;
                case "f": return Color.Silver;
                case "g": case "0": return Color.Black;
                case "1": return Color.Navy;
                case "2": return Color.Green;
                case "3": return Color.Teal;
                case "4": return Color.Maroon;
                case "5": return Color.Purple;
                case "6": return Color.Gold;
                case "7": return Color.SlateGray;
                case "8": return Color.DarkSlateGray;
                case "9": return Color.Blue;
                default: return Color.Black;
            }
        }

        public void WriteAdminLine(string s) { txtAdminLog.AppendText("\r\n" + Player.RemoveAllColors(s)); }
        public void WriteOpLine(string s) { txtOpLog.AppendText("\r\n" + Player.RemoveAllColors(s)); }
        public void WriteGlobalLine(string s) { txtGlobalLog.AppendText("\r\n" + Player.RemoveAllColors(s)); }

        // Player Log
        public void WritePlayerLine(string message)
        {
            message = Player.RemoveAllColors(message);
            txtPlayerLog.AppendText(message + Environment.NewLine);
        }

        /// <summary>
        /// Updates the list of client names in the window
        /// </summary>
        /// <param name="players">The list of players to add</param>
        public void UpdateClientList(List<Player> players) {
            if (this.InvokeRequired) {
                PlayerListCallback d = new PlayerListCallback(UpdateClientList);
                this.Invoke(d, new object[] { players });
            } 
            else 
            {
                try
                {
                    liClients.Items.Clear();
                    liPlayers.Items.Clear();
                    Player.players.ForEach(delegate(Player p) 
                    {
                        string groupname = p.group.name;
                        if (Server.devs.Contains(p.name.ToLower())) { groupname = "MCDawn Developer"; }
                        else if (Server.staff.Contains(p.name.ToLower())) { groupname = "MCDawn Staff"; }
                        else if (Server.administration.Contains(p.name.ToLower())) { groupname = "MCDawn Administrator"; }
                        liClients.Items.Add(p.name + " (" + groupname + ")");
                        liPlayers.Items.Add(p.name + " (" + groupname + ")");
                    });
                    string total = " / " + Server.players.ToString();
                    lblTotalPlayers.Text = Player.number.ToString() + total;
                    lblTotalGuests.Text = Player.guests.ToString() + total;
                    lblTotalOps.Text = Player.ops.ToString() + total;
                    if (Server.useMySQL)
                    {
                        try
                        {
                            DataTable count = MySQL.fillData("SELECT COUNT(id) FROM players");
                            lblTotalPlayersVisited.Text = count.Rows[0]["COUNT(id)"] + " players have visited this server.";
                        }
                        catch { }
                    }
                    int bancount = Group.findPerm(LevelPermission.Banned).playerList.All().Count;
                    lblTotalPlayersBanned.Text = bancount + " players have been banned.";
                }
                catch { }
            }
        }
        
        public void UpdateMapList() {
            Invoke(new Action(delegate {
                liMaps.Items.Clear();
                liLoadedLevels.Items.Clear();
                foreach (Level level in Server.levels)
                {
                    liMaps.Items.Add(level.name + " - " + level.physics);
                    liLoadedLevels.Items.Add(level.name + " - " + level.physics);
                }
            }));
        }

        public void UpdateBotList(List<PlayerBot> bots)
        {
            if (this.InvokeRequired)
            {
                PlayerBotListCallBack d = new PlayerBotListCallBack(UpdateBotList);
                this.Invoke(d, new object[] { bots });
            }
            else
            {
                liPlayerBots.Items.Clear();
                PlayerBot.playerbots.ForEach(delegate(PlayerBot p) { liPlayerBots.Items.Add(p.name); });
            }
        }

        delegate void RemoteListCallBack(List<Remote> remotes);
        internal void UpdateRemoteList(List<Remote> remotes)
        {
            try
            {
                if (this.InvokeRequired)
                {
                    RemoteListCallBack d = new RemoteListCallBack(UpdateRemoteList);
                    this.Invoke(d, new object[] { remotes });
                }
                else
                {
                    liConnectedRCs.Items.Clear();
                    Remote.remotes.ForEach(delegate(Remote p) { if (!liConnectedRCs.Items.Contains(p.name)) { liConnectedRCs.Items.Add(p.name); } });
                }
            }
            catch { }
        }

        /// <summary>
        /// Places the server's URL at the top of the window
        /// </summary>
        /// <param name="s">The URL to display</param>
        public void UpdateUrl(string s)
        {
            if (this.InvokeRequired)
            {
                StringCallback d = new StringCallback(UpdateUrl);
                this.Invoke(d, new object[] { s });
            }
            else
                txtUrl.Text = s;
        }

        private void Window_FormClosing(object sender, FormClosingEventArgs e) {
            if (notifyIcon1 != null) {
                notifyIcon1.Visible = false;
            }
            MCDawn_.Gui.Program.ExitProgram(false);
        }

        private void txtInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                if (String.IsNullOrEmpty(txtInput.Text.Trim())) return;
                string text = txtInput.Text.Trim();
                string newtext = text;
                switch (text[0])
                {
                    case '#':
                        text = text.Remove(0, 1);
                        Player.GlobalMessageOps("To Ops &f-" + "&g Console [&a" + Server.ZallState + "&g]&f- " + text);
                        Server.s.Log("(OPs): Console: " + text);
                        //Server.s.OpLog("Console: " + text);
                        IRCBot.Say("Console: " + text, true);
                        //AllServerChat.Say("(OPs) Console [" + Server.ZallState + "]: " + text);
                        WriteOpLine("<CONSOLE>" + text);
                        break;
                    case ';':
                        text = text.Remove(0, 1);
                        Player.GlobalMessageAdmins("To Admins &f-" + "&g Console [&a" + Server.ZallState + "&g]&f- " + text);
                        Server.s.Log("(Admins): Console: " + text);
                        //Server.s.AdminLog("Console: " + text);
                        //IRCBot.Say("Console: " + text, true);
                        //AllServerChat.Say("(Admins) Console [" + Server.ZallState + "]: " + text);
                        WriteAdminLine("<CONSOLE>" + text);
                        break;
                    case '@':
                        text = text.Remove(0, 1).Trim();
                        string[] words = text.Split(new char[] { ' ' }, 2);
                        Player who = Player.Find(words[0]);
                        if (who == null) 
                        { 
                            Server.s.Log("Player could not be found!");
                            txtInput.Clear();
                            return; 
                        }
                        who.SendMessage("&bFrom Console: &f" + words[1]);
                        Server.s.Log("(whispers to " + who.name + ") <CONSOLE> " + words[1]);
                        if (!Server.devs.Contains(who.name.ToLower()))
                            Player.GlobalMessageDevs("To Devs &f-" + "&gConsole &b[>] " + who.color + who.name + "&f- " + words[1]);
                        //AllServerChat.Say("(whispers to " + who.name + ") <CONSOLE> " + words[1]);
                        break;
                    default:
                        Player.GlobalChat(null, "Console [&a" + Server.ZallState + "&g]: &f" + text, false);
                        IRCBot.Say("Console [" + Server.ZallState + "]: " + text);
                        Server.s.Log("<CONSOLE> " + text);
                        //AllServerChat.Say("Console [" + Server.ZallState + "]: " + text);
                        /*try { if (miniConsole) { Chat.thisChat.Log("<CONSOLE> " + text); } }
                        catch { }*/
                        break;
                }
                txtInput.Clear();
            }
        }

        private void txtAdminInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                if (txtAdminInput.Text == null || txtAdminInput.Text.Trim() == "") { return; }
                string text = txtAdminInput.Text.Trim();
                Player.GlobalMessageAdmins("To Admins &f-" + "&gConsole [&a" + Server.ZallState + "&g]&f- " + text);
                //IRCBot.Say("Console [" + Server.ZallState + "]: " + text, true);
                WriteAdminLine("<CONSOLE> " + text);
                //AllServerChat.Say("(Admins) Console [" + Server.ZallState + "]: " + text);
                Server.s.Log("(Admins): Console: " + text);
                txtAdminInput.Clear();
            }
        }

        private void txtOpInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                if (txtOpInput.Text == null || txtOpInput.Text.Trim() == "") { return; }
                string text = txtOpInput.Text.Trim();
                Player.GlobalMessageOps("To Ops &f-" + "&gConsole [&a" + Server.ZallState + "&g]&f- " + text);
                IRCBot.Say("Console [" + Server.ZallState + "]: " + text, true);
                WriteOpLine("<CONSOLE> " + text);
                //AllServerChat.Say("(OPs) Console [" + Server.ZallState + "]: " + text);
                Server.s.Log("(Ops): Console: " + text);
                txtOpInput.Clear();
            }
        }

        private void txtGlobalInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                if (txtGlobalInput.Text == null || txtGlobalInput.Text.Trim() == "") { return; }
                /*if (!glTimerStarted) { globalTimer.Start(); glTimerStarted = true; }
                if (glSecondsElapsed >= 10 && glSent >= 3) { WriteGlobalLine("Global Chat limited to 3 lines per 10 seconds..."); return; }
                if (glSent >= 3) { glSent = 0; }
                else { glSent++; }*/
                string text = txtGlobalInput.Text.Trim();
                Command.all.Find("global").Use(null, text);
                txtGlobalInput.Clear();
                //Server.s.Log("<[Global] Console [" + Server.ZallState + "]: " + text);
            }
        }

        private void txtCommands_KeyDown(object sender, KeyEventArgs e) {
            if (e.KeyCode == Keys.Enter) {
                string sentCmd = "", sentMsg = "";

                if (txtCommands.Text == null || txtCommands.Text.Trim() == "")
                {
                    newCommand("CONSOLE: Whitespace commands are not allowed.");
                    txtCommands.Clear();
                    return;
                }

                if (txtCommands.Text[0] == '/')
                    if (txtCommands.Text.Length > 1)
                        txtCommands.Text = txtCommands.Text.Substring(1);

                if (txtCommands.Text.IndexOf(' ') != -1) {
                    sentCmd = txtCommands.Text.Split(' ')[0];
                    sentMsg = txtCommands.Text.Substring(txtCommands.Text.IndexOf(' ') + 1);
                } else if (txtCommands.Text != "") {
                    sentCmd = txtCommands.Text;
                } else {
                    return;
                }

                /*if (sentCmd.ToLower() == "global")
                {
                    if (!glTimerStarted) { globalTimer.Start(); glTimerStarted = true; }
                    if (glSecondsElapsed >= 10 && glSent >= 3) { Server.s.Log("Global Chat limited to 3 lines per 10 seconds..."); return; }
                    if (glSent >= 3) { glSent = 0; }
                    else { glSent++; }
                }*/

                try 
                {
                    Command commandcmd = Command.all.Find(sentCmd);
                    if (commandcmd == null)
                    {
                        Server.s.Log("Unknown command \"" + sentCmd.ToLower() + "\"!");
                        txtCommands.Clear();
                        return;
                    }
                    commandcmd.Use(null, sentMsg);
                    newCommand("CONSOLE: USED /" + sentCmd + " " + sentMsg);
                } 
                catch (Exception ex) 
                {
                    Server.ErrorLog(ex);
                    newCommand("CONSOLE: Failed command."); 
                }

                txtCommands.Clear();
            }
        }

        private void btnClose_Click_1(object sender, EventArgs e) { 
            if (notifyIcon1 != null) {
                notifyIcon1.Visible = false;
            }
            MCDawn_.Gui.Program.ExitProgram(false); 
        }

        public void newCommand(string p) { 
            if (txtCommandsUsed.InvokeRequired)
            {
                LogDelegate d = new LogDelegate(newCommand);
                this.Invoke(d, new object[] { p });
            }
            else
            {
                txtCommandsUsed.AppendText("\r\n" + p); 
            }
        }

        void ChangeCheck(string newCheck) { Server.ZallState = newCheck; Properties.Save("properties/server.properties"); }

        private void txtHost_TextChanged(object sender, EventArgs e)
        {
            if (txtHost.Text != "") ChangeCheck(txtHost.Text);
        }

        private void btnProperties_Click_1(object sender, EventArgs e) {
            if (!prevLoaded) { PropertyForm = new PropertyWindow(); prevLoaded = true; }
            PropertyForm.Show();
        }

        private void btnUpdate_Click_1(object sender, EventArgs e) {
            if (!MCDawn_.Gui.Program.CurrentUpdate)
                MCDawn_.Gui.Program.UpdateCheck();
            else {
                Thread messageThread = new Thread(new ThreadStart(delegate {
                    MessageBox.Show("Already checking for updates.");
                })); messageThread.Start();
            }
        }

        public static bool prevLoaded = false;
        Form PropertyForm;

        private void gBChat_Enter(object sender, EventArgs e)
        {

        }

        private void btnExtra_Click_1(object sender, EventArgs e) {
            if (!prevLoaded) { PropertyForm = new PropertyWindow(); prevLoaded = true; }
            PropertyForm.Show();
            PropertyForm.Top = this.Top + this.Height - txtCommandsUsed.Height;
            PropertyForm.Left = this.Left;
        }

        private void notifyIcon1_MouseClick(object sender, MouseEventArgs e) {
            this.Show();
            this.BringToFront();
            WindowState = FormWindowState.Normal;
        }
        
        private void button1_Click_1(object sender, EventArgs e)
        {
            new Updater().Show();
        }

        private void tmrRestart_Tick(object sender, EventArgs e)
        {
            if (Server.autorestart)
            {
                if (DateTime.Now.TimeOfDay.CompareTo(Server.restarttime.TimeOfDay) > 0 && (DateTime.Now.TimeOfDay.CompareTo(Server.restarttime.AddSeconds(1).TimeOfDay)) < 0) {
                    Player.GlobalMessage("The time is now " + DateTime.Now.TimeOfDay);
                    Player.GlobalMessage("The server will now begin auto restart procedures.");
                    Server.s.Log("The time is now " + DateTime.Now.TimeOfDay);
                    Server.s.Log("The server will now begin auto restart procedures.");

                    if (notifyIcon1 != null) {
                        notifyIcon1.Icon = null;
                        notifyIcon1.Visible = false;
                    }
                    MCDawn_.Gui.Program.ExitProgram(true);
                }
            }
        }

        private void openConsole_Click(object sender, EventArgs e)
        {
            // Yes, it's a hacky fix.  Don't ask :v
            this.Show();
            this.BringToFront();
            WindowState = FormWindowState.Normal;
            this.Show();
            this.BringToFront();
            WindowState = FormWindowState.Normal;
        }

        private void shutdownServer_Click(object sender, EventArgs e)
        {
            if (notifyIcon1 != null)
            {
                notifyIcon1.Visible = false;
            }
            MCDawn_.Gui.Program.ExitProgram(false); 
        }

        private void voiceToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (this.liClients.SelectedIndex != -1)
            {
                Command.all.Find("voice").Use(null, this.liClients.SelectedItem.ToString().Remove(this.liClients.SelectedItem.ToString().IndexOf(" ")));
            }
        }

        private void whoisToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (this.liClients.SelectedIndex != -1)
            {
                Command.all.Find("whois").Use(null, this.liClients.SelectedItem.ToString().Remove(this.liClients.SelectedItem.ToString().IndexOf(" ")));
            }
        }

        private void kickToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (this.liClients.SelectedIndex != -1)
            {
                Command.all.Find("kick").Use(null, this.liClients.SelectedItem.ToString().Remove(this.liClients.SelectedItem.ToString().IndexOf(" ")) + " You were kicked by the Console!");
            }
        }


        private void banToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (this.liClients.SelectedIndex != -1)
            {
                Command.all.Find("ban").Use(null, this.liClients.SelectedItem.ToString().Remove(this.liClients.SelectedItem.ToString().IndexOf(" ")));
            }
        }

        private void liClients_MouseDown(object sender, MouseEventArgs e)
        {
            int i;
            i = liClients.IndexFromPoint(e.X, e.Y);
            liClients.SelectedIndex = i;
        }

        private void toolStripMenuItem2_Click(object sender, EventArgs e)
        {
            if (this.liMaps.SelectedIndex != -1)
            {
                Command.all.Find("physics").Use(null, this.liMaps.SelectedItem.ToString().Remove((this.liMaps.SelectedItem.ToString().Length - 4)) + " 0");
            }
        }

        private void toolStripMenuItem3_Click(object sender, EventArgs e)
        {
            if (this.liMaps.SelectedIndex != -1)
            {
                Command.all.Find("physics").Use(null, this.liMaps.SelectedItem.ToString().Remove((this.liMaps.SelectedItem.ToString().Length - 4)) + " 1");
            }
        }

        private void toolStripMenuItem4_Click(object sender, EventArgs e)
        {
            if (this.liMaps.SelectedIndex != -1)
            {
                Command.all.Find("physics").Use(null, this.liMaps.SelectedItem.ToString().Remove((this.liMaps.SelectedItem.ToString().Length - 4)) + " 2");
            }
        }

        private void toolStripMenuItem5_Click(object sender, EventArgs e)
        {
            if (this.liMaps.SelectedIndex != -1)
            {
                Command.all.Find("physics").Use(null, this.liMaps.SelectedItem.ToString().Remove((this.liMaps.SelectedItem.ToString().Length - 4)) + " 3");
            }
        }

        private void toolStripMenuItem6_Click(object sender, EventArgs e)
        {
            if (this.liMaps.SelectedIndex != -1)
            {
                Command.all.Find("physics").Use(null, this.liMaps.SelectedItem.ToString().Remove((this.liMaps.SelectedItem.ToString().Length - 4)) + " 4");
            }
        }

        private void unloadToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (this.liMaps.SelectedIndex != -1)
            {
                Command.all.Find("unload").Use(null, this.liMaps.SelectedItem.ToString().Remove((this.liMaps.SelectedItem.ToString().Length - 4)));
            }
        }

        private void finiteModeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (this.liMaps.SelectedIndex != -1)
            {
                Command.all.Find("map").Use(null, this.liMaps.SelectedItem.ToString().Remove((this.liMaps.SelectedItem.ToString().Length - 4)) + " finite");
            }
        }

        private void animalAIToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (this.liMaps.SelectedIndex != -1)
            {
                Command.all.Find("map").Use(null, this.liMaps.SelectedItem.ToString().Remove((this.liMaps.SelectedItem.ToString().Length - 4)) + " ai");
            }
        }

        private void edgeWaterToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (this.liMaps.SelectedIndex != -1)
            {
                Command.all.Find("map").Use(null, this.liMaps.SelectedItem.ToString().Remove((this.liMaps.SelectedItem.ToString().Length - 4)) + " edge");
            }
        }

        private void growingGrassToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (this.liMaps.SelectedIndex != -1)
            {
                Command.all.Find("map").Use(null, this.liMaps.SelectedItem.ToString().Remove((this.liMaps.SelectedItem.ToString().Length - 4)) + " grass");
            }
        }

        private void survivalDeathToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (this.liMaps.SelectedIndex != -1)
            {
                Command.all.Find("map").Use(null, this.liMaps.SelectedItem.ToString().Remove((this.liMaps.SelectedItem.ToString().Length - 4)) + " death");
            }
        }

        private void killerBlocksToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (this.liMaps.SelectedIndex != -1)
            {
                Command.all.Find("map").Use(null, this.liMaps.SelectedItem.ToString().Remove((this.liMaps.SelectedItem.ToString().Length - 4)) + " killer");
            }
        }

        private void rPChatToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (this.liMaps.SelectedIndex != -1)
            {
                Command.all.Find("map").Use(null, this.liMaps.SelectedItem.ToString().Remove((this.liMaps.SelectedItem.ToString().Length - 4)) + " chat");
            }
        }

        private void saveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (this.liMaps.SelectedIndex != -1)
            {
                Command.all.Find("save").Use(null, this.liMaps.SelectedItem.ToString().Remove((this.liMaps.SelectedItem.ToString().Length - 4)));
            }
        }

        private void liMaps_MouseDown(object sender, MouseEventArgs e)
        {
            int i;
            i = liMaps.IndexFromPoint(e.X, e.Y);
            liMaps.SelectedIndex = i;
        }

        private void tabControl1_Click(object sender, EventArgs e)
        {
            try { UpdatePlayersListBox(); }
            catch { }
            try { UpdateMapList(); UnloadedlistUpdate(); }
            catch { }
            ScrollToBottom(txtLogs);
            ScrollToBottom(txtErrors);
            foreach (TabPage tP in tabControl1.TabPages)
            {
                foreach (Control ctrl in tP.Controls)
                {
                    if (ctrl is TextBox)
                    {
                        TextBox txtBox = (TextBox)ctrl;
                        txtBox.Update();
                    }
                }
            }
        }

        private void txtCommandsUsed_TextChanged(object sender, EventArgs e)
        {

        }

        private void btnRestart_Click(object sender, EventArgs e)
        {
            /*if (notifyIcon1 != null)
            {
                notifyIcon1.Visible = false;
            }*/
            try
            {
                if (MessageBox.Show("Are you sure you want to restart?", "Restart?", MessageBoxButtons.OKCancel) == DialogResult.OK) 
                {
                    Player.GlobalMessage("Server Restart! Rejoin!");
                    Thread.Sleep(750);
                    MCDawn_.Gui.Program.ExitProgram(true);
                }
                else { }
            }
            catch { Server.s.Log("Error Restarting Server."); }
        }

        public void chkMaintenance_CheckedChanged(object sender, EventArgs e)
        {
            if (Server.maintenance == true)
            {
                Server.maintenance = false;
                Player.GlobalMessage("Server placed in Normal mode.");
                Server.s.Log("Server placed in Normal mode.");
                //chkmaintenance.Checked = false;
                return;
            }
            else
            {
                Server.maintenance = true;
                Player.GlobalMessage("Server placed in maintenance mode.");
                Server.s.Log("Server placed in maintenance mode.");
                //chkmaintenance.Checked = true;
                return;
            }
        }

        private void btnJoker_Click(object sender, EventArgs e)
        {
        	if (propertiesofplayer == null || !Player.players.Contains(propertiesofplayer))
        	{
        		WritePlayerLine("No Player Selected");
        		return;
        	}
        	
        	if (!File.Exists("text/joker.txt")) {
				WritePlayerLine("The joker file doesn't exist. Creating now (text/joker.txt)");
				File.Create("text/joker.txt");
				return;
			}
        	
        	if (String.IsNullOrEmpty(File.ReadAllText("text/joker.txt"))) {
				WritePlayerLine("You must enter the joker lines in text/joker.txt!");
				return;
			}
        	
        	if (Server.devs.Contains(propertiesofplayer.name.ToLower())) { WritePlayerLine("You can't Joker a MCDawn Developer!"); return; }
        	Command.all.Find("joker").Use(null, propertiesofplayer.name);
        	if (propertiesofplayer.joker == true)
        	{
        		WritePlayerLine("Joker'd Player");
        		btnJoker.Text = "Un-Joker";
        		return;
        	}
        	else
        	{
        		WritePlayerLine("Un-Joker'd Player");
        		btnJoker.Text = "Joker";
        		return;
        	}
        }

		private void btnWarn_Click(object sender, EventArgs e)
		{
			if (propertiesofplayer == null || !Player.players.Contains(propertiesofplayer))
			{
				WritePlayerLine("No Player Selected");
				return;
			}
			if (Server.devs.Contains(propertiesofplayer.name.ToLower()))
			{
				WritePlayerLine("You can't warn a MCDawn Developer!");
				return;
			}
			if (Server.staff.Contains(propertiesofplayer.name.ToLower()))
			{
				WritePlayerLine("You can't warn a MCDawn Staff Member!");
				return;
			}
			if (Server.administration.Contains(propertiesofplayer.name.ToLower()))
			{
				WritePlayerLine("You can't warn a MCDawn Administrator!");
				return;
			}
			Command.all.Find("warn").Use(null, propertiesofplayer.name);
			WritePlayerLine("Warned player");
			return;
		}

		private void btnFreeze_Click(object sender, EventArgs e)
		{
			if (propertiesofplayer == null || !Player.players.Contains(propertiesofplayer))
			{
				WritePlayerLine("No Player Selected");
				return;
			}
			if (Server.devs.Contains(propertiesofplayer.name.ToLower()))
			{
				WritePlayerLine("You can't freeze a MCDawn Developer!");
				return;
			}
			Command.all.Find("freeze").Use(null, propertiesofplayer.name);
			if (propertiesofplayer.frozen == true)
			{
				WritePlayerLine("Froze Player");
				btnFreeze.Text = "Defrost";
				return;
			}
			else
			{
				WritePlayerLine("Defrosted Player");
				btnFreeze.Text = "Freeze";
				return;
			}
		}

        private void btnKick_Click(object sender, EventArgs e)
        {
            try
            {
                if (propertiesofplayer == null || !Player.players.Contains(propertiesofplayer))
                {
                    WritePlayerLine("No Player Selected");
                    return;
                }
                if (Server.devs.Contains(propertiesofplayer.name.ToLower()))
                {
                    WritePlayerLine("You can't kick a MCDawn Developer!");
                    return;
                }
                if (Server.staff.Contains(propertiesofplayer.name.ToLower()))
                {
                    WritePlayerLine("You can't kick a MCDawn Staff Member!");
                    return;
                }
                if (Server.administration.Contains(propertiesofplayer.name.ToLower()))
                {
                    WritePlayerLine("You can't kick a MCDawn Administrator!");
                    return;
                }
                Command.all.Find("kick").Use(null, propertiesofplayer.name);
                WritePlayerLine("Player Kicked!");
                return;
            }
            catch { }
        }

        private void btnJail_Click(object sender, EventArgs e)
        {
            try
            {
                if (propertiesofplayer == null || !Player.players.Contains(propertiesofplayer))
                {
                    WritePlayerLine("No Player Selected");
                    return;
                }
                if (Server.devs.Contains(propertiesofplayer.name.ToLower())) { WritePlayerLine("You can't jail a MCDawn Developer!"); return; }
                if (Server.staff.Contains(propertiesofplayer.name.ToLower())) { WritePlayerLine("You can't jail a MCDawn Staff Member!"); return; }
                if (Server.administration.Contains(propertiesofplayer.name.ToLower())) { WritePlayerLine("You can't jail a MCDawn Administrator!"); return; }
                Command.all.Find("jail").Use(null, propertiesofplayer.name);
                if (propertiesofplayer.jailed == true)
                {
                    WritePlayerLine("Jailed Player");
                    btnJail.Text = "Un-Jail";
                    return;
                }
                else
                {
                    WritePlayerLine("Freed Player from Jail.");
                    btnJail.Text = "Jail";
                    return;
                }
            }
            catch { }
        }

        private void btnSlap_Click(object sender, EventArgs e)
        {
            try
            {
                if (propertiesofplayer == null || !Player.players.Contains(propertiesofplayer))
                {
                    WritePlayerLine("No Player Selected");
                    return;
                }
                if (Server.devs.Contains(propertiesofplayer.name.ToLower()))
                {
                    WritePlayerLine("You can't slap a MCDawn Developer!");
                    return;
                }
                Command.all.Find("slap").Use(null, propertiesofplayer.name);
                WritePlayerLine("Slapped Player");
            }
            catch { }
        }

        private void btnHide_Click(object sender, EventArgs e)
        {
            try
            {
                if (propertiesofplayer == null || !Player.players.Contains(propertiesofplayer))
                {
                    WritePlayerLine("No Player Selected");
                    return;
                }
                if (Server.devs.Contains(propertiesofplayer.name.ToLower()) || Server.staff.Contains(propertiesofplayer.name.ToLower()) || Server.administration.Contains(propertiesofplayer.name.ToLower())) { WritePlayerLine("Can't let you do that, Starfox."); return; }
                Command.all.Find("hide").Use(propertiesofplayer, "");
                if (propertiesofplayer.hidden == true)
                {
                    WritePlayerLine("Player hidden.");
                    btnHide.Text = "Un-Hide";
                    return;
                }
                else
                {
                    WritePlayerLine("Player un-hidden.");
                    btnHide.Text = "Hide";
                    return;
                }
            }
            catch { }
        }

        private void btnKill_Click(object sender, EventArgs e)
        {
            try
            {
                if (propertiesofplayer == null || !Player.players.Contains(propertiesofplayer))
                {
                    WritePlayerLine("No Player Selected");
                    return;
                }
                if (Server.devs.Contains(propertiesofplayer.name.ToLower()))
                {
                    WritePlayerLine("You can't kill a MCDawn Developer!");
                    return;
                }
                Command.all.Find("kill").Use(null, propertiesofplayer.name);
                WritePlayerLine("Killed Player");
                return;
            }
            catch { }
        }

        private void btnXBan_Click(object sender, EventArgs e)
        {
            try
            {
                if (propertiesofplayer == null || !Player.players.Contains(propertiesofplayer))
                {
                    WritePlayerLine("No Player Selected");
                    return;
                }
                if (Server.devs.Contains(propertiesofplayer.name.ToLower()))
                {
                    WritePlayerLine("You can't Xban a MCDawn Developer!");
                    return;
                }
                if (Server.staff.Contains(propertiesofplayer.name.ToLower()))
                {
                    WritePlayerLine("You can't Xban a MCDawn Staff Member!");
                    return;
                }
                if (Server.administration.Contains(propertiesofplayer.name.ToLower()))
                {
                    WritePlayerLine("You can't Xban a MCDawn Administrator!");
                    return;
                }
                Command.all.Find("xban").Use(null, propertiesofplayer.name);
                WritePlayerLine("XBanned player");
                return;
            }
            catch { }
        }

        private void btnBan_Click(object sender, EventArgs e)
        {
            try
            {
                if (propertiesofplayer == null || !Player.players.Contains(propertiesofplayer))
                {
                    WritePlayerLine("No Player Selected");
                    return;
                }
                if (Server.devs.Contains(propertiesofplayer.name.ToLower()))
                {
                    WritePlayerLine("You can't ban a MCDawn Developer!");
                    return;
                }
                if (Server.staff.Contains(propertiesofplayer.name.ToLower()))
                {
                    WritePlayerLine("You can't ban a MCDawn Staff Member!");
                    return;
                }
                if (Server.administration.Contains(propertiesofplayer.name.ToLower()))
                {
                    WritePlayerLine("You can't ban a MCDawn Administrator!");
                    return;
                }
                Command.all.Find("ban").Use(null, propertiesofplayer.name);
                WritePlayerLine("Banned player");
                return;
            }
            catch { }
        }

        private void btnBanIP_Click(object sender, EventArgs e)
        {
            try
            {
                if (propertiesofplayer == null || !Player.players.Contains(propertiesofplayer))
                {
                    WritePlayerLine("No Player Selected");
                    return;
                }
                if (Server.devs.Contains(propertiesofplayer.name.ToLower()))
                {
                    WritePlayerLine("You can't IP-Ban a MCDawn Developer!");
                    return;
                }
                if (Server.staff.Contains(propertiesofplayer.name.ToLower()))
                {
                    WritePlayerLine("You can't IP-Ban a MCDawn Staff Member!");
                    return;
                }
                if (Server.administration.Contains(propertiesofplayer.name.ToLower()))
                {
                    WritePlayerLine("You can't IP-Ban a MCDawn Administrator!");
                    return;
                }
                Command.all.Find("banip").Use(null, "@" + propertiesofplayer.name);
                WritePlayerLine("IP-Banned player");
                return;
            }
            catch { }
        }

        private void btnChatModeration_Click(object sender, EventArgs e)
        {
            try
            {
                Command.all.Find("moderate").Use(null, "");
                if (Server.chatmod)
                {
                    btnChatModeration.Text = "ChatMod Off";
                    WritePlayerLine("Chat Moderation enabled.");
                    return;
                }
                else
                {
                    btnChatModeration.Text = "ChatMod On";
                    WritePlayerLine("Chat Moderation disabled.");
                    return;
                }
            }
            catch { }
        }

        private void btnMute_Click(object sender, EventArgs e)
        {
            try
            {
                if (propertiesofplayer == null || !Player.players.Contains(propertiesofplayer))
                {
                    WritePlayerLine("No Player Selected");
                    return;
                }
                if (Server.devs.Contains(propertiesofplayer.name.ToLower())) { WritePlayerLine("You can't mute a MCDawn Developer!"); return; }
                Command.all.Find("mute").Use(null, propertiesofplayer.name);
                if (propertiesofplayer.muted == true)
                {
                    WritePlayerLine("Muted Player");
                    btnMute.Text = "Un-Mute";
                    return;
                }
                else
                {
                    WritePlayerLine("Un-Muted Player");
                    btnMute.Text = "Mute";
                    return;
                }
            }
            catch { }
        }

        private void btnVoice_Click(object sender, EventArgs e)
        {
            try
            {
                if (propertiesofplayer == null || !Player.players.Contains(propertiesofplayer))
                {
                    WritePlayerLine("No Player Selected");
                    return;
                }
                Command.all.Find("voice").Use(null, propertiesofplayer.name);
                if (propertiesofplayer.voice == true)
                {
                    WritePlayerLine("Voiced Player");
                    btnVoice.Text = "Un-Voice";
                    return;
                }
                else
                {
                    WritePlayerLine("Un-Voiced Player");
                    btnVoice.Text = "Voice";
                    return;
                }
            }
            catch { }
        }

        private void btnPromote_Click(object sender, EventArgs e)
        {
            try
            {
                if (propertiesofplayer == null || !Player.players.Contains(propertiesofplayer))
                {
                    WritePlayerLine("No Player Selected");
                    return;
                }
                Command.all.Find("promote").Use(null, propertiesofplayer.name);
                WritePlayerLine("Promoted Player.");
                return;
            }
            catch { }
        }

        private void btnDemote_Click(object sender, EventArgs e)
        {
            try
            {
                if (propertiesofplayer == null || !Player.players.Contains(propertiesofplayer))
                {
                    WritePlayerLine("No Player Selected");
                    return;
                }
                Command.all.Find("demote").Use(null, propertiesofplayer.name);
                WritePlayerLine("Demoted Player.");
                return;
            }
            catch { }
        }

        private void liPlayers_SelectedIndexChanged(object sender, EventArgs e)
        {
            LoadPlayerTabDetails();
        }

		private void btnInvincible_Click(object sender, EventArgs e)
		{
				if (propertiesofplayer == null || !Player.players.Contains(propertiesofplayer))
				{
					WritePlayerLine("No Player Selected");
					return;
				}
				//if (Server.devs.Contains(propertiesofplayer.name.ToLower())) { WritePlayerLine("Can't let you do that, Starfox."); return; }
				Command.all.Find("invincible").Use(null, propertiesofplayer.name);
				if (propertiesofplayer.muted == true) // TODO: Wat is this? It always says "De-Inv'd Player".
				{
					WritePlayerLine("Inv'd Player");
					btnMute.Text = "De-Invincible";
					return;
				}
				else
				{
					WritePlayerLine("De-Inv'd Player");
					btnMute.Text = "Invincible";
					return;
				}
		}

        private void btnNewLevel_Click(object sender, EventArgs e)
        {
            try
            {
                if (txtLevelName.Text == "" || txtXDim.Text == "" || txtYDim.Text == "" || txtZDim.Text == "" || cmbLevelType.SelectedItem == null || cmbLevelType.Text == "") { txtLevelLog.AppendText("Please fill in all the boxes." + Environment.NewLine); return; }
                if (File.Exists("levels/" + txtLevelName.Text + ".lvl")) { txtLevelLog.AppendText("Level already exists. Delete the existing level first to proceed."); return; }

                try
                {
                    Command.all.Find("newlvl").Use(null, txtLevelName.Text.ToLower() + " " + txtXDim.Text + " " + txtYDim.Text + " " + txtZDim.Text + " " + cmbLevelType.Text.ToLower());
                }
                catch
                {
                    txtLevelLog.AppendText("Error Creating Level." + Environment.NewLine);
                }

                if (File.Exists("levels/" + txtLevelName.Text + ".lvl"))
                {
                    txtLevelLog.AppendText("Level " + txtLevelName.Text + " created." + Environment.NewLine);
                    try
                    {
                        UnloadedlistUpdate();
                        UpdateMapList();
                    }
                    catch { }
                }
                else
                {
                    MessageBox.Show("levelfilenotfound");
                    txtLevelLog.AppendText("Error Creating Level." + Environment.NewLine);
                }
            }
            catch (Exception ex) { MessageBox.Show(ex.ToString()); txtLevelLog.AppendText("Error Creating Level." + Environment.NewLine); }
            finally
            {
                txtLevelName.Clear();
                txtXDim.Clear();
                txtYDim.Clear();
                txtZDim.Clear();
                cmbLevelType.Text = "";
            }
        }

        private void btnDeleteLevel_Click(object sender, EventArgs e)
        {
            try
            {
                if (txtDeleteLevelName.Text == "") { txtLevelLog.AppendText("Please specify a level name." + Environment.NewLine); return; }
                if (!File.Exists("levels/" + txtDeleteLevelName.Text + ".lvl")) { txtLevelLog.AppendText("Level does not exist, you can't delete it." + Environment.NewLine); }
                else if (txtDeleteLevelName.Text == Server.mainLevel.ToString()) { txtLevelLog.AppendText("Cannot delete the server's main level!"); return; }
                else
                {
                    Command.all.Find("deletelvl").Use(null, txtDeleteLevelName.Text);
                    txtLevelLog.AppendText("Level " + txtDeleteLevelName.Text + " has been deleted." + Environment.NewLine);
                    txtDeleteLevelName.Clear();
                    UpdateMapList();
                    UnloadedlistUpdate();
                }
            }
            catch { txtLevelLog.AppendText("Error Deleting Level." + Environment.NewLine); }
            finally { txtDeleteLevelName.Clear(); }
        }

        private void btnRenameLevel_Click(object sender, EventArgs e)
        {
            try
            {
                if ((txtCurName.Text == Server.mainLevel.ToString()) || (txtNewName.Text == Server.mainLevel.ToString())) { txtLevelLog.AppendText("Cannot rename the main level." + Environment.NewLine); return; }
                if (txtCurName.Text == "" || txtNewName.Text == "") { txtLevelLog.AppendText("Please fill in all the Level Name boxes." + Environment.NewLine); return; }
                if (File.Exists("levels/" + txtNewName.Text + ".lvl")) { txtLevelLog.AppendText("New Level already exists." + Environment.NewLine); return; }
                if (!File.Exists("levels/" + txtCurName.Text + ".lvl")) { txtLevelLog.AppendText("Current Level does not exist." + Environment.NewLine); return; }
                Command.all.Find("renamelvl").Use(null, txtCurName.Text + " " + txtNewName.Text);
                txtLevelLog.AppendText("Level " + txtCurName.Text + " renamed to " + txtNewName.Text + Environment.NewLine);
                txtCurName.Clear();
                txtNewName.Clear();
                UpdateMapList();
                UnloadedlistUpdate();
            }
            catch { txtLevelLog.AppendText("Error Renaming Level." + Environment.NewLine); }
            finally
            {
                txtCurName.Clear();
                txtNewName.Clear();
            }
        }
        public void UnloadedlistUpdate()
        {
            try
            {
                liUnloadedLevels.Items.Clear();

                string name;
                FileInfo[] fi = new DirectoryInfo("levels/").GetFiles("*.lvl");
                foreach (FileInfo file in fi)
                {
                    name = file.Name.Replace(".lvl", "");
                    if (Level.Find(name.ToLower()) == null)
                        liUnloadedLevels.Items.Add(name);
                }
            }
            catch { }
        }
        private void liLoadedLevels_SelectedIndexChanged(object sender, EventArgs e)
        {
            LoadLevelTabDetails();
        }
        private void LoadLevelTabDetails()
        {
            try
            {
                string lvl = (liLoadedLevels.SelectedItem != null ? liLoadedLevels.SelectedItem.ToString() : "");
                if (String.IsNullOrEmpty(lvl)) return;
                lvl = lvl.Contains("-") ? lvl.Substring(0, lvl.IndexOf("-")).Trim() : lvl;
                Level l = Level.Find(lvl);
                if (l != null)
                {
                    propertiesoflevel = l;
                    txtLevelMotd.Text = l.motd ?? "ignore";
                    txtPhysics.Value = l.physics;
                    chkGrassGrowing.Checked = l.GrassGrow;
                    chkRPChat.Checked = !l.worldChat;
                    chkKillerBlocks.Checked = l.Killer;
                    chkSurvivalDeath.Checked = l.Death;
                    chkFiniteMode.Checked = l.finite;
                    chkEdgeWater.Checked = l.edgeWater;
                    chkAnimalAI.Checked = l.ai;
                    chkAllowGuns.Checked = l.allowguns;
                    txtFall.Value = l.fall;
                    txtDrown.Value = l.drown;
                    chkUnload.Checked = l.unload;
                    chkAutoLoad.Checked = (File.Exists("text/autoload.txt") ? (File.ReadAllLines("text/autoload.txt").Contains(l.name) || File.ReadAllLines("text/autoload.txt").Contains(l.name.ToLower())) : false);
                    txtLevelLog.AppendText("Level " + l.name + Environment.NewLine);
                }
            }
            catch (Exception ex) { MessageBox.Show(ex.ToString()); txtLevelLog.AppendText("Failed to load Level details." + Environment.NewLine); }
        }

        private void btnPropertiesSave_Click(object sender, EventArgs e)
        {
            try
            {
                if (propertiesoflevel == null) return;
                Level l = propertiesoflevel;
                l.motd = txtLevelMotd.Text;
                l.motd = (String.IsNullOrEmpty(txtLevelMotd.Text) ? "ignore" : txtLevelMotd.Text);
                l.physics = (int)txtPhysics.Value;
                l.GrassGrow = chkGrassGrowing.Checked;
                l.worldChat = !chkRPChat.Checked;
                l.Killer = chkKillerBlocks.Checked;
                l.Death = chkSurvivalDeath.Checked;
                l.finite = chkFiniteMode.Checked;
                l.edgeWater = chkEdgeWater.Checked;
                l.ai = chkAnimalAI.Checked;
                l.allowguns = chkAllowGuns.Checked;
                l.fall = (int)txtFall.Value;
                l.drown = (int)txtDrown.Value;
                l.unload = chkUnload.Checked;
                {
                    List<string> oldlines = new List<string>();
                    using (StreamReader r = new StreamReader("text/autoload.txt"))
                    {
                        bool done = false;
                        string line;
                        while ((line = r.ReadLine()) != null)
                        {
                            if (line.ToLower().Contains(l.name.ToLower()))
                            {
                                if (chkAutoLoad.Checked == false)
                                {
                                    line = "";
                                }
                                done = true;
                            }
                            oldlines.Add(line);
                        }
                        if (chkAutoLoad.Checked == true && done == false)
                        {
                            oldlines.Add(l.name + "=" + l.physics);
                        }
                    }
                    File.Delete("text/autoload.txt");
                    using (StreamWriter SW = new StreamWriter("text/autoload.txt"))
                        foreach (string line in oldlines)
                            if (line.Trim() != "")
                                SW.WriteLine(line);
                }
                txtLevelLog.AppendText("Level " + l.name + " saved." + Environment.NewLine);
                UpdateMapList();
            }
            catch { txtLevelLog.AppendText("Error saving level." + Environment.NewLine); }
        }

        private void btnUnloadLevel_Click(object sender, EventArgs e)
        {
            try
            {
                string lvl = liLoadedLevels.SelectedItem.ToString();
                lvl = (lvl.Contains("-") ? lvl.Substring(0, lvl.IndexOf("-")).Trim() : lvl.Trim());
                if (String.IsNullOrEmpty(liLoadedLevels.SelectedItem.ToString())) { txtLevelLog.AppendText("No Level Selected!"); return; }
                Command.all.Find("unload").Use(null, lvl);
                UpdateMapList();
                UnloadedlistUpdate();
            }
            catch { txtLevelLog.AppendText("Error Unloading Level." + Environment.NewLine); }
        }

        private void btnLoadLevel_Click(object sender, EventArgs e)
        {
            try
            {
                string lvl = liUnloadedLevels.SelectedItem.ToString();
                if (String.IsNullOrEmpty(liUnloadedLevels.SelectedItem.ToString())) { txtLevelLog.AppendText("No Level Selected!"); return; }
                Command.all.Find("load").Use(null, lvl);
                UpdateMapList();
                UnloadedlistUpdate();
            }
            catch { txtLevelLog.AppendText("Error Loading Level." + Environment.NewLine); }
        }

        private void btnUpdatePlayerList_Click(object sender, EventArgs e)
        {
            LoadPlayerTabDetails();
        }

        private void btnUpdateLevelList_Click(object sender, EventArgs e)
        {
            UnloadedlistUpdate();
            UpdateMapList();
        }

        private void btnKillPhysics_Click_1(object sender, EventArgs e)
        {
            Command.all.Find("killphysics").Use(null, "");
            Server.s.Log("All physics killed.");
            txtLevelLog.AppendText("All physics killed." + Environment.NewLine);
        }

        // Console Sounds
        private void chkConsoleSounds_CheckedChanged(object sender, EventArgs e)
        {
            Server.consoleSound = chkConsoleSounds.Checked;
            Properties.Save("properties/server.properties");
            try
            {
                using (WebClient w = new WebClient())
                    if (!File.Exists("extra/sounds/chatupdate.wav"))
                        w.DownloadFile("http://dl.dropbox.com/u/43809284/chatupdate.wav", "extra/sounds/chatupdate.wav");
            }
            catch { }
        }
        private void chkConsoleSounds_Load(object sender, EventArgs e)
        {
            System.Windows.Forms.ToolTip t = new System.Windows.Forms.ToolTip();
            t.SetToolTip(this.chkConsoleSounds, "Checking this will make the console beep when the window is minimized.");
        }

        private void linkSite_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Process.Start("http://mcdawn.com");
        }

        private void linkForums_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Process.Start("http://forums.mcdawn.com");
        }

        private void btnPlay_Click(object sender, EventArgs e)
        {
            System.Diagnostics.Process.Start(txtUrl.Text);
        }

        private void btnUpdateChangelog_Click(object sender, EventArgs e)
        {
            try
            {
                using (WebClient w = new WebClient())
                    w.DownloadFile("http://updates.mcdawn.com/Changelog.txt", "Changelog.txt");
            }
            catch { }
            if (File.Exists("Changelog.txt"))
            {
                txtChangelog.Clear();
                new Thread(() => {
                    Invoke(new Action(() => {
                        foreach (string line in File.ReadAllLines("Changelog.txt"))
                            txtChangelog.AppendText(line + Environment.NewLine);
                    }));
                }).Start();
            }
        }

        private void menuItem2_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void menuItem4_Click(object sender, EventArgs e)
        {
            if (!prevLoaded) { PropertyForm = new PropertyWindow(); prevLoaded = true; }
            PropertyForm.Show();
        }

        private void menuItem5_Click(object sender, EventArgs e)
        {
            Process proc = Process.Start("MCDawn Updater", "main " + System.Diagnostics.Process.GetCurrentProcess().Id.ToString() + ".exe");
        }

        private void menuItem6_Click(object sender, EventArgs e)
        {
            try
            {
                if (MessageBox.Show("Are you sure you want to restart?", "Restart?", MessageBoxButtons.OKCancel) == DialogResult.OK)
                {
                    Player.GlobalMessage("Server Restart! Rejoin!");
                    Thread.Sleep(750);
                    MCDawn_.Gui.Program.ExitProgram(true);
                }
                else { }
            }
            catch { Server.s.Log("Error Restarting Server."); }
        }

        private void menuItem7_Click(object sender, EventArgs e)
        {
            Close();
        }

        public bool addrcuser;
        private void btnAddRCUser_Click(object sender, EventArgs e)
        {
            addrcuser = true;
            AddRemoveRemote arr = new AddRemoveRemote();
            arr.Show();
        }

        private void txtRCKey_TextChanged(object sender, EventArgs e)
        {
            if (txtRCKey.Text != "") { RemoteServer.rcpass = txtRCKey.Text; Properties.Save("properties/server.properties"); }
        }

        private void txtRCPort_TextChanged(object sender, EventArgs e)
        {
            try { if (txtRCPort.Text != "") { RemoteServer.port = Convert.ToUInt16(txtRCPort.Text); Properties.Save("properties/server.properties"); } }
            catch { }
        }

        private void btnGenerateRCKey_Click(object sender, EventArgs e)
        {
            RemoteServer.rcpass = "";
            string rndchars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            Random rnd = new Random();
            for (int i = 0; i < 8; ++i) { RemoteServer.rcpass += rndchars[rnd.Next(rndchars.Length)]; }
            txtRCKey.Text = RemoteServer.rcpass;
            Properties.Save("properties/server.properties");
        }

        private void chkUseRemote_CheckedChanged(object sender, EventArgs e)
        {
            Server.useRemote = chkUseRemote.Checked;
            Properties.Save("properties/server.properties");
        }

        private void btnRemoveRCUser_Click(object sender, EventArgs e)
        {
            addrcuser = false;
            AddRemoveRemote arr = new AddRemoveRemote();
            arr.Show();
        }

        private void btnRCPortCheck_Click(object sender, EventArgs e)
        {
            try
            {
                lblRCCheckPortResult.ForeColor = Color.Black;
                lblRCCheckPortResult.Text = "Checking...";
                using (WebClient w = new WebClient())
                {
                    if (w.DownloadString("http://ll.www.utorrent.com:16000/testport?port=" + txtRCPort.Text + "&plain=1") == "ok")
                    {
                        lblRCCheckPortResult.ForeColor = Color.Green;
                        lblRCCheckPortResult.Text = "Port Open!";
                    }
                    else if (w.DownloadString("http://ll.www.utorrent.com:16000/testport?port=" + txtRCPort.Text + "&plain=1") == "failed")
                    {
                        lblRCCheckPortResult.ForeColor = Color.Red;
                        lblRCCheckPortResult.Text = "Port Closed.";
                    } 
                }
            }
            catch (Exception ex) 
            { 
                Server.ErrorLog(ex);
                lblRCCheckPortResult.ForeColor = Color.Yellow;
                lblRCCheckPortResult.Text = "Error Checking.";
            }
        }

        private void btnMapEditorChange_Click(object sender, EventArgs e)
        {
            MapEditorChangeBlock();
        }

        public void MapEditorChangeBlock()
        {
            try
            {
                Level l = Level.Find(txtMapEditorLevelName.Text);
                if (l == null) { l = Level.Load(txtMapEditorLevelName.Text); }
                if (l == null) { MessageBox.Show("Level " + txtMapEditorLevelName.Text + " could not be found.", "Map Editor"); return; }
                ushort x, y, z;
                if (!ushort.TryParse(txtMapEditorX.Text, out x) || (x >= l.width || x < 0)) { txtMapEditorX.Text = "0"; MessageBox.Show("Invalid x value.", "Map Editor"); return; }
                if (!ushort.TryParse(txtMapEditorY.Text, out y) || (y >= l.height || y < 0)) { txtMapEditorY.Text = "0"; MessageBox.Show("Invalid y value.", "Map Editor"); return; }
                if (!ushort.TryParse(txtMapEditorZ.Text, out z) || (z >= l.depth || z < 0)) { txtMapEditorZ.Text = "0"; MessageBox.Show("Invalid z value.", "Map Editor"); return; }
                txtMapEditorCurrentBlock.Text = Block.Name(l.GetTile(x, y, z));

                if (String.IsNullOrEmpty(txtMapEditorChangeBlock.Text)) { MessageBox.Show("Enter a block to change the selected block to."); return; }
                byte b = Block.Byte(txtMapEditorChangeBlock.Text);
                if (Block.Name(b).ToLower() == "unknown") { MessageBox.Show("Block could not be found.", "Map Editor"); return; }
                l.SetTile(x, y, z, b); Player.GlobalBlockchange(l, x, y, z, b);
                txtMapEditorCurrentBlock.Text = Block.Name(l.GetTile(x, y, z));
                MapViewerUpdateLevel();
            }
            catch (Exception ex) { Server.ErrorLog(ex); return; }
        }

        public void MapEditorUpdateBlock()
        {
            try
            {
                Level l = Level.Find(txtMapEditorLevelName.Text);
                if (l == null) { l = Level.Load(txtMapEditorLevelName.Text); }
                if (l == null) { MessageBox.Show("Level " + txtMapEditorLevelName.Text + " could not be found.", "Map Editor"); return; }
                ushort x, y, z;
                if (!ushort.TryParse(txtMapEditorX.Text, out x) || (x >= l.width || x < 0)) { txtMapEditorX.Text = "0"; MessageBox.Show("Invalid x value.", "Map Editor"); return; }
                if (!ushort.TryParse(txtMapEditorY.Text, out y) || (y >= l.depth || y < 0)) { txtMapEditorY.Text = "0"; MessageBox.Show("Invalid y value.", "Map Editor"); return; }
                if (!ushort.TryParse(txtMapEditorZ.Text, out z) || (z >= l.height || z < 0)) { txtMapEditorZ.Text = "0"; MessageBox.Show("Invalid z value.", "Map Editor"); return; }
                txtMapEditorCurrentBlock.Text = Block.Name(l.GetTile(x, y, z));
                MapViewerUpdateLevel();
            }
            catch (Exception ex) { Server.ErrorLog(ex); return; }
        }

        private void btnMapEditorUpdate_Click(object sender, EventArgs e)
        {
            MapEditorUpdateBlock();
        }

        private void btnMapViewerUpdate_Click(object sender, EventArgs e)
        {
            MapViewerUpdateLevel();
        }

        public void MapViewerUpdateLevel()
        {
            try
            {
                Level l = Level.Find(txtMapViewerLevelName.Text);
                if (l == null)
                {
                    Command.all.Find("load").Use(null, txtMapViewerLevelName.Text);
                    l = Level.Find(txtMapViewerLevelName.Text);
                }
                if (l == null) { MessageBox.Show("Level could not be found.", "Map Viewer"); return; }
                txtMapViewerX.Text = l.width.ToString();
                txtMapViewerY.Text = l.depth.ToString();
                txtMapViewerZ.Text = l.height.ToString();
                int rotation = (int)txtMapViewerRotation.Value;
                if (rotation < 0 || rotation > 3) { MessageBox.Show("Invalid rotation (must be from 0-3).", "Map Viewer"); return; }
                IsoCat IsoCat = new IsoCat(l, IsoCatMode.Normal, rotation);
                Rectangle r = new Rectangle(0, 0, picMapViewer.Width, picMapViewer.Height);
                System.ComponentModel.BackgroundWorker bgw = new System.ComponentModel.BackgroundWorker();
                bgw.WorkerReportsProgress = true;
                MapViewerLastDrawn = IsoCat.Draw(out r, bgw);
                picMapViewer.Image = MapViewerLastDrawn;
            }
            catch (Exception ex) { Server.ErrorLog(ex); }
        }
        public Bitmap MapViewerLastDrawn;

        private void btnMapViewerSave_Click(object sender, EventArgs e)
        {
            try
            {
                MapViewerUpdateLevel();
                if (!Directory.Exists("extra/levelimages")) Directory.CreateDirectory("extra/levelimages");
                int lastExisting = 0;
                string saveLocation = "extra/levelimages/" + txtMapViewerLevelName.Text + " (" + DateTime.Now.ToString("yyyy-MM-dd HH.mm.ss") + ")";
                while (File.Exists(saveLocation)) { saveLocation = "extra/levelimages/" + txtMapViewerLevelName.Text + " (" + DateTime.Now.ToString("yyyy-MM-dd HH.mm.ss") + ") (" + lastExisting + ")"; lastExisting++; }
                saveLocation += ".png";
                if (MapViewerLastDrawn != null) MapViewerLastDrawn.Save(saveLocation, ImageFormat.Png);
                if (MessageBox.Show("Saved to " + saveLocation + "." + Environment.NewLine + "Would you like to display the image now?", "Map Viewer", MessageBoxButtons.YesNo) == DialogResult.Yes)
                    Process.Start(Application.StartupPath + "/" + saveLocation);
            }
            catch (Exception ex) { MessageBox.Show(ex.ToString()); }
        }

        private void chkChatColors_CheckedChanged(object sender, EventArgs e)
        {
            Server.consoleChatColors = chkChatColors.Checked;
            Properties.Save("properties/server.properties");
        }

        private void cmbLogs_SelectedIndexChanged(object sender, EventArgs e)
        {
            string ext = File.Exists("logs/" + cmbLogs.Text + ".txt") ? ".txt" : ".log";
            new Thread(() => {
                Invoke(new Action(() => {
                    foreach (string line in File.ReadAllLines("logs/" + cmbLogs.Text + ext))
                        txtLogs.AppendText(line + Environment.NewLine);
                }));
            }).Start();
            ScrollToBottom(txtLogs);
        }

        private void cmbErrors_SelectedIndexChanged(object sender, EventArgs e)
        {
            string ext = File.Exists("logs/errors/" + cmbLogs.Text + ".txt") ? ".txt" : ".log";
            new Thread(() => {
                Invoke(new Action(() => {
                    foreach (string line in File.ReadAllLines("logs/errors/" + cmbErrors.Text + ext))
                        txtErrors.AppendText(line + Environment.NewLine);
                }));
            }).Start();
            ScrollToBottom(txtErrors);
        }

        private void txtLogs_TextChanged(object sender, EventArgs e)
        {
            ScrollToBottom(txtLogs);
        }

        private void txtErrors_TextChanged(object sender, EventArgs e)
        {
            ScrollToBottom(txtErrors);
        }

        private void Window_Resize(object sender, EventArgs e)
        {
        }
    }
}