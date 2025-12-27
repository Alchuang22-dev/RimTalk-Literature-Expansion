/*
 * File: MapBookScanner.cs
 *
 * Purpose:
 * - Scan the current Map for book Things that have not yet been processed.
 *
 * Dependencies:
 * - Verse.Map
 * - Map.listerThings
 * - BookClassifier
 * - PendingBookQueue
 *
 * Responsibilities:
 * - Iterate all Things in the map.
 * - Identify books via BookClassifier.
 * - Enqueue unprocessed books for later handling.
 *
 * Do NOT:
 * - Do not generate book content.
 * - Do not write to save data directly.
 * - Do not run LLM calls.
 */
