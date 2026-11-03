using System.Buffers.Binary;
namespace P06;
public sealed partial class ClientForm {
    // Test controller only; use the same actual view-to-scene pixel that PickObject consumes.
    Point MagicEmptySelectionPoint(){
        RefreshScene();var p=objectPatch??throw new Exception("no original selection patch");
        for(int vy=(int)Math.Max(152,Camera.ViewOffset.Y+4);vy<Camera.View.Height+Camera.ViewOffset.Y-4;vy+=4)
        for(int vx=(int)Camera.ViewOffset.X+4;vx<Camera.View.Width+Camera.ViewOffset.X-4;vx+=4){
            var point=new Point(vx,vy);if(!ClientRectangle.Contains(point)||GetChildAtPoint(point)!=null)continue;var scene=Camera.ToScene(point);int x=(int)Math.Floor(scene.X)-p.Bounds.X,y=(int)Math.Floor(scene.Y)-p.Bounds.Y;
            if((uint)x>=(uint)p.Bounds.Width||(uint)y>=(uint)p.Bounds.Height)continue;int i=y*p.Bounds.Width+x;
            if(p.Mask[i]&&p.Owners[i]>=0&&p.EntityOwners![i]==null)return point;
        }
        throw new Exception("no actual visible no-entity map pixel for normal target clear");
    }
    internal static object MagicSelectionRegression(Assets assets,string folder){
        Directory.CreateDirectory(folder);using var form=new ClientForm(assets,folder,null);
        var checks=new List<object>();void Check(bool ok,string name,object? detail=null){if(!ok)throw new Exception(name);checks.Add(new{name,detail});}
        string root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../.."));var fixture=Json.Read(Path.Combine(root,"fixtures/magic-r4-staging-sequence.json")).GetProperty("beforeFirstBolt");
        string selected=fixture.GetProperty("actor").S("Name");uint self=fixture.GetProperty("actor").GetProperty("ObjectId").GetUInt32();var packets=fixture.GetProperty("packets").EnumerateArray().Select(v=>Convert.FromHexString(v.GetString()!)).ToArray();
        var actions=new List<int>();form.login.SeedMagicSelection(packets,selected,b=>actions.Add(b[0]));
        byte[] own=(byte[])packets.Last(b=>b[0]==87&&BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(5))==self).Clone(),wolf=(byte[])packets.Last(b=>b[0]==87&&BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(9))==1110).Clone();
        BinaryPrimitives.WriteUInt16LittleEndian(own.AsSpan(1),32600);BinaryPrimitives.WriteUInt16LittleEndian(own.AsSpan(3),32949);form.login.World.Receive(own,selected);
        BinaryPrimitives.WriteUInt16LittleEndian(wolf.AsSpan(1),32600);BinaryPrimitives.WriteUInt16LittleEndian(wolf.AsSpan(3),32948);wolf[12]=4;form.login.World.Receive(wolf,selected);uint target=BinaryPrimitives.ReadUInt32LittleEndian(wolf.AsSpan(5));
        form.World.Teleport(32600,32949);form.Camera.SetCenter(form.World.DisplayPosition);form.RefreshScene();
        try{
            form.ClickScene(form.VisiblePoint(target));Check(form.login.World.TargetId==target&&actions.Count==0,"actual native positive pixel selects without C6/C164");
            var empty=form.MagicEmptySelectionPoint();var point=form.Camera.ToScene(empty);var patch=form.objectPatch!;int i=((int)Math.Floor(point.Y)-patch.Bounds.Y)*patch.Bounds.Width+(int)Math.Floor(point.X)-patch.Bounds.X;
            Check(form.GetChildAtPoint(empty)==null&&patch.Mask[i]&&patch.Owners[i]>=0&&patch.EntityOwners![i]==null,"clear pixel is actual original map raster with no selectable entity",new{empty,scene=point,mapSerial=patch.Owners[i]});
            form.ClickScene(empty);Check(form.login.World.TargetId==null&&actions.Count==0,"actual ClickScene/PickObject clears selected target without C6/C164");
            Check(form.HasVisibleObjectPixel(target),"native clear preserves real positive target pixels for subsequent Web selection");
            form.ClickScene(form.VisiblePoint(target));Check(form.login.World.TargetId==target&&actions.Count==0,"same original target can be selected again without an action packet");
            return new{kind="offline hidden WinForms controller, real ClickScene/PickObject and original compositor; derived actor positions, no Show/network login/WebView initialization. Actual Web DOM is reserved for supervisor full UI.",checks};
        }finally{form.login.Cancel();form.CompleteWebJournal();form.timer.Dispose();form.renderer.Dispose();form.patch?.Dispose();form.network.Dispose();form.wireLog.Dispose();}
    }
}
public sealed partial class LoginConnection {
    internal void SeedMagicSelection(byte[][] packets,string selected,Action<byte[]> sent){
        bool entered=false;foreach(var b in packets){if(b[0]==223)entered=true;if(entered){World.ProbeSent=true;World.Receive(b,selected);}}
        State=LoginState.InWorld;sendMovementGate=(b,v,a)=>{if(!v())return Task.FromResult(false);sent(b);a();return Task.FromResult(true);};
    }
}
