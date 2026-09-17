# Assets/Scripts/Tools/blender/restyle.py
"""
Чужая модель — в наш стиль. Основа приходит из загрузок автора,
а выходит наша: другой цвет, другой масштаб, наши имена движений.

Решение автора от 13 сентября: готовые модели в чистом виде
не используются. Они — основа, из которой делается своё, в едином
стиле проекта. Отбор основы и лицензии — docs/14-HANDOFF.md §21–§22.

**Стиль здесь держит палитра, а не старание.** Лучшая основа,
faceted_character Fabian Orrego, раскрашена не текстурой, а атласом:
512×512 из цветных полос с градиентом, и каждая грань смотрит в свою
полосу. Перекрасить атлас — перекрасить модель целиком, не трогая
ни формы, ни рига. Все модели, собранные на таких атласах, проходят
через одну и ту же функцию палитры — и совпадают по построению.

**Про лицензию.** Основа под CC BY 4.0. Переделка остаётся под теми же
условиями: автора основы указывать обязательно, и у изменённого
материала тоже (раздел 3(a) лицензии). Строка — в
Assets/Textures/ИСТОЧНИКИ.md, а не в памяти.

Запуск:

    blender --background --python Tools/blender/restyle.py -- --preview <папка>
    blender --background --python Tools/blender/restyle.py -- --preview <папка> --base <имя файла из Downloads>

Пока только превью «до» и «после»: в игру переделка не выводится,
пока автор не решит, какой оболочкой эта основа станет (§22).

**Второй опыт — Охотник, а не Карган.** 13 сентября в `Downloads` рядом
с `faceted_character...` лёг `dragon_hunter_warrior.glb`: вторая, отдельная
от `bodies.py`/`wear.py` попытка получить Охотника — не кодом, а из
готовой основы, тем же путём, что и Карган в `23-PROMPTS.md`. Флаг
`--base` запускает ровно тот же конвейер на любой модели из `Downloads`,
не заводя вторую копию файла.
"""

import colorsys
import sys
from pathlib import Path

import bpy
import numpy as np
from mathutils import Vector

DOWNLOADS = Path.home() / "Downloads"
DEFAULT_BASE = "faceted_character_locomotion_animation.glb"

# Во что превращается каждый цвет атласа.
#
# Насыщенность гасится вдвое, яркость опускается: палитра
# основы из 23-PROMPTS.md §2 — серая кость, чёрное, потёртая кожа,
# тусклое железо. Оттенок не выбрасывается совсем, а подтягивается
# к двум полюсам: тёплые цвета — к коже, холодные — к железу. Иначе
# фиолетовая ткань и зелёная ткань стали бы одинаково серыми, и
# модель потеряла бы разницу между частями, которую автор основы
# задал цветом.
#
# Первый заход (насыщенность 0.28, яркость 0.66) вышел почти
# монохромным: кожа, ткань и наручи слились в один чёрно-серый.
# Автор просил «отемни немного», а не «сделай чёрным».
SATURATION = 0.50
VALUE = 0.82
LEATHER_HUE = 0.07     # коричневый
IRON_HUE = 0.58        # холодный серо-синий
PULL = 0.55            # насколько сильно тянуть оттенок к полюсу


def grade(r, g, b):
    h, s, v = colorsys.rgb_to_hsv(r, g, b)

    warm = h < 0.20 or h > 0.85
    target = LEATHER_HUE if warm else IRON_HUE
    delta = ((target - h + 0.5) % 1.0) - 0.5
    h = (h + delta * PULL) % 1.0

    s = s * SATURATION
    # Кривая, а не множитель: светлые края полос остаются светлыми
    # настолько, чтобы грани различались, а тёмные уходят в ночь.
    v = (v ** 1.12) * VALUE
    return colorsys.hsv_to_rgb(h, s, v)


def restyle_image(img):
    w, h = img.size
    px = np.array(img.pixels[:], dtype=np.float32).reshape(h, w, 4)
    rgb = px[:, :, :3]

    flat = rgb.reshape(-1, 3)
    # Атлас — полосы, в нём немного разных цветов; считаем по уникальным.
    uniq, inverse = np.unique(np.round(flat, 4), axis=0, return_inverse=True)
    graded = np.array([grade(*c) for c in uniq], dtype=np.float32)
    rgb[:] = graded[inverse.reshape(-1)].reshape(h, w, 3)

    img.pixels[:] = px.reshape(-1)
    img.update()


def load(base):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=str(base))


def strip():
    """
    Убрать то, что к телу не относится.

    Меч — слот класса (22-LOOK.md §2), а не тело, и в руках он мешает
    ретаргету (23-PROMPTS.md §2). Икосфера и заклинание — служебные
    и декоративные объекты основы, они висят в сцене отдельно от рига.

    Имена от одной основы к другой разные (`Sword` у faceted_character,
    `DragonKillerSword_0` у dragon_hunter_warrior) — сравнение по
    подстроке без учёта регистра, а не по точному имени.
    """
    drop = ("sword", "spell", "icosphere")
    for o in list(bpy.context.scene.objects):
        low = o.name.lower()
        if any(tag in low for tag in drop):
            bpy.data.objects.remove(o, do_unlink=True)


