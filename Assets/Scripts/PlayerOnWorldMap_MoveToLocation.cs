using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class PlayerOnWorldMap_MoveToLocation : MonoBehaviour
{
    public GameObject enterButton; // UI-кнопка
    private string targetSceneName = null;
    private Transform targetLocation = null;
    private Camera mainCamera;

    private void Start()
    {
        mainCamera = Camera.main;
        enterButton.SetActive(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        LocationData location = other.GetComponent<LocationData>();
        if (location != null)
        {
            targetSceneName = location.sceneName;
            targetLocation = other.transform;
            enterButton.SetActive(true);
            UpdateButtonPosition();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        LocationData location = other.GetComponent<LocationData>();
        if (location != null)
        {
            targetSceneName = null;
            targetLocation = null;
            enterButton.SetActive(false);
        }
    }

    private void Update()
    {
        if (enterButton.activeSelf && targetLocation != null)
        {
            UpdateButtonPosition();
        }
    }

    private void UpdateButtonPosition()
    {
        Vector3 screenPos = mainCamera.WorldToScreenPoint(targetLocation.position);
        enterButton.transform.position = screenPos + new Vector3(0, -50, 0); // Смещение вниз, если нужно
    }

    public void EnterLocation()
    {
        if (!string.IsNullOrEmpty(targetSceneName))
        {
            SceneManager.LoadScene(targetSceneName);
        }
    }
}