# Tools/shots/chronicle.py
"""
Летопись кадров: как игра выглядела по датам и что менялось.

Автор, 15 октября: «нам нужно вести учёт скриншотов по датам с изменениями».
Кадры прохождения (Docs/Образцы/прохождение) переснимаются каждым прогоном
и в репозиторий не идут (.gitignore: двадцать пять картинок за прогон
раздули репозиторий до 230 МБ, пуш 20 сентября сервер отклонил). Летопись —
отобранное: каждая видимая правка кладёт кадры «после» в свою запись.

    Docs/Образцы/летопись/
      README.md                          оглавление по датам (пересобирается)
      2026-10-15 3 табличка/
        запись.json                      дата, что изменилось, где записано
        README.md                        «до» и «после» рядом (пересобирается)
        06 разговор — меню, долг.jpg     кадр: JPEG, 50–150 КБ

Кадр «до» второй раз не хранится — это кадр того же состояния из прежней
записи, где оно снималось последним. Поэтому в запись кладут только то,
что изменилось, и репозиторий растёт на мегабайты, а не на сотни.

Слова — в файле задания (UTF-8): PowerShell портит кириллицу в аргументах.

    python Tools/shots/chronicle.py add <задание.json>
    python Tools/shots/chronicle.py index

Задание:
    {
      "date": "2026-10-15",                 день правки
      "folder": "табличка",                 имя записи (номер дня ставится сам)
      "title": "Табличка — одна на все окна",
      "note": "что изменилось — абзац для автора",
      "refs": ["46-UI.md §2.1", "14-HANDOFF.md §111.17"],
      "verdict": "ждём",                    слово автора; поменять, когда скажет
      "source": "Docs/Образцы/прохождение", откуда брать (по умолчанию — прохождение)
      "set": "интерфейс",                   именованный набор состояний (ниже)
      "only": ["05", "06"],                 из набора — только эти
      "frames": [{"file": "открытие — надпись над лагерем.png",
                  "name": "открытие — надпись над лагерем"}],
      "sheet": false                        true — один лист миниатюр вместо кадров
    }

Номер в начале имени кадра прохождения («03 Греховод вышел…») сдвигается,
когда в прогон добавляют шаги, — кадр ищется без него. Кадр старше дня
записи не берётся: прогон, который его снимал, был до правки.
"""
import datetime
import json
import os
import re
import subprocess
import sys
import urllib.parse

from PIL import Image, ImageDraw, ImageFont

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
BOOK = os.path.join(ROOT, "Docs", "Образцы", "летопись")
WALK = os.path.join(ROOT, "Docs", "Образцы", "прохождение")
FONT = os.path.join(ROOT, "Assets", "Resources", "Fonts", "PTSerif-Regular.ttf")
REPO = "https://github.com/Olmad/Sinbinder-public"

# Кадр в записи — в разрешении прогона (1600): мелкий текст интерфейса
# должен читаться, о нём шаги и спорят. Тёмный кадр в JPEG — 50–150 КБ.
WIDTH = 1600
QUALITY = 82

MONTHS = ["января", "февраля", "марта", "апреля", "мая", "июня", "июля",
          "августа", "сентября", "октября", "ноября", "декабря"]

# Двадцать состояний описи интерфейса (46-UI.md §1) — номер, имя и кадр
# прохождения, где состояние снято (без номера шага). С открытия 15 октября
# выход из шатра и первые щелчки снимаются глазами Греховода, до крана, —
# лагерь сверху теперь в кадре «провожатый дошёл», один воин — в кадре описи.
SETS = {
    "интерфейс": [
        ("01", "лагерь, ничего не выбрано", "провожатый дошёл, лагерь пошёл дальше"),
        ("02", "выбран один воин", "опись — выбран один воин"),
        ("03", "выбрана группа", "опись — выбрана группа"),
        ("04", "выбран Греховод", "опись — выбран Греховод"),
        ("05", "наведение на «Оборону» — прогноз", "прогноз — подсказка на кнопке «Оборона»"),
        ("06", "разговор — меню, долг", "разговор — долг отдан"),
        ("06б", "разговор — снаряжение, обмен", "вещи — обмен вблизи"),
        ("07", "сундук", "склад — экран сундука"),
        ("08", "совет — выбор старшего", "совет открыт"),
        ("09", "полосы кино — реплика Каргана", "реплика с наездом"),
        ("10", "набег — охотники на виду", "мышь  луч попадает в видимого охотника"),
        ("10б", "набег — после боя", "набег  первая волна положена"),
        ("11", "плашка урока", "опись — плашка урока Exchange"),
        ("12", "приказ издали — отказы", "приказ издали  прогноз, отказы, причины"),
        ("13", "склеп — встреча, «нас мало»", "склеп  встретили — потеря и «нас мало»"),
        ("14", "мастерская — вселение", "склеп  мастерская — воин поднят"),
        ("15", "плата — панель", "склеп  спросили о плате"),
        ("15б", "плата — из рук в руки", "склеп — плата за вылазку"),
        ("16", "эпилог — три кнопки", "склеп  конец демо показан"),
        ("17", "карта вылазок", "опись — карта вылазок"),
        ("18", "пауза и меню", "опись — пауза"),
        ("19", "вопрос о сохранении", "опись — вопрос о сохранении"),
        ("20", "«Греховод пал»", "смерть  Греховод пал — экран конца"),
    ],
}


