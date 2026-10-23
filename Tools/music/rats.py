# «Крысы» — тема отряда Безымянного, сюита.
#
# Автор, 23 октября: «Крысы прекрасные, просто великолепные. Я думаю „Крысы“ —
# тема отряда Безымянного, потому что они сбежали и теперь пытаются просто
# выжить, бегая по углам, прячась от опасности. И отдаёт лёгким лесом под стать
# окружению». И следом: «Наверное нужен комплект мелодий этой темы. Хотя бы
# чтобы я слушал и мне было здорово».
#
# Восемь пьес — одна ночь отряда по порядку: у огня, тропа, затаились,
# удирают, отбиваются, делят добычу, крысиная песня, колыбельная. Везде мотив автора
# из «The Rat Pack» — фагот ре–фа–соль-диез–ми–ре (ре–фа–соль-диез — уменьшённое
# трезвучие: крысы бегут по нему) — и лесной состав: фагот, кларнет, пиццикато,
# флейта внизу, гобой, деревянные коробочки. Меди и хора почти нет: это не
# войско Владыки, а те, кто от войск прячется. Меняются темп, гармония и голос,
# который ведёт мотив.
#
# Уроки прежних тем — и здесь: высокое — только краской; гармония ходит;
# пульс дышит; у каждой пьесы своё начало и свой конец (это для слушания,
# а не петли; петля — у «Отбиваются», она может стать первой волной набега).
#
#   python3 Tools/music/rats.py <пьеса> выход.mid [--loop]
#   пьесы: fire trail hide flee fight loot rat lullaby
import sys
from score import loop_tempo
from orchestra import (Orchestra, CHORD, bar, HORN, STR, PAD, CELLO, BASS, BONE,
                       TIMP, ORGAN, HARP, CEL, HARPSI, PICC, PIANO,
                       BD, WOOD_HI, WOOD_LO)
from battles import drum

# Каналы сюиты: фагот — на канале челесты, кларнет — клавесина, пиццикато —
# арфы, флейта — пикколо, гобой — органа (каналов пятнадцать, и все заняты).
BSN, CLAR, PIZZ, FLUTE, OBOE = CEL, HARPSI, HARP, PICC, ORGAN
FOREST = {BSN: 70, CLAR: 71, PIZZ: 45, FLUTE: 73, OBOE: 68}

# Мотив автора: (нота, начало, длина) в долях.
MOTIF = [(50, 0, 1), (53, 1, 1), (56, 2, 1), (52, 3, 1), (50, 4, 2)]


def motif(s, ch, at, vel, root=50, k=1.0, upto=5, hold=0.0):
    """Мотив от ноты root, растянутый в k раз; upto — сколько нот сыграть
    (меньше пяти — оборвался, как дыхание); hold — продлить последнюю."""
    notes = MOTIF[:upto]
    for i, (p, st, d) in enumerate(notes):
        s.add(ch, p - 50 + root, at + st * k, d * k + (hold if i == len(notes) - 1 else 0), vel + (6 if i == 0 else 0))


def walk(s, at, roots, vel=60, breathe=4, fifth=7):
    """Пиццикато шагом: корень и квинта четвертями; такт тишины через breathe-1."""
    for b, r in enumerate(roots):
        if breathe and b % breathe == breathe - 1: continue
        for q in range(4):
            s.add(PIZZ, r if q % 2 == 0 else r + fifth, at + b * 4 + q, 0.45, vel + (8 if q == 0 else 0))


def feet(s, at, bars, vel=50, every=2):
    """Лапки — коробочки шестнадцатыми 3+3+2, краской: раз в every тактов."""
    for b in range(0, bars, every):
        for k in (0, 3, 6, 8, 11, 14):
            drum(s, WOOD_HI if k % 2 == 0 else WOOD_LO, at + b * 4 + k * 0.25, vel + (8 if k == 0 else 0), 0.25)


def chord(s, ch, at, name, beats, vel):
    for p in CHORD[name]['mid']:
        s.add(ch, p, at, beats, vel)


def bass(s, at, names, vel=66, beats=4):
    for i, name in enumerate(names):
        s.add(BASS, CHORD[name]['bass'], at + i * 4, beats, vel)
        s.add(CELLO, CHORD[name]['cello'], at + i * 4, beats, vel - 12)


# ---------- 1. У огня ----------

