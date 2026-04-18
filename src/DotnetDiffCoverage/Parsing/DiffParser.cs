using System.Text.RegularExpressions;
using DotnetDiffCoverage.Models;

namespace DotnetDiffCoverage.Parsing;

/// <summary>
/// Parses unified diff text into a normalized DiffResult.
/// </summary>
public sealed class DiffParser
{
    private static readonly Regex HunkHeaderRegex =
        new(@"^@@ -\d+(?:,\d+)? \+(\d+)(?:,\d+)? @@", RegexOptions.Compiled);

    /// <summary>
    /// Parses the unified diff text and returns the set of added line ranges per file.
    /// </summary>
    public DiffResult Parse(string diffText)
    {
        if (string.IsNullOrWhiteSpace(diffText))
            return DiffResult.Empty;

        var files = new Dictionary<string, DiffFile>(StringComparer.OrdinalIgnoreCase);
        DiffFile? currentFile = null;
        bool isBinary = false;
        int currentLineNumber = 0;
        bool inHunk = false;
        string? pendingMinusPath = null;

        // Tracks the current run of consecutive added lines within a hunk.
        int? runStart = null;
        int? runEnd = null;

        void FlushRun()
        {
            if (runStart.HasValue && currentFile != null)
                currentFile.AddedRanges.Add(new LineRange(runStart.Value, runEnd!.Value));
            runStart = null;
            runEnd = null;
        }

        foreach (var line in diffText.Split('\n'))
        {
            var trimmedLine = line.TrimEnd('\r');

            // Binary file detection — skip this file
            if (trimmedLine.StartsWith("Binary files", StringComparison.Ordinal) &&
                trimmedLine.Contains("differ"))
            {
                FlushRun();
                isBinary = true;
                currentFile = null;
                inHunk = false;
                continue;
            }

            // --- path (old file)
            if (trimmedLine.StartsWith("--- ", StringComparison.Ordinal))
            {
                FlushRun();
                isBinary = false;
                inHunk = false;
                pendingMinusPath = NormalizePath(trimmedLine[4..]);
                continue;
            }

            // +++ path (new file) — this establishes the current file
            if (trimmedLine.StartsWith("+++ ", StringComparison.Ordinal))
            {
                FlushRun();
                var plusPath = NormalizePath(trimmedLine[4..]);
                // If new file is /dev/null, this is a pure deletion — no additions possible
                if (plusPath == "/dev/null")
                {
                    currentFile = null;
                    inHunk = false;
                    continue;
                }
                var canonicalPath = plusPath == "/dev/null" ? (pendingMinusPath ?? plusPath) : plusPath;
                if (!files.TryGetValue(canonicalPath, out currentFile))
                {
                    currentFile = new DiffFile { FilePath = canonicalPath };
                    files[canonicalPath] = currentFile;
                }
                inHunk = false;
                continue;
            }

            // Hunk header @@ -old +new @@
            var hunkMatch = HunkHeaderRegex.Match(trimmedLine);
            if (hunkMatch.Success)
            {
                FlushRun();
                currentLineNumber = int.Parse(hunkMatch.Groups[1].Value);
                inHunk = true;
                continue;
            }

            if (!inHunk || currentFile == null || isBinary)
                continue;

            // Inside a hunk
            if (trimmedLine.StartsWith("+") && !trimmedLine.StartsWith("+++"))
            {
                var lineContent = trimmedLine[1..];
                if (!IsCommentOnlyLine(lineContent))
                {
                    // Extend current run or start a new one
                    if (runStart == null)
                    {
                        runStart = currentLineNumber;
                        runEnd = currentLineNumber;
                    }
                    else if (currentLineNumber == runEnd + 1)
                    {
                        runEnd = currentLineNumber;
                    }
                    else
                    {
                        // Gap — flush previous run and start new one
                        FlushRun();
                        runStart = currentLineNumber;
                        runEnd = currentLineNumber;
                    }
                }
                else
                {
                    // Comment-only line breaks the run
                    FlushRun();
                }
                currentLineNumber++;
            }
            else if (trimmedLine.StartsWith("-") && !trimmedLine.StartsWith("---"))
            {
                // Removed line — does not advance new-file line counter
            }
            else if (trimmedLine.StartsWith(" "))
            {
                // Context line — breaks any current run
                FlushRun();
                currentLineNumber++;
            }
            // "\ No newline at end of file" — ignore
        }

        // Flush any trailing run
        FlushRun();

        // Build immutable result, excluding files with no added ranges
        var result = new Dictionary<string, IReadOnlyList<LineRange>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (path, file) in files)
        {
            if (file.AddedRanges.Count > 0)
                result[path] = file.AddedRanges.AsReadOnly();
        }

        return result.Count == 0 ? DiffResult.Empty : new DiffResult(result);
    }

    /// <summary>
    /// Reads a diff file from disk and parses it.
    /// </summary>
    public DiffResult ParseFile(string filePath)
    {
        var text = File.ReadAllText(filePath);
        return Parse(text);
    }

    /// <summary>
    /// Returns true if the line content (with the leading diff marker already stripped)
    /// consists entirely of a comment and therefore has no coverable code.
    /// Lines that contain code before a comment marker (e.g. <c>int x = 5; // note</c>)
    /// are NOT considered comment-only and will return false.
    /// </summary>
    private static bool IsCommentOnlyLine(string lineContent)
    {
        var trimmed = lineContent.TrimStart();
        if (trimmed.StartsWith("//")) return true;
        if (trimmed.StartsWith("/*") || trimmed.StartsWith("*")) return true;
        return false;
    }

    /// <summary>
    /// Strips the leading a/ or b/ prefix added by git diff.
    /// </summary>
    private static string NormalizePath(string path)
    {
        if (path.StartsWith("a/", StringComparison.Ordinal) ||
            path.StartsWith("b/", StringComparison.Ordinal))
        {
            return path[2..];
        }
        return path;
    }
}
