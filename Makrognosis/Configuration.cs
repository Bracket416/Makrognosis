using Dalamud.Configuration;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace Makrognosis;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;

    public Dictionary<string, List<Tuple<int, int>>> Mechanics = new();
    public Vector2 Position = new();

    public Boolean Raw = false;
    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }
}
