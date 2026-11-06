using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Win32;

namespace WinCanvas {
 // Changes the font most Windows apps use by substituting "Segoe UI" with another family (machine-wide registry,
 // so it needs administrator approval and a sign-out). A backup is written before anything changes; Restore puts
 // every value back exactly. Windows 11's Start, Settings, and taskbar use Segoe UI Variable and are left alone.
 public static class SystemFont {
  const string FontsKey=@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Fonts",SubstitutesKey=@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\FontSubstitutes";
  static readonly string[] Faces={"Segoe UI (TrueType)","Segoe UI Bold (TrueType)","Segoe UI Bold Italic (TrueType)","Segoe UI Italic (TrueType)","Segoe UI Light (TrueType)","Segoe UI Semibold (TrueType)","Segoe UI Semilight (TrueType)","Segoe UI Black (TrueType)"};
  public class Backup { public int Version=1; public Dictionary<string,string> Fonts=new Dictionary<string,string>(); public string Substitute; }
  // Tests point this at a throwaway key under HKCU instead of the real machine registry.
  public static Func<RegistryKey> Root=()=>RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,RegistryView.Registry64);
  static RegistryKey Machine {get{return Root();}}
  public static string BackupPath(string folder){return Path.Combine(folder,"system-font-backup.json");}
  // The family Windows apps currently get instead of Segoe UI, or null when Windows is unchanged.
  public static string Current {get{try{using(var key=Machine.OpenSubKey(SubstitutesKey)){var value=key==null?null:key.GetValue("Segoe UI") as string;return String.IsNullOrEmpty(value)?null:value;}}catch(Exception){return null;}}}
  // Runs in the elevated copy of WinDotra started by Request().
  public static int Run(string mode,string folder,string family) {
   try {
    string path=BackupPath(folder);
    if(mode=="apply") {
     if(String.IsNullOrWhiteSpace(family)||family.Length>100||family.IndexOfAny(new[]{'"','\\','\0'})>=0)throw new InvalidDataException("Invalid font name.");
     if(Root==null||!FontLibrary.HasLetters(family))throw new InvalidDataException(family+" is an icon or symbol font with no letters.");
     using(var machine=Machine) {
      // Back up only once, so applying twice still restores the original Windows values.
      if(!File.Exists(path)){var backup=new Backup();using(var fonts=machine.OpenSubKey(FontsKey)){foreach(string face in Faces)backup.Fonts[face]=fonts==null?null:fonts.GetValue(face) as string;}using(var subs=machine.OpenSubKey(SubstitutesKey))backup.Substitute=subs==null?null:subs.GetValue("Segoe UI") as string;JsonFile.Save(path,backup);}
      using(var fonts=machine.OpenSubKey(FontsKey,true)){foreach(string face in Faces)if(fonts.GetValue(face)!=null)fonts.SetValue(face,"",RegistryValueKind.String);}
      using(var subs=machine.CreateSubKey(SubstitutesKey))subs.SetValue("Segoe UI",family,RegistryValueKind.String);
     }
    } else if(mode=="restore") {
     if(!File.Exists(path))throw new InvalidOperationException("No backup found; Windows fonts were not changed by WinDotra.");
     var backup=JsonFile.Read<Backup>(path);if(backup==null||backup.Version!=1||backup.Fonts==null)throw new InvalidDataException("The font backup is damaged.");
     using(var machine=Machine) {
      using(var fonts=machine.OpenSubKey(FontsKey,true)){foreach(var item in backup.Fonts){if(!Faces.Contains(item.Key))continue;if(item.Value==null)fonts.DeleteValue(item.Key,false);else fonts.SetValue(item.Key,item.Value,RegistryValueKind.String);}}
      using(var subs=machine.CreateSubKey(SubstitutesKey)){if(backup.Substitute==null)subs.DeleteValue("Segoe UI",false);else subs.SetValue("Segoe UI",backup.Substitute,RegistryValueKind.String);}
     }
     File.Delete(path);
    } else throw new ArgumentException("Unknown mode.");
    return 0;
   } catch(Exception e) { try{File.WriteAllText(Path.Combine(folder,"system-font-error.txt"),e.Message);}catch(Exception){} return 1; }
  }
  // Starts an elevated copy of WinDotra to make the change. Windows shows its admin prompt; cancelling changes nothing.
  public static void Request(string folder,string mode,string family) {
   string error=Path.Combine(folder,"system-font-error.txt");if(File.Exists(error))File.Delete(error);
   var info=new ProcessStartInfo(System.Reflection.Assembly.GetExecutingAssembly().Location,"--system-font "+mode+" \""+folder+"\" \""+(family??"")+"\""){UseShellExecute=true,Verb="runas"};
   Process process;try{process=Process.Start(info);}catch(Win32Exception e){if(e.NativeErrorCode==1223)throw new InvalidOperationException("Cancelled. Nothing was changed.");throw;}
   process.WaitForExit();
   if(process.ExitCode!=0)throw new InvalidOperationException("Windows fonts weren't changed: "+(File.Exists(error)?File.ReadAllText(error):"unknown error")+".");
  }
 }
}
