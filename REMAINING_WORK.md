# SimoNotes: Remaining Work

This checklist tracks the requirements from the original SimoNotes brief against the current `SimoNotes` project.

## Current Baseline

- .NET 10 WPF application with no ordinary startup window.
- A dedicated, per-pixel-transparent `HwndSource` desktop host, owned by Explorer's desktop window.
- Explorer desktop discovery through `Progman`, `WorkerW`, and `SHELLDLL_DefView`, with a `Progman` fallback.
- One editable sticky note with a draggable title area, close button, resizable dimensions, and an individual position.
- `NoteManager` can create and remove note controls; closing a note does not shut down the application.
- The current solution builds, and the desktop note has been visually verified on the development machine.

## Immediate Application Features

- [x] Register the global `Ctrl+Alt+N` shortcut for the full application lifetime.
- [x] Handle `WM_HOTKEY` in the native host/message path and create a note through `NoteManager`.
- [x] Detect hotkey registration failure, such as a shortcut already owned by another application, and report it without crashing.
- [x] Add a notification-area icon and tray menu with **New note** and **Exit** commands.
- [x] Keep the application running when the last note is closed; exit only through the tray command or explicit application shutdown.
- [x] Verify the desktop host does not create an ordinary taskbar button and the tray icon is available while the app runs.

## Future Improvements

- [ ] Address global hotkey conflicts: warn about common application shortcuts and prevent bindings such as `Ctrl+N` from triggering SimoNotes instead of the foreground app's command, such as VS Code's New Window action.
- [ ] Add first-run install-like behavior: when the app has never been started on the machine, ask whether to run it from its current location as a standalone executable or install it. Save the selected option in the program's settings so the standalone choice suppresses this prompt on later launches. The install flow should move the executable to a user-selected install path, add that path to `PATH`, and create the Windows registrations needed for the app to behave like an installed program. Add a `--uninstall` run option if needed; it should remove the registrations and delete the installed executable.
- [ ] Expand the note color palette and allow users to choose custom colors.
- [ ] Load custom note style definitions from a per-user JSON config, such as `~/.simonotes/config.json`.

## Desktop Integration Hardening

- [ ] Verify note placement and visibility across supported Windows and Explorer configurations; `WorkerW` behavior is undocumented and varies.
- [ ] Handle Explorer restarting or recreating desktop windows: rediscover the correct host and reattach or recreate the desktop source.
- [ ] Handle display topology, resolution, and DPI changes; resize/reposition the host and keep notes at valid desktop coordinates.
- [ ] Test desktop hit testing and Z-order with icons, application windows, Show Desktop, multiple Explorer windows, and multiple monitors.
- [ ] Define and test graceful behavior when `Progman` or a usable `WorkerW` cannot be found.

## Note Behavior and State

- [x] Add a user-accessible way to create notes in addition to the global shortcut, such as the tray menu.
- [x] Ensure multiple notes can be created, moved, resized, edited, and closed independently.
- [x] Persist and restore each note's text, position, dimensions, style, color, and opacity across application restarts.
- [x] Add per-note Note and Terminal styles, with configurable style, color, and opacity defaults for new notes.
- [x] Add text formatting and note-specific settings as required.
- [ ] Keep note coordinates and sizes valid when monitors are added, removed, or rearranged.

## Independent Display Modes

- [x] Add an always-on-top mode per note.
- [x] Implement always-on-top notes independently from the shared desktop-layer host, so changing one note's mode does not change the Z-order of other notes.
- [x] Preserve each note's content and state when switching between desktop-layer and always-on-top modes.

## Verification and Delivery

- [ ] Add focused tests for note creation/removal, state persistence, and mode changes where practical.
- [ ] Add a repeatable Windows runtime verification checklist for hotkey registration, tray lifecycle, desktop visibility, and Explorer recovery.
- [ ] Confirm startup, shutdown, and native-resource cleanup work without leaving HWNDs, hotkeys, or tray icons behind.
- [ ] Document supported Windows assumptions and the limitations of Explorer's undocumented desktop-window hierarchy.
