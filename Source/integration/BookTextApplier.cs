/*
 * File: BookTextApplier.cs
 *
 * Purpose:
 * - Apply generated book title and synopsis back to in-game Book objects.
 *
 * Dependencies:
 * - Verse.Book
 * - BookSynopsis (model)
 *
 * Responsibilities:
 * - Update book display fields such as:
 *   - Title
 *   - FlavorUI
 *   - DescriptionDetailed
 *
 * Design notes:
 * - This class performs UI/data mutation ONLY.
 * - It does NOT decide when or how content is generated.
 *
 * Do NOT:
 * - Do not call LLM services.
 * - Do not scan maps or track state.
 * - Do not modify ThingDef or defs.
 */
using System;
using System.Reflection;
using RimTalk_LiteratureExpansion.book;
using RimTalk_LiteratureExpansion.synopsis.model;
using Verse;

namespace RimTalk_LiteratureExpansion.integration
{
    public static class BookTextApplier
    {
        public static bool Apply(BookMeta meta, BookSynopsis synopsis)
        {
            if (meta == null || synopsis == null) return false;

            var title = string.IsNullOrWhiteSpace(synopsis.Title) ? meta.Title : synopsis.Title;
            var text = synopsis.Synopsis ?? string.Empty;

            bool changed = false;

            if (meta.Book != null)
            {
                ApplyToBook(meta.Book, title, text);
                changed = true;
            }
            else
            {
                changed |= ApplyToThing(meta.Thing, title, text);
            }

            var thingWithComps = meta.Thing as ThingWithComps;
            if (thingWithComps != null)
                changed |= ApplyToComps(thingWithComps, title, text);

            return changed;
        }

        private static void ApplyToBook(Book book, string title, string synopsis)
        {
            if (book == null) return;

            TrySetString(book, "Title", "title", title);
            TrySetString(book, "FlavorUI", "descriptionFlavor", synopsis);
            TrySetString(book, "DescriptionDetailed", "description", synopsis);
            TrySetBool(book, "descCanBeInvalidated", false);
        }

        private static bool ApplyToThing(Thing thing, string title, string synopsis)
        {
            if (thing == null) return false;

            bool changed = false;
            changed |= TrySetString(thing, "Title", "title", title);
            changed |= TrySetString(thing, "FlavorUI", "descriptionFlavor", synopsis);
            changed |= TrySetString(thing, "DescriptionDetailed", "description", synopsis);
            return changed;
        }

        private static bool ApplyToComps(ThingWithComps thing, string title, string synopsis)
        {
            if (thing == null) return false;

            var comps = thing.AllComps;
            if (comps == null || comps.Count == 0) return false;

            bool changed = false;
            for (int i = 0; i < comps.Count; i++)
            {
                var comp = comps[i];
                if (comp == null || !ShouldApplyToComp(comp)) continue;

                changed |= TrySetString(comp, "Title", "title", title);
                changed |= TrySetString(comp, "Label", "label", title);
                changed |= TrySetString(comp, "BookTitle", "bookTitle", title);
                changed |= TrySetString(comp, "BookLabel", "bookLabel", title);
                changed |= TrySetString(comp, "Description", "description", synopsis);
                changed |= TrySetString(comp, "DescriptionDetailed", "descriptionDetailed", synopsis);
                changed |= TrySetString(comp, "FlavorUI", "descriptionFlavor", synopsis);
                changed |= TrySetString(comp, "BookDescription", "bookDescription", synopsis);
                changed |= TrySetString(comp, "BookText", "bookText", synopsis);
            }

            return changed;
        }

        private static bool ShouldApplyToComp(object comp)
        {
            var fullName = comp.GetType().FullName ?? string.Empty;
            if (fullName.StartsWith("MedievalOverhaul.", StringComparison.Ordinal))
                return true;

            if (fullName.IndexOf("DefinableBook", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            return false;
        }

        private static bool TrySetString(object target, string propertyName, string fieldName, string value)
        {
            if (target == null) return false;

            var type = target.GetType();

            var prop = type.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var setter = prop?.GetSetMethod(true);
            if (setter != null)
            {
                prop.SetValue(target, value, null);
                return true;
            }

            var field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null && field.FieldType == typeof(string))
            {
                field.SetValue(target, value);
                return true;
            }

            return false;
        }

        private static void TrySetBool(object target, string fieldName, bool value)
        {
            if (target == null) return;

            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null && field.FieldType == typeof(bool))
                field.SetValue(target, value);
        }
    }
}
