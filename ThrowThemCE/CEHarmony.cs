using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using HarmonyLib;
using Verse;
using Verse.AI;
using RimWorld;
using System.Reflection.Emit;
using CombatExtended;

namespace ThrowThem
{
    [StaticConstructorOnStartup]
    public static class StartUp
    {
        static StartUp()
        {
            var harmony = new Harmony("com.example.patch");
            harmony.PatchAll();
        }
    }
    public static class HelperClass
    {
        public static void RemoveOneOrDestroy(ThingWithComps thing)
        {
            if (thing.stackCount > 1)
            {
                thing.stackCount--;
            }
            else
            {
                thing.Destroy(DestroyMode.Vanish);
            }
        }

        public static bool NoPrimaryWeaponOrInventory(Verb_ShootCEOneUse instance)
        {
            return EquipmentCheck(instance.ShooterPawn);
        }
        public static bool EquipmentCheck(Pawn pawn)
        {
            if (pawn == null || pawn.equipment != null || pawn.equipment.Primary != null)
            {
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(CombatExtended.Verb_ShootCEOneUse), "SelfConsume")]
    public static class CombatExtendedPatch
    {
        private static readonly MethodInfo ShooterPawn = AccessTools.Method(typeof(CombatExtended.Verb_LaunchProjectileCE), "ShooterPawn");
        private static readonly MethodInfo NoPrimaryWeaponOrInventory = AccessTools.Method(typeof(HelperClass), nameof(HelperClass.NoPrimaryWeaponOrInventory));
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator il)
        {
            var first = false;
            var second = false;
            var codes = new List<CodeInstruction>(instructions);
            MethodInfo codetofind = AccessTools.Method(typeof(Thing), nameof(ThingWithComps.Destroy));


            for (var i = 0; i < codes.Count; i++)
            {
                if (i < codes.Count - 1 && codes[i + 1].opcode == OpCodes.Callvirt && codes[i + 1].operand as MethodInfo == codetofind)
                {
                    yield return new CodeInstruction(OpCodes.Nop);

                    first = true;
                }
                else if (codes[i].opcode == OpCodes.Callvirt && codes[i].operand as MethodInfo == codetofind)
                {
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(HelperClass), nameof(HelperClass.RemoveOneOrDestroy)));
                }

                else if (i < codes.Count - 5 && codes[i].opcode == OpCodes.Nop && codes[i + 1].opcode == OpCodes.Nop && codes[i + 2].opcode == OpCodes.Ldloc_0)
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_0).WithLabels(codes[i].labels);
                    yield return new CodeInstruction(OpCodes.Call, NoPrimaryWeaponOrInventory);
                    yield return codes[i + 3];
                  //  codes[i + 1] = new CodeInstruction(OpCodes.Nop);
                  //  codes[i + 2] = new CodeInstruction(OpCodes.Call, NoPrimaryWeaponOrInventory).WithLabels(codes[i + 2].labels);
                    second = true;
                }

                else
                {
                    yield return codes[i];
                }
            }
            if (first is false)
            {
                Log.Error("Throw Them first transpiler failed, please send log to bug reports");
            }
            if (second is false)
            {
                Log.Error("Throw Them second transpiler failed, please send log to bug reports");
            }
        }
    }
}

