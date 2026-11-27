namespace KumikoUI.Export.Excel.Internal;

/// <summary>Dates as Excel serial numbers in the 1900 date system.</summary>
internal static class XlsxDates
{
    private static readonly DateTime First = new(1900, 1, 1);
    private static readonly DateTime FakeLeapDayEnd = new(1900, 3, 1);
    private static readonly DateTime Epoch = new(1899, 12, 30);

    /// <summary>
    /// The serial number of <paramref name="value"/> (days since 1899-12-31, the time as a fraction of a day to the millisecond), or
    /// <see langword="null"/> before 1900-01-01, which the 1900 system cannot hold. Excel counts a 29 February 1900 that
    /// never existed (serial 60, kept for Lotus 1-2-3 compatibility), so from 1 March 1900 on the serials are one higher
    /// than the day count: 1900-01-01 is 1, 1900-02-28 is 59 and 1900-03-01 is 61.
    /// </summary>
    public static double? FromDateTime(DateTime value)
    {
        if (value < First) return null;
        // Whole days and the time apart, the time cut to whole milliseconds (Excel's own resolution): a double cannot
        // tell the last tick of 9999-12-31 from 10000-01-01, which Excel rejects.
        double days = (value.Date - Epoch).Days + TimeFraction(value.TimeOfDay.Ticks);
        return value < FakeLeapDayEnd ? days - 1 : days;
    }

    /// <summary>A time of day as a fraction of a day, cut to whole milliseconds.</summary>
    public static double TimeFraction(long ticks) =>
        (double)(ticks - ticks % TimeSpan.TicksPerMillisecond) / TimeSpan.TicksPerDay;
}
