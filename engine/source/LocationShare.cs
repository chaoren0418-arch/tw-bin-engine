using System.Buffers.Binary;
using System.Xml;
using System.Xml.Linq;
namespace P06;

// Verified source identities and the recipient-only contract: docs/protocol/location-share.md.
// M7-O (#521) follows docs/reference/world-objects.md §5:
// - Zone is the MapSystem region id (inferred there: World_Map 0–99 / Village_Map 100–999, receiver picks the table by id>=100).
// - Receive shows string 1784 (sender + region name) and keeps an independent list of at most 10 (oldest dropped).
// - T272 (#649, docs/reference/sprites-and-strings.md §4.1): 1784 / 1785 / 2139 are string-c.tbl records (sub_8D43B0 0x8D46A7,
//   sub_8E0620 0x8E0699, sub_8D440D via sub_7DC800); mapDesc names come from MapDesc-c.tbl (CMapSystemXML this+0x48), NOT
//   string-c.tbl (same ids there are unrelated messages). sub_8D43B0 fills 1784 "%s從 %s地區…" with the sender and the
//   region record +8 as the two %s. Both tables are installed by LocationRegionLoader at run time (read-only from the client);
//   without them (CI, tests) the numbered placeholders 〔訊息1784〕 / 〔訊息1785〕 / 〔訊息2139〕 / 〔字串N〕 stay.
// - T290 (#698): with 1784 from string-c.tbl the chat line is exactly 1784 filled with (sender, region) — no engine prefix —
//   and the location link covers the whole line (ChatLine / LinkText). Without the table (CI, tests) the labelled
//   "[座標] sender：placeholder" form stays, because the original text cannot be reproduced. mapDesc ids past MapDesc-c.tbl use
//   desc-c.tbl record 4266 like sub_A730A0 (3.81 MapSystem never reaches it); without desc-c.tbl the bare region number is shown.
// R2-M (#619): the 10-record list is cleared by [WorldReset] and shown by NativeUi/LocationListPanel.cs.
public sealed record SharedLocation(ushort Map,ushort X,ushort Y,uint Zone);

/// <summary>One &lt;Map id=...&gt; of MapSystem XML World_Map (id 0–99) or Village_Map (id 100–999).</summary>
public sealed record LocationRegion(uint Id,ushort MapDesc,ushort MapNum,bool Listed) {
    public bool Village=>Id>=100;
}

/// <summary>
/// Region ids and mapDesc string ids parsed at runtime from the client's MapSystem XML (Tile.pak, never copied into git).
/// Only World_Map and Village_Map entries carry an id; Field_Map / Dungern_Map entries are not region names.
/// </summary>
public sealed class LocationRegionTable {
    public static readonly LocationRegionTable Empty=new([]);
    readonly Dictionary<uint,LocationRegion> regions;
    LocationRegionTable(IEnumerable<LocationRegion> items){regions=items.ToDictionary(r=>r.Id);}
    public int Count=>regions.Count;
    public IReadOnlyCollection<LocationRegion> Regions=>regions.Values;
    /// <summary>Receiver lookup: id &gt;= 100 → village table, otherwise world table (sub_8D43B0 +168 / +164).</summary>
    public LocationRegion? Find(uint id)=>regions.GetValueOrDefault(id);
    /// <summary>
    /// Sender region for the current game map. The original reads it from the position object +0xC (calculation not traced);
    /// inferred here: the World_Map entry whose mapNum is the current map (lowest id), else the lowest Village_Map entry, else none.
    /// </summary>
    public uint? RegionFor(ushort map){
        LocationRegion? world=null,village=null;
        foreach(var r in regions.Values){
            if(r.MapNum!=map)continue;
            if(!r.Village){if(world==null||r.Id<world.Id)world=r;}
            else if(village==null||r.Id<village.Id)village=r;
        }
        return (world??village)?.Id;
    }
    public static LocationRegionTable Parse(string xml){
        XDocument document;
        try{
            using var reader=XmlReader.Create(new StringReader(xml),new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null});
            document=XDocument.Load(reader);
        }catch(XmlException ex){throw new InvalidDataException("MapSystem XML 格式錯誤："+ex.Message,ex);}
        var root=document.Root;
        if(root==null||root.Name.LocalName!="Map_System_Configulation")throw new InvalidDataException("MapSystem XML 根節點不是 Map_System_Configulation");
        var items=new List<LocationRegion>();var seen=new HashSet<uint>();
        foreach(var (section,min,max) in new[]{("World_Map",0u,99u),("Village_Map",100u,999u)}){
            var parents=root.Elements(section).ToArray();
            if(parents.Length>1)throw new InvalidDataException($"MapSystem XML 有多個 {section}");
            foreach(var map in parents.SelectMany(p=>p.Elements("Map"))){
                uint id=Number(map,"id",max);
                if(id<min)throw new InvalidDataException($"MapSystem {section} 的 id {id} 超出 {min}–{max}");
                if(!seen.Add(id))throw new InvalidDataException($"MapSystem 地區 id {id} 重複");
                string? listed=(string?)map.Attribute("listed");
                if(listed!=null&&listed!="true"&&listed!="false")throw new InvalidDataException($"MapSystem 地區 {id} 的 listed 不是 true/false");
                items.Add(new(id,(ushort)Number(map,"mapDesc",ushort.MaxValue),(ushort)Number(map,"mapNum",ushort.MaxValue),listed!="false"));
            }
        }
        return new(items);
    }
    static uint Number(XElement map,string name,uint max){
        string? text=((string?)map.Attribute(name))?.Trim();
        if(text==null||text.Length==0||!text.All(char.IsAsciiDigit)||!uint.TryParse(text,out uint value)||value>max)
            throw new InvalidDataException($"MapSystem <Map> 的 {name} 不合法：{text??"（缺）"}");
        return value;
    }
}

