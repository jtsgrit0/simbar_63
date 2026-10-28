using UnityEngine;

public sealed partial class SimBarExperience
{
    private SimBarFloorPerformance floorPerformance;
    private string floorMissionFeedback;
    private float floorClosingSaveTimer;
    private SimBarFloorMissions.State FloorState { get { return journey == null ? null : journey.floorMissions; } }
    private bool JourneyIsFloorClosing { get { return journeyInitialized && journeyPhase == JourneyPhase.FloorClosing && journey.inProgress; } }
    private int FloorAvailableWorkers
    {
        get
        {
            int count=2;
            for(int i=0;i<opStaff.Length;i++) if(OpStaffOnDuty(i)) count++;
            return Mathf.Min(SimBarFloorMissions.MaxCleanupWorkers,count);
        }
    }
    private bool FloorTasksPending
    {
        get { return FloorState!=null && (SimBarFloorMissions.Remaining(FloorState)>0 || FloorState.guestActive || !FloorGuestExitFinished); }
    }

    private void ResetFloorForNewNight()
    {
        CloseFloorMissionDrawer(); ClearFloorActors();
        journey.floorMissions=SimBarFloorMissions.NewState(); journey.floorClosing=false;
        journey.resultFloorBonus=0f; journey.resultFloorTasks=0;
        floorClosingSaveTimer=0f; floorMissionFeedback="";
        if(floorPerformance!=null) floorPerformance.ClearEffects();
    }

    private void PrepareFloorStateImport()
    {
        ClearFloorActors();
        if(floorPerformance!=null) floorPerformance.ClearEffects();
        floorClosingSaveTimer=0f;
    }

    private void EnsureFloorPerformance()
    {
        if(floorPerformance==null && worldRoot!=null) floorPerformance=SimBarFloorPerformance.Attach(worldRoot);
    }

    private void SyncFloorWorld()
    {
        if(FloorState==null) return;
        if(FloorState.litter>0) EnsureFloorPerformance();
        if(floorPerformance!=null)
        {
            float remaining=FloorState.litter<=0?0f:Mathf.Clamp01((FloorState.litter-FloorState.cleaned)/FloorState.litter);
            floorPerformance.SetLitter(remaining,FloorState.confettiThrows,FloorState.tissueThrows);
            floorPerformance.SetCleanup(JourneyIsFloorClosing && FloorState.cleanupActive,SimBarFloorMissions.CleanupProgress(FloorState));
        }
    }

    private void FloorThrow(int effect)
    {
        if(journeyValidationMode || !JourneyIsPlaying || (paused && !floorMissionDrawerOpen)) return;
        var result=SimBarFloorMissions.Throw(FloorState,effect,journey.secondsLeft>0f);
        if(result.success)
        {
            EnsureFloorPerformance();
            int serial=effect==SimBarFloorMissions.Confetti?FloorState.confettiThrows:FloorState.tissueThrows;
            if(floorPerformance!=null) floorPerformance.Burst(effect,serial);
            ApplyFloorRewards(result);
            floorMissionFeedback=SimBarLocalization.Text("The crowd cheers! Staff will clean the paper after the show.","관객이 환호합니다! 공연 뒤 직원 청소 미션이 생겼어요.");
            CloseFloorMissionDrawer();
            ShowToast(floorMissionFeedback,3f);
        }
        else floorMissionFeedback=SimBarLocalization.Text("Each paper performance is available twice per night.","꽃가루와 휴지 퍼포먼스는 각각 밤마다 두 번 가능합니다.");
        SyncFloorWorld(); RefreshFloorMissionUI(); SaveOperations();
    }

    private void FloorAssignCleanup()
    {
        if(!JourneyIsFloorClosing || journeyValidationMode) return;
        var result=SimBarFloorMissions.StartCleanup(FloorState,FloorAvailableWorkers,true);
        if(result.success)
        {
            EnsureFloorPerformance(); BeginFloorWorkerCleanup();
            floorMissionFeedback=SimBarLocalization.Text("Cleanup assigned. Employees are walking to the floor.","청소를 배정했습니다. 직원들이 바닥으로 이동합니다.");
            CloseFloorMissionDrawer();
            ShowToast(floorMissionFeedback,3f);
        }
        else floorMissionFeedback=SimBarLocalization.Text("Cleanup is already underway, or there is no paper left.","이미 청소 중이거나 남은 쓰레기가 없습니다.");
        RefreshFloorMissionUI(); SaveOperations();
    }

