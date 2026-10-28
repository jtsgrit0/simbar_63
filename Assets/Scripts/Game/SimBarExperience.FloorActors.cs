using System.Collections.Generic;
using UnityEngine;

/// <summary>Temporarily lends existing employees to floor work without changing the service queue.</summary>
public sealed partial class SimBarExperience
{
    private sealed class FloorActorRoute
    {
        public readonly List<Vector3> points = new List<Vector3>(40);
        public int cursor;
        public Vector3 goal;
        public bool bridge, ready;
        public float blocked;
    }

    private sealed class FloorWorkerActor
    {
        public MotionActor actor;
        public Vector3 savedPosition;
        public Quaternion savedRotation;
        public int hiredIndex = -1;
        public readonly FloorActorRoute route = new FloorActorRoute();
    }

    private readonly HashSet<Transform> floorControlledRoots = new HashSet<Transform>();
    private readonly List<FloorWorkerActor> floorCleaningCrew = new List<FloorWorkerActor>(5);
    private readonly List<Vector3> floorActorOccupancy = new List<Vector3>(48);
    private readonly List<Vector4> floorActorBodies = new List<Vector4>(48);
    private SimBarVenueNavigation floorNavigation;
    private SimBarVenueNavigation FloorNavigation
    {
        get { if (floorNavigation==null) floorNavigation=new SimBarVenueNavigation(true); return floorNavigation; }
    }
    private static readonly Vector3 FloorBarExit = new Vector3(-1.35f, 0f, -.565f);
    private static readonly Vector3 FloorGuestHome = new Vector3(-.65f, 0f, -.25f);
    private static readonly Vector3 FloorGuestExit = new Vector3(8.7f, 0f, -5.4f);
    private static readonly Vector3[] FloorCleanupStops =
    {
        new Vector3(2.2f,0f,-3.9f),new Vector3(2.2f,0f,.75f),
        new Vector3(8.45f,0f,.75f),new Vector3(8.35f,0f,-3.8f),
        new Vector3(4.2f,0f,-4.8f),new Vector3(7.9f,0f,-4.8f)
    };
    private Patron floorMissionGuest;
    private MotionActor floorMissionGuestActor;
    private readonly FloorActorRoute floorGuestRoute = new FloorActorRoute();
    private readonly FloorActorRoute floorEscortRoute = new FloorActorRoute();
    private FloorWorkerActor floorEscortWorker;
    private bool floorEscortJoined, floorEscortReturning, floorEscortHomeBridgePending;
    private bool floorGuestLeaving, floorGuestExited;
    private float floorActorSeconds, floorGuestWalkSpeed = 2f;

    private bool IsFloorMissionActor(Transform actorRoot)
    {
        return actorRoot != null && floorControlledRoots.Contains(actorRoot);
    }

    private int CleanupReadyWorkers
    {
        get
        {
            if (FloorState == null || !FloorState.cleanupActive || FloorState.cleanupComplete) return 0;
            int count = 0;
            for (int i=0;i<floorCleaningCrew.Count;i++)
            {
                var worker=floorCleaningCrew[i];
                if (FloorWorkerAvailable(worker) && worker.route.ready
                    && FlatDistance(MotionPosition(worker.actor),FloorWorkerGoal(i)) <= .22f) count++;
            }
            return count;
        }
    }

    // A completed save does not replay an already committed guest departure.
    private bool FloorGuestExitFinished
    {
        get { return FloorState == null || !FloorState.guestComplete || floorMissionGuest == null || (floorGuestExited && floorEscortWorker == null); }
    }

    private void BeginFloorWorkerCleanup()
    {
        if (worldRoot == null || FloorState == null || !FloorState.cleanupActive) return;
        foreach (MotionActor actor in motionActors)
            if (actor.person.root != null && actor.person.root.gameObject.activeInHierarchy
                && (actor.person.name == "Bartender" || actor.person.name == "Server"))
                AddFloorWorker(actor,-1);
        for (int staff=0;staff<opStaffVisuals.Length && floorCleaningCrew.Count<SimBarFloorMissions.MaxCleanupWorkers;staff++)
        {
            if (!OpStaffOnDuty(staff) || opStaffVisuals[staff] == null) continue;
            foreach (MotionActor actor in motionActors)
                if (actor.person.root != null && actor.person.root.gameObject == opStaffVisuals[staff])
                { AddFloorWorker(actor,staff); break; }
        }
    }

