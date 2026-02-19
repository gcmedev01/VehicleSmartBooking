using VehicleSmartBooking.Features.Dashboard;
using VehicleSmartBooking.Features.Dashboard.Widgets;
using VehicleSmartBooking.Features.Dashboard.Widgets.CostVendor;
using VehicleSmartBooking.Features.Dashboard.Widgets.Drivers;
using VehicleSmartBooking.Features.Dashboard.Widgets.Fleet;
using VehicleSmartBooking.Features.Dashboard.Widgets.Kpis;
using VehicleSmartBooking.Features.Dashboard.Widgets.Operations;
using VehicleSmartBooking.Features.Dashboard.Widgets.UsageMix;

namespace VehicleSmartBooking.Features.Dashboard.Queries;

public interface IDashboardQueryService
{
    Task<DashboardFilterOptionsViewModel> GetFilterOptionsAsync();
    Task<KpiCardsVm> GetKpiCardsAsync(DashboardFilters filters);
    Task<UsageMixVm> GetUsageMixAsync(DashboardFilters filters);
    Task<TripScopeVm> GetTripScopeMixAsync(DashboardFilters filters);
    Task<CostTrendVm> GetVendorCostTrendAsync(DashboardFilters filters);
    Task<CostTrendVm> GetPersonalCostTrendAsync(DashboardFilters filters);
    Task<IReadOnlyList<VendorRowVm>> GetTopVendorsAsync(DashboardFilters filters, int top = 10);
    Task<VehicleStatusVm> GetVehicleStatusAsync();
    Task<IReadOnlyList<VehicleUtilRowVm>> GetTopUtilizedVehiclesAsync(DashboardFilters filters, int top = 10);
    Task<RatingDistVm> GetRatingDistributionAsync(DashboardFilters filters);
    Task<DispatchOutcomeVm> GetDispatchOutcomeAsync(DashboardFilters filters);
    Task<IReadOnlyList<TopDriverRowVm>> GetTopDriversAsync(DashboardFilters filters, int top = 5);
    Task<IReadOnlyList<ActivityItemVm>> GetActivityFeedAsync(DashboardFilters filters, int take = 30);
    Task<IReadOnlyList<RiskItemVm>> GetRiskListAsync(DashboardFilters filters, int take = 10, int riskWindowHours = 6);
    Task<CostVendorWidgetViewModel> GetCostVendorAsync(DashboardFilters filters);
    Task<FleetSnapshotWidgetViewModel> GetFleetSnapshotAsync(DashboardFilters filters);
    Task<DriverQualityWidgetViewModel> GetDriverQualityAsync(DashboardFilters filters);
    Task<ActivityExceptionsWidgetViewModel> GetActivityExceptionsAsync(DashboardFilters filters);
}