public static class LocationShareProtocol {
    /// <summary>String table id shown on receive (sender, region name). Text unverified → numbered placeholder.</summary>
    public const ushort ReceivedMessageId=1784;
    /// <summary>String table id of the TransferMenu input prompt. Text unverified → numbered placeholder.</summary>
    public const ushort PromptMessageId=1785;
    /// <summary>String table id shown instead of 1784 when the receiver's function object +16 is 0 (WO §5). Text unverified (Q-2).</summary>
    public const ushort ListUnavailableMessageId=2139;
    /// <summary>2139 from string-c.tbl; numbered placeholder without the table.</summary>
    public static string ListUnavailableText=>Message(ListUnavailableMessageId) is {} text?text:$"〔訊息{ListUnavailableMessageId}〕";
    static LocationRegionTable regions=LocationRegionTable.Empty;
    static ServerStringTable? strings,mapDesc;
    /// <summary>Runtime MapSystem region table; Empty until the client XML is loaded (then ids are shown without names).</summary>
    public static LocationRegionTable Regions{get=>Volatile.Read(ref regions);set=>Volatile.Write(ref regions,value??LocationRegionTable.Empty);}
    /// <summary>string-c.tbl for 1784 / 1785 / 2139 (installed by LocationRegionLoader; null = placeholders).</summary>
    public static ServerStringTable? Strings{get=>Volatile.Read(ref strings);set=>Volatile.Write(ref strings,value);}
    /// <summary>MapDesc-c.tbl for region names (installed by LocationRegionLoader; null = 〔字串N〕 placeholders).</summary>
    public static ServerStringTable? MapDesc{get=>Volatile.Read(ref mapDesc);set=>Volatile.Write(ref mapDesc,value);}
    static ServerStringTable? desc;
    /// <summary>desc-c.tbl for the out-of-range mapDesc name (record 4266; installed by LocationRegionLoader; null = bare id).</summary>
    public static ServerStringTable? Desc{get=>Volatile.Read(ref desc);set=>Volatile.Write(ref desc,value);}
    /// <summary>sub_A730A0: mapDesc ≥ MapDesc-c.tbl count → desc-c.tbl record 4266 (SS§4.1).</summary>
    public const int OutOfRangeMapDescId=4266;
    static string? Message(int id)=>Strings is {} t&&id<t.Count&&ServerMessageText.DisplayText(t[id]) is {Length:>0} s?s:null;
    /// <summary>Region name: MapDesc-c.tbl[mapDesc] when loaded (desc-c.tbl 4266 past its end); the bare id when unknown.</summary>
    public static string RegionLabel(uint zone){
        if(Regions.Find(zone) is not {} region)return $"地區{zone}";
        if(MapDesc is not {} names)return $"地區{zone}〔字串{region.MapDesc}〕";
        if(region.MapDesc>=names.Count)
            return Desc is {} d&&OutOfRangeMapDescId<d.Count&&ServerMessageText.DisplayText(d[OutOfRangeMapDescId]).Trim() is {Length:>0} fallback?fallback:$"地區{zone}";
        return ServerMessageText.DisplayText(names[region.MapDesc]).Trim() is {Length:>0} name?name:$"地區{zone}";
    }
    /// <summary>
    /// sub_8D43B0's C-style "%s" fill of 1784: each "%s" takes the next argument (missing ones become empty), "%%" → "%".
    /// Other conversions are copied as written (1784 only has two %s).
    /// </summary>
    public static string FormatC(string template,params string[] arguments){
        var sb=new System.Text.StringBuilder(template.Length+32);int next=0;
        for(int i=0;i<template.Length;i++){
            if(template[i]=='%'&&i+1<template.Length&&template[i+1]=='s'){sb.Append(next<arguments.Length?arguments[next]:"");next++;i++;}
            else if(template[i]=='%'&&i+1<template.Length&&template[i+1]=='%'){sb.Append('%');i++;}
            else sb.Append(template[i]);
        }
        return sb.ToString();
    }
    /// <summary>
    /// Text after "[座標] sender：": 1784 with an empty sender and the region name (string table present), otherwise the numbered
    /// placeholder. Coordinates stay typed data, not displayed text.
    /// </summary>
    public static string Format(SharedLocation location)=>Message(ReceivedMessageId) is {} template
        ?FormatC(template,"",RegionLabel(location.Zone))
        :$"{RegionLabel(location.Zone)}〔訊息{ReceivedMessageId}〕";
    /// <summary>
    /// T290: the whole chat line of a received share. With 1784 from string-c.tbl: exactly sub_8D43B0's line, 1784 filled with
    /// (sender, region). Without the table: the engine's labelled form "[座標] sender：" + <see cref="Format"/>.
    /// </summary>
    public static string ChatLine(string sender,SharedLocation location)=>Message(ReceivedMessageId) is {} template
        ?FormatC(template,sender,RegionLabel(location.Zone))
        :$"[座標] {sender}：{Format(location)}";
    /// <summary>The clickable part at the end of <see cref="ChatLine"/>: the whole line with the table, else the placeholder text.</summary>
    public static string LinkText(string sender,SharedLocation location)=>Message(ReceivedMessageId)!=null?ChatLine(sender,location):Format(location);
    /// <summary>1785 (TransferMenu prompt) plus the engine's input rule.</summary>
    public static string PromptText=>Message(PromptMessageId) is {} prompt?prompt+"（不可含空白，最多 52 字）":$"〔訊息{PromptMessageId}〕請輸入收件角色名稱（不可含空白，最多 52 字）。";
    public static uint ZoneFor(ushort map)=>Regions.RegionFor(map)??0;
    public static byte[] Encode(string receiver,SharedLocation location){
        if(string.IsNullOrWhiteSpace(receiver)||receiver.Length>52||receiver.Any(c=>char.IsControl(c)||char.IsWhiteSpace(c)))
            throw new ArgumentException(PromptText);
        byte[] name;
        try{name=LoginPackets.TextEncoding.GetBytes(receiver);}
        catch(System.Text.EncoderFallbackException){throw new ArgumentException("收件角色名稱含有遊戲字集無法表示的字元。");}
        var packet=new byte[2+name.Length+1+10];packet[0]=254;packet[1]=11;name.CopyTo(packet,2);
        var fields=packet.AsSpan(3+name.Length);
        BinaryPrimitives.WriteUInt16LittleEndian(fields,location.Map);
        BinaryPrimitives.WriteUInt16LittleEndian(fields[2..],location.X);
        BinaryPrimitives.WriteUInt16LittleEndian(fields[4..],location.Y);
        BinaryPrimitives.WriteUInt32LittleEndian(fields[6..],location.Zone);
        return LoginPackets.Pad(packet);
    }
    public static bool IsReply(byte[] plain)=>plain.Length>=3&&plain[0]==71&&BinaryPrimitives.ReadUInt16LittleEndian(plain.AsSpan(1)) is 1782 or 1783;
    public static PlayerChatLine Decode(byte[] plain){
        var r=new LoginReader(plain);byte opcode=r.C();PlayerChatLine line;
        if(opcode==250&&r.C()==111){
            string sender=r.S();var location=new SharedLocation(r.H(),r.H(),r.H(),r.D());
            line=new(0,0,PlayerChatProtocol.Display(sender),PlayerChatChannel.Whisper){Location=location};
        }else if(opcode==71){
            ushort message=r.H();byte count=r.C();
            string text=message switch {
                1782 when count==0=>"無法找到該角色或角色不在線上。",
                1783 when count==1=>$"已發送座標位置給 {PlayerChatProtocol.Display(r.S())}。",
                _=>throw new ProtocolException("unexpected location reply")
            };
            line=new(0,0,text,PlayerChatChannel.Whisper);
        }else throw new ProtocolException("unexpected location packet");
        r.Finish();return line;
    }
}

