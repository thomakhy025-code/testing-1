using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Puts the Blender boy on the boy prefab and builds his animator.
/// Does not rebuild the scene.
/// </summary>
public static class BoyHeroSetup
{
    const string ModelPath = "Assets/Models/BoyHero.fbx";
    const string ControllerPath = "Assets/Models/BoyHero.controller";
    const string PrefabPath = "Assets/Prefabs/Boy.prefab";
    const string MaterialDir = "Assets/Materials/BoyHero";

    public static string Install()
    {
        var log = new StringBuilder();
        if (AssetImporter.GetAtPath(ModelPath) == null)
            return "Missing " + ModelPath;

        ConfigureModel(ModelPath, false);
        if (Directory.Exists("Assets/Models/BoyAnims"))
        {
            var files = Directory.GetFiles("Assets/Models/BoyAnims", "*.fbx");
            for (int i = 0; i < files.Length; i++)
            {
                string assetPath = files[i].Replace('\\', '/');
                string clipName = ConfigureModel(assetPath, true);
                log.Append(clipName).Append(" | ");
            }
        }

        var motions = LoadClips();
        var controller = BuildController(motions);
        log.Append(" clips=").Append(motions.Count);
        log.Append(" attach=").Append(Attach(controller));
        return log.ToString();
    }

    static string ConfigureModel(string assetPath, bool clipFile)
    {
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        var importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
        if (importer == null)
            return "missing " + assetPath;

        importer.animationType = ModelImporterAnimationType.Generic;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.importAnimation = true;
        importer.globalScale = 1f;
        importer.useFileScale = true;
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        importer.importBlendShapes = false;
        importer.animationCompression = ModelImporterAnimationCompression.Off;
        importer.SaveAndReimport();

        var defaults = importer.defaultClipAnimations;
        var clips = new ModelImporterClipAnimation[defaults.Length];
        string names = "";
        for (int i = 0; i < defaults.Length; i++)
        {
            var source = defaults[i];
            var clip = new ModelImporterClipAnimation();
            clip.name = source.name;
            clip.takeName = source.takeName;
            clip.firstFrame = source.firstFrame;
            clip.lastFrame = source.lastFrame;
            clip.loopTime = Loops(Path.GetFileNameWithoutExtension(assetPath));
            clip.lockRootRotation = true;
            clip.lockRootHeightY = true;
            clip.lockRootPositionXZ = true;
            clip.keepOriginalOrientation = true;
            clip.keepOriginalPositionY = true;
            clip.keepOriginalPositionXZ = true;
            clips[i] = clip;
            names += source.name + " " + source.firstFrame + "-" + source.lastFrame + " ";
        }

        if (clips.Length > 0)
        {
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
        }

        return clipFile ? Path.GetFileNameWithoutExtension(assetPath) + "(" + names.Trim() + ")" : "mesh";
    }

    static bool Loops(string name)
    {
        string bare = name;
        int bar = name.LastIndexOf('|');
        if (bar >= 0 && bar < name.Length - 1)
            bare = name.Substring(bar + 1);
        return bare == "Idle" || bare == "Walk" || bare == "Run" || bare == "Guard";
    }

    static List<AnimationClip> LoadClips()
    {
        var list = new List<AnimationClip>();
        if (!Directory.Exists("Assets/Models/BoyAnims"))
            return list;

        var files = Directory.GetFiles("Assets/Models/BoyAnims", "*.fbx");
        for (int f = 0; f < files.Length; f++)
        {
            string assetPath = files[f].Replace('\\', '/');
            var assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            for (int i = 0; i < assets.Length; i++)
            {
                var clip = assets[i] as AnimationClip;
                if (clip == null || clip.name.StartsWith("__preview__"))
                    continue;
                list.Add(clip);
            }
        }

        return list;
    }

