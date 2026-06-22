using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.IO;
using System.Text;
using TjaPlayer.Audio;
using TjaPlayer.Models;
using TjaPlayer.Views;
using TjaPlayer.Gameplay;
using ManagedBass;

namespace TjaPlayer;

public partial class MainForm : Form
{
    private StateManager? stateManager;
    private AudioManager audioManager;

    private List<Score> initialSongs = new();

    public MainForm()
    {
        Text = "TJAPlayer-S";
        Size = new System.Drawing.Size(800, 600);
        audioManager = new AudioManager();
        Common.TJAPlayer_S.Audio = audioManager;
        
        Utils.ConfigManager.Load(); // 設定の読み込み

        LoadSongs();
        
        ReturnToSongSelect();
    }

    public void UpdateLoop()
    {
        stateManager?.Update();
    }

    public void RenderLoop()
    {
        stateManager?.Render();
    }

    private void LoadSongs()
    {
        string songsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Songs");
        
        if (!Directory.Exists(songsPath))
        {
            songsPath = @"D:\dev\sagami\TjaPlayer\TJAPlayer-S\Songs";
        }

        if (Directory.Exists(songsPath))
        {
            // フォルダごとに genre.ini をチェックしながら読み込む
            var songDirs = Directory.GetDirectories(songsPath, "*", SearchOption.AllDirectories).ToList();
            songDirs.Add(songsPath);

            foreach (var dir in songDirs)
            {
                string genreName = "";
                var genreColor = System.Drawing.Color.DimGray;
                var fontColor = System.Drawing.Color.White;

                string genreIniPath = Path.Combine(dir, "genre.ini");
                if (File.Exists(genreIniPath))
                {
                    var lines = File.ReadAllLines(genreIniPath, Encoding.GetEncoding("shift-jis"));
                    foreach (var line in lines)
                    {
                        if (line.StartsWith("GenreName=")) genreName = line.Substring(10).Trim();
                        if (line.StartsWith("GenreColor="))
                        {
                            try { genreColor = ColorTranslator.FromHtml(line.Substring(11).Trim().Replace("0x", "#")); } catch { }
                        }
                        if (line.StartsWith("FontColor="))
                        {
                            try { fontColor = ColorTranslator.FromHtml(line.Substring(10).Trim().Replace("0x", "#")); } catch { }
                        }
                    }
                }

                var tjaFiles = Directory.GetFiles(dir, "*.tja", SearchOption.TopDirectoryOnly);
                foreach (var file in tjaFiles)
                {
                    try
                    {
                        var score = TjaParser.Parse(file);
                        if (!string.IsNullOrEmpty(genreName)) score.Genre = genreName;
                        score.GenreColor = genreColor;
                        score.FontColor = fontColor;
                        initialSongs.Add(score);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to parse {file}: {ex.Message}");
                    }
                }

                var osuFiles = Directory.GetFiles(dir, "*.osu", SearchOption.TopDirectoryOnly);
                foreach (var file in osuFiles)
                {
                    string tjaPath = Path.ChangeExtension(file, ".tja");
                    if (!File.Exists(tjaPath))
                    {
                        try
                        {
                            var osuChart = Gameplay.OsuParser.Parse(file);
                            var tjaContent = Gameplay.OsuToTjaConverter.ConvertToTja(osuChart);
                            File.WriteAllText(tjaPath, tjaContent, System.Text.Encoding.GetEncoding(932));
                            
                            var score = TjaParser.Parse(tjaPath);
                            if (!string.IsNullOrEmpty(genreName)) score.Genre = genreName;
                            score.GenreColor = genreColor;
                            score.FontColor = fontColor;
                            initialSongs.Add(score);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Failed to convert/parse {file}: {ex.Message}");
                        }
                    }
                }
            }
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode == Keys.F2)
        {
            if (stateManager?.CurrentState is CreationModeState)
            {
                ReturnToSongSelect();
            }
            else
            {
                Controls.Clear();
                var creationMode = new CreationModeState();
                stateManager?.ChangeState(creationMode);
                Controls.Add((Control)creationMode); // IAppStateがControlを実装していると仮定
                ((Control)creationMode).Focus();
            }
        }
    }

