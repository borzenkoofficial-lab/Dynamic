# Dynamic — architecture

## Runtime shell

- WinUI 3 / Windows App SDK provides the native desktop surface and modern XAML composition.
- A Win32 HWND owns the capsule. Native extended styles keep it out of the task switcher and request non-activating display.
- The overlay is positioned relative to the active display work area and marked topmost.
- The capsule has compact and expanded layouts. Native window bounds and the hit-test region are updated together so transparent corners do not intercept clicks.
- Composition spring motion is used for lightweight emphasis; layout state is managed separately from animation.

## State model

Stable targets are Hidden, Compact, and Expanded. Requests to enter or leave the expanded state pass through Expanding and Collapsing; the layout owner completes the transition after applying target geometry. The state machine does not own HWND calls.

## System integrations

- Notifications: use Windows App SDK AppNotificationManager for app-originated local notifications. A notification listener is a separate permission-sensitive integration and must not be conflated with sending notifications.
- Media: use GlobalSystemMediaTransportControlsSessionManager for system media sessions.
- Timers and reminders: local scheduler first; Windows scheduled app notifications can be an optional delivery path.
- Startup: register a startup task only after explicit user opt-in and handle packaged/unpackaged deployment separately.

## Current implementation boundary

The compact/expanded shell and native window geometry are implemented in the repository. Media sessions, notification listening, timer scheduling, startup settings UI, tray integration, and installer packaging remain separate milestones. The expanded panel reports only local app/platform state; it does not display fabricated media, weather, or progress data.