def fire():
    """Ночь, лагерь, огонь. Фагот ведёт мотив вдвое медленнее — устало,
    без бега; кларнет отвечает спуском; гобой — тёплый голос в середине.
    Кончается ре мажором: беглецы, но живы."""
    s = Orchestra({BSN: 108, CLAR: 92, PIZZ: 80, OBOE: 86, PAD: 60, CELLO: 80, BASS: 84}, program=FOREST)
    A = ['Dm', 'Dm', 'Bb', 'Bb', 'Gm', 'Gm', 'A', 'A']
    # 1–4: пиццикато половинками, тёплые струнные
    # Вступление не тише остального: фагот, вошедший после шёпота, прыгал
    # на +24 дБ (master.py: «испуг»).
    for b in range(4):
        s.add(PIZZ, 38, bar(1) + b * 4, 1, 72); s.add(PIZZ, 45, bar(1) + b * 4 + 2, 1, 64)
    s.pads(PAD, bar(1), ['Dm', 'Dm', 'Gm', 'A'], 58)
    for i, name in enumerate(['Dm', 'Dm', 'Gm', 'A']):
        s.add(CELLO, CHORD[name]['cello'], bar(1) + i * 4, 4, 58)
    # 5–12: мотив фагота вдвое медленнее, кларнет спускается в ответ
    motif(s, BSN, bar(5), 76, k=2.0)
    s.line(CLAR, [(69, 0, 2), (67, 2, 2), (65, 4, 2), (64, 6, 2), (62, 8, 4)], bar(7) + 2, 74)
    motif(s, BSN, bar(9), 90, root=46, k=2.0)
    s.line(CLAR, [(65, 0, 2), (64, 2, 2), (62, 4, 2), (61, 6, 6)], bar(11) + 2, 72)
    s.pads(PAD, bar(5), A, 42)
    bass(s, bar(5), A, 62)
    # 13–20: фа мажор — гобой поёт мотив, фагот под ним держит корни
    F = ['F', 'F', 'Dm', 'Dm', 'Bb', 'Bb', 'A', 'A']
    motif(s, OBOE, bar(13), 84, root=65, k=2.0)
    motif(s, OBOE, bar(17), 80, root=62, k=2.0, upto=4, hold=4)
    for b, name in enumerate(F):
        s.add(BSN, CHORD[name]['cello'], bar(13) + b * 4, 3.5, 66)
    s.pads(PAD, bar(13), F, 44)
    bass(s, bar(13), F, 60)
    # 21–24: мотив снова у фагота — и ре мажор, тихо
    motif(s, BSN, bar(21), 84, k=2.0, upto=4)
    s.add(BSN, 50, bar(23), 8, 78)
    s.add(CLAR, 66, bar(23), 8, 62)                   # фа-диез: ре мажор, а не минор
    chord(s, PAD, bar(23), 'D', 8, 46)
    s.add(BASS, 26, bar(23), 8, 60)
    s.add(PIZZ, 38, bar(25), 1, 56)
    return s, 72, bar(25)


# ---------- 2. Тропа ----------

