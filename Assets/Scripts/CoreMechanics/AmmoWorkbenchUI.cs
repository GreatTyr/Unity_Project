using System.Globalization;
using UnityEngine;

/// <summary>
/// OnGUI интерфейс верстака конических боеприпасов.
/// Управляется через AmmoWorkbench.
/// </summary>
[RequireComponent(typeof(AmmoWorkbench))]
public class AmmoWorkbenchUI : MonoBehaviour
{
    private AmmoWorkbench workbench;

    private Vector2 scrollPos;

    private string sDiam = "10";
    private string sLen = "20";
    private string sExpMass = "0";
    private string sDEMass = "0";
    private string sPropMass = "0,001";
    private string sCaseMass = "0,001";
    private string sCraftCount = "1";
    private string sBarrelLen = "200";
    private string sBarrelDiam = "10";
    private string sShotAngle = "45";

    private bool stylesReady;
    private GUIStyle windowStyle;
    private GUIStyle boxStyle;
    private GUIStyle sectionStyle;
    private GUIStyle readonlyFieldStyle;
    private GUIStyle errorStyle;
    private GUIStyle warningStyle;
    private GUIStyle valueBoxStyle;

    private bool recalcRequested;
    private bool isEditingTextField;

    private static readonly string[] ChargeNames = { "FM", "HE", "EQ" };

    private static readonly string[] DENames =
    {
        "Картечь", "Дробь", "Огонь", "Химия", "Энергия"
    };

    private static readonly AmmoCalc.DamageElementType[] DEValues =
    {
        AmmoCalc.DamageElementType.Buckshot,
        AmmoCalc.DamageElementType.Pellet,
        AmmoCalc.DamageElementType.Fire,
        AmmoCalc.DamageElementType.Chemical,
        AmmoCalc.DamageElementType.Energy
    };

    private static readonly string[] AreaNames =
    {
        "Точка(P)", "Сфера(Sp)", "Конус(Cn)", "Облако(Cl)"
    };

    private static readonly AmmoCalc.AreaType[] AreaValues =
    {
        AmmoCalc.AreaType.Point,
        AmmoCalc.AreaType.Sphere,
        AmmoCalc.AreaType.Cone,
        AmmoCalc.AreaType.Cloud
    };

    private static readonly string[] FuzeNames =
    {
        "Нет(No)", "Контакт(Ct)", "Таймер(Tm)", "Высота(Alt)", "Сейсм.(Se)", "Дист.(Re)"
    };

    private void Awake()
    {
        workbench = GetComponent<AmmoWorkbench>();
        PullBuffersFromInput();
        workbench.Recalculate();
    }

    private void OnEnable()
    {
        PullBuffersFromInput();
        workbench.Recalculate();
    }

    private void OnGUI()
    {
        EnsureStyles();

        if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return)
        {
            CommitBufferedFields();
            workbench.Recalculate();
            PullBuffersFromInput();
            recalcRequested = false;
            GUI.FocusControl(null);
            Event.current.Use();
        }

        float panelW = Mathf.Min(1180f, Screen.width - 20f);
        float panelH = Mathf.Min(Screen.height - 20f, 930f);
        float x = 10f;
        float y = 10f;

        GUI.Box(new Rect(x, y, panelW, panelH), GUIContent.none, windowStyle);

        GUILayout.BeginArea(new Rect(x + 10f, y + 10f, panelW - 20f, panelH - 20f));
        scrollPos = GUILayout.BeginScrollView(scrollPos);

        bool changed = false;
        isEditingTextField = false;

        GUILayout.Label("ВЕРСТАК КОНИЧЕСКИХ БОЕПРИПАСОВ", boxStyle);

        var inp = workbench.ammoInput;

        SectionHeader("Код боеприпаса");
        GUI.enabled = false;
        GUILayout.TextField(workbench.Output != null ? workbench.Output.ammoCode : workbench.manualAmmoCode, readonlyFieldStyle);
        GUI.enabled = true;

