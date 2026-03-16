# sswitch

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![Platform: Windows 11](https://img.shields.io/badge/platform-Windows%2011-blue)](https://www.microsoft.com/windows)
[![.NET 10](https://img.shields.io/badge/.NET-10-512BD4)](https://dotnet.microsoft.com/download/dotnet/10.0)

One tray icon per Windows virtual desktop. Click to switch.

```
[1] [2] [3]   ← system tray icons (active = blue fill, inactive = outline)
```

## Requirements

- Windows 11 (build 26100+)
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) installed on Windows

## Run

```powershell
dotnet run
```

Or publish a single-file release binary:

```powershell
dotnet publish -c Release
.\bin\Release\net10.0-windows10.0.22621.0\win-x64\publish\sswitch.exe
```

## Usage

| Action | Result |
|---|---|
| Left-click icon **N** | Switch to desktop N |
| Right-click any icon → **Exit** | Quit |

Icons live in the system tray (bottom-right). On Windows 11 they may be in the `^` overflow area — drag them out to pin them to the visible tray.

Active desktop icon is **blue filled** (rounded rectangle). Inactive desktops are **outline only**. Hovering an icon shows the desktop name (or "Desktop N" if unnamed).

Icons update automatically when desktops are added, removed, or switched by any means (keyboard shortcuts, Task View, etc.).

## How it works

Uses undocumented Windows COM interfaces (`IVirtualDesktopManagerInternal`) to enumerate desktops and switch between them. A 300ms poll detects external desktop changes. No third-party dependencies.

Interface definitions sourced from [MScholtes/VirtualDesktop](https://github.com/MScholtes/VirtualDesktop).

## Windows update compatibility

The COM interface GUIDs can change with major Windows releases. If the app stops working after an update, check [MScholtes/VirtualDesktop](https://github.com/MScholtes/VirtualDesktop) for updated GUIDs and update `VirtualDesktops.cs` accordingly.

## Contributing

Issues and PRs welcome. The COM interface definitions in `VirtualDesktops.cs` are the most likely thing to need updating over time — see the Windows update section above.

## License

[MIT](LICENSE)
