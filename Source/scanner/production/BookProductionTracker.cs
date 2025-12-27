/*
 * File: BookProductionTracker.cs
 *
 * Purpose:
 * - Track books produced via Bill_Production (writing books).
 *
 * Dependencies:
 * - RimWorld Bill_Production
 * - Patch_BillProduction_Finish
 *
 * Responsibilities:
 * - Detect when a book has just been produced.
 * - Mark it as needing synopsis generation.
 *
 * Do NOT:
 * - Do not generate content here.
 * - Do not access LLM services.
 */
