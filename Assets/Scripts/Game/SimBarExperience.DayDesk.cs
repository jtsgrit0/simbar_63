using UnityEngine;
using UnityEngine.UI;

/// <summary>The selected proprietor prepares one confirmed artist at a stationary office desk.</summary>
public sealed partial class SimBarExperience
{
    private bool UseDayDesk => true;
    private static readonly string[] DeskArtNames={"jessy","edison","jaiden"};
    private RectTransform deskScenePanel,deskWorkPanel,deskArtFrame,deskCaptionRect,deskTabsRect;
    private RawImage deskArtImage;
    private Image deskSpeechBackground,deskCursor;
    private SimBarPixelPolygonGraphic deskMonitorGlow,deskPhoneGlow,deskSpeechTail;
    private Text deskSceneCaption,deskPhysicalSpeech,deskMonitorText,deskPhoneText,deskConversation,deskComputerHint,deskPhoneHint,deskProgress;
    private Toggle deskFreewillToggle;
    private Button deskComputerWork,deskPhoneWork;
    private RectTransform deskMonitorHotspot,deskPhoneHotspot;
    private readonly Button[] deskChannels=new Button[4];
    private readonly RectTransform[] deskPanes=new RectTransform[3];
    private int deskManualChannel=-1,deskPresentedChannel=-1,deskArtOwner=-1;
    private float deskVisualClock,deskRetryClock,deskManualUntil;
    private bool deskOfficeReady;

