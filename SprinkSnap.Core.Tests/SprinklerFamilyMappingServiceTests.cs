using FireSprinklerPlugin.SprinkSnap.Core.Mapping;
using FireSprinklerPlugin.SprinkSnap.Core.Models;
using Xunit;

namespace FireSprinklerPlugin.SprinkSnap.Core.Tests;

public sealed class SprinklerFamilyMappingServiceTests
{
    [Fact]
    public void GetLoadedRevitSymbolOptions_IncludesScannedSymbolsNotInCatalog()
    {
        List<SprinklerFamilyInfo> catalog = new List<SprinklerFamilyInfo>
        {
            new SprinklerFamilyInfo
            {
                ListedFamilyId = "Viking:VK302:Pendent:K5.6",
                Manufacturer = "Viking",
                Model = "VK302",
                FamilyName = "QR Pendent K5.6",
                RevitTypeName = "VK302 QR Pendent K5.6"
            }
        };

        List<LoadedRevitSymbolOption> scannedSymbols = new List<LoadedRevitSymbolOption>
        {
            new LoadedRevitSymbolOption
            {
                RevitFamilySymbolId = "9001",
                RevitFamilyName = "TY-FRB",
                RevitTypeName = "Upright",
                DisplayName = "TY-FRB : Upright"
            },
            new LoadedRevitSymbolOption
            {
                RevitFamilySymbolId = "9002",
                RevitFamilyName = "Custom Head",
                RevitTypeName = "Type 1",
                DisplayName = "Custom Head : Type 1"
            }
        };

        IList<LoadedRevitSymbolOption> options = SprinklerFamilyMappingService.GetLoadedRevitSymbolOptions(
            catalog,
            scannedSymbols);

        Assert.Equal(3, options.Count);
        Assert.Contains(options, option => option.RevitFamilySymbolId == "9001");
        Assert.Contains(options, option => option.RevitFamilySymbolId == "9002");
    }

    [Fact]
    public void GetLoadedRevitSymbolOptions_DeduplicatesCatalogAndScannedSymbols()
    {
        List<SprinklerFamilyInfo> catalog = new List<SprinklerFamilyInfo>
        {
            new SprinklerFamilyInfo
            {
                ListedFamilyId = "Viking:VK302:Pendent:K5.6",
                Manufacturer = "Viking",
                Model = "VK302",
                FamilyName = "QR Pendent K5.6",
                RevitFamilyName = "VK302",
                RevitTypeName = "Pendent",
                RevitFamilySymbolId = "1001",
                IsLoadedInProject = true
            }
        };

        List<LoadedRevitSymbolOption> scannedSymbols = new List<LoadedRevitSymbolOption>
        {
            new LoadedRevitSymbolOption
            {
                RevitFamilySymbolId = "1001",
                RevitFamilyName = "VK302",
                RevitTypeName = "Pendent",
                DisplayName = "VK302 : Pendent"
            },
            new LoadedRevitSymbolOption
            {
                RevitFamilySymbolId = "1002",
                RevitFamilyName = "TY-FRB",
                RevitTypeName = "Pendent",
                DisplayName = "TY-FRB : Pendent"
            }
        };

        IList<LoadedRevitSymbolOption> options = SprinklerFamilyMappingService.GetLoadedRevitSymbolOptions(
            catalog,
            scannedSymbols);

        Assert.Equal(3, options.Count);
        Assert.Equal(2, options.Count(option => !string.IsNullOrWhiteSpace(option.RevitFamilySymbolId)));
    }
}
