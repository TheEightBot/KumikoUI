namespace KumikoUI.Export.Excel.Internal;

/// <summary>Dates as Excel serial numbers in the 1900 date system.</summary>
internal static class XlsxDates
{
    private static readonly DateTime First = new(1900, 1, 1);
    private static readonly DateTime FakeLeapDayEnd = new(1900, 3, 1);
    private static readonly DateTime Epoch = new(1899, 12, 30);

    /// <summary>
    /// The serial number of <paramref name="value"/> (days since 1899-12-31, the time as a fraction), or
    /// <see langword="null"/> before 1900-01-01, which the 1900 system cannot hold. Excel counts a 29 February 1900 that
    /// never existed (serial 60, kept for Lotus 1-2-3 compatibility), so from 1 March 1900 on the serials are one higher
    /// than the day count: 1900-01-01 is 1, 1900-02-28 is 59 and 1900-03-01 is 61.
    /// </summary>
    public static double? FromDateTime(DateTime value)
    {
        if (value < First) return null;
        double days = (double)(value - Epoch).Ticks / TimeSpan.TicksPerDay;
        return value < FakeLeapDayEnd ? days - 1 : days;
    }
}
