namespace FiveMVehicleBuilder.Core.Services;

/// <summary>
/// Central customization registry for providers.
/// Set custom providers at application startup to override defaults.
/// </summary>
public static class Customization
{
    private static IOrganizationTypeProvider? _organizationTypeProvider;
    private static IExportFormatProvider? _exportFormatProvider;

    /// <summary>
    /// Gets the organization type provider.
    /// Returns default provider if no custom provider is set.
    /// </summary>
    public static IOrganizationTypeProvider OrganizationTypes =>
        _organizationTypeProvider ??= new DefaultOrganizationTypeProvider();

    /// <summary>
    /// Gets the export format provider.
    /// Returns default provider if no custom provider is set.
    /// </summary>
    public static IExportFormatProvider ExportFormats =>
        _exportFormatProvider ??= new DefaultExportFormatProvider();

    /// <summary>
    /// Sets a custom organization type provider.
    /// Call this at application startup.
    /// </summary>
    public static void SetOrganizationTypeProvider(IOrganizationTypeProvider provider) =>
        _organizationTypeProvider = provider;

    /// <summary>
    /// Sets a custom export format provider.
    /// Call this at application startup.
    /// </summary>
    public static void SetExportFormatProvider(IExportFormatProvider provider) =>
        _exportFormatProvider = provider;

    /// <summary>
    /// Resets all providers to defaults.
    /// Useful for testing.
    /// </summary>
    public static void Reset()
    {
        _organizationTypeProvider = null;
        _exportFormatProvider = null;
    }
}
