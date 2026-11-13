using System.Text;

namespace LP.Journal.Services.Journal;

/// <summary>
/// Оценивает, насколько черновик повторяет исходный текст.
/// Используются shingle-5 (пять слов подряд): это дешёвая, но показательная
/// проверка на дословное копирование.
/// </summary>
public sealed class NewsSimilarityService
{
    private const int ShingleSize = 5;

    public int CalculatePercent(string draft, string source)
    {
        var draftShingles = BuildShingles(draft);
        if (draftShingles.Count == 0)
        {
            return 0;
        }

        var sourceShingles = BuildShingles(source);
        if (sourceShingles.Count == 0)
        {
            return 0;
        }

        var matches = draftShingles.Count(sourceShingles.Contains);
        return (int)Math.Round(matches * 100d / draftShingles.Count);
    }

    private static HashSet<string> BuildShingles(string? text)
    {
        var words = Tokenize(text);
        var shingles = new HashSet<string>(StringComparer.Ordinal);

        if (words.Count < ShingleSize)
        {
            return shingles;
        }

        for (var i = 0; i <= words.Count - ShingleSize; i++)
        {
            shingles.Add(string.Join(' ', words.Skip(i).Take(ShingleSize)));
        }

        return shingles;
    }

    private static List<string> Tokenize(string? text)
    {
        var words = new List<string>();

        if (string.IsNullOrWhiteSpace(text))
        {
            return words;
        }

        var builder = new StringBuilder();

        foreach (var raw in text.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(raw))
            {
                builder.Append(raw);
            }
            else if (builder.Length > 0)
            {
                words.Add(builder.ToString());
                builder.Clear();
            }
        }

        if (builder.Length > 0)
        {
            words.Add(builder.ToString());
        }

        return words;
    }
}