    private void AddFloorWorker(MotionActor actor,int hiredIndex)
    {
        if (floorCleaningCrew.Count >= SimBarFloorMissions.MaxCleanupWorkers) return;
        foreach (var existing in floorCleaningCrew) if (existing.actor == actor) return;
        var worker=floorEscortWorker != null && floorEscortWorker.actor == actor ? floorEscortWorker : new FloorWorkerActor
        {
            actor=actor,hiredIndex=hiredIndex,
            savedPosition=actor.person.root.localPosition,savedRotation=actor.person.root.localRotation
        };
        floorCleaningCrew.Add(worker); floorControlledRoots.Add(actor.person.root);
        actor.moving=false; actor.speed=0f;
        if (worker == floorEscortWorker) return;
        PrepareFloorWorkerRoute(worker,floorCleaningCrew.Count-1);
    }

    private void PrepareFloorWorkerRoute(FloorWorkerActor worker,int index)
    {
        MotionActor actor=worker.actor;
        Vector3 position=MotionPosition(actor);
        worker.route.points.Clear(); worker.route.cursor=0; worker.route.ready=false; worker.route.bridge=false;
        worker.route.goal=FloorWorkerGoal(index);
        if (!FloorNavigation.IsWalkable(position))
        {
            // The existing nav grid intentionally excludes the staff's clear aisle behind the bar.
            worker.route.bridge=true;
            if (Mathf.Abs(position.z-FloorBarExit.z)>.04f)
                worker.route.points.Add(new Vector3(position.x,position.y,FloorBarExit.z));
            worker.route.points.Add(new Vector3(FloorBarExit.x,position.y,FloorBarExit.z));
        }
        else BuildFloorRoute(worker.route,actor.person.root,worker.route.goal);
    }

    private Vector3 FloorWorkerGoal(int index)
    {
        float progress=SimBarFloorMissions.CleanupProgress(FloorState);
        int segment=Mathf.Min(5,Mathf.FloorToInt(progress*6f));
        Vector3 goal=FloorCleanupStops[(segment+(index%2)*3)%FloorCleanupStops.Length];
        if (index>=2)
        {
            Vector3 offset=index<4?new Vector3(-.45f,0,.32f):new Vector3(.38f,0,-.32f);
            Vector3 candidate=goal+offset;
            if (!FloorWalkable(candidate)) candidate=goal-offset;
            if (FloorWalkable(candidate)) goal=candidate;
        }
        return goal;
    }

    private bool FloorWorkerAvailable(FloorWorkerActor worker)
    {
        return worker != floorEscortWorker && worker.actor != null && worker.actor.person.root != null
            && worker.actor.person.root.gameObject.activeInHierarchy
            && (worker.hiredIndex < 0 || OpStaffOnDuty(worker.hiredIndex));
    }

    private bool FloorWalkable(Vector3 point)
    {
        return (motionNavigation == null || motionNavigation.IsWalkable(point)) && !CrowdFloorReserved(point);
    }

    private SimBarVenueNavigation FloorActorNavigation(Transform actorRoot)
    {
        // The assisted guest keeps to the public aisle; staff alone use the bar aisle.
        return floorMissionGuest!=null && floorMissionGuest.root==actorRoot && motionNavigation!=null
            ?motionNavigation:FloorNavigation;
    }

    private void BuildFloorRoute(FloorActorRoute route,Transform actorRoot,Vector3 goal)
    {
        route.points.Clear(); route.cursor=0; route.goal=goal; route.ready=false; route.blocked=0f;
        Vector3 start=worldRoot.InverseTransformPoint(actorRoot.position);
        floorActorOccupancy.Clear(); floorActorBodies.Clear();
        foreach (MotionActor other in motionActors)
            if (other.person.root != null && other.person.root != actorRoot && !IsFloorEscortPair(actorRoot,other.person.root) && other.kind != MotionKind.Band
                && other.person.root.gameObject.activeInHierarchy)
            {
                Vector3 body=MotionPosition(other);
                if (Mathf.Abs(body.y-start.y)>1f) continue;
                floorActorOccupancy.Add(body);
                floorActorBodies.Add(new Vector4(body.x,body.y,body.z,.42f));
            }
        FloorActorNavigation(actorRoot).FindAislePath(start,goal,route.points,floorActorOccupancy,floorActorBodies);
    }

