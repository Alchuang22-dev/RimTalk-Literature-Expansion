/*
 * Purpose:
 * - Helper object to construct a TalkRequest or Query<T> for memory summarization.
 *
 * Uses:
 * - RimTalk TalkRequest
 * - PromptOverrideService
 *
 * Responsibilities:
 * - Provide prompt instructions that request MemorySummarySpec JSON output.
 *
 * Design notes:
 * - This does NOT send the request itself.
 *
 * Do NOT:
 * - Do not directly invoke AIService.
 * - Do not inject Constant.Instruction.
 */
