using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class BossStagedDestructionController : MonoBehaviour
{
    [Header("Source")]
    [SerializeField] private SpriteDissolveController sourceDissolveController;
    [SerializeField] private Shader verticalMaskShader;
    [SerializeField] private bool prepareOnAwake = true;

    [Header("Fragmentation")]
    [SerializeField] private Explodable.ShatterType shatterType = Explodable.ShatterType.Voronoi;
    [SerializeField] private int extraPoints = 12;
    [SerializeField] private int subshatterSteps = 0;
    [SerializeField] private float releaseBandHeight = 0.55f;

    [Header("Mask")]
    [SerializeField] private float maskNoiseScale = 0.12f;
    [SerializeField] private float maskNoiseAmplitude = 0.45f;
    [SerializeField] private float maskSoftness = 0.32f;
    [SerializeField] private float edgeWidth = 0.12f;
    [SerializeField] private Color edgeColor = new Color(0.75f, 0.84f, 1f, 1f);

    [Header("Fragment Motion")]
    [SerializeField] private float radialImpulse = 1.35f;
    [SerializeField] private float upwardImpulse = 1.8f;
    [SerializeField] private float randomTorque = 18f;
    [SerializeField] private float gravityScale = 0.95f;
    [SerializeField] private float linearDrag = 0.08f;
    [SerializeField] private float angularDrag = 0.08f;

    [Header("Fragment Dissolve")]
    [SerializeField] private float fragmentDissolveDelay = 0.02f;
    [SerializeField] private float fragmentDissolveDuration = 1.7f;
    [SerializeField] private float fragmentEdgeWidth = 0.05f;

    private readonly List<SourceState> sourceStates = new List<SourceState>();
    private readonly List<FragmentState> fragmentStates = new List<FragmentState>();
    private readonly Dictionary<SpriteRenderer, Material[]> originalMaterials = new Dictionary<SpriteRenderer, Material[]>();
    private Material sharedMaskMaterial;
    private Transform fragmentsRoot;
    private bool prepared;
    private float destructionMinY;
    private float destructionMaxY;
    private float currentProgress;

    private static readonly int FrontYId = Shader.PropertyToID("_FrontY");
    private static readonly int NoiseTexId = Shader.PropertyToID("_NoiseTex");
    private static readonly int NoiseScaleId = Shader.PropertyToID("_NoiseScale");
    private static readonly int NoiseAmplitudeId = Shader.PropertyToID("_NoiseAmplitude");
    private static readonly int SoftnessId = Shader.PropertyToID("_Softness");
    private static readonly int EdgeWidthId = Shader.PropertyToID("_EdgeWidth");
    private static readonly int EdgeColorId = Shader.PropertyToID("_EdgeColor");
    private static readonly int DissolveAmountId = Shader.PropertyToID("_DissolveAmount");
    private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    public bool IsPrepared => prepared;

    public void BindSource(SpriteDissolveController controller)
    {
        sourceDissolveController = controller;
    }

    private void Awake()
    {
        ResolveDefaults();
        if (prepareOnAwake)
            PrepareIfNeeded();
    }

    private void OnDisable()
    {
        RestoreOriginalRenderers();
    }

    public void PrepareIfNeeded()
    {
        if (prepared)
            return;

        ResolveDefaults();
        CleanupFragments();
        sourceStates.Clear();
        fragmentStates.Clear();
        originalMaterials.Clear();

        SpriteRenderer[] targetRenderers = GetSourceRenderers();
        if (targetRenderers == null || targetRenderers.Length == 0)
            return;

        if (verticalMaskShader == null)
        {
            Debug.LogWarning("BossStagedDestructionController: vertical mask shader was not found.");
            return;
        }

        if (sharedMaskMaterial == null)
        {
            sharedMaskMaterial = new Material(verticalMaskShader);
            sharedMaskMaterial.name = "BossStagedMask(Runtime)";
        }

        Texture2D noiseTexture = sourceDissolveController != null ? sourceDissolveController.DissolveTexture : null;
        if (noiseTexture != null)
            sharedMaskMaterial.SetTexture(NoiseTexId, noiseTexture);

        destructionMinY = float.PositiveInfinity;
        destructionMaxY = float.NegativeInfinity;

        fragmentsRoot = new GameObject("__BossStagedFragments").transform;
        fragmentsRoot.SetParent(transform, false);
        fragmentsRoot.localPosition = Vector3.zero;
        fragmentsRoot.localRotation = Quaternion.identity;
        fragmentsRoot.localScale = Vector3.one;
        fragmentsRoot.gameObject.SetActive(true);

        for (int i = 0; i < targetRenderers.Length; i++)
        {
            SpriteRenderer sourceRenderer = targetRenderers[i];
            if (sourceRenderer == null || sourceRenderer.sprite == null)
                continue;

            Bounds bounds = sourceRenderer.bounds;
            destructionMinY = Mathf.Min(destructionMinY, bounds.min.y);
            destructionMaxY = Mathf.Max(destructionMaxY, bounds.max.y);

            originalMaterials[sourceRenderer] = sourceRenderer.sharedMaterials;
            sourceStates.Add(new SourceState(sourceRenderer, new MaterialPropertyBlock()));

            List<GameObject> generated = GenerateFragmentsForRenderer(sourceRenderer);
            for (int pieceIndex = 0; pieceIndex < generated.Count; pieceIndex++)
            {
                GameObject piece = generated[pieceIndex];
                if (piece == null)
                    continue;

                piece.transform.SetParent(fragmentsRoot, true);

                MeshRenderer meshRenderer = piece.GetComponent<MeshRenderer>();
                Rigidbody2D rb = piece.GetComponent<Rigidbody2D>();
                Collider2D collider = piece.GetComponent<Collider2D>();
                if (meshRenderer == null || rb == null)
                {
                    Object.Destroy(piece);
                    continue;
                }

                meshRenderer.sortingLayerID = sourceRenderer.sortingLayerID;
                meshRenderer.sortingOrder = sourceRenderer.sortingOrder;
                piece.layer = sourceRenderer.gameObject.layer;

                rb.gravityScale = gravityScale;
                rb.drag = linearDrag;
                rb.angularDrag = angularDrag;
                rb.bodyType = RigidbodyType2D.Kinematic;
                rb.velocity = Vector2.zero;
                rb.angularVelocity = 0f;
                rb.simulated = false;

                if (collider != null)
                {
                    collider.enabled = false;
                    collider.isTrigger = true;
                }

                piece.SetActive(false);

                FragmentState state = new FragmentState(
                    piece,
                    meshRenderer,
                    rb,
                    collider,
                    piece.transform.position,
                    piece.transform.rotation,
                    piece.transform.localScale,
                    meshRenderer.bounds.center.y);
                fragmentStates.Add(state);
            }
        }

        if (sourceStates.Count == 0)
        {
            CleanupFragments();
            return;
        }

        if (!float.IsFinite(destructionMinY) || !float.IsFinite(destructionMaxY) || destructionMaxY <= destructionMinY)
        {
            Bounds bounds = sourceDissolveController != null ? sourceDissolveController.CalculateTargetBounds() : default;
            destructionMinY = bounds.min.y;
            destructionMaxY = bounds.max.y;
        }

        prepared = true;
        ResetState();
    }

    public void ForceRebuild()
    {
        prepared = false;
        CleanupFragments();
        sourceStates.Clear();
        fragmentStates.Clear();
        originalMaterials.Clear();
        PrepareIfNeeded();
    }

    public void ResetState()
    {
        if (!prepared)
            return;

        currentProgress = 0f;

        for (int i = 0; i < fragmentStates.Count; i++)
        {
            FragmentState fragment = fragmentStates[i];
            if (fragment.GameObject == null)
                continue;

            fragment.Released = false;
            fragment.GameObject.SetActive(false);
            fragment.Transform.position = fragment.InitialPosition;
            fragment.Transform.rotation = fragment.InitialRotation;
            fragment.Transform.localScale = fragment.InitialScale;

            if (fragment.Rigidbody != null)
            {
                fragment.Rigidbody.simulated = false;
                fragment.Rigidbody.bodyType = RigidbodyType2D.Kinematic;
                fragment.Rigidbody.velocity = Vector2.zero;
                fragment.Rigidbody.angularVelocity = 0f;
            }

            if (fragment.Collider != null)
                fragment.Collider.enabled = false;

            if (fragment.Renderer != null && fragment.Renderer.sharedMaterial != null && fragment.Renderer.sharedMaterial.HasProperty(DissolveAmountId))
                fragment.Renderer.sharedMaterial.SetFloat(DissolveAmountId, 0f);
        }

        for (int i = 0; i < sourceStates.Count; i++)
        {
            SourceState state = sourceStates[i];
            if (state.Renderer == null)
                continue;

            state.Renderer.enabled = true;
            state.Renderer.sharedMaterials = originalMaterials[state.Renderer];
            state.Renderer.SetPropertyBlock(null);
        }
    }

    public IEnumerator PlayStagedDestruction(float duration)
    {
        PrepareIfNeeded();
        if (!prepared)
            yield break;

        ResetState();

        float elapsed = 0f;
        float safeDuration = Mathf.Max(0.01f, duration);
        while (elapsed < safeDuration)
        {
            elapsed += Time.deltaTime;
            SetProgress(elapsed / safeDuration);
            yield return null;
        }

        SetProgress(1f);

        float tailDuration = fragmentDissolveDelay + fragmentDissolveDuration;
        float tailElapsed = 0f;
        while (tailElapsed < tailDuration)
        {
            UpdateFragmentDissolve();
            tailElapsed += Time.deltaTime;
            yield return null;
        }

        ForceHideAllFragments();
    }

    public void SetProgress(float progress)
    {
        if (!prepared)
            return;

        currentProgress = Mathf.Clamp01(progress);
        float frontY = Mathf.Lerp(destructionMinY, destructionMaxY, Mathf.SmoothStep(0f, 1f, currentProgress));

        ApplyMask(frontY);
        ReleaseFragments(frontY);
        UpdateFragmentDissolve();
    }

    private void ApplyMask(float frontY)
    {
        Texture2D noiseTexture = sourceDissolveController != null ? sourceDissolveController.DissolveTexture : null;
        for (int i = 0; i < sourceStates.Count; i++)
        {
            SourceState state = sourceStates[i];
            if (state.Renderer == null)
                continue;

            state.Renderer.sharedMaterial = sharedMaskMaterial;
            state.PropertyBlock.Clear();
            state.PropertyBlock.SetFloat(FrontYId, frontY);
            state.PropertyBlock.SetFloat(NoiseScaleId, maskNoiseScale);
            state.PropertyBlock.SetFloat(NoiseAmplitudeId, maskNoiseAmplitude);
            state.PropertyBlock.SetFloat(SoftnessId, maskSoftness);
            state.PropertyBlock.SetFloat(EdgeWidthId, edgeWidth);
            state.PropertyBlock.SetColor(EdgeColorId, edgeColor);
            if (state.Renderer.sprite != null)
                state.PropertyBlock.SetTexture(MainTexId, state.Renderer.sprite.texture);
            state.PropertyBlock.SetColor(ColorId, state.Renderer.color);
            if (noiseTexture != null)
                state.PropertyBlock.SetTexture(NoiseTexId, noiseTexture);
            state.Renderer.SetPropertyBlock(state.PropertyBlock);
        }
    }

    private void ReleaseFragments(float frontY)
    {
        Vector3 origin = new Vector3((sourceDissolveController != null ? sourceDissolveController.CalculateTargetBounds().center.x : transform.position.x), frontY, 0f);

        for (int i = 0; i < fragmentStates.Count; i++)
        {
            FragmentState fragment = fragmentStates[i];
            if (fragment.Released || fragment.GameObject == null)
                continue;

            if (fragment.CentroidY > frontY + releaseBandHeight)
                continue;

            fragment.Released = true;
            fragment.ReleasedAt = Time.time;
            fragment.GameObject.SetActive(true);

            if (fragment.Collider != null)
                fragment.Collider.enabled = true;

            if (fragment.Rigidbody == null)
                continue;

            fragment.Rigidbody.bodyType = RigidbodyType2D.Dynamic;
            fragment.Rigidbody.simulated = true;

            Vector2 direction = ((Vector2)fragment.Transform.position - (Vector2)origin);
            if (direction.sqrMagnitude < 0.0001f)
                direction = new Vector2(Random.Range(-0.3f, 0.3f), 1f);
            direction.Normalize();

            Vector2 impulse = direction * radialImpulse + Vector2.up * upwardImpulse;
            fragment.Rigidbody.AddForce(impulse, ForceMode2D.Impulse);
            fragment.Rigidbody.AddTorque(Random.Range(-randomTorque, randomTorque), ForceMode2D.Impulse);
        }
    }

    private void UpdateFragmentDissolve()
    {
        float safeDelay = Mathf.Max(0f, fragmentDissolveDelay);
        float safeDuration = Mathf.Max(0.01f, fragmentDissolveDuration);

        for (int i = 0; i < fragmentStates.Count; i++)
        {
            FragmentState fragment = fragmentStates[i];
            if (!fragment.Released || fragment.GameObject == null || !fragment.GameObject.activeSelf || fragment.Renderer == null)
                continue;

            Material material = fragment.Renderer.sharedMaterial;
            if (material == null || !material.HasProperty(DissolveAmountId))
                continue;

            float elapsedSinceRelease = Time.time - fragment.ReleasedAt - safeDelay;
            if (elapsedSinceRelease <= 0f)
                continue;

            float dissolve = Mathf.Clamp01(elapsedSinceRelease / safeDuration);
            material.SetFloat(DissolveAmountId, dissolve);
        }
    }

    private void ForceHideAllFragments()
    {
        for (int i = 0; i < fragmentStates.Count; i++)
        {
            FragmentState fragment = fragmentStates[i];
            if (fragment.GameObject == null)
                continue;

            if (fragment.Renderer != null && fragment.Renderer.sharedMaterial != null && fragment.Renderer.sharedMaterial.HasProperty(DissolveAmountId))
                fragment.Renderer.sharedMaterial.SetFloat(DissolveAmountId, 1f);
        }
    }

    private List<GameObject> GenerateFragmentsForRenderer(SpriteRenderer sourceRenderer)
    {
        GameObject surrogate = CreateSurrogateSource(sourceRenderer);
        if (surrogate == null)
            return new List<GameObject>();

        List<GameObject> generated;
        Shader fragmentShader = sourceDissolveController != null && sourceDissolveController.DissolveShader != null
            ? sourceDissolveController.DissolveShader
            : Shader.Find("Custom/Sprite Dissolve");
        Material fragmentMaterial = new Material(fragmentShader);
        fragmentMaterial.SetTexture("_MainTex", sourceRenderer.sprite.texture);
        if (sourceDissolveController != null && sourceDissolveController.DissolveTexture != null)
            fragmentMaterial.SetTexture("_DissolveTexture", sourceDissolveController.DissolveTexture);
        fragmentMaterial.SetColor("_Color", sourceRenderer.color);
        fragmentMaterial.SetFloat("_DissolveAmount", 0f);
        fragmentMaterial.SetVector("_DissolveTiling", new Vector4(4f, 4f, 0f, 0f));
        fragmentMaterial.SetFloat("_EdgeWidth", fragmentEdgeWidth);
        fragmentMaterial.SetColor("_EdgeColor", Color.white);
        fragmentMaterial.SetFloat("_UseRadial", 0f);
        fragmentMaterial.SetFloat("_InvertRadial", 0f);
        fragmentMaterial.SetFloat("_RadialProgress", 0f);
        fragmentMaterial.SetFloat("_RadialRadius", 1f);
        fragmentMaterial.SetFloat("_RadialSoftness", 1f);

        if (shatterType == Explodable.ShatterType.Triangle)
            generated = SpriteExploder.GenerateTriangularPieces(surrogate, extraPoints, subshatterSteps, fragmentMaterial);
        else
            generated = SpriteExploder.GenerateVoronoiPieces(surrogate, extraPoints, subshatterSteps, fragmentMaterial);

        Object.Destroy(surrogate);
        return generated;
    }

    private static GameObject CreateSurrogateSource(SpriteRenderer sourceRenderer)
    {
        GameObject surrogate = new GameObject(sourceRenderer.name + "__DestructionSource");
        surrogate.hideFlags = HideFlags.HideAndDontSave;
        surrogate.transform.position = sourceRenderer.transform.position;
        surrogate.transform.rotation = sourceRenderer.transform.rotation;
        surrogate.transform.localScale = sourceRenderer.transform.lossyScale;

        SpriteRenderer sr = surrogate.AddComponent<SpriteRenderer>();
        sr.sprite = sourceRenderer.sprite;
        sr.color = sourceRenderer.color;
        sr.flipX = sourceRenderer.flipX;
        sr.flipY = sourceRenderer.flipY;
        sr.sharedMaterial = sourceRenderer.sharedMaterial;
        sr.sortingLayerID = sourceRenderer.sortingLayerID;
        sr.sortingOrder = sourceRenderer.sortingOrder;

        Rigidbody2D rb = surrogate.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.simulated = false;
        rb.velocity = Vector2.zero;

        bool hasPhysicsShape = false;
        if (sourceRenderer.sprite != null && sourceRenderer.sprite.GetPhysicsShapeCount() > 0)
        {
            PolygonCollider2D polygon = surrogate.AddComponent<PolygonCollider2D>();
            int shapeCount = sourceRenderer.sprite.GetPhysicsShapeCount();
            polygon.pathCount = shapeCount;
            List<Vector2> points = new List<Vector2>();
            for (int i = 0; i < shapeCount; i++)
            {
                points.Clear();
                sourceRenderer.sprite.GetPhysicsShape(i, points);
                polygon.SetPath(i, points);
            }
            hasPhysicsShape = true;
        }

        if (!hasPhysicsShape)
        {
            BoxCollider2D box = surrogate.AddComponent<BoxCollider2D>();
            if (sourceRenderer.sprite != null)
            {
                Bounds bounds = sourceRenderer.sprite.bounds;
                box.size = bounds.size;
                box.offset = bounds.center;
            }
        }

        return surrogate;
    }

    private SpriteRenderer[] GetSourceRenderers()
    {
        if (sourceDissolveController == null)
            return GetComponentsInChildren<SpriteRenderer>(true);

        sourceDissolveController.CollectTargets();
        SpriteRenderer[] targetRenderers = sourceDissolveController.TargetRenderers;
        if (targetRenderers != null && targetRenderers.Length > 0)
        {
            List<SpriteRenderer> filtered = new List<SpriteRenderer>(targetRenderers.Length);
            for (int i = 0; i < targetRenderers.Length; i++)
            {
                SpriteRenderer renderer = targetRenderers[i];
                if (renderer == null)
                    continue;

                if (fragmentsRoot != null && renderer.transform.IsChildOf(fragmentsRoot))
                    continue;

                filtered.Add(renderer);
            }

            return filtered.ToArray();
        }

        return GetComponentsInChildren<SpriteRenderer>(true);
    }

    private void RestoreOriginalRenderers()
    {
        for (int i = 0; i < sourceStates.Count; i++)
        {
            SourceState state = sourceStates[i];
            if (state.Renderer == null)
                continue;

            if (originalMaterials.TryGetValue(state.Renderer, out Material[] materials) && materials != null)
                state.Renderer.sharedMaterials = materials;
            state.Renderer.SetPropertyBlock(null);
            state.Renderer.enabled = true;
        }
    }

    private void CleanupFragments()
    {
        if (fragmentsRoot != null)
            Object.Destroy(fragmentsRoot.gameObject);
        fragmentsRoot = null;
    }

    private void ResolveDefaults()
    {
        if (sourceDissolveController == null)
            sourceDissolveController = GetComponent<SpriteDissolveController>();

        if (verticalMaskShader == null)
            verticalMaskShader = Shader.Find("Custom/Boss Vertical Destruction");
    }

    private sealed class SourceState
    {
        public SourceState(SpriteRenderer renderer, MaterialPropertyBlock propertyBlock)
        {
            Renderer = renderer;
            PropertyBlock = propertyBlock;
        }

        public SpriteRenderer Renderer { get; }
        public MaterialPropertyBlock PropertyBlock { get; }
    }

    private sealed class FragmentState
    {
        public FragmentState(
            GameObject gameObject,
            MeshRenderer renderer,
            Rigidbody2D rigidbody,
            Collider2D collider,
            Vector3 initialPosition,
            Quaternion initialRotation,
            Vector3 initialScale,
            float centroidY)
        {
            GameObject = gameObject;
            Transform = gameObject.transform;
            Renderer = renderer;
            Rigidbody = rigidbody;
            Collider = collider;
            InitialPosition = initialPosition;
            InitialRotation = initialRotation;
            InitialScale = initialScale;
            CentroidY = centroidY;
        }

        public GameObject GameObject { get; }
        public Transform Transform { get; }
        public MeshRenderer Renderer { get; }
        public Rigidbody2D Rigidbody { get; }
        public Collider2D Collider { get; }
        public Vector3 InitialPosition { get; }
        public Quaternion InitialRotation { get; }
        public Vector3 InitialScale { get; }
        public float CentroidY { get; }
        public bool Released { get; set; }
        public float ReleasedAt { get; set; }
    }
}
