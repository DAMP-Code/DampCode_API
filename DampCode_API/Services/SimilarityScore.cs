using System.Text;
using System.Text.RegularExpressions;

namespace DampCode_API.Services;

public static partial class SimilarityScore
{
    private static readonly HashSet<string> JavaScriptWords = new(
    [
        "if", "else", "for", "while", "do", "switch", "case", "default", "break", "continue", "return",
        "function", "var", "let", "const", "class", "new", "this", "try", "catch", "finally", "throw",
        "true", "false", "null", "undefined", "typeof", "async", "await", "import", "export", "in", "of"
    ]);

    private static readonly HashSet<string> PythonWords = new(
    [
        "if", "elif", "else", "for", "while", "break", "continue", "return", "def", "class", "import", "from",
        "as", "try", "except", "finally", "raise", "with", "in", "is", "not", "and", "or", "lambda", "pass",
        "True", "False", "None", "yield", "async", "await"
    ]);

    private static readonly HashSet<string> CSharpWords = new(
    [
        "if", "else", "for", "foreach", "while", "do", "switch", "case", "default", "break", "continue", "return",
        "class", "struct", "interface", "new", "this", "var", "void", "int", "long", "double", "float", "decimal",
        "bool", "string", "char", "public", "private", "protected", "static", "try", "catch", "finally", "throw",
        "true", "false", "null", "using", "namespace", "in", "async", "await"
    ]);

    private static readonly HashSet<string> JavaWords = new(
    [
        "if", "else", "for", "while", "do", "switch", "case", "default", "break", "continue", "return",
        "class", "interface", "new", "this", "void", "int", "long", "double", "float", "boolean", "char",
        "public", "private", "protected", "static", "final", "try", "catch", "finally", "throw", "throws",
        "true", "false", "null", "import", "package", "extends", "implements"
    ]);

    private static readonly HashSet<string> SqlWords = new(
    [
        "SELECT", "FROM", "WHERE", "GROUP", "BY", "ORDER", "HAVING", "JOIN", "INNER", "LEFT", "RIGHT", "OUTER",
        "ON", "AS", "AND", "OR", "NOT", "NULL", "IN", "LIKE", "INSERT", "INTO", "VALUES", "UPDATE", "SET",
        "DELETE", "CREATE", "TABLE", "DROP", "ALTER", "DISTINCT", "LIMIT", "UNION", "CASE", "WHEN", "THEN",
        "ELSE", "END", "INT", "VARCHAR", "TEXT", "TRUE", "FALSE"
    ], StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> OtherWords = new(
    [
        "IF", "ELSE", "FOR", "WHILE", "DO", "SWITCH", "CASE", "BREAK", "CONTINUE", "RETURN", "FUNCTION",
        "CLASS", "NEW", "TRUE", "FALSE", "NULL", "INT", "STRING", "BOOL", "VOID"
    ], StringComparer.OrdinalIgnoreCase);

    [GeneratedRegex(@"[A-Za-z_][A-Za-z0-9_]*|\d+(\.\d+)?|\S")]
    private static partial Regex TokenRegex();

    public static int Compute(string reference, string code, string language)
    {
        var a = Tokenize(reference, language);
        var b = Tokenize(code, language);
        if (a.Count == 0 || b.Count == 0) return 0;

        var size = a.Count < 3 || b.Count < 3 ? 1 : 3;
        var gramsA = Grams(a, size);
        var gramsB = Grams(b, size);

        var common = 0;
        foreach (var (gram, countA) in gramsA)
        {
            if (gramsB.TryGetValue(gram, out var countB)) common += Math.Min(countA, countB);
        }

        var total = (gramsA.Values.Sum()) + gramsB.Values.Sum();
        return (int)Math.Round(200.0 * common / total, MidpointRounding.AwayFromZero);
    }

    private static List<string> Tokenize(string source, string language)
    {
        var caseInsensitive = language is "SQL" or "Outra" || !(language is "JavaScript" or "Python" or "C#" or "Java");
        var words = language switch
        {
            "JavaScript" => JavaScriptWords,
            "Python" => PythonWords,
            "C#" => CSharpWords,
            "Java" => JavaWords,
            "SQL" => SqlWords,
            _ => OtherWords
        };

        var tokens = new List<string>();
        foreach (Match match in TokenRegex().Matches(StripComments(source, language)))
        {
            var token = match.Value;
            if (char.IsDigit(token[0])) tokens.Add("NUM");
            else if (char.IsLetter(token[0]) || token[0] == '_')
                tokens.Add(words.Contains(token) ? (caseInsensitive ? token.ToUpperInvariant() : token) : "ID");
            else tokens.Add(token);
        }
        return tokens;
    }

    // Remove comentários sem mexer no que está entre aspas.
    private static string StripComments(string source, string language)
    {
        var line = language switch { "JavaScript" or "C#" or "Java" => "//", "Python" => "#", "SQL" => "--", _ => null };
        var block = language is "JavaScript" or "C#" or "Java" or "SQL";
        var result = new StringBuilder(source.Length);

        for (var i = 0; i < source.Length; i++)
        {
            var c = source[i];
            if (c is '"' or '\'')
            {
                result.Append(c);
                for (i++; i < source.Length && source[i] != '\n'; i++)
                {
                    result.Append(source[i]);
                    if (source[i] == '\\' && i + 1 < source.Length && source[i + 1] != '\n') result.Append(source[++i]);
                    else if (source[i] == c) break;
                }
                if (i < source.Length && source[i] == '\n') result.Append('\n');
            }
            else if (line is not null && string.CompareOrdinal(source, i, line, 0, line.Length) == 0)
            {
                while (i < source.Length && source[i] != '\n') i++;
                result.Append('\n');
            }
            else if (block && string.CompareOrdinal(source, i, "/*", 0, 2) == 0)
            {
                var end = source.IndexOf("*/", i + 2, StringComparison.Ordinal);
                i = end < 0 ? source.Length : end + 1;
                result.Append(' ');
            }
            else result.Append(c);
        }
        return result.ToString();
    }

    private static Dictionary<string, int> Grams(List<string> tokens, int size)
    {
        var grams = new Dictionary<string, int>();
        for (var i = 0; i + size <= tokens.Count; i++)
        {
            var gram = string.Join(' ', tokens.GetRange(i, size));
            grams[gram] = grams.GetValueOrDefault(gram) + 1;
        }
        return grams;
    }
}
