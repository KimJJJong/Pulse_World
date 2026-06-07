using System;
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
    [SerializeField] private bool _mapOnlyMode;
    [SerializeField] private float _forcedPresentationScreenLeftOffset = 1.65f;
    [SerializeField] private float _appearancePresentationScreenLeftOffset = 1.65f;

    private HomePage _currentPage = HomePage.Home;
    private bool IsMapOnlyMode => _mapOnlyMode
                                  || string.Equals(gameObject.scene.name, SceneNames.WorldMap, StringComparison.OrdinalIgnoreCase);

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
        ResolveCameraDirector();

        if (IsMapOnlyMode)
        {
            SetHomeButtonsActive(false);
            SetActive(_homeRoot, false);
            SetActive(_equipmentRoot, false);
            SetActive(_inventoryRoot, false);
            SetActive(_appearanceRoot, false);
            SetActive(_detailRoot, false);
            return;
        }

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
        if (IsMapOnlyMode)
        {
            ShowMap();
            WorldMapEntryOverlay.Play(GetComponentInParent<Canvas>());
        }
        else
        {
            ShowHome();
        }
    }

    public void ShowHome()
    {
        if (IsMapOnlyMode)
        {
            ShowMap();
            return;
        }

        Show(HomePage.Home);
    }

    public void ShowEquipment()
    {
        if (IsMapOnlyMode)
        {
            ShowMap();
            return;
        }

        Show(HomePage.Equipment);
    }

    public void ShowInventory()
    {
        if (IsMapOnlyMode)
        {
            ShowMap();
            return;
        }

        Show(HomePage.Inventory);
    }

    public void ShowAppearance()
    {
        if (IsMapOnlyMode)
        {
            ShowMap();
            return;
        }

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

    public void SetCameraDirector(HomeSceneCameraDirector cameraDirector)
    {
        if (cameraDirector == null)
            return;

        _cameraDirector = cameraDirector;
    }

    private void Show(HomePage page)
    {
        if (IsMapOnlyMode && page != HomePage.Map)
            page = HomePage.Map;

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
        ResolveCameraDirector();
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

    private void ResolveCameraDirector()
    {
        if (_cameraDirector != null)
            return;

        _cameraDirector = GetComponent<HomeSceneCameraDirector>();
        if (_cameraDirector != null)
            return;

        _cameraDirector = GetComponentInParent<HomeSceneCameraDirector>(true);
        if (_cameraDirector != null)
            return;

        var directors = Resources.FindObjectsOfTypeAll<HomeSceneCameraDirector>();
        foreach (var director in directors)
        {
            if (director == null || !director.gameObject.scene.IsValid())
                continue;

            if (director.gameObject.scene == gameObject.scene)
            {
                _cameraDirector = director;
                return;
            }
        }
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

    private void SetHomeButtonsActive(bool active)
    {
        if (_homeButtons == null)
            return;

        foreach (var button in _homeButtons)
        {
            if (button != null)
                SetActive(button.gameObject, active);
        }
    }
}
