using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WinCanvas {
 public partial class Studio {
  [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern IntPtr LoadImage(IntPtr instance,string name,uint type,int width,int height,uint flags);
  [DllImport("user32.dll",EntryPoint="LoadImageW")] static extern IntPtr LoadSystemImage(IntPtr instance,IntPtr id,uint type,int width,int height,uint flags);
  [DllImport("user32.dll")] static extern bool DrawIconEx(IntPtr dc,int x,int y,IntPtr icon,int width,int height,uint step,IntPtr brush,uint flags);
  [DllImport("user32.dll")] static extern bool DestroyCursor(IntPtr cursor);
  static readonly Dictionary<string,string> RoleNames=new Dictionary<string,string>{{"Arrow","Normal"},{"Hand","Link"},{"IBeam","Text"},{"Wait","Busy"},{"AppStarting","Working"},{"Crosshair","Precision"},{"Help","Help"},{"No","Unavailable"},{"SizeAll","Move"},{"SizeNS","Resize ↕"},{"SizeWE","Resize ↔"},{"SizeNWSE","Resize ⤡"},{"SizeNESW","Resize ⤢"},{"UpArrow","Alternate"}};
  static readonly Dictionary<string,int> SystemIds=new Dictionary<string,int>{{"Arrow",32512},{"IBeam",32513},{"Wait",32514},{"Crosshair",32515},{"UpArrow",32516},{"SizeNWSE",32642},{"SizeNESW",32643},{"SizeWE",32644},{"SizeNS",32645},{"SizeAll",32646},{"No",32648},{"Hand",32649},{"AppStarting",32650},{"Help",32651}};
  // Draws a cursor (first frame for .ani) into a small image for the preview tiles.
  static ImageSource CursorImage(string file,string role) {
   IntPtr handle=IntPtr.Zero;bool shared=false;
   try {
    if(file!=null&&File.Exists(file))handle=LoadImage(IntPtr.Zero,file,2,48,48,0x10);
    if(handle==IntPtr.Zero){int id;if(SystemIds.TryGetValue(role,out id)){handle=LoadSystemImage(IntPtr.Zero,new IntPtr(id),2,48,48,0x8000);shared=true;}}
    if(handle==IntPtr.Zero)return null;
    using(var bitmap=new System.Drawing.Bitmap(48,48,System.Drawing.Imaging.PixelFormat.Format32bppArgb)){using(var g=System.Drawing.Graphics.FromImage(bitmap)){g.Clear(System.Drawing.Color.Transparent);var dc=g.GetHdc();DrawIconEx(dc,0,0,handle,48,48,0,IntPtr.Zero,3);g.ReleaseHdc(dc);}
     var hbitmap=bitmap.GetHbitmap(System.Drawing.Color.FromArgb(0,0,0,0));try{var source=System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(hbitmap,IntPtr.Zero,Int32Rect.Empty,BitmapSizeOptions.FromEmptyOptions());source.Freeze();return source;}finally{DeleteObject(hbitmap);}}
   } catch(Exception) { return null; } finally { if(handle!=IntPtr.Zero&&!shared)DestroyCursor(handle); }
  }
  [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr obj);
  static string CurrentCursor(string role){try{using(var key=Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Control Panel\Cursors")){var value=key==null?null:key.GetValue(role) as string;return String.IsNullOrEmpty(value)?null:Environment.ExpandEnvironmentVariables(value);}}catch(Exception){return null;}}
  // Built-in Windows schemes, built from the files in C:\Windows\Cursors.
  static readonly string[][] Schemes={new[]{"Windows","aero",""},new[]{"Windows Large","aero","_l"},new[]{"Windows Extra Large","aero","_xl"},new[]{"Black","classic","_r"},new[]{"Black Large","classic","_rl"},new[]{"Classic","classic","_i"},new[]{"Classic Large","classic","_l"}};
  static Dictionary<string,string> Scheme(string family,string suffix) {
   string dir=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"Cursors");var map=new Dictionary<string,string>();
   Action<string,string> put=(role,file)=>{string path=Path.Combine(dir,file);if(File.Exists(path))map[role]=path;};
   if(family=="aero") {
    put("Arrow","aero_arrow"+suffix+".cur");put("Hand","aero_link"+suffix+".cur");put("Help","aero_helpsel"+suffix+".cur");put("AppStarting","aero_working"+suffix+".ani");put("Wait","aero_busy"+suffix+".ani");put("No","aero_unavail"+suffix+".cur");
    put("SizeNS","aero_ns"+suffix+".cur");put("SizeWE","aero_ew"+suffix+".cur");put("SizeNWSE","aero_nwse"+suffix+".cur");put("SizeNESW","aero_nesw"+suffix+".cur");put("SizeAll","aero_move"+suffix+".cur");put("UpArrow","aero_up"+suffix+".cur");
    string text=suffix==""?"_r":suffix=="_l"?"_rl":"_rm";put("IBeam","beam"+text+".cur");put("Crosshair","cross"+text+".cur");
   } else {
    put("Arrow","arrow"+suffix+".cur");put("Help","help"+suffix+".cur");put("AppStarting","wait"+suffix+".cur");put("Wait","busy"+suffix+".cur");put("Crosshair","cross"+suffix+".cur");put("IBeam","beam"+suffix+".cur");put("No","no"+suffix+".cur");
    put("SizeNS","size4"+suffix+".cur");put("SizeWE","size3"+suffix+".cur");put("SizeNWSE","size2"+suffix+".cur");put("SizeNESW","size1"+suffix+".cur");put("SizeAll","move"+suffix+".cur");put("UpArrow","up"+suffix+".cur");
    put("Hand","aero_link"+(suffix=="_i"?"_i":suffix.Contains("l")?"_l":"")+".cur");
   }
   return map;
  }
  // Maps a cursor pack's files to roles: install.inf when present, otherwise file names.
  public static Dictionary<string,string> ReadPack(string folder) {
   var files=Directory.GetFiles(folder,"*.*",SearchOption.AllDirectories).Where(f=>f.EndsWith(".cur",StringComparison.OrdinalIgnoreCase)||f.EndsWith(".ani",StringComparison.OrdinalIgnoreCase)).OrderBy(f=>Path.GetFileName(f).StartsWith("!")?1:0).ThenBy(f=>f,StringComparer.OrdinalIgnoreCase).ToList();var map=new Dictionary<string,string>();
   var inf=Directory.GetFiles(folder,"*.inf",SearchOption.AllDirectories).FirstOrDefault();
   var infKeys=new Dictionary<string,string>{{"pointer","Arrow"},{"help","Help"},{"work","AppStarting"},{"busy","Wait"},{"cross","Crosshair"},{"text","IBeam"},{"unavailable","No"},{"vert","SizeNS"},{"horz","SizeWE"},{"dgn1","SizeNWSE"},{"dgn2","SizeNESW"},{"move","SizeAll"},{"alternate","UpArrow"},{"link","Hand"}};
   if(inf!=null){bool strings=false;foreach(string raw in File.ReadAllLines(inf)){string line=raw.Trim();if(line.StartsWith("[")){strings=line.Equals("[Strings]",StringComparison.OrdinalIgnoreCase);continue;}if(!strings||!line.Contains("="))continue;string key=line.Substring(0,line.IndexOf('=')).Trim().ToLowerInvariant(),value=line.Substring(line.IndexOf('=')+1).Trim().Trim('"');string role;if(!infKeys.TryGetValue(key,out role))continue;var file=files.FirstOrDefault(f=>String.Equals(Path.GetFileName(f),value,StringComparison.OrdinalIgnoreCase));if(file!=null)map[role]=file;}}
   var hints=new[]{new[]{"Arrow","normal","pointer","arrow","select"},new[]{"Hand","link","hand"},new[]{"IBeam","text","beam","ibeam"},new[]{"Wait","busy","wait"},new[]{"AppStarting","working","work","background","appstarting"},new[]{"Crosshair","precision","cross"},new[]{"Help","help"},new[]{"No","unavailable","unavail","no"},new[]{"SizeAll","move"},new[]{"SizeNS","vert","ns","vertical"},new[]{"SizeWE","horz","we","ew","horizontal"},new[]{"SizeNWSE","dgn1","nwse","diagonal1","diagonal resize 1"},new[]{"SizeNESW","dgn2","nesw","diagonal2","diagonal resize 2"},new[]{"UpArrow","alternate","up"}};
   foreach(var hint in hints){if(map.ContainsKey(hint[0]))continue;var file=files.FirstOrDefault(f=>{string name=Path.GetFileNameWithoutExtension(f).ToLowerInvariant();return hint.Skip(1).Any(h=>name==h||name.EndsWith("_"+h)||name.EndsWith(" "+h)||name.StartsWith(h+"_")||(h.Length>3&&name.Contains(h)));});if(file!=null&&!map.ContainsValue(file))map[hint[0]]=file;}
   return map;
  }
  void ImportPack(string folder){var map=ReadPack(folder);if(map.Count==0)throw new InvalidOperationException("No .cur or .ani files found there.");foreach(var item in map){NativeCustomization.ValidateCursor(item.Value);cursors[item.Key]=item.Value;}Page(current);Report("Imported "+map.Count+" of 14 pointers. Check the previews, then Apply.");}
  void Cursors() {
   Header("Cursors","Pick a built-in scheme, import a cursor pack, or set each pointer yourself. Nothing changes until you click Apply.");
   var schemes=Card("Schemes","Built into Windows, plus macOS in black and white (apple_cursor by ful1e5, GPL-3.0, ships with WinDotra).");var chips=new WrapPanel();
   foreach(var mac in new[]{new object[]{"macOS",false},new object[]{"macOS White (for dark mode)",true}}){bool white=(bool)mac[1];string label=(string)mac[0];if(Directory.Exists(MacCursors(white)))chips.Children.Add(Chip(label,false,()=>{cursors.Clear();foreach(var item in ReadPack(MacCursors(white)))cursors[item.Key]=item.Value;Page(current);Report(label+" cursors selected. Click Apply cursors to use them.");}));}
   foreach(var scheme in Schemes){var s=scheme;chips.Children.Add(Chip(s[0],false,()=>{cursors.Clear();foreach(var item in Scheme(s[1],s[2]))cursors[item.Key]=item.Value;Page(current);Report(s[0]+" selected. Click Apply cursors to use it.");}));}
   schemes.Children.Add(chips);
   var import=Card("Import a cursor pack","Packs from the web usually come as a .zip or a folder with an install.inf. WinDotra maps the files to the right pointers.");var buttons=new WrapPanel();
   buttons.Children.Add(Button("Import .zip…",()=>{var dialog=new Microsoft.Win32.OpenFileDialog{Filter="Cursor pack|*.zip"};if(dialog.ShowDialog(this)!=true)return;if(new FileInfo(dialog.FileName).Length>100*1024*1024)throw new InvalidOperationException("That .zip is larger than 100 MB.");string target=Path.Combine(folder,"cursor-packs",Path.GetFileNameWithoutExtension(dialog.FileName)+"-"+DateTime.Now.ToString("yyyyMMddHHmmss"));Directory.CreateDirectory(target);
    using(var zip=System.IO.Compression.ZipFile.OpenRead(dialog.FileName)){foreach(var entry in zip.Entries){string name=entry.FullName.Replace('/',Path.DirectorySeparatorChar);if(entry.Length>20*1024*1024||String.IsNullOrEmpty(entry.Name))continue;string ext=Path.GetExtension(name).ToLowerInvariant();if(ext!=".cur"&&ext!=".ani"&&ext!=".inf")continue;string path=Path.GetFullPath(Path.Combine(target,name));if(!path.StartsWith(Path.GetFullPath(target),StringComparison.OrdinalIgnoreCase))continue;Directory.CreateDirectory(Path.GetDirectoryName(path));System.IO.Compression.ZipFileExtensions.ExtractToFile(entry,path,true);}}
    ImportPack(target);}));
   buttons.Children.Add(Button("Import folder…",()=>{using(var dialog=new System.Windows.Forms.FolderBrowserDialog{Description="Choose the cursor pack folder"}){if(dialog.ShowDialog()==System.Windows.Forms.DialogResult.OK)ImportPack(dialog.SelectedPath);}}));
   import.Children.Add(buttons);
   var set=Card("Your pointers",cursors.Count==0?"Showing your current pointers. Click any tile to choose a file for it.":cursors.Count+" of 14 pointers will change. Click a tile to change it, right-click to keep the current one.");
   var grid=new System.Windows.Controls.Primitives.UniformGrid{Columns=7,Margin=new Thickness(0,4,-8,0)};
   foreach(string role in NativeCustomization.Roles) {
    string key=role;bool chosen=cursors.ContainsKey(role);string file=chosen?cursors[role]:CurrentCursor(role);
    var tile=new StackPanel{HorizontalAlignment=HorizontalAlignment.Center};tile.Children.Add(new Image{Source=CursorImage(file,role),Width=40,Height=40,Margin=new Thickness(0,4,0,6)});tile.Children.Add(new TextBlock{Text=RoleNames[role],FontSize=11,HorizontalAlignment=HorizontalAlignment.Center,Foreground=Brush(chosen?Ui.Accent:Ui.Soft),FontWeight=chosen?FontWeights.SemiBold:FontWeights.Normal});
    var button=new Button{Content=tile,Padding=new Thickness(4,6,4,6),Margin=new Thickness(0,0,8,8),ToolTip=(file??"Windows default")+"\nClick to choose a file · right-click to keep current"};if(chosen){button.BorderBrush=Brush(Ui.Accent);button.BorderThickness=new Thickness(1.5);}
    button.Click+=(s,e)=>Safe(()=>{var dialog=new Microsoft.Win32.OpenFileDialog{Filter="Windows cursors|*.cur;*.ani",Title="Choose the "+RoleNames[key]+" pointer"};if(dialog.ShowDialog(this)==true){NativeCustomization.ValidateCursor(dialog.FileName);cursors[key]=dialog.FileName;Page(current);}});
    button.MouseRightButtonUp+=(s,e)=>{cursors.Remove(key);Page(current);};grid.Children.Add(button);
   }
   set.Children.Add(grid);
   var actions=new WrapPanel{Margin=new Thickness(0,8,0,0)};actions.Children.Add(Primary("Apply cursors",()=>{if(cursors.Count==0)throw new InvalidOperationException("Pick a scheme, import a pack, or choose at least one pointer first.");customization.ApplyCursors(cursors);Report("Cursors applied. Restore is available here.");cursors.Clear();Page(current);}));
   var undo=Button("Restore previous cursors",()=>{customization.UndoCursors();Report("Previous cursors restored.");Page(current);});undo.IsEnabled=customization.CursorUndo;actions.Children.Add(undo);
   if(cursors.Count>0)actions.Children.Add(Ghost("Clear choices",()=>{cursors.Clear();Page(current);}));
   actions.Children.Add(Button("Pointer size & color ↗",()=>System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("ms-settings:easeofaccess-cursorandpointersize"){UseShellExecute=true})));set.Children.Add(actions);
  }
 }
}
