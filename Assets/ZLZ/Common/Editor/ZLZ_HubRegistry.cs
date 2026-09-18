using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ZLZ.Hub
{
    // ─────────────────────────────────────────────────────────────────────────
    //  Finds every installed ZLZ package, and works out which catalog products
    //  are missing from this project so the window can show them locked.
    // ─────────────────────────────────────────────────────────────────────────
    internal static class ZLZ_HubRegistry
    {
        // The full ZLZ line-up. Entries whose Id matches an installed product are
        // dropped from the locked list, so a single edit here is all a new asset
        // needs to appear in every already-shipped package.
        static readonly ZLZ_HubCatalogEntry[] k_Catalog =
        {
            new ZLZ_HubCatalogEntry
            {
                Id                = "zlz.anime",
                DisplayName       = "ZLZ Anime Shader",
                Tagline           = "Stylised character shading, outlines, face shadow",
                BannerTextureName = "ZLZ_Banner_Anime",
                StoreUrl          = ZLZ_HubUrls.AnimeStore,
                WebsiteUrl        = ZLZ_HubUrls.Website,
            },
            new ZLZ_HubCatalogEntry
            {
                Id                = "zlz.env",
                DisplayName       = "ZLZ Environment Shader",
                Tagline           = "Water, grass, fog, planar reflection",
                BannerTextureName = "ZLZ_Banner_Env",
                StoreUrl          = ZLZ_HubUrls.EnvStore,
                WebsiteUrl        = ZLZ_HubUrls.EnvWebsite,
            },
        };

        static List<ZLZ_HubProduct> _installed;

        public static List<ZLZ_HubProduct> Installed
        {
            get
            {
                if (_installed != null) return _installed;

                _installed = new List<ZLZ_HubProduct>();
                foreach (var type in TypeCache.GetTypesDerivedFrom<ZLZ_HubProduct>())
                {
                    if (type.IsAbstract) continue;
                    try
                    {
                        if (Activator.CreateInstance(type) is ZLZ_HubProduct p) _installed.Add(p);
                    }
                    catch (Exception e)
                    {
                        // One broken provider must not take the whole Hub down with
                        // it - the other packages still have to be serviceable.
                        Debug.LogWarning($"[ZLZ Hub] Could not create {type.Name}. {e.Message}");
                    }
                }

                _installed = _installed.OrderBy(p => p.SortOrder).ThenBy(p => p.DisplayName).ToList();
                return _installed;
            }
        }

        public static List<ZLZ_HubCatalogEntry> Locked
        {
            get
            {
                var installedIds = new HashSet<string>(Installed.Select(p => p.Id));
                return k_Catalog.Where(e => !installedIds.Contains(e.Id)).ToList();
            }
        }

        public static void Invalidate() => _installed = null;
    }

    // Single place to keep the store links.
    internal static class ZLZ_HubUrls
    {
        // The store page in a browser, not the com.unity3d.kharma scheme : that one
        // opens Unity's own Package Manager, which is not where a customer expects a
        // button labelled Asset Store to take them.
        public const string AnimeStore = "https://assetstore.unity.com/packages/vfx/shaders/zlz-anime-shader-354900";

        public const string EnvStore     = "https://assetstore.unity.com/packages/slug/397684";
        public const string EnvWebsite   = "https://zlz-studio.github.io/#env";

        public const string Website = "https://zlz-studio.github.io/";

        public const string SupportEmail = "zlzstudio.production@gmail.com";
    }
}
