using P06;
using System.Buffers.Binary;
using System.Text;

// T172（#446）：背景音樂、戰鬥音樂、登入音樂與 envsound.def 地圖環境音的離線檢查。
// 只用合成的定義檔文字、合成的 Text.idx／Text.pak 與記錄器後端；不播放聲音、不讀遊戲素材（實際客戶端檢查只在本機存在時執行，唯讀）。
partial class Checks {
    sealed class MusicRecorder : IMusicStreamAudio {
        public readonly List<(MusicTrack Track, double Volume)> Started = [];
        public readonly List<MusicTrack> Stopped = [];
        public readonly List<(MusicTrack Track, double Volume)> Volumes = [];
        public readonly HashSet<long> Done = [];
        public readonly HashSet<int> Missing = [];
        public bool Start(MusicTrack track, double volume) { if (Missing.Contains(track.Music)) return false; Started.Add((track, volume)); return true; }
        public void SetVolume(MusicTrack track, double volume) => Volumes.Add((track, volume));
        public void Stop(MusicTrack track) => Stopped.Add(track);
        public bool Finished(MusicTrack track) => Done.Contains(track.Number);
        public MusicTrack Last => Started[^1].Track;
    }
    sealed class EnvRecorder : IEnvSoundAudio {
        public readonly List<(EnvSoundVoice Voice, double Volume)> Started = [];
        public readonly List<EnvSoundVoice> Stopped = [];
        public readonly Dictionary<long, double> Volume = [];
        public readonly HashSet<int> Missing = [];
        public bool Start(EnvSoundVoice voice, double volume) { if (Missing.Contains(voice.SoundId)) return false; Started.Add((voice, volume)); Volume[voice.Number] = volume; return true; }
        public void SetVolume(EnvSoundVoice voice, double volume) => Volume[voice.Number] = volume;
        public void Stop(EnvSoundVoice voice) => Stopped.Add(voice);
        public EnvSoundVoice[] OneShots => Started.Where(s => !s.Voice.Loop).Select(s => s.Voice).ToArray();
    }

    // 合成的 music.def：系統段 + 原檔各種陷阱（CRLF、分號 token、空白 INTERVAL、BATTLE 多值、\ 串接、末尾 \、只有 map x y）。
    // 系統段用自編的名稱與數值（不複製原檔內容），只保留測試需要的槽值：0→82、4→121、8→5、9→6、10→7、11→8、18→120、20→17。
    static readonly int[] SyntheticSlots = [82, 101, 102, 103, 121, 104, 105, 106, 5, 6, 7, 8, 107, 108, 8, 109, 110, 111, 120, 112, 17];
    static readonly string SyntheticMusicDef =
        "System Setting Begin\r\n" + string.Concat(SyntheticSlots.Select((v, i) => $"\tSlotName{i}={v};\r\n")) + "\t;note=999\r\n\tNoEquals\r\nSystemMenu Exit\r\n\r\n" +
        ";TOWN 1~3F\r\nLOC          0 32549 32905 32604 32980\r\nMUSIC        30\r\nINTERVAL  180\r\nSPRITE 11306\r\nSPAWN 1230\r\nBATTLE -1\r\n\r\n" +
        ";ISLAND\r\nLOC\t\t0\r\nMUSIC\t\t12\r\nINTERVAL\t\r\nSPRITE 11306\r\nBATTLE -1\r\n\r\n" +
        ";DUNGEON\r\nLOC           1 \\ 2\r\nMUSIC        63 \r\nINTERVAL   180\r\n\r\n" +
        ";NEW DUNGEON 1~7F\r\nLOC 807 \\ 808\r\n  \\ 809 \\\r\nMUSIC 65 64 66\r\nINTERVAL 10\r\n\r\n" +
        ";BOSS\r\nLOC 30 32700 32800 32710 32810\r\nMUSIC 40 41\r\nINTERVAL 5\r\nSPRITE 1500\r\nBATTLE 162 163 164 165 166\r\nDEATH 120\r\n\r\n" +
        ";TWIN\r\nLOC 31\r\nMUSIC 42\r\nSPRITE 1600\r\nTWIN_SPRITE1 1601\r\nTWIN_SPRITE2 1602\r\nBATTLE 170\r\n\r\n" +
        ";SHORT\r\nLOC 40 32000 32100 \\ 41 0\r\nMUSIC 44\r\n";

