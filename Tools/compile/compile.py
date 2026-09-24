#!/usr/bin/env python3
"""
Настоящий компилятор C# по всем скриптам игры — без Unity.

check.py ищет ошибки известных видов; этот скрипт компилирует. Разница
нашлась в первый же день: переменная, объявленная второй раз во вложенной
области (CS0136), check.py не видит, а Unity из-за неё не собирает проект
целиком (24 сентября, SelectedUnitPanelUI).

Три прохода, как у Unity:
  1. игра в сборке игрока — без UNITY_EDITOR;
  2. игра в редакторе — с UNITY_EDITOR и ссылкой на UnityEditor;
  3. редакторные скрипты (папки Editor) — отдельной сборкой поверх второй.

Ссылки берутся из NuGet и кладутся в кэш вне репозитория:
  UnityEngine.Modules 2021.3.33, Unity3D.UnityEngine.UI 2020.3.21,
  Unity3D.SDK 2021.1.14.1 (UnityEditor). Это не Unity 6: чего нет в 2021.3,
  то покажется ошибкой (пока не встречалось); что Unity 6 запретила
  (Obsolete с ошибкой), того здесь не видно. Пакеты, которых в NuGet нет
  (URP, Core RP, навигация), заменены заглушками из stubs/ — всё, что
  стоит на них, проверено только на согласие с заглушкой. Проход тумана
  (FogOfWarFeature) поэтому этой проверкой не подтверждается: его
  сигнатуры сверены с исходниками URP 17.3 отдельно (14-HANDOFF §67).

Запуск из корня репозитория:
    python3 Tools/compile/compile.py
Нужен dotnet SDK 6+ и сеть на первый запуск. Код выхода 1 — есть ошибки.
"""

import os
import re
import subprocess
import sys
import zipfile
from pathlib import Path

HERE = Path(__file__).resolve().parent
ROOT = HERE.parent.parent
ASSETS = ROOT / "Assets"
WORK = HERE / ".work"
CACHE = Path(os.environ.get("SINBINDER_REFS", Path.home() / ".cache" / "sinbinder-compile"))

PACKAGES = [
    ("unityengine.modules", "2021.3.33"),
    ("unity3d.unityengine.ui", "2020.3.21"),
    ("unity3d.sdk", "2021.1.14.1"),
]

# Предупреждения, которые Unity тоже не считает ошибками, а в старом
# коде их сотни: неиспользуемые поля, устаревшие вызовы и прочее.
NOWARN = "CS0414;CS0649;CS0169;CS0067;CS0618;CS0612;CS0168;CS0219;CS1998;CS0162;CS0108;CS0114"


def fetch(name, version):
    """Скачать и распаковать пакет NuGet, если его ещё нет в кэше."""
    target = CACHE / f"{name}.{version}"
    if target.exists():
        return target

    CACHE.mkdir(parents=True, exist_ok=True)
    nupkg = CACHE / f"{name}.{version}.nupkg"
    url = f"https://api.nuget.org/v3-flatcontainer/{name}/{version}/{name}.{version}.nupkg"
    print(f"Скачиваю {name} {version}…")

    # curl, а не urllib: он берёт прокси и сертификаты системы сам.
    subprocess.run(["curl", "-sSfL", "-m", "300", "-o", str(nupkg), url], check=True)
    with zipfile.ZipFile(nupkg) as z:
        z.extractall(target)
    nupkg.unlink()
    return target


def refs():
    engine = fetch(*PACKAGES[0]) / "lib" / "netstandard2.0"
    ui = fetch(*PACKAGES[1]) / "lib" / "UnityEngine.UI.dll"
    editor = fetch(*PACKAGES[2]) / "lib" / "UnityEditor.dll"
    return sorted(engine.glob("*.dll")) + [ui], editor


def project(name, sources, references, defines):
    """sources — пары (что включить, что исключить)."""
    items = "\n".join(
        f'    <Compile Include="{inc}"' + (f' Exclude="{exc}"' if exc else "") + " />"
        for inc, exc in sources)
    links = "\n".join(
        f'    <Reference Include="{r.stem}"><HintPath>{r}</HintPath><Private>false</Private></Reference>'
        for r in references)

    return f"""<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.1</TargetFramework>
    <LangVersion>9.0</LangVersion>
    <Nullable>disable</Nullable>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
    <GenerateAssemblyInfo>false</GenerateAssemblyInfo>
    <AssemblyName>{name}</AssemblyName>
    <DefineConstants>{defines}</DefineConstants>
    <NoWarn>{NOWARN}</NoWarn>
  </PropertyGroup>
  <ItemGroup>
{items}
  </ItemGroup>
  <ItemGroup>
{links}
  </ItemGroup>
</Project>
"""


def build(name, text):
    folder = WORK / name
    folder.mkdir(parents=True, exist_ok=True)
    (folder / f"{name}.csproj").write_text(text, encoding="utf-8")

    run = subprocess.run(["dotnet", "build", "-nologo", "-v", "q", str(folder)],
                         capture_output=True, text=True)

    errors = set()
    for line in (run.stdout + run.stderr).splitlines():
        if " error " not in line:
            continue
        line = re.sub(r"\s*\[[^\]]*\.csproj\]$", "", line.strip())
        errors.add(line.replace(str(ROOT) + os.sep, ""))

    dll = folder / "bin" / "Debug" / "netstandard2.1" / f"{name}.dll"
    return sorted(errors), dll


def main():
    engine, editor = refs()

    game = str(ASSETS / "**" / "*.cs")
    no_editor = str(ASSETS / "**" / "Editor" / "**")
    player_stubs = str(HERE / "stubs" / "player" / "*.cs")
    editor_stubs = str(HERE / "stubs" / "editor" / "*.cs")

    passes = []
    game_sources = [(game, no_editor), (player_stubs, None)]

    passes.append(("игра, сборка игрока",
                   *build("Player", project("Player", game_sources, engine, ""))))

    errors, runtime = build("InEditor",
                            project("InEditor", game_sources, engine + [editor], "UNITY_EDITOR"))
    passes.append(("игра в редакторе", errors, runtime))

    # Собранная прошлым запуском сборка лежит на месте и после провала:
    # поверх неё редакторные скрипты проверялись бы по вчерашнему коду.
    if not errors and runtime.exists():
        sources = [(str(ASSETS / "**" / "Editor" / "**" / "*.cs"), None), (editor_stubs, None)]
        passes.append(("редакторные скрипты",
                       *build("EditorScripts",
                              project("EditorScripts", sources,
                                      engine + [editor, runtime], "UNITY_EDITOR"))))
    else:
        passes.append(("редакторные скрипты",
                       ["не собирались: игра в редакторе не собралась"], None))

    total = 0
    for title, errors, _ in passes:
        print(f"\n{title}: " + ("чисто" if not errors else f"ошибок {len(errors)}"))
        for e in errors:
            print("  " + e)
        total += len(errors)

    print()
    if total:
        print(f"Ошибок: {total}. Unity с ними проект не соберёт.")
        return 1

    print("Чисто: три прохода собрались. Это не Unity 6 и не URP — см. начало файла.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
