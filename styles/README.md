# WinDotra styles

A style is a complete WinDotra look in one small file: the live wallpaper, widgets and where they sit, the island, the macOS-style menu bar, and the Dock. Every `.windotra-style` file in this folder ships with WinDotra and appears in **Styles** in the app.

## Make one

1. In WinDotra, set things up in **Widgets**, **Island**, **Live wallpaper**, and **Typography**.
2. Open **Styles → Make your own**, give it a name, your name (optional), and a one-line description.
3. Click **Export to share…**.

## Share it

Add your `.windotra-style` file to this folder and open a pull request. Keep the file name short and lowercase, like `ocean-glass.windotra-style`. Please include a screenshot in the pull request.

## What a style can change

Styles are plain JSON and can only set these fields: `Font`, `Accent`, `Background`, `FontSize`, `Opacity`, `Radius`, `Glass`, `ClockAnalog`, `Clock24`, `Scene` (`Hills`, `Drift`, `Gradient`, `Aurora`, `Lines`, `Dots`, `Stars`, `Clock`), `SceneColor1`–`3`, `SceneBackground`, `SceneSpeed`, `SceneDensity`, `SceneDark`, `LiveWallpaper`, `WallpaperFit`, `Island`, `IslandShape` (`Pill`, `Notch`, `Rounded`), `IslandColor`, `IslandIdle`, `IslandScale`, `IslandMedia`, `IslandDownloads`, `IslandAura`, `IslandHover`, the widget switches (`Clock`, `Stats`, `Note`, `Focus`, `Calendar`, `Music`, `Reminders`, `Day`), `WidgetWidth`, `MenuBar`, `MenuBarLight`, `Dock`, `DockLight`, `DockMagnify`, `DockSize`, and `Layout` (`keep`, `macos`, `windows`, `column`, `clear`), which arranges the widgets.

WinDotra rejects any file that tries to set anything else, so a style can never touch notes, tasks, your Dock apps, files, AI settings, or Windows itself. Colors use `#RRGGBB`; fonts must be installed or bundled (Inter is bundled).

```json
{
  "Format": "windotra-style",
  "Version": 1,
  "Name": "Apple",
  "Author": "WinDotra",
  "Description": "Graphite tiles, Apple blue, Inter, analog clock, calm Drift wallpaper.",
  "Settings": { "Font": "Inter", "Background": "#1C1C1E", "Accent": "#0A84FF", "Radius": 24 }
}
```