def trail():
    """Отряд идёт лесом. Пиццикато шагом, мотив коротко — у фагота, ответ
    кларнета на кварту выше, флейта внизу перекликается, как птица.
    В середине фа мажор — светлая поляна."""
    s = Orchestra({BSN: 110, CLAR: 96, PIZZ: 96, FLUTE: 82, CELLO: 84, BASS: 88, TIMP: 80,
                   PAD: 56, 9: 90}, program=FOREST)
    D = [38, 38, 34, 34, 31, 31, 33, 33]
    Am = [33, 33, 29, 29, 28, 28, 33, 33]
    F = [41, 41, 36, 36, 34, 34, 36, 33]

    def call(at, vel=66):
        """Птица: флейта внизу — ля, ре, коротко."""
        s.add(FLUTE, 69, at, 0.4, vel); s.add(FLUTE, 74, at + 0.5, 0.9, vel - 4)

    # A — 1–8
    walk(s, bar(1), D, 62)
    motif(s, BSN, bar(1) + 1, 92); motif(s, CLAR, bar(3) + 1, 82, root=55)
    call(bar(4) + 2)
    motif(s, BSN, bar(5) + 1, 94, root=46); motif(s, CLAR, bar(7) + 1, 84, root=51)
    call(bar(8) + 2, 70)
    # B — 9–16: ля минор, кларнет ведёт, коробочки краской
    walk(s, bar(9), Am, 60)
    motif(s, CLAR, bar(9) + 1, 88, root=57); motif(s, BSN, bar(11) + 1, 84, root=45)
    motif(s, CLAR, bar(13) + 1, 90, root=53); motif(s, BSN, bar(15), 82, root=45, upto=4, hold=1)
    feet(s, bar(10), 6, 46, every=4)
    # C — 17–24: фа мажор, флейта и кларнет терциями — мотив в мажоре
    walk(s, bar(17), F, 58)
    MAJ = [(65, 0, 1), (69, 1, 1), (72, 2, 1), (67, 3, 1), (65, 4, 2)]   # фа–ля–до–соль–фа
    for k, at in enumerate((bar(17), bar(19), bar(21))):
        s.line(FLUTE, MAJ, at + 1, 70 + 2 * k)
        s.line(CLAR, MAJ, at + 1, 72, shift=-3)
    s.add(BSN, 53, bar(23), 4, 76); s.add(BSN, 52, bar(24), 4, 76)
    s.pads(PAD, bar(17), ['F', 'F', 'C', 'C', 'Bb', 'Bb', 'C', 'A'], 36)
    # D — 25–32: снова ре, канон фагот — кларнет, литавры легко
    walk(s, bar(25), D, 64)
    for k in range(4):
        motif(s, BSN, bar(25) + k * 8 + 1, 92, root=[50, 46, 43, 45][k])
        motif(s, CLAR, bar(25) + k * 8 + 3, 80, root=[55, 51, 48, 50][k])
    for b in range(0, 8, 2): s.add(TIMP, 38 if b % 4 == 0 else 45, bar(25) + b * 4, 0.5, 60)
    call(bar(28) + 2); call(bar(32) + 2, 70)
    # E — 33–36: уходят — мотив тише, последняя птица
    walk(s, bar(33), [38, 38, 33, 38], 52, breathe=0)
    motif(s, BSN, bar(33) + 1, 76); motif(s, BSN, bar(35) + 1, 66, upto=4, hold=2)
    call(bar(36) + 3, 60)
    return s, 104, bar(37)


# ---------- 3. Затаились ----------

def hide():
    """Опасность рядом. Почти тишина: пиццикато по одной ноте, тритон ре —
    соль-диез; мотив у кларнета внизу обрывается на соль-диез — дыхание
    затаено. Сердце литавр еле слышно. Конец не разрешается: пронесло ли —
    неизвестно."""
    s = Orchestra({BSN: 100, CLAR: 96, PIZZ: 84, PAD: 56, TIMP: 84, CELLO: 80, BASS: 84}, program=FOREST)
    # 1–4: шаги вдалеке — пиццикато ре… соль-диез
    for b in range(4):
        s.add(PIZZ, 38, bar(1) + b * 4, 0.6, 56)
        if b % 2: s.add(PIZZ, 44, bar(1) + b * 4 + 2, 0.6, 50)
    for b in range(0, 22, 2): s.add(TIMP, 38, bar(1) + b * 4, 0.6, 56)
    # Тишина — но не пустота: низкие струнные с первого такта. Без них кларнет
    # входил после десяти тихих секунд с прыжком на +33 дБ.
    s.add(BASS, 26, bar(1), 40, 50); s.add(CELLO, 38, bar(1), 16, 44)
    for p in (50, 53): s.add(PAD, p, bar(1), 16, 30)
    # 5–10: кларнет внизу — мотив медленно, обрыв на соль-диез
    motif(s, CLAR, bar(5), 56, root=50, k=2.0, upto=3, hold=4)
    for p in (47, 56): s.add(PAD, p, bar(5), 24, 30)
    s.swell(PAD, bar(5), bar(11), 40, 90)
    # 11–14: фагот отвечает ми — ре: выдох
    s.add(BSN, 52, bar(11), 3, 60); s.add(BSN, 50, bar(11) + 3, 6, 58)
    s.swell(PAD, bar(11), bar(15), 90, 30)
    s.add(BASS, 26, bar(11), 48, 46)                   # педаль до конца: без неё — дыра цифровой тишины
    s.add(CELLO, 38, bar(13), 8, 40)
    # 15–20: снова — ближе: кларнет выше, пиццикато чаще, обрыв дольше
    for b in range(6):
        s.add(PIZZ, 44 if b % 2 else 38, bar(15) + b * 4, 0.5, 58)
        s.add(PIZZ, 44 if b % 2 == 0 else 38, bar(15) + b * 4 + 2.5, 0.5, 52)
    motif(s, CLAR, bar(15), 72, root=53, k=2.0, upto=3, hold=6)
    for p in (47, 50, 53, 56): s.add(PAD, p, bar(15), 32, 30)
    s.swell(PAD, bar(15), bar(21), 30, 100)
    # 21–22: соль-диез у фагота висит в тишине — и всё
    s.add(BSN, 44, bar(21), 8, 60)
    s.swell(PAD, bar(21), bar(23), 100, 0)
    return s, 60, bar(23)


