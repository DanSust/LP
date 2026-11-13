using System.Text;

namespace LP.Journal.Services.Journal;

/// <summary>
/// Загрузка внешнего контента с жёстким лимитом размера.
/// </summary>
public static class JournalHttp
{
    public static async Task<byte[]> ReadLimitedAsync(
        HttpResponseMessage response,
        int maxBytes,
        CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var buffer = new MemoryStream();
        var chunk = new byte[16 * 1024];
        var total = 0;

        while (true)
        {
            var read = await stream.ReadAsync(chunk, cancellationToken);
            if (read == 0)
            {
                break;
            }

            total += read;
            if (total > maxBytes)
            {
                throw new InvalidOperationException("Ответ источника слишком большой.");
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }

    public static string Decode(byte[] bytes, string? charset)
    {
        if (!string.IsNullOrWhiteSpace(charset))
        {
            try
            {
                return Encoding.GetEncoding(charset.Trim('"', ' ')).GetString(bytes);
            }
            catch (ArgumentException)
            {
                // Неизвестная кодировка — используем UTF-8.
            }
        }

        return Encoding.UTF8.GetString(bytes);
    }
}