    private void TickFloorActors(float delta)
    {
        if (worldRoot == null || paused || delta<=0f || float.IsNaN(delta) || float.IsInfinity(delta)) return;
        delta=Mathf.Min(delta,.1f); floorActorSeconds+=delta;
        UpdateFloorGuestVisual();
        if (FloorState != null && FloorState.cleanupActive) BeginFloorWorkerCleanup();
        Vector3 first=Vector3.zero,second=Vector3.zero; int brooms=0;
        for (int i=0;i<floorCleaningCrew.Count;i++)
        {
            var worker=floorCleaningCrew[i];
            if (!FloorWorkerAvailable(worker)) continue;
            MotionActor actor=worker.actor; Vector3 before=MotionPosition(actor);
            bool cleaning=FloorState != null && FloorState.cleanupActive && !FloorState.cleanupComplete;
            Vector3 goal=FloorWorkerGoal(i);
            if (cleaning)
            {
                if (!worker.route.bridge && (worker.route.goal-goal).sqrMagnitude>.01f)
                    BuildFloorRoute(worker.route,actor.person.root,goal);
                if (!worker.route.bridge && worker.route.points.Count==0 && FlatDistance(before,goal)>.2f)
                {
                    worker.route.blocked+=delta;
                    if (worker.route.blocked>=1f) BuildFloorRoute(worker.route,actor.person.root,goal);
                }
                MoveFloorRoute(worker.route,actor.person.root,1.55f,delta);
                if (worker.route.bridge && worker.route.cursor>=worker.route.points.Count)
                { worker.route.bridge=false; BuildFloorRoute(worker.route,actor.person.root,goal); }
                worker.route.ready=!worker.route.bridge && FlatDistance(MotionPosition(actor),goal)<=.2f;
            }
            float speed=Vector3.Distance(before,MotionPosition(actor))/delta;
            bool sweeping=cleaning && worker.route.ready;
            if (sweeping)
            {
                float yaw=180f+Mathf.Sin(floorActorSeconds*3f+i)*18f;
                actor.person.root.localRotation=Quaternion.RotateTowards(actor.person.root.localRotation,Quaternion.Euler(0,yaw,0),150f*delta);
                if (brooms==0) first=MotionPosition(actor); else if (brooms==1) second=MotionPosition(actor);
                brooms++;
            }
            if (actor.visual != null) actor.visual.ApplyPresentation(floorActorSeconds,.62f,speed,sweeping ? .65f : 0f);
            actor.speed=speed; actor.moving=speed>.03f;
        }
        if (floorPerformance != null) floorPerformance.SetCleanerPositions(first,second,Mathf.Min(2,brooms));
        TickFloorGuest(delta);
    }

    private void MoveFloorRoute(FloorActorRoute route,Transform actorRoot,float speed,float delta)
    {
        Vector3 position=worldRoot.InverseTransformPoint(actorRoot.position);
        SimBarVenueNavigation navigation=FloorActorNavigation(actorRoot);
        while (route.cursor<route.points.Count && FlatDistance(position,route.points[route.cursor])<.08f)
        {
            // Being near a corner does not make the next segment safe. In particular,
            // cutting the first staff-aisle node can leave a worker outside the aisle.
            if (route.cursor+1<route.points.Count && !navigation.SegmentClear(position,route.points[route.cursor+1])) break;
            route.cursor++;
        }
        if (route.cursor>=route.points.Count)
        {
            if (!route.bridge && FlatDistance(position,route.goal)>.2f)
            {
                route.blocked+=delta;
                if (route.blocked>=.8f) BuildFloorRoute(route,actorRoot,route.goal);
            }
            return;
        }
        Vector3 target=route.points[route.cursor]; target.y=position.y;
        Vector3 next=Vector3.MoveTowards(position,target,speed*delta);
        bool clear=route.bridge || navigation.SegmentClear(position,next);
        if (clear)
            foreach (MotionActor other in motionActors)
            {
                if (other.person.root==null || other.person.root==actorRoot || IsFloorEscortPair(actorRoot,other.person.root) || other.kind==MotionKind.Band || !other.person.root.gameObject.activeInHierarchy) continue;
                Vector3 body=MotionPosition(other);
                if (Mathf.Abs(body.y-position.y)>1f) continue;
                float before=FlatDistance(position,body),after=FlatDistance(next,body);
                if (after<.38f && after<before-.00001f) { clear=false; break; }
            }
        if (!clear)
        {
            route.blocked+=delta;
            if (!route.bridge && route.blocked>1.2f) BuildFloorRoute(route,actorRoot,route.goal);
            return;
        }
        route.blocked=0f; actorRoot.position=worldRoot.TransformPoint(next);
        Vector3 heading=next-position; heading.y=0f;
        if (heading.sqrMagnitude>.000001f)
            actorRoot.rotation=Quaternion.RotateTowards(actorRoot.rotation,worldRoot.rotation*Quaternion.LookRotation(heading),230f*delta);
    }