# ---------- 4. Удирают ----------

def flee():
    """Бегом. Мотив восьмыми вверх по малым терциям — круг без выхода (как
    в «The Rat Pack»); канон фагота и кларнета, лапки, пиццикато восьмыми.
    Потом мотив вниз — нашли щель. В конце — проскользнули, тишина."""
    s = Orchestra({BSN: 112, CLAR: 96, PIZZ: 94, TIMP: 90, PAD: 56, BASS: 88, CELLO: 84, 9: 96}, program=FOREST)
    up = [50, 53, 56, 59]
    down = [59, 56, 53, 50]
    for half, roots in ((0, up), (1, up), (2, down), (3, down)):
        at = bar(1 + half * 8)
        feet(s, at, 8, 52, every=1 if half in (1, 2) else 2)
        for b in range(8):
            r = roots[b % 4]
            motif(s, BSN, at + b * 4, 90, root=r, k=0.5)
            motif(s, CLAR, at + b * 4 + 2, 78, root=r + 5, k=0.5)
            if b % 4 != 3:
                for q in range(8):
                    s.add(PIZZ, r - 12 if q % 2 == 0 else r - 5, at + b * 4 + q * 0.5, 0.3, 58)
        s.roll(38, at + 7 * 4, 4, 50, 84)
        for p in (47, 50, 53, 56): s.add(PAD, p, at, 32, 34)
    # конец — проскользнули: мотив тише и ниже, последняя нота пиццикато
    motif(s, BSN, bar(33), 70, root=46, k=0.5, upto=4)
    s.add(PIZZ, 26, bar(34), 1, 66)
    return s, 150, bar(35)


# ---------- 5. Отбиваются ----------

