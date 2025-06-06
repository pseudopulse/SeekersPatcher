using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using BepInEx.Logging;
using Mono.Cecil;
using Mono.Cecil.Rocks;
using MonoMod.RuntimeDetour;
using MonoMod.RuntimeDetour.HookGen;
using UnityEngine;
using System.Reflection;
using System;
using System.Threading.Tasks;
using BepInEx.Bootstrap;
using UnityEngine.Networking.Match;
using System.Diagnostics;
using MonoMod.Cil;
using Mono.Cecil;
using Mono.Cecil.Cil;
using UnityEngine.AddressableAssets;
using UnityEngine.SceneManagement;
using MethodAttributes = Mono.Cecil.MethodAttributes;
using MonoMod.Utils;
using MonoMod.RuntimeDetour.HookGen;
using MonoMod;
using MethodBody = Mono.Cecil.Cil.MethodBody;
using static Mono.Cecil.FieldAttributes;

namespace _SeekersPatcher {
    
    public static class _SeekersPatcher {
        public static MethodReference fixedDeltaTime;
        public static IEnumerable<string> TargetDLLs => new[] { "RoR2.dll" };
        public static void Patch(AssemblyDefinition def) {
            if (def.Name.Name == "RoR2") {
                PatchRoR2(def);
            }
        }

        private static void PatchRoR2(AssemblyDefinition def) {
            var a = AssemblyDefinition.ReadAssembly(typeof(void).Assembly.Location);
            TypeReference voidRef = a.MainModule.ImportReference(typeof(void));
            AssemblyDefinition unity = AssemblyDefinition.ReadAssembly(Path.Combine(BepInEx.Paths.ManagedPath, "UnityEngine.CoreModule.dll"));

            // types
            var itemDef = def.MainModule.GetType("RoR2.ItemDef");
            var buffDef = def.MainModule.GetType("RoR2.BuffDef");
            var f = a.MainModule.ImportReference(typeof(float));
            var i = a.MainModule.ImportReference(typeof(int));
            var b = a.MainModule.ImportReference(typeof(bool));
            var colorIndex = def.MainModule.ImportReference(def.MainModule.GetType("RoR2.DamageColorIndex"));
            var vec3 = def.MainModule.ImportReference(unity.MainModule.GetType("UnityEngine.Vector3"));
            var quat = def.MainModule.ImportReference(unity.MainModule.GetType("UnityEngine.Quaternion"));
            var obj = def.MainModule.ImportReference(unity.MainModule.GetType("UnityEngine.GameObject"));
            var damageType = def.MainModule.GetType("RoR2.DamageType");
            var sd = def.MainModule.GetType("RoR2.Skills.SkillDef");

            // fix NegateAttack being renamed

            var items = def.MainModule.GetType("RoR2.DLC2Content/Items");
            items.Fields.Add(new FieldDefinition("NegateAttack", Public | Static, def.MainModule.ImportReference(itemDef)));
            items.Fields.Add(new FieldDefinition("LowerHealthHigherDamage", Public | Static, def.MainModule.ImportReference(itemDef)));
            items.Fields.Add(new FieldDefinition("GoldOnStageStart", Public | Static, def.MainModule.ImportReference(itemDef)));
            items.Fields.Add(new FieldDefinition("ResetChests", Public | Static, def.MainModule.ImportReference(itemDef)));

            var buffs = def.MainModule.GetType("RoR2.DLC2Content/Buffs");
            buffs.Fields.Add(new FieldDefinition("LowerHealthHigherDamageBuff", Public | Static, def.MainModule.ImportReference(buffDef)));

            // fix FireProjectile old params not existing

            var pm = def.MainModule.GetType("RoR2.Projectile.ProjectileManager");
            
            MethodDefinition fireProj = new("FireProjectile", MethodAttributes.Public, voidRef);
            fireProj.Parameters.Add(new ParameterDefinition(obj));
            fireProj.Parameters.Add(new ParameterDefinition(vec3));
            fireProj.Parameters.Add(new ParameterDefinition(quat));
            fireProj.Parameters.Add(new ParameterDefinition(obj));
            fireProj.Parameters.Add(new ParameterDefinition(f));
            fireProj.Parameters.Add(new ParameterDefinition(f));
            fireProj.Parameters.Add(new ParameterDefinition(b));
            fireProj.Parameters.Add(new ParameterDefinition(colorIndex));
            fireProj.Parameters.Add(new ParameterDefinition(obj));
            fireProj.Parameters.Add(new ParameterDefinition(f));
            fireProj.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));

            pm.Methods.Add(fireProj);

            // fix removal of PCMC.wasClaimed

            var pcmc = def.MainModule.GetType("RoR2.PlayerCharacterMasterController");
            pcmc.Fields.Add(new FieldDefinition("wasClaimed", Public, b));

            var pcmcup = pcmc.GetMethods().First(x => x.Name == "Update");
            int index = pcmcup.Body.Instructions.Count - 1;
            
            // load bodyinputs (ldarg, ldsfld) onto stack
            // write it into wasClaimed

            pcmcup.Body.Instructions.Insert(index, Instruction.Create(OpCodes.Ldarg_0));
            pcmcup.Body.Instructions.Insert(index + 1, Instruction.Create(OpCodes.Ldarg_0));
            pcmcup.Body.Instructions.Insert(index + 2, Instruction.Create(OpCodes.Ldfld, pcmc.Fields.First(x => x.Name == "jumpWasClaimed")));
            pcmcup.Body.Instructions.Insert(index + 3, Instruction.Create(OpCodes.Stfld, pcmc.Fields.First(x => x.Name == "wasClaimed")));
            
            var hc = def.MainModule.GetType("RoR2.HealthComponent");
            
            MethodDefinition suicide = new("Suicide", MethodAttributes.Public, voidRef);

            suicide.Parameters.Add(new ParameterDefinition(obj));
            suicide.Parameters.Add(new ParameterDefinition(obj));
            suicide.Parameters.Add(new ParameterDefinition(damageType));

            suicide.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));

            hc.Methods.Add(suicide);

            // fix invisibilityCount

            var cm = def.MainModule.GetType("RoR2.CharacterModel");
            cm.Fields.Add(new FieldDefinition("invisibilityCount", Public, i));

            // fix AssignSkill

            var gs = def.MainModule.GetType("RoR2.GenericSkill");
            MethodDefinition assign = new("AssignSkill", MethodAttributes.Public, voidRef);
            assign.Parameters.Add(new ParameterDefinition(sd));

            assign.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));;

            gs.Methods.Add(assign);

            var cb = def.MainModule.GetType("RoR2.CharacterBody");
            cb.Fields.Add(new FieldDefinition("damageFromRecalculateStats", Public, f));
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