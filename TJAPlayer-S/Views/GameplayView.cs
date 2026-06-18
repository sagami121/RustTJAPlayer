using System.Windows.Forms;
using System.Collections.Generic;
using System.IO;
using System;
using System.Drawing;
using System.Linq;
using System.Numerics;
using TjaPlayer;
using TjaPlayer.Audio;
using TjaPlayer.Gameplay;
using TjaPlayer.Models;

namespace TjaPlayer.Views;

public class GameplayView : UserControl, IAppState
{
    public AppStateEnum State => AppStateEnum.Playing;

    private readonly AudioManager audioManager;
    private readonly ScoringSystem scoringSystem;
    private readonly JudgmentSystem judgmentSystem;
    private readonly int audioStream;
    private readonly TjaChart chart;
    private readonly string songTitle;
    
    // 演奏オプション
    private Utils.ConfigManager.NoteMod noteMod = Utils.ConfigManager.CurrentNoteMod;
    private bool isDoron = Utils.ConfigManager.IsDoron;
    private int scrollSpeed = Utils.ConfigManager.ScrollSpeed;
    
    // 高精度タイマー
    private System.Diagnostics.Stopwatch playStopwatch = new();

    // ディレイ用フィールド
    private bool isStartingDelay = true;
    private double audioStartTimeSystemMs = 0;
    
    // オートプレイ用フィールド
    private bool isAutoplayEnabled = Utils.ConfigManager.Autoplay;
    private bool configChanged = false;

    // 入力エフェクト用フィールド
    private bool isLeftDonPressed;
    private bool isRightDonPressed;
    private bool isLeftKaPressed;
    private bool isRightKaPressed;
    private System.Diagnostics.Stopwatch leftDonStopwatch = new();
    private System.Diagnostics.Stopwatch rightDonStopwatch = new();
    private System.Diagnostics.Stopwatch leftKaStopwatch = new();
    private System.Diagnostics.Stopwatch rightKaStopwatch = new();

    private bool autoplayIsRightHand = true;
    private double lastAutoplayHitTimeMs = 0;

    // コンボ演出用フィールド
    private string cachedComboText = "";
    private Animations.ComboAnimation comboAnimation = new Animations.ComboAnimation();
    private float combobounces = 0;
    private int lastComboValue = 0;
    private Font comboFontBig;
    private Font comboFontSmall;

    private double cachedCurrentTimeMs = 0;
    private double CurrentPlayTimeMs => cachedCurrentTimeMs;

    public event Action? RequestedExit;

    public GameplayView(TjaChart chart, AudioManager audioManager, string songTitle)
    {
        this.chart = chart;
        this.audioManager = audioManager;
        this.songTitle = songTitle;
        this.scoringSystem = new ScoringSystem();
        this.judgmentSystem = new JudgmentSystem();
        
        comboFontBig = new Font(Utils.FontManager.KantiryuFontFamily, 24, FontStyle.Bold);
        comboFontSmall = new Font(Utils.FontManager.KantiryuFontFamily, 16, FontStyle.Bold);

        Utils.ConfigManager.Load();
        Utils.KeyConfigManager.Load(); // キー設定を読み込み

        isAutoplayEnabled = Utils.ConfigManager.Autoplay;
        // オプション再取得
        this.noteMod = Utils.ConfigManager.CurrentNoteMod;
        this.isDoron = Utils.ConfigManager.IsDoron;
        this.scrollSpeed = Utils.ConfigManager.ScrollSpeed;
        
        string fullAudioPath = System.IO.Path.Combine(chart.DirectoryPath, chart.AudioFileName);
        this.audioStream = audioManager.LoadTrack(fullAudioPath);
        
        this.DoubleBuffered = true;
        this.BackColor = Color.MidnightBlue;
        this.Dock = DockStyle.Fill;

        this.KeyDown += GameplayView_KeyDown;
        
        PreparePlay();
    }

