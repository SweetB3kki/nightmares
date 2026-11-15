using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
public sealed class UnderCollapseDestructionController : MonoBehaviour
{
    [Header("Scene Object Names")]
    [SerializeField] private string destructibleObjectName = "PL_UNDER_Разрушаемый";
    [SerializeField] private string walkHitboxObjectName = "хитбокс_разрушаемый";
    [SerializeField] private string playerTag = "Player";

    [Header("Fragmentation")]
    [SerializeField] private Explodable.ShatterType shatterType = Explodable.ShatterType.Voronoi;
    [SerializeField] private int extraPoints = 18;
    [SerializeField] private int subshatterSteps = 0;
    [SerializeField] private int fragmentSortingOrderOffset = 25;

    [Header("Motion")]
    [SerializeField] private float topFragmentInitialDownwardSpeed = 4.5f;
    [SerializeField] private float bottomFragmentInitialDownwardSpeed = 12f;
    [SerializeField] private float horizontalSeparationSpeed = 0.55f;
    [SerializeField] private float randomHorizontalSpeed = 0.25f;
    [SerializeField] private float randomExtraDownwardSpeed = 1.4f;
    [SerializeField] private float gravityScale = 3.2f;
    [SerializeField] private float linearDrag = 0f;
    [SerializeField] private float angularDrag = 0.01f;
    [SerializeField] private float maxAngularVelocity = 110f;
    [SerializeField] private float fallAccelerationDelay = 0.25f;
    [SerializeField] private float topFragmentExtraDownwardSpeed = 3f;
    [SerializeField] private float bottomFragmentExtraDownwardSpeed = 8.5f;
    [SerializeField] private float acceleratedGravityScale = 5.5f;
    [SerializeField, Range(0.05f, 0.5f)] private float topCollidableBand = 0.16f;

    [Header("Cleanup")]
    [SerializeField] private float fragmentDisableDelay = 3f;
    [SerializeField] private float fragmentDissolveDuration = 0.55f;
    [SerializeField] private float fragmentDissolveEdgeWidth = 0.05f;
    [SerializeField] private string fragmentDissolveTextureName = "DissolveMaskSampleImage";

    private static readonly int DissolveAmountId = Shader.PropertyToID("_DissolveAmount");
    private static readonly int DissolveTextureId = Shader.PropertyToID("_DissolveTexture");

    private Collider2D triggerCollider;
    private GameObject destructibleObject;
    private SpriteRenderer destructibleRenderer;
    private PolygonCollider2D destructiblePolygon;
    private Collider2D walkHitboxCollider;
    private readonly List<FragmentState> fragments = new List<FragmentState>();
    private Transform fragmentsRoot;
    private bool triggered;

    public bool IsCollapsed => triggered;

    private void Awake()
    {
        triggerCollider = GetComponent<Collider2D>();
        ResolveReferences();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (triggered || other == null || !other.CompareTag(playerTag))
            return;

        ResolveReferences();
        if (destructibleObject == null || destructibleRenderer == null || destructiblePolygon == null)
        {
            Debug.LogWarning("UnderCollapseDestructionController: destructible object references were not resolved.");
            return;
        }

        triggered = true;
        if (triggerCollider != null)
            triggerCollider.enabled = false;

        if (walkHitboxCollider != null)
            walkHitboxCollider.enabled = false;

        SaveSystem.MarkUnderCollapseTriggered(this);

        StartCollapse();
    }

    public void ApplyCollapsedStateImmediate()
    {
        ResolveReferences();
        triggered = true;

        if (triggerCollider != null)
            triggerCollider.enabled = false;

        if (walkHitboxCollider != null)
            walkHitboxCollider.enabled = false;

        CleanupFragments();

        if (destructibleRenderer != null)
            destructibleRenderer.enabled = false;

        if (destructibleObject != null)
            destructibleObject.SetActive(false);
    }

