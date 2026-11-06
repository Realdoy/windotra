using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace WinCanvas {
 // A widget anyone can make and share: a few lines of text with live values, a size, colors, and an optional link.
 // Widget files (.windotra-widget) are plain JSON. They contain no code, so they are safe to download.
 // Kind: "text" (with live values), "bar" (a progress bar for cpu, ram, battery, day, week, month, or year), or "space".
 public class WidgetLine { public string Kind="text",Text="",Color="",Weight="Normal",Align="Left",Bar="day"; public double Size=14; }
 public static class WidgetBars { public static readonly string[] All={"cpu","ram","battery","day","week","month","year"}; }
 public class CustomWidget {
  public string Format="windotra-widget"; public int Version=1;
  public string Name="",Author="",Description="",Background="",Accent="",Link=""; public int Width=1;
  public List<WidgetLine> Lines=new List<WidgetLine>();
  [ScriptIgnore] public string Path;
  public const string Extension=".windotra-widget";
  static readonly string[] Weights={"Light","Normal","Medium","SemiBold","Bold"},Aligns={"Left","Center","Right"};
  static bool Hex(string v){return v==null||v.Length==0||Regex.IsMatch(v,"^#([0-9A-Fa-f]{2})?[0-9A-Fa-f]{6}$");}
  public void Validate() {
   if(Lines!=null)foreach(var l in Lines)if(l!=null){if(l.Kind==null)l.Kind="text";if(l.Bar==null)l.Bar="day";}
   if(Format!="windotra-widget"||Version!=1||String.IsNullOrWhiteSpace(Name)||Name.Length>40||(Author??"").Length>40||(Description??"").Length>200||(Width!=1&&Width!=2)||!Hex(Background)||!Hex(Accent)||Lines==null||Lines.Count==0||Lines.Count>12)throw new InvalidDataException("That isn't a valid WinDotra widget.");
   if(!String.IsNullOrEmpty(Link)){Uri u;if(Link.Length>1000||!Uri.TryCreate(Link,UriKind.Absolute,out u)||(u.Scheme!="https"&&u.Scheme!="http"))throw new InvalidDataException("A widget link must be a web address (https://…).");}
   foreach(var l in Lines)if(l==null||l.Text==null||l.Kind==null||!new[]{"text","bar","space"}.Contains(l.Kind)||l.Bar==null||!WidgetBars.All.Contains(l.Bar)||l.Text.Length>200||l.Size<2||l.Size>120||!Hex(l.Color)||!Weights.Contains(l.Weight)||!Aligns.Contains(l.Align))throw new InvalidDataException("A widget line is invalid.");
  }
  public static CustomWidget Read(string file){if(new FileInfo(file).Length>32*1024)throw new InvalidDataException("That widget file is too large.");var w=new JavaScriptSerializer().Deserialize<CustomWidget>(File.ReadAllText(file));if(w==null)throw new InvalidDataException("That widget file is empty.");w.Validate();w.Path=file;return w;}
  public void Save(string file){Validate();File.WriteAllText(file,new JavaScriptSerializer().Serialize(this));}
  public string Id{get{return "custom:"+Regex.Replace(Name.ToLowerInvariant(),"[^a-z0-9]+","-");}}
  // Text with {tokens}: {time} {time:h:mm tt} {date} {date:dddd} {day} {greeting} {cpu} {ram} {battery} {song} {artist} {days-until:2026-12-24} {countdown:2026-12-31 23:59}.
  public static string Expand(string text,Func<string,string> live) {
   return Regex.Replace(text,@"\{([a-z\-]+)(?::([^}]*))?\}",m=>{
    string key=m.Groups[1].Value,arg=m.Groups[2].Success?m.Groups[2].Value:null;var now=DateTime.Now;
    try{
     switch(key){
      case "time":return now.ToString(arg??"HH:mm");
      case "date":return now.ToString(arg??"d MMMM yyyy");
      case "day":return now.ToString("dddd");
      case "greeting":return now.Hour<5?"Good night":now.Hour<12?"Good morning":now.Hour<18?"Good afternoon":"Good evening";
      case "days-until":{DateTime d;if(!DateTime.TryParse(arg,CultureInfo.InvariantCulture,DateTimeStyles.None,out d))return "?";return Math.Max(0,(int)Math.Ceiling((d.Date-now.Date).TotalDays)).ToString();}
      case "countdown":{DateTime d;if(!DateTime.TryParse(arg,CultureInfo.InvariantCulture,DateTimeStyles.None,out d))return "?";var left=d-now;if(left<TimeSpan.Zero)left=TimeSpan.Zero;return (left.Days>0?left.Days+"d ":"")+left.Hours.ToString("00")+":"+left.Minutes.ToString("00")+":"+left.Seconds.ToString("00");}
      default:return live!=null?live(key)??m.Value:m.Value;
     }
    }catch(FormatException){return m.Value;}
   });
  }
  // Built from the simple editor: one line per row; "# " = big, "## " = medium, "> " = small and soft.
  public static List<WidgetLine> FromText(string text){var lines=new List<WidgetLine>();foreach(var raw in (text??"").Replace("\r","").Split('\n').Take(12)){string t=raw;var l=new WidgetLine();if(t.StartsWith("# ")){l.Size=40;l.Weight="Light";t=t.Substring(2);}else if(t.StartsWith("## ")){l.Size=22;l.Weight="SemiBold";t=t.Substring(3);}else if(t.StartsWith("> ")){l.Size=12;t=t.Substring(2);}l.Text=t;lines.Add(l);}if(lines.Count==0)lines.Add(new WidgetLine{Text=" "});return lines;}
  public static string ToText(List<WidgetLine> lines){return String.Join("\n",lines.Select(l=>(l.Size>=36?"# ":l.Size>=20?"## ":l.Size<=12.5?"> ":"")+l.Text));}
  public static string Folder(string data){return System.IO.Path.Combine(data,"widgets");}
  public static List<CustomWidget> All(string data){var list=new List<CustomWidget>();string dir=Folder(data);if(!Directory.Exists(dir))return list;foreach(var f in Directory.GetFiles(dir,"*"+Extension)){try{list.Add(Read(f));}catch(Exception){}}return list.OrderBy(w=>w.Name).ToList();}
 }

 public partial class OverlayManager {
  // Live values for custom widgets.
  string LiveValue(string key){var m=media.State;switch(key){case "cpu":return stats.Cpu;case "ram":return stats.Ram;case "battery":return stats.Battery;case "song":return m.Has?m.Title:"Nothing playing";case "artist":return m.Has?m.Artist:"";default:return null;}}
  public string WidgetsFolder;
  // Builds the visual for a custom widget (also used for the preview in WinDotra and the Store).
  public static FrameworkElement RenderCustom(CustomWidget w,Palette p,Func<string,string> live,List<Action> refresh,double width,double height) {
   var stack=new StackPanel{Width=width,Height=height,ClipToBounds=true};
   foreach(var line in w.Lines){var l=line;
    if(l.Kind=="space"){stack.Children.Add(new Border{Height=Math.Max(2,l.Size)});continue;}
    if(l.Kind=="bar"){var bar=new ProgressBar{Maximum=100,Height=Math.Max(3,Math.Min(20,l.Size)),Margin=new Thickness(0,4,0,4),Foreground=Ui.Brush(l.Color.Length>0?l.Color:(w.Accent.Length>0?w.Accent:"#0A84FF")),Background=Ui.Brush(p.Chip)};
     Action fill=()=>{bar.Value=BarValue(l.Bar,live);};fill();if(refresh!=null)refresh.Add(fill);stack.Children.Add(bar);continue;}
    var t=new TextBlock{FontSize=l.Size,FontFamily=Ui.SansFamily,TextWrapping=TextWrapping.Wrap,TextTrimming=TextTrimming.CharacterEllipsis,Foreground=Ui.Brush(l.Color.Length>0?l.Color:(l.Size<=12.5?p.Soft:p.Ink)),TextAlignment=l.Align=="Center"?TextAlignment.Center:l.Align=="Right"?TextAlignment.Right:TextAlignment.Left,Margin=new Thickness(0,1,0,1)};
    t.FontWeight=(FontWeight)new FontWeightConverter().ConvertFromString(l.Weight);if(w.Accent.Length>0&&l.Size>=20&&l.Color.Length==0)t.Foreground=Ui.Brush(w.Accent);
    Action update=()=>t.Text=CustomWidget.Expand(l.Text,live);update();if(refresh!=null)refresh.Add(update);stack.Children.Add(t);}
   return stack;
  }
  // 0..100 for progress bars: system load from the live values, or how far through the day, week, month, or year we are.
  public static double BarValue(string bar,Func<string,string> live){var now=DateTime.Now;
   switch(bar){case "day":return now.TimeOfDay.TotalHours/24*100;case "week":return (((int)now.DayOfWeek+6)%7+now.TimeOfDay.TotalHours/24)/7*100;case "month":return (now.Day-1+now.TimeOfDay.TotalHours/24)/DateTime.DaysInMonth(now.Year,now.Month)*100;case "year":return (now.DayOfYear-1)/(DateTime.IsLeapYear(now.Year)?366.0:365.0)*100;
    default:{string v=live!=null?live(bar)??"":"";var m=System.Text.RegularExpressions.Regex.Match(v,@"\d+(\.\d+)?");double d;return m.Success&&double.TryParse(m.Value,NumberStyles.Any,CultureInfo.InvariantCulture,out d)?Math.Min(100,d):0;}}}
  void CustomWidgets() {
   if(WidgetsFolder==null||Profile.CustomWidgets==null)return;int n=0;
   foreach(var w in CustomWidget.All(WidgetsFolder).Where(x=>Profile.CustomWidgets.Contains(x.Id))){
    var tile=Tile(w.Width==2);var pal=w.Background.Length>0?new Palette(w.Background.Length==9?"#"+w.Background.Substring(3):w.Background,Profile.Accent):p;
    tile.Children.Add(RenderCustom(w,pal,LiveValue,refresh,tile.Width,tile.Height));
    if(w.Link.Length>0){string link=w.Link;tile.Cursor=System.Windows.Input.Cursors.Hand;tile.MouseLeftButtonUp+=(s,e)=>{if(!Editing)System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(link){UseShellExecute=true});};}
    var window=Shell(w.Id,w.Name,TileWidth(w.Width==2),60+n*30,140+n*30,tile,false);n++;
    if(w.Background.Length>0){var card=FindCard(window);if(card!=null)card.Background=Ui.Brush(w.Background);}
   }
  }
  static Border FindCard(Window w){var root=w.Content as Grid;if(root!=null)return root.Children.OfType<Border>().FirstOrDefault();return w.Content as Border;}
 }
}
