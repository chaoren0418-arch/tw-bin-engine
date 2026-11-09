using System.Reflection;
using System.Text.Json;
using P06.NativeUi;
namespace P06;

// R2-M（T258／#619）發送座標收尾的離線測試：只用合成封包（S250/111 `S H H H D`，見 docs/reference/world-objects.md §5）、
// 記憶體內的 WinForms 控制項與合成畫布；不開遊戲、網路、伺服器、DB，也不讀客戶端素材。
static class LocationShareFollowupTests {
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    const uint Me=123;
    static byte[] Le32(uint v)=>[(byte)v,(byte)(v>>8),(byte)(v>>16),(byte)(v>>24)];
    static byte[] Le16(ushort v)=>[(byte)v,(byte)(v>>8)];
    static byte[] Str(string s)=>[..LoginPackets.TextEncoding.GetBytes(s),0];
    static byte[] P(params byte[][] parts)=>LoginPackets.Pad(parts.SelectMany(x=>x).ToArray());
    static byte[] Appear(uint id,ushort x,string name)=>P([87],Le16(x),Le16(32940),Le32(id),Le16((ushort)(id==Me?0:1110)),[0,0,0,0],Le32(0),Le16(0),
        Str(name),Str(""),[0],Le32(0),Str(""),Str(""),[0,100,0,0,0,255,255]);
    static byte[] Share(string sender,ushort map=0,ushort x=32600,ushort y=32940,uint zone=0)=>P([250,111],Str(sender),Le16(map),Le16(x),Le16(y),Le32(zone));
    static void Set(object o,string name,object? value){var p=o.GetType().GetProperty(name,F);if(p!=null)p.SetValue(o,value);else o.GetType().GetField(name,F)!.SetValue(o,value);}
    static LoginConnection Seed(){
        var c=new LoginConnection();Set(c,"State",LoginState.InWorld);Set(c.World,"Entered",true);Set(c.World,"MapId",(ushort)0);Set(c.World,"TimeReplies",1);Set(c.World,"CacheEnabled",false);
        Set(c.World,"Status",JsonSerializer.Deserialize<OwnStatus>("{\"ObjectId\":123,\"Level\":44,\"Hp\":100,\"MaxHp\":100,\"Mp\":100,\"MaxMp\":100}"));
        c.World.Receive(Appear(Me,32600,"offline"),"offline");
        return c;
    }

    sealed class FakeHost:ILocationListHost {
        public LocationListView View{get;set;}=LocationListView.Empty;
        public List<(long revision,long sequence)> Selected{get;}=[];
        public void Select(LocationListView shown,long sequence)=>Selected.Add((shown.Revision,sequence));
    }
    static UiMouseEvent Mouse(UiMouseKind kind,Point at,MouseButtons button=MouseButtons.Left)=>new(kind,at,button,1,0,Keys.None);

