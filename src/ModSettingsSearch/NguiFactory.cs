using Il2Cpp;
using Il2CppInterop.Runtime;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ModSettingsSearch
{
    // Same approach as CraftingSearch: clone real game widgets, strip their game scripts (localization,
    // tweens) so nothing overwrites them, and draw with the game's own atlas sprites and fonts.
    internal static class NguiFactory
    {
        internal const int PlaceholderFontSize = 13;
        internal const int TypedFontSize = 16;

        internal static readonly Color TextColor = Color.white;
        internal static readonly Color DimTextColor = new Color(0.62f, 0.64f, 0.64f, 1f);
        internal static readonly Color ClearButtonColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        internal static readonly Color BoxColor = new Color(0.15f, 0.17f, 0.17f, 0.95f);

        internal static UILabel CreateLabel(string name, Transform parent, UILabel template, int fontSize, Color color, UIWidget.Pivot pivot, int width, int depth)
        {
            UILabel label = null;

            if (template != null)
            {
                GameObject labelGo = Object.Instantiate(template.gameObject, parent);
                labelGo.name = name;
                SetLayerRecursively(labelGo, parent.gameObject.layer);
                StripNonUiMonoBehaviours(labelGo, nameof(UILabel));
                for (int i = labelGo.transform.childCount - 1; i >= 0; i--)
                    Object.Destroy(labelGo.transform.GetChild(i).gameObject);
                label = labelGo.GetComponent<UILabel>();
            }

            if (label == null)
            {
                GameObject labelGo = new GameObject(name);
                labelGo.transform.SetParent(parent, false);
                labelGo.layer = parent.gameObject.layer;
                label = labelGo.AddComponent<UILabel>();
            }

            label.gameObject.SetActive(true);
            label.enabled = true;
            label.alpha = 1f;
            label.color = color;
            label.pivot = pivot;
            label.alignment = pivot == UIWidget.Pivot.Right ? NGUIText.Alignment.Right
                : pivot == UIWidget.Pivot.Center ? NGUIText.Alignment.Center
                : NGUIText.Alignment.Left;
            label.overflowMethod = UILabel.Overflow.ClampContent;
            label.maxLineCount = 1;
            label.fontSize = fontSize;
            label.width = width;
            label.height = fontSize * 2;
            label.depth = depth;
            label.transform.localScale = Vector3.one;
            label.transform.localRotation = Quaternion.identity;
            label.transform.localPosition = Vector3.zero;
            label.text = string.Empty;
            label.MarkAsChanged();
            return label;
        }

        internal static UISprite CreateSprite(string name, Transform parent, UISprite source, Color color, int width, int height, int depth)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.layer = parent.gameObject.layer;
            go.transform.localPosition = Vector3.zero;
            go.transform.localScale = Vector3.one;

            UISprite sprite = go.AddComponent<UISprite>();
            if (source != null)
            {
                sprite.atlas = source.atlas;
                sprite.spriteName = source.spriteName;
                sprite.type = source.type;
            }
            sprite.alpha = 1f;
            sprite.color = color;
            sprite.width = width;
            sprite.height = height;
            sprite.depth = depth;
            sprite.MarkAsChanged();
            return sprite;
        }

        // Flat, fully opaque fill. The atlas sprite the menu uses is a grungy, partly transparent texture,
        // so on its own it lets the page behind show through and makes every strip look the same.
        internal static UITexture CreateSolid(string name, Transform parent, Color color, int width, int height, int depth)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.layer = parent.gameObject.layer;
            go.transform.localPosition = Vector3.zero;
            go.transform.localScale = Vector3.one;

            UITexture tex = go.AddComponent<UITexture>();
            tex.mainTexture = Texture2D.whiteTexture;
            tex.color = color;
            tex.width = width;
            tex.height = height;
            tex.depth = depth;
            tex.MarkAsChanged();
            return tex;
        }

        internal static BoxCollider AddCollider(GameObject go, float width, float height)
        {
            BoxCollider collider = go.GetComponent<BoxCollider>() ?? go.AddComponent<BoxCollider>();
            collider.size = new Vector3(width, height, 1f);
            collider.center = Vector3.zero;
            collider.enabled = true;
            return collider;
        }

        internal static void OnPress(GameObject go, System.Action<bool> onPress)
        {
            UIEventListener listener = go.GetComponent<UIEventListener>() ?? go.AddComponent<UIEventListener>();
            listener.onPress = DelegateSupport.ConvertDelegate<UIEventListener.BoolDelegate>(
                new System.Action<GameObject, bool>((_, state) => onPress(state)));
        }

        internal static void OnDrag(GameObject go, System.Action onDrag)
        {
            UIEventListener listener = go.GetComponent<UIEventListener>() ?? go.AddComponent<UIEventListener>();
            listener.onDrag = DelegateSupport.ConvertDelegate<UIEventListener.VectorDelegate>(
                new System.Action<GameObject, Vector2>((_, _) => onDrag()));
        }

        internal static UIEventListener AddListener(GameObject go, System.Action onClick, System.Action<bool> onHover)
        {
            UIEventListener listener = go.GetComponent<UIEventListener>() ?? go.AddComponent<UIEventListener>();
            if (onClick != null)
            {
                listener.onClick = DelegateSupport.ConvertDelegate<UIEventListener.VoidDelegate>(
                    new System.Action<GameObject>((_) => onClick()));
            }
            if (onHover != null)
            {
                listener.onHover = DelegateSupport.ConvertDelegate<UIEventListener.BoolDelegate>(
                    new System.Action<GameObject, bool>((_, state) => onHover(state)));
            }
            return listener;
        }

        internal static UIInput CreateSearchInput(Transform parent, UILabel fontSource, Color fill, int width, int height, string placeholder, out GameObject clearButton)
        {
            clearButton = null;

            UIInput template = FindTemplateInput();
            GameObject clone;
            if (template != null)
            {
                clone = Object.Instantiate(template.gameObject, parent);
                StripNonUiMonoBehaviours(clone, nameof(UIInput), nameof(UISprite), nameof(UILabel), nameof(UIWidget), nameof(BoxCollider));
                DestroyExistingWidgets<UISprite>(clone);
                DestroyExistingWidgets<UILabel>(clone);
            }
            else
            {
                ModSettingsSearchMod.WarnOnce("inputTemplate", "Could not find a game UIInput to copy, using a plain one.");
                clone = new GameObject();
                clone.transform.SetParent(parent, false);
            }

            clone.name = "ModSettingsSearchInput";
            SetLayerRecursively(clone, parent.gameObject.layer);
            clone.SetActive(true);
            clone.transform.localScale = Vector3.one;
            clone.transform.localRotation = Quaternion.identity;

            UIInput input = clone.GetComponent<UIInput>() ?? clone.AddComponent<UIInput>();

            CreateSolid("SearchBg", clone.transform, fill, width, height, 499);

            UILabel label = CreateLabel("SearchLabel", clone.transform, fontSource, PlaceholderFontSize, TextColor, UIWidget.Pivot.Left, width - 50, 500);
            label.transform.localPosition = new Vector3(-(width * 0.5f) + 10f, 0f, -2f);
            label.text = placeholder;

            input.label = label;
            input.enabled = true;
            input.onChange.Clear();
            input.onSubmit.Clear();
            input.defaultText = placeholder;
            input.value = string.Empty;
            input.activeTextColor = TextColor;
            input.caretColor = TextColor;
            input.characterLimit = 40;
            input.inputType = UIInput.InputType.Standard;
            input.validation = UIInput.Validation.None;
            input.hideInput = false;

            AddCollider(clone, width, height);

            clearButton = new GameObject("ClearButton");
            clearButton.transform.SetParent(clone.transform, false);
            clearButton.layer = clone.layer;
            clearButton.transform.localPosition = new Vector3((width * 0.5f) - 15f, 0f, -3f);
            clearButton.transform.localScale = Vector3.one;

            UILabel clearLabel = CreateLabel("ClearLabel", clearButton.transform, fontSource, TypedFontSize, ClearButtonColor, UIWidget.Pivot.Center, 24, 505);
            clearLabel.overflowMethod = UILabel.Overflow.ResizeFreely;
            clearLabel.text = "X";
            AddCollider(clearButton, 26f, 26f);
            clearButton.SetActive(false);

            return input;
        }

        internal static void SyncPlaceholderFontSize(UIInput input)
        {
            if (input == null || input.label == null)
                return;

            bool isFocused = false;
            try { isFocused = input.isSelected; } catch { }

            bool showingPlaceholder = !isFocused && string.IsNullOrEmpty(input.value);
            int targetSize = showingPlaceholder ? PlaceholderFontSize : TypedFontSize;

            if (input.label.fontSize != targetSize)
            {
                input.label.fontSize = targetSize;
                input.label.height = targetSize * 2;
                input.label.MarkAsChanged();
            }

            if (showingPlaceholder && input.label.color != TextColor)
            {
                input.label.color = TextColor;
                input.label.MarkAsChanged();
            }
        }

        internal static UIInput FindTemplateInput()
        {
            var inputs = Resources.FindObjectsOfTypeAll<UIInput>();
            UIInput fallback = null;

            for (int i = 0; i < inputs.Length; i++)
            {
                var input = inputs[i];
                if (input == null || input.name == "ModSettingsSearchInput")
                    continue;

                string rootName = input.transform.root != null ? input.transform.root.name : "null";
                if (input.label != null && (input.label.bitmapFont != null || input.label.trueTypeFont != null))
                {
                    if (fallback == null)
                        fallback = input;
                    if (rootName.Contains("Custom") || input.name.Contains("Input") || input.name.Contains("Code"))
                        return input;
                }
            }

            return fallback;
        }

        internal static UILabel FindFontSource(UILabel preferred, Component searchRoot)
        {
            if (preferred != null && (preferred.bitmapFont != null || preferred.trueTypeFont != null))
                return preferred;

            foreach (var lbl in searchRoot.GetComponentsInChildren<UILabel>(true))
            {
                if (lbl.bitmapFont != null || lbl.trueTypeFont != null)
                    return lbl;
            }

            foreach (var lbl in Resources.FindObjectsOfTypeAll<UILabel>())
            {
                if (lbl != null && (lbl.bitmapFont != null || lbl.trueTypeFont != null))
                    return lbl;
            }

            return null;
        }

        internal static void StripNonUiMonoBehaviours(GameObject root, params string[] allowedTypeNames)
        {
            foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null)
                    continue;

                string typeName = mb.GetIl2CppType().Name;
                bool allowed = false;
                for (int i = 0; i < allowedTypeNames.Length; i++)
                {
                    if (typeName == allowedTypeNames[i])
                    {
                        allowed = true;
                        break;
                    }
                }

                if (!allowed)
                    Object.Destroy(mb);
            }
        }

        internal static void DestroyExistingWidgets<T>(GameObject root) where T : Component
        {
            foreach (var comp in root.GetComponentsInChildren<T>(true))
            {
                if (comp != null)
                    Object.Destroy(comp.gameObject == root ? (Object)comp : comp.gameObject);
            }
        }

        internal static void SetLayerRecursively(GameObject obj, int layer)
        {
            obj.layer = layer;
            int childCount = obj.transform.childCount;
            for (int i = 0; i < childCount; i++)
            {
                Transform child = obj.transform.GetChild(i);
                if (child != null)
                    SetLayerRecursively(child.gameObject, layer);
            }
        }
    }
}
