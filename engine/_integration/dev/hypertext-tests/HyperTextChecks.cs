using P06;
using System.Text.Json;

// T218 / M7-R: S39 query results use the NPC (HYPER_TEXT_WINDOW) panel instead of chat lines,
// and the HTML action "checkpk" sends C_CHECK_PK. Synthetic packets, memory sender, no network.
partial class Checks {
    static byte[] HyperDialog(uint npc,string html)=>Packet(b=>{b.Write((byte)39);b.Write(npc);Text(b,html);b.Write((ushort)0);b.Write((ushort)0);});
    static List<byte[]> HyperSender(LoginConnection c){
        var sent=new List<byte[]>();Sender(c,(b,v,a)=>{if(!v())return Task.FromResult(false);sent.Add(b);a();return Task.FromResult(true);});return sent;
    }
    static async Task HyperTextChecks(string output){
        string fixture=Path.Combine(Path.GetDirectoryName(Path.GetFullPath(output))!,"hypertext-fixture");Directory.CreateDirectory(fixture);
        string templates=Path.Combine(fixture,"templates.json");
        File.WriteAllText(templates,JsonSerializer.Serialize(new Dictionary<string,string>{
            ["y_who"]="<html>線上 <var src=\"#0\"> 人<br><a link=\"y_who\">重新整理</a></html>",
            ["y_k_2"]="<html>殺人榜<br><var src=\"#0\"></html>",
            ["pk-npc"]="<html>贖罪<br><a action=\"checkpk\">查詢 PK 次數</a><a action=\"CheckPK\">大寫</a><a action=\"other\">其他</a></html>"}));

        // Self pages without a template: decoded plain text in the panel, chat untouched, NPC context untouched.
        using(var c=Seed()){
            int chat=c.World.PlayerChatRevision,interaction=c.World.InteractionRevision;
            Feed(c,WhoHtml());
            Check(c.World.QueryVisible&&c.World.QueryHtml=="y_who"&&c.World.QueryPage!.Text.Split('\n')[0]=="線上人數：42","hypertext self y_who opens query page");
            Check(c.World.QueryPage!.Limitation==WorldSession.HyperTextFallbackNotice&&c.World.QueryPage.Options.Length==0,"hypertext missing template uses decoded text and says so");
            Check(c.World.PlayerChatRevision==chat&&c.World.PlayerChatLines.Length==0,"hypertext y_who adds no chat line");
            Check(c.World.InteractionRevision==interaction&&c.World.InteractionKind=="closed"&&c.World.CanStartInteraction,"hypertext self page does not block NPC talk");
            string snapshot=JsonSerializer.Serialize(c.World.InteractionSnapshot());
            Check(snapshot.Contains("\"query\":{")&&snapshot.Contains("\"html\":\"y_who\""),"hypertext snapshot exposes query page");
            int q=c.World.QueryRevision;Feed(c,QueryHtml(123,"pc_show",CheckFields()));
            Check(c.World.QueryRevision==q+1&&c.World.QueryHtml=="pc_show"&&c.World.QueryPage!.Text.StartsWith("目標：目標者"),"hypertext newer result replaces page (single window)");
            Feed(c,QueryHtml(123,"y_h_2",HeroRows("甲")));
            Check(c.World.QueryHtml=="y_h_2"&&c.World.QueryPage!.Text=="排名：英雄風雲榜・全職業\nNo.1 甲","hypertext self rank page");
            Check(c.World.CloseQueryPage()&&!c.World.QueryVisible&&!c.World.CloseQueryPage(),"hypertext close query page once");
            Check(!JsonSerializer.Serialize(c.World.InteractionSnapshot()).Contains("\"query\":{"),"hypertext closed page leaves snapshot");
            Feed(c,WhoHtml());c.World.Reset();
            Check(!c.World.QueryVisible,"hypertext page hidden after session reset");
        }
        // Self page during a pending NPC request keeps the NPC context intact.
        using(var c=Seed()){
            Call(c.World,"BeginTalk");int revision=c.World.InteractionRevision;
            Feed(c,WhoHtml());
            Check(c.World.QueryVisible&&c.World.InteractionKind=="waiting"&&c.World.InteractionRevision==revision,"hypertext self page keeps pending NPC request");
            Feed(c,HyperDialog(456,"npc"));
            Check(c.World.InteractionKind=="dialog"&&c.World.QueryVisible,"hypertext NPC page and query page coexist");
            Check(c.World.InteractionPage!.Text=="對話資源尚未載入。","hypertext ordinary page without templates unchanged");
        }
        // Templates present: the original template renders the page; self-page options are disabled.
        using(var c=Seed()){
            c.World.DialogTemplates=new(templates);Feed(c,WhoHtml());
            var page=c.World.QueryPage!;
            Check(page.Text.StartsWith("線上 42 人")&&page.Options.Length==1&&!page.Options[0].Enabled&&page.Limitation.Contains("選項尚未支援"),"hypertext template rendered with disabled self options");
        }
        // NPC ranking pages: dialog only, template first, decoded rows only when the template is missing.
        foreach(bool withTemplate in new[]{false,true}){
            using var c=Seed();if(withTemplate)c.World.DialogTemplates=new(templates);
            Call(c.World,"BeginTalk");Feed(c,QueryHtml(456,"y_k_2",RankRows(10,"甲,5")));
            var page=c.World.InteractionPage!;
            Check(c.World.InteractionKind=="dialog"&&c.World.PlayerChatLines.Length==0&&!c.World.QueryVisible,"hypertext NPC rank stays in dialog "+withTemplate);
            Check(withTemplate?page.Text.StartsWith("殺人榜\n甲,5")&&page.Limitation=="":page.Text=="排名：殺人榜\nNo.1 甲（5）"&&page.Limitation==WorldSession.HyperTextFallbackNotice,"hypertext NPC rank template/fallback "+withTemplate);
        }
        // A rejected rank page (other NPC) must not leak its fallback into the next page.
        using(var c=Seed()){
            c.World.DialogTemplates=new(templates);Call(c.World,"BeginTalk");Feed(c,QueryHtml(789,"y_w_1",RankRows(10,"外來")));
            Check(c.World.InteractionKind=="waiting","hypertext other NPC rank rejected");
            Feed(c,HyperDialog(456,"y_w_1"));
            Check(c.World.InteractionPage!.Text=="缺少此對話的原始繁中模板：y_w_1","hypertext rejected fallback does not leak");
        }
        // HTML action checkpk (HyperActCheckPK) sends field-less C51 rather than C125.
        foreach(int option in new[]{0,1}){
            using var c=Seed();c.World.DialogTemplates=new(templates);var sent=HyperSender(c);
            Check(await Service(c,"talk")&&sent[^1][0]==34,"hypertext pk NPC talk "+option);
            Feed(c,HyperDialog(456,"pk-npc"));
            Check(c.World.InteractionPage!.Options.Length==3&&c.World.InteractionPage.Options.All(o=>o.Enabled&&o.Kind=="action"),"hypertext pk page options "+option);
            Check(await Service(c,"option",option)&&sent[^1].SequenceEqual(WeaponRepairPackets.CheckPk())&&sent[^1][0]==51,"hypertext checkpk sends C51 "+option);
            Check(sent.Count==2&&c.World.InteractionKind=="waiting"&&!await Service(c,"option",option)&&sent.Count==2,"hypertext checkpk waits, no resend "+option);
        }
        using(var c=Seed()){
            c.World.DialogTemplates=new(templates);var sent=HyperSender(c);
            await Service(c,"talk");Feed(c,HyperDialog(456,"pk-npc"));
            Check(await Service(c,"option",2)&&sent[^1].SequenceEqual(NpcPackets.Action(456,"other")),"hypertext other action still C125");
        }
        using(var c=Seed()){
            c.World.DialogTemplates=new(templates);var sent=HyperSender(c);
            await Service(c,"talk");Feed(c,HyperDialog(456,"pk-npc"));c.World.SelectTarget(null);
            Check(!await Service(c,"option",0)&&sent.Count==1,"hypertext checkpk needs a valid dialog");
        }
    }
}
