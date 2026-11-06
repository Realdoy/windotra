using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace WinCanvas {
 public partial class Studio {
  // Saves and, if the desktop is running, rebuilds it so changes show right away.
  void RefreshDesktop(){Save();if(Overlays.Active||(!smoke&&(Profile.MenuBar||Profile.Dock))){Overlays.Profile=Profile;Overlays.Start();}}
  static readonly string[][] IslandColors={new[]{"Black","#000000"},new[]{"Graphite","#1C1C1E"},new[]{"Glass","#B31C1C1E"},new[]{"Midnight","#0B1A33"},new[]{"Plum","#2A1238"},new[]{"Forest","#0F2A1E"},new[]{"Wine","#3A0F1C"}};
  // How the island looks: shape, size, color, and what it says when nothing is going on.
  void IslandDesign() {
   var design=Card("Design","Make the island yours. Changes show right away on your desktop.");
   design.Children.Add(Text("Shape",13,Ui.Soft));var shapes=new WrapPanel();
   foreach(var shape in new[]{new[]{"Pill","Pill  ·  like the iPhone"},new[]{"Notch","Notch  ·  hangs from the top, like a MacBook"},new[]{"Rounded","Rounded rectangle"}}){string id=shape[0];shapes.Children.Add(Chip(shape[1],Profile.IslandShape==id,()=>{Profile.IslandShape=id;Profile.Positions.Remove("island");RefreshDesktop();Page(current);}));}
   design.Children.Add(shapes);
   design.Children.Add(Text("Color",13,Ui.Soft));var colors=new WrapPanel();
   foreach(var c in IslandColors){string hex=c[1];var dot=new StackPanel{Orientation=Orientation.Horizontal};dot.Children.Add(new Border{Width=16,Height=16,CornerRadius=new CornerRadius(8),Background=Brush(hex),BorderBrush=Brush("#55FFFFFF"),BorderThickness=new Thickness(1),Margin=new Thickness(0,0,8,0)});dot.Children.Add(new TextBlock{Text=c[0],VerticalAlignment=VerticalAlignment.Center});
    var chip=Chip("",Profile.IslandColor==hex,()=>{Profile.IslandColor=hex;RefreshDesktop();Page(current);});chip.Content=dot;colors.Children.Add(chip);}
   colors.Children.Add(Button("Custom…",()=>PickHex(Profile.IslandColor.Length==9?"#"+Profile.IslandColor.Substring(3):Profile.IslandColor,hex=>{if(Ui.IsLight(hex))throw new InvalidOperationException("Pick a darker color: the island's text is white.");Profile.IslandColor=hex;RefreshDesktop();Page(current);})));
   design.Children.Add(colors);
   design.Children.Add(Text("When nothing is playing, it shows",13,Ui.Soft));var idle=new WrapPanel();
   foreach(string option in DesktopProfile.Idles){string id=option;idle.Children.Add(Chip(id=="Text"?"My own text":id,Profile.IslandIdle==id,()=>{Profile.IslandIdle=id;RefreshDesktop();Page(current);}));}
   design.Children.Add(idle);
   if(Profile.IslandIdle=="Text"){var own=new TextBox{Text=Profile.IslandText,MaxLength=40,Margin=new Thickness(0,4,0,8),ToolTip="Up to 40 characters"};own.LostKeyboardFocus+=(s,e)=>{if(own.Text!=Profile.IslandText){Profile.IslandText=own.Text;RefreshDesktop();}};design.Children.Add(own);}
   Slider(design,"Size",Profile.IslandScale,0.8,1.4,v=>Profile.IslandScale=Math.Round(v,2));OnRelease(design,RefreshDesktop);
  }
  void IslandPage() {
   Header("Island","Your Dynamic Island: how it looks, what it shows, its round buttons, and your own quick-launch apps.");
   PageSwitch("Dynamic Island",Profile.Island,on=>{Profile.Island=on;RefreshDesktop();});
   var actions=new WrapPanel{Margin=new Thickness(0,0,0,6)};
   actions.Children.Add(Primary("Center at top",()=>{Overlays.CenterIsland();Save();Report("Island centered at the top of its screen.");}));
   actions.Children.Add(Button("Open the island",()=>{Profile.Island=true;Save();Overlays.Profile=Profile;if(!Overlays.Active)Overlays.Start();Overlays.OpenAssistant();}));
   body.Children.Add(actions);body.Children.Add(Text("Tip: right-click the island anytime to center it.",12,Ui.Faint));
   IslandDesign();
   var shows=Card("What it shows","");
   Check(shows,"Island on",Profile.Island,v=>{Profile.Island=v;RefreshDesktop();});
   Check(shows,"Now playing  ·  song, art, controls, live waveform",Profile.IslandMedia,v=>{Profile.IslandMedia=v;RefreshDesktop();});
   Check(shows,"Downloads in progress",Profile.IslandDownloads,v=>{Profile.IslandDownloads=v;RefreshDesktop();});
   Check(shows,"Open when the pointer is over it",Profile.IslandHover,v=>{Profile.IslandHover=v;RefreshDesktop();});
   Check(shows,"Rainbow aura while playing or downloading",Profile.IslandAura,v=>{Profile.IslandAura=v;RefreshDesktop();});
   var dock=Card("Round buttons","The row of circles in the open island.");
   Check(dock,"Ask AI",Profile.DockAi,v=>{Profile.DockAi=v;RefreshDesktop();});Check(dock,"Focus timer",Profile.DockFocus,v=>{Profile.DockFocus=v;RefreshDesktop();});
   Check(dock,"Tasks",Profile.DockTasks,v=>{Profile.DockTasks=v;RefreshDesktop();});Check(dock,"System  ·  CPU, memory, and your quick-launch buttons",Profile.DockSystem,v=>{Profile.DockSystem=v;RefreshDesktop();});
   var launch=Card("Quick launch","Apps, folders, and websites in the island's System panel. Up to 12.");
   for(int i=0;i<Profile.Launchers.Count;i++){
    int index=i;var item=Profile.Launchers[i];if(i>0)Divider(launch);
    var row=new Grid{Margin=new Thickness(0,6,0,6)};row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(40)});row.ColumnDefinitions.Add(new ColumnDefinition());row.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
    var icon=item.Icon;row.Children.Add(icon!=null?(UIElement)new Image{Source=icon,Width=22,Height=22,HorizontalAlignment=HorizontalAlignment.Left}:new TextBlock{Text=item.Glyph,FontFamily=new FontFamily(Ui.Icons),FontSize=18,Foreground=Brush(Ui.Soft),VerticalAlignment=VerticalAlignment.Center});
    var names=new StackPanel{VerticalAlignment=VerticalAlignment.Center};var name=new TextBox{Text=item.Name,MaxLength=60,Padding=new Thickness(8,4,8,4),Margin=new Thickness(0,0,12,2)};name.LostKeyboardFocus+=(s,e)=>{if(name.Text.Trim().Length>0&&name.Text!=item.Name){item.Name=name.Text.Trim();RefreshDesktop();}};
    names.Children.Add(name);names.Children.Add(new TextBlock{Text=item.Target,FontSize=11,Foreground=Brush(Ui.Faint),TextTrimming=TextTrimming.CharacterEllipsis});Grid.SetColumn(names,1);row.Children.Add(names);
    var tools=new StackPanel{Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center};Grid.SetColumn(tools,2);
    tools.Children.Add(Ghost("Up",()=>{if(index==0)return;Profile.Launchers.RemoveAt(index);Profile.Launchers.Insert(index-1,item);RefreshDesktop();Page(current);}));
    tools.Children.Add(Ghost("Down",()=>{if(index>=Profile.Launchers.Count-1)return;Profile.Launchers.RemoveAt(index);Profile.Launchers.Insert(index+1,item);RefreshDesktop();Page(current);}));
    tools.Children.Add(Ghost("Remove",()=>{Profile.Launchers.RemoveAt(index);RefreshDesktop();Page(current);}));row.Children.Add(tools);launch.Children.Add(row);
   }
   Action<LaunchItem> add=item=>{if(Profile.Launchers.Count>=12)throw new InvalidOperationException("The island holds up to 12 buttons. Remove one first.");Profile.Launchers.Add(item);RefreshDesktop();Page(current);Report("Added "+item.Name+" to the island.");};
   var adders=new WrapPanel{Margin=new Thickness(0,10,0,0)};
   adders.Children.Add(Button("Add app…",()=>{var dialog=new Microsoft.Win32.OpenFileDialog{Title="Choose an app or shortcut",Filter="Apps and shortcuts|*.exe;*.lnk;*.url;*.appref-ms|All files|*.*",InitialDirectory=Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms),DereferenceLinks=false};if(dialog.ShowDialog(this)==true)add(new LaunchItem{Name=Path.GetFileNameWithoutExtension(dialog.FileName),Target=dialog.FileName});}));
   adders.Children.Add(Button("Add folder…",()=>{using(var dialog=new System.Windows.Forms.FolderBrowserDialog{Description="Choose a folder for the island"}){if(dialog.ShowDialog()==System.Windows.Forms.DialogResult.OK)add(new LaunchItem{Name=new DirectoryInfo(dialog.SelectedPath).Name,Target=dialog.SelectedPath});}}));
   launch.Children.Add(adders);
   var web=new Grid{Margin=new Thickness(0,6,0,0)};web.ColumnDefinitions.Add(new ColumnDefinition());web.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});var url=new TextBox{Text="https://",MaxLength=1000};web.Children.Add(url);
   var addWeb=Button("Add website",()=>{Uri uri;if(!Uri.TryCreate(url.Text.Trim(),UriKind.Absolute,out uri)||(uri.Scheme!="https"&&uri.Scheme!="http"))throw new InvalidOperationException("Type a full web address, like https://youtube.com");add(new LaunchItem{Name=uri.Host.Replace("www.",""),Target=uri.AbsoluteUri});});addWeb.Margin=new Thickness(8,0,0,0);Grid.SetColumn(addWeb,1);web.Children.Add(addWeb);launch.Children.Add(web);
   launch.Children.Add(Ghost("Reset to the default buttons",()=>{Profile.Launchers=LaunchItem.Defaults();RefreshDesktop();Page(current);}));
   var size=Card("Size","");Slider(size,"Open island width",Math.Max(340,Math.Min(400,Profile.IslandExpandedWidth)),340,400,v=>Profile.IslandExpandedWidth=v);OnRelease(size,RefreshDesktop);
  }
 }
}
