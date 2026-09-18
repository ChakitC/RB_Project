using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ZLZ.Hub
{
    // ─────────────────────────────────────────────────────────────────────────
    //  Decides whether the Hub opens by itself.
    //
    //  Two moments only : the first time a package appears in the project, and
    //  the first time its version changes. Everything else the customer opens
    //  from Window > ZLZ > Hub. A window that reappears on every recompile is the
    //  fastest way to turn a helpful installer into the thing people uninstall.
    // ─────────────────────────────────────────────────────────────────────────
    [InitializeOnLoad]
    internal static class ZLZ_HubBootstrap
    {
        const string k_SessionKey  = "ZLZ_Hub::checked";
        const string k_TouchedKey  = "ZLZ_Hub::packageTouched";

        static ZLZ_HubBootstrap()
        {
            // Import finishes with a domain reload. delayCall is the first point
            // where the AssetDatabase is queryable and EditorWindows can open.
            EditorApplication.delayCall += Check;

            // Fires only for .unitypackage imports, never for an ordinary script
            // reimport, so it is the one signal that means "the customer just
            // installed something".
            AssetDatabase.importPackageCompleted -= OnPackageImported;
            AssetDatabase.importPackageCompleted += OnPackageImported;
        }

        // An import ALWAYS opens the Hub, whatever the version bookkeeping says.
        //
        // Tying it to a version comparison meant a release where the changelog was
        // not bumped installed in total silence, and left the customer with no way
        // to discover that setup lives in the Hub - short of deleting a file in
        // ProjectSettings that nobody could be expected to know about.
        static void OnPackageImported(string packageName)
        {
            // Someone else's package should not open our window.
            if (!SessionState.GetBool(k_TouchedKey, false)) return;
            SessionState.SetBool(k_TouchedKey, false);

            EditorApplication.delayCall += OpenAfterImport;
        }

        static void OpenAfterImport()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += OpenAfterImport;
                return;
            }

            // Whatever happens next, this session has had its say.
            SessionState.SetBool(k_SessionKey, true);

            var product = ZLZ_HubRegistry.Installed.FirstOrDefault();
            if (product == null) return;

            ZLZ_HubState.For(product.Id).shown = true;
            ZLZ_HubState.Save();

            ZLZ_HubWindow.Open(product.Id, ZLZ_HubWindow.Tab.Setup);
        }

        // Records that the batch Unity is importing contains our files, so the
        // package-completed callback above can tell our package from any other.
        public static void MarkTouched() => SessionState.SetBool(k_TouchedKey, true);

        static void Check()
        {
            if (Application.isBatchMode) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += Check;
                return;
            }

            // Once per editor session, not once per domain reload.
            if (SessionState.GetBool(k_SessionKey, false)) return;
            SessionState.SetBool(k_SessionKey, true);

            // Catches the case the import callback cannot : the package was already
            // in the project when the editor started, and the Hub has never been
            // shown here.
            foreach (var p in ZLZ_HubRegistry.Installed)
            {
                var state = ZLZ_HubState.For(p.Id);
                if (state.shown) continue;

                // Written before the window opens : if the customer closes it
                // immediately, that is an answer, and we do not ask again.
                state.shown = true;
                ZLZ_HubState.Save();

                ZLZ_HubWindow.Open(p.Id, ZLZ_HubWindow.Tab.Setup);
                return;
            }
        }

    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Notices when our own files land in the project.
    //
    //  Importing a .unitypackage into a running editor recompiles but does not
    //  restart the session, so the once-per-session guard in the bootstrap would
    //  swallow the very moment the Hub is most worth showing.
    // ─────────────────────────────────────────────────────────────────────────
    internal class ZLZ_HubImportWatcher : AssetPostprocessor
    {
        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (!TouchesZLZ(imported) && !TouchesZLZ(moved) && !TouchesZLZ(deleted)) return;

            // Only flags that our files were in the batch. Whether that batch was a
            // package install is decided by importPackageCompleted, which does not
            // fire for an ordinary script save.
            ZLZ_HubBootstrap.MarkTouched();

            // An open Hub is showing a snapshot from whenever it was built, so a new
            // banner or an edited changelog has to reach it somehow.
            ZLZ_HubWindow.RefreshOpenWindows();
        }

        static bool TouchesZLZ(string[] paths)
        {
            if (paths == null) return false;

            foreach (var path in paths)
                if (path != null && path.IndexOf("/ZLZ", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;

            return false;
        }
    }
}
