using System.Collections.Generic;
using TjaPlayer.Models;

namespace TjaPlayer.Gameplay;

public class SongManager
{
    public List<SongNode> RootNodes { get; private set; } = new();

    public void ParseTagsFromTitle(SongNode node)
    {
        // タイトルが "#TAG:Value" を含んでいたらタグとして抽出する例
        if (node.Title.Contains("#"))
        {
            var parts = node.Title.Split('#');
            node.Title = parts[0].Trim();
            for (int i = 1; i < parts.Length; i++)
            {
                var tagParts = parts[i].Split(':');
                if (tagParts.Length == 2)
                {
                    node.AddTag(tagParts[0].Trim(), tagParts[1].Trim());
                }
            }
        }
    }
}
