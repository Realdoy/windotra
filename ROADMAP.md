# WinDotra roadmap

Everything open source, everything customizable.

## Publish (do this first)
1. Install GitHub CLI: `winget install GitHub.cli`, then `gh auth login`.
2. In this folder: `gh repo create windotra --public --source . --push`
3. On github.com → the repo → Settings → Pages → Source: "Deploy from a branch", branch `main`, folder `/docs`. The site goes live at `https://<you>.github.io/windotra/`.
4. In `src/Studio.Store.cs`, set `StoreUrl` to `https://<you>.github.io/windotra/store/`, and in `docs/index.html` replace `windotra/windotra` with `<you>/windotra`. Rebuild with `build.ps1`.
5. Make a release: zip `dist` (or use `WinDotra-v0.14-portable.zip`) and run `gh release create v0.14 WinDotra-v0.14-portable.zip`.

## Next big update (v0.15)
- **Taskbar:** fully hide it when the Dock is on (not just auto-hide), and add a Dock option to show it on the second screen. Fix the island turning grey/see-through when a light page is behind it (use a solid color).
- **Widgets, much more customizable:** per-line font and color pickers, images and icons in widgets, progress bars and rings ({battery} or {cpu} as a ring), buttons that open apps or sites, background picture or gradient, glass on/off per widget, any size (free resize), a grid/list layout, and a visual editor (drag rows instead of typing).
- **Widget types:** weather (open-meteo, no key), calendar events (.ics link), RSS/news, stocks/crypto (public APIs), photo frame, quote of the day, to-do lists, notes with markdown.
- **Icons:** an icon-pack system (`docs/store/icons/<pack>/<app>.png` plus an `index.json`). Ship one open-licensed pack (for example Papirus/Fluent icons, which are freely licensed) and let the Store install packs. Apply packs to the Dock automatically by app name.
- **Store:** an online "Submit" button that opens a prefilled GitHub pull request, ratings via GitHub reactions, preview images per item, search, and categories (styles, widgets, icon packs, wallpapers, cursors).
- **Wallpapers in the Store:** shareable scenes (scene + colors) and links to openly licensed photos and videos.
- **Everything themeable:** island, menu bar, Dock, Control Center, and the app itself read colors, radius, blur, and font from the style file.
- **Real frosted glass:** non-layered windows with the Windows 11 acrylic backdrop for the menu bar, Control Center, and Dock.
- **Plugins (later):** sandboxed widget scripts, with a clear permission prompt.
- **Quality:** an automatic update check, crash log with a "copy details" button, settings search, keyboard shortcuts editor, translations (English and German first).
