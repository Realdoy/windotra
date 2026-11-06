using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace WinCanvas {
 // Small native touches: a dark caption bar on Windows 10 20H1+/11 and a drawn tray icon.
 public static class Chrome {
  [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr window,int attribute,ref int value,int size);
  [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr icon);
  public static void DarkTitleBar(Window window) {
   window.SourceInitialized+=(s,e)=>{try{int on=1;var handle=new WindowInteropHelper(window).Handle;if(DwmSetWindowAttribute(handle,20,ref on,4)!=0)DwmSetWindowAttribute(handle,19,ref on,4);}catch(Exception){}};
  }
  public static System.Drawing.Icon TrayIcon(string accent) {
   using(var bitmap=new System.Drawing.Bitmap(32,32)) using(var g=System.Drawing.Graphics.FromImage(bitmap)) {
    g.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;g.Clear(System.Drawing.Color.Transparent);
    using(var path=new System.Drawing.Drawing2D.GraphicsPath()) {path.AddArc(1,1,12,12,180,90);path.AddArc(19,1,12,12,270,90);path.AddArc(19,19,12,12,0,90);path.AddArc(1,19,12,12,90,90);path.CloseFigure();using(var fill=new System.Drawing.SolidBrush(System.Drawing.ColorTranslator.FromHtml("#171B25")))g.FillPath(fill,path);}
    using(var brush=new System.Drawing.SolidBrush(System.Drawing.ColorTranslator.FromHtml(accent)))g.FillPolygon(brush,new[]{new System.Drawing.Point(16,6),new System.Drawing.Point(26,16),new System.Drawing.Point(16,26),new System.Drawing.Point(6,16)});
    using(var hole=new System.Drawing.SolidBrush(System.Drawing.ColorTranslator.FromHtml("#171B25")))g.FillPolygon(hole,new[]{new System.Drawing.Point(16,12),new System.Drawing.Point(20,16),new System.Drawing.Point(16,20),new System.Drawing.Point(12,16)});
    IntPtr handle=bitmap.GetHicon();try{return (System.Drawing.Icon)System.Drawing.Icon.FromHandle(handle).Clone();}finally{DestroyIcon(handle);}
   }
  }
 }
 // macOS-style glass for widgets and desktop-level placement for the date widget.
 public static class Glass {
  [StructLayout(LayoutKind.Sequential)] struct Accent { public int State,Flags,Color,Animation; }
  [StructLayout(LayoutKind.Sequential)] struct Data { public int Attribute; public IntPtr Value; public int Size; }
  [DllImport("user32.dll")] static extern int SetWindowCompositionAttribute(IntPtr window,ref Data data);
  [DllImport("gdi32.dll")] static extern IntPtr CreateRoundRectRgn(int left,int top,int right,int bottom,int width,int height);
  [DllImport("user32.dll")] static extern int SetWindowRgn(IntPtr window,IntPtr region,bool redraw);
  [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr window,IntPtr after,int x,int y,int width,int height,uint flags);
  // Blur-behind (accent state 3) is undocumented but stable since Windows 10; failure just leaves the tinted card.
  public static void Blur(Window window) {
   try{var accent=new Accent{State=3,Flags=2,Color=0};int size=Marshal.SizeOf(accent);var buffer=Marshal.AllocHGlobal(size);try{Marshal.StructureToPtr(accent,buffer,false);var data=new Data{Attribute=19,Value=buffer,Size=size};SetWindowCompositionAttribute(new WindowInteropHelper(window).Handle,ref data);}finally{Marshal.FreeHGlobal(buffer);}}catch(Exception){}
  }
  // Clips the window (and so the blur) to the rounded card.
  public static void Clip(Window window,FrameworkElement card,double radius) {
   var source=PresentationSource.FromVisual(window);if(source==null)return;var m=source.CompositionTarget.TransformToDevice;var origin=card.TranslatePoint(new Point(0,0),window);
   int left=(int)Math.Round(origin.X*m.M11),top=(int)Math.Round(origin.Y*m.M22),right=(int)Math.Round((origin.X+card.ActualWidth)*m.M11)+1,bottom=(int)Math.Round((origin.Y+card.ActualHeight)*m.M22)+1,round=(int)Math.Round(radius*2*m.M11);
   SetWindowRgn(new WindowInteropHelper(window).Handle,CreateRoundRectRgn(left,top,right,bottom,round,round),true);
  }
  public static void SendToBack(Window window) { SetWindowPos(new WindowInteropHelper(window).Handle,new IntPtr(1),0,0,0,0,0x0001|0x0002|0x0010); }
 }
 public static class Brand {
  public const string Name="WinDotra",Tagline="Make Windows yours.";
  static System.Windows.Media.Imaging.BitmapSource logo;
  // The logo is embedded at build time from branding\logo.png (or the vector rendition).
  public static System.Windows.Media.Imaging.BitmapSource Logo {get{
   if(logo==null){using(var stream=System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("logo.png")){if(stream==null)return null;var image=new System.Windows.Media.Imaging.BitmapImage();image.BeginInit();image.StreamSource=stream;image.DecodePixelWidth=256;image.CacheOption=System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;image.EndInit();image.Freeze();logo=image;}}
   return logo;}}
  public static System.Drawing.Icon TrayIcon() {
   try{using(var stream=System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("logo.png")){if(stream==null)return null;using(var source=new System.Drawing.Bitmap(stream))using(var bitmap=new System.Drawing.Bitmap(32,32)){using(var g=System.Drawing.Graphics.FromImage(bitmap)){g.InterpolationMode=System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;g.DrawImage(source,0,0,32,32);}IntPtr handle=bitmap.GetHicon();try{return (System.Drawing.Icon)System.Drawing.Icon.FromHandle(handle).Clone();}finally{DestroyIcon(handle);}}}}catch(Exception){return null;}
  }
  [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr icon);
 }
}
