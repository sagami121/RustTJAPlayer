using System;
using System.Collections.Generic;

namespace TjaPlayer.Models;

public class SongNode
{
    public string Title { get; set; } = "";
    public string Genre { get; set; } = "";
    public Dictionary<string, string> Tags { get; set; } = new();
    
    // 他のメタデータプロパティ
    public List<SongNode> Children { get; set; } = new();
    
    public void AddTag(string key, string value)
    {
        Tags[key] = value;
    }
}
