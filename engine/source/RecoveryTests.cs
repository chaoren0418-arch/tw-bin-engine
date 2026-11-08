using System.Text.Json;
using P06.NativeUi;
namespace P06;

// M7-X（T224／#531）離線測試：合成封包、合成 XML、假時鐘與假送出閘門；不開遊戲、網路或讀客戶端素材。
public sealed partial class WorldSession {
    internal void SeedRecoveryTestActor(uint id,ushort map){Actor=new OwnActor(32768,32768,id,61,0,0,0,0,0,0,RecoveryTests.Name,"",0,0,"","",0,100,0,1,0,0,0);MapId=map;}
}
public sealed partial class LoginConnection {
    internal void SeedRecoveryTestWorld(uint id,ushort map,Func<byte[],Func<bool>,Action,Task<bool>> sender){World.SeedRecoveryTestActor(id,map);State=LoginState.InWorld;sendMovementGate=sender;}
}

static class RecoveryTests {
    public const string Name="recovery-test";
    const uint Own=123;
    static byte[] Death(uint id)=>LoginPackets.Pad([158,..BitConverter.GetBytes(id),8]);
    static byte[] Revive(uint id)=>LoginPackets.Pad([85,..BitConverter.GetBytes(id),0,0,0,0,0,..BitConverter.GetBytes(61u)]);
    // Full Snapshot() with an actor reads appearance assets; tests read the same view the snapshot hook publishes.
    static DeathView Death(WorldSession s)=>s.DeathSnapshot();

    // Synthetic layout: same element names as the original schema, deliberately different numbers (not a copy of UserPlayUI.xml).
    const string SyntheticXml="""
<?xml version="1.0"?>
<UserPlayStatsUI Toggle="true">
  <Window Name="UserPlayStats_Window" X="0" Y="0" BaseSurf="9001;1" FrameSurf="9002;1">
    <Button Name="CloseWindow_Button" X="200" Y="10" BaseSurf="9010" MouseEvent="CloseWindow"/>
    <Button Name="Restart_Button" X="11" Y="21" BaseSurf="9011" HighlightSurf="9013" PushSurf="9012;1" MouseEvent="Restart"/>
    <Button Name="Quit_Button" X="12" Y="22" BaseSurf="9014" MouseEvent="Quit"/>
    <Button Name="Cancel_Button" X="13" Y="23" BaseSurf="9017" MouseEvent="Cancel"/>
    <List Name="UserPlayStats_List" X="30" Y="100" Width="140" Height="40" Vertical="1" Gap="0">
      <List Name="RowA" Width="140" Height="14" Horizontal="1" Gap="3">
        <Image Name="Icon_Image" Surf="9020;1" Y="2"/>
        <Label Name="A_Label" Width="70" Height="12" String="3123" FontColor="1,2,3"/>
        <Label Name="A_Value" Width="60" Height="12" Caption="0" FontColor="4,5,6"/>
      </List>
      <List Name="RowB" Width="140" Height="14" Horizontal="1">
        <Label Name="B_Label" Width="71" Height="12" String="3999"/>
        <Label Name="B_Value" Width="61" Height="12" Caption="0"/>
      </List>
    </List>
    <List Name="DungeonTime_List" X="30" Y="200"/>
  </Window>
</UserPlayStatsUI>
""";

    [WorldModuleTest("recovery-death")]
    static void Run(WorldModuleTestContext t){
        Rules(t);Watch(t);World(t);Connection(t);Layout(t);
    }