def no_glow():
    """
    Свечение гасится целиком. Светящиеся глаза и руны уводят в другой
    жанр и спорят с ночным светом (23-PROMPTS.md §3) — это решение
    основы проекта, а не вкус.
    """
    for m in bpy.data.materials:
        if not m.use_nodes:
            continue
        bsdf = m.node_tree.nodes.get("Principled BSDF")
        if bsdf is None:
            continue
        emission = bsdf.inputs.get("Emission Color")
        if emission is not None:
            for link in list(emission.links):
                m.node_tree.links.remove(link)
            emission.default_value = (0.0, 0.0, 0.0, 1.0)
        strength = bsdf.inputs.get("Emission Strength")
        if strength is not None:
            strength.default_value = 0.0


def palette():
    """
    Перекрасить цвет. Маска свечения больше не нужна.

    Основы бывают двух родов: атлас-полосы в текстуре (faceted_character)
    и одноцветные материалы без единой картинки (dragon_hunter_warrior —
    ни один Base Color тут не был подключён к текстуре, и первый заход
    прошёл мимо всей модели, не тронув ни одного материала). Второй
    случай — то же самое `grade()`, только на значении, а не на пикселях.
    """
    for m in bpy.data.materials:
        if not m.use_nodes:
            continue
        bsdf = m.node_tree.nodes.get("Principled BSDF")
        base = bsdf.inputs["Base Color"] if bsdf else None
        if base is None:
            continue

        if base.links:
            node = base.links[0].from_node
            if node.type == "TEX_IMAGE" and node.image is not None:
                restyle_image(node.image)
                print("[ПЕРЕДЕЛКА] атлас перекрашен: " + node.image.name)
            continue

        r, g, b, a = base.default_value
        base.default_value = (*grade(r, g, b), a)
        print("[ПЕРЕДЕЛКА] цвет перекрашен: " + m.name)


def measure():
    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    lo = Vector((1e9, 1e9, 1e9))
    hi = Vector((-1e9, -1e9, -1e9))
    dg = bpy.context.evaluated_depsgraph_get()
    for o in meshes:
        ev = o.evaluated_get(dg)
        data = ev.to_mesh()
        for v in data.vertices:
            w = o.matrix_world @ v.co
            lo = Vector((min(lo.x, w.x), min(lo.y, w.y), min(lo.z, w.z)))
            hi = Vector((max(hi.x, w.x), max(hi.y, w.y), max(hi.z, w.z)))
        ev.to_mesh_clear()
    return lo, hi


def fit():
    """
    Рост в единицу, опора у ног, в нуле. Те же условия, что у наших
    четырёх оболочек (bodies.py): код ставит модель в ноль и умножает
    на свой рост, 1.2 рядовому и 1.5 опытному.

    Масштаб ставится на самый верхний объект, а не на меши: иначе риг
    и тело разъедутся.
    """
    root = bpy.data.objects.get("Sketchfab_model")
    lo, hi = measure()
    height = hi.z - lo.z
    k = 1.0 / height
    root.scale = root.scale * k
    bpy.context.view_layer.update()

    lo, hi = measure()
    root.location.z -= lo.z
    root.location.x -= (lo.x + hi.x) * 0.5
    root.location.y -= (lo.y + hi.y) * 0.5
    bpy.context.view_layer.update()

    lo, hi = measure()
    print("[ПЕРЕДЕЛКА] рост {:.3f}, опора {:.3f}, ширина {:.3f}".format(
        hi.z - lo.z, lo.z, hi.x - lo.x))


def preview(folder, tag):
    folder = Path(folder)
    folder.mkdir(parents=True, exist_ok=True)
    sc = bpy.context.scene
    sc.render.engine = "BLENDER_WORKBENCH"
    sc.display.shading.color_type = "TEXTURE"
    sc.display.shading.light = "STUDIO"
    sc.display.shading.show_shadows = True
    sc.render.resolution_x = 540
    sc.render.resolution_y = 760
    sc.world = bpy.data.worlds.new("W")
    sc.world.color = (0.16, 0.16, 0.18)

    cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam"))
    cam.data.lens = 55
    sc.collection.objects.link(cam)
    sc.camera = cam

    def shot(name, loc, aim, tall=True):
        cam.location = Vector(loc)
        cam.rotation_euler = (Vector(aim) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
        sc.render.resolution_y = 760 if tall else 540
        sc.render.filepath = str(folder / (tag + "-" + name + ".png"))
        bpy.ops.render.render(write_still=True)

    shot("front", (0.0, -2.4, 0.6), (0.0, 0.0, 0.5))
    shot("three-quarter", (1.7, -1.7, 0.9), (0.0, 0.0, 0.5))
    shot("top", (0.0, 1.3, 2.6), (0.0, 0.0, 0.45), tall=False)


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    shots = argv[argv.index("--preview") + 1] if "--preview" in argv else None
    base = DOWNLOADS / (argv[argv.index("--base") + 1] if "--base" in argv else DEFAULT_BASE)

    if not base.exists():
        raise SystemExit("Нет такого файла в Downloads: " + str(base))

    if shots:
        load(base); strip(); fit()
        preview(shots, "0-before")

    load(base)
    strip()
    no_glow()
    palette()
    fit()

    if shots:
        preview(shots, "1-after")


main()
