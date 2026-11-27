using System.Globalization;
using KumikoUI.Export.Excel.Internal;

namespace KumikoUI.Export.Excel.Tests;

public class XlsxNumberFormatTests
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    [Theory]
    [InlineData(null, "General")]
    [InlineData("", "General")]
    [InlineData("G", "General")]
    [InlineData("R", "General")]
    [InlineData("N0", "#,##0")]
    [InlineData("N", "#,##0.00")]
    [InlineData("n4", "#,##0.0000")]
    [InlineData("F", "0.00")]
    [InlineData("F0", "0")]
    [InlineData("D", "0")]
    [InlineData("D5", "00000")]
    [InlineData("E", "0.000000E+000")]
    [InlineData("e2", "0.00e+000")]
    [InlineData("C", "\"¤\"#,##0.00;\"(¤\"#,##0.00\")\"")]
    [InlineData("C0", "\"¤\"#,##0;\"(¤\"#,##0\")\"")]
    [InlineData("P", "#,##0.00\" \"%;\"-\"#,##0.00\" \"%")]
    [InlineData("0.00", "0.00")]
    [InlineData("#,##0", "#,##0")]
    [InlineData("#,##0.00;(#,##0.00)", "#,##0.00;(#,##0.00)")]
    [InlineData("0.0%", "0.0%")]
    [InlineData("'kg' 0.0", "\"kg\" 0.0")]
    [InlineData("\\#0", "\\#0")]
    [InlineData("0.00E+00", "0.00E+00")]
    public void Numbers_MapToExcelCodes(string? format, string expected) =>
        Assert.Equal(expected, XlsxNumberFormats.ForNumber(format, Inv, out _));

    [Theory]
    [InlineData("X")]
    [InlineData("X8")]
    [InlineData("B")]
    [InlineData("G5")]
    [InlineData("0.0‰")]
    [InlineData("0;;0")]
    [InlineData("0E0")]
    [InlineData("#.##abc")]
    [InlineData("0;1;2;3")]
    public void UnmappableNumberFormats_FallBackToText(string format) =>
        Assert.Null(XlsxNumberFormats.ForNumber(format, Inv, out _));

    [Fact]
    public void DecimalFormat_IsForIntegersOnly()
    {
        XlsxNumberFormats.ForNumber("D3", Inv, out bool integerOnly);
        Assert.True(integerOnly);
        XlsxNumberFormats.ForNumber("N3", Inv, out integerOnly);
        Assert.False(integerOnly);
    }

    [Fact]
    public void Currency_UsesTheCulturesSymbolAndPatterns()
    {
        var us = CultureInfo.GetCultureInfo("en-US");
        string? code = XlsxNumberFormats.ForNumber("C", us, out _);
        Assert.StartsWith("\"$\"#,##0.00;", code);
        var jp = CultureInfo.GetCultureInfo("ja-JP");
        Assert.StartsWith("\"" + jp.NumberFormat.CurrencySymbol + "\"#,##0;", XlsxNumberFormats.ForNumber("C", jp, out _));
    }

    [Theory]
    [InlineData("yyyy-MM-dd", "yyyy\"-\"mm\"-\"dd")]
    [InlineData("HH:mm", "hh\":\"mm")]
    [InlineData("H:mm:ss", "h\":\"mm\":\"ss")]
    [InlineData("HH:mm:ss.fff", "hh\":\"mm\":\"ss.000")]
    [InlineData("dd/MM/yyyy", "dd\"/\"mm\"/\"yyyy")]
    [InlineData("d MMM yy", "[$-7F]d\" \"mmm\" \"yy")]
    [InlineData("yyyy'T'HH", "yyyy\"T\"hh")]
    [InlineData("s", "yyyy\"-\"mm\"-\"dd\"T\"hh\":\"mm\":\"ss")]
    [InlineData("%d", "d")]
    [InlineData("mm:ss", "mm\":\"ss")]
    public void Dates_MapToExcelCodes(string format, string expected)
    {
        string? code = XlsxNumberFormats.ForDate(format, "G", Inv);
        // The invariant culture has no locale id Excel knows, so no prefix is written for it.
        Assert.Equal(expected.Replace("[$-7F]", "", StringComparison.Ordinal), code);
    }

    [Theory]
    [InlineData("y-MM")]      // one-digit year
    [InlineData("h:mm")]      // 12-hour clock without AM/PM
    [InlineData("HH:mm tt")]  // 24-hour clock with AM/PM
    [InlineData("yyyy gg")]   // era
    [InlineData("HH:mm zzz")] // offset
    [InlineData("ss.FFF")]    // optional fraction digits
    [InlineData("ss.ffff")]   // more than three fraction digits
    [InlineData("mm")]        // minutes Excel would read as a month
    [InlineData("o")]
    [InlineData("r")]
    [InlineData("u")]
    public void UnmappableDateFormats_FallBackToText(string format) =>
        Assert.Null(XlsxNumberFormats.ForDate(format, "G", Inv));

    [Fact]
    public void DateDefaults_FollowTheCulture()
    {
        var us = CultureInfo.GetCultureInfo("en-US");
        string? general = XlsxNumberFormats.ForDate(null, "G", us);
        Assert.StartsWith("m\"/\"d\"/\"yyyy\"", general);
        Assert.EndsWith("AM/PM", general);
        Assert.Equal("m\"/\"d\"/\"yyyy", XlsxNumberFormats.ForDate(null, "d", us));
        Assert.Equal("[$-409]mmmm\" \"yyyy", XlsxNumberFormats.ForDate("Y", "G", us));
    }

    [Theory]
    [InlineData(null, "[h]:mm:ss")]
    [InlineData("c", "[h]:mm:ss")]
    [InlineData("g", "[h]:mm:ss")]
    [InlineData("hh\\:mm", null)]
    public void Durations_MapToElapsedTime(string? format, string? expected) =>
        Assert.Equal(expected, XlsxNumberFormats.ForDuration(format));
}