    static AnimationClip FindClip(List<AnimationClip> clips, string name)
    {
        for (int i = 0; i < clips.Count; i++)
        {
            string clipName = clips[i].name;
            string assetPath = AssetDatabase.GetAssetPath(clips[i]).Replace('\\', '/');
            if (assetPath.EndsWith("/" + name + ".fbx"))
                return clips[i];
            if (clipName == name || clipName.EndsWith("|" + name))
                return clips[i];
        }

        return null;
    }

    static AnimatorController BuildController(List<AnimationClip> clips)
    {
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) != null)
            AssetDatabase.DeleteAsset(ControllerPath);

        var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        var machine = controller.layers[0].stateMachine;
        var locomotion = controller.CreateBlendTreeInController("Locomotion", out var tree, 0);
        tree.blendType = BlendTreeType.Simple1D;
        tree.blendParameter = "Speed";
        tree.useAutomaticThresholds = false;
        AddChild(tree, clips, "Idle", 0f);
        AddChild(tree, clips, "Walk", 2.15f);
        AddChild(tree, clips, "Run", 6.2f);
        machine.defaultState = locomotion;

        string[] states =
        {
            "Jump", "Tuck", "Land", "LightLand", "Dash", "DashRecover",
            "Slide", "SlideRecover", "Guard", "Attack1", "Attack2", "Attack3",
            "Flinch", "Knock", "Down", "Brake"
        };
        for (int i = 0; i < states.Length; i++)
        {
            var state = machine.AddState(states[i]);
            state.motion = FindClip(clips, states[i]);
        }

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        return controller;
    }

    static void AddChild(BlendTree tree, List<AnimationClip> clips, string name, float threshold)
    {
        var clip = FindClip(clips, name);
        if (clip != null)
            tree.AddChild(clip, threshold);
    }

    static string Attach(AnimatorController controller)
    {
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        var visuals = root.transform.Find("Visuals");
        if (visuals == null)
        {
            PrefabUtility.UnloadPrefabContents(root);
            return "no visuals";
        }

        var existing = visuals.Find("BoyHero");
        if (existing != null)
            Object.DestroyImmediate(existing.gameObject);

        var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        var hero = (GameObject)PrefabUtility.InstantiatePrefab(model);
        hero.name = "BoyHero";
        hero.transform.SetParent(visuals, false);
        hero.transform.localPosition = new Vector3(0f, 0.07f, 0f);
        hero.transform.localRotation = Quaternion.identity;
        hero.transform.localScale = Vector3.one;

        var animator = hero.GetComponent<Animator>();
        if (animator == null)
            animator = hero.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        var skinned = hero.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        for (int s = 0; s < skinned.Length; s++)
            skinned[s].updateWhenOffscreen = true;

        Paint(hero);
        HideBlocks(visuals);

        var bounds = new Bounds(hero.transform.position, Vector3.zero);
        var renderers = hero.GetComponentsInChildren<Renderer>();
        bool any = false;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (!any)
            {
                bounds = renderers[i].bounds;
                any = true;
            }
            else
            {
                bounds.Encapsulate(renderers[i].bounds);
            }
        }

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        PrefabUtility.UnloadPrefabContents(root);
        AssetDatabase.SaveAssets();
        return "height " + bounds.size.y.ToString("0.00") + " y " + bounds.min.y.ToString("0.00") + ".." + bounds.max.y.ToString("0.00");
    }

    static void HideBlocks(Transform visuals)
    {
        for (int i = 0; i < visuals.childCount; i++)
        {
            var child = visuals.GetChild(i);
            if (child.name == "BoyHero")
                continue;
            var renderers = child.GetComponentsInChildren<Renderer>(true);
            for (int r = 0; r < renderers.Length; r++)
                renderers[r].enabled = false;
        }
    }

    static void Paint(GameObject hero)
    {
        if (!AssetDatabase.IsValidFolder(MaterialDir))
            AssetDatabase.CreateFolder("Assets/Materials", "BoyHero");

        var renderers = hero.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            var shared = renderers[i].sharedMaterials;
            var next = new Material[shared.Length];
            for (int m = 0; m < shared.Length; m++)
            {
                string sourceName = shared[m] != null ? shared[m].name : "Blazer";
                next[m] = MaterialFor(sourceName);
            }

            renderers[i].sharedMaterials = next;
        }
    }

    static Material MaterialFor(string sourceName)
    {
        string clean = sourceName.Replace(" ", "").Replace(".", "_");
        string path = MaterialDir + "/" + clean + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Standard"));
            AssetDatabase.CreateAsset(material, path);
        }

        Color color = ColorFor(sourceName);
        material.color = color;
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);
        float metal = 0f;
        string lower = sourceName.ToLowerInvariant();
        if (lower.Contains("gold") || lower.Contains("steel") || lower.Contains("chape"))
            metal = 0.7f;
        if (lower.Contains("shoe") || lower.Contains("sole"))
            metal = 0.15f;
        material.SetFloat("_Metallic", metal);
        float gloss = 0.3f;
        if (metal > 0.4f)
            gloss = 0.62f;
        else if (lower.Contains("shoe") || lower.Contains("sole"))
            gloss = 0.58f;
        else if (lower.Contains("leather") || lower.Contains("hair") || lower.Contains("sock"))
            gloss = 0.22f;
        material.SetFloat("_Glossiness", gloss);
        EditorUtility.SetDirty(material);
        return material;
    }

    static Color ColorFor(string sourceName)
    {
        string name = sourceName.ToLowerInvariant();
        if (name.Contains("hair") || name.Contains("spike") || name.Contains("brow"))
            return new Color(0.05f, 0.055f, 0.07f);
        if (name.Contains("iris"))
            return new Color(0.12f, 0.20f, 0.42f);
        if (name.Contains("pupil"))
            return new Color(0.04f, 0.05f, 0.08f);
        if (name.Contains("mouth"))
            return new Color(0.55f, 0.28f, 0.28f);
        if (name.Contains("eye") || name.Contains("glint"))
            return new Color(0.95f, 0.96f, 0.98f);
        if (name.Contains("skin") || name.Contains("head") || name.Contains("hand") || name.Contains("ear") || name.Contains("nose") || name.Contains("neck"))
            return new Color(0.93f, 0.76f, 0.64f);
        if (name.Contains("tie"))
            return new Color(0.75f, 0.10f, 0.12f);
        if (name.Contains("shirt"))
            return new Color(0.96f, 0.96f, 0.97f);
        if (name.Contains("pipe"))
            return new Color(0.35f, 0.58f, 0.90f);
        if (name.Contains("trim") || name.Contains("collar") || name.Contains("cuff") || name.Contains("hem"))
            return new Color(0.93f, 0.94f, 0.96f);
        if (name.Contains("gold") || name.Contains("button") || name.Contains("buckle") || name.Contains("pommel"))
            return new Color(0.86f, 0.68f, 0.24f);
        if (name.Contains("steel") || name.Contains("guard") || name.Contains("chape"))
            return new Color(0.74f, 0.78f, 0.82f);
        if (name.Contains("grip") || name.Contains("hilt"))
            return new Color(0.07f, 0.11f, 0.22f);
        if (name.Contains("sheath"))
            return new Color(0.025f, 0.03f, 0.04f);
        if (name.Contains("leather") || name.Contains("harness") || name.Contains("belt") || name.Contains("frog"))
            return new Color(0.40f, 0.22f, 0.12f);
        if (name.Contains("sock"))
            return new Color(0.18f, 0.24f, 0.40f);
        if (name.Contains("sole"))
            return new Color(0.14f, 0.14f, 0.15f);
        if (name.Contains("shoe") || name.Contains("toe") || name.Contains("scabbard"))
            return new Color(0.035f, 0.04f, 0.06f);
        if (name.Contains("crease"))
            return new Color(0.16f, 0.20f, 0.32f);
        if (name.Contains("pant") || name.Contains("thigh") || name.Contains("shin") || name.Contains("hip"))
            return new Color(0.05f, 0.07f, 0.15f);
        return new Color(0.045f, 0.08f, 0.18f);
    }
}
