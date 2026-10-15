using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// Builds from the untouched baseline, exclusively in the separate expansion scene.
public static class BunkerExpansionBuilder
{
    const string BaseScene = "Assets/Bunker/Scenes/AbandonedBunker.unity";
    const string ExpandedScene = "Assets/Bunker/Scenes/AbandonedBunkerExpanded.unity";
    const string MatFolder = "Assets/Bunker/Expansion/Materials/";
    static Transform root, shell, roofs, props, lamps, markers;
    static Material concrete, paint, floor, metal, rust, yellow, black, glow, teal, amber, paper;
    static readonly List<string> checks = new List<string>();

    // Batch entry point. This command intentionally has no automatic editor startup hook.
    public static void Build()
    {
        Directory.CreateDirectory(MatFolder);
        EditorSceneManager.OpenScene(BaseScene, OpenSceneMode.Single);
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), ExpandedScene);
        root = Group("07 - EXPANSION", null);
        shell = Group("01 - Structure", root);
        roofs = Group("02 - Ceilings (hide for top view)", root);
        props = Group("03 - Equipment and cover", root);
        lamps = Group("04 - Lighting", root);
        markers = Group("05 - Agent integration markers", root);
        concrete = Existing("Rough concrete"); paint = Existing("Faded olive wall paint");
        floor = Existing("Dusty concrete floor"); metal = Existing("Old steel");
        rust = Existing("Rust"); yellow = Existing("Old safety yellow");
        black = Existing("Dark rubber and cracks"); paper = Existing("Old paper");
        teal = NewMat("Ventilation blue", new Color(.17f,.34f,.35f), .45f);
        amber = NewMat("Workshop ochre", new Color(.43f,.31f,.12f), .3f);
        glow = NewMat("Expansion fluorescent", new Color(.78f,.86f,.76f), 0);
        glow.EnableKeyword("_EMISSION"); glow.SetColor("_EmissionColor", new Color(.7f,.85f,.72f)*1.4f);
        MakeConnections();
        Acoustic(); Workshop(); Service(); Generator(); Staging();
        // Extend visibility for the new, larger footprint without changing the original scene.
        Camera.main.farClipPlane = 120;
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ExpandedScene, true), new EditorBuildSettingsScene(BaseScene, false) };
        AssetDatabase.SaveAssets();
        Validate(); CaptureAll();
        Debug.Log("BUNKER_EXPANSION_OK: original scene preserved; expansion saved, checked and photographed.");
    }

    static Transform Group(string name, Transform parent)
    { var go = new GameObject(name); go.transform.SetParent(parent, false); return go.transform; }
    static Material Existing(string name)
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>("Assets/Bunker/Materials/"+name+".mat");
        if (!m) throw new Exception("Missing baseline material: "+name); return m;
    }
    static Material NewMat(string name, Color color, float metallic)
    {
        string path=MatFolder+name+".mat";
        var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!m) { m=new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(m,path); }
        m.color=color; m.SetFloat("_Metallic",metallic); m.SetFloat("_Glossiness",.23f);
        EditorUtility.SetDirty(m); return m;
    }
    static GameObject Box(string name, Vector3 pos, Vector3 size, Material mat, Transform parent, bool collide=true)
    {
        var go=GameObject.CreatePrimitive(PrimitiveType.Cube); go.name=name;
        go.transform.SetParent(parent,false); go.transform.position=pos; go.transform.localScale=size;
        go.GetComponent<Renderer>().sharedMaterial=mat;
        if(!collide) UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }
    static void Rod(string name, Vector3 a, Vector3 b, float radius, Material mat, Transform parent)
    {
        var go=GameObject.CreatePrimitive(PrimitiveType.Cylinder);go.name=name;go.transform.SetParent(parent,false);
        go.transform.position=(a+b)/2;go.transform.localScale=new Vector3(radius*2,Vector3.Distance(a,b)/2,radius*2);
        go.transform.rotation=Quaternion.FromToRotation(Vector3.up,b-a);go.GetComponent<Renderer>().sharedMaterial=mat;
        UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
    }
    static void Slab(string name,float x0,float x1,float z0,float z1,float height=3.3f)
    {
        string path=MatFolder+name+" floor.mat";
        var tiled=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!tiled){tiled=new Material(floor);AssetDatabase.CreateAsset(tiled,path);}
        tiled.mainTextureScale=new Vector2((x1-x0)/2,(z1-z0)/2);EditorUtility.SetDirty(tiled);
        Box(name+" floor",new Vector3((x0+x1)/2,-.13f,(z0+z1)/2),new Vector3(x1-x0,.26f,z1-z0),tiled,shell);
        Box(name+" ceiling",new Vector3((x0+x1)/2,height+.12f,(z0+z1)/2),new Vector3(x1-x0,.24f,z1-z0),concrete,roofs);
    }
    // Axis X means a constant X wall, with intervals measured along Z.
    static void Wall(string name,bool axisX,float fixedAt,float start,float end,float height=3.3f,params float[] gaps)
    {
        float at=start;
        for(int i=0;i<=gaps.Length;i+=2)
        {
            float until=i<gaps.Length?gaps[i]:end;
            if(until>at+.001f) WallSegment(name,axisX,fixedAt,at,until,height);
            if(i<gaps.Length)
            {
                float a=gaps[i],b=gaps[i+1],mid=(a+b)/2;
                Vector3 pos=axisX?new Vector3(fixedAt,(2.5f+height)/2,mid):new Vector3(mid,(2.5f+height)/2,fixedAt);
                Vector3 size=axisX?new Vector3(.35f,height-2.5f,b-a):new Vector3(b-a,height-2.5f,.35f);
                Box(name+" doorway lintel",pos,size,concrete,shell);
                foreach(float edge in new[]{a,b})
                    Box(name+" steel jamb",axisX?new Vector3(fixedAt,1.25f,edge):new Vector3(edge,1.25f,fixedAt),axisX?new Vector3(.39f,2.5f,.08f):new Vector3(.08f,2.5f,.39f),metal,shell);
                at=b;
            }
        }
    }
    static void WallSegment(string name,bool axisX,float fixedAt,float a,float b,float height)
    {
        if(b-a>4.01f)
        {
            int pieces=Mathf.CeilToInt((b-a)/4f);
            for(int i=0;i<pieces;i++)WallSegment(name,axisX,fixedAt,Mathf.Lerp(a,b,i/(float)pieces),Mathf.Lerp(a,b,(i+1)/(float)pieces),height);
            return;
        }
        float mid=(a+b)/2;
        Box(name+" painted base",axisX?new Vector3(fixedAt,.58f,mid):new Vector3(mid,.58f,fixedAt),axisX?new Vector3(.35f,1.16f,b-a):new Vector3(b-a,1.16f,.35f),paint,shell);
        Box(name+" concrete",axisX?new Vector3(fixedAt,(height+1.16f)/2,mid):new Vector3(mid,(height+1.16f)/2,fixedAt),axisX?new Vector3(.35f,height-1.16f,b-a):new Vector3(b-a,height-1.16f,.35f),concrete,shell);
        Box(name+" paint edge",axisX?new Vector3(fixedAt,1.17f,mid):new Vector3(mid,1.17f,fixedAt),axisX?new Vector3(.365f,.04f,b-a):new Vector3(b-a,.04f,.365f),metal,props,false);
    }
    static void MakeConnections()
    {
        // Move the existing cabinet behind the new east doorway, in this scene only.
        foreach(var t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            if(t.name == "Electrical cabinet" || t.name == "Fuse module") t.position += Vector3.forward * 1.1f;
        foreach(var t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            if(t.name.StartsWith("Control west ")||t.name.StartsWith("Control east ")) UnityEngine.Object.DestroyImmediate(t.gameObject);
        Wall("Control west expansion access",true,-5,10,17,3.3f,12.5f,15.5f);
        Wall("Control east expansion access",true,5,10,17,3.3f,12.5f,15.5f);
        Slab("West link",-12,-5,12.5f,15.5f);Slab("East link",5,12,12.5f,15.5f);
        foreach(float z in new[]{12.5f,15.5f})
        { Wall("West link",false,z,-12,-5);Wall("East link",false,z,5,12); }
        Fixture(-8.5f,14,3.1f,false);Fixture(8.5f,14,3.1f,true);
        PipeRun(-11.7f,-5.3f,15.15f,2.8f,teal);PipeRun(5.3f,11.7f,15.15f,2.8f,rust);
        Marker("Control safe interaction area",BunkerAgentMarker.Kind.NoTrapZone,0,12,"Control","",2);
    }
    static void Acoustic()
    {
        Slab("Acoustic ventilation",-22,-12,10,20);
        Wall("Acoustic west",true,-22,10,20);Wall("Acoustic east",true,-12,10,20,3.3f,12.5f,15.5f);
        Wall("Acoustic south",false,10,-22,-12);Wall("Acoustic north",false,20,-22,-12,3.3f,-18.5f,-15.5f);
        // Full-height machinery provides real sightline breaks; low acoustic baffles leave traversable aisles.
        AirHandler(-20.5f,13);AirHandler(-20.5f,17.2f);
        Box("Acoustic baffle A",new Vector3(-16,1.05f,12.4f),new Vector3(2.8f,2.1f,.35f),teal,props);
        Box("Acoustic baffle B",new Vector3(-16,1.05f,16.6f),new Vector3(2.8f,2.1f,.35f),teal,props);
        for(int i=0;i<9;i++)
        {
            Box("Absorber rib A",new Vector3(-17.2f+i*.3f,1.05f,12.18f),new Vector3(.08f,1.8f,.09f),black,props,false);
            Box("Absorber rib B",new Vector3(-17.2f+i*.3f,1.05f,16.82f),new Vector3(.08f,1.8f,.09f),black,props,false);
        }
        Box("Overhead ventilation trunk",new Vector3(-20.5f,2.97f,15),new Vector3(.8f,.4f,9),metal,props,false);
        foreach(float z in new[]{11.5f,14.5f,18.5f}) Box("Duct suspension",new Vector3(-20.5f,3.18f,z),new Vector3(1.05f,.1f,.1f),rust,props,false);
        FloorBand(-18.1f,15, .9f,7.4f,teal);Fixture(-17.5f,14.5f,3.1f,false,true);Fixture(-14,18.5f,3.1f,false);
        Sign("VENTILATION / 05",new Vector3(-16.8f,2.35f,10.22f),180,teal);
        Marker("Fan noise source",BunkerAgentMarker.Kind.SoundSource,-20.5f,13,"Acoustic");
        Marker("Sheltered listening point",BunkerAgentMarker.Kind.HidePoint,-18.6f,12.8f,"Acoustic");
        Marker("Patrol west aisle",BunkerAgentMarker.Kind.PatrolPoint,-18.1f,15,"Acoustic");
        Marker("Patrol rear aisle",BunkerAgentMarker.Kind.PatrolPoint,-17,18.7f,"Acoustic");
        Marker("Sound investigation position",BunkerAgentMarker.Kind.PatrolPoint,-14,14.5f,"Acoustic");
    }
    static void AirHandler(float x,float z)
    {
        Box("Ventilation equipment plinth",new Vector3(x,.15f,z),new Vector3(2,.3f,2.5f),concrete,props);
        Box("Air handling unit",new Vector3(x,1.3f,z),new Vector3(1.6f,2f,2.1f),teal,props);
        Box("Fan grille backing",new Vector3(x+.82f,1.35f,z),new Vector3(.06f,1.35f,1.5f),black,props,false);
        for(int i=0;i<8;i++) Box("Fan grille slat",new Vector3(x+.87f,.79f+i*.16f,z),new Vector3(.05f,.035f,1.45f),metal,props,false);
        Rod("Fan service pipe",new Vector3(x,2.3f,z),new Vector3(x,3.05f,z),.12f,rust,props);
    }
    static void Workshop()
    {
        Slab("Maintenance workshop",12,24,8,20);
        Wall("Workshop west",true,12,8,20,3.3f,12.5f,15.5f);Wall("Workshop east",true,24,8,20);
        Wall("Workshop south",false,8,12,24);Wall("Workshop north",false,20,12,24,3.3f,16.5f,19.5f);
        Box("Central machine base",new Vector3(18,.17f,13),new Vector3(3.5f,.34f,5.8f),metal,props);
        foreach(float z in new[]{11.5f,14.5f})
        {
            Box("Machine housing",new Vector3(18,1.2f,z),new Vector3(3.1f,1.9f,2.5f),amber,props);
            Box("Machine access panel",new Vector3(16.42f,1.25f,z),new Vector3(.07f,1.35f,1.7f),metal,props,false);
            for(int i=0;i<5;i++) Box("Machine cooling vent",new Vector3(16.37f,1.35f+i*.1f,z),new Vector3(.03f,.035f,1.2f),black,props,false);
            Rod("Machine exhaust",new Vector3(18,2.15f,z),new Vector3(18,3.15f,z),.12f,rust,props);
        }
        Bench("Repair workbench",22.9f,11.4f,.85f,3.8f);
        Box("Tool board",new Vector3(23.77f,1.8f,11.4f),new Vector3(.08f,1.15f,3.6f),woodMaterial(),props,false);
        for(int i=0;i<6;i++) Rod("Hanging tool",new Vector3(23.68f,1.45f,10.2f+i*.45f),new Vector3(23.68f,1.95f,10.2f+i*.45f),.025f,metal,props);
        Bench("Fuse inspection bench",14.6f,8.8f,2.6f,.75f);
        Box("Objective fuse housing (visual placeholder)",new Vector3(14.6f,1.05f,8.8f),new Vector3(.5f,.35f,.35f),teal,props,false);
        FloorBand(14.1f,16, .065f,6,yellow);FloorBand(21.4f,13.5f,.065f,9,yellow);
        FloorBand(18,9.2f,6.8f,.065f,yellow);FloorBand(18,18,7.2f,.065f,yellow);
        Fixture(14.3f,11.5f,3.1f,true);Fixture(14.3f,17.5f,3.1f,true,true);Fixture(21.3f,15.5f,3.1f,true);
        Sign("MAINTENANCE / 06",new Vector3(18,2.4f,8.22f),180,amber);
        foreach(Vector2 p in new[]{new Vector2(14.2f,14),new Vector2(14.2f,17.5f)}) Marker("Shortcut sample",BunkerAgentMarker.Kind.RouteSample,p.x,p.y,"Workshop","Shortcut");
        foreach(Vector2 p in new[]{new Vector2(14.2f,9.7f),new Vector2(21.5f,9.7f),new Vector2(21.5f,17.5f)}) Marker("Bypass sample",BunkerAgentMarker.Kind.RouteSample,p.x,p.y,"Workshop","Bypass");
        foreach(Vector2 p in new[]{new Vector2(14.3f,16),new Vector2(15,18.2f),new Vector2(17,9.2f),new Vector2(21.4f,13.7f),new Vector2(21.2f,17.5f),new Vector2(18,18.2f)})
            Marker("Trap candidate",BunkerAgentMarker.Kind.TrapCandidate,p.x,p.y,"Workshop");
        Marker("Workshop entrance keep clear",BunkerAgentMarker.Kind.NoTrapZone,12.5f,14,"Workshop","",1.5f);
        Marker("Workshop exit keep clear",BunkerAgentMarker.Kind.NoTrapZone,18,20,"Workshop","",1.5f);
        Marker("Fuse interaction keep clear",BunkerAgentMarker.Kind.NoTrapZone,14.6f,9.8f,"Workshop","",.8f);
    }
    static Material woodMaterial() { return Existing("Supply crate timber"); }
    static void Bench(string name,float x,float z,float width,float depth)
    {
        Box(name+" top",new Vector3(x,.86f,z),new Vector3(width,.12f,depth),woodMaterial(),props);
        foreach(float dx in new[]{-width/2+.1f,width/2-.1f}) foreach(float dz in new[]{-depth/2+.1f,depth/2-.1f})
            Box(name+" leg",new Vector3(x+dx,.4f,z+dz),new Vector3(.08f,.8f,.08f),metal,props);
    }
    static void Service()
    {
        Slab("West return link",-18.5f,-15.5f,20,24);Slab("East return link",16.5f,19.5f,20,24);
        foreach(float x in new[]{-18.5f,-15.5f,16.5f,19.5f})Wall("Return passage",true,x,20,24);
        Slab("Rear service corridor",-19,32,24,28);
        Wall("Service south",false,24,-19,32,3.3f,-18.5f,-15.5f,16.5f,19.5f,26.5f,29.5f);
        Wall("Service north",false,28,-19,32,3.3f,-1.75f,1.75f);
        Wall("Service west end",true,-19,24,28);Wall("Service east end",true,32,24,28);
        foreach(float x in new[]{-16f,-8f,0f,8f,16f,24f,30f})
        {
            Fixture(x,26,3.1f,x>0);
            Box("Service overhead rib",new Vector3(x,3.08f,26),new Vector3(.22f,.32f,3.7f),concrete,roofs,false);
        }
        PipeRun(-18.4f,31.4f,27.62f,2.7f,rust);PipeRun(-18.4f,31.4f,27.62f,2.95f,teal);
        foreach(float x in new[]{-14f,-6f,6f,14f,22f,30f})FloorBand(x,24.5f,2,.06f,yellow);
        Fixture(-17,22,3.1f,false);Fixture(18,22,3.1f,true);
        Marker("West reinforcement entry",BunkerAgentMarker.Kind.SpawnCandidate,-17,22,"Service","West");
        Marker("East reinforcement entry",BunkerAgentMarker.Kind.SpawnCandidate,18,22,"Service","East");
        Marker("Generator threshold clear",BunkerAgentMarker.Kind.NoTrapZone,0,28,"Service","",2);
    }
    static void Generator()
    {
        Slab("Generator hall",-8,8,28,46,5);
        Wall("Generator west",true,-8,28,46,5);Wall("Generator east",true,8,28,46,5);
        Wall("Generator south",false,28,-8,8,5,-1.75f,1.75f);Wall("Generator north",false,46,-8,8,5);
        foreach(float x in new[]{-3.5f,3.5f})
        {
            Box("Generator concrete plinth",new Vector3(x,.15f,36),new Vector3(2.5f,.3f,5),concrete,props);
            Box("Generator skid",new Vector3(x,.4f,36),new Vector3(2.2f,.2f,4.7f),metal,props);
            Box("Generator engine",new Vector3(x,1.2f,36),new Vector3(1.9f,1.4f,3.7f),teal,props);
            for(int i=0;i<8;i++)Box("Engine cooling rib",new Vector3(x,1.3f,34.5f+i*.42f),new Vector3(2,.65f,.09f),metal,props,false);
            Rod("Generator exhaust riser",new Vector3(x,1.9f,37),new Vector3(x,4.65f,37),.15f,rust,props);
            Rod("Generator exhaust header",new Vector3(x,4.65f,37),new Vector3(x,4.65f,45.7f),.15f,rust,props);
            FloorBand(x,33.25f,2.8f,.08f,yellow);FloorBand(x,38.75f,2.8f,.08f,yellow);
        }
        foreach(float x in new[]{-5.8f,5.8f})
        {
            Box("Boss flank cover",new Vector3(x,.65f,31.3f),new Vector3(1.8f,1.3f,1),metal,props);
            Box("Rear switchboard",new Vector3(x,1.25f,44.8f),new Vector3(2,2.5f,.7f),teal,props);
            for(int i=0;i<3;i++) Box("Switchboard panel",new Vector3(x, .6f+i*.65f,44.41f),new Vector3(1.7f,.5f,.04f),black,props,false);
            Marker("Boss flank waypoint",BunkerAgentMarker.Kind.BossCover,x,35,"Generator");
        }
        Bench("Generator control console",0,44.6f,3.8f,.85f);
        for(int i=0;i<3;i++) Box("Console instrument",new Vector3(-1.15f+i*1.15f,1.02f,44.6f),new Vector3(.8f,.2f,.5f),black,props,false);
        foreach(float z in new[]{30.5f,37f,43.5f})
        {
            Box("Generator roof beam",new Vector3(0,4.65f,z),new Vector3(15.7f,.45f,.3f),metal,roofs,false);
            Fixture(-5,z,4.35f,false,z==37);Fixture(5,z,4.35f,true);
        }
        Sign("GENERATOR HALL / 07",new Vector3(0,2.7f,45.78f),0,teal);
        Marker("Boss initial position",BunkerAgentMarker.Kind.PatrolPoint,0,38,"Generator");
        Marker("Boss retreat position",BunkerAgentMarker.Kind.BossRetreat,0,42,"Generator");
        Marker("Boss melee approach",BunkerAgentMarker.Kind.PatrolPoint,0,32,"Generator");
    }
    static void Staging()
    {
        Slab("Service staging",24,32,16,24);
        Wall("Staging west",true,24,16,24);Wall("Staging east",true,32,16,24);
        Wall("Staging south",false,16,24,32);Wall("Staging north",false,24,24,32,3.3f,26.5f,29.5f);
        // The offset screen prevents a direct view from the corridor into the staging pocket.
        Box("Staging sightline screen",new Vector3(28,1.2f,20.9f),new Vector3(3.5f,2.4f,.35f),metal,props);
        foreach(float z in new[]{17.2f,18.1f,19})Place("Steel locker",new Vector3(31.25f,0,z),90);
        foreach(float x in new[]{25.1f,26.2f})Place("Supply crate",new Vector3(x,0,17.1f));
        Place("Supply crate",new Vector3(25.1f,.86f,17.1f));
        Bench("Dispatch bench",29,16.8f,2.4f,.7f);
        Fixture(28,18.2f,3.1f,true,true);Fixture(28,22.5f,3.1f,false);
        Sign("SERVICE STAGING / 08",new Vector3(28,2.55f,16.22f),180,amber);
        Marker("Concealed staging spawn A",BunkerAgentMarker.Kind.SpawnCandidate,27,18.5f,"Staging","A");
        Marker("Concealed staging spawn B",BunkerAgentMarker.Kind.SpawnCandidate,29,18.5f,"Staging","B");
        Marker("Staging exit path",BunkerAgentMarker.Kind.PatrolPoint,30.4f,22,"Staging");
    }
    static void Place(string name,Vector3 pos,float yaw=0)
    {
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Bunker/Prefabs/"+name+".prefab");
        if(!prefab)throw new Exception("Missing prefab "+name);
        var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab);go.transform.SetParent(props);go.transform.position=pos;go.transform.rotation=Quaternion.Euler(0,yaw,0);
    }
    static void FloorBand(float x,float z,float width,float depth,Material mat)
    {Box("Floor route marking",new Vector3(x,.008f,z),new Vector3(width,.006f,depth),mat,props,false);}
    static void PipeRun(float x0,float x1,float z,float y,Material mat)
    {
        Rod("Overhead service pipe",new Vector3(x0,y,z),new Vector3(x1,y,z),.07f,mat,props);
        for(float x=x0+.4f;x<x1;x+=3)Rod("Pipe support",new Vector3(x,y,z),new Vector3(x,3.28f,z),.025f,metal,props);
    }
    static void Fixture(float x,float z,float y,bool warm,bool shadow=false)
    {
        Box("Expansion lamp housing",new Vector3(x,y,z),new Vector3(1.25f,.14f,.35f),metal,lamps,false);
        Box("Expansion lamp diffuser",new Vector3(x,y-.085f,z),new Vector3(1.1f,.035f,.22f),glow,lamps,false);
        var t=Group("Expansion fixture light",lamps);t.position=new Vector3(x,y-.3f,z);
        var l=t.gameObject.AddComponent<Light>();l.type=LightType.Point;l.color=warm?new Color(1,.79f,.53f):new Color(.72f,.88f,.9f);
        l.intensity=1.9f;l.range=y>4?10:7;l.shadows=shadow?LightShadows.Soft:LightShadows.None;l.shadowStrength=.65f;
    }
    static void Marker(string name,BunkerAgentMarker.Kind kind,float x,float z,string zone,string route="",float radius=.5f)
    {
        var t=Group(name,markers);t.position=new Vector3(x,0,z);var m=t.gameObject.AddComponent<BunkerAgentMarker>();m.kind=kind;m.zone=zone;m.route=route;m.radius=radius;
    }
    static void Sign(string text,Vector3 pos,float yaw,Material backing)
    {
        var t=Group("Wall sign - "+text,props);t.position=pos;t.rotation=Quaternion.Euler(0,yaw,0);
        var plate=Box("Sign backing",Vector3.zero,new Vector3(3.3f,.4f,.035f),backing,t,false);plate.transform.localPosition=Vector3.zero;plate.transform.localRotation=Quaternion.identity;
        var label=Group("Depth-tested lettering",t);label.localPosition=new Vector3(0,0,-.024f);
        var tm=label.gameObject.AddComponent<TextMesh>();tm.text=text;tm.fontSize=64;tm.characterSize=.043f;tm.anchor=TextAnchor.MiddleCenter;tm.alignment=TextAlignment.Center;tm.color=new Color(.88f,.87f,.72f);
        var renderer=tm.GetComponent<MeshRenderer>();
        string path=MatFolder+"Sign lettering.mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!mat){mat=new Material(Shader.Find("Bunker/DepthText"));mat.mainTexture=tm.font.material.mainTexture;AssetDatabase.CreateAsset(mat,path);}
        renderer.sharedMaterial=mat;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
    }
    static void CheckPath(string label,params Vector2[] points)
    {
        for(int i=0;i<points.Length-1;i++)
        {
            float distance=Vector2.Distance(points[i],points[i+1]);int steps=Mathf.CeilToInt(distance/.2f);
            for(int j=0;j<=steps;j++)
            {
                Vector2 p=Vector2.Lerp(points[i],points[i+1],j/(float)Math.Max(1,steps));
                var hits=Physics.OverlapCapsule(new Vector3(p.x,.34f,p.y),new Vector3(p.x,1.48f,p.y),.29f,~0,QueryTriggerInteraction.Ignore);
                hits=hits.Where(c=>!(c is CharacterController)).ToArray();
                if(hits.Length>0)throw new Exception(label+" obstructed at "+p+": "+string.Join(",",hits.Select(c=>c.name)));
                if(!Physics.Raycast(new Vector3(p.x,.2f,p.y),Vector3.down,.5f))throw new Exception(label+" missing floor at "+p);
            }
        }
        checks.Add("PASS "+label);
    }
    public static void Validate()
    {
        checks.Clear();Physics.SyncTransforms();
        foreach(var t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            if(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)>0)throw new Exception("Missing script: "+t.name);
        foreach(var r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            foreach(var m in r.sharedMaterials)if(!m||!m.shader||!m.shader.isSupported)throw new Exception("Invalid material: "+r.name);
        checks.Add("PASS scripts and materials");
        CheckPath("Original entrance to Control",new Vector2(0,-8),new Vector2(0,11.4f));
        CheckPath("Control to Acoustic",new Vector2(0,11.4f),new Vector2(-2.5f,11.4f),new Vector2(-2.5f,14),new Vector2(-14,14));
        CheckPath("Acoustic return",new Vector2(-14,14),new Vector2(-18.1f,14),new Vector2(-18.1f,18.8f),new Vector2(-17,18.8f),new Vector2(-17,26));
        CheckPath("Control to Workshop",new Vector2(0,11.4f),new Vector2(3.4f,11.4f),new Vector2(3.4f,14),new Vector2(14.2f,14));
        CheckPath("Workshop shortcut",new Vector2(14.2f,14),new Vector2(14.2f,18),new Vector2(18,18),new Vector2(18,26));
        CheckPath("Workshop bypass",new Vector2(14.2f,14),new Vector2(13.2f,14),new Vector2(13.2f,9.6f),new Vector2(21.5f,9.6f),new Vector2(21.5f,18),new Vector2(18,18));
        CheckPath("Complete rear corridor",new Vector2(-17,26),new Vector2(30.5f,26));
        CheckPath("Generator approach and retreat",new Vector2(0,26),new Vector2(0,42));
        CheckPath("Generator west flank",new Vector2(0,30),new Vector2(-5.5f,30),new Vector2(-5.5f,29.5f),new Vector2(-7,29.5f),new Vector2(-7,40),new Vector2(0,40));
        CheckPath("Generator east flank",new Vector2(0,30),new Vector2(5.5f,30),new Vector2(7,30),new Vector2(7,40),new Vector2(0,40));
        CheckPath("Staging concealed access",new Vector2(28,26),new Vector2(28,22.5f),new Vector2(30.4f,22.5f),new Vector2(30.4f,19.5f),new Vector2(28,19.5f),new Vector2(28,18.5f));
        foreach(var m in UnityEngine.Object.FindObjectsByType<BunkerAgentMarker>(FindObjectsSortMode.None))
        {
            if(m.kind==BunkerAgentMarker.Kind.NoTrapZone||m.kind==BunkerAgentMarker.Kind.SoundSource)continue;
            var p=m.transform.position;
            var hits=Physics.OverlapCapsule(p+Vector3.up*.34f,p+Vector3.up*1.48f,.29f,~0,QueryTriggerInteraction.Ignore).Where(c=>!(c is CharacterController)).ToArray();
            if(hits.Length>0)throw new Exception("Marker obstructed: "+m.name+" / "+string.Join(",",hits.Select(c=>c.name)));
        }
        checks.Add("PASS traversable agent marker positions");
        string output=Path.GetFullPath(Path.Combine(Application.dataPath,"../../Validation.txt"));
        File.WriteAllLines(output,checks);Debug.Log(string.Join("\n",checks));
    }
    public static void CaptureAll()
    {
        string output=Path.GetFullPath(Path.Combine(Application.dataPath,"../../Previews"));Directory.CreateDirectory(output);
        Capture(output+"/01-Acoustic.png",new Vector3(-12.8f,1.85f,14),new Vector3(-19,1.35f,15.5f));
        Capture(output+"/02-Workshop.png",new Vector3(12.8f,1.85f,18.5f),new Vector3(18.6f,1.2f,12.7f));
        Capture(output+"/03-Rear-Service.png",new Vector3(-17.7f,1.7f,25.2f),new Vector3(15,1.6f,26));
        Capture(output+"/04-Generator.png",new Vector3(0,2.1f,29.5f),new Vector3(0,1.8f,39));
        Capture(output+"/05-Staging.png",new Vector3(30.3f,1.85f,20),new Vector3(27.5f,1.2f,17.3f));
        Capture(output+"/06-Control-Connections.png",new Vector3(2.7f,1.8f,11.5f),new Vector3(-5,1.5f,14));
        Capture(output+"/07-Control-East.png",new Vector3(-2.7f,1.8f,11.5f),new Vector3(5,1.5f,14));
        var roofObjects=UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Where(t=>t.name.Contains("Ceilings (hide for top view)")).Select(t=>t.gameObject).ToArray();
        foreach(var r in roofObjects)r.SetActive(false);
        bool fog=RenderSettings.fog;Color ambient=RenderSettings.ambientLight;
        RenderSettings.fog=false;RenderSettings.ambientLight=new Color(.6f,.6f,.6f);
        Capture(output+"/00-Expanded-Layout.png",new Vector3(5,65,18),new Vector3(5,0,18),true);
        RenderSettings.fog=fog;RenderSettings.ambientLight=ambient;
        foreach(var r in roofObjects)r.SetActive(true);
    }
    static void Capture(string path,Vector3 from,Vector3 target,bool top=false)
    {
        var go=new GameObject("Temporary review camera");var cam=go.AddComponent<Camera>();go.transform.position=from;go.transform.LookAt(target,top?Vector3.forward:Vector3.up);
        cam.fieldOfView=75;cam.nearClipPlane=.05f;cam.farClipPlane=150;cam.backgroundColor=new Color(.07f,.085f,.085f);cam.clearFlags=CameraClearFlags.SolidColor;
        if(top){cam.orthographic=true;cam.orthographicSize=31;}
        var rt=new RenderTexture(1600,1100,24);rt.antiAliasing=4;cam.targetTexture=rt;cam.Render();
        var previous=RenderTexture.active;RenderTexture.active=rt;var tex=new Texture2D(1600,1100,TextureFormat.RGB24,false);
        tex.ReadPixels(new Rect(0,0,1600,1100),0,0);tex.Apply();File.WriteAllBytes(path,tex.EncodeToPNG());
        RenderTexture.active=previous;cam.targetTexture=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(tex);UnityEngine.Object.DestroyImmediate(go);
    }
}
