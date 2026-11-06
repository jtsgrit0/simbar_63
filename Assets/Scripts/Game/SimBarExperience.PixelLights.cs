using System.Collections.Generic;
using UnityEngine;

public sealed partial class SimBarExperience
{
    private sealed class PixelLightView
    {
        public readonly List<SimBarPixelPolygonGraphic> pools=new List<SimBarPixelPolygonGraphic>();
        public readonly List<SimBarPixelPolygonGraphic> cones=new List<SimBarPixelPolygonGraphic>();
        public readonly List<UnityEngine.UI.Image> flares=new List<UnityEngine.UI.Image>();
    }
    private Transform pixelLightRoot;
    private readonly List<PixelLightView> pixelLightViews=new List<PixelLightView>();
    private static readonly Vector3[] PixelLightSources={new Vector3(2.55f,5.8f,4.6f),new Vector3(3.85f,5.8f,4.74f),new Vector3(7.25f,5.8f,5f),new Vector3(8.6f,5.8f,5.1f)};
    private static readonly Vector3[] PixelLightTargets={new Vector3(1.8f,.12f,-1.2f),new Vector3(6.5f,.65f,3f),new Vector3(3.9f,.12f,-1.8f),new Vector3(8.2f,.65f,2.8f)};
    private static readonly Color32[] PixelLightColors={new Color32(72,216,255,255),new Color32(255,80,201,255),new Color32(96,223,255,255),new Color32(255,193,107,255)};
    private void InitializePixelLights()
    {
        pixelLightRoot=new GameObject("Moving Pixel Stage Spotlights",typeof(RectTransform)).transform;pixelLightRoot.SetParent(pixelRoom,false);
        PixelRect((RectTransform)pixelLightRoot,Vector2.zero,new Vector2(960f,558f));
        for(int i=0;i<4;i++)
        {
            var view=new PixelLightView();pixelLightViews.Add(view);
            for(int layer=0;layer<7;layer++)
            {
                var graphic=new GameObject(layer<4?"Colored Floor Pool":"Visible Light Cone",typeof(RectTransform),typeof(CanvasRenderer),typeof(SimBarPixelPolygonGraphic)).GetComponent<SimBarPixelPolygonGraphic>();
                graphic.transform.SetParent(pixelLightRoot,false);graphic.raycastTarget=false;
                if(layer<4)view.pools.Add(graphic);else view.cones.Add(graphic);
            }
            for(int layer=0;layer<5;layer++)view.flares.Add(PixelSolid("Lamp Pixel Flare",pixelLightRoot));
        }
    }
    private static Vector2[] PixelLightRing(Vector3 centre,float radius)
    {
        var points=new Vector2[20];
        for(int i=0;i<20;i++){float angle=i*Mathf.PI/10f;points[i]=PixelProject(centre+new Vector3(Mathf.Cos(angle)*radius,0f,Mathf.Sin(angle)*radius*.8f));}
        return points;
    }
    private static Vector2[] PixelLightHull(Vector2 source,Vector2[] ring)
    {
        var points=new List<Vector2>(ring);points.Add(source);points.Sort((a,b)=>a.x==b.x?a.y.CompareTo(b.y):a.x.CompareTo(b.x));
        var hull=new List<Vector2>();
        foreach(var p in points){while(hull.Count>=2&&PixelLightCross(hull[hull.Count-1]-hull[hull.Count-2],p-hull[hull.Count-1])<=0f)hull.RemoveAt(hull.Count-1);hull.Add(p);}
        int lower=hull.Count;
        for(int i=points.Count-2;i>=0;i--){var p=points[i];while(hull.Count>lower&&PixelLightCross(hull[hull.Count-1]-hull[hull.Count-2],p-hull[hull.Count-1])<=0f)hull.RemoveAt(hull.Count-1);hull.Add(p);}
        hull.RemoveAt(hull.Count-1);return hull.ToArray();
    }
    private static float PixelLightCross(Vector2 a,Vector2 b)=>a.x*b.y-a.y*b.x;
    private void UpdatePixelLights()
    {
        if(pixelLightRoot==null)return;
        pixelLightRoot.gameObject.SetActive(JourneyIsPlaying||JourneyIsFloorClosing);
        float seconds=pixelAnimationSeconds;
        for(int i=0;i<pixelLightViews.Count;i++)
        {
            var view=pixelLightViews[i];float phase=Mathf.Repeat(seconds+.12f+(i%2)*.5f,1f);
            float pulse=Mathf.SmoothStep(0f,1f,Mathf.Clamp01(phase/.08f))*(1f-Mathf.SmoothStep(0f,1f,Mathf.Clamp01((phase-.30f)/.18f)));
            float sway=Mathf.Sin(seconds*.72f+i*1.7f)*.48f+Mathf.Sin(seconds*Mathf.PI*4f+i*Mathf.PI)*.08f;
            Vector3 target=PixelLightTargets[i]+new Vector3(sway,0f,Mathf.Cos(seconds*.58f+i*1.4f)*.24f);Vector2 source=PixelProject(PixelLightSources[i]);
            Color color=PixelLightColors[i];float radius=i%2==1?1.18f:1.48f;
            for(int layer=0;layer<4;layer++){color.a=.05f+layer*.018f+pulse*.032f;view.pools[layer].color=color;view.pools[layer].Initialize(null,PixelLightRing(target+Vector3.up*.006f,radius*(1.25f-layer*.23f)));}
            for(int layer=0;layer<3;layer++){color.a=(layer==0?.11f:layer==1?.065f:.04f)+(layer==0?.07f:layer==1?.045f:.025f)*pulse;view.cones[layer].color=color;view.cones[layer].Initialize(null,PixelLightHull(source,PixelLightRing(target,radius*(layer==0?1f:layer==1?.7f:.38f))));}
            float[] sizes={24f,12f,4f,19f,1f};
            for(int layer=0;layer<5;layer++)
            {
                var flare=view.flares[layer];color=layer==2?new Color(1f,.965f,.875f):PixelLightColors[i];
                color.a=layer==0?.08f+pulse*.08f:layer==1?.2f+pulse*.15f:layer==2?.7f+pulse*.2f:.22f+pulse*.1f;flare.color=color;
                Vector2 size=layer<3?new Vector2(sizes[layer],sizes[layer]*.66f):layer==3?new Vector2(19f,1f):new Vector2(1f,15f);
                PixelRect(flare.rectTransform,source-size*.5f,size);
            }
        }
    }
}
