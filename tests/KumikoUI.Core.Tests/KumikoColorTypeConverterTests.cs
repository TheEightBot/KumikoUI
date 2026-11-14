using System.ComponentModel;
using System.Globalization;
using KumikoUI.Core.Rendering;

namespace KumikoUI.Core.Tests;

/// <summary>
/// The string to color converter used by XAML and design-time tools: named colors, hex forms, comma
/// separated channels, culture independence and what it does with text it cannot read.
/// </summary>
public class KumikoColorTypeConverterTests
{
    private static readonly KumikoColorTypeConverter Converter = new();

    private static KumikoColor Convert(string text) =>
        Assert.IsType<KumikoColor>(Converter.ConvertFrom(null, CultureInfo.InvariantCulture, text));

    [Fact]
    public void TheColorTypeIsConvertedByThisConverter()
    {
        var converter = TypeDescriptor.GetConverter(typeof(KumikoColor));

        Assert.IsType<KumikoColorTypeConverter>(converter);
        Assert.Equal(new KumikoColor(1, 2, 3), converter.ConvertFromString("1,2,3"));
    }

    [Fact]
    public void CanConvertFrom_StringOnly()
    {
        Assert.True(Converter.CanConvertFrom(typeof(string)));
        Assert.False(Converter.CanConvertFrom(typeof(int)));
        Assert.False(Converter.CanConvertFrom(typeof(object)));
    }

    [Theory]
    [InlineData("White", 255, 255, 255, 255)]
    [InlineData("Black", 0, 0, 0, 255)]
    [InlineData("Red", 255, 0, 0, 255)]
    [InlineData("Blue", 0, 0, 255, 255)]
    [InlineData("Gray", 128, 128, 128, 255)]
    [InlineData("Transparent", 0, 0, 0, 0)]
    public void NamedColors(string name, byte r, byte g, byte b, byte a)
    {
        Assert.Equal(new KumikoColor(r, g, b, a), Convert(name));
    }

    [Theory]
    [InlineData("Orange", 255, 165, 0)]
    [InlineData("CornflowerBlue", 100, 149, 237)]
    [InlineData("rebeccapurple", 102, 51, 153)]
    [InlineData("SlateGrey", 112, 128, 144)]
    [InlineData("slategray", 112, 128, 144)]
    [InlineData("chartreuse", 127, 255, 0)]
    [InlineData("Lime", 0, 255, 0)]
    [InlineData("YellowGreen", 154, 205, 50)]
    public void StandardColorNames_AreTheCssKeywords(string name, byte r, byte g, byte b)
    {
        Assert.Equal(new KumikoColor(r, g, b), Convert(name));
    }

    [Fact]
    public void EveryBuiltInColor_IsReachableByItsName()
    {
        Assert.Equal(KumikoColor.LightGray, Convert("LightGray"));
        Assert.Equal(KumikoColor.WhiteSmoke, Convert("WhiteSmoke"));
        Assert.Equal(KumikoColor.DodgerBlue, Convert("DodgerBlue"));
        Assert.Equal(KumikoColor.LightBlue, Convert("LightBlue"));
        Assert.Equal(KumikoColor.DarkBlue, Convert("DarkBlue"));
        Assert.Equal(KumikoColor.CornflowerBlue, Convert("CornflowerBlue"));
    }

    [Theory]
    [InlineData("#FFF", 255, 255, 255, 255)]
    [InlineData("#fa0", 255, 170, 0, 255)]
    [InlineData("#000", 0, 0, 0, 255)]
    [InlineData("#8FA0", 255, 170, 0, 136)]
    [InlineData("#0000", 0, 0, 0, 0)]
    public void ShortHex_DoublesEachDigit(string text, byte r, byte g, byte b, byte a)
    {
        Assert.Equal(new KumikoColor(r, g, b, a), Convert(text));
    }

    [Fact]
    public void NamedColors_Green_MatchesTheBuiltInGreen()
    {
        Assert.Equal(KumikoColor.Green, Convert("green"));
    }

    [Theory]
    [InlineData("red")]
    [InlineData("RED")]
    [InlineData("  Red  ")]
    public void NamedColors_IgnoreCaseAndSurroundingSpace(string text)
    {
        Assert.Equal(KumikoColor.Red, Convert(text));
    }

    [Theory]
    [InlineData("#DC3545", 220, 53, 69, 255)]
    [InlineData("#dc3545", 220, 53, 69, 255)]
    [InlineData("#000000", 0, 0, 0, 255)]
    [InlineData("#FFFFFF", 255, 255, 255, 255)]
    public void SixDigitHex_IsOpaqueRgb(string text, byte r, byte g, byte b, byte a)
    {
        Assert.Equal(new KumikoColor(r, g, b, a), Convert(text));
    }

    [Theory]
    [InlineData("#80DC3545", 220, 53, 69, 128)]
    [InlineData("#00000000", 0, 0, 0, 0)]
    [InlineData("#FFFF0000", 255, 0, 0, 255)]
    public void EightDigitHex_IsArgb(string text, byte r, byte g, byte b, byte a)
    {
        Assert.Equal(new KumikoColor(r, g, b, a), Convert(text));
    }

    [Theory]
    [InlineData("220,53,69", 220, 53, 69, 255)]
    [InlineData("220, 53, 69", 220, 53, 69, 255)]
    [InlineData("220,53,69,128", 220, 53, 69, 128)]
    [InlineData(" 0 , 0 , 0 , 0 ", 0, 0, 0, 0)]
    public void CommaSeparatedChannels(string text, byte r, byte g, byte b, byte a)
    {
        Assert.Equal(new KumikoColor(r, g, b, a), Convert(text));
    }

    [Fact]
    public void CommaSeparatedChannels_FourthPartThatIsNotANumberFallsBackToOpaque()
    {
        Assert.Equal(new KumikoColor(1, 2, 3, 255), Convert("1,2,3,x"));
    }

    [Fact]
    public void Conversion_DoesNotDependOnTheCulture()
    {
        var german = CultureInfo.GetCultureInfo("de-DE");

        var color = Assert.IsType<KumikoColor>(Converter.ConvertFrom(null, german, "#80DC3545"));

        Assert.Equal(new KumikoColor(220, 53, 69, 128), color);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-color")]
    [InlineData("#12")]
    [InlineData("#123456789")]
    [InlineData("1,2")]
    [InlineData("1,2,3,4,5")]
    [InlineData("256,0,0")]
    [InlineData("-1,0,0")]
    [InlineData("a,b,c")]
    [InlineData("#12345")]
    [InlineData("#1234567")]
    [InlineData("#")]
    public void TextItCannotReadThrowsAFormatExceptionNamingTheText(string text)
    {
        var ex = Assert.Throws<FormatException>(() => Converter.ConvertFrom(null, CultureInfo.InvariantCulture, text));

        Assert.Contains("Cannot convert", ex.Message);
        Assert.Contains(text.Trim(), ex.Message);
    }

    [Theory]
    [InlineData("#GGGGGG")]
    [InlineData("#12XY56")]
    [InlineData("#GGG")]
    public void HexWithNonHexDigits_ThrowsAFormatExceptionNamingTheText(string text)
    {
        var ex = Assert.Throws<FormatException>(() => Converter.ConvertFrom(null, CultureInfo.InvariantCulture, text));

        Assert.Contains(text, ex.Message);
    }

    [Fact]
    public void ANonStringValue_IsNotSupported()
    {
        Assert.Throws<NotSupportedException>(() => Converter.ConvertFrom(null, CultureInfo.InvariantCulture, 42));
    }
}
