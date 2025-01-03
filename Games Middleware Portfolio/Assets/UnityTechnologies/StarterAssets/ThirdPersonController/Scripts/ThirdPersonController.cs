﻿using System.Collections.Generic;
using UnityEngine;
using System;
using System.Collections;
using System.Linq;
using UnityEngine.Windows.Speech;
using Unity.Netcode;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/* Note: animations are called via the controller for both the character and capsule using animator null checks
 */

namespace StarterAssets
{
    [RequireComponent(typeof(CharacterController))]
#if ENABLE_INPUT_SYSTEM 
    [RequireComponent(typeof(PlayerInput))]
#endif
    public class ThirdPersonController : NetworkBehaviour
    {
        [Header("Player")]
        [Tooltip("Move speed of the character in m/s")]
        public float MoveSpeed = 2.0f;

        [Tooltip("Sprint speed of the character in m/s")]
        public float SprintSpeed = 5.335f;

        [Tooltip("How fast the character turns to face movement direction")]
        [Range(0.0f, 0.3f)]
        public float RotationSmoothTime = 0.12f;

        [Tooltip("Acceleration and deceleration")]
        public float SpeedChangeRate = 10.0f;

        public AudioClip LandingAudioClip;
        public AudioClip[] FootstepAudioClips;
        [Range(0, 1)] public float FootstepAudioVolume = 0.5f;

        [Space(10)]
        [Tooltip("The height the player can jump")]
        public float JumpHeight = 1.2f;

        [Tooltip("The character uses its own gravity value. The engine default is -9.81f")]
        public float Gravity = -15.0f;

        [Space(10)]
        [Tooltip("Time required to pass before being able to jump again. Set to 0f to instantly jump again")]
        public float JumpTimeout = 0.50f;

        [Tooltip("Time required to pass before entering the fall state. Useful for walking down stairs")]
        public float FallTimeout = 0.15f;

        [Header("Player Grounded")]
        [Tooltip("If the character is grounded or not. Not part of the CharacterController built in grounded check")]
        public bool Grounded = true;

        [Tooltip("Useful for rough ground")]
        public float GroundedOffset = -0.14f;

        [Tooltip("The radius of the grounded check. Should match the radius of the CharacterController")]
        public float GroundedRadius = 0.28f;

        [Tooltip("What layers the character uses as ground")]
        public LayerMask GroundLayers;

        [Header("Cinemachine")]
        [Tooltip("The follow target set in the Cinemachine Virtual Camera that the camera will follow")]
        public GameObject CinemachineCameraTarget;

        [Tooltip("How far in degrees can you move the camera up")]
        public float TopClamp = 70.0f;

        [Tooltip("How far in degrees can you move the camera down")]
        public float BottomClamp = -30.0f;

        [Tooltip("Additional degress to override the camera. Useful for fine tuning camera position when locked")]
        public float CameraAngleOverride = 0.0f;

        [Tooltip("For locking the camera position on all axis")]
        public bool LockCameraPosition = false;

        // cinemachine
        private float _cinemachineTargetYaw;
        private float _cinemachineTargetPitch;

        // player
        private float _speed;
        private float _animationBlend;
        private float _targetRotation = 0.0f;
        private float _rotationVelocity;
        private float _verticalVelocity;
        private float _terminalVelocity = 53.0f;

        // timeout deltatime
        private float _jumpTimeoutDelta;
        private float _fallTimeoutDelta;

        // animation IDs
        private int _animIDSpeed;
        private int _animIDGrounded;
        private int _animIDJump;
        private int _animIDFreeFall;
        private int _animIDMotionSpeed;

        private bool isMovingForward;
        private bool isMovingBackward;
        private bool isMovingLeft;
        private bool isMovingRight;
        private bool isJumping;
        private bool isStopped;

#if ENABLE_INPUT_SYSTEM 
        private PlayerInput _playerInput;
#endif
        private Animator _animator;
        private CharacterController _controller;
        private StarterAssetsInputs _input;
        private GameObject _mainCamera;

        private const float _threshold = 0.01f;

        private bool _hasAnimator;
        List<CubeView> allCatchableItems;
        CubeView cubeView;

        private KeywordRecognizer keywordRecognizer;
        private Dictionary<string, Action> voiceActions = new Dictionary<string, Action>();

