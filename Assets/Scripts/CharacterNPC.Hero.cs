using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

/// <summary>A finger pose: curl per finger (0 straight .. 1 fist), thumb across the palm, and spread.</summary>
[Serializable]
public struct Fingers
{
    public float thumb, index, middle, ring, pinky, thumbAcross, spread;

    public Fingers(float thumb, float index, float middle, float ring, float pinky, float thumbAcross, float spread = 0f)
    { this.thumb = thumb; this.index = index; this.middle = middle; this.ring = ring; this.pinky = pinky; this.thumbAcross = thumbAcross; this.spread = spread; }

    public static readonly Fingers Relaxed = new Fingers(0.18f, 0.2f, 0.27f, 0.33f, 0.4f, 0.25f, 0.1f);
    /// <summary>Under a candle dish: fingers spread flat beneath it, the thumb over the rim.</summary>
    public static readonly Fingers Dish = new Fingers(0.42f, 0.16f, 0.2f, 0.24f, 0.3f, 0.12f, 0.35f);
    /// <summary>A book held upright from behind: fingers flat on the back cover, the thumb round the spine.</summary>
    public static readonly Fingers BookBack = new Fingers(0.3f, 0.12f, 0.14f, 0.18f, 0.24f, 0.55f, 0.15f);
    /// <summary>Fingertips hooked over the top of a book on a shelf.</summary>
    public static readonly Fingers Hook = new Fingers(0.2f, 0.55f, 0.6f, 0.68f, 0.75f, 0.2f, 0.05f);
    /// <summary>A book gripped by the spine edge: thumb on the cover, fingers under it.</summary>
    public static readonly Fingers Edge = new Fingers(0.25f, 0.3f, 0.34f, 0.4f, 0.46f, 0.7f, 0.08f);
    /// <summary>Index and thumb pinching a page corner, the other fingers tucked.</summary>
    public static readonly Fingers Pinch = new Fingers(0.45f, 0.42f, 0.62f, 0.7f, 0.78f, 0.85f, 0f);
    public static readonly Fingers Flat = new Fingers(0.05f, 0.05f, 0.05f, 0.06f, 0.08f, 0.05f, 0.2f);

    public float Curl(int f) { return f == 0 ? thumb : f == 1 ? index : f == 2 ? middle : f == 3 ? ring : pinky; }

    public static Fingers Lerp(Fingers a, Fingers b, float t)
    {
        return new Fingers(Mathf.Lerp(a.thumb, b.thumb, t), Mathf.Lerp(a.index, b.index, t), Mathf.Lerp(a.middle, b.middle, t), Mathf.Lerp(a.ring, b.ring, t),
            Mathf.Lerp(a.pinky, b.pinky, t), Mathf.Lerp(a.thumbAcross, b.thumbAcross, t), Mathf.Lerp(a.spread, b.spread, t));
    }
}

/// <summary>Where a hand should be: a palm pose in the world (the palm frame: +Z along the fingers, +Y out of the palm).</summary>
public class HandGoal
{
    public Func<Pose> palm;
    public Vector3 elbow = new Vector3(0.75f, -1f, -0.35f);   // elbow direction in body space (x is mirrored for the left arm)
    public Fingers fingers = Fingers.Relaxed;

    public static HandGoal Body(Transform body, Vector3 pos, Quaternion rot, Fingers f)
    { return new HandGoal { palm = () => new Pose(body.TransformPoint(pos), body.rotation * rot), fingers = f }; }
    public static HandGoal On(Transform prop, Pose grip, Fingers f)
    { return new HandGoal { palm = () => new Pose(prop.TransformPoint(grip.position), prop.rotation * grip.rotation), fingers = f }; }
    public static HandGoal World(Pose p, Fingers f) { return new HandGoal { palm = () => p, fingers = f }; }
    public HandGoal Elbow(Vector3 bodyDir) { elbow = bodyDir; return this; }
}

/// <summary>
/// Hero performance layers for a character who is filmed in close-up (the Curator). Every other character keeps the
/// simpler layers in CharacterNPC.cs. One owner and one order, every frame after the motion-capture clips:
///   1. locomotion: ONE authority. The script moves the character; the walk clip plays in place at the rate that makes
///      its stance foot match that ground speed (calibrated on this skeleton at this scale). No root motion.
///      Speed has acceleration limits, the body leans with acceleration and into turns.
///   2. feet: a planted foot stays where it was put (two-bone leg IK), turning on the spot is done in steps, and the
///      pelvis lowers if a planted foot would otherwise be out of reach. Each landing is a contact event (sound).
///   3. spine lean, then head and eyes: an eyeline chosen by the scene (not the camera), accents from the speech.
///   4. hands: wrist position AND orientation onto authored grips (palm frame), elbow direction, a reach limit, a path
///      that keeps clear of the body and the furniture, finger poses blended with the hand. A prop held in a hand
///      follows that hand (after IK), so contact holds for the whole motion, and changing owner never moves it.
///   5. face: visemes timed to the audio actually playing (Resources/VO/Visemes, from Tools/lipsync), blinks that
///      really close the lids, gaze, brows, a resting expression.
/// Coroutines in the scenes only set goals; nothing else writes these bones.
/// </summary>
public partial class CharacterNPC
{
    HeroRig hero;
    public bool IsHero { get { return hero != null; } }

    /// <summary>Turns on the hero layers for this character (call once, after Create). Returns false if the model is not a Rocketbox avatar.</summary>
    public bool EnableHero()
    {
        if (hero != null) return true;
        if (!rbClips || rHead == null || rPelvis == null || rHand[0] == null || rHand[1] == null) return false;
        hero = new HeroRig();
        HeroCalibrate();
        lookTarget = null;
        freezeFacing = true;
        return true;
    }

    // ===================================================================== state
    class FootState
    {
        public bool planted, stepping;
        public Vector3 pos; public Quaternion rot;              // planted pose (world)
        public Vector3 from, to; public Quaternion rFrom, rTo; public float t, dur;
        public float w;                                          // IK weight
        public Vector3 lastLanding;
    }

    class HandState
    {
        public HandGoal goal;
        public bool on;                     // IK wanted
        public float w;                     // IK weight 0..1
        public Pose fromLocal;              // where the palm was when the goal changed (body space)
        public float t = 1f, dur = 0.01f;   // progress towards the goal
        public float lift;                  // path arc height for this move
        public Fingers fingersFrom, fingersNow;
        public Pose lastPalm;
        public Vector3 elbowNow = new Vector3(0.75f, -1f, -0.35f);
    }

