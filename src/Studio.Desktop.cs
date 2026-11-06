using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace WinCanvas {
 public partial class Studio {
  // The macOS-style menu bar and Dock, both optional.
  void DesktopPage() {
   Header("Menu bar & Dock","A macOS-style menu bar across the top and a Dock at the bottom. The island sits in the middle of the menu bar, like the notch.");
   var top=Card("Menu bar","The app in front (bold), Window and Go menus, and on the right battery, Wi-Fi, sound, search, Control Center, and the clock. The logo menu has Sleep, Restart, Shut Down, and Lock.");
   Check(top,"Show the menu bar",Profile.MenuBar,v=>{Profile.MenuBar=v;if(v&&Profile.Island&&Profile.IslandShape=="Pill"){Profile.IslandShape="Notch";Profile.Positions.Remove("island");}RefreshDesktop();Page(current);});
   Check(top,"Light menu bar  ·  dark text on frosted white",Profile.MenuBarLight,v=>{Profile.MenuBarLight=v;RefreshDesktop();});
   Check(top,"Keep windows below it  ·  maximized apps start under the menu bar",Profile.MenuBarReserve,v=>{Profile.MenuBarReserve=v;RefreshDesktop();});
   top.Children.Add(Text("Tip: set the island's shape to Notch on the Island page so it hangs from the top edge.",12,Ui.Faint));
   var dock=Card("Dock","Your apps at the bottom of the screen. Running apps get a dot, icons grow under the pointer, and right-click an icon to keep, remove, or quit it.");
   Check(dock,"Show the Dock  ·  hides the Windows taskbar while it's on",Profile.Dock,v=>{Profile.Dock=v;if(v&&Profile.DockApps.Count==0)Profile.DockApps=OverlayManager.DockDefaults();TaskbarForDock(v);RefreshDesktop();Page(current);});
   Check(dock,"macOS icon shape  ·  every icon on the same rounded tile (right-click an icon to give it your own picture)",Profile.DockTiles,v=>{Profile.DockTiles=v;RefreshDesktop();});
   Check(dock,"Magnify icons under the pointer",Profile.DockMagnify,v=>{Profile.DockMagnify=v;RefreshDesktop();});
   Check(dock,"Show running apps that aren't in the Dock",Profile.DockRunning,v=>{Profile.DockRunning=v;RefreshDesktop();});
   Check(dock,"Light Dock  ·  frosted white",Profile.DockLight,v=>{Profile.DockLight=v;RefreshDesktop();});
   Check(dock,"Hide the Dock until the pointer touches the bottom edge",Profile.DockAutoHide,v=>{Profile.DockAutoHide=v;RefreshDesktop();});
   Slider(dock,"Icon size",Profile.DockSize,32,96,v=>Profile.DockSize=Math.Round(v));OnRelease(dock,RefreshDesktop);
   bool hidden=false;try{hidden=OverlayManager.TaskbarAutoHide;}catch(Exception){}
   Check(dock,"Auto-hide the Windows taskbar  ·  so only the Dock shows (Windows setting, switch back here anytime)",hidden,v=>Safe(()=>{OverlayManager.TaskbarAutoHide=v;Report(v?"The Windows taskbar now hides until you point at the bottom edge.":"The Windows taskbar is back.");}));
   var apps=Card("Apps in the Dock","Up to 30. Apps, folders, shortcuts, and websites.");
   for(int i=0;i<Profile.DockApps.Count;i++){
    int index=i;var item=Profile.DockApps[i];if(i>0)Divider(apps);
    var row=new Grid{Margin=new Thickness(0,5,0,5)};row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(40)});row.ColumnDefinitions.Add(new ColumnDefinition());row.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
    var icon=ShellIcon.Get(Path.IsPathRooted(item.Target)?item.Target:Path.Combine(Environment.SystemDirectory,item.Target),32)??item.Icon;row.Children.Add(icon!=null?(UIElement)new Image{Source=icon,Width=24,Height=24,HorizontalAlignment=HorizontalAlignment.Left}:new TextBlock{Text=item.Glyph,FontFamily=new FontFamily(Ui.Icons),FontSize=18,Foreground=Brush(Ui.Soft),VerticalAlignment=VerticalAlignment.Center});
    var names=new StackPanel{VerticalAlignment=VerticalAlignment.Center};names.Children.Add(new TextBlock{Text=item.Name,FontWeight=FontWeights.SemiBold});names.Children.Add(new TextBlock{Text=item.Target,FontSize=11,Foreground=Brush(Ui.Faint),TextTrimming=TextTrimming.CharacterEllipsis});Grid.SetColumn(names,1);row.Children.Add(names);
    var tools=new StackPanel{Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center};Grid.SetColumn(tools,2);
    tools.Children.Add(Ghost("Left",()=>{if(index==0)return;Profile.DockApps.RemoveAt(index);Profile.DockApps.Insert(index-1,item);RefreshDesktop();Page(current);}));
    tools.Children.Add(Ghost("Right",()=>{if(index>=Profile.DockApps.Count-1)return;Profile.DockApps.RemoveAt(index);Profile.DockApps.Insert(index+1,item);RefreshDesktop();Page(current);}));
    tools.Children.Add(Ghost("Remove",()=>{Profile.DockApps.RemoveAt(index);RefreshDesktop();Page(current);}));row.Children.Add(tools);apps.Children.Add(row);
   }
   Action<LaunchItem> add=item=>{if(Profile.DockApps.Count>=30)throw new InvalidOperationException("The Dock holds up to 30 apps. Remove one first.");Profile.DockApps.Add(item);RefreshDesktop();Page(current);Report("Added "+item.Name+" to the Dock.");};
   var adders=new WrapPanel{Margin=new Thickness(0,10,0,0)};
   adders.Children.Add(Button("Add app…",()=>{var dialog=new Microsoft.Win32.OpenFileDialog{Title="Choose an app or shortcut",Filter="Apps and shortcuts|*.exe;*.lnk;*.url;*.appref-ms|All files|*.*",InitialDirectory=Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms),DereferenceLinks=false};if(dialog.ShowDialog(this)==true)add(new LaunchItem{Name=Path.GetFileNameWithoutExtension(dialog.FileName),Target=dialog.FileName});}));
   adders.Children.Add(Button("Add folder…",()=>{using(var dialog=new System.Windows.Forms.FolderBrowserDialog{Description="Choose a folder for the Dock"}){if(dialog.ShowDialog()==System.Windows.Forms.DialogResult.OK)add(new LaunchItem{Name=new DirectoryInfo(dialog.SelectedPath).Name,Target=dialog.SelectedPath});}}));
   adders.Children.Add(Button("Find my apps again",()=>{Profile.DockApps=OverlayManager.DockDefaults();RefreshDesktop();Page(current);Report("The Dock has the apps WinDotra found on this PC.");}));
   apps.Children.Add(adders);
   var web=new Grid{Margin=new Thickness(0,6,0,0)};web.ColumnDefinitions.Add(new ColumnDefinition());web.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});var url=new TextBox{Text="https://",MaxLength=1000};web.Children.Add(url);
   var addWeb=Button("Add website",()=>{Uri uri;if(!Uri.TryCreate(url.Text.Trim(),UriKind.Absolute,out uri)||(uri.Scheme!="https"&&uri.Scheme!="http"))throw new InvalidOperationException("Type a full web address, like https://youtube.com");add(new LaunchItem{Name=uri.Host.Replace("www.",""),Target=uri.AbsoluteUri});});addWeb.Margin=new Thickness(8,0,0,0);Grid.SetColumn(addWeb,1);web.Children.Add(addWeb);apps.Children.Add(web);
   body.Children.Add(Primary(Overlays.Active?"Save & refresh desktop":"Save & launch",Launch));
  }
 }
}
