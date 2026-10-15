using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;

// All geometry is built from primitives, then saved as normal editable scene objects.
// Rebuilding is optional: the finished scene is included with the project.
public static class BunkerSceneBuilder
{
    const string Root = "Assets/Bunker/";
    static Material concrete, paint, floor, metal, rust, wood, canvas, paper, black, yellow, glow, damp;
    static Transform architecture, props, details, lighting, ceilings;
    static GameObject cratePrefab, barrelPrefab, lockerPrefab, bunkPrefab;
    static System.Random random;

    [MenuItem("Bunker/Rebuild scene (replaces current scene)")]
    public static void Build()
    {
        random = new System.Random(41);
        foreach (string dir in new[] { "Scenes", "Materials", "Textures", "Prefabs", "Meshes" })
            Directory.CreateDirectory(Root + dir);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        MakeMaterials();
        architecture = Group("01 - Concrete structure");
        ceilings = Group("02 - Ceilings (hide for top view)");
        props = Group("03 - Furniture and supplies");
        details = Group("04 - Pipes and wear");
        lighting = Group("05 - Lighting");
        MakePrefabs();
        MakeStructure();
        Furnish();
        AddDetails();
        AddLights();
        AddPlayer();
        ConfigureProject();
        string path = Root + "Scenes/AbandonedBunker.unity";
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), path);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(path, true) };
        AssetDatabase.SaveAssets();
        ValidateScene();
        CapturePreviews();
        Debug.Log("BUNKER_BUILD_OK: saved editable scene, prefabs, materials and previews.");
    }

    static Transform Group(string name, Transform parent = null)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    static GameObject Shape(string name, PrimitiveType shape, Vector3 pos, Vector3 size, Material mat, Transform parent, bool collide = true)
    {
        GameObject go = GameObject.CreatePrimitive(shape);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        if (!collide) UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }

    static GameObject Box(string name, Vector3 pos, Vector3 size, Material mat, Transform parent, bool collide = true)
    { return Shape(name, PrimitiveType.Cube, pos, size, mat, parent, collide); }

    static void Rod(string name, Vector3 a, Vector3 b, float radius, Material mat, Transform parent, bool collide = false)
    {
        var go = Shape(name, PrimitiveType.Cylinder, (a + b) / 2, new Vector3(radius * 2, Vector3.Distance(a, b) / 2, radius * 2), mat, parent, collide);
        go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, b - a);
    }

    static Material Mat(string name, Color color, float metallic = 0, float smoothness = 0.15f, bool texture = false)
    {
        string path = Root + "Materials/" + name + ".mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null) { mat = new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(mat, path); }
        mat.color = color;
        mat.SetFloat("_Metallic", metallic);
        mat.SetFloat("_Glossiness", smoothness);
        if (texture)
        {
            var tex = new Texture2D(256, 256, TextureFormat.RGB24, true);
            for (int y = 0; y < 256; y++) for (int x = 0; x < 256; x++)
            {
                float n = Mathf.PerlinNoise(x / 25f, y / 25f) * 0.18f + Mathf.PerlinNoise(x / 5f, y / 5f) * 0.08f;
                float grain = (float)random.NextDouble() * 0.12f;
                float v = 0.67f + n + grain;
                tex.SetPixel(x, y, new Color(v, v, v));
            }
            tex.Apply();
            string texPath = Root + "Textures/" + name + ".png";
            File.WriteAllBytes(texPath, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(texPath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(texPath);
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.maxTextureSize = 256;
            importer.SaveAndReimport();
            mat.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            mat.mainTextureScale = new Vector2(3, 3);
        }
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static void MakeMaterials()
    {
        concrete = Mat("Rough concrete", new Color(.48f, .49f, .44f), 0, .08f, true);
        paint = Mat("Faded olive wall paint", new Color(.24f, .29f, .23f), 0, .1f, true);
        floor = Mat("Dusty concrete floor", new Color(.33f, .34f, .30f), 0, .1f, true);
        metal = Mat("Old steel", new Color(.19f, .23f, .21f), .5f, .25f, true);
        rust = Mat("Rust", new Color(.36f, .18f, .095f), .25f, .12f, true);
        wood = Mat("Supply crate timber", new Color(.37f, .29f, .17f), 0, .12f, true);
        canvas = Mat("Faded canvas mattress", new Color(.28f, .31f, .20f), 0, .05f, true);
        paper = Mat("Old paper", new Color(.65f, .61f, .43f), 0, .05f);
        black = Mat("Dark rubber and cracks", new Color(.07f, .083f, .073f));
        yellow = Mat("Old safety yellow", new Color(.66f, .48f, .15f));
        glow = Mat("Fluorescent glass", new Color(.72f, .80f, .65f));
        glow.EnableKeyword("_EMISSION");
        glow.SetColor("_EmissionColor", new Color(.7f, .85f, .63f) * 1.5f);
        damp = Mat("Damp patches", new Color(.20f, .23f, .21f), .1f, .65f);
    }

    static void WallPiece(string name, Vector3 center, Vector3 size)
    {
        Box(name + " / painted base", new Vector3(center.x, .58f, center.z), new Vector3(size.x, 1.16f, size.z), paint, architecture);
        Box(name + " / concrete", new Vector3(center.x, 2.23f, center.z), new Vector3(size.x, 2.14f, size.z), concrete, architecture);
        Box(name + " / paint edge", new Vector3(center.x, 1.17f, center.z), new Vector3(size.x + .012f, .045f, size.z + .012f), metal, details, false);
    }

    static void WallX(string name, float x, float a, float b, float doorway = -100)
    {
        if (doorway < -90) WallPiece(name, new Vector3(x, 0, (a+b)/2), new Vector3(.35f, 3.3f, b-a));
        else
        {
            WallX(name + " A", x, a, doorway-.95f);
            WallX(name + " B", x, doorway+.95f, b);
            Box(name + " lintel", new Vector3(x, 2.88f, doorway), new Vector3(.35f,.84f,1.9f), concrete, architecture);
            foreach (float z in new[] { doorway-1.01f, doorway+1.01f })
                Box(name+" frame",new Vector3(x,1.23f,z),new Vector3(.44f,2.46f,.12f),metal,architecture);
            Box(name+" frame header",new Vector3(x,2.46f,doorway),new Vector3(.44f,.12f,2.14f),metal,architecture);
        }
    }

    static void WallZ(string name, float z, float a, float b, bool doorway = false)
    {
        if (!doorway) WallPiece(name, new Vector3((a+b)/2,0,z), new Vector3(b-a,3.3f,.35f));
        else
        {
            WallZ(name+" A", z, a, -1.25f);
            WallZ(name+" B", z, 1.25f, b);
            Box(name+" lintel", new Vector3(0,2.9f,z), new Vector3(2.5f,.8f,.35f),concrete,architecture);
        }
    }

    static void Slabs(string name, float x, float z, float width, float depth)
    {
        Box(name + " floor", new Vector3(x,-.13f,z), new Vector3(width,.26f,depth),floor,architecture);
        Box(name + " ceiling", new Vector3(x,3.42f,z), new Vector3(width,.24f,depth),concrete,ceilings);
    }

    static void MakeStructure()
    {
        Slabs("Main corridor",0,0,4.35f,20.35f);
        Slabs("Barracks",-5.6f,4,7.2f,8.35f);
        Slabs("Storage",5.6f,4,7.2f,8.35f);
        Slabs("Control room",0,13.6f,10.35f,7.2f);
        foreach (float x in new[] {-2f,2f})
        {
            WallX("Entry corridor",x,-10,0);
            WallX(x < 0 ? "Barracks partition" : "Storage partition",x,0,8,3);
            WallX("Rear corridor",x,8,10);
        }
        WallZ("Entrance wall",-10,-2,2);
        WallX("Barracks outer wall",-9,0,8);
        WallX("Storage outer wall",9,0,8);
        WallZ("Barracks south wall",0,-9,-2);
        WallZ("Barracks north wall",8,-9,-2);
        WallZ("Storage south wall",0,2,9);
        WallZ("Storage north wall",8,2,9);
        WallZ("Control room front",10,-5,5,true);
        WallZ("Control room rear",17,-5,5);
        WallX("Control west wall",-5,10,17);
        WallX("Control east wall",5,10,17);
        foreach (float z in new[] {-9f,-5f,-1f,7f,9.8f})
        {
            foreach (float x in new[] {-1.78f,1.78f})
                Box("Corridor concrete rib",new Vector3(x,1.57f,z),new Vector3(.25f,3.14f,.34f),concrete,architecture);
            Box("Corridor overhead beam",new Vector3(0,3.1f,z),new Vector3(3.8f,.38f,.34f),concrete,ceilings);
        }
        // Sealed entrance, decorative rather than a level exit.
        Box("Sealed blast door",new Vector3(0,1.36f,-9.78f),new Vector3(2.5f,2.72f,.16f),metal,props);
        foreach (float x in new[]{-1.05f,0,1.05f})
            Box("Blast door reinforcing bar",new Vector3(x,1.36f,-9.66f),new Vector3(.10f,2.55f,.09f),rust,props);
        Wheel("Entrance wheel",new Vector3(.55f,1.35f,-9.48f),.24f,0,props);
        // The storage door is openable. The pivot is at the edge of its panel.
        Transform hinge = Group("Storage door - press E",props);
        hinge.position = new Vector3(2.02f,0,3.9f);
        hinge.rotation = Quaternion.Euler(0,90,0);
        Box("Steel door panel",new Vector3(.86f,1.17f,0),new Vector3(1.72f,2.34f,.10f),metal,hinge);
        Box("Door handle",new Vector3(1.45f,1.1f,-.1f),new Vector3(.08f,.25f,.09f),rust,hinge,false);
        BunkerDoor door = hinge.gameObject.AddComponent<BunkerDoor>();
        door.isOpen = false;
        door.openAngle = -105;
        // Corridor floor joints and faded edge markings.
        for(float z=-9;z<10;z+=2.5f)
            Box("Floor expansion joint",new Vector3(0,.002f,z),new Vector3(3.5f,.003f,.018f),black,details,false);
        for(float z=-8;z<9;z+=1.3f) foreach(float x in new[]{-1.5f,1.5f})
            Box("Worn floor edge stripe",new Vector3(x,.004f,z),new Vector3(.07f,.005f,.8f),yellow,details,false);
    }

    static void MakePrefabs()
    {
        var crate = Group("Supply crate");
        Box("Wooden box",new Vector3(0,.43f,0),new Vector3(.95f,.86f,.85f),wood,crate);
        foreach(float x in new[]{-.34f,.34f})
            Box("Steel strap",new Vector3(x,.436f,0),new Vector3(.055f,.884f,.874f),metal,crate,false);
        foreach(float y in new[]{.1f,.76f}) foreach(float z in new[]{-.443f,.443f})
            Box("Timber batten",new Vector3(0,y,z),new Vector3(.98f,.085f,.045f),wood,crate,false);
        cratePrefab = SavePrefab(crate);

        var barrel = Group("Rusty drum");
        Shape("Steel drum",PrimitiveType.Cylinder,new Vector3(0,.48f,0),new Vector3(.64f,.48f,.64f),rust,barrel);
        foreach(float y in new[]{.06f,.31f,.66f,.91f})
            Shape("Drum hoop",PrimitiveType.Cylinder,new Vector3(0,y,0),new Vector3(.665f,.025f,.665f),metal,barrel,false);
        Shape("Filler cap",PrimitiveType.Cylinder,new Vector3(.15f,.97f,0),new Vector3(.09f,.012f,.09f),black,barrel,false);
        barrelPrefab = SavePrefab(barrel);

        var locker = Group("Steel locker");
        Box("Locker body",new Vector3(0,.98f,0),new Vector3(.67f,1.96f,.54f),metal,locker);
        Box("Door inset",new Vector3(0,1.02f,-.28f),new Vector3(.57f,1.8f,.025f),paint,locker,false);
        for(int i=0;i<5;i++) Box("Vent slot",new Vector3(0,1.6f-i*.055f,-.30f),new Vector3(.34f,.018f,.014f),black,locker,false);
        Box("Handle",new Vector3(.20f,.98f,-.33f),new Vector3(.034f,.19f,.06f),rust,locker,false);
        lockerPrefab = SavePrefab(locker);

        var bunk = Group("Army bunk bed");
        foreach(float x in new[]{-.48f,.48f}) foreach(float z in new[]{-1f,1f})
            Rod("Bed frame post",new Vector3(x,.06f,z),new Vector3(x,1.92f,z),.035f,metal,bunk,true);
        foreach(float y in new[]{.42f,1.42f})
        {
            Box("Bed base",new Vector3(0,y,0),new Vector3(1.03f,.10f,2.06f),metal,bunk);
            Box("Canvas mattress",new Vector3(0,y+.12f,0),new Vector3(.94f,.15f,1.94f),canvas,bunk);
            Box("Pillow",new Vector3(0,y+.23f,.68f),new Vector3(.63f,.13f,.40f),paper,bunk);
            Box("Folded blanket",new Vector3(0,y+.22f,-.61f),new Vector3(.93f,.06f,.57f),paint,bunk,false);
        }
        for(int i=0;i<4;i++) Rod("Ladder rung",new Vector3(.53f,.4f+i*.32f,-.68f),new Vector3(.53f,.4f+i*.32f,-.17f),.025f,metal,bunk);
        foreach(float z in new[]{-.68f,-.17f}) Rod("Ladder side",new Vector3(.53f,.08f,z),new Vector3(.53f,1.8f,z),.028f,metal,bunk);
        bunkPrefab = SavePrefab(bunk);
    }

    static GameObject SavePrefab(Transform root)
    {
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root.gameObject, Root+"Prefabs/"+root.name+".prefab");
        UnityEngine.Object.DestroyImmediate(root.gameObject);
        return prefab;
    }

    static void Place(GameObject prefab, Vector3 position, float yaw=0)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        go.transform.SetParent(props);
        go.transform.position = position;
        go.transform.rotation = Quaternion.Euler(0,yaw,0);
    }

    static void Wheel(string name, Vector3 position, float radius, float yaw, Transform parent)
    {
        Transform root=Group(name,parent); root.localPosition=position; root.localRotation=Quaternion.Euler(0,yaw,0);
        const int segments=20;
        for(int i=0;i<segments;i++)
        {
            float a=i*Mathf.PI*2/segments,b=(i+1)*Mathf.PI*2/segments;
            Rod("Wheel rim",new Vector3(Mathf.Cos(a)*radius,Mathf.Sin(a)*radius,0),new Vector3(Mathf.Cos(b)*radius,Mathf.Sin(b)*radius,0),.018f,rust,root);
        }
        for(int i=0;i<3;i++)
        {
            float a=i*Mathf.PI*2/3;
            Rod("Wheel spoke",Vector3.zero,new Vector3(Mathf.Cos(a)*radius,Mathf.Sin(a)*radius,0),.016f,metal,root);
        }
    }

    static void Table(Vector3 position, Vector3 size, Transform parent)
    {
        Box("Table top",position+new Vector3(0,.79f,0),new Vector3(size.x,.10f,size.z),wood,parent);
        foreach(float x in new[]{-.5f,.5f}) foreach(float z in new[]{-.5f,.5f})
            Box("Table leg",position+new Vector3(x*(size.x-.18f),.38f,z*(size.z-.18f)),new Vector3(.075f,.76f,.075f),metal,parent);
    }

    static void Furnish()
    {
        Place(bunkPrefab,new Vector3(-8.1f,0,2),0);
        Place(bunkPrefab,new Vector3(-8.1f,0,5.5f),0);
        foreach(float x in new[]{-3.1f,-3.85f,-4.6f}) Place(lockerPrefab,new Vector3(x,0,7.45f));
        Table(new Vector3(-5.5f,0,5),new Vector3(1.65f,0,.9f),props);
        Box("Barracks bench",new Vector3(-5.5f,.44f,4.1f),new Vector3(1.5f,.13f,.32f),wood,props);
        foreach(float x in new[]{-6.05f,-4.95f}) Box("Bench support",new Vector3(x,.2f,4.1f),new Vector3(.09f,.4f,.3f),metal,props);
        Place(cratePrefab,new Vector3(-3.3f,0,.8f),12);

        // An open steel shelf on the far wall of the store room.
        Transform shelf=Group("Storage shelving",props);
        foreach(float x in new[]{4.7f,8.2f}) foreach(float z in new[]{6.65f,7.6f})
            Box("Shelf upright",new Vector3(x,1.22f,z),new Vector3(.08f,2.44f,.08f),metal,shelf);
        foreach(float y in new[]{.18f,1.12f,2.06f})
            Box("Shelf board",new Vector3(6.45f,y,7.12f),new Vector3(3.6f,.08f,1.05f),metal,shelf);
        foreach(float x in new[]{5.4f,6.55f,7.7f}) Place(cratePrefab,new Vector3(x,.22f,7.12f));
        foreach(float x in new[]{5.3f,7.5f}) Place(cratePrefab,new Vector3(x,1.16f,7.12f));
        Place(cratePrefab,new Vector3(7.7f,0,1.2f),-10);
        Place(cratePrefab,new Vector3(7.7f,.86f,1.2f),3);
        Place(cratePrefab,new Vector3(6.55f,0,1.35f),8);
        Place(barrelPrefab,new Vector3(8.2f,0,4.3f));
        Place(barrelPrefab,new Vector3(7.4f,0,4.9f));
        Place(barrelPrefab,new Vector3(4.4f,0,1));

        // Control room: a modest map table, radio desk and electrical cabinet.
        Table(new Vector3(0,0,13.55f),new Vector3(2.9f,0,1.55f),props);
        Box("Map sheet",new Vector3(0,.852f,13.55f),new Vector3(2.25f,.008f,1.13f),paper,props,false);
        for(int i=0;i<6;i++) Box("Map grid line",new Vector3(-.9f+i*.36f,.858f,13.55f),new Vector3(.012f,.003f,1.1f),paint,details,false);
        for(int i=0;i<4;i++) Box("Map grid line",new Vector3(0,.859f,13.1f+i*.3f),new Vector3(2.2f,.003f,.012f),paint,details,false);
        Table(new Vector3(-3.6f,0,15.8f),new Vector3(1.8f,0,.8f),props);
        Box("Field radio",new Vector3(-3.6f,1.06f,15.8f),new Vector3(.94f,.43f,.52f),metal,props);
        Box("Radio face",new Vector3(-3.6f,1.06f,15.522f),new Vector3(.84f,.34f,.025f),black,props,false);
        for(int i=0;i<7;i++) Box("Speaker grille",new Vector3(-3.88f+i*.044f,1.06f,15.5f),new Vector3(.014f,.23f,.012f),metal,details,false);
        Box("Tuning display",new Vector3(-3.36f,1.13f,15.49f),new Vector3(.22f,.07f,.016f),paper,details,false);
        Rod("Radio antenna",new Vector3(-3.94f,1.28f,15.9f),new Vector3(-4.04f,1.9f,15.9f),.012f,metal,props);
        Box("Electrical cabinet",new Vector3(4.48f,1.16f,14.9f),new Vector3(.53f,2.05f,1.55f),metal,props);
        for(int i=0;i<4;i++) Box("Fuse module",new Vector3(4.19f,1.6f-i*.3f,14.9f),new Vector3(.03f,.18f,1.1f),black,props,false);
        Box("Notice board",new Vector3(.2f,1.95f,16.79f),new Vector3(2.9f,1.55f,.08f),wood,props);
        for(int i=0;i<5;i++)
        {
            var note=Box("Faded order",new Vector3(-.8f+i*.49f,1.94f+(i%2)*.18f,16.74f),new Vector3(.37f,.53f,.01f),paper,details,false);
            note.transform.Rotate(0,0,(i-2)*3);
        }
        Place(lockerPrefab,new Vector3(3.3f,0,16.45f));
        Place(cratePrefab,new Vector3(-4.1f,0,11.3f),8);
    }

    static float Rand(float min,float max) { return min+(float)random.NextDouble()*(max-min); }

    static void AddDetails()
    {
        // Rusty service pipes run along the corridor, with simple support collars.
        foreach(float x in new[]{-1.35f,-1.03f})
        {
            Rod("Overhead service pipe",new Vector3(x,2.91f,-9.7f),new Vector3(x,2.91f,9.7f),.075f,rust,details);
            for(float z=-8;z<10;z+=2.8f)
            {
                Rod("Pipe collar",new Vector3(x,2.91f,z-.045f),new Vector3(x,2.91f,z+.045f),.099f,metal,details);
                Box("Pipe bracket",new Vector3(x,3.13f,z),new Vector3(.055f,.35f,.065f),metal,details,false);
            }
        }
        Rod("Wall service pipe",new Vector3(1.69f,.57f,-9.7f),new Vector3(1.69f,.57f,-.5f),.07f,rust,details);
        Wheel("Pipe shutoff valve",new Vector3(1.54f,.76f,-3.1f),.20f,90,details);
        Box("Cable tray",new Vector3(1.55f,2.93f,-.1f),new Vector3(.18f,.12f,18.7f),metal,details,false);
        foreach(float z in new[]{-6.5f,6f})
        {
            Box("Junction box",new Vector3(1.73f,1.55f,z),new Vector3(.18f,.55f,.42f),metal,props);
            Rod("Electrical conduit",new Vector3(1.77f,1.7f,z),new Vector3(1.77f,2.88f,z),.022f,metal,details);
        }
        // Flat irregular patches and fine cracks imply dampness without costly decals.
        for(int i=0;i<25;i++)
        {
            float z=Rand(-8.5f,9f), x=(i%2==0?-1:1)*Rand(1.1f,1.56f);
            var patch=Shape("Damp floor patch",PrimitiveType.Cylinder,new Vector3(x,.006f,z),new Vector3(Rand(.25f,.68f),.005f,Rand(.4f,1.2f)),damp,details,false);
            patch.transform.Rotate(0,Rand(0,180),0);
        }
        for(int i=0;i<16;i++)
        {
            float z=Rand(-8.8f,-.4f), side=i%2==0?-1:1;
            Vector3 start=new Vector3(side*1.816f,Rand(1.4f,2.8f),z);
            for(int j=0;j<3;j++)
            {
                Vector3 end=start+new Vector3(0,-Rand(.12f,.25f),Rand(-.17f,.17f));
                Rod("Hairline wall crack",start,end,.006f,black,details);
                start=end;
            }
        }
        // Keep the middle of the passage clear for the character controller.
        for(int i=0;i<48;i++)
        {
            float x=(i%2==0?-1:1)*Rand(1.18f,1.6f), z=Rand(-8.8f,9.2f);
            Vector3 size=new Vector3(Rand(.05f,.18f),Rand(.025f,.085f),Rand(.07f,.23f));
            var chip=Box("Concrete fragment",new Vector3(x,size.y/2,z),size,concrete,details,false);
            chip.transform.Rotate(0,Rand(0,180),Rand(-12,12));
        }
        foreach(Vector3 pos in new[]{new Vector3(-5.6f,.015f,3.7f),new Vector3(.6f,.015f,7.4f),new Vector3(3.6f,.015f,5.2f)})
        {
            var page=Box("Discarded paper",pos,new Vector3(.22f,.005f,.3f),paper,details,false);
            page.transform.Rotate(0,Rand(-60,60),0);
        }
        // Two damaged ceiling panels add a small abandoned detail.
        Box("Loose ceiling panel",new Vector3(.55f,3.13f,6),new Vector3(.68f,.05f,.9f),metal,details,false).transform.Rotate(15,0,9);
    }

    static Light Point(string name, Vector3 position, Color color, float intensity, float range, bool shadows=false)
    {
        Transform t=Group(name,lighting); t.position=position;
        Light l=t.gameObject.AddComponent<Light>();
        l.type=LightType.Point; l.color=color; l.intensity=intensity; l.range=range;
        l.shadows=shadows?LightShadows.Soft:LightShadows.None;
        l.shadowStrength=.7f; l.shadowBias=.04f; l.renderMode=LightRenderMode.ForcePixel;
        return l;
    }

    static void Fixture(Vector3 pos, bool warm=false, bool flicker=false)
    {
        Box("Fluorescent housing",pos+Vector3.up*.13f,new Vector3(1.05f,.12f,.26f),metal,lighting,false);
        Box("Fluorescent tube",pos,new Vector3(.9f,.055f,.12f),glow,lighting,false);
        var lamp=Point("Fixture light",pos-Vector3.up*.25f,warm?new Color(1,.73f,.45f):new Color(.75f,.86f,.8f),1.55f,7.5f,true);
        if(flicker) lamp.gameObject.AddComponent<BunkerLight>();
    }

    static void AddLights()
    {
        Fixture(new Vector3(0,2.98f,-6.8f));
        Fixture(new Vector3(0,2.98f,-1.9f),true);
        Fixture(new Vector3(0,2.98f,4.7f),false,true);
        Fixture(new Vector3(0,3.12f,11.4f),true);
        Fixture(new Vector3(-5.8f,3.12f,3.5f));
        Fixture(new Vector3(5.8f,3.12f,4.1f),true);
        Fixture(new Vector3(0,3.12f,15.5f));
        Point("Barracks soft fill",new Vector3(-6,1.6f,6.2f),new Color(.55f,.65f,.58f),.35f,5);
        Point("Storage soft fill",new Vector3(7,1.5f,2),new Color(.72f,.59f,.4f),.35f,5);
        Point("Entry spill",new Vector3(0,1.8f,-8.7f),new Color(.46f,.58f,.66f),.4f,4);
    }

    static void AddPlayer()
    {
        Transform player=Group("06 - Player (WASD, mouse, F, E)");
        player.position=new Vector3(.25f,.04f,-8.3f);
        CharacterController cc=player.gameObject.AddComponent<CharacterController>();
        cc.height=1.8f; cc.radius=.29f; cc.center=new Vector3(0,.9f,0); cc.stepOffset=.25f;
        Transform view=Group("Player camera",player); view.localPosition=new Vector3(0,1.63f,0);
        Camera camera=view.gameObject.AddComponent<Camera>(); camera.tag="MainCamera";
        camera.fieldOfView=68; camera.nearClipPlane=.04f; camera.farClipPlane=65;
        camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.06f,.075f,.066f);
        view.gameObject.AddComponent<AudioListener>();
        Transform torch=Group("Flashlight - F",view); torch.localPosition=new Vector3(.18f,-.13f,.1f);
        Light light= torch.gameObject.AddComponent<Light>();
        light.type=LightType.Spot; light.range=17; light.spotAngle=58; light.innerSpotAngle=28;
        light.intensity=2.2f; light.color=new Color(1f,.9f,.69f); light.shadows=LightShadows.Soft;
        light.enabled=false;
        var walker=player.gameObject.AddComponent<BunkerWalker>(); walker.viewCamera=camera; walker.flashlight=light;
    }

    static void ConfigureProject()
    {
        RenderSettings.ambientMode=AmbientMode.Flat;
        RenderSettings.ambientLight=new Color(.25f,.28f,.25f);
        RenderSettings.ambientIntensity=1f;
        RenderSettings.skybox=null;
        RenderSettings.fog=true; RenderSettings.fogMode=FogMode.ExponentialSquared;
        RenderSettings.fogColor=new Color(.095f,.12f,.105f); RenderSettings.fogDensity=.017f;
        QualitySettings.pixelLightCount=8; QualitySettings.antiAliasing=4;
        QualitySettings.shadows=ShadowQuality.All; QualitySettings.shadowDistance=30;
        QualitySettings.shadowResolution=ShadowResolution.Medium;
        PlayerSettings.companyName="Student Project"; PlayerSettings.productName="Outpost 04 - Abandoned Bunker";
        PlayerSettings.defaultScreenWidth=1280; PlayerSettings.defaultScreenHeight=720;
        PlayerSettings.fullScreenMode=FullScreenMode.Windowed;
        PlayerSettings.colorSpace=ColorSpace.Linear;
        var settings=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
        var input=settings.FindProperty("activeInputHandler");
        if(input!=null) { input.intValue=0; settings.ApplyModifiedPropertiesWithoutUndo(); }
    }

    public static void ValidateScene()
    {
        int missing=0;
        foreach(GameObject root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            foreach(Transform child in root.GetComponentsInChildren<Transform>(true))
                missing+=GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject);
        if(missing!=0) throw new Exception("Missing scripts: "+missing);
        if(UnityEngine.Object.FindObjectsByType<BunkerWalker>().Length!=1) throw new Exception("Player missing");
        foreach(Renderer renderer in UnityEngine.Object.FindObjectsByType<Renderer>())
            foreach(Material material in renderer.sharedMaterials)
                if(material==null || material.shader==null || !material.shader.isSupported) throw new Exception("Missing/unsupported material on "+renderer.name);
        Physics.SyncTransforms();
        foreach(Vector3 point in new[]{new Vector3(0,1,-8),new Vector3(0,1,3),new Vector3(-4,1,3),new Vector3(4,1,3),new Vector3(0,1,11)})
            if(!Physics.Raycast(point,Vector3.down,2f)) throw new Exception("Missing floor at "+point);
        Debug.Log("BUNKER_VALIDATION_OK: references, materials, player and room floors.");
    }

    public static void CapturePreviews()
    {
        string output=Path.GetFullPath(Path.Combine(Application.dataPath,"../../Previews"));
        Directory.CreateDirectory(output);
        Capture(output+"/01-Corridor.png",new Vector3(.45f,1.65f,-8.35f),new Vector3(-.1f,1.55f,4));
        Capture(output+"/02-Barracks.png",new Vector3(-2.9f,1.8f,1),new Vector3(-6.6f,1.1f,4.4f));
        Capture(output+"/03-Control-room.png",new Vector3(3.8f,1.8f,10.95f),new Vector3(-.8f,1.1f,14.6f));
        Capture(output+"/04-Storage.png",new Vector3(3f,1.8f,2.2f),new Vector3(6.8f,1.25f,5.4f));
    }

    static void Capture(string path,Vector3 from,Vector3 target)
    {
        var go=new GameObject("Preview camera");
        var camera=go.AddComponent<Camera>();
        camera.transform.position=from; camera.transform.LookAt(target);
        camera.fieldOfView=68; camera.nearClipPlane=.05f; camera.farClipPlane=70;
        camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=Color.black;
        var rt=new RenderTexture(1440,900,24,RenderTextureFormat.ARGB32);
        rt.antiAliasing=4; camera.targetTexture=rt;
        camera.Render();
        RenderTexture previous=RenderTexture.active; RenderTexture.active=rt;
        var tex=new Texture2D(1440,900,TextureFormat.RGB24,false);
        tex.ReadPixels(new Rect(0,0,1440,900),0,0); tex.Apply();
        File.WriteAllBytes(path,tex.EncodeToPNG());
        RenderTexture.active=previous; camera.targetTexture=null;
        UnityEngine.Object.DestroyImmediate(tex); rt.Release(); UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(go);
    }
}
