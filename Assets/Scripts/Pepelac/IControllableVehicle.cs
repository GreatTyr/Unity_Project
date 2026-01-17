using System;
using UnityEngine;

/// <summary>
/// IControllableVehicle — интерфейс для "управляемых" транспортных средств.
/// Более конкретное и информативное имя, чем IVehicleController.
///
/// Контракт:
/// - EnableControl() / DisableControl() — включает/отключает локальное управление транспортом (ввод).
/// - IsControlEnabled — текущее состояние управления.
/// - Root — корневой Transform транспорта (используется для привязки игрока/seat).
/// - События OnControlEnabled / OnControlDisabled для подписчиков (камера, UI, звук).
///
/// Замечание:
/// - Реализация может быть монобихевиором (напр. PepelacController : MonoBehaviour, IControllableVehicle).
/// - Все публичные члены минималистичны, чтобы облегчить тестирование и подмену реализации.
/// </summary>
public interface IControllableVehicle
{
    // Включает управление транспортом (например, когда игрок садится).
    void EnableControl();

    // Выключает управление транспортом (например, когда игрок выходит).
    void DisableControl();

    // Возвращает, включено ли управление сейчас.
    bool IsControlEnabled { get; }

    // Корневой трансформ транспорта (для установки родителя игрока)
    Transform Root { get; }

    // События для подписчиков (необязательны для реализации, но рекомендуются)
    event Action OnControlEnabled;
    event Action OnControlDisabled;
}