using P06;
using System.Reflection;

partial class Checks {
    static byte[] BookmarkList(params Bookmark[] rows)=>Packet(b=>{
        b.Write((byte)64);b.Write((byte)0x2A);b.Write((ushort)128);b.Write((byte)2);
        b.Write(Enumerable.Repeat((byte)255,127).ToArray());b.Write((ushort)50);b.Write((ushort)rows.Length);
        foreach(var row in rows){b.Write(row.Id);Text(b,row.Name);b.Write(row.MapId);b.Write(row.X);b.Write(row.Y);}
    });
    static byte[] BookmarkOne(Bookmark row)=>Packet(b=>{b.Write((byte)92);Text(b,row.Name);b.Write(row.MapId);b.Write(row.X);b.Write(row.Y);b.Write(row.Id);});
    static void BookmarkReject(Action action,string label){
        try{action();}catch(Exception e) when(e is ProtocolException or ArgumentException){Check(true,label);return;}
        throw new Exception("expected rejection: "+label);
    }
    static async Task BookmarkChecks(){
        var one=new Bookmark(0x12345678,"甲",4,32768,65535);var two=new Bookmark(7,"Home",0,0,1);
        Check(Convert.ToHexString(BookmarkPackets.Add("甲"))=="A5A5D20000000000","C165 Big5 S + zero pad golden");
        Check(Convert.ToHexString(BookmarkPackets.Delete("甲"))=="03A5D20000000000","C3 Big5 S + zero pad golden");
        Check(Convert.ToHexString(BookmarkPackets.Delete("Home"))=="03486F6D65000000","C3 ASCII S golden");
        foreach(string bad in new[]{""," ","a\0b","a\nb","😀",new string('甲',33)}){
            BookmarkReject(()=>BookmarkPackets.Add(bad),"invalid add name");
        }
        foreach(string bad in new[]{"","a\0b","😀",new string('a',10231)})BookmarkReject(()=>BookmarkPackets.Delete(bad),"invalid delete name");
        Check(BookmarkPackets.Delete(new string('甲',40)).Length==88,"legacy server name can be deleted beyond new-name limit");
        Check(BookmarkPackets.Add(new string('甲',32)).Length==72,"64 Big5 bytes boundary accepted");
        using(var c=Seed()){
            var w=c.World;Feed(c,BookmarkList(one,two));
            Check(w.Bookmarks.Items.SequenceEqual(new[]{one,two})&&w.Bookmarks.Capacity==50,"S64 full list preserves ID Big5 map XY");
            Check(w.Bookmarks.TryGet(one.Id)==one&&w.Bookmarks.TryGet(99)==null&&w.Bookmarks.TryGet(two.Id,out var found)&&found==two,"TryGet contracts");
            var changed=one with{Name="乙",MapId=65535,X=1,Y=0};Feed(c,BookmarkOne(changed));
            Check(w.Bookmarks.TryGet(one.Id)==changed&&w.Bookmarks.Items.Length==2,"S92 upserts by id");
            Feed(c,BookmarkList(two));Check(w.Bookmarks.Items.SequenceEqual(new[]{two}),"S64 replaces prior list");
            int revision=w.Bookmarks.Revision;
            foreach(var data in new[]{BookmarkList(one),BookmarkOne(one)}){
                for(int n=0;n<data.Length;n++){
                    if(data[0]==64&&n==1)continue; // No subtype yet: shared opcode is not claimed.
                    byte[] truncated=data[..n];BookmarkReject(()=>Feed(c,truncated),"all truncated packet prefixes");
                    Check(w.Bookmarks.Revision==revision&&w.Bookmarks.Items.SequenceEqual(new[]{two}),"truncated packet atomicity");
                }
            }
            foreach(int offset in new[]{2,4,5,131}){
                var bad=BookmarkList(one);bad[offset]=0;BookmarkReject(()=>Feed(c,bad),"invalid list header/slots");
            }
            var count=BookmarkList(one);count[134]=255;count[135]=255;BookmarkReject(()=>Feed(c,count),"impossible count");
            var tail=BookmarkOne(one);tail[^1]=1;BookmarkReject(()=>Feed(c,tail),"nonzero padding");
            BookmarkReject(()=>Feed(c,BookmarkList(one,one)),"duplicate ids rejected");
            BookmarkReject(()=>Feed(c,BookmarkOne(one with{Id=0})),"zero id rejected");
            Check(w.Bookmarks.Items.SequenceEqual(new[]{two})&&w.Bookmarks.Revision==revision,"bad packet leaves old model intact");
            Feed(c,LoginPackets.Pad([64,65,0]));Check(w.EquipmentListReceived&&w.Bookmarks.Items.SequenceEqual(new[]{two}),"shared S64 equipment subtype preserved");
            Feed(c,LoginPackets.Pad([64,99]));Check(w.Bookmarks.Revision==revision,"unrelated S64 subtype ignored");
            Feed(c,LoginPackets.Pad([206,4,0,0]));Check(w.Bookmarks.TryGet(two.Id)==two,"map transition preserves bookmarks");
            Feed(c,BookmarkList());Check(w.Bookmarks.Items.Length==0,"empty login list replaces data");
        }
        using(var c=Seed()){
            Feed(c,BookmarkList(one,two));int sends=0;byte[]? wire=null;
            Sender(c,(bytes,valid,admit)=>{if(!valid())return Task.FromResult(false);wire=bytes;sends++;admit();return Task.FromResult(true);});
            var view=c.BookmarkSnapshot();Check(await c.SendBookmark(view,"新地點"),"add admitted");
            Check(sends==1&&wire!.SequenceEqual(BookmarkPackets.Add("新地點"))&&c.World.Bookmarks.Items.Length==2,"add sends name only and waits for S92");
            Check(!await c.SendBookmark(view,"stale"),"stale revision blocked");
            Check(!await c.SendBookmark(c.BookmarkSnapshot(),"home"),"case insensitive duplicate blocked");
            Check(!await c.SendBookmark(c.BookmarkSnapshot(),deleteId:99),"unknown delete blocked");
            Check(await c.SendBookmark(c.BookmarkSnapshot(),deleteId:one.Id),"delete admitted");
            Check(sends==2&&wire!.SequenceEqual(BookmarkPackets.Delete(one.Name)),"delete encodes server name instead of id");
            Check(c.World.Bookmarks.Items.Single(b=>b.Id==one.Id).DeletePending&&c.World.Bookmarks.TryGet(one.Id)==null,"pending deletion visible but excluded from skill lookup");
            Feed(c,BookmarkOne(one));Check(c.World.Bookmarks.TryGet(one.Id)==null,"late S92 cannot undo deletion");
            Check(!await c.SendBookmark(c.BookmarkSnapshot(),deleteId:one.Id)&&sends==2,"pending delete not resent");
            Feed(c,BookmarkList(one,two));Check(c.World.Bookmarks.TryGet(one.Id)==one,"authoritative list reconciles unacknowledged deletion");
            Set(c,"State",LoginState.CharacterList);Check(!await c.SendBookmark(c.BookmarkSnapshot(),"offline")&&sends==2,"offline cannot send");
        }
        foreach(string mutation in new[]{"map","move","revision","session","dead"}){
            using var c=Seed();Feed(c,BookmarkList(one));int sends=0;
            Sender(c,(bytes,valid,admit)=>{
                switch(mutation){
                    case "map":Feed(c,LoginPackets.Pad([206,4,0,0]));break;
                    case "move":Feed(c,Pack(123,32601));break;
                    case "revision":Feed(c,BookmarkOne(two));break;
                    case "session":Set(c,"generation",c.SessionGeneration+1);break;
                    case "dead":Set(c.World,"OwnDead",true);break;
                }
                if(!valid())return Task.FromResult(false);sends++;admit();return Task.FromResult(true);
            });
            Check(!await c.SendBookmark(c.BookmarkSnapshot(),"guard")&&sends==0,"queued add rejects changed "+mutation);
        }
        using(var c=Seed()){
            Feed(c,BookmarkList(one));var release=new TaskCompletionSource<bool>();int sends=0;
            Sender(c,async(bytes,valid,admit)=>{await release.Task;if(!valid())return false;sends++;admit();return true;});
            var view=c.BookmarkSnapshot();var first=c.SendBookmark(view,"first");
            Check(!await c.SendBookmark(view,"second"),"double click rejected while transport waits");release.SetResult(true);
            Check(await first&&sends==1,"one queued request written");
        }
        using(var c=Seed()){
            Feed(c,BookmarkList(Enumerable.Range(1,50).Select(i=>new Bookmark((uint)i,"name"+i,0,1,2)).ToArray()));
            int sends=0;Sender(c,(bytes,valid,admit)=>{sends++;return Task.FromResult(false);});
            Check(!await c.SendBookmark(c.BookmarkSnapshot(),"full")&&sends==0,"full server capacity blocks add");
        }
        using(var c=Seed()){
            Feed(c,BookmarkList(one));int revision=c.World.Bookmarks.Revision;
            Sender(c,(_,_,_)=>Task.FromResult(false));
            Check(!await c.SendBookmark(c.BookmarkSnapshot(),deleteId:one.Id)&&c.World.Bookmarks.Revision==revision&&c.World.Bookmarks.TryGet(one.Id)==one,"rejected transport does not mark delete");
        }
        // Native controls only, no ClientForm construction, game startup, window or network.
        Exception? uiError=null;var thread=new Thread(()=>{try{
            using var panel=new BookmarkPanel();var view=new BookmarkView(1,2,3,123,true,50,"test",[one,two]);panel.UpdateView(view);
            var list=panel.Controls.OfType<ListBox>().Single();list.SelectedIndex=0;
            Check(panel.Controls.OfType<Label>().Any(l=>l.Text.Contains("32768, 65535")),"UI selected map and coordinates");
            var del=panel.Controls.OfType<Button>().Single(b=>b.Text.Contains("刪除"));Check(del.Enabled,"UI delete enabled for active row");
            panel.UpdateView(view with{Revision=4,Items=[one with{DeletePending=true},two]});
            Check(!del.Enabled&&panel.Controls.OfType<Label>().Any(l=>l.Text.Contains("待重新登入")),"UI pending delete disabled and labeled");
            panel.UpdateView(view with{Session=2,ActorId=456,Items=[]});Check(list.Items.Count==0&&list.SelectedItem==null,"UI clears old actor selection");
        }catch(Exception ex){uiError=ex;}});thread.SetApartmentState(ApartmentState.STA);thread.Start();thread.Join();if(uiError!=null)throw uiError;
    }
}