        GUILayout.BeginHorizontal();
        workbench.manualAmmoCode = LabeledTextField("Ввод кода", workbench.manualAmmoCode, 220f, ref changed);
        if (GUILayout.Button("Вставить", GUILayout.Width(90), GUILayout.Height(24)))
        {
            workbench.manualAmmoCode = GUIUtility.systemCopyBuffer ?? "";
            changed = true;
        }
        if (GUILayout.Button("Применить", GUILayout.Width(90), GUILayout.Height(24)))
        {
            CommitBufferedFields();
            if (workbench.TryApplyManualCode())
                PullBuffersFromInput();
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(6);
        GUILayout.BeginHorizontal();

        // ===================== ЛЕВАЯ КОЛОНКА =====================
        GUILayout.BeginVertical(sectionStyle, GUILayout.Width((panelW - 60f) * 0.5f));

        SectionHeader("Вводимые параметры");

        GroupHeader("Оболочка");
        if (TierSlider("Тир оболочки", ref inp.shellTier))
        {
            changed = true;
            recalcRequested = true;
        }

        changed |= FloatField("Диаметр (мм)", ref sDiam, out _);
        changed |= FloatField("Длина (мм)", ref sLen, out _);

        float previewDiam = TryParseBufferFloatOrDefault(sDiam, inp.diameterMm);
        float previewLen = TryParseBufferFloatOrDefault(sLen, inp.lengthMm);
        previewDiam = AmmoCalc.NormalizeDiameterMm(previewDiam);
        previewLen = AmmoCalc.NormalizeLengthMm(previewLen, previewDiam);

        float previewMass = AmmoCalc.Ceil3(AmmoCalc.ProjectileMassKg(previewDiam, previewLen));
        ValueLine($"Расч. масса снаряда: {previewMass:F3} кг");

        GroupHeader("Тип боеприпаса");
        DrawChargeTypeSelector(inp, previewMass, ref changed);

        if (inp.chargeType != AmmoCalc.ChargeType.FM)
        {
            GroupHeader("Разрывной заряд");
            if (TierSlider("Тир заряда", ref inp.explosiveTier))
            {
                changed = true;
                recalcRequested = true;
            }

            float minPart = AmmoCalc.GetMinPartKg(previewMass);
            float maxExp = (inp.chargeType == AmmoCalc.ChargeType.HE)
                ? Mathf.Max(minPart, previewMass - minPart)
                : Mathf.Max(minPart, previewMass - minPart - Mathf.Max(inp.damageElementMassKg, minPart));

            changed |= FloatField("Масса заряда (кг)", ref sExpMass, out _);
            ValueLine($"Допустимо: {minPart:F3} .. {maxExp:F3} кг");
        }

        if (inp.chargeType == AmmoCalc.ChargeType.EQ)
        {
            GroupHeader("Поражающий элемент");

            DrawDESelector(inp, ref changed);

            if (inp.damageElementType == AmmoCalc.DamageElementType.Buckshot)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"Картечин: {inp.buckshotCount}", GUILayout.Width(220));
                int newBuck = Mathf.RoundToInt(GUILayout.HorizontalSlider(inp.buckshotCount, 2, 10));
                newBuck = Mathf.Clamp(newBuck, 2, 10);
                if (newBuck != inp.buckshotCount)
                {
                    CommitBufferedFields();
                    inp.buckshotCount = newBuck;
                    changed = true;
                    recalcRequested = true;
                }
                GUILayout.EndHorizontal();
            }

            if (TierSlider("Тир ПЭ", ref inp.damageElementTier))
            {
                changed = true;
                recalcRequested = true;
            }

            float minPart = AmmoCalc.GetMinPartKg(previewMass);
            float maxDe = Mathf.Max(minPart, previewMass - minPart - Mathf.Max(inp.explosiveMassKg, minPart));

