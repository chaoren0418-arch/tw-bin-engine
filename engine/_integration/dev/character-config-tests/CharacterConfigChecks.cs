using P06;
using System.Buffers.Binary;

partial class Checks {
    static byte[] ConfigReply(byte[] data,bool echoClientPadding=true)=>Packet(b=>{
        b.Write((byte)250);b.Write((byte)41);b.Write((uint)data.Length);b.Write(data);
        if(echoClientPadding)b.Write(new byte[(8-((5+data.Length)&7))&7]);
        b.Write((byte)0); // ServerBasePacket.writeByte terminator.
    });
    static void BadConfig(byte[] bytes,string label){
        try{CharacterConfigPackets.Read(bytes);throw new Exception("accepted malformed config: "+label);}
        catch(ProtocolException){Check(true,label);}
    }
    static PlayerHudSnapshot ConfigHudState(LoginConnection c)=>new(PlayerHudActions.Stamp(c),"offline","test",100,100,100,100,"","",false,"",false,[],[]);
    static async Task CharacterConfigChecks(){
        PlayerHudBinding?[] slots=[new(PlayerHudKind.Item,0x12345678),null,new(PlayerHudKind.Magic,4),new(PlayerHudKind.Magic,220)];
        var config=CharacterConfig.FromSlots(slots);
        Check(Convert.ToHexString(config.Data)=="54574343010401785634120000000000020400000002DC000000","TWCC v1 exact fixture");
        byte[] request=CharacterConfigPackets.Save(config);
        Check(request[0]==244&&BinaryPrimitives.ReadUInt32LittleEndian(request.AsSpan(1))==29&&request.Length==32,"C244 opcode D(data length + 3) padded length");
        Check(request.AsSpan(5,26).SequenceEqual(config.Data)&&request[^1]==0,"C244 exact binary body and zero pad");
        foreach(bool echo in new[]{false,true}){
            var read=CharacterConfigPackets.Read(ConfigReply(config.Data,echo));
            Check(read.Editable&&read.Slots!.SequenceEqual(slots),"S250/41 restores all four bindings echo="+echo);
        }
        for(int n=0;n<16;n++){
            byte[] opaque=Enumerable.Range(0,n).Select(i=>(byte)(i%3)).ToArray();
            var encoded=CharacterConfigPackets.Save(new(opaque));
            // Recreate server handler: declared - 3; readByte stores ALL remaining C bytes.
            byte[] response=Packet(b=>{b.Write((byte)250);b.Write((byte)41);b.Write(BinaryPrimitives.ReadUInt32LittleEndian(encoded.AsSpan(1))-3);b.Write(encoded.AsSpan(5));b.Write((byte)0);});
            Check(CharacterConfigPackets.Read(response).Data.SequenceEqual(opaque),"C/S roundtrip zero bytes and pad alignment "+n);
        }
        Check(new CharacterConfig([]).Editable&&new CharacterConfig([]).Slots!.All(x=>x==null),"empty blob clears all slots");
        var copy=config.Data;copy[0]=0;var copySlots=config.Slots!;copySlots[0]=null;
        Check(config.Editable&&config.Data[0]==84&&config.Slots![0]==slots[0],"model copies exposed buffers");
        foreach(int offset in new[]{0,4,5,6}){var unknown=config.Data;unknown[offset]=255;Check(!new CharacterConfig(unknown).Editable,"unknown format preserved offset "+offset);}
        var invalidId=config.Data;Array.Clear(invalidId,7,4);Check(!new CharacterConfig(invalidId).Editable,"zero nonempty binding id rejected");
        var hugeSkill=config.Data;BinaryPrimitives.WriteUInt32LittleEndian(hugeSkill.AsSpan(17),uint.MaxValue);Check(!new CharacterConfig(hugeSkill).Editable,"invalid skill id retained as opaque");
        byte[] valid=ConfigReply(config.Data);
        for(int n=0;n<valid.Length;n++)if(n!=((6+config.Data.Length+1+7)&~7))BadConfig(valid[..n],"truncated S config "+n);
        var bad=valid.ToArray();bad[0]=0;BadConfig(bad,"wrong opcode");bad=valid.ToArray();bad[1]=40;BadConfig(bad,"wrong subcode");
        bad=valid.ToArray();BinaryPrimitives.WriteUInt32LittleEndian(bad.AsSpan(2),uint.MaxValue);BadConfig(bad,"unsigned overflow length");
        bad=valid.ToArray();BinaryPrimitives.WriteUInt32LittleEndian(bad.AsSpan(2),65536);BadConfig(bad,"storage limit");
        bad=valid.ToArray();bad[^1]=1;BadConfig(bad,"nonzero tail");BadConfig([..valid,new byte(),0,0,0,0,0,0,0],"extra block");
        byte[] maximum=new byte[CharacterConfigPackets.MaximumDataLength];
        var maxRequest=CharacterConfigPackets.Save(new(maximum));
        Check(LoginFrames.Wrap(maxRequest).Length<=10240&&LoginFrames.Wrap(ConfigReply(maximum)).Length<=10240,"maximum C and echoed S fit frame limit");
        try{CharacterConfigPackets.Save(new(new byte[maximum.Length+1]));throw new Exception("oversize C accepted");}catch(ArgumentException){Check(true,"oversize C rejected before transport");}
        using(var c=Seed()){
            Feed(c,valid);Check(c.World.ServerCharacterConfig!.Slots!.SequenceEqual(slots),"world dispatch publishes S250/41");
            var original=c.World.ServerCharacterConfig;
            try{Feed(c,bad);}catch(ProtocolException){}
            Check(ReferenceEquals(original,c.World.ServerCharacterConfig),"malformed S cannot partially publish");
            Feed(c,Packet(b=>{b.Write((byte)250);b.Write((byte)40);b.Write((ushort)10);}));
            Check(ReferenceEquals(original,c.World.ServerCharacterConfig),"other S250 status subcode remains dispatched");
            int sends=0;Sender(c,(bytes,current,admit)=>{if(!current())return Task.FromResult(false);sends++;admit();Check(bytes.SequenceEqual(request),"real sender uses C244 encoder");return Task.FromResult(true);});
            Check(await c.SaveCharacterConfig(PlayerHudActions.Stamp(c),config)&&sends==1,"save through existing guarded sender");
            Check(c.CharacterConfigNotice.Contains("沒有儲存確認"),"send is not database acknowledgement");
            Feed(c,ConfigReply([9,0,8]));
            Check(!await c.SaveCharacterConfig(PlayerHudActions.Stamp(c),config)&&sends==1,"opaque legacy settings never overwritten");
            c.World.Reset();Check(c.World.ServerCharacterConfig==null,"reset clears exposed config");
        }
        using(var c=Seed()){
            var stamp=PlayerHudActions.Stamp(c);var release=new TaskCompletionSource();int sends=0;
            Sender(c,async(_,current,_)=>{await release.Task;if(!current())return false;sends++;return true;});
            var pending=c.SaveCharacterConfig(stamp,config);
            Check(!await c.SaveCharacterConfig(stamp,config),"concurrent saves rejected");
            c.Cancel();release.SetResult();Check(!await pending&&sends==0,"cancelled generation cannot send queued config");
        }
        using(var c=Seed()){
            var stamp=PlayerHudActions.Stamp(c);Sender(c,(_,current,_)=>{Feed(c,ConfigReply([]));return Task.FromResult(current());});
            Check(!await c.SaveCharacterConfig(stamp,config),"new server config invalidates queued save");
        }
        using(var c=Seed()){
            var hud=new CharacterConfigHud();var bindings=new PlayerHudSlots();var state=ConfigHudState(c);bindings.Observe(state.Stamp);
            int sends=0;Sender(c,(_,current,_)=>{if(!current())return Task.FromResult(false);sends++;return Task.FromResult(true);});
            Feed(c,valid);hud.Update(c,state,bindings,_=>{});
            Check(bindings.Bindings.SequenceEqual(slots)&&sends==0,"HUD restores without echo save");
            bindings.Assign(0,null);hud.Update(c,state,bindings,_=>{});Check(sends==1,"HUD explicit clear sends one config");
            hud.Update(c,state,bindings,_=>{});Check(sends==1,"HUD redraw does not resave");
            Feed(c,ConfigReply([]));hud.Update(c,state,bindings,_=>{});Check(bindings.Bindings.All(x=>x==null)&&sends==1,"server empty config replaces HUD without save");
            bindings.Assign(0,slots[0]);c.Cancel();Set(c,"State",LoginState.InWorld);Set(c.World,"Entered",true);Set(c.World,"Status",new OwnStatus(123,1,0,0,0,0,0,0,0,100,100,100,100,0,0,0,0,0,0,0,0,0,0));Feed(c,Pack(123));
            state=ConfigHudState(c);bindings.Observe(state.Stamp);hud.Update(c,state,bindings,_=>{});
            Check(bindings.Bindings.All(x=>x==null),"relogin without config does not reuse old local slots");
        }
    }
}
