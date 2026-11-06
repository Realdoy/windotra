using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;

namespace WinCanvas {
 // Per-widget look chosen in Edit mode: a size factor and an optional tile color.
 public class WidgetLook { public double Scale=1; public string Tint=""; }

 // Edit mode, like macOS: only while editing can widgets be moved, resized, recolored, or removed.
 public partial class OverlayManager {
  public bool Editing {get;private set;}
  public Action<bool> EditingChanged;
  readonly List<Action<bool>> editHooks=new List<Action<bool>>();
  Window gridWindow,toolbar;
  static readonly string[][] WidgetNames={new[]{"clock","Clock"},new[]{"stats","System"},new[]{"notes","Notes"},new[]{"focus","Focus timer"},new[]{"calendar","Calendar"},new[]{"music","Now playing"},new[]{"reminders","Reminders"},new[]{"day","Big date"}};
  static readonly string[][] DarkTints={new[]{"Default",""},new[]{"Graphite","#2C2C2E"},new[]{"Midnight","#1B2540"},new[]{"Ocean","#12384A"},new[]{"Forest","#1F3A2E"},new[]{"Plum","#3B2346"},new[]{"Wine","#4A1F2B"},new[]{"Black","#000000"}};
  static readonly string[][] LightTints={new[]{"Default",""},new[]{"White","#FFFFFF"},new[]{"Cream","#FFF6E5"},new[]{"Mint","#E6F6EC"},new[]{"Sky","#E6F0FF"},new[]{"Blush","#FFE9EE"},new[]{"Lilac","#F0E8FF"}};

  bool Enabled(string id){if(id.StartsWith("custom:"))return Profile.CustomWidgets.Contains(id);switch(id){case "clock":return Profile.Clock;case "stats":return Profile.Stats;case "notes":return Profile.Note;case "focus":return Profile.Focus;case "calendar":return Profile.Calendar;case "music":return Profile.Music;case "reminders":return Profile.Reminders;default:return Profile.Day;}}
  void Enable(string id,bool on){if(id.StartsWith("custom:")){Profile.CustomWidgets.Remove(id);if(on)Profile.CustomWidgets.Add(id);return;}switch(id){case "clock":Profile.Clock=on;break;case "stats":Profile.Stats=on;break;case "notes":Profile.Note=on;break;case "focus":Profile.Focus=on;break;case "calendar":Profile.Calendar=on;break;case "music":Profile.Music=on;break;case "reminders":Profile.Reminders=on;break;default:Profile.Day=on;break;}}
  WidgetLook LookFor(string id){WidgetLook look;if(!Profile.Looks.TryGetValue(id,out look)||look==null){look=new WidgetLook();}return look;}

  public void SetEditing(bool on) {
   Editing=on;foreach(var hook in editHooks)hook(on);
   if(on){ShowGrid();ShowToolbar();}else{if(gridForm!=null){gridForm.Close();gridForm.Dispose();gridForm=null;}if(toolbar!=null){toolbar.Close();toolbar=null;}Persist();}
   if(EditingChanged!=null)EditingChanged(on);
  }
  // Rebuilds the desktop (after adding or removing a widget) and stays in Edit mode.
  void Rebuild(){bool was=Editing;dirty=true;Persist();Start();if(was)SetEditing(true);}
  public double Snap(double value){return Profile.SnapGrid?Math.Round(value/Profile.GridSize)*Profile.GridSize:value;}

