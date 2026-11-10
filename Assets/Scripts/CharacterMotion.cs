using UnityEngine;

/// <summary>
/// Body motion. The boy plays his Blender clips. The girl still uses the block poses.
/// </summary>
public class CharacterMotion : MonoBehaviour
{
    Transform visuals;
    Transform torso;
    Transform head;
    Transform hair;
    Transform hairL;
    Transform hairR;
    Transform bow;
    Transform skirt;
    Transform legL;
    Transform legR;
    Transform armL;
    Transform armR;
    Transform eyeL;
    Transform eyeR;
    Transform pupilL;
    Transform pupilR;

    Vector3 visualsBase;
    Vector3 torsoBase;
    Vector3 headBase;
    Vector3 hairBase;
    Vector3 hairLBase;
    Vector3 hairRBase;
    Vector3 bowBase;
    Vector3 skirtBase;
    Vector3 legLBase;
    Vector3 legRBase;
    Vector3 armLBase;
    Vector3 armRBase;
    Vector3 eyeLBase;
    Vector3 eyeRBase;
    Vector3 pupilLBase;
    Vector3 pupilRBase;

    float gait;
    Vector3 lastPoint;
    float seenSpeed;
    Animator rig;
    string rigState = "";

    void Awake()
    {
        var movement = GetComponent<PlayerMovement>();
        visuals = movement != null ? movement.visuals : transform.Find("Visuals");
        if (visuals == null)
            return;

        visualsBase = visuals.localPosition;
        torso = Find("Torso", ref torsoBase);
        head = Find("Head", ref headBase);
        hair = Find("Hair", ref hairBase);
        hairL = Find("HairL", ref hairLBase);
        hairR = Find("HairR", ref hairRBase);
        bow = Find("Bow", ref bowBase);
        skirt = Find("Skirt", ref skirtBase);
        legL = Find("LegL", ref legLBase);
        legR = Find("LegR", ref legRBase);
        armL = Find("ArmL", ref armLBase);
        armR = Find("ArmR", ref armRBase);
        eyeL = Find("EyeL", ref eyeLBase);
        eyeR = Find("EyeR", ref eyeRBase);
        pupilL = Find("PupilL", ref pupilLBase);
        pupilR = Find("PupilR", ref pupilRBase);
        lastPoint = transform.position;

        var animators = GetComponentsInChildren<Animator>(true);
        for (int i = 0; i < animators.Length; i++)
        {
            if (animators[i] != null && animators[i].runtimeAnimatorController != null)
            {
                rig = animators[i];
                rig.applyRootMotion = false;
                rig.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                break;
            }
        }
    }

    Transform Find(string partName, ref Vector3 basePosition)
    {
        var part = visuals.Find(partName);
        if (part != null)
            basePosition = part.localPosition;
        return part;
    }

    public void Pose()
    {
        if (visuals == null)
            return;

        float dt = Mathf.Max(Time.deltaTime, 0.0001f);
        Vector3 now = transform.position;
        Vector3 flat = now - lastPoint;
        flat.y = 0f;
        seenSpeed = flat.magnitude / dt;
        lastPoint = now;

        var move = GetComponent<PlayerMovement>();
        var combat = GetComponent<PlayerCombat>();
        if (rig != null)
        {
            DriveRig(move, combat, dt);
            return;
        }

        ResetBody();

        if (combat != null && combat.IsDown)
        {
            LieDown();
            return;
        }

        if (combat != null && combat.Swing01 > 0.001f)
        {
            PoseAttack(combat.Swing01, combat.ComboStep);
            return;
        }

        if (combat != null && combat.IsDefending)
        {
            PoseGuard();
            return;
        }

        float dash = move != null ? move.DashPose : 0f;
        if (dash > 0.001f || dash < -0.001f)
        {
            PoseDash(dash);
            return;
        }

        float slide = move != null ? move.SlidePose : 0f;
        if (slide > 0.02f || slide < -0.001f)
        {
            PoseSlide(slide);
            return;
        }

        if (combat != null && combat.Reaction == 2)
        {
            PoseKnock();
            return;
        }

        if (combat != null && combat.Reaction == 1)
        {
            PoseFlinch();
            return;
        }

        float air = move != null ? move.AirPose : 0f;
        if (air >= 2.5f)
        {
            PoseHeavyLand(air);
            return;
        }

        if (air >= 1.5f)
        {
            PoseTuck();
            return;
        }

        if (air >= 0.5f)
        {
            PoseJump(air >= 1.15f ? 0.65f : 0.25f);
            return;
        }

        float brake = move != null ? move.BrakePose : 0f;
        float speed = move != null && move.IsOwner ? move.PlanarSpeed : seenSpeed;
        if (brake > 0.01f)
        {
            PoseBrake(brake, speed);
            return;
        }

        if (speed >= 4.4f)
            PoseRun(speed);
        else if (speed > 0.25f)
            PoseWalk(speed);
        else
            PoseIdle();
    }

