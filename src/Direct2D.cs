using System;
using System.Runtime.InteropServices;

namespace WinCanvas {
 // Minimal Direct2D interop for the live wallpaper. Only the methods WinCanvas calls are typed; the rest keep their
 // vtable slots as placeholders. Direct2D draws on the GPU into a GDI device context, which is what shows up in the
 // Windows 11 24H2+ wallpaper layer.
 [StructLayout(LayoutKind.Sequential)] public struct D2Color { public float R,G,B,A; public D2Color(float r,float g,float b,float a){R=r;G=g;B=b;A=a;} }
 [StructLayout(LayoutKind.Sequential)] public struct D2Point { public float X,Y; public D2Point(float x,float y){X=x;Y=y;} }
 [StructLayout(LayoutKind.Sequential)] public struct D2Rect { public float Left,Top,Right,Bottom; public D2Rect(float l,float t,float r,float b){Left=l;Top=t;Right=r;Bottom=b;} }
 [StructLayout(LayoutKind.Sequential)] public struct D2Ellipse { public D2Point Center; public float RadiusX,RadiusY; }
 [StructLayout(LayoutKind.Sequential)] public struct D2Matrix { public float M11,M12,M21,M22,DX,DY; public static D2Matrix Place(float sx,float sy,float dx,float dy){return new D2Matrix{M11=sx,M22=sy,DX=dx,DY=dy};} }
 [StructLayout(LayoutKind.Sequential)] public struct D2Stop { public float Position; public D2Color Color; }
 [StructLayout(LayoutKind.Sequential)] struct D2PixelFormat { public int Format,Alpha; }
 [StructLayout(LayoutKind.Sequential)] struct D2TargetProperties { public int Type; public D2PixelFormat Pixel; public float DpiX,DpiY; public int Usage,MinLevel; }
 [StructLayout(LayoutKind.Sequential)] struct D2Radial { public D2Point Center,Offset; public float RadiusX,RadiusY; }
 [StructLayout(LayoutKind.Sequential)] struct D2Linear { public D2Point Start,End; }
 [StructLayout(LayoutKind.Sequential)] public struct GdiRect { public int Left,Top,Right,Bottom; }

