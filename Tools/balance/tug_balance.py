# Tools/balance/tug_balance.py
# Баланс «Перетягивания» без Unity (12-BALANCE.md, раздел «Перетягивание»):
#  1. ценность развилок книги экспедиций (ExpeditionBook.cs) в одной мере — «монетах»;
#  2. честность мест событий (формула EventDirector) для каждого склепа на каждой карте (Maps/*.json).
# Запуск: python Tools/balance/tug_balance.py  → печатает таблицы в Markdown.
import io, json, math, os, re, sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
BOOK = os.path.join(ROOT, "Assets", "Scripts", "Gameplay", "ExpeditionBook.cs")
MAPS = os.path.join(ROOT, "Maps")

# ---------------------------------------------------------------- мера ценности, в монетах
# Монета — то, чем Греховод платит за подарок (≈3 монеты — заметный сдвиг верности жадного).
TROPHY = {"GolemRecipe": 15, "Jar": 6, "Treasury": 10, "Forge": 10, "Pillory": 8, "Coins": 8}
JAR, STONE, BONES, FLESH, SOUL = 6, 4, 2, 2, 5
RECRUIT_AT_62 = 12          # почти преданный новый человек
FOLLOWERS = 3               # пошедших в среднем
PER_POINT = 0.25            # монет за пункт верности одного пошедшего
VILLAGERS, PER_VILLAGER_POINT = 10, 0.08
PIOUS_SHARE = 0.3           # доля набожных среди людей
DELAY = 0.05                # монет за секунду отсрочки охотников


def blocks(text, start):
    """Содержимое фигурных скобок, начиная с '{' в позиции start."""
    depth, i = 0, start
    while i < len(text):
        if text[i] == "{": depth += 1
        elif text[i] == "}":
            depth -= 1
            if depth == 0: return text[start + 1:i], i
        i += 1
    raise ValueError("скобки")


def field(body, name, default=None):
    m = re.search(r"\b%s\s*=\s*([^,}]+?)\s*(,|$)" % name, body)
    if not m: return default
    v = m.group(1).strip()
    if v in ("true", "false"): return v == "true"
    v = v.rstrip("f")
    try: return float(v)
    except ValueError: return v


def array(body, name):
    m = re.search(r"\b%s\s*=\s*new\[\]\s*\{([^}]*)\}" % name, body)
    if not m:
        m2 = re.search(r"\b%s\s*=\s*(Selfish|Proud|Cargo|Schemes)\b" % name, body)
        return {"Selfish": ["Greed", "Envy", "Lust", "Gluttony"], "Proud": ["Pride", "Wrath"],
                "Cargo": list(TROPHY), "Schemes": ["Treasury", "Forge", "Pillory"]}.get(m2.group(1), []) if m2 else []
    return [x.split(".")[-1].strip() for x in m.group(1).split(",") if x.strip()]


def option(body):
    o = {k: field(body, k, 0) for k in ("Cost", "CoinsMin", "CoinsMax", "Stone", "Bones", "Flesh", "Souls", "Jars",
                                        "Recruits", "Followers", "Pleased", "Vexed", "Residents", "Pious", "DelayHunters")}
    o["RecruitTrust"] = field(body, "RecruitTrust", 50)
    for k in ("SplitResidents", "Deserters", "Ransom", "Gamble"): o[k] = bool(field(body, k, False))
    o["Trophy"] = array(body, "Trophy")
    o["Favour"] = array(body, "Favour")
    o["PleasedSins"] = array(body, "PleasedSins")
    o["VexedSins"] = array(body, "VexedSins")
    o["Label"] = re.search(r'Label\s*=\s*Loc\.N\("([^"]*)"\)', body).group(1)
    return o


def value(o, camp=0):
    v = -o["Cost"]
    coins = (o["CoinsMin"] + o["CoinsMax"]) / 2
    if o["Gamble"]: coins = o["CoinsMax"] * 2 / 3
    v += coins
    if o["Trophy"]: v += sum(TROPHY[t] for t in o["Trophy"]) / len(o["Trophy"])
    v += o["Jars"] * JAR + o["Stone"] * STONE + o["Bones"] * BONES + o["Flesh"] * FLESH + o["Souls"] * SOUL
    v += o["Recruits"] * RECRUIT_AT_62 * o["RecruitTrust"] / 62
    if o["Deserters"]: v += camp * RECRUIT_AT_62 * o["RecruitTrust"] / 62
    v += o["Followers"] * FOLLOWERS * PER_POINT
    v += (o["Pleased"] - o["Vexed"]) * PER_POINT          # примерно один пошедший того греха
    if not o["SplitResidents"]: v += o["Residents"] * VILLAGERS * PER_VILLAGER_POINT
    v += o["Pious"] * (VILLAGERS + FOLLOWERS) * PIOUS_SHARE * PER_VILLAGER_POINT
    v += o["DelayHunters"] * DELAY
    return v