    const string SyntheticEnvDef =
        ";Sonud Group (soundnumber:total_time:panning)\r\n\r\n" +
        "[BIRD_SOUND:80]\t\r\n\t2100 2101 2102\r\n[/BIRD_SOUND]\r\n\r\n" +
        "[DEEPFOREST:30]\r\n2257 2258:7 2259:8:3\r\n[/DEEPFOREST0]\r\n\r\n" +
        "[NIGHTBIRD:105]\r\n 2113\r\n[/NIGHTBIRD]\r\n\r\n" +
        ";<LOC>99</LOC> commented\r\n" +
        "      <LOC>2005</LOC>\r\n      <TIME>DAY</TIME>\r\n      <MAIN_SOUND>2053 2333</MAIN_SOUND>\r\n      <ONE_SOUND>BIRD_SOUND Chime_SOUND</ONE_SOUND>\r\n\r\n" +
        "      <TIME>NIGHT</TIME>\r\n      <MAIN_SOUND>2013</MAIN_SOUND>\r\n      <ONE_SOUND>NIGHTBIRD bird_sound</ONE_SOUND>\r\n\r\n" +
        "      <LOC>737</LOC>\r\n      <WEATHER>RAIN</WEATHER>\r\n      <MAIN_SOUND>2333</MAIN_SOUND>\r\n      <ONESOUND> </ONESOUND>\r\n\r\n" +
        "        <LOC>4 32512 32650 32583 32711 \\ 4 32512 32711 32569 32741 \\\r\n 4 32700 32700 32710 32800 \\ 5 1 2 3 </LOC>\r\n" +
        "        <MAIN_SOUND>2005</MAIN_SOUND>\r\n        <ONE_SOUND>DEEPFOREST INSECT SOUND</ONE_SOUND>\r\n";

    static void BackgroundMusicChecks() {
        MusicDefinitionChecks();
        EnvSoundDefinitionChecks();
        SoundArchiveChecks();
        BackgroundMusicControllerChecks();
        MapEnvSoundChecks();
        ClientSoundSceneChecks();
        BackgroundMusicRealClientChecks();
    }

    static void MusicDefinitionChecks() {
        var def = MusicDefinition.Parse(SyntheticMusicDef);
        Check(def.SystemSettings == 21 && def.Slots.Take(21).SequenceEqual(SyntheticSlots) && def.Slot(0) == 82 && def.Slot(4) == 121 && def.Slot(8) == 5 && def.Slot(9) == 6 && def.Slot(10) == 7 && def.Slot(11) == 8 && def.Slot(18) == 120 && def.Slot(20) == 17, "music system slots in appearance order; `;` token and lines without = skipped");
        Check(def.Slot(21) == 21 && def.Slot(201) == 201 && def.Slot(202) == -1 && def.Slot(-1) == -1, "music slots beyond file keep i→i default; out of range -1");
        Check(def.Regions.Length == 7, "music region count");
        var town = def.Regions[0];
        Check(town.Areas.Single() == new MusicArea(0, 32549, 32905, 32604, 32980) && town.Music.SequenceEqual([30]) && town.Interval == 180 && town.Sprite == 11306 && town.Spawn == 1230 && town.Battle == -1, "music town record fields; BATTLE -1 keeps initial -1");
        var island = def.Regions[1];
        Check(island.Areas.Single() == new MusicArea(0, 0, 32905, 32604, 32980) && island.Interval == -1 && island.Sprite == 11306, "music map-only LOC resets map/x1 only; blank INTERVAL stays -1 and SPRITE still parsed");
        Check(def.Regions[2].Areas.Select(a => a.Map).SequenceEqual([1, 2]) && def.Regions[2].Music.SequenceEqual([63]), "music backslash joins maps");
        Check(def.Regions[3].Areas.Select(a => a.Map).SequenceEqual([807, 808, 809]) && def.Regions[3].Music.SequenceEqual([65, 64, 66]), "music multi-line LOC with trailing backslash; MUSIC list");
        var boss = def.Regions[4];
        Check(boss.Battle == 166 && boss.Death == 120 && boss.Sprite == 1500 && !boss.TwinBoss, "music single-value fields keep last value (BATTLE 162..166 → 166)");
        Check(def.Regions[5].TwinBoss && def.Regions[5].Interval == -1, "music twin sprites both valid");
        var shortLoc = def.Regions[6];
        Check(shortLoc.Areas[0].Map == 40 && shortLoc.Areas[0].X1 == 32000 && shortLoc.Areas[0].Y1 == 32100 && shortLoc.Areas[0].X2 == 32710 && shortLoc.Areas[0].Y2 == 32810, "music `map x y` keeps stale x2/y2 from earlier LOC (no circle/rect invention)");
        Check(shortLoc.Areas[1] == new MusicArea(41, 0, 32100, 32710, 32810), "music `map 0` segment is whole map");

        // 選曲：同圖特定區域先於整圖；包含右下邊界；第一筆命中；大編號地圖不查表。
        Check(def.Region(0, 32549, 32905) == town && def.Region(0, 32604, 32980) == town && def.Region(0, 32605, 32980) == island && def.Region(0, 32548, 32905) == island, "music rect inclusive both edges, earlier record wins");
        Check(def.Region(2, 1, 1) == def.Regions[2] && def.Region(3, 1, 1) == null && def.Region(15001, 0, 0) == null, "music map match; unknown map null; >15000 not looked up");
        Check(new MusicArea(5, 10, 10, 20, 20).Contains(5, 20, 20) && !new MusicArea(5, 10, 10, 20, 20).Contains(5, 21, 20) && !new MusicArea(6, 10, 10, 20, 20).Contains(5, 15, 15), "music area contains");
        // 分號只略過該 token，註解內的數字在頂層被忽略；LOC 之前的 MUSIC 不建立記錄。
        var odd = MusicDefinition.Parse("MUSIC 9\n;C 1 2\nLOC 7 ;x\nMUSIC 3 ;y 4\nINTERVAL 2x\nLOC 8\n");
        Check(odd.Regions.Length == 2 && odd.Regions[0].Music.SequenceEqual([3]) && odd.Regions[0].Interval == 2 && odd.SystemSettings == 0 && odd.Slot(0) == 0, "music parser token-level comments, atoi prefix, no system section keeps defaults");
        Check(MusicDefinition.Parse("").Regions.Length == 0 && MusicDefinition.Parse("LOC \\ \\").Regions.Single().Areas.Length == 0, "music empty input / LOC with no maps");
    }