    private void UpdateFloorGuestVisual()
    {
        if (worldRoot==null || FloorState==null || !FloorState.guestSpawned) return;
        if (floorMissionGuest==null)
        {
            if (FloorState.guestComplete) return;
            floorMissionGuest=CreatePerson("Guest needing assistance",FloorGuestHome,new Color(.53f,.35f,.65f),Color.black,false,worldRoot,"guest",180f);
            if (floorMissionGuest==null || floorMissionGuest.root==null) return;
            motionLookup.TryGetValue(floorMissionGuest,out floorMissionGuestActor);
            floorControlledRoots.Add(floorMissionGuest.root);
        }
        if (FloorState.guestComplete && !floorGuestLeaving && !floorGuestExited)
        {
            floorGuestLeaving=true;
            BeginFloorGuestEscort();
        }
    }

    private void TickFloorGuest(float delta)
    {
        if (floorEscortReturning) TickFloorEscortReturn(delta);
        if (floorMissionGuest==null || floorMissionGuest.root==null || floorGuestExited) return;
        Vector3 before=worldRoot.InverseTransformPoint(floorMissionGuest.root.position);
        if (floorGuestLeaving)
        {
            if (floorEscortWorker==null) BeginFloorGuestEscort();
            if (floorEscortWorker!=null)
            {
                Transform escortRoot=floorEscortWorker.actor.person.root;
                Vector3 escortBefore=MotionPosition(floorEscortWorker.actor);
                if (!floorEscortJoined)
                {
                    MoveFloorRoute(floorEscortRoute,escortRoot,2.5f,delta);
                    if (floorEscortRoute.bridge && floorEscortRoute.cursor>=floorEscortRoute.points.Count)
                    {
                        floorEscortRoute.bridge=false;
                        BuildFloorRoute(floorEscortRoute,escortRoot,FloorEscortMeetingPoint());
                    }
                    if (FlatDistance(MotionPosition(floorEscortWorker.actor),before)<.95f)
                    {
                        floorEscortJoined=true;
                        BuildFloorRoute(floorGuestRoute,floorMissionGuest.root,FloorGuestExit);
                        float length=0f; Vector3 position=before;
                        foreach (Vector3 point in floorGuestRoute.points) { length+=FlatDistance(position,point); position=point; }
                        floorGuestWalkSpeed=Mathf.Clamp(length/5f,1.5f,2.6f);
                    }
                }
                if (floorEscortJoined)
                {
                    if (FlatDistance(MotionPosition(floorEscortWorker.actor),before)<1.35f)
                        MoveFloorRoute(floorGuestRoute,floorMissionGuest.root,floorGuestWalkSpeed,delta);
                    Vector3 guestPosition=worldRoot.InverseTransformPoint(floorMissionGuest.root.position);
                    FollowFloorGuest(guestPosition,delta);
                    if (FlatDistance(guestPosition,FloorGuestExit)<.2f
                        && FlatDistance(MotionPosition(floorEscortWorker.actor),FloorGuestExit)<.9f)
                    {
                        floorGuestExited=true; floorGuestLeaving=false;
                        floorMissionGuest.root.gameObject.SetActive(false);
                        BeginFloorEscortReturn();
                    }
                }
                PresentFloorEscort(escortBefore,delta);
            }
        }
        else floorMissionGuest.root.localRotation=Quaternion.Euler(0,180f+Mathf.Sin(floorActorSeconds*.7f)*3f,Mathf.Sin(floorActorSeconds*.9f)*1.5f);
        float speed=FlatDistance(before,worldRoot.InverseTransformPoint(floorMissionGuest.root.position))/delta;
        if (floorMissionGuestActor!=null)
        {
            floorMissionGuestActor.speed=speed; floorMissionGuestActor.moving=speed>.03f;
            if (floorMissionGuestActor.visual!=null)
                floorMissionGuestActor.visual.ApplyPresentation(floorActorSeconds,.25f,speed,!floorGuestLeaving && FloorState.guestStep>0 ? .25f : 0f);
        }
    }

    private bool IsFloorEscortPair(Transform first,Transform second)
    {
        if (floorEscortWorker==null || floorMissionGuest==null || floorMissionGuest.root==null) return false;
        Transform escort=floorEscortWorker.actor.person.root;
        return first==escort && second==floorMissionGuest.root || second==escort && first==floorMissionGuest.root;
    }

