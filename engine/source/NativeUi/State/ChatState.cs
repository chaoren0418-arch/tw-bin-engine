namespace P06.NativeUi;

// ui-chat-window (Issue #468, docs/design/native-ui.md §5.3): pure state/logic for CENTER_WINDOW plus the read-only adapter
// from the existing chat model. No packet parsing: lines come from WorldSession.PlayerChatLines via LoginConnection
// .PlayerChatSnapshot() (PlayerChatView), sending goes through the existing LoginConnection.SendPlayerChat.
// Verified (docs/reference/ui-layout.md §3): window (129,470), stepSize 36, base 3279 bottom-anchored at y=600, max height =
// surf height (240), step button (345,115), CHAT_BAR 2273 (10,114), 8 channel tabs at x=8 / y=10–115, filter icons y=5,
// LCHAT 3288 (260 wide) / RCHAT 3290 at x=260. Everything else in this file is marked 推測 and kept in one place.

/// <summary>Channel tabs (CHANNEL_ICON), bottom to top as in the XML: GENERAL y=115 … UPPER y=10.</summary>
public enum ChatTab { General, Blood, Party, Whole, Trade, Channel, Alliance, Upper }
/// <summary>Filter icons (CHAT_ICON, y=5): 一般 179、隊伍 198、血盟 221、交易 240、系統 258.</summary>
public enum ChatFilter { General, Party, Pledge, Trade, System }

/// <summary>Height steps. Min = 600 − window y (130); Max = base surf height (240); Step = stepSize (36).</summary>
public sealed record ChatHeights(int Min,int Max,int Step) {
    /// <summary>
    /// Min, Min+Step, … then Max (130, 166, 202, 238, 240). Whether the BIN stops at 238 or clamps to 240 is 待驗證; this is
    /// the only place to change. Max ≤ Min (base surf missing) → a single step.
    /// </summary>
    public IReadOnlyList<int> Steps{get{
        var list=new List<int>{Min};
        if(Step<=0||Max<=Min)return list;
        for(int h=Min+Step;h<Max;h+=Step)list.Add(h);
        list.Add(Max);return list;
    }}
    public int Clamp(int h)=>Steps.Where(s=>s<=h).DefaultIfEmpty(Min).Max();
    public int Grow(int h)=>Steps.FirstOrDefault(s=>s>h,Steps[^1]);
    public int Shrink(int h)=>Steps.LastOrDefault(s=>s<h,Min);
    /// <summary>The single step button (CHAT_UP and CHAT_DOWN share (345,115)): grow until Max, then back to Min. 推測.</summary>
    public int Cycle(int h)=>h>=Steps[^1]?Min:Grow(h);
}

public static class ChatRules {
    /// <summary>Same cap as the model (WorldSession keeps the last 100 lines); the panel never holds more.</summary>
    public const int MaxLines=100;
    // Text area (推測, not in the XML): relative to the window top for the top edge, to the fixed base (y=470) for the bottom.
    public const int TextLeft=30,TextRight=344,TextTop=20,TextBottom=112,LineHeight=12;
    /// <summary>Split view: LCHAT is 260 wide (verified); the right pane text starts after a 4 px margin (推測).</summary>
    public const int SplitX=260,PaneMargin=4;
    public const int WheelRows=3;
    public const int CaretBlinkMs=500;

    /// <summary>XML element base names of the channel tabs (OFF state); "_ON" / "_FOCUS" are the other states.</summary>
    public static string TabElement(ChatTab t)=>t switch{
        ChatTab.General=>"GENERAL_CHANNEL",ChatTab.Blood=>"BLOOD_CHANNEL",ChatTab.Party=>"PARTY_CHANNEL",ChatTab.Whole=>"WHOLE_CHANNEL",
        ChatTab.Trade=>"TRADE_CHANNEL",ChatTab.Channel=>"CHANNEL_CHANNEL",ChatTab.Alliance=>"ALLIANCE_CHANNEL",_=>"UPPER_CHANNEL"};
    public static string FilterElement(ChatFilter f)=>f switch{
        ChatFilter.General=>"GENERAL_CHAT",ChatFilter.Party=>"PARTY_CHAT",ChatFilter.Pledge=>"PLEDGE_CHAT",ChatFilter.Trade=>"TRADE_CHAT",_=>"SYSTEM_CHAT"};