    static void EnvSoundDefinitionChecks() {
        var def = EnvSoundDefinition.Parse(SyntheticEnvDef);
        Check(def.Groups.Length == 3 && def.Groups[0].Name == "BIRD_SOUND" && def.Groups[0].Seconds == 80 && def.Groups[0].Candidates.Select(c => c.Id).SequenceEqual([2100, 2101, 2102]) && def.Groups[0].Candidates.All(c => c.Seconds == 80 && c.Panning == -1), "env group header seconds default to candidates");
        var deep = def.Groups[1];
        Check(deep.Name == "DEEPFOREST" && deep.Candidates.SequenceEqual([new EnvSoundCandidate(2257, 30, -1), new EnvSoundCandidate(2258, 7, -1), new EnvSoundCandidate(2259, 8, 3)]), "env candidate id:seconds:panning; closer matched by prefix ([/DEEPFOREST0])");
        Check(def.Regions.Length == 4, "env region versions (DAY, NIGHT, rain-only main, rect)");
        var day = def.Regions[0]; var night = def.Regions[1];
        Check(day.Time == EnvSoundTime.Day && day.MainSound == 2333 && day.OneSounds.Select(g => g.Name).SequenceEqual(["BIRD_SOUND"]) && day.Areas.Single() == new EnvSoundArea(2005, 0, 0, 0, 0), "env DAY: MAIN last number wins; unknown group ignored");
        Check(night.Time == EnvSoundTime.Night && night.MainSound == 2013 && night.Areas.Single().Map == 2005 && night.OneSounds.Select(g => g.Name).SequenceEqual(["NIGHTBIRD"]), "env NIGHT keeps LOC list, resets sounds; group names case-sensitive");
        Check(ReferenceEquals(day.OneSounds[0], def.Groups[0]), "env groups are shared objects");
        var rain = def.Regions[2];
        Check(rain.Time == EnvSoundTime.Any && rain.MainSound == 2333 && rain.OneSounds.Length == 0, "env WEATHER and ONESOUND have no branch");
        var rect = def.Regions[3];
        Check(rect.Areas.Length == 4 && rect.Areas[3] == new EnvSoundArea(5, 1, 2, 3, 0) && rect.OneSounds.Select(g => g.Name).SequenceEqual(["DEEPFOREST"]), "env four-int segment takes first as map; `INSECT SOUND` splits to undefined names");
        Check(def.Regions.All(r => r.Areas.All(a => a.Map != 99)), "env semicolon lines skipped whole");
        // 選擇：時間、矩形右下不含、x1 == x2 不限。
        Check(def.Region(2005, 1, 1, false) == day && def.Region(2005, 1, 1, true) == night && def.Region(737, 5, 5, true) == rain, "env time-filtered first match");
        Check(def.Region(4, 32512, 32650, false) == rect && def.Region(4, 32582, 32710, false) == rect && def.Region(4, 32583, 32700, false) == null && def.Region(4, 32570, 32741, false) == null, "env rect half-open (x1≤x<x2, y1≤y<y2)");
        Check(def.Region(4, 1, 1, false) == null && EnvSoundDefinition.Parse("<LOC>4 5 6 5 9</LOC><MAIN_SOUND>1</MAIN_SOUND>").Region(4, 1000, 1000, false) != null, "env x1==x2 means whole map");
        Check(EnvSoundDefinition.Parse("<MAIN_SOUND>5</MAIN_SOUND><LOC>1</LOC><TIME>DAY</TIME><TIME>NIGHT</TIME><MAIN_SOUND>9</MAIN_SOUND>").Regions is [{ Time: EnvSoundTime.Day, MainSound: -1 }, { Time: EnvSoundTime.Night, MainSound: 9 }], "env sound before LOC ignored; TIME commits previous version even if empty");
    }

