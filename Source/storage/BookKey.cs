/*
 * File: BookKey.cs
 *
 * Purpose:
 * - Provide a stable, save-safe identifier for a specific book instance.
 *
 * Dependencies:
 * - Verse.Thing
 *
 * Design notes:
 * - Should be deterministic across save/load.
 * - Typically derived from ThingID + map or similar stable identifiers.
 *
 * Do NOT:
 * - Do not store large data here.
 */
