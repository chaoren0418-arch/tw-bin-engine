# f1186-retest helpers. Dot-source after UiaGdi.ps1. Windows PowerShell 5.1.
# No product reflection, packet injection, UIA setters, database access or process discovery.
Set-StrictMode -Version Latest

function Read-SmokeText {
    param([Parameter(Mandatory)][string]$Path)
    $stream=[IO.File]::Open([IO.Path]::GetFullPath($Path),[IO.FileMode]::Open,[IO.FileAccess]::Read,
        ([IO.FileShare]::ReadWrite -bor [IO.FileShare]::Delete))
    try {
        $reader=[IO.StreamReader]::new($stream,[Text.Encoding]::UTF8,$true)
        try { $reader.ReadToEnd() } finally { $reader.Dispose() }
    } finally { $stream.Dispose() }
}

function Read-SmokeJson {
    # A shared handle prevents reader-induced sharing violations; retry torn/truncated writes separately.
    param([Parameter(Mandatory)][string]$Path,[ValidateRange(1,40)][int]$Attempts=12)
    for($i=0;$i -lt $Attempts;$i++) {
        try {
            $text=Read-SmokeText $Path
            if([string]::IsNullOrWhiteSpace($text)){throw 'Empty JSON during write'}
            $value=ConvertFrom-Json -InputObject $text -ErrorAction Stop
            if($null -eq $value){throw 'Null JSON during write'}
            return $value
        } catch {
            if($i -eq $Attempts-1){throw}
            Start-Sleep -Milliseconds 50
        }
    }
}

function Save-SmokeJson {
    param([Parameter(Mandatory)][string]$Path,[Parameter(Mandatory)]$Value)
    $full=[IO.Path]::GetFullPath($Path)
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($full)) | Out-Null
    [IO.File]::WriteAllText($full,($Value | ConvertTo-Json -Depth 60),[Text.UTF8Encoding]::new($false))
}

function Copy-SmokeState {
    param([Parameter(Mandatory)][string]$Source,[Parameter(Mandatory)][string]$Destination)
    if([IO.Path]::GetFullPath($Source) -eq [IO.Path]::GetFullPath($Destination)){throw 'Source and destination must differ'}
    $state=Read-SmokeJson $Source
    Save-SmokeJson $Destination $state
    [pscustomobject]@{Path=[IO.Path]::GetFullPath($Destination);Sha256=(Get-FileHash -LiteralPath $Destination -Algorithm SHA256).Hash;State=$state}
}

function Get-SmokeTraceMark {
    param([Parameter(Mandatory)][string]$Path)
    # Mark only complete lines. A partial pre-existing row must not count as a new send.
    $text=Read-SmokeText $Path
    [pscustomobject]@{Path=[IO.Path]::GetFullPath($Path);Prefix=$text;Utc=[DateTime]::UtcNow.ToString('o')}
}

function Read-SmokeTraceSince {
    param([Parameter(Mandatory)]$Mark)
    $text=Read-SmokeText $Mark.Path
    if(-not $text.StartsWith($Mark.Prefix,[StringComparison]::Ordinal)){throw 'Trace was truncated/replaced; send status is unknown'}
    $tail=$text.Substring($Mark.Prefix.Length)
    if($Mark.Prefix.Length -gt 0 -and -not $Mark.Prefix.EndsWith("`n")){
        $first=$tail.IndexOf("`n");if($first -lt 0){return};$tail=$tail.Substring($first+1)
    }
    $end=$tail.LastIndexOf("`n");if($end -lt 0){return}
    foreach($line in ($tail.Substring(0,$end) -split "`n")){
        if(-not [string]::IsNullOrWhiteSpace($line)) { ConvertFrom-Json -InputObject $line -ErrorAction Stop }
    }
}

function Measure-SmokeInputInk {
    param([Parameter(Mandatory)]$Bitmap,[Parameter(Mandatory)]$Geometry,[Parameter(Mandatory)][int[]]$InputRect)
    if($InputRect.Count -ne 4 -or $InputRect[2] -lt 20 -or $InputRect[3] -lt 8){throw 'InputRect must cover the full text area (x,y,width,height), excluding its border'}
    $r=Convert-ClientLogicalRect $Geometry $InputRect
    if($r.X -lt 0 -or $r.Y -lt 0 -or $r.X+$r.Width -gt $Bitmap.Width -or $r.Y+$r.Height -gt $Bitmap.Height){throw 'Input rectangle outside capture'}
    $ink=0;$probe=0
    # Native UiText draws white, single-bit glyphs. Omit only the empty caret at the first two logical pixels.
    $skip=[Math]::Ceiling(2*$Geometry.Scale);$probeEnd=[Math]::Ceiling(14*$Geometry.Scale)
    for($y=$r.Y;$y -lt $r.Y+$r.Height;$y++){
        for($x=$r.X+$skip;$x -lt $r.X+$r.Width;$x++){
            $c=$Bitmap.GetPixel($x,$y)
            if($c.R -ge 245 -and $c.G -ge 245 -and $c.B -ge 245){$ink++;if($x -lt $r.X+$probeEnd){$probe++}}
        }
    }
    [pscustomobject]@{WhitePixels=$ink;ProbePixels=$probe;Rect=$r}
}

