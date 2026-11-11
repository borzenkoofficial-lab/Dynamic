# Dynamic — architecture

## Product boundary

Dynamic is a native Windows desktop overlay, not a webview and not a simulated taskbar widget. The first release prioritizes a reliable capsule window and motion system. Feature modules are added only after the window foundation is stable.

## Technology baseline

- C# / .NET 10.
- WinUI 3 on Windows App SDK (stable channel only).
- Win32 HWND interop for non-activating topmost overlay behavior.
- Windows Composition for GPU-backed transforms and animations.
- DWM for supported system composition effects, with a custom-material fallback.
- JSON settings in the user's local application data directory.

WinUI 3 is the native UI layer; HWND remains the authoritative operating-system window. The app must not assume that a XAML Window activation call is harmless: activating the window can change foreground/focus behavior.

## Process and modules

- `AppHost`: application lifecycle, single-instance policy, shutdown.
- `CapsuleWindow`: one native top-level overlay window.
- `WindowInterop`: HWND styles, Z-order, hit testing, DPI and monitor geometry.
- `CapsuleStateMachine`: Compact, Expanding, Expanded, Collapsing, Hidden.
- `MotionEngine`: interruptible transitions; latest requested state wins.
- `ModuleHost`: media, notifications, timers, system events, progress providers.
- `SettingsStore`: versioned settings persistence and migration.
- `TrayController`: tray menu and explicit exit/settings actions.

Feature modules publish immutable view models/events. They do not manipulate HWND styles or directly control the animation clock.

## Window contract

The capsule is a borderless top-level HWND with a transparent host surface. It is topmost only while enabled by the user. It must not activate on passive display or animation. Interactive regions may receive pointer input without forcing foreground activation where Windows permits it. When a control needs keyboard input, the app must explicitly enter an interactive mode and restore the previous focus policy afterward.

Use `WS_EX_TOOLWINDOW` to avoid a normal taskbar button and evaluate `WS_EX_NOACTIVATE` for passive mode. `WS_EX_LAYERED` and per-pixel alpha are not interchangeable with every composition path; choose one rendering strategy and validate it on Windows before combining APIs. Avoid mixing `SetLayeredWindowAttributes` and `UpdateLayeredWindow` on the same HWND without resetting the layered style, as documented by Win32.

## Motion contract

- State-driven, not click-handler-driven animations.
- A transition can be interrupted at any frame.
- A new target state starts from the current visual values, not from a reset pose.
- Geometry, opacity, blur and shadow use coordinated but independently tunable tracks.
- Reduced-motion mode disables spatial overshoot and uses short opacity/size transitions.
- Performance mode limits idle animation and stops unnecessary composition work.

## Media contract

Use `GlobalSystemMediaTransportControlsSessionManager` to enumerate compatible system media sessions and request transport controls. Compatibility depends on the source application exposing a system media session. Do not promise universal control of every player or browser tab. Lyrics are not provided by this API and require a separately licensed/provider-backed integration; they are not part of the initial MVP.

## Persistence

Settings are stored under `%LOCALAPPDATA%/Dynamic/settings.json`, with a schema version and safe defaults. Writes are atomic (temporary file then replace). Invalid or future-version files are backed up and defaults are restored rather than crashing startup.

## Startup and packaging

Prefer packaged deployment (MSIX) where startup registration and update identity are available. Startup registration must be user-controlled and reversible. An unpackaged EXE fallback can use a Startup-folder shortcut if packaging is not viable. Do not silently add a Run registry key.

## Compatibility and limitations

- Target Windows 10 22H2+ and Windows 11 initially; verify APIs at runtime and provide fallback effects.
- Multi-monitor placement must use monitor work areas and per-monitor DPI awareness.
- Windows may change Z-order, suppress activation, or alter composition under remote desktop, exclusive fullscreen, secure desktop, battery saver, or accessibility settings.
- A non-activating overlay cannot provide unrestricted keyboard focus. Text input and clipboard operations need explicit interaction flows.
- The media API only sees cooperating system media sessions.

## QA gates

A feature is not complete until Windows CI compiles it and the manual/hardware QA matrix records the Windows build, display scale, monitor topology, GPU, and result. CI build success does not prove focus, transparency, blur, hit-testing, or animation behavior.
