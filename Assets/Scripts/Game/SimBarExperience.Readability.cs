using UnityEngine;

public sealed partial class SimBarExperience
{
    public bool ReadabilityPlaying => JourneyIsPlaying && !JourneyValidationMode;

    public void RefreshReadableModalLayout()
    {
        ApplyReadableModalLayout();
    }

    public void RefreshReadableLabels()
    {
        if (opStaffCaption != null)
            opStaffCaption.text = SimBarLocalization.Format("STAFF {0}", "직원 {0}명", 2 + OpHireCount);
    }
}
