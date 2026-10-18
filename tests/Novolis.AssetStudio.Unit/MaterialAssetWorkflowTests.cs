using Novolis.AssetStudio.Authoring;
using Novolis.Rendering.Materials;
using Novolis.Visual.Adapters.Rendering;
using Novolis.Visual.Assets;
using Novolis.Visual.Compile;
using Novolis.Visual.Definitions;
using Novolis.Visual.Evaluation;
using Novolis.Visual.Materials;
using Novolis.Visual.Presets;

namespace Novolis.AssetStudio.Unit;

public sealed class MaterialAssetWorkflowTests
{
    [Test]
    public async Task Built_in_templates_compile_as_no_code_material_and_asset_graphs()
    {
        var catalog = VisualPresetCatalog.CreateDefault();

        await Assert.That(catalog.Templates).Count().IsEqualTo(4);

        foreach (var template in catalog.Templates)
        {
            foreach (var material in template.Materials)
            {
                var materialCompilation =
                    VisualMaterialCompiler.CompileMaterial(
                        material,
                        options: new VisualCompileOptions(strict: true));
                await Assert.That(materialCompilation.Success)
                    .IsTrue();
                var materialValue = VisualEvaluator.EvaluateOutput(
                    materialCompilation,
                    "material");
                await Assert.That(materialValue.Kind)
                    .IsEqualTo(VisualValueKind.Material);
            }

            var assetCompilation = VisualMaterialCompiler.CompileAsset(
                template.Asset,
                options: new VisualCompileOptions(strict: true));
            await Assert.That(assetCompilation.Success)
                .IsTrue();
        }
    }

    [Test]
    public async Task Material_instances_override_and_reset_exposed_controls()
    {
        var template = VisualPresetCatalog.CreateDefault()
            .Find("glass")!;
        var graph = template.Materials.Single();
        var session = new AssetStudioMaterialSession(graph);
        var original = session.ResolvedRecipe.SemanticFingerprint();
        var parameter = graph.Parameters.Single(
            candidate => candidate.Id == "glass.ior");

        var changed = session.SetOverride(
            parameter.Id,
            new ScalarValue(1.7));
        await Assert.That(changed.Fingerprint).IsNotEqualTo(original);
        await Assert.That(changed.Instance.Overrides).Count().IsEqualTo(1);

        var reset = changed.ResetOverride(parameter.Id);
        await Assert.That(reset.Fingerprint).IsEqualTo(original);
        await Assert.That(reset.Instance.Overrides).Count().IsEqualTo(0);
    }

    [Test]
    public async Task Material_and_asset_canonical_documents_round_trip()
    {
        var template = VisualPresetCatalog.CreateDefault()
            .Find("orbital-reusable-rocket")!;
        var material = template.Materials.First();
        var materialJson = MaterialGraphSerializer.Serialize(material);
        var materialRoundTrip = MaterialGraphSerializer.Deserialize(materialJson);
        await Assert.That(
                MaterialGraphSerializer.Serialize(materialRoundTrip))
            .IsEqualTo(materialJson);

        var assetJson = AssetRecipeSerializer.Serialize(template.Asset);
        var assetRoundTrip = AssetRecipeSerializer.Deserialize(assetJson);
        await Assert.That(AssetRecipeSerializer.Serialize(assetRoundTrip))
            .IsEqualTo(assetJson);
    }

    [Test]
    public async Task Asset_instances_override_shape_and_text_controls_without_code()
    {
        var template = VisualPresetCatalog.CreateDefault()
            .Find("neon-sign")!;
        var session = new AssetStudioAssetSession(template.Asset);
        var original = session.Fingerprint;

        var changed = session.SetOverride(
            "sign.text",
            new TextValue("ORBITAL"));

        await Assert.That(changed.Fingerprint).IsNotEqualTo(original);
        await Assert.That(changed.Instance.Resolve("sign.text"))
            .IsEqualTo(new TextValue("ORBITAL"));
        await Assert.That(changed.Compile(
                new VisualCompileOptions(strict: true))
                .Success)
            .IsTrue();
    }

