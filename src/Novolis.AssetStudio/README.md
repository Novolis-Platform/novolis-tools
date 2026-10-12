# Novolis.AssetStudio

`novolis-assetstudio` is the headless PackAsTool front door for declarative
visual recipes. It validates and inspects recipes, exports deterministic
Mermaid/SVG graph documentation, generates code-only C#, bakes CPU artifacts,
builds a safe project manifest, and compares semantic recipe content.

The same tool can create and inspect the built-in no-code modules:

```powershell
novolis-assetstudio new template lightsaber-blade --out d:\out\lightsaber.novasset.json
novolis-assetstudio inspect parameters d:\out\lightsaber.novasset.json
novolis-assetstudio export d:\out\lightsaber.novasset.json --profile asset-package --out d:\out\package
```

Available templates are `lightsaber-blade`, `neon-sign`, `glass`, and
`orbital-reusable-rocket`. A visual recipe can also produce a deterministic
thumbnail without generated code:

```powershell
novolis-assetstudio preview d:\path\asset.novvisual.json --target cpu --out d:\out\preview
novolis-assetstudio validate d:\path\asset.novvisual.json --target cpu --strict
novolis-assetstudio inspect d:\path\asset.novvisual.json --capabilities --json
novolis-assetstudio graph d:\path\asset.novvisual.json --format mermaid --out d:\out\asset.mmd
novolis-assetstudio generate d:\path\asset.novvisual.json --target csharp --out d:\out
novolis-assetstudio bake d:\path\asset.novvisual.json --target sprite-atlas --asset d:\path\icon.png --out d:\out
novolis-assetstudio build d:\path\pack.novassetproj --configuration Release --strict
novolis-assetstudio diff d:\path\left.novvisual.json d:\path\right.novvisual.json --semantic
```

The command delegates recipe loading and semantic state to
`Novolis.AssetStudio.Core`, compilation to the Visual compiler, graph
projection to WorkflowEngine graph packages, and code/bake work to the
shared Visual code-generation and baking packages. Material and asset modules
are persisted with their own canonical schema serializers and can be exported
as independent package artifacts. Recipe data is
declarative: it cannot execute C#, load arbitrary assemblies, invoke a shell,
or access paths other than the explicitly supplied input and output paths.
