# Builds the male student hero and exports an in-place FBX for Unity.
# Motion follows a classic pose-to-pose cycle: contact, down, passing, up,
# mirrored for the second step, with auto-clamped Bezier handles so the
# joints ease instead of stepping. The torso counters the hips, the arms
# oppose the legs, and the tie, hair, and scabbard lag the body.

import os
import bpy
import bmesh
from math import degrees, radians
from mathutils import Vector, Matrix, Euler

ROOT = r"C:\Users\User\Documents\Unity ai game\Testing 1"
FBX_PATH = os.path.join(ROOT, "Assets", "Models", "BoyHero.fbx")
PREVIEW = os.path.join(ROOT, "Tools", "boy_preview")
os.makedirs(os.path.dirname(FBX_PATH), exist_ok=True)
os.makedirs(PREVIEW, exist_ok=True)

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scene.render.fps = 30
scene.unit_settings.system = "METRIC"
scene.unit_settings.scale_length = 1.0

skinned = []
FRAME = 1


def rgba(rgb, a=1.0):
    return (rgb[0], rgb[1], rgb[2], a)


def make_mat(name, rgb, rough=0.5, metal=0.0):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    mat.diffuse_color = rgba(rgb)
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    if bsdf:
        bsdf.inputs["Base Color"].default_value = rgba(rgb)
        bsdf.inputs["Roughness"].default_value = rough
        if "Metallic" in bsdf.inputs:
            bsdf.inputs["Metallic"].default_value = metal
    return mat


HAIR = make_mat("Hair", (0.035, 0.038, 0.045), 0.55)
SKIN = make_mat("Skin", (0.93, 0.76, 0.64), 0.45)
BLAZER = make_mat("Blazer", (0.045, 0.08, 0.18), 0.42)
TRIM = make_mat("Trim", (0.93, 0.94, 0.96), 0.38)
PIPE = make_mat("Pipe", (0.35, 0.58, 0.90), 0.32)
SHIRT = make_mat("Shirt", (0.96, 0.96, 0.97), 0.42)
TIE = make_mat("Tie", (0.75, 0.10, 0.12), 0.38)
SHEATH = make_mat("Sheath", (0.025, 0.03, 0.04), 0.32)
IRIS = make_mat("Iris", (0.12, 0.20, 0.42), 0.22)
PANTS = make_mat("Pants", (0.05, 0.07, 0.15), 0.5)
CREASE = make_mat("Crease", (0.16, 0.20, 0.32), 0.42)
SHOE = make_mat("Shoe", (0.035, 0.04, 0.06), 0.22)
SOLE = make_mat("Sole", (0.14, 0.14, 0.15), 0.72)
GOLD = make_mat("Gold", (0.86, 0.68, 0.24), 0.25, 0.75)
STEEL = make_mat("Steel", (0.74, 0.78, 0.82), 0.18, 0.9)
GRIP = make_mat("Grip", (0.07, 0.11, 0.22), 0.55)
LEATHER = make_mat("Leather", (0.40, 0.22, 0.12), 0.48)
SOCK = make_mat("Sock", (0.18, 0.24, 0.40), 0.62)
EYE = make_mat("Eye", (0.95, 0.96, 0.98), 0.2)
PUPIL = make_mat("Pupil", (0.04, 0.05, 0.07), 0.25)
MOUTH = make_mat("Mouth", (0.55, 0.28, 0.28), 0.45)


def activate(obj):
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj


def polish(obj, mat, bone, bevel=0.0):
    activate(obj)
    if bevel > 0:
        mod = obj.modifiers.new("Bevel", "BEVEL")
        mod.width = bevel
        mod.segments = 2
        mod.limit_method = "ANGLE"
        mod.angle_limit = radians(35)
        bpy.ops.object.modifier_apply(modifier="Bevel")
    for poly in obj.data.polygons:
        poly.use_smooth = True
    obj.data.materials.append(mat)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    obj["bind"] = bone
    skinned.append(obj)
    return obj


def cube(name, loc, size, mat, bone, rot=(0, 0, 0), bevel=0.008):
    bpy.ops.mesh.primitive_cube_add(
        size=1,
        location=loc,
        rotation=tuple(radians(v) for v in rot),
    )
    obj = bpy.context.active_object
    obj.name = name
    obj.scale = size
    return polish(obj, mat, bone, bevel)


def uv_sphere(name, loc, scale, mat, bone, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_uv_sphere_add(
        segments=24,
        ring_count=16,
        radius=1,
        location=loc,
        rotation=tuple(radians(v) for v in rot),
    )
    obj = bpy.context.active_object
    obj.name = name
    obj.scale = scale
    return polish(obj, mat, bone, 0)


def cylinder(name, a, b, radius, mat, bone, vertices=16):
    a = Vector(a)
    b = Vector(b)
    mid = (a + b) * 0.5
    direction = b - a
    quat = direction.to_track_quat("Z", "Y")
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=vertices,
        radius=radius,
        depth=max(direction.length, 0.001),
        location=mid,
        rotation=quat.to_euler(),
    )
    obj = bpy.context.active_object
    obj.name = name
    return polish(obj, mat, bone, 0)


def cone(name, loc, rot, radius, depth, mat, bone):
    bpy.ops.mesh.primitive_cone_add(
        vertices=10,
        radius1=radius,
        depth=depth,
        location=loc,
        rotation=tuple(radians(v) for v in rot),
    )
    obj = bpy.context.active_object
    obj.name = name
    return polish(obj, mat, bone, 0)


def ring(name, loc, radius, mat, bone, rot=(90, 0, 0)):
    bpy.ops.mesh.primitive_torus_add(
        major_radius=radius,
        minor_radius=radius * 0.28,
        major_segments=14,
        minor_segments=6,
        location=loc,
        rotation=tuple(radians(v) for v in rot),
    )
    obj = bpy.context.active_object
    obj.name = name
    return polish(obj, mat, bone, 0)


def solid(name, bm, mat, bone, subdiv=1):
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    activate(obj)
    if subdiv > 0:
        mod = obj.modifiers.new("Sub", "SUBSURF")
        mod.levels = subdiv
        mod.render_levels = subdiv
        bpy.ops.object.modifier_apply(modifier="Sub")
    for poly in obj.data.polygons:
        poly.use_smooth = True
    obj.data.materials.append(mat)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    obj["bind"] = bone
    skinned.append(obj)
    return obj


def clump(name, pts, width, thick, mat, bone, subdiv=1):
    # A tapered tube along a curve. Used for hair locks and the blue piping.
    pts = [Vector(p) for p in pts]
    bm = bmesh.new()
    rings = []
    up = Vector((0, 0, 1))
    for i, p in enumerate(pts):
        if i < len(pts) - 1:
            tangent = pts[i + 1] - p
        else:
            tangent = p - pts[i - 1]
        if tangent.length < 1e-6:
            tangent = Vector((0, 0, 1))
        tangent.normalize()
        side = tangent.cross(up)
        if side.length < 1e-4:
            side = tangent.cross(Vector((0, 1, 0)))
        side.normalize()
        normal = side.cross(tangent).normalized()
        taper = 1.0 - 0.42 * (i / max(len(pts) - 1, 1))
        w = width * taper
        h = thick * taper
        rings.append([
            bm.verts.new(p + side * w),
            bm.verts.new(p + normal * h),
            bm.verts.new(p - side * w),
            bm.verts.new(p - normal * (h * 0.65)),
        ])
    for i in range(len(rings) - 1):
        a = rings[i]
        b = rings[i + 1]
        for k in range(4):
            n = (k + 1) % 4
            bm.faces.new((a[k], a[n], b[n], b[k]))
    bm.faces.new(list(rings[0]))
    bm.faces.new(list(reversed(rings[-1])))
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    return solid(name, bm, mat, bone, subdiv)


def coat_mesh():
    # Open blazer shell. The front does not close, so the shirt and tie sit in the gap.
    specs = [
        (0.97, 0.064, 0.162, 0.092, -0.058),
        (1.08, 0.056, 0.152, 0.086, -0.064),
        (1.14, 0.05, 0.148, 0.082, -0.066),
        (1.26, 0.048, 0.162, 0.086, -0.076),
        (1.38, 0.062, 0.188, 0.092, -0.092),
        (1.48, 0.098, 0.22, 0.064, -0.046),
    ]
    sections = []
    for z, opening, side, back_y, front_y in specs:
        sections.append([
            (-opening, front_y, z),
            (-side * 0.82, front_y * 0.2, z),
            (-side, back_y * 0.2, z),
            (-side * 0.5, back_y, z),
            (0.0, back_y * 1.05, z),
            (side * 0.5, back_y, z),
            (side, back_y * 0.2, z),
            (side * 0.82, front_y * 0.2, z),
            (opening, front_y, z),
        ])
    bm = bmesh.new()
    verts = [[bm.verts.new(p) for p in ring] for ring in sections]
    bm.verts.ensure_lookup_table()
    for s in range(len(verts) - 1):
        lower = verts[s]
        upper = verts[s + 1]
        for i in range(len(lower) - 1):
            face = bm.faces.new((lower[i], lower[i + 1], upper[i + 1], upper[i]))
            bm.normal_update()
            center = face.calc_center_median()
            if center.y < -0.02:
                outward = Vector((center.x * 0.3, -1.0, 0.0))
            elif abs(center.x) < 0.02:
                outward = Vector((0.0, 1.0, 0.0))
            else:
                outward = Vector((center.x, center.y, 0.0))
            if face.normal.dot(outward) < 0.0:
                face.normal_flip()
    return solid("Jacket", bm, BLAZER, "Chest", 1)


def tapered_box(name, z0, z1, w0, d0, w1, d1, y0, y1, mat, bone, bevel=0.012):
    mesh = bpy.data.meshes.new(name)
    bm = bmesh.new()
    rings = []
    for z, w, d, y in ((z0, w0, d0, y0), (z1, w1, d1, y1)):
        ring = []
        for x, yy in ((-w, y - d), (w, y - d), (w, y + d), (-w, y + d)):
            ring.append(bm.verts.new((x, yy, z)))
        rings.append(ring)
    bm.verts.ensure_lookup_table()
    bm.faces.new(rings[0])
    bm.faces.new(list(reversed(rings[1])))
    for i in range(4):
        bm.faces.new((rings[0][i], rings[0][(i + 1) % 4], rings[1][(i + 1) % 4], rings[1][i]))
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    return polish(obj, mat, bone, bevel)