function Clear-SmokeChat {
    param([Parameter(Mandatory)][IntPtr]$Hwnd,[Parameter(Mandatory)][string]$StatePath,
        [Parameter(Mandatory)][string]$EvidencePrefix,[Parameter(Mandatory)][int[]]$InputRect,[switch]$Fill)
    $state=Read-SmokeJson $StatePath
    if($state.state -ne 'InWorld' -or -not $state.world.renderable){throw 'Chat requires a renderable InWorld client'}
    # The channel tabs overlap the left end of CHAT_BAR; click its centre, not the text origin.
    $null=Send-ClientClick $Hwnd ($InputRect[0]+$InputRect[2]/2) ($InputRect[1]+$InputRect[3]/2) -Fill:$Fill
    # Native ChatInputLine does not implement Ctrl+A/Delete. Esc preserves its draft.
    for($i=0;$i -lt 106;$i++){ $null=Send-ClientKey $Hwnd 'Backspace' }
    Start-Sleep -Milliseconds 180
    $g=Get-ClientWindowGeometry $Hwnd -Fill:$Fill
    $before=Get-GdiClientBitmap $Hwnd
    try {
        $blank=Measure-SmokeInputInk $before.Bitmap $g $InputRect
        $blankCapture=Save-GdiClientBitmap $before ($EvidencePrefix+'-empty.png')
    } finally {$before.Bitmap.Dispose()}
    if($blank.WhitePixels -ne 0){throw "Input not visually empty ($($blank.WhitePixels) white pixels); no command sent"}
    # Positive control: verifies that the bar is open and this rectangle actually contains its input origin.
    Send-ClientText $Hwnd 'W'
    Start-Sleep -Milliseconds 150
    try {
        $probe=Get-GdiClientBitmap $Hwnd
        try {
            $probeInk=Measure-SmokeInputInk $probe.Bitmap $g $InputRect
            $probeCapture=Save-GdiClientBitmap $probe ($EvidencePrefix+'-probe.png')
        } finally {$probe.Bitmap.Dispose()}
    } finally { $null=Send-ClientKey $Hwnd 'Backspace' }
    Start-Sleep -Milliseconds 150
    $after=Get-GdiClientBitmap $Hwnd
    try {
        $afterInk=Measure-SmokeInputInk $after.Bitmap $g $InputRect
        $afterCapture=Save-GdiClientBitmap $after ($EvidencePrefix+'-verified-empty.png')
    } finally {$after.Bitmap.Dispose()}
    if($probeInk.ProbePixels -lt 5*$g.Scale*$g.Scale -or $afterInk.WhitePixels -ne 0){throw 'Empty-line positive control failed; no command sent'}
    $result=[pscustomobject]@{Empty=$true;Method='106 Backspace + GDI white-glyph check + W positive control';Before=$blankCapture;Probe=$probeCapture;After=$afterCapture;BeforeInk=$blank;ProbeInk=$probeInk;AfterInk=$afterInk}
    Save-SmokeJson ($EvidencePrefix+'-clear.json') $result
    $result
}