    public void ReturnToSongSelect()
    {
        Controls.Clear();
        var songSelectView = new SongSelectView(initialSongs, audioManager);
        songSelectView.SongSelected += (score) => SwitchToDifficultySelect(score);
        
        if (stateManager == null)
        {
            stateManager = new StateManager(songSelectView);
            Common.TJAPlayer_S.StateManager = stateManager;
        }
        else
            stateManager.ChangeState(songSelectView);

        Controls.Add(songSelectView);
        songSelectView.Focus();
    }

    public void SwitchToDifficultySelect(Score score)
    {
        Controls.Clear();
        var diffSelectView = new DifficultySelectView(score, audioManager);
        diffSelectView.DifficultySelected += (chart) => SwitchToGameplay(chart, score.Title, score);
        diffSelectView.RequestedExit += () => ReturnToSongSelect();
        stateManager?.ChangeState(diffSelectView);
        Controls.Add(diffSelectView);
        diffSelectView.Focus();
    }

    public void SwitchToGameplay(TjaChart chart, string songTitle, Score score)
    {
        Controls.Clear();
        var gameplayView = new GameplayView(chart, audioManager, songTitle, score.ScoreInit, score.ScoreDiff);
        gameplayView.SongFinished += (result) => SwitchToResult(result);
        gameplayView.RequestedExit += () => ReturnToSongSelect(); // 追加
    gameplayView.PauseRequested += () => SwitchToPause(gameplayView); // 一時停止処理を追加
        stateManager?.ChangeState(gameplayView);
        Controls.Add(gameplayView);
        gameplayView.Focus();
    }

    private void SwitchToPause(GameplayView gameplayView)
    {
        Controls.Clear();
        var pauseView = new PauseView(
            audioManager,
            gameplayView.Chart,
            gameplayView.SongTitle,
            gameplayView.ScoreInit,
            gameplayView.ScoreDiff,
            gameplayView.AudioStream,
            gameplayView.CurrentChartTimeMs,
            gameplayView.ScoringSystem,
            gameplayView.JudgmentSystem);
        pauseView.ResumeRequested += () => ResumeFromPause(gameplayView);
        pauseView.RestartRequested += () => RestartFromPause(gameplayView);
        pauseView.ExitToSongSelectRequested += () => ReturnToSongSelect();
        stateManager?.ChangeState(pauseView);
        Controls.Add(pauseView);
        pauseView.Focus();
    }

    private void ResumeFromPause(GameplayView gameplayView)
    {
        // Resume the audio stream
        if (gameplayView.AudioStream != 0)
        {
            Bass.ChannelPlay(gameplayView.AudioStream);
        }

        // Switch back to gameplay
        Controls.Clear();
        stateManager?.ChangeState(gameplayView);
        Controls.Add(gameplayView);
        gameplayView.Focus();
    }

    private void RestartFromPause(GameplayView gameplayView)
    {
        // Stop the current audio
        if (gameplayView.AudioStream != 0)
        {
            audioManager.StopTrack(gameplayView.AudioStream);
        }

        // Restart the game from the beginning
        // For restart, we create a minimal Score object with default values
        var score = new Score
        {
            Title = gameplayView.SongTitle,
            ScoreInit = 0,
            ScoreDiff = 0,
            Charts = new System.Collections.Generic.Dictionary<string, TjaChart>()
        };

        SwitchToGameplay(gameplayView.Chart, gameplayView.SongTitle, score);
    }

    public void SwitchToResult(PlayResult result)
    {
        Controls.Clear();
        var resultView = new ResultView(result);
        resultView.RequestedExit += () => ReturnToSongSelect();
        stateManager?.ChangeState(resultView);
        Controls.Add(resultView);
        resultView.Focus();
    }
}