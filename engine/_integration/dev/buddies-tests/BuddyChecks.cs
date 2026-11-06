using P06;

partial class Checks {
    static byte[] BuddyReply(uint owner=123,string all="Alice 中文 ",string online="ALICE ",ushort flag=2,ushort count=2)=>Packet(b=>{
        b.Write((byte)39);b.Write(owner);Text(b,"buddy");b.Write(flag);b.Write(count);Text(b,all);Text(b,online);
    });
    static byte[] ExcludeReply(byte kind,params string[] names)=>Packet(b=>{
        b.Write((byte)250);b.Write(kind);if(kind==17)b.Write((byte)names.Length);foreach(var n in names)Text(b,n);
    });
    static byte[] NormalChat(string text,uint speaker=456)=>Packet(b=>{b.Write((byte)81);b.Write((byte)0);b.Write(speaker);Text(b,text);});
    static byte[] Whisper(string name,string text)=>Packet(b=>{b.Write((byte)73);Text(b,name);Text(b,text);});
    static void BuddyBad(Action action,string label){bool failed=false;try{action();}catch(Exception ex) when(ex is ProtocolException or ArgumentException or System.Text.DecoderFallbackException){failed=true;}Check(failed,label);}
    static async Task BuddyChecks(bool codecOnly=false){
        Check(Convert.ToHexString(BuddyPackets.Encode(BuddyAction.Query))=="0400000000000000","C4 query has no fields");
        foreach(var (action,hex) in new[]{(BuddyAction.Add,"CF"),(BuddyAction.Delete,"CA"),(BuddyAction.ToggleExclude,"AB")}){
            Check(Convert.ToHexString(BuddyPackets.Encode(action,"Alice"))==hex+"416C6963650000","named C packet "+action);
            Check(Convert.ToHexString(BuddyPackets.Encode(action,"中文"))==hex+"A4A4A4E5000000","Big5 C packet "+action);
            foreach(var name in new[]{""," ","A B","A\0B","A\nB","😀",new string('x',53)})BuddyBad(()=>BuddyPackets.Encode(action,name),"bad name "+action+" "+name.Length);
        }
        BuddyBad(()=>BuddyPackets.Encode((BuddyAction)99,"Alice"),"unknown action");
        BuddyBad(()=>BuddyPackets.Encode(BuddyAction.Query,"Alice"),"query rejects name");
        Check(BuddyPackets.TryBuddy(BuddyReply(),out var parsed)&&parsed!.Owner==123&&parsed.Entries.Length==2&&parsed.Entries[0].Online&&!parsed.Entries[1].Online,"S39 flag2 count2 two space-delimited lists");
        Check(BuddyPackets.TryBuddy(BuddyReply(all:"",online:""),out parsed)&&parsed!.Entries.Length==0,"empty buddy list");
        Check(BuddyPackets.TryBuddy(BuddyReply(all:"Alice alice  中文 ",online:"中文 中文 "),out parsed)&&parsed!.Entries.Length==2&&parsed.Entries[1].Online,"deduplicate names and online variants");
        Check(!BuddyPackets.TryBuddy(Packet(b=>{b.Write((byte)39);b.Write(123u);Text(b,"RaiseAttr");}),out _),"other S39 remains unclaimed");
        Check(!BuddyPackets.TryExclude(LoginPackets.Pad([250,154]),out _),"unrelated PacketBox remains unclaimed");
        foreach(byte kind in new byte[]{17,18,19}){
            Check(BuddyPackets.TryExclude(ExcludeReply(kind,"中文"),out var block)&&block!.Kind==kind&&block.Names.SequenceEqual(new[]{"中文"}),"S250 valid Big5 response "+kind);
        }
        Check(BuddyPackets.TryExclude(ExcludeReply(17),out var empty)&&empty!.Names.Length==0,"S250 empty snapshot codec");
        var fullNames=Enumerable.Range(0,16).Select(i=>"n"+i).ToArray();
        Check(BuddyPackets.TryExclude(ExcludeReply(17,fullNames),out var full)&&full!.Names.SequenceEqual(fullNames),"S250 accepts server limit of 16");
        Check(BuddyPackets.TryExclude(ExcludeReply(17,"Alice","ALICE"),out var duplicate)&&duplicate!.Names.SequenceEqual(new[]{"Alice"}),"S250 snapshot deduplicates names");
        BuddyBad(()=>BuddyPackets.TryBuddy(BuddyReply(flag:1),out _),"reject wrong buddy flag");
        BuddyBad(()=>BuddyPackets.TryBuddy(BuddyReply(count:1),out _),"reject wrong buddy count");
        BuddyBad(()=>BuddyPackets.TryBuddy(BuddyReply(owner:0),out _),"reject zero owner");
        BuddyBad(()=>BuddyPackets.TryExclude(ExcludeReply(18,""),out _),"reject empty exclude name");
        BuddyBad(()=>BuddyPackets.TryExclude(ExcludeReply(17,Enumerable.Range(0,17).Select(i=>"n"+i).ToArray()),out _),"reject oversized exclude list");
        foreach(var packet in new[]{BuddyReply(),ExcludeReply(17,"Alice","中文"),ExcludeReply(18,"Alice"),ExcludeReply(19,"Alice")}){
            for(int length=1;length<packet.Length;length++){
                if(packet[0]==250&&length<2)continue;
                byte[] shortPacket=packet[..length];BuddyBad(()=>{if(shortPacket[0]==39)BuddyPackets.TryBuddy(shortPacket,out _);else BuddyPackets.TryExclude(shortPacket,out _);},"reject truncated S packet "+packet[0]+"/"+length);
            }
            var extra=packet.Concat(new byte[8]).ToArray();BuddyBad(()=>{if(extra[0]==39)BuddyPackets.TryBuddy(extra,out _);else BuddyPackets.TryExclude(extra,out _);},"reject trailing block "+packet[0]);
        }
        if(codecOnly)return;
        using(var c=Seed()){
            Feed(c,BuddyReply());Check(c.World.Buddies.Length==2&&c.World.BuddyLoaded,"real WorldSession receives S_Buddy");
            Check(c.World.InteractionKind=="closed","buddy does not open or change NPC dialog");
            var stale=c.BuddySnapshot();int rev=stale.Revision;
            BuddyBad(()=>Feed(c,BuddyReply(owner:999)),"foreign owner rejected");Check(c.World.BuddyRevision==rev,"bad owner leaves state unchanged");
            Feed(c,ExcludeReply(17,"Alice","中文"));Check(c.World.IsExcluded("aLiCe")&&c.World.ExcludedNames.Length==2,"S250/17 snapshot and case-insensitive lookup");
            Feed(c,ExcludeReply(18,"ALICE"));Check(c.World.ExcludedNames.Length==2,"duplicate S250/18 idempotent");
            int beforeBad=c.World.BuddyRevision;
            BuddyBad(()=>Feed(c,ExcludeReply(17,"Bob","").Concat(new byte[8]).ToArray()),"bad snapshot rejected atomically");
            Check(c.World.BuddyRevision==beforeBad&&c.World.IsExcluded("Alice")&&!c.World.IsExcluded("Bob"),"malformed snapshot preserves prior state");
            Feed(c,NormalChat("Alice: hidden"));Feed(c,NormalChat("ALICE: invisible",0));Feed(c,Whisper("alice","hidden"));
            Check(c.World.PlayerChatLines.Length==0,"blocked normal visible/invisible and whisper absent");
            Feed(c,NormalChat("Alice2: visible"));Feed(c,Whisper("Bob","Alice: this is only message text"));Feed(c,NormalChat("Bob: Alice: quoted"));
            Check(c.World.PlayerChatLines.Length==3,"exact sender match, no body substring filtering");
            Feed(c,Packet(b=>{b.Write((byte)243);b.Write((byte)9);Text(b,"-> (Alice) outgoing");}));
            Check(c.World.PlayerChatLines.Length==4,"outgoing whisper echo is not hidden");
            Feed(c,ExcludeReply(19,"alice"));Feed(c,NormalChat("Alice: visible"));Feed(c,Whisper("Alice","visible"));
            Check(c.World.PlayerChatLines.Length==6&&!c.World.IsExcluded("Alice"),"S250/19 restores delivery");
            Feed(c,ExcludeReply(17));Check(c.World.ExcludedNames.Length==0,"empty snapshot replaces list");
            Sender(c,(_,_,_)=>throw new Exception("stale request reached sender"));Check(!await c.SendBuddy(BuddyAction.Add,"Bob",stale),"stale UI revision rejected");
            Feed(c,ExcludeReply(18,"Alice"));Feed(c,LoginPackets.Pad([206,0,0,0]));Feed(c,Pack(123));
            Check(c.World.IsExcluded("Alice")&&c.World.BuddyLoaded,"map transfer preserves lists");
            c.World.Reset();Check(!c.World.BuddyLoaded&&c.World.Buddies.Length==0&&c.World.ExcludedNames.Length==0,"world reset clears both lists");
        }
        using(var c=Seed()){
            var sent=new List<byte[]>();Sender(c,(bytes,current,admitted)=>{if(!current())return Task.FromResult(false);sent.Add(bytes);admitted();return Task.FromResult(true);});
            foreach(var action in new[]{BuddyAction.Query,BuddyAction.Add,BuddyAction.Delete}){
                Check(await c.SendBuddy(action,action==BuddyAction.Query?"":"Alice",c.BuddySnapshot()),"real sender "+action);
                Check(c.World.Buddies.Length==0,"no optimistic buddy mutation "+action);
            }
            Check(sent.Select(p=>p[0]).SequenceEqual(new byte[]{4,207,202})&&c.World.BuddyStale,"one packet per explicit action, no racing auto-query");
            Check(await c.SendBuddy(BuddyAction.ToggleExclude,"Alice",c.BuddySnapshot()),"send C171");
            Check(!c.World.IsExcluded("Alice")&&!c.BuddySnapshot().CanToggle,"exclude waits for authoritative ack");
            Check(!await c.SendBuddy(BuddyAction.ToggleExclude,"Alice",c.BuddySnapshot())&&sent.Count==4,"pending toggle cannot double-submit");
            Feed(c,ExcludeReply(18,"Bob"));Check(!c.BuddySnapshot().CanToggle,"unrelated ack does not release pending toggle");
            Feed(c,ExcludeReply(18,"Alice"));Check(c.BuddySnapshot().CanToggle&&c.World.IsExcluded("alice"),"matching ack releases pending toggle");
            await c.SendBuddy(BuddyAction.ToggleExclude,"Alice",c.BuddySnapshot());
            Feed(c,Packet(b=>{b.Write((byte)71);b.Write((ushort)472);b.Write((byte)0);}));
            Check(c.BuddySnapshot().CanToggle&&c.World.IsExcluded("Alice")&&c.World.LastServerReply?.Id==472,"S71/472 refusal preserves list and normal message dispatch");
            await c.SendBuddy(BuddyAction.ToggleExclude,"Bob",c.BuddySnapshot());
            Feed(c,Packet(b=>{b.Write((byte)71);b.Write((ushort)109);b.Write((byte)0);}));
            Check(!c.BuddySnapshot().CanToggle&&c.World.LastServerReply?.Id==109&&c.World.BuddyNotice.Contains("沒有這個角色"),"S71/109 remains visible and cannot acknowledge an exclude toggle");
            var malformed=Packet(b=>{b.Write((byte)71);b.Write((ushort)472);b.Write((byte)0);});malformed[^1]=1;
            BuddyBad(()=>Feed(c,malformed),"malformed refusal rejected");
            Check(c.World.ExcludePending,"malformed refusal cannot release pending toggle");
            Feed(c,ExcludeReply(17,"Alice"));Check(c.BuddySnapshot().CanToggle,"authoritative snapshot releases pending toggle");
            Feed(c,BuddyReply());Check(!c.World.BuddyStale,"query response confirms new snapshot");
            using var panel=new BuddyPanel(c);Check(panel.AccessibleName=="好友與封鎖名單"&&panel.Controls.Count>0,"native buddy panel binds without network or window");
        }
        using(var c=Seed()){
            Feed(c,BuddyReply());Feed(c,ExcludeReply(18,"Alice"));
            var release=new TaskCompletionSource<bool>();int sends=0;
            Sender(c,async(_,valid,admitted)=>{await release.Task;if(!valid())return false;sends++;admitted();return true;});
            var pending=c.SendBuddy(BuddyAction.Add,"Alice",c.BuddySnapshot());
            Check(!await c.SendBuddy(BuddyAction.Add,"Alice",c.BuddySnapshot()),"in-flight send blocks duplicate");
            c.Cancel();release.SetResult(true);Check(!await pending&&sends==0,"cancel invalidates queued request");
            Check(c.World.ExcludedNames.Length==0&&c.World.Buddies.Length==0,"disconnect has no previous character lists");
        }
        using(var c=Seed()){
            int sends=0;Sender(c,(_,valid,admitted)=>{Feed(c,LoginPackets.Pad([206,0,0,0]));Feed(c,Pack(123));if(!valid())return Task.FromResult(false);sends++;admitted();return Task.FromResult(true);});
            Check(!await c.SendBuddy(BuddyAction.ToggleExclude,"Alice",c.BuddySnapshot())&&sends==0,"map change invalidates queued toggle");
        }
    }
}