    private void StartCollapse()
    {
        CleanupFragments();

        GameObject surrogate = CreateSurrogateSource();
        if (surrogate == null)
            return;

        Material fragmentMaterial = CreateFragmentMaterial();
        List<GameObject> generatedPieces = shatterType == Explodable.ShatterType.Triangle
            ? SpriteExploder.GenerateTriangularPieces(surrogate, extraPoints, subshatterSteps, fragmentMaterial)
            : SpriteExploder.GenerateVoronoiPieces(surrogate, extraPoints, subshatterSteps, fragmentMaterial);

        Object.Destroy(surrogate);

        if (generatedPieces == null || generatedPieces.Count == 0)
        {
            Debug.LogWarning("UnderCollapseDestructionController: no fragments were generated.");
            return;
        }

        fragmentsRoot = new GameObject("__UnderCollapseFragments").transform;
        fragmentsRoot.position = Vector3.zero;
        fragmentsRoot.rotation = Quaternion.identity;
        fragmentsRoot.localScale = Vector3.one;

        int sortingLayerId = destructibleRenderer.sortingLayerID;
        string sortingLayerName = destructibleRenderer.sortingLayerName;
        int sortingOrder = destructibleRenderer.sortingOrder;
        int layer = destructibleObject.layer;
        int addedFragments = 0;
        Bounds fragmentBounds = CalculateGeneratedFragmentBounds(generatedPieces);
        float minY = fragmentBounds.min.y;
        float maxY = fragmentBounds.max.y;
        float centerX = fragmentBounds.center.x;
        float width = Mathf.Max(0.01f, fragmentBounds.size.x);

        for (int i = 0; i < generatedPieces.Count; i++)
        {
            GameObject piece = generatedPieces[i];
            if (piece == null)
                continue;

            MeshRenderer meshRenderer = piece.GetComponent<MeshRenderer>();
            Rigidbody2D rb = piece.GetComponent<Rigidbody2D>();
            if (meshRenderer == null || rb == null)
            {
                Object.Destroy(piece);
                continue;
            }

            piece.transform.SetParent(fragmentsRoot, true);
            piece.layer = layer;
            piece.SetActive(true);

            meshRenderer.enabled = true;
            meshRenderer.sortingLayerID = sortingLayerId;
            meshRenderer.sortingLayerName = sortingLayerName;
            meshRenderer.sortingOrder = sortingOrder + fragmentSortingOrderOffset;
            if (fragmentMaterial != null)
                meshRenderer.sharedMaterial = new Material(fragmentMaterial);

            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.simulated = true;
            rb.gravityScale = gravityScale;
            rb.drag = linearDrag;
            rb.angularDrag = angularDrag;
            rb.velocity = Vector2.zero;
            rb.angularVelocity = Random.Range(-maxAngularVelocity, maxAngularVelocity);

            Vector3 piecePosition = piece.transform.position;
            float vertical01 = Mathf.InverseLerp(maxY, minY, piecePosition.y);
            float horizontalDirection = Mathf.Sign(piecePosition.x - centerX);
            if (Mathf.Approximately(horizontalDirection, 0f))
                horizontalDirection = Random.value < 0.5f ? -1f : 1f;

            float horizontal01 = Mathf.Clamp01(Mathf.Abs(piecePosition.x - centerX) / (width * 0.5f));
            float horizontalImpulse = (horizontalSeparationSpeed * Mathf.Lerp(0.35f, 1f, horizontal01) * horizontalDirection)
                + Random.Range(-randomHorizontalSpeed, randomHorizontalSpeed);
            float downwardSpeed = Mathf.Lerp(topFragmentInitialDownwardSpeed, bottomFragmentInitialDownwardSpeed, vertical01)
                + Random.Range(0f, randomExtraDownwardSpeed);
            bool keepColliderSolid = piecePosition.y >= Mathf.Lerp(maxY, minY, topCollidableBand);

            Collider2D collider = piece.GetComponent<Collider2D>();
            if (collider != null)
            {
                collider.enabled = keepColliderSolid;
                collider.isTrigger = !keepColliderSolid;
            }

            rb.velocity = new Vector2(horizontalImpulse, -downwardSpeed);

            fragments.Add(new FragmentState(piece, rb));
            addedFragments++;
        }

        if (addedFragments == 0)
        {
            CleanupFragments();
            Debug.LogWarning("UnderCollapseDestructionController: generated fragments were unusable.");
            return;
        }

        destructibleRenderer.enabled = false;
        StartCoroutine(AccelerateFragmentFall());
        StartCoroutine(DisableFragmentsAfterDelay());
    }

