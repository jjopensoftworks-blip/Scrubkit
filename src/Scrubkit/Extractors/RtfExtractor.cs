using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Scrubkit;

/// <summary>
/// Clean text from Rich Text Format (.rtf): strips control words and groups, expands
/// <c>\'hh</c> and <c>\uN</c> escapes, and drops non-text destinations (font/colour
/// tables, pictures, document metadata). Best-effort and hand-rolled — no dependency.
/// </summary>
public sealed class RtfExtractor : IFileExtractor
{
    // Destination groups whose contents are control data, not body text.
    private static readonly HashSet<string> IgnoredDestinations = new(StringComparer.Ordinal)
    {
        "fonttbl", "colortbl", "stylesheet", "info", "pict", "header", "footer",
        "headerl", "headerr", "footerl", "footerr", "footnote", "annotation", "field",
        "object", "themedata", "colorschememapping", "datastore", "generator",
        "listtable", "listoverridetable", "revtbl", "rsidtbl", "xmlnstbl",
    };

    private static readonly Regex Whitespace = new(@"[^\S\n]+");

    public bool CanHandle(string extension) => extension is ".rtf";

    public ExtractedContent Extract(string path)
    {
        var rtf = File.ReadAllText(path);
        var sb = new StringBuilder(rtf.Length);

        int depth = 0;          // current group nesting
        int ignoreDepth = -1;   // group depth we started ignoring at (-1 = emitting)
        int unicodeSkip = 1;    // chars of ASCII fallback to drop after each \uN (\uc)
        int i = 0, n = rtf.Length;

        while (i < n)
        {
            char c = rtf[i];
            if (c == '{') { depth++; i++; }
            else if (c == '}')
            {
                if (ignoreDepth == depth) ignoreDepth = -1;   // leaving the ignored group
                depth--; i++;
            }
            else if (c == '\\')
            {
                i++;
                if (i >= n) break;
                char next = rtf[i];
                if (next is '\\' or '{' or '}')          // escaped literal
                {
                    Emit(sb, next, ignoreDepth);
                    i++;
                }
                else if (next == '\'')                   // \'hh hex byte
                {
                    i++;
                    if (i + 1 < n && Uri.IsHexDigit(rtf[i]) && Uri.IsHexDigit(rtf[i + 1]))
                    {
                        var code = int.Parse(rtf.Substring(i, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                        Emit(sb, (char)code, ignoreDepth);
                        i += 2;
                    }
                }
                else if (char.IsLetter(next))            // control word (+ optional param)
                {
                    int wordStart = i;
                    while (i < n && char.IsLetter(rtf[i])) i++;
                    var word = rtf.Substring(wordStart, i - wordStart);

                    int paramStart = i;
                    if (i < n && rtf[i] == '-') i++;
                    while (i < n && char.IsDigit(rtf[i])) i++;
                    var hasParam = i > paramStart;
                    var param = hasParam ? int.Parse(rtf.Substring(paramStart, i - paramStart), CultureInfo.InvariantCulture) : 0;

                    if (i < n && rtf[i] == ' ') i++;     // one delimiting space is consumed

                    i += HandleWord(word, hasParam, param, sb, ref ignoreDepth, ref unicodeSkip, depth, rtf, i);
                }
                else                                     // control symbol
                {
                    if (next == '*') { if (ignoreDepth < 0) ignoreDepth = depth; }  // ignorable destination
                    else if (next == '~') Emit(sb, ' ', ignoreDepth);         // non-breaking space
                    else if (next is '_' or '-') Emit(sb, '-', ignoreDepth);       // (non-breaking) hyphen
                    i++;
                }
            }
            else if (c is '\r' or '\n') i++;             // source line breaks are not text
            else { Emit(sb, c, ignoreDepth); i++; }
        }

        return new ExtractedContent(new Dictionary<string, string>(), Normalize(sb.ToString()));
    }

    // Returns how many extra chars to advance (for \uN fallback skipping).
    private static int HandleWord(
        string word, bool hasParam, int param, StringBuilder sb,
        ref int ignoreDepth, ref int unicodeSkip, int depth, string rtf, int i)
    {
        if (IgnoredDestinations.Contains(word))
        {
            if (ignoreDepth < 0) ignoreDepth = depth;
            return 0;
        }

        switch (word)
        {
            case "par" or "line" or "sect" or "row": Emit(sb, '\n', ignoreDepth); return 0;
            case "tab": Emit(sb, '\t', ignoreDepth); return 0;
            case "cell" or "nestcell": Emit(sb, ' ', ignoreDepth); return 0;
            case "emdash": Emit(sb, '—', ignoreDepth); return 0;
            case "endash": Emit(sb, '–', ignoreDepth); return 0;
            case "lquote": Emit(sb, '‘', ignoreDepth); return 0;
            case "rquote": Emit(sb, '’', ignoreDepth); return 0;
            case "ldblquote": Emit(sb, '“', ignoreDepth); return 0;
            case "rdblquote": Emit(sb, '”', ignoreDepth); return 0;
            case "bullet": Emit(sb, '•', ignoreDepth); return 0;
            case "uc": unicodeSkip = Math.Max(0, param); return 0;
            case "u" when hasParam:
                Emit(sb, (char)(param < 0 ? param + 0x10000 : param), ignoreDepth);
                return SkipFallback(rtf, i, unicodeSkip);
            default: return 0;   // any other control word: no text
        }
    }

    // A \uN is followed by `count` chars of ASCII fallback; skip them (a \'hh counts as one).
    private static int SkipFallback(string rtf, int i, int count)
    {
        int start = i, n = rtf.Length;
        while (count > 0 && i < n)
        {
            if (rtf[i] == '\\' && i + 1 < n && rtf[i + 1] == '\'') i += 4;   // \'hh
            else if (rtf[i] == '{' || rtf[i] == '}') break;                  // don't cross groups
            else i++;
            count--;
        }
        return i - start;
    }

    private static void Emit(StringBuilder sb, char c, int ignoreDepth)
    {
        if (ignoreDepth < 0) sb.Append(c);
    }

    // Collapse runs of non-newline whitespace; keep single newlines as paragraph markers.
    private static string Normalize(string s) =>
        Whitespace.Replace(s, " ").Replace(" \n", "\n").Replace("\n ", "\n").Trim();
}
