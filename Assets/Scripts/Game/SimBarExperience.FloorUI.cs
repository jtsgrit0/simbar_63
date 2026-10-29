using UnityEngine;
using UnityEngine.UI;

/// <summary>Live random-event reaction buttons and a read-only floor status panel.</summary>
public sealed partial class SimBarExperience
{
    private bool floorMissionDrawerOpen;
    private bool floorMissionUiVisible, floorMissionUiKorean;
    private Canvas floorMissionCanvas;
    private SimBarBookingLayout floorMissionLayout;
    private RectTransform floorMissionUiRoot, floorMissionDrawer, floorReactionCard;
    private RectTransform floorReactionTrack, floorReactionFill, floorDetailViewport, floorDetailContent;
    private Button floorMissionHudButton, floorMissionCloseButton, floorReactionButton;
    private Text floorMissionTitle, floorMissionDetail, floorMissionDetailNote;
    private Text floorReactionTitle, floorReactionClock, floorReactionNarrative;
    private Text floorReactionCount, floorReactionFeedback;
    private ScrollRect floorDetailScroll;

    private static readonly Color FloorUiBackground = new Color(.045f,.060f,.085f,.99f);
    private static readonly Color FloorUiCard = new Color(.075f,.095f,.125f,1f);
    private static readonly Color FloorUiLine = new Color(.16f,.20f,.25f,1f);
    private static readonly Color FloorUiGold = new Color(.95f,.78f,.43f,1f);
    private static readonly Color FloorUiGreen = new Color(.16f,.40f,.34f,1f);
    private static readonly Color FloorUiMuted = new Color(.72f,.77f,.78f,1f);

