using CurrencyConverterMaui.Models;
using System.Globalization;
using System.Net;
using System.Text.Json;

namespace CurrencyConverterMaui.Services;

public class CurrencyService
{
    private const string BaseUrl =
        "https://www.cbr-xml-daily.ru";

    private readonly HttpClient _httpClient;

    private readonly JsonSerializerOptions _jsonOptions =
        new()
        {
            PropertyNameCaseInsensitive = true
        };


    public CurrencyService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }


    // Получение курса на выбранную дату.
    // Если курса нет, ищем предыдущий доступный день.
    public async Task<(CbrResponse Data, DateTime ActualDate)>
    GetRatesAsync(DateTime requestedDate)
    {
        requestedDate = requestedDate.Date;

        // Идем назад максимум на 14 дней,
        // пока не найдем существующий курс
        for (int i = 0; i < 14; i++)
        {
            DateTime candidateDate =
                requestedDate.AddDays(-i);


            // Сначала смотрим, есть ли курс в кэше
            CbrResponse? cached =
                await LoadFromCacheAsync(candidateDate);

            if (cached != null)
            {
                return (
                    cached,
                    candidateDate
                );
            }


            string url;

            // Для сегодняшней даты берем актуальный файл
            if (candidateDate == DateTime.Today)
            {
                url =
                    $"{BaseUrl}/daily_json.js";
            }
            else
            {
                // Для предыдущих дат используем архив
                string datePath =
                    candidateDate.ToString(
                        "yyyy'/'MM'/'dd",
                        CultureInfo.InvariantCulture);

                url =
                    $"{BaseUrl}/archive/{datePath}/daily_json.js";
            }


            try
            {
                HttpResponseMessage response =
                    await _httpClient.GetAsync(url);


                // За эту дату файла нет.
                // Например, выходной.
                if (response.StatusCode ==
                    HttpStatusCode.NotFound)
                {
                    continue;
                }


                response.EnsureSuccessStatusCode();


                string json =
                    await response.Content
                        .ReadAsStringAsync();


                CbrResponse? data =
                    JsonSerializer.Deserialize<CbrResponse>(
                        json,
                        _jsonOptions);


                if (data == null)
                {
                    continue;
                }


                // Сохраняем курс локально
                await SaveToCacheAsync(
                    candidateDate,
                    json);


                // Важно:
                // возвращаем именно дату,
                // по которой нашли файл
                return (
                    data,
                    candidateDate
                );
            }
            catch (HttpRequestException)
            {
                // Если интернета нет,
                // ищем ближайшие сохраненные данные

                CbrResponse? cachedData =
                    await FindNearestCachedAsync(
                        candidateDate);


                if (cachedData != null)
                {
                    return (
                        cachedData,
                        candidateDate
                    );
                }


                throw;
            }
        }


        throw new InvalidOperationException(
            "Не удалось найти курс валют за выбранную дату.");
    }


    // Получаем путь к файлу с кэшем.
    private static string GetCachePath(DateTime date)
    {
        string fileName =
            $"rates_{date:yyyy-MM-dd}.json";


        return Path.Combine(
            FileSystem.AppDataDirectory,
            fileName);
    }


    // Сохраняем полученный JSON.
    private static async Task SaveToCacheAsync(
        DateTime date,
        string json)
    {
        string path =
            GetCachePath(date);


        await File.WriteAllTextAsync(
            path,
            json);
    }


    // Загружаем курс из сохранённого файла.
    private CbrResponse? Deserialize(string json)
    {
        return JsonSerializer.Deserialize<CbrResponse>(
            json,
            _jsonOptions);
    }


    private async Task<CbrResponse?>
        LoadFromCacheAsync(DateTime date)
    {
        string path =
            GetCachePath(date);


        if (!File.Exists(path))
            return null;


        try
        {
            string json =
                await File.ReadAllTextAsync(path);


            return Deserialize(json);
        }
        catch
        {
            // Если файл повреждён,
            // просто считаем, что кэша нет.
            return null;
        }
    }


    // Ищем ближайший сохранённый курс.
    private async Task<CbrResponse?>
        FindNearestCachedAsync(DateTime startDate)
    {
        for (int i = 0; i < 14; i++)
        {
            DateTime date =
                startDate.AddDays(-i);


            CbrResponse? cached =
                await LoadFromCacheAsync(date);


            if (cached != null)
            {
                return cached;
            }
        }


        return null;
    }
}