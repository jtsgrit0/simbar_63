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
        journey.floorMissions.reaction=SimBarFloorChallenges.NewState(UnityEngine.Random.Range(1,int.MaxValue));
        journey.resultFloorBonus=0f; journey.resultFloorTasks=0;
        floorClosingSaveTimer=0f; floorMissionFeedback="";
        if(floorPerformance!=null) floorPerformance.ClearEffects();
    }

    private void PrepareFloorStateImport()
    {
        CloseFloorMissionDrawer(); ClearFloorActors();
        if(floorPerformance!=null) floorPerformance.ClearEffects();
        floorClosingSaveTimer=0f; floorMissionFeedback="";
    }

    private void EnsureFloorReaction()
    {
        if(FloorState==null) journey.floorMissions=SimBarFloorMissions.NewState();
        // Legacy saves receive future events only; a saved schedule never rerolls.
        if(FloorState.reaction==null)
            FloorState.reaction=SimBarFloorChallenges.NewState(UnityEngine.Random.Range(1,int.MaxValue),JourneyNightDuration-journey.secondsLeft);
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

    private void ApplyRandomFloorPerformance(int effect)
    {
        var result=SimBarFloorMissions.Throw(FloorState,effect,JourneyIsPlaying && journey.secondsLeft>0f);
        if(!result.success) return;
        EnsureFloorPerformance();
        int serial=effect==SimBarFloorMissions.Confetti?FloorState.confettiThrows:FloorState.tissueThrows;
        if(floorPerformance!=null) floorPerformance.Burst(effect,serial);
        ApplyFloorRewards(result);
        floorMissionFeedback=effect==SimBarFloorMissions.Confetti
            ?SimBarLocalization.Text("The band unexpectedly throws confetti! Staff will need to clean after the show.","밴드가 깜짝 꽃가루 퍼포먼스를 했어요! 공연 후 청소가 필요합니다.")
            :SimBarLocalization.Text("Tissue streamers fly across the crowd! A cleanup mission will follow the show.","관객 위로 휴지가 날립니다! 공연 후 청소 미션이 생겼어요.");
        ShowToast(floorMissionFeedback,4f);
    }

    private void EnsureFloorCleanupAssigned()
    {
        if(!JourneyIsFloorClosing || FloorState==null || SimBarFloorMissions.Remaining(FloorState)<=0 || FloorState.cleanupActive) return;
        var assigned=SimBarFloorMissions.StartCleanup(FloorState,FloorAvailableWorkers,true);
        if(!assigned.success) return;
        EnsureFloorPerformance(); BeginFloorWorkerCleanup();
        floorMissionFeedback=SimBarLocalization.Text("Employees are heading to the floor. When prompted, tap C or the cleanup button rapidly!","직원들이 청소 위치로 이동합니다. 안내가 뜨면 C 키나 청소 버튼을 빠르게 연타하세요!");
        ShowToast(floorMissionFeedback,4f);
    }

    private void FloorQuickPress(int kind)
    {
        if(journeyValidationMode || paused || (!JourneyIsPlaying && !JourneyIsFloorClosing) || FloorState==null || FloorState.reaction==null) return;
        var reaction=FloorState.reaction;
        if(kind==SimBarFloorChallenges.Guest && (!FloorState.guestActive || reaction.activeStage!=FloorState.guestStep)) return;
        if(kind==SimBarFloorChallenges.Cleanup && (!JourneyIsFloorClosing || !FloorState.cleanupActive || CleanupReadyWorkers<=0)) return;
        var pressed=SimBarFloorChallenges.Press(reaction,kind,true);
        if(!pressed.accepted) return;
        if(pressed.completed)
        {
            if(kind==SimBarFloorChallenges.Guest) CompleteFloorGuestReaction(pressed.stage);
            else
            {
                floorMissionFeedback=SimBarLocalization.Text("Great response! Your employees are clearing the paper. Watch for the next C prompt.","빠르게 대응했어요! 직원들이 종이를 정리합니다. 다음 C 안내를 기다리세요.");
                ShowToast(floorMissionFeedback,2.5f);
            }
        }
        RefreshFloorMissionUI(); SaveOperations();
    }

    private void CompleteFloorGuestReaction(int stage)
    {
        var result=SimBarFloorMissions.ResolveGuest(FloorState,stage);
        if(!result.success) return;
        ApplyFloorRewards(result);
        if(result.guestCompleted)
        {
            journey.score=Mathf.Min(100000000,journey.score+150);
            floorMissionFeedback=SimBarLocalization.Text("Thank you for understanding. An employee will accompany you to the exit.","이해해 주셔서 감사합니다. 직원이 출입구까지 동행하겠습니다.");
            UpdateFloorGuestVisual(); ShowToast(floorMissionFeedback,4f);
        }
        else floorMissionFeedback=result.code=="water_offered"
            ?SimBarLocalization.Text("Further orders are stopped and water is offered. Keep your voice calm for the next F prompt.","추가 주문을 멈추고 물을 안내했어요. 다음 F 안내에서 차분히 설명하세요.")
            :SimBarLocalization.Text("The guest understands. Next, check belongings and arrange an employee escort.","손님이 설명을 이해했어요. 다음에는 소지품을 확인하고 직원 동행을 안내하세요.");
    }

    private void ApplyFloorRewards(SimBarFloorMissions.Result result)
    {
        if(!result.success) return;
        fans=Mathf.Clamp(fans+result.fanGain,0,10000000);
        cash=JourneyClamp(cash+result.cashDelta,cash,0f,1000000000f);
        performance=Mathf.Clamp01(performance+result.energyGain);
    }

    private void StartPendingFloorReaction()
    {
        var reaction=FloorState.reaction;
        bool started=false;
        if(FloorState.guestActive)
            started=SimBarFloorChallenges.Start(reaction,SimBarFloorChallenges.Guest,FloorState.guestStep);
        else if(JourneyIsFloorClosing && FloorState.cleanupActive && CleanupReadyWorkers>0 && reaction.cleanupWorkBudget<=.001f)
            started=SimBarFloorChallenges.Start(reaction,SimBarFloorChallenges.Cleanup,0);
        if(started) { floorMissionFeedback=""; SaveOperations(); }
    }

    private void TickFloorMissions(float delta)
    {
        if(journeyValidationMode || paused || (!JourneyIsPlaying && !JourneyIsFloorClosing) || delta<=0f || !JourneyFinite(delta)) return;
        float reactionDelta=Mathf.Min(delta,.5f);
        delta=Mathf.Min(delta,.1f);
        EnsureFloorReaction();
        var reaction=FloorState.reaction;
        if(JourneyIsPlaying)
        {
            var due=SimBarFloorChallenges.AdvanceSchedule(reaction,JourneyNightDuration-journey.secondsLeft,true);
            for(int i=0;i<due.confetti;i++) ApplyRandomFloorPerformance(SimBarFloorMissions.Confetti);
            for(int i=0;i<due.tissue;i++) ApplyRandomFloorPerformance(SimBarFloorMissions.Tissue);
            if(due.guest && !FloorState.guestSpawned)
            {
                if(SimBarFloorMissions.SpawnGuest(FloorState,journey.day%GuestNames.Length,true).success)
                {
                    UpdateFloorGuestVisual();
                    floorMissionFeedback=SimBarLocalization.Text("A guest unexpectedly needs help! Tap F or the assistance button quickly and courteously.","갑자기 안내가 필요한 손님이 나타났어요! F 키나 안내 버튼을 빠르게 연타해 정중히 응대하세요.");
                    ShowToast(floorMissionFeedback,4f);
                }
            }
            if(due.confetti>0 || due.tissue>0 || due.guest) SaveOperations();
        }
        EnsureFloorCleanupAssigned();
        TickFloorActors(delta);
        var tick=SimBarFloorChallenges.Tick(reaction,reactionDelta,true);
        if(tick.timedOut)
        {
            satisfaction=Mathf.Max(.3f,satisfaction-.015f);
            floorMissionFeedback=SimBarLocalization.Text("Time ran out. Stay calm: another rapid-response chance will appear shortly.","시간을 놓쳤어요. 차분히 준비하세요! 잠시 뒤 다시 연타로 대응할 수 있습니다.");
            ShowToast(floorMissionFeedback,2f); SaveOperations();
        }
        if(JourneyIsFloorClosing)
        {
            int ready=CleanupReadyWorkers;
            float before=FloorState.cleaned;
            float workDelta=ready>0?Mathf.Min(delta,reaction.cleanupWorkBudget/(ready*SimBarFloorMissions.CleanupRatePerWorker)):0f;
            var worked=SimBarFloorMissions.Tick(FloorState,workDelta,ready,true);
            reaction.cleanupWorkBudget=Mathf.Max(0f,reaction.cleanupWorkBudget-(FloorState.cleaned-before));
            if(reaction.cleanupWorkBudget<.001f) reaction.cleanupWorkBudget=0f;
            if(worked.cleanupFinished)
            {
                journey.score=Mathf.Min(100000000,journey.score+150);
                floorMissionFeedback=SimBarLocalization.Text("The floor is clean. Rapid-response cleanup mission complete!","바닥이 깨끗해졌어요. 연타 청소 미션 완료!");
                ShowToast(floorMissionFeedback,3f); SaveOperations();
            }
            floorClosingSaveTimer+=delta;
            if(floorClosingSaveTimer>=3f) { floorClosingSaveTimer=0f; SaveOperations(); }
        }
        StartPendingFloorReaction();
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
        EnsureFloorReaction();
        if(entering)
        {
            foreach(Patron patron in patrons) if(patron!=null) ClearOrder(patron);
            orderQueue.Clear(); floorClosingSaveTimer=0f; CloseFloorMissionDrawer();
            floorMissionFeedback=SimBarLocalization.Text("Show over! Watch for C cleanup and F guest-assistance prompts before settlement.","공연이 끝났어요! C 청소·F 손님 안내가 뜨면 빠르게 연타하고 마감하세요.");
        }
        EnsureFloorCleanupAssigned();
        SyncFloorWorld(); UpdateFloorGuestVisual();
        if(FloorState.cleanupActive) BeginFloorWorkerCleanup();
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
