# sswitch — Claude context

## What this is
Tray-only Windows utility. One system tray icon per virtual desktop; left-click switches to that desktop. Icons update when desktops are added/removed or the active desktop changes.

## Build & run
```powershell
# From PowerShell (Windows dotnet required, runs on Windows only)
cd \\wsl.localhost\Ubuntu-24.04\home\eugene\study\sswitch
dotnet run                  # debug, fine for dev
dotnet build -c Release     # release build
.\bin\Release\net10.0-windows10.0.22621.0\win-x64\sswitch.exe

# Single-file publish (framework-dependent, ~500KB, requires .NET 10 installed)
dotnet publish -c Release
.\bin\Release\net10.0-windows10.0.22621.0\win-x64\publish\sswitch.exe
```

Kill a running instance before relaunching:
```powershell
Stop-Process -Name sswitch -ErrorAction SilentlyContinue
```

## File map
| File | Role |
|---|---|
| `Program.cs` | Entry point. `[STAThread]`, single-instance `Mutex`, `Application.Run(context)` |
| `SswitchApplicationContext.cs` | Owns lifecycle. Subscribes to `DesktopService` events, wires up `TrayIconManager` |
| `TrayIconManager.cs` | One `NotifyIcon` per desktop. Thread marshaling via hidden form's `Invoke`. Icon cleanup on dispose |
| `IconGenerator.cs` | Generates bitmap icons: rounded rect, blue fill (active) or outline (inactive), digit centered via `StringFormat` |
| `VirtualDesktops.cs` | All COM interop + `DesktopService` polling wrapper |
| `app.manifest` | DPI awareness (PerMonitorV2) + Windows 10 compatibility GUID |
| `sswitch.csproj` | `net10.0-windows10.0.22621.0`, `WinExe`, no NuGet dependencies, `PublishSingleFile` (framework-dependent) |

## Architecture

```
Application.Idle (first tick)
  └─ DesktopService.Initialize()     COM objects created on STA thread
       └─ starts 300ms poll timer

Poll tick (UI thread via WinForms Timer)
  ├─ count changed  → DesktopService.DesktopsChanged → TrayIconManager.Rebuild()
  └─ current changed → DesktopService.CurrentChanged → TrayIconManager.UpdateActive()

Left-click on NotifyIcon
  └─ DesktopInfo.Switch() → IVirtualDesktopManagerInternal.SwitchDesktop()
```

## COM interop — critical details

**No NuGet packages.** All virtual desktop access is direct COM interop in `VirtualDesktops.cs`.

**Windows version**: Tested on Windows 11 build 26200 (25H2). Interface definitions match MScholtes/VirtualDesktop `VirtualDesktop11-24H2.cs` (updated 2025-08-11).

**Key GUIDs** (Windows 11 24H2+):
- `CLSID_ImmersiveShell`: `C2F03A33-21F5-47FA-B4BB-156362A2F239`
- `CLSID_VirtualDesktopManagerInternal`: `C5E0CDCA-7B6E-41B2-9FC4-D93975CC467B`
- `IVirtualDesktopManagerInternal` IID: `53F5CA0B-158F-4124-900C-057158060B27`
- `IVirtualDesktop` IID: `3F07F4BE-B107-441A-AF0F-39D82529072C`

**Initialization** (in `DesktopService.Initialize()`):
```csharp
var shell = (IServiceProvider10)Activator.CreateInstance(Type.GetTypeFromCLSID(ClsidImmersiveShell));
_manager  = (IVirtualDesktopManagerInternal)shell.QueryService(ref svcId, ref iid);
```
Must be called on the STA thread AFTER `Application.Run()` starts the message pump (hence the `Application.Idle` defer).

**Why no notification interfaces**: `IVirtualDesktopNotification` GUIDs are not in the MScholtes reference file; polling at 300ms is used instead. Polling is reliable and low-overhead.

**vtable ordering**: `IVirtualDesktopManagerInternal` has 22 methods that must be declared in exact vtable order. Do not reorder or add methods. Reference: MScholtes `VirtualDesktop11-24H2.cs`.

**HSTRING marshaling**: `[return: MarshalAs(UnmanagedType.HString)]` is not supported on `InterfaceIsIUnknown` COM interfaces in .NET Core. `IVirtualDesktop.GetName()` and `GetWallpaperPath()` return `IntPtr` (raw HSTRING). Use `HString.ToStringAndFree()` in `VirtualDesktops.cs` which calls `WindowsGetStringRawBuffer` + `WindowsDeleteString` via P/Invoke to `api-ms-win-core-winrt-string-l1-1-0.dll`.

## WinForms tray patterns

**Hidden form**: `TrayIconManager` owns a hidden `Form` (never shown) to provide an HWND for `NotifyIcon` message pump and as `ISynchronizeInvoke` for cross-thread calls. All `NotifyIcon` manipulation goes through `_hiddenForm.Invoke(...)` because `DesktopService` events fire on the UI thread (WinForms timer), but `Rebuild`/`UpdateActive` may theoretically be called from elsewhere.

**Ghost icon prevention**: Always set `ni.Visible = false` before `ni.Dispose()`. Skipping this leaves a ghost icon in the tray until the user hovers over it.

**Icon ordering**: Icons are added in reverse order (`for i = N-1 downto 0`) so they display left-to-right as 1, 2, 3… in the tray. Windows inserts new tray icons to the left of existing ones.

**GDI handle leak**: `Bitmap.GetHicon()` returns an unmanaged HICON. Always `.Clone()` the `Icon.FromHandle()` result and call `DestroyIcon(hIcon)` immediately after. The clone is independent of the HICON.

## History / what was tried

- **Grabacr07/VirtualDesktop NuGet package**: rejected — its `ThrowIfNotSupported()` check reads the process activation context via `QueryActCtxW`. In .NET Core/5+ the CLR replaces the process activation context, hiding the app manifest, so the check always fails regardless of manifest content.
- **Harmony patching** (`Lib.Harmony`): bypassed the check successfully but the package's COM initialization still returned null for Windows 11 build 26200 (unsupported version). Removed.
- **Direct COM interop**: current approach. No external dependencies, no version checks. GUIDs sourced from MScholtes/VirtualDesktop (authoritative open-source reference).

## If COM breaks after a Windows update
The undocumented interfaces change GUIDs between major Windows releases. Symptoms: `COMException` or `NullReferenceException` from `DesktopService.Initialize()`. Fix:
1. Check MScholtes/VirtualDesktop for a new `VirtualDesktop11-*.cs` file matching the new build
2. Update the GUIDs and vtable in `VirtualDesktops.cs`
