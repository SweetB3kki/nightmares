using System.Collections;
using UnityEngine;

public class SparkCollector : MonoBehaviour
{
    public static event System.Action<SparkCollector> SparkCollected;
    private const string LabyrinthSceneName = "labirint";

    private bool pickedUp = false;
    private Animator anim;
    private bool playedSpawnAnimation = false;
    private float pickupAllowedAt = -1f;
    private Collider2D cachedCollider;
    private Vector3 initialLocalScale;

    [Header("UI animation")]
    public Animator uiSparkAnimator;

    [Header("Appear on activate")]
    public bool playAppearOnActivate = false;
    public bool useClockAppearEffect = true;
    public float clockAppearDuration = 0.35f;

    public bool IsPickedUp => pickedUp;

    private void Start()
    {
        anim = GetComponent<Animator>();
        cachedCollider = GetComponent<Collider2D>();
        initialLocalScale = transform.localScale;
        EnsureRuntimeBindings();
        ApplyLabyrinthRuntimeState();
    }

    private void OnEnable()
    {
        EnsureRuntimeBindings();
        pickupAllowedAt = -1f;

        if (pickedUp)
        {
            pickedUp = false;
            playedSpawnAnimation = false;
        }

        if (!playAppearOnActivate)
        {
            ApplyLabyrinthRuntimeState();
            return;
        }

        if (playedSpawnAnimation)
            return;

        playedSpawnAnimation = true;

        // Prevent instant pickup on activation frame.
        pickupAllowedAt = Time.time + 0.35f;

        if (cachedCollider == null)
            cachedCollider = GetComponent<Collider2D>();
        if (cachedCollider != null)
            StartCoroutine(ReEnableColliderAfterDelay(0.15f));

        if (useClockAppearEffect)
            StartCoroutine(PlayClockAppearEffect());

        ApplyLabyrinthRuntimeState();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (pickedUp || !other.CompareTag("Player"))
            return;

        if (Time.time < pickupAllowedAt)
            return;

        pickedUp = true;

        if (SparkManager.Instance != null)
        {
            SparkManager.Instance.AddSpark();
            SaveSystem.MarkSparkCollected(this);
        }
        else
        {
            Debug.LogWarning("SparkCollector: SparkManager instance not found.");
        }

        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
            col.enabled = false;

        if (anim != null)
        {
            if (!anim.enabled)
            {
                anim.enabled = true;
                anim.Rebind();
                anim.Update(0f);
            }

            anim.SetTrigger("PlayFall");
        }
        else
        {
            Destroy(gameObject);
        }

        if (uiSparkAnimator != null)
            uiSparkAnimator.SetTrigger("collect_coin");

        if (gameObject.scene.name == LabyrinthSceneName)
            GameState.MarkLabyrinthSparkCollected(SaveIdentityUtility.GetObjectId(this));

        SparkCollected?.Invoke(this);
    }

    private void EnsureRuntimeBindings()
    {
        if (anim == null)
            anim = GetComponent<Animator>();

        if (cachedCollider == null)
            cachedCollider = GetComponent<Collider2D>();

        if (initialLocalScale == Vector3.zero)
            initialLocalScale = transform.localScale;

        if (anim != null && !anim.enabled)
        {
            anim.enabled = true;
            anim.Rebind();
            anim.Update(0f);
        }

        if (cachedCollider != null && !pickedUp)
            cachedCollider.enabled = true;
    }

    public void DestroyAfterAnim()
    {
        Destroy(gameObject);
    }

    public void ResetForSceneReuse()
    {
        pickedUp = false;
        playedSpawnAnimation = false;
        pickupAllowedAt = -1f;
        transform.localScale = initialLocalScale == Vector3.zero ? transform.localScale : initialLocalScale;
        EnsureRuntimeBindings();
        ApplyLabyrinthRuntimeState();
    }

    private void ApplyLabyrinthRuntimeState()
    {
        if (gameObject.scene.name != LabyrinthSceneName)
            return;

        string sparkId = SaveIdentityUtility.GetObjectId(this);
        if (!GameState.IsLabyrinthSparkCollected(sparkId))
            return;

        pickedUp = true;

        if (cachedCollider != null)
            cachedCollider.enabled = false;

        gameObject.SetActive(false);
    }

    private IEnumerator ReEnableColliderAfterDelay(float delay)
    {
        if (cachedCollider != null)
            cachedCollider.enabled = false;

        yield return new WaitForSeconds(delay);

        if (!pickedUp && cachedCollider != null)
            cachedCollider.enabled = true;
    }

    private IEnumerator PlayClockAppearEffect()
    {
        if (clockAppearDuration <= 0f)
            yield break;

        Vector3 baseScale = initialLocalScale == Vector3.zero ? transform.localScale : initialLocalScale;
        Vector3 from = baseScale * 0.75f;
        Vector3 to = baseScale;

        transform.localScale = from;

        float t = 0f;
        while (t < clockAppearDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / clockAppearDuration);
            transform.localScale = Vector3.LerpUnclamped(from, to, k * k * (3f - 2f * k));
            yield return null;
        }

        transform.localScale = to;
    }
}
