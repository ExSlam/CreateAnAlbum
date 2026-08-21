using System;
using System.Collections.Generic;
using System.Reflection;
using Michsky.UI.ModernUIPack;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Albummodelite
{
    internal enum AlbumButtonStyle
    {
        Standard,
        Outline,
        Back,
        Confirm,
        Destructive
    }

    /// <summary>
    /// Album-only, lightweight UI resource helper. It intentionally instantiates Idol Manager's
    /// shipped MUIP Resources by name and clones the game's semantic action buttons and list slider
    /// where available. It does not depend on the standalone IM UI Framework mod.
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

        private static Font cachedLegacyFont;
        private static TMP_FontAsset cachedConfiguredTmpFont;
        private static TMP_FontAsset cachedResolvedTmpFont;
        private static bool ownsCachedResolvedTmpFont;

        internal static Font GetGameFont()
        {
            Fonts fonts = GetGameFonts();
            try
            {
                Font selected = fonts != null && fonts.IsReady() ? fonts.GetFont() : null;
                return selected != null
                    ? selected
                    : Resources.GetBuiltinResource<Font>("Arial.ttf");
            }
            catch
            {
                return Resources.GetBuiltinResource<Font>("Arial.ttf");
            }
        }

        internal static TMP_FontAsset GetTmpFont()
        {
            Fonts fonts = GetGameFonts();
            Font selected = null;
            TMP_FontAsset configured = null;

            try
            {
                if (fonts != null)
                {
                    configured = fonts.FontAsset;
                    if (fonts.IsReady())
                        selected = fonts.GetFont();
                }
            }
            catch
            {
            }

            if (cachedLegacyFont == selected &&
                cachedConfiguredTmpFont == configured &&
                IsUsableTextFont(cachedResolvedTmpFont))
            {
                return cachedResolvedTmpFont;
            }

            TMP_FontAsset resolved = FindLoadedTmpFont(selected);
            bool ownsResolved = false;
            if (!IsUsableTextFont(resolved) && selected != null)
            {
                resolved = CreateRuntimeTmpFont(selected);
                ownsResolved = IsUsableTextFont(resolved);
            }
            if (!IsUsableTextFont(resolved) && IsUsableTextFont(configured))
            {
                resolved = configured;
                ownsResolved = false;
            }
            if (!IsUsableTextFont(resolved))
            {
                resolved = Resources.Load<TMP_FontAsset>(LiberationSansSdf);
                ownsResolved = false;
            }
            if (!IsUsableTextFont(resolved))
            {
                resolved = FindFirstUsableLoadedTmpFont();
                ownsResolved = false;
            }

            if (ownsCachedResolvedTmpFont && cachedResolvedTmpFont != null &&
                cachedResolvedTmpFont != resolved)
            {
                UnityEngine.Object.Destroy(cachedResolvedTmpFont);
            }

            cachedLegacyFont = selected;
            cachedConfiguredTmpFont = configured;
            cachedResolvedTmpFont = resolved;
            ownsCachedResolvedTmpFont = ownsResolved;
            return resolved;
        }

        internal static GameObject InstantiatePrefab(string path, Transform parent, string name)
        {
            if (parent == null)
            {
                Debug.LogWarning("[CreateAlbum/UI] Cannot instantiate Resource prefab without a parent: " + path);
                return null;
            }

            GameObject prefab = Resources.Load<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogWarning("[CreateAlbum/UI] Missing Resource prefab: " + path);
                return null;
            }

            GameObject instance = UnityEngine.Object.Instantiate(prefab, parent, false);
            instance.SetActive(false);
            if (!string.IsNullOrEmpty(name))
                instance.name = name;
            SetLayerRecursively(instance, parent.gameObject.layer);
            return instance;
        }

        internal static GameObject InstantiateButton(
            Transform parent,
            string name,
            string label,
            bool outline,
            UnityAction click)
        {
            return InstantiateButton(
                parent,
                name,
                label,
                outline ? AlbumButtonStyle.Outline : AlbumButtonStyle.Standard,
                click
            );
        }

        internal static GameObject InstantiateButton(
            Transform parent,
            string name,
            string label,
            AlbumButtonStyle style,
            UnityAction click)
        {
            // Repository-wide UI invariant: a control explicitly named/labeled Close is always
            // destructive-style red with white text, even if a future caller accidentally asks
            // for a neutral/outline style.
            if (string.Equals(name, "Close", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(label, "Close", StringComparison.OrdinalIgnoreCase))
            {
                style = AlbumButtonStyle.Destructive;
            }

            bool usesVanillaSemanticSprite;
            GameObject buttonObject = InstantiateVanillaSemanticButton(
                parent,
                name,
                style,
                out usesVanillaSemanticSprite
            );
            if (buttonObject == null)
            {
                buttonObject = InstantiatePrefab(
                    GetButtonResourcePath(style),
                    parent,
                    name
                );
            }

            if (buttonObject == null)
                return CreateFallbackButton(parent, name, label, style, click);

            Button button = ResetButtonListeners(buttonObject);
            if (button == null)
            {
                Debug.LogWarning("[CreateAlbum/UI] MUIP button prefab did not expose a Button.");
                UnityEngine.Object.Destroy(buttonObject);
                return CreateFallbackButton(parent, name, label, style, click);
            }

            if (click != null)
                button.onClick.AddListener(click);

            DisableDynamicStyleUpdates(buttonObject);
            SetButtonLabel(buttonObject, label);
            ApplySemanticButtonStyle(
                buttonObject,
                style,
                usesVanillaSemanticSprite
            );
            ActivateButtonDefaults(buttonObject);
            buttonObject.SetActive(true);
            return buttonObject;
        }

        internal static void SetButtonLabel(GameObject root, string label)
        {
            if (root == null)
                return;

            ClearButtonLocalization(root);
            ConfigureButtonManagers(root, label);

            string value = label ?? "";
            TMP_FontAsset tmpFont = GetTmpFont();
            TMP_Text[] tmpTexts = root.GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < tmpTexts.Length; i++)
            {
                TMP_Text text = tmpTexts[i];
                if (text == null || IsIconText(text))
                    continue;
                text.text = value;
                if (tmpFont != null)
                    text.font = tmpFont;
            }

            Text[] texts = root.GetComponentsInChildren<Text>(true);
            Font gameFont = GetGameFont();
            for (int i = 0; i < texts.Length; i++)
            {
                Text text = texts[i];
                if (text == null || IsIconName(text.gameObject.name) || IsIconName(text.font != null ? text.font.name : null))
                    continue;
                text.text = value;
                if (gameFont != null)
                    text.font = gameFont;
            }
        }

        internal static void SetButtonFontSize(GameObject root, int fontSize)
        {
            if (root == null)
                return;

            float size = Mathf.Max(6, fontSize);
            TMP_Text[] tmpTexts = root.GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < tmpTexts.Length; i++)
            {
                TMP_Text text = tmpTexts[i];
                if (text != null && !IsIconText(text))
                    text.fontSize = size;
            }

            Text[] texts = root.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                Text text = texts[i];
                if (text != null && !IsIconName(text.gameObject.name))
                    text.fontSize = Mathf.RoundToInt(size);
            }
        }

        internal static TMP_InputField InstantiateInputField(
            Transform parent,
            string name,
            string value,
            string placeholder,
            UnityAction<string> onChanged)
        {
            GameObject ignoredRoot;
            return InstantiateInputField(parent, name, value, placeholder, onChanged, out ignoredRoot);
        }

        internal static TMP_InputField InstantiateInputField(
            Transform parent,
            string name,
            string value,
            string placeholder,
            UnityAction<string> onChanged,
            out GameObject root)
        {
            root = InstantiatePrefab(InputStandardMiddle, parent, name);
            if (root == null)
                return null;

            TMP_InputField field = root.GetComponent<TMP_InputField>();
            if (field == null)
                field = root.GetComponentInChildren<TMP_InputField>(true);
            if (field == null)
            {
                Debug.LogWarning("[CreateAlbum/UI] MUIP input prefab did not expose TMP_InputField.");
                UnityEngine.Object.Destroy(root);
                root = null;
                return null;
            }

            DisableDynamicStyleUpdates(root);
            ApplyGameFonts(root);
            TMP_Text placeholderText = field.placeholder as TMP_Text;
            if (placeholderText == null)
            {
                TMP_Text[] candidates = root.GetComponentsInChildren<TMP_Text>(true);
                for (int i = 0; i < candidates.Length; i++)
                {
                    TMP_Text candidate = candidates[i];
                    if (candidate != null && ContainsInvariant(candidate.gameObject.name, "placeholder"))
                    {
                        placeholderText = candidate;
                        break;
                    }
                }
            }
            if (placeholderText != null)
            {
                placeholderText.text = placeholder ?? "";
                Color textColor = mainScript.black32;
                placeholderText.color = new Color(
                    textColor.r,
                    textColor.g,
                    textColor.b,
                    0.62f
                );
                field.placeholder = placeholderText;
            }

            if (field.textComponent != null)
                field.textComponent.color = mainScript.black32;
            field.caretColor = mainScript.black32;
            field.selectionColor = new Color(0.24f, 0.45f, 0.82f, 0.28f);

            field.onValueChanged = new TMP_InputField.OnChangeEvent();
            field.text = value ?? "";
            if (onChanged != null)
                field.onValueChanged.AddListener(onChanged);

            root.SetActive(true);
            return field;
        }

        internal static Scrollbar InstantiateScrollbar(Transform parent, string name)
        {
            GameObject ignoredRoot;
            return InstantiateScrollbar(parent, name, out ignoredRoot);
        }

        internal static Scrollbar InstantiateScrollbar(Transform parent, string name, out GameObject root)
        {
            root = InstantiatePrefab(Scrollbar, parent, name);
            if (root == null)
                return null;

            Scrollbar scrollbar = root.GetComponent<Scrollbar>();
            if (scrollbar == null)
                scrollbar = root.GetComponentInChildren<Scrollbar>(true);
            if (scrollbar == null)
            {
                Debug.LogWarning("[CreateAlbum/UI] MUIP scrollbar prefab did not expose a Scrollbar.");
                UnityEngine.Object.Destroy(root);
                root = null;
                return null;
            }

            scrollbar.onValueChanged = new Scrollbar.ScrollEvent();
            root.SetActive(true);
            return scrollbar;
        }

        internal static bool AttachVanillaListScrollIndicator(
            Transform parent,
            ScrollRect target,
            string name,
            float rightCenterInset = 19.34f,
            float viewportRightInset = 26f)
        {
            if (parent == null || target == null || target.viewport == null)
                return false;

            target.movementType = ScrollRect.MovementType.Clamped;
            if (Application.platform == RuntimePlatform.OSXPlayer ||
                Application.platform == RuntimePlatform.OSXEditor)
            {
                target.scrollSensitivity = 3f;
                target.decelerationRate = 0.05f;
            }
            else
            {
                target.scrollSensitivity = 25f;
            }

            ReserveViewportGutter(target, viewportRightInset);

            Slider template = FindProducerListSliderTemplate();
            if (template != null)
            {
                GameObject instance = UnityEngine.Object.Instantiate(
                    template.gameObject,
                    parent,
                    false
                );
                if (instance != null)
                {
                    instance.name = string.IsNullOrEmpty(name)
                        ? "VanillaListSlider"
                        : name;
                    SetLayerRecursively(instance, parent.gameObject.layer);

                    Slider slider = instance.GetComponent<Slider>() ??
                        instance.GetComponentInChildren<Slider>(true);
                    if (slider != null)
                    {
                        slider.onValueChanged = new Slider.SliderEvent();
                        slider.direction = Slider.Direction.BottomToTop;
                        slider.minValue = 0f;
                        slider.maxValue = 1f;
                        slider.wholeNumbers = false;
                        target.verticalScrollbar = null;
                        target.verticalScrollbarSpacing = 0f;

                        AlbumScrollSliderBinding binding =
                            instance.GetComponent<AlbumScrollSliderBinding>() ??
                            instance.AddComponent<AlbumScrollSliderBinding>();
                        binding.Initialize(target, slider, rightCenterInset);
                        instance.transform.SetAsLastSibling();
                        instance.SetActive(true);
                        return true;
                    }

                    UnityEngine.Object.Destroy(instance);
                }
            }

            GameObject scrollbarRoot;
            Scrollbar scrollbar = InstantiateScrollbar(parent, name, out scrollbarRoot);
            if (scrollbar == null || scrollbarRoot == null)
                return false;

            RectTransform rect = scrollbarRoot.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.sizeDelta = new Vector2(20f, -10f);
            rect.anchoredPosition = new Vector2(-4f, 0f);
            scrollbar.direction = UnityEngine.UI.Scrollbar.Direction.BottomToTop;
            target.verticalScrollbar = scrollbar;
            target.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
            target.verticalScrollbarSpacing = 6f;
            scrollbarRoot.transform.SetAsLastSibling();
            return true;
        }

        internal static GameObject InstantiateModalSurface(Transform parent, string name)
        {
            GameObject modal = InstantiatePrefab(ModalStyle1, parent, name);
            if (modal == null)
                return null;

            ClearButtonLocalization(modal);
            ResetButtonListeners(modal);
            ApplyGameFonts(modal);
            modal.SetActive(true);
            return modal;
        }

        internal static void ConfigureCenteredPanel(RectTransform rect, Vector2 size)
        {
            if (rect == null)
                return;

            Vector2 center = new Vector2(0.5f, 0.5f);
            rect.anchorMin = center;
            rect.anchorMax = center;
            rect.pivot = center;
            rect.sizeDelta = new Vector2(Mathf.Max(0f, size.x), Mathf.Max(0f, size.y));
            rect.anchoredPosition = Vector2.zero;
        }

        private static GameObject CreateFallbackButton(
            Transform parent,
            string name,
            string label,
            AlbumButtonStyle style,
            UnityAction click)
        {
            if (parent == null)
                return null;

            // Resource absence should not make a save unusable. This path is intentionally minimal;
            // normal Idol Manager installs use the named MUIP prefab above.
            GameObject obj = new GameObject(
                name,
                typeof(RectTransform),
                typeof(Image),
                typeof(Button));
            obj.SetActive(false);
            obj.transform.SetParent(parent, false);
            SetLayerRecursively(obj, parent.gameObject.layer);

            Image image = obj.GetComponent<Image>();
            image.color = GetFallbackButtonColor(style);
            Button button = obj.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick = new Button.ButtonClickedEvent();
            if (click != null)
                button.onClick.AddListener(click);

            GameObject textObj = new GameObject("Label", typeof(RectTransform), typeof(Text));
            textObj.transform.SetParent(obj.transform, false);
            textObj.layer = obj.layer;
            RectTransform tr = textObj.GetComponent<RectTransform>();
            tr.anchorMin = Vector2.zero;
            tr.anchorMax = Vector2.one;
            tr.offsetMin = Vector2.zero;
            tr.offsetMax = Vector2.zero;
            Text text = textObj.GetComponent<Text>();
            Font font = GetGameFont();
            if (font != null)
                text.font = font;
            text.text = label ?? "";
            text.color = style == AlbumButtonStyle.Outline
                ? new Color(0.18f, 0.25f, 0.33f, 1f)
                : Color.white;
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;

            obj.SetActive(true);
            return obj;
        }

        private static string GetButtonResourcePath(AlbumButtonStyle style)
        {
            switch (style)
            {
                case AlbumButtonStyle.Outline:
                    return ButtonOutline;
                default:
                    return ButtonStandard;
            }
        }

        private static Color GetFallbackButtonColor(AlbumButtonStyle style)
        {
            switch (style)
            {
                case AlbumButtonStyle.Confirm:
                    return mainScript.green32;
                case AlbumButtonStyle.Destructive:
                    return mainScript.red32;
                case AlbumButtonStyle.Back:
                    return mainScript.blue32;
                case AlbumButtonStyle.Outline:
                    return Color.white;
                default:
                    return new Color(0.1764706f, 0.254902f, 0.33333334f, 1f);
            }
        }

        private static void ApplySemanticButtonStyle(
            GameObject root,
            AlbumButtonStyle style,
            bool usesVanillaSemanticSprite)
        {
            if (root == null)
                return;

            bool accent = style == AlbumButtonStyle.Back ||
                style == AlbumButtonStyle.Confirm ||
                style == AlbumButtonStyle.Destructive;
            if (accent && !usesVanillaSemanticSprite)
            {
                Button button = root.GetComponent<Button>() ??
                    root.GetComponentInChildren<Button>(true);
                if (button != null && button.targetGraphic != null)
                {
                    button.targetGraphic.color = style == AlbumButtonStyle.Confirm
                        ? (Color)mainScript.green32
                        : style == AlbumButtonStyle.Destructive
                            ? (Color)mainScript.red32
                            : (Color)mainScript.blue32;
                }
            }

            Color textColor = accent
                ? Color.white
                : (Color)mainScript.black32;

            TMP_Text[] tmpTexts = root.GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < tmpTexts.Length; i++)
            {
                if (tmpTexts[i] != null && !IsIconText(tmpTexts[i]))
                    tmpTexts[i].color = textColor;
            }

            Text[] texts = root.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                if (texts[i] != null && !IsIconName(texts[i].gameObject.name))
                    texts[i].color = textColor;
            }
        }

        private static GameObject InstantiateVanillaSemanticButton(
            Transform parent,
            string name,
            AlbumButtonStyle style,
            out bool usesVanillaSemanticSprite)
        {
            usesVanillaSemanticSprite = false;
            if (parent == null ||
                (style != AlbumButtonStyle.Confirm &&
                 style != AlbumButtonStyle.Destructive))
            {
                return null;
            }

            try
            {
                GameObject settings = PopupManager.GetObject(
                    PopupManager._type.settings_difficulty
                );
                if (settings == null)
                    return null;

                string templateName = style == AlbumButtonStyle.Confirm
                    ? "Apply"
                    : "Cancel";
                Transform template = FindDescendantByName(
                    settings.transform,
                    templateName
                );
                if (template == null)
                    return null;

                GameObject instance = UnityEngine.Object.Instantiate(
                    template.gameObject,
                    parent,
                    false
                );
                instance.SetActive(false);
                instance.name = string.IsNullOrEmpty(name) ? templateName : name;
                SetLayerRecursively(instance, parent.gameObject.layer);
                usesVanillaSemanticSprite = true;
                return instance;
            }
            catch
            {
                return null;
            }
        }

        private static Transform FindDescendantByName(
            Transform root,
            string name)
        {
            if (root == null)
                return null;
            if (string.Equals(root.name, name, StringComparison.Ordinal))
                return root;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindDescendantByName(root.GetChild(i), name);
                if (found != null)
                    return found;
            }
            return null;
        }

        private static void ReserveViewportGutter(ScrollRect target, float rightInset)
        {
            if (target == null || target.viewport == null)
                return;

            Vector2 offsetMax = target.viewport.offsetMax;
            float inset = Mathf.Max(0f, rightInset);
            if (offsetMax.x > -inset)
            {
                offsetMax.x = -inset;
                target.viewport.offsetMax = offsetMax;
            }
        }

        private static Slider FindProducerListSliderTemplate()
        {
            PopupManager manager = GetPopupManager();
            if (manager == null)
                return null;

            Slider slider = FindPopupSlider(
                manager,
                PopupManager._type.producer_contracts,
                "Panel/Slider"
            );
            if (slider != null)
                return slider;

            slider = FindPopupSlider(
                manager,
                PopupManager._type.producer_salaries,
                "Panel/Slider"
            );
            return slider ?? FindPopupSlider(
                manager,
                PopupManager._type.producer_loans,
                "Panel/Credit History/Slider"
            );
        }

        private static Slider FindPopupSlider(
            PopupManager manager,
            PopupManager._type type,
            string path)
        {
            try
            {
                PopupManager._popup popup = manager.GetByType(type);
                if (popup == null || popup.obj == null)
                    return null;
                Transform child = popup.obj.transform.Find(path);
                return child != null ? child.GetComponent<Slider>() : null;
            }
            catch
            {
                return null;
            }
        }

        private static PopupManager GetPopupManager()
        {
            try
            {
                if (Camera.main == null)
                    return null;
                mainScript main = Camera.main.GetComponent<mainScript>();
                return main != null && main.Data != null
                    ? main.Data.GetComponent<PopupManager>()
                    : null;
            }
            catch
            {
                return null;
            }
        }

        private static Fonts GetGameFonts()
        {
            try
            {
                if (Camera.main == null)
                    return null;

                mainScript main = Camera.main.GetComponent<mainScript>();
                if (main == null || main.Data == null)
                    return null;

                return main.Data.GetComponent<Fonts>();
            }
            catch
            {
                return null;
            }
        }

        private static void ApplyGameFonts(GameObject root)
        {
            if (root == null)
                return;

            TMP_FontAsset tmpFont = GetTmpFont();
            TMP_Text[] tmpTexts = root.GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < tmpTexts.Length; i++)
            {
                TMP_Text text = tmpTexts[i];
                if (text != null && !IsIconText(text) && tmpFont != null)
                    text.font = tmpFont;
            }

            Font gameFont = GetGameFont();
            Text[] texts = root.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                Text text = texts[i];
                if (text == null || IsIconName(text.gameObject.name) || IsIconName(text.font != null ? text.font.name : null))
                    continue;
                if (gameFont != null)
                    text.font = gameFont;
            }
        }

        private static void DisableDynamicStyleUpdates(GameObject root)
        {
            if (root == null)
                return;

            UIManagerInputField[] inputUpdaters =
                root.GetComponentsInChildren<UIManagerInputField>(true);
            for (int i = 0; i < inputUpdaters.Length; i++)
                DisableDynamicStyleUpdate(inputUpdaters[i]);

            UIManagerButton[] buttonUpdaters =
                root.GetComponentsInChildren<UIManagerButton>(true);
            for (int i = 0; i < buttonUpdaters.Length; i++)
                DisableDynamicStyleUpdate(buttonUpdaters[i]);
        }

        private static void DisableDynamicStyleUpdate(MonoBehaviour updater)
        {
            if (updater == null)
                return;

            Type updaterType = updater.GetType();
            try
            {
                FieldInfo assetField = updaterType.GetField(
                    "UIManagerAsset",
                    BindingFlags.Instance | BindingFlags.Public
                );
                if (assetField != null && assetField.GetValue(updater) == null)
                    assetField.SetValue(updater, Resources.Load<UIManager>("MUIP Manager"));

                string updateMethod = updater is UIManagerInputField
                    ? "UpdateInputField"
                    : "UpdateButton";
                MethodInfo applyStyle = updaterType.GetMethod(
                    updateMethod,
                    BindingFlags.Instance | BindingFlags.NonPublic
                );
                if (applyStyle != null)
                    applyStyle.Invoke(updater, null);
            }
            catch
            {
            }

            FieldInfo state = updaterType.GetField(
                "dynamicUpdateEnabled",
                BindingFlags.Instance | BindingFlags.NonPublic
            );
            if (state != null)
                state.SetValue(updater, true);
            updater.enabled = false;
        }

        private static Button ResetButtonListeners(GameObject root)
        {
            if (root == null)
                return null;

            Button[] buttons = root.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                Button button = buttons[i];
                if (button == null)
                    continue;
                button.onClick = new Button.ButtonClickedEvent();
                RepairTargetGraphic(button);
            }

            Button primary = root.GetComponent<Button>();
            return primary != null ? primary : (buttons.Length > 0 ? buttons[0] : null);
        }

        private static void RepairTargetGraphic(Button button)
        {
            if (button == null || button.targetGraphic != null)
                return;

            Graphic graphic = button.GetComponent<Graphic>();
            if (graphic == null)
            {
                Image[] images = button.GetComponentsInChildren<Image>(true);
                graphic = images.Length > 0 ? images[0] : null;
            }
            if (graphic != null)
                button.targetGraphic = graphic;
        }

        private static void ClearButtonLocalization(GameObject root)
        {
            if (root == null)
                return;

            Lang_Button[] langButtons = root.GetComponentsInChildren<Lang_Button>(true);
            for (int i = 0; i < langButtons.Length; i++)
            {
                Lang_Button lang = langButtons[i];
                if (lang == null)
                    continue;
                lang.Constant = "";
                lang.Tooltip = "";
            }
        }

        private static void ConfigureButtonManagers(GameObject root, string label)
        {
            if (root == null)
                return;

            string value = label ?? "";
            TMP_FontAsset font = GetTmpFont();
            ButtonManager[] managers = root.GetComponentsInChildren<ButtonManager>(true);
            for (int i = 0; i < managers.Length; i++)
            {
                ButtonManager manager = managers[i];
                if (manager == null)
                    continue;
                manager.useCustomContent = true;
                manager.buttonText = value;
                manager.clickEvent = new UnityEvent();
                SetManagedButtonText(manager.normalText, value, font);
                SetManagedButtonText(manager.highlightedText, value, font);
            }

            ButtonManagerBasic[] basicManagers =
                root.GetComponentsInChildren<ButtonManagerBasic>(true);
            for (int i = 0; i < basicManagers.Length; i++)
            {
                ButtonManagerBasic manager = basicManagers[i];
                if (manager == null)
                    continue;
                manager.useCustomContent = true;
                manager.buttonText = value;
                manager.clickEvent = new UnityEvent();
                SetManagedButtonText(manager.normalText, value, font);
            }
        }

        private static void SetManagedButtonText(TMP_Text text, string value, TMP_FontAsset font)
        {
            if (text == null || IsIconText(text))
                return;
            text.text = value;
            if (font != null)
                text.font = font;
        }

        private static void ActivateButtonDefaults(GameObject root)
        {
            ButtonDefault[] defaults = root.GetComponentsInChildren<ButtonDefault>(true);
            for (int i = 0; i < defaults.Length; i++)
            {
                ButtonDefault buttonDefault = defaults[i];
                if (buttonDefault == null)
                    continue;
                buttonDefault.DefaultTooltip = "";
                buttonDefault.SetTooltip("");
                buttonDefault.Activate(true, false);
            }
        }

        private static void SetLayerRecursively(GameObject root, int layer)
        {
            if (root == null)
                return;

            root.layer = layer;
            Transform transform = root.transform;
            for (int i = 0; i < transform.childCount; i++)
                SetLayerRecursively(transform.GetChild(i).gameObject, layer);
        }

        private static TMP_FontAsset FindLoadedTmpFont(Font selected)
        {
            if (selected == null)
                return null;

            List<string> names = new List<string>();
            AddFontName(names, selected.name);
            try
            {
                string[] fontNames = selected.fontNames;
                if (fontNames != null)
                {
                    for (int i = 0; i < fontNames.Length; i++)
                        AddFontName(names, fontNames[i]);
                }
            }
            catch
            {
            }

            TMP_FontAsset[] loaded = FindAllLoadedTmpFonts();
            for (int i = 0; i < loaded.Length; i++)
            {
                TMP_FontAsset candidate = loaded[i];
                if (!IsUsableTextFont(candidate))
                    continue;
                for (int j = 0; j < names.Count; j++)
                {
                    if (MatchesFontName(candidate, names[j]))
                        return candidate;
                }
            }
            return null;
        }

        private static TMP_FontAsset FindFirstUsableLoadedTmpFont()
        {
            TMP_FontAsset[] loaded = FindAllLoadedTmpFonts();
            for (int i = 0; i < loaded.Length; i++)
            {
                if (IsUsableTextFont(loaded[i]))
                    return loaded[i];
            }
            return null;
        }

        private static TMP_FontAsset[] FindAllLoadedTmpFonts()
        {
            try
            {
                MethodInfo method = typeof(Resources).GetMethod(
                    "FindObjectsOfTypeAll",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new Type[] { typeof(Type) },
                    null);
                if (method == null)
                    return new TMP_FontAsset[0];

                Array values = method.Invoke(null, new object[] { typeof(TMP_FontAsset) }) as Array;
                if (values == null)
                    return new TMP_FontAsset[0];

                List<TMP_FontAsset> fonts = new List<TMP_FontAsset>(values.Length);
                for (int i = 0; i < values.Length; i++)
                {
                    TMP_FontAsset font = values.GetValue(i) as TMP_FontAsset;
                    if (font != null)
                        fonts.Add(font);
                }
                return fonts.ToArray();
            }
            catch
            {
                return new TMP_FontAsset[0];
            }
        }

        private static TMP_FontAsset CreateRuntimeTmpFont(Font source)
        {
            if (source == null)
                return null;

            try
            {
                MethodInfo[] methods = typeof(TMP_FontAsset).GetMethods(BindingFlags.Public | BindingFlags.Static);
                MethodInfo best = null;
                for (int i = 0; i < methods.Length; i++)
                {
                    MethodInfo method = methods[i];
                    if (!string.Equals(method.Name, "CreateFontAsset", StringComparison.Ordinal))
                        continue;

                    ParameterInfo[] parameters = method.GetParameters();
                    if (parameters.Length == 0 || parameters[0].ParameterType != typeof(Font))
                        continue;

                    bool supported = true;
                    for (int j = 1; j < parameters.Length; j++)
                    {
                        if (!parameters[j].HasDefaultValue && !parameters[j].ParameterType.IsValueType)
                        {
                            supported = false;
                            break;
                        }
                    }
                    if (supported && (best == null || parameters.Length < best.GetParameters().Length))
                        best = method;
                }

                if (best == null)
                    return null;

                ParameterInfo[] selectedParameters = best.GetParameters();
                object[] arguments = new object[selectedParameters.Length];
                arguments[0] = source;
                for (int i = 1; i < selectedParameters.Length; i++)
                {
                    arguments[i] = selectedParameters[i].HasDefaultValue
                        ? selectedParameters[i].DefaultValue
                        : Activator.CreateInstance(selectedParameters[i].ParameterType);
                }

                TMP_FontAsset asset = best.Invoke(null, arguments) as TMP_FontAsset;
                if (asset != null)
                    asset.name = "CreateAnAlbum Runtime TMP - " + source.name;
                return IsUsableTextFont(asset) ? asset : null;
            }
            catch
            {
                return null;
            }
        }

        private static bool MatchesFontName(TMP_FontAsset font, string name)
        {
            if (font == null || string.IsNullOrEmpty(name))
                return false;
            if (ContainsInvariant(font.name, name) || ContainsInvariant(name, font.name))
                return true;
            try
            {
                string family = font.faceInfo.familyName;
                return ContainsInvariant(family, name) || ContainsInvariant(name, family);
            }
            catch
            {
                return false;
            }
        }

        private static bool IsUsableTextFont(TMP_FontAsset font)
        {
            if (font == null || IsIconName(font.name))
                return false;
            try
            {
                return !IsIconName(font.faceInfo.familyName);
            }
            catch
            {
                return true;
            }
        }

        private static bool IsIconText(TMP_Text text)
        {
            return text != null &&
                (IsIconName(text.gameObject.name) ||
                    (text.font != null && !IsUsableTextFont(text.font)));
        }

        private static bool IsIconName(string value)
        {
            if (string.IsNullOrEmpty(value))
                return false;

            return ContainsInvariant(value, "icon") ||
                ContainsInvariant(value, "font awesome") ||
                ContainsInvariant(value, "fontawesome") ||
                ContainsInvariant(value, "material symbol") ||
                ContainsInvariant(value, "materialsymbol") ||
                ContainsInvariant(value, "segmdl") ||
                ContainsInvariant(value, "wingding") ||
                ContainsInvariant(value, "symbol");
        }

        private static bool ContainsInvariant(string value, string token)
        {
            return !string.IsNullOrEmpty(value) &&
                !string.IsNullOrEmpty(token) &&
                value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void AddFontName(List<string> names, string value)
        {
            if (string.IsNullOrEmpty(value))
                return;
            for (int i = 0; i < names.Count; i++)
            {
                if (string.Equals(names[i], value, StringComparison.OrdinalIgnoreCase))
                    return;
            }
            names.Add(value);
        }
    }

    internal sealed class AlbumScrollSliderBinding : MonoBehaviour
    {
        private const float SliderWidth = 20f;
        private const float SliderScale = 1.1004126f;

        private ScrollRect scrollRect;
        private Slider slider;
        private RectTransform sliderRect;
        private RectTransform scrollRectTransform;
        private bool suppressEvents;
        private float lastTargetHeight = -1f;
        private float rightCenterInset = 19.34f;

        internal void Initialize(
            ScrollRect target,
            Slider targetSlider,
            float targetRightCenterInset)
        {
            Detach();
            scrollRect = target;
            slider = targetSlider;
            sliderRect = slider != null ? slider.GetComponent<RectTransform>() : null;
            scrollRectTransform = scrollRect != null
                ? scrollRect.GetComponent<RectTransform>()
                : null;
            rightCenterInset = targetRightCenterInset;

            if (scrollRect == null || slider == null)
                return;

            slider.onValueChanged.AddListener(OnSliderValueChanged);
            scrollRect.onValueChanged.AddListener(OnScrollRectValueChanged);
            UpdateGeometry(true);
            SyncFromScrollRect();
        }

        private void OnEnable()
        {
            UpdateGeometry(true);
            SyncFromScrollRect();
        }

        private void LateUpdate()
        {
            UpdateGeometry(false);
        }

        private void OnDestroy()
        {
            Detach();
        }

        private void Detach()
        {
            if (scrollRect != null)
                scrollRect.onValueChanged.RemoveListener(OnScrollRectValueChanged);
            if (slider != null)
                slider.onValueChanged.RemoveListener(OnSliderValueChanged);
        }

        private void OnScrollRectValueChanged(Vector2 value)
        {
            if (suppressEvents || slider == null)
                return;

            suppressEvents = true;
            slider.SetValueWithoutNotify(value.y);
            suppressEvents = false;
        }

        private void OnSliderValueChanged(float value)
        {
            if (suppressEvents || scrollRect == null)
                return;

            suppressEvents = true;
            scrollRect.verticalNormalizedPosition = value;
            suppressEvents = false;
        }

        private void SyncFromScrollRect()
        {
            if (scrollRect == null || slider == null)
                return;

            suppressEvents = true;
            slider.SetValueWithoutNotify(scrollRect.verticalNormalizedPosition);
            suppressEvents = false;
        }

        private void UpdateGeometry(bool force)
        {
            if (sliderRect == null || scrollRectTransform == null)
                return;

            float targetHeight = scrollRectTransform.rect.height;
            if (targetHeight <= 0f ||
                (!force && Mathf.Abs(targetHeight - lastTargetHeight) < 0.01f))
            {
                return;
            }

            lastTargetHeight = targetHeight;
            sliderRect.anchorMin = new Vector2(1f, 0.5f);
            sliderRect.anchorMax = new Vector2(1f, 0.5f);
            sliderRect.pivot = new Vector2(0.5f, 0.5f);
            sliderRect.localScale = new Vector3(SliderScale, SliderScale, 1f);
            sliderRect.sizeDelta = new Vector2(
                SliderWidth,
                targetHeight / SliderScale
            );
            sliderRect.anchoredPosition = new Vector2(-rightCenterInset, 0f);
        }
    }
}
