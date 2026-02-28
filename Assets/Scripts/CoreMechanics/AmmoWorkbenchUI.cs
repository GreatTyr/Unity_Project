// AmmoWorkbenchUI.cs
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// OnGUI интерфейс верстака конических снарядов.
/// Управляется через AmmoWorkbench.
/// Включается/выключается извне (TempAmmoWorkbenchInteraction).
/// </summary>
[RequireComponent(typeof(AmmoWorkbench))]
public class AmmoWorkbenchUI : MonoBehaviour
{
    private AmmoWorkbench workbench;
    private Vector2 scrollPos;
    private bool showBarrelSection = false;

    // Строковые буферы для полей ввода
    private string sDiam = "10";
    private string sLen = "20";
    private string sExpMass = "0";
    private string sDEMass = "0";
    private string sPropMass = "0,001";
    private string sCaseMass = "0,001";
    private string sCraftCount = "1";
    private string sBarrelLen = "100";
    private string sBarrelDiam = "10";

    private static readonly string[] DENames =
    {
        "Нет", "Осколки(HE)", "Картечь", "Дробь", "Огонь", "Химия", "Энергия"
    };

    private static readonly string[] AreaNames =
    {
        "Нет", "Точка(P)", "Сфера(Sp)", "Конус(Cn)", "Облако(Cl)"
    };

    private static readonly string[] FuzeNames =
    {
        "Нет(No)", "Контакт(Ct)", "Таймер(Tm)", "Высота(Alt)", "Сейсм.(Se)", "Дист.(Re)"
    };

    private void Awake()
    {
        workbench = GetComponent<AmmoWorkbench>();
    }

