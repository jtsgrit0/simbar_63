using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Resumable departures on the existing simulation clock and operations save.</summary>
public sealed partial class SimBarExperience
{
    [Serializable] private sealed class AfterShowMember
    {
        public string id,phase="stage";public int waypoint,greeting,standAttempt;
        public float timer,delay,x,z,stageLevel=1f,blockedTime;public bool visible=true;
    }
    [Serializable] private sealed class AfterShowAudience
    {
        public string id,phase="gather",partner="";public float homeX,homeZ,homeFacing,x,y,z,targetX,targetZ;
        public bool exitOnly,capture,visible=true;
    }
    [Serializable] private sealed class AfterShowData
    {
        public int version=2,session;public bool started=true,completed,departureStarted,noticeAcknowledged;public float elapsed;
        public string departingActor="";
        public List<AfterShowMember> members=new List<AfterShowMember>();
        public List<AfterShowAudience> audience=new List<AfterShowAudience>();
    }
    private static readonly string[] AfterShowBand={"singer","guitarist","bassist","drummer"};
    private string[] afterShowAudienceIds;
    private string[] AfterShowAudienceIds
    {
        get
        {
            if(afterShowAudienceIds!=null)return afterShowAudienceIds;
            var ids=new List<string>{"patron-8","patron-9","patron-10","patron-11","patron-12","patron-13","patron-14","patron-15","patron-0","patron-1","patron-2","patron-3","patron-4","patron-5","patron-6","patron-7","support-0","support-1","support-2","crowd-surfer"};
            for(int i=16;i<patrons.Count;i++)ids.Add("patron-"+i);
            afterShowAudienceIds=ids.ToArray();return afterShowAudienceIds;
        }
    }
    private static readonly Vector3 AfterShowExit=new Vector3(8.8f,0f,-5.55f);
    private static readonly Vector2[] AfterShowSpots={new Vector2(3.05f,.25f),new Vector2(2.8f,-1.65f),new Vector2(7.7f,.35f),new Vector2(8f,-1.65f),new Vector2(3.1f,-3.6f),new Vector2(4.05f,-4.65f),new Vector2(7.65f,-3.2f),new Vector2(8.25f,-4.65f)};
    private readonly Dictionary<MotionActor,FloorActorRoute> afterShowRoutes=new Dictionary<MotionActor,FloorActorRoute>();
    private readonly Dictionary<string,Vector3> afterShowApproaches=new Dictionary<string,Vector3>();
    private readonly List<Vector3> afterShowPeople=new List<Vector3>();
    private readonly List<Vector4> afterShowBodies=new List<Vector4>();
    private AfterShowData afterShowRestored;
    private bool AfterShowPending=>journey.afterShow!=null&&(!journey.afterShow.completed||!journey.afterShow.noticeAcknowledged);
    private bool AfterShowFarewellPending=>journey.inProgress&&journey.floorClosing&&journey.afterShow!=null&&journey.afterShow.completed&&!journey.afterShow.noticeAcknowledged;
    private bool IsSolidVenueActor(MotionActor actor)=>actor.kind!=MotionKind.Band
        ||AfterShowOwns(actor)&&actor.person.root!=null&&MotionPosition(actor).y<.1f;

