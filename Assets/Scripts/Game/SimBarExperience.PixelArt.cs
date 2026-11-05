using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The approved room plate and pixel atlas present the existing simulated people.
/// This layer never moves a person, serves an order, advances missions or alters saves.
/// The separate overlay Canvas leaves the original HUD and proprietor UI at native resolution.
/// </summary>
public sealed partial class SimBarExperience
{
    [Serializable] private sealed class PixelDirection{public string id;public int column;}
    [Serializable] private sealed class PixelStyle{public string id;public int row;}
    [Serializable] private sealed class PixelAtlas
    {
        public string id,resource;public int frameWidth,frameHeight,columns,cropHeight,drawHeight,footX,footY;public PixelDirection[] directionColumns;
    }
    [Serializable] private sealed class PixelAction
    {
        public string id,atlas;public string[] atlasFrames;public int row,drawHeight,cropHeight,footX,footY;public bool hasRow,horizontal,preserveStyle;public int[] frameColumns,variantRows;
        public PixelStyle[] variantStyles;public PixelDirection[] directionColumns;
    }
    [Serializable] private sealed class PixelRole
    {
        public string id,atlas;public string[] styles,atlasFrames;public int row,drawHeight,cropHeight,footX,footY;public bool horizontal;public int[] variantRows,frameColumns;
        public PixelAction[] actions;public PixelStyle[] variantStyles;public PixelDirection[] directionColumns;
    }
    [Serializable] private sealed class PixelOffset{public string id;public Vector2 offset;}
    [Serializable] private sealed class PixelBar{public float xMax,xFade,zMin,zMax;public Vector2 offset;}
    [Serializable] private sealed class PixelOccluder{public Vector2[] points;public float depth;}
    [Serializable] private sealed class PixelManifest
    {
        public int frameWidth=64,frameHeight=64,columns=4,drawHeight=54,footX=32,footY=60;
        public string defaultAtlas;
        public PixelOffset[] actorOffsets;public PixelBar barRegion;public PixelOccluder[] occluders;public PixelRole[] roles;public PixelAtlas[] atlases;
    }
    private sealed class PixelActorView
    {
        public MotionActor actor;public RectTransform root;public RawImage image;public Image marker,broom,bristles;public string id,role;public PixelLayer layer;
        public uint variantSeed;
    }
    private sealed class PixelLayer{public Transform root;public float depth;}
    private sealed class PixelParticle{public Vector3 position,velocity;public float age;public bool tissue;public Color color;public Image image;}
    private Canvas pixelWorldCanvas;
    private RectTransform pixelRoom;
    private RawImage pixelCity;
    private Texture2D pixelCharacters;
    private Camera pixelSceneCamera;
    private int pixelOriginalCullingMask;
    private bool pixelCameraOwned;
    private PixelManifest pixelManifest;
    private readonly Dictionary<MotionActor,PixelActorView> pixelActorViews=new Dictionary<MotionActor,PixelActorView>();
    private readonly List<PixelLayer> pixelStaticLayers=new List<PixelLayer>();
    private readonly List<PixelLayer> pixelDrawLayers=new List<PixelLayer>(64);
    private readonly List<MotionActor> pixelRemovedActors=new List<MotionActor>();
    private readonly List<Image> pixelPaper=new List<Image>(42);
    private readonly List<Vector2> pixelPaperPositions=new List<Vector2>(42);
    private readonly List<PixelParticle> pixelParticles=new List<PixelParticle>(64);
    private RectTransform pixelAirborne;
    private readonly Dictionary<string,PixelRole> pixelRoles=new Dictionary<string,PixelRole>();
    private readonly Dictionary<string,PixelAtlas> pixelAtlases=new Dictionary<string,PixelAtlas>();
    private readonly Dictionary<string,Texture2D> pixelAtlasTextures=new Dictionary<string,Texture2D>();
    private readonly Dictionary<string,Texture2D> pixelDecodedTextures=new Dictionary<string,Texture2D>();
    private int pixelScreenWidth,pixelScreenHeight;
    private int pixelConfettiSeen,pixelTissueSeen,pixelSession=-1;
    private SimBarFloorMissions.State pixelObservedFloorState;
    private float pixelAnimationSeconds;
    private static readonly Color32[] PixelPaperColors={new Color32(211,125,160,255),new Color32(98,189,198,255),new Color32(212,178,92,255),new Color32(146,129,180,255)};
    private static readonly int[] PixelIdleFrame={0};
    private static readonly Vector3 PixelEye=new Vector3(13f,21f,-25f),PixelLook=new Vector3(0f,1.45f,.15f);
    private static readonly Vector3 PixelForward=(PixelLook-PixelEye).normalized;
    private static readonly Vector3 PixelRight=Vector3.Cross(Vector3.up,PixelForward).normalized;
    private static readonly Vector3 PixelUp=Vector3.Cross(PixelForward,PixelRight);

