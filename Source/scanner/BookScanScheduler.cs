/*
 * File: BookScanScheduler.cs
 *
 * Purpose:
 * - Control WHEN MapBookScanner runs (e.g. once per in-game day).
 *
 * Dependencies:
 * - RimWorld tick system
 * - Game time utilities
 *
 * Responsibilities:
 * - Track last scan tick.
 * - Trigger scan at configured intervals.
 *
 * Do NOT:
 * - Do not scan maps directly.
 * - Do not enqueue books yourself.
 */
