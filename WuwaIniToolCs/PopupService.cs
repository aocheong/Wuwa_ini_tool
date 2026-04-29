using System;
using System.Drawing;
using System.Windows.Forms;

namespace WuwaIniToolCs;

internal static class PopupService
{
    public static DialogResult ShowInfo(string title, string message, IWin32Window? owner = null)
    {
        return Show(title, message, MessageBoxIcon.Information, MessageBoxButtons.OK, owner);
    }

    public static DialogResult ShowWarning(string title, string message, IWin32Window? owner = null)
    {
        return Show(title, message, MessageBoxIcon.Warning, MessageBoxButtons.OK, owner);
    }

    public static DialogResult ShowError(string title, string message, IWin32Window? owner = null)
    {
        return Show(title, message, MessageBoxIcon.Error, MessageBoxButtons.OK, owner);
    }

    public static DialogResult ConfirmYesNo(string title, string message, IWin32Window? owner = null)
    {
        return Show(title, message, MessageBoxIcon.Question, MessageBoxButtons.YesNo, owner);
    }

    public static DialogResult ShowCacheRebuildNotice(IWin32Window? owner = null)
    {
        return ShowInfo(
            "안내",
            "캐시 파일 삭제가 완료되었습니다.\n"
            + "처음 몇 분 동안은 캐시를 다시 만드는 과정에서\n"
            + "일시적으로 프레임 드랍이나 끊김(스터터링)이 나타날 수 있습니다.\n"
            + "잠시 플레이하면 점차 안정되며 정상 상태로 돌아옵니다.",
            owner);
    }

    private static DialogResult Show(
        string title,
        string message,
        MessageBoxIcon icon,
        MessageBoxButtons buttons,
        IWin32Window? owner)
    {
        try
        {
            using var popup = new PopupDialog(title, message, icon, buttons);
            return owner is null ? popup.ShowDialog() : popup.ShowDialog(owner);
        }
        catch
        {
            return owner is null
                ? MessageBox.Show(message, title, buttons, icon)
                : MessageBox.Show(owner, message, title, buttons, icon);
        }
    }

    private sealed class PopupDialog : Form
    {
        public PopupDialog(string title, string message, MessageBoxIcon icon, MessageBoxButtons buttons)
        {
            Text = title;
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            TopMost = true;
            BackColor = Color.FromArgb(23, 26, 33);
            ForeColor = Color.FromArgb(240, 243, 250);

            var messageFont = new Font("맑은 고딕", 10, FontStyle.Regular);
            var area = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1280, 720);
            var maxClientWidth = Math.Max(540, area.Width - 120);
            var maxClientHeight = Math.Max(260, area.Height - 120);

            var maxMessageWidth = Math.Max(360, maxClientWidth - 56 - 32);
            var preferredMessageWidth = GetPreferredMessageWidth(message, messageFont, maxMessageWidth);
            var measured = TextRenderer.MeasureText(
                message,
                messageFont,
                new Size(preferredMessageWidth, int.MaxValue),
                TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl);

            var contentHeight = Math.Max(88, measured.Height + 8);
            var buttonHeight = 46;
            var desiredWidth = 56 + 32 + preferredMessageWidth;
            var desiredHeight = 32 + contentHeight + 12 + buttonHeight;

            ClientSize = new Size(
                Math.Min(maxClientWidth, Math.Max(540, desiredWidth)),
                Math.Min(maxClientHeight, Math.Max(220, desiredHeight)));

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 2,
                Padding = new Padding(16),
                BackColor = BackColor
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 56));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var iconLabel = new Label
            {
                Dock = DockStyle.Top,
                Text = GetIconGlyph(icon),
                Font = new Font("맑은 고딕", 24, FontStyle.Bold),
                ForeColor = GetIconColor(icon),
                TextAlign = ContentAlignment.TopCenter,
                Height = 48,
                BackColor = Color.Transparent
            };

            var messageLabel = new Label
            {
                Dock = DockStyle.Fill,
                Text = message,
                Font = messageFont,
                ForeColor = ForeColor,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.TopLeft,
                AutoSize = false
            };

            var buttonPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                AutoSize = true,
                WrapContents = false,
                Margin = new Padding(0, 12, 0, 0),
                BackColor = Color.Transparent
            };

            if (buttons == MessageBoxButtons.YesNo)
            {
                var noButton = BuildButton("아니오", DialogResult.No);
                var yesButton = BuildButton("예", DialogResult.Yes);
                CancelButton = noButton;
                AcceptButton = yesButton;
                buttonPanel.Controls.Add(noButton);
                buttonPanel.Controls.Add(yesButton);
            }
            else
            {
                var okButton = BuildButton("확인", DialogResult.OK);
                AcceptButton = okButton;
                CancelButton = okButton;
                buttonPanel.Controls.Add(okButton);
            }

            root.Controls.Add(iconLabel, 0, 0);
            root.Controls.Add(messageLabel, 1, 0);
            root.Controls.Add(buttonPanel, 1, 1);
            Controls.Add(root);
        }

        private static int GetPreferredMessageWidth(string message, Font font, int maxWidth)
        {
            var lines = message.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
            var maxLine = 0;

            foreach (var line in lines)
            {
                var width = TextRenderer.MeasureText(line.Length == 0 ? " " : line, font).Width;
                if (width > maxLine)
                {
                    maxLine = width;
                }
            }

            var preferred = Math.Max(360, maxLine + 12);
            return Math.Min(maxWidth, preferred);
        }

        private static Button BuildButton(string text, DialogResult result)
        {
            return new Button
            {
                Text = text,
                DialogResult = result,
                Width = 92,
                Height = 34,
                Margin = new Padding(8, 0, 0, 0),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(38, 114, 236),
                ForeColor = Color.White,
                Font = new Font("맑은 고딕", 10, FontStyle.Bold),
                FlatAppearance = { BorderSize = 0 }
            };
        }

        private static string GetIconGlyph(MessageBoxIcon icon)
        {
            return icon switch
            {
                MessageBoxIcon.Error => "✕",
                MessageBoxIcon.Warning => "!",
                MessageBoxIcon.Question => "?",
                _ => "i"
            };
        }

        private static Color GetIconColor(MessageBoxIcon icon)
        {
            return icon switch
            {
                MessageBoxIcon.Error => Color.FromArgb(245, 94, 94),
                MessageBoxIcon.Warning => Color.FromArgb(255, 196, 85),
                MessageBoxIcon.Question => Color.FromArgb(122, 189, 255),
                _ => Color.FromArgb(122, 189, 255)
            };
        }
    }
}
