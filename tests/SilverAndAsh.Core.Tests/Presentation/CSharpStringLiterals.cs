using System.Globalization;
using System.Text;

namespace SilverAndAsh.Core.Tests.Presentation;

/// <summary>
/// Finds the text of every string literal in C# source (issue #367): plain "..." with escapes, verbatim @"...",
/// raw """...""" and interpolated $"..." - of an interpolated string only its own text, the holes {...} are code and
/// are searched for strings themselves. Comments and character literals are skipped. Enough for the game's own code,
/// not a full C# parser. <see cref="CodeOf"/> gives the code itself, without strings and comments (issue #672).
/// </summary>
internal static class CSharpStringLiterals
{
    public static IReadOnlyList<string> In(string source)
    {
        var found = new List<string>();
        var position = 0;
        ReadCode(source, ref position, found, source.ToCharArray(), stopAtBrace: false);
        return found;
    }

    /// <summary>
    /// The source with every comment, string literal (its text, quotes and prefix; the holes of an interpolated string
    /// stay, they are code) and character literal blanked out by spaces. Line breaks stay, so a place in the result is the
    /// same place in the source - same line, same column.
    /// </summary>
    public static string CodeOf(string source)
    {
        var code = source.ToCharArray();
        var position = 0;
        ReadCode(source, ref position, [], code, stopAtBrace: false);
        return new string(code);
    }

    private static void ReadCode(string s, ref int i, List<string> found, char[] code, bool stopAtBrace)
    {
        var depth = 0;
        while (i < s.Length)
        {
            var c = s[i];
            var start = i;
            if (c == '/' && Next(s, i) == '/')
            {
                i = s.IndexOf('\n', i) is var end and >= 0 ? end + 1 : s.Length;
                Blank(code, start, i);
            }
            else if (c == '/' && Next(s, i) == '*')
            {
                i = s.IndexOf("*/", i + 2, StringComparison.Ordinal) is var end and >= 0 ? end + 2 : s.Length;
                Blank(code, start, i);
            }
            else if (c == '\'')
            {
                SkipCharLiteral(s, ref i);
                Blank(code, start, i);
            }
            else if (StartsString(s, i, out var interpolated, out var verbatim, out var dollars, out var length))
            {
                i += length;
                found.Add(ReadString(s, ref i, start, interpolated, verbatim, dollars, found, code));
            }
            else if (c == '{')
            {
                depth++;
                i++;
            }
            else if (c == '}')
            {
                if (stopAtBrace && depth == 0)
                {
                    i++;
                    return;
                }

                depth--;
                i++;
            }
            else
            {
                i++;
            }
        }
    }

    /// <summary>Whether a string starts at <paramref name="i"/>: its prefix ($, @, $@, @$, $$) and opening quotes.</summary>
    private static bool StartsString(string s, int i, out bool interpolated, out bool verbatim, out int dollars, out int length)
    {
        interpolated = verbatim = false;
        dollars = 0;
        var j = i;
        while (j < s.Length && (s[j] == '$' || s[j] == '@'))
        {
            if (s[j] == '$')
            {
                dollars++;
            }
            else
            {
                verbatim = true;
            }

            j++;
        }

        interpolated = dollars > 0;
        length = j - i;
        if (j >= s.Length || s[j] != '"')
        {
            return false;
        }

        // An identifier right before '@' or '$' means it is not a prefix (e.g. a variable named x$ does not exist in C#).
        return length == 0 || i == 0 || !char.IsLetterOrDigit(s[i - 1]);
    }

    // Reads the string whose quotes start at i (its prefix starts at start) and blanks it out in code, all but its holes.
    private static string ReadString(string s, ref int i, int start, bool interpolated, bool verbatim, int dollars, List<string> found, char[] code)
    {
        var text = new StringBuilder();
        var segment = start;
        var quotes = CountRun(s, i, '"');
        if (quotes >= 3)
        {
            // Raw string: ends at the same number of quotes; holes need as many braces as there are dollars.
            i += quotes;
            while (i < s.Length)
            {
                if (s[i] == '"' && CountRun(s, i, '"') >= quotes)
                {
                    i += quotes;
                    Blank(code, segment, i);
                    return text.ToString();
                }

                if (interpolated && s[i] == '{' && CountRun(s, i, '{') >= dollars)
                {
                    var braces = CountRun(s, i, '{');
                    text.Append('{', braces - dollars);
                    i += braces;
                    Blank(code, segment, i);
                    ReadCode(s, ref i, found, code, stopAtBrace: true);
                    segment = i;
                    i += dollars - 1;
                    continue;
                }

                text.Append(s[i]);
                i++;
            }

            Blank(code, segment, i);
            return text.ToString();
        }

        i++; // the opening quote
        while (i < s.Length)
        {
            var c = s[i];
            if (c == '"')
            {
                if (verbatim && Next(s, i) == '"')
                {
                    text.Append('"');
                    i += 2;
                    continue;
                }

                i++;
                Blank(code, segment, i);
                return text.ToString();
            }

            if (interpolated && (c == '{' || c == '}'))
            {
                if (Next(s, i) == c)
                {
                    text.Append(c);
                    i += 2;
                    continue;
                }

                i++;
                if (c == '{')
                {
                    Blank(code, segment, i);
                    ReadCode(s, ref i, found, code, stopAtBrace: true);
                    segment = i;
                }

                continue;
            }

            if (!verbatim && c == '\\')
            {
                text.Append(Escape(s, ref i));
                continue;
            }

            text.Append(c);
            i++;
        }

        Blank(code, segment, i);
        return text.ToString();
    }

    // Spaces over code[from..to), line breaks kept.
    private static void Blank(char[] code, int from, int to)
    {
        for (var k = from; k < Math.Min(to, code.Length); k++)
        {
            if (code[k] is not ('\n' or '\r'))
            {
                code[k] = ' ';
            }
        }
    }

    private static string Escape(string s, ref int i)
    {
        var code = Next(s, i);
        i += 2;
        switch (code)
        {
            case 'n': return "\n";
            case 't': return "\t";
            case 'r': return "\r";
            case '0': return "\0";
            case 'u':
                var value = int.Parse(s.AsSpan(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                i += 4;
                return char.ConvertFromUtf32(value);
            default: return code.ToString();
        }
    }

    private static void SkipCharLiteral(string s, ref int i)
    {
        i++;
        while (i < s.Length && s[i] != '\'')
        {
            i += s[i] == '\\' ? 2 : 1;
        }

        i++;
    }

    private static int CountRun(string s, int i, char c)
    {
        var count = 0;
        while (i + count < s.Length && s[i + count] == c)
        {
            count++;
        }

        return count;
    }

    private static char Next(string s, int i) => i + 1 < s.Length ? s[i + 1] : '\0';
}
