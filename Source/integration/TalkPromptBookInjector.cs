/*
 * File: TalkPromptBookInjector.cs
 *
 * Purpose:
 * - Inject book synopsis into a TalkRequest when a pawn is reading
 *   or telling a story.
 *
 * Dependencies:
 * - RimTalk PromptOverrideService
 * - BookSynopsisCache
 * - BookMeta
 *
 * Responsibilities:
 * - Detect current book context (provided by caller).
 * - Temporarily augment the prompt/context for the current TalkRequest.
 *
 * Design notes:
 * - Injection is local to a single request.
 * - Should not affect global Constant.Instruction.
 *
 * Do NOT:
 * - Do not persist prompt changes.
 * - Do not generate synopsis here.
 */
