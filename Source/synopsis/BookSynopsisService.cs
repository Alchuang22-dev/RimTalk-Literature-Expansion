/*
 * File: BookSynopsisService.cs
 *
 * Purpose:
 * - Central service for generating or retrieving book synopses.
 *
 * Dependencies:
 * - AIService.Query<T>
 * - SynopsisPromptBuilder
 * - BookSynopsisCache
 *
 * Responsibilities:
 * - Check cache first.
 * - If missing, call LLM to generate BookSynopsis.
 *
 * Design notes:
 * - This is the ONLY class allowed to trigger LLM generation of book content.
 *
 * Do NOT:
 * - Do not apply results to Book objects.
 * - Do not manage scan scheduling.
 */
