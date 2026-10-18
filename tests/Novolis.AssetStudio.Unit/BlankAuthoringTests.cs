using Novolis.AssetStudio.Authoring;
using Novolis.Visual.Compile;
using Novolis.Visual.Definitions;

namespace Novolis.AssetStudio.Unit;

public sealed class BlankAuthoringTests
{
    [Test]
    public async Task Blank_material_is_generic_and_editable()
    {
        var catalog = new AssetStudioCatalog(
            VisualCompiler.CreateDefaultRegistry());
        var document = AssetStudioBlankDocumentFactory.CreateMaterialDocument(
            catalog: catalog);
        var session = AssetStudioAuthoringSession.Create(document, catalog);

        await Assert.That(document.DocumentKind)
            .IsEqualTo(AssetStudioDocumentKind.Material);
        await Assert.That(document.Material).IsNotNull();
        await Assert.That(
                AssetStudioAuthoringValidator.Validate(
                    document,
                    catalog.Registry).HasErrors)
            .IsFalse();

        var result = session.SetMaterialControl(
            "roughness",
            new ScalarValue(0.2));

        await Assert.That(result.Succeeded).IsTrue();
        await Assert.That(result.Session.Document.Revision)
            .IsGreaterThan(document.Revision);
        await Assert.That(
                result.Session.Document.Material!.FindParameter("roughness")!
                    .DefaultValue)
            .IsTypeOf<ScalarValue>();
    }

    [Test]
    public async Task Invalid_connection_is_rejected_without_revision_change()
    {
        var document = AssetStudioBlankDocumentFactory.CreateMaterialDocument();
        var session = AssetStudioAuthoringSession.Create(document);
        var result = session.Connect(
            document.Recipe.Nodes[0].Id,
            new PortId("material"),
            document.Recipe.Nodes[1].Id,
            new PortId("left"));

        await Assert.That(result.Applied).IsFalse();
        await Assert.That(result.Session.Document.Revision)
            .IsEqualTo(document.Revision);
        await Assert.That(result.Diagnostics.HasErrors).IsTrue();
    }

    [Test]
    public async Task Blank_asset_has_geometry_and_assignable_slot()
    {
        var asset = AssetStudioBlankDocumentFactory.CreateBlankAsset();
        var document = AssetStudioDocument.CreateAsset(asset);
        var session = AssetStudioAuthoringSession.Create(document);

        await Assert.That(document.DocumentKind)
            .IsEqualTo(AssetStudioDocumentKind.Asset);
        await Assert.That(asset.Components).Count().IsGreaterThan(0);
        await Assert.That(asset.MaterialSlots).Count().IsEqualTo(1);
        await Assert.That(session.Diagnostics.HasErrors).IsFalse();

        var result = session.AssignMaterial("surface", "material.custom");

        await Assert.That(result.Succeeded).IsTrue();
        await Assert.That(
                result.Session.Document.Asset!.FindSlot("surface")!.MaterialId)
            .IsEqualTo("material.custom");
    }
}
