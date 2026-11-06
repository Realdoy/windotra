using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WinCanvas {
 // One button in the island's quick-launch row: an app, file, folder, website, or Windows link (ms-settings: ...).
 public class LaunchItem {
  public string Name="",Target="",IconFile="";
  public static List<LaunchItem> Defaults(){return new List<LaunchItem>{new LaunchItem{Name="Files",Target="explorer.exe"},new LaunchItem{Name="Calculator",Target="calc.exe"},new LaunchItem{Name="Notepad",Target="notepad.exe"},new LaunchItem{Name="Snip",Target="ms-screenclip:"},new LaunchItem{Name="Settings",Target="ms-settings:"}};}
  public void Open(){Process.Start(new ProcessStartInfo(Target){UseShellExecute=true});}
  [System.Web.Script.Serialization.ScriptIgnore]
  public bool IsWeb {get{return Target.StartsWith("http://",StringComparison.OrdinalIgnoreCase)||Target.StartsWith("https://",StringComparison.OrdinalIgnoreCase);}}
  // A fallback glyph for things without a file icon.
  [System.Web.Script.Serialization.ScriptIgnore]
  public string Glyph {get{return IsWeb?"":Target.StartsWith("ms-settings",StringComparison.OrdinalIgnoreCase)?"":Target.StartsWith("ms-screenclip",StringComparison.OrdinalIgnoreCase)?"":Target.EndsWith(":")?"":"";}}
  static readonly Dictionary<string,ImageSource> cache=new Dictionary<string,ImageSource>(StringComparer.OrdinalIgnoreCase);
  // The real app icon for exe/lnk/folder/file targets, or null to use the glyph.
  [System.Web.Script.Serialization.ScriptIgnore]
  public ImageSource Icon {get{
   ImageSource icon;if(cache.TryGetValue(Target,out icon))return icon;icon=null;
   try {
    string path=Target;if(!Path.IsPathRooted(path)){foreach(string dir in new[]{Environment.SystemDirectory,Environment.GetFolderPath(Environment.SpecialFolder.Windows)}){string candidate=Path.Combine(dir,path);if(File.Exists(candidate)){path=candidate;break;}}}
    if(File.Exists(path)){using(var raw=System.Drawing.Icon.ExtractAssociatedIcon(path)){var source=Imaging.CreateBitmapSourceFromHIcon(raw.Handle,Int32Rect.Empty,BitmapSizeOptions.FromWidthAndHeight(32,32));source.Freeze();icon=source;}}
   } catch(Exception) { icon=null; }
   cache[Target]=icon;return icon;}}
  public static void Validate(List<LaunchItem> items){if(items==null||items.Count>12)throw new InvalidDataException("Up to 12 island buttons.");foreach(var i in items)if(i==null||String.IsNullOrWhiteSpace(i.Target)||i.Target.Length>1024||i.Name==null||i.Name.Length>60)throw new InvalidDataException("Invalid island button.");}
 }
}
