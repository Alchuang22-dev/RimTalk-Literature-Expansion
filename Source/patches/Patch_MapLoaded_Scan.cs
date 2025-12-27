/*
 * Purpose:
 * - Trigger an initial book scan when a map is loaded.
 *
 * Uses:
 * - RimWorld Map lifecycle
 * - BookScanScheduler / MapBookScanner
 *
 * Responsibilities:
 * - Ensure existing books are discovered after load.
 *
 * Design notes:
 * - One-time or guarded execution only.
 *
 * Do NOT:
 * - Do not scan repeatedly here.
 * - Do not enqueue duplicate work.
 */
