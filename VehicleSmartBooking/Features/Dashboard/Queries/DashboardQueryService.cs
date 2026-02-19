using Microsoft.EntityFrameworkCore;
using VehicleBooking.Web.Data;
using VehicleBooking.Web.Domain.Entities;
using VehicleBooking.Web.Domain.Enums;
using VehicleBooking.Web.Domain.Helpers;
using VehicleSmartBooking.Features.Dashboard;
using VehicleSmartBooking.Features.Dashboard.Widgets;
using VehicleSmartBooking.Features.Dashboard.Widgets.CostVendor;
using VehicleSmartBooking.Features.Dashboard.Widgets.Drivers;
using VehicleSmartBooking.Features.Dashboard.Widgets.Fleet;
using VehicleSmartBooking.Features.Dashboard.Widgets.Kpis;
using VehicleSmartBooking.Features.Dashboard.Widgets.Operations;
using VehicleSmartBooking.Features.Dashboard.Widgets.UsageMix;

namespace VehicleSmartBooking.Features.Dashboard.Queries;

public sealed class DashboardQueryService : IDashboardQueryService
{
    private readonly VehicleBookingDbContext _db;

    public DashboardQueryService(VehicleBookingDbContext db)
    {
        _db = db;
    }

    public async Task<DashboardFilterOptionsViewModel> GetFilterOptionsAsync()
    {
        var deptOptions = await _db.Users
            .AsNoTracking()
            .Where(u => u.DeptAbbr != null && u.DeptAbbr != "")
            .Select(u => u.DeptAbbr!)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync();

        var divOptions = await _db.Users
            .AsNoTracking()
            .Where(u => u.DivAbbr != null && u.DivAbbr != "")
            .Select(u => u.DivAbbr!)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync();

        var functionOptions = await _db.Users
            .AsNoTracking()
            .Where(u => u.FunctionAbbr != null && u.FunctionAbbr != "")
            .Select(u => u.FunctionAbbr!)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync();

        return new DashboardFilterOptionsViewModel
        {
            DeptOptions = deptOptions,
            DivOptions = divOptions,
            FunctionOptions = functionOptions
        };
    }

    public async Task<KpiCardsVm> GetKpiCardsAsync(DashboardFilters filters)
    {
        var bookingQuery = ApplyFilters(_db.Bookings.AsNoTracking().Include(b => b.ExternalRental), filters);

        var totalTrips = await bookingQuery.CountAsync();
        var fleetTrips = await bookingQuery.CountAsync(b => !b.IsExternalRental && !b.IsPersonal);
        var vendorTrips = await bookingQuery.CountAsync(b => b.IsExternalRental);
        var personalTrips = await bookingQuery.CountAsync(b => b.IsPersonal);

        var vendorCost = await bookingQuery
            .Where(b => b.ExternalRental != null)
            .Select(b => (decimal?)(b.ExternalRental!.QuotedPrice ?? 0m))
            .SumAsync() ?? 0m;

        var personalClaimCost = 0m;

        var items = new List<KpiCardItemVm>
        {
            new()
            {
                Title = "Total Trips",
                ValueText = totalTrips.ToString("N0"),
                IconClass = "bi bi-graph-up",
                Href = UrlFor("/Admin/AllBookings")
            },
            new()
            {
                Title = "Fleet Trips",
                ValueText = fleetTrips.ToString("N0"),
                IconClass = "bi bi-truck",
                Href = UrlFor("/Admin/AllBookings?mode=fleet")
            },
            new()
            {
                Title = "Vendor Trips",
                ValueText = vendorTrips.ToString("N0"),
                IconClass = "bi bi-people",
                Href = UrlFor("/Admin/AllBookings?mode=vendor")
            },
            new()
            {
                Title = "Personal Trips",
                ValueText = personalTrips.ToString("N0"),
                IconClass = "bi bi-person-check",
                Href = UrlFor("/Admin/AllBookings?mode=personal")
            },
            new()
            {
                Title = "Vendor Cost",
                ValueText = vendorCost.ToString("N2"),
                IconClass = "bi bi-cash-coin",
                Href = UrlFor("/Admin/AllBookings?mode=vendor")
            },
            new()
            {
                Title = "Personal Claim Cost",
                ValueText = personalClaimCost.ToString("N2"),
                IconClass = "bi bi-receipt",
                Href = UrlFor("/Admin/AllBookings?mode=personal")
            }
        };

        return new KpiCardsVm
        {
            Items = items
        };
    }

