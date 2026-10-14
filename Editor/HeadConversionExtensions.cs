// Defines editor integration callbacks for recipe defaults, rollback coverage and post-apply asset updates.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace digitalbreed.SyntyHeadConverter {
    /// <summary>Optional callbacks invoked synchronously on Unity's editor main thread.</summary>
    /// <remarks>
    /// Register in an InitializeOnLoad editor class. Callbacks must not start another conversion,
    /// switch scenes or modify the source prefab. Unsubscribe temporary registrations when their owner closes.
    /// ConfigureRecipe and AfterApply support multiple subscribers. AdditionalApplyFiles has one provider;
    /// a host with multiple integrations must aggregate their paths in that provider.
    /// </remarks>
    public static class HeadConversionExtensions {
        /// <summary>Customizes a newly suggested or reused recipe before automatic defaults and regions are finalized.</summary>
        /// <remarks>
        /// Invoked by HeadSource.Suggest when reuseExistingFit is true, including when an existing recipe is found.
        /// Mutate the supplied in-memory recipe; no asset is saved by this hook. Respect explicit saved choices.
        /// Setting baseConfig to a nonempty JSON object suppresses automatic fitting defaults.
        /// Exceptions abort source analysis and are shown by the window.
        /// </remarks>
        public static Action<HeadConversionRecipe> ConfigureRecipe;
        /// <summary>Returns additional files or directories to back up before applying character assets.</summary>
        /// <remarks>
        /// Assign one provider returning project-relative paths beneath Assets; null or an empty sequence adds none.
        /// The output folder is already included. Include any assets AfterApply can modify, plus directories where
        /// it may create files. Existing contents and metadata are restored on failure; new files in these paths
        /// are removed. Unlisted files and in-memory scene changes cannot be rolled back.
        /// This is called before writes, only during Apply. Exceptions abort Apply before the export starts.
        /// </remarks>
        public static Func<HeadConversionRecipe, IEnumerable<string>> AdditionalApplyFiles;
        /// <summary>Updates dependent project assets after the prefab and compact recipe have been saved.</summary>
        /// <remarks>
        /// Receives the current recipe and the saved prefab asset, not a scene instance. Invoked on every Apply,
        /// including repeated applications; make updates repeatable and save any assets you change.
        /// Exceptions trigger rollback of the output and AdditionalApplyFiles paths, then propagate to the caller.
        /// Does not run for Build preview. Register with += to preserve other subscribers.
        /// </remarks>
        public static Action<HeadConversionRecipe, GameObject> AfterApply;
    }
}
