using System;
using System.Windows.Media;

namespace WinCanvas {
 // The minimal palette: warm paper, ink, hairlines, and one indigo accent.
 public static class Ui {
  // Dark, after macOS System Settings: graphite window, raised grouped panels, Apple blue.
  public const string Paper="#1C1C1E",Surface="#2C2C2E",Line="#3A3A3C",Hover="#3A3A3C",Ink="#F5F5F7",Soft="#AEAEB2",Faint="#8E8E93",Accent="#0A84FF",AccentSoft="#1F0A84FF",Sidebar="#232325";
  public const string Font="Segoe UI Variable Text, Segoe UI",Display="Segoe UI Variable Display, Segoe UI",Icons="Segoe Fluent Icons, Segoe MDL2 Assets";
  // Inter, bundled in the fonts folder beside WinDotra.exe, is the closest open font to Apple's San Francisco.
  public static readonly FontFamily SansFamily=new FontFamily(new System.Uri(System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory,"fonts")+"\\"),"./#Inter, Segoe UI Variable Text, Segoe UI");
  public static SolidColorBrush Brush(string value) { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(value)); }
  public static bool IsLight(string hex) { var c=(Color)ColorConverter.ConvertFromString(hex); return (0.2126*c.R+0.7152*c.G+0.0722*c.B)/255>0.55; }
 }
 // Text and control colors that stay readable on whatever widget background the user picked.
 public class Palette {
  public readonly string Ink,Soft,Field,Chip,Line,OnAccent,Back;
  public Palette(string background,string accent) {
   bool light=Ui.IsLight(background);Back=background;
   Ink=light?"#1C1917":"#F4F4F5";Soft=light?"#78716C":"#A1A1AA";Field=light?"#F2F2F0":"#26262B";Chip=light?"#ECECEA":"#2E2E33";Line=light?"#14000000":"#1FFFFFFF";
   OnAccent=Ui.IsLight(accent)?"#1C1917":"#FFFFFF";
  }
 }
}
