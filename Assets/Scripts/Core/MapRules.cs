// Assets/Scripts/Core/MapRules.cs
// Rules for player-authored Tug of War maps. Does not alter game rules at runtime.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sinbinder.Core
{
    public sealed class MapRuleReport
    {
        public readonly List<string> Errors = new();
        public readonly List<string> Warnings = new();
        public bool Valid => Errors.Count == 0;
    }

    public static class MapRules
    {
        private static bool Finite(float x) => !float.IsNaN(x) && !float.IsInfinity(x);
        private static bool Inside(float x, float z, float half, float margin = 0f)
            => Finite(x) && Finite(z) && Mathf.Abs(x) <= half - margin && Mathf.Abs(z) <= half - margin;
        private static float Dist(float x, float z, float a, float b)
            => Vector2.Distance(new Vector2(x, z), new Vector2(a, b));

        /// <summary>
        /// Structural errors prevent saving from the map editor. Warnings signal balance issues
        /// without removing the author's freedom to make unusual layouts.
        /// </summary>
        public static MapRuleReport Check(MapData map)
        {
            var result = new MapRuleReport();
            if (map == null) { result.Errors.Add("Карта отсутствует."); return result; }
            if (!Finite(map.Size) || map.Size < 150f || map.Size > 800f)
            {
                result.Errors.Add("Размер поля должен быть от 150 до 800 м.");
                return result;
            }
            float half = map.Size * 0.5f;
            if (map.Crypts == null || (map.Crypts.Count != 2 && map.Crypts.Count != 4))
                result.Errors.Add("В режиме «Перетягивание каната» должно быть ровно 2 или 4 склепа.");
            if (map.Locations == null || map.Locations.Count == 0)
                result.Errors.Add("Нужна хотя бы одна подлокация с жителями.");
            if (map.Events == null || (map.Crypts != null && map.Events.Count != map.Crypts.Count * 2))
                result.Errors.Add("Число событий должно равняться двум на каждого игрока.");
            if (map.Trails == null || (map.Crypts != null && map.Trails.Count < map.Crypts.Count))
                result.Errors.Add("Нужно не меньше одной тропы охотников на игрока.");
            if (map.Things == null) result.Errors.Add("Отсутствует список предметов.");

            if (map.Crypts != null)
            {
                for (int i = 0; i < map.Crypts.Count; i++)
                {
                    var c = map.Crypts[i];
                    if (c == null || !Inside(c.X, c.Z, half, 16f) || !Finite(c.Yaw))
                    {
                        result.Errors.Add($"Склеп {i + 1}: некорректные координаты или поворот, либо слишком близко к границе.");
                        continue;
                    }
                    if (Dist(c.X, c.Z, 0f, 0f) < 45f)
                        result.Warnings.Add($"Склеп {i + 1} слишком близко к центральной легенде.");
                    for (int j = 0; j < i; j++)
                    {
                        var other = map.Crypts[j];
                        if (other != null && Finite(other.X) && Finite(other.Z) && Dist(c.X, c.Z, other.X, other.Z) < 70f)
                            result.Errors.Add($"Склепы {j + 1} и {i + 1} расположены слишком близко (меньше 70 м).");
                    }
                }
                if (map.Crypts.Count == 2 && map.Crypts.TrueForAll(c => c != null && Finite(c.X) && Finite(c.Z)))
                {
                    var a = map.Crypts[0]; var b = map.Crypts[1];
                    if (Dist(a.X + b.X, a.Z + b.Z, 0f, 0f) > 25f)
                        result.Warnings.Add("Для двух сторон желательно расположить склепы напротив друг друга.");
                }
                if (map.Crypts.Count == 4 && map.Crypts.TrueForAll(c => c != null && Finite(c.X) && Finite(c.Z)))
                {
                    float x = 0f, z = 0f;
                    foreach (var c in map.Crypts) { x += c.X; z += c.Z; }
                    if (Dist(x / 4f, z / 4f, 0f, 0f) > 20f)
                        result.Warnings.Add("Склепы четырёх сторон лучше распределить вокруг центра равномерно.");
                }
            }
            if (map.Locations != null)
            {
                for (int i = 0; i < map.Locations.Count; i++)
                {
                    var l = map.Locations[i];
                    if (l == null || !Finite(l.Radius) || l.Radius < 5f || l.Radius > 80f ||
                        l.People < 0 || l.People > 50 || !Inside(l.X, l.Z, half, l.Radius * 0.5f) ||
                        (l.Kind != "forest" && l.Kind != "field" && l.Kind != "village" && l.Kind != "fort"))
                        result.Errors.Add($"Подлокация {i + 1}: проверьте тип, координаты, радиус и жителей (0–50).");
                    else if (map.Crypts != null)
                    {
                        foreach (var c in map.Crypts)
                            if (c != null && Finite(c.X) && Finite(c.Z) && Dist(l.X, l.Z, c.X, c.Z) < l.Radius + 16f)
                            { result.Warnings.Add($"Подлокация {i + 1} близко к склепу: проверьте место появления жителей."); break; }
                    }
                }
            }
            if (map.Things != null)
            {
                for (int i = 0; i < map.Things.Count; i++)
                {
                    var t = map.Things[i];
                    if (t == null || !Inside(t.X, t.Z, half) || !Finite(t.Yaw) || !Finite(t.Scale) ||
                        t.Scale < 0.15f || t.Scale > 5f || string.IsNullOrWhiteSpace(t.Prop))
                        result.Errors.Add($"Предмет {i + 1}: некорректный тип, координаты, поворот или размер.");
                }
            }
            if (map.Trails != null)
            {
                for (int i = 0; i < map.Trails.Count; i++)
                {
                    var t = map.Trails[i];
                    if (t == null || !Inside(t.X, t.Z, half) || t.Kind != "hunters")
                    { result.Errors.Add($"Тропа охотников {i + 1}: неверная точка или тип."); continue; }
                    if (half - Mathf.Max(Mathf.Abs(t.X), Mathf.Abs(t.Z)) > 25f)
                        result.Warnings.Add($"Тропа {i + 1}: охотникам желательно выходить у края поля.");
                    if (map.Crypts != null)
                        foreach (var c in map.Crypts)
                            if (c != null && Finite(c.X) && Finite(c.Z) && Dist(t.X, t.Z, c.X, c.Z) < 65f)
                            { result.Warnings.Add($"Тропа {i + 1}: охотники появляются слишком близко к склепу."); break; }
                }
            }
            if (map.Events != null)
            {
                for (int i = 0; i < map.Events.Count; i++)
                {
                    var e = map.Events[i];
                    if (e == null || !Inside(e.X, e.Z, half) ||
                        (e.Kind != "caravan" && e.Kind != "village"))
                    { result.Errors.Add($"Событие {i + 1}: неверная точка или тип."); continue; }
                    if (map.Crypts != null)
                        foreach (var c in map.Crypts)
                            if (c != null && Finite(c.X) && Finite(c.Z) && Dist(e.X, e.Z, c.X, c.Z) < 28f)
                            { result.Warnings.Add($"Событие {i + 1} слишком близко к склепу."); break; }
                }
            }
            // Terrain additions are optional: legacy maps are still valid and remain flat.
            if (map.HeightStamps != null)
            {
                if (map.HeightStamps.Count > 100) result.Errors.Add("Слишком много изменений рельефа (не более 100).");
                for (int i=0;i<map.HeightStamps.Count;i++)
                {
                    var h=map.HeightStamps[i];
                    if (h==null || !Inside(h.X,h.Z,half,4f) || !Finite(h.Height) ||
                        !Finite(h.Radius) || h.Radius<15f || h.Radius>140f || Mathf.Abs(h.Height)>18f)
                        result.Errors.Add($"Холм/овраг {i+1}: проверьте границы, радиус и высоту.");
                    else if (Mathf.Abs(h.Height) > h.Radius * .3f)
                        result.Warnings.Add($"Холм {i+1}: крутой склон может потребовать настройки NavMesh.");
                }
            }
            if (map.Fortifications != null)
            {
                if (map.Fortifications.Count>24) result.Errors.Add("Слишком много укреплений (не более 24).");
                for(int i=0;i<map.Fortifications.Count;i++)
                {
                    var f=map.Fortifications[i];
                    if(f==null || (f.Kind!="castle" && f.Kind!="fort") ||
                        !Finite(f.HalfSize) || f.HalfSize<12f || f.HalfSize>38f ||
                        !Finite(f.Yaw) || !Inside(f.X,f.Z,half,f.HalfSize*1.5f) ||
                        f.Gates!=(f.Kind=="castle"?3:1) || f.Guards<0 || f.Guards>12 ||
                        f.Archers<0 || f.Archers>8)
                    { result.Errors.Add($"Укрепление {i+1}: недопустимые параметры или слишком близко к краю.");continue; }
                    if(f.Kind=="fort" && (map.Locations==null || !map.Locations.Exists(l=>l!=null && l.Kind=="fort" && Dist(l.X,l.Z,f.X,f.Z)<8f)))
                        result.Errors.Add($"Форт {i+1}: добавьте очаг мирных жителей внутрь стен.");
                    if(f.Kind=="castle" && (map.Crypts==null || !map.Crypts.Exists(c=>c!=null && Dist(c.X,c.Z,f.X,f.Z)<4f)))
                        result.Errors.Add($"Замок {i+1}: склеп должен стоять внутри замка.");
                }
                if(map.Fortifications.Exists(f=>f!=null && f.Kind=="castle") && map.Crypts!=null)
                    foreach(var c in map.Crypts)
                        if(c!=null && !map.Fortifications.Exists(f=>f!=null && f.Kind=="castle" && Dist(c.X,c.Z,f.X,f.Z)<4f))
                            result.Errors.Add("У каждой стороны должен быть свой замок, если выбрана замковая карта.");
                if (map.Locations != null)
                    foreach (var l in map.Locations)
                        if (l!=null && l.Kind=="fort" &&
                            !map.Fortifications.Exists(f=>f!=null && f.Kind=="fort" && Dist(l.X,l.Z,f.X,f.Z)<8f))
                            result.Errors.Add("Поселение форта осталось без стен — удалите его либо поставьте укрепление.");
                if(map.Fortifications.Exists(f=>f!=null && f.Kind=="fort") &&
                    (map.Locations==null || !map.Locations.Exists(l=>l!=null && l.Kind=="village")))
                    result.Errors.Add("На карте с фортами оставьте хотя бы одну незащищённую деревню.");
            }
            return result;
        }
    }
}
