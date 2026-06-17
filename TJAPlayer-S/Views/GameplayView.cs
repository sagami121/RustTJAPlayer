using System.Windows.Forms;
using System.Collections.Generic;
using System.IO;
using System;
using System.Drawing;
using System.Linq;
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
    
    // 高精度タイマー
    private System.Diagnostics.Stopwatch playStopwatch = new();
    private double lastAudioPosMs = 0;
    private double lastSystemTimeMs = 0;

    // ディレイ用フィールド
    private bool isStartingDelay = true;
    private double audioStartTimeSystemMs = 0;
    
    // オートプレイ用フィールド
    private bool isAutoplayEnabled = Utils.ConfigManager.Autoplay;
    private bool configChanged = false;

    private double cachedCurrentTimeMs = 0;
    private double CurrentPlayTimeMs => cachedCurrentTimeMs;

    public event Action? RequestedExit;

    public GameplayView(TjaChart chart, AudioManager audioManager)
    {
        this.chart = chart;
        this.audioManager = audioManager;
        this.scoringSystem = new ScoringSystem();
        this.judgmentSystem = new JudgmentSystem();
        
        Utils.ConfigManager.Load();
        isAutoplayEnabled = Utils.ConfigManager.Autoplay;
        
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

        if (e.KeyCode == Keys.F || e.KeyCode == Keys.J) ProcessHit(true);
        if (e.KeyCode == Keys.D || e.KeyCode == Keys.K) ProcessHit(false);
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

    private void ProcessHit(bool isDon)
    {
        string sePath = isDon ? @"Theme\default\sound\dong.wav" : @"Theme\default\sound\ka.wav";
        audioManager.PlaySoundEffect(sePath);

        double currentTime = cachedCurrentTimeMs;
        
        var activeRoll = chart.Notes.FirstOrDefault(n => !n.IsHit && 
            (n.Type == NoteType.Roll || n.Type == NoteType.BigRoll || n.Type == NoteType.Balloon) &&
            currentTime >= n.TimeMs && currentTime <= n.EndTimeMs);

        if (activeRoll != null)
        {
            scoringSystem.AddScore(Judgment.Perfect);
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
                scoringSystem.AddScore(judgment);
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

        foreach (var note in chart.Notes)
        {
            if (note.IsHit) continue;
            
            if (note.Type <= NoteType.BigKa)
            {
                if (cachedCurrentTimeMs > note.TimeMs + JudgmentSystem.BadWindowMs)
                {
                    note.IsHit = true;
                    scoringSystem.AddScore(Judgment.Miss);
                }
                else if (isAutoplayEnabled && cachedCurrentTimeMs >= note.TimeMs)
                {
                    note.IsHit = true;
                    string sePath = (note.Type == NoteType.Don || note.Type == NoteType.BigDon) ? @"Theme\default\sound\dong.wav" : @"Theme\default\sound\ka.wav";
                    audioManager.PlaySoundEffect(sePath);
                    scoringSystem.AddScore(Judgment.Perfect);
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
                            scoringSystem.AddScore(Judgment.Perfect);
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
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        double currentPlayTimeMs = cachedCurrentTimeMs;
        float targetLineX = 200f;
        float targetLineY = 100f;
        const float widthPerBeat = 150f;

        g.DrawEllipse(Pens.White, targetLineX - 30, targetLineY - 30, 60, 60);

        foreach (var bar in chart.Barlines)
        {
            if (!bar.IsVisible) continue;
            double diff = bar.TimeMs - currentPlayTimeMs;
            if (diff < -1000 || diff > 4000) continue;
            
            float pixelsPerMs = (float)(bar.Bpm / 60000.0) * widthPerBeat;
            float x = targetLineX + (float)(diff * pixelsPerMs * bar.ScrollFactorX);
            float y = targetLineY + (float)(diff * pixelsPerMs * bar.ScrollFactorY);
            g.DrawLine(Pens.Gray, x, y - 50, x, y + 50);
        }

        foreach (var note in chart.Notes)
        {
            if (note.IsHit) continue;
            if (!note.IsVisible) continue;

            double diff = note.TimeMs - currentPlayTimeMs;
            if (diff < -500 || diff > 4000) continue;
            
            float pixelsPerMs = (float)(note.Bpm / 60000.0) * widthPerBeat;
            float x = targetLineX + (float)(diff * pixelsPerMs * note.ScrollFactorX);
            float y = targetLineY + (float)(diff * pixelsPerMs * note.ScrollFactorY);
            
            Brush brush = (note.Type == NoteType.Ka || note.Type == NoteType.BigKa) ? Brushes.DeepSkyBlue : Brushes.Red;
            float size = (note.Type == NoteType.BigDon || note.Type == NoteType.BigKa) ? 70 : 50;
            
            if (note.IsGogo) g.FillEllipse(Brushes.Yellow, x - size / 2 - 2, y - size / 2 - 2, size + 4, size + 4);
            g.FillEllipse(brush, x - size / 2, y - size / 2, size, size);
            g.DrawEllipse(Pens.Black, x - size / 2, y - size / 2, size, size);
        }

        g.DrawString($"Score: {scoringSystem.Score}", this.Font, Brushes.White, 10, 10);
        g.DrawString($"Combo: {scoringSystem.Combo}", this.Font, Brushes.White, 10, 30);
        if (isAutoplayEnabled) g.DrawString("Auto Play", this.Font, Brushes.Yellow, 10, 50);
    }
}