    /// <summary>
    /// Which lines a tab shows (推測: the BIN's tab semantics are 待驗證). General = everything; 頻道 has no matching model
    /// channel and shows nothing rather than a guess.
    /// </summary>
    public static bool TabShows(ChatTab tab,PlayerChatChannel c)=>tab switch{
        ChatTab.General=>true,
        ChatTab.Blood=>c is PlayerChatChannel.Clan or PlayerChatChannel.ClanNotice,
        ChatTab.Party=>c is PlayerChatChannel.Party or PlayerChatChannel.ChatParty,
        ChatTab.Whole=>c==PlayerChatChannel.Global,
        ChatTab.Trade=>c==PlayerChatChannel.Trade,
        ChatTab.Alliance=>c==PlayerChatChannel.Alliance,
        ChatTab.Upper=>c==PlayerChatChannel.Prince,
        _=>false};
    /// <summary>Filter group of a channel (推測). Whispers have no filter. System lines are not part of PlayerChat yet.</summary>
    public static ChatFilter? FilterOf(PlayerChatChannel c)=>c switch{
        PlayerChatChannel.Normal or PlayerChatChannel.Shout or PlayerChatChannel.Global=>ChatFilter.General,
        PlayerChatChannel.Party or PlayerChatChannel.ChatParty=>ChatFilter.Party,
        PlayerChatChannel.Clan or PlayerChatChannel.ClanNotice or PlayerChatChannel.Prince or PlayerChatChannel.Alliance=>ChatFilter.Pledge,
        PlayerChatChannel.Trade=>ChatFilter.Trade,
        _=>null};
    /// <summary>Split view: the right pane holds whispers (推測; the BIN's split rule is 待驗證).</summary>
    public static bool RightPane(PlayerChatChannel c)=>c is PlayerChatChannel.Whisper or PlayerChatChannel.WhisperTo;

    /// <summary>Greedy character wrap to maxWidth. Always at least one character per row; empty text → one empty row.</summary>
    public static List<string> Wrap(string text,int maxWidth,Func<char,int> width){
        var rows=new List<string>();
        if(text.Length==0){rows.Add("");return rows;}
        int start=0,w=0;
        for(int i=0;i<text.Length;i++){
            int cw=width(text[i]);
            if(i>start&&w+cw>maxWidth){rows.Add(text[start..i]);start=i;w=0;}
            w+=cw;
        }
        rows.Add(text[start..]);return rows;
    }
}

/// <summary>One wrapped display row. Runs = the row's coloured pieces (R2-G, palette indices); their texts join to Text.</summary>
public sealed record ChatRow(long Sequence,PlayerChatChannel Channel,string Text) {
    public IReadOnlyList<ChatRun> Runs{get;init;}=[new(Text,ChatFormat.ChannelIndex(Channel))];
    /// <summary>
    /// 第二色（sub_7DC850 第 3 個參數，ChatFormat.SecondColor；R3-G #675）。Q3-9（T287／#680）：原版傳下去後沒有讀取，畫面上沒有用途；只當資料帶著，不畫。
    /// </summary>
    public int SecondColor{get;init;}=ChatFormat.White;
}

/// <summary>Display state: tab, filters, split, height, scroll. Switching tabs/filters only changes what is shown.</summary>
public sealed class ChatViewState {
    readonly HashSet<ChatFilter> off=[];
    public ChatHeights Heights{get;private set;}
    public int Height{get;private set;}
    public ChatTab Tab{get;set;}=ChatTab.General;
    public bool Split{get;set;}
    /// <summary>Rows scrolled up from the newest row (0 = following new lines).</summary>
    public int Scroll{get;private set;}
    public ChatViewState(ChatHeights heights){Heights=heights;Height=heights.Min;}
    public void SetHeights(ChatHeights heights){Heights=heights;Height=heights.Clamp(Height);}
    public void SetHeight(int h)=>Height=Heights.Clamp(h);
    public void Cycle()=>Height=Heights.Cycle(Height);
    public void Grow()=>Height=Heights.Grow(Height);
    public void Shrink()=>Height=Heights.Shrink(Height);
    public bool Enabled(ChatFilter f)=>!off.Contains(f);
    public void Toggle(ChatFilter f){if(!off.Remove(f))off.Add(f);}

