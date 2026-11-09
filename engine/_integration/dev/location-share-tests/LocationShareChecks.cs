using P06;
using System.Buffers.Binary;

partial class Checks {
    static byte[] LocationPacket(string sender="中",ushort map=4,ushort x=32600,ushort y=32940,uint zone=0)=>Packet(b=>{
        b.Write((byte)250);b.Write((byte)111);Text(b,sender);b.Write(map);b.Write(x);b.Write(y);b.Write(zone);
    });
    static byte[] LocationReply(ushort id,params string[] args)=>Packet(b=>{
        b.Write((byte)71);b.Write(id);b.Write((byte)args.Length);foreach(var arg in args)Text(b,arg);
    });
    static void LocationReject(Action action,string label){
        try{action();}catch(Exception ex) when(ex is ArgumentException or ProtocolException){Check(true,label);return;}
        throw new Exception("location accepted invalid input: "+label);
    }
    [CheckGroup("location-share"),CheckGroup("map-continuity",Order=180),CheckGroup(CheckGroups.Default,Order=370)]
    static async Task LocationShareChecks(){
        var location=new SharedLocation(0x1234,0x5678,0x9abc,0xdef01234);
        Check(Convert.ToHexString(LocationShareProtocol.Encode("中",location))=="FE0BA4A40034127856BC9A3412F0DE00","location C254 exact CP950 + HHH/D little endian vector");
        foreach(string name in new[]{""," ","a b","a\0b","a\nb","😀",new string('a',53)})
            LocationReject(()=>LocationShareProtocol.Encode(name,location),"location invalid recipient "+name.Length);
        Check(LocationShareProtocol.Encode(new string('中',52),location).Length==120,"location local recipient character budget");
        var vector=Convert.FromHexString("FA6FA4A40034127856BC9A3412F0DE00");
        var decoded=LocationShareProtocol.Decode(vector);
        Check(decoded.Location==location&&decoded.Text=="中"&&decoded.Channel==PlayerChatChannel.Whisper,"location S250/111 full fields including opaque uint zone");
        Check(PlayerChatProtocol.FormatLine(decoded)=="[座標] 中：地區3740275252〔訊息1784〕","location display is sender + region placeholder (no table), not coordinates");
        Check(LocationShareProtocol.Decode(LocationPacket("a",ushort.MaxValue,0,ushort.MaxValue,uint.MaxValue)).Location==new SharedLocation(65535,0,65535,uint.MaxValue),"location full unsigned coordinate ranges");
        Check(LocationShareProtocol.Decode(LocationReply(1782)).Text.Contains("不在線上"),"location S71/1782 failure");
        Check(LocationShareProtocol.Decode(LocationReply(1783,"測試者")).Text.Contains("測試者"),"location S71/1783 success CP950 argument");
        foreach(var packet in new[]{vector,LocationReply(1782),LocationReply(1783,"中")}){
            for(int length=0;length<packet.Length;length++){
                var cut=packet[..length];LocationReject(()=>LocationShareProtocol.Decode(cut),"location truncation "+packet[0]+"/"+length);
            }
            LocationReject(()=>LocationShareProtocol.Decode([..packet,new byte()]),"location extra length "+packet[0]);
            var bad=(byte[])packet.Clone();bad[^1]=1;LocationReject(()=>LocationShareProtocol.Decode(bad),"location nonzero padding "+packet[0]);
        }
        foreach(var bad in new[]{LocationReply(1782,"unexpected"),LocationReply(1783),LocationReply(125),LoginPackets.Pad([250,110]),LoginPackets.Pad([81,0])})
            LocationReject(()=>LocationShareProtocol.Decode(bad),"location invalid format or arg count");
        using(var c=Seed()){
            Feed(c,vector);Check(c.World.PlayerChatLines.Single().Location==location,"location actual WorldSession S250 routing");
            int revision=c.World.PlayerChatRevision;
            LocationReject(()=>Feed(c,vector[..^1]),"location malformed dispatch rejected");
            Check(c.World.PlayerChatRevision==revision&&c.World.PlayerChatLines.Length==1,"location malformed packet does not mutate history");
            Feed(c,LocationReply(1782));Feed(c,LocationReply(1783,"中"));
            Check(c.World.PlayerChatLines.Length==3&&c.World.PlayerChatLines[^1].Location==null,"location replies shown without invented outgoing coordinates");
            Feed(c,LoginPackets.Pad([250,11,99]));
            Check(c.World.Status!.Food==99&&c.World.PlayerChatLines.Length==3,"location leaves other S250 subtypes to sustenance");
            Feed(c,LocationReply(125));Check(c.World.LastServerReply?.Id==125,"location leaves unrelated S71 to Inventory");
            for(int i=0;i<101;i++)Feed(c,LocationPacket("sender",4,(ushort)i));
            Check(c.World.PlayerChatLines.Length==100&&c.World.PlayerChatLines[0].Location!.X==1,"location bounded shared history");
            c.World.Reset();Check(c.World.PlayerChatLines.Length==0,"location reset clears received history");
        }
        using(var c=Seed()){
            int sent=0;byte[]? actual=null;
            Sender(c,(bytes,current,admitted)=>{if(!current())return Task.FromResult(false);sent++;actual=bytes;admitted();return Task.FromResult(true);});
            var s=c.PlayerChatSnapshot();
            Check(await c.SendPlayerChat("/loc 中",s.Generation,s.WorldEpoch,s.Actor)&&sent==1,"location chat command uses real gated sender");
            Check(actual!.SequenceEqual(LocationShareProtocol.Encode("中",new(0,32600,32940,0))),"location sends current own map/xy and neutral zone");
            Check(c.World.PlayerChatLines.Length==0&&c.PlayerChatSnapshot().Notice.Contains("等待"),"location transport success is not a server acknowledgement");
            Check(!await c.SendLocation("中",s.Generation,s.WorldEpoch,s.Actor)&&!await c.SendPlayerChat("聊天",s.Generation,s.WorldEpoch,s.Actor)&&sent==1,"location shares chat cooldown");
        }
        using(var c=Seed()){
            int sent=0;Sender(c,(_,_,_)=>{sent++;return Task.FromResult(true);});var s=c.PlayerChatSnapshot();
            Check(!await c.SendPlayerChat("/loc",s.Generation,s.WorldEpoch,s.Actor),"location missing recipient never falls through to public chat");
            Check(!await c.SendLocation("a b",s.Generation,s.WorldEpoch,s.Actor),"location invalid recipient before sender");
            Check(!await c.SendLocation("中",s.Generation+1,s.WorldEpoch,s.Actor),"location old session refused");
            Check(!await c.SendLocation("中",s.Generation,s.WorldEpoch+1,s.Actor),"location old world refused");
            Check(!await c.SendLocation("中",s.Generation,s.WorldEpoch,s.Actor+1),"location wrong actor refused");
            Set(c.World,"OwnDead",true);Check(!await c.SendLocation("中",s.Generation,s.WorldEpoch,s.Actor)&&sent==0,"location dead actor refused");
        }
        using(var c=Seed()){
            byte[]? actual=null;Sender(c,(bytes,current,admitted)=>{if(!current())return Task.FromResult(false);actual=bytes;admitted();return Task.FromResult(true);});
            Call(c.World,"Predict",32601,32940,2,true);var s=c.PlayerChatSnapshot();
            Check(await c.SendLocation("中",s.Generation,s.WorldEpoch,s.Actor)&&BinaryPrimitives.ReadUInt16LittleEndian(actual!.AsSpan(7))==32601,"location current client position after unacknowledged movement, not stale authoritative spawn");
        }
        foreach(string change in new[]{"map","move","reset","death","lock"}){
            using var c=Seed();var release=new TaskCompletionSource<bool>();int sent=0;
            Sender(c,async(_,current,admitted)=>{await release.Task;if(!current())return false;sent++;admitted();return true;});
            var s=c.PlayerChatSnapshot();var pending=c.SendLocation("中",s.Generation,s.WorldEpoch,s.Actor);
            Check(!await c.SendLocation("中",s.Generation,s.WorldEpoch,s.Actor)&&!await c.SendPlayerChat("聊天",s.Generation,s.WorldEpoch,s.Actor),"location blocks duplicate/shared chat while queued "+change);
            switch(change){
            case "map":Feed(c,LoginPackets.Pad([206,4,0,0]));break;
            case "move":Feed(c,Pack(123,32601));break;
            case "reset":c.World.Reset();break;
            case "death":Set(c.World,"OwnDead",true);break;
            case "lock":Feed(c,LoginPackets.Pad([202,2]));break;
            }
            release.SetResult(true);Check(!await pending&&sent==0,"location revalidates before socket write "+change);
        }
        await LocationRegionChecks();
        Exception? uiError=null;var uiThread=new Thread(()=>{try{LocationShareUiChecks();}catch(Exception ex){uiError=ex;}});
        uiThread.SetApartmentState(ApartmentState.STA);uiThread.Start();uiThread.Join();if(uiError!=null)throw uiError;
    }
    static void LocationShareUiChecks(){
        using var c=Seed();int sends=0;Sender(c,(_,_,_)=>{sends++;return Task.FromResult(true);});
        using var owner=new Panel{Size=new(900,600)};
        using var chat=new PlayerHudChatPanel{Size=new(880,250),Location=new(0,300)};owner.Controls.Add(chat);
        owner.CreateControl();chat.CreateControl();chat.PerformLayout();
        var history=chat.Controls.OfType<RichTextBox>().Single();history.CreateControl();
        PointF projected=new(200,200);
        using var ui=new LocationShareUi(owner,history,c,()=>new(0,0,800,280),_=>projected,()=>true);
        var marker=owner.Controls.OfType<Label>().Single(x=>x.AccessibleName=="分享座標標記");
        var notice=owner.Controls.OfType<Label>().Single(x=>x.AccessibleName=="分享座標位置提示");
        void Update(){chat.UpdateChat(c.PlayerChatSnapshot());ui.Refresh();}
        void Click(int index){var point=history.GetPositionFromCharIndex(index);typeof(RichTextBox).GetMethod("OnMouseUp",F)!.Invoke(history,[new MouseEventArgs(MouseButtons.Left,1,point.X+1,point.Y+1,0)]);}
        Feed(c,LocationPacket("中",0));Update();
        var links=LocationSharePresentation.Links(c.PlayerChatSnapshot(),history.Text);
        Check(links.Length==1&&history.Text.Substring(links[0].Start,links[0].Length)==LocationShareProtocol.Format(new(0,32600,32940,0)),"location typed data links to exact displayed region text");
        history.Select(links[0].Start,1);Check(history.SelectionFont!.Underline,"location coordinates visibly underlined in real RichTextBox");
        Click(links[0].Start);
        Check(marker.Visible&&marker.Location==new Point(188,188)&&notice.Text.Contains("地區0"),"location actual mouse event places marker on projected tile");
        Check(sends==0&&c.World.Actor!.X==32600,"location click does not send movement or change player position");
        projected=new(900,-500);ui.Refresh();
        Check(marker.Location==new Point(776,0)&&notice.Text.Contains("視野外"),"location offscreen marker clamps within scene and labels explicitly");
        Feed(c,LocationPacket("其他角色",4));Update();links=LocationSharePresentation.Links(c.PlayerChatSnapshot(),history.Text);
        Click(links[1].Start);Check(!marker.Visible&&notice.Text.Contains("不同地圖"),"location other-map click shows destination without false on-map marker");
        Check(LocationSharePresentation.Links(c.PlayerChatSnapshot(),history.Text+"stale").Length==0,"location stale rendered snapshot cannot be clicked");
        Feed(c,Packet(b=>{b.Write((byte)243);b.Write((byte)3);Text(b,LocationShareProtocol.Format(new(0,32600,32940,0)));}));Update();
        Check(LocationSharePresentation.Links(c.PlayerChatSnapshot(),history.Text).Length==2,"location ordinary chat cannot forge a clickable coordinate");
        Feed(c,LoginPackets.Pad([206,4,0,0]));Feed(c,Pack(123));Update();
        Check(!marker.Visible&&!notice.Visible,"location world epoch transition clears marker");
        links=LocationSharePresentation.Links(c.PlayerChatSnapshot(),history.Text);Click(links[1].Start);
        Check(marker.Visible,"location deliberate click can select history after arrival");
        c.World.Reset();Update();Check(!marker.Visible&&!notice.Visible&&history.TextLength==0,"location session reset clears marker and history");
        Check(sends==0,"location all UI interaction remains local");
        ui.Dispose();Check(marker.IsDisposed&&notice.IsDisposed,"location UI disposal releases timer and controls");
    }
}
