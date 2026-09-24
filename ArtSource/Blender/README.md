# Urban building source

UrbanResidentialA.blend contains the first-party building created through Blender MCP: a measured 10 × 8 metre, three-storey brick residence with framed windows on four elevations, concrete courses, supported parapets, entrance canopy and downpipes.

The combined FBX is Assets/OpenSimCity/Content/Art/UrbanBuildings/UrbanResidentialA.fbx (19,488 vertices and seven material slots before Unity import). Editable Blender source stays outside Unity Assets. Poly Haven brick_wall_001 maps and provenance are under Assets/ThirdParty/PolyHaven.

UrbanProductionSet.blend contains ten measured production candidates created through Blender MCP: three residential, three mixed-use commercial, three industrial and one utility/power building. Their combined FBX exports use matching names under `Assets/OpenSimCity/Content/Art/UrbanBuildings`. Surface inputs are Poly Haven `brick_wall_001`, `concrete_wall_003` and `factory_wall`; every asset retains a local provenance record.

Blender review rendering is verified. Explicit URP material mapping, scale integration, LOD and Unity runtime verification remain required.
