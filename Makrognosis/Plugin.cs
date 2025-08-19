using Dalamud.Game.Command;
using Dalamud.Game.Text;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Makrognosis.Windows;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using System.IO;
using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Utility;

namespace Makrognosis;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IObjectTable Objects { get; private set; } = null!;
    [PluginService] internal static IPartyList Party { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    [PluginService] internal static IChatGui Chat { get; private set; } = null!;

    private const string CommandName = "/makro";

    public Configuration Configuration { get; init; }

    public readonly WindowSystem WindowSystem = new("Makrognosis");
    private ConfigWindow ConfigWindow { get; init; }
    private MainWindow MainWindow { get; init; }

    private UI Drawing = new();

    private Dictionary<string, string> Casts = new();

    private string Last_Caster = "";

    public Tuple<double, int> Average(string Name)
    {
        return Drawing.Get_Mechanic_Average(Name);
    }

    private void Message(XivChatType type, int timestamp, ref SeString sender, ref SeString message, ref bool isHandled)
    {
        if (sender.TextValue.Length == 0)
        {
            var Filtered = "";
            foreach (var Word in message.TextValue.Replace("Parried! ", "").Replace("Blocked!", "").Split(" "))
            {
                for (int I = 0; I < Word.Length; I++) if (Word.ToLower().ToCharArray()[I] != Word.ToUpper().ToCharArray()[I] || "0123456789".Contains(Word.ToCharArray()[I]) || "()".Contains(Word.ToCharArray()[I])) Filtered += Word.ToCharArray()[I];
                Filtered += " ";
            }
            Filtered = $" {Filtered.Trim()} ".Replace(" The ", "").Trim();

            var Enemy = Filtered.Contains(" hits you ") ? Filtered.Split(" hits you ")[0] : Filtered.Split(" readies ")[0].Split(" casts ")[0].Split(" uses ")[0].Split(" take ")[0];

            var Final_Name = "";
            foreach (var Word in Enemy.Split(" ")) Final_Name += (Word == "of" ? "of" : Word.FirstCharToUpper()) + " ";

            Enemy = Final_Name.Trim();

            if (Filtered.StartsWith("You are defeated"))
            {
                Drawing.Damage = new();
                Configuration.Mechanics = Drawing.Mechanics;
                Configuration.Save();
            }
            if (Filtered.StartsWith("You gain the effect of ")) Drawing.Gained_Effects.Add(Filtered.Split("You gain the effect of ")[1]);
            if (Drawing.Target != null && ClientState.LocalPlayer != null)
                if (Drawing.Target.Name.TextValue == Enemy || Enemy == "You")
                {
                    if (Filtered.EndsWith(" damage"))
                    {
                        if (Filtered.Contains("hits you for"))
                        {
                            var Damage = int.Parse(Filtered.Split(" damage")[0].Split(" ")[^1].Split("(")[0]);
                            var Type = 0;
                            if (message.Payloads.Count == 3)
                            {
                                if (((Dalamud.Game.Text.SeStringHandling.Payloads.IconPayload)message.Payloads[1]).Icon.ToString().Contains("Magical"))
                                {
                                    Type = 2;
                                }
                                else Type = 1;
                            }
                            var D = (new List<double> { Drawing.Total_Mitigation, Drawing.Total_Physical, Drawing.Total_Magical });
                            if (Drawing.Previous_Shield > 0.0)
                            {
                                Drawing.Damage_Queue.Add(Tuple.Create((int)Math.Ceiling(Damage * D[Type] / (message.TextValue.StartsWith("Parried!") || message.TextValue.StartsWith("Blocked!") ? 0.85 : 1.0)), Type, TimeProvider.System.GetTimestamp()));
                            }
                            else Drawing.Damage.Add(Tuple.Create((int)Math.Ceiling(Damage * D[Type] / (message.TextValue.StartsWith("Parried!") || message.TextValue.StartsWith("Blocked!") ? 0.85 : 1.0)), Type, TimeProvider.System.GetTimestamp()));
                        }
                        else if (Filtered.StartsWith("You take"))
                        {
                            var Damage = int.Parse(Filtered.Split(" damage")[0].Split(" ")[^1].Split("(")[0]);
                            var Type = 0;
                            if (message.Payloads.Count == 3)
                            {
                                if (((Dalamud.Game.Text.SeStringHandling.Payloads.IconPayload)message.Payloads[1]).Icon.ToString().Contains("Magical"))
                                {
                                    Type = 2;
                                }
                                else Type = 1;
                            }

                            if (Casts.ContainsKey(Last_Caster))
                            {
                                if (!Drawing.Mechanics.ContainsKey(Casts[Last_Caster])) Drawing.Mechanics.Add(Casts[Last_Caster], []);
                                var D = (new List<double> { Drawing.Total_Mitigation, Drawing.Total_Physical, Drawing.Total_Magical });
                                if (Drawing.Previous_Shield > 0.0)
                                {
                                    Drawing.Mechanic_Queue.Add(Tuple.Create(Casts[Last_Caster], (int)Math.Ceiling(Damage * D[Type] / (message.TextValue.StartsWith("Parried!") || message.TextValue.StartsWith("Blocked!") ? 0.85 : 1.0)), Type));
                                }
                                else Drawing.Mechanics[Casts[Last_Caster]].Add(Tuple.Create((int)Math.Ceiling(Damage * D[Type] / (message.TextValue.StartsWith("Parried!") || message.TextValue.StartsWith("Blocked!") ? 0.85 : 1.0)), Type));
                            }
                        }
                    }
                    else if (Filtered.Contains(" readies ") || Filtered.Contains(" uses ") || Filtered.Contains(" casts "))
                    {
                        Casts[Enemy] = Filtered.Split(" readies ")[^1].Split(" casts ")[^1].Split(" uses ")[^1];
                        Log.Information($"{Enemy} is casting {Casts[Enemy]}!");
                        if (!Drawing.Mechanics.ContainsKey(Casts[Enemy])) Drawing.Mechanics.Add(Casts[Enemy], []);
                        Last_Caster = Enemy;
                        Drawing.Current_Cast = Casts[Enemy];
                    }
                }
        }
    }

    public Plugin(IDalamudPluginInterface I)
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();


        // You might normally want to embed resources and load them from the manifest stream
        ConfigWindow = new ConfigWindow(this);
        MainWindow = new MainWindow(this);

        Drawing.C = Configuration;
        Drawing.Mechanics = Configuration.Mechanics ?? new();
        UI.Objects = Objects;
        UI.State = ClientState;
        UI.Main = MainWindow;
        UI.Log = Log;
        Chat.ChatMessage += Message;
        WindowSystem.AddWindow(ConfigWindow);
        WindowSystem.AddWindow(MainWindow);
        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "A useful message to display in /xlhelp"
        });

        PluginInterface.UiBuilder.Draw += DrawUI;

        // This adds a button to the plugin installer entry of this plugin which allows
        // toggling the display status of the configuration ui
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUI;

        // Adds another button doing the same but for the main ui of the plugin
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUI;

        // Add a simple message to the log with level set to information
        // Use /xllog to open the log window in-game
        // Example Output: 00:57:54.959 | INF | [Makrognosis] ===A cool log message from Sample Plugin===
        //Log.Information($"===A cool log message from {PluginInterface.Manifest.Name}===");
    }

    public void Dispose()
    {
        WindowSystem.RemoveAllWindows();
        ConfigWindow.Dispose();
        MainWindow.Dispose();
        Drawing.Dispose();

        CommandManager.RemoveHandler(CommandName);
    }

    private void OnCommand(string command, string args)
    {
        // In response to the slash command, toggle the display status of our main ui
        ToggleMainUI();
    }

    private void DrawUI()
    {
        MainWindow.P = Drawing.Local_Position;
        Drawing.Draw();
        WindowSystem.Draw();
    }

    public void ToggleConfigUI() => ConfigWindow.Toggle();
    public void ToggleMainUI() => MainWindow.Toggle();
}