# Character faces Blender -Y. +X is the character's left, where the scabbard hangs.
arm_data = bpy.data.armatures.new("BoyArmature")
arm = bpy.data.objects.new("BoyRig", arm_data)
bpy.context.collection.objects.link(arm)
activate(arm)
bpy.ops.object.mode_set(mode="EDIT")


def add_bone(name, head, tail, parent=None):
    bone = arm_data.edit_bones.new(name)
    bone.head = Vector(head)
    bone.tail = Vector(tail)
    bone.use_connect = False
    if parent:
        bone.parent = arm_data.edit_bones[parent]
    try:
        bone.align_roll(Vector((0, -1, 0)))
    except Exception:
        pass
    return bone


add_bone("Hips", (0, 0, 0.96), (0, 0, 1.08))
add_bone("Spine", (0, 0, 1.08), (0, 0, 1.28), "Hips")
add_bone("Chest", (0, 0, 1.28), (0, 0, 1.48), "Spine")
add_bone("Neck", (0, 0, 1.48), (0, -0.01, 1.56), "Chest")
add_bone("Head", (0, -0.01, 1.56), (0, -0.02, 1.76), "Neck")
add_bone("Hair", (0, 0.0, 1.72), (0, -0.02, 1.88), "Head")
add_bone("Tie", (0, -0.12, 1.40), (0, -0.14, 1.02), "Chest")
add_bone("Scabbard", (0.16, 0.04, 0.96), (0.22, 0.16, 0.30), "Hips")

add_bone("UpperArmL", (0.18, 0.0, 1.46), (0.24, 0.02, 1.18), "Chest")
add_bone("ForearmL", (0.24, 0.02, 1.18), (0.26, 0.02, 0.94), "UpperArmL")
add_bone("HandL", (0.26, 0.02, 0.94), (0.27, -0.01, 0.84), "ForearmL")
add_bone("UpperArmR", (-0.18, 0.0, 1.46), (-0.24, 0.02, 1.18), "Chest")
add_bone("ForearmR", (-0.24, 0.02, 1.18), (-0.26, 0.02, 0.94), "UpperArmR")
add_bone("HandR", (-0.26, 0.02, 0.94), (-0.27, -0.01, 0.84), "ForearmR")

add_bone("ThighL", (0.09, 0.0, 0.94), (0.10, 0.01, 0.50), "Hips")
add_bone("ShinL", (0.10, 0.01, 0.50), (0.10, 0.02, 0.08), "ThighL")
add_bone("FootL", (0.10, 0.02, 0.08), (0.10, -0.14, 0.035), "ShinL")
add_bone("ThighR", (-0.09, 0.0, 0.94), (-0.10, 0.01, 0.50), "Hips")
add_bone("ShinR", (-0.10, 0.01, 0.50), (-0.10, 0.02, 0.08), "ThighR")
add_bone("FootR", (-0.10, 0.02, 0.08), (-0.10, -0.14, 0.035), "ShinR")

bpy.ops.object.mode_set(mode="OBJECT")

# Open blazer, white shirt in the gap, red tie, blue piping. Matches the turnaround.
coat_mesh()
tapered_box("Shirt", 0.95, 1.49, 0.05, 0.014, 0.058, 0.016, -0.028, -0.034, SHIRT, "Chest", 0.004)
cylinder("CollarBand", (0, -0.012, 1.50), (0, -0.01, 1.55), 0.048, SHIRT, "Neck", 20)
cube("CollarL", (0.04, -0.09, 1.505), (0.044, 0.01, 0.04), SHIRT, "Chest", (16, 0, 28), 0.001)
cube("CollarR", (-0.04, -0.09, 1.505), (0.044, 0.01, 0.04), SHIRT, "Chest", (16, 0, -28), 0.001)
cube("TieKnot", (0, -0.078, 1.42), (0.024, 0.012, 0.028), TIE, "Chest", bevel=0.001)
tapered_box("TieBlade", 1.08, 1.39, 0.005, 0.003, 0.018, 0.005, -0.07, -0.078, TIE, "Tie", 0.0)
clump("PipeL", [(0.055, -0.07, 1.04), (0.048, -0.09, 1.22), (0.06, -0.105, 1.38), (0.09, -0.07, 1.49)], 0.009, 0.005, PIPE, "Chest", 0)
clump("PipeR", [(-0.055, -0.07, 1.04), (-0.048, -0.09, 1.22), (-0.06, -0.105, 1.38), (-0.09, -0.07, 1.49)], 0.009, 0.005, PIPE, "Chest", 0)
clump("PipeHem", [(-0.11, 0.05, 1.03), (-0.1, -0.04, 1.025), (0.0, -0.06, 1.02), (0.1, -0.04, 1.025), (0.11, 0.05, 1.03)], 0.007, 0.004, PIPE, "Chest", 0)
cube("PocketL", (0.09, -0.09, 1.22), (0.038, 0.01, 0.02), BLAZER, "Chest", bevel=0.002)
cube("PocketR", (-0.09, -0.09, 1.22), (0.038, 0.01, 0.02), BLAZER, "Chest", bevel=0.002)
cube("PocketPipeL", (0.09, -0.098, 1.228), (0.04, 0.005, 0.004), PIPE, "Chest", bevel=0.0)
cube("PocketPipeR", (-0.09, -0.098, 1.228), (0.04, 0.005, 0.004), PIPE, "Chest", bevel=0.0)
uv_sphere("ButtonHigh", (-0.042, -0.095, 1.30), (0.01, 0.006, 0.01), GOLD, "Chest")
uv_sphere("ButtonLow", (-0.038, -0.088, 1.16), (0.01, 0.006, 0.01), GOLD, "Chest")
uv_sphere("ShoulderL", (0.17, -0.01, 1.45), (0.052, 0.044, 0.038), BLAZER, "Chest")
uv_sphere("ShoulderR", (-0.17, -0.01, 1.45), (0.052, 0.044, 0.038), BLAZER, "Chest")
# One head, narrowed at the jaw, so the face is not a stack of balls.
cylinder("Neck", (0, -0.008, 1.48), (0, -0.01, 1.56), 0.05, SKIN, "Neck", 20)
head = uv_sphere("Head", (0, -0.008, 1.66), (0.092, 0.096, 0.1), SKIN, "Head")
bm = bmesh.new()
bm.from_mesh(head.data)
for vert in bm.verts:
    if vert.co.z < 1.60:
        vert.co.x *= 0.78
        if vert.co.y < 0.0:
            vert.co.y -= 0.012
    elif vert.co.z < 1.64:
        vert.co.x *= 0.9
    elif vert.co.z > 1.72:
        vert.co.x *= 1.05
bm.to_mesh(head.data)
bm.free()
uv_sphere("EarL", (0.078, 0.01, 1.64), (0.012, 0.008, 0.02), SKIN, "Head")
uv_sphere("EarR", (-0.078, 0.01, 1.64), (0.012, 0.008, 0.02), SKIN, "Head")
uv_sphere("Nose", (0, -0.1, 1.63), (0.008, 0.012, 0.01), SKIN, "Head")
# Flat eyes on the face, with an iris, not floating spheres.
uv_sphere("EyeL", (0.03, -0.096, 1.675), (0.02, 0.008, 0.016), EYE, "Head")
uv_sphere("EyeR", (-0.03, -0.096, 1.675), (0.02, 0.008, 0.016), EYE, "Head")
uv_sphere("IrisL", (0.03, -0.102, 1.674), (0.011, 0.006, 0.011), IRIS, "Head")
uv_sphere("IrisR", (-0.03, -0.102, 1.674), (0.011, 0.006, 0.011), IRIS, "Head")
uv_sphere("PupilL", (0.03, -0.106, 1.674), (0.005, 0.004, 0.005), PUPIL, "Head")
uv_sphere("PupilR", (-0.03, -0.106, 1.674), (0.005, 0.004, 0.005), PUPIL, "Head")
uv_sphere("GlintL", (0.035, -0.108, 1.68), (0.003, 0.002, 0.003), EYE, "Head")
uv_sphere("GlintR", (-0.025, -0.108, 1.68), (0.003, 0.002, 0.003), EYE, "Head")
cube("BrowL", (0.03, -0.1, 1.70), (0.026, 0.005, 0.005), HAIR, "Head", (0, 0, -8), 0.0)
cube("BrowR", (-0.03, -0.1, 1.70), (0.026, 0.005, 0.005), HAIR, "Head", (0, 0, 8), 0.0)
cube("Mouth", (0, -0.092, 1.585), (0.016, 0.004, 0.004), MOUTH, "Head", (0, 0, 0), 0.0)

# One hair volume covering the scalp, plus a few short tufts on the crown.
bpy.ops.mesh.primitive_uv_sphere_add(segments=32, ring_count=20, radius=1.0, location=(0.0, 0.012, 1.70))
hair = bpy.context.active_object
hair.name = "HairMass"
hair.scale = (0.132, 0.138, 0.096)
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
bm = bmesh.new()
bm.from_mesh(hair.data)
for vert in bm.verts:
    if vert.co.z > 1.74:
        vert.co.z += (vert.co.z - 1.72) * 0.35
    # Bangs stop on the forehead so the eyes stay visible.
    if vert.co.y < -0.04 and vert.co.z < 1.71:
        vert.co.z += 0.06
        vert.co.y += 0.03
    elif vert.co.y < 0.0 and 1.72 < vert.co.z < 1.80:
        vert.co.y -= 0.025
    if vert.co.y > 0.03:
        vert.co.z -= 0.08
        vert.co.y += 0.012
    if abs(vert.co.x) > 0.05 and vert.co.z < 1.72:
        vert.co.z -= 0.045
        vert.co.x *= 1.06
bm.to_mesh(hair.data)
bm.free()
for poly in hair.data.polygons:
    poly.use_smooth = True
hair.data.materials.append(HAIR)
hair["bind"] = "Hair"
skinned.append(hair)
clump("HairTuftA", [(0.0, -0.02, 1.78), (0.015, -0.03, 1.87)], 0.036, 0.02, HAIR, "Hair", 1)
clump("HairTuftB", [(-0.045, 0.0, 1.76), (-0.05, -0.01, 1.85)], 0.03, 0.018, HAIR, "Hair", 1)
clump("HairTuftC", [(0.045, 0.0, 1.76), (0.055, -0.02, 1.85)], 0.03, 0.018, HAIR, "Hair", 1)
clump("HairTuftD", [(0.0, 0.04, 1.76), (0.0, 0.05, 1.85)], 0.032, 0.016, HAIR, "Hair", 1)

