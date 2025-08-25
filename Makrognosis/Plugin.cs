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

    private Events.Capture C;

    public double Get_Shield()
    {

        var Previous = Drawing.Previous_Distinct_Shield;
        Drawing.Previous_Distinct_Shield = ClientState.LocalPlayer.MaxHp * ClientState.LocalPlayer.ShieldPercentage / 100.0;
        return Previous;
    }

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

            var Enemy = Filtered.Contains(" hits you ") ? Filtered.Split(" hits you ")[0] : Filtered.Split(" readies ")[0].Split(" casts ")[0].Split(" uses ")[0].Split(" begins casting ")[0].Split(" take ")[0];

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
                if (Drawing.Target.Name.TextValue == Enemy || Enemy == "You") if (Filtered.Contains(" readies ") || Filtered.Contains(" uses ") || Filtered.Contains(" casts ") || Filtered.Contains(" begins casting "))
                    {
                        Casts[Enemy] = Filtered.Split(" readies ")[^1].Split(" casts ")[^1].Split(" uses ")[^1].Split(" begins casting ")[^1];
                        Log.Information($"{Enemy} is casting {Casts[Enemy]}!");
                        if (!Drawing.Mechanics.ContainsKey(Casts[Enemy])) Drawing.Mechanics.Add(Casts[Enemy], []);
                        Drawing.Current_Cast = Casts[Enemy];
                    }
        }
    }

    public Plugin(IDalamudPluginInterface I)
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        Events.Capture.Log = Log;
        Events.Capture.Client = ClientState;
        C = new(this, I);


        // You might normally want to embed resources and load them from the manifest stream
        ConfigWindow = new ConfigWindow(this);
        MainWindow = new MainWindow(this);

        Drawing.C = Configuration;
        Drawing.Mechanics = Configuration.Mechanics ?? new();

        UI.Objects = Objects;
        UI.State = ClientState;
        UI.Main = MainWindow;
        UI.Log = Log;
        Drawing.Current_Capture = C;
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
