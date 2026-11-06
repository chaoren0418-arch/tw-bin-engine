#nullable disable
// Read-only meter adapted from the existing dot offline helper; only the supplied own PID is sampled.
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
public static class Meter71 {
 [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")] class Enumerator {}
 [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface IEnum {
  [PreserveSig] int EnumAudioEndpoints(int flow,uint mask,out IntPtr devices);
  [PreserveSig] int GetDefaultAudioEndpoint(int flow,int role,out IDevice device);
 }
 [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface IDevice {
  [PreserveSig] int Activate(ref Guid iid,uint clsctx,IntPtr p,[MarshalAs(UnmanagedType.IUnknown)] out object o);
 }
 [ComImport, Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface IManager {
  [PreserveSig] int GetAudioSessionControl(IntPtr guid,uint flags,out IntPtr o);
  [PreserveSig] int GetSimpleAudioVolume(IntPtr guid,uint flags,out IntPtr o);
  [PreserveSig] int GetSessionEnumerator(out ISessions sessions);
 }
 [ComImport, Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface ISessions {
  [PreserveSig] int GetCount(out int count);
  [PreserveSig] int GetSession(int index,[MarshalAs(UnmanagedType.IUnknown)] out object session);
 }
 [ComImport, Guid("BFB7FF88-7239-4FC9-8FA2-07C950BE9C6D"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface IControl {
  [PreserveSig] int GetState(out int state);
  [PreserveSig] int GetDisplayName(out IntPtr name);
  [PreserveSig] int SetDisplayName(IntPtr name,IntPtr context);
  [PreserveSig] int GetIconPath(out IntPtr path);
  [PreserveSig] int SetIconPath(IntPtr path,IntPtr context);
  [PreserveSig] int GetGroupingParam(out Guid grouping);
  [PreserveSig] int SetGroupingParam(ref Guid grouping,IntPtr context);
  [PreserveSig] int RegisterAudioSessionNotification(IntPtr events);
  [PreserveSig] int UnregisterAudioSessionNotification(IntPtr events);
  [PreserveSig] int GetSessionIdentifier(out IntPtr id);
  [PreserveSig] int GetSessionInstanceIdentifier(out IntPtr id);
  [PreserveSig] int GetProcessId(out uint pid);
 }
 [ComImport, Guid("C02216F6-8C67-4B5B-9D00-D008E73E0064"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface IMeter {
  [PreserveSig] int GetPeakValue(out float value);
 }
 public class Reading {public string utc; public int sessions; public float? peak; public string[] instances;}
 static void Check(int hr){Marshal.ThrowExceptionForHR(hr);}
 public static Reading[] Sample(uint pid,int seconds){
  var enumerator=(IEnum)(object)new Enumerator(); IDevice device; Check(enumerator.GetDefaultAudioEndpoint(0,0,out device));
  Guid iid=typeof(IManager).GUID;object managerObj;Check(device.Activate(ref iid,23,IntPtr.Zero,out managerObj));var manager=(IManager)managerObj;
  var result=new List<Reading>();
  for(int i=0;i<seconds*10;i++){
   ISessions sessions;Check(manager.GetSessionEnumerator(out sessions));int count;Check(sessions.GetCount(out count));float max=0;int matched=0;var identifiers=new List<string>();
   for(int j=0;j<count;j++){object control;Check(sessions.GetSession(j,out control));try{uint owner;Check(((IControl)control).GetProcessId(out owner));if(owner==pid){float v;Check(((IMeter)control).GetPeakValue(out v));max=Math.Max(max,v);matched++;IntPtr identity;Check(((IControl)control).GetSessionInstanceIdentifier(out identity));try{identifiers.Add(Marshal.PtrToStringUni(identity));}finally{Marshal.FreeCoTaskMem(identity);}}}finally{Marshal.ReleaseComObject(control);}}
   Marshal.ReleaseComObject(sessions);result.Add(new Reading{utc=DateTime.UtcNow.ToString("o"),sessions=matched,peak=matched>0?(float?)max:null,instances=identifiers.ToArray()});Thread.Sleep(100);
  }
  Marshal.ReleaseComObject(managerObj);Marshal.ReleaseComObject(device);Marshal.ReleaseComObject(enumerator);return result.ToArray();
 }
}
