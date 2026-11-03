using System.Runtime.InteropServices;
namespace P24.Audio;

// Prefixes/order checked against Microsoft Windows SDK control.h/strmif.h.
// Preserve HRESULT; LONG remains Int32, LONG_PTR is nint on both x86/x64.
[ComImport, Guid("56a868b1-0ad4-11ce-b03a-0020af0ba770"), InterfaceType(ComInterfaceType.InterfaceIsDual)]
internal interface IMediaControl
{
    [PreserveSig] int Run();
    [PreserveSig] int Pause();
    [PreserveSig] int Stop();
}
[ComImport, Guid("56a868b3-0ad4-11ce-b03a-0020af0ba770"), InterfaceType(ComInterfaceType.InterfaceIsDual)]
internal interface IBasicAudio { [PreserveSig] int put_Volume(int value); }
[ComImport, Guid("56a868b6-0ad4-11ce-b03a-0020af0ba770"), InterfaceType(ComInterfaceType.InterfaceIsDual)]
internal interface IMediaEvent
{
    [PreserveSig] int GetEventHandle(out nint handle);
    [PreserveSig] int GetEvent(out int code, out nint one, out nint two, int timeoutMs);
    [PreserveSig] int WaitForCompletion(int timeoutMs, out int code);
    [PreserveSig] int CancelDefaultHandling(int code);
    [PreserveSig] int RestoreDefaultHandling(int code);
    [PreserveSig] int FreeEventParams(int code, nint one, nint two);
}
[ComImport, Guid("56a86895-0ad4-11ce-b03a-0020af0ba770"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IBaseFilter
{
    [PreserveSig] int GetClassID(out Guid id);
    [PreserveSig] int Stop();
    [PreserveSig] int Pause();
    [PreserveSig] int Run(long start);
    [PreserveSig] int GetState(int timeout, out int state);
    [PreserveSig] int SetSyncSource(nint clock);
    [PreserveSig] int GetSyncSource(out nint clock);
    [PreserveSig] int EnumPins(out IEnumPins pins);
}
[ComImport, Guid("56a86892-0ad4-11ce-b03a-0020af0ba770"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IEnumPins
{
    [PreserveSig] int Next(int count, [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex=0)] IPin[] pins, out int fetched);
}
[ComImport, Guid("56a86891-0ad4-11ce-b03a-0020af0ba770"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IPin
{
    [PreserveSig] int Connect(IPin receive, nint mediaType);
    [PreserveSig] int ReceiveConnection(IPin connector, nint mediaType);
    [PreserveSig] int Disconnect();
    [PreserveSig] int ConnectedTo(out IPin pin);
    [PreserveSig] int ConnectionMediaType(nint mediaType);
    [PreserveSig] int QueryPinInfo(nint info);
    [PreserveSig] int QueryDirection(out int direction);
}
[ComImport, Guid("36b73882-c2c8-11cf-8b46-00805f6cef60"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IFilterGraph2
{
    [PreserveSig] int AddFilter(IBaseFilter filter, [MarshalAs(UnmanagedType.LPWStr)] string name);
    [PreserveSig] int RemoveFilter(IBaseFilter filter);
    [PreserveSig] int EnumFilters(out nint filters);
    [PreserveSig] int FindFilterByName([MarshalAs(UnmanagedType.LPWStr)] string name, out IBaseFilter filter);
    [PreserveSig] int ConnectDirect(IPin output, IPin input, nint mediaType);
    [PreserveSig] int Reconnect(IPin pin);
    [PreserveSig] int Disconnect(IPin pin);
    [PreserveSig] int SetDefaultSyncSource();
    [PreserveSig] int Connect(IPin output, IPin input);
    [PreserveSig] int Render(IPin output);
    [PreserveSig] int RenderFile([MarshalAs(UnmanagedType.LPWStr)] string path, [MarshalAs(UnmanagedType.LPWStr)] string? playlist);
    [PreserveSig] int AddSourceFilter([MarshalAs(UnmanagedType.LPWStr)] string path, [MarshalAs(UnmanagedType.LPWStr)] string name, out IBaseFilter filter);
    [PreserveSig] int SetLogFile(nint file);
    [PreserveSig] int Abort();
    [PreserveSig] int ShouldOperationContinue();
    [PreserveSig] int AddSourceFilterForMoniker(nint moniker, nint context, [MarshalAs(UnmanagedType.LPWStr)] string name, out IBaseFilter filter);
    [PreserveSig] int ReconnectEx(IPin pin, nint mediaType);
    [PreserveSig] int RenderEx(IPin output, uint flags, nint reserved);
}
