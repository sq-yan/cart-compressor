using System;
using System.Collections.Generic;
using CartCompressor.Domain;
using ExitGames.Client.Photon;
using Photon.Pun;
using REPOLib.Modules;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;

namespace CartCompressor
{
    // Host decides eligibility. Reliable snapshots carry unscaled baselines so late
    // joiners never treat an already shrunken transform as its original size.
    internal sealed class CargoRuntime : IDisposable
    {
        private sealed class ScaleState
        {
            internal Transform Target;
            internal Vector3 Original;
            internal float Current = 1;
            internal float Desired = 1;
        }
        private readonly CartAttunement attunement = new CartAttunement();
        private readonly Dictionary<int, ScaleState> scales = new Dictionary<int, ScaleState>();
        private readonly Dictionary<int, float> wanted = new Dictionary<int, float>();
        private readonly Dictionary<int, PhysGrabObject> objects = new Dictionary<int, PhysGrabObject>();
        private readonly List<string> handlers = new List<string>();
        private readonly List<int> remove = new List<int>();
        private readonly HashSet<int> knownCarts = new HashSet<int>();
        private readonly HashSet<int> currentCarts = new HashSet<int>();
        private readonly NetworkedEvent snapshot;
        private float nextScan;
        private float nextSync;
        private bool disposed;

