# Assets/Scripts/Tools/blender/retarget.py
"""
Движения Mixamo — на нашу арматуру.

Автор, 29 сентября: «сделай возможность доставать и убирать оружие,
я уверен, анимации у нас есть». Они есть: в загрузках автора лежит
набор Mixamo «Sword and Shield» — достать, убрать, удары, блок, ходьба.
Но он сделан под риг Mixamo (65 костей, «mixamorig:…»), а у нас свой —
двадцать одна кость (`bodies.py`). Без переноса чужой клип на наш риг
не ляжет.

**Как переносим.** Оба рига стоят в Т-позе: руки вдоль X, ноги вниз,
лицом в −Y (проверено замером). Значит, достаточно знать, насколько
каждая кость Mixamo в кадре повёрнута **от своей позы привязки** —
в мировых осях, — и повернуть нашу соответствующую кость на столько же
от нашей. Длины костей при этом не важны: скелет ростом метр и X Bot
ростом метр восемьдесят делают одно и то же движение. Таз, кроме
поворота, ещё и сдвигается — сдвиг пишем в долях высоты таза.

Пишет `motion/<Имя>.json`: по кадру на каждую кость поворот от позы
привязки (кватернион, мировые оси) и сдвиг таза. Из этого `bodies.py`
собирает клип для **каждой** оболочки — у каждой свой рост и свои
суставы, а движение одно. Файл лежит в репозитории: тела собираются
и без загрузок автора.

Лицензия: движения Mixamo бесплатны для игр, коммерческих тоже.

Запуск:

    blender --background --python Tools/blender/retarget.py -- --clip <файл.fbx> --name Sheathe --tilt 35,15
    blender --background --python Tools/blender/retarget.py -- --clip <файл.fbx> --name Draw --start-from Sheathe

Для набора «Sword and Shield»: «убрать» — `sheath sword 1.fbx`,
«достать» — `sheath sword 2.fbx` (по пути кисти: первый кончается у левого
бедра, второй там начинается).
"""

import json
import math
import sys
from pathlib import Path

import bpy
from mathutils import Quaternion, Vector

# Наша кость ← кость Mixamo. Позвоночник Mixamo из трёх звеньев,
# у нас из двух: поясница берёт среднее, грудь — верхнее. Повороты
# мировые, поэтому пропущенное нижнее звено не теряется — оно уже
# внутри поворота среднего.
MAP = {
    "Hips": "Hips", "Spine": "Spine1", "Chest": "Spine2", "Neck": "Neck", "Head": "Head",
    "LeftShoulder": "LeftShoulder", "LeftUpperArm": "LeftArm",
    "LeftLowerArm": "LeftForeArm", "LeftHand": "LeftHand",
    "RightShoulder": "RightShoulder", "RightUpperArm": "RightArm",
    "RightLowerArm": "RightForeArm", "RightHand": "RightHand",
    "LeftUpperLeg": "LeftUpLeg", "LeftLowerLeg": "LeftLeg",
    "LeftFoot": "LeftFoot", "LeftToes": "LeftToeBase",
    "RightUpperLeg": "RightUpLeg", "RightLowerLeg": "RightLeg",
    "RightFoot": "RightFoot", "RightToes": "RightToeBase",
}

PREFIX = "mixamorig:"


def rotation(matrix):
    """Поворот матрицы без масштаба: у Mixamo объект ×0,01."""
    return matrix.to_3x3().normalized().to_quaternion()


