using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class PartyHudPrefabBuilder
{
    public const string Output = "Assets/Prefab/User Interface/PlayerUI_PartyHud.prefab";
    const string ArtFolder = "Assets/Prefab/User Interface/PartyHudArt";

    [MenuItem("Tools/Party HUD/Create Separate Prefab")]
    public static void Create()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(Output) != null)
            throw new InvalidOperationException("Separate HUD already exists; edit it directly to preserve authored changes.");
        var ready = CopySprite("Active_Ualtimad_Skill_Open_Hud");
        var closed = CopySprite("Active_Ualtimad_Skill_Close_Hud");
        var combo = CopySprite("Combo_Hud");
        if (!AssetDatabase.CopyAsset("Assets/Prefab/User Interface/PlayerUI.prefab", Output))
            throw new InvalidOperationException("Could not copy PlayerUI prefab.");
        var root = PrefabUtility.LoadPrefabContents(Output);
        try
        {
            var canvas = root.transform.Find("UI_Manager");
            canvas.Find("PlayerHUD").gameObject.SetActive(false);
            var hud = Rect(canvas, "PartyHUD", new Vector2(.5f,.5f), Vector2.zero, Vector2.zero);
            hud.anchorMin = Vector2.zero; hud.anchorMax = Vector2.one; hud.offsetMin = hud.offsetMax = Vector2.zero;
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920,1080); scaler.matchWidthOrHeight = .5f;
            var presenter = hud.gameObject.AddComponent<PartyHudPresenter>();
            var router = hud.gameObject.AddComponent<PartyHudInputRouter>();
            Set(router, "presenter", presenter);
            var manager = canvas.GetComponent<UIManager>();
            Set(manager, "playerHudRoot", hud.gameObject);
            var portraits = new List<UnityEngine.Object>(); var fills = new List<UnityEngine.Object>();
            for (int i=0;i<3;i++)
            {
                var card=Rect(hud,"Ally"+(i+1),Vector2.zero,new Vector2(68,320-i*120),new Vector2(118,112));
                Panel(card,"Backing",Vector2.zero,new Vector2(118,112),new Color(.08f,.1f,.12f,.55f));
                portraits.Add(Icon(card,"Portrait",new Vector2(0,10),new Vector2(96,92),null));
                fills.Add(Bar(card,"HP",new Vector2(0,-44),new Vector2(108,8),new Color(.2f,.72f,.85f)));
            }
            SetArray(presenter,"portraits",portraits); SetArray(presenter,"healthFills",fills);
            var status=Rect(hud,"PlayerStatus",new Vector2(.5f,0),new Vector2(-15,60),new Vector2(480,72));
            Panel(status,"Backing",Vector2.zero,new Vector2(480,72),new Color(.05f,.07f,.08f,.75f));
            Set(presenter,"playerHealth",Bar(status,"HP",new Vector2(0,15),new Vector2(446,14),new Color(.2f,.72f,.85f)));
            Set(presenter,"playerHealthLabel",Label(status,"HPLabel",new Vector2(0,15),new Vector2(440,24),"HP / HP",20));
            var segments=new List<UnityEngine.Object>();
            for(int i=0;i<3;i++) segments.Add(Bar(status,"Command"+i,new Vector2(-150+i*150,-15),new Vector2(140,14),new Color(1,.58f,.25f)));
            SetArray(presenter,"commandSegments",segments);
            var bullet=Rect(hud,"BulletInfo",new Vector2(.55f,.42f),Vector2.zero,new Vector2(164,74));
            Panel(bullet,"Backing",Vector2.zero,new Vector2(164,74),new Color(.08f,.1f,.12f,.4f));
            Set(presenter,"ammo",Label(bullet,"Ammo",Vector2.zero,new Vector2(160,70),"BULLET\n30/300",24));
            var skillPanel=Rect(hud,"PartySkills",new Vector2(1,0),new Vector2(-250,115),new Vector2(490,230));
            var frames=new List<UnityEngine.Object>();var icons=new List<UnityEngine.Object>();var labels=new List<UnityEngine.Object>();
            for(int kind=0;kind<2;kind++) for(int slot=0;slot<4;slot++)
            {
                float size=kind==0?74:106;
                var cell=Rect(skillPanel,(kind==0?"Active":"Ultimate")+(slot+1),new Vector2(.5f,.5f),new Vector2(-180+slot*120,kind==0?62:-40),new Vector2(size,size));
                frames.Add(Icon(cell,"Frame",Vector2.zero,Vector2.one*size,closed));
                icons.Add(Icon(cell,"SkillIcon",Vector2.zero,Vector2.one*size*.65f,null));
                labels.Add(Label(cell,"Status",Vector2.zero,Vector2.one*size,"—",kind==0?18:22));
                if(kind==1) Label(cell,"Key",new Vector2(0,-55),new Vector2(35,28),(slot+1).ToString(),22);
            }
            SetArray(presenter,"skillFrames",frames);SetArray(presenter,"skillIcons",icons);SetArray(presenter,"skillLabels",labels);
            Set(presenter,"readyFrame",ready);Set(presenter,"unavailableFrame",closed);
            Set(presenter,"feedback",Label(skillPanel,"Feedback",new Vector2(0,117),new Vector2(460,28),"",18));
            var combos=Rect(hud,"ComboOffers",new Vector2(.56f,.53f),Vector2.zero,new Vector2(480,150));
            combos.pivot=new Vector2(0,.5f);
            Label(combos,"KeyE",new Vector2(-225,45),new Vector2(35,35),"E",26);
            var cards=new List<UnityEngine.Object>();var faces=new List<UnityEngine.Object>();var timers=new List<UnityEngine.Object>();
            for(int i=0;i<3;i++)
            {
                var card=Rect(combos,"Offer"+i,new Vector2(.5f,.5f),new Vector2(-160+i*128,0),new Vector2(116,140));
                faces.Add(Icon(card,"Portrait",new Vector2(0,-16),new Vector2(72,72),null));
                Icon(card,"Frame",Vector2.zero,new Vector2(116,140),combo);
                timers.Add(Label(card,"Remaining",new Vector2(26,-39),new Vector2(28,28),"",19));
                cards.Add(card);
            }
            Set(presenter,"comboRoot",combos.gameObject);SetArray(presenter,"comboCards",cards);SetArray(presenter,"comboPortraits",faces);SetArray(presenter,"comboTimes",timers);
            combos.gameObject.SetActive(false);
            PrefabUtility.SaveAsPrefabAsset(root,Output);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    static Sprite CopySprite(string name)
    {
        if (!AssetDatabase.IsValidFolder(ArtFolder)) AssetDatabase.CreateFolder("Assets/Prefab/User Interface","PartyHudArt");
        string path=ArtFolder+"/"+name+".png";
        if (AssetDatabase.LoadAssetAtPath<Texture2D>(path)==null) AssetDatabase.CopyAsset("Assets/Spain/UI/"+name+".png",path);
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType=TextureImporterType.Sprite; importer.spriteImportMode=SpriteImportMode.Single;
        importer.alphaIsTransparency=true; importer.mipmapEnabled=false; importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
    static RectTransform Rect(Transform parent,string name,Vector2 anchor,Vector2 position,Vector2 size)
    {
        var go=new GameObject(name,typeof(RectTransform));var r=(RectTransform)go.transform;
        r.SetParent(parent,false);r.anchorMin=r.anchorMax=anchor;r.anchoredPosition=position;r.sizeDelta=size;return r;
    }
    static Image Icon(Transform parent,string name,Vector2 position,Vector2 size,Sprite sprite)
    {
        var r=Rect(parent,name,new Vector2(.5f,.5f),position,size);var image=r.gameObject.AddComponent<Image>();
        image.sprite=sprite;image.preserveAspect=true;image.raycastTarget=false;image.enabled=sprite!=null;return image;
    }
    static Image Panel(Transform parent,string name,Vector2 position,Vector2 size,Color color)
    {var image=Icon(parent,name,position,size,null);image.enabled=true;image.preserveAspect=false;image.color=color;return image;}
    static Image Bar(Transform parent,string name,Vector2 position,Vector2 size,Color color)
    {
        var bg=Panel(parent,name+"Background",position,size+new Vector2(8,8),new Color(.02f,.03f,.04f,.9f));
        var fill=Panel(bg.transform,name,Vector2.zero,size,color);fill.sprite=SolidFill();fill.type=Image.Type.Filled;fill.fillMethod=Image.FillMethod.Horizontal;
        fill.fillOrigin=(int)Image.OriginHorizontal.Left;
        fill.fillAmount=1;return fill;
    }
    public static Sprite SolidFill()
    {
        const string path=ArtFolder+"/SolidFill.asset";
        foreach(var asset in AssetDatabase.LoadAllAssetsAtPath(path)) if(asset is Sprite existing) return existing;
        var texture=new Texture2D(2,2,TextureFormat.RGBA32,false) { name="SolidFill", filterMode=FilterMode.Point };
        texture.SetPixels(new[]{Color.white,Color.white,Color.white,Color.white});texture.Apply();
        AssetDatabase.CreateAsset(texture,path);
        var sprite=Sprite.Create(texture,new Rect(0,0,2,2),new Vector2(.5f,.5f),100);
        sprite.name="SolidFill";AssetDatabase.AddObjectToAsset(sprite,texture);AssetDatabase.SaveAssetIfDirty(texture);
        return sprite;
    }
    static TMP_Text Label(Transform parent,string name,Vector2 position,Vector2 size,string value,float fontSize)
    {
        var r=Rect(parent,name,new Vector2(.5f,.5f),position,size);var t=r.gameObject.AddComponent<TextMeshProUGUI>();
        t.font=TMP_Settings.defaultFontAsset;t.fontSize=fontSize;t.text=value;t.color=Color.white;
        t.alignment=TextAlignmentOptions.Center;t.raycastTarget=false;t.overflowMode=TextOverflowModes.Ellipsis;return t;
    }
    static void Set(UnityEngine.Object target,string field,UnityEngine.Object value)
    {var so=new SerializedObject(target);so.FindProperty(field).objectReferenceValue=value;so.ApplyModifiedPropertiesWithoutUndo();}
    static void SetArray(UnityEngine.Object target,string field,List<UnityEngine.Object> values)
    {var so=new SerializedObject(target);var array=so.FindProperty(field);array.arraySize=values.Count;for(int i=0;i<values.Count;i++)array.GetArrayElementAtIndex(i).objectReferenceValue=values[i];so.ApplyModifiedPropertiesWithoutUndo();}
}
