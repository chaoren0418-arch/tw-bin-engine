# #1247: DPI/IME diagnostics. Dot-source after UiaGdi.ps1 and SmokeTools.ps1.
# DPI state changes are scoped to a synchronous call on this helper's current thread.
function Import-SmokeRetestNative {
    if('SmokeRetestNative' -as [type]){return}
    Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
public sealed class SmokeDpiFrame : IDisposable {
    public Bitmap Bitmap;
    public int Width, Height, WindowWidth, WindowHeight, CropX, CropY, Awareness;
    public uint WindowDpi, CallerSystemDpi;
    public long PreviousContext, TargetContext;
    public bool Restored;
    public void Dispose() { if(Bitmap!=null) Bitmap.Dispose(); }
}
public sealed class SmokeImeThreadProbe {
    public uint Thread;
    public long Before, Requested, After, Restored;
    public bool Activated, DidChange;
}
public static class SmokeRetestNative {
    [StructLayout(LayoutKind.Sequential)] struct RECT { public int Left,Top,Right,Bottom; }
    [StructLayout(LayoutKind.Sequential)] struct POINT { public int X,Y; }
    [DllImport("user32.dll")] static extern IntPtr GetWindowDpiAwarenessContext(IntPtr h);
    [DllImport("user32.dll")] static extern IntPtr GetThreadDpiAwarenessContext();
    [DllImport("user32.dll",SetLastError=true)] static extern IntPtr SetThreadDpiAwarenessContext(IntPtr c);
    [DllImport("user32.dll")] static extern int GetAwarenessFromDpiAwarenessContext(IntPtr c);
    [DllImport("user32.dll")] static extern bool AreDpiAwarenessContextsEqual(IntPtr a,IntPtr b);
    [DllImport("user32.dll")] static extern uint GetDpiForWindow(IntPtr h);
    [DllImport("user32.dll")] static extern uint GetDpiForSystem();
    [DllImport("user32.dll")] static extern bool IsIconic(IntPtr h);
    [DllImport("user32.dll")] static extern bool GetClientRect(IntPtr h,out RECT r);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h,out RECT r);
    [DllImport("user32.dll")] static extern bool ClientToScreen(IntPtr h,ref POINT p);
    [DllImport("user32.dll",SetLastError=true)] static extern bool PrintWindow(IntPtr h,IntPtr dc,uint flags);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h,out uint pid);
    [DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] public static extern IntPtr GetKeyboardLayout(uint thread);
    [DllImport("user32.dll")] static extern int GetKeyboardLayoutList(int n,IntPtr[] values);
    [DllImport("user32.dll")] public static extern IntPtr ActivateKeyboardLayout(IntPtr layout,uint flags);
    [DllImport("imm32.dll")] public static extern IntPtr ImmGetDefaultIMEWnd(IntPtr h);
    [DllImport("user32.dll",SetLastError=true)] static extern IntPtr SendMessageTimeout(IntPtr h,uint m,IntPtr w,IntPtr l,uint flags,uint timeout,out IntPtr result);
    public static long ImeOpenStatus(IntPtr h) {
        IntPtr ime=ImmGetDefaultIMEWnd(h),result;
        if(ime==IntPtr.Zero)return -1;
        // IMC_GETOPENSTATUS. Read only; do not get/set a foreign thread's HIMC.
        if(SendMessageTimeout(ime,0x283,(IntPtr)5,IntPtr.Zero,2,500,out result)==IntPtr.Zero)return -2;
        return result.ToInt64();
    }
    public static SmokeImeThreadProbe ProbeCallingThreadLayout(long requested) {
        bool installed=false;foreach(long layout in Layouts())if(layout==requested)installed=true;
        if(!installed)throw new ArgumentException("Only already loaded layouts may be activated");
        var r=new SmokeImeThreadProbe();r.Thread=GetCurrentThreadId();r.Before=GetKeyboardLayout(0).ToInt64();r.Requested=requested;
        try {
            // flags=0: current thread only; no KLF_SETFORPROCESS/RESET/SHIFTLOCK/REORDER or registry writes.
            r.Activated=ActivateKeyboardLayout((IntPtr)requested,0)!=IntPtr.Zero;
            r.After=GetKeyboardLayout(0).ToInt64();r.DidChange=r.Before!=r.After;
        } finally {ActivateKeyboardLayout((IntPtr)r.Before,0);r.Restored=GetKeyboardLayout(0).ToInt64();}
        if(r.Restored!=r.Before)throw new InvalidOperationException("Calling thread keyboard layout not restored");
        return r;
    }
    public static long[] Layouts() {
        var a=new IntPtr[GetKeyboardLayoutList(0,null)];int n=GetKeyboardLayoutList(a.Length,a);
        var result=new long[n];for(int i=0;i<n;i++)result[i]=a[i].ToInt64();return result;
    }
    public static SmokeDpiFrame Capture(IntPtr h) {
        if(IsIconic(h))throw new InvalidOperationException("Restore the window before capture");
        var f=new SmokeDpiFrame();f.WindowDpi=GetDpiForWindow(h);f.CallerSystemDpi=GetDpiForSystem();
        IntPtr target=GetWindowDpiAwarenessContext(h);
        if(target==IntPtr.Zero)throw new InvalidOperationException("No window DPI context");
        f.TargetContext=target.ToInt64();f.Awareness=GetAwarenessFromDpiAwarenessContext(target);
        IntPtr old=SetThreadDpiAwarenessContext(target);
        if(old==IntPtr.Zero)throw new InvalidOperationException("Cannot enter target DPI context");
        f.PreviousContext=old.ToInt64();
        try {
            RECT cr,wr;POINT pt=new POINT();
            if(!GetClientRect(h,out cr)||!GetWindowRect(h,out wr)||!ClientToScreen(h,ref pt))throw new InvalidOperationException("Cannot read geometry");
            f.Width=cr.Right-cr.Left;f.Height=cr.Bottom-cr.Top;
            f.WindowWidth=wr.Right-wr.Left;f.WindowHeight=wr.Bottom-wr.Top;f.CropX=pt.X-wr.Left;f.CropY=pt.Y-wr.Top;
            if(f.Width<=0||f.Height<=0||f.CropX<0||f.CropY<0||f.CropX+f.Width>f.WindowWidth||f.CropY+f.Height>f.WindowHeight)
                throw new InvalidOperationException("Client crop is outside window in target DPI context");
            using(var full=new Bitmap(f.WindowWidth,f.WindowHeight,PixelFormat.Format24bppRgb)) {
                using(var g=Graphics.FromImage(full)) {
                    IntPtr dc=g.GetHdc();try {if(!PrintWindow(h,dc,2))throw new InvalidOperationException("PrintWindow failed: "+Marshal.GetLastWin32Error());}
                    finally {g.ReleaseHdc(dc);}
                }
                f.Bitmap=full.Clone(new Rectangle(f.CropX,f.CropY,f.Width,f.Height),PixelFormat.Format24bppRgb);
            }
        } catch {f.Dispose();throw;}
        finally {SetThreadDpiAwarenessContext(old);f.Restored=AreDpiAwarenessContextsEqual(old,GetThreadDpiAwarenessContext());}
        if(!f.Restored){f.Dispose();throw new InvalidOperationException("DPI context restoration failed");}
        return f;
    }
}
'@
}

