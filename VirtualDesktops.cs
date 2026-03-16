// Direct COM interop for Windows virtual desktops.
// Interface definitions based on MScholtes/VirtualDesktop (Windows 11 24H2+, build 26100+).
// https://github.com/MScholtes/VirtualDesktop/blob/master/VirtualDesktop11-24H2.cs

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace sswitch;

// ── COM interfaces ────────────────────────────────────────────────────────────

[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("6D5140C1-7436-11CE-8034-00AA006009FA")]
internal interface IServiceProvider10
{
    [return: MarshalAs(UnmanagedType.IUnknown)]
    object QueryService(ref Guid service, ref Guid riid);
}

[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("92CA9DCD-5622-4BBA-A805-5E9F541BD8C9")]
internal interface IObjectArray
{
    void GetCount(out int count);
    void GetAt(int index, ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out object obj);
}

[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("3F07F4BE-B107-441A-AF0F-39D82529072C")]
internal interface IVirtualDesktop
{
    bool IsViewVisible(IntPtr view);
    Guid GetId();
    IntPtr GetName();         // returns HSTRING; use HString.ToString(ptr)
    IntPtr GetWallpaperPath(); // returns HSTRING
    bool IsRemote();
}

// Vtable matches Windows 11 24H2 (build 26100+) — MScholtes VirtualDesktop11-24H2.cs
[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("53F5CA0B-158F-4124-900C-057158060B27")]
internal interface IVirtualDesktopManagerInternal
{
    int GetCount();
    void MoveViewToDesktop(IntPtr view, IVirtualDesktop desktop);
    bool CanViewMoveDesktops(IntPtr view);
    IVirtualDesktop GetCurrentDesktop();
    void GetDesktops(out IObjectArray desktops);
    [PreserveSig] int GetAdjacentDesktop(IVirtualDesktop from, int direction, out IVirtualDesktop desktop);
    void SwitchDesktop(IVirtualDesktop desktop);
    void SwitchDesktopAndMoveForegroundView(IVirtualDesktop desktop);
    IVirtualDesktop CreateDesktop();
    void MoveDesktop(IVirtualDesktop desktop, int nIndex);
    void RemoveDesktop(IVirtualDesktop desktop, IVirtualDesktop fallback);
    IVirtualDesktop FindDesktop(ref Guid desktopId);
    void GetDesktopSwitchIncludeExcludeViews(IVirtualDesktop desktop, out IObjectArray u1, out IObjectArray u2);
    void SetDesktopName(IVirtualDesktop desktop, [MarshalAs(UnmanagedType.HString)] string name);
    void SetDesktopWallpaper(IVirtualDesktop desktop, [MarshalAs(UnmanagedType.HString)] string path);
    void UpdateWallpaperPathForAllDesktops([MarshalAs(UnmanagedType.HString)] string path);
    void CopyDesktopState(IntPtr view0, IntPtr view1);
    void CreateRemoteDesktop([MarshalAs(UnmanagedType.HString)] string path, out IVirtualDesktop desktop);
    void SwitchRemoteDesktop(IVirtualDesktop desktop, IntPtr switchtype);
    void SwitchDesktopWithAnimation(IVirtualDesktop desktop);
    void GetLastActiveDesktop(out IVirtualDesktop desktop);
    void WaitForAnimationToComplete();
}

// ── HSTRING helper ────────────────────────────────────────────────────────────

internal static class HString
{
    [DllImport("api-ms-win-core-winrt-string-l1-1-0.dll", CallingConvention = CallingConvention.StdCall)]
    private static extern IntPtr WindowsGetStringRawBuffer(IntPtr hstring, out uint length);

    [DllImport("api-ms-win-core-winrt-string-l1-1-0.dll", CallingConvention = CallingConvention.StdCall)]
    private static extern int WindowsDeleteString(IntPtr hstring);

