using UnityEditor;
using UnityEngine;

namespace Toaba.Editor
{
    static class ToabaSettingsProvider
    {
        static readonly GUIContent AppIdLabel = new GUIContent(
            "App ID", "On the app's Settings → General page in the Toaba dashboard.");
        static readonly GUIContent KeyLabel = new GUIContent(
            "Publishable key", "Under Publishable keys on the app's Overview page in the Toaba dashboard.");

        const string Page = "Project/Toaba";

        static SerializedObject settings;

        [SettingsProvider]
        static SettingsProvider Create() =>
            new SettingsProvider(Page, SettingsScope.Project)
            {
                guiHandler = _ => Draw(),
                keywords = new[] { "Toaba", "App ID", "Publishable key" },
            };

        [MenuItem("Tools/Toaba/Settings")]
        static void Open() => SettingsService.OpenProjectSettings(Page);

        static void Draw()
        {
            if (settings == null || settings.targetObject == null)
                settings = ToabaSettings.Load() is ToabaSettings found ? new SerializedObject(found) : null;

            if (settings == null)
            {
                EditorGUILayout.HelpBox("Toaba isn't set up in this project.", MessageType.Info);
                if (GUILayout.Button("Set up Toaba", GUILayout.Width(120)))
                    settings = new SerializedObject(CreateAsset());
                return;
            }

            settings.Update();
            EditorGUILayout.PropertyField(settings.FindProperty("appId"), AppIdLabel);
            EditorGUILayout.PropertyField(settings.FindProperty("publishableKey"), KeyLabel);
            if (settings.ApplyModifiedProperties())
                AssetDatabase.SaveAssetIfDirty(settings.targetObject);
        }

        static ToabaSettings CreateAsset()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");
            var asset = ScriptableObject.CreateInstance<ToabaSettings>();
            AssetDatabase.CreateAsset(asset, $"Assets/Resources/{ToabaSettings.ResourceName}.asset");
            return asset;
        }
    }
}
