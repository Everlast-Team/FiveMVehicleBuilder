namespace FiveMVehicleBuilder.Core.Services;

/// <summary>
/// Provides organization types and categories for service vehicles.
/// Implement this interface to add custom organization types.
/// </summary>
public interface IOrganizationTypeProvider
{
    /// <summary>
    /// Gets available organization types (e.g., "police", "medical", "fire").
    /// </summary>
    IReadOnlyList<string> GetOrganizationTypes();

    /// <summary>
    /// Gets categories for a specific organization type.
    /// </summary>
    /// <param name="orgType">The organization type.</param>
    /// <returns>List of categories, or empty list if not found.</returns>
    IReadOnlyList<string> GetCategoriesForType(string orgType);

    /// <summary>
    /// Gets all organization types with their categories.
    /// </summary>
    IReadOnlyDictionary<string, string[]> GetAllCategories();
}

/// <summary>
/// Default implementation with no organization types.
/// Override this in custom implementations to add project-specific types.
/// </summary>
public class DefaultOrganizationTypeProvider : IOrganizationTypeProvider
{
    public virtual IReadOnlyList<string> GetOrganizationTypes() => [];

    public virtual IReadOnlyList<string> GetCategoriesForType(string orgType) => [];

    public virtual IReadOnlyDictionary<string, string[]> GetAllCategories() =>
        new Dictionary<string, string[]>();
}
