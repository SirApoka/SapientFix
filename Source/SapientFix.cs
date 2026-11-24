using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld;
using Verse;

namespace SapientFix
{
    // Big and Small builds its HL_* defs before static constructors run, so the patch must exist by Mod construction.
    public class SapientFixMod : Mod
    {
        public SapientFixMod(ModContentPack content) : base(content)
        {
            WhitelistExtender.Install();
        }
    }

    public class SapientCompExclusionDef : Def
    {
        public List<string> excludedComps = new List<string>();
    }

    [StaticConstructorOnStartup]
    public static class Startup
    {
        internal const string Tag = "[Sapient Fix] ";
        internal const string HarmonyId = "SirApoka.SapientFix";

        internal static readonly Type SocioButterflyCompType =
            AccessTools.TypeByName("RecreationalSexWithEuterpe.Comp_SocioButterflyTracker");
        internal static readonly Type SocioButterflyPropsType =
            AccessTools.TypeByName("RecreationalSexWithEuterpe.CompProperties_SocioButterflyTracker");

        static Startup()
        {
            try
            {
                new Harmony(HarmonyId).PatchAll(Assembly.GetExecutingAssembly());
            }
            catch (Exception e)
            {
                Log.Error(Tag + "Patching failed. " + e);
            }

            try
            {
                WhitelistExtender.Report();
            }
            catch (Exception e)
            {
                Log.Error(Tag + "Startup report failed. " + e);
            }
        }
    }

    internal static class WhitelistExtender
    {
        private static readonly HashSet<Type> Added = new HashSet<Type>();
        private static MethodInfo allSettingsGetter;
        private static FieldInfo whitelistField;
        private static bool installed;

        internal static void Install()
        {
            try
            {
                Type generator = AccessTools.TypeByName("BigAndSmall.HumanlikeAnimalGenerator");
                Type settings = AccessTools.TypeByName("BigAndSmall.HumanlikeAnimalSettings");
                MethodInfo target = generator == null ? null
                    : AccessTools.Method(generator, "GenerateHumanlikeAnimals", new[] { typeof(bool) });
                allSettingsGetter = settings == null ? null : AccessTools.PropertyGetter(settings, "AllHASettings");
                whitelistField = settings == null ? null : AccessTools.Field(settings, "compWhitelist");

                if (target == null || allSettingsGetter == null || whitelistField == null)
                {
                    Log.Warning(Startup.Tag + "Big and Small's comp whitelist not found. Comp compatibility inactive.");
                    return;
                }

                new Harmony(Startup.HarmonyId).Patch(target,
                    prefix: new HarmonyMethod(typeof(WhitelistExtender), nameof(Prefix)));
                installed = true;
            }
            catch (Exception e)
            {
                Log.Error(Startup.Tag + "Could not patch Big and Small. Comp compatibility inactive. " + e);
            }
        }

        private static void Prefix()
        {
            try
            {
                Extend();
            }
            catch (Exception e)
            {
                Log.Error(Startup.Tag + "Could not extend Big and Small's whitelist. " + e);
            }
        }

        private static void Extend()
        {
            ThingDef human = DefDatabase<ThingDef>.GetNamedSilentFail("Human");
            if (human?.comps == null) return;

            List<List<string>> lists = ((IEnumerable)allSettingsGetter.Invoke(null, null))
                .Cast<object>()
                .Select(s => (List<string>)whitelistField.GetValue(s))
                .Where(l => l != null)
                .ToList();
            if (lists.Count == 0)
            {
                Log.Warning(Startup.Tag + "Big and Small has no whitelist settings. Comp compatibility inactive.");
                return;
            }

            var known = new HashSet<string>(lists.SelectMany(l => l), StringComparer.OrdinalIgnoreCase);
            HashSet<string> excluded = Exclusions();

            foreach (CompProperties comp in human.comps)
            {
                if (comp?.compClass == null) continue;
                string props = comp.GetType().ToString();
                string compClass = comp.compClass.ToString();
                if (excluded.Contains(props) || excluded.Contains(compClass)) continue;
                if (known.Contains(props) || known.Contains(compClass)) continue;

                // The exact compClass: whitelisting a shared props type would also release unrelated animal comps.
                lists[0].Add(compClass);
                known.Add(compClass);
                Added.Add(comp.compClass);
            }
        }

        private static HashSet<string> Exclusions() =>
            new HashSet<string>(
                DefDatabase<SapientCompExclusionDef>.AllDefs.SelectMany(d => d.excludedComps),
                StringComparer.OrdinalIgnoreCase);

