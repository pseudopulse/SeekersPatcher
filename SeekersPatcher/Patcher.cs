using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MethodAttributes = Mono.Cecil.MethodAttributes;
using FieldAttributes = Mono.Cecil.FieldAttributes;
using RoR2;
using RoR2.Projectile;
using UnityEngine;
using SEC = System.Security;

//Allows you to access private methods/fields/etc from the stubbed Assembly-CSharp that is included.

[module: SEC.UnverifiableCode]
#pragma warning disable CS0618 // Type or member is obsolete
[assembly: SEC.Permissions.SecurityPermission(SEC.Permissions.SecurityAction.RequestMinimum, SkipVerification = true)]
#pragma warning restore CS0618 // Type or member is obsolete

namespace _SeekersPatcher
{
    public static class _SeekersPatcher
    {
        public static IEnumerable<string> TargetDLLs
        {
            get
            {
                yield return "RoR2.dll";
            }
        }

        private const string RoR2 = "RoR2", RoR2UI = "RoR2.UI", RoR2Projectile = "RoR2.Projectile", RoR2Skills = "RoR2.Skills";
        private static ModuleDefinition ror2;

        public static void Patch(AssemblyDefinition assemblyDef)
        {
            if (assemblyDef?.Name?.Name != "RoR2")
                return;

            ror2 = assemblyDef.MainModule;

            PatchMissingFields();
            PatchSuicide();
            PatchProjectileFire();

            ror2 = null;
        }

        private static void PatchMissingFields()
        {
            var tdDLC2Items = ror2.GetType("RoR2.DLC2Content/Items");
            var tdItemDef = ror2.GetType(RoR2, nameof(ItemDef));
            AddField(tdDLC2Items, tdItemDef, "NegateAttack", FieldAttributes.Public | FieldAttributes.Static);
            AddField(tdDLC2Items, tdItemDef, "LowerHealthHigherDamage", FieldAttributes.Public | FieldAttributes.Static);
            AddField(tdDLC2Items, tdItemDef, "GoldOnStageStart", FieldAttributes.Public | FieldAttributes.Static);
            AddField(tdDLC2Items, tdItemDef, "ResetChests", FieldAttributes.Public | FieldAttributes.Static);



            var tdDLC2Buffs = ror2.GetType("RoR2.DLC2Content/Buffs");
            var tdBuffDef = ror2.GetType(RoR2, nameof(BuffDef));
            AddField(tdDLC2Buffs, tdBuffDef, "LowerHealthHigherDamageBuff", FieldAttributes.Public | FieldAttributes.Static);



            var tdBulletAttack = ror2.GetType(RoR2, nameof(BulletAttack));
            var tdEffectData = ror2.GetType(RoR2, nameof(EffectData));
            var refBool = ror2.ImportReference(typeof(bool));
            AddField(tdBulletAttack, tdEffectData, "_EffectData", FieldAttributes.Public | FieldAttributes.Static);
            AddField(tdBulletAttack, refBool, "_UsePools", FieldAttributes.Public | FieldAttributes.Static);



            var tdPCMC = ror2.GetType(RoR2, nameof(PlayerCharacterMasterController));
            AddField(tdPCMC, refBool, "wasClaimed", FieldAttributes.Public);



            var tdInventory = ror2.GetType(RoR2, nameof(Inventory));
            var refIntArray = ror2.ImportReference(typeof(int[]));
            AddField(tdInventory, refIntArray, "itemStacks", FieldAttributes.Public);



            var tdPickupOption = ror2.GetType("RoR2.PickupPickerController/Option");
            var tdPickupIndex = ror2.GetType(RoR2, nameof(PickupIndex));
            AddField(tdPickupOption, tdPickupIndex, "pickupIndex", FieldAttributes.Public);


            var tdShopTerminal = ror2.GetType(RoR2, nameof(ShopTerminalBehavior));
            AddField(tdShopTerminal, tdPickupIndex, "pickupIndex", FieldAttributes.Public);

            var tdGenericPickupOption = ror2.GetType(RoR2, nameof(GenericPickupController));
            AddField(tdGenericPickupOption, tdPickupIndex, "pickupIndex", FieldAttributes.Public);


            var tdCharacterModel = ror2.GetType(RoR2, nameof(CharacterModel));
            var refInt = ror2.ImportReference(typeof(int));
            AddField(tdCharacterModel, refInt, "invisibilityCount", FieldAttributes.Public);
            


            void AddField(TypeDefinition typeDef, TypeReference fieldTypeRef, string fieldName, FieldAttributes attr)
            {
                if (typeDef.Fields.Any(f => f.Name == fieldName && f.FieldType.Name == fieldTypeRef.Name))
                {
                    Logger.Warn($"{fieldName} already exists in {typeDef.FullName}");
                    return;
                }

                typeDef.Fields.Add(new FieldDefinition(fieldName, attr, fieldTypeRef));
                Logger.Debug("Added " + typeDef.Fields.Last().FullName);
            }
        }

