using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace WinCanvas {
 public partial class Studio {
  // Saves the scene settings and, if the wallpaper is running, restarts it with them.
  void ApplyScene(){Save();if(Overlays.WallpaperRunning){Overlays.Profile=Profile;Overlays.StartWallpaper();}}
  void StartScene(){Save();Overlays.Profile=Profile;Overlays.StartWallpaper();}
  void PickHex(string current,Action<string> set) {
   using(var dialog=new System.Windows.Forms.ColorDialog{FullOpen=true}){if(current.Length>0)dialog.Color=System.Drawing.ColorTranslator.FromHtml(current);if(dialog.ShowDialog()!=System.Windows.Forms.DialogResult.OK)return;set(String.Format("#{0:X2}{1:X2}{2:X2}",dialog.Color.R,dialog.Color.G,dialog.Color.B));}
  }
  // Applies a slider's value when the mouse is released, so the wallpaper doesn't restart on every tick.
  void OnRelease(Panel panel,Action apply){var slider=panel.Children.OfType<Slider>().Last();slider.PreviewMouseUp+=(s,e)=>Safe(apply);slider.KeyUp+=(s,e)=>Safe(apply);}
  const string PictureFilter="Pictures|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.tif;*.tiff",VideoFilter="Videos|*.mp4;*.m4v;*.mov;*.wmv;*.avi;*.mkv;*.webm";
  // Copies a chosen picture or video into WinDotra's own folder, so moving or deleting the original is fine.
  string Keep(string title,string filter,bool video) {
   var dialog=new Microsoft.Win32.OpenFileDialog{Title=title,Filter=filter};if(dialog.ShowDialog(this)!=true)return null;
   long limit=video?2048L*1024*1024:80L*1024*1024;if(new FileInfo(dialog.FileName).Length>limit)throw new InvalidOperationException(video?"That video is larger than 2 GB.":"That picture is larger than 80 MB.");
   if(!video)using(System.Drawing.Image.FromFile(dialog.FileName)){}
   else using(new VideoDecoder(dialog.FileName)){}
   string dir=Path.Combine(folder,"backgrounds");Directory.CreateDirectory(dir);string copy=Path.Combine(dir,Guid.NewGuid().ToString("N")+Path.GetExtension(dialog.FileName).ToLowerInvariant());
   Report("Copying "+Path.GetFileName(dialog.FileName)+"…");File.Copy(dialog.FileName,copy);return copy;
  }
  void Wallpaper() {
   Header("Live wallpaper","Animated scenes, your photos, or your videos behind your desktop icons. Every screen gets its own, or one picture can span them all. It pauses while a full-screen app or game is in front.");
   var holder=new Border();body.Children.Add(Framed(holder));holder.Child=DesktopPreview(300,false);changed=()=>{holder.Child=DesktopPreview(300,false);};
   var actions=new WrapPanel{Margin=new Thickness(0,0,0,4)};
   actions.Children.Add(Primary(Overlays.WallpaperRunning?"Stop live wallpaper":"Start live wallpaper",ToggleWallpaper));
   if(Overlays.WallpaperRunning){var stats=Text(Overlays.WallpaperStats(),12,Ui.Faint);stats.VerticalAlignment=VerticalAlignment.Center;stats.Margin=new Thickness(8,0,0,0);actions.Children.Add(stats);}
   body.Children.Add(actions);
   var scenes=Card("Scene","");var grid=new UniformGrid2(2);
   for(int i=0;i<WallpaperScene.Kinds.Length;i++){string kind=WallpaperScene.Kinds[i];if((kind=="Photo"&&Profile.ScenePhoto.Length==0)||(kind=="Video"&&Profile.SceneVideo.Length==0))continue;grid.Add(Choice(kind,WallpaperScene.Descriptions[i],Profile.Scene==kind,()=>{Profile.Scene=kind;ApplyScene();Page(current);}));}
   scenes.Children.Add(grid.Panel);
   var mine=Card("Your photo or video","Use any picture or video as your wallpaper. It's copied into WinDotra's folder, so moving the original is fine. Videos loop silently.");var mediaRow=new WrapPanel();
   mediaRow.Children.Add(Primary("Choose a picture…",()=>{var copy=Keep("Choose a background picture",PictureFilter,false);if(copy==null)return;Profile.ScenePhoto=copy;Profile.Scene="Photo";StartScene();Page(current);Report("Your picture is now your desktop background.");}));
   mediaRow.Children.Add(Primary("Choose a video…",()=>{var copy=Keep("Choose a background video",VideoFilter,true);if(copy==null)return;Profile.SceneVideo=copy;Profile.Scene="Video";StartScene();Page(current);Report("Your video is now your desktop background.");}));
   mine.Children.Add(mediaRow);
   Check(mine,"Gentle motion for pictures  ·  slow Ken Burns drift (Fill only)",Profile.PhotoMotion,v=>{Profile.PhotoMotion=v;ApplyScene();Page(current);});
   if(Profile.ScenePhoto.Length>0){var row=new WrapPanel();row.Children.Add(Button("Also set my picture as the Windows wallpaper",()=>SetWindowsWallpaper(Profile.ScenePhoto)));var undo=Button("Restore my previous Windows wallpaper",RestoreWindowsWallpaper);undo.IsEnabled=File.Exists(WallpaperUndoPath);row.Children.Add(undo);mine.Children.Add(row);mine.Children.Add(Text("The Windows wallpaper shows on the lock screen and whenever the live wallpaper is off.",12,Ui.Faint));}
   // Fit and layout: the multi-monitor freedom.
   var fit=Card("Fit and screens","How pictures and videos fill each screen, and whether every screen gets its own copy or one picture spans them all.");
   fit.Children.Add(Text("Fit",13,Ui.Soft));var fits=new WrapPanel();
   foreach(var f in new[]{new[]{"Fill","Fill  ·  crop to cover"},new[]{"Fit","Fit  ·  show all, add bars"},new[]{"Stretch","Stretch"},new[]{"Center","Center  ·  actual size"}}){string id=f[0];fits.Children.Add(Chip(f[1],Profile.WallpaperFit==id,()=>{Profile.WallpaperFit=id;ApplyScene();Page(current);}));}
   fit.Children.Add(fits);
   fit.Children.Add(Text("Screens",13,Ui.Soft));var layouts=new WrapPanel();
   layouts.Children.Add(Chip("Each screen gets its own",Profile.WallpaperLayout=="Each",()=>{Profile.WallpaperLayout="Each";ApplyScene();Page(current);}));
   layouts.Children.Add(Chip("One picture across all screens",Profile.WallpaperLayout=="Span",()=>{Profile.WallpaperLayout="Span";ApplyScene();Page(current);}));
   fit.Children.Add(layouts);
   ScreenChoices(fit);
   var colors=Card("Colors","Pick up to three colors for the scene and a background (also the color of the bars when a picture is fitted). Empty means the scene's own palette.");var swatches=new WrapPanel();
   foreach(var slot in new[]{"1","2","3","bg"}){string key=slot;string value=key=="1"?Profile.SceneColor1:key=="2"?Profile.SceneColor2:key=="3"?Profile.SceneColor3:Profile.SceneBackground;
    swatches.Children.Add(Swatch(key=="bg"?"Background":"Color "+key,value.Length>0?value:"#00000000",()=>PickHex(value,hex=>{if(key=="1")Profile.SceneColor1=hex;else if(key=="2")Profile.SceneColor2=hex;else if(key=="3")Profile.SceneColor3=hex;else Profile.SceneBackground=hex;ApplyScene();Page(current);})));}
   swatches.Children.Add(Button("Reset colors",()=>{Profile.SceneColor1=Profile.SceneColor2=Profile.SceneColor3=Profile.SceneBackground="";ApplyScene();Page(current);}));colors.Children.Add(swatches);
   Check(colors,"Dark scene",Profile.SceneDark,v=>{Profile.SceneDark=v;ApplyScene();Page(current);});
   var motion=Card("Motion","");
   Slider(motion,"Speed",Profile.SceneSpeed,0.1,3,v=>Profile.SceneSpeed=Math.Round(v,2));OnRelease(motion,ApplyScene);
   Slider(motion,"Density  ·  how many dots, lines, stars, or blobs",Profile.SceneDensity,0.3,2,v=>Profile.SceneDensity=Math.Round(v,2));OnRelease(motion,()=>{ApplyScene();Page(current);});
   var speed=Card("Performance","Higher settings look smoother but use more of your graphics card and CPU. Balanced is a good default. Each extra screen adds a little work.");
   var rates=new WrapPanel();foreach(var rate in new[]{new object[]{"Battery saver · 15 fps",15},new object[]{"Balanced · 30 fps",30},new object[]{"Smooth · 60 fps",60}}){int fps=(int)rate[1];rates.Children.Add(Chip((string)rate[0],Profile.SceneFps==fps,()=>{Profile.SceneFps=fps;ApplyScene();Page(current);}));}speed.Children.Add(rates);
   var details=new WrapPanel{Margin=new Thickness(0,6,0,0)};foreach(var level in new[]{new object[]{"Light detail",0.33},new object[]{"Normal detail",0.5},new object[]{"Full detail (heavier)",1.0}}){double value=(double)level[1];details.Children.Add(Chip((string)level[0],Math.Abs(Profile.SceneDetail-value)<0.01,()=>{Profile.SceneDetail=value;ApplyScene();Page(current);}));}speed.Children.Add(details);
   var options=Card("Options","");
   Check(options,"Start with the desktop  ·  runs whenever WinDotra starts",Profile.LiveWallpaper,v=>{Profile.LiveWallpaper=v;Save();});
   options.Children.Add(Text("If Explorer restarts or you plug in a screen, the live wallpaper rebuilds itself. Stopping or quitting shows your normal wallpaper again.",12,Ui.Faint));
  }
  string selectedScreen;
  // What one screen shows: the main choice, or its own picture, video, or scene.
  SceneOptions OptionsFor(string value){var o=SceneOptions.From(Profile);if(value.StartsWith("scene:"))o.Kind=value.Substring(6);else if(value.Length>0){if(SceneOptions.IsVideo(value)){o.Kind="Video";o.Video=value;}else{o.Kind="Photo";o.Photo=value;}}return o;}
  static string Describe(string value){return value.Length==0?"Same as main":value.StartsWith("scene:")?value.Substring(6):SceneOptions.IsVideo(value)?"Own video":"Own picture";}
  // A map of your screens as they're arranged in Windows, each showing its wallpaper. Click one to change it.
  void ScreenChoices(Panel parent) {
   var screens=System.Windows.Forms.Screen.AllScreens.OrderBy(s=>s.Bounds.Left).ToList();if(screens.Count<2&&Profile.ScreenMedia.Count==0)return;
   if(selectedScreen==null||!screens.Any(x=>x.DeviceName==selectedScreen))selectedScreen=screens.First(x=>x.Primary).DeviceName;
   parent.Children.Add(Text("Your screens, as they're arranged. Click one to give it its own wallpaper.",13,Ui.Soft));
   int left=screens.Min(x=>x.Bounds.Left),top=screens.Min(x=>x.Bounds.Top),right=screens.Max(x=>x.Bounds.Right),bottom=screens.Max(x=>x.Bounds.Bottom);
   double scale=Math.Min(640.0/(right-left),230.0/(bottom-top));
   var map=new Canvas{Width=(right-left)*scale,Height=(bottom-top)*scale,Margin=new Thickness(0,10,0,12),HorizontalAlignment=HorizontalAlignment.Center};
   int n=0;
   foreach(var screen in screens){
    n++;string name=screen.DeviceName,value;Profile.ScreenMedia.TryGetValue(name,out value);value=Profile.WallpaperLayout=="Span"?"":value??"";bool selected=name==selectedScreen;
    var face=new Grid{ClipToBounds=true};face.Children.Add(new WallpaperScene(OptionsFor(value)));
    var badge=new Border{Background=Brush("#B0000000"),CornerRadius=new CornerRadius(6),Padding=new Thickness(7,3,7,3),Margin=new Thickness(6),HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Bottom,Child=new TextBlock{Text=n+(screen.Primary?" · main":"")+"  ·  "+Describe(value),FontSize=11,Foreground=Brushes.White}};face.Children.Add(badge);
    var monitor=new Border{Width=screen.Bounds.Width*scale-8,Height=screen.Bounds.Height*scale-8,CornerRadius=new CornerRadius(7),BorderThickness=new Thickness(selected?3:1),BorderBrush=Brush(selected?Ui.Accent:"#55FFFFFF"),ClipToBounds=true,Child=face,Cursor=System.Windows.Input.Cursors.Hand,ToolTip=screen.Bounds.Width+" × "+screen.Bounds.Height};
    monitor.MouseLeftButtonUp+=(o,e)=>{selectedScreen=name;Page(current);};
    Canvas.SetLeft(monitor,(screen.Bounds.Left-left)*scale+4);Canvas.SetTop(monitor,(screen.Bounds.Top-top)*scale+4);map.Children.Add(monitor);
   }
   parent.Children.Add(map);
   if(Profile.WallpaperLayout=="Span"){parent.Children.Add(Text("One picture spans all screens. Switch to “Each screen gets its own” to set them one by one.",12,Ui.Faint));return;}
   var chosen=screens.First(x=>x.DeviceName==selectedScreen);int index=screens.IndexOf(chosen)+1;string current2;Profile.ScreenMedia.TryGetValue(selectedScreen,out current2);current2=current2??"";string key=selectedScreen;
   var heading=Text("Screen "+index+(chosen.Primary?" (main)":"")+"  ·  "+chosen.Bounds.Width+" × "+chosen.Bounds.Height+(chosen.Bounds.Height>chosen.Bounds.Width?" portrait":""),14,Ui.Ink);heading.FontWeight=FontWeights.SemiBold;parent.Children.Add(heading);
   var own=new WrapPanel();
   own.Children.Add(Chip("Same as main",current2.Length==0,()=>{Profile.ScreenMedia.Remove(key);ApplyScene();Page(current);}));
   own.Children.Add(Chip("Picture…",current2.Length>0&&!current2.StartsWith("scene:")&&!SceneOptions.IsVideo(current2),()=>{var copy=Keep("Picture for screen "+index,PictureFilter,false);if(copy==null)return;Profile.ScreenMedia[key]=copy;ApplyScene();Page(current);}));
   own.Children.Add(Chip("Video…",SceneOptions.IsVideo(current2),()=>{var copy=Keep("Video for screen "+index,VideoFilter,true);if(copy==null)return;Profile.ScreenMedia[key]=copy;ApplyScene();Page(current);}));
   parent.Children.Add(own);
   parent.Children.Add(Text("Or an animated scene just for this screen",12,Ui.Faint));var scenes=new WrapPanel();
   foreach(var k in WallpaperScene.Kinds.Where(k=>k!="Photo"&&k!="Video")){string kind=k;scenes.Children.Add(Chip(kind,current2=="scene:"+kind,()=>{Profile.ScreenMedia[key]="scene:"+kind;ApplyScene();Page(current);}));}
   parent.Children.Add(scenes);
   if(Profile.ScreenMedia.Count>0)parent.Children.Add(Ghost("Make every screen the same again",()=>{Profile.ScreenMedia.Clear();ApplyScene();Page(current);}));
  }
  string WallpaperUndoPath{get{return Path.Combine(folder,"windows-wallpaper-undo.json");}}
  public class WallpaperUndo { public string Path,Style,Tile; }
  // Sets the normal Windows wallpaper (with a matching fit) and keeps the previous one for a one-click restore.
  void SetWindowsWallpaper(string picture) {
   var desktop=new WindowsDesktop();
   using(var key=Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"Control Panel\Desktop")){
    if(!File.Exists(WallpaperUndoPath))JsonFile.Save(WallpaperUndoPath,new WallpaperUndo{Path=desktop.GetWallpaper(),Style=(key.GetValue("WallpaperStyle")??"10").ToString(),Tile=(key.GetValue("TileWallpaper")??"0").ToString()});
    string style=Profile.WallpaperLayout=="Span"?"22":Profile.WallpaperFit=="Fit"?"6":Profile.WallpaperFit=="Stretch"?"2":Profile.WallpaperFit=="Center"?"0":"10";
    key.SetValue("WallpaperStyle",style);key.SetValue("TileWallpaper","0");
   }
   desktop.SetWallpaper(picture);Report("Your picture is also your Windows wallpaper now. Restore the previous one here anytime.");Page(current);
  }
  void RestoreWindowsWallpaper() {
   var undo=JsonFile.Read<WallpaperUndo>(WallpaperUndoPath);if(undo==null||undo.Path==null||undo.Style==null||undo.Style.Length>3||undo.Tile==null||undo.Tile.Length>2)throw new InvalidDataException("The saved wallpaper backup is invalid.");
   using(var key=Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"Control Panel\Desktop")){key.SetValue("WallpaperStyle",undo.Style);key.SetValue("TileWallpaper",undo.Tile);}
   new WindowsDesktop().SetWallpaper(undo.Path);File.Delete(WallpaperUndoPath);Report("Your previous Windows wallpaper is back.");Page(current);
  }
  // A small two-column grid that fills row by row.
  class UniformGrid2 { public readonly System.Windows.Controls.Primitives.UniformGrid Panel; public UniformGrid2(int columns){Panel=new System.Windows.Controls.Primitives.UniformGrid{Columns=columns,Margin=new Thickness(0,0,-8,0)};} public void Add(FrameworkElement e){e.Margin=new Thickness(0,0,8,8);Panel.Children.Add(e);} }
 }
}