    public async Task<UsageMixVm> GetUsageMixAsync(DashboardFilters filters)
    {
        var bookingQuery = ApplyFilters(_db.Bookings.AsNoTracking(), filters);

        var months = await bookingQuery
            .GroupBy(b => new { b.StartAtUtc.Year, b.StartAtUtc.Month })
            .Select(group => new UsageMixMonthVm
            {
                Year = group.Key.Year,
                Month = group.Key.Month,
                Label = $"{group.Key.Year}-{group.Key.Month:D2}",
                FleetTrips = group.Count(b => !b.IsExternalRental && !b.IsPersonal),
                VendorTrips = group.Count(b => b.IsExternalRental),
                PersonalTrips = group.Count(b => b.IsPersonal)
            })
            .OrderBy(x => x.Year)
            .ThenBy(x => x.Month)
            .ToListAsync();

        return new UsageMixVm
        {
            Months = months
        };
    }

    public async Task<TripScopeVm> GetTripScopeMixAsync(DashboardFilters filters)
    {
        var bookingQuery = ApplyFilters(_db.Bookings.AsNoTracking(), filters);

        var months = await bookingQuery
            .GroupBy(b => new { b.StartAtUtc.Year, b.StartAtUtc.Month })
            .Select(group => new TripScopeMonthVm
            {
                Year = group.Key.Year,
                Month = group.Key.Month,
                Label = $"{group.Key.Year}-{group.Key.Month:D2}",
                InProvinceTrips = group.Count(b => b.TripType == TripType.InProvince),
                OutProvinceTrips = group.Count(b => b.TripType == TripType.OutProvince)
            })
            .OrderBy(x => x.Year)
            .ThenBy(x => x.Month)
            .ToListAsync();

        return new TripScopeVm
        {
            Months = months
        };
    }

    public async Task<CostVendorWidgetViewModel> GetCostVendorAsync(DashboardFilters filters)
    {
        var bookingQuery = ApplyFilters(_db.Bookings.AsNoTracking().Include(b => b.ExternalRental), filters)
            .Where(b => b.ExternalRental != null);

        var totalExternalCost = await bookingQuery
            .Select(b => (decimal?)(b.ExternalRental!.QuotedPrice ?? 0m))
            .SumAsync() ?? 0m;

        var externalBookingCount = await bookingQuery.CountAsync();

        var topVendors = await bookingQuery
            .Where(b => b.ExternalRental!.VendorName != null && b.ExternalRental!.VendorName != "")
            .GroupBy(b => b.ExternalRental!.VendorName!)
            .Select(group => new VendorCostItem
            {
                VendorName = group.Key,
                TotalCost = group.Sum(x => x.ExternalRental!.QuotedPrice ?? 0m),
                BookingCount = group.Count()
            })
            .OrderByDescending(x => x.TotalCost)
            .ThenByDescending(x => x.BookingCount)
            .Take(5)
            .ToListAsync();

        return new CostVendorWidgetViewModel
        {
            TotalExternalCost = totalExternalCost,
            ExternalBookingCount = externalBookingCount,
            TopVendors = topVendors
        };
    }

    public async Task<FleetSnapshotWidgetViewModel> GetFleetSnapshotAsync(DashboardFilters filters)
    {
        var vehiclesQuery = _db.Vehicles.AsNoTracking().Where(v => v.IsActive);
        var driversQuery = _db.Drivers.AsNoTracking().Where(d => d.IsActive);

        var activeVehicles = await vehiclesQuery.CountAsync();
        var availableVehicles = await vehiclesQuery.CountAsync(v => v.Status == VehicleStatus.Available);
        var maintenanceVehicles = await vehiclesQuery.CountAsync(v => v.Status == VehicleStatus.Maintenance);
        var outOfServiceVehicles = await vehiclesQuery.CountAsync(v => v.Status == VehicleStatus.OutOfService);
        var activeDrivers = await driversQuery.CountAsync();

        return new FleetSnapshotWidgetViewModel
        {
            ActiveVehicles = activeVehicles,
            AvailableVehicles = availableVehicles,
            MaintenanceVehicles = maintenanceVehicles,
            OutOfServiceVehicles = outOfServiceVehicles,
            ActiveDrivers = activeDrivers
        };
    }