function Get-SmokeImeDiagnostic {
    param([Parameter(Mandatory)][IntPtr]$Hwnd,[Parameter(Mandatory)][string]$Path)
    Import-SmokeRetestNative
    [uint32]$targetPid=0
    $thread=[SmokeRetestNative]::GetWindowThreadProcessId($Hwnd,[ref]$targetPid)
    if($thread -eq 0){throw 'No target window thread'}
    $layouts=[SmokeRetestNative]::Layouts()
    $targetBefore=[SmokeRetestNative]::GetKeyboardLayout($thread).ToInt64()
    $result=[ordered]@{Hwnd=$Hwnd.ToInt64();TargetProcessId=$targetPid;TargetThread=$thread;
        TargetLayoutBefore=$targetBefore;LoadedLayouts=$layouts;
        CallingThreadProbe=[SmokeRetestNative]::ProbeCallingThreadLayout($targetBefore);
        TargetLayoutAfter=[SmokeRetestNative]::GetKeyboardLayout($thread).ToInt64();
        DefaultImeHwnd=[SmokeRetestNative]::ImmGetDefaultIMEWnd($Hwnd).ToInt64();
        TargetOpenStatus=[SmokeRetestNative]::ImeOpenStatus($Hwnd);
        CompositionVerified=$false;
        Scope='Diagnostic only. Calling-thread activation does not activate/open the client IME. Do not pass a foreign HIMC to ImmSetOpenStatus. Require real composition/candidate evidence.'}
    Save-SmokeJson $Path $result
    [pscustomobject]$result
}

