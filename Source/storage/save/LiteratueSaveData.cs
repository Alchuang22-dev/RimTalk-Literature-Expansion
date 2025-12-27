/*
 * File: LiteratueSaveData.cs
 *
 * Purpose:
 * - Persist Literature Expansion data across save/load.
 *
 * Dependencies:
 * - Verse.IExposable
 * - BookSynopsisCache (or underlying data)
 *
 * Responsibilities:
 * - Expose cached synopsis and processed-book markers.
 *
 * Do NOT:
 * - Do not perform logic during ExposeData beyond serialization.
 */
