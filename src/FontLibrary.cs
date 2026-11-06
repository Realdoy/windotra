using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Media;
using Microsoft.Win32;

namespace WinCanvas {
 // Installs fonts for the current Windows account (no admin), and lists free fonts that can be added in one click.
 public static class FontLibrary {
  // Free fonts from the Google Fonts library (SIL Open Font License): name, path in the library, description.
  public static readonly string[][] Free={
   new[]{"Inter","inter/Inter%5Bopsz,wght%5D.ttf","Clean interface sans"},new[]{"Poppins","poppins/Poppins-Regular.ttf","Friendly and geometric"},new[]{"Montserrat","montserrat/Montserrat%5Bwght%5D.ttf","Bold city signage"},
   new[]{"DM Sans","dmsans/DMSans%5Bopsz,wght%5D.ttf","Soft and modern"},new[]{"Outfit","outfit/Outfit%5Bwght%5D.ttf","Rounded geometric"},new[]{"Manrope","manrope/Manrope%5Bwght%5D.ttf","Crisp and technical"},
   new[]{"Sora","sora/Sora%5Bwght%5D.ttf","Wide and futuristic"},new[]{"Space Grotesk","spacegrotesk/SpaceGrotesk%5Bwght%5D.ttf","Quirky grotesk"},new[]{"Nunito","nunito/Nunito%5Bwght%5D.ttf","Round and warm"},
   new[]{"Quicksand","quicksand/Quicksand%5Bwght%5D.ttf","Light and airy"},new[]{"JetBrains Mono","jetbrainsmono/JetBrainsMono%5Bwght%5D.ttf","Coding monospace"},new[]{"Orbitron","orbitron/Orbitron%5Bwght%5D.ttf","Sci-fi display"},
   new[]{"Bebas Neue","bebasneue/BebasNeue-Regular.ttf","Tall headline caps"},new[]{"Righteous","righteous/Righteous-Regular.ttf","Retro display"},new[]{"Press Start 2P","pressstart2p/PressStart2P-Regular.ttf","8-bit pixel"},
   new[]{"Pacifico","pacifico/Pacifico-Regular.ttf","Surfy script"},new[]{"Lobster","lobster/Lobster-Regular.ttf","Bold script"},new[]{"Caveat","caveat/Caveat%5Bwght%5D.ttf","Handwritten"}};
  const string Library="https://github.com/google/fonts/raw/main/ofl/";
  const string FontsKey=@"Software\Microsoft\Windows NT\CurrentVersion\Fonts";
  static string UserFolder {get{return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Microsoft","Windows","Fonts");}}
  [DllImport("gdi32.dll",CharSet=CharSet.Unicode)] static extern int AddFontResource(string file);
  [DllImport("user32.dll")] static extern IntPtr SendMessageTimeout(IntPtr window,uint message,IntPtr w,IntPtr l,uint flags,uint timeout,out IntPtr result);

  // Per-user fonts: family name -> file. Read from the registry so fonts added before this run are included.
  static Dictionary<string,string> UserFonts() {
   var map=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
   try{using(var key=Registry.CurrentUser.OpenSubKey(FontsKey)){if(key!=null)foreach(string name in key.GetValueNames()){var file=key.GetValue(name) as string;if(file==null)continue;if(!Path.IsPathRooted(file))file=Path.Combine(UserFolder,file);if(!File.Exists(file))continue;map[System.Text.RegularExpressions.Regex.Replace(name,@"\s*\((TrueType|OpenType)\)$","")]=file;}}}catch(Exception){}
   return map;
  }
  public static List<string> Families() {
   var names=new HashSet<string>(Fonts.SystemFontFamilies.Select(f=>f.Source),StringComparer.OrdinalIgnoreCase);
   foreach(var item in UserFonts())names.Add(FamilyOf(item.Value)??item.Key);
   return names.OrderBy(n=>n).ToList();
  }
  // False for icon/symbol fonts (Segoe Fluent Icons, Wingdings, ...): they have no ordinary letters.
  public static bool HasLetters(string family) {
   if(String.IsNullOrWhiteSpace(family))return false;
   try{var face=new Typeface(For(family),System.Windows.FontStyles.Normal,System.Windows.FontWeights.Normal,System.Windows.FontStretches.Normal);GlyphTypeface glyphs;if(!face.TryGetGlyphTypeface(out glyphs))return true;if(glyphs.Symbol)return false;if(String.Equals(glyphs.Win32FamilyNames.Values.FirstOrDefault(),family,StringComparison.OrdinalIgnoreCase)||glyphs.FamilyNames.Values.Any(n=>String.Equals(n,family,StringComparison.OrdinalIgnoreCase)))return "AaZz09".All(c=>glyphs.CharacterToGlyphMap.ContainsKey(c));return true;}catch(Exception){return true;}
  }
  public static bool Has(string family){return Families().Contains(family,StringComparer.OrdinalIgnoreCase);}
  // A FontFamily that also works for fonts installed after WinDotra started (loaded straight from the file).
  public static FontFamily For(string family) {
   // Every family carries a sans-serif fallback, so a font that can't load never turns into a serif default.
   const string fallback=", Segoe UI Variable Text, Segoe UI";
   if(String.Equals(family,"Inter",StringComparison.OrdinalIgnoreCase))return Ui.SansFamily;
   if(Fonts.SystemFontFamilies.Any(f=>String.Equals(f.Source,family,StringComparison.OrdinalIgnoreCase)))return new FontFamily(family+fallback);
   foreach(var item in UserFonts()){string name=FamilyOf(item.Value);if(String.Equals(name,family,StringComparison.OrdinalIgnoreCase))return new FontFamily(new Uri(Path.GetDirectoryName(item.Value)+"\\"),"./"+Path.GetFileName(item.Value)+"#"+name+fallback);}
   return new FontFamily(family+fallback);
  }
  static string FamilyOf(string file) {
   try{var face=new GlyphTypeface(new Uri(file));string name;if(face.Win32FamilyNames.TryGetValue(System.Globalization.CultureInfo.GetCultureInfo("en-US"),out name)||face.FamilyNames.TryGetValue(System.Globalization.CultureInfo.GetCultureInfo("en-US"),out name))return name;return face.FamilyNames.Values.FirstOrDefault();}catch(Exception){return null;}
  }
  // Installs a .ttf/.otf/.ttc for this Windows account and returns its family name.
  public static string Install(string source) {
   string ext=Path.GetExtension(source).ToLowerInvariant();if(ext!=".ttf"&&ext!=".otf"&&ext!=".ttc")throw new InvalidOperationException("Choose a .ttf, .otf, or .ttc font file.");
   if(new FileInfo(source).Length>50*1024*1024)throw new InvalidOperationException("That font file is larger than 50 MB.");
   string family=FamilyOf(source);if(String.IsNullOrEmpty(family))throw new InvalidOperationException("That file doesn't look like a font Windows can use.");
   Directory.CreateDirectory(UserFolder);string target=Path.Combine(UserFolder,Path.GetFileName(source).Replace("%5B","[").Replace("%5D","]"));
   if(!String.Equals(Path.GetFullPath(source),Path.GetFullPath(target),StringComparison.OrdinalIgnoreCase))File.Copy(source,target,true);
   using(var key=Registry.CurrentUser.CreateSubKey(FontsKey))key.SetValue(family+(ext==".otf"?" (OpenType)":" (TrueType)"),target);
   AddFontResource(target);IntPtr result;SendMessageTimeout(new IntPtr(0xFFFF),0x001D,IntPtr.Zero,IntPtr.Zero,2,1000,out result);
   return family;
  }
  public static async Task<string> Download(string libraryPath) {
   string temp=Path.Combine(Path.GetTempPath(),"WinDotra-font-"+Guid.NewGuid().ToString("N")+Path.GetExtension(libraryPath.Replace("%5D","")));
   using(var http=new HttpClient{Timeout=TimeSpan.FromMinutes(2)}){var bytes=await http.GetByteArrayAsync(Library+libraryPath);if(bytes.Length>50*1024*1024)throw new InvalidOperationException("Download too large.");File.WriteAllBytes(temp,bytes);}
   string named=Path.Combine(Path.GetTempPath(),Uri.UnescapeDataString(Path.GetFileName(libraryPath)));File.Copy(temp,named,true);File.Delete(temp);
   try{return Install(named);}finally{try{File.Delete(named);}catch(Exception){}}
  }
 }
}
