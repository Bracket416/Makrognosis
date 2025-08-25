using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Makrognosis.Events
{
    internal class Service
    {
        [PluginService] internal static IGameInteropProvider GameInteropProvider { get; private set; }

        internal static void Initialize(IDalamudPluginInterface Interface)
        {
            Interface.Create<Service>();
        }

    }
}
