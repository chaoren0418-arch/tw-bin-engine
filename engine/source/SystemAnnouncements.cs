namespace P06;
public static class SystemAnnouncements {
    // Fixed d287 JAR ServerDeleteItemTimer.runCycle bytecode 42/66/89/250:
    // all four S_SystemMessage(String) calls go to World.broadcastPacketToAll.
    // S243/9 carries no semantic type; only these exact proven forms are announcements.
    public static bool TryClassify(string text,out string display){
        bool known=text is @"\fR地面物品將在 30 秒後清除。" or @"\fR地面物品將在 15 秒後清除。" or @"\fR地面物品將在 5 秒後清除。" or @"\fR已完成清除地面道具。"
            or @"\fY【時空裂痕】出現在遠古戰場！"; // Captured r4 ambient + TimedSpawn/DeAmbience source evidence.
        display=known?text[3..]:"";return known;
    }
}
