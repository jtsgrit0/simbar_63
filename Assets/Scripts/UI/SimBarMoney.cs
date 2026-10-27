using System.Globalization;

/// <summary>Displays saved game credits as Korean won without changing economy values.</summary>
public static class SimBarMoney
{
    private const double WonPerCredit = 1000d;

    public static string Format(float credits, bool signed = false)
    {
        double won = credits * WonPerCredit;
        return won.ToString(signed ? "+₩#,##0;-₩#,##0;₩0" : "₩#,##0;-₩#,##0;₩0", CultureInfo.InvariantCulture);
    }
}
