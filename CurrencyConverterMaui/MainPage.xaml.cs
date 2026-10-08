using CurrencyConverterMaui.Services;
using CurrencyConverterMaui.ViewModels;

namespace CurrencyConverterMaui;

public partial class MainPage : ContentPage
{
    public MainPage()
    {
        InitializeComponent();

        HttpClient httpClient = new HttpClient();

        CurrencyService currencyService =
            new CurrencyService(httpClient);

        BindingContext =
            new MainViewModel(currencyService);
    }
}
