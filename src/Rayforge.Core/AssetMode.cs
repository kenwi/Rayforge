namespace Rayforge;

/// <summary>How the web host decides which assets to fetch into the Emscripten file system.</summary>
public enum AssetMode
{
    /// <summary>
    /// A build-generated list: the host loads everything before the first frame, with no progress UI.
    /// Right for small game asset sets.
    /// </summary>
    Bundled,

    /// <summary>
    /// A manifest with groups and progress, loaded before the first frame (or on demand in a later
    /// milestone). For large asset sets. Both modes use fetch -> MEMFS; only the list differs.
    /// </summary>
    Preload,
}
