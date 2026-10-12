using System.CommandLine;

namespace Novolis.AssetStudio.Cli;

internal static class AssetStudioCommandLine
{
    public static RootCommand CreateRootCommand()
    {
        var jsonOption = new Option<bool>("--json")
        {
            Description = "Write one deterministic JSON document instead of human-readable output.",
            DefaultValueFactory = _ => false,
            Recursive = true,
        };
        var strictOption = new Option<bool>("--strict")
        {
            Description = "Treat warnings and target approximations as failures.",
            DefaultValueFactory = _ => false,
            Recursive = true,
        };

        var root = new RootCommand("""
            novolis-assetstudio — headless visual recipe validation, inspection,
            graph export, deterministic C# generation, baking, and builds.

            Recipes are declarative JSON. They are never executed as C#, loaded
            through reflection, or passed to a shell.
            """)
        {
            jsonOption,
            strictOption,
        };

        var validate = new Command("validate", "Validate and compile one visual recipe.");
        var validateRecipe = RecipeArgument();
        var validateTarget = TargetOption("Compilation target.", required: true);
        validate.Arguments.Add(validateRecipe);
        validate.Options.Add(validateTarget);
        validate.SetAction(async (parseResult, cancellationToken) =>
            await AssetStudioOperations.RunSafeAsync(
                    parseResult,
                    jsonOption,
                    cancellationToken,
                    () => AssetStudioOperations.Validate(
                        parseResult,
                        validateRecipe,
                        validateTarget,
                        strictOption,
                        parseResult.GetValue(jsonOption)))
                .ConfigureAwait(false));
        root.Subcommands.Add(validate);

        var inspect = new Command("inspect", "Inspect a recipe and its negotiated capabilities.");
        var inspectRecipe = RecipeArgument();
        var inspectTarget = TargetOption(
            "Target used for capability negotiation.",
            required: false,
            defaultValue: "cpu");
        var capabilitiesOption = new Option<bool>("--capabilities")
        {
            Description = "Include negotiated capability decisions in the report.",
            DefaultValueFactory = _ => false,
        };
        inspect.Arguments.Add(inspectRecipe);
        inspect.Options.Add(inspectTarget);
        inspect.Options.Add(capabilitiesOption);
        inspect.SetAction(async (parseResult, cancellationToken) =>
            await AssetStudioOperations.RunSafeAsync(
                    parseResult,
                    jsonOption,
                    cancellationToken,
                    () => AssetStudioOperations.Inspect(
                        parseResult,
                        inspectRecipe,
                        inspectTarget,
                        capabilitiesOption,
                        strictOption,
                        parseResult.GetValue(jsonOption)))
                .ConfigureAwait(false));
        root.Subcommands.Add(inspect);

        var newCommand = new Command(
            "new",
            "Create a no-code material or asset from the built-in catalog.");
        var templateCommand = new Command(
            "template",
            "Write a built-in template as canonical asset JSON.");
        var templateId = new Argument<string>("template")
        {
            Description =
                "Template id: lightsaber-blade, neon-sign, glass, or orbital-reusable-rocket.",
        };
        var templateOut = RequiredOption(
            "--out",
            "Output .novasset.json file.");
        templateCommand.Arguments.Add(templateId);
        templateCommand.Options.Add(templateOut);
        templateCommand.SetAction(async (parseResult, cancellationToken) =>
            await AssetStudioOperations.RunSafeAsync(
                    parseResult,
                    jsonOption,
                    cancellationToken,
                    () => AssetStudioOperations.NewTemplate(
                        parseResult,
                        templateId,
                        templateOut,
                        parseResult.GetValue(jsonOption)))
                .ConfigureAwait(false));
        newCommand.Subcommands.Add(templateCommand);
        root.Subcommands.Add(newCommand);

        var parameters = new Command(
            "inspect-parameters",
            "List the exposed controls in a material or asset module.");
        var parametersSource = new Argument<string>("source")
        {
            Description = "Path to canonical material or asset JSON.",
        };
        parameters.Arguments.Add(parametersSource);
        parameters.SetAction(async (parseResult, cancellationToken) =>
            await AssetStudioOperations.RunSafeAsync(
                    parseResult,
                    jsonOption,
                    cancellationToken,
                    () => AssetStudioOperations.InspectParameters(
                        parseResult,
                        parametersSource,
                        parseResult.GetValue(jsonOption)))
                .ConfigureAwait(false));
        inspect.Subcommands.Add(parameters);

        var preview = new Command(
            "preview",
            "Bake a deterministic preview thumbnail for a visual recipe.");
        var previewRecipe = RecipeArgument();
        var previewTarget = TargetOption(
            "Preview target used for capability negotiation.",
            required: false,
            defaultValue: "cpu");
        var previewOut = RequiredOption(
            "--out",
            "Output directory for preview.png and preview.json.");
        var previewWidth = new Option<int>("--width")
        {
            Description = "Preview width in pixels.",
            DefaultValueFactory = _ => 512,
        };
        var previewHeight = new Option<int>("--height")
        {
            Description = "Preview height in pixels.",
            DefaultValueFactory = _ => 512,
        };
        preview.Arguments.Add(previewRecipe);
        preview.Options.Add(previewTarget);
        preview.Options.Add(previewOut);
        preview.Options.Add(previewWidth);
        preview.Options.Add(previewHeight);
        preview.SetAction(async (parseResult, cancellationToken) =>
            await AssetStudioOperations.RunSafeAsync(
                    parseResult,
                    jsonOption,
                    cancellationToken,
                    () => AssetStudioOperations.Preview(
                        parseResult,
                        previewRecipe,
                        previewTarget,
                        previewOut,
                        previewWidth,
                        previewHeight,
                        strictOption,
                        parseResult.GetValue(jsonOption)))
                .ConfigureAwait(false));
        root.Subcommands.Add(preview);

        var export = new Command(
            "export",
            "Export a recipe through a deterministic Asset Studio profile.");
        var exportRecipe = RecipeArgument();
        var exportProfile = RequiredOption(
            "--profile",
            "Profile: definition, material-module, asset-package, preview-thumbnail, code, bake, packed, or build.");
        var exportOut = RequiredOption(
            "--out",
            "Output directory for export artifacts.");
        export.Arguments.Add(exportRecipe);
        export.Options.Add(exportProfile);
        export.Options.Add(exportOut);
        export.SetAction(async (parseResult, cancellationToken) =>
            await AssetStudioOperations.RunSafeAsync(
                    parseResult,
                    jsonOption,
                    cancellationToken,
                    () => AssetStudioOperations.Export(
                        parseResult,
                        exportRecipe,
                        exportProfile,
                        exportOut,
                        strictOption,
                        parseResult.GetValue(jsonOption)))
                .ConfigureAwait(false));
        root.Subcommands.Add(export);

        var graph = new Command("graph", "Export the semantic recipe graph as Mermaid or SVG.");
        var graphRecipe = RecipeArgument();
        var graphFormat = new Option<string>("--format")
        {
            Description = "Graph format: mermaid or svg.",
            DefaultValueFactory = _ => "mermaid",
        };
        var graphOut = RequiredOption(
            "--out",
            "Output file for the graph document.");
        graph.Arguments.Add(graphRecipe);
        graph.Options.Add(graphFormat);
        graph.Options.Add(graphOut);
        graph.SetAction(async (parseResult, cancellationToken) =>
            await AssetStudioOperations.RunSafeAsync(
                    parseResult,
                    jsonOption,
                    cancellationToken,
                    () => AssetStudioOperations.Graph(
                        parseResult,
                        graphRecipe,
                        graphFormat,
                        graphOut,
                        strictOption,
                        parseResult.GetValue(jsonOption)))
                .ConfigureAwait(false));
        root.Subcommands.Add(graph);

        var generate = new Command("generate", "Generate deterministic C# from one visual recipe.");
        var generateRecipe = RecipeArgument();
        var generateTarget = TargetOption(
            "Generation target. The headless generator accepts csharp.",
            required: false,
            defaultValue: "csharp");
        var generateOut = RequiredOption(
            "--out",
            "Output directory for generated source and its manifest.");
        var generateNamespace = new Option<string>("--namespace")
        {
            Description = "Generated C# namespace.",
            DefaultValueFactory = _ => "Generated.Visuals",
        };
        var generateTypeName = new Option<string>("--type-name")
        {
            Description = "Generated C# type name.",
            DefaultValueFactory = _ => "GeneratedVisual",
        };
        var generateMode = new Option<string>("--mode")
        {
            Description = "Code mode: builder, specialized, or embedded-payload.",
            DefaultValueFactory = _ => "specialized",
        };
        generate.Arguments.Add(generateRecipe);
        generate.Options.Add(generateTarget);
        generate.Options.Add(generateOut);
        generate.Options.Add(generateNamespace);
        generate.Options.Add(generateTypeName);
        generate.Options.Add(generateMode);
        generate.SetAction(async (parseResult, cancellationToken) =>
            await AssetStudioOperations.RunSafeAsync(
                    parseResult,
                    jsonOption,
                    cancellationToken,
                    () => AssetStudioOperations.Generate(
                        parseResult,
                        generateRecipe,
                        generateTarget,
                        generateOut,
                        generateNamespace,
                        generateTypeName,
                        generateMode,
                        strictOption,
                        parseResult.GetValue(jsonOption)))
                .ConfigureAwait(false));
        root.Subcommands.Add(generate);

        var bake = new Command("bake", "Bake deterministic CPU artifacts from one visual recipe.");
        var bakeRecipe = RecipeArgument();
        var bakeTarget = RequiredOption(
            "--target",
            "Bake target: sprite-atlas, maps, or mesh.");
        var bakeOut = RequiredOption(
            "--out",
            "Output directory for baked artifacts and its manifest.");
        var bakeWidth = new Option<int>("--width")
        {
            Description = "Bake width in pixels.",
            DefaultValueFactory = _ => 256,
        };
        var bakeHeight = new Option<int>("--height")
        {
            Description = "Bake height in pixels.",
            DefaultValueFactory = _ => 256,
        };
        var bakeSeed = new Option<int>("--seed")
        {
            Description = "Deterministic bake seed.",
            DefaultValueFactory = _ => 0,
        };
        var bakeAssets = new Option<string[]>("--asset")
        {
            Description = "Decoded image input for sprite-atlas baking. Repeat for multiple assets.",
            Arity = ArgumentArity.ZeroOrMore,
            AllowMultipleArgumentsPerToken = true,
        };
        bake.Arguments.Add(bakeRecipe);
        bake.Options.Add(bakeTarget);
        bake.Options.Add(bakeOut);
        bake.Options.Add(bakeWidth);
        bake.Options.Add(bakeHeight);
        bake.Options.Add(bakeSeed);
        bake.Options.Add(bakeAssets);
        bake.SetAction(async (parseResult, cancellationToken) =>
            await AssetStudioOperations.RunSafeAsync(
                    parseResult,
                    jsonOption,
                    cancellationToken,
                    () => AssetStudioOperations.Bake(
                        parseResult,
                        bakeRecipe,
                        bakeTarget,
                        bakeOut,
                        bakeWidth,
                        bakeHeight,
                        bakeSeed,
                        bakeAssets,
                        strictOption,
                        parseResult.GetValue(jsonOption)))
                .ConfigureAwait(false));
        root.Subcommands.Add(bake);

        var build = new Command("build", "Compile every recipe named by a safe .novassetproj manifest.");
        var buildProject = new Argument<string>("project")
        {
            Description = "Path to a .novassetproj JSON manifest.",
        };
        var buildConfiguration = new Option<string>("--configuration")
        {
            Description = "Build configuration label.",
            DefaultValueFactory = _ => "Release",
        };
        var buildOut = new Option<string?>("--out")
        {
            Description = "Optional output directory for generated source and the build report.",
        };
        build.Arguments.Add(buildProject);
        build.Options.Add(buildConfiguration);
        build.Options.Add(buildOut);
        build.SetAction(async (parseResult, cancellationToken) =>
            await AssetStudioOperations.RunSafeAsync(
                    parseResult,
                    jsonOption,
                    cancellationToken,
                    () => AssetStudioOperations.Build(
                        parseResult,
                        buildProject,
                        buildConfiguration,
                        buildOut,
                        strictOption,
                        parseResult.GetValue(jsonOption)))
                .ConfigureAwait(false));
        root.Subcommands.Add(build);

        var diff = new Command("diff", "Compare two recipes by semantic content only.");
        var diffLeft = new Argument<string>("left")
        {
            Description = "Left recipe path.",
        };
        var diffRight = new Argument<string>("right")
        {
            Description = "Right recipe path.",
        };
        var semanticOption = new Option<bool>("--semantic")
        {
            Description = "Compare canonical semantic recipe content, excluding editor layout.",
            DefaultValueFactory = _ => false,
        };
        diff.Arguments.Add(diffLeft);
        diff.Arguments.Add(diffRight);
        diff.Options.Add(semanticOption);
        diff.SetAction(async (parseResult, cancellationToken) =>
            await AssetStudioOperations.RunSafeAsync(
                    parseResult,
                    jsonOption,
                    cancellationToken,
                    () => AssetStudioOperations.Diff(
                        parseResult,
                        diffLeft,
                        diffRight,
                        semanticOption,
                        parseResult.GetValue(jsonOption)))
                .ConfigureAwait(false));
        root.Subcommands.Add(diff);

        return root;
    }

    private static Argument<string> RecipeArgument() =>
        new("recipe")
        {
            Description = "Path to a .novvisual.json recipe.",
        };

    private static Option<string> TargetOption(
        string description,
        bool required,
        string? defaultValue = null)
    {
        var option = new Option<string>("--target")
        {
            Description = description,
            Required = required,
        };
        if (defaultValue is not null)
        {
            option.DefaultValueFactory = _ => defaultValue;
        }

        return option;
    }

    private static Option<string> RequiredOption(
        string name,
        string description) =>
        new(name)
        {
            Description = description,
            Required = true,
        };
}
