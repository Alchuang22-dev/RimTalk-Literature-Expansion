/*
 * Purpose:
 * - Temporarily override RimTalk prompt behavior for specific requests
 *   (e.g. memory summary generation).
 *
 * Uses:
 * - RimTalk PromptService
 * - PromptOverrideService
 *
 * Responsibilities:
 * - Intercept prompt building at request scope.
 * - Inject custom instruction when explicitly enabled.
 *
 * Design notes:
 * - Overrides must be reversible and request-local.
 *
 * Do NOT:
 * - Do not change Constant.Instruction globally.
 * - Do not affect unrelated TalkRequests.
 */
