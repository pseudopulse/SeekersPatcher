using System;
using System.Linq;
using System.Reflection;
using System.Security;
using System.Security.Permissions;
using BepInEx;
using HarmonyLib;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using RoR2;
using RoR2BepInExPack.GameAssetPathsBetter;
using UnityEngine.AddressableAssets;

//Allows you to access private methods/fields/etc from the stubbed Assembly-CSharp that is included.

[module: UnverifiableCode]
#pragma warning disable CS0618 // Type or member is obsolete
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
#pragma warning restore CS0618 // Type or member is obsolete

namespace SeekersPatcherDLL
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    public class _0Main : BaseUnityPlugin 
    {
        public const string PluginGUID = PluginAuthor + "." + PluginName;
        public const string PluginAuthor = "___0pseudopulse";
        public const string PluginName = "__SeekersPatcherDLL";
        public const string PluginVersion = "1.0.0";

        public static BepInEx.Logging.ManualLogSource ModLogger;

        private static FieldInfo oldStacksField;

        private void Awake() 
        {
            // set logger
            ModLogger = Logger;

            oldStacksField = typeof(Inventory).GetFields().FirstOrDefault(f => f.Name is "itemStacks" && f.FieldType == typeof(int[]));

            Hook wasClaimedHook = new Hook(
                AccessTools.Method(typeof(PlayerCharacterMasterController), nameof(PlayerCharacterMasterController.Update)),
                JumpClaimed
            );

            ILHook getInvisHook = new ILHook(
                AccessTools.PropertyGetter(typeof(CharacterModel), nameof(CharacterModel.invisibilityCount)),
                InvisibilityGet
            );

            ILHook setInvisHook = new ILHook(
                AccessTools.PropertySetter(typeof(CharacterModel), nameof(CharacterModel.invisibilityCount)),
                InvisibilitySet
            );

            Hook inventoryAcquire = new Hook(
                AccessTools.Method(typeof(Inventory), nameof(Inventory.AcquirePooledResources)),
                AcquirePools
            );

            Hook inventoryRelease = new Hook(
                AccessTools.Method(typeof(Inventory), nameof(Inventory.ReleasePooledResources)),
                ReleasePools
            );

            Inventory.onInventoryChangedGlobal += Inventory_onInventoryChangedGlobal;

            typeof(DLC2Content.Items).GetField("NegateAttack").SetValue(null, Addressables.LoadAssetAsync<ItemDef>(RoR2_DLC2_Items_SpeedBoostPickup.SpeedBoostPickup_asset).WaitForCompletion());
            typeof(DLC2Content.Items).GetField("GoldOnStageStart").SetValue(null, Addressables.LoadAssetAsync<ItemDef>(RoR2_DLC2_Items_BarrageOnBoss.BarrageOnBoss_asset).WaitForCompletion());
            typeof(DLC2Content.Items).GetField("ResetChests").SetValue(null, Addressables.LoadAssetAsync<ItemDef>(RoR2_DLC2_Items_ItemDropChanceOnKill.ItemDropChanceOnKill_asset).WaitForCompletion());
            typeof(DLC2Content.Items).GetField("LowerHealthHigherDamage").SetValue(null, Addressables.LoadAssetAsync<ItemDef>(RoR2_DLC2_Items_AttackSpeedPerNearbyAllyOrEnemy.AttackSpeedPerNearbyAllyOrEnemy_asset).WaitForCompletion());
            typeof(DLC2Content.Buffs).GetField("LowerHealthHigherDamageBuff").SetValue(null, Addressables.LoadAssetAsync<BuffDef>(RoR2_DLC2.bdAttackSpeedPerNearbyAllyOrEnemyBuff_asset).WaitForCompletion());
        }

        private static void Inventory_onInventoryChangedGlobal(Inventory inventory)
        {
            if (oldStacksField?.GetValue(inventory) is int[] stacks)
                inventory.WriteAllPermanentItemStacks(stacks);
        }

        private static void AcquirePools(Action<Inventory> orig, Inventory self)
        {
            orig(self);

            oldStacksField?.SetValue(self, ItemCatalog.PerItemBufferPool.Request<int>());
        }

        private static void ReleasePools(Action<Inventory> orig, Inventory self)
        {
            orig(self);

            if (oldStacksField?.GetValue(self) is int[] stacks)
                ItemCatalog.PerItemBufferPool.Return(ref stacks);
        }

        private static void JumpClaimed(Action<PlayerCharacterMasterController> orig, PlayerCharacterMasterController self)
        {
            orig(self);

            AccessTools.Field(typeof(PlayerCharacterMasterController), "wasClaimed").SetValue(self, self.jumpWasClaimed);
        }

        private static void InvisibilityGet(ILContext il)
        {
            ILCursor c = new(il);
            if (!c.TryGotoNext(MoveType.Before, x => x.MatchLdfld(out _)))
            {
                ModLogger.LogError("InvisibilityGet failed");
                return;
            }

            c.Next.Operand = AccessTools.Field(typeof(CharacterModel), nameof(CharacterModel.invisibilityCount));
        }

        private static void InvisibilitySet(ILContext il)
        {
            ILCursor[] c = null;
            if (!new ILCursor(il).TryFindNext(out c,
                    x => x.MatchLdfld(out _),
                    x => x.MatchStfld(out _)
                ))
            {
                ModLogger.LogError("InvisibilitySet failed");
                return;
            }

            c[0].Next.Operand = AccessTools.Field(typeof(CharacterModel), nameof(CharacterModel.invisibilityCount));
            c[1].Next.Operand = AccessTools.Field(typeof(CharacterModel), nameof(CharacterModel.invisibilityCount));
        }
    }
}