    public async Task<DriverQualityWidgetViewModel> GetDriverQualityAsync(DashboardFilters filters)
    {
        var bookingIdsQuery = ApplyFilters(_db.Bookings.AsNoTracking(), filters).Select(b => b.BookingId);

        var ratingsQuery = _db.DriverRatings
            .AsNoTracking()
            .Include(r => r.Driver)
            .ThenInclude(d => d.User)
            .Where(r => bookingIdsQuery.Contains(r.BookingId));

        var ratingCount = await ratingsQuery.CountAsync();

        var averageScore = await ratingsQuery
            .Select(r => (r.Score1 + r.Score2 + r.Score3 + r.Score4 + r.Score5) / 5.0)
            .DefaultIfEmpty(0)
            .AverageAsync();

        var recentRatings = await ratingsQuery
            .OrderByDescending(r => r.CreatedAtUtc)
            .Take(5)
            .Select(r => new DriverRatingSummary
            {
                DriverName = r.Driver.User.UsernameTH ?? r.Driver.User.UsernameEN ?? r.Driver.User.UserCode,
                AverageScore = (r.Score1 + r.Score2 + r.Score3 + r.Score4 + r.Score5) / 5.0,
                RatedAtUtc = r.CreatedAtUtc
            })
            .ToListAsync();

        return new DriverQualityWidgetViewModel
        {
            AverageScore = averageScore,
            RatingCount = ratingCount,
            RecentRatings = recentRatings
        };
    }

    public async Task<ActivityExceptionsWidgetViewModel> GetActivityExceptionsAsync(DashboardFilters filters)
    {
        var bookingQuery = ApplyFilters(_db.Bookings.AsNoTracking().Include(b => b.Requester), filters);
        var recentThreshold = DateTime.UtcNow.AddDays(-7);

        var recentActivityCount = await bookingQuery.CountAsync(b => b.CreatedAtUtc >= recentThreshold);

        var exceptionStatuses = new[]
        {
            BookingStatus.AdminActionRequired,
            BookingStatus.Cancelled,
            BookingStatus.Rejected,
            BookingStatus.VendorRejectedByUser
        };

        var exceptionCount = await bookingQuery.CountAsync(b => exceptionStatuses.Contains(b.Status));

        var recentExceptions = await bookingQuery
            .Where(b => exceptionStatuses.Contains(b.Status))
            .OrderByDescending(b => b.CreatedAtUtc)
            .Take(5)
            .Select(b => new ExceptionBookingItem
            {
                BookingId = b.BookingId,
                RequesterName = b.Requester.UsernameTH ?? b.Requester.UsernameEN ?? b.Requester.UserCode,
                StatusLabel = b.Status.ToString(),
                CreatedAtUtc = b.CreatedAtUtc
            })
            .ToListAsync();

        return new ActivityExceptionsWidgetViewModel
        {
            RecentActivityCount = recentActivityCount,
            ExceptionCount = exceptionCount,
            RecentExceptions = recentExceptions
        };
    }

    public async Task<CostTrendVm> GetVendorCostTrendAsync(DashboardFilters filters)
    {
        var bookingQuery = ApplyFilters(_db.Bookings.AsNoTracking().Include(b => b.ExternalRental), filters)
            .Where(b => b.ExternalRental != null);

        var months = await bookingQuery
            .GroupBy(b => new { b.StartAtUtc.Year, b.StartAtUtc.Month })
            .Select(group => new CostTrendMonthVm
            {
                Year = group.Key.Year,
                Month = group.Key.Month,
                Label = $"{group.Key.Year}-{group.Key.Month:D2}",
                TotalCost = group.Sum(b => b.ExternalRental!.QuotedPrice ?? 0m)
            })
            .OrderBy(x => x.Year)
            .ThenBy(x => x.Month)
            .ToListAsync();

        return new CostTrendVm
        {
            Months = months
        };
    }

