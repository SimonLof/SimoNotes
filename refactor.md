# Refactor proposal

## Summary

The project already has a useful feature-oriented starting point: `Notes`, `Settings`, and `Desktop` keep related code together. Preserve that vertical-slice structure. The biggest maintainability opportunity is reducing the amount of coordination held by `App` and `NoteManager`, not introducing a new application-wide layering scheme or interfaces for every class.

## Recommended refactors

### 1. Separate note layer/window coordination from note collection management

`NoteManager` currently owns the note collection and state, creates and tears down two views and a floating window per note, routes view events, handles activation/deactivation and fade transitions, tracks drag/resize state, maps screen coordinates, and performs DPI/native redraw work. These responsibilities are tightly coupled in one class, which makes changes to window behavior risky and difficult to test independently.

Refactor in small steps:

1. Keep `NoteManager` as the feature-facing API used by the app and notes list.
2. Extract a `NoteWindowCoordinator` (or similarly named class) to own each note's desktop/floating view transition, window activation, and layer-specific visibility.
3. Extract a `NotePositioningService` only if screen-coordinate, display-change, and DPI behavior still form a cohesive unit after the first extraction. Keep Win32 calls behind the existing `Desktop` interop boundary.
4. Leave collection operations, note state, and `NotesChanged` notifications with `NoteManager`; avoid creating a generic event-bus or service locator.

The extraction should keep the existing behavior for hidden notes, always-on-top notes, temporary “bring all to front,” focus/caret restoration, and drag/resize transitions. Add focused tests around extracted decision logic where it can be tested without WPF windows; retain manual/integration checks for actual WPF and desktop interop behavior.

### 2. Move application workflows out of `App.xaml.cs`

`App` currently handles startup and shutdown, single-instance enforcement, tray UI, settings-window lifetime, hotkey changes, note creation, save debouncing, save errors, and note persistence. It is the composition root, but also contains several complete workflows.

Extract cohesive coordinators while keeping `App` responsible for composition and application lifetime:

- `NoteSaveCoordinator`: subscribe to note changes, debounce saves, handle shutdown saves, and report persistence errors.
- `SettingsCoordinator`: apply and persist hotkey and default-appearance changes, including error reporting to the settings view.
- A tray/menu presenter only if tray setup and menu actions continue to grow; otherwise keep the small tray setup in `App`.

Pass the existing `IConfigPersistence` and `INotePersistence` abstractions into these coordinators rather than letting them construct file implementations. Keep UI notifications explicit and close to their owning window.

### 3. Move all windows to MVVM after clarifying application-state boundaries

The long-term goal should be a consistent MVVM structure for every window, so behavior and navigation are predictable across the app. This includes `SettingsWindow`, `NotesManagerWindow`, and `FontLicenseWindow`; simple windows can have small view models rather than exceptions to the convention. Apply the same separation to substantial user controls such as `NoteControl` where it improves clarity.

Do this after extracting the app workflows and note-window coordination above. Those refactors establish which state and operations belong to the application, note, and settings features; view models can then consume those clear boundaries instead of absorbing responsibilities directly from `App` or `NoteManager`.

- Put binding declarations in each view's XAML, separate from code-behind, and assign a feature-local view model as its `DataContext`.
- Put user-facing state, validation, and feature actions in view models; expose state through bindable properties and actions through commands.
- Keep code-behind focused on view updates and WPF-specific interactions that are awkward or inappropriate as application logic, such as focus/caret placement, popup/window lifecycle, and visual-only caret blinking. Translate those interactions into view-model commands or explicit events.
- Keep rendering brushes, DPI transforms, and native window dragging in the view or a dedicated WPF interaction component rather than coupling view models to WPF controls.
- Extract key-to-`HotkeyBinding` conversion/validation into testable logic, with the settings view model owning the resulting state and action.

Migrate one window at a time, starting with `NotesManagerWindow`, then `SettingsWindow` and `FontLicenseWindow`. Keep view models independent of WPF controls so their behavior can be unit tested. Avoid a generic MVVM framework or a big-bang rewrite; use the existing feature boundaries and introduce only the notification/command support needed by the views.

### 4. Make persistence contracts explicit at the feature boundary

`INotePersistence` and `IConfigPersistence` are useful seams for file I/O. Keep them feature-local and inject them at composition time. Make the note-load behavior explicit in the contract: distinguish “no saved file” (first run) from “could not read or parse saved data” rather than returning an empty list for both. That distinction lets startup handle recovery deliberately and prevents persistence failures from looking like a successful empty state.

If settings persistence adds more data, consider a single settings document/store rather than accumulating one store class per setting. Avoid a generic repository abstraction; JSON file access is the implementation detail that needs a seam here.

## Suggested vertical-slice layout

Keep the current feature roots and introduce subfolders only as each feature grows. For example:

```text
SimoNotes/
  Desktop/
    Interop/
    DesktopHost.cs
  Notes/
    Model/
      NoteAppearance.cs
      NoteModel.cs
      NoteState.cs
    Persistence/
      INotePersistence.cs
      NotePersistenceToFile.cs
    UI/
      NoteControl.xaml
      NoteControl.xaml.cs
      NotesManagerWindow.xaml
      NotesManagerWindow.xaml.cs
    ViewModels/
      NotesManagerViewModel.cs
    NoteManager.cs
    NoteWindowCoordinator.cs
  Settings/
    Model/
    Persistence/
    UI/
    ViewModels/
      SettingsViewModel.cs
      FontLicenseViewModel.cs
    SettingsCoordinator.cs
  App.xaml
  App.xaml.cs
```

Treat this as a direction, not a required bulk move: keep small feature folders flat until there is enough code to benefit from subfolders. Do not create top-level `Models`, `Views`, `Services`, or `Repositories` buckets; those would scatter each vertical slice across the project. `Desktop` remains the boundary for Windows-specific host and interop concerns.

## Suggested order

1. Extract and test `NoteSaveCoordinator`; it is a bounded workflow with clear persistence inputs and avoids changing note-window behavior.
2. Extract note-window/layer coordination from `NoteManager`, preserving the public feature-facing API.
3. Separate settings application/persistence from app lifetime so settings state and actions have a clear feature boundary.
4. Reorganize folders during those extractions, moving only files belonging to the changed slice.
5. As the final structural step, migrate every window to MVVM one at a time, beginning with `NotesManagerWindow`; keep bindings in XAML, view-only updates in code-behind, and user-facing logic in the view model.

For each step, keep the application behavior unchanged, build the WPF project, and manually verify startup/shutdown persistence, tray actions, hotkey changes, note visibility/layer transitions, dragging, resizing, and display/DPI changes as applicable.

## Scope note

This is a refactor proposal only; no source code was changed. The worktree already contains unrelated modifications and deletions, which should be preserved during any implementation.
