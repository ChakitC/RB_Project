using System;
using System.Collections.Generic;
using UnityEngine.Rendering.Universal;

namespace ZLZ.Hub
{
    // ═════════════════════════════════════════════════════════════════════════
    //  FROZEN CONTRACT - ADD ONLY.
    //
    //  Every ZLZ package ships its own copy of this folder, and whichever package
    //  the customer imports LAST is the copy that wins. Someone running Anime 2.0
    //  next to Environment 1.0 therefore compiles the older package's provider
    //  against the newer contract - in their project, where a compile error stops
    //  everything including the Hub that would explain it.
    //
    //  So, once both packages are on sale:
    //      ADDING a member with a default        safe
    //      making an abstract member virtual     safe
    //      removing / renaming a member          BREAKS the other package
    //      changing a signature or return type   BREAKS the other package
    //
    //  The rule covers this file only. ZLZ_HubWindow, ZLZ_HubUrp, ZLZ_HubMigration
    //  and the rest are free to change however they like - nothing outside Common
    //  derives from them.
    // ═════════════════════════════════════════════════════════════════════════

    // ─────────────────────────────────────────────────────────────────────────
    //  Shared contract between the Hub window and every ZLZ package.
    //
    //  A package never talks to the window directly. It subclasses ZLZ_HubProduct
    //  and the Hub finds it through TypeCache, which is what makes "ZLZ Env Shader
    //  installs later and its panel simply appears" work without shipping an
    //  updated Anime Shader on the same day.
    //
    //  This file is duplicated byte-for-byte into every ZLZ package under
    //  Assets/ZLZ/Common. Exporting it with the SAME GUID from this project keeps
    //  Unity treating it as one file when a customer owns two ZLZ assets, instead
    //  of importing a second copy and breaking the build on duplicate types.
    // ─────────────────────────────────────────────────────────────────────────

    public enum ZLZ_CheckStatus
    {
        Ok,         // nothing to do
        Warning,    // works, but the customer is losing quality or perf
        Error       // the shader is visibly broken until this is fixed
    }

    // One row of the renderer matrix : a ScriptableRendererFeature the package
    // wants installed into the customer's URP renderers.
    public class ZLZ_HubFeature
    {
        public string DisplayName;
        public Type   FeatureType;              // must derive from ScriptableRendererFeature
        public string AssetName;                // sub-asset name, must match what the package uses elsewhere
        public bool   Recommended = true;       // ticked by default on a renderer that renders the game
        public string Tooltip;

        // Whether the feature starts switched ON once installed. Several ZLZ features
        // are meant to sit in the renderer switched OFF until the customer actually
        // uses them - a screen-space outline or a selection outline costs full passes
        // every frame, so installing one must not silently turn it on.
        public bool DefaultActive = true;

        // Runs once, immediately after the feature is created. For defaults that make
        // the feature useful when it IS switched on (e.g. a layer mask that would
        // otherwise be Nothing, drawing an invisible pass).
        public Action<ScriptableRendererFeature> Configure;
    }

    // One asset a check looked at, so the row can say WHICH file is wrong instead of
    // just that something is.
    public class ZLZ_HubCheckTarget
    {
        public string          Name;     // short label, e.g. "HighFidelity"
        public string          Path;     // full asset path, shown on hover
        public ZLZ_CheckStatus Status;
    }

    // One row of the checklist below the feature matrix.
    public class ZLZ_HubCheck
    {
        public string                Title;
        public string                Description;

        // Heading this row is filed under. Rows sharing a group are drawn together
        // beneath it, so a package can separate "settings your project needs" from
        // work of a different kind - creating helper renderer assets, say - instead
        // of doing the second silently and hoping nobody minds.
        public string                Group;
        public Action                Fix;               // null = the customer has to do it by hand
        public Func<string>          FixSummary;        // what Fix() is about to touch, shown before it runs

        // Per-asset breakdown. Leave null for a check that has a single project-wide
        // answer (colour space, say) - the row then shows one "Project" chip.
        public Func<List<ZLZ_HubCheckTarget>> Targets;

        // Only needed when Targets is null. With targets the overall status is the
        // worst of them, so the two can never disagree.
        public Func<ZLZ_CheckStatus> Evaluate;

        public ZLZ_CheckStatus Status()
        {
            var targets = Targets?.Invoke();
            if (targets == null || targets.Count == 0)
                return Evaluate != null ? Evaluate() : ZLZ_CheckStatus.Ok;

            var worst = ZLZ_CheckStatus.Ok;
            foreach (var t in targets) if (t.Status > worst) worst = t.Status;
            return worst;
        }
    }

    public abstract class ZLZ_HubProduct
    {
        // Checks filed under this group are drawn beneath a printout of what each URP
        // asset's Renderer List actually contains, index by index. Anything that adds
        // a renderer to that list belongs here, where the customer can see the list
        // it is about to change.
        public const string RendererListGroup = "Renderer List";

        // Stable across versions. Used as the key in ZLZ_Hub.json and to match
        // against the catalog, so renaming the product must not change it.
        public abstract string Id          { get; }
        public abstract string DisplayName { get; }

        public virtual string Tagline           => string.Empty;
        public virtual int    SortOrder         => 100;
        public virtual string BannerTextureName => null;   // Texture2D asset name, looked up by AssetDatabase

        // Where to crop 16:9 key art when it lands in the much wider hero strip.
        // 0 = keep the top of the image, 1 = keep the bottom. Characters usually
        // want a low number so heads survive the crop.
        public virtual float BannerFocus => 0.3f;
        public virtual string StoreUrl     => null;
        public virtual string WebsiteUrl   => null;

        // Where a support request should land. Release notes are deliberately not
        // mirrored into the package : the Asset Store submission form already
        // requires them and publishes them on the product page, so keeping a second
        // copy here would be work with no reader.
        public virtual string SupportEmail => null;

        public abstract IEnumerable<ZLZ_HubFeature> GetFeatures();

        public virtual IEnumerable<ZLZ_HubCheck> GetChecks() => Array.Empty<ZLZ_HubCheck>();
    }

    // A ZLZ product the customer does NOT have in this project. The Hub can only
    // tell "files are here" from "files are not here" - Unity exposes no ownership
    // information - so a customer who owns Env but has not imported it yet also
    // lands here. The store button is therefore worded as a destination, not as
    // a purchase.
    //
    // Outside the ADD-ONLY rule above, despite living in this file : no package
    // constructs or derives from this type, only ZLZ_HubRegistry (which ships in
    // the same folder) does, so the two always move together.
    public class ZLZ_HubCatalogEntry
    {
        public string Id;
        public string DisplayName;
        public string Tagline;
        public string BannerTextureName;
        public string StoreUrl;
        public string WebsiteUrl;
    }
}