    [Test]
    public async Task Text_values_round_trip_in_canonical_recipe_json()
    {
        var value = new TextValue("NOVOLIS");
        var json = VisualRecipeSerializer.SerializeValue(value);

        await Assert.That(
                VisualRecipeSerializer.DeserializeValue(json))
            .IsEqualTo(value);
    }

    [Test]
    public async Task Asset_geometry_lowers_from_component_metadata_without_template_switches()
    {
        var catalog = VisualPresetCatalog.CreateDefault();

        foreach (var template in catalog.Templates)
        {
            var materialCompilation = VisualMaterialCompiler.CompileMaterial(
                template.Materials.First(),
                options: new VisualCompileOptions(strict: true));
            var materialValue = VisualEvaluator.EvaluateOutput(
                materialCompilation,
                "material");
            var lowered = RenderingMaterialAdapter.LowerMaterial(
                (MaterialValue)materialValue);
            var material = lowered.Material ??
                           new StandardMaterial
                           {
                               BaseColor = new System.Numerics.Vector3(
                                   0.5f,
                                   0.5f,
                                   0.5f),
                           };
            var materialMap = template.Asset.MaterialSlots.ToDictionary(
                slot => slot.MaterialId,
                _ => material,
                StringComparer.Ordinal);

            var preview = VisualAssetSceneAdapter.Convert(
                template.Asset,
                instance: null,
                materialMap,
                material);

            await Assert.That(preview.Rendering.Scene).IsNotNull();
            await Assert.That(preview.Rendering.Scene!.Meshes.Count)
                .IsGreaterThan(0);
            await Assert.That(preview.Diagnostics.Any(diagnostic =>
                    diagnostic.Code == "VRA501"))
                .IsFalse();
        }
    }

    [Test]
    public async Task Asset_geometry_components_keep_distinct_material_slot_assignments()
    {
        var blank = AssetStudioBlankDocumentFactory.CreateBlankAsset();
        var source = blank.Components
            .Single(component => component.Kind == AssetComponentKind.Geometry)
            .Source;
        var accentGeometry = new AssetGeometryDefinition(
            "geometry.accent",
            AssetGeometryPrimitive.Sphere,
            size: [0.5, 0.5, 0.5],
            position: [2, 0, 0]);
        var asset = new AssetRecipe(
            blank.AssetId,
            blank.DisplayName,
            blank.Graph,
            blank.Components.Append(
                new AssetComponent(
                    "accent",
                    "Accent",
                    AssetComponentKind.Geometry,
                    source,
                    materialSlotId: "accent",
                    geometry: accentGeometry)),
            blank.MaterialSlots.Append(
                new AssetMaterialSlot(
                    "accent",
                    "Accent",
                    "material.accent")));
        var surface = new StandardMaterial
        {
            BaseColor = new System.Numerics.Vector3(0.1f, 0.2f, 0.3f),
        };
        var accent = new StandardMaterial
        {
            BaseColor = new System.Numerics.Vector3(0.8f, 0.7f, 0.6f),
        };

        var preview = VisualAssetSceneAdapter.Convert(
            asset,
            instance: null,
            new Dictionary<string, Novolis.Rendering.Materials.IMaterial>
            {
                ["material.default"] = surface,
                ["material.accent"] = accent,
            },
            surface);

        await Assert.That(preview.Rendering.Meshes).Count().IsEqualTo(2);
        var colors = preview.Rendering.Meshes
            .Select(mesh => ((StandardMaterial)mesh.Mesh!.Material).BaseColor)
            .ToArray();
        await Assert.That(colors).Contains(surface.BaseColor);
        await Assert.That(colors).Contains(accent.BaseColor);
    }
}
