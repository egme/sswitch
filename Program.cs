using System.Threading;
using System.Windows.Forms;

namespace sswitch;

static class Program
{
    [STAThread]
    static void Main()
    {
        using var mutex = new Mutex(true, "sswitch-3f8a2d1c-4e7b-4a9c-8b6f-1d2e3f4a5b6c", out bool isNew);
        if (!isNew)
        {
            MessageBox.Show("sswitch is already running.", "sswitch",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        using var context = new SswitchApplicationContext();
        Application.Run(context);
    }
}
