using System.Reflection;
namespace P06;

// T360／R8-E（#824）離線測試：list.spr 105 cloth、111 stride、112 altdir 的解析與物件端規則（FU8§5）。
// 由 --module-tests 執行（名稱 listspr-fields）。只用合成 list.spr 條目（直接放進 EffectSpriteCatalog 的 metadata 快取），
// 格式照本機 list.spz 的實際寫法（名稱不一致、gfx 0 → 6262 等）；不讀原客戶端、不讀 engine/assets、不開遊戲、網路或 DB。
static class ListSprFieldTests {
    const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
    static readonly Dictionary<int,string> Rows=new(){
        [0]="#0 8 prince\n0.walk(1 4,24.0:4 24.1:4 24.2:4 24.3:4)\n101.shadow(3226)\n104.attr(0)\n105.cloth(1 6262)\n",
        [200]="#200 8 elf\n0.walk(1 1,0.0:4)\n105.clothes(4 202 205 206 210)\n",
        [234]="#234 8 multi\n0.walk(1 1,0.0:4)\n105.clothed(6 201 204 206 210 208 211)\n",
        [1062]="#1062 8 guard\n0.walk(1 1,0.0:4)\n111.stride(1)\n",
        [1395]="#1395 8 gate\n0.walk(1 1,0.0:4)\n112.altdir(1410)\n",
        // 影格參照裡的 105／111／112（例如 `24.105:4`、`]112`）不是欄位。
        [3000]="#3000 1 decoy\n0.effect(0 3,24.105:4 0.111:2]112 0.2:2)\n",
    };
    static EffectSpriteCatalog Catalog(){
        var c=new EffectSpriteCatalog(Path.Combine(AppContext.BaseDirectory,"listspr-fields-no-client"));
        var meta=(Dictionary<int,string>)typeof(EffectSpriteCatalog).GetField("metadata",F)!.GetValue(c)!;
        foreach(var (g,row) in Rows)meta[g]=row;
        typeof(EffectSpriteCatalog).GetField("metadataLoaded",F)!.SetValue(c,true);
        return c;
    }
    static bool Rejects(string row){try{ListSprFields.Parse(row);return false;}catch(InvalidDataException){return true;}}

    [WorldModuleTest("listspr-fields")]
    static void Run(WorldModuleTestContext t){Parse(t);Errors(t);Catalog(t);Layers(t);}

    static void Parse(WorldModuleTestContext t){
        var p=ListSprFields.Parse(Rows[0]);
        t.Check(p.Cloth.SequenceEqual(new[]{6262})&&p.Stride==0&&p.AltDir==null,"gfx 0 cloth(1 6262)",p);
        t.Check(ListSprFields.Parse(Rows[200]).Cloth.SequenceEqual(new[]{202,205,206,210}),"clothes 名稱也認（只看編號 105）");
        t.Check(ListSprFields.Parse(Rows[234]).Cloth.SequenceEqual(new[]{201,204,206,210,208,211}),"6 層照順序");
        var s=ListSprFields.Parse(Rows[1062]);t.Check(s.Stride==1&&s.Cloth.Length==0&&s.AltDir==null,"stride(1)",s);
        var a=ListSprFields.Parse(Rows[1395]);t.Check(a.AltDir==1410&&a.Stride==0&&a.Cloth.Length==0,"altdir(1410)",a);
        t.Check(ReferenceEquals(ListSprFields.Parse(Rows[3000]),ListSprFields.Empty),"影格參照不當欄位");
        t.Check(ReferenceEquals(ListSprFields.Parse("#9 1 x\n0.walk(1 1,0.0:4)\n"),ListSprFields.Empty),"沒有欄位＝Empty");
        // 不被 1105.／2112. 之類的更長編號誤認。
        t.Check(ReferenceEquals(ListSprFields.Parse("#9 1 x\n1105.cloth(1 5)\n2112.altdir(3)\n"),ListSprFields.Empty),"更長編號不誤認");
    }
    static void Errors(WorldModuleTestContext t){
        t.Check(Rejects("#9 1 x\n105.cloth(2 6262)\n"),"cloth 個數不符拒絕");
        t.Check(Rejects("#9 1 x\n105.cloth(11 1 2 3 4 5 6 7 8 9 10 11)\n"),"cloth 超過 10 層拒絕");
        t.Check(Rejects("#9 1 x\n105.cloth(1 70000)\n"),"cloth gfx 超出 word 拒絕");
        t.Check(Rejects("#9 1 x\n105.cloth()\n"),"cloth 空白拒絕");
        t.Check(Rejects("#9 1 x\n111.stride(256)\n"),"stride 超出 byte 拒絕");
        t.Check(Rejects("#9 1 x\n111.stride(1 2)\n"),"stride 多值拒絕");
        t.Check(Rejects("#9 1 x\n112.altdir(abc)\n"),"altdir 非數字拒絕");
        t.Check(!Rejects("#9 1 x\n105.cloth(0)\n")&&ListSprFields.Parse("#9 1 x\n105.cloth(0)\n").Cloth.Length==0,"cloth(0) 合法、沒有層");
    }
    static void Catalog(WorldModuleTestContext t){
        var c=Catalog();
        t.Check(c.Fields(0)?.Cloth.SequenceEqual(new[]{6262})==true,"catalog gfx 0");
        t.Check(c.Fields(5940)==null,"catalog 沒有的 gfx 回 null");
        t.Check(c.Fields(1395)?.AltDir==1410,"catalog altdir");
        var missing=new EffectSpriteCatalog(Path.Combine(AppContext.BaseDirectory,"listspr-fields-no-client"));
        t.Check(missing.Fields(0)==null,"讀不到動畫表時回 null，不丟例外");
    }
    static void Layers(WorldModuleTestContext t){
        var cloth=ListSprFields.Parse(Rows[0]);
        t.Check(!ListSprLayers.ClothVerified,"cloth 待真機確認，預設關閉");
        t.Check(ListSprLayers.ClothLayers(cloth).Length==0,"未確認前不多畫附加層");
        t.Check(ListSprLayers.ClothLayers(cloth,verified:true).SequenceEqual(new[]{6262}),"確認後回 6262");
        t.Check(ListSprLayers.ClothLayers(null,verified:true).Length==0,"沒有欄位不畫");
        var alt=ListSprFields.Parse(Rows[1395]);
        t.Check(ListSprLayers.AltDirGfx(1395,6,alt)==1410,"朝向 6 換 altdir");
        t.Check(Enumerable.Range(0,8).Where(d=>d!=6).All(d=>ListSprLayers.AltDirGfx(1395,d,alt)==1395),"其他朝向不換");
        t.Check(ListSprLayers.AltDirGfx(0,6,cloth)==0&&ListSprLayers.AltDirGfx(0,6,null)==0,"沒有 altdir 不換");
        var stride=ListSprFields.Parse(Rows[1062]);
        t.Check(ListSprLayers.StrideNext(0,stride)==1&&ListSprLayers.StrideNext(1,stride)==0,"stride(1)：0→1→0");
        var three=ListSprFields.Parse("#9 1 x\n111.stride(3)\n");
        t.Check(new[]{0,1,2,3}.Select(n=>ListSprLayers.StrideNext(n,three)).SequenceEqual(new[]{1,2,3,0}),"stride(3)：0…3 循環");
        t.Check(ListSprLayers.StrideNext(5,cloth)==0&&ListSprLayers.StrideNext(5,null)==0,"沒有 stride 恆 0");
    }
}