    [WorldModuleTest("location-share-followup")]
    static void Run(WorldModuleTestContext t){
        // 1. 註冊：清單改由 M7-A 的 [WorldReset] 清空；面板只在註冊檔加一行。
        var entries=WorldExtensionRegistry.Default.Entries;
        t.Check(entries.Any(e=>e.Kind=="reset"&&e.Method=="WorldSession.ResetLocationShares"),"[WorldReset] ResetLocationShares registered");
        t.Check(WorldExtensionRegistry.Default.Errors.Count==0,"default registry valid",WorldExtensionRegistry.Default.Errors);
        t.Equal(1,UiPanelRegistry.Panels.Count(r=>r.Id==LocationListPanel.PanelId),"location list panel registered once");
        t.Check(UiPanelRegistry.Panels.Single(r=>r.Id==LocationListPanel.PanelId).Layout.Length>0,"registration names a layout XML");
        t.Equal((ushort)2139,LocationShareProtocol.ListUnavailableMessageId,"+16 = 0 message id is 2139 (WO §5)");
        t.Equal("〔訊息2139〕",LocationShareProtocol.ListUnavailableText,"2139 text stays a numbered placeholder until Q-2");

        // 2. 清單：最多 10 筆、先刪最舊；與聊天歷史分開；換 actor 不再猜測清空，只由 Reset 清空。
        using(var c=Seed()){
            var w=c.World;
            t.Equal(0,w.LocationShares.Length,"list starts empty");
            int revision=w.LocationShareRevision;
            for(int i=1;i<=12;i++)w.Receive(Share("寄件"+i,0,(ushort)(32600+i)),"offline");
            var list=w.LocationShares;
            t.Equal(WorldSession.LocationShareLimit,list.Length,"list keeps at most 10");
            t.Check(list[0].Sender=="寄件3"&&list[^1].Sender=="寄件12"&&list[0].Location.X==32603,"oldest two dropped first");
            t.Check(list.Select(r=>r.Sequence).SequenceEqual(list.Select(r=>r.Sequence).Order())&&list.Select(r=>r.Sequence).Distinct().Count()==10,"sequences unique and increasing");
            t.Check(w.LocationShareRevision>revision,"revision advances on record");
            t.Equal(12,w.PlayerChatLines.Count(l=>l.Location!=null),"chat history keeps its own lines");
            Set(w,"Status",JsonSerializer.Deserialize<OwnStatus>("{\"ObjectId\":999}"));
            t.Equal(10,w.LocationShares.Length,"actor/status change alone does not clear the list (no guess, reset only)");
            var snapshot=c.LocationShareSnapshot();
            t.Check(snapshot.Records.Length==10&&snapshot.Revision==w.LocationShareRevision&&snapshot.Generation==c.SessionGeneration&&snapshot.WorldEpoch==w.WorldEpoch,"LoginConnection snapshot copies list under the capture lock");
            int before=w.LocationShareRevision;
            w.Reset();
            t.Equal(0,w.LocationShares.Length,"WorldSession.Reset clears the list through [WorldReset]");
            t.Check(w.LocationShareRevision>before,"reset advances revision (UI drops stale rows)");
            t.Equal(10,snapshot.Records.Length,"earlier snapshot is an independent copy");
        }

        // 3. 功能物件 +16 為 0：只顯示 2139，不記錄、不產生座標連結。
        using(var c=Seed()){
            var w=c.World;
            w.Receive(Share("甲"),"offline");int revision=w.LocationShareRevision;int chat=w.PlayerChatLines.Length;
            w.LocationListObjectPresent=false;
            w.Receive(Share("乙",4,1,2,7),"offline");
            var line=w.PlayerChatLines[^1];
            t.Check(w.PlayerChatLines.Length==chat+1&&line.Text=="〔訊息2139〕"&&line.Location==null,"+16 = 0 shows only message 2139 without a location");
            t.Check(PlayerChatProtocol.FormatLine(line)=="〔訊息2139〕"||PlayerChatProtocol.FormatLine(line).EndsWith("〔訊息2139〕",StringComparison.Ordinal),"2139 line carries no sender/region text",PlayerChatProtocol.FormatLine(line));
            t.Check(w.LocationShares.Length==1&&w.LocationShareRevision==revision,"+16 = 0 records nothing");
            t.Throws<ProtocolException>(()=>w.Receive(Share("乙")[..^4],"offline"),"+16 = 0 still rejects malformed packets");
            w.Reset();t.Check(!w.LocationListObjectPresent,"+16 flag is not session data (kept on reset)");
            w.LocationListObjectPresent=true;w.Receive(Share("丙"),"offline");
            t.Check(w.LocationShares.Single().Sender=="丙"&&w.PlayerChatLines[^1].Location!=null,"present again: recorded and shown as 1784 line");
        }

        // 4. 面板（合成狀態，不讀素材）：只在 Field_Map 開著且清單不空時出現，只畫文字、不畫底色。
        {
            var anchor=Rectangle.Empty;var host=new FakeHost();var panel=new LocationListPanel(()=>anchor);
            var inWorld=new UiStaticGameState(InWorld:true);var commands=new UiNullCommands();
            panel.Load(null!,null!);
            t.Check(panel.Layer==UiLayer.Window&&panel.Id==LocationListPanel.PanelId,"window layer, engine-owned id");
            panel.Update(inWorld);t.Check(!panel.Visible&&panel.Bounds.IsEmpty,"no host → hidden");
            panel.Attach(host);
            LocationListRow[] rows=[new(5,"甲","地區1〔字串400〕",4),new(9,"乙","地區0",0),new(11,"丙","地區120〔字串888〕",4)];
            host.View=new(77,rows,null);panel.Update(inWorld);
            t.Check(panel.Visible&&panel.Bounds.IsEmpty,"Field_Map closed → nothing painted or hit");
            anchor=new(5,250,170,200);
            var b=panel.Bounds;
            t.Equal(new Rectangle(177,250,LocationListPanel.Width,3*LocationListPanel.RowHeight+2*LocationListPanel.Padding),b,"bounds beside Field_Map, one row per record");
            anchor=new(700,500,90,90);
            t.Check(panel.Bounds.Right<=UiRoot.LogicalWidth&&panel.Bounds.Bottom<=UiRoot.LogicalHeight,"bounds clamped inside 800×600",panel.Bounds);
            anchor=new(5,250,170,200);
            t.Equal("乙：地區0",LocationListPanel.RowText(rows[1]),"row text = sender + region label");
            t.Equal(rows[0],panel.RowAt(new(b.X+3,b.Y+LocationListPanel.Padding)),"hit row 0");
            t.Equal(rows[2],panel.RowAt(new(b.X+3,b.Bottom-LocationListPanel.Padding-1)),"hit row 2");
            t.Check(panel.RowAt(new(b.X+3,b.Y))==null&&panel.RowAt(new(b.X-1,b.Y+5))==null,"padding and outside are not rows");
            Point Row(int i)=>new(b.X+10,b.Y+LocationListPanel.Padding+i*LocationListPanel.RowHeight+4);
            t.Check(panel.Mouse(Mouse(UiMouseKind.Down,Row(1)),commands)&&panel.Mouse(Mouse(UiMouseKind.Up,Row(1)),commands),"click consumed");
            t.Check(host.Selected.SequenceEqual([(77L,9L)]),"click selects with shown revision and row sequence",host.Selected);
            panel.Mouse(Mouse(UiMouseKind.Down,Row(0)),commands);panel.Mouse(Mouse(UiMouseKind.Up,Row(2)),commands);
            t.Equal(1,host.Selected.Count,"press on one row, release on another → no selection");
            panel.Mouse(Mouse(UiMouseKind.Down,Row(0)),commands);host.View=new(78,rows,null);panel.Update(inWorld);panel.Mouse(Mouse(UiMouseKind.Up,Row(0)),commands);
            t.Equal(1,host.Selected.Count,"list changed between press and release → no selection");
            t.Check(!panel.Mouse(Mouse(UiMouseKind.Down,new(b.Right+5,b.Y+5)),commands),"outside not consumed");
            panel.Mouse(Mouse(UiMouseKind.Down,Row(0),MouseButtons.Right),commands);panel.Mouse(Mouse(UiMouseKind.Up,Row(0),MouseButtons.Right),commands);
            t.Equal(1,host.Selected.Count,"right click does nothing");
            t.Check(!panel.Key(new UiKeyEvent(Keys.Escape,true,false),commands),"no key handling");
            t.Equal(0,commands.Calls.Count,"panel issues no commands");

            host.View=new(79,rows,9);panel.Update(inWorld);
            using var canvas=new UiCanvas();canvas.Clear(Color.Transparent);canvas.Restrict(panel.Bounds);panel.Paint(canvas);canvas.Release();
            int ink=0,outside=0,cyan=0;
            for(int y=0;y<UiRoot.LogicalHeight;y++)for(int x=0;x<UiRoot.LogicalWidth;x++){
                var px=canvas.Bitmap.GetPixel(x,y);if(px.A==0)continue;
                if(b.Contains(x,y))ink++;else outside++;
                if(px.R==0&&px.G==255&&px.B==255)cyan++;
            }
            t.Check(ink>0&&outside==0,"text drawn only inside bounds",new{ink,outside});
            t.Check(ink<b.Width*b.Height/3,"no background fill (rule 5: text only)",new{ink,area=b.Width*b.Height});
            t.Check(cyan>0,"selected row drawn in the highlight colour",cyan);
            t.Check(Enumerable.Range(b.X,b.Width).All(x=>canvas.Bitmap.GetPixel(x,b.Top).A==0),"top padding line stays transparent (no frame or fill)");

            panel.Update(new UiStaticGameState(InWorld:false));
            t.Check(!panel.Visible,"left the world → hidden");
            host.View=new(80,[],null);panel.Update(inWorld);
            t.Check(!panel.Visible&&panel.Bounds.IsEmpty,"empty list → hidden");
        }

        // 5. 轉接：LocationShareUi 提供清單、點列只放標記（不送封包、不移動），過期與重置都拒絕。
        Exception? error=null;
        var thread=new Thread(()=>{try{HostChecks(t);}catch(Exception ex){error=ex;}});
        thread.SetApartmentState(ApartmentState.STA);thread.Start();thread.Join();
        t.Check(error==null,"host checks ran on STA thread",error?.ToString());
    }

