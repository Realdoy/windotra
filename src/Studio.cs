using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace WinCanvas {
 public partial class Studio : Window {
  readonly string folder; readonly Action appearance; readonly CustomizationService customization;
  public DesktopProfile Profile; public OverlayManager Overlays;
  readonly StackPanel body=new StackPanel{MaxWidth=820,HorizontalAlignment=HorizontalAlignment.Left}; readonly TextBlock status=new TextBlock(),running=new TextBlock(); readonly ScrollViewer scroll=new ScrollViewer();
  readonly System.Windows.Shapes.Ellipse runningDot=new System.Windows.Shapes.Ellipse{Width=7,Height=7,Margin=new Thickness(2,0,9,0)};
  readonly Dictionary<string,string> cursors=new Dictionary<string,string>();
  readonly Dictionary<string,Button> nav=new Dictionary<string,Button>();
  readonly DispatcherTimer animate=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(33)};
  System.Windows.Forms.NotifyIcon tray; GlobalShortcut shortcut,panic; bool trayHinted,quitting,launched;
  public void Quit(){quitting=true;Close();}
  public const string AppVersion="0.16";
  static string updateTag,updateUrl;
  // Asks GitHub for the newest release once per start; if it's newer, says so in the tray, the status bar, and on Home.
  async void CheckForUpdate(){try{using(var http=new System.Net.Http.HttpClient{Timeout=TimeSpan.FromSeconds(8)}){http.DefaultRequestHeaders.UserAgent.ParseAdd("WinDotra/"+AppVersion);var json=await http.GetStringAsync("https://api.github.com/repos/Realdoy/windotra/releases/latest");var data=new System.Web.Script.Serialization.JavaScriptSerializer().Deserialize<Dictionary<string,object>>(json);string tag=Convert.ToString(data["tag_name"]).TrimStart('v');Version latest,mine;
    if(Version.TryParse(tag,out latest)&&Version.TryParse(AppVersion,out mine)&&latest>mine){updateTag=tag;updateUrl=Convert.ToString(data["html_url"]);Report("WinDotra "+tag+" is out. Download it from Home.");if(tray!=null)tray.ShowBalloonTip(4000,"WinDotra "+tag+" is available","Open WinDotra → Home to download the update.",System.Windows.Forms.ToolTipIcon.Info);if(current=="Home")Page("Home");}}}catch(Exception){}}
  readonly WrapPanel quick=new WrapPanel{Margin=new Thickness(56,14,40,0)};
  // One-click switches for the big pieces, always at the top.
  void QuickBar(){
   quick.Children.Clear();bool on=Overlays!=null&&Overlays.Active;
   Func<string,bool,Action,Button> pill=(label,active,action)=>{var b=new Button{Content=(active?"● ":"○ ")+label,FontSize=12,Padding=new Thickness(12,5,12,5),Margin=new Thickness(0,0,8,0),Background=Brush(active?"#1F0A84FF":Ui.Surface),Foreground=Brush(active?"#64B5FF":Ui.Soft),ToolTip=active?"On: click to turn off":"Off: click to turn on"};b.Click+=(s,e)=>Safe(action);return b;};
   quick.Children.Add(pill("Desktop",on,()=>{if(Overlays.Active){Overlays.Stop();Report("Widgets, island, menu bar, and Dock are off.");}else Launch();}));
   quick.Children.Add(pill("Live wallpaper",Overlays.WallpaperRunning,()=>{Profile.LiveWallpaper=!Overlays.WallpaperRunning;Save();ToggleWallpaper();}));
   quick.Children.Add(pill("Menu bar",Profile.MenuBar,()=>{Profile.MenuBar=!Profile.MenuBar;RefreshDesktop();}));
   quick.Children.Add(pill("Dock",Profile.Dock,()=>{Profile.Dock=!Profile.Dock;if(Profile.Dock&&Profile.DockApps.Count==0)Profile.DockApps=OverlayManager.DockDefaults();TaskbarForDock(Profile.Dock);RefreshDesktop();}));
   quick.Children.Add(pill("Island",Profile.Island,()=>{Profile.Island=!Profile.Island;RefreshDesktop();}));
   var off=new Button{Content="Turn everything off",FontSize=12,Padding=new Thickness(12,5,12,5),Background=Brush("#33FF453A"),Foreground=Brush("#FF6961"),ToolTip="Back to normal Windows (Ctrl+Alt+Shift+W)"};off.Click+=(s,e)=>Safe(BackToWindows);quick.Children.Add(off);
  }
  public void LaunchQuietly(){if(launched||smoke||NeedsSetup||Profile.Off)return;launched=true;try{Launch();}catch(Exception e){Report(e.Message);}}
  const string RunKey=@"Software\Microsoft\Windows\CurrentVersion\Run";
  static bool StartsWithWindows {get{using(var key=Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKey))return key!=null&&(key.GetValue("WinDotra")!=null||key.GetValue("WinCanvas")!=null);}}
  // Keeps the sign-in entry pointing at this copy of WinDotra (and moves the old WinCanvas entry over).
  static void HealStartup(){try{if(StartsWithWindows)SetStartup(true);}catch(Exception){}}
  static void SetStartup(bool on){using(var key=Microsoft.Win32.Registry.CurrentUser.CreateSubKey(RunKey)){key.DeleteValue("WinCanvas",false);if(on)key.SetValue("WinDotra","\""+System.Reflection.Assembly.GetExecutingAssembly().Location+"\" --background");else key.DeleteValue("WinDotra",false);}}
  readonly List<System.Windows.Input.Cursor> previews=new List<System.Windows.Input.Cursor>();
  string current="Home"; bool smoke; Action changed;
  static readonly string[][] Pages={new[]{"Home","","Status and shortcuts"},new[]{"Styles","","Complete looks in one click, share your own"},new[]{"AI","","Ask anything, no API keys"},new[]{"Island","","Dynamic Island, buttons, quick launch"},new[]{"Menu bar & Dock","\uE8A1","Menu bar across the top, Dock at the bottom"},new[]{"Widgets","","Clock, system, notes, focus, calendar, island"},new[]{"Live wallpaper","","Calm animated scenes behind your icons"},new[]{"Typography","","Widget and classic Windows fonts"},new[]{"Cursors","","Static and animated pointers"},new[]{"Store","","Download styles and widgets, share your own"}};
  // One-click widget looks: name, accent, background.
  static readonly string[][] Themes={new[]{"Paper","#4F46E5","#FFFFFF"},new[]{"Stone","#57534E","#F5F5F4"},new[]{"Sage","#4D7C5A","#F3F6F1"},new[]{"Sky","#2563EB","#F2F7FB"},new[]{"Blush","#BE185D","#FBF4F4"},new[]{"Ink","#A5B4FC","#18181B"},new[]{"Night","#F5F5F4","#0C0A09"},new[]{"Midnight","#60A5FA","#111827"}};
  static SolidColorBrush Brush(string value) { return Ui.Brush(value); }
  public Studio(string folder,ResourceDictionary resources,Action appearance,bool smoke) {
   this.folder=folder; this.appearance=appearance;this.smoke=smoke; Resources=resources;
   Title="WinDotra"; Width=1180;Height=820;MinWidth=980;MinHeight=640;WindowStartupLocation=WindowStartupLocation.CenterScreen;
   Background=Brush(Ui.Paper);Foreground=Brush(Ui.Ink);FontFamily=Ui.SansFamily;FontSize=13;Icon=Brand.Logo;if(!smoke)HealStartup();
   Profile=new DesktopProfile(); string warning=null;
   if(File.Exists(ProfilePath)) {try {Profile=JsonFile.Read<DesktopProfile>(ProfilePath); if(Profile==null)throw new InvalidDataException("Empty profile");Profile.Validate();}catch(Exception e){Profile=new DesktopProfile();warning="Saved profile could not load: "+e.Message+" Original file retained until you save.";}}
   customization=new CustomizationService(new NativeCustomization(),folder);
   SetupAi();
   Overlays=new OverlayManager(Profile,Save,Report){DesktopAttach=!smoke,Demo=smoke};Overlays.Chat=chat;Overlays.ConfigureAi=OpenAiSetup;Overlays.ConfigureIsland=()=>{Show();Activate();Page("Island");};Overlays.DataFolder=folder;Overlays.WidgetsFolder=folder;Overlays.SwitchAppearance=dark=>Safe(()=>AppleAppearance(dark));Overlays.GoBackToWindows=()=>Safe(BackToWindows);Overlays.ShowStudio=()=>{Show();if(WindowState==WindowState.Minimized)WindowState=WindowState.Normal;Activate();};Overlays.ChangeEngine=engine=>Safe(()=>{SaveAi(c=>c.Provider=engine);if(current=="AI")Page(current);});Overlays.EditingChanged=on=>Report(on?"Edit mode: drag widgets, resize with the blue corner, use − to remove and ⋯ for size and color. Click Done when finished.":"Widgets locked in place.");
   if(!smoke){shortcut=new GlobalShortcut(this,()=>Overlays.OpenAssistant(),Report);panic=new GlobalShortcut(this,()=>Safe(BackToWindows),Report,7711,0x4007,0x57,"Ctrl+Alt+Shift+W");}
   var root=new Grid();root.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(232)});root.ColumnDefinitions.Add(new ColumnDefinition());
   root.Children.Add(Sidebar());
   var right=new Grid();right.RowDefinitions.Add(new RowDefinition());right.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});Grid.SetColumn(right,1);
   scroll.Content=body;scroll.VerticalScrollBarVisibility=ScrollBarVisibility.Auto;scroll.HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled;scroll.Padding=new Thickness(56,40,40,40);right.Children.Add(scroll);
   // Pages use one readable column: as wide as the window allows, up to 820.
   scroll.SizeChanged+=(s,e)=>{body.Width=Math.Max(420,Math.Min(820,scroll.ActualWidth-scroll.Padding.Left-scroll.Padding.Right-12));};
   right.RowDefinitions.Insert(0,new RowDefinition{Height=GridLength.Auto});Grid.SetRow(scroll,1);right.Children.Add(quick);
   right.Children.Add(StatusBar());root.Children.Add(right);
   WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.CanResize;
   System.Windows.Shell.WindowChrome.SetWindowChrome(this,new System.Windows.Shell.WindowChrome{CaptionHeight=36,ResizeBorderThickness=new Thickness(6),CornerRadius=new CornerRadius(0),GlassFrameThickness=new Thickness(0),UseAeroCaptionButtons=false});
   var frame=new Grid();frame.Children.Add(root);var lights=TrafficLights();frame.Children.Add(lights);Content=frame;
   StateChanged+=(s,e)=>frame.Margin=WindowState==WindowState.Maximized?new Thickness(7):new Thickness(0);
   SourceInitialized+=(s,e)=>{try{int round=2;DwmSetWindowAttribute(new System.Windows.Interop.WindowInteropHelper(this).Handle,33,ref round,4);}catch(Exception){}};
   animate.Tick+=(s,e)=>{foreach(var scene in FindScenes(body))scene.Tick();};
   if(!smoke) {
    tray=new System.Windows.Forms.NotifyIcon{Icon=Brand.TrayIcon()??Chrome.TrayIcon(Ui.Accent),Text="WinDotra",Visible=true};
    tray.DoubleClick+=(s,e)=>{Show();Activate();};var menu=new System.Windows.Forms.ContextMenuStrip();
    menu.Items.Add("Open WinDotra",null,(s,e)=>{Show();Activate();});menu.Items.Add("Ask AI",null,(s,e)=>Overlays.OpenAssistant());menu.Items.Add("Launch desktop",null,(s,e)=>Safe(Launch));
    menu.Items.Add("Edit widgets",null,(s,e)=>Safe(()=>{if(!Overlays.Active)Launch();Overlays.SetEditing(!Overlays.Editing);}));menu.Items.Add("Live wallpaper on/off",null,(s,e)=>Safe(ToggleWallpaper));menu.Items.Add("Stop everything",null,(s,e)=>{Overlays.Stop();Report("Widgets, island, and live wallpaper stopped.");});menu.Items.Add("Back to normal Windows",null,(s,e)=>Safe(BackToWindows));menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());menu.Items.Add("Quit WinDotra",null,(s,e)=>Quit());tray.ContextMenuStrip=menu;
   }
   PreviewKeyDown+=(s,e)=>{
    if((Keyboard.Modifiers&ModifierKeys.Control)==0)return;
    int index=e.Key>=Key.D1&&e.Key<=Key.D9?e.Key-Key.D1:e.Key>=Key.NumPad1&&e.Key<=Key.NumPad9?e.Key-Key.NumPad1:e.Key==Key.D0||e.Key==Key.NumPad0?9:-1;
    if(index>=0&&index<Pages.Length){Page(Pages[index][0]);e.Handled=true;}else if(e.Key==Key.L){Safe(Launch);e.Handled=true;}
   };
   // Closing the window keeps WinCanvas running in the tray; Quit (tray menu) really exits.
   Closing+=(s,e)=>{if(quitting||smoke||tray==null)return;e.Cancel=true;Hide();if(!trayHinted){trayHinted=true;tray.ShowBalloonTip(2500,"WinDotra keeps running","Your island and widgets stay on. Double-click the tray icon to come back, or right-click it to quit.",System.Windows.Forms.ToolTipIcon.Info);}};
   Closed+=(s,e)=>{animate.Stop();foreach(var cursor in previews)cursor.Dispose();Overlays.Dispose();chat.Cancel();aiClient.Dispose();if(shortcut!=null)shortcut.Dispose();if(panic!=null)panic.Dispose();if(tray!=null){tray.Icon.Dispose();tray.Dispose();}};
   if(!smoke)CheckForUpdate();
   Page(NeedsSetup?"Setup":"Home");Report(warning??aiWarning??"Nothing launches or changes Windows until you choose an action.");
  }
  static IEnumerable<WallpaperScene> FindScenes(DependencyObject parent) {
   for(int i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++){var child=VisualTreeHelper.GetChild(parent,i);var scene=child as WallpaperScene;if(scene!=null)yield return scene;else foreach(var inner in FindScenes(child))yield return inner;}
  }
  FrameworkElement Sidebar() {
   var side=new Grid{Margin=new Thickness(20,50,16,20)};side.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});side.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});side.RowDefinitions.Add(new RowDefinition());side.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
   var brand=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(10,0,0,30)};
   brand.Children.Add(new Image{Source=Brand.Logo,Width=30,Height=30,VerticalAlignment=VerticalAlignment.Center});
   var names=new StackPanel{Margin=new Thickness(10,0,0,0),VerticalAlignment=VerticalAlignment.Center};names.Children.Add(new TextBlock{Text=Brand.Name,FontFamily=Ui.SansFamily,FontSize=17,FontWeight=FontWeights.SemiBold});names.Children.Add(new TextBlock{Text=Brand.Tagline,FontSize=11,Foreground=Brush(Ui.Faint)});brand.Children.Add(names);side.Children.Add(brand);
   var list=new StackPanel();Grid.SetRow(list,1);
   for(int i=0;i<Pages.Length;i++){string page=Pages[i][0];var item=NavItem(Pages[i][1],page,"Ctrl+"+(i+1));item.Click+=(s,e)=>Safe(()=>Page(page));nav[page]=item;list.Children.Add(item);}
   list.Children.Add(new Border{Height=1,Background=Brush(Ui.Line),Margin=new Thickness(10,14,10,14)});
   var wall=NavItem("","Wallpaper & theme","Static wallpaper, light and dark mode");wall.Click+=(s,e)=>Safe(appearance);list.Children.Add(wall);side.Children.Add(list);
   var foot=new StackPanel{Margin=new Thickness(10,0,0,0)};Grid.SetRow(foot,3);var line=new StackPanel{Orientation=Orientation.Horizontal};line.Children.Add(runningDot);running.FontSize=12;running.Foreground=Brush(Ui.Soft);running.TextWrapping=TextWrapping.Wrap;running.MaxWidth=170;line.Children.Add(running);foot.Children.Add(line);
   var actions=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(-12,6,0,0)};
   var hide=Ghost("To tray",()=>{if(tray!=null){Hide();if(!trayHinted){trayHinted=true;tray.ShowBalloonTip(2500,"WinDotra is still running","Double-click the tray icon to come back.",System.Windows.Forms.ToolTipIcon.Info);}}});hide.ToolTip="Hide this window; everything keeps running";actions.Children.Add(hide);
   var stop=Ghost("Stop all",()=>{Overlays.Stop();Report("Widgets, island, and live wallpaper stopped.");});stop.ToolTip="Close widgets, island, and live wallpaper";actions.Children.Add(stop);foot.Children.Add(actions);side.Children.Add(foot);
   return new Border{Background=Brush(Ui.Sidebar),BorderBrush=Brush("#000000"),BorderThickness=new Thickness(0,0,1,0),Child=side};
  }
  [System.Runtime.InteropServices.DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr window,int attribute,ref int value,int size);
  // Close (hides to the tray), minimize, and zoom, drawn like macOS. Symbols appear when the pointer is over them.
  FrameworkElement TrafficLights() {
   var row=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Top,Margin=new Thickness(18,16,0,0),Background=Brushes.Transparent};
   var marks=new List<TextBlock>();
   Func<string,string,string,Action,Button> light=(fill,edge,glyph,action)=>{
    var mark=new TextBlock{Text=glyph,FontSize=9,FontWeight=FontWeights.Bold,Foreground=Brush("#99000000"),HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center,Opacity=0,Margin=new Thickness(0,-1,0,0)};marks.Add(mark);
    var face=new Grid{Width=13,Height=13};face.Children.Add(new System.Windows.Shapes.Ellipse{Fill=Brush(fill),Stroke=Brush(edge),StrokeThickness=0.6});face.Children.Add(mark);
    var b=new Button{Content=face,Width=13,Height=13,Margin=new Thickness(0,0,8,0),Padding=new Thickness(0),Background=Brushes.Transparent,BorderThickness=new Thickness(0),Cursor=System.Windows.Input.Cursors.Arrow,Focusable=false};
    var template=new ControlTemplate(typeof(Button));template.VisualTree=new FrameworkElementFactory(typeof(ContentPresenter));b.Template=template;
    System.Windows.Shell.WindowChrome.SetIsHitTestVisibleInChrome(b,true);b.Click+=(s,e)=>action();return b;};
   row.Children.Add(light("#FF5F57","#E0443E","×",()=>Close()));
   row.Children.Add(light("#FEBC2E","#DEA123","−",()=>WindowState=WindowState.Minimized));
   row.Children.Add(light("#28C840","#1AAB29","+",()=>WindowState=WindowState==WindowState.Maximized?WindowState.Normal:WindowState.Maximized));
   row.MouseEnter+=(s,e)=>{foreach(var m in marks)m.Opacity=1;};row.MouseLeave+=(s,e)=>{foreach(var m in marks)m.Opacity=0;};
   return row;
  }
  FrameworkElement StatusBar() {
   var bar=new Border{BorderBrush=Brush(Ui.Line),BorderThickness=new Thickness(0,1,0,0),Padding=new Thickness(56,12,40,14)};Grid.SetRow(bar,2);
   var grid=new Grid();grid.ColumnDefinitions.Add(new ColumnDefinition());grid.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
   status.TextWrapping=TextWrapping.Wrap;status.Foreground=Brush(Ui.Soft);status.FontSize=13;status.VerticalAlignment=VerticalAlignment.Center;grid.Children.Add(status);
   var keys=new TextBlock{Text="Ctrl+1–0 pages  ·  Ctrl+L launch  ·  Ctrl+Alt+Space AI",FontSize=11,Foreground=Brush(Ui.Faint),VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(16,0,0,0)};Grid.SetColumn(keys,1);grid.Children.Add(keys);
   bar.Child=grid;return bar;
  }
  static readonly Dictionary<string,string> TileColors=new Dictionary<string,string>{{"Home","#0A84FF"},{"Styles","#FF375F"},{"AI","#BF5AF2"},{"Island","#3A3A3C"},{"Menu bar & Dock","#5E5CE6"},{"Widgets","#FF9F0A"},{"Live wallpaper","#32ADE6"},{"Typography","#FF453A"},{"Cursors","#30D158"},{"Store","#0A84FF"},{"Profiles","#8E8E93"},{"Wallpaper & theme","#64D2FF"}};
  // A rounded, colored square with a white symbol, like the icons in macOS System Settings.
  static Border IconTile(string glyph,string label,double size){string color;if(!TileColors.TryGetValue(label,out color))color="#636366";var c=(Color)ColorConverter.ConvertFromString(color);
   return new Border{Width=size,Height=size,CornerRadius=new CornerRadius(size*0.27),Background=new LinearGradientBrush(WallpaperScene.Lighten(c,0.12),c,90),Margin=new Thickness(0,0,10,0),VerticalAlignment=VerticalAlignment.Center,Child=new TextBlock{Text=glyph,FontFamily=new FontFamily(Ui.Icons),FontSize=size*0.52,Foreground=Brushes.White,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center}};}
  Button NavItem(string glyph,string label,string tip) {
   var row=new StackPanel{Orientation=Orientation.Horizontal};row.Children.Add(IconTile(glyph,label,22));row.Children.Add(new TextBlock{Text=label,VerticalAlignment=VerticalAlignment.Center});
   return new Button{Content=row,Style=(Style)Resources["Nav"],ToolTip=tip};
  }
  Button Ghost(string label,Action action) {var b=new Button{Content=label,Style=(Style)Resources["Ghost"],FontSize=12,Padding=new Thickness(12,6,12,6),Margin=new Thickness(0)};b.Click+=(s,e)=>Safe(action);return b;}
  string ProfilePath {get{return Path.Combine(folder,"desktop.json");}}
  void Save() {Profile.Validate();JsonFile.Save(ProfilePath,Profile);}
  void Report(string text) {
   status.Text=text;try{QuickBar();}catch(Exception){}bool on=Overlays!=null&&Overlays.Active,live=Overlays!=null&&Overlays.WallpaperRunning;runningDot.Fill=Brush(on||live?"#16A34A":Ui.Faint);
   running.Text=on||live?(on?Overlays.Windows.Count+" widget"+(Overlays.Windows.Count==1?"":"s"):"")+(on&&live?" + ":"")+(live?"live wallpaper":"")+" running":"Desktop not running";
  }
  void Safe(Action action) {try{action();}catch(Exception e){Report(e.Message);if(!smoke)MessageBox.Show(this,e.Message,"WinDotra",MessageBoxButton.OK,MessageBoxImage.Warning);else throw;}}
  TextBlock Text(string value,double size,string color) {return new TextBlock{Text=value,FontSize=size,Foreground=Brush(color),TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,4,0,8)};}
  Button Button(string title,Action action) {var b=new Button{Content=title,Margin=new Thickness(0,8,8,8),HorizontalAlignment=HorizontalAlignment.Left};b.Click+=(s,e)=>Safe(action);return b;}
  Button Primary(string title,Action action) {var b=Button(title,action);b.Style=(Style)Resources["Primary"];return b;}
  // Sections are separated by hairlines instead of boxes.
  // Sections look like macOS Settings: a bold title and a short note above a rounded, raised panel.
  StackPanel Card(string title,string description) {
   var section=new StackPanel{Margin=new Thickness(0,8,0,22)};var heading=Text(title,15,Ui.Ink);heading.FontWeight=FontWeights.Bold;heading.Margin=new Thickness(4,0,0,2);section.Children.Add(heading);
   if(description.Length>0){var note=Text(description,12.5,Ui.Faint);note.Margin=new Thickness(4,0,0,8);section.Children.Add(note);}else heading.Margin=new Thickness(4,0,0,8);
   var panel=new StackPanel{Margin=new Thickness(16,8,16,10)};section.Children.Add(new Border{Background=Brush(Ui.Surface),CornerRadius=new CornerRadius(12),Child=panel});
   body.Children.Add(section);return panel;
  }
  void Header(string title,string description) {
   var heading=new TextBlock{Text=title,FontFamily=Ui.SansFamily,FontSize=26,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,0,0,6),TextWrapping=TextWrapping.Wrap};body.Children.Add(heading);var sub=Text(description,15,Ui.Soft);sub.Margin=new Thickness(0,0,0,24);body.Children.Add(sub);
  }
  void PageSwitch(string label,bool on,Action<bool> set){
   var row=new Grid{Margin=new Thickness(0,-8,0,20)};row.ColumnDefinitions.Add(new ColumnDefinition());row.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
   var text=new TextBlock{Text=label+(on?"  ·  on":"  ·  off"),FontSize=14,FontWeight=FontWeights.SemiBold,Foreground=Brush(on?"#30D158":Ui.Soft),VerticalAlignment=VerticalAlignment.Center};row.Children.Add(text);
   var toggle=new CheckBox{IsChecked=on,Style=(Style)Resources["Switch"],Padding=new Thickness(0),VerticalAlignment=VerticalAlignment.Center,ToolTip=on?"Turn off":"Turn on"};toggle.Click+=(s2,e2)=>Safe(()=>{set(toggle.IsChecked==true);Page(current);});Grid.SetColumn(toggle,1);row.Children.Add(toggle);
   body.Children.Add(new Border{Background=Brush(Ui.Surface),CornerRadius=new CornerRadius(12),Padding=new Thickness(16,10,16,10),Margin=new Thickness(0,0,0,18),Child=row});
  }
  void Divider(Panel panel) {panel.Children.Add(new Border{Height=1,Background=Brush(Ui.Line)});}
  void Check(Panel panel,string label,bool initial,Action<bool> update) {
   if(panel.Children.Count>0&&panel.Children[panel.Children.Count-1] is CheckBox)Divider(panel);
   var c=new CheckBox{Content=label,IsChecked=initial,Style=(Style)Resources["Switch"]};c.Click+=(s,e)=>{update(c.IsChecked==true);if(changed!=null)changed();};panel.Children.Add(c);
  }
  void Slider(Panel panel,string label,double value,double min,double max,Action<double> update) {
   var row=new Grid{Margin=new Thickness(0,10,0,0)};row.Children.Add(new TextBlock{Text=label,FontSize=13,Foreground=Brush(Ui.Soft)});
   var number=new TextBlock{Text=value.ToString("0.##"),FontSize=13,Foreground=Brush(Ui.Ink),HorizontalAlignment=HorizontalAlignment.Right};row.Children.Add(number);panel.Children.Add(row);
   var slider=new Slider{Minimum=min,Maximum=max,Value=value,TickFrequency=max-min<=5?0.05:1,IsSnapToTickEnabled=true,Margin=new Thickness(0,4,0,4)};
   slider.ValueChanged+=(s,e)=>{update(slider.Value);number.Text=slider.Value.ToString("0.##");if(changed!=null)changed();};panel.Children.Add(slider);
  }
  // A searchable font list; every name is shown in its own font.
  FrameworkElement Fonts(string initial,Action<string> update) {
   var panel=new StackPanel{Margin=new Thickness(0,6,0,10)};var all=FontLibrary.Families();
   var search=new TextBox{Margin=new Thickness(0,0,0,6),ToolTip="Search fonts"};var hint=new TextBlock{Text="Search "+all.Count+" fonts",Foreground=Brush(Ui.Faint),Margin=new Thickness(12,0,0,6),VerticalAlignment=VerticalAlignment.Center,IsHitTestVisible=false};
   var box=new Grid();box.Children.Add(search);box.Children.Add(hint);panel.Children.Add(box);
   var item=new FrameworkElementFactory(typeof(TextBlock));item.SetBinding(TextBlock.TextProperty,new System.Windows.Data.Binding());item.SetBinding(TextBlock.FontFamilyProperty,new System.Windows.Data.Binding());item.SetValue(TextBlock.FontSizeProperty,16.0);item.SetValue(TextBlock.PaddingProperty,new Thickness(6,3,6,3));
   var list=new ListBox{Height=230,ItemTemplate=new DataTemplate{VisualTree=item},BorderBrush=Brush(Ui.Line),BorderThickness=new Thickness(1),Background=Brush(Ui.Surface)};
   VirtualizingStackPanel.SetIsVirtualizing(list,true);ScrollViewer.SetHorizontalScrollBarVisibility(list,ScrollBarVisibility.Disabled);panel.Children.Add(list);
   bool ready=false;Action filter=()=>{string q=search.Text.Trim();list.ItemsSource=q.Length==0?all:all.Where(n=>n.IndexOf(q,StringComparison.OrdinalIgnoreCase)>=0).ToList();list.SelectedItem=initial;};
   search.TextChanged+=(s,e)=>{hint.Visibility=search.Text.Length>0?Visibility.Collapsed:Visibility.Visible;filter();};
   list.SelectionChanged+=(s,e)=>{if(ready&&list.SelectedItem!=null){initial=list.SelectedItem.ToString();update(initial);}};
   filter();if(list.SelectedItem!=null)list.ScrollIntoView(list.SelectedItem);ready=true;return panel;
  }
  Button Swatch(string label,string color,Action action) {
   var row=new StackPanel{Orientation=Orientation.Horizontal};row.Children.Add(new Border{Width=14,Height=14,CornerRadius=new CornerRadius(7),Background=Brush(color),BorderBrush=Brush("#26000000"),BorderThickness=new Thickness(1),Margin=new Thickness(0,0,9,0)});row.Children.Add(new TextBlock{Text=label});row.Children.Add(new TextBlock{Text="  "+color,Foreground=Brush(Ui.Faint)});
   var b=Button("",action);b.Content=row;return b;
  }
  Button Chip(string label,bool selected,Action action) {
   var b=Button(label,action);b.Padding=new Thickness(14,7,14,7);b.Margin=new Thickness(0,4,8,4);if(selected){b.Background=Brush(Ui.Accent);b.Foreground=Brush("#FFFFFF");}return b;
  }
  void PickColor(bool accent) {
   using(var dialog=new System.Windows.Forms.ColorDialog()) {dialog.FullOpen=true;dialog.Color=System.Drawing.ColorTranslator.FromHtml(accent?Profile.Accent:Profile.Background);if(dialog.ShowDialog()!=System.Windows.Forms.DialogResult.OK)return;string color=String.Format("#{0:X2}{1:X2}{2:X2}",dialog.Color.R,dialog.Color.G,dialog.Color.B);if(accent)Profile.Accent=color;else Profile.Background=color;}Page(current);
  }
  void Launch() {Profile.Off=false;Profile.Validate();Save();if(!smoke)TaskbarForDock(Profile.Dock);Overlays.Profile=Profile;Overlays.Start();Report("Desktop launched. Drag a widget's title to move it; hover the island to open it.");}
  void ToggleWallpaper() {if(Overlays.WallpaperRunning){Overlays.StopWallpaper();Report("Live wallpaper stopped. Your normal wallpaper is back.");}else{Overlays.Profile=Profile;Overlays.StartWallpaper();Report("Live wallpaper running behind your icons. It pauses when a full-screen app is in front.");}if(current=="Live wallpaper")Page(current);}
  public void Page(string name) {
   // Re-rendering the same page (after a color pick, say) keeps your scroll position.
   double offset=name==current?scroll.VerticalOffset:0;
   current=name;changed=null;animate.Stop();body.Children.Clear();foreach(var cursor in previews)cursor.Dispose();previews.Clear();
   foreach(var item in nav)item.Value.Tag=item.Key==name?"active":null;
   if(name=="Home")Home();else if(name=="AI")AiSettings();else if(name=="Widgets")Widgets();else if(name=="Live wallpaper")Wallpaper();else if(name=="Typography")Typography();else if(name=="Cursors")Cursors();else if(name=="Setup")Setup();else if(name=="Island")IslandPage();else if(name=="Styles")StylesPage();else if(name=="Menu bar & Dock")DesktopPage();else if(name=="Profiles")Profiles();else StorePage();
   scroll.UpdateLayout();scroll.ScrollToVerticalOffset(offset);
   if(FindScenes(body).Any()&&!smoke)animate.Start();
   if(!smoke)body.BeginAnimation(UIElement.OpacityProperty,new System.Windows.Media.Animation.DoubleAnimation(0.3,1,TimeSpan.FromMilliseconds(160)));
  }
  // A miniature of the desktop: the chosen live-wallpaper scene with the island and two widgets on top.
  Grid DesktopPreview(double height,bool widgets) {
   var preview=new Grid{Height=height,ClipToBounds=true};preview.Children.Add(new WallpaperScene(SceneOptions.From(Profile)));if(!widgets)return preview;
   var pv=new Palette(Profile.Background,Profile.Accent);var font=FontLibrary.For(Profile.Font);var edge=Brush(pv.Line);
   var pill=new TextBlock{Text=DateTime.Now.ToString(Profile.Clock24?"HH:mm":"h:mm tt")+"   ·   "+DateTime.Now.ToString("ddd d MMM")+"   ·   Ask AI",FontFamily=font,FontSize=11.5,Foreground=Brush("#F4F4F5"),HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};
   preview.Children.Add(new Border{Width=Math.Min(Profile.IslandWidth*0.72,360),Height=38,VerticalAlignment=VerticalAlignment.Top,Margin=new Thickness(0,14,0,0),CornerRadius=new CornerRadius(19),Background=Brush("#000000"),Child=pill,Effect=new System.Windows.Media.Effects.DropShadowEffect{BlurRadius=16,ShadowDepth=2,Opacity=0.2}});
   var tile=new StackPanel{Margin=new Thickness(16,10,16,12)};tile.Children.Add(new TextBlock{Text="Clock",FontSize=9.5,Foreground=Brush(pv.Soft),FontFamily=font});
   tile.Children.Add(new TextBlock{Text=DateTime.Now.ToString(Profile.Clock24?"HH:mm":"h:mm"),FontSize=Math.Min(Profile.FontSize*2.4,40),FontWeight=FontWeights.Light,Foreground=Brush(pv.Ink),FontFamily=font});tile.Children.Add(new TextBlock{Text=DateTime.Now.ToString("dddd, d MMMM"),FontSize=Math.Min(Profile.FontSize*0.8,15),Foreground=Brush(pv.Soft),FontFamily=font});
   preview.Children.Add(new Border{Width=Profile.WidgetWidth*0.72,HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Bottom,Margin=new Thickness(18),CornerRadius=new CornerRadius(Profile.Radius*0.72),Background=Brush(Profile.Background),BorderBrush=edge,BorderThickness=new Thickness(1),Opacity=Profile.Opacity,Child=tile});
   var meters=new StackPanel{Margin=new Thickness(16,10,16,14)};meters.Children.Add(new TextBlock{Text="System",FontSize=9.5,Foreground=Brush(pv.Soft),FontFamily=font,Margin=new Thickness(0,0,0,6)});
   foreach(var pair in new[]{new[]{"CPU","0.32"},new[]{"Memory","0.58"}}){meters.Children.Add(new TextBlock{Text=pair[0],FontSize=9,Foreground=Brush(pv.Soft),FontFamily=font});meters.Children.Add(new ProgressBar{Maximum=1,Value=Double.Parse(pair[1],System.Globalization.CultureInfo.InvariantCulture),Foreground=Brush(Profile.Accent),Background=Brush(pv.Chip),Margin=new Thickness(0,3,0,7)});}
   preview.Children.Add(new Border{Width=Profile.WidgetWidth*0.62,HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Bottom,Margin=new Thickness(18),CornerRadius=new CornerRadius(Profile.Radius*0.72),Background=Brush(Profile.Background),BorderBrush=edge,BorderThickness=new Thickness(1),Opacity=Profile.Opacity,Child=meters});
   return preview;
  }
  Border Framed(UIElement child) {return new Border{CornerRadius=new CornerRadius(12),ClipToBounds=true,Margin=new Thickness(0,8,0,12),Child=child,BorderBrush=Brush(Ui.Line),BorderThickness=new Thickness(1)};}
  StackPanel Stat(string label,string value) {
   var panel=new StackPanel{Margin=new Thickness(0,0,20,0)};panel.Children.Add(new TextBlock{Text=label,FontSize=12,Foreground=Brush(Ui.Soft)});panel.Children.Add(new TextBlock{Text=value,FontSize=16,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,4,0,0),TextTrimming=TextTrimming.CharacterEllipsis});return panel;
  }
  // Warns when the Windows menu font is an icon font (menus everywhere show boxes) and offers a one-click fix.
  void MenuFontWarning() {
   string face=null;try{face=new NativeCustomization().ReadFonts().Menu.Face;}catch(Exception){}
   if(face==null||FontLibrary.HasLetters(face))return;
   var row=new Grid();row.ColumnDefinitions.Add(new ColumnDefinition());row.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
   var text=Text("Your Windows menu font is “"+face+"”, an icon font with no letters, so menus in Windows and apps show boxes.",13,"#FFD60A");text.VerticalAlignment=VerticalAlignment.Center;text.Margin=new Thickness(0,0,12,0);row.Children.Add(text);
   var fix=Primary(customization.FontUndo?"Fix: restore previous fonts":"Fix: use Segoe UI",()=>{if(customization.FontUndo)customization.UndoFonts();else customization.ApplyFonts("Segoe UI",9);Report("Windows menu fonts fixed. Reopen apps to see the change.");Page(current);});fix.Margin=new Thickness(0);Grid.SetColumn(fix,1);row.Children.Add(fix);
   body.Children.Add(new Border{Background=Brush("#3A2E0A"),BorderBrush=Brush("#7A5F10"),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(12),Padding=new Thickness(16,12,16,12),Margin=new Thickness(0,0,0,20),Child=row});
  }
  void Home() {
   int hour=DateTime.Now.Hour;Header(hour<5?"Working late.":hour<12?"Good morning.":hour<18?"Good afternoon.":"Good evening.",DateTime.Now.ToString("dddd, d MMMM")+". Your desktop, kept simple.");
   MenuFontWarning();
   var escape=new Grid{Margin=new Thickness(0,0,0,20)};escape.ColumnDefinitions.Add(new ColumnDefinition());escape.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
   var escapeText=Text("Panic button: puts back the Windows taskbar, your cursors, fonts, and wallpaper, and turns WinDotra's desktop off. Also in the tray menu, the menu bar's logo menu, and Ctrl+Alt+Shift+W.",12.5,Ui.Soft);escapeText.Margin=new Thickness(0,0,16,0);escapeText.VerticalAlignment=VerticalAlignment.Center;escape.Children.Add(escapeText);
   var escapeButton=Button("Back to normal Windows",BackToWindows);escapeButton.Background=Brush("#FF453A");escapeButton.Foreground=Brushes.White;escapeButton.FontWeight=FontWeights.SemiBold;escapeButton.Margin=new Thickness(0);Grid.SetColumn(escapeButton,1);escape.Children.Add(escapeButton);body.Children.Add(escape);
   string before=Path.Combine(ProfilesFolder,"Before going back to Windows.json");
   if(Profile.Off&&File.Exists(before)){var back=Primary("Bring back my WinDotra desktop",()=>{UseProfile(LoadProfile(before));Report("Welcome back. Your desktop is as you left it.");});back.Margin=new Thickness(0,-8,0,20);body.Children.Add(back);}
   if(updateTag!=null){var up=Primary("Download WinDotra "+updateTag,()=>System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(updateUrl){UseShellExecute=true}));up.Margin=new Thickness(0,-8,0,20);body.Children.Add(up);}
   int open=Profile.Tasks.Count(t=>!t.Done),widgets=new[]{Profile.Clock,Profile.Stats,Profile.Note,Profile.Focus,Profile.Calendar}.Count(v=>v);
   var stats=new UniformGrid{Columns=4,Margin=new Thickness(0,0,0,24)};
   stats.Children.Add(Stat("Desktop",Overlays.Active?"Running":"Off"));stats.Children.Add(Stat("AI",AiSummary()));stats.Children.Add(Stat("Tasks",open==0?"All clear":open+" open"));stats.Children.Add(Stat("Live wallpaper",Overlays.WallpaperRunning?Profile.Scene:"Off"));body.Children.Add(stats);
   body.Children.Add(Framed(DesktopPreview(260,true)));
   var actions=new WrapPanel{Margin=new Thickness(0,0,0,12)};actions.Children.Add(Primary("Launch desktop",Launch));actions.Children.Add(Button(Overlays.WallpaperRunning?"Stop live wallpaper":"Start live wallpaper",()=>{ToggleWallpaper();Page(current);}));actions.Children.Add(Button("Ask AI",()=>Overlays.OpenAssistant()));body.Children.Add(actions);
   var list=Card("Everything in WinDotra","");
   foreach(var page in Pages.Skip(1)){string target=page[0];list.Children.Add(Row(page[1],target,page[2],()=>Page(target)));}
   list.Children.Add(Row("","Wallpaper & theme","Static wallpaper, light and dark mode, transparency",appearance));
   list.Children.Add(Text(widgets+" of 5 widgets on"+(Profile.Island?", island on":"")+". Nothing runs at startup unless you start it.",12,Ui.Faint));
   body.Children.Add(Button("Run setup again",StartSetup));
   var always=Card("Always there","Closing this window keeps your island, widgets, and live wallpaper running. Quit from the tray icon (right-click it).");
   Check(always,"Start WinDotra quietly when I sign in to Windows",StartsWithWindows,v=>Safe(()=>{SetStartup(v);Report(v?"WinDotra will start in the background when you sign in.":"WinDotra won't start by itself anymore.");}));
  }
  Button Row(string glyph,string title,string description,Action action) {
   var grid=new Grid();grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(40)});grid.ColumnDefinitions.Add(new ColumnDefinition());grid.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
   grid.Children.Add(IconTile(glyph,title,26));
   var text=new StackPanel();text.Children.Add(new TextBlock{Text=title,FontSize=14,FontWeight=FontWeights.SemiBold});text.Children.Add(new TextBlock{Text=description,FontSize=12,Foreground=Brush(Ui.Soft),Margin=new Thickness(0,2,0,0)});Grid.SetColumn(text,1);grid.Children.Add(text);
   var arrow=new TextBlock{Text="",FontFamily=new FontFamily(Ui.Icons),FontSize=11,Foreground=Brush(Ui.Faint),VerticalAlignment=VerticalAlignment.Center};Grid.SetColumn(arrow,2);grid.Children.Add(arrow);
   var b=new Button{Content=grid,Style=(Style)Resources["Ghost"],HorizontalContentAlignment=HorizontalAlignment.Stretch,HorizontalAlignment=HorizontalAlignment.Stretch,Padding=new Thickness(10,12,12,12),Margin=new Thickness(-10,0,0,0),Foreground=Brush(Ui.Ink)};b.Click+=(s,e)=>Safe(action);return b;
  }
  void Widgets() {
   Header("Widgets","Choose what lives on your desktop. The preview follows your changes; widgets update when you launch.");
   PageSwitch("Widgets on the desktop",Overlays.Active,on=>{if(on)Launch();else{Overlays.Stop();Report("Widgets are off.");}});
   var actions=new WrapPanel{Margin=new Thickness(0,0,0,18)};actions.Children.Add(Primary("Edit widgets on the desktop",()=>{Save();if(!Overlays.Active){Overlays.Profile=Profile;Overlays.Start();}Overlays.SetEditing(true);}));actions.Children.Add(Button("Save & launch",Launch));actions.Children.Add(Button("Reset positions",()=>{Profile.Positions.Clear();Launch();}));actions.Children.Add(Button("Stop widgets",()=>{Overlays.Stop();Report("Widgets stopped.");}));body.Children.Add(actions);
   var holder=new Border();body.Children.Add(Framed(holder));holder.Child=DesktopPreview(240,true);changed=()=>{holder.Child=DesktopPreview(240,true);};
   var themes=Card("Theme","Sets the widget background and accent together.");
   var swatches=new WrapPanel();
   foreach(var theme in Themes){string accent=theme[1],background=theme[2];var dots=new StackPanel{Orientation=Orientation.Horizontal};dots.Children.Add(new Border{Width=18,Height=18,CornerRadius=new CornerRadius(9),Background=Brush(background),BorderBrush=Brush("#26000000"),BorderThickness=new Thickness(1),Child=new System.Windows.Shapes.Ellipse{Width=7,Height=7,Fill=Brush(accent)}});dots.Children.Add(new TextBlock{Text=theme[0],Margin=new Thickness(8,0,0,0),VerticalAlignment=VerticalAlignment.Center});
    bool on=Profile.Accent==accent&&Profile.Background==background;var chip=Button("",()=>{Profile.Accent=accent;Profile.Background=background;Page(current);Report("Theme set. Launch to see it on your desktop.");});chip.Content=dots;chip.Padding=new Thickness(10,7,14,7);chip.Margin=new Thickness(0,4,8,4);if(on){chip.BorderBrush=Brush(Ui.Ink);chip.BorderThickness=new Thickness(1.5);}swatches.Children.Add(chip);}
   themes.Children.Add(swatches);var colors=new WrapPanel{Margin=new Thickness(0,6,0,0)};colors.Children.Add(Swatch("Accent",Profile.Accent,()=>PickColor(true)));colors.Children.Add(Swatch("Background",Profile.Background,()=>PickColor(false)));themes.Children.Add(colors);Check(themes,"Frosted glass  ·  macOS-style blurred cards",Profile.Glass,v=>Profile.Glass=v);
   var modules=Card("On your desktop","Widgets stay where they are. To move, resize, or recolor them, use Edit widgets (right-click any widget).");
   Check(modules,"Clock",Profile.Clock,v=>Profile.Clock=v);Check(modules,"System  ·  CPU, memory, power",Profile.Stats,v=>Profile.Stats=v);Check(modules,"Notes  ·  saved automatically",Profile.Note,v=>Profile.Note=v);Check(modules,"Focus timer",Profile.Focus,v=>Profile.Focus=v);Check(modules,"Calendar",Profile.Calendar,v=>Profile.Calendar=v);Check(modules,"Now playing  ·  album art and controls",Profile.Music,v=>Profile.Music=v);Check(modules,"Reminders  ·  shares your island tasks",Profile.Reminders,v=>Profile.Reminders=v);Check(modules,"Big date  ·  F R I D A Y on your desktop, Rainmeter style",Profile.Day,v=>Profile.Day=v);
   var style=Card("Shape","Size, corners, and see-through amount.");
   Slider(style,"Opacity",Profile.Opacity,0.35,1,v=>Profile.Opacity=v);Slider(style,"Corner radius",Profile.Radius,0,40,v=>Profile.Radius=v);Slider(style,"Widget size",Profile.WidgetWidth,240,480,v=>Profile.WidgetWidth=v);
   var behavior=Card("Behavior","");
   Check(behavior,"Keep widgets above windows",Profile.Topmost,v=>Profile.Topmost=v);Check(behavior,"Analog clock face",Profile.ClockAnalog,v=>Profile.ClockAnalog=v);Check(behavior,"24-hour clock",Profile.Clock24,v=>Profile.Clock24=v);Check(behavior,"Sound when focus ends",Profile.FocusSound,v=>Profile.FocusSound=v);
   Slider(behavior,"Focus minutes",Profile.FocusMinutes,1,180,v=>Profile.FocusMinutes=(int)v);
  }
  void Typography() {
   MenuFontWarning();
   Header("Typography","Any installed font for your widgets, plus the classic Windows menu and title fonts.");
   var widget=Card("Widget font","Used by every widget and the island. Imports need the same font installed.");
   var sample=new TextBlock{Text="The quick brown fox jumps.  0123456789",FontSize=Profile.FontSize*1.6,Foreground=Brush(Ui.Ink),FontFamily=FontLibrary.For(Profile.Font),TextWrapping=TextWrapping.Wrap};
   var small=new TextBlock{Text="Focus 25:00   ·   CPU 32%   ·   Tuesday, 3 March",FontSize=Profile.FontSize,Foreground=Brush(Ui.Soft),FontFamily=FontLibrary.For(Profile.Font),Margin=new Thickness(0,8,0,0)};
   var specimen=new StackPanel{Margin=new Thickness(22,18,22,18)};specimen.Children.Add(sample);specimen.Children.Add(small);widget.Children.Add(new Border{Background=Brush(Ui.Surface),BorderBrush=Brush(Ui.Line),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(10),Margin=new Thickness(0,4,0,12),Child=specimen});
   widget.Children.Add(Fonts(Profile.Font,f=>{Profile.Font=f;sample.FontFamily=FontLibrary.For(f);small.FontFamily=sample.FontFamily;}));Slider(widget,"Size",Profile.FontSize,11,28,v=>{Profile.FontSize=v;sample.FontSize=v*1.6;small.FontSize=v;});widget.Children.Add(Primary("Save & launch",Launch));
   var more=Card("Add fonts","Install any .ttf, .otf, or .ttc file, or add free fonts with one click. Fonts are installed for your Windows account only (no admin), so other apps can use them too.");
   more.Children.Add(Primary("Install a font file…",()=>{var dialog=new Microsoft.Win32.OpenFileDialog{Title="Choose font files",Filter="Fonts|*.ttf;*.otf;*.ttc",Multiselect=true};if(dialog.ShowDialog(this)!=true)return;var names=dialog.FileNames.Select(FontLibrary.Install).ToList();Report("Installed "+String.Join(", ",names)+". Pick it in the list above.");Page(current);}));
   more.Children.Add(Text("Free font pack  ·  Google Fonts, open license",13,Ui.Soft));var pack=new WrapPanel();
   foreach(var font in FontLibrary.Free){string name=font[0],path=font[1];bool have=FontLibrary.Has(name);var chip=Chip((have?"✓ ":"")+name,false,()=>{});chip.ToolTip=font[2]+(have?" · installed":" · click to download and install");if(have)chip.FontFamily=FontLibrary.For(name);
    chip.Click+=async(s,e)=>{if(FontLibrary.Has(name)){Profile.Font=name;Save();Page(current);Report(name+" is now your widget font.");return;}chip.Content="Installing "+name+"…";chip.IsEnabled=false;try{string added=await FontLibrary.Download(path);Report(added+" installed. Click it again to use it for your widgets.");Page(current);}catch(Exception ex){chip.Content=name;chip.IsEnabled=true;Report("Couldn't install "+name+": "+ex.Message);}};
    pack.Children.Add(chip);}
   more.Children.Add(pack);
   // Windows 11 draws most of its UI with its own fonts, so there are two levels: the classic metrics (title bars,
   // menus) and a system-wide "Segoe UI" substitution that most apps follow after signing out.
   string swapped=SystemFont.Current;
   var system=Card("Windows font","Pick a font, then choose how far it goes. Today: "+(swapped!=null?"Windows apps use "+swapped+".":"Windows apps use Segoe UI (the default)."));
   string family=swapped??"Segoe UI";int points=10;var classic=new TextBlock{Text="File   Edit   View   Help   ·   The quick brown fox",FontSize=points*96.0/72,Foreground=Brush(Ui.Ink),FontFamily=FontLibrary.For(family),Margin=new Thickness(16,10,16,10)};system.Children.Add(new Border{Background=Brush(Ui.Surface),BorderBrush=Brush(Ui.Line),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(8),Margin=new Thickness(0,4,0,8),Child=classic});
   system.Children.Add(Fonts(family,f=>{family=f;classic.FontFamily=FontLibrary.For(f);}));
   var whole=Text("Most Windows apps  ·  File Explorer, Notepad, dialogs, most desktop apps",14,Ui.Ink);whole.FontWeight=FontWeights.SemiBold;whole.Margin=new Thickness(0,8,0,2);system.Children.Add(whole);
   system.Children.Add(Text("Needs your administrator approval and a sign-out to show up. A backup is saved first. Start, Settings, and the taskbar on Windows 11 keep their own font.",12,Ui.Soft));
   var wholeActions=new WrapPanel();
   wholeActions.Children.Add(Primary("Use this font in Windows",()=>{
    if(MessageBox.Show(this,"Change the font of most Windows apps to "+family+"?\n\nWindows will ask for administrator approval. Sign out and back in to see it. You can restore the original font here anytime.",Brand.Name,MessageBoxButton.OKCancel,MessageBoxImage.Question)!=MessageBoxResult.OK)return;
    SystemFont.Request(folder,"apply",family);Report("Done. Sign out and back in to see "+family+" across Windows. Restore is on this page.");Page(current);}));
   var restoreWhole=Button("Restore the Windows font",()=>{SystemFont.Request(folder,"restore",null);Report("Windows font restored. Sign out and back in to see the original.");Page(current);});restoreWhole.IsEnabled=File.Exists(SystemFont.BackupPath(folder));wholeActions.Children.Add(restoreWhole);system.Children.Add(wholeActions);
   var titles=Text("Title bars and menus only",14,Ui.Ink);titles.FontWeight=FontWeights.SemiBold;titles.Margin=new Thickness(0,14,0,2);system.Children.Add(titles);
   system.Children.Add(Text("Classic title bars, menus, and message boxes, right away and without admin. Windows 11 shows these in fewer places.",12,Ui.Soft));
   Slider(system,"Size in points",points,8,20,v=>{points=(int)v;classic.FontSize=points*96.0/72;});
   var actions=new WrapPanel();actions.Children.Add(Button("Apply to title bars and menus",()=>{customization.ApplyFonts(family,points);Report("Title bar and menu fonts applied. Restore is available here.");Page(current);}));
   var undo=Button("Restore title bars and menus",()=>{customization.UndoFonts();Report("Previous title bar and menu fonts restored.");Page(current);});undo.IsEnabled=customization.FontUndo;actions.Children.Add(undo);system.Children.Add(actions);
  }
  string ProfilesFolder{get{return Path.Combine(folder,"profiles");}}
  string TaskbarFlag{get{return Path.Combine(folder,"taskbar-hidden-by-windotra");}}
  // Hides the Windows taskbar while the Dock is on, and puts it back only if WinDotra was the one that hid it.
  void TaskbarForDock(bool dock){try{if(dock&&!OverlayManager.TaskbarAutoHide){OverlayManager.TaskbarAutoHide=true;File.WriteAllText(TaskbarFlag,"");}else if(!dock&&File.Exists(TaskbarFlag)){OverlayManager.TaskbarAutoHide=false;File.Delete(TaskbarFlag);}}catch(Exception e){Report("Couldn't change the taskbar: "+e.Message);}}
  static string MacCursors(bool white){return Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"cursors",white?"macos-white":"macos");}
  // The Apple style goes all the way: macOS cursors, Inter for title bars and menus, and the taskbar out of the way.
  string AppleExtras(bool dark) {
   var done=new List<string>();
   TaskbarForDock(true);done.Add("taskbar hidden");
   try{if(Directory.Exists(MacCursors(dark))){customization.ApplyCursors(ReadPack(MacCursors(dark)));done.Add(dark?"white macOS cursors":"macOS cursors");}}catch(Exception e){Report("Cursors: "+e.Message);}
   try{string inter=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"fonts","Inter-Regular.ttf");if(!FontLibrary.Has("Inter")&&File.Exists(inter))FontLibrary.Install(inter);if(FontLibrary.Has("Inter")){customization.ApplyFonts("Inter",9);done.Add("Inter font");}}catch(Exception e){Report("Font: "+e.Message);}
   return String.Join(", ",done);
  }
  // The panic button: everything WinDotra changed goes back to how Windows was, and its desktop turns off.
  public void BackToWindows() {
   try{Directory.CreateDirectory(ProfilesFolder);JsonFile.Save(Path.Combine(ProfilesFolder,"Before going back to Windows.json"),Profile);}catch(Exception){}
   Overlays.Stop();var notes=new List<string>();
   try{if(customization.CursorUndo){customization.UndoCursors();notes.Add("cursors");}}catch(Exception e){notes.Add("cursors failed ("+e.Message+")");}
   try{if(customization.FontUndo){customization.UndoFonts();notes.Add("fonts");}}catch(Exception e){notes.Add("fonts failed ("+e.Message+")");}
   TaskbarForDock(false);notes.Add("taskbar");
   try{if(File.Exists(WallpaperUndoPath)){RestoreWindowsWallpaper();notes.Add("wallpaper");}}catch(Exception){}
   Profile.Off=true;try{Save();}catch(Exception){}
   if(current!=null)Page(current);
   Report("Back to normal Windows: "+String.Join(", ",notes)+" restored, WinDotra's desktop is off."+(SystemFont.Current!=null?" Your whole-Windows font is still "+SystemFont.Current+": restore it on the Typography page.":"")+" Your setup is saved as “Before going back to Windows” in Profiles.");
  }
  void Profiles() {
   Header("Profiles","Save your whole desktop as a file on this PC, switch between saved desktops, and share them.");
   var save=Card("Save this desktop","Saves the look, widgets and where they sit, the island, menu bar, Dock, wallpaper, notes, and tasks. AI settings and chats are never included.");
   var name=new TextBox{Text="My desktop "+DateTime.Now.ToString("yyyy-MM-dd HH.mm"),MaxLength=60,Margin=new Thickness(0,4,0,6)};save.Children.Add(Text("Name",12,Ui.Faint));save.Children.Add(name);
   var actions=new WrapPanel();
   actions.Children.Add(Primary("Save",()=>{Save();Directory.CreateDirectory(ProfilesFolder);string clean=Safe(String.IsNullOrWhiteSpace(name.Text)?"My desktop":name.Text.Trim());string file=Path.Combine(ProfilesFolder,clean+".json");JsonFile.Save(file,Profile);Page(current);Report("Saved “"+clean+"” on this PC. It's in the list below.");}));
   actions.Children.Add(Button("Export…",()=>{Profile.Validate();var dialog=new Microsoft.Win32.SaveFileDialog{Filter="WinDotra profile|*.json",FileName=Safe(name.Text.Trim())+".json"};if(dialog.ShowDialog(this)==true){JsonFile.Save(dialog.FileName,Profile);Report("Profile exported. Notes and tasks are included.");}}));
   actions.Children.Add(Button("Import…",()=>{var dialog=new Microsoft.Win32.OpenFileDialog{Filter="WinDotra profile|*.json"};if(dialog.ShowDialog(this)!=true)return;var imported=LoadProfile(dialog.FileName);Directory.CreateDirectory(ProfilesFolder);File.Copy(dialog.FileName,Path.Combine(ProfilesFolder,Path.GetFileName(dialog.FileName)),true);UseProfile(imported);Report("Imported and switched to "+Path.GetFileNameWithoutExtension(dialog.FileName)+". It's saved in the list below too.");}));
   actions.Children.Add(Ghost("Open profiles folder",()=>{Directory.CreateDirectory(ProfilesFolder);System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(ProfilesFolder){UseShellExecute=true});}));
   save.Children.Add(actions);
   var files=Directory.Exists(ProfilesFolder)?new DirectoryInfo(ProfilesFolder).GetFiles("*.json").OrderByDescending(f=>f.LastWriteTime).ToList():new List<FileInfo>();
   var list=Card("Saved on this PC",files.Count==0?"Nothing saved yet. Click Save above.":files.Count+" saved desktop"+(files.Count==1?"":"s")+"  ·  "+ProfilesFolder);
   for(int i=0;i<files.Count;i++){
    var file=files[i];if(i>0)Divider(list);
    var row=new Grid{Margin=new Thickness(0,6,0,6)};row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(40)});row.ColumnDefinitions.Add(new ColumnDefinition());row.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
    row.Children.Add(IconTile("\uE8A5","Profiles",26));
    string summary;try{var p=JsonFile.Read<DesktopProfile>(file.FullName);summary=p.Scene+" wallpaper  ·  "+p.Font+(p.MenuBar?"  ·  menu bar":"")+(p.Dock?"  ·  Dock":"")+"  ·  "+p.Tasks.Count+" tasks";}catch(Exception){summary="Can't be read";}
    var text=new StackPanel{VerticalAlignment=VerticalAlignment.Center};text.Children.Add(new TextBlock{Text=Path.GetFileNameWithoutExtension(file.Name),FontWeight=FontWeights.SemiBold,TextTrimming=TextTrimming.CharacterEllipsis});text.Children.Add(new TextBlock{Text=file.LastWriteTime.ToString("g")+"  ·  "+summary,FontSize=11.5,Foreground=Brush(Ui.Faint),TextTrimming=TextTrimming.CharacterEllipsis});Grid.SetColumn(text,1);row.Children.Add(text);
    var tools=new StackPanel{Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center};Grid.SetColumn(tools,2);
    var use=Primary("Use",()=>{UseProfile(LoadProfile(file.FullName));Report("Switched to “"+Path.GetFileNameWithoutExtension(file.Name)+"”.");});use.Margin=new Thickness(0,0,6,0);use.Padding=new Thickness(14,5,14,5);tools.Children.Add(use);
    tools.Children.Add(Ghost("Update",()=>{Save();JsonFile.Save(file.FullName,Profile);Page(current);Report("“"+Path.GetFileNameWithoutExtension(file.Name)+"” now matches your current desktop.");}));
    tools.Children.Add(Ghost("Show",()=>System.Diagnostics.Process.Start("explorer.exe","/select,\""+file.FullName+"\"")));
    tools.Children.Add(Ghost("Delete",()=>{if(MessageBox.Show(this,"Move “"+Path.GetFileNameWithoutExtension(file.Name)+"” to the Recycle Bin?",Brand.Name,MessageBoxButton.OKCancel,MessageBoxImage.Question)!=MessageBoxResult.OK)return;Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(file.FullName,Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);Page(current);Report("Moved to the Recycle Bin.");}));
    row.Children.Add(tools);list.Children.Add(row);
   }
   var reset=Card("Start over","Go back to WinDotra's defaults. Your saved desktops above stay.");
   reset.Children.Add(Button("Reset to defaults",()=>{Overlays.Stop();Profile=new DesktopProfile();Overlays.Profile=Profile;Overlays.Focus.Reset(Profile.FocusMinutes);Page(current);Report("Defaults loaded as a draft. Save or launch when ready.");}));
   reset.Children.Add(Text("Exports include notes and tasks, so check them before sharing.",12,Ui.Faint));
  }
  DesktopProfile LoadProfile(string file){var p=JsonFile.Read<DesktopProfile>(file);if(p==null)throw new InvalidDataException("That profile is empty.");p.Validate();return p;}
  void UseProfile(DesktopProfile p){Overlays.Stop();Profile=p;Overlays.Profile=Profile;Overlays.Focus.Reset(Profile.FocusMinutes);Launch();Page(current);}
  public static void Capture(Window window,string path) {
   window.UpdateLayout();var content=(FrameworkElement)window.Content;var visual=new DrawingVisual();
   int width=(int)Math.Ceiling(content.ActualWidth+content.Margin.Left+content.Margin.Right),height=(int)Math.Ceiling(content.ActualHeight+content.Margin.Top+content.Margin.Bottom);
   using(var dc=visual.RenderOpen()){dc.DrawRectangle(window.Background,null,new Rect(0,0,width,height));dc.DrawRectangle(new VisualBrush(content){ViewboxUnits=BrushMappingMode.Absolute,Viewbox=new Rect(content.Margin.Left,content.Margin.Top,Math.Max(1,content.ActualWidth),Math.Max(1,content.ActualHeight))},null,new Rect(content.Margin.Left,content.Margin.Top,Math.Max(1,content.ActualWidth),Math.Max(1,content.ActualHeight)));}
   var bitmap=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);bitmap.Render(visual);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var stream=File.Create(path))encoder.Save(stream);
  }
 }
}
