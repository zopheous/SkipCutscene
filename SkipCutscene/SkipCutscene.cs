using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Security.Cryptography;
using Dalamud;
using Dalamud.Game;
using Dalamud.Game.Command;
using Dalamud.Game.Gui;
using Dalamud.Game.Text;
using Dalamud.IoC;
using Dalamud.Logging;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;

namespace Plugins.a08381.SkipCutscene
{
    public class SkipCutscene : IDalamudPlugin
    {

        private readonly Config _config;
        private readonly RandomNumberGenerator _csp;

        private readonly decimal _base = uint.MaxValue;

        [PluginService] public static IFramework Framework { get; private set; }
        [PluginService] public static IGameGui GameGui { get; private set; }

        [SuppressMessage("ReSharper", "PossibleNullReferenceException")]
        [SuppressMessage("ReSharper", "AssignNullToNotNullAttribute")]
        public SkipCutscene()
        {
            if (Interface.GetPluginConfig() is not Config configuration || configuration.Version == 0)
                configuration = new Config { IsEnabled = true, Version = 1 };

            _config = configuration;

            Address = new CutsceneAddressResolver();

            Address.Setup(SigScanner);

            if (Address.Valid)
            {
                PluginLog.Information("Cutscene Offset Found.");
                if (_config.IsEnabled)
                    SetEnabled(true);
            }
            else
            {
                PluginLog.Error("Cutscene Offset Not Found.");
                PluginLog.Warning("Plugin Disabling...");
                Dispose();
                return;
            }
            _csp = RandomNumberGenerator.Create();
            Framework.Update += OnFrameworkUpdate;
        }

        public void Dispose()
        {
            SetEnabled(false);
            GC.SuppressFinalize(this);
        }

        public string Name => "SkipCutscene";

        [PluginService]
        public static IClientState ClientState { get; private set; }

        [PluginService]
        public static IDalamudPluginInterface Interface { get; private set; }

        [PluginService]
        public static ISigScanner SigScanner { get; private set; }

        [PluginService]
        public static ICommandManager CommandManager { get; private set; }

        [PluginService]
        public static IChatGui ChatGui { get; private set; }

        [PluginService]
        public static IPluginLog PluginLog { get; private set; }

        public CutsceneAddressResolver Address { get; }

        public void SetEnabled(bool isEnable)
        {
            if (!Address.Valid) return;
            if (isEnable)
            {
                SafeMemory.Write<short>(Address.Offset1, -28528);
                SafeMemory.Write<short>(Address.Offset2, -28528);
            }
            else
            {
                SafeMemory.Write<short>(Address.Offset1, 13173);
                SafeMemory.Write<short>(Address.Offset2, 6260);
            }
        }

        private bool viewingCutscene = false;
        private int territoryType = 0;
        private unsafe void OnFrameworkUpdate(IFramework framework)
        {
            if (ClientState.TerritoryType != territoryType)
            {
                viewingCutscene = false;
                territoryType = ClientState.TerritoryType;
            }

            var partyListPtr = GameGui.GetAddonByName("_PartyList");
            if (partyListPtr != IntPtr.Zero)
            {
                var partyList = (AddonPartyList*)partyListPtr;
                var viewingCutscene = false;
                foreach (var member in partyList->PartyMembers)
                    viewingCutscene |= member.Name->NodeText.ToString().Contains("Viewing Cutscene");
                if (!viewingCutscene && this.viewingCutscene)
                {
                    ChatGui.Print(new()
                    {
                        Type = XivChatType.Echo,
                        Name = "[Skip Cutscene]",
                        Message = "[Skip Cutscene] Party ready!"
                    });
                    UIModule.PlayChatSoundEffect(7);
                }
                this.viewingCutscene = viewingCutscene;
            }
        }
    }

    public class CutsceneAddressResolver : BaseAddressResolver
    {

        public bool Valid => Offset1 != IntPtr.Zero && Offset2 != IntPtr.Zero;

        public IntPtr Offset1 { get; private set; }
        public IntPtr Offset2 { get; private set; }

        protected override void Setup64Bit(ISigScanner sig)
        {
            Offset1 = sig.ScanText("75 33 48 8B 0D ?? ?? ?? ?? BA ?? 00 00 00 48 83 C1 10 E8 ?? ?? ?? ?? 83 78");
            Offset2 = sig.ScanText("74 18 8B D7 48 8D 0D");
            SkipCutscene.PluginLog.Information(
                "Offset1: [\"ffxiv_dx11.exe\"+{0}]",
                (Offset1.ToInt64() - Process.GetCurrentProcess().MainModule!.BaseAddress.ToInt64()).ToString("X")
                );
            SkipCutscene.PluginLog.Information(
                "Offset2: [\"ffxiv_dx11.exe\"+{0}]",
                (Offset2.ToInt64() - Process.GetCurrentProcess().MainModule!.BaseAddress.ToInt64()).ToString("X")
                );
        }

    }
}