function Get-SmokeDpiCapture {
    param([Parameter(Mandatory)][IntPtr]$Hwnd,[Parameter(Mandatory)][string]$Path)
    Import-SmokeRetestNative
    $frame=[SmokeRetestNative]::Capture($Hwnd)
    try {
        $full=[IO.Path]::GetFullPath($Path)
        $null=[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($full))
        $stats=[ClientInputNative]::Stats($frame.Bitmap)
        if($stats[2] -eq 0 -or $stats[3] -eq 0){throw 'PrintWindow returned a blank frame; not valid DPI evidence'}
        $frame.Bitmap.Save($full,[Drawing.Imaging.ImageFormat]::Png)
        $result=[ordered]@{Path=$full;Method='PrintWindow-target-thread-context-client-crop';Width=$frame.Width;Height=$frame.Height;
            WindowDpi=$frame.WindowDpi;CallerSystemDpi=$frame.CallerSystemDpi;Awareness=$frame.Awareness;
            WindowSize=@($frame.WindowWidth,$frame.WindowHeight);CropOrigin=@($frame.CropX,$frame.CropY);
            PreviousContext=$frame.PreviousContext;TargetContext=$frame.TargetContext;ContextRestored=$frame.Restored;
            NonBlackRatio=$stats[2];Sha256=(Get-FileHash -LiteralPath $full -Algorithm SHA256).Hash;
            Scope='Target effective DPI; does not change or prove physical desktop DPI. Compare all four canvas edges visually.'}
        Save-SmokeJson ($full+'.json') $result
        [pscustomobject]$result
    } finally {$frame.Dispose()}
}

