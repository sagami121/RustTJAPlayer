using System;
using System.Windows.Forms;
using TjaPlayer.Audio;
using TjaPlayer.Models;
using TjaPlayer.Views;

namespace TjaPlayer;

public partial class MainForm : Form
{
    private StateManager stateManager;
    private AudioManager audioManager;
    private System.Windows.Forms.Timer renderTimer;

    public MainForm()
    {
        Text = "TJAPlayer-S";
        Size = new System.Drawing.Size(800, 600);
        audioManager = new AudioManager();

        // Initialize with Song Selection
        var initialSongs = new System.Collections.Generic.List<SongInfo>(); // Populate this
        stateManager = new StateManager(new SongSelectView(initialSongs));

        if (stateManager.CurrentState is Control control)
        {
            Controls.Add(control);
        }

        // Setup render timer (~60 FPS)
        renderTimer = new System.Windows.Forms.Timer { Interval = 16 }; // ~60 fps
        renderTimer.Tick += (sender, e) =>
        {
            stateManager.Update();
            stateManager.Render();
        };
        renderTimer.Start();
    }

    public void SwitchToGameplay(Tja chart)
    {
        Controls.Clear();
        var gameplayView = new GameplayView(chart, audioManager);
        stateManager.ChangeState(gameplayView);
        Controls.Add(gameplayView);
    }
}
