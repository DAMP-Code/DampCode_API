using DampCode_API.Services;

namespace DampCode_API.Tests;

public sealed class SimilarityScoreTests
{
    [Fact]
    public void Identical_Returns100() =>
        Assert.Equal(100, SimilarityScore.Compute("let soma = a + b;", "let soma = a + b;", "JavaScript"));

    [Fact]
    public void RenamedAndReformatted_Returns100() =>
        Assert.Equal(100, SimilarityScore.Compute(
            "function soma(a, b) {\n  return a + b; // soma\n}",
            "function total(x,y){return x+y;}",
            "JavaScript"));

    [Fact]
    public void PythonHashComment_Ignored() =>
        Assert.Equal(100, SimilarityScore.Compute(
            "def f(x):\n    return x * 2",
            "# dobra\ndef g(y):\n    return y * 2  # fim",
            "Python"));

    [Fact]
    public void SqlCaseInsensitive() =>
        Assert.Equal(100, SimilarityScore.Compute("SELECT nome FROM clientes", "select nome from clientes", "SQL"));

    [Fact]
    public void StringWithSlashesIsNotAComment() =>
        Assert.Equal(100, SimilarityScore.Compute(
            "let u = \"http://x\"; f(u);",
            "let u = \"http://x\"; f(u); // fim",
            "JavaScript"));

    [Fact]
    public void Unrelated_IsLow()
    {
        var reference = "int soma = 0;\nfor (int i = 0; i < valores.Length; i++) { soma += valores[i]; }";
        var code = "public class Ola { public static void main(String[] args) { System.out.println(\"oi\"); } }";

        Assert.True(SimilarityScore.Compute(reference, code, "C#") < 40);
    }

    [Fact]
    public void EmptyCode_Returns0()
    {
        Assert.Equal(0, SimilarityScore.Compute("x = 1", "", "Python"));
        Assert.Equal(0, SimilarityScore.Compute("", "", "Python"));
    }

    [Fact]
    public void ShortCode_UsesUnigrams() =>
        Assert.Equal(100, SimilarityScore.Compute("x", "y", "Python"));
}