  // The edit chrome around one widget tile: dashed outline, remove (−), options (⋯), and a resize handle.
  void AddEditChrome(Window w,string id,Border card,double baseWidth) {
   var look=LookFor(id);var root=new Grid();w.Content=null;var menu0=card.ContextMenu;card.ContextMenu=null;root.ContextMenu=menu0;root.Children.Add(card);w.Content=root;
   if(look.Tint.Length>0){var t=(Color)ColorConverter.ConvertFromString(look.Tint);byte a=(byte)(Profile.Glass?0xD6:0xFF);card.Background=new LinearGradientBrush(Color.FromArgb(a,(byte)Math.Min(255,t.R+18),(byte)Math.Min(255,t.G+18),(byte)Math.Min(255,t.B+18)),Color.FromArgb(a,t.R,t.G,t.B),90);}
   Action<double> scale=sc=>{root.LayoutTransform=new ScaleTransform(sc,sc);w.Width=baseWidth*sc;};scale(look.Scale);
   var chrome=new Grid{Visibility=Visibility.Collapsed};root.Children.Add(chrome);
   chrome.Children.Add(new Rectangle{Margin=new Thickness(13),RadiusX=26,RadiusY=26,Stroke=Brush("#0A84FF"),StrokeThickness=2,StrokeDashArray=new DoubleCollection{4,3},IsHitTestVisible=false});
   Func<string,string,string,Button> round=(glyph,back,tip)=>{var b=new Button{Width=24,Height=24,Background=Brush(back),Foreground=Brushes.White,BorderThickness=new Thickness(0),Padding=new Thickness(0),Cursor=Cursors.Hand,ToolTip=tip,Content=new TextBlock{Text=glyph,FontFamily=new FontFamily(Ui.Icons),FontSize=11,Foreground=Brushes.White,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center},Template=CircleTemplate()};b.Effect=new System.Windows.Media.Effects.DropShadowEffect{BlurRadius=6,ShadowDepth=1,Opacity=0.4};return b;};
   var remove=round("","#FF453A","Remove this widget");remove.HorizontalAlignment=HorizontalAlignment.Left;remove.VerticalAlignment=VerticalAlignment.Top;remove.Margin=new Thickness(4,4,0,0);
   remove.Click+=(s,e)=>{Enable(id,false);Profile.Looks.Remove(id);Rebuild();};chrome.Children.Add(remove);
   var options=round("","#636366","Size and color");options.HorizontalAlignment=HorizontalAlignment.Right;options.VerticalAlignment=VerticalAlignment.Top;options.Margin=new Thickness(0,4,4,0);
   var menu=new ContextMenu();
   foreach(var size in new[]{new object[]{"Small",0.8},new object[]{"Medium",1.0},new object[]{"Large",1.25},new object[]{"Extra large",1.5}}){double v=(double)size[1];var item=new MenuItem{Header="Size: "+size[0]};item.Click+=(s,e)=>{look=LookFor(id);look.Scale=v;Profile.Looks[id]=look;scale(v);dirty=true;};menu.Items.Add(item);}
   menu.Items.Add(new Separator());
   foreach(var tint in Ui.IsLight(Profile.Background)?LightTints:DarkTints){string hex=tint[1];var item=new MenuItem{Header="Color: "+tint[0]};item.Click+=(s,e)=>{look=LookFor(id);look.Tint=hex;Profile.Looks[id]=look;Rebuild();};menu.Items.Add(item);}
   options.ContextMenu=menu;options.Click+=(s,e)=>{menu.PlacementTarget=options;menu.IsOpen=true;};chrome.Children.Add(options);
   // Resize: drag the corner handle; the whole tile scales.
   var grip=new Thumb{Width=22,Height=22,HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Bottom,Margin=new Thickness(0,0,6,6),Cursor=Cursors.SizeNWSE,ToolTip="Drag to resize"};
   var face=new FrameworkElementFactory(typeof(Ellipse));face.SetValue(Shape.FillProperty,Brush("#0A84FF"));face.SetValue(Shape.StrokeProperty,Brushes.White);face.SetValue(Shape.StrokeThicknessProperty,2.0);grip.Template=new ControlTemplate(typeof(Thumb)){VisualTree=face};
   grip.DragDelta+=(s,e)=>{look=LookFor(id);look.Scale=Math.Max(0.6,Math.Min(1.8,look.Scale+e.HorizontalChange/baseWidth));Profile.Looks[id]=look;scale(look.Scale);};
   grip.DragCompleted+=(s,e)=>{dirty=true;Persist();};chrome.Children.Add(grip);
   Action<bool> hook=on=>chrome.Visibility=on?Visibility.Visible:Visibility.Collapsed;editHooks.Add(hook);hook(Editing);
  }

