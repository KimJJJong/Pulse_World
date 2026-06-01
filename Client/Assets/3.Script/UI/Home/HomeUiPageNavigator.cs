using UnityEngine;
using UnityEngine.UI;

public sealed class HomeUiPageNavigator : MonoBehaviour
{
    [SerializeField] private GameObject _homeRoot;
    [SerializeField] private GameObject _equipmentRoot;
    [SerializeField] private GameObject _inventoryRoot;
    [SerializeField] private GameObject _appearanceRoot;
    [SerializeField] private GameObject _mapRoot;
    [SerializeField] private GameObject _detailRoot;
    [SerializeField] private Button _equipmentButton;
    [SerializeField] private Button _inventoryButton;
    [SerializeField] private Button _appearanceButton;
    [SerializeField] private Button _mapButton;
    [SerializeField] private Button _equipmentBackButton;
    [SerializeField] private Button[] _homeButtons;
    [SerializeField] private HomeSceneCameraDirector _cameraDirector;
    [SerializeField] private bool _forceCameraPresentation;
    [SerializeField] private float _forcedPresentationScreenLeftOffset = 1.65f;
    [SerializeField] private float _appearancePresentationScreenLeftOffset = 1.65f;

    private HomePage _currentPage = HomePage.Home;

    private enum HomePage
    {
        Home,
        Equipment,
        Inventory,
        Appearance,
        Map
    }

    private void Awake()
    {
        Bind(_equipmentButton, ShowEquipment);
        Bind(_inventoryButton, ShowInventory);
        Bind(_appearanceButton, ShowAppearance);
        Bind(_mapButton, ShowMap);
        Bind(_equipmentBackButton, ShowHome);

        if (_homeButtons != null)
        {
            foreach (var button in _homeButtons)
                Bind(button, ShowHome);
        }
    }

    private void Start()
    {
        ShowHome();
    }

    public void ShowHome()
    {
        Show(HomePage.Home);
    }

    public void ShowEquipment()
    {
        Show(HomePage.Equipment);
    }

    public void ShowInventory()
    {
        Show(HomePage.Inventory);
    }

    public void ShowAppearance()
    {
        Show(HomePage.Appearance);
    }

    public void ShowMap()
    {
        Show(HomePage.Map);
    }

    public void SetForceCameraPresentation(bool force)
    {
        _forceCameraPresentation = force;
        UpdateCameraPresentation(_currentPage);
    }

    private void Show(HomePage page)
    {
        _currentPage = page;
        SetActive(_homeRoot, page == HomePage.Home);
        SetActive(_equipmentRoot, page == HomePage.Equipment);
        SetActive(_inventoryRoot, page == HomePage.Inventory);
        SetActive(_appearanceRoot, page == HomePage.Appearance);
        SetActive(_mapRoot, page == HomePage.Map);
        SetActive(_detailRoot, false);

        UpdateCameraPresentation(page);
    }

    private void UpdateCameraPresentation(HomePage page)
    {
        if (_cameraDirector == null)
            return;

        var active = _forceCameraPresentation || page == HomePage.Appearance;
        if (active)
        {
            var offset = page == HomePage.Appearance
                ? _appearancePresentationScreenLeftOffset
                : _forcedPresentationScreenLeftOffset;
            _cameraDirector.SetModelScreenLeftOffset(offset);
        }

        _cameraDirector.SetAppearancePresentation(active);
    }

    private static void Bind(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null || action == null)
            return;

        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
    }

    private static void SetActive(GameObject target, bool active)
    {
        if (target != null && target.activeSelf != active)
            target.SetActive(active);
    }
}