    private void InitializePixelPresentation()
    {
        Texture2D venue=LoadPixelTexture("SimBarPixelArt/Venue");
        pixelCharacters=LoadPixelTexture("SimBarPixelArt/Characters");
        Texture2D city=LoadPixelTexture("SimBarPixelArt/Seoul");
        TextAsset manifest=Resources.Load<TextAsset>("SimBarPixelArt/Presentation");
        if(venue==null||pixelCharacters==null||city==null||manifest==null)
        {Debug.LogWarning("Pixel room assets are incomplete; keeping the authored 3D presentation.");return;}
        try{pixelManifest=JsonUtility.FromJson<PixelManifest>(manifest.text);}catch(Exception error){Debug.LogError(error);return;}
        if(pixelManifest?.roles==null||pixelManifest.roles.Length==0)return;
        venue.filterMode=pixelCharacters.filterMode=city.filterMode=FilterMode.Point;
        foreach(PixelRole role in pixelManifest.roles)pixelRoles[role.id]=role;
        foreach(PixelAtlas atlas in pixelManifest.atlases??Array.Empty<PixelAtlas>())
        {
            Texture2D texture=LoadPixelTexture(atlas.resource);
            if(texture==null){Debug.LogError("Missing approved pixel atlas: "+atlas.resource);return;}
            texture.filterMode=FilterMode.Point;pixelAtlases[atlas.id]=atlas;pixelAtlasTextures[atlas.id]=texture;
        }
        var canvasObject=new GameObject("SimBar Approved Pixel World",typeof(RectTransform),typeof(Canvas));
        canvasObject.transform.SetParent(transform,false);pixelWorldCanvas=canvasObject.GetComponent<Canvas>();
        pixelWorldCanvas.renderMode=RenderMode.ScreenSpaceOverlay;pixelWorldCanvas.sortingOrder=400;
        pixelWorldCanvas.pixelPerfect=true;
        pixelCity=PixelImage("Pixel Seoul Night",canvasObject.transform,city);
        pixelCity.rectTransform.anchorMin=Vector2.zero;pixelCity.rectTransform.anchorMax=Vector2.one;
        pixelCity.rectTransform.offsetMin=pixelCity.rectTransform.offsetMax=Vector2.zero;
        pixelRoom=new GameObject("Pixel Venue and Real Actors",typeof(RectTransform)).GetComponent<RectTransform>();
        pixelRoom.SetParent(canvasObject.transform,false);pixelRoom.anchorMin=pixelRoom.anchorMax=new Vector2(.5f,.5f);
        pixelRoom.pivot=new Vector2(.5f,.5f);pixelRoom.sizeDelta=new Vector2(960f,558f);
        var plate=PixelImage("Approved Empty Room Plate",pixelRoom,venue);PixelRect(plate.rectTransform,Vector2.zero,new Vector2(960f,558f));
        var paperRoot=new GameObject("Mission Paper from Saved State",typeof(RectTransform)).GetComponent<RectTransform>();
        paperRoot.SetParent(pixelRoom,false);PixelRect(paperRoot,Vector2.zero,new Vector2(960f,558f));
        var paperRandom=new System.Random(44091);
        for(int i=0;i<42;i++)
        {
            Image fleck=PixelSolid("Saved Paper "+i,paperRoot);pixelPaper.Add(fleck);
            pixelPaperPositions.Add(PixelProject(new Vector3(2f+(float)paperRandom.NextDouble()*7.2f,.025f,-4.1f+(float)paperRandom.NextDouble()*5.2f)));
        }
        pixelAirborne=new GameObject("Actual Performance Paper in Flight",typeof(RectTransform)).GetComponent<RectTransform>();
        pixelAirborne.SetParent(pixelRoom,false);PixelRect(pixelAirborne,Vector2.zero,new Vector2(960f,558f));
        for(int i=0;i<64;i++){var particle=new PixelParticle{image=PixelSolid("Airborne Paper "+i,pixelAirborne)};particle.image.gameObject.SetActive(false);pixelParticles.Add(particle);}
        foreach(PixelOccluder layer in pixelManifest.occluders??Array.Empty<PixelOccluder>())
        {
            var polygon=new GameObject("Pixel Furniture Foreground",typeof(RectTransform),typeof(CanvasRenderer),typeof(SimBarPixelPolygonGraphic)).GetComponent<SimBarPixelPolygonGraphic>();
            polygon.transform.SetParent(pixelRoom,false);polygon.Initialize(venue,layer.points);
            pixelStaticLayers.Add(new PixelLayer{root=polygon.transform,depth=layer.depth});
        }
        // Keep all navigation transforms, lights and mission actors alive. The main
        // camera now contributes only the base clear; the separate minimap still works.
        pixelSceneCamera=Camera.main;TakePixelCamera();
        FitPixelPresentation();
    }
    private Texture2D LoadPixelTexture(string resource)
    {
        if(pixelDecodedTextures.TryGetValue(resource,out Texture2D cached))return cached;
        TextAsset encoded=Resources.Load<TextAsset>(resource);if(encoded==null)return null;
        var texture=new Texture2D(2,2,TextureFormat.RGBA32,false,false){name=resource,filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};
        bool loaded=ImageConversion.LoadImage(texture,encoded.bytes,true);Resources.UnloadAsset(encoded);
        if(!loaded){Destroy(texture);return null;}
        texture.filterMode=FilterMode.Point;pixelDecodedTextures[resource]=texture;return texture;
    }
    private void OnEnable()
    {
        if(pixelWorldCanvas==null)return;pixelWorldCanvas.enabled=true;
        TakePixelCamera();
    }
    private void OnDisable()
    {
        if(pixelWorldCanvas!=null)pixelWorldCanvas.enabled=false;
        RestorePixelCamera();
    }
    private void TakePixelCamera()
    {
        if(pixelSceneCamera==null||pixelCameraOwned)return;
        pixelOriginalCullingMask=pixelSceneCamera.cullingMask;pixelSceneCamera.cullingMask=0;pixelCameraOwned=true;
    }
    private void RestorePixelCamera()
    {
        if(pixelSceneCamera!=null&&pixelCameraOwned)pixelSceneCamera.cullingMask=pixelOriginalCullingMask;
        pixelCameraOwned=false;
    }
    private void DestroyPixelPresentation()
    {
        RestorePixelCamera();
        if(pixelWorldCanvas!=null)Destroy(pixelWorldCanvas.gameObject);
        foreach(Texture2D texture in pixelDecodedTextures.Values)if(texture!=null)Destroy(texture);
        pixelDecodedTextures.Clear();pixelAtlasTextures.Clear();pixelAtlases.Clear();
        pixelActorViews.Clear();pixelStaticLayers.Clear();pixelDrawLayers.Clear();pixelParticles.Clear();pixelPaper.Clear();pixelPaperPositions.Clear();
    }

