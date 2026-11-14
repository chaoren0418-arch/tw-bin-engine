using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;

namespace P06;

public static class PortalPackets {
    public static byte[] Enter(int x,int y){
        if(x is <0 or >ushort.MaxValue)throw new ArgumentOutOfRangeException(nameof(x));
        if(y is <0 or >ushort.MaxValue)throw new ArgumentOutOfRangeException(nameof(y));
        byte[] bytes=new byte[8];bytes[0]=219;
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(1),(ushort)x);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(3),(ushort)y);
        return bytes;
    }
}

// S32 tile attributes are NOT portal flags. Read the named portal records from
// the exact source already accepted by OriginalMapStore; destinations stay server-owned.
internal sealed class PortalCatalog {
    readonly string root;
    readonly MapRawSource source;
    readonly Dictionary<string,JsonElement> entries;
    // T1195 (#1195): legacy .seg-only rows of the r29 manifest (maps[].unsupportedBlocks with sources[].sha256), read from TW_CLIENT_ROOT like OriginalMapStore.
    readonly Dictionary<string,string> legacySeg=new(StringComparer.Ordinal);
    readonly int map;readonly string? clientRoot;
    readonly ConditionalWeakTable<OriginalMapBlock,HashSet<(int,int)>> cells=new();
    internal PortalCatalog(string assetsRoot,int map,string? clientRoot=null){
        root=Path.Combine(assetsRoot,"maps");source=MapRawSource.Open(assetsRoot,clientRoot);this.map=map;this.clientRoot=clientRoot??source.ClientRoot;
        using var integrity=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(assetsRoot,"integrity.json")));
        var pin=integrity.RootElement.GetProperty("files").EnumerateArray().Single(p=>p.S("path")=="maps/manifest.json");
        using var manifest=JsonDocument.Parse(Verified(Path.Combine(root,"manifest.json"),pin.S("sha256")));
        if(manifest.RootElement.S("schema")!="p24-map-provider-v1")throw new InvalidDataException("portal map schema");
        entries=manifest.RootElement.GetProperty("blocks").EnumerateArray().Where(p=>p.I("mapId")==map).ToDictionary(p=>p.S("name"),p=>p.Clone());
        if(manifest.RootElement.TryGetProperty("maps",out var maps)&&maps.ValueKind==JsonValueKind.Array)
            foreach(var m in maps.EnumerateArray().Where(m=>m.I("mapId")==map))
                if(m.TryGetProperty("unsupportedBlocks",out var rows)&&rows.ValueKind==JsonValueKind.Array)
                    foreach(var row in rows.EnumerateArray())if(SegBlocks.LegacySha256(map,row) is string sha)legacySeg[row.GetProperty("name").GetString()!]=sha;
    }
    static byte[] Verified(string path,string hash){
        var bytes=File.ReadAllBytes(path);
        if(!Convert.ToHexString(SHA256.HashData(bytes)).Equals(hash,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("portal source hash mismatch");
        return bytes;
    }
    internal bool Contains(OriginalMapCache cache,int x,int y){
        if(!cache.Bounds.Contains(x,y)||!cache.Contains(x,y))return false;
        foreach(var block in cache.Slots.OfType<OriginalMapBlock>()){
            int localX=2*x-32768-block.X,localY=y-block.Y;
            if(localX is <0 or >=128||localY is <0 or >=64)continue;
            return cells.GetValue(block,ReadBlock).Contains((localX,localY));
        }
        return false;
    }
    HashSet<(int,int)> ReadBlock(OriginalMapBlock block){
        if(block.Sha256=="verified-absent-original-four-paths")return [];
        JsonElement entry=default;bool legacy=false;string? legacySha=null;
        if(!entries.TryGetValue(block.Name+".s32",out entry)&&!entries.TryGetValue(block.Name+".seg",out entry)&&!(legacy=legacySeg.TryGetValue(block.Name,out legacySha)))throw new InvalidDataException("portal block missing");
        if(!(legacy?legacySha!:entry.S("sha256")).Equals(block.Sha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("portal block identity changed");
        bool seg=legacy||entry.TryGetProperty("format",out var format)&&format.GetString()=="classic-seg";
        // T1195: any SEG identified by the manifest SHA-256 (sub_F0B570's SEG branch has no name check); the map 6 anchor keeps its pin.
        if(seg)try{SegBlocks.CheckAnchor(map,block.Name,block.Sha256);}catch(NotSupportedException ex){throw new NotSupportedException("unverified portal SEG profile",ex);}
        byte[] bytes;
        try{bytes=legacy?SegBlocks.ReadLegacy(clientRoot,map,block.Name,block.Sha256):source.Block(entry.S("path"),block.Sha256);}
        catch(InvalidDataException ex) when(ex.Message.StartsWith("Original cache source hash",StringComparison.Ordinal)){throw new InvalidDataException("portal source hash mismatch",ex);}
        return ReadCells(bytes,seg);
    }
    internal static HashSet<(int,int)> ReadCells(byte[] bytes,bool seg){
        using var reader=new BinaryReader(new MemoryStream(bytes,false));
        void Skip(long size){if(size<0||size>reader.BaseStream.Length-reader.BaseStream.Position)throw new InvalidDataException("truncated portal source");reader.BaseStream.Position+=size;}
        int Count(bool small=false){int count=small?reader.ReadUInt16():reader.ReadInt32();if(count<0||count>65536)throw new InvalidDataException("portal section count");return count;}
        Skip(8192*(seg?2:4));Skip(Count(true)*(seg?4L:6L));Skip(8192*(seg?1:2));
        int groups=Count();for(int i=0;i<groups;i++){Skip(2);Skip(Count(true)*(seg?5L:7L));}
        Skip(Count()*5L);Skip(seg?reader.ReadByte():Count()*4L);
        // T1195: the SEG branch of sub_F0B570 reads the portal section only while data remains (VA 0xF0D10C).
        if(seg&&reader.BaseStream.Position==reader.BaseStream.Length)return [];
        int portals=Count(true);var result=new HashSet<(int,int)>();
        for(int i=0;i<portals;i++){
            Skip(reader.ReadByte());int x=reader.ReadByte(),y=reader.ReadByte();Skip(6);
            if(x>=128||y>=64)throw new InvalidDataException("portal local coordinates");
            result.Add((x,y));
        }
        return result; // Effect tail belongs to the existing map decoder.
    }
}

public sealed partial class LoginConnection {
    readonly ConditionalWeakTable<OriginalMapCache,PortalCatalog> portalCatalogs=new();
    internal Func<int,Task> PortalDelay=ms=>Task.Delay(ms);
    async Task EnterPortalAtArrival(int x,int y,int session,int world,int corrections){
        if(session!=generation||world!=World.WorldEpoch||corrections!=World.Corrections||!MovementAvailable||World.MapCache is not {} cache||World.MapId!=cache.MapId||World.Actor is not {} actor||actor.X!=x||actor.Y!=y||sendMovementGate is not {} sender)return;
        try{
            var catalog=portalCatalogs.GetValue(cache,c=>new(World.MapAssetsRoot,c.MapId,World.MapClientRoot));
            if(!catalog.Contains(cache,x,y))return;
        }catch(Exception ex) when(ex is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or InvalidOperationException or NotSupportedException or KeyNotFoundException){
            World.MotionNotice="入口資料無法核驗；未送出入口請求。";Set(State,World.MotionNotice);return;
        }
        int lease=movementLease,transition=World.TeleportGeneration;
        bool Current()=>session==generation&&world==World.WorldEpoch&&corrections==World.Corrections&&lease==movementLease&&transition==World.TeleportGeneration&&MovementAvailable&&ReferenceEquals(cache,World.MapCache)&&World.MapId==cache.MapId&&World.Actor is {} current&&current.ObjectId==actor.ObjectId&&current.X==x&&current.Y==y;
        // C29's local position is predicted before the visual step completes.
        // Keep the movement lease through arrival and revalidate under the send lock.
        int remaining=(int)Math.Clamp(lastMovementAt+(World.MovementAnimationMs??MovementRules.StepMs)-MovementClock(),0,int.MaxValue);
        if(remaining>0)await PortalDelay(remaining);
        if(!Current())return;
        await sender(PortalPackets.Enter(x,y),Current,()=>{
            World.MotionNotice="已送出入口座標；等待伺服器判定。";
            Set(State,World.MotionNotice);
        });
    }
}