def fight():
    """Стычка с побитыми охотниками — кандидат в первую волну набега
    (`43-SOUND.md`). Как «Шуганули», но мотив — отряда, а не Владыки:
    отбиваются сами. Фагот и кларнет, пиццикато шагом, короткие удары
    тромбона внизу — отряд огрызается; дважды тромбон «уа-уа-уа» над
    удирающим. Петля 36 тактов (--loop).

    23 октября, автор: «мелодия сама себя перебивает и без смеха не то».
    Кларнет вступал, пока фагот ещё не допел (наложение на две и на четыре
    доли). Теперь — по очереди, как разговор: фагот договорил, в паузе
    кларнет хихикает (тем же смешком, что в «Делят добычу»), отвечает
    кларнет — и отряд огрызается тромбоном."""
    s = Orchestra({BSN: 110, CLAR: 96, PIZZ: 96, CELLO: 88, BASS: 94, TIMP: 90, BONE: 98,
                   PAD: 54, 9: 94}, program=FOREST)
    D = [38, 38, 34, 34, 31, 31, 33, 33]
    F = [41, 41, 36, 36, 34, 34, 36, 33]

    def snap(at, root=38, vel=80):
        """Огрызнулись: тромбон коротко внизу с литаврой."""
        s.add(BONE, root, at, 0.4, vel); s.add(BONE, root + 7, at, 0.4, vel - 8)
        s.add(TIMP, 38 if root % 12 == 2 else 45, at, 0.4, vel - 6)

    def wah(at, vel=90):
        for k, p in enumerate((45, 44, 43)):
            s.add(BONE, p, at + k * 0.75, 0.6, vel - k * 4)
        s.add(BONE, 42, at + 2.25, 1.6, vel - 14)

    def giggle(at, root=62, vel=74):
        """Смешок кларнета: форшлаг и короткая нота, дважды."""
        for k in (0, 0.75):
            s.add(CLAR, root + 1, at + k, 0.1, vel - 8); s.add(CLAR, root, at + k + 0.1, 0.3, vel)

    def talk(at, roots, vel):
        """Разговор по два такта: фагот — смешок — кларнет — тромбон огрызнулся.
        Мотив длится шесть долей; две оставшиеся — пауза, в ней и смех."""
        for k, r in enumerate(roots):
            t = at + k * 8
            if k % 2 == 0:
                motif(s, BSN, t, vel, root=r)
                giggle(t + 6.25, 62 + (k % 4))
            else:
                motif(s, CLAR, t, vel - 8, root=r + 5)
                snap(t + 6.5, CHORD[['Dm', 'Bb', 'Gm', 'A'][k % 4]]['bass'] + 12)

    # A — 1–8: фагот, смешок, кларнет отвечает, тромбон огрызается
    walk(s, bar(1), D, 62)
    talk(bar(1), [50, 50, 46, 45], 92)
    # B — 9–16: погоня — мотив восьмыми, лапки
    walk(s, bar(9), D, 66)
    feet(s, bar(9), 8, 52)
    for b in range(8):
        motif(s, BSN if b % 2 == 0 else CLAR, bar(9) + b * 4, 88, root=[50, 53, 56, 59][b % 4] + (0 if b % 2 == 0 else 5), k=0.5)
    snap(bar(13), 34); wah(bar(16))
    # C — 17–24: фа мажор — отбились на миг
    walk(s, bar(17), F, 60)
    MAJ = [(65, 0, 1), (69, 1, 1), (72, 2, 1), (67, 3, 1), (65, 4, 2)]
    s.line(CLAR, MAJ, bar(17) + 1, 84, shift=-12); s.line(BSN, MAJ, bar(19) + 1, 86, shift=-12)
    s.line(CLAR, MAJ, bar(21) + 1, 86, shift=-10); s.line(BSN, MAJ, bar(23) + 1, 84, shift=-15)
    s.pads(PAD, bar(17), ['F', 'F', 'C', 'C', 'Bb', 'Bb', 'C', 'A'], 34)
    # D — 25–32: снова наседают — всё вместе
    walk(s, bar(25), D, 68)
    feet(s, bar(25), 8, 54, every=2)
    talk(bar(25), [50, 46, 43, 45], 96)
    for b in range(0, 8, 2): s.add(TIMP, 38 if b % 4 == 0 else 45, bar(25) + b * 4 + 2, 0.5, 64)
    # E — 33–36: выдох, тромбон — к петле
    walk(s, bar(33), [38, 38, 33, 33], 56)
    motif(s, BSN, bar(33) + 1, 82)
    wah(bar(35), 84)
    s.add(TIMP, 38, bar(36) + 3, 0.5, 66)
    return s, 136, bar(37)


# ---------- 6. Делят добычу ----------

def loot():
    """После стычки — делят, что нашли. Смешно: пиццикато «ум-па», фагот
    считает мотив повторёнными нотами (ре-ре, фа-фа, соль-диез-соль-диез —
    как монеты), кларнет хихикает форшлагами. Жадность Марги — с улыбкой."""
    s = Orchestra({BSN: 112, CLAR: 96, PIZZ: 96, CELLO: 86, BASS: 90, TIMP: 76, PAD: 50, 9: 80}, program=FOREST)
    ROOTS = ['Dm', 'Dm', 'Gm', 'A', 'Dm', 'Bb', 'Gm', 'A']

    def umpa(at, names, vel=64):
        for i, name in enumerate(names):
            c = CHORD[name]
            t = at + i * 4
            s.add(PIZZ, c['cello'], t, 0.4, vel + 8); s.add(PIZZ, c['cello'] + 7, t + 2, 0.4, vel)
            for q in (1, 3):
                for p in c['mid']: s.add(PIZZ, p, t + q, 0.3, vel - 12)

    def count(ch, at, root, vel):
        """Мотив, пересчитанный по монетке: каждая нота дважды восьмыми."""
        for i, (p, st, d) in enumerate(MOTIF[:4]):
            for h in (0, 0.5):
                s.add(ch, p - 50 + root, at + st + h, 0.35, vel + (6 if h == 0 else -4))
        s.add(ch, root, at + 4, 1.5, vel)

    def giggle(at, root=62, vel=72):
        """Хихиканье кларнета: форшлаг и короткая нота, дважды."""
        for k in (0, 0.75):
            s.add(CLAR, root + 1, at + k, 0.1, vel - 8); s.add(CLAR, root, at + k + 0.1, 0.3, vel)

    for half in range(3):
        at = bar(1 + half * 8)
        umpa(at, ROOTS, 62 + 2 * half)
        for k in range(4):
            count(BSN, at + k * 8, [50, 46, 43, 45][k] if half != 1 else [50, 50, 46, 45][k], 88)
            giggle(at + k * 8 + 6, 62 + (3 if half == 1 else 0))
        if half == 1:
            for b in range(0, 8, 2): drum(s, WOOD_HI, at + b * 4 + 3.5, 58, 0.25)   # монетка упала
    # конец — последний пересчёт и «хм» фагота внизу
    count(BSN, bar(25), 50, 80)
    s.add(BSN, 38, bar(27), 2, 84)
    s.add(PIZZ, 26, bar(27), 1, 70)
    return s, 116, bar(28)


