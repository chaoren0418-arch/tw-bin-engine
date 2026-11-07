using P06;

partial class Checks {
    static byte[] GhostPack(byte flags=0x84,uint id=123,string name="offline",ushort gfx=0,byte weapon=0,byte heading=2)=>Packet(b=>{
        b.Write((byte)87);b.Write((ushort)32600);b.Write((ushort)32940);b.Write(id);b.Write(gfx);
        b.Write(weapon);b.Write(heading);b.Write((byte)7);b.Write((byte)1);b.Write(0x12345678u);b.Write((short)-123);
        Text(b,name);Text(b,"觀戰稱號");b.Write(flags);b.Write(0xabcdef01u);Text(b,"血盟");Text(b,"");
        b.Write((byte)0xb0);b.Write((byte)255);b.Write((byte)8);b.Write((byte)0);b.Write((byte)0);b.Write((byte)255);b.Write((byte)255);
    });
    static LoginConnection GhostSeed(){var c=Seed();Feed(c,GhostPack());return c;}
    static async Task GhostModeChecks(){
        Check(Convert.ToHexString(GhostModePackets.Exit())=="AD00000000000000","C173 empty body with exact 8-byte wire padding");
        Check(Convert.ToHexString(NpcPackets.Action(0x12345678,"ent"))=="7D78563412656E740000000000000000","existing C125 D/S ent entrance encoding");
        using(var c=Seed()){
            Sender(c,(_,_,_)=>throw new Exception("non-ghost sent C173"));
            Check(!c.World.IsGhost&&!await c.ExitGhost(c.GhostModeSnapshot()),"normal actor cannot exit ghost");
            for(int flags=0;flags<256;flags++){
                Feed(c,GhostPack((byte)flags));
                Check(c.World.IsGhost==((flags&128)!=0),"S87 tests only ghost bit: "+flags);
            }
            var a=c.World.Actor!;
            Check(a.X==32600&&a.Y==32940&&a.Gfx==0&&a.Weapon==0&&a.Direction==2&&a.Light==7&&a.Speed==1&&a.Exp==0x12345678&&a.Lawful==-123,"S87 preserves server position/appearance/action values");
            Check(a.Title=="觀戰稱號"&&a.ClanId==0xabcdef01&&a.Clan=="血盟"&&a.Owner==""&&a.Rank==0xb0&&a.HpPercent==255&&a.Brave==8&&a.Level==0&&a.Unknown==0&&a.Tail1==255&&a.Tail2==255,"S87 variable strings and complete tail decoded");
            Feed(c,GhostPack(4));int revision=c.World.GhostRevision;
            Feed(c,GhostPack(0x84,456,"target"));Check(!c.World.IsGhost&&c.World.GhostRevision==revision,"other S87 actor cannot change own ghost state");
            Feed(c,GhostPack(4,weapon:8));Check(c.World.OwnDead&&!c.World.IsGhost,"dead actor is not automatically a ghost");
        }
        using(var c=GhostSeed()){
            int revision=c.World.GhostRevision;
            var packet=GhostPack();
            for(int length=0;length<packet.Length;length++){
                bool rejected=false;try{Feed(c,packet[..length]);}catch(ProtocolException){rejected=true;}
                Check(rejected&&c.World.IsGhost&&c.World.GhostRevision==revision,"truncated S87 is atomic: "+length);
            }
            foreach(var invalid in new[]{GhostPack(4,name:"wrong"),GhostPack(4,heading:8),GhostPack(4).Concat(new byte[16]).ToArray()}){
                bool rejected=false;try{Feed(c,invalid);}catch(ProtocolException){rejected=true;}
                Check(rejected&&c.World.IsGhost&&c.World.GhostRevision==revision,"invalid self S87 cannot clear ghost");
            }
            var badPadding=GhostPack(4).Concat(new byte[8]).ToArray();badPadding[^1]=1;
            bool bad=false;try{Feed(c,badPadding);}catch(ProtocolException){bad=true;}
            Check(bad&&c.World.IsGhost,"nonzero padding cannot clear ghost");
            Feed(c,GhostPack(4).Concat(new byte[8]).ToArray());
            Check(!c.World.IsGhost,"S87 recovery N+8 authoritative clear");
            Feed(c,GhostPack().Concat(new byte[8]).ToArray());Check(c.World.IsGhost,"S87 recovery N+8 authoritative enter");
        }
        using(var c=GhostSeed()){
            int sends=0;byte[]? wire=null;long now=1000;Set(c,"GhostClock",(Func<long>)(()=>now));
            Sender(c,(b,v,a)=>{if(!v())return Task.FromResult(false);sends++;wire=b;a();return Task.FromResult(true);});
            var shown=c.GhostModeSnapshot();var actor=c.World.Actor;
            Check(shown.Active&&shown.CanExit&&await c.ExitGhost(shown),"ghost exit admitted");
            Check(sends==1&&wire!.SequenceEqual(GhostModePackets.Exit()),"sender receives one exact C173");
            Check(ReferenceEquals(actor,c.World.Actor)&&c.World.IsGhost&&c.GhostModeSnapshot().Waiting,"C173 does not predict exit or appearance");
            Check(!await c.ExitGhost(shown)&&sends==1,"repeated click while awaiting response sends nothing");
            Feed(c,GhostPack());Check(c.GhostModeSnapshot().Waiting,"same ghost refresh does not acknowledge exit");
            Feed(c,Packet(b=>{b.Write((byte)71);b.Write((ushort)189);b.Write((byte)0);}));
            Check(c.World.IsGhost&&c.GhostModeSnapshot().Waiting,"unrelated server message does not clear ghost");
            now+=15000;var retry=c.GhostModeSnapshot();
            Check(!retry.Waiting&&retry.CanExit&&retry.Notice.Contains("尚未確認")&&sends==1,"timeout permits explicit retry without automatic send");
            Check(await c.ExitGhost(retry)&&sends==2,"manual retry after timeout");
            Feed(c,GhostPack(4));Check(!c.World.IsGhost&&!c.GhostModeSnapshot().Active,"server clears ghost on own S87");
            Feed(c,GhostPack());Check(!await c.ExitGhost(shown)&&c.GhostModeSnapshot().CanExit,"old ghost-cycle view rejected after exit and reentry");
        }
        foreach(bool queued in new[]{false,true})foreach(string change in new[]{"session","world","clear","cycle","map","reset","teleport"}){
            using var c=GhostSeed();var shown=c.GhostModeSnapshot();int sends=0;
            void Mutate(){switch(change){
                case "session":Set(c,"generation",c.SessionGeneration+1);break;
                case "world":Set(c.World,"WorldEpoch",c.World.WorldEpoch+1);break;
                case "clear":Feed(c,GhostPack(4));break;
                case "cycle":Feed(c,GhostPack(4));Feed(c,GhostPack());break;
                case "map":Feed(c,LoginPackets.Pad([206,4,0,0]));break;
                case "reset":c.World.Reset();break;
                case "teleport":Feed(c,Packet(b=>{b.Write((byte)149);b.Write((ushort)1);b.Write(0u);}));break;
            }}
            Sender(c,(b,v,a)=>{if(queued)Mutate();if(!v())return Task.FromResult(false);sends++;a();return Task.FromResult(true);});
            if(!queued)Mutate();
            Check(!await c.ExitGhost(shown)&&sends==0,"stale/transition exit rejected before socket: "+queued+" "+change);
        }
        using(var c=GhostSeed()){
            var release=new TaskCompletionSource<bool>();int sends=0;
            Sender(c,async(b,v,a)=>{await release.Task;if(!v())return false;sends++;a();return true;});
            var shown=c.GhostModeSnapshot();var pending=c.ExitGhost(shown);
            Check(!await c.ExitGhost(shown)&&c.GhostModeSnapshot().Waiting,"queued exit excludes concurrent clicks");
            release.SetResult(true);Check(await pending&&sends==1,"one queued request is sent");
        }
        using(var c=GhostSeed()){
            Sender(c,(b,v,a)=>{if(!v())return Task.FromResult(false);a();Feed(c,GhostPack(4));return Task.FromResult(true);});
            Check(await c.ExitGhost(c.GhostModeSnapshot())&&!c.World.IsGhost&&!c.GhostModeSnapshot().Waiting,"authoritative exit arriving before send completion is retained");
        }
        using(var c=GhostSeed()){
            var release=new TaskCompletionSource<bool>();
            Sender(c,async(b,v,a)=>{await release.Task;throw new IOException("old transport");});
            var old=c.ExitGhost(c.GhostModeSnapshot());Set(c,"generation",c.SessionGeneration+1);
            Sender(c,(b,v,a)=>{if(!v())return Task.FromResult(false);a();return Task.FromResult(true);});
            Check(await c.ExitGhost(c.GhostModeSnapshot()),"new session not blocked by old queued request");
            release.SetResult(true);Check(!await old&&c.State==LoginState.InWorld&&c.GhostModeSnapshot().Waiting,"old failure cannot cancel or unlock newer request");
        }
        using(var c=GhostSeed()){
            Sender(c,(_,_,_)=>throw new IOException("transport failed"));
            Check(!await c.ExitGhost(c.GhostModeSnapshot())&&c.State==LoginState.Faulted&&!c.World.IsGhost,"current transport failure cancels safely without auto retry");
        }
        using(var c=GhostSeed()){
            Sender(c,(b,v,a)=>{if(!v())return Task.FromResult(false);a();return Task.FromResult(true);});
            Feed(c,GhostPack(gfx:65535,weapon:17));Set(c.World,"CacheEnabled",true);
            Check(!c.World.Renderable&&c.GhostModeSnapshot().CanExit&&await c.ExitGhost(c.GhostModeSnapshot()),"unsupported map/gfx cannot trap ghost exit");
            Check(c.World.Actor!.Gfx==65535&&c.World.Actor.Weapon==17,"ghost never substitutes an invented appearance");
        }
        using(var c=GhostSeed()){
            var form=KeyboardForm(c);Set(form,"normalProduct",true);
            var view=(GhostModeView)typeof(ClientForm).GetMethod("ReadGhostMode",F)!.Invoke(form,null)!;
            Check(view.Active&&view.Notice.Contains("幽靈模式"),"native HUD reads current ghost state through module hook");
            c.Cancel();Check(typeof(ClientForm).GetMethod("ReadGhostMode",F)!.Invoke(form,null)==null,"native HUD hides ghost after cancellation");
        }
    }
}
