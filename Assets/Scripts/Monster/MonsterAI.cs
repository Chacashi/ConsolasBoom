using UnityEngine;
using UnityEngine.AI;
using System;
using System.Collections.Generic;
using HouseEscape.Systems;
using HouseEscape.Player;

namespace HouseEscape.Monster
{
    public class MonsterAI : MonoBehaviour
    {
        public enum MonsterState
        {
            Idle,
            Patrol,
            Chase,
            Attack
        }

        [Header("NavMesh")]
        [SerializeField] private float patrolSpeed = 3.5f;
        [SerializeField] private float chaseSpeed = 5.0f;
        [SerializeField] private float stopDistance = 1.5f;

        [Header("Detection")]
        [SerializeField] private float detectionRadius = 8f;
        [SerializeField] private LayerMask detectionMask;
        [SerializeField] private float loseInterestTime = 5f;

        [Header("Attack")]
        [SerializeField] private float attackDamage = 30f;
        [SerializeField] private float attackCooldown = 2f;
        [SerializeField] private float attackRange = 2f;

        [Header("Patrol Waypoints")]
        [SerializeField] private Transform[] patrolWaypoints;
        [SerializeField] private int waypointIndex;
        [SerializeField] private float waypointReachDistance = 1f;

        [Header("References")]
        [SerializeField] private NavMeshAgent navMeshAgent;
        [SerializeField] private LightCycleManager lightCycleManager;

        private MonsterState _currentState = MonsterState.Idle;
        private Transform _targetPlayer;
        private float _loseInterestTimer;
        private float _attackTimer;
        private bool _isLosingInterest;

        public event Action<MonsterState> OnStateChanged;
        public event Action<Transform> OnTargetChanged;
        public event Action<float> OnAttack;

        public MonsterState CurrentState => _currentState;
        public Transform TargetPlayer => _targetPlayer;
        public bool IsChasing => _currentState == MonsterState.Chase;

        private void Awake()
        {
            navMeshAgent = GetComponent<NavMeshAgent>();
            if (navMeshAgent == null)
                navMeshAgent = gameObject.AddComponent<NavMeshAgent>();

            navMeshAgent.speed = patrolSpeed;
            navMeshAgent.angularSpeed = 120f;
            navMeshAgent.acceleration = 8f;
        }

        private void Start()
        {
            if (patrolWaypoints != null && patrolWaypoints.Length > 0)
                waypointIndex = 0;
        }

        private void Update()
        {
            HandleState();
            HandleAttackCooldown();
        }

        private void HandleState()
        {
            switch (_currentState)
            {
                case MonsterState.Idle:
                    HandleIdle();
                    break;
                case MonsterState.Patrol:
                    HandlePatrol();
                    break;
                case MonsterState.Chase:
                    HandleChase();
                    break;
                case MonsterState.Attack:
                    HandleAttack();
                    break;
            }
        }

        private void HandleIdle()
        {
            bool lightsOn = lightCycleManager != null ? lightCycleManager.LightsAreOn : false;

            if (lightsOn)
            {
                ChangeState(MonsterState.Chase);
            }
            else
            {
                ChangeState(MonsterState.Patrol);
            }
        }

        private void HandlePatrol()
        {
            bool lightsOn = lightCycleManager != null ? lightCycleManager.LightsAreOn : false;

            if (lightsOn)
            {
                ChangeState(MonsterState.Chase);
                return;
            }

            if (patrolWaypoints == null || patrolWaypoints.Length == 0)
                return;

            if (!navMeshAgent.pathPending && !navMeshAgent.isStopped && navMeshAgent.remainingDistance > waypointReachDistance)
            {
                if (navMeshAgent.remainingDistance < waypointReachDistance && navMeshAgent.velocity.sqrMagnitude < 0.1f)
                {
                    NextWaypoint();
                }
            }
            else if (navMeshAgent.pathPending || navMeshAgent.isStopped)
            {
                NextWaypoint();
            }
        }

        private void HandleChase()
        {
            bool lightsOn = lightCycleManager != null ? lightCycleManager.LightsAreOn : true;

            if (!lightsOn && _targetPlayer == null)
            {
                ChangeState(MonsterState.Patrol);
                return;
            }

            if (_targetPlayer == null || !_targetPlayer.gameObject.activeInHierarchy)
            {
                _targetPlayer = null;
                _loseInterestTimer = 0f;
                _isLosingInterest = false;
                ChangeState(MonsterState.Patrol);
                return;
            }

            if (_isLosingInterest)
            {
                _loseInterestTimer -= Time.deltaTime;
                if (_loseInterestTimer <= 0f)
                {
                    _targetPlayer = null;
                    _isLosingInterest = false;
                    ChangeState(MonsterState.Patrol);
                }
            }
            else
            {
                navMeshAgent.isStopped = false;
                navMeshAgent.speed = chaseSpeed;
                navMeshAgent.SetDestination(_targetPlayer.position);

                if (Vector3.Distance(transform.position, _targetPlayer.position) < stopDistance)
                {
                    ChangeState(MonsterState.Attack);
                }
            }
        }

