// InteractableBase.cs
// Базовый класс для интерактивных объектов.
// Реализует подсветку (через смену материалов/эмиссию), и вызывает менеджер при OnTriggerEnter/Exit.
// Наследуйся от этого класса и реализуй метод Interact() для конкретного действия.

using UnityEngine;

[RequireComponent(typeof(Collider))]
public abstract class InteractableBase : MonoBehaviour, Interactable
{
    [Header("Hint")]
    [Tooltip("Текст подсказки, который будет показан на UI при наведении")]
    [TextArea]
    public string hintText = "Нажмите F";

    [Tooltip("Тип взаимодействия (для шаблонов UI и логики)")]
    public InteractionType interactionType = InteractionType.Simple;

    [Tooltip("Отображаемая клавиша/иконка (например, F, E)")]
    public string keyLabel = "F";

    [Header("Highlight")]
    public bool useEmissionHighlight = true;      // включать эмиссию материала
    public Color emissionColor = Color.yellow;   // цвет эмиссии
    public float emissionIntensity = 2f;         // интенсивность эмиссии
    public Material highlightMaterial;           // опционально: заменить материал целиком для подсветки
    public Light highlightLight;                 // опционально: включать свет при подсветке

    // внутренние поля для хранения оригинальных материалов
    Renderer[] renderers;
    Material[] originalMaterials;
    bool isHighlighted = false;

    protected virtual void Awake()
    {
        // Кэшируем рендереры и оригинальные материалы на Awake
        renderers = GetComponentsInChildren<Renderer>();
        originalMaterials = new Material[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
            originalMaterials[i] = renderers[i].material;
    }

    // Интерфейсные методы для hover — могут быть переопределены в наследниках
    public virtual void OnHoverEnter()
    {
        SetHighlight(true);
    }

    public virtual void OnHoverExit()
    {
        SetHighlight(false);
    }

    // Переключение подсветки: либо меняем материал, либо включаем эмиссию
    protected virtual void SetHighlight(bool enable)
    {
        if (isHighlighted == enable) return;
        isHighlighted = enable;

        if (highlightMaterial != null)
        {
            // Если задан override материал — применить/вернуть оригинал
            for (int i = 0; i < renderers.Length; i++)
                renderers[i].material = enable ? highlightMaterial : originalMaterials[i];
        }
        else if (useEmissionHighlight)
        {
            // Меняем эмиссию у текущего материала
            for (int i = 0; i < renderers.Length; i++)
            {
                var mat = renderers[i].material;
                if (enable)
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", emissionColor * emissionIntensity);
                }
                else
                {
                    mat.SetColor("_EmissionColor", Color.black);
                    mat.DisableKeyword("_EMISSION");
                }
            }
        }

        // Управление Light (если указан)
        if (highlightLight != null)
            highlightLight.enabled = enable;
    }

    // Наследник обязан реализовать логику Interact()
    public abstract void Interact();

    // Удобный хук: если интерактивный объект имеет Collider (обычно не trigger),
    // и игрок имеет триггер-сферу (на child), то этот OnTriggerEnter/Exit позволит
    // автоматически уведомить менеджер взаимодействия на игроке.
    void OnTriggerEnter(Collider other)
    {
        var mgr = other.GetComponentInParent<PlayerInteractionManager>();
        if (mgr != null) mgr.OnObjectHovered(this);
    }

    void OnTriggerExit(Collider other)
    {
        var mgr = other.GetComponentInParent<PlayerInteractionManager>();
        if (mgr != null) mgr.OnObjectUnhovered(this);
    }
}