# Slim sleeves. A blue cuff ring, then a short white shirt cuff.
cylinder("SleeveL", (0.16, -0.008, 1.45), (0.24, 0.012, 1.16), 0.058, BLAZER, "UpperArmL", 24)
cylinder("SleeveR", (-0.16, -0.008, 1.45), (-0.24, 0.012, 1.16), 0.058, BLAZER, "UpperArmR", 24)
cylinder("ForeSleeveL", (0.24, 0.012, 1.16), (0.26, 0.012, 0.95), 0.048, BLAZER, "ForearmL", 20)
cylinder("ForeSleeveR", (-0.24, 0.012, 1.16), (-0.26, 0.012, 0.95), 0.048, BLAZER, "ForearmR", 20)
cylinder("CuffPipeL", (0.258, 0.012, 0.965), (0.264, 0.01, 0.935), 0.052, PIPE, "ForearmL", 20)
cylinder("CuffPipeR", (-0.258, 0.012, 0.965), (-0.264, 0.01, 0.935), 0.052, PIPE, "ForearmR", 20)
cylinder("ShirtCuffL", (0.254, 0.01, 0.94), (0.26, 0.006, 0.905), 0.03, SHIRT, "ForearmL", 16)
cylinder("ShirtCuffR", (-0.254, 0.01, 0.94), (-0.26, 0.006, 0.905), 0.03, SHIRT, "ForearmR", 16)
uv_sphere("HandMeshL", (0.262, 0.0, 0.875), (0.026, 0.014, 0.036), SKIN, "HandL")
uv_sphere("HandMeshR", (-0.262, 0.0, 0.875), (0.026, 0.014, 0.036), SKIN, "HandR")
uv_sphere("ThumbL", (0.242, -0.012, 0.89), (0.01, 0.008, 0.014), SKIN, "HandL")
uv_sphere("ThumbR", (-0.242, -0.012, 0.89), (0.01, 0.008, 0.014), SKIN, "HandR")

# Trousers under the jacket, a belt only across the back, and short black shoes.
cube("HipFill", (0, 0.012, 0.94), (0.26, 0.15, 0.12), PANTS, "Hips", bevel=0.02)
cube("BeltBack", (0, 0.07, 1.0), (0.2, 0.035, 0.016), LEATHER, "Hips", bevel=0.002)
uv_sphere("Buckle", (0.12, 0.03, 1.0), (0.016, 0.01, 0.014), GOLD, "Hips")
cube("ShoeL", (0.10, -0.04, 0.045), (0.06, 0.15, 0.04), SHOE, "FootL", bevel=0.008)
cube("ShoeR", (-0.10, -0.04, 0.045), (0.06, 0.15, 0.04), SHOE, "FootR", bevel=0.008)
cube("ToeL", (0.10, -0.115, 0.038), (0.044, 0.04, 0.024), SHOE, "FootL", bevel=0.006)
cube("ToeR", (-0.10, -0.115, 0.038), (0.044, 0.04, 0.024), SHOE, "FootR", bevel=0.006)
cube("ShoeSoleL", (0.10, -0.04, 0.024), (0.056, 0.17, 0.01), SOLE, "FootL", bevel=0.001)
cube("ShoeSoleR", (-0.10, -0.04, 0.024), (0.056, 0.17, 0.01), SOLE, "FootR", bevel=0.001)
cube("ShoeHeelL", (0.10, 0.028, 0.038), (0.046, 0.04, 0.026), SHOE, "FootL", bevel=0.003)
cube("ShoeHeelR", (-0.10, 0.028, 0.038), (0.046, 0.04, 0.026), SHOE, "FootR", bevel=0.003)

# Dark sheath on the left hip, with a silver guard at the belt.
cylinder("Sheath", (0.16, 0.04, 0.9), (0.2, 0.13, 0.36), 0.016, SHEATH, "Scabbard")
cylinder("Chape", (0.198, 0.125, 0.4), (0.205, 0.14, 0.33), 0.02, STEEL, "Scabbard")
uv_sphere("ChapeTip", (0.208, 0.145, 0.32), (0.014, 0.012, 0.014), STEEL, "Scabbard")
cylinder("Throat", (0.158, 0.03, 0.93), (0.168, 0.05, 0.88), 0.022, STEEL, "Scabbard")
cube("SwordGuard", (0.15, 0.0, 0.97), (0.07, 0.01, 0.016), STEEL, "Scabbard", (12, 0, 0), 0.001)
cylinder("SwordGrip", (0.145, -0.008, 1.02), (0.155, 0.008, 0.975), 0.012, GRIP, "Scabbard")
uv_sphere("Pommel", (0.14, -0.012, 1.035), (0.016, 0.012, 0.016), STEEL, "Scabbard")

for obj in skinned:
    mw = obj.matrix_world.copy()
    obj.parent = arm
    obj.matrix_world = mw
    group = obj.vertex_groups.new(name=obj["bind"])
    group.add([vert.index for vert in obj.data.vertices], 1.0, "REPLACE")
    modifier = obj.modifiers.new("Armature", "ARMATURE")
    modifier.object = arm


def bare_cylinder(name, a, b, radius):
    a = Vector(a)
    b = Vector(b)
    mid = (a + b) * 0.5
    direction = b - a
    quat = direction.to_track_quat("Z", "Y")
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=20,
        radius=radius,
        depth=max(direction.length, 0.001),
        location=mid,
        rotation=quat.to_euler(),
    )
    obj = bpy.context.active_object
    obj.name = name
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    return obj


def bare_sphere(name, loc, radius):
    bpy.ops.mesh.primitive_uv_sphere_add(
        segments=20, ring_count=12, radius=radius, location=loc
    )
    obj = bpy.context.active_object
    obj.name = name
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    return obj


def build_leg(name, hip, knee, ankle, thigh_name, shin_name):
    # One mesh: thigh tube, a ball at the hinge, then the calf.
    # Everything inside the ball stays on the thigh, so the calf
    # swings out of the joint instead of breaking away from it.
    parts = [
        bare_cylinder(name + "Thigh", hip, knee, 0.07),
        bare_sphere(name + "Joint", knee, 0.084),
        bare_sphere(name + "Cap", (knee[0], knee[1] - 0.03, knee[2]), 0.038),
        bare_cylinder(name + "Calf", knee, ankle, 0.052),
        bare_sphere(name + "Ankle", ankle, 0.056),
    ]
    activate(parts[0])
    for part in parts:
        part.select_set(True)
    bpy.ops.object.join()
    leg = bpy.context.active_object
    leg.name = name
    leg.data.materials.clear()
    leg.data.materials.append(PANTS)
    knee_v = Vector(knee)
    thigh_group = leg.vertex_groups.new(name=thigh_name)
    shin_group = leg.vertex_groups.new(name=shin_name)
    for vert in leg.data.vertices:
        inside_joint = (vert.co - knee_v).length <= 0.078
        if inside_joint or vert.co.z >= knee_v.z - 0.01:
            thigh_group.add([vert.index], 1.0, "REPLACE")
        else:
            shin_group.add([vert.index], 1.0, "REPLACE")
    leg.parent = arm
    modifier = leg.modifiers.new("Armature", "ARMATURE")
    modifier.object = arm
    skinned.append(leg)
    return leg


build_leg("LegL", (0.09, 0.0, 0.94), (0.10, 0.01, 0.50), (0.10, 0.02, 0.07), "ThighL", "ShinL")
build_leg("LegR", (-0.09, 0.0, 0.94), (-0.10, 0.01, 0.50), (-0.10, 0.02, 0.07), "ThighR", "ShinR")

bpy.context.view_layer.update()

# Pick, for each bone, the local axis that actually swings the limb forward or out.
# Forward means the free end moves toward -Y. Side means it moves outward.
SIDE_DIR = {
    "UpperArmL": Vector((1, 0, 0)),
    "ForearmL": Vector((1, 0, 0)),
    "HandL": Vector((1, 0, 0)),
    "ThighL": Vector((1, 0, 0)),
    "ShinL": Vector((1, 0, 0)),
    "FootL": Vector((1, 0, 0)),
    "UpperArmR": Vector((-1, 0, 0)),
    "ForearmR": Vector((-1, 0, 0)),
    "HandR": Vector((-1, 0, 0)),
    "ThighR": Vector((-1, 0, 0)),
    "ShinR": Vector((-1, 0, 0)),
    "FootR": Vector((-1, 0, 0)),
    "Scabbard": Vector((1, 0, 0)),
}
CAL = {}
FORWARD = Vector((0, -1, 0))


def best_axis(rest, tail, direction, banned):
    winner = None
    for axis in range(3):
        if axis == banned:
            continue
        axis_vec = rest.col[axis].normalized()
        for sign in (1.0, -1.0):
            rotated = Matrix.Rotation(radians(25) * sign, 3, axis_vec) @ tail
            score = (rotated - tail).dot(direction)
            if winner is None or score > winner[0]:
                winner = (score, axis, sign)
    return winner


for pose_bone in arm.pose.bones:
    rest = pose_bone.bone.matrix_local.to_3x3()
    tail = rest.col[1].normalized()
    side = SIDE_DIR.get(pose_bone.name, Vector((1, 0, 0)))
    forward_pick = best_axis(rest, tail, FORWARD, None)
    side_pick = best_axis(rest, tail, side, forward_pick[1])
    used = {forward_pick[1], side_pick[1]}
    twist_axis = ({0, 1, 2} - used).pop()
    CAL[pose_bone.name] = (forward_pick[1], forward_pick[2], side_pick[1], side_pick[2], twist_axis)
    print(
        f"{pose_bone.name}: fwd {forward_pick[1]}*{forward_pick[2]:.0f} "
        f"side {side_pick[1]}*{side_pick[2]:.0f} twist {twist_axis}"
    )

BONES = [pose_bone.name for pose_bone in arm.pose.bones]
MID = {"Hips", "Spine", "Chest", "Neck", "Head", "Hair", "Tie", "Scabbard"}


def apply_bone(name, forward=0.0, side=0.0, twist=0.0, bob=None, sway=None):
    pose_bone = arm.pose.bones[name]
    pose_bone.rotation_mode = "XYZ"
    axis_f, sign_f, axis_s, sign_s, twist_axis = CAL[name]
    euler = [0.0, 0.0, 0.0]
    euler[axis_f] += radians(forward) * sign_f
    euler[axis_s] += radians(side) * sign_s
    euler[twist_axis] += radians(twist)
    pose_bone.rotation_euler = Euler(euler, "XYZ")
    pose_bone.keyframe_insert(data_path="rotation_euler", frame=FRAME)
    if name == "Hips":
        amount = 0.0 if bob is None else bob
        lateral = 0.0 if sway is None else sway
        rest = pose_bone.bone.matrix_local.to_3x3()
        pose_bone.location = rest.inverted() @ Vector((lateral, 0.0, amount))
        pose_bone.keyframe_insert(data_path="location", frame=FRAME)


