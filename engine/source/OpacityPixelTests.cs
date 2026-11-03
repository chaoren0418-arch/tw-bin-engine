using System.Buffers.Binary;
using System.Security.Cryptography;
namespace P06;
public static class OpacityPixelTests {
    public static void Run(string output){
        string root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../..")),reference=Path.Combine(root,"reference/opacity");Directory.CreateDirectory(output);
        var data=OpacityCatalog.Shared.Value;var state=new SceneOpacity(data);var vectors=Json.Read(Path.Combine(reference,"native-state-vectors.json")).GetProperty("steps").EnumerateArray().ToArray();
        using var renderer=new Renderer(new Assets(Path.Combine(root,"assets")));var results=new List<object>();
        for(int step=0;step<vectors.Length;step++){
            var row=vectors[step];long now=row.GetProperty("expected").GetProperty("time").GetInt64();OpacityHistoricalPumps.Until(state,now);if(row.S("action")=="move"){var move=row.A("argument");state.Move(move[0],move[1],now);}
            string rgb=Path.Combine(reference,$"golden-step{step}.rgb555");if(!File.Exists(rgb))continue;
            renderer.SceneBackground(state);byte[] expected=File.ReadAllBytes(rgb),mask=File.ReadAllBytes(Path.ChangeExtension(rgb,"mask"));int mismatches=0,maskMismatches=0;
            for(int i=0;i<renderer.SceneColors.Length;i++){if(renderer.SceneColors[i]!=BinaryPrimitives.ReadUInt16LittleEndian(expected.AsSpan(2*i)))mismatches++;if(renderer.SceneMask[i]!=(mask[i]!=0))maskMismatches++;}
            results.Add(new{step,pixels=renderer.SceneColors.Length,mismatches,maskMismatches,backgroundUpdate=renderer.LastBackgroundUpdate});if(mismatches!=0||maskMismatches!=0){Json.Save(Path.Combine(output,"result.json"),new{status="fail",results});throw new Exception("original opacity golden differs");}
            var xy=row.GetProperty("expected").A("position");var center=Coordinates.Screen(xy[0],xy[1]);using var crop=renderer.Background.Clone(Rectangle.Intersect(new((int)center.X-360,(int)center.Y-240,720,480),new(0,0,renderer.Width,renderer.Height)),System.Drawing.Imaging.PixelFormat.Format32bppArgb);crop.Save(Path.Combine(output,$"original-scene-step{step}.png"));
        }
        byte[] table=File.ReadAllBytes(Path.Combine(reference,"blue-weights.bin"));int cases=0;for(int k=0;k<32;k++)for(int d=0;d<32;d++)for(int s=0;s<32;s++){int v=BinaryPrimitives.ReadUInt16LittleEndian(table.AsSpan(2*(k*1024+d*32+s)));ushort source=(ushort)(s|(s<<5)|(s<<10)),dest=(ushort)(d|(d<<5)|(d<<10));if(Renderer.Weighted(source,dest,k)!=(v|(v<<5)|(v<<10)))throw new Exception("native-generated table mismatch");cases++;}
        var actor=new Draw(0,0,0,1,"actor",0,-1,-1,new(2,[new(0,0,0x7c00)]),123);var roof=actor with{Layer="objects",ObjectId=0,Tile=new(2,[new(0,0,31)])};var r=new Rectangle(actor.Px,actor.Py,1,1);var cs=new ushort[1];var ms=new bool[1];var own=new int[1];var ids=new uint?[1];var ownership=new List<object>();
        foreach(var value in new[]{new OpacityValue(0,0,0),new(1,0,1),new(2,0,2),new(0x400,31,1),new(0x400,27,1),new(0x400,0,2)}){Array.Clear(cs);Array.Clear(ms);Array.Clear(ids);Renderer.Blit(actor,r,cs,ms,own,ids);Renderer.Blit(roof,r,cs,ms,own,ids,value);bool expected=value.Hidden||value.Half||value.Transition&&value.Alpha<31;if((ids[0]==123)!=expected||!ms[0])throw new Exception("opacity owner-mask");ownership.Add(new{value,pixel=cs[0],owner=ids[0],mask=ms[0]});}
        Json.Save(Path.Combine(output,"result.json"),new{status="pass",scope="Independent original-source full scenes, original native table, opaque/half/hidden/transitional selection ownership",results,nativeTableCases=cases,ownership});
    }
}
