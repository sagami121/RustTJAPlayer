using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using TjaPlayer.Gameplay;
using TjaPlayer.Models;

namespace TjaPlayer.Views;

public class DifficultySelectView : UserControl, IAppState
{
    public AppStateEnum State => AppStateEnum.SongSelect;
    
    private Score score;
    private List<string> difficulties;
    private int selectedIndex = 0;
    private float currentScrollIdx = 0f;

    public event Action<TjaChart>? DifficultySelected;

    public DifficultySelectView(Score score)
    {
        this.score = score;
        this.difficulties = new List<string>(score.Charts.Keys);
        Dock = DockStyle.Fill;
        DoubleBuffered = true;
        BackColor = Color.Black;
        
        TabStop = true;
        KeyDown += DifficultySelectView_KeyDown;
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        Focus();
    }

    private void DifficultySelectView_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.D && selectedIndex > 0)
        {
            selectedIndex--;
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.K && selectedIndex < difficulties.Count - 1)
        {
            selectedIndex++;
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.J || e.KeyCode == Keys.Enter)
        {
            DifficultySelected?.Invoke(score.Charts[difficulties[selectedIndex]]);
            e.Handled = true;
        }
    }

    public new void Update()
    {
        float lerpSpeed = 10f;
        float deltaTime = 0.016f;
        currentScrollIdx = currentScrollIdx + (selectedIndex - currentScrollIdx) * (deltaTime * lerpSpeed);
    }

    public void Render() { Invalidate(); }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        Graphics g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        float centerY = Height / 2f;
        float baseBarHeight = 100f;

        for (int i = 0; i < difficulties.Count; i++)
        {
            float diff = i - currentScrollIdx;
            if (Math.Abs(diff) > 4.5f) continue;

            float y = centerY + (diff * baseBarHeight);
            float targetX = 100f + (float)Math.Pow(Math.Abs(diff), 1.5) * 45f;
            float scale = 1.0f - (Math.Min(Math.Abs(diff), 3f) * 0.08f);
            float alpha = 1.0f - (Math.Min(Math.Abs(diff), 4f) * 0.2f);
            bool isSelected = Math.Abs(diff) < 0.5f;

            DrawDiffBar(g, targetX, y, scale, alpha, difficulties[i], isSelected);
        }
    }

    private void DrawDiffBar(Graphics g, float x, float y, float scale, float alpha, string difficulty, bool isSelected)
    {
        int barWidth = isSelected ? 500 : 400;
        int barHeight = (int)(60 * scale);
        
        Color barColor = isSelected ? Color.DeepPink : Color.DimGray;
        using (Brush brush = new SolidBrush(Color.FromArgb((int)(alpha * 255), barColor)))
        {
            g.FillRectangle(brush, x, y - barHeight / 2f, barWidth * scale, barHeight);
        }

        using (Brush textBrush = new SolidBrush(Color.FromArgb((int)(alpha * 255), Color.White)))
        {
            Font font = new Font("Arial", isSelected ? 20 * scale : 16 * scale, FontStyle.Bold);
            g.DrawString(difficulty, font, textBrush, x + 10, y - barHeight / 2f + 5);
        }
    }
}