 [ComImport,Guid("06152247-6f50-465a-9245-118bfd3b6007"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
 interface ID2D1Factory {
  void ReloadSystemMetrics(); void GetDesktopDpi(); void CreateRectangleGeometry(); void CreateRoundedRectangleGeometry(); void CreateEllipseGeometry(); void CreateGeometryGroup(); void CreateTransformedGeometry(); void CreatePathGeometry(); void CreateStrokeStyle(); void CreateDrawingStateBlock();
  void CreateWicBitmapRenderTarget(); void CreateHwndRenderTarget(); void CreateDxgiSurfaceRenderTarget();
  [PreserveSig] int CreateDCRenderTarget(ref D2TargetProperties properties,out ID2D1DCRenderTarget target);
 }
 [ComImport,Guid("1c51bc64-de61-46fd-9899-63a5d8f03950"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
 interface ID2D1DCRenderTarget {
  void GetFactory(); void CreateBitmap(); void CreateBitmapFromWicBitmap(); void CreateSharedBitmap(); void CreateBitmapBrush();
  [PreserveSig] int CreateSolidColorBrush(ref D2Color color,IntPtr properties,out IntPtr brush);
  [PreserveSig] int CreateGradientStopCollection(IntPtr stops,int count,int gamma,int extend,out IntPtr collection);
  [PreserveSig] int CreateLinearGradientBrush(ref D2Linear properties,IntPtr brushProperties,IntPtr collection,out IntPtr brush);
  [PreserveSig] int CreateRadialGradientBrush(ref D2Radial properties,IntPtr brushProperties,IntPtr collection,out IntPtr brush);
  void CreateCompatibleRenderTarget(); void CreateLayer(); void CreateMesh();
  [PreserveSig] void DrawLine(D2Point start,D2Point end,IntPtr brush,float width,IntPtr style);
  void DrawRectangle();
  [PreserveSig] void FillRectangle(ref D2Rect rect,IntPtr brush);
  void DrawRoundedRectangle(); void FillRoundedRectangle(); void DrawEllipse();
  [PreserveSig] void FillEllipse(ref D2Ellipse ellipse,IntPtr brush);
  void DrawGeometry(); void FillGeometry(); void FillMesh(); void FillOpacityMask(); void DrawBitmap(); void DrawText(); void DrawTextLayout(); void DrawGlyphRun(); void SetTransform(); void GetTransform();
  [PreserveSig] void SetAntialiasMode(int mode);
  void GetAntialiasMode(); void SetTextAntialiasMode(); void GetTextAntialiasMode(); void SetTextRenderingParams(); void GetTextRenderingParams(); void SetTags(); void GetTags(); void PushLayer(); void PopLayer(); void Flush(); void SaveDrawingState(); void RestoreDrawingState(); void PushAxisAlignedClip(); void PopAxisAlignedClip();
  [PreserveSig] void Clear(ref D2Color color);
  [PreserveSig] void BeginDraw();
  [PreserveSig] int EndDraw(out ulong tag1,out ulong tag2);
  void GetPixelFormat(); void SetDpi(); void GetDpi(); void GetSize(); void GetPixelSize(); void GetMaximumBitmapSize(); void IsSupported();
  [PreserveSig] int BindDC(IntPtr dc,ref GdiRect rect);
 }
 // ID2D1Brush methods, shared by every brush: GetFactory, SetOpacity, SetTransform, ...
 [ComImport,Guid("2cd906a8-12e2-11dc-9fed-001143a055f9"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
 interface ID2D1Brush { void GetFactory(); [PreserveSig] void SetOpacity(float opacity); [PreserveSig] void SetTransform(ref D2Matrix matrix); }
 [ComImport,Guid("2cd906a9-12e2-11dc-9fed-001143a055f9"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
 interface ID2D1SolidColorBrush { void GetFactory(); [PreserveSig] void SetOpacity(float opacity); [PreserveSig] void SetTransform(ref D2Matrix matrix); void GetOpacity(); void GetTransform(); [PreserveSig] void SetColor(ref D2Color color); }

 public class D2Brush { internal IntPtr Pointer; internal ID2D1Brush Base; internal ID2D1SolidColorBrush Solid;
  // Gradients are defined on a unit square/circle; Place() moves and scales them for each draw.
  public void Place(float sx,float sy,float dx,float dy){var m=D2Matrix.Place(sx,sy,dx,dy);Base.SetTransform(ref m);}
  public void Recolor(D2Color color){Solid.SetColor(ref color);}
  public void Transform(float m11,float m12,float m21,float m22,float dx,float dy){var m=new D2Matrix{M11=m11,M12=m12,M21=m21,M22=m22,DX=dx,DY=dy};Base.SetTransform(ref m);}
  public void Release(){if(Solid!=null)Marshal.ReleaseComObject(Solid);if(Base!=null)Marshal.ReleaseComObject(Base);Marshal.Release(Pointer);}
 }
 public class Direct2D : IDisposable {
  [DllImport("d2d1.dll")] static extern int D2D1CreateFactory(int type,ref Guid iid,IntPtr options,out ID2D1Factory factory);
  readonly ID2D1Factory factory; readonly ID2D1DCRenderTarget target; readonly System.Collections.Generic.List<D2Brush> brushes=new System.Collections.Generic.List<D2Brush>();
  public Direct2D() {
   var iid=typeof(ID2D1Factory).GUID;Check(D2D1CreateFactory(0,ref iid,IntPtr.Zero,out factory));
   // Pixels, not DIPs (96 DPI), B8G8R8A8 with alpha ignored (the wallpaper is opaque).
   var properties=new D2TargetProperties{Type=0,Pixel=new D2PixelFormat{Format=87,Alpha=3},DpiX=96,DpiY=96};Check(factory.CreateDCRenderTarget(ref properties,out target));
  }
  static void Check(int hr){if(hr<0)Marshal.ThrowExceptionForHR(hr);}
  D2Brush Wrap(IntPtr pointer,bool solid){var o=Marshal.GetObjectForIUnknown(pointer);var b=new D2Brush{Pointer=pointer,Base=(ID2D1Brush)o,Solid=solid?(ID2D1SolidColorBrush)o:null};brushes.Add(b);return b;}
  public void Bind(IntPtr dc,int width,int height){var rect=new GdiRect{Right=width,Bottom=height};Check(target.BindDC(dc,ref rect));}
  public void Begin(D2Color background){target.BeginDraw();target.Clear(ref background);}
  public void End(){ulong a,b;target.EndDraw(out a,out b);}
  public D2Brush Solid(D2Color color){IntPtr brush;Check(target.CreateSolidColorBrush(ref color,IntPtr.Zero,out brush));return Wrap(brush,true);}
  // The stops go in as a pinned C array. (Declared as D2Stop[], COM interop would send a SAFEARRAY and every gradient came out black.)
  IntPtr Stops(D2Stop[] stops){IntPtr collection;var pin=GCHandle.Alloc(stops,GCHandleType.Pinned);try{Check(target.CreateGradientStopCollection(pin.AddrOfPinnedObject(),stops.Length,0,0,out collection));}finally{pin.Free();}return collection;}
  public D2Brush Radial(D2Stop[] stops){var collection=Stops(stops);var p=new D2Radial{RadiusX=1,RadiusY=1};IntPtr brush;try{Check(target.CreateRadialGradientBrush(ref p,IntPtr.Zero,collection,out brush));}finally{Marshal.Release(collection);}return Wrap(brush,false);}
  public D2Brush Linear(D2Stop[] stops,D2Point start,D2Point end){var collection=Stops(stops);var p=new D2Linear{Start=start,End=end};IntPtr brush;try{Check(target.CreateLinearGradientBrush(ref p,IntPtr.Zero,collection,out brush));}finally{Marshal.Release(collection);}return Wrap(brush,false);}
  public void Ellipse(float x,float y,float rx,float ry,D2Brush brush){var e=new D2Ellipse{Center=new D2Point(x,y),RadiusX=rx,RadiusY=ry};target.FillEllipse(ref e,brush.Pointer);}
  public void Rectangle(float l,float t,float r,float b,D2Brush brush){var rect=new D2Rect(l,t,r,b);target.FillRectangle(ref rect,brush.Pointer);}
  public void Line(float x1,float y1,float x2,float y2,D2Brush brush,float width){target.DrawLine(new D2Point(x1,y1),new D2Point(x2,y2),brush.Pointer,width,IntPtr.Zero);}
  public static D2Color Color(System.Windows.Media.Color c,float alpha){return new D2Color(c.R/255f,c.G/255f,c.B/255f,alpha);}
  public void Dispose(){foreach(var b in brushes)b.Release();brushes.Clear();Marshal.ReleaseComObject(target);Marshal.ReleaseComObject(factory);}
 }
}
