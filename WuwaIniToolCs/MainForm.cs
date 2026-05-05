using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;

namespace WuwaIniToolCs;

public sealed class MainForm : Form
{
    private const int LaunchNoticeDurationMs = 2000;

    private static readonly Color BgDark = Color.FromArgb(23, 26, 33);
    private static readonly Color BgPanel = Color.FromArgb(30, 35, 45);
    private static readonly Color BgInput = Color.FromArgb(36, 41, 53);
    private static readonly Color AccentBlue = Color.FromArgb(38, 114, 236);
    private static readonly Color AccentBlueHover = Color.FromArgb(52, 128, 248);
    private static readonly Color AccentBlueDown = Color.FromArgb(27, 96, 210);
    private static readonly Color BtnSecondary = Color.FromArgb(55, 63, 79);
    private static readonly Color BtnSecondaryHover = Color.FromArgb(68, 76, 94);
    private static readonly Color BtnSecondaryDown = Color.FromArgb(48, 56, 72);
    private static readonly Color BtnPreset = Color.FromArgb(45, 53, 68);
    private static readonly Color BtnPresetHover = Color.FromArgb(57, 66, 84);
    private static readonly Color BtnPresetDown = Color.FromArgb(40, 48, 63);
    private static readonly Color TextMain = Color.FromArgb(240, 243, 250);
    private static readonly Color TextSub = Color.FromArgb(176, 186, 203);

    private readonly TextBox _win64PathTextBox = new();
    private readonly CheckBox _rtCheckBox = new();
    private readonly TableLayoutPanel _presetPanel = new();
    private readonly Label _statusLabel = new();
    private Button? _selectedPresetButton;
    private readonly List<string> _specImagePaths = new();
    private NotifyIcon? _trayIcon;
    private ContextMenuStrip? _trayMenu;
    private bool _isHiddenToTray;
    private Icon _appIcon = SystemIcons.Application;
    private bool _ownsAppIcon;
    private bool _isPathSearchInProgress;

    public MainForm()
    {
        Text = "띵조 INI 딸깍툴";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(900, 740);
        Size = new Size(980, 820);
        BackColor = BgDark;
        ForeColor = TextMain;

        (_appIcon, _ownsAppIcon) = LoadAppIcon();
        Icon = _appIcon;

        CacheService.EnsureCacheFile();
        EnsurePresetStructure();

        BuildLayout();
        LoadInitialState();
        RefreshPresetButtons();

        FormClosing += (_, _) =>
        {
            DisposeTrayIcon();
            if (_ownsAppIcon)
            {
                _appIcon.Dispose();
                _ownsAppIcon = false;
            }
        };
    }

    private void SetSelectedPresetButton(Button button)
    {
        if (_selectedPresetButton is not null && !_selectedPresetButton.IsDisposed)
        {
            ApplyPresetSelectedStyle(_selectedPresetButton, false);
        }

        _selectedPresetButton = button;
        ApplyPresetSelectedStyle(button, true);
    }

    private void ClearSelectedPresetButton()
    {
        if (_selectedPresetButton is null || _selectedPresetButton.IsDisposed)
        {
            _selectedPresetButton = null;
            return;
        }

        ApplyPresetSelectedStyle(_selectedPresetButton, false);
        _selectedPresetButton = null;
    }

