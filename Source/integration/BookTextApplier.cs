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
