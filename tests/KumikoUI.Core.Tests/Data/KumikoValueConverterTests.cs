using System.Globalization;
using KumikoUI.Core.Data;

namespace KumikoUI.Core.Tests.Data;

/// <summary>
/// Converting edited values to property types (#32): every supported type, in en-US and de-DE, with
/// blank text, out-of-range numbers, fractions into whole-number types and unparsable text reported as
/// messages instead of exceptions or silent rounding.
/// </summary>
public class KumikoValueConverterTests
{
    private static readonly CultureInfo En = CultureInfo.GetCultureInfo("en-US");
    private static readonly CultureInfo De = CultureInfo.GetCultureInfo("de-DE");

    [Flags]
    private enum Access { None = 0, Read = 1, Write = 2 }

    private enum Mode { Off, On, Auto }

    private static object? Convert(object? value, Type type, CultureInfo culture)
    {
        Assert.True(KumikoValueConverter.TryConvert(value, type, culture, out var result, out var error), error);
        return result;
    }

    private static string Fails(object? value, Type type, CultureInfo culture)
    {
        Assert.False(KumikoValueConverter.TryConvert(value, type, culture, out _, out var error));
        Assert.False(string.IsNullOrEmpty(error));
        return error!;
    }

    // ── Numbers ───────────────────────────────────────────────────

    [Theory]
    [InlineData("en-US", "1,234", 1234)]
    [InlineData("de-DE", "1.234", 1234)]
    [InlineData("en-US", "-42", -42)]
    [InlineData("de-DE", " 7 ", 7)]
    [InlineData("en-US", "3.0", 3)]
    public void Int_ParsesInTheCulture(string culture, string text, int expected) =>
        Assert.Equal(expected, Convert(text, typeof(int), CultureInfo.GetCultureInfo(culture)));

    [Theory]
    [InlineData(typeof(byte), "300")]
    [InlineData(typeof(sbyte), "-129")]
    [InlineData(typeof(short), "40000")]
    [InlineData(typeof(ushort), "-1")]
    [InlineData(typeof(int), "9999999999")]
    [InlineData(typeof(uint), "-5")]
    [InlineData(typeof(long), "99999999999999999999")]
    [InlineData(typeof(ulong), "-1")]
    public void Integers_OutOfRange_AreReported(Type type, string text) =>
        Assert.Equal("The number is out of range.", Fails(text, type, En));

    [Fact]
    public void Integers_InRange_ForEveryWidth()
    {
        Assert.Equal((byte)255, Convert("255", typeof(byte), En));
        Assert.Equal((sbyte)-128, Convert("-128", typeof(sbyte), En));
        Assert.Equal((short)-32768, Convert("-32768", typeof(short), En));
        Assert.Equal((ushort)65535, Convert("65535", typeof(ushort), En));
        Assert.Equal(4_000_000_000u, Convert("4000000000", typeof(uint), En));
        Assert.Equal(9_000_000_000L, Convert("9000000000", typeof(long), En));
        Assert.Equal(18_000_000_000_000_000_000ul, Convert("18000000000000000000", typeof(ulong), En));
    }

    [Theory]
    [InlineData("en-US", "2.6")]
    [InlineData("de-DE", "2,6")]
    public void Int_Fractions_AreRejectedNotRounded(string culture, string text) =>
        Assert.Equal("Enter a whole number.", Fails(text, typeof(int), CultureInfo.GetCultureInfo(culture)));

    [Fact]
    public void Decimal_KeepsEveryDigit_InEitherCulture()
    {
        Assert.Equal(1234567.890123456789m, Convert("1234567.890123456789", typeof(decimal), En));
        Assert.Equal(1234567.890123456789m, Convert("1.234.567,890123456789", typeof(decimal), De));
        Assert.Equal(0.1m, Convert("1E-1", typeof(decimal), En));
    }

    [Theory]
    [InlineData("en-US", "2.5", 2.5)]
    [InlineData("de-DE", "2,5", 2.5)]
    [InlineData("en-US", "1,234.5", 1234.5)]
    [InlineData("en-US", "1e3", 1000)]
    public void Double_ParsesInTheCulture(string culture, string text, double expected) =>
        Assert.Equal(expected, Convert(text, typeof(double), CultureInfo.GetCultureInfo(culture)));

