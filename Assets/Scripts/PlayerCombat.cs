using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Sword swings, shared blood, and the respawn that follows a loss.
/// The server confirms each hit so both players see the same blood.
/// </summary>
public class PlayerCombat : NetworkBehaviour
{
    public const int MaxBlood = 100;
    public const int SwingDamage = 20;
    const float SwingTime = 0.5f;
    const float HitReach = 1.7f;
    const float ServerReach = 2.25f;

    public bool IsDown => IsSpawned && down.Value;
    public bool IsDefending => IsSpawned && defending.Value;

    readonly NetworkVariable<int> blood = new NetworkVariable<int>(
        MaxBlood,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    readonly NetworkVariable<bool> down = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    readonly NetworkVariable<float> reviveAt = new NetworkVariable<float>(
        -1f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    readonly NetworkVariable<bool> defending = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner);

    readonly NetworkVariable<float> syncedSwing = new NetworkVariable<float>(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner);

    readonly NetworkVariable<int> syncedCombo = new NetworkVariable<int>(
        1,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner);

    readonly NetworkVariable<int> reaction = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    readonly NetworkVariable<float> reactUntil = new NetworkVariable<float>(
        -1f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public float Swing01 => IsSpawned ? (IsOwner ? swing : syncedSwing.Value) : 0f;
    public int ComboStep => IsSpawned ? (IsOwner ? combo : syncedCombo.Value) : 1;
    public int Reaction => IsSpawned ? reaction.Value : 0;

    JumpButton attackButton;
    Transform visuals;
    Transform armR;
    Transform armL;
    Quaternion armRBase = Quaternion.identity;
    Quaternion armLBase = Quaternion.identity;
    Transform sword;
    Transform slash;
    Transform weaponHand;
    Transform weaponArm;
    Transform barRoot;
    Transform barFill;
    TextMesh plate;
    string plateLabel = "Player";
    Text bloodText;
    GameObject hudRoot;
    Image attackImage;
    Color attackReady = new Color(0.72f, 0.16f, 0.18f, 0.94f);
    Renderer[] bodyRenderers;
    MaterialPropertyBlock flashBlock;
    float flash;
    float swing;
    int combo = 1;
    int swingCombo = 1;
    float comboWindow = -99f;
    float nextSwing;
    bool hitSent;
    bool hudReady;
    double nextServerHit;
    double nextPropHit;
    double nextSlam;

    void Awake()
    {
        visuals = transform.Find("Visuals");
        if (visuals != null)
        {
            armR = visuals.Find("ArmR");
            armL = visuals.Find("ArmL");
            if (armR != null)
                armRBase = armR.localRotation;
            if (armL != null)
                armLBase = armL.localRotation;
            bodyRenderers = visuals.GetComponentsInChildren<Renderer>();
            weaponHand = FindBone("HandR");
            weaponArm = FindBone("ForearmR");
        }

        plate = GetComponentInChildren<TextMesh>();
        if (plate != null)
            plateLabel = plate.text;
        flashBlock = new MaterialPropertyBlock();
    }

    public void BindAttackButton(JumpButton button)
    {
        attackButton = button;
        if (button != null)
            attackImage = button.GetComponent<Image>();
    }

    public void SetDefending(bool value)
    {
        if (!IsOwner || !IsSpawned)
            return;
        if (IsDown)
            value = false;
        if (defending.Value != value)
            defending.Value = value;
    }

    public void RequestDoor(int doorId)
    {
        if (!IsOwner || !IsSpawned)
            return;
        ToggleDoorServerRpc(doorId);
    }

    public override void OnNetworkSpawn()
    {
        blood.OnValueChanged += OnBloodChanged;
        BuildSword();
        BuildBar();
        RefreshReadout();
        if (IsServer)
            PushWorld(OwnerClientId);
    }

    public override void OnNetworkDespawn()
    {
        blood.OnValueChanged -= OnBloodChanged;
        if (hudRoot != null)
            Destroy(hudRoot);
    }

    void OnDestroy()
    {
        if (hudRoot != null)
            Destroy(hudRoot);
    }

    public void BreakGround(Vector3 position)
    {
        if (!IsOwner || !IsSpawned || IsDown)
            return;

        BreakGroundServerRpc(position, Random.Range(1, int.MaxValue));
    }

    void Update()
    {
        if (!IsSpawned)
            return;

        if (IsServer && down.Value && reviveAt.Value > 0f && NetworkManager.ServerTime.Time >= reviveAt.Value)
            Revive();

        if (IsServer && reaction.Value != 0 && reactUntil.Value > 0f && NetworkManager.ServerTime.Time >= reactUntil.Value)
        {
            reaction.Value = 0;
            reactUntil.Value = -1f;
        }

        if (IsOwner)
        {
            if (!hudReady)
            {
                CreateHud();
                hudReady = true;
            }

            TickSwing();
        }

        if (flash > 0f)
        {
            flash = Mathf.MoveTowards(flash, 0f, Time.deltaTime * 4f);
            ApplyFlash();
        }

        RefreshReadout();
    }

    void TickSwing()
    {
        if (IsDown)
        {
            swing = 0f;
            combo = 1;
            if (attackButton != null)
                attackButton.Consume();
        }
        else if (swing <= 0f)
        {
            if (!IsDown && Time.time >= nextSwing && AttackPressed())
            {
                var movement = GetComponent<PlayerMovement>();
                if (movement != null && movement.LocksActions)
                {
                    if (attackButton != null)
                        attackButton.Consume();
                }
                else
                {
                    if (movement != null)
                        movement.FaceLookDirection();
                    if (Time.time > comboWindow || combo >= 3)
                        combo = 1;
                    else
                        combo++;
                    comboWindow = Time.time + 3f;
                    swingCombo = combo;
                    if (syncedCombo.Value != combo)
                        syncedCombo.Value = combo;
                    swing = 0.001f;
                    hitSent = false;
                    nextSwing = Time.time + SwingTime;
                }
            }
        }
        else
        {
            swing += Time.deltaTime / SwingTime;
            if (!hitSent && swing >= 0.42f)
            {
                hitSent = true;
                var movement = GetComponent<PlayerMovement>();
                if (movement != null)
                    movement.FaceLookDirection();
                TryHit();
            }

            if (swing >= 1f)
                swing = 0f;
        }

        if (Mathf.Abs(syncedSwing.Value - swing) > 0.02f || (swing == 0f && syncedSwing.Value != 0f))
            syncedSwing.Value = swing;

        if (attackImage != null)
        {
            bool ready = Time.time >= nextSwing && swing <= 0f && !IsDown;
            attackImage.color = ready ? attackReady : attackReady * 0.55f;
        }
    }

    bool AttackPressed()
    {
        bool pressed = attackButton != null && attackButton.Consume();
        bool keyboard = Input.GetKeyDown(KeyCode.F);
        var movement = GetComponent<PlayerMovement>();
        bool tap = movement != null && movement.EmptyTap;
        return pressed || keyboard || tap;
    }

    void TryHit()
    {
        var players = FindObjectsOfType<PlayerCombat>();
        PlayerCombat target = null;
        float best = HitReach;
        for (int i = 0; i < players.Length; i++)
        {
            var other = players[i];
            if (other == null || other == this || !other.IsSpawned || other.IsDown)
                continue;

            Vector3 delta = other.transform.position - transform.position;
            if (Mathf.Abs(delta.y) > 1.7f)
                continue;

            var flat = new Vector3(delta.x, 0f, delta.z);
            float distance = flat.magnitude;
            if (distance > best || distance < 0.05f)
                continue;
            if (Vector3.Dot(transform.forward, flat / distance) < 0.2f)
                continue;

            best = distance;
            target = other;
        }

        if (target != null)
            HitServerRpc(target.NetworkObjectId, swingCombo);

        Breakable prop = FindProp();
        if (prop != null)
            DamagePropServerRpc(prop.Id);
    }

    Breakable FindProp()
    {
        Vector3 origin = transform.position + Vector3.up * 1.15f + transform.forward * 0.8f;
        var hits = Physics.OverlapSphere(origin, 1.05f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        Breakable best = null;
        float bestDistance = 1.15f;
        for (int i = 0; i < hits.Length; i++)
        {
            var prop = hits[i].GetComponentInParent<Breakable>();
            if (prop == null || prop.Blood <= 0)
                continue;
            Vector3 closest = hits[i].ClosestPoint(origin);
            Vector3 delta = closest - transform.position;
            delta.y = 0f;
            if (delta.sqrMagnitude > 0.01f && Vector3.Dot(transform.forward, delta.normalized) < 0.05f)
                continue;
            float distance = Vector3.Distance(origin, closest);
            if (distance >= bestDistance)
                continue;
            bestDistance = distance;
            best = prop;
        }

        return best;
    }

    [ServerRpc]
    void HitServerRpc(ulong targetId, int step)
    {
        if (!IsSpawned || down.Value)
            return;
        if (NetworkManager.ServerTime.Time < nextServerHit)
            return;
        if (!NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(targetId, out var netObject))
            return;

        var target = netObject.GetComponent<PlayerCombat>();
        if (target == null || target == this || target.down.Value)
            return;

        Vector3 delta = target.transform.position - transform.position;
        if (Mathf.Abs(delta.y) > 2f)
            return;
        var flat = new Vector3(delta.x, 0f, delta.z);
        float distance = flat.magnitude;
        if (distance > ServerReach || distance < 0.05f)
            return;
        if (Vector3.Dot(transform.forward, flat / distance) < 0.05f)
            return;

        nextServerHit = NetworkManager.ServerTime.Time + 0.42f;
        int hit = Mathf.Clamp(step, 1, 3);
        target.ApplyHit(transform.position + Vector3.up * 1.05f + flat.normalized * 0.6f, hit);
    }

    void ApplyHit(Vector3 sparkPoint, int step)
    {
        ApplyDamage(SwingDamage, sparkPoint);
        if (!IsServer || down.Value)
            return;

        if (step >= 3)
        {
            reaction.Value = 2;
            reactUntil.Value = (float)NetworkManager.ServerTime.Time + 0.5f;
            KnockClientRpc(2);
            return;
        }

        reaction.Value = 1;
        reactUntil.Value = (float)NetworkManager.ServerTime.Time + 0.2f;
        KnockClientRpc(1);
    }

    void ApplyDamage(int amount, Vector3 sparkPoint)
    {
        if (!IsServer || down.Value)
            return;

        if (defending.Value)
            amount = Mathf.Max(1, amount / 2);

        int next = Mathf.Max(0, blood.Value - amount);
        if (next <= 0)
        {
            down.Value = true;
            reviveAt.Value = (float)NetworkManager.ServerTime.Time + 5f;
        }

        blood.Value = next;
        StruckClientRpc(sparkPoint);
    }

    void Revive()
    {
        blood.Value = MaxBlood;
        down.Value = false;
        reviveAt.Value = -1f;
        RespawnClientRpc(new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new[] { OwnerClientId }
            }
        });
    }

    [ClientRpc]
    void KnockClientRpc(int kind)
    {
        if (!IsOwner)
            return;

        var movement = GetComponent<PlayerMovement>();
        if (movement != null)
            movement.ApplyReaction(kind);
    }

    [ClientRpc]
    void RespawnClientRpc(ClientRpcParams rpcParams = default)
    {
        if (!IsOwner)
            return;

        var movement = GetComponent<PlayerMovement>();
        if (movement != null)
            movement.ReturnToSpawn();
    }

    [ClientRpc]
    void StruckClientRpc(Vector3 point)
    {
        for (int i = 0; i < 7; i++)
        {
            var bit = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bit.name = "HitSpark";
            bit.hideFlags = HideFlags.DontSave;
            Destroy(bit.GetComponent<Collider>());
            bit.transform.position = point;
            bit.transform.localScale = Vector3.one * Random.Range(0.05f, 0.09f);
            PaintRenderer(bit.GetComponent<Renderer>(), new Color(0.95f, 0.18f, 0.16f));
            StartCoroutine(FlySpark(bit.transform, Random.insideUnitSphere * 2.4f + Vector3.up * 1.6f));
        }
    }

    IEnumerator FlySpark(Transform bit, Vector3 velocity)
    {
        float life = 0.35f;
        while (life > 0f && bit != null)
        {
            life -= Time.deltaTime;
            velocity += Vector3.down * 9f * Time.deltaTime;
            bit.position += velocity * Time.deltaTime;
            yield return null;
        }

        if (bit != null)
            Destroy(bit.gameObject);
    }

    [ServerRpc]
    void BreakGroundServerRpc(Vector3 point, int seed)
    {
        if (!IsSpawned || down.Value)
            return;
        if (NetworkManager.ServerTime.Time < nextSlam)
            return;

        nextSlam = NetworkManager.ServerTime.Time + 0.35f;
        var flat = new Vector3(point.x, transform.position.y, point.z);
        if ((flat - transform.position).sqrMagnitude > 16f)
            point = transform.position;

        Shock(point);
        BreakGroundClientRpc(point, seed);
    }

    void Shock(Vector3 point)
    {
        const int damage = 5;
        var players = FindObjectsOfType<PlayerCombat>();
        for (int i = 0; i < players.Length; i++)
        {
            var other = players[i];
            if (other == null || other == this || !other.IsSpawned || other.IsDown)
                continue;

            Vector3 delta = other.transform.position - point;
            delta.y = 0f;
            if (delta.magnitude > GroundBreak.EffectRadius)
                continue;
            if (Mathf.Abs(other.transform.position.y - point.y) > 2.2f)
                continue;

            other.ApplyDamage(damage, other.transform.position + Vector3.up);
        }

        foreach (var pair in Breakable.ById)
        {
            var prop = pair.Value;
            if (prop == null || prop.Blood <= 0)
                continue;
            if (DistanceTo(prop, point) > GroundBreak.EffectRadius)
                continue;

            int next = Mathf.Max(0, Village.BloodOf(prop.Id) - damage);
            Village.SetAuthorityBlood(prop.Id, next);
            PropBloodClientRpc(prop.Id, next);
        }
    }

    static float DistanceTo(Breakable prop, Vector3 point)
    {
        float nearest = Vector3.Distance(prop.transform.position, point);
        var cols = prop.GetComponentsInChildren<Collider>();
        for (int i = 0; i < cols.Length; i++)
        {
            if (cols[i] == null || !cols[i].enabled)
                continue;
            float distance = Vector3.Distance(point, cols[i].ClosestPoint(point));
            if (distance < nearest)
                nearest = distance;
        }

        return nearest;
    }

    [ClientRpc]
    void BreakGroundClientRpc(Vector3 point, int seed)
    {
        GroundBreak.Spawn(point, seed);
    }

    [ServerRpc]
    void DamagePropServerRpc(int propId)
    {
        if (!IsSpawned || down.Value)
            return;
        if (NetworkManager.ServerTime.Time < nextPropHit)
            return;
        if (!Breakable.ById.TryGetValue(propId, out var prop) || prop.Blood <= 0)
            return;
        if (!PropInReach(prop))
            return;

        nextPropHit = NetworkManager.ServerTime.Time + 0.4;
        int next = Mathf.Max(0, Village.BloodOf(propId) - SwingDamage);
        Village.SetAuthorityBlood(propId, next);
        PropBloodClientRpc(propId, next);
    }

    bool PropInReach(Breakable prop)
    {
        Vector3 origin = transform.position + Vector3.up * 1.1f;
        float nearest = 99f;
        var cols = prop.GetComponentsInChildren<Collider>();
        for (int i = 0; i < cols.Length; i++)
        {
            if (cols[i] == null || !cols[i].enabled)
                continue;
            float distance = Vector3.Distance(origin, cols[i].ClosestPoint(origin));
            if (distance < nearest)
                nearest = distance;
        }

        return nearest <= 2.6f;
    }

    [ClientRpc]
    void PropBloodClientRpc(int propId, int bloodValue)
    {
        if (Breakable.ById.TryGetValue(propId, out var prop))
            prop.SetBlood(bloodValue);
    }

    [ServerRpc]
    void ToggleDoorServerRpc(int doorId)
    {
        bool open = Village.ToggleAuthorityDoor(doorId);
        DoorClientRpc(doorId, open);
    }

    [ClientRpc]
    void DoorClientRpc(int doorId, bool open)
    {
        Village.ApplyDoor(doorId, open);
    }

    void PushWorld(ulong clientId)
    {
        var ids = new List<int>();
        var bloods = new List<int>();
        var doors = new List<int>();
        Village.CopyState(ids, bloods, doors);
        SyncWorldClientRpc(ids.ToArray(), bloods.ToArray(), doors.ToArray(), new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new[] { clientId }
            }
        });
    }

    [ClientRpc]
    void SyncWorldClientRpc(int[] ids, int[] bloods, int[] openDoors, ClientRpcParams rpcParams = default)
    {
        if (ids != null && bloods != null)
        {
            int count = Mathf.Min(ids.Length, bloods.Length);
            for (int i = 0; i < count; i++)
            {
                if (Breakable.ById.TryGetValue(ids[i], out var prop))
                    prop.SetBlood(bloods[i]);
            }
        }

        if (openDoors == null)
            return;
        for (int i = 0; i < openDoors.Length; i++)
            Village.ApplyDoor(openDoors[i], true);
    }

    void OnBloodChanged(int previous, int next)
    {
        if (next < previous)
            flash = 1f;
        RefreshReadout();
    }

    void RefreshReadout()
    {
        if (!IsSpawned)
            return;

        int shown = blood.Value;
        float seconds = 0f;
        if (down.Value && reviveAt.Value > 0f)
            seconds = Mathf.Ceil(Mathf.Max(0f, reviveAt.Value - (float)NetworkManager.ServerTime.Time));

        if (plate != null)
        {
            plate.text = down.Value
                ? plateLabel + "\n" + shown + "  " + seconds.ToString("0") + "s"
                : plateLabel + "\n" + shown;
        }

        if (barFill != null)
        {
            float pct = Mathf.Clamp01(shown / (float)MaxBlood);
            barFill.localScale = new Vector3(Mathf.Max(0.02f, 0.92f * pct), 0.09f, 1f);
            barFill.localPosition = new Vector3(-0.46f * (1f - pct), 0f, 0.02f);
            var renderer = barFill.GetComponent<Renderer>();
            if (renderer != null && renderer.sharedMaterial != null)
                renderer.sharedMaterial.color = Color.Lerp(new Color(0.45f, 0.05f, 0.05f), new Color(0.86f, 0.12f, 0.14f), pct);
        }

        if (IsOwner && bloodText != null)
        {
            bloodText.text = down.Value
                ? "Blood 0\nBack in " + seconds.ToString("0")
                : "Blood " + shown;
        }
    }

    void ApplyFlash()
    {
        if (bodyRenderers == null)
            return;

        if (flash <= 0.01f)
        {
            for (int i = 0; i < bodyRenderers.Length; i++)
            {
                if (bodyRenderers[i] != null)
                    bodyRenderers[i].SetPropertyBlock(null);
            }

            return;
        }

        flashBlock.SetColor("_Color", Color.Lerp(Color.white, new Color(1f, 0.25f, 0.22f), flash));
        for (int i = 0; i < bodyRenderers.Length; i++)
        {
            if (bodyRenderers[i] != null)
                bodyRenderers[i].SetPropertyBlock(flashBlock);
        }
    }

    void BuildSword()
    {
        if (visuals == null || sword != null)
            return;

        bool girl = gameObject.name.Contains("Girl");
        sword = new GameObject("Sword").transform;
        sword.SetParent(visuals, false);

        Blade(sword, "Grip", new Vector3(0.055f, 0.14f, 0.055f), new Vector3(0f, 0.07f, 0f), new Color(0.28f, 0.15f, 0.08f));
        Blade(sword, "Guard", new Vector3(0.24f, 0.045f, 0.08f), new Vector3(0f, 0.16f, 0f), new Color(0.78f, 0.64f, 0.28f));
        Blade(sword, "Blade", new Vector3(0.09f, 0.72f, 0.028f), new Vector3(0f, 0.54f, 0f),
            girl ? new Color(0.95f, 0.72f, 0.68f) : new Color(0.82f, 0.88f, 0.92f));

        var slashGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
        slashGo.name = "Slash";
        Destroy(slashGo.GetComponent<Collider>());
        slash = slashGo.transform;
        slash.SetParent(transform, false);
        slash.localScale = new Vector3(0.15f, 0.08f, 0.9f);
        PaintRenderer(slashGo.GetComponent<Renderer>(), new Color(1f, 0.92f, 0.7f));
        slash.gameObject.SetActive(false);
        if (weaponHand != null)
            sword.gameObject.SetActive(false);
    }

    Transform FindBone(string boneName)
    {
        var parts = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < parts.Length; i++)
        {
            if (parts[i].name == boneName)
                return parts[i];
        }

        return null;
    }

    void BuildBar()
    {
        barRoot = new GameObject("BloodBar").transform;
        barRoot.SetParent(transform, false);
        barRoot.localPosition = new Vector3(0f, 2.05f, 0f);

        var back = GameObject.CreatePrimitive(PrimitiveType.Quad);
        back.name = "Back";
        Destroy(back.GetComponent<Collider>());
        back.transform.SetParent(barRoot, false);
        back.transform.localScale = new Vector3(1f, 0.14f, 1f);
        PaintRenderer(back.GetComponent<Renderer>(), new Color(0.12f, 0.05f, 0.05f));

        var fill = GameObject.CreatePrimitive(PrimitiveType.Quad);
        fill.name = "Fill";
        Destroy(fill.GetComponent<Collider>());
        fill.transform.SetParent(barRoot, false);
        PaintRenderer(fill.GetComponent<Renderer>(), new Color(0.86f, 0.12f, 0.14f));
        barFill = fill.transform;
    }

    void LateUpdate()
    {
        if (!IsSpawned)
            return;

        var motion = GetComponent<CharacterMotion>();
        if (motion != null)
            motion.Pose();

        float shown = Swing01;
        bool swinging = shown > 0.001f;
        if (slash != null)
        {
            bool show = swinging && shown > 0.28f && shown < 0.72f && !IsDown;
            slash.gameObject.SetActive(show);
            if (show)
            {
                slash.localPosition = new Vector3(0.15f, 1.05f, 0.85f);
                float yaw = ComboStep >= 3 ? -30f : (ComboStep == 2 ? 10f : -24f);
                slash.localRotation = Quaternion.Euler(70f - Ease(shown) * 90f, yaw, ComboStep == 2 ? 70f : 12f);
            }
        }

        PlaceSword();

        if (barRoot == null)
            return;

        var cam = Camera.main;
        if (cam == null)
            return;

        barRoot.position = transform.position + Vector3.up * 2.05f;
        Vector3 toCamera = cam.transform.position - barRoot.position;
        if (toCamera.sqrMagnitude > 0.001f)
            barRoot.rotation = Quaternion.LookRotation(toCamera, Vector3.up);
    }

    void PlaceSword()
    {
        if (sword == null || visuals == null)
            return;

        if (weaponHand != null)
        {
            var move = GetComponent<PlayerMovement>();
            float dash = move != null ? move.DashPose : 0f;
            bool thrusting = dash > 0.84f || dash < -0.001f;
            bool drawn = ((Swing01 > 0.02f || IsDefending) || thrusting) && !IsDown;
            if (sword.parent != weaponHand)
                sword.SetParent(weaponHand, false);
            Vector3 boneScale = weaponHand.lossyScale;
            sword.localScale = new Vector3(
                Mathf.Abs(boneScale.x) > 0.0001f ? 1f / boneScale.x : 1f,
                Mathf.Abs(boneScale.y) > 0.0001f ? 1f / boneScale.y : 1f,
                Mathf.Abs(boneScale.z) > 0.0001f ? 1f / boneScale.z : 1f);
            Vector3 along = weaponArm != null ? weaponHand.position - weaponArm.position : weaponHand.up;
            if (along.sqrMagnitude < 0.0001f)
                along = weaponHand.up;
            along.Normalize();
            sword.position = weaponHand.position + along * 0.02f;
            sword.rotation = Quaternion.FromToRotation(Vector3.up, along);
            if (sword.gameObject.activeSelf != drawn)
                sword.gameObject.SetActive(drawn);
            return;
        }

        if (armR == null)
            return;

        Vector3 hand = armR.localPosition + armR.localRotation * (Vector3.down * armR.localScale.y);
        sword.localPosition = hand;
        sword.localRotation = armR.localRotation * Quaternion.Euler(180f, 0f, 0f);
    }

    static float Ease(float value)
    {
        return value * value * (3f - 2f * value);
    }

    void CreateHud()
    {
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        hudRoot = new GameObject("BloodHud");
        var canvas = hudRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 21;
        var scaler = hudRoot.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        var go = new GameObject("Blood");
        go.transform.SetParent(hudRoot.transform, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -18f);
        rect.sizeDelta = new Vector2(420f, 80f);
        bloodText = go.AddComponent<Text>();
        bloodText.font = font;
        bloodText.fontSize = 32;
        bloodText.alignment = TextAnchor.UpperCenter;
        bloodText.color = new Color(1f, 0.82f, 0.82f);
        bloodText.raycastTarget = false;
        bloodText.text = "Blood " + MaxBlood;
    }

    static GameObject Blade(Transform parent, string name, Vector3 scale, Vector3 localPosition, Color color)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localScale = scale;
        go.transform.localPosition = localPosition;
        PaintRenderer(go.GetComponent<Renderer>(), color);
        return go;
    }

    static void PaintRenderer(Renderer renderer, Color color)
    {
        if (renderer == null)
            return;
        var shader = Shader.Find("Standard");
        if (shader == null)
            shader = Shader.Find("Diffuse");
        if (shader == null)
            return;

        var material = new Material(shader);
        material.mainTexture = Texture2D.whiteTexture;
        material.color = color;
        material.SetColor("_Color", color);
        if (material.HasProperty("_Glossiness"))
            material.SetFloat("_Glossiness", 0.02f);
        if (material.HasProperty("_Metallic"))
            material.SetFloat("_Metallic", 0f);
        renderer.sharedMaterial = material;
    }
}
