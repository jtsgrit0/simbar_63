using System;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

public static partial class SimBarRuntimeVerification
{
    [MenuItem("SimBar/Validation/Check Poster Scouting Save Flow")]
    public static void CheckPosterIntegrationMenu() { Debug.Log(CheckPosterIntegration()); }
    public static string CheckPosterIntegration()
    {
        var report=new Report();
        var host=Object.FindAnyObjectByType<SimBarExperience>();
        if(!Application.isPlaying || host==null || host.IsPourInProgress)
        { report.failures.Add("Enter stable Play Mode before poster integration checks."); return report.ToString(); }
        var snapshot=new JourneyCheckSnapshot(host);
        try
        {
            Write(host,"journeyValidationMode",false);
            Call(host,"ImportJourneyState",""); Write(host,"cash",20000f); Write(host,"opActiveEvent",-1);
            SetJourneyPhase(host,"Lobby"); Call(host,"JourneyStartNight",false);
            Write(host,"scoutingPosterIndex",0);
            report.Check(Read<bool>(host,"bookingShowingPosters"),"A new morning opens the real-poster scouting desk.");
            int band=SimBarPosterCatalog.Entries[0].artistIndices[0];
            Call(host,"BookingOffer",band,1);
            var plan=Property<SimBarBooking.Plan>(host,"DayPlan");
            report.Check(plan.blocks==0 && Read<float>(host,"cash")==20000f,"A locked real artist cannot consume money or time through the root booking callback.");
            Call(host,"BookingPrimaryAction");
            report.Check(plan.blocks==1 && plan.scoutedPosterMask==1 && SimBarBooking.IsDiscovered(Property<SimBarBooking.Career>(host,"BookingCareer"),band)
                && Read<float>(host,"cash")==20000f,"The primary poster action unlocks its acts in one free two-hour transaction.");
            Call(host,"BookingScoutPoster");
            report.Check(plan.blocks==1,"Repeated research cannot consume a second block.");
            host.SaveOperations(); Call(host,"ImportJourneyState",""); Call(host,"LoadOperations");
            Call(host,"JourneyReturnToLobby"); Call(host,"ContinueJourney");
            plan=Property<SimBarBooking.Plan>(host,"DayPlan");
            report.Check(plan.blocks==1 && plan.scoutedPosterMask==1 && SimBarBooking.IsDiscovered(Property<SimBarBooking.Career>(host,"BookingCareer"),band),
                "Scouting time and permanent discoveries survive the actual operations save and reload.");
            Call(host,"BookingPrimaryAction");
            report.Check(!Read<bool>(host,"bookingShowingPosters") && Read<int>(host,"bookingSelectedBand")==band,
                "The second primary action opens booking details for a discovered real artist.");
            int accepted=-1;
            foreach(int candidate in SimBarPosterCatalog.Entries[0].artistIndices)
            {
                var trial=JsonUtility.FromJson<SimBarBooking.Plan>(JsonUtility.ToJson(plan));
                SimBarBooking.Listen(trial,Property<SimBarBooking.Career>(host,"BookingCareer"),candidate);
                if(SimBarBooking.Offer(trial,Property<SimBarBooking.Career>(host,"BookingCareer"),candidate,1,20000f).success) { accepted=candidate; break; }
            }
            if(accepted<0) throw new Exception("No accepted deterministic real-artist fixture.");
            Call(host,"BookingListen",accepted); Call(host,"BookingOffer",accepted,1); Call(host,"BookingPromote");
            float cash=Read<float>(host,"cash");
            report.Check(plan.blocks==4 && plan.bookedMask==(1<<accepted) && cash==20000f-SimBarBooking.Fee(accepted,1)-SimBarBooking.PromotionCost,
                "Research, demo, real-artist offer and promotion fill four blocks and charge only accepted fees and promotion.");
            Call(host,"BookingLaunchNight"); for(int i=0;i<3;i++) Call(host,"JourneyAdvanceGuide");
            string expected=SimBarLocalization.IsKorean?SimBarBooking.Bands12[accepted].name:SimBarBooking.Bands12[accepted].nameEnglish;
            report.Check(Property<string>(host,"BookingLiveLabel").Contains(expected),"The confirmed real poster artist appears on tonight's live label.");
            Call(host,"TickJourney",180f); cash=Read<float>(host,"cash");
            int growth=Property<SimBarBooking.Career>(host,"BookingCareer").growth[accepted];
            Call(host,"SettleJourneyNight"); host.SaveOperations(); Call(host,"LoadOperations"); Call(host,"ContinueJourney");
            report.Check(growth>0 && Read<float>(host,"cash")==cash && Property<SimBarBooking.Career>(host,"BookingCareer").growth[accepted]==growth,
                "A real-name lineup settles ticket cash and simulated growth exactly once after reload.");
            Call(host,"JourneyNextNight");
            report.Check(SimBarBooking.IsDiscovered(Property<SimBarBooking.Career>(host,"BookingCareer"),accepted)
                && Property<SimBarBooking.Plan>(host,"DayPlan").blocks==0,"The next morning keeps discoveries with four fresh planning blocks.");
        }
        catch(Exception error) { report.failures.Add(error.GetBaseException().ToString()); }
        finally { snapshot.Restore(host); }
        return report.ToString();
    }
}
