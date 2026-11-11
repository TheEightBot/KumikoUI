using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Tests.Rendering;

/// <summary>
/// One-line ellipsis and wrapping (#23), measured monospace: every UTF-16 code unit is 8 px wide, so a cut by code
/// units would land inside surrogate pairs and combining sequences unless the layout keeps text elements whole.
/// </summary>
public class KumikoTextLayoutTests
{
    private const float Unit = 8f;
    private static readonly KumikoTextMeasurer Mono = s => s.Length * Unit;

    private static readonly string Ellipsis = KumikoTextLayout.Ellipsis;
    private static readonly string Grin = char.ConvertFromUtf32(0x1F600);                 // two code units
    private static readonly string EAcute = "e" + (char)0x0301;                            // e + combining acute
    private static readonly string Family = char.ConvertFromUtf32(0x1F468) + (char)0x200D  // man ZWJ woman ZWJ girl
        + char.ConvertFromUtf32(0x1F469) + (char)0x200D + char.ConvertFromUtf32(0x1F467);

    private static string[] Lines(KumikoTextLines lines) =>
        Enumerable.Range(0, lines.Count).Select(lines.GetLine).ToArray();

    private static string OneLine(string text, float width) =>
        Lines(KumikoTextLayout.LayOut(text, width, KumikoTextWrap.None, 1, Mono)).SingleOrDefault() ?? "";

    private static void AssertNoBrokenSurrogates(string s)
    {
        for (int i = 0; i < s.Length; i++)
        {
            if (char.IsHighSurrogate(s[i]))
            {
                Assert.True(i + 1 < s.Length && char.IsLowSurrogate(s[i + 1]), $"lone high surrogate at {i} in '{s}'");
                i++;
            }
            else
            {
                Assert.False(char.IsLowSurrogate(s[i]), $"lone low surrogate at {i} in '{s}'");
            }
        }
    }

    // ── One line ──────────────────────────────────────────────────

    [Fact]
    public void LayOut_TextThatFits_ReturnsTheSameStringInstance()
    {
        const string text = "Hello";

        var lines = KumikoTextLayout.LayOut(text, 100, KumikoTextWrap.None, 1, Mono);

        Assert.Equal(1, lines.Count);
        Assert.Same(text, lines.GetLine(0));
        Assert.Equal(40, lines.GetLineWidth(0));
    }

    [Theory]
    [InlineData(0, "")]
    [InlineData(1, "a")]
    [InlineData(3, "abc")]
    [InlineData(4, "abc")]      // room for half the emoji: it is dropped, not split
    [InlineData(5, "abcG")]
    [InlineData(6, "abcGd")]
    public void LayOut_EmojiAtTheEllipsisBoundary_IsNeverSplit(int unitsBeforeEllipsis, string expectedKept)
    {
        // "abc" + an emoji (two code units, written G in the data) + "def".
        string text = "abc" + Grin + "def";

        string shown = OneLine(text, (unitsBeforeEllipsis + 1) * Unit);

        AssertNoBrokenSurrogates(shown);
        Assert.Equal(expectedKept.Replace("G", Grin) + Ellipsis, shown);
    }

    [Fact]
    public void LayOut_CombiningSequenceAtTheBoundary_StaysWithItsBase()
    {
        // "caf" + e-acute (two code units) + "s": room for four code units before the ellipsis would split it.
        string text = "caf" + EAcute + "s";

        string shown = OneLine(text, 5 * Unit);

        Assert.Equal("caf" + Ellipsis, shown);
    }

    [Fact]
    public void LayOut_ZwjSequence_IsKeptWholeOrDropped()
    {
        string text = "a" + Family + "b";
        for (int units = 1; units <= text.Length + 1; units++)
        {
            string shown = OneLine(text, units * Unit);
            AssertNoBrokenSurrogates(shown);
            Assert.True(shown == text || shown == "a" + Ellipsis || shown == Ellipsis || shown == "a" + Family + Ellipsis,
                $"width {units}: '{shown}'");
        }
    }

