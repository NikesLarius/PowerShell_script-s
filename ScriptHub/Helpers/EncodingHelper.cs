using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace ScriptHub.Helpers;

public static class EncodingHelper
{
    public static readonly Encoding Utf8WithBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);

    public static string SanitizeCode(string code)
    {
        if (string.IsNullOrEmpty(code)) return string.Empty;

        return code
            .Replace('\u00A0', ' ')   // Non-breaking space (NBSP)
            .Replace('\u200B', ' ')   // Zero-width space
            .Replace('\u202F', ' ')   // Narrow no-break space
            .Replace("\uFEFF", "");   // Zero-width no-break space inside body
    }

    public static string ReadTextAutoEncoding(string path)
    {
        if (!File.Exists(path)) return string.Empty;

        var bytes = File.ReadAllBytes(path);
        if (bytes.Length == 0) return string.Empty;

        // Check for UTF-8 BOM (0xEF, 0xBB, 0xBF)
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
        }

        // Try strict UTF-8
        try
        {
            var strictUtf8 = new UTF8Encoding(false, true);
            return strictUtf8.GetString(bytes);
        }
        catch
        {
            // Fallback Windows-1251 (ANSI)
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            var win1251 = Encoding.GetEncoding(1251);
            return win1251.GetString(bytes);
        }
    }

    public static async Task<string> ReadTextAutoEncodingAsync(string path)
    {
        if (!File.Exists(path)) return string.Empty;

        var bytes = await File.ReadAllBytesAsync(path);
        if (bytes.Length == 0) return string.Empty;

        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
        }

        try
        {
            var strictUtf8 = new UTF8Encoding(false, true);
            return strictUtf8.GetString(bytes);
        }
        catch
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            var win1251 = Encoding.GetEncoding(1251);
            return win1251.GetString(bytes);
        }
    }

    public static void WriteTextUtf8Bom(string path, string content)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);

        var sanitized = SanitizeCode(content);
        File.WriteAllText(path, sanitized, Utf8WithBom);
    }

    public static async Task WriteTextUtf8BomAsync(string path, string content)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);

        var sanitized = SanitizeCode(content);
        await File.WriteAllTextAsync(path, sanitized, Utf8WithBom);
    }

    public static void EnsurePs1FileEncoding(string path)
    {
        try
        {
            if (!File.Exists(path) || !path.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase))
                return;

            var bytes = File.ReadAllBytes(path);
            if (bytes.Length == 0) return;

            bool hasBom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
            var text = ReadTextAutoEncoding(path);
            var sanitized = SanitizeCode(text);

            if (!hasBom || sanitized != text)
            {
                File.WriteAllText(path, sanitized, Utf8WithBom);
            }
        }
        catch { }
    }
}