function Send-SmokeCommand {
    param([Parameter(Mandatory)][IntPtr]$Hwnd,[Parameter(Mandatory)][string]$StatePath,
        [Parameter(Mandatory)][string]$TracePath,[Parameter(Mandatory)][string]$Command,
        [Parameter(Mandatory)][ValidateRange(0,255)][int]$ExpectedOpcode,
        [int[]]$ResponseOpcodes=@(),[ValidateRange(1,30)][int]$TimeoutSeconds=4,
        [Parameter(Mandatory)][string]$EvidencePrefix,[Parameter(Mandatory)][int[]]$InputRect,[switch]$Fill)
    if($Command.Length -eq 0 -or $Command.Length -gt 106 -or $Command -match '[\r\n\x00-\x1f]'){throw 'Command must be one nonempty line within the native 106-character limit'}
    $result=[ordered]@{Command=$Command;ExpectedOpcode=$ExpectedOpcode;ResponseOpcodes=$ResponseOpcodes;SendConfirmed=$false;ResponseObserved=$false;Status='not-attempted';Error=$null}
    try {
        # Require a working trace before touching the draft; TW_PACKET_TRACE must be set before launching the client.
        $null=Get-SmokeTraceMark $TracePath
        $result.Clear=Clear-SmokeChat $Hwnd $StatePath $EvidencePrefix $InputRect -Fill:$Fill
        Send-ClientText $Hwnd $Command
        Start-Sleep -Milliseconds 150
        $result.Typed=Get-GdiClientCapture $Hwnd ($EvidencePrefix+'-typed.png')
        $mark=Get-SmokeTraceMark $TracePath
        $result.SubmitUtc=[DateTime]::UtcNow.ToString('o')
        $null=Send-ClientKey $Hwnd 'Enter'
        $deadline=[DateTime]::UtcNow.AddSeconds($TimeoutSeconds);$rows=@()
        do {
            Start-Sleep -Milliseconds 100
            $rows=@(Read-SmokeTraceSince $mark)
            $sent=@($rows | Where-Object {$_.dir -eq 'out' -and $_.op -eq $ExpectedOpcode})
            if($sent.Count -gt 0){
                $result.SendConfirmed=$true
                $responses=@($rows | Where-Object {$_.dir -eq 'in' -and $ResponseOpcodes -contains [int]$_.op -and [datetime]$_.utc -ge [datetime]$sent[0].utc})
                $result.ResponseObserved=$responses.Count -gt 0
                if($result.ResponseObserved){break}
            }
        } while([DateTime]::UtcNow -lt $deadline)
        $result.TraceRows=$rows
        $result.Status=if(-not $result.SendConfirmed){'send-not-observed'}elseif($ResponseOpcodes.Count -eq 0){'sent-response-not-specified'}elseif($result.ResponseObserved){'sent-expected-response-observed'}else{'sent-no-expected-response-in-window'}
        $result.After=Get-GdiClientCapture $Hwnd ($EvidencePrefix+'-after.png')
        $result.State=Copy-SmokeState $StatePath ($EvidencePrefix+'-state.json')
    } catch { $result.Error=$_.Exception.Message;$result.Status='unknown-or-precondition-failed' }
    Save-SmokeJson ($EvidencePrefix+'-send.json') $result
    [pscustomobject]$result
}

function Convert-SmokeObjectPoint {
    param([Parameter(Mandatory)]$State,[Parameter(Mandatory)][uint32]$ObjectId,
        [ValidateRange(0.35,3)][double]$Zoom=1.5,[double[]]$BodyOffset=@(0,-24))
    if($State.state -ne 'InWorld' -or -not $State.world.renderable){throw 'Object selection requires a renderable world'}
    $a=$State.world.actor;$objects=@($State.world.objects | Where-Object ObjectId -EQ $ObjectId)
    if($null -eq $a -or $objects.Count -ne 1){throw 'Object must occur exactly once in current visible-object state'}
    $o=$objects[0]
    if($o.Dead -or $o.AppearancePending){throw 'Object is dead or its appearance is not ready'}
    if($BodyOffset.Count -ne 2){throw 'BodyOffset must be scene-pixel x,y'}
    # Coordinates.Screen + Camera.ToView; native WorldRect = (0,0,800,600), Follow=true, actor stopped.
    $dx=[double]$o.X-$a.X;$dy=[double]$o.Y-$a.Y
    $x=400+(24*($dx+$dy)+$BodyOffset[0])*$Zoom
    $y=300+(12*($dy-$dx)+$BodyOffset[1])*$Zoom
    if($x -lt 0 -or $x -ge 800 -or $y -lt 0 -or $y -ge 470){throw 'Projected body is outside the uncovered native scene'}
    [pscustomobject]@{ObjectId=$ObjectId;ActorId=$a.ObjectId;ActorX=$a.X;ActorY=$a.Y;ObjectX=$o.X;ObjectY=$o.Y;X=$x;Y=$y;Zoom=$Zoom;BodyOffset=$BodyOffset;Map=$State.world.mapId;Epoch=$State.world.worldEpoch;Revision=$State.revision}
}

