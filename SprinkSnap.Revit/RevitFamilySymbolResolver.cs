using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.Revit.DB;
using FireSprinklerPlugin.SprinkSnap.Core;
using FireSprinklerPlugin.SprinkSnap.Core.Models;

namespace FireSprinklerPlugin.SprinkSnap.Revit;

public static class RevitFamilySymbolResolver
{
    public static FamilySymbol ResolveSymbol(Document document, SprinklerFamilyInfo familyInfo)
    {
        if (document == null || familyInfo == null)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(familyInfo.RevitFamilySymbolId)
            && int.TryParse(familyInfo.RevitFamilySymbolId, NumberStyles.Integer, CultureInfo.InvariantCulture, out int symbolId))
        {
            FamilySymbol symbolById = document.GetElement(new ElementId(symbolId)) as FamilySymbol;
            if (symbolById != null)
            {
                return symbolById;
            }
        }

        IEnumerable<FamilySymbol> symbols = new FilteredElementCollector(document)
            .OfCategory(BuiltInCategory.OST_Sprinklers)
            .WhereElementIsElementType()
            .OfType<FamilySymbol>();

        return symbols.FirstOrDefault(symbol =>
                string.Equals(symbol.FamilyName, familyInfo.RevitFamilyName, StringComparison.OrdinalIgnoreCase)
                && string.Equals(symbol.Name, familyInfo.RevitTypeName, StringComparison.OrdinalIgnoreCase))
            ?? symbols.FirstOrDefault(symbol =>
                string.Equals(symbol.FamilyName, familyInfo.FamilyName, StringComparison.OrdinalIgnoreCase)
                && string.Equals(symbol.Name, familyInfo.RevitTypeName, StringComparison.OrdinalIgnoreCase));
    }

    public static SprinklerFamilyInfo ResolveFamilyForRoom(
        RoomInfo room,
        IEnumerable<SprinklerFamilyInfo> catalog)
    {
        List<SprinklerFamilyInfo> families = catalog?.ToList() ?? new List<SprinklerFamilyInfo>();
        if (families.Count == 0)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(room.SelectedSprinklerFamilyName))
        {
            SprinklerFamilyInfo byDisplayName = families.FirstOrDefault(family =>
                string.Equals(family.DisplayName, room.SelectedSprinklerFamilyName, StringComparison.OrdinalIgnoreCase));
            if (byDisplayName != null)
            {
                return byDisplayName;
            }

            SprinklerFamilyInfo byFamilyName = families.FirstOrDefault(family =>
                string.Equals(family.FamilyName, room.SelectedSprinklerFamilyName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(family.RevitFamilyName, room.SelectedSprinklerFamilyName, StringComparison.OrdinalIgnoreCase));
            if (byFamilyName != null)
            {
                return byFamilyName;
            }
        }

        if (!string.IsNullOrWhiteSpace(room.AutoSelectedSprinklerName))
        {
            return families.FirstOrDefault(family =>
                string.Equals(family.DisplayName, room.AutoSelectedSprinklerName, StringComparison.OrdinalIgnoreCase));
        }

        return families.FirstOrDefault(family => family.IsLoadedInProject) ?? families.FirstOrDefault();
    }
}