def key_all(frame, pose):
    global FRAME
    FRAME = frame
    merged = {name: {} for name in BONES}
    merged.update(pose)
    for name, values in merged.items():
        apply_bone(name, **values)


def mirror(pose):
    flipped = {}
    for name, values in pose.items():
        if name.endswith("L"):
            other = name[:-1] + "R"
        elif name.endswith("R"):
            other = name[:-1] + "L"
        else:
            other = name
        copied = dict(values)
        if name in MID:
            if "twist" in copied:
                copied["twist"] = -copied["twist"]
            if "side" in copied:
                copied["side"] = -copied["side"]
            if "sway" in copied:
                copied["sway"] = -copied["sway"]
        flipped[other] = copied
    return flipped


def smooth_action(action):
    curves = []
    slot = arm.animation_data.action_slot if arm.animation_data else None
    if hasattr(action, "fcurves"):
        curves.extend(list(action.fcurves))
    if hasattr(action, "layers"):
        for layer in action.layers:
            for strip in getattr(layer, "strips", []):
                bags = []
                if hasattr(strip, "channelbags"):
                    bags.extend(list(strip.channelbags))
                if slot is not None and hasattr(strip, "channelbag"):
                    try:
                        bags.append(strip.channelbag(slot))
                    except TypeError:
                        try:
                            bags.append(strip.channelbag(slot, ensure=False))
                        except Exception:
                            pass
                for bag in bags:
                    if bag is not None and hasattr(bag, "fcurves"):
                        curves.extend(list(bag.fcurves))
    count = 0
    seen = set()
    for curve in curves:
        if curve is None or id(curve) in seen:
            continue
        seen.add(id(curve))
        count += 1
        for key in curve.keyframe_points:
            key.interpolation = "BEZIER"
            key.handle_left_type = "AUTO_CLAMPED"
            key.handle_right_type = "AUTO_CLAMPED"
    print(f"smoothed {action.name}: {count} curves")


def begin_action(name):
    action = bpy.data.actions.new(name)
    action.use_fake_user = True
    anim = arm.animation_data_create()
    anim.action = action
    if getattr(anim, "action_slot", None) is None and hasattr(action, "slots"):
        try:
            anim.action_slot = action.slots.new("OBJECT", name)
        except Exception as exc:
            print("slot", name, exc)
    return action


def finish_action(action, end):
    smooth_action(action)
    if hasattr(action, "use_frame_range"):
        action.use_frame_range = True
        action.frame_start = 1
        action.frame_end = end
    print(f"action {action.name} frames 1-{end}")
    return action


def build_action(name, keys, end):
    action = begin_action(name)
    for frame, pose in keys:
        key_all(frame, pose)
    return finish_action(action, end)


# Contact, recoil, passing, and up. Left leg leads on the first half.
contact_l = {
    "Hips": {"forward": 4, "twist": -8, "bob": -0.02},
    "Spine": {"forward": 5, "twist": 7},
    "Chest": {"forward": 3, "twist": 4},
    "Neck": {"twist": -2},
    "Head": {"forward": -3, "twist": -3},
    "Hair": {"forward": -8, "twist": -4},
    "Tie": {"forward": 12, "twist": 8},
    "Scabbard": {"forward": 6, "twist": 5},
    "ThighL": {"forward": 32, "side": 3},
    "ShinL": {"forward": 8},
    "FootL": {"forward": -14},
    "ThighR": {"forward": -24, "side": 2},
    "ShinR": {"forward": 16},
    "FootR": {"forward": 16},
    "UpperArmL": {"forward": -28, "side": 8},
    "ForearmL": {"forward": 30},
    "HandL": {"forward": 12},
    "UpperArmR": {"forward": 28, "side": 8},
    "ForearmR": {"forward": 18},
    "HandR": {"forward": 8},
}
down_l = {
    "Hips": {"forward": 8, "twist": -4, "bob": -0.055},
    "Spine": {"forward": 10, "twist": 4},
    "Chest": {"forward": 6, "twist": 2},
    "Head": {"forward": -6, "twist": -2},
    "Hair": {"forward": -4},
    "Tie": {"forward": 16, "twist": 4},
    "Scabbard": {"forward": 8},
    "ThighL": {"forward": 18, "side": 4},
    "ShinL": {"forward": 44},
    "FootL": {"forward": 2},
    "ThighR": {"forward": -10, "side": 2},
    "ShinR": {"forward": 32},
    "FootR": {"forward": 8},
    "UpperArmL": {"forward": -8, "side": 6},
    "ForearmL": {"forward": 22},
    "UpperArmR": {"forward": 8, "side": 6},
    "ForearmR": {"forward": 20},
}
pass_l = {
    "Hips": {"forward": 2, "twist": 3, "bob": 0.028},
    "Spine": {"forward": 3, "twist": -2},
    "Chest": {"twist": -2},
    "Head": {"forward": -2, "twist": 1},
    "Hair": {"forward": 4, "twist": 3},
    "Tie": {"forward": 4, "twist": -4},
    "ThighL": {"forward": 2, "side": 2},
    "ShinL": {"forward": 8},
    "FootL": {"forward": 4},
    "ThighR": {"forward": 16, "side": 2},
    "ShinR": {"forward": 70},
    "FootR": {"forward": -8},
    "UpperArmL": {"forward": 12, "side": 7},
    "ForearmL": {"forward": 16},
    "UpperArmR": {"forward": -12, "side": 7},
    "ForearmR": {"forward": 18},
}
up_l = {
    "Hips": {"forward": 2, "twist": 7, "bob": 0.01},
    "Spine": {"forward": 4, "twist": -5},
    "Chest": {"twist": -4},
    "Head": {"forward": -2, "twist": 2},
    "Hair": {"forward": 6, "twist": 4},
    "Tie": {"forward": -4, "twist": -6},
    "ThighL": {"forward": -16},
    "ShinL": {"forward": 24},
    "FootL": {"forward": 12},
    "ThighR": {"forward": 26, "side": 2},
    "ShinR": {"forward": 16},
    "FootR": {"forward": -10},
    "UpperArmL": {"forward": 22, "side": 8},
    "ForearmL": {"forward": 14},
    "UpperArmR": {"forward": -20, "side": 8},
    "ForearmR": {"forward": 22},
}

# Walk poses follow the reference sheet: contact, down, passing, up.
# The knee bends backward. Negative shin swing folds the calf up behind
# the knee. Positive shin swing is what kicked the foot out in front.
walk_contact = {
    "Hips": {"forward": 2, "twist": -6, "side": 4, "bob": -0.015, "sway": 0.02},
    "Spine": {"forward": 4, "twist": 8},
    "Chest": {"forward": 2, "twist": 5},
    "Neck": {"twist": -3},
    "Head": {"forward": -7, "twist": -4, "side": -4},
    "Hair": {"forward": -6, "twist": -5},
    "Tie": {"forward": 10, "twist": 6},
    "Scabbard": {"forward": 4, "twist": 3},
    "ThighL": {"forward": 26, "side": 2},
    "ShinL": {"forward": -6},
    "FootL": {"forward": -18},
    "ThighR": {"forward": -22, "side": 2},
    "ShinR": {"forward": -16},
    "FootR": {"forward": 26},
    "UpperArmL": {"forward": -22, "side": 6},
    "ForearmL": {"forward": 28},
    "HandL": {"forward": 8},
    "UpperArmR": {"forward": 22, "side": 6},
    "ForearmR": {"forward": 24},
    "HandR": {"forward": 6},
}
walk_down = {
    "Hips": {"forward": 6, "twist": -4, "side": 5, "bob": -0.055, "sway": 0.035},
    "Spine": {"forward": 8, "twist": 5},
    "Chest": {"forward": 4, "twist": 3},
    "Head": {"forward": -17, "twist": -4, "side": -5},
    "Hair": {"forward": -2},
    "Tie": {"forward": 16, "twist": 6},
    "Scabbard": {"forward": 8},
    "ThighL": {"forward": 16, "side": 2},
    "ShinL": {"forward": -34},
    "FootL": {"forward": 2},
    "ThighR": {"forward": -8, "side": 2},
    "ShinR": {"forward": -48},
    "FootR": {"forward": 10},
    "UpperArmL": {"forward": -30, "side": 8},
    "ForearmL": {"forward": 24},
    "HandL": {"forward": 6},
    "UpperArmR": {"forward": 30, "side": 8},
    "ForearmR": {"forward": 20},
    "HandR": {"forward": 6},
}
walk_pass = {
    "Hips": {"forward": 1, "twist": 2, "side": -3, "bob": 0.02, "sway": 0.015},
    "Spine": {"forward": 2, "twist": -2},
    "Chest": {"twist": -2},
    "Head": {"forward": -2, "twist": 2, "side": 3},
    "Hair": {"forward": 6, "twist": 4},
    "Tie": {"forward": 2, "twist": -6},
    "Scabbard": {"forward": -4},
    "ThighL": {"forward": 3, "side": 2},
    "ShinL": {"forward": -4},
    "FootL": {"forward": 2},
    "ThighR": {"forward": 34, "side": 2},
    "ShinR": {"forward": -68},
    "FootR": {"forward": 6},
    "UpperArmL": {"forward": 6, "side": 5},
    "ForearmL": {"forward": 18},
    "HandL": {"forward": 4},
    "UpperArmR": {"forward": -6, "side": 5},
    "ForearmR": {"forward": 20},
    "HandR": {"forward": 4},
}
walk_up = {
    "Hips": {"forward": 2, "twist": 6, "side": -2, "bob": 0.01, "sway": -0.01},
    "Spine": {"forward": 3, "twist": -6},
    "Chest": {"twist": -4},
    "Head": {"forward": -4, "twist": 4, "side": 2},
    "Hair": {"forward": 8, "twist": 5},
    "Tie": {"forward": -4, "twist": -7},
    "Scabbard": {"forward": -2},
    "ThighL": {"forward": -20, "side": 2},
    "ShinL": {"forward": -14},
    "FootL": {"forward": 24},
    "ThighR": {"forward": 32, "side": 2},
    "ShinR": {"forward": -30},
    "FootR": {"forward": -16},
    "UpperArmL": {"forward": 22, "side": 6},
    "ForearmL": {"forward": 16},
    "HandL": {"forward": 4},
    "UpperArmR": {"forward": -20, "side": 6},
    "ForearmR": {"forward": 22},
    "HandR": {"forward": 6},
}
walk_keys = [
    (1, walk_contact),
    (4, walk_down),
    (7, walk_pass),
    (10, walk_up),
    (13, mirror(walk_contact)),
    (16, mirror(walk_down)),
    (19, mirror(walk_pass)),
    (22, mirror(walk_up)),
    (25, walk_contact),
]
build_action("Walk", walk_keys, 24)


