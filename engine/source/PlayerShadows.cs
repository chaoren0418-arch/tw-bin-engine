using System.Security.Cryptography;
namespace P06;

// A separate original 101.shadow sprite, sampled with the body's exact sid:frame key.
// Source flags are actorFlags|5 (VA81C87F..81C8AC, ImageBase400000).
// Flag1 selects the RGB555 half-blend; shadow pixels never become pick targets.
public sealed class PlayerShadowCatalog {
    readonly string root;
    readonly Dictionary<(int Gfx,int BodySprite),(string Path,string Sha)> profiles=[];
    readonly Dictionary<(int,int),Dictionary<string,Frame>> loaded=[];
    static void Verify(string path,string sha){using var f=File.OpenRead(path);if(!Convert.ToHexString(SHA256.HashData(f)).Equals(sha,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Original shadow integrity: "+path);}
    public PlayerShadowCatalog(string assets){
        root=Path.GetFullPath(assets);const string relative="shadows/manifest.json";
        var pin=Json.Read(Path.Combine(root,"integrity.json")).GetProperty("files").EnumerateArray().Single(f=>f.S("path")==relative);
        string path=Path.Combine(root,relative);Verify(path,pin.S("sha256"));var manifest=Json.Read(path);
        if(manifest.S("schema")!="p24-original-shadows-v1")throw new InvalidDataException("Original shadow schema");
        foreach(var p in manifest.GetProperty("profiles").EnumerateArray()){
            string full=Path.GetFullPath(Path.Combine(root,p.S("path")));
            if(!full.StartsWith(root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Shadow path");
            profiles.Add((p.I("gfx"),p.I("bodySprite")),(full,p.S("sha256")));
        }
    }
    public Frame? Get(int gfx,int bodySprite,string key){
        var id=(gfx,bodySprite);if(!profiles.TryGetValue(id,out var source))return null;
        if(!loaded.TryGetValue(id,out var frames)){
            Verify(source.Path,source.Sha);var j=Json.Read(source.Path);
            if(j.S("schema")!="p24-original-shadow-frames-v1"||j.I("gfx")!=gfx||j.I("bodySprite")!=bodySprite||j.I("flags")!=5)throw new InvalidDataException("Shadow profile identity");
            frames=[];
            foreach(var f in j.GetProperty("frames").EnumerateArray()){
                var blocks=f.GetProperty("blocks").EnumerateArray().Select(b=>new Block(b.I("a"),b.I("b"),b.I("z"),b.I("source_order"),new Tile(2,b.GetProperty("pixels").EnumerateArray().Select(p=>{
                    var v=p.EnumerateArray().Select(x=>x.GetInt32()).ToArray();if(v.Length!=3||v[0]<0||v[0]>=24||v[1]<0||v[1]>=24||v[2]<0||v[2]>32767)throw new InvalidDataException("Shadow pixel");return new Pixel(v[0],v[1],(ushort)v[2]);
                }).ToArray()))).ToArray();
                frames.Add(f.S("key"),new Frame(f.S("key"),f.A("bounds"),blocks));
            }
            loaded.Add(id,frames);
        }
        // Unprovided appearances/actions stay unshadowed; never reuse an unrelated frame.
        return frames.GetValueOrDefault(key);
    }
}

public sealed partial class Renderer {
    PlayerShadowCatalog? playerShadows;
    IEnumerable<Draw> ShadowDraws(ObjectAppearance? appearance,Frame body,int x,int y,uint objectId=0){
        var shadow=(playerShadows??=new PlayerShadowCatalog(assets.Root)).Get(appearance?.Gfx??0,appearance?.Sprite??3225,body.Key);
        if(shadow==null)return [];
        // Original body is inserted before shadow; equal-z nodes insert-before-existing.
        // Descending serial reproduces this, while real per-block Z and tile coordinates remain intact.
        return shadow.Blocks.Select(b=>new Draw(2*(x-Coordinates.MinX)+b.A,y-Coordinates.MinY+b.B,b.Z,1100000+b.Order,"shadow",b.Order,-1,-1,b.Tile,objectId));
    }
}