    public Task<CostTrendVm> GetPersonalCostTrendAsync(DashboardFilters filters)
    {
        var empty = new CostTrendVm
        {
            Months = Array.Empty<CostTrendMonthVm>()
        };

        return Task.FromResult(empty);
    }

    public async Task<IReadOnlyList<VendorRowVm>> GetTopVendorsAsync(DashboardFilters filters, int top = 10)
    {
        var bookingQuery = ApplyFilters(_db.Bookings.AsNoTracking().Include(b => b.ExternalRental), filters)
            .Where(b => b.ExternalRental != null && b.ExternalRental!.VendorName != null && b.ExternalRental!.VendorName != "");

        var vendors = await bookingQuery
            .GroupBy(b => b.ExternalRental!.VendorName!)
            .Select(group => new VendorRowVm
            {
                VendorName = group.Key,
                Trips = group.Count(),
                TotalCost = group.Sum(b => b.ExternalRental!.QuotedPrice ?? 0m),
                AverageCostPerTrip = group.Sum(b => b.ExternalRental!.QuotedPrice ?? 0m) / Math.Max(1, group.Count()),
                LastUsedAtUtc = group.Max(b => (DateTime?)b.StartAtUtc)
            })
            .OrderByDescending(x => x.TotalCost)
            .ThenByDescending(x => x.Trips)
            .Take(top)
            .ToListAsync();

        return vendors;
    }

    public async Task<VehicleStatusVm> GetVehicleStatusAsync()
    {
        var items = await _db.Vehicles
            .AsNoTracking()
            .Where(v => v.IsActive)
            .GroupBy(v => v.Status)
            .Select(group => new VehicleStatusItemVm
            {
                Status = group.Key,
                Label = group.Key.ToString(),
                Count = group.Count()
            })
            .OrderBy(x => x.Status)
            .ToListAsync();

        return new VehicleStatusVm
        {
            Items = items
        };
    }

    public async Task<IReadOnlyList<VehicleUtilRowVm>> GetTopUtilizedVehiclesAsync(DashboardFilters filters, int top = 10)
    {
        var bookingQuery = ApplyFilters(_db.Bookings.AsNoTracking(), filters)
            .Where(b => b.AssignedVehicleId != null);

        var vehicles = await (
                from b in bookingQuery
                join v in _db.Vehicles.AsNoTracking() on b.AssignedVehicleId equals v.VehicleId
                where v.IsActive
                group b by new { v.VehicleId, v.PlateNo, v.VehicleType } into grouped
                select new VehicleUtilRowVm
                {
                    PlateNo = grouped.Key.PlateNo,
                    VehicleType = grouped.Key.VehicleType,
                    TripsCount = grouped.Count(),
                    TotalHours = grouped.Sum(x => EF.Functions.DateDiffMinute(x.StartAtUtc, x.EndAtUtc)) / 60.0,
                    LastTripAtUtc = grouped.Max(x => (DateTime?)x.StartAtUtc)
                })
            .OrderByDescending(x => x.TripsCount)
            .ThenByDescending(x => x.TotalHours)
            .Take(top)
            .ToListAsync();

        return vehicles;
    }

    public async Task<RatingDistVm> GetRatingDistributionAsync(DashboardFilters filters)
    {
        var bookingIdsQuery = ApplyFilters(_db.Bookings.AsNoTracking(), filters)
            .Select(b => b.BookingId);

        var ratingValues = await _db.DriverRatings
            .AsNoTracking()
            .Where(r => bookingIdsQuery.Contains(r.BookingId))
            .Select(r => (r.Score1 + r.Score2 + r.Score3 + r.Score4 + r.Score5) / 5.0)
            .ToListAsync();

        var average = ratingValues.Count == 0 ? 0 : ratingValues.Average();

        var buckets = Enumerable.Range(1, 5)
            .Select(score => new RatingBucketVm
            {
                Score = score,
                Count = ratingValues.Count(value => (int)Math.Round(value, MidpointRounding.AwayFromZero) == score)
            })
            .ToList();

        return new RatingDistVm
        {
            AverageRating = average,
            RatingCount = ratingValues.Count,
            Buckets = buckets
        };
    }

