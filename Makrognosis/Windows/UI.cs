using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.Sheets;
using Makrognosis.Events;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using static Dalamud.Interface.Utility.Raii.ImRaii;

namespace Makrognosis.Windows
{
    unsafe internal class UI : IDisposable
    {

        public Capture Current_Capture;

        public Configuration C = new();

        public static IClientState State;

        private static readonly Lumina.Excel.ExcelSheet<Lumina.Excel.Sheets.Status> Reference = Plugin.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Status>();

        public static MainWindow Main;

        private static Dictionary<uint, Tuple<uint, uint>> Mitigations = new();

        public static IObjectTable Objects;

        public static IPartyList Party;

        public Vector2 Local_Position = new Vector2();

        public Dalamud.Game.ClientState.Objects.Types.IBattleNpc Target;

        public List<Tuple<int, int, long>> Damage = new();

        public Dictionary<string, List<Tuple<int, int>>> Mechanics = new();

        public string Current_Cast = "";

        public double Total_Mitigation = 1.0;

        public double Total_Physical = 1.0;

        public double Total_Magical = 1.0;

        public double Previous_Shield = -1.0;

        public double Previous_Distinct_Shield = 0.0;

        public double Compiled_Loss = 0;

        public List<Tuple<int, int, long>> Damage_Queue = new();

        public List<Tuple<string, int, int>> Mechanic_Queue = new();

        public Dictionary<string, Tuple<double, double>> Shields = new();

        private List<string> New_Shields = new();

        private double Compiled_Gain = 0;

        public List<string> Gained_Effects = new();

        public static IPluginLog Log;

        public double Defense = 1.0;
        public double Magical_Defense = 1.0;
        public double Tenacity = 1.0;
        public UI()
        {

            List<KeyValuePair<uint, Tuple<uint, uint>>> Mitigation_List = new();

            var Lines = """
                194 20 20
                195 40 40
                196 80 80
                863 80 80
                864 80 80
                1931 80 80
                1191 20 20
                1193 10 10
                1174 10 10
                1176 15 15
                2678 10 10
                2679 10 10
                2674 15 15
                2675 15 15
                746 10 20
                1894 10 20
                2682 10 10
                2829 40 40
                3832 40 40
                3829 40 40
                3835 40 40
                1834 30 30
                1873 10 10
                2708 15 15
                317 0 5
                1875 0 5
                299 10 10
                2711 10 10
                849 10 10
                2717 10 10
                3896 10 10
                3890 10 10
                2618 10 10
                2619 10 10
                3003 10 10
                1195 10 5
                3853 10 10
                3854 10 10
                1951 15 15
                860 10 10
                1826 15 15
                1203 5 10
                2707 0 10
                """.Trim();

            foreach (var Line in Lines.Split("\n"))
            {
                var Split_Line = Line.Split(" ");
                if (Split_Line.Length == 3) Mitigation_List.Add(KeyValuePair.Create(uint.Parse(Split_Line[0]), Tuple.Create(uint.Parse(Split_Line[1]), uint.Parse(Split_Line[2]))));
            }

            Mitigations = new(Mitigation_List);
        }

        public List<Tuple<int, int>> Clean(List<Tuple<int, int>> Mechanic, double Range)
        {
            var Output = new List<Tuple<int, int>>();
            if (Mechanic.Count == 0) return Output;
            var Average = 0.0;

            foreach (var Instance in Mechanic) Average += Instance.Item1;

            Average /= Mechanic.Count;

            var Closest = int.MinValue;

            foreach (var Instance in Mechanic) if (Math.Abs(Instance.Item1 - Average) < Math.Abs(Closest - Average)) Closest = Instance.Item1;

            foreach (var Instance in Mechanic) if (Math.Abs(Closest - Instance.Item1) < Closest * Range) Output.Add(Instance);

            return Output;
        }
        public Tuple<double, int> Get_Mechanic_Average(string Name)
        {
            if (!Mechanics.ContainsKey(Name)) return Tuple.Create(0.0, 0);
            if (Mechanics[Name].Count == 0) return Tuple.Create(0.0, 0);
            var Cleaned = Clean(Mechanics[Name], 0.05);
            var Sum = 0.0;
            if (Cleaned.Count == 0) return new Tuple<double, int>(0.0, 0);
            foreach (var D in Cleaned) Sum += D.Item1;
            return Tuple.Create(Sum / Cleaned.Count, Mechanics[Name][0].Item2);
        }
        public class Modifiers
        {
            public int Main = 0;
            public int Sub = 0;
            public int Div = 0;
            public Modifiers(int M, int S, int D) { Main = M; Sub = S; Div = D; }
        }

