using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace sswitch;

internal sealed class TrayIconManager : IDisposable
{
    private readonly Action _exitCallback;
    private readonly List<NotifyIcon> _icons = new();
    private readonly Form _hiddenForm;
    private bool _disposed;

    public TrayIconManager(Action exitCallback)
    {
        _exitCallback = exitCallback;

        _hiddenForm = new Form
        {
            WindowState   = FormWindowState.Minimized,
            ShowInTaskbar = false,
            Opacity       = 0,
            Text          = "sswitch-host",
        };
        _hiddenForm.Show();
        _hiddenForm.Hide();
    }

    public void Rebuild(DesktopInfo[] desktops, DesktopInfo current)
    {
        if (_hiddenForm.InvokeRequired)
        {
            _hiddenForm.Invoke(() => Rebuild(desktops, current));
            return;
        }

        DisposeIcons();

        int iconSize = SystemInformation.SmallIconSize.Width;
        // Add in reverse so the tray displays them left-to-right as 1, 2, 3…
        for (int i = desktops.Length - 1; i >= 0; i--)
        {
            bool isActive = desktops[i].Id == current.Id;
            _icons.Insert(0, CreateIcon(desktops[i], i + 1, isActive, iconSize));
        }
    }

    public void UpdateActive(DesktopInfo current)
    {
        if (_hiddenForm.InvokeRequired)
        {
            _hiddenForm.Invoke(() => UpdateActive(current));
            return;
        }

        var desktops = DesktopService.GetAll();
        int iconSize  = SystemInformation.SmallIconSize.Width;

        for (int i = 0; i < _icons.Count && i < desktops.Length; i++)
        {
            bool isActive = desktops[i].Id == current.Id;
            var  oldIcon  = _icons[i].Icon;
            _icons[i].Icon = IconGenerator.Generate(i + 1, isActive, iconSize);
            _icons[i].Text = TooltipText(i + 1, desktops[i].Name, isActive);
            oldIcon?.Dispose();
        }
    }

    private NotifyIcon CreateIcon(DesktopInfo desktop, int number, bool isActive, int iconSize)
    {
        var contextMenu = new ContextMenuStrip();
        contextMenu.Items.Add(new ToolStripMenuItem($"Desktop {number}") { Enabled = false });
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add("Exit", null, (_, _) => _exitCallback());

        var ni = new NotifyIcon
        {
            Icon             = IconGenerator.Generate(number, isActive, iconSize),
            Visible          = true,
            Text             = TooltipText(number, desktop.Name, isActive),
            ContextMenuStrip = contextMenu,
        };

        ni.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
                desktop.Switch();
        };

        return ni;
    }

    private static string TooltipText(int number, string name, bool isActive)
    {
        string label = string.IsNullOrEmpty(name) ? $"Desktop {number}" : name;
        return isActive ? $"{label} (active)" : label;
    }

    private void DisposeIcons()
    {
        foreach (var ni in _icons)
        {
            ni.Visible = false;
            ni.Icon?.Dispose();
            ni.ContextMenuStrip?.Dispose();
            ni.Dispose();
        }
        _icons.Clear();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        DisposeIcons();
        _hiddenForm.Dispose();
    }
}
