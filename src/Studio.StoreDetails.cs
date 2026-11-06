using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WinCanvas {
 public partial class Studio {
  // Pictures for store items: a real-size picture WinDotra draws for you, plus up to 3 screenshots of your own.
  static readonly System.Text.RegularExpressions.Regex ImagePath=new System.Text.RegularExpressions.Regex("^previews/[a-z0-9-]{1,80}\\.(png|jpg)$");
  public static void CheckImages(List<string> images){if(images==null)return;if(images.Count>4||images.Any(i=>i==null||!ImagePath.IsMatch(i)))throw new InvalidDataException("An item lists invalid pictures.");}
  // Draws an element to PNG bytes at the given scale (2 = sharp on high-DPI screens).
  static byte[] Png(FrameworkElement element,double scale){
   var scaled=new Border{Child=element,LayoutTransform=new ScaleTransform(scale,scale)};var host=new Border{Child=scaled,Background=Brushes.Transparent};
   host.Measure(new Size(double.PositiveInfinity,double.PositiveInfinity));host.Arrange(new Rect(host.DesiredSize));host.UpdateLayout();
   var bitmap=new RenderTargetBitmap((int)Math.Ceiling(host.DesiredSize.Width),(int)Math.Ceiling(host.DesiredSize.Height),96,96,PixelFormats.Pbgra32);bitmap.Render(host);
   var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var stream=new MemoryStream()){encoder.Save(stream);return stream.ToArray();}
  }
  // A widget exactly as it looks on the desktop (same size, colors, and corners).
  FrameworkElement RealWidget(CustomWidget w){
   double unit=Math.Round(Profile.WidgetWidth*0.62),width=(w.Width==2?unit*2+12:unit)-32,height=unit-30;
   var pal=new Palette(w.Background.Length==7?w.Background:w.Background.Length==9?"#"+w.Background.Substring(3):Profile.Background,Profile.Accent);
   var live=new Func<string,string>(k=>k=="cpu"?"23%":k=="ram"?"48%":k=="battery"?"82% battery":k=="song"?"Glass Horizon":k=="artist"?"Sample artist":null);
   var inner=OverlayManager.RenderCustom(w,pal,live,null,width,height);
   return new Border{Margin=new Thickness(24),Opacity=w.Opacity,Background=Brush(pal.Back),CornerRadius=new CornerRadius(w.Radius),Padding=new Thickness(16),Child=inner,Effect=new System.Windows.Media.Effects.DropShadowEffect{BlurRadius=24,ShadowDepth=4,Opacity=0.35}};
  }
  // Asks for optional screenshots and returns (store path, bytes) pairs, the drawn preview first.
  List<KeyValuePair<string,byte[]>> PreviewPictures(string slug,FrameworkElement drawn){
   var list=new List<KeyValuePair<string,byte[]>>{new KeyValuePair<string,byte[]>("previews/"+slug+"-1.png",Png(drawn,2))};
   if(MessageBox.Show(this,"WinDotra made a real-size picture of it for the store.\n\nAdd up to 3 screenshots of your own too (for example of your desktop)?",Brand.Name,MessageBoxButton.YesNo,MessageBoxImage.Question)==MessageBoxResult.Yes){
    var dialog=new Microsoft.Win32.OpenFileDialog{Title="Choose up to 3 screenshots",Filter="Pictures|*.png;*.jpg;*.jpeg",Multiselect=true};
    if(dialog.ShowDialog(this)==true)foreach(var file in dialog.FileNames.Take(3)){if(new FileInfo(file).Length>1024*1024)throw new InvalidOperationException(Path.GetFileName(file)+" is larger than 1 MB. Save it smaller (or as JPG) and try again.");string ext=Path.GetExtension(file).ToLowerInvariant()==".png"?".png":".jpg";list.Add(new KeyValuePair<string,byte[]>("previews/"+slug+"-"+(list.Count+1)+ext,File.ReadAllBytes(file)));}
   }
   return list;
  }
  static string Slug(string name){var s=System.Text.RegularExpressions.Regex.Replace(name.ToLowerInvariant(),"[^a-z0-9]+","-").Trim('-');return s.Length==0?"item":s.Length>60?s.Substring(0,60):s;}
  // Pictures for an item: from the copy inside WinDotra, or from the website.
  static ImageSource Picture(StoreItem item,string path){try{var image=new BitmapImage();image.BeginInit();image.UriSource=item.Base.StartsWith("http")?new Uri(item.Base+path):new Uri(Path.Combine(item.Base,path.Replace('/','\\')));image.CacheOption=BitmapCacheOption.OnLoad;image.EndInit();return image;}catch(Exception){return null;}}
  // The details window: every picture at real size, the full description, the author, and Get / Report.
  void ShowDetails(StoreItem item,FrameworkElement drawn){
   var body2=new StackPanel{Margin=new Thickness(28,24,28,24)};
   body2.Children.Add(new TextBlock{Text=(item.Type=="widget"?"WIDGET":"STYLE"),FontSize=11,Foreground=Brush(Ui.Faint)});
   body2.Children.Add(new TextBlock{Text=item.Name,FontSize=26,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,2,0,4)});
   var by=new TextBlock{Text="by "+(item.Author.Length>0?item.Author:"someone"),FontSize=13,Foreground=Brush(item.AuthorUrl.Length>0?"#64B5FF":Ui.Soft),Margin=new Thickness(0,0,0,12)};if(item.AuthorUrl.StartsWith("https://github.com/")){string profile=item.AuthorUrl;by.Cursor=System.Windows.Input.Cursors.Hand;by.MouseLeftButtonUp+=(s,e)=>System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(profile){UseShellExecute=true});}body2.Children.Add(by);
   body2.Children.Add(new TextBlock{Text=item.Description,FontSize=14,Foreground=Brush(Ui.Soft),TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,16)});
   var pictures=new WrapPanel();
   if(item.Images!=null&&item.Images.Count>0)foreach(var path in item.Images){var src=Picture(item,path);if(src!=null)pictures.Children.Add(new Border{CornerRadius=new CornerRadius(12),Background=Brush("#111113"),Margin=new Thickness(0,0,12,12),Padding=new Thickness(8),Child=new Image{Source=src,Stretch=Stretch.None,MaxWidth=860,MaxHeight=520}});}
   else if(drawn!=null)pictures.Children.Add(new Border{CornerRadius=new CornerRadius(12),Background=Brush("#111113"),Padding=new Thickness(8),Child=drawn});
   body2.Children.Add(pictures);
   var row=new WrapPanel{Margin=new Thickness(0,8,0,0)};row.Children.Add(Primary("Get",()=>Install(item)));var report=Ghost("Report",()=>System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(ReportUrl(item.Type,item.Name,item.File)){UseShellExecute=true}));report.Foreground=Brush("#FF6961");row.Children.Add(report);body2.Children.Add(row);
   var window=new Window{Title=item.Name+" · WinDotra Store",Width=920,Height=680,WindowStartupLocation=WindowStartupLocation.CenterOwner,Owner=this,Background=Brush(Ui.Paper),Foreground=Brush(Ui.Ink),FontFamily=Ui.SansFamily,Resources=Resources,Content=new ScrollViewer{Content=body2,VerticalScrollBarVisibility=ScrollBarVisibility.Auto}};
   window.Show();
  }
 }
}
