/*
 * File: BookSynopsisCache.cs
 *
 * Purpose:
 * - Cache generated book synopses to avoid repeated LLM calls.
 *
 * Dependencies:
 * - BookKey
 * - BookSynopsis
 *
 * Responsibilities:
 * - Store and retrieve synopsis by BookKey.
 * - Used by scanner and integration layers.
 *
 * Do NOT:
 * - Do not generate content.
 * - Do not decide scanning logic.
 */