    [Fact]
    public void Float_ParsesAndRejectsOverflow()
    {
        Assert.Equal(0.25f, Convert("0,25", typeof(float), De));
        Assert.Equal("The number is out of range.", Fails("1e300", typeof(float), En));
    }

    [Theory]
    [InlineData(typeof(int))]
    [InlineData(typeof(double))]
    [InlineData(typeof(decimal))]
    public void Numbers_TextThatIsNotANumber_IsReported(Type type) =>
        Assert.Equal("Enter a number.", Fails("abc", type, En));

    [Fact]
    public void Numbers_InTheWrongCulture_AreNotSilentlyMisread()
    {
        // "2.5" in de-DE is the group separator: 25, not 2.5, so it must not land in an int as 2 or 3
        Assert.Equal(25, Convert("2.5", typeof(int), De));
        Assert.Equal("Enter a whole number.", Fails("2,5", typeof(int), De));
    }

    // ── Blank text and null ───────────────────────────────────────

    [Theory]
    [InlineData(typeof(int?))]
    [InlineData(typeof(decimal?))]
    [InlineData(typeof(DateTime?))]
    [InlineData(typeof(Guid?))]
    [InlineData(typeof(Mode?))]
    public void BlankText_ClearsANullableProperty(Type type)
    {
        Assert.Null(Convert("", type, En));
        Assert.Null(Convert("   ", type, De));
        Assert.Null(Convert(null, type, En));
    }

    [Theory]
    [InlineData(typeof(int))]
    [InlineData(typeof(double))]
    [InlineData(typeof(DateTime))]
    [InlineData(typeof(bool))]
    [InlineData(typeof(Mode))]
    public void BlankText_IsRequired_ForANonNullableValueType(Type type)
    {
        Assert.Equal("A value is required.", Fails("", type, En));
        Assert.Equal("A value is required.", Fails(null, type, En));
    }

    [Fact]
    public void Strings_KeepTheTextAsTyped_AndFormatOtherValuesInTheCulture()
    {
        Assert.Equal("", Convert("", typeof(string), En));
        Assert.Equal("  spaced  ", Convert("  spaced  ", typeof(string), En));
        Assert.Null(Convert(null, typeof(string), En));
        Assert.Equal("3,5", Convert(3.5, typeof(string), De));
    }

    // ── Other types ───────────────────────────────────────────────

    [Fact]
    public void BoolCharAndGuid()
    {
        Assert.Equal(true, Convert("TRUE", typeof(bool), En));
        Assert.Equal("Enter true or false.", Fails("yes", typeof(bool), En));
        Assert.Equal('x', Convert("x", typeof(char), En));
        Assert.Equal("Enter a single character.", Fails("xy", typeof(char), En));
        var id = Guid.NewGuid();
        Assert.Equal(id, Convert(id.ToString(), typeof(Guid), En));
        Assert.Equal("Enter a valid identifier.", Fails("not-a-guid", typeof(Guid), En));
    }

    [Fact]
    public void Enums_ByNameIgnoringCase_OrByADefinedNumber()
    {
        Assert.Equal(Mode.Auto, Convert("auto", typeof(Mode), En));
        Assert.Equal(Mode.On, Convert("1", typeof(Mode), En));
        Assert.Equal(Mode.On, Convert(1, typeof(Mode), En));
        Assert.Equal(Mode.On, Convert((long)1, typeof(Mode?), En));
        Assert.Equal("Enter one of: Off, On, Auto.", Fails("Sideways", typeof(Mode), En));
        Assert.Equal("Enter one of: Off, On, Auto.", Fails("7", typeof(Mode), En));
        Assert.Equal("Enter one of: Off, On, Auto.", Fails(7, typeof(Mode), En));
    }

    [Fact]
    public void FlagsEnums_AcceptCombinations()
    {
        Assert.Equal(Access.Read | Access.Write, Convert("Read, Write", typeof(Access), En));
        Assert.Equal(Access.Read | Access.Write, Convert(3, typeof(Access), En));
    }

    // ── Dates and times ───────────────────────────────────────────

    [Theory]
    [InlineData("en-US", "1/31/2024")]
    [InlineData("de-DE", "31.01.2024")]
    [InlineData("en-US", "2024-01-31")]
    public void DateTime_And_DateOnly_ParseInTheCulture(string culture, string text)
    {
        var c = CultureInfo.GetCultureInfo(culture);
        Assert.Equal(new DateTime(2024, 1, 31), Convert(text, typeof(DateTime), c));
        Assert.Equal(new DateOnly(2024, 1, 31), Convert(text, typeof(DateOnly), c));
        Assert.Equal(new DateOnly(2024, 1, 31), Convert(text, typeof(DateOnly?), c));
    }