            changed |= FloatField("Масса ПЭ (кг)", ref sDEMass, out _);
            ValueLine($"Допустимо: {minPart:F3} .. {maxDe:F3} кг");
        }

        GroupHeader("Область поражения");
        DrawAreaSelector(inp, ref changed);

        if (inp.chargeType != AmmoCalc.ChargeType.FM)
        {
            GroupHeader("Взрыватель");
            int fuzeIdx = (int)inp.fuzeType;
            int newFuze = GUILayout.SelectionGrid(fuzeIdx, FuzeNames, 3);
            if (newFuze != fuzeIdx)
            {
                CommitBufferedFields();
                inp.fuzeType = (AmmoCalc.FuzeType)newFuze;
                changed = true;
                recalcRequested = true;
            }
        }

        GroupHeader("Метательный заряд");
        if (TierSlider("Тир мет. заряда", ref inp.propellantTier))
        {
            changed = true;
            recalcRequested = true;
        }

        GUILayout.BeginHorizontal();

        GUILayout.BeginVertical();
        string propBefore = sPropMass;
        sPropMass = FloatFieldRaw("Масса мет. заряда (кг)", sPropMass, out _);
        if (sPropMass != propBefore) changed = true;
        GUILayout.EndVertical();

        GUILayout.BeginVertical(GUILayout.Width(120));
        GUILayout.Space(2f);
        if (GUILayout.Button("Макс. скорость", GUILayout.Width(120), GUILayout.Height(20)))
        {
            CommitBufferedFields();
            workbench.Recalculate();
            var currentOutput = workbench.Output;
            if (currentOutput != null)
            {
                inp.propellantMassKg = AmmoCalc.CalculatePropellantMassForMaxSpeed(currentOutput, workbench.barrelInput);
                sPropMass = inp.propellantMassKg.ToString("0.###", CultureInfo.InvariantCulture).Replace('.', ',');
                changed = true;
                recalcRequested = true;
            }
        }
        GUILayout.EndVertical();

        GUILayout.EndHorizontal();

        GroupHeader("Гильза");
        if (TierSlider("Тир гильзы", ref inp.caseTier))
        {
            changed = true;
            recalcRequested = true;
        }

        GUILayout.BeginHorizontal();

        GUILayout.BeginVertical();
        string caseBefore = sCaseMass;
        sCaseMass = FloatFieldRaw("Масса гильзы (кг)", sCaseMass, out _);
        if (sCaseMass != caseBefore) changed = true;
        GUILayout.EndVertical();

        GUILayout.BeginVertical(GUILayout.Width(120));
        GUILayout.Space(2f);
        if (GUILayout.Button("Мин. масса", GUILayout.Width(120), GUILayout.Height(20)))
        {
            CommitBufferedFields();
            workbench.Recalculate();
            var currentOutput = workbench.Output;
            if (currentOutput != null)
            {
                inp.caseMassKg = AmmoCalc.CalculateCaseMassForMinStrength(currentOutput);
                sCaseMass = inp.caseMassKg.ToString("0.###", CultureInfo.InvariantCulture).Replace('.', ',');
                changed = true;
                recalcRequested = true;
            }
        }
        GUILayout.EndVertical();

        GUILayout.EndHorizontal();

        GroupHeader("Изготовление");
        changed |= IntField("Количество", ref sCraftCount, out _);

        GroupHeader("Параметры ствола");
        changed |= FloatField("Длина ствола (мм)", ref sBarrelLen, out _);
        changed |= FloatField("Диаметр ствола (мм)", ref sBarrelDiam, out _);
        changed |= FloatField("Угол возвышения (°)", ref sShotAngle, out _);

        GUILayout.BeginHorizontal();
        GUILayout.Label($"Угол возвышения: {workbench.barrelInput.shotAngleDeg:F3}°", GUILayout.Width(220));
        float newAngle = GUILayout.HorizontalSlider(workbench.barrelInput.shotAngleDeg, 0f, 90f);
        newAngle = AmmoCalc.NormalizeAngleDeg(newAngle);
        if (Mathf.Abs(newAngle - workbench.barrelInput.shotAngleDeg) > 0.0001f)
        {
            CommitBufferedFields();
            workbench.barrelInput.shotAngleDeg = newAngle;
            sShotAngle = newAngle.ToString("0.###", CultureInfo.InvariantCulture).Replace('.', ',');
            changed = true;
            recalcRequested = true;
        }
        GUILayout.EndHorizontal();

        GUILayout.EndVertical();

        GUILayout.Space(10);

        // ===================== ПРАВАЯ КОЛОНКА =====================
        GUILayout.BeginVertical(sectionStyle, GUILayout.Width((panelW - 60f) * 0.5f));

        SectionHeader("Вычисляемые параметры");

        if (recalcRequested && !isEditingTextField && Event.current.type == EventType.Repaint)
        {
            CommitBufferedFields();
            workbench.Recalculate();
            PullBuffersFromInput();
            recalcRequested = false;
        }

        var o = workbench.Output;
        var b = workbench.BarrelOutput;

        if (o != null)
        {
            GroupHeader("Боеприпас");
            ValueLine($"Тип: {o.chargeType}");
            ValueLine($"Область поражения: {AreaToText(o.areaType)}");
            ValueLine($"Масса снаряда: {o.totalProjectileMassKg:F3} кг");
            ValueLine($"Масса оболочки: {o.shellMassKg:F3} кг");
            ValueLine($"Прочность оболочки: {o.shellStrength:F3}");

            if (o.chargeType != AmmoCalc.ChargeType.FM)
            {
                ValueLine($"Масса заряда: {o.explosiveMassKg:F3} кг");
                ValueLine($"Мощность заряда: {o.explosivePower:F3}");
            }

            if (o.chargeType == AmmoCalc.ChargeType.EQ)
            {
                ValueLine($"ПЭ: {DamageElementToText(o.damageElementType)}");
                ValueLine($"Масса ПЭ: {o.damageElementMassKg:F3} кг");

                if (o.damageElementType == AmmoCalc.DamageElementType.Buckshot)
                {
                    ValueLine($"Картечин: {o.buckshotCount}");
                    ValueLine($"Масса картечины: {o.buckshotSingleMassKg:F3} кг");
                    ValueLine($"Угол разлёта картечи: {o.buckshotSpreadAngleDeg:F3}°");
                }
            }

            if (!(o.chargeType == AmmoCalc.ChargeType.EQ &&
                  o.damageElementType == AmmoCalc.DamageElementType.Buckshot))
            {
                ValueLine($"Радиус поражения: {o.damageRadius:F3} м");
                ValueLine($"Пробитие в области: {o.areaPenetration:F3}");
                ValueLine($"Урон в области: {o.areaDamage:F3}");
            }

            if (o.areaType == AmmoCalc.AreaType.Cone &&
                o.damageElementType != AmmoCalc.DamageElementType.Buckshot)
            {
                ValueLine($"Угол конуса: {o.coneAngleDeg:F3}°");
            }

            ValueLine($"Сила выталкивания: {o.propulsionForce:F3}");
            ValueLine($"Прочность гильзы: {o.caseStrength:F3}");
            ValueLine($"Масса боеприпаса: {o.totalAmmoMassKg:F3} кг");
            ValueLine($"Эффективная гравитация: {o.effectiveGravity:F3}");

            GroupHeader("Стоимость");
            var costs = workbench.Costs;
            if (costs != null)
            {
                foreach (var c in costs)
                {
                    if (c.isEnergy)
                    {
                        long total = c.amountEnergy * inp.craftCount;
                        ValueLine($"Энергия: {total} ед.");
                    }
                    else
                    {
                        var ri = AmmoCalc.GetResourceIndex(c.resourceType, c.tier);
                        string rName = ResourcesStorage.ResourceName(ri);
                        float totalKg = AmmoCalc.Ceil3(c.amountKg * inp.craftCount);
                        ValueLine($"{rName}: {totalKg:F3} кг");
                    }
                }
            }

            GroupHeader("Оценка для ствола");
            if (b != null && b.valid)
            {
                ValueLine($"Скорость снаряда: {b.projectileSpeed:F3} м/с");
                ValueLine($"Точность: {b.accuracy:F6}°");
                ValueLine($"Дистанция полёта: {b.flightDistance:F3} м");
                ValueLine($"Макс. высота: {b.maxHeight:F3} м");
                ValueLine($"Прямой урон: {b.directDamage:F3}");
                ValueLine($"Прямое пробитие: {b.directPenetration:F3}");
            }
        }

        if (workbench.Warnings.Count > 0)
        {
            GUILayout.Space(8);
            foreach (var w in workbench.Warnings)
                WarningLine(w);
        }

        if (workbench.Errors.Count > 0)
        {
            GUILayout.Space(8);
            foreach (var e in workbench.Errors)
                ErrorLine(e);
        }

        GUILayout.EndVertical();

        GUILayout.EndHorizontal();

        GUILayout.Space(10);

        GUILayout.BeginHorizontal();
        GUI.enabled = workbench.CanCraft;
        if (GUILayout.Button("ИЗГОТОВИТЬ", GUILayout.Height(36)))
        {
            CommitBufferedFields();
            workbench.Recalculate();
            workbench.TryCraft();
            PullBuffersFromInput();
            recalcRequested = false;
        }
        GUI.enabled = true;

        if (GUILayout.Button("Сброс", GUILayout.Width(120), GUILayout.Height(36)))
        {
            workbench.ResetToDefaults();
            PullBuffersFromInput();
            recalcRequested = false;
        }
        GUILayout.EndHorizontal();

        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    private void CommitBufferedFields()
    {
        var inp = workbench.ammoInput;

        if (TryParseBufferFloat(sDiam, out float diam))
            inp.diameterMm = AmmoCalc.NormalizeDiameterMm(diam);

        if (TryParseBufferFloat(sLen, out float len))
            inp.lengthMm = AmmoCalc.NormalizeLengthMm(len, inp.diameterMm);

        float previewMass = AmmoCalc.Ceil3(AmmoCalc.ProjectileMassKg(inp.diameterMm, inp.lengthMm));
        float minPart = AmmoCalc.GetMinPartKg(previewMass);

        if (TryParseBufferFloat(sExpMass, out float expMass))
        {
            if (inp.chargeType == AmmoCalc.ChargeType.HE)
            {
                float maxExp = Mathf.Max(minPart, previewMass - minPart);
                inp.explosiveMassKg = AmmoCalc.Ceil3(Mathf.Clamp(expMass, minPart, maxExp));
            }
            else if (inp.chargeType == AmmoCalc.ChargeType.EQ)
            {
                float maxExp = Mathf.Max(minPart, previewMass - minPart - Mathf.Max(inp.damageElementMassKg, minPart));
                inp.explosiveMassKg = AmmoCalc.Ceil3(Mathf.Clamp(expMass, minPart, maxExp));
            }
        }

        if (TryParseBufferFloat(sDEMass, out float deMass))
        {
            if (inp.chargeType == AmmoCalc.ChargeType.EQ)
            {
                float maxDe = Mathf.Max(minPart, previewMass - minPart - Mathf.Max(inp.explosiveMassKg, minPart));
                inp.damageElementMassKg = AmmoCalc.Ceil3(Mathf.Clamp(deMass, minPart, maxDe));
            }
        }

        if (TryParseBufferFloat(sPropMass, out float propMass))
            inp.propellantMassKg = AmmoCalc.Ceil3(Mathf.Max(propMass, 0.001f));

        if (TryParseBufferFloat(sCaseMass, out float caseMass))
            inp.caseMassKg = AmmoCalc.Ceil3(Mathf.Max(caseMass, 0.001f));

        if (int.TryParse(sCraftCount, out int count))
            inp.craftCount = Mathf.Max(count, 1);

        if (TryParseBufferFloat(sBarrelLen, out float barrelLen))
            workbench.barrelInput.barrelLengthMm = Mathf.Max(1f, Mathf.Ceil(barrelLen));

        if (TryParseBufferFloat(sBarrelDiam, out float barrelDiam))
            workbench.barrelInput.barrelDiameterMm = AmmoCalc.Ceil2(Mathf.Max(1f, barrelDiam));

        if (TryParseBufferFloat(sShotAngle, out float shotAngle))
            workbench.barrelInput.shotAngleDeg = AmmoCalc.NormalizeAngleDeg(shotAngle);
    }

    private bool TryParseBufferFloat(string buffer, out float value)
    {
        string normalized = (buffer ?? "").Replace(',', '.');
        return float.TryParse(
            normalized,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out value);
    }

    private float TryParseBufferFloatOrDefault(string buffer, float defaultValue)
    {
        return TryParseBufferFloat(buffer, out float value) ? value : defaultValue;
    }

    private void DrawChargeTypeSelector(AmmoCalc.AmmoInput inp, float previewMass, ref bool changed)
    {
        int current = (int)inp.chargeType;

        GUILayout.BeginHorizontal();

        for (int i = 0; i < ChargeNames.Length; i++)
        {
            AmmoCalc.ChargeType type = (AmmoCalc.ChargeType)i;
            bool allowed = AmmoCalc.IsChargeTypeAllowed(type, previewMass);

            bool prevEnabled = GUI.enabled;
            GUI.enabled = allowed;

            if (GUILayout.Toggle(current == i, ChargeNames[i], GUI.skin.button, GUILayout.Height(26)))
            {
                if (current != i)
                {
                    CommitBufferedFields();
                    inp.chargeType = type;
                    changed = true;
                    recalcRequested = true;
                }
            }

            GUI.enabled = prevEnabled;
        }

        GUILayout.EndHorizontal();

        if (!AmmoCalc.IsChargeTypeAllowed(AmmoCalc.ChargeType.HE, previewMass))
            WarningLine("HE доступен при массе снаряда от 0.500 кг");
        if (!AmmoCalc.IsChargeTypeAllowed(AmmoCalc.ChargeType.EQ, previewMass))
            WarningLine("EQ доступен при массе снаряда от 1.000 кг");
    }

    private void DrawDESelector(AmmoCalc.AmmoInput inp, ref bool changed)
    {
        GUILayout.BeginHorizontal();

        for (int i = 0; i < DEValues.Length; i++)
        {
            bool selected = inp.damageElementType == DEValues[i];
            if (GUILayout.Toggle(selected, DENames[i], GUI.skin.button, GUILayout.Height(24)))
            {
                if (!selected)
                {
                    CommitBufferedFields();
                    inp.damageElementType = DEValues[i];
                    changed = true;
                    recalcRequested = true;
                }
            }
        }

        GUILayout.EndHorizontal();
    }

    private void DrawAreaSelector(AmmoCalc.AmmoInput inp, ref bool changed)
    {
        GUILayout.BeginHorizontal();

        for (int i = 0; i < AreaValues.Length; i++)
        {
            AmmoCalc.AreaType area = AreaValues[i];
            bool allowed = IsAreaAllowed(inp, area);
            bool selected = AmmoCalc.NormalizeAreaType(inp.areaType) == area;

            bool prev = GUI.enabled;
            GUI.enabled = allowed;

            if (GUILayout.Toggle(selected, AreaNames[i], GUI.skin.button, GUILayout.Height(24)))
            {
                if (allowed && !selected)
                {
                    CommitBufferedFields();
                    inp.areaType = area;
                    changed = true;
                    recalcRequested = true;
                }
            }

            GUI.enabled = prev;
        }

        GUILayout.EndHorizontal();
    }

    private bool IsAreaAllowed(AmmoCalc.AmmoInput inp, AmmoCalc.AreaType area)
    {
        switch (inp.chargeType)
        {
            case AmmoCalc.ChargeType.FM:
                return area == AmmoCalc.AreaType.Point;

            case AmmoCalc.ChargeType.HE:
                return area == AmmoCalc.AreaType.Sphere;

            case AmmoCalc.ChargeType.EQ:
                switch (inp.damageElementType)
                {
                    case AmmoCalc.DamageElementType.Buckshot:
                        return area == AmmoCalc.AreaType.Point;
                    case AmmoCalc.DamageElementType.Pellet:
                        return area == AmmoCalc.AreaType.Sphere || area == AmmoCalc.AreaType.Cone;
                    case AmmoCalc.DamageElementType.Fire:
                    case AmmoCalc.DamageElementType.Chemical:
                    case AmmoCalc.DamageElementType.Energy:
                        return area == AmmoCalc.AreaType.Cloud;
                }
                break;
        }

        return false;
    }

    private bool TierSlider(string label, ref int current)
    {
        int old = current;
        GUILayout.BeginHorizontal();
        GUILayout.Label($"{label}: {current}", GUILayout.Width(220));
        current = Mathf.RoundToInt(GUILayout.HorizontalSlider(current, 1, 10));
        current = Mathf.Clamp(current, 1, 10);
        GUILayout.EndHorizontal();
        return old != current;
    }

    private string FloatFieldRaw(string label, string buffer, out float value)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, GUILayout.Width(220));
        buffer = GUILayout.TextField(buffer, GUILayout.Width(130));
        GUILayout.EndHorizontal();

        if (GUI.GetNameOfFocusedControl() != "")
            isEditingTextField = true;

        string normalized = buffer.Replace(',', '.');
        if (!float.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            value = 0f;

        return buffer;
    }

    private bool FloatField(string label, ref string buffer, out float value)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, GUILayout.Width(220));
        string newBuffer = GUILayout.TextField(buffer, GUILayout.Width(130));
        GUILayout.EndHorizontal();

        bool changed = newBuffer != buffer;
        buffer = newBuffer;

        if (GUI.GetNameOfFocusedControl() != "")
            isEditingTextField = true;

        string normalized = buffer.Replace(',', '.');
        if (!float.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            value = 0f;

        return changed;
    }

    private bool IntField(string label, ref string buffer, out int value)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, GUILayout.Width(220));
        string newBuffer = GUILayout.TextField(buffer, GUILayout.Width(130));
        GUILayout.EndHorizontal();

        bool changed = newBuffer != buffer;
        buffer = newBuffer;

        if (GUI.GetNameOfFocusedControl() != "")
            isEditingTextField = true;

        if (!int.TryParse(buffer, out value))
            value = 1;

        return changed;
    }

    private string LabeledTextField(string label, string value, float labelWidth, ref bool changed)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, GUILayout.Width(labelWidth));
        string newValue = GUILayout.TextField(value);
        GUILayout.EndHorizontal();
        if (newValue != value) changed = true;
        return newValue;
    }

    private void PullBuffersFromInput()
    {
        var inp = workbench.ammoInput;
        sDiam = inp.diameterMm.ToString("0.00", CultureInfo.InvariantCulture).Replace('.', ',');
        sLen = Mathf.CeilToInt(inp.lengthMm).ToString();
        sExpMass = inp.explosiveMassKg.ToString("0.###", CultureInfo.InvariantCulture).Replace('.', ',');
        sDEMass = inp.damageElementMassKg.ToString("0.###", CultureInfo.InvariantCulture).Replace('.', ',');
        sPropMass = inp.propellantMassKg.ToString("0.###", CultureInfo.InvariantCulture).Replace('.', ',');
        sCaseMass = inp.caseMassKg.ToString("0.###", CultureInfo.InvariantCulture).Replace('.', ',');
        sCraftCount = inp.craftCount.ToString();
        sBarrelLen = workbench.barrelInput.barrelLengthMm.ToString("0", CultureInfo.InvariantCulture).Replace('.', ',');
        sBarrelDiam = workbench.barrelInput.barrelDiameterMm.ToString("0.00", CultureInfo.InvariantCulture).Replace('.', ',');
        sShotAngle = workbench.barrelInput.shotAngleDeg.ToString("0.###", CultureInfo.InvariantCulture).Replace('.', ',');
    }

    private void EnsureStyles()
    {
        if (stylesReady) return;

        windowStyle = new GUIStyle(GUI.skin.box);
        windowStyle.normal.background = MakeTex(new Color(0.08f, 0.08f, 0.1f, 0.95f));

        boxStyle = new GUIStyle(GUI.skin.box);
        boxStyle.alignment = TextAnchor.MiddleCenter;
        boxStyle.fontStyle = FontStyle.Bold;
        boxStyle.normal.background = MakeTex(new Color(0.16f, 0.16f, 0.2f, 1f));

        sectionStyle = new GUIStyle(GUI.skin.box);
        sectionStyle.padding = new RectOffset(8, 8, 8, 8);
        sectionStyle.normal.background = MakeTex(new Color(0.14f, 0.14f, 0.17f, 0.98f));

        readonlyFieldStyle = new GUIStyle(GUI.skin.textField);
        readonlyFieldStyle.normal.background = MakeTex(new Color(0.2f, 0.2f, 0.24f, 1f));
        readonlyFieldStyle.normal.textColor = Color.white;

        valueBoxStyle = new GUIStyle(GUI.skin.box);
        valueBoxStyle.alignment = TextAnchor.MiddleLeft;
        valueBoxStyle.normal.background = MakeTex(new Color(0.18f, 0.18f, 0.22f, 1f));

        errorStyle = new GUIStyle(GUI.skin.label);
        errorStyle.normal.textColor = new Color(1f, 0.35f, 0.35f, 1f);
        errorStyle.wordWrap = true;

        warningStyle = new GUIStyle(GUI.skin.label);
        warningStyle.normal.textColor = new Color(1f, 0.9f, 0.35f, 1f);
        warningStyle.wordWrap = true;

        stylesReady = true;
    }

    private Texture2D MakeTex(Color c)
    {
        Texture2D tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, c);
        tex.Apply();
        return tex;
    }

    private void SectionHeader(string text)
    {
        GUILayout.Space(4);
        GUILayout.Label(text, boxStyle);
    }

    private void GroupHeader(string text)
    {
        GUILayout.Space(4);
        GUILayout.Label($"— {text} —", GUI.skin.box);
    }

    private void ValueLine(string text)
    {
        GUILayout.Label(text, valueBoxStyle);
    }

    private void ErrorLine(string text)
    {
        GUILayout.Label($"✖ {text}", errorStyle);
    }

    private void WarningLine(string text)
    {
        GUILayout.Label($"⚠ {text}", warningStyle);
    }

    private string AreaToText(AmmoCalc.AreaType a)
    {
        a = AmmoCalc.NormalizeAreaType(a);
        switch (a)
        {
            case AmmoCalc.AreaType.Point: return "Точка";
            case AmmoCalc.AreaType.Sphere: return "Сфера";
            case AmmoCalc.AreaType.Cone: return "Конус";
            case AmmoCalc.AreaType.Cloud: return "Облако";
            default: return "Точка";
        }
    }

    private string DamageElementToText(AmmoCalc.DamageElementType t)
    {
        switch (t)
        {
            case AmmoCalc.DamageElementType.Shrapnel: return "Осколки";
            case AmmoCalc.DamageElementType.Buckshot: return "Картечь";
            case AmmoCalc.DamageElementType.Pellet: return "Дробь";
            case AmmoCalc.DamageElementType.Fire: return "Огонь";
            case AmmoCalc.DamageElementType.Chemical: return "Химия";
            case AmmoCalc.DamageElementType.Energy: return "Энергия";
            default: return "Нет";
        }
    }
}