# Code Review

## Findings

### [P2] Skip DPI interop for text-only changes

[NoteControl.xaml.cs](SimoNotes/Notes/NoteControl.xaml.cs#L242) raises `NoteChanged` for every editor text change. [NoteManager.cs](SimoNotes/Notes/NoteManager.cs#L507) handles that general-purpose event by calling `UpdateDesktopNoteScale`, which converts the note position to screen coordinates and queries the monitor DPI through native APIs. Typing therefore repeats geometry and DPI work even though the note has not moved or resized. This is on the interactive path and may add avoidable latency during editing.

Separate content changes from geometry changes, or otherwise recalculate scale only when position, size, or monitor changes. Keep the existing DPI update for those geometry transitions.

### [P2] Avoid synchronous note-file writes on the UI thread

The dispatcher timer calls `SaveNotesNow` after each 400 ms quiet period ([App.xaml.cs](SimoNotes/App.xaml.cs#L303)); that collects every note and calls `NoteStateStore.Save`, which serializes the full collection and performs synchronous file writes and replacement ([NoteStateStore.cs](SimoNotes/Notes/NoteStateStore.cs#L31)). Large note collections or long text can briefly block input and rendering after edits. The debounce reduces write frequency but does not move the work off the UI thread.

Capture the WPF-owned note state on the dispatcher, then serialize and write that immutable snapshot asynchronously. Coalesce pending snapshots and preserve write ordering so an older save cannot overwrite a newer one.

### [P2] Debounce default-appearance persistence

The settings opacity slider handles every value change ([SettingsWindow.xaml.cs](SimoNotes/Settings/SettingsWindow.xaml.cs#L118)) and emits a new appearance from `SetDefaultAppearance` ([SettingsWindow.xaml.cs](SimoNotes/Settings/SettingsWindow.xaml.cs#L131)). The app immediately saves that appearance ([App.xaml.cs](SimoNotes/App.xaml.cs#L216), [NoteAppearanceStore.cs](SimoNotes/Settings/NoteAppearanceStore.cs#L29)). Dragging the slider can therefore cause repeated synchronous serialization and disk replacement on the UI thread.

Debounce this setting save, or save once when the interaction is committed/window closes. Continue updating the preview immediately; only persistence needs to be delayed.

### [P3] Consolidate repeated note teardown paths

`NoteManager` repeats event unsubscription, collection removal, and floating-window cleanup across explicit close, floating-window close, remove-all, and disposal paths. The duplicated `_floatingNoteWindows.Clear()` in `Dispose` ([NoteManager.cs](SimoNotes/Notes/NoteManager.cs#L200)) is a small sign that these paths can drift.

Consider a small shared cleanup routine for per-note event and collection cleanup, while keeping the distinct window-close behavior at each call site. This is a maintainability improvement, not a hot-path optimization.

### [P3] Share the common atomic JSON-write implementation

`NoteStateStore`, `HotkeyBindingStore`, and `NoteAppearanceStore` each create the same application-data directory, serialize JSON, write a `.tmp` file, and replace the destination. A narrow shared helper for this sequence would reduce duplicated persistence mechanics and make future changes to atomic replacement consistent. The current duplication is small, so this is worthwhile mainly if more persisted settings are added.

## Review Scope

These are static code-path findings; performance impact has not been measured with a profiler. No behavior changes were made as part of this review.