    void DriveRig(PlayerMovement move, PlayerCombat combat, float dt)
    {
        bool down = combat != null && combat.IsDown;
        float swing = combat != null ? combat.Swing01 : 0f;
        int combo = combat != null ? combat.ComboStep : 1;
        bool guard = combat != null && combat.IsDefending && swing <= 0.001f;
        float dash = move != null ? move.DashPose : 0f;
        float slide = move != null ? move.SlidePose : 0f;
        int reaction = combat != null ? combat.Reaction : 0;
        float air = move != null ? move.AirPose : 0f;
        float brake = move != null ? move.BrakePose : 0f;
        float speed = move != null && move.IsOwner ? move.PlanarSpeed : seenSpeed;
        rig.SetFloat("Speed", speed, 0.14f, dt);

        string state;
        float norm = -1f;
        if (down)
            state = "Down";
        else if (swing > 0.001f)
        {
            state = combo >= 3 ? "Attack3" : (combo == 2 ? "Attack2" : "Attack1");
            norm = Mathf.Clamp01(swing);
        }
        else if (guard)
            state = "Guard";
        else if (dash > 0.001f)
        {
            state = "Dash";
            norm = Mathf.Clamp01(dash);
        }
        else if (dash < -0.001f)
        {
            state = "DashRecover";
            norm = Mathf.Clamp01(1f - (Mathf.Abs(dash) / 0.18f));
        }
        else if (slide > 0.02f)
            state = "Slide";
        else if (slide < -0.001f)
        {
            state = "SlideRecover";
            norm = Mathf.Clamp01(1f + slide);
        }
        else if (reaction == 2)
            state = "Knock";
        else if (reaction == 1)
            state = "Flinch";
        else if (air >= 2.5f)
        {
            state = "Land";
            norm = Mathf.InverseLerp(3f, 2.5f, air);
        }
        else if (air >= 1.5f)
        {
            state = "Tuck";
            norm = Mathf.InverseLerp(1.5f, 2.4f, Mathf.Min(air, 2.4f));
        }
        else if (air >= 1.02f && air < 1.2f)
        {
            state = "LightLand";
            norm = Mathf.InverseLerp(1.02f, 1.18f, air);
        }
        else if (air >= 0.5f && air < 1f)
        {
            state = "Jump";
            norm = Mathf.InverseLerp(0.5f, 0.98f, air);
        }
        else if (air >= 0.5f)
            state = "Jump";
        else if (brake > 0.01f)
        {
            state = "Brake";
            norm = Mathf.Clamp01(1f - (brake / 0.36f));
        }
        else
            state = "Locomotion";

        if (state == "Locomotion" && speed > 0.25f && speed < 3.4f)
            rig.speed = Mathf.Clamp(speed / 2.15f, 0.72f, 1.05f);
        else
            rig.speed = 1f;

        if (norm >= 0f)
        {
            rig.Play(state, 0, norm);
            rigState = state;
            return;
        }

        if (rigState != state)
        {
            rig.CrossFadeInFixedTime(state, 0.12f, 0);
            rigState = state;
        }
    }

    void ResetBody()
    {
        visuals.localPosition = visualsBase;
        visuals.localRotation = Quaternion.identity;
        Place(torso, torsoBase, Quaternion.identity);
        Place(head, headBase, Quaternion.identity);
        Place(hair, hairBase, Quaternion.identity);
        Place(hairL, hairLBase, Quaternion.identity);
        Place(hairR, hairRBase, Quaternion.identity);
        Place(bow, bowBase, Quaternion.identity);
        Place(skirt, skirtBase, Quaternion.identity);
        Place(legL, legLBase, Quaternion.identity);
        Place(legR, legRBase, Quaternion.identity);
        Place(armL, armLBase, Quaternion.identity);
        Place(armR, armRBase, Quaternion.identity);
        Place(eyeL, eyeLBase, Quaternion.identity);
        Place(eyeR, eyeRBase, Quaternion.identity);
        Place(pupilL, pupilLBase, Quaternion.identity);
        Place(pupilR, pupilRBase, Quaternion.identity);
    }

