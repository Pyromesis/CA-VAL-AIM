"""Run with Blender --background --python tools/build-viewmodel-arm.py."""

from math import cos, pi, sin, tau
from pathlib import Path

import bpy


bpy.ops.wm.read_factory_settings(use_empty=True)
vertices, faces, slots = [], [], []
materials = ("Fabric", "Cuff", "Panel", "Trim", "Skin")


def vertex(x, y, z):
    # Author in hand space and convert to Blender's Z-up before exporting.
    vertices.append((x, -z, y))
    return len(vertices) - 1


def face(indices, material):
    faces.append(indices)
    slots.append(material)


def skin(rows, material, loop, band=None):
    steps = len(rows[0])
    for i, (lower, upper) in enumerate(zip(rows, rows[1:])):
        surface = band[i] if band is not None else material
        for j in range(steps if loop else steps - 1):
            next_j = (j + 1) % steps
            face((lower[j], lower[next_j], upper[next_j], upper[j]), surface)
    if loop:
        face(tuple(reversed(rows[0])), material)
        face(tuple(rows[-1]), material)


# Section positions, radial and dorsopalmar half-widths, and centreline offset.
# The radial muscle belly is offset toward the inner face and tapers toward the elbow.
profile = [
    (-.014, .021, .016, .000, .000), (.014, .021, .017, .000, .000),
    (.046, .022, .017, .000, .000), (.078, .024, .018, -.001, .000),
    (.120, .028, .021, -.005, .002), (.163, .033, .024, -.008, .003),
    (.209, .038, .027, -.011, .002), (.257, .039, .029, -.013, -.002),
    (.318, .041, .033, -.011, -.008), (.390, .038, .034, -.006, -.016),
    (.455, .034, .031, -.001, -.033), (.520, .037, .032, .004, -.055),
    (.610, .048, .041, .011, -.102), (.740, .052, .045, .019, -.167),
    (.900, .053, .046, .030, -.235),
]


def contour(z, angle, lift=0, wrinkle=0):
    for first, second in zip(profile, profile[1:]):
        if z <= second[0]:
            t = (z - first[0]) / (second[0] - first[0])
            rx, ry, cx, cy = [a + (b - a) * t for a, b in zip(first[1:], second[1:])]
            break
    # The wrist is oval. The two forearm muscle bellies flatten at the elbow.
    muscle = .0022 * sin(pi * min(1, max(0, z / .38))) * cos(3 * angle - 1.2)
    cloth = wrinkle * (sin(117 * z + 2 * cos(angle)) + .35 * sin(193 * z - angle))
    return (cx + cos(angle) * (rx + lift + muscle + cloth),
            cy + sin(angle) * (ry + lift + muscle + cloth), z)


def tube(sections, material, wrinkle=0, band=None):
    surfaces = None
    if band is not None:
        start, end, band_material = band
        surfaces = [band_material if start <= (lower[0] + upper[0]) / 2 <= end else material
                    for lower, upper in zip(sections, sections[1:])]
    skin([[vertex(*contour(z, tau * j / 40, lift, wrinkle)) for j in range(40)]
          for z, lift in sections], material, True, surfaces)


def applique(z0, z1, angle, half_width, lift, material):
    # Sewn patch follows the arm and has a raised, beveled perimeter.
    rows = []
    for length, width, rise in ((0, .69, 0), (.09, 1, .70), (.22, 1, 1),
                                (.78, 1, 1), (.91, 1, .70), (1, .69, 0)):
        z = z0 + (z1 - z0) * length
        rows.append([vertex(*contour(z, angle + half_width * width * side,
                                     lift + rise * .002 * (1 - side ** 4)))
                     for side in (-1, -.80, -.38, 0, .38, .80, 1)])
    skin(rows, material, False)


def rings(start, end, steps, lift=0):
    return [(start + (end - start) * i / steps if i < steps else end, lift)
            for i in range(steps + 1)]


# Bare forearm, with the seam buried at both ends.
tube(rings(-.014, .262, 49), 4)
# Leather gauntlet ends at the wrist. The clasp is a separate hard surface.
tube([(-.012, .0020), (-.003, .0028), (.018, .0038), (.038, .0037),
      (.054, .0034), (.060, .0026), (.064, 0)], 1)
tube([(.038, .0033), (.041, .0048), (.047, .0051), (.050, .0033)], 2)
applique(.018, .054, -.81, .36, .0054, 2)
applique(.028, .045, -.81, .11, .0075, 3)

# The oversized fold makes the sleeve termination visible in first person.
tube([(.235, 0), (.238, .003), (.242, .007), (.249, .008), (.257, .0077),
      (.263, .0054), (.267, .0072), (.273, .0072), (.278, .0045)]
     + rings(.282, .900, 68, .004), 0, .0014, band=(.238, .263, 2))
applique(.283, .356, -.91, .52, .0060, 1)
applique(.294, .345, -.91, .34, .0073, 2)
for angle in (-1.12, -.91, -.70):
    applique(.301, .321, angle, .035, .0097, 3)

mesh = bpy.data.meshes.new("Forearm")
mesh.from_pydata(vertices, [], faces)
mesh.update()
arm = bpy.data.objects.new("Forearm", mesh)
bpy.context.collection.objects.link(arm)
for name, color in zip(materials, [(.28, .34, .32), (.12, .13, .14), (.38, .40, .34), (.64, .30, .10), (.53, .32, .23)]):
    material = bpy.data.materials.new(name)
    material.diffuse_color = (*color, 1)
    mesh.materials.append(material)
for polygon, material in zip(mesh.polygons, slots):
    polygon.material_index = material
    polygon.use_smooth = len(polygon.vertices) == 4

bpy.context.view_layer.objects.active = arm
arm.select_set(True)
output = Path(__file__).resolve().parents[1] / "CaValAim" / "assets" / "weapons" / "viewmodel_forearm.glb"
bpy.ops.export_scene.gltf(filepath=str(output), export_format="GLB", use_selection=True, export_yup=True)
