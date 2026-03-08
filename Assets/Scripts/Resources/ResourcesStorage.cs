using System;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

// ResourcesStorage.cs
// Оптимизированное хранилище ресурсов с подготовкой к сериализации/снятию снапшотов и сетевой интеграции.
// - Energy (E) хранится в long energyUnits
// - 60 ресурсных тиров хранятся в long[] grams (в граммах)
// - Inspector: отображение в порядке Energy, Provisions T1 (P1) ... Nanites T10 (N10)
// - Значения по умолчанию: energyUnits = 1_000_000_000; each tier = 1_000_000 (грамм)

public class ResourcesStorage : MonoBehaviour
{
    // Default values
    public const long DefaultEnergy = 1_000_000_000L;
    public const long DefaultTierGrams = 1_000_000L; // 1_000_000 g = 1000 kg

    [Header("Energy (E)")]
    [Tooltip("Энергия в энергоединицах (E).")]
    [SerializeField]
    private long energyUnits = DefaultEnergy;

    [SerializeField]
    [Tooltip("Ресурсы в граммах (60 элементов): Provisions, Fuel, Metal, Building, Chemicals, Nanites (T1..T10).")]
    private long[] grams = new long[ResourceTiersCount];

    // Dirty mask: каждый бит соответствует одному тиру; бит 60 используется для energy (вне массива)
    // Позволяет пометить изменённые элементы для экономной синхронизации.
    [NonSerialized]
    private ulong dirtyMask = 0UL;

    public const int TiersPerType = 10;
    public const int ResourceTypesCount = 6;
    public const int ResourceTiersCount = ResourceTypesCount * TiersPerType; // 60
    public const long GramsPerKg = 1000L;

    #region Enums and Index helpers

    public enum ResourceType
    {
        Provisions = 0, // P
        Fuel = 1,       // F
        Metal = 2,      // M
        Building = 3,   // B
        Chemicals = 4,  // C
        Nanites = 5     // N
    }

    public enum ResourceIndex
    {
        P1 = 0, P2 = 1, P3 = 2, P4 = 3, P5 = 4, P6 = 5, P7 = 6, P8 = 7, P9 = 8, P10 = 9,
        F1 = 10, F2 = 11, F3 = 12, F4 = 13, F5 = 14, F6 = 15, F7 = 16, F8 = 17, F9 = 18, F10 = 19,
        M1 = 20, M2 = 21, M3 = 22, M4 = 23, M5 = 24, M6 = 25, M7 = 26, M8 = 27, M9 = 28, M10 = 29,
        B1 = 30, B2 = 31, B3 = 32, B4 = 33, B5 = 34, B6 = 35, B7 = 36, B8 = 37, B9 = 38, B10 = 39,
        C1 = 40, C2 = 41, C3 = 42, C4 = 43, C5 = 44, C6 = 45, C7 = 46, C8 = 47, C9 = 48, C10 = 49,
        N1 = 50, N2 = 51, N3 = 52, N4 = 53, N5 = 54, N6 = 55, N7 = 56, N8 = 57, N9 = 58, N10 = 59
    }

    #endregion

    #region Unity lifecycle

    private void Reset()
    {
        energyUnits = DefaultEnergy;
        EnsureArray();
        for (int i = 0; i < grams.Length; i++) grams[i] = DefaultTierGrams;
        dirtyMask = ulong.MaxValue; // mark all dirty on reset
    }

    private void Awake()
    {
        EnsureArray();
#if UNITY_EDITOR
        // Для свежесозданных объектов выставим дефолты, если массив нулевой
        bool anyZero = energyUnits == 0;
        for (int i = 0; i < grams.Length; i++) if (grams[i] == 0) { anyZero = true; break; }
        if (anyZero)
        {
            energyUnits = DefaultEnergy;
            for (int i = 0; i < grams.Length; i++) grams[i] = DefaultTierGrams;
            dirtyMask = ulong.MaxValue;
        }
#endif
    }

    private void OnValidate()
    {
        EnsureArray();
        if (energyUnits < 0) energyUnits = 0;
        for (int i = 0; i < grams.Length; i++) if (grams[i] < 0) grams[i] = 0;
    }

