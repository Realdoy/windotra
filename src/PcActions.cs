using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace WinCanvas {
 // Things the island can do on the PC when the AI suggests them. The AI only proposes; nothing runs until the user
 // clicks Run on each card. There is deliberately no "run command", script, or terminal action.
 public class PcAction {
  public string Kind,Argument;
  public string Describe() {
   switch(Kind) {
    case "open_app": return "Open "+Argument;
    case "open_url": return "Open "+Argument;
    case "search_web": return "Search the web for “"+Argument+"”";
    case "open_folder": return "Open your "+Argument+" folder";
    case "media": return Argument=="next"?"Next track":Argument=="previous"?"Previous track":"Play / pause music";
    case "volume": return Argument=="mute"?"Mute or unmute":"Turn the volume "+Argument;
    case "lock_pc": return "Lock this PC";
    case "focus": return "Start a "+Argument+"-minute focus timer";
    case "add_task": return "Add task “"+Argument+"”";
    case "open_settings": return "Open "+Argument+" settings";
    default: return Kind;
   }
  }
 }
 public interface IPcHost { void Media(string command); void StartFocus(int minutes); void AddTask(string text); }

 public static class PcActions {
  public const string Protocol="\n\nYou can also act on the user's Windows PC. When the user asks you to do something you can do with the actions below, reply with one short sentence and then put each action on its own line in exactly this form: [[action: argument]]. The user must click Run before anything happens, so never claim it is done. Available actions:\n[[open_app: name]] e.g. notepad, calculator, paint, spotify, discord, chrome, edge, explorer, task manager, or any installed app name\n[[open_url: https://...]]\n[[search_web: words]]\n[[open_folder: documents|downloads|desktop|pictures|music|videos]]\n[[media: play|pause|next|previous]]\n[[volume: up|down|mute]]\n[[lock_pc: now]]\n[[focus: minutes]]\n[[add_task: text]]\n[[open_settings: display|sound|bluetooth|wifi|personalization|apps|update]]\nNever invent other actions. You cannot run commands, delete files, or change system settings yourself.";
  static readonly Regex Pattern=new Regex(@"\[\[\s*([a-z_]+)\s*:\s*([^\]\r\n]{1,300})\]\]",RegexOptions.IgnoreCase);
  static readonly string[] Kinds={"open_app","open_url","search_web","open_folder","media","volume","lock_pc","focus","add_task","open_settings"};
  public static List<PcAction> Parse(string reply) {
   var list=new List<PcAction>();
   foreach(Match m in Pattern.Matches(reply??"")) {
    string kind=m.Groups[1].Value.ToLowerInvariant(),arg=m.Groups[2].Value.Trim();
    if(!Kinds.Contains(kind)||list.Count>=5)continue;
    if(kind=="open_url"){Uri uri;if(!Uri.TryCreate(arg,UriKind.Absolute,out uri)||(uri.Scheme!="https"&&uri.Scheme!="http"))continue;}
    if(kind=="focus"){int minutes;if(!int.TryParse(Regex.Match(arg,@"\d+").Value,out minutes)||minutes<1||minutes>180)continue;arg=minutes.ToString();}
    if(kind=="media"&&!new[]{"play","pause","next","previous"}.Contains(arg.ToLowerInvariant()))continue;
    if(kind=="volume"&&!new[]{"up","down","mute"}.Contains(arg.ToLowerInvariant()))continue;
    if(kind=="open_folder"&&!new[]{"documents","downloads","desktop","pictures","music","videos"}.Contains(arg.ToLowerInvariant()))continue;
    if(kind=="open_settings"&&!Settings.ContainsKey(arg.ToLowerInvariant()))continue;
    if(kind=="media"||kind=="volume"||kind=="open_folder"||kind=="open_settings")arg=arg.ToLowerInvariant();
    list.Add(new PcAction{Kind=kind,Argument=arg});
   }
   return list;
  }
  public static string Strip(string reply){return Pattern.Replace(reply??"","").Trim();}

  static readonly Dictionary<string,string> Settings=new Dictionary<string,string>{{"display","ms-settings:display"},{"sound","ms-settings:sound"},{"bluetooth","ms-settings:bluetooth"},{"wifi","ms-settings:network-wifi"},{"personalization","ms-settings:personalization"},{"apps","ms-settings:appsfeatures"},{"update","ms-settings:windowsupdate"}};
  static readonly Dictionary<string,string> Apps=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase){{"notepad","notepad.exe"},{"calculator","calc.exe"},{"calc","calc.exe"},{"paint","mspaint.exe"},{"explorer","explorer.exe"},{"file explorer","explorer.exe"},{"files","explorer.exe"},{"task manager","taskmgr.exe"},{"settings","ms-settings:"},{"spotify","spotify:"},{"edge","microsoft-edge:"},{"microsoft edge","microsoft-edge:"},{"snipping tool","ms-screenclip:"},{"camera","microsoft.windows.camera:"},{"store","ms-windows-store:"},{"microsoft store","ms-windows-store:"},{"mail","outlookmail:"},{"clock","ms-clock:"},{"photos","ms-photos:"}};
  [DllImport("user32.dll")] static extern bool LockWorkStation();
  [DllImport("user32.dll")] static extern void keybd_event(byte key,byte scan,uint flags,UIntPtr extra);
  static void Key(byte key,int times){for(int i=0;i<times;i++){keybd_event(key,0,0,UIntPtr.Zero);keybd_event(key,0,2,UIntPtr.Zero);}}
  static void Shell(string target){Process.Start(new ProcessStartInfo(target){UseShellExecute=true});}

  public static string Run(PcAction action,IPcHost host) {
   switch(action.Kind) {
    case "open_app": return OpenApp(action.Argument);
    case "open_url": Shell(action.Argument); return "Opened in your browser.";
    case "search_web": Shell("https://www.bing.com/search?q="+Uri.EscapeDataString(action.Argument)); return "Search opened.";
    case "open_folder": {
     var folder=action.Argument=="downloads"?new DownloadWatcher().Folder:Environment.GetFolderPath(action.Argument=="documents"?Environment.SpecialFolder.MyDocuments:action.Argument=="desktop"?Environment.SpecialFolder.DesktopDirectory:action.Argument=="pictures"?Environment.SpecialFolder.MyPictures:action.Argument=="music"?Environment.SpecialFolder.MyMusic:Environment.SpecialFolder.MyVideos);
     Shell(folder); return "Folder opened.";
    }
    case "media": host.Media(action.Argument); return "Done.";
    case "volume": if(action.Argument=="mute")Key(0xAD,1);else Key(action.Argument=="up"?(byte)0xAF:(byte)0xAE,5); return "Volume changed.";
    case "lock_pc": LockWorkStation(); return "Locked.";
    case "focus": host.StartFocus(int.Parse(action.Argument)); return "Focus timer started.";
    case "add_task": host.AddTask(action.Argument.Length>250?action.Argument.Substring(0,250):action.Argument); return "Task added.";
    case "open_settings": Shell(Settings[action.Argument]); return "Settings opened.";
   }
   throw new InvalidOperationException("Unknown action.");
  }
  // Known apps first, then a Start menu shortcut whose name matches (the same thing as searching Start).
  static string OpenApp(string name) {
   string target;if(Apps.TryGetValue(name.Trim(),out target)){Shell(target);return "Opened "+name+".";}
   var roots=new[]{Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu)};
   string wanted=name.Trim().ToLowerInvariant(),best=null;int bestScore=int.MaxValue;
   foreach(string root in roots.Where(Directory.Exists)) foreach(string file in SafeFiles(root)) {
    string title=Path.GetFileNameWithoutExtension(file).ToLowerInvariant();if(!title.Contains(wanted))continue;
    if(title.Contains("uninstall"))continue;int score=title.Length-wanted.Length;if(score<bestScore){bestScore=score;best=file;}
   }
   if(best==null)throw new InvalidOperationException("Couldn't find an app called “"+name+"” in the Start menu.");
   Shell(best);return "Opened "+Path.GetFileNameWithoutExtension(best)+".";
  }
  static IEnumerable<string> SafeFiles(string root) {
   var pending=new Stack<string>();pending.Push(root);
   while(pending.Count>0){string dir=pending.Pop();string[] files,dirs;try{files=Directory.GetFiles(dir,"*.lnk");dirs=Directory.GetDirectories(dir);}catch(Exception){continue;}foreach(var f in files)yield return f;foreach(var d in dirs)pending.Push(d);}
  }
 }
}
