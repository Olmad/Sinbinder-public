# Tools/shots/gif.py
# Учебник с гифками (автор, 28.10: «нужен небольшой учебник с гифками — как, что и почему
# делается»). Серии кадров снимают прогон демо и проверка поля (Assets/Scripts/Editor/Reel.cs)
# в Docs/Учебник/кадры/<серия>/000.png…; здесь из каждой серии — гифка, и страница
# Docs/Учебник/учебник.html: раздел — гифка и три строки: что делаешь, что происходит, почему.
#
# Запуск: python Tools/shots/gif.py
import html
import os

from PIL import Image

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
BOOK = os.path.join(ROOT, "Docs", "Учебник")
FRAMES = os.path.join(BOOK, "кадры")
WIDTH = 640

# Сколько секунд снималась серия (Reel.Start в прогоне и проверке поля): кадр в гифке стоит
# столько, сколько прошло на самом деле, — снимок тяжёл, и кадров выходит меньше шести в секунду.
SECONDS = {"полка": 6.5, "бревно": 4.5, "полено": 4.5, "ворон": 6.0, "дверь": 5.0, "подарок": 4.5, "приказ": 6.5}

# (серия, заголовок, что делаешь, что происходит, почему)
SECTIONS = [
    ("Лагерь", None),
    ("полка", "F — по взгляду",
     "Смотришь на вещь — глазами (середина кадра) или мышью сверху — и жмёшь F.",
     "Подсказка внизу называет то, на что смотришь: эту пластинку, ту, граммофон. F берёт её.",
     "Рядом бывает много вещей сразу. Прежде F доставалось ближайшей — теперь той, что под взглядом."),
    ("бревно", "Сесть у огня",
     "Смотришь на бревно у костра — «F — сесть на бревно».",
     "Греховод садится на ближнюю половину лицом к огню. Шаг, приказ мышью или F — встаёт.",
     "Лагерь — место, где живут, а не только проходят. Сидя, до огня рука достаёт: полено можно бросить, не вставая."),
    ("полено", "Полено в костёр",
     "Смотришь на огонь — «F — подбросить полено».",
     "Вспышка, искры, треск — и кто-то у огня отзывается вслух, своим голосом.",
     "Лагерь отзывается: по ответу видно, кто какой. Каждый говорит раз за вечер."),
    ("ворон", "Ворон Каргана",
     "Ничего — смотри.",
     "Раз в сто секунд ворон слетает с плеча Каргана: круг над ним, конёк ближней палатки, и назад.",
     "Ворон прибился к Каргану ещё при жизни и не ушёл после смерти. Карган падёт — ворон слетит к огню."),
    ("Перетягивание каната", None),
    ("дверь", "Склеп и дверь",
     "Изнутри склепа смотришь на дверь — «F — открыть дверь склепа».",
     "Створка распахивается внутрь. Закрытая держит путь: ни войти, ни выйти. Наведи мышь на трон или дверь — их прочность.",
     "Конец партии — разрушенный трон. Дверь — первое, что держит натиск."),
    ("подарок", "Расположить ничьего: подарок",
     "Подходишь к ничьему, смотришь на него — «F — дать монету». В кошеле партии 20 монет.",
     "Жадный рад больше всех, гордый — почти никак («Я не нищий»). Второй подарок тому же — вдвое дешевле. Завистливый рядом, кому не дали, обижен.",
     "Воины на карте ничьи: слушаются того, к кому верны. Верность ко всем своя — и каждый грех ценит своё."),
    ("приказ", "Приказ по душе",
     "Выделяешь ничьего и приказываешь, как своему.",
     "Послушал — привыкает слушать. Приказ по душе поднимает верность сильнее: гордому — ему одному, гневному — в бой, унылому — недалеко.",
     "Слушаться он не обязан: голосует сам, верностью к тебе. Подсказка при наведении скажет словом, как он к тебе."),
]

