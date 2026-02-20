using UnityEngine;

[RequireComponent(typeof(Collider))]
public abstract class InteractableBase : MonoBehaviour, IInteractable
{
    [Header("Hint")]
    [TextArea]
    public string hintText = "ֽאזלטעו F";

    public InteractionType interactionType = InteractionType.Simple;
    public string keyLabel = "F";

    [Header("Highlight")]
    public bool useEmissionHighlight = true;
    public Color emissionColor = Color.yellow;
    public float emissionIntensity = 2f;
    public Material highlightMaterial;
    public Light highlightLight;

    Renderer[] renderers;
    Material[] originalSharedMaterials;
    bool isHighlighted = false;
    bool materialsInstanced = false;

    protected virtual void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>();
        originalSharedMaterials = new Material[renderers.Length];

        for (int i = 0; i < renderers.Length; i++)
            originalSharedMaterials[i] = renderers[i].sharedMaterial;
    }

    protected virtual void OnDestroy()
    {
        if (materialsInstanced && renderers != null)
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;

                var mat = renderers[i].material;
                if (mat != null && mat != originalSharedMaterials[i])
                    Object.Destroy(mat);

                renderers[i].sharedMaterial = originalSharedMaterials[i];
            }
        }
    }

    public virtual void OnHoverEnter()
    {
        SetHighlight(true);
    }

    public virtual void OnHoverExit()
    {
        SetHighlight(false);
    }

    protected virtual void SetHighlight(bool enable)
    {
        if (isHighlighted == enable) return;
        isHighlighted = enable;

        if (highlightMaterial != null)
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                renderers[i].sharedMaterial = enable
                    ? highlightMaterial
                    : originalSharedMaterials[i];
            }
        }
        else if (useEmissionHighlight)
        {
            if (enable)
            {
                if (!materialsInstanced)
                {
                    for (int i = 0; i < renderers.Length; i++)
                    {
                        if (renderers[i] == null) continue;
                        renderers[i].material = new Material(originalSharedMaterials[i]);
                    }
                    materialsInstanced = true;
                }

                for (int i = 0; i < renderers.Length; i++)
                {
                    if (renderers[i] == null) continue;
                    var mat = renderers[i].material;
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", emissionColor * emissionIntensity);
                }
            }
            else
            {
                if (materialsInstanced)
                {
                    for (int i = 0; i < renderers.Length; i++)
                    {
                        if (renderers[i] == null) continue;
                        var mat = renderers[i].material;
                        mat.SetColor("_EmissionColor", Color.black);
                        mat.DisableKeyword("_EMISSION");
                    }
                }
            }
        }

        if (highlightLight != null)
            highlightLight.enabled = enable;
    }

    public abstract void Interact();
}