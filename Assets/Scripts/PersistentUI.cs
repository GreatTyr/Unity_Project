using UnityEngine;

public class PersistentUI : MonoBehaviour
{
    public static PersistentUI Instance { get; private set; }

    [Header("—сылки на UI панели")]
    [SerializeField] private FurnaceUI furnaceUI;

    public FurnaceUI FurnaceUI => furnaceUI;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }
}