function Get-SmokeChatBitmap {
    # Internal Get-GdiClientBitmap-shaped result. Mapping here is into capture pixels, NOT SendInput screen pixels.
    # Caller owns Bitmap. Input continues using UiaGdi's physical geometry (never the virtualised capture geometry).
    param([Parameter(Mandatory)][IntPtr]$Hwnd,[switch]$Fill)
    Import-SmokeRetestNative
    $g=Get-ClientWindowGeometry $Hwnd -Fill:$Fill
    $frame=[SmokeRetestNative]::Capture($Hwnd)
    try {
        $ratio=$frame.Width/[double]$g.ClientWidth
        if([Math]::Abs($ratio-$frame.Height/[double]$g.ClientHeight) -gt 0.000001){throw 'Nonuniform capture/physical ratio; cannot verify chat ink'}
        $scale=if($Fill){[Math]::Min($frame.Width/800.0,$frame.Height/600.0)}else{[Math]::Max(1,[Math]::Min([Math]::Floor($frame.Width/800.0),[Math]::Floor($frame.Height/600.0)))}
        if([Math]::Abs($g.Scale*$ratio-$scale) -gt 0.000001){throw 'Physical input mapping differs from target viewport; restore default window size before sending chat'}
        $g.Scale=$scale
        $g.ViewportWidth=[int][Math]::Round(800*$scale);$g.ViewportHeight=[int][Math]::Round(600*$scale)
        $g.ViewportLeft=[int][Math]::Truncate(($frame.Width-$g.ViewportWidth)/2)
        $g.ViewportTop=[int][Math]::Truncate(($frame.Height-$g.ViewportHeight)/2)
        $g.ClientWidth=$frame.Width;$g.ClientHeight=$frame.Height
        $g.RatioNote="target-DPI capture pixels; capture/physical=$ratio; input uses physical geometry"
        $stats=[ClientInputNative]::Stats($frame.Bitmap)
        if($stats[2] -eq 0 -or $stats[3] -eq 0){throw 'Blank capture cannot verify chat ink'}
        $result=@{Bitmap=$frame.Bitmap;Geometry=$g;Stats=$stats;Method='PrintWindow-target-thread-context-client-crop';PrintWindowError=$null;BitBltError=$null;Foreground=$null}
        $frame.Bitmap=$null
        $result
    }finally{$frame.Dispose()}
}

function Save-SmokeCanvasCapture {
    param([Parameter(Mandatory)][IntPtr]$Hwnd,[Parameter(Mandatory)][string]$StatePath,
        [Parameter(Mandatory)][string]$CaptureDirectory,[Parameter(Mandatory)][string]$EvidencePrefix,
        [ValidateRange(1,15)][int]$TimeoutSeconds=5)
    $result=[ordered]@{Captured=$false;Method='engine-Ctrl+P';Error=$null}
    try {
        $state=Read-SmokeJson $StatePath
        if($state.state -ne 'InWorld' -or -not $state.world.renderable){throw 'Ctrl+P only captures InWorld; use Get-SmokeDpiCapture for login/selection/create'}
        $before=@(Get-ChildItem -LiteralPath $CaptureDirectory -Filter '*.png' -ErrorAction SilentlyContinue | ForEach-Object FullName)
        Assert-SmokeMainForeground $Hwnd
        $null=Send-ClientKey $Hwnd 'Ctrl+P'
        $deadline=[DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
        do {
            Start-Sleep -Milliseconds 100
            $fresh=@(Get-ChildItem -LiteralPath $CaptureDirectory -Filter '*.png' -ErrorAction SilentlyContinue | Where-Object {$before -notcontains $_.FullName})
            if($fresh.Count -eq 1){
                try {$bmp=[Drawing.Bitmap]::new($fresh[0].FullName)}catch{continue}
                try {
                    if($bmp.Width -ne 800 -or $bmp.Height -ne 600){throw 'Engine canvas is not 800x600'}
                    $result.Width=$bmp.Width;$result.Height=$bmp.Height
                } finally {$bmp.Dispose()}
                $result.Source=$fresh[0].FullName;$result.Path=[IO.Path]::GetFullPath($EvidencePrefix+'-canvas.png')
                $null=[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($result.Path))
                [IO.File]::Copy($result.Source,$result.Path,$false)
                $result.Sha256=(Get-FileHash -LiteralPath $result.Path -Algorithm SHA256).Hash
                $result.WindowDpi=[UiaGdiNative]::GetDpiForWindow($Hwnd);$result.Captured=$true
                break
            }
            if($fresh.Count -gt 1){throw 'Multiple new captures; attribution ambiguous'}
        }while([DateTime]::UtcNow -lt $deadline)
        if(-not $result.Captured){throw 'No new complete Ctrl+P canvas within timeout'}
    }catch{$result.Error=$_.Exception.Message}
    Save-SmokeJson ($EvidencePrefix+'-canvas.json') $result
    [pscustomobject]$result
}
