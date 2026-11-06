using System.Runtime.InteropServices;
namespace P24.Audio;

// Real zero-PCM stream, owned solely by the combat transport thread. This keeps a
// process audio session alive between events, including mute; never proves an event played.
internal interface IAudioSessionLifetime
{
    bool IsOpen { get; }
    void Start();
    void Stop();
}
internal sealed class SilentAudioSession : IAudioSessionLifetime
{
    nint device, data, header;
    bool prepared;
    public bool IsOpen => device != 0;
    [StructLayout(LayoutKind.Sequential, Pack=2)]
    struct WaveFormat { public ushort Tag, Channels; public uint Samples, Bytes; public ushort Align, Bits, Extra; }
    [StructLayout(LayoutKind.Sequential)]
    struct WaveHeader { public nint Data; public uint Length, Recorded; public nuint User; public uint Flags, Loops; public nint Next; public nuint Reserved; }
    static readonly uint HeaderSize=(uint)Marshal.SizeOf<WaveHeader>();
    static void Check(uint code) { if(code!=0)throw new InvalidOperationException($"Silent audio stream MMRESULT {code}"); }
    public void Start()
    {
        if(IsOpen)return;
        try {
            var format=new WaveFormat {Tag=1,Channels=1,Samples=22050,Bytes=44100,Align=2,Bits=16};
            Check(waveOutOpen(out device,uint.MaxValue,ref format,0,0,0));
            byte[] silence=new byte[44100];
            data=Marshal.AllocHGlobal(silence.Length);Marshal.Copy(silence,0,data,silence.Length);
            header=Marshal.AllocHGlobal((int)HeaderSize);
            Marshal.StructureToPtr(new WaveHeader{Data=data,Length=(uint)silence.Length,Flags=4|8,Loops=uint.MaxValue},header,false);
            Check(waveOutPrepareHeader(device,header,HeaderSize));prepared=true;
            Check(waveOutWrite(device,header,HeaderSize));
        } catch { Stop();throw; }
    }
    public void Stop()
    {
        // Preserve owned buffers/handle on native failure so the worker can retry safely.
        if(device!=0){
            Check(waveOutReset(device));
            if(prepared){Check(waveOutUnprepareHeader(device,header,HeaderSize));prepared=false;}
            Check(waveOutClose(device));device=0;
        }
        if(header!=0){Marshal.FreeHGlobal(header);header=0;}
        if(data!=0){Marshal.FreeHGlobal(data);data=0;}
    }
    [DllImport("winmm.dll")] static extern uint waveOutOpen(out nint h,uint id,ref WaveFormat format,nint callback,nint instance,uint flags);
    [DllImport("winmm.dll")] static extern uint waveOutPrepareHeader(nint h,nint header,uint size);
    [DllImport("winmm.dll")] static extern uint waveOutWrite(nint h,nint header,uint size);
    [DllImport("winmm.dll")] static extern uint waveOutReset(nint h);
    [DllImport("winmm.dll")] static extern uint waveOutUnprepareHeader(nint h,nint header,uint size);
    [DllImport("winmm.dll")] static extern uint waveOutClose(nint h);
}
