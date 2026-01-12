using UnityEngine;

public enum InteractionType
{
    Simple,         // просто одно действие по F
    Menu,           // открывается InteractionMenuUI
    VehicleEnter,   // «Сесть за штурвал/в транспорт»
    ScenePortal     // переход в другую сцену
}