    static void HostChecks(WorldModuleTestContext t){
        using var c=Seed();int sends=0;
        Func<byte[],Func<bool>,Action,Task<bool>> sender=(_,_,_)=>{sends++;return Task.FromResult(true);};Set(c,"sendMovementGate",sender);
        using var owner=new Panel{Size=new(900,600)};using var history=new RichTextBox();owner.Controls.Add(history);
        owner.CreateControl();history.CreateControl();
        var root=new UiRoot();var panel=new LocationListPanel(()=>new(5,250,170,200));root.Add(panel);
        using var ui=new LocationShareUi(owner,history,c,()=>new(0,0,800,470),_=>new PointF(200,200),()=>true,()=>root);
        t.Check(ReferenceEquals(panel.Host,ui.ListSource),"LocationShareUi attaches itself to the registered panel");
        var marker=owner.Controls.OfType<Label>().Single(x=>x.AccessibleName=="分享座標標記");
        var notice=owner.Controls.OfType<Label>().Single(x=>x.AccessibleName=="分享座標位置提示");

        c.World.Receive(Share("甲",0,32610,32950,1),"offline");c.World.Receive(Share("乙",4),"offline");
        var view=ui.ListView();
        t.Check(view.Rows.Count==2&&view.Rows[0].Sender=="甲"&&view.Rows[1].Sender=="乙"&&view.Selected==null,"host view mirrors the world list",view);
        t.Equal(LocationShareProtocol.RegionLabel(1),view.Rows[0].Region,"row region uses the same label as chat");
        panel.Update(new UiStaticGameState(InWorld:true));t.Check(panel.Visible,"panel visible with rows");

        t.Check(!ui.SelectShare(view with{Revision=view.Revision+1},view.Rows[0].Sequence),"stale revision refused");
        t.Check(!ui.SelectShare(view,12345),"unknown sequence refused");
        t.Check(!marker.Visible&&!notice.Visible,"nothing selected yet");
        t.Check(ui.SelectShare(view,view.Rows[0].Sequence),"current row selected");
        t.Check(marker.Visible&&notice.Visible&&notice.Text.Contains("分享座標"),"same-map row places the ⊕ marker",notice.Text);
        t.Equal(view.Rows[0].Sequence,ui.ListView().Selected,"selection reported back to the panel");
        t.Check(ui.SelectShare(ui.ListView(),view.Rows[1].Sequence)&&!marker.Visible&&notice.Text.Contains("不同地圖"),"other-map row shows the notice without a false marker",notice.Text);
        t.Check(sends==0&&c.World.Actor!.X==32600,"row selection never sends or moves");

        var stale=ui.ListView();
        c.World.Reset();ui.Refresh();
        t.Check(ui.ListView().Rows.Count==0&&!marker.Visible&&!notice.Visible,"reset clears rows, selection and marker");
        t.Check(!ui.SelectShare(stale,stale.Rows[0].Sequence),"pre-reset view cannot select");
        ui.Dispose();
        t.Check(ui.ListView().Rows.Count==0,"disposed host shows nothing");
    }
}
