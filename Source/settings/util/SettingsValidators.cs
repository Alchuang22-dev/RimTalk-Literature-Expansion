/*
 * Purpose:
 * - Local validation helpers for settings inputs.
 *
 * Uses:
 * - System.Uri parsing
 *
 * Responsibilities:
 * - Validate baseUrl is a valid absolute URI.
 * - Validate apiKey non-empty when required.
 * - Validate model non-empty (if required by your client).
 *
 * Do NOT:
 * - Do not perform network calls.
 */
using System;

namespace RimTalk_LiteratureExpansion.settings.util
{
    public static class SettingsValidators
    {
        public static bool TryValidateBaseUrl(string baseUrl, out string error)
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                error = "Base URL is empty.";
                return false;
            }

            if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri))
            {
                error = "Base URL must be an absolute URI.";
                return false;
            }

            if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                error = "Base URL must start with http or https.";
                return false;
            }

            error = null;
            return true;
        }

        public static bool TryValidateApiKey(string apiKey, out string error)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                error = "API key is empty.";
                return false;
            }

            error = null;
            return true;
        }

        public static bool TryValidateModel(string model, out string error)
        {
            if (string.IsNullOrWhiteSpace(model))
            {
                error = "Model is empty.";
                return false;
            }

            error = null;
            return true;
        }
    }
}
