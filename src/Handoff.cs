using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;

namespace WinCanvas {
 // Finds the Claude and ChatGPT desktop apps and hands a question (and optional screenshot) to them.
 public static class AiApps {
  static string Alias(string name){return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Microsoft","WindowsApps",name);}
  static string FirstExisting(params string[] paths){return paths.FirstOrDefault(File.Exists);}
  public static string ClaudeApp {get{return FirstExisting(Alias("claude-desktop.exe"),Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"AnthropicClaude","claude.exe"));}}
  public static string ChatGptApp {get{return FirstExisting(Alias("chatgpt-desktop.exe"),Alias("ChatGPT.exe"),Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Programs","ChatGPT","ChatGPT.exe"));}}
  public static string AppFor(string provider){return provider=="Claude app"?ClaudeApp:provider=="ChatGPT app"?ChatGptApp:null;}
  // Where a question goes when nothing answers inside the island: an installed app first, then the browser.
  public static string FallbackFor(AiConfig config){if(config.BrowserSite=="ChatGPT"&&ChatGptApp!=null)return "ChatGPT app";if(config.BrowserSite!="ChatGPT"&&ClaudeApp!=null)return "Claude app";return null;}

  [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window,out uint process);
  [DllImport("user32.dll")] static extern void keybd_event(byte key,byte scan,uint flags,UIntPtr extra);
  static void Paste(){keybd_event(0x11,0,0,UIntPtr.Zero);keybd_event(0x56,0,0,UIntPtr.Zero);keybd_event(0x56,0,2,UIntPtr.Zero);keybd_event(0x11,0,2,UIntPtr.Zero);}
  static bool AppInFront(string app) {
   uint pid;GetWindowThreadProcessId(GetForegroundWindow(),out pid);
   try{string name=Process.GetProcessById((int)pid).ProcessName.ToLowerInvariant();return app.Contains("ChatGPT")||app.Contains("chatgpt")?name.Contains("chatgpt"):name.Contains("claude");}catch(Exception){return false;}
  }
  // Opens the app, waits for it to come to the front, then pastes the screenshot and the question. Never presses Enter.
  public static async Task<string> Ask(string provider,string question,string screenshot) {
   string app=AppFor(provider);if(app==null)throw new InvalidOperationException(provider+" isn't installed on this PC.");
   string label=provider.Replace(" app","");
   Process.Start(new ProcessStartInfo(app){UseShellExecute=true});
   for(int i=0;i<40&&!AppInFront(app);i++)await Task.Delay(150);
   if(!AppInFront(app)){Clipboard.SetText(question);return label+" is opening. Your question is on the clipboard: press Ctrl+V there.";}
   await Task.Delay(600);
   if(screenshot!=null){Clipboard.SetImage(new BitmapImage(new Uri(screenshot)));Paste();await Task.Delay(900);}
   Clipboard.SetText(question);Paste();
   return "Sent to "+label+". Check it and press Enter there.";
  }
 }

 // Captures the screen for the AI only when the user switches "Share screen" on for a message.
 public static class ScreenShot {
  // Saves the monitor the island is on, scaled to at most 1600 px wide, and returns the PNG path.
  public static string Capture(Window hide) {
   var screen=hide!=null?System.Windows.Forms.Screen.FromHandle(new System.Windows.Interop.WindowInteropHelper(hide).Handle):System.Windows.Forms.Screen.PrimaryScreen;var area=screen.Bounds;
   double opacity=hide!=null?hide.Opacity:1;if(hide!=null){hide.Opacity=0;hide.UpdateLayout();System.Threading.Thread.Sleep(120);}
   try {
    using(var full=new System.Drawing.Bitmap(area.Width,area.Height)) {
     using(var g=System.Drawing.Graphics.FromImage(full))g.CopyFromScreen(area.Left,area.Top,0,0,area.Size);
     double scale=Math.Min(1,1600.0/area.Width);int w=(int)(area.Width*scale),h=(int)(area.Height*scale);
     string folder=Path.Combine(Path.GetTempPath(),"WinDotra-screen");Directory.CreateDirectory(folder);string path=Path.Combine(folder,"screen.png");
     using(var small=new System.Drawing.Bitmap(w,h)){using(var g=System.Drawing.Graphics.FromImage(small)){g.InterpolationMode=System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;g.DrawImage(full,0,0,w,h);}small.Save(path,System.Drawing.Imaging.ImageFormat.Png);}
     return path;
    }
   } finally { if(hide!=null)hide.Opacity=opacity; }
  }
  public static void Forget(string path){try{if(path!=null&&File.Exists(path))File.Delete(path);}catch(Exception){}}
 }
}