def book():
    text = io.open(BOOK, encoding="utf-8").read()
    out = []
    for m in re.finditer(r'new\(\)\s*\{\s*Id\s*=\s*"(\w+)"', text):
        body, _ = blocks(text, text.index("{", m.start()))
        a = body.index("A = new()"); b = body.index("B = new()")
        ab, _ = blocks(body, body.index("{", a)); bb, _ = blocks(body, body.index("{", b))
        guards = field(body[:a], "Guards", 0) or 0
        camp = field(body[:a], "Camp", 0) or 0
        out.append({"id": m.group(1), "fight": guards > 0, "guards": guards, "A": option(ab), "B": option(bb), "camp": camp})
    return out


# ---------------------------------------------------------------- места событий (как EventDirector.Configure)
FROM_CRYPT, APART, FROM_VILLAGE, OWN_REACH = 30, 16, 36, 70


def places(m):
    crypts = [(c["X"], c["Z"]) for c in m["Crypts"]]
    edge = m.get("Size", 400) * 0.5 - 12
    wish = [(s["X"], s["Z"]) for s in m.get("Events", [])]
    for i in range(len(crypts)):
        for k in range(i + 1, len(crypts)):
            wish.append(((crypts[i][0] + crypts[k][0]) / 2, (crypts[i][1] + crypts[k][1]) / 2))
    for c in crypts:
        to = (-c[0], -c[1]); n = math.hypot(*to) or 1.0
        reach = min(OWN_REACH, n * 0.55)
        for turn in (-40, 40):
            a = math.radians(-turn)   # Quaternion.Euler(0, turn, 0) в Unity — по часовой, если смотреть сверху
            x, z = to[0] / n, to[1] / n
            wish.append((c[0] + (x * math.cos(a) - z * math.sin(a)) * reach, c[1] + (x * math.sin(a) + z * math.cos(a)) * reach))
    for i, l in enumerate(m.get("Locations", [])):
        r = l.get("Radius", 18)
        for k in range(2):
            a = math.radians(i * 73 + k * 180 + 45)
            wish.append((l["X"] + math.cos(a) * (r + FROM_VILLAGE + 4), l["Z"] + math.sin(a) * (r + FROM_VILLAGE + 4)))
    got = []
    for p in wish:
        if abs(p[0]) > edge or abs(p[1]) > edge: continue
        if any(math.dist(p, c) < FROM_CRYPT for c in crypts): continue
        if any(math.dist(p, (l["X"], l["Z"])) < l.get("Radius", 18) + FROM_VILLAGE for l in m.get("Locations", [])): continue
        if any(math.dist(p, q) < APART for q in got): continue
        got.append(p)
    return crypts, got


def main():
    lines = ["### Развилки экспедиций — ценность в монетах", "",
             "| Род | Бой | A | B | A, мон. | B, мон. | Перевес |", "|---|---|---|---|---:|---:|---|"]
    skew = []
    for e in book():
        va, vb = value(e["A"], e["camp"]), value(e["B"], e["camp"])
        gap = abs(va - vb)
        note = "—"
        if gap > 8 and not e["fight"]:
            note = "**мирное: выбор очевиден**"; skew.append(e["id"])
        elif gap > 12:
            note = "велик (решает лидер — терпимо)"
        lines.append(f"| {e['id']} | {'да' if e['fight'] else 'нет'} | {e['A']['Label']} | {e['B']['Label']} | {va:.1f} | {vb:.1f} | {note} |")
    lines += ["", f"Мирных с очевидным выбором (разница > 8 мон.): {', '.join(skew) if skew else 'нет'}.", ""]

    lines += ["### Места событий — честность для склепов", ""]
    for path in sorted(os.listdir(MAPS)):
        if not path.endswith(".json"): continue
        m = json.load(io.open(os.path.join(MAPS, path), encoding="utf-8-sig"))
        crypts, got = places(m)
        lines.append(f"**{m.get('Name') or path[:-5]}** — мест {len(got)}, склепов {len(crypts)}")
        lines.append("")
        lines.append("| Склеп | ближних мест | до ближнего, м | среднее до трёх ближних, м |")
        lines.append("|---|---:|---:|---:|")
        nearest = [0] * len(crypts)
        for p in got:
            d = [math.dist(p, c) for c in crypts]
            if min(d) < sorted(d)[1] - 5 if len(d) > 1 else True: nearest[d.index(min(d))] += 1
        for i, c in enumerate(crypts):
            ds = sorted(math.dist(p, c) for p in got)
            lines.append(f"| {i + 1} | {nearest[i]} | {ds[0]:.0f} | {sum(ds[:3]) / min(3, len(ds)):.0f} |" if ds else f"| {i + 1} | 0 | — | — |")
        lines.append("")
    print("\n".join(lines))


if __name__ == "__main__":
    main()
