using DomainModels;
using DomainModels.Configuration.Interfaces;
using DomainModels.Storage.Interfaces;
using DomainModels.Storage.Models;
using Fobelity.Home.MiniSplit.Domain.Analytics;
using Fobelity.Home.MiniSplit.Domain.Analytics.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using System.Collections.Generic;
using System.Net.Http;

namespace Fobelity.Home.MiniSplit.Service.Controllers
{
  [ApiController]
  [Authorize]
  [Route("api/[controller]")]
  public class AnalyticsController : Controller
  {
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<AnalyticsController> _logger;
    private readonly IConfigurationService _configurationService;
    private readonly ITableStorageService _tableStorageService;

    public AnalyticsController(
      IHttpClientFactory httpClientFactory,
      ILogger<AnalyticsController> logger,
      IConfigurationService configurationService,
      ITableStorageService tableStorageService)
    {
      _httpClientFactory = httpClientFactory;
      _logger = logger;
      _configurationService = configurationService;
      _tableStorageService = tableStorageService;
    }

    [HttpGet("last-month-runtime")]
    [ProducesResponseType(typeof(MiniSplitRuntimeAnalyticsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
    [Authorize]
    public async Task<IActionResult> LastMonthRuntime()
    {
      try
      {
        var centralTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time");

        // Calculate last month in CDT
        var nowCdt = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, centralTimeZone);
        var startOfLastMonthCdt = new DateTime(nowCdt.Year, nowCdt.Month, 1).AddMonths(-1);
        var startOfThisMonthCdt = new DateTime(nowCdt.Year, nowCdt.Month, 1);
        var endOfLastMonthCdt = startOfThisMonthCdt.AddSeconds(-1);

        // Convert to UTC for filtering
        var startUtc = TimeZoneInfo.ConvertTimeToUtc(startOfLastMonthCdt, centralTimeZone);
        var endUtc = TimeZoneInfo.ConvertTimeToUtc(endOfLastMonthCdt, centralTimeZone);

        var logEntries = await _tableStorageService.QueryEntitiesAsync<MiniSplitLogActivity>(
          "minisplitlogs", filter: $"PartitionKey eq 'minisplit'"
        );

        var toggles = logEntries
          .Where(e =>
            e.TimestampUtc >= startUtc &&
            e.TimestampUtc <= endUtc &&
            (e.Notes?.Contains("Turned on", StringComparison.OrdinalIgnoreCase) == true ||
             e.Notes?.Contains("Turned off", StringComparison.OrdinalIgnoreCase) == true))
          .OrderBy(e => e.TimestampUtc)
          .ToList();

        double totalMinutes = 0;
        int sessionCount = 0;
        MiniSplitLogActivity? lastOn = null;
        List<double> sessionDurations = new();

        foreach (var entry in toggles)
        {
          if (entry.Notes.Contains("Turned on", StringComparison.OrdinalIgnoreCase))
          {
            lastOn = entry;
          }
          else if (entry.Notes.Contains("Turned off", StringComparison.OrdinalIgnoreCase) && lastOn != null)
          {
            var duration = (entry.TimestampUtc - lastOn.TimestampUtc).TotalMinutes;
            totalMinutes += duration;
            sessionDurations.Add(duration);
            sessionCount++;
            lastOn = null;
          }
        }

        var response = new MiniSplitRuntimeAnalyticsResponse
        {
          Success = true,
          Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
          TrackingId = Guid.NewGuid().ToString(),
          TotalRuntimeHours = Math.Round(totalMinutes / 60, 2),
          AverageSessionMinutes = sessionDurations.Count > 0
            ? Math.Round(sessionDurations.Average(), 2)
            : null,
          SessionCount = sessionCount
        };

        return Ok(response);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Failed to calculate last month runtime.");
        return StatusCode(500, new { message = "Internal server error calculating runtime." });
      }
    }


    [HttpGet("last-month-energy-cost")]
    [ProducesResponseType(typeof(EnergyCostResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
    [Authorize]
    public async Task<IActionResult> LastMonthEnergyCost()
    {
      try
      {
        var centralTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time");

        // Determine the last month in Central Time
        var nowCdt = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, centralTimeZone);
        var startOfLastMonthCdt = new DateTime(nowCdt.Year, nowCdt.Month, 1).AddMonths(-1);
        var startOfThisMonthCdt = new DateTime(nowCdt.Year, nowCdt.Month, 1);
        var endOfLastMonthCdt = startOfThisMonthCdt.AddSeconds(-1);

        // Convert to UTC for filtering
        var startUtc = TimeZoneInfo.ConvertTimeToUtc(startOfLastMonthCdt, centralTimeZone);
        var endUtc = TimeZoneInfo.ConvertTimeToUtc(endOfLastMonthCdt, centralTimeZone);

        var logEntries = await _tableStorageService.QueryEntitiesAsync<MiniSplitLogActivity>(
          "minisplitlogs", filter: $"PartitionKey eq 'minisplit'"
        );

        var coldConfig = await _tableStorageService.GetEntityAsync<MiniSplitConfigEntity>("minisplitconfig", "config", "cold");
        var heatConfig = await _tableStorageService.GetEntityAsync<MiniSplitConfigEntity>("minisplitconfig", "config", "heat");

        var toggles = logEntries
          .Where(e =>
            e.TimestampUtc >= startUtc &&
            e.TimestampUtc <= endUtc &&
            (e.Notes?.Contains("Turned on", StringComparison.OrdinalIgnoreCase) == true ||
             e.Notes?.Contains("Turned off", StringComparison.OrdinalIgnoreCase) == true))
          .OrderBy(e => e.TimestampUtc)
          .ToList();

        double totalCost = 0;
        int sessionCount = 0;
        MiniSplitLogActivity? lastOn = null;

        foreach (var entry in toggles)
        {
          if (entry.Notes.Contains("Turned on", StringComparison.OrdinalIgnoreCase))
          {
            lastOn = entry;
          }
          else if (entry.Notes.Contains("Turned off", StringComparison.OrdinalIgnoreCase) && lastOn != null)
          {
            var durationHours = (entry.TimestampUtc - lastOn.TimestampUtc).TotalHours;
            var mode = lastOn.Mode?.ToLower();

            if (mode == "cold" && coldConfig?.costPerKWh != null && coldConfig?.kWhPerHour != null)
            {
              totalCost += durationHours * coldConfig.kWhPerHour * coldConfig.costPerKWh;
            }
            else if (mode == "heat" && heatConfig?.costPerKWh != null && heatConfig?.kWhPerHour != null)
            {
              totalCost += durationHours * heatConfig.kWhPerHour * heatConfig.costPerKWh;
            }

            sessionCount++;
            lastOn = null;
          }
        }

        var response = new EnergyCostResponse
        {
          Success = true,
          Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
          TrackingId = Guid.NewGuid().ToString(),
          TotalCostUSD = Math.Round(totalCost, 2),
          SessionCount = sessionCount
        };

        return Ok(response);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Failed to calculate energy cost.");
        return StatusCode(500, new { message = "Internal server error calculating energy cost." });
      }
    }


    [HttpGet("last-month-summary")]
    [ProducesResponseType(typeof(MonthlySummaryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
    [Authorize]
    public async Task<IActionResult> LastMonthSummary()
    {
      try
      {
        var centralTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time");

        // Get current date in Central Time
        var nowCdt = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, centralTimeZone);

        // Calculate last month's range in CDT
        var startOfLastMonthCdt = new DateTime(nowCdt.Year, nowCdt.Month, 1).AddMonths(-1);
        var startOfThisMonthCdt = new DateTime(nowCdt.Year, nowCdt.Month, 1);
        var endOfLastMonthCdt = startOfThisMonthCdt.AddSeconds(-1); // last moment of previous month

        // Convert to UTC for filtering
        var startUtc = TimeZoneInfo.ConvertTimeToUtc(startOfLastMonthCdt, centralTimeZone);
        var endUtc = TimeZoneInfo.ConvertTimeToUtc(endOfLastMonthCdt, centralTimeZone);

        var logEntries = await _tableStorageService.QueryEntitiesAsync<MiniSplitLogActivity>(
          "minisplitlogs", filter: $"PartitionKey eq 'minisplit'"
        );

        var coolConfig = await _tableStorageService.GetEntityAsync<MiniSplitConfig>("minisplitconfig", "config", "cold");

        var toggles = logEntries
          .Where(e =>
            e.TimestampUtc >= startUtc &&
            e.TimestampUtc <= endUtc &&
            (e.Notes?.Contains("Turned on", StringComparison.OrdinalIgnoreCase) == true ||
             e.Notes?.Contains("Turned off", StringComparison.OrdinalIgnoreCase) == true))
          .OrderBy(e => e.TimestampUtc)
          .ToList();

        double totalMinutes = 0;
        int sessionCount = 0;
        MiniSplitLogActivity? lastOn = null;

        foreach (var entry in toggles)
        {
          if (entry.Notes.Contains("Turned on", StringComparison.OrdinalIgnoreCase))
          {
            lastOn = entry;
          }
          else if (entry.Notes.Contains("Turned off", StringComparison.OrdinalIgnoreCase) && lastOn != null)
          {
            var duration = (entry.TimestampUtc - lastOn.TimestampUtc).TotalMinutes;
            totalMinutes += duration;
            sessionCount++;
            lastOn = null;
          }
        }

        double totalHours = totalMinutes / 60;
        double totalKWh = (coolConfig != null) ? totalHours * coolConfig.kWhPerHour : 0;
        double totalCost = (coolConfig != null) ? totalKWh * coolConfig.costPerKWh : 0;

        var response = new MonthlySummaryResponse
        {
          Success = true,
          Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
          TrackingId = Guid.NewGuid().ToString(),

          TotalRuntimeHours = Math.Round(totalHours, 2),
          SessionCount = sessionCount,
          TotalCostUSD = Math.Round(totalCost, 2),
          TotalKWhUsed = Math.Round(totalKWh, 2),
          AverageSessionMinutes = sessionCount > 0 ? Math.Round(totalMinutes / sessionCount, 2) : null,

          CoolConfig = coolConfig!,
          HeatConfig = null // You can extend logic later if you want to process heat mode
        };

        return Ok(response);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Failed to generate monthly summary.");
        return StatusCode(500, new { message = "Internal server error generating monthly summary." });
      }
    }


    [HttpGet("summary-by-range")]
    [ProducesResponseType(typeof(MonthlySummaryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
    [Authorize]
    public async Task<IActionResult> SummaryByRange([FromQuery] DateTime startDate, [FromQuery] DateTime endDate)
    {
      try
      {
        if (endDate <= startDate)
          return BadRequest(new { message = "endDate must be after startDate." });

        var logEntries = await _tableStorageService.QueryEntitiesAsync<MiniSplitLogActivity>(
          "minisplitlogs", filter: $"PartitionKey eq 'minisplit'"
        );

        var coolConfig = await _tableStorageService.GetEntityAsync<MiniSplitConfig>("minisplitconfig", "config", "cold");
        var centralTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time");

        // Convert input range from Central Time to UTC
        var startUtc = TimeZoneInfo.ConvertTimeToUtc(startDate.Date, centralTimeZone);
        var endUtc = TimeZoneInfo.ConvertTimeToUtc(endDate.Date.AddDays(1), centralTimeZone); // exclusive

        // Filter relevant on/off toggle logs
        var toggles = logEntries
          .Where(e =>
            e.TimestampUtc >= startUtc &&
            e.TimestampUtc < endUtc &&
            (e.Notes?.Contains("Turned on", StringComparison.OrdinalIgnoreCase) == true ||
             e.Notes?.Contains("Turned off", StringComparison.OrdinalIgnoreCase) == true))
          .OrderBy(e => e.TimestampUtc)
          .ToList();

        // Process toggles to calculate runtime
        double totalMinutes = 0;
        int sessionCount = 0;
        MiniSplitLogActivity? lastOn = null;

        foreach (var entry in toggles)
        {
          if (entry.Notes.Contains("Turned on", StringComparison.OrdinalIgnoreCase))
          {
            lastOn = entry;
          }
          else if (entry.Notes.Contains("Turned off", StringComparison.OrdinalIgnoreCase) && lastOn != null)
          {
            var duration = (entry.TimestampUtc - lastOn.TimestampUtc).TotalMinutes;
            totalMinutes += duration;
            sessionCount++;
            lastOn = null;
          }
        }

        double totalHours = totalMinutes / 60;
        double totalKWh = coolConfig != null ? totalHours * coolConfig.kWhPerHour : 0;
        double totalCost = coolConfig != null ? totalKWh * coolConfig.costPerKWh : 0;

        var response = new MonthlySummaryResponse
        {
          Success = true,
          Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
          TrackingId = Guid.NewGuid().ToString(),
          TotalRuntimeHours = Math.Round(totalHours, 2),
          TotalKWhUsed = Math.Round(totalKWh, 2),
          TotalCostUSD = Math.Round(totalCost, 2),
          SessionCount = sessionCount,
          AverageSessionMinutes = sessionCount > 0 ? Math.Round(totalMinutes / sessionCount, 2) : null,
          CoolConfig = coolConfig!,
          HeatConfig = null // You can include heatConfig if you plan to calculate mixed modes
        };

        return Ok(response);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Failed to generate summary by range.");
        return StatusCode(500, new { message = "Internal server error generating summary by range." });
      }
    }


    [HttpGet("summary-daily-breakdown")]
    [ProducesResponseType(typeof(DailySummaryResponse), StatusCodes.Status200OK)]
    [Authorize]
    public async Task<IActionResult> SummaryDailyBreakdown([FromQuery] DateTime startDate, [FromQuery] DateTime endDate)
    {
      try
      {
        if (endDate < startDate)
          return BadRequest(new { message = "endDate must be on/after startDate." });

        var tz = TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time");
        var coolConfig = await _tableStorageService.GetEntityAsync<MiniSplitConfig>("minisplitconfig", "config", "cold");

        // local (CDT) inclusive range
        var startCdt = startDate.Date;
        var endCdtInclusive = endDate.Date;

        // pull logs once
        var allLogs = await _tableStorageService.QueryEntitiesAsync<MiniSplitLogActivity>(
          "minisplitlogs", filter: $"PartitionKey eq 'minisplit'");

        // all ON/OFF toggles (sorted)
        var allToggles = allLogs
          .Where(e => e.Notes?.IndexOf("Turned on", StringComparison.OrdinalIgnoreCase) >= 0
                   || e.Notes?.IndexOf("Turned off", StringComparison.OrdinalIgnoreCase) >= 0)
          .OrderBy(e => e.TimestampUtc)
          .ToList();

        var summaries = new List<DailySummaryEntry>();

        for (var day = startCdt; day <= endCdtInclusive; day = day.AddDays(1))
        {
          var dayStartUtc = TimeZoneInfo.ConvertTimeToUtc(day, tz);
          var dayEndUtc = TimeZoneInfo.ConvertTimeToUtc(day.AddDays(1), tz);

          // state at start of day
          var priorToggle = allToggles.LastOrDefault(t => t.TimestampUtc < dayStartUtc);
          bool isOn = priorToggle != null &&
                      priorToggle.Notes.IndexOf("Turned on", StringComparison.OrdinalIgnoreCase) >= 0;
          DateTimeOffset? lastOnUtc = isOn ? dayStartUtc : (DateTimeOffset?)null;

          // toggles inside day
          var togglesInDay = allToggles
            .Where(t => t.TimestampUtc >= dayStartUtc && t.TimestampUtc < dayEndUtc)
            .ToList();

          DateTime? firstOnLocal = null;
          DateTime? lastOffLocal = null;
          double runtimeMinutes = 0;
          int sessionCount = 0;

          foreach (var t in togglesInDay)
          {
            bool isOnNote = t.Notes.IndexOf("Turned on", StringComparison.OrdinalIgnoreCase) >= 0;

            if (isOnNote)
            {
              if (firstOnLocal == null)
                firstOnLocal = TimeZoneInfo.ConvertTimeFromUtc(t.TimestampUtc.UtcDateTime, tz);

              if (!isOn)
              {
                isOn = true;
                lastOnUtc = t.TimestampUtc;
              }
            }
            else // off
            {
              lastOffLocal = TimeZoneInfo.ConvertTimeFromUtc(t.TimestampUtc.UtcDateTime, tz);

              if (isOn && lastOnUtc != null)
              {
                var start = lastOnUtc.Value < dayStartUtc ? dayStartUtc : lastOnUtc.Value;
                var end = t.TimestampUtc > dayEndUtc ? dayEndUtc : t.TimestampUtc;
                if (end > start)
                {
                  runtimeMinutes += (end - start).TotalMinutes;
                  sessionCount++;
                }
                isOn = false;
                lastOnUtc = null;
              }
            }
          }

          // still ON at end of day
          if (isOn && lastOnUtc != null)
          {
            var start = lastOnUtc.Value < dayStartUtc ? dayStartUtc : lastOnUtc.Value;
            var end = dayEndUtc;
            if (end > start)
              runtimeMinutes += (end - start).TotalMinutes;
          }

          var hours = runtimeMinutes / 60.0;
          var kWh = (coolConfig != null) ? hours * coolConfig.kWhPerHour : 0.0;
          var cost = (coolConfig != null) ? kWh * coolConfig.costPerKWh : 0.0;

          // metrics from all logs in the day
          var logsInDay = allLogs.Where(l => l.TimestampUtc >= dayStartUtc && l.TimestampUtc < dayEndUtc).ToList();

          double? avgOutside = logsInDay
            .Where(l => l.OutsideTempF > 0)
            .Select(l => (double?)l.OutsideTempF)
            .DefaultIfEmpty(null)
            .Average();

          double? maxOutside = logsInDay
            .Where(l => l.OutsideTempF > 0)
            .Select(l => (double?)l.OutsideTempF)
            .DefaultIfEmpty(null)
            .Max();

          double? maxInside = logsInDay
            .Where(l => l.InsideTempF > 0)
            .Select(l => (double?)l.InsideTempF)
            .DefaultIfEmpty(null)
            .Max();

          // Use the most recent non-zero ThresholdCool seen that day (common for setpoint)
          int? thresholdCool = logsInDay
            .Where(l => l.ThresholdCool > 0)
            .OrderBy(l => l.TimestampUtc)
            .Select(l => (int?)l.ThresholdCool)
            .LastOrDefault();

          summaries.Add(new DailySummaryEntry
          {
            Date = day,
            RuntimeHours = Math.Round(hours, 2),
            TotalKWhUsed = Math.Round(kWh, 2),
            TotalCostUSD = Math.Round(cost, 2),
            SessionCount = sessionCount,

            AvgOutsideTempF = avgOutside.HasValue ? Math.Round(avgOutside.Value, 1) : (double?)null,
            MaxOutsideTempF = maxOutside.HasValue ? Math.Round(maxOutside.Value, 1) : (double?)null,
            MaxInsideTempF = maxInside.HasValue ? Math.Round(maxInside.Value, 1) : (double?)null,
            ThresholdCool = thresholdCool,

            TurnedOn = firstOnLocal,
            TurnedOff = lastOffLocal
          });
        }

        var response = new DailySummaryResponse
        {
          Success = true,
          Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
          TrackingId = Guid.NewGuid().ToString(),
          DailySummaries = summaries.OrderBy(s => s.Date).ToList()
        };

        return Ok(response);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Failed to generate daily breakdown from toggle logs.");
        return StatusCode(500, new { message = "Internal server error generating daily breakdown." });
      }
    }




    [HttpGet("summary-hourly-breakdown")]
    [ProducesResponseType(typeof(List<HourlySummaryEntry>), StatusCodes.Status200OK)]
    [Authorize]
    public async Task<IActionResult> SummaryHourlyBreakdown([FromQuery] DateTime date)
    {
      try
      {
        var centralTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time");
        var startUtc = TimeZoneInfo.ConvertTimeToUtc(date.Date, centralTimeZone);
        var endUtc = startUtc.AddDays(1);

        var coolConfig = await _tableStorageService.GetEntityAsync<MiniSplitConfig>("minisplitconfig", "config", "cold");

        var allEntries = await _tableStorageService.QueryEntitiesAsync<MiniSplitLogActivity>(
          "minisplitlogs", filter: $"PartitionKey eq 'minisplit'");

        var toggleEvents = allEntries
          .Where(e =>
            e.TimestampUtc >= startUtc && e.TimestampUtc < endUtc &&
            (e.Notes?.Contains("Turned on", StringComparison.OrdinalIgnoreCase) == true ||
             e.Notes?.Contains("Turned off", StringComparison.OrdinalIgnoreCase) == true))
          .OrderBy(e => e.TimestampUtc)
          .ToList();

        var hourlyData = new List<HourlySummaryEntry>();
        MiniSplitLogActivity? lastOn = null;

        double totalRuntimeMinutes = 0;
        bool isCurrentlyOn = false;
        DateTimeOffset? lastOnTime = null;

        for (int hour = 0; hour < 24; hour++)
        {
          var hourStart = startUtc.AddHours(hour);
          var hourEnd = hourStart.AddHours(1);

          var entriesInHour = allEntries
            .Where(e => e.TimestampUtc >= hourStart && e.TimestampUtc < hourEnd)
            .ToList();

          var togglesInHour = toggleEvents
            .Where(e => e.TimestampUtc >= hourStart && e.TimestampUtc < hourEnd)
            .ToList();

          double runtimeThisHour = 0;

          foreach (var toggle in togglesInHour)
          {
            if (toggle.Notes.Contains("Turned on", StringComparison.OrdinalIgnoreCase))
            {
              isCurrentlyOn = true;
              lastOnTime = toggle.TimestampUtc;
            }
            else if (toggle.Notes.Contains("Turned off", StringComparison.OrdinalIgnoreCase))
            {
              if (isCurrentlyOn && lastOnTime != null)
              {
                // Runtime ends at the toggle off time
                var onTime = lastOnTime.Value < hourStart ? hourStart : lastOnTime.Value;
                var offTime = toggle.TimestampUtc > hourEnd ? hourEnd : toggle.TimestampUtc;

                runtimeThisHour += Math.Max(0, (offTime - onTime).TotalMinutes);
                isCurrentlyOn = false;
                lastOnTime = null;
              }
            }
          }

          // Still on at end of hour without toggle
          if (isCurrentlyOn && lastOnTime != null)
          {
            var onTime = lastOnTime.Value < hourStart ? hourStart : lastOnTime.Value;
            var offTime = hourEnd;

            runtimeThisHour += Math.Max(0, (offTime - onTime).TotalMinutes);
          }

          totalRuntimeMinutes += runtimeThisHour;

          // Calculate energy and cost for this hour only
          var kWhUsed = coolConfig != null ? (runtimeThisHour / 60.0) * coolConfig.kWhPerHour : 0;
          var costUSD = coolConfig != null ? kWhUsed * coolConfig.costPerKWh : 0;

          var mostRecent = entriesInHour.LastOrDefault();

          hourlyData.Add(new HourlySummaryEntry
          {
            Hour = TimeZoneInfo.ConvertTimeFromUtc(hourStart, centralTimeZone),
            RuntimeMinutes = Math.Round(runtimeThisHour, 1),
            EnergyKWhUsed = Math.Round(kWhUsed, 3), // ← Added this line
            EnergyCostUSD = Math.Round(costUSD, 2),
            AvgOutsideTempF = Math.Round(
              entriesInHour.Where(e => e.OutsideTempF > 0).Select(e => e.OutsideTempF).DefaultIfEmpty().Average(), 1),
            ThresholdCool = mostRecent?.ThresholdCool ?? 0,
            TempSet = mostRecent != null ? mostRecent.TempSet / 10 : 0,
            OutsideTempF = mostRecent?.OutsideTempF ?? 0,
            InsideTempF = mostRecent?.InsideTempF ?? 0,
            IsOn = mostRecent?.IsOn ?? false
          });

        }


        return Ok(hourlyData.OrderBy(h => h.Hour));
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Failed to generate hourly breakdown.");
        return StatusCode(500, new { message = "Internal server error generating hourly breakdown." });
      }
    }


    [HttpGet("summary-weekly-breakdown")]
    [ProducesResponseType(typeof(List<WeeklySummaryEntry>), StatusCodes.Status200OK)]
    [Authorize]
    public async Task<IActionResult> SummaryWeeklyBreakdown([FromQuery] DateTime startDate, [FromQuery] DateTime endDate)
    {
      try
      {
        if (endDate <= startDate)
          return BadRequest(new { message = "endDate must be after startDate." });

        var tz = TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time");

        // Normalize query dates as local CDT days
        var startCdt = startDate.Date;
        var endCdtExclusive = endDate.Date.AddDays(1);

        // Build weekly windows in CDT (Sunday-start weeks)
        DateTime cursor = DateTimeExtensions.StartOfWeek(startCdt, DayOfWeek.Sunday);
        var weeks = new List<(DateTime weekStartCdt, DateTime weekEndCdtExclusive)>();
        while (cursor < endCdtExclusive)
        {
          var weekStart = cursor;
          var weekEndExclusive = cursor.AddDays(7);
          // Clip to requested range
          var winStart = weekStart < startCdt ? startCdt : weekStart;
          var winEndExclusive = weekEndExclusive > endCdtExclusive ? endCdtExclusive : weekEndExclusive;
          if (winStart < winEndExclusive)
            weeks.Add((winStart, winEndExclusive));
          cursor = weekEndExclusive;
        }

        // Fetch config
        var coolConfig = await _tableStorageService.GetEntityAsync<MiniSplitConfig>("minisplitconfig", "config", "cold");

        // Pull all log entries once (we’ll window them below)
        var allLogs = await _tableStorageService.QueryEntitiesAsync<MiniSplitLogActivity>(
          "minisplitlogs", filter: $"PartitionKey eq 'minisplit'");

        // Pre-index toggle events (note we need events outside the range to determine carry-in state)
        var allToggles = allLogs
          .Where(e => e.Notes?.IndexOf("Turned on", StringComparison.OrdinalIgnoreCase) >= 0
                   || e.Notes?.IndexOf("Turned off", StringComparison.OrdinalIgnoreCase) >= 0)
          .OrderBy(e => e.TimestampUtc)
          .ToList();

        var results = new List<WeeklySummaryEntry>();

        foreach (var (weekStartCdt, weekEndCdtExclusive) in weeks)
        {
          // Convert this week's CDT window to UTC
          var weekStartUtc = TimeZoneInfo.ConvertTimeToUtc(weekStartCdt, tz);
          var weekEndUtcExclusive = TimeZoneInfo.ConvertTimeToUtc(weekEndCdtExclusive, tz);

          // Toggles within the window
          var togglesInWeek = allToggles
            .Where(t => t.TimestampUtc >= weekStartUtc && t.TimestampUtc < weekEndUtcExclusive)
            .ToList();

          // Determine carry-in state: last toggle BEFORE weekStartUtc
          var priorToggle = allToggles
            .LastOrDefault(t => t.TimestampUtc < weekStartUtc);

          bool isOn = priorToggle != null &&
                      priorToggle.Notes.IndexOf("Turned on", StringComparison.OrdinalIgnoreCase) >= 0;

          DateTimeOffset? lastOnTime = isOn ? weekStartUtc : (DateTimeOffset?)null;
          double runtimeMinutes = 0;
          int sessionCount = 0;

          // Walk toggles and accumulate runtime bounded to this week window
          foreach (var t in togglesInWeek)
          {
            if (t.Notes.IndexOf("Turned on", StringComparison.OrdinalIgnoreCase) >= 0)
            {
              if (!isOn)
              {
                isOn = true;
                lastOnTime = t.TimestampUtc;
              }
              // If already on, ignore redundant "on"
            }
            else if (t.Notes.IndexOf("Turned off", StringComparison.OrdinalIgnoreCase) >= 0)
            {
              if (isOn && lastOnTime != null)
              {
                var start = lastOnTime.Value < weekStartUtc ? weekStartUtc : lastOnTime.Value;
                var end = t.TimestampUtc > weekEndUtcExclusive ? weekEndUtcExclusive : t.TimestampUtc;
                if (end > start)
                {
                  runtimeMinutes += (end - start).TotalMinutes;
                  sessionCount++;
                }
                isOn = false;
                lastOnTime = null;
              }
              // If already off, ignore redundant "off"
            }
          }

          // Carry past the end of week if still ON
          if (isOn && lastOnTime != null)
          {
            var start = lastOnTime.Value < weekStartUtc ? weekStartUtc : lastOnTime.Value;
            var end = weekEndUtcExclusive;
            if (end > start)
              runtimeMinutes += (end - start).TotalMinutes;
          }

          // Energy + cost
          var hours = runtimeMinutes / 60.0;
          var kWh = (coolConfig != null) ? hours * coolConfig.kWhPerHour : 0.0;
          var cost = (coolConfig != null) ? kWh * coolConfig.costPerKWh : 0.0;

          // Avg outside temp uses all logs in week window
          var logsInWeek = allLogs.Where(l => l.TimestampUtc >= weekStartUtc && l.TimestampUtc < weekEndUtcExclusive);
          var avgOutside = logsInWeek.Where(l => l.OutsideTempF > 0).Select(l => l.OutsideTempF)
                            .DefaultIfEmpty().Average();

          results.Add(new WeeklySummaryEntry
          {
            WeekStartDate = weekStartCdt,                  // CDT week start (Sunday)
            RuntimeHours = Math.Round(hours, 2),
            TotalKWhUsed = Math.Round(kWh, 2),
            TotalCostUSD = Math.Round(cost, 2),
            AvgOutsideTempF = Math.Round(avgOutside, 1),
            SessionCount = sessionCount
          });
        }

        return Ok(results.OrderBy(r => r.WeekStartDate));
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Failed to generate weekly breakdown.");
        return StatusCode(500, new { message = "Internal server error generating weekly breakdown." });
      }
    }




  }
}
