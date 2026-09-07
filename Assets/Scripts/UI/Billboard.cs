// Assets/Scripts/UI/Billboard.cs
using UnityEngine;

namespace Sinbinder.UI
{
    /// <summary>
    /// Держать объект развёрнутым к камере.
    ///
    /// Нужен надголовному интерфейсу: холст в мировом пространстве стоит
    /// как поставлен, и значок намерения виден с одной стороны, а с другой
    /// исчезает. Для кадра, которым игра продаётся, это неприемлемо.
    ///
    /// Разворот — чистая косметика и на решения не влияет никак, поэтому
    /// правило повторяемости его не касается.
    /// </summary>
    public class Billboard : MonoBehaviour
    {
        private Transform _eye;

        void Start() => Retarget();

        /// <summary>Найти камеру заново. Нужен после возврата из пула.</summary>
        public void Retarget()
        {
            var cam = Camera.main;
            _eye = cam != null ? cam.transform : null;
        }

        void LateUpdate()
        {
            if (_eye == null) { Retarget(); return; }

            // Смотрим в ту же сторону, куда смотрит камера, а не на неё:
            // так надписи над разными воинами остаются параллельными
            // и строй не разъезжается веером.
            transform.rotation = _eye.rotation;
        }
    }
}