    private IEnumerator AccelerateFragmentFall()
    {
        float delay = Mathf.Max(0.01f, fallAccelerationDelay);
        yield return new WaitForSeconds(delay);

        if (fragments.Count == 0)
            yield break;

        float minY = float.MaxValue;
        float maxY = float.MinValue;
        for (int i = 0; i < fragments.Count; i++)
        {
            FragmentState fragment = fragments[i];
            if (fragment == null || fragment.GameObject == null)
                continue;

            float y = fragment.GameObject.transform.position.y;
            if (y < minY)
                minY = y;
            if (y > maxY)
                maxY = y;
        }

        if (minY == float.MaxValue || maxY == float.MinValue)
            yield break;

        for (int i = 0; i < fragments.Count; i++)
        {
            FragmentState fragment = fragments[i];
            if (fragment == null || fragment.GameObject == null || fragment.Rigidbody == null)
                continue;

            Rigidbody2D rb = fragment.Rigidbody;
            if (!rb.simulated)
                continue;

            float vertical01 = Mathf.InverseLerp(maxY, minY, fragment.GameObject.transform.position.y);
            float extraDownwardImpulse = Mathf.Lerp(topFragmentExtraDownwardSpeed, bottomFragmentExtraDownwardSpeed, vertical01);

            rb.gravityScale = acceleratedGravityScale;
            Vector2 velocity = rb.velocity;
            velocity.y -= extraDownwardImpulse;
            rb.velocity = velocity;
        }
    }

