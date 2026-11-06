# Contributing

Build with `./build.ps1` on Windows. Run `dist/WinCanvas.exe --self-test` and inspect `dist/test-results.txt`. Use `--studio-smoke docs/studio-preview.png` for all Studio pages and overlays; use `--smoke-test docs/preview.png` for the appearance editor. These do not apply Windows settings. Studio smoke uses temporary storage.

Test real Windows changes in a disposable Windows account or VM. Keep settings scoped to the current user, explicitly applied, and reversible. No telemetry, forced Explorer restarts, or administrator requirements.

## Module layout

- `AiClient.cs`: provider transport, request validation, encrypted storage, and chat sessions.
- `IslandChat.cs`, `Studio.Ai.cs`: chat controls and provider setup.
- `GlobalShortcut.cs`: registered keyboard shortcut and cleanup.
- `AiTests.cs`: simulated HTTP streams and actual chat-control smoke tests.
- `Studio.cs`: navigation, configuration, profiles, and tray lifecycle.
- `Overlays.cs`: floating widgets, island, system statistics, focus clock.
- `Customization.cs`: profile validation, atomic JSON storage, native fonts/cursors, and recovery service.
- `StudioTests.cs`: fake native backend and module tests.
- `Program.cs`, `MainWindow.xaml`, `Settings.cs`, `Tests.cs`: entry point and appearance editor/recovery.

Add built-in modules in source; profiles remain declarative data. Do not turn imported JSON into code or arbitrary command launchers. Bound inputs, keep recovery durable, and scope restores to managed settings.

Issues should include Windows version, steps, and expected/actual results. Check personal paths before sharing recovery files. Pull requests should explain the change and its validation.

AI replies must remain non-executable text. Never forward credentials on redirects, include keys in profile exports, or automatically collect screen/clipboard contents. Keep protocol tests offline, and document live tests separately.
