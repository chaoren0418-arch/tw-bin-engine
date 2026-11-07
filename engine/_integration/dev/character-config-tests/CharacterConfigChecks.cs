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
        // T176: eight slots F5–F12 (count byte 8, 46 bytes).
        PlayerHudBinding?[] slots=[new(PlayerHudKind.Item,0x12345678),null,new(PlayerHudKind.Magic,4),new(PlayerHudKind.Magic,220),null,new(PlayerHudKind.Item,1),null,new(PlayerHudKind.Magic,int.MaxValue)];
        var config=CharacterConfig.FromSlots(slots);
        Check(Convert.ToHexString(config.Data)=="54574343"+"01"+"08"+"0178563412"+"0000000000"+"0204000000"+"02DC000000"+"0000000000"+"0101000000"+"0000000000"+"02FFFFFF7F","TWCC v1 eight-slot exact fixture");
        byte[] request=CharacterConfigPackets.Save(config);
        Check(request[0]==244&&BinaryPrimitives.ReadUInt32LittleEndian(request.AsSpan(1))==49&&request.Length==56,"C244 opcode D(data length + 3) padded length");
        Check(request.AsSpan(5,46).SequenceEqual(config.Data)&&request.AsSpan(51).ToArray().All(x=>x==0),"C244 exact binary body and zero pad");
        foreach(bool echo in new[]{false,true}){
            var read=CharacterConfigPackets.Read(ConfigReply(config.Data,echo));
            Check(read.Editable&&read.Slots!.SequenceEqual(slots),"S250/41 restores all eight bindings echo="+echo);
        }
        try{CharacterConfig.FromSlots(slots[..4]);throw new Exception("four-slot save accepted");}catch(ArgumentException){Check(true,"saving requires all eight slots");}
        // T138 blobs (count 4, 26 bytes) still restore into F5–F8 and are re-saved as eight slots.
        byte[] legacy=Convert.FromHexString("54574343010401785634120000000000020400000002DC000000");
        foreach(bool echo in new[]{false,true}){
            var old=CharacterConfigPackets.Read(ConfigReply(legacy,echo));
            Check(old.Editable&&old.Slots!.Length==8&&old.Slots.Take(4).SequenceEqual(slots.Take(4))&&old.Slots.Skip(4).All(x=>x==null),"S250/41 legacy four-slot blob restores F5–F8 echo="+echo);
            Check(CharacterConfig.FromSlots(old.Slots!).Data.Length==46,"legacy blob re-encodes as eight slots");
        }
        foreach(var (count,length) in new[]{(4,46),(8,26),(5,31),(0,6),(9,51)}){
            var mismatch=new byte[length];"TWCC"u8.CopyTo(mismatch);mismatch[4]=1;mismatch[5]=(byte)count;
            Check(!new CharacterConfig(mismatch).Editable,$"count {count} with length {length} kept opaque");
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
        var hugeLast=config.Data;hugeLast[^1]=0x80;Check(!new CharacterConfig(hugeLast).Editable,"invalid F12 skill id retained as opaque");
        var badKind=config.Data;badKind[6+7*5]=3;Check(!new CharacterConfig(badKind).Editable,"unknown F12 kind retained as opaque");
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