def main():
    argv = sys.argv[sys.argv.index("--") + 1:]

    def opt(key, default=None):
        return argv[argv.index(key) + 1] if key in argv else default

    clip = Path(opt("--clip"))
    name = opt("--name")
    out = Path(__file__).resolve().parent / "motion" / (name + ".json")

    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    bpy.ops.import_scene.fbx(filepath=str(clip))

    arm = next(o for o in bpy.context.scene.objects if o.type == "ARMATURE")
    act = arm.animation_data.action
    first, last = (int(round(x)) for x in act.frame_range)
    first = int(opt("--from", first))
    last = int(opt("--to", last))

    world = arm.matrix_world
    rest = {ours: rotation(world @ arm.data.bones[PREFIX + theirs].matrix_local)
            for ours, theirs in MAP.items()}
    hips_rest = world @ arm.data.bones[PREFIX + "Hips"].head_local
    height = hips_rest.z

    scene = bpy.context.scene
    frames, hips, hand = [], [], []
    for f in range(first, last + 1):
        scene.frame_set(f)
        row = {}
        for ours, theirs in MAP.items():
            pb = arm.pose.bones[PREFIX + theirs]
            delta = rotation(world @ pb.matrix) @ rest[ours].inverted()
            row[ours] = [round(c, 5) for c in delta]
        frames.append(row)

        head = world @ arm.pose.bones[PREFIX + "Hips"].head
        hips.append([round(c, 5) for c in (head - hips_rest) / height])
        hand.append(world @ arm.pose.bones[PREFIX + "RightHand"].head)

    # Где кисть отпускает рукоять — для «убрать»: первый кадр, после
    # которого кисть уже не уходит от своего последнего места дальше
    # пяти сантиметров X Bot (у нашего тела ростом в единицу это около
    # трёх). Там вещь переходит из руки в ножны.
    settle = len(hand) - 1
    for i in range(len(hand) - 1, -1, -1):
        if (hand[i] - hand[-1]).length > 0.05:
            break
        settle = i

    # Наклон ножен. У Mixamo меч в конце «убрать» лежит горизонтально
    # и смотрит назад — у худого скелета он уходил сквозь таз (кадр
    # проверки 29 сентября). Докручиваем кисть к концу клипа: клинок
    # вниз-назад и наружу от бедра. Крутим движение, а не подмену —
    # иначе меч прыгал бы, переходя из руки в ножны.
    tilt = opt("--tilt")
    if tilt is not None:
        down, out_ = (float(x) for x in tilt.split(","))
        extra = (Quaternion((0.0, 0.0, 1.0), math.radians(-out_))
                 @ Quaternion((1.0, 0.0, 0.0), math.radians(-down)))
        begin = max(0, settle - 11)
        for i, row in enumerate(frames):
            w = 0.0 if i <= begin else min(1.0, (i - begin) / float(settle - begin))
            w = w * w * (3.0 - 2.0 * w)
            q = Quaternion((1.0, 0.0, 0.0, 0.0)).slerp(extra, w) @ Quaternion(row["RightHand"])
            row["RightHand"] = [round(c, 5) for c in q]

    # Начать с позы другого клипа: «достать» — с того кадра «убрать»,
    # где кисть отпустила меч. Иначе в первом кадре рука стоит в семи
    # сантиметрах от ножен, и меч, переходя в неё, прыгает.
    start = opt("--start-from")
    if start is not None:
        other = json.loads((out.parent / (start + ".json")).read_text(encoding="utf-8"))
        pose = other["frames"][other["settle"]]
        shift = other["hips"][other["settle"]]
        span = int(opt("--blend", "8"))
        for i in range(min(span, len(frames))):
            w = i / float(span)
            w = w * w * (3.0 - 2.0 * w)
            row = frames[i]
            for bone in row:
                a, b = Quaternion(pose[bone]), Quaternion(row[bone])
                if a.dot(b) < 0.0:
                    b.negate()
                row[bone] = [round(c, 5) for c in a.slerp(b, w)]
            hips[i] = [round(shift[k] + (hips[i][k] - shift[k]) * w, 5) for k in range(3)]

    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_text(json.dumps({
        "source": clip.name, "fps": 30, "frames": frames, "hips": hips,
        "settle": settle,
    }, ensure_ascii=False), encoding="utf-8")
    print(f"[ДВИЖЕНИЯ] {name}: из «{clip.name}» кадров {len(frames)}, кисть садится на {settle}-м → {out}")


if __name__ == "__main__":
    main()
