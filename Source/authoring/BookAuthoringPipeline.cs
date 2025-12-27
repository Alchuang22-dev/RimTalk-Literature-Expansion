/*
 * Purpose:
 * - Orchestrate the book authoring pipeline when a pawn finishes writing a book.
 *
 * Uses:
 * - RimTalk AIService.Query<T>
 * - MemorySummaryRequest / BookFromSummaryRequest
 * - BookSynopsisService (optional finalization)
 *
 * Flow:
 * 1) Create a TalkRequest with normal RimTalk context injection.
 * 2) Override prompt to request a structured MemorySummarySpec (JSON).
 * 3) Feed the summary into a second Query<T> to generate book title/synopsis.
 *
 * Design notes:
 * - This class coordinates steps only; no LLM prompt strings are hardcoded here.
 *
 * Do NOT:
 * - Do not directly read RimTalkMemoryExpansion.
 * - Do not call Chat/ChatStreaming.
 * - Do not write book UI fields here.
 */
