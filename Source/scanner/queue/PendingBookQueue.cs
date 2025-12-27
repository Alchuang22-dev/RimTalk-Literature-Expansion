/*
 * File: PendingBookQueue.cs
 *
 * Purpose:
 * - Queue of books waiting for synopsis/title generation.
 *
 * Dependencies:
 * - PendingBookRecord
 *
 * Responsibilities:
 * - Enqueue / dequeue pending book tasks.
 * - Avoid duplicates.
 *
 * Design notes:
 * - This is a lightweight in-memory structure.
 *
 * Do NOT:
 * - Do not persist data here.
 * - Do not call LLM services.
 */
