using System.Text.Json;
namespace P06;

// T231（#549）：聊天頻道開關的離線測試（合成封包，不連遊戲、網路或素材）。由 --module-tests 執行。
static class ChatChannelToggleTests {
    static byte[] P(params byte[] b)=>LoginPackets.Pad(b);
    static JsonElement Channels(WorldSession s)=>WorldModuleTestContext.SnapshotJson(s).GetProperty("extensions").GetProperty("chatChannels");

    [WorldModuleTest("chat-channel-toggle")]
    static void Run(WorldModuleTestContext t){
        var all=ChatChannelSettings.Default;
        var none=new ChatChannelSettings(false,false,false);

        // 1. C26 單一頻道：type 0 全體、2 密語、6 買賣；button 0 關、1 開；補齊 8 byte。
        t.Check(ChatChannelPackets.Toggle(ChatChannelKind.World,false).SequenceEqual(P(26,0,0)),"C26 world off");
        t.Check(ChatChannelPackets.Toggle(ChatChannelKind.World,true).SequenceEqual(P(26,0,1)),"C26 world on");
        t.Check(ChatChannelPackets.Toggle(ChatChannelKind.Whisper,false).SequenceEqual(P(26,2,0)),"C26 whisper off");
        t.Check(ChatChannelPackets.Toggle(ChatChannelKind.Trade,true).SequenceEqual(P(26,6,1)),"C26 trade on");
        t.Equal(8,ChatChannelPackets.Toggle(ChatChannelKind.Trade,true).Length,"C26 padded to 8");
        t.Throws<ArgumentOutOfRangeException>(()=>ChatChannelPackets.Toggle((ChatChannelKind)9,true),"type 9 not sent (3.81 ignores)");

        // 2. 255 合併位元：90／91／94／95。
        t.Equal((byte)95,ChatChannelPackets.MergedButton(all),"merged all on = 95");
        t.Equal((byte)91,ChatChannelPackets.MergedButton(new(true,false,true)),"world on whisper off = 91");
        t.Equal((byte)94,ChatChannelPackets.MergedButton(new(false,true,true)),"world off whisper on = 94");
        t.Equal((byte)90,ChatChannelPackets.MergedButton(none),"both off = 90");
        t.Check(ChatChannelPackets.Merged(new(false,true,false)).SequenceEqual(P(26,255,94)),"C26/255 bytes");
        var sync=ChatChannelPackets.Sync(new(true,false,false));
        t.Equal(2,sync.Length,"sync sends 255 then 6");
        t.Check(sync[0].SequenceEqual(P(26,255,91))&&sync[1].SequenceEqual(P(26,6,0)),"sync bytes in original order");

        // 3. 以 3.81 伺服器語義套用封包，所有組合都能還原原設定。
        foreach(bool w in new[]{false,true})foreach(bool x in new[]{false,true})foreach(bool tr in new[]{false,true}){
            var want=new ChatChannelSettings(w,x,tr);var got=ChatChannelSettings.Default;var other=none;
            foreach(var packet in ChatChannelPackets.Sync(want)){
                t.Check(ChatChannelPackets.TryDecode(packet,out byte type,out byte button),"decode sync packet");
                got=ChatChannelPackets.Apply(got,type,button);other=ChatChannelPackets.Apply(other,type,button);
            }
            t.Equal(want,got,$"sync from default → {want}");
            t.Equal(want,other,$"sync from all-off → {want}");
        }
        t.Equal(new ChatChannelSettings(false,true,true),ChatChannelPackets.Apply(all,255,126),"126 = 94 + 0x20");
        t.Equal(all,ChatChannelPackets.Apply(all,255,92),"92 ignored by server");
        t.Equal(all,ChatChannelPackets.Apply(all,255,0),"255/0 ignored");
        t.Equal(all,ChatChannelPackets.Apply(all,0,2),"button 2 ignored");
        t.Equal(all,ChatChannelPackets.Apply(all,9,0),"type 9 ignored");
        t.Equal(new ChatChannelSettings(true,true,false),ChatChannelPackets.Apply(all,6,0),"6/0 trade off");
        t.Check(!ChatChannelPackets.TryDecode(P(136,0,0),out _,out _),"not C26");

        // 4. 聊天列指令：R6-E（#781）改成原版語法，細項在 ChatCommandFollowupTests；舊的 `/on 頻道` 已移除。
        t.Equal(new ChatChannelCommand(2,false),ChatChannelCommands.Parse("/whisper off"),"/whisper off");
        t.Equal(null,ChatChannelCommands.Parse("/off 全體"),"/off 全體 no longer a command");
        t.Equal(null,ChatChannelCommands.Parse("/say /whisper on"),"/say not a command");
        t.Equal("全體聊天：關",ChatChannelCommands.Describe(ChatChannelKind.World,false),"describe uses 開/關");

        // 5. 保存位元。
        t.Equal((byte)0x07,all.Flags,"all flags");
        t.Equal((byte)0x05,new ChatChannelSettings(true,false,true).Flags,"world+trade flags");
        t.Equal(null,ChatChannelSettings.FromFlags(0x08),"unknown flag rejected");
        t.Equal(none,ChatChannelSettings.FromFlags(0),"zero flags");

        // 6. TWCC：沒設過頻道時位元組與 v1 完全相同；設過後為 v2，快捷列不變。
        var slots=new PlayerHudBinding?[CharacterConfig.SlotCount];
        slots[0]=new(PlayerHudKind.Item,0x1234_5678);slots[7]=new(PlayerHudKind.Magic,42);
        var v1=CharacterConfig.FromSlots(slots);
        t.Equal(CharacterConfig.EncodedLength(8),v1.Data.Length,"v1 length unchanged");
        t.Equal((byte)1,v1.Data[4],"v1 version byte");
        t.Equal(null,v1.Channels,"v1 has no channels");
        var v2=v1.WithChannels(new(false,true,false));
        t.Equal(CharacterConfig.EncodedLength(8)+1,v2.Data.Length,"v2 length");
        t.Equal((byte)2,v2.Data[4],"v2 version byte");
        t.Check(v1.Data.AsSpan(5,41).SequenceEqual(v2.Data.AsSpan(5,41)),"v2 keeps slot bytes");
        var back=new CharacterConfig(v2.Data);
        t.Check(back.Editable,"v2 parses editable");
        t.Equal<ChatChannelSettings?>(new ChatChannelSettings(false,true,false),back.Channels,"v2 channels round-trip");
        t.Check(back.Slots!.SequenceEqual(slots),"v2 slots round-trip");
        t.Check(CharacterConfig.FromSlots(slots,all).Data.SequenceEqual(back.WithChannels(all).Data),"WithChannels = FromSlots");
        byte[] bad=v2.Data;bad[^1]=0x80;
        t.Check(!new CharacterConfig(bad).Editable,"v2 unknown flag rejected");
        t.Equal(null,new CharacterConfig(bad).Channels,"rejected config exposes no channels");
        byte[] truncated=v2.Data[..^1];truncated[4]=2;
        t.Check(!new CharacterConfig(truncated).Editable,"v2 without flags byte rejected");
        byte[] legacy=new byte[CharacterConfig.EncodedLength(4)];"TWCC"u8.CopyTo(legacy);legacy[4]=1;legacy[5]=4;
        t.Check(new CharacterConfig(legacy).Editable,"legacy v1 count 4 still accepted");
        byte[] legacyV2=[..legacy,0x07];legacyV2[4]=2;
        t.Check(!new CharacterConfig(legacyV2).Editable,"v2 requires eight slots");
        t.Check(new CharacterConfig([]).Channels==null&&new CharacterConfig([]).Editable,"empty server blob = no channels");
        t.Throws<InvalidOperationException>(()=>new CharacterConfig("XXXX"u8).WithChannels(all),"legacy blob not overwritten");
        t.Check(CharacterConfigPackets.Save(v2).Length%8==0,"C244 save of v2 padded");

        // 7. 擴充點註冊與快照。
        var reg=WorldExtensionRegistry.Default;
        t.Check(reg.Errors.Count==0,"default registry has no errors",reg.Errors);
        t.Check(reg.Entries.Any(e=>e.Kind=="reset"&&e.Method=="WorldSession.ResetChatChannels"),"reset registered");
        t.Check(reg.Entries.Any(e=>e.Kind=="snapshot"&&e.Key=="chatChannels"),"snapshot registered");
        var s=new WorldSession();
        var snap=Channels(s);
        t.Check(snap.GetProperty("world").GetBoolean()&&snap.GetProperty("whisper").GetBoolean()&&snap.GetProperty("trade").GetBoolean(),"fresh session = all on");
        t.Check(!snap.GetProperty("stored").GetBoolean(),"fresh session not stored");
        t.Equal(null,s.PersistedChatChannels,"not entered: nothing persisted");
        s.Reset();
        t.Equal(all,s.ChatChannels,"reset keeps default");
    }
}
