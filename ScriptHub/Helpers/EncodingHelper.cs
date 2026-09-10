using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ScriptHub.Helpers;

public static class EncodingHelper
{
    private static readonly UTF8Encoding _utf8Strict = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
    public static readonly Encoding Utf8WithBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);

    static EncodingHelper()
    {
        try
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }
        catch { }
    }

    public static Encoding GetSystemOemEncoding()
    {
        try
        {
            int oemCp = CultureInfo.CurrentCulture.TextInfo.OEMCodePage;
            if (oemCp > 0)
            {
                return Encoding.GetEncoding(oemCp);
            }
        }
        catch { }

        try
        {
            return Encoding.GetEncoding(866); // Default Russian OEM (CP866)
        }
        catch
        {
            return Encoding.Default;
        }
    }

    public static Encoding GetSystemAnsiEncoding()
    {
        try
        {
            int ansiCp = CultureInfo.CurrentCulture.TextInfo.ANSICodePage;
            if (ansiCp > 0)
            {
                return Encoding.GetEncoding(ansiCp);
            }
        }
        catch { }

        try
        {
            return Encoding.GetEncoding(1251); // Default Russian ANSI (Windows-1251)
        }
        catch
        {
            return Encoding.Default;
        }
    }

    public static string DecodeBytesAuto(byte[] bytes, int offset = 0, int count = -1)
    {
        if (bytes == null || bytes.Length == 0) return string.Empty;
        if (count < 0) count = bytes.Length - offset;
        if (count <= 0) return string.Empty;

        // 1. Try strict UTF-8
        try
        {
            return _utf8Strict.GetString(bytes, offset, count);
        }
        catch (DecoderFallbackException) { }

        // 2. Try OEM code page (CP866 on Russian Windows)
        try
        {
            var oem = GetSystemOemEncoding();
            return oem.GetString(bytes, offset, count);
        }
        catch { }

        // 3. Try ANSI code page (Windows-1251 on Russian Windows)
        try
        {
            var ansi = GetSystemAnsiEncoding();
            return ansi.GetString(bytes, offset, count);
        }
        catch { }

        // 4. Default fallback
        return Encoding.Default.GetString(bytes, offset, count);
    }

    public static async Task StreamLinesAsync(
        Stream stream,
        Action<string, bool> outputCallback,
        bool isError,
        CancellationToken cancellationToken = default)
    {
        var buffer = new byte[4096];
        using var lineBuffer = new MemoryStream();

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                if (bytesRead <= 0) break;

                for (int i = 0; i < bytesRead; i++)
                {
                    byte b = buffer[i];
                    if (b == (byte)'\n')
                    {
                        var lineBytes = lineBuffer.ToArray();
                        lineBuffer.SetLength(0);
                        var line = DecodeBytesAuto(lineBytes).TrimEnd('\r');
                        outputCallback(line, isError);
                    }
                    else
                    {
                        lineBuffer.WriteByte(b);
                    }
                }
            }

            if (lineBuffer.Length > 0)
            {
                var lineBytes = lineBuffer.ToArray();
                lineBuffer.SetLength(0);
                var line = DecodeBytesAuto(lineBytes).TrimEnd('\r');
                outputCallback(line, isError);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception) { }
    }

    public static async Task StreamLogFileAsync(
        string logFilePath,
        Process proc,
        Action<string, bool> outputCallback,
        CancellationToken cancellationToken = default)
    {
        using var fileStream = new FileStream(logFilePath, FileMode.OpenOrCreate, FileAccess.Read, FileShare.ReadWrite);
        var buffer = new byte[4096];
        using var lineBuffer = new MemoryStream();

        while (!proc.HasExited || fileStream.Position < fileStream.Length)
        {
            int bytesRead = await fileStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
            if (bytesRead > 0)
            {
                for (int i = 0; i < bytesRead; i++)
                {
                    byte b = buffer[i];
                    if (b == (byte)'\n')
                    {
                        var lineBytes = lineBuffer.ToArray();
                        lineBuffer.SetLength(0);
                        var line = DecodeBytesAuto(lineBytes).TrimEnd('\r');
                        bool isErr = line.Contains("error", StringComparison.OrdinalIgnoreCase) ||
                                     line.Contains("ошибка", StringComparison.OrdinalIgnoreCase) ||
                                     line.StartsWith("Exception:", StringComparison.OrdinalIgnoreCase) ||
                                     line.StartsWith("ПРЕДУПРЕЖДЕНИЕ:", StringComparison.OrdinalIgnoreCase);
                        outputCallback(line, isErr);
                    }
                    else
                    {
                        lineBuffer.WriteByte(b);
                    }
                }
            }
            else
            {
                if (proc.HasExited) break;
                await Task.Delay(100, cancellationToken);
            }
        }

        if (lineBuffer.Length > 0)
        {
            var lineBytes = lineBuffer.ToArray();
            var line = DecodeBytesAuto(lineBytes).TrimEnd('\r');
            bool isErr = line.Contains("error", StringComparison.OrdinalIgnoreCase) ||
                         line.Contains("ошибка", StringComparison.OrdinalIgnoreCase) ||
                         line.StartsWith("Exception:", StringComparison.OrdinalIgnoreCase) ||
                         line.StartsWith("ПРЕДУПРЕЖДЕНИЕ:", StringComparison.OrdinalIgnoreCase);
            outputCallback(line, isErr);
        }
    }

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

        return DecodeBytesAuto(bytes);
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

        return DecodeBytesAuto(bytes);
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
