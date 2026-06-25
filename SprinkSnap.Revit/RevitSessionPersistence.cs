using System;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.ExtensibleStorage;
using FireSprinklerPlugin.SprinkSnap.Core.Models;
using FireSprinklerPlugin.SprinkSnap.Core.Persistence;

namespace FireSprinklerPlugin.SprinkSnap.Revit;

public static class RevitSessionPersistence
{
    private static readonly Guid SchemaGuid = new Guid("8F4E2A1B-3C9D-4E5F-A6B7-8C9D0E1F2A3B");

    private const string SchemaName = "SprinkSnapSession_v1";

    private const string SessionJsonField = "SessionJson";

    private const string SchemaVersionField = "SchemaVersion";

    public static SprinkSnapSessionSnapshot TryLoad(Document document)
    {
        if (document == null)
        {
            return null;
        }

        Schema schema = Schema.Lookup(SchemaGuid);
        if (schema == null)
        {
            return null;
        }

        Entity entity = document.ProjectInformation.GetEntity(schema);
        if (!entity.IsValid())
        {
            return null;
        }

        string json = entity.Get<string>(SessionJsonField);
        return SprinkSnapSessionPersistenceService.Deserialize(json);
    }

    public static void Save(Document document, SprinkSnapProjectState projectState)
    {
        if (document == null || projectState == null)
        {
            return;
        }

        SprinkSnapSessionSnapshot snapshot = SprinkSnapSessionPersistenceService.CreateSnapshot(
            projectState,
            RevitDocumentKey.Create(document));
        string json = SprinkSnapSessionPersistenceService.Serialize(snapshot);
        if (string.IsNullOrWhiteSpace(json))
        {
            return;
        }

        using (Transaction transaction = new Transaction(document, "Save SprinkSnap Session"))
        {
            transaction.Start();
            Schema schema = GetOrCreateSchema();
            Entity entity = document.ProjectInformation.GetEntity(schema);
            if (!entity.IsValid())
            {
                entity = new Entity(schema);
            }

            entity.Set(SessionJsonField, json);
            entity.Set(SchemaVersionField, SprinkSnapSessionSnapshot.CurrentSchemaVersion);
            document.ProjectInformation.SetEntity(entity);
            transaction.Commit();
        }
    }

    private static Schema GetOrCreateSchema()
    {
        Schema existingSchema = Schema.Lookup(SchemaGuid);
        if (existingSchema != null)
        {
            return existingSchema;
        }

        SchemaBuilder builder = new SchemaBuilder(SchemaGuid);
        builder.SetSchemaName(SchemaName);
        builder.SetReadAccessLevel(AccessLevel.Public);
        builder.SetWriteAccessLevel(AccessLevel.Public);
        builder.AddSimpleField(SessionJsonField, typeof(string));
        builder.AddSimpleField(SchemaVersionField, typeof(int));
        return builder.Finish();
    }
}
