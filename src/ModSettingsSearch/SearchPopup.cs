using Il2Cpp;
using UnityEngine;

namespace ModSettingsSearch
{
    internal static class SearchPopup
    {
        private const int Width = 530;
        private const int RowHeight = 34;
        private const int VisibleRows = 10;
        private const string Placeholder = "Type part of a mod name";

        private static readonly Color RowColor = new Color(0.12f, 0.13f, 0.13f, 1f);
        private static readonly Color RowStripe = new Color(0.16f, 0.17f, 0.17f, 1f);
        private static readonly Color RowHighlight = new Color(0.32f, 0.35f, 0.35f, 1f);
        private static readonly Color FrameColor = new Color(0.42f, 0.44f, 0.44f, 1f);
        private static readonly Color CurrentModColor = new Color(0.95f, 0.8f, 0.45f, 1f);

        private static Panel_OptionsMenu panel;
        private static GameObject tab;
        private static ConsoleComboBox selector;

        private static GameObject root;
        private static GameObject searchButton;
        private static UISprite searchButtonBg;
        private static UIInput input;
        private static GameObject clearButton;
        private static UILabel countLabel;
        private static UIWidget scrollThumb;
        private static UIWidget shade;
        private static int popupHeight;
        private static readonly List<UIWidget> rowBgs = new();
        private static readonly List<UILabel> rowLabels = new();

        private static List<string> allMods = new();
        private static readonly List<string> matches = new();
        private static string lastQuery;
        private static int highlight;
        private static int scrollOffset;

        private static int tabShownFrame = -1;
        private static int blockUntilFrame = -1;
        private static bool focusNextFrame;
        private static bool openRequested;
        private static float grabOffset;

        internal static bool IsOpen => root != null && root.activeSelf;

        // ModSettings and the options menu both react to Esc/back. While the popup is open, and for a
        // frame after it closes, they have to ignore it or the same press closes the whole menu.
        internal static bool BlocksMenuInput => IsOpen || Time.frameCount <= blockUntilFrame;

        // a couple of extra frames so the Enter/Esc that closed the popup doesn't reach the game either
        internal static bool BlocksKeys => IsOpen || Time.frameCount <= blockUntilFrame + 1;

        // Ctrl+F from another menu: open once the tab has had a couple of frames to show
        internal static void RequestOpen() => openRequested = true;

        private static bool IsTyping => input != null && (input.isSelected || UICamera.selectedObject == input.gameObject);

        internal static void Tick(Panel_OptionsMenu optionsPanel)
        {
            if (panel != optionsPanel || tab == null)
            {
                panel = optionsPanel;
                tab = ModSettingsAccess.GetTab(panel);
                selector = ModSettingsAccess.GetSelector(tab);
                root = null;
                searchButton = null;
            }

            if (tab == null || selector == null || !tab.activeInHierarchy)
            {
                if (IsOpen)
                    Close();
                tabShownFrame = -1;
                openRequested = false;
                return;
            }

            if (tabShownFrame < 0)
                tabShownFrame = Time.frameCount;

            if (root == null)
                Build();

            if (IsOpen)
                UpdateOpen();
            else
                CheckOpenKeys();
        }

        private static void CheckOpenKeys()
        {
            // skip the press that opened the tab and the one that just closed the popup
            if (Time.frameCount - tabShownFrame < 2 || Time.frameCount <= blockUntilFrame + 1)
                return;

            if (openRequested)
            {
                openRequested = false;
                Open();
                return;
            }

            // Enter / A on the mod picker row. Mouse clicks also count as "continue", so leave those out.
            if (InputManager.GetContinuePressed(panel) && !Input.GetMouseButtonDown(0) && !Input.GetMouseButtonUp(0)
                && UIInput.selection == null && ModSettingsAccess.GetSelectedRow(tab) == 0)
            {
                Open();
            }
        }

        internal static void Open()
        {
            if (root == null)
                return;

            allMods = ModSettingsAccess.GetModNames(selector);
            root.SetActive(true);
            Place();
            panel.m_OptionDescriptionLabel.gameObject.SetActive(false);

            input.value = string.Empty;
            lastQuery = null;
            focusNextFrame = false;
            Refilter();
            GameAudioManager.PlayGUIButtonClick();
        }

