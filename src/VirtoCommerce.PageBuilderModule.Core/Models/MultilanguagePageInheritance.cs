namespace VirtoCommerce.PageBuilderModule.Core.Models;

/// <summary>
/// Inheritance of <see cref="MultilanguagePage"/> settings by its language versions.
/// A language version always stores the effective value of every inheritable field, so existing readers
/// (search filters, indexing, export, content settings) keep working unchanged. <see cref="GroupedPageBuilderPage.OverriddenFields"/>
/// lists the fields the language sets itself; every other field is overwritten from the root.
/// </summary>
public static class MultilanguagePageInheritance
{
    public const string Permalink = nameof(GroupedPageBuilderPage.Permalink);
    public const string Visibility = nameof(GroupedPageBuilderPage.Visibility);
    public const string UserGroups = nameof(GroupedPageBuilderPage.UserGroups);
    public const string OrganizationId = nameof(GroupedPageBuilderPage.OrganizationId);
    public const string StartDate = nameof(GroupedPageBuilderPage.StartDate);
    public const string EndDate = nameof(GroupedPageBuilderPage.EndDate);

    private const char Separator = ',';

    public static IReadOnlyList<string> InheritableFields { get; } =
        [Permalink, Visibility, UserGroups, OrganizationId, StartDate, EndDate];

    public static ISet<string> GetOverriddenFields(GroupedPageBuilderPage group)
    {
        var fields = group.OverriddenFields?.Split(Separator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];

        return fields
            .Select(Normalize)
            .Where(x => x != null)
            .ToHashSet(StringComparer.Ordinal);
    }

    public static bool IsOverridden(GroupedPageBuilderPage group, string field)
    {
        return GetOverriddenFields(group).Contains(field);
    }

    /// <summary>
    /// Stores the mask in the canonical order. An empty mask is stored as an empty string, never null:
    /// null means "not supplied" for a save and keeps the stored mask.
    /// </summary>
    public static void SetOverriddenFields(GroupedPageBuilderPage group, IEnumerable<string> fields)
    {
        var normalized = fields
            .Select(Normalize)
            .Where(x => x != null)
            .ToHashSet(StringComparer.Ordinal);

        group.OverriddenFields = string.Join(Separator, InheritableFields.Where(normalized.Contains));
    }

    /// <summary>
    /// Inheritable fields whose value in the language version differs from the root.
    /// </summary>
    public static IList<string> GetDifferingFields(MultilanguagePage root, GroupedPageBuilderPage group)
    {
        return InheritableFields
            .Where(field => !AreEqual(GetRootValue(root, field), GetGroupValue(group, field)))
            .ToList();
    }

    /// <summary>
    /// Copies the shared name and every non-overridden field from the root into the language version.
    /// </summary>
    /// <returns><c>true</c> when the language version changed.</returns>
    public static bool Apply(MultilanguagePage root, GroupedPageBuilderPage group)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(group);

        var changed = false;

        if (group.Name != root.Name)
        {
            group.Name = root.Name;
            changed = true;
        }

        var overridden = GetOverriddenFields(group);

        foreach (var field in InheritableFields.Where(x => !overridden.Contains(x)))
        {
            var value = GetRootValue(root, field);
            if (!AreEqual(value, GetGroupValue(group, field)))
            {
                SetGroupValue(group, field, value);
                changed = true;
            }
        }

        return changed;
    }

    /// <summary>
    /// Gives the language version the root values of the given fields, overridden or not, so they become inherited again.
    /// </summary>
    /// <returns><c>true</c> when the language version changed.</returns>
    public static bool Reset(MultilanguagePage root, GroupedPageBuilderPage group, IEnumerable<string> fields)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(group);

        var changed = false;

        foreach (var field in fields.Select(Normalize).Where(x => x != null).Distinct())
        {
            var value = GetRootValue(root, field);
            if (!AreEqual(value, GetGroupValue(group, field)))
            {
                SetGroupValue(group, field, value);
                changed = true;
            }
        }

        return changed;
    }

    // Clients send an empty string for a cleared text field, the root may store null: both mean "no value".
    private static bool AreEqual(object left, object right)
    {
        if (left is null or string && right is null or string)
        {
            return string.Equals((string)left ?? string.Empty, (string)right ?? string.Empty, StringComparison.Ordinal);
        }

        return Equals(left, right);
    }

    private static string Normalize(string field)
    {
        return InheritableFields.FirstOrDefault(x => string.Equals(x, field, StringComparison.OrdinalIgnoreCase));
    }

    private static object GetRootValue(MultilanguagePage root, string field)
    {
        return field switch
        {
            Permalink => root.Permalink,
            Visibility => root.Visibility,
            UserGroups => root.UserGroups,
            OrganizationId => root.OrganizationId,
            StartDate => root.StartDate,
            EndDate => root.EndDate,
            _ => throw new ArgumentOutOfRangeException(nameof(field), field, null),
        };
    }

    private static object GetGroupValue(GroupedPageBuilderPage group, string field)
    {
        return field switch
        {
            Permalink => group.Permalink,
            Visibility => group.Visibility,
            UserGroups => group.UserGroups,
            OrganizationId => group.OrganizationId,
            StartDate => group.StartDate,
            EndDate => group.EndDate,
            _ => throw new ArgumentOutOfRangeException(nameof(field), field, null),
        };
    }

    private static void SetGroupValue(GroupedPageBuilderPage group, string field, object value)
    {
        switch (field)
        {
            case Permalink:
                group.Permalink = (string)value;
                break;
            case Visibility:
                group.Visibility = (bool)value;
                break;
            case UserGroups:
                group.UserGroups = (string)value;
                break;
            case OrganizationId:
                group.OrganizationId = (string)value;
                break;
            case StartDate:
                group.StartDate = (DateTime?)value;
                break;
            case EndDate:
                group.EndDate = (DateTime?)value;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(field), field, null);
        }
    }
}
