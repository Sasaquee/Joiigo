using System.Collections;
using System.Linq;
using Game.Arena;
using Game.Core.Map;
using Game.Core.Math;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Game.Tests.PlayMode
{
    /// <summary>
    /// A cidade do mapa novo (passe do mapa, D-073 a D-082): os prédios são o limite do mapa. Confere o grupo
    /// Cidade/Colisores (um BoxCollider por prédio, camada Cenario, fora de transform espelhado), que as áreas andáveis ficam
    /// livres, que os prédios bloqueiam a vista e a passagem entre as avenidas, a altura máxima dentro do envelope da câmera
    /// (D-076) e a rendering layer "Vazavel" nas peças que podem tapar o jogador. Depende da cena gerada por
    /// Game → Setup → Construir Arena com o CityBuilder do mapa novo.
    /// </summary>
    public class CityLayoutTests
    {
        private const string MapSettingsPath = "Assets/_Game/Data/Map/MapLayoutSettings.asset";

        // Raio do agente (constructo, 0,9 m) menos uma folga de pele: um collider rente à borda andável não conta como invasão.
        private const float AgentRadius = 0.9f;
        private const float SkinTolerance = 0.03f;
        private const float GridStep = 2f;
        private const float GridExtent = 90f;
        private const float EnvelopeTolerance = 0.01f;
        private const float RayHeight = 1f;
        private const float WedgeAngle = -22.5f;      // entre as avenidas -45° e 0°
        private const float WedgeMirrorAngle = 22.5f; // entre as avenidas 0° e +45°
        private const float WedgeRayDistance = 60f;

        [UnitySetUp]
        public IEnumerator CarregaArena()
        {
            yield return ArenaTestScene.Load();
        }

        [UnityTearDown]
        public IEnumerator Limpa()
        {
            yield return ArenaTestScene.Cleanup();
        }

        // ---------- Apoio ----------

        private static MapLayout LoadLayout()
        {
#if UNITY_EDITOR
            var settings = AssetDatabase.LoadAssetAtPath<MapLayoutSettings>(MapSettingsPath);
            if (settings != null)
                return settings.ToLayout();
#endif
            var defaults = ScriptableObject.CreateInstance<MapLayoutSettings>();
            var layout = defaults.ToLayout();
            Object.DestroyImmediate(defaults);
            return layout;
        }

        private static Transform City()
        {
            var city = GameObject.Find("Cidade");
            Assert.IsNotNull(city, "Grupo Cidade da arena");
            return city.transform;
        }

        private static Transform Colliders()
        {
            var group = City().Find("Colisores");
            Assert.IsNotNull(group, "Grupo Cidade/Colisores");
            return group;
        }

        private static BoxCollider[] BuildingColliders() => Colliders().GetComponentsInChildren<BoxCollider>(true);

        /// <summary>Planta do collider no plano: centro, eixo da frente e meias medidas (a mesma convenção do CityBuilder).</summary>
        private static MapRect Footprint(BoxCollider box)
        {
            var t = box.transform;
            var center = t.TransformPoint(box.center);
            var axis = t.forward;
            var scale = t.lossyScale;
            return new MapRect(new Float2(center.x, center.z), new Float2(axis.x, axis.z),
                box.size.z * scale.z * 0.5f, box.size.x * scale.x * 0.5f);
        }

        // ---------- Colisores ----------

        [Test]
        public void Colisores_UmBoxColliderPorPredio_NaCamadaCenario()
        {
            var group = Colliders();
            Assert.Greater(group.childCount, 60, "Prédios o bastante para fechar o mapa");
            Assert.AreEqual(0, group.GetComponentsInChildren<MeshCollider>(true).Length, "Nada de MeshCollider por prédio");
            foreach (Transform child in group)
            {
                var colliders = child.GetComponents<Collider>();
                Assert.AreEqual(1, colliders.Length, $"{child.name}: um collider só");
                Assert.IsInstanceOf<BoxCollider>(colliders[0], child.name);
                Assert.AreEqual(MapLayers.Cenario, child.gameObject.layer, $"{child.name}: camada Cenario");
                Assert.IsFalse(colliders[0].isTrigger, child.name);
            }
        }

        [Test]
        public void Colisores_ForaDeTransformEspelhado()
        {
            var group = Colliders();
            Assert.IsFalse(group.IsChildOf(City().Find("Predios")), "Os colisores não podem ficar sob os prédios (espelhados)");
            foreach (var box in BuildingColliders())
            {
                var scale = box.transform.lossyScale;
                Assert.Greater(scale.x, 0f, box.name);
                Assert.Greater(scale.y, 0f, box.name);
                Assert.Greater(scale.z, 0f, box.name);
            }
        }

        [Test]
        public void Marcos_TorreEPiloesTemColliderNoMeioDasPracasMenores()
        {
            var layout = LoadLayout();
            var group = City().Find("ColisoresMarcos");
            Assert.IsNotNull(group, "Grupo Cidade/ColisoresMarcos");
            Assert.AreEqual(layout.StreetCount, group.childCount, "Um marco por praça menor");
            foreach (Transform marker in group)
            {
                Assert.AreEqual(MapLayers.Cenario, marker.gameObject.layer, marker.name);
                Assert.IsInstanceOf<BoxCollider>(marker.GetComponent<Collider>(), marker.name);
                bool inside = false;
                for (int i = 0; i < layout.StreetCount; i++)
                {
                    var c = layout.SmallPlazas[i].Center;
                    inside |= Vector2.Distance(new Vector2(marker.position.x, marker.position.z), new Vector2(c.X, c.Y)) < 0.5f;
                }
                Assert.IsTrue(inside, $"{marker.name} no centro de uma praça menor");
            }
        }

        // ---------- Corredor andável livre ----------

        [Test]
        public void AreasAndaveis_NaoTemColliderDePredio()
        {
            var layout = LoadLayout();
            var group = Colliders();
            Physics.SyncTransforms();

            int sampled = 0;
            string example = null;
            int bad = 0;
            for (float x = -GridExtent; x <= GridExtent; x += GridStep)
            {
                for (float z = -GridExtent; z <= GridExtent; z += GridStep)
                {
                    if (!layout.IsWalkable(x, z, AgentRadius))
                        continue;
                    sampled++;
                    // Cápsula do agente acima do chão (os pisos andáveis também são Cenario, mas só contam os prédios).
                    var hits = Physics.OverlapCapsule(new Vector3(x, 1f, z), new Vector3(x, 2.4f, z), AgentRadius - SkinTolerance,
                        MapLayers.CenarioMask, QueryTriggerInteraction.Ignore);
                    foreach (var hit in hits)
                    {
                        if (!hit.transform.IsChildOf(group))
                            continue;
                        bad++;
                        example ??= $"({x}, {z}) encosta em {hit.name}";
                    }
                }
            }
            Assert.Greater(sampled, 400, "Pontos andáveis amostrados");
            Assert.AreEqual(0, bad, $"Colliders de prédio invadindo o corredor andável ({example})");
        }

        // ---------- Os prédios são o limite ----------

        [Test]
        public void Predios_BloqueiamAVistaEntreAsAvenidas()
        {
            var group = Colliders();
            Physics.SyncTransforms();
            var origin = new Vector3(0f, RayHeight, 0f);

            foreach (float centerAngle in new[] { WedgeAngle, WedgeMirrorAngle })
            {
                // Um leque de 5 raios de 1°: um vão de 0,7 m entre dois prédios não pode esconder a regra.
                int blocked = 0;
                for (int k = -2; k <= 2; k++)
                {
                    float a = (centerAngle + k) * Mathf.Deg2Rad;
                    var dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                    bool hitBuilding = Physics.RaycastAll(origin, dir, WedgeRayDistance, MapLayers.CenarioMask, QueryTriggerInteraction.Ignore)
                        .Any(h => h.transform.IsChildOf(group));
                    if (hitBuilding)
                        blocked++;
                }
                Assert.GreaterOrEqual(blocked, 4, $"A cunha em {centerAngle}° tem de estar fechada por prédios antes de r {WedgeRayDistance}");
            }
        }

        [Test]
        public void Predios_CercamAPracaCentral_EmTodaAVolta()
        {
            var group = Colliders();
            Physics.SyncTransforms();
            var origin = new Vector3(0f, RayHeight, 0f);
            var layout = LoadLayout();

            // De 5° em 5°, o raio que sai do centro da praça tem de bater num prédio antes de 40 m, menos nas avenidas
            // (aberturas) e num pequeno vão entre dois prédios (tolerância de 10%).
            int open = 0, closed = 0, blocked = 0;
            for (float angle = -180f; angle < 180f; angle += 5f)
            {
                var probe = MapLayout.Polar(angle, 34f); // um ponto da avenida logo depois da fachada, se houver avenida
                if (layout.IsWalkable(probe.X, probe.Y))
                {
                    open++;
                    continue;
                }
                closed++;
                float a = angle * Mathf.Deg2Rad;
                var dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                bool hit = Physics.RaycastAll(origin, dir, 40f, MapLayers.CenarioMask, QueryTriggerInteraction.Ignore)
                    .Any(h => h.transform.IsChildOf(group));
                if (hit)
                    blocked++;
            }
            Assert.Greater(closed, open, "A praça é mais parede que abertura");
            Assert.GreaterOrEqual(blocked, Mathf.CeilToInt(closed * 0.9f), $"Prédios em volta da praça: {blocked} de {closed} direções fechadas");
        }

        // ---------- Altura (D-076) ----------

        [Test]
        public void Predios_NaoPassamDoLimiteDoEnvelopeDaCamera()
        {
            var layout = LoadLayout();
            int limited = 0, free = 0;
            foreach (var box in BuildingColliders())
            {
                var footprint = Footprint(box);
                float limit = layout.MaxBuildingHeight(footprint);
                float height = box.size.y * box.transform.lossyScale.y;
                if (float.IsPositiveInfinity(limit))
                {
                    free++;
                    continue;
                }
                limited++;
                Assert.LessOrEqual(height, limit + EnvelopeTolerance,
                    $"{box.name}: planta toca o envelope da câmera e passa de {limit} m ({height} m)");
            }
            Assert.Greater(limited, 0, "Há prédios dentro do envelope da câmera");
            Assert.Greater(free, 0, "Há prédios altos fora do envelope (o resto da cidade não tem limite)");
        }

        // ---------- Vazável (D-076, D-079) ----------

        [Test]
        public void PecasDaCidade_TemARenderingLayerVazavel_ChaoNao()
        {
            var city = City();
            foreach (string groupName in new[] { "Predios", "Marcos", "PontesCanos" })
            {
                var group = city.Find(groupName);
                Assert.IsNotNull(group, $"Grupo Cidade/{groupName}");
                var renderers = group.GetComponentsInChildren<MeshRenderer>(true);
                // Pontes de canos só existem onde as duas paredes da avenida são altas o bastante (6 m): podem faltar.
                if (groupName != "PontesCanos")
                    Assert.IsNotEmpty(renderers, groupName);
                foreach (var r in renderers)
                {
                    Assert.AreNotEqual(0u, r.renderingLayerMask & MapLayers.VazavelRenderingLayerMask,
                        $"{groupName}/{r.name} sem a rendering layer {MapLayers.VazavelRenderingLayerIndex}");
                }
            }

            var ground = city.Find("Chao");
            Assert.IsNotNull(ground, "Chão visual da cidade");
            foreach (var r in ground.GetComponentsInChildren<MeshRenderer>(true))
                Assert.AreEqual(0u, r.renderingLayerMask & MapLayers.VazavelRenderingLayerMask, "O chão não pode ser recortado");
        }

        [Test]
        public void ChaoVisual_CobreAteOAlcanceDaCamera_SemCollider()
        {
            var ground = City().Find("Chao");
            Assert.IsNotNull(ground);
            Assert.AreEqual(0, ground.GetComponentsInChildren<Collider>(true).Length, "O chão visual da cidade não tem collider");
            var bounds = new Bounds();
            bool first = true;
            foreach (var r in ground.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (first) { bounds = r.bounds; first = false; }
                else bounds.Encapsulate(r.bounds);
            }
            Assert.GreaterOrEqual(bounds.extents.x, 84f, "Chão até r 85 (a câmera alcança ~77)");
            Assert.GreaterOrEqual(bounds.extents.z, 84f);
        }
    }
}
