using System;
using System.IO;
using System.Text.RegularExpressions;

namespace ScriptHub.Helpers;

public static class ScriptMetadataHelper
{
    public static (string Title, string Description) ExtractMetadata(string filePath, string content)
    {
        var defaultTitle = Path.GetFileNameWithoutExtension(filePath);
        var ext = Path.GetExtension(filePath).ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(content))
        {
            return (defaultTitle, "Импортированный скрипт");
        }

        string? extractedTitle = null;
        string? extractedDescription = null;

        if (ext == ".ps1")
        {
            // 1. Try PowerShell block comment <# ... #>
            var blockCommentMatch = Regex.Match(content, @"<#\s*(.*?)\s*#>", RegexOptions.Singleline);
            if (blockCommentMatch.Success)
            {
                var blockText = blockCommentMatch.Groups[1].Value;

                // Check for .SYNOPSIS
                var synopsisMatch = Regex.Match(blockText, @"\.SYNOPSIS\s*(.*?)(?=\r?\n\s*\.[a-zA-Z]+|\Z)", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                if (synopsisMatch.Success)
                {
                    extractedTitle = CleanCommentText(synopsisMatch.Groups[1].Value);
                }

                // Check for .DESCRIPTION
                var descMatch = Regex.Match(blockText, @"\.DESCRIPTION\s*(.*?)(?=\r?\n\s*\.[a-zA-Z]+|\Z)", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                if (descMatch.Success)
                {
                    extractedDescription = CleanCommentText(descMatch.Groups[1].Value);
                }
                else if (string.IsNullOrWhiteSpace(extractedDescription) && !string.IsNullOrWhiteSpace(extractedTitle))
                {
                    extractedDescription = extractedTitle;
                }
            }

            // 2. Try single-line comments at the top
            if (string.IsNullOrWhiteSpace(extractedDescription))
            {
                var lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
                foreach (var rawLine in lines)
                {
                    var line = rawLine.Trim();
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    if (line.StartsWith("#") && !line.StartsWith("#!") && !line.StartsWith("#requires", StringComparison.OrdinalIgnoreCase))
                    {
                        var text = line.TrimStart('#').Trim();
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            if (string.IsNullOrWhiteSpace(extractedDescription))
                            {
                                extractedDescription = text;
                            }
                            break;
                        }
                    }
                    else
                    {
                        // Stop at first non-comment line
                        break;
                    }
                }
            }
        }
        else if (ext == ".bat" || ext == ".cmd")
        {
            var lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();
                if (string.IsNullOrWhiteSpace(line) || line.Equals("@echo off", StringComparison.OrdinalIgnoreCase) || line.Equals("echo off", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (line.StartsWith("::"))
                {
                    var text = line.TrimStart(':').Trim();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        extractedDescription = text;
                        break;
                    }
                }
                else if (line.StartsWith("REM ", StringComparison.OrdinalIgnoreCase))
                {
                    var text = line[4..].Trim();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        extractedDescription = text;
                        break;
                    }
                }
                else
                {
                    break;
                }
            }
        }

        var finalTitle = !string.IsNullOrWhiteSpace(extractedTitle) && extractedTitle.Length <= 60
            ? extractedTitle
            : defaultTitle;

        var finalDescription = !string.IsNullOrWhiteSpace(extractedDescription)
            ? extractedDescription
            : "Импортированный скрипт";

        return (finalTitle, finalDescription);
    }

    private static string CleanCommentText(string text)
    {
        var cleaned = Regex.Replace(text, @"^\s*#\s*", "", RegexOptions.Multiline);
        cleaned = cleaned.Trim();
        // Limit to reasonable length if it's too long
        if (cleaned.Length > 300)
        {
            cleaned = cleaned[..297] + "...";
        }
        return cleaned;
    }
}