    static string SoundTestDir(string name) {
        string dir = Path.Combine(Directory.GetCurrentDirectory(), "scratch", "background-music-tests", name);
        Directory.CreateDirectory(dir); return dir;
    }
    static void WriteTextArchive(string dir, params (string Name, byte[] Data)[] items) {
        var pak = new MemoryStream(); var rows = new byte[items.Length * 28];
        for (int i = 0; i < items.Length; i++) {
            var row = rows.AsSpan(i * 28, 28);
            BinaryPrimitives.WriteUInt32LittleEndian(row, (uint)pak.Length);
            var name = Encoding.ASCII.GetBytes(items[i].Name); name.AsSpan(0, Math.Min(20, name.Length)).CopyTo(row[4..]);
            BinaryPrimitives.WriteUInt32LittleEndian(row[24..], (uint)items[i].Data.Length);
            pak.Write(SoundDefinitionSource.Transform(items[i].Data, true));
        }
        var idx = new byte[4 + rows.Length]; BinaryPrimitives.WriteUInt32LittleEndian(idx, (uint)items.Length);
        SoundDefinitionSource.Transform(rows, true).CopyTo(idx, 4);
        File.WriteAllBytes(Path.Combine(dir, "Text.idx"), idx); File.WriteAllBytes(Path.Combine(dir, "Text.pak"), pak.ToArray());
    }

    static void SoundArchiveChecks() {
        var tail = Encoding.ASCII.GetBytes("ABCDEFGHIJ"); // 10 bytes：前 8 轉換、尾端 2 bytes 原樣。
        var enc = SoundDefinitionSource.Transform(tail, true);
        Check(enc.AsSpan(8).SequenceEqual(tail.AsSpan(8)) && !enc.AsSpan(0, 8).SequenceEqual(tail.AsSpan(0, 8)) && SoundDefinitionSource.Transform(enc, false).SequenceEqual(tail), "Text DES-ECB full blocks only; partial tail unchanged");
        Check(SoundDefinitionSource.Transform([], false).Length == 0, "Text transform empty");

        string ok = SoundTestDir("ok");
        WriteTextArchive(ok, ("list.spr", Encoding.ASCII.GetBytes("#0 x")), ("ABCDEFGHIJKLMNOPQRST", [1, 2, 3]), ("music.def", Encoding.ASCII.GetBytes(SyntheticMusicDef)), ("envsound.def", Encoding.ASCII.GetBytes(SyntheticEnvDef + "1234567")));
        var loaded = SoundDefinitionSource.Load(ok);
        Check(loaded.Status == "ready" && loaded.Music!.Regions.Length == 7 && loaded.Music.Slot(0) == 82 && loaded.EnvSound!.Regions.Length == 4, "Text archive loads both definitions (20-byte name without NUL tolerated)");

        string upper = SoundTestDir("upper");
        WriteTextArchive(upper, ("MUSIC.DEF", Encoding.ASCII.GetBytes(SyntheticMusicDef)));
        var partial = SoundDefinitionSource.Load(upper);
        Check(partial.Music != null && partial.EnvSound == null && partial.Status.Contains("envsound.def"), "Text archive case-insensitive name; missing envsound reported");

        string dup = SoundTestDir("dup");
        WriteTextArchive(dup, ("music.def", [65]), ("music.def", [66]));
        Check(SoundDefinitionSource.Load(dup) is { Music: null, EnvSound: null } d && d.Status.StartsWith("讀取失敗"), "Text archive duplicate target rejected");

        Check(SoundDefinitionSource.Load(SoundTestDir("missing")).Status.Contains("不存在"), "Text archive missing files → no definitions");

        var idx = new byte[4 + 28]; BinaryPrimitives.WriteUInt32LittleEndian(idx, 1);
        var row = new byte[28]; BinaryPrimitives.WriteUInt32LittleEndian(row, 100); Encoding.ASCII.GetBytes("music.def").CopyTo(row, 4); BinaryPrimitives.WriteUInt32LittleEndian(row.AsSpan(24), 10);
        SoundDefinitionSource.Transform(row, true).CopyTo(idx, 4);
        Throws(() => SoundDefinitionSource.Index(idx, 50, "music.def"), "Text index entry beyond pak rejected");
        Throws(() => SoundDefinitionSource.Index(idx[..30], 500, "music.def"), "Text index not plain28 rejected");
        Throws(() => SoundDefinitionSource.Index([1, 0], 500), "Text index truncated rejected");
        Check(SoundDefinitionSource.Index(idx, 110, "music.def")["MUSIC.DEF"] == (100u, 10u), "Text index offset/size");
    }
    static void Throws(Action action, string label) { try { action(); } catch (InvalidDataException) { Check(true, label); return; } Check(false, label); }

    static MusicContext World(int map, int x, int y, bool enabled = true, double volume = 1) => new(MusicScene.World, map, x, y, enabled, volume);