def as_run(pose):
    biased = {}
    for name, values in pose.items():
        item = dict(values)
        if name == "Spine":
            item["forward"] = item.get("forward", 0) + 12
        elif name == "Chest":
            item["forward"] = item.get("forward", 0) + 8
        elif name == "Hips":
            item["forward"] = item.get("forward", 0) + 10
            item["bob"] = item.get("bob", 0) * 1.4 - 0.02
        elif name == "Head":
            item["forward"] = item.get("forward", 0) - 14
        elif name == "Hair":
            item["forward"] = item.get("forward", 0) - 10
        elif name.startswith("Thigh"):
            item["forward"] = item.get("forward", 0) * 1.3
        elif name.startswith("Shin"):
            item["forward"] = item.get("forward", 0) * 1.05 + 10
        elif name.startswith("UpperArm"):
            item["forward"] = item.get("forward", 0) * 0.75 - 22
        elif name.startswith("Forearm"):
            item["forward"] = item.get("forward", 0) + 22
        biased[name] = item
    return biased


# Run poses match the five-frame reference: push, lift, flight, reach, other foot.
# The body leans forward. The head stays aimed along the run, and the calf
# folds behind the knee instead of kicking out.
run_push = {
    "Hips": {"forward": 14, "twist": -8, "side": 4, "bob": -0.02, "sway": 0.03},
    "Spine": {"forward": 10, "twist": 10},
    "Chest": {"forward": 6, "twist": 6},
    "Neck": {"twist": -4},
    "Head": {"forward": -18, "twist": -4, "side": -4},
    "Hair": {"forward": -8, "twist": -6},
    "Tie": {"forward": -16, "twist": 8},
    "Scabbard": {"forward": -22, "twist": 6},
    "ThighL": {"forward": 52, "side": 3},
    "ShinL": {"forward": -52},
    "FootL": {"forward": -10},
    "ThighR": {"forward": -40, "side": 2},
    "ShinR": {"forward": -16},
    "FootR": {"forward": 30},
    "UpperArmL": {"forward": -46, "side": 10},
    "ForearmL": {"forward": 68},
    "HandL": {"forward": 10},
    "UpperArmR": {"forward": 48, "side": 8},
    "ForearmR": {"forward": 62},
    "HandR": {"forward": 8},
}
run_lift = {
    "Hips": {"forward": 16, "twist": -4, "side": 2, "bob": -0.008, "sway": 0.015},
    "Spine": {"forward": 11, "twist": 5},
    "Chest": {"forward": 7, "twist": 3},
    "Neck": {"twist": -2},
    "Head": {"forward": -22, "twist": -2, "side": -2},
    "Hair": {"forward": -12, "twist": -4},
    "Tie": {"forward": -22},
    "Scabbard": {"forward": -28},
    "ThighL": {"forward": 46, "side": 2},
    "ShinL": {"forward": -28},
    "FootL": {"forward": -8},
    "ThighR": {"forward": -22, "side": 2},
    "ShinR": {"forward": -48},
    "FootR": {"forward": 14},
    "UpperArmL": {"forward": -18, "side": 8},
    "ForearmL": {"forward": 60},
    "UpperArmR": {"forward": 20, "side": 8},
    "ForearmR": {"forward": 58},
}
run_flight = {
    "Hips": {"forward": 18, "twist": 2, "bob": 0.02},
    "Spine": {"forward": 12, "twist": -2},
    "Chest": {"forward": 8, "twist": -2},
    "Head": {"forward": -26, "twist": 2},
    "Hair": {"forward": -16, "twist": 4},
    "Tie": {"forward": -28, "twist": -6},
    "Scabbard": {"forward": -36},
    "ThighL": {"forward": 58, "side": 2},
    "ShinL": {"forward": -16},
    "FootL": {"forward": -18},
    "ThighR": {"forward": -50, "side": 2},
    "ShinR": {"forward": -8},
    "FootR": {"forward": 26},
    "UpperArmL": {"forward": -62, "side": 12},
    "ForearmL": {"forward": 52},
    "HandL": {"forward": 8},
    "UpperArmR": {"forward": 64, "side": 10},
    "ForearmR": {"forward": 48},
    "HandR": {"forward": 8},
}
run_reach = {
    "Hips": {"forward": 15, "twist": 6, "side": -3, "bob": 0.004, "sway": -0.01},
    "Spine": {"forward": 10, "twist": -6},
    "Chest": {"forward": 6, "twist": -4},
    "Neck": {"twist": 2},
    "Head": {"forward": -20, "twist": 2, "side": 3},
    "Hair": {"forward": -10, "twist": 5},
    "Tie": {"forward": -18},
    "Scabbard": {"forward": -24},
    "ThighL": {"forward": 34, "side": 2},
    "ShinL": {"forward": -42},
    "FootL": {"forward": -6},
    "ThighR": {"forward": -14, "side": 2},
    "ShinR": {"forward": -36},
    "FootR": {"forward": 10},
    "UpperArmL": {"forward": -8, "side": 8},
    "ForearmL": {"forward": 55},
    "UpperArmR": {"forward": 10, "side": 8},
    "ForearmR": {"forward": 58},
}
run_keys = [
    (1, run_push),
    (3, run_lift),
    (5, run_flight),
    (7, run_reach),
    (9, mirror(run_push)),
    (11, mirror(run_lift)),
    (13, mirror(run_flight)),
    (15, mirror(run_reach)),
    (17, run_push),
]
build_action("Run", run_keys, 16)

idle_a = {
    "Chest": {"forward": 1},
    "Head": {"twist": -1},
    "UpperArmL": {"forward": 4, "side": 6},
    "UpperArmR": {"forward": 5, "side": 7},
    "ForearmL": {"forward": 8},
    "ForearmR": {"forward": 10},
    "ThighL": {"forward": 3},
    "ThighR": {"forward": 3},
}
idle_b = {
    "Hips": {"side": 2, "bob": 0.012},
    "Spine": {"forward": 2, "twist": 2},
    "Chest": {"forward": 4, "twist": 1},
    "Head": {"forward": -1, "twist": 1},
    "Hair": {"forward": -3, "twist": 2},
    "Tie": {"forward": 6, "twist": 3},
    "UpperArmL": {"forward": 6, "side": 7},
    "UpperArmR": {"forward": 3, "side": 6},
    "ForearmL": {"forward": 12},
    "ForearmR": {"forward": 8},
    "ThighL": {"forward": 4},
    "ThighR": {"forward": 2},
}
build_action("Idle", [(1, idle_a), (24, idle_b), (30, idle_b), (48, idle_a)], 48)

# Single jump: crouch, then a step-over. The leading knee stays up through
# the rise, the top, and the start of the fall. The other leg hangs straight.
jump_neutral = {}
jump_crouch = {
    "Hips": {"forward": 14, "bob": -0.11},
    "Spine": {"forward": 10},
    "Chest": {"forward": 4},
    "Head": {"forward": -16},
    "Hair": {"forward": 8},
    "Tie": {"forward": 14},
    "UpperArmL": {"forward": -28, "side": 10},
    "UpperArmR": {"forward": -28, "side": 10},
    "ForearmL": {"forward": 22},
    "ForearmR": {"forward": 22},
    "ThighL": {"forward": 38, "side": 3},
    "ThighR": {"forward": 38, "side": 3},
    "ShinL": {"forward": -58},
    "ShinR": {"forward": -58},
    "FootL": {"forward": 4},
    "FootR": {"forward": 4},
}
jump_takeoff = {
    "Hips": {"forward": 8, "bob": -0.03},
    "Spine": {"forward": 4},
    "Head": {"forward": -8},
    "Hair": {"forward": 4},
    "UpperArmL": {"forward": -6, "side": 16},
    "UpperArmR": {"forward": -4, "side": 14},
    "ForearmL": {"forward": 14},
    "ForearmR": {"forward": 12},
    "ThighL": {"forward": 40, "side": 2},
    "ShinL": {"forward": -42},
    "FootL": {"forward": -2},
    "ThighR": {"forward": 10},
    "ShinR": {"forward": -16},
    "FootR": {"forward": -8},
}
jump_ascent = {
    "Hips": {"forward": 6, "bob": 0.012},
    "Spine": {"forward": 2},
    "Head": {"forward": -5},
    "Hair": {"forward": -8},
    "Tie": {"forward": 8},
    "UpperArmL": {"forward": 8, "side": 6},
    "UpperArmR": {"forward": 12, "side": 8},
    "ForearmL": {"forward": 10},
    "ForearmR": {"forward": 12},
    "ThighL": {"forward": 78, "side": 3},
    "ShinL": {"forward": -82},
    "FootL": {"forward": 6},
    "ThighR": {"forward": -4},
    "ShinR": {"forward": -5},
    "FootR": {"forward": -14},
}
jump_peak = {
    "Hips": {"forward": 3, "bob": 0.016},
    "Spine": {"forward": 1},
    "Head": {"forward": -3},
    "Hair": {"forward": -10},
    "Tie": {"forward": 6},
    "UpperArmL": {"forward": 6, "side": 6},
    "UpperArmR": {"forward": 14, "side": 8},
    "ForearmL": {"forward": 8},
    "ForearmR": {"forward": 10},
    "ThighL": {"forward": 96, "side": 4},
    "ShinL": {"forward": -94},
    "FootL": {"forward": 8},
    "ThighR": {"forward": 4},
    "ShinR": {"forward": -3},
    "FootR": {"forward": -8},
}
jump_descent = {
    "Hips": {"forward": 6},
    "Spine": {"forward": 3},
    "Head": {"forward": -6},
    "Hair": {"forward": -4},
    "Tie": {"forward": 8},
    "UpperArmL": {"forward": 14, "side": 8},
    "UpperArmR": {"forward": 14, "side": 8},
    "ForearmL": {"forward": 10},
    "ForearmR": {"forward": 10},
    "ThighL": {"forward": 52, "side": 2},
    "ShinL": {"forward": -58},
    "FootL": {"forward": 4},
    "ThighR": {"forward": 10},
    "ShinR": {"forward": -14},
    "FootR": {"forward": -12},
}
jump_prepare = {
    "Hips": {"forward": 8, "bob": -0.02},
    "Spine": {"forward": 4},
    "Head": {"forward": -8},
    "UpperArmL": {"forward": 14, "side": 12},
    "UpperArmR": {"forward": 14, "side": 12},
    "ForearmL": {"forward": 12},
    "ForearmR": {"forward": 12},
    "ThighL": {"forward": 18},
    "ThighR": {"forward": 18},
    "ShinL": {"forward": -24},
    "ShinR": {"forward": -24},
    "FootL": {"forward": -12},
    "FootR": {"forward": -12},
}
jump_impact = {
    "Hips": {"forward": 16, "bob": -0.1},
    "Spine": {"forward": 8},
    "Chest": {"forward": 4},
    "Head": {"forward": -18},
    "Hair": {"forward": 10},
    "Tie": {"forward": 16},
    "UpperArmL": {"forward": 8, "side": 22},
    "UpperArmR": {"forward": 8, "side": 22},
    "ForearmL": {"forward": 18},
    "ForearmR": {"forward": 18},
    "ThighL": {"forward": 36, "side": 3},
    "ThighR": {"forward": 36, "side": 3},
    "ShinL": {"forward": -52},
    "ShinR": {"forward": -52},
    "FootL": {"forward": 2},
    "FootR": {"forward": 2},
}
jump_recover = {
    "Hips": {"forward": 6, "bob": -0.035},
    "Spine": {"forward": 3},
    "Head": {"forward": -7},
    "UpperArmL": {"forward": 4, "side": 8},
    "UpperArmR": {"forward": 4, "side": 8},
    "ForearmL": {"forward": 8},
    "ForearmR": {"forward": 8},
    "ThighL": {"forward": 16},
    "ThighR": {"forward": -6},
    "ShinL": {"forward": -18},
    "ShinR": {"forward": -8},
}
build_action("Jump", [
    (1, jump_neutral),
    (5, jump_crouch),
    (8, jump_takeoff),
    (12, jump_ascent),
    (16, jump_peak),
    (18, jump_peak),
    (21, jump_descent),
    (24, jump_prepare),
    (26, jump_impact),
    (28, jump_recover),
    (30, jump_neutral),
], 30)

