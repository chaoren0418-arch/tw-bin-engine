namespace P06;
public sealed partial class LoginConnection {
    internal void SeedInventoryLayout(){
        Cancel();var rows=Json.Read(Path.Combine(AppContext.BaseDirectory,"../../fixtures/combat-initialization.json")).GetProperty("rows").EnumerateArray().Select(x=>x.GetProperty("packet")).Where(x=>x.TryGetProperty("plainHex",out _)).Select(x=>Convert.FromHexString(x.S("plainHex"))).ToArray();string name=CapturedCharacterName(rows);bool entered=false;
        foreach(var b in rows){if(b[0]==223)entered=true;if(entered){World.ProbeSent=true;World.Receive(b,name);}}World.Receive(LoginPackets.Pad([123,1,0,0,0]),name);
        byte[] Item(uint id,ushort icon,ushort desc,byte use,uint count,string raw)=>[..BitConverter.GetBytes(id),..BitConverter.GetBytes(desc),use,0,..BitConverter.GetBytes(icon),1,..BitConverter.GetBytes(count),1,..LoginPackets.TextEncoding.GetBytes(raw),0,0,10,0,0,0,0,0,0,0,0,0,0];
        World.Receive(LoginPackets.Pad([5,3,..Item(1,81,130,249,6,"$238 (6)"),..Item(2,5,0,255,3,"$4 (3)"),..Item(3,475,1339,14,1,"$55")]),name);Set(LoginState.InWorld,"明示合成背包排版回歸；無socket");
    }
}
public sealed partial class ClientForm {
    async Task InventoryLayoutDemo(){
        try{
            await Until(()=>webReady,15000);login.SeedInventoryLayout();SyncInventory();await Task.Delay(400);RefreshScene();Update();
            var potion=inventoryList.Items.Cast<InventoryItem>().Single(i=>i.ObjectId==1);inventoryList.SelectedItem=potion;PublishWebState();await Task.Delay(200);
            string js="(()=>{const rows=[...document.querySelectorAll('.inventoryItem')];return rows.length===3&&rows.every(r=>r.querySelector('img')?.complete&&r.querySelector('img').naturalWidth>0)&&!document.getElementById('useItem').disabled&&document.documentElement.scrollWidth<=document.documentElement.clientWidth})()";
            Json.Save(Path.Combine(evidence!,"dom-diagnostics.json"),new{nativeEnabled=useItemButton.Enabled,login.ItemAvailable,login.MovementBusy,World.Walking,dom=await web!.ExecuteScriptAsync("JSON.stringify({rows:[...document.querySelectorAll(\'.inventoryItem\')].map(r=>({text:r.innerText,img:r.querySelector(\'img\')?.src,complete:r.querySelector(\'img\')?.complete,width:r.querySelector(\'img\')?.naturalWidth})),disabled:document.getElementById(\'useItem\').disabled,scroll:document.documentElement.scrollWidth,client:document.documentElement.clientWidth})")});
            CaptureEvidence("layout-before-assert");if(!useItemButton.Enabled||!login.ItemAvailable||login.MovementBusy||World.Walking||await web.ExecuteScriptAsync(js)!="true")throw new Exception("inventory DOM/images/layout");
            CaptureEvidence("synthetic-inventory-layout");await using(var f=File.Create(Path.Combine(evidence!,"synthetic-inventory-layout-web.png")))await web.CoreWebView2.CapturePreviewAsync(Microsoft.Web.WebView2.Core.CoreWebView2CapturePreviewImageFormat.Png,f);
            int epoch=login.World.WorldEpoch;login.Cancel();await Task.Delay(150);DispatchWeb(LocalPage,$"{{\"v\":1,\"id\":900,\"cmd\":\"useItem\",\"itemId\":1,\"epoch\":{epoch}}}");if(login.World.Inventory.Length!=0)throw new Exception("stale web item resurrected");
            Json.Save(Path.Combine(evidence!,"result.json"),new{status="pass",kind="explicit synthetic inventory in real WinForms/WebView2, no socket or server consumption",icons=3,oldEpochRejected=true,dom=await web.ExecuteScriptAsync("document.getElementById('inventoryPanel').innerText")});
        }catch(Exception ex){NetworkExitCode=6;Json.Save(Path.Combine(evidence!,"result.json"),new{status="fail",error=ex.ToString()});}finally{login.Cancel();Close();}
    }
}
