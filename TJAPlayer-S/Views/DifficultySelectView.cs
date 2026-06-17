using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using TjaPlayer.Gameplay;
using TjaPlayer.Models;
using TjaPlayer.Audio;

namespace TjaPlayer.Views;

public class DifficultySelectView : UserControl, IAppState
{
    public AppStateEnum State => AppStateEnum.SongSelect;
    
    private readonly AudioManager audioManager;
    private readonly Score score;
    private readonly List<string> difficulties;
    private int selectedIndex = 0;
    private float currentScrollIdx = 0f;

    public event Action<TjaChart>? DifficultySelected;
    public event Action? RequestedExit;

    private bool showOptions = false;
    private int selectedOptionIndex = 0;
    private readonly string[] optionLabels = { "Mod", "ドロン", "スピード" };
    private readonly string[] modNames = { "なし", "あべこべ", "きまぐれ", "でたらめ" };

    private string GetOptionValueString(int index)
    {
        switch (index)
        {
            case 0: return modNames[(int)Utils.ConfigManager.CurrentNoteMod];
            case 1: return Utils.ConfigManager.IsDoron ? "オン" : "オフ";
            case 2: return Utils.ConfigManager.ScrollSpeed + "倍";
            default: return "";
        }
    }

    public DifficultySelectView(Score score, AudioManager audioManager)
    {
        this.score = score;
        this.audioManager = audioManager;
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
        if (showOptions)
        {
            if (e.KeyCode == Keys.Escape || e.KeyCode == Keys.E)
            {
                showOptions = false;
                audioManager.PlaySoundEffect(System.IO.Path.Combine("Theme", "default", "sound", "ka.wav"));
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.K && selectedOptionIndex < optionLabels.Length - 1)
            {
                selectedOptionIndex++;
                audioManager.PlaySoundEffect(System.IO.Path.Combine("Theme", "default", "sound", "ka.wav"));
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.D && selectedOptionIndex > 0)
            {
                selectedOptionIndex--;
                audioManager.PlaySoundEffect(System.IO.Path.Combine("Theme", "default", "sound", "ka.wav"));
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.J || e.KeyCode == Keys.Enter)
            {
                switch (selectedOptionIndex)
                {
                    case 0:
                        Utils.ConfigManager.CurrentNoteMod = (Utils.ConfigManager.NoteMod)(((int)Utils.ConfigManager.CurrentNoteMod + 1) % 4);
                        break;
                    case 1:
                        Utils.ConfigManager.IsDoron = !Utils.ConfigManager.IsDoron;
                        break;
                    case 2:
                        Utils.ConfigManager.ScrollSpeed = (Utils.ConfigManager.ScrollSpeed % 4) + 1;
                        break;
                }
                Utils.ConfigManager.Save();
                audioManager.PlaySoundEffect(System.IO.Path.Combine("Theme", "default", "sound", "dong.wav"));
                e.Handled = true;
            }
            return;
        }

        if (e.KeyCode == Keys.Escape)
        {
            RequestedExit?.Invoke();
            audioManager.PlaySoundEffect(System.IO.Path.Combine("Theme", "default", "sound", "ka.wav"));
            e.Handled = true;
            return;
        }

        if (e.KeyCode == Keys.E)
        {
            showOptions = true;
            audioManager.PlaySoundEffect(System.IO.Path.Combine("Theme", "default", "sound", "dong.wav"));
            e.Handled = true;
            return;
        }

        if (difficulties.Count == 0) return;

        if (e.KeyCode == Keys.D && selectedIndex > 0)
        {
            selectedIndex--;
            audioManager.PlaySoundEffect(System.IO.Path.Combine("Theme", "default", "sound", "ka.wav"));
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.K && selectedIndex < difficulties.Count - 1)
        {
            selectedIndex++;
            audioManager.PlaySoundEffect(System.IO.Path.Combine("Theme", "default", "sound", "ka.wav"));
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.J || e.KeyCode == Keys.Enter)
        {
            if (selectedIndex >= 0 && selectedIndex < difficulties.Count)
            {
                audioManager.PlaySoundEffect(System.IO.Path.Combine("Theme", "default", "sound", "dong.wav"));
                DifficultySelected?.Invoke(score.Charts[difficulties[selectedIndex]]);
                e.Handled = true;
            }
        }
    }

    public void Render()
    {
        Invalidate();
    }

    public new void Update()
    {
        float lerpSpeed = 10f;
        float deltaTime = 0.016f;
        currentScrollIdx = currentScrollIdx + (selectedIndex - currentScrollIdx) * (deltaTime * lerpSpeed);
    }

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

        // オプション画面の描画
        if (showOptions)
        {
            g.FillRectangle(new SolidBrush(Color.FromArgb(200, Color.Black)), 0, 0, Width, Height);
            g.DrawString("演奏オプション", new Font(Utils.FontManager.KantiryuFontFamily ?? FontFamily.GenericSansSerif, 30, FontStyle.Bold), Brushes.White, 300, 30);
            for (int i = 0; i < optionLabels.Length; i++)
            {
                string text = $"{optionLabels[i]}: {GetOptionValueString(i)}";
                g.DrawString(text, new Font(Utils.FontManager.KantiryuFontFamily ?? FontFamily.GenericSansSerif, 24), i == selectedOptionIndex ? Brushes.Yellow : Brushes.White, 300, 100 + i * 60);
            }
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
            Font font = new Font(Utils.FontManager.KantiryuFontFamily ?? FontFamily.GenericSansSerif, isSelected ? 20 * scale : 16 * scale, FontStyle.Bold);
            g.DrawString(difficulty, font, textBrush, x + 10, y - barHeight / 2f + 5);
        }
    }
}
