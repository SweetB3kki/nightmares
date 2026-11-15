using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
[DisallowMultipleComponent]
public class SpriteDissolveController : MonoBehaviour
{
    [Header("Targets")]
    [SerializeField] private bool autoCollectChildren = true;
    [SerializeField] private SpriteRenderer[] targetRenderers;
    [SerializeField] private Shader dissolveShader;
    [SerializeField] private Texture2D dissolveTexture;

    [Header("Dissolve")]
    [Range(0f, 1f)]
    [SerializeField] private float dissolveAmount = 0f;
    [SerializeField] private Vector2 dissolveTiling = new Vector2(4f, 4f);
    [SerializeField] private Vector2 dissolveOffset = Vector2.zero;
    [Range(0.001f, 0.3f)]
    [SerializeField] private float edgeWidth = 0.06f;
    [SerializeField] private Color edgeColor = Color.white;

    [Header("Radial")]
    [SerializeField] private bool useRadialMask = false;
    [SerializeField] private bool invertRadialMask = false;
    [Range(0f, 1f)]
    [SerializeField] private float radialProgress = 0f;
    [SerializeField] private float radialRadius = 5f;
    [SerializeField] private float radialSoftness = 0.35f;
    [SerializeField] private Transform radialCenterTarget;
    [SerializeField] private Vector3 radialCenterOffset = Vector3.zero;

    [Header("Test")]
    [SerializeField] private bool animateInPlayMode = false;
    [SerializeField] private float animateDuration = 2f;
    [SerializeField] private bool pingPong = true;

    private readonly Dictionary<SpriteRenderer, Material> runtimeMaterials = new Dictionary<SpriteRenderer, Material>();
    private readonly Dictionary<SpriteRenderer, Material> originalMaterials = new Dictionary<SpriteRenderer, Material>();
    private static readonly int DissolveAmountId = Shader.PropertyToID("_DissolveAmount");
    private static readonly int DissolveTilingId = Shader.PropertyToID("_DissolveTiling");
    private static readonly int EdgeWidthId = Shader.PropertyToID("_EdgeWidth");
    private static readonly int EdgeColorId = Shader.PropertyToID("_EdgeColor");
    private static readonly int DissolveTextureId = Shader.PropertyToID("_DissolveTexture");
    private static readonly int UseRadialId = Shader.PropertyToID("_UseRadial");
    private static readonly int InvertRadialId = Shader.PropertyToID("_InvertRadial");
    private static readonly int RadialProgressId = Shader.PropertyToID("_RadialProgress");
    private static readonly int RadialRadiusId = Shader.PropertyToID("_RadialRadius");
    private static readonly int RadialSoftnessId = Shader.PropertyToID("_RadialSoftness");
    private static readonly int WorldCenterId = Shader.PropertyToID("_WorldCenter");
    private static readonly int MainTexId = Shader.PropertyToID("_MainTex");

    public float DissolveAmount
    {
        get => dissolveAmount;
        set
        {
            dissolveAmount = Mathf.Clamp01(value);
            ApplyProperties();
        }
    }

    public Texture2D DissolveTexture => dissolveTexture;

    public SpriteRenderer[] TargetRenderers => targetRenderers;

    public Shader DissolveShader => dissolveShader;

    public bool UseRadialMask
    {
        get => useRadialMask;
        set
        {
            useRadialMask = value;
            ApplyProperties();
        }
    }

    public bool InvertRadialMask
    {
        get => invertRadialMask;
        set
        {
            invertRadialMask = value;
            ApplyProperties();
        }
    }

    public float RadialProgress
    {
        get => radialProgress;
        set
        {
            radialProgress = Mathf.Clamp01(value);
            ApplyProperties();
        }
    }

    public float RadialRadius
    {
        get => radialRadius;
        set
        {
            radialRadius = Mathf.Max(0.01f, value);
            ApplyProperties();
        }
    }

    public float RadialSoftness
    {
        get => radialSoftness;
        set
        {
            radialSoftness = Mathf.Max(0.001f, value);
            ApplyProperties();
        }
    }

    public Transform RadialCenterTarget
    {
        get => radialCenterTarget;
        set
        {
            radialCenterTarget = value;
            ApplyProperties();
        }
    }

    public Vector3 RadialCenterOffset
    {
        get => radialCenterOffset;
        set
        {
            radialCenterOffset = value;
            ApplyProperties();
        }
    }

    private void OnEnable()
    {
        ResolveShader();
        CollectTargets();
        CreateRuntimeMaterials();
        ApplyProperties();
    }

    private void Update()
    {
        if (animateInPlayMode && Application.isPlaying)
        {
            float duration = Mathf.Max(0.01f, animateDuration);
            float t = Time.time / duration;
            dissolveAmount = pingPong ? Mathf.PingPong(t, 1f) : Mathf.Clamp01(t);
        }

        ApplyProperties();
    }

    private void OnDisable()
    {
        RestoreOriginalMaterials();
    }

    private void OnValidate()
    {
        dissolveAmount = Mathf.Clamp01(dissolveAmount);
        dissolveTiling.x = Mathf.Max(0.01f, dissolveTiling.x);
        dissolveTiling.y = Mathf.Max(0.01f, dissolveTiling.y);
        edgeWidth = Mathf.Clamp(edgeWidth, 0.001f, 0.3f);
        radialRadius = Mathf.Max(0.01f, radialRadius);
        radialSoftness = Mathf.Max(0.001f, radialSoftness);
        animateDuration = Mathf.Max(0.01f, animateDuration);

        if (!isActiveAndEnabled)
            return;

        ResolveShader();
        CollectTargets();
        CreateRuntimeMaterials();
        ApplyProperties();
    }

