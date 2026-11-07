# SimoNotes

SimoNotes is published as a single-file, framework-dependent Windows application. The .NET Desktop Runtime is not bundled with the executable, so the **.NET 10 Desktop Runtime for Windows x64** must be installed before running it.

Download the .NET Desktop Runtime from Microsoft's [official .NET 10 download page](https://dotnet.microsoft.com/en-us/download/dotnet/10.0). Choose the **.NET Desktop Runtime** for **Windows x64**.

Each note has separate desktop and floating text editors. Text, caret, and selection are transferred between them, but WPF keeps undo history per editor, so undo does not continue across edits made in the other view.

Note opacity is currently a bit inconsistent in some conditions and may not behave as expected.