# Forward flip. Hip pitch carries the whole body over: layout, tuck, then open.
flip_layout = {
    "Hips": {"forward": 38},
    "Spine": {"forward": -6},
    "Chest": {"forward": -4},
    "Head": {"forward": -12},
    "Hair": {"forward": 18},
    "Tie": {"forward": -12},
    "UpperArmL": {"forward": 168, "side": 8},
    "UpperArmR": {"forward": 158, "side": 6},
    "ForearmL": {"forward": 4},
    "ForearmR": {"forward": 6},
    "ThighL": {"forward": -6},
    "ThighR": {"forward": -6},
    "ShinL": {"forward": -3},
    "ShinR": {"forward": -3},
    "FootL": {"forward": -14},
    "FootR": {"forward": -14},
}
flip_pitch = {
    "Hips": {"forward": 95},
    "Spine": {"forward": 16},
    "Chest": {"forward": 10},
    "Head": {"forward": 4},
    "Hair": {"forward": 12},
    "Tie": {"forward": 8},
    "UpperArmL": {"forward": 100, "side": 20},
    "UpperArmR": {"forward": 96, "side": 18},
    "ForearmL": {"forward": 70},
    "ForearmR": {"forward": 70},
    "Scabbard": {"forward": -50},
    "ThighL": {"forward": 90},
    "ThighR": {"forward": 90},
    "ShinL": {"forward": -110},
    "ShinR": {"forward": -110},
    "FootL": {"forward": 6},
    "FootR": {"forward": 6},
}
flip_tuck = {
    "Hips": {"forward": 170},
    "Spine": {"forward": 12},
    "Chest": {"forward": 8},
    "Head": {"forward": 8},
    "Hair": {"forward": -8},
    "Tie": {"forward": 10},
    "Scabbard": {"forward": -80},
    "UpperArmL": {"forward": 80, "side": 24},
    "UpperArmR": {"forward": 80, "side": 24},
    "ForearmL": {"forward": 100},
    "ForearmR": {"forward": 100},
    "ThighL": {"forward": 110},
    "ThighR": {"forward": 110},
    "ShinL": {"forward": -150},
    "ShinR": {"forward": -150},
    "FootL": {"forward": 16},
    "FootR": {"forward": 16},
}
flip_open = {
    "Hips": {"forward": 275},
    "Spine": {"forward": 10},
    "Chest": {"forward": 4},
    "Head": {"forward": -6},
    "Hair": {"forward": 8},
    "UpperArmL": {"forward": 72, "side": 18},
    "UpperArmR": {"forward": 72, "side": 18},
    "ForearmL": {"forward": 18},
    "ForearmR": {"forward": 18},
    "ThighL": {"forward": 30},
    "ThighR": {"forward": 30},
    "ShinL": {"forward": -26},
    "ShinR": {"forward": -26},
    "FootL": {"forward": -6},
    "FootR": {"forward": -6},
}
flip_exit = {
    "Hips": {"forward": 350},
    "Spine": {"forward": 10},
    "Head": {"forward": -12},
    "Hair": {"forward": 6},
    "Tie": {"forward": 8},
    "UpperArmL": {"forward": 58, "side": 16},
    "UpperArmR": {"forward": 58, "side": 16},
    "ForearmL": {"forward": 14},
    "ForearmR": {"forward": 14},
    "ThighL": {"forward": 26},
    "ThighR": {"forward": 26},
    "ShinL": {"forward": -32},
    "ShinR": {"forward": -32},
}
build_action("Tuck", [
    (1, flip_layout),
    (6, flip_pitch),
    (12, flip_tuck),
    (18, flip_open),
    (24, flip_exit),
], 24)

land = {
    "Hips": {"forward": 36, "bob": -0.28},
    "Spine": {"forward": 14},
    "Chest": {"forward": 4},
    "Head": {"forward": -30},
    "Hair": {"forward": 12},
    "Tie": {"forward": 16},
    "UpperArmL": {"forward": 108, "side": 22},
    "UpperArmR": {"forward": 108, "side": 22},
    "ForearmL": {"forward": 12},
    "ForearmR": {"forward": 12},
    "ThighL": {"forward": 82, "side": 8},
    "ThighR": {"forward": 82, "side": 8},
    "ShinL": {"forward": -108},
    "ShinR": {"forward": -108},
    "FootL": {"forward": 12},
    "FootR": {"forward": 12},
}
build_action("Land", [(1, land), (8, {})], 8)
build_action(
    "LightLand",
    [(1, {"Hips": {"bob": -0.03}, "ThighL": {"forward": 16}, "ThighR": {"forward": 16}, "ShinL": {"forward": 18}, "ShinR": {"forward": 18}}), (6, {})],
    6,
)

# Dash matches the five-pose sheet. The slow half is the coil. The burst
# is the low drive with the arms trailing. The last instant thrusts both
# arms forward. Knees fold backward, the same way the walk does.
dash_coil = {
    "Hips": {"forward": 30, "bob": -0.24},
    "Spine": {"forward": 12},
    "Chest": {"forward": 6},
    "Head": {"forward": -26},
    "Hair": {"forward": 6},
    "Tie": {"forward": 14},
    "Scabbard": {"forward": -6},
    "UpperArmL": {"forward": 22, "side": 12},
    "UpperArmR": {"forward": 28, "side": 8},
    "ForearmL": {"forward": 52},
    "ForearmR": {"forward": 58},
    "ThighL": {"forward": 64, "side": 5},
    "ThighR": {"forward": 60, "side": 4},
    "ShinL": {"forward": -86},
    "ShinR": {"forward": -82},
    "FootL": {"forward": 6},
    "FootR": {"forward": 4},
}
dash_drive = {
    "Hips": {"forward": 34, "bob": -0.03},
    "Spine": {"forward": 10},
    "Chest": {"forward": 4},
    "Head": {"forward": -30},
    "Hair": {"forward": -14},
    "Tie": {"forward": -16},
    "Scabbard": {"forward": -28},
    "UpperArmL": {"forward": -52, "side": 10},
    "UpperArmR": {"forward": -58, "side": 8},
    "ForearmL": {"forward": 12},
    "ForearmR": {"forward": 10},
    "ThighL": {"forward": 40},
    "ShinL": {"forward": -26},
    "FootL": {"forward": -10},
    "ThighR": {"forward": -26},
    "ShinR": {"forward": -8},
    "FootR": {"forward": 16},
}
dash_thrust = {
    "Hips": {"forward": 22, "bob": -0.01},
    "Spine": {"forward": 6},
    "Chest": {"forward": 3},
    "Head": {"forward": -18},
    "Hair": {"forward": -8},
    "Tie": {"forward": -8},
    "Scabbard": {"forward": -12},
    "UpperArmL": {"forward": 88, "side": 14},
    "UpperArmR": {"forward": 96, "side": 10},
    "ForearmL": {"forward": 6},
    "ForearmR": {"forward": 4},
    "ThighL": {"forward": 46},
    "ShinL": {"forward": -20},
    "FootL": {"forward": -10},
    "ThighR": {"forward": -24},
    "ShinR": {"forward": -8},
    "FootR": {"forward": 14},
}
dash_rise = {
    "Hips": {"forward": 12, "bob": -0.01},
    "Spine": {"forward": 4},
    "Head": {"forward": -8},
    "Hair": {"forward": -2},
    "Tie": {"forward": 2},
    "Scabbard": {"forward": -6},
    "UpperArmL": {"forward": 16, "side": 8},
    "UpperArmR": {"forward": 58, "side": 8},
    "ForearmL": {"forward": 12},
    "ForearmR": {"forward": 8},
    "ThighL": {"forward": 14},
    "ShinL": {"forward": -14},
    "ThighR": {"forward": -16},
    "ShinR": {"forward": -16},
    "FootR": {"forward": 8},
}
# 30 frames cover the 1 second dash. 0.5s is frame 15, 0.9s is frame 27.
build_action(
    "Dash",
    [(1, dash_coil), (8, dash_coil), (15, dash_coil), (18, dash_drive), (24, dash_drive), (27, dash_thrust), (30, dash_thrust)],
    30,
)
build_action("DashRecover", [(1, dash_rise), (8, {})], 8)

slide = {
    "Hips": {"forward": -62, "bob": -0.08},
    "Spine": {"forward": -8},
    "Chest": {"forward": -6},
    "Head": {"forward": -18},
    "UpperArmL": {"forward": -20, "side": 16},
    "UpperArmR": {"forward": 48, "side": 8},
    "ForearmR": {"forward": 16},
    "ThighL": {"forward": -74},
    "ShinL": {"forward": 8},
    "FootL": {"forward": 6},
    "ThighR": {"forward": 64},
    "ShinR": {"forward": 78},
}
build_action("Slide", [(1, slide), (8, slide)], 8)
build_action("SlideRecover", [(1, slide), (8, {})], 8)

