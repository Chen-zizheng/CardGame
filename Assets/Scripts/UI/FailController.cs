using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 失败/结算场景里的那一屏，现在统一由 ResultScreen 现搭（Assets/Scripts/UI/ResultScreen.cs）。
// 这个组件仍然留在场景上，只负责"保证进场景时那一屏会被搭出来"，不再自己摆按钮——
// 之前它靠 Inspector 里拖的两个 Button，一旦漏拖就只剩一块空屏幕。
public class FailController : MonoBehaviour
{
    [SerializeField] private Button retryButton;
    [SerializeField] private Button mainMenuButton;

    void Start()
    {
        ResultScreen.Apply(SceneManager.GetActiveScene().name);

        // 老场景的按钮若还接着，就顺手停用，避免和新按钮重叠
        if (retryButton != null) retryButton.gameObject.SetActive(false);
        if (mainMenuButton != null) mainMenuButton.gameObject.SetActive(false);
    }
}