    void PoseIdle()
    {
        Swing(legL, legLBase, new Vector3(6f, 0f, 0f));
        Swing(legR, legRBase, new Vector3(6f, 0f, 0f));
        Swing(armL, armLBase, new Vector3(16f, 0f, 8f));
        Swing(armR, armRBase, new Vector3(18f, -8f, -6f));
    }

    void PoseWalk(float speed)
    {
        gait += Time.deltaTime * Mathf.Lerp(7.2f, 9.5f, Mathf.InverseLerp(0.4f, 3.2f, speed));
        float step = Mathf.Sin(gait);
        Swing(legL, legLBase, new Vector3(-28f * step, 0f, 0f));
        Swing(legR, legRBase, new Vector3(28f * step, 0f, 0f));
        Swing(armR, armRBase, new Vector3(-26f * step, 0f, -4f));
        Swing(armL, armLBase, new Vector3(26f * step, 0f, 4f));
        Twist(step * 5f, Mathf.Abs(step) * 0.035f);
        LookHead(0f, -step * 5f);
    }

    void PoseRun(float speed)
    {
        gait += Time.deltaTime * Mathf.Lerp(12f, 16f, Mathf.InverseLerp(4.4f, 7f, speed));
        float step = Mathf.Sin(gait);
        visuals.localRotation = Quaternion.Euler(45f, step * 4f, 0f);
        visuals.localPosition = visualsBase + new Vector3(0f, Mathf.Abs(step) * 0.04f, 0.08f);
        Swing(legL, legLBase, new Vector3(-50f * step, 0f, 6f));
        Swing(legR, legRBase, new Vector3(50f * step, 0f, -6f));
        Swing(armL, armLBase, new Vector3(78f, 8f, 10f));
        Swing(armR, armRBase, new Vector3(78f, -8f, -10f));
        LookHead(-40f, -step * 4f);
    }

    void PoseBrake(float timer, float speed)
    {
        float u = Mathf.Clamp01(timer / 0.36f);
        gait += Time.deltaTime * Mathf.Lerp(14f, 18f, Mathf.Clamp01(speed / 6f));
        float step = Mathf.Sin(gait);
        visuals.localRotation = Quaternion.Euler(Mathf.Lerp(0f, 45f, u), 0f, 0f);
        Swing(legL, legLBase, new Vector3(-32f * step, 0f, 0f));
        Swing(legR, legRBase, new Vector3(32f * step, 0f, 0f));
        Swing(armL, armLBase, new Vector3(Mathf.Lerp(16f, 70f, u), 0f, 6f));
        Swing(armR, armRBase, new Vector3(Mathf.Lerp(18f, 70f, u), 0f, -6f));
        LookHead(Mathf.Lerp(0f, -36f, u), 0f);
    }

    void PoseJump(float crouch)
    {
        Swing(legL, legLBase, new Vector3(18f, 0f, -4f));
        Swing(legR, legRBase, new Vector3(18f, 0f, 4f));
        Swing(armL, armLBase, new Vector3(-12f, 0f, 72f));
        Swing(armR, armRBase, new Vector3(-12f, 0f, -72f));
        visuals.localPosition = visualsBase + new Vector3(0f, -0.04f * crouch, 0f);
        LookHead(-12f, 0f);
    }

    void PoseTuck()
    {
        Swing(legL, legLBase, new Vector3(-78f, 0f, 8f));
        Swing(legR, legRBase, new Vector3(-78f, 0f, -8f));
        Swing(armL, armLBase, new Vector3(55f, 10f, 24f));
        Swing(armR, armRBase, new Vector3(55f, -10f, -24f));
        visuals.localPosition = visualsBase + new Vector3(0f, -0.06f, 0.04f);
        LookHead(8f, 0f);
    }