    [ContextMenu("Collect Targets")]
    public void CollectTargets()
    {
        if (!autoCollectChildren)
            return;

        targetRenderers = GetComponentsInChildren<SpriteRenderer>(true);
    }

    [ContextMenu("Set Visible")]
    public void SetVisible()
    {
        dissolveAmount = 0f;
        ApplyProperties();
    }

    [ContextMenu("Set Hidden")]
    public void SetHidden()
    {
        dissolveAmount = 1f;
        ApplyProperties();
    }

    public Bounds CalculateTargetBounds()
    {
        bool hasBounds = false;
        Bounds combinedBounds = default;

        if (targetRenderers == null)
            return combinedBounds;

        for (int i = 0; i < targetRenderers.Length; i++)
        {
            SpriteRenderer renderer = targetRenderers[i];
            if (renderer == null)
                continue;

            if (!hasBounds)
            {
                combinedBounds = renderer.bounds;
                hasBounds = true;
                continue;
            }

            combinedBounds.Encapsulate(renderer.bounds);
        }

        return combinedBounds;
    }

    private void ResolveShader()
    {
        if (dissolveShader != null)
        {
            ResolveTexture();
            return;
        }

        dissolveShader = Shader.Find("Custom/Sprite Dissolve");
        ResolveTexture();
    }

    private void ResolveTexture()
    {
        if (dissolveTexture != null)
            return;

#if UNITY_EDITOR
        dissolveTexture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/DissolveSprite.png");
#endif
    }

    private void CreateRuntimeMaterials()
    {
        if (dissolveShader == null || targetRenderers == null)
            return;

        for (int i = 0; i < targetRenderers.Length; i++)
        {
            SpriteRenderer renderer = targetRenderers[i];
            if (renderer == null)
                continue;

            if (!originalMaterials.ContainsKey(renderer))
                originalMaterials[renderer] = renderer.sharedMaterial;

            if (runtimeMaterials.TryGetValue(renderer, out Material existing) && existing != null)
                continue;

            Material runtimeMaterial = new Material(dissolveShader)
            {
                name = renderer.gameObject.name + "_DissolveInstance"
            };

            runtimeMaterials[renderer] = runtimeMaterial;
            renderer.sharedMaterial = runtimeMaterial;
        }
    }

    private void ApplyProperties()
    {
        if (runtimeMaterials.Count == 0)
            return;

        Vector3 worldCenter = radialCenterTarget != null
            ? radialCenterTarget.position + radialCenterOffset
            : transform.position + radialCenterOffset;

        foreach (KeyValuePair<SpriteRenderer, Material> pair in runtimeMaterials)
        {
            SpriteRenderer renderer = pair.Key;
            Material material = pair.Value;
            if (renderer == null || material == null)
                continue;

            if (renderer.sprite != null && material.HasProperty(MainTexId))
                material.SetTexture(MainTexId, renderer.sprite.texture);

            if (dissolveTexture != null && material.HasProperty(DissolveTextureId))
                material.SetTexture(DissolveTextureId, dissolveTexture);

            if (material.HasProperty(DissolveAmountId))
                material.SetFloat(DissolveAmountId, dissolveAmount);

            if (material.HasProperty(DissolveTilingId))
                material.SetVector(DissolveTilingId, new Vector4(dissolveTiling.x, dissolveTiling.y, dissolveOffset.x, dissolveOffset.y));

            if (material.HasProperty(EdgeWidthId))
                material.SetFloat(EdgeWidthId, edgeWidth);

            if (material.HasProperty(EdgeColorId))
                material.SetColor(EdgeColorId, edgeColor);

            if (material.HasProperty(UseRadialId))
                material.SetFloat(UseRadialId, useRadialMask ? 1f : 0f);

            if (material.HasProperty(InvertRadialId))
                material.SetFloat(InvertRadialId, invertRadialMask ? 1f : 0f);

            if (material.HasProperty(RadialProgressId))
                material.SetFloat(RadialProgressId, radialProgress);

            if (material.HasProperty(RadialRadiusId))
                material.SetFloat(RadialRadiusId, radialRadius);

            if (material.HasProperty(RadialSoftnessId))
                material.SetFloat(RadialSoftnessId, radialSoftness);

            if (material.HasProperty(WorldCenterId))
                material.SetVector(WorldCenterId, worldCenter);
        }
    }

    private void RestoreOriginalMaterials()
    {
        foreach (KeyValuePair<SpriteRenderer, Material> pair in runtimeMaterials)
        {
            SpriteRenderer renderer = pair.Key;
            Material runtimeMaterial = pair.Value;
            if (renderer != null && originalMaterials.TryGetValue(renderer, out Material original))
                renderer.sharedMaterial = original;

            if (runtimeMaterial != null)
            {
                if (Application.isPlaying)
                    Destroy(runtimeMaterial);
                else
                    DestroyImmediate(runtimeMaterial);
            }
        }

        runtimeMaterials.Clear();
        originalMaterials.Clear();
    }
}

