using Game.Arena;
using Game.Core.Map;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace Game.EditorTools
{
    /// <summary>
    /// Assa a NavMesh do mapa (passe do mapa, D-077, D-080): só para calcular caminhos dos inimigos pelas ruas,
    /// sem NavMeshAgent (o CharacterController continua movendo). Coloca um NavMeshSurface em Arena/Navegacao que junta os
    /// PhysicsColliders estáticos do cenário (chão andável, prédios, vedação, máquinas) nas camadas Default e Cenario,
    /// dentro de um volume que cobre o mapa, e grava o NavMeshData em Assets/_Game/Arena/ArenaNavMesh.asset (apagando o anterior).
    /// Jogador e inimigos só existem em jogo, então nada com CharacterController entra no bake. Funciona em batchmode (CPU).
    /// Chamado no fim do <see cref="ArenaBuilder.Build"/>, depois do chão, dos portões e da cidade.
    /// </summary>
    public static class ArenaNavMeshBuilder
    {
        public const string NavMeshAssetPath = "Assets/_Game/Arena/ArenaNavMesh.asset";
        private const string LayoutSettingsPath = "Assets/_Game/Data/Map/MapLayoutSettings.asset";
        private const string GroupName = "Navegacao";
        private const string HumanoidName = "Humanoid"; // agente de ProjectSettings/NavMeshAreas.asset (raio 0,9)

        // Volume do bake (técnico, não é gameplay): do chão (-1 m) até 3 m acima. Telhados e pontes de cano ficam de fora,
        // então não nascem ilhas de NavMesh em cima dos prédios; as paredes dos prédios entram só como obstáculo.
        private const float VolumeBottom = -1f;
        private const float VolumeTop = 3f;
        private const float VolumeMargin = 6f;

        /// <summary>Assa com o layout do asset <c>Data/Map/MapLayoutSettings.asset</c> (ou o padrão se ele não existir).</summary>
        public static NavMeshSurface Bake(Transform arenaRoot)
        {
            var settings = AssetDatabase.LoadAssetAtPath<MapLayoutSettings>(LayoutSettingsPath);
            return Bake(arenaRoot, settings != null ? settings.ToLayout() : new MapLayout());
        }

        public static NavMeshSurface Bake(Transform arenaRoot, MapLayout layout)
        {
            var old = arenaRoot.Find(GroupName);
            if (old != null)
                Object.DestroyImmediate(old.gameObject);
            NavMesh.RemoveAllNavMeshData(); // sobras de uma arena anterior registradas no editor

            var go = new GameObject(GroupName);
            go.transform.SetParent(arenaRoot, false);
            go.isStatic = true;
            var surface = go.AddComponent<NavMeshSurface>();
            surface.agentTypeID = HumanoidAgentTypeId();
            surface.collectObjects = CollectObjects.Volume;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask = (1 << 0) | MapLayers.CenarioMask; // Default + Cenario
            surface.defaultArea = 0; // Walkable
            surface.ignoreNavMeshAgent = true;
            surface.ignoreNavMeshObstacle = true;
            surface.buildHeightMesh = false;
            ComputeVolume(layout, out Vector3 center, out Vector3 size);
            surface.center = center;
            surface.size = size;
            MarkObstaclesNotWalkable(arenaRoot);

            surface.BuildNavMesh();
            if (surface.navMeshData == null)
                throw new System.InvalidOperationException("O bake da NavMesh não gerou dados: confira o chão andável e as camadas.");

            // Grava como asset (apagando o anterior) e recarrega pelo caminho: a referência da cena é a do asset.
            AssetDatabase.DeleteAsset(NavMeshAssetPath);
            AssetDatabase.CreateAsset(surface.navMeshData, NavMeshAssetPath);
            AssetDatabase.SaveAssets();
            var saved = AssetDatabase.LoadAssetAtPath<NavMeshData>(NavMeshAssetPath);
            if (saved == null)
                throw new System.InvalidOperationException($"NavMeshData não encontrada em {NavMeshAssetPath} depois de gravar.");
            surface.navMeshData = saved;
            EditorUtility.SetDirty(surface);

            Debug.Log($"NavMesh assada (agente {NavMesh.GetSettingsNameFromID(surface.agentTypeID)}, raio {NavMesh.GetSettingsByID(surface.agentTypeID).agentRadius:0.##} m) em {NavMeshAssetPath}.");
            return surface;
        }

        // Grupos de colliders que são obstáculo: prédios, marcos (torre e pilões), vedação e lampiões.
        private static readonly string[] ObstacleGroups = { "Colisores", "ColisoresMarcos", "Vedacao", "LampioesCristal" };
        private const int NotWalkableArea = 1; // "Not Walkable" de NavMeshAreas.asset

        /// <summary>
        /// Os colliders dos prédios e dos marcos são caixas ocas para o gerador de NavMesh: o chão dentro da pegada deles
        /// virava uma ilha andável (e uma carta ou um inimigo podia cair lá dentro). Marca cada grupo como "Not Walkable"
        /// para o bake abrir um buraco na pegada de cada peça.
        /// </summary>
        private static void MarkObstaclesNotWalkable(Transform arenaRoot)
        {
            foreach (Transform t in arenaRoot.GetComponentsInChildren<Transform>(true))
            {
                if (System.Array.IndexOf(ObstacleGroups, t.name) < 0 || t.GetComponent<NavMeshModifier>() != null)
                    continue;
                var modifier = t.gameObject.AddComponent<NavMeshModifier>();
                modifier.overrideArea = true;
                modifier.area = NotWalkableArea;
                modifier.applyToChildren = true;
            }
        }

        /// <summary>O agente "Humanoid" de NavMeshAreas.asset; se não houver com esse nome, o primeiro.</summary>
        private static int HumanoidAgentTypeId()
        {
            int count = NavMesh.GetSettingsCount();
            for (int i = 0; i < count; i++)
            {
                var s = NavMesh.GetSettingsByIndex(i);
                if (NavMesh.GetSettingsNameFromID(s.agentTypeID) == HumanoidName)
                    return s.agentTypeID;
            }
            return NavMesh.GetSettingsByIndex(0).agentTypeID;
        }

        /// <summary>Caixa em volta de toda a área andável (praça, avenidas, praças menores, bocas) mais uma margem, na altura do volume.</summary>
        private static void ComputeVolume(MapLayout layout, out Vector3 center, out Vector3 size)
        {
            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;

            void Include(float x, float z)
            {
                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
                if (z < minZ) minZ = z;
                if (z > maxZ) maxZ = z;
            }

            void IncludeDisc(MapDisc d)
            {
                Include(d.Center.X - d.Radius, d.Center.Y - d.Radius);
                Include(d.Center.X + d.Radius, d.Center.Y + d.Radius);
            }

            void IncludeRect(MapRect r)
            {
                foreach (float along in new[] { -r.HalfLength, r.HalfLength })
                    foreach (float across in new[] { -r.HalfWidth, r.HalfWidth })
                    {
                        var p = r.ToWorld(along, across);
                        Include(p.X, p.Y);
                    }
            }

            IncludeDisc(layout.Plaza);
            for (int i = 0; i < layout.StreetCount; i++)
            {
                IncludeRect(layout.Avenues[i].Rect);
                IncludeDisc(layout.SmallPlazas[i].Disc);
                IncludeRect(layout.Mouths[i].Rect);
            }

            minX -= VolumeMargin; maxX += VolumeMargin;
            minZ -= VolumeMargin; maxZ += VolumeMargin;
            center = new Vector3((minX + maxX) * 0.5f, (VolumeBottom + VolumeTop) * 0.5f, (minZ + maxZ) * 0.5f);
            size = new Vector3(maxX - minX, VolumeTop - VolumeBottom, maxZ - minZ);
        }
    }
}
