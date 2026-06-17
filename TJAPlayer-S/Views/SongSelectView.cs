using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using TjaPlayer.Gameplay;
using TjaPlayer.Audio;

namespace TjaPlayer.Views;

public class SongSelectView : UserControl, IAppState
{
    public AppStateEnum State => AppStateEnum.SongSelect;
    
    private AudioManager audioManager;
    private List<Score> songs;
    private int selectedIndex = 0;
    private float currentScrollIdx = 0f;

    public event Action<Score>? SongSelected;

    public SongSelectView(List<Score> songs, AudioManager audioManager)
    {
        this.songs = songs;
        this.audioManager = audioManager;
        Dock = DockStyle.Fill;
        DoubleBuffered = true;
        BackColor = Color.Black;
        
        TabStop = true;
        
        KeyDown += SongSelectView_KeyDown;
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        Focus();
    }

    private void SongSelectView_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.D && selectedIndex > 0)
        {
            selectedIndex--;
            audioManager.PlaySoundEffect(System.IO.Path.Combine("Theme", "default", "sound", "ka.wav"));
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.K && selectedIndex < songs.Count - 1)
        {
            selectedIndex++;
            audioManager.PlaySoundEffect(System.IO.Path.Combine("Theme", "default", "sound", "ka.wav"));
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.J || e.KeyCode == Keys.Enter)
        {
            audioManager.PlaySoundEffect(System.IO.Path.Combine("Theme", "default", "sound", "dong.wav"));
            SongSelected?.Invoke(songs[selectedIndex]);
            e.Handled = true;
        }
    }

    public new void Update()
    {
        float lerpSpeed = 10f;
        float deltaTime = 0.016f; // 仮のdeltaTime
        currentScrollIdx = currentScrollIdx + (selectedIndex - currentScrollIdx) * (deltaTime * lerpSpeed);
    }

    public void Render()
    {
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        Graphics g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        float centerY = Height / 2f;
        float baseBarHeight = 100f; // 高さを少し広げる

        for (int i = 0; i < songs.Count; i++)
        {
            float diff = i - currentScrollIdx;
            if (Math.Abs(diff) > 4.5f) continue;

            float y = centerY + (diff * baseBarHeight);
            float targetX = 100f + (float)Math.Pow(Math.Abs(diff), 1.5) * 45f;
            float scale = 1.0f - (Math.Min(Math.Abs(diff), 3f) * 0.08f);
            float alpha = 1.0f - (Math.Min(Math.Abs(diff), 4f) * 0.2f);
            bool isSelected = Math.Abs(diff) < 0.5f;

            DrawHijiriBar(g, targetX, y, scale, alpha, songs[i], isSelected);
        }
    }

    private void DrawHijiriBar(Graphics g, float x, float y, float scale, float alpha, Score song, bool isSelected)
    {
        int barWidth = isSelected ? 500 : 400;
        int barHeight = (int)(60 * scale);
        
        Color barColor = isSelected ? Color.Gold : song.GenreColor;
        using (Brush brush = new SolidBrush(Color.FromArgb((int)(alpha * 255), barColor)))
        {
            g.FillRectangle(brush, x, y - barHeight / 2f, barWidth * scale, barHeight);
        }

        using (Brush textBrush = new SolidBrush(Color.FromArgb((int)(alpha * 255), isSelected ? Color.White : song.FontColor)))
        using (Font font = new Font("Arial", isSelected ? 20 * scale : 16 * scale, FontStyle.Bold))
        {
            g.DrawString(song.Title, font, textBrush, x + 10, y - barHeight / 2f + 5);
        }
    }
}
