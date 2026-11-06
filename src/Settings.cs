using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Web.Script.Serialization;
using Microsoft.Win32;

namespace WinCanvas {
 public class SavedValue { public string Name; public bool Exists; public int Value; }
 public class Snapshot { public int Version = 1; public List<SavedValue> Values; public string Wallpaper; public bool WallpaperChanged; }
 public interface IDesktop {
  SavedValue Read(string name); void Write(SavedValue value); string GetWallpaper(); void SetWallpaper(string path); void Notify();
 }
 public class WindowsDesktop : IDesktop {
  const string Key = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
  [DllImport("user32.dll", CharSet=CharSet.Unicode, SetLastError=true)] static extern bool SystemParametersInfo(uint action, uint param, StringBuilder value, uint flags);
  [DllImport("user32.dll", CharSet=CharSet.Unicode, SetLastError=true, EntryPoint="SystemParametersInfoW")] static extern bool SetParameters(uint action, uint param, string value, uint flags);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] static extern IntPtr SendMessageTimeout(IntPtr window, uint message, UIntPtr wparam, string lparam, uint flags, uint timeout, out UIntPtr result);
  public SavedValue Read(string name) {
   using(var key=Registry.CurrentUser.OpenSubKey(Key)) {
    object value=key==null ? null : key.GetValue(name);
    if(value!=null && key.GetValueKind(name)!=RegistryValueKind.DWord) throw new InvalidOperationException("Unexpected registry type for " + name + ". No changes made.");
    return new SavedValue { Name=name, Exists=value!=null, Value=value==null ? 0 : (int)value };
   }
  }
  public void Write(SavedValue value) {
   using(var key=Registry.CurrentUser.CreateSubKey(Key)) {
    if(value.Exists) key.SetValue(value.Name,value.Value,RegistryValueKind.DWord); else key.DeleteValue(value.Name,false);
   }
  }
  public string GetWallpaper() {
   var path=new StringBuilder(32768);
   if(!SystemParametersInfo(0x0073,(uint)path.Capacity,path,0)) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
   return path.ToString();
  }
  public void SetWallpaper(string path) {
   if(!SetParameters(0x0014,0,path,3)) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
  }
  public void Notify() { UIntPtr result; SendMessageTimeout(new IntPtr(0xffff),0x001A,UIntPtr.Zero,"ImmersiveColorSet",2,1000,out result); }
 }
 public class SettingsEngine {
  public static readonly string[] Names={"AppsUseLightTheme","SystemUsesLightTheme","EnableTransparency"};
  readonly IDesktop desktop; readonly string backup;
  public SettingsEngine(IDesktop desktop, string backup) { this.desktop=desktop; this.backup=backup; }
  public bool CanUndo { get { return File.Exists(backup); } }
  public static void Validate(Snapshot snapshot) {
   if(snapshot==null || snapshot.Version!=1 || snapshot.Values==null || snapshot.Values.Count!=3 || snapshot.Wallpaper==null) throw new InvalidDataException("Invalid recovery file.");
   for(int i=0;i<Names.Length;i++) if(snapshot.Values[i]==null || snapshot.Values[i].Name!=Names[i]) throw new InvalidDataException("Invalid recovery settings.");
  }
  public static Snapshot Decode(string json) { var result=new JavaScriptSerializer().Deserialize<Snapshot>(json); Validate(result); return result; }
  Snapshot Capture(bool wallpaperChanged) {
   var snapshot=new Snapshot { Values=new List<SavedValue>(), Wallpaper=desktop.GetWallpaper(), WallpaperChanged=wallpaperChanged };
   foreach(string name in Names) snapshot.Values.Add(desktop.Read(name));
   return snapshot;
  }
  void Save(Snapshot snapshot) {
   Directory.CreateDirectory(Path.GetDirectoryName(backup));
   string temp=backup+".tmp";
   File.WriteAllText(temp,new JavaScriptSerializer().Serialize(snapshot),Encoding.UTF8);
   if(File.Exists(backup)) File.Replace(temp,backup,null); else File.Move(temp,backup);
  }
  void Restore(Snapshot snapshot) {
   Validate(snapshot);
   // Attempt every restore operation, even if one fails. Keep backup on any failure.
   var errors=new List<Exception>();
   foreach(var value in snapshot.Values) { try { desktop.Write(value); } catch(Exception e) { errors.Add(e); } }
   if(snapshot.WallpaperChanged) { try { desktop.SetWallpaper(snapshot.Wallpaper); } catch(Exception e) { errors.Add(e); } }
   desktop.Notify();
   if(errors.Count>0) throw new AggregateException("Some settings could not be restored. Recovery file retained.",errors);
  }
  public void Apply(bool appsLight, bool systemLight, bool transparency, string wallpaper) {
   var snapshot=Capture(wallpaper!=null);
   Save(snapshot); // Durable recovery must exist before any Windows changes.
   try {
    int[] values={appsLight?1:0,systemLight?1:0,transparency?1:0};
    for(int i=0;i<Names.Length;i++) desktop.Write(new SavedValue { Name=Names[i], Exists=true, Value=values[i] });
    if(wallpaper!=null) desktop.SetWallpaper(wallpaper);
    desktop.Notify();
   } catch(Exception original) {
    try { Restore(snapshot); } catch(Exception recovery) { throw new AggregateException("Apply failed; automatic recovery was incomplete. Use Undo to retry.",original,recovery); }
    throw new InvalidOperationException("Apply failed. Previous settings were restored. " + original.Message,original);
   }
  }
  public void Undo() { var snapshot=Decode(File.ReadAllText(backup)); Restore(snapshot); File.Delete(backup); }
 }
}
