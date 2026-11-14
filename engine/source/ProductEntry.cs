namespace P06;
public static class ProductEntry {
    public const string Profile="p24-real-jar-d287-client2";
    public const int Port=42061;
    public const string ServerName="本機遊戲伺服器";
    public static string StateName(LoginState state)=>state switch{LoginState.Offline=>"離線預覽",LoginState.Connecting=>"連線中",LoginState.AwaitSeed=>"等待連線交握",LoginState.Authenticating=>"驗證帳密中",LoginState.AwaitList or LoginState.ReadingCharacters=>"接收角色清單",LoginState.CharacterList=>"請選擇角色",LoginState.EnteringWorld=>"進入世界中",LoginState.InWorld=>"已進入世界",LoginState.Rejected=>"登入遭拒絕",LoginState.Disconnected=>"連線已中斷",LoginState.Cancelled=>"已取消／斷線",_=>"連線發生錯誤"};
    public const string NormalSwitch="--normal-product";
    public const string EvidenceSwitch="--evidence";
    public const string RejectMessage="正常入口不接受診斷或自動登入參數。";
    /// <summary>
    /// T411 (#948): besides itself, <c>--normal-product</c> accepts exactly one <c>--evidence &lt;dir&gt;</c> pair (either order),
    /// so the real-machine smoke gets native-ui.log / window-events.json / snapshots from the normal product in one run.
    /// Everything else is still rejected with <see cref="RejectMessage"/>. The directory must lie inside the project folder
    /// (CLAUDE.md rule 1), see <see cref="CheckEvidenceDirectory"/>.
    /// </summary>
    public static bool Normal(string[] args)=>Normal(args,DefaultProjectRoot());
    public static bool Normal(string[] args,string? projectRoot){
        if(!args.Contains(NormalSwitch))return args.Length==0;
        if(args.Length==1)return true;
        int at=Array.IndexOf(args,EvidenceSwitch);
        if(args.Length!=3||args.Count(a=>a==NormalSwitch)!=1||at<0||at+1>=args.Length)throw new ArgumentException(RejectMessage);
        string value=args[at+1];
        if(string.IsNullOrWhiteSpace(value)||value.StartsWith("--",StringComparison.Ordinal))throw new ArgumentException(RejectMessage);
        CheckEvidenceDirectory(value,projectRoot);
        return true;
    }
    /// <summary>Project folder for a product built at &lt;project&gt;/engine/build/client-product-dev/ (same layout Program.Main uses for ../../assets).</summary>
    public static string? DefaultProjectRoot(){
        try{return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","..",".."));}catch(Exception){return null;}
    }
    /// <summary>
    /// Throws unless <paramref name="directory"/> (relative paths resolve against the current directory, exactly like the
    /// writers do) is strictly inside <paramref name="projectRoot"/>, not under <c>releases\</c> (immutable archives) and
    /// not reached through a junction or symbolic link (e.g. engine\assets → releases\r29\assets). Returns the full path.
    /// </summary>
    public static string CheckEvidenceDirectory(string directory,string? projectRoot){
        string Hint(string why)=>"證據目錄"+why+"：請改用專案資料夾內的目錄，例如 "+(projectRoot==null?"scratch\\smoke-m7\\<日期>":Path.Combine(projectRoot,"scratch","smoke-m7","<日期>"))+"。";
        if(string.IsNullOrWhiteSpace(projectRoot))throw new ArgumentException(Hint("無法核對，找不到專案資料夾"));
        string root=Path.TrimEndingDirectorySeparator(Path.GetFullPath(projectRoot));
        string full;
        try{full=Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));}catch(Exception){throw new ArgumentException(Hint("不是有效路徑（"+directory+"）"));}
        if(!full.StartsWith(root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new ArgumentException(Hint("不在專案資料夾（"+root+"）內（"+full+"）"));
        string relative=full[(root.Length+1)..];
        string first=relative.Split(Path.DirectorySeparatorChar)[0];
        if(first.Equals("releases",StringComparison.OrdinalIgnoreCase))throw new ArgumentException(Hint("不可放在封存的 releases 下（"+full+"）"));
        for(string? p=full;p!=null&&p.Length>root.Length;p=Path.GetDirectoryName(p)){
            if(Directory.Exists(p)&&new DirectoryInfo(p).Attributes.HasFlag(FileAttributes.ReparsePoint))throw new ArgumentException(Hint("不可經過 junction 或符號連結（"+p+"）"));
        }
        return full;
    }
    /// <summary>
    /// T317 (#751, user decision #743-1): the normal product starts with the native UI unless --classic-ui (or the settings
    /// file) says otherwise. Diagnostic harness entries keep the legacy HUD by default (--native-ui still turns it on).
    /// </summary>
    public static bool NativeUiDefault(bool normalProduct)=>normalProduct;
}
public sealed partial class ClientForm {
    readonly bool normalProduct;
    TabControl? normalPanels;
    void InitNormalPanels(){
        normalPanels=new TabControl{Dock=DockStyle.Fill,Multiline=true,AccessibleName="開發工具服務分頁"};
        // #1172：這裡與各功能檔（BookmarkUi、BuddyUi、ClanUi、ClanWar、ClientEffect6016、DuelUi、Marriage、ObjectRendering、PetUi、
        // RingWindows、TitleAndEmote）加進來的一般 TabPage，對跨執行緒 WM_GETTEXT 回空字串（同 #1163 DevToolsPage），免得 UIA 代理接手錯位。
        CrossThreadTabText.Apply(normalPanels);
        foreach(var (title,body,button) in new (string,Control,Control?)[]{("背包",inventoryPanel,null),("地面",groundPanel,null),("魔法",magicPanel,null),("角色",rewardsLabel,null),("能力點",bonusStatsPanel,null),("個人商店",privateShopPanel,null),("NPC",npcPanel,npcTalk),("交易",tradePanel,tradeInvite)}){
            var page=new TabPage(title){BackColor=Color.FromArgb(23,38,41),AutoScroll=true};body.Parent=null;body.Dock=DockStyle.Fill;page.Controls.Add(body);
            if(button!=null){button.Parent=null;button.Dock=DockStyle.Top;page.Controls.Add(button);button.BringToFront();}normalPanels.TabPages.Add(page);
        }
        normalPanels.Enter+=(_,_)=>LeaveGameFocus("normal-panels");InitPlayerHud();
    }
    void SyncNormalPanels(){
        if(normalPanels==null)return;
        normalPanels.Enabled=login.State==LoginState.InWorld;
        if(playerLegacyOpen&&normalPanels.Enabled){playerLegacyOpen=false;ShowDevTools(normalPanels);}
    }
}
