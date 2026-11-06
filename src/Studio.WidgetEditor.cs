using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace WinCanvas {
 public partial class Studio {
  CustomWidget draft; string draftOriginalPath;
  static readonly string[][] Values={new[]{"Time","{time}"},new[]{"Time (12-hour)","{time:h:mm tt}"},new[]{"Date","{date}"},new[]{"Weekday","{day}"},new[]{"Greeting","{greeting}"},new[]{"Song","{song}"},new[]{"Artist","{artist}"},new[]{"CPU","{cpu}"},new[]{"Memory","{ram}"},new[]{"Battery","{battery}"},new[]{"Days until a date","{days-until:2026-12-24}"},new[]{"Countdown","{countdown:2026-12-31 23:59}"}};
  static readonly string[][] Sizes={new[]{"Huge","56"},new[]{"Big","40"},new[]{"Title","22"},new[]{"Normal","14"},new[]{"Small","12"}};
  static readonly string[][] Colors={new[]{"Auto",""},new[]{"White","#FFFFFF"},new[]{"Black","#1C1C1E"},new[]{"Grey","#8E8E93"},new[]{"Blue","#0A84FF"},new[]{"Red","#FF3B30"},new[]{"Orange","#FF9F0A"},new[]{"Yellow","#FFD60A"},new[]{"Green","#30D158"},new[]{"Pink","#FF375F"},new[]{"Purple","#BF5AF2"}};
  // Starts editing a widget (a copy, so nothing changes until Save).
  void EditWidget(CustomWidget w){draft=new System.Web.Script.Serialization.JavaScriptSerializer().Deserialize<CustomWidget>(new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(w));draftOriginalPath=w.Path;Page("Store");}
  Button Swatch2(string color,bool on,Action pick){var b=new Button{Width=22,Height=22,Margin=new Thickness(0,0,4,0),Padding=new Thickness(0),ToolTip=color.Length==0?"Automatic":color,Background=color.Length==0?(Brush)new LinearGradientBrush(Color.FromRgb(255,255,255),Color.FromRgb(28,28,30),45):Brush(color),BorderBrush=Brush(on?Ui.Accent:"#55FFFFFF"),BorderThickness=new Thickness(on?2:1)};b.Click+=(s,e)=>Safe(pick);return b;}
  // The visual widget maker: rows you can see and click, with a live preview.
  void WidgetMaker() {
   if(draft==null)draft=new CustomWidget{Name="My widget",Lines=new List<WidgetLine>{new WidgetLine{Text="{greeting}",Size=15,Weight="SemiBold"},new WidgetLine{Text="{time}",Size=40,Weight="Light"},new WidgetLine{Text="{date:dddd, d MMMM}",Size=12}}};
   var make=Card(draftOriginalPath!=null?"Edit “"+draft.Name+"”":"Make a widget","Add rows, pick sizes and colors, and drop in live values. The preview updates as you go.");
   var layout=new Grid();layout.ColumnDefinitions.Add(new ColumnDefinition());layout.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});make.Children.Add(layout);
   var left=new StackPanel{Margin=new Thickness(0,0,20,0)};layout.Children.Add(left);
   var preview=new StackPanel{Margin=new Thickness(0,6,0,0),MinWidth=200};Grid.SetColumn(preview,1);layout.Children.Add(preview);
   Action refresh=()=>{preview.Children.Clear();preview.Children.Add(Text("Preview",12,Ui.Faint));try{preview.Children.Add(WidgetPreview(draft));}catch(Exception e){preview.Children.Add(Text(e.Message,12,"#FF6961"));}};
   // Widget settings.
   var name=new TextBox{Text=draft.Name,MaxLength=40,Margin=new Thickness(0,2,0,8)};name.TextChanged+=(s,e)=>{draft.Name=name.Text;refresh();};left.Children.Add(Text("Name",12,Ui.Faint));left.Children.Add(name);
   var size=new WrapPanel();size.Children.Add(Chip("Small",draft.Width==1,()=>{draft.Width=1;Page(current);}));size.Children.Add(Chip("Wide",draft.Width==2,()=>{draft.Width=2;Page(current);}));left.Children.Add(size);
   left.Children.Add(Text("Background",12,Ui.Faint));var back=new WrapPanel{Margin=new Thickness(0,0,0,6)};
   foreach(var c in new[]{"","#1C1C1E","#FFFFFF","#0B1A33","#0A84FF","#FF9F0A","#1F7A4D","#5E3A9E","#3A0F1C","#B31C1C1E"}){string hex=c;back.Children.Add(Swatch2(hex,draft.Background==hex,()=>{draft.Background=hex;Page(current);}));}
   var anyBack=Button("Any color…",()=>PickHex(draft.Background.Length==7?draft.Background:"#1C1C1E",hex=>{draft.Background=hex;Page(current);}));anyBack.Padding=new Thickness(10,3,10,3);anyBack.Margin=new Thickness(6,0,0,0);back.Children.Add(anyBack);left.Children.Add(back);
   left.Children.Add(Text("Accent (titles and bars)",12,Ui.Faint));var accent=new WrapPanel{Margin=new Thickness(0,0,0,8)};foreach(var c in Colors){string hex=c[1];accent.Children.Add(Swatch2(hex,draft.Accent==hex,()=>{draft.Accent=hex;Page(current);}));}left.Children.Add(accent);
   // Rows.
   left.Children.Add(Text("Rows",12,Ui.Faint));
   for(int i=0;i<draft.Lines.Count;i++){
    int index=i;var line=draft.Lines[i];
    var row=new Border{Background=Brush("#1C1C1E"),CornerRadius=new CornerRadius(10),Padding=new Thickness(10,8,8,8),Margin=new Thickness(0,0,0,6)};var inner=new StackPanel();row.Child=inner;
    var top=new Grid();top.ColumnDefinitions.Add(new ColumnDefinition());top.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});inner.Children.Add(top);
    if(line.Kind=="text"){
     var text=new TextBox{Text=line.Text,MaxLength=200,Margin=new Thickness(0,0,8,0)};text.TextChanged+=(s,e)=>{line.Text=text.Text;refresh();};top.Children.Add(text);
     var insert=new ContextMenu();foreach(var v in Values){string token=v[1];var item=new MenuItem{Header=v[0],InputGestureText=token};item.Click+=(s,e)=>{int at=text.CaretIndex;text.Text=text.Text.Insert(Math.Min(at,text.Text.Length),token);text.CaretIndex=at+token.Length;text.Focus();};insert.Items.Add(item);}
     var add=Ghost("+ Live value",()=>{});add.Click+=(s,e)=>{insert.PlacementTarget=(UIElement)s;insert.IsOpen=true;};add.ContextMenu=insert;
     var tools0=new StackPanel{Orientation=Orientation.Horizontal};tools0.Children.Add(add);Grid.SetColumn(tools0,1);top.Children.Add(tools0);
    } else if(line.Kind=="bar"){
     var pick=new WrapPanel();foreach(var b in new[]{new[]{"cpu","CPU"},new[]{"ram","Memory"},new[]{"battery","Battery"},new[]{"day","Day"},new[]{"week","Week"},new[]{"month","Month"},new[]{"year","Year"}}){string key=b[0];pick.Children.Add(Chip(b[1],line.Bar==key,()=>{line.Bar=key;Page(current);}));}top.Children.Add(pick);
    } else top.Children.Add(Text("Empty space",12,Ui.Faint));
    var tools=new WrapPanel{Margin=new Thickness(0,6,0,0)};inner.Children.Add(tools);
    if(line.Kind=="text"){
     foreach(var s0 in Sizes){double v=double.Parse(s0[1]);var chip=Chip(s0[0],Math.Abs(line.Size-v)<0.5,()=>{line.Size=v;Page(current);});chip.Padding=new Thickness(9,3,9,3);tools.Children.Add(chip);}
     var bold=Chip("Bold",line.Weight=="Bold"||line.Weight=="SemiBold",()=>{line.Weight=line.Weight=="Normal"||line.Weight=="Light"?"SemiBold":line.Size>=36?"Light":"Normal";Page(current);});bold.Padding=new Thickness(9,3,9,3);tools.Children.Add(bold);
     var align=Chip(line.Align=="Left"?"⟸ Left":line.Align=="Center"?"⟺ Center":"⟹ Right",false,()=>{line.Align=line.Align=="Left"?"Center":line.Align=="Center"?"Right":"Left";Page(current);});align.Padding=new Thickness(9,3,9,3);tools.Children.Add(align);
    } else {var thick=Chip(line.Size<=6?"Thin":"Thick",false,()=>{line.Size=line.Size<=6?(line.Kind=="bar"?10:24):(line.Kind=="bar"?4:8);Page(current);});thick.Padding=new Thickness(9,3,9,3);tools.Children.Add(thick);}
    if(line.Kind!="space"){var colors=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(6,4,0,0)};foreach(var c in Colors){string hex=c[1];colors.Children.Add(Swatch2(hex,line.Color==hex,()=>{line.Color=hex;Page(current);}));}tools.Children.Add(colors);}
    var move=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(8,0,0,0)};
    move.Children.Add(Ghost("↑",()=>{if(index==0)return;draft.Lines.RemoveAt(index);draft.Lines.Insert(index-1,line);Page(current);}));
    move.Children.Add(Ghost("↓",()=>{if(index>=draft.Lines.Count-1)return;draft.Lines.RemoveAt(index);draft.Lines.Insert(index+1,line);Page(current);}));
    move.Children.Add(Ghost("Remove",()=>{if(draft.Lines.Count>1){draft.Lines.RemoveAt(index);Page(current);}}));tools.Children.Add(move);
    left.Children.Add(row);
   }
   var adds=new WrapPanel{Margin=new Thickness(0,2,0,8)};
   if(draft.Lines.Count<12){adds.Children.Add(Button("+ Text",()=>{draft.Lines.Add(new WidgetLine{Text="New row"});Page(current);}));adds.Children.Add(Button("+ Progress bar",()=>{draft.Lines.Add(new WidgetLine{Kind="bar",Bar="day",Size=6});Page(current);}));adds.Children.Add(Button("+ Space",()=>{draft.Lines.Add(new WidgetLine{Kind="space",Size=8});Page(current);}));}
   left.Children.Add(adds);
   var link=new TextBox{Text=draft.Link,MaxLength=1000,Margin=new Thickness(0,2,0,8)};link.TextChanged+=(s,e)=>draft.Link=link.Text.Trim();left.Children.Add(Text("Opens when clicked (optional web address)",12,Ui.Faint));left.Children.Add(link);
   var about=new TextBox{Text=draft.Description,MaxLength=200,Margin=new Thickness(0,2,0,8)};about.TextChanged+=(s,e)=>draft.Description=about.Text.Trim();left.Children.Add(Text("Description (for the Store)",12,Ui.Faint));left.Children.Add(about);
   var author=new TextBox{Text=draft.Author,MaxLength=40,Margin=new Thickness(0,2,0,8)};author.TextChanged+=(s,e)=>draft.Author=author.Text.Trim();left.Children.Add(Text("Your name (optional)",12,Ui.Faint));left.Children.Add(author);
   refresh();
   var actions=new WrapPanel{Margin=new Thickness(0,6,0,0)};
   actions.Children.Add(Primary(draftOriginalPath!=null?"Save changes":"Save and put on desktop",()=>{var w=SaveDraft();Report(w.Name+" is on your desktop. Move it with Edit widgets.");}));
   actions.Children.Add(Button("Publish to Store…",()=>{var w=SaveDraft();Publish(w.Path,"widget",w.Name);}));
   actions.Children.Add(Ghost("Start over",()=>{draft=null;draftOriginalPath=null;Page(current);}));
   make.Children.Add(actions);
  }
  CustomWidget SaveDraft(){
   if(String.IsNullOrWhiteSpace(draft.Name))draft.Name="My widget";draft.Name=draft.Name.Trim();draft.Validate();Directory.CreateDirectory(CustomWidget.Folder(folder));
   if(draftOriginalPath!=null&&File.Exists(draftOriginalPath)){try{Profile.CustomWidgets.Remove(CustomWidget.Read(draftOriginalPath).Id);}catch(Exception){}File.Delete(draftOriginalPath);}
   string path=Path.Combine(CustomWidget.Folder(folder),Safe(draft.Name)+CustomWidget.Extension);draft.Save(path);draft.Path=path;draftOriginalPath=path;
   if(!Profile.CustomWidgets.Contains(draft.Id))Profile.CustomWidgets.Add(draft.Id);Save();Overlays.Profile=Profile;Overlays.Start();Page(current);return draft;
  }

  // ---- Publishing: one click from WinDotra to a pull request on GitHub. ----
  const string StoreRepo="Realdoy/windotra";
  static string GhPath(){foreach(var p in new[]{@"C:\Program Files\GitHub CLI\gh.exe",Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),@"Programs\GitHub CLI\gh.exe")})if(File.Exists(p))return p;foreach(string dir in (Environment.GetEnvironmentVariable("PATH")??"").Split(';')){try{var p=Path.Combine(dir.Trim(),"gh.exe");if(File.Exists(p))return p;}catch(Exception){}}return null;}
  static string Gh(string gh,string args,string input=null){var info=new ProcessStartInfo(gh,args){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,RedirectStandardInput=input!=null,StandardOutputEncoding=Encoding.UTF8};
   using(var p=Process.Start(info)){if(input!=null){p.StandardInput.Write(input);p.StandardInput.Close();}string o=p.StandardOutput.ReadToEnd(),e=p.StandardError.ReadToEnd();p.WaitForExit();if(p.ExitCode!=0)throw new InvalidOperationException((e.Length>0?e:o).Trim().Split('\n')[0]);return o.Trim();}}
  // With the GitHub app (gh) signed in: fork if needed, add the file on a new branch, and open a pull request.
  // Without it: open GitHub's "new file" page with the file already filled in.
  async void Publish(string file,string type,string name){
   string json=File.ReadAllText(file),folderName=type=="style"?"styles":"widgets",fileName=Safe(name).ToLowerInvariant().Replace(' ','-')+(type=="style"?Styles.Extension:CustomWidget.Extension);
   string gh=GhPath();
   if(gh==null){string url="https://github.com/"+StoreRepo+"/new/main/docs/store/"+folderName+"?filename="+Uri.EscapeDataString(fileName)+"&value="+Uri.EscapeDataString(json);Process.Start(new ProcessStartInfo(url){UseShellExecute=true});Report("GitHub opened with your "+type+" filled in. Click “Propose changes”, then “Create pull request”.");return;}
   Report("Publishing "+name+" to the Store…");
   try{
    string pr=await Task.Run(()=>{
     string me=Gh(gh,"api user --jq .login");string owner=StoreRepo.Split('/')[0],repo=StoreRepo;
     if(!me.Equals(owner,StringComparison.OrdinalIgnoreCase)){Gh(gh,"repo fork "+StoreRepo+" --clone=false");repo=me+"/windotra";System.Threading.Thread.Sleep(3000);}
     string sha=Gh(gh,"api repos/"+StoreRepo+"/git/ref/heads/main --jq .object.sha");string branch="store-"+fileName.Replace('.','-')+"-"+DateTime.Now.ToString("yyyyMMddHHmmss");
     Gh(gh,"api repos/"+repo+"/git/refs -f ref=refs/heads/"+branch+" -f sha="+sha);
     string body=new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(new Dictionary<string,object>{{"message","Add "+type+": "+name},{"content",Convert.ToBase64String(Encoding.UTF8.GetBytes(json))},{"branch",branch}});
     Gh(gh,"api -X PUT repos/"+repo+"/contents/docs/store/"+folderName+"/"+Uri.EscapeDataString(fileName)+" --input -",body);
     string request=new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(new Dictionary<string,object>{{"title","Store: "+name+" ("+type+")"},{"head",me+":"+branch},{"base","main"},{"body","Shared from WinDotra. The store catalog updates itself when this is merged."}});
     return Gh(gh,"api repos/"+StoreRepo+"/pulls --input - --jq .html_url",request);});
    Process.Start(new ProcessStartInfo(pr){UseShellExecute=true});Report("Published! Your pull request is open on GitHub. Once it's merged, everyone sees "+name+" in the Store.");
   }catch(Exception e){Report("Couldn't publish with the GitHub app ("+e.Message+"). Opening GitHub in your browser instead.");string url="https://github.com/"+StoreRepo+"/new/main/docs/store/"+folderName+"?filename="+Uri.EscapeDataString(fileName)+"&value="+Uri.EscapeDataString(json);Process.Start(new ProcessStartInfo(url){UseShellExecute=true});}
  }
 }
}
