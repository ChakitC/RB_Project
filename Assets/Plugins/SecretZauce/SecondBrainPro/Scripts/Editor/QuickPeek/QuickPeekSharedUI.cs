using System.Collections.Generic;
using System.Linq;
using SecretZauce.SecondBrain.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SecretZauce.SecondBrain.Pro.Editor
{
    /// <summary>
    /// Shared UI helpers for the QuickPeek / Container children toolbars and tab bars.
    /// Provides a consistent visual for: Tabs/Foldouts toggle, Expand/Collapse-all foldout arrow and Settings button,
    /// and a horizontally-scrollable tab bar.
    ///
    /// The helpers are intentionally minimal: they draw visuals and return flags indicating which
    /// action the caller should perform (expand/collapse/settings/layout change). Callers remain
    /// responsible for applying preferences, persistence and callbacks.
    /// </summary>
    public static class QuickPeekSharedUI
    {
        // ──────────────────────────── Icon cache ────────────────────────────
        static Texture s_TabIcon;
        static Texture s_FoldoutIcon;
        static Texture s_SettingsIcon;
        static Texture s_ExpandIcon;
        static Texture s_CollapseIcon;
        static bool    s_LayoutIconsProSkin;
        static GUIStyle s_LeftAlignedTabStyle;
        static Texture s_EnterIcon;

        // ──────────────────────────── Prefs-key helpers ────────────────────────────

        /// <summary>
        /// Returns the EditorPrefs key used to persist foldout states for a container asset.
        /// Returns null when the asset cannot be identified.
        /// foldout state is synchronised between windows.
        /// </summary>
        public static string GetFoldoutPrefsKey(Object asset)
        {
            if (asset == null) return null;
            if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset as ScriptableObject, out string guid, out long local))
                return !string.IsNullOrEmpty(guid) ? $"QuickPeek_Foldouts_{guid}_{local}" : null;
            var path = AssetDatabase.GetAssetPath(asset);
            if (!string.IsNullOrEmpty(path))
            {
                var pathGuid = AssetDatabase.AssetPathToGUID(path);
                return !string.IsNullOrEmpty(pathGuid) ? $"QuickPeek_Foldouts_{pathGuid}" : null;
            }
            return null;
        }

        /// <summary>
        /// Returns the EditorPrefs key used to persist the selected tab index for a container asset.
        /// Returns null when the asset cannot be identified.
        /// </summary>
        public static string GetTabPrefsKey(Object asset)
        {
            if (asset == null) return null;
            if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset as ScriptableObject, out string guid, out long local))
                return !string.IsNullOrEmpty(guid) ? $"QuickPeek_Tabs_{guid}_{local}" : null;
            var path = AssetDatabase.GetAssetPath(asset);
            if (!string.IsNullOrEmpty(path))
            {
                var pathGuid = AssetDatabase.AssetPathToGUID(path);
                return !string.IsNullOrEmpty(pathGuid) ? $"QuickPeek_Tabs_{pathGuid}" : null;
            }
            return null;
        }

        // ──────────────────────────── Foldout list helpers ─────────────────────────

        /// <summary>
        /// Sets all entries in <paramref name="foldoutStates"/> to <paramref name="value"/>,
        /// growing the list to <paramref name="count"/> items if necessary.
        /// </summary>
        public static void SetAllFoldouts(List<bool> foldoutStates, int count, bool value)
        {
            if (foldoutStates == null) return;
            for (int i = 0; i < count; i++)
            {
                if (i >= foldoutStates.Count)
                    foldoutStates.Add(value);
                else
                    foldoutStates[i] = value;
            }
        }
        // ──────────────────────── Container content drawing ───────────────────────

        /// <summary>
        /// Options that control how the foldout list is rendered in <see cref="DrawFoldoutsContent"/>.
        /// Use the static preset properties for the two standard window styles.
        /// </summary>
        public struct FoldoutListOptions
        {
            /// <summary>Horizontal padding added on each side of the expanded editor (ignored when <see cref="UseIndentLevel"/> is true).</summary>
            public float ContentPadding;
            /// <summary>If true, a thin separator line is drawn below each header and below expanded content.</summary>
            public bool ShowSeparators;
            /// <summary>If true, <c>EditorGUI.indentLevel</c> is incremented for content instead of padding.</summary>
            public bool UseIndentLevel;
            /// <summary>Extra space added below a collapsed foldout row (0 = no spacing).</summary>
            public float CollapsedSpacing;
            /// <summary>If true, the outer scroll view suppresses the horizontal scrollbar.</summary>
            public bool SuppressHorizontalScroll;

            /// <summary>Compact preset for the floating <c>QuickPeekWindow</c> (padded, no separators, suppressed h-scroll).</summary>
            public static FoldoutListOptions QuickPeek => new FoldoutListOptions
            {
                ContentPadding = 14f, ShowSeparators = false, UseIndentLevel = false,
                CollapsedSpacing = 0f, SuppressHorizontalScroll = true
            };

            public static FoldoutListOptions EditorWindow => new FoldoutListOptions
            {
                ContentPadding = 14f, ShowSeparators = true, UseIndentLevel = false,
                CollapsedSpacing = 6f, SuppressHorizontalScroll = false
            };
        }

        /// <summary>
        /// Draws a padded, scrollable inspector for the currently selected tab.
        /// </summary>
        /// <param name="editors">Parallel list of editors matching the tab items.</param>
        /// <param name="selectedTab">Index of the active tab.</param>
        /// <param name="scrollPos">Scroll position (updated in place).</param>
        /// <param name="padding">Horizontal padding on each side of the inspector.</param>
        /// <param name="suppressHorizontalScroll">When true, the horizontal scrollbar is hidden (use in fixed-width popup windows).</param>
        /// <param name="verticalSpacing">Extra space added above and below the inspector (0 = no spacing).</param>
        public static void DrawTabContent(
            List<UnityEditor.Editor> editors, int selectedTab, ref Vector2 scrollPos,
            float padding = 14f, bool suppressHorizontalScroll = false, float verticalSpacing = 4f)
        {
            scrollPos = suppressHorizontalScroll
                ? EditorGUILayout.BeginScrollView(scrollPos, false, false, GUIStyle.none, GUI.skin.verticalScrollbar, GUIStyle.none)
                : EditorGUILayout.BeginScrollView(scrollPos);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(padding);
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            if (verticalSpacing > 0f) GUILayout.Space(verticalSpacing);
            if (selectedTab >= 0 && selectedTab < editors.Count && editors[selectedTab] != null)
                editors[selectedTab].OnInspectorGUI();
            if (verticalSpacing > 0f) GUILayout.Space(verticalSpacing);
            EditorGUILayout.EndVertical();
            GUILayout.Space(padding);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndScrollView();
        }

        /// <summary>
        /// Draws a scrollable foldout list for <paramref name="children"/> / <paramref name="editors"/>.
        /// Returns <c>true</c> if any foldout state changed during this call.
        /// </summary>
        /// <param name="animationState">Optional animation state for smooth expand/collapse transitions.</param>
        public static bool DrawFoldoutsContent(
            List<Object> children, List<UnityEditor.Editor> editors, List<bool> foldoutStates,
            ref Vector2 scrollPos, FoldoutListOptions options, FoldoutAnimationState animationState = null)
        {
            bool changed = false;

            scrollPos = options.SuppressHorizontalScroll
                ? EditorGUILayout.BeginScrollView(scrollPos, false, false, GUIStyle.none, GUI.skin.verticalScrollbar, GUIStyle.none)
                : EditorGUILayout.BeginScrollView(scrollPos);

            Color sepColor = EditorGUIUtility.isProSkin
                ? new Color(0.12f, 0.12f, 0.12f, 1f)
                : new Color(0.72f, 0.72f, 0.72f, 1f);

            for (int i = 0; i < children.Count; i++)
            {
                var child = children[i];
                if (child == null) continue;
                if (i >= foldoutStates.Count) foldoutStates.Add(false);

                // Check if this item should be a non-expandable row (Container or SceneAsset)
                bool isContainerChild = child is IStructure;
                bool isSceneAssetChild = child is SceneAsset;
                bool isNavigationItem = isContainerChild || isSceneAssetChild;
                bool isSceneComponentRef = child is SceneComponentRef;

                // ── Draw item row ──
                float headerHeight = EditorGUIUtility.singleLineHeight + 6f;
                Rect headerRect = GUILayoutUtility.GetRect(0, headerHeight, GUILayout.ExpandWidth(true));
                Color headerBg = EditorGUIUtility.isProSkin
                    ? new Color(0.16f, 0.16f, 0.16f, 1f)
                    : new Color(0.83f, 0.83f, 0.83f, 1f);
                EditorGUI.DrawRect(headerRect, headerBg);

                if (isNavigationItem)
                {
                    // ── Non-expandable row with ">" button (Container or SceneAsset) ──
                    float buttonWidth = 20f;
                    float buttonPadding = 4f;
                    Rect buttonRect = new Rect(headerRect.xMax - buttonWidth - buttonPadding, 
                        headerRect.y + (headerRect.height - buttonWidth) / 2f, buttonWidth, buttonWidth);

                    // Draw icon + label (bold, no foldout arrow)
                    var labelStyle = new GUIStyle(EditorStyles.boldLabel)
                    {
                        alignment = TextAnchor.MiddleLeft
                    };
                    float xOff = headerRect.x + 4;
                    Texture navIcon = ItemUtils.GetIconForNode(child);
                    if (navIcon != null)
                    {
                        float yCenter = headerRect.y + headerRect.height * 0.5f;
                        GUI.DrawTexture(new Rect(xOff, yCenter - 8f, 16f, 16f), navIcon);
                        xOff += 18f;
                    }
                    Rect labelRect = new Rect(xOff, headerRect.y,
                        headerRect.xMax - xOff - buttonWidth - buttonPadding - 4f, headerRect.height);
                    EditorGUI.LabelField(labelRect, ItemUtils.GetDisplayName(child), labelStyle);

                    // Draw ">" button
                    var arrowStyle = new GUIStyle(GUI.skin.label)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        fontSize = 16,
                        fontStyle = FontStyle.Bold
                    };
                    
                    bool isHoveringButton = Event.current != null && buttonRect.Contains(Event.current.mousePosition);
                    arrowStyle.normal.textColor = isHoveringButton 
                        ? new Color(1f, 1f, 1f, 1f) 
                        : new Color(0.6f, 0.6f, 0.6f, 0.6f);
                    
                    if (isHoveringButton)
                    {
                        EditorGUI.DrawRect(buttonRect, new Color(0.3f, 0.3f, 0.3f, 0.5f));
                    }
                    
                    Rect btnRect = buttonRect;
                    btnRect.y -= 1;
                    string tooltip = isContainerChild ? "Open in Container Children Editor" : "Open Scene";
                    if (GUI.Button(btnRect, new GUIContent(s_EnterIcon) { tooltip = tooltip }, arrowStyle))
                    {
                        try
                        {
                            if (isContainerChild)
                            {
                                ContainerChildrenInspector.Open((IStructure)child);
                            }
                            else // isSceneAssetChild
                            {
                                string scenePath = AssetDatabase.GetAssetPath(child);
                                if (!string.IsNullOrEmpty(scenePath))
                                {
                                    var notif = new GUIContent("Open Scene: " + child.name);
                                    EditorGUIUtils.ShowNotificationOnActiveView(notif);
                                    EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                                    EditorGUIUtils.FocusHierarchyWindowIfPresent();
                                }
                            }
                        }
                        catch (System.Exception ex)
                        {
                            Debug.LogWarning($"[QuickPeek] Failed to open: {ex.Message}");
                        }
                        Event.current?.Use();
                    }

                    if (options.ShowSeparators)
                        EditorGUI.DrawRect(new Rect(headerRect.x, headerRect.yMax - 1, headerRect.width, 1), sepColor);

                    // Skip to next item - no expandable content for navigation items
                    continue;
                }

                // ── Normal expandable foldout (for non-Container, non-SceneAsset items) ──
                if (isSceneComponentRef)
                {
                    var componentRef = (SceneComponentRef)child;
                    var resolvedComponent = SceneObjectMap.Resolve(componentRef.sceneComponent);

                    float xOffset = headerRect.x + 4f;
                    float yCenter = headerRect.y + headerRect.height * 0.5f;
                    float lineHeight = EditorGUIUtility.singleLineHeight;

                    float foldoutWidth = 12f;
                    Rect foldoutRect = new Rect(xOffset, yCenter - lineHeight * 0.5f, foldoutWidth, lineHeight);
                    xOffset += foldoutWidth + 2f;

                    Texture2D componentIcon = null;
                    try
                    {
                        if (resolvedComponent != null)
                        {
                            var iconContent = EditorGUIUtility.ObjectContent(resolvedComponent, resolvedComponent.GetType());
                            if (iconContent != null)
                                componentIcon = iconContent.image as Texture2D;
                        }
                    }
                    catch { }

                    if (componentIcon != null)
                    {
                        Rect iconRect = new Rect(xOffset, yCenter - 8f, 16f, 16f);
                        GUI.DrawTexture(iconRect, componentIcon);
                        xOffset += 18f;
                    }

                    if (resolvedComponent is Behaviour behaviour)
                    {
                        Rect enableRect = new Rect(xOffset, yCenter - lineHeight * 0.5f, 16f, lineHeight);
                        bool wasEnabled = behaviour.enabled;
                        bool isEnabled = EditorGUI.Toggle(enableRect, wasEnabled);
                        if (isEnabled != wasEnabled)
                        {
                            Undo.RecordObject(behaviour, "Toggle Component Enabled");
                            behaviour.enabled = isEnabled;
                            EditorUtility.SetDirty(behaviour);
                        }
                        xOffset += 18f;
                    }

                    string componentLabel = GetSceneComponentFoldoutLabel(componentRef, resolvedComponent);
                    float labelWidth = headerRect.width - (xOffset - headerRect.x) - 8f;
                    Rect labelRect = new Rect(xOffset, yCenter - lineHeight * 0.5f, Mathf.Max(0f, labelWidth), lineHeight);

                    bool componentPrev = foldoutStates[i];
                    bool componentNow = EditorGUI.Foldout(foldoutRect, componentPrev, GUIContent.none, true);
                    if (componentNow != componentPrev)
                    {
                        foldoutStates[i] = componentNow;
                        changed = true;
                    }

                    var labelStyle = new GUIStyle(EditorStyles.boldLabel);
                    GUI.Label(labelRect, componentLabel, labelStyle);

                    if (options.ShowSeparators)
                        EditorGUI.DrawRect(new Rect(headerRect.x, headerRect.yMax - 1, headerRect.width, 1), sepColor);

                    float fadedComponent = 1f;
                    if (animationState != null)
                    {
                        var animBool = animationState.GetOrCreate(child, foldoutStates[i]);
                        animBool.target = foldoutStates[i];
                        fadedComponent = animBool.faded;
                    }

                    if (fadedComponent <= 0f)
                        continue;

                    bool componentShouldShowContent = foldoutStates[i] && i < editors.Count && editors[i] != null;
                    bool componentGroupVisible = animationState != null
                        ? EditorGUILayout.BeginFadeGroup(fadedComponent)
                        : componentShouldShowContent;

                    if (componentGroupVisible && componentShouldShowContent)
                    {
                        if (options.UseIndentLevel)
                        {
                            EditorGUI.indentLevel++;
                            EditorGUILayout.BeginVertical();
                            editors[i].OnInspectorGUI();
                            EditorGUILayout.EndVertical();
                            EditorGUI.indentLevel--;
                        }
                        else
                        {
                            EditorGUILayout.BeginHorizontal();
                            GUILayout.Space(options.ContentPadding);
                            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
                            GUILayout.Space(4f);
                            editors[i].OnInspectorGUI();
                            GUILayout.Space(4f);
                            EditorGUILayout.EndVertical();
                            GUILayout.Space(options.ContentPadding);
                            EditorGUILayout.EndHorizontal();
                        }

                        if (options.ShowSeparators)
                        {
                            Rect sepRect = GUILayoutUtility.GetRect(0, 4, GUILayout.ExpandWidth(true));
                            sepRect.height = 1;
                            EditorGUI.DrawRect(sepRect, sepColor);
                        }
                    }

                    if (animationState != null)
                        EditorGUILayout.EndFadeGroup();
                    else if (!componentShouldShowContent && options.CollapsedSpacing > 0f)
                        GUILayout.Space(options.CollapsedSpacing);

                    continue;
                }

                var headerStyle = new GUIStyle(EditorStyles.foldout)
                {
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleLeft,
                    imagePosition = ImagePosition.ImageLeft
                };

                Texture defaultIcon = ItemUtils.GetIconForNode(child);
                var defaultFoldContent = new GUIContent(ItemUtils.GetDisplayName(child), defaultIcon);
                Rect foldRect = new Rect(headerRect.x + 4, headerRect.y + 3,
                    headerRect.width - 8, EditorGUIUtility.singleLineHeight);
                bool defaultPrev = foldoutStates[i];
                bool defaultNow = EditorGUI.Foldout(foldRect, defaultPrev, defaultFoldContent, true, headerStyle);
                if (defaultNow != defaultPrev)
                {
                    foldoutStates[i] = defaultNow;
                    changed = true;
                }

                if (options.ShowSeparators)
                    EditorGUI.DrawRect(new Rect(headerRect.x, headerRect.yMax - 1, headerRect.width, 1), sepColor);

                // ── Foldout expand / collapse animation ──
                float faded = 1f;
                if (animationState != null)
                {
                    var animBool = animationState.GetOrCreate(child, foldoutStates[i]);
                    animBool.target = foldoutStates[i];
                    faded = animBool.faded;
                }

                // Skip content if fully collapsed and not mid-animation
                if (faded <= 0f)
                    continue;

                // ── Content ──
                bool defaultShouldShowContent = foldoutStates[i] && i < editors.Count && editors[i] != null;
                
                // Begin animated fade group when animation is enabled
                bool defaultGroupVisible = animationState != null
                    ? EditorGUILayout.BeginFadeGroup(faded)
                    : defaultShouldShowContent;

                if (defaultGroupVisible && defaultShouldShowContent)
                {
                    if (options.UseIndentLevel)
                    {
                        EditorGUI.indentLevel++;
                        EditorGUILayout.BeginVertical();
                        editors[i].OnInspectorGUI();
                        EditorGUILayout.EndVertical();
                        EditorGUI.indentLevel--;
                    }
                    else
                    {
                        EditorGUILayout.BeginHorizontal();
                        GUILayout.Space(options.ContentPadding);
                        EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
                        GUILayout.Space(4f);
                        editors[i].OnInspectorGUI();
                        GUILayout.Space(4f);
                        EditorGUILayout.EndVertical();
                        GUILayout.Space(options.ContentPadding);
                        EditorGUILayout.EndHorizontal();
                    }

                    if (options.ShowSeparators)
                    {
                        Rect sepRect = GUILayoutUtility.GetRect(0, 4, GUILayout.ExpandWidth(true));
                        sepRect.height = 1;
                        EditorGUI.DrawRect(sepRect, sepColor);
                    }
                }
                
                // End animated fade group when animation is enabled
                if (animationState != null)
                    EditorGUILayout.EndFadeGroup();
                else if (!defaultShouldShowContent && options.CollapsedSpacing > 0f)
                {
                    GUILayout.Space(options.CollapsedSpacing);
                }
            }

            EditorGUILayout.EndScrollView();
            return changed;
        }

        static string GetSceneComponentFoldoutLabel(SceneComponentRef sceneComponentRef, Component resolvedComponent)
        {
            if (resolvedComponent != null)
                return resolvedComponent.GetType().Name;

            string fallback = sceneComponentRef?.sceneComponent?.LastKnownComponentTypeName;
            if (!string.IsNullOrEmpty(fallback))
                return fallback;

            string assetName = sceneComponentRef != null ? sceneComponentRef.name : "Scene Component";
            return TrimParenthesizedSuffix(assetName);
        }

        static string TrimParenthesizedSuffix(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "Scene Component";

            int idx = value.LastIndexOf(" (", System.StringComparison.Ordinal);
            if (idx > 0 && value.EndsWith(")"))
                return value.Substring(0, idx);

            return value;
        }

        public struct ToolbarResult
        {
            public int NewLayout; // 0 = Tabs, 1 = Foldouts
            public bool LayoutChanged;
            public bool ExpandAllClicked;
            public bool CollapseAllClicked;
            public bool SettingsClicked;
        }

        /// <summary>
        /// Draws inline right-side controls with optional Tabs/Foldouts toggle,
        /// expand/collapse and settings buttons.
        /// Caller owns the header row and should place these controls on the right side.
        /// Returns a ToolbarResult describing user actions.
        /// </summary>
        public static ToolbarResult DrawLayoutToolbar(int layoutMode, bool showLayoutToggle, List<bool> foldoutStates)
        {
            var res = new ToolbarResult { NewLayout = layoutMode, LayoutChanged = false, ExpandAllClicked = false, CollapseAllClicked = false, SettingsClicked = false };

            float tbHeight = EditorStyles.toolbar.fixedHeight;
            float btnSize = tbHeight;
            GUIStyle squareStyle = new GUIStyle(EditorStyles.toolbarButton)
            {
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(0, 0, 0, 0)
            };

            bool ps = EditorGUIUtility.isProSkin;
            if (s_TabIcon == null || s_LayoutIconsProSkin != ps)
            {
                s_LayoutIconsProSkin = ps;
                s_TabIcon     = IconUtils.Load("layout_tab");
                s_FoldoutIcon = IconUtils.Load("layout_foldout");
                s_EnterIcon =  IconUtils.Load("enter");
                s_SettingsIcon = IconUtils.Load("settings");
                s_ExpandIcon  = IconUtils.Load("expand");
                s_CollapseIcon = IconUtils.Load("collapse");
            }
            var tabIconContent  = s_TabIcon     != null ? new GUIContent(s_TabIcon,     "Tabs")     : new GUIContent("T", "Tabs");
            var foldIconContent = s_FoldoutIcon != null ? new GUIContent(s_FoldoutIcon, "Foldouts") : new GUIContent("F", "Foldouts");

            if (showLayoutToggle)
            {
                GUIContent[] modeContents = new[] { tabIconContent, foldIconContent };
                int newLayout = GUILayout.Toolbar(layoutMode, modeContents, squareStyle, GUILayout.Width(btnSize * 2 + 2), GUILayout.Height(btnSize));
                if (newLayout != layoutMode)
                {
                    res.NewLayout = newLayout;
                    res.LayoutChanged = true;
                }
            }

            GUILayout.Space(2f);
            Rect toggleRect = GUILayoutUtility.GetRect(btnSize - 4, btnSize - 8);
            bool allExpanded = foldoutStates != null && foldoutStates.Count > 0 && foldoutStates.All(s => s);
            if (layoutMode == 1)
            {
                Texture foldIcon = allExpanded ? s_CollapseIcon : s_ExpandIcon;
                string  foldTip  = allExpanded ? "Collapse All" : "Expand All";
                if (foldIcon != null)
                {
                    if (GUI.Button(toggleRect, new GUIContent(foldIcon, foldTip), squareStyle))
                    {
                        if (allExpanded) res.CollapseAllClicked = true;
                        else res.ExpandAllClicked = true;
                    }
                }
                else
                {
                    var fc = new GUIContent(string.Empty, foldTip);
                    bool newAll = EditorGUI.Foldout(toggleRect, allExpanded, fc, true);
                    if (newAll != allExpanded)
                    {
                        if (newAll) res.ExpandAllClicked = true;
                        else res.CollapseAllClicked = true;
                    }
                }
            }

            GUILayout.Space(6f);
            GUIContent settingsContent = s_SettingsIcon != null
                ? new GUIContent(s_SettingsIcon, "Settings")
                : EditorGUIUtility.IconContent("d_SettingsIcon");
            if (GUILayout.Button(settingsContent, EditorStyles.toolbarButton, GUILayout.Width(24)))
                res.SettingsClicked = true;

            return res;
        }

        /// <summary>
        /// Draws a horizontally-scrollable tab bar and returns the selected index.
        /// </summary>
        public static int DrawTabsBar(int selectedTab, string[] tabNames, ref Vector2 tabScroll)
        {
            var contents = tabNames != null
                ? System.Array.ConvertAll(tabNames, n => new GUIContent(n ?? string.Empty))
                : new GUIContent[0];
            return DrawTabsBar(selectedTab, contents, ref tabScroll);
        }

        /// <summary>
        /// Draws a tab bar with icon+label GUIContent and returns the selected index.
        /// All tabs are given equal proportional width so every tab is visible without scrolling.
        /// </summary>
        public static int DrawTabsBar(int selectedTab, GUIContent[] tabContents, ref Vector2 tabScroll)
        {
            if (tabContents == null || tabContents.Length == 0)
                return -1;

            GUIStyle tabStyle = GetLeftAlignedTabStyle();
            const float maxTabWidth = 200f;
            const float minTabWidth = 40f;

            // Proportional width: divide available view width evenly so all tabs are simultaneously visible.
            float viewWidth = EditorGUIUtility.currentViewWidth;
            float perTabWidth = Mathf.Clamp(viewWidth / tabContents.Length, minTabWidth, maxTabWidth);

            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            int newTab = selectedTab;
            for (int i = 0; i < tabContents.Length; i++)
            {
                var content = tabContents[i] ?? GUIContent.none;
                bool hasIcon = content.image != null;
                float iconOffset = hasIcon ? 18f : 0f;
                string fullText = content.text ?? string.Empty;
                string displayText = GetEllipsizedTabLabel(fullText, tabStyle, perTabWidth - iconOffset);
                var drawContent = new GUIContent(displayText, content.image, fullText);
                bool isSelected = i == selectedTab;

                bool pressed = GUILayout.Toggle(isSelected, drawContent, tabStyle, GUILayout.Width(perTabWidth));
                if (pressed && !isSelected)
                    newTab = i;
            }
            EditorGUILayout.EndHorizontal();
            return newTab;
        }

        static GUIStyle GetLeftAlignedTabStyle()
        {
            if (s_LeftAlignedTabStyle != null)
                return s_LeftAlignedTabStyle;

            s_LeftAlignedTabStyle = new GUIStyle(EditorStyles.toolbarButton)
            {
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Clip,
                wordWrap = false
            };

            return s_LeftAlignedTabStyle;
        }

        static string GetEllipsizedTabLabel(string text, GUIStyle style, float maxTabWidth)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            float availableWidth = Mathf.Max(0f, maxTabWidth - style.padding.horizontal - 8f);
            if (style.CalcSize(new GUIContent(text)).x <= availableWidth)
                return text;

            const string ellipsis = "...";
            if (style.CalcSize(new GUIContent(ellipsis)).x >= availableWidth)
                return ellipsis;

            int low = 0;
            int high = text.Length;
            while (low < high)
            {
                int mid = (low + high + 1) / 2;
                string candidate = text.Substring(0, mid) + ellipsis;
                if (style.CalcSize(new GUIContent(candidate)).x <= availableWidth)
                    low = mid;
                else
                    high = mid - 1;
            }

            return low <= 0 ? ellipsis : text.Substring(0, low) + ellipsis;
        }

    }
}