    private IEnumerator DisableFragmentsAfterDelay()
    {
        float delay = Mathf.Max(0.01f, fragmentDisableDelay);
        yield return new WaitForSeconds(delay);

        float dissolveDuration = Mathf.Max(0.01f, fragmentDissolveDuration);
        for (int i = 0; i < fragments.Count; i++)
        {
            FragmentState fragment = fragments[i];
            if (fragment == null || fragment.GameObject == null)
                continue;

            if (fragment.Rigidbody != null)
            {
                fragment.Rigidbody.velocity = Vector2.zero;
                fragment.Rigidbody.angularVelocity = 0f;
                fragment.Rigidbody.simulated = false;
            }

            if (fragment.Collider != null)
            {
                fragment.Collider.enabled = false;
                fragment.Collider.isTrigger = true;
            }
        }

        float elapsed = 0f;
        while (elapsed < dissolveDuration)
        {
            float dissolve = Mathf.Clamp01(elapsed / dissolveDuration);
            for (int i = 0; i < fragments.Count; i++)
            {
                FragmentState fragment = fragments[i];
                if (fragment == null || fragment.Renderer == null)
                    continue;

                Material material = fragment.Renderer.sharedMaterial;
                if (material != null && material.HasProperty(DissolveAmountId))
                    material.SetFloat(DissolveAmountId, dissolve);
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        for (int i = 0; i < fragments.Count; i++)
        {
            FragmentState fragment = fragments[i];
            if (fragment == null || fragment.GameObject == null)
                continue;

            if (fragment.Renderer != null)
            {
                Material material = fragment.Renderer.sharedMaterial;
                if (material != null && material.HasProperty(DissolveAmountId))
                    material.SetFloat(DissolveAmountId, 1f);
            }

            fragment.GameObject.SetActive(false);
        }
    }

    private GameObject CreateSurrogateSource()
    {
        if (destructibleRenderer == null || destructibleRenderer.sprite == null || destructiblePolygon == null)
            return null;

        Sprite surrogateSprite = CreateCroppedSpriteForCollider(destructibleRenderer.sprite, destructiblePolygon);
        if (surrogateSprite == null)
            surrogateSprite = destructibleRenderer.sprite;

        GameObject surrogate = new GameObject(destructibleObject.name + "__CollapseSource");
        surrogate.hideFlags = HideFlags.HideAndDontSave;
        surrogate.transform.position = destructibleObject.transform.position;
        surrogate.transform.rotation = destructibleObject.transform.rotation;
        surrogate.transform.localScale = destructibleObject.transform.lossyScale;

        SpriteRenderer sr = surrogate.AddComponent<SpriteRenderer>();
        sr.sprite = surrogateSprite;
        sr.color = destructibleRenderer.color;
        sr.flipX = destructibleRenderer.flipX;
        sr.flipY = destructibleRenderer.flipY;
        sr.sharedMaterial = destructibleRenderer.sharedMaterial;
        sr.sortingLayerID = destructibleRenderer.sortingLayerID;
        sr.sortingOrder = destructibleRenderer.sortingOrder;

        Rigidbody2D rb = surrogate.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.simulated = false;
        rb.velocity = Vector2.zero;

        PolygonCollider2D polygon = surrogate.AddComponent<PolygonCollider2D>();
        polygon.pathCount = destructiblePolygon.pathCount;
        for (int i = 0; i < destructiblePolygon.pathCount; i++)
            polygon.SetPath(i, destructiblePolygon.GetPath(i));

        return surrogate;
    }

    private static Sprite CreateCroppedSpriteForCollider(Sprite sourceSprite, PolygonCollider2D sourceCollider)
    {
        if (sourceSprite == null || sourceCollider == null)
            return null;

        Bounds localBounds = CalculateColliderLocalBounds(sourceCollider);
        if (localBounds.size.x <= 0.0001f || localBounds.size.y <= 0.0001f)
            return null;

        Rect sourceRect = sourceSprite.rect;
        float ppu = Mathf.Max(0.0001f, sourceSprite.pixelsPerUnit);
        Vector2 sourcePivot = sourceSprite.pivot;

        Vector2 minPixels = sourcePivot + (Vector2)localBounds.min * ppu;
        Vector2 maxPixels = sourcePivot + (Vector2)localBounds.max * ppu;

        float rectMinX = Mathf.Clamp(Mathf.Floor(minPixels.x), 0f, sourceRect.width - 1f);
        float rectMinY = Mathf.Clamp(Mathf.Floor(minPixels.y), 0f, sourceRect.height - 1f);
        float rectMaxX = Mathf.Clamp(Mathf.Ceil(maxPixels.x), rectMinX + 1f, sourceRect.width);
        float rectMaxY = Mathf.Clamp(Mathf.Ceil(maxPixels.y), rectMinY + 1f, sourceRect.height);

        Rect cropRect = new Rect(
            sourceRect.x + rectMinX,
            sourceRect.y + rectMinY,
            rectMaxX - rectMinX,
            rectMaxY - rectMinY);

        Vector2 newPivotPixels = new Vector2(
            sourcePivot.x - rectMinX,
            sourcePivot.y - rectMinY);

        return Sprite.Create(
            sourceSprite.texture,
            cropRect,
            new Vector2(newPivotPixels.x / cropRect.width, newPivotPixels.y / cropRect.height),
            ppu,
            0,
            SpriteMeshType.FullRect);
    }

    private static Bounds CalculateColliderLocalBounds(PolygonCollider2D sourceCollider)
    {
        bool initialized = false;
        Bounds bounds = default;

        for (int pathIndex = 0; pathIndex < sourceCollider.pathCount; pathIndex++)
        {
            Vector2[] points = sourceCollider.GetPath(pathIndex);
            for (int i = 0; i < points.Length; i++)
            {
                Vector3 point = points[i];
                if (!initialized)
                {
                    bounds = new Bounds(point, Vector3.zero);
                    initialized = true;
                }
                else
                {
                    bounds.Encapsulate(point);
                }
            }
        }

        return bounds;
    }

    private static Bounds CalculateGeneratedFragmentBounds(List<GameObject> generatedPieces)
    {
        bool initialized = false;
        Bounds bounds = default;

        for (int i = 0; i < generatedPieces.Count; i++)
        {
            GameObject piece = generatedPieces[i];
            if (piece == null)
                continue;

            Vector3 position = piece.transform.position;
            if (!initialized)
            {
                bounds = new Bounds(position, Vector3.zero);
                initialized = true;
            }
            else
            {
                bounds.Encapsulate(position);
            }
        }

        return initialized ? bounds : new Bounds(Vector3.zero, Vector3.one);
    }

    private Material CreateFragmentMaterial()
    {
        if (destructibleRenderer == null || destructibleRenderer.sprite == null)
            return null;

        Shader fragmentShader = Shader.Find("Custom/Sprite Dissolve");
        if (fragmentShader == null)
            fragmentShader = Shader.Find("Sprites/Default");
        if (fragmentShader == null)
            return null;

        Material material = new Material(fragmentShader);
        material.name = "UnderCollapseFragment(Runtime)";
        material.SetTexture("_MainTex", destructibleRenderer.sprite.texture);

        if (material.HasProperty("_Color"))
            material.SetColor("_Color", destructibleRenderer.color);
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", destructibleRenderer.color);
        if (material.HasProperty(DissolveTextureId))
            material.SetTexture(DissolveTextureId, ResolveDissolveTexture());
        if (material.HasProperty("_DissolveAmount"))
            material.SetFloat("_DissolveAmount", 0f);
        if (material.HasProperty("_DissolveTiling"))
            material.SetVector("_DissolveTiling", new Vector4(4f, 4f, 0f, 0f));
        if (material.HasProperty("_EdgeWidth"))
            material.SetFloat("_EdgeWidth", fragmentDissolveEdgeWidth);
        if (material.HasProperty("_EdgeColor"))
            material.SetColor("_EdgeColor", Color.white);
        if (material.HasProperty("_UseRadial"))
            material.SetFloat("_UseRadial", 0f);
        if (material.HasProperty("_InvertRadial"))
            material.SetFloat("_InvertRadial", 0f);
        if (material.HasProperty("_Cull"))
            material.SetInt("_Cull", 0);

        return material;
    }

    private Texture2D ResolveDissolveTexture()
    {
        Texture2D namedTexture = FindTextureByName(fragmentDissolveTextureName);
        if (namedTexture != null)
            return namedTexture;

        SpriteDissolveController[] controllers = Resources.FindObjectsOfTypeAll<SpriteDissolveController>();
        for (int i = 0; i < controllers.Length; i++)
        {
            SpriteDissolveController controller = controllers[i];
            if (controller == null || controller.DissolveTexture == null)
                continue;

            return controller.DissolveTexture;
        }

        return Texture2D.whiteTexture;
    }

    private Texture2D FindTextureByName(string textureName)
    {
        if (string.IsNullOrWhiteSpace(textureName))
            return null;

#if UNITY_EDITOR
        string[] guids = AssetDatabase.FindAssets(textureName + " t:Texture2D");
        for (int i = 0; i < guids.Length; i++)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (texture != null && string.Equals(texture.name, textureName, System.StringComparison.OrdinalIgnoreCase))
                return texture;
        }
#endif

        Texture2D[] textures = Resources.FindObjectsOfTypeAll<Texture2D>();
        for (int i = 0; i < textures.Length; i++)
        {
            Texture2D texture = textures[i];
            if (texture == null)
                continue;

            if (string.Equals(texture.name, textureName, System.StringComparison.OrdinalIgnoreCase))
                return texture;
        }

        return null;
    }

    private void ResolveReferences()
    {
        if (destructibleObject == null)
        {
            destructibleObject = FindInScene(gameObject.scene, destructibleObjectName);
            if (destructibleObject != null)
            {
                destructibleRenderer = destructibleObject.GetComponent<SpriteRenderer>();
                destructiblePolygon = destructibleObject.GetComponent<PolygonCollider2D>();
            }
        }

        if (walkHitboxCollider == null)
        {
            GameObject walkHitbox = FindInScene(gameObject.scene, walkHitboxObjectName);
            if (walkHitbox != null)
                walkHitboxCollider = walkHitbox.GetComponent<Collider2D>();
        }
    }

    private void CleanupFragments()
    {
        if (fragmentsRoot != null)
            Object.Destroy(fragmentsRoot.gameObject);

        fragmentsRoot = null;
        fragments.Clear();
    }

    private static GameObject FindInScene(Scene scene, string targetName)
    {
        if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrEmpty(targetName))
            return null;

        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            GameObject found = FindInHierarchy(roots[i].transform, targetName);
            if (found != null)
                return found;
        }