    private void BuildDayDeskUI()
    {
        var host=new GameObject("Selected Owner Day Desk",typeof(RectTransform),typeof(Canvas),typeof(GraphicRaycaster));host.transform.SetParent(transform,false);
        bookingCanvas=host.GetComponent<Canvas>();bookingCanvas.renderMode=RenderMode.ScreenSpaceOverlay;bookingCanvas.sortingOrder=650;
        bookingRoot=BookingPanel("Day Desk Background",host.transform,BookingPaper);BookingStretch(bookingRoot);
        bookingHeader=BookingPanel("Day Desk Header",bookingRoot,BookingPaper);bookingFooter=BookingPanel("Day Desk Footer",bookingRoot,BookingCard);
        bookingTitle=BookingText("Day Desk Owner Heading",bookingHeader,"","",24,BookingInk,true);
        bookingClockLabel=BookingText("Day Desk Planning Hour",bookingHeader,"","",14,BookingGreen,true);
        bookingBack=BookingButton("Day Desk Save Lobby",bookingHeader,"SAVE / LOBBY","저장 / 로비",12,BookingLine,BookingInk);bookingBack.onClick.AddListener(BookingBackToLobby);
        bookingLanguage=BookingButton("Day Desk Language",bookingHeader,"한국어","ENGLISH",12,BookingCard,BookingInk);bookingLanguage.onClick.AddListener(()=>SimBarLocalization.SetLanguage(!SimBarLocalization.IsKorean));
        var viewport=BookingPanel("Day Desk Scroll Viewport",bookingRoot,new Color(1f,1f,1f,.001f));viewport.gameObject.AddComponent<RectMask2D>();
        bookingContent=BookingPanel("Day Desk Scroll Content",viewport,Color.clear);bookingContent.GetComponent<Image>().raycastTarget=false;
        bookingScroll=viewport.gameObject.AddComponent<ScrollRect>();bookingScroll.viewport=viewport;bookingScroll.content=bookingContent;bookingScroll.horizontal=false;bookingScroll.vertical=true;bookingScroll.movementType=ScrollRect.MovementType.Clamped;bookingScroll.scrollSensitivity=30f;
        deskScenePanel=BookingPanel("Selected Owner Office Scene",bookingContent,BookingPaper);
        deskArtFrame=new GameObject("Office Art 640x360",typeof(RectTransform)).GetComponent<RectTransform>();deskArtFrame.SetParent(deskScenePanel,false);
        deskArtImage=PixelImage("Selected Owner Office Art",deskArtFrame,null);BookingPlace(deskArtImage.rectTransform,0,0,640,360);
        deskMonitorGlow=DeskPolygon("Active Computer Screen",new[]{new Vector2(23,175),new Vector2(202,175),new Vector2(207,276),new Vector2(30,276)},new Color(.15f,.65f,.55f,.15f));
        deskPhoneGlow=DeskPolygon("Active Phone Screen",new[]{new Vector2(493,293),new Vector2(532,293),new Vector2(555,326),new Vector2(511,326)},new Color(.25f,.75f,.55f,.15f));
        deskMonitorText=BookingText("Physical Monitor Work",deskArtFrame,"","",13,Color.white,true);deskMonitorText.alignment=TextAnchor.MiddleCenter;BookingPlace(deskMonitorText.rectTransform,32,182,166,82);
        deskPhoneText=BookingText("Physical Phone Messages",deskArtFrame,"","",12,Color.white,true);deskPhoneText.alignment=TextAnchor.MiddleCenter;BookingPlace(deskPhoneText.rectTransform,505,296,34,22);
        deskCursor=PixelSolid("Physical Monitor Cursor",deskArtFrame);deskCursor.color=new Color(.65f,1f,.78f);BookingPlace(deskCursor.rectTransform,181,257,2,9);
        var monitor=BookingButton("Office Computer Hotspot",deskArtFrame,"","",1,Color.clear,Color.clear);deskMonitorHotspot=monitor.GetComponent<RectTransform>();BookingPlace(deskMonitorHotspot,30,176,170,99);monitor.onClick.AddListener(()=>SetDeskChannel(1));
        var phone=BookingButton("Office Phone Hotspot",deskArtFrame,"","",1,Color.clear,Color.clear);deskPhoneHotspot=phone.GetComponent<RectTransform>();BookingPlace(deskPhoneHotspot,485,285,81,59);phone.onClick.AddListener(()=>SetDeskChannel(2));
        var speech=BookingPanel("Owner Speech Bubble",deskArtFrame,new Color(1f,.98f,.90f));deskSpeechBackground=speech.GetComponent<Image>();deskSpeechBackground.raycastTarget=false;BookingPlace(speech,19,14,198,90);
        var outline=speech.gameObject.AddComponent<Outline>();outline.effectColor=new Color(.16f,.22f,.24f,.7f);outline.effectDistance=new Vector2(1f,-1f);
        deskSpeechTail=DeskPolygon("Owner Speech Bubble Tail",new[]{new Vector2(188,102),new Vector2(207,102),new Vector2(230,123)},new Color(1f,.98f,.90f));
        deskPhysicalSpeech=BookingText("Owner Speech",speech,"","",12,BookingInk,true);BookingPlace(deskPhysicalSpeech.rectTransform,9,7,180,76);
        deskSceneCaption=BookingText("Owner Office Caption",deskScenePanel,"","",12,BookingMuted);deskSceneCaption.alignment=TextAnchor.MiddleCenter;deskCaptionRect=deskSceneCaption.rectTransform;
        deskWorkPanel=BookingPanel("Day Desk Work Pane",bookingContent,BookingCard);deskTabsRect=BookingPanel("Work Channels",deskWorkPanel,Color.clear);deskTabsRect.GetComponent<Image>().raycastTarget=false;
        string[] en={"AUTO","DIALOGUE","COMPUTER","PHONE"},ko={"자동","대화","컴퓨터","휴대폰"};
        for(int i=0;i<4;i++){int mode=i-1;deskChannels[i]=BookingButton("Day Desk Channel "+en[i],deskTabsRect,en[i],ko[i],13,BookingLine,BookingInk);deskChannels[i].onClick.AddListener(()=>SetDeskChannel(mode));}
        for(int i=0;i<3;i++)deskPanes[i]=BookingPanel("Day Desk "+(i==0?"Dialogue":i==1?"Computer":"Phone")+" Pane",deskWorkPanel,Color.clear);
        deskConversation=BookingText("Owner Work Conversation",deskPanes[0],"","",19,BookingInk,true);
        var row=BookingPanel("Freewill Checkbox Roster",deskPanes[1],BookingPaleGreen);deskFreewillToggle=row.gameObject.AddComponent<Toggle>();deskFreewillToggle.targetGraphic=row.GetComponent<Image>();
        var box=BookingPanel("Freewill Checkbox",row,BookingCard);BookingPlace(box,12,12,26,26);box.GetComponent<Image>().raycastTarget=false;var border=box.gameObject.AddComponent<Outline>();border.effectColor=BookingGreen;border.effectDistance=new Vector2(1,-1);
        var check=new GameObject("Freewill Checkmark",typeof(RectTransform),typeof(CanvasRenderer),typeof(SimBarPixelPolygonGraphic)).GetComponent<SimBarPixelPolygonGraphic>();check.transform.SetParent(box,false);check.Initialize(null,new[]{new Vector2(3,12),new Vector2(9,18),new Vector2(21,5),new Vector2(24,8),new Vector2(9,24),new Vector2(0,15)});check.color=BookingGreen;deskFreewillToggle.graphic=check;
        var label=BookingText("Only Available Band Freewill",row,"Freewill","프리윌",20,BookingInk,true);BookingPlace(label.rectTransform,50,8,220,36);
        deskFreewillToggle.onValueChanged.AddListener(BookingDeskSelect);
        deskComputerHint=BookingText("Computer Plan Instructions",deskPanes[1],"","",15,BookingMuted);
        deskComputerWork=BookingButton("Save Freewill Computer Plan",deskPanes[1],"SAVE SHOW PLAN","컴퓨터로 공연 계획 저장",16,BookingGreen,Color.white);deskComputerWork.onClick.AddListener(()=>BookingDeskWork(0));
        deskPhoneHint=BookingText("Phone Confirmation Instructions",deskPanes[2],"","",17,BookingInk,true);
        deskPhoneWork=BookingButton("Confirm Freewill By Phone",deskPanes[2],"CONFIRM FREEWILL","휴대폰으로 출연 확인",16,BookingGreen,Color.white);deskPhoneWork.onClick.AddListener(()=>BookingDeskWork(1));
        deskProgress=BookingText("Day Desk Progress",bookingFooter,"","",13,BookingGreen,true);
        bookingFeedbackLabel=BookingText("Day Desk Feedback",bookingFooter,"","",12,BookingMuted);
        bookingWalletLabel=BookingText("Day Desk Wallet",bookingFooter,"","",12,BookingMuted);
        bookingLaunchButton=BookingButton("Open Prepared Freewill Night",bookingFooter,"OPEN TONIGHT'S SHOW","오늘 밤 공연 시작",18,BookingGreen,Color.white);bookingLaunchButton.onClick.AddListener(BookingLaunchNight);
        if(canvas!=null){bookingLiveText=BookingText("Tonight Live Lineup",canvas.transform,"","",15,new Color(.93f,.88f,.67f),true);SetRect(bookingLiveText.rectTransform,new Vector2(.5f,1f),new Vector2(0,-141f),new Vector2(700,38));bookingLiveText.alignment=TextAnchor.MiddleCenter;}
        bookingUiKorean=SimBarLocalization.IsKorean;bookingLayout=SimBarBookingLayout.Attach(bookingCanvas,LayoutBookingUI);RefreshDayDeskUI();
    }

