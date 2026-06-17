using TjaPlayer.Audio;

namespace TjaPlayer.Common;

public static class TJAPlayer_S
{
    // グローバルにアクセスしたいインスタンスをここに定義
    public static AudioManager Audio { get; set; } = null!;
    public static StateManager StateManager { get; set; } = null!;
}
