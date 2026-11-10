using P06;
using P06.NativeUi;

// T317 (#751, user decision #743-1): native UI is the default for the product entry; --native-ui stays as a compatible
// no-op-equivalent; --classic-ui switches back to the legacy WinForms HUD. Offline only: no ClientForm, no client resources.
// Three cases are walked exactly like Program.Main does it: Strip → ProductEntry.Normal → DefaultOn → Resolve.
partial class Checks {
    [CheckGroup("native-ui-default"),CheckGroup("native-ui-canvas",Order=10),CheckGroup(CheckGroups.Default,Order=41)]
    static void NativeUiDefaultChecks(string repo){
        string dir=Path.Combine(Path.GetFullPath(repo),"scratch","issue-751","options");Directory.CreateDirectory(dir);
        string settings=Path.Combine(dir,UiOptions.SettingsFileName);File.Delete(settings); // no settings file: only the default / arguments decide
        bool savedLine=UiOptions.CommandLine,savedClassic=UiOptions.Classic,savedDefault=UiOptions.DefaultOn;
        var panelsBefore=UiPanelRegistry.Panels.Select(p=>p.Id).ToArray();
        Check(panelsBefore.Length>0&&UiPanelRegistry.Errors.Count==0,"panel registry discovered with no errors: "+panelsBefore.Length+" panels");
        foreach(var id in new[]{"LEFT_WINDOW","CENTER_WINDOW",MenuButtonsPanel.PanelId,MiniMapPanel.PanelId})
            Check(panelsBefore.Contains(id),"registry has "+id);
        try{
            Check(UiOptions.DefaultOn,"static DefaultOn starts true (native UI is the default)");
            Check(ProductEntry.NativeUiDefault(true)&&!ProductEntry.NativeUiDefault(false),"product entry → native default; diagnostic harness → legacy default");

            // Runs the Program.Main sequence for one command line and returns what InitNativeUi would resolve.
            (bool normal,UiOptions options) Launch(string[] argv){
                UiOptions.CommandLine=false;UiOptions.Classic=false;UiOptions.DefaultOn=true;
                var rest=UiOptions.Strip(argv);bool normal=ProductEntry.Normal(rest);
                UiOptions.DefaultOn=ProductEntry.NativeUiDefault(normal);
                return (normal,UiOptions.Resolve(UiOptions.CommandLine,settings));
            }
            string[] Ids()=>UiPanelRegistry.Panels.Select(p=>p.Id).ToArray();

            // 1. default (launcher: --normal-product, or no arguments).
            foreach(var argv in new[]{new[]{"--normal-product"},Array.Empty<string>()}){
                var (normal,o)=Launch(argv);string label="["+string.Join(" ",argv)+"]";
                Check(normal&&o.Enabled&&o.Source=="default"&&!o.Fill&&o==UiOptions.On,"default "+label+": normal product, native UI on");
                Check(!UiOptions.CommandLine&&!UiOptions.Classic,"default "+label+": no switch recorded");
                Check(Ids().SequenceEqual(panelsBefore),"default "+label+": same panel registrations");
            }
            // 2. --native-ui (compatible: accepted, same effect as the default).
            foreach(var argv in new[]{new[]{"--native-ui"},new[]{"--normal-product","--native-ui"},new[]{"--native-ui","--normal-product"}}){
                var (normal,o)=Launch(argv);string label="["+string.Join(" ",argv)+"]";
                Check(normal,"--native-ui "+label+": still the normal product (no error)");
                Check(o.Enabled&&o.Source=="argument"&&UiOptions.CommandLine&&!UiOptions.Classic,"--native-ui "+label+": native UI on");
                Check(o.Enabled==Launch(["--normal-product"]).options.Enabled,"--native-ui "+label+": same on/off as the default");
                Check(Ids().SequenceEqual(panelsBefore),"--native-ui "+label+": same panel registrations");
            }
            // 3. --classic-ui (legacy WinForms HUD).
            foreach(var argv in new[]{new[]{"--classic-ui"},new[]{"--normal-product","--classic-ui"},new[]{"--classic-ui","--native-ui"}}){
                var (normal,o)=Launch(argv);string label="["+string.Join(" ",argv)+"]";
                Check(normal,"--classic-ui "+label+": still the normal product (no error)");
                Check(!o.Enabled&&!o.Fill&&o.Source=="argument"&&UiOptions.Classic,"--classic-ui "+label+": native UI off (legacy HUD)");
                Check(Ids().SequenceEqual(panelsBefore),"--classic-ui "+label+": same panel registrations");
            }
            Check(UiOptions.Strip(["--classic-ui","--native-ui","--normal-product"]).SequenceEqual(new[]{"--normal-product"}),"Strip removes both switches and keeps the rest in order");

            // --classic-ui wins over a settings file that asks for the native UI; settings can still opt out without arguments.
            File.WriteAllText(settings,"{\"nativeUi\":true,\"nativeUiFill\":true}");
            Check(!Launch(["--classic-ui"]).options.Enabled,"--classic-ui beats settings nativeUi=true");
            var filled=Launch(["--normal-product"]).options;Check(filled.Enabled&&filled.Fill&&filled.Source=="settings","settings fill honoured on the default path");
            File.WriteAllText(settings,"{\"nativeUi\":false}");
            var optedOut=Launch(["--normal-product"]).options;Check(!optedOut.Enabled&&optedOut.Source=="settings","settings nativeUi=false opts out of the default");
            Check(Launch(["--native-ui"]).options.Enabled,"--native-ui beats settings nativeUi=false");
            File.Delete(settings);

            // Diagnostic harness entries (any other argument) keep the legacy HUD unless --native-ui is given.
            UiOptions.CommandLine=false;UiOptions.Classic=false;
            var diag=UiOptions.Strip(["--demo","x"]);bool diagNormal=ProductEntry.Normal(diag);
            Check(!diagNormal&&!UiOptions.Resolve(false,false,settings,ProductEntry.NativeUiDefault(diagNormal)).Enabled,"diagnostic entry (--demo) defaults to the legacy HUD");
            Check(UiOptions.Resolve(true,false,settings,ProductEntry.NativeUiDefault(diagNormal)).Enabled,"diagnostic entry + --native-ui → native UI");
            bool rejected=false;try{ProductEntry.Normal(UiOptions.Strip(["--normal-product","--classic-ui","--demo"]));}catch(ArgumentException){rejected=true;}
            Check(rejected,"--normal-product with a real diagnostic argument is still rejected (switches do not hide it)");
        }finally{UiOptions.CommandLine=savedLine;UiOptions.Classic=savedClassic;UiOptions.DefaultOn=savedDefault;File.Delete(settings);}
    }
}
