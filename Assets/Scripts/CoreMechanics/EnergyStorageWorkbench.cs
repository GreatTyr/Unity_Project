using System;
using System.Collections.Generic;
using UnityEngine;

public class EnergyStorageWorkbench : BaseModuleWorkbench
{
    [Header("Energy Storage")]
    public EnergyStorageDatabase energyStorageDatabase;

    private List<StandardEnergyStorage> allRefs = new List<StandardEnergyStorage>();
    private string[] refNames = new string[0];
    private int selectedRefIndex;
    private StandardEnergyStorage selectedRef;

    private float calcEnergyCapacity;
    private float calcCraftTimeSeconds;

    protected override string ModuleTypeName => "EnergyStorage";

    protected override void RebuildReferenceList()
    {
        allRefs.Clear();
        if (energyStorageDatabase == null)
        {
            refNames = new string[0];
            selectedRef = null;
            return;
        }

        allRefs = energyStorageDatabase.GetAll();
        refNames = new string[allRefs.Count];

        for (int i = 0; i < allRefs.Count; i++)
        {
            var s = allRefs[i];
            if (s != null)
            {
                string faction = string.IsNullOrEmpty(s.FactionShortName) ? "NONE" : s.FactionShortName;
                string bp = string.IsNullOrEmpty(s.BlueprintId) ? "000" : s.BlueprintId;
                refNames[i] = $"[{faction}-{bp}] {s.gameObject.name} (T{s.ModuleTier})";
            }
            else
            {
                refNames[i] = "(null)";
            }
        }

        if (selectedRefIndex >= allRefs.Count) selectedRefIndex = 0;
        if (allRefs.Count > 0) SelectReference(selectedRefIndex);
        else selectedRef = null;
    }

    protected override string[] GetReferenceNames() => refNames;
    protected override int GetSelectedReferenceIndex() => selectedRefIndex;
    protected override int GetReferenceCount() => allRefs.Count;
    protected override string GetReferenceBlueprintID() => selectedRef != null ? selectedRef.BlueprintId : "000";

    protected override bool TryFindAndSelectReference(string faction, string blueprintId)
    {
        if (energyStorageDatabase == null) return false;
        var found = energyStorageDatabase.GetByFactionAndBlueprintID(faction, blueprintId);
        if (found == null) return false;

        int idx = allRefs.IndexOf(found);
        if (idx < 0) return false;

        SelectReference(idx);
        return true;
    }

    protected override void SelectReference(int index)
    {
        if (index < 0 || index >= allRefs.Count) return;

        selectedRefIndex = index;
        selectedRef = allRefs[index];

        if (selectedRef != null)
        {
            scaler.SetReference(
                selectedRef.LengthMeters,
                selectedRef.WidthMeters,
                selectedRef.HeightMeters,
                selectedRef.RealVolumeM3,
                selectedRef.ConstantFillPercent
            );
        }
    }

    protected override int GetReferenceTier() => selectedRef != null ? selectedRef.ModuleTier : 1;
    protected override string GetReferenceFaction() => selectedRef != null ? selectedRef.FactionShortName : "";
    protected override float GetReferenceFillPercent() => selectedRef != null ? selectedRef.ConstantFillPercent : 100f;
    protected override float GetReferenceVolumeCoeffPercent() => selectedRef != null ? selectedRef.VolumeCoefficientPercent : 100f;
    protected override string GetReferenceName() => selectedRef != null ? selectedRef.gameObject.name : "";
    protected override GameObject GetReferencePrefab() => selectedRef != null ? selectedRef.gameObject : null;

    protected override ResourcesStorage.ResourceIndex GetMetalIndex()
    {
        int tier = GetReferenceTier();
        return (ResourcesStorage.ResourceIndex)((int)ResourcesStorage.ResourceType.Metal * ResourcesStorage.TiersPerType + (tier - 1));
    }

    protected override void RecalculateSpecifics()
    {
        if (selectedRef == null)
        {
            calcEnergyCapacity = 0f;
            calcCraftTimeSeconds = 0f;
            return;
        }

        float fillFactor = selectedRef.ConstantFillPercent / 100f;
        float effectiveVolume = scaler.CalcEffectiveVolume * fillFactor;
        float effectiveVolumeDm3 = effectiveVolume * 1000f;

        calcEnergyCapacity = R3(effectiveVolumeDm3 * TierCoeffs.Get(selectedRef.ModuleTier));
        calcCraftTimeSeconds = selectedRef.CraftTimePerLiter * effectiveVolumeDm3;
    }

    protected override void DrawModuleSpecificSection()
    {
        GUILayout.BeginVertical("box");
        GUILayout.Label("Параметры Energy Storage", GetBoldStyle());

        GUILayout.BeginHorizontal();
        ParamBox("Емкость", $"{calcEnergyCapacity:F3}");
        ParamBox("Время крафта", $"<color=#00FF00>{calcCraftTimeSeconds:F1} сек</color>");
        GUILayout.EndHorizontal();

        GUILayout.EndVertical();
    }

    private void ParamBox(string label, string val)
    {
        GUILayout.BeginVertical(GUILayout.Width(180));
        GUILayout.Label($"<color=#AAAAAA>{label}</color>", new GUIStyle(GUI.skin.label) { fontSize = 12 });
        GUILayout.Label(val, GetBoldStyle());
        GUILayout.EndVertical();
    }

    protected override string GetSpecificCodeSegment()
    {
        return $"C{calcEnergyCapacity.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)}";
    }

    protected override ModuleData CreateSpecificModuleData()
    {
        var data = new EnergyStorageData();
        data.energyCapacity = calcEnergyCapacity;
        return data;
    }

    private static float R3(float v) => (float)Math.Round(v, 3);
}