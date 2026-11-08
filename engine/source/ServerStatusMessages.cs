namespace P06;
// Fixed Server_Game.jar d287bc4b...: UserAddHp / L1SkillUse.
// See reference/development/status-message-fix-r1/source-evidence.
// This class only routes messages; it never commits HP, magic, trade or equipment state.
internal static class ServerStatusMessages {
    // T228 (#546): an S71 id at or past the string table count is dropped by the original client (sub_942B40),
    // so it must not acknowledge or settle any pending flow; treat it like a status-only message.
    public static bool IsInformational(ushort id,string[] args)=>
        ServerMessageText.IsDropped(id) ||
        id==77&&args.Length==0 ||
        id==161&&args.Length==3&&args[0].Length>0&&args[1]=="$245"&&args[2]=="$247";
    public static bool IsInformational(string text)=>text=="你覺得舒服多了。";
    // S105 screen-center mount; DisplayNotices paints this text and it commits no state.
    public static string? CenterNotice(WorldSession world)=>world.ActiveRedNotice()?.Text;
}