        private static bool HasComp(ThingDef def, Type compClass) =>
            def.comps != null && def.comps.Any(c => c?.compClass == compClass);

        internal static void Report()
        {
            if (!installed) return;

            List<ThingDef> sapient = DefDatabase<ThingDef>.AllDefsListForReading
                .Where(d => d.defName.StartsWith("HL_") && d.race != null && d.race.Humanlike &&
                            d.thingClass != null && typeof(Pawn).IsAssignableFrom(d.thingClass))
                .ToList();
            List<ThingDef> animals = DefDatabase<ThingDef>.AllDefsListForReading
                .Where(d => d.race != null && d.race.Animal)
                .ToList();

            Log.Message(Startup.Tag + "Active. " + Added.Count + " comp class(es) added to Big and Small's whitelist; " +
                        sapient.Count + " sapient def(s) checked.");
            foreach (Type type in Added.OrderBy(t => t.FullName))
            {
                Log.Message(Startup.Tag + "  - " + type.FullName + ": " + sapient.Count(d => HasComp(d, type)) + "/" +
                            sapient.Count + " sapient def(s), " + animals.Count(d => HasComp(d, type)) +
                            " source animal def(s) already had it.");
            }

            HashSet<string> excluded = Exclusions();
            List<string> missing = (ThingDefOf.Human.comps ?? new List<CompProperties>())
                .Where(c => c?.compClass != null &&
                            !excluded.Contains(c.GetType().ToString()) &&
                            !excluded.Contains(c.compClass.ToString()) &&
                            sapient.Any(d => !HasComp(d, c.compClass)))
                .Select(c => c.compClass.FullName)
                .Distinct()
                .ToList();
            if (missing.Count > 0)
                Log.Warning(Startup.Tag + "Human comps not on every sapient def: " + string.Join(", ", missing));
        }
    }

    [HarmonyPatch]
    internal static class Patch_SocioButterflyGetFoodPostfix_NullGuard
    {
        private static MethodBase target;
        private static readonly HashSet<ThingDef> Reported = new HashSet<ThingDef>();

        private static bool Prepare()
        {
            if (Startup.SocioButterflyCompType == null || Startup.SocioButterflyPropsType == null)
            {
                Log.Message(Startup.Tag + "Socio Butterfly not detected; null guard inactive.");
                return false;
            }

            Type t = AccessTools.TypeByName("RecreationalSexWithEuterpe.JobGiver_GetFood_GetPriority_Patch");
            target = t == null ? null
                : AccessTools.Method(t, "Postfix", new[] { typeof(Pawn), typeof(float).MakeByRefType() });
            if (target == null)
                Log.Warning(Startup.Tag + "Socio Butterfly GetFood postfix not found (renamed or fixed upstream?). Null guard inactive.");
            return target != null;
        }

        private static MethodBase TargetMethod() => target;

        private static readonly ConditionalWeakTable<Pawn, object> KnownHasTracker =
            new ConditionalWeakTable<Pawn, object>();
        private static readonly object Yes = new object();

        private static volatile bool errorLogged;

        private static bool Prefix(Pawn pawn)
        {
            try
            {
                return ShouldRunOriginal(pawn);
            }
            catch (Exception e)
            {
                if (!errorLogged)
                {
                    errorLogged = true;
                    Log.Error(Startup.Tag + "Null guard failed; skipping Socio Butterfly's food-priority postfix " +
                              "while this persists (logged once). " + e);
                }
                return false;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool ShouldRunOriginal(Pawn pawn)
        {
            if (pawn == null || !pawn.IsColonistPlayerControlled || !pawn.RaceProps.Humanlike) return true;

            if (KnownHasTracker.TryGetValue(pawn, out _)) return true;

            List<ThingComp> comps = pawn.AllComps;
            for (int i = 0; i < comps.Count; i++)
            {
                if (Startup.SocioButterflyCompType.IsInstanceOfType(comps[i]))
                {
                    try { KnownHasTracker.Add(pawn, Yes); } catch (ArgumentException) { }
                    return true;
                }
            }

            lock (Reported)
            {
                if (Reported.Add(pawn.def))
                    Log.Warning(Startup.Tag + "Pawn '" + pawn.LabelShort + "' (def " + pawn.def?.defName +
                                ") has no Socio Butterfly tracker comp; skipping food-priority postfix for this def.");
            }
            return false;
        }
    }
}