    private static RawImage PixelImage(string name,Transform parent,Texture image)
    {
        var result=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(RawImage)).GetComponent<RawImage>();
        result.transform.SetParent(parent,false);result.texture=image;result.raycastTarget=false;return result;
    }
    private static Image PixelSolid(string name,Transform parent)
    {
        var result=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(Image)).GetComponent<Image>();
        result.transform.SetParent(parent,false);result.raycastTarget=false;return result;
    }
    private static void PixelRect(RectTransform rect,Vector2 point,Vector2 size)
    {
        rect.anchorMin=rect.anchorMax=new Vector2(0f,1f);rect.pivot=new Vector2(0f,1f);
        rect.anchoredPosition=new Vector2(point.x,-point.y);rect.sizeDelta=size;
    }
    private static Vector2 PixelProject(Vector3 world)
    {
        Vector3 p=world-PixelEye;const float scale=558f/16.8f;
        return new Vector2(480f+Vector3.Dot(p,PixelRight)*scale,279f-Vector3.Dot(p,PixelUp)*scale);
    }
    private void FitPixelPresentation()
    {
        if(pixelWorldCanvas==null||pixelRoom==null)return;
        pixelScreenWidth=Screen.width;pixelScreenHeight=Screen.height;
        pixelRoom.localScale=Vector3.one*(Screen.height/558f);pixelRoom.anchoredPosition=Vector2.zero;
        float aspect=Screen.width/Mathf.Max(1f,Screen.height),imageAspect=pixelCity.texture.width/(float)pixelCity.texture.height;
        pixelCity.uvRect=imageAspect>aspect?new Rect((1f-aspect/imageAspect)*.5f,0f,aspect/imageAspect,1f)
            :new Rect(0f,(1f-imageAspect/aspect)*.5f,1f,imageAspect/aspect);
    }
    private PixelActorView CreatePixelActor(MotionActor actor)
    {
        string name=actor.person.name,id=name.ToLowerInvariant(),role="guest";
        if(name=="Singer"||name=="Guitarist"||name=="Bassist"||name=="Drummer")role=id;
        else if(name=="Bartender")role="bartender";else if(name=="Server")role="server";
        else if(name.StartsWith("Bar Guest ")){role="barseated";id="bar-seat-"+name.Substring(10);}
        else if(name.StartsWith("Lounge Guest ")){role="seated";id="lounge-"+name.Substring(13);}
        else if(actor.pitSlot==11){role="surfer";id="crowd-surfer";}
        else if(actor.pitSlot>=8&&actor.pitSlot<=10){role="support";id="support-"+(actor.pitSlot-8);}
        else if(name.StartsWith("Hired "))
            for(int i=0;i<opStaffVisuals.Length;i++)if(opStaffVisuals[i]==actor.person.root.gameObject){role=id="hired-"+i;break;}
        var root=new GameObject("Pixel Actor / "+name,typeof(RectTransform)).GetComponent<RectTransform>();
        root.SetParent(pixelRoom,false);root.anchorMin=root.anchorMax=new Vector2(0f,1f);root.pivot=new Vector2(.5f,.5f);root.sizeDelta=Vector2.zero;
        var image=PixelImage("Animated Pixel Person",root,pixelCharacters);
        var marker=PixelSolid("Live Order or Guest Marker",root);marker.gameObject.SetActive(false);
        var broom=PixelSolid("Actual Cleaner Broom",root);broom.color=new Color32(182,144,97,255);broom.gameObject.SetActive(false);
        var bristles=PixelSolid("Cleaner Broom Bristles",broom.transform);bristles.color=new Color32(199,178,126,255);
        string modelKey=actor.visual!=null?actor.visual.ModelKey:actor.person.root.name;
        uint variantSeed=PixelHash(id+"|"+modelKey);
        return new PixelActorView{actor=actor,root=root,image=image,marker=marker,broom=broom,bristles=bristles,id=id,role=role,variantSeed=variantSeed,layer=new PixelLayer{root=root}};
    }
    private static uint PixelHash(string value)
    {
        uint result=2166136261;unchecked{foreach(char c in value)result=(result^c)*16777619;}return result;
    }
    private Vector2 PixelActorOffset(PixelActorView view,Vector3 world)
    {
        Vector2 offset=Vector2.zero;
        foreach(PixelOffset value in pixelManifest.actorOffsets??Array.Empty<PixelOffset>())if(value.id==view.id)offset+=value.offset;
        PixelBar region=pixelManifest.barRegion;
        if(region!=null&&(view.role=="bartender"||view.role=="server"||view.role.StartsWith("hired-"))
            &&world.z>=region.zMin&&world.z<=region.zMax)
            offset+=region.offset*Mathf.Clamp01((region.xMax-world.x)/Mathf.Max(.01f,region.xFade));
        return offset;
    }
    private string PixelActionFor(MotionActor actor)
    {
        foreach(FloorWorkerActor worker in floorCleaningCrew)
            if(worker.actor==actor&&worker.route.ready&&FloorState!=null&&FloorState.cleanupActive&&!FloorState.cleanupComplete
                &&FloorState.reaction!=null&&FloorState.reaction.cleanupWorkBudget>0f)return "clean";
        if(actor.pitSlot==11&&actor.pitActive)return CrowdSurfWeight>.1f?"surf":"idle";
        if(actor.pitSlot>=8&&actor.pitSlot<=10&&actor.pitActive)return "support";
        if(actor.pitActive)return actor.pitSlot<6?"mosh":"slam";
        if(actor.moving)return "walk";
        if(actor.queueIndex>=0)return "queue";
        if(actor.kind==MotionKind.Band)return JourneyIsPlaying?"perform":"idle";
        if(actor.visual!=null&&actor.visual.ServingBlend>.12f)return "serve";
        if(actor.visual!=null&&actor.visual.IsWalking)return "walk";
        if(actor.kind==MotionKind.BarStaff)return "idle";
        if(actor.celebration>.05f||actor.person.isDancing)return "dance";
        return actor.person.isDancing?"dance":"idle";
    }
    private void UpdatePixelActor(PixelActorView view)
    {
        MotionActor actor=view.actor;Vector3 world=MotionPosition(actor);Vector2 offset=PixelActorOffset(view,world);
        Vector2 p=PixelProject(world)+offset;PixelRole role=pixelRoles.TryGetValue(view.role,out PixelRole found)?found:pixelRoles["guest"];
        string action=PixelActionFor(actor);bool active=!paused&&(JourneyIsPlaying||JourneyIsFloorClosing);
        int framePhase=(int)(view.variantSeed%4);
        int frame=active?Mathf.FloorToInt(pixelAnimationSeconds*(action=="walk"?7f:action=="clean"?6f:5f)+framePhase)%4:0;
        PixelAction selected=null;
        foreach(PixelAction candidate in role.actions??Array.Empty<PixelAction>())if(candidate.id==action){selected=candidate;break;}
        if(selected==null&&(action=="mosh"||action=="slam"||action=="surf"||action=="support"))
            foreach(PixelAction candidate in role.actions??Array.Empty<PixelAction>())if(candidate.id=="dance"){selected=candidate;break;}
        string wardrobe=actor.visual!=null?actor.visual.ModelKey:"Casual";
        if(role.styles!=null&&role.styles.Length>0)
        {
            bool supported=false;foreach(string style in role.styles)if(style==wardrobe){supported=true;break;}
            if(!supported)
            {
                string suffix=view.id.Substring(view.id.LastIndexOf('-')+1);
                int selection=int.TryParse(suffix,out int index)?index:actor.index;
                wardrobe=role.styles[Mathf.Abs(selection)%role.styles.Length];
            }
        }
        if(selected!=null&&selected.preserveStyle)
        {
            bool supported=false;foreach(PixelStyle style in selected.variantStyles??Array.Empty<PixelStyle>())if(style.id==wardrobe){supported=true;break;}
            if(!supported)selected=null;
        }
        int[] frames=selected?.frameColumns??role.frameColumns;
        if(frames==null||frames.Length==0)frames=PixelIdleFrame;
        string atlasId=!string.IsNullOrEmpty(selected?.atlas)?selected.atlas:!string.IsNullOrEmpty(role.atlas)?role.atlas:!string.IsNullOrEmpty(pixelManifest.defaultAtlas)?pixelManifest.defaultAtlas:"animation";
        string[] atlasFrames=selected?.atlasFrames??role.atlasFrames;
        if(atlasFrames!=null&&atlasFrames.Length>0)atlasId=atlasFrames[frame%atlasFrames.Length];
        PixelAtlas atlas=pixelAtlases.TryGetValue(atlasId,out PixelAtlas sheet)?sheet:null;
        Texture2D texture=pixelAtlasTextures.TryGetValue(atlasId,out Texture2D source)?source:pixelCharacters;
        int frameWidth=atlas!=null&&atlas.frameWidth>0?atlas.frameWidth:pixelManifest.frameWidth;
        int frameHeight=atlas!=null&&atlas.frameHeight>0?atlas.frameHeight:pixelManifest.frameHeight;
        int[] rows=selected?.variantRows??role.variantRows;
        int col=frames[frame%frames.Length],row=rows!=null&&rows.Length>0?rows[view.variantSeed%(uint)rows.Length]:selected!=null&&selected.hasRow?selected.row:role.row;
        PixelStyle[] styles=selected?.variantStyles!=null&&selected.variantStyles.Length>0?selected.variantStyles
            :selected==null||!selected.hasRow?role.variantStyles:null;
        foreach(PixelStyle style in styles??Array.Empty<PixelStyle>())if(style.id==wardrobe){row=style.row;break;}
        Vector3 forward=PixelFacingForward(view,world);
        string direction=(Vector3.Dot(forward,PixelForward)<0f?"F":"B")+(Vector3.Dot(forward,PixelRight)>=0f?"R":"L");
        PixelDirection[] directions=selected?.directionColumns!=null&&selected.directionColumns.Length>0?selected.directionColumns
            :role.directionColumns!=null&&role.directionColumns.Length>0?role.directionColumns:atlas?.directionColumns;
        bool directional=directions!=null&&directions.Length>0;
        foreach(PixelDirection facing in directions??Array.Empty<PixelDirection>())if(facing.id==direction){col=facing.column;break;}
        float crop=selected!=null&&selected.cropHeight>0?selected.cropHeight:role.cropHeight>0?role.cropHeight:atlas!=null&&atlas.cropHeight>0?atlas.cropHeight:frameHeight;
        float height=selected!=null&&selected.drawHeight>0?selected.drawHeight:role.drawHeight>0?role.drawHeight:atlas!=null&&atlas.drawHeight>0?atlas.drawHeight:pixelManifest.drawHeight,scale=height/crop;
        float footX=selected!=null&&selected.footX>0?selected.footX:role.footX>0?role.footX:atlas!=null&&atlas.footX>0?atlas.footX:pixelManifest.footX;
        float footY=selected!=null&&selected.footY>0?selected.footY:role.footY>0?role.footY:atlas!=null&&atlas.footY>0?atlas.footY:pixelManifest.footY;
        view.image.texture=texture;
        view.image.uvRect=new Rect(col*frameWidth/(float)texture.width,
            1f-(row*frameHeight+crop)/texture.height,
            frameWidth/(float)texture.width,crop/texture.height);
        RectTransform image=view.image.rectTransform;image.anchorMin=image.anchorMax=new Vector2(.5f,.5f);
        image.pivot=new Vector2(footX/frameWidth,1f-footY/crop);
        image.sizeDelta=new Vector2(frameWidth*scale,height);
        float yaw=actor.person.root.localEulerAngles.y;
        float yawSin=Mathf.Sin(yaw*Mathf.Deg2Rad);
        bool flip=!directional&&yawSin<-.18f;
        float bob=active&&(action=="dance"||action=="perform")?Mathf.Round(Mathf.Sin(pixelAnimationSeconds*5f+(view.variantSeed%9))*0.6f):0f;
        view.root.anchoredPosition=new Vector2(Mathf.Round(p.x),-Mathf.Round(p.y+bob));
        view.root.localScale=new Vector3(flip?-1f:1f,1f,1f);
        bool surf=actor.pitSlot==11&&CrowdSurfWeight>.1f&&actor.pitActive;
        bool horizontal=selected?.horizontal==true||role.horizontal;
        image.anchoredPosition=surf&&!horizontal?new Vector2(-7f,0f):Vector2.zero;
        image.localRotation=Quaternion.Euler(0f,0f,surf&&!horizontal?90f:0f);
        bool assisted=actor==floorMissionGuestActor&&FloorState!=null&&FloorState.guestActive;
        bool order=actor.person.orderMarker!=null&&actor.person.orderMarker.activeSelf;
        view.marker.gameObject.SetActive(assisted||order);
        if(assisted||order){view.marker.color=assisted?new Color32(244,200,110,255):new Color32(225,186,115,255);PixelRect(view.marker.rectTransform,new Vector2(-2f,-49f),new Vector2(4f,assisted?7f:4f));}
        bool cleaning=action=="clean";view.broom.gameObject.SetActive(cleaning);
        if(cleaning)
        {
            var shaft=view.broom.rectTransform;shaft.anchorMin=shaft.anchorMax=new Vector2(.5f,.5f);shaft.pivot=new Vector2(.5f,1f);
            shaft.anchoredPosition=new Vector2(5f,17f);shaft.sizeDelta=new Vector2(1.5f,20f);
            shaft.localRotation=Quaternion.Euler(0f,0f,-18f+Mathf.Sin(pixelAnimationSeconds*7f+actor.index)*12f);
            var bristles=view.bristles.rectTransform;bristles.anchorMin=bristles.anchorMax=new Vector2(.5f,0f);
            bristles.pivot=new Vector2(.5f,1f);bristles.anchoredPosition=Vector2.zero;bristles.sizeDelta=new Vector2(7f,3f);
        }
        float depth=PixelProject(new Vector3(world.x,0f,world.z)).y+offset.y+(view.role=="barseated"?25f:0f);
        view.layer.depth=depth;pixelDrawLayers.Add(view.layer);
    }
    private Vector3 PixelFacingForward(PixelActorView view,Vector3 world)
    {
        MotionActor actor=view.actor;
        // Face the surface or person being used while leaving the actual motion
        // transforms under the existing simulation's exclusive control.
        if(view.role=="barseated")return Vector3.forward;
        if(view.role=="seated")return world.z>0f?Vector3.back:Vector3.forward;
        if(actor.kind==MotionKind.Band)return new Vector3(ConcertPitCenter.x-world.x,0f,ConcertPitCenter.z-world.z).normalized;
        if(actor.pitActive&&actor.pitSlot>=0&&actor.pitSlot<6)
        {Vector3 radial=world-ConcertPitCenter;return new Vector3(-radial.z,0f,radial.x).normalized;}
        if(actor.pitActive&&(actor.pitSlot==6||actor.pitSlot==7))return actor.pitSlot==6?Vector3.right:Vector3.left;
        if(actor.pitActive&&actor.pitSlot>=8&&actor.pitSlot<=10&&concertActors[11]?.person.root!=null)
        {Vector3 toward=MotionPosition(concertActors[11])-world;toward.y=0f;return toward.normalized;}
        if(actor.queueIndex>=0&&!actor.moving)
        {
            Vector3 employee=new Vector3(-4.2f,0f,-.565f);
            foreach(MotionActor other in motionActors)if(other.person.name=="Server"){employee=MotionPosition(other);break;}
            Vector3 toward=employee-world;toward.y=0f;return toward.normalized;
        }
        if((view.role=="bartender"||view.role=="server")&&!actor.moving&&!IsFloorMissionActor(actor.person.root))return Vector3.back;
        if(view.role=="guest"&&actor.kind==MotionKind.Guest&&!actor.moving)
            return new Vector3(5.5f-world.x,0f,2.7f-world.z).normalized;
        Vector3 forward=worldRoot.InverseTransformDirection(actor.person.root.forward);forward.y=0f;return forward.normalized;
    }
    private void UpdatePixelPaper()
    {
        var state=FloorState;int throws=state==null?0:state.confettiThrows+state.tissueThrows;
        float remaining=state==null||state.litter<=0?0f:Mathf.Clamp01(1f-state.cleaned/state.litter);
        int count=Mathf.CeilToInt(Mathf.Min(42,throws*13)*remaining);
        for(int i=0;i<pixelPaper.Count;i++)
        {
            var image=pixelPaper[i];image.gameObject.SetActive(i<count);
            Vector2 p=pixelPaperPositions[i];
            if(i>=count)continue;bool tissue=state.tissueThrows>0&&i%Mathf.Max(1,throws)<state.tissueThrows;
            image.color=tissue?new Color32(233,229,217,255):PixelPaperColors[i%4];PixelRect(image.rectTransform,p,new Vector2(tissue?4f:2f,1f));
        }
    }
    private void BurstPixelPaper(bool tissue,int serial)
    {
        var random=new System.Random(3917+serial*97+(tissue?7001:0));int remaining=tissue?22:32;
        foreach(var particle in pixelParticles)
        {
            if(particle.image.gameObject.activeSelf)continue;
            particle.tissue=tissue;particle.age=0f;
            particle.position=new Vector3(3f+(float)random.NextDouble()*5f,2.4f+(float)random.NextDouble()*1.6f,2.8f+(float)random.NextDouble()*.5f);
            particle.velocity=new Vector3(((float)random.NextDouble()-.5f)*2.5f,.5f+(float)random.NextDouble()*1.8f,-1.5f-(float)random.NextDouble()*2.5f);
            particle.color=tissue?new Color32(241,237,225,255):PixelPaperColors[remaining%4];particle.image.color=particle.color;
            particle.image.gameObject.SetActive(true);if(--remaining<=0)break;
        }
    }
    private void UpdateAirbornePixelPaper(float delta)
    {
        if(FloorState==null)return;
        int session=journey==null?0:journey.session;
        if(pixelSession!=session||!ReferenceEquals(pixelObservedFloorState,FloorState)
            ||pixelConfettiSeen>FloorState.confettiThrows||pixelTissueSeen>FloorState.tissueThrows)
        {
            pixelSession=session;pixelObservedFloorState=FloorState;
            pixelConfettiSeen=FloorState.confettiThrows;pixelTissueSeen=FloorState.tissueThrows;
            foreach(var particle in pixelParticles)particle.image.gameObject.SetActive(false);
        }
        // A saved old throw stays on the floor. Only new scheduled performances burst.
        while(pixelConfettiSeen<FloorState.confettiThrows)BurstPixelPaper(false,++pixelConfettiSeen);
        while(pixelTissueSeen<FloorState.tissueThrows)BurstPixelPaper(true,++pixelTissueSeen);
        foreach(var particle in pixelParticles)
        {
            if(!particle.image.gameObject.activeSelf)continue;particle.age+=delta;
            particle.velocity.y-=delta*3.2f;particle.position+=particle.velocity*delta;
            if(particle.position.y<.03f||particle.age>7f){particle.image.gameObject.SetActive(false);continue;}
            PixelRect(particle.image.rectTransform,PixelProject(particle.position),new Vector2(particle.tissue?4f:2f,1f));
        }
    }
    private void LateUpdate()
    {
        if(pixelWorldCanvas==null)return;
        bool visible=worldRoot!=null&&worldRoot.gameObject.activeInHierarchy;pixelWorldCanvas.enabled=visible;if(!visible)return;
        if(pixelScreenWidth!=Screen.width||pixelScreenHeight!=Screen.height)FitPixelPresentation();
        float delta=!paused&&(JourneyIsPlaying||JourneyIsFloorClosing)?Mathf.Min(.1f,Time.unscaledDeltaTime):0f;
        pixelAnimationSeconds+=delta;
        // The 3D night-backdrop component can add its own layer on first activation.
        // Keep the camera clear while this separately composited pixel world is active.
        Camera main=Camera.main;if(main!=null&&main.cullingMask!=0)main.cullingMask=0;
        pixelDrawLayers.Clear();pixelDrawLayers.AddRange(pixelStaticLayers);
        pixelRemovedActors.Clear();
        foreach(var pair in pixelActorViews)if(pair.Key.person.root==null||!motionActors.Contains(pair.Key))pixelRemovedActors.Add(pair.Key);
        foreach(var actor in pixelRemovedActors){Destroy(pixelActorViews[actor].root.gameObject);pixelActorViews.Remove(actor);}
        foreach(MotionActor actor in motionActors)
        {
            if(actor.person.root==null)continue;
            if(!pixelActorViews.TryGetValue(actor,out PixelActorView view)){view=CreatePixelActor(actor);pixelActorViews.Add(actor,view);}
            bool active=actor.person.root.gameObject.activeInHierarchy;view.root.gameObject.SetActive(active);if(active)UpdatePixelActor(view);
        }
        UpdatePixelPaper();pixelDrawLayers.Sort((a,b)=>a.depth.CompareTo(b.depth));
        foreach(PixelLayer layer in pixelDrawLayers)layer.root.SetAsLastSibling();
        UpdateAirbornePixelPaper(delta);pixelAirborne.SetAsLastSibling();
    }
}