CSS = """
:root { --bg:#14110f; --card:#1e1a17; --ink:#e9e1d2; --dim:#a89e8c; --gold:#c9a45c; }
body { margin:0; background:var(--bg); color:var(--ink); font:16px/1.55 Georgia, 'PT Serif', serif; }
main { max-width:760px; margin:0 auto; padding:24px 16px 60px; }
h1 { color:var(--gold); font-weight:normal; letter-spacing:.04em; }
h2 { color:var(--gold); font-weight:normal; border-bottom:1px solid #3a322a; padding-bottom:4px; margin-top:42px; }
section { background:var(--card); border:1px solid #2f2822; border-radius:8px; padding:14px 16px; margin:18px 0; }
section h3 { margin:0 0 10px; font-weight:normal; font-size:20px; }
img { width:100%; height:auto; border-radius:4px; display:block; margin-bottom:10px; background:#000; }
dl { margin:0; display:grid; grid-template-columns:auto 1fr; gap:4px 12px; }
dt { color:var(--dim); }
dd { margin:0; }
.none { color:var(--dim); font-style:italic; margin-bottom:10px; }
"""


def gif(series):
    folder = os.path.join(FRAMES, series)
    if not os.path.isdir(folder):
        return None
    files = sorted(f for f in os.listdir(folder) if f.endswith(".png"))
    if len(files) < 2:
        return None
    frames = []
    for f in files:
        im = Image.open(os.path.join(folder, f)).convert("RGB")
        im = im.resize((WIDTH, round(im.height * WIDTH / im.width)), Image.LANCZOS)
        frames.append(im.quantize(colors=128, method=Image.MEDIANCUT))
    out = os.path.join(BOOK, series + ".gif")
    step = int(1000 * SECONDS.get(series, len(frames) / 6.0) / len(frames))
    frames[0].save(out, save_all=True, append_images=frames[1:], duration=step, loop=0, optimize=True)
    print("[УЧЕБНИК] {}: кадров {}, {:.1f} МБ".format(series, len(frames), os.path.getsize(out) / 1e6))
    return series + ".gif"


def page():
    parts = ["<!doctype html><html lang='ru'><head><meta charset='utf-8'>",
             "<meta name='viewport' content='width=device-width, initial-scale=1'>",
             "<title>Учебник Sinbinder</title><style>" + CSS + "</style></head><body><main>",
             "<h1>Sinbinder — учебник</h1>",
             "<p>Кадры сняты из самой игры прогоном: учебник обновляется вместе с ней.</p>"]
    for s in SECTIONS:
        if s[1] is None:
            parts.append("<h2>{}</h2>".format(html.escape(s[0])))
            continue
        series, title, do, see, why = s
        g = gif(series)
        parts.append("<section><h3>{}</h3>".format(html.escape(title)))
        parts.append("<img src='{}' alt='{}'>".format(g, html.escape(title)) if g
                     else "<div class='none'>Кадров этой серии ещё нет — снимет следующий прогон.</div>")
        parts.append("<dl><dt>Что делаешь</dt><dd>{}</dd><dt>Что происходит</dt><dd>{}</dd>"
                     "<dt>Почему</dt><dd>{}</dd></dl></section>".format(*map(html.escape, (do, see, why))))
    # Обучение «Перетягиванию» — кадры плашек девяти уроков (TutorialRun).
    lessons = os.path.join(FRAMES, "обучение")
    if os.path.isdir(lessons):
        parts.append("<h2>Обучение «Перетягиванию» — девять уроков</h2>")
        for i in range(1, 10):
            src = os.path.join(lessons, "урок %d.png" % i)
            if not os.path.exists(src):
                continue
            im = Image.open(src).convert("RGB")
            im = im.resize((WIDTH, round(im.height * WIDTH / im.width)), Image.LANCZOS)
            name = "урок-%d.jpg" % i
            im.save(os.path.join(BOOK, name), quality=86)
            parts.append("<section><img src='{}' alt='урок {}'></section>".format(name, i))
    parts.append("</main></body></html>")
    out = os.path.join(BOOK, "учебник.html")
    with open(out, "w", encoding="utf-8") as f:
        f.write("\n".join(parts))
    print("[УЧЕБНИК] страница: " + out)


if __name__ == "__main__":
    os.makedirs(BOOK, exist_ok=True)
    page()
