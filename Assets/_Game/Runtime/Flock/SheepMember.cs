using System;
using System.Collections;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
public sealed class SheepMember : MonoBehaviour
{
    [Header("Click Interaction")]
    [SerializeField, Min(0.1f)] private float nameLabelDuration = 1f;
    [SerializeField, Min(0f)] private float nameLabelRise = 0.5f;

    public static event Action<SheepMember> AnyClicked;

    public FlockController Flock { get; private set; }
    public SheepFlockAgent Agent { get; private set; }

    private TMP_Text nameLabel;
    private Coroutine nameLabelRoutine;

    private void Awake()
    {
        Rigidbody2D body = GetComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0f;
        body.freezeRotation = true;
        body.useFullKinematicContacts = true;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;

        GetComponent<CircleCollider2D>().isTrigger = true;
        SheepVisualAnimator.Ensure(gameObject);
    }

    internal bool Join(FlockController flock)
    {
        if (flock == null || (Flock != null && Flock != flock))
            return false;

        Flock = flock;
        return true;
    }

    internal void SetAgent(SheepFlockAgent agent)
    {
        Agent = agent;
    }

    internal void Leave(FlockController flock)
    {
        if (Flock == flock)
        {
            Flock = null;
            Agent = null;
        }
    }

    /// <summary>左键点击单只羊：按其当前品质发光、浮现名字，并拉一泡屎。三步都复用既有系统。</summary>
    private void OnMouseDown()
    {
        if (Time.timeScale == 0f)
            return;

        SpecialSheepMarker marker = GetComponent<SpecialSheepMarker>();
        SheepQuality quality = marker != null ? marker.Quality : SheepQuality.Common;
        SpecialSheepAcquisitionVfx.Ensure(gameObject).PlayGlowPreview(quality);

        ShowNameLabel();
        Flock?.GetComponent<PoopAbility>()?.TryPoopAt(this);
        AnyClicked?.Invoke(this);
    }

    private void ShowNameLabel()
    {
        SheepIdentity identity = GetComponent<SheepIdentity>();
        if (identity == null || string.IsNullOrWhiteSpace(identity.DisplayName))
            return;

        if (nameLabelRoutine != null)
            StopCoroutine(nameLabelRoutine);
        nameLabelRoutine = StartCoroutine(PlayNameLabel(identity.DisplayName));
    }

    private IEnumerator PlayNameLabel(string displayName)
    {
        nameLabel ??= CreateNameLabel();

        SpriteRenderer sprite = GetComponent<SpriteRenderer>();
        float baseHeight = (sprite != null ? sprite.bounds.extents.y : 0.5f) + 0.2f;
        nameLabel.text = displayName;
        nameLabel.gameObject.SetActive(true);

        float elapsed = 0f;
        while (elapsed < nameLabelDuration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / nameLabelDuration);
            nameLabel.transform.localPosition = new Vector3(0f, baseHeight + nameLabelRise * progress, 0f);
            nameLabel.alpha = 1f - progress;
            yield return null;
        }

        nameLabel.gameObject.SetActive(false);
        nameLabelRoutine = null;
    }

    private TMP_Text CreateNameLabel()
    {
        GameObject labelObject = new GameObject("NameLabel");
        labelObject.transform.SetParent(transform, false);
        TextMeshPro label = labelObject.AddComponent<TextMeshPro>();
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = 3f;
        label.color = Color.white;
        MvpTmpUiFont.Apply(label);
        return label;
    }
}
