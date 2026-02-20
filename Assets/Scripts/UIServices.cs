using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Простой Service Locator для UI-сервисов.
/// Заменяет 7+ статических Instance полей единой точкой доступа.
///
/// Использование:
///   Регистрация:   UIServices.Register(this);        // в Awake каждого сервиса
///   Снятие:        UIServices.Unregister(this);      // в OnDestroy
///   Получение:     UIServices.Get<CrosshairUI>()     // вместо CrosshairUI.Instance
///
/// При смене сцены вызвать UIServices.ClearAll() из bootstrap-скрипта.
/// </summary>
public static class UIServices
{
    private static readonly Dictionary<Type, object> services = new Dictionary<Type, object>();

    /// <summary>
    /// Зарегистрировать сервис. Если уже есть экземпляр такого типа — перезаписывает.
    /// </summary>
    public static void Register<T>(T service) where T : class
    {
        var type = typeof(T);

        if (services.ContainsKey(type))
        {
            Debug.LogWarning($"[UIServices] Перезапись сервиса {type.Name}");
        }

        services[type] = service;
    }

    /// <summary>
    /// Снять регистрацию сервиса (при OnDestroy).
    /// </summary>
    public static void Unregister<T>(T service) where T : class
    {
        var type = typeof(T);

        if (services.TryGetValue(type, out var existing) && ReferenceEquals(existing, service))
        {
            services.Remove(type);
        }
    }

    /// <summary>
    /// Получить сервис. Возвращает null если не зарегистрирован.
    /// </summary>
    public static T Get<T>() where T : class
    {
        if (services.TryGetValue(typeof(T), out var service))
            return service as T;

        return null;
    }

    /// <summary>
    /// Очистить все сервисы. Вызывать при смене сцены.
    /// </summary>
    public static void ClearAll()
    {
        services.Clear();
    }
}