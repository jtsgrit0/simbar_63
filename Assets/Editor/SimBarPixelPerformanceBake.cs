using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering.Universal;

/// <summary>Technical capture of the game's original rig, clips, arm IK and instruments.</summary>
public static class SimBarPixelPerformanceBake
{
    private const int Cell=64;
    private static int Frames=32,Rows=16;
    private enum CaptureKind{Performance,Walk,BandSocial,GuestSocial}
    private static CaptureKind kind;
    private static bool socialSequence;
    private static readonly string[] Roles={"singer","guitarist","bassist","drummer"};
    private static readonly float[] Yaw={90f,0f,270f,180f};
    private static readonly string[] Directions={"FR","BR","BL","FL"};
    private static Scene scene;
    private static Camera camera;
    private static RenderTexture target;
    private static Texture2D frame,atlas;
    private static SimBarCharacterVisual[] people;
    private static int cursor;
    private static string destination;
    public static bool Busy { get; private set; }
    public static string LastError { get; private set; }

    public static string Start(string output="ArtSource/PixelArt/V3/BandPerformance.png")
        =>StartJob(CaptureKind.Performance,output);
    public static string StartWalk(string output="ArtSource/PixelArt/V3/BandWalk.png")
        =>StartJob(CaptureKind.Walk,output);
    public static string StartBandSocial(string output="ArtSource/PixelArt/V3/BandSocial.png")
        =>StartJob(CaptureKind.BandSocial,output);
    public static string StartGuestSocial(string output="ArtSource/PixelArt/V3/GuestSocial.png")
        =>StartJob(CaptureKind.GuestSocial,output);
    public static string StartSocialSequence(){socialSequence=true;return StartWalk();}
    private static string StartJob(CaptureKind capture,string output)
    {
        if(Busy)throw new InvalidOperationException("A band capture is already running.");
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play Mode before baking performance art.");
        kind=capture;Frames=kind==CaptureKind.Performance?32:8;Rows=kind==CaptureKind.GuestSocial?64:kind==CaptureKind.BandSocial?32:16;
        LastError=null;destination=Path.GetFullPath(output);Directory.CreateDirectory(Path.GetDirectoryName(destination));
        scene=EditorSceneManager.NewPreviewScene();cursor=0;
        try
        {
            var root=new GameObject("Original FBX Pixel Capture"){hideFlags=HideFlags.HideAndDontSave};
            SceneManager.MoveGameObjectToScene(root,scene);
            people=new SimBarCharacterVisual[kind==CaptureKind.GuestSocial?8:4];
            Color[] outfits={new Color(.88f,.18f,.42f),new Color(.12f,.62f,.78f),new Color(.48f,.34f,.78f),new Color(.96f,.72f,.2f)};
            for(int i=0;i<people.Length;i++)
            {
                string role=kind==CaptureKind.GuestSocial?(i<6?"guest":i==6?"guitarist":"singer"):Roles[i];
                int variant=kind==CaptureKind.GuestSocial&&i<6?i:0;
                GameObject person=SimBarCharacterVisual.Create(role,root.transform,Vector3.zero,outfits[i%4],variant,kind==CaptureKind.GuestSocial);
                if(person==null)throw new InvalidOperationException("Missing original FBX character: "+role);
                person.hideFlags=HideFlags.HideAndDontSave;people[i]=person.GetComponent<SimBarCharacterVisual>();
                people[i].SetManaged(true);person.SetActive(false);
            }
            camera=new GameObject("Transparent Pixel Capture Camera"){hideFlags=HideFlags.HideAndDontSave}.AddComponent<Camera>();
            SceneManager.MoveGameObjectToScene(camera.gameObject,scene);camera.scene=scene;
            Vector3 forward=(new Vector3(0f,1.45f,.15f)-new Vector3(13f,21f,-25f)).normalized;
            Vector3 right=Vector3.Cross(Vector3.up,forward).normalized,up=Vector3.Cross(forward,right);
            camera.orthographic=true;camera.orthographicSize=.98f;camera.aspect=1f;
            float pixelsPerMetre=Cell/(2f*camera.orthographicSize);
            Vector3 centre=Vector3.up*(28f/(pixelsPerMetre*up.y));
            camera.transform.SetPositionAndRotation(centre-forward*10f,Quaternion.LookRotation(forward,Vector3.up));
            camera.nearClipPlane=.01f;camera.farClipPlane=30f;camera.clearFlags=CameraClearFlags.SolidColor;
            camera.backgroundColor=Color.clear;camera.allowHDR=false;camera.allowMSAA=false;
            var data=camera.GetUniversalAdditionalCameraData();data.renderPostProcessing=false;data.antialiasing=AntialiasingMode.None;data.renderShadows=false;
            var light=new GameObject("Capture Key Light"){hideFlags=HideFlags.HideAndDontSave}.AddComponent<Light>();
            SceneManager.MoveGameObjectToScene(light.gameObject,scene);light.type=LightType.Directional;light.intensity=1.2f;
            light.transform.rotation=Quaternion.Euler(35f,-30f,0f);light.shadows=LightShadows.None;
            target=new RenderTexture(Cell,Cell,24,RenderTextureFormat.ARGB32){name="64px FBX Performance Capture",filterMode=FilterMode.Point,antiAliasing=1};
            target.Create();camera.targetTexture=target;
            frame=new Texture2D(Cell,Cell,TextureFormat.RGBA32,false,false){filterMode=FilterMode.Point};
            atlas=new Texture2D(Cell*Frames,Cell*Rows,TextureFormat.RGBA32,false,false){filterMode=FilterMode.Point};
            atlas.SetPixels32(new Color32[Cell*Frames*Cell*Rows]);
            Busy=true;EditorApplication.update+=Tick;AssemblyReloadEvents.beforeAssemblyReload+=Cancel;
            WriteStatus("running");return destination;
        }
        catch(Exception error){LastError=error.ToString();Cleanup();throw;}
    }
    private static void Tick()
    {
        if(!Busy)return;
        try
        {
            for(int batch=0;batch<4&&cursor<Frames*Rows;batch++,cursor++)
            {
                int row=cursor/Frames,index=cursor%Frames,role=(row%(people.Length*4))/4,direction=row%4;
                for(int i=0;i<people.Length;i++)people[i].gameObject.SetActive(i==role);
                var person=people[role];person.transform.localRotation=Quaternion.Euler(0f,Yaw[direction],0f);
                // Reinitialize at each direction so a static preview never depends on a previous capture.
                if(index==0){person.SetManaged(false);person.SetManaged(true);}
                if(kind==CaptureKind.Performance)person.ApplyPresentation(index/8f,.8f,0f,0f);
                else if(kind==CaptureKind.Walk)person.ApplyOffStagePresentation(index*((Mathf.PI*2f/8.516667f)/8f),1.1f);
                else person.ApplyOffStagePresentation(index/8f,0f,row<people.Length*4?"highfive":"hug",index/7f);
                camera.Render();var previous=RenderTexture.active;RenderTexture.active=target;
                frame.ReadPixels(new Rect(0,0,Cell,Cell),0,0,false);frame.Apply(false,false);RenderTexture.active=previous;
                atlas.SetPixels32(index*Cell,(Rows-row-1)*Cell,Cell,Cell,frame.GetPixels32());
            }
            if(cursor<Frames*Rows){if(cursor%64==0)WriteStatus("running");return;}
            atlas.Apply(false,false);File.WriteAllBytes(destination,atlas.EncodeToPNG());
            WriteStatus("completed");CaptureKind finished=kind;Cleanup();
            if(socialSequence)
            {
                if(finished==CaptureKind.Walk)StartBandSocial();
                else if(finished==CaptureKind.BandSocial)StartGuestSocial();
                else socialSequence=false;
            }
        }
        catch(Exception error){LastError=error.ToString();WriteStatus("failed");Debug.LogException(error);Cleanup();}
    }
    public static void Cancel(){socialSequence=false;if(Busy){LastError="Capture cancelled by an assembly reload.";WriteStatus("cancelled");}Cleanup();}
    private static void WriteStatus(string status)
    {
        string path=Path.ChangeExtension(destination,"status.json");
        File.WriteAllText(path,"{\"status\":\""+status+"\",\"framesDone\":"+cursor+",\"frames\":"+(Frames*Rows)+",\"frameWidth\":64,\"frameHeight\":64,\"columns\":"+Frames+",\"rows\":"+Rows+",\"fps\":8,\"duration\":"+(kind==CaptureKind.Performance?4:1)+",\"source\":\"Original SimBarCharacterVisual rig, clips, guitar IK and paired arm IK\"}");
    }
    private static void Cleanup()
    {
        EditorApplication.update-=Tick;AssemblyReloadEvents.beforeAssemblyReload-=Cancel;Busy=false;
        if(camera!=null)camera.targetTexture=null;
        if(scene.IsValid())EditorSceneManager.ClosePreviewScene(scene);
        if(target!=null){target.Release();UnityEngine.Object.DestroyImmediate(target);}if(frame!=null)UnityEngine.Object.DestroyImmediate(frame);if(atlas!=null)UnityEngine.Object.DestroyImmediate(atlas);
        camera=null;target=null;frame=null;atlas=null;people=null;
    }
}