    public async Task<DispatchOutcomeVm> GetDispatchOutcomeAsync(DashboardFilters filters)
    {
        var bookingIdsQuery = ApplyFilters(_db.Bookings.AsNoTracking(), filters)
            .Select(b => b.BookingId);

        var logs = await _db.BookingDispatchLogs
            .AsNoTracking()
            .Where(l => bookingIdsQuery.Contains(l.BookingId))
            .ToListAsync();

        var accepted = logs.Count(l => l.DriverAction == DriverAction.Accepted);
        var declined = logs.Count(l => l.DriverAction == DriverAction.Declined);
        var noResponse = logs.Count(l => l.DriverAction == null || l.DriverAction == DriverAction.TimedOut);

        var declineReasons = logs
            .Where(l => l.DriverAction == DriverAction.Declined && !string.IsNullOrWhiteSpace(l.DeclineReason))
            .GroupBy(l => l.DeclineReason!.Trim())
            .Select(group => new DeclineReasonVm
            {
                Reason = group.Key,
                Count = group.Count()
            })
            .OrderByDescending(x => x.Count)
            .Take(5)
            .ToList();

        return new DispatchOutcomeVm
        {
            AcceptedCount = accepted,
            DeclinedCount = declined,
            NoResponseCount = noResponse,
            TopDeclineReasons = declineReasons
        };
    }

    public async Task<IReadOnlyList<TopDriverRowVm>> GetTopDriversAsync(DashboardFilters filters, int top = 5)
    {
        var bookingQuery = ApplyFilters(_db.Bookings.AsNoTracking(), filters)
            .Where(b => b.AssignedDriverId != null);

        var bookingIdsQuery = bookingQuery.Select(b => b.BookingId);

        var tripCounts = await bookingQuery
            .GroupBy(b => b.AssignedDriverId!.Value)
            .Select(group => new { DriverId = group.Key, Trips = group.Count() })
            .ToListAsync();

        if (tripCounts.Count == 0)
        {
            return Array.Empty<TopDriverRowVm>();
        }

        var driverIds = tripCounts.Select(x => x.DriverId).ToList();

        var driverNames = await _db.Drivers
            .AsNoTracking()
            .Include(d => d.User)
            .Where(d => driverIds.Contains(d.DriverId))
            .Select(d => new { d.DriverId, Name = d.User.UsernameTH ?? d.User.UsernameEN ?? d.User.UserCode })
            .ToListAsync();

        var ratingMap = await _db.DriverRatings
            .AsNoTracking()
            .Where(r => driverIds.Contains(r.DriverId) && bookingIdsQuery.Contains(r.BookingId))
            .GroupBy(r => r.DriverId)
            .Select(group => new { DriverId = group.Key, Avg = group.Average(r => (r.Score1 + r.Score2 + r.Score3 + r.Score4 + r.Score5) / 5.0) })
            .ToListAsync();

        var dispatchMap = await _db.BookingDispatchLogs
            .AsNoTracking()
            .Where(l => driverIds.Contains(l.DriverId) && bookingIdsQuery.Contains(l.BookingId))
            .GroupBy(l => l.DriverId)
            .Select(group => new
            {
                DriverId = group.Key,
                Accepted = group.Count(x => x.DriverAction == DriverAction.Accepted),
                Declined = group.Count(x => x.DriverAction == DriverAction.Declined),
                TimedOut = group.Count(x => x.DriverAction == DriverAction.TimedOut || x.DriverAction == null)
            })
            .ToListAsync();

        var nameMap = driverNames.ToDictionary(x => x.DriverId, x => x.Name);
        var avgMap = ratingMap.ToDictionary(x => x.DriverId, x => x.Avg);
        var dispatchStats = dispatchMap.ToDictionary(x => x.DriverId, x => x);

        var results = tripCounts
            .Select(trip =>
            {
                var totalDispatch = 0;
                var accepted = 0;
                if (dispatchStats.TryGetValue(trip.DriverId, out var dispatch))
                {
                    accepted = dispatch.Accepted;
                    totalDispatch = dispatch.Accepted + dispatch.Declined + dispatch.TimedOut;
                }

                var acceptRate = totalDispatch == 0 ? 0 : accepted / (double)totalDispatch;

                return new TopDriverRowVm
                {
                    DriverName = nameMap.TryGetValue(trip.DriverId, out var name) ? name : trip.DriverId.ToString(),
                    Trips = trip.Trips,
                    AverageRating = avgMap.TryGetValue(trip.DriverId, out var avg) ? avg : 0,
                    AcceptRate = acceptRate
                };
            })
            .OrderByDescending(x => x.Trips)
            .ThenByDescending(x => x.AverageRating)
            .Take(top)
            .ToList();

        return results;
    }

