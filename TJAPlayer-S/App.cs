namespace TjaPlayer;

public class CTja {
    public int nInstanceDifficulty = 0;
    public CLocalCounters LocalCounters = new CLocalCounters();
    public CLocalTriggers LocalTriggers = new CLocalTriggers();
    public int nノーツ数_Common = 0;
    public int nデモBGMオフセット = 0;
}

public static class TjaPlayerManager
{
    public static CTja? GetTJA(int player) => null; // 暫定
    public static Config ConfigIni = new Config(); // 仮

    public class Config
    {
        public bool[] bAutoPlay = new bool[5];
        public bool bAIBattleMode = false;
        public int nPoliphonicSounds = 4;
    }

    public static SaveFile[] SaveFileInstances = new SaveFile[5]; // 仮

    public static int GetActualPlayer(int player) => player;
}

public class SaveFile {
    public void tSetGlobalCounter(string name, double val) {}
    public double tGetGlobalCounter(string name) => 0;
    public void tSetGlobalTrigger(string name, bool val) {}
    public bool tGetGlobalTrigger(string name) => false;
}
