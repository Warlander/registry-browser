using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Warlogic.RegistryBrowser
{
    public class PackagePublishWindow : EditorWindow
    {
        private string _packageId;
        private string _displayName;
        private string _localVersion;
        private string _currentRegistryVersion;
        private bool _isRepublish;
        private IReadOnlyList<RegistryScope> _candidateRegistries;
        private string _preselectedRegistryUrl;
        private Action _onPublishCompleted;

        private bool _confirmed;
        private PopupField<string> _registryDropdown;

        public static void Open(string packageId, string displayName, string localVersion,
            string currentRegistryVersion, bool isRepublish, IReadOnlyList<RegistryScope> candidateRegistries,
            string preselectedRegistryUrl, Action onPublishCompleted)
        {
            var window = CreateInstance<PackagePublishWindow>();
            window._packageId = packageId;
            window._displayName = displayName;
            window._localVersion = localVersion;
            window._currentRegistryVersion = currentRegistryVersion;
            window._isRepublish = isRepublish;
            window._candidateRegistries = candidateRegistries;
            window._preselectedRegistryUrl = preselectedRegistryUrl;
            window._onPublishCompleted = onPublishCompleted;
            window.titleContent = new GUIContent("Publish Package");
            window.minSize = new Vector2(400, 200);
            window.ShowUtility();
        }

        private void CreateGUI()
        {
            var root = rootVisualElement;
            root.style.paddingLeft = 12;
            root.style.paddingRight = 12;
            root.style.paddingTop = 12;
            root.style.paddingBottom = 12;

            var titleLabel = new Label($"Publish \"{_displayName}\"");
            titleLabel.style.fontSize = 15;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.marginBottom = 12;
            titleLabel.style.whiteSpace = WhiteSpace.Normal;
            root.Add(titleLabel);

            AddInfoRow(root, "Package ID", _packageId);

            string currentVersionText = !string.IsNullOrEmpty(_currentRegistryVersion) ? _currentRegistryVersion : "Not published";
            AddInfoRow(root, "Current version", currentVersionText);
            AddInfoRow(root, "Version to publish", _localVersion);

            bool showRegistryDropdown = _candidateRegistries != null && _candidateRegistries.Count > 1;
            if (showRegistryDropdown)
            {
                var registryLabels = new List<string>(_candidateRegistries.Count);
                foreach (RegistryScope registry in _candidateRegistries)
                    registryLabels.Add($"{registry.Scope} — {registry.RegistryUrl}");

                _registryDropdown = new PopupField<string>("Registry", registryLabels, 0);
                _registryDropdown.style.marginBottom = 8;
                root.Add(_registryDropdown);
            }
            else
            {
                string registryDisplay = !string.IsNullOrEmpty(_preselectedRegistryUrl)
                    ? _preselectedRegistryUrl
                    : (_candidateRegistries != null && _candidateRegistries.Count == 1
                        ? _candidateRegistries[0].RegistryUrl
                        : "Unknown");
                AddInfoRow(root, "Registry", registryDisplay);
            }

            if (_isRepublish)
            {
                var warningLabel = new Label(
                    $"Version {_localVersion} already exists on the registry. " +
                    "The existing version will be unpublished before publishing the new tarball.");
                warningLabel.style.whiteSpace = WhiteSpace.Normal;
                warningLabel.style.color = new Color(0.95f, 0.75f, 0.2f);
                warningLabel.style.marginTop = 8;
                warningLabel.style.marginBottom = 8;
                root.Add(warningLabel);
            }

            var spacer = new VisualElement();
            spacer.style.flexGrow = 1;
            root.Add(spacer);

            var buttonRow = new VisualElement();
            buttonRow.style.flexDirection = FlexDirection.Row;
            buttonRow.style.justifyContent = Justify.FlexEnd;
            buttonRow.style.marginTop = 12;
            root.Add(buttonRow);

            var cancelButton = new Button(OnCancelClicked) { text = "Cancel" };
            cancelButton.style.paddingLeft = 12;
            cancelButton.style.paddingRight = 12;
            cancelButton.style.marginRight = 6;
            buttonRow.Add(cancelButton);

            var confirmButton = new Button(OnConfirmClicked) { text = "Publish" };
            confirmButton.style.paddingLeft = 12;
            confirmButton.style.paddingRight = 12;
            buttonRow.Add(confirmButton);
        }

        private void OnConfirmClicked()
        {
            _confirmed = true;

            string registryUrl = ResolveRegistryUrl();
            string packageId = _packageId;
            string version = _localVersion;
            bool isRepublish = _isRepublish;
            Action callback = _onPublishCompleted;

            Close();

            _ = PerformPublishAsync(packageId, version, isRepublish, registryUrl, callback);
        }

        private void OnCancelClicked()
        {
            Close();
        }

        private void OnDestroy()
        {
            if (!_confirmed)
                _onPublishCompleted?.Invoke();
        }

        private string ResolveRegistryUrl()
        {
            if (_registryDropdown != null && _candidateRegistries != null)
                return _candidateRegistries[_registryDropdown.index].RegistryUrl;

            if (!string.IsNullOrEmpty(_preselectedRegistryUrl))
                return _preselectedRegistryUrl;

            if (_candidateRegistries != null && _candidateRegistries.Count == 1)
                return _candidateRegistries[0].RegistryUrl;

            return "";
        }

        private static async Task PerformPublishAsync(string packageId, string version,
            bool isRepublish, string registryUrl, Action callback)
        {
            try
            {
                if (isRepublish)
                {
                    EditorUtility.DisplayProgressBar("Publishing", "Unpublishing previous version\u2026", 0.1f);
                    await PackagePublishOperations.NpmUnpublishAsync(packageId, version, registryUrl);
                }

                EditorUtility.DisplayProgressBar("Publishing", "Packing package\u2026", 0.4f);
                string tarballPath = await PackagePublishOperations.PackAsync(packageId);

                EditorUtility.DisplayProgressBar("Publishing", "Publishing to registry\u2026", 0.7f);
                await PackagePublishOperations.NpmPublishAsync(tarballPath, registryUrl);

                EditorUtility.ClearProgressBar();
                EditorUtility.DisplayDialog("Published",
                    $"Successfully published {packageId}@{version} to {registryUrl}.", "OK");
            }
            catch (Exception ex)
            {
                EditorUtility.ClearProgressBar();
                Debug.LogError($"[RegistryBrowser] Publish failed: {ex.Message}");
                EditorUtility.DisplayDialog("Publish Failed", ex.Message, "OK");
            }

            callback?.Invoke();
        }

        private static void AddInfoRow(VisualElement parent, string label, string value)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.marginBottom = 4;

            var nameLabel = new Label(label + ":");
            nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            nameLabel.style.minWidth = 130;
            row.Add(nameLabel);

            var valueLabel = new Label(value);
            valueLabel.style.flexGrow = 1;
            valueLabel.style.whiteSpace = WhiteSpace.Normal;
            row.Add(valueLabel);

            parent.Add(row);
        }
    }
}