        private static List<Modifiers> Level_Modifiers = new List<Modifiers> { new Modifiers(20, 56, 56),
new Modifiers(21, 57, 57),
new Modifiers(22, 60, 60),
new Modifiers(24, 62, 62),
new Modifiers(26, 65, 65),
new Modifiers(27, 68, 68),
new Modifiers(29, 70, 70),
new Modifiers(31, 73, 73),
new Modifiers(33, 76, 76),
new Modifiers(35, 78, 78),
new Modifiers(36, 82, 82),
new Modifiers(38, 85, 85),
new Modifiers(41, 89, 89),
new Modifiers(44, 93, 93),
new Modifiers(46, 96, 96),
new Modifiers(49, 100, 100),
new Modifiers(52, 104, 104),
new Modifiers(54, 109, 109),
new Modifiers(57, 113, 113),
new Modifiers(60, 116, 116),
new Modifiers(63, 122, 122),
new Modifiers(67, 127, 127),
new Modifiers(71, 133, 133),
new Modifiers(74, 138, 138),
new Modifiers(78, 144, 144),
new Modifiers(81, 150, 150),
new Modifiers(85, 155, 155),
new Modifiers(89, 162, 162),
new Modifiers(92, 168, 168),
new Modifiers(97, 173, 173),
new Modifiers(101, 181, 181),
new Modifiers(106, 188, 188),
new Modifiers(110, 194, 194),
new Modifiers(115, 202, 202),
new Modifiers(119, 209, 209),
new Modifiers(124, 215, 215),
new Modifiers(128, 223, 223),
new Modifiers(134, 229, 229),
new Modifiers(139, 236, 236),
new Modifiers(144, 244, 244),
new Modifiers(150, 253, 253),
new Modifiers(155, 263, 263),
new Modifiers(161, 272, 272),
new Modifiers(166, 283, 283),
new Modifiers(171, 292, 292),
new Modifiers(177, 302, 302),
new Modifiers(183, 311, 311),
new Modifiers(189, 322, 322),
new Modifiers(196, 331, 331),
new Modifiers(202, 341, 341),
new Modifiers(204, 342, 366),
new Modifiers(205, 344, 392),
new Modifiers(207, 345, 418),
new Modifiers(209, 346, 444),
new Modifiers(210, 347, 470),
new Modifiers(212, 349, 496),
new Modifiers(214, 350, 522),
new Modifiers(215, 351, 548),
new Modifiers(217, 352, 574),
new Modifiers(218, 354, 600),
new Modifiers(224, 355, 630),
new Modifiers(228, 356, 660),
new Modifiers(236, 357, 690),
new Modifiers(244, 358, 720),
new Modifiers(252, 359, 750),
new Modifiers(260, 360, 780),
new Modifiers(268, 361, 810),
new Modifiers(276, 362, 840),
new Modifiers(284, 363, 870),
new Modifiers(292, 364, 900),
new Modifiers(296, 365, 940),
new Modifiers(300, 366, 980),
new Modifiers(305, 367, 1020),
new Modifiers(310, 368, 1060),
new Modifiers(315, 370, 1100),
new Modifiers(320, 372, 1140),
new Modifiers(325, 374, 1180),
new Modifiers(330, 376, 1220),
new Modifiers(335, 378, 1260),
new Modifiers(340, 380, 1300),
new Modifiers(345, 382, 1360),
new Modifiers(350, 384, 1420),
new Modifiers(355, 386, 1480),
new Modifiers(360, 388, 1540),
new Modifiers(365, 390, 1600),
new Modifiers(370, 392, 1660),
new Modifiers(375, 394, 1720),
new Modifiers(380, 396, 1780),
new Modifiers(385, 398, 1840),
new Modifiers(390, 400, 1900),
new Modifiers(395, 402, 1988),
new Modifiers(400, 404, 2076),
new Modifiers(405, 406, 2164),
new Modifiers(410, 408, 2252),
new Modifiers(415, 410, 2340),
new Modifiers(420, 412, 2428),
new Modifiers(425, 414, 2516),
new Modifiers(430, 416, 2604),
new Modifiers(435, 418, 2692),
new Modifiers(440, 420, 2780) };

        public static double Physical_Mitigation(int Defense, int Level) => Math.Floor(15.0 * Defense / Level_Modifiers[Level - 1].Div) / 100.0;

        public static double Magical_Mitigation(int Magical_Defense, int Level) => Math.Floor(15.0 * Magical_Defense / Level_Modifiers[Level - 1].Div) / 100.0;