function Select-SmokeObject {
    param([Parameter(Mandatory)][IntPtr]$Hwnd,[Parameter(Mandatory)][string]$StatePath,
        [Parameter(Mandatory)][uint32]$ObjectId,[Parameter(Mandatory)][string]$EvidencePrefix,
        [ValidateRange(0.35,3)][double]$Zoom=1.5,[double[]]$BodyOffset=@(0,-24),
        [ValidateRange(1,30)][int]$TimeoutSeconds=5,[switch]$Fill)
    $before=Read-SmokeJson $StatePath
    $point=Convert-SmokeObjectPoint $before $ObjectId -Zoom $Zoom -BodyOffset $BodyOffset
    $result=[ordered]@{Point=$point;Selected=$false;BeforeTarget=$before.world.targetId;AfterTarget=$null;Error=$null}
    try {
        if($before.world.targetId -eq $ObjectId){throw 'Already selected; clear selection first to prove this click'}
        $result.Before=Get-GdiClientCapture $Hwnd ($EvidencePrefix+'-before.png')
        $result.Click=Send-ClientClick $Hwnd $point.X $point.Y -Fill:$Fill
        $deadline=[DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
        do {
            Start-Sleep -Milliseconds 100;$after=Read-SmokeJson $StatePath
            if($after.state -ne 'InWorld' -or $after.world.worldEpoch -ne $point.Epoch -or $after.world.mapId -ne $point.Map){throw 'World changed during selection'}
            $result.AfterTarget=$after.world.targetId
            $result.Selected=($after.revision -gt $point.Revision -and $after.world.targetId -eq $ObjectId)
            if($result.Selected){break}
        } while([DateTime]::UtcNow -lt $deadline)
        $result.After=Get-GdiClientCapture $Hwnd ($EvidencePrefix+'-after.png')
        $result.State=Copy-SmokeState $StatePath ($EvidencePrefix+'-state.json')
        if(-not $result.Selected){$result.Error='Click not confirmed by a newer targetId snapshot; no automatic retry'}
    } catch {$result.Error=$_.Exception.Message}
    Save-SmokeJson ($EvidencePrefix+'-select.json') $result
    [pscustomobject]$result
}

function Close-SmokeClient {
    # Pass the actual Process returned by Start-Process (or the owned child with verified creation time).
    param([Parameter(Mandatory)][Diagnostics.Process]$Process,[Parameter(Mandatory)][IntPtr]$Hwnd,
        [Parameter(Mandatory)][string]$EventsPath,[ValidateRange(1,60)][int]$TimeoutSeconds=15)
    $closed=$false;$keyError=$null;$deadline=[DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    if(-not $Process.HasExited){
        if((Get-ClientWindowProcessId $Hwnd) -ne $Process.Id){throw 'Window does not belong to the owned process'}
        try {$null=Send-ClientKey $Hwnd 'Alt+F4'} catch {$keyError=$_.Exception.Message}
    }
    do {
        if(Test-Path -LiteralPath $EventsPath){
            try {$events=Read-SmokeJson $EventsPath;$closed=@($events | Where-Object {$_.PSObject.Properties['kind'] -and $_.kind -eq 'closed'}).Count -gt 0} catch {}
        }
        if($closed -and $Process.HasExited){break}
        Start-Sleep -Milliseconds 100
    } while([DateTime]::UtcNow -lt $deadline)
    # Do not race FormClosed or kill the launcher when its child is still draining. Caller decides on timeout cleanup.
    [pscustomobject]@{ProcessId=$Process.Id;Closed=$closed;Exited=$Process.HasExited;Passed=($closed -and $Process.HasExited);KeyError=$keyError}
}

function Record-SmokeLoopback {
    param([Parameter(Mandatory)][Diagnostics.Process]$Process,[Parameter(Mandatory)][string]$Path,
        [ValidateRange(1,30)][int]$Seconds=3,
        # Old strict mode: any other active render session rejects, even if its meter is silent.
        [switch]$RequireExclusive)
    if($Process.HasExited){throw 'Owned client has exited'}
    $repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
    $scratch=[IO.Path]::GetFullPath((Join-Path $repo 'scratch'))+[IO.Path]::DirectorySeparatorChar
    $full=[IO.Path]::GetFullPath($Path)
    if(-not $full.StartsWith($scratch,[StringComparison]::OrdinalIgnoreCase)){throw 'Loopback output must be under this worktree scratch/'}
    # Reject junctions on the output path, including an existing output file.
    for($p=$full;$p -and $p.Length -ge $scratch.TrimEnd('\').Length;$p=[IO.Path]::GetDirectoryName($p)){
        if((Test-Path -LiteralPath $p) -and ((Get-Item -LiteralPath $p -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)){throw 'Loopback output must not follow a junction/symlink'}
    }
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($full)) | Out-Null
    if(-not ('SmokeLoopback' -as [type])){Add-Type -Path (Join-Path $PSScriptRoot 'SmokeLoopback.cs')}
    # other-audible / other-active-unmetered: record as skipped (environment); never close other programs.
    $result=[SmokeLoopback]::Record($Process,$full,$Seconds,[bool]$RequireExclusive)
    Save-SmokeJson ($full+'.json') $result
    $result
}
