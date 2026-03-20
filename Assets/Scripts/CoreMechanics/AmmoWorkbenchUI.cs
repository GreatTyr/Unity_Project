// AmmoWorkbenchUI.cs
using System.Globalization;
using UnityEngine;

/// <summary>
/// OnGUI интерфейс верстака конических снарядов.
/// Управляется через AmmoWorkbench.
/// Включается/выключается извне.
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

    private bool stylesReady;
    private GUIStyle windowStyle;
    private GUIStyle boxStyle;
    private GUIStyle sectionStyle;
    private GUIStyle readonlyFieldStyle;
    private GUIStyle errorStyle;
    private GUIStyle warningStyle;
    private GUIStyle valueBoxStyle;

    private static readonly string[] ChargeNames =
    {
        "FM", "HE", "EQ"
    };

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

        float panelW = Mathf.Min(1180f, Screen.width - 20f);
        float panelH = Mathf.Min(Screen.height - 20f, 880f);
        float x = 10f;
        float y = 10f;

        GUI.Box(new Rect(x, y, panelW, panelH), GUIContent.none, windowStyle);

        GUILayout.BeginArea(new Rect(x + 10f, y + 10f, panelW - 20f, panelH - 20f));
        scrollPos = GUILayout.BeginScrollView(scrollPos);

        bool changed = false;

        GUILayout.Label("ВЕРСТАК КОНИЧЕСКИХ СНАРЯДОВ", boxStyle);

        var inp = workbench.ammoInput;

        // Верхняя строка: текущий код
        SectionHeader("Код снаряда");
        GUI.enabled = false;
        GUILayout.TextField(workbench.Output != null ? workbench.Output.ammoCode : workbench.manualAmmoCode, readonlyFieldStyle);
        GUI.enabled = true;

        // Ввод кода вручную
        GUILayout.BeginHorizontal();
        workbench.manualAmmoCode = LabeledTextField("Ввод кода", workbench.manualAmmoCode, 220f, ref changed);
        if (GUILayout.Button("Вставить", GUILayout.Width(90), GUILayout.Height(24)))
        {
            workbench.manualAmmoCode = GUIUtility.systemCopyBuffer ?? "";
            changed = true;
        }
        if (GUILayout.Button("Применить", GUILayout.Width(90), GUILayout.Height(24)))
        {
            if (!workbench.TryApplyManualCode())
            {
                // ошибка останется в craftError
            }
            PullBuffersFromInput();
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(6);

        GUILayout.BeginHorizontal();

        // ===================== ЛЕВАЯ КОЛОНКА =====================
        GUILayout.BeginVertical(sectionStyle, GUILayout.Width((panelW - 60f) * 0.5f));

        SectionHeader("Вводимые параметры");

        // Оболочка
        GroupHeader("Оболочка");
        changed |= TierSlider("Тир оболочки", ref inp.shellTier);

        changed |= FloatField("Диаметр (мм)", ref sDiam, out float diam);
        inp.diameterMm = AmmoCalc.NormalizeDiameterMm(diam);

        changed |= FloatField("Длина (мм)", ref sLen, out float len);
        inp.lengthMm = AmmoCalc.NormalizeLengthMm(len, inp.diameterMm);

        float previewMass = AmmoCalc.Ceil3(AmmoCalc.ProjectileMassKg(inp.diameterMm, inp.lengthMm));
        ValueLine($"Расч. масса снаряда: {previewMass:F3} кг");

        // Тип снаряда
        GroupHeader("Тип снаряда");
        DrawChargeTypeSelector(inp, previewMass, ref changed);

        // Разрывной заряд
        if (inp.chargeType != AmmoCalc.ChargeType.FM)
        {
            GroupHeader("Разрывной заряд");
            changed |= TierSlider("Тир заряда", ref inp.explosiveTier);

            float minPart = AmmoCalc.GetMinPartKg(previewMass);
            float maxExp = (inp.chargeType == AmmoCalc.ChargeType.HE)
                ? Mathf.Max(minPart, previewMass - minPart)
                : Mathf.Max(minPart, previewMass - minPart - Mathf.Max(inp.damageElementMassKg, minPart));

            changed |= FloatField("Масса заряда (кг)", ref sExpMass, out float expM);
            inp.explosiveMassKg = Mathf.Max(expM, minPart);
            if (inp.explosiveMassKg > maxExp) inp.explosiveMassKg = maxExp;
            ValueLine($"Допустимо: {minPart:F3} .. {maxExp:F3} кг");
        }

        // Поражающий элемент
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
                    inp.buckshotCount = newBuck;
                    changed = true;
                }
                GUILayout.EndHorizontal();
            }

            changed |= TierSlider("Тир ПЭ", ref inp.damageElementTier);

            float minPart = AmmoCalc.GetMinPartKg(previewMass);
            float maxDe = Mathf.Max(minPart, previewMass - minPart - Mathf.Max(inp.explosiveMassKg, minPart));

            changed |= FloatField("Масса ПЭ (кг)", ref sDEMass, out float deM);
            inp.damageElementMassKg = Mathf.Max(deM, minPart);
            if (inp.damageElementMassKg > maxDe) inp.damageElementMassKg = maxDe;
            ValueLine($"Допустимо: {minPart:F3} .. {maxDe:F3} кг");
        }

        // Область поражения
        GroupHeader("Область поражения");
        DrawAreaSelector(inp, ref changed);

        // Взрыватель
        if (inp.chargeType != AmmoCalc.ChargeType.FM)
        {
            GroupHeader("Взрыватель");
            int fuzeIdx = (int)inp.fuzeType;
            int newFuze = GUILayout.SelectionGrid(fuzeIdx, FuzeNames, 3);
            if (newFuze != fuzeIdx)
            {
                inp.fuzeType = (AmmoCalc.FuzeType)newFuze;
                changed = true;
            }
        }

        // Толкающий заряд
        GroupHeader("Толкающий заряд");
        changed |= TierSlider("Тир толк. заряда", ref inp.propellantTier);
        changed |= FloatField("Масса толк. заряда (кг)", ref sPropMass, out float propM);
        inp.propellantMassKg = Mathf.Max(propM, 0.001f);

        // Гильза
        GroupHeader("Гильза");
        changed |= TierSlider("Тир гильзы", ref inp.caseTier);
        changed |= FloatField("Масса гильзы (кг)", ref sCaseMass, out float caseM);
        inp.caseMassKg = Mathf.Max(caseM, 0.001f);

        // Количество
        GroupHeader("Изготовление");
        changed |= IntField("Количество", ref sCraftCount, out int count);
        inp.craftCount = Mathf.Max(count, 1);

        // Ствол
        GroupHeader("Параметры ствола");
        changed |= FloatField("Длина ствола (мм)", ref sBarrelLen, out float bLen);
        workbench.barrelInput.barrelLengthMm = Mathf.Max(1f, bLen);

        changed |= FloatField("Диаметр ствола (мм)", ref sBarrelDiam, out float bDiam);
        workbench.barrelInput.barrelDiameterMm = Mathf.Max(1f, bDiam);

        GUILayout.EndVertical();

        GUILayout.Space(10);

        // ===================== ПРАВАЯ КОЛОНКА =====================
        GUILayout.BeginVertical(sectionStyle, GUILayout.Width((panelW - 60f) * 0.5f));

        SectionHeader("Вычисляемые параметры");

        if (changed)
        {
            workbench.Recalculate();
            PullBuffersFromInput();
        }

        var o = workbench.Output;
        var b = workbench.BarrelOutput;

        if (o != null && string.IsNullOrEmpty(o.error))
        {
            GroupHeader("Снаряд");
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
                    ValueLine($"Картечин: {o.buckshotCount}");
            }

            ValueLine($"Радиус поражения: {o.damageRadius:F3} м");
            ValueLine($"Пробитие в области: {o.areaPenetration:F3}");
            ValueLine($"Урон в области: {o.areaDamage:F3}");
            if (o.areaType == AmmoCalc.AreaType.Cone)
                ValueLine($"Угол конуса: {o.coneAngleDeg:F3}°");

            ValueLine($"Сила выталкивания: {o.propulsionForce:F3}");
            ValueLine($"Прочность гильзы: {o.caseStrength:F3}");
            ValueLine($"Масса выстрела: {o.totalShotMassKg:F3} кг");

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
                ValueLine($"Макс. дальность: {b.maxRange:F3} м");
                ValueLine($"Дальность прямого выстрела: {b.directFireRange:F3} м");
                ValueLine($"Прямой урон: {b.directDamage:F3}");
                ValueLine($"Прямое пробитие: {b.directPenetration:F3}");
            }
            else
            {
                WarningLine("неверные параметры ствола");
                WarningLine("Длина ствола >= длины снаряда");
                WarningLine("Диаметр ствола от диаметра снаряда до 1.25 диаметра");
            }
        }
        else
        {
            if (o != null && !string.IsNullOrEmpty(o.error))
                ErrorLine(o.error);
        }

        if (!string.IsNullOrEmpty(workbench.CraftError))
        {
            GUILayout.Space(8);
            WarningLine(workbench.CraftError);
        }

        GUILayout.EndVertical();

        GUILayout.EndHorizontal();

        GUILayout.Space(10);

        GUILayout.BeginHorizontal();
        GUI.enabled = workbench.CanCraft;
        if (GUILayout.Button("ИЗГОТОВИТЬ", GUILayout.Height(36)))
        {
            workbench.TryCraft();
        }
        GUI.enabled = true;

        if (GUILayout.Button("Сброс", GUILayout.Width(120), GUILayout.Height(36)))
        {
            workbench.ResetToDefaults();
            PullBuffersFromInput();
        }
        GUILayout.EndHorizontal();

        GUILayout.EndScrollView();
        GUILayout.EndArea();
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
                    inp.chargeType = type;
                    changed = true;
                }
            }

            GUI.enabled = prevEnabled;
        }

        GUILayout.EndHorizontal();

        if (!AmmoCalc.IsChargeTypeAllowed(AmmoCalc.ChargeType.HE, previewMass))
            WarningLine("HE доступен при массе снаряда от 0.100 кг");
        if (!AmmoCalc.IsChargeTypeAllowed(AmmoCalc.ChargeType.EQ, previewMass))
            WarningLine("EQ доступен при массе снаряда от 0.300 кг");
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
                    inp.damageElementType = DEValues[i];
                    changed = true;
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
                    inp.areaType = area;
                    changed = true;
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

    private bool FloatField(string label, ref string buffer, out float value)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, GUILayout.Width(220));
        string newBuffer = GUILayout.TextField(buffer, GUILayout.Width(130));
        GUILayout.EndHorizontal();

        bool changed = newBuffer != buffer;
        buffer = newBuffer;

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
        sDiam = inp.diameterMm.ToString("0.##", CultureInfo.InvariantCulture).Replace('.', ',');
        sLen = Mathf.CeilToInt(inp.lengthMm).ToString();
        sExpMass = inp.explosiveMassKg.ToString("0.###", CultureInfo.InvariantCulture).Replace('.', ',');
        sDEMass = inp.damageElementMassKg.ToString("0.###", CultureInfo.InvariantCulture).Replace('.', ',');
        sPropMass = inp.propellantMassKg.ToString("0.###", CultureInfo.InvariantCulture).Replace('.', ',');
        sCaseMass = inp.caseMassKg.ToString("0.###", CultureInfo.InvariantCulture).Replace('.', ',');
        sCraftCount = inp.craftCount.ToString();
        sBarrelLen = workbench.barrelInput.barrelLengthMm.ToString("0.###", CultureInfo.InvariantCulture).Replace('.', ',');
        sBarrelDiam = workbench.barrelInput.barrelDiameterMm.ToString("0.###", CultureInfo.InvariantCulture).Replace('.', ',');
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