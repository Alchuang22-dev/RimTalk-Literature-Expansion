/*
 * Purpose:
 * - Carry temporary prompt override state for a single TalkRequest.
 *
 * Uses:
 * - RimTalk TalkRequest
 *
 * Responsibilities:
 * - Store flags / override text used during prompt construction.
 *
 * Design notes:
 * - Lifetime is strictly per-request.
 *
 * Do NOT:
 * - Do not persist this object.
 * - Do not apply overrides globally.
 */