    static void Rules(WorldModuleTestContext t){
        t.Equal(501264,DeathTimeoutRules.QuitTimeoutFor(null),"default timeout 501,264 ms (WO§6)");
        t.Equal(501264,DeathTimeoutRules.QuitTimeoutFor(4),"ordinary map timeout");
        t.Equal(501264,DeathTimeoutRules.QuitTimeoutFor(1016),"map 1016 is outside the long range");
        t.Equal(1801234,DeathTimeoutRules.QuitTimeoutFor(1017),"map 1017 long timeout");
        t.Equal(1801234,DeathTimeoutRules.QuitTimeoutFor(1023),"map 1023 long timeout");
        t.Equal(501264,DeathTimeoutRules.QuitTimeoutFor(1024),"map 1024 is outside the long range");
        t.Equal(5000,DeathTimeoutRules.WindowDelayFor(2600),"map 2600 window delayed 5000 ms");
        t.Equal(0,DeathTimeoutRules.WindowDelayFor(4),"other maps show the window at once");
        t.Equal("7A00000000000000",Convert.ToHexString(DeathTimeoutRules.Quit()),"C122 QUITGAME has no fields, padded to 8");
    }

    static void Watch(WorldModuleTestContext t){
        var w=new DeathWatch();
        t.Equal("alive",w.View(0).Phase,"initially alive");
        t.Check(!w.Dismiss()&&!w.Reopen(),"no window actions while alive");
        w.Update(true,1000,4);
        t.Check(w.WindowOpen(1000)&&!w.QuitDue(1000+501263)&&w.QuitDue(1000+501264),"window at death, quit exactly at 501,264 ms");
        w.Update(true,9000,1017);
        t.Equal((long?)1000+501264,w.QuitAt,"later polls and map changes keep the first death stamp");
        t.Check(w.Dismiss()&&!w.WindowOpen(2000)&&w.QuitDue(1000+501264),"cancel closes the window but the timer keeps running");
        t.Check(!w.Dismiss(),"second dismiss ignored");
        t.Check(w.Reopen()&&w.WindowOpen(2000),"toggle re-opens the window");
        w.MarkQuitSent();
        t.Check(!w.QuitDue(10_000_000)&&!w.WindowOpen(10_000_000)&&!w.Reopen(),"quit sent once; window closed");
        t.Equal("quit-sent",w.View(10_000_000).Phase,"quit phase");
        w.Update(false,3000,4);
        t.Equal("alive",w.View(3000).Phase,"leaving death clears the watch");
        w.Update(true,5000,1020);
        t.Equal((long?)5000+1801234,w.QuitAt,"new death restarts the timer with that map's rule");
        w.Clear();w.Stamp(100,2600);
        t.Check(!w.WindowOpen(5099)&&w.WindowOpen(5100),"map 2600 window delayed 5 s");
        t.Equal("window-delayed",w.View(5099).Phase,"delayed phase");
    }

    static void World(WorldModuleTestContext t){
        var bare=WorldModuleTestContext.SnapshotJson(new WorldSession()).GetProperty("extensions");
        t.Check(bare.TryGetProperty("death",out var hook)&&hook.GetProperty("phase").GetString()=="alive","Snapshot().extensions.death published through the M7-A hook");
        t.Check(WorldExtensionRegistry.Default.Entries.Any(e=>e.Kind=="observe"&&e.Opcode==158&&e.Method.EndsWith("ObserveOwnDeathAction",StringComparison.Ordinal)),"S158 death observer registered");
        long now=50_000;var s=new WorldSession{DeathClock=()=>now};
        s.SeedRecoveryTestActor(Own,4);
        t.Equal("alive",Death(s).Phase,"seeded alive");
        s.Receive(Death(456),Name);
        t.Equal("alive",Death(s).Phase,"another object's death does not start the timer");
        s.Receive(Death(Own),Name);now+=700; // poll happens later than the packet
        var d=Death(s);
        t.Check(s.OwnDead,"S158 action 8 marks own death");
        t.Equal((long?)50_000L,d.DiedAt,"timer starts at the S158 death packet, not at the later poll");
        t.Equal((long?)50_000L+501264,d.QuitAt,"quit deadline");
        t.Check(d.WindowOpen,"UserPlayStatsUI shown on death");
        now=50_000+501263;t.Check(!s.DeathQuitDue,"not due 1 ms early");
        now=50_000+501264;t.Check(s.DeathQuitDue,"due at the deadline while dead");
        t.Check(s.DismissDeathWindow()&&!Death(s).WindowOpen,"cancel hides the window");
        s.Receive(Revive(Own),Name);
        t.Check(!s.OwnDead&&!s.DeathQuitDue,"S85 resurrection stops the timer");
        t.Equal("alive",Death(s).Phase,"S85 closes UserPlayStatsUI");

        var delayed=new WorldSession{DeathClock=()=>now};delayed.SeedRecoveryTestActor(Own,2600);now=1000;
        delayed.Receive(Death(Own),Name);
        t.Equal("window-delayed",Death(delayed).Phase,"map 2600 delays the window");
        now=6000;t.Check(Death(delayed).WindowOpen,"map 2600 window after 5 s");
        delayed.Reset();
        t.Equal("alive",Death(delayed).Phase,"world reset clears the death watch");

        var longMap=new WorldSession{DeathClock=()=>now};longMap.SeedRecoveryTestActor(Own,1019);now=0;
        longMap.Receive(Death(Own),Name);
        now=501264;t.Check(!longMap.DeathQuitDue,"map 1019 not due at the ordinary deadline");
        now=1801234;t.Check(longMap.DeathQuitDue,"map 1019 due at 1,801,234 ms");
    }

