using System;
using System.Linq;
using HarmonyLib;
using REPOLib.Modules;

using UnityEngine;
using UnityEngine.Events;

namespace CartCompressor
{
    internal static class CompressorShop
    {
        internal static Item Item;
        private static GameObject storage;
        private static float nextRegistrationAttempt;

        internal static void Tick()
        {
            if (Item != null) BindSavedState();
            if (Item == null && Time.unscaledTime >= nextRegistrationAttempt)
            {
                nextRegistrationAttempt = Time.unscaledTime + 5;
                try { Register(); }
                catch (Exception ex) { Log.Error("Cart Compressor deferred registration failed: " + ex); }
            }
        }

        internal static void Register()
        {
            if (Item != null) { BindSavedState(); return; }
            // Load a vanilla upgrade as a runtime-only template; no game assets are distributed.
            Item source = null;
            GameObject sourcePrefab = null;
            // Item asset names differ from prefab/display names. Match the actual component,
            // never an English display string or an assumed asset filename.
            var stats = StatsManager.instance;
            if (stats == null || stats.itemDictionary.Count == 0) return;
            var candidates = stats.itemDictionary.Values.OrderBy(x => x.name, StringComparer.Ordinal).ToArray();
            foreach (var candidate in candidates)
            {
                if (candidate.itemType != SemiFunc.itemType.item_upgrade || candidate.prefab == null) continue;
                // PrefabRef resolves the game's resource/prefab pools as well as Resources.
                var candidatePrefab = candidate.prefab.Prefab;
                if (candidatePrefab == null || candidatePrefab.GetComponent<ItemUpgradePlayerGrabStrength>() == null) continue;
                source = candidate;
                sourcePrefab = candidatePrefab;
                break;
            }
            if (source == null)
                throw new InvalidOperationException("No upgrade prefab with ItemUpgradePlayerGrabStrength found among " + candidates.Count() + " item assets");
            Log.Info("Cart Compressor template resolved by component: " + source.name + " -> " + sourcePrefab.name);

            storage = new GameObject("CartCompressor Prefab Storage");
            storage.SetActive(false);
            UnityEngine.Object.DontDestroyOnLoad(storage);
            var prefab = UnityEngine.Object.Instantiate(sourcePrefab, storage.transform, false);
            prefab.name = "Item Upgrade Cart Compressor";
            prefab.SetActive(true); // Parent remains inactive; Awake runs only for spawned copies.
            var attributes = prefab.GetComponent<ItemAttributes>();
            var vanilla = prefab.GetComponent<ItemUpgradePlayerGrabStrength>();
            if (vanilla != null) UnityEngine.Object.DestroyImmediate(vanilla);
            var item = UnityEngine.Object.Instantiate(source);
            item.name = "Item Upgrade Cart Compressor";
            item.itemName = "CART COMPRESSOR";
            item.itemNameLocalized = null;
            item.description = "CART CRUNCH: personal cart compression, levels I / II / III. Cargo size: 76.7% / 70.8% / 65%. Untrained handlers cancel compression.";
            item.value = ScriptableObject.CreateInstance<Value>();
            item.value.valueMin = 3750;
            item.value.valueMax = 4500;
            item.maxAmountInShop = 1;
            item.maxAmount = 6;
            item.maxPurchase = false;
            attributes.item = item;
            prefab.AddComponent<CompressorConsume>();
            var upgrade = prefab.GetComponent<ItemUpgrade>();
            upgrade.upgradeEvent = new UnityEvent();
            CerealVisual.Apply(prefab);
            Plugin.Upgrade = Upgrades.GetUpgrade(Plugin.UpgradeId) ?? Upgrades.RegisterUpgrade(Plugin.UpgradeId, item, null, null);
            if (Plugin.Upgrade == null || Items.RegisterItem(attributes) == null)
                throw new InvalidOperationException("REPOLib rejected Cart Compressor registration");
            Item = item;
            BindSavedState();
            Log.Info("Cart Compressor registered: personal cereal upgrade, 3 tiers, price floor $15,000.");
        }

        private static void BindSavedState()
        {
            var stats = StatsManager.instance;
            if (stats == null || Plugin.Upgrade == null) return;
            // Registration may happen after REPOLib's run-start hook (e.g. loading a save).
            string key = "playerUpgrade" + Plugin.UpgradeId;
            if (stats.dictionaryOfDictionaries.TryGetValue(key, out var saved))
                Plugin.Upgrade.PlayerDictionary = saved;
            else stats.dictionaryOfDictionaries[key] = Plugin.Upgrade.PlayerDictionary;
        }
    }

    public sealed class CompressorConsume : MonoBehaviour
    {
        private bool consumed;
        private void Awake()
        {
            // Runtime UnityEvent listeners on a template do not safely remap on Instantiate.
            var upgrade = GetComponent<ItemUpgrade>();
            upgrade.upgradeEvent = new UnityEvent();
            upgrade.upgradeEvent.AddListener(Consume);
        }
        public void Consume()
        {
            if (consumed) return;
            var toggle = GetComponent<ItemToggle>();
            var player = SemiFunc.PlayerAvatarGetFromPhotonID(toggle.playerTogglePhotonID);
            if (player == null || !player.isLocal || Plugin.Upgrade == null) return;
            consumed = true;
            if (Plugin.Upgrade.GetLevel(player) < Domain.UpgradeBalance.MaxLevel) Plugin.Upgrade.AddLevel(player);
        }
    }

    [HarmonyPatch(typeof(StatsManager), "RunStartStats")]
    internal static class RegisterCompressorPatch
    {
        [HarmonyPostfix, HarmonyPriority(Priority.First)]
        private static void Postfix()
        {
            try { CompressorShop.Register(); }
            catch (Exception ex) { Log.Error("Cart Compressor registration failed: " + ex); }
        }
    }

    [HarmonyPatch(typeof(ShopManager), "GetAllItemsFromStatsManager")]
    internal static class RegisterCompressorBeforeShopPatch
    {
        [HarmonyPrefix]
        private static void Prefix()
        {
            try { CompressorShop.Register(); }
            catch (Exception ex) { Log.Error("Cart Compressor shop registration failed: " + ex); }
        }
    }

    [HarmonyPatch(typeof(ShopManager), "UpgradeValueGet")]
    internal static class CompressorPricePatch
    {
        [HarmonyPostfix]
        private static void Postfix(Item item, ref float __result)
        {
            if (item == CompressorShop.Item && item != null) __result = Mathf.Max(15, __result);
        }
    }

    // Stop the local interaction before it broadcasts the toggle RPC; this avoids
    // destroying a fourth box or racing the replicated upgrade event on other peers.
    [HarmonyPatch(typeof(ItemToggle), "ToggleItem")]
    internal static class MaxTierConsumptionPatch
    {
        private static bool Prefix(ItemToggle __instance, bool toggle, int player)
        {
            if (!toggle || __instance.GetComponent<CompressorConsume>() == null) return true;
            var avatar = SemiFunc.PlayerAvatarGetFromPhotonID(player);
            if (avatar == null || Plugin.Upgrade == null) return true;
            if (Plugin.Upgrade.GetLevel(avatar) < Domain.UpgradeBalance.MaxLevel) return true;
            Log.Info("Cart Compressor is already level III; cereal box was not consumed.");
            return false;
        }
    }
}