        private static void PatchSuicide()
        {
            var name = nameof(HealthComponent.Suicide);

            var tdDamageType = ror2.GetType(RoR2, nameof(DamageType));
            var tdDamageTypeCombo = ror2.GetType(RoR2, nameof(DamageTypeCombo));
            var tdHealthComponent = ror2.GetType(RoR2, nameof(HealthComponent));
            var refGameObject = ror2.ImportReference(typeof(GameObject));
            var refVoid = ror2.ImportReference(typeof(void));


            if (tdHealthComponent.Methods.Any(m => m.Name == name && m.HasParameters && m.Parameters.Count == 3 && m.Parameters.Last().ParameterType.Name == tdDamageType.Name))
            {
                Logger.Warn(name + " exists, abort patch");
                return;
            }

            var newMethodDef = tdHealthComponent.Methods.First(m => m.Name == name);
            var comboImplicitMethodDef = tdDamageTypeCombo.Methods.First(m => m.Name is "op_Implicit" && m.HasParameters && m.Parameters.Count == 1 && m.Parameters[0].ParameterType.Name == tdDamageType.Name);

            var methodDef = new MethodDefinition(name, MethodAttributes.Public, refVoid);
            methodDef.Parameters.Add(new ParameterDefinition(refGameObject));
            methodDef.Parameters.Add(new ParameterDefinition(refGameObject));
            methodDef.Parameters.Add(new ParameterDefinition(tdDamageType));

            /*	
                IL_0000: ldarg.0
	            IL_0001: ldarg.1
	            IL_0002: ldarg.2
	            IL_0003: ldarg.3
	            IL_0005: call valuetype [RoR2]RoR2.DamageTypeCombo [RoR2]RoR2.DamageTypeCombo::op_Implicit(valuetype [RoR2]RoR2.DamageType)
	            IL_000a: callvirt instance void [RoR2]RoR2.HealthComponent::Suicide(class [UnityEngine.CoreModule]UnityEngine.GameObject, class [UnityEngine.CoreModule]UnityEngine.GameObject, valuetype [RoR2]RoR2.DamageTypeCombo)
	            IL_000f: ret
            */

            var il = methodDef.Body.GetILProcessor();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Ldarg_2);
            il.Emit(OpCodes.Ldarg_3);
            il.Emit(OpCodes.Call, comboImplicitMethodDef);
            il.Emit(OpCodes.Callvirt, newMethodDef);
            il.Emit(OpCodes.Ret);

            tdHealthComponent.Methods.Add(methodDef);
            Logger.Debug(methodDef.FullName);
        }

