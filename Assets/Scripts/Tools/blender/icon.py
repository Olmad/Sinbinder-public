# Assets/Scripts/Tools/blender/icon.py
"""
Иконка игры (автор, 29.10: «может стоит сделать иконку?»). Банка души: то, что делает Греховод,
одной вещью — душа, связанная и запертая. Та же модель, что в игре (props.py: SoulJarFull), снятая
крупно: стекло, золотой обод, душа светится старым золотом изнутри, холодный контровой свет по краю,
вокруг — тьма. Читается и в 16 пикселях: светлое пятно в тёмном силуэте банки.

Cycles, а не Eevee: стекло со светом внутри у Eevee выходит матовой бутылкой.

    blender --background --python Assets/Scripts/Tools/blender/icon.py -- <выход.png> [размер]
"""

import math
import os
import sys

import bpy
from mathutils import Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import props


def material(obj, name):
    for m in obj.data.materials:
        if m.name == name or m.name.split(".")[0] == name:
            return m
    return None


def node(m):
    return m.node_tree.nodes.get("Principled BSDF")


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    out = argv[0] if argv else "icon.png"
    size = int(argv[1]) if len(argv) > 1 else 1024

    prop = next(p for p in props.PROPS if p.name == "SoulJarFull")
    obj = props.build(prop)

    glass = node(material(obj, "Glass"))
    glass.inputs["Roughness"].default_value = 0.08
    glass.inputs["IOR"].default_value = 1.45
    glass.inputs["Transmission Weight"].default_value = 1.0
    glass.inputs["Base Color"].default_value = (0.62, 0.74, 0.68, 1.0)
    gold = node(material(obj, "Old Gold"))
    gold.inputs["Metallic"].default_value = 1.0
    gold.inputs["Roughness"].default_value = 0.32
    for name, colour, power in (("Soul Dim", (1.0, 0.42, 0.12, 1.0), 14.0), ("Soul", (1.0, 0.78, 0.38, 1.0), 40.0)):
        b = node(material(obj, name))
        b.inputs["Base Color"].default_value = colour
        b.inputs["Emission Color"].default_value = colour
        b.inputs["Emission Strength"].default_value = power

    lo = Vector((9, 9, 9)); hi = Vector((-9, -9, -9))
    for v in obj.data.vertices:
        lo = Vector(map(min, lo, v.co)); hi = Vector(map(max, hi, v.co))
    mid = (lo + hi) * 0.5
    tall = hi.z - lo.z

    sc = bpy.context.scene
    sc.render.engine = "CYCLES"
    sc.cycles.samples = 160
    sc.cycles.use_denoising = True
    sc.render.resolution_x = sc.render.resolution_y = size
    sc.view_settings.view_transform = "AgX"
    sc.view_settings.look = "AgX - Punchy" if "AgX - Punchy" in [i.identifier for i in sc.view_settings.bl_rna.properties["look"].enum_items] else "None"

    world = bpy.data.worlds.new("W"); sc.world = world
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[0].default_value = (0.010, 0.009, 0.012, 1.0)
    world.node_tree.nodes["Background"].inputs[1].default_value = 1.0

    # Свет души — точечный внутри банки, тёплый: он и красит стекло изнутри.
    inner = bpy.data.objects.new("Inner", bpy.data.lights.new("Inner", "POINT"))
    inner.data.energy = 9.0; inner.data.color = (1.0, 0.66, 0.30); inner.data.shadow_soft_size = 0.02
    inner.location = mid + Vector((0, 0, -tall * 0.05))
    sc.collection.objects.link(inner)
    # Контровой холодный — сзади слева: край стекла и обод читаются на чёрном.
    rim = bpy.data.objects.new("Rim", bpy.data.lights.new("Rim", "AREA"))
    rim.data.energy = 6.0; rim.data.color = (0.55, 0.65, 1.0); rim.data.size = 0.15
    rim.location = mid + Vector((-0.25, 0.35, 0.20))
    rim.rotation_euler = (mid - rim.location).to_track_quat("-Z", "Y").to_euler()
    sc.collection.objects.link(rim)
    # Слабый заполняющий спереди — чтобы бечёвка и обод не тонули.
    fill = bpy.data.objects.new("Fill", bpy.data.lights.new("Fill", "AREA"))
    fill.data.energy = 0.8; fill.data.color = (1.0, 0.85, 0.7); fill.data.size = 0.3
    fill.location = mid + Vector((0.25, -0.4, 0.05))
    fill.rotation_euler = (mid - fill.location).to_track_quat("-Z", "Y").to_euler()
    sc.collection.objects.link(fill)

    # Земля под банкой — тёмная, ловит отсвет души.
    ground = bpy.data.meshes.new("Ground")
    ground.from_pydata([(-2, -2, lo.z), (2, -2, lo.z), (2, 2, lo.z), (-2, 2, lo.z)], [], [(0, 1, 2, 3)])
    gm = bpy.data.materials.new("Ground"); gm.use_nodes = True
    node(gm).inputs["Base Color"].default_value = (0.03, 0.025, 0.022, 1)
    node(gm).inputs["Roughness"].default_value = 0.9
    ground.materials.append(gm)
    sc.collection.objects.link(bpy.data.objects.new("Ground", ground))

    cam_data = bpy.data.cameras.new("Cam")
    cam_data.lens = 85
    cam = bpy.data.objects.new("Cam", cam_data)
    sc.collection.objects.link(cam); sc.camera = cam
    # Чуть снизу и сбоку: банка выглядит вещью, а не чертежом; кадр — квадрат с полями.
    dist = tall * 5.0
    cam.location = mid + Vector((dist * 0.35, -dist * 0.93, -tall * 0.05))
    aim = mid + Vector((0, 0, tall * 0.02))
    cam.rotation_euler = (aim - cam.location).to_track_quat("-Z", "Y").to_euler()

    sc.render.filepath = out
    bpy.ops.render.render(write_still=True)
    print("[ИКОНКА] " + out)


main()