/// <summary>One received share kept in the independent list (world-objects.md §5: map, x, y, sender; at most 10).</summary>
public sealed record LocationShareRecord(long Sequence,string Sender,SharedLocation Location,LocationRegion? Region);

public sealed partial class WorldSession {
    public const int LocationShareLimit=10;
    readonly Queue<LocationShareRecord> locationShares=new();
    public int LocationShareRevision {get;private set;}
    public LocationShareRecord[] LocationShares=>locationShares.ToArray();
    /// <summary>
    /// sub_8D43B0 first branch (WO §5): when the receiver's function object +16 is 0, only system message 2139 is shown and
    /// nothing is recorded. What +16 is in the original is not traced, so the product never clears it (always true);
    /// the branch exists so the data path matches the original once the condition is known. Not a session value (kept on reset).
    /// </summary>
    public bool LocationListObjectPresent {get;set;}=true;
    // R2-M: cleared through the M7-A [WorldReset] extension point (WorldSession.Reset runs on disconnect and on every world entry),
    // replacing the old actor/sequence guess.
    [WorldReset] void ResetLocationShares(){locationShares.Clear();LocationShareRevision++;}
    void ReceiveLocationShare(byte[] plain){
        var line=LocationShareProtocol.Decode(plain);
        if(line.Location!=null&&!LocationListObjectPresent)line=new(0,0,LocationShareProtocol.ListUnavailableText,PlayerChatChannel.Whisper);
        line=line with{Sequence=++playerChatSequence};
        playerChatLines.Enqueue(line);
        while(playerChatLines.Count>100)playerChatLines.Dequeue();
        playerChatRevision++;
        if(line.Location is {} location){
            locationShares.Enqueue(new(line.Sequence,line.Text,location,LocationShareProtocol.Regions.Find(location.Zone)));
            while(locationShares.Count>LocationShareLimit)locationShares.Dequeue();
            LocationShareRevision++;
        }
    }
    /// <summary>Region names come from the runtime table; redraw chat once it is loaded so displayed text and links agree.</summary>
    public void RefreshLocationLabels(){playerChatRevision++;LocationShareRevision++;}
}