    private void OnGUI()
    {
        float panelW = 450f;
        float panelH = Screen.height - 40f;

        GUILayout.BeginArea(new Rect(10, 10, panelW, panelH));
        scrollPos = GUILayout.BeginScrollView(scrollPos);

        GUILayout.Label("══════ ВЕРСТАК КОНИЧЕСКИХ СНАРЯДОВ ══════",
                         GUI.skin.box);
        GUILayout.Space(4);

        var inp = workbench.ammoInput;

        // ────── ОБОЛОЧКА ──────
        SectionHeader("Оболочка");
        inp.shellTier = TierSlider("Тир оболочки", inp.shellTier);
        sDiam = FloatField("Диаметр (мм)", sDiam, out float diam);
        inp.diameterMm = Mathf.Clamp(diam, 1f, 100000f);
        sLen = FloatField("Длина (мм)", sLen, out float len);
        inp.lengthMm = Mathf.Clamp(len, inp.diameterMm * 2f, 1000000f);

        // Показываем вычисленную массу
        float previewMass = AmmoCalc.Ceil3(AmmoCalc.CylinderMassKg(inp.diameterMm, inp.lengthMm));
        InfoLabel($"Расч. масса снаряда: {previewMass:F3} кг");

        // ────── РАЗРЫВНОЙ ЗАРЯД ──────
        SectionHeader("Разрывной заряд");
        inp.explosiveTier = TierSlider("Тир заряда (0=нет)", inp.explosiveTier, true);
        sExpMass = FloatField("Масса заряда (кг)", sExpMass, out float expM);
        inp.explosiveMassKg = Mathf.Max(expM, 0f);

        // ────── ПОРАЖАЮЩИЙ ЭЛЕМЕНТ ──────
        SectionHeader("Поражающий элемент");
        int deIdx = (int)inp.damageElementType;
        deIdx = GUILayout.SelectionGrid(deIdx, DENames, 4);
        inp.damageElementType = (AmmoCalc.DamageElementType)deIdx;

        if (inp.damageElementType == AmmoCalc.DamageElementType.Buckshot)
        {
            GUILayout.BeginHorizontal();
            string buckLabel = inp.buckshotCount >= 11 ? "много" : inp.buckshotCount.ToString();
            GUILayout.Label($"Картечин: {buckLabel}", GUILayout.Width(200));
            inp.buckshotCount = (int)GUILayout.HorizontalSlider(inp.buckshotCount, 2, 11);
            GUILayout.EndHorizontal();
        }

        inp.damageElementTier = TierSlider("Тир ПЭ (0=нет)", inp.damageElementTier, true);
        sDEMass = FloatField("Масса ПЭ (кг)", sDEMass, out float deM);
        inp.damageElementMassKg = Mathf.Max(deM, 0f);

        // ────── ОБЛАСТЬ ПОРАЖЕНИЯ ──────
        SectionHeader("Область поражения");
        int areaIdx = (int)inp.areaType;
        areaIdx = GUILayout.SelectionGrid(areaIdx, AreaNames, 5);
        inp.areaType = (AmmoCalc.AreaType)areaIdx;

        // ────── ВЗРЫВАТЕЛЬ ──────
        SectionHeader("Взрыватель");
        int fuzeIdx = (int)inp.fuzeType;
        fuzeIdx = GUILayout.SelectionGrid(fuzeIdx, FuzeNames, 3);
        inp.fuzeType = (AmmoCalc.FuzeType)fuzeIdx;

        // ────── ТОЛКАЮЩИЙ ЗАРЯД ──────
        SectionHeader("Толкающий заряд");
        inp.propellantTier = TierSlider("Тир толк. заряда", inp.propellantTier);
        sPropMass = FloatField("Масса толк. заряда (кг)", sPropMass, out float propM);
        inp.propellantMassKg = Mathf.Max(propM, 0.001f);

        // ────── ГИЛЬЗА ──────
        SectionHeader("Гильза");
        inp.caseTier = TierSlider("Тир гильзы", inp.caseTier);
        sCaseMass = FloatField("Масса гильзы (кг)", sCaseMass, out float caseM);
        inp.caseMassKg = Mathf.Max(caseM, 0.001f);

        // ────── КОЛИЧЕСТВО ──────
        SectionHeader("Крафт");
        sCraftCount = FloatField("Количество", sCraftCount, out float cntF);
        inp.craftCount = Mathf.Max((int)cntF, 1);

        // ────── ПЕРЕСЧЁТ ──────
        GUILayout.Space(6);
        if (GUILayout.Button("▶ Пересчитать", GUILayout.Height(28)))
        {
            workbench.Recalculate();
        }

        // ────── РЕЗУЛЬТАТЫ ──────
        GUILayout.Space(8);
        var o = workbench.Output;
        if (o != null)
        {
            GUILayout.Label("══════ РЕЗУЛЬТАТ ══════", GUI.skin.box);

            if (!string.IsNullOrEmpty(o.error))
            {
                ErrorLabel(o.error);
            }
            else
            {
                InfoLabel($"Тип заряда: {o.chargeType}");
                InfoLabel($"Масса снаряда: {o.totalProjectileMassKg:F3} кг");
                InfoLabel($"Масса оболочки: {o.shellMassKg:F3} кг");
                InfoLabel($"Прочность оболочки: {o.shellStrength:F3}");

                if (o.chargeType != AmmoCalc.ChargeType.FM)
                {
                    InfoLabel($"Мощность заряда: {o.explosivePower:F3}");
                    InfoLabel($"Радиус поражения: {o.damageRadius:F3} м");
                    InfoLabel($"Пробитие в радиусе: {o.areaPenetration:F3}");
                    InfoLabel($"Повреждение в радиусе: {o.areaDamage:F3}");
                    if (o.areaType == AmmoCalc.AreaType.Cone)
                        InfoLabel($"Угол конуса: {o.coneAngleDeg:F3}°");
                }

                InfoLabel($"Сила выталкивания: {o.propulsionForce:F3}");
                InfoLabel($"Прочность гильзы: {o.caseStrength:F3}");
                InfoLabel($"Масса выстрела: {o.totalShotMassKg:F3} кг");

                GUILayout.Space(4);
                GUILayout.Label($"Код: {o.ammoCode}", GUI.skin.textField);

                // ────── СТОИМОСТЬ ──────
                GUILayout.Space(4);
                SectionHeader($"Стоимость (×{inp.craftCount})");
                var costs = workbench.Costs;
                if (costs != null)
                {
                    foreach (var c in costs)
                    {
                        if (c.isEnergy)
                        {
                            long total = c.amountEnergy * inp.craftCount;
                            InfoLabel($"  Энергия: {total} ед.");
                        }
                        else
                        {
                            var ri = AmmoCalc.GetResourceIndex(c.resourceType, c.tier);
                            string rName = ResourcesStorage.ResourceName(ri);
                            float totalKg = AmmoCalc.Ceil3(c.amountKg * inp.craftCount);
                            InfoLabel($"  {rName}: {totalKg:F3} кг");
                        }
                    }
                }

                // ────── ДОПОЛНИТЕЛЬНАЯ ОЦЕНКА (СТВОЛ) ──────
                GUILayout.Space(6);
                showBarrelSection = GUILayout.Toggle(showBarrelSection,
                    "▼ Дополнительная оценка (ствол)");

                if (showBarrelSection)
                {
                    SectionHeader("Параметры ствола");

                    sBarrelLen = FloatField("Длина ствола (мм)", sBarrelLen, out float bLen);
                    workbench.barrelInput.barrelLengthMm =
                        Mathf.Clamp(bLen, inp.lengthMm, 1000000f);

                    float maxBD = Mathf.Floor(inp.diameterMm * 1.25f);
                    if (maxBD < inp.diameterMm) maxBD = inp.diameterMm;
                    sBarrelDiam = FloatField(
                        $"Диаметр ствола (мм) [{inp.diameterMm:F0}–{maxBD:F0}]",
                        sBarrelDiam, out float bDiam);
                    workbench.barrelInput.barrelDiameterMm =
                        Mathf.Clamp(bDiam, inp.diameterMm, maxBD);

                    if (GUILayout.Button("Пересчитать оценку"))
                    {
                        workbench.Recalculate();
                    }

                    var b = workbench.BarrelOutput;
                    if (b != null)
                    {
                        SectionHeader("Результат оценки");
                        InfoLabel($"Скорость снаряда: {b.projectileSpeed:F3} м/с");
                        InfoLabel($"Точность (отклонение): {b.accuracy:F6}°");
                        InfoLabel($"Макс. дальность: {b.maxRange:F3} м");
                        InfoLabel($"Дальность прямого выстрела: {b.directFireRange:F3} м");
                        InfoLabel($"Прямой урон: {b.directDamage:F3}");
                        InfoLabel($"Прямое пробитие: {b.directPenetration:F3}");
                    }
                }
            }
        }

        // ────── КНОПКА КРАФТА ──────
        GUILayout.Space(12);

        string err = workbench.CraftError;
        bool craft = workbench.CanCraft;

        if (!craft && !string.IsNullOrEmpty(err))
        {
            WarningLabel(err);
        }

        GUI.enabled = craft;
        if (GUILayout.Button("═══ КРАФТ ═══", GUILayout.Height(40)))
        {
            if (workbench.TryCraft())
            {
                Debug.Log("[AmmoWorkbenchUI] Крафт выполнен успешно!");
            }
        }
        GUI.enabled = true;

        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    // ===================== UI ХЕЛПЕРЫ =====================

    private int TierSlider(string label, int current, bool allowZero = false)
    {
        int min = allowZero ? 0 : 1;
        GUILayout.BeginHorizontal();
        GUILayout.Label($"{label}: {current}", GUILayout.Width(220));
        current = Mathf.RoundToInt(GUILayout.HorizontalSlider(current, min, 10));
        GUILayout.EndHorizontal();
        return current;
    }

    private string FloatField(string label, string buffer, out float value)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, GUILayout.Width(220));
        buffer = GUILayout.TextField(buffer, GUILayout.Width(120));
        GUILayout.EndHorizontal();

        string normalized = buffer.Replace(',', '.');
        if (!float.TryParse(normalized,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out value))
        {
            value = 0f;
        }
        return buffer;
    }

    private void SectionHeader(string text)
    {
        GUILayout.Space(4);
        GUILayout.Label($"── {text} ──", GUI.skin.box);
    }

    private void InfoLabel(string text)
    {
        GUILayout.Label(text);
    }

    private void ErrorLabel(string text)
    {
        Color prev = GUI.color;
        GUI.color = Color.red;
        GUILayout.Label($"✖ {text}");
        GUI.color = prev;
    }

    private void WarningLabel(string text)
    {
        Color prev = GUI.color;
        GUI.color = Color.yellow;
        GUILayout.Label($"⚠ {text}");
        GUI.color = prev;
    }
}