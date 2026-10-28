using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public sealed partial class SimBarExperience
{
    private bool bookingShowingPosters;
    private int scoutingPosterIndex, scoutingRenderedIndex = -1;
    private Button bookingPosterTab, scoutingAction, scoutingPrevious, scoutingNext, scoutingSource, scoutingInstagram;
    private readonly Button[] scoutingVenueFilters = new Button[4];
    private readonly List<Button> scoutingArtistButtons = new List<Button>();
    private RectTransform scoutingBoard, scoutingImageFrame, scoutingInfo;
    private RawImage scoutingImage;
    private Text scoutingHeading, scoutingIntro, scoutingTitle, scoutingProvenance, scoutingImageStatus, scoutingHint, scoutingZoomHint;
    private readonly Dictionary<string, Texture2D> scoutingTextures = new Dictionary<string, Texture2D>();
    private UnityWebRequest scoutingRequest;
    private string scoutingRequestedImage;

    private void BuildPosterScoutingUI()
    {
        bookingPosterTab = BookingButton("Poster discovery tab", bookingHeader, "POSTERS", "포스터 발굴", 14, BookingLine, BookingInk);
        bookingPosterTab.onClick.AddListener(() => BookingShowPosters(true));
        scoutingBoard = BookingPanel("Real performance poster scouting", bookingContent, BookingCard);
        scoutingHeading = BookingText("Scouting heading", scoutingBoard, "FIND YOUR NEXT LINEUP", "포스터에서 다음 라인업을 발굴하세요", 23, BookingInk, true);
        scoutingIntro = BookingText("Scouting instructions", scoutingBoard,
            "Browse real posters for free. Research a lineup in one two-hour block, then review a demo or make an offer.",
            "포스터 열람은 무료입니다. 2시간 조사로 출연진을 발굴하고, 데모 검토와 섭외 제안으로 이어가세요.", 14, BookingMuted);
        for (int i=0; i<4; i++)
        {
            int venue=i;
            scoutingVenueFilters[i]=BookingButton("Poster venue filter " + i, scoutingBoard, "", "", 13, BookingLine, BookingInk);
            scoutingVenueFilters[i].onClick.AddListener(() => SelectScoutingVenue(venue));
        }
        scoutingImageFrame=BookingPanel("Complete unaltered poster image", scoutingBoard, BookingPaper);
        var zoom=scoutingImageFrame.gameObject.AddComponent<Button>();
        zoom.targetGraphic=scoutingImageFrame.GetComponent<Image>();
        zoom.onClick.AddListener(() => Application.OpenURL(ScoutingImageUrl(SimBarPosterCatalog.Entries[scoutingPosterIndex].imagePath)));
        scoutingZoomHint=BookingText("Poster original size hint",scoutingBoard,"TAP POSTER TO OPEN FULL SIZE >","포스터를 누르면 원본 크기로 열립니다 >",12,BookingGreen);
        var imageObject=new GameObject("On demand poster image",typeof(RectTransform),typeof(CanvasRenderer),typeof(RawImage));
        imageObject.transform.SetParent(scoutingImageFrame,false);
        scoutingImage=imageObject.GetComponent<RawImage>(); scoutingImage.raycastTarget=false;
        scoutingImageStatus=BookingText("Poster image loading state",scoutingImageFrame,"","",14,BookingMuted);
        scoutingInfo=BookingPanel("Poster lineup and discovery",scoutingBoard,BookingCard);
        scoutingTitle=BookingText("Poster event title",scoutingInfo,"","",22,BookingInk,true);
        scoutingProvenance=BookingText("Poster origin and evidence",scoutingInfo,"","",13,BookingMuted);
        scoutingAction=BookingButton("Research real poster lineup",scoutingInfo,"","",17,BookingGreen,Color.white);
        scoutingAction.onClick.AddListener(BookingScoutPoster);
        scoutingHint=BookingText("Simulation scope",scoutingInfo,
            "Discovery stays with your career. All genres can play every venue. Artist fees, acceptance and growth are game values; no real booking is sent.",
            "발굴한 팀은 다음 날에도 유지됩니다. 모든 장르를 어느 매장에나 제안할 수 있습니다. 출연료·수락·성장은 게임 설정이며 실제 연락을 보내지 않습니다.",13,BookingMuted);
        scoutingSource=BookingButton("Open inspected poster source",scoutingInfo,"POSTER SOURCE", "포스터 출처",13,BookingLine,BookingInk);
        scoutingSource.onClick.AddListener(() => Application.OpenURL(SimBarPosterCatalog.Entries[scoutingPosterIndex].sourceUrl));
        scoutingInstagram=BookingButton("Open venue Instagram",scoutingInfo,"VENUE INSTAGRAM", "공연장 인스타그램",13,BookingLine,BookingInk);
        scoutingInstagram.onClick.AddListener(() => Application.OpenURL(SimBarPosterCatalog.Entries[scoutingPosterIndex].instagramUrl));
        scoutingPrevious=BookingButton("Previous inspected poster",scoutingBoard,"< PREVIOUS", "< 이전 포스터",14,BookingLine,BookingInk);
        scoutingNext=BookingButton("Next inspected poster",scoutingBoard,"NEXT >", "다음 포스터 >",14,BookingLine,BookingInk);
        scoutingPrevious.onClick.AddListener(() => SelectScoutingPoster(scoutingPosterIndex-1));
        scoutingNext.onClick.AddListener(() => SelectScoutingPoster(scoutingPosterIndex+1));
    }

    private void BookingShowPosters(bool show)
    {
        bookingShowingPosters=show;
        if (show) bookingShowingArchive=false;
        bookingVenueMenuOpen=false;
        if (bookingVenueMenu!=null) bookingVenueMenu.gameObject.SetActive(false);
        RefreshBookingUI();
        LayoutBookingUI(bookingUiSize);
        bookingScroll.StopMovement(); bookingScroll.verticalNormalizedPosition=1f;
    }

    private void SelectScoutingVenue(int venue)
    {
        for(int i=0;i<SimBarPosterCatalog.Entries.Length;i++)
            if(SimBarPosterCatalog.Entries[i].venueId==SimBarBooking.Venues4[venue].id) { SelectScoutingPoster(i); return; }
    }

    private void SelectScoutingPoster(int index)
    {
        if(SimBarPosterCatalog.Entries.Length==0) return;
        scoutingPosterIndex=(index%SimBarPosterCatalog.Entries.Length+SimBarPosterCatalog.Entries.Length)%SimBarPosterCatalog.Entries.Length;
        if(scoutingRequest!=null) scoutingRequest.Abort();
        scoutingRequestedImage=null;
        RefreshBookingUI();
        LayoutBookingUI(bookingUiSize);
        bookingScroll.StopMovement(); bookingScroll.verticalNormalizedPosition=1f;
    }

    private void BookingScoutPoster()
    {
        if(!JourneyIsPlanning) return;
        BookingApply(SimBarBooking.Scout(DayPlan,BookingCareer,scoutingPosterIndex));
        RefreshPosterScoutingUI();
    }

    private void BookingPrimaryAction()
    {
        if(!bookingShowingPosters) { BookingLaunchNight(); return; }
        if(SimBarPosterCatalog.Entries.Length==0) return;
        if(!SimBarBooking.IsPosterDiscovered(BookingCareer,scoutingPosterIndex)) { BookingScoutPoster(); return; }
        bookingSelectedBand=SimBarPosterCatalog.Entries[scoutingPosterIndex].artistIndices[0];
        BookingShowPosters(false);
    }

    private void RefreshPosterScoutingUI()
    {
        if(scoutingBoard==null) return;
        scoutingBoard.gameObject.SetActive(bookingShowingPosters);
        BookingButtonColor(bookingPosterTab,bookingShowingPosters?BookingGreen:BookingLine,bookingShowingPosters?Color.white:BookingInk);
        if(!bookingShowingPosters || !JourneyIsPlanning || SimBarPosterCatalog.Entries.Length==0) return;
        scoutingPosterIndex=Mathf.Clamp(scoutingPosterIndex,0,SimBarPosterCatalog.Entries.Length-1);
        var entry=SimBarPosterCatalog.Entries[scoutingPosterIndex];
        bool korean=SimBarLocalization.IsKorean;
        for(int i=0;i<4;i++)
        {
            var venue=SimBarBooking.Venues4[i];
            scoutingVenueFilters[i].GetComponentInChildren<Text>().text=korean?venue.name:venue.nameEnglish;
            BookingButtonColor(scoutingVenueFilters[i],entry.venueId==venue.id?BookingPaleGreen:BookingLine,BookingInk);
        }
        scoutingTitle.text=entry.title + "\n" + entry.date;
        string englishEvidence=entry.instagramUrl.Contains("/p/")?"Public poster mirror / original Instagram link verified.":"Public poster mirror / individual Instagram post unverified.";
        scoutingProvenance.text=entry.handle + SimBarLocalization.Text(" / checked "," / 이미지 확인 ")+SimBarPosterCatalog.CheckedOn+"\n"+(korean?entry.evidence:englishEvidence);
        if(scoutingRenderedIndex!=scoutingPosterIndex)
        {
            scoutingRenderedIndex=scoutingPosterIndex;
            foreach(var button in scoutingArtistButtons) if(button!=null) Destroy(button.gameObject);
            scoutingArtistButtons.Clear();
            foreach(int artist in entry.artistIndices)
            {
                int band=artist;
                var button=BookingButton("Poster artist " + band,scoutingInfo,"","",16,BookingPaper,BookingInk);
                button.onClick.AddListener(() =>
                {
                    if(!SimBarBooking.IsDiscovered(BookingCareer,band))
                    {
                        bookingFeedback=SimBarLocalization.Text("Research this poster first to unlock the band.","먼저 이 포스터를 조사하면 섭외 후보로 열립니다.");
                        RefreshBookingUI(); return;
                    }
                    bookingSelectedBand=band;
                    BookingShowPosters(false);
                });
                scoutingArtistButtons.Add(button);
            }
            LayoutBookingUI(bookingUiSize);
        }
        for(int i=0;i<scoutingArtistButtons.Count;i++)
        {
            int band=entry.artistIndices[i]; var artist=SimBarBooking.Bands12[band];
            bool known=SimBarBooking.IsDiscovered(BookingCareer,band);
            scoutingArtistButtons[i].GetComponentInChildren<Text>().text=(korean?artist.name:artist.nameEnglish)+"\n"+
                (known?SimBarLocalization.Text("OPEN BOOKING DETAILS >","섭외 상세 보기 >"):SimBarLocalization.Text("LOCKED / RESEARCH POSTER","미발굴 / 포스터 조사 필요"));
            BookingButtonColor(scoutingArtistButtons[i],known?BookingPaleGreen:BookingPaper,BookingInk);
        }
        bool discovered=SimBarBooking.IsPosterDiscovered(BookingCareer,scoutingPosterIndex);
        scoutingAction.interactable=!discovered && SimBarBooking.CanAct(DayPlan);
        scoutingAction.GetComponentInChildren<Text>().text=discovered?SimBarLocalization.Text("LINEUP DISCOVERED", "출연진 발굴 완료"):
            SimBarLocalization.Format("RESEARCH POSTER / 2H / {0} ACTS", "포스터 조사 / 2시간 / {0}팀 발굴",entry.artistIndices.Length);
        if(scoutingTextures.TryGetValue(entry.imagePath,out var texture))
        {
            scoutingRequestedImage=entry.imagePath;
            scoutingImage.texture=texture; scoutingImage.color=Color.white;
            scoutingImageStatus.text="";
            FitScoutingImage();
        }
        else if(scoutingRequestedImage!=entry.imagePath)
        {
            scoutingImage.texture=null; scoutingImage.color=Color.clear;
            scoutingImageStatus.text=SimBarLocalization.Text("Loading the full poster...", "포스터 이미지를 불러오는 중...");
            if(scoutingRequest!=null) scoutingRequest.Abort();
            scoutingRequestedImage=entry.imagePath;
            StartCoroutine(LoadScoutingPoster(entry.imagePath));
        }
    }

    private IEnumerator LoadScoutingPoster(string path)
    {
        string url=ScoutingImageUrl(path);
        using(var request=UnityWebRequestTexture.GetTexture(url,true))
        {
            scoutingRequest=request; request.timeout=20;
            yield return request.SendWebRequest();
            bool latest=scoutingRequest==request;
            if(latest) scoutingRequest=null;
            if(request.result==UnityWebRequest.Result.Success)
            {
                Texture2D texture=DownloadHandlerTexture.GetContent(request);
                texture.name="Inspected performance poster";
                scoutingTextures[path]=texture;
                if(scoutingRequestedImage==path) RefreshPosterScoutingUI();
            }
            else if(latest && scoutingRequestedImage==path && scoutingImageStatus!=null)
                scoutingImageStatus.text=SimBarLocalization.Text("Image unavailable. Open the credited poster source below.","이미지를 불러오지 못했습니다. 아래 포스터 출처에서 확인할 수 있습니다.");
        }
    }

    private static string ScoutingImageUrl(string path)
    {
        string url=Application.streamingAssetsPath.TrimEnd('/')+"/"+path;
        return url.Contains("://")?url:"file:///"+url.Replace('\\','/');
    }

    private float LayoutPosterScoutingUI(float width,bool wide)
    {
        if(scoutingBoard==null) return 0f;
        float inner=width-32f;
        BookingPlace(scoutingHeading.rectTransform,16,12,inner,wide?40:72); scoutingHeading.fontSize=wide?23:21;
        BookingPlace(scoutingIntro.rectTransform,16,wide?60:86,inner,wide?48:74);
        float filtersY=wide?118:170;
        float filterWidth=(inner-18f)/(wide?4:2);
        for(int i=0;i<4;i++) BookingPlace(scoutingVenueFilters[i].GetComponent<RectTransform>(),16+(wide?i:i%2)*(filterWidth+6),filtersY+(wide?0:i/2*44),filterWidth,38);
        float top=filtersY+(wide?54:100);
        float imageWidth=wide?Mathf.Min(470f,inner*0.44f):inner;
        float imageHeight=wide?600f:Mathf.Min(620f,imageWidth*1.42f);
        BookingPlace(scoutingImageFrame,16,top,imageWidth,imageHeight);
        BookingPlace(scoutingZoomHint.rectTransform,16,top+imageHeight+5,imageWidth,28);
        BookingPlace(scoutingImageStatus.rectTransform,12,20,imageWidth-24,100);
        float infoWidth=wide?inner-imageWidth-20:inner;
        float infoTop=wide?top:top+imageHeight+44;
        BookingPlace(scoutingInfo,wide?36+imageWidth:16,infoTop,infoWidth,650);
        BookingPlace(scoutingTitle.rectTransform,0,0,infoWidth,96);
        BookingPlace(scoutingProvenance.rectTransform,0,100,infoWidth,84);
        float rowsY=192;
        for(int i=0;i<scoutingArtistButtons.Count;i++) BookingPlace(scoutingArtistButtons[i].GetComponent<RectTransform>(),0,rowsY+i*66,infoWidth,60);
        float actionY=rowsY+scoutingArtistButtons.Count*66+10;
        BookingPlace(scoutingAction.GetComponent<RectTransform>(),0,actionY,infoWidth,60);
        scoutingAction.GetComponentInChildren<Text>().fontSize=infoWidth<390?15:17;
        BookingPlace(scoutingHint.rectTransform,0,actionY+72,infoWidth,110);
        float half=(infoWidth-8)/2;
        BookingPlace(scoutingSource.GetComponent<RectTransform>(),0,actionY+194,half,46);
        BookingPlace(scoutingInstagram.GetComponent<RectTransform>(),half+8,actionY+194,half,46);
        float infoHeight=actionY+248;
        scoutingInfo.sizeDelta=new Vector2(infoWidth,infoHeight);
        float bottom=wide?Mathf.Max(top+imageHeight+34,infoTop+infoHeight):infoTop+infoHeight;
        BookingPlace(scoutingPrevious.GetComponent<RectTransform>(),16,bottom+16,(inner-10)/2,44);
        BookingPlace(scoutingNext.GetComponent<RectTransform>(),26+(inner-10)/2,bottom+16,(inner-10)/2,44);
        float height=bottom+78;
        BookingPlace(scoutingBoard,0,0,width,height);
        FitScoutingImage();
        return height;
    }

    private void FitScoutingImage()
    {
        if(scoutingImage==null || scoutingImage.texture==null) return;
        float availableWidth=scoutingImageFrame.rect.width-16,availableHeight=scoutingImageFrame.rect.height-16;
        float scale=Mathf.Min(availableWidth/scoutingImage.texture.width,availableHeight/scoutingImage.texture.height);
        float width=scoutingImage.texture.width*scale,height=scoutingImage.texture.height*scale;
        BookingPlace(scoutingImage.rectTransform,(scoutingImageFrame.rect.width-width)/2,(scoutingImageFrame.rect.height-height)/2,width,height);
    }

    private void DestroyPosterScouting()
    {
        if(scoutingRequest!=null) { scoutingRequest.Abort(); scoutingRequest.Dispose(); scoutingRequest=null; }
        foreach(var texture in scoutingTextures.Values) if(texture!=null) Destroy(texture);
        scoutingTextures.Clear();
    }
}
