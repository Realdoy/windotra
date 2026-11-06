using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace WinCanvas {
 // Everything a scene can be tuned with. Empty colors mean "use the scene's own palette".
 public class SceneOptions {
  public string Kind="Drift",Color1="",Color2="",Color3="",Background="";public bool Dark;public double Speed=1,Density=1;
  public Rect Focus=new Rect(0,0,1,1); // where text scenes center (the primary monitor), as fractions of the canvas
  public string Accent="#4F46E5",Photo="",Video="",Fit="Fill",Layout="Each";public bool PhotoMotion=true;
  public Dictionary<string,string> Screens=new Dictionary<string,string>();
  public static SceneOptions From(DesktopProfile p){return new SceneOptions{Photo=p.ScenePhoto,Video=p.SceneVideo,Fit=p.WallpaperFit,Layout=p.WallpaperLayout,Screens=new Dictionary<string,string>(p.ScreenMedia),PhotoMotion=p.PhotoMotion,Kind=p.Scene,Dark=p.SceneDark,Color1=p.SceneColor1,Color2=p.SceneColor2,Color3=p.SceneColor3,Background=p.SceneBackground,Speed=p.SceneSpeed,Density=p.SceneDensity,Accent=p.Accent};}
  public SceneOptions With(string kind){var copy=(SceneOptions)MemberwiseClone();copy.Kind=kind;return copy;}
  public static readonly string[] VideoTypes={".mp4",".m4v",".mov",".wmv",".avi",".mkv",".webm"};
  public static bool IsVideo(string path){return VideoTypes.Contains(System.IO.Path.GetExtension(path??"").ToLowerInvariant());}
 }
 // One animated scene. Used both as the real wallpaper and as the preview in Studio.
 public class WallpaperScene : Canvas {
  public static readonly string[] Kinds={"Hills","Drift","Gradient","Aurora","Lines","Dots","Stars","Clock","Photo","Video"};
  public static readonly string[] Descriptions={"Layered hills like macOS Sonoma, softly moving","Soft color clouds that drift","A slow, shifting color wash (lightest)","Glowing bands like northern lights","Thin waves, barely moving","A grid that breathes","Stars gliding toward you","A large, quiet clock and the day's progress","Your own picture, still or gently drifting","Your own video, looping and muted"};
  public readonly string Kind; readonly SceneOptions o; readonly DateTime start=DateTime.Now; readonly Color[] palette; readonly Random random=new Random(7);
  Image photo;MediaElement video;readonly List<Shape> shapes=new List<Shape>(); readonly List<double[]> stars=new List<double[]>(); TextBlock time,date; Rectangle day,track; LinearGradientBrush wash;
  static Color C(string hex){return (Color)ColorConverter.ConvertFromString(hex);}
  static Stretch StretchFor(string fit){return fit=="Fit"?Stretch.Uniform:fit=="Stretch"?Stretch.Fill:fit=="Center"?Stretch.None:Stretch.UniformToFill;}
  public WallpaperScene(SceneOptions options) {
   o=options;Kind=o.Kind;ClipToBounds=true;bool dark=o.Dark;
   palette=PaletteFor(o);Background=Ui.Brush(BackgroundFor(o));
   if(Kind=="Photo"){photo=new Image{Stretch=StretchFor(o.Fit),IsHitTestVisible=false};try{if(System.IO.File.Exists(o.Photo)){var image=new BitmapImage();image.BeginInit();image.UriSource=new Uri(o.Photo);image.DecodePixelWidth=1600;image.CacheOption=BitmapCacheOption.OnLoad;image.EndInit();image.Freeze();photo.Source=image;}}catch(Exception){}Children.Add(photo);}
   if(Kind=="Video"){video=new MediaElement{Stretch=StretchFor(o.Fit),IsMuted=true,LoadedBehavior=System.Windows.Controls.MediaState.Manual,UnloadedBehavior=System.Windows.Controls.MediaState.Close,IsHitTestVisible=false};try{if(System.IO.File.Exists(o.Video)){video.Source=new Uri(o.Video);video.MediaEnded+=(s,e)=>{video.Position=TimeSpan.Zero;video.Play();};video.Loaded+=(s,e)=>video.Play();}}catch(Exception){}Children.Add(video);}
   int density=(int)Math.Round(Math.Max(0.3,o.Density)*10);
   if(Kind=="Hills") {
    var sky=new LinearGradientBrush{StartPoint=new Point(0,0),EndPoint=new Point(0,1)};sky.GradientStops.Add(new GradientStop(Lighten(palette[0],dark?0.05:0.55),0));sky.GradientStops.Add(new GradientStop(C(BackgroundFor(o)),1));Background=sky;
    for(int i=0;i<4;i++){var band=HillColors(i);var fill=new LinearGradientBrush(band[0],band[1],new Point(0,0),new Point(0,1));var hill=new Path{Fill=fill,IsHitTestVisible=false,Stroke=new SolidColorBrush(Color.FromArgb((byte)(dark?60:140),255,255,255)),StrokeThickness=1.5};shapes.Add(hill);Children.Add(hill);}
   } else if(Kind=="Drift") {
    int count=Math.Max(3,Math.Min(7,density/2+2));
    for(int i=0;i<count;i++){var c=palette[i%4];var blob=new Ellipse{Fill=new RadialGradientBrush(Color.FromArgb((byte)(dark?110:150),c.R,c.G,c.B),Color.FromArgb(0,c.R,c.G,c.B)),IsHitTestVisible=false};shapes.Add(blob);Children.Add(blob);}
   } else if(Kind=="Gradient") {
    wash=new LinearGradientBrush{StartPoint=new Point(0,0),EndPoint=new Point(1,1)};foreach(var c in palette.Take3())wash.GradientStops.Add(new GradientStop(c,0));Background=wash;
   } else if(Kind=="Aurora") {
    for(int i=0;i<3;i++){var c=palette[i];var band=new Path{Fill=new LinearGradientBrush(new GradientStopCollection{new GradientStop(Color.FromArgb(0,c.R,c.G,c.B),0),new GradientStop(Color.FromArgb(150,c.R,c.G,c.B),0.55),new GradientStop(Color.FromArgb(0,c.R,c.G,c.B),1)},new Point(0,0),new Point(0,1)),IsHitTestVisible=false};shapes.Add(band);Children.Add(band);}
   } else if(Kind=="Lines") {
    int count=Math.Max(4,density);for(int i=0;i<count;i++){bool hero=i==count/2;var line=new Path{Stroke=hero?new SolidColorBrush(palette[3]):Ui.Brush(dark?"#3F3F46":"#D6D3D1"),StrokeThickness=hero?2:1.2,IsHitTestVisible=false};shapes.Add(line);Children.Add(line);}
   } else if(Kind=="Dots") {
    int cols=Math.Max(8,(int)(16*o.Density)),rows=Math.Max(5,(int)(9*o.Density));Tag=new[]{cols,rows};
    var fill=Ui.Brush(dark?"#52525B":"#C9C5C0");for(int i=0;i<cols*rows;i++){var dot=new Ellipse{Fill=fill,IsHitTestVisible=false};shapes.Add(dot);Children.Add(dot);}
   } else if(Kind=="Stars") {
    int count=(int)(160*Math.Max(0.3,o.Density));
    for(int i=0;i<count;i++){var star=new Ellipse{Fill=new SolidColorBrush(i%9==0?palette[3]:Colors.White),IsHitTestVisible=false};shapes.Add(star);Children.Add(star);stars.Add(new[]{random.NextDouble()*2-1,random.NextDouble()*2-1,random.NextDouble()});}
   } else if(Kind=="Clock") {
    time=new TextBlock{FontFamily=Ui.SansFamily,FontWeight=FontWeights.Light,Foreground=Ui.Brush(dark?"#F4F4F5":Ui.Ink)};date=new TextBlock{FontFamily=Ui.SansFamily,Foreground=Ui.Brush(dark?"#A1A1AA":Ui.Soft)};
    track=new Rectangle{Height=2,Fill=Ui.Brush(dark?"#27272A":Ui.Line)};day=new Rectangle{Height=2,Fill=new SolidColorBrush(palette[3])};Children.Add(time);Children.Add(date);Children.Add(track);Children.Add(day);
   }
   SizeChanged+=(s,e)=>Tick();
  }
  public static Color Lighten(Color c,double k){return Color.FromRgb((byte)(c.R+(255-c.R)*k),(byte)(c.G+(255-c.G)*k),(byte)(c.B+(255-c.B)*k));}
  public static Color Darken(Color c,double k){return Color.FromRgb((byte)(c.R*k),(byte)(c.G*k),(byte)(c.B*k));}
  // Top and bottom color of each hill, back to front: lighter and hazier far away, richer up close.
  Color[] HillColors(int i){return HillColorsFor(palette,o.Dark,i);}
  public static Color[] HillColorsFor(Color[] pal,bool dark,int i){var c=pal[i%3];double haze=dark?0.1*(3-i):0.16*(3-i);return new[]{Lighten(c,haze+(dark?0.05:0.25)),dark?Darken(c,0.55-0.08*i):Lighten(Darken(c,0.9-0.05*i),haze*0.4)};}
  public static double HillTop(int i,double x,double t,double h){double phase=t*(0.05+0.015*i)+i*1.3;return h*(0.30+0.16*i)+Math.Sin(phase+x*(2.1+0.5*i))*h*0.07+Math.Sin(phase*0.6+x*(4.3-0.7*i)+i)*h*0.035;}
  public bool Slow {get{return Kind=="Clock";}}
  // Shared with the GPU renderer so the preview and the real wallpaper match.
  public static Color[] PaletteFor(SceneOptions o) {
   bool dark=o.Dark;string[] defaults=o.Kind=="Hills"?(dark?new[]{"#29457A","#5B3A8C","#8C3A62"}:new[]{"#8DB8FF","#C3A6FF","#FFB0C4"}):o.Kind=="Gradient"?(dark?new[]{"#1E1B4B","#0F766E","#831843"}:new[]{"#C7D2FE","#A7F3D0","#FBCFE8"}):o.Kind=="Aurora"?new[]{"#22D3EE","#A78BFA","#34D399"}:dark?new[]{"#0EA5A4","#DB2777","#F59E0B"}:new[]{"#5EEAD4","#FBCFE8","#FDE68A"};
   return new[]{C(o.Color1.Length>0?o.Color1:defaults[0]),C(o.Color2.Length>0?o.Color2:defaults[1]),C(o.Color3.Length>0?o.Color3:defaults[2]),C(o.Accent)};
  }
  public static string BackgroundFor(SceneOptions o){return o.Background.Length>0?o.Background:o.Kind=="Hills"?(o.Dark?"#0A0F1E":"#F1ECFF"):o.Kind=="Stars"||o.Kind=="Aurora"?(o.Dark?"#05060A":"#0B1020"):o.Kind=="Photo"||o.Kind=="Video"?"#000000":o.Dark?"#111113":"#F7F6F3";}
  public void Tick() {
   double w=ActualWidth,h=ActualHeight,t=(DateTime.Now-start).TotalSeconds*o.Speed;if(w<1||h<1)return;
   if(Kind=="Hills") {
    for(int i=0;i<shapes.Count;i++){var g=new StreamGeometry();using(var ctx=g.Open()){ctx.BeginFigure(new Point(0,HillTop(i,0,t,h)),true,true);for(int x=1;x<=64;x++)ctx.LineTo(new Point(w*x/64.0,HillTop(i,x/64.0,t,h)),true,true);ctx.LineTo(new Point(w,h),false,false);ctx.LineTo(new Point(0,h),false,false);}g.Freeze();((Path)shapes[i]).Data=g;}
   } else if(Kind=="Drift") {
    for(int i=0;i<shapes.Count;i++){double size=Math.Max(w,h)*(0.55+0.08*i),speed=0.035+0.012*i;var blob=shapes[i];blob.Width=size;blob.Height=size;
     SetLeft(blob,w/2-size/2+Math.Sin(t*speed+i*1.7)*w*0.32);SetTop(blob,h/2-size/2+Math.Cos(t*speed*1.3+i*2.3)*h*0.3);}
   } else if(Kind=="Gradient") {
    double a=t*0.05;wash.StartPoint=new Point(0.5+0.5*Math.Cos(a),0.5+0.5*Math.Sin(a));wash.EndPoint=new Point(0.5-0.5*Math.Cos(a),0.5-0.5*Math.Sin(a));
    for(int i=0;i<3;i++)wash.GradientStops[i].Offset=Math.Max(0,Math.Min(1,i*0.5+0.12*Math.Sin(t*0.07+i*2)));
   } else if(Kind=="Aurora") {
    for(int i=0;i<shapes.Count;i++){
     var geometry=new StreamGeometry();double top=h*(0.12+i*0.12),thick=h*(0.32+0.05*i),phase=t*(0.12+0.04*i)+i*1.9;
     using(var ctx=geometry.Open()){ctx.BeginFigure(new Point(0,top+Math.Sin(phase)*h*0.08),true,true);for(int x=1;x<=48;x++){double px=w*x/48.0;ctx.LineTo(new Point(px,top+Math.Sin(phase+x*0.13)*h*0.08+Math.Sin(phase*0.7+x*0.05)*h*0.05),false,false);}
      for(int x=48;x>=0;x--){double px=w*x/48.0;ctx.LineTo(new Point(px,top+thick+Math.Sin(phase*1.1+x*0.09)*h*0.06),false,false);}}
     geometry.Freeze();((Path)shapes[i]).Data=geometry;
    }
   } else if(Kind=="Lines") {
    int count=shapes.Count;
    for(int i=0;i<count;i++){
     var geometry=new StreamGeometry();double baseY=h*(0.5+(i-count/2.0)*0.045),amp=h*(0.04+0.012*Math.Abs(count/2-i)),phase=t*0.25+i*0.45;
     using(var ctx=geometry.Open()){ctx.BeginFigure(new Point(0,baseY+Math.Sin(phase)*amp+Math.Sin(phase*0.6)*amp*0.6),false,false);for(int x=1;x<=96;x++){double px=w*x/96.0;ctx.LineTo(new Point(px,baseY+Math.Sin(phase+x*0.07)*amp+Math.Sin(phase*0.6+x*0.023)*amp*0.6),true,true);}}
     geometry.Freeze();((Path)shapes[i]).Data=geometry;
    }
   } else if(Kind=="Dots") {
    var grid=(int[])Tag;int cols=grid[0],rows=grid[1];double gapX=w/(cols+1),gapY=h/(rows+1);
    for(int i=0;i<shapes.Count;i++){int col=i%cols,row=i/cols;double wave=Math.Sin(t*0.9-col*0.35-row*0.25),size=Math.Max(2,Math.Min(gapX,gapY)*(0.06+0.05*(wave+1)));var dot=shapes[i];dot.Width=size;dot.Height=size;dot.Opacity=0.45+0.4*(wave+1)/2;SetLeft(dot,gapX*(col+1)-size/2);SetTop(dot,gapY*(row+1)-size/2);}
   } else if(Kind=="Photo") {
    // Ken Burns-style drift: the picture is 6% larger than the screen and slides very slowly.
    bool drift=o.PhotoMotion&&o.Fit=="Fill";photo.Width=w*(drift?1.06:1);photo.Height=h*(drift?1.06:1);double a=drift?t*0.04:0;SetLeft(photo,drift?-w*0.03*(1+Math.Sin(a)):0);SetTop(photo,drift?-h*0.03*(1+Math.Cos(a*0.8)):0);
   } else if(Kind=="Video") {
    video.Width=w;video.Height=h;
   } else if(Kind=="Stars") {
    double cx=w*(o.Focus.X+o.Focus.Width/2),cy=h*(o.Focus.Y+o.Focus.Height/2),reach=Math.Max(w,h)*0.6;
    for(int i=0;i<stars.Count;i++){var s=stars[i];double z=((s[2]-t*0.04)%1+1)%1,depth=1-z,px=cx+s[0]/Math.Max(0.05,z)*reach*0.15,py=cy+s[1]/Math.Max(0.05,z)*reach*0.15,size=1+depth*depth*3.5;
     var star=shapes[i];star.Width=star.Height=size;star.Opacity=Math.Min(1,depth*1.4);SetLeft(star,px-size/2);SetTop(star,py-size/2);}
   } else {
    var now=DateTime.Now;double fw=w*o.Focus.Width,fh=h*o.Focus.Height,cx=w*o.Focus.X+fw/2,cy=h*o.Focus.Y+fh*0.4;
    time.Text=now.ToString("HH:mm");time.FontSize=Math.Max(8,fh*0.18);date.Text=now.ToString("dddd, d MMMM");date.FontSize=Math.Max(12,fh*0.026);
    time.Measure(new Size(w,h));date.Measure(new Size(w,h));SetLeft(time,cx-time.DesiredSize.Width/2);SetTop(time,cy-time.DesiredSize.Height/2);SetLeft(date,cx-date.DesiredSize.Width/2);SetTop(date,cy+time.DesiredSize.Height/2+fh*0.01);
    double barWidth=fw*0.24,barTop=cy+time.DesiredSize.Height/2+fh*0.08;SetLeft(track,cx-barWidth/2);SetTop(track,barTop);track.Width=barWidth;SetLeft(day,cx-barWidth/2);SetTop(day,barTop);day.Width=barWidth*now.TimeOfDay.TotalHours/24;
   }
  }
 }
 // The same scenes drawn with Direct2D on the GPU, for the real wallpaper.
 class GpuScene {
  readonly SceneOptions o; readonly Direct2D d; readonly float w,h; readonly D2Color back; readonly Color[] pal; readonly List<D2Brush> brushes=new List<D2Brush>(); readonly List<double[]> stars=new List<double[]>();
  readonly D2Brush solid; readonly int count,cols,rows;
  static D2Stop Stop(float at,Color c,float alpha){return new D2Stop{Position=at,Color=Direct2D.Color(c,alpha)};}
  public GpuScene(SceneOptions options,Direct2D direct2d,int width,int height) {
   o=options;d=direct2d;w=width;h=height;pal=WallpaperScene.PaletteFor(o);back=Direct2D.Color((Color)ColorConverter.ConvertFromString(WallpaperScene.BackgroundFor(o)),1);
   solid=d.Solid(new D2Color(1,1,1,1));int density=(int)Math.Round(Math.Max(0.3,o.Density)*10);var random=new Random(7);
   if(o.Kind=="Hills"){brushes.Add(d.Linear(new[]{Stop(0,WallpaperScene.Lighten(pal[0],o.Dark?0.05:0.55),1),Stop(1,(Color)ColorConverter.ConvertFromString(WallpaperScene.BackgroundFor(o)),1)},new D2Point(0,0),new D2Point(0,1)));for(int i=0;i<4;i++){var band=WallpaperScene.HillColorsFor(pal,o.Dark,i);brushes.Add(d.Linear(new[]{Stop(0,band[0],1),Stop(1,band[1],1)},new D2Point(0,0),new D2Point(0,1)));}}
   else if(o.Kind=="Drift"){count=Math.Max(3,Math.Min(7,density/2+2));for(int i=0;i<count;i++){var c=pal[i%4];brushes.Add(d.Radial(new[]{Stop(0,c,o.Dark?0.43f:0.59f),Stop(1,c,0)}));}}
   else if(o.Kind=="Gradient")brushes.Add(d.Linear(new[]{Stop(0,pal[0],1),Stop(0.5f,pal[1],1),Stop(1,pal[2],1)},new D2Point(0,0),new D2Point(1,0)));
   else if(o.Kind=="Aurora")for(int i=0;i<3;i++)brushes.Add(d.Linear(new[]{Stop(0,pal[i],0),Stop(0.55f,pal[i],0.6f),Stop(1,pal[i],0)},new D2Point(0,0),new D2Point(0,1)));
   else if(o.Kind=="Lines")count=Math.Max(4,density);
   else if(o.Kind=="Dots"){cols=Math.Max(8,(int)(16*o.Density));rows=Math.Max(5,(int)(9*o.Density));}
   else if(o.Kind=="Stars"){count=(int)(160*Math.Max(0.3,o.Density));for(int i=0;i<count;i++)stars.Add(new[]{random.NextDouble()*2-1,random.NextDouble()*2-1,random.NextDouble()});}
  }
  public void Draw(double seconds) {
   double t=seconds*o.Speed;d.Begin(back);
   if(o.Kind=="Hills") {
    brushes[0].Place(1,h,0,0);d.Rectangle(0,0,w,h,brushes[0]);const int strips=160;float step=w/strips;var rim=Direct2D.Color(Colors.White,o.Dark?0.22f:0.5f);
    for(int i=0;i<4;i++){var brush=brushes[i+1];float py=0;for(int s=0;s<=strips;s++){float top=(float)WallpaperScene.HillTop(i,s/(double)strips,t,h);brush.Place(1,h-top,0,top);if(s<strips)d.Rectangle(s*step,top,(s+1)*step+0.6f,h,brush);if(s>0){solid.Recolor(rim);d.Line((s-1)*step,py,s*step,top,solid,1.6f);}py=top;}}
   } else if(o.Kind=="Drift") {
    for(int i=0;i<count;i++){float size=(float)(Math.Max(w,h)*(0.55+0.08*i)),r=size/2,speed=(float)(0.035+0.012*i);float x=(float)(w/2+Math.Sin(t*speed+i*1.7)*w*0.32),y=(float)(h/2+Math.Cos(t*speed*1.3+i*2.3)*h*0.3);brushes[i].Place(r,r,x,y);d.Ellipse(x,y,r,r,brushes[i]);}
   } else if(o.Kind=="Gradient") {
    double a=t*0.05;float length=(float)Math.Sqrt(w*w+h*h),cx=w/2,cy=h/2,cos=(float)Math.Cos(a),sin=(float)Math.Sin(a);
    brushes[0].Transform(cos*length,sin*length,-sin*length,cos*length,cx-cos*length/2,cy-sin*length/2);d.Rectangle(0,0,w,h,brushes[0]);
   } else if(o.Kind=="Aurora") {
    const int strips=96;float step=w/strips;
    for(int i=0;i<3;i++){double phase=t*(0.12+0.04*i)+i*1.9;for(int s=0;s<strips;s++){double x=s*48.0/strips;float top=(float)(h*(0.12+i*0.12)+Math.Sin(phase+x*0.13)*h*0.08+Math.Sin(phase*0.7+x*0.05)*h*0.05),bottom=(float)(h*(0.12+i*0.12)+h*(0.32+0.05*i)+Math.Sin(phase*1.1+x*0.09)*h*0.06);brushes[i].Place(1,Math.Max(1,bottom-top),0,top);d.Rectangle(s*step,top,(s+1)*step+0.5f,bottom,brushes[i]);}}
   } else if(o.Kind=="Lines") {
    var gray=o.Dark?Direct2D.Color(Colors.Gray,0.35f):new D2Color(0.84f,0.83f,0.82f,1);
    for(int i=0;i<count;i++){bool hero=i==count/2;solid.Recolor(hero?Direct2D.Color(pal[3],1):gray);double baseY=h*(0.5+(i-count/2.0)*0.045),amp=h*(0.04+0.012*Math.Abs(count/2-i)),phase=t*0.25+i*0.45;
     float px=0,py=(float)(baseY+Math.Sin(phase)*amp+Math.Sin(phase*0.6)*amp*0.6);for(int x=1;x<=96;x++){float nx=w*x/96f,ny=(float)(baseY+Math.Sin(phase+x*0.07)*amp+Math.Sin(phase*0.6+x*0.023)*amp*0.6);d.Line(px,py,nx,ny,solid,hero?2:1.2f);px=nx;py=ny;}}
   } else if(o.Kind=="Dots") {
    float gapX=w/(cols+1),gapY=h/(rows+1);var dot=o.Dark?Color.FromRgb(82,82,91):Color.FromRgb(201,197,192);
    for(int i=0;i<cols*rows;i++){int col=i%cols,row=i/cols;double wave=Math.Sin(t*0.9-col*0.35-row*0.25);float size=(float)Math.Max(2,Math.Min(gapX,gapY)*(0.06+0.05*(wave+1)));solid.Recolor(Direct2D.Color(dot,(float)(0.45+0.4*(wave+1)/2)));d.Ellipse(gapX*(col+1),gapY*(row+1),size/2,size/2,solid);}
   } else if(o.Kind=="Stars") {
    double cx=w*(o.Focus.X+o.Focus.Width/2),cy=h*(o.Focus.Y+o.Focus.Height/2),reach=Math.Max(w,h)*0.6;
    for(int i=0;i<stars.Count;i++){var s=stars[i];double z=((s[2]-t*0.04)%1+1)%1,depth=1-z;float px=(float)(cx+s[0]/Math.Max(0.05,z)*reach*0.15),py=(float)(cy+s[1]/Math.Max(0.05,z)*reach*0.15),size=(float)(1+depth*depth*3.5);solid.Recolor(Direct2D.Color(i%9==0?pal[3]:Colors.White,(float)Math.Min(1,depth*1.4)));d.Ellipse(px,py,size/2,size/2,solid);}
   }
   d.End();
  }
 }
 static class PaletteExtensions { public static IEnumerable<Color> Take3(this Color[] colors){for(int i=0;i<3;i++)yield return colors[i];} }

 // Hosts the wallpaper behind the desktop icons. On Windows 11 24H2+ only GDI-drawn windows show up in that layer, so a
 // background thread draws every frame off-screen and copies it straight to the window with GDI. Each monitor gets its
 // own picture (or one picture can span them all), so nothing is stretched across screens of different shapes.
 // No code runs inside Explorer; stopping restores the normal wallpaper.
 public class LiveWallpaper : IDisposable {
  [StructLayout(LayoutKind.Sequential)] internal struct Rect32 { public int Left,Top,Right,Bottom; }
  [StructLayout(LayoutKind.Sequential)] internal struct BitmapInfo { public int Size,Width,Height; public short Planes,BitCount; public int Compression,ImageSize,XPerMeter,YPerMeter,ColorsUsed,ColorsImportant; }
  delegate bool EnumProc(IntPtr window,IntPtr data);
  [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern IntPtr FindWindow(string cls,string name);
  [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern IntPtr FindWindowEx(IntPtr parent,IntPtr after,string cls,string name);
  [DllImport("user32.dll")] static extern IntPtr SendMessageTimeout(IntPtr window,uint message,IntPtr w,IntPtr l,uint flags,uint timeout,out IntPtr result);
  [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc proc,IntPtr data);
  [DllImport("user32.dll")] static extern IntPtr SetParent(IntPtr child,IntPtr parent);
  [DllImport("user32.dll")] static extern IntPtr GetParent(IntPtr child);
  [DllImport("user32.dll")] static extern bool IsWindow(IntPtr window);
  [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr window,IntPtr after,int x,int y,int width,int height,uint flags);
  [DllImport("user32.dll")] static extern bool GetClientRect(IntPtr window,out Rect32 rect);
  [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr window,out Rect32 rect);
  [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetClassName(IntPtr window,StringBuilder name,int max);
  [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern bool SystemParametersInfo(uint action,uint param,StringBuilder value,uint flags);
  [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr window);
  [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr window,int index);
  [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr window,int index,int value);
  [DllImport("user32.dll")] static extern bool SetLayeredWindowAttributes(IntPtr window,uint key,byte alpha,uint flags);
  [DllImport("user32.dll")] static extern IntPtr GetAncestor(IntPtr window,uint flags);
  [DllImport("user32.dll")] internal static extern IntPtr GetDC(IntPtr window);
  [DllImport("user32.dll")] internal static extern int ReleaseDC(IntPtr window,IntPtr dc);
  [DllImport("gdi32.dll")] internal static extern int SetStretchBltMode(IntPtr dc,int mode);
  [DllImport("gdi32.dll")] internal static extern bool SetBrushOrgEx(IntPtr dc,int x,int y,IntPtr previous);
  [DllImport("gdi32.dll")] internal static extern int StretchDIBits(IntPtr dc,int x,int y,int width,int height,int sx,int sy,int sw,int sh,IntPtr bits,ref BitmapInfo info,uint usage,uint rop);
  [DllImport("gdi32.dll")] internal static extern IntPtr CreateCompatibleDC(IntPtr dc);
  [DllImport("gdi32.dll")] internal static extern IntPtr CreateDIBSection(IntPtr dc,ref BitmapInfo info,uint usage,out IntPtr bits,IntPtr section,uint offset);
  [DllImport("gdi32.dll")] internal static extern IntPtr SelectObject(IntPtr dc,IntPtr obj);
  [DllImport("gdi32.dll")] internal static extern bool DeleteObject(IntPtr obj);
  [DllImport("gdi32.dll")] internal static extern bool DeleteDC(IntPtr dc);
  [DllImport("gdi32.dll")] internal static extern bool StretchBlt(IntPtr dest,int x,int y,int w,int h,IntPtr src,int sx,int sy,int sw,int sh,uint rop);
  [DllImport("gdi32.dll")] internal static extern bool BitBlt(IntPtr dest,int x,int y,int w,int h,IntPtr src,int sx,int sy,uint rop);
  [DllImport("gdi32.dll")] internal static extern IntPtr CreateSolidBrush(int color);
  [DllImport("user32.dll")] internal static extern int FillRect(IntPtr dc,ref Rect32 rect,IntPtr brush);
  [DllImport("winmm.dll")] static extern uint timeBeginPeriod(uint period);
  [DllImport("winmm.dll")] static extern uint timeEndPeriod(uint period);
  class Host : System.Windows.Forms.Form {
   public Host(){SetStyle(System.Windows.Forms.ControlStyles.AllPaintingInWmPaint|System.Windows.Forms.ControlStyles.UserPaint|System.Windows.Forms.ControlStyles.Opaque,true);FormBorderStyle=System.Windows.Forms.FormBorderStyle.None;ShowInTaskbar=false;StartPosition=System.Windows.Forms.FormStartPosition.Manual;Location=new System.Drawing.Point(-32000,-32000);BackColor=System.Drawing.Color.Black;}
   protected override bool ShowWithoutActivation {get{return true;}}
   protected override void OnPaintBackground(System.Windows.Forms.PaintEventArgs e){}
  }
  // Each start gets its own run: a render thread only ever draws into its own window, and stops when its run ends,
  // even if an older thread was still busy (preparing a big photo, say) when the wallpaper restarted.
  class RunState { public volatile bool On=true; public IntPtr Window; }
  Host window; IntPtr handle,layerWindow; Thread renderer; RunState run; volatile bool paused; string mode="";
  SceneOptions lastOptions; int lastFps; double lastDetail; Rect32 lastArea;
  readonly DispatcherTimer watch=new DispatcherTimer(); readonly Dispatcher dispatcher;
  public bool Running {get{return window!=null;}} public bool Paused {get{return paused;}}
  public double FrameMilliseconds {get;private set;} public double FramesPerSecond {get;private set;} public double DrawMilliseconds {get;private set;}
  public string Problem {get;private set;}
  public Action<string> Report;
  public static int StretchMode=0; public static string TestPhoto; // diagnostics: 0 = automatic, otherwise a GDI stretch mode
  public LiveWallpaper() {
   dispatcher=Dispatcher.CurrentDispatcher;
   // Pause while a full-screen app or game covers the screen; reattach if Explorer restarted or the screens changed.
   watch.Interval=TimeSpan.FromSeconds(2);watch.Tick+=(s,e)=>{paused=Covered();Heal();};
   // Windows sends "display changed" for many reasons (including the wallpaper attaching itself), so wait a moment and
   // only rebuild when the screens really are different. Rebuilding on every notice used to loop and freeze the app.
   screenCheck.Interval=TimeSpan.FromSeconds(1.5);screenCheck.Tick+=(s,e)=>{screenCheck.Stop();if(Running&&ScreenLayout()!=lastScreens)Restart("Your screens changed, so the live wallpaper was rebuilt for them.");};
   Microsoft.Win32.SystemEvents.DisplaySettingsChanged+=(s,e)=>dispatcher.BeginInvoke(new Action(()=>{screenCheck.Stop();screenCheck.Start();}));
  }
  readonly DispatcherTimer screenCheck=new DispatcherTimer(); string lastScreens=""; DateTime lastStart=DateTime.MinValue;
  static string ScreenLayout(){return String.Join(";",System.Windows.Forms.Screen.AllScreens.Select(s=>s.DeviceName+"="+s.Bounds));}
  void Heal() {
   if(window==null||(DateTime.Now-lastStart).TotalSeconds<10)return;Rect32 area;
   bool lost=!IsWindow(layerWindow)||GetParent(handle)!=layerWindow||(GetClientRect(layerWindow,out area)&&(area.Right-area.Left!=lastArea.Right-lastArea.Left||area.Bottom-area.Top!=lastArea.Bottom-lastArea.Top));
   if(lost)Restart("The desktop was reset (Explorer restarted), so the live wallpaper reattached itself.");
  }
  public static void Log(string text){try{System.IO.File.AppendAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(),"windotra-wallpaper.log"),DateTime.Now.ToString("HH:mm:ss.fff")+" "+text+Environment.NewLine);}catch(Exception){}}
  void Restart(string why){Log("restart: "+why);try{Start(lastOptions,lastFps,lastDetail);if(Report!=null)Report(why);}catch(Exception e){Problem=e.Message;if(Report!=null)Report(e.Message);}}
  static IntPtr Progman() {
   IntPtr progman=FindWindow("Progman",null),result;if(progman==IntPtr.Zero)return IntPtr.Zero;
   SendMessageTimeout(progman,0x052C,new IntPtr(0xD),new IntPtr(1),0,1000,out result);SendMessageTimeout(progman,0x052C,IntPtr.Zero,IntPtr.Zero,0,1000,out result);
   return progman;
  }
  static IntPtr ClassicLayer() {
   IntPtr worker=IntPtr.Zero;
   EnumWindows((top,data)=>{if(FindWindowEx(top,IntPtr.Zero,"SHELLDLL_DefView",null)!=IntPtr.Zero){var next=FindWindowEx(IntPtr.Zero,top,"WorkerW",null);if(next!=IntPtr.Zero&&IsWindowVisible(next))worker=next;}return true;},IntPtr.Zero);
   return worker;
  }
  static bool Covered() {
   IntPtr top=GetForegroundWindow();if(top==IntPtr.Zero)return false;var name=new StringBuilder(64);GetClassName(top,name,64);string cls=name.ToString();if(cls=="WorkerW"||cls=="Progman"||cls=="Shell_TrayWnd")return false;
   Rect32 r;if(!GetWindowRect(top,out r))return false;var screen=System.Windows.Forms.Screen.FromHandle(top).Bounds;return r.Left<=screen.Left&&r.Top<=screen.Top&&r.Right>=screen.Right&&r.Bottom>=screen.Bottom;
  }
  // fps: frames per second; detail: render scale (1 = full resolution).
  public void Start(SceneOptions options,int fps,double detail) {
   var started=Stopwatch.StartNew();Log("start "+options.Kind);lastStart=DateTime.Now;lastScreens=ScreenLayout();
   Stop(false);IntPtr progman=Progman();lastOptions=options;lastFps=fps;lastDetail=detail;Problem=null;
   if(progman==IntPtr.Zero)throw new InvalidOperationException("Windows didn't expose the desktop, so the live wallpaper can't start. Your normal wallpaper is unchanged.");
   IntPtr icons=FindWindowEx(progman,IntPtr.Zero,"SHELLDLL_DefView",null),classic=icons==IntPtr.Zero?ClassicLayer():IntPtr.Zero;
   if(icons==IntPtr.Zero&&classic==IntPtr.Zero)throw new InvalidOperationException("Windows didn't expose the wallpaper layer, so the live wallpaper can't start. Your normal wallpaper is unchanged.");
   window=new Host();handle=window.Handle;Rect32 area;
   SetWindowLong(handle,-16,(GetWindowLong(handle,-16)&~unchecked((int)0x80C00000))|0x40000000);
   if(icons!=IntPtr.Zero) {
    // 24H2+: a layered child of Progman, ordered below the icons and above Windows' own wallpaper.
    mode="progman";layerWindow=progman;SetWindowLong(handle,-20,GetWindowLong(handle,-20)|0x00080000);SetLayeredWindowAttributes(handle,0,255,2);
    SetParent(handle,progman);GetClientRect(progman,out area);SetWindowPos(handle,icons,0,0,area.Right-area.Left,area.Bottom-area.Top,0x0010|0x0040);
    IntPtr wallpaper=FindWindowEx(progman,IntPtr.Zero,"WorkerW",null);if(wallpaper!=IntPtr.Zero)SetWindowPos(wallpaper,handle,0,0,0,0,0x0001|0x0002|0x0010);
   } else {
    mode="workerw";layerWindow=classic;SetParent(handle,classic);GetClientRect(classic,out area);SetWindowPos(handle,IntPtr.Zero,0,0,area.Right-area.Left,area.Bottom-area.Top,0x0004|0x0010|0x0040);
   }
   lastArea=area;int width=Math.Max(1,area.Right-area.Left),height=Math.Max(1,area.Bottom-area.Top);
   // Where each monitor sits inside the wallpaper window.
   Rect32 layerRect;GetWindowRect(layerWindow,out layerRect);var screens=new List<Screen32>();
   foreach(var s in System.Windows.Forms.Screen.AllScreens){var b=s.Bounds;screens.Add(new Screen32{Name=s.DeviceName,Primary=s.Primary,X=b.Left-layerRect.Left,Y=b.Top-layerRect.Top,W=b.Width,H=b.Height});}
   var primary=screens.FirstOrDefault(s=>s.Primary)??new Screen32{W=width,H=height};
   options.Focus=new Rect(primary.X/(double)width,primary.Y/(double)height,primary.W/(double)width,primary.H/(double)height);
   window.Visible=true;paused=false;var current=new RunState{Window=handle};run=current;
   var plan=screens;bool span=options.Layout=="Span";
   renderer=new Thread(()=>Render(current,options,width,height,span?null:plan,Math.Max(5,Math.Min(60,fps)),Math.Max(0.2,Math.Min(1,detail)))){IsBackground=true,Priority=ThreadPriority.BelowNormal,Name="WinDotra wallpaper"};
   renderer.SetApartmentState(ApartmentState.STA);renderer.Start();watch.Start();Log("start done in "+started.ElapsedMilliseconds+"ms");
  }
  internal class Screen32 { public string Name; public bool Primary; public int X,Y,W,H; }

  // One region of the wallpaper (a monitor, or every monitor together when spanning) and what it shows.
  abstract class Tile {
   public int X,Y,W,H; public double Interval=1000; public double Last=-1e9;
   public abstract void Draw(IntPtr dc,double seconds);
   public virtual void Dispose(){}
   protected static void Bars(IntPtr dc,int x,int y,int w,int h,int ix,int iy,int iw,int ih,int color){var brush=CreateSolidBrush(color);try{var r=new Rect32{Left=x,Top=y,Right=x+w,Bottom=iy};if(iy>y)FillRect(dc,ref r,brush);r=new Rect32{Left=x,Top=iy+ih,Right=x+w,Bottom=y+h};if(iy+ih<y+h)FillRect(dc,ref r,brush);r=new Rect32{Left=x,Top=iy,Right=ix,Bottom=iy+ih};if(ix>x)FillRect(dc,ref r,brush);r=new Rect32{Left=ix+iw,Top=iy,Right=x+w,Bottom=iy+ih};if(ix+iw<x+w)FillRect(dc,ref r,brush);}finally{DeleteObject(brush);}}
   // Where a picture of size sw×sh lands inside w×h for each fit mode (may be larger than the tile when filling).
   public static void Place(string fit,int sw,int sh,int w,int h,out int dx,out int dy,out int dw,out int dh){
    double scale=fit=="Fit"?Math.Min(w/(double)sw,h/(double)sh):fit=="Center"?1:Math.Max(w/(double)sw,h/(double)sh);
    if(fit=="Stretch"){dx=0;dy=0;dw=w;dh=h;return;}dw=(int)Math.Round(sw*scale);dh=(int)Math.Round(sh*scale);dx=(w-dw)/2;dy=(h-dh)/2;}
   protected static int Gdi(string hex){var c=(Color)ColorConverter.ConvertFromString(hex);return c.R|(c.G<<8)|(c.B<<16);}
  }
  class PhotoTile : Tile {
   IntPtr memory,bitmap,old;int pw,ph;bool drift;SceneOptions o;
   public PhotoTile(SceneOptions options,string path,int x,int y,int w,int h){
    X=x;Y=y;W=w;H=h;o=options;drift=o.PhotoMotion&&o.Fit=="Fill";Interval=drift?50:1000;pw=drift?(int)(w*1.06):w;ph=drift?(int)(h*1.06):h;
    using(var big=new System.Drawing.Bitmap(pw,ph)){
     using(var g=System.Drawing.Graphics.FromImage(big)){var back=(Color)ColorConverter.ConvertFromString(WallpaperScene.BackgroundFor(o));g.Clear(System.Drawing.Color.FromArgb(back.R,back.G,back.B));g.InterpolationMode=System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;g.PixelOffsetMode=System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
      if(System.IO.File.Exists(path))using(var source=System.Drawing.Image.FromFile(path)){int dx,dy,dw,dh;Place(o.Fit,source.Width,source.Height,pw,ph,out dx,out dy,out dw,out dh);g.DrawImage(source,dx,dy,dw,dh);}}
     bitmap=big.GetHbitmap();
    }
    memory=CreateCompatibleDC(IntPtr.Zero);old=SelectObject(memory,bitmap);
   }
   public override void Draw(IntPtr dc,double seconds){double a=drift?seconds*o.Speed*0.04:0;int dx=drift?(int)((pw-W)/2.0*(1+Math.Sin(a))):0,dy=drift?(int)((ph-H)/2.0*(1+Math.Cos(a*0.8))):0;BitBlt(dc,X,Y,W,H,memory,dx,dy,0x00CC0020);}
   public override void Dispose(){if(memory!=IntPtr.Zero){SelectObject(memory,old);DeleteObject(bitmap);DeleteDC(memory);memory=IntPtr.Zero;}}
  }
  class SceneTile : Tile {
   IntPtr memory,bitmap,old,bits;int pw,ph;Direct2D gpu;GpuScene painter;
   public SceneTile(SceneOptions o,int x,int y,int w,int h,int fps,double detail){
    X=x;Y=y;W=w;H=h;Interval=1000.0/fps;pw=Math.Max(1,(int)(w*detail));ph=Math.Max(1,(int)(h*detail));
    var info=new BitmapInfo{Size=40,Width=pw,Height=-ph,Planes=1,BitCount=32};memory=CreateCompatibleDC(IntPtr.Zero);bitmap=CreateDIBSection(memory,ref info,0,out bits,IntPtr.Zero,0);old=SelectObject(memory,bitmap);
    gpu=new Direct2D();gpu.Bind(memory,pw,ph);painter=new GpuScene(o,gpu,pw,ph);
   }
   public override void Draw(IntPtr dc,double seconds){painter.Draw(seconds);SetStretchBltMode(dc,StretchMode>0?StretchMode:3);StretchBlt(dc,X,Y,W,H,memory,0,0,pw,ph,0x00CC0020);}
   public override void Dispose(){if(gpu!=null){gpu.Dispose();gpu=null;}if(memory!=IntPtr.Zero){SelectObject(memory,old);DeleteObject(bitmap);DeleteDC(memory);memory=IntPtr.Zero;}}
  }
  class ClockTile : Tile {
   IntPtr memory,bitmap,old,bits;WallpaperScene scene;RenderTargetBitmap target;
   public ClockTile(SceneOptions o,int x,int y,int w,int h){
    X=x;Y=y;W=w;H=h;Interval=1000;var info=new BitmapInfo{Size=40,Width=w,Height=-h,Planes=1,BitCount=32};memory=CreateCompatibleDC(IntPtr.Zero);bitmap=CreateDIBSection(memory,ref info,0,out bits,IntPtr.Zero,0);old=SelectObject(memory,bitmap);
    scene=new WallpaperScene(o);scene.Measure(new Size(w,h));scene.Arrange(new Rect(0,0,w,h));scene.UpdateLayout();target=new RenderTargetBitmap(w,h,96,96,PixelFormats.Pbgra32);
   }
   public override void Draw(IntPtr dc,double seconds){scene.Tick();scene.UpdateLayout();target.Clear();target.Render(scene);target.CopyPixels(new Int32Rect(0,0,W,H),bits,W*H*4,W*4);BitBlt(dc,X,Y,W,H,memory,0,0,0x00CC0020);}
   public override void Dispose(){if(memory!=IntPtr.Zero){SelectObject(memory,old);DeleteObject(bitmap);DeleteDC(memory);memory=IntPtr.Zero;}}
  }
  class VideoTile : Tile {
   readonly VideoDecoder video;readonly string fit;readonly int back;bool barsDrawn;
   public VideoTile(VideoDecoder decoder,SceneOptions o,int x,int y,int w,int h,int fps){X=x;Y=y;W=w;H=h;video=decoder;fit=o.Fit;back=Gdi(WallpaperScene.BackgroundFor(o));Interval=Math.Max(1000.0/fps,decoder.FrameSeconds*1000*0.5);}
   public override void Draw(IntPtr dc,double seconds){
    if(!video.HasFrame)return;int dx,dy,dw,dh;Place(fit,video.Width,video.Height,W,H,out dx,out dy,out dw,out dh);
    var info=new BitmapInfo{Size=40,Width=video.Width,Height=video.Height,Planes=1,BitCount=32};
    // Crop instead of drawing outside the tile, so neighbouring monitors are never painted over.
    int sx=0,sy=0,sw=video.Width,sh=video.Height,tx=dx,ty=dy,tw=dw,th=dh;
    if(dw>W){double k=video.Width/(double)dw;sx=(int)(-dx*k);sw=(int)(W*k);tx=0;tw=W;}
    if(dh>H){double k=video.Height/(double)dh;sy=(int)(-dy*k);sh=(int)(H*k);ty=0;th=H;}
    if(!barsDrawn||fit=="Fit"||fit=="Center"){Bars(dc,X,Y,W,H,X+tx,Y+ty,tw,th,back);barsDrawn=true;}
    SetStretchBltMode(dc,StretchMode>0?StretchMode:3);
    // The frame is a bottom-up DIB, so StretchDIBits counts source rows from the bottom.
    StretchDIBits(dc,X+tx,Y+ty,tw,th,sx,video.Height-sy-sh,sw,sh,video.Frame,ref info,0,0x00CC0020);
   }
  }

  List<Tile> Build(SceneOptions options,int width,int height,List<Screen32> screens,int fps,double detail,Dictionary<string,VideoDecoder> videos) {
   var tiles=new List<Tile>();
   var regions=screens??new List<Screen32>{new Screen32{Name="all",X=0,Y=0,W=width,H=height,Primary=true}};
   foreach(var r in regions) {
    string kind=options.Kind,media=null;string custom;
    if(screens!=null&&options.Screens!=null&&options.Screens.TryGetValue(r.Name,out custom)&&!String.IsNullOrEmpty(custom)){
     if(custom.StartsWith("scene:"))kind=custom.Substring(6);else{media=custom;kind=SceneOptions.IsVideo(custom)?"Video":"Photo";}}
    if(kind=="Photo"&&media==null)media=options.Photo;if(kind=="Video"&&media==null)media=options.Video;
    var o=options.With(kind);o.Focus=screens!=null?new Rect(0,0,1,1):options.Focus;
    if(kind=="Photo")tiles.Add(new PhotoTile(o,media??TestPhoto??"",r.X,r.Y,r.W,r.H));
    else if(kind=="Video"){
     VideoDecoder decoder;if(!videos.TryGetValue(media??"",out decoder)){if(!System.IO.File.Exists(media??""))throw new InvalidOperationException("The wallpaper video is missing. Choose it again on the Live wallpaper page.");decoder=new VideoDecoder(media);videos[media]=decoder;}
     tiles.Add(new VideoTile(decoder,o,r.X,r.Y,r.W,r.H,fps));}
    else if(kind=="Clock")tiles.Add(new ClockTile(o,r.X,r.Y,r.W,r.H));
    else tiles.Add(new SceneTile(o,r.X,r.Y,r.W,r.H,fps,detail));
   }
   return tiles;
  }
  void Render(RunState me,SceneOptions options,int width,int height,List<Screen32> screens,int fps,double detail) {
   var videos=new Dictionary<string,VideoDecoder>();List<Tile> tiles=null;timeBeginPeriod(1);
   try {
    try{tiles=Build(options,width,height,screens,fps,detail,videos);}
    catch(Exception e){Problem=e.Message;var report=Report;if(report!=null)dispatcher.BeginInvoke(new Action(()=>report("Live wallpaper: "+e.Message)));tiles=new List<Tile>();}
    var clock=Stopwatch.StartNew();double average=0;int frames=0;var second=Stopwatch.StartNew();bool first=true;
    while(me.On) {
     if(paused){Thread.Sleep(250);first=true;continue;}
     double began=clock.Elapsed.TotalMilliseconds,seconds=clock.Elapsed.TotalSeconds;
     foreach(var video in videos.Values){video.Update(seconds);VideoInfo=" video="+video.Width+"x"+video.Height+" decode="+video.DecodeMilliseconds.ToString("0.0")+"ms";}
     // Never GetDC(0): that is the whole screen. Only draw into this run's own, still-existing window.
     IntPtr target=me.Window;IntPtr dc=me.On&&target!=IntPtr.Zero&&IsWindow(target)?GetDC(target):IntPtr.Zero;
     if(dc!=IntPtr.Zero){try{foreach(var tile in tiles)if(first||began-tile.Last>=tile.Interval-1){tile.Draw(dc,seconds);tile.Last=began;}}finally{ReleaseDC(target,dc);}}
     first=false;double spent=clock.Elapsed.TotalMilliseconds-began;average=average==0?spent:average*0.9+spent*0.1;FrameMilliseconds=average;DrawMilliseconds=average;frames++;
     if(second.ElapsedMilliseconds>=1000){FramesPerSecond=frames*1000.0/second.ElapsedMilliseconds;frames=0;second.Restart();}
     double now=clock.Elapsed.TotalMilliseconds,wait=tiles.Count==0?500:tiles.Min(t=>t.Last+t.Interval)-now;
     Thread.Sleep(wait>1?(int)Math.Min(wait,500):1);
    }
   } finally {
    timeEndPeriod(1);if(tiles!=null)foreach(var tile in tiles)tile.Dispose();foreach(var video in videos.Values)video.Dispose();
   }
  }
  public void Stop(){Stop(true);}
  void Stop(bool restore) {
   watch.Stop();if(run!=null){run.On=false;run.Window=IntPtr.Zero;run=null;}if(renderer!=null){renderer.Join(600);renderer=null;} // a busy old thread finishes on its own; it can no longer draw
   if(window==null)return;window.Close();window.Dispose();window=null;handle=IntPtr.Zero;
   // Ask Windows to repaint the static wallpaper that was behind the scene (no settings are written).
   if(restore){var path=new StringBuilder(32768);if(SystemParametersInfo(0x0073,(uint)path.Capacity,path,0))SystemParametersInfo(0x0014,0,path,0);}
  }
  // Diagnostic for --wallpaper-check: attach, run, and report placement and speed.
  public string Check(string kind,int fps,double detail,string layout,string fit,string media) {
   var o=new SceneOptions{Kind=kind,Dark=true,Photo=TestPhoto??"",Layout=layout,Fit=fit};if(kind=="Video")o.Video=media;Start(o,fps,detail);var parent=GetAncestor(handle,1);var name=new StringBuilder(64);GetClassName(parent,name,64);Rect32 r;GetWindowRect(handle,out r);
   return "mode="+mode+" parent="+name+" visible="+IsWindowVisible(handle)+" rect="+r.Left+","+r.Top+","+r.Right+","+r.Bottom+" screens="+System.Windows.Forms.Screen.AllScreens.Length;
  }
  public static string VideoInfo="";
  // Diagnostics: draws one frame of a GPU scene off-screen and saves it.
  public static void RenderTest(string kind,bool dark,int w,int h,string file) {
   var info=new BitmapInfo{Size=40,Width=w,Height=-h,Planes=1,BitCount=32};IntPtr bits,memory=CreateCompatibleDC(IntPtr.Zero),bitmap=CreateDIBSection(memory,ref info,0,out bits,IntPtr.Zero,0),old=SelectObject(memory,bitmap);
   using(var gpu=new Direct2D()){gpu.Bind(memory,w,h);new GpuScene(new SceneOptions{Kind=kind,Dark=dark},gpu,w,h).Draw(10);}
   SelectObject(memory,old);using(var image=System.Drawing.Image.FromHbitmap(bitmap))image.Save(file);DeleteObject(bitmap);DeleteDC(memory);
  }
  // Diagnostics: copies what the wallpaper window shows into a small PNG.
  public void Snapshot(string file) {
   Rect32 r;GetClientRect(handle,out r);int w=r.Right,h=r.Bottom;if(w<=0||h<=0)return;
   using(var full=new System.Drawing.Bitmap(w,h)){using(var g=System.Drawing.Graphics.FromImage(full)){IntPtr target=g.GetHdc();IntPtr dc=GetDC(handle);BitBlt(target,0,0,w,h,dc,0,0,0x00CC0020);ReleaseDC(handle,dc);g.ReleaseHdc(target);}
    using(var small=new System.Drawing.Bitmap(w/4,h/4)){using(var g=System.Drawing.Graphics.FromImage(small))g.DrawImage(full,0,0,w/4,h/4);small.Save(file);}}
  }
  public string Speed(){return "frame="+FrameMilliseconds.ToString("0.0")+"ms fps="+FramesPerSecond.ToString("0.0")+VideoInfo+(Problem!=null?" problem="+Problem:"");}
  public void Dispose(){Stop();}
 }
}
