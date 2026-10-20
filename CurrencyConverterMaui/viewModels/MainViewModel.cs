using System.Collections.ObjectModel;
using System.Globalization;
using CurrencyConverterMaui.Models;
using CurrencyConverterMaui.Services;
using System.Windows.Input;

namespace CurrencyConverterMaui.ViewModels;

public class MainViewModel : BaseViewModel
{
    private readonly CurrencyService _currencyService;

    // чтобы изменение одного поля не вызывало изменение другого 
    private bool _isUpdating;

    // какое поле редактировали последним
    private string _lastEditedField = "from";

    private readonly string _savedFromCurrencyCode;
    private readonly string _savedToCurrencyCode;

    // команда смены валют местами
    public ICommand SwapCurrenciesCommand { get; }

    // список валют
    // // // //

    private ObservableCollection<CurrencyRate> _currencies = new();

    public ObservableCollection<CurrencyRate> Currencies
    {
        get => _currencies;

        set => SetProperty(
            ref _currencies,
            value);
    }


    // первая выбранная валюта 

    private CurrencyRate? _fromCurrency;

    public CurrencyRate? FromCurrency
    {
        get => _fromCurrency;

        set
        {
            if (!SetProperty(
                    ref _fromCurrency,
                    value))
            {
                return;
            }

            if (_isUpdating)
                return;

            SaveState();
            Recalculate();
        }
    }


    // вторая валюта

    private CurrencyRate? _toCurrency;

    public CurrencyRate? ToCurrency
    {
        get => _toCurrency;

        set
        {
            if (!SetProperty(
                    ref _toCurrency,
                    value))
            {
                return;
            }

            if (_isUpdating)
                return;

            SaveState();
            Recalculate();
        }
    }


    // первое поле ввода

    private string _firstAmount = "1";

    public string FirstAmount
    {
        get => _firstAmount;

        set
        {
            value ??= "";

            if (!SetProperty(
                    ref _firstAmount,
                    value))
            {
                return;
            }

            if (_isUpdating)
                return;

            _lastEditedField = "from";

            SaveState();
            ConvertFromTo();
        }
    }


    // второе поле ввода

    private string _secondAmount = "";

    public string SecondAmount
    {
        get => _secondAmount;

        set
        {
            value ??= "";

            if (!SetProperty(
                    ref _secondAmount,
                    value))
            {
                return;
            }

            if (_isUpdating)
                return;

            _lastEditedField = "to";

            ConvertToFrom();

            SaveState();
        }
    }


    // выбранная дата

    private DateTime _selectedDate;

    public DateTime SelectedDate
    {
        get => _selectedDate;

        set
        {
            if (!SetProperty(
                    ref _selectedDate,
                    value))
            {
                return;
            }

            SaveState();

            _ = LoadRatesAsync();
        }
    }


    // загрузка 

    private bool _isBusy;

    public bool IsBusy
    {
        get => _isBusy;

        set => SetProperty(
            ref _isBusy,
            value);
    }


    // текст даты курса

    private string _rateDateText = "Загрузка курса...";

    public string RateDateText
    {
        get => _rateDateText;

        set => SetProperty(
            ref _rateDateText,
            value);
    }


    // ошибка

    private string _errorMessage = "";

    public string ErrorMessage
    {
        get => _errorMessage;

        set => SetProperty(
            ref _errorMessage,
            value);
    }


    // используются DatePicker

    public DateTime Today => DateTime.Today;

    public DateTime MinimumDate => new(1999, 1, 1);


    // конструктор

    public MainViewModel(
        CurrencyService currencyService)
    {
        _currencyService = currencyService;

        SwapCurrenciesCommand = new Command(SwapCurrencies);

        //специально меняем внутренние поля, а не свойства
        //чтобы во время конструктора не запускались лишние пересчеты

        _selectedDate = LoadSavedDate();

        _firstAmount = Preferences.Default.Get(
            "first_amount",
            "1");

        _secondAmount = Preferences.Default.Get(
            "second_amount",
            "");

        _savedFromCurrencyCode = Preferences.Default.Get(
            "from_currency",
            "RUB");

        _savedToCurrencyCode = Preferences.Default.Get(
            "to_currency",
            "USD");

        _lastEditedField = Preferences.Default.Get(
            "last_edited",
            "from");

        _ = LoadRatesAsync();
    }