        private static void PatchProjectileFire()
        {
            var name = nameof(ProjectileManager.FireProjectile);

            var tdProjectileManager = ror2.GetType(RoR2Projectile, nameof(ProjectileManager));
            var tdDamageColorIndex = ror2.GetType(RoR2, nameof(DamageColorIndex));
            var refVector3 = ror2.ImportReference(typeof(Vector3));
            var refQuaternion = ror2.ImportReference(typeof(Quaternion));
            var refGameObject = ror2.ImportReference(typeof(GameObject));
            var refVoid = ror2.ImportReference(typeof(void));
            var refFloat = ror2.ImportReference(typeof(float));
            var refBool = ror2.ImportReference(typeof(bool));

            if (tdProjectileManager.Methods.Any(m => m.Name == name && m.HasParameters && m.Parameters.Count == 10))
            {
                Logger.Warn(name + " exists, abort patch");
                return;
            }

            var newMethodDef = tdProjectileManager.Methods.First(m => m.Name is nameof(ProjectileManager.FireProjectileWithoutDamageType));

            var methodDef = new MethodDefinition(name, MethodAttributes.Public, refVoid);
            methodDef.Parameters.Add(new ParameterDefinition(refGameObject));
            methodDef.Parameters.Add(new ParameterDefinition(refVector3));
            methodDef.Parameters.Add(new ParameterDefinition(refQuaternion));
            methodDef.Parameters.Add(new ParameterDefinition(refGameObject));
            methodDef.Parameters.Add(new ParameterDefinition(refFloat));
            methodDef.Parameters.Add(new ParameterDefinition(refFloat));
            methodDef.Parameters.Add(new ParameterDefinition(refBool));
            methodDef.Parameters.Add(new ParameterDefinition(tdDamageColorIndex));
            methodDef.Parameters.Add(new ParameterDefinition(refGameObject));
            methodDef.Parameters.Add(new ParameterDefinition(refFloat));

            /*	
                IL_0000: ldarg.0
	            IL_0005: ldarg.1
	            IL_0006: ldarg.2
	            IL_0007: ldarg.3
	            IL_0008: ldarg.s p4
	            IL_000a: ldarg.s p5
	            IL_000c: ldarg.s p6
	            IL_000e: ldarg.s p7
	            IL_0010: ldarg.s p8
	            IL_0012: ldarg.s p9
	            IL_0014: ldarg.s p10
	            IL_0016: callvirt instance void [RoR2]RoR2.Projectile.ProjectileManager::FireProjectileWithoutDamageType(
                                                class [UnityEngine.CoreModule]UnityEngine.GameObject, valuetype [UnityEngine.CoreModule]UnityEngine.Vector3, valuetype [UnityEngine.CoreModule]UnityEngine.Quaternion,
                                                class [UnityEngine.CoreModule]UnityEngine.GameObject, float32, float32, bool, valuetype [RoR2]RoR2.DamageColorIndex, class [UnityEngine.CoreModule]UnityEngine.GameObject, float32)
	            IL_001b: ret
            */
            var il = methodDef.Body.GetILProcessor();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Ldarg_2);
            il.Emit(OpCodes.Ldarg_3);
            il.Emit(OpCodes.Ldarg, 4);
            il.Emit(OpCodes.Ldarg, 5);
            il.Emit(OpCodes.Ldarg, 6);
            il.Emit(OpCodes.Ldarg, 7);
            il.Emit(OpCodes.Ldarg, 8);
            il.Emit(OpCodes.Ldarg, 9);
            il.Emit(OpCodes.Ldarg, 10);
            il.Emit(OpCodes.Callvirt, newMethodDef);
            il.Emit(OpCodes.Ret);

            tdProjectileManager.Methods.Add(methodDef);
            Logger.Debug(methodDef.FullName);
        }
        internal static class Logger
        {
            private static readonly ManualLogSource logSource = BepInEx.Logging.Logger.CreateLogSource("SeekersPatcher");

            public static void Info(object data) => logSource.LogInfo(data);

            public static void Error(object data) => logSource.LogError(data);

            public static void Warn(object data) => logSource.LogWarning(data);

            public static void Fatal(object data) => logSource.LogFatal(data);

            public static void Message(object data) => logSource.LogMessage(data);

            public static void Debug(object data) => logSource.LogDebug(data);
        }
    }
}