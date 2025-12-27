/*
 * File: SynopsisPromptBuilder.cs
 *
 * Purpose:
 * - Build the LLM prompt for generating a book synopsis.
 *
 * Dependencies:
 * - BookMeta
 * - Pawn context (if provided externally)
 *
 * Responsibilities:
 * - Construct a clear, bounded instruction.
 * - Request structured JSON output (BookSynopsis).
 *
 * Do NOT:
 * - Do not call AIService.
 * - Do not inject RimTalk Constant.Instruction.
 */
