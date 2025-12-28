using Dalamud.Game;
using Dalamud.Hooking;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.Havok.Common.Base.Math.QsTransform;
using Lumina.Data.Parsing;
using Makrognosis.Windows;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Makrognosis.Events
{
    internal unsafe class Capture : IDisposable
    {
        private Plugin P;

        private Dictionary<uint, List<Tuple<double, int>>> Damage = new();

        private unsafe delegate void ProcessPacketActionEffectDelegate(
    uint casterEntityId, Character* casterPtr, Vector3* targetPos, ActionEffectHandler.Header* header, ActionEffectHandler.TargetEffects* effects,
    GameObjectId* targetEntityIds);

        private readonly Hook<ProcessPacketActionEffectDelegate> Action_Hook;

        public static IClientState Client;

        public static IPluginLog Log;


        public unsafe Capture(Plugin P, IDalamudPluginInterface Interface, ISigScanner S)
        {

            Service.Initialize(Interface);

            this.P = P;

            Service.GameInteropProvider.InitializeFromAttributes(this);

            Action_Hook =
                Service.GameInteropProvider.HookFromSignature<ProcessPacketActionEffectDelegate>(ActionEffectHandler.Addresses.Receive.String,
                    ProcessPacketActionEffectDetour);
            Action_Hook.Enable();
        }
        private unsafe void ProcessPacketActionEffectDetour(uint User_ID, Character* User_Pointer, Vector3* Target_Position, ActionEffectHandler.Header* Header, ActionEffectHandler.TargetEffects* Effects, GameObjectId* Target_IDs)
        {
            Action_Hook.Original(User_ID, User_Pointer, Target_Position, Header, Effects, Target_IDs);
            if (Header is null) return;
            try
            {
                if (Header->NumTargets > 0 && Client.LocalPlayer is not null)
                {
                    var ID = Header->SpellId;
                    if (ID == 7560) UI.Target_Timers[1203] = 14.8;
                    if (ID == 7549) UI.Target_Timers[1195] = 14.8;
                    if (ID == 7535) UI.Target_Timers[1193] = 14.8;
                    for (var I = 0; I < Header->NumTargets; I++) if ((uint)(Target_IDs[I] & uint.MaxValue) == Client.LocalPlayer.GameObjectId)
                        {
                            var S = Client.LocalPlayer.MaxHp * Client.LocalPlayer.ShieldPercentage / 100.0;
                            var Shield_Delta = S - P.Get_Shield();
                            for (var J = 0; J < 8; J++)
                            {
                                ref var Effect = ref Effects[I].Effects[J];
                                if (Effect.Type == 0) return;
                                uint Total = Effect.Value;
                                if ((Effect.Param4 & 0x40) == 0x40)
                                    Total += (uint)Effect.Param3 << 16;
                                if (0 < Effect.Type && Effect.Type < 7 && Effect.Type != 4)
                                {
                                    var Type = (Effect.Param1 & 0xF);
                                    if (!Damage.ContainsKey(ID)) Damage.Add(ID, new List<Tuple<double, int>> { });
                                    if (Total + Shield_Delta > 0) Damage[ID].Add(Tuple.Create(Total + Shield_Delta, Type == 5 ? 2 : (Type == 7 ? 1 : 0)));
                                }
                            }
                            break;
                        }
                }
            }
            catch (Exception Error)
            {
                Log.Error(Error.Message);
                // Something happened here...
            }
        }

        public Dictionary<uint, List<Tuple<double, int>>> Get_Damage()
        {
            Dictionary<uint, List<Tuple<double, int>>> Previous_Damage = new();
            foreach (var Mechanic in Damage.Keys)
            {
                Previous_Damage.Add(Mechanic, new List<Tuple<double, int>> { });
                foreach (var Instance in Damage[Mechanic]) Previous_Damage[Mechanic].Add(Instance);
                Damage[Mechanic] = new List<Tuple<double, int>>();
            }
            return Previous_Damage;
        }

        public void Update(IFramework F)
        {
            if (Client.LocalPlayer is not null)
            {
                var Battle = (BattleChara*)Client.LocalPlayer.Address;
                if (Battle is not null) if (!Battle->InCombat && Damage.Count > 0) Clear();
            }
        }

        public void Clear() => Damage.Clear();

        public void Dispose()
        {
            Action_Hook.Dispose();
        }

    }
}
