using System;
using System.Collections.Generic;
using System.IO;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using FireSprinklerPlugin.SprinkSnap.Core.NFPA13;

namespace FireSprinklerPlugin.SprinkSnap.Revit;

public interface IHazardClassificationParameterStorage
{
    string ParameterName { get; }

    string Read(Room room);

    void EnsureRoomParameterBinding(Document document);

    void Write(Document document, IDictionary<ElementId, string> approvedClassifications);
}

public sealed class HazardClassificationParameterStorage : IHazardClassificationParameterStorage
{
    private const string SharedParameterGroupName = "SprinkSnap Fire Protection";

    public string ParameterName => "SS_HazardClassification";

    public string Read(Room room)
    {
        Parameter parameter = room.LookupParameter(ParameterName);
        return parameter?.AsString() ?? string.Empty;
    }

    public void EnsureRoomParameterBinding(Document document)
    {
        Autodesk.Revit.ApplicationServices.Application application = document.Application;
        Category roomCategory = document.Settings.Categories.get_Item(BuiltInCategory.OST_Rooms);

        Definition existingDefinition = FindExistingProjectParameterDefinition(
            document,
            ParameterName,
            out ElementBinding existingBinding);

        if (existingDefinition != null)
        {
            if (!BindingContainsCategory(existingBinding, roomCategory))
            {
                CategorySet categorySet = CopyBoundCategories(application, existingBinding);
                categorySet.Insert(roomCategory);

                InstanceBinding binding = application.Create.NewInstanceBinding(categorySet);
                if (!document.ParameterBindings.ReInsert(existingDefinition, binding, BuiltInParameterGroup.PG_DATA))
                {
                    throw new InvalidOperationException("Unable to bind SS_HazardClassification to Revit rooms.");
                }
            }

            return;
        }

        Definition sharedParameterDefinition = GetOrCreateSharedParameterDefinition(application);
        CategorySet roomCategorySet = application.Create.NewCategorySet();
        roomCategorySet.Insert(roomCategory);

        InstanceBinding roomBinding = application.Create.NewInstanceBinding(roomCategorySet);
        bool inserted = document.ParameterBindings.Insert(
            sharedParameterDefinition,
            roomBinding,
            BuiltInParameterGroup.PG_DATA);

        if (!inserted && !document.ParameterBindings.ReInsert(sharedParameterDefinition, roomBinding, BuiltInParameterGroup.PG_DATA))
        {
            throw new InvalidOperationException("Unable to create SS_HazardClassification room parameter binding.");
        }
    }

    public void Write(Document document, IDictionary<ElementId, string> approvedClassifications)
    {
        foreach (KeyValuePair<ElementId, string> classification in approvedClassifications)
        {
            Room room = document.GetElement(classification.Key) as Room;
            if (room == null)
            {
                continue;
            }

            Parameter parameter = room.LookupParameter(ParameterName);
            if (parameter == null)
            {
                throw new InvalidOperationException("SS_HazardClassification was not available on room " + room.Number + ".");
            }

            if (parameter.IsReadOnly)
            {
                throw new InvalidOperationException("SS_HazardClassification is read-only on room " + room.Number + ".");
            }

            parameter.Set(classification.Value);
        }
    }

    private static Definition FindExistingProjectParameterDefinition(
        Document document,
        string parameterName,
        out ElementBinding binding)
    {
        DefinitionBindingMapIterator iterator = document.ParameterBindings.ForwardIterator();
        iterator.Reset();

        while (iterator.MoveNext())
        {
            Definition definition = iterator.Key;
            if (definition != null && string.Equals(definition.Name, parameterName, StringComparison.Ordinal))
            {
                binding = iterator.Current as ElementBinding;
                return definition;
            }
        }

        binding = null;
        return null;
    }

    private static bool BindingContainsCategory(ElementBinding binding, Category category)
    {
        if (binding == null || category == null)
        {
            return false;
        }

        foreach (Category boundCategory in binding.Categories)
        {
            if (boundCategory != null && boundCategory.Id.Equals(category.Id))
            {
                return true;
            }
        }

        return false;
    }