    class Finger { public Transform bone; public Quaternion bind; public Vector3 axis; public float maxDeg; }

    class HeroProp
    {
        public Transform t; public int hand = -1; public Pose offset;          // pose of the prop in the palm frame
        public Pose offsetFrom; public float slideT = 1f, slideDur = 0.01f;    // re-gripping: the hand slides along it
        public Pose offsetTo;
    }

    class HeroRig
    {
        // locomotion
        public float speed, prevSpeed, accel, stanceSpeed = 1.2f, cycle = 1f, yawRate, prevYaw, phase = 0.38f;
        public float[] contactFrom = { 0f, 0.5f }, contactTo = { 0.5f, 1f };
        public bool walking;
        public float leanFwd, leanSide;
        // feet
        public FootState[] feet = { new FootState(), new FootState() };
        public float legLen = 0.9f, ankleHeight = 0.08f, pelvisDrop, stepCooldown;
        public int lastStepFoot = 1;
        // hands
        public HandState[] hands = { new HandState(), new HandState() };
        public Pose[] palmInHand = new Pose[2];
        public List<Finger>[] fingers = { new List<Finger>(), new List<Finger>() };
        public Transform[] clav = new Transform[2];
        public readonly List<HeroProp> props = new List<HeroProp>();
        public readonly List<Bounds> obstacles = new List<Bounds>();   // world boxes the hands and props keep above / out of
        // head and eyes
        public Vector3 eyeline; public bool hasEyeline; public float headYaw, headPitch, headRoll, headYawV, headPitchV;
        public float gestureT = 10f, gestureAmt, gestureKind;
        // face
        public HeroFace face;
    }

