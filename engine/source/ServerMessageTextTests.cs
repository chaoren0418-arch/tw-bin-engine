using System.Text;
namespace P06;

// T228 (#546) offline checks. "server-message-text" uses an invented table in a synthetic, DES-encoded Text archive built in
// memory (no game assets). "server-message-text-client" reads the original client's table only when it is present locally
// (identity pinned in docs/reference/protocol-gaps.md §3); without it, it checks the built-in fallback instead.
static class ServerMessageTextTests {
    static byte[] Cp950(string s){Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);return Encoding.GetEncoding(950).GetBytes(s);}
    // Synthetic Text.idx/Text.pak: names may repeat, rows and payloads DES-encoded, payload lengths not multiples of 8.
    static (byte[] Index,byte[] Pak) Archive(params (string Name,byte[] Data)[] files){
        using var pak=new MemoryStream();var rows=new byte[28*files.Length];
        for(int i=0;i<files.Length;i++){
            var row=rows.AsSpan(i*28,28);BitConverter.TryWriteBytes(row,(uint)pak.Position);
            Encoding.ASCII.GetBytes(files[i].Name).CopyTo(row[4..]);BitConverter.TryWriteBytes(row[24..],(uint)files[i].Data.Length);
            pak.Write(TextArchive.Encode(files[i].Data));
        }
        return ([..BitConverter.GetBytes((uint)files.Length),..TextArchive.Encode(rows)],pak.ToArray());
    }
    static byte[] S71(ushort id,params string[] args){
        var b=new List<byte>{71,(byte)id,(byte)(id>>8),(byte)args.Length};
        foreach(string a in args){b.AddRange(LoginPackets.TextEncoding.GetBytes(a));b.Add(0);}
        return LoginPackets.Pad(b.ToArray());
    }
    // Invented records (not game text) that exercise every sub_933850 branch; 6 lines for a header count of 7 (last id reads "").
    const string Table="7\r\n王族\r\n\\f1金幣不足。\r\n\\f1%0不足%s。\r\n獲得%0%o 。\r\n%%-%n-%9-%d-%x-%甲-%1-%0\r\n尾%\r\n";

    [WorldModuleTest("server-message-text")]
    static void Run(WorldModuleTestContext t){
        // 1. TextArchive: DES round trip, trailing partial block stays plain, duplicate names allowed until Single() asks for one.
        byte[] odd=Encoding.ASCII.GetBytes("0123456789abc");
        t.Check(TextArchive.Decode(TextArchive.Encode(odd)).AsSpan().SequenceEqual(odd),"DES-ECB round trip (13 bytes)");
        t.Check(TextArchive.Encode(odd).AsSpan(8).SequenceEqual(odd.AsSpan(8)),"trailing partial block is not encoded");
        var tableBytes=Cp950(Table);
        var (idx,pak)=Archive(("dup.html",[1,2,3]),("string-c.tbl",tableBytes),("dup.html",[4,5]),("list.spr",odd));
        var archive=TextArchive.FromBytes(idx,pak);
        t.Equal(4,archive.Entries.Count,"all idx records listed, duplicates included");
        t.Equal(1,archive.Single("STRING-C.TBL")?.Record??-1,"Single is case-insensitive");
        t.Check(archive.Read(archive.Single("list.spr")!).AsSpan().SequenceEqual(odd),"payload decoded");
        t.Check(archive.Single("missing.tbl")==null,"absent name -> null");
        t.Throws<InvalidDataException>(()=>archive.Single("dup.html"),"repeated name rejected when one file is required");
        t.Throws<InvalidDataException>(()=>TextArchive.FromBytes(idx[..^1],pak),"idx size must be 4+28n");
        t.Throws<InvalidDataException>(()=>TextArchive.FromBytes(idx,pak[..^1]),"record past pak end rejected");

        // 2. Table: count header, zero-based ids, CRLF, ids past stored lines read as "".
        var table=ServerStringTable.Load(archive);
        t.Equal(7,table.Count,"count = first line");
        t.Equal("王族",table[0],"id 0 is the first line after the header");
        t.Equal("",table[6],"id < count past the stored lines is empty (sub_4EAFF0)");
        t.Throws<ArgumentOutOfRangeException>(()=>_=table[7],"id >= count is not a record");
        t.Throws<InvalidDataException>(()=>ServerStringTable.Parse(Cp950("x\r\n")),"non-numeric header rejected");

        // 3. sub_933850 (CP950) formatting.
        t.Equal("A\\<不足。",ServerMessageText.Format("%0不足%s。",["A"]),"%0 substituted (+\\<), %s dropped");
        t.Equal("%-n---x-甲-B\\<-A\\<",ServerMessageText.Format("%%-%n-%9-%d-%x-%甲-%1-%0",["A","B"]),"%%, %n, missing %9, %d, %x, %<Big5>, order");
        t.Equal("尾",ServerMessageText.Format("尾%",[]),"trailing % stops output");
        t.Equal("a b^c\\<",ServerMessageText.Format("%0",["a^b^c"]),"only the first ^ in an argument becomes a space");
        t.Equal("%1\\<",ServerMessageText.Format("%0",["%1","X"]),"argument text is never re-parsed");
        t.Equal("x",ServerMessageText.DisplayText("\\f1x\\F3\\<"),"display text drops \\fN, \\FN and \\<");

        // 4. Decode with the table: every id < count is displayed; id >= count is dropped (sub_942B40).
        using(ServerMessageText.UseTable(table)){
            var r=ServerMessageText.Decode(1,[]);
            t.Check(r is {Recognized:true,Displayed:true,Text:"金幣不足。",Raw:"\\f1金幣不足。"},"id 1 from the table",r);
            r=ServerMessageText.Decode(1,["unused"]);
            t.Check(r is {Recognized:true,Text:"金幣不足。"},"extra arguments are ignored like the original",r);
            r=ServerMessageText.Decode(2,["$4"]);
            t.Check(r is {Recognized:true,Text:"$4不足。"}&&r.Raw=="\\f1$4\\<不足。","%0/%s template; $ token left for the display layer",r);
            r=ServerMessageText.Decode(6,[]);
            t.Check(r is {Recognized:true,Displayed:true,Text:""},"last id < count displays an empty record",r);
            r=ServerMessageText.Decode(7,["x"]);
            t.Check(r is {Recognized:false,Displayed:false,Text:""}&&r.Arguments.SequenceEqual(["x"]),"id >= count is dropped",r);
            t.Check(ServerMessageText.IsDropped(7)&&ServerMessageText.IsDropped(4492)&&!ServerMessageText.IsDropped(6),"IsDropped follows the table count");
            t.Check(ServerStatusMessages.IsInformational(4492,[])&&!ServerStatusMessages.IsInformational(1,[]),"dropped ids never acknowledge a pending flow");
            var s=new WorldSession();
            s.Receive(S71(3,"短劍"),"");
            t.Check(s.LastServerReply is {Id:3,Recognized:true,Text:"獲得短劍 。"}&&s.LastServerMessage=="獲得短劍 。","S71 through WorldSession uses the table",s.LastServerReply);
            s.Receive(S71(4492),"");
            t.Check(s.LastServerReply is {Id:4492,Displayed:false}&&s.LastServerMessage=="","S71 id >= count reaches the session as not displayed",s.LastServerReply);
        }
        // 5. Without a table (CI, no client): previous built-in texts, nothing is dropped.
        using(ServerMessageText.UseTable(null)){
            t.Check(ServerMessageText.Decode(125,[]) is {Recognized:true,Displayed:true,Text:"你不能夠放棄此樣物品。"},"fallback 125");
            t.Check(ServerMessageText.Decode(403,["A"]) is {Recognized:true,Text:"獲得A 。"},"fallback 403");
            t.Check(ServerMessageText.Decode(700,["a","b"]) is {Recognized:false,Displayed:true,Text:"原服訊息 700：a、b"},"fallback unknown id stays labeled");
            t.Check(!ServerMessageText.IsDropped(4492),"no table -> nothing dropped");
        }
    }

    [WorldModuleTest("server-message-text-client")]
    static void Client(WorldModuleTestContext t){
        string root=GroundSpriteReader.ClientRoot;
        if(!File.Exists(Path.Combine(root,"Text.idx"))){
            t.Check(ServerMessageText.Table==null&&ServerMessageText.TableError!=null,"no client Text.idx -> no table, error recorded");
            t.Check(ServerMessageText.Decode(189,[]) is {Recognized:true,Text:"金幣不足。"},"no client -> built-in fallback");
            return;
        }
        var table=ServerMessageText.Table;
        t.Check(table!=null,"client string-c.tbl loads",ServerMessageText.TableError);
        if(table==null)return;
        t.Equal("4ebbc1a7a002a4c1dcdfa73dbf056b0e00648e0789763410c40e5079a22d43e9",table.Sha256,"decoded string-c.tbl SHA-256 (protocol-gaps §3)");
        t.Equal(3484,table.Count,"record count");
        t.Check(ServerMessageText.Decode(125,[]) is {Recognized:true,Text:"你不能夠放棄此樣物品。"},"125 matches the previously verified text");
        t.Check(ServerMessageText.Decode(189,[]) is {Recognized:true,Text:"金幣不足。"},"189 matches the previously verified text");
        t.Check(ServerMessageText.Decode(337,["A"]).Text=="A不足。"&&ServerMessageText.Decode(403,["A"]).Text=="獲得A 。","337/403 match the previous hand-written results");
        t.Check(ServerMessageText.Decode(3483,[]).Displayed&&!ServerMessageText.Decode(3484,[]).Displayed&&!ServerMessageText.Decode(4492,[]).Displayed,"ids >= 3484 (e.g. 4492) are dropped");
    }
}
