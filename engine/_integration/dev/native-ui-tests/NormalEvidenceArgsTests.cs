using P06;
using P06.NativeUi;

// T411 (#948, smoke-m7 K-1/K-2): --normal-product accepts exactly one extra --evidence <dir>; every other diagnostic or
// auto-login argument is still rejected with the unchanged message. The directory must be inside the project folder.
// Offline only: synthetic argv, no ClientForm, no game, no client resources. Folders live under <repo>/scratch/issue-948/;
// only the link this test created is removed (Directory.Delete without recursion).
partial class Checks {
    [CheckGroup("normal-evidence-args"),CheckGroup("native-ui-canvas",Order=30),CheckGroup(CheckGroups.Default,Order=43)]
    static void NormalEvidenceArgsChecks(string repo){
        string project=Path.Combine(Path.GetFullPath(repo),"scratch","issue-948","project");
        string inside=Path.Combine(project,"scratch","smoke-m7","run-A");Directory.CreateDirectory(inside);
        bool savedLine=UiOptions.CommandLine,savedClassic=UiOptions.Classic,savedDefault=UiOptions.DefaultOn;

        // Program.Main order: Strip → ProductEntry.Normal → NativeUiDefault.
        bool Accept(string[] argv)=>ProductEntry.Normal(UiOptions.Strip(argv),project);
        string? Reject(string[] argv){try{ProductEntry.Normal(UiOptions.Strip(argv),project);return null;}catch(ArgumentException ex){return ex.Message;}}
        string L(string[] argv)=>"["+string.Join(" ",argv)+"]";
        try{
            // 1. Accepted: --normal-product alone, or with one --evidence <dir> pair in either order (switches stripped first).
            string[][] accepted=[
                ["--normal-product"],
                ["--normal-product","--evidence",inside],
                ["--evidence",inside,"--normal-product"],
                ["--normal-product","--native-ui","--evidence",inside],
                ["--classic-ui","--normal-product","--evidence",inside],
                ["--normal-product","--evidence",inside+Path.DirectorySeparatorChar],
                ["--normal-product","--evidence",Path.Combine(project,"scratch","not-yet-created")],
            ];
            foreach(var argv in accepted){
                UiOptions.CommandLine=false;UiOptions.Classic=false;
                bool normal=Accept(argv);
                Check(normal,"accepted "+L(argv)+": normal product");
                Check(ProductEntry.NativeUiDefault(normal),"accepted "+L(argv)+": native UI default stays on (normal product behaviour unchanged)");
            }
            // Relative path resolves against the current directory, exactly like the evidence writers do.
            string relative=Path.GetRelativePath(Environment.CurrentDirectory,inside);
            if(!Path.IsPathRooted(relative))Check(Accept(["--normal-product","--evidence",relative]),"relative evidence path inside the project is accepted: "+relative);
            // The evidence writers see the same directory (UiResourceLog → native-ui.log; Program.Main → ClientForm evidence).
            Check(UiResourceLog.EvidenceArgument(["P11.ClientEngine.exe","--normal-product","--evidence",inside])==inside,"native-ui.log picks up --evidence next to --normal-product");
            Check(UiResourceLog.EvidenceArgument(["P11.ClientEngine.exe","--evidence",inside,"--normal-product"])==inside,"native-ui.log picks up --evidence before --normal-product");
            Check(ProductEntry.CheckEvidenceDirectory(inside,project)==Path.TrimEndingDirectorySeparator(Path.GetFullPath(inside)),"CheckEvidenceDirectory returns the full path");

            // 2. Still rejected with the unchanged message: any other diagnostic / auto-login argument, malformed --evidence.
            string[][] rejected=[
                ["--normal-product","--demo","x"],
                ["--normal-product","--login-demo","x"],
                ["--normal-product","--port","42061"],
                ["--normal-product","--assets","x"],
                ["--normal-product","--evidence"],
                ["--normal-product","--evidence",""],
                ["--normal-product","--evidence","  "],
                ["--normal-product","--evidence","--demo"],
                ["--normal-product","--evidence-dir",inside],
                ["--normal-product","x","--evidence"],
                ["--normal-product","--evidence",inside,"--demo","x"],
                ["--normal-product","--evidence",inside,"--port","1"],
                ["--normal-product","--evidence",inside,"--evidence",inside],
                ["--normal-product","--evidence",inside,"--login-port","42061"],
                ["--normal-product","--normal-product","--evidence"],
                ["--normal-product","--classic-ui","--demo"],
                ["--evidence","--normal-product","x"],
            ];
            foreach(var argv in rejected)Check(Reject(argv)==ProductEntry.RejectMessage,"rejected "+L(argv)+" with the unchanged message");
            Check(ProductEntry.RejectMessage=="正常入口不接受診斷或自動登入參數。","reject message text is the pre-T411 one");

            // 3. --evidence outside the project folder (or on a forbidden path) is rejected with a hint, not silently ignored.
            string parent=Path.GetDirectoryName(project)!;
            string[] outside=[
                Path.Combine(parent,"elsewhere"),
                project+"-other",                       // shares the prefix but is a sibling
                project,                                // the project folder itself is not "inside"
                Path.Combine(project,"..","escape"),
                Path.Combine(project,"releases","r33","evidence"),
                Path.Combine(project,"RELEASES"),
            ];
            foreach(var dir in outside){
                string? message=Reject(["--normal-product","--evidence",dir]);
                Check(message!=null&&message!=ProductEntry.RejectMessage&&message.StartsWith("證據目錄",StringComparison.Ordinal)&&message.Contains("scratch"),"evidence "+dir+" rejected with a project-folder hint: "+message);
            }
            bool noRoot=false;try{ProductEntry.Normal(["--normal-product","--evidence",inside],null);}catch(ArgumentException ex){noRoot=ex.Message.StartsWith("證據目錄",StringComparison.Ordinal);}
            Check(noRoot,"unknown project folder → --evidence rejected (cannot be checked)");
            Check(ProductEntry.Normal(["--normal-product"],null),"unknown project folder does not affect --normal-product alone");
            Check(ProductEntry.DefaultProjectRoot() is string root&&Path.IsPathRooted(root),"default project folder is resolved from the product directory");

            // A junction / symbolic link inside the project (like engine\assets → releases\r29\assets) is not accepted.
            string target=Path.Combine(project,"scratch","link-target");Directory.CreateDirectory(target);
            string link=Path.Combine(project,"scratch","link-948");
            bool made=false;
            try{if(!Directory.Exists(link))Directory.CreateSymbolicLink(link,target);made=true;}catch(Exception){} // needs privileges on Windows
            if(made){
                try{
                    string? message=Reject(["--normal-product","--evidence",Path.Combine(link,"run")]);
                    Check(message!=null&&message.Contains("junction"),"evidence through a symbolic link rejected: "+message);
                }finally{Directory.Delete(link);} // removes the link only (no recursion)
            }

            // 4. Diagnostic entries are unchanged: --evidence without --normal-product is still the diagnostic harness.
            Check(!ProductEntry.Normal(["--evidence",inside],project),"--evidence alone stays the diagnostic entry");
            Check(!ProductEntry.NativeUiDefault(ProductEntry.Normal(["--evidence",inside],project)),"diagnostic --evidence keeps the legacy HUD default");
            Check(!ProductEntry.Normal(["--evidence",Path.Combine(parent,"elsewhere")],project),"diagnostic --evidence is not subject to the project-folder rule");
            Check(ProductEntry.Normal([],project),"no arguments → normal product");
            Check(!ProductEntry.Normal(["--demo","x"],project),"--demo → diagnostic entry");
        }finally{UiOptions.CommandLine=savedLine;UiOptions.Classic=savedClassic;UiOptions.DefaultOn=savedDefault;}
    }
}
