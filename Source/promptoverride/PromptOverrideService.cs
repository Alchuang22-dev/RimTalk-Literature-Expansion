/*
 * Purpose:
 * - Manage enabling and disabling prompt overrides for RimTalk requests.
 *
 * Uses:
 * - PromptOverrideContext
 * - RimTalk PromptService (via patch)
 *
 * Responsibilities:
 * - Provide a safe API to apply prompt overrides.
 * - Ensure overrides are cleared after use.
 *
 * Design notes:
 * - This is the ONLY place that controls prompt overrides.
 *
 * Do NOT:
 * - Do not hardcode prompt text here.
 * - Do not bypass RimTalk context building.
 */
 