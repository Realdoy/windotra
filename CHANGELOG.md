# Changelog

## 0.16 — Visual widget maker and one-click publishing

- Widget maker: rows you click instead of codes. Text rows with sizes (Huge to Small), bold, alignment, 11 colors, and a + Live value menu; progress bars for CPU, memory, battery, or how far through the day/week/month/year; empty space; any background color; accent color; reorder and remove rows; live preview.
- Publish to Store: one click on a widget (Store) or a style (Styles). With the GitHub app signed in, WinDotra forks, uploads, and opens the pull request for you; otherwise GitHub opens with the file filled in.
- The store catalog (docs/store/index.json) now rebuilds itself on GitHub, so sharing only needs the file.

## 0.15 — Fixes and quick switches

- Fixed: your own and downloaded widgets didn't appear on the desktop.
- Quick switches at the top of every page: Desktop, Live wallpaper, Menu bar, Dock, Island, and Turn everything off.
- The live wallpaper is off unless you turn it on; styles no longer switch it on.
- Island: a volume slider, and a + button to add apps to quick launch (right-click one to remove it).
- Update check: WinDotra tells you when a new version is on GitHub.

## 0.14 â€” Store, custom widgets, and a website

- Store: download styles and widgets made by the community (plain JSON, never programs). Works offline with the catalog that ships inside WinDotra; adds the online catalog from the project website when reachable. Replaces the Profiles page.
- Custom widgets: make your own in the Store with a few lines of text and live values ({time}, {date}, {greeting}, {cpu}, {ram}, {battery}, {song}, {artist}, {days-until:â€¦}, {countdown:â€¦}), pick a size and color, and export them to share. They behave like every other widget in Edit mode.
- Website for GitHub Pages in docs/: features with preview images, the store catalog, and how to share your own.
- Control Center: Dark Mode now switches between the Apple and Apple Dark styles (and Windows' own dark mode).
- macOS cursors: apple_cursor by ful1e5 (GPL-3.0), black for light mode and white for dark mode. The Apple styles pick the right one.
- Edit mode grid is drawn once with GDI, so dragging widgets is smooth again.
- Softer widgets (larger corners, gentler shadows, thinner edges), more translucent menu bar and Dock, slightly smaller interface text.
- After the panic button, Home offers "Bring back my WinDotra desktop".

## 0.13 â€” A menu bar that works, Control Center, and a panic button

- Menu bar: sits at the very top, shows the app in front, and its menus now work. Apps with classic menus get their real menus; everything else gets File, Edit, View, Window, Go, and Help that send the usual shortcuts. Start, search, and other shell pieces no longer replace the app name.
- Control Center (like macOS): Wi-Fi with your network name, Bluetooth, screen mirroring, now playing, Focus, dark mode, display brightness (laptop screens), a volume slider, and shortcuts.
- Full screen: the menu bar, Dock, and island hide while a video, game, or any full-screen app is in front.
- Apple style goes all the way: macOS cursors (a public-domain set by Lihpe25, now bundled), Inter for title bars and menus, the Windows taskbar hidden while the Dock is on, and the Dock in macOS icon shape.
- Panic button: "Back to normal Windows" on Home, in the tray menu, in the menu bar's logo menu, and with Ctrl+Alt+Shift+W. It restores the taskbar, cursors, fonts, and wallpaper, turns WinDotra's desktop off, and saves your setup as a profile.
- Dock: macOS icon shape option and your own icon per app (right-click â†’ Change iconâ€¦).
- Live wallpaper: a map of your screens as they're arranged, each showing its wallpaper; click one to give it a picture, video, or scene.
- Fixed: the notch island landed in the top-left corner; a kink at the left edge of the Lines scene.

## 0.12 â€” Menu bar, Dock, and real multi-monitor wallpapers

- Live wallpaper: every screen gets its own picture (no more stretching across monitors of different shapes), or one picture can span them all. Fit modes: Fill, Fit, Stretch, Center. Each screen can have its own picture, video, or scene.
- Video wallpapers: any MP4, MOV, WMV, or MKV Windows can play, looping and muted, decoded with Media Foundation.
- Fixed: gradients in the GPU wallpaper rendered black (Drift, Gradient, Aurora). The wallpaper now also reattaches itself after Explorer restarts or screens change.
- New Hills scene, like macOS Sonoma.
- macOS-style menu bar (app in front, Window, Go, Help, status icons, clock, Sleep/Restart/Shut Down/Lock) and Dock (your apps, running dots, magnification, auto-hide, Keep in Dock, Quit). Optional, on the new Menu bar & Dock page.
- Island design: Pill, Notch, or Rounded shape, colors, size, and what it shows when idle.
- ChatGPT right in the island through the Codex command-line tool (your ChatGPT account, no API key), with a Claude / ChatGPT switch in the island.
- Styles are now whole desktops: wallpaper, widget layout, island, menu bar, and Dock. New Apple and Apple Dark styles.
- Profiles: Save writes a file on this PC and the page lists your saved desktops (Use, Update, Show, Delete to the Recycle Bin).
- Mac-style window with traffic-light buttons and colored icon tiles; menus without the white icon column.
- Removed the Lock positions switch: widgets only move in Edit mode.
- Use the photo as the normal Windows wallpaper too, with one-click restore.
## 0.11 â€” Edit mode and Styles

- Edit mode: widgets and the island only move, resize, recolor, or get removed while editing (right-click a widget or the island, the tray, or the Widgets page). Per-widget size and color, corner resize handle, optional grid with snapping, floating Editing toolbar with Add widget.
- Styles: Apple, Apple Light, Windows 11, Midnight Neon, Paper, and Rainmeter presets; save, export, import, and share .windotra-style files (look settings only; anything else is rejected). Community styles live in the styles folder.
- Your photo as the live background, still or with a slow Ken Burns drift.
- Premium island media view: larger art, Inter type, thicker progress bar, solid vector transport icons, sound output button. The island is always on top.
- Inter (open-source, San Francisco-like) bundled as the default font.
- Focus length presets in the island, timer widget menu, and settings.
- Fixed: island width setting refused as out of range; WinDotra menus no longer use the Windows menu font; icon fonts can't be applied to Windows, with a one-click fix banner; Reminders count alignment.

## 0.10 â€” Polish

- New dark look for the app after macOS System Settings: graphite window, blue selection, grouped rounded panels, blue switches; the wallpaper editor matches.
- Widgets are drawn as smooth rounded tiles with a soft shadow (no more black boxes from the OS blur effect, which fails on recent Windows builds). Glass = translucent tiles.
- Clock face with numerals and an orange second hand, red Calendar date, AC indicator on desktop PCs, music widget tinted by the album color.
- The island is always solid, regardless of widget opacity.
- Fonts always fall back to a sans-serif instead of a serif default.
- AI screen sharing: messages about your screen share it automatically; optional 'always share'; a labelled Screen button. Verified end-to-end with the Claude command-line tool.

## 0.9.1

- Typography: new **Use this font in Windows** changes the font of most Windows apps (File Explorer, Notepad, dialogs, most desktop apps) by substituting Segoe UI. Needs administrator approval and a sign-out; a backup is saved first and **Restore the Windows font** puts every value back. Windows 11 Start, Settings, and the taskbar keep Segoe UI Variable.
- The old option is now clearly labelled **Title bars and menus only** (Windows 11 uses those fonts in few places).
- Self-test covers the font swap round trip against a throwaway registry key.

## 0.9 Ã¢â‚¬â€ WinDotra

- Renamed to WinDotra ("Make Windows yours.") with a new logo, app icon, and tray icon; the build turns `branding/logo.png` (or the vector rendition) into a multi-size .ico. Existing settings and the sign-in entry carry over.
- First-run setup with Claude app, ChatGPT app, and Claude command-line detection.
- Claude app / ChatGPT app hand-off and opt-in, per-message screen sharing for every AI route.
- Live wallpaper rewritten: Direct2D (GPU) rendering on a background thread, 7 scenes, colors/speed/density, 15/30/60 fps and detail presets, live fps readout.
- New Island page: one-click and right-click centering, dock toggles, custom quick-launch apps/folders/sites with real icons.
- macOS-style widget redesign, plus Now Playing and Reminders widgets.
- Font picker with previews, per-user font install, and an 18-font free pack.
- Cursor schemes, live previews, and cursor-pack import (.zip/folder/install.inf).
- Fixed: launcher icons broke saving the profile (caught by the self-test before release).

## 0.8 Ã¢â‚¬â€ Always there

- Live wallpaper fixed on Windows 11 24H2 and later (scene now lives inside Progman below the icons, painted through GDI). Verified on build 26300.
- AI can act on the PC through a fixed allow-list (open apps/sites/folders, search, music, volume, lock, focus timer, tasks, settings pages). Every action is a card you must Run; there is no command or script action.
- Claude sign-in problems are detected (no more errors shown as answers); Auto falls back to the browser; a Sign in to Claude button opens Claude for /login.
- Island: Apple-sized, shrinks as soon as the pointer leaves or you click elsewhere; compact chat with Enter to send.
- Runs in the background: closing the window keeps everything running in the tray; reopening shows the window; desktop launches automatically; optional start at sign-in.
- New Rainmeter-style F R I D A Y date widget, and macOS-style frosted glass widgets.

## 0.7 Ã¢â‚¬â€ Apple-style island

- Island redesigned after Apple's Dynamic Island: pure black, compact leading/trailing items, springy open/close morph from the center.
- Expanded media: art, title, artist, album-tinted waveform, elapsed and remaining time, plain white transport controls.
- Round icon dock for Ask AI, Focus, Tasks, and System; orange focus timer and blue downloads as live activities.
- Rainbow glow is now an opt-in "aura" (off by default).
- Auto AI falls back to the browser when Claude isn't signed in; screenshots render at true size.

## 0.6 Ã¢â‚¬â€ Island

- Black Dynamic Island pill with now-playing media from Windows media controls: title, artist, album art, seek bar, previous/play/next.
- Live waveform driven by the default speakers' peak level.
- Browser download progress from the Downloads folder, with a finished state and Open button.
- Animated rainbow glow while playing or downloading; switches for media, downloads, and glow.
- Open island stays compact; AI, Focus, Tasks, and System open as tabs.
- `--media-check` diagnostic; fixed a crash when Ollama detection finished without a UI context.

## 0.5 Ã¢â‚¬â€ Calm

- Minimal redesign of the app, wallpaper editor, widgets, and island: paper, ink, hairlines, one accent; light and dark widget backgrounds both stay readable.
- AI without API keys: Auto, free local Ollama (with detection and one-click model downloads), Claude subscription via the Claude command-line tool, or browser hand-off to claude.ai/ChatGPT. Key fields removed; older cloud settings migrate to Auto.
- Live wallpaper with Drift, Lines, Dots, and Clock scenes, light/dark, full-screen pause, start-with-desktop, and `--wallpaper-check`.
- New Home page, eight minimal themes, single-column pages, Ctrl+1Ã¢â‚¬â€œ7 shortcuts.
- Island: click the top line to open, drag it to move; tasks strike through when done.

## 0.4 Ã¢â‚¬â€ Polish

- New design system shared by Studio, the appearance editor, and every overlay: toggle switches, slim sliders, dark dropdowns, inputs, scrollbars, tooltips, and hover/pressed states.
- Redesigned Studio: icon sidebar with active page, live desktop status, greeting dashboard with stat cards, and clickable module tiles.
- Live preview on Widgets & island that updates as you change colors, opacity, corners, sizes, and fonts.
- Eight one-click widget themes (Lavender, Mint, Sunset, Ocean, Rose, Lemon, Mono, Paper).
- New Calendar widget (month view, today highlighted, follows your locale's first weekday).
- System pulse and island now show CPU/memory/power meters that turn amber/red under load or low battery.
- Focus timer progress bar; island tabs show which one is active; more quick launchers (Notepad, Snip, Settings).
- Four new wallpaper moods: Ember, Rose, Aurora, Graphite.
- Typography specimens for widget and classic Windows fonts; cursor set summary; profile draft summary.
- Keyboard shortcuts: Ctrl+1Ã¢â‚¬â€œ6 switch pages, Ctrl+L launches the desktop.
- Dark Windows title bar, drawn tray icon with Ask AI/Launch menu entries, soft widget shadows, and fade-in.

## 0.3 Ã¢â‚¬â€ AI island

- Added streaming Claude, OpenAI-compatible, and Ollama chat inside the island.
- Added DPAPI-encrypted API keys and optional per-provider/model history.
- Added prompt starters, Stop, New chat, Copy reply, and Ctrl+Alt+Space.
- Added a persistent local checklist, expanded-width control, hover toggle, and focus sounds.
- Added protocol, encryption, cancellation, history, and real chat-control tests using simulated providers.
- Kept AI configuration and credentials out of shareable desktop profiles.

## 0.2 Ã¢â‚¬â€ Studio

- Module navigation and tray controls.
- Clock, system monitor, editable notes, focus timer, and expanding island.
- Font, color, size, opacity, position, and visibility controls.
- Classic Windows font customization with separate recovery.
- Custom cursors for 14 roles, previews, validation, and recovery.
- JSON profile import/export with bounded inputs.
- Recovery, profile, native-layout, and timer tests; Studio/overlay smoke testing.

## 0.1 Ã¢â‚¬â€ Appearance

- Wallpaper palettes, app/system themes, transparency, and one-step undo.