    static void Connection(WorldModuleTestContext t){
        long now=0;var sent=new List<byte[]>();bool allow=true;
        Task<bool> Sender(byte[] b,Func<bool> valid,Action admit){if(!allow||!valid())return Task.FromResult(false);sent.Add(b);admit();return Task.FromResult(true);}
        using(var c=new LoginConnection()){
            t.Check(!c.CheckDeathTimeout().GetAwaiter().GetResult()&&sent.Count==0,"offline: nothing sent");
            c.SeedRecoveryTestWorld(Own,4,Sender);c.World.DeathClock=()=>now;
            t.Check(!c.CheckDeathTimeout().GetAwaiter().GetResult()&&sent.Count==0,"alive: nothing sent");
            c.World.Receive(Death(Own),Name);
            t.Check(c.DeathSnapshot().WindowOpen&&c.CanRestart,"dead: window open, restart available");
            now=501263;t.Check(!c.CheckDeathTimeout().GetAwaiter().GetResult()&&sent.Count==0,"before deadline: nothing sent");
            t.Check(c.DismissDeathWindow()&&!c.DeathSnapshot().WindowOpen&&c.ReopenDeathWindow()&&c.DeathSnapshot().WindowOpen,"cancel and re-open through the connection");
            t.Check(c.RestartAfterDeath().GetAwaiter().GetResult()&&sent.Count==1&&sent[0][0]==177,"restart still sends C177");
            t.Check(!c.DeathSnapshot().WindowOpen&&c.DeathSnapshot().Dismissed,"restart closes the window (WO§6)");
            now=501264;allow=false;
            t.Check(!c.CheckDeathTimeout().GetAwaiter().GetResult()&&sent.Count==1&&c.State==LoginState.InWorld,"gate refusal: nothing sent, still connected");
            allow=true;
            t.Check(c.CheckDeathTimeout().GetAwaiter().GetResult(),"timeout while still dead sends C_QUIT");
            t.Equal(2,sent.Count,"exactly one quit packet");
            t.Equal("7A00000000000000",Convert.ToHexString(sent[^1]),"quit bytes are C122");
            t.Equal(LoginState.Disconnected,c.State,"connection closed after the quit packet");
            t.Check(!c.CheckDeathTimeout().GetAwaiter().GetResult()&&sent.Count==2,"no second quit");
        }
        sent.Clear();now=0;
        using(var c=new LoginConnection()){
            c.SeedRecoveryTestWorld(Own,4,Sender);c.World.DeathClock=()=>now;
            c.World.Receive(Death(Own),Name);c.World.Receive(Revive(Own),Name);now=600_000;
            t.Check(!c.CheckDeathTimeout().GetAwaiter().GetResult()&&sent.Count==0&&c.State==LoginState.InWorld,"resurrected before the deadline: no quit");
        }
        sent.Clear();now=0;
        using(var c=new LoginConnection()){
            // Resurrection between the due check and the gate: the gate's validity re-check drops the quit.
            bool reviveFirst=true;
            c.SeedRecoveryTestWorld(Own,4,(b,valid,admit)=>{if(reviveFirst){reviveFirst=false;c.World.Receive(Revive(Own),Name);}if(!valid())return Task.FromResult(false);sent.Add(b);admit();return Task.FromResult(true);});
            c.World.DeathClock=()=>now;c.World.Receive(Death(Own),Name);now=501264;
            t.Check(!c.CheckDeathTimeout().GetAwaiter().GetResult()&&sent.Count==0&&c.State==LoginState.InWorld,"late resurrection cancels the queued quit");
        }
    }

