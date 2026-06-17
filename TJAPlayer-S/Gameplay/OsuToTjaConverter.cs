using System;
using System.Collections.Generic;
using TjaPlayer.Models;

namespace TjaPlayer.Gameplay;

public class OsuToTjaConverter
{
    public static string ConvertToTja(OsuChart osuChart)
    {
        // 簡易的なTJA生成ロジック
        // 本来はTimingPointsを解析して小節に区切る必要がある
        var tja = new List<string>();
        tja.Add($"TITLE:{osuChart.Title}");
        tja.Add($"ARTIST:{osuChart.Artist}");
        tja.Add("COURSE:Oni");
        tja.Add("#START");
        
        // とりあえずすべての音符を1小節に詰め込む（簡易変換）
        // 厳密な再現には小節線計算が必要
        foreach (var obj in osuChart.HitObjects)
        {
            tja.Add("1"); // ドン
        }
        
        tja.Add("#END");
        return string.Join(Environment.NewLine, tja);
    }
}
