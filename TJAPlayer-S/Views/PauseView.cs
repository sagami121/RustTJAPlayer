using System;
using System.Drawing;
using System.Windows.Forms;
using TjaPlayer.Audio;
using TjaPlayer.Gameplay;
using TjaPlayer.Models;
using ManagedBass;

namespace TjaPlayer.Views;

public class PauseView : UserControl, IAppState
{
    public AppStateEnum State => AppStateEnum.Playing; // Keep as Playing state logically

    private readonly AudioManager audioManager;
    private readonly TjaChart chart;
    private readonly string songTitle;
    private readonly int scoreInit;
    private readonly int scoreDiff;
    private readonly int audioStream;
    private readonly double pauseTimeMs; // Chart time when paused
    private readonly ScoringSystem scoringSystem;
    private readonly JudgmentSystem judgmentSystem;

    // Events to communicate back to MainForm
    public event Action? ResumeRequested;
    public event Action? RestartRequested;
    public event Action? ExitToSongSelectRequested;

    // Menu state
    private int selectedIndex = 0;
    private readonly string[] menuOptions = { "Resume", "Restart", "Quit to Song Select" };

    public PauseView(
        AudioManager audioManager,
        TjaChart chart,
        string songTitle,
        int scoreInit,
        int scoreDiff,
        int audioStream,
        double currentChartTimeMs,
        ScoringSystem scoringSystem,
        JudgmentSystem judgmentSystem)
    {
        this.audioManager = audioManager;
        this.chart = chart;
        this.songTitle = songTitle;
        this.scoreInit = scoreInit;
        this.scoreDiff = scoreDiff;
        this.audioStream = audioStream;
        this.pauseTimeMs = currentChartTimeMs;
        this.scoringSystem = scoringSystem;
        this.judgmentSystem = judgmentSystem;

        Dock = DockStyle.Fill;
        BackColor = Color.Black;
        DoubleBuffered = true;

        TabStop = true;
        KeyDown += PauseView_KeyDown;

        // Pause the audio when entering this state
        if (audioStream != 0)
        {
            Bass.ChannelPause(audioStream);
        }
    }

    private void PauseView_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape)
        {
            // ESC from pause menu exits to song select
            audioManager.PlaySoundEffect(System.IO.Path.Combine("Theme", "default", "sound", "ka.wav"));
            ExitToSongSelectRequested?.Invoke();
            return;
        }

        if (e.KeyCode == Keys.K && selectedIndex < menuOptions.Length - 1)
        {
            selectedIndex++;
            audioManager.PlaySoundEffect(System.IO.Path.Combine("Theme", "default", "sound", "ka.wav"));
            e.Handled = true;
            return;
        }

        if (e.KeyCode == Keys.D && selectedIndex > 0)
        {
            selectedIndex--;
            audioManager.PlaySoundEffect(System.IO.Path.Combine("Theme", "default", "sound", "ka.wav"));
            e.Handled = true;
            return;
        }

        if (e.KeyCode == Keys.J || e.KeyCode == Keys.Enter)
        {
            audioManager.PlaySoundEffect(System.IO.Path.Combine("Theme", "default", "sound", "dong.wav"));
            HandleMenuSelection();
            e.Handled = true;
            return;
        }
    }

    private void HandleMenuSelection()
    {
        switch (selectedIndex)
        {
            case 0: // Resume
                ResumeRequested?.Invoke();
                break;
            case 1: // Restart
                RestartRequested?.Invoke();
                break;
            case 2: // Quit to Song Select
                ExitToSongSelectRequested?.Invoke();
                break;
        }
    }

    public new void Update()
    {
        // No updates needed while paused
    }

    public void Render()
    {
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        Graphics g = e.Graphics;
        Utils.SkinManager.RenderBackground(g, Width, Height);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        // Draw semi-transparent overlay
        using (Brush overlayBrush = new SolidBrush(Color.FromArgb(180, Color.Black)))
        {
            g.FillRectangle(overlayBrush, 0, 0, Width, Height);
        }

        // Draw title
        using (Font titleFont = new Font(Utils.FontManager.KantiryuFontFamily, 32, FontStyle.Bold))
        {
            SizeF titleSize = g.MeasureString("PAUSED", titleFont);
            g.DrawString("PAUSED", titleFont, Brushes.Yellow, (Width - titleSize.Width) / 2, Height / 3);
        }

        // Draw menu options
        float menuStartY = Height / 2;
        float menuItemHeight = 40;
        float menuItemSpacing = 10;

        for (int i = 0; i < menuOptions.Length; i++)
        {
            bool isSelected = (i == selectedIndex);
            using (Font menuFont = new Font(Utils.FontManager.KantiryuFontFamily, 24, isSelected ? FontStyle.Bold : FontStyle.Regular))
            {
                Color menuColor = isSelected ? Color.White : Color.Gray;

                SizeF itemSize = g.MeasureString(menuOptions[i], menuFont);
                float x = (Width - itemSize.Width) / 2;
                float y = menuStartY + i * (menuItemHeight + menuItemSpacing);

                g.DrawString(menuOptions[i], menuFont, new SolidBrush(menuColor), x, y);
            }
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Resume audio when leaving pause state (if not already resumed by caller)
            // Actually, the caller (MainForm) should handle resuming audio when switching back to gameplay
            // So we don't do it here to avoid double-resume issues
        }
        base.Dispose(disposing);
    }
}