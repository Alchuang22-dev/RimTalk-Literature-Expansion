/*
 * Purpose:
 * - Periodically trigger book scanning (e.g. once per in-game day).
 *
 * Uses:
 * - RimWorld tick system
 * - BookScanScheduler
 *
 * Responsibilities:
 * - Delegate timing decisions to scheduler.
 *
 * Design notes:
 * - Patch should be extremely lightweight.
 *
 * Do NOT:
 * - Do not scan maps directly.
 * - Do not perform LLM calls here.
 */