    public bool Shows(PlayerChatLine line,bool rightPane=false){
        if(ChatRules.FilterOf(line.Channel) is ChatFilter f&&!Enabled(f))return false;
        if(Split)return rightPane==ChatRules.RightPane(line.Channel)&&(rightPane||ChatRules.TabShows(Tab,line.Channel));
        return !rightPane&&ChatRules.TabShows(Tab,line.Channel);
    }
    /// <summary>Last MaxLines lines that pass tab/filter, wrapped to width (display text and colours from ChatFormat, R2-G).</summary>
    public List<ChatRow> Rows(IReadOnlyList<PlayerChatLine> lines,int width,Func<char,int> charWidth,bool rightPane=false){
        var rows=new List<ChatRow>();
        foreach(var line in lines.Skip(Math.Max(0,lines.Count-ChatRules.MaxLines)))
            if(Shows(line,rightPane)){
                var runs=ChatFormat.Runs(line);
                int start=0,second=ChatFormat.SecondColor(line);
                foreach(var r in ChatRules.Wrap(string.Concat(runs.Select(x=>x.Text)),width,charWidth)){
                    rows.Add(new(line.Sequence,line.Channel,r){Runs=Slice(runs,start,r.Length),SecondColor=second});start+=r.Length;
                }
            }
        return rows;
    }
    /// <summary>The runs covering [start, start+length) of the joined text; an empty row keeps the colour at that point.</summary>
    public static List<ChatRun> Slice(IReadOnlyList<ChatRun> runs,int start,int length){
        var result=new List<ChatRun>();int at=0;
        foreach(var run in runs){
            int from=Math.Max(start,at),to=Math.Min(start+length,at+run.Text.Length);
            if(to>from)result.Add(new(run.Text[(from-at)..(to-at)],run.Color));
            at+=run.Text.Length;
        }
        if(result.Count==0)result.Add(new("",runs.Count>0?runs[^1].Color:ChatFormat.White));
        return result;
    }
    public static int MaxScroll(int totalRows,int visibleRows)=>Math.Max(0,totalRows-Math.Max(0,visibleRows));
    public void ScrollBy(int rows,int totalRows,int visibleRows)=>Scroll=Math.Clamp(Scroll+rows,0,MaxScroll(totalRows,visibleRows));
    /// <summary>Keeps the viewed rows still when new rows arrive while scrolled up; at 0 the view follows new lines.</summary>
    public void Arrived(int newRows,int totalRows,int visibleRows){if(Scroll>0)Scroll=Math.Clamp(Scroll+newRows,0,MaxScroll(totalRows,visibleRows));else ClampScroll(totalRows,visibleRows);}
    public void ClampScroll(int totalRows,int visibleRows)=>Scroll=Math.Clamp(Scroll,0,MaxScroll(totalRows,visibleRows));
    public void ResetScroll()=>Scroll=0;
}

/// <summary>
/// The CHAT_BAR input line. Same flow as PlayerChatKeyboardFlow / PlayerHudChatPanel: Enter opens (the opening Enter stays
/// held until released), a later fresh Enter submits only valid text (PlayerChatProtocol parse + encode), Esc closes,
/// the text is cleared only after a successful send, and at most MaxInputCharacters characters are accepted.
/// </summary>
public sealed class ChatInputLine {
    readonly PlayerChatEnterPress enter=new();
    readonly System.Text.StringBuilder text=new();
    public bool Open{get;private set;}
    public bool Pending{get;set;}
    public string Text=>text.ToString();
    public void Begin(bool byEnter){Open=true;enter.Open(byEnter);}
    public void Close(){Open=false;enter.Release();}
    public void Clear()=>text.Clear();
    /// <summary>Enter key down while open: true = this press may submit.</summary>
    public bool EnterDown(Keys modifiers)=>enter.Down(modifiers);
    public void EnterUp()=>enter.Release();
    public bool Type(char c){
        if(!Open||char.IsControl(c)||text.Length>=PlayerChatProtocol.MaxInputCharacters)return false;
        text.Append(c);return true;
    }
    public bool Backspace(){if(!Open||text.Length==0)return false;text.Length--;return true;}
    /// <summary>Chat text, or a local command (/on, /off, C199 chat-party, /loc) with valid arguments (R2-G).</summary>
    public bool Valid=>PlayerChatProtocol.Valid(text.ToString());
    /// <summary>R10-F：有提示等待答覆時，任何一列（含空白＝140 的「全部」、Y/N）都可以送出當作答覆。</summary>
    public bool ValidFor(ChatLinePrompt? prompt)=>prompt!=null||Valid;
}

