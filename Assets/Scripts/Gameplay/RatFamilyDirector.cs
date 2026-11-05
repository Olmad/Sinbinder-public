// Sinbinder v306: a family of six real scene actors, independent of the army.
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Sinbinder.Core;

namespace Sinbinder.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class RatFamilyDirector : MonoBehaviour
    {
        private const string CampScene = "Prologue_Camp";
        private const float WakeDelay = 2.45f;  // After the player actually rises, not after loading.
        private const float AmbientGap = 155f; // Animals occasionally return to the camp.
        private static RatFamilyDirector _instance;

        private RatFamilyActor _mother;
        private readonly List<RatFamilyActor> _pups = new List<RatFamilyActor>();
        private readonly List<Vector3> _path = new List<Vector3>();
        private Transform _family;
        private int _nextWaypoint;
        private bool _walking;
        private bool _pausedByPup;
        private float _spawnedAt;
        private float _nextAmbient;
        private float _lastCheck;
        private bool _playedOpening;
        private Vector3 _burrow;
        private GameObject _nest;
        private bool _reportedBlocked;
        private float _waitingSince;
        private float _nextRescue;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            _instance = null;
            SceneManager.sceneLoaded -= Loaded;
            SceneManager.sceneLoaded += Loaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install() => Loaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);

        // На каждую загрузку лагеря, а не только на первую: AfterSceneLoad
        // срабатывает раз за запуск, и после «Начать сначала», «С начала доли»
        // и возврата из склепа семьи уже не было бы (05.11).
        private static void Loaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != CampScene || _instance != null) return;
            var host = new GameObject("Семейство крыс");
            SceneManager.MoveGameObjectToScene(host, scene);
            _instance = host.AddComponent<RatFamilyDirector>();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            _nextAmbient = Time.time + AmbientGap;
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private IEnumerator Start()
        {
            // Lying becomes true only AFTER the existing dream screen has cleared;
            // it turns false at the end of the F-to-rise animation.
            bool sawLying = false;
            while (SceneManager.GetActiveScene().name == CampScene)
            {
                if (RaidEvent.Running) yield break;
                if (OpeningReveal.Lying) sawLying = true;
                if (sawLying && !OpeningReveal.Lying)
                {
                    yield return new WaitForSecondsRealtime(WakeDelay);
                    if (this != null && !RaidEvent.Running && SceneManager.GetActiveScene().name == CampScene)
                    {
                        _playedOpening = SpawnFamily();
                        if (_playedOpening) _nextAmbient = Time.time + AmbientGap;
                    }
                    yield break;
                }
                // A loaded mid-camp save never sees Lying; ambient appears later from Update.
                yield return null;
            }
        }

        private static bool Calm()
        {
            var pause = GamePauseController.Instance;
            return !RaidEvent.Running && (pause == null || !pause.IsPaused)
                   && !OpeningReveal.Dreaming && !OpeningReveal.Lying;
        }

        private void Update()
        {
            if (SceneManager.GetActiveScene().name != CampScene) return;
            if (RaidEvent.Running) { if (_family != null) RetireFamily(); return; }
            if (!Calm()) return;
            if (_family == null)
            {
                if (Time.time >= _nextAmbient && SinbinderPlayer.Exists && !OpeningReveal.Busy
                    && !UI.StartPanel.Waiting && !UI.PrologueTitleUI.Showing)
                {
                    if (SpawnFamily()) _nextAmbient = Time.time + AmbientGap;
                    else _nextAmbient = Time.time + 20f;
                }
                return;
            }
            if (_mother == null || !_mother.OnMesh || _pups.Count != 5) return;
            // Let the player see them by the doorway before they depart;
            // wait for the existing title and camera pullback, not a new cinematic.
            if (!_walking)
            {
                if (OpeningReveal.Busy || Time.time - _spawnedAt < 5f) return;
                _walking = true;
                SetNextWaypoint();
            }
            if (Time.time - _lastCheck < .22f) return;
            _lastCheck = Time.time;
            bool far = false;
            for (int i = 0; i < _pups.Count; i++)
            {
                var pup = _pups[i];
                if (pup == null) continue;
                if (pup.DistanceFrom(_mother) > 2.25f) far = true;
                pup.Follow(_mother);
            }
            // The mother waits for the laggard, and resumes as soon as all five catch up.
            if (far != _pausedByPup)
            {
                _pausedByPup = far;
                if (far) _waitingSince = Time.time;
                _mother.Wait(far);
                if (!far && _nextWaypoint < _path.Count) _mother.MoveTo(_path[_nextWaypoint]);
            }
            if (far)
            {
                // Usually wait. If a child cannot find the route for eight seconds,
                // walk back to it instead of leaving the family stuck forever.
                if (Time.time - _waitingSince > 8f && Time.time >= _nextRescue)
                {
                    _nextRescue = Time.time + 2f;
                    RatFamilyActor last = null;
                    foreach (var pup in _pups)
                        if (pup != null && (last == null || pup.DistanceFrom(_mother) > last.DistanceFrom(_mother))) last = pup;
                    if (last != null && !_mother.MoveTo(last.transform.position)) _mother.Wait(true);
                }
                return;
            }
            if (_nextWaypoint < _path.Count &&
                Vector3.Distance(_mother.transform.position, _path[_nextWaypoint]) < .65f)
            {
                _nextWaypoint++;
                SetNextWaypoint();
            }
            if (_nextWaypoint >= _path.Count &&
                Vector3.Distance(_mother.transform.position, _burrow) < 1.1f)
            {
                bool allHome = true;
                foreach (var pup in _pups)
                    if (pup != null && pup.DistanceFrom(_mother) > 1.8f) allHome = false;
                if (allHome) RetireFamily();
            }
        }

        private bool SpawnFamily()
        {
            if (_family != null || RaidEvent.Running) return false;
            var tentGo = GameObject.Find("Палатка Греховода");
            var tent = tentGo != null ? tentGo.GetComponent<TentInterior>() : null;
            if (tent == null)
            {
                Debug.LogWarning("[КРЫСЫ] Палатка Греховода не найдена; семейство не создаём посреди карты.");
                return false;
            }
            Vector3 doorway = Ground(tent.Doorstep(1.25f));
            if (!OnNavmesh(doorway, out var start))
            {
                Debug.LogWarning("[КРЫСЫ] Вокруг выхода из палатки нет NavMesh; семейство отложено.");
                return false;
            }
            var family = new GameObject("Мать и пять крысят");
            family.transform.SetParent(transform, false);
            _family = family.transform;
            _mother = RatFamilyActor.Create(0, start, _family);
            if (_mother == null || !_mother.OnMesh) { RetireFamily(); return false; }
            _mother.transform.rotation = tent.transform.rotation;
            _pups.Clear();
            float[] offsets = { -.48f, -.18f, .22f, .50f, .04f };
            for (int i = 0; i < 5; i++)
            {
                Vector3 near = start + tent.transform.right * offsets[i]
                                      - tent.transform.forward * (.32f + i*.13f);
                if (OnNavmesh(near, out var placed)) near = placed;
                var pup = RatFamilyActor.Create(i+1,near,_family);
                if (pup != null) { pup.transform.rotation = tent.transform.rotation; _pups.Add(pup); }
            }
            // Campfire is at origin, but use its actual marker if the builder shifted the scene.
            var campfire = GameObject.Find("Костёр");
            var fire = campfire != null ? campfire.transform.position : Vector3.zero;
            float spread = CampFocus.Spread;
            Vector3 nestWanted = fire + new Vector3(7.5f*spread,0f,5.7f*spread);
            if (!OnNavmesh(nestWanted, out _burrow)) _burrow = start;
            BuildPath(tent, fire, spread);
            MakeNest(_burrow);
            _walking = false;
            _nextWaypoint = 0;
            _pausedByPup = false;
            _reportedBlocked = false;
            _spawnedAt = Time.time;
            return true;
        }

        private void BuildPath(TentInterior tent, Vector3 fire, float spread)
        {
            _path.Clear();
            // Shape, not a scripted straight line through the 25 tents.
            Vector3[] wanted =
            {
                tent.Doorstep(4.2f),
                fire + new Vector3(2.0f*spread,0f,-4.2f*spread),
                fire + new Vector3(6.1f*spread,0f,-2.7f*spread),
                fire + new Vector3(7.2f*spread,0f,2.8f*spread),
                _burrow
            };
            foreach (var v in wanted)
                if (OnNavmesh(v, out var valid)) _path.Add(valid);
            if (_path.Count == 0) _path.Add(_burrow);
        }

        private void SetNextWaypoint()
        {
            if (_mother == null || !_mother.OnMesh) return;
            while (_nextWaypoint < _path.Count)
            {
                if (_mother.MoveTo(_path[_nextWaypoint])) return;
                if (!_reportedBlocked)
                {
                    _reportedBlocked = true;
                    Debug.LogWarning("[КРЫСЫ] Один участок маршрута недоступен; ищем следующий проходимый.");
                }
                _nextWaypoint++;
            }
            // If nothing is reachable, keep the actors in place. Never force them through walls.
            _mother.Wait(true);
        }

        private static Vector3 Ground(Vector3 position)
        {
            if (OnNavmesh(position, out var found)) return found;
            return position;
        }

        private static bool OnNavmesh(Vector3 requested, out Vector3 found)
        {
            if (NavMesh.SamplePosition(requested, out var hit, 3.2f, NavMesh.AllAreas))
            { found = hit.position; return true; }
            found = requested;
            return false;
        }

        private void MakeNest(Vector3 where)
        {
            if (_nest != null) Destroy(_nest);
            _nest = new GameObject("Нора крыс");
            _nest.transform.SetParent(transform, false);
            _nest.transform.position = where;
            var hole = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            hole.name = "Вход в нору";
            hole.transform.SetParent(_nest.transform, false);
            hole.transform.localPosition = Vector3.up * .015f;
            hole.transform.localScale = new Vector3(.58f,.034f,.37f);
            hole.GetComponent<Renderer>().sharedMaterial = RatFamilyVisual.Soil;
            Destroy(hole.GetComponent<Collider>());
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI * 2f / 6f;
                var stone = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                stone.name = "Камень у норы";
                stone.transform.SetParent(_nest.transform, false);
                stone.transform.localPosition = new Vector3(Mathf.Cos(a)*.39f,.035f,Mathf.Sin(a)*.28f);
                stone.transform.localScale = new Vector3(.14f,.075f,.13f);
                stone.GetComponent<Renderer>().sharedMaterial = RatFamilyVisual.Pebble;
                Destroy(stone.GetComponent<Collider>());
            }
        }

        private void RetireFamily()
        {
            if (_family != null) Destroy(_family.gameObject);
            _family = null;
            _mother = null;
            _pups.Clear();
            _walking = false;
            _pausedByPup = false;
            _nextAmbient = Time.time + AmbientGap;
        }
    }
}
