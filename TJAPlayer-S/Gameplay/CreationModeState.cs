using System;
using System.Drawing;
using System.Windows.Forms;
using TjaPlayer.Models;
using TjaPlayer.Utils;

namespace TjaPlayer.Gameplay;

public class CreationModeState : UserControl, IAppState
{
    public AppStateEnum State => AppStateEnum.Playing;

    private int _targetMeasure = 0;
    private double _currentMeasure = 0.0;
    private int _totalMeasures = 100;
    private DateTime _startTime = DateTime.Now;

    public CreationModeState()
    {
        this.Dock = DockStyle.Fill;
        this.BackColor = Color.FromArgb(128, 0, 0, 0); // 半透明の黒背景
    }

    public new void Update()
    {
        // 簡易的な入力処理
        _currentMeasure = Easing.Lerp(_currentMeasure, _targetMeasure, 0.1);
    }

    public void Render()
    {
        this.Invalidate(); // OnPaintをトリガー
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        Graphics g = e.Graphics;
        
        // デバッグ用: 背景を描画
        g.Clear(Color.FromArgb(128, 0, 0, 255)); // 青背景でデバッグ

        // ConfigManager の設定を反映
        if (ConfigManager.CreationMode_ShowMeasure)
        {
            string measureText = $"MEASURE: {(int)_currentMeasure:D3} / {_totalMeasures:D3}";
            g.DrawString(measureText, this.Font, Brushes.White, 100, 50);
        }

        // PRESS SPACE KEY の点滅
        double time = (DateTime.Now - _startTime).TotalSeconds;
        double t = (Math.Sin(time * 2) + 1) / 2.0;
        double alpha = Easing.EaseInOut(t);

        using (Brush brush = new SolidBrush(Color.FromArgb((int)(alpha * 255), 255, 255, 0)))
        {
            g.DrawString("PRESS SPACE KEY", new Font(this.Font.FontFamily, 20, FontStyle.Bold), brush, 300, 300);
        }
    }
}
