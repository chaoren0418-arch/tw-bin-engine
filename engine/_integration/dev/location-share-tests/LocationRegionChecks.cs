using P06;

// M7-O (#521): MapSystem region id, 1784 display placeholder and the 10-entry list (docs/reference/world-objects.md §5).
// Synthetic XML only; the real MapSystem XML stays in the client's Tile.pak. Set LOCATION_REGION_XML to a locally
// decrypted MapSystem XML (outside git) to additionally parse the real file.
partial class Checks {
    const string RegionXml="""
<?xml version="1.0"?>
<Map_System_Configulation>
  <World_Map pos = "45,30">
    <Map id = "1" listed = "true" mapDesc = "193" mapNum = "4" pngNum = "10001"><NPC icon = "3865" loc = "1,2" mapDesc = "296"/></Map>
    <Map id = "2" listed = "false" mapDesc = "183" mapNum = "5"/>
    <Map id = "5" mapDesc = "199" mapNum = "63"/>
  </World_Map>
  <Field_Map pos = "5,250"><Map mapNum = "4" pngNum = "10101"/></Field_Map>
  <Village_Map pos = "45,30">
    <Map id = "100" mapDesc = "264" mapNum = "0"/>
    <Map id = "102" mapDesc = "168" mapNum = "4"/>
    <Map id = "101" mapDesc = "165" mapNum = "4"/>
    <Map id = "123" mapDesc = "260" mapNum = "63"/>
  </Village_Map>
  <Dungern_Map pos = "5,250"><Map mapNum = "1"/></Dungern_Map>
</Map_System_Configulation>
""";
    static string RegionDoc(string world,string village="")=>$"<Map_System_Configulation><World_Map>{world}</World_Map><Village_Map>{village}</Village_Map></Map_System_Configulation>";
    static async Task LocationRegionChecks(){
        var saved=LocationShareProtocol.Regions;
        try{await LocationRegionCore();}
        finally{LocationShareProtocol.Regions=saved;}
    }
    static async Task LocationRegionCore(){
        var table=LocationRegionTable.Parse(RegionXml);
        Check(table.Count==7&&table.Find(1)==new LocationRegion(1,193,4,true)&&table.Find(2)!.Listed==false&&table.Find(101)!.Village&&!table.Find(5)!.Village,"region World_Map/Village_Map ids, mapDesc, mapNum, listed parsed; Field/Dungern entries ignored");
        Check(table.Find(3)==null&&table.Find(999)==null,"region unknown id has no entry");
        Check(table.RegionFor(4)==1&&table.RegionFor(63)==5,"region sender prefers World_Map entry of current map over villages");
        Check(table.RegionFor(0)==100,"region falls back to lowest Village_Map entry when no world entry");
        Check(table.RegionFor(777)==null,"region unresolved map gives no id");
        Check(LocationRegionTable.Parse(RegionDoc("",$"<Map id='102' mapDesc='1' mapNum='9'/><Map id='101' mapDesc='1' mapNum='9'/>")).RegionFor(9)==101,"region village fallback is deterministic (lowest id)");
        foreach(var (xml,label) in new[]{
            ("<Other><World_Map/></Other>","root"),
            (RegionDoc("<Map id='1' mapDesc='1' mapNum='1'/><Map id='1' mapDesc='2' mapNum='2'/>"),"duplicate id"),
            (RegionDoc("","<Map id='1' mapDesc='1' mapNum='1'/>")+"","village id below 100"),
            (RegionDoc("<Map id='100' mapDesc='1' mapNum='1'/>"),"world id above 99"),
            (RegionDoc("","<Map id='1000' mapDesc='1' mapNum='1'/>"),"village id above 999"),
            (RegionDoc("<Map id='-1' mapDesc='1' mapNum='1'/>"),"negative id"),
            (RegionDoc("<Map id='1' mapNum='1'/>"),"missing mapDesc"),
            (RegionDoc("<Map id='1' mapDesc='70000' mapNum='1'/>"),"mapDesc overflow"),
            (RegionDoc("<Map id='1' mapDesc='1' mapNum='x'/>"),"non-numeric mapNum"),
            (RegionDoc("<Map id='1' mapDesc='1' mapNum='1' listed='yes'/>"),"bad listed"),
            ("<Map_System_Configulation><World_Map/><World_Map/></Map_System_Configulation>","multiple World_Map"),
            ("<!DOCTYPE x [<!ENTITY a 'b'>]><Map_System_Configulation/>","DTD"),
            ("<Map_System_Configulation>","malformed"),
        }){
            try{LocationRegionTable.Parse(xml);throw new Exception("region accepted invalid XML: "+label);}
            catch(InvalidDataException){Check(true,"region rejects "+label);}
        }

        LocationShareProtocol.Regions=LocationRegionTable.Empty;
        Check(LocationShareProtocol.ZoneFor(4)==0&&LocationShareProtocol.RegionLabel(1)=="地區1","region without table: neutral zone 0, id shown as number");
        LocationShareProtocol.Regions=table;
        Check(LocationShareProtocol.ZoneFor(4)==1&&LocationShareProtocol.ZoneFor(777)==0,"region ZoneFor uses table, 0 when unresolved");
        var line=LocationShareProtocol.Decode(LocationPacket("中",4,32600,32940,1));
        Check(PlayerChatProtocol.FormatLine(line)=="[座標] 中：地區1〔字串193〕〔訊息1784〕","region receive shows sender + region mapDesc placeholder + 1784 placeholder, no coordinates");
        Check(LocationShareProtocol.Format(new(4,1,2,102))=="地區102〔字串168〕〔訊息1784〕","region village id >= 100 resolved from village table");
        Check(LocationShareProtocol.Format(new(4,1,2,77))=="地區77〔訊息1784〕","region unknown id shows only the number");
        Check(line.Location==new SharedLocation(4,32600,32940,1),"region typed coordinates kept for marker");
        try{LocationShareProtocol.Encode("",new(0,0,0,0));throw new Exception("empty recipient accepted");}
        catch(ArgumentException ex){Check(ex.Message.Contains("〔訊息1785〕"),"region recipient prompt marked with 1785 placeholder");}

        LocationShareProtocol.Regions=table;
        using(var c=Seed()){
            byte[]? actual=null;
            Sender(c,(bytes,current,admitted)=>{if(!current())return Task.FromResult(false);actual=bytes;admitted();return Task.FromResult(true);});
            var s=c.PlayerChatSnapshot();
            Check(await c.SendLocation("中",s.Generation,s.WorldEpoch,s.Actor),"region send admitted");
            Check(actual!.SequenceEqual(LocationShareProtocol.Encode("中",new(0,32600,32940,100))),"region send fills MapSystem region id of current map");
        }
        using(var c=Seed()){
            Check(c.World.LocationShares.Length==0,"region list starts empty");
            Feed(c,LocationReply(1783,"中"));
            Check(c.World.LocationShares.Length==0,"region server replies are not recorded");
            int revision=c.World.LocationShareRevision;
            for(int i=0;i<12;i++)Feed(c,LocationPacket("寄件"+i,4,(ushort)i,32940,1));
            var list=c.World.LocationShares;
            Check(list.Length==WorldSession.LocationShareLimit&&list[0].Location.X==2&&list[0].Sender=="寄件2"&&list[^1].Sender=="寄件11","region list keeps at most 10, oldest dropped first");
            Check(list.All(r=>r.Region==table.Find(1))&&list[^1].Sequence==c.World.PlayerChatLines[^1].Sequence,"region list stores resolved region and chat sequence");
            Check(c.World.PlayerChatLines.Length==13&&c.World.LocationShareRevision>revision,"region list is independent of chat history");
            Feed(c,LocationPacket("未知",4,1,1,77));
            Check(c.World.LocationShares[^1].Region==null,"region unknown id recorded without region");
            int chat=c.World.PlayerChatRevision;c.World.RefreshLocationLabels();
            Check(c.World.PlayerChatRevision==chat+1,"region table load forces chat redraw");
            c.World.Reset();
            Check(c.World.LocationShares.Length==0,"region session reset clears list");
        }
        LocationShareProtocol.Regions=LocationRegionTable.Empty;

        if(Environment.GetEnvironmentVariable("LOCATION_REGION_XML") is {Length:>0} path&&File.Exists(path)){
            var real=LocationRegionTable.Parse(File.ReadAllText(path));
            Check(real.Count>0&&real.Regions.All(r=>r.Village==(r.Id>=100)),"region optional local MapSystem XML parsed: "+real.Count);
            Console.WriteLine($"region local MapSystem: {real.Count} regions; map 4 -> {real.RegionFor(4)}; map 0 -> {real.RegionFor(0)}");
        }
    }
}
