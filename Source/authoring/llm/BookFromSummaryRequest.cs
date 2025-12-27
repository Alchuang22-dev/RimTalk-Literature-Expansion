/*
 * Purpose:
 * - Helper object to generate book title/synopsis from a memory summary.
 *
 * Uses:
 * - RimTalk AIService.Query<BookTitleSpec>
 *
 * Responsibilities:
 * - Build instruction text based on MemorySummarySpec.
 *
 * Design notes:
 * - This represents the SECOND stage of the authoring pipeline.
 *
 * Do NOT:
 * - Do not access pawn context directly.
 * - Do not perform book classification or UI updates.
 */