        private bool IsCurrentDeviceMouse
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                return _playerInput.currentControlScheme == "KeyboardMouse";
#else
				return false;
#endif
            }
        }

        public override void OnNetworkSpawn()
        {
            if (!IsOwner) 
            { 
                Destroy(this); 
            }
        }


        private void Awake()
        {
            if (_mainCamera == null)
            {
                _mainCamera = GameObject.FindGameObjectWithTag("MainCamera");
                if (!_animator) { gameObject.GetComponent<Animator>(); }

                allCatchableItems = FindObjectsOfType<CubeView>().ToList();
            }
        }

        void Start()
        {
            InitializeVoiceCommands();
            _cinemachineTargetYaw = CinemachineCameraTarget.transform.rotation.eulerAngles.y;

            _hasAnimator = TryGetComponent(out _animator);
            _controller = GetComponent<CharacterController>();
            _input = GetComponent<StarterAssetsInputs>();
#if ENABLE_INPUT_SYSTEM 
            _playerInput = GetComponent<PlayerInput>();
#else
			Debug.LogError( "Starter Assets package is missing dependencies. Please use Tools/Starter Assets/Reinstall Dependencies to fix it");
#endif

            AssignAnimationIDs();

            _jumpTimeoutDelta = JumpTimeout;
            _fallTimeoutDelta = FallTimeout;


        }

        private void InitializeVoiceCommands()
        {
            voiceActions.Add("forward", () =>
            {
                isMovingForward = true;
                ClearOtherMovementFlags("forward");
            });
            voiceActions.Add("back", () =>
            {
                isMovingBackward = true;
                ClearOtherMovementFlags("back");
            });
            voiceActions.Add("left", () =>
            {
                isMovingLeft = true;
                ClearOtherMovementFlags("left");
            });
            voiceActions.Add("right", () =>
            {
                isMovingRight = true;
                ClearOtherMovementFlags("right");
            });
            voiceActions.Add("jump", () => isJumping = true);
            voiceActions.Add("stop", () => isStopped = true);

            keywordRecognizer = new KeywordRecognizer(voiceActions.Keys.ToArray());
            keywordRecognizer.OnPhraseRecognized += OnVoiceCommandRecognized;
            keywordRecognizer.Start();
        }

        private void ClearOtherMovementFlags(string activeDirection)
        {
            // Disable all other movement flags except the one passed
            isMovingForward = activeDirection == "forward";
            isMovingBackward = activeDirection == "back";
            isMovingLeft = activeDirection == "left";
            isMovingRight = activeDirection == "right";
        }

        private CubeView ClosestObject()
        {
            CubeView closestObject = null;
            float closestDistance = float.MaxValue;
            foreach (CubeView obj in allCatchableItems)
            {
                float distance = Vector3.Distance(transform.position, obj.transform.position);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestObject = obj;
                }
            }
            return closestObject;
        }

        private void Update()
        {
            _hasAnimator = TryGetComponent(out _animator);

            JumpAndGravity();
            GroundedCheck();

            Vector3 keyboardInput = new Vector3(_input.move.x, _input.move.y, 0);

            Vector3 voiceInput = Vector3.zero;

            if (isMovingForward) voiceInput += new Vector3(0, -1, 0);
            if (isMovingBackward) voiceInput += new Vector3(0, 1, 0);
            if (isMovingLeft) voiceInput += new Vector3(-1, 0, 0);
            if (isMovingRight) voiceInput += new Vector3(1, 0, 0);

            Vector3 combinedInput = keyboardInput + voiceInput;

            if (combinedInput.magnitude > 1)
            {
                combinedInput = combinedInput.normalized;
            }

            _input.move = combinedInput;

            if (isJumping)
            {
                _input.jump = true;
                isJumping = false;
            }

            if (isStopped)
            {
                isMovingForward = false;
                isMovingBackward = false;
                isMovingLeft = false;
                isMovingRight = false;

                _input.move = Vector3.zero;
                isStopped = false;
            }

            Move();

            if (Input.GetKeyDown(KeyCode.J))
            {
                if (_animator != null)
                {
                    _animator.SetTrigger("Talk");
                }
            }

            if (Input.GetKeyDown(KeyCode.P))
            {
                cubeView = ClosestObject();
            }

            if (Input.GetKeyDown(KeyCode.E) && cubeView != null)
            {
                float distanceToFocus = Vector3.Distance(transform.position, cubeView.transform.position);
                if (distanceToFocus <= 2.0f)
                {
                    PickUpObject(cubeView);
                }
            }
            if (Input.GetKeyDown(KeyCode.T) && cubeView != null)
            {
                _animator.SetTrigger("Throw");
                ThrowObject(cubeView);
            }
        }

        private void PickUpObject(CubeView obj)
        {
            Transform handTransform = _animator.GetBoneTransform(HumanBodyBones.RightHand);
            obj.transform.SetParent(handTransform);
            obj.transform.localPosition = Vector3.zero;
            obj.transform.localRotation = Quaternion.identity;

            // Disable physics on the object
            Rigidbody objRigidbody = obj.GetComponent<Rigidbody>();
            if (objRigidbody != null)
            {
                objRigidbody.isKinematic = true;
            }
            // m_animator.SetTrigger("PickUp");
        }

        private void ThrowObject(CubeView obj)
        {
            if (obj == null) return;

            // Check if the object is already attached to the right hand
            Transform handTransform = _animator.GetBoneTransform(HumanBodyBones.RightHand);
            if (obj.transform.parent != handTransform)
            {
                Debug.LogWarning("Object is not attached to the right hand!");
                return;
            }

            // Detach and throw the object
            obj.transform.SetParent(null);
            Rigidbody objRigidbody = obj.GetComponent<Rigidbody>();
            if (objRigidbody != null)
            {
                objRigidbody.isKinematic = false;
                Vector3 throwDirection = -handTransform.forward + Vector3.up;
                float throwForce = 7.5f;
                objRigidbody.AddForce(throwDirection * throwForce, ForceMode.Impulse);
            }
            cubeView = null;
        }

        private void LateUpdate()
        {
            CameraRotation();

            Transform headTransform = _animator.GetBoneTransform(HumanBodyBones.Head);

            if (cubeView)
            {
                Vector3 newForward = (cubeView.transform.position - headTransform.position).normalized;
                Vector3 newRight = (Vector3.down - Vector3.Dot(Vector3.down, newForward) * newForward).normalized;
                Vector3 newUp = Vector3.Cross(newForward, newRight);
                headTransform.rotation = Quaternion.LookRotation(newForward, newUp);
            }

            //Vector3 lookat = new Vector3(2,20,3);

            //headTransform.LookAt(lookat);
        }

        private void AssignAnimationIDs()
        {
            _animIDSpeed = Animator.StringToHash("Speed");
            _animIDGrounded = Animator.StringToHash("Grounded");
            _animIDJump = Animator.StringToHash("Jump");
            _animIDFreeFall = Animator.StringToHash("FreeFall");
            _animIDMotionSpeed = Animator.StringToHash("MotionSpeed");
        }

        private void GroundedCheck()
        {
            // set sphere position, with offset
            Vector3 spherePosition = new Vector3(transform.position.x, transform.position.y - GroundedOffset,
                transform.position.z);
            Grounded = Physics.CheckSphere(spherePosition, GroundedRadius, GroundLayers,
                QueryTriggerInteraction.Ignore);

            // update animator if using character
            if (_hasAnimator)
            {
                _animator.SetBool(_animIDGrounded, Grounded);
            }
        }

        private void OnVoiceCommandRecognized(PhraseRecognizedEventArgs args)
        {
            Debug.Log($"Recognized Voice Command: {args.text}");
            if (voiceActions.ContainsKey(args.text))
            {
                voiceActions[args.text].Invoke();
            }
        }



        private void CameraRotation()
        {
            // if there is an input and camera position is not fixed
            if (_input.look.sqrMagnitude >= _threshold && !LockCameraPosition)
            {
                //Don't multiply mouse input by Time.deltaTime;
                float deltaTimeMultiplier = IsCurrentDeviceMouse ? 1.0f : Time.deltaTime;

                _cinemachineTargetYaw += _input.look.x * deltaTimeMultiplier;
                _cinemachineTargetPitch += _input.look.y * deltaTimeMultiplier;
            }

            // clamp our rotations so our values are limited 360 degrees
            _cinemachineTargetYaw = ClampAngle(_cinemachineTargetYaw, float.MinValue, float.MaxValue);
            _cinemachineTargetPitch = ClampAngle(_cinemachineTargetPitch, BottomClamp, TopClamp);

            // Cinemachine will follow this target
            CinemachineCameraTarget.transform.rotation = Quaternion.Euler(_cinemachineTargetPitch + CameraAngleOverride,
                _cinemachineTargetYaw, 0.0f);
        }

        private void Move()
        {
            // set target speed based on move speed, sprint speed and if sprint is pressed
            float targetSpeed = _input.sprint ? SprintSpeed : MoveSpeed;

            // a simplistic acceleration and deceleration designed to be easy to remove, replace, or iterate upon

            // note: Vector2's == operator uses approximation so is not floating point error prone, and is cheaper than magnitude
            // if there is no input, set the target speed to 0
            if (_input.move == Vector2.zero) targetSpeed = 0.0f;

            // a reference to the players current horizontal velocity
            float currentHorizontalSpeed = new Vector3(_controller.velocity.x, 0.0f, _controller.velocity.z).magnitude;

            float speedOffset = 0.1f;
            float inputMagnitude = _input.analogMovement ? _input.move.magnitude : 1f;

            // accelerate or decelerate to target speed
            if (currentHorizontalSpeed < targetSpeed - speedOffset ||
                currentHorizontalSpeed > targetSpeed + speedOffset)
            {
                // creates curved result rather than a linear one giving a more organic speed change
                // note T in Lerp is clamped, so we don't need to clamp our speed
                _speed = Mathf.Lerp(currentHorizontalSpeed, targetSpeed * inputMagnitude,
                    Time.deltaTime * SpeedChangeRate);

                // round speed to 3 decimal places
                _speed = Mathf.Round(_speed * 1000f) / 1000f;
            }
            else
            {
                _speed = targetSpeed;
            }

            _animationBlend = Mathf.Lerp(_animationBlend, targetSpeed, Time.deltaTime * SpeedChangeRate);
            if (_animationBlend < 0.01f) _animationBlend = 0f;

            // normalise input direction
            Vector3 inputDirection = new Vector3(_input.move.x, 0.0f, _input.move.y).normalized;

            // note: Vector2's != operator uses approximation so is not floating point error prone, and is cheaper than magnitude
            // if there is a move input rotate player when the player is moving
            if (_input.move != Vector2.zero)
            {
                _targetRotation = Mathf.Atan2(inputDirection.x, inputDirection.z) * Mathf.Rad2Deg +
                                  _mainCamera.transform.eulerAngles.y;
                float rotation = Mathf.SmoothDampAngle(transform.eulerAngles.y, _targetRotation, ref _rotationVelocity,
                    RotationSmoothTime);

                // rotate to face input direction relative to camera position
                transform.rotation = Quaternion.Euler(0.0f, rotation, 0.0f);
            }


            Vector3 targetDirection = Quaternion.Euler(0.0f, _targetRotation, 0.0f) * Vector3.forward;

            // move the player
            _controller.Move(targetDirection.normalized * (_speed * Time.deltaTime) +
                             new Vector3(0.0f, _verticalVelocity, 0.0f) * Time.deltaTime);

            // update animator if using character
            if (_hasAnimator)
            {
                _animator.SetFloat(_animIDSpeed, _animationBlend);
                _animator.SetFloat(_animIDMotionSpeed, inputMagnitude);
            }
        }

        private void JumpAndGravity()
        {
            if (Grounded)
            {
                // reset the fall timeout timer
                _fallTimeoutDelta = FallTimeout;

                // update animator if using character
                if (_hasAnimator)
                {
                    _animator.SetBool(_animIDJump, false);
                    _animator.SetBool(_animIDFreeFall, false);
                }

                // stop our velocity dropping infinitely when grounded
                if (_verticalVelocity < 0.0f)
                {
                    _verticalVelocity = -2f;
                }

                // Jump
                if (_input.jump && _jumpTimeoutDelta <= 0.0f)
                {
                    // the square root of H * -2 * G = how much velocity needed to reach desired height
                    _verticalVelocity = Mathf.Sqrt(JumpHeight * -2f * Gravity);

                    // update animator if using character
                    if (_hasAnimator)
                    {
                        _animator.SetBool(_animIDJump, true);
                    }
                }

                // jump timeout
                if (_jumpTimeoutDelta >= 0.0f)
                {
                    _jumpTimeoutDelta -= Time.deltaTime;
                }
            }
            else
            {
                // reset the jump timeout timer
                _jumpTimeoutDelta = JumpTimeout;

                // fall timeout
                if (_fallTimeoutDelta >= 0.0f)
                {
                    _fallTimeoutDelta -= Time.deltaTime;
                }
                else
                {
                    // update animator if using character
                    if (_hasAnimator)
                    {
                        _animator.SetBool(_animIDFreeFall, true);
                    }
                }

                // if we are not grounded, do not jump
                _input.jump = false;
            }

            // apply gravity over time if under terminal (multiply by delta time twice to linearly speed up over time)
            if (_verticalVelocity < _terminalVelocity)
            {
                _verticalVelocity += Gravity * Time.deltaTime;
            }
        }

        private static float ClampAngle(float lfAngle, float lfMin, float lfMax)
        {
            if (lfAngle < -360f) lfAngle += 360f;
            if (lfAngle > 360f) lfAngle -= 360f;
            return Mathf.Clamp(lfAngle, lfMin, lfMax);
        }

        private void OnFootstep(AnimationEvent animationEvent)
        {
            if (animationEvent.animatorClipInfo.weight > 0.5f)
            {
                if (FootstepAudioClips.Length > 0)
                {
                    var index = UnityEngine.Random.Range(0, FootstepAudioClips.Length);
                    AudioSource.PlayClipAtPoint(FootstepAudioClips[index], transform.TransformPoint(_controller.center), FootstepAudioVolume);
                }
            }
        }

        private void OnLand(AnimationEvent animationEvent)
        {
            if (animationEvent.animatorClipInfo.weight > 0.5f)
            {
                AudioSource.PlayClipAtPoint(LandingAudioClip, transform.TransformPoint(_controller.center), FootstepAudioVolume);
            }
        }
    }
}