    void PoseHeavyLand(float air)
    {
        float u = Mathf.InverseLerp(2.5f, 3f, air);
        Swing(legL, legLBase, new Vector3(16f * u, 0f, -6f));
        Swing(legR, legRBase, new Vector3(72f * u, 0f, 8f));
        Swing(armL, armLBase, new Vector3(24f * u, 0f, 16f));
        Swing(armR, armRBase, new Vector3(10f * u, -16f, -10f));
        visuals.localRotation = Quaternion.Euler(22f * u, 0f, 0f);
        visuals.localPosition = visualsBase + new Vector3(0f, -0.16f * u, 0.05f * u);
        LookHead(28f * u, 0f);
    }

    void PoseSlide(float slide)
    {
        float lean = slide > 0f ? 1f : Mathf.Clamp01(1f + slide);
        visuals.localRotation = Quaternion.Euler(-68f * lean, 0f, 0f);
        visuals.localPosition = visualsBase + new Vector3(0f, -0.22f * lean, 0f);
        Swing(legL, legLBase, new Vector3(-74f * lean, 0f, 0f));
        Swing(legR, legRBase, new Vector3(68f * lean, 0f, 0f));
        Swing(armL, armLBase, new Vector3(62f * lean, 0f, 18f));
        Swing(armR, armRBase, new Vector3(-36f * lean, -12f, -8f));
        LookHead(36f * lean, 0f);
    }

    void PoseDash(float dash)
    {
        if (dash < 0f)
        {
            float back = Mathf.Clamp01(-dash / 0.18f);
            visuals.localRotation = Quaternion.Euler(12f * back, 0f, 0f);
            Swing(armL, armLBase, new Vector3(-70f * back, 0f, 8f));
            Swing(armR, armRBase, new Vector3(-80f * back, 0f, -6f));
            Swing(legL, legLBase, new Vector3(16f * back, 0f, 0f));
            Swing(legR, legRBase, new Vector3(22f * back, 0f, 0f));
            LookHead(6f * back, 0f);
            return;
        }

        if (dash < 0.5f)
        {
            visuals.localPosition = visualsBase + new Vector3(0f, -0.32f, 0f);
            visuals.localRotation = Quaternion.Euler(28f, 0f, 0f);
            Swing(legL, legLBase, new Vector3(54f, 0f, 22f));
            Swing(legR, legRBase, new Vector3(54f, 0f, -22f));
            Swing(armL, armLBase, new Vector3(-74f, 16f, 28f));
            Swing(armR, armRBase, new Vector3(-74f, -16f, -28f));
            LookHead(26f, 0f);
            return;
        }

        if (dash < 0.9f)
        {
            gait += Time.deltaTime * 22f;
            float step = Mathf.Sin(gait);
            visuals.localPosition = visualsBase + new Vector3(0f, -0.16f, 0.1f);
            visuals.localRotation = Quaternion.Euler(24f, 0f, 0f);
            Swing(legL, legLBase, new Vector3(-42f * step, 0f, 4f));
            Swing(legR, legRBase, new Vector3(42f * step, 0f, -4f));
            Swing(armL, armLBase, new Vector3(84f, 0f, 8f));
            Swing(armR, armRBase, new Vector3(-96f, 0f, -6f));
            LookHead(-8f, 0f);
            return;
        }

        visuals.localRotation = Quaternion.Euler(18f, 0f, 0f);
        visuals.localPosition = visualsBase + new Vector3(0f, -0.08f, 0.12f);
        Swing(armL, armLBase, new Vector3(-102f, 6f, 4f));
        Swing(armR, armRBase, new Vector3(-108f, -4f, -4f));
        Swing(legL, legLBase, new Vector3(20f, 0f, 0f));
        Swing(legR, legRBase, new Vector3(28f, 0f, 0f));
        LookHead(12f, 0f);
    }

