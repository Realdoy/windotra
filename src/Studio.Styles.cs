using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace WinCanvas {
 public partial class Studio {
  static string Setting(StyleFile style,string key,string fallback){object value;return style.Settings.TryGetValue(key,out value)&&value!=null&&value.ToString().Length>0?value.ToString():fallback;}
  // A small picture of a style: its wallpaper colors, a widget tile, and the island.
  // A miniature desktop for a style: its real wallpaper scene, menu bar, island, a widget, and the Dock.
  FrameworkElement StylePreview(StyleFile style) {
   string bg=Setting(style,"Background","#1C1C1E"),accent=Setting(style,"Accent","#0A84FF");
   DesktopProfile look;try{look=Styles.Apply(new DesktopProfile(),style);}catch(Exception){look=new DesktopProfile();}
   var grid=new Grid{Height=132,ClipToBounds=true};var options=SceneOptions.From(look);if(options.Kind=="Photo"||options.Kind=="Video")options.Kind="Gradient";grid.Children.Add(new WallpaperScene(options));
   if(look.MenuBar)grid.Children.Add(new Border{Height=9,VerticalAlignment=VerticalAlignment.Top,Background=Brush(look.MenuBarLight?"#C8F2F2F7":"#B0161618")});
   if(look.Island){bool notch=look.IslandShape=="Notch";grid.Children.Add(new Border{Width=74*look.IslandScale,Height=notch?16:18,CornerRadius=notch?new CornerRadius(0,0,8,8):new CornerRadius(look.IslandShape=="Rounded"?6:9),Background=Brush(look.IslandColor),VerticalAlignment=VerticalAlignment.Top,Margin=new Thickness(0,notch?0:(look.MenuBar?13:6),0,0)});}
   if(look.Dock){var dock=new StackPanel{Orientation=Orientation.Horizontal};string[] colors={"#0A84FF","#34C759","#FF9F0A","#FF375F","#AF52DE","#5AC8FA","#8E8E93"};foreach(var c in colors)dock.Children.Add(new Border{Width=11,Height=11,CornerRadius=new CornerRadius(3),Background=Brush(c),Margin=new Thickness(1.5)});grid.Children.Add(new Border{HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Bottom,Margin=new Thickness(0,0,0,5),Padding=new Thickness(3),CornerRadius=new CornerRadius(6),Background=Brush(look.DockLight?"#B8F5F5F7":"#A01E1E20"),Child=dock});}
   var pv=new Palette(bg,accent);var tile=new StackPanel{Margin=new Thickness(9,6,9,6)};tile.Children.Add(new TextBlock{Text="10:09",FontSize=15,FontWeight=FontWeights.Light,Foreground=Brush(pv.Ink),FontFamily=FontLibrary.For(Setting(style,"Font","Inter"))});tile.Children.Add(new Border{Height=4,Width=50,CornerRadius=new CornerRadius(2),Background=Brush(accent),HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(0,4,0,0)});
   double radius=0;Double.TryParse(Setting(style,"Radius","20"),System.Globalization.NumberStyles.Any,System.Globalization.CultureInfo.InvariantCulture,out radius);
   if(look.Day){grid.Children.Add(new TextBlock{Text="F R I D A Y",FontFamily=Ui.SansFamily,FontWeight=FontWeights.Light,FontSize=20,Foreground=Brushes.White,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center});}
   else grid.Children.Add(new Border{Width=82,Height=52,CornerRadius=new CornerRadius(Math.Min(14,radius*0.5)),Background=Brush(bg),Opacity=look.Glass?0.9:1,HorizontalAlignment=look.Positions.ContainsKey("calendar")&&look.Positions["calendar"].X>400?HorizontalAlignment.Right:HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Top,Margin=new Thickness(10,look.MenuBar?16:10,10,10),Child=tile,Effect=new System.Windows.Media.Effects.DropShadowEffect{BlurRadius=10,ShadowDepth=2,Opacity=0.35}});
   return new Border{CornerRadius=new CornerRadius(10),ClipToBounds=true,Child=grid};
  }
  // Switches between Apple and Apple Dark (used by Dark Mode in the Control Center), Windows' own mode included.
  public void AppleAppearance(bool dark) {
   SystemControls.DarkMode=dark;
   var style=Styles.All(folder).FirstOrDefault(x=>x.Name==(dark?"Apple Dark":"Apple"));if(style==null)return;
   Profile=Styles.Apply(Profile,style);Profile.Off=false;Save();Overlays.Profile=Profile;Overlays.Start();AppleExtras(dark);if(current=="Styles"||current=="Home")Page(current);Report((dark?"Dark":"Light")+" mode: "+style.Name+" style is on.");
  }
  void StylesPage() {
   Header("Styles","Complete looks in one click. Make your own and share it: a style is a small file anyone can import.");
   var gallery=new UniformGrid{Columns=3,Margin=new Thickness(0,0,-12,10)};
   foreach(var style in Styles.All(folder)) {
    var s=style;var card=new StackPanel{Margin=new Thickness(12,12,12,14)};card.Children.Add(StylePreview(s));
    card.Children.Add(new TextBlock{Text=s.Name,FontSize=15,FontWeight=FontWeights.Bold,Margin=new Thickness(2,10,0,2)});
    card.Children.Add(new TextBlock{Text=s.Description,FontSize=12,Foreground=Brush(Ui.Soft),TextWrapping=TextWrapping.Wrap,Height=50,Margin=new Thickness(2,0,0,2)});
    card.Children.Add(new TextBlock{Text="by "+(s.Author.Length>0?s.Author:"someone"),FontSize=11,Foreground=Brush(Ui.Faint),Margin=new Thickness(2,0,0,8)});
    var use=Primary("Use this style",()=>{Profile=Styles.Apply(Profile,s);Profile.Off=false;Save();Overlays.Profile=Profile;Overlays.Start();string extras=s.Name.StartsWith("Apple")?AppleExtras(s.Name.Contains("Dark")):"";if(!s.Name.StartsWith("Apple"))TaskbarForDock(Profile.Dock);Page(current);Report(s.Name+" is on: wallpaper, widgets, island"+(Profile.MenuBar?", menu bar":"")+(Profile.Dock?", Dock":"")+(extras.Length>0?", "+extras:"")+". Changed your mind? Home → Back to normal Windows (or Ctrl+Alt+Shift+W).");});use.Margin=new Thickness(2,0,0,0);card.Children.Add(use);
    gallery.Children.Add(new Border{Background=Brush(Ui.Surface),CornerRadius=new CornerRadius(14),Margin=new Thickness(0,0,12,12),Child=card});
   }
   body.Children.Add(gallery);
   var make=Card("Make your own","Set things up the way you like in Widgets, Island, Live wallpaper, and Typography, then save it as a style. Styles contain only the look, never your notes, tasks, apps, or positions.");
   var nameBox=new TextBox{Text="My style",MaxLength=60,Margin=new Thickness(0,4,0,6)};var authorBox=new TextBox{Text="",MaxLength=40,Margin=new Thickness(0,0,0,6),ToolTip="Your name or GitHub username (optional)"};var aboutBox=new TextBox{Text="",MaxLength=160,Margin=new Thickness(0,0,0,8),ToolTip="One sentence about your style"};
   make.Children.Add(Text("Name",12,Ui.Faint));make.Children.Add(nameBox);make.Children.Add(Text("Author (optional)",12,Ui.Faint));make.Children.Add(authorBox);make.Children.Add(Text("Description",12,Ui.Faint));make.Children.Add(aboutBox);
   Func<StyleFile> capture=()=>{if(String.IsNullOrWhiteSpace(nameBox.Text))throw new InvalidOperationException("Give your style a name.");return Styles.Capture(Profile,nameBox.Text.Trim(),authorBox.Text.Trim(),aboutBox.Text.Trim());};
   var buttons=new WrapPanel();
   buttons.Children.Add(Primary("Save to my styles",()=>{var style=capture();Directory.CreateDirectory(Styles.Folder(folder));Styles.Save(style,Path.Combine(Styles.Folder(folder),Safe(style.Name)+Styles.Extension));Page(current);Report(style.Name+" saved. It's in the gallery above.");}));
   buttons.Children.Add(Button("Export to share…",()=>{var style=capture();var dialog=new Microsoft.Win32.SaveFileDialog{Filter="WinDotra style|*"+Styles.Extension,FileName=Safe(style.Name)+Styles.Extension};if(dialog.ShowDialog(this)==true){Styles.Save(style,dialog.FileName);Report("Exported. Send the file to friends, or share it with the WinDotra community (see Sharing below).");}}));
   buttons.Children.Add(Button("Import a style…",()=>{var dialog=new Microsoft.Win32.OpenFileDialog{Filter="WinDotra style|*"+Styles.Extension};if(dialog.ShowDialog(this)!=true)return;var style=Styles.Read(dialog.FileName);Directory.CreateDirectory(Styles.Folder(folder));File.Copy(dialog.FileName,Path.Combine(Styles.Folder(folder),Safe(style.Name)+Styles.Extension),true);Page(current);Report(style.Name+" imported. Click Use this style to try it.");}));
   buttons.Children.Add(Ghost("Open styles folder",()=>{Directory.CreateDirectory(Styles.Folder(folder));System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Styles.Folder(folder)){UseShellExecute=true});}));
   make.Children.Add(buttons);
   var share=Card("Sharing","WinDotra is open source. Community styles live in the project's styles folder: add your .windotra-style file there with a pull request on GitHub, and it ships to everyone. Styles are plain text, so anyone can check them before using them.");
   share.Children.Add(Text("Style files can only change colors, fonts, sizes, widget choices, the island's look, and the live wallpaper. WinDotra rejects files that try anything else.",12,Ui.Faint));
  }
  static string Safe(string name){foreach(char c in Path.GetInvalidFileNameChars())name=name.Replace(c,'-');return name.Trim();}
 }
}