        private void HandleAttack()
        {
            if (_targetPlayer == null || !_targetPlayer.gameObject.activeInHierarchy)
            {
                _targetPlayer = null;
                ChangeState(MonsterState.Patrol);
                return;
            }

            navMeshAgent.isStopped = true;
            navMeshAgent.speed = 0f;

            if (Vector3.Distance(transform.position, _targetPlayer.position) > attackRange)
            {
                ChangeState(MonsterState.Chase);
                return;
            }

            if (Vector3.Distance(transform.position, _targetPlayer.position) > detectionRadius)
            {
                _isLosingInterest = true;
                _loseInterestTimer = loseInterestTime;
                ChangeState(MonsterState.Chase);
                return;
            }

            LookAtTarget();
        }

        private void HandleAttackCooldown()
        {
            if (_targetPlayer == null)
                return;

            _attackTimer += Time.deltaTime;

            if (_attackTimer >= attackCooldown)
            {
                _attackTimer = 0f;
                Attack();
            }
        }

        private void Attack()
        {
            if (_targetPlayer == null) return;

            var targetHealth = _targetPlayer.GetComponent<PlayerHealth>();
            if (targetHealth != null)
            {
                targetHealth.TakeDamage(attackDamage);
                OnAttack?.Invoke(attackDamage);
                Debug.Log("[MonsterAI] Attacking " + _targetPlayer.name + " for " + attackDamage + " damage");
            }
        }

        private void NextWaypoint()
        {
            if (patrolWaypoints == null || patrolWaypoints.Length == 0)
                return;

            waypointIndex = (waypointIndex + 1) % patrolWaypoints.Length;
            navMeshAgent.SetDestination(patrolWaypoints[waypointIndex].position);
        }

        private void LookAtTarget()
        {
            if (_targetPlayer == null) return;

            Vector3 lookDirection = _targetPlayer.position - transform.position;
            Quaternion targetRotation = Quaternion.LookRotation(lookDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 5f);
        }

        private void ChangeState(MonsterState newState)
        {
            if (_currentState == newState)
                return;

            _currentState = newState;
            OnStateChanged?.Invoke(newState);

            switch (newState)
            {
                case MonsterState.Idle:
                    navMeshAgent.isStopped = true;
                    break;
                case MonsterState.Patrol:
                    navMeshAgent.isStopped = false;
                    navMeshAgent.speed = patrolSpeed;
                    if (patrolWaypoints != null && patrolWaypoints.Length > 0)
                        NextWaypoint();
                    break;
                case MonsterState.Chase:
                    navMeshAgent.isStopped = false;
                    navMeshAgent.speed = chaseSpeed;
                    break;
                case MonsterState.Attack:
                    navMeshAgent.isStopped = true;
                    navMeshAgent.speed = 0f;
                    _attackTimer = 0f;
                    break;
            }

            Debug.Log("[MonsterAI] State changed: " + _currentState);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player") || other.GetComponentInParent<PlayerController>() != null)
            {
                if (Vector3.Distance(transform.position, other.transform.position) < detectionRadius)
                {
                    _targetPlayer = other.transform;
                    _isLosingInterest = false;
                    _loseInterestTimer = 0f;
                    ChangeState(MonsterState.Chase);
                    OnTargetChanged?.Invoke(_targetPlayer);
                    Debug.Log("[MonsterAI] Detected player: " + other.name);
                }
            }
        }

        private void OnTriggerStay(Collider other)
        {
            if (other.CompareTag("Player") || other.GetComponentInParent<PlayerController>() != null)
            {
                if (Vector3.Distance(transform.position, other.transform.position) < detectionRadius)
                {
                    if (_targetPlayer == null)
                    {
                        _targetPlayer = other.transform;
                        _isLosingInterest = false;
                        _loseInterestTimer = 0f;
                        ChangeState(MonsterState.Chase);
                        OnTargetChanged?.Invoke(_targetPlayer);
                    }
                    _isLosingInterest = false;
                }
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.CompareTag("Player") || other.GetComponentInParent<PlayerController>() != null)
            {
                if (Vector3.Distance(transform.position, other.transform.position) > detectionRadius)
                {
                    if (_targetPlayer != null && _targetPlayer == other.transform)
                    {
                        _isLosingInterest = true;
                        _loseInterestTimer = loseInterestTime;
                    }
                }
            }
        }

        public void SetTarget(Transform target)
        {
            _targetPlayer = target;
            _isLosingInterest = false;
            _loseInterestTimer = 0f;
            if (_currentState == MonsterState.Patrol || _currentState == MonsterState.Idle)
            {
                ChangeState(MonsterState.Chase);
            }
            OnTargetChanged?.Invoke(_targetPlayer);
        }

        public void ClearTarget()
        {
            _targetPlayer = null;
            _isLosingInterest = false;
            _loseInterestTimer = 0f;
            ChangeState(MonsterState.Patrol);
            OnTargetChanged?.Invoke(null);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, detectionRadius);

            Gizmos.color = new Color(1f, 0.5f, 0f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, attackRange);

            if (patrolWaypoints != null)
            {
                Gizmos.color = Color.cyan;
                for (int i = 0; i < patrolWaypoints.Length; i++)
                {
                    Gizmos.DrawWireSphere(patrolWaypoints[i].position, 0.5f);
                    Gizmos.color = new Color(0f, 1f, 1f, 0.3f);
                    Gizmos.DrawRay(patrolWaypoints[i].position, Vector3.up);
                    Gizmos.color = Color.cyan;
                }
            }
        }
    }
}