guard = {
    "Hips": {"bob": -0.05},
    "Spine": {"forward": 6},
    "Chest": {"forward": 6, "twist": -8},
    "Head": {"forward": 14, "twist": 4},
    "UpperArmR": {"forward": -128, "side": -18},
    "ForearmR": {"forward": 48},
    "HandR": {"forward": 10},
    "UpperArmL": {"forward": 18, "side": 12},
    "ForearmL": {"forward": 16},
    "ThighL": {"forward": 16, "side": 6},
    "ThighR": {"forward": 18, "side": 6},
    "ShinL": {"forward": 20},
    "ShinR": {"forward": 22},
}
guard_b = dict(guard)
guard_b = {name: dict(values) for name, values in guard.items()}
guard_b["Chest"] = {"forward": 8, "twist": -6}
guard_b["Head"] = {"forward": 12, "twist": 3}
build_action("Guard", [(1, guard), (12, guard_b), (24, guard)], 24)

# Hit 1 is a diagonal cut: the sword starts high on his right and finishes low across his left.
attack1_wind = {
    "Hips": {"twist": 14, "forward": 16, "bob": -0.04},
    "Spine": {"twist": 18, "forward": 8},
    "Chest": {"twist": 16, "forward": 4},
    "Head": {"twist": -10, "forward": -4},
    "Hair": {"forward": -8},
    "Tie": {"forward": 12},
    "Scabbard": {"forward": -10},
    "UpperArmR": {"forward": -158, "side": 46},
    "ForearmR": {"forward": 26},
    "HandR": {"forward": 8},
    "UpperArmL": {"forward": 40, "side": 16},
    "ForearmL": {"forward": 16},
    "ThighL": {"forward": 50},
    "ShinL": {"forward": -46},
    "FootL": {"forward": -8},
    "ThighR": {"forward": -24},
    "ShinR": {"forward": -16},
    "FootR": {"forward": 12},
}
attack1_hit = {
    "Hips": {"twist": -18, "forward": 22, "bob": -0.06},
    "Spine": {"twist": -20, "forward": 10},
    "Chest": {"twist": -18, "forward": 6},
    "Head": {"twist": 8, "forward": 6},
    "Hair": {"forward": 8},
    "Tie": {"forward": 6},
    "UpperArmR": {"forward": 58, "side": -62},
    "ForearmR": {"forward": 10},
    "HandR": {"forward": 4},
    "UpperArmL": {"forward": -34, "side": 18},
    "ForearmL": {"forward": 14},
    "ThighL": {"forward": 34},
    "ShinL": {"forward": -30},
    "ThighR": {"forward": -18},
    "ShinR": {"forward": -14},
}
attack1_end = {
    "Hips": {"twist": -8, "forward": 10},
    "Spine": {"twist": -10, "forward": 4},
    "Chest": {"twist": -6},
    "Head": {"twist": 4},
    "UpperArmR": {"forward": 24, "side": -34},
    "ForearmR": {"forward": 16},
    "UpperArmL": {"forward": 8, "side": 12},
    "ThighL": {"forward": 14},
    "ShinL": {"forward": -12},
    "ThighR": {"forward": -8},
    "ShinR": {"forward": -10},
}
build_action("Attack1", [(1, attack1_wind), (4, attack1_wind), (7, attack1_hit), (12, attack1_end), (16, attack1_end)], 16)

# Hit 2 stays low and sweeps across, with the torso turning through the cut.
attack2_wind = {
    "Hips": {"twist": 20, "forward": 12, "bob": -0.08},
    "Spine": {"twist": 16, "forward": 6},
    "Chest": {"twist": 14, "forward": 2},
    "Head": {"twist": -8},
    "Tie": {"forward": 8},
    "UpperArmR": {"forward": 16, "side": 74},
    "ForearmR": {"forward": 14},
    "UpperArmL": {"forward": 22, "side": 10},
    "ForearmL": {"forward": 12},
    "ThighL": {"forward": 30},
    "ShinL": {"forward": -26},
    "ThighR": {"forward": -16},
    "ShinR": {"forward": -12},
}
attack2_hit = {
    "Hips": {"twist": -22, "forward": 14, "bob": -0.05},
    "Spine": {"twist": -20, "forward": 8},
    "Chest": {"twist": -16, "forward": 4},
    "Head": {"twist": 10},
    "Hair": {"forward": 6},
    "Tie": {"forward": -6},
    "UpperArmR": {"forward": 20, "side": -76},
    "ForearmR": {"forward": 8},
    "UpperArmL": {"forward": -22, "side": 14},
    "ThighL": {"forward": 18},
    "ShinL": {"forward": -16},
    "ThighR": {"forward": -12},
    "ShinR": {"forward": -10},
}
attack2_end = {
    "Hips": {"twist": -10, "forward": 6},
    "Spine": {"twist": -8},
    "Chest": {"twist": -6},
    "UpperArmR": {"forward": 12, "side": -38},
    "ForearmR": {"forward": 14},
    "ThighL": {"forward": 8},
    "ThighR": {"forward": -6},
}
build_action("Attack2", [(1, attack2_wind), (4, attack2_wind), (7, attack2_hit), (12, attack2_end), (16, attack2_end)], 16)

# Hit 3 rises from a loaded knee into a sword held straight up.
attack3_wind = {
    "Hips": {"forward": 16, "bob": -0.1, "twist": 8},
    "Spine": {"forward": 8, "twist": 8},
    "Chest": {"twist": 6, "forward": 4},
    "Head": {"forward": 8},
    "Tie": {"forward": 10},
    "UpperArmR": {"forward": 52, "side": 30},
    "ForearmR": {"forward": 20},
    "UpperArmL": {"forward": -14, "side": 14},
    "ThighR": {"forward": 74},
    "ShinR": {"forward": -90},
    "FootR": {"forward": 8},
    "ThighL": {"forward": 14},
    "ShinL": {"forward": -18},
}
attack3_hit = {
    "Hips": {"forward": -6, "bob": 0.02, "twist": -12},
    "Spine": {"forward": -10, "twist": -12},
    "Chest": {"forward": -8, "twist": -10},
    "Head": {"forward": -16},
    "Hair": {"forward": -14},
    "Tie": {"forward": -8},
    "UpperArmR": {"forward": -136, "side": -16},
    "ForearmR": {"forward": 12},
    "UpperArmL": {"forward": 18, "side": 16},
    "ThighR": {"forward": 56},
    "ShinR": {"forward": -68},
    "ThighL": {"forward": -6},
    "ShinL": {"forward": -8},
}
attack3_end = {
    "Hips": {"forward": -2},
    "Spine": {"forward": -2},
    "Head": {"forward": -20},
    "Hair": {"forward": -10},
    "UpperArmR": {"forward": -170, "side": 2},
    "ForearmR": {"forward": 6},
    "UpperArmL": {"forward": 6, "side": 10},
    "ThighR": {"forward": 16},
    "ShinR": {"forward": -14},
    "ThighL": {"forward": 4},
    "ShinL": {"forward": -8},
}
build_action("Attack3", [(1, attack3_wind), (4, attack3_wind), (7, attack3_hit), (12, attack3_end), (16, attack3_end)], 16)

flinch = {
    "Spine": {"forward": 10, "twist": 6},
    "Chest": {"forward": 8},
    "Head": {"forward": 12},
    "UpperArmL": {"forward": 16, "side": 10},
    "UpperArmR": {"forward": 16, "side": 10},
    "ForearmL": {"forward": 28},
    "ForearmR": {"forward": 28},
}
build_action("Flinch", [(1, flinch), (6, {})], 6)

knock = {
    "Hips": {"forward": -12},
    "Spine": {"forward": -18},
    "Chest": {"forward": -14},
    "Head": {"forward": -16},
    "UpperArmL": {"forward": -130, "side": 16},
    "UpperArmR": {"forward": -130, "side": 16},
    "ForearmL": {"forward": 20},
    "ForearmR": {"forward": 20},
    "ThighL": {"forward": 18},
    "ThighR": {"forward": 18},
}
build_action("Knock", [(1, knock), (8, knock)], 8)

down = {
    "Hips": {"forward": 78, "bob": -0.55},
    "Spine": {"forward": 8},
    "Chest": {"forward": 6},
    "Head": {"forward": 10},
    "UpperArmL": {"forward": 20, "side": 16},
    "UpperArmR": {"forward": -20, "side": 10},
    "ThighL": {"forward": 10},
    "ThighR": {"forward": 18},
    "ShinL": {"forward": 8},
    "ShinR": {"forward": 14},
}
build_action("Down", [(1, down), (8, down)], 8)

brake = {
    "Hips": {"forward": 20, "bob": -0.02},
    "Spine": {"forward": 18},
    "Chest": {"forward": 12},
    "Head": {"forward": -18},
    "UpperArmL": {"forward": 36, "side": 8},
    "UpperArmR": {"forward": 36, "side": 8},
    "ForearmL": {"forward": 28},
    "ForearmR": {"forward": 28},
    "ThighL": {"forward": 24},
    "ThighR": {"forward": -16},
    "ShinL": {"forward": 20},
    "ShinR": {"forward": 28},
}
build_action("Brake", [(1, brake), (5, as_run(pass_l)), (10, {})], 10)

# Bounds so the exported height can be checked before Unity imports it.
min_z = 99.0
max_z = -99.0
for obj in skinned:
    for corner in obj.bound_box:
        world = obj.matrix_world @ Vector(corner)
        min_z = min(min_z, world.z)
        max_z = max(max_z, world.z)
print(f"HEIGHT {min_z:.3f} to {max_z:.3f}")


def assign_action(action):
    anim = arm.animation_data_create()
    anim.action = action
    if not hasattr(anim, "action_slot"):
        return
    slots = list(getattr(action, "slots", []))
    if not slots and hasattr(action, "slots"):
        try:
            slots = [action.slots.new("OBJECT", action.name)]
        except Exception as exc:
            print("slot", action.name, exc)
            return
    if slots:
        try:
            anim.action_slot = slots[0]
        except Exception as exc:
            print("assign", action.name, exc)


