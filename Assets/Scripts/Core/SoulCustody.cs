// Перевод: текст через Loc
using System;
using System.Collections.Generic;
using Sinbinder.Crypt;
using Sinbinder.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Sinbinder.Core
{
    [Serializable]
    public sealed class SavedSoulCustody
    {
        public bool Captured;
        public int JarCount;
        public int SelectedJar;
        public List<SavedJarSoul> FilledJars = new();
        public SavedBindingContents Hands;
        public SavedBindingContents Device;
        public int DeviceRaised;
    }

    [Serializable]
    public struct SavedJarSoul
    {
        public int Index;
        public SavedSoul Soul;
    }

    [Serializable]
    public struct SavedBindingContents
    {
        public bool HasSoul;
        public SavedSoul Soul;
        public bool HasShell;
        public ShellType Shell;
    }

    /// <summary>Снимает и заменяет содержимое сумы, рук и устройства связывания.</summary>
    internal static class SoulCustody
    {
        internal static SavedSoulCustody Capture(Scene scene)
        {
            var device = FindDevice(scene);
            var state = new SavedSoulCustody
            {
                Captured = true,
                JarCount = Satchel.Jars,
                SelectedJar = Satchel.Selected,
                Hands = CaptureContents(CryptHands.Soul, CryptHands.Quality, CryptHands.HasShell, CryptHands.Shell),
                Device = device != null ? CaptureContents(device.SoulInside, device.Quality,
                    device.HasShell, device.ShellInside) : default,
                DeviceRaised = device != null ? device.Raised : 0,
            };

            for (int i = 0; i < state.JarCount; i++)
            {
                var jar = Satchel.At(i);
                if (jar.FullJar)
                    state.FilledJars.Add(new SavedJarSoul { Index = i, Soul = SaveSystem.Record(jar.Soul, jar.Quality) });
            }

            return state;
        }

        private static SavedBindingContents CaptureContents(SoulData soul, SoulQuality quality,
            bool hasShell, ShellType shell)
            => new SavedBindingContents
            {
                HasSoul = soul != null,
                Soul = SaveSystem.Record(soul, quality),
                HasShell = hasShell,
                Shell = shell,
            };

        internal static bool IsValid(SavedSoulCustody state, IReadOnlyList<SavedSoul> shelf)
        {
            var ids = new HashSet<string>();
            if (shelf != null)
                foreach (var soul in shelf)
                    if (soul != null && !string.IsNullOrEmpty(soul.Id) && !ids.Add(soul.Id))
                        return false;

            // JsonUtility может создать пустой вложенный объект для старого файла.
            if (state == null || !state.Captured) return true;
            if (state.JarCount < 0 || state.JarCount > Satchel.Max ||
                state.SelectedJar < 0 || state.SelectedJar >= Math.Max(1, state.JarCount) ||
                state.FilledJars == null || state.DeviceRaised < 0 ||
                (state.Hands.HasSoul && state.Hands.HasShell))
                return false;

            var jarIndices = new HashSet<int>();
            foreach (var jar in state.FilledJars)
                if (jar.Index < 0 || jar.Index >= state.JarCount ||
                    !jarIndices.Add(jar.Index) || !AcceptSoul(jar.Soul, ids))
                    return false;

            return AcceptContents(state.Hands, ids) && AcceptContents(state.Device, ids);
        }

        private static bool AcceptContents(SavedBindingContents contents, HashSet<string> ids)
            => (!contents.HasSoul || AcceptSoul(contents.Soul, ids)) &&
                (!contents.HasShell || Enum.IsDefined(typeof(ShellType), contents.Shell));

        private static bool AcceptSoul(SavedSoul soul, HashSet<string> ids)
            => SaveSystem.IsValidSoul(soul) && !string.IsNullOrEmpty(soul.Id) && ids.Add(soul.Id);

        internal static void Restore(SavedSoulCustody state, Scene scene)
        {
            var device = FindDevice(scene);
            CryptHands.Forget();

            if (state == null || !state.Captured)
            {
                Satchel.Forget();
                if (device != null) device.RestoreContents(null, default, false, default, 0);
            }
            else
            {
                Satchel.RestoreLayout(state.JarCount, state.SelectedJar);
                foreach (var jar in state.FilledJars)
                    Satchel.Fill(jar.Index, SaveSystem.Soul(jar.Soul), (SoulQuality)jar.Soul.Quality);

                if (state.Hands.HasSoul)
                    CryptHands.TakeSoul(SaveSystem.Soul(state.Hands.Soul), (SoulQuality)state.Hands.Soul.Quality);
                else if (state.Hands.HasShell)
                    CryptHands.TakeShell(state.Hands.Shell);

                var contents = state.Device;
                var soul = contents.HasSoul ? SaveSystem.Soul(contents.Soul) : null;
                var quality = contents.HasSoul ? (SoulQuality)contents.Soul.Quality : default;
                if (device != null)
                    device.RestoreContents(soul, quality, contents.HasShell, contents.Shell, state.DeviceRaised);
                else if (soul != null)
                    SoulManager.Instance?.PutBack(new SoulManager.Kept(soul, quality));
            }

            RestoreShelfView(scene);
        }

        private static BindingDevice FindDevice(Scene scene)
        {
            if (Lords.Active || !scene.IsValid()) return null;

            var workshop = CryptWorkshop.Device;
            if (workshop != null && workshop.gameObject.scene == scene) return workshop;

            BindingDevice found = null;
            foreach (var device in Object.FindObjectsByType<BindingDevice>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (device.gameObject.scene != scene) continue;
                if (found != null) return null;
                found = device;
            }
            return found;
        }

        private static void RestoreShelfView(Scene scene)
        {
            if (!Application.isPlaying || !scene.IsValid()) return;
            foreach (var shelf in Object.FindObjectsByType<SoulShelf>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (shelf.gameObject.scene == scene) shelf.OnLoaded();
        }
    }
}