    private void PreparePlay()
    {
        foreach (var note in chart.Notes)
        {
            note.IsHit = false;
            note.LastHitTimeMs = 0;
            
            // オプション適用
            if (noteMod == Utils.ConfigManager.NoteMod.Abekobe)
            {
                if (note.Type == NoteType.Don) note.Type = NoteType.Ka;
                else if (note.Type == NoteType.Ka) note.Type = NoteType.Don;
                else if (note.Type == NoteType.BigDon) note.Type = NoteType.BigKa;
                else if (note.Type == NoteType.BigKa) note.Type = NoteType.BigDon;
            }
            // きまぐれ/でたらめ (簡易)
            Random rng = new Random();
            if (noteMod == Utils.ConfigManager.NoteMod.Kimagure && rng.NextDouble() < 0.2)
                note.Type = (note.Type == NoteType.Don || note.Type == NoteType.BigDon) ? NoteType.Ka : NoteType.Don;
            if (noteMod == Utils.ConfigManager.NoteMod.Detarame && rng.NextDouble() < 0.5)
                note.Type = (note.Type == NoteType.Don || note.Type == NoteType.BigDon) ? NoteType.Ka : NoteType.Don;
        }

        isStartingDelay = true;
        playStopwatch.Restart();
        
        if (audioStream != 0)
        {
            ManagedBass.Bass.ChannelStop(audioStream);
            ManagedBass.Bass.ChannelSetPosition(audioStream, 0);
        }
    }

