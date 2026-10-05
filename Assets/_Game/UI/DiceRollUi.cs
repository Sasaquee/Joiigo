using System.Collections.Generic;
using Game.Dice;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// D20 grande na tela (D-047): quando alguém pega uma carta do chão, o dado de latão e cristal rola no centro
    /// da tela, desacelera e assenta com o número sorteado de frente e em pé. O jogo segue por trás; nada aqui recebe o mouse.
    /// O dado é 3D (Art/Models/Dice/D20.fbx) num "palco" longe da arena, renderizado numa textura pequena e ampliada
    /// sem suavizar, no mesmo pixel do jogo. Todos os jogadores veem a mesma rolagem (CardDropService.RollStarted).
    /// Criado pelo construtor da arena sob a Canvas "UI".
    /// </summary>
    public class DiceRollUi : MonoBehaviour
    {
        // Palco do dado: longe da arena e da cidade, onde nenhuma outra câmera olha.
        private static readonly Vector3 StagePosition = new Vector3(0f, -500f, 0f);
        private const float CameraDistance = 2.6f;
        private const float CameraFov = 30f;
        // Tamanho do dado na tela (fração da altura) e pixels do jogo (o jogo renderiza a 360 de altura).
        private const float ScreenHeightFraction = 0.42f;
        private const float GamePixelsTall = 360f;
        // Animação (frações da duração recebida).
        private const float SettleStart = 0.72f;
        private const float FadeTime = 0.35f;
        private const float HoldAfter = 0.9f;

        [SerializeField] private GameObject diePrefab;

        private RawImage image;
        private RenderTexture texture;
        private Camera stageCamera;
        private Transform die;
        private readonly Dictionary<int, Quaternion> faceUp = new Dictionary<int, Quaternion>();

        private bool playing;
        private float time;
        private float duration;
        private int result;
        private Vector3 spinAxis;
        private float spinSpeed;
        private Quaternion settleFrom;
        private Quaternion settleTo;
        private bool settling;

        /// <summary>Número mostrado na rolagem atual (0 se nenhuma). Para testes.</summary>
        public int ShowingResult => playing ? result : 0;
        public bool IsPlaying => playing;

        public void Configure(GameObject prefab) => diePrefab = prefab;

        private void OnEnable() => CardDropService.RollStarted += OnRollStarted;

        private void OnDisable()
        {
            CardDropService.RollStarted -= OnRollStarted;
            playing = false;
            if (image != null)
                image.enabled = false;
        }

        private void OnDestroy()
        {
            if (texture != null)
                texture.Release();
            if (stageCamera != null)
                Destroy(stageCamera.transform.parent.gameObject);
        }

        private void EnsureStage()
        {
            if (die != null || diePrefab == null)
                return;

            var stage = new GameObject("PalcoDoDado").transform;
            stage.position = StagePosition;

            die = Instantiate(diePrefab, stage).transform;
            die.localPosition = Vector3.zero;
            die.localRotation = Quaternion.identity;
            BuildFaceTable();

            var camGo = new GameObject("CameraDoDado");
            camGo.transform.SetParent(stage, false);
            camGo.transform.localPosition = new Vector3(0f, 0f, -CameraDistance);
            camGo.transform.localRotation = Quaternion.identity;
            stageCamera = camGo.AddComponent<Camera>();
            stageCamera.fieldOfView = CameraFov;
            stageCamera.nearClipPlane = 0.5f;
            stageCamera.farClipPlane = CameraDistance + 2f;
            stageCamera.clearFlags = CameraClearFlags.SolidColor;
            stageCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            stageCamera.enabled = false;

            // Luz quente de frente e um contorno ciano atrás: latão e cristal (Pilar 4).
            AddLight(stage, new Vector3(-1.2f, 1.6f, -2f), new Color(1f, 0.85f, 0.6f), 6f);
            AddLight(stage, new Vector3(1.4f, -0.4f, 1.6f), new Color(0.35f, 0.9f, 1f), 5f);

            int side = Mathf.Max(64, Mathf.RoundToInt(GamePixelsTall * ScreenHeightFraction));
            texture = new RenderTexture(side, side, 16, RenderTextureFormat.ARGB32) { filterMode = FilterMode.Point };
            texture.Create();
            stageCamera.targetTexture = texture;

            var rect = UiFactory.MakeRect("Dado", transform);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.52f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            image = rect.gameObject.AddComponent<RawImage>();
            image.texture = texture;
            image.raycastTarget = false;
            image.enabled = false;
        }

        private static void AddLight(Transform parent, Vector3 localPosition, Color color, float intensity)
        {
            var go = new GameObject("Luz");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = 6f;
        }

        /// <summary>
        /// Para cada número N, a rotação do dado que deixa a face N de frente para a câmera (-Z do palco) e o
        /// número em pé (Topo_N para cima). Vem dos Empties Centro_N e Topo_N do modelo.
        /// </summary>
        private void BuildFaceTable()
        {
            faceUp.Clear();
            foreach (Transform t in die.GetComponentsInChildren<Transform>(true))
            {
                if (!t.name.StartsWith("Centro_") || !int.TryParse(t.name.Substring(7), out int n))
                    continue;
                Transform top = FindChild(die, "Topo_" + n);
                if (top == null)
                    continue;
                Vector3 normal = die.InverseTransformPoint(t.position).normalized;
                Vector3 up = die.InverseTransformPoint(top.position) - die.InverseTransformPoint(t.position);
                if (normal.sqrMagnitude < 0.5f || up.sqrMagnitude < 1e-6f)
                    continue;
                Quaternion faceFrame = Quaternion.LookRotation(normal, up.normalized);
                Quaternion viewFrame = Quaternion.LookRotation(Vector3.back, Vector3.up); // de frente para a câmera
                faceUp[n] = viewFrame * Quaternion.Inverse(faceFrame);
            }
        }

        private static Transform FindChild(Transform root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name)
                    return t;
            return null;
        }

        private void OnRollStarted(ulong roller, int value, float rollDuration)
        {
            EnsureStage();
            if (die == null)
                return;

            result = value;
            duration = Mathf.Max(0.5f, rollDuration);
            time = 0f;
            playing = true;
            settling = false;
            spinAxis = Random.onUnitSphere;
            spinSpeed = Random.Range(900f, 1300f);
            die.localRotation = Random.rotationUniform;
            image.enabled = true;
            stageCamera.enabled = true;
        }

        private void Update()
        {
            if (!playing || die == null)
                return;

            time += Time.unscaledDeltaTime;
            float settleAt = duration * SettleStart;

            if (time < settleAt)
            {
                // Rola desacelerando, com um quique que vai baixando.
                float k = time / settleAt;
                float speed = spinSpeed * (1f - k) * (1f - k) + 120f;
                die.localRotation = Quaternion.AngleAxis(speed * Time.unscaledDeltaTime, spinAxis) * die.localRotation;
                float bounce = Mathf.Abs(Mathf.Sin(k * Mathf.PI * 3f)) * (1f - k) * 0.35f;
                die.localPosition = new Vector3(0f, bounce, 0f);
            }
            else if (time < duration)
            {
                if (!settling)
                {
                    settling = true;
                    settleFrom = die.localRotation;
                    settleTo = faceUp.TryGetValue(result, out var q) ? q : die.localRotation;
                }
                float k = Mathf.Clamp01((time - settleAt) / (duration - settleAt));
                float ease = 1f - Mathf.Pow(1f - k, 3f);
                die.localRotation = Quaternion.Slerp(settleFrom, settleTo, ease);
                die.localPosition = Vector3.Lerp(die.localPosition, Vector3.zero, ease);
            }
            else
            {
                die.localRotation = settleTo;
                die.localPosition = Vector3.zero;
            }

            // Tamanho na tela e desaparecimento no fim.
            var parent = transform as RectTransform;
            float height = (parent != null && parent.rect.height > 1f ? parent.rect.height : Screen.height) * ScreenHeightFraction;
            image.rectTransform.sizeDelta = new Vector2(height, height);
            float end = duration + HoldAfter;
            float alpha = time < end ? 1f : 1f - (time - end) / FadeTime;
            image.color = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));

            if (time >= end + FadeTime)
            {
                playing = false;
                image.enabled = false;
                stageCamera.enabled = false;
            }
        }
    }
}
