using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Windows.Foundation;
using Windows.Media.Control;
using Windows.Storage.Streams;

namespace WinCanvas {
 // Awaits WinRT operations without System.Runtime.WindowsRuntime (which needs the Windows SDK's unified metadata to compile).
 static class WinRt {
  public static Task<T> Wait<T>(IAsyncOperation<T> operation) {
   var result=new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
   operation.Completed=(info,status)=>{try{if(status==AsyncStatus.Completed)result.TrySetResult(info.GetResults());else if(status==AsyncStatus.Canceled)result.TrySetCanceled();else result.TrySetException(info.ErrorCode??new InvalidOperationException("Windows operation failed."));}catch(Exception e){result.TrySetException(e);}};
   return result.Task;
  }
 }

 public class MediaState {
  public bool Has,Playing; public string Title="",Artist="",App=""; public TimeSpan Position,Duration; public ImageSource Art; public Color Tint=Color.FromRgb(0xE5,0xE5,0xEA);
  public double Progress {get{return Duration.TotalSeconds>0?Math.Max(0,Math.Min(1,Position.TotalSeconds/Duration.TotalSeconds)):0;}}
 }

 // Reads what Windows' media overlay shows (Spotify, browsers, Media Player, ...). Read-only except for the
 // play/pause/skip/seek buttons the user clicks. Requires Windows 10 1809 or later; otherwise it stays empty.
 public class MediaWatcher {
  GlobalSystemMediaTransportControlsSessionManager manager; GlobalSystemMediaTransportControlsSession session;
  public readonly MediaState State=new MediaState(); public bool Demo; bool starting,refreshing; string shownTrack=""; DateTime nextProperties=DateTime.MinValue;
  async void Connect() {
   if(starting||manager!=null)return; starting=true;
   try{manager=await WinRt.Wait(GlobalSystemMediaTransportControlsSessionManager.RequestAsync());}catch(Exception){manager=null;}finally{starting=false;}
  }
  public async void Refresh() {
   if(Demo){State.Has=true;State.Playing=true;State.Title="Glass Horizon";State.Artist="Sample artist";State.App="Music";State.Duration=TimeSpan.FromSeconds(214);State.Position=TimeSpan.FromSeconds(83);if(State.Art==null){State.Art=DemoArt();State.Tint=TintOf(State.Art as BitmapSource);}return;}
   if(manager==null){Connect();return;} if(refreshing)return; refreshing=true;
   try {
    session=manager.GetCurrentSession();
    if(session==null){State.Has=false;State.Playing=false;return;}
    var playback=session.GetPlaybackInfo();State.Playing=playback.PlaybackStatus==GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
    var timeline=session.GetTimelineProperties();State.Duration=timeline.EndTime-timeline.StartTime;
    var position=timeline.Position;if(State.Playing&&timeline.LastUpdatedTime.Year>2000)position+=DateTimeOffset.Now-timeline.LastUpdatedTime;
    State.Position=position<TimeSpan.Zero?TimeSpan.Zero:position>State.Duration&&State.Duration>TimeSpan.Zero?State.Duration:position;
    State.App=AppName(session.SourceAppUserModelId);
    if(DateTime.UtcNow>=nextProperties) {
     nextProperties=DateTime.UtcNow.AddSeconds(2);
     var properties=await WinRt.Wait(session.TryGetMediaPropertiesAsync());
     State.Title=properties.Title??"";State.Artist=String.IsNullOrEmpty(properties.Artist)?properties.AlbumArtist??"":properties.Artist;State.Has=State.Title.Length>0;
     string track=State.App+"|"+State.Title+"|"+State.Artist;
     if(track!=shownTrack){shownTrack=track;State.Art=properties.Thumbnail==null?null:await Load(properties.Thumbnail);State.Tint=TintOf(State.Art as BitmapSource);}
    }
   } catch(Exception) { State.Has=false; } finally { refreshing=false; }
  }
  static string AppName(string id) {
   if(String.IsNullOrEmpty(id))return "";string name=id.Split('!')[0];if(name.EndsWith(".exe",StringComparison.OrdinalIgnoreCase))name=name.Substring(0,name.Length-4);int dot=name.LastIndexOf('.');if(dot>=0&&dot<name.Length-1&&name.Contains("_"))name=name.Substring(dot+1).Split('_')[0];
   return name.Length==0?"":Char.ToUpperInvariant(name[0])+name.Substring(1);
  }
  static async Task<ImageSource> Load(IRandomAccessStreamReference reference) {
   try {
    using(var stream=await WinRt.Wait(reference.OpenReadAsync())) {
     if(stream.Size==0||stream.Size>8*1024*1024)return null;
     using(var reader=new DataReader(stream.GetInputStreamAt(0))) {
      uint size=(uint)stream.Size;await WinRt.Wait(reader.LoadAsync(size));var bytes=new byte[size];reader.ReadBytes(bytes);
      var image=new BitmapImage();image.BeginInit();image.StreamSource=new MemoryStream(bytes);image.CacheOption=BitmapCacheOption.OnLoad;image.DecodePixelWidth=160;image.EndInit();image.Freeze();return image;
     }
    }
   } catch(Exception) { return null; }
  }
  // Like Apple's waveform: the most colorful tone of the album art, lifted so it reads on black.
  public static Color TintOf(BitmapSource art) {
   var fallback=Color.FromRgb(0xE5,0xE5,0xEA);if(art==null)return fallback;
   try {
    var pixels=new FormatConvertedBitmap(art,PixelFormats.Bgra32,null,0);int w=pixels.PixelWidth,h=pixels.PixelHeight;var data=new byte[w*h*4];pixels.CopyPixels(data,w*4,0);
    double r=0,g=0,b=0,total=0;
    for(int i=0;i<data.Length;i+=16){double bl=data[i],gr=data[i+1],re=data[i+2];double max=Math.Max(re,Math.Max(gr,bl)),min=Math.Min(re,Math.Min(gr,bl));double weight=Math.Pow((max-min)/255.0,2)*(max/255.0)+0.002;r+=re*weight;g+=gr*weight;b+=bl*weight;total+=weight;}
    r/=total;g/=total;b/=total;double top=Math.Max(r,Math.Max(g,b));if(top<1)return fallback;double lift=235/top;
    Func<double,byte> clamp=v=>(byte)Math.Max(0,Math.Min(255,v));return Color.FromRgb(clamp(r*lift*0.85+255*0.15),clamp(g*lift*0.85+255*0.15),clamp(b*lift*0.85+255*0.15));
   } catch(Exception) { return fallback; }
  }
  static ImageSource DemoArt() {
   var visual=new DrawingVisual();using(var dc=visual.RenderOpen()){dc.DrawRectangle(new LinearGradientBrush(Color.FromRgb(0x63,0x66,0xF1),Color.FromRgb(0xEC,0x48,0x99),45),null,new System.Windows.Rect(0,0,96,96));dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(90,255,255,255)),null,new System.Windows.Point(64,36),26,26);}
   var bitmap=new RenderTargetBitmap(96,96,96,96,PixelFormats.Pbgra32);bitmap.Render(visual);bitmap.Freeze();return bitmap;
  }
  async void Run(Func<GlobalSystemMediaTransportControlsSession,IAsyncOperation<bool>> command) { if(session==null)return; try{await WinRt.Wait(command(session));nextProperties=DateTime.MinValue;Refresh();}catch(Exception){} }
  public void PlayPause(){if(Demo){State.Playing=!State.Playing;return;}Run(s=>s.TryTogglePlayPauseAsync());}
  public void Next(){Run(s=>s.TrySkipNextAsync());}
  public void Previous(){Run(s=>s.TrySkipPreviousAsync());}
  public void Seek(double fraction){if(State.Duration<=TimeSpan.Zero)return;long ticks=(long)(State.Duration.Ticks*Math.Max(0,Math.Min(1,fraction)));Run(s=>s.TryChangePlaybackPositionAsync(ticks));}
 }

 // Peak level of the default speakers, used to drive the island's waveform. Reads a level number only; never audio.
 public class AudioMeter {
  [ComImport,Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")] class DeviceEnumerator {}
  [ComImport,Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface IMMDeviceEnumerator { [PreserveSig] int EnumAudioEndpoints(int flow,int mask,out IntPtr devices); [PreserveSig] int GetDefaultAudioEndpoint(int flow,int role,out IMMDevice device); }
  [ComImport,Guid("D666063F-1587-4E43-81F1-B948E807363F"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface IMMDevice { [PreserveSig] int Activate(ref Guid id,int context,IntPtr parameters,[MarshalAs(UnmanagedType.IUnknown)] out object result); }
  [ComImport,Guid("C02216F6-8C67-4B5B-9D00-D008E73E0064"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface IAudioMeterInformation { [PreserveSig] int GetPeakValue(out float peak); }
  IAudioMeterInformation meter; DateTime refreshAt=DateTime.MinValue;
  public double Peak() {
   try {
    // The default output device can change (headphones, HDMI), so re-acquire it every few seconds.
    if(meter==null||DateTime.UtcNow>=refreshAt) {
     refreshAt=DateTime.UtcNow.AddSeconds(4);var devices=(IMMDeviceEnumerator)new DeviceEnumerator();IMMDevice device;
     if(devices.GetDefaultAudioEndpoint(0,1,out device)!=0||device==null){meter=null;return 0;}
     var id=typeof(IAudioMeterInformation).GUID;object result;meter=device.Activate(ref id,23,IntPtr.Zero,out result)==0?result as IAudioMeterInformation:null;
    }
    float peak;return meter!=null&&meter.GetPeakValue(out peak)==0?Math.Max(0,Math.Min(1,peak)):0;
   } catch(Exception) { meter=null; return 0; }
  }
 }

 public class DownloadItem { public string Name; public long Bytes; public double Speed; }
 // Watches the Downloads folder for browsers' in-progress files (.crdownload, .part, ...). Browsers don't record the
 // final size in those files, so the island shows bytes received and speed rather than a percentage.
 public class DownloadWatcher {
  [DllImport("shell32.dll")] static extern int SHGetKnownFolderPath(ref Guid id,uint flags,IntPtr token,out IntPtr path);
  static readonly string[] Partial={".crdownload",".part",".partial",".download",".opdownload"};
  readonly Dictionary<string,long> last=new Dictionary<string,long>(); DateTime lastPoll=DateTime.UtcNow;
  public readonly string Folder; public List<DownloadItem> Active=new List<DownloadItem>(); public string Finished; public DateTime FinishedAt; public bool Demo;
  public DownloadWatcher() {
   try{var id=new Guid("374DE290-123F-4565-9164-39C4925E467B");IntPtr raw;if(SHGetKnownFolderPath(ref id,0,IntPtr.Zero,out raw)==0){Folder=Marshal.PtrToStringUni(raw);Marshal.FreeCoTaskMem(raw);}}catch(Exception){}
   if(String.IsNullOrEmpty(Folder))Folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"Downloads");
  }
  public static string Size(double bytes){return bytes>=1073741824?(bytes/1073741824).ToString("0.0")+" GB":bytes>=1048576?(bytes/1048576).ToString("0.0")+" MB":(bytes/1024).ToString("0")+" KB";}
  static string Clean(string name){foreach(string ext in Partial)if(name.EndsWith(ext,StringComparison.OrdinalIgnoreCase))return name.Substring(0,name.Length-ext.Length);return name;}
  public void Poll() {
   if(Demo){Active=new List<DownloadItem>{new DownloadItem{Name="holiday-photos.zip",Bytes=31457280*1+2936012,Speed=5452595}};return;}
   var now=DateTime.UtcNow;double seconds=Math.Max(0.5,(now-lastPoll).TotalSeconds);lastPoll=now;var seen=new Dictionary<string,long>();var active=new List<DownloadItem>();
   try {
    if(Directory.Exists(Folder)) foreach(var file in new DirectoryInfo(Folder).EnumerateFiles()) {
     if(!Partial.Any(ext=>file.Name.EndsWith(ext,StringComparison.OrdinalIgnoreCase)))continue;
     if(now-file.LastWriteTimeUtc>TimeSpan.FromMinutes(10))continue; // abandoned partial file
     long before;long bytes=file.Length;seen[file.FullName]=bytes;
     active.Add(new DownloadItem{Name=Clean(file.Name),Bytes=bytes,Speed=last.TryGetValue(file.FullName,out before)?Math.Max(0,(bytes-before)/seconds):0});
    }
   } catch(Exception) {}
   foreach(var gone in last.Keys.Where(k=>!seen.ContainsKey(k)).ToList()){string final=Clean(Path.GetFileName(gone));if(!final.StartsWith("Unconfirmed",StringComparison.OrdinalIgnoreCase)){Finished=final;FinishedAt=now;}}
   last.Clear();foreach(var item in seen)last[item.Key]=item.Value;Active=active;
  }
  public bool RecentlyFinished {get{return Finished!=null&&DateTime.UtcNow-FinishedAt<TimeSpan.FromSeconds(8);}}
 }
}