    private void FloorGuestChoice(int choice)
    {
        if(journeyValidationMode || (!JourneyIsPlaying && !JourneyIsFloorClosing) || (paused && !floorMissionDrawerOpen)) return;
        var result=SimBarFloorMissions.ResolveGuest(FloorState,choice);
        if(result.success)
        {
            ApplyFloorRewards(result);
            if(result.guestCompleted)
            {
                journey.score=Mathf.Min(100000000,journey.score+150);
                floorMissionFeedback=SimBarLocalization.Text("Thank you for understanding. A staff member will accompany you to the exit.","이해해 주셔서 감사합니다. 직원이 출입구까지 동행하겠습니다.");
                UpdateFloorGuestVisual(); CloseFloorMissionDrawer();
                ShowToast(floorMissionFeedback,4f);
            }
            else floorMissionFeedback=result.code=="water_offered"
                ?SimBarLocalization.Text("Further service is paused. Offer water and let the guest feel heard.","추가 주문을 정리하고 물을 권했습니다. 차분히 이야기를 이어가세요.")
                :SimBarLocalization.Text("The guest understands. Check their belongings and offer an escort.","손님이 안내를 이해했습니다. 소지품을 확인하고 동행을 제안하세요.");
        }
        else floorMissionFeedback=SimBarLocalization.Text("Start by listening, then explain politely before accompanying the guest.","먼저 상태를 살피고, 정중히 설명한 뒤 동행을 안내해 주세요.");
        RefreshFloorMissionUI(); SaveOperations();
    }

    private void ApplyFloorRewards(SimBarFloorMissions.Result result)
    {
        if(!result.success) return;
        fans=Mathf.Clamp(fans+result.fanGain,0,10000000);
        cash=JourneyClamp(cash+result.cashDelta,cash,0f,1000000000f);
        performance=Mathf.Clamp01(performance+result.energyGain);
    }

    private void TickFloorMissions(float delta)
    {
        if(journeyValidationMode || paused || (!JourneyIsPlaying && !JourneyIsFloorClosing) || delta<=0f || !JourneyFinite(delta)) return;
        delta=Mathf.Min(delta,.1f);
        if(FloorState==null) journey.floorMissions=SimBarFloorMissions.NewState();
        if(JourneyIsPlaying && !FloorState.guestSpawned && JourneyNightDuration-journey.secondsLeft>=70f)
        {
            var arrived=SimBarFloorMissions.SpawnGuest(FloorState,journey.day%GuestNames.Length,true);
            if(arrived.success)
            {
                UpdateFloorGuestVisual();
                ShowToast(SimBarLocalization.Text("A guest needs a courteous exit. Open FLOOR MISSIONS / H.","정중한 퇴점 안내가 필요한 손님이 있어요. 공연·홀 미션 / H"),5f);
                SaveOperations();
            }
        }
        TickFloorActors(delta);
        if(JourneyIsFloorClosing)
        {
            var worked=SimBarFloorMissions.Tick(FloorState,delta,CleanupReadyWorkers,true);
            if(worked.cleanupFinished)
            {
                journey.score=Mathf.Min(100000000,journey.score+150);
                floorMissionFeedback=SimBarLocalization.Text("The floor is clean. Staff cleanup mission complete!", "바닥이 깨끗해졌습니다. 직원 청소 미션 완료!");
                ShowToast(floorMissionFeedback,3f); SaveOperations();
            }
            floorClosingSaveTimer+=delta;
            if(floorClosingSaveTimer>=3f) { floorClosingSaveTimer=0f; SaveOperations(); }
        }
        SyncFloorWorld();
        if(floorPerformance!=null) floorPerformance.TickEffects(delta);
        RefreshFloorMissionUI();
        if(JourneyIsFloorClosing && !FloorTasksPending)
        {
            journey.floorClosing=false; CloseFloorMissionDrawer(); FinishJourneyNight();
        }
    }

    private bool TryBeginFloorClosing()
    {
        if(journeyValidationMode || !FloorTasksPending) return false;
        bool entering=!JourneyIsFloorClosing;
        CancelPourGame(false);
        if(serviceMenuOpen) CloseServiceMenu();
        if(opMenuOpen) CloseOperations();
        journey.secondsLeft=0f; gameClock=24f; journey.guidePending=false;
        journey.floorClosing=true; journey.settlementPending=false;
        journeyPhase=JourneyPhase.FloorClosing; paused=false;
        if(pausePanel!=null) pausePanel.SetActive(false);
        if(entering)
        {
            foreach(Patron patron in patrons) if(patron!=null) ClearOrder(patron);
            orderQueue.Clear(); floorClosingSaveTimer=0f;
            floorMissionFeedback=SimBarLocalization.Text("The show has ended. Finish staff cleanup and the guest's courteous exit before settlement.","공연이 끝났습니다. 직원 청소와 손님 안내를 마치면 정산됩니다.");
        }
        SyncFloorWorld(); UpdateFloorGuestVisual();
        if(FloorState.cleanupActive) BeginFloorWorkerCleanup();
        if(entering) OpenFloorMissionDrawer();
        RefreshUI(); RefreshJourneyHUD(); SaveOperations();
        return true;
    }

    private void CompleteFloorNight()
    {
        var receipt=SimBarFloorMissions.Settle(FloorState);
        if(receipt.success) { ApplyFloorRewards(receipt); journey.resultFloorBonus=receipt.cashDelta; }
        journey.resultFloorTasks=(FloorState!=null && FloorState.cleanupComplete?1:0)+(FloorState!=null && FloorState.guestComplete?1:0);
    }
}
