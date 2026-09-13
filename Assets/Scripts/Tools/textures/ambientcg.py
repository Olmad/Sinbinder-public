# Assets/Scripts/Tools/textures/ambientcg.py
"""
Материалы с ambientCG — по списку, а не руками из браузера.

Решено вдвоём 13 сентября (docs/14-HANDOFF.md §16–§17): земля
и окружение берутся с ambientCG, и только с него. Один источник —
одна подготовка сканов, лицензия CC0 без оговорок.

Почему скрипт, а не «скачал и положил»:

* **Список — это решение, и он виден диффом.** Какой материал
  в игре и зачем, записано здесь, а не в памяти того, кто качал.
* **Учёт ведётся сам.** Каждое скачивание дописывает строку
  в Assets/Textures/ИСТОЧНИКИ.md: файл, откуда, лицензия, дата.
  Проверять это придётся перед продажей, когда цена ошибки
  максимальна (§17.4), — и история с Tripo показала, как быстро
  «откуда-то» становится «не наше».
* **Размер один на всё.** 1K: камера стоит под 84° на высоте
  двадцати двух метров, и 2K сверху неотличим, а на машине автора
  четыре гигабайта видеопамяти.

Карты берутся четыре: цвет, нормали (OpenGL — Unity читает их так),
шероховатость и затенение. Высота и смещение не нужны: рельефа
на земле с такой высоты не видно.

Запуск:

    python Tools/textures/ambientcg.py            # докачать недостающее
    python Tools/textures/ambientcg.py --list     # показать список

Уже скачанное не перекачивается.
"""

import datetime
import io
import json
import sys
import urllib.request
import zipfile
from pathlib import Path

SIZE = "1K-JPG"

# Что нужно демо, и ни одним материалом больше. Земля — в первую
# очередь: сверху она занимает большую часть кадра.
#
# Подобрано по превью, а не по популярности. Первый заход, взятый
# вслепую из самых популярных, вышел пёстрым: ярко-зелёный мох,
# оранжевая глина, светлая чистая мостовая, плоско-красная кожа.
# Грейд правит оттенок, но не превращает весёлый материал в мрачный,
# поэтому отбор идёт по палитре основы из 23-PROMPTS.md §2: серая кость,
# чёрное, потёртая коричневая кожа, тусклое железо.
WANTED = [
    ("Ground048",       "лагерь: тёмная лесная земля под палатками"),
    ("Ground110",       "лагерь: утоптанная земля со щебнем у костра"),
    ("Gravel043",       "тропы и край карты"),
    ("Rock050",         "валуны"),
    ("Rock022",         "скальные выходы, слоистые"),
    ("PavingStones127", "склеп: пол из тёмных плит"),
    ("Bricks076A",      "склеп: стены из грубого камня"),
    ("Planks037A",      "столы, стенды, ящики"),
    ("Fabric061",       "палатки"),
    ("Leather033A",     "сума, ремни: потёртая тёмная кожа"),
]

# Суффиксы файлов ambientCG → что это.
MAPS = {
    "_Color.jpg": "Color",
    "_NormalGL.jpg": "NormalGL",
    "_Roughness.jpg": "Roughness",
    "_AmbientOcclusion.jpg": "AmbientOcclusion",
}

API = "https://ambientcg.com/api/v2/full_json?type=Material&id={}&include=downloadData"


def unity_root():
    here = Path(__file__).resolve()
    for up in here.parents:
        if (up / "Assets").is_dir():
            return up
    raise SystemExit("Папки Assets над скриптом нет: некуда класть текстуры.")


def fetch(url):
    request = urllib.request.Request(url, headers={"User-Agent": "Sinbinder-texture-fetch"})
    with urllib.request.urlopen(request, timeout=120) as response:
        return response.read()


def link(asset_id):
    data = json.loads(fetch(API.format(asset_id)))
    found = data.get("foundAssets") or []
    if not found:
        raise RuntimeError(asset_id + ": на ambientCG такого нет")

    folders = found[0]["downloadFolders"]["default"]["downloadFiletypeCategories"]
    for item in folders["zip"]["downloads"]:
        if item["attribute"] == SIZE:
            return item["downloadLink"]

    raise RuntimeError(asset_id + ": нет размера " + SIZE)


LEDGER_HEAD = """# Источники текстур

Каждая текстура в проекте — с указанием, откуда и под какой лицензией.
Заведено 13 сентября (`docs/14-HANDOFF.md` §17.4): через три месяца никто
не вспомнит, CC0 этот файл или пришёл «откуда-то», а проверять придётся
перед продажей.

Строки дописывает `Tools/textures/ambientcg.py`. Кладёшь текстуру
руками — дописываешь строку руками. Файл без строки здесь в проект
не входит.

| папка | откуда | лицензия | зачем | дата |
|---|---|---|---|---|
"""


def record(ledger, asset_id, purpose):
    if not ledger.exists():
        ledger.write_text(LEDGER_HEAD, encoding="utf-8")

    text = ledger.read_text(encoding="utf-8")
    if "| `" + asset_id + "/` |" in text:
        return

    row = "| `{0}/` | https://ambientcg.com/view?id={0} | CC0 1.0 | {1} | {2} |\n".format(
        asset_id, purpose, datetime.date.today().isoformat())
    ledger.write_text(text + row, encoding="utf-8")


def main():
    # Консоль Windows по умолчанию не UTF-8, и русские строки
    # превращаются в вопросы.
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")

    if "--list" in sys.argv:
        for asset_id, purpose in WANTED:
            print(asset_id.ljust(18) + purpose)
        return

    root = unity_root() / "Assets" / "Textures"
    root.mkdir(parents=True, exist_ok=True)
    ledger = root / "ИСТОЧНИКИ.md"

    for asset_id, purpose in WANTED:
        folder = root / asset_id
        # Готово — это есть цвет. Не «все четыре карты»: у части
        # материалов ambientCG затенения нет вовсе (Leather037), и такой
        # признак качал бы их заново при каждом запуске.
        if (folder / (asset_id + "_Color.jpg")).exists():
            record(ledger, asset_id, purpose)
            print("[ТЕКСТУРЫ] " + asset_id + ": уже есть")
            continue

        archive = zipfile.ZipFile(io.BytesIO(fetch(link(asset_id))))
        folder.mkdir(exist_ok=True)

        taken = 0
        for name in archive.namelist():
            for suffix, label in MAPS.items():
                if name.endswith(suffix):
                    (folder / (asset_id + "_" + label + ".jpg")).write_bytes(archive.read(name))
                    taken += 1

        if taken != len(MAPS):
            print("[ТЕКСТУРЫ] " + asset_id + ": карт " + str(taken) + " из " + str(len(MAPS))
                  + " — в архиве не всё, проверь руками")

        record(ledger, asset_id, purpose)
        print("[ТЕКСТУРЫ] " + asset_id + ": " + purpose)


main()
