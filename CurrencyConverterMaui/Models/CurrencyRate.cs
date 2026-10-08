using System;
using System.Collections.Generic;
using System.Text;

namespace CurrencyConverterMaui.Models;

public class CurrencyRate
{
    public string Code { get; set; } = String.Empty;
    public string Name { get; set; } = String.Empty;

    public decimal RubPerUnit { get; set; }

    // текст, который видим в Picker
    public string DisplayName => $"{Name} ({Code})";
}
