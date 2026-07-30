using System.Text.RegularExpressions;

namespace PageToMovie.Engine;

public sealed record DialogueSpeechBeat(
    string DialogueText,
    int WordCount,
    double EstimatedDurationSeconds
);

public static class DialoguePacingSplitter
{
    private static readonly Regex TerminalPunctuationRegex = new(@"[.!?](?=\s|$)", RegexOptions.Compiled);
    private static readonly Regex ProsodicPunctuationRegex = new(@"[—;:](?=\s|$)|—", RegexOptions.Compiled);
    private static readonly Regex ConjunctionCommaRegex = new(@",\s+(?:and|but|or|so|yet|because|when|as|while|since)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex AnyCommaRegex = new(@",(?=\s)", RegexOptions.Compiled);

    /// <summary>
    /// Splits a dialogue turn into optimal 5s-9s speech beats (12-22 words) based on natural prosodic punctuation pauses.
    /// </summary>
    public static List<DialogueSpeechBeat> SplitDialogue(
        string dialogueText,
        int minWords = 12,
        int targetMaxWords = 22,
        int hardMaxWords = 25,
        double wordsPerSecond = 2.6)
    {
        if (string.IsNullOrWhiteSpace(dialogueText))
            return new List<DialogueSpeechBeat>();

        var cleaned = dialogueText.Trim();
        var words = cleaned.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length <= targetMaxWords)
        {
            var dur = Math.Max(2.0, Math.Round(words.Length / wordsPerSecond, 1));
            return new List<DialogueSpeechBeat> { new DialogueSpeechBeat(cleaned, words.Length, dur) };
        }

        // Split into candidate clause chunks using natural punctuation hierarchy
        var rawChunks = SplitIntoClauseChunks(cleaned, hardMaxWords);
        
        // Merge tiny orphan chunks (< 4 words) with adjacent beats
        var mergedBeats = MergeOrphanChunks(rawChunks, targetMaxWords);

        // Convert to DialogueSpeechBeat DTOs
        var results = new List<DialogueSpeechBeat>();
        foreach (var chunk in mergedBeats)
        {
            var chunkWords = chunk.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
            if (chunkWords > 0)
            {
                var duration = Math.Max(2.5, Math.Round(chunkWords / wordsPerSecond, 1));
                results.Add(new DialogueSpeechBeat(chunk, chunkWords, duration));
            }
        }

        return results;
    }

    private static List<string> SplitIntoClauseChunks(string text, int maxWords)
    {
        var chunks = new List<string>();
        var currentWords = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (currentWords.Length <= maxWords)
        {
            chunks.Add(text);
            return chunks;
        }

        // Tier 1: Try splitting by terminal punctuation (. ! ?)
        var sentences = SplitByRegexMatches(text, TerminalPunctuationRegex);
        if (sentences.Count > 1)
        {
            var accum = "";
            foreach (var s in sentences)
            {
                var combined = string.IsNullOrEmpty(accum) ? s : accum + " " + s;
                var wordCount = combined.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
                if (wordCount <= maxWords)
                {
                    accum = combined;
                }
                else
                {
                    if (!string.IsNullOrEmpty(accum))
                        chunks.AddRange(SplitIntoClauseChunks(accum, maxWords));
                    accum = s;
                }
            }
            if (!string.IsNullOrEmpty(accum))
                chunks.AddRange(SplitIntoClauseChunks(accum, maxWords));

            return chunks;
        }

        // Tier 2: Try splitting by prosodic punctuation (— ; :)
        var prosodicChunks = SplitByRegexMatches(text, ProsodicPunctuationRegex);
        if (prosodicChunks.Count > 1)
        {
            var accum = "";
            foreach (var pc in prosodicChunks)
            {
                var combined = string.IsNullOrEmpty(accum) ? pc : accum + " " + pc;
                var wordCount = combined.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
                if (wordCount <= maxWords)
                {
                    accum = combined;
                }
                else
                {
                    if (!string.IsNullOrEmpty(accum))
                        chunks.Add(accum);
                    accum = pc;
                }
            }
            if (!string.IsNullOrEmpty(accum))
                chunks.Add(accum);

            return chunks;
        }

        // Tier 3: Try splitting by conjunction commas (, and / , but / , as)
        var conjChunks = SplitByRegexMatches(text, ConjunctionCommaRegex);
        if (conjChunks.Count > 1)
        {
            var accum = "";
            foreach (var cc in conjChunks)
            {
                var combined = string.IsNullOrEmpty(accum) ? cc : accum + " " + cc;
                var wordCount = combined.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
                if (wordCount <= maxWords)
                {
                    accum = combined;
                }
                else
                {
                    if (!string.IsNullOrEmpty(accum))
                        chunks.Add(accum);
                    accum = cc;
                }
            }
            if (!string.IsNullOrEmpty(accum))
                chunks.Add(accum);

            return chunks;
        }

        // Tier 4: Fallback to any comma or whitespace split
        var commaChunks = SplitByRegexMatches(text, AnyCommaRegex);
        if (commaChunks.Count > 1)
        {
            var accum = "";
            foreach (var cc in commaChunks)
            {
                var combined = string.IsNullOrEmpty(accum) ? cc : accum + " " + cc;
                var wordCount = combined.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
                if (wordCount <= maxWords)
                {
                    accum = combined;
                }
                else
                {
                    if (!string.IsNullOrEmpty(accum))
                        chunks.Add(accum);
                    accum = cc;
                }
            }
            if (!string.IsNullOrEmpty(accum))
                chunks.Add(accum);

            return chunks;
        }

        // Hard Fallback: Word window split
        chunks.Add(text);
        return chunks;
    }

    private static List<string> SplitByRegexMatches(string text, Regex regex)
    {
        var matches = regex.Matches(text);
        if (matches.Count == 0)
            return new List<string> { text };

        var parts = new List<string>();
        int lastIndex = 0;
        foreach (Match m in matches)
        {
            int splitEnd = m.Index + m.Length;
            string part = text.Substring(lastIndex, splitEnd - lastIndex).Trim();
            if (!string.IsNullOrWhiteSpace(part))
                parts.Add(part);
            lastIndex = splitEnd;
        }

        if (lastIndex < text.Length)
        {
            string remainder = text.Substring(lastIndex).Trim();
            if (!string.IsNullOrWhiteSpace(remainder))
                parts.Add(remainder);
        }

        return parts;
    }

    private static List<string> MergeOrphanChunks(List<string> rawChunks, int maxWords)
    {
        if (rawChunks.Count <= 1)
            return rawChunks;

        var merged = new List<string>();
        string? current = null;

        foreach (var chunk in rawChunks)
        {
            if (current == null)
            {
                current = chunk;
                continue;
            }

            var chunkWords = chunk.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
            var currentWords = current.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;

            // If chunk is an orphan (< 4 words) or combining them stays under maxWords
            if (chunkWords < 4 || (currentWords + chunkWords) <= maxWords)
            {
                current = current + " " + chunk;
            }
            else
            {
                merged.Add(current);
                current = chunk;
            }
        }

        if (current != null)
        {
            if (merged.Count > 0)
            {
                var currentWords = current.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
                if (currentWords < 4)
                {
                    merged[merged.Count - 1] = merged[merged.Count - 1] + " " + current;
                }
                else
                {
                    merged.Add(current);
                }
            }
            else
            {
                merged.Add(current);
            }
        }

        return merged;
    }
}