    private void EnsureArray()
    {
        if (grams == null || grams.Length != ResourceTiersCount)
            grams = new long[ResourceTiersCount];
    }

    #endregion

    #region Dirtiness (for network/save optimization)

    public void MarkDirtyEnergy()
    {
        dirtyMask |= (1UL << 63); // use highest bit for energy
    }

    public void MarkDirtyTier(int idx)
    {
        if (idx < 0 || idx >= ResourceTiersCount) return;
        dirtyMask |= (1UL << idx);
    }

    public bool IsDirty()
    {
        return dirtyMask != 0UL;
    }

    public bool IsTierDirty(int idx)
    {
        if (idx < 0 || idx >= ResourceTiersCount) return false;
        return (dirtyMask & (1UL << idx)) != 0UL;
    }

    public bool IsEnergyDirty()
    {
        return (dirtyMask & (1UL << 63)) != 0UL;
    }

    public void ClearDirty()
    {
        dirtyMask = 0UL;
    }

    #endregion

    #region Energy accessors

    public long EnergyUnits
    {
        get => energyUnits;
        set
        {
            long v = Math.Max(0L, value);
            if (v != energyUnits)
            {
                energyUnits = v;
                MarkDirtyEnergy();
            }
        }
    }

    public void AddEnergy(long amount)
    {
        if (amount <= 0) return;
        checked { energyUnits += amount; }
        MarkDirtyEnergy();
    }

    public bool TryConsumeEnergy(long amount)
    {
        if (amount <= 0) return true;
        if (energyUnits >= amount)
        {
            energyUnits -= amount;
            MarkDirtyEnergy();
            return true;
        }
        return false;
    }

    #endregion

    #region Resource accessors (kg <-> grams)

    public double GetKilograms(ResourceIndex idx)
    {
        return grams[(int)idx] / (double)GramsPerKg;
    }

    public long GetGrams(ResourceIndex idx)
    {
        return grams[(int)idx];
    }

    public void SetKilograms(ResourceIndex idx, double kg)
    {
        long g = KgToGramsRounded(kg);
        SetGrams(idx, g);
    }

    public void SetGrams(ResourceIndex idx, long g)
    {
        int i = (int)idx;
        long newg = Math.Max(0L, g);
        if (grams[i] != newg)
        {
            grams[i] = newg;
            MarkDirtyTier(i);
        }
    }

    public void AddKilograms(ResourceIndex idx, double kg)
    {
        long delta = KgToGramsRounded(kg);
        if (delta <= 0) return;
        AddGrams(idx, delta);
    }

    public void AddGrams(ResourceIndex idx, long g)
    {
        if (g <= 0) return;
        int i = (int)idx;
        checked { grams[i] += g; }
        MarkDirtyTier(i);
    }

    public bool TryRemoveKilograms(ResourceIndex idx, double kg)
    {
        long g = KgToGramsRounded(kg);
        return TryRemoveGrams(idx, g);
    }

    public bool TryRemoveGrams(ResourceIndex idx, long g)
    {
        if (g <= 0) return true;
        int i = (int)idx;
        if (grams[i] >= g)
        {
            grams[i] -= g;
            MarkDirtyTier(i);
            return true;
        }
        return false;
    }

    public bool TransferTo(ResourcesStorage target, ResourceIndex idx, long gramsAmount)
    {
        if (target == null || gramsAmount <= 0) return false;
        if (!TryRemoveGrams(idx, gramsAmount)) return false;
        target.AddGrams(idx, gramsAmount);
        return true;
    }

    #endregion

    #region DTO / Snapshot (для сериализации и сети)

    [Serializable]
    public struct ResourcesDTO
    {
        public int version;
        public long energyUnits;
        public long[] grams;
    }

    public ResourcesDTO ToDTO(int version = 1)
    {
        EnsureArray();
        return new ResourcesDTO
        {
            version = version,
            energyUnits = this.energyUnits,
            grams = (long[])this.grams.Clone()
        };
    }

