using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace WinCanvas {
 // First-run setup: a few friendly steps. Everything chosen here can be changed later in WinDotra.
 public partial class Studio {
  int setupStep;
  string SetupMarker {get{return Path.Combine(folder,"setup.json");}}
  public bool NeedsSetup {get{return !smoke&&!File.Exists(SetupMarker);}}
  // AI routes with a short description and whether they're ready on this PC.
  object[][] AiRoutes() {
   string claudeApp=AiApps.ClaudeApp,chatgpt=AiApps.ChatGptApp,cli=ClaudeCode.Find(),codex=Codex.Find();
   return new[]{
    new object[]{"Auto","Pick for me","Uses whatever is ready: free local AI, your Claude subscription, then an installed app, then the browser.",true},
    new object[]{"Claude app","Claude app","Opens the Claude app with your question pasted in. Uses your Claude account.",claudeApp!=null},
    new object[]{"ChatGPT app","ChatGPT app","Opens the ChatGPT app with your question pasted in. Uses your ChatGPT account.",chatgpt!=null},
    new object[]{"Claude Code","Claude, right in the island","Answers appear in the island using your Claude subscription (needs the Claude command-line tool).",cli!=null},
    new object[]{"Codex","ChatGPT, right in the island","Answers appear in the island using your ChatGPT account (needs the Codex command-line tool, signed in once).",codex!=null},
    new object[]{"Ollama","Free local AI","Runs on this PC with Ollama. Private and offline. Answers appear in the island.",true},
    new object[]{"Browser","Just my browser","Opens claude.ai or ChatGPT in your browser with the question filled in.",true}};
  }
  Button AiRouteChoice(object[] route,Action after) {
   string provider=(string)route[0];bool ready=(bool)route[3];bool installable=provider=="Claude app"||provider=="ChatGPT app"||provider=="Claude Code"||provider=="Codex";
   string title=(string)route[1]+(installable?(ready?"   ✓ installed":"   · not installed"):"");
   var b=Choice(title,(string)route[2],aiConfig.Provider==provider,()=>{SaveAi(c=>c.Provider=provider);if(provider=="ChatGPT app")SaveAi(c=>c.BrowserSite="ChatGPT");if(provider=="Claude app")SaveAi(c=>c.BrowserSite="Claude");after();});
   if(installable&&!ready)b.Opacity=0.6;return b;
  }
  void Setup() {
   string[] titles={"Welcome","Your AI","Your look","Your desktop","All set"};
   var dots=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(0,0,0,26)};
   for(int i=0;i<titles.Length;i++)dots.Children.Add(new Border{Width=i==setupStep?22:8,Height=8,CornerRadius=new CornerRadius(4),Background=Brush(i<=setupStep?Ui.Ink:Ui.Line),Margin=new Thickness(0,0,6,0)});
   body.Children.Add(dots);
   if(setupStep==0) {
    body.Children.Add(new Image{Source=Brand.Logo,Width=112,Height=112,HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(0,0,0,18)});
    Header("Welcome to "+Brand.Name,Brand.Name+" — "+Brand.Tagline+" A Dynamic Island, widgets, live wallpapers, an AI that can help on your PC, and your own fonts and cursors.");
    body.Children.Add(Text("This takes about a minute. You can change everything later.",14,Ui.Soft));
   } else if(setupStep==1) {
    Header("Which AI do you want to use?","No API keys. "+(AiApps.ClaudeApp!=null||AiApps.ChatGptApp!=null?"We found "+(AiApps.ClaudeApp!=null&&AiApps.ChatGptApp!=null?"the Claude and ChatGPT apps":AiApps.ClaudeApp!=null?"the Claude app":"the ChatGPT app")+" on this PC.":"You can switch anytime in AI."));
    foreach(var route in AiRoutes())body.Children.Add(AiRouteChoice(route,()=>Page(current)));
   } else if(setupStep==2) {
    Header("Pick a look","Colors for your widgets and island accents. The preview updates right away.");
    var holder=new Border();body.Children.Add(Framed(holder));holder.Child=DesktopPreview(220,true);
    var swatches=new WrapPanel();foreach(var theme in Themes){string accent=theme[1],background=theme[2];swatches.Children.Add(Chip(theme[0],Profile.Accent==accent&&Profile.Background==background,()=>{Profile.Accent=accent;Profile.Background=background;Save();Page(current);}));}
    body.Children.Add(swatches);var options=new StackPanel{Margin=new Thickness(0,12,0,0)};body.Children.Add(options);
    Check(options,"Frosted glass widgets (macOS style)",Profile.Glass,v=>{Profile.Glass=v;Save();});
   } else if(setupStep==3) {
    Header("What goes on your desktop?","Turn things on now; you can move and restyle them later.");
    var list=new StackPanel();body.Children.Add(list);
    Check(list,"Dynamic Island  ·  music, downloads, AI, timer",Profile.Island,v=>{Profile.Island=v;Save();});Check(list,"Clock",Profile.Clock,v=>{Profile.Clock=v;Save();});Check(list,"Calendar",Profile.Calendar,v=>{Profile.Calendar=v;Save();});
    Check(list,"Now playing widget",Profile.Music,v=>{Profile.Music=v;Save();});Check(list,"Reminders",Profile.Reminders,v=>{Profile.Reminders=v;Save();});Check(list,"System  ·  CPU, memory, battery rings",Profile.Stats,v=>{Profile.Stats=v;Save();});
    Check(list,"Big date  ·  F R I D A Y",Profile.Day,v=>{Profile.Day=v;Save();});
    var wall=Card("Live wallpaper","");var chips=new WrapPanel();chips.Children.Add(Chip("None",!Profile.LiveWallpaper,()=>{Profile.LiveWallpaper=false;Save();Page(current);}));
    foreach(string kind in WallpaperScene.Kinds){string k=kind;chips.Children.Add(Chip(k,Profile.LiveWallpaper&&Profile.Scene==k,()=>{Profile.LiveWallpaper=true;Profile.Scene=k;Save();Page(current);}));}wall.Children.Add(chips);
   } else {
    Header("You're all set","Your island and widgets start now. "+Brand.Name+" keeps running in the tray when you close this window.");
    body.Children.Add(Text("AI: "+AiSummary()+"   ·   Theme: "+Profile.Accent+" on "+Profile.Background+"   ·   Live wallpaper: "+(Profile.LiveWallpaper?Profile.Scene:"off"),13,Ui.Soft));
    Check(body,"Start "+Brand.Name+" quietly when I sign in to Windows",StartsWithWindows,v=>Safe(()=>SetStartup(v)));
   }
   var footer=new Grid{Margin=new Thickness(0,28,0,0)};var left=new StackPanel{Orientation=Orientation.Horizontal};var right=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};
   if(setupStep>0)left.Children.Add(Button("Back",()=>{setupStep--;Page(current);}));
   if(setupStep<titles.Length-1)left.Children.Add(Ghost("Skip setup",FinishSetup));
   right.Children.Add(Primary(setupStep==0?"Let's go":setupStep==titles.Length-1?"Start "+Brand.Name:"Next",()=>{if(setupStep==titles.Length-1)FinishSetup();else{setupStep++;Page(current);}}));
   footer.Children.Add(left);footer.Children.Add(right);body.Children.Add(footer);
  }
  void FinishSetup() {
   File.WriteAllText(SetupMarker,"{\"done\":\""+DateTime.Now.ToString("s")+"\"}");setupStep=0;Save();
   launched=true;Launch();Page("Home");Report("Welcome to "+Brand.Name+". Your desktop is running; everything is adjustable from the sidebar.");
  }
  public void StartSetup(){setupStep=0;Page("Setup");}
 }
}
