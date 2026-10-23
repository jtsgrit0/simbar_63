public sealed partial class SimBarExperience
{
    public bool ReadabilityPlaying => JourneyIsPlaying && !JourneyValidationMode;

    // Compatibility hooks for editor tooling built around the former layout.
    // The original UI is constructed directly by its owning partial classes.
    public void RefreshReadableModalLayout() { }
    public void ApplyReadableModalLayout() { }
    public void RefreshReadableLabels(bool compact = false, bool inlineDrinks = false) { }
}
