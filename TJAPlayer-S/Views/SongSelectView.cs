using System.Windows.Forms;
using TjaPlayer.Models;

namespace TjaPlayer.Views;

public class SongSelectView : UserControl, IAppState
{
    public AppStateEnum State => AppStateEnum.SongSelect;
    
    private ListBox songListBox = new ListBox();
    private List<SongInfo> songs;

    public SongSelectView(List<SongInfo> songs)
    {
        this.songs = songs;
        Dock = DockStyle.Fill;
        songListBox.Dock = DockStyle.Fill;
        
        foreach (var song in songs)
        {
            songListBox.Items.Add(song.Chart.Title ?? "Unknown Song");
        }
        
        Controls.Add(songListBox);
    }

    public new void Update()
    {
        // Prototype: no input handling yet
    }

    public void Render()
    {
        // Prototype: SlimDX rendering placeholder
    }
}