    public static string ToStringAndFree(IntPtr hstring)
    {
        if (hstring == IntPtr.Zero) return "";
        var ptr    = WindowsGetStringRawBuffer(hstring, out uint length);
        var result = length == 0 ? "" : Marshal.PtrToStringUni(ptr, (int)length) ?? "";
        WindowsDeleteString(hstring);
        return result;
    }
}

// ── Public model ──────────────────────────────────────────────────────────────

internal sealed class DesktopInfo
{
    private readonly IVirtualDesktop _com;
    private readonly IVirtualDesktopManagerInternal _manager;

    public Guid   Id    { get; }
    public int    Index { get; }
    public string Name  { get; }

    internal DesktopInfo(IVirtualDesktop com, int index, IVirtualDesktopManagerInternal manager)
    {
        _com     = com;
        _manager = manager;
        Id       = com.GetId();
        Index    = index;
        Name     = HString.ToStringAndFree(com.GetName());
    }

    public void Switch() => _manager.SwitchDesktop(_com);
}

// ── Service (polling-based) ───────────────────────────────────────────────────

internal static class DesktopService
{
    private static readonly Guid ClsidImmersiveShell             = new("C2F03A33-21F5-47FA-B4BB-156362A2F239");
    private static readonly Guid ClsidVirtualDesktopMgrInternal  = new("C5E0CDCA-7B6E-41B2-9FC4-D93975CC467B");
    private static readonly Guid IidVirtualDesktop               = typeof(IVirtualDesktop).GUID;

    private static IVirtualDesktopManagerInternal? _manager;
    private static System.Windows.Forms.Timer? _pollTimer;
    private static int  _lastCount;
    private static Guid _lastCurrentId;

    public static event EventHandler? CurrentChanged;
    public static event EventHandler? DesktopsChanged;

    // Must be called once on the STA (UI) thread after Application.Run starts.
    public static void Initialize()
    {
        var shell = (IServiceProvider10)Activator.CreateInstance(
            Type.GetTypeFromCLSID(ClsidImmersiveShell)!)!;
        var svcId  = ClsidVirtualDesktopMgrInternal;
        var iid    = typeof(IVirtualDesktopManagerInternal).GUID;
        _manager = (IVirtualDesktopManagerInternal)shell.QueryService(ref svcId, ref iid);

        var current = _manager.GetCurrentDesktop();
        _lastCurrentId = current.GetId();
        _lastCount     = _manager.GetCount();

        _pollTimer = new System.Windows.Forms.Timer { Interval = 300 };
        _pollTimer.Tick += Poll;
        _pollTimer.Start();
    }

    public static DesktopInfo[] GetAll()
    {
        _manager!.GetDesktops(out var array);
        array.GetCount(out int count);
        var result = new DesktopInfo[count];
        var iidVd = IidVirtualDesktop;
        for (int i = 0; i < count; i++)
        {
            array.GetAt(i, ref iidVd, out var obj);
            result[i] = new DesktopInfo((IVirtualDesktop)obj, i, _manager);
        }
        Marshal.ReleaseComObject(array);
        return result;
    }

    public static DesktopInfo GetCurrent()
    {
        var com = _manager!.GetCurrentDesktop();
        var all = GetAll();
        foreach (var d in all)
            if (d.Id == com.GetId()) return d;
        return all[0];
    }

    public static void Stop() => _pollTimer?.Stop();

    private static void Poll(object? sender, EventArgs e)
    {
        try
        {
            int count = _manager!.GetCount();
            if (count != _lastCount)
            {
                _lastCount = count;
                DesktopsChanged?.Invoke(null, EventArgs.Empty);
                return; // CurrentChanged will fire on the next tick after rebuild
            }

            var currentId = _manager.GetCurrentDesktop().GetId();
            if (currentId != _lastCurrentId)
            {
                _lastCurrentId = currentId;
                CurrentChanged?.Invoke(null, EventArgs.Empty);
            }
        }
        catch
        {
            // COM error (e.g. explorer restart) — ignore and retry next tick
        }
    }
}
