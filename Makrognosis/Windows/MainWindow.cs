using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Lumina.Excel.Sheets;
namespace Makrognosis.Windows;
public class MainWindow : Window, IDisposable
{
    public bool Configuring = false;
    private Plugin Plugin;
    public uint ID = 0;
    public bool Ready = false;
    public List<string> Mechanics = new();
    private List<string> Types = new List<string> { "", "(Physical)", "(Magical)" };
    public double Defense = 1.0;
    public double Magical_Defense = 1.0;
    public double Tenacity = 1.0;
    public bool Drawing = true;

    public Vector2 P;

    public MainWindow(Plugin plugin)
        : base("Makrognosis##Main", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(375, 330),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
        Plugin = plugin;
    }


    private void Configure()
    {
        Plugin.Configuration.Position = P;
        Plugin.Configuration.Save();
    }
    public void Dispose() { }

    public override void Draw()
    {
        Drawing = true;
        using (var child = ImRaii.Child("SomeChildWithAScrollbar", Vector2.Zero, true))
        {
            // Check if this child is drawing

            if (child.Success)
            {
                var Add = false;
                ImGui.Checkbox(Configuring ? "Save##Makro Move" : "Move##Makro Move", ref Add);
                if (Add)
                {
                    Configure();
                    Configuring = !Configuring;
                }
                Add = false;
                ImGui.Checkbox(Plugin.Configuration.Raw ? "Raw Damage##Makro Move" : "Geared Damage##Makro Move", ref Add);
                if (Add)
                {
                    Plugin.Configuration.Raw = !Plugin.Configuration.Raw;
                    Configure();
                }
                var DEF = (new List<double> { Math.Max(Defense, Magical_Defense), Defense, Magical_Defense });
                foreach (var Mechanic in Mechanics)
                {
                    var Data = Plugin.Average(Mechanic);
                    var Damage = Data.Item1 * (Plugin.Configuration.Raw ? 1.0 : DEF[Data.Item2]);
                    ImGui.Text(Mechanic + ": " + (int)(Damage * 0.95) + " → " + (int)(Damage * 1.05) + " " + Types[Data.Item2]);
                }
            }
        }
    }
}
