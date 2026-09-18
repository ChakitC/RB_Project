using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ZLZ.Hub
{
    // ─────────────────────────────────────────────────────────────────────────
    //  The one window every ZLZ package opens into.
    //
    //  Layout is 16:9 so the Asset Store key art can be reused as-is : a rail of
    //  product cards on the left (which is what keeps the rest of the line-up in
    //  view without a single advertising banner), and the selected product's
    //  work area on the right.
    //
    //  Built with inline UI Toolkit styles instead of .uxml/.uss on purpose. The
    //  package has to survive a customer moving or renaming its folder, and a
    //  stylesheet resolved by path is the first thing that breaks when they do.
    // ─────────────────────────────────────────────────────────────────────────
    public class ZLZ_HubWindow : EditorWindow
    {
        public enum Tab { Setup, Support }

        const float k_RailWidth        = 172f;
        const float k_CardBannerHeight = 104f;   // 3:2 against the ~156px card at the narrowest rail

        // The work area stops widening past this. Beyond it the banner degenerated
        // into a 6:1 sliver of hair and the matrix scattered its checkboxes against
        // the far edge with a canyon of empty space in the middle.
        const float k_ContentMaxWidth = 1400f;

        // Hero height follows its own width, so key art keeps the proportions it was
        // drawn at instead of being cropped to whatever band is left over.
        const float k_HeroAspect    = 4.27f;
        const float k_HeroMinHeight = 200f;
        const float k_HeroMaxHeight = 330f;

        // Wide enough for the shortened renderer names ("HighFidelity", "Performant")
        // at 10px, so a header never has to be cut. Past what the pane can hold the
        // table scrolls sideways instead of squeezing the text away.
        const float k_ColWidth  = 92f;
        const float k_NameWidth = 190f;

        [SerializeField] string _selectedId;
        [SerializeField] Tab    _tab = Tab.Setup;

        List<ZLZ_RendererEntry> _renderers = new List<ZLZ_RendererEntry>();

        // What the customer has ticked, which is not the same as what is installed.
        // Nothing is written to a renderer asset until Install is pressed, so the
        // recommendation can be reviewed and overridden before anything changes.
        readonly Dictionary<string, bool> _desired = new Dictionary<string, bool>();
        string _desiredProductId;

        static string DesiredKey(ZLZ_HubFeature f, ZLZ_RendererEntry r)
            => (f.FeatureType != null ? f.FeatureType.FullName : f.DisplayName) + "|" + r.Path;

        // Seeds anything not ticked yet. On a project that has never been set up the
        // seed is the recommendation; after that it mirrors what is really installed,
        // so reopening the window never proposes changes on its own.
        void SyncDesired(ZLZ_HubProduct p)
        {
            if (_desiredProductId != p.Id) { _desired.Clear(); _desiredProductId = p.Id; }

            bool neverConfigured = !IsConfigured(p);

            foreach (var f in p.GetFeatures())
            foreach (var r in _renderers)
            {
                string key = DesiredKey(f, r);
                if (_desired.ContainsKey(key)) continue;

                // Presence, not "switched on". Several ZLZ features are installed and
                // deliberately left off; that is a finished install, not a missing one.
                bool present = ZLZ_HubUrp.IsPresent(r.Data, f.FeatureType);
                _desired[key] = present || (neverConfigured && r.IsRecommended && f.Recommended);
            }
        }

        bool Desired(ZLZ_HubFeature f, ZLZ_RendererEntry r)
            => _desired.TryGetValue(DesiredKey(f, r), out var v) && v;

        void CollectPending(ZLZ_HubProduct p,
                            List<(ZLZ_RendererEntry r, ZLZ_HubFeature f)> add,
                            List<(ZLZ_RendererEntry r, ZLZ_HubFeature f)> remove)
        {
            foreach (var f in p.GetFeatures())
            foreach (var r in _renderers)
            {
                bool present = ZLZ_HubUrp.IsPresent(r.Data, f.FeatureType);
                bool want    = Desired(f, r);

                if (want && !present) add.Add((r, f));
                else if (!want && present) remove.Add((r, f));
            }
        }

        // ── Palette ───────────────────────────────────────────────────────────
        static bool  Pro       => EditorGUIUtility.isProSkin;
        static Color Surface   => Pro ? new Color32(48, 48, 48, 255)  : new Color32(200, 200, 200, 255);
        static Color Panel     => Pro ? new Color32(56, 56, 56, 255)  : new Color32(213, 213, 213, 255);
        static Color Card      => Pro ? new Color32(66, 66, 66, 255)  : new Color32(226, 226, 226, 255);
        static Color Line      => Pro ? new Color32(32, 32, 32, 255)  : new Color32(160, 160, 160, 255);
        static Color TextMain  => Pro ? new Color32(215, 215, 215, 255) : new Color32(28, 28, 28, 255);
        static Color TextDim   => Pro ? new Color32(150, 150, 150, 255) : new Color32(95, 95, 95, 255);
        static Color Accent    => Pro ? new Color32(94, 156, 232, 255)  : new Color32(38, 106, 186, 255);
        static Color AccentBg  => Pro ? new Color32(40, 62, 86, 255)    : new Color32(196, 216, 240, 255);
        static Color Ok        => Pro ? new Color32(122, 192, 126, 255) : new Color32(38, 122, 60, 255);
        static Color Warn      => Pro ? new Color32(226, 174, 74, 255)  : new Color32(150, 100, 10, 255);
        static Color Bad       => Pro ? new Color32(224, 110, 105, 255) : new Color32(168, 44, 40, 255);

        // ── Entry points ──────────────────────────────────────────────────────

        // Window/ZLZ, next to Mask Packer and Shader Optimizer : Unity keeps its own
        // editor windows under Window, and more to the point that is where the
        // already-shipped ZLZ tools live. Priority 0 floats the Hub to the top of
        // the submenu, above the tools it launches.
        // Set the moment ANY code path opens the Hub this editor session. OnEnable below reads it
        // to tell an intentional open apart from Unity's window-layout restore : an EditorWindow
        // left open at shutdown is serialized into the layout and resurrected on the next launch,
        // which made the Hub "pop up on every Unity start" even though no bootstrap code ran.
        // SessionState survives domain reloads but resets when the editor restarts - exactly the
        // lifetime that distinguishes the two cases.
        const string k_OpenedThisSession = "ZLZ_Hub::windowOpenedThisSession";

        [MenuItem("Window/ZLZ/Hub", false, 0)]
        public static ZLZ_HubWindow Open()
        {
            SessionState.SetBool(k_OpenedThisSession, true);
            var w = GetWindow<ZLZ_HubWindow>(false, "ZLZ Hub", true);
            w.titleContent = new GUIContent("ZLZ Hub");
            w.minSize      = new Vector2(960f, 540f);

            // 16:9 on first open. Anything the customer resizes afterwards is
            // theirs to keep, so this only fires while the window is still at
            // Unity's default size.
            if (w.position.width < 900f || w.position.height < 500f)
            {
                Rect main = EditorGUIUtility.GetMainWindowPosition();
                w.position = new Rect(main.x + (main.width - 1280f) * 0.5f,
                                      main.y + (main.height - 720f) * 0.5f,
                                      1280f, 720f);
            }

            w.Show();
            return w;
        }

        public static void Open(string productId, Tab tab)
        {
            var w = Open();
            w._selectedId = productId;
            w._tab        = tab;
            w.Rebuild();
        }

        void CreateGUI() => Rebuild();

        // Kills the layout-resurrected copy. On a fresh editor start SessionState is empty, so an
        // instance whose OnEnable runs before anything called Open() can only be Unity restoring
        // the previous session's layout - close it, the Hub opens on import or from the menu, not
        // because it happened to be open when the editor quit. Mid-session domain reloads re-run
        // OnEnable too, but the flag survives those, so an intentionally opened window stays.
        // The close is deferred : closing a window while the layout is still being built throws.
        void OnEnable()
        {
            if (SessionState.GetBool(k_OpenedThisSession, false)) return;
            // The flag is re-checked when the close actually runs : on a genuinely fresh install
            // the bootstrap's own delayCall may legitimately open the Hub in this same first
            // frame, and it may get the restored instance back from GetWindow - closing it then
            // would swallow the one popup that is supposed to happen.
            EditorApplication.delayCall += () =>
            {
                if (this != null && !SessionState.GetBool(k_OpenedThisSession, false)) Close();
            };
        }

        // Redraws whatever Hub windows are open. The window builds itself once and
        // then sits there, so anything that lands in the project afterwards - a
        // banner texture, an edited changelog, a renderer asset - stayed invisible
        // until someone thought to press Rescan.
        internal static void RefreshOpenWindows()
        {
            foreach (var found in Resources.FindObjectsOfTypeAll<ZLZ_HubWindow>())
            {
                var window = found;
                EditorApplication.delayCall += () =>
                {
                    if (window != null) window.Rebuild();
                };
            }
        }

        // ── Build ─────────────────────────────────────────────────────────────

        void Rebuild()
        {
            ZLZ_HubRegistry.Invalidate();
            _renderers = ZLZ_HubUrp.ScanRenderers();

            var root = rootVisualElement;
            root.Clear();
            root.style.backgroundColor = Surface;
            root.style.flexGrow        = 1f;

            root.Add(BuildHeader());

            // Above everything, spanning the window : this is a project-level problem,
            // not something about whichever product happens to be selected, and it is
            // not dismissible because the layout has to converge sooner or later.
            var migration = BuildMigrationBanner();
            if (migration != null) root.Add(migration);

            var body = VE(FlexDirection.Row);
            body.style.flexGrow = 1f;

            var rail  = BuildRail();
            var right = BuildRight();
            body.Add(rail);
            body.Add(right);

            // The work area takes what it can use and no more; everything left over
            // goes to the rail rather than sitting as empty gutters on both sides of
            // a maximised window. The rail is the product shelf, so space spent there
            // is space spent showing the line-up.
            body.RegisterCallback<GeometryChangedEvent>(evt =>
            {
                float total = evt.newRect.width;
                if (total <= 1f) return;

                float work = Mathf.Clamp(total - k_RailWidth, 0f, k_ContentMaxWidth);
                right.style.width = work;
                rail.style.width  = Mathf.Max(k_RailWidth, total - work);
            });

            root.Add(body);
        }

        VisualElement BuildMigrationBanner()
        {
            var pending = ZLZ_HubMigration.Pending();
            if (pending.Count == 0) return null;

            var bar = VE(FlexDirection.Row);
            bar.style.alignItems      = Align.Center;
            bar.style.paddingLeft     = 12f;
            bar.style.paddingRight    = 12f;
            bar.style.paddingTop      = 9f;
            bar.style.paddingBottom   = 9f;
            bar.style.flexShrink      = 0f;
            bar.style.backgroundColor = new Color(Warn.r, Warn.g, Warn.b, Pro ? 0.16f : 0.22f);
            Border(bar, bottom: 1f);

            var glyph = Lbl("⚠", 15, Warn);
            glyph.style.marginRight = 9f;
            bar.Add(glyph);

            var texts = VE();
            texts.style.flexShrink = 1f;
            texts.Add(Lbl("This project still uses the old ZLZ folder layout", 12, TextMain, FontStyle.Bold));

            string names = string.Join(", ", pending.ConvertAll(m => m.From.Substring("Assets/".Length)));
            var sub = Lbl($"{names}  ·  everything moves into Assets/ZLZ, including your own files inside those folders",
                          10, TextDim);
            sub.style.marginTop = 2f;
            texts.Add(sub);

            bar.Add(texts);
            bar.Add(Spacer());

            var go = Btn("Reorganise folders", () => { ZLZ_HubMigration.Run(); Rebuild(); }, accent: true);
            go.style.height       = 26f;
            go.style.paddingLeft  = 16f;
            go.style.paddingRight = 16f;
            go.style.flexShrink   = 0f;
            bar.Add(go);

            return bar;
        }

        VisualElement BuildHeader()
        {
            var bar = VE(FlexDirection.Row);
            bar.style.height          = 30f;
            bar.style.alignItems      = Align.Center;
            bar.style.paddingLeft     = 10f;
            bar.style.paddingRight    = 10f;
            bar.style.backgroundColor = Panel;
            Border(bar, bottom: 1f);

            var mark = Lbl("Z", 11, Accent, FontStyle.Bold);
            mark.style.backgroundColor         = AccentBg;
            mark.style.width                   = 18f;
            mark.style.height                  = 18f;
            mark.style.unityTextAlign          = TextAnchor.MiddleCenter;
            mark.style.borderTopLeftRadius     = 5f;
            mark.style.borderTopRightRadius    = 5f;
            mark.style.borderBottomLeftRadius  = 5f;
            mark.style.borderBottomRightRadius = 5f;
            bar.Add(mark);

            var title = Lbl("ZLZ Hub", 12, TextMain, FontStyle.Bold);
            title.style.marginLeft = 7f;
            bar.Add(title);

            bar.Add(Spacer());

            var product = Selected();
            if (product != null)
            {
                int done  = CountDone(product);
                int total = CountTotal(product);
                var progress = Lbl($"Setup {done} / {total}", 11, TextDim);
                progress.style.marginRight = 8f;
                bar.Add(progress);
            }

            bar.Add(Btn("Rescan", Rebuild));
            return bar;
        }

        // ── Left rail : the product line-up ───────────────────────────────────

        VisualElement BuildRail()
        {
            var rail = VE();
            rail.style.width           = k_RailWidth;   // recomputed as the window resizes
            rail.style.flexShrink      = 0f;
            rail.style.backgroundColor = Panel;
            rail.style.paddingLeft     = 8f;
            rail.style.paddingRight    = 8f;
            rail.style.paddingTop      = 8f;
            rail.style.paddingBottom   = 8f;
            Border(rail, right: 1f);

            // Cards fill the rail edge to edge. Capping them only moved the empty
            // space from the window's edge into the rail - the same gap, one panel
            // over. With the banner locked to the art's own 3:2 there is nothing left
            // to fit around.
            var column = VE();
            column.style.width    = Length.Percent(100);
            column.style.flexGrow = 1f;

            column.Add(Lbl("Your ZLZ assets", 10, TextDim));

            var scroll = new ScrollView();
            scroll.style.flexGrow  = 1f;
            scroll.style.marginTop = 6f;

            foreach (var p in ZLZ_HubRegistry.Installed) scroll.Add(BuildProductCard(p));
            foreach (var e in ZLZ_HubRegistry.Locked)    scroll.Add(BuildLockedCard(e));

            column.Add(scroll);

            // One button, one job. The menu used to also offer documentation and a
            // chat link, which the Website button already covers.
            var support = Btn("Copy system info", CopySystemInfo);
            support.style.marginTop = 6f;
            support.tooltip = "Copies your Unity, URP and renderer setup. Paste it when you report a problem.";
            column.Add(support);

            rail.Add(column);
            return rail;
        }

        VisualElement BuildProductCard(ZLZ_HubProduct p)
        {
            bool selected = p.Id == SelectedId();
            var  card     = CardBox(selected);
            card.style.marginBottom = 7f;

            card.Add(CardBanner(p.BannerTextureName, selected ? AccentBg : Card));

            var pad = VE();
            pad.style.paddingLeft   = 7f;
            pad.style.paddingRight  = 7f;
            pad.style.paddingTop    = 5f;
            pad.style.paddingBottom = 6f;
            pad.Add(Lbl(p.DisplayName, 11, TextMain, FontStyle.Bold));
            // Status only. The version number is hand-typed until the store check can
            // supply a real one, and a number nobody can vouch for is worse than none.
            pad.Add(Lbl(StatusWord(p), 10, StatusColor(p)));
            card.Add(pad);

            card.RegisterCallback<MouseDownEvent>(_ =>
            {
                _selectedId = p.Id;
                _tab        = Tab.Setup;
                Rebuild();
            });

            return card;
        }

        VisualElement BuildLockedCard(ZLZ_HubCatalogEntry e)
        {
            bool selected = e.Id == SelectedId();
            var  card     = CardBox(selected);
            card.style.marginBottom = 7f;
            card.style.opacity      = 0.82f;

            var banner = CardBanner(e.BannerTextureName, selected ? AccentBg : Card);

            var pill = Lbl("LOCKED", 9, TextDim);
            pill.style.position                = Position.Absolute;
            pill.style.top                     = 5f;
            pill.style.right                   = 5f;
            pill.style.paddingLeft             = 6f;
            pill.style.paddingRight            = 6f;
            pill.style.paddingTop              = 1f;
            pill.style.paddingBottom           = 1f;
            pill.style.backgroundColor         = new Color(0f, 0f, 0f, 0.45f);
            pill.style.borderTopLeftRadius     = 7f;
            pill.style.borderTopRightRadius    = 7f;
            pill.style.borderBottomLeftRadius  = 7f;
            pill.style.borderBottomRightRadius = 7f;
            banner.Add(pill);

            card.Add(banner);

            var pad = VE();
            pad.style.paddingLeft   = 7f;
            pad.style.paddingRight  = 7f;
            pad.style.paddingTop    = 5f;
            pad.style.paddingBottom = 6f;
            pad.Add(Lbl(e.DisplayName, 11, TextDim, FontStyle.Bold));

            // The card is the pitch : a single-package export carries every catalog banner
            // (ZLZ/Common/Textures), so the customer sees what the product looks like, reads
            // what it does, and has both destinations one click away.
            if (!string.IsNullOrEmpty(e.Tagline))
            {
                var tag = Lbl(e.Tagline, 10, TextDim);
                tag.style.whiteSpace   = WhiteSpace.Normal;
                tag.style.marginBottom = 3f;
                pad.Add(tag);
            }

            var links = VE();
            links.style.flexDirection = FlexDirection.Row;

            // The two ↗ labels are the ONLY spots on the card that leave the editor - the arrow
            // is the announcement. They stop the event so the card-level select below never
            // doubles up behind the browser.
            var store = Lbl("Asset Store ↗", 10, Accent);
            store.style.marginRight = 10f;
            store.RegisterCallback<MouseDownEvent>(evt =>
            {
                evt.StopPropagation();
                if (!string.IsNullOrEmpty(e.StoreUrl)) Application.OpenURL(e.StoreUrl);
            });
            links.Add(store);

            if (!string.IsNullOrEmpty(e.WebsiteUrl))
            {
                var site = Lbl("Website ↗", 10, Accent);
                site.RegisterCallback<MouseDownEvent>(evt =>
                {
                    evt.StopPropagation();
                    Application.OpenURL(e.WebsiteUrl);
                });
                links.Add(site);
            }
            pad.Add(links);
            card.Add(pad);

            // Clicking the card SELECTS it, exactly like the installed cards above : the right
            // pane becomes the product page and the customer leaves the editor only through a
            // button that says so. Sending them straight to the store from here read as an ad
            // hijack, and a card nobody dares click sells nothing.
            card.RegisterCallback<MouseDownEvent>(_ =>
            {
                _selectedId = e.Id;
                Rebuild();
            });

            return card;
        }

        // Product page for a catalog entry that is NOT in this project : hero art, tagline and
        // clearly labelled external buttons - the same page shape an installed product gets, so
        // the whole rail behaves as one list. This is the cross-sell surface ; the customer
        // reads what the asset is here and leaves the editor only when THEY press a button.
        VisualElement BuildLockedRight(ZLZ_HubCatalogEntry e)
        {
            var inner = VE();
            inner.style.width    = Length.Percent(100);
            inner.style.flexGrow = 1f;

            var tex  = FindTexture(e.BannerTextureName + "_Wide") ?? FindTexture(e.BannerTextureName);
            var hero = VE();
            hero.style.height     = k_HeroMinHeight;
            hero.style.flexShrink = 0f;
            if (tex != null) ApplyBanner(hero, tex, 0.5f);
            else             hero.style.backgroundColor = AccentBg;

            var strip = VE(FlexDirection.Row);
            strip.style.position        = Position.Absolute;
            strip.style.left            = 0f;
            strip.style.right           = 0f;
            strip.style.bottom          = 0f;
            strip.style.paddingLeft     = 16f;
            strip.style.paddingRight    = 16f;
            strip.style.paddingTop      = 10f;
            strip.style.paddingBottom   = 12f;
            strip.style.alignItems      = Align.Center;
            strip.style.backgroundColor = new Color(0f, 0f, 0f, tex != null ? 0.62f : 0.42f);

            var texts = VE();
            texts.style.flexShrink = 1f;
            texts.Add(Lbl(e.DisplayName, 20, Color.white, FontStyle.Bold));
            if (!string.IsNullOrEmpty(e.Tagline))
            {
                var sub = Lbl(e.Tagline, 12, new Color(1f, 1f, 1f, 0.85f));
                sub.style.marginTop = 3f;
                texts.Add(sub);
            }
            strip.Add(texts);
            strip.Add(Spacer());
            strip.Add(HeroLink("Website",     e.WebsiteUrl));
            strip.Add(HeroLink("Asset Store", e.StoreUrl));
            hero.Add(strip);
            inner.Add(hero);

            var content = new ScrollView();
            content.style.flexGrow      = 1f;
            content.style.paddingLeft   = 12f;
            content.style.paddingRight  = 12f;
            content.style.paddingTop    = 8f;
            content.style.paddingBottom = 12f;

            var note = Lbl("Not installed in this project", 12, Warn, FontStyle.Bold);
            note.style.marginTop = 4f;
            content.Add(note);

            var pitch = Lbl(
                "Get it on the Asset Store and import it into this project - " +
                "this Hub then installs and configures its renderer features from the same window.",
                12, TextDim);
            pitch.style.whiteSpace = WhiteSpace.Normal;
            pitch.style.marginTop  = 6f;
            content.Add(pitch);

            inner.Add(content);

            // Same responsive hero as the installed page (BuildRight registers the identical
            // callback) : without it this hero sat at the 200 px minimum while the installed
            // one grew with the window, and the two pages read as different sizes.
            inner.RegisterCallback<GeometryChangedEvent>(evt =>
            {
                float width = evt.newRect.width;
                if (width <= 1f) return;
                hero.style.height = Mathf.Clamp(width / k_HeroAspect, k_HeroMinHeight, k_HeroMaxHeight);
            });

            return inner;
        }

        // ── Right pane ────────────────────────────────────────────────────────

        VisualElement BuildRight()
        {
            var pane = VE();
            pane.style.flexShrink = 0f;   // width is driven by the body's resize handler

            var product = Selected();
            if (product == null)
            {
                // Selected() only knows installed products ; a locked catalog pick lands on its
                // product page here, with the external links as labelled buttons on the hero.
                var locked = ZLZ_HubRegistry.Locked.FirstOrDefault(x => x.Id == SelectedId());
                if (locked != null)
                {
                    pane.Add(BuildLockedRight(locked));
                    return pane;
                }

                var empty = Lbl("No ZLZ package found in this project.", 12, TextDim);
                empty.style.marginTop  = 40f;
                empty.style.marginLeft = 20f;
                pane.Add(empty);
                return pane;
            }

            var inner = VE();
            inner.style.width    = Length.Percent(100);
            inner.style.flexGrow = 1f;

            var hero = BuildHero(product);
            inner.Add(hero);
            inner.Add(BuildTabs(product));

            var content = new ScrollView();
            content.style.flexGrow      = 1f;
            content.style.paddingLeft   = 12f;
            content.style.paddingRight  = 12f;
            content.style.paddingTop    = 8f;
            content.style.paddingBottom = 12f;

            switch (_tab)
            {
                case Tab.Support: content.Add(BuildSupport(product)); break;
                default:          content.Add(BuildSetup(product));   break;
            }

            inner.Add(content);

            // Layout runs after this method returns, so the hero can only be sized
            // once the column knows how wide it actually is.
            inner.RegisterCallback<GeometryChangedEvent>(evt =>
            {
                float width = evt.newRect.width;
                if (width <= 1f) return;
                hero.style.height = Mathf.Clamp(width / k_HeroAspect, k_HeroMinHeight, k_HeroMaxHeight);
            });

            pane.Add(inner);
            return pane;
        }

        VisualElement BuildHero(ZLZ_HubProduct p)
        {
            // A wide crop of the 16:9 key art if the package ships one, otherwise
            // the 16:9 art itself, cropped around BannerFocus.
            var tex  = FindTexture(p.BannerTextureName + "_Wide") ?? FindTexture(p.BannerTextureName);
            var hero = VE();
            hero.style.height     = k_HeroMinHeight;   // replaced once the column reports its width
            hero.style.flexShrink = 0f;

            if (tex != null) ApplyBanner(hero, tex, p.BannerFocus);
            else             hero.style.backgroundColor = AccentBg;

            // With no art the hero still has to answer "which asset is this?", so
            // the name goes big in the middle rather than leaving a blank slab.
            if (tex == null)
            {
                var placeholder = VE();
                placeholder.style.flexGrow       = 1f;
                placeholder.style.justifyContent = Justify.Center;
                placeholder.style.alignItems     = Align.Center;

                var big = Lbl(p.DisplayName, 26, Accent, FontStyle.Bold);
                placeholder.Add(big);

                if (!string.IsNullOrEmpty(p.Tagline))
                {
                    var tag = Lbl(p.Tagline, 12, TextDim);
                    tag.style.marginTop = 4f;
                    placeholder.Add(tag);
                }

                var hint = Lbl($"Drop a 16:9 texture named  {p.BannerTextureName}  anywhere in the project", 10, TextDim);
                hint.style.marginTop = 10f;
                hint.style.opacity   = 0.7f;
                placeholder.Add(hint);

                hero.Add(placeholder);
            }

            // Text sits on a solid strip rather than straight on the art. Key art
            // is bright in places and the title becomes unreadable without it.
            var strip = VE(FlexDirection.Row);
            strip.style.position        = Position.Absolute;
            strip.style.left            = 0f;
            strip.style.right           = 0f;
            strip.style.bottom          = 0f;
            strip.style.paddingLeft     = 16f;
            strip.style.paddingRight    = 16f;
            strip.style.paddingTop      = 10f;
            strip.style.paddingBottom   = 12f;
            strip.style.alignItems      = Align.Center;
            strip.style.backgroundColor = new Color(0f, 0f, 0f, tex != null ? 0.62f : 0.42f);

            var texts = VE();
            texts.style.flexShrink = 1f;

            // No version badge. The only number available today is typed by hand into
            // the changelog, so it claims "this is the version you have" with nothing
            // backing it - one forgotten edit and it lies to every customer. It comes
            // back once the Asset Store check can supply a number that is actually
            // true.
            var titleRow = VE(FlexDirection.Row);
            titleRow.style.alignItems = Align.Center;
            titleRow.Add(Lbl(p.DisplayName, 20, Color.white, FontStyle.Bold));

            texts.Add(titleRow);

            var sub = Lbl(HeroSubtitle(p), 12, new Color(1f, 1f, 1f, 0.85f));
            sub.style.marginTop = 3f;
            texts.Add(sub);

            strip.Add(texts);
            strip.Add(Spacer());

            // Not action buttons. Installing belongs under the table, next to the rows
            // it changes ; a second Install up here only ever meant "the same thing,
            // somewhere else". The banner is the product, so its buttons go where the
            // product lives.
            strip.Add(HeroLink("Website",     p.WebsiteUrl));
            strip.Add(HeroLink("Asset Store", p.StoreUrl));

            hero.Add(strip);
            return hero;
        }

        static Color StatusTint(ZLZ_CheckStatus s)
            => s == ZLZ_CheckStatus.Ok ? Ok : s == ZLZ_CheckStatus.Warning ? Warn : Bad;

        // Small pill naming one asset and how it fared.
        static VisualElement Chip(string text, ZLZ_CheckStatus status, string tooltip)
        {
            Color tint = StatusTint(status);

            var l = Lbl(text, 10, tint);
            l.style.marginRight    = 5f;
            l.style.marginBottom   = 2f;
            l.style.paddingLeft    = 7f;
            l.style.paddingRight   = 7f;
            l.style.paddingTop     = 1f;
            l.style.paddingBottom  = 1f;
            l.style.whiteSpace     = WhiteSpace.NoWrap;
            l.style.backgroundColor = new Color(tint.r, tint.g, tint.b, 0.13f);

            l.style.borderTopWidth    = 1f;
            l.style.borderBottomWidth = 1f;
            l.style.borderLeftWidth   = 1f;
            l.style.borderRightWidth  = 1f;
            l.style.borderTopColor    = new Color(tint.r, tint.g, tint.b, 0.5f);
            l.style.borderBottomColor = new Color(tint.r, tint.g, tint.b, 0.5f);
            l.style.borderLeftColor   = new Color(tint.r, tint.g, tint.b, 0.5f);
            l.style.borderRightColor  = new Color(tint.r, tint.g, tint.b, 0.5f);

            l.style.borderTopLeftRadius     = 8f;
            l.style.borderTopRightRadius    = 8f;
            l.style.borderBottomLeftRadius  = 8f;
            l.style.borderBottomRightRadius = 8f;

            if (!string.IsNullOrEmpty(tooltip)) l.tooltip = tooltip;
            return l;
        }

        static VisualElement HeroLink(string label, string url)
        {
            if (string.IsNullOrEmpty(url)) return new VisualElement();

            var b = Btn(label, () => OpenLink(url));
            b.style.height       = 28f;
            b.style.fontSize     = 12;
            b.style.paddingLeft  = 14f;
            b.style.paddingRight = 14f;
            b.style.marginLeft   = 6f;
            b.style.flexShrink   = 0f;
            b.tooltip            = url;
            return b;
        }

        string HeroSubtitle(ZLZ_HubProduct p)
            => IsConfigured(p)
                 ? (string.IsNullOrEmpty(p.Tagline) ? "Installed" : p.Tagline)
                 : "Files imported · renderer features not configured";

        VisualElement BuildTabs(ZLZ_HubProduct p)
        {
            var row = VE(FlexDirection.Row);
            row.style.paddingLeft   = 12f;
            row.style.paddingTop    = 8f;
            row.style.flexShrink    = 0f;
            Border(row, bottom: 1f);

            AddTab(row, "Setup",   Tab.Setup);
            AddTab(row, "Support", Tab.Support);
            return row;
        }

        void AddTab(VisualElement parent, string label, Tab tab)
        {
            bool on  = _tab == tab;
            var  lbl = Lbl(label, 11, on ? Accent : TextDim);
            lbl.style.marginRight            = 14f;
            lbl.style.paddingBottom          = 5f;
            lbl.style.borderBottomWidth      = on ? 2f : 0f;
            lbl.style.borderBottomColor      = Accent;
            lbl.RegisterCallback<MouseDownEvent>(_ => { _tab = tab; Rebuild(); });
            parent.Add(lbl);
        }

        // ── Setup tab ─────────────────────────────────────────────────────────

        // Stacked, not side by side. Two columns split the width that the renderer
        // matrix needs most : a project with several URP renderers ran out of room
        // and started clipping both the column headers and the feature names. Full
        // width for the table, checklist underneath, page scrolls.
        VisualElement BuildSetup(ZLZ_HubProduct p)
        {
            SyncDesired(p);

            var col = VE();

            col.Add(Section("Renderer features", $"{_renderers.Count} URP renderers found"));
            col.Add(BuildMatrix(p));
            col.Add(BuildLegend());
            col.Add(BuildInstallRow(p));

            // Checks are grouped by their own heading, so work that is not "a project
            // setting" - creating helper renderer assets, for one - gets announced
            // under its own name instead of being folded in where nobody looks.
            string meta = $"{ZLZ_HubUrp.ScanPipelineAssets().Count} URP quality assets found";

            foreach (var group in p.GetChecks().GroupBy(c => string.IsNullOrEmpty(c.Group)
                                                                ? "Project settings" : c.Group))
            {
                var gap = new VisualElement();
                gap.style.height = 22f;
                col.Add(gap);

                bool rendererList = group.Key == ZLZ_HubProduct.RendererListGroup;

                col.Add(Section(group.Key,
                                rendererList ? "what each URP asset renders through" : meta));

                // The list itself first, then the rows that change it. Reading "add a
                // reflection renderer" means nothing without seeing the list it goes
                // into and which slot numbers are already taken.
                if (rendererList) col.Add(BuildRendererList());

                col.Add(BuildChecks(group.ToList()));
            }

            return col;
        }

        // Section heading : bigger and brighter than anything it introduces, with a
        // rule under it. Every label on this tab used to be 10px dim grey, so the
        // headings carried no more weight than the rows and the page read as one
        // undifferentiated block. The count rides alongside in the quiet style, so
        // the title itself stays the thing the eye lands on.
        static VisualElement Section(string title, string meta = null)
        {
            var box = VE();
            box.style.marginBottom = 7f;

            var row = VE(FlexDirection.Row);
            row.style.alignItems    = Align.FlexEnd;
            row.style.paddingBottom = 5f;

            row.Add(Lbl(title, 13, TextMain, FontStyle.Bold));

            if (!string.IsNullOrEmpty(meta))
            {
                var m = Lbl(meta, 10, TextDim);
                m.style.marginLeft   = 8f;
                m.style.marginBottom = 1f;
                row.Add(m);
            }

            Border(row, bottom: 1f);
            box.Add(row);
            return box;
        }

        VisualElement BuildMatrix(ZLZ_HubProduct p)
        {
            var table = CardBox(false);

            if (_renderers.Count == 0)
            {
                var none = Lbl("No URP renderer assets found in this project.", 11, Warn);
                none.style.paddingLeft = 8f; none.style.paddingTop = 8f; none.style.paddingBottom = 8f;
                table.Add(none);
                return table;
            }

            // The table keeps its natural size and the viewport scrolls sideways when
            // a project has more renderers than fit. Shrinking the columns instead
            // would clip the very labels the customer needs to tell them apart.
            table.style.minWidth  = k_NameWidth + k_ColWidth * _renderers.Count;
            table.style.flexGrow  = 1f;
            table.style.flexShrink = 0f;

            // Header
            var head = VE(FlexDirection.Row);
            head.style.backgroundColor = Panel;
            head.style.paddingLeft     = 8f;
            head.style.paddingRight    = 8f;
            head.style.paddingTop      = 4f;
            head.style.paddingBottom   = 4f;

            var hName = Lbl("Feature", 10, TextDim);
            hName.style.width      = k_NameWidth;
            hName.style.flexGrow   = 1f;
            hName.style.flexShrink = 0f;
            head.Add(hName);

            foreach (var r in _renderers)
            {
                var c = Lbl(r.ShortName, 10, r.IsActive ? Accent : TextDim);
                c.style.width          = k_ColWidth;
                c.style.flexShrink     = 0f;
                c.style.unityTextAlign = TextAnchor.MiddleCenter;
                c.style.whiteSpace     = WhiteSpace.NoWrap;

                // Faded so the eye skips it : nothing renders the game through this
                // renderer, so features installed here only cost fill rate.
                if (!r.IsRecommended) c.style.opacity = 0.45f;

                c.tooltip = r.Path
                          + (r.IsActive      ? "\n(currently active)" : "")
                          + (r.IsRecommended ? "" : "\nSecondary renderer - not what the game renders through, so nothing is recommended here.");
                head.Add(c);
            }
            table.Add(head);

            // Only the selected product's features. A locked row for a package that
            // is not installed used to sit here as a cross-sell, but it promised
            // something this table can never deliver : even after buying, those
            // features appear under their OWN card, never in this one. The whole
            // page is scoped to one product - banner, what's new, learn, review -
            // and Setup has to obey the same rule or the page has two minds.
            // The line-up lives on the rail cards, where it is honest.
            foreach (var f in p.GetFeatures()) table.Add(BuildMatrixRow(f));

            var scroll = new ScrollView(ScrollViewMode.Horizontal);
            scroll.style.flexGrow = 1f;
            scroll.Add(table);
            return scroll;
        }

        VisualElement BuildMatrixRow(ZLZ_HubFeature f)
        {
            var row = VE(FlexDirection.Row);
            row.style.paddingLeft   = 8f;
            row.style.paddingRight  = 8f;
            row.style.paddingTop    = 3f;
            row.style.paddingBottom = 3f;
            row.style.alignItems    = Align.Center;
            Border(row, top: 1f);

            var name = Lbl(f.DisplayName, 11, TextMain);
            name.style.width      = k_NameWidth;
            name.style.flexGrow   = 1f;
            name.style.flexShrink = 0f;
            name.style.whiteSpace = WhiteSpace.NoWrap;
            name.tooltip          = f.Tooltip ?? string.Empty;
            row.Add(name);

            foreach (var r in _renderers)
            {
                var entry   = r;
                var feature = f;

                bool present = ZLZ_HubUrp.IsPresent(entry.Data, feature.FeatureType);
                bool want    = Desired(feature, entry);

                var cell = Checkbox(present, want);
                cell.tooltip = present && want  ? "Installed"
                             : !present && want ? "Will be installed when you press Apply"
                             : present          ? "Will be REMOVED when you press Apply - its settings are lost"
                                                : "Not installed";

                cell.RegisterCallback<MouseDownEvent>(_ =>
                {
                    _desired[DesiredKey(feature, entry)] = !want;
                    Rebuild();
                });

                row.Add(cell);
            }

            return row;
        }

        // Four states share one square, so the square has to say which is which.
        VisualElement BuildLegend()
        {
            var row = VE(FlexDirection.Row);
            row.style.marginTop  = 8f;
            row.style.alignItems = Align.Center;
            row.style.flexWrap   = Wrap.Wrap;

            row.Add(LegendItem(true,  true,  "Installed"));
            row.Add(LegendItem(false, true,  "Will be installed"));
            row.Add(LegendItem(true,  false, "Will be removed"));
            row.Add(LegendItem(false, false, "Not installed"));
            return row;
        }

        static VisualElement LegendItem(bool present, bool want, string text)
        {
            var item = VE(FlexDirection.Row);
            item.style.alignItems  = Align.Center;
            item.style.marginRight = 16f;
            item.style.marginTop   = 2f;

            var swatch = Checkbox(present, want);
            swatch.style.width = 20f;
            item.Add(swatch);

            var l = Lbl(text, 10, TextDim);
            l.style.marginLeft = 3f;
            item.Add(l);
            return item;
        }

        VisualElement BuildInstallRow(ZLZ_HubProduct p)
        {
            var row = VE(FlexDirection.Row);
            row.style.marginTop  = 10f;
            row.style.alignItems = Align.Center;

            var add    = new List<(ZLZ_RendererEntry r, ZLZ_HubFeature f)>();
            var remove = new List<(ZLZ_RendererEntry r, ZLZ_HubFeature f)>();
            CollectPending(p, add, remove);

            int pending = add.Count + remove.Count;

            // Greyed out with nothing pending, so the button always answers "is there
            // anything left to do?" without the customer having to read the table.
            var install = Btn(pending > 0 ? $"Apply  ({pending})" : "Apply",
                              () => Apply(p), accent: pending > 0);
            install.SetEnabled(pending > 0);
            install.style.height       = 24f;
            install.style.paddingLeft  = 18f;
            install.style.paddingRight = 18f;
            row.Add(install);

            string summary =
                pending == 0        ? "Nothing staged - the table matches the project"
              : remove.Count == 0   ? $"{add.Count} to install"
              : add.Count == 0      ? $"{remove.Count} to remove"
                                    : $"{add.Count} to install, {remove.Count} to remove";

            var note = Lbl(summary + (pending > 0 ? "  ·  nothing is written until you press Apply" : ""),
                           10, remove.Count > 0 ? Bad : TextDim);
            note.style.marginLeft = 10f;
            row.Add(note);

            return row;
        }

        // Prints every URP asset's Renderer List with its slot numbers, because a
        // feature's "Renderer Index" is meaningless until you can see which renderer
        // sits at which index - and because these rows are what the Fix buttons
        // underneath are about to add to.
        VisualElement BuildRendererList()
        {
            var box = CardBox(false);
            box.style.marginTop     = 4f;
            box.style.paddingLeft   = 10f;
            box.style.paddingRight  = 10f;
            box.style.paddingTop    = 8f;
            box.style.paddingBottom = 8f;

            var assets = ZLZ_HubUrp.ScanPipelineAssets();
            if (assets.Count == 0)
            {
                box.Add(Lbl("No URP asset found in this project.", 11, Warn));
                return box;
            }

            for (int a = 0; a < assets.Count; a++)
            {
                var asset = assets[a];

                if (a > 0)
                {
                    var gap = new VisualElement();
                    gap.style.height = 9f;
                    box.Add(gap);
                }

                string assetName = System.IO.Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(asset));
                box.Add(Lbl(assetName, 11, TextMain, FontStyle.Bold));

                foreach (var row in ZLZ_HubUrp.RendererListOf(asset))
                {
                    var line = VE(FlexDirection.Row);
                    line.style.alignItems = Align.Center;
                    line.style.marginTop  = 3f;
                    line.style.marginLeft = 10f;

                    var index = Lbl(row.Index.ToString(), 10, TextDim);
                    index.style.width          = 18f;
                    index.style.unityTextAlign = TextAnchor.MiddleRight;
                    line.Add(index);

                    var name = Lbl(row.Name, 11, row.IsDefault ? TextMain : TextDim);
                    name.style.marginLeft = 8f;
                    line.Add(name);

                    if (row.IsDefault)
                    {
                        var tag = Chip("default · renders the game", ZLZ_CheckStatus.Ok, null);
                        tag.style.marginLeft = 8f;
                        line.Add(tag);
                    }

                    box.Add(line);
                }
            }

            return box;
        }

        VisualElement BuildChecks(List<ZLZ_HubCheck> checks)
        {
            var box = CardBox(false);
            box.style.marginTop     = 4f;
            box.style.paddingLeft   = 9f;
            box.style.paddingRight  = 9f;
            box.style.paddingTop    = 7f;
            box.style.paddingBottom = 8f;

            if (checks.Count == 0)
            {
                box.Add(Lbl("Nothing to check for this package.", 11, TextDim));
                return box;
            }

            foreach (var check in checks)
            {
                var c       = check;
                var status  = Safe(c.Status);
                var targets = Safe(c.Targets) ?? new List<ZLZ_HubCheckTarget>();

                var row = VE(FlexDirection.Row);
                row.style.alignItems   = Align.Center;
                row.style.marginBottom = 7f;

                var glyph = Lbl(status == ZLZ_CheckStatus.Ok ? "✔" : status == ZLZ_CheckStatus.Warning ? "⚠" : "✖",
                                11, StatusTint(status));
                glyph.style.width = 16f;
                row.Add(glyph);

                var title = Lbl(c.Title, 11, TextMain);
                title.style.width      = k_NameWidth;
                title.style.flexShrink = 0f;
                title.tooltip          = c.Description ?? string.Empty;
                row.Add(title);

                // One chip per asset the check looked at, so "HDR is off" always says
                // off WHERE. A check with no targets is project-wide and gets one chip.
                var chips = VE(FlexDirection.Row);
                chips.style.flexGrow = 1f;
                chips.style.flexWrap = Wrap.Wrap;

                if (targets.Count == 0)
                    chips.Add(Chip("Project", status, null));
                else
                    foreach (var t in targets) chips.Add(Chip(t.Name, t.Status, t.Path));

                row.Add(chips);

                if (status == ZLZ_CheckStatus.Ok || c.Fix == null)
                {
                    row.Add(Lbl(status == ZLZ_CheckStatus.Ok ? "ok" : "manual", 10, TextDim));
                }
                else
                {
                    int bad = targets.Count(t => t.Status != ZLZ_CheckStatus.Ok);
                    row.Add(Btn(bad > 1 ? $"Fix all ({bad})" : "Fix", () => RunFix(c)));
                }

                box.Add(row);
            }

            var fixable = checks.Where(c => c.Fix != null && Safe(c.Status) != ZLZ_CheckStatus.Ok).ToList();
            if (fixable.Count > 0)
            {
                var all = Btn($"Fix all ({fixable.Count})", () =>
                {
                    foreach (var c in fixable) RunFix(c, silent: true);
                    AssetDatabase.SaveAssets();
                    Rebuild();
                });
                all.style.marginTop = 2f;
                box.Add(all);
            }

            return box;
        }

        // ── Support ───────────────────────────────────────────────────────────
        //
        // Replaces the old "what's new" and "learn" tabs. Release notes live on the
        // store page and documentation lives on the website ; mirroring either into
        // the package meant maintaining a second copy that goes stale. What the Hub
        // can do that neither of those can is hand over the exact state of THIS
        // project, which is the thing every support thread starts by asking for.
        VisualElement BuildSupport(ZLZ_HubProduct p)
        {
            var box = VE();

            box.Add(Section("Something not working?"));

            var intro = Lbl("Send us your project's setup and we can usually answer without a round of questions first.",
                            11, TextMain);
            intro.style.whiteSpace   = WhiteSpace.Normal;
            intro.style.marginBottom = 10f;
            box.Add(intro);

            string info = BuildSystemInfo();

            var preview = CardBox(false);
            preview.style.paddingLeft   = 10f;
            preview.style.paddingRight  = 10f;
            preview.style.paddingTop    = 8f;
            preview.style.paddingBottom = 8f;

            var text = Lbl(info, 10, TextDim);
            text.style.whiteSpace = WhiteSpace.Normal;
            preview.Add(text);
            box.Add(preview);

            var buttons = VE(FlexDirection.Row);
            buttons.style.marginTop = 10f;

            // Copy first, not "open mail app". mailto hands off to whatever Windows
            // has registered, which on most machines is an Outlook nobody set up,
            // and does nothing at all for someone who lives in webmail. Copying
            // works on every machine, and the customer sends it however they
            // already send mail.
            var copy = Btn("Copy report", CopySystemInfo, accent: true);
            copy.style.height       = 26f;
            copy.style.paddingLeft  = 16f;
            copy.style.paddingRight = 16f;
            buttons.Add(copy);

            var mail = Btn("Open mail app", () => SendSupportEmail(p, info));
            mail.style.height     = 26f;
            mail.style.marginLeft = 6f;
            mail.tooltip          = "Uses whichever email program this computer is set up with.";
            buttons.Add(mail);

            box.Add(buttons);

            if (!string.IsNullOrEmpty(p.SupportEmail))
            {
                var label = Lbl("Send it to", 10, TextDim);
                label.style.marginTop = 12f;
                box.Add(label);

                // A read-only field rather than a label : the address has to be
                // selectable, because copying it by hand is the fallback that always
                // works no matter what mail setup the customer has.
                var address = new TextField { value = p.SupportEmail, isReadOnly = true };
                address.style.marginTop  = 2f;
                address.style.marginLeft = 0f;
                address.style.maxWidth   = 320f;
                box.Add(address);
            }

            return box;
        }

        void SendSupportEmail(ZLZ_HubProduct p, string info)
        {
            if (string.IsNullOrEmpty(p.SupportEmail))
            {
                CopySystemInfo();
                return;
            }

            string subject = Uri.EscapeDataString($"{p.DisplayName} - support request");
            string body    = Uri.EscapeDataString(
                "Describe what happened here.\n\n\n" +
                "------------------------------------\n" + info);

            Application.OpenURL($"mailto:{p.SupportEmail}?subject={subject}&body={body}");

            // The clipboard copy is the safety net : if no mail client opens, the
            // report is still one paste away rather than lost.
            EditorGUIUtility.systemCopyBuffer = info;
        }

        // ── Actions ───────────────────────────────────────────────────────────

        // Applies exactly what the table says and nothing else.
        void Apply(ZLZ_HubProduct p)
        {
            var add    = new List<(ZLZ_RendererEntry r, ZLZ_HubFeature f)>();
            var remove = new List<(ZLZ_RendererEntry r, ZLZ_HubFeature f)>();
            CollectPending(p, add, remove);

            if (add.Count == 0 && remove.Count == 0) return;

            var sb = new StringBuilder();
            foreach (var group in add.Concat(remove).GroupBy(w => w.r))
            {
                sb.AppendLine("  " + group.Key.Path);
                foreach (var w in group)
                    sb.AppendLine((add.Contains(w) ? "      + " : "      - ") + w.f.DisplayName);
            }

            // Adding is not a decision that needs asking twice - it is the thing the
            // button exists for, and unticking undoes it. REMOVING is different : the
            // feature's tuned values go with it and re-adding brings back defaults, so
            // that one still stops to say so. Everything is written to the Console
            // either way, so a customer on locked source control can see what changed.
            if (remove.Count > 0)
            {
                string warning =
                    $"{remove.Count} feature(s) will be removed:\n\n{sb}\n" +
                    "Whatever was tuned on them is discarded and cannot be undone.";

                if (!EditorUtility.DisplayDialog("ZLZ Hub", warning, "Remove", "Cancel")) return;
            }

            foreach (var w in remove) ZLZ_HubUrp.Remove(w.r.Data, w.f);
            foreach (var w in add)    ZLZ_HubUrp.Install(w.r.Data, w.f);
            ZLZ_HubUrp.Save();

            Debug.Log($"[ZLZ Hub] {p.DisplayName} - renderer features updated:\n{sb}");

            // Re-seed from what is now real, so the table shows the result rather than
            // the request that produced it.
            _desired.Clear();
            Rebuild();
        }

        // Fixes are project settings the package needs anyway - linear colour space,
        // HDR, depth priming - and each one is a single toggle the customer can flip
        // back in the inspector. Asking permission for those dressed routine setup up
        // as something to be nervous about. What changed goes to the Console instead.
        void RunFix(ZLZ_HubCheck check, bool silent = false)
        {
            if (check?.Fix == null) return;

            string summary = Safe(check.FixSummary);

            try { check.Fix(); }
            catch (Exception e) { Debug.LogError($"[ZLZ Hub] Fix failed for \"{check.Title}\". {e}"); return; }

            Debug.Log($"[ZLZ Hub] Fixed \"{check.Title}\".{(string.IsNullOrEmpty(summary) ? "" : "\n" + summary)}");

            if (!silent) { AssetDatabase.SaveAssets(); Rebuild(); }
        }

        void CopySystemInfo()
        {
            EditorGUIUtility.systemCopyBuffer = BuildSystemInfo();
            Debug.Log("[ZLZ Hub] System info copied to the clipboard - paste it into your support message.");
        }

        // Half of every support thread is spent asking which Unity, which URP,
        // which renderer. This hands the customer all of it in one click.
        string BuildSystemInfo()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Unity        : {Application.unityVersion}");
            sb.AppendLine($"Platform     : {EditorUserBuildSettings.activeBuildTarget}");
            sb.AppendLine($"Color space  : {PlayerSettings.colorSpace}");
            sb.AppendLine($"Graphics API : {SystemInfo.graphicsDeviceType}");
            sb.AppendLine();

            foreach (var p in ZLZ_HubRegistry.Installed)
                sb.AppendLine($"{p.DisplayName} : {(IsConfigured(p) ? "set up" : "not set up")}");
            sb.AppendLine();

            foreach (var r in _renderers)
            {
                sb.AppendLine($"Renderer {(r.IsActive ? "*" : " ")} {r.Path}");
                var product = Selected();
                if (product == null) continue;
                foreach (var f in product.GetFeatures())
                {
                    bool on      = ZLZ_HubUrp.IsEnabled(r.Data, f.FeatureType);
                    bool present = ZLZ_HubUrp.Find(r.Data, f.FeatureType) != null;
                    sb.AppendLine($"    {(on ? "on " : present ? "off" : "-- ")}  {f.DisplayName}");
                }
            }

            return sb.ToString();
        }

        static void OpenLink(string url)
        {
            if (string.IsNullOrEmpty(url)) return;
            Application.OpenURL(url);
        }

        // ── Small helpers ─────────────────────────────────────────────────────

        string SelectedId()
        {
            var installed = ZLZ_HubRegistry.Installed;
            if (!string.IsNullOrEmpty(_selectedId))
            {
                if (installed.Any(p => p.Id == _selectedId)) return _selectedId;
                // A locked catalog entry is selectable too : its card opens a product page in
                // the right pane (BuildLockedRight) instead of throwing the customer into a
                // browser the moment they click.
                if (ZLZ_HubRegistry.Locked.Any(x => x.Id == _selectedId)) return _selectedId;
            }
            return installed.Count == 0 ? null : installed[0].Id;
        }

        ZLZ_HubProduct Selected()
        {
            string id = SelectedId();
            return id == null ? null : ZLZ_HubRegistry.Installed.FirstOrDefault(p => p.Id == id);
        }

        // Asked of the project, not remembered. A stored "configured" flag can drift
        // from the truth the moment someone edits a renderer by hand, and it needed a
        // version string to compare against - a string nothing could keep honest.
        bool IsConfigured(ZLZ_HubProduct p)
        {
            bool anyTarget = false;

            foreach (var f in p.GetFeatures())
            {
                if (!f.Recommended) continue;

                foreach (var r in _renderers)
                {
                    if (!r.IsRecommended) continue;

                    anyTarget = true;
                    if (!ZLZ_HubUrp.IsPresent(r.Data, f.FeatureType)) return false;
                }
            }

            return anyTarget;
        }

        string StatusWord(ZLZ_HubProduct p)  => IsConfigured(p) ? "installed" : "needs setup";
        Color  StatusColor(ZLZ_HubProduct p) => IsConfigured(p) ? TextDim : Accent;

        int CountDone(ZLZ_HubProduct p)
        {
            int n = 0;
            foreach (var f in p.GetFeatures())
                if (_renderers.Any(r => ZLZ_HubUrp.IsPresent(r.Data, f.FeatureType))) n++;
            foreach (var c in p.GetChecks())
                if (Safe(c.Evaluate) == ZLZ_CheckStatus.Ok) n++;
            return n;
        }

        int CountTotal(ZLZ_HubProduct p) => p.GetFeatures().Count() + p.GetChecks().Count();

        static T Safe<T>(Func<T> f)
        {
            try { return f == null ? default : f(); }
            catch (Exception e) { Debug.LogWarning($"[ZLZ Hub] {e.Message}"); return default; }
        }

        static Texture2D FindTexture(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            foreach (var guid in AssetDatabase.FindAssets($"{name} t:Texture2D"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (System.IO.Path.GetFileNameWithoutExtension(path) == name)
                    return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }
            return null;
        }

        // Every product card gets the same banner block, whatever art it ships and
        // whether it ships any at all - a rail where each card is a different
        // height reads as broken, not as responsive. The art is fitted inside with
        // "contain", so it is never cropped and never stretched; anything that is
        // not 3:2 just letterboxes against the card colour.
        static VisualElement CardBanner(string textureName, Color fallback)
        {
            var v = VE();
            v.style.height          = k_CardBannerHeight;
            v.style.flexShrink      = 0f;
            v.style.backgroundColor = fallback;

            var tex = FindTexture(textureName);
            if (tex != null)
            {
                v.style.backgroundImage = new StyleBackground(tex);
                v.style.backgroundSize  = new StyleBackgroundSize(new BackgroundSize(BackgroundSizeType.Contain));
            }

            // The rail widens to fill whatever the work area does not use, so the
            // card art grows with it instead of sitting as a small tile against a
            // field of empty panel.
            v.RegisterCallback<GeometryChangedEvent>(evt =>
            {
                float width = evt.newRect.width;
                if (width <= 1f) return;

                // Exactly the art's 3:2, so "contain" fills the frame edge to edge and
                // never letterboxes.
                float target = Mathf.Max(70f, width / 1.5f);

                // Setting the height re-triggers this event, so only act on a real
                // difference or the layout never settles.
                if (Mathf.Abs(evt.newRect.height - target) < 0.5f) return;
                v.style.height = target;
            });

            return v;
        }

        // background-size cover, not the deprecated unityBackgroundScaleMode : key
        // art is 16:9 and the hero is far wider than that, so it has to crop. focus
        // decides which slice survives - 0 keeps the top, 1 keeps the bottom.
        static void ApplyBanner(VisualElement v, Texture2D tex, float focus)
        {
            v.style.backgroundImage    = new StyleBackground(tex);
            v.style.backgroundSize     = new StyleBackgroundSize(new BackgroundSize(BackgroundSizeType.Cover));
            v.style.backgroundPositionY = new StyleBackgroundPosition(
                new BackgroundPosition(BackgroundPositionKeyword.Top, Length.Percent(Mathf.Clamp01(focus) * 100f)));
        }

        // Drawn, not typed. The editor font has no padlock or ballot-box glyph and
        // renders them as an empty square, which reads as a broken package.
        // Rectangles always render.
        // One question per square : is this feature in this renderer, and is that about
        // to change. Whether an installed feature is currently switched on is a
        // different matter, decided in the renderer itself - mixing it in here gave
        // the square two meanings and left half its states unclickable.
        static VisualElement Checkbox(bool present, bool want)
        {
            var wrap = VE();
            wrap.style.width          = k_ColWidth;
            wrap.style.flexShrink     = 0f;
            wrap.style.alignItems     = Align.Center;
            wrap.style.justifyContent = Justify.Center;

            Color idle = Pro ? new Color32(110, 110, 110, 255) : new Color32(130, 130, 130, 255);

            Color edge = present && want  ? Ok        // installed
                       : !present && want ? Accent    // staged install
                       : present          ? Bad       // staged removal
                                          : idle;     // not installed

            string glyph = present && !want ? "✖"
                         : present || want  ? "✔"
                                            : null;

            bool filled = present || want;

            var box = VE();
            box.style.width           = 14f;
            box.style.height          = 14f;
            box.style.alignItems      = Align.Center;
            box.style.justifyContent  = Justify.Center;
            box.style.backgroundColor = filled ? new Color(edge.r, edge.g, edge.b, 0.18f) : Color.clear;

            box.style.borderTopWidth    = 1f;
            box.style.borderBottomWidth = 1f;
            box.style.borderLeftWidth   = 1f;
            box.style.borderRightWidth  = 1f;
            box.style.borderTopColor    = edge;
            box.style.borderBottomColor = edge;
            box.style.borderLeftColor   = edge;
            box.style.borderRightColor  = edge;

            box.style.borderTopLeftRadius     = 3f;
            box.style.borderTopRightRadius    = 3f;
            box.style.borderBottomLeftRadius  = 3f;
            box.style.borderBottomRightRadius = 3f;

            if (glyph != null)
            {
                // Glyphs the editor font is known to have - the shipping dashboard
                // already draws both in this same editor.
                var mark = Lbl(glyph, 9, edge);
                mark.style.marginBottom = 1f;
                box.Add(mark);
            }

            wrap.Add(box);
            return wrap;
        }

        static VisualElement VE(FlexDirection dir = FlexDirection.Column)
        {
            var v = new VisualElement();
            v.style.flexDirection = dir;
            return v;
        }

        static VisualElement Spacer()
        {
            var v = new VisualElement();
            v.style.flexGrow = 1f;
            return v;
        }

        static Label Lbl(string text, int size, Color color, FontStyle style = FontStyle.Normal)
        {
            var l = new Label(text);
            l.style.fontSize        = size;
            l.style.color           = color;
            l.style.unityFontStyleAndWeight = style;
            return l;
        }

        static Button Btn(string text, Action onClick, bool accent = false)
        {
            var b = new Button(onClick) { text = text };
            b.style.fontSize     = 11;
            b.style.height       = 20f;
            b.style.paddingLeft  = 9f;
            b.style.paddingRight = 9f;
            b.style.marginLeft   = 0f;
            b.style.marginRight  = 0f;
            if (accent)
            {
                b.style.color                = Accent;
                b.style.borderTopColor       = Accent;
                b.style.borderBottomColor    = Accent;
                b.style.borderLeftColor      = Accent;
                b.style.borderRightColor     = Accent;
            }
            return b;
        }

        static VisualElement CardBox(bool selected)
        {
            var v = VE();
            v.style.backgroundColor = Card;
            v.style.overflow        = Overflow.Hidden;

            float w = selected ? 2f : 1f;
            v.style.borderTopWidth    = w;
            v.style.borderBottomWidth = w;
            v.style.borderLeftWidth   = w;
            v.style.borderRightWidth  = w;

            Color c = selected ? Accent : Line;
            v.style.borderTopColor    = c;
            v.style.borderBottomColor = c;
            v.style.borderLeftColor   = c;
            v.style.borderRightColor  = c;

            v.style.borderTopLeftRadius     = 7f;
            v.style.borderTopRightRadius    = 7f;
            v.style.borderBottomLeftRadius  = 7f;
            v.style.borderBottomRightRadius = 7f;
            return v;
        }

        static void Border(VisualElement v, float top = 0f, float bottom = 0f, float right = 0f)
        {
            if (top > 0f)    { v.style.borderTopWidth    = top;    v.style.borderTopColor    = Line; }
            if (bottom > 0f) { v.style.borderBottomWidth = bottom; v.style.borderBottomColor = Line; }
            if (right > 0f)  { v.style.borderRightWidth  = right;  v.style.borderRightColor  = Line; }
        }
    }
}
