using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace WinCanvas {
 // Crisp, large icons for files, apps, shortcuts, and folders (the 32 px icons Windows hands out look blurry in a Dock).
 public static class ShellIcon {
  [StructLayout(LayoutKind.Sequential)] struct Size32 { public int Width,Height; }
  [StructLayout(LayoutKind.Sequential)] struct Bitmap32 { public int Type,Width,Height,WidthBytes; public short Planes,BitsPixel; public IntPtr Bits; }
  [StructLayout(LayoutKind.Sequential)] struct DibSection { public Bitmap32 Bm; public int Size,Width,Height; public short Planes,BitCount; public int Compression,SizeImage,XPels,YPels,Used,Important,F0,F1,F2; public IntPtr Section; public int Offset; }
  [ComImport,Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
  interface IShellItemImageFactory { [PreserveSig] int GetImage(Size32 size,int flags,out IntPtr bitmap); }
  [DllImport("shell32.dll",CharSet=CharSet.Unicode)] static extern int SHCreateItemFromParsingName(string path,IntPtr bind,[In] ref Guid iid,[MarshalAs(UnmanagedType.Interface)] out IShellItemImageFactory item);
  [DllImport("gdi32.dll")] static extern int GetObject(IntPtr handle,int size,ref DibSection value);
  [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr handle);
  static readonly Dictionary<string,ImageSource> cache=new Dictionary<string,ImageSource>(StringComparer.OrdinalIgnoreCase);
  public static ImageSource Get(string path,int size) {
   string key=path+"|"+size;ImageSource result;if(cache.TryGetValue(key,out result))return result;result=null;
   try {
    var iid=typeof(IShellItemImageFactory).GUID;IShellItemImageFactory factory;
    if(SHCreateItemFromParsingName(path,IntPtr.Zero,ref iid,out factory)>=0&&factory!=null) {
     IntPtr bitmap;
     if(factory.GetImage(new Size32{Width=size,Height=size},0x1|0x4,out bitmap)>=0&&bitmap!=IntPtr.Zero) {
      try {
       var dib=new DibSection();GetObject(bitmap,Marshal.SizeOf(typeof(DibSection)),ref dib);
       int w=dib.Bm.Width,h=dib.Bm.Height,stride=dib.Bm.WidthBytes;
       if(dib.Bm.Bits!=IntPtr.Zero&&dib.Bm.BitsPixel==32&&w>0&&h>0) {
        var pixels=new byte[stride*h];Marshal.Copy(dib.Bm.Bits,pixels,0,pixels.Length);
        if(dib.Height>0){var flipped=new byte[pixels.Length];for(int y=0;y<h;y++)Buffer.BlockCopy(pixels,y*stride,flipped,(h-1-y)*stride,stride);pixels=flipped;}
        var source=BitmapSource.Create(w,h,96,96,PixelFormats.Pbgra32,null,pixels,stride);source.Freeze();result=source;
       }
      } finally { DeleteObject(bitmap); }
     }
     Marshal.ReleaseComObject(factory);
    }
   } catch(Exception) { result=null; }
   cache[key]=result;return result;
  }
 }

 // Finds the real program behind a target (shortcut, alias, or exe) so the Dock can tell whether it's running.
 static class AppTarget {
  static readonly Dictionary<string,string> cache=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
  public static string Exe(string target) {
   string result;if(cache.TryGetValue(target,out result))return result;result=null;
   try {
    string path=target;
    if(path.EndsWith(".lnk",StringComparison.OrdinalIgnoreCase)&&File.Exists(path)){var shell=Type.GetTypeFromProgID("WScript.Shell");object instance=Activator.CreateInstance(shell);object link=shell.InvokeMember("CreateShortcut",System.Reflection.BindingFlags.InvokeMethod,null,instance,new object[]{path});path=(string)link.GetType().InvokeMember("TargetPath",System.Reflection.BindingFlags.GetProperty,null,link,null);Marshal.FinalReleaseComObject(link);Marshal.FinalReleaseComObject(instance);}
    if(!String.IsNullOrEmpty(path)&&path.EndsWith(".exe",StringComparison.OrdinalIgnoreCase)){if(!System.IO.Path.IsPathRooted(path)){string full=System.IO.Path.Combine(Environment.SystemDirectory,path);if(!File.Exists(full))full=System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),path);path=full;}result=System.IO.Path.GetFileName(path).ToLowerInvariant();}
   } catch(Exception) { result=null; }
   cache[target]=result;return result;
  }
 }

 // Top-level windows like the ones in Alt+Tab, with the program that owns each one.
 static class AppWindows {
  delegate bool EnumProc(IntPtr window,IntPtr data);
  [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc proc,IntPtr data);
  [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr window);
  [DllImport("user32.dll")] static extern IntPtr GetWindow(IntPtr window,uint command);
  [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr window,int index);
  [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetWindowText(IntPtr window,StringBuilder text,int max);
  [DllImport("user32.dll")] static extern int GetWindowTextLength(IntPtr window);
  [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window,out uint process);
  [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr window,int attribute,out int value,int size);
  [DllImport("kernel32.dll")] static extern IntPtr OpenProcess(uint access,bool inherit,uint id);
  [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);
  [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] static extern bool QueryFullProcessImageName(IntPtr process,int flags,StringBuilder name,ref int size);
  [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr window);
  [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr window,int command);
  [DllImport("user32.dll")] static extern bool IsIconic(IntPtr window);
  [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll")] static extern bool PostMessage(IntPtr window,uint message,IntPtr w,IntPtr l);
  [DllImport("user32.dll")] static extern bool IsZoomed(IntPtr window);
  public class Item { public IntPtr Handle; public string Path,Exe,Title; public uint Pid; }
  static readonly Dictionary<uint,string> paths=new Dictionary<uint,string>();
  public static string PathOf(uint pid) {
   string path;if(paths.TryGetValue(pid,out path))return path;path=null;
   IntPtr process=OpenProcess(0x1000,false,pid);if(process!=IntPtr.Zero){try{var name=new StringBuilder(1024);int size=name.Capacity;if(QueryFullProcessImageName(process,0,name,ref size))path=name.ToString();}finally{CloseHandle(process);}}
   if(paths.Count>500)paths.Clear();paths[pid]=path;return path;
  }
  public static string ExeOf(IntPtr window){uint pid;GetWindowThreadProcessId(window,out pid);var path=PathOf(pid);return path==null?null:System.IO.Path.GetFileName(path).ToLowerInvariant();}
  public static List<Item> List() {
   var list=new List<Item>();uint self=(uint)Process.GetCurrentProcess().Id;
   EnumWindows((h,d)=>{
    if(!IsWindowVisible(h)||GetWindow(h,4)!=IntPtr.Zero)return true;int ex=GetWindowLong(h,-20);if((ex&0x80)!=0&&(ex&0x40000)==0)return true;
    int cloaked;if(DwmGetWindowAttribute(h,14,out cloaked,4)==0&&cloaked!=0)return true;int length=GetWindowTextLength(h);if(length==0)return true;
    uint pid;GetWindowThreadProcessId(h,out pid);if(pid==self)return true;string path=PathOf(pid);if(path==null)return true;
    var title=new StringBuilder(length+1);GetWindowText(h,title,title.Capacity);
    list.Add(new Item{Handle=h,Pid=pid,Path=path,Exe=System.IO.Path.GetFileName(path).ToLowerInvariant(),Title=title.ToString()});return true;},IntPtr.Zero);
   return list;
  }
  public static void Activate(IntPtr window){if(IsIconic(window))ShowWindow(window,9);SetForegroundWindow(window);}
  public static void Minimize(IntPtr window){ShowWindow(window,6);}
  public static void Zoom(IntPtr window){ShowWindow(window,IsZoomed(window)?9:3);}
  public static void Close(IntPtr window){PostMessage(window,0x0010,IntPtr.Zero,IntPtr.Zero);}
 }

 // Keyboard shortcuts the menu bar uses to open Windows' own panels (search, quick settings, notifications).
 static class Keys {
  [DllImport("user32.dll")] static extern void keybd_event(byte key,byte scan,uint flags,UIntPtr extra);
  public static void Win(byte key){keybd_event(0x5B,0,0,UIntPtr.Zero);keybd_event(key,0,0,UIntPtr.Zero);keybd_event(key,0,2,UIntPtr.Zero);keybd_event(0x5B,0,2,UIntPtr.Zero);}
 }

 // The macOS-style menu bar and Dock. Both are optional; the menu bar reserves its strip so maximized windows stay below it.
 public partial class OverlayManager {
  [StructLayout(LayoutKind.Sequential)] struct AppBarData { public int Size; public IntPtr Window; public uint Callback,Edge; public LiveWallpaper.Rect32 Rect; public IntPtr Param; }
  [DllImport("shell32.dll")] static extern IntPtr SHAppBarMessage(uint message,ref AppBarData data);
  [DllImport("user32.dll")] static extern bool LockWorkStation();
  [DllImport("user32.dll")] static extern bool GetCursorPos(out System.Drawing.Point point);
  Window menuBar,dockWindow; IntPtr menuBarHandle; IntPtr lastApp; readonly DispatcherTimer desktopTimer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(120)};
  public Action ShowStudio; public Window MenuBarWindow{get{return menuBar;}} public Window DockWindow{get{return dockWindow;}}
  public static double MenuBarHeight=30;
  static ImageSource Logo(){try{return Brand.Logo;}catch(Exception){return null;}}
  bool MenuLight{get{return Profile.MenuBarLight;}}

  // Primary screen in device-independent pixels.
  static Rect PrimaryDip(Visual visual,bool work) {
   var screen=System.Windows.Forms.Screen.PrimaryScreen;var r=work?screen.WorkingArea:screen.Bounds;var source=visual==null?null:PresentationSource.FromVisual(visual);
   if(source==null){double k=96.0/NativeCustomization.Dpi();return new Rect(r.Left*k,r.Top*k,r.Width*k,r.Height*k);}
   var m=source.CompositionTarget.TransformFromDevice;var a=m.Transform(new Point(r.Left,r.Top));var b=m.Transform(new Point(r.Right,r.Bottom));return new Rect(a,b);
  }

  void StartMacDesktop() {
   if(Profile.MenuBar)MenuBar();if(Profile.Dock)DockBar();
   hiddenForFullScreen=false;if(menuBar!=null||dockWindow!=null||islandWindow!=null){desktopTimer.Tick-=DesktopTick;desktopTimer.Tick+=DesktopTick;desktopTimer.Start();}
   // The island sits above the menu bar, like the notch.
   if(islandWindow!=null){islandWindow.Topmost=false;islandWindow.Topmost=true;}
  }
  void StopMacDesktop() {
   desktopTimer.Stop();CloseControlCenter();
   if(menuBar!=null){RemoveAppBar();menuBar.Close();menuBar=null;}
   if(dockWindow!=null){dockWindow.Close();dockWindow=null;}
  }
  int desktopTicks;Action updateMenuBar,updateDock,dockHover;
  bool hiddenForFullScreen;
  // The notch hangs from the very top edge. Windows nudges windows out of the menu bar's reserved strip, so put it back.
  void KeepNotch(){if(islandWindow==null||Profile.IslandShape!="Notch"||Editing||hiddenForFullScreen)return;double top=PrimaryDip(islandWindow,false).Top-18*Profile.IslandScale;if(Math.Abs(islandWindow.Top-top)>0.5&&islandWindow.Top<top+60)islandWindow.Top=top;}
  public Window ControlCenterWindow{get{return controlCenter;}}
  void FullScreenCheck(){var fs=SystemControls.FullScreenOn();bool hide=fs!=null&&fs.Primary;if(hide==hiddenForFullScreen)return;hiddenForFullScreen=hide;
   foreach(var w in new[]{menuBar,dockWindow,islandWindow,controlCenter}){if(w==null)continue;if(hide)w.Hide();else{w.Show();}}}
  void DesktopTick(object s,EventArgs e){desktopTicks++;if(desktopTicks%4==0){FullScreenCheck();KeepNotch();}if(hiddenForFullScreen)return;if(dockHover!=null)dockHover();if(desktopTicks%8==0){if(updateMenuBar!=null)updateMenuBar();}if(desktopTicks%12==0&&updateDock!=null)updateDock();}

  // ---------- Menu bar ----------
  Button BarButton(object content,Action click,string tip) {
   var b=new Button{Content=content,Padding=new Thickness(9,0,9,0),Margin=new Thickness(0),Height=MenuBarHeight-6,VerticalAlignment=VerticalAlignment.Center,Background=Brushes.Transparent,Foreground=Brush(MenuLight?"#1D1D1F":"#FFFFFF"),BorderThickness=new Thickness(0),FontSize=13,Cursor=Cursors.Arrow,ToolTip=tip,FocusVisualStyle=null};
   var template=new ControlTemplate(typeof(Button));var border=new FrameworkElementFactory(typeof(Border));border.Name="B";border.SetValue(Border.CornerRadiusProperty,new CornerRadius(5));border.SetValue(Border.BackgroundProperty,new TemplateBindingExtension(Control.BackgroundProperty));border.SetValue(Border.PaddingProperty,new TemplateBindingExtension(Control.PaddingProperty));
   var content2=new FrameworkElementFactory(typeof(ContentPresenter));content2.SetValue(FrameworkElement.VerticalAlignmentProperty,VerticalAlignment.Center);border.AppendChild(content2);template.VisualTree=border;
   var over=new Trigger{Property=UIElement.IsMouseOverProperty,Value=true};over.Setters.Add(new Setter(Border.BackgroundProperty,Brush(MenuLight?"#1F000000":"#33FFFFFF"),"B"));template.Triggers.Add(over);b.Template=template;
   b.Click+=(s,e)=>{try{click();}catch(Exception ex){report(ex.Message);}};return b;
  }
  MenuItem Item(string header,Action action){var item=new MenuItem{Header=header};item.Click+=(s,e)=>{try{action();}catch(Exception ex){report(ex.Message);}};return item;}
  void Drop(Button button,ContextMenu menu){menu.PlacementTarget=button;menu.Placement=System.Windows.Controls.Primitives.PlacementMode.Bottom;menu.VerticalOffset=4;menu.IsOpen=true;}
  static void Open(string target){Process.Start(new ProcessStartInfo(target){UseShellExecute=true});}
  bool Confirm(string text){return MessageBox.Show(text,"WinDotra",MessageBoxButton.OKCancel,MessageBoxImage.Question)==MessageBoxResult.OK;}

  StackPanel menusHost; IntPtr menusFor;
  public Action GoBackToWindows;
  void MenuBar() {
   string ink=MenuLight?"#1D1D1F":"#FFFFFF";
   var bar=new Grid{Height=MenuBarHeight,Background=Brush(MenuLight?"#B8F4F4F7":"#A6161618")};
   bar.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});bar.ColumnDefinitions.Add(new ColumnDefinition());bar.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
   var left=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(8,0,0,0)};bar.Children.Add(left);
   // The system menu (like the Apple menu): about, settings, sleep, restart, shut down, and the way back to plain Windows.
   var logoImage=Logo();var logo=BarButton(logoImage!=null?(object)new Image{Source=logoImage,Width=16,Height=16}:new TextBlock{Text="\uE782",FontFamily=new FontFamily(Ui.Icons),FontSize=14},()=>{},"WinDotra");
   var system=new ContextMenu();
   system.Items.Add(Item("About This PC",()=>Open("ms-settings:about")));system.Items.Add(new Separator());
   system.Items.Add(Item("System Settings…",()=>Open("ms-settings:")));system.Items.Add(Item("WinDotra…",()=>{if(ShowStudio!=null)ShowStudio();}));system.Items.Add(Item("Edit widgets",()=>SetEditing(!Editing)));system.Items.Add(new Separator());
   system.Items.Add(Item("Force Quit…",()=>Open("taskmgr.exe")));system.Items.Add(new Separator());
   system.Items.Add(Item("Sleep",()=>System.Windows.Forms.Application.SetSuspendState(System.Windows.Forms.PowerState.Suspend,false,false)));
   system.Items.Add(Item("Restart…",()=>{if(Confirm("Restart your PC now? Save your work first."))Process.Start(new ProcessStartInfo("shutdown.exe","/r /t 0"){CreateNoWindow=true,UseShellExecute=false});}));
   system.Items.Add(Item("Shut Down…",()=>{if(Confirm("Shut down your PC now? Save your work first."))Process.Start(new ProcessStartInfo("shutdown.exe","/s /t 0"){CreateNoWindow=true,UseShellExecute=false});}));
   system.Items.Add(new Separator());system.Items.Add(Item("Lock Screen",()=>LockWorkStation()));
   system.Items.Add(Item("Sign Out…",()=>{if(Confirm("Sign out of Windows now? Save your work first."))Process.Start(new ProcessStartInfo("shutdown.exe","/l"){CreateNoWindow=true,UseShellExecute=false});}));
   system.Items.Add(new Separator());system.Items.Add(Item("Back to normal Windows",()=>{if(GoBackToWindows!=null)GoBackToWindows();}));
   logo.Click+=(s,e)=>Drop(logo,system);left.Children.Add(logo);
   // The app in front, in bold, then its menus: the app's real menus when it has classic ones, otherwise
   // File, Edit, View, Window, and Help that send the usual shortcuts to it.
   var appName=new TextBlock{Text="Desktop",FontWeight=FontWeights.Bold,FontSize=13,Foreground=Brush(ink)};var app=BarButton(appName,()=>{},null);left.Children.Add(app);
   var appMenu=new ContextMenu();
   appMenu.Items.Add(Item("Show in File Explorer",()=>{var exe=lastApp==IntPtr.Zero?null:AppPath(lastApp);if(exe!=null)Process.Start("explorer.exe","/select,\""+exe+"\"");}));appMenu.Items.Add(new Separator());
   appMenu.Items.Add(Item("Hide",()=>{if(lastApp!=IntPtr.Zero)AppWindows.Minimize(lastApp);}));
   appMenu.Items.Add(Item("Quit",()=>{if(lastApp==IntPtr.Zero)return;var exe=AppWindows.ExeOf(lastApp);if(exe==null||exe=="explorer.exe"){AppWindows.Close(lastApp);return;}foreach(var w in AppWindows.List().Where(w=>w.Exe==exe))AppWindows.Close(w.Handle);}));
   app.Click+=(s,e)=>Drop(app,appMenu);
   menusHost=new StackPanel{Orientation=Orientation.Horizontal};left.Children.Add(menusHost);
   // Status items on the right.
   var right=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(0,0,8,0)};Grid.SetColumn(right,2);bar.Children.Add(right);
   Func<string,string,Action<Button>,Button> icon=(glyph,tip,action)=>{Button b=null;b=BarButton(new TextBlock{Text=glyph,FontFamily=new FontFamily(Ui.Icons),FontSize=14,Foreground=Brush(ink)},()=>action(b),tip);return b;};
   var battery=new TextBlock{FontSize=12.5,Foreground=Brush(ink),VerticalAlignment=VerticalAlignment.Center};var batteryGlyph=new TextBlock{Text="\uE83F",FontFamily=new FontFamily(Ui.Icons),FontSize=15,Foreground=Brush(ink),Margin=new Thickness(5,0,0,0),VerticalAlignment=VerticalAlignment.Center};
   var batteryRow=new StackPanel{Orientation=Orientation.Horizontal};batteryRow.Children.Add(battery);batteryRow.Children.Add(batteryGlyph);var batteryButton=BarButton(batteryRow,()=>Open("ms-settings:batterysaver"),"Battery");right.Children.Add(batteryButton);
   right.Children.Add(icon("\uE701","Wi-Fi",b=>ToggleControlCenter(b)));right.Children.Add(icon("\uE767","Sound",b=>ToggleControlCenter(b)));
   right.Children.Add(icon("\uE721","Search",b=>SystemControls.Press(SystemControls.Win,SystemControls.Key('S'))));right.Children.Add(icon("\uE9E9","Control Center",b=>ToggleControlCenter(b)));
   var clock=new TextBlock{FontSize=13,Foreground=Brush(ink),FontWeight=FontWeights.Medium};right.Children.Add(BarButton(clock,()=>SystemControls.Press(SystemControls.Win,SystemControls.Key('N')),"Notifications and calendar"));
   menuBar=new Window{Title="WinDotra menu bar",WindowStyle=WindowStyle.None,AllowsTransparency=true,Background=Brushes.Transparent,ShowInTaskbar=false,ShowActivated=false,Topmost=true,ResizeMode=ResizeMode.NoResize,Content=bar,FontFamily=Ui.SansFamily,Height=MenuBarHeight};
   if(Application.Current!=null&&Application.Current.MainWindow!=null)menuBar.Resources=Application.Current.MainWindow.Resources;
   var screen=PrimaryDip(null,false);menuBar.Left=screen.Left;menuBar.Top=screen.Top;menuBar.Width=screen.Width;
   menuBar.SourceInitialized+=(s,e)=>{menuBarHandle=new WindowInteropHelper(menuBar).Handle;if(Profile.MenuBarReserve)AddAppBar();};
   menuBar.Show();menuBar.Top=screen.Top;
   updateMenuBar=()=>{
    var front=AppWindows.GetForegroundWindow();var name=front==IntPtr.Zero?null:AppTitle(front);
    if(name!=null){appName.Text=name;lastApp=front;if(front!=menusFor){menusFor=front;RebuildMenus(front);}}
    var now=DateTime.Now;clock.Text=now.ToString("ddd d MMM")+"   "+now.ToString(Profile.Clock24?"HH:mm":"h:mm tt");
    bool hasBattery=stats.BatteryLevel>=0;batteryButton.Visibility=hasBattery?Visibility.Visible:Visibility.Collapsed;if(hasBattery){battery.Text=Math.Round(stats.BatteryLevel)+"%";batteryGlyph.Text=stats.BatteryLevel>75?"\uE83F":stats.BatteryLevel>40?"\uE85F":"\uE853";}
   };
   RebuildMenus(IntPtr.Zero);updateMenuBar();
  }
  // Brings the app back to the front, then sends it a shortcut (the menu bar took focus when you clicked it).
  void SendTo(IntPtr window,params ushort[] keys){if(window!=IntPtr.Zero)AppWindows.Activate(window);var later=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(120)};later.Tick+=(s,e)=>{later.Stop();SystemControls.Press(keys);};later.Start();}
  MenuItem Shortcut(string header,string gesture,IntPtr window,params ushort[] keys){var item=Item(header,()=>SendTo(window,keys));item.InputGestureText=gesture;return item;}
  void RebuildMenus(IntPtr window) {
   if(menusHost==null)return;menusHost.Children.Clear();string ink=MenuLight?"#1D1D1F":"#FFFFFF";
   Action<string,ContextMenu> add=(title,menu)=>{var b=BarButton(new TextBlock{Text=title,Foreground=Brush(ink)},()=>{},null);b.Click+=(s,e)=>Drop(b,menu);menusHost.Children.Add(b);};
   const ushort C=SystemControls.Ctrl,S=SystemControls.Shift;Func<char,ushort> K=SystemControls.Key;
   // The app's own classic menus, exactly as it has them.
   var native=window==IntPtr.Zero?IntPtr.Zero:SystemControls.GetMenu(window);
   var entries=native==IntPtr.Zero?null:SystemControls.ReadMenu(native,0);
   if(entries!=null&&entries.Count(e=>e.Children!=null&&e.Children.Count>0)>=2){
    foreach(var top in entries.Where(e=>e.Children!=null&&e.Children.Count>0).Take(9)){var menu=new ContextMenu();Fill(menu.Items,top.Children,window);add(top.Text,menu);}
   } else {
    string exe=window==IntPtr.Zero?"":AppWindows.ExeOf(window)??"";bool browser=new[]{"chrome.exe","msedge.exe","firefox.exe","brave.exe","opera.exe","vivaldi.exe"}.Contains(exe);
    var file=new ContextMenu();
    if(browser){file.Items.Add(Shortcut("New Tab","Ctrl+T",window,C,K('T')));file.Items.Add(Shortcut("New Window","Ctrl+N",window,C,K('N')));file.Items.Add(Shortcut("New Private Window","Ctrl+Shift+N",window,C,S,K('N')));file.Items.Add(Shortcut("Reopen Closed Tab","Ctrl+Shift+T",window,C,S,K('T')));file.Items.Add(new Separator());file.Items.Add(Shortcut("Close Tab","Ctrl+W",window,C,K('W')));}
    else if(exe=="explorer.exe"){file.Items.Add(Shortcut("New Window","Ctrl+N",window,C,K('N')));file.Items.Add(Shortcut("New Folder","Ctrl+Shift+N",window,C,S,K('N')));file.Items.Add(new Separator());file.Items.Add(Shortcut("Close Window","Ctrl+W",window,C,K('W')));}
    else{file.Items.Add(Shortcut("New","Ctrl+N",window,C,K('N')));file.Items.Add(Shortcut("Open…","Ctrl+O",window,C,K('O')));file.Items.Add(new Separator());file.Items.Add(Shortcut("Save","Ctrl+S",window,C,K('S')));file.Items.Add(Shortcut("Save As…","Ctrl+Shift+S",window,C,S,K('S')));}
    file.Items.Add(Shortcut("Print…","Ctrl+P",window,C,K('P')));file.Items.Add(new Separator());file.Items.Add(Item("Close Window",()=>{if(window!=IntPtr.Zero)AppWindows.Close(window);}));
    add("File",file);
    var edit=new ContextMenu();
    edit.Items.Add(Shortcut("Undo","Ctrl+Z",window,C,K('Z')));edit.Items.Add(Shortcut("Redo","Ctrl+Y",window,C,K('Y')));edit.Items.Add(new Separator());
    edit.Items.Add(Shortcut("Cut","Ctrl+X",window,C,K('X')));edit.Items.Add(Shortcut("Copy","Ctrl+C",window,C,K('C')));edit.Items.Add(Shortcut("Paste","Ctrl+V",window,C,K('V')));edit.Items.Add(Shortcut("Select All","Ctrl+A",window,C,K('A')));edit.Items.Add(new Separator());
    edit.Items.Add(Shortcut("Find…","Ctrl+F",window,C,K('F')));edit.Items.Add(new Separator());edit.Items.Add(Item("Emoji & Symbols",()=>SendTo(window,SystemControls.Win,0xBE)));
    add("Edit",edit);
    var view=new ContextMenu();
    if(browser||exe=="explorer.exe")view.Items.Add(Shortcut("Reload","F5",window,0x74));
    view.Items.Add(Shortcut("Zoom In","Ctrl++",window,C,0xBB));view.Items.Add(Shortcut("Zoom Out","Ctrl+-",window,C,0xBD));view.Items.Add(Shortcut("Actual Size","Ctrl+0",window,C,0x30));view.Items.Add(new Separator());
    view.Items.Add(Shortcut("Enter Full Screen","F11",window,0x7A));
    if(browser){view.Items.Add(new Separator());view.Items.Add(Shortcut("History","Ctrl+H",window,C,K('H')));view.Items.Add(Shortcut("Downloads","Ctrl+J",window,C,K('J')));view.Items.Add(Shortcut("Bookmarks","Ctrl+Shift+O",window,C,S,K('O')));}
    add("View",view);
   }
   var windowMenu=new ContextMenu();
   windowMenu.Items.Add(Item("Minimize",()=>{if(window!=IntPtr.Zero)AppWindows.Minimize(window);}));windowMenu.Items.Add(Item("Zoom",()=>{if(window!=IntPtr.Zero)AppWindows.Zoom(window);}));windowMenu.Items.Add(new Separator());
   windowMenu.Items.Add(Shortcut("Move to Left Half","Win+←",window,SystemControls.Win,0x25));windowMenu.Items.Add(Shortcut("Move to Right Half","Win+→",window,SystemControls.Win,0x27));windowMenu.Items.Add(Shortcut("Move to Next Screen","Win+Shift+→",window,SystemControls.Win,S,0x27));windowMenu.Items.Add(new Separator());
   windowMenu.Items.Add(Item("Show Desktop",()=>SystemControls.Press(SystemControls.Win,K('D'))));windowMenu.Items.Add(Item("Mission Control (Task View)",()=>SystemControls.Press(SystemControls.Win,0x09)));
   add("Window",windowMenu);
   var go=new ContextMenu();string home=Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
   foreach(var place in new[]{new[]{"Home",home},new[]{"Desktop",Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)},new[]{"Documents",Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)},new[]{"Downloads",System.IO.Path.Combine(home,"Downloads")},new[]{"Pictures",Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)},new[]{"Music",Environment.GetFolderPath(Environment.SpecialFolder.MyMusic)},new[]{"This PC","shell:MyComputerFolder"},new[]{"Recycle Bin","shell:RecycleBinFolder"}}){string target=place[1];go.Items.Add(Item(place[0],()=>Process.Start("explorer.exe",target.StartsWith("shell:")?target:"\""+target+"\"")));}
   add("Go",go);
   var help=new ContextMenu();help.Items.Add(Item("Get Help",()=>Open("ms-contact-support:")));help.Items.Add(Item("Tips",()=>Open("ms-get-started:")));help.Items.Add(Item("WinDotra…",()=>{if(ShowStudio!=null)ShowStudio();}));add("Help",help);
  }
  void Fill(ItemCollection items,List<SystemControls.Entry> entries,IntPtr window){
   foreach(var e in entries){
    if(e.Separator){if(items.Count>0&&!(items[items.Count-1] is Separator))items.Add(new Separator());continue;}
    var item=new MenuItem{Header=e.Text,InputGestureText=e.Shortcut,IsEnabled=!e.Disabled,IsChecked=e.Checked};
    if(e.Children!=null)Fill(item.Items,e.Children,window);else{uint id=e.Id;item.Click+=(s,a)=>{AppWindows.Activate(window);SystemControls.Command(window,id);};}
    items.Add(item);
   }
  }
  static string AppPath(IntPtr window){uint pid;GetWindowThreadProcessId(window,out pid);return AppWindows.PathOf(pid);}
  [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window,out uint process);
  // Windows' own shell pieces (Start, search, the taskbar) aren't apps, so the menu bar keeps showing the last real app.
  static readonly string[] ShellParts={"shellexperiencehost","startmenuexperiencehost","searchhost","searchapp","textinputhost","lockapp","shellhost","widgets","windowsterminal_overlay","ctfmon"};
  static readonly Dictionary<string,string> appNames=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
  string AppTitle(IntPtr window) {
   string path=AppPath(window);if(path==null)return null;uint pid;GetWindowThreadProcessId(window,out pid);if(pid==(uint)Process.GetCurrentProcess().Id)return null;
   string file=System.IO.Path.GetFileNameWithoutExtension(path).ToLowerInvariant();if(ShellParts.Contains(file))return null;
   if(file=="explorer"){var cls=new StringBuilder(64);GetClassName(window,cls,64);string c=cls.ToString();return c=="CabinetWClass"?"File Explorer":c=="Progman"||c=="WorkerW"?"Desktop":null;}
   if(file=="applicationframehost"){var title=new StringBuilder(128);GetWindowText(window,title,128);return title.Length>0&&title.Length<40?title.ToString():null;}
   string name;if(appNames.TryGetValue(path,out name))return name;
   try{var info=FileVersionInfo.GetVersionInfo(path);name=!String.IsNullOrWhiteSpace(info.FileDescription)&&info.FileDescription.Length<40?info.FileDescription:!String.IsNullOrWhiteSpace(info.ProductName)?info.ProductName:System.IO.Path.GetFileNameWithoutExtension(path);}catch(Exception){name=System.IO.Path.GetFileNameWithoutExtension(path);}
   appNames[path]=name;return name;
  }
  [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetWindowText(IntPtr window,StringBuilder text,int max);
  // Reserves the top strip so maximized windows stay below the bar. Always at the very top of the main screen.
  void AddAppBar() {
   var screen=System.Windows.Forms.Screen.PrimaryScreen.Bounds;var source=PresentationSource.FromVisual(menuBar);double scale=source!=null?source.CompositionTarget.TransformToDevice.M22:1;int height=(int)Math.Round(MenuBarHeight*scale);
   var data=new AppBarData{Size=Marshal.SizeOf(typeof(AppBarData)),Window=menuBarHandle,Edge=1};SHAppBarMessage(0,ref data);
   data.Rect=new LiveWallpaper.Rect32{Left=screen.Left,Top=screen.Top,Right=screen.Right,Bottom=screen.Top+height};SHAppBarMessage(2,ref data);
   data.Rect.Top=screen.Top;data.Rect.Bottom=screen.Top+height;SHAppBarMessage(3,ref data);
   var top=PrimaryDip(menuBar,false);menuBar.Top=top.Top;menuBar.Left=top.Left;
  }
  void RemoveAppBar(){if(menuBarHandle==IntPtr.Zero)return;var data=new AppBarData{Size=Marshal.SizeOf(typeof(AppBarData)),Window=menuBarHandle};SHAppBarMessage(1,ref data);menuBarHandle=IntPtr.Zero;}

  // ---------- Dock ----------
  public static bool TaskbarAutoHide {
   get{var data=new AppBarData{Size=Marshal.SizeOf(typeof(AppBarData))};return ((long)SHAppBarMessage(4,ref data)&1)!=0;}
   set{var data=new AppBarData{Size=Marshal.SizeOf(typeof(AppBarData)),Window=FindWindow("Shell_TrayWnd",null),Param=new IntPtr(value?1:2)};SHAppBarMessage(10,ref data);}
  }
  [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern IntPtr FindWindow(string cls,string name);
  // Apps found on this PC for a first Dock: Files, your browser, music, chat, AI, and settings.
  public static List<LaunchItem> DockDefaults() {
   var list=new List<LaunchItem>{new LaunchItem{Name="File Explorer",Target="explorer.exe"}};
   string local=Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),roaming=Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),x86=Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),pf=Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
   foreach(var app in new[]{new[]{"Google Chrome",System.IO.Path.Combine(pf,@"Google\Chrome\Application\chrome.exe")},new[]{"Google Chrome",System.IO.Path.Combine(x86,@"Google\Chrome\Application\chrome.exe")},new[]{"Microsoft Edge",System.IO.Path.Combine(x86,@"Microsoft\Edge\Application\msedge.exe")},new[]{"Firefox",System.IO.Path.Combine(pf,@"Mozilla Firefox\firefox.exe")},new[]{"Spotify",System.IO.Path.Combine(roaming,@"Spotify\Spotify.exe")},new[]{"Discord",System.IO.Path.Combine(local,@"Discord\Update.exe")},new[]{"Steam",System.IO.Path.Combine(x86,@"Steam\steam.exe")},new[]{"Visual Studio Code",System.IO.Path.Combine(local,@"Programs\Microsoft VS Code\Code.exe")},new[]{"Claude",AiApps.ClaudeApp??""},new[]{"ChatGPT",AiApps.ChatGptApp??""},new[]{"Terminal",System.IO.Path.Combine(local,@"Microsoft\WindowsApps\wt.exe")}}){
    // Store-app aliases have no icon of their own; those apps still show up in the Dock while they run.
    if(app[1].Length==0||!File.Exists(app[1])||app[1].IndexOf(@"\WindowsApps\",StringComparison.OrdinalIgnoreCase)>=0||app[0]=="Discord"||list.Any(i=>i.Name==app[0]))continue;
    list.Add(new LaunchItem{Name=app[0],Target=app[1]});}
   list.Add(new LaunchItem{Name="Notepad",Target="notepad.exe"});list.Add(new LaunchItem{Name="Settings",Target="ms-settings:"});
   return list.Take(14).ToList();
  }
  static readonly Dictionary<string,string[]> Tiles=new Dictionary<string,string[]>{{"ms-settings:",new[]{"","#8E8E93","#5A5A5E"}},{"ms-screenclip:",new[]{"","#FF6B6B","#C7353B"}},{"calc.exe",new[]{"","#FF9F0A","#C66A00"}},{"web",new[]{"","#5AC8FA","#0A84FF"}},{"other",new[]{"","#AEAEB2","#636366"}}};
  // An app icon at Dock size: the real icon when there is one, otherwise a rounded colored tile with a symbol.
  public string DataFolder;
  FrameworkElement DockIcon(LaunchItem item,double size) {
   string target=item.Target;ImageSource image=null;
   // Your own icon for this app, if you picked one.
   if(!String.IsNullOrEmpty(item.IconFile)&&File.Exists(item.IconFile)){try{var own=new BitmapImage();own.BeginInit();own.UriSource=new Uri(item.IconFile);own.DecodePixelWidth=(int)(size*2);own.CacheOption=BitmapCacheOption.OnLoad;own.EndInit();own.Freeze();return new Image{Source=own,Width=size,Height=size};}catch(Exception){}}
   if(!item.IsWeb&&!target.EndsWith(":")){string path=target;if(!System.IO.Path.IsPathRooted(path)){string full=System.IO.Path.Combine(Environment.SystemDirectory,path);if(!File.Exists(full))full=System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),path);path=full;}if(File.Exists(path)||Directory.Exists(path)||path.StartsWith("::"))image=ShellIcon.Get(path,(int)(size*1.5));}
   if(target=="shell:RecycleBinFolder")image=ShellIcon.Get("::{645FF040-5081-101B-9F08-00AA002F954E}",(int)(size*1.5));
   if(image!=null&&Profile.DockTiles){
    // macOS shape: every icon sits on the same rounded white tile, so the Dock looks even.
    var tileGrid=new Grid{Width=size,Height=size};tileGrid.Children.Add(new Border{CornerRadius=new CornerRadius(size*0.225),Background=new LinearGradientBrush(Color.FromRgb(255,255,255),Color.FromRgb(232,232,237),90),BorderBrush=Brush("#1F000000"),BorderThickness=new Thickness(0.8),Effect=new System.Windows.Media.Effects.DropShadowEffect{BlurRadius=6,ShadowDepth=1.5,Opacity=0.25,Direction=270}});
    tileGrid.Children.Add(new Image{Source=image,Width=size*0.66,Height=size*0.66,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center});return tileGrid;}
   if(image!=null)return new Image{Source=image,Width=size,Height=size};
   string[] tile;if(!Tiles.TryGetValue(item.IsWeb?"web":target,out tile))tile=Tiles["other"];
   var back=new LinearGradientBrush((Color)ColorConverter.ConvertFromString(tile[1]),(Color)ColorConverter.ConvertFromString(tile[2]),90);
   return new Border{Width=size,Height=size,CornerRadius=new CornerRadius(size*0.225),Background=back,Child=new TextBlock{Text=tile[0],FontFamily=new FontFamily(Ui.Icons),FontSize=size*0.5,Foreground=Brushes.White,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center}};
  }
  void DockBar() {
   double size=Profile.DockSize;bool light=Profile.DockLight;
   var row=new StackPanel{Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Bottom};
   var plate=new Border{CornerRadius=new CornerRadius(size*0.42),Background=Brush(light?"#8CF5F5F7":"#801E1E20"),BorderBrush=Brush(light?"#66FFFFFF":"#26FFFFFF"),BorderThickness=new Thickness(1),Padding=new Thickness(6,6,6,4),VerticalAlignment=VerticalAlignment.Bottom,Child=row,Effect=new System.Windows.Media.Effects.DropShadowEffect{BlurRadius=24,ShadowDepth=4,Opacity=0.35,Direction=270}};
   var host=new Grid{Margin=new Thickness(20,0,20,6)};host.Children.Add(plate);
   var cells=new List<FrameworkElement>();var dots=new Dictionary<FrameworkElement,Ellipse>();var exes=new Dictionary<FrameworkElement,string>();
   Func<LaunchItem,bool,FrameworkElement> cell=null;
   cell=(item,pinned)=>{
    var stack=new StackPanel{Margin=new Thickness(3,0,3,0),VerticalAlignment=VerticalAlignment.Bottom,Background=Brushes.Transparent,Cursor=Cursors.Hand,ToolTip=item.Name};
    var art=new Grid{Width=size,Height=size,LayoutTransform=new ScaleTransform(1,1),RenderTransform=new TranslateTransform(),VerticalAlignment=VerticalAlignment.Bottom};art.Children.Add(DockIcon(item,size));stack.Children.Add(art);
    var dot=new Ellipse{Width=4,Height=4,Fill=Brush(light?"#3A3A3C":"#E5E5EA"),Margin=new Thickness(0,3,0,0),Visibility=Visibility.Hidden};stack.Children.Add(dot);dots[stack]=dot;exes[stack]=AppTarget.Exe(item.Target);
    stack.MouseLeftButtonUp+=(s,e)=>{try{
     var exe=exes[stack];var running=exe==null?null:AppWindows.List().FirstOrDefault(w=>w.Exe==exe&&!(exe=="explorer.exe"&&!IsExplorerWindow(w.Handle)));
     if(running!=null)AppWindows.Activate(running.Handle);else{item.Open();var bounce=(TranslateTransform)art.RenderTransform;bounce.BeginAnimation(TranslateTransform.YProperty,new DoubleAnimation(0,-size*0.35,TimeSpan.FromMilliseconds(260)){AutoReverse=true,RepeatBehavior=new RepeatBehavior(2),EasingFunction=new QuadraticEase{EasingMode=EasingMode.EaseOut}});}
    }catch(Exception ex){report(ex.Message);}};
    var menu=new ContextMenu();menu.Items.Add(Item("Open",()=>item.Open()));
    if(!item.IsWeb&&!item.Target.EndsWith(":"))menu.Items.Add(Item("Show in File Explorer",()=>{string t=item.Target;if(System.IO.Path.IsPathRooted(t))Process.Start("explorer.exe","/select,\""+t+"\"");}));
    menu.Items.Add(new Separator());
    if(pinned)menu.Items.Add(Item("Change icon…",()=>{var dialog=new Microsoft.Win32.OpenFileDialog{Title="Choose an icon for "+item.Name,Filter="Icons and pictures|*.png;*.ico;*.jpg;*.jpeg;*.bmp"};if(dialog.ShowDialog()!=true)return;string dir=System.IO.Path.Combine(DataFolder??System.IO.Path.GetTempPath(),"icons");Directory.CreateDirectory(dir);string copy=System.IO.Path.Combine(dir,Guid.NewGuid().ToString("N")+System.IO.Path.GetExtension(dialog.FileName).ToLowerInvariant());File.Copy(dialog.FileName,copy);item.IconFile=copy;dirty=true;Persist();RebuildDock();}));
    if(pinned&&!String.IsNullOrEmpty(item.IconFile))menu.Items.Add(Item("Use the app's own icon",()=>{item.IconFile="";dirty=true;Persist();RebuildDock();}));
    if(pinned)menu.Items.Add(Item("Remove from Dock",()=>{Profile.DockApps.Remove(item);dirty=true;Persist();RebuildDock();}));
    else menu.Items.Add(Item("Keep in Dock",()=>{if(Profile.DockApps.Count>=30)throw new InvalidOperationException("The Dock holds up to 30 apps.");Profile.DockApps.Add(new LaunchItem{Name=item.Name,Target=item.Target});dirty=true;Persist();RebuildDock();}));
    menu.Items.Add(Item("Quit",()=>{var exe=exes[stack];if(exe==null)return;foreach(var w in AppWindows.List().Where(w=>w.Exe==exe))AppWindows.Close(w.Handle);}));
    menu.Items.Add(new Separator());menu.Items.Add(Item("Dock settings…",()=>{if(ShowStudio!=null)ShowStudio();}));
    stack.ContextMenu=menu;cells.Add(stack);return stack;
   };
   foreach(var item in Profile.DockApps)row.Children.Add(cell(item,true));
   var runningStart=row.Children.Count;var runningDivider=new Border{Width=1,Margin=new Thickness(6,6,6,10),Background=Brush(light?"#33000000":"#44FFFFFF"),Visibility=Visibility.Collapsed};row.Children.Add(runningDivider);
   var runningRow=new StackPanel{Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Bottom};row.Children.Add(runningRow);
   row.Children.Add(new Border{Width=1,Margin=new Thickness(6,6,6,10),Background=Brush(light?"#33000000":"#44FFFFFF")});
   string downloadsPath=System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"Downloads");
   row.Children.Add(cell(new LaunchItem{Name="Downloads",Target=downloadsPath},false));
   row.Children.Add(cell(new LaunchItem{Name="Recycle Bin",Target="shell:RecycleBinFolder"},false));
   dockWindow=new Window{Title="WinDotra Dock",WindowStyle=WindowStyle.None,AllowsTransparency=true,Background=Brushes.Transparent,ShowInTaskbar=false,ShowActivated=false,Topmost=true,ResizeMode=ResizeMode.NoResize,SizeToContent=SizeToContent.WidthAndHeight,Content=host,FontFamily=Ui.SansFamily};
   if(Application.Current!=null&&Application.Current.MainWindow!=null)dockWindow.Resources=Application.Current.MainWindow.Resources;
   host.MinHeight=size*(Profile.DockMagnify?1.75:1.05)+26;
   dockWindow.Show();bool hidden=false;
   Action place=()=>{var area=PrimaryDip(dockWindow,!Profile.DockAutoHide||TaskbarAutoHide);var full=PrimaryDip(dockWindow,false);dockWindow.UpdateLayout();dockWindow.Left=area.Left+(area.Width-dockWindow.ActualWidth)/2;double bottom=Profile.DockAutoHide?full.Bottom:area.Bottom;dockWindow.Top=hidden?full.Bottom-6:bottom-dockWindow.ActualHeight;};
   place();dockWindow.SizeChanged+=(s,e)=>place();
   // Magnification: icons near the pointer grow, like the macOS Dock.
   Action<double> magnify=x=>{foreach(var c in cells){if(!c.IsVisible)continue;var art=(FrameworkElement)((StackPanel)c).Children[0];double center=c.TranslatePoint(new Point(c.ActualWidth/2,0),row).X,distance=Math.Abs(x-center),k=Double.IsNaN(x)||!Profile.DockMagnify?1:1+0.6*Math.Max(0,1-distance/(size*2.4));var scale=(ScaleTransform)art.LayoutTransform;scale.ScaleX=scale.ScaleY=k;}};
   row.MouseMove+=(s,e)=>magnify(e.GetPosition(row).X);row.MouseLeave+=(s,e)=>magnify(Double.NaN);
   // Running apps get a dot; apps that aren't pinned appear after a divider.
   var shownRunning=new List<string>();
   updateDock=()=>{
    var windowsNow=AppWindows.List();var exesNow=new HashSet<string>(windowsNow.Where(w=>w.Exe!="explorer.exe"||IsExplorerWindow(w.Handle)).Select(w=>w.Exe));
    foreach(var pair in dots){string exe;exes.TryGetValue(pair.Key,out exe);pair.Value.Visibility=exe!=null&&exesNow.Contains(exe)?Visibility.Visible:Visibility.Hidden;}
    if(!Profile.DockRunning)return;
    var pinnedExes=new HashSet<string>(Profile.DockApps.Select(i=>AppTarget.Exe(i.Target)).Where(x=>x!=null));
    var extra=windowsNow.Where(w=>!pinnedExes.Contains(w.Exe)&&w.Exe!="explorer.exe"&&w.Exe!="applicationframehost.exe"&&w.Exe!="textinputhost.exe"&&w.Exe!="systemsettings.exe").GroupBy(w=>w.Exe).Select(g=>g.First()).Take(10).ToList();
    var names=extra.Select(w=>w.Exe).ToList();if(names.SequenceEqual(shownRunning))return;shownRunning=names;
    foreach(FrameworkElement old in runningRow.Children){cells.Remove(old);dots.Remove(old);exes.Remove(old);}runningRow.Children.Clear();
    foreach(var w in extra){var c=cell(new LaunchItem{Name=AppTitle(w.Handle)??w.Exe,Target=w.Path},false);runningRow.Children.Add(c);}
    runningDivider.Visibility=runningRow.Children.Count>0?Visibility.Visible:Visibility.Collapsed;
    foreach(var pair in dots){string exe;exes.TryGetValue(pair.Key,out exe);pair.Value.Visibility=exe!=null&&exesNow.Contains(exe)?Visibility.Visible:Visibility.Hidden;}
   };
   updateDock();
   // Auto-hide: slides away and comes back when the pointer touches the bottom of the screen.
   dockHover=()=>{if(!Profile.DockAutoHide||dockWindow==null)return;System.Drawing.Point cursor;GetCursorPos(out cursor);var screen=System.Windows.Forms.Screen.PrimaryScreen.Bounds;bool near=cursor.Y>=screen.Bottom-3&&cursor.X>=screen.Left&&cursor.X<screen.Right;bool over=dockWindow.IsMouseOver||cells.Any(c=>c.ContextMenu!=null&&c.ContextMenu.IsOpen);bool hide=!near&&!over;if(hide!=hidden){hidden=hide;place();}};
   if(Profile.DockAutoHide){hidden=true;place();}
  }
  static bool IsExplorerWindow(IntPtr window){var name=new StringBuilder(64);GetClassName(window,name,64);return name.ToString()=="CabinetWClass";}
  [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetClassName(IntPtr window,StringBuilder name,int max);
  void RebuildDock(){if(dockWindow!=null){dockWindow.Close();dockWindow=null;}updateDock=null;dockHover=null;if(Profile.Dock)DockBar();}
 }
}