/// <summary>Consistent copy of the 10-record list for the UI thread (taken under the world capture lock).</summary>
public sealed record LocationShareListView(int Generation,int WorldEpoch,int Revision,LocationShareRecord[] Records);

public sealed partial class LoginConnection {
    public LocationShareListView LocationShareSnapshot()=>CaptureWorld(()=>new LocationShareListView(generation,World.WorldEpoch,World.LocationShareRevision,World.LocationShares));
    async Task<bool> SendLocationCore(string receiver,int session,int world,uint actor){
        if(!PlayerChatAccess||!PlayerChatStamp(session,world,actor)||playerChatSending||WorldClock()<playerChatNextAt||sendMovementGate==null||World.MapId is not {} map||World.Actor is not {} own)return false;
        // Actor is the current client position (possibly predicted after C29, which has no per-step ACK).
        // Zone is relayed untouched by the server; the receiving client names it from MapSystem (world-objects.md §5).
        // MapSystem region of the current map (inferred rule, see LocationRegionTable.RegionFor); 0 only when unresolved.
        var location=new SharedLocation(map,own.X,own.Y,LocationShareProtocol.ZoneFor(map));byte[] packet;
        try{packet=LocationShareProtocol.Encode(receiver,location);}
        catch(ArgumentException ex){playerChatNotice=ex.Message;Changed?.Invoke();return false;}
        var sender=sendMovementGate;int lease=++playerChatLease;playerChatSending=true;
        try{
            bool sent=await sender(packet,()=>lease==playerChatLease&&PlayerChatAccess&&PlayerChatStamp(session,world,actor)&&World.MapId==map&&World.Actor is {} current&&current.X==location.X&&current.Y==location.Y,()=>playerChatNextAt=WorldClock()+1000);
            if(lease!=playerChatLease||!PlayerChatStamp(session,world,actor))return false;
            playerChatNotice=sent?"座標請求已送出，等待伺服器確認。":"角色或位置已變更，請重新發送座標。";
            Changed?.Invoke();return sent;
        }catch(Exception ex){
            if(session==generation){Cancel();Set(LoginState.Faulted,"座標傳送失敗："+ex.Message);}
            return false;
        }finally{if(lease==playerChatLease)playerChatSending=false;}
    }
}

