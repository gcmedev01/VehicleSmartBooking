# .NET 8 Upgrade Report

## Project target framework modifications

| Project name                                   | Old Target Framework | New Target Framework | Commits                 |
|:-----------------------------------------------|:--------------------:|:--------------------:|-------------------------|
| VehicleSmartBooking\VehicleSmartBooking.csproj | net9.0               | net8.0               | a864bc75; 7b7c189d      |

## NuGet Packages

| Package Name                             | Old Version | New Version | Commit Id |
|:-----------------------------------------|:-----------:|:-----------:|-----------|
| Microsoft.EntityFrameworkCore.SqlServer  | 9.0.0       | 8.0.7       | unknown   |
| Microsoft.EntityFrameworkCore.Tools      | 9.0.0       | 8.0.7       | unknown   |
| Microsoft.AspNetCore.Authentication.Cookies | 2.3.9    |             | unknown   |

## Project feature upgrades

### VehicleSmartBooking\VehicleSmartBooking.csproj

- Removed `MapStaticAssets` and `.WithStaticAssets()` from `Program.cs` to align with .NET 8.

## All commits

| Commit ID | Description                                                                 |
|:----------|:-----------------------------------------------------------------------------|
| 85bf2f5b  | Commit upgrade plan                                                         |
| a864bc75  | Downgrade target framework in VehicleSmartBooking.csproj                     |
| 56f6b073  | Remove `WithStaticAssets` call in `Program.cs`                               |
| c8cbf7b4  | Remove `MapStaticAssets` call in `Program.cs`                                |
| 7b7c189d  | Store final changes for step `Upgrade VehicleSmartBooking\VehicleSmartBooking.csproj` |
| unknown   | chore: align packages and publish profile with net8                          |

## Next steps

- Install the .NET 8 Hosting Bundle on the IIS server (includes AspNetCoreModuleV2).
- Verify the IIS app pool is set to **No Managed Code**.
- Restart IIS after installing the hosting bundle.

## Costs

- Token usage (input/output): unavailable
- Cost: unavailable
