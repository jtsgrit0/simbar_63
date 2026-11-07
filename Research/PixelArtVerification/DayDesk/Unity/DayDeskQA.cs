using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public static class DayDeskQA
{
    const string Dir="Research/PixelArtVerification/DayDesk/Unity/";
    const BindingFlags B=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
    static object Get(object x,string key){var f=x.GetType().GetField(key,B);return f!=null?f.GetValue(x):x.GetType().GetProperty(key,B).GetValue(x);}
    static void Set(object x,string key,object value)=>x.GetType().GetField(key,B).SetValue(x,value);
    static object Call(object x,string key,params object[] args){try{return x.GetType().GetMethod(key,B).Invoke(x,args);}catch(TargetInvocationException e){System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(e.InnerException??e).Throw();return null;}}
    static SimBarExperience Game()=>UnityEngine.Object.FindAnyObjectByType<SimBarExperience>();
    static void Require(bool v,string message){if(!v)throw new Exception(message);}
    static object Record(string key,object result,bool capture=false){Directory.CreateDirectory(Dir);Canvas.ForceUpdateCanvases();File.WriteAllText(Dir+key+".json",JsonConvert.SerializeObject(result,Formatting.Indented));if(capture)ScreenCapture.CaptureScreenshot(Path.GetFullPath(Dir+key+".png"));return result;}
    static Type ViewType()=>typeof(UnityEditor.EditorWindow).Assembly.GetType("UnityEditor.GameView");
    static object View()=>Resources.FindObjectsOfTypeAll(ViewType()).First();
    static object Group(){var t=typeof(UnityEditor.EditorWindow).Assembly.GetType("UnityEditor.GameViewSizes");return Call(t.GetProperty("instance",B|BindingFlags.FlattenHierarchy).GetValue(null),"GetGroup",ViewType().GetProperty("currentSizeGroupType",B).GetValue(View()));}
    static object[] Presets(){var g=Group();return Enumerable.Range(0,(int)Call(g,"GetBuiltinCount")+(int)Call(g,"GetCustomCount")).Select(i=>{var s=Call(g,"GetGameViewSize",i);return (object)new{index=i,name=((string[])Call(g,"GetDisplayTexts"))[i],width=(int)Get(s,"width"),height=(int)Get(s,"height"),type=Get(s,"sizeType").ToString()};}).ToArray();}
    public static object OriginalViewportShapes(){return Record("original-viewport-shapes",Presets());}
    static void Click(Button button){Require(button.gameObject.activeInHierarchy&&button.interactable,"Button unavailable: "+button.name);ExecuteEvents.Execute(button.gameObject,new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left},ExecuteEvents.pointerClickHandler);}
    static object Journey(SimBarExperience g)=>Get(g,"journey");
    static SimBarBooking.Plan Plan(SimBarExperience g)=>(SimBarBooking.Plan)Get(Journey(g),"booking");
    static string Journal(SimBarExperience g)=>(string)Call(g,"ExportJourneyState");
    static string[] OfficeKeys(SimBarExperience g)=>((IDictionary)Get(g,"pixelDecodedTextures")).Keys.Cast<string>().Where(k=>k.StartsWith("SimBarOffice/")).OrderBy(k=>k).ToArray();
    static void Fresh(SimBarExperience g,int owner)
    {
        Set(g,"journeyPhase",Enum.Parse(Get(g,"journeyPhase").GetType(),"Lobby"));Call(g,"SelectOwner",owner);Call(g,"ImportJourneyState","{\"version\":1,\"day\":1,\"tutorialComplete\":true}");Set(g,"cash",1000f);Call(g,"BeginDayPlanning",false);Canvas.ForceUpdateCanvases();
    }
    public static object Models()
    {
        var desk=JObject.Parse(SimBarRuntimeVerification.CheckDesk());var legacy=JObject.Parse(SimBarRuntimeVerification.CheckBooking());Require(!desk["failures"].Any(),desk.ToString());Require(!legacy["failures"].Any(),legacy.ToString());return Record("model",new{desk,legacy});
    }
    public static object Backup()
    {
        Directory.CreateDirectory(Dir);File.WriteAllText(Dir+"prefs-backup.json",JsonConvert.SerializeObject(new[]{SimBarExperience.OperationsSaveKey,SimBarExperience.OwnerPreferenceKey,SimBarLocalization.LanguageKey}.Select(key=>new{key,exists=PlayerPrefs.HasKey(key),value=PlayerPrefs.GetString(key)})));
        Directory.CreateDirectory("Library/SimBarQA");File.WriteAllText("Library/SimBarQA/DayDesk-GameView.json",JsonConvert.SerializeObject(new{index=ViewType().GetProperty("selectedSizeIndex",B).GetValue(View()),custom=(int)Call(Group(),"GetCustomCount"),texts=(string[])Call(Group(),"GetDisplayTexts"),runInBackground=UnityEditor.PlayerSettings.runInBackground}));
        return new{saved=true};
    }
    static object Size(int w,int h)
    {
        var g=Group();var assembly=typeof(UnityEditor.EditorWindow).Assembly;Type t=assembly.GetType("UnityEditor.GameViewSize"),kind=assembly.GetType("UnityEditor.GameViewSizeType");object size=Activator.CreateInstance(t,B,null,new object[]{Enum.Parse(kind,"FixedResolution"),w,h,"SimBar temporary desk QA"},null);Call(g,"AddCustomSize",size);int index=(int)Call(g,"GetBuiltinCount")+(int)Call(g,"GetCustomCount")-1;ViewType().GetProperty("selectedSizeIndex",B).SetValue(View(),index);return new{width=w,height=h,index};
    }
    public static object Desktop()=>Size(1280,800);
    public static object Mobile()=>Size(390,844);
    public static object Landscape()=>Size(844,390);
    public static object Startup()
    {
        var g=Game();g.enabled=false;var keys=OfficeKeys(g);Require(keys.Length==0,"Office art decoded before planning");return Record("startup",new{officeKeys=keys,canvasConstructed=Get(g,"bookingCanvas")!=null,officeTexture=((RawImage)Get(g,"deskArtImage")).texture==null});
    }
    static object Owner(int owner,string key)
    {
        var g=Game();Fresh(g,owner);SimBarLocalization.SetLanguage(true);Call(g,"RefreshBookingUI");Call(Get(g,"bookingLayout"),"RefreshLayout");Call(g,"SetDeskChannel",1);
        var image=(RawImage)Get(g,"deskArtImage");var names=new[]{"jessy","edison","jaiden"};Require((int)Get(g,"deskArtOwner")==owner&&(bool)Get(g,"deskOfficeReady")&&image.texture.width==640&&image.texture.height==360&&image.texture.filterMode==FilterMode.Point&&image.uvRect==new Rect(0,0,1,1),"Wrong selected office texture or fit");
        var canvas=(Canvas)Get(g,"bookingCanvas");var buttons=canvas.GetComponentsInChildren<Button>(true).Select(b=>b.name).ToArray();Require(canvas.GetComponentsInChildren<Toggle>(true).Length==1&&!buttons.Any(s=>s.Contains("Poster")||s.Contains("Demo")||s.Contains("Offer")||s.Contains("Archive")||s.Contains("Scout")),"Old workflow UI constructed");
        var art=(RectTransform)Get(g,"deskArtFrame");var caption=(RectTransform)Get(g,"deskCaptionRect");Require(-caption.anchoredPosition.y>=-art.anchoredPosition.y+art.rect.height*art.localScale.y,"Caption obscures office phone");
        string glyphText=string.Join("",canvas.GetComponentsInChildren<Text>(true).Select(t=>t.text));var font=(Font)Get(g,"BookingFont");var missing=glyphText.Where(c=>!char.IsWhiteSpace(c)&&!font.HasCharacter(c)).Distinct().ToArray();Require(missing.Length==0,"Missing glyphs: "+new string(missing));
        return Record(key,new{owner=g.SelectedOwnerId,resource="SimBarOffice/desk-"+names[owner],officeKeys=OfficeKeys(g),viewport=new{Screen.width,Screen.height},identityPreserved=true,fullImage=true,point=true,captionBelowImage=true,oldWorkflowAbsent=true,onlyCheckbox=canvas.GetComponentsInChildren<Toggle>(true).Single().name,missingGlyphs=new string(missing),buttons},true);
    }
    public static object Jessy()=>Owner(0,"desktop-jessy");
    public static object Edison()=>Owner(1,"desktop-edison");
    public static object Jaiden()=>Owner(2,"desktop-jaiden");
    public static object MobileJessy()=>Owner(0,"mobile-jessy");
    public static object MobileEdison()=>Owner(1,"mobile-edison");
    public static object MobileJaiden()=>Owner(2,"mobile-jaiden");
    public static object LandscapeJessy()=>Owner(0,"landscape-jessy");
    public static object Channels()
    {
        var g=Game();var art=(RectTransform)Get(g,"deskArtFrame");var pos=art.anchoredPosition;var scale=art.localScale;
        for(int channel=0;channel<3;channel++)
        {
            Call(g,"SetDeskChannel",channel);Require(((Image)Get(g,"deskSpeechBackground")).gameObject.activeSelf==(channel==0)&&((SimBarPixelPolygonGraphic)Get(g,"deskSpeechTail")).gameObject.activeSelf==(channel==0),"Speech bubble and tail phase mismatch");Require(((Text)Get(g,"deskMonitorText")).gameObject.activeSelf==(channel==1)&&((Text)Get(g,"deskPhoneText")).gameObject.activeSelf==(channel==2),"Inactive physical work text remains shown");Require(pos==art.anchoredPosition&&scale==art.localScale,"Owner art moved between channels");Record("channel-"+channel,new{channel,stationary=true,onlyCurrentIndicator=true});
        }
        return new{passed=true};
    }
    static object ChannelCapture(int channel){Call(Game(),"SetDeskChannel",channel);return Record("channel-"+channel,new{channel,stationary=true,onlyCurrentIndicator=true},true);}
    public static object Dialogue()=>ChannelCapture(0);
    public static object Computer()=>ChannelCapture(1);
    public static object Phone()=>ChannelCapture(2);
    public static object SilenceForStop(){foreach(var source in UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include))source.Stop();AudioListener.pause=true;return new{qaOnlyAudioStopped=true};}
    public static object State()
    {
        var g=Game();Fresh(g,0);float cash=(float)Get(g,"cash");var p=Plan(g);Call(g,"BookingLaunchNight");Require(!(bool)Get(Journey(g),"inProgress"),"Premature launch");
        Call(g,"SetDeskChannel",1);var toggle=(Toggle)Get(g,"deskFreewillToggle");toggle.isOn=true;Require(p.freewillSelected&&p.deskDoneMask==0,"Real checkbox failed");Click((Button)Get(g,"deskComputerWork"));Require(p.deskDoneMask==1,"Real computer work failed");
        string partial=Journal(g);Call(g,"JourneyReturnToLobby");Call(g,"ImportJourneyState",partial);Call(g,"ContinueJourney");p=Plan(g);Require(p.freewillSelected&&p.deskDoneMask==1&&(bool)Get(g,"JourneyIsPlanning"),"Partial plan did not continue");
        Call(g,"SetDeskChannel",2);Click((Button)Get(g,"deskPhoneWork"));Require(p.deskDoneMask==3,"Real phone confirmation failed");string complete=Journal(g);Call(g,"BookingDeskWork",1);Require(Journal(g)==complete,"Repeated phone work changed journal");
        string old=Journal(g);Call(g,"BookingListen",0);Call(g,"BookingOffer",0,0);Call(g,"BookingPromote");Call(g,"BookingScoutPoster");Call(g,"BookingSelectVenue",2);Call(g,"BookingSetTicket",2);Call(g,"BookingShowArchive",true);Call(g,"BookingShowPosters",true);Require(Journal(g)==old,"Legacy workflow changed desk");
        toggle=(Toggle)Get(g,"deskFreewillToggle");toggle.isOn=false;Require(!p.freewillSelected&&p.deskDoneMask==0,"Deselect did not reset tasks");toggle.isOn=true;Call(g,"BookingDeskWork",0);Call(g,"BookingDeskWork",1);Require((float)Get(g,"cash")==cash,"Desk work charged a fee");
        Call(g,"SetDeskChannel",1);var art=(RectTransform)Get(g,"deskArtFrame");var pos=art.anchoredPosition;var scale=art.localScale;string state=Journal(g);for(int i=0;i<72;i++)Call(g,"TickDayDeskPresentation",.1f);Require((int)Get(g,"deskManualChannel")==-1,"Manual channel never returned to auto");Require(pos==art.anchoredPosition&&scale==art.localScale&&Journal(g)==state,"Presentation moved art or changed persisted state");
        Call(g,"SetDeskChannel",1);Record("prepared-computer",new{p.deskDoneMask,p.freewillSelected,hour=SimBarBooking.Hour(p),cash},true);
        Click((Button)Get(g,"bookingLaunchButton"));Require((bool)Get(Journey(g),"inProgress")&&p.locked&&p.bookedMask==(1<<24)&&p.attemptedMask==(1<<24)&&p.attempts==1&&(float)Get(g,"cash")==cash,"Actual launch did not commit only Freewill at zero fee");
        return Record("state",new{actualCheckbox=true,actualComputerButton=true,actualPhoneButton=true,partialSaveContinue=true,reversible=true,noFee=true,oldHandlersBlocked=true,autoResumed=true,stationaryArt=true,normalNightStarted=true,bookedMask=p.bookedMask,metrics=SimBarBooking.Forecast(p,(SimBarBooking.Career)Get(Journey(g),"bookingCareer"))});
    }
    public static object Migration()
    {
        var g=Game();var cases=new System.Collections.Generic.List<object>();
        foreach(int mode in new[]{0,1,2,3})
        {
            var old=SimBarBooking.NewPlan(3,2);old.blocks=2;old.promotions=1;string plan=JsonUtility.ToJson(old);bool trusted=mode==0;float opening=mode==3?800:1000;
            Set(g,"cash",840f);Call(g,"ImportJourneyState","{\"version\":1,\"day\":3,\"booking\":"+plan+",\"hasDayOpeningCash\":"+(mode==1?"false":"true")+",\"dayOpeningCash\":"+opening+"}");if(mode==2)Set(Journey(g),"dayOpeningCash",float.PositiveInfinity);
            Call(g,"BeginDayPlanning",false);var j=Journey(g);float value=(float)Get(g,"cash");Require(SimBarBooking.IsDesk(Plan(g))&&value==(trusted?1000:840)&&(float)Get(j,"deskMigrationCredit")== (trusted?160:0)&&(bool)Get(j,"deskMigrationTrustedOpening")==trusted&&!string.IsNullOrEmpty((string)Get(j,"legacyDeskPlanJson")),"Migration credit mismatch mode "+mode);
            string saved=Journal(g);Call(g,"ImportJourneyState",saved);Call(g,"BeginDayPlanning",false);Require((float)Get(g,"cash")==value&&(float)Get(Journey(g),"deskMigrationCredit")== (trusted?160:0),"Repeated migration credited twice");cases.Add(new{mode,credit=trusted?160:0,wallet=value,oneTime=true});
        }
        var legacy=SimBarBooking.NewPlan(4,1);SimBarBooking.Lock(legacy);string frozen=JsonUtility.ToJson(legacy);Set(g,"cash",700f);Call(g,"ImportJourneyState","{\"version\":1,\"day\":4,\"session\":2,\"inProgress\":true,\"secondsLeft\":90,\"booking\":"+frozen+"}");Call(g,"BeginDayPlanning",false);Require(!SimBarBooking.IsDesk(Plan(g))&&JsonUtility.ToJson(Plan(g))==frozen&&(float)Get(g,"cash")==700,"Active old contract changed");
        var active=SimBarBooking.NewPlan(5,0);Call(g,"ImportJourneyState","{\"version\":1,\"day\":5,\"session\":3,\"inProgress\":true,\"booking\":"+JsonUtility.ToJson(active)+"}");Require(Plan(g).locked&&!SimBarBooking.IsDesk(Plan(g)),"Unlocked active old night was migrated rather than recovered");
        return Record("migration",new{cases,oldLockedActivePreserved=true,oldActiveUnlockedRecovered=true,legacyPureModelReceiptSuitePassed=true});
    }
    public static object Restore()
    {
        var saved=JArray.Parse(File.ReadAllText(Dir+"prefs-backup.json"));foreach(var x in saved){string key=(string)x["key"];if((bool)x["exists"])PlayerPrefs.SetString(key,(string)x["value"]);else PlayerPrefs.DeleteKey(key);}PlayerPrefs.Save();
        var config=JObject.Parse(File.ReadAllText("Library/SimBarQA/DayDesk-GameView.json"));
        try
        {
            var group=Group();ViewType().GetProperty("selectedSizeIndex",B).SetValue(View(),(int)config["index"]);int builtin=(int)Call(group,"GetBuiltinCount"),total=builtin+(int)Call(group,"GetCustomCount");Require(total<100,"Unexpected viewport count");
            for(int i=total-1;i>=builtin+(int)config["custom"];i--)
            {
                var s=Call(group,"GetGameViewSize",i);string name=((string[])Call(group,"GetDisplayTexts"))[i];int w=(int)Get(s,"width"),h=(int)Get(s,"height");bool owned=name.StartsWith("SimBar temporary desk QA",StringComparison.Ordinal)&&Get(s,"sizeType").ToString()=="FixedResolution"&&((w==1280&&h==800)||(w==390&&h==844)||(w==844&&h==390));
                if(owned){int count=(int)Call(group,"GetCustomCount");Call(group,"RemoveCustomSize",i);Require((int)Call(group,"GetCustomCount")==count-1,"Temporary viewport removal failed");}
            }
        }
        finally{UnityEditor.PlayerSettings.runInBackground=(bool)config["runInBackground"];}
        return Readback();
    }
    public static object Readback()
    {
        var saved=JArray.Parse(File.ReadAllText(Dir+"prefs-backup.json"));var config=JObject.Parse(File.ReadAllText("Library/SimBarQA/DayDesk-GameView.json"));var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();var presets=Presets();bool shapes=!File.Exists(Dir+"original-viewport-shapes.json")||JToken.DeepEquals(JToken.Parse(File.ReadAllText(Dir+"original-viewport-shapes.json")),JToken.FromObject(presets));return Record("preservation",new{prefsRestored=saved.All(x=>PlayerPrefs.HasKey((string)x["key"])==(bool)x["exists"]&&PlayerPrefs.GetString((string)x["key"])==(string)x["value"]),gameViewRestored=(int)ViewType().GetProperty("selectedSizeIndex",B).GetValue(View())==(int)config["index"]&&((string[])Call(Group(),"GetDisplayTexts")).SequenceEqual(config["texts"].ToObject<string[]>()),viewportShapesRestored=shapes,customCount=(int)Call(Group(),"GetCustomCount"),scene=scene.path,sceneDirty=scene.isDirty,playing=UnityEditor.EditorApplication.isPlaying,runInBackground=UnityEditor.PlayerSettings.runInBackground,compiling=UnityEditor.EditorApplication.isCompiling});
    }
    public static object PreferenceHashes()
    {
        string original=File.ReadAllText(Dir+"prefs-backup.json");var saved=JArray.Parse(original);var current=new JArray(saved.Select(x=>new JObject{{"key",(string)x["key"]},{"exists",PlayerPrefs.HasKey((string)x["key"])},{"value",PlayerPrefs.GetString((string)x["key"])}}));string actual=current.ToString(Formatting.None);Func<string,string> hash=s=>BitConverter.ToString(System.Security.Cryptography.SHA256.Create().ComputeHash(System.Text.Encoding.UTF8.GetBytes(s))).Replace("-","").ToLowerInvariant();Require(original==actual,"Preference bytes differ");return Record("preference-hashes",new{originalSha256=hash(original),restoredSha256=hash(actual),byteExact=true});
    }
}
