using System.Text.RegularExpressions;
using P06.NativeUi;

// T404 (#919, gap-matrix 15.12.6/15.12.7): with --evidence <dir> every [native-ui] line is appended, with a timestamp,
// to <dir>/native-ui.log; without the argument nothing is written; a failed write never throws.
// Offline only: files live under <repo>/scratch/issue-919/; only the files this test created are deleted (never recursive).
partial class Checks {
    [CheckGroup("native-ui-evidence-log"),CheckGroup("native-ui-canvas",Order=20),CheckGroup(CheckGroups.Default,Order=42)]
    static void NativeUiEvidenceLogChecks(string repo){
        string root=Path.Combine(Path.GetFullPath(repo),"scratch","issue-919");
        string dir=Path.Combine(root,"evidence");Directory.CreateDirectory(dir);
        string log=Path.Combine(dir,UiResourceLog.EvidenceFileName);
        string blocker=Path.Combine(root,"blocker-file"); // a file where the evidence directory should be → write must fail quietly
        string? saved=UiResourceLog.EvidenceDirectory;
        try{
            // Argument parsing.
            Check(UiResourceLog.EvidenceArgument(["P11.ClientEngine.exe","--evidence",dir])==dir,"--evidence <dir> is picked up");
            Check(UiResourceLog.EvidenceArgument(["x","--demo","d","--evidence","e","--port","1"])=="e","--evidence among other arguments");
            Check(UiResourceLog.EvidenceArgument(["P11.ClientEngine.exe"])==null,"no --evidence → off");
            Check(UiResourceLog.EvidenceArgument(["x","--evidence"])==null,"--evidence without a value → off");
            Check(UiResourceLog.EvidenceArgument(["x","--evidence",""])==null,"--evidence with an empty value → off");
            Check(UiResourceLog.EvidenceArgument(["x","--evidence-dir","d"])==null,"only the exact --evidence switch counts");
            Check(saved==UiResourceLog.EvidenceArgument(Environment.GetCommandLineArgs()),"default directory comes from this process' command line");

            // Off: nothing is written.
            File.Delete(log);
            UiResourceLog.EvidenceDirectory=null;
            UiResourceLog.Write("t404 off line");
            Check(!File.Exists(log),"no evidence directory → no native-ui.log");
            Check(UiResourceLog.Recent().LastOrDefault()=="t404 off line","Recent() still records when off");

            // On: each line appended with a timestamp, one record per line, UTF-8.
            UiResourceLog.EvidenceDirectory=dir;
            UiResourceLog.Write("ui-xml-fallback first");
            UiResourceLog.Write("missing surf 中文 second");
            UiResourceLog.Write("multi\r\nline\nthird");
            Check(File.Exists(log),"native-ui.log created in the evidence directory");
            var lines=File.Exists(log)?File.ReadAllLines(log,System.Text.Encoding.UTF8):[];
            Check(lines.Length==3,"three records → three lines: "+lines.Length);
            var stamp=new Regex(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{3}[+-]\d{2}:\d{2} \[native-ui\] (.*)$");
            string[] expected=["ui-xml-fallback first","missing surf 中文 second","multi line third"];
            for(int i=0;i<expected.Length;i++){
                var m=i<lines.Length?stamp.Match(lines[i]):Match.Empty;
                Check(m.Success&&m.Groups[1].Value==expected[i],"line "+(i+1)+" = timestamp + [native-ui] + text: "+(i<lines.Length?lines[i]:"(missing)"));
            }
            Check(File.ReadAllBytes(log).FirstOrDefault()!=0xEF,"no UTF-8 BOM");
            Check(UiResourceLog.LastEvidenceError==null,"no write error recorded");

            // Append, not overwrite (a second session in the same evidence directory keeps the first one's lines).
            UiResourceLog.Write("fourth");
            Check(File.ReadAllLines(log).Length==4&&File.ReadAllLines(log)[3].EndsWith(" [native-ui] fourth"),"later lines are appended");

            // The log can be read while the game keeps it in use (FileShare.ReadWrite).
            using(var reader=new FileStream(log,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete)){
                UiResourceLog.Write("fifth while open");
                Check(reader.Length>0,"reader open during write");
            }
            Check(File.ReadAllLines(log).Length==5,"write succeeds while another reader has the file open");

            // Failure: the evidence directory is actually a file → Write must not throw.
            File.WriteAllText(blocker,"not a directory");
            UiResourceLog.EvidenceDirectory=blocker;
            bool threw=false;try{UiResourceLog.Write("t404 failing line");}catch(Exception){threw=true;}
            Check(!threw,"failed evidence write does not throw");
            Check(UiResourceLog.LastEvidenceError!=null,"failed evidence write is remembered: "+UiResourceLog.LastEvidenceError);
            Check(UiResourceLog.Recent().LastOrDefault()=="t404 failing line","Recent() still records after a failed write");
            UiResourceLog.EvidenceDirectory=dir;UiResourceLog.Write("recovered");
            Check(UiResourceLog.LastEvidenceError==null&&File.ReadAllLines(log).Last().EndsWith(" [native-ui] recovered"),"next good write clears the error");
        }finally{
            UiResourceLog.EvidenceDirectory=saved;
            File.Delete(log);File.Delete(blocker); // only the files this test created
        }
    }
}