    static void Layout(WorldModuleTestContext t){
        var layout=UserPlayStatsLayout.From(UiLayout.Parse(SyntheticXml,"UserPlayUI-c.xml"));
        t.Equal((int?)9001,layout.BaseSurf,"window base surf, first number of \"9001;1\"");
        t.Equal((int?)9002,layout.FrameSurf,"frame surf");
        t.Check(layout.Toggle,"Toggle=true kept");
        t.Equal("11,21,9011,Restart|12,22,9014,Quit|13,23,9017,Cancel",string.Join("|",new[]{layout.Restart,layout.Quit,layout.Cancel}.Select(b=>$"{b.X},{b.Y},{b.BaseSurf},{b.MouseEvent}")),"button coordinates and surfs read from XML");
        t.Equal("200,10,CloseWindow",layout.Close is {} close?$"{close.X},{close.Y},{close.MouseEvent}":"none","close button optional but read");
        t.Equal(30,layout.ListX,"stats list X");t.Equal(100,layout.ListY,"stats list Y");t.Equal(14,layout.RowHeight,"row height");
        t.Equal(2,layout.Rows.Count,"only UserPlayStats_List rows, not dungeon times");
        var a=layout.Rows[0];var b=layout.Rows[1];
        t.Equal("RowA,9020,2,3123,70,60,3",$"{a.Name},{a.IconSurf},{a.IconY},{a.StringId},{a.LabelWidth},{a.ValueWidth},{a.Gap}","row A fields");
        t.Equal(Color.FromArgb(1,2,3).ToArgb(),a.LabelColor.ToArgb(),"label FontColor");
        t.Equal(Color.FromArgb(4,5,6).ToArgb(),a.ValueColor.ToArgb(),"value FontColor");
        t.Equal("RowB,,3999,0",$"{b.Name},{b.IconSurf},{b.StringId},{b.Gap}","row without icon: no surf drawn");
        t.Throws<UiLayoutException>(()=>UserPlayStatsLayout.From(UiLayout.Parse(SyntheticXml.Replace("Restart_Button","Other_Button"),"x.xml")),"missing restart button rejected, no default coordinates");
        t.Throws<UiLayoutException>(()=>UserPlayStatsLayout.From(UiLayout.Parse(SyntheticXml.Replace("UserPlayStatsUI","OtherUI"),"x.xml")),"wrong root rejected");
        t.Throws<UiLayoutException>(()=>UserPlayStatsLayout.From(UiLayout.Parse(SyntheticXml.Replace("\"9011\"","\"abc\""),"x.xml")),"non-numeric surf rejected");
        t.Throws<UiLayoutException>(()=>UserPlayStatsLayout.From(UiLayout.Parse(SyntheticXml.Replace("\"1,2,3\"","\"1,2\""),"x.xml")),"bad FontColor rejected");
    }
}
