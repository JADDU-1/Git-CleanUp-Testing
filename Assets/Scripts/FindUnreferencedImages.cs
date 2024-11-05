using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UI;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor.AddressableAssets;

public class UnreferencedImagesFinder : EditorWindow
{
    private string folderPath = "Assets/Textures";

    [MenuItem("Tools/Find Unreferenced Images")]
    public static void ShowWindow()
    {
        GetWindow<UnreferencedImagesFinder>("Find Unreferenced Images");
    }

    private void OnGUI()
    {
        GUILayout.Label("Find Unreferenced Images in Folder", EditorStyles.boldLabel);
        folderPath = EditorGUILayout.TextField("Folder Path", folderPath);

        if (GUILayout.Button("Find Unreferenced Images"))
        {
            FindUnreferencedImages();
        }
    }

    private void FindUnreferencedImages()
    {
        List<string> unreferencedImages = new List<string>();

        // Get all texture assets in the folder
        string[] allTextures = Directory.GetFiles(folderPath, "*.png", SearchOption.AllDirectories);
        allTextures = allTextures.Concat(Directory.GetFiles(folderPath, "*.jpg", SearchOption.AllDirectories)).ToArray();

        foreach (string texturePath in allTextures)
        {
            string assetPath = texturePath.Replace(Application.dataPath, "Assets");
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);

            if (texture == null)
                continue;

            // Skip if the asset is marked as addressable
            if (AddressableAssetSettingsDefaultObject.Settings != null &&
                AddressableAssetSettingsDefaultObject.Settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(assetPath)) != null)
                continue;

            // Skip if texture has a reference in scenes, prefabs, or is used in Image/SpriteRenderer components
            if (IsTextureInScenes(texture) || IsTextureInPrefabs(texture))
                continue;

            // Log unreferenced image path
            unreferencedImages.Add(assetPath);
        }

        if (unreferencedImages.Count > 0)
        {
            Debug.Log("Unreferenced images found:");
            foreach (string imagePath in unreferencedImages)
            {
                Debug.Log(imagePath);
            }
        }
        else
        {
            Debug.Log("No unreferenced images found.");
        }
    }

    private bool IsTextureInPrefabs(Texture2D texture)
    {
        // Load all prefabs in the project
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
        foreach (string prefabGuid in prefabGuids)
        {
            string prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null) continue;

            if (IsTextureInGameObject(prefab, texture))
                return true;
        }
        return false;
    }

    private bool IsTextureInScenes(Texture2D texture)
    {
        string[] scenes = AssetDatabase.FindAssets("t:Scene");
        foreach (string guid in scenes)
        {
            string scenePath = AssetDatabase.GUIDToAssetPath(guid);
            SceneAsset sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath);
            if (sceneAsset != null)
            {
                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
                GameObject[] rootObjects = scene.GetRootGameObjects();
                foreach (GameObject rootObject in rootObjects)
                {
                    if (IsTextureInGameObject(rootObject, texture))
                    {
                        EditorSceneManager.CloseScene(scene, false);
                        return true;
                    }
                }
                EditorSceneManager.CloseScene(scene, false);
            }
        }
        return false;
    }

    private bool IsTextureInGameObject(GameObject gameObject, Texture2D texture)
    {
        var components = gameObject.GetComponentsInChildren<Component>(true);
        foreach (var component in components)
        {
            // Check if texture is assigned to Image or SpriteRenderer component
            if (component is Image imageComponent && imageComponent.sprite != null && imageComponent.sprite.texture == texture)
            {
                return true;
            }
            if (component is SpriteRenderer spriteRenderer && spriteRenderer.sprite != null && spriteRenderer.sprite.texture == texture)
            {
                return true;
            }

            // Check all other serialized fields for references to this texture
            SerializedObject serializedObject = new SerializedObject(component);
            SerializedProperty property = serializedObject.GetIterator();
            while (property.NextVisible(true))
            {
                if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue == texture)
                {
                    return true;
                }
            }
        }
        return false;
    }
}
