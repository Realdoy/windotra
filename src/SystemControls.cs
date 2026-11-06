using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace WinCanvas {
 // Volume, brightness, dark mode, Wi-Fi name, and key presses for the menu bar and Control Center.
 public static class SystemControls {
  [ComImport,Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")] class DeviceEnumerator {}
  [ComImport,Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface IMMDeviceEnumerator { [PreserveSig] int EnumAudioEndpoints(int flow,int mask,out IntPtr devices); [PreserveSig] int GetDefaultAudioEndpoint(int flow,int role,out IMMDevice device); }
  [ComImport,Guid("D666063F-1587-4E43-81F1-B948E807363F"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface IMMDevice { [PreserveSig] int Activate(ref Guid id,int context,IntPtr parameters,[MarshalAs(UnmanagedType.IUnknown)] out object result); }
  [ComImport,Guid("5CDF2C82-841E-4546-9722-0CF74078229A"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface IAudioEndpointVolume {
   void RegisterControlChangeNotify(); void UnregisterControlChangeNotify(); void GetChannelCount(); void SetMasterVolumeLevel();
   [PreserveSig] int SetMasterVolumeLevelScalar(float level,ref Guid context);
   void GetMasterVolumeLevel();
   [PreserveSig] int GetMasterVolumeLevelScalar(out float level);
   void SetChannelVolumeLevel(); void SetChannelVolumeLevelScalar(); void GetChannelVolumeLevel(); void GetChannelVolumeLevelScalar();
   [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool mute,ref Guid context);
   [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
  }
  static IAudioEndpointVolume Endpoint(){try{var e=(IMMDeviceEnumerator)new DeviceEnumerator();IMMDevice device;if(e.GetDefaultAudioEndpoint(0,1,out device)!=0)return null;var id=typeof(IAudioEndpointVolume).GUID;object result;return device.Activate(ref id,23,IntPtr.Zero,out result)==0?result as IAudioEndpointVolume:null;}catch(Exception){return null;}}
  public static double Volume{get{var v=Endpoint();float level;return v!=null&&v.GetMasterVolumeLevelScalar(out level)==0?level:-1;}set{var v=Endpoint();if(v==null)return;var g=Guid.Empty;v.SetMasterVolumeLevelScalar((float)Math.Max(0,Math.Min(1,value)),ref g);}}
  public static bool Muted{get{var v=Endpoint();bool m;return v!=null&&v.GetMute(out m)==0&&m;}set{var v=Endpoint();if(v==null)return;var g=Guid.Empty;v.SetMute(value,ref g);}}
  // Built-in laptop screens can be dimmed; most desktop monitors can't, and then this returns -1.
  public static int Brightness{get{try{using(var s=new System.Management.ManagementObjectSearcher(@"root\WMI","SELECT CurrentBrightness FROM WmiMonitorBrightness"))foreach(System.Management.ManagementObject o in s.Get())return Convert.ToInt32(o["CurrentBrightness"]);}catch(Exception){}return -1;}
   set{try{using(var s=new System.Management.ManagementObjectSearcher(@"root\WMI","SELECT * FROM WmiMonitorBrightnessMethods"))foreach(System.Management.ManagementObject o in s.Get())o.InvokeMethod("WmiSetBrightness",new object[]{(uint)1,(byte)Math.Max(0,Math.Min(100,value))});}catch(Exception){}}}
  const string Personalize=@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
  public static bool DarkMode{get{using(var k=Microsoft.Win32.Registry.CurrentUser.OpenSubKey(Personalize)){var v=k==null?null:k.GetValue("AppsUseLightTheme");return v is int&&(int)v==0;}}
   set{using(var k=Microsoft.Win32.Registry.CurrentUser.CreateSubKey(Personalize)){k.SetValue("AppsUseLightTheme",value?0:1,Microsoft.Win32.RegistryValueKind.DWord);k.SetValue("SystemUsesLightTheme",value?0:1,Microsoft.Win32.RegistryValueKind.DWord);}new WindowsDesktop().Notify();}}
  public static string WifiName(){try{var info=new ProcessStartInfo("netsh","wlan show interfaces"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,StandardOutputEncoding=Encoding.GetEncoding(System.Globalization.CultureInfo.CurrentCulture.TextInfo.OEMCodePage)};using(var p=Process.Start(info)){string text=p.StandardOutput.ReadToEnd();p.WaitForExit(3000);foreach(var line in text.Split('\n')){var t=line.Trim();if(t.StartsWith("SSID")&&!t.StartsWith("SSID-")&&t.Contains(":")&&!t.StartsWith("BSSID"))return t.Substring(t.IndexOf(':')+1).Trim();}}}catch(Exception){}return null;}

  // ---- Keys, sent to the app in front (used by the menu bar's File, Edit, and View menus). ----
  [StructLayout(LayoutKind.Sequential)] struct Input { public int Type; public KeyInput Key; public long Pad; }
  [StructLayout(LayoutKind.Sequential)] struct KeyInput { public ushort Vk,Scan; public uint Flags,Time; public IntPtr Extra; }
  [DllImport("user32.dll")] static extern uint SendInput(uint count,Input[] inputs,int size);
  public const ushort Ctrl=0x11,Shift=0x10,Alt=0x12,Win=0x5B;
  public static void Press(params ushort[] keys){var list=new List<Input>();foreach(var k in keys)list.Add(new Input{Type=1,Key=new KeyInput{Vk=k}});foreach(var k in keys.Reverse())list.Add(new Input{Type=1,Key=new KeyInput{Vk=k,Flags=2}});SendInput((uint)list.Count,list.ToArray(),Marshal.SizeOf(typeof(Input)));}
  public static ushort Key(char c){return (ushort)char.ToUpperInvariant(c);}

  // ---- Classic Win32 menus: many desktop apps still have one, and the menu bar shows the real thing. ----
  [DllImport("user32.dll")] public static extern IntPtr GetMenu(IntPtr window);
  [DllImport("user32.dll")] static extern int GetMenuItemCount(IntPtr menu);
  [DllImport("user32.dll")] static extern IntPtr GetSubMenu(IntPtr menu,int position);
  [DllImport("user32.dll")] static extern uint GetMenuItemID(IntPtr menu,int position);
  [DllImport("user32.dll")] static extern uint GetMenuState(IntPtr menu,uint id,uint flags);
  [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetMenuString(IntPtr menu,uint id,StringBuilder text,int max,uint flags);
  [DllImport("user32.dll")] static extern bool PostMessage(IntPtr window,uint message,IntPtr w,IntPtr l);
  public class Entry { public string Text,Shortcut; public uint Id; public bool Separator,Disabled,Checked; public List<Entry> Children; }
  public static List<Entry> ReadMenu(IntPtr menu,int depth) {
   var list=new List<Entry>();int count=GetMenuItemCount(menu);if(count<=0||count>80)return list;
   for(int i=0;i<count;i++){
    uint state=GetMenuState(menu,(uint)i,0x400);var text=new StringBuilder(256);GetMenuString(menu,(uint)i,text,256,0x400);
    if((state&0x800)!=0||text.Length==0){list.Add(new Entry{Separator=true});continue;}
    string label=text.ToString(),shortcut="";int tab=label.IndexOf('\t');if(tab>=0){shortcut=label.Substring(tab+1);label=label.Substring(0,tab);}
    var entry=new Entry{Text=label.Replace("&&","\u0001").Replace("&","").Replace("\u0001","&"),Shortcut=shortcut,Disabled=(state&0x3)!=0,Checked=(state&0x8)!=0};
    IntPtr sub=GetSubMenu(menu,i);if(sub!=IntPtr.Zero){if(depth<3)entry.Children=ReadMenu(sub,depth+1);}else entry.Id=GetMenuItemID(menu,i);
    list.Add(entry);
   }
   return list;
  }
  public static void Command(IntPtr window,uint id){PostMessage(window,0x0111,new IntPtr(id),IntPtr.Zero);}

  // A window covering its whole monitor (a game, or a video in full screen).
  [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetClassName(IntPtr window,StringBuilder name,int max);
  [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr window,out LiveWallpaper.Rect32 rect);
  public static System.Windows.Forms.Screen FullScreenOn() {
   IntPtr top=GetForegroundWindow();if(top==IntPtr.Zero)return null;var name=new StringBuilder(64);GetClassName(top,name,64);string cls=name.ToString();
   if(cls=="WorkerW"||cls=="Progman"||cls=="Shell_TrayWnd"||cls=="Shell_SecondaryTrayWnd")return null;uint pid;GetWindowThreadProcessId(top,out pid);if(pid==(uint)Process.GetCurrentProcess().Id)return null;
   LiveWallpaper.Rect32 r;if(!GetWindowRect(top,out r))return null;var screen=System.Windows.Forms.Screen.FromHandle(top);var b=screen.Bounds;
   return r.Left<=b.Left&&r.Top<=b.Top&&r.Right>=b.Right&&r.Bottom>=b.Bottom?screen:null;
  }
  [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window,out uint process);
 }
}
