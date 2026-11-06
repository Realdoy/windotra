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
 public class StoreItem { public string Type="",Name="",Author="",Description="",File=""; [ScriptIgnore] public string Base; }
 public class StoreIndex { public List<StoreItem> Items=new List<StoreItem>(); }
 public partial class Studio {
  // The community store lives on the project's GitHub Pages site (docs/store). A copy ships inside WinDotra, so the
  // store works offline; the online catalog adds whatever people have shared since.
  public const string StoreUrl="https://windotra.github.io/windotra/store/";
  static string LocalStore{get{return Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"store");}}
  static List<StoreItem> onlineItems; static bool onlineTried;
  static List<StoreItem> ReadIndex(string json,string baseLocation){var index=new JavaScriptSerializer().Deserialize<StoreIndex>(json);var list=new List<StoreItem>();if(index==null||index.Items==null)return list;
   foreach(var i in index.Items.Take(500)){if(i==null||(i.Type!="style"&&i.Type!="widget")||String.IsNullOrWhiteSpace(i.Name)||i.Name.Length>60||i.File==null||i.File.Contains("..")||i.File.Length>200)continue;i.Base=baseLocation;list.Add(i);}return list;}
  List<StoreItem> Catalog(){var list=new List<StoreItem>();try{string idx=Path.Combine(LocalStore,"index.json");if(File.Exists(idx))list.AddRange(ReadIndex(File.ReadAllText(idx),LocalStore));}catch(Exception){}
   if(onlineItems!=null)foreach(var i in onlineItems)if(!list.Any(x=>x.Type==i.Type&&x.Name==i.Name))list.Add(i);return list;}
  async void LoadOnline(){if(onlineTried)return;onlineTried=true;try{using(var http=new HttpClient{Timeout=TimeSpan.FromSeconds(6)}){http.DefaultRequestHeaders.UserAgent.ParseAdd("WinDotra");string json=await http.GetStringAsync(StoreUrl+"index.json");if(json.Length>512*1024)return;onlineItems=ReadIndex(json,StoreUrl);}if(current=="Store")Dispatcher.BeginInvoke(new Action(()=>Page("Store")));}catch(Exception){onlineItems=null;}}
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
   var inner=OverlayManager.RenderCustom(w,pal,live,null,w.Width==2?300:140,110);return new Border{Background=Brush(pal.Back),CornerRadius=new CornerRadius(18),Padding=new Thickness(14),HorizontalAlignment=HorizontalAlignment.Left,Child=inner,Effect=new System.Windows.Media.Effects.DropShadowEffect{BlurRadius=16,ShadowDepth=2,Opacity=0.3}};}
  string widgetDraft;CustomWidget editing;
  void StorePage() {
   Header("Store","Styles and widgets made by the WinDotra community. Everything here is plain settings and text, never programs, so it's safe to install.");LoadOnline();
   var catalog=Catalog();
   body.Children.Add(Text(onlineItems!=null?"Showing the online store and the items that ship with WinDotra.":"Showing the items that ship with WinDotra. The online store adds more when it's reachable.",12,Ui.Faint));
   foreach(string type in new[]{"widget","style"}){
    var items=catalog.Where(i=>i.Type==type).ToList();if(items.Count==0)continue;
    var section=Card(type=="widget"?"Widgets":"Styles",type=="widget"?"Install one and it appears on your desktop. Move it with Edit widgets.":"Whole desktops: wallpaper, widgets, island, menu bar, and Dock.");
    var grid=new UniformGrid{Columns=3,Margin=new Thickness(0,0,-12,0)};
    foreach(var it in items){var item=it;var card=new StackPanel{Margin=new Thickness(12)};
     if(item.Type=="widget"){try{var path=item.Base.StartsWith("http")?null:Path.Combine(item.Base,item.File.Replace('/','\\'));if(path!=null)card.Children.Add(WidgetPreview(CustomWidget.Read(path)));}catch(Exception){}}
     else{try{var path=item.Base.StartsWith("http")?null:Path.Combine(item.Base,item.File.Replace('/','\\'));if(path!=null)card.Children.Add(StylePreview(Styles.Read(path)));}catch(Exception){}}
     card.Children.Add(new TextBlock{Text=item.Name,FontSize=14,FontWeight=FontWeights.SemiBold,Margin=new Thickness(2,10,0,2)});
     card.Children.Add(new TextBlock{Text=item.Description,FontSize=11.5,Foreground=Brush(Ui.Soft),TextWrapping=TextWrapping.Wrap,Height=46,Margin=new Thickness(2,0,0,2)});
     card.Children.Add(new TextBlock{Text="by "+(item.Author.Length>0?item.Author:"someone")+(item.Base.StartsWith("http")?"  ·  online":""),FontSize=11,Foreground=Brush(Ui.Faint),Margin=new Thickness(2,0,0,8)});
     var get=Primary("Get",()=>Install(item));get.Margin=new Thickness(2,0,0,0);get.Padding=new Thickness(18,5,18,5);card.Children.Add(get);
     grid.Children.Add(new Border{Background=Brush(Ui.Surface),CornerRadius=new CornerRadius(16),Margin=new Thickness(0,0,12,12),Child=card});}
    section.Children.Add(grid);
   }
   // Your own widgets: installed and made here.
   var mine=CustomWidget.All(folder);var yours=Card("Your widgets",mine.Count==0?"None yet. Get one above or make your own below.":"Switch them on and off, edit, or export them to share.");
   foreach(var w0 in mine){var w=w0;var row=new Grid{Margin=new Thickness(0,6,0,6)};row.ColumnDefinitions.Add(new ColumnDefinition());row.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
    var name=new StackPanel{VerticalAlignment=VerticalAlignment.Center};name.Children.Add(new TextBlock{Text=w.Name,FontWeight=FontWeights.SemiBold});name.Children.Add(new TextBlock{Text=(w.Description.Length>0?w.Description:"Custom widget")+(w.Author.Length>0?"  ·  by "+w.Author:""),FontSize=11.5,Foreground=Brush(Ui.Faint),TextTrimming=TextTrimming.CharacterEllipsis});row.Children.Add(name);
    var tools=new StackPanel{Orientation=Orientation.Horizontal};Grid.SetColumn(tools,1);bool on=Profile.CustomWidgets.Contains(w.Id);
    tools.Children.Add(Chip(on?"On desktop":"Off",on,()=>{if(on)Profile.CustomWidgets.Remove(w.Id);else Profile.CustomWidgets.Add(w.Id);RefreshDesktop();Page(current);}));
    tools.Children.Add(Ghost("Edit",()=>{editing=w;widgetDraft=null;Page(current);}));
    tools.Children.Add(Ghost("Export…",()=>{var d=new Microsoft.Win32.SaveFileDialog{Filter="WinDotra widget|*"+CustomWidget.Extension,FileName=Safe(w.Name)+CustomWidget.Extension};if(d.ShowDialog(this)==true){w.Save(d.FileName);Report("Exported. Share the file, or add it to the store (see Share below).");}}));
    tools.Children.Add(Ghost("Delete",()=>{Profile.CustomWidgets.Remove(w.Id);Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(w.Path,Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);RefreshDesktop();Page(current);}));
    row.Children.Add(tools);yours.Children.Add(row);}
   var importRow=new WrapPanel();importRow.Children.Add(Button("Import a widget or style…",()=>{var d=new Microsoft.Win32.OpenFileDialog{Filter="WinDotra files|*"+CustomWidget.Extension+";*"+Styles.Extension};if(d.ShowDialog(this)!=true)return;Install(new StoreItem{Type=d.FileName.EndsWith(Styles.Extension)?"style":"widget",Name=Path.GetFileNameWithoutExtension(d.FileName),File=Path.GetFileName(d.FileName),Base=Path.GetDirectoryName(d.FileName)});}));yours.Children.Add(importRow);
   // The widget maker.
   var make=Card(editing!=null?"Edit “"+editing.Name+"”":"Make a widget","One line per row. Start a row with “# ” for big text, “## ” for a heading, “> ” for small grey text. Live values: {time} {time:h:mm tt} {date} {date:dddd} {day} {greeting} {cpu} {ram} {battery} {song} {artist} {days-until:2026-12-24} {countdown:2026-12-31 23:59}.");
   var w2=editing??new CustomWidget{Name="My widget",Lines=CustomWidget.FromText("## {greeting}\n# {time}\n> {date:dddd, d MMMM}")};
   var nameBox=new TextBox{Text=w2.Name,MaxLength=40,Margin=new Thickness(0,4,0,6)};var descBox=new TextBox{Text=w2.Description,MaxLength=200,Margin=new Thickness(0,0,0,6)};var authorBox=new TextBox{Text=w2.Author,MaxLength=40,Margin=new Thickness(0,0,0,6)};
   var textBox=new TextBox{Text=widgetDraft??CustomWidget.ToText(w2.Lines),AcceptsReturn=true,Height=110,FontFamily=new FontFamily("Cascadia Mono, Consolas"),Margin=new Thickness(0,0,0,6),VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
   var linkBox=new TextBox{Text=w2.Link,MaxLength=1000,Margin=new Thickness(0,0,0,6),ToolTip="Optional: a web address that opens when you click the widget"};
   var editor=new Grid();editor.ColumnDefinitions.Add(new ColumnDefinition());editor.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
   var fields=new StackPanel{Margin=new Thickness(0,0,20,0)};foreach(var pair in new object[][]{new object[]{"Name",nameBox},new object[]{"Rows",textBox},new object[]{"Link (optional)",linkBox},new object[]{"Description",descBox},new object[]{"Your name (optional)",authorBox}}){fields.Children.Add(Text((string)pair[0],12,Ui.Faint));fields.Children.Add((UIElement)pair[1]);}
   editor.Children.Add(fields);var preview=new Border{Margin=new Thickness(0,18,0,0)};Grid.SetColumn(preview,1);editor.Children.Add(preview);make.Children.Add(editor);
   bool wide=w2.Width==2;string background=w2.Background;
   Func<CustomWidget> build=()=>new CustomWidget{Name=String.IsNullOrWhiteSpace(nameBox.Text)?"My widget":nameBox.Text.Trim(),Description=descBox.Text.Trim(),Author=authorBox.Text.Trim(),Link=linkBox.Text.Trim(),Width=wide?2:1,Background=background,Lines=CustomWidget.FromText(textBox.Text)};
   Action refresh=()=>{try{preview.Child=WidgetPreview(build());}catch(Exception){}};refresh();
   textBox.TextChanged+=(s,e)=>{widgetDraft=textBox.Text;refresh();};nameBox.TextChanged+=(s,e)=>refresh();
   var options=new WrapPanel();options.Children.Add(Chip(wide?"Medium (wide)":"Small",true,()=>{}));
   ((Button)options.Children[0]).Click+=(s,e)=>{wide=!wide;((Button)options.Children[0]).Content=wide?"Medium (wide)":"Small";refresh();};
   foreach(var c in new[]{new[]{"Your theme",""},new[]{"Graphite","#1C1C1E"},new[]{"White","#FFFFFF"},new[]{"Blue","#0A84FF"},new[]{"Orange","#FF9F0A"},new[]{"Green","#1F7A4D"},new[]{"Purple","#5E3A9E"}}){string hex=c[1];var chip=Chip(c[0],false,()=>{});chip.Click+=(s,e)=>{background=hex;refresh();};options.Children.Add(chip);}
   make.Children.Add(options);
   var actions=new WrapPanel{Margin=new Thickness(0,6,0,0)};
   actions.Children.Add(Primary(editing!=null?"Save changes":"Save and put on desktop",()=>{var w=build();w.Validate();Directory.CreateDirectory(CustomWidget.Folder(folder));if(editing!=null&&editing.Path!=null&&File.Exists(editing.Path)){Profile.CustomWidgets.Remove(editing.Id);File.Delete(editing.Path);}w.Save(Path.Combine(CustomWidget.Folder(folder),Safe(w.Name)+CustomWidget.Extension));if(!Profile.CustomWidgets.Contains(w.Id))Profile.CustomWidgets.Add(w.Id);editing=null;widgetDraft=null;Save();Overlays.Profile=Profile;Overlays.Start();Page(current);Report(w.Name+" is on your desktop. Move it with Edit widgets.");}));
   if(editing!=null)actions.Children.Add(Ghost("Cancel",()=>{editing=null;widgetDraft=null;Page(current);}));
   make.Children.Add(actions);
   var share=Card("Share yours","Export a widget or style, then add it to the community store with a pull request on GitHub (put the file in docs/store and add a line to docs/store/index.json). Once it's merged, everyone sees it here.");
   share.Children.Add(Button("Open the store website ↗",()=>System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(StoreUrl.Replace("store/","")){UseShellExecute=true})));
  }
 }
}
