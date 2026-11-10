using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Displays the selected track's complete, authentic jacket on the rear LED.</summary>
public sealed partial class SimBarExperience
{
    [Serializable] private sealed class AlbumJacketTrack{public int index;public string resource;}
    [Serializable] private sealed class AlbumJacketManifest{public AlbumJacketTrack[] tracks;}
    private AlbumJacketManifest albumJacketManifest;
    private bool albumJacketManifestLoaded;
    private int albumJacketIndex=-1;
    private RectTransform pixelAlbumRoot;
    private SimBarPixelPolygonGraphic pixelAlbumImage;
    private SimBarPixelPolygonGraphic pixelAlbumPadding;
    private readonly List<SimBarPixelPolygonGraphic> pixelAlbumFixtures=new List<SimBarPixelPolygonGraphic>();
    private GameObject physicalAlbumRoot;
    private Mesh physicalAlbumImageMesh;
    private Material physicalAlbumImageMaterial;
    private readonly List<UnityEngine.Object> albumJacketObjects=new List<UnityEngine.Object>();
    private static readonly Vector2[] AlbumFullUV={new Vector2(0f,1f),new Vector2(1f,1f),new Vector2(1f,0f),new Vector2(0f,0f)};
    private static readonly Vector2[] AlbumDefaultQuad={new Vector2(665f,117f),new Vector2(842f,163f),new Vector2(842f,230f),new Vector2(665f,184f)};
    private static readonly Vector2[] AlbumDefaultLamps={new Vector2(683f,128f),new Vector2(735f,140f),new Vector2(782f,151f),new Vector2(822f,162f)};
    private Vector2[] AlbumActiveQuad=>pixelManifest?.ledQuad?.Length==4?pixelManifest.ledQuad:AlbumDefaultQuad;