    [Fact]
    public void LayOut_NarrowRect_StillGetsAnEllipsis()
    {
        // Wider than one character plus the ellipsis, narrower than the old 20 px threshold.
        Assert.Equal("H" + Ellipsis, OneLine("Hello", 16));
    }

    [Fact]
    public void LayOut_NarrowerThanTheEllipsis_ShowsTheEllipsisAlone()
    {
        Assert.Equal(Ellipsis, OneLine("Hello", 5));
    }

    [Fact]
    public void LayOut_ZeroWidth_ShowsNothing()
    {
        Assert.Equal(0, KumikoTextLayout.LayOut("Hello", 0, KumikoTextWrap.None, 1, Mono).Count);
    }

    [Fact]
    public void LayOut_TrailingSpaceBeforeTheEllipsis_IsTrimmed()
    {
        Assert.Equal("ab" + Ellipsis, OneLine("ab  cdef", 4 * Unit));
    }

    [Theory]
    [InlineData("a\nb", "a b")]
    [InlineData("a\r\nb", "a b")]
    [InlineData("a\rb", "a b")]
    [InlineData("a\tb", "a b")]
    [InlineData("a\u0001b", "ab")]
    public void LayOut_OneLine_ShowsLineBreaksAndTabsAsSpaces(string text, string expected)
    {
        Assert.Equal(expected, OneLine(text, 100));
    }

    [Fact]
    public void LayOut_OneLine_ShowsUnicodeLineSeparatorsAsSpaces()
    {
        Assert.Equal("a b c", OneLine("a" + (char)0x2028 + "b" + (char)0x2029 + "c", 100));
    }

    [Fact]
    public void ContainsControlCharacters_DetectsLineBreaksAndTabs()
    {
        Assert.False(KumikoTextLayout.ContainsControlCharacters("plain " + Grin + EAcute));
        Assert.True(KumikoTextLayout.ContainsControlCharacters("a\nb"));
        Assert.True(KumikoTextLayout.ContainsControlCharacters("a\tb"));
        Assert.True(KumikoTextLayout.ContainsControlCharacters("a" + (char)0x2028));
    }

    // ── Wrapping ──────────────────────────────────────────────────

    [Fact]
    public void LayOut_WordWrap_BreaksAtSpacesAndDropsThem()
    {
        var lines = KumikoTextLayout.LayOut("The quick brown fox", 10 * Unit, KumikoTextWrap.Word, int.MaxValue, Mono);

        Assert.Equal(new[] { "The quick", "brown fox" }, Lines(lines));
        Assert.Equal(9 * Unit, lines.GetLineWidth(0));
        Assert.Equal(9 * Unit, lines.Width);
    }

    [Fact]
    public void LayOut_WordWrap_BreaksAfterAHyphen()
    {
        var lines = KumikoTextLayout.LayOut("well-known", 6 * Unit, KumikoTextWrap.Word, int.MaxValue, Mono);

        Assert.Equal(new[] { "well-", "known" }, Lines(lines));
    }

    [Fact]
    public void LayOut_WordWrap_BreaksAWordTooLongForTheLineBetweenCharacters()
    {
        var lines = KumikoTextLayout.LayOut("ab abcdefghij", 4 * Unit, KumikoTextWrap.Word, int.MaxValue, Mono);

        Assert.Equal(new[] { "ab", "abcd", "efgh", "ij" }, Lines(lines));
    }

    [Fact]
    public void LayOut_CharacterWrap_BreaksBetweenAnyCharacters()
    {
        var lines = KumikoTextLayout.LayOut("The quick", 4 * Unit, KumikoTextWrap.Character, int.MaxValue, Mono);

        Assert.Equal(new[] { "The", "quic", "k" }, Lines(lines));
    }

    [Fact]
    public void LayOut_CharacterWrap_NeverSplitsAnEmoji()
    {
        string text = "ab" + Grin + Grin + "cd";

        var lines = Lines(KumikoTextLayout.LayOut(text, 3 * Unit, KumikoTextWrap.Character, int.MaxValue, Mono));

        Assert.Equal(new[] { "ab", Grin, Grin + "c", "d" }, lines);
    }

