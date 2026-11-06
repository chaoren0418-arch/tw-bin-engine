namespace P06;

public static class MailUi {
    public static void Attach(PlayerHudView hud, FlowLayoutPanel menu) {
        var button = new Button { Text = "信件", AccessibleName = "信件", Width = 58, Height = 30,
            Margin = new(3, 2, 3, 2), FlatStyle = FlatStyle.Flat, BackColor = PlayerHudPainter.Panel, ForeColor = Color.White };
        button.Click += (_, _) => { if (hud.FindForm() is ClientForm form) form.OpenMail(); };
        menu.Controls.Add(button);
    }
}

public sealed partial class ClientForm {
    internal void OpenMail() {
        if (login.State != LoginState.InWorld) return;
        LeaveGameFocus("mail"); ClearInput("mail");
        using var dialog = new MailDialog(login, ResolveNpcText);
        dialog.ShowDialog(this);
    }
}

// Modal native editor owns keyboard focus; the game cannot interpret body text as movement.
public sealed class MailDialog : Form {
    readonly LoginConnection connection;
    readonly Func<string, string> displayMessage;
    readonly MailStamp stamp;
    readonly System.Windows.Forms.Timer refresh = new() { Interval = 200 };
    readonly ComboBox box = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 110, AccessibleName = "信匣" };
    readonly ListView letters = new() { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, MultiSelect = false, HideSelection = false, AccessibleName = "信件清單" };
    readonly TextBox reading = new() { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, AccessibleName = "讀信本文" };
    readonly Label heading = new() { Dock = DockStyle.Top, Height = 48, AutoEllipsis = true };
    readonly Label notice = new() { Dock = DockStyle.Bottom, Height = 70, AutoEllipsis = true, Padding = new(6), AccessibleName = "信件狀態" };
    readonly CheckBox clan = new() { Text = "血盟信（郵資 1000 金幣）", AutoSize = true };
    readonly TextBox recipient = new() { Dock = DockStyle.Fill, AccessibleName = "收件人或血盟名稱", MaxLength = MailPackets.MaxPacketBytes };
    readonly TextBox subject = new() { Dock = DockStyle.Fill, AccessibleName = "信件標題", MaxLength = MailPackets.MaxPacketBytes / 2 };
    readonly TextBox body = new() { Dock = DockStyle.Fill, Multiline = true, AcceptsReturn = true, ScrollBars = ScrollBars.Vertical, AccessibleName = "信件本文", MaxLength = MailPackets.MaxPacketBytes / 2 };
    readonly Label budget = new() { AutoSize = true };
    readonly Button send = new() { Text = "寄出一次", AutoSize = true };
    readonly Button read = new() { Text = "讀信", AutoSize = true }, delete = new() { Text = "刪除", AutoSize = true }, store = new() { Text = "存入保管箱", AutoSize = true };
    readonly Button reload = new() { Text = "重新整理", AutoSize = true };
    int revision = -1;
    bool sending, submitted, refreshing, closing;
    MailView view;
    uint? SelectedId => letters.SelectedItems.Count == 1 ? (uint)letters.SelectedItems[0].Tag! : null;

    public MailDialog(LoginConnection connection, Func<string, string>? displayMessage = null) {
        this.connection = connection; this.displayMessage = displayMessage ?? (text => text);
        view = connection.CaptureWorld(() => connection.World.MailSnapshot()); stamp = view.Stamp;
        Text = "信件"; AccessibleName = "信件"; StartPosition = FormStartPosition.CenterParent;
        Size = new(780, 640); MinimumSize = new(640, 540); ShowInTaskbar = false;
        var tabs = new TabControl { Dock = DockStyle.Fill };
        var inbox = new TabPage("收件匣"); var compose = new TabPage("寫信"); tabs.TabPages.AddRange([inbox, compose]);
        box.Items.AddRange(["一般", "血盟", "保管箱"]); box.SelectedIndex = 0;
        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 38, AutoScroll = true };
        toolbar.Controls.AddRange([box, reload, read, store, delete]);
        letters.Columns.Add("狀態", 55); letters.Columns.Add("寄件人", 140); letters.Columns.Add("標題", 350); letters.Columns.Add("日期", 90);
        var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, Size = new(740, 440), SplitterDistance = 210 };
        split.Panel1.Controls.Add(letters); split.Panel2.Controls.Add(reading); split.Panel2.Controls.Add(heading);
        inbox.Controls.Add(split); inbox.Controls.Add(toolbar);
        var editor = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 5, Padding = new(8) };
        editor.ColumnStyles.Add(new(SizeType.Absolute, 110)); editor.ColumnStyles.Add(new(SizeType.Percent, 100));
        foreach (int height in new[] { 32, 32, 32 }) editor.RowStyles.Add(new(SizeType.Absolute, height));
        editor.RowStyles.Add(new(SizeType.Percent, 100)); editor.RowStyles.Add(new(SizeType.Absolute, 70));
        editor.Controls.Add(clan, 1, 0);
        editor.Controls.Add(new Label { Text = "收件人／血盟", AutoSize = true }, 0, 1); editor.Controls.Add(recipient, 1, 1);
        editor.Controls.Add(new Label { Text = "標題", AutoSize = true }, 0, 2); editor.Controls.Add(subject, 1, 2);
        editor.Controls.Add(new Label { Text = "本文", AutoSize = true }, 0, 3); editor.Controls.Add(body, 1, 3);
        var draftActions = new FlowLayoutPanel { Dock = DockStyle.Fill };
        var newDraft = new Button { Text = "清空並寫新信", AutoSize = true };
        draftActions.Controls.AddRange([send, newDraft, budget]); editor.Controls.Add(draftActions, 1, 4);
        compose.Controls.Add(editor); Controls.Add(tabs); Controls.Add(notice);
        reload.Click += async (_, _) => await Run("list");
        box.SelectedIndexChanged += async (_, _) => await Run("list");
        read.Click += async (_, _) => await Run("read");
        letters.DoubleClick += async (_, _) => await Run("read");
        letters.SelectedIndexChanged += (_, _) => { if (!refreshing) ShowSelectedBody(); UpdateButtons(); };
        store.Click += async (_, _) => await Run("store");
        delete.Click += async (_, _) => {
            if (SelectedId is uint id && MessageBox.Show(this, "確定刪除選取的信件？", "刪除信件", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes && SelectedId == id)
                await Run("delete");
        };
        send.Click += async (_, _) => await Run(clan.Checked ? "clan" : "write");
        newDraft.Click += (_, _) => { if (sending) return; subject.Clear(); body.Clear(); submitted = false; UpdateButtons(); };
        foreach (var field in new[] { recipient, subject, body }) field.TextChanged += (_, _) => UpdateButtons();
        clan.CheckedChanged += (_, _) => UpdateButtons();
        refresh.Tick += (_, _) => Sync();
        FormClosing += (_, _) => { closing = true; refresh.Stop(); };
        Shown += async (_, _) => { refresh.Start(); Sync(); await Run("list"); };
        Sync();
    }
    void ShowSelectedBody() {
        var selected = view.Headers.FirstOrDefault(x => x.Id == SelectedId);
        heading.Text = selected == null ? "請選擇信件並按讀信。" : $"寄件人：{selected.Sender}\n標題：{selected.Subject}";
        reading.Text = selected != null && view.ReadingId == selected.Id ? view.Body : "";
    }
    void Sync() {
        if (IsDisposed) return;
        view = connection.CaptureWorld(() => connection.World.MailSnapshot());
        if (connection.State != LoginState.InWorld || view.Stamp != stamp) { Close(); return; }
        if (revision != view.Revision) {
            revision = view.Revision; uint? selected = SelectedId; refreshing = true;
            letters.BeginUpdate(); letters.Items.Clear();
            foreach (var mail in view.Headers) {
                var item = new ListViewItem([mail.ReadStatus == 0 ? "未讀" : "已讀", mail.Sender, mail.Subject,
                    mail.Month == 0 ? "未提供" : $"{mail.Year:D2}/{mail.Month:D2}/{mail.Day:D2}"]) { Tag = mail.Id };
                letters.Items.Add(item); item.Selected = mail.Id == selected;
            }
            letters.EndUpdate(); refreshing = false; ShowSelectedBody();
        }
        string? server = connection.CaptureWorld(() => connection.World.LastServerMessage);
        notice.Text = view.Notice + (server == null ? "" : "\n最近伺服器訊息：" + displayMessage(server));
        UpdateButtons();
    }
    void UpdateButtons() {
        bool available = !sending && connection.State == LoginState.InWorld && !connection.World.ActionLocked;
        read.Enabled = delete.Enabled = store.Enabled = available && SelectedId != null;
        reload.Enabled = box.Enabled = available;
        string? error = null;
        try { _ = MailPackets.Write(clan.Checked, recipient.Text, subject.Text, body.Text); }
        catch (ArgumentException ex) { error = ex.Message; }
        budget.Text = submitted ? "此草稿已送出；請核對收件結果，另寄請寫新信。" : error ??
            $"一般郵資 50 金幣；本文剩餘 {Math.Max(0, MailPackets.RemainingBodyUnits(recipient.Text, subject.Text) - body.Text.Length)} 個 UTF-16 單位。";
        send.Enabled = available && !submitted && error == null;
        recipient.ReadOnly = subject.ReadOnly = body.ReadOnly = sending || submitted;
        clan.Enabled = !sending && !submitted;
    }
    async Task Run(string command) {
        if (sending || command is "write" or "clan" && submitted) return;
        uint id = SelectedId ?? 0;
        if (command is "read" or "delete" or "store" && id == 0) return;
        sending = true; UpdateButtons();
        try {
            bool sent = await connection.Mail(command, (byte)box.SelectedIndex, id, stamp, recipient.Text, subject.Text, body.Text,
                () => !closing && !IsDisposed && !Disposing);
            if (sent && command is "write" or "clan") submitted = true;
        } finally { sending = false; if (!IsDisposed) Sync(); }
    }
    protected override void Dispose(bool disposing) { if (disposing) refresh.Dispose(); base.Dispose(disposing); }
}