    private SimBarPixelPolygonGraphic DeskPolygon(string name,Vector2[] points,Color color)
    {
        var graphic=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(SimBarPixelPolygonGraphic)).GetComponent<SimBarPixelPolygonGraphic>();graphic.transform.SetParent(deskArtFrame,false);graphic.Initialize(null,points);graphic.color=color;return graphic;
    }
    private void RefreshDayDeskUI()
    {
        if(bookingCanvas==null)return;
        bool visible=JourneyIsPlanning&&!JourneyValidationMode&&SimBarBooking.IsDesk(DayPlan);
        if(bookingCanvas.gameObject.activeSelf!=visible)bookingCanvas.gameObject.SetActive(visible);
        if(bookingLiveText!=null){bookingLiveText.gameObject.SetActive(JourneyIsPlaying&&!JourneyValidationMode);bookingLiveText.text=BookingLiveLabel;}
        if(!visible){bookingUiWasPlanning=false;return;}
        if(!bookingUiWasPlanning){bookingUiWasPlanning=true;deskVisualClock=0f;deskManualChannel=-1;deskPresentedChannel=-1;bookingLayout.RefreshLayout();bookingScroll.verticalNormalizedPosition=1f;}
        if(bookingUiKorean!=SimBarLocalization.IsKorean){bookingUiKorean=SimBarLocalization.IsKorean;foreach(Text text in bookingCanvas.GetComponentsInChildren<Text>(true))text.font=BookingFont;}
        LoadSelectedDeskArt();
        bookingTitle.text=SimBarLocalization.Format("DAY {0} / {1}'S DESK","{0}일차 / {1}의 업무 데스크",journey.day,SelectedOwnerName);
        bookingClockLabel.text=SimBarBooking.Hour(DayPlan).ToString("00")+":00";
        deskSceneCaption.text=SimBarLocalization.Format("{0} prepares tonight's live show.","{0} 사장이 오늘 밤 공연을 준비해요.",SelectedOwnerName);
        deskFreewillToggle.SetIsOnWithoutNotify(DayPlan.freewillSelected);deskFreewillToggle.interactable=!DayPlan.locked&&!DayPlan.completed;
        bool planned=(DayPlan.deskDoneMask&1)!=0,confirmed=(DayPlan.deskDoneMask&2)!=0;
        deskComputerHint.text=planned?SimBarLocalization.Text("The show plan is saved. Confirm the artist by phone.","공연 계획을 저장했어요. 휴대폰으로 출연을 확인해요."):SimBarLocalization.Text("Check Freewill, then prepare the show on the computer.","프리윌을 체크하고 컴퓨터로 공연 계획을 준비해요.");
        deskComputerWork.interactable=DayPlan.freewillSelected&&!planned&&!DayPlan.locked;
        deskPhoneHint.text=confirmed?SimBarLocalization.Text("Freewill is confirmed. We are ready for tonight!","프리윌 출연 확인 완료! 오늘 밤 준비가 끝났어요!"):planned?SimBarLocalization.Text("The computer plan is ready. Call Freewill to confirm.","컴퓨터 계획이 준비됐어요. 프리윌에게 전화로 확인해요."):SimBarLocalization.Text("Save the computer plan before confirming the call.","컴퓨터 공연 계획을 먼저 저장하고 전화로 확인해요.");
        deskPhoneWork.interactable=DayPlan.freewillSelected&&planned&&!confirmed&&!DayPlan.locked;
        deskProgress.text=SimBarLocalization.Format("{0} FREEWILL  /  {1} COMPUTER  /  {2} PHONE","{0} 프리윌 선택  /  {1} 컴퓨터 계획  /  {2} 전화 확인",DayPlan.freewillSelected?"✓":"○",planned?"✓":"○",confirmed?"✓":"○");
        bookingFeedbackLabel.text=bookingFeedback??"";bookingWalletLabel.text=SimBarMoney.Format(cash);
        bookingLaunchButton.interactable=SimBarBooking.CanLaunchDesk(DayPlan);
        deskPresentedChannel=-1;TickDayDeskPresentation(0f);
    }
    private void LoadSelectedDeskArt()
    {
        int owner=Mathf.Clamp(selectedOwner,0,2);
        if(deskArtOwner==owner&&deskOfficeReady)return;
        Texture2D texture=LoadPixelTexture("SimBarOffice/desk-"+DeskArtNames[owner]);deskOfficeReady=texture!=null;
        if(texture==null)texture=SimBarWebTextures.Load("SimBarArt/Owners/"+OwnerPortraits[owner]);
        deskArtOwner=owner;deskArtImage.texture=texture;if(texture!=null)texture.filterMode=FilterMode.Point;
        // The V2 Jessy illustration has its own lower monitor and phone screens.
        // Keep the other owners' approved screen calibration when switching back.
        bool jessy=owner==0;
        deskMonitorGlow.Initialize(null,jessy?new[]{new Vector2(23,188),new Vector2(202,188),new Vector2(207,287),new Vector2(30,287)}:new[]{new Vector2(23,175),new Vector2(202,175),new Vector2(207,276),new Vector2(30,276)});
        deskPhoneGlow.Initialize(null,jessy?new[]{new Vector2(493,301),new Vector2(532,301),new Vector2(555,342),new Vector2(511,342)}:new[]{new Vector2(493,293),new Vector2(532,293),new Vector2(555,326),new Vector2(511,326)});
        BookingPlace(deskMonitorText.rectTransform,32,jessy?194:182,166,82);
        BookingPlace(deskPhoneText.rectTransform,505,jessy?307:296,34,22);
        BookingPlace(deskCursor.rectTransform,181,jessy?269:257,2,9);
        BookingPlace(deskMonitorHotspot,30,jessy?188:176,170,jessy?96:99);
        BookingPlace(deskPhoneHotspot,485,jessy?297:285,81,jessy?57:59);
        deskMonitorGlow.gameObject.SetActive(deskOfficeReady);deskPhoneGlow.gameObject.SetActive(deskOfficeReady);deskMonitorText.gameObject.SetActive(deskOfficeReady);deskPhoneText.gameObject.SetActive(deskOfficeReady);deskCursor.gameObject.SetActive(deskOfficeReady);
        if(bookingUiSize.x>0f)LayoutDayDeskUI(bookingUiSize);
    }
    private void SetDeskChannel(int channel)
    {
        if(channel< -1||channel>2||!JourneyIsPlanning)return;deskManualChannel=channel;deskManualUntil=deskVisualClock+7f;deskPresentedChannel=-1;TickDayDeskPresentation(0f);
    }
    private string DeskSpeech()
    {
        if(DayPlan==null||!DayPlan.freewillSelected)return SimBarLocalization.Text("Let's choose Freewill for tonight!","오늘의 밴드, 프리윌을 체크해요!");
        if(DayPlan.deskDoneMask==3)return SimBarLocalization.Text("All confirmed. Let's open tonight's show!","확인 완료! 오늘 밤 공연을 열어요!");
        if((DayPlan.deskDoneMask&1)!=0)return SimBarLocalization.Text("The plan is ready. Time to call Freewill.","공연 계획 완료! 전화로 확인해요.");
        return SimBarLocalization.Text("I'll prepare the show on the computer.","컴퓨터로 오늘 공연을 준비해요.");
    }
    private void TickDayDeskPresentation(float delta)
    {
        if(bookingCanvas==null||!bookingCanvas.gameObject.activeInHierarchy||!JourneyIsPlanning||!SimBarBooking.IsDesk(DayPlan))return;
        deskVisualClock+=Mathf.Clamp(delta,0f,.1f);deskRetryClock+=Mathf.Clamp(delta,0f,.1f);
        if(deskManualChannel>=0&&deskVisualClock>=deskManualUntil)deskManualChannel=-1;
        if(!deskOfficeReady&&deskRetryClock>1f){deskRetryClock=0f;LoadSelectedDeskArt();}
        int channel=deskManualChannel>=0?deskManualChannel:Mathf.FloorToInt(deskVisualClock/4.2f)%3;
        if(channel!=deskPresentedChannel)
        {
            deskPresentedChannel=channel;for(int i=0;i<3;i++)deskPanes[i].gameObject.SetActive(i==channel);
            for(int i=0;i<4;i++){bool chosen=i==0?deskManualChannel<0:deskManualChannel==i-1;BookingButtonColor(deskChannels[i],chosen?BookingGreen:BookingLine,chosen?Color.white:BookingInk);}
        }
        string speech=DeskSpeech();deskConversation.text=SelectedOwnerName+"\n\n"+speech;deskPhysicalSpeech.text=speech;
        deskSpeechBackground.gameObject.SetActive(channel==0);deskSpeechTail.gameObject.SetActive(channel==0);
        deskMonitorText.gameObject.SetActive(deskOfficeReady&&channel==1);deskPhoneText.gameObject.SetActive(deskOfficeReady&&channel==2);
        float pulse=.5f+.5f*Mathf.Sin(deskVisualClock*3f);
        deskMonitorGlow.color=new Color(.15f,.65f,.55f,channel==1?.12f+pulse*.07f:.025f);
        deskPhoneGlow.color=new Color(.25f,.75f,.55f,channel==2?.15f+pulse*.10f:.025f);
        deskSpeechBackground.color=channel==0?new Color(1f,.98f,.90f):new Color(.94f,.93f,.86f);
        deskMonitorText.text=DayPlan.freewillSelected?"FREEWILL\n"+SimBarLocalization.Text((DayPlan.deskDoneMask&1)!=0?"PLAN SAVED":"SHOW PLAN",(DayPlan.deskDoneMask&1)!=0?"계획 저장 완료":"공연 계획"):SimBarLocalization.Text("SHOW PLAN","공연 계획");
        deskPhoneText.text=(DayPlan.deskDoneMask&2)!=0?"OK":channel==2?"•••":"";
        deskCursor.gameObject.SetActive(deskOfficeReady&&channel==1&&Mathf.FloorToInt(deskVisualClock*2f)%2==0);
    }

    private void LayoutDayDeskUI(Vector2 size)
    {
        if(deskScenePanel==null||size.x<=0f)return;bookingUiSize=size;
        bool landscape=size.x>=700f||size.y<500f;bool shortView=size.y<500f;float margin=12f,gap=12f,header=shortView?68f:90f,footer=shortView?86f:104f;
        float width=size.x-margin*2f,body=Mathf.Max(120f,size.y-header-footer);
        bool narrow=size.x<700f&&!shortView;
        BookingPlace(bookingHeader,0,0,size.x,header);BookingPlace(bookingTitle.rectTransform,margin,10,width-116,narrow?44:32);bookingTitle.fontSize=shortView||narrow?18:22;
        BookingPlace(bookingClockLabel.rectTransform,margin,narrow?60:44,width-116,24);BookingPlace(bookingBack.GetComponent<RectTransform>(),size.x-112,10,100,30);BookingPlace(bookingLanguage.GetComponent<RectTransform>(),size.x-112,44,100,26);
        BookingPlace(bookingScroll.viewport,margin,header,width,body);
        float workWidth=landscape?Mathf.Clamp(width*.36f,250f,350f):width;
        float sceneWidth=landscape?width-workWidth-gap:width;
        float sceneHeight=landscape?body:sceneWidth*360f/640f+40f;
        float workHeight=landscape?Mathf.Max(body,236f):290f;
        float contentHeight=landscape?Mathf.Max(sceneHeight,workHeight):sceneHeight+gap+workHeight;
        BookingPlace(bookingContent,0,0,width,Mathf.Max(body,contentHeight));BookingPlace(deskScenePanel,0,0,sceneWidth,sceneHeight);BookingPlace(deskWorkPanel,landscape?sceneWidth+gap:0,landscape?0:sceneHeight+gap,workWidth,workHeight);
        Texture texture=deskArtImage.texture;float artW=deskOfficeReady||texture==null?640f:texture.width,artH=deskOfficeReady||texture==null?360f:texture.height;
        float caption=32f,scale=Mathf.Min(sceneWidth/artW,Mathf.Max(1f,sceneHeight-caption)/artH);
        BookingPlace(deskArtFrame,(sceneWidth-artW*scale)*.5f,Mathf.Max(0f,(sceneHeight-caption-artH*scale)*.5f),artW,artH);deskArtFrame.localScale=Vector3.one*scale;BookingPlace(deskArtImage.rectTransform,0,0,artW,artH);
        deskPhysicalSpeech.fontSize=Mathf.Clamp(Mathf.CeilToInt(11f/scale),12,22);
        BookingPlace(deskCaptionRect,4f,sceneHeight-caption+2f,sceneWidth-8f,caption-2f);
        BookingPlace(deskTabsRect,8,8,workWidth-16,34);float tab=(workWidth-22f)/4f;
        for(int i=0;i<4;i++)BookingPlace(deskChannels[i].GetComponent<RectTransform>(),i*(tab+2f),0,tab,34);
        float inner=workWidth-28f,paneHeight=workHeight-58f;
        for(int i=0;i<3;i++)BookingPlace(deskPanes[i],14,52,inner,paneHeight);
        BookingPlace(deskConversation.rectTransform,0,8,inner,paneHeight-12f);deskConversation.fontSize=shortView?16:19;
        BookingPlace(deskFreewillToggle.GetComponent<RectTransform>(),0,8,inner,52);
        BookingPlace(deskComputerHint.rectTransform,0,72,inner,Mathf.Max(42f,paneHeight-144f));deskComputerHint.fontSize=shortView?13:15;
        BookingPlace(deskComputerWork.GetComponent<RectTransform>(),0,paneHeight-56,inner,48);
        BookingPlace(deskPhoneHint.rectTransform,0,10,inner,Mathf.Max(54f,paneHeight-80f));deskPhoneHint.fontSize=shortView?14:17;
        BookingPlace(deskPhoneWork.GetComponent<RectTransform>(),0,paneHeight-56,inner,48);
        BookingPlace(bookingFooter,0,size.y-footer,size.x,footer);BookingPlace(deskProgress.rectTransform,margin,5,width,24);deskProgress.fontSize=shortView?11:13;
        BookingPlace(bookingFeedbackLabel.rectTransform,margin,31,Mathf.Max(1f,width-210f),28);BookingPlace(bookingWalletLabel.rectTransform,margin,62,width-210f,22);
        BookingPlace(bookingLaunchButton.GetComponent<RectTransform>(),size.x-212,35,200,48);bookingLaunchButton.GetComponentInChildren<Text>().fontSize=shortView?14:17;
    }
}
