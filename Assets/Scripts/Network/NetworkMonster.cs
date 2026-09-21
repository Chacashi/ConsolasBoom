using UnityEngine;
using Unity.Netcode;
using HouseEscape.Monster;
using HouseEscape.Systems;
using System;

namespace HouseEscape.Network
{
    public class NetworkMonster : NetworkBehaviour
    {
        [Header("Monster Network")]
        [SerializeField] private MonsterAI monsterAI;

        [Header("Network Variables")]
        [SerializeField] private NetworkVariable<float> _health = new NetworkVariable<float>(100f);
        [SerializeField] private NetworkVariable<int> _stateIndex = new NetworkVariable<int>(0);
        [SerializeField] private NetworkVariable<Vector3> _targetPosition = new NetworkVariable<Vector3>(Vector3.zero);

        public NetworkVariable<float> Health => _health;
        public NetworkVariable<int> StateIndex => _stateIndex;
        public NetworkVariable<Vector3> TargetPosition => _targetPosition;

        public event Action OnMonsterDetected;
        public event Action OnMonsterLostTarget;

        private void Awake()
        {
            if (monsterAI == null)
                monsterAI = GetComponent<MonsterAI>();

            if (monsterAI != null)
            {
                monsterAI.OnStateChanged += HandleStateChanged;
                monsterAI.OnTargetChanged += HandleTargetChanged;
            }
        }

        private void OnDestroy()
        {
            if (monsterAI != null)
            {
                monsterAI.OnStateChanged -= HandleStateChanged;
                monsterAI.OnTargetChanged -= HandleTargetChanged;
            }
        }

        private void HandleStateChanged(MonsterAI.MonsterState state)
        {
            if (IsServer)
            {
                _stateIndex.Value = (int)state;
            }
        }

        private void HandleTargetChanged(Transform target)
        {
            if (IsServer)
            {
                if (target != null)
                {
                    _targetPosition.Value = target.position;
                    OnMonsterDetected?.Invoke();
                }
                else
                {
                    _targetPosition.Value = Vector3.zero;
                    OnMonsterLostTarget?.Invoke();
                }
            }
        }

        public void ServerRpcSetTarget(Vector3 targetPosition)
        {
            if (!IsServer) return;

            _targetPosition.Value = targetPosition;
            if (monsterAI != null)
            {
                var players = FindObjectsOfType<NetworkPlayer>();
                foreach (var player in players)
                {
                    if (Vector3.Distance(player.transform.position, targetPosition) < 1f)
                    {
                        monsterAI.SetTarget(player.transform);
                        break;
                    }
                }
            }
        }

        public void ClientRpcUpdatePosition(Vector3 position, Quaternion rotation)
        {
            transform.position = position;
            transform.rotation = rotation;
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            Debug.Log("[NetworkMonster] Monster spawned on network");

            if (IsServer && monsterAI != null)
            {
                monsterAI.OnStateChanged += HandleStateChanged;
            }
        }

        private void Update()
        {
            if (IsServer && monsterAI != null)
            {
                _stateIndex.Value = (int)monsterAI.CurrentState;
            }
        }
    }
}