        public static double Tenacity_Mitigation(int Tenacity, int Level) => Math.Floor(200.0 * (Tenacity - Level_Modifiers[Level - 1].Sub) / Level_Modifiers[Level - 1].Div) / 1000.0;
        public void Draw()
        {

            if (State.LocalPlayer != null)
            {

                var Attributes = UIState.Instance();
                if (Attributes->PlayerState.Attributes.Length > 24)
                {
                    Defense = 1.0 - Physical_Mitigation(Attributes->PlayerState.Attributes[21], Attributes->PlayerState.CurrentLevel);
                    Magical_Defense = 1.0 - Magical_Mitigation(Attributes->PlayerState.Attributes[24], Attributes->PlayerState.CurrentLevel);
                    Tenacity = 1.0 - Tenacity_Mitigation(Attributes->PlayerState.Attributes[19], Attributes->PlayerState.CurrentLevel);
                }

                if (Attributes->PlayerState.CurrentClassJobId == 19 || Attributes->PlayerState.CurrentClassJobId == 21 || Attributes->PlayerState.CurrentClassJobId == 32 || Attributes->PlayerState.CurrentClassJobId == 37) Tenacity /= 0.8;

                ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0);
                ImGui.SetNextWindowPos(C.Position, ImGuiCond.FirstUseEver);
                ImGui.SetNextWindowSize(new Vector2(205, 135));

                ImGui.SetNextWindowBgAlpha((float)0.75);

                var Flags = ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.AlwaysAutoResize;

                if (!Main.Configuring) Flags |= ImGuiWindowFlags.NoInputs;

                ImGui.Begin("##Makro UI", Flags);


                var Captured_Damage = Current_Capture.Get_Damage();

                foreach (var M in Captured_Damage.Keys) foreach (var Instance in Captured_Damage[M])
                    {
                        var Name = M.ToString();
                        if (Plugin.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Action>().TryGetRow(M, out var N)) Name = N.Name.ExtractText();
                        Name = (Name.Length == 0 || Name == "Attack" ? "Auto" : Name);
                        var Mitigation = (new double[] { Total_Mitigation, Total_Physical, Total_Magical })[Instance.Item2];
                        if (Name == "Auto")
                        {
                            Damage.Add(Tuple.Create((int) Math.Ceiling(Instance.Item1 * Mitigation), Instance.Item2, TimeProvider.System.GetTimestamp()));
                        } else
                        {
                            if (!Mechanics.ContainsKey(Name)) Mechanics.Add(Name, new List<Tuple<int, int>> { });
                            Mechanics[Name].Add(Tuple.Create((int)Math.Ceiling(Instance.Item1 * Mitigation), Instance.Item2));
                        }
                            Log.Information($"{Name}: " + (Instance.Item1 * Mitigation));
                    }

                var Shield = State.LocalPlayer.MaxHp * State.LocalPlayer.ShieldPercentage / 100.0;

                if (Previous_Shield == -1.0) Previous_Shield = Shield;

                if (Previous_Shield != Shield) Previous_Distinct_Shield = Previous_Shield;

                //var Shield_Increase = Math.Max(0, Shield - Previous_Shield);
                //
                //Compiled_Gain += Shield_Increase;
                //
                //var All_Shields = new List<string>();
                //
                //foreach (var Status in State.LocalPlayer.StatusList)
                //{
                //    Reference.TryGetRow(Status.StatusId, out var S);
                //    if (S.Description.ToString().ToLower().Contains(" nulli"))
                //    {
                //        var Name = S.Name.ExtractText();
                //        if (!Shields.ContainsKey(Name)) Shields.Add(Name, Tuple.Create(0.0, 0.0));
                //        if (Shields[Name].Item2 == 0.0 || Shields[Name].Item2 < Status.RemainingTime) if (!New_Shields.Contains(Name))
                //            {
                //                New_Shields.Add(Name);
                //                // Log.Information("You gained " + Name + "!");
                //            }
                //        Shields[Name] = Tuple.Create(Shields[Name].Item1, Status.RemainingTime + 0.0);
                //        All_Shields.Add(Name);
                //    }
                //}
                //Gained_Effects.Clear();
                //foreach (var S in Shields.Keys) if (!All_Shields.Contains(S)) Shields[S] = Tuple.Create(Shields[S].Item1, 0.0);
                //
                //if (Compiled_Gain > 0.0)
                //{
                //    if (New_Shields.Count > 0)
                //    {
                //        Compiled_Gain /= New_Shields.Count;
                //        foreach (var S in New_Shields)
                //        {
                //            Shields[S] = Tuple.Create(Shields[S].Item1 + Compiled_Gain, Shields[S].Item2);
                //            //Log.Information(S + $" ({(int)Shields[S].Item2}): " + Shields[S].Item1);
                //        }
                //        Compiled_Gain = 0;
                //        New_Shields.Clear();
                //    }
                //}
                //
                //var Shield_Damage = Math.Max(0, Previous_Shield - Shield);
                //foreach (var S in Shields.Keys) if (Shields[S].Item2 == 0.0)
                //    {
                //        Shield_Damage -= Shields[S].Item1;
                //        Shields[S] = Tuple.Create(0.0, 0.0);
                //    }
                //var Running_Damage = Shield_Damage;
                //foreach (var S in Shields.Keys)
                //{
                //    if (Shields[S].Item2 != 0.0)
                //    {
                //        var T = Shields[S].Item1 - Running_Damage;
                //        Shields[S] = Tuple.Create(Math.Max(0, T), Shields[S].Item2);
                //        Running_Damage -= Math.Min(0, T);
                //    }
                //    //if (Shields[S].Item2 > 0)
                //    //    Log.Information(S + $" ({(int)Shields[S].Item2}): " + Shields[S].Item1);
                //}
                //
                //
                //Compiled_Loss += Shield_Damage;
                //
                //Compiled_Loss = Math.Max(0, Compiled_Loss);
                //
                //if ((Damage_Queue.Count + Mechanic_Queue.Count) > 0) Compiled_Loss /= (Damage_Queue.Count + Mechanic_Queue.Count);
                //
                //if (Compiled_Loss > 0 && Damage_Queue.Count + Mechanic_Queue.Count > 0)
                //{
                //    //Log.Information(Compiled_Loss + "");
                //    var D = (new List<double> { Total_Mitigation, Total_Physical, Total_Magical });
                //    foreach (var Item in Damage_Queue) Damage.Add(Tuple.Create(Item.Item1 + (int)Math.Ceiling(Compiled_Loss * D[Item.Item2]), Item.Item2, Item.Item3));
                //    foreach (var Item in Mechanic_Queue) Mechanics[Item.Item1].Add(Tuple.Create(Item.Item2 + (int)Math.Ceiling(Compiled_Loss * D[Item.Item3]), Item.Item3));
                //    Compiled_Loss = 0;
                //    Damage_Queue.Clear();
                //    Mechanic_Queue.Clear();
                //}


                Previous_Shield = Shield;

                var Total = Math.Floor((double)(State.LocalPlayer.CurrentHp + Shield));

                var Original = Total;

                var Physical = Total / Defense / Tenacity;

                var Magical = Total / Magical_Defense / Tenacity;

                var Statuses = new List<string>();

                Total_Mitigation = 1.0 / Tenacity / Math.Max(Defense, Magical_Defense);

                Total_Physical = 1.0 / Defense / Tenacity;

                Total_Magical = 1.0 / Magical_Defense / Tenacity;
                
                var L = new List<Dalamud.Game.ClientState.Statuses.Status>();

                if (State.LocalPlayer.TargetObject != null && State.LocalPlayer.TargetObject is Dalamud.Game.ClientState.Objects.Types.IBattleNpc) Target = (Dalamud.Game.ClientState.Objects.Types.IBattleNpc)State.LocalPlayer.TargetObject;
                //if (State.LocalPlayer.TargetObject != null && State.LocalPlayer.TargetObject is Dalamud.Game.ClientState.Objects.Types.IBattleChara) foreach (var Status in ((Dalamud.Game.ClientState.Objects.Types.IBattleChara) State.LocalPlayer.TargetObject).StatusList) L.Add(Status);


                if (Target != null && Objects.Contains(Target)) if (Target.StatusList != null) foreach (var Status in Target.StatusList) L.Add(Status);
                foreach (var Status in State.LocalPlayer.StatusList) L.Add(Status);
                foreach (var Status in L)
                {
                    if (Mitigations.ContainsKey(Status.StatusId))
                    {
                        if (Mitigations[Status.StatusId].Item1 == Mitigations[Status.StatusId].Item2) Total /= (1.0 - Mitigations[Status.StatusId].Item1 / 100.0);
                        if (Mitigations[Status.StatusId].Item1 == Mitigations[Status.StatusId].Item2) Total_Mitigation /= (1.0 - Mitigations[Status.StatusId].Item1 / 100.0);
                        Physical /= (1.0 - Mitigations[Status.StatusId].Item1 / 100.0);
                        Total_Physical /= (1.0 - Mitigations[Status.StatusId].Item1 / 100.0);
                        Magical /= (1.0 - Mitigations[Status.StatusId].Item2 / 100.0);
                        Total_Magical /= (1.0 - Mitigations[Status.StatusId].Item2 / 100.0);
                    }
                    Reference.TryGetRow(Status.StatusId, out var S);
                    //if (S.Description.ToString().ToLower().Contains("damage taken is reduced") || S.Description.ToString().ToLower().Contains("is nullifying damage") || S.Description.ToString().ToLower().Contains("damage are reduced") || S.Description.ToString().ToLower().Contains("damage is reduced") || S.Description.ToString().ToLower().Contains("damage dealt is reduced"))
                    Statuses.Add(S.Name.ExtractText() + $" ({Status.StatusId})");
                }

                Total = Math.Floor(Total / Math.Max(Defense, Magical_Defense) / Tenacity);
                Physical = Math.Floor(Physical);
                Magical = Math.Floor(Magical);
                //var Average_Defense = Math.Min(Defense, Magical_Defense) / Math.Max(Defense, Magical_Defense);
                UInt32[] Color_Category = new UInt32[] { 0xFF0000FF, 0xFF00FFFF, (uint)(0.9924 * uint.MaxValue) };
                ImGui.TextColored(Color_Category[(int)(Math.Min(Color_Category.Length - 1, Math.Max(0, Color_Category.Length * Total / Math.Floor(State.LocalPlayer.MaxHp / Math.Max(Defense, Magical_Defense) / Tenacity))))], "Total: " + (int)Math.Ceiling(Total * (C.Raw ? 1.0 : Tenacity * Math.Max(Defense, Magical_Defense))) + $" ({(int)Math.Round(100.0 * (1.0 - (1.0 / (Total_Mitigation * (C.Raw ? 1.0 : Tenacity * Math.Max(Defense, Magical_Defense))))))}%)");
                ImGui.Text("Physical: " + (int)Math.Ceiling(Physical * (C.Raw ? 1.0 : Tenacity * Defense)) + $" ({(int)Math.Round(100.0 * (1.0 - (1.0 / (Total_Physical * (C.Raw ? 1.0 : Tenacity * Defense)))))}%)");
                ImGui.Text("Magical: " + (int)Math.Ceiling(Magical * (C.Raw ? 1.0 : Tenacity * Magical_Defense)) + $" ({(int)Math.Round(100.0 * (1.0 - (1.0 / (Total_Magical * (C.Raw ? 1.0 : Tenacity * Magical_Defense)))))}%)");
                var H = (new List<double> { Total, Physical, Magical });
                var DEF = (new List<double> { Tenacity * Math.Max(Defense, Magical_Defense), Tenacity * Defense, Tenacity * Magical_Defense });
                var Mechanic = Get_Mechanic_Average(Current_Cast);
                if (Current_Cast.Length > 0)
                {
                    if ((int)(Mechanic.Item1 * 1.05) >= H[Mechanic.Item2])
                    {
                        ImGui.TextColored(0xFF0000FF, $"{Current_Cast}: " + (int)Math.Ceiling(Mechanic.Item1 * 1.05 * (C.Raw ? 1.0 : DEF[Mechanic.Item2])));
                    }
                    else ImGui.TextColored((uint)(0.9924 * uint.MaxValue), $"{Current_Cast}: " + (int)Math.Ceiling(Mechanic.Item1 * 1.05 * (C.Raw ? 1.0 : DEF[Mechanic.Item2])));
                }
                if (Damage.Count > 0)
                {
                    var Removed = new List<int>();
                    var T = TimeProvider.System.GetTimestamp();
                    var Average = 0.0;
                    var Count = 0;
                    for (var I = 0; I < Damage.Count; I++) if (T - Damage[I].Item3 > 10000)
                        {
                            Average += Damage[I].Item1;
                            Count++;
                        }
                        else
                            Removed.Add(I);

                    Average /= Count;
                    Average *= 1.05;

                    ImGui.TextColored(1.5 * Average >= Total ? 0xFF0000FF : (uint)(0.9924 * uint.MaxValue), "Auto: " + (int)Math.Ceiling(Average * (C.Raw ? 1.0 : DEF[0])));
                    ImGui.Text("ETD: " + (int)(10.0 * (Total / (Average / 3))) / 10.0 + "s");
                }

                //ImGui.Text(string.Join(", ", Statuses));

                Local_Position = ImGui.GetWindowPos();
                ImGui.End();
                ImGui.PopStyleVar();
                Main.Mechanics = Mechanics.Keys.ToList();
                Main.Defense = Defense;
                Main.Magical_Defense = Magical_Defense;
                Main.Tenacity = Tenacity;
            }
        }

        public void Dispose()
        {
        }

    }
}
