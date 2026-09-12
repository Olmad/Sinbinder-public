// Assets/Scripts/Editor/BodyImport.cs
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Sinbinder.Gameplay;

namespace Sinbinder.EditorTools
{
    /// <summary>
    /// Что происходит с телом, когда оно легло в <c>Resources/Bodies</c>.
    ///
    /// Модель приходит из Блендера (<c>Tools/blender/skeleton.py</c>), и между
    /// файлом и воином на экране лежат три вещи, каждая из которых молча
    /// ломает всё остальное:
    ///
    /// <list type="number">
    /// <item><b>Масштаб и оси.</b> Модель ростом в единицу, опора у ног,
    /// лицом в +Z — это условия <see cref="WarriorLook.Build"/>, который
    /// умножает её на свой рост.</item>
    /// <item><b>Имена клипов.</b> Блендер называет дорожки
    /// «Skeleton|Flee», а <c>Animator.Play</c> ищет «Flee» и по чужому
    /// имени <b>молча ничего не делает</b> — худший вид поломки, потому
    /// что выглядит как «анимация просто не сделана». Отсекаем всё
    /// до вертикальной черты.</item>
    /// <item><b>Аниматор.</b> Импортированная модель несёт <c>Animator</c>
    /// без контроллера, а контроллер — редакторный ассет, и руками его
    /// собирать пришлось бы под каждую новую оболочку.</item>
    /// </list>
    ///
    /// Поэтому всё три делаются сами, при импорте. Положили новый
    /// <c>Zombie.fbx</c> — рядом появился <c>ZombieMotion.controller</c>
    /// с состояниями из <see cref="BodyMotion"/>, и воин ожил. Ни одного
    /// шага, который надо вспомнить через месяц.
    ///
    /// <b>Generic, а не Humanoid</b> — нарочно. Клипы написаны под эту же
    /// арматуру, и Generic проигрывает их ровно так, как написано, без
    /// мышечной нормализации. Арматура при этом humanoid-совместима
    /// (все пятнадцать обязательных костей, имена общепринятые, поза
    /// привязки T-образная), так что переключить тип — одна строка
    /// в тот день, когда понадобится чужая анимация.
    /// </summary>
    public class BodyImport : AssetPostprocessor
    {
        private const string Folder = "Assets/Resources/Bodies/";

        /// <summary>Клипы, которые обязаны зацикливаться. Остальные —
        /// падение, удар — проигрываются один раз, и зацикленное падение
        /// выглядит как судорога.</summary>
        private static readonly HashSet<string> Looping = new HashSet<string>
        {
            BodyMotion.Idle, BodyMotion.Walk, BodyMotion.Flee,
        };

        private static bool Ours(string path)
            => path != null
            && path.StartsWith(Folder)
            && path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase);

        // ------------------------------------------------------- импорт

        void OnPreprocessModel()
        {
            if (!Ours(assetPath)) return;

            var im = (ModelImporter)assetImporter;

            // Масштаб берём из файла: скрипт кладёт скелет ростом
            // в единицу нарочно, и любая поправка здесь сделает
            // опытного воина втрое выше рядового.
            im.useFileScale = true;
            im.globalScale = 1f;

            im.importNormals = ModelImporterNormals.Import;
            im.importBlendShapes = false;
            im.importCameras = false;
            im.importLights = false;
            im.importVisibility = false;

            // Материалы внутри модели, а не отдельными файлами: иначе
            // в Resources заводится папка ассетов, которые никто
            // не ищет по имени, но которые едут в сборку.
            im.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            im.materialLocation = ModelImporterMaterialLocation.InPrefab;

            im.animationType = ModelImporterAnimationType.Generic;
            im.animationCompression = ModelImporterAnimationCompression.Off;
            im.importAnimation = true;

            // Аватар — не формальность. Без него Unity не вешает на модель
            // Animator вовсе, Resources отдаёт тело без единого движения,
            // и WarriorAnimation молчит, не найдя, кому говорить. Проверено
            // щупом 12 сентября: аниматора не было, а выглядело это как
            // «анимации не сделаны».
            im.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        }

        void OnPreprocessAnimation()
        {
            if (!Ours(assetPath)) return;

            var im = (ModelImporter)assetImporter;
            var clips = im.defaultClipAnimations;
            if (clips == null || clips.Length == 0) return;

            for (int i = 0; i < clips.Length; i++)
            {
                clips[i].name = Bare(clips[i].name);
                clips[i].loopTime = Looping.Contains(clips[i].name);
                clips[i].loopPose = clips[i].loopTime;
            }

            im.clipAnimations = clips;
        }

        /// <summary>«Skeleton|Flee» → «Flee».</summary>
        private static string Bare(string take)
        {
            if (string.IsNullOrEmpty(take)) return take;

            int bar = take.LastIndexOf('|');
            return bar >= 0 && bar + 1 < take.Length ? take.Substring(bar + 1) : take;
        }

        // --------------------------------------------------- аниматор

        static void OnPostprocessAllAssets(string[] imported, string[] deleted,
                                           string[] moved, string[] movedFrom)
        {
            foreach (var path in imported)
            {
                if (Ours(path)) Wire(path);
            }
        }

        /// <summary>
        /// Собрать контроллер рядом с моделью.
        ///
        /// Состояния — все из <see cref="BodyMotion.All"/>, а не только
        /// те, на которые есть клип. Состояния, которого нет,
        /// <c>Animator.Play</c> не находит и молчит, и воин застывает
        /// в предыдущем движении: мёртвый продолжает бежать. Поэтому
        /// недостающие движения временно показывают покой — это видно
        /// как «ещё не нарисовано», а не как поломка.
        /// </summary>
        private static void Wire(string modelPath)
        {
            var clips = new Dictionary<string, AnimationClip>();

            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(modelPath))
            {
                var clip = asset as AnimationClip;
                if (clip != null && !clip.name.StartsWith("__")) clips[clip.name] = clip;
            }

            if (clips.Count == 0) return;

            string name = System.IO.Path.GetFileNameWithoutExtension(modelPath);
            string path = Folder + name + "Motion.controller";

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller == null)
                controller = AnimatorController.CreateAnimatorControllerAtPath(path);

            var machine = controller.layers[0].stateMachine;

            // Пересобираем начисто: иначе после правки клипов в контроллере
            // остались бы состояния от прошлого захода, и разойтись им
            // было бы негде, кроме как молча.
            foreach (var child in machine.states) machine.RemoveState(child.state);

            AnimationClip fallback;
            clips.TryGetValue(BodyMotion.Idle, out fallback);

            var missing = new List<string>();

            foreach (var motion in BodyMotion.All())
            {
                var state = machine.AddState(motion);

                AnimationClip clip;
                if (clips.TryGetValue(motion, out clip)) state.motion = clip;
                else { state.motion = fallback; missing.Add(motion); }

                if (motion == BodyMotion.Idle) machine.defaultState = state;
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            string said = $"[ТЕЛО] {name}: клипы — {string.Join(", ", clips.Keys)}; "
                        + $"контроллер {path}";

            if (missing.Count > 0)
                said += $"; ещё не нарисованы — {string.Join(", ", missing)} "
                      + "(показывают покой)";

            Debug.Log(said);
        }
    }
}
