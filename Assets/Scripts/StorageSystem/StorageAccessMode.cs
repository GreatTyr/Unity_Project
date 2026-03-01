using System;

[Flags]
public enum StorageAccessMode
{
    None = 0,
    Read = 1 << 0,
    Write = 1 << 1,
    TransferIn = 1 << 2,
    TransferOut = 1 << 3,
    CraftConsume = 1 << 4,
    CraftProduce = 1 << 5
}