using System;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Warlogic.RegistryBrowser
{
    public class CreateLocalPackageWindow : EditorWindow
    {
        private static readonly Regex ValidPackageIdRegex = new Regex(@"^[a-z0-9]+(\.[a-z0-9]+)*$", RegexOptions.Compiled);

        private TextField _packageIdField;
        private Label _displayNamePreview;
        private Label _validationLabel;
        private Button _createButton;
        private Action _onCreated;

        public static void Open(Action onCreated)
        {
            var window = GetWindow<CreateLocalPackageWindow>(utility: true, title: "Create Local Package");
            window._onCreated = onCreated;
            window.minSize = new Vector2(380, 160);
            window.maxSize = new Vector2(600, 160);
            window.Show();
        }

        private void CreateGUI()
        {
            var root = rootVisualElement;
            root.style.paddingLeft = 12;
            root.style.paddingRight = 12;
            root.style.paddingTop = 12;
            root.style.paddingBottom = 12;

            var idRow = new VisualElement();
            idRow.style.flexDirection = FlexDirection.Row;
            idRow.style.alignItems = Align.Center;
            idRow.style.marginBottom = 6;

            var idLabel = new Label("Package ID:");
            idLabel.style.minWidth = 100;
            idRow.Add(idLabel);

            _packageIdField = new TextField();
            _packageIdField.style.flexGrow = 1;
            _packageIdField.RegisterValueChangedCallback(OnPackageIdChanged);
            idRow.Add(_packageIdField);
            root.Add(idRow);

            var previewRow = new VisualElement();
            previewRow.style.flexDirection = FlexDirection.Row;
            previewRow.style.alignItems = Align.Center;
            previewRow.style.marginBottom = 6;

            var previewLabel = new Label("Display Name:");
            previewLabel.style.minWidth = 100;
            previewRow.Add(previewLabel);

            _displayNamePreview = new Label();
            _displayNamePreview.style.color = new Color(0.6f, 0.6f, 0.6f);
            previewRow.Add(_displayNamePreview);
            root.Add(previewRow);

            _validationLabel = new Label();
            _validationLabel.style.color = new Color(0.85f, 0.35f, 0.35f);
            _validationLabel.style.marginBottom = 8;
            _validationLabel.style.whiteSpace = WhiteSpace.Normal;
            _validationLabel.style.display = DisplayStyle.None;
            root.Add(_validationLabel);

            var buttonRow = new VisualElement();
            buttonRow.style.flexDirection = FlexDirection.Row;
            buttonRow.style.justifyContent = Justify.FlexEnd;

            var cancelButton = new Button(Close) { text = "Cancel" };
            cancelButton.style.marginRight = 6;
            buttonRow.Add(cancelButton);

            _createButton = new Button(OnCreateClicked) { text = "Create" };
            _createButton.SetEnabled(false);
            buttonRow.Add(_createButton);

            root.Add(buttonRow);
        }

        private void OnPackageIdChanged(ChangeEvent<string> evt)
        {
            string id = evt.newValue ?? "";
            _displayNamePreview.text = LocalPackageCreator.DeriveDisplayName(id);

            bool valid = !string.IsNullOrEmpty(id) && ValidPackageIdRegex.IsMatch(id);
            _createButton.SetEnabled(valid);

            if (!string.IsNullOrEmpty(id) && !valid)
            {
                _validationLabel.text = "ID must use only lowercase letters, digits, and dots (e.g. com.company.mypackage).";
                _validationLabel.style.display = DisplayStyle.Flex;
            }
            else
            {
                _validationLabel.style.display = DisplayStyle.None;
            }
        }

        private async void OnCreateClicked()
        {
            string packageId = _packageIdField.value;
            string displayName = LocalPackageCreator.DeriveDisplayName(packageId);

            if (GitEmbedOperations.IsEmbedded(packageId))
            {
                EditorUtility.DisplayDialog("Package Already Exists",
                    $"A package directory for \"{packageId}\" already exists in Packages/Embeds/.",
                    "OK");
                return;
            }

            try
            {
                bool initGit = RegistryBrowserConfig.LoadInitGitForNewPackages();
                await LocalPackageCreator.CreatePackageAsync(packageId, displayName, initGit);
                _onCreated?.Invoke();
                Close();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[RegistryBrowser] Failed to create package: {ex.Message}");
                EditorUtility.DisplayDialog("Create Failed", ex.Message, "OK");
            }
        }
    }
}