public sealed record LocationChatLink(int Start,int Length,long Sequence,SharedLocation Location);
public static class LocationSharePresentation {
    // Match the actual displayed snapshot. Plain chat text cannot manufacture a location link.
    public static LocationChatLink[] Links(PlayerChatView view,string displayedText){
        var lines=view.Lines.Select(PlayerChatProtocol.FormatLine).ToArray();
        if(displayedText.Replace("\r\n","\n",StringComparison.Ordinal)!=string.Join("\n",lines))return [];
        var links=new List<LocationChatLink>();int start=0;
        for(int i=0;i<lines.Length;i++){
            if(view.Lines[i].Location is {} location){
                int length=LocationShareProtocol.LinkText(view.Lines[i].Text,location).Length;
                links.Add(new(start+lines[i].Length-length,length,view.Lines[i].Sequence,location));
            }
            start+=lines[i].Length+1;
        }
        return links.ToArray();
    }
    public static Point MarkerPoint(PointF projected,Rectangle viewport)=>new(
        (int)Math.Clamp(projected.X,viewport.Left+12,Math.Max(viewport.Left+12,viewport.Right-12)),
        (int)Math.Clamp(projected.Y,viewport.Top+12,Math.Max(viewport.Top+12,viewport.Bottom-12)));
}

// The form lifecycle extension below attaches through public controls; no changes to PlayerHudChat.cs.
// This is a UI-only indicator: clicks never move the player, change the camera or send packets.
public sealed class LocationShareUi:IDisposable {
    readonly Control owner;readonly RichTextBox history;readonly LoginConnection connection;
    readonly Func<Rectangle> viewport;readonly Func<SharedLocation,PointF> project;readonly Func<bool> active;
    readonly Label marker=new(){Text="⊕",TextAlign=ContentAlignment.MiddleCenter,Size=new(24,24),ForeColor=Color.Cyan,BackColor=Color.FromArgb(20,40,45),Enabled=false,Visible=false,AccessibleName="分享座標標記"};
    readonly Label notice=new(){AutoEllipsis=true,ForeColor=Color.Cyan,BackColor=Color.FromArgb(20,40,45),Enabled=false,Visible=false,AccessibleName="分享座標位置提示"};
    readonly System.Windows.Forms.Timer refresh=new(){Interval=100};
    readonly Func<P06.NativeUi.UiRoot?> nativeRoot;readonly ListHost listHost;
    PlayerChatView? displayed;LocationChatLink[] links=[];LocationChatLink? selected;long? listSelected;
    string styledText="";bool disposed;
    public LocationShareUi(Control owner,RichTextBox history,LoginConnection connection,Func<Rectangle> viewport,Func<SharedLocation,PointF> project,Func<bool> active,Func<P06.NativeUi.UiRoot?>? nativeRoot=null){
        this.owner=owner;this.history=history;this.connection=connection;this.viewport=viewport;this.project=project;this.active=active;
        this.nativeRoot=nativeRoot??(()=>null);listHost=new(this);
        owner.Controls.AddRange([marker,notice]);
        history.MouseUp+=ClickLocation;history.MouseMove+=HoverLocation;history.TextChanged+=HistoryChanged;
        owner.Disposed+=OwnerDisposed;refresh.Tick+=RefreshTick;refresh.Start();Refresh();
    }
    void HistoryChanged(object? sender,EventArgs e){styledText="";}
    void RefreshTick(object? sender,EventArgs e)=>Refresh();
    void OwnerDisposed(object? sender,EventArgs e)=>Dispose();
    void HoverLocation(object? sender,MouseEventArgs e){
        Refresh();history.Cursor=AtPoint(e.Location)!=null?Cursors.Hand:Cursors.IBeam;
    }
    LocationChatLink? AtPoint(Point point){
        int index=history.GetCharIndexFromPosition(point);if(index>=history.TextLength)return null;
        var glyph=history.GetPositionFromCharIndex(index);var next=history.GetPositionFromCharIndex(index+1);
        int width=next.Y==glyph.Y&&next.X>glyph.X?next.X-glyph.X:TextRenderer.MeasureText(history.Text[index].ToString(),history.Font,Size.Empty,TextFormatFlags.NoPadding).Width;
        // RichEdit otherwise maps blank space to the last character of a nearby line.
        if(point.Y<glyph.Y||point.Y>=glyph.Y+history.Font.Height+4||point.X<glyph.X||point.X>=glyph.X+width)return null;
        return links.FirstOrDefault(link=>index>=link.Start&&index<link.Start+link.Length);
    }
    void ClickLocation(object? sender,MouseEventArgs e){
        if(e.Button!=MouseButtons.Left)return;
        Refresh();if(AtPoint(e.Location) is {} link)Select(link);
    }
    void Select(LocationChatLink link){selected=link;listSelected=null;Refresh();}