    public async Task<IReadOnlyList<ActivityItemVm>> GetActivityFeedAsync(DashboardFilters filters, int take = 30)
    {
        var bookingQuery = ApplyFilters(_db.Bookings.AsNoTracking(), filters);

        var approvals = await (
                from approval in _db.BookingApprovals.AsNoTracking()
                join booking in bookingQuery on approval.BookingId equals booking.BookingId
                select new ActivityItemVm
                {
                    BookingId = approval.BookingId,
                    Title = "Approval update",
                    Detail = $"Level {approval.LevelNo} • {approval.Status}",
                    Source = "Approvals",
                    OccurredAtUtc = approval.ActionAtUtc ?? approval.CreatedAtUtc
                })
            .ToListAsync();

        var dispatchLogs = await (
                from log in _db.BookingDispatchLogs.AsNoTracking()
                join booking in bookingQuery on log.BookingId equals booking.BookingId
                select new ActivityItemVm
                {
                    BookingId = log.BookingId,
                    Title = "Dispatch",
                    Detail = log.DriverAction.HasValue ? log.DriverAction.Value.ToString() : "Dispatched",
                    Source = "Dispatch",
                    OccurredAtUtc = log.DriverActionAtUtc ?? log.DispatchedAtUtc
                })
            .ToListAsync();

        var vendorEvents = await (
                from rental in _db.ExternalRentals.AsNoTracking()
                join booking in bookingQuery on rental.BookingId equals booking.BookingId
                select new ActivityItemVm
                {
                    BookingId = rental.BookingId,
                    Title = "Vendor",
                    Detail = rental.VendorName ?? "Vendor update",
                    Source = "Vendors",
                    OccurredAtUtc = rental.UserDecisionAtUtc ?? rental.QuoteSentAtUtc ?? rental.AdminClosedAtUtc ?? booking.UpdatedAtUtc
                })
            .ToListAsync();

        var attachments = await (
                from attachment in _db.BookingAttachments.AsNoTracking()
                join booking in bookingQuery on attachment.BookingId equals booking.BookingId
                select new ActivityItemVm
                {
                    BookingId = attachment.BookingId,
                    Title = "Attachment uploaded",
                    Detail = attachment.FileName,
                    Source = "Attachments",
                    OccurredAtUtc = attachment.UploadedAtUtc
                })
            .ToListAsync();

        var all = approvals
            .Concat(dispatchLogs)
            .Concat(vendorEvents)
            .Concat(attachments)
            .OrderByDescending(x => x.OccurredAtUtc)
            .Take(take)
            .ToList();

        return all;
    }