        private static void Close()
        {
            input.value = string.Empty;
            input.isSelected = false;
            if (UICamera.selectedObject == input.gameObject)
                UICamera.selectedObject = null;
            InputLock.Set(false);
            focusNextFrame = false;
            root.SetActive(false);
            blockUntilFrame = Time.frameCount + 1;
        }

        internal static void ForceClose()
        {
            if (IsOpen)
                Close();
            InputLock.Set(false);
        }

        private static void UpdateOpen()
        {
            // set by the X button, so the box keeps focus after clearing
            if (focusNextFrame)
            {
                focusNextFrame = false;
                Focus();
            }

            if (KeyBlock.Down(KeyCode.Tab))
            {
                if (IsTyping)
                {
                    input.isSelected = false;
                    UICamera.selectedObject = null;
                }
                else
                {
                    Focus();
                }
            }

            bool typing = IsTyping;
            InputLock.Set(typing);
            NguiFactory.SyncPlaceholderFontSize(input);

            // Rewired is off while typing, so read the keyboard directly there
            if (KeyBlock.Down(KeyCode.Escape) || (!typing && InputManager.GetEscapePressed(panel)))
            {
                Close();
                GameAudioManager.PlayGUIButtonClick();
                return;
            }

            if (input.value != lastQuery)
                Refilter();

            if (KeyBlock.Down(KeyCode.Return) || KeyBlock.Down(KeyCode.KeypadEnter)
                || (!typing && InputManager.GetContinuePressed(panel) && !Input.GetMouseButtonDown(0) && !Input.GetMouseButtonUp(0)))
            {
                Pick(highlight);
                return;
            }

            int move = 0;
            if (KeyBlock.Down(KeyCode.UpArrow)) move = -1;
            else if (KeyBlock.Down(KeyCode.DownArrow)) move = 1;
            else if (KeyBlock.Down(KeyCode.PageUp)) move = -VisibleRows;
            else if (KeyBlock.Down(KeyCode.PageDown)) move = VisibleRows;
            // skip the stick/d-pad read while an arrow key is held, or Rewired's menu nav moves it twice
            else if (!typing && !KeyBlock.Held(KeyCode.UpArrow) && !KeyBlock.Held(KeyCode.DownArrow))
            {
                float v = Il2Cpp.Utils.GetMenuMovementVertical(panel, true, true);
                if (v > 0f) move = -1;
                else if (v < 0f) move = 1;
            }

            if (move != 0 && matches.Count > 0)
            {
                highlight = Mathf.Clamp(highlight + move, 0, matches.Count - 1);
                KeepHighlightVisible();
                GameAudioManager.PlayGUIScroll();
                Redraw();
            }

            float wheel = Input.mouseScrollDelta.y;
            if (wheel != 0f)
                ScrollTo(scrollOffset + (wheel > 0f ? -3 : 3));
        }

        private static void ScrollTo(int offset)
        {
            offset = Mathf.Clamp(offset, 0, Mathf.Max(0, matches.Count - VisibleRows));
            if (offset == scrollOffset)
                return;
            scrollOffset = offset;
            Redraw();
        }

        // pointer position in the scroll track's local space (NGUI events give screen pixels)
        private static float PointerY()
        {
            Camera cam = UICamera.currentCamera ?? UICamera.mainCamera;
            Vector3 world = cam.ScreenToWorldPoint(new Vector3(UICamera.lastTouchPosition.x, UICamera.lastTouchPosition.y, 0f));
            return scrollThumb.transform.parent.InverseTransformPoint(world).y;
        }

        private static void OnTrackPress(bool pressed)
        {
            if (!pressed)
                return;
            float y = PointerY(), thumbY = scrollThumb.transform.localPosition.y;
            if (y > thumbY + scrollThumb.height * 0.5f)
                ScrollTo(scrollOffset - VisibleRows);
            else if (y < thumbY - scrollThumb.height * 0.5f)
                ScrollTo(scrollOffset + VisibleRows);
        }

        private static void OnThumbDrag()
        {
            float range = (VisibleRows * RowHeight - scrollThumb.height) * 0.5f;
            if (range <= 0f)
                return;
            float t = Mathf.Clamp01((range - (PointerY() - grabOffset)) / (2f * range));
            ScrollTo(Mathf.RoundToInt(t * (matches.Count - VisibleRows)));
        }

