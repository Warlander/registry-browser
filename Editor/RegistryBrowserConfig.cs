using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Warlogic.RegistryBrowser
{
    public static class RegistryBrowserConfig
    {
        private const string SettingsFilePath = "ProjectSettings/RegistryBrowserConfig.json";

        [Serializable]
        private class SettingsData
        {
            public bool showPackageManagerWarning = true;
            public bool gitIgnorePromptShown = false;
            public bool initGitForNewPackages = true;
            public List<RegistryScope> registries = new();
        }

        private static SettingsData _settings;

        private static SettingsData Settings
        {
            get
            {
                if (_settings != null)
                    return _settings;

                if (File.Exists(SettingsFilePath))
                {
                    SettingsData data = JsonUtility.FromJson<SettingsData>(File.ReadAllText(SettingsFilePath));
                    if (data != null)
                    {
                        _settings = data;
                        return _settings;
                    }
                }

                // File missing or unreadable — create defaults and persist them.
                _settings = new SettingsData();
                Save();
                return _settings;
            }
        }

        private static void Save()
        {
            File.WriteAllText(SettingsFilePath, JsonUtility.ToJson(_settings, true));
        }

        public static IReadOnlyList<RegistryScope> LoadRegistries()
            => Settings.registries;

        public static bool LoadShowPackageManagerWarning()
            => Settings.showPackageManagerWarning;

        public static bool LoadInitGitForNewPackages()
            => Settings.initGitForNewPackages;

        public static bool LoadGitIgnorePromptShown()
            => Settings.gitIgnorePromptShown;

        public static void MarkGitIgnorePromptShown()
        {
            Settings.gitIgnorePromptShown = true;
            Save();
        }

        [SettingsProvider]
        private static SettingsProvider CreateSettingsProvider()
        {
            List<RegistryScope> editing = null;
            bool? gitIgnoreInPlace = null;

            var provider = new SettingsProvider("Project/Registry Browser", SettingsScope.Project)
            {
                label = "Registry Browser",
                guiHandler = _ =>
                {
                    SettingsData settings = Settings;

                    if (editing == null)
                        editing = new List<RegistryScope>(settings.registries);

                    if (gitIgnoreInPlace == null)
                        gitIgnoreInPlace = GitEmbedOperations.IsEmbedFolderInGitIgnore();

                    EditorGUILayout.LabelField("Git Ignore Setup", EditorStyles.boldLabel);
                    EditorGUILayout.Space(4);

                    bool alreadyIgnored = gitIgnoreInPlace == true;
                    GUI.enabled = !alreadyIgnored;
                    if (GUILayout.Button("Add Packages/Embeds/ to .gitignore"))
                    {
                        GitEmbedOperations.AddEmbedFolderToGitIgnore();
                        gitIgnoreInPlace = true;
                    }
                    GUI.enabled = true;

                    if (alreadyIgnored)
                        EditorGUILayout.LabelField("Packages/Embeds/ is already in .gitignore.");

                    EditorGUILayout.Space(12);

                    EditorGUILayout.LabelField("Local Package Creation", EditorStyles.boldLabel);
                    EditorGUILayout.Space(4);

                    bool newInitGit = EditorGUILayout.ToggleLeft("Initialize Git Repository for New Packages", settings.initGitForNewPackages);
                    EditorGUILayout.Space(12);

                    EditorGUILayout.LabelField("Package Manager Integration", EditorStyles.boldLabel);
                    EditorGUILayout.Space(4);

                    bool newShowWarning = EditorGUILayout.ToggleLeft("Show Warning for Managed Packages", settings.showPackageManagerWarning);
                    EditorGUILayout.Space(8);

                    EditorGUILayout.LabelField("Tracked Registries", EditorStyles.boldLabel);
                    EditorGUILayout.Space(4);

                    bool changed = newShowWarning != settings.showPackageManagerWarning || newInitGit != settings.initGitForNewPackages;
                    settings.showPackageManagerWarning = newShowWarning;
                    settings.initGitForNewPackages = newInitGit;
                    int removeIndex = -1;

                    for (int i = 0; i < editing.Count; i++)
                    {
                        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                        string newScope = EditorGUILayout.TextField("Scope Prefix", editing[i].Scope);
                        string newUrl = EditorGUILayout.TextField("Registry URL", editing[i].RegistryUrl);

                        if (GUILayout.Button("Remove", GUILayout.Width(70)))
                            removeIndex = i;

                        EditorGUILayout.EndVertical();
                        EditorGUILayout.Space(2);

                        if (newScope != editing[i].Scope || newUrl != editing[i].RegistryUrl)
                        {
                            editing[i] = new RegistryScope(newScope, newUrl);
                            changed = true;
                        }
                    }

                    if (removeIndex >= 0)
                    {
                        editing.RemoveAt(removeIndex);
                        changed = true;
                    }

                    if (GUILayout.Button("Add Registry"))
                    {
                        editing.Add(new RegistryScope("", ""));
                        changed = true;
                    }

                    if (changed)
                    {
                        settings.registries = editing;
                        Save();
                    }
                }
            };

            return provider;
        }
    }
}
