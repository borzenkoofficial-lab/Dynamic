# Dynamic — architecture

## Runtime shell

- WinUI 3 / Windows App SDK provides the native desktop surface and modern XAML composition.
- A Win32 HWND owns the capsule. Native extended styles keep it out of the task switcher and request non-activating display.
- The overlay is positioned relative to the primary display work area and marked topmost.
- The capsule has compact and expanded layouts. Native window bounds and the hit-test region are updated together so transparent corners do not intercept clicks.
- Composition spring motion is used for lightweight emphasis; layout state is managed separately from animation.

## State model

Stable targets are Hidden, Compact, and Expanded. Requests to enter or leave the expanded state pass through Expanding and Collapsing; the layout owner completes the transition after applying target geometry. The state machine does not own HWND calls.

## System integrations

- Media: GlobalSystemMediaTransportControlsSessionManager reads the active Windows media session and requests play/pause/next actions. Apps may decline transport requests or expose incomplete metadata.
- Notifications: use Windows App SDK AppNotificationManager for app-originated local notifications. A notification listener is a separate permission-sensitive integration and must not be conflated with sending notifications.
- Timers and reminders: local scheduler first; Windows scheduled app notifications can be an optional delivery path.
- Startup: register a startup task only after explicit user opt-in and handle packaged/unpackaged deployment separately.

## Current implementation boundary

The compact/expanded shell and first media-session integration are implemented in source. Media artwork, notification listening, timer scheduling, startup settings UI, tray integration, and installer packaging remain separate milestones. The app does not fabricate media, weather, or progress data.


## Animated interaction and activity modules

- Window expansion/collapse is interpolated from live HWND bounds; each frame updates both position and rounded hit-test region. Transitions can be interrupted and retargeted from the current geometry.
- Media metadata includes timeline position/duration and the system-provided thumbnail. The UI extrapolates playback position locally between session updates; it does not invent track data.
- Countdown uses a monotonic Stopwatch, so wall-clock adjustments do not change remaining time. Completion raises an in-app transient notification.
- ProgressActivityService is a producer contract for real download/export/install integrations. The UI stays hidden until a producer reports actual progress; no fabricated system-wide download detection is implied.


## Notification and progress boundaries

- The current notification surface is an in-app transient toast for Dynamic-owned events (timer completion and completed activity reports). It is not yet an OS notification listener.
- Windows notification ingestion requires a separate permission/package-identity decision and is not represented as implemented.
- Progress cards render producer-reported percentage, detail and speed. No system-wide download scanner is claimed until a real supported source is connected.
- Countdown presets are 5, 15 and 25 minutes; countdown uses monotonic elapsed time and currently resets when the app process exits.