    [Fact]
    public void DateOnly_FromTextWithATime_KeepsTheDate() =>
        Assert.Equal(new DateOnly(2024, 1, 31), Convert("1/31/2024 3:45 PM", typeof(DateOnly), En));

    [Fact]
    public void TimeOnly_TimeSpan_And_DateTimeOffset()
    {
        Assert.Equal(new TimeOnly(14, 30), Convert("2:30 PM", typeof(TimeOnly), En));
        Assert.Equal(new TimeOnly(14, 30), Convert("14:30", typeof(TimeOnly), De));
        Assert.Equal(new TimeSpan(1, 2, 3, 4), Convert("1.02:03:04", typeof(TimeSpan), En));
        Assert.Equal(TimeSpan.FromMinutes(90), Convert("1:30", typeof(TimeSpan), De));
        Assert.Equal(new DateTimeOffset(2024, 1, 31, 8, 0, 0, TimeSpan.FromHours(2)),
            Convert("2024-01-31T08:00:00+02:00", typeof(DateTimeOffset), En));
    }

    [Fact]
    public void DateAndTimeText_ThatDoesNotParse_IsReported()
    {
        Assert.Equal("Enter a valid date.", Fails("31.01.2024", typeof(DateTime), En));
        Assert.Equal("Enter a valid date.", Fails("someday", typeof(DateOnly), En));
        Assert.Equal("Enter a valid date.", Fails("someday", typeof(DateTimeOffset), En));
        Assert.Equal("Enter a valid time.", Fails("25:99", typeof(TimeOnly), En));
        Assert.Equal("Enter a valid duration.", Fails("a while", typeof(TimeSpan), En));
    }

    [Fact]
    public void DatesAndTimes_ConvertBetweenTheirRepresentations()
    {
        var dt = new DateTime(2024, 1, 31, 10, 15, 0);
        Assert.Equal(new DateOnly(2024, 1, 31), Convert(dt, typeof(DateOnly), En));
        Assert.Equal(new TimeOnly(10, 15), Convert(dt, typeof(TimeOnly), En));
        Assert.Equal(new DateTime(2024, 1, 31), Convert(new DateOnly(2024, 1, 31), typeof(DateTime), En));
        Assert.Equal(new DateTimeOffset(dt), Convert(dt, typeof(DateTimeOffset), En));
        Assert.Equal(dt, Convert(new DateTimeOffset(dt), typeof(DateTime), En));
        Assert.Equal(new DateOnly(2024, 1, 31), Convert(new DateTimeOffset(dt), typeof(DateOnly), En));
        Assert.Equal(new TimeOnly(1, 30), Convert(TimeSpan.FromMinutes(90), typeof(TimeOnly), En));
        Assert.Equal(TimeSpan.FromMinutes(90), Convert(new TimeOnly(1, 30), typeof(TimeSpan), En));
        Assert.Equal("Enter a valid time.", Fails(TimeSpan.FromHours(30), typeof(TimeOnly), En));
    }

    // ── Typed values between numeric types ────────────────────────

    [Fact]
    public void Numbers_ConvertBetweenTypes_WithoutRounding()
    {
        Assert.Equal(2, Convert(2.0, typeof(int), En));
        Assert.Equal(2L, Convert(2m, typeof(long), En));
        Assert.Equal(2.5, Convert(2.5m, typeof(double), En));
        Assert.Equal(2.5m, Convert(2.5, typeof(decimal), En));
        Assert.Equal("Enter a whole number.", Fails(2.5, typeof(int), En));
        Assert.Equal("The number is out of range.", Fails(300, typeof(byte), En));
        Assert.Equal("The number is out of range.", Fails(double.MaxValue, typeof(decimal), En));
        Assert.Equal("The number is out of range.", Fails(1e300, typeof(float), En));
    }

    [Fact]
    public void Values_ThatCannotConvert_AreReported()
    {
        Assert.Equal("Enter a valid value.", Fails(new object(), typeof(int), En));
        Assert.Equal("Enter a valid value.", Fails(DateTime.Now, typeof(Guid), En));
        Assert.Equal("Enter a valid value.", Fails("text", typeof(Version), En));
    }
}