    // ===================================================================== calibration
    void HeroCalibrate()
    {
        var h = hero;
        // ---- the walk clip on this skeleton at this scale: stance-foot speed and when each foot is planted
        if (cWalk != null && graph.IsValid())
        {
            const int N = 96;
            var lp = new Vector3[2, N];
            mixer.SetInputWeight(0, 0f); mixer.SetInputWeight(1, 1f); mixer.SetInputWeight(2, 0f);
            double keep = pWalk.GetTime();
            for (int s = 0; s < N; s++)
            {
                pWalk.SetTime(cWalk.length * s / N);
                graph.Evaluate(0f);
                for (int i = 0; i < 2; i++) lp[i, s] = transform.InverseTransformPoint(rFoot[i].position);
            }
            pWalk.SetTime(keep);
            float dt = cWalk.length / N;
            // stance speed: how fast a firmly planted foot travels backwards (the median, both feet)
            var speeds = new List<float>();
            float[] minY = new float[2], range = new float[2];
            for (int i = 0; i < 2; i++)
            {
                float lo = float.MaxValue, hi = float.MinValue;
                for (int s = 0; s < N; s++) { lo = Mathf.Min(lo, lp[i, s].y); hi = Mathf.Max(hi, lp[i, s].y); }
                minY[i] = lo; range[i] = hi - lo;
                for (int s = 0; s < N; s++)
                {
                    int s2 = (s + 1) % N;
                    if (lp[i, s].y < lo + range[i] * 0.1f && lp[i, s2].y < lo + range[i] * 0.1f) speeds.Add(-(lp[i, s2].z - lp[i, s].z) / dt);
                }
            }
            speeds.Sort();
            float stance = speeds.Count > 0 ? speeds[speeds.Count / 2] : 0f;
            if (stance > 0.4f && stance < 3f) h.stanceSpeed = stance;
            // contact: low AND moving back with the ground (within 0.4 m/s); the longest such run in the cycle
            for (int i = 0; i < 2; i++)
            {
                var ok = new bool[N];
                for (int s = 0; s < N; s++)
                {
                    int s2 = (s + 1) % N;
                    Vector3 v = (lp[i, s2] - lp[i, s]) / dt;
                    ok[s] = lp[i, s].y < minY[i] + range[i] * 0.3f && (v + new Vector3(0, 0, h.stanceSpeed)).magnitude < 0.4f * Mathf.Max(1f, h.stanceSpeed);
                }
                int bestStart = 0, bestLen = 0;
                for (int s = 0; s < N; s++)
                {
                    if (ok[(s + N - 1) % N] || !ok[s]) continue;      // start of a run
                    int len = 0; while (len < N && ok[(s + len) % N]) len++;
                    if (len > bestLen) { bestLen = len; bestStart = s; }
                }
                if (bestLen == 0) { bestStart = i == 0 ? 0 : N / 2; bestLen = N / 2; }
                // over the whole planted run the foot must travel back exactly as far as the body goes forward:
                // that average is the stance speed that makes the stride wheel exact
                float back = -(lp[i, (bestStart + bestLen) % N].z - lp[i, bestStart].z);
                if (bestLen > 4 && back > 0.05f) speeds.Add(back / (bestLen * dt));
                // lock only the middle of the planted run; heel strike and toe roll stay with the animation
                int trim = Mathf.RoundToInt(bestLen * 0.15f);
                h.contactFrom[i] = (bestStart + trim) / (float)N;
                h.contactTo[i] = (bestStart + bestLen - trim) / (float)N;      // may exceed 1: wraps
            }
            // the stance speed from the planted runs (the last two entries added above) when they exist
            if (speeds.Count >= 2) { float avg = (speeds[speeds.Count - 1] + speeds[speeds.Count - 2]) * 0.5f; if (avg > 0.4f && avg < 3f) h.stanceSpeed = avg; }
            h.ankleHeight = Mathf.Min(minY[0], minY[1]);
            h.cycle = cWalk.length;
            mixer.SetInputWeight(0, 1f); mixer.SetInputWeight(1, 0f);
            graph.Evaluate(0f);
            Debug.Log(string.Format("[Hero] {0}: walk clip stance speed {1:F2} m/s (cycle {2:F2} s), contact L {3:F2}-{4:F2} R {5:F2}-{6:F2}, ankle {7:F3} m",
                name, h.stanceSpeed, h.cycle, h.contactFrom[0], h.contactTo[0], h.contactFrom[1], h.contactTo[1], h.ankleHeight));
        }
        h.legLen = Vector3.Distance(rThigh[0].position, rCalf[0].position) + Vector3.Distance(rCalf[0].position, rFoot[0].position);
        // ---- hands: the palm frame in hand-bone space, and every finger joint's curl axis (from the model's bind pose)
        var prefab = style != null && !string.IsNullOrEmpty(style.rb) ? Resources.Load<GameObject>("Characters/RB/" + style.rb) : null;
        var bind = new Dictionary<string, Transform>();
        if (prefab != null) foreach (var t in prefab.GetComponentsInChildren<Transform>(true)) if (!bind.ContainsKey(t.name)) bind[t.name] = t;
        string[] sd = { "L", "R" };
        for (int i = 0; i < 2; i++)
        {
            Transform hand = rHand[i];
            string pre = "Bip01 " + sd[i] + " Finger";
            Transform mid = FindDeep(hand, pre + "2"), idx = FindDeep(hand, pre + "1"), pinky = FindDeep(hand, pre + "4");
            h.clav[i] = FindDeep(rbRoot, "Bip01 " + sd[i] + " Clavicle");
            if (mid == null || idx == null || pinky == null) continue;
            // positions of child bones relative to the hand are rigid, so this holds in any pose
            Vector3 fwd = hand.InverseTransformDirection(mid.position - hand.position);
            float palmLen = fwd.magnitude; fwd.Normalize();
            Vector3 across = hand.InverseTransformDirection(idx.position - pinky.position);
            across = (across - Vector3.Dot(across, fwd) * fwd).normalized;              // towards the thumb
            bool right = i == 1;
            Vector3 normal = (right ? 1f : -1f) * Vector3.Cross(fwd, across);            // out of the palm
            // which side is really the palm: the resting fingers curl towards it
            Transform tip = FindDeep(mid, pre + "22");
            if (tip != null)
            {
                Vector3 curl = hand.InverseTransformDirection(tip.position - mid.position);
                if (Vector3.Dot(curl - Vector3.Dot(curl, fwd) * fwd, normal) < -0.004f * palmLen) normal = -normal;
            }
            float scale = Mathf.Max(0.5f, palmLen / 0.095f);
            h.palmInHand[i] = new Pose(fwd * palmLen * 0.5f + normal * 0.014f * scale, Quaternion.LookRotation(fwd, normal));
            // finger joints
            h.fingers[i].Clear();
            Quaternion handBind = bind.ContainsKey(hand.name) ? bind[hand.name].rotation : hand.rotation;
            Vector3 normalBindW = handBind * normal, fwdBindW = handBind * fwd;
            for (int f = 0; f < 5; f++)
            {
                Transform j0 = FindDeep(hand, pre + f), j1 = j0 != null ? FindDeep(j0, pre + f + "1") : null, j2 = j1 != null ? FindDeep(j1, pre + f + "2") : null;
                Transform[] chain = { j0, j1, j2 };
                float[] maxDeg = f == 0 ? new[] { 28f, 40f, 55f } : new[] { 65f, 95f, 70f };
                for (int k = 0; k < 3; k++)
                {
                    var b = chain[k]; if (b == null) continue;
                    Transform bb = bind.ContainsKey(b.name) ? bind[b.name] : b;
                    Transform child = k < 2 ? chain[k + 1] : null;
                    Transform cb = child != null && bind.ContainsKey(child.name) ? bind[child.name] : child;
                    Vector3 seg = cb != null ? (cb.position - bb.position) : (bb.position - (bind.ContainsKey(chain[Mathf.Max(0, k - 1)].name) ? bind[chain[Mathf.Max(0, k - 1)].name].position : bb.position));
                    if (seg.sqrMagnitude < 1e-8f) seg = fwdBindW;
                    seg.Normalize();
                    // curling turns each segment towards the palm side
                    Vector3 axisW = f == 0 ? Vector3.Cross(seg, normalBindW + fwdBindW * 0.4f) : Vector3.Cross(seg, normalBindW);
                    if (axisW.sqrMagnitude < 1e-6f) axisW = Vector3.Cross(fwdBindW, normalBindW);
                    h.fingers[i].Add(new Finger { bone = b, bind = bb.localRotation, axis = Quaternion.Inverse(bb.rotation) * axisW.normalized, maxDeg = maxDeg[k] });
                }
            }
            h.hands[i].fingersNow = h.hands[i].fingersFrom = Fingers.Relaxed;
            h.hands[i].lastPalm = PalmPose(i);
        }
        // ---- his own materials (eyes that catch the light, hands without the suit's tint), then the face
        HeroMaterials();
        h.face = new HeroFace();
        h.face.Setup(this);
        // ---- feet start planted where they are
        for (int i = 0; i < 2; i++) { var f = h.feet[i]; f.planted = true; f.pos = rFoot[i].position; f.rot = rFoot[i].rotation; f.w = 1f; f.lastLanding = f.pos; }
        h.prevYaw = transform.eulerAngles.y;
    }