    private void GameplayView_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape)
        {
            Exit();
            return;
        }

        if (e.KeyCode == Keys.F1)
        {
            isAutoplayEnabled = !isAutoplayEnabled;
            Utils.ConfigManager.Autoplay = isAutoplayEnabled;
            configChanged = true;
            return;
        }

        // 演奏操作はオートプレイ中は無効化
        if (isAutoplayEnabled) return;

        if (e.KeyCode == Utils.KeyConfigManager.DonLeft) ProcessHit(true, true);
        else if (e.KeyCode == Utils.KeyConfigManager.DonRight) ProcessHit(true, false);
        else if (e.KeyCode == Utils.KeyConfigManager.KaLeft) ProcessHit(false, true);
        else if (e.KeyCode == Utils.KeyConfigManager.KaRight) ProcessHit(false, false);
    }

    private void Exit()
    {
        audioManager.StopTrack(audioStream);
        if (configChanged)
        {
            Utils.ConfigManager.Save();
        }
        RequestedExit?.Invoke();
    }

    private void ProcessHit(bool isDon, bool isLeft)
    {
        // Trigger visual effect
        if (isDon)
        {
            if (isLeft)
            {
                isLeftDonPressed = true;
                leftDonStopwatch.Restart();
            }
            else
            {
                isRightDonPressed = true;
                rightDonStopwatch.Restart();
            }
        }
        else
        {
            if (isLeft)
            {
                isLeftKaPressed = true;
                leftKaStopwatch.Restart();
            }
            else
            {
                isRightKaPressed = true;
                rightKaStopwatch.Restart();
            }
        }

        string sePath = isDon ? @"Theme\default\sound\dong.wav" : @"Theme\default\sound\ka.wav";
        audioManager.PlaySoundEffect(sePath);

        double currentTime = cachedCurrentTimeMs;
        
        var activeRoll = chart.Notes.FirstOrDefault(n => !n.IsHit && 
            (n.Type == NoteType.Roll || n.Type == NoteType.BigRoll || n.Type == NoteType.Balloon) &&
            currentTime >= n.TimeMs && currentTime <= n.EndTimeMs);

        if (activeRoll != null)
        {
            scoringSystem.AddScore(Judgment.Perfect, false);
            return;
        }

        Note? closestNote = null;
        double minDiff = double.MaxValue;

        foreach (var note in chart.Notes)
        {
            if (note.IsHit) continue;
            if (note.Type > NoteType.BigKa) continue;
            
            double diff = currentTime - note.TimeMs;
            
            if (Math.Abs(diff) <= JudgmentSystem.BadWindowMs)
            {
                bool isNoteDon = (note.Type == NoteType.Don || note.Type == NoteType.BigDon);
                bool isNoteKa = (note.Type == NoteType.Ka || note.Type == NoteType.BigKa);
                
                if ((isDon && isNoteDon) || (!isDon && isNoteKa))
                {
                    if (Math.Abs(diff) < minDiff)
                    {
                        minDiff = Math.Abs(diff);
                        closestNote = note;
                    }
                }
            }
        }

        if (closestNote != null)
        {
            double diff = currentTime - closestNote.TimeMs;
            var judgment = judgmentSystem.Judge(diff);
            
            closestNote.IsHit = true;
            if (judgment != Judgment.None)
            {
                bool isBigNote = (closestNote.Type == NoteType.BigDon || closestNote.Type == NoteType.BigKa);
                scoringSystem.AddScore(judgment, isBigNote);

            // コンボ加算アニメーション
            if (judgment == Judgment.Perfect || judgment == Judgment.Good)
            {
                comboAnimation.AddCombo();
            }
            }
        }
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        audioManager.StopTrack(audioStream);
        if (configChanged) Utils.ConfigManager.Save();
        base.OnHandleDestroyed(e);
    }

    public new void Update()
    {
        double systemTimeMs = playStopwatch.Elapsed.TotalMilliseconds;
        double totalElapsedMs = systemTimeMs - 2000.0;
        
        double audioStartTimeMs = (chart.WaveOffsetMs < 0) ? -chart.WaveOffsetMs : 0;

        if (isStartingDelay && totalElapsedMs >= audioStartTimeMs)
        {
            isStartingDelay = false;
            ManagedBass.Bass.ChannelPlay(audioStream, false);
            audioStartTimeSystemMs = systemTimeMs;
        }

        double chartTime;
        if (isStartingDelay)
        {
            chartTime = totalElapsedMs + chart.WaveOffsetMs;
        }
        else
        {
            double playbackSpeed = Utils.ConfigManager.PlaybackSpeed;
            double timeSinceAudioStart = (systemTimeMs - audioStartTimeSystemMs) * playbackSpeed;
            chartTime = timeSinceAudioStart + chart.WaveOffsetMs;

            var state = ManagedBass.Bass.ChannelIsActive(audioStream);
            if (state == ManagedBass.PlaybackState.Stopped)
            {
                HandleSongFinished();
                return;
            }
        }

        cachedCurrentTimeMs = chartTime + Utils.ConfigManager.JudgeOffset;

        // コンボバウンド更新
        if (scoringSystem.Combo != lastComboValue)
        {
            lastComboValue = scoringSystem.Combo;
            combobounces = 0;
        }

        if (combobounces < 90)
        {
            combobounces += 3.0f; // 速度調整
            if (combobounces > 90) combobounces = 90; // カウントを90でキャップ
        }

        foreach (var note in chart.Notes)
        {
            if (note.IsHit) continue;
            
            if (note.Type <= NoteType.BigKa)
            {
                if (cachedCurrentTimeMs > note.TimeMs + JudgmentSystem.BadWindowMs)
                {
                    note.IsHit = true;
                    scoringSystem.AddScore(Judgment.Miss, false);
                }
                else if (isAutoplayEnabled && cachedCurrentTimeMs >= note.TimeMs)
                {
                    note.IsHit = true;
                    string sePath = (note.Type == NoteType.Don || note.Type == NoteType.BigDon) ? @"Theme\default\sound\dong.wav" : @"Theme\default\sound\ka.wav";
                    audioManager.PlaySoundEffect(sePath);
                    bool isBigNote = (note.Type == NoteType.BigDon || note.Type == NoteType.BigKa);
                    scoringSystem.AddScore(Judgment.Perfect, isBigNote);
                    
                    // Simulate input effect for Autoplay
                    if (cachedCurrentTimeMs - lastAutoplayHitTimeMs > 1000)
                    {
                        autoplayIsRightHand = true;
                    }
                    lastAutoplayHitTimeMs = cachedCurrentTimeMs;

                    if (note.Type == NoteType.Don) { 
                        if (autoplayIsRightHand) { isRightDonPressed = true; rightDonStopwatch.Restart(); }
                        else { isLeftDonPressed = true; leftDonStopwatch.Restart(); }
                        autoplayIsRightHand = !autoplayIsRightHand;
                    }
                    else if (note.Type == NoteType.BigDon) { isLeftDonPressed = true; isRightDonPressed = true; leftDonStopwatch.Restart(); rightDonStopwatch.Restart(); }
                    else if (note.Type == NoteType.Ka) { 
                        if (autoplayIsRightHand) { isRightKaPressed = true; rightKaStopwatch.Restart(); }
                        else { isLeftKaPressed = true; leftKaStopwatch.Restart(); }
                        autoplayIsRightHand = !autoplayIsRightHand;
                    }
                    else if (note.Type == NoteType.BigKa) { isLeftKaPressed = true; isRightKaPressed = true; leftKaStopwatch.Restart(); rightKaStopwatch.Restart(); }

                    // コンボ加算アニメーション
                    combobounces = 1;
                    cachedComboText = scoringSystem.Combo.ToString();
                }
            }
            else if (note.Type == NoteType.Roll || note.Type == NoteType.BigRoll || note.Type == NoteType.Balloon)
            {
                if (cachedCurrentTimeMs >= note.TimeMs && cachedCurrentTimeMs <= note.EndTimeMs)
                {
                    if (isAutoplayEnabled)
                    {
                        double interval = (60000.0 / note.Bpm) / 4.0;
                        if (cachedCurrentTimeMs >= note.LastHitTimeMs + interval)
                        {
                            note.LastHitTimeMs = cachedCurrentTimeMs;
                            audioManager.PlaySoundEffect(@"Theme\default\sound\dong.wav");
                            bool isBigNote = (note.Type == NoteType.BigRoll);
                            scoringSystem.AddScore(Judgment.Perfect, isBigNote);
                            
                            // Simulate roll effect
                            if (isRightDonPressed)
                            {
                                isLeftDonPressed = true; leftDonStopwatch.Restart();
                                isRightDonPressed = false;
                            }
                            else
                            {
                                isRightDonPressed = true; rightDonStopwatch.Restart();
                                isLeftDonPressed = false;
                            }
                        }
                    }
                }
                else if (cachedCurrentTimeMs > note.EndTimeMs)
                {
                    note.IsHit = true;
                }
            }
        }
    }

    public event Action<PlayResult>? SongFinished;

    private void HandleSongFinished()
    {
        audioManager.StopTrack(audioStream);
        if (configChanged) Utils.ConfigManager.Save();
        
        var result = new PlayResult
        {
            SongTitle = "Song",
            PerfectCount = scoringSystem.PerfectCount,
            GoodCount = scoringSystem.GoodCount,
            MissCount = scoringSystem.MissCount,
            MaxCombo = scoringSystem.MaxCombo,
            TotalScore = scoringSystem.Score
        };
        
        SongFinished?.Invoke(result);
    }

    public void Render()
    {
        this.Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        Graphics g = e.Graphics;
        Utils.SkinManager.RenderBackground(g, Width, Height);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        double currentPlayTimeMs = cachedCurrentTimeMs;
        float targetLineX = 150f; // Left side
        float targetLineY = this.ClientSize.Height / 2f;
        const float widthPerBeat = 150f;

        // 1. Draw Lane Background
        if (Utils.SkinManager.LaneImage != null)
        {
            float laneHeight = 130;
            System.Drawing.Imaging.ColorMatrix matrix = new System.Drawing.Imaging.ColorMatrix();
            matrix.Matrix33 = 0.5f; // 50% transparency
            using var attributes = new System.Drawing.Imaging.ImageAttributes();
            attributes.SetColorMatrix(matrix, System.Drawing.Imaging.ColorMatrixFlag.Default, System.Drawing.Imaging.ColorAdjustType.Bitmap);
            
            g.DrawImage(Utils.SkinManager.LaneImage, 
                new Rectangle(0, (int)(targetLineY - laneHeight / 2), Width, (int)laneHeight),
                0, 0, Utils.SkinManager.LaneImage.Width, Utils.SkinManager.LaneImage.Height,
                GraphicsUnit.Pixel, attributes);
        }

        // 2. Input Effect Taiko Drum
        if (leftDonStopwatch.ElapsedMilliseconds > 100) isLeftDonPressed = false;
        if (rightDonStopwatch.ElapsedMilliseconds > 100) isRightDonPressed = false;
        if (leftKaStopwatch.ElapsedMilliseconds > 100) isLeftKaPressed = false;
        if (rightKaStopwatch.ElapsedMilliseconds > 100) isRightKaPressed = false;

        float drumX = 65f;
        float drumY = targetLineY;
        float outerRadius = 45f;
        float innerRadius = 32f;

        // Draw outer rim (Ka)
        using (SolidBrush leftKaBrush = new SolidBrush(isLeftKaPressed ? Color.DeepSkyBlue : Color.Gray))
        {
            g.FillPie(leftKaBrush, drumX - outerRadius, drumY - outerRadius, outerRadius * 2, outerRadius * 2, 90, 180);
        }
        using (SolidBrush rightKaBrush = new SolidBrush(isRightKaPressed ? Color.DeepSkyBlue : Color.Gray))
        {
            g.FillPie(rightKaBrush, drumX - outerRadius, drumY - outerRadius, outerRadius * 2, outerRadius * 2, -90, 180);
        }
        g.DrawEllipse(Pens.Black, drumX - outerRadius, drumY - outerRadius, outerRadius * 2, outerRadius * 2);

        // Draw inner face (Don)
        using (SolidBrush leftDonBrush = new SolidBrush(isLeftDonPressed ? Color.Red : Color.White))
        {
            g.FillPie(leftDonBrush, drumX - innerRadius, drumY - innerRadius, innerRadius * 2, innerRadius * 2, 90, 180);
        }
        using (SolidBrush rightDonBrush = new SolidBrush(isRightDonPressed ? Color.Red : Color.White))
        {
            g.FillPie(rightDonBrush, drumX - innerRadius, drumY - innerRadius, innerRadius * 2, innerRadius * 2, -90, 180);
        }
        g.DrawEllipse(Pens.Black, drumX - innerRadius, drumY - innerRadius, innerRadius * 2, innerRadius * 2);
        
        // Split line
        g.DrawLine(Pens.Black, drumX, drumY - outerRadius, drumX, drumY + outerRadius);

        // 3. Combo (Inside Drum)
        if (scoringSystem.Combo >= 10)
        {
            DrawCombo(g, scoringSystem.Combo, comboAnimation.Scale, drumX, drumY);
        }

        // 4. Draw Target
        g.DrawEllipse(Pens.White, targetLineX - 40, targetLineY - 40, 80, 80);

        // 5. Draw Barlines
        foreach (var bar in chart.Barlines)
        {
            if (!bar.IsVisible) continue;
            double diff = bar.TimeMs - currentPlayTimeMs;
            if (diff < -1000 || diff > 4000) continue;
            
            float pixelsPerMs = (float)(bar.Bpm / 60000.0) * widthPerBeat * scrollSpeed;
            Complex scrollOffset = bar.ScrollValue * (diff * pixelsPerMs);
            float x = targetLineX + (float)scrollOffset.Real;
            float y = targetLineY + (float)scrollOffset.Imaginary;
            g.DrawLine(Pens.Gray, x, y - 50, x, y + 50);
        }

        // 6. Draw Notes
        foreach (var note in chart.Notes)
        {
            if (note.IsHit) continue;
            double diff = note.TimeMs - currentPlayTimeMs;
            if (diff > 20000) continue;
            
            if (!note.IsVisible) continue;
            if (isDoron) continue;
            if (diff < -1000) continue;
            
            float pixelsPerMs = (float)(note.Bpm / 60000.0) * widthPerBeat * scrollSpeed;
            Complex scrollOffset = note.ScrollValue * (diff * pixelsPerMs);
            float x = targetLineX + (float)scrollOffset.Real;
            float y = targetLineY + (float)scrollOffset.Imaginary;
            
            if (x < -1000 || x > Width + 1000) continue;
            
            Brush brush = (note.Type == NoteType.Ka || note.Type == NoteType.BigKa) ? Brushes.DeepSkyBlue : Brushes.Red;
            float size = (note.Type == NoteType.BigDon || note.Type == NoteType.BigKa) ? 70 : 50;
            
            if (note.IsGogo) g.FillEllipse(Brushes.Yellow, x - size / 2 - 2, y - size / 2 - 2, size + 4, size + 4);
            g.FillEllipse(brush, x - size / 2, y - size / 2, size, size);
            g.DrawEllipse(Pens.Black, x - size / 2, y - size / 2, size, size);
        }

        g.DrawString($"Score: {scoringSystem.Score}", new Font(Utils.FontManager.KantiryuFontFamily, 12), Brushes.White, 10, 10);
        if (isAutoplayEnabled) g.DrawString("Auto Play", new Font(Utils.FontManager.KantiryuFontFamily, 12), Brushes.Yellow, 10, 50);
        
        // 曲タイトル表示 (右上に表示)
        SizeF titleSize = g.MeasureString(songTitle, new Font(Utils.FontManager.KantiryuFontFamily, 16));
        g.DrawString(songTitle, new Font(Utils.FontManager.KantiryuFontFamily, 16), Brushes.White, Width - titleSize.Width - 10, 10);
    }

    private void DrawCombo(Graphics g, int combo, float scale, float drumX, float drumY)
    {
        // 白色数字テクスチャを使用
        if (Utils.SkinManager.ComboImage == null || Utils.SkinManager.ComboTextImage == null) return;

        string comboStr = combo.ToString();
        int digitCount = comboStr.Length;
        
        // ベース位置を太鼓の中心(drumY)に設定
        float baseTextY = drumY;
        
        // バウンドアニメーション
        float bounceOffset = (combobounces >= 90) ? 0.0f : (float)(Math.Sin(combobounces / 90.0 * Math.PI) * -5.0);

        // 数字画像の幅と高さ(見た目に合わせて調整)
        float digitWidth = Utils.SkinManager.ComboImage.Width / 10f;
        float digitHeight = Utils.SkinManager.ComboImage.Height;
        float drawScale = scale * 0.7f;

        // 数字描画（文字間隔を調整）
        float letterSpacing = -10f * drawScale;
        float totalWidth = digitCount * (digitWidth + letterSpacing) * drawScale;
        float currentX = drumX - (totalWidth / 2f);

        foreach (char c in comboStr)
        {
            int digit = c - '0';
            Rectangle srcRect = new Rectangle((int)(digit * digitWidth), 0, (int)digitWidth, (int)digitHeight);
            // 太鼓の真ん中に来るように位置を調整
            Rectangle destRect = new Rectangle((int)currentX, (int)(baseTextY + bounceOffset - (digitHeight * drawScale / 2f)), (int)(digitWidth * drawScale), (int)(digitHeight * drawScale));
            g.DrawImage(Utils.SkinManager.ComboImage, destRect, srcRect, GraphicsUnit.Pixel);
            currentX += (digitWidth + letterSpacing) * drawScale;
        }

        // コンボテキスト描画（上側の白色のコンボテキストのみを使用）
        Rectangle textSrcRect = new Rectangle(0, 0, Utils.SkinManager.ComboTextImage.Width, Utils.SkinManager.ComboTextImage.Height / 2);
        Rectangle textDestRect = new Rectangle(
            (int)(drumX - (Utils.SkinManager.ComboTextImage.Width * 0.8f / 2f)),
            (int)(baseTextY + 20),
            (int)(Utils.SkinManager.ComboTextImage.Width * 0.8f),
            (int)(Utils.SkinManager.ComboTextImage.Height / 2 * 0.8f)
        );
        g.DrawImage(Utils.SkinManager.ComboTextImage, textDestRect, textSrcRect, GraphicsUnit.Pixel);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            comboFontBig.Dispose();
            comboFontSmall.Dispose();
        }
        base.Dispose(disposing);
    }
}
