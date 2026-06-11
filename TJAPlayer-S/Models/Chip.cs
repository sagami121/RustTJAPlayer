namespace TjaPlayer.Models;

public enum NoteChannel
{
    Don = 0x11,
    Ka = 0x12,
    DonBig = 0x13,
    KaBig = 0x14,
    Roll = 0x15,
    RollBig = 0x16,
    Balloon = 0x17,
    RollEnd = 0x18,
    Mine = 0x19
}

public class Chip
{
    public int ChannelNo { get; set; }
    public double TimeMs { get; set; }
    public bool IsHitted { get; set; }
    public bool IsMissed { get; set; }
    public double Scroll { get; set; } = 1.0;
    
    // For rendering position
    public float ScreenX { get; set; }
    
    public bool IsNote => ChannelNo >= 0x11 && ChannelNo <= 0x19;
}
