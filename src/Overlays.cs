using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace WinCanvas {
 public class FocusClock {
  public TimeSpan Remaining; public bool Running; DateTime deadline;
  public FocusClock(int minutes) { Reset(minutes); }
  public void Reset(int minutes) { Running=false; Remaining=TimeSpan.FromMinutes(minutes); }
  public void Toggle(DateTime now) { if(Running) { Tick(now); Running=false; } else { if(Remaining<=TimeSpan.Zero) return; deadline=now+Remaining; Running=true; } }
  public void Tick(DateTime now) { if(!Running) return; Remaining=deadline-now; if(Remaining<=TimeSpan.Zero) { Remaining=TimeSpan.Zero; Running=false; } }
  public string Text { get { return ((int)Remaining.TotalMinutes).ToString("00")+":"+Remaining.Seconds.ToString("00"); } }
 }
 public class SystemStats {
  [StructLayout(LayoutKind.Sequential)] struct Memory { public uint Length,Load; public ulong TotalPhysical,AvailablePhysical,TotalPage,AvailablePage,TotalVirtual,AvailableVirtual,Extended; }
  [DllImport("kernel32.dll")] static extern bool GlobalMemoryStatusEx(ref Memory status);
  [DllImport("kernel32.dll")] static extern bool GetSystemTimes(out ulong idle,out ulong kernel,out ulong user);
  ulong lastIdle,lastTotal; bool sampled;
  public string Cpu="—",Ram="—",Battery="—"; public double CpuLoad=-1,RamLoad=-1,BatteryLevel=-1;
  public void Sample() {
   ulong idle,kernel,user;
   if(GetSystemTimes(out idle,out kernel,out user)) {
    ulong total=kernel+user;
    if(sampled && total>lastTotal && idle>=lastIdle) { CpuLoad=Math.Max(0,Math.Min(100,100.0*(1-(double)(idle-lastIdle)/(total-lastTotal)))); Cpu=CpuLoad.ToString("0")+"%"; }
    lastIdle=idle; lastTotal=total; sampled=true;
   } else Cpu="unavailable";
   var memory=new Memory(); memory.Length=(uint)Marshal.SizeOf(typeof(Memory));
   bool hasMemory=GlobalMemoryStatusEx(ref memory); RamLoad=hasMemory?(double)memory.Load:-1; Ram=hasMemory?memory.Load+"%":"unavailable";
   var power=System.Windows.Forms.SystemInformation.PowerStatus;
   BatteryLevel=(power.BatteryChargeStatus & System.Windows.Forms.BatteryChargeStatus.NoSystemBattery)!=0||power.BatteryLifePercent<0||power.BatteryLifePercent>1?-1:power.BatteryLifePercent*100;
   Battery=(power.BatteryChargeStatus & System.Windows.Forms.BatteryChargeStatus.NoSystemBattery)!=0?"AC power":power.BatteryLifePercent<0||power.BatteryLifePercent>1?"Power unknown":Math.Round(power.BatteryLifePercent*100)+"% battery";
  }
 }
 public partial class OverlayManager : IDisposable, IPcHost {
  readonly List<Window> windows=new List<Window>(); readonly List<Action> refresh=new List<Action>();
  readonly DispatcherTimer timer=new DispatcherTimer(); readonly Action save; readonly Action<string> report;
  public DesktopProfile Profile; public FocusClock Focus; readonly SystemStats stats=new SystemStats();
  public ChatSession Chat; public Action ConfigureAi; public bool DesktopAttach=true;
  readonly LiveWallpaper wallpaper=new LiveWallpaper();
  Window islandWindow; IslandChat islandChat; Action openAssistant; Palette p;
  readonly MediaWatcher media=new MediaWatcher(); readonly AudioMeter audio=new AudioMeter(); readonly DownloadWatcher downloads=new DownloadWatcher();
  readonly DispatcherTimer wave=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(60)};
  // Demo mode (smoke tests and screenshots) shows a sample track and download instead of the user's real ones.
  public bool Demo { set { media.Demo=value; downloads.Demo=value; } }
  Action renderTasks;
  // IPcHost: what AI actions may do once the user clicks Run.
  public void Media(string command) { if(command=="next")media.Next();else if(command=="previous")media.Previous();else if(!media.State.Has||(command=="play")!=media.State.Playing)media.PlayPause(); }
  public void StartFocus(int minutes) { Focus.Reset(minutes); Focus.Toggle(DateTime.UtcNow); }
  public void AddTask(string text) { if(Profile.Tasks.Count>=40)throw new InvalidOperationException("Your task list is full (40)."); Profile.Tasks.Add(new ChecklistItem{Text=text}); dirty=true; if(renderTasks!=null)renderTasks(); }
  // Apple-sized: the open island stays compact no matter what older settings asked for.
  double Wide { get { return Math.Max(340,Math.Min(Profile.IslandExpandedWidth,400)); } }
  bool dirty; public bool Active { get { return windows.Count>0; } } public bool WallpaperRunning { get { return wallpaper.Running; } }
  public OverlayManager(DesktopProfile profile,Action save,Action<string> report) {
   Profile=profile; this.save=save; this.report=report; Focus=new FocusClock(profile.FocusMinutes);
   timer.Interval=TimeSpan.FromSeconds(1); timer.Tick+=(s,e)=>Tick();
  }
  static SolidColorBrush Brush(string value) { return Ui.Brush(value); }
  TextBlock Text(string value,double size,string color) { return new TextBlock{Text=value,FontSize=size,Foreground=Brush(color),TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,3,0,3)}; }
  // Primary actions are ink-on-paper (inverted on dark widgets); the accent is kept for progress and "today".
  Button Button(string title,Action action,Palette pal=null) {
   pal=pal??p;var b=new Button{Content=title,Padding=new Thickness(12,6,12,6),Margin=new Thickness(0,6,6,0),Background=Brush(pal.Ink),Foreground=Brush(pal.Back),BorderThickness=new Thickness(0),Cursor=Cursors.Hand};
   b.Click+=(s,e)=>{ try { action(); } catch(Exception ex) { report(ex.Message); } }; return b;
  }
  Button Quiet(string title,Action action,Palette pal=null) { pal=pal??p; var b=Button(title,action,pal); b.Background=Brush(pal.Chip); b.Foreground=Brush(pal.Ink); return b; }
  // A labelled meter row: caption and value on one line, a hairline bar underneath. Loads turn amber/red when high.
  Action<string,double> Meter(Panel parent,string caption,bool load=true,Palette pal=null) {
   pal=pal??p; var row=new Grid{Margin=new Thickness(0,8,0,0)}; var name=Text(caption,Profile.FontSize*0.8,pal.Soft); var value=Text("",Profile.FontSize*0.85,pal.Ink); value.HorizontalAlignment=HorizontalAlignment.Right; value.FontWeight=FontWeights.SemiBold; row.Children.Add(name); row.Children.Add(value); parent.Children.Add(row);
   var bar=new ProgressBar{Maximum=100,Foreground=Brush(Profile.Accent),Background=Brush(pal.Chip),Margin=new Thickness(0,3,0,2)}; parent.Children.Add(bar);
   return (text,percent)=>{ value.Text=text; bar.Value=percent<0?0:percent; bar.Foreground=Brush(load&&percent>=90?"#DC2626":load&&percent>=75?"#D97706":!load&&percent>=0&&percent<=15?"#DC2626":Profile.Accent); };
  }
  void Persist() { if(!dirty) return; try { save(); dirty=false; } catch(Exception e) { dirty=false; report("Could not save desktop layout: "+e.Message); } }
  void Tick() { bool wasRunning=Focus.Running; Focus.Tick(DateTime.UtcNow); if(wasRunning&&!Focus.Running&&Focus.Remaining==TimeSpan.Zero&&Profile.FocusSound)System.Media.SystemSounds.Asterisk.Play(); stats.Sample(); if((islandWindow!=null&&Profile.IslandMedia)||musicShown) media.Refresh(); if(islandWindow!=null&&Profile.IslandDownloads) downloads.Poll(); foreach(var action in refresh) action(); Persist(); }
  public void Start() {
   Stop(); Profile.Validate(); p=new Palette(Profile.Background,Profile.Accent);
   if(Profile.Clock) Clock(); if(Profile.Stats) Stats(); if(Profile.Note) Notes(); if(Profile.Focus) TimerWidget(); if(Profile.Calendar) CalendarWidget(); if(Profile.Music) MusicWidget(); if(Profile.Reminders) RemindersWidget(); if(Profile.Day) DayWidget(); CustomWidgets(); if(Profile.Island) Island();
   if(Profile.LiveWallpaper&&DesktopAttach) { try { StartWallpaper(); } catch(Exception e) { report(e.Message); } }
   if(DesktopAttach||Profile.MenuBar||Profile.Dock||islandWindow!=null) { try { StartMacDesktop(); } catch(Exception e) { report(e.Message); } }
   Tick(); timer.Start();
  }
  public void StartWallpaper() { wallpaper.Report=report; wallpaper.Start(SceneOptions.From(Profile),Profile.SceneFps,Profile.SceneDetail); }
  public void StopWallpaper() { wallpaper.Stop(); }
  public string WallpaperStats() { return wallpaper.FramesPerSecond>0?"Running at "+wallpaper.FramesPerSecond.ToString("0")+" fps":"Starting…"; }
  public void Stop() { StopMacDesktop(); editHooks.Clear(); if(gridForm!=null){gridForm.Close();gridForm.Dispose();gridForm=null;} if(toolbar!=null){toolbar.Close();toolbar=null;} renderTasks=null; musicShown=false; timer.Stop(); wave.Stop(); wallpaper.Stop(); if(islandChat!=null)islandChat.Close();islandChat=null;islandWindow=null;openAssistant=null; Persist(); foreach(var w in windows) w.Close(); windows.Clear(); refresh.Clear(); }
  public void Dispose() { Stop(); }
  Window Shell(string id,string title,double width,double x,double y,StackPanel content,bool island) {
   var w=new Window{Title="WinDotra · "+title,Width=width,SizeToContent=SizeToContent.Height,WindowStyle=WindowStyle.None,ResizeMode=ResizeMode.NoResize,AllowsTransparency=true,Background=Brush("#00000000"),ShowInTaskbar=false,ShowActivated=false,Topmost=Profile.Topmost,FontFamily=FontLibrary.For(Profile.Font),FontSize=Profile.FontSize,Foreground=Brush(p.Ink),Opacity=Profile.Opacity};
   if(Application.Current!=null&&Application.Current.MainWindow!=null)w.Resources=Application.Current.MainWindow.Resources;
   if(island)w.Topmost=true; // the island always stays above other windows, like Apple's
   var panel=new StackPanel();
   var handle=Text(island?"WinDotra":title,Profile.FontSize*0.75,p.Soft);
   var menu=new ContextMenu(); var hide=new MenuItem{Header="Hide until next launch"}; hide.Click+=(s,e)=>w.Hide(); menu.Items.Add(hide); handle.ContextMenu=menu;
   panel.Children.Add(content);
   if(island) {
    var spectrum=new LinearGradientBrush(new GradientStopCollection{new GradientStop(Color.FromRgb(0x3B,0x82,0xF6),0),new GradientStop(Color.FromRgb(0x8B,0x5C,0xF6),0.3),new GradientStop(Color.FromRgb(0xEC,0x48,0x99),0.6),new GradientStop(Color.FromRgb(0xF9,0x73,0x16),0.85),new GradientStop(Color.FromRgb(0x22,0xD3,0xEE),1)},new Point(0,0.5),new Point(1,0.5));
    var turn=new RotateTransform(0,0.5,0.5);spectrum.RelativeTransform=turn;turn.BeginAnimation(RotateTransform.AngleProperty,new DoubleAnimation(0,360,TimeSpan.FromSeconds(8)){RepeatBehavior=RepeatBehavior.Forever});
    islandGlow=new Border{Background=spectrum,CornerRadius=IslandCorners(false),Margin=new Thickness(-3),Opacity=0,Effect=new System.Windows.Media.Effects.BlurEffect{Radius=14}};
    islandPill=new Border{Background=Brush(Profile.IslandColor),BorderBrush=Brush(Profile.IslandShape=="Notch"?"#00FFFFFF":"#14FFFFFF"),BorderThickness=new Thickness(1),CornerRadius=IslandCorners(false),Padding=new Thickness(8,7,10,7),Child=panel,Effect=new System.Windows.Media.Effects.DropShadowEffect{BlurRadius=18,ShadowDepth=3,Opacity=0.3,Color=Colors.Black}};
    // The window keeps a fixed, centered width; only the pill inside animates, so it grows from the middle.
    islandFrame=new Grid{HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Top};islandFrame.Children.Add(islandGlow);islandFrame.Children.Add(islandPill);
    var stack=new Grid{Margin=new Thickness(18),LayoutTransform=new ScaleTransform(Profile.IslandScale,Profile.IslandScale)};stack.Children.Add(islandFrame);w.Content=stack;w.Foreground=Brush(Night.Ink);w.FontFamily=Ui.SansFamily;
   } else if(id=="day") {
    // Rainmeter-style: bare text on the desktop, no card. Empty space stays click-through.
    w.Content=new Border{Background=Brushes.Transparent,Padding=new Thickness(12),Child=panel};w.Topmost=false;
   } else {
    // Apple-style tile drawn entirely by WPF, so corners are smooth on every Windows build: a slightly lighter top,
    // a hairline edge, and a soft shadow. "Glass" makes the tile translucent instead of solid.
    var baseColor=(Color)ColorConverter.ConvertFromString(Profile.Background);bool light=Ui.IsLight(Profile.Background);byte alpha=(byte)(Profile.Glass?0xC4:0xFF);
    Func<Color,double,Color> lift=(c,amount)=>Color.FromArgb(alpha,(byte)Math.Min(255,c.R+(255-c.R)*amount),(byte)Math.Min(255,c.G+(255-c.G)*amount),(byte)Math.Min(255,c.B+(255-c.B)*amount));
    var fill=new LinearGradientBrush(lift(baseColor,light?0.0:0.07),lift(baseColor,0),90);
    w.Content=new Border{Background=fill,BorderBrush=Brush(light?"#0D000000":"#14FFFFFF"),BorderThickness=new Thickness(0.8),CornerRadius=new CornerRadius(26),Padding=new Thickness(16),Margin=new Thickness(16),Child=panel,Effect=new System.Windows.Media.Effects.DropShadowEffect{BlurRadius=36,ShadowDepth=6,Direction=270,Opacity=light?0.12:0.32,Color=Colors.Black}};
   }
   if(!island&&id!="day") {
    // Like macOS widgets: no title bar. Moving, resizing, and recoloring only happen in Edit mode.
    var editItem=new MenuItem{Header="Edit widgets"};editItem.Click+=(s,e)=>SetEditing(!Editing);menu.Items.Insert(0,editItem);menu.Opened+=(s,e)=>editItem.Header=Editing?"Done editing":"Edit widgets";
    ((FrameworkElement)w.Content).ContextMenu=menu;AddEditChrome(w,id,(Border)w.Content,width);
    w.MouseLeftButtonDown+=(s,e)=>{if(!Editing||e.ButtonState!=MouseButtonState.Pressed||Interactive(e.OriginalSource as DependencyObject))return;w.DragMove();w.Left=Snap(w.Left);w.Top=Snap(w.Top);Profile.Positions[id]=new Position{X=w.Left,Y=w.Top};dirty=true;};
   }
   Position pos; if(Profile.Positions.TryGetValue(id,out pos)) { x=pos.X; y=pos.Y; }
   w.Left=x; w.Top=y; w.Show(); w.UpdateLayout();
   double shown=island?1:Profile.Opacity;w.Opacity=shown;w.BeginAnimation(UIElement.OpacityProperty,new DoubleAnimation(0,shown,TimeSpan.FromMilliseconds(240)){EasingFunction=new CubicEase()});
   // Recover an off-screen saved position when monitor configuration changes.
   var source=PresentationSource.FromVisual(w); var matrix=source.CompositionTarget.TransformToDevice;
   var pixels=matrix.Transform(new Point(w.Left,w.Top)); var dimensions=matrix.Transform(new Vector(w.ActualWidth,w.ActualHeight));
   bool visible=false; var rect=new System.Drawing.Rectangle((int)pixels.X,(int)pixels.Y,(int)dimensions.X,(int)dimensions.Y);
   foreach(var screen in System.Windows.Forms.Screen.AllScreens) { var overlap=System.Drawing.Rectangle.Intersect(island?screen.Bounds:screen.WorkingArea,rect); if(overlap.Width>=Math.Min(rect.Width,120)&&overlap.Height>=Math.Min(rect.Height,island?30:80)) visible=true; }
   if(!visible) { w.Left=SystemParameters.WorkArea.Left+24; w.Top=SystemParameters.WorkArea.Top+24; }
   windows.Add(w); return w;
  }
  // macOS-style widget tiles: small tiles are square, medium tiles are two small tiles wide. No title bars.
  double Unit {get{return Math.Round(Profile.WidgetWidth*0.62);}}
  double TileWidth(bool medium){return (medium?Unit*2+12:Unit)+32;}
  StackPanel Tile(bool medium){return new StackPanel{Height=Unit-30,Width=(medium?Unit*2+12:Unit)-32,ClipToBounds=false};}
  static bool Interactive(DependencyObject source){for(var o=source;o!=null;o=o is Visual||o is System.Windows.Media.Media3D.Visual3D?VisualTreeHelper.GetParent(o):LogicalTreeHelper.GetParent(o)){if(o is System.Windows.Controls.Primitives.ButtonBase||o is System.Windows.Controls.Primitives.TextBoxBase||o is Slider||o is System.Windows.Controls.Primitives.Thumb)return true;}return false;}
  TextBlock Label(string text,double size,string color,FontWeight weight){return new TextBlock{Text=text,FontSize=size,Foreground=Brush(color),FontWeight=weight,TextTrimming=TextTrimming.CharacterEllipsis};}
  // A progress ring; set(0..1) redraws the arc.
  Grid Ring(double size,double thickness,string color,out Action<double> set) {
   var grid=new Grid{Width=size,Height=size};grid.Children.Add(new Ellipse{Width=size,Height=size,Stroke=Brush(p.Chip),StrokeThickness=thickness});
   var arc=new Path{Stroke=Brush(color),StrokeThickness=thickness,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round};grid.Children.Add(arc);
   set=f=>{f=Math.Max(0,Math.Min(0.9999,f));double r=(size-thickness)/2,c=size/2,a=f*2*Math.PI;var fig=new PathFigure{StartPoint=new Point(c,c-r)};fig.Segments.Add(new ArcSegment(new Point(c+r*Math.Sin(a),c-r*Math.Cos(a)),new Size(r,r),0,f>0.5,SweepDirection.Clockwise,true));arc.Data=new PathGeometry(new[]{fig});arc.Visibility=f<0.005?Visibility.Hidden:Visibility.Visible;};
   return grid;
  }
  void Clock() {
   var tile=Tile(false);double size=Math.Min(tile.Width,tile.Height);var now=DateTime.Now;
   if(Profile.ClockAnalog) {
    // An analog face like the macOS clock widget, with the second hand in your accent color.
    var face=new Canvas{Width=size,Height=size,HorizontalAlignment=HorizontalAlignment.Center};double c=size/2;
    face.Children.Add(new Ellipse{Width=size,Height=size,Fill=Brush(Ui.IsLight(Profile.Background)?"#FFFFFF":"#1C1C1E"),Stroke=Brush(p.Line),StrokeThickness=1});
    for(int i=0;i<12;i++){if(i%3==0)continue;double a=i*Math.PI/6,outer=c-6,inner=c-11;face.Children.Add(new Line{X1=c+Math.Sin(a)*inner,Y1=c-Math.Cos(a)*inner,X2=c+Math.Sin(a)*outer,Y2=c-Math.Cos(a)*outer,Stroke=Brush(i%3==0?p.Ink:p.Soft),StrokeThickness=i%3==0?2.4:1.3,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round});}
    Func<double,double,string,RotateTransform> hand=(length,width,color)=>{var turn=new RotateTransform(0,c,c);face.Children.Add(new Line{X1=c,Y1=c+length*0.18,X2=c,Y2=c-length,Stroke=Brush(color),StrokeThickness=width,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round,RenderTransform=turn});return turn;};
    foreach(int n in new[]{12,3,6,9}){double a=n*Math.PI/6;var label=new TextBlock{Text=n.ToString(),FontSize=size*0.1,FontWeight=FontWeights.SemiBold,Foreground=Brush(p.Ink),FontFamily=Ui.SansFamily};label.Measure(new Size(size,size));Canvas.SetLeft(label,c+Math.Sin(a)*(c-26)-label.DesiredSize.Width/2);Canvas.SetTop(label,c-Math.Cos(a)*(c-26)-label.DesiredSize.Height/2);face.Children.Add(label);}
    var hours=hand(c*0.5,4,p.Ink);var minutes=hand(c*0.72,3,p.Ink);var seconds=hand(c*0.8,1.4,"#FF9F0A");
    face.Children.Add(new Ellipse{Width=7,Height=7,Fill=Brush("#FF9F0A"),Margin=new Thickness(c-3.5,c-3.5,0,0)});
    tile.Children.Add(face);
    refresh.Add(()=>{var t=DateTime.Now;hours.Angle=(t.Hour%12+t.Minute/60.0)*30;minutes.Angle=(t.Minute+t.Second/60.0)*6;seconds.Angle=t.Second*6;});
   } else {
    var day=Label("",Profile.FontSize*0.85,Profile.Accent,FontWeights.SemiBold);var time=Label("",size*0.36,p.Ink,FontWeights.Light);time.FontFamily=Ui.SansFamily;time.Margin=new Thickness(0,size*0.08,0,0);var date=Label("",Profile.FontSize*0.85,p.Soft,FontWeights.Normal);
    tile.Children.Add(day);tile.Children.Add(time);tile.Children.Add(date);
    refresh.Add(()=>{var t=DateTime.Now;day.Text=t.ToString("dddd").ToUpperInvariant();time.Text=t.ToString(Profile.Clock24?"HH:mm":"h:mm");date.Text=t.ToString("d MMMM");});
   }
   Shell("clock","Clock",TileWidth(false),30,100,tile,false);
  }
  void Stats() {
   var tile=Tile(true);tile.Children.Add(Label("System",Profile.FontSize*0.85,p.Soft,FontWeights.SemiBold));
   var row=new System.Windows.Controls.Primitives.UniformGrid{Columns=3,Margin=new Thickness(0,8,0,0)};tile.Children.Add(row);double size=Math.Min((tile.Width-24)/3,tile.Height-48);
   Func<string,string,Action<string,double>> gauge=(caption,color)=>{var stack=new StackPanel{HorizontalAlignment=HorizontalAlignment.Center};Action<double> set;var ring=Ring(size,size*0.11,color,out set);var value=Label("",size*0.22,p.Ink,FontWeights.SemiBold);value.HorizontalAlignment=HorizontalAlignment.Center;value.VerticalAlignment=VerticalAlignment.Center;ring.Children.Add(value);stack.Children.Add(ring);var name=Label(caption,Profile.FontSize*0.75,p.Soft,FontWeights.Normal);name.HorizontalAlignment=HorizontalAlignment.Center;name.Margin=new Thickness(0,5,0,0);stack.Children.Add(name);row.Children.Add(stack);return (text,percent)=>{bool plug=caption=="Battery"&&percent<0;value.Text=plug?"AC":percent<0?"–":Math.Round(percent)+"%";set(plug?1:percent<0?0:percent/100);};};
   var cpu=gauge("CPU",Profile.Accent);var ram=gauge("Memory","#30B0C7");var power=gauge("Battery","#30D158");
   refresh.Add(()=>{cpu(stats.Cpu,stats.CpuLoad);ram(stats.Ram,stats.RamLoad);power(stats.Battery,stats.BatteryLevel);});
   Shell("stats","System",TileWidth(true),30,310,tile,false);
  }
  void Notes() {
   var tile=Tile(true);tile.Children.Add(Label("Notes",Profile.FontSize,"#E5A50A",FontWeights.Bold));
   var note=new TextBox{Text=Profile.Notes,AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,Height=tile.Height-28,MaxLength=10000,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Foreground=Brush(p.Ink),CaretBrush=Brush(p.Ink),Background=Brushes.Transparent,BorderThickness=new Thickness(0),Padding=new Thickness(0,6,0,0),FontSize=Profile.FontSize};
   note.TextChanged+=(s,e)=>{Profile.Notes=note.Text;dirty=true;};tile.Children.Add(note);
   Shell("notes","Notes",TileWidth(true),30,520,tile,false);
  }
  void TimerWidget() {
   var tile=Tile(false);double size=tile.Height-6;Action<double> set;var ring=Ring(size,size*0.08,"#FF9F0A",out set);ring.HorizontalAlignment=HorizontalAlignment.Center;ring.Cursor=Cursors.Hand;ring.ToolTip="Click to start or pause · right-click the tile to reset";
   var center=new StackPanel{VerticalAlignment=VerticalAlignment.Center,HorizontalAlignment=HorizontalAlignment.Center};var time=Label("",size*0.2,p.Ink,FontWeights.Light);time.FontFamily=Ui.SansFamily;time.HorizontalAlignment=HorizontalAlignment.Center;var state=Label("",Profile.FontSize*0.75,p.Soft,FontWeights.Normal);state.HorizontalAlignment=HorizontalAlignment.Center;
   center.Children.Add(time);center.Children.Add(state);ring.Children.Add(center);ring.Background=Brushes.Transparent;ring.MouseLeftButtonUp+=(s,e)=>{Focus.Toggle(DateTime.UtcNow);e.Handled=true;};tile.Children.Add(ring);
   refresh.Add(()=>{time.Text=Focus.Text;state.Text=Focus.Running?"Focus":Focus.Remaining==TimeSpan.Zero?"Done":"Paused";set(1-Focus.Remaining.TotalMinutes/Profile.FocusMinutes);});
   var w=Shell("focus","Focus",TileWidth(false),340,310,tile,false);var menu2=((FrameworkElement)w.Content).ContextMenu;var reset=new MenuItem{Header="Reset timer"};reset.Click+=(s,e)=>Focus.Reset(Profile.FocusMinutes);menu2.Items.Insert(0,reset);
   var length=new MenuItem{Header="Focus length"};foreach(int m in FocusLengths){int minutes=m;var item=new MenuItem{Header=m+" minutes",IsCheckable=true};item.Click+=(s,e)=>SetFocusLength(minutes);length.Items.Add(item);}length.SubmenuOpened+=(s,e)=>{foreach(MenuItem item in length.Items)item.IsChecked=item.Header.ToString()==Profile.FocusMinutes+" minutes";};menu2.Items.Insert(1,length);
  }
  bool musicShown;
  void MusicWidget() {
   musicShown=true;var tile=Tile(true);var layers=new Grid{Height=tile.Height};tile.Children.Add(layers);
   // The album art, blurred, glows behind the tile like Apple's music widget.
   // A Canvas doesn't clip, so the blurred art can fill the whole card (the window's rounded region trims it).
   // Like Apple's music widget, the whole tile takes on the album's color.
   Border card=null;var glow=new Rectangle();
   var front=new Grid();front.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});front.ColumnDefinitions.Add(new ColumnDefinition());layers.Children.Add(front);
   double artSize=tile.Height-4;var art=new Border{Width=artSize,Height=artSize,CornerRadius=new CornerRadius(12),Background=Brush(p.Chip),ClipToBounds=true,Margin=new Thickness(0,0,14,0),Child=new TextBlock{Text="",FontFamily=new FontFamily(Ui.Icons),FontSize=28,Foreground=Brush(p.Soft),HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center}};front.Children.Add(art);
   var info=new StackPanel{VerticalAlignment=VerticalAlignment.Center};Grid.SetColumn(info,1);front.Children.Add(info);
   var title=Label("Nothing playing",Profile.FontSize*1.05,p.Ink,FontWeights.SemiBold);var artist=Label("Play something in Spotify, your browser, or any player",Profile.FontSize*0.8,p.Soft,FontWeights.Normal);artist.TextWrapping=TextWrapping.Wrap;
   var bar=new ProgressBar{Maximum=1,Height=4,Foreground=Brush(p.Ink),Background=Brush(p.Chip),Margin=new Thickness(0,10,0,6)};
   var controls=new StackPanel{Orientation=Orientation.Horizontal};Func<string,Action,Button> control=(glyph,action)=>{var b=new Button{Content=new TextBlock{Text=glyph,FontFamily=new FontFamily(Ui.Icons),FontSize=16,Foreground=Brush(p.Ink)},Background=Brushes.Transparent,BorderThickness=new Thickness(0),Padding=new Thickness(6,4,6,4),Margin=new Thickness(0,0,10,0),Cursor=Cursors.Hand};b.Click+=(s,e)=>action();return b;};
   var play=control("",media.PlayPause);controls.Children.Add(control("",media.Previous));controls.Children.Add(play);controls.Children.Add(control("",media.Next));
   info.Children.Add(title);info.Children.Add(artist);info.Children.Add(bar);info.Children.Add(controls);
   ImageSource shown=null;
   refresh.Add(()=>{var m=media.State;bar.Visibility=controls.Visibility=m.Has?Visibility.Visible:Visibility.Collapsed;if(!m.Has){title.Text="Nothing playing";artist.Text="Play something in Spotify, your browser, or any player";}else{title.Text=m.Title;artist.Text=m.Artist.Length>0?m.Artist:m.App;bar.Value=m.Progress;((TextBlock)play.Content).Text=m.Playing?"":"";}
    var art2=m.Has?m.Art:null;if(art2!=shown){shown=art2;if(art2==null){art.Background=Brush(p.Chip);art.Child=new TextBlock{Text="\uE8D6",FontFamily=new FontFamily(Ui.Icons),FontSize=28,Foreground=Brush(p.Soft),HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};glow.Fill=null;}else if(m.Art!=null){var brush=new ImageBrush(m.Art){Stretch=Stretch.UniformToFill};art.Background=brush;art.Child=null;glow.Fill=brush;}else{art.Background=Brush(p.Chip);glow.Fill=null;}}});
   var musicWindow=Shell("music","Now playing",TileWidth(true),340,100,tile,false);card=musicWindow.Content as Border;var plain=card!=null?card.Background:null;
   refresh.Add(()=>{if(card==null)return;var m=media.State;if(!m.Has||m.Art==null){card.Background=plain;return;}var c=m.Tint;Func<double,Color> shade=k=>Color.FromRgb((byte)(c.R*k),(byte)(c.G*k),(byte)(c.B*k));card.Background=new LinearGradientBrush(shade(0.55),shade(0.22),90);title.Foreground=Brushes.White;artist.Foreground=Brush("#D0FFFFFF");bar.Foreground=Brushes.White;bar.Background=Brush("#40FFFFFF");foreach(var b in controls.Children.OfType<Button>())((TextBlock)b.Content).Foreground=Brushes.White;});
  }
  void RemindersWidget() {
   var tile=Tile(true);var top=new Grid();top.Children.Add(Label("Reminders",Profile.FontSize,Profile.Accent,FontWeights.Bold));var count=Label("",Profile.FontSize*1.3,Profile.Accent,FontWeights.Bold);count.HorizontalAlignment=HorizontalAlignment.Right;count.VerticalAlignment=VerticalAlignment.Center;top.Children.Add(count);tile.Children.Add(top);
   var list=new StackPanel{Height=tile.Height-62,Margin=new Thickness(0,4,0,0)};tile.Children.Add(list);
   var input=new TextBox{MaxLength=250,Padding=new Thickness(10,6,10,6),Background=Brush(p.Chip),Foreground=Brush(p.Ink),CaretBrush=Brush(p.Ink),BorderThickness=new Thickness(0),ToolTip="New reminder · press Enter"};
   var hint=Label("New reminder",Profile.FontSize*0.85,p.Soft,FontWeights.Normal);hint.IsHitTestVisible=false;hint.Margin=new Thickness(12,0,0,0);hint.VerticalAlignment=VerticalAlignment.Center;
   var field=new Grid();field.Children.Add(input);field.Children.Add(hint);input.TextChanged+=(s,e)=>hint.Visibility=input.Text.Length>0?Visibility.Collapsed:Visibility.Visible;tile.Children.Add(field);
   Action render=()=>{list.Children.Clear();int open=Profile.Tasks.Count(t=>!t.Done);count.Text=open>0?open.ToString():"";
    foreach(var item in Profile.Tasks.Where(t=>!t.Done).Take(4)){var task=item;var row=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(0,4,0,4),Background=Brushes.Transparent,Cursor=Cursors.Hand};
     row.Children.Add(new Ellipse{Width=15,Height=15,Stroke=Brush(Profile.Accent),StrokeThickness=1.6,Margin=new Thickness(0,0,9,0),VerticalAlignment=VerticalAlignment.Center});var text=Label(task.Text,Profile.FontSize*0.9,p.Ink,FontWeights.Normal);text.MaxWidth=tile.Width-30;row.Children.Add(text);
     row.ToolTip="Click to complete";row.MouseLeftButtonUp+=(s,e)=>{task.Done=true;dirty=true;if(renderTasks!=null)renderTasks();e.Handled=true;};list.Children.Add(row);}
    if(open==0)list.Children.Add(Label("All done. Add a reminder below.",Profile.FontSize*0.85,p.Soft,FontWeights.Normal));};
   input.KeyDown+=(s,e)=>{if(e.Key!=Key.Enter||String.IsNullOrWhiteSpace(input.Text))return;e.Handled=true;try{AddTask(input.Text.Trim());input.Clear();}catch(Exception ex){report(ex.Message);}};
   renderTasks+=render;render();
   Shell("reminders","Reminders",TileWidth(true),340,520,tile,false);
  }
  // "F R I D A Y" with the date and time underneath, like the Rainmeter skins.
  void DayWidget() {
   var p2=new StackPanel{HorizontalAlignment=HorizontalAlignment.Center};
   Func<double,FontWeight,double,StackPanel> spaced=(size,weight,gap)=>new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Center,Tag=new object[]{size,weight,gap}};
   var day=spaced(Profile.FontSize*6,FontWeights.Light,Profile.FontSize*1.6);var date=spaced(Profile.FontSize*1.25,FontWeights.SemiBold,Profile.FontSize*0.32);var time=new TextBlock{FontSize=Profile.FontSize*1.15,FontWeight=FontWeights.SemiBold,Foreground=Brushes.White,HorizontalAlignment=HorizontalAlignment.Center,Margin=new Thickness(0,Profile.FontSize,0,0)};
   date.Margin=new Thickness(0,Profile.FontSize*0.6,0,0);p2.Children.Add(day);p2.Children.Add(date);p2.Children.Add(time);
   p2.Effect=new System.Windows.Media.Effects.DropShadowEffect{BlurRadius=14,ShadowDepth=0,Opacity=0.45,Color=Colors.Black};
   Action<StackPanel,string> letters=(row,text)=>{var spec=(object[])row.Tag;row.Children.Clear();for(int i=0;i<text.Length;i++)row.Children.Add(new TextBlock{Text=text[i].ToString(),FontSize=(double)spec[0],FontWeight=(FontWeight)spec[1],FontFamily=Ui.SansFamily,Foreground=Brushes.White,Margin=new Thickness(0,0,i==text.Length-1?0:(double)spec[2],0)});};
   string shownDay=null;
   refresh.Add(()=>{var now=DateTime.Now;var english=System.Globalization.CultureInfo.InvariantCulture;string name=now.ToString("dddd",english).ToUpperInvariant();if(name!=shownDay){shownDay=name;letters(day,name);letters(date,now.ToString("dd  MMMM, yyyy",english).ToUpperInvariant()+".");}time.Text="- "+now.ToString(Profile.Clock24?"HH:mm":"h:mm tt")+" -";});
   var area=SystemParameters.WorkArea;var w=Shell("day","Day",Profile.FontSize*44,area.Left+(area.Width-Profile.FontSize*44)/2,area.Top+area.Height*0.4,p2,false);
   p2.Background=Brushes.Transparent;p2.ToolTip="Move it in Edit mode (right-click a widget or the island)";
   editHooks.Add(on=>p2.Cursor=on?Cursors.SizeAll:Cursors.Arrow);
   p2.MouseLeftButtonDown+=(s,e)=>{if(Editing&&e.ButtonState==MouseButtonState.Pressed){w.DragMove();w.Left=Snap(w.Left);w.Top=Snap(w.Top);Profile.Positions["day"]=new Position{X=w.Left,Y=w.Top};dirty=true;}};
   // Stay on the desktop under other windows, like a Rainmeter skin.
   Glass.SendToBack(w);w.Activated+=(s,e)=>Glass.SendToBack(w);
  }
  void CalendarWidget() {
   var tile=Tile(true);var grid=new Grid{Height=tile.Height};grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(0.42,GridUnitType.Star)});grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(0.58,GridUnitType.Star)});tile.Children.Add(grid);
   var left=new StackPanel();grid.Children.Add(left);var weekday=Label("",Profile.FontSize*0.85,"#FF453A",FontWeights.Bold);var number=Label("",tile.Height*0.5,p.Ink,FontWeights.Light);number.FontFamily=Ui.SansFamily;number.Margin=new Thickness(-2,-tile.Height*0.04,0,0);var month=Label("",Profile.FontSize*0.85,p.Soft,FontWeights.Normal);
   left.Children.Add(weekday);left.Children.Add(number);left.Children.Add(month);
   var days=new System.Windows.Controls.Primitives.UniformGrid{Columns=7};Grid.SetColumn(days,1);grid.Children.Add(days);DateTime shown=DateTime.MinValue;double cell=Math.Min((tile.Width*0.58)/7,tile.Height/7.2),font=cell*0.48;
   refresh.Add(()=>{
    var today=DateTime.Today;if(today==shown)return;shown=today;weekday.Text=today.ToString("dddd").ToUpperInvariant();number.Text=today.Day.ToString();month.Text=today.ToString("MMMM yyyy");days.Children.Clear();
    var format=System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat;var first=format.FirstDayOfWeek;
    for(int i=0;i<7;i++){var head=Label(format.GetShortestDayName((DayOfWeek)(((int)first+i)%7)).Substring(0,1),font*0.9,p.Soft,FontWeights.SemiBold);head.HorizontalAlignment=HorizontalAlignment.Center;head.VerticalAlignment=VerticalAlignment.Center;days.Children.Add(head);}
    int offset=((int)new DateTime(today.Year,today.Month,1).DayOfWeek-(int)first+7)%7;for(int i=0;i<offset;i++)days.Children.Add(new Border());
    for(int d=1;d<=DateTime.DaysInMonth(today.Year,today.Month);d++){bool now=d==today.Day;var text=new TextBlock{Text=d.ToString(),FontSize=font,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center,Foreground=Brush(now?"#FFFFFF":d<today.Day?p.Soft:p.Ink),FontWeight=now?FontWeights.Bold:FontWeights.Normal};days.Children.Add(new Border{Width=cell,Height=cell,CornerRadius=new CornerRadius(cell/2),Background=now?Brush("#FF453A"):Brushes.Transparent,Child=text});}
   });
   Shell("calendar","Calendar",TileWidth(true),30,310,tile,false);
  }
  // The island follows Apple's Dynamic Island: a pure black pill that hugs its content and springs open.
  static readonly Palette Night=new Palette("#000000","#FFFFFF");
  const string Secondary="#98989F",Rail="#3A3A3C",Fill2="#1C1C1E";
  Border islandPill,islandGlow; Grid islandFrame; bool glowing;
  static TextBlock Glyph(string code,double size,string color) { return new TextBlock{Text=code,FontFamily=new FontFamily(Ui.Icons),FontSize=size,Foreground=Ui.Brush(color),HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center}; }
  // A row of rounded bars; the returned action sets them from a 0..1 level. paint recolors them (album tint).
  Action<double,double> Waveform(Panel parent,int count,double width,double height,double gap,out Action<Color> paint) {
   var row=new StackPanel{Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center,Height=height};var bars=new List<Rectangle>();
   for(int i=0;i<count;i++){var bar=new Rectangle{Width=width,Height=width,RadiusX=width/2,RadiusY=width/2,Fill=Brushes.White,Margin=new Thickness(gap/2,0,gap/2,0),VerticalAlignment=VerticalAlignment.Center};bars.Add(bar);row.Children.Add(bar);}
   parent.Children.Add(row);var heights=new double[count];
   paint=color=>{var brush=new SolidColorBrush(color);brush.Freeze();foreach(var bar in bars)bar.Fill=brush;};
   return (level,time)=>{for(int i=0;i<count;i++){double shape=0.4+0.6*Math.Abs(Math.Sin(time*(2.3+i*0.9)+i*1.7));double target=level<=0.01?0:Math.Min(1,0.12+level*1.8*shape);heights[i]=heights[i]*0.5+target*0.5;bars[i].Height=Math.Max(width,height*heights[i]);}};
  }
  Button Round(string code,double size,double glyph,Action action) {
   var b=new Button{Content=Glyph(code,glyph,"#FFFFFF"),Width=size,Height=size,Padding=new Thickness(0),Margin=new Thickness(5,0,5,0),Background=Brush(Fill2),BorderThickness=new Thickness(0),Cursor=Cursors.Hand,Template=CircleTemplate()};
   b.Click+=(s,e)=>{try{action();}catch(Exception ex){report(ex.Message);}};return b;
  }
  Button Plain(string code,double glyph,Action action) {
   var b=new Button{Content=Glyph(code,glyph,"#FFFFFF"),Width=glyph*2.2,Height=glyph*2.2,Padding=new Thickness(0),Margin=new Thickness(10,0,10,0),Background=Brushes.Transparent,BorderThickness=new Thickness(0),Cursor=Cursors.Hand,Template=CircleTemplate()};
   b.Click+=(s,e)=>{try{action();}catch(Exception ex){report(ex.Message);}};return b;
  }
  void Island() {
   var ip=Night;var root=new StackPanel();glowing=false;
   // Compact: a leading item and a trailing item at the edges, like Apple's compact Live Activities.
   var compact=new Grid{Height=26,Background=Brushes.Transparent,Cursor=Cursors.Hand};compact.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});compact.ColumnDefinitions.Add(new ColumnDefinition());compact.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
   var lead=new Border{Width=24,Height=24,CornerRadius=new CornerRadius(6),ClipToBounds=true,VerticalAlignment=VerticalAlignment.Center};var leadGlyph=Glyph("",14,"#FFFFFF");lead.Child=leadGlyph;compact.Children.Add(lead);
   var middle=new TextBlock{FontSize=13,FontWeight=FontWeights.SemiBold,Foreground=Brushes.White,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center,TextTrimming=TextTrimming.CharacterEllipsis,Margin=new Thickness(8,0,8,0)};Grid.SetColumn(middle,1);compact.Children.Add(middle);
   var trail=new Grid{VerticalAlignment=VerticalAlignment.Center,MinWidth=24};Grid.SetColumn(trail,2);compact.Children.Add(trail);
   var smallWaveHost=new StackPanel{HorizontalAlignment=HorizontalAlignment.Right};trail.Children.Add(smallWaveHost);Action<Color> paintSmall;var setSmallWave=Waveform(smallWaveHost,5,2.6,16,2.4,out paintSmall);
   var trailText=new TextBlock{FontSize=13,FontWeight=FontWeights.SemiBold,VerticalAlignment=VerticalAlignment.Center,HorizontalAlignment=HorizontalAlignment.Right};trail.Children.Add(trailText);
   root.Children.Add(compact);
   var expanded=new StackPanel{Visibility=Visibility.Collapsed,Margin=new Thickness(6,4,6,4),RenderTransformOrigin=new Point(0.5,0),RenderTransform=new ScaleTransform(1,1)};
   // Now playing.
   var mediaCard=new StackPanel();var top=new Grid();top.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});top.ColumnDefinitions.Add(new ColumnDefinition());top.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
   // Apple's Now Playing: big rounded art, bold title over a grey artist, a thick grey progress bar, solid transport icons.
   var bigArt=new Border{Width=68,Height=68,CornerRadius=new CornerRadius(14),Background=Brush(Fill2),Margin=new Thickness(0,0,14,0),ClipToBounds=true};top.Children.Add(bigArt);
   var info=new StackPanel{VerticalAlignment=VerticalAlignment.Center};Grid.SetColumn(info,1);var bigTitle=new TextBlock{FontSize=17,FontWeight=FontWeights.SemiBold,Foreground=Brushes.White,TextTrimming=TextTrimming.CharacterEllipsis};var artist=new TextBlock{FontSize=16,Foreground=Brush(Secondary),TextTrimming=TextTrimming.CharacterEllipsis,Margin=new Thickness(0,1,0,0)};info.Children.Add(bigTitle);info.Children.Add(artist);top.Children.Add(info);
   var bigWaveHost=new StackPanel{Margin=new Thickness(12,0,2,0),VerticalAlignment=VerticalAlignment.Center};Grid.SetColumn(bigWaveHost,2);top.Children.Add(bigWaveHost);Action<Color> paintBig;var setBigWave=Waveform(bigWaveHost,6,3,26,3,out paintBig);mediaCard.Children.Add(top);
   var seek=new Grid{Margin=new Thickness(0,18,0,0)};seek.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});seek.ColumnDefinitions.Add(new ColumnDefinition());seek.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
   var elapsed=new TextBlock{FontSize=12.5,FontWeight=FontWeights.Medium,Foreground=Brush(Secondary),Width=42,VerticalAlignment=VerticalAlignment.Center};var remaining=new TextBlock{FontSize=12.5,FontWeight=FontWeights.Medium,Foreground=Brush(Secondary),Width=48,TextAlignment=TextAlignment.Right,VerticalAlignment=VerticalAlignment.Center};Grid.SetColumn(remaining,2);
   var track=new Grid{Height=18,Background=Brushes.Transparent,Cursor=Cursors.Hand,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(6,0,6,0)};Grid.SetColumn(track,1);var rail=new Border{Height=7,CornerRadius=new CornerRadius(3.5),Background=Brush(Rail)};var fill=new Border{Height=7,CornerRadius=new CornerRadius(3.5),Background=Brush("#A1A1A6"),HorizontalAlignment=HorizontalAlignment.Left,Width=0};track.Children.Add(rail);track.Children.Add(fill);
   track.MouseEnter+=(s,e)=>{rail.Height=fill.Height=10;fill.Background=Brushes.White;};track.MouseLeave+=(s,e)=>{rail.Height=fill.Height=7;fill.Background=Brush("#A1A1A6");};
   track.MouseLeftButtonDown+=(s,e)=>{media.Seek(e.GetPosition(track).X/Math.Max(1,track.ActualWidth));e.Handled=true;};
   seek.Children.Add(elapsed);seek.Children.Add(track);seek.Children.Add(remaining);mediaCard.Children.Add(seek);
   // Solid vector icons, drawn rather than taken from an icon font, so they look like Apple's.
   Func<Geometry,double,Action,Button> shape=(geometry,size,action)=>{var path=new Path{Data=geometry,Fill=Brushes.White,Stretch=Stretch.Uniform,Height=size};var b=new Button{Content=path,Width=size*2.4,Height=size*2.4,Padding=new Thickness(0),Margin=new Thickness(6,0,6,0),Background=Brushes.Transparent,BorderThickness=new Thickness(0),Cursor=Cursors.Hand,Template=CircleTemplate()};b.Click+=(s,e)=>{try{action();}catch(Exception ex){report(ex.Message);}};return b;};
   var rewind=Geometry.Parse("M 13,0 L 13,16 L 0,8 Z M 27,0 L 27,16 L 14,8 Z");var forward=Geometry.Parse("M 0,0 L 13,8 L 0,16 Z M 14,0 L 27,8 L 14,16 Z");
   var pauseShape=new GeometryGroup();pauseShape.Children.Add(new RectangleGeometry(new Rect(0,0,7,24),2,2));pauseShape.Children.Add(new RectangleGeometry(new Rect(12,0,7,24),2,2));var playShape=Geometry.Parse("M 1,0 L 20,12 L 1,24 Z");
   var transportRow=new Grid{Margin=new Thickness(0,10,0,2)};var transport=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Center};transportRow.Children.Add(transport);
   var play=shape(pauseShape,24,media.PlayPause);var playIcon=(Path)play.Content;
   transport.Children.Add(shape(rewind,16,media.Previous));transport.Children.Add(play);transport.Children.Add(shape(forward,16,media.Next));
   var output=new Button{Content=new TextBlock{Text="\uE7F5",FontFamily=new FontFamily(Ui.Icons),FontSize=18,Foreground=Brush(Secondary)},Width=38,Height=38,Background=Brushes.Transparent,BorderThickness=new Thickness(0),Cursor=Cursors.Hand,HorizontalAlignment=HorizontalAlignment.Right,ToolTip="Sound output",Template=CircleTemplate()};
   output.Click+=(s,e)=>{try{Process.Start(new ProcessStartInfo("ms-settings:sound"){UseShellExecute=true});}catch(Exception ex){report(ex.Message);}};transportRow.Children.Add(output);
   mediaCard.Children.Add(transportRow);
   expanded.Children.Add(mediaCard);
   // Idle: a calm clock, the way the island looks with no activity.
   var idleCard=new StackPanel{Margin=new Thickness(0,2,0,4)};var bigClock=new TextBlock{FontSize=44,FontWeight=FontWeights.Light,Foreground=Brushes.White,FontFamily=Ui.SansFamily,HorizontalAlignment=HorizontalAlignment.Center};var idleLine=new TextBlock{FontSize=13,Foreground=Brush(Secondary),HorizontalAlignment=HorizontalAlignment.Center};idleCard.Children.Add(bigClock);idleCard.Children.Add(idleLine);expanded.Children.Add(idleCard);
   // Downloads.
   var downloadCard=new Grid{Margin=new Thickness(0,14,0,0)};downloadCard.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});downloadCard.ColumnDefinitions.Add(new ColumnDefinition());downloadCard.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
   var dIcon=Glyph("",15,"#0A84FF");downloadCard.Children.Add(new Border{Width=36,Height=36,CornerRadius=new CornerRadius(18),Background=Brush(Fill2),Margin=new Thickness(0,0,12,0),Child=dIcon});
   var dInfo=new StackPanel{VerticalAlignment=VerticalAlignment.Center};Grid.SetColumn(dInfo,1);var dName=new TextBlock{FontSize=14,FontWeight=FontWeights.SemiBold,Foreground=Brushes.White,TextTrimming=TextTrimming.CharacterEllipsis};var dDetail=new TextBlock{FontSize=12,Foreground=Brush(Secondary),Margin=new Thickness(0,1,0,6)};
   var shimmer=new Border{Height=4,CornerRadius=new CornerRadius(2),Background=Brush(Rail),ClipToBounds=true};var sweep=new Border{Width=80,Height=4,CornerRadius=new CornerRadius(2),HorizontalAlignment=HorizontalAlignment.Left,Background=new LinearGradientBrush(Color.FromArgb(0,0x0A,0x84,0xFF),Color.FromRgb(0x0A,0x84,0xFF),0),RenderTransform=new TranslateTransform()};shimmer.Child=sweep;
   ((TranslateTransform)sweep.RenderTransform).BeginAnimation(TranslateTransform.XProperty,new DoubleAnimation(-80,Wide,TimeSpan.FromSeconds(1.4)){RepeatBehavior=RepeatBehavior.Forever});
   dInfo.Children.Add(dName);dInfo.Children.Add(dDetail);dInfo.Children.Add(shimmer);downloadCard.Children.Add(dInfo);
   var openFolder=Round("",34,14,()=>Process.Start(new ProcessStartInfo(downloads.Folder){UseShellExecute=true}));openFolder.ToolTip="Open Downloads";openFolder.Margin=new Thickness(12,0,0,0);Grid.SetColumn(openFolder,2);downloadCard.Children.Add(openFolder);
   expanded.Children.Add(downloadCard);
   // A dock of round buttons for everything else.
   var dock=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Center,Margin=new Thickness(0,16,0,2)};expanded.Children.Add(dock);
   var ai=new StackPanel{Margin=new Thickness(0,12,0,0)};var tools=new StackPanel{Margin=new Thickness(0,12,0,0)};var tasks=Checklist(ip);tasks.Margin=new Thickness(0,12,0,0);var apps=new StackPanel{Margin=new Thickness(0,8,0,0)};
   if(Chat!=null){islandChat=new IslandChat(Chat,()=>{if(ConfigureAi!=null)ConfigureAi();},Profile.Accent,ip,"#000000"){Host=this,ChangeEngine=ChangeEngine};ai.Children.Add(islandChat);}
   else ai.Children.Add(Text("Open AI in WinDotra to get started.",13,ip.Soft));
   var countdown=Text("",40,"#FF9F0A");countdown.FontWeight=FontWeights.Light;countdown.FontFamily=Ui.SansFamily;countdown.HorizontalAlignment=HorizontalAlignment.Center;tools.Children.Add(countdown);
   var timerButtons=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Center};var focusToggle=Round("",44,16,()=>Focus.Toggle(DateTime.UtcNow));focusToggle.Background=Brush("#3A2A10");((TextBlock)focusToggle.Content).Foreground=Brush("#FF9F0A");timerButtons.Children.Add(focusToggle);timerButtons.Children.Add(Round("",44,16,()=>Focus.Reset(Profile.FocusMinutes)));tools.Children.Add(timerButtons);
   var lengths=new WrapPanel{HorizontalAlignment=HorizontalAlignment.Center,Margin=new Thickness(0,12,0,0)};var lengthLabel=new TextBlock{Foreground=Brush(Secondary),FontSize=12,HorizontalAlignment=HorizontalAlignment.Center,Margin=new Thickness(0,8,0,0)};
   Action showLengths=()=>{lengthLabel.Text=Profile.FocusMinutes+" minute focus";foreach(Button b in lengths.Children){bool on=(int)b.Tag==Profile.FocusMinutes;b.Background=on?Brush("#FF9F0A"):Brush(Fill2);b.Foreground=on?Brushes.Black:Brushes.White;}};
   Func<string,Action,Button> pill=(text,action)=>{var b=new Button{Content=text,FontSize=12,Padding=new Thickness(10,4,10,4),Margin=new Thickness(3),Background=Brush(Fill2),Foreground=Brushes.White,BorderThickness=new Thickness(0),Cursor=Cursors.Hand,Tag=0};b.Click+=(s,e)=>{action();showLengths();};return b;};
   lengths.Children.Add(pill("−5",()=>SetFocusLength(Profile.FocusMinutes-5)));foreach(int m in FocusLengths){int minutes=m;var b=pill(m.ToString(),()=>SetFocusLength(minutes));b.Tag=m;lengths.Children.Add(b);}lengths.Children.Add(pill("+5",()=>SetFocusLength(Profile.FocusMinutes+5)));
   ((Button)lengths.Children[0]).Tag=-1;((Button)lengths.Children[lengths.Children.Count-1]).Tag=-1;tools.Children.Add(lengthLabel);tools.Children.Add(lengths);showLengths();
   var cpuMeter=Meter(apps,"CPU",true,ip);var ramMeter=Meter(apps,"Memory",true,ip);
   // Quick launch: your own apps and sites (Island page in WinDotra), with their real icons when there are files behind them.
   var shortcuts=new WrapPanel{HorizontalAlignment=HorizontalAlignment.Center,Margin=new Thickness(0,14,0,0)};
   foreach(var launcher in Profile.Launchers){var item=launcher;var icon=item.Icon;var b=Round(item.Glyph,40,15,()=>item.Open());if(icon!=null){b.Content=new Image{Source=icon,Width=20,Height=20};}b.ToolTip=item.Name;b.Margin=new Thickness(4);shortcuts.Children.Add(b);}apps.Children.Add(shortcuts);
   expanded.Children.Add(ai);expanded.Children.Add(tools);expanded.Children.Add(tasks);expanded.Children.Add(apps);
   bool pinned=false,open=false;string shownTab=null;var dockButtons=new Dictionary<string,Button>();
   Action<string> tab=name=>{shownTab=name;foreach(var item in dockButtons){bool on=item.Key==name;item.Value.Background=on?Brushes.White:Brush(Fill2);((TextBlock)item.Value.Content).Foreground=on?Brushes.Black:Brushes.White;}ai.Visibility=name=="ai"?Visibility.Visible:Visibility.Collapsed;tools.Visibility=name=="tools"?Visibility.Visible:Visibility.Collapsed;tasks.Visibility=name=="tasks"?Visibility.Visible:Visibility.Collapsed;apps.Visibility=name=="apps"?Visibility.Visible:Visibility.Collapsed;};
   foreach(var item in new[]{new[]{"","Ask AI","ai"},new[]{"","Focus timer","tools"},new[]{"","Tasks","tasks"},new[]{"","System","apps"}}){string key=item[2];if(!DockOn(key))continue;var b=Round(item[0],36,14,()=>{pinned=true;tab(shownTab==key?null:key);});b.ToolTip=item[1];dockButtons[key]=b;dock.Children.Add(b);}tab(null);
   var scroll=new ScrollViewer{Content=expanded,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};root.Children.Add(scroll);
   double windowWidth=Wide+36;var area=SystemParameters.WorkArea;
   if(Profile.IslandShape=="Notch")Profile.Positions.Remove("island");
   double islandTop=Profile.IslandShape=="Notch"?PrimaryDip(null,false).Top-18*Profile.IslandScale:area.Top+4;
   var w=Shell("island","Island",windowWidth*Profile.IslandScale,area.Left+(area.Width-windowWidth*Profile.IslandScale)/2,islandTop,root,true);islandWindow=w;
   if(Profile.IslandShape=="Notch"&&!Profile.Positions.ContainsKey("island"))w.Top=islandTop;
   // Compact width follows what's shown; opening springs the pill to full width like Apple's.
   Func<double> compactWidth=()=>{bool activity=(Profile.IslandMedia&&media.State.Has)||(Profile.IslandDownloads&&downloads.Active.Count>0)||Focus.Running||(Chat!=null&&Chat.Busy);return activity?250:150;};
   double targetWidth=0;
   Action<double> morph=width=>{if(Math.Abs(width-targetWidth)<1)return;targetWidth=width;islandFrame.BeginAnimation(FrameworkElement.WidthProperty,new DoubleAnimation(width,TimeSpan.FromMilliseconds(420)){EasingFunction=new BackEase{Amplitude=0.35,EasingMode=EasingMode.EaseOut}});};
   islandFrame.Width=compactWidth();targetWidth=islandFrame.Width;
   Action updateIsland=null;
   Action<bool> expand=value=>{
    var screenSource=PresentationSource.FromVisual(w);if(open==value||screenSource==null||islandWindow!=w)return;open=value;var toPixels=screenSource.CompositionTarget.TransformToDevice;var toDip=screenSource.CompositionTarget.TransformFromDevice;
    var location=toPixels.Transform(new Point(w.Left,w.Top));var screen=System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point((int)location.X,(int)location.Y));
    var topLeft=toDip.Transform(new Point(screen.WorkingArea.Left,screen.WorkingArea.Top));var bottomRight=toDip.Transform(new Point(screen.WorkingArea.Right,screen.WorkingArea.Bottom));
    scroll.MaxHeight=Math.Max(100,bottomRight.Y-topLeft.Y-120);
    islandPill.CornerRadius=IslandCorners(value);islandGlow.CornerRadius=islandPill.CornerRadius;compact.Visibility=value?Visibility.Collapsed:Visibility.Visible;
    morph(value?Wide:compactWidth());
    expanded.Visibility=value?Visibility.Visible:Visibility.Collapsed;
    if(value){var ease=new CubicEase{EasingMode=EasingMode.EaseOut};expanded.BeginAnimation(UIElement.OpacityProperty,new DoubleAnimation(0,1,TimeSpan.FromMilliseconds(300)){EasingFunction=ease});var scale=(ScaleTransform)expanded.RenderTransform;scale.BeginAnimation(ScaleTransform.ScaleXProperty,new DoubleAnimation(0.92,1,TimeSpan.FromMilliseconds(380)){EasingFunction=new BackEase{Amplitude=0.3,EasingMode=EasingMode.EaseOut}});scale.BeginAnimation(ScaleTransform.ScaleYProperty,new DoubleAnimation(0.85,1,TimeSpan.FromMilliseconds(380)){EasingFunction=new BackEase{Amplitude=0.3,EasingMode=EasingMode.EaseOut}});}
    if(updateIsland!=null)updateIsland();w.UpdateLayout();
    double minTop=Profile.IslandShape=="Notch"?toDip.Transform(new Point(screen.Bounds.Left,screen.Bounds.Top)).Y-18*Profile.IslandScale:topLeft.Y;
    w.Top=Math.Max(minTop,Math.Min(w.Top,bottomRight.Y-w.ActualHeight));
   };
   // Like Apple's: it shrinks back as soon as the pointer leaves or you click somewhere else.
   var closeLater=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(220)};closeLater.Tick+=(s,e)=>{closeLater.Stop();if(DesktopAttach&&!w.IsMouseOver)expand(false);};
   w.MouseEnter+=(s,e)=>{closeLater.Stop();if(Profile.IslandHover)expand(true);};w.MouseLeave+=(s,e)=>closeLater.Start();w.Deactivated+=(s,e)=>{if(DesktopAttach&&islandWindow==w&&!w.IsMouseOver)expand(false);};
   // Click to open or close, drag to move: on the compact pill, and on any empty part of the open island.
   MouseButtonEventHandler grab=(s,e)=>{if(e.ButtonState!=MouseButtonState.Pressed||e.OriginalSource is TextBox)return;double left=w.Left,topY=w.Top;if(Editing)w.DragMove();
    if(Math.Abs(w.Left-left)+Math.Abs(w.Top-topY)>2){Profile.Positions["island"]=new Position{X=w.Left,Y=w.Top};dirty=true;}else if(s==compact){pinned=!open;expand(!open);}else if(open){pinned=false;expand(false);}e.Handled=true;};
   compact.MouseLeftButtonDown+=grab;top.MouseLeftButtonDown+=grab;idleCard.MouseLeftButtonDown+=grab;idleCard.Background=Brushes.Transparent;top.Background=Brushes.Transparent;
   compact.ToolTip="Click to open · drag to move · right-click for options";
   var menu=new ContextMenu();var editWidgets=new MenuItem{Header="Edit widgets"};editWidgets.Click+=(s,e)=>SetEditing(!Editing);menu.Items.Add(editWidgets);menu.Opened+=(s,e)=>editWidgets.Header=Editing?"Done editing":"Edit widgets";
   var center=new MenuItem{Header="Center at top"};center.Click+=(s,e)=>CenterIsland();menu.Items.Add(center);
   var settings=new MenuItem{Header="Island settings…"};settings.Click+=(s,e)=>{if(ConfigureIsland!=null)ConfigureIsland();};menu.Items.Add(settings);
   var hideIsland=new MenuItem{Header="Hide until next launch"};hideIsland.Click+=(s,e)=>w.Hide();menu.Items.Add(hideIsland);islandPill.ContextMenu=menu;
   openAssistant=()=>{w.Show();pinned=true;tab("ai");expand(true);w.Activate();if(islandChat!=null)islandChat.FocusInput();};
   DateTime born=DateTime.Now;wave.Tick+=(s,e)=>{if(islandWindow!=w)return;double level=media.State.Playing?audio.Peak():0,time=(DateTime.Now-born).TotalSeconds;if(!open)setSmallWave(level,time);else setBigWave(level,time);};
   ImageSource shownArt=null;Color shownTint=Colors.Transparent;
   updateIsland=()=>{
    var m=media.State;bool hasMedia=Profile.IslandMedia&&m.Has,downloading=Profile.IslandDownloads&&downloads.Active.Count>0,finished=Profile.IslandDownloads&&!downloading&&downloads.RecentlyFinished;
    var d=downloading?downloads.Active.OrderByDescending(x=>x.Speed).First():null;
    if(m.Art!=shownArt){shownArt=m.Art;bigArt.Background=m.Art==null?(System.Windows.Media.Brush)Brush(Fill2):new ImageBrush(m.Art){Stretch=Stretch.UniformToFill};}
    if(m.Tint!=shownTint){shownTint=m.Tint;paintSmall(m.Tint);paintBig(m.Tint);}
    string clock=DateTime.Now.ToString(Profile.Clock24?"HH:mm":"h:mm");
    // Compact content, in Apple's priority order: AI reply, music, download, timer, idle.
    lead.Background=Brushes.Transparent;leadGlyph.Text="";middle.Text="";trailText.Text="";smallWaveHost.Visibility=Visibility.Collapsed;
    if(Chat!=null&&Chat.Busy){leadGlyph.Text="";leadGlyph.Foreground=Brushes.White;trailText.Text="…";trailText.FontFamily=Ui.SansFamily;trailText.Foreground=Brushes.White;}
    else if(hasMedia){if(m.Art!=null)lead.Background=new ImageBrush(m.Art){Stretch=Stretch.UniformToFill};else{leadGlyph.Text="";leadGlyph.Foreground=Brushes.White;}if(m.Playing)smallWaveHost.Visibility=Visibility.Visible;else{trailText.Text="";trailText.FontFamily=new FontFamily(Ui.Icons);trailText.Foreground=Brush(Secondary);}}
    else if(downloading){leadGlyph.Text="";leadGlyph.Foreground=Brush("#0A84FF");trailText.Text=DownloadWatcher.Size(d.Bytes);trailText.FontFamily=Ui.SansFamily;trailText.Foreground=Brush("#0A84FF");}
    else if(Focus.Running||Focus.Remaining==TimeSpan.Zero){leadGlyph.Text="";leadGlyph.Foreground=Brush("#FF9F0A");trailText.Text=Focus.Text;trailText.FontFamily=Ui.SansFamily;trailText.Foreground=Brush("#FF9F0A");}
    else if(finished){leadGlyph.Text="";leadGlyph.Foreground=Brush("#30D158");middle.Text=downloads.Finished;}
    else middle.Text=Profile.IslandIdle=="Nothing"?"":Profile.IslandIdle=="Date"?DateTime.Now.ToString("ddd d MMM"):Profile.IslandIdle=="Clock and date"?DateTime.Now.ToString("ddd d")+"  "+clock:Profile.IslandIdle=="Text"?Profile.IslandText:clock;
    // Expanded content.
    mediaCard.Visibility=hasMedia?Visibility.Visible:Visibility.Collapsed;idleCard.Visibility=!hasMedia&&!downloading&&!finished?Visibility.Visible:Visibility.Collapsed;
    bigClock.Text=clock;idleLine.Text=DateTime.Now.ToString("dddd, d MMMM")+"   ·   "+stats.Battery;
    if(hasMedia){bigTitle.Text=m.Title;artist.Text=m.Artist.Length>0?m.Artist:m.App;elapsed.Text=Clock(m.Position);remaining.Text="-"+Clock(m.Duration-m.Position<TimeSpan.Zero?TimeSpan.Zero:m.Duration-m.Position);fill.Width=rail.ActualWidth*m.Progress;playIcon.Data=m.Playing?(Geometry)pauseShape:playShape;bigWaveHost.Visibility=m.Playing?Visibility.Visible:Visibility.Hidden;}
    downloadCard.Visibility=downloading||finished?Visibility.Visible:Visibility.Collapsed;downloadCard.Margin=new Thickness(0,hasMedia?16:2,0,0);
    if(downloading){dName.Text=d.Name+(downloads.Active.Count>1?"  (+"+(downloads.Active.Count-1)+" more)":"");dDetail.Text=DownloadWatcher.Size(d.Bytes)+(d.Speed>0?"  ·  "+DownloadWatcher.Size(d.Speed)+"/s":"");shimmer.Visibility=Visibility.Visible;dIcon.Text="";dIcon.Foreground=Brush("#0A84FF");}
    else if(finished){dName.Text=downloads.Finished;dDetail.Text="Downloaded";shimmer.Visibility=Visibility.Collapsed;dIcon.Text="";dIcon.Foreground=Brush("#30D158");}
    countdown.Text=Focus.Text;((TextBlock)focusToggle.Content).Text=Focus.Running?"":"";cpuMeter(stats.Cpu,stats.CpuLoad);ramMeter(stats.Ram,stats.RamLoad);
    if(!open)morph(compactWidth());
    bool glow=Profile.IslandAura&&((hasMedia&&m.Playing)||downloading);if(glow!=glowing){glowing=glow;islandGlow.BeginAnimation(UIElement.OpacityProperty,new DoubleAnimation(glow?0.85:0,TimeSpan.FromMilliseconds(600)));}
    if(hasMedia&&m.Playing){if(!wave.IsEnabled)wave.Start();}else if(wave.IsEnabled){wave.Stop();setSmallWave(0,0);setBigWave(0,0);}
   };
   refresh.Add(updateIsland);
  }
  static string Clock(TimeSpan time) { return time.TotalHours>=1?((int)time.TotalHours)+":"+time.Minutes.ToString("00")+":"+time.Seconds.ToString("00"):((int)time.TotalMinutes)+":"+time.Seconds.ToString("00"); }
  static ControlTemplate CircleTemplate() {
   var template=new ControlTemplate(typeof(Button));var grid=new FrameworkElementFactory(typeof(Grid));var back=new FrameworkElementFactory(typeof(Ellipse));back.SetValue(Shape.FillProperty,new TemplateBindingExtension(Control.BackgroundProperty));
   var hover=new FrameworkElementFactory(typeof(Ellipse));hover.Name="Hover";hover.SetValue(Shape.FillProperty,Brushes.White);hover.SetValue(UIElement.OpacityProperty,0.0);
   var content=new FrameworkElementFactory(typeof(ContentPresenter));content.SetValue(FrameworkElement.HorizontalAlignmentProperty,HorizontalAlignment.Center);content.SetValue(FrameworkElement.VerticalAlignmentProperty,VerticalAlignment.Center);
   grid.AppendChild(back);grid.AppendChild(hover);grid.AppendChild(content);template.VisualTree=grid;
   var over=new Trigger{Property=UIElement.IsMouseOverProperty,Value=true};over.Setters.Add(new Setter(UIElement.OpacityProperty,0.12,"Hover"));template.Triggers.Add(over);
   var pressed=new Trigger{Property=System.Windows.Controls.Primitives.ButtonBase.IsPressedProperty,Value=true};pressed.Setters.Add(new Setter(UIElement.OpacityProperty,0.22,"Hover"));template.Triggers.Add(pressed);
   return template;
  }
  StackPanel Checklist(Palette pal) {
   var panel=new StackPanel();
   var list=new StackPanel();panel.Children.Add(new ScrollViewer{Content=list,MaxHeight=250,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});
   var input=new TextBox{MaxLength=250,Padding=new Thickness(10,8,10,8),Margin=new Thickness(0,8,0,0),Background=Brush(pal.Field),Foreground=Brush(pal.Ink),CaretBrush=Brush(pal.Ink),BorderThickness=new Thickness(0),ToolTip="Type a task and press Enter"};panel.Children.Add(input);
   Action render=null;render=()=>{list.Children.Clear();if(Profile.Tasks.Count==0)list.Children.Add(Text("Nothing on your list. Add a task below.",12,pal.Soft));foreach(var item in Profile.Tasks){var task=item;var row=new DockPanel();var remove=Quiet("×",()=>{Profile.Tasks.Remove(task);dirty=true;render();},pal);remove.Background=Brushes.Transparent;remove.Foreground=Brush(pal.Soft);remove.ToolTip="Remove task";DockPanel.SetDock(remove,Dock.Right);row.Children.Add(remove);var done=new CheckBox{Content=new TextBlock{Text=task.Text,TextWrapping=TextWrapping.Wrap,Foreground=Brush(task.Done?pal.Soft:pal.Ink),TextDecorations=task.Done?TextDecorations.Strikethrough:null},IsChecked=task.Done,Margin=new Thickness(0,8,8,6)};done.Click+=(s,e)=>{task.Done=done.IsChecked==true;dirty=true;render();};row.Children.Add(done);list.Children.Add(row);}};
   Action add=()=>{if(String.IsNullOrWhiteSpace(input.Text))return;if(Profile.Tasks.Count>=40){report("Checklist is full (40 tasks). Remove completed items first.");return;}Profile.Tasks.Add(new ChecklistItem{Text=input.Text.Trim()});input.Clear();dirty=true;render();};
   var buttons=new WrapPanel();buttons.Children.Add(Button("Add",add,pal));buttons.Children.Add(Quiet("Clear done",()=>{Profile.Tasks.RemoveAll(t=>t.Done);dirty=true;render();},pal));panel.Children.Add(buttons);input.KeyDown+=(s,e)=>{if(e.Key==Key.Enter){add();e.Handled=true;}};renderTasks+=render;render();return panel;
  }
  public Action ConfigureIsland; public Action<string> ChangeEngine;
  public static readonly int[] FocusLengths={5,10,15,25,45,60,90};
  // Changes the focus length (and resets the timer to it unless it's running).
  public void SetFocusLength(int minutes){Profile.FocusMinutes=Math.Max(1,Math.Min(180,minutes));dirty=true;if(!Focus.Running)Focus.Reset(Profile.FocusMinutes);Persist();}
  bool DockOn(string key){return key=="ai"?Profile.DockAi:key=="tools"?Profile.DockFocus:key=="tasks"?Profile.DockTasks:Profile.DockSystem;}
  // Puts the island back at the top center of the screen it's on and forgets its dragged position.
  public void CenterIsland(){Profile.Positions.Remove("island");dirty=true;if(islandWindow==null)return;var w=islandWindow;var screen=System.Windows.Forms.Screen.FromHandle(new System.Windows.Interop.WindowInteropHelper(w).Handle);var source=PresentationSource.FromVisual(w);if(source==null)return;var toDip=source.CompositionTarget.TransformFromDevice;bool notch=Profile.IslandShape=="Notch";var r=notch?screen.Bounds:screen.WorkingArea;var topLeft=toDip.Transform(new Point(r.Left,r.Top));var size=toDip.Transform(new Point(r.Width,r.Height));w.Left=topLeft.X+(size.X-w.ActualWidth)/2;w.Top=notch?topLeft.Y-18*Profile.IslandScale:topLeft.Y+4;}
  // Pill: fully round. Notch: hangs from the top edge like a MacBook notch. Rounded: a softer rectangle.
  CornerRadius IslandCorners(bool open){double r=Profile.IslandShape=="Rounded"?(open?22:12):(open?34:21);return Profile.IslandShape=="Notch"?new CornerRadius(0,0,r,r):new CornerRadius(r);}
  public void OpenAssistant(){if(islandWindow==null){if(p==null)p=new Palette(Profile.Background,Profile.Accent);Island();Tick();timer.Start();}if(openAssistant!=null)openAssistant();}
  public IList<Window> Windows { get { return windows.AsReadOnly(); } }
 }
}
