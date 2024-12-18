using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [Header("References")]
    public PlayerMovementStats moveStats;
    [SerializeField] private Collider2D _feetColl;
    [SerializeField] private Collider2D _bodyColl;

    private Rigidbody2D _rb;

    // Movement variables
    private Vector2 _moveVelocity;
    private bool _isFacingRight;

    // Collision variables
    private RaycastHit2D _groundHit;
    private RaycastHit2D _headHit;
    private bool _isGrounded;
    private bool _bumpedHead;

    // Jump variables
    [SerializeField] public float verticalVelocity { get; private set; }
    [SerializeField] private bool _isJumping;
    [SerializeField] private bool _isFastFalling;
    [SerializeField] private bool _isFalling;
    [SerializeField] private float _fastFallTime;
    [SerializeField] private float _fastFallReleaseSpeed;
    [SerializeField] private int _numberOfJumpsUsed;

    // Apex jump variables
    [SerializeField] private float _apexPoint;
    [SerializeField] private float _timePastApexThreshold;
    [SerializeField] private bool _isPastApexThreshold;

    // Jump buffer variables
    [SerializeField] private float _jumpBufferTimer;
    [SerializeField] private bool _jumpReleaseDuringBuffer;

    // Coyote time variables
    [SerializeField] private float _coyoteTimer;

    private void Awake()
    {
        _isFacingRight = true;

        _rb = GetComponent<Rigidbody2D>();
    }

    private void Update()
    {
        CountTimers();
        JumpChecks();

        Debug.Log("Vertical Vel: " + verticalVelocity);
    }

    private void FixedUpdate()
    {
        CollisionCheck();
        Jump();

        // Set movement acceleration and deceleration (ground or air)
        if (_isGrounded) Move(moveStats.groundAcceleration, moveStats.groundDeceleration, InputManager.Movement);
        else Move(moveStats.airAcceleration, moveStats.airDeceletation, InputManager.Movement);
    }

    #region Movement

    private void Move(float acceleration, float deceleration, Vector2 moveInput)
    {
        if (moveInput != Vector2.zero)
        {
            // Check if he needs to turn
            TurnCheck(moveInput);

            Vector2 targetVelocity = Vector2.zero;
            if (InputManager.RunIsHeld) targetVelocity = new Vector2(moveInput.x, 0f) * moveStats.maxRunSpeed;
            else targetVelocity = new Vector2(moveInput.x, 0f) * moveStats.maxWalkSpeed;

            _moveVelocity = Vector2.Lerp(_moveVelocity, targetVelocity, acceleration * Time.fixedDeltaTime);
            _rb.velocity = new Vector2(_moveVelocity.x, _rb.velocity.y);
        }

        else if (moveInput == Vector2.zero)
        {
            _moveVelocity = Vector2.Lerp(_moveVelocity, Vector2.zero, deceleration * Time.fixedDeltaTime);
            _rb.velocity = new Vector2(_moveVelocity.x, _rb.velocity.y);
        }
    }

    private void TurnCheck(Vector2 moveInput)
    {
        if (_isFacingRight && moveInput.x < 0) Turn(false);
        if (!_isFacingRight && moveInput.x > 0) Turn(true);
    }

    private void Turn(bool turnRight)
    {
        if (turnRight)
        {
            _isFacingRight = true;
            transform.Rotate(0f, 180f, 0f);
        }
        else
        {
            _isFacingRight = false;
            transform.Rotate(0f, -180f, 0f);
        }
    }

    #endregion

    #region Jump

    private void JumpChecks()
    {
        // Press jump button
        if (InputManager.JumpWasPressed)
        {
            _jumpBufferTimer = moveStats.jumpBufferTime;
            _jumpReleaseDuringBuffer = false;
        }

        // Release jump button
        if (InputManager.JumpWasReleased)
        {
            if (_jumpBufferTimer > 0f) _jumpReleaseDuringBuffer = true;

            if (_isJumping && verticalVelocity > 0f)
            {
                if (_isPastApexThreshold)
                {
                    _isPastApexThreshold = false;
                    _isFastFalling = true;
                    _fastFallTime = moveStats.timeForUpwardsCancel;
                    verticalVelocity = 0f;
                }
                else
                {
                    _isFastFalling = true;
                    _fastFallReleaseSpeed = verticalVelocity;
                }
            }
        }

        // Initiate jump with buffering and coyote time
        if (_jumpBufferTimer > 0f && !_isJumping && (_isGrounded || _coyoteTimer > 0f))
        {
            InitiateJump(1);

            if (_jumpReleaseDuringBuffer)
            {
                _isFastFalling = true;
                _fastFallReleaseSpeed = verticalVelocity;
            }
        }

        // Double jump
        else if (_jumpBufferTimer > 0f && _isJumping && _numberOfJumpsUsed < moveStats.numberOfjumpsAllowed)
        {
            _isFastFalling = false;
            InitiateJump(1);
        }

        // Air jump after coyote time
        else if (_jumpBufferTimer > 0f && _isFalling && _numberOfJumpsUsed < moveStats.numberOfjumpsAllowed - 1)
        {
            InitiateJump(2);
            _isFastFalling = false;
        }

        // Landing
        if ((_isJumping || _isFalling) && _isGrounded && verticalVelocity <= 0f)
        {
            _isJumping = false;
            _isFalling = false;
            _isFastFalling = false;
            _fastFallTime = 0f;
            _isPastApexThreshold = false;
            verticalVelocity = Physics2D.gravity.y;

            _numberOfJumpsUsed = 0;
        }
    }

    private void InitiateJump(int numberOfJumpUsed)
    {
        if (!_isJumping) _isJumping = true;

        _jumpBufferTimer = 0f;
        _numberOfJumpsUsed += numberOfJumpUsed;
        verticalVelocity = moveStats.initialJumpVelocity;
    }

    private void Jump()
    {
        // Gravity while jumping
        if (_isJumping)
        {
            // Check for head bump
            if (_bumpedHead) _isFastFalling = true;

            // Gravity on ascending
            if (verticalVelocity >= 0f)
            {
                // Apex controls
                _apexPoint = Mathf.InverseLerp(moveStats.initialJumpVelocity, 0f, verticalVelocity);

                if (_apexPoint > moveStats.apexThreshold)
                {
                    if (!_isPastApexThreshold)
                    {
                        _isPastApexThreshold = true;
                        _timePastApexThreshold = 0f;
                    }

                    if (_isPastApexThreshold)
                    {
                        _timePastApexThreshold += Time.fixedDeltaTime;
                        if (_timePastApexThreshold < moveStats.apexHangTime) verticalVelocity = 0f;
                        else verticalVelocity = -0.01f;
                    }
                }
            }

            // Gravity on descending but not past apex threshold
            else if (!_isFastFalling)
            {
                verticalVelocity += moveStats.gravity * Time.fixedDeltaTime;
                if (_isPastApexThreshold) _isPastApexThreshold = false;
            }
        }

        // Gravity on descending
        else if (!_isFastFalling)
        {
            verticalVelocity += moveStats.gravity * moveStats.gravityOnReleaseMultiplier * Time.fixedDeltaTime;
        }

        else if (verticalVelocity < 0f)
        {
            if (!_isFalling) _isFalling = true;
        }

        // Jump cut
        if (_isFastFalling)
        {
            if (_fastFallTime >= moveStats.timeForUpwardsCancel)
            {
                verticalVelocity += moveStats.gravity * moveStats.gravityOnReleaseMultiplier * Time.fixedDeltaTime;
            }
            else if (_fastFallTime < moveStats.timeForUpwardsCancel)
            {
                verticalVelocity = Mathf.Lerp(_fastFallReleaseSpeed, 0f, (_fastFallTime / moveStats.timeForUpwardsCancel));
            }

            _fastFallTime += Time.fixedDeltaTime;
        }

        // Gravity while falling
        if (!_isGrounded && !_isJumping)
        {
            if (!_isFalling) _isFalling = true;

            verticalVelocity += moveStats.gravity * Time.fixedDeltaTime;
        }

        // Clamp fall speed
        verticalVelocity = Mathf.Clamp(verticalVelocity, -moveStats.maxFallSpeed, 50f);

        _rb.velocity = new Vector2(_rb.velocity.x, verticalVelocity);
    }

    #endregion

    #region Collisions

    private void IsGrounded()
    {
        Vector2 boxCastOrigin = new Vector2(_feetColl.bounds.center.x, _feetColl.bounds.min.y);
        Vector2 boxCastSize = new Vector2(_feetColl.bounds.size.x, moveStats.groundDetectionRayLength);

        _groundHit = Physics2D.BoxCast(boxCastOrigin, boxCastSize, 0f, Vector2.down, moveStats.groundDetectionRayLength, moveStats.groundLayer);
        if (_groundHit.collider != null) _isGrounded = true;
        else _isGrounded = false;

        #region Debugging Grounded
        if (moveStats.debugShowIsGroundedBox)
        {
            Color rayColor;
            if (_isGrounded) rayColor = Color.green;
            else rayColor = Color.red;

            Debug.DrawRay(new Vector2(boxCastOrigin.x - boxCastSize.x / 2, boxCastOrigin.y), Vector2.down * moveStats.groundDetectionRayLength, rayColor);
            Debug.DrawRay(new Vector2(boxCastOrigin.x + boxCastSize.x / 2, boxCastOrigin.y), Vector2.down * moveStats.groundDetectionRayLength, rayColor);
            Debug.DrawRay(new Vector2(boxCastOrigin.x - boxCastSize.x / 2, boxCastOrigin.y - moveStats.groundDetectionRayLength), Vector2.right * boxCastSize.x, rayColor);
        }

        #endregion
    }

    private void BumpedHead()
    {
        Vector2 boxCastOrigin = new Vector2(_feetColl.bounds.center.x, _bodyColl.bounds.max.y);
        Vector2 boxCastSize = new Vector2(_feetColl.bounds.size.x * moveStats.headWidth, moveStats.headDetectionRayLength);

        _headHit = Physics2D.BoxCast(boxCastOrigin, boxCastSize, 0f, Vector2.up, moveStats.headDetectionRayLength, moveStats.groundLayer);
        if (_headHit.collider != null) _bumpedHead = true;
        else _bumpedHead = false;

        #region Debugging BumpedHead

        if (moveStats.debugShowHeadBumpBox)
        {
            float headWidth = moveStats.headWidth;

            Color rayColor;
            if (_bumpedHead) rayColor = Color.green;
            else rayColor = Color.red;

            Debug.DrawRay(new Vector2(boxCastOrigin.x - boxCastSize.x / 2 * headWidth, boxCastOrigin.y), Vector2.up * moveStats.headDetectionRayLength, rayColor);
            Debug.DrawRay(new Vector2(boxCastOrigin.x + (boxCastSize.x / 2) * headWidth, boxCastOrigin.y), Vector2.up * moveStats.headDetectionRayLength, rayColor);
            Debug.DrawRay(new Vector2(boxCastOrigin.x - boxCastSize.x / 2 * headWidth, boxCastOrigin.y + moveStats.headDetectionRayLength), Vector2.right * boxCastSize.x * headWidth, rayColor);
        }

        #endregion
    }

    private void CollisionCheck()
    {
        IsGrounded();
        BumpedHead();
    }

    #endregion

    #region Timers

    private void CountTimers()
    {
        _jumpBufferTimer -= Time.deltaTime;

        if (!_isGrounded) _coyoteTimer -= Time.deltaTime;
        else _coyoteTimer = moveStats.jumpCoyoteTime;
    }

    #endregion

}
