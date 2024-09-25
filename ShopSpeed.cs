using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using ShopAPI;

namespace ShopSpeed
{
    public class ShopSpeed : BasePlugin
    {
        public override string ModuleName => "[SHOP] Speed";
        public override string ModuleDescription => "";
        public override string ModuleAuthor => "E!N";
        public override string ModuleVersion => "v1.0.1";

        private IShopApi? SHOP_API;
        private const string CategoryName = "Speed";
        public static JObject? JsonSpeed { get; private set; }
        private readonly PlayerSpeed[] playerSpeeds = new PlayerSpeed[65];

        public override void OnAllPluginsLoaded(bool hotReload)
        {
            SHOP_API = IShopApi.Capability.Get();
            if (SHOP_API == null) return;

            LoadConfig();
            InitializeShopItems();
            SetupTimersAndListeners();
        }

        private void LoadConfig()
        {
            string configPath = Path.Combine(ModuleDirectory, "../../configs/plugins/Shop/Speed.json");
            if (File.Exists(configPath))
            {
                JsonSpeed = JObject.Parse(File.ReadAllText(configPath));
            }
        }

        private void InitializeShopItems()
        {
            if (JsonSpeed == null || SHOP_API == null) return;

            SHOP_API.CreateCategory(CategoryName, "Скорость");

            foreach (var item in JsonSpeed.Properties().Where(p => p.Value is JObject))
            {
                Task.Run(async () =>
                {
                    int itemId = await SHOP_API.AddItem(
                        item.Name,
                        (string)item.Value["name"]!,
                        CategoryName,
                        (int)item.Value["price"]!,
                        (int)item.Value["sellprice"]!,
                        (int)item.Value["duration"]!
                    );
                    SHOP_API.SetItemCallbacks(itemId, OnClientBuyItem, OnClientSellItem, OnClientToggleItem);
                }).Wait();
            }
        }

        private void SetupTimersAndListeners()
        {
            RegisterListener<Listeners.OnClientDisconnect>(playerSlot => playerSpeeds[playerSlot] = null!);

            RegisterEventHandler<EventPlayerHurt>(PrePlayerHurtHandler);
        }

        public HookResult OnClientBuyItem(CCSPlayerController player, int itemId, string categoryName, string uniqueName,
            int buyPrice, int sellPrice, int duration, int count)
        {
            if (TryGetSpeedModifierValue(uniqueName, out float speedModifierValue))
            {
                playerSpeeds[player.Slot] = new PlayerSpeed(speedModifierValue, itemId);
                ApplySpeedModifier(player, speedModifierValue);
            }
            else
            {
                Logger.LogError($"{uniqueName} has invalid or missing 'speedmodifier' in config!");
            }
            return HookResult.Continue;
        }

        public HookResult OnClientToggleItem(CCSPlayerController player, int itemId, string uniqueName, int state)
        {
            if (state == 1 && TryGetSpeedModifierValue(uniqueName, out float speedModifierValue))
            {
                playerSpeeds[player.Slot] = new PlayerSpeed(speedModifierValue, itemId);
                ApplySpeedModifier(player, speedModifierValue);
            }
            else if (state == 0)
            {
                OnClientSellItem(player, itemId, uniqueName, 0);
            }
            return HookResult.Continue;
        }

        public HookResult OnClientSellItem(CCSPlayerController player, int itemId, string uniqueName, int sellPrice)
        {
            playerSpeeds[player.Slot] = null!;
            ApplySpeedModifier(player, 1.0f); // Reset speed
            return HookResult.Continue;
        }


        private HookResult PrePlayerHurtHandler(EventPlayerHurt @event, GameEventInfo info)
        {
            CCSPlayerController? player = @event.Userid;

            if (player == null) return HookResult.Continue;
            if (playerSpeeds[player.Slot] == null) return HookResult.Continue;

            ApplySpeedModifier(player, playerSpeeds[player.Slot].SpeedModifier);
            return HookResult.Continue;
        }

        private static void ApplySpeedModifier(CCSPlayerController player, float speedModifierValue)
        {
            if (player == null || !player.IsValid) return;
            var playerPawn = player.PlayerPawn.Value;
            if (playerPawn != null)
            {
                playerPawn.VelocityModifier = speedModifierValue;
                Utilities.SetStateChanged(player, "CCSPlayerPawn", "m_flVelocityModifier");
            }
        }

        private static bool TryGetSpeedModifierValue(string uniqueName, out float speedModifierValue)
        {
            speedModifierValue = 0f;
            if (JsonSpeed != null && JsonSpeed.TryGetValue(uniqueName, out var obj) && obj is JObject jsonItem && jsonItem["speedmodifier"] != null && jsonItem["speedmodifier"]!.Type != JTokenType.Null)
            {
                speedModifierValue = float.Parse(jsonItem["speedmodifier"]!.ToString());
                return true;
            }
            return false;
        }

        public record class PlayerSpeed(float SpeedModifier, int ItemID);
    }
}