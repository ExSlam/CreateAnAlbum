using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Albummodelite
{
    /// <summary>
    /// Album-only, lightweight UI resource helper. It intentionally instantiates Idol Manager's
    /// shipped MUIP Resources by name. It does not clone controls from live vanilla scenes and does
    /// not depend on the standalone IM UI Framework mod.
    /// </summary>
    internal static class AlbumUiResources
    {
        internal const string ButtonStandard = "button/basic/Standard";
        internal const string ButtonOutline = "button/basic - outline/Standard";
        internal const string ButtonRounded = "button/rounded/Standard";
        internal const string InputStandardMiddle = "input field/Input Field - Standard (Middle)";
        internal const string Scrollbar = "scrollbar/Scrollbar";
        internal const string ModalStyle1 = "modal window/Style 1";
        internal const string Dropdown = "dropdown/Dropdown";
        internal const string LiberationSansSdf = "fonts & materials/LiberationSans SDF";

        internal static Font GetGameFont()
        {
            try
            {
                if (Camera.main == null)
                    return null;

                mainScript main = Camera.main.GetComponent<mainScript>();
                if (main == null || main.Data == null)
                    return null;

                Fonts fonts = main.Data.GetComponent<Fonts>();
                return fonts != null && fonts.IsReady() ? fonts.GetFont() : null;
            }
            catch
            {
                return null;
            }
        }

        internal static TMP_FontAsset GetTmpFont()
        {
            return Resources.Load<TMP_FontAsset>(LiberationSansSdf);
        }

        internal static GameObject InstantiatePrefab(string path, Transform parent, string name)
        {
            GameObject prefab = Resources.Load<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogWarning("[CreateAlbum/UI] Missing Resource prefab: " + path);
                return null;
            }

            GameObject instance = UnityEngine.Object.Instantiate(prefab, parent, false);
            if (!string.IsNullOrEmpty(name))
                instance.name = name;
            return instance;
        }

        internal static GameObject InstantiateButton(
            Transform parent,
            string name,
            string label,
            bool outline,
            UnityAction click)
        {
            GameObject buttonObject = InstantiatePrefab(
                outline ? ButtonOutline : ButtonStandard,
                parent,
                name);

            if (buttonObject == null)
                return CreateFallbackButton(parent, name, label, click);

            Button button = buttonObject.GetComponent<Button>();
            if (button == null)
                button = buttonObject.GetComponentInChildren<Button>(true);
            if (button == null)
                button = buttonObject.AddComponent<Button>();

            button.onClick = new Button.ButtonClickedEvent();
            if (click != null)
                button.onClick.AddListener(click);

            SetButtonLabel(buttonObject, label);
            return buttonObject;
        }

        internal static void SetButtonLabel(GameObject root, string label)
        {
            if (root == null)
                return;

            TMP_FontAsset tmpFont = GetTmpFont();
            TMP_Text[] tmpTexts = root.GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < tmpTexts.Length; i++)
            {
                if (tmpTexts[i] == null)
                    continue;
                tmpTexts[i].text = label ?? "";
                if (tmpFont != null)
                    tmpTexts[i].font = tmpFont;
            }

            Text[] texts = root.GetComponentsInChildren<Text>(true);
            Font gameFont = GetGameFont();
            for (int i = 0; i < texts.Length; i++)
            {
                if (texts[i] == null)
                    continue;
                texts[i].text = label ?? "";
                if (gameFont != null)
                    texts[i].font = gameFont;
            }
        }

        internal static TMP_InputField InstantiateInputField(
            Transform parent,
            string name,
            string value,
            string placeholder,
            UnityAction<string> onChanged)
        {
            GameObject instance = InstantiatePrefab(InputStandardMiddle, parent, name);
            if (instance == null)
                return null;

            TMP_InputField field = instance.GetComponent<TMP_InputField>();
            if (field == null)
                field = instance.GetComponentInChildren<TMP_InputField>(true);
            if (field == null)
            {
                Debug.LogWarning("[CreateAlbum/UI] MUIP input prefab did not expose TMP_InputField.");
                return null;
            }

            TMP_FontAsset font = GetTmpFont();
            if (field.textComponent != null && font != null)
                field.textComponent.font = font;

            if (field.placeholder is TMP_Text)
            {
                TMP_Text placeholderText = (TMP_Text)field.placeholder;
                placeholderText.text = placeholder ?? "";
                if (font != null)
                    placeholderText.font = font;
            }

            field.onValueChanged = new TMP_InputField.OnChangeEvent();
            if (onChanged != null)
                field.onValueChanged.AddListener(onChanged);
            field.text = value ?? "";
            return field;
        }

        internal static Scrollbar InstantiateScrollbar(Transform parent, string name)
        {
            GameObject instance = InstantiatePrefab(Scrollbar, parent, name);
            if (instance == null)
                return null;

            Scrollbar scrollbar = instance.GetComponent<Scrollbar>();
            if (scrollbar == null)
                scrollbar = instance.GetComponentInChildren<Scrollbar>(true);
            return scrollbar;
        }

        internal static GameObject InstantiateModalSurface(Transform parent, string name)
        {
            GameObject modal = InstantiatePrefab(ModalStyle1, parent, name);
            if (modal == null)
                return null;

            // We use the shipped visual tree as a customizable surface, but Album owns its content.
            // Disable any stock interactive buttons/events that the generic MUIP sample modal carries.
            Button[] buttons = modal.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
                buttons[i].onClick = new Button.ButtonClickedEvent();

            return modal;
        }

        private static GameObject CreateFallbackButton(
            Transform parent,
            string name,
            string label,
            UnityAction click)
        {
            // Resource absence should not make a save unusable. This path is intentionally minimal;
            // normal Idol Manager installs use the named MUIP prefab above.
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            Image image = obj.AddComponent<Image>();
            Button button = obj.AddComponent<Button>();
            button.targetGraphic = image;
            if (click != null)
                button.onClick.AddListener(click);

            GameObject textObj = new GameObject("Label");
            textObj.transform.SetParent(obj.transform, false);
            RectTransform tr = textObj.AddComponent<RectTransform>();
            tr.anchorMin = Vector2.zero;
            tr.anchorMax = Vector2.one;
            tr.offsetMin = Vector2.zero;
            tr.offsetMax = Vector2.zero;
            Text text = textObj.AddComponent<Text>();
            Font font = GetGameFont();
            if (font != null)
                text.font = font;
            text.text = label ?? "";
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
            return obj;
        }
    }
}
