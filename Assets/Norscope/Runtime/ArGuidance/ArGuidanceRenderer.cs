using System.Collections.Generic;
using UnityEngine;

namespace Norscope.ArGuidance
{
    /// <summary>
    /// Spawns and shows guidance holograms under the tracking origin.
    /// Future task integration: ShowGuidance / SetCurrentGuidance / HideAllGuidance.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ArGuidanceRenderer : MonoBehaviour
    {
        [SerializeField] private ArGuidanceProfile profile;
        [SerializeField] private Transform guidanceRoot;

        private readonly Dictionary<string, ArHologramVisualBase> _instances =
            new Dictionary<string, ArHologramVisualBase>();

        public Transform GuidanceRoot => guidanceRoot;

        public void Bind(ArGuidanceProfile equipmentProfile, Transform root)
        {
            profile = equipmentProfile;
            guidanceRoot = root;
        }

        public void EnsureInstances(IArCalibrationRepository repository)
        {
            if (profile == null || guidanceRoot == null || repository == null)
                return;

            ArCalibrationFile file = repository.LoadOrCreate();
            string[] ids = ArGuidanceProfile.Phase1ComponentIds;
            for (int i = 0; i < ids.Length; i++)
            {
                string id = ids[i];
                if (_instances.ContainsKey(id) && _instances[id] != null)
                    continue;

                ArComponentCalibration cal = null;
                repository.TryGetComponent(id, out cal);
                if (cal == null)
                    cal = JsonArCalibrationRepository.CreateDefaultComponent(id);

                GameObject prefab = profile.GetPrefab(cal.visualType);
                if (prefab == null)
                {
                    Debug.LogWarning("[AR Guidance] Missing prefab for " + id);
                    continue;
                }

                GameObject instance = Instantiate(prefab, guidanceRoot);
                instance.name = id;
                instance.transform.localPosition = cal.localPosition;
                instance.transform.localRotation = cal.localRotation;
                instance.transform.localScale = cal.localScale;

                ArHologramVisualBase visual = instance.GetComponent<ArHologramVisualBase>();
                if (visual == null)
                    visual = instance.GetComponentInChildren<ArHologramVisualBase>(true);
                if (visual != null)
                {
                    visual.ConfigureIdentity(id, cal.visualType);
                    visual.NotifyPoseApplied();
                    visual.SetVisible(false);
                    visual.SetManipulationEnabled(false);
                    _instances[id] = visual;
                    RegisterScrewMarkersIfAny(visual, id);
                }
                else
                {
                    // Broken prefab script → would render always-on and ignore HideAll.
                    Debug.LogWarning("[AR Guidance] Prefab missing ArHologramVisualBase: " + id);
                    instance.SetActive(false);
                }
            }
        }

        public void ApplyCalibration(ArComponentCalibration calibration)
        {
            if (calibration == null ||
                !_instances.TryGetValue(calibration.componentId, out ArHologramVisualBase visual) ||
                visual == null)
                return;

            visual.transform.localPosition = calibration.localPosition;
            visual.transform.localRotation = calibration.localRotation;
            visual.transform.localScale = calibration.localScale;
            visual.NotifyPoseApplied();
        }

        /// <summary>
        /// Prefer saved calibration; fall back to hologram-library authored defaults.
        /// </summary>
        public void ApplyPoseFor(
            string hologramId,
            IArCalibrationRepository repository,
            ArHologramLibrary library)
        {
            if (string.IsNullOrEmpty(hologramId) ||
                !_instances.TryGetValue(hologramId, out ArHologramVisualBase visual) ||
                visual == null)
                return;

            if (repository != null &&
                repository.TryGetComponent(hologramId, out ArComponentCalibration cal) &&
                cal != null)
            {
                ApplyCalibration(cal);
                return;
            }

            if (library?.entries == null)
                return;
            for (int i = 0; i < library.entries.Length; i++)
            {
                ArHologramLibraryEntry entry = library.entries[i];
                if (entry == null || entry.hologramId != hologramId)
                    continue;
                visual.transform.localPosition = entry.defaultLocalPosition;
                visual.transform.localRotation = Quaternion.Euler(entry.defaultLocalEuler);
                visual.transform.localScale = entry.defaultLocalScale.sqrMagnitude < 0.0001f
                    ? Vector3.one
                    : entry.defaultLocalScale;
                visual.NotifyPoseApplied();
                return;
            }
        }

        public void EnsureLibraryInstances(ArHologramLibrary library)
        {
            if (library?.entries == null || guidanceRoot == null)
                return;

            for (int i = 0; i < library.entries.Length; i++)
                TryEnsureLibraryEntryInstance(library.entries[i]);
        }

        /// <summary>Instantiates exactly one instance for this library entry if one doesn't
        /// already exist — shared by the eager EnsureLibraryInstances batch pass and the lazy
        /// per-selection EnsureResolved path below, so both guarantee "exactly one instance,
        /// never a duplicate" the same way.</summary>
        private bool TryEnsureLibraryEntryInstance(ArHologramLibraryEntry entry)
        {
            if (entry == null || string.IsNullOrEmpty(entry.hologramId) || entry.prefab == null)
                return false;
            if (_instances.ContainsKey(entry.hologramId) && _instances[entry.hologramId] != null)
                return true;
            if (guidanceRoot == null)
                return false;

            GameObject instance = Instantiate(entry.prefab, guidanceRoot);
            instance.name = entry.hologramId;
            instance.transform.localPosition = entry.defaultLocalPosition;
            instance.transform.localRotation = Quaternion.Euler(entry.defaultLocalEuler);
            instance.transform.localScale = entry.defaultLocalScale.sqrMagnitude < 0.0001f
                ? Vector3.one
                : entry.defaultLocalScale;

            ArHologramVisualBase visual = instance.GetComponent<ArHologramVisualBase>();
            if (visual == null)
            {
                // Do not fall back to child markers — a screw-group prefab with a broken root
                // script would otherwise register one child under the group id and never
                // expose h19_screw_01..04 (or equivalent leaves) for calibration.
                Debug.LogWarning("[AR Guidance] Library prefab missing root visual: " + entry.hologramId);
                Destroy(instance);
                return false;
            }

            visual.ConfigureIdentity(entry.hologramId, entry.visualType);
            visual.NotifyPoseApplied();
            visual.SetVisible(false);
            visual.SetManipulationEnabled(false);
            _instances[entry.hologramId] = visual;
            RegisterScrewMarkersIfAny(visual, entry.hologramId);

            // Independently-registered screw leaves (H04, one top-level library entry per
            // screw — no shared group parent) have nothing else numbering them, unlike group
            // children handled by RegisterScrewMarkersIfAny above.
            ArScrewMarkerVisual soloMarker = visual as ArScrewMarkerVisual;
            if (soloMarker != null)
            {
                int number = ArCalibrationIds.ParseScrewNumber(entry.hologramId);
                if (number > 0)
                    soloMarker.SetScrewNumber(number);
            }

            return true;
        }

        /// <summary>Standard local position for a leaf's very first spawn this session, used
        /// only when no saved calibration exists yet for it. Slightly above the QR so it is
        /// not buried in the marker surface / debug cube.</summary>
        public static readonly Vector3 DefaultFirstSpawnLocalPosition = new Vector3(0f, 0.08f, 0f);

        /// <summary>Calibration findability scale for tiny screw markers (saved pose stores
        /// authored scale 1 — this boost is applied only while showing, then normalized on save
        /// via SaveSelected reading current transform; we keep authored scale at 1 and boost
        /// geometry instead in ShowSelected).</summary>
        public const float CalibrationScrewFindScale = 3f;

        /// <summary>
        /// Resolves componentId's hologram from the library on demand — the entry point a
        /// component selection should always go through. Returns true immediately if an
        /// instance already exists (never creates a duplicate). Screw leaf ids that share a
        /// group prefab (H19) resolve via their parent group entry, which registers every
        /// sibling screw marker as a side effect. If a saved calibration exists for
        /// componentId it is applied; otherwise the instance is placed at
        /// DefaultFirstSpawnLocalPosition so never-calibrated holograms appear at the QR /
        /// tracking origin for calibration (even when BeginSession already applied a library
        /// panel offset). Returns false with a human-readable error (also logged) if the id
        /// cannot be resolved at all.
        /// </summary>
        public bool EnsureResolved(
            string componentId,
            IArCalibrationRepository repository,
            ArHologramLibrary library,
            out string error)
        {
            error = null;
            if (string.IsNullOrEmpty(componentId))
            {
                error = "No component selected.";
                return false;
            }

            if (_instances.TryGetValue(componentId, out ArHologramVisualBase existing) && existing != null)
            {
                ApplySavedOrFirstSpawnPose(componentId, existing, repository);
                return true;
            }

            if (library == null)
            {
                error = "Hologram library is not assigned.";
                Debug.LogError("[AR Guidance] " + error);
                return false;
            }

            string lookupId = ResolveLibraryLookupId(componentId);
            ArHologramLibraryEntry entry = library.Find(lookupId);
            if (entry == null || entry.prefab == null)
            {
                error = "No prefab registered for \"" + componentId + "\" in the hologram library.";
                Debug.LogError("[AR Guidance] " + error);
                return false;
            }

            if (!TryEnsureLibraryEntryInstance(entry))
            {
                error = "Prefab for \"" + componentId + "\" could not be instantiated.";
                Debug.LogError("[AR Guidance] " + error);
                return false;
            }

            if (!_instances.TryGetValue(componentId, out ArHologramVisualBase resolved) || resolved == null)
            {
                error = "\"" + componentId + "\" was not found after instantiating its group prefab.";
                Debug.LogError("[AR Guidance] " + error);
                return false;
            }

            ApplySavedOrFirstSpawnPose(componentId, resolved, repository);
            return true;
        }

        /// <summary>
        /// Saved calibration wins; otherwise snap to the origin-adjacent first-spawn pose so
        /// the user can see and grab the hologram during calibration.
        /// </summary>
        private void ApplySavedOrFirstSpawnPose(
            string componentId,
            ArHologramVisualBase visual,
            IArCalibrationRepository repository)
        {
            if (visual == null)
                return;

            if (repository != null &&
                repository.TryGetComponent(componentId, out ArComponentCalibration cal) &&
                cal != null)
            {
                ApplyCalibration(cal);
                return;
            }

            visual.transform.localPosition = DefaultFirstSpawnLocalPosition;
            visual.NotifyPoseApplied();
        }

        private static string ResolveLibraryLookupId(string componentId)
        {
            // H04 and H19 screws are independent top-level library entries (one per screw).
            return componentId;
        }

        private void RegisterScrewMarkersIfAny(ArHologramVisualBase visual, string groupId)
        {
            ArScrewGroupVisual group = visual as ArScrewGroupVisual;
            if (group == null)
                group = visual != null ? visual.GetComponent<ArScrewGroupVisual>() : null;
            if (group == null)
                return;

            // Parent stays at identity so child locals are QR-origin-relative.
            group.transform.localPosition = Vector3.zero;
            group.transform.localRotation = Quaternion.identity;
            group.transform.localScale = Vector3.one;

            // Prefer hierarchy discovery — serialized marker refs can break after prefab rewrites.
            ArScrewMarkerVisual[] markers = group.GetComponentsInChildren<ArScrewMarkerVisual>(true);
            if (markers == null || markers.Length == 0)
                markers = group.Markers;
            if (markers == null || markers.Length == 0)
            {
                Debug.LogWarning("[AR Guidance] Screw group has no markers: " + groupId);
                return;
            }

            for (int i = 0; i < markers.Length; i++)
            {
                ArScrewMarkerVisual marker = markers[i];
                if (marker == null)
                    continue;
                string id = ArCalibrationIds.ScrewComponentId(groupId, i + 1);
                marker.ConfigureIdentity(id, ArVisualType.ScrewMarker);
                marker.SetScrewNumber(i + 1);
                marker.SetVisible(false);
                marker.SetManipulationEnabled(false);
                _instances[id] = marker;
            }
        }

        public string[] GetCalibratableIds()
        {
            var ids = new List<string>();
            string[] preferred = ArCalibrationIds.LeafCalibrationOrder;
            for (int i = 0; i < preferred.Length; i++)
            {
                string id = preferred[i];
                if (_instances.TryGetValue(id, out ArHologramVisualBase visual) &&
                    visual != null &&
                    !ids.Contains(id))
                    ids.Add(id);
            }

            return ids.ToArray();
        }

        /// <summary>
        /// Legacy screw-group parents (if still present) must remain identity so any
        /// remaining child locals stay QR-relative. H04/H19 screws are independent
        /// top-level entries and do not use a group parent.
        /// </summary>
        public void EnforceScrewGroupParentIdentity()
        {
            EnforceIdentity("bottom_cover_screw_group");
        }

        private void EnforceIdentity(string componentId)
        {
            if (!_instances.TryGetValue(componentId, out ArHologramVisualBase visual) ||
                visual == null)
                return;
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one;
        }

        public void HideAll()
        {
            foreach (var pair in _instances)
            {
                if (pair.Value != null)
                {
                    pair.Value.SetVisible(false);
                    pair.Value.SetManipulationEnabled(false);
                    pair.Value.SetVisualState(ArHologramVisualState.Default);
                }
            }
        }

        public void SetVisible(string componentId, bool visible)
        {
            if (!_instances.TryGetValue(componentId, out ArHologramVisualBase visual) ||
                visual == null)
                return;

            ArScrewMarkerVisual marker = visual as ArScrewMarkerVisual;
            if (marker != null)
            {
                SetScrewMarkerVisible(marker, visible);
                if (visible)
                    marker.SetOpacity(Mathf.Max(marker.Opacity, 0.85f));
                return;
            }

            visual.SetVisible(visible);
            if (visible && visual.Opacity < 0.05f)
                visual.SetOpacity(0.85f);
        }

        /// <summary>
        /// Screw markers are children of a group. Parent must be active or they never appear.
        /// Solo calibration shows only the selected marker.
        /// </summary>
        private static void SetScrewMarkerVisible(ArScrewMarkerVisual marker, bool visible)
        {
            if (marker == null)
                return;

            ArScrewGroupVisual group = marker.GetComponentInParent<ArScrewGroupVisual>(true);
            if (group == null)
            {
                marker.SetVisible(visible);
                return;
            }

            if (!visible)
            {
                marker.SetVisible(false);
                ArScrewMarkerVisual[] markers = group.Markers;
                bool any = false;
                if (markers != null)
                {
                    for (int i = 0; i < markers.Length; i++)
                    {
                        if (markers[i] != null && markers[i].IsVisible)
                        {
                            any = true;
                            break;
                        }
                    }
                }

                if (!any && group.gameObject.activeSelf)
                    group.gameObject.SetActive(false);
                return;
            }

            if (!group.gameObject.activeSelf)
                group.gameObject.SetActive(true);

            ArScrewMarkerVisual[] all = group.Markers;
            if (all == null || all.Length == 0)
                all = group.GetComponentsInChildren<ArScrewMarkerVisual>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] == null)
                    continue;
                all[i].SetVisible(all[i] == marker);
            }
        }

        public ArHologramVisualBase GetVisual(string componentId)
        {
            _instances.TryGetValue(componentId, out ArHologramVisualBase visual);
            return visual;
        }

        /// <summary>Diagnostics helper — counts how many spawned instances currently report
        /// themselves visible. Used to confirm "show only the selected visual" held after a
        /// selection change (should be 0 or 1 in calibration mode).</summary>
        public int CountVisible()
        {
            int count = 0;
            foreach (var pair in _instances)
            {
                if (pair.Value != null && pair.Value.IsVisible)
                    count++;
            }

            return count;
        }

        public void CollectAllVisuals(List<ArHologramVisualBase> into)
        {
            if (into == null)
                return;
            foreach (var pair in _instances)
            {
                if (pair.Value == null || into.Contains(pair.Value))
                    continue;
                into.Add(pair.Value);
            }
        }

        /// <summary>Future task-step integration entry point. Unused in Phase 1.</summary>
        public void ShowGuidance(string componentId)
        {
            HideAll();
            SetVisible(componentId, true);
        }

        /// <summary>Future task-step integration entry point. Unused in Phase 1.</summary>
        public void SetCurrentGuidance(ArGuidanceDefinition definition)
        {
            if (definition == null)
            {
                HideAllGuidance();
                return;
            }

            HideAll();
            SetVisible(definition.componentId, true);
            ArHologramVisualBase visual = GetVisual(definition.componentId);
            if (visual != null)
                visual.SetVisualState(definition.state);
        }

        public void HideAllGuidance() => HideAll();
    }
}
