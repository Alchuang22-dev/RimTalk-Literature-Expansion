using System;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimTalk_LiteratureExpansion.events.letters
{
    public static class LetterGiftResolver
    {
        private sealed class GiftOption
        {
            public string Kind;
            public ThingDef Def;
            public IntRange CountRange;

            public GiftOption(string kind, ThingDef def, IntRange countRange)
            {
                Kind = kind;
                Def = def;
                CountRange = countRange;
            }
        }

        private static readonly GiftOption[] Options =
        {
            new GiftOption("food", ThingDefOf.Pemmican, new IntRange(15, 30)),
            new GiftOption("medicine", ThingDefOf.MedicineHerbal, new IntRange(2, 5)),
            new GiftOption("textile", ThingDefOf.Cloth, new IntRange(25, 50)),
            new GiftOption("materials", ThingDefOf.WoodLog, new IntRange(30, 60)),
            new GiftOption("materials", ThingDefOf.Steel, new IntRange(25, 45)),
            new GiftOption("components", ThingDefOf.ComponentIndustrial, new IntRange(1, 2))
        };

        public static bool TryResolveGift(string giftKind, out ThingDef def, out int count)
        {
            def = null;
            count = 0;

            var matches = GetMatchingOptions(giftKind);
            var option = matches.Length > 0 ? matches.RandomElement() : Options.RandomElement();
            if (option?.Def == null) return false;

            def = option.Def;
            count = Mathf.Clamp(option.CountRange.RandomInRange, 1, def.stackLimit);
            return true;
        }

        private static GiftOption[] GetMatchingOptions(string giftKind)
        {
            if (string.IsNullOrWhiteSpace(giftKind)) return Array.Empty<GiftOption>();

            var lower = giftKind.Trim().ToLowerInvariant();
            int count = 0;
            for (int i = 0; i < Options.Length; i++)
            {
                if (string.Equals(Options[i].Kind, lower, StringComparison.OrdinalIgnoreCase))
                    count++;
            }

            if (count == 0) return Array.Empty<GiftOption>();

            var result = new GiftOption[count];
            int index = 0;
            for (int i = 0; i < Options.Length; i++)
            {
                if (string.Equals(Options[i].Kind, lower, StringComparison.OrdinalIgnoreCase))
                    result[index++] = Options[i];
            }

            return result;
        }
    }
}