    static void BackgroundMusicControllerChecks() {
        var def = MusicDefinition.Parse(SyntheticMusicDef);
        // 登入：系統槽 0（music82），單次；播完後每秒檢查再播。
        var rec = new MusicRecorder(); var bgm = new BackgroundMusic(def, rec, new Random(1));
        bgm.Update(new(MusicScene.Login, 0, 0, 0, true, .5), 0);
        Check(rec.Started.Count == 1 && rec.Last is { Music: 82, Loop: false, Kind: MusicKind.Login } && rec.Started[0].Volume == .5, "login plays slot 0 (music82) once at music volume");
        bgm.Update(new(MusicScene.Login, 0, 0, 0, true, .5), 500);
        rec.Done.Add(rec.Last.Number);
        bgm.Update(new(MusicScene.Login, 0, 0, 0, true, .5), 900);
        Check(rec.Started.Count == 1 && !bgm.Playing, "login finished track waits for 1 s check");
        bgm.Update(new(MusicScene.Login, 0, 0, 0, true, .5), 1000);
        Check(rec.Started.Count == 2 && rec.Last.Music == 82, "login replays at next 1 s check");
        // 選角：系統槽 8（music5）循環。
        bgm.Update(new(MusicScene.CharacterSelect, 0, 0, 0, true, 1), 1100);
        Check(rec.Last is { Music: 5, Loop: true, Kind: MusicKind.CharacterSelect } && rec.Stopped.Count >= 2, "character select loops slot 8 (music5), login stopped");
        bgm.Update(new(MusicScene.CharacterSelect, 0, 0, 0, true, 1), 1200);
        Check(rec.Started.Count == 3, "character select not restarted while playing");

        // 進入世界：區域選曲（實際編號，不查系統槽）。
        bgm.Update(World(0, 32570, 32940), 2000);
        Check(rec.Last is { Music: 30, Loop: false, Kind: MusicKind.Region } && bgm.Region == def.Regions[0], "world town region plays music30 once");
        // INTERVAL 180：開始時間 + 180 s 之後才換下一首。
        rec.Done.Add(rec.Last.Number);
        bgm.Update(World(0, 32570, 32940), 100_000);
        Check(rec.Started.Count == 4 && !bgm.Playing, "finished region track waits INTERVAL from start time");
        bgm.Update(World(0, 32570, 32940), 2000 + 180_000 - 1);
        Check(rec.Started.Count == 4, "INTERVAL not yet elapsed");
        bgm.Update(World(0, 32570, 32940), 2000 + 180_000);
        Check(rec.Started.Count == 5 && rec.Last.Music == 30, "INTERVAL elapsed → next track");

        // 多首候選：不連續重複同一候選索引；INTERVAL 10。
        var rec2 = new MusicRecorder(); var dungeon = new BackgroundMusic(def, rec2, new Random(7)); long t = 0;
        dungeon.Update(World(808, 1, 1), t);
        for (int i = 0; i < 60; i++) { rec2.Done.Add(rec2.Last.Number); t += 10_000; dungeon.Update(World(808, 1, 1), t); }
        var picks = rec2.Started.Select(s => s.Track.Music).ToArray();
        Check(picks.Length == 61 && picks.Zip(picks.Skip(1)).All(p => p.First != p.Second) && picks.Distinct().Order().SequenceEqual([64, 65, 66]), "multi-track region never repeats consecutively, uses all candidates");

        // 空白 INTERVAL（-1）：播完即換。
        var rec3 = new MusicRecorder(); var island = new BackgroundMusic(def, rec3, new Random(3));
        island.Update(World(0, 1, 1), 0); rec3.Done.Add(rec3.Last.Number); island.Update(World(0, 1, 1), 1);
        Check(rec3.Started.Count == 2 && rec3.Last.Music == 12, "INTERVAL -1 restarts immediately after finish");

        // 戰鬥：立即切到槽 9（music6）；對 PC 是槽 10（music7）；同曲不重開。
        var rec4 = new MusicRecorder(); var fight = new BackgroundMusic(def, rec4, new Random(4));
        fight.Update(World(0, 32570, 32940, volume: .8), 0);
        fight.Attack(false, 1000);
        Check(rec4.Last is { Music: 6, Kind: MusicKind.Battle } && rec4.Stopped.Count == 1 && rec4.Started[^1].Volume == .8, "attack switches immediately to hunting music (slot 9 → music6)");
        fight.Attack(false, 2000);
        Check(rec4.Started.Count == 2, "same battle track not restarted while playing");
        fight.Attack(true, 3000);
        Check(rec4.Last.Music == 7 && rec4.Started.Count == 3, "attack on PC uses slot 10 (music7)");
        fight.Update(World(0, 32570, 32940, volume: .8), 3000 + 15_000);
        Check(!fight.Fading, "battle holds for 15 s");
        fight.Update(World(0, 32570, 32940, volume: .8), 3000 + 15_001 + 999);
        Check(fight.Fading && fight.FadeLevel(3000 + 15_001 + 999) == 127, "battle > 15 s → fade starts at next 1 s check");
        long f0 = 3000 + 15_001 + 999; rec4.Volumes.Clear();
        for (int step = 1; step <= 15; step++) fight.Update(World(0, 32570, 32940, volume: .8), f0 + step * 50);
        Check(rec4.Volumes.Count == 15 && Math.Abs(rec4.Volumes[0].Volume - .8 * 119 / 127) < 1e-9 && Math.Abs(rec4.Volumes[^1].Volume - .8 * 7 / 127) < 1e-9 && rec4.Started.Count == 3, "fade −8 per 50 ms (127→7 over 15 steps)");
        fight.Update(World(0, 32570, 32940, volume: .8), f0 + 800);
        Check(!fight.Fading && rec4.Last is { Music: 30, Kind: MusicKind.Region } && rec4.Started[^1].Volume == .8, "fade reaches 0 at 0.8 s → region track at full volume, no fade-in");
        // 戰鬥中播完：保持安靜，直到下一次攻擊。
        fight.Attack(false, 50_000); rec4.Done.Add(rec4.Last.Number); fight.Update(World(0, 32570, 32940), 51_000);
        Check(rec4.Last.Kind == MusicKind.Battle && !fight.Playing, "finished battle track stays silent during battle");
        fight.Attack(false, 52_000);
        Check(rec4.Last.Kind == MusicKind.Battle && fight.Playing && rec4.Started.Count == 6, "next attack restarts battle track");
        fight.BattleMusicEnabled = false; fight.Attack(true, 53_000);
        Check(rec4.Started.Count == 6 && fight.LastBattle == 53_000, "battle music option off: time recorded, no switch");

        // 換區：目前曲目不在新候選中 → 0.8 秒淡出後切換；在候選中則不打斷。
        var rec5 = new MusicRecorder(); var walk = new BackgroundMusic(def, rec5, new Random(5));
        walk.Update(World(0, 32570, 32940), 0);
        walk.Update(World(0, 1, 1), 100);
        Check(walk.Fading && rec5.Started.Count == 1, "region change fades out current");
        walk.Update(World(0, 1, 1), 900);
        Check(rec5.Last.Music == 12 && !walk.Fading, "after fade the new region track starts");
        walk.Update(World(15001, 1, 1), 1000); walk.Update(World(15001, 1, 1), 1800);
        Check(rec5.Last is { Music: 23, Kind: MusicKind.LargeMap }, "map > 15000 fixed music23");
        walk.Update(World(3, 1, 1), 1900); walk.Update(World(3, 1, 1), 2700);
        Check(walk.Current == null && !walk.Playing && rec5.Stopped.Count >= 3, "no record → silence after fade");

        // 頭目：SPRITE 登記後改播 BATTLE（單值最後值 166）；雙頭目需兩個都登記。
        var rec6 = new MusicRecorder(); var bossBgm = new BackgroundMusic(def, rec6, new Random(6));
        bossBgm.Update(World(30, 32705, 32805), 0);
        Check(rec6.Last.Music is 40 or 41, "boss region normal music before sprite");
        bossBgm.RegisterBoss(1500); bossBgm.Update(World(30, 32705, 32805), 100); bossBgm.Update(World(30, 32705, 32805), 900);
        Check(rec6.Last is { Music: 166, Kind: MusicKind.Boss }, "registered boss sprite switches to BATTLE track");
        var rec7 = new MusicRecorder(); var twin = new BackgroundMusic(def, rec7, new Random(6));
        twin.RegisterBoss(1601); twin.Update(World(31, 1, 1), 0);
        Check(rec7.Last.Music == 42, "twin boss needs both sprites");
        twin.RegisterBoss(1602); twin.Update(World(31, 1, 1), 10); twin.Update(World(31, 1, 1), 810);
        Check(rec7.Last.Music == 170, "twin boss both registered → BATTLE");

        // 停用／靜音：立即停止；重新啟用回區域音樂。
        bossBgm.Update(World(30, 32705, 32805, enabled: false), 2000);
        Check(bossBgm.Current == null && rec6.Stopped.Count >= 2, "music disabled stops immediately");
        bossBgm.Attack(false, 2100);
        Check(rec6.Started.Count == 2, "attack while disabled does not start music");
        bossBgm.Update(World(30, 32705, 32805), 2200);
        Check(rec6.Last.Music == 166 && bossBgm.Playing, "re-enabled returns to region music immediately");

        // 保護曲目：正在播槽 12/13/15/16/17 的曲目時，戰鬥與淡出都不打斷。
        var protectedDef = MusicDefinition.Parse("LOC 50\nMUSIC 13\nINTERVAL 1\nLOC 51\nMUSIC 1\n");
        var rec8 = new MusicRecorder(); var prot = new BackgroundMusic(protectedDef, rec8, new Random(8));
        prot.Update(World(50, 0, 0), 0); prot.Attack(false, 10); prot.Update(World(51, 0, 0), 20);
        Check(rec8.Started.Count == 1 && !prot.Fading && rec8.Last.Music == 13, "uninterruptible slot track (music13 = slot 13 default) not interrupted");

        // 缺檔：後端拒絕時每秒最多重試一次。
        var rec9 = new MusicRecorder(); rec9.Missing.Add(30); var miss = new BackgroundMusic(def, rec9, new Random(9));
        miss.Update(World(0, 32570, 32940), 0); miss.Update(World(0, 32570, 32940), 500);
        Check(rec9.Started.Count == 0 && miss.Current == null, "missing track does not start");
        rec9.Missing.Clear(); miss.Update(World(0, 32570, 32940), 999);
        Check(rec9.Started.Count == 0, "retry throttled within 1 s");
        miss.Update(World(0, 32570, 32940), 1000);
        Check(rec9.Started.Count == 1 && rec9.Last.Music == 30, "retry after 1 s");
        // 音量跟隨 MusicVolume。
        miss.Update(World(0, 32570, 32940, volume: .25), 1100);
        Check(rec9.Volumes[^1].Volume == .25, "music volume follows category gain");
        miss.Update(new(MusicScene.None, 0, 0, 0, true, 1), 1200);
        Check(miss.Current == null, "scene None stops music");
    }