    // загрузка курса

    private async Task LoadRatesAsync()
    {
        IsBusy = true;
        ErrorMessage = "";
        RateDateText = "Загрузка курса";

        try
        {
            string fromCode =
                FromCurrency?.Code
                ?? _savedFromCurrencyCode;

            string toCode =
                ToCurrency?.Code
                ?? _savedToCurrencyCode;


            var result =
                await _currencyService.GetRatesAsync(
                    SelectedDate);


            List<CurrencyRate> loadedCurrencies = new();


            // Добавляем рубль вручную

            loadedCurrencies.Add(
                new CurrencyRate
                {
                    Code = "RUB",
                    Name = "Российский рубль",
                    RubPerUnit = 1m
                });


            // добавляем остальные валюты 

            loadedCurrencies.AddRange(
                result.Data.Valute.Values
                    .Where(currency =>
                        currency.Nominal > 0)
                    .Select(currency =>
                        new CurrencyRate
                        {
                            Code = currency.CharCode,
                            Name = currency.Name,

                            RubPerUnit =
                                currency.Value /
                                currency.Nominal
                        })
                    .OrderBy(currency =>
                        currency.Name));


            // временно запрещаем авто пересчет, потому что программно считаем валюты

            _isUpdating = true;


            Currencies =
                new ObservableCollection<CurrencyRate>(
                    loadedCurrencies);


            FromCurrency =
                loadedCurrencies.FirstOrDefault(
                    currency =>
                        currency.Code == fromCode)
                ??
                loadedCurrencies.First(
                    currency =>
                        currency.Code == "RUB");


            ToCurrency =
                loadedCurrencies.FirstOrDefault(
                    currency =>
                        currency.Code == toCode)
                ??
                loadedCurrencies.FirstOrDefault(
                    currency =>
                        currency.Code == "USD")
                ??
                loadedCurrencies.First();


            _isUpdating = false;


            DateTime actualDate =
                result.ActualDate;


            if (actualDate == SelectedDate.Date)
            {
                RateDateText =
                    $"Курс на {FormatDate(actualDate)}";
            }
            else
            {
                RateDateText =
                    $"Курс на {FormatDate(actualDate)} " +
                    $"(за {SelectedDate:dd.MM.yyyy} данных нет)";
            }


            Recalculate();
            SaveState();
        }
        catch (Exception ex)
        {
            _isUpdating = false;

            ErrorMessage =
                $"Ошибка загрузки курса: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    // смена валют местами

    private void SwapCurrencies()
    {
        if (FromCurrency == null ||
            ToCurrency == null)
        {
            return;
        }

        // запрещаем автоматический пересчет,
        // пока программно меняем значения

        _isUpdating = true;


        // сохраняем текущие валюты

        CurrencyRate oldFromCurrency =
            FromCurrency;

        CurrencyRate oldToCurrency =
            ToCurrency;


        // сохраняем текущие значения

        string oldFirstAmount =
            FirstAmount;

        string oldSecondAmount =
            SecondAmount;


        // меняем валюты местами

        FromCurrency =
            oldToCurrency;

        ToCurrency =
            oldFromCurrency;


        // меняем значения местами

        FirstAmount =
            oldSecondAmount;

        SecondAmount =
            oldFirstAmount;


        _isUpdating = false;


        // считаем, что теперь основным является верхнее поле

        _lastEditedField = "from";


        // сохраняем новое состояние

        SaveState();
    }


    // определяем направление пересчёта

    private void Recalculate()
    {
        if (_lastEditedField == "to")
        {
            ConvertToFrom();
        }
        else
        {
            ConvertFromTo();
        }
    }



    // пересчёт из первой валюты во вторую

    // пересчёт из первой валюты во вторую

    private void ConvertFromTo()
    {
        if (FromCurrency == null ||
            ToCurrency == null)
        {
            return;
        }


        if (!TryParseAmount(
                FirstAmount,
                out decimal amount))
        {
            _isUpdating = true;

            SecondAmount = "";

            _isUpdating = false;

            ErrorMessage =
                "Введите корректное число допустимого размера.";

            return;
        }


        try
        {
            // исходную в рубли

            decimal rubles =
                checked(
                    amount *
                    FromCurrency.RubPerUnit);


            // из рублей в целевую

            decimal result =
                checked(
                    rubles /
                    ToCurrency.RubPerUnit);


            _isUpdating = true;

            SecondAmount =
                FormatAmount(result);

            _isUpdating = false;


            ErrorMessage = "";

            SaveState();
        }
        catch (OverflowException)
        {
            _isUpdating = true;

            SecondAmount = "";

            _isUpdating = false;

            ErrorMessage =
                "Число слишком большое для расчёта.";
        }
    }


    // второе поле -> первое 

    private void ConvertToFrom()
    {
        if (FromCurrency == null ||
            ToCurrency == null)
        {
            return;
        }


        if (!TryParseAmount(
                SecondAmount,
                out decimal amount))
        {
            _isUpdating = true;

            FirstAmount = "";

            _isUpdating = false;

            ErrorMessage =
                "Введите корректное число допустимого размера.";

            return;
        }


        try
        {
            // переводим вторую валюту в рубли

            decimal rubles =
                checked(
                    amount *
                    ToCurrency.RubPerUnit);


            // из рублей переводим в первую валюту

            decimal result =
                checked(
                    rubles /
                    FromCurrency.RubPerUnit);


            _isUpdating = true;

            FirstAmount =
                FormatAmount(result);

            _isUpdating = false;


            ErrorMessage = "";

            SaveState();
        }
        catch (OverflowException)
        {
            _isUpdating = true;

            FirstAmount = "";

            _isUpdating = false;

            ErrorMessage =
                "Число слишком большое для расчёта.";
        }
    }


    // преобразование строки в число

    private static bool TryParseAmount(
        string text,
        out decimal value)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            value = 0;
            return false;
        }


        text = text
            .Trim()
            .Replace(" ", "");


        if (decimal.TryParse(
                text,
                NumberStyles.Number,
                CultureInfo.CurrentCulture,
                out value))
        {
            return true;
        }


        text =
            text.Replace(',', '.');


        return decimal.TryParse(
            text,
            NumberStyles.Number,
            CultureInfo.InvariantCulture,
            out value);
    }


