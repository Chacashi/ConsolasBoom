using System;
using HouseEscape.Core.Timeline;
using Unity.Netcode;
using UnityEngine;

namespace HouseEscape.Networking.Timeline
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkMatchTimeline : NetworkBehaviour, IMatchTimelineSource
    {
        [SerializeField] private bool startMatchOnServerSpawn = true;

        private readonly NetworkVariable<NetworkMatchTimelineSnapshot> _snapshot =
            new NetworkVariable<NetworkMatchTimelineSnapshot>(
                default,
                NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);

        private MatchTimelineMachine _machine;
        private NetworkManager _owningNetworkManager;
        private MatchTimelineSnapshot _publishedSnapshot;

        public MatchTimelineSnapshot Snapshot => _publishedSnapshot;

        public double SynchronizedTime => _owningNetworkManager != null
            ? _owningNetworkManager.ServerTime.Time
            : 0d;

        public event Action<MatchTimelineSnapshot> SnapshotChanged;

        public override void OnNetworkSpawn()
        {
            _owningNetworkManager = NetworkObject.NetworkManager;
            _snapshot.OnValueChanged += HandleReplicatedSnapshotChanged;

            if (IsServer)
            {
                _machine = new MatchTimelineMachine(new UniformLightPhaseDurationSampler());
                // A NetworkVariable retains its local value across despawn. Clear it before
                // an optional manual start so a respawn cannot expose the previous match.
                _snapshot.Value = default;
                if (startMatchOnServerSpawn)
                {
                    BeginMatchOnServer();
                }
            }

            PublishSnapshot(_snapshot.Value.ToCoreSnapshot());
        }

        public override void OnNetworkDespawn()
        {
            _snapshot.OnValueChanged -= HandleReplicatedSnapshotChanged;
            _machine = null;
            _owningNetworkManager = null;
            PublishSnapshot(default);
        }

        public void BeginMatchOnServer()
        {
            if (!IsSpawned || !IsServer || _machine == null)
            {
                throw new InvalidOperationException("Only a spawned server timeline can begin the match.");
            }

            if (_machine.Snapshot.IsStarted)
            {
                return;
            }

            _machine.Start(_owningNetworkManager.ServerTime.Time);
            _snapshot.Value = new NetworkMatchTimelineSnapshot(_machine.Snapshot);
        }

        /// <summary>
        /// Server-only: ends the running match early with an outcome. Completion makes the match
        /// inactive for every consumer of <see cref="MatchTimelineAvailability"/> (generators
        /// cancel, the monster sleeps, avatar input stops).
        /// </summary>
        public bool EndMatchOnServer(MatchOutcome outcome)
        {
            if (!IsSpawned || !IsServer || _machine == null)
            {
                throw new InvalidOperationException("Only a spawned server timeline can end the match.");
            }

            if (!_machine.EndEarly(outcome, _owningNetworkManager.ServerTime.Time))
            {
                return false;
            }

            _snapshot.Value = new NetworkMatchTimelineSnapshot(_machine.Snapshot);
            return true;
        }

        private void Update()
        {
            if (!IsSpawned || !IsServer || _machine == null)
            {
                return;
            }

            if (_machine.Advance(_owningNetworkManager.ServerTime.Time))
            {
                _snapshot.Value = new NetworkMatchTimelineSnapshot(_machine.Snapshot);
            }
        }

        private void HandleReplicatedSnapshotChanged(
            NetworkMatchTimelineSnapshot previous,
            NetworkMatchTimelineSnapshot current)
        {
            PublishSnapshot(current.ToCoreSnapshot());
        }

        private void PublishSnapshot(MatchTimelineSnapshot snapshot)
        {
            if (_publishedSnapshot.Equals(snapshot))
            {
                return;
            }

            _publishedSnapshot = snapshot;
            SnapshotChanged?.Invoke(_publishedSnapshot);
        }
    }
}
