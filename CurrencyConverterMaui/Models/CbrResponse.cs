namespace CurrencyConverterMaui.Models;

public class CbrResponse
{
    public DateTime Date { get; set; }

    public Dictionary<string, CbrCurrency> Valute { get; set; } = new();
}

public class CbrCurrency
{
    public string CharCode { get; set; } = string.Empty;

    public int Nominal { get; set; }

    public string Name { get; set; } = string.Empty;

    public decimal Value { get; set; }
}