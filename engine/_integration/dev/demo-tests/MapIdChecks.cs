using P06;

partial class Checks {
    static void MapIdOverloads(){
        // Literal synthetic vectors independent of the product decoder.
        byte[] dry=Convert.FromHexString("CE34120000000000");
        byte[] wet=Convert.FromHexString("CE34120100000000");
        byte[] single=Convert.FromHexString("CE34120010F8000000000000100D35C5");
        byte[] recoveryDry=Convert.FromHexString("CE341200000000000000000000000000");
        byte[] recoveryWet=Convert.FromHexString("CE341201000000000000000000000000");
        foreach(var (packet,water) in new[]{(dry,false),(wet,true),(single,false),(recoveryDry,false),(recoveryWet,true)}){
            using var c=Seed();Set(c.World,"Underwater",true);
            int epoch=c.World.WorldEpoch;
            Feed(c,packet);
            Check(c.World.MapId==0x1234&&c.World.Underwater==water,"S206 overload map and water "+packet.Length+"/"+water);
            Check(c.World.WorldEpoch>epoch&&c.World.Actor==null&&c.World.Objects.Length==0&&c.World.TargetId==null,"S206 overload retires old world "+packet.Length+"/"+water);
            Feed(c,Pack(123));Feed(c,Pack(456,32601));c.World.SelectTarget(456);
            epoch=c.World.WorldEpoch;Feed(c,packet);
            Check(c.World.WorldEpoch>epoch&&c.World.Actor==null&&c.World.Objects.Length==0&&c.World.TargetId==null,"S206 same-map reset "+packet.Length+"/"+water);
        }
        using var unchanged=Seed();Set(unchanged.World,"Underwater",true);
        var actor=unchanged.World.Actor;var objects=unchanged.World.Objects;var target=unchanged.World.TargetId;
        int oldEpoch=unchanged.World.WorldEpoch;
        void Reject(byte[] packet,string label){
            bool rejected=false;try{Feed(unchanged,packet);}catch(ProtocolException){rejected=true;}
            Check(rejected,"S206 rejects "+label);
            Check(unchanged.World.MapId==0&&unchanged.World.Underwater&&unchanged.World.Actor==actor&&
                unchanged.World.WorldEpoch==oldEpoch&&unchanged.World.TargetId==target&&unchanged.World.Objects.SequenceEqual(objects),
                "S206 invalid packet preserves world "+label);
        }
        for(int n=1;n<dry.Length;n++)Reject(dry[..n],"short normal length "+n);
        for(int n=1;n<single.Length;n++)Reject(single[..n],"short single length "+n);
        for(int n=4;n<dry.Length;n++){
            var invalid=(byte[])dry.Clone();invalid[n]=1;Reject(invalid,"normal nonzero padding "+n);
        }
        for(int n=4;n<single.Length;n++)for(int value=0;value<=255;value++){
            if(value==single[n])continue;
            var invalid=(byte[])single.Clone();invalid[n]=(byte)value;Reject(invalid,$"single tail {n}/{value}");
        }
        for(int water=1;water<=255;water++){
            var invalid=(byte[])single.Clone();invalid[3]=(byte)water;Reject(invalid,"single water "+water);
        }
        for(int extra=1;extra<=8;extra++){
            if(extra!=8)Reject([..dry,..new byte[extra]],"normal extension "+extra);
            Reject([..single,..new byte[extra]],"single extension "+extra);
            Reject([..recoveryDry,..new byte[extra]],"recovery extension "+extra);
        }
        for(int water=2;water<=255;water++){
            var invalid=(byte[])recoveryDry.Clone();invalid[3]=(byte)water;Reject(invalid,"recovery water "+water);
        }
        foreach(var recovery in new[]{recoveryDry,recoveryWet})for(int n=4;n<recovery.Length;n++)for(int value=1;value<=255;value++){
            var invalid=(byte[])recovery.Clone();invalid[n]=(byte)value;Reject(invalid,$"recovery tail {recovery[3]}/{n}/{value}");
        }
    }
}