    private void BuildFloorMissionUI()
    {
        if(floorMissionCanvas!=null) return;
        var host=new GameObject("Spontaneous floor missions",typeof(RectTransform),typeof(Canvas),typeof(GraphicRaycaster));
        host.transform.SetParent(transform,false);
        floorMissionCanvas=host.GetComponent<Canvas>();
        floorMissionCanvas.renderMode=RenderMode.ScreenSpaceOverlay;
        floorMissionCanvas.sortingOrder=620;
        floorMissionUiRoot=BookingPanel("Floor mission HUD root",host.transform,Color.clear);
        BookingStretch(floorMissionUiRoot);
        floorMissionUiRoot.GetComponent<Image>().raycastTarget=false;
        floorMissionHudButton=BookingButton("Random floor mission status",floorMissionUiRoot,"","",13,FloorUiBackground,FloorUiGold);
        floorMissionHudButton.onClick.AddListener(ToggleFloorMissionDrawer);

        floorReactionCard=BookingPanel("Timed spontaneous floor reaction",floorMissionUiRoot,FloorUiBackground);
        floorReactionTitle=BookingText("Reaction event heading",floorReactionCard,"","",18,FloorUiGold,true);
        floorReactionClock=BookingText("Reaction seconds remaining",floorReactionCard,"","",24,Color.white,true);
        floorReactionClock.alignment=TextAnchor.UpperRight;
        floorReactionClock.horizontalOverflow=HorizontalWrapMode.Overflow;
        floorReactionClock.verticalOverflow=VerticalWrapMode.Overflow;
        floorReactionClock.resizeTextForBestFit=true;
        floorReactionClock.resizeTextMinSize=18;
        floorReactionClock.resizeTextMaxSize=24;
        floorReactionNarrative=BookingText("Reaction courteous task",floorReactionCard,"","",14,Color.white,true);
        floorReactionCount=BookingText("Reaction rapid press progress",floorReactionCard,"","",16,FloorUiGold,true);
        floorReactionTrack=BookingPanel("Reaction press progress track",floorReactionCard,FloorUiLine);
        floorReactionTrack.GetComponent<Image>().raycastTarget=false;
        floorReactionFill=BookingPanel("Reaction press progress fill",floorReactionTrack,FloorUiGreen);
        floorReactionFill.GetComponent<Image>().raycastTarget=false;
        floorReactionButton=BookingButton("Rapidly tap to respond",floorReactionCard,"","",18,FloorUiGreen,Color.white);
        floorReactionButton.onClick.AddListener(() =>
        {
            if(FloorState!=null && FloorState.reaction!=null)
                FloorQuickPress(FloorState.reaction.activeKind);
        });
        floorReactionFeedback=BookingText("Reaction result and retry feedback",floorReactionCard,"","",12,FloorUiMuted);

        floorMissionDrawer=BookingPanel("Read-only spontaneous floor status",floorMissionUiRoot,FloorUiCard);
        floorMissionTitle=BookingText("Floor status title",floorMissionDrawer,"RANDOM FLOOR EVENTS","돌발 홀 미션 현황",20,FloorUiGold,true);
        floorMissionDetailNote=BookingText("Floor status live-time note",floorMissionDrawer,
            "Events occur on their own. The show and reaction clock keep running.",
            "이벤트는 무작위로 발생합니다. 이 창에서도 공연과 미션 시간은 계속 흐릅니다.",12,FloorUiMuted);
        floorDetailViewport=BookingPanel("Floor status scrolling viewport",floorMissionDrawer,new Color(1f,1f,1f,.001f));
        floorDetailViewport.gameObject.AddComponent<RectMask2D>();
        floorDetailContent=BookingPanel("Floor status scrolling content",floorDetailViewport,Color.clear);
        floorDetailContent.GetComponent<Image>().raycastTarget=false;
        floorMissionDetail=BookingText("Read-only floor event and worker status",floorDetailContent,"","",13,Color.white);
        floorDetailScroll=floorDetailViewport.gameObject.AddComponent<ScrollRect>();
        floorDetailScroll.viewport=floorDetailViewport; floorDetailScroll.content=floorDetailContent;
        floorDetailScroll.horizontal=false; floorDetailScroll.vertical=true;
        floorDetailScroll.movementType=ScrollRect.MovementType.Clamped;
        floorDetailScroll.scrollSensitivity=28f;
        floorMissionCloseButton=BookingButton("Close read-only floor status",floorMissionDrawer,
            "CLOSE / THE SHOW CONTINUES","닫기 / 공연은 계속됩니다",14,FloorUiLine,Color.white);
        floorMissionCloseButton.onClick.AddListener(CloseFloorMissionDrawer);
        floorMissionDrawer.gameObject.SetActive(false);
        floorReactionCard.gameObject.SetActive(false);
        floorMissionUiKorean=SimBarLocalization.IsKorean;
        floorMissionLayout=SimBarBookingLayout.Attach(floorMissionCanvas,LayoutFloorMissionUI);
        RefreshFloorMissionUI();
    }

    private void OpenFloorMissionDrawer()
    {
        if(floorMissionCanvas==null || floorMissionDrawerOpen || JourneyValidationMode
            || (!JourneyIsPlaying && !JourneyIsFloorClosing)) return;
        if(paused && !serviceMenuOpen && !opMenuOpen) return;
        if(serviceMenuOpen) CloseServiceMenu();
        if(opMenuOpen) CloseOperations();
        if(paused) return;
        floorMissionDrawerOpen=true;
        floorMissionLayout.RefreshLayout();
        floorDetailScroll.StopMovement();
        floorDetailScroll.verticalNormalizedPosition=1f;
        RefreshFloorMissionUI();
    }

    private void CloseFloorMissionDrawer()
    {
        floorMissionDrawerOpen=false;
        if(floorMissionDrawer!=null) floorMissionDrawer.gameObject.SetActive(false);
    }

    private void ToggleFloorMissionDrawer()
    {
        if(floorMissionDrawerOpen) CloseFloorMissionDrawer();
        else OpenFloorMissionDrawer();
    }