    static EnvSoundContext Env(int map, int x, int y, bool night = false, bool enabled = true, double volume = 1) => new(map, x, y, night, enabled, volume);

    static void MapEnvSoundChecks() {
        var def = EnvSoundDefinition.Parse(SyntheticEnvDef);
        var rec = new EnvRecorder(); var env = new MapEnvSound(def, rec, new Random(11), new SoundCooldown());
        env.Update(Env(2005, 1, 1, volume: .6), 0);
        var main = rec.Started.First(s => s.Voice.Loop);
        Check(main.Voice.SoundId == 2333 && main.Volume == 0 && env.MainSound == 2333, "env main sound starts looping at 0 (fade-in)");
        env.Update(Env(2005, 1, 1, volume: .6), 40);
        Check(Math.Abs(rec.Volume[main.Voice.Number] - .6 / 25) < 1e-9, "env fade-in 40 ms per step (1/25)");
        env.Update(Env(2005, 1, 1, volume: .6), 999);
        Check(Math.Abs(rec.Volume[main.Voice.Number] - .6 * 24 / 25) < 1e-9, "env fade-in not complete before 1 s");
        env.Update(Env(2005, 1, 1, volume: .6), 1000);
        Check(Math.Abs(rec.Volume[main.Voice.Number] - .6) < 1e-9, "env fade-in complete at 1 s");
        // ONE_SOUND：第一次檢查（t=0）已抽 BIRD_SOUND 播一次；群組冷卻 80 秒。
        var first = rec.OneShots;
        Check(first.Length == 1 && first[0].SoundId is >= 2100 and <= 2102, "env ONE_SOUND plays one candidate from group");
        long t = 0;
        for (int i = 0; i < 79; i++) { t += 999; env.Update(Env(2005, 1, 1), t); }
        Check(rec.OneShots.Length == 1, "env group cooldown 80 s blocks (no other group drawn)");
        t = 80_000; env.Update(Env(2005, 1, 1), t);
        Check(rec.OneShots.Length == 2 && rec.OneShots[1].SoundId != first[0].SoundId, "env group plays again after cooldown, different candidate index");
        // 夜間：換主音（舊主音 1 秒淡出、新主音淡入）。
        env.Update(Env(2005, 1, 1, night: true), t + 1);
        var nightMain = rec.Started.Last(s => s.Voice.Loop).Voice;
        Check(nightMain.SoundId == 2013 && env.Mains(t + 1).Count == 2, "env night switches main sound with crossfade");
        env.Update(Env(2005, 1, 1, night: true), t + 1 + 1000);
        Check(rec.Stopped.Contains(main.Voice) && env.Mains(t + 1001).Single().Voice == nightMain, "env old main stopped after 1 s fade-out");
        // 區域外：主音淡出，沒有新主音。
        env.Update(Env(9, 1, 1), t + 2000);
        env.Update(Env(9, 1, 1), t + 3000);
        Check(rec.Stopped.Contains(nightMain) && env.MainSound == -1 && env.Mains(t + 3000).Count == 0, "env leaving region fades main out");
        // 同 id 主音跨區不重開。
        var rec2 = new EnvRecorder(); var same = new MapEnvSound(EnvSoundDefinition.Parse("<LOC>1</LOC><MAIN_SOUND>7</MAIN_SOUND><LOC>2</LOC><MAIN_SOUND>7</MAIN_SOUND>"), rec2, new Random(1));
        same.Update(Env(1, 0, 0), 0); same.Update(Env(2, 0, 0), 500);
        Check(rec2.Started.Count == 1 && rec2.Stopped.Count == 0, "env same main id across regions keeps playing");
        // 失敗不更新冷卻；200 ms id 冷卻共用；停用清除。
        var rec3 = new EnvRecorder(); rec3.Missing.Add(2113); var shared = new SoundCooldown();
        var def3 = EnvSoundDefinition.Parse(SyntheticEnvDef); var nightOnly = new MapEnvSound(def3, rec3, new Random(2), shared);
        nightOnly.Update(Env(2005, 1, 1, night: true), 0);
        Check(rec3.OneShots.Length == 0 && def3.Groups[2].NextAt == long.MinValue, "env failed play keeps group cooldown unchanged");
        rec3.Missing.Clear(); nightOnly.Update(Env(2005, 1, 1, night: true), 998);
        Check(rec3.OneShots.Length == 0, "env ONE_SOUND check cadence 999 ms");
        Check(shared.TryAcquire(2113, 999), "test: occupy id cooldown");
        nightOnly.Update(Env(2005, 1, 1, night: true), 999);
        Check(rec3.OneShots.Length == 0, "env one-shot respects global 200 ms id cooldown");
        nightOnly.Update(Env(2005, 1, 1, night: true), 1998);
        Check(rec3.OneShots.Single().SoundId == 2113 && def3.Groups[2].NextAt == 1998 + 105_000, "env one-shot after id cooldown; group cooldown = candidate seconds");
        nightOnly.Update(Env(2005, 1, 1, enabled: false), 4000);
        Check(nightOnly.Mains(4000).Count == 0 && rec3.Stopped.Count == 1 && nightOnly.Region == null, "env disabled stops main immediately");
        // 主音缺檔：每秒最多重試一次。
        var rec4 = new EnvRecorder(); rec4.Missing.Add(2005); var miss = new MapEnvSound(def, rec4, new Random(3));
        miss.Update(Env(4, 32512, 32650), 0); miss.Update(Env(4, 32512, 32650), 500);
        Check(rec4.Started.All(s => !s.Voice.Loop), "env missing main not started");
        rec4.Missing.Clear(); miss.Update(Env(4, 32512, 32650), 999);
        Check(rec4.Started.Count(s => s.Voice.Loop) == 1, "env main retried after 999 ms");
    }

