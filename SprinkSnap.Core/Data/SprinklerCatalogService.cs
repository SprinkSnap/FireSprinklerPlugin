using System.Collections.Generic;
using System.Linq;
using FireSprinklerPlugin.SprinkSnap.Core.Models;

namespace FireSprinklerPlugin.SprinkSnap.Core.Data;

public sealed class SprinklerCatalogService : ISprinklerFamilySelector
{
    private static readonly object SyncRoot = new object();
    private static SprinklerCatalogService defaultInstance;

    private IReadOnlyList<SprinklerFamilyInfo> families = new List<SprinklerFamilyInfo>();
    private IReadOnlyList<string> categories = new List<string>();
    private string catalogPath = string.Empty;
    private string catalogSourceKind = string.Empty;
    private string libraryName = string.Empty;
    private IList<string> loadMessages = new List<string>();

    private SprinklerCatalogService()
    {
        Reload(null);
    }

    public static SprinklerCatalogService Default
    {
        get
        {
            lock (SyncRoot)
            {
                defaultInstance ??= new SprinklerCatalogService();
                return defaultInstance;
            }
        }
    }

    public string CatalogPath => catalogPath;

    public string CatalogSourceKind => catalogSourceKind;

    public string LibraryName => libraryName;

    public IReadOnlyList<string> LoadMessages => loadMessages.ToList();

    public IReadOnlyList<string> GetCategories()
    {
        return categories;
    }

    public IReadOnlyList<SprinklerFamilyInfo> GetAvailableFamilies()
    {
        return families;
    }

    public SprinklerCatalogLoadResult Reload(string optionalCatalogPath)
    {
        lock (SyncRoot)
        {
            SprinklerCatalogLoadResult result = SprinklerCatalogLoader.Load(optionalCatalogPath);
            families = result.Families?.ToList() ?? new List<SprinklerFamilyInfo>();
            categories = result.Categories?.Count > 0
                ? result.Categories.ToList()
                : families.Select(family => family.Category)
                    .Where(category => !string.IsNullOrWhiteSpace(category))
                    .Distinct()
                    .OrderBy(category => category)
                    .ToList();
            catalogPath = result.SourcePath ?? string.Empty;
            catalogSourceKind = result.SourceKind ?? string.Empty;
            libraryName = result.LibraryName ?? string.Empty;
            loadMessages = result.Messages?.ToList() ?? new List<string>();
            return result;
        }
    }
}