        return null;
    }

    private static GameObject FindInHierarchy(Transform root, string targetName)
    {
        if (root == null)
            return null;

        if (string.Equals(root.name, targetName, System.StringComparison.OrdinalIgnoreCase))
            return root.gameObject;

        for (int i = 0; i < root.childCount; i++)
        {
            GameObject found = FindInHierarchy(root.GetChild(i), targetName);
            if (found != null)
                return found;
        }

        return null;
    }

    private sealed class FragmentState
    {
        public FragmentState(GameObject gameObject, Rigidbody2D rigidbody)
        {
            GameObject = gameObject;
            Rigidbody = rigidbody;
            Collider = gameObject != null ? gameObject.GetComponent<Collider2D>() : null;
            Renderer = gameObject != null ? gameObject.GetComponent<MeshRenderer>() : null;
        }

        public GameObject GameObject { get; }
        public Rigidbody2D Rigidbody { get; }
        public Collider2D Collider { get; }
        public MeshRenderer Renderer { get; }
    }
}

public static class UnderCollapseDestructionBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "SampleScene")
            return;

        GameObject triggerObject = GameObject.Find("TrigerUnderCollapse");
        if (triggerObject == null)
            return;

        if (triggerObject.GetComponent<UnderCollapseDestructionController>() == null)
            triggerObject.AddComponent<UnderCollapseDestructionController>();
    }
}
