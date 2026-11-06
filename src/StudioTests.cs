using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace WinCanvas {
 class FakeCustomization : ICustomization {
  public FontSnapshot Fonts; public List<CursorValue> Cursors; public int FontFailures,CursorFailures;
  public FakeCustomization() {
   var f=new DesktopFont{Face="Segoe UI",Height=-12,Weight=400};Fonts=new FontSnapshot{Caption=f,SmallCaption=f,Menu=f,Status=f,Message=f};
   Cursors=NativeCustomization.Roles.Select(r=>new CursorValue{Role=r,Exists=r=="Arrow",Path=r=="Arrow"?"%SystemRoot%\\Cursors\\arrow.cur":null,Kind=r=="Arrow"?2:1}).ToList();
  }
  public FontSnapshot ReadFonts(){return Fonts;}
  public void WriteFonts(FontSnapshot value){if(FontFailures-->0)throw new IOException("Mock font failure");Fonts=value;}
  public List<CursorValue> ReadCursors(){return Cursors;}
  public void WriteCursors(List<CursorValue> values){if(CursorFailures-->0)throw new IOException("Mock cursor failure");Cursors=values;}
 }
 public static class StudioTests {
  static void Check(bool value,string message){if(!value)throw new Exception(message);}
  static void Reject(Action action){bool failed=false;try{action();}catch{failed=true;}Check(failed,"Expected rejection");}
  public static void Run(string folder) {
   Check(Marshal.SizeOf(typeof(DesktopFont))==92,"LOGFONTW layout");Check(Marshal.SizeOf(typeof(WindowMetrics))==504,"NONCLIENTMETRICSW layout");
   var profile=new DesktopProfile();profile.Validate();profile.Note=true;profile.Positions["island"]=new Position{X=-400,Y=50};
   string file=Path.Combine(folder,"profile.json");JsonFile.Save(file,profile);var copy=JsonFile.Read<DesktopProfile>(file);copy.Validate();Check(copy.Note&&copy.Positions["island"].X==-400,"Profile roundtrip");
   copy.Opacity=Double.NaN;Reject(copy.Validate);copy.Opacity=1;copy.Accent="red";Reject(copy.Validate);copy.Accent="#AABBCC";copy.Positions["unknown"]=new Position();Reject(copy.Validate);
   var timer=new FocusClock(1);var now=new DateTime(2026,1,1,0,0,0,DateTimeKind.Utc);timer.Toggle(now);timer.Tick(now.AddSeconds(20));Check(timer.Text=="00:40","Timer elapsed");timer.Toggle(now.AddSeconds(20));timer.Tick(now.AddHours(1));Check(timer.Text=="00:40"&&!timer.Running,"Pause");timer.Toggle(now.AddHours(1));timer.Tick(now.AddHours(2));Check(timer.Remaining==TimeSpan.Zero&&!timer.Running,"Timer completion after sleep");timer.Reset(25);Check(timer.Text=="25:00","Timer reset");
   var fake=new FakeCustomization();var service=new CustomizationService(fake,folder);service.ApplyFonts("Consolas",12);Check(fake.Fonts.Menu.Face=="Consolas"&&service.FontUndo,"Font apply");service.UndoFonts();Check(fake.Fonts.Menu.Face=="Segoe UI"&&!service.FontUndo,"Font restore");
   fake.FontFailures=1;Reject(()=>service.ApplyFonts("Arial",10));Check(fake.Fonts.Menu.Face=="Segoe UI"&&service.FontUndo,"Font rollback");
   fake.FontFailures=2;Reject(()=>service.ApplyFonts("Arial",10));Check(service.FontUndo,"Font recovery kept");service.UndoFonts();
   service.ApplyCursorValues(new Dictionary<string,string>{{"Arrow","copied.cur"},{"Hand","hand.ani"}});Check(fake.Cursors[0].Path=="copied.cur"&&fake.Cursors[1].Exists,"Cursor apply");service.UndoCursors();Check(fake.Cursors[0].Kind==2&&!fake.Cursors[1].Exists,"Cursor exact existence/type restore");
   fake.CursorFailures=1;Reject(()=>service.ApplyCursorValues(new Dictionary<string,string>{{"Arrow","next.cur"}}));Check(fake.Cursors[0].Path.Contains("SystemRoot")&&service.CursorUndo,"Cursor rollback");
   fake.CursorFailures=1;Reject(service.UndoCursors);Check(service.CursorUndo,"Cursor undo retry");service.UndoCursors();
   Reject(()=>service.ApplyCursors(new Dictionary<string,string>{{"Arrow","missing.cur"}}));Check(!service.CursorUndo,"Invalid cursor does not touch backup");
   File.WriteAllText(Path.Combine(folder,"fonts-undo.json"),"{}");Reject(service.UndoFonts);
   var parsed=PcActions.Parse("Sure, opening it.\n[[open_app: notepad]]\n[[volume: UP]]\n[[run: del C:\\*]]\n[[open_url: file:///c:/secret]]\n[[focus: 999]]\n[[focus: 25 minutes]]");
   Check(parsed.Count==3&&parsed[0].Kind=="open_app"&&parsed[1].Argument=="up"&&parsed[2].Argument=="25","PC actions: allow-list parsing");
   Check(PcActions.Strip("Done.\n[[lock_pc: now]]")=="Done.","PC actions: markup hidden from transcript");
   // Styles: every built-in applies cleanly; a style that reaches beyond the look is rejected.
   foreach(var style in Styles.BuiltIn())Check(Styles.Apply(new DesktopProfile(),style)!=null,"Style "+style.Name+" applies");
   string sneaky=Path.Combine(folder,"sneaky"+Styles.Extension);File.WriteAllText(sneaky,"{\"Format\":\"windotra-style\",\"Version\":1,\"Name\":\"x\",\"Settings\":{\"Notes\":\"overwritten\"}}");Reject(()=>Styles.Read(sneaky));
   var mac=Styles.Apply(new DesktopProfile(),Styles.BuiltIn()[0]);Check(mac.MenuBar&&mac.Dock&&mac.IslandShape=="Notch"&&mac.Scene=="Hills"&&mac.Positions.ContainsKey("calendar"),"Apple style is a whole desktop");
   string badLayout=Path.Combine(folder,"layout"+Styles.Extension);File.WriteAllText(badLayout,"{\"Format\":\"windotra-style\",\"Version\":1,\"Name\":\"x\",\"Settings\":{\"Layout\":\"everywhere\"}}");Reject(()=>Styles.Read(badLayout));
   var screens=new DesktopProfile();screens.ScreenMedia[@"\\.\DISPLAY1"]="scene:Nope";Reject(screens.Validate);screens.ScreenMedia[@"\\.\DISPLAY1"]="scene:Hills";screens.Validate();screens.IslandColor="red";Reject(screens.Validate);
   var round=Styles.Capture(new DesktopProfile{Accent="#123456"},"Mine","me","test");string mine=Path.Combine(folder,"mine"+Styles.Extension);Styles.Save(round,mine);Check(Styles.Apply(new DesktopProfile(),Styles.Read(mine)).Accent=="#123456","Style export/import round trip");
   // Whole-Windows font swap: apply then restore against a fake registry root, and require an exact round trip.
   const string testRoot=@"Software\WinDotraSelfTest";const string fontsPath=testRoot+@"\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Fonts";Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(testRoot,false);
   using(var fonts=Microsoft.Win32.Registry.CurrentUser.CreateSubKey(fontsPath)){fonts.SetValue("Segoe UI (TrueType)","segoeui.ttf");fonts.SetValue("Segoe UI Bold (TrueType)","segoeuib.ttf");}
   var realRoot=SystemFont.Root;SystemFont.Root=()=>Microsoft.Win32.Registry.CurrentUser.OpenSubKey(testRoot,true);
   try {
    Check(SystemFont.Run("apply",folder,"Poppins")==0&&SystemFont.Current=="Poppins","System font apply");
    using(var fonts=Microsoft.Win32.Registry.CurrentUser.OpenSubKey(fontsPath))Check((string)fonts.GetValue("Segoe UI (TrueType)")==""&&fonts.GetValue("Segoe UI Light (TrueType)")==null,"System font blanks only existing Segoe UI faces");
    Check(SystemFont.Run("apply",folder,"Inter")==0,"System font re-apply keeps first backup");
    Check(SystemFont.Run("restore",folder,"")==0&&SystemFont.Current==null,"System font restore");
    using(var fonts=Microsoft.Win32.Registry.CurrentUser.OpenSubKey(fontsPath))Check((string)fonts.GetValue("Segoe UI (TrueType)")=="segoeui.ttf"&&(string)fonts.GetValue("Segoe UI Bold (TrueType)")=="segoeuib.ttf","System font exact restore");
    Check(SystemFont.Run("apply",folder,"bad\"name")==1,"System font rejects unsafe names");
   } finally { SystemFont.Root=realRoot;Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(testRoot,false); }
  }
 }
}