        // Centred on the screen, clamped ~25px (at 1080p) inside its edges. Done in screen space and converted
        // back, so UIRoot scaling, resolution and aspect ratio all come out right. Redone on every open
        // in case the resolution changed.
        private static void Place()
        {
            Camera cam = NGUITools.FindCameraForLayer(root.layer);
            if (cam == null)
                return;

            Transform content = root.transform.parent;
            float pad = 25f * Screen.height / 1080f;
            Vector3 topRight = content.InverseTransformPoint(cam.ScreenToWorldPoint(new Vector3(Screen.width - pad, Screen.height - pad, 0f)));
            Vector3 bottomLeft = content.InverseTransformPoint(cam.ScreenToWorldPoint(new Vector3(pad, pad, 0f)));

            Vector3 middle = content.InverseTransformPoint(cam.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, 0f)));

            float halfW = Width * 0.5f + 2f, halfH = popupHeight * 0.5f + 2f;
            float x = Mathf.Max(Mathf.Min(middle.x, topRight.x - halfW), bottomLeft.x + halfW);
            float y = Mathf.Min(Mathf.Max(middle.y, bottomLeft.y + halfH), topRight.y - halfH);
            root.transform.localPosition = new Vector3(x, y, root.transform.localPosition.z);

            Vector3 center = root.transform.InverseTransformPoint(cam.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, 0f)));
            shade.transform.localPosition = new Vector3(center.x, center.y, 0f);
        }

        private static void Focus()
        {
            input.isSelected = true;
            UICamera.selectedObject = input.gameObject;
        }

        private static void Refilter()
        {
            lastQuery = input.value ?? string.Empty;
            string query = lastQuery.Trim();
            string squashed = query.Replace(" ", "");

            matches.Clear();
            if (query.Length == 0)
            {
                matches.AddRange(allMods);
            }
            else
            {
                // names that start with the query first, then any other match, both in ModSettings' order
                var rest = new List<string>();
                foreach (string mod in allMods)
                {
                    string modSquashed = mod.Replace(" ", "");
                    if (mod.StartsWith(query, StringComparison.OrdinalIgnoreCase) || modSquashed.StartsWith(squashed, StringComparison.OrdinalIgnoreCase))
                        matches.Add(mod);
                    else if (mod.Contains(query, StringComparison.OrdinalIgnoreCase) || modSquashed.Contains(squashed, StringComparison.OrdinalIgnoreCase))
                        rest.Add(mod);
                }
                matches.AddRange(rest);
            }

            highlight = query.Length == 0 ? Mathf.Max(0, matches.IndexOf(selector.value)) : 0;
            scrollOffset = 0;
            KeepHighlightVisible();
            clearButton.SetActive(lastQuery.Length > 0);
            Redraw();
        }

        private static void KeepHighlightVisible()
        {
            if (highlight < scrollOffset)
                scrollOffset = highlight;
            else if (highlight >= scrollOffset + VisibleRows)
                scrollOffset = highlight - VisibleRows + 1;
            scrollOffset = Mathf.Clamp(scrollOffset, 0, Mathf.Max(0, matches.Count - VisibleRows));
        }

        private static void Redraw()
        {
            string current = selector.value;
            for (int i = 0; i < VisibleRows; i++)
            {
                int index = scrollOffset + i;
                bool used = index < matches.Count;
                rowBgs[i].gameObject.SetActive(used || (i == 0 && matches.Count == 0));

                if (!used)
                {
                    if (i == 0 && matches.Count == 0)
                    {
                        rowLabels[0].text = "No matching mods";
                        rowLabels[0].color = NguiFactory.DimTextColor;
                        rowBgs[0].color = RowColor;
                    }
                    continue;
                }

                SetClipped(rowLabels[i], matches[index]);
                rowLabels[i].color = matches[index] == current ? CurrentModColor : NguiFactory.TextColor;
                // by visible row: the first row under the search box is plain, stripes start on the second
                rowBgs[i].color = index == highlight ? RowHighlight : i % 2 == 1 ? RowStripe : RowColor;
            }

            countLabel.text = matches.Count == allMods.Count ? $"{allMods.Count} mods" : $"{matches.Count} of {allMods.Count}";

            bool scrolls = matches.Count > VisibleRows;
            scrollThumb.transform.parent.gameObject.SetActive(scrolls);
            if (scrolls)
            {
                int track = VisibleRows * RowHeight;
                int thumb = Mathf.Max(24, track * VisibleRows / matches.Count);
                float t = (float)scrollOffset / (matches.Count - VisibleRows);
                scrollThumb.height = thumb;
                scrollThumb.GetComponent<BoxCollider>().size = new Vector3(20f, thumb, 1f);
                scrollThumb.transform.localPosition = new Vector3(0f, (track - thumb) * 0.5f - t * (track - thumb), 0f);
            }
        }

        // ClampContent just cuts the text off mid-letter, so end long names with "..." instead
        private static void SetClipped(UILabel label, string text)
        {
            label.text = text;
            if (label.Wrap(text, out string fit) || string.IsNullOrEmpty(fit))
                return;
            string cut = fit.TrimEnd();
            while (cut.Length > 1)
            {
                cut = cut.Substring(0, cut.Length - 1).TrimEnd();
                if (label.Wrap(cut + "...", out _))
                    break;
            }
            label.text = cut + "...";
        }

        private static void Pick(int index)
        {
            if (index < 0 || index >= matches.Count)
                return;

            string mod = matches[index];
            Close();
            GameAudioManager.PlayGUIButtonClick();
            ModSettingsAccess.SelectMod(selector, mod);
        }

        private static void Build()
        {
            Transform content = selector.transform.parent;
            UILabel font = NguiFactory.FindFontSource(selector.m_LableValue, tab.transform);
            UISprite box = selector.transform.Find("Console_Background")?.GetComponent<UISprite>();

            rowBgs.Clear();
            rowLabels.Clear();
            BuildSearchButton(content, font, box);

            int listTop = -117;
            int height = -listTop + (VisibleRows - 1) * RowHeight + 17 + 48;
            float top = height * 0.5f;

            // fallback spot under the mod picker; Place() centres it on the screen when it opens
            Vector3 anchor = content.InverseTransformPoint(box != null ? box.transform.position : selector.transform.position);
            popupHeight = height;
            UIPanel popupPanel = NGUITools.AddChild<UIPanel>(content.gameObject);
            popupPanel.depth = 1000;
            root = popupPanel.gameObject;
            root.name = "ModSettingsSearchPopup";
            root.transform.localPosition = new Vector3(anchor.x, anchor.y - 30f - top, -50f);
            Transform r = root.transform;

            // dims the rest of the menu and eats clicks outside the popup (clicking it closes the popup).
            // Big enough for ultrawide; Place() keeps it centred on the screen.
            shade = NguiFactory.CreateSolid("Shade", r, new Color(0f, 0f, 0f, 0.5f), 10000, 10000, 0);
            NguiFactory.AddCollider(shade.gameObject, 10000, 10000);
            NguiFactory.AddListener(shade.gameObject, Close, null);

            NguiFactory.CreateSolid("Border", r, FrameColor, Width + 4, height + 4, 1);
            UITexture bg = NguiFactory.CreateSolid("Background", r, new Color(0.08f, 0.09f, 0.09f, 1f), Width, height, 2);
            NguiFactory.AddCollider(bg.gameObject, Width, height);
            UITexture header = NguiFactory.CreateSolid("Header", r, new Color(0.14f, 0.15f, 0.15f, 1f), Width, 46, 3);
            header.transform.localPosition = new Vector3(0f, top - 23f, 0f);

            UILabel title = NguiFactory.CreateLabel("Title", r, font, 18, NguiFactory.TextColor, UIWidget.Pivot.Left, 300, 10);
            title.transform.localPosition = new Vector3(-Width * 0.5f + 20f, top - 24f, 0f);
            title.text = "Find a mod page";

            countLabel = NguiFactory.CreateLabel("Count", r, font, 13, NguiFactory.DimTextColor, UIWidget.Pivot.Right, 160, 10);
            countLabel.transform.localPosition = new Vector3(Width * 0.5f - 20f, top - 24f, 0f);

            input = NguiFactory.CreateSearchInput(r, font, new Color(0.24f, 0.26f, 0.26f, 1f), Width - 40, 36, Placeholder, out clearButton);
            input.transform.localPosition = new Vector3(0f, top - 70f, -20f);
            // framed and lighter than the rows so it reads as an input, not another list entry
            UITexture inputFrame = NguiFactory.CreateSolid("SearchFrame", input.transform, new Color(0.7f, 0.72f, 0.72f, 1f), Width - 36, 40, 497);
            inputFrame.transform.localPosition = new Vector3(0f, 0f, 1f);
            UITexture separator = NguiFactory.CreateSolid("Separator", r, FrameColor, Width - 40, 2, 4);
            separator.transform.localPosition = new Vector3(0f, top - 96f, 0f);
            NguiFactory.AddListener(clearButton, () =>
            {
                input.value = string.Empty;
                Refilter();
                focusNextFrame = true;
            }, null);

            int rowWidth = Width - 56;
            for (int i = 0; i < VisibleRows; i++)
            {
                int row = i;
                UITexture rowBg = NguiFactory.CreateSolid("Row" + i, r, RowColor, rowWidth, RowHeight - 4, 5);
                rowBg.transform.localPosition = new Vector3(-8f, top + listTop - i * RowHeight, 0f);
                NguiFactory.AddCollider(rowBg.gameObject, rowWidth, RowHeight - 4);
                NguiFactory.AddListener(rowBg.gameObject, () => Pick(scrollOffset + row), hovered =>
                {
                    if (hovered && scrollOffset + row < matches.Count && highlight != scrollOffset + row)
                    {
                        highlight = scrollOffset + row;
                        Redraw();
                    }
                });

                UILabel label = NguiFactory.CreateLabel("Label", rowBg.transform, font, 16, NguiFactory.TextColor, UIWidget.Pivot.Left, rowWidth - 24, 6);
                label.transform.localPosition = new Vector3(-rowWidth * 0.5f + 12f, 0f, 0f);

                rowBgs.Add(rowBg);
                rowLabels.Add(label);
            }

            int trackHeight = VisibleRows * RowHeight;
            UITexture track = NguiFactory.CreateSolid("ScrollTrack", r, new Color(0.2f, 0.22f, 0.22f, 1f), 6, trackHeight - 4, 5);
            track.transform.localPosition = new Vector3(Width * 0.5f - 24f, top + listTop - (VisibleRows - 1) * RowHeight * 0.5f, 0f);
            scrollThumb = NguiFactory.CreateSolid("Thumb", track.transform, new Color(0.75f, 0.77f, 0.77f, 1f), 6, 40, 6);
            // colliders wider than the 6px bar so it's easy to grab
            NguiFactory.AddCollider(track.gameObject, 20f, trackHeight - 4);
            NguiFactory.OnPress(track.gameObject, OnTrackPress);
            NguiFactory.AddCollider(scrollThumb.gameObject, 20f, 40f);
            NguiFactory.OnPress(scrollThumb.gameObject, pressed =>
            {
                if (pressed)
                    grabOffset = PointerY() - scrollThumb.transform.localPosition.y;
            });
            NguiFactory.OnDrag(scrollThumb.gameObject, OnThumbDrag);

            UILabel hint = NguiFactory.CreateLabel("Hint", r, font, 12, NguiFactory.DimTextColor, UIWidget.Pivot.Center, Width - 40, 10);
            hint.transform.localPosition = new Vector3(0f, -top + 22f, 0f);
            hint.text = "Up/Down to choose, Enter to open, Tab to type, Esc to close";

            root.SetActive(false);
        }

        private static void BuildSearchButton(Transform content, UILabel font, UISprite box)
        {
            const int w = 110, h = 36;
            Transform increase = selector.transform.Find("Button_Increase");

            searchButton = new GameObject("ModSettingsSearchButton");
            searchButton.transform.SetParent(content, false);
            searchButton.layer = content.gameObject.layer;
            Vector3 pos = content.InverseTransformPoint((increase ?? selector.transform).position);
            searchButton.transform.localPosition = new Vector3(pos.x + 50f + w * 0.5f, pos.y, pos.z);

            searchButtonBg = NguiFactory.CreateSprite("Bg", searchButton.transform, box, NguiFactory.BoxColor, w, h, 50);
            UILabel label = NguiFactory.CreateLabel("Label", searchButton.transform, font, 16, NguiFactory.TextColor, UIWidget.Pivot.Center, w - 10, 51);
            label.text = "Search";

            NguiFactory.AddCollider(searchButton, w, h);
            NguiFactory.AddListener(searchButton, () => Open(),
                hovered => searchButtonBg.color = hovered ? RowHighlight : NguiFactory.BoxColor);
        }
    }
}
