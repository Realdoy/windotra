using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace WinCanvas {
 public class StoreItem { public string Type="",Name="",Author="",AuthorUrl="",Description="",File=""; public List<string> Images=new List<string>(); [ScriptIgnore] public string Base; }
 public class StoreIndex { public List<StoreItem> Items=new List<StoreItem>(); }
 public partial class Studio {
  // The community store lives on the project's GitHub Pages site (docs/store). A copy ships inside WinDotra, so the
  // store works offline; the online catalog adds whatever people have shared since.
  static string ReportUrl(string type,string name,string file){return "https://github.com/Realdoy/windotra/issues/new?labels=report&title="+Uri.EscapeDataString("Report "+type+": "+name)+"&body="+Uri.EscapeDataString("Item: "+name+"\nFile: docs/store/"+file+"\n\nWhat's wrong with it?\n");}
  readonly HashSet<string> expandedSections=new HashSet<string>();
  public const string StoreUrl="https://realdoy.github.io/windotra/store/";
  static string LocalStore{get{return Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"store");}}
  static List<StoreItem> onlineItems; static bool onlineTried;
  static List<StoreItem> ReadIndex(string json,string baseLocation){var index=new JavaScriptSerializer().Deserialize<StoreIndex>(json);var list=new List<StoreItem>();if(index==null||index.Items==null)return list;
   foreach(var i in index.Items.Take(500)){if(i!=null&&i.AuthorUrl==null)i.AuthorUrl="";if(i!=null&&(i.Images==null||i.Images.Any(x=>x==null||!ImagePath.IsMatch(x))))i.Images=new List<string>();if(i==null||(i.Type!="style"&&i.Type!="widget")||String.IsNullOrWhiteSpace(i.Name)||i.Name.Length>60||i.File==null||i.File.Contains("..")||i.File.Length>200)continue;i.Base=baseLocation;list.Add(i);}return list;}
  List<StoreItem> Catalog(){var list=new List<StoreItem>();try{string idx=Path.Combine(LocalStore,"index.json");if(File.Exists(idx))list.AddRange(ReadIndex(File.ReadAllText(idx),LocalStore));}catch(Exception){}
   if(onlineItems!=null)foreach(var i in onlineItems)if(!list.Any(x=>x.Type==i.Type&&x.Name==i.Name))list.Add(i);return list;}
  async void LoadOnline(){if(onlineTried)return;onlineTried=true;try{using(var http=new HttpClient{Timeout=TimeSpan.FromSeconds(6)}){http.DefaultRequestHeaders.UserAgent.ParseAdd("WinDotra");string json=await http.GetStringAsync(StoreUrl+"index.json?t="+DateTime.UtcNow.Ticks);if(json.Length>512*1024)return;onlineItems=ReadIndex(json,StoreUrl);Report("Store refreshed: "+onlineItems.Count+" items online.");}if(current=="Store")Dispatcher.BeginInvoke(new Action(()=>Page("Store")));}catch(Exception){onlineItems=null;}}
  async Task<string> Fetch(StoreItem item){if(item.Base.StartsWith("http")){using(var http=new HttpClient{Timeout=TimeSpan.FromSeconds(10)}){http.DefaultRequestHeaders.UserAgent.ParseAdd("WinDotra");var text=await http.GetStringAsync(item.Base+item.File);if(text.Length>64*1024)throw new InvalidDataException("That item is too large.");return text;}}return File.ReadAllText(Path.Combine(item.Base,item.File.Replace('/','\\')));}
  // Downloads (or copies) an item, checks it, and keeps it in your styles or widgets folder.
  async void Install(StoreItem item){
   try{
    string text=await Fetch(item);string temp=Path.Combine(Path.GetTempPath(),"windotra-"+Guid.NewGuid().ToString("N"));File.WriteAllText(temp,text);
    try{
     if(item.Type=="style"){var style=Styles.Read(temp);Directory.CreateDirectory(Styles.Folder(folder));File.Copy(temp,Path.Combine(Styles.Folder(folder),Safe(style.Name)+Styles.Extension),true);Report(style.Name+" installed. It's in Styles: click Use this style.");}
     else{var w=CustomWidget.Read(temp);Directory.CreateDirectory(CustomWidget.Folder(folder));w.Save(Path.Combine(CustomWidget.Folder(folder),Safe(w.Name)+CustomWidget.Extension));if(!Profile.CustomWidgets.Contains(w.Id))Profile.CustomWidgets.Add(w.Id);Save();if(Overlays.Active){Overlays.Profile=Profile;Overlays.Start();}Report(w.Name+" installed and on your desktop. Move it in Edit widgets.");}
    }finally{File.Delete(temp);}
    Page(current);
   }catch(Exception e){Report("Couldn't install "+item.Name+": "+e.Message);}
  }
  FrameworkElement WidgetPreview(CustomWidget w){var pal=new Palette(w.Background.Length==7?w.Background:w.Background.Length==9?"#"+w.Background.Substring(3):Profile.Background,Profile.Accent);var live=new Func<string,string>(k=>k=="cpu"?"23%":k=="ram"?"48%":k=="battery"?"AC power":k=="song"?"Glass Horizon":k=="artist"?"Sample artist":null);
   var inner=OverlayManager.RenderCustom(w,pal,live,null,w.Width==2?300:140,110);return new Border{Opacity=w.Opacity,Background=Brush(pal.Back),CornerRadius=new CornerRadius(Math.Min(w.Radius,26)),Padding=new Thickness(14),HorizontalAlignment=HorizontalAlignment.Left,Child=inner,Effect=new System.Windows.Media.Effects.DropShadowEffect{BlurRadius=16,ShadowDepth=2,Opacity=0.3}};}
  void StorePage() {
   Header("Store","Styles and widgets made by the WinDotra community. Everything here is plain settings and text, never programs, so it's safe to install.");LoadOnline();
   // Refresh: asks the website for the newest list again (new uploads, removed items).
   var refreshRow=new WrapPanel{Margin=new Thickness(0,0,0,6)};refreshRow.Children.Add(Button("↻  Refresh store",()=>{onlineItems=null;onlineTried=false;isOwner=null;Report("Refreshing the store…");LoadOnline();}));body.Children.Add(refreshRow);
   var catalog=Catalog();
   body.Children.Add(Text(onlineItems!=null?"Showing the online store and the items that ship with WinDotra.":"Showing the items that ship with WinDotra. The online store adds more when it's reachable.",12,Ui.Faint));
   foreach(string type in new[]{"widget","style"}){
    var items=catalog.Where(i=>i.Type==type).ToList();if(items.Count==0)continue;
    var section=Card(type=="widget"?"Widgets":"Styles",type=="widget"?"Install one and it appears on your desktop. Move it with Edit widgets.":"Whole desktops: wallpaper, widgets, island, menu bar, and Dock.");
    var grid=new UniformGrid{Columns=3,Margin=new Thickness(0,0,-12,0)};
    bool all=expandedSections.Contains(type);int shown=0;
    foreach(var it in items){var item=it;if(!all&&shown>=6)break;shown++;var card=new StackPanel{Margin=new Thickness(12)};FrameworkElement drawn=null;
     try{var path=item.Base.StartsWith("http")?null:Path.Combine(item.Base,item.File.Replace('/',Path.DirectorySeparatorChar));if(path!=null)drawn=item.Type=="widget"?(FrameworkElement)WidgetPreview(CustomWidget.Read(path)):StylePreview(Styles.Read(path));}catch(Exception){}
     ImageSource picture=item.Images.Count>0?Picture(item,item.Images[0]):null;
     var frame=new Border{Height=150,CornerRadius=new CornerRadius(12),Background=Brush("#111113"),ClipToBounds=true,Cursor=System.Windows.Input.Cursors.Hand,ToolTip="Details"};
     if(picture!=null)frame.Child=new Image{Source=picture,Stretch=Stretch.Uniform,Margin=new Thickness(6)};else if(drawn!=null)frame.Child=new Viewbox{Child=drawn,Stretch=Stretch.Uniform,StretchDirection=StretchDirection.DownOnly,Margin=new Thickness(8)};else frame.Child=new TextBlock{Text="No preview",Foreground=Brush(Ui.Faint),HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};
     FrameworkElement big=null;try{var path=item.Base.StartsWith("http")?null:Path.Combine(item.Base,item.File.Replace('/',Path.DirectorySeparatorChar));if(path!=null)big=item.Type=="widget"?RealWidget(CustomWidget.Read(path)):(FrameworkElement)new Border{Width=640,Child=StylePreview(Styles.Read(path)),LayoutTransform=new ScaleTransform(1.6,1.6)};}catch(Exception){}
     frame.MouseLeftButtonUp+=(o,e)=>ShowDetails(item,big);card.Children.Add(frame);
     card.Children.Add(new TextBlock{Text=item.Name,FontSize=14,FontWeight=FontWeights.SemiBold,Margin=new Thickness(2,10,0,2)});
     card.Children.Add(new TextBlock{Text=item.Description,FontSize=11.5,Foreground=Brush(Ui.Soft),TextWrapping=TextWrapping.Wrap,Height=46,Margin=new Thickness(2,0,0,2)});
     var by=new TextBlock{Text="by "+(item.Author.Length>0?item.Author:"someone")+(item.Base.StartsWith("http")?"  ·  online":""),FontSize=11,Foreground=Brush(item.AuthorUrl.StartsWith("https://github.com/")?"#64B5FF":Ui.Faint),Margin=new Thickness(2,0,0,8)};if(item.AuthorUrl.StartsWith("https://github.com/")){string profile=item.AuthorUrl;by.Cursor=System.Windows.Input.Cursors.Hand;by.ToolTip=profile;by.MouseLeftButtonUp+=(s,e)=>System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(profile){UseShellExecute=true});}card.Children.Add(by);
     var getRow=new StackPanel{Orientation=Orientation.Horizontal};var get=Primary("Get",()=>Install(item));get.Margin=new Thickness(2,0,8,0);get.Padding=new Thickness(18,5,18,5);getRow.Children.Add(get);
     var report=Ghost("Report",()=>System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(ReportUrl(item.Type,item.Name,item.File)){UseShellExecute=true}));report.Foreground=Brush("#FF6961");report.ToolTip="Something wrong or offensive? Tell the maintainers on GitHub.";getRow.Children.Add(report);var details=Ghost("Details",()=>ShowDetails(item,big));getRow.Children.Add(details);
     if(IsStoreOwner()){var remove=Ghost("Remove",()=>RemoveFromStore(item));remove.ToolTip="You own the store: remove this item for everyone";getRow.Children.Add(remove);}card.Children.Add(getRow);
     grid.Children.Add(new Border{Background=Brush(Ui.Surface),CornerRadius=new CornerRadius(16),Margin=new Thickness(0,0,12,12),Child=card});}
    section.Children.Add(grid);
    if(items.Count>6){string t=type;section.Children.Add(Button(all?"Show fewer":"Show all "+items.Count,()=>{if(expandedSections.Contains(t))expandedSections.Remove(t);else expandedSections.Add(t);Page(current);}));}
   }
   // Your own widgets: installed and made here.
   var mine=CustomWidget.All(folder);var yours=Card("Your widgets",mine.Count==0?"None yet. Get one above or make your own below.":"Switch them on and off, edit, or export them to share.");
   foreach(var w0 in mine){var w=w0;var row=new Grid{Margin=new Thickness(0,6,0,6)};row.ColumnDefinitions.Add(new ColumnDefinition());row.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
    var name=new StackPanel{VerticalAlignment=VerticalAlignment.Center};name.Children.Add(new TextBlock{Text=w.Name,FontWeight=FontWeights.SemiBold});name.Children.Add(new TextBlock{Text=(w.Description.Length>0?w.Description:"Custom widget")+(w.Author.Length>0?"  ·  by "+w.Author:""),FontSize=11.5,Foreground=Brush(Ui.Faint),TextTrimming=TextTrimming.CharacterEllipsis});row.Children.Add(name);
    var tools=new StackPanel{Orientation=Orientation.Horizontal};Grid.SetColumn(tools,1);bool on=Profile.CustomWidgets.Contains(w.Id);
    tools.Children.Add(Chip(on?"On desktop":"Off",on,()=>{if(on)Profile.CustomWidgets.Remove(w.Id);else Profile.CustomWidgets.Add(w.Id);RefreshDesktop();Page(current);}));
    tools.Children.Add(Ghost("Edit",()=>EditWidget(w)));tools.Children.Add(Ghost("Publish…",()=>Publish(w.Path,"widget",w.Name)));
    tools.Children.Add(Ghost("Export…",()=>{var d=new Microsoft.Win32.SaveFileDialog{Filter="WinDotra widget|*"+CustomWidget.Extension,FileName=Safe(w.Name)+CustomWidget.Extension};if(d.ShowDialog(this)==true){w.Save(d.FileName);Report("Exported. Share the file, or add it to the store (see Share below).");}}));
    tools.Children.Add(Ghost("Delete",()=>{Profile.CustomWidgets.Remove(w.Id);Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(w.Path,Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);RefreshDesktop();Page(current);}));
    row.Children.Add(tools);yours.Children.Add(row);}
   var importRow=new WrapPanel();importRow.Children.Add(Button("Import a widget or style…",()=>{var d=new Microsoft.Win32.OpenFileDialog{Filter="WinDotra files|*"+CustomWidget.Extension+";*"+Styles.Extension};if(d.ShowDialog(this)!=true)return;Install(new StoreItem{Type=d.FileName.EndsWith(Styles.Extension)?"style":"widget",Name=Path.GetFileNameWithoutExtension(d.FileName),File=Path.GetFileName(d.FileName),Base=Path.GetDirectoryName(d.FileName)});}));yours.Children.Add(importRow);
   WidgetMaker();
   var share=Card("Share yours","Click Publish… on any of your widgets (or Publish to Store on a style in Styles). You sign in with GitHub once; your GitHub name and profile link are shown as the author. Once it's accepted, everyone sees it here.");
   share.Children.Add(Button("Open the store website ↗",()=>System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(StoreUrl.Replace("store/","")){UseShellExecute=true})));
  }
 }
}
