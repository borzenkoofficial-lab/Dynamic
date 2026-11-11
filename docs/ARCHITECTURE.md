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
