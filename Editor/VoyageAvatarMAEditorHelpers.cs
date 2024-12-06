
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using nadena.dev.modular_avatar.core;
using System.Reflection;
using System;
using System.IO;
using VRC.SDK3.Avatars.Components;
using UnityEditor.Animations;
using UnityEngine.UIElements;

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
    

    static Transform PrepareMenuOnAvatar(GameObject item, out Transform avatarTransform)
    {

        Transform avatar = FindAvatarRoot(item.transform);
        avatarTransform = avatar;
        if (avatar == null)
        {
            return null;
        }
        Undo.RecordObject(avatar, "[QT] Quick Toggle");
        Transform quickToggleTransform = avatar.Find(quickToggleMenuGoName);

        GameObject quickToggleObject;
        if (quickToggleTransform == null)
        {
            quickToggleObject = new GameObject(quickToggleMenuGoName);
            Undo.RegisterCreatedObjectUndo(quickToggleObject, $"[QT] Create menu for {avatar.name}");
            Undo.RecordObject(quickToggleObject.transform, $"[QT] Set {item.name} onto the avatar");
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
            Undo.RegisterCreatedObjectUndo(go, $"[QT] Create a GameObject for {avatar.name} Toggles");
            Undo.RecordObject(go.transform, $"[QT] Setting Toggles as a child of the Quick Toggles menu object");
            togglesMenuTransform = go.transform;
            togglesMenuTransform.parent = quickToggleTransform;
        }

        GameObject togglesMenuObject = togglesMenuTransform.gameObject;

        ModularAvatarMenuItem modularAvatarMenu = togglesMenuObject.GetComponent<ModularAvatarMenuItem>();
        if (modularAvatarMenu == null)
        {

            modularAvatarMenu = Undo.AddComponent<ModularAvatarMenuItem>(togglesMenuObject);
        }
        modularAvatarMenu.Control.type = VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionsMenu.Control.ControlType.SubMenu;
        modularAvatarMenu.MenuSource = SubmenuSource.Children;

        ModularAvatarMenuInstaller modularAvatarMenuInstaller = togglesMenuObject.GetComponent<ModularAvatarMenuInstaller>();
        if (modularAvatarMenuInstaller == null)
        {
            modularAvatarMenuInstaller = Undo.AddComponent< ModularAvatarMenuInstaller>(togglesMenuObject);
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
        Undo.RegisterCreatedObjectUndo(prefabInstance, $"[QT] Quick Toggle");
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

    public static AnimatorController CreateToggleItemController(
        GameObject selectedGameObject,
        string projectRelativePath,
        string itemName,
        string parameterName,
        string suffix,
        string subFolderName)
    {

        string fsFriendlySubFolderName = FilesystemFriendlyName(subFolderName.Replace(" ", "_"));

        string generatedAssetsPath = projectRelativePath + '/' + fsFriendlySubFolderName;
        if (!AssetDatabase.IsValidFolder(generatedAssetsPath))
        {
            string createdFolderGuid = AssetDatabase.CreateFolder(projectRelativePath, fsFriendlySubFolderName);
            if (createdFolderGuid == "")
            {
                Debug.LogError($"Could not create subdirectory {generatedAssetsPath}");
                return null;
            }
            generatedAssetsPath = AssetDatabase.GUIDToAssetPath(createdFolderGuid);
        }



        string itemFsFriendlyName = FilesystemFriendlyName(itemName.Replace(" ", "_"));
        
        string stateMachineName = $"Toggle_{itemFsFriendlyName}";
        
        AnimationClip offClip = new AnimationClip { name = $"{stateMachineName}-off", wrapMode = WrapMode.Once };
        AnimationClip onClip = new AnimationClip { name = $"{stateMachineName}-on", wrapMode = WrapMode.Once };

        offClip.SetCurve(itemName, typeof(GameObject), "m_IsActive", AnimationCurve.Constant(0, 1 / 60f, 0));
        onClip.SetCurve(itemName, typeof(GameObject), "m_IsActive", AnimationCurve.Constant(0, 1 / 60f, 1));

        AssetDatabase.CreateAsset(offClip, $"{generatedAssetsPath}/{offClip.name}-{suffix}.anim");
        AssetDatabase.CreateAsset(onClip, $"{generatedAssetsPath}/{onClip.name}-{suffix}.anim");

        AnimatorController generatedFxController = AnimatorController.CreateAnimatorControllerAtPath($"{generatedAssetsPath}/FX_Toggle_Item_{itemFsFriendlyName}-{suffix}.controller");
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

        return generatedFxController;
    }

    [MenuItem("GameObject/Voyage/Is Valid Object")]
    public static void IsValidObject(MenuCommand menuCommand)
    {
        GameObject go = Selection.activeGameObject;
        if (go == null || go.scene == null || go.scene.IsValid() == false)
        { 
            Debug.Log("Invalid");
            return;
        }
    }

    public static bool ShouldIgnoreGameObject(GameObject go)
    {
        return
            go == null
            || go.TryGetComponent<ModularAvatarMergeAnimator>(out var _) == true
            || go.TryGetComponent<ModularAvatarParameters>(out var _) == true;
    }

    public static void AddTagIfNotPresent(string tagToAdd)
    {
        string[] tags = UnityEditorInternal.InternalEditorUtility.tags;
        foreach (string tag in tags)
        {
            if (tag == tagToAdd) { return; }
        }

        UnityEditorInternal.InternalEditorUtility.AddTag(tagToAdd);
    }

    [MenuItem("GameObject/Voyage/Invididual toggle (Modular Avatar)")]
    public static void ToggleThroughMenuMa(MenuCommand menuCommand)
    {
        

        if (menuCommand.context != Selection.activeGameObject) { return; }

        var currentUndoGroup = Undo.GetCurrentGroup();

        GameObject[] gameObjects = Selection.gameObjects;
        if (gameObjects.Length == 0) { return; }

        TryGetActiveFolderPath(out string outFolderPath);

        /* Let's avoid leading the user to the 'Packages' directory, this might lead to disaster */
        outFolderPath = (outFolderPath.StartsWith("Assets") ? outFolderPath : "Assets");

        var assetsFolder = Application.dataPath;

        string saveDirPath = EditorUtility.SaveFolderPanel("Where to save the animations", outFolderPath, "");
        if (saveDirPath.Length == 0) { return; }
        if (!saveDirPath.StartsWith(assetsFolder))
        {
            return;
        }
        string assetsRelativePath = saveDirPath.Substring(assetsFolder.Length);
        //if (assetsRelativePath.StartsWith('/')) assetsRelativePath += "/";

        string suffix = DateTime.Now.ToString("s").Replace(":", "");
        string projectRelativePath = $"Assets/{assetsRelativePath}";
        string folderName = $"QuickToggle-{suffix}";

        if (!AssetDatabase.IsValidFolder(projectRelativePath))
        {
            Debug.LogError("Could not determine the save relative path");
            return;
        }

        string generatedFolderGuid = AssetDatabase.CreateFolder(projectRelativePath, folderName);

        if (generatedFolderGuid == "")
        {
            Debug.LogError($"generatedFolderGuid : {generatedFolderGuid} - Asked for {projectRelativePath}, {folderName}");
            Debug.LogError("Could not create a folder to store the assets to generate");
            return;
        }

        string generatedAssetsRelativePath = AssetDatabase.GUIDToAssetPath(generatedFolderGuid);

        Transform lastMenuAdded = null;
        AddTagIfNotPresent(quickToggleTag);
        foreach (var selectedGameObject in gameObjects)
        {
            if (ShouldIgnoreGameObject(selectedGameObject))
            {
                Debug.LogWarning($"[Quick Toggle] Ignoring {selectedGameObject}");
                continue;
            }
            Transform menuTransform = PrepareMenuOnAvatar(selectedGameObject, out Transform avatarTransform);

            Undo.RecordObject(selectedGameObject.transform, $"[QT] Recording the transform of {selectedGameObject.name}");

            string itemName = selectedGameObject.name;
            string itemFsFriendlyName = FilesystemFriendlyName(itemName.Replace(" ", "_"));
            string parameterName = $"toggle_{itemFsFriendlyName}";

            if (selectedGameObject == null) return;

            AnimatorController generatedFxController = CreateToggleItemController(
                selectedGameObject, generatedAssetsRelativePath,
                itemName, parameterName,
                selectedGameObject.GetInstanceID().ToString(),
                avatarTransform != null ? avatarTransform.name : itemName);
            if (generatedFxController == null)
            {
                Debug.LogWarning($"Could not create an animator for {selectedGameObject.name}. Skipping");
                continue;
            }

            bool createAnimator = (selectedGameObject.TryGetComponent<Animator>(out Animator _) == false);
            
            if (createAnimator)
            {
                Animator animator = Undo.AddComponent<Animator>(selectedGameObject);
                animator.runtimeAnimatorController = generatedFxController;
            }

            var mergeAnimator = Undo.AddComponent<ModularAvatarMergeAnimator>(selectedGameObject);
            mergeAnimator.animator = generatedFxController;
            mergeAnimator.deleteAttachedAnimator = createAnimator;

            var avatarParameters = Undo.AddComponent<ModularAvatarParameters>(selectedGameObject);
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

            var menuItemObject = new GameObject() { name = selectedGameObject.name, tag = quickToggleTag };
            Undo.RegisterCreatedObjectUndo(menuItemObject, "[QT] Add a child to represent the Menu Item");
            Undo.RecordObject(menuItemObject.transform, "[QT] Setting the object transform");
            menuItemObject.transform.parent = selectedGameObject.transform;

            var menuItem = Undo.AddComponent<ModularAvatarMenuItem>(menuItemObject);
            menuItem.Control.type = VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionsMenu.Control.ControlType.Toggle;
            menuItem.Control.name = itemName;
            menuItem.Control.parameter = new VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionsMenu.Control.Parameter()
            {
                name = parameterName
            };

            var menuItemInstaller = Undo.AddComponent<ModularAvatarMenuInstaller>(menuItemObject);

            if (menuTransform != null)
            {
                AddItemToModularAvatarMenu(menuTransform, selectedGameObject.name, menuItemInstaller);
                lastMenuAdded = menuTransform;
            }

        }

        if (lastMenuAdded != null)
        {
            Selection.activeGameObject = lastMenuAdded.gameObject;
        }

        Undo.CollapseUndoOperations(currentUndoGroup);



    }


}
#endif