    private Vector3 FloorEscortMeetingPoint()
    {
        Vector3 position=worldRoot.InverseTransformPoint(floorMissionGuest.root.position);
        Vector3 meeting=position+new Vector3(-.5f,0f,-.3f);
        return FloorWalkable(meeting)?meeting:position;
    }

    private void BeginFloorGuestEscort()
    {
        if (floorEscortWorker!=null || floorMissionGuest==null) return;
        MotionActor selected=null;
        foreach (MotionActor actor in motionActors)
        {
            if (actor.person.root==null || !actor.person.root.gameObject.activeInHierarchy) continue;
            if (actor.person.name=="Server") { selected=actor; break; }
            if (actor.person.name=="Bartender") selected=actor;
        }
        if (selected==null) return;
        foreach (var worker in floorCleaningCrew) if (worker.actor==selected) { floorEscortWorker=worker; break; }
        if (floorEscortWorker==null) floorEscortWorker=new FloorWorkerActor
        {
            actor=selected,savedPosition=selected.person.root.localPosition,
            savedRotation=selected.person.root.localRotation
        };
        floorControlledRoots.Add(selected.person.root);
        floorEscortJoined=false; floorEscortReturning=false; floorEscortHomeBridgePending=false;
        floorEscortRoute.points.Clear(); floorEscortRoute.cursor=0; floorEscortRoute.bridge=false;
        Vector3 position=MotionPosition(selected);
        if (!FloorNavigation.IsWalkable(position))
        {
            floorEscortRoute.bridge=true;
            if (Mathf.Abs(position.z-FloorBarExit.z)>.04f)
                floorEscortRoute.points.Add(new Vector3(position.x,position.y,FloorBarExit.z));
            floorEscortRoute.points.Add(new Vector3(FloorBarExit.x,position.y,FloorBarExit.z));
        }
        else BuildFloorRoute(floorEscortRoute,selected.person.root,FloorEscortMeetingPoint());
    }

    private void FollowFloorGuest(Vector3 guestPosition,float delta)
    {
        Transform escortRoot=floorEscortWorker.actor.person.root;
        Vector3 position=MotionPosition(floorEscortWorker.actor);
        if (FlatDistance(position,guestPosition)<=.6f) return;
        if (FloorNavigation.SegmentClear(position,guestPosition)
            && FloorDirectPathClear(position,guestPosition,escortRoot))
        {
            floorEscortRoute.points.Clear(); floorEscortRoute.points.Add(guestPosition);
            floorEscortRoute.cursor=0; floorEscortRoute.goal=guestPosition; floorEscortRoute.bridge=false;
        }
        else if (floorEscortRoute.cursor>=floorEscortRoute.points.Count || (floorEscortRoute.goal-guestPosition).sqrMagnitude>.25f)
            BuildFloorRoute(floorEscortRoute,escortRoot,guestPosition);
        MoveFloorRoute(floorEscortRoute,escortRoot,floorGuestWalkSpeed+.6f,delta);
    }

    private bool FloorDirectPathClear(Vector3 start,Vector3 goal,Transform escortRoot)
    {
        Vector3 travel=goal-start; travel.y=0f;
        foreach (MotionActor other in motionActors)
        {
            if (other.person.root==null || other.person.root==escortRoot || IsFloorEscortPair(escortRoot,other.person.root)
                || other.kind==MotionKind.Band || !other.person.root.gameObject.activeInHierarchy) continue;
            Vector3 body=MotionPosition(other);
            if (Mathf.Abs(body.y-start.y)>1f) continue;
            Vector3 offset=body-start; offset.y=0f;
            if (offset.sqrMagnitude<.42f*.42f && Vector3.Dot(offset,travel)<=0f) continue;
            float along=travel.sqrMagnitude<=.000001f?0f:Mathf.Clamp01(Vector3.Dot(offset,travel)/travel.sqrMagnitude);
            if ((offset-travel*along).sqrMagnitude<.42f*.42f) return false;
        }
        return true;
    }

    private void PresentFloorEscort(Vector3 before,float delta)
    {
        if (floorEscortWorker==null) return;
        MotionActor actor=floorEscortWorker.actor;
        float speed=FlatDistance(before,MotionPosition(actor))/delta;
        actor.speed=speed; actor.moving=speed>.03f;
        if (actor.visual!=null) actor.visual.ApplyPresentation(floorActorSeconds,.4f,speed,.15f);
    }

