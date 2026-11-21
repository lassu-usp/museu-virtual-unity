"""Generate offline numeral outlines; Unity authors the solid meshes from this data.

Requires matplotlib, fonttools and shapely >= 2.1. The DejaVu font license is
copied alongside the generated outlines. No Python dependency is needed in Unity.
"""
import json
import shutil
from pathlib import Path

from fontTools.ttLib import TTFont
from matplotlib.font_manager import FontProperties, findfont
from matplotlib.textpath import TextPath
from shapely import constrained_delaunay_triangles
from shapely.geometry import GeometryCollection, Polygon
from shapely.geometry.polygon import orient

ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / "Assets/Museu/Art/Typography"
font_path = Path(findfont("DejaVu Sans"))
font = TTFont(font_path)
properties = FontProperties(fname=font_path)
outlines = {}
for digit in "0123456789":
    shape = GeometryCollection()
    for contour in TextPath((0, 0), digit, size=1000, prop=properties).to_polygons():
        shape = shape.symmetric_difference(Polygon(contour))
    assert shape.is_valid and shape.geom_type == "Polygon", digit
    # About 1.6 mm maximum contour deviation at the authored 82 cm numeral height.
    outlines[digit] = orient(shape.simplify(1.5, preserve_topology=True), sign=1)

bottom = min(p.bounds[1] for p in outlines.values())
height = max(p.bounds[3] for p in outlines.values()) - bottom
glyphs = []
for digit, polygon in outlines.items():
    points, lookup = [], {}

    def index(point):
        key = tuple(round(float(v), 6) for v in point)
        if key not in lookup:
            lookup[key] = len(points) // 2
            points.extend([round(key[0] / height, 7), round((key[1] - bottom) / height, 7)])
        return lookup[key]

    rings = [{"indices": [index(p) for p in list(r.coords)[:-1]]}
             for r in [polygon.exterior, *polygon.interiors]]
    triangles = []
    tessellation = constrained_delaunay_triangles(polygon)
    assert abs(sum(t.area for t in tessellation.geoms) - polygon.area) < 1e-6
    for triangle in tessellation.geoms:
        triangles.extend(index(p) for p in list(orient(triangle, sign=1).exterior.coords)[:-1])
    glyph_name = font.getBestCmap()[ord(digit)]
    advance = font["hmtx"][glyph_name][0] / font["head"].unitsPerEm * 1000 / height
    glyphs.append({"digit": digit, "advance": round(advance, 7), "points": points,
                   "triangles": triangles, "rings": rings})

OUTPUT.mkdir(parents=True, exist_ok=True)
(OUTPUT / "ThresholdGlyphs.json").write_text(json.dumps({"glyphs": glyphs}, separators=(",", ":")), encoding="utf-8")
shutil.copyfile(font_path.parent / "LICENSE_DEJAVU", OUTPUT / "DejaVu-LICENSE.txt")
print(f"Generated {len(glyphs)} glyphs, {sum(len(g['triangles']) // 3 for g in glyphs)} cap triangles.")