    public void FromDTO(ResourcesDTO dto)
    {
        EnsureArray();
        this.energyUnits = Math.Max(0L, dto.energyUnits);
        if (dto.grams != null && dto.grams.Length == ResourceTiersCount)
            Array.Copy(dto.grams, this.grams, ResourceTiersCount);
        else
            Debug.LogWarning("ResourcesStorage.FromDTO: invalid grams length");
        // mark all dirty so caller может решить что нужно отправить/сохранить
        dirtyMask = ulong.MaxValue;
    }

    // Snapshot helpers (например для сетевого кода): быстрый снимок и применение
    public ResourcesDTO GetSnapshot(int version = 1) => ToDTO(version);
    public void ApplySnapshot(ResourcesDTO snap) => FromDTO(snap);

    #endregion

    #region Utilities

    public static long KgToGramsRounded(double kg)
    {
        double g = kg * GramsPerKg;
        long rounded = (long)Math.Round(g);
        if (rounded < 0) rounded = 0;
        return rounded;
    }

    public long this[ResourceIndex idx]
    {
        get => GetGrams(idx);
        set => SetGrams(idx, value);
    }

    public static string ResourceTypeAbbrev(ResourceType t)
    {
        return t switch
        {
            ResourceType.Provisions => "P",
            ResourceType.Fuel => "F",
            ResourceType.Metal => "M",
            ResourceType.Building => "B",
            ResourceType.Chemicals => "C",
            ResourceType.Nanites => "N",
            _ => "?"
        };
    }

    public static string ResourceFullName(int index)
    {
        if (index < 0 || index >= ResourceTiersCount) return "Unknown";
        int type = index / TiersPerType;
        int tier = (index % TiersPerType) + 1;
        string typeName = type switch
        {
            0 => "Provisions",
            1 => "Fuel",
            2 => "Metal",
            3 => "BuildingMaterials",
            4 => "Chemicals",
            5 => "Nanites",
            _ => "?"
        };
        char abbrev = type switch
        {
            0 => 'P',
            1 => 'F',
            2 => 'M',
            3 => 'B',
            4 => 'C',
            5 => 'N',
            _ => '?'
        };
        return $"{typeName} T{tier} ({abbrev}{tier})";
    }

    public static string ResourceName(ResourceIndex idx)
    {
        return ResourceFullName((int)idx);
    }

    #endregion
}

#if UNITY_EDITOR
[CustomEditor(typeof(ResourcesStorage))]
public class ResourcesStorageEditor : Editor
{
    private ResourcesStorage rs;
    private SerializedProperty energyProp;
    private SerializedProperty gramsProp;

    private void OnEnable()
    {
        rs = (ResourcesStorage)target;
        energyProp = serializedObject.FindProperty("energyUnits");
        gramsProp = serializedObject.FindProperty("grams");

        if (gramsProp.arraySize != ResourcesStorage.ResourceTiersCount)
        {
            gramsProp.arraySize = ResourcesStorage.ResourceTiersCount;
            serializedObject.ApplyModifiedProperties();
        }
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.LabelField("Resources Storage", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        // Energy
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel("Energy (E)");
        long newEnergy = EditorGUILayout.LongField(rs.EnergyUnits);
        if (newEnergy != rs.EnergyUnits)
        {
            serializedObject.FindProperty("energyUnits").longValue = Math.Max(0L, newEnergy);
            rs.MarkDirtyEnergy();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();

        // Draw resource tiers with full labels e.g. "Provisions T1 (P1)"
        for (int i = 0; i < ResourcesStorage.ResourceTiersCount; i++)
        {
            string label = ResourcesStorage.ResourceFullName(i);
            long g = rs.GetGrams((ResourcesStorage.ResourceIndex)i);
            double kg = g / (double)ResourcesStorage.GramsPerKg;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel(label);
            string kgStr = EditorGUILayout.TextField(kg.ToString("F3"));
            if (double.TryParse(kgStr, out double parsedKg))
            {
                long newG = ResourcesStorage.KgToGramsRounded(parsedKg);
                if (newG != g)
                {
                    gramsProp.GetArrayElementAtIndex(i).longValue = Math.Max(0L, newG);
                    rs.MarkDirtyTier(i);
                }
            }
            EditorGUILayout.LabelField("kg", GUILayout.Width(30));
            EditorGUILayout.EndHorizontal();
        }

        serializedObject.ApplyModifiedProperties();
    }
}
#endif