    private static CategorySet CopyBoundCategories(
        Autodesk.Revit.ApplicationServices.Application application,
        ElementBinding existingBinding)
    {
        CategorySet categorySet = application.Create.NewCategorySet();
        if (existingBinding == null)
        {
            return categorySet;
        }

        foreach (Category category in existingBinding.Categories)
        {
            if (category != null && !CategorySetContains(categorySet, category))
            {
                categorySet.Insert(category);
            }
        }

        return categorySet;
    }

    private static bool CategorySetContains(CategorySet categorySet, Category category)
    {
        foreach (Category existingCategory in categorySet)
        {
            if (existingCategory != null && existingCategory.Id.Equals(category.Id))
            {
                return true;
            }
        }

        return false;
    }

    private static Definition GetOrCreateSharedParameterDefinition(
        Autodesk.Revit.ApplicationServices.Application application)
    {
        string originalSharedParameterFile = application.SharedParametersFilename;
        bool changedSharedParameterFile = false;

        try
        {
            if (string.IsNullOrWhiteSpace(originalSharedParameterFile) || !File.Exists(originalSharedParameterFile))
            {
                string sharedParameterFile = Path.Combine(
                    Path.GetTempPath(),
                    "SprinkSnap_SharedParameters.txt");

                EnsureSharedParameterFileExists(sharedParameterFile);
                application.SharedParametersFilename = sharedParameterFile;
                changedSharedParameterFile = true;
            }

            DefinitionFile definitionFile = application.OpenSharedParameterFile();
            if (definitionFile == null)
            {
                throw new InvalidOperationException("Unable to open a Revit shared parameter file.");
            }

            DefinitionGroup definitionGroup = GetOrCreateDefinitionGroup(definitionFile, SharedParameterGroupName);
            Definition existingDefinition = FindDefinition(definitionGroup, "SS_HazardClassification");
            if (existingDefinition != null)
            {
                return existingDefinition;
            }

            ExternalDefinitionCreationOptions creationOptions =
                new ExternalDefinitionCreationOptions("SS_HazardClassification", SpecTypeId.String.Text)
                {
                    Description = "Designer-approved SprinkSnap " + Nfpa13Edition.ShortLabel + " hazard classification.",
                    UserModifiable = true,
                    Visible = true
                };

            return definitionGroup.Definitions.Create(creationOptions);
        }
        finally
        {
            if (changedSharedParameterFile)
            {
                application.SharedParametersFilename = originalSharedParameterFile;
            }
        }
    }

    private static void EnsureSharedParameterFileExists(string sharedParameterFile)
    {
        if (File.Exists(sharedParameterFile))
        {
            return;
        }

        File.WriteAllLines(
            sharedParameterFile,
            new[]
            {
                "# This is a Revit shared parameter file.",
                "# Do not edit manually.",
                "*META\tVERSION\tMINVERSION",
                "META\t2\t1",
                "*GROUP\tID\tNAME",
                "*PARAM\tGUID\tNAME\tDATATYPE\tDATACATEGORY\tGROUP\tVISIBLE\tDESCRIPTION\tUSERMODIFIABLE\tHIDEWHENNOVALUE"
            });
    }

    private static DefinitionGroup GetOrCreateDefinitionGroup(DefinitionFile definitionFile, string groupName)
    {
        foreach (DefinitionGroup group in definitionFile.Groups)
        {
            if (string.Equals(group.Name, groupName, StringComparison.Ordinal))
            {
                return group;
            }
        }

        return definitionFile.Groups.Create(groupName);
    }

    private static Definition FindDefinition(DefinitionGroup definitionGroup, string definitionName)
    {
        foreach (Definition definition in definitionGroup.Definitions)
        {
            if (definition != null && string.Equals(definition.Name, definitionName, StringComparison.Ordinal))
            {
                return definition;
            }
        }

        return null;
    }
}

