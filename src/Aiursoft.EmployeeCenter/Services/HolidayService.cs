using Aiursoft.EmployeeCenter.Entities;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Memory;

namespace Aiursoft.EmployeeCenter.Services;

/// <summary>
/// Service for fetching Chinese public holiday information from external API
/// Uses NateScarlet/holiday-cn API with caching to minimize API calls, and honors local overrides.
/// </summary>
public class HolidayService
{
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly ILogger<HolidayService> _logger;
    private readonly EmployeeCenterDbContext _context;
    private const string ApiBaseUrl = "https://raw.githubusercontent.com/NateScarlet/holiday-cn/master";

    public HolidayService(
        IHttpClientFactory httpClientFactory,
        IMemoryCache cache,
        ILogger<HolidayService> logger,
        EmployeeCenterDbContext context)
    {
        _httpClient = httpClientFactory.CreateClient();
        _cache = cache;
        _logger = logger;
        _context = context;
    }

    /// <summary>
    /// Check if a specific date is a public holiday in China, respecting local adjustments.
    /// </summary>
    /// <param name="date">Date to check</param>
    /// <returns>True if the date is a public holiday, false otherwise</returns>
    public async Task<bool> IsPublicHolidayAsync(DateTime date)
    {
        var targetDate = date.Date;

        // 1. Check local manual adjustments first (overrides API logic)
        var adjustment = await _context.AdjustedHolidays
            .FirstOrDefaultAsync(a => a.Date.Date == targetDate);

        if (adjustment != null)
        {
            return adjustment.Type == HolidayType.RestDay;
        }

        // 2. Fallback to API logic
        var year = date.Year;
        var cacheKey = $"holidays_cn_{year}";

        if (!_cache.TryGetValue<Dictionary<string, bool>>(cacheKey, out var yearHolidays) || yearHolidays == null)
        {
            yearHolidays = new Dictionary<string, bool>();
            try
            {
                var url = $"{ApiBaseUrl}/{year}.json";
                var response = await _httpClient.GetStringAsync(url);
                var result = JsonSerializer.Deserialize<HolidayApiResponse>(response);

                if (result?.Days != null)
                {
                    foreach (var day in result.Days)
                    {
                        yearHolidays[day.Date] = day.IsOffDay;
                    }
                }

                // Cache for 7 days (holidays don't change retroactively)
                _cache.Set(cacheKey, yearHolidays, TimeSpan.FromDays(7));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch holiday information for year {Year}. Falling back to weekends.", year);
                // On error, cache empty dictionary for a shorter period (1 hour) to enable retry
                _cache.Set(cacheKey, yearHolidays, TimeSpan.FromHours(1));
            }
        }

        var dateKey = date.ToString("yyyy-MM-dd");
        if (yearHolidays.TryGetValue(dateKey, out var isOffDay))
        {
            return isOffDay;
        }

        // 3. Fallback to default weekend logic if not in holiday list
        return date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday;
    }

    /// <summary>
    /// Get all public holidays in a date range
    /// </summary>
    /// <param name="start">Start date (inclusive)</param>
    /// <param name="end">End date (inclusive)</param>
    /// <returns>HashSet of dates that are public holidays</returns>
    public async Task<HashSet<DateTime>> GetPublicHolidaysInRangeAsync(DateTime start, DateTime end)
    {
        var holidays = new HashSet<DateTime>();
        var currentDate = start.Date;

        while (currentDate <= end.Date)
        {
            if (await IsPublicHolidayAsync(currentDate))
            {
                holidays.Add(currentDate);
            }
            currentDate = currentDate.AddDays(1);
        }

        return holidays;
    }

    /// <summary>
    /// Response model for the holiday API
    /// </summary>
    private class HolidayApiResponse
    {
        [JsonPropertyName("year")]
        public int Year { get; set; }

        [JsonPropertyName("days")]
        public List<HolidayDay> Days { get; set; } = new();
    }

    private class HolidayDay
    {
        [JsonPropertyName("date")]
        public string Date { get; set; } = string.Empty;

        [JsonPropertyName("isOffDay")]
        public bool IsOffDay { get; set; }
    }
}