    private void RefreshFloorMissionUI()
    {
        if(floorMissionCanvas==null) return;
        bool visible=!JourneyValidationMode && (JourneyIsPlaying || JourneyIsFloorClosing);
        if(!visible) CloseFloorMissionDrawer();
        if(floorMissionCanvas.gameObject.activeSelf!=visible) floorMissionCanvas.gameObject.SetActive(visible);
        if(!visible) { floorMissionUiVisible=false; return; }
        if(!floorMissionUiVisible) { floorMissionUiVisible=true; floorMissionLayout.RefreshLayout(); }
        if(floorMissionUiKorean!=SimBarLocalization.IsKorean)
        {
            floorMissionUiKorean=SimBarLocalization.IsKorean;
            floorMissionFeedback="";
            foreach(Text label in floorMissionCanvas.GetComponentsInChildren<Text>(true)) label.font=BookingFont;
        }
        var state=FloorState;
        var reaction=state==null?null:state.reaction;
        int kind=reaction==null?SimBarFloorChallenges.None:reaction.activeKind;
        bool active=kind!=SimBarFloorChallenges.None;
        bool retry=reaction!=null && reaction.cooldown>0f;
        int remaining=SimBarFloorMissions.Remaining(state);
        string summary=active?SimBarLocalization.Text("RESPOND NOW / TAP QUICKLY","지금 대응 / 빠르게 연타")
            :retry?SimBarLocalization.Text("NEXT RESPONSE SHORTLY","잠시 뒤 다음 대응")
            :remaining>0?SimBarLocalization.Format("PAPER LEFT {0}","남은 정리량 {0}",remaining)
            :state!=null && state.guestActive?SimBarLocalization.Text("A GUEST NEEDS HELP","도움이 필요한 손님")
            :SimBarLocalization.Text("WATCH FOR RANDOM EVENTS","무작위 돌발 상황 대기");
        floorMissionHudButton.GetComponentInChildren<Text>().text=SimBarLocalization.Text("RANDOM FLOOR MISSIONS / H\n","돌발 홀 미션 / H\n")+summary;
        floorMissionHudButton.interactable=!paused || serviceMenuOpen || opMenuOpen;
        bool showReaction=!paused && (active || retry);
        floorReactionCard.gameObject.SetActive(showReaction);
        // Status never covers an urgent tap target and never pauses the simulation.
        floorMissionDrawer.gameObject.SetActive(floorMissionDrawerOpen && !paused && !showReaction);
        if(showReaction)
        {
            bool cleanup=kind==SimBarFloorChallenges.Cleanup;
            int target=cleanup?SimBarFloorChallenges.CleanupPresses:SimBarFloorChallenges.GuestPresses;
            int presses=reaction==null?0:Mathf.Clamp(reaction.presses,0,target);
            float seconds=active?Mathf.Max(0f,reaction.secondsLeft):Mathf.Max(0f,reaction.cooldown);
            floorReactionTitle.text=active
                ?cleanup?SimBarLocalization.Text("SUDDEN CLEANUP!","돌발 청소 미션!"):SimBarLocalization.Text("GUEST ASSISTANCE!","돌발 손님 안내!")
                :SimBarLocalization.Text("NEXT RESPONSE SHORTLY","다음 대응 준비");
            floorReactionClock.text=seconds.ToString("0.0")+SimBarLocalization.Text("s","초");
            floorReactionClock.color=active && seconds<=2f?new Color(1f,.39f,.31f):Color.white;
            floorReactionNarrative.text=!active
                ?SimBarLocalization.Text("Your response is being checked. Any unfinished task will receive another reaction chance.","대응 결과를 확인하고 있습니다. 끝내지 못한 미션은 잠시 뒤 다시 도전할 수 있습니다.")
                :cleanup?SimBarLocalization.Text("Paper covers the floor after the show. Tap rapidly to help your employees keep cleaning!","공연 뒤 바닥에 종이가 남았어요. 빠르게 연타해 직원들의 청소를 도와주세요!")
                :FloorGuestReactionNarrative(reaction.activeStage);
            floorReactionCount.text=active
                ?SimBarLocalization.Format("QUICK TAPS  {0} / {1}","빠르게 연타  {0} / {1}",presses,target)
                :SimBarLocalization.Text("WATCH FOR THE NEXT PROMPT","다음 안내가 뜨면 다시 도전하세요");
            floorReactionButton.interactable=active && !paused;
            floorReactionButton.GetComponentInChildren<Text>().text=!active
                ?SimBarLocalization.Text("RETRY SHORTLY","잠시 뒤 다시 대응")
                :cleanup?SimBarLocalization.Text("[C] TAP QUICKLY / CLEAN","[C] 빠르게 연타 / 청소")
                :SimBarLocalization.Text("[F] TAP QUICKLY / ASSIST","[F] 빠르게 연타 / 정중한 안내");
            BookingButtonColor(floorReactionButton,cleanup?FloorUiGreen:new Color(.34f,.26f,.13f),Color.white);
            floorReactionFeedback.text=string.IsNullOrEmpty(floorMissionFeedback)
                ?SimBarLocalization.Text("Tap the button repeatedly, or press the displayed key.","버튼을 여러 번 터치하거나 표시된 키를 반복해서 누르세요.")
                :floorMissionFeedback;
            BookingPlace(floorReactionFill,0f,0f,Mathf.Max(0f,floorReactionTrack.rect.width)*(active?presses/(float)target:0f),Mathf.Max(0f,floorReactionTrack.rect.height));
        }
        if(floorMissionDrawerOpen && !showReaction)
        {
            string cleaning=state==null || state.litter==0?SimBarLocalization.Text("No paper to clean yet.","현재 치울 종이가 없습니다.")
                :state.cleanupComplete?SimBarLocalization.Text("Floor cleanup complete.","바닥 청소 완료")
                :SimBarLocalization.Format("Paper left {0} / Cleanup {1}%\nStaff available {2} / Arrived and working {3}",
                    "남은 정리량 {0} / 청소 {1}%\n배치 가능한 직원 {2}명 / 현장 작업 {3}명",remaining,
                    Mathf.RoundToInt(SimBarFloorMissions.CleanupProgress(state)*100f),FloorAvailableWorkers,CleanupReadyWorkers);
            string guest=state==null || !state.guestSpawned?SimBarLocalization.Text("No assistance request yet.","아직 안내 요청이 없습니다.")
                :state.guestComplete?FloorGuestExitFinished?SimBarLocalization.Text("Courteous exit complete.","정중한 퇴점 안내 완료")
                    :SimBarLocalization.Text("An employee is accompanying the guest and returning.","직원이 손님과 동행한 뒤 복귀하고 있습니다.")
                :SimBarLocalization.Format("Courteous assistance: stage {0} / 3", "정중한 손님 안내 {0} / 3단계",state.guestStep+1);
            floorMissionDetail.text=SimBarLocalization.Format("RANDOM STAGE EFFECTS\nConfetti {0} / Tissue {1}\nPerformers choose the moment. Watch the floor after the show.\n\nEMPLOYEE CLEANUP\n{2}\n\nCOURTEOUS GUEST EXIT\n{3}",
                "무작위 공연 퍼포먼스\n꽃가루 {0}회 / 휴지 {1}회\n공연자들이 때를 정해 퍼포먼스를 합니다. 공연 후 바닥을 살펴보세요.\n\n직원 청소\n{2}\n\n정중한 손님 퇴점 안내\n{3}",
                state==null?0:state.confettiThrows,state==null?0:state.tissueThrows,cleaning,guest);
        }
    }

