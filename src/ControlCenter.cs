using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace WinCanvas {
 // A Control Center like macOS: network, Bluetooth, now playing, dark mode, focus, display, sound, and shortcuts.
 public partial class OverlayManager {
  Window controlCenter; DateTime controlClosed; public Action<bool> SwitchAppearance;
  public void ToggleControlCenter(FrameworkElement anchor) {
   if(controlCenter!=null){CloseControlCenter();return;}
   if((DateTime.Now-controlClosed).TotalMilliseconds<300)return; // the click that closed it
   bool light=Profile.MenuBarLight;string ink=light?"#1D1D1F":"#FFFFFF",soft=light?"#6E6E73":"#A1A1A6",tile=light?"#FFFFFFFF":"#FF2C2C2E",on="#0A84FF";
   var root=new StackPanel{Width=340};
   Func<UIElement,Border> Tile=content=>new Border{Background=Brush(tile),CornerRadius=new CornerRadius(18),Padding=new Thickness(12),Margin=new Thickness(5),Child=content};
   Func<string,string,bool,Action,Grid> roundToggle=null;
   roundToggle=(glyph,label,active,action)=>{var row=new Grid{Margin=new Thickness(0,4,0,4),Background=Brushes.Transparent,Cursor=Cursors.Hand};row.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});row.ColumnDefinitions.Add(new ColumnDefinition());
    var circle=new Border{Width=32,Height=32,CornerRadius=new CornerRadius(16),Background=Brush(active?on:(light?"#26000000":"#33FFFFFF")),Child=new TextBlock{Text=glyph,FontFamily=new FontFamily(Ui.Icons),FontSize=14,Foreground=Brushes.White,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center}};row.Children.Add(circle);
    var text=new TextBlock{Text=label,FontSize=12.5,FontWeight=FontWeights.SemiBold,Foreground=Brush(ink),VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(9,0,0,0),TextTrimming=TextTrimming.CharacterEllipsis};Grid.SetColumn(text,1);row.Children.Add(text);
    row.MouseLeftButtonUp+=(s,e)=>{try{action();}catch(Exception ex){report(ex.Message);}};return row;};
   // Row 1: connectivity on the left, now playing on the right.
   var top=new Grid();top.ColumnDefinitions.Add(new ColumnDefinition());top.ColumnDefinitions.Add(new ColumnDefinition());
   var net=new StackPanel();var wifiRow=roundToggle("","Wi-Fi",true,()=>Open("ms-settings:network-wifi"));net.Children.Add(wifiRow);
   net.Children.Add(roundToggle("","Bluetooth",true,()=>Open("ms-settings:bluetooth")));net.Children.Add(roundToggle("","Screen Mirroring",false,()=>Keys.Win(0x4B)));
   top.Children.Add(Tile(net));
   var playing=new StackPanel();var m=media.State;
   var art=new Border{Width=44,Height=44,CornerRadius=new CornerRadius(8),Background=m.Has&&m.Art!=null?(Brush)new ImageBrush(m.Art){Stretch=Stretch.UniformToFill}:Brush(light?"#1F000000":"#33FFFFFF"),HorizontalAlignment=HorizontalAlignment.Left};playing.Children.Add(art);
   playing.Children.Add(new TextBlock{Text=m.Has?m.Title:"Not Playing",FontSize=12.5,FontWeight=FontWeights.SemiBold,Foreground=Brush(ink),Margin=new Thickness(0,8,0,0),TextTrimming=TextTrimming.CharacterEllipsis});
   playing.Children.Add(new TextBlock{Text=m.Has?(m.Artist.Length>0?m.Artist:m.App):"",FontSize=11.5,Foreground=Brush(soft),TextTrimming=TextTrimming.CharacterEllipsis});
   var transport=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(-6,4,0,0)};
   Func<string,Action,Button> small=(glyph,action)=>{var b=new Button{Content=new TextBlock{Text=glyph,FontFamily=new FontFamily(Ui.Icons),FontSize=14,Foreground=Brush(ink)},Width=32,Height=28,Background=Brushes.Transparent,BorderThickness=new Thickness(0),Padding=new Thickness(0),Cursor=Cursors.Hand,Template=CircleTemplate()};b.Click+=(s,e)=>{try{action();}catch(Exception ex){report(ex.Message);}};return b;};
   transport.Children.Add(small("",media.Previous));transport.Children.Add(small(m.Playing?"":"",media.PlayPause));transport.Children.Add(small("",media.Next));playing.Children.Add(transport);
   var playTile=Tile(playing);Grid.SetColumn(playTile,1);top.Children.Add(playTile);root.Children.Add(top);
   // Row 2: focus and dark mode.
   var row2=new Grid();row2.ColumnDefinitions.Add(new ColumnDefinition());row2.ColumnDefinitions.Add(new ColumnDefinition());
   row2.Children.Add(Tile(roundToggle("","Focus",Focus.Running,()=>{if(!Focus.Running)StartFocus(Profile.FocusMinutes);else Focus.Toggle(DateTime.UtcNow);CloseControlCenter();})));
   bool dark=SystemControls.DarkMode;var darkTile=Tile(roundToggle("",dark?"Dark Mode":"Light Mode",dark,()=>{CloseControlCenter();if(SwitchAppearance!=null)SwitchAppearance(!dark);else SystemControls.DarkMode=!dark;}));Grid.SetColumn(darkTile,1);row2.Children.Add(darkTile);root.Children.Add(row2);
   // Display and sound sliders.
   Func<string,string,double,Action<double>,Action,Border> sliderTile=(title,glyph,value,set,more)=>{var stack=new StackPanel();var head=new Grid();head.Children.Add(new TextBlock{Text=title,FontSize=12.5,FontWeight=FontWeights.SemiBold,Foreground=Brush(ink)});
    if(more!=null){var link=new TextBlock{Text="",FontFamily=new FontFamily(Ui.Icons),FontSize=11,Foreground=Brush(soft),HorizontalAlignment=HorizontalAlignment.Right,Cursor=Cursors.Hand,Background=Brushes.Transparent};link.MouseLeftButtonUp+=(s,e)=>more();head.Children.Add(link);}
    stack.Children.Add(head);var line=new Grid{Margin=new Thickness(0,8,0,0)};line.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});line.ColumnDefinitions.Add(new ColumnDefinition());
    line.Children.Add(new TextBlock{Text=glyph,FontFamily=new FontFamily(Ui.Icons),FontSize=13,Foreground=Brush(soft),VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,8,0)});
    var slider=new Slider{Minimum=0,Maximum=100,Value=value,VerticalAlignment=VerticalAlignment.Center};slider.ValueChanged+=(s,e)=>{try{set(slider.Value);}catch(Exception){}};Grid.SetColumn(slider,1);line.Children.Add(slider);stack.Children.Add(line);return Tile(stack);};
   int brightness=SystemControls.Brightness;
   if(brightness>=0)root.Children.Add(sliderTile("Display","",brightness,v=>SystemControls.Brightness=(int)v,()=>Open("ms-settings:display")));
   else{var display=Tile(roundToggle("","Display, night light, and HDR",false,()=>Open("ms-settings:display")));root.Children.Add(display);}
   double volume=SystemControls.Volume;
   root.Children.Add(sliderTile("Sound",SystemControls.Muted?"":"",volume<0?0:volume*100,v=>{SystemControls.Volume=v/100;if(SystemControls.Muted&&v>0)SystemControls.Muted=false;},()=>Open("ms-settings:sound")));
   // Shortcuts.
   var shortcuts=new UniformGrid2Wrap();
   foreach(var s in new[]{new[]{"","Calculator","calc.exe"},new[]{"","Focus timer","focus"},new[]{"","Screenshot","ms-screenclip:"},new[]{"","Lock","lock"}}){string target=s[2];var b=new Button{Width=56,Height=56,Margin=new Thickness(5),Background=Brush(tile),BorderThickness=new Thickness(0),Cursor=Cursors.Hand,ToolTip=s[1],Template=CircleTemplate(),Content=new TextBlock{Text=s[0],FontFamily=new FontFamily(Ui.Icons),FontSize=18,Foreground=Brush(ink)}};
    b.Click+=(o,e)=>{try{CloseControlCenter();if(target=="focus")StartFocus(Profile.FocusMinutes);else if(target=="lock")LockWorkStation();else Open(target);}catch(Exception ex){report(ex.Message);}};shortcuts.Panel.Children.Add(b);}
   root.Children.Add(shortcuts.Panel);
   var panel=new Border{Background=Brush(light?"#FAECECF0":"#FA1C1C1E"),BorderBrush=Brush(light?"#33FFFFFF":"#33FFFFFF"),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(22),Padding=new Thickness(7),Margin=new Thickness(16),Child=root,Effect=new System.Windows.Media.Effects.DropShadowEffect{BlurRadius=30,ShadowDepth=6,Opacity=0.35}};
   controlCenter=new Window{Title="WinDotra Control Center",WindowStyle=WindowStyle.None,AllowsTransparency=true,Background=Brushes.Transparent,ShowInTaskbar=false,Topmost=true,ResizeMode=ResizeMode.NoResize,SizeToContent=SizeToContent.WidthAndHeight,Content=panel,FontFamily=Ui.SansFamily};
   if(Application.Current!=null&&Application.Current.MainWindow!=null)controlCenter.Resources=Application.Current.MainWindow.Resources;
   var screen=PrimaryDip(null,false);controlCenter.Left=screen.Right-340-16-14-16;controlCenter.Top=screen.Top+MenuBarHeight-10;
   // Closes when you click anywhere else (but not in the instant it opens, while the click that opened it finishes).
   var opened=DateTime.Now;var self=controlCenter;
   self.Closing+=(s,e)=>{if(controlCenter==self){controlCenter=null;controlClosed=DateTime.Now;}};
   self.Deactivated+=(s,e)=>{if(controlCenter==self&&(DateTime.Now-opened).TotalMilliseconds>400)self.Close();};
   controlCenter.Show();controlCenter.Activate();panel.BeginAnimation(UIElement.OpacityProperty,new DoubleAnimation(0,1,TimeSpan.FromMilliseconds(160)));
   // The Wi-Fi name takes a moment to read, so it fills in after the panel opens.
   Task.Run(()=>SystemControls.WifiName()).ContinueWith(t=>{if(t.Result!=null)((TextBlock)wifiRow.Children[1]).Text="Wi-Fi  ·  "+t.Result;},TaskScheduler.FromCurrentSynchronizationContext());
  }
  void CloseControlCenter(){var w=controlCenter;controlCenter=null;controlClosed=DateTime.Now;if(w!=null)w.Close();}
  class UniformGrid2Wrap { public readonly WrapPanel Panel=new WrapPanel{HorizontalAlignment=HorizontalAlignment.Center,Margin=new Thickness(0,2,0,2)}; }
 }
}
