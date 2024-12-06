
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using nadena.dev.modular_avatar.core;
using System.Reflection;
using System;
using System.IO;
using VRC.SDK3.Avatars.Components;
using UnityEditor.Animations;

public class VoyageAvatarMAEditorHelpers
{
    public static string ScriptDirPath
    {
        get
        {
            var g = AssetDatabase.FindAssets($"t:Script {nameof(VoyageAvatarMAEditorHelpers)}");
            return Path.GetDirectoryName(AssetDatabase.GUIDToAssetPath(g[0]));
        }
    }

    public static string menuInstallTargetPrefabPath = $"{ScriptDirPath}/menuInstallTarget.prefab";

    private static bool TryGetActiveFolderPath(out string path)
    {
        var _tryGetActiveFolderPath = typeof(ProjectWindowUtil).GetMethod("TryGetActiveFolderPath", BindingFlags.Static | BindingFlags.NonPublic);

        object[] args = new object[] { null };
        bool found = (bool)_tryGetActiveFolderPath.Invoke(null, args);
        path = (string)args[0];

        return found;
    }

    /**
     * <summary>Convert the provided name to a useable filename.</summary>
     * 
     * <remarks>This mostly remove all characters deemed "invalid" in filenames.</remarks>
     * 
     * <param name="name">The name to convert</param>
     * 
     * <returns>The provided name with invalid characters replaced by underscores</returns>
     */
    public static string FilesystemFriendlyName(string name)
    {
        var invalids = System.IO.Path.GetInvalidFileNameChars();
        return string.Join("_", name.Split(invalids, StringSplitOptions.RemoveEmptyEntries)).TrimEnd('.');
    }

    public static Transform FindAvatarRoot(Transform itemTransform)
    {
        if (itemTransform == null) { return null; }
        if (itemTransform.GetComponent<VRCAvatarDescriptor>() != null) { return itemTransform; }
        else return FindAvatarRoot(itemTransform.parent);
    }

    const string quickToggleMenuGoName = "QuickToggle-MA-Menu";
    const string quickToggleTag = "QuickToggle-Tag";
    

    static Transform PrepareMenuOnAvatar(GameObject firstItem)
    {
        Transform avatar = FindAvatarRoot(firstItem.transform);
        if (avatar == null)
        {
            Debug.Log("No avatar !");
            return null;
        }
        Transform quickToggleTransform = avatar.Find(quickToggleMenuGoName);

        GameObject quickToggleObject;
        if (quickToggleTransform == null)
        {
            quickToggleObject = new GameObject(quickToggleMenuGoName);
            quickToggleTransform = quickToggleObject.transform;
            quickToggleTransform.parent = avatar;
        }

        Transform togglesMenuTransform = null;
        quickToggleObject = quickToggleTransform.gameObject;
        for (int childIndex = 0; childIndex < quickToggleObject.transform.childCount; childIndex++)
        {
            Transform child = quickToggleTransform.GetChild(childIndex);
            if (child.name != "Toggles") continue;
            togglesMenuTransform = child;
        }

        if (togglesMenuTransform == null)
        {
            GameObject go = new GameObject("Toggles");
            togglesMenuTransform = go.transform;
            togglesMenuTransform.parent = quickToggleTransform;
        }

        ModularAvatarMenuItem modularAvatarMenu = togglesMenuTransform.GetComponent<ModularAvatarMenuItem>();
        if (modularAvatarMenu == null)
        {
            modularAvatarMenu = togglesMenuTransform.gameObject.AddComponent<ModularAvatarMenuItem>();
        }
        modularAvatarMenu.Control.type = VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionsMenu.Control.ControlType.SubMenu;
        modularAvatarMenu.MenuSource = SubmenuSource.Children;

        ModularAvatarMenuInstaller modularAvatarMenuInstaller = togglesMenuTransform.GetComponent<ModularAvatarMenuInstaller>();
        if (modularAvatarMenuInstaller == null)
        {
            modularAvatarMenuInstaller = togglesMenuTransform.gameObject.AddComponent<ModularAvatarMenuInstaller>();
        }

        

        return togglesMenuTransform;

    }
    public static void AddItemToModularAvatarMenu(
        Transform modularAvatarMenuRoot,
        string itemName,
        ModularAvatarMenuInstaller targetInstaller)
    {

        GameObject menuInstallerTarget = AssetDatabase.LoadAssetAtPath<GameObject>(menuInstallTargetPrefabPath);
        GameObject prefabInstance = GameObject.Instantiate(menuInstallerTarget, modularAvatarMenuRoot);
        prefabInstance.name = itemName;

        Component[] components = prefabInstance.GetComponents<Component>();

        foreach (var component in components)
        {

            string componentTypeName = component.GetType().ToString();
            bool isMenuInstallerTarget = componentTypeName == "nadena.dev.modular_avatar.core.ModularAvatarMenuInstallTarget";

            if (!isMenuInstallerTarget) { continue; }

            Debug.Log(componentTypeName);

            component.GetType().GetField("installer").SetValue(component, targetInstaller);
        }
    }

