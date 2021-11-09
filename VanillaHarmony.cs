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

    class ThrowThem
    {
        private static readonly List<Type> ModTypes = new List<Type>() { AccessTools.TypeByName("CombatExtended.Verb_ShootCEOneUse"), AccessTools.TypeByName("Verb_LaunchProjectile") };


        [HarmonyPatch(typeof(Pawn_InventoryTracker), "GetGizmos")]
        class GetGizmosPatch
        {

            public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, ThingOwner<Thing> ___innerContainer, Pawn ___pawn)
            {
                foreach (Gizmo item in __result)
                {
                    yield return item;
                }
                foreach (Thing item in ___innerContainer)
                {
                    CompEquippable compEquippable = item.TryGetComp<CompEquippable>();
                    if (compEquippable != null && ModTypes.Contains(compEquippable.PrimaryVerb.verbProps.verbClass))
                    {
                        compEquippable.PrimaryVerb.caster = ___pawn;
                        Command_VerbTarget command_VerbTarget = new Command_VerbTarget();
                        ThingStyleDef styleDef = item.StyleDef;
                        command_VerbTarget.defaultDesc = item.LabelCap + ": " + item.def.description.CapitalizeFirst();
                        command_VerbTarget.icon = ((styleDef != null && styleDef.UIIcon != null) ? styleDef.UIIcon : item.def.uiIcon);
                        command_VerbTarget.iconAngle = item.def.uiIconAngle;
                        command_VerbTarget.iconOffset = item.def.uiIconOffset;
                        command_VerbTarget.tutorTag = "VerbTarget";
                        command_VerbTarget.verb = compEquippable.PrimaryVerb;
                        command_VerbTarget.hotKey = KeyBindingDefOf.Misc4;

                        if (___pawn.Faction != Faction.OfPlayer)
                        {
                            command_VerbTarget.Disable("CannotOrderNonControlled".Translate());
                        }
                        else
                        {
                            if (___pawn.WorkTagIsDisabled(WorkTags.Violent))
                            {
                                command_VerbTarget.Disable("IsIncapableOfViolence".Translate(___pawn.LabelShort, ___pawn));
                            }
                            else if (!___pawn.drafter.Drafted)
                            {
                                command_VerbTarget.Disable("IsNotDrafted".Translate(___pawn.LabelShort, ___pawn));
                            }
                            else if (compEquippable.PrimaryVerb is Verb_LaunchProjectile)
                            {
                                Apparel apparel = compEquippable.PrimaryVerb.FirstApparelPreventingShooting();
                                if (apparel != null)
                                {
                                    command_VerbTarget.Disable("ApparelPreventsShooting".Translate(___pawn.Named("PAWN"), apparel.Named("APPAREL")).CapitalizeFirst());
                                }
                            }
                            else if (EquipmentUtility.RolePreventsFromUsing(___pawn, compEquippable.PrimaryVerb.EquipmentSource, out string reason))
                            {
                                command_VerbTarget.Disable(reason);
                            }
                            yield return command_VerbTarget;
                        }
                    }
                }
                yield break;
            }
        }


        [HarmonyPatch(typeof(Pawn), "TryGetAttackVerb")]
        class TryGetAttackVerbPatch
        {
            public static Verb Postfix(Verb __result, Pawn __instance)
            {
                if (__instance?.CurJob?.GetCachedDriverDirect is JobDriver_AttackStatic)
                {
                    if (__instance?.inventory?.innerContainer == null)
                    {
                        return __result;
                    }
                    if (__instance?.CurJob.verbToUse != __result)
                        foreach (Thing item in __instance.inventory.innerContainer)
                        {
                            CompEquippable compEquippable = item.TryGetComp<CompEquippable>();
                            if (compEquippable != null && ModTypes.Contains(compEquippable.PrimaryVerb?.verbProps?.verbClass))
                            {
                                if (__instance.CurJob.verbToUse == compEquippable.PrimaryVerb)
                                {
                                    return compEquippable.PrimaryVerb;
                                }
                            }
                        }
                }
                return __result;
            }
        }

        [HarmonyPatch(typeof(JobDriver_AttackStatic), "MakeNewToils")]
        class AttackStaticPatch
        {
            public static void Prefix(JobDriver_AttackStatic __instance)
            {
                Pawn pawn = __instance.pawn;
                if (pawn.CurJob?.verbToUse?.EquipmentSource != null)
                {
                    __instance.FailOnMissingItems(pawn, pawn.CurJob.verbToUse.EquipmentSource);
                }
            }
        }
    }

    public static class HelperClass
    {
        public static T FailOnMissingItems<T>(this T f, Pawn x, Thing t) where T : IJobEndable
        {
            {
                f.AddEndCondition(delegate
                {
                    return (x.equipment.Contains(t) || x.inventory.Contains(t)) ? JobCondition.Ongoing : JobCondition.Incompletable;
                });
                return f;
            }
        }
    }
}