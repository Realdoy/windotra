using System;
using System.Collections.Generic;
using System.IO;
namespace WinCanvas {
 internal class FakeDesktop : IDesktop {
  public Dictionary<string,int> Values=new Dictionary<string,int>(); public string Wallpaper="original.bmp"; public bool FailNextWrite; public bool FailWallpaper;
  public SavedValue Read(string name) { return new SavedValue {Name=name,Exists=Values.ContainsKey(name),Value=Values.ContainsKey(name)?Values[name]:0}; }
  public void Write(SavedValue v) { if(FailNextWrite) { FailNextWrite=false; throw new IOException("Simulated write failure"); } if(v.Exists) Values[v.Name]=v.Value; else Values.Remove(v.Name); }
  public string GetWallpaper() { return Wallpaper; }
  public void SetWallpaper(string path) { if(FailWallpaper) throw new IOException("Simulated wallpaper failure"); Wallpaper=path; }
  public void Notify() {}
 }
 public static class Tests {
  static void Check(bool value,string name) { if(!value) throw new Exception(name); }
  public static int Run() {
   string dir=Path.Combine(Path.GetTempPath(),"WinDotra-test-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
   string report=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"test-results.txt");
   try {
    var fake=new FakeDesktop(); fake.Values[SettingsEngine.Names[0]]=1;
    var engine=new SettingsEngine(fake,Path.Combine(dir,"undo.json"));
    engine.Apply(false,false,true,"new.bmp"); Check(fake.Values[SettingsEngine.Names[0]]==0 && fake.Wallpaper=="new.bmp","Apply");
    engine.Undo(); Check(fake.Values.Count==1 && fake.Values[SettingsEngine.Names[0]]==1 && fake.Wallpaper=="original.bmp" && !engine.CanUndo,"Restore absent values and wallpaper");
    engine.Apply(false,false,false,null); Check(fake.Wallpaper=="original.bmp","Wallpaper opt-out"); engine.Undo();
    fake.FailNextWrite=true; bool failed=false;
    try { engine.Apply(false,false,false,"new.bmp"); } catch(InvalidOperationException) { failed=true; }
    Check(failed && fake.Values.Count==1 && fake.Wallpaper=="original.bmp" && engine.CanUndo,"Failed apply recovery");
    engine.Apply(false,false,true,"one.bmp"); engine.Apply(true,true,false,"two.bmp"); engine.Undo();
    Check(fake.Wallpaper=="one.bmp" && fake.Values[SettingsEngine.Names[0]]==0,"Repeated apply undo");
    engine.Apply(true,true,false,"three.bmp"); fake.FailWallpaper=true; failed=false;
    try { engine.Undo(); } catch(AggregateException) { failed=true; }
    Check(failed && engine.CanUndo,"Failed undo retains recovery"); fake.FailWallpaper=false; engine.Undo();
    failed=false; try { SettingsEngine.Decode("{\"Version\":9}"); } catch(InvalidDataException) { failed=true; }
    Check(failed,"Reject malformed recovery");
    StudioTests.Run(dir);AiTests.Run(dir);
    File.WriteAllText(report,"PASS: AI protocols, Unicode streaming, cancellation, encryption, history bounds, and error handling; studio profile validation and roundtrip; font and cursor rollback/retry; timer pause/resume/sleep; native struct layouts; apply; exact restore including missing values; wallpaper opt-out; automatic rollback; repeated apply; recovery retry; invalid backup rejection. No Windows settings changed.");
    return 0;
   } catch(Exception e) { File.WriteAllText(report,"FAIL: "+e); return 1; }
   finally { Directory.Delete(dir,true); }
  }
 }
}