    public static void ControllerAddLayer(
        AnimatorController controller,
        AnimatorStateMachine stateMachine)
    {
        AnimatorControllerLayer layer = new AnimatorControllerLayer();
        layer.stateMachine = stateMachine;
        layer.name = stateMachine.name;
        layer.defaultWeight = 1;

        AssetDatabase.AddObjectToAsset(layer.stateMachine, controller);
        foreach (var graphicalState in stateMachine.states)
        {
            var state = graphicalState.state;

            AssetDatabase.AddObjectToAsset(state, controller);
            foreach (var transition in state.transitions)
            {
                AssetDatabase.AddObjectToAsset(transition, controller);
            }
        }

        controller.AddLayer(layer);
    }

    public static AnimatorStateTransition MakeInstant(AnimatorStateTransition transition)
    {
        transition.exitTime = 0;
        transition.hasExitTime = false;
        transition.duration = 0;
        return transition;
    }

        [MenuItem("GameObject/Voyage/Invididual toggle (Modular Avatar)")]
    public static void ToggleThroughMenuMa(MenuCommand menuCommand)
    {
        if (menuCommand.context != Selection.activeGameObject) { return; }

        GameObject[] gameObjects = Selection.gameObjects;
        if (gameObjects.Length == 0) { return; }

        TryGetActiveFolderPath(out string outFolderPath);
        var assetsFolder = Application.dataPath;


        string saveDirPath = EditorUtility.SaveFolderPanel("Where to save the animations", outFolderPath, "");
        if (saveDirPath.Length == 0) { return; }
        if (!saveDirPath.StartsWith(assetsFolder))
        {
            return;
        }
        string assetsRelativePath = saveDirPath.Substring(assetsFolder.Length);
        if (assetsRelativePath.StartsWith('/')) assetsRelativePath += "/";

        string projectRelativePath = $"Assets/{assetsRelativePath}";

        Transform menuTransform = PrepareMenuOnAvatar(gameObjects[0]);

        if (menuTransform == null)
        {
            return;
        }

        foreach (var selectedGameObject in gameObjects)
        {
            if (selectedGameObject == null) return;

            string itemName = selectedGameObject.name;
            string itemFsFriendlyName = FilesystemFriendlyName(itemName.Replace(" ", "_"));
            string parameterName = $"toggle_{itemFsFriendlyName}";
            string stateMachineName = $"Toggle_{itemFsFriendlyName}";
            string currentTimeWithoutColons = DateTime.Now.ToString("s").Replace(":", "");



            AnimationClip offClip = new AnimationClip { name = $"{stateMachineName}-off", wrapMode = WrapMode.Once };
            AnimationClip onClip = new AnimationClip { name = $"{stateMachineName}-on", wrapMode = WrapMode.Once };

            offClip.SetCurve("", typeof(GameObject), "m_IsActive", AnimationCurve.Constant(0, 1 / 60f, 0));
            onClip.SetCurve("", typeof(GameObject), "m_IsActive", AnimationCurve.Constant(0, 1 / 60f, 1));

            AssetDatabase.CreateAsset(offClip, $"{projectRelativePath}/{offClip.name}-{currentTimeWithoutColons}.anim");
            AssetDatabase.CreateAsset(onClip, $"{projectRelativePath}/{onClip.name}-{currentTimeWithoutColons}.anim");

            AnimatorController generatedFxController = AnimatorController.CreateAnimatorControllerAtPath($"{projectRelativePath}/FX_Toggle_Item_{itemFsFriendlyName}-{currentTimeWithoutColons}.controller");
            generatedFxController.AddParameter(
                new AnimatorControllerParameter
                {
                    name = parameterName,
                    type = AnimatorControllerParameterType.Bool,
                    defaultBool = selectedGameObject.activeSelf
                }
            );

            AnimatorStateMachine stateMachine = new AnimatorStateMachine();
            stateMachine.name = $"Toggle_{itemFsFriendlyName}";

            var hideState = stateMachine.AddState("OFF");
            hideState.motion = offClip;
            hideState.writeDefaultValues = false;

            var showState = stateMachine.AddState("ON");
            showState.motion = onClip;
            showState.writeDefaultValues = false;

            if (selectedGameObject.activeSelf)
            {
                stateMachine.defaultState = showState;
                var toHideTransition = showState.AddTransition(hideState);
                MakeInstant(toHideTransition).AddCondition(AnimatorConditionMode.IfNot, 1, parameterName);

                var toExitTransition = hideState.AddExitTransition();
                MakeInstant(toExitTransition).AddCondition(AnimatorConditionMode.If, 1, parameterName);
            }
            else
            {
                stateMachine.defaultState = hideState;
                var toShowTransition = hideState.AddTransition(showState);
                MakeInstant(toShowTransition).AddCondition(AnimatorConditionMode.If, 1, parameterName);

                var toExitTransition = showState.AddExitTransition();
                MakeInstant(toExitTransition).AddCondition(AnimatorConditionMode.IfNot, 1, parameterName);
            }

            ControllerAddLayer(generatedFxController, stateMachine);

            EditorUtility.SetDirty(stateMachine);
            EditorUtility.SetDirty(generatedFxController);

            AssetDatabase.Refresh();

            var mergeAnimator = selectedGameObject.AddComponent<ModularAvatarMergeAnimator>();
            mergeAnimator.animator = generatedFxController;

            var avatarParameters = selectedGameObject.AddComponent<ModularAvatarParameters>();
            float defaultValue = selectedGameObject.activeSelf ? 1f : 0f;

            avatarParameters.parameters.Add(
                new ParameterConfig()
                {
                    defaultValue = defaultValue,
                    nameOrPrefix = parameterName,
                    saved = true,
                    syncType = ParameterSyncType.Bool,
                    internalParameter = true
                });

            var menuItemObject = new GameObject();
            menuItemObject.transform.parent = selectedGameObject.transform;
            menuItemObject.name = selectedGameObject.name;

            var menuItem = menuItemObject.AddComponent<ModularAvatarMenuItem>();
            menuItem.Control.type = VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionsMenu.Control.ControlType.Toggle;
            menuItem.Control.name = itemName;
            menuItem.Control.parameter = new VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionsMenu.Control.Parameter()
            {
                name = parameterName
            };

            var menuItemInstaller = menuItemObject.AddComponent<ModularAvatarMenuInstaller>();

            AddItemToModularAvatarMenu(menuTransform, selectedGameObject.name, menuItemInstaller);
            
        }

        Selection.activeGameObject = menuTransform.gameObject;


        

    }
 
}
#endif