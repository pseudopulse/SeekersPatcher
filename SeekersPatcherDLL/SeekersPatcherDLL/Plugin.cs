using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using BepInEx;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using RoR2;
using RoR2.ContentManagement;
using RoR2.Projectile;
using UnityEngine;
using System.Reflection;
using Unity.Baselib.LowLevel;
using System.Reflection.Emit;
using OpCodes = Mono.Cecil.Cil.OpCodes;
using MonoMod.RuntimeDetour.HookGen;
using RoR2.Skills;

namespace SeekersPatcherDLL {
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    public class _0Main : BaseUnityPlugin {
        public const string PluginGUID = PluginAuthor + "." + PluginName;
        public const string PluginAuthor = "___0pseudopulse";
        public const string PluginName = "__SeekersPatcherDLL";
        public const string PluginVersion = "1.0.0";

        public static BepInEx.Logging.ManualLogSource ModLogger;
        public delegate void orig_ctor(Hook hook, MethodBase from, MethodBase to, object delegTarg, ref HookConfig conf);
        public delegate void orig_Add(MethodBase method, Delegate hookDelegate);
        public delegate void orig_Suicide(HealthComponent instance, GameObject p1, GameObject p2, DamageType dt);
        public delegate void orig_AssignSkill(GenericSkill skill, SkillDef p1);
        public static FieldInfo invisCount;
        public delegate void orig_FireProjectile(ProjectileManager instance, GameObject p1, Vector3 p2, Quaternion p3, GameObject p4, float p5, float p6, bool p7, DamageColorIndex p8, GameObject p9, float p10);
        public static MethodInfo Update;
        public static FieldInfo damageFromRecalculateStats;
        public void Awake() {
            // set logger
            ModLogger = Logger;

            invisCount = typeof(RoR2.CharacterModel).GetField("invisibilityCount", (BindingFlags)(-1));

            damageFromRecalculateStats = typeof(RoR2.CharacterBody).GetField("damageFromRecalculateStats", (BindingFlags)(-1));

            // fakeULHT = typeof(HealthComponent).GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance).Where(x => x.Name == "UpdateLastHitTimeRedirect").First();
            
            On.RoR2.DLC2Content.LoadStaticContentAsync += OnLoadContent;
            Logger.LogError(typeof(ProjectileManager).GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance).Where(x => x.Name == "FireProjectile").OrderBy(x => x.GetParameters().Length).ElementAt(1));
            Hook hook = new(
                typeof(ProjectileManager).GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance).Where(x => x.Name == "FireProjectile").OrderBy(x => x.GetParameters().Length).ElementAt(1),
                typeof(_0Main).GetMethod(nameof(FireProjectile), System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            );

            Hook hook2 = new(
                typeof(HealthComponent).GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance).Where(x => x.Name == "Suicide").FirstOrDefault(x => x.GetParameters()[x.GetParameters().Length - 1].ParameterType == typeof(RoR2.DamageType)),
                typeof(_0Main).GetMethod(nameof(Suicide), System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            );

            Hook hook3 = new(
                typeof(GenericSkill).GetMethods((BindingFlags)(-1)).Where(x => x.Name == "AssignSkill").OrderBy(x => x.GetParameters().Length).ElementAt(0),
                typeof(_0Main).GetMethod(nameof(AssignSkill), System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            );

            ILHook hook4 = new ILHook(
                typeof(CharacterModel).GetMethod("get_invisibilityCount", (BindingFlags)(-1)),
                Invisibility
            );

            ILHook hook5 = new ILHook(
                typeof(CharacterModel).GetMethod("set_invisibilityCount", (BindingFlags)(-1)),
                InvisibilitySet
            );

            IL.RoR2.CharacterBody.RecalculateStats += DamageFromRecalc;

            // Update = typeof(Stage).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic);
            
            // Hook hookHook = new(
            //     typeof(Hook).GetConstructors().First(x => x.GetParameters().Length == 4),
            //     typeof(_0Main).GetMethod(nameof(Apply), System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            // );

            /*Hook hook2 = new(
                typeof(HookEndpointManager).GetMethods((BindingFlags.Static | BindingFlags.Public)).Where(x => x.Name == "Add").ElementAt(1),
                typeof(_0Main).GetMethod(nameof(OnAdd), (BindingFlags)(-1))
            );

            IL.RoR2.HealthComponent.UpdateLastHitTime += OnUpdate;*/
        }

        private void DamageFromRecalc(ILContext il)
        {
            ILCursor c = new(il);
            c.TryGotoNext(MoveType.After, x => x.MatchStfld("RoR2.CharacterBody", "statsDirty"));
            c.Emit(OpCodes.Ldarg_0);
            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<CharacterBody, float>>((cb) => {
                return cb.damage;
            });
            c.Emit(OpCodes.Stfld, damageFromRecalculateStats);
        }

        public static void Invisibility(ILContext il) {
            ILCursor c =  new(il);
            c.TryGotoNext(MoveType.Before, x => x.MatchLdfld(out _));
            c.Next.Operand = invisCount;
        }
        public static void InvisibilitySet(ILContext il) {
            ILCursor c =  new(il);
            c.TryGotoNext(MoveType.Before, x => x.MatchLdfld(out _));
            c.Next.Operand = invisCount;
            c.TryGotoNext(MoveType.Before, x => x.MatchStfld(out _));
            c.Next.Operand = invisCount;
        }

        public static void UselessMethod(string y) {
            _ = y;
            return;
        }

        public static void OnAdd(orig_Add orig, MethodBase method, Delegate hook) {
            if (method.Name == "FixedUpdate" && method.DeclaringType.Name == "Stage") {
                orig(Update, hook);
                return;
            }

            orig(method, hook);
        }

        public static void AssignSkill(orig_AssignSkill orig, GenericSkill self, SkillDef p1) {
            self.AssignSkill(p1, false);
        }

        public static void Suicide(orig_Suicide orig, HealthComponent self, GameObject p1, GameObject p2, DamageType p3) {
            self.Suicide(p1, p2, p3);
        }
        public static void FireProjectile(orig_FireProjectile orig, ProjectileManager instance, GameObject p1, Vector3 p2, Quaternion p3, GameObject p4, float p5, float p6, bool p7, DamageColorIndex p8, GameObject p9, float p10) {
            ProjectileManager.instance.FireProjectile(p1, p2, p3, p4, p5, p6, p7, p8, p9, p10);
        }

        private IEnumerator OnLoadContent(On.RoR2.DLC2Content.orig_LoadStaticContentAsync orig, RoR2.DLC2Content self, LoadStaticContentAsyncArgs args)
        {
            yield return orig(self, args);
            typeof(RoR2.DLC2Content.Items).GetField("NegateAttack").SetValue(null, DLC2Content.Items.SpeedBoostPickup);
            typeof(RoR2.DLC2Content.Items).GetField("GoldOnStageStart").SetValue(null, DLC2Content.Items.BarrageOnBoss);
            typeof(RoR2.DLC2Content.Items).GetField("ResetChests").SetValue(null, DLC2Content.Items.ItemDropChanceOnKill);
            typeof(RoR2.DLC2Content.Items).GetField("LowerHealthHigherDamage").SetValue(null, DLC2Content.Items.AttackSpeedPerNearbyAllyOrEnemy);
            typeof(RoR2.DLC2Content.Buffs).GetField("LowerHealthHigherDamageBuff").SetValue(null, DLC2Content.Buffs.AttackSpeedPerNearbyAllyOrEnemyBuff);
        }
    }
}