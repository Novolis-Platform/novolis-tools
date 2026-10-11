# Novolis Doom

`novolis-doom` extracts classic Doom-format WAD maps and resources into a
neutral JSON and PNG package for Novolis labs.

```powershell
novolis-doom extract --input C:\path\to\freedoom.wad --map E1M1 --output C:\temp\doom-e1m1 --content-profile freedoom
```

The tool parses classic `VERTEXES`, `LINEDEFS`, `SIDEDEFS`, `SECTORS`, and
`THINGS` map data, plus palettes, patches, composite textures, flats, and
sprites. Generated PNGs are validated through Asset Studio’s bounded image
importer, and sprite frames are packed with Visual Baking into a deterministic
atlas and frame manifest. Original Doom WAD data is not included; use a
legally obtained local copy and do not redistribute generated proprietary
resources.