# ---------- 7. Крысиная песня ----------

def rat():
    """Песня отряда — когда никто не гонится. Мотив становится напевом:
    кларнет поёт его длиннее, с хвостом, фагот подпевает октавой ниже,
    флейта внизу — эхом. Пиццикато в три счёта: не марш и не бег, а пляс
    у огня. Середина — в фа мажоре, все вместе."""
    s = Orchestra({BSN: 108, CLAR: 98, PIZZ: 90, FLUTE: 80, OBOE: 82, PAD: 54, BASS: 86, CELLO: 80, 9: 76},
                  program=FOREST)
    # В три четверти: доля — четверть, такт — три доли.
    def b3(n): return (n - 1) * 3

    def oompah(at, roots, vel=60):
        for i, r in enumerate(roots):
            t = at + i * 3
            s.add(PIZZ, r, t, 0.5, vel + 8)
            for q in (1, 2):
                s.add(PIZZ, r + 12 + (3 if q == 1 else 7), t + q, 0.4, vel - 10)

    SONG = [(50, 0, 1), (53, 1, 1), (56, 2, 1), (52, 3, 2), (50, 5, 1),     # мотив, в три счёта
            (53, 6, 1), (52, 7, 1), (50, 8, 1), (49, 9, 3)]                 # хвост — на до-диез
    SONG2 = [(50, 0, 1), (53, 1, 1), (56, 2, 1), (52, 3, 2), (50, 5, 1),
             (52, 6, 1), (53, 7, 1), (55, 8, 1), (57, 9, 3)]                # хвост вверх — к ля
    roots = [38, 38, 38, 33, 38, 38, 33, 33]
    oompah(b3(1), roots + roots)
    s.line(CLAR, SONG, b3(1), 86, shift=12); s.line(CLAR, SONG2, b3(5), 86, shift=12)
    s.line(BSN, SONG, b3(9), 90); s.line(CLAR, SONG2, b3(9), 70, shift=12)
    s.line(FLUTE, [(74, 0, 0.5), (69, 0.5, 1.5)], b3(12) + 1, 62)
    s.line(BSN, SONG2, b3(13), 90)
    # середина — фа мажор, все вместе, гобой ведёт
    froots = [41, 41, 36, 36, 34, 34, 36, 33]
    oompah(b3(17), froots, 64)
    MAJ = [(65, 0, 1), (69, 1, 1), (72, 2, 1), (67, 3, 2), (65, 5, 1),
           (69, 6, 1), (67, 7, 1), (65, 8, 1), (64, 9, 3)]
    s.line(OBOE, MAJ, b3(17), 84); s.line(BSN, MAJ, b3(17), 76, shift=-24)
    s.line(CLAR, MAJ, b3(21), 84, shift=-3); s.line(FLUTE, MAJ, b3(21), 60)
    for i, name in enumerate(['F', 'F', 'C', 'C', 'Bb', 'Bb', 'C', 'A']):
        chord(s, PAD, b3(17) + i * 3, name, 3, 34)
    # снова ре — и в конце напев обрывается смешком
    oompah(b3(25), roots, 60)
    s.line(CLAR, SONG, b3(25), 88, shift=12); s.line(BSN, SONG, b3(25), 82)
    s.line(CLAR, SONG2, b3(29), 84, shift=12)
    s.add(BSN, 38, b3(33), 1, 82); s.add(PIZZ, 26, b3(33), 0.5, 70)
    drum(s, WOOD_HI, b3(33) + 1, 56, 0.25)
    return s, 132, b3(34)


