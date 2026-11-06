using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Web.Script.Serialization;

namespace WinCanvas {
 // A style is a whole shareable look: wallpaper, widgets and where they sit, the island, the menu bar, and the Dock.
 // It never carries notes, tasks, apps, files, or anything that runs. Files are plain JSON (.windotra-style).
 public class StyleFile {
  public string Format="windotra-style"; public int Version=1;
  public string Name="",Author="",AuthorUrl="",Description="";
  public Dictionary<string,object> Settings=new Dictionary<string,object>();
  [ScriptIgnore] public string Path;
 }
 public static class Styles {
  public const string Extension=".windotra-style";
  // Only these profile fields may be set by a style, plus "Layout" (a named widget arrangement).
  static readonly string[] Allowed={"Font","Accent","Background","FontSize","Opacity","Radius","Glass","ClockAnalog","Clock24","Scene","SceneColor1","SceneColor2","SceneColor3","SceneBackground","SceneSpeed","SceneDensity","SceneDark","LiveWallpaper","WallpaperFit","IslandMedia","IslandDownloads","IslandAura","IslandHover","Island","IslandShape","IslandColor","IslandIdle","IslandScale","Clock","Stats","Note","Focus","Calendar","Music","Reminders","Day","WidgetWidth","MenuBar","MenuBarLight","Dock","DockLight","DockMagnify","DockSize","DockTiles"};
  public static readonly string[] Layouts={"keep","macos","windows","column","clear"};
  static StyleFile Make(string name,string description,params object[] pairs){var style=new StyleFile{Name=name,Author="WinDotra",Description=description};for(int i=0;i<pairs.Length;i+=2)style.Settings[(string)pairs[i]]=pairs[i+1];return style;}
  // Everything a full look sets, so switching styles never leaves pieces of the previous one behind.
  static object[] Full(params object[] pairs){
   var all=new Dictionary<string,object>{{"Clock",true},{"Stats",false},{"Note",false},{"Focus",false},{"Calendar",false},{"Music",false},{"Reminders",false},{"Day",false},{"Island",true},{"MenuBar",false},{"Dock",false},{"IslandShape","Pill"},{"IslandColor","#000000"},{"IslandIdle","Clock"},{"IslandScale",1.0},{"IslandAura",false},{"SceneColor1",""},{"SceneColor2",""},{"SceneColor3",""},{"SceneBackground",""},{"WallpaperFit","Fill"},{"DockTiles",false},{"Glass",false},{"Opacity",1.0},{"Font","Inter"},{"SceneSpeed",1.0},{"SceneDensity",1.0},{"Layout","keep"}};
   for(int i=0;i<pairs.Length;i+=2)all[(string)pairs[i]]=pairs[i+1];
   return all.SelectMany(p=>new[]{(object)p.Key,p.Value}).ToArray();
  }
  public static List<StyleFile> BuiltIn() {
   return new List<StyleFile>{
    Make("Apple","macOS Sonoma on Windows: pastel hills, menu bar, Dock, notch island, white widgets on the side.",Full("Background","#FFFFFF","Accent","#007AFF","Radius",22.0,"Glass",true,"ClockAnalog",true,"Scene","Hills","SceneDark",false,"SceneColor1","#B49BFF","SceneColor2","#FF8FB1","SceneColor3","#58B96E","SceneBackground","#FFD9E4","Calendar",true,"Reminders",true,"Music",true,"Note",true,"MenuBar",true,"MenuBarLight",true,"Dock",true,"DockLight",true,"DockMagnify",true,"DockSize",52.0,"DockTiles",true,"IslandShape","Notch","IslandIdle","Nothing","Layout","macos")),
    Make("Apple Dark","macOS in dark mode: midnight hills, dark menu bar and Dock, graphite widgets.",Full("Background","#1C1C1E","Accent","#0A84FF","Radius",22.0,"Glass",true,"ClockAnalog",true,"Scene","Hills","SceneDark",true,"SceneColor1","#3A4C8C","SceneColor2","#6B3F8F","SceneColor3","#22475E","SceneBackground","#070A14","Calendar",true,"Reminders",true,"Music",true,"MenuBar",true,"MenuBarLight",false,"Dock",true,"DockLight",false,"DockMagnify",true,"DockSize",52.0,"DockTiles",true,"IslandShape","Notch","IslandIdle","Nothing","Layout","macos")),
    Make("Windows 11","Fluent: Mica-dark tiles in a column, rounded island, digital clock, system stats, calm blue gradient.",Full("Font","Segoe UI Variable Text","Background","#202020","Accent","#60CDFF","Radius",8.0,"ClockAnalog",false,"Scene","Gradient","SceneDark",true,"SceneColor1","#0F2A44","SceneColor2","#1B1B3A","SceneColor3","#0B3B3C","Stats",true,"Calendar",true,"IslandShape","Rounded","IslandColor","#E6202020","Layout","windows")),
    Make("Midnight Neon","Deep black, hot pink, a starfield, a glowing island, and a dark Dock.",Full("Background","#0B0B12","Accent","#FF2E88","Radius",20.0,"Opacity",0.95,"Glass",true,"ClockAnalog",false,"IslandAura",true,"IslandColor","#0B0B12","Scene","Stars","SceneDark",true,"SceneSpeed",1.4,"Music",true,"Stats",true,"Dock",true,"DockLight",false,"DockSize",46.0,"Layout","column")),
    Make("Paper","Warm paper tiles, rust accent, quiet lines, a light Dock, and a soft rounded island.",Full("Background","#FAF7F0","Accent","#C2410C","Radius",18.0,"ClockAnalog",true,"Scene","Lines","SceneDark",false,"SceneBackground","#F4EFE4","Note",true,"Reminders",true,"Dock",true,"DockLight",true,"DockMagnify",false,"DockSize",44.0,"IslandShape","Rounded","IslandColor","#2B2622","Layout","column")),
    Make("Rainmeter","Just the big F R I D A Y date on a dark gradient. No tiles, no Dock, a tiny island.",Full("Background","#000000","Accent","#FFFFFF","Radius",14.0,"Opacity",0.7,"Glass",true,"Clock",false,"Day",true,"Scene","Gradient","SceneDark",true,"SceneColor1","#0E2A3A","SceneColor2","#1A1F3A","SceneColor3","#2A1638","IslandScale",0.85,"IslandIdle","Nothing","Layout","clear"))};
  }
  public static string Folder(string data){return System.IO.Path.Combine(data,"styles");}
  // Built-in styles, styles shipped next to the app (community styles folder), and ones you imported.
  public static List<StyleFile> All(string data) {
   var list=BuiltIn();
   foreach(string dir in new[]{System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"styles"),Folder(data)}) {
    if(!Directory.Exists(dir))continue;
    foreach(string file in Directory.GetFiles(dir,"*"+Extension)){try{var style=Read(file);style.Path=file;if(!list.Any(s=>s.Name==style.Name))list.Add(style);}catch(Exception){}}
   }
   return list;
  }
  public static StyleFile Read(string file) {
   if(new FileInfo(file).Length>64*1024)throw new InvalidDataException("That style file is too large.");
   var style=new JavaScriptSerializer().Deserialize<StyleFile>(File.ReadAllText(file));
   if(style==null||style.Format!="windotra-style"||style.Version!=1||String.IsNullOrWhiteSpace(style.Name)||style.Name.Length>60||style.Settings==null)throw new InvalidDataException("That isn't a WinDotra style file.");
   if(style.Settings.Keys.Any(k=>!Allowed.Contains(k)&&k!="Layout"))throw new InvalidDataException("That style tries to change something styles can't change.");
   object layout;if(style.Settings.TryGetValue("Layout",out layout)&&!Layouts.Contains(Convert.ToString(layout)))throw new InvalidDataException("That style uses an unknown widget layout.");
   return style;
  }
  // Applies a style to a copy of the profile; the change only sticks if the result validates.
  public static DesktopProfile Apply(DesktopProfile current,StyleFile style) {
   var copy=new JavaScriptSerializer().Deserialize<DesktopProfile>(new JavaScriptSerializer().Serialize(current));
   foreach(var item in style.Settings) {
    if(!Allowed.Contains(item.Key))continue;var field=typeof(DesktopProfile).GetField(item.Key,BindingFlags.Public|BindingFlags.Instance);if(field==null||item.Value==null)continue;
    field.SetValue(copy,Convert.ChangeType(item.Value,field.FieldType,System.Globalization.CultureInfo.InvariantCulture));
   }
   object layout;if(style.Settings.TryGetValue("Layout",out layout))Arrange(copy,Convert.ToString(layout));
   if(copy.Dock&&copy.DockApps.Count==0)copy.DockApps=OverlayManager.DockDefaults();
   copy.Validate();return copy;
  }
  // Named widget arrangements, measured from the main screen's work area.
  public static void Arrange(DesktopProfile p,string layout) {
   if(layout==null||layout=="keep")return;
   var keep=p.Positions.ContainsKey("island")?p.Positions["island"]:null;p.Positions.Clear();if(keep!=null)p.Positions["island"]=keep;p.Looks.Clear();
   if(layout=="clear")return;
   var area=System.Windows.SystemParameters.WorkArea;double unit=Math.Round(p.WidgetWidth*0.62),small=unit+32,medium=unit*2+12+32,tall=unit+34,top=area.Top+(p.MenuBar?44:16),left=area.Left+8,right=area.Right-8;
   Action<string,double,double> at=(id,x,y)=>p.Positions[id]=new Position{X=Math.Round(x),Y=Math.Round(y)};
   if(layout=="macos"){
    // Like the Sonoma desktop: media and reminders down the left, calendar and clock in the top right.
    at("music",left,top);at("reminders",left,top+tall);at("notes",left,top+tall*2);at("stats",left,top+tall*3);
    at("calendar",right-medium,top);at("clock",right-small,top+tall);at("focus",right-small*2,top+tall);
   } else if(layout=="windows"){
    at("clock",left,top);at("stats",left,top+tall);at("calendar",left,top+tall*2);at("music",left,top+tall*3);at("notes",left+medium,top);at("reminders",left+medium,top+tall);at("focus",left+small,top);
   } else {
    double x=right-medium;at("clock",x,top);at("music",x,top+tall);at("stats",x,top+tall*2);at("notes",x,top+tall*3);at("reminders",x-medium,top);at("calendar",x-medium,top+tall);at("focus",x+medium-small*2,top);
   }
  }
  // Captures the current look as a style file.
  public static StyleFile Capture(DesktopProfile profile,string name,string author,string description) {
   var style=new StyleFile{Name=name,Author=author,Description=description};
   foreach(string key in Allowed){var field=typeof(DesktopProfile).GetField(key);if(field!=null)style.Settings[key]=field.GetValue(profile);}
   return style;
  }
  public static void Save(StyleFile style,string file){File.WriteAllText(file,new JavaScriptSerializer().Serialize(style));}
 }
}
