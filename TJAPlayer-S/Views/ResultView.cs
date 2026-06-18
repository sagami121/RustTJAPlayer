using System;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using TjaPlayer.Models;

namespace TjaPlayer.Views;

public class ResultView : UserControl, IAppState
{
    public AppStateEnum State => AppStateEnum.Playing; // 仮のリザルト状態
    private PlayResult result;
    public event Action? RequestedExit;

    // 描画キャッシュ
    private string songTitle;
    private string perfectText;
    private string goodText;
    private string missText;
    private string comboText;
    private string scoreText;

    public ResultView(PlayResult result)
    {
        this.result = result;
        this.songTitle = result.SongTitle;
        Dock = DockStyle.Fill;
        BackColor = Color.Black;
        DoubleBuffered = true;

        // 文字列キャッシュの生成
        perfectText = $"良: {result.PerfectCount}";
        goodText = $"可: {result.GoodCount}";
        missText = $"不可: {result.MissCount}";
        comboText = $"Max Combo: {result.MaxCombo}";
        scoreText = $"Total Score: {result.TotalScore}";

        KeyDown += (s, e) => {
            if (e.KeyCode == Keys.Escape || e.KeyCode == Keys.J || e.KeyCode == Keys.Enter)
                RequestedExit?.Invoke();
        };
        TabStop = true;
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        Focus();
    }

    public new void Update() { }
    public void Render() { Invalidate(); }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        Graphics g = e.Graphics;
        Utils.SkinManager.RenderBackground(g, Width, Height);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        // 曲タイトル表示 (右上に表示)
        Font titleFont = new Font(Utils.FontManager.KantiryuFontFamily, 20);
        SizeF titleSize = g.MeasureString(songTitle, titleFont);
        g.DrawString(songTitle, titleFont, Brushes.White, Width - titleSize.Width - 10, 10);
        
        g.DrawString(perfectText, new Font("Arial", 25), Brushes.Gold, 50, 150);
        g.DrawString(goodText, new Font("Arial", 25), Brushes.White, 50, 200);
        g.DrawString(missText, new Font("Arial", 25), Brushes.BlueViolet, 50, 250);
        
        g.DrawString(comboText, new Font("Arial", 25), Brushes.Cyan, 400, 150);
        g.DrawString(scoreText, new Font("Arial", 35, FontStyle.Bold), Brushes.White, 400, 250);
    }
}
