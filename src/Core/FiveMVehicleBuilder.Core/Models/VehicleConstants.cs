using FiveMVehicleBuilder.Core.Services;

namespace FiveMVehicleBuilder.Core.Models;

/// <summary>
/// Constants for GTA vehicle classes.
/// For organization types, use Customization.OrganizationTypes instead.
/// </summary>
public static class VehicleConstants
{
    /// <summary>
    /// Namen der 22 GTA-Fahrzeugklassen (Index = Klassen-ID)
    /// </summary>
    public static readonly string[] VehicleClasses =
    [
        "Kompakt",         // 0 - Compacts
        "Limousine",       // 1 - Sedans
        "SUV",             // 2 - SUVs
        "Coupé",           // 3 - Coupes
        "Muscle",          // 4 - Muscle
        "Sport Klassik",   // 5 - Sports Classics
        "Sport",           // 6 - Sports
        "Super",           // 7 - Super
        "Motorrad",        // 8 - Motorcycles
        "Offroad",         // 9 - Off-road
        "Industrie",       // 10 - Industrial
        "Utility",         // 11 - Utility
        "Van",             // 12 - Vans
        "Fahrrad",         // 13 - Cycles
        "Boot",            // 14 - Boats
        "Helikopter",      // 15 - Helicopters
        "Flugzeug",        // 16 - Planes
        "Dienst",          // 17 - Service
        "Einsatz",         // 18 - Emergency
        "Militär",         // 19 - Military
        "Gewerbe",         // 20 - Commercial
        "Zug"              // 21 - Trains
    ];

    /// <summary>
    /// Mapping von vehicles.meta vehicleClass-Werten zu numerischen IDs
    /// </summary>
    public static readonly Dictionary<string, int> VehicleClassMapping = new(StringComparer.OrdinalIgnoreCase)
    {
        { "VC_COMPACT", 0 },
        { "VC_SEDAN", 1 },
        { "VC_SUV", 2 },
        { "VC_COUPE", 3 },
        { "VC_MUSCLE", 4 },
        { "VC_SPORT_CLASSIC", 5 },
        { "VC_SPORT", 6 },
        { "VC_SUPER", 7 },
        { "VC_MOTORCYCLE", 8 },
        { "VC_OFF_ROAD", 9 },
        { "VC_INDUSTRIAL", 10 },
        { "VC_UTILITY", 11 },
        { "VC_VAN", 12 },
        { "VC_CYCLE", 13 },
        { "VC_BOAT", 14 },
        { "VC_HELICOPTER", 15 },
        { "VC_PLANE", 16 },
        { "VC_SERVICE", 17 },
        { "VC_EMERGENCY", 18 },
        { "VC_MILITARY", 19 },
        { "VC_COMMERCIAL", 20 },
        { "VC_RAIL", 21 }
    };

    /// <summary>
    /// Available organization types for service vehicles.
    /// Delegates to Customization.OrganizationTypes provider.
    /// </summary>
    public static IReadOnlyList<string> OrgTypes =>
        Customization.OrganizationTypes.GetOrganizationTypes();

    /// <summary>
    /// Categories for organization types.
    /// Delegates to Customization.OrganizationTypes provider.
    /// </summary>
    public static IReadOnlyDictionary<string, string[]> OrgCategories =>
        Customization.OrganizationTypes.GetAllCategories();
}
