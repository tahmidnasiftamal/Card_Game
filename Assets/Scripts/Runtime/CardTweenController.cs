using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace CrimsonDynasty
{
    public class CardTweenController : MonoBehaviour
    {
        [Header("Targets (UI)")]
        [SerializeField] private RectTransform rect;     // UI card (if used)
        [SerializeField] private Image mainImage;        // front image (for color/glow)
        [SerializeField] private CanvasGroup canvasGroup;// for fade

        [Header("Targets (World)")]
        [SerializeField] private Transform worldTarget;  // non-UI alternative
        [SerializeField] private SpriteRenderer spriteRenderer;

        [Header("Defaults")]
        [SerializeField] private float defaultMove = 100f;     // px (UI) or units (world)
        [SerializeField] private float defaultDuration = 0.6f;

        private Tween _loop;     // current loop tween (float/move/orbit)
        private Sequence _glow;

        private Transform T => (rect ? rect as Transform : worldTarget ? worldTarget : transform);

        private void Reset()
        {
            rect = GetComponent<RectTransform>();
            mainImage = GetComponentInChildren<Image>();
            canvasGroup = GetComponentInChildren<CanvasGroup>();
            worldTarget = transform;
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        // ---------- Spawning / Deleting ----------
        public void SpawnPop(float duration = 0.35f, float overshoot = 1.1f)
        {
            KillAll();

            var t = T;
            var startScale = t.localScale;
            t.localScale = Vector3.zero;

            EnsureCanvasGroup();
            canvasGroup.alpha = 0f;

            var s = DOTween.Sequence();
            s.Append(t.DOScale(overshoot, duration * 0.6f).SetEase(Ease.OutBack))
             .Append(t.DOScale(startScale, duration * 0.4f).SetEase(Ease.OutSine))
             .Join(canvasGroup.DOFade(1f, duration));
        }

        public void FadeAway(float duration = 0.5f)
        {
            EnsureCanvasGroup();
            canvasGroup.DOFade(0f, duration);
        }

        public void DeleteWithFade(float duration = 0.5f)
        {
            EnsureCanvasGroup();
            canvasGroup.DOFade(0f, duration).OnComplete(() => Destroy(gameObject));
        }

        // ---------- Rotations ----------
        public void SpinZ(float degreesPerSec = 360f)
        {
            KillLoop();
            _loop = T.DORotate(new Vector3(0, 0, 360), 360f / Mathf.Abs(degreesPerSec), RotateMode.FastBeyond360)
                    .SetLoops(-1, LoopType.Restart)
                    .SetEase(Ease.Linear);
        }

        public void FlipY(float duration = 0.4f) =>
            T.DORotate(new Vector3(0, 180f, 0), duration, RotateMode.Fast).SetRelative();

        public void FlipX(float duration = 0.4f) =>
            T.DORotate(new Vector3(180f, 0, 0), duration, RotateMode.Fast).SetRelative();

        // ---------- Floating / Movement ----------
        public void StartFloat(float amplitude = 30f, float period = 1.4f)
        {
            KillLoop();
            var start = T.localPosition;

            // Use a manual updater to create a smooth sine float
            _loop = DOTween.To(() => 0f, _ =>
            {
                float angVel = Mathf.PI * 2f / period;
                float y = Mathf.Sin(Time.realtimeSinceStartup * angVel) * amplitude;
                var p = start; p.y += y;
                T.localPosition = p;
            }, 1f, 999f).SetUpdate(true);
        }

        public void MoveUpDown(float distance, float duration, int loops = -1)
        {
            KillLoop();
            _loop = T.DOLocalMoveY(T.localPosition.y + distance, duration)
                    .SetLoops(loops, LoopType.Yoyo)
                    .SetEase(Ease.InOutSine);
        }

        public void MoveLeftRight(float distance, float duration, int loops = -1)
        {
            KillLoop();
            _loop = T.DOLocalMoveX(T.localPosition.x + distance, duration)
                    .SetLoops(loops, LoopType.Yoyo)
                    .SetEase(Ease.InOutSine);
        }

        public void OrbitLocal(float radius = 120f, float period = 2.5f)
        {
            KillLoop();
            var center = T.localPosition;

            _loop = DOTween.To(() => 0f, theta =>
            {
                float angVel = Mathf.PI * 2f / period;
                float a = theta * angVel;
                var p = center + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * radius;
                T.localPosition = p;
            }, 999f, 999f).SetEase(Ease.Linear).SetUpdate(true);
        }

        // ---------- Glow / Highlight ----------
        public void GlowPulse(Color color, float maxMultiplier = 1.2f, float period = 0.8f)
        {
            KillGlow();

            if (mainImage)
            {
                var baseColor = mainImage.color;
                _glow = DOTween.Sequence();
                _glow.Append(DOTween.To(() => 0f, v => mainImage.color = Color.Lerp(baseColor, color, v),
                                        1f, period * 0.5f).SetEase(Ease.InOutSine))
                     .Append(DOTween.To(() => 1f, v => mainImage.color = Color.Lerp(baseColor, color, v),
                                        0f, period * 0.5f).SetEase(Ease.InOutSine))
                     .SetLoops(-1);
            }
            else if (spriteRenderer)
            {
                var baseColor = spriteRenderer.color;
                _glow = DOTween.Sequence();
                _glow.Append(DOTween.To(() => 0f, v => spriteRenderer.color = Color.Lerp(baseColor, color, v),
                                        1f, period * 0.5f).SetEase(Ease.InOutSine))
                     .Append(DOTween.To(() => 1f, v => spriteRenderer.color = Color.Lerp(baseColor, color, v),
                                        0f, period * 0.5f).SetEase(Ease.InOutSine))
                     .SetLoops(-1);
            }
        }

        public void StopGlow() => KillGlow();

        // ---------- Helpers ----------
        public void StopAllMotion()
        {
            KillLoop();
            T.DOKill(); // kills non-looping tweens on this transform
        }

        private void KillLoop() { _loop?.Kill(); _loop = null; }
        private void KillGlow() { _glow?.Kill(); _glow = null; }

        private void KillAll()
        {
            KillLoop();
            KillGlow();
            T.DOKill();
            if (mainImage) mainImage.DOKill();
            if (spriteRenderer) spriteRenderer.DOKill();
            if (canvasGroup) canvasGroup.DOKill();
        }

        private void EnsureCanvasGroup()
        {
            if (!canvasGroup)
            {
                canvasGroup = GetComponentInChildren<CanvasGroup>();
                if (!canvasGroup)
                {
                    var go = new GameObject("CanvasGroup");
                    go.transform.SetParent(transform, false);
                    canvasGroup = go.AddComponent<CanvasGroup>();
                }
            }
        }
    }
}