        internal CargoRuntime() { snapshot = new NetworkedEvent("Cart Compressor Cargo v2", Receive); }
        private int PlayerLevel(string id)
        {
            var player = SemiFunc.PlayerAvatarGetFromSteamID(id);
            return player == null || player.isDisabled || player.deadSet || Plugin.Upgrade == null ? 0 : Plugin.Upgrade.GetLevel(id);
        }
        internal void Tick()
        {
            if (GameManager.instance == null || RunManager.instance == null) return;
            bool host = SemiFunc.IsMasterClientOrSingleplayer();
            if (host && Time.unscaledTime >= nextScan)
            {
                nextScan = Time.unscaledTime + .1f;
                Scan();
                if (Time.unscaledTime >= nextSync) { nextSync = Time.unscaledTime + .5f; Broadcast(); }
            }
            Animate();
        }
        private void Scan()
        {
            wanted.Clear(); objects.Clear(); currentCarts.Clear();
            if (Plugin.Enabled.Value)
            foreach (var cart in UnityEngine.Object.FindObjectsByType<PhysGrabCart>(FindObjectsSortMode.None))
            {
                int cartId = cart.GetInstanceID();
                currentCarts.Add(cartId); knownCarts.Add(cartId);
                var body = cart.GetComponent<PhysGrabObject>();
                if (body == null) continue;
                handlers.Clear();
                foreach (var grabber in body.playerGrabbing)
                    handlers.Add(grabber != null && grabber.playerAvatar != null ? grabber.playerAvatar.steamID : null);
                var contacts = body.GetComponent<CartContacts>();
                if (contacts == null) contacts = body.gameObject.AddComponent<CartContacts>();
                contacts.Append(handlers);
                float factor = UpgradeBalance.Factor(attunement.Observe(cartId, handlers, PlayerLevel));
                if (factor >= 1 || cart.itemsInCart == null) continue;
                foreach (var item in cart.itemsInCart)
                {
                    if (!Supported(item)) continue;
                    int id = item.GetInstanceID();
                    // If two cart volumes overlap, the weaker effect wins deterministically.
                    if (!wanted.TryGetValue(id, out float existing) || factor > existing) wanted[id] = factor;
                    objects[id] = item;
                }
            }
            remove.Clear();
            foreach (int id in knownCarts) if (!currentCarts.Contains(id)) remove.Add(id);
            foreach (int id in remove) { knownCarts.Remove(id); attunement.Remove(id); }
            foreach (var state in scales.Values) state.Desired = 1;
            foreach (var pair in wanted) SetTarget(objects[pair.Key].transform, pair.Value, null);
        }
        private static bool Supported(PhysGrabObject item, bool hostPhysics = true)
        {
            if (item == null || item.dead || item.rb == null || (hostPhysics && item.rb.isKinematic)) return false;
            if (item.GetComponent<ValuableObject>() == null || item.GetComponent<ItemAttributes>() != null) return false;
            if (item.GetComponent<PhysGrabCart>() != null || item.GetComponent<PlayerDeathHead>() != null || item.GetComponentInParent<Enemy>() != null) return false;
            var mounting = item.GetComponent<SemiMounting>();
            return mounting == null; // Vehicle/mountable transforms have special ownership; exclude them.
        }
        private void SetTarget(Transform target, float factor, Vector3? baseline)
        {
            int id = target.gameObject.GetInstanceID();
            if (!scales.TryGetValue(id, out var state))
            {
                Vector3 original = baseline ?? target.localScale;
                float current = baseline.HasValue && original.sqrMagnitude > .000001f
                    ? Mathf.Clamp(target.localScale.magnitude / original.magnitude, .65f, 1) : 1;
                state = new ScaleState { Target = target, Original = original, Current = current };
                scales[id] = state;
            }
            if (baseline.HasValue) state.Original = baseline.Value;
            state.Desired = Mathf.Clamp(factor, .65f, 1);
        }
        private void Animate()
        {
            remove.Clear();
            foreach (var pair in scales)
            {
                var state = pair.Value;
                if (state.Target == null) { remove.Add(pair.Key); continue; }
                state.Current = Mathf.MoveTowards(state.Current, state.Desired,
                    Time.deltaTime * (state.Desired > state.Current ? 4f : .8f));
                state.Target.localScale = state.Original * state.Current;
                if (state.Current == 1 && state.Desired == 1) remove.Add(pair.Key);
            }
            foreach (int id in remove) scales.Remove(id);
        }
        private void Broadcast()
        {
            if (!SemiFunc.IsMultiplayer()) return;
            var rows = new List<object>();
            foreach (var state in scales.Values)
            {
                if (state.Target == null || state.Desired >= 1) continue;
                var view = state.Target.GetComponent<PhotonView>();
                if (view == null || view.ViewID == 0) continue;
                rows.Add(new object[] { view.ViewID, state.Desired, state.Original });
            }
            snapshot.RaiseEvent(new Hashtable { { "rows", rows.ToArray() } }, NetworkingEvents.RaiseOthers, SendOptions.SendReliable);
        }
        private void Receive(EventData data)
        {
            if (disposed || PhotonNetwork.MasterClient == null || data.Sender != PhotonNetwork.MasterClient.ActorNumber) return;
            if (data.CustomData is not Hashtable payload || payload["rows"] is not object[] rows) return;
            foreach (var state in scales.Values) state.Desired = 1;
            foreach (var raw in rows)
            {
                if (raw is not object[] row || row.Length != 3 || row[0] is not int id || row[1] is not float factor || row[2] is not Vector3 baseline) continue;
                if (float.IsNaN(factor) || factor < .65f || factor >= 1 || !Finite(baseline)) continue;
                var view = PhotonView.Find(id);
                if (view != null && Supported(view.GetComponent<PhysGrabObject>(), false)) SetTarget(view.transform, factor, baseline);
            }
        }
        private static bool Finite(Vector3 value) =>
            !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
            !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
            !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        internal void Reset()
        {
            foreach (var state in scales.Values) if (state.Target != null) state.Target.localScale = state.Original;
            scales.Clear(); wanted.Clear(); objects.Clear(); attunement.Clear(); knownCarts.Clear();
        }
        public void Dispose() { disposed = true; Reset(); }
    }

    public sealed class CartContacts : MonoBehaviour
    {
        private readonly Dictionary<PlayerAvatar, float> recent = new Dictionary<PlayerAvatar, float>();
        private readonly List<PlayerAvatar> expired = new List<PlayerAvatar>();
        private void OnCollisionEnter(Collision collision) => Observe(collision);
        private void OnCollisionStay(Collision collision) => Observe(collision);
        private void Observe(Collision collision)
        {
            var player = collision.collider.GetComponentInParent<PlayerAvatar>();
            if (player != null) recent[player] = Time.time;
        }
        internal void Append(List<string> players)
        {
            expired.Clear();
            foreach (var pair in recent)
                if (pair.Key == null || Time.time - pair.Value > .12f) expired.Add(pair.Key);
                else players.Add(pair.Key.steamID);
            foreach (var player in expired) recent.Remove(player);
        }
    }
}
