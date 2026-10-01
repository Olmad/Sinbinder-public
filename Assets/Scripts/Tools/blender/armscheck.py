# Assets/Scripts/Tools/blender/armscheck.py
"""
Меч в руке и меч в ножнах — одно и то же место на кадре посадки.

Вещь «в ножнах» (`wear.py`, `stowed`) — то же оружие, переведённое с кисти
на таз на кадре, где кисть отпускает рукоять в клипе «убрать»
(`retarget.py`, `settle`). Если они на этом кадре совпадают, подмена в игре
не видна. Проверено так 29 сентября (`14-HANDOFF.md` §112.7); проверять
так же после любой правки клипа, кулака или оружия.

Тело и вещи берутся из `Assets/Resources` — сначала пересобрать
`bodies.py` и `wear.py`. Оружие в руке красится красным, в ножнах — синим:
на кадре посадки красного быть не должно видно — оно внутри синего.

Запуск:

    blender --background --python Tools/blender/armscheck.py -- --out <папка>
    blender --background --python Tools/blender/armscheck.py -- --out <папка> \
        --weapon Sword --sheath Scabbard --clip Draw --frames 1,6,12,25 --shell Skeleton
"""

import sys
from pathlib import Path

import bpy
from mathutils import Vector

sys.path.insert(0, str(Path(__file__).resolve().parent))

import bodies


def opt(argv, key, default=None):
    return argv[argv.index(key) + 1] if key in argv else default


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    out = Path(opt(argv, "--out"))
    out.mkdir(parents=True, exist_ok=True)
    weapon = opt(argv, "--weapon", "Sword")
    sheath = opt(argv, "--sheath", "Scabbard")
    clip = opt(argv, "--clip", "Sheathe")
    shell = opt(argv, "--shell", "Skeleton")
    root = bodies.unity_root() / "Assets" / "Resources"

    # Кадр посадки по умолчанию — из того же файла движения, что и клип.
    settle = bodies.motion("Sheathe")["settle"] + 1
    frames = [int(x) for x in opt(argv, "--frames", f"{settle},1").split(",")]

    bodies.wipe()
    bpy.ops.import_scene.fbx(filepath=str(root / "Bodies" / (shell + ".fbx")))
    arm = next(o for o in bpy.data.objects if o.type == "ARMATURE")

    def wear(name):
        own = root / "Wear" / shell / (name + ".fbx")
        return own if own.exists() else root / "Wear" / (name + ".fbx")

    def attach(path, bone, tint):
        before = set(bpy.data.objects)
        bpy.ops.import_scene.fbx(filepath=str(path))
        for o in set(bpy.data.objects) - before:
            if o.type != "MESH":
                continue
            # Как в игре: вещь жёстко на кости — группа вершин с весом 1.
            o.vertex_groups.new(name=bone).add(list(range(len(o.data.vertices))), 1.0, "REPLACE")
            o.modifiers.new("кость", "ARMATURE").object = arm
            if tint:
                for s in o.material_slots:
                    if s.material:
                        s.material = s.material.copy()
                        s.material.diffuse_color = tint

    attach(wear(weapon), "RightHand", (0.9, 0.3, 0.2, 1))
    if wear(weapon + "Stowed").exists():
        attach(wear(weapon + "Stowed"), "Hips", (0.2, 0.5, 0.9, 1))
    if sheath != "-" and wear(sheath).exists():
        attach(wear(sheath), "Hips", None)

    act = next(a for a in bpy.data.actions if a.name.endswith(clip))
    arm.animation_data.action = act
    slots = getattr(act, "slots", None)
    if slots is not None and len(slots) and hasattr(arm.animation_data, "action_slot"):
        arm.animation_data.action_slot = slots[0]

    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.light = "STUDIO"
    scene.display.shading.color_type = "MATERIAL"
    scene.render.resolution_x = scene.render.resolution_y = 700
    scene.world = bpy.data.worlds.new("w")
    scene.world.color = (0.2, 0.2, 0.22)
    cam_data = bpy.data.cameras.new("c")
    cam_data.type = "ORTHO"
    cam = bpy.data.objects.new("c", cam_data)
    scene.collection.objects.link(cam)
    scene.camera = cam

    for frame in frames:
        scene.frame_set(frame)
        for view, loc, aim in (("q", (1.1, -1.6, 0.9), (0.05, -0.05, 0.55)),
                               ("side", (1.8, 0.0, 0.6), (0.0, 0.0, 0.55)),
                               ("front", (0.0, -1.8, 0.6), (0.0, 0.0, 0.55))):
            cam.location = Vector(loc)
            cam.rotation_euler = (Vector(aim) - cam.location).to_track_quat("-Z", "Y").to_euler()
            cam_data.ortho_scale = 0.8
            scene.render.filepath = str(out / f"{shell}-{weapon}-{clip}-{frame:02d}-{view}.png")
            bpy.ops.render.render(write_still=True)
    print(f"[НОЖНЫ] {shell}/{weapon}, «{clip}», кадры {frames} → {out}")


if __name__ == "__main__":
    main()