# ---------- 8. Колыбельная ----------

def lullaby():
    """Перед рассветом. Кларнет один поёт мотив медленно, и соль-диез
    уходит в ля — уменьшённое трезвучие разрешается: бежать больше не надо,
    хотя бы до утра. Арфа, тихие струнные, гобой отвечает; в конце валторна
    держит ля — скитание с надеждой (тон темы Безымянного, `44-LORE.md` §2)."""
    # Кларнет был громче всех на 11 дБ (автор, 23.10: «один инструмент слишком
    # выделяется громкостью») — тише; гобой и арфа ближе к нему.
    s = Orchestra({CLAR: 78, OBOE: 92, PIZZ: 90, PAD: 66, CELLO: 80, BASS: 80, HORN: 64},
                  program={**FOREST, PIZZ: 46})                       # здесь арфа, а не пиццикато
    A = ['Dm', 'Gm', 'A', 'Dm', 'Bb', 'Gm', 'A', 'A']
    for i, name in enumerate(A):
        c = CHORD[name]
        for k, p in enumerate([c['cello'], *c['mid'], c['mid'][1]]):
            s.add(PIZZ, p, bar(1) + i * 4 + k * 0.75, 2.5, 60 if k else 68)
    s.pads(PAD, bar(1), A, 46)
    # мотив кларнета медленно — соль-диез уходит в ля
    LULL = [(62, 0, 2), (65, 2, 2), (68, 4, 2), (69, 6, 4), (64, 10, 2), (62, 12, 4)]
    s.line(CLAR, LULL, bar(3), 68)                     # мягко: после арфы прыгал на +18 дБ
    s.line(OBOE, [(69, 0, 2), (67, 2, 2), (65, 4, 2), (64, 6, 6)], bar(7) + 2, 62, shift=-5)
    # вторая строфа — в ре мажоре
    D = ['D', 'G', 'A', 'D', 'Bm', 'G', 'A', 'D']
    for i, name in enumerate(D):
        c = CHORD[name]
        for k, p in enumerate([c['cello'], *c['mid'], c['mid'][1]]):
            s.add(PIZZ, p, bar(9) + i * 4 + k * 0.75, 2.5, 50 if k else 58)
    s.pads(PAD, bar(9), D, 36)
    LULL2 = [(62, 0, 2), (66, 2, 2), (69, 4, 2), (71, 6, 4), (66, 10, 2), (62, 12, 4)]
    s.line(CLAR, LULL2, bar(11), 72)
    s.line(OBOE, [(74, 0, 2), (73, 2, 2), (71, 4, 2), (69, 6, 6)], bar(15) + 2, 60, shift=-5)
    for i, name in enumerate(D):
        s.add(BASS, CHORD[name]['bass'], bar(9) + i * 4, 4, 52)
    # конец — валторна держит ля, кларнет — ре; ре мажор, тихо
    # Конец — тише всего, что было (автор: «в конце довольно громко»): ре
    # мажор шёпотом и угасает всеми голосами, а не одними струнными.
    s.add(HORN, 57, bar(17), 12, 42)
    s.add(CLAR, 62, bar(17), 12, 48)
    chord(s, PAD, bar(17), 'D', 12, 30)
    for ch in (HORN, CLAR, BASS):
        s.swell(ch, bar(17) + 2, bar(20), 100, 0)
    s.add(BASS, 26, bar(17), 12, 50)
    s.swell(PAD, bar(18), bar(20), 100, 0)
    return s, 60, bar(20)


PIECES = {'fire': fire, 'trail': trail, 'hide': hide, 'flee': flee, 'fight': fight,
          'loot': loot, 'rat': rat, 'lullaby': lullaby}

if __name__ == '__main__':
    name, out = sys.argv[1], sys.argv[2]
    s, bpm, length = PIECES[name]()
    if '--loop' in sys.argv:
        tempo = loop_tempo(bpm, length)
        s.repeat(length)
        s.save(out, 60e6 / tempo)
        sec = length * tempo / 1e6
        print(f'{out}: {name}, нот {s.count()}, темп {60e6 / tempo:.2f}, круг {sec:.6f} с; '
              f'петля — с {sec:.6f} по {2 * sec:.6f} с')
    else:
        s.save(out, bpm)
        print(f'{out}: {name}, нот {s.count()}, темп {bpm}, конец на {s.end() * 60 / bpm:.1f} с')