  [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr window,int index);
  [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr window,int index,int value);
  // A faint grid over the desktop while editing; clicks pass straight through it. It's painted once with GDI in a
  // plain window (the old full-screen WPF overlay was redrawn on every drag and made moving widgets lag).
  class GridForm : System.Windows.Forms.Form {
   readonly int size;
   public GridForm(System.Drawing.Rectangle area,int size){this.size=size;FormBorderStyle=System.Windows.Forms.FormBorderStyle.None;ShowInTaskbar=false;StartPosition=System.Windows.Forms.FormStartPosition.Manual;Bounds=area;BackColor=System.Drawing.Color.Magenta;TransparencyKey=System.Drawing.Color.Magenta;Opacity=0.35;}
   protected override bool ShowWithoutActivation{get{return true;}}
   protected override System.Windows.Forms.CreateParams CreateParams{get{var p=base.CreateParams;p.ExStyle|=0x20|0x80000|0x80|0x08000000;return p;}}
   protected override void OnPaint(System.Windows.Forms.PaintEventArgs e){using(var pen=new System.Drawing.Pen(System.Drawing.Color.White,1)){for(int x=0;x<Width;x+=size)e.Graphics.DrawLine(pen,x,0,x,Height);for(int y=0;y<Height;y+=size)e.Graphics.DrawLine(pen,0,y,Width,y);}}
  }
  GridForm gridForm;
  void ShowGrid() {
   if(gridForm!=null){gridForm.Close();gridForm.Dispose();gridForm=null;}if(!Profile.ShowGrid)return;
   var area=System.Windows.Forms.Screen.PrimaryScreen.WorkingArea;int g=(int)Math.Round(Profile.GridSize*NativeCustomization.Dpi()/96.0);
   gridForm=new GridForm(area,Math.Max(4,g));gridForm.Show();
  }
  // The floating "Editing" bar: grid, snap, add a widget, done.
  void ShowToolbar() {
   if(toolbar!=null)toolbar.Close();
   var bar=new StackPanel{Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center};
   bar.Children.Add(new TextBlock{Text="Editing widgets",Foreground=Brushes.White,FontWeight=FontWeights.SemiBold,FontSize=13,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(6,0,14,0)});
   Func<string,bool,Action<bool>,CheckBox> toggle=(text,on,set)=>{var c=new CheckBox{Content=text,IsChecked=on,Foreground=Brushes.White,FontSize=12,Margin=new Thickness(0,0,14,0),VerticalAlignment=VerticalAlignment.Center};c.Click+=(s,e)=>set(c.IsChecked==true);return c;};
   bar.Children.Add(toggle("Grid",Profile.ShowGrid,v=>{Profile.ShowGrid=v;dirty=true;ShowGrid();}));
   bar.Children.Add(toggle("Snap",Profile.SnapGrid,v=>{Profile.SnapGrid=v;dirty=true;}));
   var add=new Button{Content="Add widget",FontSize=12,Padding=new Thickness(12,5,12,5),Margin=new Thickness(0,0,8,0)};var addMenu=new ContextMenu();
   foreach(var widget in WidgetNames){string key=widget[0];if(Enabled(key))continue;var item=new MenuItem{Header=widget[1]};item.Click+=(s,e)=>{Enable(key,true);Rebuild();};addMenu.Items.Add(item);}
   if(addMenu.Items.Count==0)addMenu.Items.Add(new MenuItem{Header="All widgets are on",IsEnabled=false});
   add.ContextMenu=addMenu;add.Click+=(s,e)=>{addMenu.PlacementTarget=add;addMenu.IsOpen=true;};bar.Children.Add(add);
   var done=new Button{Content="Done",FontSize=12,FontWeight=FontWeights.SemiBold,Padding=new Thickness(16,5,16,5),Background=Brush("#0A84FF"),Foreground=Brushes.White,Margin=new Thickness(0)};done.Click+=(s,e)=>SetEditing(false);bar.Children.Add(done);
   var pill=new Border{Background=Brush("#E6202022"),BorderBrush=Brush("#33FFFFFF"),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(22),Padding=new Thickness(14,8,8,8),Child=bar,Margin=new Thickness(16),Effect=new System.Windows.Media.Effects.DropShadowEffect{BlurRadius=24,ShadowDepth=4,Opacity=0.5}};
   toolbar=new Window{WindowStyle=WindowStyle.None,AllowsTransparency=true,Background=Brushes.Transparent,ShowInTaskbar=false,Topmost=true,ResizeMode=ResizeMode.NoResize,SizeToContent=SizeToContent.WidthAndHeight,Content=pill,Title="WinDotra editing",FontFamily=Ui.SansFamily};
   if(Application.Current!=null&&Application.Current.MainWindow!=null)toolbar.Resources=Application.Current.MainWindow.Resources;
   var area=SystemParameters.WorkArea;toolbar.Show();toolbar.Left=area.Left+(area.Width-toolbar.ActualWidth)/2;toolbar.Top=area.Bottom-toolbar.ActualHeight-24;
  }
 }
}