    private MotionActor AfterShowActor(string id)
    {
        if(id!=null&&id.StartsWith("patron-")&&int.TryParse(id.Substring(7),out int index))
            return index>=0&&index<patrons.Count&&motionLookup.TryGetValue(patrons[index],out MotionActor guest)?guest:null;
        if(id!=null&&id.StartsWith("support-")&&int.TryParse(id.Substring(8),out int support))return support>=0&&support<3?concertActors[8+support]:null;
        if(id=="crowd-surfer")return concertActors[11];
        foreach(MotionActor actor in motionActors)if(actor.kind==MotionKind.Band&&actor.person.name.ToLowerInvariant()==id)return actor;
        return null;
    }
    private bool AfterShowOwns(MotionActor actor)
    {
        if(journey.afterShow==null)return false;
        foreach(var member in journey.afterShow.members)if(AfterShowActor(member.id)==actor)return true;
        // The departing crowd remains owned through the shared barrier and exit.
        foreach(var audience in journey.afterShow.audience)if(AfterShowActor(audience.id)==actor)return true;
        return false;
    }
    private void ResetAfterShow()
    {
        foreach(MotionActor actor in motionActors)
        {
            if(AfterShowOwns(actor)&&actor.person.root!=null)
            {
                actor.person.root.position=worldRoot.TransformPoint(actor.home);actor.person.root.gameObject.SetActive(true);
                actor.person.root.localRotation=Quaternion.Euler(0f,actor.person.baseYaw,0f);
                actor.path.Clear();actor.pathIndex=0;actor.moving=false;actor.speed=0f;actor.wait=2f;
            }
            actor.afterShowAction=null;actor.afterShowProgress=0f;
            if(actor.visual!=null)actor.visual.SetOffStagePresentation(false);
        }
        journey.afterShow=null;afterShowRestored=null;afterShowRoutes.Clear();afterShowApproaches.Clear();
    }
    private void SanitizeAfterShow()
    {
        var state=journey.afterShow;
        if(state==null)return;
        if((state.version!=1&&state.version!=2)||state.session!=journey.session||state.members==null||state.audience==null)
        {journey.afterShow=null;return;}
        int previousVersion=state.version;bool settled=journey.session==journey.settledSession&&!journey.inProgress;
        var members=new List<AfterShowMember>();
        for(int i=0;i<4;i++)
        {
            var member=state.members.Find(m=>m!=null&&m.id==AfterShowBand[i]);if(member==null){journey.afterShow=null;return;}
            if(settled)member.phase="gone";else if(previousVersion==1&&member.phase=="exit")member.phase="await-exit";
            if(Array.IndexOf(new[]{"stage","approach","highfive","hug","await-exit","exit","gone"},member.phase)<0)member.phase="stage";
            member.waypoint=Mathf.Clamp(member.waypoint,0,8);member.greeting=Mathf.Clamp(member.greeting,0,2);
            if(member.phase=="await-exit"||member.phase=="exit"||member.phase=="gone")member.greeting=2;
            member.timer=JourneyClamp(member.timer,0f,0f,2f);member.delay=JourneyClamp(member.delay,0f,0f,3f);
            member.x=JourneyClamp(member.x,5.35f,-20f,20f);member.z=JourneyClamp(member.z,3.5f,-20f,20f);
            member.stageLevel=JourneyClamp(member.stageLevel,1f,0f,1f);member.visible=member.phase!="gone";
            member.blockedTime=0f;member.standAttempt=Mathf.Clamp(member.standAttempt,0,10000);
            members.Add(member);
        }
        var audience=new List<AfterShowAudience>();
        for(int i=0;i<AfterShowAudienceIds.Length;i++)
        {
            string id=AfterShowAudienceIds[i];var fan=state.audience.Find(a=>a!=null&&a.id==id);bool missing=fan==null,exitOnly=i>=8;
            if(missing)fan=new AfterShowAudience{id=id,phase=exitOnly?"farewell-wait":"gather",x=i<8?AfterShowSpots[i].x:6f,z=i<8?AfterShowSpots[i].y:-4.25f};
            if(missing){fan.homeX=fan.x;fan.homeZ=fan.z;}
            Vector2 target=i<8?AfterShowSpots[i]:new Vector2(JourneyClamp(fan.x,6f,-20f,20f),JourneyClamp(fan.z,-4.25f,-20f,20f));
            if(settled)fan.phase="gone";else if(fan.phase=="return"||fan.phase=="done")fan.phase="farewell-wait";
            if(Array.IndexOf(new[]{"gather","wait","greet","farewell-wait","exit","gone"},fan.phase)<0)fan.phase=exitOnly?"farewell-wait":"gather";
            fan.x=JourneyClamp(fan.x,target.x,-20f,20f);fan.z=JourneyClamp(fan.z,target.y,-20f,20f);fan.y=JourneyClamp(fan.y,0f,0f,3f);
            fan.homeX=JourneyClamp(fan.homeX,fan.x,-20f,20f);fan.homeZ=JourneyClamp(fan.homeZ,fan.z,-20f,20f);
            fan.homeFacing=JourneyClamp(fan.homeFacing,0f,-Mathf.PI,Mathf.PI);
            fan.targetX=target.x;fan.targetZ=target.y;fan.exitOnly=exitOnly;fan.visible=fan.phase!="gone";fan.capture=missing&&!settled||fan.capture;
            if(Array.IndexOf(AfterShowBand,fan.partner)<0)fan.partner="";
            audience.Add(fan);
        }
        state.members=members;state.audience=audience;state.version=2;
        if(!state.members.Exists(m=>m.id==state.departingActor&&m.phase=="exit")&&!state.audience.Exists(a=>a.id==state.departingActor&&a.phase=="exit"))state.departingActor="";
        state.departureStarted=settled||state.departureStarted;state.noticeAcknowledged=settled||state.noticeAcknowledged;
        state.elapsed=JourneyClamp(state.elapsed,0f,0f,1000000f);state.completed=state.members.TrueForAll(m=>m.phase=="gone")&&state.audience.TrueForAll(a=>a.phase=="gone");
    }
    private void EnsureAfterShow()
    {
        if(journey.afterShow!=null&&journey.afterShow.session==journey.session)return;
        var state=new AfterShowData{session=journey.session};
        for(int i=0;i<4;i++)
        {
            var actor=AfterShowActor(AfterShowBand[i]);Vector3 p=actor!=null?MotionPosition(actor):new Vector3(5.35f,.62f,3.5f);
            bool visible=actor==null||actor.person.root.gameObject.activeSelf;
            state.members.Add(new AfterShowMember{id=AfterShowBand[i],x=p.x,z=p.z,delay=i*.7f,stageLevel=p.y>.1f?1f:0f,phase=!visible?"gone":p.y>.1f?"stage":"approach",greeting=visible?0:2,visible=visible});
        }
        for(int i=0;i<AfterShowAudienceIds.Length;i++)
        {
            string id=AfterShowAudienceIds[i];var actor=AfterShowActor(id);bool exitOnly=i>=8;Vector2 target=i<8?AfterShowSpots[i]:new Vector2(6f,-4.25f);Vector3 p=actor!=null?MotionPosition(actor):new Vector3(target.x,0f,target.y);
            if(exitOnly)target=new Vector2(p.x,p.z);bool visible=actor==null||actor.person.root.gameObject.activeSelf;
            float facing=actor!=null?Mathf.DeltaAngle(0f,actor.person.root.localEulerAngles.y)*Mathf.Deg2Rad:0f;
            state.audience.Add(new AfterShowAudience{id=id,x=p.x,y=p.y,z=p.z,homeX=p.x,homeZ=p.z,homeFacing=facing,targetX=target.x,targetZ=target.y,exitOnly=exitOnly,visible=visible,phase=!visible?"gone":exitOnly?"farewell-wait":"gather"});
        }
        state.completed=state.members.TrueForAll(m=>m.phase=="gone")&&state.audience.TrueForAll(a=>a.phase=="gone");
        journey.afterShow=state;
    }
    private void RestoreAfterShow()
    {
        var state=journey.afterShow;if(state==null||ReferenceEquals(state,afterShowRestored))return;
        afterShowRoutes.Clear();afterShowApproaches.Clear();afterShowRestored=state;
        foreach(var member in state.members)
        {
            var actor=AfterShowActor(member.id);if(actor==null)continue;
            actor.person.root.position=worldRoot.TransformPoint(new Vector3(member.x,actor.home.y*member.stageLevel,member.z));
            actor.person.root.gameObject.SetActive(member.phase!="gone");actor.pitActive=false;
            actor.afterShowAction=member.phase=="highfive"||member.phase=="hug"?member.phase:"aftershow-wait";
            actor.afterShowProgress=member.timer/(member.phase=="hug"?1.5f:1.1f);actor.moving=false;
            if(actor.visual!=null)actor.visual.SetOffStagePresentation(true);
        }
        foreach(var fan in state.audience)
        {
            var actor=AfterShowActor(fan.id);if(actor==null)continue;
            if(fan.capture){Vector3 captured=MotionPosition(actor);fan.x=fan.homeX=captured.x;fan.z=fan.homeZ=captured.z;fan.y=captured.y;fan.capture=false;}
            actor.person.root.position=worldRoot.TransformPoint(new Vector3(fan.x,fan.y,fan.z));actor.person.root.gameObject.SetActive(fan.phase!="gone");actor.pitActive=false;actor.moving=false;
            actor.afterShowAction=fan.id=="crowd-surfer"&&fan.y>.1f?"surf":"aftershow-wait";actor.afterShowProgress=0f;
            if(fan.phase=="greet"&&AfterShowActor(fan.partner) is MotionActor partner)
            {actor.afterShowAction=partner.afterShowAction;actor.afterShowProgress=partner.afterShowProgress;AfterShowFace(actor,partner);AfterShowFace(partner,actor);}
        }
    }
    private void SyncAfterShow()
    {
        var state=journey.afterShow;if(state==null||!ReferenceEquals(state,afterShowRestored))return;
        foreach(var member in state.members){var a=AfterShowActor(member.id);if(a==null)continue;Vector3 p=MotionPosition(a);member.x=p.x;member.z=p.z;member.visible=a.person.root.gameObject.activeSelf;}
        foreach(var fan in state.audience){var a=AfterShowActor(fan.id);if(a==null)continue;Vector3 p=MotionPosition(a);fan.x=p.x;fan.y=p.y;fan.z=p.z;fan.visible=a.person.root.gameObject.activeSelf;}
    }
    private void AfterShowFace(MotionActor actor,MotionActor target)
    {
        Vector3 direction=MotionPosition(target)-MotionPosition(actor);direction.y=0f;
        if(direction.sqrMagnitude>.00001f)actor.person.root.rotation=worldRoot.rotation*Quaternion.LookRotation(direction);
    }
    private bool AfterShowBodyClear(MotionActor actor,Vector3 from,Vector3 to)
    {
        Vector3 step=to-from;step.y=0f;
        foreach(var other in motionActors)
        {
            if(other==actor||other.person.root==null||!other.person.root.gameObject.activeInHierarchy)continue;
            Vector3 p=MotionPosition(other);if(Mathf.Abs(p.y-from.y)>.4f)continue;
            Vector3 offset=p-from;offset.y=0f;float before=offset.magnitude;
            if(before<.38f&&Vector3.Dot(offset,step)<=0f)continue;
            float amount=step.sqrMagnitude>.000001f?Mathf.Clamp01(Vector3.Dot(offset,step)/step.sqrMagnitude):0f;
            if((offset-step*amount).sqrMagnitude<.38f*.38f)return false;
        }
        return true;
    }
    private bool AfterShowBuildRoute(MotionActor actor,Vector3 goal,FloorActorRoute route)
    {
        route.goal=goal;route.points.Clear();route.cursor=0;route.blocked=0f;
        Vector3 p=MotionPosition(actor);afterShowPeople.Clear();afterShowBodies.Clear();
        foreach(var other in motionActors)
        {
            if(other==actor||other.person.root==null||!other.person.root.gameObject.activeInHierarchy)continue;
            Vector3 body=MotionPosition(other);if(Mathf.Abs(p.y-body.y)>.4f)continue;
            afterShowPeople.Add(body);afterShowBodies.Add(new Vector4(body.x,0f,body.z,.4f));
        }
        return motionNavigation.FindAfterShowPath(p,goal,route.points,afterShowPeople,afterShowBodies);
    }
    private bool AfterShowGreetingClear(MotionActor actor,MotionActor guest,Vector3 point)
    {
        foreach(var other in motionActors)
        {
            if(other==actor||other==guest||other.person.root==null||!other.person.root.gameObject.activeInHierarchy||!IsSolidVenueActor(other))continue;
            Vector3 body=MotionPosition(other);
            if(Mathf.Abs(body.y-point.y)<=.4f&&FlatDistance(body,point)<.4f)return false;
        }
        return true;
    }
    private bool AfterShowWalk(MotionActor actor,Vector3 goal,float delta,float speed=1.3f,float tolerance=.18f)
    {
        if(!afterShowRoutes.TryGetValue(actor,out var route)){route=new FloorActorRoute();afterShowRoutes[actor]=route;AfterShowBuildRoute(actor,goal,route);}
        Vector3 p=MotionPosition(actor);actor.moving=false;
        if(FlatDistance(p,goal)<=tolerance)return true;
        if((route.goal-goal).sqrMagnitude>.001f)AfterShowBuildRoute(actor,goal,route);
        while(route.cursor<route.points.Count&&FlatDistance(p,route.points[route.cursor])<Mathf.Min(.05f,tolerance))route.cursor++;
        if(route.cursor<route.points.Count)
        {
            Vector3 target=route.points[route.cursor];target.y=0f;Vector3 next=Vector3.MoveTowards(p,target,speed*delta);
            if(motionNavigation.SegmentClear(p,next)&&AfterShowBodyClear(actor,p,next))
            {
                actor.person.root.position=worldRoot.TransformPoint(next);Vector3 heading=next-p;heading.y=0f;
                if(heading.sqrMagnitude>.000001f)actor.person.root.rotation=Quaternion.RotateTowards(actor.person.root.rotation,worldRoot.rotation*Quaternion.LookRotation(heading),230f*delta);
                actor.moving=heading.sqrMagnitude>.000001f;route.blocked=0f;
            }
        }
        if(!actor.moving){route.blocked+=delta;if(route.blocked>.8f)AfterShowBuildRoute(actor,goal,route);}
        return FlatDistance(MotionPosition(actor),goal)<=tolerance;
    }
    private List<Vector3> AfterShowStageRoute(int i)
    {
        float x=5.35f+(i-1.5f)*.3f;var result=new List<Vector3>();
        if(i==3)result.AddRange(new[]{new Vector3(4.05f,0f,5.55f),new Vector3(3.2f,0f,4.8f),new Vector3(3.2f,0f,3.05f)});
        else if(i==1)result.Add(new Vector3(3.4f,0f,3.05f));else if(i==2)result.Add(new Vector3(7.75f,0f,3.05f));
        result.AddRange(new[]{new Vector3(x,0f,2.55f),new Vector3(x,0f,2.15f),new Vector3(x,0f,1.65f),new Vector3(x,0f,1.15f)});return result;
    }

