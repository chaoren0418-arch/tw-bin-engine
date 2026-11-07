using System.Buffers.Binary;
namespace P06;

// Verified source identities and the recipient-only contract: docs/protocol/location-share.md.
public sealed record SharedLocation(ushort Map,ushort X,ushort Y,uint Zone);
public static class LocationShareProtocol {
    public static string Format(SharedLocation location)=>$"地圖 {location.Map} ({location.X}, {location.Y})";
    public static byte[] Encode(string receiver,SharedLocation location){
        if(string.IsNullOrWhiteSpace(receiver)||receiver.Length>52||receiver.Any(c=>char.IsControl(c)||char.IsWhiteSpace(c)))
            throw new ArgumentException("請輸入收件角色名稱（不可含空白，最多 52 字）。");
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

public sealed partial class WorldSession {
    void ReceiveLocationShare(byte[] plain){
        var line=LocationShareProtocol.Decode(plain);
        playerChatLines.Enqueue(line with{Sequence=++playerChatSequence});
        while(playerChatLines.Count>100)playerChatLines.Dequeue();
        playerChatRevision++;
    }
}

public sealed partial class LoginConnection {
    async Task<bool> SendLocationCore(string receiver,int session,int world,uint actor){
        if(!PlayerChatAccess||!PlayerChatStamp(session,world,actor)||playerChatSending||WorldClock()<playerChatNextAt||sendMovementGate==null||World.MapId is not {} map||World.Actor is not {} own)return false;
        // Actor is the current client position (possibly predicted after C29, which has no per-step ACK).
        // Zone is opaque client UI metadata in the server relay; 0 is our neutral value.
        var location=new SharedLocation(map,own.X,own.Y,0);byte[] packet;
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
                int length=LocationShareProtocol.Format(location).Length;
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
    PlayerChatView? displayed;LocationChatLink[] links=[];LocationChatLink? selected;
    string styledText="";bool disposed;
    public LocationShareUi(Control owner,RichTextBox history,LoginConnection connection,Func<Rectangle> viewport,Func<SharedLocation,PointF> project,Func<bool> active){
        this.owner=owner;this.history=history;this.connection=connection;this.viewport=viewport;this.project=project;this.active=active;
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
    void Select(LocationChatLink link){selected=link;Refresh();}
    public void Refresh(){
        if(disposed||owner.IsDisposed||history.IsDisposed)return;
        var view=connection.PlayerChatSnapshot();
        if(displayed==null||displayed.Generation!=view.Generation||displayed.WorldEpoch!=view.WorldEpoch||displayed.Actor!=view.Actor){selected=null;styledText="";}
        displayed=view;links=LocationSharePresentation.Links(view,history.Text);
        if(styledText!=history.Text){
            int caret=history.SelectionStart,count=history.SelectionLength;
            using var underline=new Font(history.Font,FontStyle.Underline);
            foreach(var link in links){history.Select(link.Start,link.Length);history.SelectionFont=underline;history.SelectionColor=Color.Cyan;}
            history.Select(history.TextLength,0);history.SelectionFont=history.Font;
            history.Select(caret,count);styledText=history.Text;
        }
        if(!active()||connection.State!=LoginState.InWorld||!connection.World.Ready){selected=null;}
        if(selected is {} old&&!view.Lines.Any(line=>line.Sequence==old.Sequence&&line.Location==old.Location))selected=null;
        if(selected is not {} target){marker.Visible=notice.Visible=false;return;}
        var bounds=viewport();notice.SetBounds(bounds.Left+8,bounds.Top+8,Math.Max(1,bounds.Width-16),24);
        string text=LocationShareProtocol.Format(target.Location);
        if(connection.World.MapId!=target.Location.Map){marker.Visible=false;notice.Text="分享座標（不同地圖）："+text;}
        else{
            var p=project(target.Location);var point=LocationSharePresentation.MarkerPoint(p,bounds);
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
            location=>Camera.ToView(Coordinates.Screen(location.X,location.Y)),()=>login.World.Renderable);
    }
}