    // формат числа

    private static string FormatAmount(
        decimal value)
    {
        return value.ToString(
            "0.######",
            CultureInfo.CurrentCulture);
    }


    // формат даты 

    private static string FormatDate(
        DateTime date)
    {
        CultureInfo russian =
            CultureInfo.GetCultureInfo(
                "ru-RU");


        return date.ToString(
            "d MMMM yyyy",
            russian);
    }


    private void SaveState()
    {
        Preferences.Default.Set(
            "selected_date",
            SelectedDate.ToString(
                "yyyy-MM-dd"));


        Preferences.Default.Set(
            "first_amount",
            FirstAmount);


        Preferences.Default.Set(
            "second_amount",
            SecondAmount);


        Preferences.Default.Set(
            "from_currency",
            FromCurrency?.Code ?? "RUB");


        Preferences.Default.Set(
            "to_currency",
            ToCurrency?.Code ?? "USD");


        Preferences.Default.Set(
            "last_edited",
            _lastEditedField);
    }


    // Восстановление даты

    private static DateTime LoadSavedDate()
    {
        string savedDate =
            Preferences.Default.Get(
                "selected_date",
                DateTime.Today.ToString(
                    "yyyy-MM-dd"));


        if (DateTime.TryParseExact(
                savedDate,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out DateTime date))
        {
            return date > DateTime.Today
                ? DateTime.Today
                : date;
        }


        return DateTime.Today;
    }
}