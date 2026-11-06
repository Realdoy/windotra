using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace WinCanvas {
 public sealed class GlobalShortcut : IDisposable {
  [DllImport("user32.dll")]static extern bool RegisterHotKey(IntPtr hwnd,int id,uint modifiers,uint key);
  [DllImport("user32.dll")]static extern bool UnregisterHotKey(IntPtr hwnd,int id);
  readonly Window window;readonly Action action;readonly Action<string> report;HwndSource source;IntPtr handle;bool registered;
  readonly int id=7710;readonly uint modifiers=0x4003,key=0x20;readonly string name="Ctrl+Alt+Space";
  public GlobalShortcut(Window window,Action action,Action<string> report){this.window=window;this.action=action;this.report=report;window.SourceInitialized+=Initialize;}
  public GlobalShortcut(Window window,Action action,Action<string> report,int id,uint modifiers,uint key,string name):this(window,action,report){this.id=id;this.modifiers=modifiers;this.key=key;this.name=name;}
  void Initialize(object sender,EventArgs args){handle=new WindowInteropHelper(window).Handle;source=HwndSource.FromHwnd(handle);source.AddHook(Hook);registered=RegisterHotKey(handle,id,modifiers,key);if(!registered)report(name+" is in use by another app.");}
  IntPtr Hook(IntPtr hwnd,int message,IntPtr wparam,IntPtr lparam,ref bool handled){if(message==0x0312&&wparam.ToInt32()==id){handled=true;try{action();}catch(Exception e){report(e.Message);}}return IntPtr.Zero;}
  public void Dispose(){window.SourceInitialized-=Initialize;if(registered)UnregisterHotKey(handle,id);if(source!=null)source.RemoveHook(Hook);}
 }
}