    // R2-M: the native list panel (NativeUi/LocationListPanel.cs) reads the 10-record list through this host. A row click
    // only places the same ⊕ marker as a chat link; it never moves the player or sends a packet.
    sealed class ListHost(LocationShareUi ui):P06.NativeUi.ILocationListHost {
        public P06.NativeUi.LocationListView View=>ui.ListView();
        public void Select(P06.NativeUi.LocationListView shown,long sequence)=>ui.SelectShare(shown,sequence);
    }
    static long ListRevision(LocationShareListView list)=>((long)list.Generation<<32)|(uint)list.Revision;
    internal P06.NativeUi.ILocationListHost ListSource=>listHost;
    internal P06.NativeUi.LocationListView ListView(){
        if(disposed)return P06.NativeUi.LocationListView.Empty;
        var list=connection.LocationShareSnapshot();
        var rows=list.Records.Select(r=>new P06.NativeUi.LocationListRow(r.Sequence,r.Sender,LocationShareProtocol.RegionLabel(r.Location.Zone),r.Location.Map)).ToArray();
        return new(ListRevision(list),rows,listSelected is long s&&rows.Any(r=>r.Sequence==s)?s:null);
    }
    internal bool SelectShare(P06.NativeUi.LocationListView shown,long sequence){
        if(disposed)return false;
        var list=connection.LocationShareSnapshot();
        if(ListRevision(list)!=shown.Revision||list.Records.All(r=>r.Sequence!=sequence))return false;
        listSelected=sequence;selected=null;Refresh();return listSelected==sequence;
    }
    void AttachList(){
        if(nativeRoot()?.Find(P06.NativeUi.LocationListPanel.PanelId) is P06.NativeUi.LocationListPanel panel&&panel.Host==null)panel.Attach(listHost);
    }

