# `water.xml` & `texconv.exe` Reference

## 1. Verified `water.xml` Block Schemas

Root element is `<WaterData>`. Bounds use GTA V world XY coordinates (`minX`, `maxX`, `minY`, `maxY`):

### `<WaterQuads>`
```xml
<Item>
  <minX value="-1592" />
  <maxX value="-1304" />
  <minY value="-1744" />
  <maxY value="-1624" />
  <Type value="0" />
  <IsInvisible value="false" />
  <HasLimitedDepth value="false" />
  <z value="0.0" />
  <a1 value="26" />
  <a2 value="26" />
  <a3 value="26" />
  <a4 value="26" />
  <NoStencil value="false" />
</Item>
```

### `<CalmingQuads>`
```xml
<Item>
  <minX value="1752" />
  <maxX value="2076" />
  <minY value="216" />
  <maxY value="800" />
  <fDampening value="0.05" />
</Item>
```

### `<WaveQuads>`
```xml
<Item>
  <minX value="1664" />
  <maxX value="1988" />
  <minY value="-120" />
  <maxY value="132" />
  <Amplitude value="1.6" />
  <XDirection value="-0.603208" />
  <YDirection value="-0.797584" />
</Item>
```

## 2. Verified `texconv.exe` Options

From `DirectXTex texconv --help`:
- `-ft <filetype>` — output type (`png`, `dds`, etc.)
- `-f <format>` — pixel format (`DXT1`, `DXT5`, `BC7_UNORM`, etc.)
- `-w <n>` / `-h <n>` — resize output dimensions (use power-of-two sizes for mipmapped GTA DDS)
- `-m <n>` — mipmap levels (`0` for full chain)
- `-l` — force lowercase output filenames (matches Sollumz / FiveM lowercase dictionary stems)
- `-y` — overwrite existing output files
- `-o <directory>` — target output folder
- `-srgbi` / `-srgbo` — explicit sRGB input/output control when converting linear normal/spec maps vs color albedo
