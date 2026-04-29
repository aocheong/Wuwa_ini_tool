using System;
using System.Drawing;
using System.Windows.Forms;

namespace WuwaIniToolCs;

internal static class AutoCloseNotice
{
    public static void Show(string title, string message, int durationMs, IWin32Window? owner = null)
    {
        using var popup = new Form
        {
            Text = title,
            StartPosition = FormStartPosition.CenterScreen,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            ShowInTaskbar = false,
            TopMost = true,
            BackColor = Color.FromArgb(23, 26, 33),
            ForeColor = Color.FromArgb(240, 243, 250),
            ClientSize = new Size(520, 168)
        };

        var label = new Label
        {
            Dock = DockStyle.Fill,
            Text = message,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("맑은 고딕", 10, FontStyle.Regular),
            Padding = new Padding(18, 12, 18, 12),
            ForeColor = popup.ForeColor,
            BackColor = Color.Transparent
        };
        popup.Controls.Add(label);

        var timer = new System.Windows.Forms.Timer { Interval = Math.Max(1, durationMs) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            popup.Close();
        };

        popup.Shown += (_, _) => timer.Start();
        popup.FormClosed += (_, _) => timer.Dispose();

        if (owner is null)
        {
            popup.ShowDialog();
            return;
        }

        popup.ShowDialog(owner);
    }
}
