using P06;
using System.Buffers.Binary;

partial class Checks {
    const uint TransferItem=0x12345678;
    static void TransferInventory(LoginConnection c,uint count=10,byte use=0,string name="測試物品")=>Feed(c,Packet(b=>{
        b.Write((byte)15);b.Write(TransferItem);b.Write((ushort)1234);b.Write(use);b.Write((byte)0);
        b.Write((ushort)12);b.Write((byte)1);b.Write(count);b.Write((byte)0);Text(b,name);b.Write((byte)0);b.Write(new byte[11]);
    }));
    static LoginConnection TransferSeed(){var c=Seed();TransferInventory(c);c.World.SelectItem(TransferItem);return c;}
    static ItemTransferRequest TransferRequest(LoginConnection c,bool delete=false)=>c.PrepareItemTransfer(c.CaptureItemTransfer(),delete?null:456,"3")??throw new Exception("prepare transfer");
    static byte[] TransferAmount(uint count)=>Packet(b=>{b.Write((byte)24);b.Write(TransferItem);Text(b,"測試物品");b.Write(count);b.Write((byte)0);});
    static async Task ItemTransferChecks(){
        Check(Convert.ToHexString(ItemTransferPackets.Delete(TransferItem))=="8A78563412000000","C138 D little endian, 8-byte padding, no quantity");
        Check(Convert.ToHexString(ItemTransferPackets.Give(0x89abcdef,0x1234,0x5678,TransferItem,0x1020304))=="2DEFCDAB8934127856785634120403020100000000000000","C45 DHHDD exact field order and 24-byte padding");
        foreach(var action in new Action[]{()=>ItemTransferPackets.Delete(0),()=>ItemTransferPackets.Give(0,1,1,1,1),()=>ItemTransferPackets.Give(1,1,1,0,1),()=>ItemTransferPackets.Give(1,1,1,1,0),()=>ItemTransferPackets.Give(1,1,1,1,0x80000000)}){
            bool threw=false;try{action();}catch(ArgumentOutOfRangeException){threw=true;}Check(threw,"encoder rejects invalid identity/count");
        }
        Check(BinaryPrimitives.ReadUInt32LittleEndian(ItemTransferPackets.Give(1,0,ushort.MaxValue,1,int.MaxValue).AsSpan(13))==int.MaxValue,"C45 accepts signed count maximum");
        foreach(bool delete in new[]{true,false}){
            using var c=TransferSeed();int sends=0;byte[]? wire=null;var original=c.World.SelectedItem;
            Sender(c,(b,v,a)=>{if(!v())return Task.FromResult(false);sends++;wire=b;a();return Task.FromResult(true);});
            var cancelled=TransferRequest(c,delete);
            Check(!await c.SendItemTransfer(cancelled,false)&&!await c.SendItemTransfer(cancelled,true)&&sends==0,"cancel consumes confirmation without sending "+delete);
            var request=TransferRequest(c,delete);
            Check(await c.SendItemTransfer(request,true)&&sends==1,"confirmed transfer sends exactly once "+delete);
            Check(wire!.SequenceEqual(delete?ItemTransferPackets.Delete(TransferItem):ItemTransferPackets.Give(456,32601,32940,TransferItem,3)),"transport receives expected wire "+delete);
            Check(ReferenceEquals(original,c.World.SelectedItem)&&c.World.SelectedItem!.Count==10,"send does not predict inventory mutation "+delete);
            Check(!await c.SendItemTransfer(request,true)&&c.PrepareItemTransfer(c.CaptureItemTransfer(),null,"")==null&&sends==1,"duplicate confirmation and click cooldown blocked "+delete);
            if(delete){Feed(c,Packet(b=>{b.Write((byte)57);b.Write(TransferItem);}));Check(c.World.SelectedItem==null&&!c.World.Inventory.Any(),"S57 D authoritatively removes stack and selection");}
            else{Feed(c,TransferAmount(7));Check(c.World.SelectedItem?.Count==7,"S24 DSDC amount response authoritative");}
        }
        foreach(string invalid in new[]{"","0","-1","11","2147483648","1.5","abc"}){
            using var c=TransferSeed();Check(c.PrepareItemTransfer(c.CaptureItemTransfer(),456,invalid)==null&&c.World.ItemNotice.Contains("數量不合法"),"shared quantity validation "+invalid);
        }
        using(var c=TransferSeed()){
            Check(c.PrepareItemTransfer(c.CaptureItemTransfer(),null,"invalid")?.Count==10,"delete is whole stack independent of quantity input");
            Check(!c.World.SelectedItem!.SupportedUse&&c.PrepareItemTransfer(c.CaptureItemTransfer(),456,"10")!=null,"transfer is independent of SupportedUse and drop catalog");
            foreach(uint target in new uint[]{0,123,999})Check(c.PrepareItemTransfer(c.CaptureItemTransfer(),target,"1")==null,"missing/self target rejected "+target);
            Feed(c,Pack(456,32603));Check(c.PrepareItemTransfer(c.CaptureItemTransfer(),456,"1")==null,"target beyond 2 tiles rejected");
            Feed(c,Pack(456,32602));Check(c.PrepareItemTransfer(c.CaptureItemTransfer(),456,"1")!=null,"target at 2 tiles accepted");
            Feed(c,Pack(456,32601,32940,8));Check(c.PrepareItemTransfer(c.CaptureItemTransfer(),456,"1")==null,"dead target rejected");
        }
        foreach(bool queued in new[]{false,true})foreach(string change in new[]{"count","selection","target-move","target-remove","target-replace","actor-move","world","session","equipment","dead","lock","input","visibility"}){
            using var c=TransferSeed();var request=TransferRequest(c);int sends=0;bool visible=true;
            void Mutate(){switch(change){
                case "count":Feed(c,TransferAmount(9));break;
                case "selection":c.World.SelectItem(TransferItem);break;
                case "target-move":Feed(c,Move(456,32601,32940,2));break;
                case "target-remove":Feed(c,Packet(b=>{b.Write((byte)120);b.Write(456u);}));break;
                case "target-replace":Feed(c,Packet(b=>{b.Write((byte)120);b.Write(456u);}));Feed(c,Pack(456,32601));break;
                case "actor-move":Feed(c,Move(123,32600,32940,2));break;
                case "world":Set(c.World,"WorldEpoch",c.World.WorldEpoch+1);break;
                case "session":Set(c,"generation",c.SessionGeneration+1);break;
                case "equipment":Set(c.World,"EquipmentRevision",c.World.EquipmentRevision+1);break;
                case "dead":Set(c.World,"Status",c.World.Status! with{Hp=0});break;
                case "lock":Feed(c,LoginPackets.Pad([202,2]));break;
                case "input":c.CancelMovementInput();break;
                case "visibility":visible=false;break;
            }}
            // Input cancellation is a queued-send fence; confirmation itself captures the current input version.
            if(!queued&&change=="input")continue;
            Sender(c,(b,v,a)=>{if(queued)Mutate();if(!v())return Task.FromResult(false);sends++;a();return Task.FromResult(true);});
            if(!queued)Mutate();
            Check(!await c.SendItemTransfer(request,true,()=>visible)&&sends==0,"stale transfer rejected "+queued+" "+change);
        }
        using(var c=TransferSeed()){
            TransferInventory(c,1,1,"劍 ($9)");Check(c.PrepareItemTransfer(c.CaptureItemTransfer(),null,"")==null,"equipped item cannot be deleted");
            Check(c.PrepareItemTransfer(c.CaptureItemTransfer(),456,"1")==null,"equipped item cannot be given");
        }
        foreach(bool queued in new[]{false,true}){
            using var c=TransferSeed();var request=TransferRequest(c,true);int sends=0;
            Sender(c,(b,v,a)=>{if(queued)Feed(c,TransferAmount(11));if(!v())return Task.FromResult(false);sends++;a();return Task.FromResult(true);});
            if(!queued)Feed(c,TransferAmount(11));
            Check(!await c.SendItemTransfer(request,true)&&sends==0,"delete cannot remove a larger stack than confirmed "+queued);
        }
        using(var c=TransferSeed()){
            var request=TransferRequest(c);int sends=0;
            Sender(c,(b,v,a)=>{if(!v())return Task.FromResult(false);sends++;a();return Task.FromResult(true);});
            Check(!await c.SendItemTransfer(request,true,()=>false)&&!await c.SendItemTransfer(request,true)&&sends==0,"failed admission consumes the confirmation token");
        }
        using(var c=TransferSeed()){
            Set(c.World,"TradeState","open");Check(c.PrepareItemTransfer(c.CaptureItemTransfer(),null,"")==null,"trade excludes delete");
            Check(c.PrepareItemTransfer(c.CaptureItemTransfer(),456,"1")==null,"trade excludes give");
        }
        using(var c=TransferSeed()){
            var request=TransferRequest(c);var release=new TaskCompletionSource<bool>();int sends=0;
            Sender(c,async(b,v,a)=>{Check(c.MovementBusy,"transfer participates in shared item busy gate");await release.Task;if(!v())return false;sends++;a();Feed(c,TransferAmount(7));return true;});
            var pending=c.SendItemTransfer(request,true);
            Check(!await c.SendItemTransfer(request,true)&&!await c.UseSelectedItem(),"pending transfer excludes repeated action");
            release.SetResult(true);Check(await pending&&sends==1&&c.World.SelectedItem?.Count==7,"authoritative reply before completion preserves successful send ownership");
        }
        using(var c=TransferSeed()){
            Sender(c,(b,v,a)=>{if(!v())return Task.FromResult(false);a();Feed(c,Packet(w=>{w.Write((byte)71);w.Write((ushort)210);w.Write((byte)1);Text(w,"測試物品");}));return Task.FromResult(true);});
            Check(await c.SendItemTransfer(TransferRequest(c),true)&&c.World.LastServerReply!=null&&c.World.SelectedItem?.Count==10,"S71 HCS denial visible without inventory prediction");
        }
        using(var c=TransferSeed()){
            var release=new TaskCompletionSource<bool>();var request=TransferRequest(c);
            Sender(c,async(b,v,a)=>{if(!v())return false;a();await release.Task;return true;});
            var pending=c.SendItemTransfer(request,true);Set(c,"generation",c.SessionGeneration+1);Set(c,"itemLease",100);Set(c,"itemSending",true);Set(c.World,"ItemNotice","新連線訊息");
            release.SetResult(true);Check(!await pending&&c.World.ItemNotice=="新連線訊息"&&c.ItemBusy,"old completion cannot clear new connection busy state or overwrite notice");
        }
        using(var c=TransferSeed()){
            int sends=0;Sender(c,(_,_,_)=>{sends++;throw new IOException("offline transport failure");});
            Check(!await c.SendItemTransfer(TransferRequest(c),true)&&sends==1&&c.State==LoginState.Faulted,"transport failure faults session without retry");
        }
    }
}
