using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WinCanvas {
 public class Program {
  Window window; string first="#332B66", second="#101D38", mood="Dusk";
  readonly string data=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"WinCanvas"); // data folder keeps its original name so existing settings and backups still work
  SettingsEngine engine; bool loading=true;
  T Get<T>(string name) where T:class { return window.FindName(name) as T; }
  [STAThread] public static int Main(string[] args) {
   if(args.Length>0 && args[0]=="--self-test") return Tests.Run();
   // Writes the built-in styles as files (used for the repository's styles folder).
   if(args.Length>1 && args[0]=="--export-styles") { Directory.CreateDirectory(args[1]); foreach(var style in Styles.BuiltIn()) Styles.Save(style,Path.Combine(args[1],style.Name.Replace(" ","-").ToLowerInvariant()+Styles.Extension)); return 0; }
   // Elevated helper for the whole-Windows font swap (started by SystemFont.Request with the admin prompt).
   if(args.Length>=3 && args[0]=="--system-font") return SystemFont.Run(args[1],args[2],args.Length>3?args[3]:"");
   if(args.Length>0 && args[0]=="--media-check") {
    // Reads the current media session for a few seconds and reports what the island would see (no titles are written).
    new Application(); var media=new MediaWatcher(); var meter=new AudioMeter(); double loudest=0; int ticks=0; var frame=new System.Windows.Threading.DispatcherFrame();
    var poll=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromMilliseconds(500)}; poll.Tick+=(s,e)=>{media.Refresh();loudest=Math.Max(loudest,meter.Peak());if(++ticks>=10){poll.Stop();frame.Continue=false;}}; poll.Start(); System.Windows.Threading.Dispatcher.PushFrame(frame);
    var m=media.State; string report="has="+m.Has+" playing="+m.Playing+" app="+m.App+" art="+(m.Art!=null)+" duration="+m.Duration+" position="+m.Position+" peak="+loudest.ToString("0.00");
    File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"media-check.txt"),report); return 0;
   }
   // Asks ChatGPT (Codex CLI) one question through the same code the island uses.
   if(args.Length>0 && args[0]=="--codex-check") { string result; try { var exe=Codex.Find(); if(exe==null) throw new Exception("codex not found"); result=Codex.Send(exe,new AiConfig(),new System.Collections.Generic.List<ChatMessage>{new ChatMessage{role="user",content="Reply with exactly: WINDOTRA CHATGPT OK"}},new Progress<string>(_=>{}),System.Threading.CancellationToken.None).Result; } catch(Exception e) { result="FAIL "+(e.InnerException??e).Message; } File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"codex-check.txt"),result); return result.StartsWith("FAIL")?1:0; }
   if(args.Length>3 && args[0]=="--scene-test") { LiveWallpaper.RenderTest(args[1],args[2]=="dark",960,540,args[3]); return 0; }
   if(args.Length>0 && args[0]=="--wallpaper-check") {
    if(args.Length>5)LiveWallpaper.StretchMode=int.Parse(args[5]);if(args.Length>6&&args[6].Length>0)LiveWallpaper.TestPhoto=args[6];
    new Application(); var live=new LiveWallpaper(); string result;
    try { result=live.Check(args.Length>1?args[1]:"Drift",args.Length>3?int.Parse(args[3]):30,args.Length>4?double.Parse(args[4],System.Globalization.CultureInfo.InvariantCulture):0.5,args.Length>7?args[7]:"Each",args.Length>8?args[8]:"Fill",args.Length>9?args[9]:""); var frame=new System.Windows.Threading.DispatcherFrame(); var wait=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromSeconds(args.Length>2?int.Parse(args[2]):3)}; wait.Tick+=(s,e)=>{wait.Stop();frame.Continue=false;}; wait.Start(); System.Windows.Threading.Dispatcher.PushFrame(frame); result+=" "+live.Speed(); live.Snapshot(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"wallpaper-check.png")); }
    catch(Exception e) { result="FAIL "+e.Message; } finally { live.Stop(); }
    File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"wallpaper-check.txt"),result); return result.StartsWith("FAIL")?1:0;
   }
   bool created;
   using(var mutex=new Mutex(true,"Local\\WinDotra.Desktop",out created)) {
    // A second launch just asks the running copy to show its window.
    if(!created) { try { using(var show=EventWaitHandle.OpenExisting("Local\\WinDotra.Show")) show.Set(); } catch(Exception) { } return 0; }
    try {
     var app=new Application(); var program=new Program(); program.Load();
     if(args.Length>0 && args[0]=="--smoke-test") {
      program.window.Show(); program.window.UpdateLayout();
      program.Preset("Ocean","#184B63","#091F35",false);
      program.Preset("Daylight","#F3CDA2","#ACBADB",true);
      program.Preset("Dusk","#332B66","#101D38",false);
      var surface=(System.Windows.FrameworkElement)program.window.Content;
      var bmp=new RenderTargetBitmap((int)surface.ActualWidth+60,(int)surface.ActualHeight+60,96,96,PixelFormats.Pbgra32);
      var drawing=new DrawingVisual();
      using(var context=drawing.RenderOpen()) { context.DrawRectangle(program.window.Background,null,new Rect(0,0,bmp.PixelWidth,bmp.PixelHeight)); context.DrawRectangle(new VisualBrush(surface),null,new Rect(30,30,surface.ActualWidth,surface.ActualHeight)); }
      bmp.Render(drawing); var encoder=new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bmp));
      using(var stream=File.Create(args.Length>1?args[1]:"preview.png")) encoder.Save(stream);
      program.window.Close(); return 0;
     }
     bool studioSmoke=args.Length>0 && args[0]=="--studio-smoke";
     string studioFolder=studioSmoke?Path.Combine(Path.GetTempPath(),"WinDotra-smoke-"+Guid.NewGuid().ToString("N")):program.data;
     var studio=new Studio(studioFolder,program.window.Resources,()=>{program.window.Show();program.window.Activate();},studioSmoke);
     program.window.Closing+=(s,e)=>{e.Cancel=true;program.window.Hide();};
     app.ShutdownMode=ShutdownMode.OnMainWindowClose; app.MainWindow=studio;
     if(studioSmoke) {
      studio.Show();
      string target=args.Length>1?args[1]:"studio-preview.png";
      Studio.Capture(studio,target);
      foreach(string page in new[]{"Styles","AI","Island","Menu bar & Dock","Widgets","Live wallpaper","Typography","Cursors","Store","Setup"}) {studio.Page(page);studio.UpdateLayout();Studio.Capture(studio,Path.Combine(Path.GetDirectoryName(Path.GetFullPath(target)),"page-"+page.Replace(" & ","-").Replace(" ","-").ToLowerInvariant()+".png"));}
      studio.Profile.Note=true;studio.Profile.Focus=true;studio.Profile.Calendar=true;studio.Profile.Music=true;studio.Profile.Reminders=true;studio.Profile.Tasks.Add(new ChecklistItem{Text="Water the plants"});studio.Profile.Tasks.Add(new ChecklistItem{Text="Call Sam back"});studio.Overlays.Start();
      foreach(var overlay in studio.Overlays.Windows) {overlay.UpdateLayout();Studio.Capture(overlay,Path.Combine(Path.GetDirectoryName(Path.GetFullPath(target)),"overlay-"+studio.Overlays.Windows.IndexOf(overlay)+".png"));if(overlay.ActualWidth<100||overlay.ActualHeight<30)throw new Exception("Overlay layout failed.");}
      studio.Overlays.SetEditing(true);var edited=studio.Overlays.Windows[0];edited.UpdateLayout();Studio.Capture(edited,Path.Combine(Path.GetDirectoryName(Path.GetFullPath(target)),"overlay-edit.png"));studio.Overlays.SetEditing(false);
      var island=studio.Overlays.Windows[studio.Overlays.Windows.Count-1];
      studio.Overlays.OpenAssistant();
      var frame=new System.Windows.Threading.DispatcherFrame();
      var delay=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromMilliseconds(1100)};
      delay.Tick+=(s,e)=>{delay.Stop();frame.Continue=false;};delay.Start();System.Windows.Threading.Dispatcher.PushFrame(frame);
      island.UpdateLayout();if(island.ActualHeight<150)throw new Exception("Island expansion failed.");
      Studio.Capture(island,Path.Combine(Path.GetDirectoryName(Path.GetFullPath(target)),"island-expanded.png"));
      var native=new NativeCustomization();if(String.IsNullOrEmpty(native.ReadFonts().Menu.Face)||native.ReadCursors().Count!=14)throw new Exception("Native read-only integration failed.");
      // The macOS-style menu bar and Dock (without reserving screen space during the test).
      studio.Profile.MenuBar=true;studio.Profile.MenuBarReserve=false;studio.Profile.Dock=true;studio.Profile.DockApps=OverlayManager.DockDefaults();studio.Profile.IslandShape="Notch";studio.Overlays.Start();
      var bar=studio.Overlays.MenuBarWindow;var dockWin=studio.Overlays.DockWindow;if(bar==null||dockWin==null)throw new Exception("Menu bar or Dock failed to start.");
      bar.UpdateLayout();dockWin.UpdateLayout();Studio.Capture(bar,Path.Combine(Path.GetDirectoryName(Path.GetFullPath(target)),"menubar.png"));Studio.Capture(dockWin,Path.Combine(Path.GetDirectoryName(Path.GetFullPath(target)),"dock.png"));
      if(dockWin.ActualWidth<200)throw new Exception("Dock layout failed.");
      studio.Overlays.ToggleControlCenter(null);var cc=studio.Overlays.ControlCenterWindow;if(cc==null)throw new Exception("Control Center failed to open.");cc.UpdateLayout();Studio.Capture(cc,Path.Combine(Path.GetDirectoryName(Path.GetFullPath(target)),"control-center.png"));studio.Overlays.ToggleControlCenter(null);
      foreach(var style in Styles.BuiltIn()){var applied=Styles.Apply(studio.Profile,style);if(applied==null)throw new Exception("Style failed: "+style.Name);}
      studio.Overlays.Stop();AiTests.SmokeUi(studioFolder,studio.Resources);studio.Close();if(Directory.Exists(studioFolder))Directory.Delete(studioFolder,true);return 0;
     }
     bool background=args.Length>0 && args[0]=="--background";
     using(var show=new EventWaitHandle(false,EventResetMode.AutoReset,"Local\\WinDotra.Show")) {
      ThreadPool.RegisterWaitForSingleObject(show,(state,timeout)=>studio.Dispatcher.BeginInvoke(new Action(()=>{studio.Show();if(studio.WindowState==WindowState.Minimized)studio.WindowState=WindowState.Normal;studio.Activate();})),null,-1,false);
      // WinCanvas starts your desktop right away; --background (used at sign-in) skips the window.
      studio.Loaded+=(s,e)=>studio.LaunchQuietly();
      if(background&&!studio.NeedsSetup) { studio.LaunchQuietly(); return app.Run(); }
      return app.Run(studio);
     }
    } catch(Exception e) { if(args.Length>0)File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"smoke-error.txt"),e.ToString());else MessageBox.Show(e.Message,"WinDotra",MessageBoxButton.OK,MessageBoxImage.Error); return 1; }
   }
  }
  void Load() {
   using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("MainWindow.xaml")) window=(Window)XamlReader.Load(stream);window.Icon=Brand.Logo;
   var desktop=new WindowsDesktop(); engine=new SettingsEngine(desktop,Path.Combine(data,"undo.json"));
   Get<ComboBox>("AppMode").SelectedIndex=desktop.Read(SettingsEngine.Names[0]).Value==0 ? 0:1;
   Get<ComboBox>("SystemMode").SelectedIndex=desktop.Read(SettingsEngine.Names[1]).Value==0 ? 0:1;
   Get<CheckBox>("Transparency").IsChecked=desktop.Read(SettingsEngine.Names[2]).Value!=0;
   Get<Button>("Dusk").Click+=(s,e)=>Preset("Dusk","#332B66","#101D38",false);
   Get<Button>("Ocean").Click+=(s,e)=>Preset("Ocean","#184B63","#091F35",false);
   Get<Button>("Forest").Click+=(s,e)=>Preset("Forest","#345C49","#102E31",false);
   Get<Button>("Daylight").Click+=(s,e)=>Preset("Daylight","#F3CDA2","#ACBADB",true);
   Get<Button>("Ember").Click+=(s,e)=>Preset("Ember","#7A3B2E","#1E1225",false);
   Get<Button>("Rose").Click+=(s,e)=>Preset("Rose","#7B3F66","#22183A",false);
   Get<Button>("Aurora").Click+=(s,e)=>Preset("Aurora","#1F6B62","#1C1E4A",false);
   Get<Button>("Graphite").Click+=(s,e)=>Preset("Graphite","#3A3F4B","#121419",false);
   Get<Button>("CustomColor").Click+=(s,e)=>Colors();
   Get<Button>("Apply").Click+=(s,e)=>Apply();
   Get<Button>("Undo").Click+=(s,e)=>Undo();
   Get<Button>("TaskbarSettings").Click+=(s,e)=>OpenSettings("ms-settings:taskbar");
   Get<Button>("ColorSettings").Click+=(s,e)=>OpenSettings("ms-settings:colors");
   Get<ComboBox>("AppMode").SelectionChanged+=(s,e)=>Preview();
   Get<ComboBox>("SystemMode").SelectionChanged+=(s,e)=>Preview();
   Get<CheckBox>("Transparency").Click+=(s,e)=>Preview();
   Get<Button>("Undo").IsEnabled=engine.CanUndo;
   loading=false; Preview();
  }
  void OpenSettings(string uri) { try { Process.Start(new ProcessStartInfo(uri) { UseShellExecute=true }); } catch(Exception e) { Error(e); } }
  void Preset(string name,string a,string b,bool light) {
   mood=name; first=a; second=b; Get<ComboBox>("AppMode").SelectedIndex=light?1:0;
   Get<ComboBox>("SystemMode").SelectedIndex=light?1:0; Preview();
   Get<TextBlock>("Status").Text=name+" selected. Apply when you're ready.";
  }
  static Color ColorOf(string hex) { return (Color)ColorConverter.ConvertFromString(hex); }
  static SolidColorBrush Brush(string hex) { return new SolidColorBrush(ColorOf(hex)); }
  void Preview() {
   if(loading) return;
   Get<Grid>("Preview").Background=new LinearGradientBrush(ColorOf(first),ColorOf(second),45);
   bool light=Get<ComboBox>("AppMode").SelectedIndex==1;
   Get<Border>("MockWindow").Background=Brush(light?"#F2F5F8":"#E61C202B");
   Get<TextBlock>("MockTitle").Foreground=Brush(light?"#202635":"#F3F4F8");
   Get<TextBlock>("MockBody").Foreground=Brush(light?"#536078":"#AAB5CB");
   bool systemLight=Get<ComboBox>("SystemMode").SelectedIndex==1;
   var taskbar=Get<Border>("MockTaskbar"); taskbar.Background=Brush(systemLight?"#E3E6F0":"#141821");
   taskbar.Opacity=Get<CheckBox>("Transparency").IsChecked==true?0.85:1;
   Get<TextBlock>("PaletteLabel").Text=mood+" · "+first+" → "+second;
  }
  void Colors() {
   using(var dialog=new System.Windows.Forms.ColorDialog()) {
    dialog.FullOpen=true; dialog.Color=System.Drawing.ColorTranslator.FromHtml(first);
    if(dialog.ShowDialog()!=System.Windows.Forms.DialogResult.OK) return;
    string a=System.Drawing.ColorTranslator.ToHtml(dialog.Color);
    dialog.Color=System.Drawing.ColorTranslator.FromHtml(second);
    if(dialog.ShowDialog()!=System.Windows.Forms.DialogResult.OK) return;
    first=a; second=System.Drawing.ColorTranslator.ToHtml(dialog.Color); mood="Custom"; Preview();
    Get<TextBlock>("Status").Text="Custom wallpaper colors selected. Ready to apply.";
   }
  }
  string CreateWallpaper() {
   Directory.CreateDirectory(data);
   var visual=new DrawingVisual();
   using(var draw=visual.RenderOpen()) {
    draw.DrawRectangle(new LinearGradientBrush(ColorOf(first),ColorOf(second),45),null,new Rect(0,0,3840,2160));
    draw.DrawEllipse(Brush("#12FFFFFF"),null,new Point(3200,200),1100,1100);
    draw.DrawEllipse(null,new Pen(Brush("#15FFFFFF"),180),new Point(200,2050),900,900);
   }
   var bitmap=new RenderTargetBitmap(3840,2160,96,96,PixelFormats.Pbgra32); bitmap.Render(visual);
   var encoder=new BmpBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
   // Unique names preserve the previous wallpaper for undo, even after repeated applies.
   string file=Path.Combine(data,"wallpaper-"+Guid.NewGuid().ToString("N")+".bmp");
   using(var stream=File.Create(file)) encoder.Save(stream);
   return file;
  }
  void Apply() {
   string wallpaper=null;
   try {
    if(Get<CheckBox>("Wallpaper").IsChecked==true) wallpaper=CreateWallpaper();
    engine.Apply(Get<ComboBox>("AppMode").SelectedIndex==1,Get<ComboBox>("SystemMode").SelectedIndex==1,Get<CheckBox>("Transparency").IsChecked==true,wallpaper);
    Get<TextBlock>("Status").Text="Applied! Some Windows surfaces may need sign-out to refresh. Undo is ready.";
   } catch(Exception e) { Error(e); }
   Get<Button>("Undo").IsEnabled=engine.CanUndo;
  }
  void Undo() {
   try {
    engine.Undo();
    var desktop=new WindowsDesktop();
    Get<ComboBox>("AppMode").SelectedIndex=desktop.Read(SettingsEngine.Names[0]).Value==0?0:1;
    Get<ComboBox>("SystemMode").SelectedIndex=desktop.Read(SettingsEngine.Names[1]).Value==0?0:1;
    Get<CheckBox>("Transparency").IsChecked=desktop.Read(SettingsEngine.Names[2]).Value!=0;
    Preview(); Get<TextBlock>("Status").Text="Previous Windows settings restored. Wallpaper preview still shows your draft.";
   } catch(Exception e) { Error(e); }
   Get<Button>("Undo").IsEnabled=engine.CanUndo;
  }
  void Error(Exception e) { Get<TextBlock>("Status").Text=e.Message; MessageBox.Show(window,e.Message,"WinDotra",MessageBoxButton.OK,MessageBoxImage.Warning); }
 }
}
