/*
 * File: BookType.cs
 *
 * Purpose:
 * - Define internal book categories used by RimTalk Literature Expansion.
 * - This enum is ONLY for classification results, not gameplay logic.
 *
 * Dependencies:
 * - None (pure enum).
 *
 * Design notes:
 * - Only include book types that can be reliably identified from:
 *   - Vanilla RimWorld
 *   - Vanilla Books Expanded (VBE)
 *   - Children's Books
 * - Do NOT invent speculative types (e.g. Novel, Textbook) unless backed by code.
 *
 * Expected values:
 * - VanillaBook
 * - VBE_Newspaper
 * - VBE_SkillBook
 * - CB_ChildrensBook
 * - CB_ColoringBook
 *
 * Do NOT:
 * - Do not try to mirror RimWorld BookOutcomeDoer hierarchy.
 * - Do not encode gameplay effects here.
 */


namespace RimTalk_LiteratureExpansion.book
{
    /// <summary>
    /// 仅基于已提供源码/反编译文件能可靠识别的类型。
    /// </summary>
    public enum BookType
    {
        Unknown = 0,

        /// <summary>
        /// Vanilla：ThingDef.HasComp<RimWorld.CompBook>() 或 Verse.Book
        /// </summary>
        VanillaBook = 1,

        /// <summary>
        /// VBE：VanillaBooksExpanded.Newspaper : Verse.Book
        /// </summary>
        VBE_Newspaper = 10,

        /// <summary>
        /// VBE：ThingDef 带有 DefModExtension VanillaBooksExpanded.RecipeSkillBook（字段：SkillDef skill）
        /// </summary>
        VBE_SkillBook = 11,

        /// <summary>
        /// Children’s Books：ThingDef == Childrens_Books.ChildrensBookDefOf.BBLK_ChildrensBook
        /// </summary>
        CB_ChildrensBook = 20,

        /// <summary>
        /// Children’s Books：ThingDef == Childrens_Books.ChildrensBookDefOf.BBLK_ColoringBook
        /// </summary>
        CB_ColoringBook = 21
    }
}