    static void ClientSoundSceneChecks() {
        ClientSoundScene.Reset();
        var session = new WorldSession();
        ClientSoundScene.Observe(session, 0); ClientSoundScene.Attack(session, 1, 2, 0); ClientSoundScene.ObserveScene(MusicScene.Login, 1, false, 0);
        Check(ClientSoundScene.Music == null && ClientSoundScene.EnvSound == null, "product hook is a no-op without backend");
        Check(ClientSoundScene.Scene(LoginState.Offline) == MusicScene.Login && ClientSoundScene.Scene(LoginState.CharacterList) == MusicScene.CharacterSelect && ClientSoundScene.Scene(LoginState.InWorld) == MusicScene.World && ClientSoundScene.Scene(LoginState.EnteringWorld) == MusicScene.None, "login state → music scene");
        var rec = new MusicRecorder(); var env = new EnvRecorder();
        ClientSoundScene.Configure(new SoundDefinitions(MusicDefinition.Parse(SyntheticMusicDef), EnvSoundDefinition.Parse(SyntheticEnvDef), "ready"), rec, env);
        ClientSoundScene.ObserveScene(MusicScene.Login, .5, false, 0);
        Check(rec.Last.Music == 82 && rec.Started[0].Volume == .5, "facade login scene plays login music at master gain");
        ClientSoundScene.Observe(session, 10);
        Check(rec.Stopped.Count == 1 && ClientSoundScene.Music!.Current == null, "facade: world not renderable → music stopped");
        ClientSoundScene.Reset();
        Check(ClientSoundScene.Music == null, "facade reset");
    }

