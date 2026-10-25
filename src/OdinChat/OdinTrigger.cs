using System;

namespace OdinChat
{
    internal static class OdinTrigger
    {
        internal static bool TryExtractQuestion(string text, string trigger, out string question)
        {
            question = null;
            if (string.IsNullOrWhiteSpace(text))
                return false;

            if (string.IsNullOrWhiteSpace(trigger))
                trigger = "odin";

            string trimmed = text.Trim();
            if (!trimmed.StartsWith(trigger, StringComparison.OrdinalIgnoreCase))
                return false;

            if (trimmed.Length == trigger.Length)
            {
                question = string.Empty;
                return true;
            }

            char next = trimmed[trigger.Length];
            if (!IsSeparator(next))
                return false;

            question = trimmed
                .Substring(trigger.Length)
                .TrimStart(' ', '\t', ',', '!', ':', '-', '.', '?', ';')
                .Trim();

            return true;
        }

        private static bool IsSeparator(char c)
        {
            return char.IsWhiteSpace(c) || c == ',' || c == '!' || c == ':' || c == '-' || c == '.' || c == '?' || c == ';';
        }
    }
}
