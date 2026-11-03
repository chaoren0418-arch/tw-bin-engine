namespace P06;
public static class ProductEntry {
    public const string Profile="p24-real-jar-d287-client2";
    public const int Port=42061;
    public const string ServerName="本機遊戲伺服器";
    public static string StateName(LoginState state)=>state switch{LoginState.Offline=>"離線預覽",LoginState.Connecting=>"連線中",LoginState.AwaitSeed=>"等待連線交握",LoginState.Authenticating=>"驗證帳密中",LoginState.AwaitList or LoginState.ReadingCharacters=>"接收角色清單",LoginState.CharacterList=>"請選擇角色",LoginState.EnteringWorld=>"進入世界中",LoginState.InWorld=>"已進入世界",LoginState.Rejected=>"登入遭拒絕",LoginState.Disconnected=>"連線已中斷",LoginState.Cancelled=>"已取消／斷線",_=>"連線發生錯誤"};
    public static bool Normal(string[] args){if(args.Contains("--normal-product")){if(args.Length!=1)throw new ArgumentException("正常入口不接受診斷或自動登入參數。");return true;}return args.Length==0;}
}
public sealed partial class ClientForm {
    readonly bool normalProduct;
    TabControl? normalPanels;
    int NormalSceneLeft=>normalProduct&&normalPanels?.Visible==true?normalPanels.Right+12:0;
    void InitNormalPanels(){
        if(!normalProduct)return;
        normalPanels=new TabControl{Visible=false,Location=new(12,148),Size=new(470,440),Multiline=true,AccessibleName="玩家服務視窗"};
        foreach(var (title,body,button) in new (string,Control,Control?)[]{("背包",inventoryPanel,null),("地面",groundPanel,null),("魔法",magicPanel,null),("角色",rewardsLabel,null),("能力點",bonusStatsPanel,null),("個人商店",privateShopPanel,null),("NPC",npcPanel,npcTalk),("交易",tradePanel,tradeInvite)}){
            var page=new TabPage(title){BackColor=Color.FromArgb(23,38,41),AutoScroll=true};body.Parent=null;body.Dock=DockStyle.Fill;page.Controls.Add(body);
            if(button!=null){button.Parent=null;button.Dock=DockStyle.Top;page.Controls.Add(button);button.BringToFront();}normalPanels.TabPages.Add(page);
        }
        normalPanels.Enter+=(_,_)=>LeaveGameFocus("normal-panels");Controls.Add(normalPanels);normalPanels.BringToFront();InitPlayerHud();
    }
    void SyncNormalPanels(){
        if(normalPanels==null)return;
        normalPanels.Visible=playerLegacyOpen&&login.State==LoginState.InWorld;
        if(normalPanels.Visible)normalPanels.Bounds=new(12,8,Math.Min(470,Math.Max(260,ClientSize.Width-(webHost.Visible?webHost.Width:0)-24)),Math.Max(100,ClientSize.Height-PlayerHudGeometry.BaseHeight-16));
    }
}