    private static void ApplyPresetSelectedStyle(Button button, bool isSelected)
    {
        if (isSelected)
        {
            button.BackColor = AccentBlue;
            button.FlatAppearance.BorderColor = AccentBlueHover;
            button.FlatAppearance.BorderSize = 2;
            button.FlatAppearance.MouseOverBackColor = AccentBlueHover;
            button.FlatAppearance.MouseDownBackColor = AccentBlueDown;
            return;
        }

        button.BackColor = BtnPreset;
        button.FlatAppearance.BorderColor = Color.FromArgb(66, 76, 96);
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.MouseOverBackColor = BtnPresetHover;
        button.FlatAppearance.MouseDownBackColor = BtnPresetDown;
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 6,
            Padding = new Padding(24, 20, 24, 18),
            BackColor = BgDark
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 88));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var topBar = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            Height = 80,
            Margin = new Padding(0, 0, 0, 8),
            BackColor = Color.Transparent
        };
        topBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 124));
        topBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        topBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 124));
        topBar.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var top = new Label
        {
            Text = "띵조 INI 딸깍툴",
            Font = new Font("맑은 고딕", 16, FontStyle.Bold),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = TextMain,
            BackColor = Color.Transparent
        };
        top.UseCompatibleTextRendering = true;
        top.Padding = new Padding(0, 4, 0, 0);

        var specButton = BuildUiButton("사양표");
        specButton.Size = new Size(108, 36);
        specButton.Dock = DockStyle.Fill;
        specButton.Font = new Font("맑은 고딕", 10, FontStyle.Bold);
        specButton.Margin = new Padding(12, 14, 0, 14);
        specButton.Click += (_, _) => OpenSpecChart();

        var customPathButton = BuildUiButton("커스텀 경로");
        customPathButton.Size = new Size(108, 36);
        customPathButton.Dock = DockStyle.Fill;
        customPathButton.Font = new Font("맑은 고딕", 10, FontStyle.Bold);
        customPathButton.Margin = new Padding(0, 14, 12, 14);
        customPathButton.Click += async (_, _) => await UseCustomConfigPathAsync();

        topBar.Controls.Add(customPathButton, 0, 0);
        topBar.Controls.Add(top, 1, 0);
        topBar.Controls.Add(specButton, 2, 0);
        root.Controls.Add(topBar, 0, 0);

        var pathCard = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            AutoSize = true,
            Padding = new Padding(14, 12, 14, 12),
            Margin = new Padding(0, 0, 0, 12),
            BackColor = BgPanel
        };
        pathCard.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        pathCard.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        pathCard.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        pathCard.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var pathTitle = new Label
        {
            Text = "대상 경로 (Win64 / WindowsNoEditor)",
            Font = new Font("맑은 고딕", 11, FontStyle.Regular),
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 6),
            ForeColor = TextMain,
            BackColor = Color.Transparent
        };
        pathCard.Controls.Add(pathTitle, 0, 0);

        _win64PathTextBox.Dock = DockStyle.Fill;
        _win64PathTextBox.Font = new Font("맑은 고딕", 11, FontStyle.Regular);
        _win64PathTextBox.Margin = new Padding(0, 0, 0, 10);
        _win64PathTextBox.Height = 38;
        _win64PathTextBox.BackColor = BgInput;
        _win64PathTextBox.ForeColor = TextMain;
        _win64PathTextBox.BorderStyle = BorderStyle.FixedSingle;
        pathCard.Controls.Add(_win64PathTextBox, 0, 1);

        var actionRow = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 8)
        };
        actionRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.34f));
        actionRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
        actionRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));

        var autoButton = BuildUiButton("자동 찾기");
        autoButton.Margin = new Padding(0, 0, 6, 0);
        autoButton.Click += async (_, _) => await AutoFindWin64Async();

        var chooseButton = BuildUiButton("폴더 선택");
        chooseButton.Margin = new Padding(6, 0, 6, 0);
        chooseButton.Click += (_, _) => SelectWin64Folder();

        var openButton = BuildUiButton("선택된 경로폴더 열기");
        openButton.Margin = new Padding(6, 0, 0, 0);
        openButton.Click += (_, _) => OpenSelectedFolder();

        actionRow.Controls.Add(autoButton, 0, 0);
        actionRow.Controls.Add(chooseButton, 1, 0);
        actionRow.Controls.Add(openButton, 2, 0);
        pathCard.Controls.Add(actionRow, 0, 2);

        var utilRow = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 0)
        };
        utilRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        utilRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        var cacheClearButton = BuildUiButton("캐시 제거");
        cacheClearButton.Height = 34;
        cacheClearButton.Margin = new Padding(0, 0, 6, 0);
        cacheClearButton.Click += (_, _) => ClearCache();

        var restoreButton = BuildUiButton("원래대로 복원");
        restoreButton.Height = 34;
        restoreButton.Margin = new Padding(6, 0, 0, 0);
        restoreButton.Click += (_, _) => RestoreDefault();

        utilRow.Controls.Add(cacheClearButton, 0, 0);
        utilRow.Controls.Add(restoreButton, 1, 0);
        pathCard.Controls.Add(utilRow, 0, 3);

        root.Controls.Add(pathCard, 0, 1);

        var optRow = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            AutoSize = true,
            Margin = new Padding(0, 6, 0, 12),
            BackColor = Color.Transparent
        };
        optRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        optRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var infoLabel = new Label
        {
            Text = "원하는 사양 버튼을 누르면 해당 프리셋 Engine.ini로 교체됩니다.",
            AutoSize = true,
            Dock = DockStyle.Fill,
            Font = new Font("맑은 고딕", 11, FontStyle.Regular),
            ForeColor = TextMain,
            TextAlign = ContentAlignment.MiddleLeft,
            BackColor = Color.Transparent
        };

        _rtCheckBox.Text = "Ray Tracing (RT) 사용";
        _rtCheckBox.AutoSize = true;
        _rtCheckBox.Font = new Font("맑은 고딕", 11, FontStyle.Bold);
        _rtCheckBox.Margin = new Padding(8, 0, 0, 0);
        _rtCheckBox.ForeColor = TextMain;
        _rtCheckBox.BackColor = Color.Transparent;
        _rtCheckBox.CheckedChanged += (_, _) =>
        {
            CacheService.SaveRtEnabled(_rtCheckBox.Checked);
            RefreshPresetButtons();
        };

        optRow.Controls.Add(infoLabel, 0, 0);
        optRow.Controls.Add(_rtCheckBox, 1, 0);
        root.Controls.Add(optRow, 0, 2);

        _presetPanel.Dock = DockStyle.Fill;
        _presetPanel.AutoScroll = true;
        _presetPanel.ColumnCount = 2;
        _presetPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        _presetPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        _presetPanel.GrowStyle = TableLayoutPanelGrowStyle.AddRows;
        _presetPanel.Padding = new Padding(6);
        _presetPanel.Margin = new Padding(0, 0, 0, 12);
        _presetPanel.BackColor = BgPanel;
        _presetPanel.CellBorderStyle = TableLayoutPanelCellBorderStyle.None;
        root.Controls.Add(_presetPanel, 0, 3);

        var launchButton = BuildPrimaryButton("게임 실행");
        launchButton.Click += (_, _) => LaunchGame();
        root.Controls.Add(launchButton, 0, 4);

        _statusLabel.Dock = DockStyle.Top;
        _statusLabel.Text = "오류가 발생하면 팝업 안내와 permission_debug_log.txt를 확인해 주세요.";
        _statusLabel.AutoSize = true;
        _statusLabel.ForeColor = TextSub;
        _statusLabel.Font = new Font("맑은 고딕", 10, FontStyle.Regular);
        _statusLabel.Margin = new Padding(0, 2, 0, 0);
        _statusLabel.BackColor = Color.Transparent;
        root.Controls.Add(_statusLabel, 0, 5);

        Controls.Add(root);
    }

    private void LoadInitialState()
    {
        _rtCheckBox.Checked = CacheService.GetCachedRtEnabled();

        var initialPath = AppPaths.DefaultWin64Folder();
        var cachedTargetDir = CacheService.GetCachedLastTargetDir();
        if (!string.IsNullOrWhiteSpace(cachedTargetDir))
        {
            initialPath = cachedTargetDir;
        }
        else
        {
            var autoFound = GamePathService.AutoFindWin64Folder();
            if (!string.IsNullOrWhiteSpace(autoFound))
            {
                initialPath = autoFound;
            }
        }

        _win64PathTextBox.Text = initialPath;
    }

    private void RefreshPresetButtons()
    {
        _presetPanel.SuspendLayout();
        _presetPanel.Controls.Clear();
        _presetPanel.RowStyles.Clear();
        _selectedPresetButton = null;

        var isRt = _rtCheckBox.Checked;
        var group = isRt ? "rt_on" : "rt_off";

        var presets = new List<(string Label, string Key)>
        {
            ("초고사양", "ultra"),
            ("고사양", "high"),
            ("중상옵", "mid_high"),
            ("중하옵", "mid_low")
        };

        if (!isRt)
        {
            presets.Add(("저사양", "low"));
            presets.Add(("초저사양", "very_low"));
        }

        var rowCount = (presets.Count + 1) / 2;
        _presetPanel.RowCount = rowCount;
        for (var row = 0; row < rowCount; row++)
        {
            _presetPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / rowCount));
        }

        for (var i = 0; i < presets.Count; i++)
        {
            var preset = presets[i];
            var text = $"{preset.Label} (RT {(isRt ? "ON" : "OFF")})";
            var folder = $"{group}/{preset.Key}";

            var button = BuildPresetButton(text);
            button.Click += (_, _) => ApplyPreset(text, folder, button);

            var row = i / 2;
            var col = i % 2;
            _presetPanel.Controls.Add(button, col, row);
        }

        _presetPanel.ResumeLayout();
    }

    private static Button BuildUiButton(string text)
    {
        var button = new Button
        {
            Text = text,
            Dock = DockStyle.Fill,
            Height = 36,
            Margin = Padding.Empty,
            Font = new Font("맑은 고딕", 11, FontStyle.Regular),
            BackColor = BtnSecondary,
            ForeColor = TextMain,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = BtnSecondaryHover;
        button.FlatAppearance.MouseDownBackColor = BtnSecondaryDown;
        return button;
    }

    private void OpenSpecChart()
    {
        EnsureSpecImagePaths();
        if (_specImagePaths.Count == 0)
        {
            PopupService.ShowWarning(
                "사양표 없음",
                "사양표 이미지를 찾지 못했습니다.\nassets 폴더에 .png/.jpg/.jpeg/.webp 파일을 넣어 주세요.",
                this);
            return;
        }

        var viewer = new Form
        {
            Text = "사양표",
            StartPosition = FormStartPosition.CenterParent,
            MinimumSize = new Size(640, 480),
            BackColor = BgDark
        };
        viewer.ClientSize = GetSpecInitialClientSize();

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(10),
            BackColor = BgDark
        };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var canvasPanel = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = BgPanel,
            BorderStyle = BorderStyle.FixedSingle
        };

        var picture = new PictureBox
        {
            SizeMode = PictureBoxSizeMode.StretchImage,
            BackColor = BgPanel,
            Cursor = Cursors.Hand
        };
        canvasPanel.Controls.Add(picture);

        var control = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            Height = 44,
            Margin = new Padding(0, 10, 0, 0)
        };
        control.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        control.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        control.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));

        var prev = BuildUiButton("이전");
        var next = BuildUiButton("다음");
        var page = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = TextMain,
            BackColor = Color.Transparent,
            Font = new Font("맑은 고딕", 7, FontStyle.Bold)
        };

        var index = 0;
        Bitmap? sourceImage = null;

        void RenderImage()
        {
            if (sourceImage is null)
            {
                return;
            }

            var viewportWidth = Math.Max(1, canvasPanel.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 6);
            var scale = viewportWidth / (double)sourceImage.Width;
            var width = Math.Max(1, viewportWidth);
            var height = Math.Max(1, (int)Math.Round(sourceImage.Height * scale));

            var old = picture.Image;
            picture.Image = new Bitmap(sourceImage, new Size(width, height));
            old?.Dispose();

            picture.Size = new Size(width, height);
            picture.Location = new Point(0, 0);

            page.Text = $"{index + 1} / {_specImagePaths.Count}   ({Path.GetFileName(_specImagePaths[index])})";
        }

        void LoadCurrentSource()
        {
            sourceImage?.Dispose();
            sourceImage = null;

            sourceImage = LoadImageForViewer(_specImagePaths[index]);
            RenderImage();
            canvasPanel.AutoScrollPosition = new Point(0, 0);
        }

        void ScrollByWheel(MouseEventArgs e)
        {
            var currentY = -canvasPanel.AutoScrollPosition.Y;
            var nextY = Math.Max(0, currentY - e.Delta);
            canvasPanel.AutoScrollPosition = new Point(0, nextY);
        }

        prev.Click += (_, _) =>
        {
            index = (index - 1 + _specImagePaths.Count) % _specImagePaths.Count;
            LoadCurrentSource();
        };
        next.Click += (_, _) =>
        {
            index = (index + 1) % _specImagePaths.Count;
            LoadCurrentSource();
        };

        picture.MouseWheel += (_, e) => ScrollByWheel(e);
        canvasPanel.MouseWheel += (_, e) => ScrollByWheel(e);

        canvasPanel.Resize += (_, _) =>
        {
            RenderImage();
        };

        control.Controls.Add(prev, 0, 0);
        control.Controls.Add(page, 1, 0);
        control.Controls.Add(next, 2, 0);

        root.Controls.Add(canvasPanel, 0, 0);
        root.Controls.Add(control, 0, 1);
        viewer.Controls.Add(root);

        if (_specImagePaths.Count <= 1)
        {
            prev.Enabled = false;
            next.Enabled = false;
        }

        LoadCurrentSource();
        viewer.ShowDialog(this);

        var last = picture.Image;
        picture.Image = null;
        last?.Dispose();
        sourceImage?.Dispose();
    }

    private void EnsureSpecImagePaths()
    {
        _specImagePaths.Clear();
        if (!Directory.Exists(AppPaths.AssetsDir))
        {
            return;
        }

        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".png", ".jpg", ".jpeg", ".webp"
        };

        foreach (var file in Directory.EnumerateFiles(AppPaths.AssetsDir))
        {
            if (!allowed.Contains(Path.GetExtension(file)))
            {
                continue;
            }

            _specImagePaths.Add(file);
        }

        _specImagePaths.Sort(StringComparer.OrdinalIgnoreCase);
    }

    private Size GetSpecInitialClientSize()
    {
        if (_specImagePaths.Count == 0)
        {
            return new Size(980, 760);
        }

        var preferred = _specImagePaths.Find(static p =>
            string.Equals(Path.GetFileNameWithoutExtension(p), "2", StringComparison.OrdinalIgnoreCase))
            ?? _specImagePaths[0];

        try
        {
            var info = SixLabors.ImageSharp.Image.Identify(preferred);
            if (info is null)
            {
                return new Size(980, 760);
            }

            var desiredW = info.Width + 24;
            var desiredH = info.Height + 84;

            var area = Screen.FromControl(this).WorkingArea;
            var maxW = Math.Max(640, area.Width - 80);
            var maxH = Math.Max(480, area.Height - 80);

            var finalW = Math.Clamp(desiredW, 640, maxW);
            var finalH = Math.Clamp(desiredH, 480, maxH);
            return new Size(finalW, finalH);
        }
        catch
        {
            return new Size(980, 760);
        }
    }

    private static Bitmap LoadImageForViewer(string path)
    {
        using var image = SixLabors.ImageSharp.Image.Load<Rgba32>(path);
        using var ms = new MemoryStream();
        image.Save(ms, new PngEncoder());
        ms.Position = 0;
        using var temp = new Bitmap(ms);
        return new Bitmap(temp);
    }

    private static Button BuildPrimaryButton(string text)
    {
        var button = new Button
        {
            Text = text,
            Dock = DockStyle.Top,
            Height = 42,
            Font = new Font("맑은 고딕", 13, FontStyle.Bold),
            BackColor = AccentBlue,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Margin = new Padding(0, 0, 0, 10),
            Cursor = Cursors.Hand
        };
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = AccentBlueHover;
        button.FlatAppearance.MouseDownBackColor = AccentBlueDown;
        return button;
    }

    private static Button BuildPresetButton(string text)
    {
        var button = new Button
        {
            Text = text,
            Dock = DockStyle.Fill,
            Height = 60,
            Margin = new Padding(10),
            Font = new Font("맑은 고딕", 13, FontStyle.Bold),
            BackColor = BtnPreset,
            ForeColor = TextMain,
            FlatStyle = FlatStyle.Flat,
            TextAlign = ContentAlignment.MiddleCenter,
            AutoSize = false,
            Cursor = Cursors.Hand
        };
        button.FlatAppearance.BorderColor = Color.FromArgb(66, 76, 96);
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.MouseOverBackColor = BtnPresetHover;
        button.FlatAppearance.MouseDownBackColor = BtnPresetDown;
        return button;
    }

    private async Task AutoFindWin64Async()
    {
        var found = await RunPathSearchAsync(
            "Win64 경로를 탐색 중입니다. (1차: 빠른 탐색, 2차: 정밀 탐색)",
            GamePathService.FindWin64FolderWithFallbackDepth);
        if (string.IsNullOrWhiteSpace(found))
        {
            PopupService.ShowWarning("안내", "자동으로 Win64 폴더를 찾지 못했습니다.\n폴더 선택으로 직접 지정해 주세요.", this);
            return;
        }

        SetWin64AndCache(found);
        PopupService.ShowInfo("완료", $"Win64 폴더를 찾았습니다.\n\n{found}", this);
    }

    private void SelectWin64Folder()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Win64 폴더를 선택하세요"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        SetWin64AndCache(dialog.SelectedPath);
    }

    private async Task UseCustomConfigPathAsync()
    {
        PopupService.ShowWarning(
            "안내",
            "해당 경로는 구글플레이, 모드 유저를 위한 경로입니다.\n우회실행이 필요한 일부 명령어가 동작하지않습니다.",
            this);

        var found = await RunPathSearchAsync(
            "커스텀 경로를 탐색 중입니다. (1차: 빠른 탐색, 2차: 정밀 탐색)",
            GamePathService.FindCustomConfigFolderWithFallbackDepth);
        if (string.IsNullOrWhiteSpace(found))
        {
            PopupService.ShowWarning("안내", "자동으로 WindowsNoEditor 경로를 찾지 못했습니다.\n폴더 선택으로 직접 지정해 주세요.", this);
            return;
        }

        SetWin64AndCache(found);
        PopupService.ShowInfo("완료", $"커스텀 경로를 찾았습니다.\n\n{found}", this);
    }

    private async Task<string?> RunPathSearchAsync(string searchingStatusText, Func<string?> searchFunc)
    {
        if (_isPathSearchInProgress)
        {
            PopupService.ShowWarning("안내", "이미 경로 탐색이 진행 중입니다.\n잠시만 기다려 주세요.", this);
            return null;
        }

        _isPathSearchInProgress = true;
        var previousStatus = _statusLabel.Text;
        var previousCursor = Cursor;
        Cursor = Cursors.WaitCursor;
        UseWaitCursor = true;
        _statusLabel.Text = searchingStatusText;

        try
        {
            return await Task.Run(searchFunc);
        }
        finally
        {
            UseWaitCursor = false;
            Cursor = previousCursor;
            _statusLabel.Text = previousStatus;
            _isPathSearchInProgress = false;
        }
    }

    private void OpenSelectedFolder()
    {
        var dir = _win64PathTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(dir))
        {
            PopupService.ShowWarning("안내", "먼저 대상 경로를 지정해 주세요.", this);
            return;
        }

        try
        {
            AppPaths.OpenFolder(dir);
        }
        catch (Exception ex)
        {
            PopupService.ShowError("오류", $"폴더 열기에 실패했습니다.\n\n{ex.Message}", this);
        }
    }

    private void LaunchGame()
    {
        try
        {
            var prepared = PrepareWin64AndIni(autoDetect: true, ensureIni: false);
            var win64Dir = prepared?.Win64Dir;
            var targetIni = prepared?.TargetIni;

            if (AppPaths.IsValidCustomConfigFolder(win64Dir ?? string.Empty))
            {
                PopupService.ShowWarning(
                    "안내",
                    "해당 경로는 구글플레이/모드 유저가 사용하는 경로입니다.\n구글플레이/모드 유저는 우회 실행을 사용할 수 없습니다.",
                    this);
                return;
            }

            var gameExe = GamePathService.FindGameExe(win64Dir);
            if (string.IsNullOrWhiteSpace(gameExe))
            {
                PopupService.ShowError("오류", "게임 실행 파일을 찾지 못했습니다.\n자동 탐색 또는 폴더 선택으로 Win64 폴더 경로를 먼저 정확히 지정해 주세요.", this);
                return;
            }

            var resolvedWin64 = AppPaths.GameExeToWin64(gameExe);
            if (!string.IsNullOrWhiteSpace(resolvedWin64))
            {
                win64Dir = resolvedWin64;
                SetWin64AndCache(resolvedWin64);
            }

            win64Dir ??= Path.GetDirectoryName(gameExe) ?? string.Empty;
            targetIni = AppPaths.Win64ToEngineIni(win64Dir);

            if (!File.Exists(targetIni))
            {
                PopupService.ShowWarning("안내", "Engine.ini 파일이 없습니다.\n먼저 원하는 사양 프리셋을 적용한 뒤 다시 게임 실행을 눌러 주세요.", this);
                return;
            }

            var needsAdmin = ElevationService.IsProtectedPath(targetIni);
            if (ElevationService.EnsureAdminIfNeeded(
                    needsAdmin,
                    () => ElevationService.RelaunchAsAdmin("--elevated-launch", QuoteArg(targetIni), QuoteArg(gameExe))))
            {
                return;
            }

            EngineIniService.CleanupBeforeLaunch(targetIni);
            Process.Start(new ProcessStartInfo
            {
                FileName = gameExe,
                Arguments = EngineIniService.BuildLaunchArgs(Path.GetFileName(targetIni)),
                WorkingDirectory = Path.GetDirectoryName(gameExe) ?? AppPaths.AppBaseDir,
                UseShellExecute = true,
                Verb = "open"
            });

            AutoCloseNotice.Show("실행", $"게임 실행을 요청했습니다.\n\n실행 파일:\n{gameExe}", LaunchNoticeDurationMs, this);
            HideToTray();
        }
        catch (UnauthorizedAccessException ex)
        {
            var targetIni = AppPaths.Win64ToEngineIni(_win64PathTextBox.Text.Trim());

            if (!ElevationService.IsAdmin())
            {
                var gameExe = GamePathService.FindGameExe(_win64PathTextBox.Text.Trim());
                if (!string.IsNullOrWhiteSpace(gameExe) && File.Exists(targetIni))
                {
                    if (ElevationService.EnsureAdminIfNeeded(
                            true,
                            () => ElevationService.RelaunchAsAdmin("--elevated-launch", QuoteArg(targetIni), QuoteArg(gameExe))))
                    {
                        return;
                    }
                }
            }

            AppLogger.LogDetail("game_launch_ini_permission_error", new Dictionary<string, string?>
            {
                ["target_ini"] = targetIni,
                ["exception_type"] = ex.GetType().Name,
                ["exception"] = ex.ToString()
            });

            PopupService.ShowError(
                "Engine.ini 쓰기 실패",
                $"게임 실행 전 Engine.ini를 수정하지 못했습니다.\n\n대상 파일:\n{targetIni}\n\n파일이 읽기 전용이거나 다른 프로세스가 사용 중인지 확인해 주세요.",
                this);
        }
        catch (Exception ex)
        {
            AppLogger.LogDetail("game_launch_error", new Dictionary<string, string?>
            {
                ["exception_type"] = ex.GetType().Name,
                ["exception"] = ex.ToString()
            });
            PopupService.ShowError("오류", $"게임 실행 중 문제가 발생했습니다.\n\n{ex.Message}", this);
        }
    }

    private void ApplyPreset(string presetName, string presetFolder, Button clickedButton)
    {
        var sourceIni = Path.Combine(AppPaths.PresetsDir, presetFolder.Replace('/', Path.DirectorySeparatorChar), "engine.ini");
        var groupKey = presetFolder[..presetFolder.LastIndexOf('/')];
        var presetKey = presetFolder[(presetFolder.LastIndexOf('/') + 1)..];

        if (!File.Exists(sourceIni))
        {
            PopupService.ShowError("오류", $"프리셋 파일을 찾을 수 없습니다.\n\n파일: {sourceIni}", this);
            return;
        }

        var prepared = PrepareWin64AndIni(autoDetect: true, ensureIni: false);
        if (prepared is null)
        {
            PopupService.ShowError("오류", "대상 경로를 찾지 못했습니다.\n먼저 [자동 찾기] 또는 [커스텀 경로] 버튼으로 경로를 지정해 주세요.", this);
            return;
        }

        var confirmMessage = $"선택한 사양: [{presetName}]\n\n아래 경로에 실제로 적용할까요?\n{prepared.Value.TargetIni}";
        if (AppConstants.TryGetPresetRecommendedSpec(groupKey, presetKey, out var spec))
        {
            confirmMessage += $"\n\n【권장 사양】\n{spec}\n\n※ 권장 사양은 참고용입니다.";
        }

        if (PopupService.ConfirmYesNo("적용 확인", confirmMessage, this) != DialogResult.Yes)
        {
            return;
        }

        SetSelectedPresetButton(clickedButton);

        try
        {
            if (ElevationService.EnsureAdminIfNeeded(
                    ElevationService.IsProtectedPath(prepared.Value.TargetIni),
                    () => ElevationService.RelaunchAsAdmin("--elevated-copy", QuoteArg(sourceIni), QuoteArg(prepared.Value.TargetIni), QuoteArg(presetName))))
            {
                return;
            }

            AppPaths.EnsureEngineIni(prepared.Value.TargetIni);
            var backupPath = EngineIniService.CreateBackup(prepared.Value.TargetIni, presetName);

            var copied = EngineIniService.TryCopyPresetIniWithRetry(
                sourceIni,
                prepared.Value.TargetIni,
                out var copyError,
                out var copyAttempts);

            if (!copied)
            {
                AppLogger.LogDetail("preset_apply_copy_retry_failed", new Dictionary<string, string?>
                {
                    ["source_ini"] = sourceIni,
                    ["target_ini"] = prepared.Value.TargetIni,
                    ["preset_name"] = presetName,
                    ["attempts"] = copyAttempts.ToString(),
                    ["exception_type"] = copyError?.GetType().Name,
                    ["exception"] = copyError?.ToString()
                });

                PopupService.ShowError(
                    "권한 오류",
                    $"파일 쓰기 권한이 없거나 파일이 사용 중입니다.\n"
                    + $"{copyAttempts}회 재시도했지만 적용에 실패했습니다.\n"
                    + "게임/런처를 완전히 종료한 뒤 다시 시도해 주세요.\n"
                    + "백신/랜섬웨어 보호의 차단 기록도 함께 확인해 주세요.\n\n"
                    + $"대상 파일:\n{prepared.Value.TargetIni}\n\n"
                    + $"상세 오류:\n{copyError?.Message}",
                    this);
                return;
            }

            var backupSummary = File.Exists(backupPath)
                ? $"백업 파일:\n{backupPath}"
                : "백업할 기존 Engine.ini가 없어 백업은 건너뛰었습니다.";

            PopupService.ShowInfo(
                "완료",
                $"[{presetName}] 적용이 완료되었습니다.\n\n대상 파일:\n{prepared.Value.TargetIni}\n\n{backupSummary}",
                this);

            var savedDir = ResolveSavedDir(prepared.Value.Win64Dir);
            if (PopupService.ConfirmYesNo(
                    "캐시 제거 권장",
                    $"캐시 적중 이슈를 방지하기 위해 캐시 제거를 권장합니다.\n"
                    + "지금 캐시(PSO/PSOReport + 셰이더 캐시)를 제거할까요?\n\n"
                    + $"기준 Saved 경로:\n{savedDir}",
                    this) == DialogResult.Yes)
            {
                var result = EngineIniService.DeleteCacheFolders(savedDir);
                PopupService.ShowInfo("완료", result, this);
                PopupService.ShowCacheRebuildNotice(this);
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            AppLogger.LogDetail("preset_apply_permission_error", new Dictionary<string, string?>
            {
                ["source_ini"] = sourceIni,
                ["target_ini"] = prepared.Value.TargetIni,
                ["preset_name"] = presetName,
                ["exception_type"] = ex.GetType().Name,
                ["exception"] = ex.ToString()
            });

            PopupService.ShowError(
                "권한 오류",
                $"파일 쓰기 권한이 없거나 파일이 다른 프로세스에서 사용 중입니다.\n게임/런처를 완전히 종료한 뒤 다시 시도해 주세요.\n\n대상 파일:\n{prepared.Value.TargetIni}\n\n상세 오류:\n{ex.Message}",
                this);
        }
        catch (Exception ex)
        {
            AppLogger.LogDetail("preset_apply_error", new Dictionary<string, string?>
            {
                ["source_ini"] = sourceIni,
                ["target_ini"] = prepared.Value.TargetIni,
                ["preset_name"] = presetName,
                ["exception_type"] = ex.GetType().Name,
                ["exception"] = ex.ToString()
            });
            PopupService.ShowError("오류", $"적용 중 문제가 발생했습니다.\n\n{ex.Message}", this);
        }
        finally
        {
            ClearSelectedPresetButton();
        }
    }

    private void ClearCache()
    {
        var prepared = PrepareWin64AndIni(autoDetect: true, ensureIni: false);
        if (prepared is null)
        {
            PopupService.ShowError("오류", "대상 경로를 찾지 못했습니다.\n먼저 [자동 찾기] 또는 [커스텀 경로] 버튼으로 경로를 지정해 주세요.", this);
            return;
        }

        var savedDir = ResolveSavedDir(prepared.Value.Win64Dir);
        if (PopupService.ConfirmYesNo(
                "캐시 제거 확인",
                $"아래 작업을 진행할까요?\n- 게임 캐시: PSO, PSOReport\n- 그래픽 셰이더 캐시\n\n기준 Saved 경로:\n{savedDir}",
                this) != DialogResult.Yes)
        {
            return;
        }

        if (ElevationService.EnsureAdminIfNeeded(
                ElevationService.IsProtectedPath(Path.Combine(savedDir, "PSO")),
                () => ElevationService.RelaunchAsAdmin("--elevated-clear-cache", QuoteArg(savedDir))))
        {
            return;
        }

        try
        {
            var result = EngineIniService.DeleteCacheFolders(savedDir);
            PopupService.ShowInfo("완료", result, this);
            PopupService.ShowCacheRebuildNotice(this);
        }
        catch (Exception ex)
        {
            PopupService.ShowError("오류", $"캐시 제거 중 문제가 발생했습니다.\n\n{ex.Message}", this);
        }
    }

    private void RestoreDefault()
    {
        var win64 = _win64PathTextBox.Text.Trim();
        if (!AppPaths.IsValidTargetIniFolder(win64))
        {
            PopupService.ShowError("오류", "대상 경로를 찾지 못했습니다.\n먼저 [자동 찾기] 또는 [커스텀 경로] 버튼으로 경로를 지정해 주세요.", this);
            return;
        }

        var targetIni = AppPaths.TargetFolderToEngineIni(win64);
        if (!File.Exists(targetIni))
        {
            PopupService.ShowWarning("안내", "삭제할 Engine.ini 파일이 없습니다.\n경로를 확인해 주세요.", this);
            return;
        }

        if (PopupService.ConfirmYesNo("순정복원 확인", $"현재 지정된 Engine.ini 파일을 삭제합니다.\n\n대상 파일:\n{targetIni}\n\n삭제를 진행할까요?", this) != DialogResult.Yes)
        {
            return;
        }

        if (ElevationService.EnsureAdminIfNeeded(
                ElevationService.IsProtectedPath(targetIni),
                () => ElevationService.RelaunchAsAdmin("--elevated-delete-ini", QuoteArg(targetIni))))
        {
            return;
        }

        try
        {
            File.Delete(targetIni);
            PopupService.ShowInfo("완료", $"Engine.ini를 삭제했습니다.\n\n{targetIni}", this);
        }
        catch (Exception ex)
        {
            PopupService.ShowError("오류", $"순정복원 중 문제가 발생했습니다.\n\n{ex.Message}", this);
        }
    }

    private void SetWin64AndCache(string win64Dir)
    {
        _win64PathTextBox.Text = win64Dir;

        if (AppPaths.IsValidTargetIniFolder(win64Dir))
        {
            CacheService.SaveLastTargetDir(win64Dir);
        }

        if (AppPaths.IsValidWin64Folder(win64Dir))
        {
            var gameExe = AppPaths.Win64ToGameExe(win64Dir);
            if (!string.IsNullOrWhiteSpace(gameExe))
            {
                CacheService.SaveGameExePath(gameExe);
            }
        }
    }

    private (string Win64Dir, string TargetIni)? PrepareWin64AndIni(bool autoDetect, bool ensureIni)
    {
        var win64 = _win64PathTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(win64) && autoDetect)
        {
            var found = GamePathService.FindWin64FolderWithFallbackDepth();
            if (!string.IsNullOrWhiteSpace(found))
            {
                SetWin64AndCache(found);
                win64 = found;
            }
        }

        if (!AppPaths.IsValidTargetIniFolder(win64))
        {
            return null;
        }

        var targetIni = AppPaths.TargetFolderToEngineIni(win64);
        if (ensureIni)
        {
            AppPaths.EnsureEngineIni(targetIni);
        }

        SetWin64AndCache(win64);
        return (win64, targetIni);
    }

    private static string ResolveSavedDir(string targetDir)
    {
        if (AppPaths.IsValidCustomConfigFolder(targetDir))
        {
            return Directory.GetParent(Directory.GetParent(targetDir)?.FullName ?? targetDir)?.FullName ?? targetDir;
        }

        return Path.Combine(Directory.GetParent(targetDir)?.Parent?.FullName ?? targetDir, "Saved");
    }

    private static string QuoteArg(string arg)
    {
        return arg.Contains(' ') ? $"\"{arg}\"" : arg;
    }

    private bool EnsureTrayIcon()
    {
        if (_trayIcon is not null)
        {
            return true;
        }

        try
        {
            _trayMenu = new ContextMenuStrip();
            var restoreItem = new ToolStripMenuItem("창 복원");
            restoreItem.Click += (_, _) => RestoreFromTray();

            var exitItem = new ToolStripMenuItem("종료");
            exitItem.Click += (_, _) =>
            {
                DisposeTrayIcon();
                Close();
            };

            _trayMenu.Items.Add(restoreItem);
            _trayMenu.Items.Add(exitItem);

            _trayIcon = new NotifyIcon
            {
                Text = "띵조 INI 딸깍툴",
                Icon = _appIcon,
                ContextMenuStrip = _trayMenu,
                Visible = true
            };
            _trayIcon.DoubleClick += (_, _) => RestoreFromTray();
            return true;
        }
        catch
        {
            DisposeTrayIcon();
            return false;
        }
    }

    private void HideToTray()
    {
        if (_isHiddenToTray)
        {
            return;
        }

        if (!EnsureTrayIcon())
        {
            return;
        }

        ShowInTaskbar = false;
        Hide();
        _isHiddenToTray = true;
    }

    private void RestoreFromTray()
    {
        if (!_isHiddenToTray)
        {
            return;
        }

        ShowInTaskbar = true;
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
        _isHiddenToTray = false;
        DisposeTrayIcon();
    }

    private void DisposeTrayIcon()
    {
        if (_trayIcon is not null)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            _trayIcon = null;
        }

        if (_trayMenu is not null)
        {
            _trayMenu.Dispose();
            _trayMenu = null;
        }

        _isHiddenToTray = false;
    }

    private static (Icon Icon, bool Owned) LoadAppIcon()
    {
        try
        {
            var iconPath = Path.Combine(AppPaths.AssetsDir, "app.ico");
            if (File.Exists(iconPath))
            {
                return (new Icon(iconPath), true);
            }
        }
        catch
        {
        }

        return (SystemIcons.Application, false);
    }

    private static void EnsurePresetStructure()
    {
        Directory.CreateDirectory(AppPaths.PresetsDir);

        var groups = new (string GroupName, string[] Keys, string RtValue)[]
        {
            ("rt_off", AppConstants.PresetKeysOff, "0"),
            ("rt_on", AppConstants.PresetKeysOn, "1")
        };

        foreach (var group in groups)
        {
            var groupDir = Path.Combine(AppPaths.PresetsDir, group.GroupName);
            Directory.CreateDirectory(groupDir);

            foreach (var key in group.Keys)
            {
                var folder = Path.Combine(groupDir, key);
                Directory.CreateDirectory(folder);

                var iniPath = Path.Combine(folder, "engine.ini");
                if (!File.Exists(iniPath))
                {
                    var template =
                        "; 프리셋 파일\n" +
                        "; 이 파일은 UTF-8로 저장해 주세요.\n\n" +
                        "[/Script/Engine.RendererSettings]\n" +
                        "r.ScreenPercentage=100\n" +
                        $"r.RayTracing.LoadConfig={group.RtValue}\n";
                    File.WriteAllText(iniPath, template);
                }
            }
        }
    }
}