def bake_quaternions():
    # The FBX exporter was writing one frozen rotation per clip. Sampling the
    # posed bones and storing quaternion keys gives Unity a curve that moves.
    originals = [action for action in bpy.data.actions]
    snapshots = []
    for action in originals:
        assign_action(action)
        start = int(round(action.frame_start))
        end = int(round(action.frame_end))
        frames = []
        previous = {}
        for frame in range(start, end + 1):
            scene.frame_set(frame)
            bpy.context.view_layer.update()
            depsgraph = bpy.context.evaluated_depsgraph_get()
            posed = arm.evaluated_get(depsgraph)
            pose = {}
            for pose_bone in posed.pose.bones:
                quat = pose_bone.matrix_basis.to_quaternion()
                if pose_bone.name in previous and previous[pose_bone.name].dot(quat) < 0.0:
                    quat.negate()
                previous[pose_bone.name] = quat.copy()
                pose[pose_bone.name] = (tuple(quat), tuple(pose_bone.location))
            frames.append(pose)
        if action.name == "Walk":
            for frame in (1, 4, 7, 10, 13):
                scene.frame_set(frame)
                bpy.context.view_layer.update()
                posed_now = arm.evaluated_get(bpy.context.evaluated_depsgraph_get())
                fl = posed_now.pose.bones["FootL"].head
                fr = posed_now.pose.bones["FootR"].head
                knee = posed_now.pose.bones["ShinR"].head
                thigh = posed_now.pose.bones["ThighR"]
                shin = posed_now.pose.bones["ShinR"]
                thigh_dir = (thigh.tail - thigh.head).normalized()
                shin_dir = (shin.tail - shin.head).normalized()
                thigh_deg = degrees(thigh_dir.angle(Vector((0, 0, -1))))
                knee_deg = degrees(thigh_dir.angle(shin_dir))
                crown = (posed_now.pose.bones["Head"].tail - posed_now.pose.bones["Head"].head).normalized()
                nod = degrees(crown.angle(Vector((0, 0, 1))))
                print(
                    f"FEET {frame} L y={fl.y:.3f} z={fl.z:.3f} "
                    f"R y={fr.y:.3f} z={fr.z:.3f} kneeR y={knee.y:.3f} z={knee.z:.3f} "
                    f"thighR={thigh_deg:.0f} kneeR={knee_deg:.0f} headNod={nod:.1f}"
                )
        if action.name in ("Jump", "Tuck", "Land", "Dash", "DashRecover"):
            show = {"Jump": (5, 12, 17), "Tuck": (1, 12), "Land": (1,), "Dash": (8, 20, 28), "DashRecover": (1,)}[action.name]
            for frame in show:
                scene.frame_set(frame)
                bpy.context.view_layer.update()
                posed_now = arm.evaluated_get(bpy.context.evaluated_depsgraph_get())
                hips = posed_now.pose.bones["Hips"].head
                head = posed_now.pose.bones["Head"]
                crown = (head.tail - head.head).normalized()
                knee_l = posed_now.pose.bones["ShinL"].head
                foot_l = posed_now.pose.bones["FootL"].head
                knee_r = posed_now.pose.bones["ShinR"].head
                foot_r = posed_now.pose.bones["FootR"].head
                hand = posed_now.pose.bones["HandL"].head
                print(
                    f"POSE {action.name} {frame} up={crown.z:.2f} "
                    f"headY={head.head.y - hips.y:.2f} headZ={head.head.z - hips.z:.2f} "
                    f"kneeLy={knee_l.y - hips.y:.2f} footLy={foot_l.y - hips.y:.2f} footLz={foot_l.z:.2f} "
                    f"kneeRy={knee_r.y - hips.y:.2f} footRy={foot_r.y - hips.y:.2f} "
                    f"handZ={hand.z:.2f} handY={hand.y:.2f}"
                )
        if action.name == "Run":
            for frame in (1, 3, 5, 7, 9):
                scene.frame_set(frame)
                bpy.context.view_layer.update()
                posed_now = arm.evaluated_get(bpy.context.evaluated_depsgraph_get())
                foot = posed_now.pose.bones["FootL"].head
                knee = posed_now.pose.bones["ShinL"].head
                back = posed_now.pose.bones["FootR"].head
                crown = (posed_now.pose.bones["Head"].tail - posed_now.pose.bones["Head"].head).normalized()
                nod = degrees(crown.angle(Vector((0, 0, 1))))
                print(
                    f"RUN {frame} frontFoot y={foot.y:.2f} z={foot.z:.2f} "
                    f"knee y={knee.y:.2f} backFoot y={back.y:.2f} z={back.z:.2f} "
                    f"headNod={nod:.1f}"
                )
        if action.name in ("Walk", "Run", "Attack1") and len(frames) > 4:
            print(action.name, "sample", frames[0]["ThighL"][0], frames[min(8, len(frames) - 1)]["UpperArmR"][0])
        snapshots.append((action.name, start, end, frames))

    if arm.animation_data:
        arm.animation_data.action = None
    for action in list(bpy.data.actions):
        bpy.data.actions.remove(action)

    for name, start, end, frames in snapshots:
        action = bpy.data.actions.new(name)
        action.use_fake_user = True
        assign_action(action)
        for index, pose in enumerate(frames):
            frame = start + index
            for pose_bone in arm.pose.bones:
                pose_bone.rotation_mode = "QUATERNION"
                quat, loc = pose[pose_bone.name]
                pose_bone.rotation_quaternion = quat
                pose_bone.keyframe_insert(data_path="rotation_quaternion", frame=frame)
                if pose_bone.name == "Hips":
                    pose_bone.location = loc
                    pose_bone.keyframe_insert(data_path="location", frame=frame)
        if hasattr(action, "use_frame_range"):
            action.use_frame_range = True
            action.frame_start = start
            action.frame_end = end
        smooth_action(action)
        print("baked", name, start, end, "frames", len(frames))

    for pose_bone in arm.pose.bones:
        pose_bone.rotation_quaternion = (1.0, 0.0, 0.0, 0.0)
        pose_bone.location = (0.0, 0.0, 0.0)
    if arm.animation_data:
        arm.animation_data.action = None


bake_quaternions()

bpy.ops.object.select_all(action="DESELECT")
arm.select_set(True)
for obj in skinned:
    obj.select_set(True)
bpy.context.view_layer.objects.active = arm

# One file holds the mesh. Each clip is its own file because the exporter
# only samples the active action's motion reliably.
clip_dir = os.path.join(ROOT, "Assets", "Models", "BoyAnims")
os.makedirs(clip_dir, exist_ok=True)
export_kwargs = dict(
    use_selection=True,
    object_types={"ARMATURE", "MESH"},
    add_leaf_bones=False,
    bake_anim=True,
    bake_anim_use_all_actions=False,
    bake_anim_use_nla_strips=False,
    bake_anim_simplify_factor=0.0,
    bake_anim_force_startend_keying=True,
    bake_anim_step=1.0,
    axis_forward="-Z",
    axis_up="Y",
    apply_unit_scale=True,
    use_armature_deform_only=True,
    mesh_smooth_type="FACE",
    path_mode="AUTO",
    use_mesh_modifiers=True,
)
if os.environ.get("SKIP_FBX") == "1":
    print("SKIP_FBX")
else:
    if arm.animation_data:
        arm.animation_data.action = None
    bpy.ops.export_scene.fbx(filepath=FBX_PATH, **export_kwargs)
    print("EXPORTED", FBX_PATH)
    for action in list(bpy.data.actions):
        assign_action(action)
        scene.frame_start = int(round(action.frame_start))
        scene.frame_end = int(round(action.frame_end))
        scene.frame_set(scene.frame_start)
        clip_path = os.path.join(clip_dir, action.name + ".fbx")
        bpy.ops.export_scene.fbx(filepath=clip_path, **export_kwargs)
        print("CLIP", action.name)

# Preview renders. Lights and the ground are not part of the export.
bpy.ops.mesh.primitive_plane_add(size=4, location=(0, 0, 0))
ground = bpy.context.active_object
ground.name = "PreviewGround"
ground_mat = make_mat("Ground", (0.16, 0.18, 0.2), 0.8)
ground.data.materials.append(ground_mat)

try:
    scene.render.engine = "BLENDER_EEVEE_NEXT"
except Exception:
    scene.render.engine = "BLENDER_WORKBENCH"
scene.render.resolution_x = 720
scene.render.resolution_y = 560
if hasattr(scene, "eevee") and hasattr(scene.eevee, "taa_render_samples"):
    scene.eevee.taa_render_samples = 8
scene.render.film_transparent = False
scene.view_settings.view_transform = "Standard"
world = bpy.data.worlds.new("PreviewWorld")
scene.world = world
world.use_nodes = True
world.node_tree.nodes["Background"].inputs[0].default_value = (0.16, 0.17, 0.19, 1)

bpy.ops.object.light_add(type="SUN", location=(1.5, -2.0, 4.0))
sun = bpy.context.active_object
sun.data.energy = 4.2
sun.rotation_euler = (radians(48), radians(12), radians(24))
bpy.ops.object.light_add(type="AREA", location=(-1.6, -1.4, 2.2))
area = bpy.context.active_object
area.data.energy = 180
area.data.size = 2.5

bpy.ops.object.camera_add(location=(0, -3.5, 1.05))
camera = bpy.context.active_object
camera.data.type = "ORTHO"
camera.data.ortho_scale = 2.25
scene.camera = camera
arm.hide_render = True
arm.hide_set(True)


def aim(location):
    camera.location = Vector(location)
    direction = Vector((0, 0, 0.92)) - camera.location
    camera.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()


def shoot(filename, action_name, frame):
    action = bpy.data.actions.get(action_name)
    arm.animation_data.action = action
    if hasattr(arm.animation_data, "action_slot") and action is not None:
        slots = list(getattr(action, "slots", []))
        if slots:
            try:
                arm.animation_data.action_slot = slots[0]
            except Exception as exc:
                print("preview slot", exc)
    scene.frame_set(frame)
    bpy.context.view_layer.update()
    scene.render.filepath = os.path.join(PREVIEW, filename)
    bpy.ops.render.render(write_still=True)
    print("RENDERED", filename)


aim((0.0, -3.4, 1.12))
camera.data.ortho_scale = 2.35
shoot("outfit_front.png", "Idle", 8)
aim((-3.6, 0.25, 1.05))
camera.data.ortho_scale = 2.6
shoot("atk1_wind.png", "Attack1", 4)
shoot("atk1_hit.png", "Attack1", 7)
shoot("atk2_wind.png", "Attack2", 4)
shoot("atk2_hit.png", "Attack2", 7)
shoot("atk3_wind.png", "Attack3", 4)
shoot("atk3_hit.png", "Attack3", 7)
shoot("atk3_end.png", "Attack3", 14)
print("DONE")