    private void BeginFloorEscortReturn()
    {
        if (floorEscortWorker==null) return;
        floorEscortReturning=true;
        int cleanupIndex=floorCleaningCrew.IndexOf(floorEscortWorker);
        bool cleanup=cleanupIndex>=0 && FloorState!=null && FloorState.cleanupActive;
        Vector3 goal=cleanup?FloorWorkerGoal(cleanupIndex):floorEscortWorker.savedPosition;
        floorEscortHomeBridgePending=!cleanup && !FloorNavigation.IsWalkable(goal);
        floorEscortRoute.bridge=false;
        BuildFloorRoute(floorEscortRoute,floorEscortWorker.actor.person.root,floorEscortHomeBridgePending?FloorBarExit:goal);
    }

    private void TickFloorEscortReturn(float delta)
    {
        if (floorEscortWorker==null) { floorEscortReturning=false; return; }
        MotionActor actor=floorEscortWorker.actor;
        Vector3 before=MotionPosition(actor);
        MoveFloorRoute(floorEscortRoute,actor.person.root,2.5f,delta);
        PresentFloorEscort(before,delta);
        if (FlatDistance(MotionPosition(actor),floorEscortRoute.goal)>.2f) return;
        if (floorEscortHomeBridgePending)
        {
            floorEscortHomeBridgePending=false;
            floorEscortRoute.points.Clear(); floorEscortRoute.cursor=0; floorEscortRoute.bridge=true;
            floorEscortRoute.goal=floorEscortWorker.savedPosition;
            floorEscortRoute.points.Add(new Vector3(floorEscortWorker.savedPosition.x,0f,FloorBarExit.z));
            floorEscortRoute.points.Add(floorEscortWorker.savedPosition);
            return;
        }
        int index=floorCleaningCrew.IndexOf(floorEscortWorker);
        FloorWorkerActor returning=floorEscortWorker;
        floorEscortWorker=null; floorEscortReturning=false; floorEscortJoined=false;
        if (index>=0) PrepareFloorWorkerRoute(returning,index);
        else
        {
            floorControlledRoots.Remove(actor.person.root);
            actor.path.Clear(); actor.pathIndex=0; actor.state=MotionState.Idle;
            actor.speed=0f; actor.moving=false; actor.routeFailed=false; actor.wait=.5f;
        }
    }

    private void ClearFloorActors()
    {
        if (floorEscortWorker!=null && !floorCleaningCrew.Contains(floorEscortWorker))
        {
            MotionActor actor=floorEscortWorker.actor;
            if (actor!=null && actor.person.root!=null)
            {
                actor.person.root.localPosition=floorEscortWorker.savedPosition;
                actor.person.root.localRotation=floorEscortWorker.savedRotation;
                actor.path.Clear(); actor.pathIndex=0; actor.state=MotionState.Idle;
                actor.routeFailed=false; actor.moving=false; actor.speed=0f; actor.wait=1f;
            }
        }
        foreach (var worker in floorCleaningCrew)
        {
            if (worker.actor==null || worker.actor.person.root==null) continue;
            worker.actor.person.root.localPosition=worker.savedPosition;
            worker.actor.person.root.localRotation=worker.savedRotation;
            worker.actor.path.Clear(); worker.actor.pathIndex=0; worker.actor.state=MotionState.Idle;
            worker.actor.routeFailed=false; worker.actor.moving=false; worker.actor.speed=0f; worker.actor.wait=1f;
        }
        floorCleaningCrew.Clear();
        if (floorMissionGuest!=null)
        {
            if (floorMissionGuestActor!=null) motionActors.Remove(floorMissionGuestActor);
            motionLookup.Remove(floorMissionGuest);
            if (floorMissionGuest.root!=null) { floorMissionGuest.root.gameObject.SetActive(false); Destroy(floorMissionGuest.root.gameObject); }
        }
        floorControlledRoots.Clear(); floorMissionGuest=null; floorMissionGuestActor=null;
        floorGuestLeaving=floorGuestExited=false; floorActorSeconds=0f;
        floorEscortWorker=null; floorEscortJoined=floorEscortReturning=floorEscortHomeBridgePending=false;
        floorEscortRoute.points.Clear(); floorEscortRoute.cursor=0;
        floorGuestRoute.points.Clear(); floorGuestRoute.cursor=0;
        if (floorPerformance!=null) floorPerformance.SetCleanerPositions(Vector3.zero,Vector3.zero,0);
        motionOrdersDirty=true;
    }
}