/// <summary>
/// R10-F（T390／#905，gap-matrix 15.12.6；1:29、4:ExchangeUI）：聊天輸入列上等待答覆的提示（原版 sub_AC98E0：提示字串放進輸入列，
/// 答一次回呼一次）。Key 用來分辨是不是同一個提示（換了提示就清掉輸入列的草稿）；Text＝提示字串。
/// Answer 不是 null＝本機答覆（交易 140：答覆直接交給交易窗，不經聊天送出）；null＝答覆照常走 SendChat，由
/// LoginConnection.SendPlayerChat 的確認分派接手（刪除確認、私商 1028）。Cancel＝Esc（不送）。
/// </summary>
public sealed record ChatLinePrompt(object Key,string Text,Func<string,bool>? Answer=null,Action? Cancel=null) {
    public bool Local=>Answer!=null;
}

/// <summary>Chat commands (implemented by the client bridge; every member forwards to the existing chat flow).</summary>
public interface IUiChatCommands:IUiCommands {
    /// <summary>OpenPlayerChat rules (PlayerChatFlowRules.CanOpen) + LeaveGameFocus. false = may not open.</summary>
    bool BeginChatInput();
    /// <summary>ExitPlayerChat: ReturnGameFocus.</summary>
    void EndChatInput();
    /// <summary>Existing LoginConnection.SendPlayerChat with the displayed stamp; false if the stamp is stale or not sent.</summary>
    Task<bool> SendChat(string text,PlayerChatView displayed);
}

/// <summary>A panel that accepts typed characters (WM_CHAR after IME composition), routed by the client bridge.</summary>
public interface IUiTextInput { bool Char(char c); }

/// <summary>Per-frame chat snapshot. View null = not in world / unknown. GameFocus true = the game owns the keyboard.</summary>
public sealed record UiChatSnapshot(PlayerChatView? View,bool GameFocus) {
    public static readonly UiChatSnapshot Empty=new(null,true);
}
public interface IUiChatSource { UiChatSnapshot Chat { get; } }

/// <summary>Decorator: the shared IUiGameState plus the chat snapshot (built by the client bridge once per frame).</summary>
public sealed class UiChatGameState(IUiGameState inner,UiChatSnapshot chat):IUiGameState,IUiChatSource {
    public IUiGameState Inner=>inner;
    public UiChatSnapshot Chat=>chat;
    public bool InWorld=>inner.InWorld;
    public string? CharacterName=>inner.CharacterName;
    public int? Level=>inner.Level;
    public long? Hp=>inner.Hp;
    public long? MaxHp=>inner.MaxHp;
    public long? Mp=>inner.Mp;
    public long? MaxMp=>inner.MaxMp;
    public long Now=>inner.Now;
    /// <summary>Bridge order: UiHpMpGameState (#465) → UiMacroGameState (#466) → UiLeftStatusGameState (#464) → UiChatGameState; looks through all three.</summary>
    public static UiChatSnapshot From(IUiGameState? state){
        for(int depth=0;state!=null&&depth<8;depth++){
            if(state is IUiChatSource s)return s.Chat;
            state=state switch{UiHpMpGameState h=>h.Base,UiMacroGameState m=>m.Inner,UiLeftStatusGameState l=>l.Inner,_=>null};
        }
        return UiChatSnapshot.Empty;
    }
    /// <summary>The bridge commands are wrapped in UiMacroCommands (#466); find the chat commands underneath.</summary>
    public static IUiChatCommands? Commands(IUiCommands? commands){
        for(int depth=0;commands!=null&&depth<8;depth++){
            if(commands is IUiChatCommands c)return c;
            commands=commands is UiMacroCommands m?m.Inner:null;
        }
        return null;
    }
}