def bare(name):
    """Имя кадра без номера шага и без расширения."""
    return re.sub(r"^\d+\s+", "", os.path.splitext(name)[0])


def human(date):
    d = datetime.date.fromisoformat(date)
    return f"{d.day} {MONTHS[d.month - 1]}"


def link(path):
    """Путь для Markdown: пробелы и кириллица — так, чтобы GitHub не споткнулся."""
    return urllib.parse.quote(path.replace(os.sep, "/"), safe="/")


def entries():
    """Записи по порядку: дата, потом номер дня."""
    found = []
    if not os.path.isdir(BOOK):
        return found
    for folder in os.listdir(BOOK):
        spec = os.path.join(BOOK, folder, "запись.json")
        if os.path.isfile(spec):
            with open(spec, encoding="utf-8") as f:
                e = json.load(f)
            e["_folder"] = folder
            found.append(e)
    found.sort(key=lambda e: (e["date"], e.get("seq", 0)))
    return found


def shrink(src, dst):
    im = Image.open(src).convert("RGB")
    if im.width > WIDTH:
        im = im.resize((WIDTH, round(im.height * WIDTH / im.width)), Image.LANCZOS)
    im.save(dst, "JPEG", quality=QUALITY, optimize=True, progressive=True)


def sheet(files, dst, thumb=400, columns=4):
    """Лист миниатюр с подписями — для дней, от которых остались разрозненные кадры."""
    font = ImageFont.truetype(FONT, 15)
    cells = []
    for path, label in files:
        im = Image.open(path).convert("RGB")
        im = im.resize((thumb, round(im.height * thumb / im.width)), Image.LANCZOS)
        cells.append((im, label))
    cell_h = max(im.height for im, _ in cells) + 44
    rows = (len(cells) + columns - 1) // columns
    out = Image.new("RGB", (columns * (thumb + 8) + 8, rows * cell_h + 8), (14, 12, 11))
    draw = ImageDraw.Draw(out)
    for i, (im, label) in enumerate(cells):
        x = 8 + (i % columns) * (thumb + 8)
        y = 8 + (i // columns) * cell_h
        out.paste(im, (x, y))
        words, line, lines = label.split(), "", []
        for w in words:
            if draw.textlength(line + " " + w, font=font) > thumb and line:
                lines.append(line)
                line = w
            else:
                line = (line + " " + w).strip()
        lines.append(line)
        for k, text in enumerate(lines[:2]):
            draw.text((x + 2, y + im.height + 3 + k * 18), text, font=font, fill=(214, 204, 184))
    out.save(dst, "JPEG", quality=82, optimize=True, progressive=True)


def add(task_path):
    with open(task_path, encoding="utf-8") as f:
        task = json.load(f)

    date = task["date"]
    source = task.get("source") or WALK
    if not os.path.isabs(source):
        source = os.path.join(ROOT, source)
    files = [f for f in os.listdir(source) if f.lower().endswith((".png", ".jpg"))]

    def newest(want):
        # Один шаг под разными номерами — старый кадр остался от прежней
        # нумерации прогона; нужен свежий.
        hit = [f for f in files if bare(f) == want]
        hit.sort(key=lambda f: os.path.getmtime(os.path.join(source, f)), reverse=True)
        return hit[0] if hit else None

    wanted = []      # (ключ, имя, файл-источник)
    if task.get("set"):
        only = set(task.get("only") or [])
        for num, name, walk in SETS[task["set"]]:
            if only and num not in only:
                continue
            # Источник — либо прохождение (ищем по имени шага), либо папка,
            # где кадры уже названы по описи («06 разговор — меню, долг.png»).
            hit = next((f for f in files if os.path.splitext(f)[0] == f"{num} {name}"), None)
            hit = hit or newest(walk)
            if not hit:
                print("НЕТ кадра:", num, name, "←", walk)
                continue
            wanted.append((num, f"{num} {name}", hit))
    for fr in task.get("frames", []):
        hit = fr["file"] if fr["file"] in files else newest(bare(fr["file"]))
        if not hit:
            print("НЕТ кадра:", fr["file"])
            continue
        name = fr.get("name") or bare(fr["file"])
        wanted.append((name, name, hit))

    if not wanted:
        sys.exit("Ни одного кадра — запись не заведена.")

    # Кадр старше дня записи — от прогона до правки: не он.
    stale = []
    for key, name, file in wanted:
        shot = datetime.date.fromtimestamp(os.path.getmtime(os.path.join(source, file))).isoformat()
        if shot < date and not task.get("allow_old"):
            stale.append(f"{file} ({shot})")
    if stale:
        sys.exit("Кадры старше дня записи — прогон был до правки:\n  " + "\n  ".join(stale))

    same_day = [e for e in entries() if e["date"] == date]
    seq = max([e.get("seq", 0) for e in same_day] + [0]) + 1
    folder = f"{date} {seq} {task['folder']}"
    path = os.path.join(BOOK, folder)
    os.makedirs(path, exist_ok=True)

    record = {k: task[k] for k in ("date", "title", "note") if k in task}
    record["seq"] = seq
    record["refs"] = task.get("refs", [])
    record["verdict"] = task.get("verdict", "ждём")
    record["frames"] = []

    if task.get("sheet"):
        sheet([(os.path.join(source, f), name) for _, name, f in wanted],
              os.path.join(path, "лист.jpg"),
              task.get("thumb", 400), task.get("columns", 4))
        record["sheet"] = "лист.jpg"
        record["frames"] = [{"key": k, "name": n, "source": f} for k, n, f in wanted]
    else:
        for key, name, file in wanted:
            out = name + ".jpg"
            shrink(os.path.join(source, file), os.path.join(path, out))
            shot = datetime.datetime.fromtimestamp(os.path.getmtime(os.path.join(source, file)))
            record["frames"].append({"key": key, "name": name, "file": out, "source": file,
                                     "shot": shot.strftime("%Y-%m-%d %H:%M")})

    with open(os.path.join(path, "запись.json"), "w", encoding="utf-8") as f:
        json.dump(record, f, ensure_ascii=False, indent=2)
        f.write("\n")

    size = sum(os.path.getsize(os.path.join(path, x)) for x in os.listdir(path))
    print(f"запись: {folder} — кадров {len(wanted)}, {size / 1e6:.1f} МБ")
    index()


def commit_of(folder):
    """Коммит, которым запись вошла в репозиторий; ещё не вошла — пусто."""
    try:
        out = subprocess.run(
            ["git", "-c", "core.quotepath=false", "log", "--diff-filter=A", "--format=%h", "-1",
             "--", os.path.join("Docs", "Образцы", "летопись", folder, "запись.json")],
            cwd=ROOT, capture_output=True, text=True, encoding="utf-8")
        return out.stdout.strip()
    except OSError:
        return ""


# Папки образцов, которые шли в репозиторий до летописи.
SAMPLES = ["показ", "сцены", "облики", "предметы", "план", "переделка", "связывание", "интерфейс"]


def history():
    """Коммиты, менявшие отобранные кадры до летописи, — со ссылкой на папку в тот день."""
    paths = [os.path.join("Docs", "Образцы", p) for p in SAMPLES]
    try:
        out = subprocess.run(["git", "-c", "core.quotepath=false", "log", "--date=short",
                              "--format=%h|%ad|%s", "--"] + paths,
                             cwd=ROOT, capture_output=True, text=True, encoding="utf-8")
    except OSError:
        return []
    lines = []
    for row in out.stdout.splitlines():
        parts = row.split("|", 2)
        if len(parts) < 3:
            continue
        h, d, subject = parts
        lines.append(f"* {human(d)} — {subject} — [кадры]({REPO}/tree/{h}/{link('Docs/Образцы')})")
    return lines


def index():
    book = entries()
    last = {}        # ключ состояния → (запись, кадр): где оно снималось последним

    for e in book:
        folder = e["_folder"]
        commit = commit_of(folder)
        refs = " · ".join(e.get("refs", []))
        lines = [f"# {human(e['date'])} — {e['title']}", ""]
        if e.get("note"):
            lines += [e["note"], ""]
        tail = []
        if refs:
            tail.append(f"Записано: {refs}")
        if commit:
            tail.append(f"коммит [`{commit}`]({REPO}/commit/{commit})")
        tail.append(f"Автор: {e.get('verdict', 'ждём')}")
        lines += [". ".join(tail) + ".", ""]
        lines += ["[← летопись](../README.md)", ""]

        if e.get("sheet"):
            lines += [f"![лист кадров]({link(e['sheet'])})", ""]
            lines += ["Кадры на листе:", ""]
            lines += [f"* {fr['name']}" for fr in e["frames"]]
            lines.append("")
        elif not any(fr["key"] in last for fr in e["frames"]):
            # Снято впервые — сравнивать не с чем: кадры во всю ширину.
            for fr in e["frames"]:
                lines += [f"**{fr['name']}**", "", f"![]({link(fr['file'])})", ""]
        else:
            lines += ["| Состояние | До | После |", "|---|---|---|"]
            for fr in e["frames"]:
                before = last.get(fr["key"])
                if before:
                    b_entry, b_frame = before
                    cell = (f"![]({link('../' + b_entry['_folder'] + '/' + b_frame['file'])})"
                            f"<br>{human(b_entry['date'])}, {b_entry['title']}")
                else:
                    cell = "впервые"
                lines.append(f"| {fr['name']} | {cell} | ![]({link(fr['file'])}) |")
            lines.append("")
        if not e.get("sheet"):
            for fr in e["frames"]:
                last[fr["key"]] = (e, fr)

        with open(os.path.join(BOOK, folder, "README.md"), "w", encoding="utf-8", newline="\n") as f:
            f.write("\n".join(lines))
        e["_commit"] = commit

    head = [
        "# Летопись кадров",
        "",
        "Как игра выглядела по датам и что менялось (автор, 15 октября: «нам",
        "нужно вести учёт скриншотов по датам с изменениями»). Каждая видимая",
        "правка — запись: кадры «после»; «до» — тот же кадр из прежней записи,",
        "где состояние снималось последним. Новые — сверху.",
        "",
        "| Дата | Что изменилось | Кадров | Где записано | Автор |",
        "|---|---|---|---|---|",
    ]
    for e in reversed(book):
        refs = " · ".join(e.get("refs", []))
        commit = f" · [`{e['_commit']}`]({REPO}/commit/{e['_commit']})" if e.get("_commit") else ""
        head.append(f"| {human(e['date'])} | [{e['title']}]({link(e['_folder'] + '/README.md')}) "
                    f"| {len(e['frames'])} | {refs}{commit} | {e.get('verdict', 'ждём')} |")
    earlier = history()
    if earlier:
        head += [
            "",
            "## До летописи — кадры в истории git",
            "",
            "Отобранные кадры (показ, сцены, облики, предметы, план) шли",
            "в репозиторий и раньше. По ссылке — папка `Docs/Образцы` такой,",
            "какой она была после этого коммита.",
            "",
        ] + earlier
    head += [
        "",
        "## Как добавить запись",
        "",
        "После правки, которую видно, — прогон (`demo-walkthrough.ps1`, для",
        "интерфейса — с `-All`), потом задание и одна команда:",
        "",
        "```",
        "python Tools/shots/chronicle.py add задание.json",
        "```",
        "",
        "Формат задания и набор состояний интерфейса — в начале",
        "`Tools/shots/chronicle.py`. Кладите только состояния, которые правка",
        "изменила: «до» скрипт найдёт сам. Слово автора — поле `verdict`",
        "в `запись.json` записи, потом `python Tools/shots/chronicle.py index`.",
        "",
    ]
    with open(os.path.join(BOOK, "README.md"), "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(head))
    print("оглавление:", len(book), "записей")


if __name__ == "__main__":
    if len(sys.argv) >= 3 and sys.argv[1] == "add":
        add(sys.argv[2])
    elif len(sys.argv) >= 2 and sys.argv[1] == "index":
        index()
    else:
        print(__doc__)