    private Vector2 AlbumLedPoint(float x,float y)
    {
        Vector2[] q=AlbumActiveQuad;
        return q[0]+(q[1]-q[0]).normalized*x+(q[3]-q[0]).normalized*y;
    }
    private Vector2[] AlbumLedFit(float width,float height)
    {
        Vector2[] quad=AlbumActiveQuad;float planeWidth=(quad[1]-quad[0]).magnitude,planeHeight=(quad[3]-quad[0]).magnitude;
        float scale=Mathf.Min(planeWidth/Mathf.Max(1f,width),planeHeight/Mathf.Max(1f,height));
        float w=width*scale,h=height*scale,x=(planeWidth-w)*.5f,y=(planeHeight-h)*.5f;
        return new[]{AlbumLedPoint(x,y),AlbumLedPoint(x+w,y),AlbumLedPoint(x+w,y+h),AlbumLedPoint(x,y+h)};
    }
    private SimBarPixelPolygonGraphic AlbumPolygon(string name,Texture texture,Vector2[] points,Vector2[] uv=null)
    {
        var graphic=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(SimBarPixelPolygonGraphic)).GetComponent<SimBarPixelPolygonGraphic>();
        graphic.transform.SetParent(pixelAlbumRoot,false);graphic.Initialize(texture,points,uv);return graphic;
    }
    private void InitializeAlbumJackets(Texture venue)
    {
        pixelAlbumRoot=new GameObject("Selected Track Rear LED",typeof(RectTransform)).GetComponent<RectTransform>();
        pixelAlbumRoot.SetParent(pixelRoom,false);PixelRect(pixelAlbumRoot,Vector2.zero,new Vector2(960f,558f));
        pixelAlbumPadding=AlbumPolygon("LED Dark Padding",null,AlbumActiveQuad);
        pixelAlbumPadding.color=new Color32(8,11,19,255);
        pixelAlbumImage=AlbumPolygon("Full Album Jacket",null,AlbumLedFit(1f,1f),AlbumFullUV);
        // These original fixture pixels are in front of the LED, before live light effects.
        BuildPixelAlbumFixtures(venue);
        pixelAlbumRoot.gameObject.SetActive(false);
    }
    private void BuildPixelAlbumFixtures(Texture venue)
    {
        Vector2[] centers=pixelManifest?.ledLamps?.Length>0?pixelManifest.ledLamps:AlbumDefaultLamps;
        Vector2[] silhouette={new Vector2(-5f,-12f),new Vector2(4f,-11f),new Vector2(10f,-4f),new Vector2(10f,4f),new Vector2(4f,10f),new Vector2(-5f,9f),new Vector2(-11f,3f),new Vector2(-10f,-5f)};
        float scale=pixelManifest!=null&&pixelManifest.ledFixtureScale>0f?pixelManifest.ledFixtureScale:1f;
        foreach(Vector2 center in centers){var points=new Vector2[silhouette.Length];for(int i=0;i<points.Length;i++)points[i]=center+silhouette[i]*scale;var fixture=AlbumPolygon("Original Hanging Stage Fixture",venue,points);RegisterPixelVenueMask(fixture,points);pixelAlbumFixtures.Add(fixture);}
    }
    private void RefreshPixelAlbumGeometry()
    {
        if(pixelAlbumRoot==null)return;
        pixelAlbumPadding.Initialize(null,AlbumActiveQuad);
        foreach(SimBarPixelPolygonGraphic fixture in pixelAlbumFixtures)
        {
            pixelVenueMasks.RemoveAll(value=>value.graphic==fixture);
            fixture.gameObject.SetActive(false);Destroy(fixture.gameObject);
        }
        pixelAlbumFixtures.Clear();BuildPixelAlbumFixtures(pixelVenueTexture);albumJacketIndex=-1;
    }
    private void UpdateAlbumJacket()
    {
        // Decode only on entering the venue, never during the proprietor/startup screen.
        if(!albumJacketManifestLoaded)
        {
            albumJacketManifestLoaded=true;TextAsset manifest=Resources.Load<TextAsset>("SimBarAlbumJackets/Tracks");
            if(manifest!=null){try{albumJacketManifest=JsonUtility.FromJson<AlbumJacketManifest>(manifest.text);}catch(Exception error){Debug.LogWarning(error.Message);}Resources.UnloadAsset(manifest);}
        }
        if(albumJacketIndex==playlistIndex)return;albumJacketIndex=playlistIndex;
        Texture2D cover=null;
        foreach(AlbumJacketTrack track in albumJacketManifest?.tracks??Array.Empty<AlbumJacketTrack>())
            if(track.index==playlistIndex){cover=LoadPixelTexture(track.resource);break;}
        bool available=cover!=null;
        if(pixelAlbumRoot!=null)pixelAlbumRoot.gameObject.SetActive(available);
        if(!available){if(physicalAlbumRoot!=null)physicalAlbumRoot.SetActive(false);return;}
        cover.filterMode=FilterMode.Bilinear;
        if(pixelAlbumImage!=null)pixelAlbumImage.Initialize(cover,AlbumLedFit(cover.width,cover.height),AlbumFullUV);
        UpdatePhysicalAlbumJacket(cover);
    }
    private Mesh AlbumSurface(string name,Transform parent,Vector3[] vertices,Texture texture,Color tint,out Material material)
    {
        var surface=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));surface.transform.SetParent(parent,false);
        var mesh=new Mesh{name=name,vertices=vertices,uv=AlbumFullUV,triangles=new[]{0,1,2,0,2,3}};mesh.RecalculateNormals();mesh.RecalculateBounds();albumJacketObjects.Add(mesh);
        surface.GetComponent<MeshFilter>().sharedMesh=mesh;
        material=new Material(Shader.Find("Universal Render Pipeline/Unlit")??Shader.Find("Unlit/Texture")){name=name};albumJacketObjects.Add(material);
        material.color=tint;material.mainTexture=texture;
        if(material.HasProperty("_BaseColor"))material.SetColor("_BaseColor",tint);
        if(material.HasProperty("_BaseMap"))material.SetTexture("_BaseMap",texture);
        if(material.HasProperty("_Cull"))material.SetFloat("_Cull",(float)CullMode.Off);
        var renderer=surface.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;return mesh;
    }
    private static Vector3[] PhysicalAlbumQuad(float x,float y,float width,float height,float z)=>new[]{new Vector3(x,y+height,z),new Vector3(x+width,y+height,z),new Vector3(x+width,y,z),new Vector3(x,y,z)};
    private void UpdatePhysicalAlbumJacket(Texture2D cover)
    {
        // The authored rear sign belongs to a merged truss mesh. Add an inset instead
        // of changing that shared mesh or its other neon/fixture materials.
        if(physicalAlbumRoot==null)
        {
            physicalAlbumRoot=new GameObject("Rear LED Selected Album");physicalAlbumRoot.transform.SetParent(worldRoot,false);
            AlbumSurface("Rear LED Dark Padding",physicalAlbumRoot.transform,PhysicalAlbumQuad(2.60f,2.48f,5.60f,2.34f,7.075f),null,new Color32(8,11,19,255),out Material padding);
            physicalAlbumImageMesh=AlbumSurface("Rear LED Full Jacket",physicalAlbumRoot.transform,PhysicalAlbumQuad(2.60f,2.48f,5.60f,2.34f,7.065f),cover,Color.white,out physicalAlbumImageMaterial);
        }
        physicalAlbumRoot.SetActive(true);
        float scale=Mathf.Min(5.60f/cover.width,2.34f/cover.height),w=cover.width*scale,h=cover.height*scale;
        physicalAlbumImageMesh.vertices=PhysicalAlbumQuad(2.60f+(5.60f-w)*.5f,2.48f+(2.34f-h)*.5f,w,h,7.065f);physicalAlbumImageMesh.RecalculateBounds();
        physicalAlbumImageMaterial.mainTexture=cover;if(physicalAlbumImageMaterial.HasProperty("_BaseMap"))physicalAlbumImageMaterial.SetTexture("_BaseMap",cover);
    }
    private void DestroyAlbumJackets()
    {
        if(physicalAlbumRoot!=null)Destroy(physicalAlbumRoot);
        foreach(UnityEngine.Object item in albumJacketObjects)if(item!=null)Destroy(item);
        albumJacketObjects.Clear();
        pixelAlbumFixtures.Clear();pixelAlbumPadding=null;
    }
}
