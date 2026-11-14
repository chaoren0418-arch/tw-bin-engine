param([string]$EvidenceDirectory='scratch/issue-1210/contracts')
$ErrorActionPreference='Stop'
$ev=[IO.Path]::GetFullPath($EvidenceDirectory)
$null=New-Item -ItemType Directory -Force $ev,"$ev/tmp"
$env:TEMP="$ev/tmp";$env:TMP=$env:TEMP
. "$PSScriptRoot/SmokeTools.ps1"
. "$PSScriptRoot/UiaGdi.ps1"
Import-SmokeLoopback
$checks=[Collections.Generic.List[object]]::new()
function Check([string]$Name,[bool]$Passed,$Detail){
    $checks.Add([pscustomobject]@{Name=$Name;Passed=$Passed;Detail=$Detail})
    if(-not $Passed){throw "Failed: $Name"}
}
Add-Type @'
using System;
using System.IO;
using System.Text;
using System.Threading;
public sealed class SmokeConcurrentWriter {
    public int SharingFailures, Writes;
    public Thread Thread;
    public void Start(string path) {
        Thread=new Thread(()=>{
            for(int i=0;i<250;i++) {
                try {
                    using(var f=new FileStream(path,FileMode.Create,FileAccess.Write,FileShare.ReadWrite|FileShare.Delete)) {
                        byte[] b=Encoding.UTF8.GetBytes("{\"state\":\"InWorld\",\"revision\":"+i+"}");
                        f.Write(b,0,5);f.Flush();Thread.Sleep(1);f.Write(b,5,b.Length-5);
                    }
                    Writes++;
                } catch(IOException) {SharingFailures++;}
                Thread.Sleep(2);
            }
        });Thread.Start();
    }
}
'@
try {
    $state="$ev/shared.json";[IO.File]::WriteAllText($state,'{"state":"InWorld","revision":-1}')
    $writer=[SmokeConcurrentWriter]::new();$writer.Start($state)
    $reads=0
    for($i=0;$i -lt 60;$i++){$s=Read-SmokeJson $state -Attempts 40;if($s.state -eq 'InWorld'){$reads++}}
    $writer.Thread.Join()
    Check 'concurrent-truncate-and-read' ($reads -eq 60 -and $writer.SharingFailures -eq 0 -and $writer.Writes -eq 250) @{Reads=$reads;Writes=$writer.Writes;SharingFailures=$writer.SharingFailures}
    $copy=Copy-SmokeState $state "$ev/copied.json"
    Check 'copy-validated-json' ($copy.State.revision -eq 249) $copy.Sha256
    $trace="$ev/trace.jsonl"
    [IO.File]::WriteAllText($trace,"{`"dir`":`"out`",`"op`":125}`n{`"dir`":`"out`"")
    $mark=Get-SmokeTraceMark $trace
    [IO.File]::AppendAllText($trace,",`"op`":125}`n{`"dir`":`"in`",`"op`":89}`n{`"dir`":`"out`",`"op`":4}`n{`"dir`":")
    $rows=@(Read-SmokeTraceSince $mark)
    Check 'ignore-preexisting-and-partial-records' ($rows.Count -eq 2 -and $rows[0].op -eq 89 -and $rows[1].op -eq 4) $rows
    [IO.File]::WriteAllText($trace,'')
    $refused=$false;try {$null=Read-SmokeTraceSince $mark}catch{$refused=$true}
    Check 'trace-truncation-is-unknown' $refused 'Refused changed prefix, not a false no-send result'
    $fixture=[pscustomobject]@{state='InWorld';revision=123;world=[pscustomobject]@{renderable=$true;mapId=0;worldEpoch=1;actor=[pscustomobject]@{ObjectId=10334;X=32644;Y=32951};objects=@([pscustomobject]@{ObjectId=1900240001;X=32644;Y=32952;Dead=$false;AppearancePending=$false})}}
    $point=Convert-SmokeObjectPoint $fixture 1900240001
    Check 'native-world-projection' ($point.X -eq 436 -and $point.Y -eq 282) $point
    $refused=$false;try {$null=Convert-SmokeObjectPoint $fixture 999}catch{$refused=$true}
    Check 'absent-object-refused' $refused 'No input is sent for an absent objid'
    Check 'loopback-interop-compiled' ($null -ne ('SmokeLoopback' -as [type])) 'C# interop compiled; not an audio playback claim'
    # Pure session classification (no audio device): ClassifySession(isOwn,state,peak,threshold,meterOk,crossProcess,requireExclusive)
    $t=[SmokeLoopback]::SilenceThreshold
    Check 'loopback-threshold-constant' ($t -eq 1e-4) $t
    $cases=@(
        @{Name='own-audible';Args=@($true,1,0.25,$t,$true,$false,$false);Expect='own'},
        @{Name='other-active-silent';Args=@($false,1,0.00005,$t,$true,$false,$false);Expect='other-active-silent'},
        @{Name='other-silent-at-threshold';Args=@($false,1,$t,$t,$true,$false,$false);Expect='other-active-silent'},
        @{Name='other-audible';Args=@($false,1,0.01,$t,$true,$false,$false);Expect='other-audible'},
        @{Name='other-unmetered';Args=@($false,1,0.0,$t,$false,$false,$false);Expect='other-active-unmetered'},
        @{Name='other-nan-peak-unmetered';Args=@($false,1,[double]::NaN,$t,$true,$false,$false);Expect='other-active-unmetered'},
        @{Name='cross-process-silent';Args=@($true,1,0.0,$t,$true,$true,$false);Expect='other-active-silent'},
        @{Name='cross-process-audible';Args=@($true,1,0.5,$t,$true,$true,$false);Expect='other-audible'},
        @{Name='other-inactive';Args=@($false,0,0.9,$t,$true,$false,$false);Expect='inactive'},
        @{Name='require-exclusive-silent-other';Args=@($false,1,0.0,$t,$true,$false,$true);Expect='other-active-exclusive'},
        @{Name='require-exclusive-own';Args=@($true,1,0.2,$t,$true,$false,$true);Expect='own'}
    )
    foreach($c in $cases){
        $a=$c.Args
        $v=[SmokeLoopback]::ClassifySession([bool]$a[0],[int]$a[1],[double]$a[2],[double]$a[3],[bool]$a[4],[bool]$a[5],[bool]$a[6])
        Check "loopback-classify-$($c.Name)" ($v -eq $c.Expect) @{Got=$v;Expect=$c.Expect;Args=($a -join ',')}
    }
    $rejecting=@('other-audible','other-active-unmetered','other-active-exclusive' | Where-Object {[SmokeLoopback]::IsRejecting($_)})
    $continuing=@('own','inactive','other-active-silent','capture-host-ignored' | Where-Object {[SmokeLoopback]::IsRejecting($_)})
    Check 'loopback-reject-set' ($rejecting.Count -eq 3 -and $continuing.Count -eq 0) @{Rejecting=$rejecting;Continuing=$continuing}
    $shape=[SmokeLoopbackResult]::new()
    $fields=@('Threshold','OwnMaxPeak','OtherSessions','RejectReason','Frames','CapturedSeconds','Rms','Peak','HasWaveform','Scope')
    $missing=@($fields | Where-Object {-not $shape.PSObject.Properties[$_]})
    Check 'loopback-json-fields' ($missing.Count -eq 0) @{Missing=$missing}
    $entry=Copy-ClientLogEntry ([ordered]@{Keys='Ctrl+Shift+F9';VirtualKey='0x78';Modifiers=@('0x11','0x10')}) 'test'
    Check 'key-log-preserves-Keys-member' ($entry['Keys'] -eq 'Ctrl+Shift+F9' -and $entry['VirtualKey'] -eq '0x78' -and $entry['Action'] -eq 'test') $entry
} finally {Save-SmokeJson "$ev/results.json" @($checks.ToArray())}
$checks | Format-Table Name,Passed