    public void Refresh(){
        if(disposed||owner.IsDisposed||history.IsDisposed)return;
        AttachList();
        var view=connection.PlayerChatSnapshot();
        if(displayed==null||displayed.Generation!=view.Generation||displayed.WorldEpoch!=view.WorldEpoch||displayed.Actor!=view.Actor){selected=null;listSelected=null;styledText="";}
        displayed=view;links=LocationSharePresentation.Links(view,history.Text);
        if(styledText!=history.Text){
            int caret=history.SelectionStart,count=history.SelectionLength;
            using var underline=new Font(history.Font,FontStyle.Underline);
            foreach(var link in links){history.Select(link.Start,link.Length);history.SelectionFont=underline;history.SelectionColor=Color.Cyan;}
            history.Select(history.TextLength,0);history.SelectionFont=history.Font;
            history.Select(caret,count);styledText=history.Text;
        }
        if(!active()||connection.State!=LoginState.InWorld||!connection.World.Ready){selected=null;listSelected=null;}
        if(selected is {} old&&!view.Lines.Any(line=>line.Sequence==old.Sequence&&line.Location==old.Location))selected=null;
        SharedLocation? chosen=selected?.Location;
        if(chosen==null&&listSelected is long sequence){
            var list=connection.LocationShareSnapshot();
            var record=list.Generation==view.Generation?list.Records.FirstOrDefault(r=>r.Sequence==sequence):null;
            if(record==null)listSelected=null;else chosen=record.Location;
        }
        if(chosen is not {} target){marker.Visible=notice.Visible=false;return;}
        var bounds=viewport();notice.SetBounds(bounds.Left+8,bounds.Top+8,Math.Max(1,bounds.Width-16),24);
        string text=LocationShareProtocol.Format(target);
        if(connection.World.MapId!=target.Map){marker.Visible=false;notice.Text="分享座標（不同地圖）："+text;}
        else{
            var p=project(target);var point=LocationSharePresentation.MarkerPoint(p,bounds);
            marker.Location=new(point.X-12,point.Y-12);marker.Visible=true;marker.BringToFront();
            notice.Text=(bounds.Contains(Point.Round(p))?"分享座標：":"分享座標（視野外，邊緣指示）：")+text;
        }
        notice.Visible=true;notice.BringToFront();
    }
    public void Dispose(){
        if(disposed)return;disposed=true;refresh.Stop();refresh.Dispose();
        history.MouseUp-=ClickLocation;history.MouseMove-=HoverLocation;history.TextChanged-=HistoryChanged;
        owner.Disposed-=OwnerDisposed;marker.Dispose();notice.Dispose();
    }
}

public sealed partial class ClientForm {
    LocationShareUi? locationShareUi;
    protected override void OnShown(EventArgs e){
        base.OnShown(e);
        if(!normalProduct||playerHud==null||locationShareUi!=null)return;
        var chat=playerHud.Controls.OfType<PlayerHudChatPanel>().Single();
        var history=chat.Controls.OfType<RichTextBox>().Single();
        locationShareUi=new(this,history,login,()=>PlayerSceneBounds,
            location=>Camera.ToView(Coordinates.Screen(location.X,location.Y)),()=>login.World.Renderable,()=>NativeUiRoot);
        _=LocationRegionLoader.LoadAsync(login);
    }
}

/// <summary>Background load of MapSystem XML from the client's Tile.pak (decrypted in memory only).</summary>
public static class LocationRegionLoader {
    public static Task LoadAsync(LoginConnection login)=>Task.Run(()=>{
        try{
            // T272: string-c.tbl (1784/1785/2139) and MapDesc-c.tbl (region names) from the client's Text.pak, in memory only.
            LocationShareProtocol.Strings=ServerMessageText.Table;
            try{LocationShareProtocol.MapDesc=TextArchive.Open(GroundSpriteReader.ClientRoot).ReadMapDesc();}
            catch(Exception ex) when(ex is IOException or InvalidDataException or UnauthorizedAccessException or System.Security.Cryptography.CryptographicException or ArgumentException or System.Text.DecoderFallbackException){
                System.Diagnostics.Trace.WriteLine("[location-share] "+TextArchive.MapDescFileName+" unavailable: "+ex.Message);
            }
            // T290: desc-c.tbl only supplies record 4266 for mapDesc ids past MapDesc-c.tbl (sub_A730A0).
            try{LocationShareProtocol.Desc=TextArchive.Open(GroundSpriteReader.ClientRoot).ReadDesc();}
            catch(Exception ex) when(ex is IOException or InvalidDataException or UnauthorizedAccessException or System.Security.Cryptography.CryptographicException or ArgumentException or System.Text.DecoderFallbackException){
                System.Diagnostics.Trace.WriteLine("[location-share] "+TextArchive.DescFileName+" unavailable: "+ex.Message);
            }
            var (text,resolved)=P06.NativeUi.UiResources.Shared.Xml.ReadText("MapSystem");
            var table=LocationRegionTable.Parse(text);
            LocationShareProtocol.Regions=table;
            login.CaptureWorld(()=>{login.World.RefreshLocationLabels();return 0;});
            System.Diagnostics.Trace.WriteLine($"[location-share] {resolved}: {table.Count} regions");
        }catch(Exception ex){
            // Without the table the region id is still sent/shown as a number; nothing is invented.
            System.Diagnostics.Trace.WriteLine("[location-share] MapSystem region table unavailable: "+ex.Message);
        }
    });
}