    void PoseAttack(float swing, int combo)
    {
        float u = swing * swing * (3f - 2f * swing);
        Vector3 armFrom;
        Vector3 armTo;
        float yawFrom;
        float yawTo;
        float pitch;
        if (combo >= 3)
        {
            armFrom = new Vector3(36f, -18f, -36f);
            armTo = new Vector3(-158f, 36f, 48f);
            yawFrom = 18f;
            yawTo = -24f;
            pitch = Mathf.Lerp(18f, -8f, u);
            Swing(legL, legLBase, new Vector3(Mathf.Lerp(28f, 6f, u), 0f, 0f));
            Swing(legR, legRBase, new Vector3(Mathf.Lerp(34f, 4f, u), 0f, 0f));
        }
        else if (combo == 2)
        {
            armFrom = new Vector3(8f, 48f, 78f);
            armTo = new Vector3(-8f, -62f, -82f);
            yawFrom = -18f;
            yawTo = 32f;
            pitch = 8f;
            Swing(legL, legLBase, new Vector3(10f, 0f, 0f));
            Swing(legR, legRBase, new Vector3(14f, 0f, 0f));
        }
        else
        {
            armFrom = new Vector3(-150f, -28f, -46f);
            armTo = new Vector3(42f, 36f, 74f);
            yawFrom = 28f;
            yawTo = -34f;
            pitch = 12f;
            Swing(legL, legLBase, new Vector3(12f, 0f, -4f));
            Swing(legR, legRBase, new Vector3(8f, 0f, 4f));
        }

        visuals.localRotation = Quaternion.Euler(pitch, Mathf.Lerp(yawFrom, yawTo, u), 0f);
        Swing(armR, armRBase, Vector3.Lerp(armFrom, armTo, u));
        Swing(armL, armLBase, new Vector3(22f, 0f, 16f));
        LookHead(-pitch * 0.35f, Mathf.Lerp(-yawFrom, -yawTo, u) * 0.25f);
    }

    void PoseGuard()
    {
        visuals.localPosition = visualsBase + new Vector3(0f, -0.06f, 0f);
        visuals.localRotation = Quaternion.Euler(10f, -8f, 0f);
        Swing(armR, armRBase, new Vector3(-138f, -48f, -72f));
        Swing(armL, armLBase, new Vector3(24f, 10f, 18f));
        Swing(legL, legLBase, new Vector3(20f, 0f, -6f));
        Swing(legR, legRBase, new Vector3(-18f, 0f, 8f));
        LookHead(16f, 8f);
    }

    void PoseKnock()
    {
        Swing(legL, legLBase, new Vector3(-48f, 0f, 10f));
        Swing(legR, legRBase, new Vector3(-48f, 0f, -10f));
        Swing(armL, armLBase, new Vector3(-20f, 0f, 64f));
        Swing(armR, armRBase, new Vector3(-20f, 0f, -64f));
        LookHead(-8f, 0f);
    }

    void PoseFlinch()
    {
        visuals.localRotation = Quaternion.Euler(14f, -10f, 0f);
        Swing(armR, armRBase, new Vector3(-20f, -10f, -8f));
        Swing(armL, armLBase, new Vector3(28f, 0f, 10f));
        Swing(legL, legLBase, new Vector3(12f, 0f, 0f));
        Swing(legR, legRBase, new Vector3(-8f, 0f, 0f));
        LookHead(12f, 6f);
    }

    void LieDown()
    {
        visuals.localRotation = Quaternion.Euler(78f, 0f, 0f);
        visuals.localPosition = new Vector3(0f, 0.22f, 0.28f);
    }

    void Twist(float yaw, float bob)
    {
        visuals.localRotation = Quaternion.Euler(0f, yaw, 0f);
        visuals.localPosition = visualsBase + new Vector3(0f, bob, 0f);
    }

    void LookHead(float pitch, float yaw)
    {
        var rot = Quaternion.Euler(pitch, yaw, 0f);
        Turn(head, headBase, rot);
        Turn(hair, hairBase, rot);
        Turn(hairL, hairLBase, rot);
        Turn(hairR, hairRBase, rot);
        Turn(bow, bowBase, rot);
        Turn(eyeL, eyeLBase, rot);
        Turn(eyeR, eyeRBase, rot);
        Turn(pupilL, pupilLBase, rot);
        Turn(pupilR, pupilRBase, rot);
    }

    static void Place(Transform part, Vector3 position, Quaternion rotation)
    {
        if (part == null)
            return;
        part.localPosition = position;
        part.localRotation = rotation;
    }

    static void Turn(Transform part, Vector3 position, Quaternion rotation)
    {
        if (part == null)
            return;
        part.localRotation = rotation;
        part.localPosition = position;
    }

    static void Swing(Transform limb, Vector3 basePosition, Vector3 euler)
    {
        if (limb == null)
            return;

        var rotation = Quaternion.Euler(euler);
        float half = limb.localScale.y;
        Vector3 hip = basePosition + Vector3.up * half;
        limb.localRotation = rotation;
        limb.localPosition = hip - rotation * (Vector3.up * half);
    }
}