    public async Task<IReadOnlyList<RiskItemVm>> GetRiskListAsync(DashboardFilters filters, int take = 10, int riskWindowHours = 6)
    {
        var bookingQuery = ApplyFilters(_db.Bookings.AsNoTracking().Include(b => b.Requester), filters);
        var terminal = BookingStatusHelper.TerminalStatuses;
        var now = DateTime.UtcNow;
        var threshold = now.AddHours(riskWindowHours);

        var candidates = await bookingQuery
            .Where(b => !terminal.Contains(b.Status)
                        && b.StartAtUtc >= now
                        && b.StartAtUtc <= threshold)
            .ToListAsync();

        if (candidates.Count == 0)
        {
            return Array.Empty<RiskItemVm>();
        }

        var bookingIds = candidates.Select(b => b.BookingId).ToList();
        var dispatchLogs = await _db.BookingDispatchLogs
            .AsNoTracking()
            .Where(l => bookingIds.Contains(l.BookingId))
            .ToListAsync();

        var latestDispatch = dispatchLogs
            .GroupBy(l => l.BookingId)
            .ToDictionary(
                group => group.Key,
                group => group.OrderByDescending(l => l.DispatchedAtUtc).FirstOrDefault());

        var risks = new List<RiskItemVm>();

        foreach (var booking in candidates)
        {
            var reasons = new List<string>();

            if (booking.Status == BookingStatus.WaitingApproval)
            {
                reasons.Add("Pending approval");
            }

            if (booking.Status == BookingStatus.WaitingDriverAccept)
            {
                if (!latestDispatch.TryGetValue(booking.BookingId, out var log) || log == null)
                {
                    reasons.Add("No dispatch response");
                }
                else if (log.DriverAction == null || log.DriverAction == DriverAction.TimedOut)
                {
                    reasons.Add("No dispatch response");
                }
                else if (log.DriverAction == DriverAction.Declined)
                {
                    reasons.Add("Driver declined");
                }
            }

            if (booking.AssignedVehicleId == null || booking.AssignedDriverId == null)
            {
                reasons.Add("Unassigned");
            }

            if (booking.Status == BookingStatus.WaitingAdminVendorQuotation
                || booking.Status == BookingStatus.WaitingUserVendorAccept
                || booking.Status == BookingStatus.WaitingAdminVendorConfirm)
            {
                reasons.Add("Vendor pending");
            }

            if (booking.Status == BookingStatus.WaitingAdminPersonal)
            {
                reasons.Add("Personal approval pending");
            }

            if (booking.Status == BookingStatus.AdminActionRequired)
            {
                reasons.Add("Admin action required");
            }

            if (reasons.Count == 0)
            {
                continue;
            }

            risks.Add(new RiskItemVm
            {
                BookingId = booking.BookingId,
                RequesterName = booking.Requester.UsernameTH ?? booking.Requester.UsernameEN ?? booking.Requester.UserCode,
                Status = booking.Status,
                StartAtUtc = booking.StartAtUtc,
                Reason = string.Join(" • ", reasons.Distinct())
            });
        }

        return risks
            .OrderBy(x => x.StartAtUtc)
            .Take(take)
            .ToList();
    }

    private static IQueryable<Booking> ApplyFilters(IQueryable<Booking> query, DashboardFilters filters)
    {
        if (filters.DateFrom.HasValue)
        {
            var fromUtc = filters.DateFrom.Value.Date;
            query = query.Where(b => b.StartAtUtc >= fromUtc);
        }

        if (filters.DateTo.HasValue)
        {
            var toUtc = filters.DateTo.Value.Date.AddDays(1);
            query = query.Where(b => b.StartAtUtc < toUtc);
        }

        if (!string.IsNullOrWhiteSpace(filters.Dept))
        {
            var dept = filters.Dept.Trim();
            query = query.Where(b => b.Requester.DeptAbbr == dept);
        }

        if (!string.IsNullOrWhiteSpace(filters.Div))
        {
            var div = filters.Div.Trim();
            query = query.Where(b => b.Requester.DivAbbr == div);
        }

        if (!string.IsNullOrWhiteSpace(filters.Function))
        {
            var function = filters.Function.Trim();
            query = query.Where(b => b.Requester.FunctionAbbr == function);
        }

        if (!string.IsNullOrWhiteSpace(filters.Mode))
        {
            var mode = filters.Mode.Trim().ToLowerInvariant();
            query = mode switch
            {
                "fleet" => query.Where(b => !b.IsExternalRental && !b.IsPersonal),
                "vendor" => query.Where(b => b.IsExternalRental),
                "personal" => query.Where(b => b.IsPersonal),
                _ => query
            };
        }

        if (filters.TripScope.HasValue)
        {
            var tripScope = filters.TripScope.Value;
            query = query.Where(b => b.TripType == tripScope);
        }

        if (filters.VehicleTypeRequested.HasValue)
        {
            var vehicleType = filters.VehicleTypeRequested.Value;
            query = query.Where(b => b.VehicleTypeRequested == vehicleType);
        }

        if (filters.Status.HasValue)
        {
            var status = filters.Status.Value;
            query = query.Where(b => b.Status == status);
        }

        return query;
    }

    private static string UrlFor(string href) => href;
}
