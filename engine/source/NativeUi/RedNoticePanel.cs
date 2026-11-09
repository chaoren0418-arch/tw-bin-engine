using System.Text;
namespace P06.NativeUi;

// ui-red-notice (Issue #474, docs/design/native-ui.md §5.3). Every rule is docs/reference/ui-layout.md §2 "紅字與畫面中央系統訊息
// （V2，Issue #457）" (A) 紅字區, TW16 TW1608262200.bin:
//   handler  S_WARNING_CODE sub_94FB00 (VA 0x94FB00 / FO 0x54EF00): id 1006 → 1017; id ≥ string-table count → not processed
//   text     sub_933850: string-c.tbl[id], %0–%9 → arguments (ServerMessageText.Format is the port)
//   slots    byte_1544388, 5 × 132 byte: 1829 → slot 0, 554 → slot 1, 553 → slot 2, every other id → slot 4 (+15000 ms clear,
//            push 3A98h at VA 0x94FCEA / FO 0x54F0EA); slots 0–2 have no timer; world reset sub_F0A580 clears all five
//   draw     sub_F0F0C0 loop at VA 0xF10205: y = 144 + 12k, x = 400 − (bytes·6)/2 (CP950 byte count, escape codes included),
//            sub_841770: text palette[3] #FF0000, outline palette[0] #000000 at ±1 px up/down/left/right, no background,
//            "\f<c>" → palette[c − '0'], "\<" → the line's start colour; one line per message, never wrapped
// The queue is the existing DisplayNotices one (WorldSession.redNotice, overwritten by each S105) read through RedNoticeState.
// Layer World (not Float, §3.1): the original draws this on the main surface and it takes no input; a Float panel would make
// UiInput route clicks on the text to the panel instead of the world. World-layer panels are painted above the world scene and
// below every bar/window, and are never hit-tested.
// Not modelled (推測 in V2): the second 15 s timer of an earlier message clearing a later one early, the strcpy overflow past
// 131 bytes, the re-armed clear of sub_F067B0 / sub_F12610, palette entries above 3 (unread: the colour is left unchanged).

/// <summary>One drawn line: slot k, raw text (escape codes kept, as the original measures it) and its top-left.</summary>
public sealed record RedNoticeLine(int Slot,string Raw,Point Origin);

/// <summary>A run of characters drawn in one RGB555 colour.</summary>
public sealed record RedNoticeRun(string Text,ushort Color);

public sealed class RedNoticePanel:IUiPanel {
    [UiPanel(150)] internal static UiPanelRegistration Registration=>new(RedNoticePanel.PanelId,"uistatus86",_=>new RedNoticePanel());   // ui-red-notice #474 (no XML: V2 hard-coded layout)
    public const string PanelId="RED_NOTICE";
    public const int CenterX=400,Top=144,LineHeight=12,SlotCount=5,NormalSlot=4,ByteWidth=6,NormalMs=15000;
    public const ushort AuthDLost=1829,Slot1Id=554,NpcServerDown=553,RemappedFrom=1006,RemappedTo=1017;
    /// <summary>dword_1369D70[0..3] (FO 0xF68570), the only entries V2 read.</summary>
    public static readonly ushort[] Palette=[0x0000,0x001F,0x03E0,0x7C00];
    public const int TextIndex=3,OutlineIndex=0;
    /// <summary>Clip band: slot 0 top − 1 px outline to slot 4 bottom + outline; the whole canvas width.</summary>
    public static readonly Rectangle Band=new(0,Top-1,UiRoot.LogicalWidth,SlotCount*LineHeight+4);