    private string FloorGuestReactionNarrative(int stage)
    {
        if(stage<=0) return SimBarLocalization.Text("A guest has had too much. Tap rapidly to pause additional orders and offer water politely.","술에 취한 손님이 있어요. 빠르게 연타해 추가 주문을 잠시 멈추고 물을 정중히 안내하세요.");
        if(stage==1) return SimBarLocalization.Text("Keep your voice calm. Tap rapidly to explain and politely ask the guest to leave.","차분하게 설명할 차례예요. 빠르게 연타해 손님께 정중하게 퇴점을 요청하세요.");
        return SimBarLocalization.Text("Check the guest's belongings. Tap rapidly to arrange an employee escort to the exit.","소지품을 확인할 차례예요. 빠르게 연타해 직원의 출입구 동행을 안내하세요.");
    }

    private void LayoutFloorMissionUI(Vector2 size)
    {
        if(floorReactionCard==null || size.x<=0f || size.y<=0f) return;
        bool compact=size.y<500f;
        bool veryShort=size.y<300f;
        float hudWidth=Mathf.Min(230f,size.x-24f);
        float hudY=compact?12f:112f;
        BookingPlace(floorMissionHudButton.GetComponent<RectTransform>(),size.x-hudWidth-12f,hudY,hudWidth,veryShort?36f:54f);
        floorMissionHudButton.GetComponentInChildren<Text>().fontSize=veryShort?11:13;
        float width=Mathf.Min(340f,size.x-24f);
        float top=veryShort?56f:compact?74f:180f;
        float height=Mathf.Min(compact?260f:300f,size.y-top-(veryShort?8f:12f));
        bool tight=height<245f;
        float inner=width-28f;
        BookingPlace(floorReactionCard,size.x-width-12f,top,width,height);
        BookingPlace(floorReactionTitle.rectTransform,14f,tight?8f:12f,inner-100f,tight?24f:29f);
        BookingPlace(floorReactionClock.rectTransform,width-108f,8f,94f,45f);
        floorReactionTitle.fontSize=tight?15:compact?16:18;
        floorReactionClock.fontSize=compact?22:24;
        floorReactionClock.resizeTextMaxSize=compact?22:24;
        float narrativeY=tight?42f:compact?45f:52f;
        float narrativeHeight=tight?32f:compact?60f:77f;
        BookingPlace(floorReactionNarrative.rectTransform,14f,narrativeY,inner,narrativeHeight);
        floorReactionNarrative.fontSize=tight?11:compact?13:14;
        float countY=tight?79f:compact?111f:139f;
        BookingPlace(floorReactionCount.rectTransform,14f,countY,inner,tight?19f:25f);
        floorReactionCount.fontSize=tight?14:16;
        BookingPlace(floorReactionTrack,14f,tight?104f:countY+29f,inner,tight?5f:7f);
        float buttonHeight=tight?46f:58f;
        float buttonY=tight?height-14f-buttonHeight:compact?157f:186f;
        BookingPlace(floorReactionButton.GetComponent<RectTransform>(),14f,buttonY,inner,buttonHeight);
        floorReactionButton.GetComponentInChildren<Text>().fontSize=tight || inner<290f?16:18;
        BookingPlace(floorReactionFeedback.rectTransform,14f,buttonY+66f,inner,Mathf.Max(26f,height-buttonY-73f));
        floorReactionFeedback.fontSize=compact?11:12;
        floorReactionFeedback.gameObject.SetActive(!tight);

        float detailWidth=Mathf.Min(380f,size.x-24f);
        float detailHeight=Mathf.Min(390f,size.y-top-(veryShort?8f:12f));
        float detailInner=detailWidth-28f;
        BookingPlace(floorMissionDrawer,size.x-detailWidth-12f,top,detailWidth,detailHeight);
        BookingPlace(floorMissionTitle.rectTransform,14f,12f,detailInner,30f);
        BookingPlace(floorMissionDetailNote.rectTransform,14f,49f,detailInner,47f);
        float viewHeight=Mathf.Max(1f,detailHeight-158f);
        BookingPlace(floorDetailViewport,14f,105f,detailInner,viewHeight);
        BookingPlace(floorDetailContent,0f,0f,detailInner,Mathf.Max(342f,viewHeight));
        BookingPlace(floorMissionDetail.rectTransform,0f,0f,detailInner,342f);
        BookingPlace(floorMissionCloseButton.GetComponent<RectTransform>(),14f,detailHeight-48f,detailInner,38f);
    }

    private void DestroyFloorMissionUI()
    {
        floorMissionDrawerOpen=false;
        if(floorMissionCanvas!=null) Destroy(floorMissionCanvas.gameObject);
        floorMissionCanvas=null; floorMissionLayout=null;
    }
}
