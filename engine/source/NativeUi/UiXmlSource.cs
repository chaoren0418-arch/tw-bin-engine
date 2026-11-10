using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
namespace P06.NativeUi;

/// <summary>
/// UI XML stored in Tile.pak (docs/design/native-ui.md §2.2, docs/reference/ui-layout.md §1 and V4 (c)(d)).
/// Encrypted blobs start with "X?xm": body=blob[4..] is AES-128-CBC over whole 16-byte blocks, the trailing
/// &lt;16 bytes are XORed with the running IV (no padding), output = "&lt;"+blob[1..4]+plain.
/// </summary>
public sealed class UiXmlSource(ClientArchive tile) {
    // Key / IV from TW1608262200.bin sub_A9FA30 (VA 0xA9FA30, File Offset 0x69EE30); same bytes as rea-scripts/ui_xml_decrypt.py.
    static readonly byte[] Key=[0xDC,0x84,0x01,0x21,0x2A,0x40,0x20,0x0A,0xDD,0x25,0xB9,0xA7,0x0D,0xB9,0xC9,0x4E];
    static readonly byte[] Iv=[0x3E,0x09,0x78,0xAA,0xC4,0xD5,0x30,0x63,0x30,0x0C,0x5F,0x9A,0x80,0x7F,0x22,0x46];
    /// <summary>Language letter appended by FileManager (CP950 → 'c', sub_A14A60).</summary>
    public const char LanguageSuffix='c';
    static readonly Regex Declaration=new(@"\A(?:\xEF\xBB\xBF)?<\?xml[^>]*?\bencoding\s*=\s*[""']([A-Za-z0-9._\-]+)[""']",RegexOptions.CultureInvariant);

    public ClientArchive Archive=>tile;
    /// <summary>
    /// T319 (#757): asked only when the main Tile.pak has neither "-c" nor plain name (main client always wins).
    /// Null (default; synthetic tests) = no reference client.
    /// </summary>
    public RefClientXmlSource? Reference{get;set;}

    public static bool IsEncrypted(ReadOnlySpan<byte> blob)=>blob.Length>=4&&blob[0]==(byte)'X'&&blob[1]==(byte)'?'&&blob[2]==(byte)'x'&&blob[3]==(byte)'m';

    public static byte[] Decrypt(byte[] blob){
        if(!IsEncrypted(blob))throw new UiResourceException("UI XML 不是加密格式（開頭不是 X?xm）");
        using var aes=Aes.Create();aes.Key=Key;
        int bodyLength=blob.Length-4,full=bodyLength/16*16;
        var output=new byte[blob.Length];output[0]=(byte)'<';output[1]=blob[1];output[2]=blob[2];output[3]=blob[3];
        Span<byte> iv=stackalloc byte[16];Iv.CopyTo(iv);
        Span<byte> plain=stackalloc byte[16];
        // CBC done block by block with ECB: DecryptCbc would treat the tail as padding.
        for(int i=0;i<full;i+=16){
            var cipher=blob.AsSpan(4+i,16);
            aes.DecryptEcb(cipher,plain,PaddingMode.None);
            for(int k=0;k<16;k++)output[4+i+k]=(byte)(plain[k]^iv[k]);
            cipher.CopyTo(iv);
        }
        for(int k=0;k<bodyLength-full;k++)output[4+full+k]=(byte)(blob[4+full+k]^iv[k]);
        return output;
    }

    /// <summary>
    /// Text decoding: an explicit encoding declaration wins; without one UTF-8 (V4 (c): expat default, BIN does no CP950
    /// conversion). Invalid bytes are rejected, never replaced.
    /// </summary>
    public static string DecodeText(byte[] plain,string fileName){
        var head=Encoding.Latin1.GetString(plain,0,Math.Min(plain.Length,256));
        var m=Declaration.Match(head);
        Encoding encoding;
        if(m.Success){
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            try{encoding=Encoding.GetEncoding(m.Groups[1].Value,EncoderFallback.ExceptionFallback,DecoderFallback.ExceptionFallback);}
            catch(ArgumentException ex){throw new UiLayoutException(fileName,"","不支援的 XML 編碼宣告 "+m.Groups[1].Value,ex);}
        }else encoding=new UTF8Encoding(false,true);
        int skip=plain.Length>=3&&plain[0]==0xEF&&plain[1]==0xBB&&plain[2]==0xBF&&encoding.CodePage==65001?3:0;
        try{return encoding.GetString(plain,skip,plain.Length-skip);}
        catch(DecoderFallbackException ex){throw new UiLayoutException(fileName,"",$"內容不是合法的 {encoding.WebName}",ex);}
    }

    /// <summary>"uistatus86" / "uistatus86.xml" → "uistatus86-c.xml" (or unchanged if already localized).</summary>
    public static string LocalizedName(string fileName){
        string ext=Path.GetExtension(fileName),stem=Path.GetFileNameWithoutExtension(fileName);
        if(stem.EndsWith("-"+LanguageSuffix,StringComparison.OrdinalIgnoreCase))return fileName;
        return stem+"-"+LanguageSuffix+ext;
    }
    static string Normalize(string baseName){
        if(string.IsNullOrWhiteSpace(baseName)||baseName.IndexOfAny(['/','\\'])>=0)throw new UiLayoutException(baseName,"","UI XML 檔名不合法");
        return Path.HasExtension(baseName)?baseName:baseName+".xml";
    }

    /// <summary>
    /// FileManager order (V4 (d), sub_52D6D0): "&lt;stem&gt;-c.&lt;ext&gt;" first, then the plain name.
    /// Returns decrypted bytes and the archive name actually used; null when neither exists.
    /// </summary>
    public (byte[] Plain,string ResolvedName)? TryReadPlain(string baseName){
        string name=Normalize(baseName),localized=LocalizedName(name);
        var entry=tile.Find(localized);
        if(entry==null&&!localized.Equals(name,StringComparison.OrdinalIgnoreCase)){
            entry=tile.Find(name);
            if(entry!=null)UiResourceLog.Write($"ui-xml-fallback {localized} -> {name}");
        }
        if(entry==null)return Reference?.TryRead(name) is {} r?(r.Plain,r.ResolvedName):null;
        return (Decrypt(tile.Read(entry)),entry.Name);
    }

    public (string Text,string ResolvedName) ReadText(string baseName){
        var found=TryReadPlain(baseName)??throw new UiLayoutException(Normalize(baseName),"","Tile.pak 中找不到 UI XML（含 -c 版）");
        return (DecodeText(found.Plain,found.ResolvedName),found.ResolvedName);
    }

    public UiLayout Load(string baseName){
        var (text,resolved)=ReadText(baseName);
        return UiLayout.Parse(text,resolved);
    }

    /// <summary>All *.xml names stored in Tile.idx (for diagnostics / optional local checks).</summary>
    public IEnumerable<string> XmlNames()=>tile.Entries.Keys.Where(n=>n.EndsWith(".xml",StringComparison.OrdinalIgnoreCase)).Order(StringComparer.OrdinalIgnoreCase);
}