    static readonly Encoding Cp950=CreateCp950();
    static Encoding CreateCp950(){Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);return Encoding.GetEncoding(950);}

    /// <summary>Slot of a message id (0x94FC40–0x94FCEA).</summary>
    public static int SlotOf(ushort id)=>id switch{AuthDLost=>0,Slot1Id=>1,NpcServerDown=>2,_=>NormalSlot};
    /// <summary>strlen of the CP950 string.</summary>
    public static int ByteCount(string raw)=>Cp950.GetByteCount(raw);
    /// <summary>Top-left of slot k: (400 − bytes·6/2, 144 + 12k).</summary>
    public static Point Origin(int slot,string raw)=>new(CenterX-ByteCount(raw)*ByteWidth/2,Top+LineHeight*slot);

    /// <summary>
    /// Text the original would store (sub_94FB00 + sub_933850). Without the string table the engine's DisplayNotices text is used
    /// (no colour codes; this is the engine fallback, not original text). null = the original ignores the packet (id ≥ count).
    /// </summary>
    public static string? Raw(RedNotice notice,ServerStringTable? table){
        if(table==null)return notice.Text;
        ushort id=notice.Id==RemappedFrom?RemappedTo:notice.Id;
        if(id>=table.Count)return null;
        return ServerMessageText.Format(table[id],notice.Arguments);
    }

    /// <summary>sub_841770 colour runs: "\f&lt;c&gt;" switches to palette[c − '0'], "\&lt;" back to the start colour.</summary>
    public static IReadOnlyList<RedNoticeRun> Runs(string raw,ushort start){
        var runs=new List<RedNoticeRun>();var sb=new StringBuilder();ushort color=start;
        void Flush(){if(sb.Length>0){runs.Add(new(sb.ToString(),color));sb.Clear();}}
        for(int i=0;i<raw.Length;i++){
            char c=raw[i];
            if(c=='\\'&&i+1<raw.Length){
                char k=raw[i+1];
                if(k=='f'&&i+2<raw.Length){
                    Flush();int n=raw[i+2]-'0';
                    if(n>=0&&n<Palette.Length)color=Palette[n];
                    else UiResourceLog.Write($"red-notice: 色碼 \\f{raw[i+2]} 的調色盤值未查證，維持目前顏色");
                    i+=2;continue;
                }
                if(k=='<'){Flush();color=start;i+=1;continue;}
            }
            sb.Append(c);
        }
        Flush();
        return runs;
    }

    readonly string?[] slots=new string?[SlotCount];
    long normalUntil;int? epoch;RedNotice? seen;

    public string Id=>PanelId;
    public UiLayer Layer=>UiLayer.World;
    public Rectangle Bounds=>Visible?Band:Rectangle.Empty;
    public bool Visible=>slots.Any(s=>!string.IsNullOrEmpty(s));
    /// <summary>Raw text per slot (null = empty).</summary>
    public IReadOnlyList<string?> Slots=>slots;
    public IEnumerable<RedNoticeLine> Lines(){
        for(int k=0;k<SlotCount;k++)if(slots[k] is {Length:>0} s)yield return new(k,s,Origin(k,s));
    }

    public void Load(UiLayout layout,SurfCatalog surfs){}   // V2 layout is hard-coded in the BIN; no XML, no surf

    public void Update(IUiGameState state){
        var s=state.InWorld?RedNoticeState.Of(state):null;
        if(s==null){Array.Clear(slots);return;}
        if(epoch!=s.WorldEpoch){Array.Clear(slots);epoch=s.WorldEpoch;}   // sub_F0A580 on world/map reset
        if(s.Latest is {} n&&!ReferenceEquals(n,seen)){seen=n;Accept(n,s);}
        if(slots[NormalSlot]!=null&&state.Now>=normalUntil)slots[NormalSlot]=null;   // sub_F044A0 after 15000 ms
    }
    void Accept(RedNotice n,RedNoticeState s){
        string? raw=Raw(n,s.Table);
        if(raw==null)return;
        if(s.Resolve!=null)raw=s.Resolve(raw);   // "$nnn" names (the formatter's name lookup at 0x5417C0, engine ObjectAssets)
        int k=SlotOf(n.Id);slots[k]=raw;
        if(k==NormalSlot)normalUntil=n.At+NormalMs;
    }

    public void Paint(UiCanvas canvas){
        var g=canvas.Graphics;var outline=NameColors.ToColor(Palette[OutlineIndex]);
        foreach(var line in Lines()){
            var runs=Runs(line.Raw,Palette[TextIndex]);
            var placed=new List<(RedNoticeRun Run,int X)>();int x=line.Origin.X;
            foreach(var r in runs){placed.Add((r,x));x+=UiText.Measure(g,r.Text).Width;}
            int y=line.Origin.Y;
            foreach(var (r,rx) in placed){
                UiText.Draw(g,r.Text,rx,y-1,outline);UiText.Draw(g,r.Text,rx,y+1,outline);
                UiText.Draw(g,r.Text,rx-1,y,outline);UiText.Draw(g,r.Text,rx+1,y,outline);
            }
            foreach(var (r,rx) in placed)UiText.Draw(g,r.Text,rx,y,NameColors.ToColor(r.Color));
        }
    }

    public bool Mouse(UiMouseEvent e,IUiCommands commands)=>false;
    public bool Key(UiKeyEvent e,IUiCommands commands)=>false;
}
