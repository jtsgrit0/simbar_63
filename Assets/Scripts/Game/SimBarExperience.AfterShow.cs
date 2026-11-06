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
        public string id,phase="gather",partner="";public float homeX,homeZ,homeFacing,x,z,targetX,targetZ;
    }
    [Serializable] private sealed class AfterShowData
    {
        public int version=1,session;public bool started=true,completed;public float elapsed;
        public string returningFan="";
        public List<AfterShowMember> members=new List<AfterShowMember>();
        public List<AfterShowAudience> audience=new List<AfterShowAudience>();
    }
    private static readonly string[] AfterShowBand={"singer","guitarist","bassist","drummer"};
    private static readonly Vector2[] AfterShowSpots={new Vector2(3.05f,.25f),new Vector2(2.8f,-1.65f),new Vector2(7.7f,.35f),new Vector2(8f,-1.65f),new Vector2(3.1f,-3.6f),new Vector2(4.05f,-4.65f),new Vector2(7.65f,-3.2f),new Vector2(8.25f,-4.65f)};
    private readonly Dictionary<MotionActor,FloorActorRoute> afterShowRoutes=new Dictionary<MotionActor,FloorActorRoute>();
    private readonly Dictionary<string,Vector3> afterShowApproaches=new Dictionary<string,Vector3>();
    private readonly List<Vector3> afterShowPeople=new List<Vector3>();
    private readonly List<Vector4> afterShowBodies=new List<Vector4>();
    private AfterShowData afterShowRestored;
    private bool AfterShowPending=>journey.afterShow!=null&&!journey.afterShow.completed;
    private bool IsSolidVenueActor(MotionActor actor)=>actor.kind!=MotionKind.Band
        ||AfterShowOwns(actor)&&actor.person.root!=null&&MotionPosition(actor).y<.1f;

    private MotionActor AfterShowActor(string id)
    {
        if(id!=null&&id.StartsWith("patron-")&&int.TryParse(id.Substring(7),out int index))
            return index>=0&&index<patrons.Count&&motionLookup.TryGetValue(patrons[index],out MotionActor guest)?guest:null;
        foreach(MotionActor actor in motionActors)if(actor.kind==MotionKind.Band&&actor.person.name.ToLowerInvariant()==id)return actor;
        return null;
    }
    private bool AfterShowOwns(MotionActor actor)
    {
        if(journey.afterShow==null)return false;
        foreach(var member in journey.afterShow.members)if(AfterShowActor(member.id)==actor)return true;
        // Keep returned fans at their saved places until the next night resets
        // the sequence; an early fan must not wander before the last one returns.
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
        if(state.version!=1||state.session!=journey.session||state.members==null||state.audience==null||state.members.Count!=4||state.audience.Count!=8)
        {journey.afterShow=null;return;}
        for(int i=0;i<4;i++)
        {
            var member=state.members[i];if(member==null||member.id!=AfterShowBand[i]){journey.afterShow=null;return;}
            if(Array.IndexOf(new[]{"stage","approach","highfive","hug","exit","gone"},member.phase)<0)member.phase="stage";
            member.waypoint=Mathf.Clamp(member.waypoint,0,8);member.greeting=Mathf.Clamp(member.greeting,0,2);
            member.timer=JourneyClamp(member.timer,0f,0f,2f);member.delay=JourneyClamp(member.delay,0f,0f,3f);
            member.x=JourneyClamp(member.x,5.35f,-20f,20f);member.z=JourneyClamp(member.z,3.5f,-20f,20f);
            member.stageLevel=JourneyClamp(member.stageLevel,1f,0f,1f);member.visible=member.phase!="gone";
            member.blockedTime=0f;member.standAttempt=Mathf.Clamp(member.standAttempt,0,10000);
        }
        for(int i=0;i<8;i++)
        {
            var fan=state.audience[i];if(fan==null||fan.id!="patron-"+(8+i)){journey.afterShow=null;return;}
            if(Array.IndexOf(new[]{"gather","wait","greet","return","done"},fan.phase)<0)fan.phase="gather";
            fan.x=JourneyClamp(fan.x,AfterShowSpots[i].x,-20f,20f);fan.z=JourneyClamp(fan.z,AfterShowSpots[i].y,-20f,20f);
            fan.homeX=JourneyClamp(fan.homeX,fan.x,-20f,20f);fan.homeZ=JourneyClamp(fan.homeZ,fan.z,-20f,20f);
            fan.homeFacing=JourneyClamp(fan.homeFacing,0f,-Mathf.PI,Mathf.PI);
            fan.targetX=AfterShowSpots[i].x;fan.targetZ=AfterShowSpots[i].y;
            if(Array.IndexOf(AfterShowBand,fan.partner)<0)fan.partner="";
        }
        if(!state.audience.Exists(fan=>fan.id==state.returningFan&&fan.phase=="return"))state.returningFan="";
        state.elapsed=JourneyClamp(state.elapsed,0f,0f,1000000f);state.completed=state.members.TrueForAll(m=>m.phase=="gone")&&state.audience.TrueForAll(a=>a.phase=="done");
    }
    private void EnsureAfterShow()
    {
        if(journey.afterShow!=null&&journey.afterShow.session==journey.session)return;
        var state=new AfterShowData{session=journey.session};
        for(int i=0;i<4;i++)
        {
            var actor=AfterShowActor(AfterShowBand[i]);Vector3 p=actor!=null?MotionPosition(actor):new Vector3(5.35f,.62f,3.5f);
            state.members.Add(new AfterShowMember{id=AfterShowBand[i],x=p.x,z=p.z,delay=i*.7f,stageLevel=p.y>.1f?1f:0f,phase=p.y>.1f?"stage":"approach"});
        }
        for(int i=0;i<8;i++)
        {
            string id="patron-"+(8+i);var actor=AfterShowActor(id);Vector3 p=actor!=null?MotionPosition(actor):new Vector3(AfterShowSpots[i].x,0f,AfterShowSpots[i].y);
            float facing=actor!=null?Mathf.DeltaAngle(0f,actor.person.root.localEulerAngles.y)*Mathf.Deg2Rad:0f;
            state.audience.Add(new AfterShowAudience{id=id,x=p.x,z=p.z,homeX=p.x,homeZ=p.z,homeFacing=facing,targetX=AfterShowSpots[i].x,targetZ=AfterShowSpots[i].y});
        }
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
            actor.person.root.position=worldRoot.TransformPoint(new Vector3(fan.x,0f,fan.z));actor.pitActive=false;actor.moving=false;
            actor.afterShowAction="aftershow-wait";actor.afterShowProgress=0f;
            if(fan.phase=="greet"&&AfterShowActor(fan.partner) is MotionActor partner)
            {actor.afterShowAction=partner.afterShowAction;actor.afterShowProgress=partner.afterShowProgress;AfterShowFace(actor,partner);AfterShowFace(partner,actor);}
        }
    }
    private void SyncAfterShow()
    {
        var state=journey.afterShow;if(state==null||!ReferenceEquals(state,afterShowRestored))return;
        foreach(var member in state.members){var a=AfterShowActor(member.id);if(a==null)continue;Vector3 p=MotionPosition(a);member.x=p.x;member.z=p.z;member.visible=a.person.root.gameObject.activeSelf;}
        foreach(var fan in state.audience){var a=AfterShowActor(fan.id);if(a==null)continue;Vector3 p=MotionPosition(a);fan.x=p.x;fan.z=p.z;}
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
    private void TickAfterShow(float delta)
    {
        if(!JourneyIsFloorClosing||paused||!JourneyFinite(delta)||delta<=0f)return;
        EnsureAfterShow();RestoreAfterShow();delta=Mathf.Min(.1f,delta);var state=journey.afterShow;state.elapsed+=delta;
        // A return slot survives saves. One fan finishes the physical walk
        // before the next leaves, keeping opposing home routes from cycling.
        if(!state.audience.Exists(fan=>fan.id==state.returningFan&&fan.phase=="return"))
        {var next=state.audience.Find(fan=>fan.phase=="return");state.returningFan=next?.id??"";}
        // A waiting returner may stand on the active fan's home. Let that
        // blocker walk first; the bounded visited set keeps priorities stable.
        int visited=0;
        for(int attempt=0;attempt<8;attempt++)
        {
            int active=state.audience.FindIndex(fan=>fan.id==state.returningFan&&fan.phase=="return");
            if(active<0)break;visited|=1<<active;
            var home=state.audience[active];int blocker=-1;
            for(int i=0;i<state.audience.Count;i++)
            {
                var fan=state.audience[i];if((visited&(1<<i))!=0||fan.phase!="return")continue;
                var actor=AfterShowActor(fan.id);
                if(actor!=null&&FlatDistance(MotionPosition(actor),new Vector3(home.homeX,0f,home.homeZ))<.4f){blocker=i;break;}
            }
            if(blocker<0)break;state.returningFan=state.audience[blocker].id;
        }
        foreach(var fan in state.audience)
        {
            var actor=AfterShowActor(fan.id);if(actor==null)continue;
            actor.afterShowAction="aftershow-wait";actor.afterShowProgress=0f;actor.moving=false;
            if(fan.phase=="gather"||fan.phase=="return")
            {
                bool returning=fan.phase=="return";Vector3 goal=new Vector3(returning?fan.homeX:fan.targetX,0f,returning?fan.homeZ:fan.targetZ);
                if(returning&&fan.id!=state.returningFan)continue;
                actor.afterShowAction="walk";
                // A greeting guest may wait at the safe edge of a spot already
                // occupied by an ordinary patron; no actor is moved through a body.
                if(AfterShowWalk(actor,goal,delta,1.15f,returning?.02f:.3f))
                {
                    fan.phase=returning?"done":"wait";actor.afterShowAction="aftershow-wait";actor.moving=false;
                    if(returning){state.returningFan="";actor.wait=30f;actor.path.Clear();actor.pathIndex=0;actor.state=MotionState.Idle;actor.person.root.localRotation=Quaternion.Euler(0f,fan.homeFacing*Mathf.Rad2Deg,0f);}
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
            else if(member.phase=="exit")
            {
                actor.afterShowAction="walk";
                if(AfterShowWalk(actor,new Vector3(8.8f,0f,-5.55f),delta,1.65f)){member.phase="gone";member.visible=false;actor.person.root.gameObject.SetActive(false);actor.moving=false;}
            }
            else
            {
                var fan=state.audience[i*2+Mathf.Min(1,member.greeting)];var guest=AfterShowActor(fan.id);
                if(guest==null){member.greeting++;member.phase=member.greeting>=2?"exit":"approach";continue;}
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
                    {fan.phase="return";fan.partner="";member.greeting++;member.timer=0f;member.phase=member.greeting>=2?"exit":"approach";afterShowApproaches.Remove(member.id);afterShowRoutes.Remove(actor);afterShowRoutes.Remove(guest);}
                }
            }
            actor.speed=FlatDistance(before,MotionPosition(actor))/delta;
            if(actor.visual!=null)actor.visual.ApplyOffStagePresentation(state.elapsed,actor.speed,actor.afterShowAction,actor.afterShowProgress);
        }
        foreach(var fan in state.audience){var actor=AfterShowActor(fan.id);if(actor!=null&&fan.phase!="done"&&actor.visual!=null)actor.visual.ApplyOffStagePresentation(state.elapsed,actor.moving?1.15f:0f,actor.afterShowAction,actor.afterShowProgress);}
        state.completed=state.members.TrueForAll(m=>m.phase=="gone")&&state.audience.TrueForAll(a=>a.phase=="done");SyncAfterShow();
    }
}
