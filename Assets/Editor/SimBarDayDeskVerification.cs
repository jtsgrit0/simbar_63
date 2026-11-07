using System;
using UnityEditor;
using UnityEngine;

public static partial class SimBarRuntimeVerification
{
    [MenuItem("SimBar/Validation/Check Selected Owner Desk Model")]
    public static void CheckDeskMenu() => Debug.Log(CheckDesk());

    /// <summary>Checks the new deterministic desk without changing the legacy plan factory or player data.</summary>
    public static string CheckDesk()
    {
        var report=new Report();
        var random=UnityEngine.Random.state;
        try
        {
            var career=SimBarBooking.NewCareer();
            var plan=SimBarBooking.NewDeskPlan(2,1);
            report.Check(SimBarBooking.FreewillIndex==24&&SimBarBooking.BandCount==25&&SimBarBooking.AvailableBandIndices.Length==1&&SimBarBooking.AvailableBandIndices[0]==24&&SimBarBooking.Bands12[24].id=="freewill", "Freewill is appended at stable index 24 and is the only available desk artist.");
            report.Check(!SimBarBooking.IsDesk(SimBarBooking.NewPlan(2,1))&&SimBarBooking.CanAct(SimBarBooking.NewPlan(2,1)),"Legacy NewPlan retains its four-block rules.");
            report.Check(SimBarBooking.IsDesk(plan)&&SimBarBooking.Hour(plan)==10&&!SimBarBooking.CanLaunchDesk(plan)&&SimBarBooking.DeskWork(plan,0).code=="band_required", "The desk starts unselected at 10:00 and cannot launch or work without a band.");
            report.Check(SimBarBooking.DeskSelect(plan,true).success&&SimBarBooking.Hour(plan)==13&&SimBarBooking.DeskWork(plan,1).code=="plan_required", "Selection is free and a phone call requires the computer plan first.");
            var preview=SimBarBooking.Forecast(plan,career);
            report.Check(preview.bookedCount==1&&preview.attendance==26&&preview.ticketIncome==390f&&plan.bookedMask==0,"Selected Freewill previews one show without committing or charging a booking.");
            report.Check(SimBarBooking.DeskWork(plan,0).success&&SimBarBooking.Hour(plan)==16&&!SimBarBooking.CanLaunchDesk(plan)&&SimBarBooking.LockDesk(plan,career).code=="desk_incomplete", "The computer advances to 16:00 while launch remains gated by confirmation.");
            string before=JsonUtility.ToJson(plan);
            report.Check(SimBarBooking.DeskWork(plan,0).code=="already_done"&&JsonUtility.ToJson(plan)==before,"Repeated computer work has no state or financial effects.");
            plan=JsonUtility.FromJson<SimBarBooking.Plan>(before);SimBarBooking.Sanitize(plan,career);
            report.Check(plan.deskDoneMask==1&&plan.freewillSelected&&SimBarBooking.DeskWork(plan,1).success&&SimBarBooking.Hour(plan)==18&&SimBarBooking.CanLaunchDesk(plan),"A saved partial plan resumes and the phone completes preparation at 18:00.");
            before=JsonUtility.ToJson(plan);
            report.Check(SimBarBooking.DeskWork(plan,1).code=="already_done"&&JsonUtility.ToJson(plan)==before,"Repeated confirmation is idempotent.");
            report.Check(SimBarBooking.DeskSelect(plan,false).success&&plan.deskDoneMask==0&&SimBarBooking.Hour(plan)==10&&!SimBarBooking.CanLaunchDesk(plan),"Deselecting reverses the checklist and clears both tasks.");
            SimBarBooking.DeskSelect(plan,true);SimBarBooking.DeskWork(plan,0);SimBarBooking.DeskWork(plan,1);
            before=JsonUtility.ToJson(plan);
            report.Check(SimBarBooking.Listen(plan,career,0).code=="desk_only"&&SimBarBooking.Offer(plan,career,0,0,1000).code=="desk_only"&&SimBarBooking.Promote(plan,career,1000).code=="desk_only"&&SimBarBooking.Scout(plan,career,0).code=="desk_only"&&!SimBarBooking.CanAct(plan)&&JsonUtility.ToJson(plan)==before,"Legacy scouting, offers, demos and promotion cannot alter a desk plan.");
            career.growth[24]=17;career.growth[0]=90;
            var locked=SimBarBooking.LockDesk(plan,career);var forecast=SimBarBooking.Forecast(plan,career);before=JsonUtility.ToJson(plan);
            report.Check(locked.success&&locked.cashDelta==0&&plan.bookedMask==(1<<24)&&plan.attemptedMask==(1<<24)&&plan.attempts==1&&plan.bookedGrowth[24]==17&&plan.bookedGrowth[0]==0&&plan.blocks==0&&plan.promotions==0&&plan.ticketTier==1,"Lock commits only Freewill, snapshots its growth and charges no fee.");
            career.growth[24]=99;
            report.Check(SimBarBooking.LockDesk(plan,career).code=="locked"&&JsonUtility.ToJson(plan)==before&&JsonUtility.ToJson(SimBarBooking.Forecast(plan,career))==JsonUtility.ToJson(forecast),"A locked desk and its frozen forecast resist duplicate launch and later career changes.");
            plan=JsonUtility.FromJson<SimBarBooking.Plan>(before);SimBarBooking.Sanitize(plan,career);
            report.Check(plan.freewillSelected&&plan.deskDoneMask==3&&plan.bookedMask==(1<<24)&&JsonUtility.ToJson(SimBarBooking.Forecast(plan,career))==JsonUtility.ToJson(forecast),"Locked desk saves retain the exact frozen receipt.");
            var paid=SimBarBooking.Complete(plan,career,3);before=BookingSnapshot(plan,career);
            report.Check(paid.success&&paid.income==forecast.ticketIncome&&SimBarBooking.Complete(plan,career,3).code=="already_completed"&&BookingSnapshot(plan,career)==before,"Desk settlement pays and grows the artist only once.");
            var malformed=SimBarBooking.NewDeskPlan(1,0);malformed.freewillSelected=true;malformed.deskDoneMask=2;malformed.bookedMask=7;malformed.promotions=2;
            SimBarBooking.Sanitize(malformed,career);
            report.Check(malformed.deskDoneMask==0&&malformed.bookedMask==0&&malformed.promotions==0&&!SimBarBooking.CanLaunchDesk(malformed),"Malformed desk progress and legacy masks are repaired without granting preparation.");
            report.Check(!SimBarBooking.DeskSelect(null,true).success&&!SimBarBooking.DeskWork(null,0).success&&!SimBarBooking.LockDesk(null,career).success,"Missing desk input is rejected safely.");
        }
        catch(Exception e){report.failures.Add(e.ToString());}
        finally{UnityEngine.Random.state=random;}
        return report.ToString();
    }
}
