using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Калькулятор крафта хранилища энергии.
/// Реализует IModuleCalculator для ModuleWorkbench.
/// </summary>
public class EnergyStorageCalculator : IModuleCalculator
{
    // ====================== State ======================

    private List<StandardEnergyStorage> allRefs;
    private string[] refNames;
    private int selectedIndex;
    private StandardEnergyStorage selected;

    // Calculated results
    private float calcCapacity;

    // ====================== Constructor ======================

    public EnergyStorageCalculator()
    {
        Refresh();
    }

    public void Refresh()
    {
        var db = EnergyStorageDatabase.Instance;
        if (db != null)
        {
            allRefs = db.GetAll();
        }
        else
        {
            allRefs = new List<StandardEnergyStorage>();
        }

        refNames = new string[allRefs.Count];
        for (int i = 0; i < allRefs.Count; i++)
        {
            var es = allRefs[i];
            refNames[i] = es != null
                ? $"{es.gameObject.name} (T{es.ModuleTier})"
                : "(null)";
        }

        selectedIndex = 0;
        selected = allRefs.Count > 0 ? allRefs[0] : null;
    }

    // ====================== IModuleCalculator ======================

    public string ModuleType => ModuleTypesDatabase.TYPE_ENERGY_STORAGE;

    public int ReferenceCount => allRefs.Count;

    public string[] GetReferenceNames() => refNames;

    public void SelectReference(int index)
    {
        if (index < 0 || index >= allRefs.Count) return;
        selectedIndex = index;
        selected = allRefs[index];
    }

    public int SelectedIndex => selectedIndex;

    public float RefLength => selected != null ? selected.LengthMeters : 0f;
    public float RefWidth => selected != null ? selected.WidthMeters : 0f;
    public float RefHeight => selected != null ? selected.HeightMeters : 0f;
    public float RefRealVolume => selected != null ? selected.RealVolumeM3 : 0f;
    public float RefFillPercent => selected != null ? selected.FillPercentUsed : 100f;
    public float RefVolumeCoefficientPercent => selected != null ? selected.VolCoeffPercent : 100f;
    public int RefModuleTier => selected != null ? selected.ModuleTier : 1;
    public string RefFaction => selected != null ? selected.FactionShortName : "";

    public void Calculate(ModuleScaleData data)
    {
        if (selected == null)
        {
            calcCapacity = 0f;
            return;
        }

        calcCapacity = StandardEnergyStorage.CalcCapacity(data.effectiveVolume, selected.ModuleTier);
    }

    public void DrawResultsGUI()
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label("Energy Capacity:", GUILayout.Width(130));
        GUILayout.Label($"{calcCapacity:F3}");
        GUILayout.EndHorizontal();
    }

    public string GetCodeSegment()
    {
        // E[capacity]
        return $"E{calcCapacity:F3}";
    }

    public GameObject GetPrefab()
    {
        if (selected == null) return null;
        return selected.gameObject;
    }
}