    private static int AfterShowDepartureCount(AfterShowData state)=>state.members.Count+state.audience.Count;
    private static string AfterShowDepartureId(AfterShowData state,int index)=>index<state.members.Count?state.members[index].id:state.audience[index-state.members.Count].id;
    private static string AfterShowDeparturePhase(AfterShowData state,int index)=>index<state.members.Count?state.members[index].phase:state.audience[index-state.members.Count].phase;
    private static void SetAfterShowDeparturePhase(AfterShowData state,int index,string phase)
    {
        if(index<state.members.Count){state.members[index].phase=phase;state.members[index].visible=phase!="gone";}
        else{state.audience[index-state.members.Count].phase=phase;state.audience[index-state.members.Count].visible=phase!="gone";state.audience[index-state.members.Count].partner="";}
    }
    private void BeginAfterShowDepartures(AfterShowData state)
    {
        if(state.departureStarted||!state.members.TrueForAll(m=>m.greeting>=2||m.phase=="gone"))return;
        var surfer=AfterShowActor("crowd-surfer");if(surfer!=null&&MotionPosition(surfer).y>.01f)return;
        state.departureStarted=true;state.departingActor="";
        for(int i=0;i<AfterShowDepartureCount(state);i++)
        {
            if(AfterShowDeparturePhase(state,i)=="gone")continue;SetAfterShowDeparturePhase(state,i,"exit");
            var actor=AfterShowActor(AfterShowDepartureId(state,i));if(actor==null)continue;
            afterShowRoutes.Remove(actor);actor.afterShowAction="aftershow-wait";actor.afterShowProgress=0f;actor.moving=false;
        }
    }
    private void TickAfterShowDepartures(AfterShowData state,float delta)
    {
        bool active=false;for(int i=0;i<AfterShowDepartureCount(state);i++)if(AfterShowDepartureId(state,i)==state.departingActor&&AfterShowDeparturePhase(state,i)=="exit")active=true;
        if(!active)state.departingActor="";
        if(string.IsNullOrEmpty(state.departingActor))
        {
            float nearest=float.MaxValue;string next="";
            for(int i=0;i<AfterShowDepartureCount(state);i++)
            {
                if(AfterShowDeparturePhase(state,i)!="exit")continue;string id=AfterShowDepartureId(state,i);var actor=AfterShowActor(id);
                if(actor==null||!actor.person.root.gameObject.activeSelf){SetAfterShowDeparturePhase(state,i,"gone");continue;}
                float distance=FlatDistance(MotionPosition(actor),AfterShowExit);
                if(distance<nearest||distance==nearest&&string.CompareOrdinal(id,next)<0){nearest=distance;next=id;}
            }
            state.departingActor=next;
        }
        for(int i=0;i<AfterShowDepartureCount(state);i++)
        {
            string id=AfterShowDepartureId(state,i);var actor=AfterShowActor(id);if(actor==null)continue;Vector3 before=MotionPosition(actor);
            actor.afterShowProgress=0f;actor.afterShowAction="aftershow-wait";actor.moving=false;
            if(AfterShowDeparturePhase(state,i)=="gone"){actor.person.root.gameObject.SetActive(false);continue;}
            if(id==state.departingActor)
            {
                actor.afterShowAction="walk";
                if(AfterShowWalk(actor,AfterShowExit,delta,i<state.members.Count?1.65f:1.45f,.18f))
                {SetAfterShowDeparturePhase(state,i,"gone");actor.person.root.gameObject.SetActive(false);actor.moving=false;actor.path.Clear();actor.pathIndex=0;afterShowRoutes.Remove(actor);state.departingActor="";}
            }
            actor.speed=FlatDistance(before,MotionPosition(actor))/delta;
            if(actor.visual!=null)actor.visual.ApplyOffStagePresentation(state.elapsed,actor.speed,actor.afterShowAction,0f);
        }
    }
    private void CompleteAfterShowFrame(AfterShowData state)
    {
        bool completed=state.members.TrueForAll(m=>m.phase=="gone")&&state.audience.TrueForAll(a=>a.phase=="gone"),justCompleted=completed&&!state.completed;
        state.completed=completed;SyncAfterShow();
        if(AfterShowFarewellPending)
        {paused=true;RefreshJourneyHUD();if(justCompleted)SaveOperations();}
    }
    private void AcknowledgeAfterShowFarewell()
    {
        if(!JourneyIsFloorClosing||!AfterShowFarewellPending)return;
        journey.afterShow.noticeAcknowledged=true;paused=false;RefreshJourneyHUD();SaveOperations();
        if(!FloorTasksPending)FinishJourneyNight();
    }
    private void TickAfterShow(float delta)
    {
        if(!JourneyIsFloorClosing||paused||!JourneyFinite(delta)||delta<=0f)return;
        EnsureAfterShow();RestoreAfterShow();delta=Mathf.Min(.1f,delta);var state=journey.afterShow;state.elapsed+=delta;
        if(state.departureStarted){TickAfterShowDepartures(state,delta);CompleteAfterShowFrame(state);return;}
        var surfer=AfterShowActor("crowd-surfer");
        if(surfer!=null&&state.audience.Exists(f=>f.id=="crowd-surfer"&&f.phase!="gone"))
        {Vector3 p=MotionPosition(surfer);p.y=Mathf.Max(0f,p.y-delta*1.85f);surfer.person.root.position=worldRoot.TransformPoint(p);}
        foreach(var fan in state.audience)
        {
            var actor=AfterShowActor(fan.id);if(actor==null)continue;
            actor.afterShowAction="aftershow-wait";actor.afterShowProgress=0f;actor.moving=false;
            if(fan.phase=="gone"){actor.person.root.gameObject.SetActive(false);continue;}
            if(fan.exitOnly)
            {
                bool raised=surfer!=null&&MotionPosition(surfer).y>.1f;
                actor.afterShowAction=fan.id=="crowd-surfer"&&raised?"surf":fan.id.StartsWith("support-")&&raised?"support":"aftershow-wait";continue;
            }
            if(fan.phase=="gather")
            {
                Vector3 goal=new Vector3(fan.targetX,0f,fan.targetZ);
                actor.afterShowAction="walk";
                // A greeting guest may wait at the safe edge of a spot already
                // occupied by an ordinary patron; no actor is moved through a body.
                if(AfterShowWalk(actor,goal,delta,1.15f,.3f))
                {
                    fan.phase="wait";actor.afterShowAction="aftershow-wait";actor.moving=false;
                }
            }
            else if(fan.phase=="wait"&&AfterShowActor(fan.partner) is MotionActor partner)AfterShowFace(actor,partner);
        }
        for(int i=0;i<state.members.Count;i++)
        {
            var member=state.members[i];var actor=AfterShowActor(member.id);if(actor==null){member.phase="gone";continue;}
            Vector3 before=MotionPosition(actor);actor.afterShowProgress=0f;actor.afterShowAction="aftershow-wait";actor.moving=false;
            if(member.phase=="gone"){actor.person.root.gameObject.SetActive(false);continue;}
            if(member.phase=="stage")
            {
                if(member.delay>0f)member.delay=Mathf.Max(0f,member.delay-delta);
                else
                {
                    var route=AfterShowStageRoute(i);float budget=delta*1.3f;Vector3 p=before;
                    while(member.waypoint<route.Count&&budget>0f)
                    {
                        Vector3 target=route[member.waypoint];target.y=p.y;float d=FlatDistance(p,target),step=Mathf.Min(d,budget);
                        if(d>.00001f){Vector3 heading=target-p;heading.y=0f;actor.person.root.rotation=worldRoot.rotation*Quaternion.LookRotation(heading);p=Vector3.MoveTowards(p,target,step);actor.moving=true;}
                        budget-=step;if(d<=step+.00001f)member.waypoint++;
                    }
                    member.stageLevel=p.z>=2.2f?1f:p.z>=1.65f?.3f+.7f*(p.z-1.65f)/.55f:Mathf.Max(0f,.3f*(p.z-1.15f)/.5f);
                    p.y=actor.home.y*member.stageLevel;actor.person.root.position=worldRoot.TransformPoint(p);actor.afterShowAction="walk";
                    if(member.waypoint>=route.Count){member.stageLevel=0f;member.phase="approach";}
                }
            }
            else if(member.phase=="await-exit"||member.phase=="exit")
            {
                member.phase="await-exit";actor.afterShowAction="aftershow-wait";actor.moving=false;
            }
            else
            {
                var fan=state.audience[i*2+Mathf.Min(1,member.greeting)];var guest=AfterShowActor(fan.id);
                if(guest==null||fan.phase=="gone"){member.greeting++;member.phase=member.greeting>=2?"await-exit":"approach";continue;}
                fan.partner=member.id;
                if(member.phase=="approach")
                {
                    // The guest first finishes gathering. Plan from the actual
                    // resting place so the illustrated palms meet side on.
                    if(fan.phase!="wait")member.blockedTime=0f;
                    else
                    {
                        Vector3 fanPosition=MotionPosition(guest);float radius=member.greeting==0?.60f:.43f;
                        if(afterShowApproaches.TryGetValue(member.id,out Vector3 cached)&&(Mathf.Abs(FlatDistance(cached,fanPosition)-radius)>.1f||!AfterShowGreetingClear(actor,guest,cached)))afterShowApproaches.Remove(member.id);
                        if(!afterShowApproaches.TryGetValue(member.id,out Vector3 goal))
                        {
                            float axis=Mathf.Atan2(.461f,.887f),side=(before.x-fanPosition.x)*.887f+(before.z-fanPosition.z)*.461f<0f?Mathf.PI:0f;
                            for(int attempt=0;attempt<16;attempt++)
                            {
                                int index=(attempt+member.standAttempt)%16,turn=index==0?0:((index+1)/2)*(index%2==1?1:-1);
                                float angle=axis+side+turn*Mathf.PI/8f;Vector3 candidate=fanPosition+new Vector3(Mathf.Cos(angle)*radius,0f,Mathf.Sin(angle)*radius);
                                var route=new FloorActorRoute();if(motionNavigation.IsWalkable(candidate)&&AfterShowGreetingClear(actor,guest,candidate)&&AfterShowBuildRoute(actor,candidate,route)){goal=candidate;afterShowApproaches[member.id]=goal;afterShowRoutes[actor]=route;break;}
                            }
                        }
                        bool arrived=afterShowApproaches.ContainsKey(member.id)&&AfterShowWalk(actor,goal,delta,1.3f,.04f);
                        actor.afterShowAction=arrived?"aftershow-wait":"walk";member.blockedTime=actor.moving?0f:member.blockedTime+delta;
                        if(!arrived&&member.blockedTime>2f){member.standAttempt++;afterShowApproaches.Remove(member.id);member.blockedTime=0f;}
                        if(arrived&&FlatDistance(MotionPosition(actor),fanPosition)<(member.greeting==0?.65f:.48f))
                        {member.phase=member.greeting==0?"highfive":"hug";member.timer=0f;fan.phase="greet";actor.moving=guest.moving=false;}
                    }
                }
                if(member.phase=="highfive"||member.phase=="hug")
                {
                    member.timer+=delta;float duration=member.phase=="hug"?1.5f:1.1f,progress=Mathf.Clamp01(member.timer/duration);
                    actor.afterShowAction=guest.afterShowAction=member.phase;actor.afterShowProgress=guest.afterShowProgress=progress;actor.moving=guest.moving=false;
                    AfterShowFace(actor,guest);AfterShowFace(guest,actor);
                    if(member.timer>=duration)
                    {fan.phase="farewell-wait";fan.partner="";guest.afterShowAction="aftershow-wait";guest.afterShowProgress=0f;guest.path.Clear();guest.pathIndex=0;member.greeting++;member.timer=0f;member.phase=member.greeting>=2?"await-exit":"approach";afterShowApproaches.Remove(member.id);afterShowRoutes.Remove(actor);afterShowRoutes.Remove(guest);}
                }
            }
            actor.speed=FlatDistance(before,MotionPosition(actor))/delta;
            if(actor.visual!=null)actor.visual.ApplyOffStagePresentation(state.elapsed,actor.speed,actor.afterShowAction,actor.afterShowProgress);
        }
        foreach(var fan in state.audience){var actor=AfterShowActor(fan.id);if(actor!=null&&fan.phase!="gone"&&actor.visual!=null)actor.visual.ApplyOffStagePresentation(state.elapsed,actor.moving?1.15f:0f,actor.afterShowAction,actor.afterShowProgress);}
        BeginAfterShowDepartures(state);CompleteAfterShowFrame(state);
    }
}
