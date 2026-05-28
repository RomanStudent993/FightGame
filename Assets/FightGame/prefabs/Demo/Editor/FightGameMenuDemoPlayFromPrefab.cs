#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Play на префабе FightGame_MenuDemo открывает сцену меню, а не последнюю активную (например EducationDemo).
/// </summary>
[InitializeOnLoad]
static class FightGameMenuDemoPlayFromPrefab
{
    const string MenuDemoScenePath = "Assets/FightGame/prefabs/Demo/FightGame_MenuDemo.unity";
    const string PrefabPath = "Assets/FightGame/prefabs/Demo/FightGame_MenuDemo.prefab";

    static FightGameMenuDemoPlayFromPrefab()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.ExitingEditMode)
            return;

        if (!IsMenuDemoPrefabSelected())
            return;

        if (EditorSceneManager.GetActiveScene().path == MenuDemoScenePath)
            return;

        if (!System.IO.File.Exists(MenuDemoScenePath))
            return;

        EditorSceneManager.OpenScene(MenuDemoScenePath, OpenSceneMode.Single);
    }

    static bool IsMenuDemoPrefabSelected()
    {
        Object active = Selection.activeObject;
        if (active == null)
            return false;

        string path = AssetDatabase.GetAssetPath(active);
        if (path == PrefabPath)
            return true;

        if (active is GameObject go && PrefabUtility.IsPartOfPrefabAsset(go))
        {
            string assetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go);
            return assetPath == PrefabPath;
        }

        return false;
    }
}
#endif
