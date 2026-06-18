using SlimDX.Windows;
using System.Windows.Forms;
using System;

namespace TjaPlayer;

static class Program
{
    [STAThread]
    static void Main()
    {
        try
        {
            Utils.FontManager.Load(); // フォント読み込み
            Utils.SkinManager.Load(); // スキン読み込み
            using (var mainForm = new MainForm())
            {
                mainForm.Show();
                MessagePump.Run(mainForm, () =>
                {
                    mainForm.UpdateLoop();
                    mainForm.RenderLoop();
                });
            }
        }
        catch (Exception ex)
        {
            System.IO.File.WriteAllText("error.log", ex.ToString());
            MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }    
}