    // 只在本機存在原客戶端時執行（唯讀）；CI 沒有遊戲素材，略過且不計入。
    static void BackgroundMusicRealClientChecks() {
        const string root = @"D:\lineage開發\0-客戶端";
        if (!File.Exists(Path.Combine(root, "Text.idx"))) return;
        var defs = SoundDefinitionSource.Load(root);
        Check(defs.Status == "ready" && defs.Music is { SystemSettings: 21 } m && m.Slot(0) == 82 && m.Slot(8) == 5 && m.Slot(9) == 6 && m.Slot(10) == 7 && m.Slot(18) == 120, "real client music.def system slots (T171 table)");
        var music = defs.Music!;
        Check(music.Region(0, 32570, 32940)?.Music.SequenceEqual([30]) == true && music.Region(0, 32000, 32000)?.Music.SequenceEqual([12]) == true && music.Region(2, 1, 1)?.Music.SequenceEqual([63]) == true && music.Region(4, 33050, 32300)?.Music.SequenceEqual([13]) == true, "real client music regions (TI town 30, TI 12, TI dungeon 63, elf town 13)");
        Check(music.Region(810, 1, 1)?.Music.Order().SequenceEqual([64, 65, 66]) == true && music.Region(0, 32000, 32000)?.Interval == -1, "real client NEW GLUDIO DUNGEON candidates; blank INTERVAL -1");
        var env = defs.EnvSound!;
        Check(env.Groups.Length == 98 && env.Groups.First(g => g.Name == "BIRD_SOUND").Seconds == 80, "real client envsound groups (98)");
        Check(env.Region(68, 1, 1, false)?.MainSound == 2000 && env.Region(68, 1, 1, true)?.MainSound == 2001 && env.Region(2005, 1, 1, false)?.MainSound == 2333, "real client envsound day/night main sounds (68: 2000/2001; 2005: 2333)");
        Check(env.Region(68, 1, 1, true)!.OneSounds.Select(g => g.Name).SequenceEqual(["NIGHTBIRD_SOUND"]) && env.Groups.Any(g => g.Name == "DEEPFORESTBIRD_SOUND" && g.Candidates.Length == 5), "real client envsound ONE_SOUND groups and prefix-closed group");
    }
}
