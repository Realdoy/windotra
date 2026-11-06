using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Web.Script.Serialization;
using Microsoft.Win32;

namespace WinCanvas {
 public static class JsonFile {
  public static T Read<T>(string path) { if(new FileInfo(path).Length>262144) throw new InvalidDataException("File is too large."); return new JavaScriptSerializer().Deserialize<T>(File.ReadAllText(path)); }
  public static void Save<T>(string path,T value) {
   Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
   string temp=path+".tmp"; File.WriteAllText(temp,new JavaScriptSerializer().Serialize(value));
   if(File.Exists(path)) File.Replace(temp,path,null); else File.Move(temp,path);
  }
 }
 public class ChecklistItem { public string Text; public bool Done; }
 public class Position { public double X; public double Y; }
 public class DesktopProfile {
  public int Version=2;
  public string Font="Inter", Accent="#4F46E5", Background="#FFFFFF", Notes="Make room for your next idea.", Scene="Drift", ScenePhoto="", SceneColor1="", SceneColor2="", SceneColor3="", SceneBackground="";
  public double SceneSpeed=1, SceneDensity=1, SceneDetail=0.5; public int SceneFps=30;
  public double FontSize=14, Opacity=0.97, Radius=14, WidgetWidth=280, IslandWidth=380;
  public int FocusMinutes=25;
  public double IslandExpandedWidth=440; public bool IslandHover=true, FocusSound=false, LiveWallpaper=false, PhotoMotion=true, SceneDark=false, IslandMedia=true, IslandDownloads=true, IslandGlow=false, IslandAura=false;
  public List<ChecklistItem> Tasks=new List<ChecklistItem>();
  public Dictionary<string,WidgetLook> Looks=new Dictionary<string,WidgetLook>(); public bool ShowGrid=true, SnapGrid=true; public double GridSize=24;
  public List<LaunchItem> Launchers=LaunchItem.Defaults(); public bool DockAi=true, DockFocus=true, DockTasks=true, DockSystem=true;
  public bool Clock=true, Stats=true, Note=false, Focus=false, Calendar=false, Day=true, Glass=true, Music=false, Reminders=false, ClockAnalog=true, Island=true, Topmost=true, Clock24=true, Locked=false;
  public Dictionary<string,Position> Positions=new Dictionary<string,Position>();
  // Live wallpaper: your video, how pictures fit, one picture per screen or one across all, and per-screen choices.
  public string SceneVideo="", WallpaperFit="Fill", WallpaperLayout="Each"; public Dictionary<string,string> ScreenMedia=new Dictionary<string,string>();
  // Island design.
  public string IslandShape="Pill", IslandColor="#000000", IslandIdle="Clock", IslandText=""; public double IslandScale=1;
  // macOS-style menu bar and Dock.
  public bool MenuBar=false, MenuBarLight=false, MenuBarReserve=true, Dock=false, DockMagnify=true, DockRunning=true, DockAutoHide=false, DockLight=false; public double DockSize=52;
  public List<LaunchItem> DockApps=new List<LaunchItem>(); public bool DockTiles=false, Off=false; public List<string> CustomWidgets=new List<string>();
  public static readonly string[] Fits={"Fill","Fit","Stretch","Center"},Layouts={"Each","Span"},Shapes={"Pill","Notch","Rounded"},Idles={"Clock","Date","Clock and date","Text","Nothing"};
  public void Validate() {
   if(Version!=2 || String.IsNullOrWhiteSpace(Font) || Font.Length>100 || Notes==null || Notes.Length>10000 || Positions==null || Positions.Count>40 || CustomWidgets==null || CustomWidgets.Count>20 || CustomWidgets.Any(c=>c==null||!c.StartsWith("custom:")||c.Length>60)) throw new InvalidDataException("Invalid desktop profile.");
   if(!System.Text.RegularExpressions.Regex.IsMatch(Accent??"","^#[0-9A-Fa-f]{6}$") || !System.Text.RegularExpressions.Regex.IsMatch(Background??"","^#[0-9A-Fa-f]{6}$")) throw new InvalidDataException("Colors must use #RRGGBB.");
   LaunchItem.Validate(Launchers);
   if(Looks==null||Looks.Count>40)throw new InvalidDataException("Invalid widget looks.");
   foreach(var look in Looks){if((!new[]{"clock","stats","notes","focus","calendar","music","reminders","day"}.Contains(look.Key)&&!look.Key.StartsWith("custom:"))||look.Value==null||look.Value.Tint==null||(look.Value.Tint.Length>0&&!System.Text.RegularExpressions.Regex.IsMatch(look.Value.Tint,"^#[0-9A-Fa-f]{6}$")))throw new InvalidDataException("Invalid widget look.");Range(look.Value.Scale,0.6,1.8);}
   Range(GridSize,8,80);
   if(SceneVideo==null||SceneVideo.Length>1024||!Fits.Contains(WallpaperFit)||!Layouts.Contains(WallpaperLayout)||ScreenMedia==null||ScreenMedia.Count>16||ScreenMedia.Any(m=>m.Key==null||m.Key.Length>64||m.Value==null||m.Value.Length>1024||(m.Value.StartsWith("scene:")&&!WallpaperScene.Kinds.Contains(m.Value.Substring(6)))))throw new InvalidDataException("Invalid wallpaper layout.");
   if(!Shapes.Contains(IslandShape)||!Idles.Contains(IslandIdle)||IslandText==null||IslandText.Length>40||!System.Text.RegularExpressions.Regex.IsMatch(IslandColor??"","^#([0-9A-Fa-f]{2})?[0-9A-Fa-f]{6}$"))throw new InvalidDataException("Invalid island design.");
   Range(IslandScale,0.8,1.4);Range(DockSize,32,96);
   if(DockApps==null||DockApps.Count>30||DockApps.Any(i=>i==null||String.IsNullOrWhiteSpace(i.Target)||i.Target.Length>1024||i.Name==null||i.Name.Length>60))throw new InvalidDataException("Invalid Dock apps.");
   if(Tasks==null||Tasks.Count>40||Tasks.Any(t=>t==null||String.IsNullOrWhiteSpace(t.Text)||t.Text.Length>250))throw new InvalidDataException("Invalid checklist.");
   if(Scene==null||!WallpaperScene.Kinds.Contains(Scene)) throw new InvalidDataException("Unknown live wallpaper scene.");
   foreach(string color in new[]{SceneColor1,SceneColor2,SceneColor3,SceneBackground}) if(color==null||(color.Length>0&&!System.Text.RegularExpressions.Regex.IsMatch(color,"^#[0-9A-Fa-f]{6}$"))) throw new InvalidDataException("Scene colors must use #RRGGBB.");
   if(ScenePhoto==null||ScenePhoto.Length>1024)throw new InvalidDataException("Invalid background photo.");
   Range(SceneSpeed,0.1,4); Range(SceneDensity,0.3,2); Range(SceneDetail,0.2,1); Range(SceneFps,5,60);
   Range(IslandExpandedWidth,340,720);Range(FontSize,11,28); Range(Opacity,0.35,1); Range(Radius,0,40); Range(WidgetWidth,240,480); Range(IslandWidth,340,640); Range(FocusMinutes,1,180);
   foreach(var item in Positions) { if((!new[]{"clock","stats","notes","focus","calendar","day","music","reminders","island"}.Contains(item.Key)&&!item.Key.StartsWith("custom:")) || item.Key.Length>60 || item.Value==null) throw new InvalidDataException("Invalid widget position."); Range(item.Value.X,-100000,100000); Range(item.Value.Y,-100000,100000); }
  }
  static void Range(double v,double min,double max) { if(Double.IsNaN(v)||Double.IsInfinity(v)||v<min||v>max) throw new InvalidDataException("Profile value is outside the supported range."); }
 }
 [StructLayout(LayoutKind.Sequential,CharSet=System.Runtime.InteropServices.CharSet.Unicode)]
 public struct DesktopFont {
  public int Height,Width,Escapement,Orientation,Weight;
  public byte Italic,Underline,StrikeOut,CharSet,OutPrecision,ClipPrecision,Quality,PitchAndFamily;
  [MarshalAs(UnmanagedType.ByValTStr,SizeConst=32)] public string Face;
 }
 [StructLayout(LayoutKind.Sequential,CharSet=System.Runtime.InteropServices.CharSet.Unicode)]
 public struct WindowMetrics {
  public uint Size; public int BorderWidth,ScrollWidth,ScrollHeight,CaptionWidth,CaptionHeight;
  public DesktopFont Caption; public int SmallCaptionWidth,SmallCaptionHeight;
  public DesktopFont SmallCaption; public int MenuWidth,MenuHeight;
  public DesktopFont Menu,Status,Message; public int PaddedBorderWidth;
 }
 public class FontSnapshot { public int Version=1; public DesktopFont Caption,SmallCaption,Menu,Status,Message; }
 public class CursorValue { public string Role,Path; public bool Exists; public int Kind; }
 public class CursorSnapshot { public int Version=1; public List<CursorValue> Values; }
 public interface ICustomization {
  FontSnapshot ReadFonts(); void WriteFonts(FontSnapshot value);
  List<CursorValue> ReadCursors(); void WriteCursors(List<CursorValue> values);
 }
 public class NativeCustomization : ICustomization {
  public static readonly string[] Roles={"Arrow","Hand","IBeam","Wait","AppStarting","Crosshair","Help","No","SizeAll","SizeNS","SizeWE","SizeNWSE","SizeNESW","UpArrow"};
  [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr window);
  [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr window,IntPtr dc);
  [DllImport("gdi32.dll")] static extern int GetDeviceCaps(IntPtr dc,int index);
  public static int Dpi() { var dc=GetDC(IntPtr.Zero); if(dc==IntPtr.Zero) return 96; try { int dpi=GetDeviceCaps(dc,90); return dpi>0?dpi:96; } finally { ReleaseDC(IntPtr.Zero,dc); } }
  const string CursorKey=@"Control Panel\Cursors";
  [DllImport("user32.dll",EntryPoint="SystemParametersInfoW",SetLastError=true)] static extern bool Metrics(uint action,uint size,ref WindowMetrics value,uint flags);
  [DllImport("user32.dll",EntryPoint="SystemParametersInfoW",SetLastError=true)] static extern bool Parameters(uint action,uint size,IntPtr value,uint flags);
  [DllImport("user32.dll",CharSet=System.Runtime.InteropServices.CharSet.Unicode,SetLastError=true)] static extern IntPtr LoadCursorFromFile(string path);
  [DllImport("user32.dll")] static extern bool DestroyCursor(IntPtr cursor);
  static void Check(bool result) { if(!result) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error()); }
  static WindowMetrics ReadMetrics() { var m=new WindowMetrics(); m.Size=(uint)Marshal.SizeOf(typeof(WindowMetrics)); Check(Metrics(0x29,m.Size,ref m,0)); return m; }
  public FontSnapshot ReadFonts() { var m=ReadMetrics(); return new FontSnapshot{Caption=m.Caption,SmallCaption=m.SmallCaption,Menu=m.Menu,Status=m.Status,Message=m.Message}; }
  public void WriteFonts(FontSnapshot f) {
   var m=ReadMetrics(); m.Caption=f.Caption; m.SmallCaption=f.SmallCaption; m.Menu=f.Menu; m.Status=f.Status; m.Message=f.Message;
   Check(Metrics(0x2A,m.Size,ref m,3));
  }
  public List<CursorValue> ReadCursors() {
   var values=new List<CursorValue>();
   using(var key=Registry.CurrentUser.OpenSubKey(CursorKey)) foreach(string role in Roles) {
    object value=key==null?null:key.GetValue(role,null,RegistryValueOptions.DoNotExpandEnvironmentNames);
    if(value!=null && !(value is string)) throw new InvalidDataException("Unexpected cursor registry value.");
    values.Add(new CursorValue{Role=role,Exists=value!=null,Path=value as string,Kind=value==null?(int)RegistryValueKind.String:(int)key.GetValueKind(role)});
   }
   return values;
  }
  public void WriteCursors(List<CursorValue> values) {
   using(var key=Registry.CurrentUser.CreateSubKey(CursorKey)) foreach(var v in values) {
    if(v.Exists) key.SetValue(v.Role,v.Path,(RegistryValueKind)v.Kind); else key.DeleteValue(v.Role,false);
   }
   Check(Parameters(0x57,0,IntPtr.Zero,0));
  }
  public static void ValidateCursor(string path) {
   string ext=System.IO.Path.GetExtension(path).ToLowerInvariant();
   if(!File.Exists(path)||(ext!=".cur"&&ext!=".ani") || new FileInfo(path).Length>10*1024*1024) throw new InvalidDataException("Choose a .cur or .ani cursor file under 10 MB.");
   IntPtr cursor=LoadCursorFromFile(path); if(cursor==IntPtr.Zero) throw new InvalidDataException("Windows could not load this cursor file."); DestroyCursor(cursor);
  }
 }
 public class CustomizationService {
  readonly ICustomization native; readonly string folder;
  public CustomizationService(ICustomization native,string folder) { this.native=native; this.folder=folder; }
  string FontPath { get { return Path.Combine(folder,"fonts-undo.json"); } }
  string CursorPath { get { return Path.Combine(folder,"cursors-undo.json"); } }
  public bool FontUndo { get { return File.Exists(FontPath); } }
  public bool CursorUndo { get { return File.Exists(CursorPath); } }
  static DesktopFont Change(DesktopFont f,string family,int points) { f.Face=family; f.Height=-(int)Math.Round(points*NativeCustomization.Dpi()/72.0); return f; }
  public void ApplyFonts(string family,int points) {
   if(!FontLibrary.HasLetters(family))throw new InvalidOperationException(family+" is an icon or symbol font with no letters, so Windows text would turn into boxes. Pick a normal font.");
   if(String.IsNullOrWhiteSpace(family)||family.Length>31||points<8||points>20) throw new InvalidDataException("Choose a font with a name up to 31 characters and a size from 8–20 pt.");
   var previous=native.ReadFonts(); if(!File.Exists(FontPath)) JsonFile.Save(FontPath,previous);
   var next=new FontSnapshot{Caption=Change(previous.Caption,family,points),SmallCaption=Change(previous.SmallCaption,family,points),Menu=Change(previous.Menu,family,points),Status=Change(previous.Status,family,points),Message=Change(previous.Message,family,points)};
   try { native.WriteFonts(next); } catch(Exception e) { try { native.WriteFonts(previous); } catch(Exception r) { throw new AggregateException("Font apply and rollback failed. Recovery retained.",e,r); } throw new InvalidOperationException("Font apply failed; previous fonts restored.",e); }
  }
  public void UndoFonts() {
   var f=JsonFile.Read<FontSnapshot>(FontPath);
   if(f==null||f.Version!=1) throw new InvalidDataException("Invalid font backup.");
   foreach(var item in new[]{f.Caption,f.SmallCaption,f.Menu,f.Status,f.Message}) if(String.IsNullOrWhiteSpace(item.Face)||item.Face.Length>31||Math.Abs((long)item.Height)>500) throw new InvalidDataException("Invalid font backup.");
   native.WriteFonts(f); File.Delete(FontPath);
  }
  public void ApplyCursors(Dictionary<string,string> paths) {
   if(paths.Count==0) throw new InvalidDataException("Choose at least one cursor file.");
   foreach(var p in paths) { if(!NativeCustomization.Roles.Contains(p.Key)) throw new InvalidDataException("Unknown cursor role."); NativeCustomization.ValidateCursor(p.Value); }
   var copied=new Dictionary<string,string>(); Directory.CreateDirectory(Path.Combine(folder,"cursors"));
   foreach(var p in paths) { string path=Path.Combine(folder,"cursors",Guid.NewGuid().ToString("N")+Path.GetExtension(p.Value)); File.Copy(p.Value,path); copied[p.Key]=path; }
   ApplyCursorValues(copied);
  }
  internal void ApplyCursorValues(Dictionary<string,string> paths) {
   var previous=native.ReadCursors(); if(!File.Exists(CursorPath)) JsonFile.Save(CursorPath,new CursorSnapshot{Values=previous});
   var next=previous.Select(v=>new CursorValue{Role=v.Role,Exists=v.Exists,Path=v.Path,Kind=v.Kind}).ToList();
   foreach(var v in next) if(paths.ContainsKey(v.Role)) { v.Exists=true; v.Path=paths[v.Role]; v.Kind=(int)RegistryValueKind.String; }
   try { native.WriteCursors(next); } catch(Exception e) { try { native.WriteCursors(previous); } catch(Exception r) { throw new AggregateException("Cursor apply and rollback failed. Recovery retained.",e,r); } throw new InvalidOperationException("Cursor apply failed; previous cursors restored.",e); }
  }
  public void UndoCursors() {
   var s=JsonFile.Read<CursorSnapshot>(CursorPath);
   if(s==null||s.Version!=1||s.Values==null||s.Values.Count!=NativeCustomization.Roles.Length) throw new InvalidDataException("Invalid cursor backup.");
   for(int i=0;i<s.Values.Count;i++) { var v=s.Values[i]; if(v==null||v.Role!=NativeCustomization.Roles[i]||(v.Exists&&(v.Path==null||(v.Kind!=1&&v.Kind!=2)))) throw new InvalidDataException("Invalid cursor backup."); }
   native.WriteCursors(s.Values); File.Delete(CursorPath);
  }
 }
}