    // ===================================================================== the frame
    void HeroFrame(float dt)
    {
        var h = hero;
        // ---- 1. locomotion: the clip mix and rate follow the scripted ground speed
        float spd = h.speed;
        h.accel = Mathf.Lerp(h.accel, (spd - h.prevSpeed) / dt, Mathf.Clamp01(dt * 6f)); h.prevSpeed = spd;
        float yaw = transform.eulerAngles.y;
        h.yawRate = Mathf.Lerp(h.yawRate, Mathf.DeltaAngle(h.prevYaw, yaw) / dt, Mathf.Clamp01(dt * 8f)); h.prevYaw = yaw;
        float wantWalk = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.04f, 0.32f, spd));
        walkBlend = Mathf.MoveTowards(walkBlend, wantWalk, dt * 3.5f);
        mixer.SetInputWeight(0, 1f - walkBlend); mixer.SetInputWeight(1, walkBlend); mixer.SetInputWeight(2, 0f);
        // a stride wheel: the walk clip's phase follows the distance walked, so its stance foot travels back exactly
        // as fast as the body goes forward (one locomotion authority: the script moves him, the clip only poses him)
        h.phase = Mathf.Repeat(h.phase + spd * dt / (h.stanceSpeed * h.cycle), 1f);
        pWalk.SetSpeed(0f);
        pWalk.SetTime(h.phase * cWalk.length);
        moving = spd > 0.04f;

        // ---- 2. body lean (acceleration, turning, scripted) then the feet
        h.leanFwd = Mathf.Lerp(h.leanFwd, Mathf.Clamp(h.accel * 2.2f, -3f, 4f), Mathf.Clamp01(dt * 5f));
        h.leanSide = Mathf.Lerp(h.leanSide, Mathf.Clamp(-h.yawRate * spd * 0.03f, -3f, 3f), Mathf.Clamp01(dt * 4f));
        leanNow = Mathf.MoveTowards(leanNow, lean, dt * 30f);
        HeroFeet(dt);
        if (rSpine != null)
            rSpine.rotation = Quaternion.AngleAxis(leanNow + h.leanFwd, transform.right) * Quaternion.AngleAxis(h.leanSide, transform.forward) * rSpine.rotation;

        // ---- 3. head and eyes
        HeroHead(dt);
        // ---- 4. hands, fingers, props
        for (int i = 0; i < 2; i++) HeroHand(i, dt);
        foreach (var p in h.props) HeroPropFollow(p, dt);
        // ---- 5. face
        h.face.Apply(this, dt);
    }

    // ===================================================================== locomotion API
    /// <summary>
    /// Walks to a point (parent space) like a person: turns towards it in steps if it is behind, eases into the
    /// stride, slows to a stop on the spot (no snapping: whatever is left is under 3 cm and the feet keep their
    /// contact), then optionally turns to a final heading. Ends when the feet have settled.
    /// </summary>
    public IEnumerator HeroWalk(Vector3 localTarget, float cruise, float endYaw = float.NaN, float accel = 0.75f, float decel = 0.6f)
    {
        var h = hero;
        if (h == null) { yield return WalkTo(localTarget, cruise, -1f, false); yield break; }
        int token = ++walkToken;
        walking = true; h.walking = true;
        if (walkBlend < 0.05f) h.phase = Mathf.Repeat(h.contactTo[0] + 0.03f, 1f);     // the left foot leaves first
        Vector3 Target() { return transform.parent != null ? transform.parent.TransformPoint(localTarget) : localTarget; }
        Vector3 to = Target() - transform.position; to.y = 0;
        if (to.magnitude > 0.08f && Vector3.Angle(transform.forward, to) > 40f)
            yield return HeroTurnCo(Quaternion.LookRotation(to).eulerAngles.y, token);
        float elapsed = 0f;
        while (token == walkToken)
        {
            if (this == null) yield break;
            float dt = Time.deltaTime;
            if (dt <= 0f) { yield return null; continue; }          // paused
            elapsed += dt;
            to = Target() - transform.position; to.y = 0;
            float dist = to.magnitude;
            if (dist < 0.03f && h.speed < 0.06f) break;
            // turn along the path; a tighter turn the slower he goes
            if (dist > 0.05f)
            {
                var want = Quaternion.LookRotation(to.normalized);
                float maxTurn = (40f + 70f * Mathf.Clamp01(h.speed / 0.8f)) * dt;
                transform.rotation = Quaternion.RotateTowards(transform.rotation, want, maxTurn);
            }
            float ang = Vector3.Angle(transform.forward, to);
            // speed: accelerate to cruise, brake so that he stops exactly at the point (v^2 = 2 a d)
            float vMax = Mathf.Min(cruise, Mathf.Sqrt(2f * decel * Mathf.Max(0f, dist - 0.02f)));
            if (ang > 60f) vMax = Mathf.Min(vMax, 0.15f);
            h.speed = h.speed < vMax ? Mathf.Min(vMax, h.speed + accel * dt) : Mathf.Max(vMax, h.speed - decel * 1.6f * dt);
            // always along the facing: never sideways
            Vector3 step = transform.forward * Mathf.Min(h.speed * dt, dist);
            step.y = 0;
            transform.position += step;
            if (elapsed > 60f) break;
            yield return null;
        }
        h.speed = 0f;
        walking = false; moving = false;
        LastWalk = token == walkToken ? WalkResult.Arrived : WalkResult.Cancelled;
        if (!float.IsNaN(endYaw) && token == walkToken) yield return HeroTurnCo(endYaw, token);
        // let the feet settle (closing step)
        for (float t = 0; t < 1.2f && (h.feet[0].stepping || h.feet[1].stepping || walkBlend > 0.05f); t += Time.deltaTime) yield return null;
        h.walking = false;
    }

    /// <summary>Turns on the spot to a world heading, stepping as he turns.</summary>
    public IEnumerator HeroTurn(float yaw) { return HeroTurnCo(yaw, walkToken); }

    IEnumerator HeroTurnCo(float yaw, int token)
    {
        var h = hero;
        float start = transform.eulerAngles.y, total = Mathf.DeltaAngle(start, yaw);
        float dur = Mathf.Lerp(0.55f, 1.6f, Mathf.Abs(total) / 180f);
        for (float t = 0; t < dur; )
        {
            if (this == null || token != walkToken) yield break;
            if (Time.deltaTime > 0f) t += Time.deltaTime;
            float k = t / dur, e = k * k * (3f - 2f * k);
            transform.rotation = Quaternion.Euler(0f, start + total * e, 0f);
            yield return null;
        }
        for (float t = 0; t < 0.8f && (h.feet[0].stepping || h.feet[1].stepping); t += Time.deltaTime) yield return null;
    }

    /// <summary>Raised by a foot landing (0 = left, 1 = right) with the world position and how hard it landed (0..1).</summary>
    public event Action<int, Vector3, float> FootContact;

    // ===================================================================== feet
    bool InContact(int i, float phase)
    {
        float a = hero.contactFrom[i], b = hero.contactTo[i];
        return b > 1f ? (phase >= a || phase < b - 1f) : (phase >= a && phase < b);
    }

    void HeroFeet(float dt)
    {
        var h = hero;
        float phase = cWalk != null ? (float)(pWalk.GetTime() / cWalk.length) % 1f : 0f;
        bool walkMode = walkBlend > 0.5f;
        h.stepCooldown -= dt;
        Vector3 up = Vector3.up;
        for (int i = 0; i < 2; i++)
        {
            var f = h.feet[i];
            Vector3 animPos = rFoot[i].position; Quaternion animRot = rFoot[i].rotation;
            Vector3 groundPos = new Vector3(animPos.x, transform.position.y + h.ankleHeight, animPos.z);
            if (walkMode)
            {
                bool c = InContact(i, phase);
                if (c && !f.planted) Land(i, groundPos, animRot, 0.8f);
                else if (!c && f.planted) f.planted = false;
                // never drag the body over a foot it cannot reach: let that foot go with the animation instead
                if (f.planted && Vector3.Distance(rThigh[i].position, f.pos) > h.legLen * 1.01f) f.planted = false;
                f.stepping = false;
            }
            else if (!f.stepping)
            {
                if (!f.planted) Land(i, groundPos, animRot, 0.25f);
                else
                {
                    // the animation (and the body's turn) wants this foot elsewhere: step there
                    Vector3 d = groundPos - f.pos; d.y = 0;
                    float yawErr = Quaternion.Angle(animRot, f.rot);
                    var other = h.feet[1 - i];
                    if ((d.magnitude > 0.11f * (style != null ? style.height / 1.75f : 1f) || yawErr > 24f) && !other.stepping && other.planted && h.stepCooldown <= 0f
                        && (h.lastStepFoot != i || (d.magnitude > 0.2f || yawErr > 40f)))
                    {
                        f.stepping = true; f.planted = false; f.t = 0f;
                        f.dur = Mathf.Lerp(0.3f, 0.42f, Mathf.Clamp01(d.magnitude / 0.35f));
                        f.from = f.pos; f.rFrom = f.rot; f.to = groundPos + d.normalized * Mathf.Min(0.02f, d.magnitude * 0.1f); f.rTo = animRot;
                        h.lastStepFoot = i; h.stepCooldown = 0.12f;
                    }
                }
            }
            if (f.stepping)
            {
                f.t += dt / f.dur;
                if (f.t >= 1f) { f.stepping = false; Land(i, f.to, f.rTo, 0.4f); }
            }
            // IK weight: full while planted or stepping, the clip's own swing otherwise
            float wantW = f.planted || f.stepping ? 1f : 0f;
            f.w = Mathf.MoveTowards(f.w, wantW, dt * (wantW > f.w ? 14f : 9f));
        }
        // pelvis: lower it a little if a planted foot is beyond the leg's reach
        float drop = 0f;
        for (int i = 0; i < 2; i++)
        {
            var f = h.feet[i]; if (f.w <= 0.01f) continue;
            Vector3 target = FootTarget(f);
            float d = Vector3.Distance(rThigh[i].position, target) - h.legLen * 0.985f;
            drop = Mathf.Max(drop, d * f.w);
        }
        h.pelvisDrop = Mathf.Lerp(h.pelvisDrop, Mathf.Clamp(drop, 0f, 0.035f), Mathf.Clamp01(dt * 10f));
        if (h.pelvisDrop > 0.0005f) rPelvis.position -= Vector3.up * h.pelvisDrop;
        for (int i = 0; i < 2; i++)
        {
            var f = h.feet[i]; if (f.w <= 0.001f) continue;
            Vector3 target = FootTarget(f);
            Quaternion trot = f.stepping ? Quaternion.Slerp(f.rFrom, f.rTo, Ease(f.t)) : f.rot;
            Quaternion u0 = rThigh[i].rotation, c0 = rCalf[i].rotation, f0 = rFoot[i].rotation;
            Vector3 knee = transform.forward + transform.right * (i == 0 ? sideL : -sideL) * 0.15f;
            Vector3 footFwd = trot * Quaternion.Inverse(f0) * transform.forward;
            Reach(rThigh[i], rCalf[i], rFoot[i], target, (knee + new Vector3(footFwd.x, 0, footFwd.z)).normalized);
            rFoot[i].rotation = trot;
            float w = f.w;
            if (w < 0.999f)
            {
                rThigh[i].rotation = Quaternion.Slerp(u0, rThigh[i].rotation, w);
                rCalf[i].rotation = Quaternion.Slerp(c0, rCalf[i].rotation, w);
                rFoot[i].rotation = Quaternion.Slerp(f0, rFoot[i].rotation, w);
            }
        }
    }

    static float Ease(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }

    Vector3 FootTarget(FootState f)
    {
        if (!f.stepping) return f.pos;
        float e = Ease(f.t);
        float arc = Mathf.Sin(Mathf.Clamp01(f.t) * Mathf.PI) * 0.06f * (style != null ? style.height / 1.75f : 1f);
        return Vector3.Lerp(f.from, f.to, e) + Vector3.up * arc;
    }


    void Land(int i, Vector3 pos, Quaternion rot, float strength)
    {
        var f = hero.feet[i];
        f.planted = true; f.pos = pos; f.rot = rot;
        float moved = Vector3.Distance(pos, f.lastLanding);
        f.lastLanding = pos;
        if (moved > 0.04f && FootContact != null) FootContact(i, pos, Mathf.Clamp01(strength * Mathf.Lerp(0.6f, 1f, hero.speed)));
    }

    // ===================================================================== head and eyes
    /// <summary>Where he looks (world point). The scene sets it to the investigator, a prop, the window... never "the camera" by default.</summary>
    public void Eyeline(Vector3 world) { if (hero == null) return; hero.eyeline = world; hero.hasEyeline = true; }
    public void EyelineOff() { if (hero != null) hero.hasEyeline = false; }

    void HeroHead(float dt)
    {
        var h = hero;
        float wantYaw = 0f, wantPitch = 0f;
        Vector3 gaze = GazeTarget();
        if (h.hasEyeline)
        {
            Vector3 l = transform.InverseTransformPoint(gaze) - transform.InverseTransformPoint(rHead.position);
            wantYaw = Mathf.Clamp(Mathf.Atan2(l.x, l.z) * Mathf.Rad2Deg, -60f, 60f);
            wantPitch = Mathf.Clamp(-Mathf.Atan2(l.y, new Vector2(l.x, l.z).magnitude) * Mathf.Rad2Deg, -25f, 28f);
        }
        if (walkBlend > 0.3f) { wantYaw *= 0.5f; wantPitch *= 0.5f; }
        // the head follows the eyes with a lag (the eyes get there first), smoothly, without overshoot
        h.headYaw = Mathf.SmoothDamp(h.headYaw, wantYaw * 0.8f, ref h.headYawV, 0.32f, 200f, dt);
        h.headPitch = Mathf.SmoothDamp(h.headPitch, wantPitch * 0.75f, ref h.headPitchV, 0.36f, 200f, dt);
        // gestures on the stressed words: a small nod or a tilt, then back
        h.gestureT += dt;
        float g = h.gestureT < 0.18f ? Ease(h.gestureT / 0.18f) : Mathf.Clamp01(1f - (h.gestureT - 0.18f) / 0.5f);
        g = Ease(g) * h.gestureAmt;
        float nod = h.gestureKind < 0.6f ? 2.4f * g : 0.8f * g, tilt = h.gestureKind >= 0.6f ? (h.gestureKind < 0.8f ? 2.2f : -2.2f) * g : 0f;
        // slow, small drift (noise, not a sine wave)
        float t = Time.time;
        float driftY = (Mathf.PerlinNoise(t * 0.17f, 3.1f) - 0.5f) * 1.6f, driftP = (Mathf.PerlinNoise(7.7f, t * 0.13f) - 0.5f) * 1.2f;
        Vector3 rightAxis = transform.right;
        if (rNeck != null) rNeck.rotation = Quaternion.AngleAxis(h.headYaw * 0.4f, transform.up) * Quaternion.AngleAxis(h.headPitch * 0.35f, rightAxis) * rNeck.rotation;
        rHead.rotation = Quaternion.AngleAxis(h.headYaw * 0.6f + driftY, transform.up)
                       * Quaternion.AngleAxis(h.headPitch * 0.65f + nod + driftP, rightAxis)
                       * Quaternion.AngleAxis(tilt, transform.forward) * rHead.rotation;
    }

    /// <summary>A gesture on a stressed word (strength 0..1). Called by the face layer from the speech's accents.</summary>
    void Accent(float strength, float kind)
    {
        hero.gestureT = 0f; hero.gestureAmt = Mathf.Clamp01(strength); hero.gestureKind = kind;
        if (hero.face != null) hero.face.BrowAccent(strength * 0.8f);
    }

    Vector3 GazeTarget()
    {
        var h = hero;
        if (h.hasEyeline) return h.eyeline;
        return rHead.position + transform.forward * 3f;
    }

    /// <summary>Point between the eyes as animated now (for framing, focus and eyelines).</summary>
    public Vector3 EyePoint()
    {
        if (hero != null && hero.face != null && hero.face.eyeL != null && hero.face.eyeR != null)
            return (hero.face.eyeL.position + hero.face.eyeR.position) * 0.5f;
        return rHead != null ? rHead.position + Vector3.up * 0.08f : transform.position + Vector3.up * 1.7f;
    }

    /// <summary>Top of the head (for headroom in framing).</summary>
    public Vector3 HeadTop() { return EyePoint() + (rHead != null ? rHead.rotation * hero.face.headUpLocal : Vector3.up) * 0.13f * (style != null ? style.height / 1.75f : 1f); }

    public Transform HeadBone { get { return rHead; } }
    /// <summary>For the capture log: speed, walk blend, clip phase, pelvis drop, each foot (P planted, S stepping, - swinging) with IK weight.</summary>
    public string HeroDebug()
    {
        if (hero == null) return "";
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        float phase = cWalk != null ? (float)(pWalk.GetTime() / cWalk.length) % 1f : 0f;
        string F(int i) { var f = hero.feet[i]; return (f.planted ? "P" : f.stepping ? "S" : "-") + f.w.ToString("0.0", inv); }
        return string.Format(inv, "{0:F2} {1:F2} {2:F2} {3:F3} {4} {5}", hero.speed, walkBlend, phase, hero.pelvisDrop, F(0), F(1));
    }
    public Transform HandBone(int i) { return rHand[i]; }
    public Transform FootBone(int i) { return rFoot[i]; }
    public float Speed { get { return hero != null ? hero.speed : curSpeed; } }

    // ===================================================================== hands
    /// <summary>The palm frame of a hand as it is now (world).</summary>
    public Pose PalmPose(int i)
    {
        var ph = hero.palmInHand[i];
        return new Pose(rHand[i].TransformPoint(ph.position), rHand[i].rotation * ph.rotation);
    }

    /// <summary>Moves a hand to a goal over `dur` seconds along a path that clears the body and the furniture.</summary>
    public void HandTo(int i, HandGoal goal, float dur, float lift = 0.05f)
    {
        var hs = hero.hands[i];
        Pose now = hs.w > 0.01f ? hs.lastPalm : PalmPose(i);
        hs.fromLocal = new Pose(transform.InverseTransformPoint(now.position), Quaternion.Inverse(transform.rotation) * now.rotation);
        hs.fingersFrom = hs.fingersNow;
        hs.goal = goal; hs.on = true; hs.t = 0f; hs.dur = Mathf.Max(0.05f, dur); hs.lift = lift;
        if (hs.w < 0.01f) hs.w = 0.01f;
    }

    /// <summary>Coroutine form: waits until the hand has arrived.</summary>
    public IEnumerator HandToCo(int i, HandGoal goal, float dur, float lift = 0.05f)
    {
        HandTo(i, goal, dur, lift);
        var hs = hero.hands[i];
        while (hs.goal == goal && hs.t < 1f) yield return null;
    }

    /// <summary>Lets the hand go back to the clip's pose over `dur`.</summary>
    public void HandFree(int i, float dur)
    {
        var hs = hero.hands[i];
        // fade out from where the hand IS (not from where its last move started, which made the target jump back)
        Pose now = hs.w > 0.01f ? hs.lastPalm : PalmPose(i);
        hs.fromLocal = new Pose(transform.InverseTransformPoint(now.position), Quaternion.Inverse(transform.rotation) * now.rotation);
        hs.on = false; hs.dur = Mathf.Max(0.05f, dur);
        hs.fingersFrom = hs.fingersNow; hs.t = 0f;
    }

    public bool HandArrived(int i) { var hs = hero.hands[i]; return hs.t >= 1f; }

    /// <summary>A world box that hands and held props keep out of (table top, shelf boards, the body is built in).</summary>
    public void AddObstacle(Bounds b) { if (hero != null) hero.obstacles.Add(b); }
    public void ClearObstacles() { if (hero != null) hero.obstacles.Clear(); }

    void HeroHand(int i, float dt)
    {
        var h = hero; var hs = h.hands[i];
        if (hs.dur > 0f) hs.t = Mathf.Min(1f, hs.t + dt / hs.dur);
        float e = Ease(hs.t);
        hs.w = hs.on ? Mathf.Max(hs.w, Mathf.Min(1f, 0.01f + e)) : Mathf.Min(hs.w, 1f - e);
        hs.fingersNow = Fingers.Lerp(hs.fingersFrom, hs.goal != null && hs.on ? hs.goal.fingers : Fingers.Relaxed, e);
        if (hs.w <= 0.001f || hs.goal == null) { hs.lastPalm = PalmPose(i); return; }
        Pose goal = hs.goal.palm();
        Pose from = new Pose(transform.TransformPoint(hs.fromLocal.position), transform.rotation * hs.fromLocal.rotation);
        Pose target;
        if (hs.on)
        {
            // a curved path: out from the body and up over obstacles in the middle of the move
            Vector3 mid = (from.position + goal.position) * 0.5f;
            Vector3 outDir = mid - (rSpine != null ? rSpine.position : transform.position + Vector3.up); outDir.y = 0;
            Vector3 bulge = (outDir.sqrMagnitude > 1e-6f ? outDir.normalized * 0.04f : Vector3.zero) + Vector3.up * hs.lift;
            float s = Mathf.Sin(e * Mathf.PI);
            target = new Pose(Vector3.Lerp(from.position, goal.position, e) + bulge * s * Mathf.Clamp01(Vector3.Distance(from.position, goal.position) / 0.25f),
                              Quaternion.Slerp(from.rotation, goal.rotation, e));
        }
        else target = from;
        target.position = Clear(i, target.position);
        hs.lastPalm = target;
        // the wrist target from the palm target
        Quaternion handRot = target.rotation * Quaternion.Inverse(h.palmInHand[i].rotation);
        Vector3 wrist = target.position - handRot * h.palmInHand[i].position;
        // reach limit: a little help from the collar bone, then the arm stops short rather than stretching
        float upper = Vector3.Distance(rArm[i].position, rFore[i].position), lower = Vector3.Distance(rFore[i].position, rHand[i].position);
        float reach = (upper + lower) * 0.985f, dist = Vector3.Distance(rArm[i].position, wrist);
        if (dist > reach && h.clav[i] != null)
        {
            // weighted like the rest of the arm: unweighted, it stayed at full strength through a release fade and
            // switched off the frame the weight reached zero (the shoulder, and the hand with it, snapped ~10 cm)
            Vector3 toW = (wrist - h.clav[i].position).normalized, toA = (rArm[i].position - h.clav[i].position).normalized;
            float extra = Mathf.Clamp((dist - reach) * 60f, 0f, 10f);
            h.clav[i].rotation = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(toA, toW), extra / 30f * Ease(hs.w)) * h.clav[i].rotation;
        }
        Quaternion u0 = rArm[i].rotation, f0 = rFore[i].rotation, h0 = rHand[i].rotation;
        float s2 = i == 0 ? sideL : -sideL;
        hs.elbowNow = Vector3.Lerp(hs.elbowNow, hs.goal.elbow, Mathf.Clamp01(dt * 6f));
        Vector3 elbowW = transform.TransformDirection(new Vector3(hs.elbowNow.x * s2, hs.elbowNow.y, hs.elbowNow.z));
        Reach(rArm[i], rFore[i], rHand[i], wrist, elbowW);
        // wrist orientation: share the roll between forearm and hand (there are no twist bones), limit the bend
        Quaternion rel = Quaternion.Inverse(rFore[i].rotation) * handRot;
        Vector3 foreAxisLocal = Quaternion.Inverse(rFore[i].rotation) * (rHand[i].position - rFore[i].position).normalized;
        SwingTwist(rel, foreAxisLocal, out Quaternion swing, out Quaternion twist);
        twist.ToAngleAxis(out float twAng, out Vector3 twAxis);
        if (twAng > 180f) twAng -= 360f;
        rFore[i].rotation = rFore[i].rotation * Quaternion.AngleAxis(twAng * 0.5f, twAxis);
        rel = Quaternion.Inverse(rFore[i].rotation) * handRot;
        SwingTwist(rel, Quaternion.Inverse(rFore[i].rotation) * (rHand[i].position - rFore[i].position).normalized, out swing, out twist);
        swing.ToAngleAxis(out float swAng, out Vector3 swAxis);
        if (swAng > 180f) swAng -= 360f;
        float lim = 75f;
        if (Mathf.Abs(swAng) > lim) swing = Quaternion.AngleAxis(Mathf.Sign(swAng) * lim, swAxis);
        rHand[i].rotation = rFore[i].rotation * swing * twist;
        float w = Ease(hs.w);
        if (w < 0.999f)
        {
            rArm[i].rotation = Quaternion.Slerp(u0, rArm[i].rotation, w);
            rFore[i].rotation = Quaternion.Slerp(f0, rFore[i].rotation, w);
            rHand[i].rotation = Quaternion.Slerp(h0, rHand[i].rotation, w);
        }
        ApplyFingers(i, hs.fingersNow, 1f);
    }

    static void SwingTwist(Quaternion q, Vector3 axis, out Quaternion swing, out Quaternion twist)
    {
        Vector3 r = new Vector3(q.x, q.y, q.z);
        Vector3 p = Vector3.Project(r, axis);
        twist = new Quaternion(p.x, p.y, p.z, q.w);
        float m = Mathf.Sqrt(twist.x * twist.x + twist.y * twist.y + twist.z * twist.z + twist.w * twist.w);
        twist = m < 1e-6f ? Quaternion.identity : new Quaternion(twist.x / m, twist.y / m, twist.z / m, twist.w / m);
        swing = q * Quaternion.Inverse(twist);
    }

    /// <summary>Keeps a hand position out of the torso and above the furniture tops it is over.</summary>
    Vector3 Clear(int i, Vector3 p)
    {
        var h = hero;
        // torso: a capsule from the pelvis to the neck
        if (rPelvis != null && rNeck != null)
        {
            Vector3 a = rPelvis.position + Vector3.up * 0.1f, b = rNeck.position - Vector3.up * 0.06f;
            float r = 0.2f * (style != null ? style.height / 1.75f : 1f);
            Vector3 ab = b - a; float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude);
            Vector3 c = a + ab * t; Vector3 d = p - c; d.y = 0;
            if (d.magnitude < r)
            {
                Vector3 dir = d.sqrMagnitude > 1e-6f ? d.normalized : transform.forward;
                p = c + dir * r + Vector3.up * (p.y - c.y);
            }
        }
        foreach (var b in h.obstacles)
        {
            if (p.x > b.min.x - 0.02f && p.x < b.max.x + 0.02f && p.z > b.min.z - 0.02f && p.z < b.max.z + 0.02f && p.y < b.max.y + 0.03f && p.y > b.min.y - 0.1f)
                p.y = b.max.y + 0.03f;
        }
        return p;
    }

    void ApplyFingers(int i, Fingers pose, float w)
    {
        var list = hero.fingers[i];
        // the finger chain list is in order: thumb (3), index (3), middle (3), ring (3), pinky (3)
        for (int k = 0; k < list.Count; k++)
        {
            var fj = list[k];
            int finger = k / 3, joint = k % 3;
            float curl = pose.Curl(finger);
            float deg = curl * fj.maxDeg;
            if (finger == 0 && joint == 0) deg = pose.thumbAcross * fj.maxDeg;
            Quaternion want = fj.bind * Quaternion.AngleAxis(deg, fj.axis);
            if (joint == 0 && finger > 0 && Mathf.Abs(pose.spread) > 0.001f)
                want = want * Quaternion.AngleAxis((finger - 2.5f) * pose.spread * -5f, Vector3.Cross(fj.axis, Vector3.right).sqrMagnitude > 0.1f ? Vector3.Cross(fj.axis, Vector3.right).normalized : Vector3.up);
            fj.bone.localRotation = w >= 0.999f ? want : Quaternion.Slerp(fj.bone.localRotation, want, Mathf.Max(w, 0.35f));
        }
    }

    // ===================================================================== props
    HeroProp PropOf(Transform t)
    {
        foreach (var p in hero.props) if (p.t == t) return p;
        var np = new HeroProp { t = t };
        hero.props.Add(np);
        return np;
    }

    /// <summary>The hand takes the prop where it is: from now on the prop follows that hand. No movement at the switch.</summary>
    public void Attach(Transform prop, int hand)
    {
        var p = PropOf(prop);
        // against the hand as it really is (the follow uses the same), not the IK target: if the reach fell short,
        // measuring from the target made the prop jump by the shortfall the moment it was taken
        Pose palm = PalmPose(hand);
        Quaternion inv = Quaternion.Inverse(palm.rotation);
        p.offset = new Pose(inv * (prop.position - palm.position), inv * prop.rotation);
        p.offsetTo = p.offset; p.slideT = 1f;
        p.hand = hand;
    }

    /// <summary>The prop sits in the hand at a grip (the palm frame in the prop's space) - for a scene that starts with it held.</summary>
    public void Hold(Transform prop, int hand, Pose grip)
    {
        var p = PropOf(prop);
        Quaternion inv = Quaternion.Inverse(grip.rotation);
        p.offset = new Pose(-(inv * grip.position), inv);
        p.offsetTo = p.offset; p.slideT = 1f;
        p.hand = hand;
    }

    /// <summary>The hand is at the goal at once (before the first frame of a scene, never during one).</summary>
    public void HandSnap(int i, HandGoal goal)
    {
        var hs = hero.hands[i];
        hs.goal = goal; hs.on = true; hs.t = 1f; hs.dur = 0.05f; hs.w = 1f; hs.lift = 0f;
        Pose g = goal.palm();
        hs.fromLocal = new Pose(transform.InverseTransformPoint(g.position), Quaternion.Inverse(transform.rotation) * g.rotation);
        hs.fingersFrom = hs.fingersNow = goal.fingers;
        hs.lastPalm = g;
    }

    /// <summary>The prop's offset as a grip (palm frame in prop space) - the inverse of Hold.</summary>
    public static Pose GripOffset(Pose grip) { Quaternion inv = Quaternion.Inverse(grip.rotation); return new Pose(-(inv * grip.position), inv); }

    /// <summary>The prop stays where it is now (world), owned by `parent`.</summary>
    public void Detach(Transform prop, Transform parent)
    {
        var p = PropOf(prop);
        p.hand = -1;
        prop.SetParent(parent, true);
    }

    /// <summary>Re-grip: the hand slides along the prop until the prop sits at `offset` in the palm (no jump).</summary>
    public void Regrip(Transform prop, Pose offset, float dur)
    {
        var p = PropOf(prop);
        p.offsetFrom = p.offset; p.offsetTo = offset; p.slideT = 0f; p.slideDur = Mathf.Max(0.05f, dur);
    }

    /// <summary>The palm pose that puts an attached prop at a world pose (for placing it exactly).</summary>
    public Pose PalmFor(Transform prop, Pose propWorld)
    {
        var p = PropOf(prop);
        Pose o = p.slideT < 1f ? p.offsetTo : p.offset;
        Quaternion r = propWorld.rotation * Quaternion.Inverse(o.rotation);
        return new Pose(propWorld.position - r * o.position, r);
    }

    public Pose PropOffset(Transform prop) { return PropOf(prop).offset; }

    void HeroPropFollow(HeroProp p, float dt)
    {
        if (p.t == null || p.hand < 0) return;
        if (p.slideT < 1f)
        {
            p.slideT = Mathf.Min(1f, p.slideT + dt / p.slideDur);
            float e = Ease(p.slideT);
            p.offset = new Pose(Vector3.Lerp(p.offsetFrom.position, p.offsetTo.position, e), Quaternion.Slerp(p.offsetFrom.rotation, p.offsetTo.rotation, e));
        }
        Pose palm = PalmPose(p.hand);
        p.t.SetPositionAndRotation(palm.position + palm.rotation * p.offset.position, palm.rotation * p.offset.rotation);
    }

    // ===================================================================== face API
    /// <summary>The resting expression: "neutral", "knowing", "smile", "grave", "intent", "blow".</summary>
    public void Expression(string name, float blend = 0.6f) { if (hero != null) hero.face.SetExpression(name, blend); }
    public void Blink() { if (hero != null) hero.face.BlinkNow(); }
    /// <summary>Eyes only (the head stays): a glance at a world point for `seconds`.</summary>
    public void Glance(Vector3 world, float seconds) { if (hero != null) hero.face.Glance(world, seconds); }
    public bool HeroSpeaking { get { return hero != null && hero.face.Speaking; } }
    internal AudioSource VoiceSource { get { return voice; } }
    internal Transform HeadForFace { get { return rHead; } }
    internal Transform JawForFace { get { return rJaw; } }
    internal Transform RootForFace { get { return rbRoot; } }
    internal void AccentFromFace(float s, float kind) { Accent(s, kind); }
    internal Vector3 GazeForFace() { return GazeTarget(); }
}
