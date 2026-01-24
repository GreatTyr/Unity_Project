using System;
using System.Collections.Generic;
using UnityEngine;

// Ресурсы: первая буква — индекс (P,F,M,C,B,N)
public enum ResourceType
{
    Provisions, // P
    Fuel,       // F
    Metal,      // M
    Chemicals,  // C (ранее Polymers)
    BuildingMaterials, // B
    Nanites     // N
}

// Одна запись: конкретный ресурс + тир
[Serializable]
public struct ResourceEntry
{
    // Упакованный ключ: (letterIndex << 4) | (тир-1)  -> помещается в byte
    public byte key;

    public ResourceType resource;
    [Range(1, 10)]
    public byte тир; // 1..10

    public ushort value; // пример дополнительного поля (количество/вес/стоимость)
}

// Пара для инвентаря/рецепта: ключ + количество
[Serializable]
public struct ResourceAmount
{
    public byte key;      // упакованный ключ (см. PackKey)
    public ushort count;  // количество
}

// ScriptableObject с плоским списком 60 элементов
[CreateAssetMenu(fileName = "ResourcesFlat", menuName = "Config/ResourcesFlat", order = 2)]
public class ResourcesFlatSO : ScriptableObject
{
    public const int EXPECTED_COUNT = 60;
    public const int RESOURCE_TYPES = 6;

    [SerializeField]
    private ResourceEntry[] entries = new ResourceEntry[EXPECTED_COUNT];

    // Индекс: byte key -> индекс в entries[]
    private Dictionary<byte, int> index;

    private void OnValidate()
    {
        if (entries == null || entries.Length != EXPECTED_COUNT)
            entries = new ResourceEntry[EXPECTED_COUNT];

        int p = 0;
        for (int r = 0; r < RESOURCE_TYPES; r++)
        {
            for (int t = 1; t <= 10; t++)
            {
                if (p >= entries.Length) break;
                entries[p].resource = (ResourceType)r;
                entries[p].тир = (byte)t;
                entries[p].key = PackKey((ResourceType)r, t);
                // entries[p].value оставляем как есть (можно редактировать в инспекторе)
                p++;
            }
        }

        index = null;
    }

    // Упаковка ключа: letterIndex (0..15) в старшие 4 бита, tier-1 (0..15) в младшие 4 бита.
    public static byte PackKey(ResourceType resource, int тир)
    {
        byte letterIndex = LetterIndexOf(resource);
        int t = Mathf.Clamp(тир, 1, 10) - 1;
        return (byte)((letterIndex << 4) | (t & 0x0F));
    }

    // Получить letterIndex по ResourceType (фиксированный мэппинг)
    // P=0, F=1, M=2, C=3, B=4, N=5
    public static byte LetterIndexOf(ResourceType r)
    {
        switch (r)
        {
            case ResourceType.Provisions: return 0; // 'P'
            case ResourceType.Fuel: return 1;       // 'F'
            case ResourceType.Metal: return 2;      // 'M'
            case ResourceType.Chemicals: return 3;  // 'C'
            case ResourceType.BuildingMaterials: return 4; // 'B'
            case ResourceType.Nanites: return 5;    // 'N'
            default: return 0;
        }
    }

    // Распаковка: получить ResourceType и тир из ключа
    public static void UnpackKey(byte key, out ResourceType resource, out int тир)
    {
        byte letter = (byte)(key >> 4);
        byte t = (byte)(key & 0x0F);
        resource = ResourceTypeFromLetterIndex(letter);
        тир = t + 1;
    }

    public static ResourceType ResourceTypeFromLetterIndex(byte letter)
    {
        switch (letter)
        {
            case 0: return ResourceType.Provisions;
            case 1: return ResourceType.Fuel;
            case 2: return ResourceType.Metal;
            case 3: return ResourceType.Chemicals;
            case 4: return ResourceType.BuildingMaterials;
            case 5: return ResourceType.Nanites;
            default: return ResourceType.Provisions;
        }
    }

    // Парсинг строки вида "P1" -> key (если неверно, вернёт 0)
    public static bool TryParseKey(string s, out byte key)
    {
        key = 0;
        if (string.IsNullOrEmpty(s) || s.Length < 2) return false;
        char letter = char.ToUpperInvariant(s[0]);
        if (!byte.TryParse(s.Substring(1), out byte tier)) return false;
        byte letterIndex;
        switch (letter)
        {
            case 'P': letterIndex = 0; break;
            case 'F': letterIndex = 1; break;
            case 'M': letterIndex = 2; break;
            case 'C': letterIndex = 3; break;
            case 'B': letterIndex = 4; break;
            case 'N': letterIndex = 5; break;
            default: return false;
        }
        int t = Mathf.Clamp(tier, 1, 10) - 1;
        key = (byte)((letterIndex << 4) | (t & 0x0F));
        return true;
    }

    private void EnsureIndex()
    {
        if (index != null) return;
        index = new Dictionary<byte, int>(EXPECTED_COUNT);
        for (int i = 0; i < entries.Length; i++)
        {
            index[entries[i].key] = i;
        }
    }

    public bool TryGetEntryByKey(byte key, out ResourceEntry entry)
    {
        EnsureIndex();
        if (index.TryGetValue(key, out int idx))
        {
            entry = entries[idx];
            return true;
        }
        entry = default;
        return false;
    }

    public ResourceEntry GetEntryByKey(byte key)
    {
        EnsureIndex();
        if (index.TryGetValue(key, out int idx))
            return entries[idx];
        throw new KeyNotFoundException($"Resource key not found: 0x{key:X2}");
    }

    // Прямой список для итераций
    public IReadOnlyList<ResourceEntry> Entries => entries;
}