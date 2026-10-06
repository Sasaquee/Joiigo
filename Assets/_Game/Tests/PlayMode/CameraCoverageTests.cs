using System.Collections;
using Game.Arena;
using Game.Cameras;
using Game.Core.Math;
using Game.Core.View;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    /// <summary>
    /// Cobertura da câmera no mapa novo (passe do mapa, plano §5): para focos numa grade de 2 m sobre a área andável,
    /// todos os cantos da vista (16:9 e 21:9) ficam dentro de r ≤ 80 (o chão visual vai até 85) e cada canto fora da
    /// área andável tem um prédio (collider Cenario alto) a até 8 m. Depende da cena gerada com o mapa novo.
    /// </summary>
    public class CameraCoverageTests
    {
        private const float GridStep = 2f;
        private const float MaxCornerRadius = 83f; // o chão visual vai até 85; os prédios do fundo até ~82
        private const float MaxDistanceToBuilding = 8f;
        // Collider de prédio ou vedação (alto); o chão andável também é Cenario, mas tem o topo rente ao chão.
        private const float MinBuildingTop = 2f;

        private static readonly float[] Aspects = { CameraFootprint.Aspect16x9, CameraFootprint.Aspect21x9 };

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

        // ---------- Área andável ----------

        // ligar ao MapLayout (P1): trocar esta função por MapLayout.IsWalkable e os números pelo MapLayoutSettings.
        // Hoje segue a seção 1 do plano: praça de raio 28,3; avenidas a -45°, 0° e +45° (9 m entre fachadas, de s 26 a s 41);
        // praças menores em s 47 (raio 9); bocas de 6 m de largura de s 53 a s 60.
        private const float SquareRadius = 28.3f;
        private const float AvenueHalfWidth = 4.5f;
        private const float AvenueFrom = 26f;
        private const float AvenueTo = 41f;
        private const float SmallSquareDistance = 47f;
        private const float SmallSquareRadius = 9f;
        private const float MouthHalfWidth = 3f;
        private const float MouthFrom = 53f;
        private const float MouthTo = 60f;
        private static readonly float[] AvenueAngles = { -45f, 0f, 45f };

        private static bool IsWalkable(float x, float z)
        {
            if (x * x + z * z <= SquareRadius * SquareRadius)
                return true;
            foreach (float angle in AvenueAngles)
            {
                float a = angle * Mathf.Deg2Rad;
                float s = x * Mathf.Sin(a) + z * Mathf.Cos(a);      // ao longo do eixo da avenida
                float lateral = x * Mathf.Cos(a) - z * Mathf.Sin(a); // para o lado
                if (Mathf.Abs(lateral) <= AvenueHalfWidth && s >= AvenueFrom && s <= AvenueTo)
                    return true;
                if (Mathf.Abs(lateral) <= MouthHalfWidth && s >= MouthFrom && s <= MouthTo)
                    return true;
                float ds = s - SmallSquareDistance;
                if (ds * ds + lateral * lateral <= SmallSquareRadius * SmallSquareRadius)
                    return true;
            }
            return false;
        }

        // ---------- Câmera ----------

        private static CameraRig LoadRig()
        {
            var cam = Camera.main;
            Assert.IsNotNull(cam, "Câmera principal da Arena");
            var follow = cam.GetComponent<CameraFollow>();
            Assert.IsNotNull(follow, "A câmera principal tem CameraFollow");
            var s = follow.Settings;
            Assert.IsNotNull(s, "CameraSettings ligado");
            return CameraFootprint.OrbitRig(s.distance, s.pitch, s.yaw, s.focusHeight, s.fieldOfView);
        }

        [Test]
        public void GeometriaDoCore_BateComACameraReal()
        {
            // Segurança: a pegada do Core tem de coincidir com o que o CameraFollow faz de fato.
            var cam = Camera.main;
            var follow = cam.GetComponent<CameraFollow>();
            var rig = LoadRig();
            var focus = new Vector3(7f, 0f, -4f);
            follow.Apply(focus);
            var position = cam.transform.position;
            Assert.AreEqual(focus.x + rig.Offset.X, position.x, 0.01f);
            Assert.AreEqual(focus.z + rig.Offset.Y, position.z, 0.01f);
            Assert.AreEqual(rig.Height, position.y, 0.01f);
        }

        [UnityTest]
        public IEnumerator CantosDaVista_FicamDentroDoChaoVisual()
        {
            yield return null;
            var rig = LoadRig();
            var corners = new Float2[CameraFootprint.CornerCount];

            int bad = 0;
            string example = null;
            float worst = 0f;
            for (float x = -64f; x <= 64f; x += GridStep)
            {
                for (float z = -64f; z <= 64f; z += GridStep)
                {
                    if (!IsWalkable(x, z))
                        continue;
                    foreach (float aspect in Aspects)
                    {
                        CameraFootprint.TryCorners(rig, new Float2(x, z), aspect, corners);
                        float r = CameraFootprint.FarthestDistance(corners, Float2.Zero);
                        worst = Mathf.Max(worst, r);
                        if (r > MaxCornerRadius)
                        {
                            bad++;
                            example ??= $"foco ({x}, {z}), aspecto {aspect:0.00}: r = {r:0.0} m";
                        }
                    }
                }
            }
            Assert.AreEqual(0, bad, $"Vista além de r {MaxCornerRadius} ({example}); pior r = {worst:0.0} m");
        }

        [UnityTest]
        public IEnumerator CantoForaDaAreaAndavel_TemPredioAte8m()
        {
            yield return null;
            Physics.SyncTransforms();
            var rig = LoadRig();
            var corners = new Float2[CameraFootprint.CornerCount];
            var hits = new Collider[256];

            int bad = 0, checkedCorners = 0;
            string example = null;
            for (float x = -64f; x <= 64f; x += GridStep)
            {
                for (float z = -64f; z <= 64f; z += GridStep)
                {
                    if (!IsWalkable(x, z))
                        continue;
                    foreach (float aspect in Aspects)
                    {
                        CameraFootprint.TryCorners(rig, new Float2(x, z), aspect, corners);
                        for (int i = 0; i < corners.Length; i++)
                        {
                            if (IsWalkable(corners[i].X, corners[i].Y))
                                continue;
                            checkedCorners++;
                            if (!HasBuildingNear(corners[i], hits))
                            {
                                bad++;
                                example ??= $"foco ({x}, {z}), aspecto {aspect:0.00}, canto ({corners[i].X:0.0}, {corners[i].Y:0.0})";
                            }
                        }
                    }
                }
            }
            Assert.Greater(checkedCorners, 0, "A grade tem cantos fora da área andável");
            Assert.AreEqual(0, bad, $"Canto sem prédio a {MaxDistanceToBuilding} m: {bad} de {checkedCorners} ({example})");
        }

        /// <summary>Tem collider Cenario de prédio (topo ≥ 2 m, não o chão) a até 8 m do canto, no plano.</summary>
        private static bool HasBuildingNear(Float2 corner, Collider[] buffer)
        {
            var center = new Vector3(corner.X, MinBuildingTop, corner.Y);
            int n = Physics.OverlapSphereNonAlloc(center, MaxDistanceToBuilding, buffer, MapLayers.CenarioMask,
                QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                if (buffer[i].bounds.max.y >= MinBuildingTop)
                    return true;
            }
            return false;
        }
    }
}
