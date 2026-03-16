using System;
using System.Windows.Forms;

namespace sswitch;

internal sealed class SswitchApplicationContext : ApplicationContext
{
    private readonly TrayIconManager _manager;

    public SswitchApplicationContext()
    {
        _manager = new TrayIconManager(ExitApplication);

        DesktopService.CurrentChanged  += OnCurrentChanged;
        DesktopService.DesktopsChanged += OnDesktopsChanged;

        Application.Idle += OnFirstIdle;
    }

    private void OnFirstIdle(object? sender, EventArgs e)
    {
        Application.Idle -= OnFirstIdle;
        try
        {
            DesktopService.Initialize();
            _manager.Rebuild(DesktopService.GetAll(), DesktopService.GetCurrent());
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Init failed:\n\n{ex}", "sswitch error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            Application.Exit();
        }
    }

    private void OnCurrentChanged(object? sender, EventArgs e)
        => _manager.UpdateActive(DesktopService.GetCurrent());

    private void OnDesktopsChanged(object? sender, EventArgs e)
        => _manager.Rebuild(DesktopService.GetAll(), DesktopService.GetCurrent());

    private void ExitApplication()
    {
        DesktopService.Stop();
        _manager.Dispose();
        Application.Exit();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            DesktopService.CurrentChanged  -= OnCurrentChanged;
            DesktopService.DesktopsChanged -= OnDesktopsChanged;
            DesktopService.Stop();
            _manager.Dispose();
        }
        base.Dispose(disposing);
    }
}