    [Fact]
    public void LayOut_Wrap_StartsANewLineAtEachLineBreak()
    {
        var lines = KumikoTextLayout.LayOut("one\r\ntwo\n\nfour\n", 100, KumikoTextWrap.Word, int.MaxValue, Mono);

        Assert.Equal(new[] { "one", "two", "", "four" }, Lines(lines));
    }

    [Fact]
    public void LayOut_Wrap_EndsTheLastLineThatFitsWithAnEllipsis()
    {
        var lines = KumikoTextLayout.LayOut("The quick brown fox jumps", 10 * Unit, KumikoTextWrap.Word, 2, Mono);

        Assert.Equal(new[] { "The quick", "brown fox" + Ellipsis }, Lines(lines));
    }

    [Fact]
    public void LayOut_Wrap_AddsAnEllipsisWhenAWholeLineFollowsTheLastSlot()
    {
        var lines = KumikoTextLayout.LayOut("first\nsecond", 100, KumikoTextWrap.Word, 1, Mono);

        Assert.Equal(new[] { "first" + Ellipsis }, Lines(lines));
    }

    [Fact]
    public void LayOut_Wrap_LastLineEllipsisNeverSplitsAnEmoji()
    {
        string text = "abcd ab" + Grin + Grin + "cd";

        var lines = Lines(KumikoTextLayout.LayOut(text, 5 * Unit, KumikoTextWrap.Word, 2, Mono));

        Assert.Equal("abcd", lines[0]);
        AssertNoBrokenSurrogates(lines[1]);
        Assert.Equal("ab" + Grin + Ellipsis, lines[1]);
    }

    [Fact]
    public void LayOut_EmptyText_HasNoLines()
    {
        Assert.Same(KumikoTextLines.Empty, KumikoTextLayout.LayOut("", 100, KumikoTextWrap.Word, 3, Mono));
        Assert.Same(KumikoTextLines.Empty, KumikoTextLayout.LayOut(null, 100, KumikoTextWrap.None, 1, Mono));
    }

    [Fact]
    public void LayOut_NullMeasurer_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => KumikoTextLayout.LayOut("a", 10, KumikoTextWrap.None, 1, null!));
    }

    // ── Helpers ───────────────────────────────────────────────────

    [Theory]
    [InlineData(0, 0)]
    [InlineData(3, 3)]
    [InlineData(4, 4)]
    [InlineData(5, 4)]
    [InlineData(6, 6)]
    [InlineData(7, 6)]
    [InlineData(100, 8)]
    public void FitTextElements_ReturnsATextElementBoundary(int units, int expected)
    {
        // "abcd" + e-acute + emoji: element boundaries at 0, 1, 2, 3, 4, 6 and 8.
        string text = "abcd" + EAcute + Grin;

        Assert.Equal(expected, KumikoTextLayout.FitTextElements(text, units * Unit, Mono));
    }

    [Theory]
    [InlineData(36f, 2)]
    [InlineData(30f, 2)]
    [InlineData(29f, 1)]
    [InlineData(5f, 1)]
    public void GetMaxLines_CountsTheLinesThatFit(float height, int expected)
    {
        // 14 px of text, 16 px from one line to the next.
        var metrics = new KumikoFontMetrics(-11, 3, 2);

        Assert.Equal(expected, KumikoTextLayout.GetMaxLines(height, metrics));
    }

    [Fact]
    public void GetHeight_StacksLinesWithoutTheLastLeading()
    {
        var metrics = new KumikoFontMetrics(-11, 3, 2);
        var lines = KumikoTextLayout.LayOut("a\nb\nc", 100, KumikoTextWrap.Word, int.MaxValue, Mono);

        Assert.Equal(14 + 2 * 16, lines.GetHeight(metrics));
        Assert.Equal(0, KumikoTextLines.Empty.GetHeight(metrics));
    }
}
